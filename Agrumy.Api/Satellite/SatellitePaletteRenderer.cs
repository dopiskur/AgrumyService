using Agrumy.Shared.Models;

namespace Agrumy.Api.Satellite
{
    /// Renders a stored scalar-index grid (D9's GridBase64) into a display PNG with a fixed per-index palette (Detaljni dizajn S, D10/S-C step 3 - "paleta po indeksu fiksna preko cijele razine"), pure C# via MinimalPng, no GDAL. Only meaningful for scalar indices (NDVI/NDMI/NDWI/NDSI) - composites (SwirComposite/NaturalColor) are already an RGB PNG and never go through this.
    public static class SatellitePaletteRenderer
    {
        public sealed record DecodedGrid(int Width, int Height, byte[] Pixels);

        /// Reads the width/height header CdseSentinelHubSource.BuildQuantizedGrid writes, then the one-byte-per-pixel payload.
        public static DecodedGrid Decode(byte[] grid)
        {
            if (grid.Length < 8)
            {
                throw new ArgumentException("Grid is too short to contain a width/height header.");
            }
            int width = BitConverter.ToInt32(grid, 0);
            int height = BitConverter.ToInt32(grid, 4);
            if (width <= 0 || height <= 0 || grid.Length - 8 != width * height)
            {
                throw new ArgumentException($"Grid header ({width}x{height}) doesn't match payload length {grid.Length - 8}.");
            }
            var pixels = new byte[width * height];
            Buffer.BlockCopy(grid, 8, pixels, 0, pixels.Length);
            return new DecodedGrid(width, height, pixels);
        }

        /// Diverging color stops per index, 0-255 domain (evalscript's own quantization of the [-1,1] index range); 0xFF (invalid) pixels render fully transparent so the parcel outline shows through instead of a false color.
        public static byte[] RenderPng(byte[] grid, SatelliteIndex index)
        {
            DecodedGrid decoded = Decode(grid);
            (byte R, byte G, byte B)[] palette = PaletteFor(index);
            var rgba = new byte[decoded.Pixels.Length * 4];
            for (int i = 0; i < decoded.Pixels.Length; i++)
            {
                byte value = decoded.Pixels[i];
                int o = i * 4;
                if (value == 0xFF)
                {
                    rgba[o + 3] = 0; // fully transparent - alpha-out invalid pixels
                    continue;
                }
                (byte r, byte g, byte b) = palette[value];
                rgba[o] = r;
                rgba[o + 1] = g;
                rgba[o + 2] = b;
                rgba[o + 3] = 255;
            }
            return MinimalPng.EncodeRgba8(decoded.Width, decoded.Height, rgba);
        }

        private static readonly Dictionary<SatelliteIndex, (byte R, byte G, byte B)[]> PaletteCache = [];

        private static (byte R, byte G, byte B)[] PaletteFor(SatelliteIndex index)
        {
            if (PaletteCache.TryGetValue(index, out var cached))
            {
                return cached;
            }
            (byte, byte, byte)[] stops = index switch
            {
                // Brown (bare/stressed) -> yellow -> green (dense canopy), the conventional NDVI ramp.
                SatelliteIndex.Ndvi => [(140, 81, 10), (223, 194, 125), (247, 247, 247), (161, 215, 106), (0, 109, 44)],
                // Tan (dry) -> blue (high moisture).
                SatelliteIndex.Ndmi => [(166, 97, 26), (223, 194, 125), (245, 245, 245), (128, 205, 193), (1, 102, 94)],
                // Green (land) -> white -> deep blue (open water).
                SatelliteIndex.Ndwi => [(1, 102, 94), (128, 205, 193), (245, 245, 245), (140, 81, 10), (84, 48, 5)],
                // Grey (no snow) -> cyan/white (snow/ice).
                SatelliteIndex.Ndsi => [(82, 82, 82), (189, 189, 189), (240, 240, 240), (158, 202, 225), (8, 81, 156)],
                _ => throw new ArgumentException($"{index} is a visual composite - it has no scalar palette.", nameof(index)),
            };
            var full = BuildGradient(stops, 256);
            PaletteCache[index] = full;
            return full;
        }

        private static (byte R, byte G, byte B)[] BuildGradient((byte R, byte G, byte B)[] stops, int steps)
        {
            var result = new (byte, byte, byte)[steps];
            int segments = stops.Length - 1;
            for (int i = 0; i < steps; i++)
            {
                double t = (double)i / (steps - 1) * segments;
                int segment = Math.Min((int)t, segments - 1);
                double frac = t - segment;
                (byte R, byte G, byte B) a = stops[segment];
                (byte R, byte G, byte B) b = stops[segment + 1];
                result[i] = (
                    (byte)Math.Round(a.R + ((b.R - a.R) * frac)),
                    (byte)Math.Round(a.G + ((b.G - a.G) * frac)),
                    (byte)Math.Round(a.B + ((b.B - a.B) * frac)));
            }
            return result;
        }
    }
}
