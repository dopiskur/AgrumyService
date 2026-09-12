using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Satellite
{
    /// The one provider built in Detaljni dizajn S sesija B - Copernicus Data Space Ecosystem's Sentinel Hub Catalog/Process/Statistical APIs (Sentinel-2 L2A only; commercial collections are S-B2). Catalog filtering, Process rendering (multipart/mixed, explicit pixel width/height, positional part matching), and Statistics have all been exercised against live CDSE responses, not just documentation/unit tests - see each method's own remarks for the specific wire-format quirks that required.
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

        /// The Process/Catalog/Statistical "type" value for a collection - Sentinel-2 uses CDSE's universal type string; commercial collections are each organization's own Sentinel Hub BYOC subscription, referenced by the "byoc-" prefix convention documented for third-party collections (not live-verified - no commercial CDSE subscription exists to test against in this session).
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
                // Live-verified against CDSE (2026-09): the STAC "query" extension ({"eo:cloud_cover":{"lte":N}}) is rejected outright ("Invalid json format, problematic key 'query'") - CDSE's Catalog only accepts CQL2-JSON.
                ["filter-lang"] = "cql2-json",
                ["filter"] = new JsonObject { ["op"] = "<=", ["args"] = new JsonArray(new JsonObject { ["property"] = "eo:cloud_cover" }, maxCloudPercent) },
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
            double resolutionMeters = GetCapabilities(collection).ResolutionMeters;
            (int pixelWidth, int pixelHeight) = ComputePixelDimensions(geoJsonPolygon, resolutionMeters, resolutionMeters);
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
                // Live-verified against CDSE (2026-09): resx/resy alone silently renders a 1x1 image against a CRS84 (geographic, not metric) geometry bounds - explicit pixel width/height is derived from the collection's own ResolutionMeters instead, not sent on the wire.
                ["output"] = new JsonObject
                {
                    ["width"] = pixelWidth,
                    ["height"] = pixelHeight,
                    ["responses"] = new JsonArray(
                        new JsonObject { ["identifier"] = "default", ["format"] = new JsonObject { ["type"] = "image/png" } },
                        new JsonObject { ["identifier"] = "dataMask", ["format"] = new JsonObject { ["type"] = "image/png" } }),
                },
                ["evalscript"] = def.RenderScript,
            };

            using var request = NewRequest(HttpMethod.Post, ProcessUrl, token, body);
            // Live-verified against CDSE (2026-09): "multipart/form-data" is rejected ("unsupported mime type"); "multipart/mixed" is the value CDSE's Process API actually honors for a multi-response request.
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("multipart/mixed"));
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

            // Composites are served as-is, so masked pixels (outside the polygon, clouds) get alpha 0 here rather than rendering as opaque black on the map.
            byte[]? png = !renderPng ? null : def.HasStatistics ? defaultPng : ApplyMaskAsAlpha(valuePixels, maskPixels);
            return new IndexRender(png, boundsJson, statsJson, validPercent, grid);
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
            double resolutionMeters = GetCapabilities(collection).ResolutionMeters;
            (int pixelWidth, int pixelHeight) = ComputePixelDimensions(geoJsonPolygon, resolutionMeters, resolutionMeters);

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
                    // Live-verified against CDSE (2026-09): resx/resy are taken in CRS84 degrees here too, collapsing a whole parcel into sampleCount=1 - explicit pixel width/height is what actually samples at 10 m.
                    ["width"] = pixelWidth,
                    ["height"] = pixelHeight,
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
                double sampleCount = ReadDouble(bandStats["sampleCount"]) ?? 0;
                double noDataCount = ReadDouble(bandStats["noDataCount"]) ?? 0;
                double validPercent = sampleCount > 0 ? 100.0 * (sampleCount - noDataCount) / sampleCount : 0;
                if (validPercent <= 0)
                {
                    continue; // every pixel masked (cloud/shadow) - the stats come back as the string "NaN", nothing to plot or render for this day
                }

                results.Add(new SceneStatEntry(
                    SourceSceneId: $"stat-{dt:yyyy-MM-dd}",
                    SceneDateUtc: DateOnly.FromDateTime(dt.UtcDateTime),
                    CloudPercent: 0, // Statistical API's aggregate response doesn't carry per-scene cloud_cover; Reliable is judged purely on ValidPixelPercent here.
                    ValidPixelPercent: validPercent,
                    Stats: new SatelliteIndexStats
                    {
                        Mean = ReadDouble(bandStats["mean"]),
                        Min = ReadDouble(bandStats["min"]),
                        Max = ReadDouble(bandStats["max"]),
                        StdDev = ReadDouble(bandStats["stDev"]),
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

        // CDSE serializes NaN/Infinity stats as JSON strings, which GetValue<double> throws on.
        private static double? ReadDouble(JsonNode? node) => node is JsonValue value && value.TryGetValue(out double d) ? d : null;

        private static HttpRequestMessage NewRequest(HttpMethod method, string url, string token, JsonObject body)
        {
            var request = new HttpRequestMessage(method, url)
            {
                Content = JsonContent.Create(body),
            };
            // Per-request header, never httpClient.DefaultRequestHeaders - this HttpClient is shared/pooled across every organization's calls.
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

            // Live-verified against CDSE (2026-09): parts carry no Content-Disposition header at all (only Content-Type: image/png), so matching by header name never fires - CDSE returns parts in the SAME ORDER as the request's output.responses array (default first, dataMask second), so position is the only reliable signal.
            byte[]? defaultPng = null, dataMaskPng = null;
            Microsoft.AspNetCore.WebUtilities.MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync(ct)) != null)
            {
                using var buffer = new MemoryStream();
                await section.Body.CopyToAsync(buffer, ct);
                byte[] bytes = buffer.ToArray();
                if (defaultPng == null)
                {
                    defaultPng = bytes;
                }
                else if (dataMaskPng == null)
                {
                    dataMaskPng = bytes;
                }
            }
            return (defaultPng, dataMaskPng);
        }

        // CDSE's Process API render needs explicit pixel width/height (see RenderIndexAsync's own remarks) - derived here from the polygon's geographic extent and a target meters-per-pixel, since CRS84 is degrees, not meters. Clamped to [1, MaxRenderPixels] both ways: a degenerate/near-zero polygon still gets a real request, and a huge polygon can't blow past CDSE's own per-request pixel cap.
        private const int MaxRenderPixels = 2500;
        internal static (int Width, int Height) ComputePixelDimensions(string geoJsonPolygon, double resxMeters, double resyMeters)
        {
            var coords = new List<(double Lon, double Lat)>();
            void Collect(JsonNode? node)
            {
                if (node is not JsonArray arr || arr.Count == 0)
                {
                    return;
                }
                if (arr[0] is JsonValue)
                {
                    coords.Add((arr[0]!.GetValue<double>(), arr[1]!.GetValue<double>()));
                    return;
                }
                foreach (JsonNode? child in arr)
                {
                    Collect(child);
                }
            }
            Collect(JsonNode.Parse(geoJsonPolygon)?["coordinates"]);

            double minLon = coords.Min(c => c.Lon), maxLon = coords.Max(c => c.Lon);
            double minLat = coords.Min(c => c.Lat), maxLat = coords.Max(c => c.Lat);
            const double MetersPerDegreeLat = 111320.0;
            double metersPerDegreeLon = MetersPerDegreeLat * Math.Cos((minLat + maxLat) / 2 * Math.PI / 180);
            int width = (int)Math.Round((maxLon - minLon) * metersPerDegreeLon / resxMeters);
            int height = (int)Math.Round((maxLat - minLat) * MetersPerDegreeLat / resyMeters);
            return (Math.Clamp(width, 1, MaxRenderPixels), Math.Clamp(height, 1, MaxRenderPixels));
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

        private static byte[] ApplyMaskAsAlpha(MinimalPng.Decoded value, MinimalPng.Decoded mask)
        {
            int pixelCount = value.Width * value.Height;
            var rgba = new byte[pixelCount * 4];
            for (int i = 0; i < pixelCount; i++)
            {
                int src = i * value.BytesPerPixel;
                rgba[i * 4] = value.Pixels[src];
                rgba[(i * 4) + 1] = value.BytesPerPixel >= 3 ? value.Pixels[src + 1] : value.Pixels[src];
                rgba[(i * 4) + 2] = value.BytesPerPixel >= 3 ? value.Pixels[src + 2] : value.Pixels[src];
                rgba[(i * 4) + 3] = i < mask.Pixels.Length && mask.Pixels[i] != 0 ? (byte)255 : (byte)0;
            }
            return MinimalPng.EncodeRgba8(value.Width, value.Height, rgba);
        }

        /// Only called for scalar indices (HasStatistics=true) - composites reuse their rendered PNG directly, they have no per-pixel scalar to store a time series against. Grid format: 4-byte width + 4-byte height (little-endian) header, then one byte per pixel (RenderScript's 0..Evalscripts.QuantizedMax quantization), 0xFF marking an invalid/masked pixel - see SatellitePaletteRenderer.Decode for the reader.
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
                    validValues.Add(Evalscripts.Dequantize(value.Pixels[i])); // same [-1,1] domain as the Statistical API's backfill stats, so one series never mixes scales
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
