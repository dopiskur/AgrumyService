using System.Buffers.Binary;
using System.IO.Compression;

namespace Agrumy.Api.Satellite
{
    /// Encode/decode for exactly the PNG subset this module needs - 8-bit grayscale or 8-bit RGB, no interlacing, filter method 0 (Detaljni dizajn S, B4 - "SatellitePaletteRenderer, pure C#, no GDAL"). Decode side reads what Sentinel Hub's Process API returns when asked for image/png; encode side renders the palette-applied display PNG from a stored grid. Deliberately not a general PNG library - anything outside this subset (16-bit, palette color type, interlacing) throws rather than silently misreading.
    public static class MinimalPng
    {
        private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        public static byte[] EncodeGrayscale8(int width, int height, byte[] pixels) => Encode(width, height, pixels, bytesPerPixel: 1, colorType: 0);

        public static byte[] EncodeRgb8(int width, int height, byte[] rgbInterleaved) => Encode(width, height, rgbInterleaved, bytesPerPixel: 3, colorType: 2);

        public static byte[] EncodeRgba8(int width, int height, byte[] rgbaInterleaved) => Encode(width, height, rgbaInterleaved, bytesPerPixel: 4, colorType: 6);

        private static byte[] Encode(int width, int height, byte[] pixels, int bytesPerPixel, byte colorType)
        {
            if (pixels.Length != width * height * bytesPerPixel)
            {
                throw new ArgumentException($"Expected {width * height * bytesPerPixel} pixel bytes, got {pixels.Length}.");
            }

            // Filter type 0 (None) per scanline - simplest correct encoding; PNG doesn't require adaptive filtering, only that the decoder honor whatever filter byte is written.
            int stride = width * bytesPerPixel;
            var raw = new byte[(stride + 1) * height];
            for (int y = 0; y < height; y++)
            {
                raw[y * (stride + 1)] = 0;
                Buffer.BlockCopy(pixels, y * stride, raw, (y * (stride + 1)) + 1, stride);
            }

            using var idatStream = new MemoryStream();
            using (var zlib = new ZLibStream(idatStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(raw, 0, raw.Length);
            }

            using var output = new MemoryStream();
            output.Write(Signature);
            WriteChunk(output, "IHDR", BuildIhdr(width, height, colorType));
            WriteChunk(output, "IDAT", idatStream.ToArray());
            WriteChunk(output, "IEND", []);
            return output.ToArray();
        }

        private static byte[] BuildIhdr(int width, int height, byte colorType)
        {
            var ihdr = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
            BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
            ihdr[8] = 8; // bit depth
            ihdr[9] = colorType;
            ihdr[10] = 0; // compression method
            ihdr[11] = 0; // filter method
            ihdr[12] = 0; // interlace method
            return ihdr;
        }

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            Span<byte> lengthBuf = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(lengthBuf, data.Length);
            output.Write(lengthBuf);

            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            output.Write(typeBytes);
            output.Write(data);

            uint crc = Crc32(typeBytes, data);
            Span<byte> crcBuf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crcBuf, crc);
            output.Write(crcBuf);
        }

        public sealed record Decoded(int Width, int Height, int BytesPerPixel, byte[] Pixels);

        public static Decoded Decode(byte[] png)
        {
            if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature))
            {
                throw new InvalidDataException("Not a PNG file (bad signature).");
            }

            int width = 0, height = 0;
            byte colorType = 0, bitDepth = 0, interlace = 0;
            using var idat = new MemoryStream();
            int pos = 8;
            while (pos < png.Length)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos, 4));
                string type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
                int dataStart = pos + 8;
                if (type == "IHDR")
                {
                    width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(dataStart, 4));
                    height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(dataStart + 4, 4));
                    bitDepth = png[dataStart + 8];
                    colorType = png[dataStart + 9];
                    interlace = png[dataStart + 12];
                }
                else if (type == "IDAT")
                {
                    idat.Write(png, dataStart, length);
                }
                else if (type == "IEND")
                {
                    break;
                }
                pos = dataStart + length + 4; // skip CRC
            }

            if (bitDepth != 8)
            {
                throw new NotSupportedException($"Only 8-bit PNGs are supported (got bit depth {bitDepth}).");
            }
            if (interlace != 0)
            {
                throw new NotSupportedException("Interlaced PNGs are not supported.");
            }
            int bytesPerPixel = colorType switch
            {
                0 => 1, // grayscale
                2 => 3, // RGB
                6 => 4, // RGBA
                _ => throw new NotSupportedException($"Unsupported PNG color type {colorType}."),
            };

            idat.Position = 0;
            using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
            using var decompressed = new MemoryStream();
            zlib.CopyTo(decompressed);
            byte[] raw = decompressed.ToArray();

            int stride = width * bytesPerPixel;
            var pixels = new byte[stride * height];
            var previousScanline = new byte[stride];
            for (int y = 0; y < height; y++)
            {
                int rowStart = y * (stride + 1);
                byte filterType = raw[rowStart];
                var scanline = new byte[stride];
                Buffer.BlockCopy(raw, rowStart + 1, scanline, 0, stride);
                Unfilter(filterType, scanline, previousScanline, bytesPerPixel);
                Buffer.BlockCopy(scanline, 0, pixels, y * stride, stride);
                previousScanline = scanline;
            }

            return new Decoded(width, height, bytesPerPixel, pixels);
        }

        private static void Unfilter(byte filterType, byte[] scanline, byte[] previous, int bpp)
        {
            for (int i = 0; i < scanline.Length; i++)
            {
                int a = i >= bpp ? scanline[i - bpp] : 0;
                int b = previous[i];
                int c = i >= bpp ? previous[i - bpp] : 0;
                int recon = filterType switch
                {
                    0 => scanline[i],
                    1 => scanline[i] + a,
                    2 => scanline[i] + b,
                    3 => scanline[i] + ((a + b) / 2),
                    4 => scanline[i] + PaethPredictor(a, b, c),
                    _ => throw new NotSupportedException($"Unsupported PNG filter type {filterType}."),
                };
                scanline[i] = (byte)(recon & 0xFF);
            }
        }

        private static int PaethPredictor(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc)
            {
                return a;
            }
            return pb <= pc ? b : c;
        }

        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                }
                table[n] = c;
            }
            return table;
        }

        private static uint Crc32(byte[] type, byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in type)
            {
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }
            foreach (byte b in data)
            {
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }
            return crc ^ 0xFFFFFFFF;
        }
    }
}
