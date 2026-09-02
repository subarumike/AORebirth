namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.IO;
    using StbImageWriteSharp;

    internal static class HeightmapPngWriter
    {
        internal static void WriteGndaPng(string path, byte[] pixels, int width, int height)
        {
            if (pixels == null)
            {
                throw new ArgumentNullException("pixels");
            }

            if (pixels.Length != width * height)
            {
                throw new ArgumentException("GNDA height pixel buffer length does not match width and height.");
            }

            using (var stream = File.Create(path))
            {
                var writer = new ImageWriter();
                writer.WritePng(
                    pixels,
                    width,
                    height,
                    ColorComponents.Grey,
                    stream);
            }
        }

        internal static void WriteChgaPng(string path, ushort[] pixels, int width, int height)
        {
            if (pixels == null)
            {
                throw new ArgumentNullException("pixels");
            }

            if (pixels.Length != width * height)
            {
                throw new ArgumentException("CHGA height pixel buffer length does not match width and height.");
            }

            byte[] encoded = new byte[pixels.Length * 2];
            for (int index = 0; index < pixels.Length; index++)
            {
                ushort value = pixels[index];
                encoded[(index * 2)] = (byte)(value >> 8);
                encoded[(index * 2) + 1] = (byte)(value & 0xFF);
            }

            using (var stream = File.Create(path))
            {
                var writer = new ImageWriter();
                writer.WritePng(
                    encoded,
                    width,
                    height,
                    ColorComponents.GreyAlpha,
                    stream);
            }
        }
    }
}
