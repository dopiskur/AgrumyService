using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Migration
{
    /// Applies a TenantExport to this server (see Agrumy.Shared.Models.TenantImportTarget for ByName vs AsSentinel) - every id on the target is freshly assigned, stitched back together via the *IdMap dictionaries below.
    public class TenantImportService(
        ITenantRepository tenantRepo, IUserRepository userRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IDeviceRepository deviceRepo, ISensorDataRepository sensorDataRepo,
        IFarmOpenfieldRepository farmOpenfieldRepo, IFarmParcelRepository farmParcelRepo, ISowingRepository sowingRepo, ICropCatalogRepository cropCatalogRepo,
        IFieldLogRepository fieldLogRepo, IZonePlantingRepository zonePlantingRepo)
    {
        /// ByName: ties to an existing organization with this exact name, or creates one; GlobalAdmin-only at the controller layer.
        public async Task<TenantImportResult> ImportByNameAsync(TenantExport export, string targetTenantName)
        {
            int tenantId = await tenantRepo.TenantGetIdAsync(targetTenantName) ?? await tenantRepo.TenantAddAsync(targetTenantName);
            return await ImportIntoTenantAsync(export, tenantId, targetTenantName);
        }

        /// AsSentinel: TenantID=0, only while TenantZeroIsEmptyAsync - reachable without an admin session (a brand-new self-hosted server has nobody to log in as yet); discards the pending bootstrap admin placeholder first.
        public async Task<(TenantImportResult? result, string? error)> ImportAsSentinelAsync(TenantExport export)
        {
            if (!await tenantRepo.TenantZeroIsEmptyAsync())
            {
                return (null, "TenantID 0 on this server already has data - import-as-sentinel refuses to merge into or overwrite an existing installation.");
            }
            await userRepo.BootstrapAdminDiscardPendingAsync();
            return (await ImportIntoTenantAsync(export, 0, "Default"), null);
        }

        private async Task<TenantImportResult> ImportIntoTenantAsync(TenantExport export, int tenantId, string? tenantName)
        {
            var result = new TenantImportResult { TargetTenantId = tenantId, TargetTenantName = tenantName };

            await ImportUsersAsync(export, tenantId, result);
            Dictionary<int, int> unitIdMap = await ImportUnitsAsync(export, tenantId, result);
            // TenantExport predates Farm and carries no farm data, so every imported unit lands farm-less; this is a no-op for an existing organization that already has a farm, and otherwise sweeps the freshly-imported units into a new "First farm" same as a brand-new registration would.
            await deviceFarmUnitRepo.EnsureFirstFarmAsync(tenantId);
            Dictionary<int, int> zoneIdMap = await ImportZonesAsync(export, tenantId, unitIdMap, result);
            await ImportZoneRulesAsync(export, tenantId, zoneIdMap, result);

            (Dictionary<int, int> farmParcelZoneIdMap, int? openFieldFarmId) = await ImportFarmParcelsAsync(export, tenantId, result);
            Dictionary<int, int> sowingIdMap = await ImportSowingsAsync(export, tenantId, openFieldFarmId, farmParcelZoneIdMap, result);
            Dictionary<int, int> zonePlantingIdMap = await ImportZonePlantingsAsync(export, tenantId, zoneIdMap, result);
            await ImportFieldLogEntriesAsync(export, tenantId, sowingIdMap, farmParcelZoneIdMap, zonePlantingIdMap, zoneIdMap, result);
            await ImportHarvestResultsAsync(export, sowingIdMap, zonePlantingIdMap, farmParcelZoneIdMap, result);

            Dictionary<int, int> deviceIdMap = await ImportDevicesAsync(export, tenantId, unitIdMap, zoneIdMap, farmParcelZoneIdMap, sowingIdMap, result);
            await ImportSensorDataAsync(export, tenantId, deviceIdMap, unitIdMap, zoneIdMap, result);

            return result;
        }

        private async Task ImportUsersAsync(TenantExport export, int tenantId, TenantImportResult result)
        {
            foreach (TenantExportUser eu in export.Users)
            {
                // Email/Username carry a GLOBAL unique index (not per-organization) - a collision (re-run import, or already has an account) is expected, so skip the row rather than fail the batch.
                if (!string.IsNullOrEmpty(eu.User.Email) && await userRepo.UserGetAsync(null, eu.User.Email, null) is not null)
                {
                    result.UsersSkipped++;
                    result.SkippedReasons.Add($"User {eu.User.Email}: an account with this email already exists on this server.");
                    continue;
                }
                if (!string.IsNullOrEmpty(eu.User.Username) && await userRepo.UserGetAsync(null, null, eu.User.Username) is not null)
                {
                    result.UsersSkipped++;
                    result.SkippedReasons.Add($"User {eu.User.Email}: username '{eu.User.Username}' already exists on this server.");
                    continue;
                }

                var newUser = new User
                {
                    TenantID = tenantId,
                    Email = eu.User.Email,
                    Username = eu.User.Username,
                    FirstName = eu.User.FirstName,
                    LastName = eu.User.LastName,
                    Phone = eu.User.Phone,
                    Enabled = eu.User.Enabled,
                    EmailVerified = eu.User.EmailVerified,
                    TimeZone = eu.User.TimeZone,
                    // Portable hash, unproven identity on THIS server - see Agrumy.Shared.Models.User.MustChangePassword.
                    MustChangePassword = true,
                };
                await userRepo.UserAddAsync(newUser, new UserSecret { PwdHash = eu.PwdHash, PwdSalt = eu.PwdSalt });

                // UserAddAsync doesn't return the new id - same re-fetch-by-email pattern as UserApiController.UserRegistration.
                User? added = await userRepo.UserGetAsync(null, eu.User.Email, null);
                if (added?.IDUser is not int newId)
                {
                    result.UsersSkipped++;
                    result.SkippedReasons.Add($"User {eu.User.Email}: did not persist.");
                    continue;
                }
                // Global-* roles are a server-level concept, not portable across an organization export/import boundary - never let an imported user land on this server as a Global admin/reader/etc.
                var importableRoles = eu.Roles.Where(r => !r.StartsWith("Global", StringComparison.Ordinal)).ToList();
                await userRepo.UserRolesSetAsync(newId, importableRoles);
                result.UsersImported++;
            }
        }

        private async Task<Dictionary<int, int>> ImportUnitsAsync(TenantExport export, int tenantId, TenantImportResult result)
        {
            var map = new Dictionary<int, int>();
            foreach (DeviceFarmUnit u in export.Units)
            {
                DeviceFarmUnit created = await deviceFarmUnitRepo.DeviceFarmUnitAddAsync(new DeviceFarmUnit { TenantID = tenantId, DeviceFarmUnitName = u.DeviceFarmUnitName });
                if (u.IDDeviceFarmUnit is int oldId && created.IDDeviceFarmUnit is int newId)
                {
                    map[oldId] = newId;
                }
                result.UnitsImported++;
            }
            return map;
        }

        // 0 is not a real id on either table - used here purely as an in-memory "not found in map" marker, checked by callers below.
        private static int RemapOrSentinel(int oldId, Dictionary<int, int> map) =>
            map.GetValueOrDefault(oldId, 0);

        // Same "not found" case as RemapOrSentinel, but for the nullable Device.DeviceFarmUnitID/DeviceFarmUnitZoneID - null (unassigned), not 0, is the correct fallback there.
        private static int? RemapOrNull(int? oldId, Dictionary<int, int> map) =>
            oldId is int id && map.TryGetValue(id, out int newId) ? newId : null;

        private async Task<Dictionary<int, int>> ImportZonesAsync(TenantExport export, int tenantId, Dictionary<int, int> unitIdMap, TenantImportResult result)
        {
            var map = new Dictionary<int, int>();
            foreach (DeviceFarmUnitZone z in export.Zones)
            {
                DeviceFarmUnitZone created = await deviceFarmUnitRepo.DeviceFarmUnitZoneAddAsync(new DeviceFarmUnitZone
                {
                    TenantID = tenantId,
                    DeviceFarmUnitID = RemapOrSentinel(z.DeviceFarmUnitID, unitIdMap),
                    DeviceFarmUnitZoneName = z.DeviceFarmUnitZoneName,
                    WaterPumpMaxRunSeconds = z.WaterPumpMaxRunSeconds,
                    WaterPumpCooldownSeconds = z.WaterPumpCooldownSeconds,
                    SkipWaterPumpWhenRainPredicted = z.SkipWaterPumpWhenRainPredicted,
                    TankCapacityLiters = z.TankCapacityLiters,
                    WaterLevelRawEmpty = z.WaterLevelRawEmpty,
                    WaterLevelRawFull = z.WaterLevelRawFull,
                    WaterPumpMinLevel = z.WaterPumpMinLevel,
                    HeatingMaxRunSeconds = z.HeatingMaxRunSeconds,
                    VentilationMaxRunSeconds = z.VentilationMaxRunSeconds,
                });
                if (z.IDDeviceFarmUnitZone is int oldId && created.IDDeviceFarmUnitZone is int newId)
                {
                    map[oldId] = newId;
                }
                result.ZonesImported++;
            }
            return map;
        }

        private async Task ImportZoneRulesAsync(TenantExport export, int tenantId, Dictionary<int, int> zoneIdMap, TenantImportResult result)
        {
            // Export only ever captured Zone-scoped rules (TenantExportService predates Unit/Global scope) - r.DeviceFarmUnitZoneID is
            // therefore always set here, never null.
            foreach (DeviceFarmUnitZoneRule r in export.ZoneRules.Where(r => r.DeviceFarmUnitZoneID != null))
            {
                int newZoneId = RemapOrSentinel(r.DeviceFarmUnitZoneID!.Value, zoneIdMap);
                if (newZoneId == 0)
                {
                    continue; // the zone it belonged to failed to import - an orphaned rule is worse than a dropped one
                }
                await deviceFarmUnitRepo.RuleAddAsync(new DeviceFarmUnitZoneRule
                {
                    TenantID = tenantId,
                    DeviceFarmUnitZoneID = newZoneId,
                    ActionType = r.ActionType,
                    RelayFunction = r.RelayFunction,
                    Name = r.Name,
                    Description = r.Description,
                    Root = r.Root,
                    TargetPercent = r.TargetPercent,
                    IsSafetyRule = r.IsSafetyRule,
                    NotificationSubject = r.NotificationSubject,
                    NotificationBody = r.NotificationBody,
                });
                result.ZoneRulesImported++;
            }
        }

        /// #585 - every source-tenant Open-Field farm collapses into ONE new one here, same "sweep into first farm" simplification EnsureFirstFarmAsync already applies to Greenhouse Units (TenantExportFarmParcel's own doc comment). Returns the new farm's id (null if the export had no parcels at all) alongside the zone id map, since ImportSowingsAsync needs Sowing.FarmID too.
        private async Task<(Dictionary<int, int> ZoneIdMap, int? OpenFieldFarmId)> ImportFarmParcelsAsync(TenantExport export, int tenantId, TenantImportResult result)
        {
            var zoneMap = new Dictionary<int, int>();
            if (export.FarmParcels.Count == 0)
            {
                return (zoneMap, null);
            }
            (DeviceFarm farm, FarmOpenfield openfield) = await farmOpenfieldRepo.FarmOpenfieldCreateAsync("Imported", tenantId);
            foreach (TenantExportFarmParcel ep in export.FarmParcels)
            {
                (FarmParcel parcel, FarmParcelZone defaultZone) = await farmParcelRepo.FarmParcelAddAsync(new FarmParcel
                {
                    TenantID = tenantId,
                    FarmOpenfieldID = openfield.IDFarmOpenfield!.Value,
                    FarmParcelName = ep.Parcel.FarmParcelName,
                });
                if (ep.Parcel.GeometryGeoJson != null && ep.Parcel.AreaHectares is double parcelArea)
                {
                    await farmParcelRepo.FarmParcelGeometrySetAsync(parcel.IDFarmParcel!.Value, ep.Parcel.GeometryGeoJson, parcelArea,
                        ep.Parcel.BboxMinLat ?? 0, ep.Parcel.BboxMinLon ?? 0, ep.Parcel.BboxMaxLat ?? 0, ep.Parcel.BboxMaxLon ?? 0, ep.Parcel.ArkodParcelId);
                }
                result.FarmParcelsImported++;

                // FarmParcelAddAsync's own default zone stands in for the source's first zone; a source parcel already split into more than one gets the rest via the same Split operation the live UI uses.
                List<FarmParcelZone> newZones = [defaultZone];
                if (ep.Zones.Count > 1)
                {
                    IList<FarmParcelZone> split = await farmParcelRepo.FarmParcelZoneSplitAsync(defaultZone.IDFarmParcelZone!.Value, ep.Zones.Select(z => z.FarmParcelZoneName ?? "Zone").ToList());
                    newZones = split.ToList();
                }
                for (int i = 0; i < ep.Zones.Count && i < newZones.Count; i++)
                {
                    FarmParcelZone src = ep.Zones[i];
                    FarmParcelZone created = newZones[i];
                    await farmParcelRepo.FarmParcelZoneUpdateAsync(new FarmParcelZone
                    {
                        IDFarmParcelZone = created.IDFarmParcelZone,
                        FarmParcelZoneName = src.FarmParcelZoneName,
                        WaterPumpMaxRunSeconds = src.WaterPumpMaxRunSeconds,
                        WaterPumpCooldownSeconds = src.WaterPumpCooldownSeconds,
                        SkipWaterPumpWhenRainPredicted = src.SkipWaterPumpWhenRainPredicted,
                        TankCapacityLiters = src.TankCapacityLiters,
                        WaterLevelRawEmpty = src.WaterLevelRawEmpty,
                        WaterLevelRawFull = src.WaterLevelRawFull,
                        WaterPumpMinLevel = src.WaterPumpMinLevel,
                        HeatingMaxRunSeconds = src.HeatingMaxRunSeconds,
                        VentilationMaxRunSeconds = src.VentilationMaxRunSeconds,
                        HeatingFailSafePolicy = src.HeatingFailSafePolicy,
                    });
                    if (src.GeometryGeoJson != null && src.AreaHectares is double zoneArea)
                    {
                        await farmParcelRepo.FarmParcelZoneGeometrySetAsync(created.IDFarmParcelZone!.Value, src.GeometryGeoJson, zoneArea,
                            src.BboxMinLat ?? 0, src.BboxMinLon ?? 0, src.BboxMaxLat ?? 0, src.BboxMaxLon ?? 0);
                    }
                    if (src.IDFarmParcelZone is int oldZoneId && created.IDFarmParcelZone is int newZoneId)
                    {
                        zoneMap[oldZoneId] = newZoneId;
                    }
                    result.FarmParcelZonesImported++;
                }
            }
            return (zoneMap, farm.IDDeviceFarm);
        }

        private async Task<Dictionary<int, int>> ImportSowingsAsync(TenantExport export, int tenantId, int? openFieldFarmId, Dictionary<int, int> farmParcelZoneIdMap, TenantImportResult result)
        {
            var map = new Dictionary<int, int>();
            if (openFieldFarmId is not int farmId)
            {
                return map;
            }
            foreach (TenantExportSowing es in export.Sowings)
            {
                int cropId = await cropCatalogRepo.CropFindOrCreateByNameAsync(tenantId, es.CropName ?? es.Sowing.SowingName ?? "Crop");

                // A Closed sowing's historical zone assignments aren't reconstructed (TenantExportSowing's own doc comment) - only a currently-Active sowing occupies any zone here.
                var occupiedZoneIds = new List<int>();
                if (es.Sowing.Status == GrowingCycleStatus.Active && es.Sowing.IDSowing is int idSowing)
                {
                    foreach (TenantExportFarmParcel ep in export.FarmParcels)
                    {
                        foreach (FarmParcelZone z in ep.Zones)
                        {
                            if (z.CurrentSowingID == idSowing && z.IDFarmParcelZone is int oldZoneId && farmParcelZoneIdMap.TryGetValue(oldZoneId, out int newZoneId))
                            {
                                occupiedZoneIds.Add(newZoneId);
                            }
                        }
                    }
                }

                Sowing created = await sowingRepo.SowingRestoreAsync(new Sowing
                {
                    TenantID = tenantId,
                    FarmID = farmId,
                    CropID = cropId,
                    Variety = es.Sowing.Variety,
                    SeedRateKgPerHa = es.Sowing.SeedRateKgPerHa,
                    StartDate = es.Sowing.StartDate,
                    ExpectedDurationDays = es.Sowing.ExpectedDurationDays,
                    Status = es.Sowing.Status,
                    HarvestDate = es.Sowing.HarvestDate,
                    ClosedUtc = es.Sowing.ClosedUtc,
                    // ClosedByUserID intentionally dropped - a source-tenant user id that may not exist (or may map to someone else entirely) on the target.
                    Notes = es.Sowing.Notes,
                }, occupiedZoneIds);
                if (es.Sowing.IDSowing is int oldId && created.IDSowing is int newId)
                {
                    map[oldId] = newId;
                }
                result.SowingsImported++;
            }
            return map;
        }

        private async Task<Dictionary<int, int>> ImportZonePlantingsAsync(TenantExport export, int tenantId, Dictionary<int, int> zoneIdMap, TenantImportResult result)
        {
            var map = new Dictionary<int, int>();
            foreach (ZonePlanting zp in export.ZonePlantings)
            {
                if (!zoneIdMap.TryGetValue(zp.DeviceFarmUnitZoneID, out int newZoneId))
                {
                    continue; // the Greenhouse zone it belonged to failed to import
                }
                int cropId = await cropCatalogRepo.CropFindOrCreateByNameAsync(tenantId, zp.CropName ?? "Crop");
                ZonePlanting created = await zonePlantingRepo.ZonePlantingRestoreAsync(new ZonePlanting
                {
                    TenantID = tenantId,
                    DeviceFarmUnitZoneID = newZoneId,
                    CropID = cropId,
                    PlantedDate = zp.PlantedDate,
                    ExpectedDurationDays = zp.ExpectedDurationDays,
                    Status = zp.Status,
                    HarvestDate = zp.HarvestDate,
                    ClosedUtc = zp.ClosedUtc,
                    Notes = zp.Notes,
                });
                if (zp.IDZonePlanting is int oldId && created.IDZonePlanting is int newId)
                {
                    map[oldId] = newId;
                }
                result.ZonePlantingsImported++;
            }
            return map;
        }

        /// Exactly one of the four scope FKs must survive remapping (matching FieldLogEntry's own "scoped to exactly one" invariant) - an entry whose scope failed to import entirely is dropped rather than orphaned, same reasoning as ImportZoneRulesAsync.
        private async Task ImportFieldLogEntriesAsync(TenantExport export, int tenantId, Dictionary<int, int> sowingIdMap, Dictionary<int, int> farmParcelZoneIdMap, Dictionary<int, int> zonePlantingIdMap, Dictionary<int, int> zoneIdMap, TenantImportResult result)
        {
            foreach (TenantExportFieldLogEntry efl in export.FieldLogEntries)
            {
                FieldLogEntry e = efl.Entry;
                int? newSowingId = RemapOrNull(e.SowingID, sowingIdMap);
                int? newFarmParcelZoneId = RemapOrNull(e.FarmParcelZoneID, farmParcelZoneIdMap);
                int? newZonePlantingId = RemapOrNull(e.ZonePlantingID, zonePlantingIdMap);
                int? newDeviceFarmUnitZoneId = RemapOrNull(e.DeviceFarmUnitZoneID, zoneIdMap);
                if (newSowingId == null && newFarmParcelZoneId == null && newZonePlantingId == null && newDeviceFarmUnitZoneId == null)
                {
                    continue;
                }
                FieldLogEntry created = await fieldLogRepo.FieldLogEntryAddAsync(new FieldLogEntry
                {
                    TenantID = tenantId,
                    SowingID = newSowingId,
                    FarmParcelZoneID = newFarmParcelZoneId,
                    ZonePlantingID = newZonePlantingId,
                    DeviceFarmUnitZoneID = newDeviceFarmUnitZoneId,
                    EntryType = e.EntryType,
                    DateUtc = e.DateUtc,
                    // CreatedByUserID intentionally dropped - same reasoning as Sowing.ClosedByUserID above.
                    Note = e.Note,
                    PayloadJson = e.PayloadJson,
                    IsClosingEntry = e.IsClosingEntry,
                });
                result.FieldLogEntriesImported++;

                // Metadata only - the physical file under fieldlog-store on the SOURCE server does not travel with the export (TenantExportFieldLogEntry's own doc comment).
                foreach (FieldLogAttachment att in efl.Attachments)
                {
                    await fieldLogRepo.FieldLogAttachmentAddAsync(new FieldLogAttachment
                    {
                        FieldLogEntryID = created.IDFieldLogEntry!.Value,
                        FileName = att.FileName,
                        ContentType = att.ContentType,
                        StoragePath = att.StoragePath,
                        SizeBytes = att.SizeBytes,
                    });
                    result.FieldLogAttachmentsImported++;
                }
            }
        }

        /// SowingID/ZonePlantingID are the two scopes HarvestResult supports - a result whose scope failed to import entirely is dropped, same reasoning as ImportFieldLogEntriesAsync.
        private async Task ImportHarvestResultsAsync(TenantExport export, Dictionary<int, int> sowingIdMap, Dictionary<int, int> zonePlantingIdMap, Dictionary<int, int> farmParcelZoneIdMap, TenantImportResult result)
        {
            foreach (HarvestResult h in export.HarvestResults)
            {
                int? newSowingId = RemapOrNull(h.SowingID, sowingIdMap);
                int? newZonePlantingId = RemapOrNull(h.ZonePlantingID, zonePlantingIdMap);
                if (newSowingId == null && newZonePlantingId == null)
                {
                    continue;
                }
                await fieldLogRepo.HarvestResultAddAsync(new HarvestResult
                {
                    SowingID = newSowingId,
                    ZonePlantingID = newZonePlantingId,
                    FarmParcelZoneID = RemapOrNull(h.FarmParcelZoneID, farmParcelZoneIdMap),
                    DateUtc = h.DateUtc,
                    YieldKg = h.YieldKg,
                    MoisturePercent = h.MoisturePercent,
                    QualityGrade = h.QualityGrade,
                    LossesKg = h.LossesKg,
                    MetricsJson = h.MetricsJson,
                    Note = h.Note,
                });
                result.HarvestResultsImported++;
            }
        }

        private async Task<Dictionary<int, int>> ImportDevicesAsync(TenantExport export, int tenantId, Dictionary<int, int> unitIdMap, Dictionary<int, int> zoneIdMap, Dictionary<int, int> farmParcelZoneIdMap, Dictionary<int, int> sowingIdMap, TenantImportResult result)
        {
            var map = new Dictionary<int, int>();
            foreach (TenantExportDevice ed in export.Devices)
            {
                // ApiId is globally unique - a collision (re-run import, or already here under a different organization) is skipped, not failed on.
                if (!string.IsNullOrEmpty(ed.ApiId) && await deviceRepo.DeviceGetByApiIdAsync(ed.ApiId) is not null)
                {
                    result.DevicesSkipped++;
                    result.SkippedReasons.Add($"Device {ed.Device.DeviceName} ({ed.ApiId}): already exists on this server.");
                    continue;
                }

                // ApiId/ApiKey kept AS EXPORTED - a real device's firmware needs no reconfiguration to keep talking to whichever server now owns this row.
                Device created = await deviceRepo.DeviceAddAsync(new Device
                {
                    TenantID = tenantId,
                    DeviceRoleID = ed.Device.DeviceRoleID,
                    DeviceFarmUnitID = RemapOrNull(ed.Device.DeviceFarmUnitID, unitIdMap),
                    DeviceFarmUnitZoneID = RemapOrNull(ed.Device.DeviceFarmUnitZoneID, zoneIdMap),
                    // Mutually exclusive with the Greenhouse pair above on the source already (DeviceAssignToFarmParcelZoneAsync's own invariant) - remapping both pairs unconditionally is safe, only the branch the device was actually on ever has a non-null id to remap.
                    FarmParcelZoneID = RemapOrNull(ed.Device.FarmParcelZoneID, farmParcelZoneIdMap),
                    SowingID = RemapOrNull(ed.Device.SowingID, sowingIdMap),
                    DeviceName = ed.Device.DeviceName,
                    MacAddress = ed.Device.MacAddress,
                    ApiId = ed.ApiId,
                    ApiKey = ed.ApiKey,
                    ServicePoint = ed.Device.ServicePoint,
                    DeviceTypeServiceID = ed.Device.DeviceTypeServiceID,
                    DeviceSensorEnabled = ed.Device.DeviceSensorEnabled,
                    DeviceControllerEnabled = ed.Device.DeviceControllerEnabled,
                    BatteryEnabled = ed.Device.BatteryEnabled,
                    Enabled = ed.Device.Enabled,
                    ConfigVersion = 1,
                    IsGateway = ed.Device.IsGateway,
                    GatewayProfile = ed.Device.GatewayProfile,
                });

                if (ed.Sensor != null)
                {
                    await deviceRepo.DeviceConfigSensorUpdateAsync(created.IDDevice, ed.Sensor);
                }
                if (ed.Controller != null)
                {
                    await deviceRepo.DeviceConfigControllerUpdateAsync(created.IDDevice, ed.Controller);
                }
                if (ed.Device.IDDevice is int oldId && created.IDDevice is int newId)
                {
                    map[oldId] = newId;
                }
                result.DevicesImported++;
            }
            return map;
        }

        private async Task ImportSensorDataAsync(TenantExport export, int tenantId, Dictionary<int, int> deviceIdMap, Dictionary<int, int> unitIdMap, Dictionary<int, int> zoneIdMap, TenantImportResult result)
        {
            if (!export.IncludesSensorData || export.SensorData is not { Count: > 0 })
            {
                return;
            }

            var rows = new List<SensorData>();
            foreach (SensorData sd in export.SensorData)
            {
                // A reading for a device that itself got skipped has nowhere valid to attach - drop it rather than orphan it against device 0.
                if (sd.DeviceID is not int oldDeviceId || !deviceIdMap.TryGetValue(oldDeviceId, out int newDeviceId))
                {
                    continue;
                }
                rows.Add(new SensorData
                {
                    TenantID = tenantId,
                    DeviceID = newDeviceId,
                    DeviceFarmUnitID = sd.DeviceFarmUnitID is int u ? RemapOrSentinel(u, unitIdMap) : 0,
                    DeviceFarmUnitZoneID = sd.DeviceFarmUnitZoneID is int z ? RemapOrSentinel(z, zoneIdMap) : 0,
                    Battery = sd.Battery,
                    Temperature = sd.Temperature,
                    SoilTemperature = sd.SoilTemperature,
                    Humidity = sd.Humidity,
                    Moisture = sd.Moisture,
                    Light = sd.Light,
                    Co2 = sd.Co2,
                    Tvoc = sd.Tvoc,
                    Barometer = sd.Barometer,
                    LiquidPH = sd.LiquidPH,
                    RainLevel = sd.RainLevel,
                    WaterLevel = sd.WaterLevel,
                    Wind = sd.Wind,
                    DateCreated = sd.DateCreated,
                });
            }
            if (rows.Count > 0)
            {
                await sensorDataRepo.SensorDataImportAsync(rows);
                result.SensorDataRowsImported = rows.Count;
            }
        }
    }
}
