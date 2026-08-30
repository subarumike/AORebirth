using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.UI;

namespace AOSharpLiveCapture.Zam2022
{
    public sealed class Main : AOPluginEntry
    {
        private const int StopQuietSeconds = 2;
        private const int StopMaximumSeconds = 5;

        private readonly object syncRoot = new object();
        private readonly Stopwatch captureClock = new Stopwatch();
        private string pluginDirectory = string.Empty;
        private string sessionDirectory = string.Empty;
        private StreamWriter packetLog;
        private StreamWriter rawPacketLog;
        private StreamWriter eventLog;
        private bool enabled;
        private bool stopRequested;
        private DateTime captureStartUtc;
        private DateTime stopRequestedUtc;
        private DateTime lastPacketUtc;
        private long globalOrdinal;
        private int inboundSequence;
        private int outboundSequence;
        private int rawWriteErrors;

        public override void Run(string pluginDir)
        {
            this.pluginDirectory = pluginDir ?? string.Empty;
            try
            {
                Network.PacketReceived += this.OnPacketReceived;
                Network.PacketSent += this.OnPacketSent;
                Game.OnUpdate += this.OnUpdate;
                Chat.RegisterCommand("aocap", this.OnCommand);
                Chat.WriteLine(
                    "AOSharpLiveCapture Zam 2022 ready. Use /aocap start and /aocap stop.",
                    ChatColor.Gold);
            }
            catch (Exception ex)
            {
                this.WriteFallbackError("Run", ex);
                this.UnsubscribeNoThrow();
            }
        }

        public override void Teardown()
        {
            this.UnsubscribeNoThrow();
            lock (this.syncRoot)
            {
                this.FinalizeSessionNoThrow("plugin teardown");
            }

        }

