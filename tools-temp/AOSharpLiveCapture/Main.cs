using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.UI;

using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace AOSharpLiveCapture
{
    public class Main : AOPluginEntry
    {
        private readonly object syncRoot = new object();
        private readonly HashSet<string> knownCharacters = new HashSet<string>();
        private readonly HashSet<string> knownCorpses = new HashSet<string>();
        private readonly HashSet<string> interestingMessageNames = new HashSet<string>
        {
            "SimpleCharFullUpdate",
            "CharInPlay",
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
            "ClientMoveItemToInventory",
            "ContainerAddItem",
            "ClientContainerAddItem",
            "BankCorpse",
            "Feedback",
            "Stat"
        };

        private string sessionDirectory;
        private StreamWriter eventsLog;
        private StreamWriter packetsLog;
        private bool enabled;
        private int inboundPacketCount;
        private int outboundPacketCount;
        private int decodedInboundCount;
        private int decodedOutboundCount;
        private DateTime nextFlushUtc;
        private DateTime nextSnapshotUtc;
        private CombatLootSmoke combatLootSmoke;

        public override void Run(string pluginDir)
        {
            this.sessionDirectory = CreateSessionDirectory(pluginDir);
            this.eventsLog = CreateWriter(Path.Combine(this.sessionDirectory, "events.log"));
            this.packetsLog = CreateWriter(Path.Combine(this.sessionDirectory, "packets.hex.log"));
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
            this.FlushAndClose();
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
            this.LogPacket("IN", this.inboundPacketCount, packet);
        }

        private void OnPacketSent(object sender, byte[] packet)
        {
            if (!this.enabled)
            {
                return;
            }

            this.outboundPacketCount++;
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
            this.LogN3Message("OUT-N3", this.decodedOutboundCount, message);
        }

        private void OnChatMessageReceived(object sender, ChatMessageBody message)
        {
            if (!this.enabled || message == null)
            {
                return;
            }

            this.LogEvent("CHAT", this.DescribeObject(message));
        }

        private void OnDynelSpawned(object sender, Dynel dynel)
        {
            if (!this.enabled || dynel == null)
            {
                return;
            }

            this.LogEvent("DYNEL-SPAWNED", this.DescribeDynel(dynel));
        }

        private void OnCharInPlay(object sender, SimpleChar character)
        {
            if (!this.enabled || character == null)
            {
                return;
            }

            this.LogEvent("CHAR-IN-PLAY", this.DescribeCharacter(character));
        }

        private void OnPlayfieldInit(object sender, uint playfieldId)
        {
            this.LogEvent("PLAYFIELD-INIT", playfieldId.ToString(CultureInfo.InvariantCulture));
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
                    }
                }

                foreach (string removed in this.knownCharacters.Except(currentCharacters).ToArray())
                {
                    this.LogEvent("CHAR-GONE", removed);
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
            string detail = interesting ? this.DescribeObject(message) : string.Empty;

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
                "identity={0} name={1} player={2} npc={3} pet={4} inPlay={5} alive={6} hp={7}/{8} pct={9:0.0} level={10} pos={11} attacking={12} fightingTarget={13} monsterData={14} catMesh={15} visualFlags={16} state={17} currentState={18} actionCategory={19} deadTimer={20} corpseType={21} corpseInstance={22} corpseAnimKey={23} dieAnim={24}",
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
            }
        }

        private void FlushAndClose()
        {
            lock (this.syncRoot)
            {
                this.eventsLog?.Flush();
                this.packetsLog?.Flush();
                this.eventsLog?.Dispose();
                this.packetsLog?.Dispose();
                this.eventsLog = null;
                this.packetsLog = null;
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

        private static string OneLine(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value.Replace("\r", "\\r").Replace("\n", "\\n");
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
