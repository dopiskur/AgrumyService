using Agrumy.Shared.Models;

namespace Agrumy.Api.Satellite
{
    /// One evalscript per index (Detaljni dizajn S, B2 step 5) - every scalar index outputs a "default" (1-band FLOAT32 value) plus a "dataMask" (1-band UINT8 validity) output, the shape both the Process API (raw grid) and the Statistical API (per-scene aggregation, masked by dataMask) expect from the same script. SwirComposite/NaturalColor are 3-band visual composites with no scalar meaning - HasStatistics=false, Process API (rendering) only.
    public static class Evalscripts
    {
        public sealed record Definition(string Script, bool HasStatistics);

        // SCL exclusion list mirrors SclCloudMask.InvalidClasses exactly - kept as a literal here (not templated in) so the fixture checksum test below catches an accidental drift between the two independently.
        private const string SclGuard = """
            function isValidScl(scl) {
              return [0,1,3,7,8,9,10].indexOf(scl) === -1;
            }
            """;

        public static readonly IReadOnlyDictionary<SatelliteIndex, Definition> All = new Dictionary<SatelliteIndex, Definition>
        {
            [SatelliteIndex.Ndvi] = new(BuildScalar(["B04", "B08"], "(s.B08 - s.B04) / (s.B08 + s.B04)"), true),
            [SatelliteIndex.Ndmi] = new(BuildScalar(["B08", "B11"], "(s.B08 - s.B11) / (s.B08 + s.B11)"), true),
            [SatelliteIndex.Ndwi] = new(BuildScalar(["B03", "B08"], "(s.B03 - s.B08) / (s.B03 + s.B08)"), true),
            [SatelliteIndex.Ndsi] = new(BuildScalar(["B03", "B11"], "(s.B03 - s.B11) / (s.B03 + s.B11)"), true),
            [SatelliteIndex.SwirComposite] = new(BuildComposite(["B12", "B08", "B04"]), false),
            [SatelliteIndex.NaturalColor] = new(BuildComposite(["B04", "B03", "B02"]), false),
        };

        /// S-B2, D3 - PlanetScope has no SWIR band, so only NDVI/NDWI/NaturalColor exist for it; band names (blue/green/red/nir) match Sentinel Hub's PlanetScope collection docs (collections.sentinel-hub.com/planetscope), not Sentinel-2's B0x convention. No cloud/shadow mask evalscript function exists yet for PlanetScope's own udm1/cloud bands - built from documentation, not exercised against a live PlanetScope response.
        public static readonly IReadOnlyDictionary<SatelliteIndex, Definition> PlanetScope = new Dictionary<SatelliteIndex, Definition>
        {
            [SatelliteIndex.Ndvi] = new(BuildScalarNoScl(["red", "nir"], "(s.nir - s.red) / (s.nir + s.red)"), true),
            [SatelliteIndex.Ndwi] = new(BuildScalarNoScl(["green", "nir"], "(s.green - s.nir) / (s.green + s.nir)"), true),
            [SatelliteIndex.NaturalColor] = new(BuildCompositeNoScl(["red", "green", "blue"]), false),
        };

        private static string BuildScalarNoScl(string[] bands, string formula) =>
            $$"""
            //VERSION=3
            function setup() {
              return {
                input: [{ bands: [{{string.Join(", ", bands.Select(b => $"\"{b}\""))}}, "clear"] }],
                output: [
                  { id: "default", bands: 1, sampleType: "FLOAT32" },
                  { id: "dataMask", bands: 1, sampleType: "UINT8" }
                ]
              };
            }
            function evaluatePixel(s) {
              let value = {{formula}};
              return { default: [value], dataMask: [s.clear] };
            }
            """;

        private static string BuildCompositeNoScl(string[] rgbBands) =>
            $$"""
            //VERSION=3
            function setup() {
              return {
                input: [{ bands: [{{string.Join(", ", rgbBands.Select(b => $"\"{b}\""))}}, "clear"] }],
                output: [
                  { id: "default", bands: 3, sampleType: "UINT8" },
                  { id: "dataMask", bands: 1, sampleType: "UINT8" }
                ]
              };
            }
            function evaluatePixel(s) {
              let gain = 2.5, gamma = 1.8;
              function tone(v) { return 255 * Math.pow(Math.min(1, v * gain), 1 / gamma); }
              return { default: [tone(s.{{rgbBands[0]}}), tone(s.{{rgbBands[1]}}), tone(s.{{rgbBands[2]}})], dataMask: [s.clear] };
            }
            """;

        private static string BuildScalar(string[] bands, string formula) =>
            $$"""
            //VERSION=3
            function setup() {
              return {
                input: [{ bands: [{{string.Join(", ", bands.Select(b => $"\"{b}\""))}}, "SCL"] }],
                output: [
                  { id: "default", bands: 1, sampleType: "FLOAT32" },
                  { id: "dataMask", bands: 1, sampleType: "UINT8" }
                ]
              };
            }
            function evaluatePixel(s) {
              let value = {{formula}};
              return { default: [value], dataMask: [isValidScl(s.SCL) ? 1 : 0] };
            }
            {{SclGuard}}
            """;

        private static string BuildComposite(string[] rgbBands) =>
            $$"""
            //VERSION=3
            function setup() {
              return {
                input: [{ bands: [{{string.Join(", ", rgbBands.Select(b => $"\"{b}\""))}}, "SCL"] }],
                output: [
                  { id: "default", bands: 3, sampleType: "UINT8" },
                  { id: "dataMask", bands: 1, sampleType: "UINT8" }
                ]
              };
            }
            // 2.5x gain + gamma matches Sentinel Hub's own "Highlight Optimized Natural Color" preset, not a raw linear stretch - raw reflectance renders near-black for most crop canopy.
            function evaluatePixel(s) {
              let gain = 2.5, gamma = 1.8;
              function tone(v) { return 255 * Math.pow(Math.min(1, v * gain), 1 / gamma); }
              return {
                default: [tone(s.{{rgbBands[0]}}), tone(s.{{rgbBands[1]}}), tone(s.{{rgbBands[2]}})],
                dataMask: [isValidScl(s.SCL) ? 1 : 0]
              };
            }
            {{SclGuard}}
            """;
    }
}
