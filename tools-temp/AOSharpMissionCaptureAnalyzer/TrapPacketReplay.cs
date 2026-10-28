namespace AOSharpMissionCaptureAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Web.Script.Serialization;

    using AORebirth.CaptureProtocol;

    /// <summary>Bounded offline trap projection; source bytes/context are never rewritten.</summary>
    internal static class TrapPacketReplay
    {
        internal static int Run(string captureRoot, string outputRoot)
        {
            captureRoot = Path.GetFullPath(captureRoot);
            outputRoot = Path.GetFullPath(outputRoot);
            if (!Directory.Exists(captureRoot))
                throw new DirectoryNotFoundException(captureRoot);
            string sourcePrefix = captureRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (outputRoot.Equals(captureRoot, StringComparison.OrdinalIgnoreCase)
                || outputRoot.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Derived trap output must be outside the original capture root.");

            var records = new List<object>();
            var sources = new List<object>();
            int selected = 0;
            int decoded = 0;
            int mines = 0;
            int traps = 0;
            int failures = 0;
            foreach (string source in Directory.GetFiles(captureRoot, "raw-packets.csv", SearchOption.AllDirectories).OrderBy(path => path))
            {
                string before = HashFile(source);
                int logicalRecord = 0;
                using (var reader = new StreamReader(source, Encoding.UTF8, true))
                {
                    string header = reader.ReadLine();
                    string[] columns = Program.ParseCsvLine(header ?? string.Empty);
                    int rawIndex = Array.IndexOf(columns, "RawHex");
                    if (rawIndex < 0)
                        throw new InvalidDataException("RawHex column is absent: " + source);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        logicalRecord++;
                        if (string.IsNullOrWhiteSpace(line))
                            continue;
                        string[] fields = Program.ParseCsvLine(line);
                        if (fields.Length <= rawIndex)
                            continue;
                        string raw = fields[rawIndex];
                        int key;
                        if (raw.Length < 40 || !int.TryParse(raw.Substring(32, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out key)
                            || (key != RawTrapFullUpdateDecoder.MineKey && key != RawTrapFullUpdateDecoder.TrapItemKey))
                            continue;
                        selected++;
                        if (key == RawTrapFullUpdateDecoder.MineKey)
                            mines++;
                        else
                            traps++;
                        var context = new Dictionary<string, string>();
                        for (int index = 0; index < columns.Length && index < fields.Length; index++)
                            context[columns[index]] = fields[index];
                        try
                        {
                            byte[] packet = Hex(raw);
                            RawTrapFullUpdate parsed = RawTrapFullUpdateDecoder.DecodePacket(packet);
                            records.Add(new
                            {
                                Capture = new DirectoryInfo(Path.GetDirectoryName(source)).Name,
                                SourceCsv = source,
                                SourceCsvSha256 = before,
                                SourceLogicalRecord = logicalRecord,
                                PacketSha256 = Hash(packet),
                                RawHex = raw,
                                Context = context,
                                DecodeStatus = "COMPLETE_BYTE_GRAMMAR",
                                NativeMineExtensionMeaning = key == RawTrapFullUpdateDecoder.MineKey ? "UNRECOVERED_NEUTRAL_VALUES_PRESERVED" : null,
                                Packet = parsed
                            });
                            decoded++;
                        }
                        catch (Exception exception)
                        {
                            failures++;
                            records.Add(new
                            {
                                SourceCsv = source,
                                SourceLogicalRecord = logicalRecord,
                                RawHex = raw,
                                Context = context,
                                DecodeStatus = "FAIL",
                                Error = exception.GetType().Name + ": " + exception.Message
                            });
                        }
                    }
                }
                string after = HashFile(source);
                if (!string.Equals(before, after, StringComparison.Ordinal))
                    throw new InvalidDataException("Original capture changed during replay: " + source);
                sources.Add(new { SourceCsv = source, Sha256Before = before, Sha256After = after, RawRows = logicalRecord });
            }

            Directory.CreateDirectory(outputRoot);
            var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 };
            string output = Path.Combine(outputRoot, "trap-packets.decoded.json");
            File.WriteAllText(output, json.Serialize(new
            {
                Schema = "aorebirth-original-trap-packet-replay/1",
                Status = failures == 0 ? "PASS_BYTE_GRAMMAR_WITH_UNRECOVERED_MINE_CONSUMER_SEMANTICS" : "FAIL",
                Selected = selected,
                MineRecords = mines,
                TrapItemRecords = traps,
                SuccessfullyDecoded = decoded,
                Failures = failures,
                Sources = sources,
                Records = records
            }), new UTF8Encoding(false));
            Console.WriteLine("Trap replay: selected=" + selected + " mines=" + mines + " trapItems=" + traps
                + " decoded=" + decoded + " failures=" + failures + " output=" + output);
            return failures == 0 ? 0 : 1;
        }

        private static byte[] Hex(string text)
        {
            if (text.Length % 2 != 0)
                throw new FormatException("Odd raw packet hex length.");
            var bytes = new byte[text.Length / 2];
            for (int index = 0; index < bytes.Length; index++)
                bytes[index] = byte.Parse(text.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return bytes;
        }

        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
