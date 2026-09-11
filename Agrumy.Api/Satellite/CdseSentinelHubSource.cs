using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Satellite
{
    /// The one provider built in Detaljni dizajn S sesija B - Copernicus Data Space Ecosystem's Sentinel Hub Catalog/Process/Statistical APIs (Sentinel-2 L2A only; commercial collections are S-B2). Request/response shapes are built from CDSE's published API documentation (documentation.dataspace.copernicus.eu/APIs/SentinelHub) - never exercised against a live tenant token in this session (D1 means there is no server-wide credential to test with), so treat a first real run as the actual acceptance test, not this code's unit tests alone.
    public sealed class CdseSentinelHubSource(HttpClient httpClient, ICdseTokenProvider tokenProvider, ILogger<CdseSentinelHubSource> logger) : ISatelliteImagerySource
    {
        private const string CatalogUrl = "https://sh.dataspace.copernicus.eu/catalog/v1/search";
        private const string ProcessUrl = "https://sh.dataspace.copernicus.eu/process/v1";
        private const string StatisticsUrl = "https://sh.dataspace.copernicus.eu/statistics/v1";

        public SatelliteCapabilities GetCapabilities(SatelliteCollection collection) => collection switch
        {
            SatelliteCollection.Sentinel2 => new SatelliteCapabilities(
                Indices: Enum.GetValues<SatelliteIndex>(),
                ResolutionMeters: 10,
                RevisitDays: 5,
                MaxParcelsPerDay: 300, // conservative cap for a CDSE free-tier client credential (D12's SyncEvaluator enforces this as an upper bound, not a promise every free account actually has this much headroom)
                SupportsStatistics: true),
            // S-B2, D3 - no SWIR band, so no NDMI/NDSI/SwirComposite; daily revisit, 3m. MaxParcelsPerDay lower - commercial PU cost per call is higher (roadmap step 3).
            SatelliteCollection.PlanetScope => new SatelliteCapabilities(
                Indices: [SatelliteIndex.Ndvi, SatelliteIndex.Ndwi, SatelliteIndex.NaturalColor],
                ResolutionMeters: 3,
                RevisitDays: 1,
                MaxParcelsPerDay: 50,
                SupportsStatistics: true),
            // Pleiades/SPOT - tasked (on-order), not a fixed revisit; capabilities-only in this session, see the class doc comment.
            SatelliteCollection.PleiadesSpot => new SatelliteCapabilities(
                Indices: [SatelliteIndex.Ndvi, SatelliteIndex.NaturalColor],
                ResolutionMeters: 0.5,
                RevisitDays: 0,
                MaxParcelsPerDay: 10,
                SupportsStatistics: false),
            _ => throw new ArgumentOutOfRangeException(nameof(collection)),
        };

        /// The Process/Catalog/Statistical "type" value for a collection - Sentinel-2 uses CDSE's universal type string; commercial collections are each tenant's own Sentinel Hub BYOC subscription, referenced by the "byoc-" prefix convention documented for third-party collections (not live-verified - no commercial CDSE subscription exists to test against in this session).
        private static string ResolveCollectionType(SatelliteCollection collection, string? commercialCollectionId) => collection switch
        {
            SatelliteCollection.Sentinel2 => "sentinel-2-l2a",
            _ => string.IsNullOrWhiteSpace(commercialCollectionId)
                ? throw new InvalidOperationException($"{collection} requires a CommercialCollectionId (the tenant's own Sentinel Hub BYOC subscription) before it can be queried.")
                : $"byoc-{commercialCollectionId}",
        };

        private static IReadOnlyDictionary<SatelliteIndex, Evalscripts.Definition> EvalscriptsFor(SatelliteCollection collection) => collection switch
        {
            SatelliteCollection.Sentinel2 => Evalscripts.All,
            SatelliteCollection.PlanetScope => Evalscripts.PlanetScope,
            _ => throw new NotSupportedException($"{collection} has no evalscripts wired up yet - capabilities-only in this session."),
        };

        public async Task<IReadOnlyList<SceneCandidate>> FindScenesAsync(int tenantId, SatelliteCollection collection, string? commercialCollectionId, BoundingBox bbox, DateOnly fromUtc, DateOnly toUtc, int maxCloudPercent, CancellationToken ct)
        {
            string? token = await tokenProvider.GetAccessTokenAsync(tenantId, ct);
            if (token == null)
            {
                return [];
            }
            string collectionType = ResolveCollectionType(collection, commercialCollectionId);

            var body = new JsonObject
            {
                ["collections"] = new JsonArray(collectionType),
                ["datetime"] = $"{fromUtc:yyyy-MM-dd}T00:00:00Z/{toUtc:yyyy-MM-dd}T23:59:59Z",
                ["bbox"] = new JsonArray(bbox.MinLon, bbox.MinLat, bbox.MaxLon, bbox.MaxLat),
                ["limit"] = 100,
                ["query"] = new JsonObject { ["eo:cloud_cover"] = new JsonObject { ["lte"] = maxCloudPercent } },
            };

            using var request = NewRequest(HttpMethod.Post, CatalogUrl, token, body);
            using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
            if (!await EnsureSuccessAsync(response, "Catalog", tenantId, ct))
            {
                return [];
            }

            JsonNode? json = await JsonNode.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var results = new List<SceneCandidate>();
            foreach (JsonNode? feature in json?["features"]?.AsArray() ?? [])
            {
                string? id = feature?["id"]?.GetValue<string>();
                string? datetime = feature?["properties"]?["datetime"]?.GetValue<string>();
                double cloud = feature?["properties"]?["eo:cloud_cover"]?.GetValue<double>() ?? 0;
                if (id != null && datetime != null && DateTimeOffset.TryParse(datetime, out DateTimeOffset dt))
                {
                    results.Add(new SceneCandidate(id, DateOnly.FromDateTime(dt.UtcDateTime), cloud));
                }
            }
            return results;
        }

        public async Task<IndexRender> RenderIndexAsync(int tenantId, SatelliteCollection collection, string? commercialCollectionId, SceneCandidate scene, string geoJsonPolygon, SatelliteIndex index, bool renderPng, bool renderGrid, CancellationToken ct)
        {
            string? token = await tokenProvider.GetAccessTokenAsync(tenantId, ct);
            if (token == null)
            {
                throw new InvalidOperationException($"No CDSE token available for tenant {tenantId}.");
            }
            string collectionType = ResolveCollectionType(collection, commercialCollectionId);

            Evalscripts.Definition def = EvalscriptsFor(collection)[index];
            var body = new JsonObject
            {
                ["input"] = new JsonObject
                {
                    ["bounds"] = new JsonObject
                    {
                        ["geometry"] = JsonNode.Parse(geoJsonPolygon),
                        ["properties"] = new JsonObject { ["crs"] = "http://www.opengis.net/def/crs/OGC/1.3/CRS84" },
                    },
                    ["data"] = new JsonArray(new JsonObject
                    {
                        ["type"] = collectionType,
                        ["dataFilter"] = new JsonObject
                        {
                            ["timeRange"] = new JsonObject { ["from"] = $"{scene.SceneDateUtc:yyyy-MM-dd}T00:00:00Z", ["to"] = $"{scene.SceneDateUtc:yyyy-MM-dd}T23:59:59Z" },
                        },
                    }),
                },
                ["output"] = new JsonObject
                {
                    ["resx"] = 10,
                    ["resy"] = 10,
                    ["responses"] = new JsonArray(
                        new JsonObject { ["identifier"] = "default", ["format"] = new JsonObject { ["type"] = "image/png" } },
                        new JsonObject { ["identifier"] = "dataMask", ["format"] = new JsonObject { ["type"] = "image/png" } }),
                },
                ["evalscript"] = def.Script,
            };

            using var request = NewRequest(HttpMethod.Post, ProcessUrl, token, body);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("multipart/form-data"));
            using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
            if (!await EnsureSuccessAsync(response, "Process", tenantId, ct))
            {
                throw new InvalidOperationException($"Process API request failed for tenant {tenantId}, scene {scene.SourceSceneId}, index {index}.");
            }

            (byte[]? defaultPng, byte[]? dataMaskPng) = await ReadMultipartPngsAsync(response, ct);
            if (defaultPng == null || dataMaskPng == null)
            {
                throw new InvalidOperationException("Process API response did not contain both default and dataMask parts.");
            }

            MinimalPng.Decoded valuePixels = MinimalPng.Decode(defaultPng);
            MinimalPng.Decoded maskPixels = MinimalPng.Decode(dataMaskPng);
            double validPercent = ComputeValidPercent(maskPixels.Pixels);

            byte[]? grid = renderGrid && def.HasStatistics ? BuildQuantizedGrid(valuePixels, maskPixels) : null;
            string statsJson = def.HasStatistics ? BuildStatsJson(valuePixels, maskPixels) : "{}";
            string boundsJson = geoJsonPolygon;

            return new IndexRender(renderPng ? defaultPng : null, boundsJson, statsJson, validPercent, grid);
        }

        public async Task<IReadOnlyList<SceneStatEntry>> BackfillStatisticsAsync(int tenantId, SatelliteCollection collection, string? commercialCollectionId, BoundingBox bbox, string geoJsonPolygon, DateOnly fromUtc, DateOnly toUtc, SatelliteIndex index, int maxCloudPercent, CancellationToken ct)
        {
            IReadOnlyDictionary<SatelliteIndex, Evalscripts.Definition> evalscripts = EvalscriptsFor(collection);
            if (!evalscripts[index].HasStatistics)
            {
                throw new ArgumentException($"{index} is a visual composite with no scalar statistics.", nameof(index));
            }
            string? token = await tokenProvider.GetAccessTokenAsync(tenantId, ct);
            if (token == null)
            {
                return [];
            }
            string collectionType = ResolveCollectionType(collection, commercialCollectionId);

            var body = new JsonObject
            {
                ["input"] = new JsonObject
                {
                    ["bounds"] = new JsonObject
                    {
                        ["geometry"] = JsonNode.Parse(geoJsonPolygon),
                        ["properties"] = new JsonObject { ["crs"] = "http://www.opengis.net/def/crs/OGC/1.3/CRS84" },
                    },
                    ["data"] = new JsonArray(new JsonObject
                    {
                        ["type"] = collectionType,
                        ["dataFilter"] = new JsonObject { ["maxCloudCoverage"] = maxCloudPercent },
                    }),
                },
                ["aggregation"] = new JsonObject
                {
                    ["timeRange"] = new JsonObject { ["from"] = $"{fromUtc:yyyy-MM-dd}T00:00:00Z", ["to"] = $"{toUtc:yyyy-MM-dd}T23:59:59Z" },
                    ["aggregationInterval"] = new JsonObject { ["of"] = "P1D" },
                    ["resx"] = 10,
                    ["resy"] = 10,
                    ["evalscript"] = evalscripts[index].Script,
                },
                ["calculations"] = new JsonObject { ["default"] = new JsonObject() },
            };

            using var request = NewRequest(HttpMethod.Post, StatisticsUrl, token, body);
            using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
            if (!await EnsureSuccessAsync(response, "Statistics", tenantId, ct))
            {
                return [];
            }

            JsonNode? json = await JsonNode.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var results = new List<SceneStatEntry>();
            foreach (JsonNode? entry in json?["data"]?.AsArray() ?? [])
            {
                string? fromDate = entry?["interval"]?["from"]?.GetValue<string>();
                if (fromDate == null || !DateTimeOffset.TryParse(fromDate, out DateTimeOffset dt))
                {
                    continue;
                }
                JsonNode? bandStats = entry?["outputs"]?["default"]?["bands"]?["B0"]?["stats"];
                if (bandStats == null)
                {
                    continue; // interval with no valid observation (cloud/no scene) - Statistical API still emits the interval, just with no stats
                }
                double sampleCount = bandStats["sampleCount"]?.GetValue<double>() ?? 0;
                double noDataCount = bandStats["noDataCount"]?.GetValue<double>() ?? 0;
                double validPercent = sampleCount > 0 ? 100.0 * (sampleCount - noDataCount) / sampleCount : 0;

                results.Add(new SceneStatEntry(
                    SourceSceneId: $"stat-{dt:yyyy-MM-dd}",
                    SceneDateUtc: DateOnly.FromDateTime(dt.UtcDateTime),
                    CloudPercent: 0, // Statistical API's aggregate response doesn't carry per-scene cloud_cover; Reliable is judged purely on ValidPixelPercent here.
                    ValidPixelPercent: validPercent,
                    Stats: new SatelliteIndexStats
                    {
                        Mean = bandStats["mean"]?.GetValue<double>(),
                        Min = bandStats["min"]?.GetValue<double>(),
                        Max = bandStats["max"]?.GetValue<double>(),
                        StdDev = bandStats["stDev"]?.GetValue<double>(),
                        SampleCount = (int)sampleCount,
                    }));
            }
            return results;
        }

        public async Task<QuotaSnapshot> QuotaStatusAsync(int tenantId, CancellationToken ct)
        {
            string? token = await tokenProvider.GetAccessTokenAsync(tenantId, ct);
            if (token == null)
            {
                return new QuotaSnapshot(0, "{}");
            }

            // CDSE doesn't expose a dedicated "quota remaining" JSON endpoint - a lightweight Catalog call's rate-limit response headers are the documented signal (X-RateLimit-Remaining/-Limit), same as most Sentinel Hub deployments; treated as best-effort, 0% used if the headers are absent rather than guessing.
            var probeBody = new JsonObject { ["collections"] = new JsonArray("sentinel-2-l2a"), ["limit"] = 1 };
            using var request = NewRequest(HttpMethod.Post, CatalogUrl, token, probeBody);
            using HttpResponseMessage response = await httpClient.SendAsync(request, ct);

            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out IEnumerable<string>? remainingValues) &&
                response.Headers.TryGetValues("X-RateLimit-Limit", out IEnumerable<string>? limitValues) &&
                double.TryParse(remainingValues.FirstOrDefault(), out double remaining) &&
                double.TryParse(limitValues.FirstOrDefault(), out double limit) && limit > 0)
            {
                double usedPercent = 100.0 * (limit - remaining) / limit;
                return new QuotaSnapshot(usedPercent, JsonSerializer.Serialize(new { remaining, limit, checkedAtUtc = DateTimeOffset.UtcNow }));
            }
            return new QuotaSnapshot(0, JsonSerializer.Serialize(new { note = "No rate-limit headers on this response.", checkedAtUtc = DateTimeOffset.UtcNow }));
        }

        private static HttpRequestMessage NewRequest(HttpMethod method, string url, string token, JsonObject body)
        {
            var request = new HttpRequestMessage(method, url)
            {
                Content = JsonContent.Create(body),
            };
            // Per-request header, never httpClient.DefaultRequestHeaders - this HttpClient is shared/pooled across every tenant's calls.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return request;
        }

        private async Task<bool> EnsureSuccessAsync(HttpResponseMessage response, string api, int tenantId, CancellationToken ct)
        {
            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            string body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("CDSE {Api} API returned {StatusCode} for tenant {TenantId}: {Body}", api, response.StatusCode, tenantId, body.Length > 500 ? body[..500] : body);
            return false;
        }

        private static async Task<(byte[]? DefaultPng, byte[]? DataMaskPng)> ReadMultipartPngsAsync(HttpResponseMessage response, CancellationToken ct)
        {
            string? contentType = response.Content.Headers.ContentType?.ToString();
            if (contentType == null || !contentType.Contains("multipart", StringComparison.OrdinalIgnoreCase))
            {
                // A single-response request (defensive fallback - this method always asks for two) would land here; nothing usable to extract.
                return (null, null);
            }
            string boundary = Microsoft.Net.Http.Headers.HeaderUtilities.RemoveQuotes(Microsoft.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType).Boundary.Value!).Value!;
            var reader = new MultipartReader(boundary, await response.Content.ReadAsStreamAsync(ct));

            byte[]? defaultPng = null, dataMaskPng = null;
            Microsoft.AspNetCore.WebUtilities.MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync(ct)) != null)
            {
                using var buffer = new MemoryStream();
                await section.Body.CopyToAsync(buffer, ct);
                byte[] bytes = buffer.ToArray();
                if (section.Headers != null && section.Headers.TryGetValue("Content-Disposition", out Microsoft.Extensions.Primitives.StringValues disposition) && disposition.ToString().Contains("dataMask"))
                {
                    dataMaskPng = bytes;
                }
                else
                {
                    defaultPng ??= bytes;
                }
            }
            return (defaultPng, dataMaskPng);
        }

        private static double ComputeValidPercent(byte[] maskPixels)
        {
            if (maskPixels.Length == 0)
            {
                return 0;
            }
            int valid = maskPixels.Count(b => b != 0);
            return 100.0 * valid / maskPixels.Length;
        }

        /// Only called for scalar indices (HasStatistics=true) - composites reuse their rendered PNG directly, they have no per-pixel scalar to store a time series against. Grid format: 4-byte width + 4-byte height (little-endian) header, then one byte per pixel (the evalscript's own 0-255 quantization), 0xFF (-1 as signed int8) marking an invalid/masked pixel - see SatellitePaletteRenderer.Decode for the reader.
        private static byte[] BuildQuantizedGrid(MinimalPng.Decoded value, MinimalPng.Decoded mask)
        {
            var grid = new byte[8 + value.Pixels.Length];
            BitConverter.GetBytes(value.Width).CopyTo(grid, 0);
            BitConverter.GetBytes(value.Height).CopyTo(grid, 4);
            for (int i = 0; i < value.Pixels.Length; i++)
            {
                bool valid = i < mask.Pixels.Length && mask.Pixels[i] != 0;
                grid[8 + i] = valid ? value.Pixels[i] : (byte)0xFF;
            }
            return grid;
        }

        private static string BuildStatsJson(MinimalPng.Decoded value, MinimalPng.Decoded mask)
        {
            var validValues = new List<double>();
            for (int i = 0; i < value.Pixels.Length && i < mask.Pixels.Length; i++)
            {
                if (mask.Pixels[i] != 0)
                {
                    validValues.Add(value.Pixels[i]);
                }
            }
            if (validValues.Count == 0)
            {
                return JsonSerializer.Serialize(new SatelliteIndexStats { SampleCount = 0 });
            }
            double mean = validValues.Average();
            double variance = validValues.Sum(v => (v - mean) * (v - mean)) / validValues.Count;
            return JsonSerializer.Serialize(new SatelliteIndexStats
            {
                Mean = mean,
                Min = validValues.Min(),
                Max = validValues.Max(),
                StdDev = Math.Sqrt(variance),
                SampleCount = validValues.Count,
            });
        }
    }

    public sealed class SatelliteImagerySourceFactory(Agrumy.Api.Dal.Interface.ISatelliteConfigRepository configRepo, CdseSentinelHubSource cdseSource) : ISatelliteImagerySourceFactory
    {
        public async Task<ISatelliteImagerySource?> ForAsync(int tenantId, CancellationToken ct)
        {
            TenantSatelliteConfig? config = await configRepo.SatelliteConfigGetAsync(tenantId);
            if (config is not { Enabled: true })
            {
                return null;
            }
            // Only provider built in this session (B) - S-B2 adds SentinelHubCommercial as a second branch here.
            return config.Provider == SatelliteProvider.CdseSentinelHub ? cdseSource : null;
        }
    }
}
