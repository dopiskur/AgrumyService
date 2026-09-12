using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Notifications;
using Agrumy.Api.Satellite;
using Agrumy.Shared.Models;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Daily per-organization satellite sync (Detaljni dizajn S, B3) - backfills a zone's whole archive once (Statistical API, stats only), then incrementally ingests new scenes since the last one (Process API, raw grid for scalar indices). One organization's failure/quota-pause never blocks another (D1/D12). The external tick (SatelliteSyncBackgroundService) still fires once a day; an organization with SyncIntervalDays &gt; 1 just gets skipped on the ticks it isn't due for yet.
    public sealed class SatelliteSyncEvaluator(
        ISatelliteConfigRepository configRepo, IFarmParcelRepository farmParcelRepo, ISatelliteSceneRepository sceneRepo,
        ISatelliteImagerySourceFactory sourceFactory, IUserRepository userRepo, INotificationDispatcher dispatcher,
        ILogger<SatelliteSyncEvaluator> logger)
    {
        private static readonly DateOnly ArchiveStartDate = new(2017, 1, 1);
        private const double QuotaPauseThresholdPercent = 90.0;
        // The daily tick and a SyncNow enqueue can overlap - two runs backfilling the same zone race on the scene unique index and one of them dies mid-zone, so a second trigger queues behind the first.
        private static readonly SemaphoreSlim RunLock = new(1, 1);

        /// isManualTrigger (FarmOpenfieldApiController.SatelliteMapSyncNow) bypasses every organization's own SyncIntervalDays gate entirely and never touches LastAutoSyncUtc - the daily background tick is the only writer of that field, so a manual sync can never shift or reset it.
        public async Task RunOnceAsync(bool isManualTrigger = false, CancellationToken ct = default)
        {
            await RunLock.WaitAsync(ct);
            try
            {
                IList<TenantSatelliteConfig> configs = await configRepo.SatelliteConfigsGetEnabledAsync();
                foreach (TenantSatelliteConfig config in configs)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!isManualTrigger && config.LastAutoSyncUtc is DateTimeOffset lastAutoSync
                        && DateTimeOffset.UtcNow - lastAutoSync < TimeSpan.FromDays(config.SyncIntervalDays))
                    {
                        continue; // this organization's own interval hasn't elapsed since the last automatic run
                    }
                    try
                    {
                        await RunForTenantAsync(config, ct);
                    }
                    catch (Exception ex)
                    {
                        // A token/auth failure (or any other organization-level error) stops THIS organization's tick, never the whole job.
                        logger.LogWarning(ex, "Satellite sync failed for tenant {TenantId}.", config.IDTenant);
                        await NotifyAsync(config.IDTenant, NotificationEventType.SatelliteSyncFailed, "Agrumy: satellite sync failed",
                            $"The satellite sync job failed for your tenant: {ex.Message}. It will retry on the next scheduled run.", ct);
                    }
                    finally
                    {
                        if (!isManualTrigger)
                        {
                            await configRepo.SatelliteConfigLastAutoSyncSetAsync(config.IDTenant, DateTimeOffset.UtcNow);
                        }
                    }
                }
            }
            finally
            {
                RunLock.Release();
            }
        }

        private async Task RunForTenantAsync(TenantSatelliteConfig config, CancellationToken ct)
        {
            if (config.QuotaPausedUntilUtc is DateTimeOffset pausedUntil)
            {
                if (DateTimeOffset.UtcNow < pausedUntil)
                {
                    return; // still paused - the on-demand grid-fetch path (API read endpoints) is the only thing still allowed
                }
                await configRepo.SatelliteConfigQuotaSnapshotSetAsync(config.IDTenant, config.LastQuotaSnapshotJson ?? "{}", null);
                await configRepo.SatelliteConfigQuotaPausedNotifiedAsync(config.IDTenant, null);
            }

            ISatelliteImagerySource? source = await sourceFactory.ForAsync(config.IDTenant, ct);
            if (source == null)
            {
                return;
            }

            QuotaSnapshot quota = await source.QuotaStatusAsync(config.IDTenant, ct);
            await configRepo.SatelliteConfigQuotaSnapshotSetAsync(config.IDTenant, quota.RawJson, null);
            if (quota.UsedPercent >= QuotaPauseThresholdPercent)
            {
                DateTimeOffset pauseUntil = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
                await configRepo.SatelliteConfigQuotaSnapshotSetAsync(config.IDTenant, quota.RawJson, pauseUntil);
                if (config.QuotaPausedNotifiedAtUtc == null)
                {
                    await NotifyAsync(config.IDTenant, NotificationEventType.SatelliteQuotaPaused, "Agrumy: satellite quota paused",
                        $"Your satellite imagery quota has reached {quota.UsedPercent:0.#}% - syncing is paused until {pauseUntil:yyyy-MM-dd}. Viewing already-ingested data still works; opening an older date still costs one on-demand call.", ct);
                    await configRepo.SatelliteConfigQuotaPausedNotifiedAsync(config.IDTenant, DateTimeOffset.UtcNow);
                }
                return;
            }

            SatelliteCapabilities capabilities = source.GetCapabilities(config.Collection);
            IReadOnlyList<SatelliteIndex> indices = config.DefaultIndices.Count > 0 ? config.DefaultIndices : capabilities.Indices;
            IList<FarmParcelZone> zones = await farmParcelRepo.FarmParcelZonesWithGeometryGetAsync(config.IDTenant);

            int processedZones = 0;
            foreach (FarmParcelZone zone in zones)
            {
                if (processedZones >= capabilities.MaxParcelsPerDay)
                {
                    break;
                }
                if (zone.GeometryGeoJson is not string geometry || zone.BboxMinLat is not double minLat || zone.BboxMinLon is not double minLon
                    || zone.BboxMaxLat is not double maxLat || zone.BboxMaxLon is not double maxLon || zone.IDFarmParcelZone is not int idZone)
                {
                    continue;
                }
                try
                {
                    var bbox = new BoundingBox(minLat, minLon, maxLat, maxLon);
                    if (zone.SatelliteBackfillCompletedUtc == null)
                    {
                        await BackfillZoneAsync(source, config.IDTenant, config.Collection, config.CommercialCollectionId, idZone, bbox, geometry, indices, config.MaxCloudPercent, config.MinValidPixelPercent, ct);
                    }
                    else
                    {
                        await IngestNewScenesAsync(source, config.IDTenant, config.Collection, config.CommercialCollectionId, idZone, bbox, geometry, indices, config.MaxCloudPercent, config.MinValidPixelPercent, ct);
                    }
                }
                catch (Exception ex)
                {
                    // Per-zone failures don't stop the organization's other zones - the zone just retries next tick.
                    logger.LogWarning(ex, "Satellite sync failed for zone {ZoneId} (tenant {TenantId}).", idZone, config.IDTenant);
                }
                processedZones++;
            }
        }

        private static IReadOnlyDictionary<SatelliteIndex, Evalscripts.Definition> EvalscriptDefinitionsFor(SatelliteCollection collection) => collection switch
        {
            SatelliteCollection.Sentinel2 => Evalscripts.All,
            SatelliteCollection.PlanetScope => Evalscripts.PlanetScope,
            _ => new Dictionary<SatelliteIndex, Evalscripts.Definition>(), // Pleiades - no evalscripts wired up yet (capabilities-only, see CdseSentinelHubSource's own doc comment); an empty map means the indices.Where(...) filters below simply skip every index for it.
        };

        private async Task BackfillZoneAsync(ISatelliteImagerySource source, int tenantId, SatelliteCollection collection, string? commercialCollectionId, int zoneId, BoundingBox bbox, string geometry, IReadOnlyList<SatelliteIndex> indices, int maxCloudPercent, int minValidPixelPercent, CancellationToken ct)
        {
            IReadOnlyDictionary<SatelliteIndex, Evalscripts.Definition> evalscripts = EvalscriptDefinitionsFor(collection);
            foreach (SatelliteIndex index in indices.Where(i => evalscripts.TryGetValue(i, out var d) && d.HasStatistics))
            {
                IReadOnlyList<SceneStatEntry> entries = await source.BackfillStatisticsAsync(tenantId, collection, commercialCollectionId, bbox, geometry, ArchiveStartDate, DateOnly.FromDateTime(DateTime.UtcNow), index, maxCloudPercent, ct);
                foreach (SceneStatEntry entry in entries)
                {
                    FarmParcelZoneSatelliteScene? scene = await sceneRepo.SceneGetBySourceIdAsync(zoneId, entry.SourceSceneId)
                        ?? await sceneRepo.SceneAddAsync(new FarmParcelZoneSatelliteScene
                        {
                            FarmParcelZoneID = zoneId,
                            SceneDateUtc = entry.SceneDateUtc,
                            SourceSceneId = entry.SourceSceneId,
                            CloudPercent = entry.CloudPercent,
                            ValidPixelPercent = entry.ValidPixelPercent,
                            Reliable = entry.ValidPixelPercent >= minValidPixelPercent,
                        });
                    await sceneRepo.IndexUpsertAsync(new ParcelSatelliteIndex
                    {
                        SceneID = scene!.IDFarmParcelZoneSatelliteScene,
                        Index = index,
                        StatsJson = System.Text.Json.JsonSerializer.Serialize(entry.Stats),
                        GridBase64 = null, // D9/D10 - fetched on first view, not during backfill
                    });
                }
            }
            await sceneRepo.FarmParcelZoneBackfillCompletedSetAsync(zoneId, DateTimeOffset.UtcNow);
            await NotifyAsync(tenantId, NotificationEventType.SatelliteBackfillCompleted, "Agrumy: satellite backfill completed",
                $"The full historical archive (since {ArchiveStartDate:yyyy-MM-dd}) has been ingested for one of your zones - statistics are now available on its Satellite tab.", ct);
        }

        private async Task IngestNewScenesAsync(ISatelliteImagerySource source, int tenantId, SatelliteCollection collection, string? commercialCollectionId, int zoneId, BoundingBox bbox, string geometry, IReadOnlyList<SatelliteIndex> indices, int maxCloudPercent, int minValidPixelPercent, CancellationToken ct)
        {
            FarmParcelZoneSatelliteScene? latest = await sceneRepo.LatestSceneGetAsync(zoneId);
            DateOnly fromDate = latest?.SceneDateUtc.AddDays(1) ?? ArchiveStartDate;
            DateOnly toDate = DateOnly.FromDateTime(DateTime.UtcNow);
            if (fromDate > toDate)
            {
                return;
            }
            IReadOnlyDictionary<SatelliteIndex, Evalscripts.Definition> evalscripts = EvalscriptDefinitionsFor(collection);

            IReadOnlyList<SceneCandidate> candidates = await source.FindScenesAsync(tenantId, collection, commercialCollectionId, bbox, fromDate, toDate, maxCloudPercent, ct);
            // Overlapping Sentinel-2 tiles list the same acquisition date twice - one render per date is all the map/series can use, so keep the clearer one.
            IEnumerable<SceneCandidate> oneScenePerDate = candidates.GroupBy(c => c.SceneDateUtc).Select(g => g.OrderBy(c => c.CloudPercent).First()).OrderBy(c => c.SceneDateUtc);
            foreach (SceneCandidate candidate in oneScenePerDate)
            {
                if (await sceneRepo.SceneGetBySourceIdAsync(zoneId, candidate.SourceSceneId) != null)
                {
                    continue; // idempotent - already ingested (ux_farmParcelZoneSatelliteScene_zone_scene backs this up at the DB level too)
                }

                double worstValidPercent = 100;
                var renders = new List<(SatelliteIndex Index, IndexRender Render)>();
                foreach (SatelliteIndex index in indices.Where(evalscripts.ContainsKey))
                {
                    // PNG is never generated at ingest time (D10) - only the raw grid for scalar indices; composites store nothing here and render on first view instead, same lazy path.
                    IndexRender render = await source.RenderIndexAsync(tenantId, collection, commercialCollectionId, candidate, geometry, index, renderPng: false, renderGrid: evalscripts[index].HasStatistics, ct);
                    renders.Add((index, render));
                    worstValidPercent = Math.Min(worstValidPercent, render.ValidPixelPercent);
                }

                FarmParcelZoneSatelliteScene scene = await sceneRepo.SceneAddAsync(new FarmParcelZoneSatelliteScene
                {
                    FarmParcelZoneID = zoneId,
                    SceneDateUtc = candidate.SceneDateUtc,
                    SourceSceneId = candidate.SourceSceneId,
                    CloudPercent = candidate.CloudPercent,
                    ValidPixelPercent = worstValidPercent,
                    Reliable = worstValidPercent >= minValidPixelPercent,
                });
                foreach ((SatelliteIndex index, IndexRender render) in renders)
                {
                    await sceneRepo.IndexUpsertAsync(new ParcelSatelliteIndex
                    {
                        SceneID = scene.IDFarmParcelZoneSatelliteScene,
                        Index = index,
                        BoundsJson = render.BoundsJson,
                        StatsJson = render.StatsJson,
                        GridBase64 = render.GridRaw == null ? null : Convert.ToBase64String(render.GridRaw),
                    });
                }
            }
        }

        private async Task NotifyAsync(int tenantId, NotificationEventType eventType, string subject, string body, CancellationToken ct)
        {
            IReadOnlyList<NotificationRecipient> recipients = await NotificationRecipientBuilder.BuildForTenantAdminsAsync(userRepo, tenantId, eventType);
            if (recipients.Count > 0)
            {
                await dispatcher.DispatchToRecipientsAsync(subject, body, NotificationSeverity.Warning, recipients, ct: ct);
            }
        }
    }
}
