using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CCT.Tests.ArtV1
{
    /// <summary>
    /// 테스트용 PNG 생성기. UnityEngine 없이 검수기 입력 파일을 만든다.
    /// 좌표는 좌하단 원점(04 §7)으로 받고 PNG 행 순서(위쪽부터)로 바꿔 쓴다.
    /// </summary>
    public sealed class ArtV1TestImage
    {
        public readonly int Width;
        public readonly int Height;
        public readonly byte[] Rgba;

        public ArtV1TestImage(int width, int height)
        {
            Width = width;
            Height = height;
            Rgba = new byte[width * height * 4];
        }

        public ArtV1TestImage Set(int x, int yFromBottom, byte r, byte g, byte b, byte a)
        {
            int o = ((Height - 1 - yFromBottom) * Width + x) * 4;
            Rgba[o] = r; Rgba[o + 1] = g; Rgba[o + 2] = b; Rgba[o + 3] = a;
            return this;
        }

        /// <summary>x0..x1, y0..y1(좌하단 원점, 양끝 포함) 사각형을 칠한다.</summary>
        public ArtV1TestImage Fill(int x0, int y0, int x1, int y1, byte r, byte g, byte b, byte a)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Set(x, y, r, g, b, a);
            return this;
        }

        /// <summary>일반 몸체 프레임: 접지선 y=foot부터 높이 height 픽셀의 불투명 몸통, 나머지는 실제 알파 0.</summary>
        public static ArtV1TestImage Body(int canvas, int foot, int height, int halfWidth = 40)
        {
            int cx = canvas / 2;
            return new ArtV1TestImage(canvas, canvas).Fill(cx - halfWidth, foot, cx + halfWidth, foot + height - 1, 90, 60, 40, 255);
        }

        /// <summary>흰/밝은 회색 8px 바둑판을 알파 255로 전체에 그린다 — 가짜 투명 배경 재현.</summary>
        public ArtV1TestImage PaintCheckerboard(int x0, int y0, int x1, int y1, int cell = 8)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    byte v = ((x / cell) + (y / cell)) % 2 == 0 ? (byte)255 : (byte)204;
                    Set(x, y, v, v, v, 255);
                }
            return this;
        }
    }

    public static class ArtV1TestPng
    {
        /// <param name="filter">모든 행에 쓸 PNG 필터 0..4 (디코더 필터 해제 검증용).</param>
        public static byte[] EncodeRgba(ArtV1TestImage image, int filter = 0) =>
            Encode(image.Width, image.Height, 6, 4, image.Rgba, filter);

        /// <summary>알파 채널 없는 RGB PNG (알파는 버린다).</summary>
        public static byte[] EncodeRgb(ArtV1TestImage image, int filter = 0)
        {
            var rgb = new byte[image.Width * image.Height * 3];
            for (int i = 0, j = 0; i < image.Rgba.Length; i += 4, j += 3)
            {
                rgb[j] = image.Rgba[i];
                rgb[j + 1] = image.Rgba[i + 1];
                rgb[j + 2] = image.Rgba[i + 2];
            }
            return Encode(image.Width, image.Height, 2, 3, rgb, filter);
        }

        public static void Write(string path, byte[] png)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, png);
        }

        static byte[] Encode(int width, int height, byte colorType, int bpp, byte[] pixels, int filter)
        {
            int stride = width * bpp;
            var raw = new byte[(stride + 1) * height];
            for (int y = 0; y < height; y++)
            {
                raw[y * (stride + 1)] = (byte)filter;
                for (int x = 0; x < stride; x++)
                {
                    int cur = pixels[y * stride + x];
                    int left = x >= bpp ? pixels[y * stride + x - bpp] : 0;
                    int up = y > 0 ? pixels[(y - 1) * stride + x] : 0;
                    int upLeft = (y > 0 && x >= bpp) ? pixels[(y - 1) * stride + x - bpp] : 0;
                    int predicted;
                    switch (filter)
                    {
                        case 0: predicted = 0; break;
                        case 1: predicted = left; break;
                        case 2: predicted = up; break;
                        case 3: predicted = (left + up) >> 1; break;
                        case 4: predicted = Paeth(left, up, upLeft); break;
                        default: throw new ArgumentOutOfRangeException(nameof(filter));
                    }
                    raw[y * (stride + 1) + 1 + x] = (byte)(cur - predicted);
                }
            }

            using (var ms = new MemoryStream())
            {
                ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                var ihdr = new byte[13];
                WriteBE(ihdr, 0, width);
                WriteBE(ihdr, 4, height);
                ihdr[8] = 8;
                ihdr[9] = colorType;
                WriteChunk(ms, "IHDR", ihdr);
                WriteChunk(ms, "IDAT", Zlib(raw));
                WriteChunk(ms, "IEND", new byte[0]);
                return ms.ToArray();
            }
        }

        static byte[] Zlib(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);
                using (var deflate = new DeflateStream(ms, CompressionMode.Compress, true))
                    deflate.Write(data, 0, data.Length);
                uint a = 1, b = 0;
                foreach (byte d in data)
                {
                    a = (a + d) % 65521;
                    b = (b + a) % 65521;
                }
                var adler = new byte[4];
                WriteBE(adler, 0, (int)((b << 16) | a));
                ms.Write(adler, 0, 4);
                return ms.ToArray();
            }
        }

        static void WriteChunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            WriteBE(len, 0, data.Length);
            s.Write(len, 0, 4);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes, 0, 4);
            s.Write(data, 0, data.Length);
            uint crc = 0xFFFFFFFF;
            crc = Crc(crc, typeBytes);
            crc = Crc(crc, data);
            var c = new byte[4];
            WriteBE(c, 0, (int)(crc ^ 0xFFFFFFFF));
            s.Write(c, 0, 4);
        }

        static uint Crc(uint crc, byte[] data)
        {
            foreach (byte d in data)
            {
                crc ^= d;
                for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }
            return crc;
        }

        static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            return pb <= pc ? b : c;
        }

        static void WriteBE(byte[] d, int o, int v)
        {
            d[o] = (byte)(v >> 24);
            d[o + 1] = (byte)(v >> 16);
            d[o + 2] = (byte)(v >> 8);
            d[o + 3] = (byte)v;
        }
    }
}
