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
        private const int FollowTargetMessage = 0x260F3671;
        private const int SetPosMessage = 0x195E496E;
        private const int StopMovingCmdMessage = 0x742E2314;
        private const int CharDcMoveMessage = 0x54111123;

        private readonly object syncRoot = new object();
        private readonly Stopwatch captureClock = new Stopwatch();
        private string pluginDirectory = string.Empty;
        private string sessionDirectory = string.Empty;
        private StreamWriter packetLog;
        private StreamWriter rawPacketLog;
        private StreamWriter movementPacketLog;
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
        private int movementPacketRows;
        private int movementFollowTargetPackets;
        private int movementUsableFollowTargetPackets;
        private int movementSetPosPackets;
        private int movementStopMovingCmdPackets;
        private int movementCharDcMovePackets;
        private int movementDecodeErrors;

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
                                + " movement=" + this.movementPacketRows.ToString(CultureInfo.InvariantCulture)
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
            this.movementPacketLog = CreateWriter(Path.Combine(this.sessionDirectory, "movement-packets.csv"));
            this.eventLog = CreateWriter(Path.Combine(this.sessionDirectory, "events.log"));
            this.rawPacketLog.WriteLine(
                "CapturedUtc,ElapsedMilliseconds,Direction,GlobalOrdinal,Sequence,PacketLength,N3TypeValue,N3TypeName,IdentityType,IdentityInstance,PreservationStatus,RawHex");
            this.movementPacketLog.WriteLine(
                "CapturedUtc,Direction,Sequence,MessageType,SourceType,SourceInstance,SourceIdentity,SourceName,TargetType,TargetInstance,TargetIdentity,TargetName,FollowKind,CurrentX,CurrentY,CurrentZ,DestinationX,DestinationY,DestinationZ,Speed,Animation,Flags,PathCount,RawParams,RawTailHex");

            this.globalOrdinal = 0;
            this.inboundSequence = 0;
            this.outboundSequence = 0;
            this.rawWriteErrors = 0;
            this.movementPacketRows = 0;
            this.movementFollowTargetPackets = 0;
            this.movementUsableFollowTargetPackets = 0;
            this.movementSetPosPackets = 0;
            this.movementStopMovingCmdPackets = 0;
            this.movementCharDcMovePackets = 0;
            this.movementDecodeErrors = 0;
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
                            "{0:o} {1} #{2} len={3} n3={4} hex={5}",
                            capturedUtc,
                            direction,
                            sequence,
                            packetLength,
                            n3Type,
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

                try
                {
                    this.ExportMovementPacket(capturedUtc, direction, sequence, packet, n3Type);
                }
                catch (Exception ex)
                {
                    this.movementDecodeErrors++;
                    this.WriteFallbackError("Movement." + direction, ex);
                }
            }
        }

        private void ExportMovementPacket(
            DateTime capturedUtc,
            string direction,
            int sequence,
            byte[] packet,
            int messageType)
        {
            if (packet == null || packet.Length < 29)
            {
                return;
            }

            if (messageType == FollowTargetMessage)
            {
                this.ExportFollowTargetPacket(capturedUtc, direction, sequence, packet);
            }
            else if (messageType == SetPosMessage)
            {
                this.ExportSetPosPacket(capturedUtc, direction, sequence, packet);
            }
            else if (messageType == StopMovingCmdMessage)
            {
                this.ExportStopMovingCmdPacket(capturedUtc, direction, sequence, packet);
            }
            else if (messageType == CharDcMoveMessage)
            {
                this.ExportCharDcMovePacket(capturedUtc, direction, sequence, packet);
            }
        }

        private void ExportFollowTargetPacket(
            DateTime capturedUtc,
            string direction,
            int sequence,
            byte[] packet)
        {
            uint sourceType;
            uint sourceInstance;
            if (!TryReadIdentity(packet, 20, out sourceType, out sourceInstance))
            {
                return;
            }

            byte baseUnknown = packet[28];
            if (packet.Length < 31)
            {
                this.movementDecodeErrors++;
                this.WriteMovementPacketRow(
                    capturedUtc,
                    direction,
                    sequence,
                    "FollowTarget",
                    sourceType,
                    sourceInstance,
                    null,
                    null,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "base_unknown=" + baseUnknown.ToString(CultureInfo.InvariantCulture),
                    string.Empty,
                    "base_unknown=" + baseUnknown.ToString(CultureInfo.InvariantCulture) + ";follow_type=missing",
                    GetRawTailHex(packet, 29));
                return;
            }

            this.movementFollowTargetPackets++;
            byte followType = packet[29];
            byte followUnknown = packet[30];
            string flags = string.Format(
                CultureInfo.InvariantCulture,
                "base_unknown={0};follow_type={1};follow_unknown={2}",
                baseUnknown,
                followType,
                followUnknown);

            if (followType == 1)
            {
                this.ExportFollowTargetPathPacket(
                    capturedUtc,
                    direction,
                    sequence,
                    packet,
                    sourceType,
                    sourceInstance,
                    followUnknown,
                    flags);
                return;
            }

            if (followType == 2)
            {
                uint targetType;
                uint targetInstance;
                uint? nullableTargetType = null;
                uint? nullableTargetInstance = null;
                int tailOffset = 31;
                string rawParams = flags;
                if (TryReadIdentity(packet, 31, out targetType, out targetInstance))
                {
                    nullableTargetType = targetType;
                    nullableTargetInstance = targetInstance;
                    tailOffset = 39;
                    rawParams += ";target=" + FormatIdentity(targetType, targetInstance);
                }

                if (packet.Length >= 55)
                {
                    rawParams += string.Format(
                        CultureInfo.InvariantCulture,
                        ";target_unknown1={0};target_unknown2={1};target_unknown3={2};target_unknown4={3}",
                        ReadInt32BigEndian(packet, 39),
                        ReadInt32BigEndian(packet, 43),
                        ReadInt32BigEndian(packet, 47),
                        ReadInt32BigEndian(packet, 51));
                    tailOffset = 55;
                }

                this.WriteMovementPacketRow(
                    capturedUtc,
                    direction,
                    sequence,
                    "FollowTarget",
                    sourceType,
                    sourceInstance,
                    nullableTargetType,
                    nullableTargetInstance,
                    "Target",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    followUnknown.ToString(CultureInfo.InvariantCulture),
                    flags,
                    string.Empty,
                    rawParams,
                    GetRawTailHex(packet, tailOffset));
                return;
            }

            this.WriteMovementPacketRow(
                capturedUtc,
                direction,
                sequence,
                "FollowTarget",
                sourceType,
                sourceInstance,
                null,
                null,
                "Type" + followType.ToString(CultureInfo.InvariantCulture),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                followUnknown.ToString(CultureInfo.InvariantCulture),
                flags,
                string.Empty,
                flags,
                GetRawTailHex(packet, 31));
        }

        private void ExportFollowTargetPathPacket(
            DateTime capturedUtc,
            string direction,
            int sequence,
            byte[] packet,
            uint sourceType,
            uint sourceInstance,
            byte followUnknown,
            string flags)
        {
            if (packet.Length < 32)
            {
                this.movementDecodeErrors++;
                this.WriteMovementPacketRow(
                    capturedUtc,
                    direction,
                    sequence,
                    "FollowTarget",
                    sourceType,
                    sourceInstance,
                    null,
                    null,
                    "NpcPath",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    followUnknown.ToString(CultureInfo.InvariantCulture),
                    flags,
                    string.Empty,
                    flags + ";path_count=missing",
                    string.Empty);
                return;
            }

            int pathCount = packet[31];
            int coordinateOffset = 32;
            int availableCoordinates = Math.Max(0, (packet.Length - coordinateOffset) / 12);
            int decodedCoordinates = Math.Min(pathCount, availableCoordinates);
            int tailOffset = coordinateOffset + decodedCoordinates * 12;
            string currentX = string.Empty;
            string currentY = string.Empty;
            string currentZ = string.Empty;
            string destinationX = string.Empty;
            string destinationY = string.Empty;
            string destinationZ = string.Empty;

            if (decodedCoordinates > 0)
            {
                currentX = FormatFloat(ReadSingleBigEndian(packet, coordinateOffset));
                currentY = FormatFloat(ReadSingleBigEndian(packet, coordinateOffset + 4));
                currentZ = FormatFloat(ReadSingleBigEndian(packet, coordinateOffset + 8));
                int destinationOffset = coordinateOffset + (decodedCoordinates - 1) * 12;
                destinationX = FormatFloat(ReadSingleBigEndian(packet, destinationOffset));
                destinationY = FormatFloat(ReadSingleBigEndian(packet, destinationOffset + 4));
                destinationZ = FormatFloat(ReadSingleBigEndian(packet, destinationOffset + 8));
            }

            if (pathCount > 0 && decodedCoordinates == pathCount)
            {
                this.movementUsableFollowTargetPackets++;
            }
            else if (decodedCoordinates != pathCount)
            {
                this.movementDecodeErrors++;
            }

            string rawParams = string.Format(
                CultureInfo.InvariantCulture,
                "{0};path_count={1};decoded_path_count={2}",
                flags,
                pathCount,
                decodedCoordinates);
            if (decodedCoordinates != pathCount)
            {
                rawParams += ";truncated=true";
            }

            this.WriteMovementPacketRow(
                capturedUtc,
                direction,
                sequence,
                "FollowTarget",
                sourceType,
                sourceInstance,
                null,
                null,
                "NpcPath",
                currentX,
                currentY,
                currentZ,
                destinationX,
                destinationY,
                destinationZ,
                string.Empty,
                followUnknown.ToString(CultureInfo.InvariantCulture),
                flags,
                pathCount.ToString(CultureInfo.InvariantCulture),
                rawParams,
                GetRawTailHex(packet, tailOffset));
        }

        private void ExportSetPosPacket(
            DateTime capturedUtc,
            string direction,
            int sequence,
            byte[] packet)
        {
            if (packet.Length < 41)
            {
                this.movementDecodeErrors++;
                return;
            }

            uint sourceType;
            uint sourceInstance;
            if (!TryReadIdentity(packet, 20, out sourceType, out sourceInstance))
            {
                return;
            }

            this.movementSetPosPackets++;
            string flags = "base_unknown=" + packet[28].ToString(CultureInfo.InvariantCulture);
            this.WriteMovementPacketRow(
                capturedUtc,
                direction,
                sequence,
                "SetPos",
                sourceType,
                sourceInstance,
                null,
                null,
                string.Empty,
                FormatFloat(ReadSingleBigEndian(packet, 29)),
                FormatFloat(ReadSingleBigEndian(packet, 33)),
                FormatFloat(ReadSingleBigEndian(packet, 37)),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                flags,
                string.Empty,
                flags,
                GetRawTailHex(packet, 41));
        }

        private void ExportStopMovingCmdPacket(
            DateTime capturedUtc,
            string direction,
            int sequence,
            byte[] packet)
        {
            if (packet.Length < 41)
            {
                this.movementDecodeErrors++;
                return;
            }

            uint sourceType;
            uint sourceInstance;
            if (!TryReadIdentity(packet, 20, out sourceType, out sourceInstance))
            {
                return;
            }

            this.movementStopMovingCmdPackets++;
            string flags = "base_unknown=" + packet[28].ToString(CultureInfo.InvariantCulture);
            string rawParams = string.Format(
                CultureInfo.InvariantCulture,
                "{0};unknown1={1};unknown2={2};unknown3={3}",
                flags,
                ReadInt32BigEndian(packet, 29),
                ReadInt32BigEndian(packet, 33),
                ReadInt32BigEndian(packet, 37));
            this.WriteMovementPacketRow(
                capturedUtc,
                direction,
                sequence,
                "StopMovingCmd",
                sourceType,
                sourceInstance,
                null,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                flags,
                string.Empty,
                rawParams,
                GetRawTailHex(packet, 41));
        }

        private void ExportCharDcMovePacket(
            DateTime capturedUtc,
            string direction,
            int sequence,
            byte[] packet)
        {
            if (packet.Length < 70)
            {
                this.movementDecodeErrors++;
                return;
            }

            uint sourceType;
            uint sourceInstance;
            if (!TryReadIdentity(packet, 20, out sourceType, out sourceInstance))
            {
                return;
            }

            this.movementCharDcMovePackets++;
            byte baseUnknown = packet[28];
            byte moveType = packet[29];
            string flags = string.Format(
                CultureInfo.InvariantCulture,
                "base_unknown={0};move_type={1}",
                baseUnknown,
                moveType);
            string rawParams = string.Format(
                CultureInfo.InvariantCulture,
                "base_unknown={0};move_type={1};heading={2},{3},{4},{5};unknown1={6};aux_a={7};aux_b={8}",
                baseUnknown,
                moveType,
                FormatFloat(ReadSingleBigEndian(packet, 30)),
                FormatFloat(ReadSingleBigEndian(packet, 34)),
                FormatFloat(ReadSingleBigEndian(packet, 38)),
                FormatFloat(ReadSingleBigEndian(packet, 42)),
                ReadInt32BigEndian(packet, 58),
                FormatFloat(ReadSingleBigEndian(packet, 62)),
                FormatFloat(ReadSingleBigEndian(packet, 66)));
            this.WriteMovementPacketRow(
                capturedUtc,
                direction,
                sequence,
                "CharDCMove",
                sourceType,
                sourceInstance,
                null,
                null,
                "CharacterMove",
                FormatFloat(ReadSingleBigEndian(packet, 46)),
                FormatFloat(ReadSingleBigEndian(packet, 50)),
                FormatFloat(ReadSingleBigEndian(packet, 54)),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                flags,
                string.Empty,
                rawParams,
                GetRawTailHex(packet, 70));
        }

        private void WriteMovementPacketRow(
            DateTime capturedUtc,
            string direction,
            int sequence,
            string messageType,
            uint sourceType,
            uint sourceInstance,
            uint? targetType,
            uint? targetInstance,
            string followKind,
            string currentX,
            string currentY,
            string currentZ,
            string destinationX,
            string destinationY,
            string destinationZ,
            string speed,
            string animation,
            string flags,
            string pathCount,
            string rawParams,
            string rawTailHex)
        {
            this.movementPacketRows++;
            this.movementPacketLog.WriteLine(
                string.Join(
                    ",",
                    Csv(capturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                    Csv(direction),
                    sequence.ToString(CultureInfo.InvariantCulture),
                    Csv(messageType),
                    Csv(FormatIdentityType(sourceType)),
                    Csv(FormatInstance(sourceInstance)),
                    Csv(FormatIdentity(sourceType, sourceInstance)),
                    Csv(string.Empty),
                    Csv(targetType.HasValue ? FormatIdentityType(targetType.Value) : string.Empty),
                    Csv(targetInstance.HasValue ? FormatInstance(targetInstance.Value) : string.Empty),
                    Csv(targetType.HasValue && targetInstance.HasValue ? FormatIdentity(targetType.Value, targetInstance.Value) : string.Empty),
                    Csv(string.Empty),
                    Csv(followKind),
                    Csv(currentX),
                    Csv(currentY),
                    Csv(currentZ),
                    Csv(destinationX),
                    Csv(destinationY),
                    Csv(destinationZ),
                    Csv(speed),
                    Csv(animation),
                    Csv(flags),
                    Csv(pathCount),
                    Csv(rawParams),
                    Csv(rawTailHex)));
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
            if (!this.enabled
                && this.packetLog == null
                && this.rawPacketLog == null
                && this.movementPacketLog == null
                && this.eventLog == null)
            {
                return;
            }

            this.enabled = false;
            this.stopRequested = false;
            this.captureClock.Stop();
            this.WriteEvent("FINALIZE", reason);
            this.FlushNoThrow();
            this.WriteMovementSummaryNoThrow();
            this.WriteCaptureInfo(true, reason);
            this.packetLog = CloseWriter(this.packetLog);
            this.rawPacketLog = CloseWriter(this.rawPacketLog);
            this.movementPacketLog = CloseWriter(this.movementPacketLog);
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
            json.AppendLine("  \"movementProjection\": {");
            json.AppendLine("    \"rows\": " + this.movementPacketRows.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"followTargetPackets\": " + this.movementFollowTargetPackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"usableFollowTargetPackets\": " + this.movementUsableFollowTargetPackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"setPosPackets\": " + this.movementSetPosPackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"stopMovingCmdPackets\": " + this.movementStopMovingCmdPackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"charDCMovePackets\": " + this.movementCharDcMovePackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"decodeErrors\": " + this.movementDecodeErrors.ToString(CultureInfo.InvariantCulture));
            json.AppendLine("  },");
            json.AppendLine("  \"processingAllowed\": true,");
            json.AppendLine("  \"offlineDecodeRequired\": true,");
            json.AppendLine("  \"recaptureRequired\": " + (this.rawWriteErrors > 0 ? "true" : "false") + ",");
            json.AppendLine("  \"finalized\": " + (finalized ? "true" : "false") + ",");
            json.AppendLine("  \"detail\": " + Json(reason));
            json.AppendLine("}");
            File.WriteAllText(path, json.ToString(), new UTF8Encoding(false));
        }

        private void WriteMovementSummaryNoThrow()
        {
            if (string.IsNullOrWhiteSpace(this.sessionDirectory))
            {
                return;
            }

            try
            {
                string path = Path.Combine(this.sessionDirectory, "movement-summary.json");
                StringBuilder json = new StringBuilder();
                json.AppendLine("{");
                json.AppendLine("  \"generatedUtc\": \"" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\",");
                json.AppendLine("  \"captureFolderPath\": " + Json(this.sessionDirectory) + ",");
                json.AppendLine("  \"movementPacketsCsv\": " + Json(Path.Combine(this.sessionDirectory, "movement-packets.csv")) + ",");
                json.AppendLine("  \"followTargetDecodedWithUsablePath\": " + (this.movementUsableFollowTargetPackets > 0 ? "true" : "false") + ",");
                json.AppendLine("  \"counts\": {");
                json.AppendLine("    \"movementPacketRows\": " + this.movementPacketRows.ToString(CultureInfo.InvariantCulture) + ",");
                json.AppendLine("    \"followTargetPackets\": " + this.movementFollowTargetPackets.ToString(CultureInfo.InvariantCulture) + ",");
                json.AppendLine("    \"usableFollowTargetPackets\": " + this.movementUsableFollowTargetPackets.ToString(CultureInfo.InvariantCulture) + ",");
                json.AppendLine("    \"setPosPackets\": " + this.movementSetPosPackets.ToString(CultureInfo.InvariantCulture) + ",");
                json.AppendLine("    \"stopMovingCmdPackets\": " + this.movementStopMovingCmdPackets.ToString(CultureInfo.InvariantCulture) + ",");
                json.AppendLine("    \"charDCMovePackets\": " + this.movementCharDcMovePackets.ToString(CultureInfo.InvariantCulture) + ",");
                json.AppendLine("    \"decodeErrors\": " + this.movementDecodeErrors.ToString(CultureInfo.InvariantCulture));
                json.AppendLine("  }");
                json.AppendLine("}");
                File.WriteAllText(path, json.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                this.WriteFallbackError("MovementSummary", ex);
            }
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
            TryFlush(this.movementPacketLog);
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

        private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24)
                   | ((uint)bytes[offset + 1] << 16)
                   | ((uint)bytes[offset + 2] << 8)
                   | bytes[offset + 3];
        }

        private static float ReadSingleBigEndian(byte[] bytes, int offset)
        {
            byte[] value = new byte[4];
            value[0] = bytes[offset + 3];
            value[1] = bytes[offset + 2];
            value[2] = bytes[offset + 1];
            value[3] = bytes[offset];
            return BitConverter.ToSingle(value, 0);
        }

        private static bool TryReadIdentity(
            byte[] bytes,
            int offset,
            out uint identityType,
            out uint identityInstance)
        {
            identityType = 0;
            identityInstance = 0;
            if (bytes == null || offset < 0 || bytes.Length < offset + 8)
            {
                return false;
            }

            identityType = ReadUInt32BigEndian(bytes, offset);
            identityInstance = ReadUInt32BigEndian(bytes, offset + 4);
            return true;
        }

        private static string FormatIdentity(uint identityType, uint identityInstance)
        {
            return FormatIdentityType(identityType) + ":" + FormatInstance(identityInstance);
        }

        private static string FormatIdentityType(uint identityType)
        {
            return identityType == 50000
                       ? "SimpleChar"
                       : identityType.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatInstance(uint identityInstance)
        {
            return identityInstance.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("G9", CultureInfo.InvariantCulture);
        }

        private static string GetRawTailHex(byte[] bytes, int offset)
        {
            if (bytes == null || offset >= bytes.Length)
            {
                return string.Empty;
            }

            if (offset < 0)
            {
                offset = 0;
            }

            byte[] tail = new byte[bytes.Length - offset];
            Buffer.BlockCopy(bytes, offset, tail, 0, tail.Length);
            return ToHex(tail);
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
