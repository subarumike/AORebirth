using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.UI;
using AORebirth.CaptureProtocol;
using AOBackpack = AOSharp.Core.Inventory.Backpack;
using AOContainer = AOSharp.Core.Inventory.Container;
using AOInventory = AOSharp.Core.Inventory.Inventory;
using AOItem = AOSharp.Core.Inventory.Item;

namespace AOSharpLiveCapture.Mike2022
{
    public sealed class Main : AOPluginEntry
    {
        private const int StopQuietSeconds = 2;
        private const int StopMaximumSeconds = 5;
        private const int FollowTargetMessage = 0x260F3671;
        private const int SetPosMessage = 0x195E496E;
        private const int StopMovingCmdMessage = 0x742E2314;
        private const int CharDcMoveMessage = 0x54111123;
        private const int AttackMessage = 0x28494070;
        private const int AttackInfoMessage = 0x46002F16;
        private const int MissedAttackInfoMessage = 0x5C654B28;
        private const int CharacterActionMessage = 0x5E477770;
        private const int GenericCmdMessage = 0x52526858;
        private const int CorpseFullUpdateMessage = 0x4F474E05;
        private const int InventoryUpdateMessage = 0x4E536976;
        private const int DespawnMessage = 0x36510078;
        private const int GenericCmdUseAction = 3;
        private const int DeleteItemAction = 112;
        private const int ItemUseCorrelationSeconds = 30;
        private const int CheckpointSeconds = 2;
        private const int VisibilitySampleMilliseconds = 500;
        private const double AggroPositionWindowSeconds = 2.0;

        private sealed class EntitySnapshot
        {
            internal DateTime CapturedUtc;
            internal string Identity;
            internal string Name;
            internal int PlayfieldId;
            internal float X;
            internal float Y;
            internal float Z;
            internal float HeadingX;
            internal float HeadingY;
            internal float HeadingZ;
            internal float HeadingW;
            internal int Level;
            internal int Health;
            internal int HealthDamage;
            internal uint MonsterData;
            internal string Kind;
            internal bool HasPosition;
            internal bool HasLineOfSight;
            internal bool IsInLineOfSight;
            internal bool HasInPlay;
            internal bool IsInPlay;
            internal bool IsNpc;
            internal bool IsPlayer;
        }

        private sealed class CorpseSnapshot
        {
            internal DateTime CapturedUtc;
            internal uint CorpseType;
            internal uint CorpseInstance;
            internal string CorpseIdentity;
            internal string Name;
            internal int PlayfieldId;
            internal float X;
            internal float Y;
            internal float Z;
            internal string DeadNpcIdentity;
            internal int Credits;
            internal uint MonsterData;
        }

        private sealed class PositionObservation
        {
            internal DateTime CapturedUtc;
            internal string MessageType;
            internal string Direction;
            internal int Sequence;
            internal float X;
            internal float Y;
            internal float Z;
        }

        private sealed class PositionHistory
        {
            internal PositionObservation Previous;
            internal PositionObservation Latest;
        }

        private sealed class PendingAttack
        {
            internal DateTime CapturedUtc;
            internal double ElapsedMilliseconds;
            internal long GlobalOrdinal;
            internal string Direction;
            internal int Sequence;
            internal string SourceIdentity;
            internal string TargetIdentity;
            internal bool PlayerPreviouslyAttackedSource;
        }

        private sealed class ItemSnapshot
        {
            internal DateTime CapturedUtc;
            internal string ContainerKind;
            internal string ContainerIdentity;
            internal string ContainerName;
            internal string ContainerSlotIdentity;
            internal string SlotIdentity;
            internal string UniqueIdentity;
            internal int LowId;
            internal int HighId;
            internal int QualityLevel;
            internal int Charges;
            internal string Name;
            internal string ResolutionStatus;
            internal string Error;
        }

        private sealed class PendingItemUse
        {
            internal DateTime CapturedUtc;
            internal double ElapsedMilliseconds;
            internal long GlobalOrdinal;
            internal int Sequence;
            internal string SourceIdentity;
            internal string TargetSlotIdentity;
            internal ItemSnapshot Item;
        }

        private readonly object syncRoot = new object();
        private readonly Stopwatch captureClock = new Stopwatch();
        private string pluginDirectory = string.Empty;
        private string sessionDirectory = string.Empty;
        private StreamWriter packetLog;
        private StreamWriter rawPacketLog;
        private StreamWriter movementPacketLog;
        private StreamWriter scfuAppearanceLog;
        private StreamWriter worldSnapshotLog;
        private StreamWriter playerCombatContextLog;
        private StreamWriter aggroObservationLog;
        private StreamWriter visibilityObservationLog;
        private StreamWriter inventorySnapshotLog;
        private StreamWriter itemUseObservationLog;
        private StreamWriter eventLog;
        private readonly Dictionary<string, EntitySnapshot> knownEntities =
            new Dictionary<string, EntitySnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CorpseSnapshot> knownCorpses =
            new Dictionary<string, CorpseSnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PositionHistory> positions =
            new Dictionary<string, PositionHistory>(StringComparer.OrdinalIgnoreCase);
        private readonly List<PendingAttack> pendingAttacks = new List<PendingAttack>();
        private readonly HashSet<string> playerAttackTargets =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> observedCorpseIdentities =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EntitySnapshot> previousVisibleDynels =
            new Dictionary<string, EntitySnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ItemSnapshot> latestItemsBySlot =
            new Dictionary<string, ItemSnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PendingItemUse> pendingItemUses =
            new Dictionary<string, PendingItemUse>(StringComparer.OrdinalIgnoreCase);
        private bool enabled;
        private bool stopRequested;
        private bool automaticCaptureEnabled;
        private bool teardownRequested;
        private bool inventorySnapshotRequested;
        private DateTime captureStartUtc;
        private DateTime stopRequestedUtc;
        private DateTime lastPacketUtc;
        private DateTime nextCheckpointUtc;
        private DateTime nextVisibilitySampleUtc;
        private DateTime previousVisibilitySampleUtc;
        private EntitySnapshot previousVisibilityPlayer;
        private int sessionPlayfieldId;
        private string sessionAreaName = string.Empty;
        private string playerIdentity = string.Empty;
        private string lastValidationStatus = "RUNNING";
        private string lastValidationSummary = string.Empty;
        private long globalOrdinal;
        private int inboundSequence;
        private int outboundSequence;
        private int rawWriteErrors;
        private int rawMissingPackets;
        private int checkpointWriteErrors;
        private int movementPacketRows;
        private int movementFollowTargetPackets;
        private int movementUsableFollowTargetPackets;
        private int movementSetPosPackets;
        private int movementStopMovingCmdPackets;
        private int movementCharDcMovePackets;
        private int movementDecodeErrors;
        private int scfuPackets;
        private int scfuDecodeErrors;
        private int worldSnapshotRows;
        private int worldSnapshotErrors;
        private int playerCombatContextRows;
        private int playerCombatContextCompleteRows;
        private int playerCombatContextErrors;
        private int attackStarts;
        private int attackInfoPackets;
        private int missedAttackPackets;
        private int deathActions;
        private int corpseFullUpdatePackets;
        private int inventoryUpdatePackets;
        private int identityLinkedInventoryPackets;
        private int aggroObservationRows;
        private int npcToPlayerAggroObservationRows;
        private int unprovokedNpcAggroObservationRows;
        private int visibilitySamples;
        private int visibilityObservationRows;
        private int visibilityTransitionRows;
        private int visibilitySampleErrors;
        private int despawnPackets;
        private int lineOfSightStateObservations;
        private int lineOfSightTransitionRows;
        private int lineOfSightReadErrors;
        private int inPlayStateObservations;
        private int inPlayTransitionRows;
        private int inPlayReadErrors;
        private int inventorySnapshotRows;
        private int inventorySnapshotErrors;
        private int itemUseRequests;
        private int resolvedItemUseRequests;
        private int itemUseAcknowledgements;
        private int deleteItemActions;
        private int correlatedDeleteItems;
        private int itemUseObservationRows;
        private int itemUseObservationErrors;