        private void OnCommand(string command, string[] args, ChatWindow chatWindow)
        {
            string action = args == null || args.Length == 0
                                ? "status"
                                : args[0].ToLowerInvariant();
            try
            {
                switch (action)
                {
                    case "start":
                        lock (this.syncRoot)
                        {
                            this.StartSession();
                        }

                        chatWindow.WriteLine("AO capture started: " + this.sessionDirectory, ChatColor.Gold);
                        break;

                    case "stop":
                        lock (this.syncRoot)
                        {
                            if (!this.enabled)
                            {
                                chatWindow.WriteLine("AO capture is not running.", ChatColor.Gold);
                                return;
                            }

                            this.stopRequested = true;
                            this.stopRequestedUtc = DateTime.UtcNow;
                            this.WriteEvent("STOP-REQUEST", "Finalizing after packet drain.");
                        }

                        chatWindow.WriteLine(
                            "AO capture stop requested; finalizing after the packet drain.",
                            ChatColor.Gold);
                        break;

                    case "flush":
                        lock (this.syncRoot)
                        {
                            this.FlushNoThrow();
                        }

                        chatWindow.WriteLine("AO capture flushed.", ChatColor.Gold);
                        break;

                    case "mark":
                        lock (this.syncRoot)
                        {
                            this.WriteEvent("MARK", JoinArguments(args, 1));
                        }

                        chatWindow.WriteLine("AO capture marker written.", ChatColor.Gold);
                        break;

                    default:
                        lock (this.syncRoot)
                        {
                            string state = this.enabled ? (this.stopRequested ? "stopping" : "running") : "idle";
                            chatWindow.WriteLine(
                                "AO capture " + state
                                + ". in=" + this.inboundSequence.ToString(CultureInfo.InvariantCulture)
                                + " out=" + this.outboundSequence.ToString(CultureInfo.InvariantCulture)
                                + (string.IsNullOrWhiteSpace(this.sessionDirectory) ? string.Empty : " folder=" + this.sessionDirectory),
                                ChatColor.Gold);
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                this.WriteFallbackError("Command." + action, ex);
                chatWindow.WriteLine("AO capture command failed: " + ex.Message, ChatColor.Red);
            }
        }

        private void StartSession()
        {
            if (this.enabled)
            {
                this.FinalizeSessionNoThrow("restarted by command");
            }

            DateTime localNow = DateTime.Now;
            string areaName = GetAreaNameNoThrow();
            int playfieldId = GetPlayfieldIdNoThrow();
            string captureId = CaptureSessionLayout.CreateCaptureId(localNow);
            this.sessionDirectory = CaptureSessionLayout.CreateSessionDirectory(
                this.pluginDirectory,
                areaName,
                playfieldId,
                captureId,
                "Zam 2022");

            this.packetLog = CreateWriter(Path.Combine(this.sessionDirectory, "packets.hex.log"));
            this.rawPacketLog = CreateWriter(Path.Combine(this.sessionDirectory, "raw-packets.csv"));
            this.eventLog = CreateWriter(Path.Combine(this.sessionDirectory, "events.log"));
            this.rawPacketLog.WriteLine(
                "CapturedUtc,ElapsedMilliseconds,Direction,GlobalOrdinal,Sequence,PacketLength,N3TypeValue,N3TypeName,IdentityType,IdentityInstance,PreservationStatus,RawHex");

            this.globalOrdinal = 0;
            this.inboundSequence = 0;
            this.outboundSequence = 0;
            this.rawWriteErrors = 0;
            this.stopRequested = false;
            this.captureStartUtc = DateTime.UtcNow;
            this.lastPacketUtc = this.captureStartUtc;
            this.captureClock.Restart();
            this.enabled = true;
            this.WriteEvent("START", "AOSharp Zam 2022 compatibility capture started.");
            this.WriteCaptureInfo(false, string.Empty);
        }

        private void OnPacketReceived(object sender, byte[] packet)
        {
            this.CapturePacket("IN", packet, true);
        }

        private void OnPacketSent(object sender, byte[] packet)
        {
            this.CapturePacket("OUT", packet, false);
        }

        private void CapturePacket(string direction, byte[] packet, bool inbound)
        {
            lock (this.syncRoot)
            {
                if (!this.enabled)
                {
                    return;
                }

                DateTime capturedUtc = DateTime.UtcNow;
                this.lastPacketUtc = capturedUtc;
                int sequence = inbound ? ++this.inboundSequence : ++this.outboundSequence;
                long ordinal = ++this.globalOrdinal;
                int packetLength = packet == null ? 0 : packet.Length;
                int n3Type = packetLength >= 20 ? ReadInt32BigEndian(packet, 16) : 0;
                int identityType = packetLength >= 28 ? ReadInt32BigEndian(packet, 20) : 0;
                int identityInstance = packetLength >= 28 ? ReadInt32BigEndian(packet, 24) : 0;
                string rawHex = ToHex(packet);

                try
                {
                    this.packetLog.WriteLine(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0:o} {1} #{2} len={3} n3Type={4} identity={5}:{6} hex={7}",
                            capturedUtc,
                            direction,
                            sequence,
                            packetLength,
                            n3Type,
                            identityType,
                            identityInstance,
                            rawHex));
                    this.rawPacketLog.WriteLine(
                        string.Join(
                            ",",
                            Csv(capturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                            this.captureClock.Elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
                            Csv(direction),
                            ordinal.ToString(CultureInfo.InvariantCulture),
                            sequence.ToString(CultureInfo.InvariantCulture),
                            packetLength.ToString(CultureInfo.InvariantCulture),
                            n3Type.ToString(CultureInfo.InvariantCulture),
                            Csv(string.Empty),
                            identityType.ToString(CultureInfo.InvariantCulture),
                            identityInstance.ToString(CultureInfo.InvariantCulture),
                            Csv(packet == null ? "raw_missing" : "raw_complete"),
                            Csv(rawHex)));
                }
                catch (Exception ex)
                {
                    this.rawWriteErrors++;
                    this.WriteFallbackError("Packet." + direction, ex);
                }
            }
        }

        private void OnUpdate(object sender, float deltaTime)
        {
            bool finalized = false;
            string finalizedDirectory = string.Empty;
            lock (this.syncRoot)
            {
                if (!this.enabled || !this.stopRequested)
                {
                    return;
                }

                DateTime now = DateTime.UtcNow;
                bool quiet = (now - this.lastPacketUtc).TotalSeconds >= StopQuietSeconds;
                bool maximum = (now - this.stopRequestedUtc).TotalSeconds >= StopMaximumSeconds;
                if (quiet || maximum)
                {
                    finalizedDirectory = this.sessionDirectory;
                    this.FinalizeSessionNoThrow(quiet ? "quiet packet drain complete" : "maximum packet drain elapsed");
                    finalized = true;
                }
            }

            if (finalized)
            {
                Chat.WriteLine("AO capture finalized: " + finalizedDirectory, ChatColor.Gold);
            }
        }

        private void FinalizeSessionNoThrow(string reason)
        {
            if (!this.enabled && this.packetLog == null && this.rawPacketLog == null && this.eventLog == null)
            {
                return;
            }

            this.enabled = false;
            this.stopRequested = false;
            this.captureClock.Stop();
            this.WriteEvent("FINALIZE", reason);
            this.FlushNoThrow();
            this.WriteCaptureInfo(true, reason);
            this.packetLog = CloseWriter(this.packetLog);
            this.rawPacketLog = CloseWriter(this.rawPacketLog);
            this.eventLog = CloseWriter(this.eventLog);
        }

        private void WriteCaptureInfo(bool finalized, string reason)
        {
            if (string.IsNullOrWhiteSpace(this.sessionDirectory))
            {
                return;
            }

            string path = Path.Combine(this.sessionDirectory, "capture_info.json");
            StringBuilder json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"captureMode\": \"aosharp-zam-2022-raw-compatible\",");
            json.AppendLine("  \"captureStartUtc\": \"" + this.captureStartUtc.ToString("o", CultureInfo.InvariantCulture) + "\",");
            if (finalized)
            {
                json.AppendLine("  \"captureFinalizedUtc\": \"" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\",");
            }

            json.AppendLine("  \"inboundRaw\": " + this.inboundSequence.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("  \"outboundRaw\": " + this.outboundSequence.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("  \"rawPacketWriteErrors\": " + this.rawWriteErrors.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("  \"processingAllowed\": true,");
            json.AppendLine("  \"offlineDecodeRequired\": true,");
            json.AppendLine("  \"recaptureRequired\": " + (this.rawWriteErrors > 0 ? "true" : "false") + ",");
            json.AppendLine("  \"finalized\": " + (finalized ? "true" : "false") + ",");
            json.AppendLine("  \"detail\": " + Json(reason));
            json.AppendLine("}");
            File.WriteAllText(path, json.ToString(), new UTF8Encoding(false));
        }

        private void WriteEvent(string category, string detail)
        {
            if (this.eventLog == null)
            {
                return;
            }

            this.eventLog.WriteLine(
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                + " [" + category + "] " + OneLine(detail));
        }

        private void FlushNoThrow()
        {
            TryFlush(this.packetLog);
            TryFlush(this.rawPacketLog);
            TryFlush(this.eventLog);
        }

        private void UnsubscribeNoThrow()
        {
            try { Network.PacketReceived -= this.OnPacketReceived; } catch { }
            try { Network.PacketSent -= this.OnPacketSent; } catch { }
            try { Game.OnUpdate -= this.OnUpdate; } catch { }
        }

        private void WriteFallbackError(string boundary, Exception ex)
        {
            try
            {
                string root = string.IsNullOrWhiteSpace(this.pluginDirectory)
                                  ? AppDomain.CurrentDomain.BaseDirectory
                                  : this.pluginDirectory;
                File.AppendAllText(
                    Path.Combine(root, "capture-callback-errors.log"),
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                    + " boundary=" + boundary
                    + " error=" + ex.GetType().FullName
                    + ": " + OneLine(ex.Message)
                    + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private static string GetAreaNameNoThrow()
        {
            try { return Playfield.Name; } catch { return string.Empty; }
        }

        private static int GetPlayfieldIdNoThrow()
        {
            try { return Playfield.ModelIdentity.Instance; } catch { return 0; }
        }

        private static StreamWriter CreateWriter(string path)
        {
            return new StreamWriter(
                new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
                new UTF8Encoding(false))
            {
                AutoFlush = true
            };
        }

        private static StreamWriter CloseWriter(StreamWriter writer)
        {
            if (writer == null)
            {
                return null;
            }

            try { writer.Flush(); } catch { }
            try { writer.Dispose(); } catch { }
            return null;
        }

        private static void TryFlush(StreamWriter writer)
        {
            if (writer == null)
            {
                return;
            }

            try { writer.Flush(); } catch { }
        }

        private static string JoinArguments(string[] args, int start)
        {
            return args == null || args.Length <= start
                       ? string.Empty
                       : string.Join(" ", args, start, args.Length - start);
        }

        private static int ReadInt32BigEndian(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24)
                   | (bytes[offset + 1] << 16)
                   | (bytes[offset + 2] << 8)
                   | bytes[offset + 3];
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder result = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                result.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private static string Json(string value)
        {
            return "\"" + OneLine(value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string OneLine(string value)
        {
            return (value ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
