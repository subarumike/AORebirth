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
        private DateTime nextFlushUtc;
        private DateTime nextSnapshotUtc;
        private DateTime captureStartUtc;
        private DateTime captureStartLocal;
        private DateTime lastPacketUtc;
        private string lastPlayfieldId = string.Empty;
        private CombatLootSmoke combatLootSmoke;

        public override void Run(string pluginDir)
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
            this.LogEvent("PLUGIN", "Commands: /aocap start | stop | mark <text> | status | flush | snapshot");
            this.LogEvent("PLUGIN", "Smoke commands: /aosmoke start [mobAlias] | stop | status | log");
            this.LogEvent("PLUGIN", "ShopUpdate CSV: " + Path.Combine(this.sessionDirectory, "shop-updates.csv"));
            this.LogEvent("PLUGIN", "VendingMachineFullUpdate CSV: " + Path.Combine(this.sessionDirectory, "vendor-full-updates.csv"));
            this.LogEvent("PLUGIN", "System messages log: " + Path.Combine(this.sessionDirectory, "system-messages.log"));
            this.LogEvent("PLUGIN", "Chat/dialogue log: " + Path.Combine(this.sessionDirectory, "chat-dialogue.log"));
            this.LogEvent("PLUGIN", "NPC interactions log: " + Path.Combine(this.sessionDirectory, "npc-interactions.log"));
            this.LogEvent("PLUGIN", "Inventory update CSV: " + Path.Combine(this.sessionDirectory, "inventory-updates.csv"));
            this.LogEvent("PLUGIN", "Enemy state CSV: " + Path.Combine(this.sessionDirectory, "enemy-state.csv"));
            this.LogEvent("PLUGIN", "Enemy state JSON: " + Path.Combine(this.sessionDirectory, "enemy-state.json"));
            this.LogEvent("PLUGIN", "Capture info: " + Path.Combine(this.sessionDirectory, "capture_info.json"));
            this.LogEvent("PLUGIN", "Capture session metadata: " + Path.Combine(this.sessionDirectory, "capture-session.json"));
            this.LogSnapshot("initial");
            Chat.WriteLine("AOSharpLiveCapture logging to " + this.sessionDirectory, ChatColor.Gold);
        }

        public override void Teardown()
        {
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

                default:
                    chatWindow.WriteLine(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "AO capture {0}. inRaw={1} outRaw={2} inN3={3} outN3={4} dir={5}",
                            this.enabled ? "running" : "stopped",
                            this.inboundPacketCount,
                            this.outboundPacketCount,
                            this.decodedInboundCount,
                            this.decodedOutboundCount,
                            this.sessionDirectory),
                        ChatColor.Gold);
                    break;
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
            this.TrackEnemyStateFromMessage(direction, sequence, message);
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

            if (this.enemyCombatEventCount > 0 && this.enemyStateRowCount == 0)
            {
                issues.Add("Combat packets were observed, but enemy-state.csv has no rows.");
            }

            if (this.enemyCombatEventCount == 0)
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
                json.Append("    \"enemyTrackedEntities\": ");
                json.Append(this.enemyStates.Count.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyStateRows\": ");
                json.Append(this.enemyStateRowCount.ToString(CultureInfo.InvariantCulture));
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
                this.eventsLog?.Dispose();
                this.packetsLog?.Dispose();
                this.shopUpdatesLog?.Dispose();
                this.vendorFullUpdatesLog?.Dispose();
                this.systemMessagesLog?.Dispose();
                this.chatDialogueLog?.Dispose();
                this.npcInteractionsLog?.Dispose();
                this.inventoryUpdatesLog?.Dispose();
                this.enemyStateLog?.Dispose();
                this.eventsLog = null;
                this.packetsLog = null;
                this.shopUpdatesLog = null;
                this.vendorFullUpdatesLog = null;
                this.systemMessagesLog = null;
                this.chatDialogueLog = null;
                this.npcInteractionsLog = null;
                this.inventoryUpdatesLog = null;
                this.enemyStateLog = null;
            }
        }

        private static StreamWriter CreateWriter(string path)
        {
            return new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
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
