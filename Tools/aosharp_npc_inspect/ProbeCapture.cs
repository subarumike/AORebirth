using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace NpcInspectProbe
{
    // Independent, bounded raw-hook archive. No network or AOSharp dependencies.
    public sealed class ProbeCapture : IDisposable
    {
        public const int MaxPackets = 20000;
        public const long MaxBytes = 16 * 1024 * 1024;
        private readonly BinaryWriter raw;
        private readonly StreamWriter index;
        private bool disposed;
        public int Saved { get; private set; }
        public int Dropped { get; private set; }
        public long Bytes { get; private set; }
        public bool Truncated { get; private set; }

        public ProbeCapture(string prefix)
        {
            raw = new BinaryWriter(new FileStream(prefix + "-packets.bin", FileMode.CreateNew,
                FileAccess.Write, FileShare.Read), Encoding.UTF8);
            try
            {
                index = new StreamWriter(new FileStream(prefix + "-packets.log", FileMode.CreateNew,
                    FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
                raw.Write(Encoding.ASCII.GetBytes("NPIPCAP2"));
                Bytes = 8;
                index.WriteLine("NPIPCAP2: callback observation times/order, not wire timestamps. IN=0 OUT=1. Offsets refer to record starts.");
                Flush();
            }
            catch { try { if (index != null) index.Dispose(); } finally { raw.Dispose(); } throw; }
        }

        public bool Append(bool outgoing, byte[] packet, DateTime utc, double elapsedMs)
        {
            if (disposed) throw new ObjectDisposedException("ProbeCapture");
            // Record header: direction u8, UTC ticks i64, elapsed ms f64,
            // payload length i32; all numeric archive fields little endian.
            long recordBytes = 21L + (packet == null ? 0 : packet.Length);
            if (Truncated || packet == null || packet.Length > 1024 * 1024
                || Saved >= MaxPackets || recordBytes > MaxBytes - Bytes)
            {
                Dropped++;
                if (!Truncated)
                {
                    Truncated = true;
                    index.WriteLine("CAPTURE_INCOMPLETE: limit or invalid callback input; further raw packets omitted.");
                    Flush();
                }
                return false;
            }
            long offset = Bytes;
            raw.Write((byte)(outgoing ? 1 : 0));
            raw.Write(utc.Ticks);
            raw.Write(elapsedMs);
            raw.Write(packet.Length);
            raw.Write(packet);
            Bytes += recordBytes;
            Saved++;
            index.WriteLine(utc.ToString("O") + " elapsedMs=" + elapsedMs.ToString("F3", CultureInfo.InvariantCulture)
                + " direction=" + (outgoing ? "OUT" : "IN") + " record=" + Saved + " offset=" + offset
                + " bytes=" + packet.Length + " " + PacketView.Describe(packet));
            return true;
        }

        public void Flush() { raw.Flush(); index.Flush(); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { raw.Dispose(); } finally { index.Dispose(); }
        }
    }
}
