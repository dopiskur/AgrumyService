using Agrumy.Shared.Models;

namespace Agrumy.Api.Satellite
{
    /// One evalscript pair per index (Detaljni dizajn S, B2 step 5) - Script is the FLOAT32 form the Statistical API aggregates over, RenderScript the UINT8 form the Process API can write as image/png (CDSE rejects FLOAT32 for PNG outright); both emit the same "default" + "dataMask" outputs. SwirComposite/NaturalColor are 3-band visual composites with no scalar meaning - HasStatistics=false, one script serves both roles.
    public static class Evalscripts
    {
        public sealed record Definition(string Script, bool HasStatistics, string RenderScript);

        /// Render quantization: 0..254 spans [-1, 1], so 255 stays free as the stored grid's invalid-pixel sentinel (see SatellitePaletteRenderer.RenderPng).
        public const int QuantizedMax = 254;

        public static double Dequantize(byte quantized) => quantized / (double)QuantizedMax * 2 - 1;

        // SCL exclusion list mirrors SclCloudMask.InvalidClasses exactly - kept as a literal here (not templated in) so the fixture checksum test below catches an accidental drift between the two independently.
        private const string SclGuard = """
            function isValidScl(scl) {
              return [0,1,3,7,8,9,10].indexOf(scl) === -1;
            }
            """;

        public static readonly IReadOnlyDictionary<SatelliteIndex, Definition> All = new Dictionary<SatelliteIndex, Definition>
        {
            [SatelliteIndex.Ndvi] = Scalar(["B04", "B08"], "(s.B08 - s.B04) / (s.B08 + s.B04)"),
            [SatelliteIndex.Ndmi] = Scalar(["B08", "B11"], "(s.B08 - s.B11) / (s.B08 + s.B11)"),
            [SatelliteIndex.Ndwi] = Scalar(["B03", "B08"], "(s.B03 - s.B08) / (s.B03 + s.B08)"),
            [SatelliteIndex.Ndsi] = Scalar(["B03", "B11"], "(s.B03 - s.B11) / (s.B03 + s.B11)"),
            [SatelliteIndex.SwirComposite] = Composite(BuildComposite(["B12", "B08", "B04"])),
            [SatelliteIndex.NaturalColor] = Composite(BuildComposite(["B04", "B03", "B02"])),
        };

        /// S-B2, D3 - PlanetScope has no SWIR band, so only NDVI/NDWI/NaturalColor exist for it; band names (blue/green/red/nir) match Sentinel Hub's PlanetScope collection docs (collections.sentinel-hub.com/planetscope), not Sentinel-2's B0x convention. No cloud/shadow mask evalscript function exists yet for PlanetScope's own udm1/cloud bands - built from documentation, not exercised against a live PlanetScope response.
        public static readonly IReadOnlyDictionary<SatelliteIndex, Definition> PlanetScope = new Dictionary<SatelliteIndex, Definition>
        {
            [SatelliteIndex.Ndvi] = ScalarNoScl(["red", "nir"], "(s.nir - s.red) / (s.nir + s.red)"),
            [SatelliteIndex.Ndwi] = ScalarNoScl(["green", "nir"], "(s.green - s.nir) / (s.green + s.nir)"),
            [SatelliteIndex.NaturalColor] = Composite(BuildCompositeNoScl(["red", "green", "blue"])),
        };

        private static Definition Scalar(string[] bands, string formula) =>
            new(BuildScalar(bands, formula, quantize: false), true, BuildScalar(bands, formula, quantize: true));

        private static Definition ScalarNoScl(string[] bands, string formula) =>
            new(BuildScalarNoScl(bands, formula, quantize: false), true, BuildScalarNoScl(bands, formula, quantize: true));

        private static Definition Composite(string script) => new(script, false, script);

        private static string BandList(IEnumerable<string> bands) => string.Join(", ", bands.Select(b => $"\"{b}\""));

        private static string ScalarBody(string formula, string validExpr, bool quantize) => quantize
            ? $$"""
              function evaluatePixel(s) {
                let value = {{formula}};
                let valid = {{validExpr}} && isFinite(value);
                let q = Math.max(0, Math.min({{QuantizedMax}}, Math.round((value + 1) / 2 * {{QuantizedMax}})));
                return { default: [valid ? q : 0], dataMask: [valid ? 1 : 0] };
              }
              """
            : $$"""
              function evaluatePixel(s) {
                let value = {{formula}};
                return { default: [value], dataMask: [{{validExpr}} ? 1 : 0] };
              }
              """;

        private static string BuildScalarNoScl(string[] bands, string formula, bool quantize) =>
            $$"""
            //VERSION=3
            function setup() {
              return {
                input: [{ bands: [{{BandList(bands)}}, "clear"] }],
                output: [
                  { id: "default", bands: 1, sampleType: "{{(quantize ? "UINT8" : "FLOAT32")}}" },
                  { id: "dataMask", bands: 1, sampleType: "UINT8" }
                ]
              };
            }
            {{ScalarBody(formula, "s.clear", quantize)}}
            """;

        private static string BuildCompositeNoScl(string[] rgbBands) =>
            $$"""
            //VERSION=3
            function setup() {
              return {
                input: [{ bands: [{{BandList(rgbBands)}}, "clear"] }],
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

        private static string BuildScalar(string[] bands, string formula, bool quantize) =>
            $$"""
            //VERSION=3
            function setup() {
              return {
                input: [{ bands: [{{BandList(bands)}}, "SCL"] }],
                output: [
                  { id: "default", bands: 1, sampleType: "{{(quantize ? "UINT8" : "FLOAT32")}}" },
                  { id: "dataMask", bands: 1, sampleType: "UINT8" }
                ]
              };
            }
            {{ScalarBody(formula, "isValidScl(s.SCL)", quantize)}}
            {{SclGuard}}
            """;

        private static string BuildComposite(string[] rgbBands) =>
            $$"""
            //VERSION=3
            function setup() {
              return {
                input: [{ bands: [{{BandList(rgbBands)}}, "SCL"] }],
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
