using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.Inventory;
using AOSharp.Core.UI;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace AOSharpLiveCapture
{
    internal sealed class CombatLootSmoke
    {
        private const string DefaultMobAlias = "beachleet";
        private const string DefaultMobName = "Codex Test Beach Leet";
        private const string RequestFileName = "run-combat-loot-smoke.txt";
        private const string ResultFileName = "last-combat-loot-smoke.result";
        private const int MoveToInventoryPlacement = 0x6F;
        private const int UniqueTestLowId = 0x4545F;
        private const int UniqueTestHighId = 0x4545A;
        private const int CleanupBatchSize = 40;

        private readonly string pluginDirectory;
        private readonly string requestFilePath;
        private readonly string resultFilePath;
        private readonly Action<string> captureLog;
        private readonly HashSet<Identity> knownCorpses = new HashSet<Identity>();
        private readonly HashSet<string> attemptedLootSlots = new HashSet<string>();

        private StreamWriter log;
        private string logPath;
        private SmokeState state = SmokeState.Idle;
        private DateTime stateStartedUtc;
        private DateTime runStartedUtc;
        private DateTime nextActionUtc;
        private DateTime reopenSentUtc;
        private DateTime lastCorpseInventorySignalUtc;
        private DateTime lastCorpseAccessSignalUtc;
        private DateTime lastLootMoveSignalUtc;
        private int cleanupAttemptCount;
        private Identity targetIdentity = Identity.None;
        private Identity corpseIdentity = Identity.None;
        private string expectedCharacterName = string.Empty;
        private string mobAlias = DefaultMobAlias;
        private string mobName = DefaultMobName;
        private int itemCountBeforeMove;
        private int reopenUseCount;
        private int lootAttemptCount;
        private bool closeIssued;
        private bool closeConfirmed;
        private bool closeHookCalled;
        private bool requireReopen = true;
        private string lastCloseWindowName = string.Empty;

        private static readonly Dictionary<string, string> KnownMobNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "beachleet", "Codex Test Beach Leet" },
                { "leet", "Codex Test Beach Leet" },
                { "codexleet", "Codex Test Beach Leet" },
                { "islandreet", "Codex Test Island Reet" },
                { "reet", "Codex Test Island Reet" },
                { "shoresnake", "Codex Test Shore Snake" },
                { "snake", "Codex Test Shore Snake" },
                { "rollerrat", "Codex Test Stowaway Rollerrat" },
                { "stowawayrollerrat", "Codex Test Stowaway Rollerrat" },
                { "rat", "Codex Test Stowaway Rollerrat" },
                { "duneflea", "Codex Test Dune Flea" },
                { "flea", "Codex Test Dune Flea" },
                { "surflizard", "Codex Test Surf Lizard" },
                { "lizard", "Codex Test Surf Lizard" },
                { "cliffmalle", "Codex Test Cliff Malle" },
                { "malle", "Codex Test Cliff Malle" },
                { "reefsalamander", "Codex Test Reef Salamander" },
                { "salamander", "Codex Test Reef Salamander" },
                { "alienspider", "Codex Test Alien Spider - Zix" },
                { "spider", "Codex Test Alien Spider - Zix" },
                { "zix", "Codex Test Alien Spider - Zix" },
                { "a004", "Beach Leet" }
            };

        private static readonly HashSet<int> TestLootCleanupItemIds = new HashSet<int>
        {
            27350,
            27351,
            27352
        };

        public CombatLootSmoke(string pluginDirectory, Action<string> captureLog)
        {
            this.pluginDirectory = string.IsNullOrWhiteSpace(pluginDirectory)
                ? Directory.GetCurrentDirectory()
                : pluginDirectory;
            this.requestFilePath = Path.Combine(this.pluginDirectory, RequestFileName);
            this.resultFilePath = Path.Combine(this.pluginDirectory, ResultFileName);
            this.captureLog = captureLog ?? (_ => { });
        }

        private enum SmokeState
        {
            Idle,
            WaitingForPlayer,
            SpawnMob,
            CleanupInventory,
            WaitMob,
            StartAttack,
            WaitCorpse,
            OpenFirst,
            WaitFirstItems,
            LootFirst,
            WaitFirstMoved,
            CloseLootWindow,
            ReopenCorpse,
            WaitReopened,
            LootRemaining,
            WaitRemainingMoved,
            WaitCorpseGone,
            Passed,
            Failed
        }

        public void OnCommand(string command, string[] args, ChatWindow chatWindow)
        {
            string subCommand = args.Length == 0 ? "status" : args[0].ToLowerInvariant();

            switch (subCommand)
            {
                case "start":
                    string requestedAlias = args.Length > 1 ? args[1] : string.Empty;
                    bool requestedRequireReopen = args.Length <= 2
                        || !string.Equals(args[2], "basic", StringComparison.OrdinalIgnoreCase);
                    this.Start("chat command", string.Empty, requestedAlias, string.Empty, requestedRequireReopen);
                    chatWindow.WriteLine(
                        "Combat loot smoke started for " + this.mobName + " (" + this.mobAlias + ").",
                        ChatColor.Gold);
                    break;

                case "stop":
                    this.Stop("chat command");
                    chatWindow.WriteLine("Combat loot smoke stopped.", ChatColor.Gold);
                    break;

                case "log":
                    chatWindow.WriteLine(string.IsNullOrEmpty(this.logPath) ? "No combat loot smoke log yet." : this.logPath, ChatColor.Gold);
                    break;

                default:
                    chatWindow.WriteLine(this.StatusText(), ChatColor.Gold);
                    break;
            }
        }

        public void OnN3MessageReceived(N3Message message)
        {
            if (message == null)
            {
                return;
            }

            try
            {
                if (message.N3MessageType == N3MessageType.InventoryUpdate)
                {
                    this.lastCorpseInventorySignalUtc = DateTime.UtcNow;
                    this.Write("IN InventoryUpdate " + this.DescribeMessage(message));
                }
                else if (message.N3MessageType == N3MessageType.ContainerAddItem)
                {
                    this.lastLootMoveSignalUtc = DateTime.UtcNow;
                    this.Write("IN ContainerAddItem " + this.DescribeMessage(message));
                }
                else if (message.N3MessageType == N3MessageType.CharacterAction)
                {
                    this.lastCorpseAccessSignalUtc = DateTime.UtcNow;
                    this.Write("IN CharacterAction " + this.DescribeMessage(message));
                }
                else if (message.N3MessageType.ToString() == "Action")
                {
                    this.lastCorpseAccessSignalUtc = DateTime.UtcNow;
                    this.Write("IN Action " + this.DescribeMessage(message));
                }
                else if (message.N3MessageType == N3MessageType.Despawn)
                {
                    this.Write("IN Despawn " + this.DescribeMessage(message));
                }
            }
            catch (Exception ex)
            {
                this.Write("N3 receive log error: " + ex.Message);
            }
        }

        public void OnN3MessageSent(N3Message message)
        {
            if (message == null || !this.IsRunning)
            {
                return;
            }

            try
            {
                if (message.N3MessageType == N3MessageType.Attack
                    || message.N3MessageType == N3MessageType.GenericCmd
                    || message.N3MessageType == N3MessageType.ChatCmd
                    || message.N3MessageType == N3MessageType.ClientMoveItemToInventory)
                {
                    this.Write("OUT " + message.N3MessageType + " " + this.DescribeMessage(message));
                }
            }
            catch (Exception ex)
            {
                this.Write("N3 sent log error: " + ex.Message);
            }
        }

        public void Update(float deltaTime)
        {
            try
            {
                this.CheckRequestFile();

                if (!this.IsRunning)
                {
                    return;
                }

                this.UpdateState();
            }
            catch (Exception ex)
            {
                this.Fail("Unhandled smoke exception: " + ex);
            }
        }

        public void Teardown()
        {
            this.Write("teardown");
            this.CloseLog();
        }

        private bool IsRunning
        {
            get
            {
                return this.state != SmokeState.Idle
                    && this.state != SmokeState.Passed
                    && this.state != SmokeState.Failed;
            }
        }

        private void CheckRequestFile()
        {
            if (this.IsRunning || !File.Exists(this.requestFilePath))
            {
                return;
            }

            string requested;
            try
            {
                using (var stream = new FileStream(this.requestFilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    requested = reader.ReadToEnd();
                }
            }
            catch (IOException)
            {
                return;
            }
            catch (Exception ex)
            {
                this.Write("Request file read failed: " + ex.Message);
                return;
            }

            try
            {
                File.Delete(this.requestFilePath);
            }
            catch (Exception ex)
            {
                this.Write("Request file delete failed; delaying smoke start: " + ex.Message);
                return;
            }

            string reason = "request file";
            if (!string.IsNullOrWhiteSpace(requested))
            {
                reason += ": " + OneLine(requested.Trim());
            }

            this.Start(
                reason,
                this.ParseRequestValue(requested, "character"),
                this.ParseRequestValue(requested, "mobAlias"),
                this.ParseRequestValue(requested, "mobName"),
                this.ParseRequestBool(requested, "requireReopen", true));
        }

        private void Start(
            string reason,
            string expectedCharacter,
            string requestedMobAlias,
            string requestedMobName,
            bool requestedRequireReopen)
        {
            this.CloseLog();

            Directory.CreateDirectory(Path.Combine(this.pluginDirectory, "smoke-runs"));
            this.logPath = Path.Combine(
                this.pluginDirectory,
                "smoke-runs",
                DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-combat-loot.log");
            this.log = new StreamWriter(new FileStream(this.logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
            {
                AutoFlush = true
            };

            this.knownCorpses.Clear();
            foreach (Corpse corpse in SafeArray(() => DynelManager.Corpses.ToArray()))
            {
                this.knownCorpses.Add(corpse.Identity);
            }

            this.attemptedLootSlots.Clear();
            this.targetIdentity = Identity.None;
            this.corpseIdentity = Identity.None;
            this.itemCountBeforeMove = 0;
            this.reopenUseCount = 0;
            this.lootAttemptCount = 0;
            this.cleanupAttemptCount = 0;
            this.closeIssued = false;
            this.closeConfirmed = false;
            this.closeHookCalled = false;
            this.requireReopen = requestedRequireReopen;
            this.lastCloseWindowName = string.Empty;
            this.expectedCharacterName = expectedCharacter ?? string.Empty;
            this.mobAlias = NormalizeMobAlias(requestedMobAlias);
            this.mobName = ResolveMobName(this.mobAlias, requestedMobName);
            this.lastCorpseInventorySignalUtc = DateTime.MinValue;
            this.lastCorpseAccessSignalUtc = DateTime.MinValue;
            this.lastLootMoveSignalUtc = DateTime.MinValue;
            this.runStartedUtc = DateTime.UtcNow;
            this.nextActionUtc = DateTime.MinValue;

            this.Write("START reason=" + reason);
            if (!string.IsNullOrWhiteSpace(this.expectedCharacterName))
            {
                this.Write("Expected character: " + this.expectedCharacterName);
            }

            this.Write("Mob under test: alias=" + this.mobAlias + " name=" + this.mobName);
            this.Write("Require reopen: " + this.requireReopen);
            this.Write("Known corpses before run: " + this.knownCorpses.Count.ToString(CultureInfo.InvariantCulture));
            Chat.WriteLine("Combat loot smoke started for " + this.mobName + ". Log: " + this.logPath, ChatColor.Gold);
            this.Transition(SmokeState.WaitingForPlayer, "start");
        }

        private void Stop(string reason)
        {
            if (!this.IsRunning)
            {
                this.Write("STOP ignored; not running. reason=" + reason);
                return;
            }

            this.WriteResult("STOP", reason);
            this.Transition(SmokeState.Idle, "stopped");
            this.CloseLog();
        }

        private void UpdateState()
        {
            switch (this.state)
            {
                case SmokeState.WaitingForPlayer:
                    this.WaitingForPlayer();
                    break;

                case SmokeState.SpawnMob:
                    this.SpawnMob();
                    break;

                case SmokeState.CleanupInventory:
                    this.CleanupInventory();
                    break;

                case SmokeState.WaitMob:
                    this.WaitMob();
                    break;

                case SmokeState.StartAttack:
                    this.StartAttack();
                    break;

                case SmokeState.WaitCorpse:
                    this.WaitCorpse();
                    break;

                case SmokeState.OpenFirst:
                    this.OpenFirst();
                    break;

                case SmokeState.WaitFirstItems:
                    this.WaitFirstItems();
                    break;

                case SmokeState.LootFirst:
                    this.LootFirst();
                    break;

                case SmokeState.WaitFirstMoved:
                    this.WaitFirstMoved();
                    break;

                case SmokeState.CloseLootWindow:
                    this.CloseLootWindow();
                    break;

                case SmokeState.ReopenCorpse:
                    this.ReopenCorpse();
                    break;

                case SmokeState.WaitReopened:
                    this.WaitReopened();
                    break;

                case SmokeState.LootRemaining:
                    this.LootRemaining();
                    break;

                case SmokeState.WaitRemainingMoved:
                    this.WaitRemainingMoved();
                    break;

                case SmokeState.WaitCorpseGone:
                    this.WaitCorpseGone();
                    break;
            }
        }

        private void WaitingForPlayer()
        {
            if (DynelManager.LocalPlayer != null && !Game.IsZoning)
            {
                string playerName = Safe(() => DynelManager.LocalPlayer.Name);
                if (!string.IsNullOrWhiteSpace(this.expectedCharacterName)
                    && !string.Equals(playerName, this.expectedCharacterName, StringComparison.OrdinalIgnoreCase))
                {
                    this.Fail("Refusing to run on character '" + playerName + "'; request expected '" + this.expectedCharacterName + "'.");
                    return;
                }

                this.Write("Local player: " + this.DescribeCharacter(DynelManager.LocalPlayer));
                this.Transition(SmokeState.CleanupInventory, "player ready");
                return;
            }

            if (this.TimedOut(30))
            {
                this.Fail("Timed out waiting for local player.");
            }
        }

        private void CleanupInventory()
        {
            List<Item> testItems = this.GetInventoryCleanupItems();
            if (testItems.Count == 0)
            {
                this.Transition(SmokeState.SpawnMob, "test loot inventory clean");
                return;
            }

            if (DateTime.UtcNow >= this.nextActionUtc)
            {
                this.cleanupAttemptCount++;
                this.DeleteInventoryTestLoot(testItems, "pre-run cleanup");
                this.nextActionUtc = DateTime.UtcNow.AddSeconds(1);
            }

            if (this.TimedOut(12))
            {
                this.Fail(
                    "Timed out cleaning test loot before run. Remaining="
                    + this.DescribeItems(this.GetInventoryCleanupItems()));
            }
        }

        private void SpawnMob()
        {
            SimpleChar existing = this.FindAliveTestMob();
            if (existing != null)
            {
                this.targetIdentity = existing.Identity;
                this.Write("Using existing test mob: " + this.DescribeCharacter(existing));
                this.Transition(SmokeState.StartAttack, "existing mob");
                return;
            }

            LocalPlayer player = DynelManager.LocalPlayer;
            if (player == null)
            {
                this.Transition(SmokeState.WaitingForPlayer, "player disappeared");
                return;
            }

            Network.Send(new ChatCmdMessage
            {
                Identity = player.Identity,
                Target = player.Identity,
                Command = "spawn " + this.mobAlias
            });

            this.Write("Sent ChatCmd spawn " + this.mobAlias + ".");
            this.Transition(SmokeState.WaitMob, "spawn sent");
        }

        private void WaitMob()
        {
            SimpleChar mob = this.FindAliveTestMob();
            if (mob != null)
            {
                this.targetIdentity = mob.Identity;
                this.Write("Spawned/found target: " + this.DescribeCharacter(mob));
                this.Transition(SmokeState.StartAttack, "mob found");
                return;
            }

            if (this.TimedOut(12))
            {
                this.Fail("Timed out waiting for spawned " + this.mobName + " (" + this.mobAlias + ").");
            }
        }

        private void StartAttack()
        {
            SimpleChar target = this.GetTargetMob();
            LocalPlayer player = DynelManager.LocalPlayer;
            if (target == null || player == null)
            {
                this.Fail("Target or player disappeared before attack.");
                return;
            }

            target.Target();
            Network.Send(new AttackMessage
            {
                Identity = player.Identity,
                Target = target.Identity,
                Unknown1 = 0
            });

            this.Write("Sent attack. player=" + player.Identity + " target=" + this.DescribeCharacter(target));
            this.Transition(SmokeState.WaitCorpse, "attack sent");
        }

        private void WaitCorpse()
        {
            Corpse corpse = this.FindNewCorpse();
            if (corpse != null)
            {
                this.corpseIdentity = corpse.Identity;
                this.Write("Found corpse: " + this.DescribeCorpse(corpse));
                this.Transition(SmokeState.OpenFirst, "corpse found");
                return;
            }

            SimpleChar target = this.GetTargetMob();
            if (target != null && target.IsAlive && DateTime.UtcNow >= this.nextActionUtc)
            {
                this.nextActionUtc = DateTime.UtcNow.AddSeconds(3);
                Network.Send(new AttackMessage
                {
                    Identity = DynelManager.LocalPlayer.Identity,
                    Target = target.Identity,
                    Unknown1 = 0
                });
                this.Write("Attack resend while waiting for corpse: " + this.DescribeCharacter(target));
            }

            if (this.TimedOut(25))
            {
                this.Fail("Timed out waiting for corpse after attack.");
            }
        }

        private void OpenFirst()
        {
            Corpse corpse = this.GetCorpse();
            if (corpse == null)
            {
                this.Fail("Corpse disappeared before first open.");
                return;
            }

            corpse.Open();
            this.Write("Opened corpse first time: " + this.DescribeCorpse(corpse));
            this.Transition(SmokeState.WaitFirstItems, "first corpse use sent");
        }

        private void WaitFirstItems()
        {
            Corpse corpse = this.GetCorpse();
            if (corpse == null)
            {
                this.Fail("Corpse disappeared before loot inventory arrived.");
                return;
            }

            List<Item> items = this.GetCorpseItems();
            if (items.Count >= 2 || (!this.requireReopen && items.Count > 0))
            {
                this.Write("Corpse first open has " + items.Count.ToString(CultureInfo.InvariantCulture) + " items: " + this.DescribeItems(items));
                this.Transition(SmokeState.LootFirst, "first loot list ready");
                return;
            }

            if (items.Count == 1 && this.TimedOut(3))
            {
                this.Fail("Corpse only has one item; cannot exercise close/reopen with remaining loot. items=" + this.DescribeItems(items));
                return;
            }

            if (this.TimedOut(8))
            {
                this.Fail("Timed out waiting for first corpse loot list. open=" + Safe(() => corpse.IsOpen.ToString()));
            }
        }

        private void LootFirst()
        {
            List<Item> items = this.GetCorpseItems();
            int minimumItems = this.requireReopen ? 2 : 1;
            if (items.Count < minimumItems)
            {
                this.Fail("Expected at least " + minimumItems.ToString(CultureInfo.InvariantCulture)
                    + " corpse item(s) before first loot; found "
                    + items.Count.ToString(CultureInfo.InvariantCulture));
                return;
            }

            Item item = this.ChooseItemToLoot(items, true);
            if (item == null)
            {
                this.Fail("No lootable first item was available. items=" + this.DescribeItems(items));
                return;
            }

            this.itemCountBeforeMove = items.Count;
            this.lootAttemptCount++;
            this.attemptedLootSlots.Add(ItemSlotKey(item));
            item.MoveToInventory(MoveToInventoryPlacement);
            this.Write("First loot attempt " + this.lootAttemptCount.ToString(CultureInfo.InvariantCulture) + ": " + this.DescribeItem(item));
            this.Transition(SmokeState.WaitFirstMoved, "first move sent");
        }

        private void WaitFirstMoved()
        {
            List<Item> items = this.GetCorpseItems();
            if (items.Count < this.itemCountBeforeMove)
            {
                this.Write("First item moved. Remaining items=" + this.DescribeItems(items));
                if (!this.requireReopen)
                {
                    this.attemptedLootSlots.Clear();
                    this.Transition(items.Count == 0 ? SmokeState.WaitCorpseGone : SmokeState.LootRemaining, "basic first move observed");
                }
                else
                {
                    this.Transition(SmokeState.CloseLootWindow, "first move observed");
                }

                return;
            }

            if (this.TimedOut(4))
            {
                this.Write("First loot attempt did not reduce corpse items. before="
                    + this.itemCountBeforeMove.ToString(CultureInfo.InvariantCulture)
                    + " now="
                    + items.Count.ToString(CultureInfo.InvariantCulture)
                    + " items="
                    + this.DescribeItems(items));

                if (this.lootAttemptCount < Math.Max(1, items.Count))
                {
                    this.Transition(SmokeState.LootFirst, "try another first item");
                }
                else
                {
                    this.Fail("Unable to loot the first item. This can happen if all remaining items are duplicate unique items.");
                }
            }
        }

        private void CloseLootWindow()
        {
            if (this.closeIssued)
            {
                Corpse corpse = this.GetCorpse();
                bool serverCloseSignal = this.lastCorpseAccessSignalUtc >= this.stateStartedUtc;
                this.closeConfirmed = corpse == null || !SafeBool(() => corpse.IsOpen) || serverCloseSignal;

                if (this.closeConfirmed || this.ElapsedInState().TotalSeconds >= 1.0)
                {
                    this.Transition(
                        SmokeState.ReopenCorpse,
                        this.closeConfirmed ? "container close signal observed" : "close delay elapsed without open-state change");
                }

                return;
            }

            string closeError;
            if (TryCloseContainer(this.corpseIdentity, out closeError))
            {
                this.closeHookCalled = true;
                this.Write("Called InventoryGUIModule.CloseContainer for corpse " + this.corpseIdentity);
            }
            else
            {
                this.Write("InventoryGUIModule.CloseContainer failed: " + closeError);
                Window activeWindow = Window.GetActiveWindow();
                if (activeWindow != null)
                {
                    this.lastCloseWindowName = activeWindow.Name;
                    activeWindow.Close();
                    this.Write("Closed active window after first loot. name=" + this.lastCloseWindowName);
                }
                else
                {
                    this.Write("No active window found to close after first loot.");
                }
            }

            this.closeIssued = true;
        }

        private void ReopenCorpse()
        {
            Corpse corpse = this.GetCorpse();
            if (corpse == null)
            {
                this.Fail("Corpse disappeared before reopen.");
                return;
            }

            this.reopenUseCount++;
            this.reopenSentUtc = DateTime.UtcNow;
            corpse.Open();
            this.Write("Reopen use sent count=" + this.reopenUseCount.ToString(CultureInfo.InvariantCulture)
                + " corpse=" + this.DescribeCorpse(corpse)
                + " closedWindow=" + this.lastCloseWindowName);
            this.Transition(SmokeState.WaitReopened, "reopen use sent");
        }

        private void WaitReopened()
        {
            Corpse corpse = this.GetCorpse();
            if (corpse == null)
            {
                this.Fail("Corpse disappeared while waiting for reopen.");
                return;
            }

            List<Item> items = this.GetCorpseItems();
            bool sawServerSignal = this.lastCorpseInventorySignalUtc >= this.reopenSentUtc
                || this.lastCorpseAccessSignalUtc >= this.reopenSentUtc;
            bool looksOpen = SafeBool(() => corpse.IsOpen);

            if (items.Count > 0 && sawServerSignal)
            {
                this.Write("Corpse reopened. open=" + looksOpen
                    + " signal=" + sawServerSignal
                    + " items=" + this.DescribeItems(items));
                this.attemptedLootSlots.Clear();
                this.Transition(SmokeState.LootRemaining, "reopen observed");
                return;
            }

            if (this.ElapsedInState().TotalSeconds > 2 && this.reopenUseCount < 2)
            {
                this.Transition(SmokeState.ReopenCorpse, "second reopen use");
                return;
            }

            if (this.TimedOut(7))
            {
                this.Fail("Timed out waiting for corpse reopen. open=" + looksOpen + " items=" + this.DescribeItems(items));
            }
        }

        private void LootRemaining()
        {
            List<Item> items = this.GetCorpseItems();
            if (items.Count == 0)
            {
                this.Transition(SmokeState.WaitCorpseGone, "corpse empty");
                return;
            }

            Item item = this.ChooseItemToLoot(items, false);
            if (item == null)
            {
                if (this.OnlyUnlootableDuplicateUniqueItems(items))
                {
                    this.Pass("Spawned, killed, opened, looted, reopened, and skipped remaining duplicate unique item(s) already owned by the player.");
                    return;
                }

                this.Fail("No remaining loot item could be selected. items=" + this.DescribeItems(items));
                return;
            }

            this.itemCountBeforeMove = items.Count;
            this.lootAttemptCount++;
            this.attemptedLootSlots.Add(ItemSlotKey(item));
            item.MoveToInventory(MoveToInventoryPlacement);
            this.Write("Remaining loot attempt " + this.lootAttemptCount.ToString(CultureInfo.InvariantCulture) + ": " + this.DescribeItem(item));
            this.Transition(SmokeState.WaitRemainingMoved, "remaining move sent");
        }

        private void WaitRemainingMoved()
        {
            List<Item> items = this.GetCorpseItems();
            if (items.Count < this.itemCountBeforeMove)
            {
                this.Write("Remaining item moved. Remaining count=" + items.Count.ToString(CultureInfo.InvariantCulture)
                    + " items=" + this.DescribeItems(items));
                this.attemptedLootSlots.Clear();
                this.Transition(items.Count == 0 ? SmokeState.WaitCorpseGone : SmokeState.LootRemaining, "remaining move observed");
                return;
            }

            if (this.TimedOut(4))
            {
                if (this.attemptedLootSlots.Count < items.Count)
                {
                    this.Write("Remaining loot attempt did not move; trying another item. items=" + this.DescribeItems(items));
                    this.Transition(SmokeState.LootRemaining, "try another remaining item");
                }
                else
                {
                    this.Fail("Unable to loot remaining item(s). This can happen if the only remaining loot is a duplicate unique item. items=" + this.DescribeItems(items));
                }
            }
        }

        private void WaitCorpseGone()
        {
            Corpse corpse = this.GetCorpse();
            if (corpse == null)
            {
                if (!this.requireReopen)
                {
                    this.Pass("Spawned, killed, opened, looted DB/basic item(s), and corpse despawned.");
                    return;
                }

                string closeText = this.closeConfirmed
                    ? "closed corpse container through client hook"
                    : (this.closeHookCalled ? "called client close hook" : "close not confirmed");
                this.Pass("Spawned, killed, opened, looted first item, " + closeText + ", reopened, looted remaining items, corpse despawned.");
                return;
            }

            if (this.TimedOut(12))
            {
                this.Fail("Corpse did not despawn after all loot was taken. corpse=" + this.DescribeCorpse(corpse));
            }
        }

        private SimpleChar FindAliveTestMob()
        {
            return SafeArray(() => DynelManager.NPCs.ToArray())
                .Where(character => character != null && SafeBool(() => character.IsAlive) && this.IsTestMob(character))
                .OrderBy(character => this.DistanceFromPlayer(character))
                .FirstOrDefault();
        }

        private SimpleChar GetTargetMob()
        {
            if (this.targetIdentity == Identity.None)
            {
                return this.FindAliveTestMob();
            }

            return SafeArray(() => DynelManager.NPCs.ToArray())
                .FirstOrDefault(character => character.Identity == this.targetIdentity);
        }

        private Corpse FindNewCorpse()
        {
            Corpse[] corpses = SafeArray(() => DynelManager.Corpses.ToArray());
            IEnumerable<Corpse> candidates = corpses
                .Where(corpse => corpse != null && !this.knownCorpses.Contains(corpse.Identity))
                .OrderBy(corpse => this.DistanceFromPlayer(corpse));

            string expectedCorpseName = "Remains of " + this.mobName;
            Corpse named = candidates.FirstOrDefault(
                corpse => string.Equals(Safe(() => corpse.Name), expectedCorpseName, StringComparison.OrdinalIgnoreCase));
            if (named != null)
            {
                return named;
            }

            return null;
        }

        private Corpse GetCorpse()
        {
            if (this.corpseIdentity == Identity.None)
            {
                return null;
            }

            return SafeArray(() => DynelManager.Corpses.ToArray())
                .FirstOrDefault(corpse => corpse.Identity == this.corpseIdentity);
        }

        private List<Item> GetCorpseItems()
        {
            Corpse corpse = this.GetCorpse();
            if (corpse == null)
            {
                return new List<Item>();
            }

            try
            {
                return corpse.Container.Items ?? new List<Item>();
            }
            catch (Exception ex)
            {
                this.Write("GetCorpseItems failed: " + ex.Message);
                return new List<Item>();
            }
        }

        private Item ChooseItemToLoot(List<Item> items, bool leaveLoot)
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            List<Item> lootableItems = items.Where(this.CanLootItem).ToList();
            IEnumerable<Item> candidates = lootableItems.Where(item => !this.attemptedLootSlots.Contains(ItemSlotKey(item)));
            if (leaveLoot)
            {
                candidates = candidates.Where(item => lootableItems.Any(other => !SameSlot(other, item)));
            }

            List<Item> candidateList = candidates.ToList();
            if (candidateList.Count == 0)
            {
                return null;
            }

            if (leaveLoot)
            {
                Item uniqueNotOwned = candidateList.FirstOrDefault(item => this.IsUniqueTestItem(item));
                if (uniqueNotOwned != null)
                {
                    return uniqueNotOwned;
                }

                Item itemLeavingNonUnique = candidateList.FirstOrDefault(item =>
                    !this.IsUniqueTestItem(item)
                    && lootableItems.Any(other => !SameSlot(other, item)));
                if (itemLeavingNonUnique != null)
                {
                    return itemLeavingNonUnique;
                }
            }

            Item nonUnique = candidateList.FirstOrDefault(item => !this.IsUniqueTestItem(item));
            return nonUnique ?? candidateList[0];
        }

        private bool IsTestMob(SimpleChar character)
        {
            return string.Equals(Safe(() => character.Name), this.mobName, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsUniqueTestItem(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return item.Id == UniqueTestLowId
                || item.Id == UniqueTestHighId
                || item.HighId == UniqueTestLowId
                || item.HighId == UniqueTestHighId;
        }

        private bool CanLootItem(Item item)
        {
            return item != null
                && (!this.IsUniqueTestItem(item) || !this.InventoryContainsItem(item));
        }

        private bool OnlyUnlootableDuplicateUniqueItems(List<Item> items)
        {
            return items != null
                && items.Count > 0
                && items.All(item => this.IsUniqueTestItem(item) && this.InventoryContainsItem(item));
        }

        private bool InventoryContainsItem(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return SafeArray(() => Inventory.Items.ToArray()).Any(existing =>
                existing.Id == item.Id
                || existing.Id == item.HighId
                || existing.HighId == item.Id
                || existing.HighId == item.HighId);
        }

        private List<Item> GetInventoryCleanupItems()
        {
            return SafeArray(() => Inventory.Items.ToArray())
                .Where(this.IsCleanupTestLootItem)
                .OrderBy(item => Safe(() => item.Name))
                .ThenBy(item => ItemSlotKey(item))
                .ToList();
        }

        private bool IsCleanupTestLootItem(Item item)
        {
            return item != null
                && (TestLootCleanupItemIds.Contains(item.Id)
                    || TestLootCleanupItemIds.Contains(item.HighId));
        }

        private void DeleteInventoryTestLoot(string reason)
        {
            this.DeleteInventoryTestLoot(this.GetInventoryCleanupItems(), reason);
        }

        private void DeleteInventoryTestLoot(List<Item> items, string reason)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            int deleted = 0;
            foreach (Item item in items.Take(CleanupBatchSize))
            {
                try
                {
                    item.Delete();
                    deleted++;
                    this.Write("Deleted test loot from inventory for " + reason + ": " + this.DescribeItem(item));
                }
                catch (Exception ex)
                {
                    this.Write("Delete test loot failed for " + reason + ": " + ex.Message + " item=" + this.DescribeItem(item));
                }
            }

            this.Write(
                "Test loot cleanup sent "
                + deleted.ToString(CultureInfo.InvariantCulture)
                + "/"
                + items.Count.ToString(CultureInfo.InvariantCulture)
                + " delete request(s) for "
                + reason
                + ".");
        }

        private void Transition(SmokeState nextState, string reason)
        {
            this.state = nextState;
            this.stateStartedUtc = DateTime.UtcNow;
            this.Write("STATE " + nextState + " reason=" + reason);
        }

        private bool TimedOut(double seconds)
        {
            return this.ElapsedInState().TotalSeconds >= seconds;
        }

        private TimeSpan ElapsedInState()
        {
            return DateTime.UtcNow - this.stateStartedUtc;
        }

        private void Pass(string message)
        {
            this.WriteResult("PASS", message);
            this.DeleteInventoryTestLoot("pass");
            Chat.WriteLine("Combat loot smoke PASS: " + message, ChatColor.Green);
            this.Transition(SmokeState.Passed, "pass");
            this.CloseLog();
        }

        private void Fail(string message)
        {
            this.WriteResult("FAIL", message);
            this.DeleteInventoryTestLoot("fail");
            Chat.WriteLine("Combat loot smoke FAIL: " + message, ChatColor.Red);
            this.Transition(SmokeState.Failed, "fail");
            this.CloseLog();
        }

        private void WriteResult(string result, string message)
        {
            string text = string.Format(
                CultureInfo.InvariantCulture,
                "{0:o} RESULT {1} state={2} elapsedSeconds={3:0.0} message={4} log={5}",
                DateTime.UtcNow,
                result,
                this.state,
                (DateTime.UtcNow - this.runStartedUtc).TotalSeconds,
                OneLine(message),
                this.logPath ?? string.Empty);

            this.Write(text);

            try
            {
                File.WriteAllText(this.resultFilePath, text + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
            }
        }

        private string StatusText()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Combat loot smoke state={0} running={1} expected={2} mob={3}/{4} target={5} corpse={6} log={7}",
                this.state,
                this.IsRunning,
                string.IsNullOrWhiteSpace(this.expectedCharacterName) ? "(any)" : this.expectedCharacterName,
                this.mobAlias,
                this.mobName,
                this.targetIdentity,
                this.corpseIdentity,
                this.logPath ?? "(none)");
        }

        private void Write(string message)
        {
            string line = string.Format(CultureInfo.InvariantCulture, "{0:o} {1}", DateTime.UtcNow, OneLine(message));
            this.captureLog(OneLine(message));

            try
            {
                if (this.log != null)
                {
                    this.log.WriteLine(line);
                }
            }
            catch
            {
            }
        }

        private void CloseLog()
        {
            try
            {
                this.log?.Flush();
                this.log?.Dispose();
            }
            catch
            {
            }

            this.log = null;
        }

        private string DescribeCharacter(SimpleChar character)
        {
            if (character == null)
            {
                return "null";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "identity={0} name={1} hp={2}/{3} alive={4} attacking={5} pos={6}",
                character.Identity,
                Safe(() => character.Name),
                Safe(() => character.Health.ToString(CultureInfo.InvariantCulture)),
                Safe(() => character.MaxHealth.ToString(CultureInfo.InvariantCulture)),
                Safe(() => character.IsAlive.ToString()),
                Safe(() => character.IsAttacking.ToString()),
                Safe(() => character.Position.ToString()));
        }

        private string DescribeCorpse(Corpse corpse)
        {
            if (corpse == null)
            {
                return "null";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "identity={0} name={1} open={2} pos={3} items={4}",
                corpse.Identity,
                Safe(() => corpse.Name),
                Safe(() => corpse.IsOpen.ToString()),
                Safe(() => corpse.Position.ToString()),
                this.DescribeItems(SafeList(() => corpse.Container.Items)));
        }

        private string DescribeItems(IEnumerable<Item> items)
        {
            if (items == null)
            {
                return "null";
            }

            return string.Join(
                ";",
                items.Select(this.DescribeItem).ToArray());
        }

        private string DescribeItem(Item item)
        {
            if (item == null)
            {
                return "null";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "slot={0} id={1}/{2} ql={3} name={4} uniqueTest={5}",
                item.Slot,
                item.Id,
                item.HighId,
                item.QualityLevel,
                Safe(() => item.Name),
                this.IsUniqueTestItem(item));
        }

        private string DescribeMessage(N3Message message)
        {
            if (message == null)
            {
                return "null";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("identity=");
            builder.Append(message.Identity);

            foreach (System.Reflection.PropertyInfo property in message.GetType().GetProperties())
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0 || property.Name == "Identity")
                {
                    continue;
                }

                object value;
                try
                {
                    value = property.GetValue(message, null);
                }
                catch
                {
                    continue;
                }

                builder.Append(' ');
                builder.Append(property.Name);
                builder.Append('=');
                builder.Append(FormatValue(value));
            }

            return builder.ToString();
        }

        private float DistanceFromPlayer(Dynel dynel)
        {
            LocalPlayer player = DynelManager.LocalPlayer;
            if (dynel == null || player == null)
            {
                return float.MaxValue;
            }

            return SafeFloat(() => dynel.DistanceFrom(player));
        }

        private static string ItemSlotKey(Item item)
        {
            return item == null ? string.Empty : item.Slot.ToString();
        }

        private static bool TryCloseContainer(Identity identity, out string error)
        {
            error = string.Empty;

            try
            {
                IntPtr module = InventoryGuiModuleNative.GetInstance();
                if (module == IntPtr.Zero)
                {
                    error = "InventoryGUIModule instance was null";
                    return false;
                }

                InventoryGuiModuleNative.CloseContainer(module, ref identity);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool SameSlot(Item left, Item right)
        {
            return left != null && right != null && left.Slot == right.Slot;
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

            System.Collections.IEnumerable enumerable = value as System.Collections.IEnumerable;
            if (enumerable != null && !(value is string))
            {
                List<string> parts = new List<string>();
                int count = 0;
                foreach (object item in enumerable)
                {
                    if (parts.Count < 6)
                    {
                        parts.Add(FormatValue(item));
                    }

                    count++;
                }

                return "count=" + count.ToString(CultureInfo.InvariantCulture) + "[" + string.Join(",", parts.ToArray()) + "]";
            }

            return value.ToString();
        }

        private static T[] SafeArray<T>(Func<T[]> func)
        {
            try
            {
                return func() ?? new T[0];
            }
            catch
            {
                return new T[0];
            }
        }

        private static List<T> SafeList<T>(Func<List<T>> func)
        {
            try
            {
                return func() ?? new List<T>();
            }
            catch
            {
                return new List<T>();
            }
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
                return float.MaxValue;
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

        private string ParseRequestValue(string request, string key)
        {
            if (string.IsNullOrWhiteSpace(request) || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            foreach (string line in request.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string lineKey = line.Substring(0, separator).Trim();
                if (string.Equals(lineKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(separator + 1).Trim();
                }
            }

            return string.Empty;
        }

        private bool ParseRequestBool(string request, string key, bool defaultValue)
        {
            string value = this.ParseRequestValue(request, key);
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            bool parsed;
            if (bool.TryParse(value, out parsed))
            {
                return parsed;
            }

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeMobAlias(string requestedMobAlias)
        {
            return string.IsNullOrWhiteSpace(requestedMobAlias)
                ? DefaultMobAlias
                : requestedMobAlias.Trim();
        }

        private static string ResolveMobName(string requestedMobAlias, string requestedMobName)
        {
            if (!string.IsNullOrWhiteSpace(requestedMobName))
            {
                return requestedMobName.Trim();
            }

            string knownName;
            if (KnownMobNames.TryGetValue(NormalizeMobAlias(requestedMobAlias), out knownName))
            {
                return knownName;
            }

            return DefaultMobName;
        }

        private static class InventoryGuiModuleNative
        {
            [DllImport("GUI.dll", EntryPoint = "?GetInstanceIfAny@InventoryGUIModule_c@@SAPAV1@XZ", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr GetInstance();

            [DllImport("GUI.dll", EntryPoint = "?CloseContainer@InventoryGUIModule_c@@QAEXABVIdentity_t@@@Z", CallingConvention = CallingConvention.ThisCall)]
            public static extern void CloseContainer(IntPtr pThis, ref Identity identity);
        }
    }
}
