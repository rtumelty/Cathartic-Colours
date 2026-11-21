// A minimal PNG encoder for test screenshots (CC-046, gap recorded in ADR-006).
//
// Texture2D.EncodeToPNG and ScreenCapture live in the Image Conversion and Screen Capture engine modules, which the
// project doesn't include, and adding them would change the player for a test-only need. This writes an 8-bit RGB
// PNG with System.IO.Compression, which every Unity platform has.
using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace TestSupport
{
    public static class PngWriter
    {
        static readonly uint[] crcTable = MakeCrcTable();

        /// <summary>Writes the pixels (bottom row first, as Unity stores them) as an RGB PNG.</summary>
        public static void Write(string path, Color32[] pixels, int width, int height)
        {
            File.WriteAllBytes(path, Encode(pixels, width, height));
        }

        public static byte[] Encode(Color32[] pixels, int width, int height)
        {
            if (pixels.Length != width * height)
                throw new ArgumentException($"{pixels.Length} pixels for a {width}x{height} image.");

            // Filter byte 0 (none) then RGB per row, top row first.
            var raw = new byte[height * (1 + width * 3)];
            int i = 0;
            for (int y = height - 1; y >= 0; y--)
            {
                raw[i++] = 0;
                for (int x = 0; x < width; x++)
                {
                    var p = pixels[y * width + x];
                    raw[i++] = p.r;
                    raw[i++] = p.g;
                    raw[i++] = p.b;
                }
            }

            using var png = new MemoryStream();
            png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);

            var header = new byte[13];
            WriteBigEndian(header, 0, (uint)width);
            WriteBigEndian(header, 4, (uint)height);
            header[8] = 8;  // bit depth
            header[9] = 2;  // colour type: RGB
            WriteChunk(png, "IHDR", header);
            WriteChunk(png, "IDAT", Zlib(raw));
            WriteChunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
        }

        static byte[] Zlib(byte[] data)
        {
            using var output = new MemoryStream();
            output.WriteByte(0x78); // zlib header: deflate, 32K window
            output.WriteByte(0x01);
            using (var deflate = new DeflateStream(output, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
                deflate.Write(data, 0, data.Length);
            uint a = 1, b = 0;
            foreach (byte d in data)
            {
                a = (a + d) % 65521;
                b = (b + a) % 65521;
            }
            var adler = new byte[4];
            WriteBigEndian(adler, 0, (b << 16) | a);
            output.Write(adler, 0, 4);
            return output.ToArray();
        }

        static void WriteChunk(Stream s, string type, byte[] data)
        {
            var length = new byte[4];
            WriteBigEndian(length, 0, (uint)data.Length);
            s.Write(length, 0, 4);
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes, 0, 4);
            s.Write(data, 0, data.Length);
            uint crc = 0xFFFFFFFF;
            foreach (byte t in typeBytes) crc = crcTable[(crc ^ t) & 0xFF] ^ (crc >> 8);
            foreach (byte d in data) crc = crcTable[(crc ^ d) & 0xFF] ^ (crc >> 8);
            var crcBytes = new byte[4];
            WriteBigEndian(crcBytes, 0, crc ^ 0xFFFFFFFF);
            s.Write(crcBytes, 0, 4);
        }

        static void WriteBigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        static uint[] MakeCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }
    }
}
