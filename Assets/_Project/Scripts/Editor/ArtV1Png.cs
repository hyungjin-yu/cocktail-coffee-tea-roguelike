using System;
using System.IO;
using System.IO.Compression;

namespace CCT.Editor
{
    /// <summary>
    /// Art v1 검수용 PNG 읽기. 헤더(크기·색 형식·tRNS 유무)와 RGBA8 픽셀을 돌려준다.
    /// Texture2D.LoadImage는 알파 채널이 없는 PNG도 RGBA로 바꿔버려 "원본에 알파가 있었는가"를
    /// 구분할 수 없고 Unity 밖에서 테스트할 수도 없어서 직접 디코딩한다.
    /// 지원: 비트 깊이 8(모든 색 형식)과 16(그레이/RGB/RGBA, 상위 바이트 사용), 비인터레이스.
    /// 픽셀 배열은 PNG 순서(위쪽 행부터)이며 좌하단 원점 좌표 변환은 호출하는 쪽이 한다.
    /// </summary>
    public sealed class ArtV1Png
    {
        public const byte ColorGray = 0;
        public const byte ColorRgb = 2;
        public const byte ColorPalette = 3;
        public const byte ColorGrayAlpha = 4;
        public const byte ColorRgba = 6;

        static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public int Width { get; private set; }
        public int Height { get; private set; }
        public byte BitDepth { get; private set; }
        public byte ColorType { get; private set; }
        public byte Interlace { get; private set; }
        public bool HasTransparencyChunk { get; private set; }

        /// <summary>원본 파일 형식 자체에 알파 정보가 있는가 (RGBA/GrayAlpha 또는 tRNS 청크).</summary>
        public bool HasAlphaChannel =>
            ColorType == ColorRgba || ColorType == ColorGrayAlpha || HasTransparencyChunk;

        /// <summary>RGBA8, 위쪽 행부터. 디코딩하지 않았거나 실패하면 null.</summary>
        public byte[] Rgba { get; private set; }

        /// <summary>헤더는 읽었지만 픽셀 디코딩을 지원하지 않는 경우의 사유.</summary>
        public string DecodeError { get; private set; }

        public byte AlphaAt(int x, int yFromTop) => Rgba[(yFromTop * Width + x) * 4 + 3];

        public static ArtV1Png ReadFile(string path, bool decodePixels = true) =>
            Read(File.ReadAllBytes(path), decodePixels);

