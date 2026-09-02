namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Text;

    internal static class EmbeddedPngHeightmapDecoder
    {
        private static readonly byte[] GndaMagic = Encoding.ASCII.GetBytes("GNDA");

        internal sealed class DecodedPng
        {
            internal int Width { get; set; }

            internal int Height { get; set; }

            internal byte[] Pixels { get; set; }

            internal int EndOffset { get; set; }
        }

        internal static bool TryDecodeGndaHeightmap(
            byte[] rawRecord,
            out DecodedPng heightmap,
            out int width,
            out int height,
            out float tileSize,
            out float heightScale)
        {
            heightmap = null;
            width = 0;
            height = 0;
            tileSize = 0f;
            heightScale = 0f;

            byte[] payload;
            if (!TilemapPayloadLocator.TryGetGndaPayload(rawRecord, out payload))
            {
                return false;
            }

            return TryDecodeGndaPayload(
                payload,
                out heightmap,
                out width,
                out height,
                out tileSize,
                out heightScale);
        }

        internal static bool TryDecodeGndaPayload(
            byte[] payload,
            out DecodedPng heightmap,
            out int width,
            out int height,
            out float tileSize,
            out float heightScale)
        {
            heightmap = null;
            width = 0;
            height = 0;
            tileSize = 0f;
            heightScale = 0f;

            if (payload == null
                || payload.Length < 24
                || !StartsWith(payload, GndaMagic))
            {
                return false;
            }

            width = ReadUInt16(payload, 12);
            height = ReadUInt16(payload, 14);
            tileSize = ReadSingle(payload, 16);
            heightScale = ReadSingle(payload, 20);

            List<DecodedPng> pngs = FindPngs(payload);
            if (pngs.Count < 2)
            {
                return false;
            }

            DecodedPng candidate = pngs[1];
            if (candidate.Width != width
                || candidate.Height != height
                || candidate.Pixels == null
                || candidate.Pixels.Length != width * height)
            {
                return false;
            }

            heightmap = candidate;
            return true;
        }

        private static List<DecodedPng> FindPngs(byte[] payload)
        {
            List<DecodedPng> result = new List<DecodedPng>();
            byte[] signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            int position = IndexOf(payload, signature, 0);
            while (position >= 0)
            {
                DecodedPng png = DecodePng(payload, position);
                result.Add(png);
                position = IndexOf(payload, signature, png.EndOffset);
            }

            return result;
        }

        private static DecodedPng DecodePng(byte[] payload, int position)
        {
            int cursor = position + 8;
            List<byte> compressed = new List<byte>();
            int width = 0;
            int height = 0;
            int bitDepth = 0;
            int colorType = 0;
            int end = position + 8;

            while (cursor + 12 <= payload.Length)
            {
                int length = ReadInt32BigEndian(payload, cursor);
                byte chunkType0 = payload[cursor + 4];
                byte chunkType1 = payload[cursor + 5];
                byte chunkType2 = payload[cursor + 6];
                byte chunkType3 = payload[cursor + 7];
                int chunkStart = cursor + 8;
                cursor += 12 + length;

                if (chunkType0 == (byte)'I'
                    && chunkType1 == (byte)'H'
                    && chunkType2 == (byte)'D'
                    && chunkType3 == (byte)'R')
                {
                    width = ReadInt32BigEndian(payload, chunkStart);
                    height = ReadInt32BigEndian(payload, chunkStart + 4);
                    bitDepth = payload[chunkStart + 8];
                    colorType = payload[chunkStart + 9];
                }
                else if (chunkType0 == (byte)'I'
                         && chunkType1 == (byte)'D'
                         && chunkType2 == (byte)'A'
                         && chunkType3 == (byte)'T')
                {
                    for (int index = 0; index < length; index++)
                    {
                        compressed.Add(payload[chunkStart + index]);
                    }
                }
                else if (chunkType0 == (byte)'I'
                         && chunkType1 == (byte)'E'
                         && chunkType2 == (byte)'N'
                         && chunkType3 == (byte)'D')
                {
                    end = cursor;
                    break;
                }
            }

            int channels = GetChannels(colorType);
            int bytesPerPixel = Math.Max(1, channels * bitDepth / 8);
            int stride = (width * channels * bitDepth + 7) / 8;
            byte[] filtered = Inflate(compressed.ToArray());
            byte[] pixels = new byte[height * stride];
            int source = 0;

            for (int row = 0; row < height; row++)
            {
                int filterType = filtered[source];
                source++;
                int rowStart = row * stride;
                int previousStart = rowStart - stride;
                for (int column = 0; column < stride; column++)
                {
                    int raw = filtered[source];
                    source++;
                    int left = column >= bytesPerPixel ? pixels[rowStart + column - bytesPerPixel] : 0;
                    int up = row > 0 ? pixels[previousStart + column] : 0;
                    int upperLeft = row > 0 && column >= bytesPerPixel
                        ? pixels[previousStart + column - bytesPerPixel]
                        : 0;

                    if (filterType == 1)
                    {
                        raw += left;
                    }
                    else if (filterType == 2)
                    {
                        raw += up;
                    }
                    else if (filterType == 3)
                    {
                        raw += (left + up) / 2;
                    }
                    else if (filterType == 4)
                    {
                        raw += Paeth(left, up, upperLeft);
                    }
                    else if (filterType != 0)
                    {
                        throw new InvalidDataException("Unsupported PNG filter type: " + filterType);
                    }

                    pixels[rowStart + column] = (byte)(raw & 0xFF);
                }
            }

            return new DecodedPng
            {
                Width = width,
                Height = height,
                Pixels = pixels,
                EndOffset = end,
            };
        }

        private static int GetChannels(int colorType)
        {
            switch (colorType)
            {
                case 0:
                    return 1;
                case 2:
                    return 3;
                case 3:
                    return 1;
                case 4:
                    return 2;
                case 6:
                    return 4;
                default:
                    throw new InvalidDataException("Unsupported PNG color type: " + colorType);
            }
        }

        private static int Paeth(int left, int up, int upperLeft)
        {
            int value = left + up - upperLeft;
            int leftDistance = Math.Abs(value - left);
            int upDistance = Math.Abs(value - up);
            int upperLeftDistance = Math.Abs(value - upperLeft);
            if (leftDistance <= upDistance && leftDistance <= upperLeftDistance)
            {
                return left;
            }

            if (upDistance <= upperLeftDistance)
            {
                return up;
            }

            return upperLeft;
        }

        private static byte[] Inflate(byte[] compressed)
        {
            using (var input = new MemoryStream(compressed))
            using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                zlib.CopyTo(output);
                return output.ToArray();
            }
        }

        private static bool StartsWith(byte[] payload, byte[] prefix)
        {
            if (payload.Length < prefix.Length)
            {
                return false;
            }

            for (int index = 0; index < prefix.Length; index++)
            {
                if (payload[index] != prefix[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static int IndexOf(byte[] payload, byte[] pattern, int start)
        {
            for (int index = start; index <= payload.Length - pattern.Length; index++)
            {
                bool matched = true;
                for (int patternIndex = 0; patternIndex < pattern.Length; patternIndex++)
                {
                    if (payload[index + patternIndex] != pattern[patternIndex])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int ReadUInt16(byte[] payload, int offset)
        {
            return payload[offset] | (payload[offset + 1] << 8);
        }

        private static float ReadSingle(byte[] payload, int offset)
        {
            byte[] buffer = new byte[4];
            Buffer.BlockCopy(payload, offset, buffer, 0, 4);
            return BitConverter.ToSingle(buffer, 0);
        }

        private static int ReadInt32BigEndian(byte[] payload, int offset)
        {
            return (payload[offset] << 24)
                   | (payload[offset + 1] << 16)
                   | (payload[offset + 2] << 8)
                   | payload[offset + 3];
        }
    }
}