        public override void Run(string pluginDir)
        {
            this.pluginDirectory = pluginDir ?? string.Empty;
            try
            {
                Network.PacketReceived += this.OnPacketReceived;
                Network.PacketSent += this.OnPacketSent;
                AOInventory.ContainerOpened += this.OnContainerOpened;
                Game.OnUpdate += this.OnUpdate;
                Chat.RegisterCommand("aocap", this.OnCommand);
                Chat.WriteLine(
                    "AOSharpLiveCapture Mike 2022 ready. Use /aocap start; automatic capture is off.",
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
                this.teardownRequested = true;
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
                            this.automaticCaptureEnabled = false;
                            if (this.enabled)
                            {
                                this.stopRequested = false;
                                this.WriteEvent("MANUAL-START", "Existing session retained; automatic continuation disabled.");
                                this.WriteWorldSnapshot("manual-start");
                                this.WritePlayerCombatContext("manual-start");
                                this.WriteInventorySnapshot("manual-start");
                            }
                            else
                            {
                                this.StartSession("manual command");
                            }
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

                            this.automaticCaptureEnabled = false;
                            this.stopRequested = true;
                            this.stopRequestedUtc = DateTime.UtcNow;
                            this.WriteEvent("STOP-REQUEST", "Finalizing after packet drain; automatic continuation disabled.");
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
                            this.WriteWorldSnapshot("mark");
                            this.WritePlayerCombatContext("mark");
                            this.WriteInventorySnapshot("mark");
                        }

                        chatWindow.WriteLine("AO capture marker written.", ChatColor.Gold);
                        break;

                    case "snapshot":
                        lock (this.syncRoot)
                        {
                            this.WriteWorldSnapshot("manual-snapshot");
                            this.WritePlayerCombatContext("manual-snapshot");
                            this.WriteInventorySnapshot("manual-snapshot");
                            this.WriteCaptureInfo(false, "manual snapshot");
                        }

                        chatWindow.WriteLine("AO capture world, player, and inventory snapshot written.", ChatColor.Gold);
                        break;

                    case "auto":
                        lock (this.syncRoot)
                        {
                            string mode = args != null && args.Length > 1
                                              ? args[1].ToLowerInvariant()
                                              : "status";
                            if (mode == "on")
                            {
                                this.automaticCaptureEnabled = true;
                                if (!this.enabled)
                                {
                                    this.StartSession("automatic capture enabled");
                                }
                            }
                            else if (mode == "off")
                            {
                                this.automaticCaptureEnabled = false;
                            }

                            chatWindow.WriteLine(
                                "AO automatic capture " + (this.automaticCaptureEnabled ? "enabled." : "disabled."),
                                ChatColor.Gold);
                        }

                        break;

                    default:
                        lock (this.syncRoot)
                        {
                            string state = this.enabled ? (this.stopRequested ? "stopping" : "running") : "idle";
                            chatWindow.WriteLine(
                                "AO capture " + state
                                + ". auto=" + (this.automaticCaptureEnabled ? "on" : "off")
                                + ". in=" + this.inboundSequence.ToString(CultureInfo.InvariantCulture)
                                + " out=" + this.outboundSequence.ToString(CultureInfo.InvariantCulture)
                                + " movement=" + this.movementPacketRows.ToString(CultureInfo.InvariantCulture)
                                + " scfu=" + this.scfuPackets.ToString(CultureInfo.InvariantCulture)
                                + " visibility=" + this.visibilitySamples.ToString(CultureInfo.InvariantCulture)
                                + "/" + this.visibilityTransitionRows.ToString(CultureInfo.InvariantCulture)
                                + " los=" + this.lineOfSightStateObservations.ToString(CultureInfo.InvariantCulture)
                                + "/" + this.lineOfSightTransitionRows.ToString(CultureInfo.InvariantCulture)
                                + " inplay=" + this.inPlayStateObservations.ToString(CultureInfo.InvariantCulture)
                                + "/" + this.inPlayTransitionRows.ToString(CultureInfo.InvariantCulture)
                                + " attacks=" + this.attackStarts.ToString(CultureInfo.InvariantCulture)
                                + " corpses=" + this.corpseFullUpdatePackets.ToString(CultureInfo.InvariantCulture)
                                + " loot=" + this.inventoryUpdatePackets.ToString(CultureInfo.InvariantCulture)
                                + " itemuses=" + this.resolvedItemUseRequests.ToString(CultureInfo.InvariantCulture)
                                + "/" + this.itemUseRequests.ToString(CultureInfo.InvariantCulture)
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

        private void StartSession(string reason)
        {
            if (this.enabled)
            {
                this.FinalizeSessionNoThrow("restarted by command");
            }

            DateTime localNow = DateTime.Now;
            string areaName = GetAreaNameNoThrow();
            int playfieldId = GetPlayfieldIdNoThrow();
            this.sessionAreaName = areaName;
            this.sessionPlayfieldId = playfieldId;
            string captureId = CaptureSessionLayout.CreateCaptureId(localNow);
            this.sessionDirectory = CaptureSessionLayout.CreateSessionDirectory(
                this.pluginDirectory,
                areaName,
                playfieldId,
                captureId,
                "Mike 2022");

            try
            {
                this.packetLog = CreateWriter(Path.Combine(this.sessionDirectory, "packets.hex.log"));
                this.rawPacketLog = CreateWriter(Path.Combine(this.sessionDirectory, "raw-packets.csv"));
                this.movementPacketLog = CreateWriter(Path.Combine(this.sessionDirectory, "movement-packets.csv"));
                this.scfuAppearanceLog = CreateWriter(Path.Combine(this.sessionDirectory, "scfu-appearance.csv"));
                this.worldSnapshotLog = CreateWriter(Path.Combine(this.sessionDirectory, "world-snapshot.csv"));
                this.playerCombatContextLog = CreateWriter(Path.Combine(this.sessionDirectory, "player-combat-context.csv"));
                this.aggroObservationLog = CreateWriter(Path.Combine(this.sessionDirectory, "aggro-observations.csv"));
                this.visibilityObservationLog = CreateWriter(Path.Combine(this.sessionDirectory, "visibility-observations.csv"));
                this.inventorySnapshotLog = CreateWriter(Path.Combine(this.sessionDirectory, "inventory-snapshots.csv"));
                this.itemUseObservationLog = CreateWriter(Path.Combine(this.sessionDirectory, "item-use-observations.csv"));
                this.eventLog = CreateWriter(Path.Combine(this.sessionDirectory, "events.log"));
                this.rawPacketLog.WriteLine(
                    "CapturedUtc,ElapsedMilliseconds,Direction,GlobalOrdinal,Sequence,PacketLength,N3TypeValue,N3TypeName,IdentityType,IdentityInstance,PreservationStatus,RawHex");
                this.movementPacketLog.WriteLine(
                    "CapturedUtc,Direction,Sequence,MessageType,SourceType,SourceInstance,SourceIdentity,SourceName,TargetType,TargetInstance,TargetIdentity,TargetName,FollowKind,CurrentX,CurrentY,CurrentZ,DestinationX,DestinationY,DestinationZ,Speed,Animation,Flags,PathCount,RawParams,RawTailHex");
                this.scfuAppearanceLog.WriteLine(RawScfuAppearanceCsv.Header);
                this.worldSnapshotLog.WriteLine(
                    "CapturedUtc,Phase,Kind,Identity,Name,PlayfieldId,PositionX,PositionY,PositionZ,HeadingX,HeadingY,HeadingZ,HeadingW,Level,Health,HealthDamage,MonsterData,DeadNpcIdentity,Credits,EvidenceUtc,EvidenceSource");
                this.playerCombatContextLog.WriteLine(
                    "CapturedUtc,Phase,Identity,Name,Level,Profession,PositionX,PositionY,PositionZ,Health,MaxHealth,RunSpeed,EvadeClsC,DodgeRanged,DuckExp,MeleeAC,ProjectileAC,EnergyAC,ChemicalAC,RadiationAC,ColdAC,PoisonAC,FireAC,ActiveNanos,Equipment,Error");
                this.aggroObservationLog.WriteLine(
                    "CapturedUtc,ElapsedMilliseconds,GlobalOrdinal,Direction,Sequence,SourceIdentity,SourceName,TargetIdentity,TargetName,InitiatorRole,PlayerPreviouslyAttackedSource,SourcePositionUtc,SourcePositionMessage,SourceX,SourceY,SourceZ,TargetPositionUtc,TargetPositionMessage,TargetX,TargetY,TargetZ,PreviousTargetPositionUtc,PreviousTargetX,PreviousTargetY,PreviousTargetZ,TriggerDistance,PreviousDistance,DistanceBracketMin,DistanceBracketMax,SourcePositionDeltaMs,TargetPositionDeltaMs,CorrelationStatus");
                this.visibilityObservationLog.WriteLine(
                    "CapturedUtc,ElapsedMilliseconds,SampleIndex,Phase,Observation,Identity,Name,Kind,PlayfieldId,PlayerX,PlayerY,PlayerZ,EntityX,EntityY,EntityZ,Distance,PreviousSampleUtc,PreviousPlayerX,PreviousPlayerY,PreviousPlayerZ,PreviousEntityX,PreviousEntityY,PreviousEntityZ,PreviousDistance,LineOfSight,PreviousLineOfSight,IsInPlay,PreviousIsInPlay,Direction,Sequence,GlobalOrdinal,EvidenceSource");
                this.inventorySnapshotLog.WriteLine(
                    "CapturedUtc,ElapsedMilliseconds,Phase,ContainerKind,ContainerIdentity,ContainerName,ContainerSlotIdentity,ContainerOpen,ItemSlotIdentity,ItemUniqueIdentity,LowId,HighId,QualityLevel,Name,Charges,ResolutionStatus,Error");
                this.itemUseObservationLog.WriteLine(
                    "CapturedUtc,ElapsedMilliseconds,Phase,Direction,GlobalOrdinal,Sequence,SourceIdentity,Action,TargetSlotIdentity,ContainerKind,ContainerIdentity,ContainerName,ContainerSlotIdentity,ItemUniqueIdentity,LowId,HighId,QualityLevel,Name,Charges,ItemSnapshotUtc,ResolutionStatus,RelatedUseUtc,RelatedUseOrdinal,RelatedUseSequence,EvidenceSource,Error");
            }
            catch
            {
                this.packetLog = CloseWriter(this.packetLog);
                this.rawPacketLog = CloseWriter(this.rawPacketLog);
                this.movementPacketLog = CloseWriter(this.movementPacketLog);
                this.scfuAppearanceLog = CloseWriter(this.scfuAppearanceLog);
                this.worldSnapshotLog = CloseWriter(this.worldSnapshotLog);
                this.playerCombatContextLog = CloseWriter(this.playerCombatContextLog);
                this.aggroObservationLog = CloseWriter(this.aggroObservationLog);
                this.visibilityObservationLog = CloseWriter(this.visibilityObservationLog);
                this.inventorySnapshotLog = CloseWriter(this.inventorySnapshotLog);
                this.itemUseObservationLog = CloseWriter(this.itemUseObservationLog);
                this.eventLog = CloseWriter(this.eventLog);
                throw;
            }

            this.globalOrdinal = 0;
            this.inboundSequence = 0;
            this.outboundSequence = 0;
            this.rawWriteErrors = 0;
            this.rawMissingPackets = 0;
            this.checkpointWriteErrors = 0;
            this.movementPacketRows = 0;
            this.movementFollowTargetPackets = 0;
            this.movementUsableFollowTargetPackets = 0;
            this.movementSetPosPackets = 0;
            this.movementStopMovingCmdPackets = 0;
            this.movementCharDcMovePackets = 0;
            this.movementDecodeErrors = 0;
            this.scfuPackets = 0;
            this.scfuDecodeErrors = 0;
            this.worldSnapshotRows = 0;
            this.worldSnapshotErrors = 0;
            this.playerCombatContextRows = 0;
            this.playerCombatContextCompleteRows = 0;
            this.playerCombatContextErrors = 0;
            this.attackStarts = 0;
            this.attackInfoPackets = 0;
            this.missedAttackPackets = 0;
            this.deathActions = 0;
            this.corpseFullUpdatePackets = 0;
            this.inventoryUpdatePackets = 0;
            this.identityLinkedInventoryPackets = 0;
            this.aggroObservationRows = 0;
            this.npcToPlayerAggroObservationRows = 0;
            this.unprovokedNpcAggroObservationRows = 0;
            this.visibilitySamples = 0;
            this.visibilityObservationRows = 0;
            this.visibilityTransitionRows = 0;
            this.visibilitySampleErrors = 0;
            this.despawnPackets = 0;
            this.lineOfSightStateObservations = 0;
            this.lineOfSightTransitionRows = 0;
            this.lineOfSightReadErrors = 0;
            this.inPlayStateObservations = 0;
            this.inPlayTransitionRows = 0;
            this.inPlayReadErrors = 0;
            this.inventorySnapshotRows = 0;
            this.inventorySnapshotErrors = 0;
            this.itemUseRequests = 0;
            this.resolvedItemUseRequests = 0;
            this.itemUseAcknowledgements = 0;
            this.deleteItemActions = 0;
            this.correlatedDeleteItems = 0;
            this.itemUseObservationRows = 0;
            this.itemUseObservationErrors = 0;
            this.knownEntities.Clear();
            this.knownCorpses.Clear();
            this.positions.Clear();
            this.pendingAttacks.Clear();
            this.playerAttackTargets.Clear();
            this.observedCorpseIdentities.Clear();
            this.previousVisibleDynels.Clear();
            this.latestItemsBySlot.Clear();
            this.pendingItemUses.Clear();
            this.previousVisibilityPlayer = null;
            this.previousVisibilitySampleUtc = DateTime.MinValue;
            this.lastValidationStatus = "RUNNING";
            this.lastValidationSummary = string.Empty;
            this.stopRequested = false;
            this.inventorySnapshotRequested = false;
            this.captureStartUtc = DateTime.UtcNow;
            this.lastPacketUtc = this.captureStartUtc;
            this.captureClock.Restart();
            this.enabled = true;
            this.nextCheckpointUtc = this.captureStartUtc.AddSeconds(CheckpointSeconds);
            this.nextVisibilitySampleUtc = this.captureStartUtc.AddMilliseconds(VisibilitySampleMilliseconds);
            this.WriteEvent("START", "AOSharp Mike 2022 compatibility capture started: " + reason);
            this.WriteWorldSnapshot("capture-start");
            this.WritePlayerCombatContext("capture-start");
            this.WriteInventorySnapshot("capture-start");
            this.WriteCaptureInfo(false, reason);
        }

        private void OnPacketReceived(object sender, byte[] packet)
        {
            this.CapturePacket("IN", packet, true);
        }

        private void OnPacketSent(object sender, byte[] packet)
        {
            this.CapturePacket("OUT", packet, false);
        }

        private void OnContainerOpened(object sender, AOContainer container)
        {
            lock (this.syncRoot)
            {
                if (!this.enabled)
                {
                    return;
                }

                this.inventorySnapshotRequested = true;
            }
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
                if (packet == null)
                {
                    this.rawMissingPackets++;
                }

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

                try
                {
                    this.CaptureEvidenceProjection(
                        capturedUtc,
                        this.captureClock.Elapsed.TotalMilliseconds,
                        ordinal,
                        direction,
                        sequence,
                        packet,
                        n3Type);
                }
                catch (Exception ex)
                {
                    this.WriteFallbackError("Evidence." + direction, ex);
                }

                this.ResolvePendingAttacks(capturedUtc, false);
            }
        }

        private void CaptureEvidenceProjection(
            DateTime capturedUtc,
            double elapsedMilliseconds,
            long ordinal,
            string direction,
            int sequence,
            byte[] packet,
            int messageType)
        {
            if (packet == null)
            {
                return;
            }

            if (messageType == RawSimpleCharFullUpdateDecoder.SimpleCharFullUpdateType)
            {
                this.CaptureSimpleCharFullUpdate(
                    capturedUtc,
                    elapsedMilliseconds,
                    ordinal,
                    direction,
                    sequence,
                    packet);
                return;
            }

            if (messageType == DespawnMessage)
            {
                this.CaptureDespawnObservation(
                    capturedUtc,
                    elapsedMilliseconds,
                    ordinal,
                    direction,
                    sequence,
                    packet);
                return;
            }

            if (messageType == GenericCmdMessage)
            {
                this.CaptureGenericCmdItemUse(
                    capturedUtc,
                    elapsedMilliseconds,
                    ordinal,
                    direction,
                    sequence,
                    packet);
                return;
            }

            if (messageType == AttackMessage)
            {
                this.CaptureAttackStart(
                    capturedUtc,
                    elapsedMilliseconds,
                    ordinal,
                    direction,
                    sequence,
                    packet);
                return;
            }

            if (messageType == AttackInfoMessage)
            {
                this.attackInfoPackets++;
                return;
            }

            if (messageType == MissedAttackInfoMessage)
            {
                this.missedAttackPackets++;
                return;
            }

            if (messageType == CharacterActionMessage)
            {
                int action = packet.Length >= 33 ? ReadInt32BigEndian(packet, 29) : 0;
                if (action == 99)
                {
                    this.deathActions++;
                }

                if (action == DeleteItemAction)
                {
                    this.CaptureDeleteItemObservation(
                        capturedUtc,
                        elapsedMilliseconds,
                        ordinal,
                        direction,
                        sequence,
                        packet);
                }

                return;
            }

            if (messageType == CorpseFullUpdateMessage)
            {
                this.CaptureCorpseFullUpdate(capturedUtc, packet);
                return;
            }

            if (messageType == InventoryUpdateMessage)
            {
                this.inventoryUpdatePackets++;
                this.latestItemsBySlot.Clear();
                this.inventorySnapshotRequested = true;
                if (this.PacketContainsObservedCorpseIdentity(packet))
                {
                    this.identityLinkedInventoryPackets++;
                }
            }
        }

        private void CaptureGenericCmdItemUse(
            DateTime capturedUtc,
            double elapsedMilliseconds,
            long ordinal,
            string direction,
            int sequence,
            byte[] packet)
        {
            if (packet.Length < 61 || ReadInt32BigEndian(packet, 37) != GenericCmdUseAction)
            {
                return;
            }

            uint userType;
            uint userInstance;
            uint targetType;
            uint targetInstance;
            if (!TryReadIdentity(packet, 45, out userType, out userInstance)
                || !TryReadIdentity(packet, 53, out targetType, out targetInstance)
                || !IsPotentialItemSlotType(targetType))
            {
                return;
            }

            string sourceIdentity = FormatIdentity(userType, userInstance);
            string targetSlotIdentity = FormatIdentity(targetType, targetInstance);
            this.RefreshPlayerIdentity();
            if (this.playerIdentity.Length > 0
                && !IdentityEquals(sourceIdentity, this.playerIdentity))
            {
                return;
            }

            bool outbound = string.Equals(direction, "OUT", StringComparison.OrdinalIgnoreCase);
            ItemSnapshot item = null;
            string resolutionError = string.Empty;
            string resolutionStatus;
            PendingItemUse relatedUse = null;

            if (outbound)
            {
                this.itemUseRequests++;
                if (this.TryResolveItemSlot(
                        new Identity((IdentityType)targetType, unchecked((int)targetInstance)),
                        capturedUtc,
                        out item,
                        out resolutionError))
                {
                    this.resolvedItemUseRequests++;
                    resolutionStatus = item.ResolutionStatus;
                }
                else
                {
                    resolutionStatus = "UNRESOLVED";
                }

                relatedUse = new PendingItemUse
                {
                    CapturedUtc = capturedUtc,
                    ElapsedMilliseconds = elapsedMilliseconds,
                    GlobalOrdinal = ordinal,
                    Sequence = sequence,
                    SourceIdentity = sourceIdentity,
                    TargetSlotIdentity = targetSlotIdentity,
                    Item = item
                };
                this.pendingItemUses[targetSlotIdentity] = relatedUse;
            }
            else
            {
                this.itemUseAcknowledgements++;
                if (this.TryGetRecentPendingItemUse(targetSlotIdentity, capturedUtc, out relatedUse))
                {
                    item = relatedUse.Item;
                    resolutionStatus = item == null ? "ACK_CORRELATED_UNRESOLVED" : "ACK_CORRELATED_RESOLVED";
                    if (item == null)
                    {
                        resolutionError = "The acknowledged slot was unresolved at the outbound use boundary.";
                    }
                }
                else if (this.TryResolveItemSlot(
                             new Identity((IdentityType)targetType, unchecked((int)targetInstance)),
                             capturedUtc,
                             out item,
                             out resolutionError))
                {
                    resolutionStatus = "ACK_" + item.ResolutionStatus;
                }
                else
                {
                    resolutionStatus = "ACK_UNRESOLVED";
                }
            }

            this.WriteItemUseObservation(
                capturedUtc,
                elapsedMilliseconds,
                outbound ? "USE_REQUEST" : "USE_ACK",
                direction,
                ordinal,
                sequence,
                sourceIdentity,
                "Use",
                targetSlotIdentity,
                item,
                resolutionStatus,
                relatedUse,
                "RawGenericCmd+AOSharpInventory",
                resolutionError);
        }

        private void CaptureDeleteItemObservation(
            DateTime capturedUtc,
            double elapsedMilliseconds,
            long ordinal,
            string direction,
            int sequence,
            byte[] packet)
        {
            uint sourceType;
            uint sourceInstance;
            uint targetType;
            uint targetInstance;
            if (packet.Length < 45
                || !TryReadIdentity(packet, 20, out sourceType, out sourceInstance)
                || !TryReadIdentity(packet, 37, out targetType, out targetInstance)
                || !IsPotentialItemSlotType(targetType))
            {
                return;
            }

            string sourceIdentity = FormatIdentity(sourceType, sourceInstance);
            string targetSlotIdentity = FormatIdentity(targetType, targetInstance);
            this.RefreshPlayerIdentity();
            if (this.playerIdentity.Length > 0
                && !IdentityEquals(sourceIdentity, this.playerIdentity))
            {
                return;
            }

            this.deleteItemActions++;
            PendingItemUse relatedUse;
            ItemSnapshot item = null;
            string resolutionError = string.Empty;
            string resolutionStatus;
            if (this.TryGetRecentPendingItemUse(targetSlotIdentity, capturedUtc, out relatedUse))
            {
                this.correlatedDeleteItems++;
                item = relatedUse.Item;
                resolutionStatus = item == null ? "DELETE_CORRELATED_UNRESOLVED" : "DELETE_CORRELATED_RESOLVED";
                if (item == null)
                {
                    resolutionError = "The deleted slot was unresolved at the outbound use boundary.";
                }

                this.pendingItemUses.Remove(targetSlotIdentity);
            }
            else if (this.TryResolveItemSlot(
                         new Identity((IdentityType)targetType, unchecked((int)targetInstance)),
                         capturedUtc,
                         out item,
                         out resolutionError))
            {
                relatedUse = null;
                resolutionStatus = "DELETE_" + item.ResolutionStatus;
            }
            else
            {
                relatedUse = null;
                resolutionStatus = "DELETE_UNRESOLVED";
            }

            this.WriteItemUseObservation(
                capturedUtc,
                elapsedMilliseconds,
                "DELETE_ITEM",
                direction,
                ordinal,
                sequence,
                sourceIdentity,
                "DeleteItem",
                targetSlotIdentity,
                item,
                resolutionStatus,
                relatedUse,
                "RawCharacterAction+PendingItemUse",
                resolutionError);
        }

        private bool TryGetRecentPendingItemUse(
            string targetSlotIdentity,
            DateTime capturedUtc,
            out PendingItemUse pending)
        {
            if (this.pendingItemUses.TryGetValue(targetSlotIdentity, out pending)
                && capturedUtc >= pending.CapturedUtc
                && capturedUtc - pending.CapturedUtc <= TimeSpan.FromSeconds(ItemUseCorrelationSeconds))
            {
                return true;
            }

            pending = null;
            return false;
        }

        private bool TryResolveItemSlot(
            Identity slot,
            DateTime capturedUtc,
            out ItemSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            var errors = new List<string>();
            try
            {
                AOItem item;
                if (AOInventory.Find(slot, out item) && item != null)
                {
                    snapshot = this.CreateItemSnapshot(
                        capturedUtc,
                        "MAIN_INVENTORY",
                        this.playerIdentity,
                        GetLocalPlayerNameNoThrow(),
                        string.Empty,
                        item,
                        "RESOLVED_LIVE");
                    this.latestItemsBySlot[snapshot.SlotIdentity] = snapshot;
                    error = string.Empty;
                    return true;
                }
            }
            catch (Exception ex)
            {
                errors.Add("main inventory: " + ex.GetType().Name + ": " + OneLine(ex.Message));
            }

            try
            {
                List<AOBackpack> backpacks = AOInventory.Backpacks;
                foreach (AOBackpack backpack in backpacks)
                {
                    try
                    {
                        if (backpack == null || !backpack.IsOpen)
                        {
                            continue;
                        }

                        AOItem item;
                        if (!backpack.Find(slot, out item) || item == null)
                        {
                            continue;
                        }

                        snapshot = this.CreateItemSnapshot(
                            capturedUtc,
                            "BACKPACK",
                            FormatRuntimeIdentity(backpack.Identity),
                            GetBackpackNameNoThrow(backpack),
                            FormatRuntimeIdentity(backpack.Slot),
                            item,
                            "RESOLVED_LIVE");
                        this.latestItemsBySlot[snapshot.SlotIdentity] = snapshot;
                        error = string.Empty;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        errors.Add("backpack: " + ex.GetType().Name + ": " + OneLine(ex.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add("backpack list: " + ex.GetType().Name + ": " + OneLine(ex.Message));
            }

            ItemSnapshot cached;
            string slotIdentity = FormatRuntimeIdentity(slot);
            if (this.latestItemsBySlot.TryGetValue(slotIdentity, out cached)
                && capturedUtc >= cached.CapturedUtc
                && capturedUtc - cached.CapturedUtc <= TimeSpan.FromSeconds(ItemUseCorrelationSeconds))
            {
                snapshot = CloneItemSnapshot(cached, "RESOLVED_RECENT_SNAPSHOT");
                error = errors.Count == 0 ? string.Empty : string.Join(" | ", errors.ToArray());
                return true;
            }

            error = errors.Count == 0
                        ? "Slot was not found in the main inventory or any loaded open backpack."
                        : string.Join(" | ", errors.ToArray());
            return false;
        }

        private ItemSnapshot CreateItemSnapshot(
            DateTime capturedUtc,
            string containerKind,
            string containerIdentity,
            string containerName,
            string containerSlotIdentity,
            AOItem item,
            string resolutionStatus)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            return new ItemSnapshot
            {
                CapturedUtc = capturedUtc,
                ContainerKind = containerKind ?? string.Empty,
                ContainerIdentity = containerIdentity ?? string.Empty,
                ContainerName = containerName ?? string.Empty,
                ContainerSlotIdentity = containerSlotIdentity ?? string.Empty,
                SlotIdentity = FormatRuntimeIdentity(item.Slot),
                UniqueIdentity = FormatRuntimeIdentity(item.UniqueIdentity),
                LowId = item.Id,
                HighId = item.HighId,
                QualityLevel = item.QualityLevel,
                Charges = item.Charges,
                Name = item.Name ?? string.Empty,
                ResolutionStatus = resolutionStatus ?? string.Empty,
                Error = string.Empty
            };
        }

        private static ItemSnapshot CloneItemSnapshot(ItemSnapshot source, string resolutionStatus)
        {
            return new ItemSnapshot
            {
                CapturedUtc = source.CapturedUtc,
                ContainerKind = source.ContainerKind,
                ContainerIdentity = source.ContainerIdentity,
                ContainerName = source.ContainerName,
                ContainerSlotIdentity = source.ContainerSlotIdentity,
                SlotIdentity = source.SlotIdentity,
                UniqueIdentity = source.UniqueIdentity,
                LowId = source.LowId,
                HighId = source.HighId,
                QualityLevel = source.QualityLevel,
                Charges = source.Charges,
                Name = source.Name,
                ResolutionStatus = resolutionStatus,
                Error = source.Error
            };
        }

        private void WriteInventorySnapshot(string phase)
        {
            if (this.inventorySnapshotLog == null)
            {
                return;
            }

            DateTime capturedUtc = DateTime.UtcNow;
            this.RefreshPlayerIdentity();
            string playerName = GetLocalPlayerNameNoThrow();
            try
            {
                List<AOItem> items = AOInventory.Items;
                if (items == null || items.Count == 0)
                {
                    this.WriteInventorySnapshotRow(
                        capturedUtc,
                        phase,
                        true,
                        ContainerStatusSnapshot(
                            capturedUtc,
                            "MAIN_INVENTORY",
                            this.playerIdentity,
                            playerName,
                            string.Empty),
                        "EMPTY",
                        string.Empty);
                }
                else
                {
                    foreach (AOItem item in items)
                    {
                        try
                        {
                            ItemSnapshot snapshot = this.CreateItemSnapshot(
                                capturedUtc,
                                "MAIN_INVENTORY",
                                this.playerIdentity,
                                playerName,
                                string.Empty,
                                item,
                                "SNAPSHOT");
                            this.latestItemsBySlot[snapshot.SlotIdentity] = snapshot;
                            this.WriteInventorySnapshotRow(
                                capturedUtc,
                                phase,
                                true,
                                snapshot,
                                "ITEM",
                                string.Empty);
                        }
                        catch (Exception ex)
                        {
                            this.inventorySnapshotErrors++;
                            this.WriteInventorySnapshotRow(
                                capturedUtc,
                                phase,
                                true,
                                ContainerStatusSnapshot(
                                    capturedUtc,
                                    "MAIN_INVENTORY",
                                    this.playerIdentity,
                                    playerName,
                                    string.Empty),
                                "ITEM_READ_ERROR",
                                ex.GetType().Name + ": " + OneLine(ex.Message));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.inventorySnapshotErrors++;
                this.WriteInventorySnapshotRow(
                    capturedUtc,
                    phase,
                    true,
                    ContainerStatusSnapshot(
                        capturedUtc,
                        "MAIN_INVENTORY",
                        this.playerIdentity,
                        playerName,
                        string.Empty),
                    "CONTAINER_READ_ERROR",
                    ex.GetType().Name + ": " + OneLine(ex.Message));
            }

            List<AOBackpack> backpacks;
            try
            {
                backpacks = AOInventory.Backpacks;
            }
            catch (Exception ex)
            {
                this.inventorySnapshotErrors++;
                this.WriteInventorySnapshotRow(
                    capturedUtc,
                    phase,
                    false,
                    ContainerStatusSnapshot(
                        capturedUtc,
                        "BACKPACKS",
                        string.Empty,
                        string.Empty,
                        string.Empty),
                    "CONTAINER_LIST_ERROR",
                    ex.GetType().Name + ": " + OneLine(ex.Message));
                return;
            }

            foreach (AOBackpack backpack in backpacks)
            {
                if (backpack == null)
                {
                    continue;
                }

                string containerIdentity = FormatRuntimeIdentity(backpack.Identity);
                string containerSlotIdentity = FormatRuntimeIdentity(backpack.Slot);
                string containerName = GetBackpackNameNoThrow(backpack);
                bool isOpen;
                try
                {
                    isOpen = backpack.IsOpen;
                }
                catch (Exception ex)
                {
                    this.inventorySnapshotErrors++;
                    this.WriteInventorySnapshotRow(
                        capturedUtc,
                        phase,
                        false,
                        ContainerStatusSnapshot(
                            capturedUtc,
                            "BACKPACK",
                            containerIdentity,
                            containerName,
                            containerSlotIdentity),
                        "OPEN_STATE_ERROR",
                        ex.GetType().Name + ": " + OneLine(ex.Message));
                    continue;
                }

                if (!isOpen)
                {
                    this.WriteInventorySnapshotRow(
                        capturedUtc,
                        phase,
                        false,
                        ContainerStatusSnapshot(
                            capturedUtc,
                            "BACKPACK",
                            containerIdentity,
                            containerName,
                            containerSlotIdentity),
                        "CLOSED",
                        string.Empty);
                    continue;
                }

                try
                {
                    List<AOItem> items = backpack.Items;
                    if (items == null || items.Count == 0)
                    {
                        this.WriteInventorySnapshotRow(
                            capturedUtc,
                            phase,
                            true,
                            ContainerStatusSnapshot(
                                capturedUtc,
                                "BACKPACK",
                                containerIdentity,
                                containerName,
                                containerSlotIdentity),
                            "OPEN_EMPTY",
                            string.Empty);
                        continue;
                    }

                    foreach (AOItem item in items)
                    {
                        try
                        {
                            ItemSnapshot snapshot = this.CreateItemSnapshot(
                                capturedUtc,
                                "BACKPACK",
                                containerIdentity,
                                containerName,
                                containerSlotIdentity,
                                item,
                                "SNAPSHOT");
                            this.latestItemsBySlot[snapshot.SlotIdentity] = snapshot;
                            this.WriteInventorySnapshotRow(
                                capturedUtc,
                                phase,
                                true,
                                snapshot,
                                "ITEM",
                                string.Empty);
                        }
                        catch (Exception ex)
                        {
                            this.inventorySnapshotErrors++;
                            this.WriteInventorySnapshotRow(
                                capturedUtc,
                                phase,
                                true,
                                ContainerStatusSnapshot(
                                    capturedUtc,
                                    "BACKPACK",
                                    containerIdentity,
                                    containerName,
                                    containerSlotIdentity),
                                "ITEM_READ_ERROR",
                                ex.GetType().Name + ": " + OneLine(ex.Message));
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.inventorySnapshotErrors++;
                    this.WriteInventorySnapshotRow(
                        capturedUtc,
                        phase,
                        true,
                        ContainerStatusSnapshot(
                            capturedUtc,
                            "BACKPACK",
                            containerIdentity,
                            containerName,
                            containerSlotIdentity),
                        "CONTAINER_READ_ERROR",
                        ex.GetType().Name + ": " + OneLine(ex.Message));
                }
            }
        }

        private void WriteInventorySnapshotRow(
            DateTime capturedUtc,
            string phase,
            bool containerOpen,
            ItemSnapshot item,
            string status,
            string error)
        {
            if (this.inventorySnapshotLog == null || item == null)
            {
                return;
            }

            try
            {
                this.inventorySnapshotLog.WriteLine(
                    string.Join(
                        ",",
                        Csv(capturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                        this.captureClock.Elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
                        Csv(phase),
                        Csv(item.ContainerKind),
                        Csv(item.ContainerIdentity),
                        Csv(item.ContainerName),
                        Csv(item.ContainerSlotIdentity),
                        Csv(containerOpen ? "true" : "false"),
                        Csv(item.SlotIdentity),
                        Csv(item.UniqueIdentity),
                        item.SlotIdentity.Length == 0 ? string.Empty : item.LowId.ToString(CultureInfo.InvariantCulture),
                        item.SlotIdentity.Length == 0 ? string.Empty : item.HighId.ToString(CultureInfo.InvariantCulture),
                        item.SlotIdentity.Length == 0 ? string.Empty : item.QualityLevel.ToString(CultureInfo.InvariantCulture),
                        Csv(item.Name),
                        item.SlotIdentity.Length == 0 ? string.Empty : item.Charges.ToString(CultureInfo.InvariantCulture),
                        Csv(status),
                        Csv(error)));
                this.inventorySnapshotRows++;
            }
            catch (Exception ex)
            {
                this.inventorySnapshotErrors++;
                this.WriteFallbackError("InventorySnapshot.Write", ex);
            }
        }

        private void WriteItemUseObservation(
            DateTime capturedUtc,
            double elapsedMilliseconds,
            string phase,
            string direction,
            long ordinal,
            int sequence,
            string sourceIdentity,
            string action,
            string targetSlotIdentity,
            ItemSnapshot item,
            string resolutionStatus,
            PendingItemUse relatedUse,
            string evidenceSource,
            string error)
        {
            if (this.itemUseObservationLog == null)
            {
                return;
            }

            bool includeRelated = relatedUse != null
                                  && !string.Equals(phase, "USE_REQUEST", StringComparison.OrdinalIgnoreCase);
            try
            {
                this.itemUseObservationLog.WriteLine(
                    string.Join(
                        ",",
                        Csv(capturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                        elapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
                        Csv(phase),
                        Csv(direction),
                        ordinal.ToString(CultureInfo.InvariantCulture),
                        sequence.ToString(CultureInfo.InvariantCulture),
                        Csv(sourceIdentity),
                        Csv(action),
                        Csv(targetSlotIdentity),
                        Csv(item == null ? string.Empty : item.ContainerKind),
                        Csv(item == null ? string.Empty : item.ContainerIdentity),
                        Csv(item == null ? string.Empty : item.ContainerName),
                        Csv(item == null ? string.Empty : item.ContainerSlotIdentity),
                        Csv(item == null ? string.Empty : item.UniqueIdentity),
                        item == null ? string.Empty : item.LowId.ToString(CultureInfo.InvariantCulture),
                        item == null ? string.Empty : item.HighId.ToString(CultureInfo.InvariantCulture),
                        item == null ? string.Empty : item.QualityLevel.ToString(CultureInfo.InvariantCulture),
                        Csv(item == null ? string.Empty : item.Name),
                        item == null ? string.Empty : item.Charges.ToString(CultureInfo.InvariantCulture),
                        Csv(item == null ? string.Empty : item.CapturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                        Csv(resolutionStatus),
                        Csv(includeRelated ? relatedUse.CapturedUtc.ToString("o", CultureInfo.InvariantCulture) : string.Empty),
                        includeRelated ? relatedUse.GlobalOrdinal.ToString(CultureInfo.InvariantCulture) : string.Empty,
                        includeRelated ? relatedUse.Sequence.ToString(CultureInfo.InvariantCulture) : string.Empty,
                        Csv(evidenceSource),
                        Csv(error)));
                this.itemUseObservationRows++;
            }
            catch (Exception ex)
            {
                this.itemUseObservationErrors++;
                this.WriteFallbackError("ItemUseObservation.Write", ex);
            }
        }

        private static ItemSnapshot ContainerStatusSnapshot(
            DateTime capturedUtc,
            string containerKind,
            string containerIdentity,
            string containerName,
            string containerSlotIdentity)
        {
            return new ItemSnapshot
            {
                CapturedUtc = capturedUtc,
                ContainerKind = containerKind ?? string.Empty,
                ContainerIdentity = containerIdentity ?? string.Empty,
                ContainerName = containerName ?? string.Empty,
                ContainerSlotIdentity = containerSlotIdentity ?? string.Empty,
                SlotIdentity = string.Empty,
                UniqueIdentity = string.Empty,
                Name = string.Empty,
                ResolutionStatus = string.Empty,
                Error = string.Empty
            };
        }

        private static bool IsPotentialItemSlotType(uint identityType)
        {
            return (identityType >= 0x65 && identityType <= 0x6F)
                   || identityType == 0x73;
        }

        private static string FormatRuntimeIdentity(Identity identity)
        {
            return FormatIdentity(
                unchecked((uint)(int)identity.Type),
                unchecked((uint)identity.Instance));
        }

        private static string GetLocalPlayerNameNoThrow()
        {
            try { return DynelManager.LocalPlayer == null ? string.Empty : DynelManager.LocalPlayer.Name; }
            catch { return string.Empty; }
        }

        private static string GetBackpackNameNoThrow(AOBackpack backpack)
        {
            try { return backpack == null ? string.Empty : backpack.Name ?? string.Empty; }
            catch { return string.Empty; }
        }

        private void CaptureSimpleCharFullUpdate(
            DateTime capturedUtc,
            double elapsedMilliseconds,
            long ordinal,
            string direction,
            int sequence,
            byte[] packet)
        {
            this.scfuPackets++;
            RawSimpleCharFullUpdate decoded;
            string error;
            bool success = RawSimpleCharFullUpdateDecoder.TryDecodePacket(
                packet,
                out decoded,
                out error);
            if (!success)
            {
                this.scfuDecodeErrors++;
            }

            var metadata = new RawScfuCaptureMetadata
            {
                CapturedUtc = capturedUtc.ToString("o", CultureInfo.InvariantCulture),
                ElapsedMilliseconds = elapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
                Direction = direction,
                GlobalOrdinal = ordinal.ToString(CultureInfo.InvariantCulture),
                Sequence = sequence.ToString(CultureInfo.InvariantCulture)
            };
            this.scfuAppearanceLog.WriteLine(
                RawScfuAppearanceCsv.FormatRow(metadata, packet, decoded, error));

            if (!success || decoded == null)
            {
                return;
            }

            string identity = NormalizeIdentity(decoded.Identity.ToString());
            var entity = new EntitySnapshot
            {
                CapturedUtc = capturedUtc,
                Identity = identity,
                Name = decoded.Name ?? string.Empty,
                PlayfieldId = decoded.PlayfieldId ?? this.sessionPlayfieldId,
                X = decoded.Position.X,
                Y = decoded.Position.Y,
                Z = decoded.Position.Z,
                HeadingX = decoded.Heading.X,
                HeadingY = decoded.Heading.Y,
                HeadingZ = decoded.Heading.Z,
                HeadingW = decoded.Heading.W,
                Level = decoded.Level,
                Health = decoded.Health,
                HealthDamage = decoded.HealthDamage,
                MonsterData = decoded.MonsterData,
                Kind = decoded.Npc != null ? "NPC" : "Character",
                HasPosition = true,
                IsNpc = decoded.Npc != null
            };
            this.knownEntities[identity] = entity;
            this.UpdatePosition(
                identity,
                capturedUtc,
                "SimpleCharFullUpdate",
                direction,
                sequence,
                decoded.Position.X,
                decoded.Position.Y,
                decoded.Position.Z);
        }

        private void CaptureDespawnObservation(
            DateTime capturedUtc,
            double elapsedMilliseconds,
            long ordinal,
            string direction,
            int sequence,
            byte[] packet)
        {
            uint identityType;
            uint identityInstance;
            if (!TryReadIdentity(packet, 20, out identityType, out identityInstance))
            {
                return;
            }

            this.despawnPackets++;
            string identity = FormatIdentity(identityType, identityInstance);
            EntitySnapshot prior;
            this.previousVisibleDynels.TryGetValue(identity, out prior);
            if (prior == null)
            {
                this.knownEntities.TryGetValue(identity, out prior);
            }

            var entity = new EntitySnapshot
            {
                CapturedUtc = capturedUtc,
                Identity = identity,
                Name = prior == null ? this.ResolveEntityName(identity) : prior.Name,
                PlayfieldId = this.sessionPlayfieldId,
                Kind = prior == null ? "Unknown" : prior.Kind,
                HasPosition = false
            };

            this.WriteVisibilityObservation(
                capturedUtc,
                elapsedMilliseconds,
                "raw-packet",
                "DESPAWN_PACKET",
                entity,
                null,
                this.previousVisibilityPlayer,
                null,
                direction,
                sequence,
                ordinal,
                "RawDespawnIdentityOnly");
        }

        private void CaptureAttackStart(
            DateTime capturedUtc,
            double elapsedMilliseconds,
            long ordinal,
            string direction,
            int sequence,
            byte[] packet)
        {
            if (packet.Length < 38 || packet[37] != 0)
            {
                return;
            }

            uint sourceType;
            uint sourceInstance;
            uint targetType;
            uint targetInstance;
            if (!TryReadIdentity(packet, 20, out sourceType, out sourceInstance)
                || !TryReadIdentity(packet, 29, out targetType, out targetInstance))
            {
                return;
            }

            this.attackStarts++;
            string sourceIdentity = FormatIdentity(sourceType, sourceInstance);
            string targetIdentity = FormatIdentity(targetType, targetInstance);
            this.RefreshPlayerIdentity();
            bool sourceIsPlayer = IdentityEquals(sourceIdentity, this.playerIdentity);
            bool playerPreviouslyAttackedSource = this.playerAttackTargets.Contains(sourceIdentity);
            if (sourceIsPlayer)
            {
                this.playerAttackTargets.Add(targetIdentity);
            }

            this.pendingAttacks.Add(
                new PendingAttack
                {
                    CapturedUtc = capturedUtc,
                    ElapsedMilliseconds = elapsedMilliseconds,
                    GlobalOrdinal = ordinal,
                    Direction = direction,
                    Sequence = sequence,
                    SourceIdentity = sourceIdentity,
                    TargetIdentity = targetIdentity,
                    PlayerPreviouslyAttackedSource = playerPreviouslyAttackedSource
                });

            if (this.attackStarts == 1)
            {
                this.WriteWorldSnapshot("first-attack");
                this.WritePlayerCombatContext("first-attack");
            }
        }

        private void CaptureCorpseFullUpdate(DateTime capturedUtc, byte[] packet)
        {
            this.corpseFullUpdatePackets++;
            if (packet.Length < 231)
            {
                return;
            }

            uint corpseType = ReadUInt32BigEndian(packet, 20);
            uint corpseInstance = ReadUInt32BigEndian(packet, 24);
            string corpseIdentity = FormatIdentity(corpseType, corpseInstance);
            int nameOffset = FindAscii(packet, "Remains of ");
            string name = string.Empty;
            int monsterDataOffset = -1;
            if (nameOffset >= 4)
            {
                int nameLength = ReadInt32BigEndian(packet, nameOffset - 4);
                if (nameLength > 0 && nameOffset + nameLength <= packet.Length)
                {
                    name = Encoding.ASCII.GetString(packet, nameOffset, nameLength).TrimEnd('\0');
                    monsterDataOffset = nameOffset + nameLength + 72;
                }
            }

            uint deadNpcType = ReadUInt32BigEndian(packet, 183);
            uint deadNpcInstance = ReadUInt32BigEndian(packet, 191);
            var corpse = new CorpseSnapshot
            {
                CapturedUtc = capturedUtc,
                CorpseType = corpseType,
                CorpseInstance = corpseInstance,
                CorpseIdentity = corpseIdentity,
                Name = name,
                PlayfieldId = ReadInt32BigEndian(packet, 73),
                X = ReadSingleBigEndian(packet, 45),
                Y = ReadSingleBigEndian(packet, 49),
                Z = ReadSingleBigEndian(packet, 53),
                DeadNpcIdentity = FormatIdentity(deadNpcType, deadNpcInstance),
                Credits = ReadInt32BigEndian(packet, 207),
                MonsterData = monsterDataOffset >= 0 && monsterDataOffset + 4 <= packet.Length
                                  ? ReadUInt32BigEndian(packet, monsterDataOffset)
                                  : 0
            };
            this.knownCorpses[corpseIdentity] = corpse;
            this.observedCorpseIdentities.Add(corpseIdentity);
            this.WriteWorldSnapshot("corpse-observed");
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
            float currentXValue = 0;
            float currentYValue = 0;
            float currentZValue = 0;

            if (decodedCoordinates > 0)
            {
                currentXValue = ReadSingleBigEndian(packet, coordinateOffset);
                currentYValue = ReadSingleBigEndian(packet, coordinateOffset + 4);
                currentZValue = ReadSingleBigEndian(packet, coordinateOffset + 8);
                currentX = FormatFloat(currentXValue);
                currentY = FormatFloat(currentYValue);
                currentZ = FormatFloat(currentZValue);
                int destinationOffset = coordinateOffset + (decodedCoordinates - 1) * 12;
                destinationX = FormatFloat(ReadSingleBigEndian(packet, destinationOffset));
                destinationY = FormatFloat(ReadSingleBigEndian(packet, destinationOffset + 4));
                destinationZ = FormatFloat(ReadSingleBigEndian(packet, destinationOffset + 8));
                this.UpdatePosition(
                    FormatIdentity(sourceType, sourceInstance),
                    capturedUtc,
                    "FollowTarget",
                    direction,
                    sequence,
                    currentXValue,
                    currentYValue,
                    currentZValue);
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
            float positionX = ReadSingleBigEndian(packet, 29);
            float positionY = ReadSingleBigEndian(packet, 33);
            float positionZ = ReadSingleBigEndian(packet, 37);
            this.UpdatePosition(
                FormatIdentity(sourceType, sourceInstance),
                capturedUtc,
                "SetPos",
                direction,
                sequence,
                positionX,
                positionY,
                positionZ);
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
                FormatFloat(positionX),
                FormatFloat(positionY),
                FormatFloat(positionZ),
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
            float positionX = ReadSingleBigEndian(packet, 46);
            float positionY = ReadSingleBigEndian(packet, 50);
            float positionZ = ReadSingleBigEndian(packet, 54);
            this.UpdatePosition(
                FormatIdentity(sourceType, sourceInstance),
                capturedUtc,
                "CharDCMove",
                direction,
                sequence,
                positionX,
                positionY,
                positionZ);
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
                FormatFloat(positionX),
                FormatFloat(positionY),
                FormatFloat(positionZ),
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

        private void UpdatePosition(
            string identity,
            DateTime capturedUtc,
            string messageType,
            string direction,
            int sequence,
            float x,
            float y,
            float z)
        {
            identity = NormalizeIdentity(identity);
            if (identity.Length == 0)
            {
                return;
            }

            PositionHistory history;
            if (!this.positions.TryGetValue(identity, out history))
            {
                history = new PositionHistory();
                this.positions[identity] = history;
            }

            var observation = new PositionObservation
            {
                CapturedUtc = capturedUtc,
                MessageType = messageType,
                Direction = direction,
                Sequence = sequence,
                X = x,
                Y = y,
                Z = z
            };
            if (history.Latest == null || !SamePosition(history.Latest, observation))
            {
                history.Previous = history.Latest;
            }

            history.Latest = observation;
        }

        private void ResolvePendingAttacks(DateTime now, bool force)
        {
            for (int index = this.pendingAttacks.Count - 1; index >= 0; index--)
            {
                PendingAttack attack = this.pendingAttacks[index];
                PositionHistory sourceHistory;
                PositionHistory targetHistory;
                this.positions.TryGetValue(attack.SourceIdentity, out sourceHistory);
                this.positions.TryGetValue(attack.TargetIdentity, out targetHistory);
                PositionObservation source = sourceHistory == null ? null : sourceHistory.Latest;
                PositionObservation target = targetHistory == null ? null : targetHistory.Latest;
                bool fresh = source != null
                             && target != null
                             && Math.Abs((source.CapturedUtc - attack.CapturedUtc).TotalSeconds) <= AggroPositionWindowSeconds
                             && Math.Abs((target.CapturedUtc - attack.CapturedUtc).TotalSeconds) <= AggroPositionWindowSeconds;
                bool expired = (now - attack.CapturedUtc).TotalSeconds >= AggroPositionWindowSeconds;
                if (!fresh && !force && !expired)
                {
                    continue;
                }

                PositionObservation previousTarget = targetHistory == null
                                                         ? null
                                                         : targetHistory.Previous;
                this.WriteAggroObservation(
                    attack,
                    source,
                    target,
                    previousTarget,
                    fresh
                        ? "positions-within-2s"
                        : source == null || target == null
                              ? "missing-position"
                              : "stale-position");
                this.pendingAttacks.RemoveAt(index);
            }
        }

        private void WriteAggroObservation(
            PendingAttack attack,
            PositionObservation source,
            PositionObservation target,
            PositionObservation previousTarget,
            string correlationStatus)
        {
            this.RefreshPlayerIdentity();
            bool sourceIsPlayer = IdentityEquals(attack.SourceIdentity, this.playerIdentity);
            bool targetIsPlayer = IdentityEquals(attack.TargetIdentity, this.playerIdentity);
            string initiatorRole = sourceIsPlayer
                                       ? "Player"
                                       : targetIsPlayer
                                             ? "NpcToPlayer"
                                             : "Other";
            double? triggerDistance = Distance(source, target);
            double? previousDistance = Distance(source, previousTarget);
            double? bracketMin = null;
            double? bracketMax = null;
            if (triggerDistance.HasValue && previousDistance.HasValue)
            {
                bracketMin = Math.Min(triggerDistance.Value, previousDistance.Value);
                bracketMax = Math.Max(triggerDistance.Value, previousDistance.Value);
            }

            this.aggroObservationRows++;
            if (targetIsPlayer && !sourceIsPlayer)
            {
                this.npcToPlayerAggroObservationRows++;
                if (!attack.PlayerPreviouslyAttackedSource)
                {
                    this.unprovokedNpcAggroObservationRows++;
                }
            }

            this.aggroObservationLog.WriteLine(
                string.Join(
                    ",",
                    Csv(attack.CapturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                    attack.ElapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
                    attack.GlobalOrdinal.ToString(CultureInfo.InvariantCulture),
                    Csv(attack.Direction),
                    attack.Sequence.ToString(CultureInfo.InvariantCulture),
                    Csv(attack.SourceIdentity),
                    Csv(this.ResolveEntityName(attack.SourceIdentity)),
                    Csv(attack.TargetIdentity),
                    Csv(this.ResolveEntityName(attack.TargetIdentity)),
                    Csv(initiatorRole),
                    Csv(attack.PlayerPreviouslyAttackedSource ? "true" : "false"),
                    Csv(FormatPositionUtc(source)),
                    Csv(source == null ? string.Empty : source.MessageType),
                    Csv(FormatPositionCoordinate(source, "X")),
                    Csv(FormatPositionCoordinate(source, "Y")),
                    Csv(FormatPositionCoordinate(source, "Z")),
                    Csv(FormatPositionUtc(target)),
                    Csv(target == null ? string.Empty : target.MessageType),
                    Csv(FormatPositionCoordinate(target, "X")),
                    Csv(FormatPositionCoordinate(target, "Y")),
                    Csv(FormatPositionCoordinate(target, "Z")),
                    Csv(FormatPositionUtc(previousTarget)),
                    Csv(FormatPositionCoordinate(previousTarget, "X")),
                    Csv(FormatPositionCoordinate(previousTarget, "Y")),
                    Csv(FormatPositionCoordinate(previousTarget, "Z")),
                    Csv(FormatNullableDouble(triggerDistance)),
                    Csv(FormatNullableDouble(previousDistance)),
                    Csv(FormatNullableDouble(bracketMin)),
                    Csv(FormatNullableDouble(bracketMax)),
                    Csv(source == null
                            ? string.Empty
                            : (source.CapturedUtc - attack.CapturedUtc).TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)),
                    Csv(target == null
                            ? string.Empty
                            : (target.CapturedUtc - attack.CapturedUtc).TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)),
                    Csv(correlationStatus)));

            if (targetIsPlayer && !sourceIsPlayer)
            {
                this.WriteWorldSnapshot("npc-aggro");
                this.WritePlayerCombatContext("npc-aggro");
            }
        }

        private void OnUpdate(object sender, float deltaTime)
        {
            bool finalized = false;
            string finalizedDirectory = string.Empty;
            string validationStatus = string.Empty;
            string validationSummary = string.Empty;
            string activeDirectory = string.Empty;
            lock (this.syncRoot)
            {
                DateTime now = DateTime.UtcNow;
                if (!this.enabled)
                {
                    if (this.automaticCaptureEnabled && !this.teardownRequested)
                    {
                        this.StartSession("automatic recovery");
                        activeDirectory = this.sessionDirectory;
                    }

                    return;
                }

                int currentPlayfieldId = GetPlayfieldIdNoThrow();
                bool playfieldChanged = !this.stopRequested
                                        && this.automaticCaptureEnabled
                                        && currentPlayfieldId != 0
                                        && currentPlayfieldId != this.sessionPlayfieldId;
                bool quiet = this.stopRequested
                             && (now - this.lastPacketUtc).TotalSeconds >= StopQuietSeconds;
                bool maximum = this.stopRequested
                               && (now - this.stopRequestedUtc).TotalSeconds >= StopMaximumSeconds;
                if (playfieldChanged || quiet || maximum)
                {
                    finalizedDirectory = this.sessionDirectory;
                    string reason = playfieldChanged
                                        ? "automatic playfield rotation"
                                        : quiet
                                              ? "quiet packet drain complete"
                                              : "maximum packet drain elapsed";
                    this.FinalizeSessionNoThrow(reason, !playfieldChanged);
                    validationStatus = this.lastValidationStatus;
                    validationSummary = this.lastValidationSummary;
                    finalized = true;
                    if (this.automaticCaptureEnabled && !this.teardownRequested)
                    {
                        this.StartSession(playfieldChanged
                                              ? "automatic playfield continuation"
                                              : "automatic post-finalize continuation");
                        activeDirectory = this.sessionDirectory;
                    }
                }
                else
                {
                    if (this.inventorySnapshotRequested)
                    {
                        this.inventorySnapshotRequested = false;
                        this.WriteInventorySnapshot("deferred-inventory-refresh");
                    }

                    if (now >= this.nextVisibilitySampleUtc)
                    {
                        this.CaptureLiveWorldSnapshot(now, "periodic-visibility", false);
                        this.nextVisibilitySampleUtc = now.AddMilliseconds(VisibilitySampleMilliseconds);
                    }

                    if (now >= this.nextCheckpointUtc)
                    {
                        this.ResolvePendingAttacks(now, false);
                        this.FlushNoThrow();
                        this.WriteCaptureInfo(false, "periodic crash-safe checkpoint");
                        this.nextCheckpointUtc = now.AddSeconds(CheckpointSeconds);
                    }
                }
            }

            if (finalized)
            {
                ChatColor color = string.Equals(validationStatus, "PASS", StringComparison.OrdinalIgnoreCase)
                                      ? ChatColor.Gold
                                      : ChatColor.Red;
                Chat.WriteLine(
                    "AO capture finalized: " + finalizedDirectory
                    + " | validation=" + validationStatus
                    + (validationSummary.Length == 0 ? string.Empty : " | " + validationSummary),
                    color);
                if (activeDirectory.Length > 0)
                {
                    Chat.WriteLine("AO automatic capture continued: " + activeDirectory, ChatColor.Gold);
                }
            }
        }

        private void FinalizeSessionNoThrow(string reason)
        {
            this.FinalizeSessionNoThrow(reason, true);
        }

        private void FinalizeSessionNoThrow(string reason, bool captureFinalContext)
        {
            if (!this.enabled
                && this.packetLog == null
                && this.rawPacketLog == null
                && this.movementPacketLog == null
                && this.scfuAppearanceLog == null
                && this.worldSnapshotLog == null
                && this.playerCombatContextLog == null
                && this.aggroObservationLog == null
                && this.visibilityObservationLog == null
                && this.inventorySnapshotLog == null
                && this.itemUseObservationLog == null
                && this.eventLog == null)
            {
                return;
            }

            this.enabled = false;
            this.stopRequested = false;
            this.captureClock.Stop();
            this.ResolvePendingAttacks(DateTime.UtcNow, true);
            if (captureFinalContext)
            {
                this.WriteWorldSnapshot("capture-end");
                this.WritePlayerCombatContext("capture-end");
                this.WriteInventorySnapshot("capture-end");
            }
            else
            {
                this.WriteEvent(
                    "FINAL-CONTEXT-SKIPPED",
                    "Playfield rotation detected; new-playfield dynels are excluded from the old session.");
            }
            this.ValidateSession();
            this.WriteEvent("FINALIZE", reason);
            this.FlushNoThrow();
            this.WriteMovementSummaryNoThrow();
            this.WriteCaptureInfo(true, reason);
            this.packetLog = CloseWriter(this.packetLog);
            this.rawPacketLog = CloseWriter(this.rawPacketLog);
            this.movementPacketLog = CloseWriter(this.movementPacketLog);
            this.scfuAppearanceLog = CloseWriter(this.scfuAppearanceLog);
            this.worldSnapshotLog = CloseWriter(this.worldSnapshotLog);
            this.playerCombatContextLog = CloseWriter(this.playerCombatContextLog);
            this.aggroObservationLog = CloseWriter(this.aggroObservationLog);
            this.visibilityObservationLog = CloseWriter(this.visibilityObservationLog);
            this.inventorySnapshotLog = CloseWriter(this.inventorySnapshotLog);
            this.itemUseObservationLog = CloseWriter(this.itemUseObservationLog);
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
            json.AppendLine("  \"captureMode\": \"aosharp-mike-2022-raw-compatible\",");
            json.AppendLine("  \"automaticCaptureEnabled\": " + (this.automaticCaptureEnabled ? "true" : "false") + ",");
            json.AppendLine("  \"sessionAreaName\": " + Json(this.sessionAreaName) + ",");
            json.AppendLine("  \"sessionPlayfieldId\": " + this.sessionPlayfieldId.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("  \"captureStartUtc\": \"" + this.captureStartUtc.ToString("o", CultureInfo.InvariantCulture) + "\",");
            json.AppendLine("  \"checkpointUtc\": \"" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\",");
            if (finalized)
            {
                json.AppendLine("  \"captureFinalizedUtc\": \"" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\",");
            }

            json.AppendLine("  \"inboundRaw\": " + this.inboundSequence.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("  \"outboundRaw\": " + this.outboundSequence.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("  \"rawPacketWriteErrors\": " + this.rawWriteErrors.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("  \"rawMissingPackets\": " + this.rawMissingPackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("  \"checkpointWriteErrors\": " + this.checkpointWriteErrors.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("  \"movementProjection\": {");
            json.AppendLine("    \"rows\": " + this.movementPacketRows.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"followTargetPackets\": " + this.movementFollowTargetPackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"usableFollowTargetPackets\": " + this.movementUsableFollowTargetPackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"setPosPackets\": " + this.movementSetPosPackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"stopMovingCmdPackets\": " + this.movementStopMovingCmdPackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"charDCMovePackets\": " + this.movementCharDcMovePackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"decodeErrors\": " + this.movementDecodeErrors.ToString(CultureInfo.InvariantCulture));
            json.AppendLine("  },");
            json.AppendLine("  \"evidenceProjection\": {");
            json.AppendLine("    \"scfuPackets\": " + this.scfuPackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"scfuDecodeErrors\": " + this.scfuDecodeErrors.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"worldSnapshotRows\": " + this.worldSnapshotRows.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"worldSnapshotErrors\": " + this.worldSnapshotErrors.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"playerCombatContextRows\": " + this.playerCombatContextRows.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"playerCombatContextCompleteRows\": " + this.playerCombatContextCompleteRows.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"playerCombatContextErrors\": " + this.playerCombatContextErrors.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"attackStarts\": " + this.attackStarts.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"attackInfoPackets\": " + this.attackInfoPackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"missedAttackPackets\": " + this.missedAttackPackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"deathActions\": " + this.deathActions.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"corpseFullUpdatePackets\": " + this.corpseFullUpdatePackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"inventoryUpdatePackets\": " + this.inventoryUpdatePackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"identityLinkedInventoryPackets\": " + this.identityLinkedInventoryPackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"aggroObservationRows\": " + this.aggroObservationRows.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"npcToPlayerAggroObservationRows\": " + this.npcToPlayerAggroObservationRows.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"unprovokedNpcAggroObservationRows\": " + this.unprovokedNpcAggroObservationRows.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"visibilitySamples\": " + this.visibilitySamples.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"visibilityObservationRows\": " + this.visibilityObservationRows.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"visibilityTransitionRows\": " + this.visibilityTransitionRows.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"visibilitySampleErrors\": " + this.visibilitySampleErrors.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"despawnPackets\": " + this.despawnPackets.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"lineOfSightStateObservations\": " + this.lineOfSightStateObservations.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"lineOfSightTransitionRows\": " + this.lineOfSightTransitionRows.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"lineOfSightReadErrors\": " + this.lineOfSightReadErrors.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"inPlayStateObservations\": " + this.inPlayStateObservations.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"inPlayTransitionRows\": " + this.inPlayTransitionRows.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"inPlayReadErrors\": " + this.inPlayReadErrors.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"inventorySnapshotRows\": " + this.inventorySnapshotRows.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"inventorySnapshotErrors\": " + this.inventorySnapshotErrors.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"itemUseRequests\": " + this.itemUseRequests.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"resolvedItemUseRequests\": " + this.resolvedItemUseRequests.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"itemUseAcknowledgements\": " + this.itemUseAcknowledgements.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"deleteItemActions\": " + this.deleteItemActions.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"correlatedDeleteItems\": " + this.correlatedDeleteItems.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"itemUseObservationRows\": " + this.itemUseObservationRows.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"itemUseObservationErrors\": " + this.itemUseObservationErrors.ToString(CultureInfo.InvariantCulture));
            json.AppendLine("  },");
            json.AppendLine("  \"validationStatus\": " + Json(this.lastValidationStatus) + ",");
            json.AppendLine("  \"validationSummary\": " + Json(this.lastValidationSummary) + ",");
            json.AppendLine("  \"processingAllowed\": true,");
            json.AppendLine("  \"offlineDecodeRequired\": true,");
            json.AppendLine("  \"recaptureRequired\": " + (this.RawRecaptureRequired() ? "true" : "false") + ",");
            json.AppendLine("  \"finalized\": " + (finalized ? "true" : "false") + ",");
            json.AppendLine("  \"detail\": " + Json(reason));
            json.AppendLine("}");
            try
            {
                WriteAllTextAtomically(path, json.ToString());
            }
            catch (Exception ex)
            {
                this.checkpointWriteErrors++;
                this.WriteFallbackError("CaptureInfo", ex);
            }
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
                WriteAllTextAtomically(path, json.ToString());
            }
            catch (Exception ex)
            {
                this.WriteFallbackError("MovementSummary", ex);
            }
        }

        private void WriteWorldSnapshot(string phase)
        {
            if (this.worldSnapshotLog == null)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            this.CaptureLiveWorldSnapshot(now, phase, true);
            foreach (EntitySnapshot entity in this.knownEntities.Values)
            {
                if ((now - entity.CapturedUtc).TotalMinutes > 10)
                {
                    continue;
                }

                if (this.sessionPlayfieldId != 0
                    && entity.PlayfieldId != 0
                    && entity.PlayfieldId != this.sessionPlayfieldId)
                {
                    continue;
                }

                this.worldSnapshotRows++;
                this.worldSnapshotLog.WriteLine(
                    string.Join(
                        ",",
                        Csv(now.ToString("o", CultureInfo.InvariantCulture)),
                        Csv(phase),
                        Csv(entity.IsNpc ? "NPC" : "Character"),
                        Csv(entity.Identity),
                        Csv(entity.Name),
                        entity.PlayfieldId.ToString(CultureInfo.InvariantCulture),
                        FormatFloat(entity.X),
                        FormatFloat(entity.Y),
                        FormatFloat(entity.Z),
                        FormatFloat(entity.HeadingX),
                        FormatFloat(entity.HeadingY),
                        FormatFloat(entity.HeadingZ),
                        FormatFloat(entity.HeadingW),
                        entity.Level.ToString(CultureInfo.InvariantCulture),
                        entity.Health.ToString(CultureInfo.InvariantCulture),
                        entity.HealthDamage.ToString(CultureInfo.InvariantCulture),
                        entity.MonsterData.ToString(CultureInfo.InvariantCulture),
                        Csv(string.Empty),
                        string.Empty,
                        Csv(entity.CapturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                        Csv("RawSimpleCharFullUpdate")));
            }

            foreach (CorpseSnapshot corpse in this.knownCorpses.Values)
            {
                if ((now - corpse.CapturedUtc).TotalMinutes > 10)
                {
                    continue;
                }

                if (this.sessionPlayfieldId != 0
                    && corpse.PlayfieldId != 0
                    && corpse.PlayfieldId != this.sessionPlayfieldId)
                {
                    continue;
                }

                this.worldSnapshotRows++;
                this.worldSnapshotLog.WriteLine(
                    string.Join(
                        ",",
                        Csv(now.ToString("o", CultureInfo.InvariantCulture)),
                        Csv(phase),
                        Csv("Corpse"),
                        Csv(corpse.CorpseIdentity),
                        Csv(corpse.Name),
                        corpse.PlayfieldId.ToString(CultureInfo.InvariantCulture),
                        FormatFloat(corpse.X),
                        FormatFloat(corpse.Y),
                        FormatFloat(corpse.Z),
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        corpse.MonsterData.ToString(CultureInfo.InvariantCulture),
                        Csv(corpse.DeadNpcIdentity),
                        corpse.Credits.ToString(CultureInfo.InvariantCulture),
                        Csv(corpse.CapturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                        Csv("RawCorpseFullUpdate")));
            }
        }

        private void CaptureLiveWorldSnapshot(DateTime capturedUtc, string phase, bool writeWorldRows)
        {
            var currentVisibleDynels =
                new Dictionary<string, EntitySnapshot>(StringComparer.OrdinalIgnoreCase);
            EntitySnapshot currentPlayer = null;
            bool visibilitySampleComplete = true;
            try
            {
                this.RefreshPlayerIdentity();
                IEnumerable dynels = DynelManager.AllDynels;
                if (dynels == null)
                {
                    throw new InvalidOperationException("DynelManager.AllDynels is unavailable.");
                }

                IEnumerable characters = DynelManager.Characters;
                if (characters == null)
                {
                    throw new InvalidOperationException("DynelManager.Characters is unavailable.");
                }

                var charactersByIdentity =
                    new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (object character in characters)
                {
                    try
                    {
                        string characterIdentity = NormalizeIdentity(MemberText(character, "Identity"));
                        if (characterIdentity.Length == 0)
                        {
                            visibilitySampleComplete = false;
                            continue;
                        }

                        charactersByIdentity[characterIdentity] = character;
                    }
                    catch (Exception ex)
                    {
                        visibilitySampleComplete = false;
                        this.worldSnapshotErrors++;
                        this.WriteFallbackError("WorldSnapshot.Character", ex);
                    }
                }

                foreach (object dynel in dynels)
                {
                    try
                    {
                        string identity = NormalizeIdentity(MemberText(dynel, "Identity"));
                        object character = null;
                        bool isCharacter = identity.Length > 0
                                           && charactersByIdentity.TryGetValue(identity, out character);
                        object evidenceDynel = isCharacter ? character : dynel;
                        string evidenceSource = isCharacter ? "AOSharpSimpleChar" : "AOSharpDynel";
                        string name = MemberText(evidenceDynel, "Name");
                        object position = MemberValue(evidenceDynel, "Position");
                        object rotation = MemberValue(evidenceDynel, "Rotation");
                        string positionX = MemberNumber(position, "X");
                        string positionY = MemberNumber(position, "Y");
                        string positionZ = MemberNumber(position, "Z");
                        string level = NamedStat(evidenceDynel, "Level");
                        string health = NamedStat(evidenceDynel, "Health");
                        string monsterData = NamedStat(evidenceDynel, "MonsterData");
                        bool isNpc;
                        bool isPlayer;
                        bool.TryParse(MemberText(evidenceDynel, "IsNpc"), out isNpc);
                        bool.TryParse(MemberText(evidenceDynel, "IsPlayer"), out isPlayer);
                        string runtimeType = evidenceDynel == null ? string.Empty : evidenceDynel.GetType().Name;
                        string kind = runtimeType.IndexOf("Corpse", StringComparison.OrdinalIgnoreCase) >= 0
                                          ? "Corpse"
                                          : isNpc
                                                ? "NPC"
                                                : isPlayer
                                                      ? "Player"
                                                      : runtimeType;
                        bool isLocalCharacter = isCharacter
                                                && IdentityEquals(identity, this.playerIdentity);
                        bool lineOfSight = false;
                        bool hasLineOfSight = isCharacter
                                              && !isLocalCharacter
                                              && this.TryReadLineOfSight(character, out lineOfSight);

                        bool isInPlay = false;
                        bool hasInPlay = isCharacter
                                         && !isLocalCharacter
                                         && this.TryReadInPlay(character, out isInPlay);

                        if (writeWorldRows)
                        {
                            this.worldSnapshotRows++;
                            this.worldSnapshotLog.WriteLine(
                                string.Join(
                                    ",",
                                    Csv(capturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                                    Csv(phase),
                                    Csv(kind),
                                    Csv(identity),
                                    Csv(name),
                                    this.sessionPlayfieldId.ToString(CultureInfo.InvariantCulture),
                                    Csv(positionX),
                                    Csv(positionY),
                                    Csv(positionZ),
                                    Csv(MemberNumber(rotation, "X")),
                                    Csv(MemberNumber(rotation, "Y")),
                                    Csv(MemberNumber(rotation, "Z")),
                                    Csv(MemberNumber(rotation, "W")),
                                    Csv(level),
                                    Csv(health),
                                    Csv(string.Empty),
                                    Csv(monsterData),
                                    Csv(string.Empty),
                                    Csv(string.Empty),
                                    Csv(capturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                                    Csv(evidenceSource)));
                        }

                        if (identity.Length == 0)
                        {
                            visibilitySampleComplete = false;
                            continue;
                        }

                        float x;
                        float y;
                        float z;
                        if (float.TryParse(positionX, NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                            && float.TryParse(positionY, NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                            && float.TryParse(positionZ, NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                        {
                            int parsedLevel;
                            int parsedHealth;
                            uint parsedMonsterData;
                            int.TryParse(level, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedLevel);
                            int.TryParse(health, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedHealth);
                            uint.TryParse(monsterData, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedMonsterData);
                            var visible = new EntitySnapshot
                            {
                                CapturedUtc = capturedUtc,
                                Identity = identity,
                                Name = name,
                                PlayfieldId = this.sessionPlayfieldId,
                                X = x,
                                Y = y,
                                Z = z,
                                Level = parsedLevel,
                                Health = parsedHealth,
                                MonsterData = parsedMonsterData,
                                Kind = kind,
                                HasPosition = true,
                                HasLineOfSight = hasLineOfSight,
                                IsInLineOfSight = lineOfSight,
                                HasInPlay = hasInPlay,
                                IsInPlay = isInPlay,
                                IsNpc = isNpc,
                                IsPlayer = isPlayer
                            };
                            currentVisibleDynels[identity] = visible;
                            if (IdentityEquals(identity, this.playerIdentity))
                            {
                                currentPlayer = visible;
                            }

                            this.UpdatePosition(
                                identity,
                                capturedUtc,
                                evidenceSource,
                                "LOCAL",
                                0,
                                x,
                                y,
                                z);
                        }
                        else
                        {
                            visibilitySampleComplete = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        visibilitySampleComplete = false;
                        this.worldSnapshotErrors++;
                        this.WriteFallbackError("WorldSnapshot.Dynel", ex);
                    }
                }

                if (!visibilitySampleComplete || currentPlayer == null)
                {
                    this.visibilitySampleErrors++;
                    this.WriteEvent(
                        "VISIBILITY-SAMPLE-SKIPPED",
                        "Incomplete dynel set or local player unavailable during " + phase + ".");
                }
                else
                {
                    this.TrackVisibilitySample(capturedUtc, phase, currentVisibleDynels, currentPlayer);
                }
            }
            catch (Exception ex)
            {
                this.visibilitySampleErrors++;
                this.worldSnapshotErrors++;
                this.WriteFallbackError("WorldSnapshot.AllDynels", ex);
            }

        }

        private void TrackVisibilitySample(
            DateTime capturedUtc,
            string phase,
            Dictionary<string, EntitySnapshot> currentVisibleDynels,
            EntitySnapshot currentPlayer)
        {
            if (this.visibilityObservationLog == null)
            {
                return;
            }

            try
            {
                this.visibilitySamples++;
                bool baseline = this.previousVisibilitySampleUtc == DateTime.MinValue;
                foreach (KeyValuePair<string, EntitySnapshot> pair in currentVisibleDynels)
                {
                    EntitySnapshot previousEntity = null;
                    bool hasPrevious = !baseline
                                       && this.previousVisibleDynels.TryGetValue(pair.Key, out previousEntity);
                    bool isLocalPlayer = IdentityEquals(pair.Key, this.playerIdentity);
                    if (!isLocalPlayer && (pair.Value.HasLineOfSight || pair.Value.HasInPlay))
                    {
                        this.WriteVisibilityObservation(
                            capturedUtc,
                            this.captureClock.Elapsed.TotalMilliseconds,
                            phase,
                            "CLIENT_STATE",
                            pair.Value,
                            currentPlayer,
                            this.previousVisibilityPlayer,
                            hasPrevious ? previousEntity : null,
                            string.Empty,
                            0,
                            0,
                            "AOSharpSimpleChar.ClientState");

                        if (pair.Value.HasLineOfSight)
                        {
                            this.lineOfSightStateObservations++;
                        }

                        if (pair.Value.HasInPlay)
                        {
                            this.inPlayStateObservations++;
                        }
                    }

                    if (hasPrevious)
                    {
                        if (pair.Value.HasLineOfSight
                            && previousEntity.HasLineOfSight
                            && pair.Value.IsInLineOfSight != previousEntity.IsInLineOfSight)
                        {
                            this.WriteVisibilityObservation(
                                capturedUtc,
                                this.captureClock.Elapsed.TotalMilliseconds,
                                phase,
                                pair.Value.IsInLineOfSight ? "LOS_GAINED" : "LOS_LOST",
                                pair.Value,
                                currentPlayer,
                                this.previousVisibilityPlayer,
                                previousEntity,
                                string.Empty,
                                0,
                                0,
                                "AOSharpSimpleChar.IsInLineOfSight");
                        }

                        if (pair.Value.HasInPlay
                            && previousEntity.HasInPlay
                            && pair.Value.IsInPlay != previousEntity.IsInPlay)
                        {
                            this.WriteVisibilityObservation(
                                capturedUtc,
                                this.captureClock.Elapsed.TotalMilliseconds,
                                phase,
                                pair.Value.IsInPlay ? "INPLAY_GAINED" : "INPLAY_LOST",
                                pair.Value,
                                currentPlayer,
                                this.previousVisibilityPlayer,
                                previousEntity,
                                string.Empty,
                                0,
                                0,
                                "AOSharpSimpleChar.IsInPlay");
                        }

                        continue;
                    }

                    this.WriteVisibilityObservation(
                        capturedUtc,
                        this.captureClock.Elapsed.TotalMilliseconds,
                        phase,
                        baseline ? "BASELINE" : "APPEARED",
                        pair.Value,
                        currentPlayer,
                        this.previousVisibilityPlayer,
                        null,
                        string.Empty,
                        0,
                        0,
                        "AOSharpDynelTransition");
                }

                if (!baseline)
                {
                    foreach (KeyValuePair<string, EntitySnapshot> pair in this.previousVisibleDynels)
                    {
                        if (currentVisibleDynels.ContainsKey(pair.Key))
                        {
                            continue;
                        }

                        var disappeared = new EntitySnapshot
                        {
                            CapturedUtc = capturedUtc,
                            Identity = pair.Value.Identity,
                            Name = pair.Value.Name,
                            PlayfieldId = pair.Value.PlayfieldId,
                            Kind = pair.Value.Kind,
                            HasPosition = false,
                            IsNpc = pair.Value.IsNpc,
                            IsPlayer = pair.Value.IsPlayer
                        };
                        this.WriteVisibilityObservation(
                            capturedUtc,
                            this.captureClock.Elapsed.TotalMilliseconds,
                            phase,
                            "DISAPPEARED",
                            disappeared,
                            currentPlayer,
                            this.previousVisibilityPlayer,
                            pair.Value,
                            string.Empty,
                            0,
                            0,
                            "AOSharpDynelTransition");
                    }
                }

                this.previousVisibleDynels.Clear();
                foreach (KeyValuePair<string, EntitySnapshot> pair in currentVisibleDynels)
                {
                    this.previousVisibleDynels[pair.Key] = pair.Value;
                }

                this.previousVisibilityPlayer = currentPlayer;
                this.previousVisibilitySampleUtc = capturedUtc;
            }
            catch (Exception ex)
            {
                this.visibilitySampleErrors++;
                this.WriteFallbackError("VisibilitySample", ex);
            }
        }

        private bool TryReadLineOfSight(object dynel, out bool value)
        {
            return this.TryReadClientBooleanState(
                dynel,
                "IsInLineOfSight",
                "LineOfSight",
                ref this.lineOfSightReadErrors,
                out value);
        }

        private bool TryReadInPlay(object dynel, out bool value)
        {
            return this.TryReadClientBooleanState(
                dynel,
                "IsInPlay",
                "InPlay",
                ref this.inPlayReadErrors,
                out value);
        }

        private bool TryReadClientBooleanState(
            object dynel,
            string propertyName,
            string errorCategory,
            ref int readErrors,
            out bool value)
        {
            value = false;
            if (dynel == null)
            {
                readErrors++;
                return false;
            }

            try
            {
                PropertyInfo property = dynel.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property == null || property.GetIndexParameters().Length != 0)
                {
                    readErrors++;
                    if (readErrors <= 5)
                    {
                        this.WriteEvent(
                            "CLIENT-STATE-READ-ERROR",
                            propertyName + " is unavailable on " + dynel.GetType().FullName + ".");
                    }

                    return false;
                }

                object raw = property.GetValue(dynel, null);
                if (raw is bool)
                {
                    value = (bool)raw;
                    return true;
                }

                if (raw != null && bool.TryParse(raw.ToString(), out value))
                {
                    return true;
                }

                readErrors++;
                return false;
            }
            catch (Exception ex)
            {
                readErrors++;
                if (readErrors <= 5)
                {
                    this.WriteFallbackError(errorCategory, ex);
                }

                return false;
            }
        }

        private void WriteVisibilityObservation(
            DateTime capturedUtc,
            double elapsedMilliseconds,
            string phase,
            string observation,
            EntitySnapshot entity,
            EntitySnapshot currentPlayer,
            EntitySnapshot previousPlayer,
            EntitySnapshot previousEntity,
            string direction,
            int sequence,
            long globalOrdinal,
            string evidenceSource)
        {
            if (this.visibilityObservationLog == null || entity == null)
            {
                return;
            }

            double? distance = Distance(currentPlayer, entity);
            double? previousDistance = Distance(previousPlayer, previousEntity);
            this.visibilityObservationRows++;
            if (string.Equals(observation, "APPEARED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(observation, "DISAPPEARED", StringComparison.OrdinalIgnoreCase))
            {
                this.visibilityTransitionRows++;
            }
            else if (string.Equals(observation, "LOS_GAINED", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(observation, "LOS_LOST", StringComparison.OrdinalIgnoreCase))
            {
                this.lineOfSightTransitionRows++;
            }
            else if (string.Equals(observation, "INPLAY_GAINED", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(observation, "INPLAY_LOST", StringComparison.OrdinalIgnoreCase))
            {
                this.inPlayTransitionRows++;
            }

            this.visibilityObservationLog.WriteLine(
                string.Join(
                    ",",
                    Csv(capturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                    elapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
                    this.visibilitySamples.ToString(CultureInfo.InvariantCulture),
                    Csv(phase),
                    Csv(observation),
                    Csv(entity.Identity),
                    Csv(entity.Name),
                    Csv(entity.Kind),
                    entity.PlayfieldId.ToString(CultureInfo.InvariantCulture),
                    FormatEntityCoordinate(currentPlayer, "X"),
                    FormatEntityCoordinate(currentPlayer, "Y"),
                    FormatEntityCoordinate(currentPlayer, "Z"),
                    FormatEntityCoordinate(entity, "X"),
                    FormatEntityCoordinate(entity, "Y"),
                    FormatEntityCoordinate(entity, "Z"),
                    FormatDistance(distance),
                    this.previousVisibilitySampleUtc == DateTime.MinValue
                        ? string.Empty
                        : Csv(this.previousVisibilitySampleUtc.ToString("o", CultureInfo.InvariantCulture)),
                    FormatEntityCoordinate(previousPlayer, "X"),
                    FormatEntityCoordinate(previousPlayer, "Y"),
                    FormatEntityCoordinate(previousPlayer, "Z"),
                    FormatEntityCoordinate(previousEntity, "X"),
                    FormatEntityCoordinate(previousEntity, "Y"),
                    FormatEntityCoordinate(previousEntity, "Z"),
                    FormatDistance(previousDistance),
                    FormatLineOfSight(entity),
                    FormatLineOfSight(previousEntity),
                    FormatInPlay(entity),
                    FormatInPlay(previousEntity),
                    Csv(direction),
                    sequence == 0 ? string.Empty : sequence.ToString(CultureInfo.InvariantCulture),
                    globalOrdinal == 0 ? string.Empty : globalOrdinal.ToString(CultureInfo.InvariantCulture),
                    Csv(evidenceSource)));
        }

        private void WritePlayerCombatContext(string phase)
        {
            if (this.playerCombatContextLog == null)
            {
                return;
            }

            DateTime capturedUtc = DateTime.UtcNow;
            string identity = string.Empty;
            string name = string.Empty;
            string positionX = string.Empty;
            string positionY = string.Empty;
            string positionZ = string.Empty;
            string error = string.Empty;
            object localPlayer = null;
            try
            {
                localPlayer = DynelManager.LocalPlayer;
                if (localPlayer == null)
                {
                    throw new InvalidOperationException("DynelManager.LocalPlayer is unavailable.");
                }

                identity = NormalizeIdentity(MemberText(localPlayer, "Identity"));
                name = MemberText(localPlayer, "Name");
                object position = MemberValue(localPlayer, "Position");
                positionX = MemberNumber(position, "X");
                positionY = MemberNumber(position, "Y");
                positionZ = MemberNumber(position, "Z");
                if (identity.Length > 0)
                {
                    this.playerIdentity = identity;
                }

                float x;
                float y;
                float z;
                if (float.TryParse(positionX, NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                    && float.TryParse(positionY, NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                    && float.TryParse(positionZ, NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                {
                    this.UpdatePosition(
                        identity,
                        capturedUtc,
                        "PlayerCombatContext",
                        "LOCAL",
                        0,
                        x,
                        y,
                        z);
                }
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + OneLine(ex.Message);
                this.playerCombatContextErrors++;
            }

            string level = NamedStat(localPlayer, "Level");
            string profession = NamedStat(localPlayer, "Profession");
            string health = NamedStat(localPlayer, "Health");
            string maxHealth = NamedStat(localPlayer, "MaxHealth");
            string runSpeed = NamedStat(localPlayer, "RunSpeed");
            if (runSpeed.Length == 0)
            {
                runSpeed = NamedStat(localPlayer, "Runspeed");
            }

            string evade = NamedStat(localPlayer, "EvadeClsC");
            string dodge = NamedStat(localPlayer, "DodgeRanged");
            string duck = NamedStat(localPlayer, "DuckExp");
            string meleeAc = NamedStat(localPlayer, "MeleeAC");
            string projectileAc = NamedStat(localPlayer, "ProjectileAC");
            string energyAc = NamedStat(localPlayer, "EnergyAC");
            string chemicalAc = NamedStat(localPlayer, "ChemicalAC");
            string radiationAc = NamedStat(localPlayer, "RadiationAC");
            string coldAc = NamedStat(localPlayer, "ColdAC");
            string poisonAc = NamedStat(localPlayer, "PoisonAC");
            string fireAc = NamedStat(localPlayer, "FireAC");
            object activeNanos = MemberValue(localPlayer, "ActiveNanos")
                                 ?? MemberValue(localPlayer, "Buffs");
            object equipment = MemberValue(localPlayer, "Equipment")
                               ?? MemberValue(localPlayer, "Weapons");
            bool complete = identity.Length > 0
                            && name.Length > 0
                            && positionX.Length > 0
                            && positionY.Length > 0
                            && positionZ.Length > 0
                            && level.Length > 0
                            && profession.Length > 0
                            && health.Length > 0
                            && maxHealth.Length > 0
                            && runSpeed.Length > 0
                            && evade.Length > 0
                            && dodge.Length > 0
                            && duck.Length > 0
                            && meleeAc.Length > 0
                            && projectileAc.Length > 0
                            && energyAc.Length > 0
                            && chemicalAc.Length > 0
                            && radiationAc.Length > 0
                            && coldAc.Length > 0
                            && poisonAc.Length > 0
                            && fireAc.Length > 0;
            if (complete)
            {
                this.playerCombatContextCompleteRows++;
            }
            else if (error.Length == 0)
            {
                error = "Required player identity, position, profession, health, speed, evade, or armor fields were unavailable.";
                this.playerCombatContextErrors++;
            }

            this.playerCombatContextRows++;
            this.playerCombatContextLog.WriteLine(
                string.Join(
                    ",",
                    Csv(capturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                    Csv(phase),
                    Csv(identity),
                    Csv(name),
                    Csv(level),
                    Csv(profession),
                    Csv(positionX),
                    Csv(positionY),
                    Csv(positionZ),
                    Csv(health),
                    Csv(maxHealth),
                    Csv(runSpeed),
                    Csv(evade),
                    Csv(dodge),
                    Csv(duck),
                    Csv(meleeAc),
                    Csv(projectileAc),
                    Csv(energyAc),
                    Csv(chemicalAc),
                    Csv(radiationAc),
                    Csv(coldAc),
                    Csv(poisonAc),
                    Csv(fireAc),
                    Csv(CollectionText(activeNanos)),
                    Csv(CollectionText(equipment)),
                    Csv(error)));
        }

        private void ValidateSession()
        {
            var issues = new List<string>();
            if (this.rawWriteErrors > 0)
            {
                issues.Add("raw packet write errors=" + this.rawWriteErrors.ToString(CultureInfo.InvariantCulture));
            }

            if (this.rawMissingPackets > 0)
            {
                issues.Add("packet callbacks with missing raw payload=" + this.rawMissingPackets.ToString(CultureInfo.InvariantCulture));
            }

            if (this.inboundSequence + this.outboundSequence == 0)
            {
                issues.Add("no raw packet stream");
            }

            if (this.checkpointWriteErrors > 0)
            {
                issues.Add("capture checkpoint write errors=" + this.checkpointWriteErrors.ToString(CultureInfo.InvariantCulture));
            }

            if (this.scfuPackets == 0)
            {
                issues.Add("no SCFU identity/spawn evidence");
            }
            else if (this.scfuDecodeErrors > 0)
            {
                issues.Add("SCFU decode errors=" + this.scfuDecodeErrors.ToString(CultureInfo.InvariantCulture));
            }

            if (this.worldSnapshotRows == 0)
            {
                issues.Add("no immediate or event world snapshot rows");
            }
            else if (this.worldSnapshotErrors > 0)
            {
                issues.Add("world snapshot errors=" + this.worldSnapshotErrors.ToString(CultureInfo.InvariantCulture));
            }

            if (this.visibilitySamples < 2)
            {
                issues.Add("insufficient periodic visibility samples=" + this.visibilitySamples.ToString(CultureInfo.InvariantCulture));
            }
            else if (this.visibilitySampleErrors > 0)
            {
                issues.Add("visibility sample errors=" + this.visibilitySampleErrors.ToString(CultureInfo.InvariantCulture));
            }

            if (this.lineOfSightStateObservations == 0)
            {
                issues.Add("no non-local AOSharp line-of-sight state observations");
            }
            else if (this.lineOfSightReadErrors > 0)
            {
                issues.Add("line-of-sight read errors=" + this.lineOfSightReadErrors.ToString(CultureInfo.InvariantCulture));
            }

            if (this.inPlayStateObservations == 0)
            {
                issues.Add("no non-local AOSharp in-play state observations");
            }
            else if (this.inPlayReadErrors > 0)
            {
                issues.Add("in-play read errors=" + this.inPlayReadErrors.ToString(CultureInfo.InvariantCulture));
            }

            if (this.movementPacketRows == 0)
            {
                issues.Add("no movement evidence");
            }

            if (this.attackStarts == 0 && this.attackInfoPackets == 0 && this.missedAttackPackets == 0)
            {
                issues.Add("no combat evidence");
            }
            else if (this.attackStarts == 0)
            {
                issues.Add("combat damage observed without an attack-start boundary");
            }
            else if (this.aggroObservationRows == 0)
            {
                issues.Add("attack observed without aggro position correlation");
            }
            else if (this.npcToPlayerAggroObservationRows == 0)
            {
                issues.Add("no NPC-to-player attack boundary for aggro observation");
            }
            else if (this.unprovokedNpcAggroObservationRows == 0)
            {
                issues.Add("NPC-to-player attacks followed player initiation; unprovoked aggro range is not proven");
            }

            if (this.deathActions == 0 && this.corpseFullUpdatePackets == 0)
            {
                issues.Add("no death/corpse evidence");
            }

            if (this.inventoryUpdatePackets == 0)
            {
                issues.Add("no loot inventory evidence");
            }
            else if (this.identityLinkedInventoryPackets == 0)
            {
                issues.Add("loot inventory is not linked to an observed corpse identity");
            }

            if (this.playerCombatContextRows == 0
                || this.playerCombatContextCompleteRows == 0
                || this.playerCombatContextErrors > 0)
            {
                issues.Add("player combat context incomplete");
            }

            if (this.inventorySnapshotRows == 0)
            {
                issues.Add("no player inventory snapshot rows");
            }
            else if (this.inventorySnapshotErrors > 0)
            {
                issues.Add("player inventory snapshot errors=" + this.inventorySnapshotErrors.ToString(CultureInfo.InvariantCulture));
            }

            if (this.itemUseRequests > this.resolvedItemUseRequests)
            {
                issues.Add(
                    "unresolved item-use slot identities="
                    + (this.itemUseRequests - this.resolvedItemUseRequests).ToString(CultureInfo.InvariantCulture));
            }

            if (this.itemUseObservationErrors > 0)
            {
                issues.Add("item-use observation write errors=" + this.itemUseObservationErrors.ToString(CultureInfo.InvariantCulture));
            }

            this.lastValidationStatus = this.rawWriteErrors > 0 || this.rawMissingPackets > 0
                                            ? "FAIL"
                                            : issues.Count == 0
                                                  ? "PASS"
                                                  : "INCOMPLETE";
            this.lastValidationSummary = string.Join("; ", issues.ToArray());
            this.WriteValidationJson(issues);
        }

        private void WriteValidationJson(List<string> issues)
        {
            try
            {
                string path = Path.Combine(this.sessionDirectory, "capture-validation.json");
                StringBuilder json = new StringBuilder();
                json.AppendLine("{");
                json.AppendLine("  \"status\": " + Json(this.lastValidationStatus) + ",");
                json.AppendLine("  \"summary\": " + Json(this.lastValidationSummary) + ",");
                json.AppendLine("  \"recaptureRequired\": " + (this.RawRecaptureRequired() ? "true" : "false") + ",");
                json.AppendLine("  \"coverage\": {");
                json.AppendLine("    \"rawPacketStream\": " + (this.inboundSequence + this.outboundSequence > 0 && this.rawWriteErrors == 0 && this.rawMissingPackets == 0 ? "true" : "false") + ",");
                json.AppendLine("    \"identityAndSpawn\": " + (this.scfuPackets > 0 && this.scfuDecodeErrors == 0 ? "true" : "false") + ",");
                json.AppendLine("    \"worldSnapshot\": " + (this.worldSnapshotRows > 0 && this.worldSnapshotErrors == 0 ? "true" : "false") + ",");
                json.AppendLine("    \"visibilitySampling\": " + (this.visibilitySamples >= 2 && this.visibilitySampleErrors == 0 ? "true" : "false") + ",");
                json.AppendLine("    \"visibilityTransitions\": " + (this.visibilityTransitionRows > 0 ? "true" : "false") + ",");
                json.AppendLine("    \"lineOfSightState\": " + (this.lineOfSightStateObservations > 0 && this.lineOfSightReadErrors == 0 ? "true" : "false") + ",");
                json.AppendLine("    \"lineOfSightTransitions\": " + (this.lineOfSightTransitionRows > 0 ? "true" : "false") + ",");
                json.AppendLine("    \"inPlayState\": " + (this.inPlayStateObservations > 0 && this.inPlayReadErrors == 0 ? "true" : "false") + ",");
                json.AppendLine("    \"inPlayTransitions\": " + (this.inPlayTransitionRows > 0 ? "true" : "false") + ",");
                json.AppendLine("    \"despawnPackets\": " + (this.despawnPackets > 0 ? "true" : "false") + ",");
                json.AppendLine("    \"playerCombatContext\": " + (this.playerCombatContextCompleteRows > 0 ? "true" : "false") + ",");
                json.AppendLine("    \"movement\": " + (this.movementPacketRows > 0 ? "true" : "false") + ",");
                json.AppendLine("    \"combatStart\": " + (this.attackStarts > 0 ? "true" : "false") + ",");
                json.AppendLine("    \"npcToPlayerAggro\": " + (this.npcToPlayerAggroObservationRows > 0 ? "true" : "false") + ",");
                json.AppendLine("    \"unprovokedAggroRange\": " + (this.unprovokedNpcAggroObservationRows > 0 ? "true" : "false") + ",");
                json.AppendLine("    \"deathOrCorpse\": " + (this.deathActions > 0 || this.corpseFullUpdatePackets > 0 ? "true" : "false") + ",");
                json.AppendLine("    \"identityLinkedLoot\": " + (this.identityLinkedInventoryPackets > 0 ? "true" : "false") + ",");
                json.AppendLine("    \"playerInventorySnapshot\": " + (this.inventorySnapshotRows > 0 && this.inventorySnapshotErrors == 0 ? "true" : "false") + ",");
                json.AppendLine("    \"itemUseIdentity\": " + (this.itemUseRequests > 0 && this.itemUseRequests == this.resolvedItemUseRequests && this.itemUseObservationErrors == 0 ? "true" : "false"));
                json.AppendLine("  },");
                json.AppendLine("  \"issues\": [");
                for (int index = 0; index < issues.Count; index++)
                {
                    json.Append("    ");
                    json.Append(Json(issues[index]));
                    json.AppendLine(index + 1 < issues.Count ? "," : string.Empty);
                }

                json.AppendLine("  ]");
                json.AppendLine("}");
                WriteAllTextAtomically(path, json.ToString());
            }
            catch (Exception ex)
            {
                this.WriteFallbackError("Validation", ex);
            }
        }

        private bool RawRecaptureRequired()
        {
            return this.rawWriteErrors > 0
                   || this.rawMissingPackets > 0
                   || this.inboundSequence + this.outboundSequence == 0;
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
            TryFlush(this.scfuAppearanceLog);
            TryFlush(this.worldSnapshotLog);
            TryFlush(this.playerCombatContextLog);
            TryFlush(this.aggroObservationLog);
            TryFlush(this.visibilityObservationLog);
            TryFlush(this.inventorySnapshotLog);
            TryFlush(this.itemUseObservationLog);
            TryFlush(this.eventLog);
        }

        private void UnsubscribeNoThrow()
        {
            try { Network.PacketReceived -= this.OnPacketReceived; } catch { }
            try { Network.PacketSent -= this.OnPacketSent; } catch { }
            try { AOInventory.ContainerOpened -= this.OnContainerOpened; } catch { }
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

        private static void WriteAllTextAtomically(string path, string content)
        {
            string pendingPath = path + ".pending";
            File.WriteAllText(pendingPath, content ?? string.Empty, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Replace(pendingPath, path, null);
            }
            else
            {
                File.Move(pendingPath, path);
            }
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

        private bool PacketContainsObservedCorpseIdentity(byte[] packet)
        {
            foreach (CorpseSnapshot corpse in this.knownCorpses.Values)
            {
                if (!this.observedCorpseIdentities.Contains(corpse.CorpseIdentity))
                {
                    continue;
                }

                if (ContainsIdentity(packet, corpse.CorpseType, corpse.CorpseInstance))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsIdentity(byte[] packet, uint identityType, uint identityInstance)
        {
            if (packet == null || packet.Length < 8)
            {
                return false;
            }

            byte[] identity = new byte[8];
            WriteUInt32BigEndian(identity, 0, identityType);
            WriteUInt32BigEndian(identity, 4, identityInstance);
            for (int offset = 0; offset <= packet.Length - identity.Length; offset++)
            {
                bool match = true;
                for (int index = 0; index < identity.Length; index++)
                {
                    if (packet[offset + index] != identity[index])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return true;
                }
            }

            return false;
        }

        private static void WriteUInt32BigEndian(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)(value >> 24);
            bytes[offset + 1] = (byte)(value >> 16);
            bytes[offset + 2] = (byte)(value >> 8);
            bytes[offset + 3] = (byte)value;
        }

        private static int FindAscii(byte[] bytes, string value)
        {
            if (bytes == null || string.IsNullOrEmpty(value))
            {
                return -1;
            }

            byte[] needle = Encoding.ASCII.GetBytes(value);
            for (int offset = 0; offset <= bytes.Length - needle.Length; offset++)
            {
                bool match = true;
                for (int index = 0; index < needle.Length; index++)
                {
                    if (bytes[offset + index] != needle[index])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return offset;
                }
            }

            return -1;
        }

        private void RefreshPlayerIdentity()
        {
            try
            {
                object localPlayer = DynelManager.LocalPlayer;
                string identity = NormalizeIdentity(MemberText(localPlayer, "Identity"));
                if (identity.Length > 0)
                {
                    this.playerIdentity = identity;
                }
            }
            catch
            {
            }
        }

        private string ResolveEntityName(string identity)
        {
            EntitySnapshot entity;
            return this.knownEntities.TryGetValue(NormalizeIdentity(identity), out entity)
                       ? entity.Name
                       : string.Empty;
        }

        private static bool SamePosition(PositionObservation left, PositionObservation right)
        {
            return left != null
                   && right != null
                   && Math.Abs(left.X - right.X) < 0.001f
                   && Math.Abs(left.Y - right.Y) < 0.001f
                   && Math.Abs(left.Z - right.Z) < 0.001f;
        }

        private static double? Distance(PositionObservation left, PositionObservation right)
        {
            if (left == null || right == null)
            {
                return null;
            }

            double x = left.X - right.X;
            double y = left.Y - right.Y;
            double z = left.Z - right.Z;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        private static double? Distance(EntitySnapshot left, EntitySnapshot right)
        {
            if (left == null || right == null || !left.HasPosition || !right.HasPosition)
            {
                return null;
            }

            double x = left.X - right.X;
            double y = left.Y - right.Y;
            double z = left.Z - right.Z;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        private static string FormatEntityCoordinate(EntitySnapshot entity, string coordinate)
        {
            if (entity == null || !entity.HasPosition)
            {
                return string.Empty;
            }

            if (coordinate == "X")
            {
                return FormatFloat(entity.X);
            }

            if (coordinate == "Y")
            {
                return FormatFloat(entity.Y);
            }

            return FormatFloat(entity.Z);
        }

        private static string FormatLineOfSight(EntitySnapshot entity)
        {
            return entity != null && entity.HasLineOfSight
                       ? (entity.IsInLineOfSight ? "true" : "false")
                       : string.Empty;
        }

        private static string FormatInPlay(EntitySnapshot entity)
        {
            return entity != null && entity.HasInPlay
                       ? (entity.IsInPlay ? "true" : "false")
                       : string.Empty;
        }

        private static string FormatDistance(double? value)
        {
            return value.HasValue
                       ? value.Value.ToString("0.######", CultureInfo.InvariantCulture)
                       : string.Empty;
        }

        private static string FormatPositionUtc(PositionObservation observation)
        {
            return observation == null
                       ? string.Empty
                       : observation.CapturedUtc.ToString("o", CultureInfo.InvariantCulture);
        }

        private static string FormatPositionCoordinate(PositionObservation observation, string coordinate)
        {
            if (observation == null)
            {
                return string.Empty;
            }

            if (coordinate == "X")
            {
                return FormatFloat(observation.X);
            }

            if (coordinate == "Y")
            {
                return FormatFloat(observation.Y);
            }

            return FormatFloat(observation.Z);
        }

        private static string FormatNullableDouble(double? value)
        {
            return value.HasValue
                       ? value.Value.ToString("0.###", CultureInfo.InvariantCulture)
                       : string.Empty;
        }

        private static string NormalizeIdentity(string identity)
        {
            string value = (identity ?? string.Empty).Trim();
            if (value.StartsWith("(", StringComparison.Ordinal)
                && value.EndsWith(")", StringComparison.Ordinal))
            {
                value = value.Substring(1, value.Length - 2);
            }

            int separator = value.IndexOf(':');
            if (separator <= 0 || separator + 1 >= value.Length)
            {
                return value;
            }

            uint instance;
            if (!uint.TryParse(
                    value.Substring(separator + 1),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out instance))
            {
                return value;
            }

            return value.Substring(0, separator)
                   + ":"
                   + instance.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static bool IdentityEquals(string left, string right)
        {
            return string.Equals(
                NormalizeIdentity(left),
                NormalizeIdentity(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static object MemberValue(object instance, string name)
        {
            if (instance == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(instance, null);
            }

            FieldInfo field = type.GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(instance);
        }

        private static string MemberText(object instance, string name)
        {
            return ValueText(MemberValue(instance, name));
        }

        private static string MemberNumber(object instance, string name)
        {
            return ValueText(MemberValue(instance, name));
        }

        private static string NamedStat(object character, string statName)
        {
            if (character == null)
            {
                return string.Empty;
            }

            object direct = MemberValue(character, statName);
            if (direct != null)
            {
                return ValueText(direct);
            }

            MethodInfo[] methods = character.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (MethodInfo method in methods)
            {
                if (!string.Equals(method.Name, "GetStat", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if ((parameters.Length != 1 && parameters.Length != 2)
                    || !parameters[0].ParameterType.IsEnum
                    || (parameters.Length == 2 && parameters[1].ParameterType != typeof(int)))
                {
                    continue;
                }

                try
                {
                    object key = Enum.Parse(parameters[0].ParameterType, statName, true);
                    object[] arguments = parameters.Length == 1
                                             ? new[] { key }
                                             : new[]
                                               {
                                                   key,
                                                   parameters[1].IsOptional
                                                       ? parameters[1].DefaultValue
                                                       : (object)2
                                               };
                    return ValueText(method.Invoke(character, arguments));
                }
                catch
                {
                }
            }

            object stats = MemberValue(character, "Stats");
            IDictionary dictionary = stats as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (string.Equals(ValueText(entry.Key), statName, StringComparison.OrdinalIgnoreCase))
                    {
                        return ValueText(entry.Value);
                    }
                }
            }

            IEnumerable enumerable = stats as IEnumerable;
            if (enumerable != null)
            {
                foreach (object entry in enumerable)
                {
                    string key = MemberText(entry, "Key");
                    if (key.Length == 0)
                    {
                        key = MemberText(entry, "Stat");
                    }

                    if (string.Equals(key, statName, StringComparison.OrdinalIgnoreCase))
                    {
                        object value = MemberValue(entry, "Value");
                        return ValueText(value ?? entry);
                    }
                }
            }

            return string.Empty;
        }

        private static string CollectionText(object collection)
        {
            IEnumerable enumerable = collection as IEnumerable;
            if (enumerable == null || collection is string)
            {
                return ValueText(collection);
            }

            var values = new List<string>();
            foreach (object item in enumerable)
            {
                values.Add(DescribeCollectionItem(item));
                if (values.Count >= 128)
                {
                    break;
                }
            }

            return string.Join("|", values.ToArray());
        }

        private static string DescribeCollectionItem(object item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            object key = MemberValue(item, "Key");
            object dictionaryValue = MemberValue(item, "Value");
            if (key != null && dictionaryValue != null)
            {
                return ValueText(key) + "=" + DescribeCollectionItem(dictionaryValue);
            }

            string identity = MemberText(item, "Identity");
            string name = MemberText(item, "Name");
            if (identity.Length > 0 || name.Length > 0)
            {
                var fields = new List<string>();
                if (identity.Length > 0)
                {
                    fields.Add("identity=" + NormalizeIdentity(identity));
                }

                if (name.Length > 0)
                {
                    fields.Add("name=" + name.Replace("|", "/"));
                }

                string remainingTime = MemberNumber(item, "RemainingTime");
                if (remainingTime.Length > 0)
                {
                    fields.Add("remaining=" + remainingTime);
                }

                string stackingOrder = MemberNumber(item, "StackingOrder");
                if (stackingOrder.Length > 0)
                {
                    fields.Add("stacking=" + stackingOrder);
                }

                string attackRange = MemberNumber(item, "AttackRange");
                if (attackRange.Length > 0)
                {
                    fields.Add("range=" + attackRange);
                }

                return string.Join(";", fields.ToArray());
            }

            return ValueText(item);
        }

        private static string ValueText(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            object nested = MemberValue(value, "Value");
            if (nested != null && !ReferenceEquals(nested, value))
            {
                value = nested;
            }

            IFormattable formattable = value as IFormattable;
            return formattable == null
                       ? value.ToString()
                       : formattable.ToString(null, CultureInfo.InvariantCulture);
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
            if (identityType == 0xC350)
            {
                return "SimpleChar";
            }

            if (identityType == 0xC76A)
            {
                return "Corpse";
            }

            return identityType.ToString(CultureInfo.InvariantCulture);
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
