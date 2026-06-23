using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.UI;

using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace AOSharpLiveCapture
{
    public class Main : AOPluginEntry
    {
        private static readonly object LifecycleSyncRoot = new object();
        private const int LocalEnemyCombatContextSeconds = 10;
        private static Main activeInstance;

        private readonly object syncRoot = new object();
        private readonly HashSet<string> knownCharacters = new HashSet<string>();
        private readonly HashSet<string> knownCorpses = new HashSet<string>();
        private readonly HashSet<string> exportedShopUpdateFingerprints = new HashSet<string>();
        private readonly HashSet<string> vendorInteractionIdentities = new HashSet<string>();
        private readonly HashSet<string> shopUpdateIdentities = new HashSet<string>();
        private readonly HashSet<string> vendorFullUpdateIdentities = new HashSet<string>();
        private readonly Dictionary<string, EnemyEntityState> enemyStates = new Dictionary<string, EnemyEntityState>();
        private readonly Dictionary<string, List<EnemyStateEvent>> enemyStateTimeline = new Dictionary<string, List<EnemyStateEvent>>();
        private readonly HashSet<string> interestingMessageNames = new HashSet<string>
        {
            "SimpleCharFullUpdate",
            "CharInPlay",
            "ChatText",
            "CharacterAction",
            "GenericCmd",
            "TemplateAction",
            "CorpseFullUpdate",
            "Despawn",
            "StopFight",
            "FightModeUpdate",
            "Attack",
            "AttackInfo",
            "HealthDamage",
            "MissedAttackInfo",
            "SpecialAttackInfo",
            "CharSecSpecAttack",
            "InventoryUpdate",
            "ShopUpdate",
            "Trade",
            "SimpleItemFullUpdate",
            "WeaponItemFullUpdate",
            "ClientMoveItemToInventory",
            "ContainerAddItem",
            "ClientContainerAddItem",
            "BankCorpse",
            "Feedback",
            "FormatFeedback",
            "Stat",
            "StartLogout",
            "StopLogout",
            "SetPos",
            "CharDCMove",
            "InventoryUpdated",
            "Quest",
            "QuestFullUpdate",
            "QuestAlternative",
            "CreateQuest",
            "NewLevel",
            "KnubotNPCDescription",
            "KnubotOpenChatWindow",
            "KnubotAppendText",
            "KnubotAnswerList",
            "KnubotAnswer",
            "KnubotCloseChatWindow",
            "KnubotStartTrade",
            "KnubotTrade",
            "KnubotFinishTrade",
            "KnubotRejectedItems",
            "VendingMachineFullUpdate"
        };

        private string sessionDirectory;
        private StreamWriter eventsLog;
        private StreamWriter packetsLog;
        private StreamWriter shopUpdatesLog;
        private StreamWriter vendorFullUpdatesLog;
        private StreamWriter systemMessagesLog;
        private StreamWriter chatDialogueLog;
        private StreamWriter npcInteractionsLog;
        private StreamWriter inventoryUpdatesLog;
        private StreamWriter enemyStateLog;
        private StreamWriter enemyFullUpdatesLog;
        private StreamWriter enemyCombatLog;
        private StreamWriter enemyMovementLog;
        private StreamWriter enemyStatUpdatesLog;
        private bool enabled;
        private bool captureFinalized;
        private int inboundPacketCount;
        private int outboundPacketCount;
        private int decodedInboundCount;
        private int decodedOutboundCount;
        private int shopUpdateMessageCount;
        private int shopUpdateRowCount;
        private int vendorFullUpdateMessageCount;
        private int systemMessageCount;
        private int chatDialogueMessageCount;
        private int npcInteractionCount;
        private int inventoryUpdateMessageCount;
        private int inventoryUpdateRowCount;
        private int vendorInteractionAttemptCount;
        private int enemyStateRowCount;
        private int enemyCombatEventCount;
        private int enemyDamageEventCount;
        private int enemyDeathEventCount;
        private int enemySpawnEventCount;
        private int enemyDespawnEventCount;
        private int enemyHealthUpdateCount;
        private int enemyPositionUpdateCount;
        private int enemyFullUpdateRowCount;
        private int enemyCombatRowCount;
        private int enemyMovementRowCount;
        private int enemyStatUpdateRowCount;
        private DateTime nextFlushUtc;
        private DateTime nextSnapshotUtc;
        private DateTime captureStartUtc;
        private DateTime captureStartLocal;
        private DateTime lastPacketUtc;
        private DateTime localEnemyCombatContextUntilUtc;
        private string lastPlayfieldId = string.Empty;
        private CombatLootSmoke combatLootSmoke;
        private bool initialized;
        private bool enemyFightCaptureEnabled;
        private bool enemyFightCaptureStarted;

        public override void Run(string pluginDir)
        {
            lock (LifecycleSyncRoot)
            {
                if (activeInstance != null)
                {
                    return;
                }

                activeInstance = this;
                this.initialized = true;
            }

            try
            {
            this.captureStartUtc = DateTime.UtcNow;
            this.captureStartLocal = DateTime.Now;
            this.lastPacketUtc = this.captureStartUtc;

            this.sessionDirectory = CreateSessionDirectory(pluginDir);
            this.eventsLog = CreateWriter(Path.Combine(this.sessionDirectory, "events.log"));
            this.packetsLog = CreateWriter(Path.Combine(this.sessionDirectory, "packets.hex.log"));
            this.shopUpdatesLog = CreateWriter(Path.Combine(this.sessionDirectory, "shop-updates.csv"));
            this.shopUpdatesLog.WriteLine("CapturedUtc,Direction,Sequence,TerminalIdentity,Slot,LowId,HighId,Quality");
            this.vendorFullUpdatesLog = CreateWriter(Path.Combine(this.sessionDirectory, "vendor-full-updates.csv"));
            this.vendorFullUpdatesLog.WriteLine("CapturedUtc,Direction,Sequence,Identity,OwnerType,OwnerInstance,PlayfieldId,PositionX,PositionY,PositionZ,Unknown7,Template,Mesh,BuyModifier,SellModifier,StatsCount");
            this.systemMessagesLog = CreateWriter(Path.Combine(this.sessionDirectory, "system-messages.log"));
            this.chatDialogueLog = CreateWriter(Path.Combine(this.sessionDirectory, "chat-dialogue.log"));
            this.npcInteractionsLog = CreateWriter(Path.Combine(this.sessionDirectory, "npc-interactions.log"));
            this.inventoryUpdatesLog = CreateWriter(Path.Combine(this.sessionDirectory, "inventory-updates.csv"));
            this.inventoryUpdatesLog.WriteLine("CapturedUtc,Direction,Sequence,InventoryIdentity,Handle,Slot,Placement,Flags,Count,ItemIdentity,LowId,HighId,Quality,Unknown");
            this.enemyStateLog = CreateWriter(Path.Combine(this.sessionDirectory, "enemy-state.csv"));
            this.enemyStateLog.WriteLine("timestamp,entityId,level,currentHealth,maxHealth,x,y,z,eventType");
            this.enemyFullUpdatesLog = CreateWriter(Path.Combine(this.sessionDirectory, "enemy-full-updates.csv"));
            this.enemyFullUpdatesLog.WriteLine("CapturedUtc,Direction,Sequence,Identity,Name,PlayfieldId,PositionX,PositionY,PositionZ,HeadingX,HeadingY,HeadingZ,HeadingW,FightingTargetRole,FightingTargetIdentity,Version,Flags,CharacterFlags,AccountFlags,Expansions,CharacterInfoType,NPCFamily,LosHeight,Level,Health,HealthDamage,MonsterData,MonsterScale,VisualFlags,VisibleTitle,Unknown1Length,HeadMesh,RunSpeedBase,ActiveNanoCount,TextureCount,Textures,MeshCount,Meshes,Flags2,Unknown2,Detail");
            this.enemyCombatLog = CreateWriter(Path.Combine(this.sessionDirectory, "enemy-combat.csv"));
            this.enemyCombatLog.WriteLine("CapturedUtc,Direction,Sequence,MessageType,SourceRole,SourceIdentity,TargetRole,TargetIdentity,AuxRole1,AuxIdentity1,AuxRole2,AuxIdentity2,Action,Amount,TargetHp,Unknown1,Unknown2,Unknown3,Unknown4,Unknown5,Unknown6,Detail");
            this.enemyMovementLog = CreateWriter(Path.Combine(this.sessionDirectory, "enemy-movement.csv"));
            this.enemyMovementLog.WriteLine("CapturedUtc,Direction,Sequence,MessageType,IdentityRole,Identity,MoveType,PositionX,PositionY,PositionZ,HeadingX,HeadingY,HeadingZ,HeadingW,Unknown1,Unknown2,Unknown3,Detail");
            this.enemyStatUpdatesLog = CreateWriter(Path.Combine(this.sessionDirectory, "enemy-stat-updates.csv"));
            this.enemyStatUpdatesLog.WriteLine("CapturedUtc,Direction,Sequence,MessageType,IdentityRole,Identity,Stat,StatId,Value,PositionX,PositionY,PositionZ,StatsCount,Detail");
            this.WriteEnemyStateJson();
            this.WriteCaptureSessionMetadata(this.captureStartUtc, this.captureStartLocal);
            this.WriteCaptureInfo(null, CaptureValidation.Running());
            this.enabled = true;
            this.nextFlushUtc = DateTime.UtcNow.AddSeconds(2);
            this.nextSnapshotUtc = DateTime.UtcNow.AddSeconds(1);
            this.combatLootSmoke = new CombatLootSmoke(pluginDir, this.LogSmokeEvent);

            Network.PacketReceived += this.OnPacketReceived;
            Network.PacketSent += this.OnPacketSent;
            Network.N3MessageReceived += this.OnN3MessageReceived;
            Network.N3MessageSent += this.OnN3MessageSent;
            Network.ChatMessageReceived += this.OnChatMessageReceived;
            DynelManager.DynelSpawned += this.OnDynelSpawned;
            DynelManager.CharInPlay += this.OnCharInPlay;
            Game.PlayfieldInit += this.OnPlayfieldInit;
            Game.TeleportStarted += this.OnTeleportStarted;
            Game.TeleportEnded += this.OnTeleportEnded;
            Game.TeleportFailed += this.OnTeleportFailed;
            Game.OnUpdate += this.OnUpdate;

            Chat.RegisterCommand("aocap", this.OnCommand);
            Chat.RegisterCommand("aosmoke", this.OnSmokeCommand);

            this.LogEvent("PLUGIN", "AOSharpLiveCapture loaded. session=" + this.sessionDirectory);
            this.LogEvent("PLUGIN", "Commands: /aocap start | stop | mark <text> | status | flush | snapshot | dynels [force] | fight start|stop|status");
            this.LogEvent("PLUGIN", "Smoke commands: /aosmoke start [mobAlias] | stop | status | log");
            this.LogEvent("PLUGIN", "ShopUpdate CSV: " + Path.Combine(this.sessionDirectory, "shop-updates.csv"));
            this.LogEvent("PLUGIN", "VendingMachineFullUpdate CSV: " + Path.Combine(this.sessionDirectory, "vendor-full-updates.csv"));
            this.LogEvent("PLUGIN", "System messages log: " + Path.Combine(this.sessionDirectory, "system-messages.log"));
            this.LogEvent("PLUGIN", "Chat/dialogue log: " + Path.Combine(this.sessionDirectory, "chat-dialogue.log"));
            this.LogEvent("PLUGIN", "NPC interactions log: " + Path.Combine(this.sessionDirectory, "npc-interactions.log"));
            this.LogEvent("PLUGIN", "Inventory update CSV: " + Path.Combine(this.sessionDirectory, "inventory-updates.csv"));
            this.LogEvent("PLUGIN", "Enemy state CSV: " + Path.Combine(this.sessionDirectory, "enemy-state.csv"));
            this.LogEvent("PLUGIN", "Enemy full update CSV: " + Path.Combine(this.sessionDirectory, "enemy-full-updates.csv"));
            this.LogEvent("PLUGIN", "Enemy combat CSV: " + Path.Combine(this.sessionDirectory, "enemy-combat.csv"));
            this.LogEvent("PLUGIN", "Enemy movement CSV: " + Path.Combine(this.sessionDirectory, "enemy-movement.csv"));
            this.LogEvent("PLUGIN", "Enemy stat update CSV: " + Path.Combine(this.sessionDirectory, "enemy-stat-updates.csv"));
            this.LogEvent("PLUGIN", "Enemy state JSON: " + Path.Combine(this.sessionDirectory, "enemy-state.json"));
            this.LogEvent("PLUGIN", "Capture info: " + Path.Combine(this.sessionDirectory, "capture_info.json"));
            this.LogEvent("PLUGIN", "Capture session metadata: " + Path.Combine(this.sessionDirectory, "capture-session.json"));
            this.LogSnapshot("initial");
            Chat.WriteLine("AOSharpLiveCapture logging to " + this.sessionDirectory, ChatColor.Gold);
            }
            catch
            {
                lock (LifecycleSyncRoot)
                {
                    if (ReferenceEquals(activeInstance, this))
                    {
                        activeInstance = null;
                    }
                }

                this.initialized = false;
                throw;
            }
        }

        public override void Teardown()
        {
            if (!this.initialized)
            {
                return;
            }

            lock (LifecycleSyncRoot)
            {
                if (!ReferenceEquals(activeInstance, this))
                {
                    return;
                }
            }

            Network.PacketReceived -= this.OnPacketReceived;
            Network.PacketSent -= this.OnPacketSent;
            Network.N3MessageReceived -= this.OnN3MessageReceived;
            Network.N3MessageSent -= this.OnN3MessageSent;
            Network.ChatMessageReceived -= this.OnChatMessageReceived;
            DynelManager.DynelSpawned -= this.OnDynelSpawned;
            DynelManager.CharInPlay -= this.OnCharInPlay;
            Game.PlayfieldInit -= this.OnPlayfieldInit;
            Game.TeleportStarted -= this.OnTeleportStarted;
            Game.TeleportEnded -= this.OnTeleportEnded;
            Game.TeleportFailed -= this.OnTeleportFailed;
            Game.OnUpdate -= this.OnUpdate;
            this.combatLootSmoke?.Teardown();

            this.LogEvent("PLUGIN", "AOSharpLiveCapture teardown.");
            this.FinalizeCapture();

            lock (LifecycleSyncRoot)
            {
                if (ReferenceEquals(activeInstance, this))
                {
                    activeInstance = null;
                }
            }

            this.initialized = false;
        }

        private void OnCommand(string command, string[] args, ChatWindow chatWindow)
        {
            string subCommand = args.Length == 0 ? "status" : args[0].ToLowerInvariant();
            switch (subCommand)
            {
                case "start":
                    this.enabled = true;
                    this.LogEvent("COMMAND", "capture started");
                    chatWindow.WriteLine("AO capture started: " + this.sessionDirectory, ChatColor.Gold);
                    break;

                case "stop":
                    this.LogEvent("COMMAND", "capture stopped");
                    this.enabled = false;
                    this.Flush();
                    chatWindow.WriteLine("AO capture stopped.", ChatColor.Gold);
                    break;

                case "mark":
                    string marker = args.Length > 1 ? string.Join(" ", args.Skip(1).ToArray()) : "(no text)";
                    this.LogEvent("MARK", marker);
                    chatWindow.WriteLine("AO capture marker written.", ChatColor.Gold);
                    break;

                case "flush":
                    this.Flush();
                    chatWindow.WriteLine("AO capture flushed.", ChatColor.Gold);
                    break;

                case "snapshot":
                    this.LogSnapshot("manual");
                    chatWindow.WriteLine("AO capture snapshot written.", ChatColor.Gold);
                    break;

                case "dynels":
                    bool forceDynelDump = args.Length > 1 && string.Equals(args[1], "force", StringComparison.OrdinalIgnoreCase);
                    DynelDumpResult result = this.DumpDynelsNoThrow(forceDynelDump);
                    this.TryWriteChat(
                        chatWindow,
                        result.Success
                            ? result.AlreadyWritten
                                ? "AO dynel static dump already exists: " + result.CsvPath + " Use /aocap dynels force to replace it."
                                : string.Format(
                                    CultureInfo.InvariantCulture,
                                    "AO dynel static dump wrote {0} rows: {1}",
                                    result.Count,
                                    result.CsvPath)
                            : "AO dynel dump failed: " + result.Error,
                        ChatColor.Gold);
                    break;

                case "fight":
                    this.OnFightCommand(args, chatWindow);
                    break;

                default:
                    chatWindow.WriteLine(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "AO capture {0}. fight={1}. inRaw={2} outRaw={3} inN3={4} outN3={5} dir={6}",
                            this.enabled ? "running" : "stopped",
                            this.enemyFightCaptureEnabled ? "on" : "off",
                            this.inboundPacketCount,
                            this.outboundPacketCount,
                            this.decodedInboundCount,
                            this.decodedOutboundCount,
                            this.sessionDirectory),
                        ChatColor.Gold);
                    break;
            }
        }

        private void OnFightCommand(string[] args, ChatWindow chatWindow)
        {
            string action = args.Length > 1 ? args[1].ToLowerInvariant() : "status";
            switch (action)
            {
                case "start":
                    this.enemyFightCaptureEnabled = true;
                    this.enemyFightCaptureStarted = true;
                    this.LogEvent("COMMAND", "enemy fight capture started");
                    this.TryWriteChat(chatWindow, "AO enemy fight capture started.", ChatColor.Gold);
                    break;

                case "stop":
                    this.enemyFightCaptureEnabled = false;
                    this.LogEvent("COMMAND", "enemy fight capture stopped");
                    this.Flush();
                    this.TryWriteChat(chatWindow, "AO enemy fight capture stopped.", ChatColor.Gold);
                    break;

                default:
                    this.TryWriteChat(
                        chatWindow,
                        "AO enemy fight capture " + (this.enemyFightCaptureEnabled ? "running." : "stopped."),
                        ChatColor.Gold);
                    break;
            }
        }

        private DynelDumpResult DumpDynelsNoThrow(bool force)
        {
            try
            {
                return this.DumpDynels(force);
            }
            catch (Exception ex)
            {
                this.TryLogEvent("DYNEL-DUMP-ERROR", ex.ToString());
                return DynelDumpResult.Failed(ex.Message);
            }
        }

        private DynelDumpResult DumpDynels(bool force)
        {
            DateTime capturedUtc = DateTime.UtcNow;
            string csvPath = Path.Combine(this.sessionDirectory, "dynels.csv");
            string jsonPath = Path.Combine(this.sessionDirectory, "dynels.json");
            string summaryPath = Path.Combine(this.sessionDirectory, "dynels-summary.txt");

            if (!force && File.Exists(csvPath) && new FileInfo(csvPath).Length > 0)
            {
                return DynelDumpResult.AlreadyExists(csvPath, jsonPath, summaryPath);
            }

            Dynel[] dynels = DynelManager.AllDynels == null ? new Dynel[0] : DynelManager.AllDynels.ToArray();
            LocalPlayer localPlayer = DynelManager.LocalPlayer;

            DynelDumpRow[] rows = dynels.Select(
                    (dynel, index) => this.CreateDynelDumpRow(capturedUtc, index, dynel, localPlayer))
                .OrderBy(x => x.SortType)
                .ThenBy(x => x.SortInstance)
                .ThenBy(x => x.Name)
                .ToArray();

            this.WriteDynelCsv(csvPath, rows);
            this.WriteDynelJson(jsonPath, capturedUtc, rows);
            this.WriteDynelSummary(summaryPath, capturedUtc, rows);

            this.TryLogEvent(
                "DYNEL-DUMP",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "rows={0} force={1} csv={2} json={3} summary={4}",
                    rows.Length,
                    force,
                    csvPath,
                    jsonPath,
                    summaryPath));

            return new DynelDumpResult(rows.Length, csvPath, jsonPath, summaryPath);
        }

        private DynelDumpRow CreateDynelDumpRow(DateTime capturedUtc, int index, Dynel dynel, LocalPlayer localPlayer)
        {
            var row = new DynelDumpRow
            {
                CapturedUtc = capturedUtc.ToString("o", CultureInfo.InvariantCulture),
                LocalCharacterName = this.GetLocalCharacterName(),
                LocalCharacterIdentity = Safe(() => localPlayer == null ? string.Empty : localPlayer.Identity.ToString()),
                PlayfieldIdentity = this.GetDetectedPlayfieldId(),
                Index = index.ToString(CultureInfo.InvariantCulture)
            };

            if (dynel == null)
            {
                row.Error = "null dynel";
                return row;
            }

            try
            {
                Identity identity = dynel.Identity;
                row.Identity = Safe(() => identity.ToString());
                row.IdentityType = Safe(() => identity.Type.ToString());
                row.IdentityTypeValue = Safe(() => ((int)identity.Type).ToString(CultureInfo.InvariantCulture));
                row.Instance = Safe(() => identity.Instance.ToString(CultureInfo.InvariantCulture));
                row.InstanceHex = Safe(() => identity.Instance.ToString("X8", CultureInfo.InvariantCulture));
                row.SortType = (int)identity.Type;
                row.SortInstance = identity.Instance;
            }
            catch (Exception ex)
            {
                row.Error = "identity: " + ex.Message;
            }

            row.ClassName = Safe(() => dynel.GetType().Name);
            row.Name = Safe(() => dynel.Name);
            row.Position = Safe(() => dynel.Position.ToString());
            row.DynelCategory = this.GetDynelCategory(dynel);
            row.Pointer = Safe(() => "0x" + dynel.Pointer.ToInt64().ToString("X", CultureInfo.InvariantCulture));

            if (SafeBool(() => dynel.Identity.Type == IdentityType.SimpleChar))
            {
                SimpleChar character = dynel.Cast<SimpleChar>();
                bool isPet = SafeBool(() => character.IsPet);
                bool isNpc = SafeBool(() => character.IsNpc);
                bool isPlayer = SafeBool(() => character.IsPlayer);

                row.CharacterKind = isPet ? "pet" : isNpc ? "npc" : isPlayer ? "player" : "simplechar";
                row.IsNpc = isNpc.ToString();
                row.IsPet = isPet.ToString();

                if (isNpc || isPet)
                {
                    row.IsInPlay = Safe(() => character.IsInPlay.ToString());
                    row.IsAlive = Safe(() => character.IsAlive.ToString());
                    row.IsAttacking = Safe(() => character.IsAttacking.ToString());
                    row.FightingTarget = Safe(
                        () =>
                        {
                            if (character.FightingTarget == null)
                            {
                                return string.Empty;
                            }

                            string target = character.FightingTarget.Identity.ToString();
                            return target == this.GetLocalPlayerIdentityString() ? "local-player" : target;
                        });
                    row.Health = SafeStat(character, Stat.Health);
                    row.MaxHealth = SafeStat(character, Stat.MaxHealth);
                    row.HealthPercent = SafeFloat(() => character.HealthPercent).ToString("R", CultureInfo.InvariantCulture);
                    row.NpcLevel = SafeStat(character, Stat.Level);
                    row.MonsterData = SafeStat(character, Stat.MonsterData);
                    row.CatMesh = SafeStat(character, Stat.CATMesh);
                    row.DisplayCatMesh = SafeStat(character, Stat.DisplayCATMesh);
                    row.VisualFlags = SafeStat(character, Stat.VisualFlags);
                    row.State = SafeStat(character, Stat.State);
                    row.CurrentState = SafeStat(character, Stat.CurrentState);
                    row.ActionCategory = SafeStat(character, Stat.ActionCategory);
                    row.Scale = SafeStat(character, Stat.Scale);
                    row.CharRadius = SafeStat(character, Stat.CharRadius);
                    row.NpcBrainState = SafeStat(character, Stat.NPCBrainState);
                    row.PetState = SafeStat(character, Stat.PetState);
                    row.PetOwnerId = isPet ? Safe(() => character.PetOwnerId.ToString(CultureInfo.InvariantCulture)) : string.Empty;
                    row.NpcFamily = SafeStat(character, Stat.NPCFamily);
                    row.NpcVicinityFamily = SafeStat(character, Stat.NPCVicinityFamily);
                    row.RunSpeed = SafeStat(character, Stat.RunSpeed);
                    row.MinDamage = SafeStat(character, Stat.MinDamage);
                    row.MaxDamage = SafeStat(character, Stat.MaxDamage);
                    row.DefaultAttackType = SafeStat(character, Stat.DefaultAttackType);
                    row.DamageType1 = SafeStat(character, Stat.DamageType1);
                    row.DamageType2 = SafeStat(character, Stat.DamageType2);
                    row.AttackDelay = SafeStat(character, Stat.AttackDelay);
                    row.RechargeDelay = SafeStat(character, Stat.RechargeDelay);
                    row.AttackDelayCap = SafeStat(character, Stat.AttackDelayCap);
                    row.RechargeDelayCap = SafeStat(character, Stat.RechargeDelayCap);
                    row.EquippedWeapons = SafeStat(character, Stat.EquippedWeapons);
                    row.HealDelta = SafeStat(character, Stat.HealDelta);
                    row.DeadTimer = SafeStat(character, Stat.DeadTimer);
                    row.CorpseType = SafeStat(character, Stat.CorpseType);
                    row.CorpseInstance = SafeStat(character, Stat.CorpseInstance);
                    row.CorpseAnimKey = SafeStat(character, Stat.CorpseAnimKey);
                    row.DieAnim = SafeStat(character, Stat.DieAnim);
                }
            }

            return row;
        }

        private string GetDynelCategory(Dynel dynel)
        {
            if (dynel == null)
            {
                return "null";
            }

            string identityType = Safe(() => dynel.Identity.Type.ToString());
            if (identityType == "SimpleChar")
            {
                return "character";
            }

            if (identityType == "Door")
            {
                return "door";
            }

            if (identityType == "Terminal")
            {
                return "terminal";
            }

            if (identityType == "CityController")
            {
                return "city-controller";
            }

            if (identityType == "VendingMachine")
            {
                return "vendor";
            }

            if (identityType == "Corpse")
            {
                return "corpse";
            }

            return identityType;
        }

        private void WriteDynelCsv(string path, DynelDumpRow[] rows)
        {
            using (StreamWriter writer = CreateWriter(path))
            {
                writer.WriteLine("CapturedUtc,LocalCharacterName,LocalCharacterIdentity,PlayfieldIdentity,Index,DynelCategory,CharacterKind,Identity,IdentityType,IdentityTypeValue,Instance,InstanceHex,ClassName,Name,Position,IsNpc,IsPet,IsInPlay,IsAlive,IsAttacking,FightingTarget,Health,MaxHealth,HealthPercent,NpcLevel,MonsterData,CATMesh,DisplayCATMesh,VisualFlags,State,CurrentState,ActionCategory,Scale,CharRadius,NPCBrainState,PetState,PetOwnerId,NPCFamily,NPCVicinityFamily,RunSpeed,MinDamage,MaxDamage,DefaultAttackType,DamageType1,DamageType2,AttackDelay,RechargeDelay,AttackDelayCap,RechargeDelayCap,EquippedWeapons,HealDelta,DeadTimer,CorpseType,CorpseInstance,CorpseAnimKey,DieAnim,Pointer,Error");

                foreach (DynelDumpRow row in rows)
                {
                    writer.WriteLine(string.Join(
                        ",",
                        new[]
                        {
                            Csv(row.CapturedUtc),
                            Csv(row.LocalCharacterName),
                            Csv(row.LocalCharacterIdentity),
                            Csv(row.PlayfieldIdentity),
                            Csv(row.Index),
                            Csv(row.DynelCategory),
                            Csv(row.CharacterKind),
                            Csv(row.Identity),
                            Csv(row.IdentityType),
                            Csv(row.IdentityTypeValue),
                            Csv(row.Instance),
                            Csv(row.InstanceHex),
                            Csv(row.ClassName),
                            Csv(row.Name),
                            Csv(row.Position),
                            Csv(row.IsNpc),
                            Csv(row.IsPet),
                            Csv(row.IsInPlay),
                            Csv(row.IsAlive),
                            Csv(row.IsAttacking),
                            Csv(row.FightingTarget),
                            Csv(row.Health),
                            Csv(row.MaxHealth),
                            Csv(row.HealthPercent),
                            Csv(row.NpcLevel),
                            Csv(row.MonsterData),
                            Csv(row.CatMesh),
                            Csv(row.DisplayCatMesh),
                            Csv(row.VisualFlags),
                            Csv(row.State),
                            Csv(row.CurrentState),
                            Csv(row.ActionCategory),
                            Csv(row.Scale),
                            Csv(row.CharRadius),
                            Csv(row.NpcBrainState),
                            Csv(row.PetState),
                            Csv(row.PetOwnerId),
                            Csv(row.NpcFamily),
                            Csv(row.NpcVicinityFamily),
                            Csv(row.RunSpeed),
                            Csv(row.MinDamage),
                            Csv(row.MaxDamage),
                            Csv(row.DefaultAttackType),
                            Csv(row.DamageType1),
                            Csv(row.DamageType2),
                            Csv(row.AttackDelay),
                            Csv(row.RechargeDelay),
                            Csv(row.AttackDelayCap),
                            Csv(row.RechargeDelayCap),
                            Csv(row.EquippedWeapons),
                            Csv(row.HealDelta),
                            Csv(row.DeadTimer),
                            Csv(row.CorpseType),
                            Csv(row.CorpseInstance),
                            Csv(row.CorpseAnimKey),
                            Csv(row.DieAnim),
                            Csv(row.Pointer),
                            Csv(row.Error)
                        }));
                }
            }
        }

        private void WriteDynelJson(string path, DateTime capturedUtc, DynelDumpRow[] rows)
        {
            var json = new StringBuilder();
            json.AppendLine("{");
            json.Append("  \"capturedUtc\": ");
            json.Append(Json(capturedUtc.ToString("o", CultureInfo.InvariantCulture)));
            json.AppendLine(",");
            json.Append("  \"captureFolderPath\": ");
            json.Append(Json(this.sessionDirectory));
            json.AppendLine(",");
            json.Append("  \"playfieldIdentity\": ");
            json.Append(Json(Safe(() => Playfield.Identity.ToString())));
            json.AppendLine(",");
            json.Append("  \"localCharacterName\": ");
            json.Append(Json(Safe(() => DynelManager.LocalPlayer == null ? string.Empty : DynelManager.LocalPlayer.Name)));
            json.AppendLine(",");
            json.Append("  \"dynelCount\": ");
            json.Append(rows.Length.ToString(CultureInfo.InvariantCulture));
            json.AppendLine(",");
            json.AppendLine("  \"dynels\": [");

            for (int i = 0; i < rows.Length; i++)
            {
                if (i > 0)
                {
                    json.AppendLine(",");
                }

                this.AppendDynelRowJson(json, rows[i], "    ");
            }

            json.AppendLine();
            json.AppendLine("  ]");
            json.AppendLine("}");
            File.WriteAllText(path, json.ToString(), Encoding.UTF8);
        }

        private void AppendDynelRowJson(StringBuilder json, DynelDumpRow row, string indent)
        {
            json.Append(indent);
            json.AppendLine("{");
            AppendJsonField(json, indent + "  ", "capturedUtc", row.CapturedUtc, true);
            AppendJsonField(json, indent + "  ", "identity", row.Identity, true);
            AppendJsonField(json, indent + "  ", "identityType", row.IdentityType, true);
            AppendJsonField(json, indent + "  ", "identityTypeValue", row.IdentityTypeValue, true);
            AppendJsonField(json, indent + "  ", "instance", row.Instance, true);
            AppendJsonField(json, indent + "  ", "instanceHex", row.InstanceHex, true);
            AppendJsonField(json, indent + "  ", "className", row.ClassName, true);
            AppendJsonField(json, indent + "  ", "name", row.Name, true);
            AppendJsonField(json, indent + "  ", "position", row.Position, true);
            AppendJsonField(json, indent + "  ", "dynelCategory", row.DynelCategory, true);
            AppendJsonField(json, indent + "  ", "characterKind", row.CharacterKind, true);
            AppendJsonField(json, indent + "  ", "isNpc", row.IsNpc, true);
            AppendJsonField(json, indent + "  ", "isPet", row.IsPet, true);
            AppendJsonField(json, indent + "  ", "isInPlay", row.IsInPlay, true);
            AppendJsonField(json, indent + "  ", "isAlive", row.IsAlive, true);
            AppendJsonField(json, indent + "  ", "isAttacking", row.IsAttacking, true);
            AppendJsonField(json, indent + "  ", "fightingTarget", row.FightingTarget, true);
            AppendJsonField(json, indent + "  ", "health", row.Health, true);
            AppendJsonField(json, indent + "  ", "maxHealth", row.MaxHealth, true);
            AppendJsonField(json, indent + "  ", "healthPercent", row.HealthPercent, true);
            AppendJsonField(json, indent + "  ", "npcLevel", row.NpcLevel, true);
            AppendJsonField(json, indent + "  ", "monsterData", row.MonsterData, true);
            AppendJsonField(json, indent + "  ", "catMesh", row.CatMesh, true);
            AppendJsonField(json, indent + "  ", "displayCatMesh", row.DisplayCatMesh, true);
            AppendJsonField(json, indent + "  ", "visualFlags", row.VisualFlags, true);
            AppendJsonField(json, indent + "  ", "state", row.State, true);
            AppendJsonField(json, indent + "  ", "currentState", row.CurrentState, true);
            AppendJsonField(json, indent + "  ", "actionCategory", row.ActionCategory, true);
            AppendJsonField(json, indent + "  ", "scale", row.Scale, true);
            AppendJsonField(json, indent + "  ", "charRadius", row.CharRadius, true);
            AppendJsonField(json, indent + "  ", "npcBrainState", row.NpcBrainState, true);
            AppendJsonField(json, indent + "  ", "petState", row.PetState, true);
            AppendJsonField(json, indent + "  ", "petOwnerId", row.PetOwnerId, true);
            AppendJsonField(json, indent + "  ", "npcFamily", row.NpcFamily, true);
            AppendJsonField(json, indent + "  ", "npcVicinityFamily", row.NpcVicinityFamily, true);
            AppendJsonField(json, indent + "  ", "runSpeed", row.RunSpeed, true);
            AppendJsonField(json, indent + "  ", "minDamage", row.MinDamage, true);
            AppendJsonField(json, indent + "  ", "maxDamage", row.MaxDamage, true);
            AppendJsonField(json, indent + "  ", "defaultAttackType", row.DefaultAttackType, true);
            AppendJsonField(json, indent + "  ", "damageType1", row.DamageType1, true);
            AppendJsonField(json, indent + "  ", "damageType2", row.DamageType2, true);
            AppendJsonField(json, indent + "  ", "attackDelay", row.AttackDelay, true);
            AppendJsonField(json, indent + "  ", "rechargeDelay", row.RechargeDelay, true);
            AppendJsonField(json, indent + "  ", "attackDelayCap", row.AttackDelayCap, true);
            AppendJsonField(json, indent + "  ", "rechargeDelayCap", row.RechargeDelayCap, true);
            AppendJsonField(json, indent + "  ", "equippedWeapons", row.EquippedWeapons, true);
            AppendJsonField(json, indent + "  ", "healDelta", row.HealDelta, true);
            AppendJsonField(json, indent + "  ", "deadTimer", row.DeadTimer, true);
            AppendJsonField(json, indent + "  ", "corpseType", row.CorpseType, true);
            AppendJsonField(json, indent + "  ", "corpseInstance", row.CorpseInstance, true);
            AppendJsonField(json, indent + "  ", "corpseAnimKey", row.CorpseAnimKey, true);
            AppendJsonField(json, indent + "  ", "dieAnim", row.DieAnim, true);
            AppendJsonField(json, indent + "  ", "pointer", row.Pointer, true);
            AppendJsonField(json, indent + "  ", "error", row.Error, false);
            json.AppendLine();
            json.Append(indent);
            json.Append("}");
        }

        private void WriteDynelSummary(string path, DateTime capturedUtc, DynelDumpRow[] rows)
        {
            var summary = new StringBuilder();
            summary.AppendLine("Dynel dump");
            summary.Append("CapturedUtc: ");
            summary.AppendLine(capturedUtc.ToString("o", CultureInfo.InvariantCulture));
            summary.Append("CaptureFolder: ");
            summary.AppendLine(this.sessionDirectory);
            summary.Append("Playfield: ");
            summary.AppendLine(Safe(() => Playfield.Identity.ToString()));
            summary.Append("LocalCharacter: ");
            summary.AppendLine(Safe(() => DynelManager.LocalPlayer == null ? string.Empty : DynelManager.LocalPlayer.Identity + " " + DynelManager.LocalPlayer.Name));
            summary.Append("DynelCount: ");
            summary.AppendLine(rows.Length.ToString(CultureInfo.InvariantCulture));
            summary.AppendLine();
            summary.AppendLine("Counts by identity type:");

            foreach (var group in rows.GroupBy(x => string.IsNullOrWhiteSpace(x.IdentityType) ? "(unknown)" : x.IdentityType)
                         .OrderBy(x => x.Key))
            {
                summary.Append("  ");
                summary.Append(group.Key);
                summary.Append(": ");
                summary.AppendLine(group.Count().ToString(CultureInfo.InvariantCulture));
            }

            File.WriteAllText(path, summary.ToString(), Encoding.UTF8);
        }

        private void TryLogEvent(string category, string message)
        {
            try
            {
                this.LogEvent(category, message);
            }
            catch
            {
            }
        }

        private void TryWriteChat(ChatWindow chatWindow, string message, ChatColor color)
        {
            try
            {
                if (chatWindow != null)
                {
                    chatWindow.WriteLine(message, color);
                }
            }
            catch
            {
            }
        }

        private void OnSmokeCommand(string command, string[] args, ChatWindow chatWindow)
        {
            this.combatLootSmoke?.OnCommand(command, args, chatWindow);
        }

        private void OnPacketReceived(object sender, byte[] packet)
        {
            if (!this.enabled)
            {
                return;
            }

            this.inboundPacketCount++;
            this.lastPacketUtc = DateTime.UtcNow;
            this.LogPacket("IN", this.inboundPacketCount, packet);
        }

        private void OnPacketSent(object sender, byte[] packet)
        {
            if (!this.enabled)
            {
                return;
            }

            this.outboundPacketCount++;
            this.lastPacketUtc = DateTime.UtcNow;
            this.LogPacket("OUT", this.outboundPacketCount, packet);
        }

        private void OnN3MessageReceived(object sender, N3Message message)
        {
            this.combatLootSmoke?.OnN3MessageReceived(message);

            if (!this.enabled || message == null)
            {
                return;
            }

            this.decodedInboundCount++;
            this.lastPacketUtc = DateTime.UtcNow;
            this.LogN3Message("IN-N3", this.decodedInboundCount, message);
        }

        private void OnN3MessageSent(object sender, N3Message message)
        {
            this.combatLootSmoke?.OnN3MessageSent(message);

            if (!this.enabled || message == null)
            {
                return;
            }

            this.decodedOutboundCount++;
            this.lastPacketUtc = DateTime.UtcNow;
            this.LogN3Message("OUT-N3", this.decodedOutboundCount, message);
        }

        private void OnChatMessageReceived(object sender, ChatMessageBody message)
        {
            if (!this.enabled || message == null)
            {
                return;
            }

            this.LogEvent("CHAT", this.DescribeObject(message));
            this.LogChatDialogue("CHAT", 0, message.PacketType.ToString(), "chat-protocol", this.DescribeObject(message));
        }

        private void OnDynelSpawned(object sender, Dynel dynel)
        {
            if (!this.enabled || dynel == null)
            {
                return;
            }

            this.LogEvent("DYNEL-SPAWNED", this.DescribeDynel(dynel));
            this.TrackEnemyFromDynel(dynel, "spawn");
        }

        private void OnCharInPlay(object sender, SimpleChar character)
        {
            if (!this.enabled || character == null)
            {
                return;
            }

            this.LogEvent("CHAR-IN-PLAY", this.DescribeCharacter(character));
            this.TrackEnemyFromCharacter(character, "spawn");
        }

        private void OnPlayfieldInit(object sender, uint playfieldId)
        {
            this.lastPlayfieldId = playfieldId.ToString(CultureInfo.InvariantCulture);
            this.LogEvent("PLAYFIELD-INIT", this.lastPlayfieldId);
            this.knownCharacters.Clear();
            this.knownCorpses.Clear();
            this.LogSnapshot("playfield-init");
        }

        private void OnTeleportStarted(object sender, EventArgs e)
        {
            this.LogEvent("TELEPORT", "started");
        }

        private void OnTeleportEnded(object sender, EventArgs e)
        {
            this.LogEvent("TELEPORT", "ended");
            this.LogSnapshot("teleport-ended");
        }

        private void OnTeleportFailed(object sender, EventArgs e)
        {
            this.LogEvent("TELEPORT", "failed");
        }

        private void OnUpdate(object sender, float deltaTime)
        {
            this.combatLootSmoke?.Update(deltaTime);

            if (!this.enabled)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (now >= this.nextSnapshotUtc)
            {
                this.nextSnapshotUtc = now.AddSeconds(1);
                this.TrackDynelChanges();
            }

            if (now >= this.nextFlushUtc)
            {
                this.nextFlushUtc = now.AddSeconds(2);
                this.Flush();
            }
        }

        private void TrackDynelChanges()
        {
            try
            {
                HashSet<string> currentCharacters = new HashSet<string>();
                foreach (SimpleChar character in DynelManager.Characters.ToArray())
                {
                    string key = character.Identity.ToString();
                    currentCharacters.Add(key);
                    if (this.knownCharacters.Add(key))
                    {
                        this.LogEvent("CHAR-SEEN", this.DescribeCharacter(character));
                        this.TrackEnemyFromCharacter(character, "spawn");
                    }
                }

                foreach (string removed in this.knownCharacters.Except(currentCharacters).ToArray())
                {
                    this.LogEvent("CHAR-GONE", removed);
                    this.TrackEnemyGone(removed);
                    this.knownCharacters.Remove(removed);
                }

                HashSet<string> currentCorpses = new HashSet<string>();
                foreach (Corpse corpse in DynelManager.Corpses.ToArray())
                {
                    string key = corpse.Identity.ToString();
                    currentCorpses.Add(key);
                    if (this.knownCorpses.Add(key))
                    {
                        this.LogEvent("CORPSE-SEEN", this.DescribeCorpse(corpse));
                    }
                }

                foreach (string removed in this.knownCorpses.Except(currentCorpses).ToArray())
                {
                    this.LogEvent("CORPSE-GONE", removed);
                    this.knownCorpses.Remove(removed);
                }
            }
            catch (Exception ex)
            {
                this.LogEvent("SNAPSHOT-ERROR", ex.Message);
            }
        }

        private void LogSnapshot(string reason)
        {
            try
            {
                string localPlayer = DynelManager.LocalPlayer == null
                    ? "local=null"
                    : this.DescribeCharacter(DynelManager.LocalPlayer);

                this.LogEvent(
                    "SNAPSHOT",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "reason={0} server={1} clientInst={2} playfield={3} chars={4} npcs={5} corpses={6} dynels={7} {8}",
                        reason,
                        Safe(() => Game.ServerId.ToString(CultureInfo.InvariantCulture)),
                        Safe(() => Game.ClientInst.ToString(CultureInfo.InvariantCulture)),
                        Safe(() => Playfield.Identity.ToString()),
                        Safe(() => DynelManager.Characters.Count().ToString(CultureInfo.InvariantCulture)),
                        Safe(() => DynelManager.NPCs.Count().ToString(CultureInfo.InvariantCulture)),
                        Safe(() => DynelManager.Corpses.Count().ToString(CultureInfo.InvariantCulture)),
                        Safe(() => DynelManager.AllDynels.Count.ToString(CultureInfo.InvariantCulture)),
                        localPlayer));
            }
            catch (Exception ex)
            {
                this.LogEvent("SNAPSHOT-ERROR", ex.ToString());
            }
        }

        private void LogPacket(string direction, int sequence, byte[] packet)
        {
            lock (this.syncRoot)
            {
                this.packetsLog.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:o} {1} #{2} len={3} {4} hex={5}",
                        DateTime.UtcNow,
                        direction,
                        sequence,
                        packet == null ? 0 : packet.Length,
                        this.DescribeRawPacket(packet),
                        ToHex(packet)));
            }
        }

        private void LogN3Message(string direction, int sequence, N3Message message)
        {
            string messageName = message.N3MessageType.ToString();
            bool interesting = this.interestingMessageNames.Contains(messageName);
            string detail = interesting ? this.DescribeN3Message(message) : string.Empty;
            this.ExportSpecializedMessage(direction, sequence, message);
            if (this.enemyFightCaptureEnabled)
            {
                this.ExportEnemyN3Evidence(direction, sequence, message);
                this.TrackEnemyStateFromMessage(direction, sequence, message);
            }
            ShopUpdateMessage shopUpdate = message as ShopUpdateMessage;
            if (shopUpdate != null)
            {
                this.ExportShopUpdate(direction, sequence, shopUpdate);
            }

            VendingMachineFullUpdateMessage vendorFullUpdate = message as VendingMachineFullUpdateMessage;
            if (vendorFullUpdate != null)
            {
                this.ExportVendorFullUpdate(direction, sequence, vendorFullUpdate);
            }

            InventoryUpdateMessage inventoryUpdate = message as InventoryUpdateMessage;
            if (inventoryUpdate != null)
            {
                this.ExportInventoryUpdate(direction, sequence, inventoryUpdate);
            }

            this.LogEvent(
                direction,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "#{0} type={1} identity={2}{3}",
                    sequence,
                    message.N3MessageType,
                    message.Identity,
                    string.IsNullOrEmpty(detail) ? string.Empty : " " + detail));
        }

        private string DescribeN3Message(N3Message message)
        {
            ShopUpdateMessage shopUpdate = message as ShopUpdateMessage;
            if (shopUpdate != null)
            {
                return this.DescribeShopUpdate(shopUpdate);
            }

            return this.DescribeObject(message);
        }

        private void ExportSpecializedMessage(string direction, int sequence, N3Message message)
        {
            string messageName = message.N3MessageType.ToString();

            if (IsSystemMessage(messageName))
            {
                this.LogSystemMessage(direction, sequence, messageName, this.ExtractMessageText(message), this.DescribeObject(message));
            }

            if (IsDialogueMessage(messageName))
            {
                this.LogChatDialogue(direction, sequence, messageName, this.ExtractMessageText(message), this.DescribeObject(message));
            }

            GenericCmdMessage genericCmd = message as GenericCmdMessage;
            if (genericCmd != null)
            {
                this.TrackVendorInteraction(genericCmd);
                this.LogNpcInteraction(direction, sequence, messageName, this.ExtractMessageText(message), this.DescribeObject(message));
                return;
            }

            if (IsNpcInteractionMessage(messageName))
            {
                this.LogNpcInteraction(direction, sequence, messageName, this.ExtractMessageText(message), this.DescribeObject(message));
            }
        }

        private void TrackVendorInteraction(GenericCmdMessage message)
        {
            if (message.Action != GenericCmdAction.Use || message.Target.Type != IdentityType.VendingMachine)
            {
                return;
            }

            lock (this.syncRoot)
            {
                this.vendorInteractionAttemptCount++;
                this.vendorInteractionIdentities.Add(message.Target.ToString());
            }
        }

        private string ExtractMessageText(N3Message message)
        {
            ChatTextMessage chatText = message as ChatTextMessage;
            if (chatText != null)
            {
                return chatText.Text ?? string.Empty;
            }

            FormatFeedbackMessage formatFeedback = message as FormatFeedbackMessage;
            if (formatFeedback != null)
            {
                return formatFeedback.Message ?? string.Empty;
            }

            KnuBotAppendTextMessage appendText = message as KnuBotAppendTextMessage;
            if (appendText != null)
            {
                return appendText.Text ?? string.Empty;
            }

            KnuBotStartTradeMessage startTrade = message as KnuBotStartTradeMessage;
            if (startTrade != null)
            {
                return startTrade.Message ?? string.Empty;
            }

            KnuBotAnswerListMessage answerList = message as KnuBotAnswerListMessage;
            if (answerList != null)
            {
                KnuBotDialogOption[] options = answerList.DialogOptions ?? new KnuBotDialogOption[0];
                return string.Join(" | ", options.Select(option => option == null ? string.Empty : option.Text ?? string.Empty).ToArray());
            }

            return string.Empty;
        }

        private void LogSystemMessage(string direction, int sequence, string messageName, string text, string detail)
        {
            lock (this.syncRoot)
            {
                this.systemMessageCount++;
                this.systemMessagesLog.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:o} [{1}] #{2} type={3} text={4} detail={5}",
                        DateTime.UtcNow,
                        direction,
                        sequence,
                        messageName,
                        OneLine(text),
                        OneLine(detail)));
            }
        }

        private void LogChatDialogue(string direction, int sequence, string messageName, string text, string detail)
        {
            lock (this.syncRoot)
            {
                this.chatDialogueMessageCount++;
                this.chatDialogueLog.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:o} [{1}] #{2} type={3} text={4} detail={5}",
                        DateTime.UtcNow,
                        direction,
                        sequence,
                        messageName,
                        OneLine(text),
                        OneLine(detail)));
            }
        }

        private void LogNpcInteraction(string direction, int sequence, string messageName, string text, string detail)
        {
            lock (this.syncRoot)
            {
                this.npcInteractionCount++;
                this.npcInteractionsLog.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:o} [{1}] #{2} type={3} text={4} detail={5}",
                        DateTime.UtcNow,
                        direction,
                        sequence,
                        messageName,
                        OneLine(text),
                        OneLine(detail)));
            }
        }

        private static bool IsSystemMessage(string messageName)
        {
            return messageName == "Feedback"
                || messageName == "FormatFeedback"
                || messageName == "Quest"
                || messageName == "QuestFullUpdate"
                || messageName == "QuestAlternative"
                || messageName == "CreateQuest"
                || messageName == "NewLevel"
                || messageName == "ResearchUpdate"
                || messageName == "Stat";
        }

        private static bool IsDialogueMessage(string messageName)
        {
            return messageName == "ChatText"
                || messageName.StartsWith("Knubot", StringComparison.Ordinal);
        }

        private static bool IsNpcInteractionMessage(string messageName)
        {
            return messageName == "CharacterAction"
                || messageName == "InfromPlayer"
                || messageName.StartsWith("Knubot", StringComparison.Ordinal);
        }

        private string DescribeShopUpdate(ShopUpdateMessage message)
        {
            VendingMachineSlot[] slots = message.VendingMachineSlots ?? new VendingMachineSlot[0];
            StringBuilder result = new StringBuilder();
            result.Append("ShopUpdateMessage { ");
            result.Append("Unknown=");
            result.Append(message.Unknown.ToString(CultureInfo.InvariantCulture));
            result.Append(" VendingMachineSlots=count=");
            result.Append(slots.Length.ToString(CultureInfo.InvariantCulture));
            result.Append('[');

            for (int i = 0; i < slots.Length; i++)
            {
                VendingMachineSlot slot = slots[i];
                if (i > 0)
                {
                    result.Append(';');
                }

                result.Append('#');
                result.Append(i.ToString(CultureInfo.InvariantCulture));
                result.Append(":low=");
                result.Append(slot.ItemLowId.ToString(CultureInfo.InvariantCulture));
                result.Append(",high=");
                result.Append(slot.ItemHighId.ToString(CultureInfo.InvariantCulture));
                result.Append(",ql=");
                result.Append(slot.Quality.ToString(CultureInfo.InvariantCulture));
            }

            result.Append("] }");
            return result.ToString();
        }

        private void ExportShopUpdate(string direction, int sequence, ShopUpdateMessage message)
        {
            VendingMachineSlot[] slots = message.VendingMachineSlots ?? new VendingMachineSlot[0];
            string fingerprint = message.Identity + ":" + string.Join(
                ";",
                slots.Select(
                    slot => slot.ItemLowId.ToString(CultureInfo.InvariantCulture)
                        + "/"
                        + slot.ItemHighId.ToString(CultureInfo.InvariantCulture)
                        + ":"
                        + slot.Quality.ToString(CultureInfo.InvariantCulture)).ToArray());

            lock (this.syncRoot)
            {
                this.shopUpdateMessageCount++;
                this.shopUpdateIdentities.Add(message.Identity.ToString());
                if (!this.exportedShopUpdateFingerprints.Add(fingerprint))
                {
                    return;
                }

                string capturedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                string terminalIdentity = message.Identity.ToString();
                for (int i = 0; i < slots.Length; i++)
                {
                    VendingMachineSlot slot = slots[i];
                    this.shopUpdateRowCount++;
                    this.shopUpdatesLog.WriteLine(
                        string.Join(
                            ",",
                            Csv(capturedUtc),
                            Csv(direction),
                            sequence.ToString(CultureInfo.InvariantCulture),
                            Csv(terminalIdentity),
                            i.ToString(CultureInfo.InvariantCulture),
                            slot.ItemLowId.ToString(CultureInfo.InvariantCulture),
                            slot.ItemHighId.ToString(CultureInfo.InvariantCulture),
                            slot.Quality.ToString(CultureInfo.InvariantCulture)));
                }

                this.shopUpdatesLog.Flush();
            }

            this.LogEvent(
                "SHOP-EXPORT",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "terminal={0} slots={1} csv={2}",
                    message.Identity,
                    slots.Length,
                Path.Combine(this.sessionDirectory, "shop-updates.csv")));
        }

        private void ExportVendorFullUpdate(string direction, int sequence, VendingMachineFullUpdateMessage message)
        {
            GameTuple<Stat, int>[] stats = message.Stats ?? new GameTuple<Stat, int>[0];
            int template = GetStatValue(stats, (Stat)23);
            int mesh = GetStatValue(stats, (Stat)12);
            int buyModifier = GetStatValue(stats, (Stat)426);
            int sellModifier = GetStatValue(stats, (Stat)427);

            lock (this.syncRoot)
            {
                this.vendorFullUpdateMessageCount++;
                this.vendorFullUpdateIdentities.Add(message.Identity.ToString());
                this.vendorFullUpdatesLog.WriteLine(
                    string.Join(
                        ",",
                        Csv(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                        Csv(direction),
                        sequence.ToString(CultureInfo.InvariantCulture),
                        Csv(message.Identity.ToString()),
                        message.OwnerType.ToString(CultureInfo.InvariantCulture),
                        message.OwnerInstance.ToString(CultureInfo.InvariantCulture),
                        message.PlayfieldId.ToString(CultureInfo.InvariantCulture),
                        OptionalFloat(() => message.Position.Value.X),
                        OptionalFloat(() => message.Position.Value.Y),
                        OptionalFloat(() => message.Position.Value.Z),
                        message.Unknown7.ToString(CultureInfo.InvariantCulture),
                        template.ToString(CultureInfo.InvariantCulture),
                        mesh.ToString(CultureInfo.InvariantCulture),
                        buyModifier.ToString(CultureInfo.InvariantCulture),
                        sellModifier.ToString(CultureInfo.InvariantCulture),
                        stats.Length.ToString(CultureInfo.InvariantCulture)));
                this.vendorFullUpdatesLog.Flush();
            }
        }

        private void ExportInventoryUpdate(string direction, int sequence, InventoryUpdateMessage message)
        {
            InventorySlot[] items = message.Items ?? new InventorySlot[0];

            lock (this.syncRoot)
            {
                this.inventoryUpdateMessageCount++;
                string capturedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                string inventoryIdentity = message.InventoryIdentity.ToString();
                for (int i = 0; i < items.Length; i++)
                {
                    InventorySlot item = items[i];
                    this.inventoryUpdateRowCount++;
                    this.inventoryUpdatesLog.WriteLine(
                        string.Join(
                            ",",
                            Csv(capturedUtc),
                            Csv(direction),
                            sequence.ToString(CultureInfo.InvariantCulture),
                            Csv(inventoryIdentity),
                            message.Handle.ToString(CultureInfo.InvariantCulture),
                            i.ToString(CultureInfo.InvariantCulture),
                            item.Placement.ToString(CultureInfo.InvariantCulture),
                            item.Flags.ToString(CultureInfo.InvariantCulture),
                            item.Count.ToString(CultureInfo.InvariantCulture),
                            Csv(item.Identity.ToString()),
                            item.ItemLowId.ToString(CultureInfo.InvariantCulture),
                            item.ItemHighId.ToString(CultureInfo.InvariantCulture),
                            item.Quality.ToString(CultureInfo.InvariantCulture),
                            item.Unknown.ToString(CultureInfo.InvariantCulture)));
                }

                this.inventoryUpdatesLog.Flush();
            }
        }

        private void ExportEnemyN3Evidence(string direction, int sequence, N3Message message)
        {
            SimpleCharFullUpdateMessage simpleCharFullUpdate = message as SimpleCharFullUpdateMessage;
            if (simpleCharFullUpdate != null)
            {
                this.ExportEnemyFullUpdate(direction, sequence, simpleCharFullUpdate);
                return;
            }

            StatMessage stat = message as StatMessage;
            if (stat != null)
            {
                this.ExportEnemyStatUpdates(direction, sequence, message, message.Identity, GetMemberValue(stat, "Stats"), GetMemberValue(stat, "Position"));
                return;
            }

            SimpleItemFullUpdateMessage simpleItemFullUpdate = message as SimpleItemFullUpdateMessage;
            if (simpleItemFullUpdate != null)
            {
                this.ExportEnemyStatUpdates(
                    direction,
                    sequence,
                    message,
                    message.Identity,
                    GetMemberValue(simpleItemFullUpdate, "Stats"),
                    GetMemberValue(simpleItemFullUpdate, "Position"));
                return;
            }

            CharDCMoveMessage charMove = message as CharDCMoveMessage;
            if (charMove != null)
            {
                this.ExportEnemyMovement(
                    direction,
                    sequence,
                    message,
                    message.Identity,
                    GetMemberValue(charMove, "MoveType"),
                    GetMemberValue(charMove, "Position") ?? GetMemberValue(charMove, "Coordinates"),
                    GetMemberValue(charMove, "Heading"));
                return;
            }

            SetPosMessage setPos = message as SetPosMessage;
            if (setPos != null)
            {
                this.ExportEnemyMovement(
                    direction,
                    sequence,
                    message,
                    message.Identity,
                    string.Empty,
                    GetMemberValue(setPos, "Position") ?? GetMemberValue(setPos, "Coordinates"),
                    null);
                return;
            }

            DespawnMessage despawn = message as DespawnMessage;
            if (despawn != null)
            {
                this.ExportEnemyMovement(direction, sequence, message, message.Identity, "despawn", null, null);
                this.ExportEnemyCombat(direction, sequence, message, null, null, null);
                return;
            }

            if (message is AttackMessage
                || message is AttackInfoMessage
                || message is SpecialAttackInfoMessage
                || message is MissedAttackInfoMessage
                || message is HealthDamageMessage
                || message is CharacterActionMessage
                || message is StopFightMessage)
            {
                object target = GetMemberValue(message, "Target")
                    ?? GetMemberValue(message, "Defender")
                    ?? GetMemberValue(message, "Unknown4");
                object aux1 = GetMemberValue(message, "Unknown3");
                object aux2 = GetMemberValue(message, "Unknown4");

                this.ExportEnemyCombat(direction, sequence, message, target, aux1, aux2);
            }
        }

        private void ExportEnemyFullUpdate(string direction, int sequence, SimpleCharFullUpdateMessage message)
        {
            object characterInfo = GetMemberValue(message, "CharacterInfo");
            if (!this.IsNpcCharacterInfo(characterInfo))
            {
                return;
            }

            object position = GetMemberValue(message, "Position") ?? GetMemberValue(message, "Coordinates");
            object heading = GetMemberValue(message, "Heading");
            object fightingTarget = GetMemberValue(message, "FightingTarget");
            string fightingTargetRole;
            string fightingTargetIdentity;
            this.DescribeIdentityForEnemyOutput(fightingTarget, out fightingTargetRole, out fightingTargetIdentity);

            lock (this.syncRoot)
            {
                this.enemyFullUpdateRowCount++;
                this.enemyFullUpdatesLog.WriteLine(
                    string.Join(
                        ",",
                        Csv(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                        Csv(direction),
                        sequence.ToString(CultureInfo.InvariantCulture),
                        Csv(message.Identity.ToString()),
                        Csv(GetMemberString(message, "Name")),
                        Csv(GetMemberString(message, "PlayfieldId")),
                        MemberComponent(position, "X"),
                        MemberComponent(position, "Y"),
                        MemberComponent(position, "Z"),
                        MemberComponent(heading, "X"),
                        MemberComponent(heading, "Y"),
                        MemberComponent(heading, "Z"),
                        MemberComponent(heading, "W"),
                        Csv(fightingTargetRole),
                        Csv(fightingTargetIdentity),
                        Csv(GetMemberString(message, "Version")),
                        Csv(GetMemberString(message, "Flags")),
                        Csv(GetMemberString(message, "CharacterFlags")),
                        Csv(GetMemberString(message, "AccountFlags")),
                        Csv(GetMemberString(message, "Expansions")),
                        Csv(characterInfo == null ? string.Empty : characterInfo.GetType().Name),
                        Csv(GetMemberString(characterInfo, "Family")),
                        Csv(GetMemberString(characterInfo, "LosHeight")),
                        Csv(GetMemberString(message, "Level")),
                        Csv(GetMemberString(message, "Health")),
                        Csv(GetMemberString(message, "HealthDamage")),
                        Csv(GetMemberString(message, "MonsterData")),
                        Csv(GetMemberString(message, "MonsterScale")),
                        Csv(GetMemberString(message, "VisualFlags")),
                        Csv(GetMemberString(message, "VisibleTitle")),
                        Csv(GetByteArrayLengthString(GetMemberValue(message, "Unknown1"))),
                        Csv(GetMemberString(message, "HeadMesh")),
                        Csv(GetMemberString(message, "RunSpeedBase")),
                        Csv(GetCountString(GetMemberValue(message, "ActiveNanos"))),
                        Csv(GetCountString(GetMemberValue(message, "Textures"))),
                        Csv(FormatValue(GetMemberValue(message, "Textures"))),
                        Csv(GetCountString(GetMemberValue(message, "Meshes"))),
                        Csv(FormatValue(GetMemberValue(message, "Meshes"))),
                        Csv(GetMemberString(message, "Flags2")),
                        Csv(GetMemberString(message, "Unknown2")),
                        Csv(this.DescribeObject(message))));
                this.enemyFullUpdatesLog.Flush();
            }
        }

        private void ExportEnemyCombat(string direction, int sequence, N3Message message, object target, object aux1, object aux2)
        {
            string sourceRole;
            string sourceIdentity;
            this.DescribeIdentityForEnemyOutput(message.Identity, out sourceRole, out sourceIdentity);

            string targetRole;
            string targetIdentity;
            this.DescribeIdentityForEnemyOutput(target, out targetRole, out targetIdentity);

            string auxRole1;
            string auxIdentity1;
            this.DescribeIdentityForEnemyOutput(aux1, out auxRole1, out auxIdentity1);

            string auxRole2;
            string auxIdentity2;
            this.DescribeIdentityForEnemyOutput(aux2, out auxRole2, out auxIdentity2);

            DateTime capturedUtc = DateTime.UtcNow;
            bool hasEnemyRole = IsEnemyRole(sourceRole)
                || IsEnemyRole(targetRole)
                || IsEnemyRole(auxRole1)
                || IsEnemyRole(auxRole2);
            bool hasLocalPlayerRole = IsLocalPlayerRole(sourceRole)
                || IsLocalPlayerRole(targetRole)
                || IsLocalPlayerRole(auxRole1)
                || IsLocalPlayerRole(auxRole2);

            lock (this.syncRoot)
            {
                if (hasEnemyRole && hasLocalPlayerRole)
                {
                    this.localEnemyCombatContextUntilUtc = capturedUtc.AddSeconds(LocalEnemyCombatContextSeconds);
                }

                bool includeLocalTerminalRow = hasLocalPlayerRole
                    && IsLocalPlayerCombatTerminalMessage(message, sourceRole)
                    && capturedUtc <= this.localEnemyCombatContextUntilUtc;
                if (!hasEnemyRole && !includeLocalTerminalRow)
                {
                    return;
                }

                this.enemyCombatRowCount++;
                this.enemyCombatLog.WriteLine(
                    string.Join(
                        ",",
                        Csv(capturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                        Csv(direction),
                        sequence.ToString(CultureInfo.InvariantCulture),
                        Csv(message.N3MessageType.ToString()),
                        Csv(sourceRole),
                        Csv(sourceIdentity),
                        Csv(targetRole),
                        Csv(targetIdentity),
                        Csv(auxRole1),
                        Csv(auxIdentity1),
                        Csv(auxRole2),
                        Csv(auxIdentity2),
                        Csv(GetMemberString(message, "Action")),
                        Csv(GetMemberString(message, "Amount")),
                        Csv(GetMemberString(message, "TargetHp")),
                        Csv(GetMemberString(message, "Unknown1")),
                        Csv(GetMemberString(message, "Unknown2")),
                        Csv(GetMemberString(message, "Unknown3")),
                        Csv(GetMemberString(message, "Unknown4")),
                        Csv(GetMemberString(message, "Unknown5")),
                        Csv(GetMemberString(message, "Unknown6")),
                        Csv(this.DescribeObject(message))));
                this.enemyCombatLog.Flush();
            }
        }

        private void ExportEnemyMovement(
            string direction,
            int sequence,
            N3Message message,
            object identity,
            object moveType,
            object position,
            object heading)
        {
            string role;
            string safeIdentity;
            this.DescribeIdentityForEnemyOutput(identity, out role, out safeIdentity);
            if (!IsEnemyRole(role))
            {
                return;
            }

            lock (this.syncRoot)
            {
                this.enemyMovementRowCount++;
                this.enemyMovementLog.WriteLine(
                    string.Join(
                        ",",
                        Csv(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                        Csv(direction),
                        sequence.ToString(CultureInfo.InvariantCulture),
                        Csv(message.N3MessageType.ToString()),
                        Csv(role),
                        Csv(safeIdentity),
                        Csv(FormatObjectForCsv(moveType)),
                        MemberComponent(position, "X"),
                        MemberComponent(position, "Y"),
                        MemberComponent(position, "Z"),
                        MemberComponent(heading, "X"),
                        MemberComponent(heading, "Y"),
                        MemberComponent(heading, "Z"),
                        MemberComponent(heading, "W"),
                        Csv(GetMemberString(message, "Unknown1")),
                        Csv(GetMemberString(message, "Unknown2")),
                        Csv(GetMemberString(message, "Unknown3")),
                        Csv(this.DescribeObject(message))));
                this.enemyMovementLog.Flush();
            }
        }

        private void ExportEnemyStatUpdates(
            string direction,
            int sequence,
            N3Message message,
            object identity,
            object statsObject,
            object position)
        {
            string role;
            string safeIdentity;
            this.DescribeIdentityForEnemyOutput(identity, out role, out safeIdentity);
            if (!IsEnemyRole(role))
            {
                return;
            }

            IEnumerable stats = statsObject as IEnumerable;
            if (stats == null)
            {
                return;
            }

            string capturedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            string statsCount = GetCountString(statsObject);
            string detail = this.DescribeObject(message);

            lock (this.syncRoot)
            {
                foreach (object stat in stats)
                {
                    object statName;
                    object statValue;
                    if (!TryGetGameTupleValues(stat, out statName, out statValue))
                    {
                        continue;
                    }

                    this.enemyStatUpdateRowCount++;
                    this.enemyStatUpdatesLog.WriteLine(
                        string.Join(
                            ",",
                            Csv(capturedUtc),
                            Csv(direction),
                            sequence.ToString(CultureInfo.InvariantCulture),
                            Csv(message.N3MessageType.ToString()),
                            Csv(role),
                            Csv(safeIdentity),
                            Csv(FormatObjectForCsv(statName)),
                            Csv(GetStatNumericValue(statName)),
                            Csv(FormatObjectForCsv(statValue)),
                            MemberComponent(position, "X"),
                            MemberComponent(position, "Y"),
                            MemberComponent(position, "Z"),
                            Csv(statsCount),
                            Csv(detail)));
                }

                this.enemyStatUpdatesLog.Flush();
            }
        }

        private void TrackEnemyStateFromMessage(string direction, int sequence, N3Message message)
        {
            SimpleCharFullUpdateMessage simpleCharFullUpdate = message as SimpleCharFullUpdateMessage;
            if (simpleCharFullUpdate != null)
            {
                this.TrackEnemyFromSimpleCharFullUpdate(direction, sequence, simpleCharFullUpdate);
                return;
            }

            StatMessage stat = message as StatMessage;
            if (stat != null)
            {
                this.TrackEnemyFromStatMessage(direction, sequence, stat);
                return;
            }

            HealthDamageMessage healthDamage = message as HealthDamageMessage;
            if (healthDamage != null)
            {
                this.TrackEnemyFromHealthDamage(direction, sequence, healthDamage);
                return;
            }

            AttackInfoMessage attackInfo = message as AttackInfoMessage;
            if (attackInfo != null)
            {
                bool didDamage = attackInfo.Amount > 0;
                this.TrackEnemyCombatTarget(direction, sequence, attackInfo.Target, didDamage ? "damage" : "update", didDamage);
                return;
            }

            SpecialAttackInfoMessage specialAttackInfo = message as SpecialAttackInfoMessage;
            if (specialAttackInfo != null)
            {
                bool didDamage = specialAttackInfo.Amount > 0;
                this.TrackEnemyCombatTarget(direction, sequence, specialAttackInfo.Target, didDamage ? "damage" : "update", didDamage);
                return;
            }

            AttackMessage attack = message as AttackMessage;
            if (attack != null)
            {
                this.TrackEnemyCombatTarget(direction, sequence, attack.Target, "update", false);
                return;
            }

            MissedAttackInfoMessage missedAttackInfo = message as MissedAttackInfoMessage;
            if (missedAttackInfo != null)
            {
                this.TrackEnemyCombatTarget(direction, sequence, missedAttackInfo.Defender, "update", false);
                return;
            }

            CharacterActionMessage characterAction = message as CharacterActionMessage;
            if (characterAction != null)
            {
                this.TrackEnemyCharacterAction(direction, sequence, characterAction);
                return;
            }

            CharDCMoveMessage charMove = message as CharDCMoveMessage;
            if (charMove != null)
            {
                this.TrackEnemyPosition(direction, sequence, charMove.Identity, charMove.Position, "update");
                return;
            }

            SetPosMessage setPos = message as SetPosMessage;
            if (setPos != null)
            {
                this.TrackEnemyPosition(direction, sequence, setPos.Identity, setPos.Position, "update");
                return;
            }

            DespawnMessage despawn = message as DespawnMessage;
            if (despawn != null)
            {
                this.TrackEnemyDespawn(direction, sequence, despawn.Identity);
                return;
            }

            SimpleItemFullUpdateMessage simpleItemFullUpdate = message as SimpleItemFullUpdateMessage;
            if (simpleItemFullUpdate != null)
            {
                this.TrackEnemyFromSimpleItemFullUpdate(direction, sequence, simpleItemFullUpdate);
            }
        }

        private void TrackEnemyCharacterAction(string direction, int sequence, CharacterActionMessage message)
        {
            if (!string.Equals(GetMemberString(message, "Action"), "Death", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!this.IsTrackableEnemyIdentity(message.Identity))
            {
                return;
            }

            DateTime timestamp = DateTime.UtcNow;
            lock (this.syncRoot)
            {
                bool created;
                EnemyEntityState state = this.GetOrCreateEnemyState(message.Identity, timestamp, out created);
                this.enemyCombatEventCount++;
                if (created)
                {
                    this.RecordEnemyStateEvent(state, timestamp, "spawn");
                }

                this.RecordEnemyDeath(state, timestamp);
            }
        }

        private void TrackEnemyFromSimpleCharFullUpdate(string direction, int sequence, SimpleCharFullUpdateMessage message)
        {
            if (!this.IsEnemySimpleCharUpdate(message))
            {
                return;
            }

            DateTime timestamp = DateTime.UtcNow;
            lock (this.syncRoot)
            {
                bool created;
                EnemyEntityState state = this.GetOrCreateEnemyState(message.Identity, timestamp, out created);
                state.Level = message.Level;

                if (message.Health > 0)
                {
                    state.MaxHealth = message.Health;
                    state.CurrentHealth = message.HealthDamage > 0 && message.Health >= message.HealthDamage
                        ? message.Health - message.HealthDamage
                        : message.Health;
                    this.enemyHealthUpdateCount++;
                }

                if (this.UpdateEnemyPosition(state, message.Position))
                {
                    this.enemyPositionUpdateCount++;
                }

                this.RecordEnemyStateEvent(state, timestamp, created ? "spawn" : "update");
                this.RecordEnemyDeathIfNeeded(state, timestamp);
            }
        }

        private void TrackEnemyFromStatMessage(string direction, int sequence, StatMessage message)
        {
            if (!this.IsTrackableEnemyIdentity(message.Identity))
            {
                return;
            }

            GameTuple<Stat, uint>[] stats = message.Stats ?? new GameTuple<Stat, uint>[0];
            if (!ContainsEnemyStateStats(stats))
            {
                return;
            }

            DateTime timestamp = DateTime.UtcNow;
            lock (this.syncRoot)
            {
                bool created;
                EnemyEntityState state = this.GetOrCreateEnemyState(message.Identity, timestamp, out created);
                bool changed = false;
                foreach (GameTuple<Stat, uint> stat in stats)
                {
                    changed |= this.ApplyEnemyStat(state, stat.Value1, ToInt32Clamp(stat.Value2));
                }

                if (changed)
                {
                    this.RecordEnemyStateEvent(state, timestamp, created ? "spawn" : "update");
                    this.RecordEnemyDeathIfNeeded(state, timestamp);
                }
            }
        }

        private void TrackEnemyFromSimpleItemFullUpdate(string direction, int sequence, SimpleItemFullUpdateMessage message)
        {
            if (!this.IsTrackableEnemyIdentity(message.Identity))
            {
                return;
            }

            GameTuple<Stat, int>[] stats = message.Stats ?? new GameTuple<Stat, int>[0];
            if (!ContainsEnemyStateStats(stats) && !message.Position.HasValue)
            {
                return;
            }

            DateTime timestamp = DateTime.UtcNow;
            lock (this.syncRoot)
            {
                bool created;
                EnemyEntityState state = this.GetOrCreateEnemyState(message.Identity, timestamp, out created);
                bool changed = false;
                foreach (GameTuple<Stat, int> stat in stats)
                {
                    changed |= this.ApplyEnemyStat(state, stat.Value1, stat.Value2);
                }

                if (message.Position.HasValue && this.UpdateEnemyPosition(state, message.Position.Value))
                {
                    changed = true;
                    this.enemyPositionUpdateCount++;
                }

                if (changed)
                {
                    this.RecordEnemyStateEvent(state, timestamp, created ? "spawn" : "update");
                    this.RecordEnemyDeathIfNeeded(state, timestamp);
                }
            }
        }

        private void TrackEnemyFromHealthDamage(string direction, int sequence, HealthDamageMessage message)
        {
            if (!this.IsTrackableEnemyIdentity(message.Target))
            {
                return;
            }

            DateTime timestamp = DateTime.UtcNow;
            lock (this.syncRoot)
            {
                bool created;
                EnemyEntityState state = this.GetOrCreateEnemyState(message.Target, timestamp, out created);
                this.enemyCombatEventCount++;
                this.enemyDamageEventCount++;
                state.CurrentHealth = message.TargetHp;
                this.enemyHealthUpdateCount++;
                if (created)
                {
                    this.RecordEnemyStateEvent(state, timestamp, "spawn");
                }

                this.RecordEnemyStateEvent(state, timestamp, "damage");
                this.RecordEnemyDeathIfNeeded(state, timestamp);
            }
        }

        private void TrackEnemyCombatTarget(string direction, int sequence, Identity target, string eventType, bool isDamage)
        {
            if (!this.IsTrackableEnemyIdentity(target))
            {
                return;
            }

            DateTime timestamp = DateTime.UtcNow;
            lock (this.syncRoot)
            {
                bool created;
                EnemyEntityState state = this.GetOrCreateEnemyState(target, timestamp, out created);
                this.enemyCombatEventCount++;
                if (isDamage)
                {
                    this.enemyDamageEventCount++;
                }

                if (created)
                {
                    this.RecordEnemyStateEvent(state, timestamp, "spawn");
                }

                this.RecordEnemyStateEvent(state, timestamp, eventType);
            }
        }

        private void TrackEnemyPosition(string direction, int sequence, Identity identity, Vector3 position, string eventType)
        {
            if (!this.IsTrackableEnemyIdentity(identity))
            {
                return;
            }

            DateTime timestamp = DateTime.UtcNow;
            lock (this.syncRoot)
            {
                bool created;
                EnemyEntityState state = this.GetOrCreateEnemyState(identity, timestamp, out created);
                if (this.UpdateEnemyPosition(state, position))
                {
                    this.enemyPositionUpdateCount++;
                    this.RecordEnemyStateEvent(state, timestamp, created ? "spawn" : eventType);
                }
            }
        }

        private void TrackEnemyDespawn(string direction, int sequence, Identity identity)
        {
            if (!this.IsTrackableEnemyIdentity(identity))
            {
                return;
            }

            DateTime timestamp = DateTime.UtcNow;
            lock (this.syncRoot)
            {
                bool created;
                EnemyEntityState state = this.GetOrCreateEnemyState(identity, timestamp, out created);
                if (created)
                {
                    this.RecordEnemyStateEvent(state, timestamp, "spawn");
                }

                this.RecordEnemyStateEvent(state, timestamp, "despawn");
            }
        }

        private void TrackEnemyFromDynel(Dynel dynel, string requestedEventType)
        {
            if (!this.enemyFightCaptureEnabled)
            {
                return;
            }

            if (dynel == null || dynel.Identity.Type != IdentityType.SimpleChar)
            {
                return;
            }

            try
            {
                this.TrackEnemyFromCharacter(dynel.Cast<SimpleChar>(), requestedEventType);
            }
            catch
            {
                // Dynel snapshots are best-effort; decoded packets remain the capture source of truth.
            }
        }

        private void TrackEnemyFromCharacter(SimpleChar character, string requestedEventType)
        {
            if (!this.IsEnemyCharacter(character))
            {
                return;
            }

            DateTime timestamp = DateTime.UtcNow;
            lock (this.syncRoot)
            {
                bool created;
                EnemyEntityState state = this.GetOrCreateEnemyState(character.Identity, timestamp, out created);
                state.Level = TryGetCharacterStat(character, Stat.Level);
                state.CurrentHealth = TryGetCharacterStat(character, Stat.Health);
                state.MaxHealth = TryGetCharacterStat(character, Stat.MaxHealth);
                this.enemyHealthUpdateCount++;
                if (this.UpdateEnemyPosition(state, character.Position))
                {
                    this.enemyPositionUpdateCount++;
                }

                this.RecordEnemyStateEvent(state, timestamp, created ? "spawn" : requestedEventType == "spawn" ? "update" : requestedEventType);
                this.RecordEnemyDeathIfNeeded(state, timestamp);
            }
        }

        private void TrackEnemyGone(string entityId)
        {
            if (!this.enemyFightCaptureEnabled)
            {
                return;
            }

            DateTime timestamp = DateTime.UtcNow;
            lock (this.syncRoot)
            {
                EnemyEntityState state;
                if (!this.enemyStates.TryGetValue(entityId, out state))
                {
                    return;
                }

                this.RecordEnemyStateEvent(state, timestamp, "despawn");
            }
        }

        private EnemyEntityState GetOrCreateEnemyState(Identity identity, DateTime timestamp, out bool created)
        {
            string entityId = identity.ToString();
            EnemyEntityState state;
            if (this.enemyStates.TryGetValue(entityId, out state))
            {
                created = false;
                return state;
            }

            state = new EnemyEntityState
            {
                EntityId = entityId,
                FirstSeenUtc = timestamp,
                LastUpdateUtc = timestamp
            };
            this.enemyStates.Add(entityId, state);
            this.enemyStateTimeline.Add(entityId, new List<EnemyStateEvent>());
            created = true;
            return state;
        }

        private bool ApplyEnemyStat(EnemyEntityState state, Stat stat, int value)
        {
            switch (stat)
            {
                case Stat.Health:
                    state.CurrentHealth = value;
                    this.enemyHealthUpdateCount++;
                    return true;

                case Stat.MaxHealth:
                    state.MaxHealth = value;
                    this.enemyHealthUpdateCount++;
                    return true;

                case Stat.Level:
                    state.Level = value;
                    return true;

                default:
                    return false;
            }
        }

        private bool UpdateEnemyPosition(EnemyEntityState state, Vector3 position)
        {
            bool changed = state.X != position.X || state.Y != position.Y || state.Z != position.Z;
            state.X = position.X;
            state.Y = position.Y;
            state.Z = position.Z;
            return changed;
        }

        private void RecordEnemyDeathIfNeeded(EnemyEntityState state, DateTime timestamp)
        {
            if (!state.CurrentHealth.HasValue || state.CurrentHealth.Value > 0 || state.DeathLogged)
            {
                return;
            }

            this.RecordEnemyDeath(state, timestamp);
        }

        private void RecordEnemyDeath(EnemyEntityState state, DateTime timestamp)
        {
            if (state.DeathLogged)
            {
                return;
            }

            state.DeathLogged = true;
            this.RecordEnemyStateEvent(state, timestamp, "death");
        }

        private void RecordEnemyStateEvent(EnemyEntityState state, DateTime timestamp, string eventType)
        {
            state.LastUpdateUtc = timestamp;
            EnemyStateEvent stateEvent = new EnemyStateEvent
            {
                TimestampUtc = timestamp,
                EntityId = state.EntityId,
                Level = state.Level,
                CurrentHealth = state.CurrentHealth,
                MaxHealth = state.MaxHealth,
                X = state.X,
                Y = state.Y,
                Z = state.Z,
                EventType = eventType
            };

            List<EnemyStateEvent> timeline;
            if (!this.enemyStateTimeline.TryGetValue(state.EntityId, out timeline))
            {
                timeline = new List<EnemyStateEvent>();
                this.enemyStateTimeline.Add(state.EntityId, timeline);
            }

            timeline.Add(stateEvent);
            this.enemyStateRowCount++;
            if (eventType == "spawn")
            {
                this.enemySpawnEventCount++;
            }
            else if (eventType == "death")
            {
                this.enemyDeathEventCount++;
            }
            else if (eventType == "despawn")
            {
                this.enemyDespawnEventCount++;
            }

            this.enemyStateLog.WriteLine(
                string.Join(
                    ",",
                    Csv(timestamp.ToString("o", CultureInfo.InvariantCulture)),
                    Csv(state.EntityId),
                    NullableInt(state.Level),
                    NullableInt(state.CurrentHealth),
                    NullableInt(state.MaxHealth),
                    NullableFloat(state.X),
                    NullableFloat(state.Y),
                    NullableFloat(state.Z),
                    Csv(eventType)));
        }

        private bool IsEnemySimpleCharUpdate(SimpleCharFullUpdateMessage message)
        {
            if (!this.IsTrackableEnemyIdentity(message.Identity))
            {
                return false;
            }

            if (message.CharacterInfo is SimpleCharInfo.NPCInfo)
            {
                return true;
            }

            return this.enemyStates.ContainsKey(message.Identity.ToString());
        }

        private bool IsTrackableEnemyIdentity(Identity identity)
        {
            return identity.Type == IdentityType.SimpleChar && !this.IsLocalPlayerIdentity(identity);
        }

        private bool IsLocalPlayerIdentity(Identity identity)
        {
            try
            {
                return DynelManager.LocalPlayer != null && identity.Equals(DynelManager.LocalPlayer.Identity);
            }
            catch
            {
                return false;
            }
        }

        private void DescribeIdentityForEnemyOutput(object identityValue, out string role, out string safeIdentity)
        {
            role = string.Empty;
            safeIdentity = string.Empty;

            string identityText = FormatIdentityValue(identityValue);
            if (string.IsNullOrWhiteSpace(identityText))
            {
                return;
            }

            if (identityText == this.GetLocalPlayerIdentityString())
            {
                role = "local-player";
                return;
            }

            string identityType = GetIdentityTypeName(identityValue);
            role = identityType;
            safeIdentity = identityText;

            if (this.enemyStates.ContainsKey(identityText) || this.IsNpcDynelIdentity(identityValue))
            {
                role = "enemy";
            }
        }

        private bool IsNpcDynelIdentity(object identityValue)
        {
            try
            {
                if (!(identityValue is Identity))
                {
                    return false;
                }

                Identity identity = (Identity)identityValue;
                if (identity.Type != IdentityType.SimpleChar)
                {
                    return false;
                }

                Dynel dynel = DynelManager.GetDynel(identity);
                if (dynel == null)
                {
                    return false;
                }

                SimpleChar character = dynel.Cast<SimpleChar>();
                return SafeBool(() => character.IsNpc) || SafeBool(() => character.IsPet);
            }
            catch
            {
                return false;
            }
        }

        private bool IsNpcCharacterInfo(object characterInfo)
        {
            if (characterInfo == null)
            {
                return false;
            }

            string typeName = characterInfo.GetType().Name;
            return typeName.IndexOf("npc", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsEnemyRole(string role)
        {
            return string.Equals(role, "enemy", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLocalPlayerRole(string role)
        {
            return string.Equals(role, "local-player", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLocalPlayerCombatTerminalMessage(N3Message message, string sourceRole)
        {
            if (!IsLocalPlayerRole(sourceRole))
            {
                return false;
            }

            if (message is StopFightMessage)
            {
                return true;
            }

            if (message is CharacterActionMessage)
            {
                return string.Equals(GetMemberString(message, "Action"), "Death", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private string GetLocalPlayerIdentityString()
        {
            return Safe(() => DynelManager.LocalPlayer == null ? string.Empty : DynelManager.LocalPlayer.Identity.ToString());
        }

        private bool IsEnemyCharacter(SimpleChar character)
        {
            if (character == null || this.IsLocalPlayerIdentity(character.Identity))
            {
                return false;
            }

            return SafeBool(() => character.IsNpc) || SafeBool(() => character.IsPet);
        }

        private static bool ContainsEnemyStateStats(GameTuple<Stat, uint>[] stats)
        {
            foreach (GameTuple<Stat, uint> stat in stats)
            {
                if (IsEnemyStateStat(stat.Value1))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsEnemyStateStats(GameTuple<Stat, int>[] stats)
        {
            foreach (GameTuple<Stat, int> stat in stats)
            {
                if (IsEnemyStateStat(stat.Value1))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEnemyStateStat(Stat stat)
        {
            return stat == Stat.Health || stat == Stat.MaxHealth || stat == Stat.Level;
        }

        private static int? TryGetCharacterStat(SimpleChar character, Stat stat)
        {
            try
            {
                return character.GetStat(stat);
            }
            catch
            {
                return null;
            }
        }

        private void WriteCaptureSessionMetadata(DateTime captureStartUtc, DateTime captureStartLocal)
        {
            try
            {
                string path = Path.Combine(this.sessionDirectory, "capture-session.json");
                Process process = Process.GetCurrentProcess();
                try
                {
                    StringBuilder json = new StringBuilder();
                    json.AppendLine("{");
                    json.Append("  \"captureStartUtc\": ");
                    json.Append(Json(captureStartUtc.ToString("o", CultureInfo.InvariantCulture)));
                    json.AppendLine(",");
                    json.Append("  \"captureStartLocal\": ");
                    json.Append(Json(captureStartLocal.ToString("o", CultureInfo.InvariantCulture)));
                    json.AppendLine(",");
                    json.Append("  \"captureFolderPath\": ");
                    json.Append(Json(this.sessionDirectory));
                    json.AppendLine(",");
                    json.AppendLine("  \"aoClientProcess\": {");
                    json.Append("    \"id\": ");
                    json.Append(process.Id.ToString(CultureInfo.InvariantCulture));
                    json.AppendLine(",");
                    json.Append("    \"processName\": ");
                    json.Append(Json(Safe(() => process.ProcessName)));
                    json.AppendLine(",");
                    json.Append("    \"mainWindowTitle\": ");
                    json.Append(Json(Safe(() => process.MainWindowTitle)));
                    json.AppendLine();
                    json.AppendLine("  },");
                    json.Append("  \"notes\": ");
                    json.Append(Json(string.Empty));
                    json.AppendLine();
                    json.AppendLine("}");

                    File.WriteAllText(path, json.ToString(), Encoding.UTF8);
                }
                finally
                {
                    process.Dispose();
                }
            }
            catch (Exception ex)
            {
                this.LogEvent("SESSION-METADATA-ERROR", ex.ToString());
            }
        }

        private void FinalizeCapture()
        {
            if (this.captureFinalized)
            {
                return;
            }

            this.captureFinalized = true;
            this.enabled = false;

            this.LogEvent("PLUGIN", "Final capture flush delay starting: 5 seconds.");
            this.Flush();
            Thread.Sleep(TimeSpan.FromSeconds(5));
            this.Flush();
            this.WriteEnemyStateJson();

            CaptureValidation validation = this.ValidateCapture();
            this.WriteCaptureHealth(validation);
            this.WriteCaptureInfo(DateTime.UtcNow, validation);
            this.LogEvent(
                "CAPTURE-VALIDATION",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "status={0} processingAllowed={1} issues={2}",
                    validation.Status,
                    validation.ProcessingAllowed,
                    validation.Issues.Count));

            this.FlushAndClose();
        }

        private CaptureValidation ValidateCapture()
        {
            List<string> issues = new List<string>();
            List<string> notes = new List<string>();

            if (this.GetSessionFileLength("events.log") <= 0)
            {
                issues.Add("events.log is empty or missing.");
            }

            if (this.GetSessionFileLength("packets.hex.log") <= 0)
            {
                notes.Add("packets.hex.log is empty; no raw packets were observed in this session.");
            }

            if (this.vendorInteractionAttemptCount > 0 && this.shopUpdateRowCount == 0)
            {
                issues.Add("Vendor/shop interactions were observed, but shop-updates.csv has no stock rows.");
            }

            if (this.vendorInteractionAttemptCount > 0 && this.vendorFullUpdateMessageCount == 0)
            {
                issues.Add("Vendor/shop interactions were observed, but vendor-full-updates.csv has no vendor full-update entries.");
            }

            if (this.shopUpdateMessageCount > 0 && this.shopUpdateRowCount == 0)
            {
                issues.Add("ShopUpdate messages were observed, but all exported shop updates were empty.");
            }

            if (this.vendorInteractionAttemptCount == 0)
            {
                notes.Add("No GenericCmd Use against VendingMachine identities was observed; shop-specific row checks are informational only.");
            }

            if (this.chatDialogueMessageCount == 0)
            {
                notes.Add("No chat/dialogue messages were observed.");
            }

            if (this.systemMessageCount == 0)
            {
                notes.Add("No system/feedback/quest messages were observed.");
            }

            if (this.enemyFightCaptureStarted
                && (this.enemyCombatEventCount > 0 || this.enemyCombatRowCount > 0)
                && this.enemyStateRowCount == 0)
            {
                issues.Add("Combat packets were observed, but enemy-state.csv has no rows.");
            }

            if (!this.enemyFightCaptureStarted)
            {
                notes.Add("Enemy fight capture was not started; enemy behavior CSVs are intentionally gated.");
            }
            else if (this.enemyCombatEventCount == 0 && this.enemyCombatRowCount == 0)
            {
                notes.Add("No enemy combat packets were observed.");
            }

            string status = issues.Count == 0 ? "complete" : "incomplete";
            return new CaptureValidation(status, issues.Count == 0, issues, notes);
        }

        private void WriteEnemyStateJson()
        {
            try
            {
                string path = Path.Combine(this.sessionDirectory, "enemy-state.json");
                StringBuilder json = new StringBuilder();
                lock (this.syncRoot)
                {
                    json.AppendLine("{");
                    json.Append("  \"generatedUtc\": ");
                    json.Append(Json(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
                    json.AppendLine(",");
                    json.AppendLine("  \"entities\": {");

                    string[] entityIds = this.enemyStateTimeline.Keys.OrderBy(value => value).ToArray();
                    for (int i = 0; i < entityIds.Length; i++)
                    {
                        string entityId = entityIds[i];
                        json.Append("    ");
                        json.Append(Json(entityId));
                        json.AppendLine(": [");

                        List<EnemyStateEvent> timeline = this.enemyStateTimeline[entityId];
                        for (int j = 0; j < timeline.Count; j++)
                        {
                            this.AppendEnemyStateEventJson(json, timeline[j], "      ");
                            if (j < timeline.Count - 1)
                            {
                                json.Append(",");
                            }

                            json.AppendLine();
                        }

                        json.Append("    ]");
                        if (i < entityIds.Length - 1)
                        {
                            json.Append(",");
                        }

                        json.AppendLine();
                    }

                    json.AppendLine("  }");
                    json.AppendLine("}");
                }

                File.WriteAllText(path, json.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                this.LogEvent("ENEMY-STATE-JSON-ERROR", ex.ToString());
            }
        }

        private void AppendEnemyStateEventJson(StringBuilder json, EnemyStateEvent stateEvent, string indent)
        {
            json.Append(indent);
            json.Append("{ ");
            json.Append("\"timestamp\": ");
            json.Append(Json(stateEvent.TimestampUtc.ToString("o", CultureInfo.InvariantCulture)));
            json.Append(", \"entityId\": ");
            json.Append(Json(stateEvent.EntityId));
            json.Append(", \"level\": ");
            AppendJsonNullableInt(json, stateEvent.Level);
            json.Append(", \"currentHealth\": ");
            AppendJsonNullableInt(json, stateEvent.CurrentHealth);
            json.Append(", \"maxHealth\": ");
            AppendJsonNullableInt(json, stateEvent.MaxHealth);
            json.Append(", \"x\": ");
            AppendJsonNullableFloat(json, stateEvent.X);
            json.Append(", \"y\": ");
            AppendJsonNullableFloat(json, stateEvent.Y);
            json.Append(", \"z\": ");
            AppendJsonNullableFloat(json, stateEvent.Z);
            json.Append(", \"eventType\": ");
            json.Append(Json(stateEvent.EventType));
            json.Append(" }");
        }

        private void WriteCaptureHealth(CaptureValidation validation)
        {
            try
            {
                string path = Path.Combine(this.sessionDirectory, "capture-health.json");
                StringBuilder json = new StringBuilder();
                json.AppendLine("{");
                json.Append("  \"timestampUtc\": ");
                json.Append(Json(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
                json.AppendLine(",");
                this.AppendValidationJson(json, validation, "  ");
                json.AppendLine();
                json.AppendLine("}");
                File.WriteAllText(path, json.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                this.LogEvent("CAPTURE-HEALTH-ERROR", ex.ToString());
            }
        }

        private void WriteCaptureInfo(DateTime? captureEndUtc, CaptureValidation validation)
        {
            try
            {
                DateTime timestampUtc = DateTime.UtcNow;
                DateTime durationEndUtc = captureEndUtc ?? timestampUtc;
                string path = Path.Combine(this.sessionDirectory, "capture_info.json");
                StringBuilder json = new StringBuilder();
                json.AppendLine("{");
                json.Append("  \"timestampUtc\": ");
                json.Append(Json(timestampUtc.ToString("o", CultureInfo.InvariantCulture)));
                json.AppendLine(",");
                json.Append("  \"captureStartUtc\": ");
                json.Append(Json(this.captureStartUtc.ToString("o", CultureInfo.InvariantCulture)));
                json.AppendLine(",");
                json.Append("  \"captureEndUtc\": ");
                json.Append(captureEndUtc.HasValue ? Json(captureEndUtc.Value.ToString("o", CultureInfo.InvariantCulture)) : "null");
                json.AppendLine(",");
                json.Append("  \"sessionDurationSeconds\": ");
                json.Append(Math.Max(0, (durationEndUtc - this.captureStartUtc).TotalSeconds).ToString("0.###", CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("  \"captureFolderPath\": ");
                json.Append(Json(this.sessionDirectory));
                json.AppendLine(",");
                json.Append("  \"characterName\": ");
                json.Append(Json(this.GetLocalCharacterName()));
                json.AppendLine(",");
                json.Append("  \"playfieldId\": ");
                json.Append(Json(this.GetDetectedPlayfieldId()));
                json.AppendLine(",");
                json.AppendLine("  \"packetCounts\": {");
                json.Append("    \"inboundRaw\": ");
                json.Append(this.inboundPacketCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"outboundRaw\": ");
                json.Append(this.outboundPacketCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"decodedInboundN3\": ");
                json.Append(this.decodedInboundCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"decodedOutboundN3\": ");
                json.Append(this.decodedOutboundCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine();
                json.AppendLine("  },");
                json.AppendLine("  \"captureCounts\": {");
                json.Append("    \"vendorInteractionAttempts\": ");
                json.Append(this.vendorInteractionAttemptCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"vendorFullUpdateMessages\": ");
                json.Append(this.vendorFullUpdateMessageCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"shopUpdateMessages\": ");
                json.Append(this.shopUpdateMessageCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"shopUpdateRows\": ");
                json.Append(this.shopUpdateRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"systemMessages\": ");
                json.Append(this.systemMessageCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"chatDialogueMessages\": ");
                json.Append(this.chatDialogueMessageCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"npcInteractions\": ");
                json.Append(this.npcInteractionCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"inventoryUpdateMessages\": ");
                json.Append(this.inventoryUpdateMessageCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"inventoryUpdateRows\": ");
                json.Append(this.inventoryUpdateRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyFightCaptureStarted\": ");
                json.Append(this.enemyFightCaptureStarted ? "true" : "false");
                json.AppendLine(",");
                json.Append("    \"enemyFightCaptureEnabled\": ");
                json.Append(this.enemyFightCaptureEnabled ? "true" : "false");
                json.AppendLine(",");
                json.Append("    \"enemyTrackedEntities\": ");
                json.Append(this.enemyStates.Count.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyStateRows\": ");
                json.Append(this.enemyStateRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyFullUpdateRows\": ");
                json.Append(this.enemyFullUpdateRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyCombatRows\": ");
                json.Append(this.enemyCombatRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyMovementRows\": ");
                json.Append(this.enemyMovementRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyStatUpdateRows\": ");
                json.Append(this.enemyStatUpdateRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyCombatEvents\": ");
                json.Append(this.enemyCombatEventCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyDamageEvents\": ");
                json.Append(this.enemyDamageEventCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyDeathEvents\": ");
                json.Append(this.enemyDeathEventCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemySpawnEvents\": ");
                json.Append(this.enemySpawnEventCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyDespawnEvents\": ");
                json.Append(this.enemyDespawnEventCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyHealthUpdates\": ");
                json.Append(this.enemyHealthUpdateCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyPositionUpdates\": ");
                json.Append(this.enemyPositionUpdateCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine();
                json.AppendLine("  },");
                json.Append("  \"lastPacketUtc\": ");
                json.Append(this.lastPacketUtc == default(DateTime) ? "null" : Json(this.lastPacketUtc.ToString("o", CultureInfo.InvariantCulture)));
                json.AppendLine(",");
                json.Append("  \"vendorInteractionIdentities\": ");
                AppendJsonStringArray(json, this.vendorInteractionIdentities.OrderBy(value => value).ToArray());
                json.AppendLine(",");
                json.Append("  \"shopUpdateIdentities\": ");
                AppendJsonStringArray(json, this.shopUpdateIdentities.OrderBy(value => value).ToArray());
                json.AppendLine(",");
                json.Append("  \"vendorFullUpdateIdentities\": ");
                AppendJsonStringArray(json, this.vendorFullUpdateIdentities.OrderBy(value => value).ToArray());
                json.AppendLine(",");
                this.AppendValidationJson(json, validation, "  ");
                json.AppendLine();
                json.AppendLine("}");

                File.WriteAllText(path, json.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                this.LogEvent("CAPTURE-INFO-ERROR", ex.ToString());
            }
        }

        private void AppendValidationJson(StringBuilder json, CaptureValidation validation, string indent)
        {
            json.Append(indent);
            json.AppendLine("\"validation\": {");
            json.Append(indent);
            json.Append("  \"status\": ");
            json.Append(Json(validation.Status));
            json.AppendLine(",");
            json.Append(indent);
            json.Append("  \"processingAllowed\": ");
            json.Append(validation.ProcessingAllowed ? "true" : "false");
            json.AppendLine(",");
            json.Append(indent);
            json.Append("  \"issues\": ");
            AppendJsonStringArray(json, validation.Issues);
            json.AppendLine(",");
            json.Append(indent);
            json.Append("  \"notes\": ");
            AppendJsonStringArray(json, validation.Notes);
            json.AppendLine();
            json.Append(indent);
            json.Append("}");
        }

        private long GetSessionFileLength(string fileName)
        {
            string path = Path.Combine(this.sessionDirectory, fileName);
            if (!File.Exists(path))
            {
                return 0;
            }

            return new FileInfo(path).Length;
        }

        private string GetLocalCharacterName()
        {
            return Safe(() => DynelManager.LocalPlayer == null ? string.Empty : DynelManager.LocalPlayer.Name);
        }

        private string GetDetectedPlayfieldId()
        {
            if (!string.IsNullOrWhiteSpace(this.lastPlayfieldId))
            {
                return this.lastPlayfieldId;
            }

            return Safe(() => Playfield.Identity.ToString());
        }

        private sealed class CaptureValidation
        {
            public CaptureValidation(string status, bool processingAllowed, List<string> issues, List<string> notes)
            {
                this.Status = status;
                this.ProcessingAllowed = processingAllowed;
                this.Issues = issues;
                this.Notes = notes;
            }

            public string Status { get; private set; }

            public bool ProcessingAllowed { get; private set; }

            public List<string> Issues { get; private set; }

            public List<string> Notes { get; private set; }

            public static CaptureValidation Running()
            {
                return new CaptureValidation(
                    "running",
                    false,
                    new List<string>(),
                    new List<string> { "Capture is active; final validation runs during plugin teardown." });
            }
        }

        private sealed class DynelDumpResult
        {
            public DynelDumpResult(int count, string csvPath, string jsonPath, string summaryPath)
            {
                this.Count = count;
                this.CsvPath = csvPath;
                this.JsonPath = jsonPath;
                this.SummaryPath = summaryPath;
                this.Success = true;
            }

            public int Count { get; private set; }

            public string CsvPath { get; private set; }

            public string JsonPath { get; private set; }

            public string SummaryPath { get; private set; }

            public bool Success { get; private set; }

            public string Error { get; private set; }

            public bool AlreadyWritten { get; private set; }

            public static DynelDumpResult Failed(string error)
            {
                return new DynelDumpResult(0, string.Empty, string.Empty, string.Empty)
                {
                    Success = false,
                    Error = error ?? string.Empty
                };
            }

            public static DynelDumpResult AlreadyExists(string csvPath, string jsonPath, string summaryPath)
            {
                return new DynelDumpResult(0, csvPath, jsonPath, summaryPath)
                {
                    AlreadyWritten = true
                };
            }
        }

        private sealed class DynelDumpRow
        {
            public int SortType { get; set; }

            public int SortInstance { get; set; }

            public string CapturedUtc { get; set; }

            public string LocalCharacterName { get; set; }

            public string LocalCharacterIdentity { get; set; }

            public string PlayfieldIdentity { get; set; }

            public string Index { get; set; }

            public string DynelCategory { get; set; }

            public string CharacterKind { get; set; }

            public string Identity { get; set; }

            public string IdentityType { get; set; }

            public string IdentityTypeValue { get; set; }

            public string Instance { get; set; }

            public string InstanceHex { get; set; }

            public string ClassName { get; set; }

            public string Name { get; set; }

            public string Position { get; set; }

            public string IsNpc { get; set; }

            public string IsPet { get; set; }

            public string IsInPlay { get; set; }

            public string IsAlive { get; set; }

            public string IsAttacking { get; set; }

            public string FightingTarget { get; set; }

            public string Health { get; set; }

            public string MaxHealth { get; set; }

            public string HealthPercent { get; set; }

            public string NpcLevel { get; set; }

            public string MonsterData { get; set; }

            public string CatMesh { get; set; }

            public string DisplayCatMesh { get; set; }

            public string VisualFlags { get; set; }

            public string State { get; set; }

            public string CurrentState { get; set; }

            public string ActionCategory { get; set; }

            public string Scale { get; set; }

            public string CharRadius { get; set; }

            public string NpcBrainState { get; set; }

            public string PetState { get; set; }

            public string PetOwnerId { get; set; }

            public string NpcFamily { get; set; }

            public string NpcVicinityFamily { get; set; }

            public string RunSpeed { get; set; }

            public string MinDamage { get; set; }

            public string MaxDamage { get; set; }

            public string DefaultAttackType { get; set; }

            public string DamageType1 { get; set; }

            public string DamageType2 { get; set; }

            public string AttackDelay { get; set; }

            public string RechargeDelay { get; set; }

            public string AttackDelayCap { get; set; }

            public string RechargeDelayCap { get; set; }

            public string EquippedWeapons { get; set; }

            public string HealDelta { get; set; }

            public string DeadTimer { get; set; }

            public string CorpseType { get; set; }

            public string CorpseInstance { get; set; }

            public string CorpseAnimKey { get; set; }

            public string DieAnim { get; set; }

            public string Pointer { get; set; }

            public string Error { get; set; }
        }

        private sealed class EnemyEntityState
        {
            public string EntityId { get; set; }

            public int? Level { get; set; }

            public int? CurrentHealth { get; set; }

            public int? MaxHealth { get; set; }

            public float? X { get; set; }

            public float? Y { get; set; }

            public float? Z { get; set; }

            public DateTime FirstSeenUtc { get; set; }

            public DateTime LastUpdateUtc { get; set; }

            public bool DeathLogged { get; set; }
        }

        private sealed class EnemyStateEvent
        {
            public DateTime TimestampUtc { get; set; }

            public string EntityId { get; set; }

            public int? Level { get; set; }

            public int? CurrentHealth { get; set; }

            public int? MaxHealth { get; set; }

            public float? X { get; set; }

            public float? Y { get; set; }

            public float? Z { get; set; }

            public string EventType { get; set; }
        }

        private static int GetStatValue(GameTuple<Stat, int>[] stats, Stat stat)
        {
            foreach (GameTuple<Stat, int> entry in stats)
            {
                if (entry.Value1 == stat)
                {
                    return entry.Value2;
                }
            }

            return 0;
        }

        private void LogEvent(string category, string message)
        {
            lock (this.syncRoot)
            {
                this.eventsLog.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:o} [{1}] {2}",
                        DateTime.UtcNow,
                        category,
                        OneLine(message)));
            }
        }

        private void LogSmokeEvent(string message)
        {
            this.LogEvent("SMOKE", message);
        }

        private string DescribeRawPacket(byte[] packet)
        {
            if (packet == null || packet.Length < 20)
            {
                return "type=unknown";
            }

            int typeValue = ReadInt32BigEndian(packet, 16);
            string typeName = Enum.IsDefined(typeof(N3MessageType), typeValue)
                ? ((N3MessageType)typeValue).ToString()
                : "0x" + typeValue.ToString("X8", CultureInfo.InvariantCulture);

            return "n3=" + typeName;
        }

        private string DescribeDynel(Dynel dynel)
        {
            if (dynel == null)
            {
                return "null";
            }

            try
            {
                if (dynel.Identity.Type == IdentityType.SimpleChar)
                {
                    return this.DescribeCharacter(dynel.Cast<SimpleChar>());
                }

                if (dynel.Identity.Type == IdentityType.Corpse)
                {
                    return this.DescribeCorpse(dynel.Cast<Corpse>());
                }

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "identity={0} name={1} pos={2}",
                    dynel.Identity,
                    Safe(() => dynel.Name),
                    Safe(() => dynel.Position.ToString()));
            }
            catch (Exception ex)
            {
                return "identity=" + Safe(() => dynel.Identity.ToString()) + " error=" + ex.Message;
            }
        }

        private string DescribeCharacter(SimpleChar character)
        {
            if (character == null)
            {
                return "null";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "identity={0} name={1} player={2} npc={3} pet={4} inPlay={5} alive={6} hp={7}/{8} pct={9:0.0} level={10} computerLiteracy={11} pos={12} attacking={13} fightingTarget={14} monsterData={15} catMesh={16} visualFlags={17} state={18} currentState={19} actionCategory={20} deadTimer={21} corpseType={22} corpseInstance={23} corpseAnimKey={24} dieAnim={25}",
                Safe(() => character.Identity.ToString()),
                Safe(() => character.Name),
                Safe(() => character.IsPlayer.ToString()),
                Safe(() => character.IsNpc.ToString()),
                Safe(() => character.IsPet.ToString()),
                Safe(() => character.IsInPlay.ToString()),
                Safe(() => character.IsAlive.ToString()),
                SafeStat(character, Stat.Health),
                SafeStat(character, Stat.MaxHealth),
                SafeFloat(() => character.HealthPercent),
                SafeStat(character, Stat.Level),
                SafeStat(character, Stat.ComputerLiteracy),
                Safe(() => character.Position.ToString()),
                Safe(() => character.IsAttacking.ToString()),
                Safe(() => character.FightingTarget == null ? "null" : character.FightingTarget.Identity.ToString()),
                SafeStat(character, Stat.MonsterData),
                SafeStat(character, Stat.CATMesh),
                SafeStat(character, Stat.VisualFlags),
                SafeStat(character, Stat.State),
                SafeStat(character, Stat.CurrentState),
                SafeStat(character, Stat.ActionCategory),
                SafeStat(character, Stat.DeadTimer),
                SafeStat(character, Stat.CorpseType),
                SafeStat(character, Stat.CorpseInstance),
                SafeStat(character, Stat.CorpseAnimKey),
                SafeStat(character, Stat.DieAnim));
        }

        private string DescribeCorpse(Corpse corpse)
        {
            if (corpse == null)
            {
                return "null";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "identity={0} name={1} pos={2} open={3}",
                Safe(() => corpse.Identity.ToString()),
                Safe(() => corpse.Name),
                Safe(() => corpse.Position.ToString()),
                Safe(() => corpse.IsOpen.ToString()));
        }

        private string DescribeObject(object value)
        {
            if (value == null)
            {
                return "null";
            }

            Type type = value.GetType();
            StringBuilder result = new StringBuilder();
            result.Append(type.Name);
            result.Append(" { ");

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                result.Append(property.Name);
                result.Append('=');
                result.Append(Safe(() => FormatValue(property.GetValue(value, null))));
                result.Append(' ');
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                result.Append(field.Name);
                result.Append('=');
                result.Append(Safe(() => FormatValue(field.GetValue(value))));
                result.Append(' ');
            }

            result.Append('}');
            return result.Length > 6000 ? result.ToString(0, 6000) + "..." : result.ToString();
        }

        private static object GetMemberValue(object value, string name)
        {
            if (value == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                Type type = value.GetType();
                PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    return property.GetValue(value, null);
                }

                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
                if (field != null)
                {
                    return field.GetValue(value);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static string GetMemberString(object value, string name)
        {
            return FormatObjectForCsv(GetMemberValue(value, name));
        }

        private static string MemberComponent(object value, string name)
        {
            return FormatObjectForCsv(GetMemberValue(value, name));
        }

        private static string GetIdentityTypeName(object identityValue)
        {
            object type = GetMemberValue(identityValue, "Type");
            return FormatObjectForCsv(type);
        }

        private static string FormatIdentityValue(object identityValue)
        {
            if (identityValue == null)
            {
                return string.Empty;
            }

            string text = FormatObjectForCsv(identityValue);
            return string.Equals(text, "None:0", StringComparison.OrdinalIgnoreCase) ? string.Empty : text;
        }

        private static string FormatObjectForCsv(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            IFormattable formattable = value as IFormattable;
            if (formattable != null)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        private static string GetCountString(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            ICollection collection = value as ICollection;
            if (collection != null)
            {
                return collection.Count.ToString(CultureInfo.InvariantCulture);
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null || value is string)
            {
                return string.Empty;
            }

            int count = 0;
            foreach (object ignored in enumerable)
            {
                count++;
            }

            return count.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetByteArrayLengthString(object value)
        {
            byte[] bytes = value as byte[];
            return bytes == null ? string.Empty : bytes.Length.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is string)
            {
                return "\"" + value + "\"";
            }

            if (value is byte[] bytes)
            {
                return "byte[" + bytes.Length.ToString(CultureInfo.InvariantCulture) + "]:" + ToHex(bytes.Take(32).ToArray());
            }

            string tupleValue = TryFormatGameTuple(value);
            if (tupleValue != null)
            {
                return tupleValue;
            }

            if (value is IEnumerable enumerable && !(value is string))
            {
                List<string> items = new List<string>();
                int count = 0;
                foreach (object item in enumerable)
                {
                    count++;
                    if (items.Count < 6)
                    {
                        items.Add(FormatValue(item));
                    }
                }

                return "count=" + count.ToString(CultureInfo.InvariantCulture) + "[" + string.Join(",", items.ToArray()) + "]";
            }

            return value.ToString();
        }

        private static string TryFormatGameTuple(object value)
        {
            Type type = value.GetType();
            if (!type.IsGenericType || type.GetGenericTypeDefinition().FullName != "SmokeLounge.AOtomation.Messaging.GameData.GameTuple`2")
            {
                return null;
            }

            PropertyInfo value1 = type.GetProperty("Value1", BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo value2 = type.GetProperty("Value2", BindingFlags.Instance | BindingFlags.Public);
            if (value1 == null || value2 == null)
            {
                return null;
            }

            object left = value1.GetValue(value, null);
            object right = value2.GetValue(value, null);
            return FormatValue(left) + "=" + FormatValue(right);
        }

        private static bool TryGetGameTupleValues(object value, out object left, out object right)
        {
            left = null;
            right = null;

            if (value == null)
            {
                return false;
            }

            Type type = value.GetType();
            if (!type.IsGenericType || type.GetGenericTypeDefinition().FullName != "SmokeLounge.AOtomation.Messaging.GameData.GameTuple`2")
            {
                return false;
            }

            PropertyInfo value1 = type.GetProperty("Value1", BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo value2 = type.GetProperty("Value2", BindingFlags.Instance | BindingFlags.Public);
            if (value1 == null || value2 == null)
            {
                return false;
            }

            left = value1.GetValue(value, null);
            right = value2.GetValue(value, null);
            return true;
        }

        private static string GetStatNumericValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            try
            {
                if (value is Enum)
                {
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                }

                IConvertible convertible = value as IConvertible;
                if (convertible != null)
                {
                    return convertible.ToInt32(CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private void Flush()
        {
            lock (this.syncRoot)
            {
                this.eventsLog.Flush();
                this.packetsLog.Flush();
                this.shopUpdatesLog.Flush();
                this.vendorFullUpdatesLog.Flush();
                this.systemMessagesLog.Flush();
                this.chatDialogueLog.Flush();
                this.npcInteractionsLog.Flush();
                this.inventoryUpdatesLog.Flush();
                this.enemyStateLog.Flush();
                this.enemyFullUpdatesLog.Flush();
                this.enemyCombatLog.Flush();
                this.enemyMovementLog.Flush();
                this.enemyStatUpdatesLog.Flush();
            }
        }

        private void FlushAndClose()
        {
            lock (this.syncRoot)
            {
                this.eventsLog?.Flush();
                this.packetsLog?.Flush();
                this.shopUpdatesLog?.Flush();
                this.vendorFullUpdatesLog?.Flush();
                this.systemMessagesLog?.Flush();
                this.chatDialogueLog?.Flush();
                this.npcInteractionsLog?.Flush();
                this.inventoryUpdatesLog?.Flush();
                this.enemyStateLog?.Flush();
                this.enemyFullUpdatesLog?.Flush();
                this.enemyCombatLog?.Flush();
                this.enemyMovementLog?.Flush();
                this.enemyStatUpdatesLog?.Flush();
                this.eventsLog?.Dispose();
                this.packetsLog?.Dispose();
                this.shopUpdatesLog?.Dispose();
                this.vendorFullUpdatesLog?.Dispose();
                this.systemMessagesLog?.Dispose();
                this.chatDialogueLog?.Dispose();
                this.npcInteractionsLog?.Dispose();
                this.inventoryUpdatesLog?.Dispose();
                this.enemyStateLog?.Dispose();
                this.enemyFullUpdatesLog?.Dispose();
                this.enemyCombatLog?.Dispose();
                this.enemyMovementLog?.Dispose();
                this.enemyStatUpdatesLog?.Dispose();
                this.eventsLog = null;
                this.packetsLog = null;
                this.shopUpdatesLog = null;
                this.vendorFullUpdatesLog = null;
                this.systemMessagesLog = null;
                this.chatDialogueLog = null;
                this.npcInteractionsLog = null;
                this.inventoryUpdatesLog = null;
                this.enemyStateLog = null;
                this.enemyFullUpdatesLog = null;
                this.enemyCombatLog = null;
                this.enemyMovementLog = null;
                this.enemyStatUpdatesLog = null;
            }
        }

        private static StreamWriter CreateWriter(string path)
        {
            return new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
            {
                AutoFlush = false
            };
        }

        private static StreamWriter CreateAppendWriter(string path)
        {
            return new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
            {
                AutoFlush = false
            };
        }

        private static string CreateSessionDirectory(string pluginDir)
        {
            string baseDirectory = string.IsNullOrWhiteSpace(pluginDir) ? Directory.GetCurrentDirectory() : pluginDir;
            string directory = Path.Combine(baseDirectory, "captures", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string Safe(Func<string> func)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                return "<" + ex.GetType().Name + ":" + ex.Message + ">";
            }
        }

        private static string SafeStat(SimpleChar character, Stat stat)
        {
            return Safe(() => character.GetStat(stat).ToString(CultureInfo.InvariantCulture));
        }

        private static bool SafeBool(Func<bool> func)
        {
            try
            {
                return func();
            }
            catch
            {
                return false;
            }
        }

        private static float SafeFloat(Func<float> func)
        {
            try
            {
                return func();
            }
            catch
            {
                return 0;
            }
        }

        private static string OptionalFloat(Func<float> func)
        {
            try
            {
                return func().ToString("R", CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string OneLine(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value.Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string Csv(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string NullableInt(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        private static string NullableFloat(float? value)
        {
            return value.HasValue ? value.Value.ToString("R", CultureInfo.InvariantCulture) : string.Empty;
        }

        private static string Json(string value)
        {
            if (value == null)
            {
                return "null";
            }

            StringBuilder result = new StringBuilder(value.Length + 2);
            result.Append('"');
            foreach (char ch in value)
            {
                switch (ch)
                {
                    case '\\':
                        result.Append("\\\\");
                        break;

                    case '"':
                        result.Append("\\\"");
                        break;

                    case '\r':
                        result.Append("\\r");
                        break;

                    case '\n':
                        result.Append("\\n");
                        break;

                    case '\t':
                        result.Append("\\t");
                        break;

                    default:
                        if (ch < ' ')
                        {
                            result.Append("\\u");
                            result.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            result.Append(ch);
                        }

                        break;
                }
            }

            result.Append('"');
            return result.ToString();
        }

        private static void AppendJsonStringArray(StringBuilder json, IEnumerable<string> values)
        {
            json.Append("[");

            bool first = true;
            foreach (string value in values ?? new string[0])
            {
                if (!first)
                {
                    json.Append(", ");
                }

                json.Append(Json(value));
                first = false;
            }

            json.Append("]");
        }

        private static void AppendJsonNullableInt(StringBuilder json, int? value)
        {
            if (value.HasValue)
            {
                json.Append(value.Value.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                json.Append("null");
            }
        }

        private static void AppendJsonNullableFloat(StringBuilder json, float? value)
        {
            if (value.HasValue)
            {
                json.Append(value.Value.ToString("R", CultureInfo.InvariantCulture));
            }
            else
            {
                json.Append("null");
            }
        }

        private static void AppendJsonField(StringBuilder json, string indent, string name, string value, bool comma)
        {
            json.Append(indent);
            json.Append(Json(name));
            json.Append(": ");
            json.Append(Json(value ?? string.Empty));
            if (comma)
            {
                json.Append(",");
            }

            json.AppendLine();
        }

        private static int ToInt32Clamp(uint value)
        {
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder result = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                result.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }

        private static int ReadInt32BigEndian(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24)
                | (bytes[offset + 1] << 16)
                | (bytes[offset + 2] << 8)
                | bytes[offset + 3];
        }
    }
}