        public static ArtV1Png Read(byte[] data, bool decodePixels = true)
        {
            if (data == null || data.Length < 8) throw new InvalidDataException("PNG가 아니다: 파일이 너무 짧다");
            for (int k = 0; k < 8; k++)
                if (data[k] != Signature[k]) throw new InvalidDataException("PNG 시그니처가 아니다");

            var png = new ArtV1Png();
            var idat = new MemoryStream();
            byte[] palette = null;
            byte[] paletteAlpha = null;
            int transGray = -1;
            int transR = -1, transG = -1, transB = -1;
            bool sawHeader = false;

            int pos = 8;
            while (pos + 8 <= data.Length)
            {
                int length = ReadInt32BE(data, pos);
                if (length < 0 || pos + 12 + length > data.Length) throw new InvalidDataException("PNG 청크 길이가 잘못됐다");
                string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
                int body = pos + 8;

                switch (type)
                {
                    case "IHDR":
                        png.Width = ReadInt32BE(data, body);
                        png.Height = ReadInt32BE(data, body + 4);
                        png.BitDepth = data[body + 8];
                        png.ColorType = data[body + 9];
                        png.Interlace = data[body + 12];
                        sawHeader = true;
                        break;
                    case "PLTE":
                        palette = new byte[length];
                        Buffer.BlockCopy(data, body, palette, 0, length);
                        break;
                    case "tRNS":
                        png.HasTransparencyChunk = true;
                        if (png.ColorType == ColorPalette)
                        {
                            paletteAlpha = new byte[length];
                            Buffer.BlockCopy(data, body, paletteAlpha, 0, length);
                        }
                        else if (png.ColorType == ColorGray && length >= 2)
                        {
                            transGray = (data[body] << 8) | data[body + 1];
                        }
                        else if (png.ColorType == ColorRgb && length >= 6)
                        {
                            transR = (data[body] << 8) | data[body + 1];
                            transG = (data[body + 2] << 8) | data[body + 3];
                            transB = (data[body + 4] << 8) | data[body + 5];
                        }
                        break;
                    case "IDAT":
                        idat.Write(data, body, length);
                        break;
                }

                pos = body + length + 4; // CRC 건너뜀
                if (type == "IEND") break;
            }

            if (!sawHeader) throw new InvalidDataException("IHDR 청크가 없다");
            if (!decodePixels) return png;

            if (png.Interlace != 0) { png.DecodeError = "인터레이스(Adam7) PNG는 검수 디코딩을 지원하지 않는다"; return png; }
            int channels = ChannelCount(png.ColorType);
            if (channels == 0) { png.DecodeError = $"알 수 없는 색 형식 {png.ColorType}"; return png; }
            bool depthOk = png.BitDepth == 8 || (png.BitDepth == 16 && png.ColorType != ColorPalette);
            if (!depthOk) { png.DecodeError = $"비트 깊이 {png.BitDepth}는 검수 디코딩을 지원하지 않는다"; return png; }

            int bytesPerSample = png.BitDepth / 8;
            int bpp = channels * bytesPerSample;
            int stride = png.Width * bpp;
            byte[] raw = Inflate(idat.ToArray());
            long expected = (long)(stride + 1) * png.Height;
            if (raw.Length < expected) throw new InvalidDataException("IDAT 데이터가 이미지 크기보다 짧다");

            var rows = Unfilter(raw, png.Height, stride, bpp);
            var rgba = new byte[png.Width * png.Height * 4];
            for (int y = 0; y < png.Height; y++)
            {
                int rowStart = y * stride;
                for (int x = 0; x < png.Width; x++)
                {
                    int src = rowStart + x * bpp;
                    int dst = (y * png.Width + x) * 4;
                    byte r, g, b, a = 255;
                    switch (png.ColorType)
                    {
                        case ColorGray:
                        {
                            int v = Sample(rows, src, bytesPerSample);
                            r = g = b = rows[src];
                            if (transGray >= 0 && v == transGray) a = 0;
                            break;
                        }
                        case ColorGrayAlpha:
                            r = g = b = rows[src];
                            a = rows[src + bytesPerSample];
                            break;
                        case ColorRgb:
                        {
                            r = rows[src];
                            g = rows[src + bytesPerSample];
                            b = rows[src + 2 * bytesPerSample];
                            if (transR >= 0
                                && Sample(rows, src, bytesPerSample) == transR
                                && Sample(rows, src + bytesPerSample, bytesPerSample) == transG
                                && Sample(rows, src + 2 * bytesPerSample, bytesPerSample) == transB) a = 0;
                            break;
                        }
                        case ColorRgba:
                            r = rows[src];
                            g = rows[src + bytesPerSample];
                            b = rows[src + 2 * bytesPerSample];
                            a = rows[src + 3 * bytesPerSample];
                            break;
                        default: // palette
                        {
                            int index = rows[src];
                            if (palette == null || index * 3 + 2 >= palette.Length)
                                throw new InvalidDataException("팔레트 색인이 PLTE 범위를 벗어난다");
                            r = palette[index * 3];
                            g = palette[index * 3 + 1];
                            b = palette[index * 3 + 2];
                            if (paletteAlpha != null && index < paletteAlpha.Length) a = paletteAlpha[index];
                            break;
                        }
                    }
                    rgba[dst] = r; rgba[dst + 1] = g; rgba[dst + 2] = b; rgba[dst + 3] = a;
                }
            }
            png.Rgba = rgba;
            return png;
        }

        static int Sample(byte[] rows, int offset, int bytesPerSample) =>
            bytesPerSample == 1 ? rows[offset] : (rows[offset] << 8) | rows[offset + 1];

        static int ChannelCount(byte colorType)
        {
            switch (colorType)
            {
                case ColorGray: return 1;
                case ColorRgb: return 3;
                case ColorPalette: return 1;
                case ColorGrayAlpha: return 2;
                case ColorRgba: return 4;
                default: return 0;
            }
        }

        static byte[] Inflate(byte[] zlib)
        {
            if (zlib.Length < 2) throw new InvalidDataException("IDAT가 비어 있다");
            if ((zlib[1] & 0x20) != 0) throw new InvalidDataException("사전(FDICT)을 쓰는 zlib 스트림은 지원하지 않는다");
            using (var input = new MemoryStream(zlib, 2, zlib.Length - 2))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                deflate.CopyTo(output);
                return output.ToArray();
            }
        }

        /// <summary>PNG 필터(None/Sub/Up/Average/Paeth)를 풀어 필터 바이트가 없는 행 배열을 만든다.</summary>
        static byte[] Unfilter(byte[] raw, int height, int stride, int bpp)
        {
            var result = new byte[stride * height];
            for (int y = 0; y < height; y++)
            {
                int filter = raw[y * (stride + 1)];
                int src = y * (stride + 1) + 1;
                int dst = y * stride;
                int prev = dst - stride;
                for (int x = 0; x < stride; x++)
                {
                    int left = x >= bpp ? result[dst + x - bpp] : 0;
                    int up = y > 0 ? result[prev + x] : 0;
                    int upLeft = (y > 0 && x >= bpp) ? result[prev + x - bpp] : 0;
                    int value = raw[src + x];
                    switch (filter)
                    {
                        case 0: break;
                        case 1: value += left; break;
                        case 2: value += up; break;
                        case 3: value += (left + up) >> 1; break;
                        case 4: value += Paeth(left, up, upLeft); break;
                        default: throw new InvalidDataException($"알 수 없는 PNG 필터 {filter} (행 {y})");
                    }
                    result[dst + x] = (byte)value;
                }
            }
            return result;
        }

        static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            return pb <= pc ? b : c;
        }

        static int ReadInt32BE(byte[] d, int o) => (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];
    }
}
