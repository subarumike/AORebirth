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

using AORebirth.CaptureProtocol;
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
        private const int CaptureStopQuietPeriodSeconds = 2;
        private const int CaptureStopMaximumDrainSeconds = 5;
        private const int RawCaptureGateOpen = 0;
        private const int RawCaptureGateTentative = 1;
        private const int RawCaptureGateClosed = 2;
        private static Main activeInstance;

        private readonly object syncRoot = new object();
        private readonly HashSet<string> knownCharacters = new HashSet<string>();
        private readonly HashSet<string> knownCorpses = new HashSet<string>();
        private readonly HashSet<string> exportedShopUpdateFingerprints = new HashSet<string>();
        private readonly HashSet<string> vendorInteractionIdentities = new HashSet<string>();
        private readonly HashSet<string> shopUpdateIdentities = new HashSet<string>();
        private readonly HashSet<string> vendorFullUpdateIdentities = new HashSet<string>();
        private readonly HashSet<string> focusedEnemyIdentities = new HashSet<string>();
        private readonly Dictionary<string, RecentEnemyFullUpdateEvidence> recentEnemyFullUpdates =
            new Dictionary<string, RecentEnemyFullUpdateEvidence>();
        private readonly Dictionary<string, EnemyEntityState> enemyStates = new Dictionary<string, EnemyEntityState>();
        private readonly Dictionary<string, List<EnemyStateEvent>> enemyStateTimeline = new Dictionary<string, List<EnemyStateEvent>>();
        private readonly Dictionary<string, CorpseLifecycleEvidence> corpseEvidenceByDeadNpc =
            new Dictionary<string, CorpseLifecycleEvidence>();
        private readonly Dictionary<string, CorpseLifecycleEvidence> activeCorpseEvidenceByCorpse =
            new Dictionary<string, CorpseLifecycleEvidence>();
        private readonly Dictionary<string, int> corpseInventorySnapshotCounts =
            new Dictionary<string, int>();
        private readonly HashSet<string> corpseLootInitialEnemyKeys = new HashSet<string>();
        private readonly CaptureCallbackBoundary callbackBoundary = new CaptureCallbackBoundary();

        private const int CorpseFullUpdateDeadNpcTypeOffset = 183;
        private const int CorpseFullUpdateDeadNpcInstanceOffset = 191;
        private const int CorpseFullUpdateMonsterDataSuffixOffset = 72;
        private const int CorpseFullUpdateTailDeadNpcTypeSuffixOffset = 80;
        private const int CorpseFullUpdateTailDeadNpcInstanceSuffixOffset = 84;
        private const string LootCaptureRequestFileName = "loot-10.request";
        private const string ExternalControlRequestFileName = "AOSharpLiveCapture.control";
        private const string CaptureBootstrapReadyEventPrefix = @"Local\AOSharpCaptureBootstrap_";
        private const string CaptureBootstrapChannelSuffix = "_capture_safe";
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
            "FollowTarget",
            "StopFight",
            "FightModeUpdate",
            "Attack",
            "AttackInfo",
            "SpecialAttackWeapon",
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
            "N3Teleport",
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
        private string pluginDirectory;
        private StreamWriter eventsLog;
        private StreamWriter packetsLog;
        private StreamWriter rawPacketsCsvLog;
        private StreamWriter scfuAppearanceLog;
        private StreamWriter shopUpdatesLog;
        private StreamWriter vendorFullUpdatesLog;
        private StreamWriter systemMessagesLog;
        private StreamWriter chatDialogueLog;
        private StreamWriter npcInteractionsLog;
        private StreamWriter inventoryUpdatesLog;
        private StreamWriter corpseLootObservationsLog;
        private StreamWriter enemyStateLog;
        private StreamWriter enemyFullUpdatesLog;
        private StreamWriter enemyCombatLog;
        private StreamWriter enemyMovementLog;
        private StreamWriter movementPacketsLog;
        private StreamWriter enemyStatUpdatesLog;
        private StreamWriter enemyFightEventsLog;
        private StreamWriter corpseFullUpdatesLog;
        private StreamWriter npcLifecycleLog;
        private bool enabled;
        private bool captureFinalized;
        private bool captureStopDrainRequested;
        private DateTime? captureStopRequestedUtc;
        private DateTime? captureStopQuietDeadlineUtc;
        private DateTime? captureStopMaximumDeadlineUtc;
        private DateTime? captureFinalizedUtc;
        private bool captureQuietPeriodPassed;
        private int inboundPacketCount;
        private int outboundPacketCount;
        private int decodedInboundCount;
        private int decodedOutboundCount;
        private int decodedN3EventRowCount;
        private int n3CaptureStageErrorCount;
        private int rawCombatPacketCount;
        private int rawSimpleCharFullUpdatePacketCount;
        private int rawSimpleCharFullUpdateDecodeCount;
        private int rawSimpleCharFullUpdateDecodeErrorCount;
        private int rawSimpleCharFullUpdateIncompleteDecodeCount;
        private int rawNpcSimpleCharFullUpdateCount;
        private int scfuAppearanceRowCount;
        private int rawPacketLogRowCount;
        private int rawPacketIndexRowCount;
        private int rawPacketPreservedCount;
        private int rawPacketWriteErrorCount;
        private int rawPacketProjectionErrorCount;
        private long rawPacketGlobalOrdinal;
        private int rawPacketCallbacksInFlight;
        private int rawCaptureGateState;
        private int rawPacketCallbackDrainTimeoutCount;
        private readonly Stopwatch captureClock = new Stopwatch();
        private int shopUpdateMessageCount;
        private int shopUpdateRowCount;
        private int vendorFullUpdateMessageCount;
        private int systemMessageCount;
        private int chatDialogueMessageCount;
        private int npcInteractionCount;
        private int inventoryUpdateMessageCount;
        private int inventoryUpdateRowCount;
        private int corpseLootObservationRowCount;
        private int corpseLootInitialSnapshotCount;
        private int corpseLootUnlinkedSnapshotCount;
        private int corpseLootMissingPlayerContextCount;
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
        private int movementPacketRowCount;
        private int movementFollowTargetPacketCount;
        private int movementUsableFollowTargetPacketCount;
        private int movementSetPosPacketCount;
        private int movementStopMovingCmdPacketCount;
        private int movementDecodeErrorCount;
        private int enemyStatUpdateRowCount;
        private int corpseFullUpdatePacketCount;
        private int corpseFullUpdateRowCount;
        private int corpseFullUpdateDecodeErrorCount;
        private int corpseInventoryUpdateCount;
        private int corpseSeenEventCount;
        private int corpseGoneEventCount;
        private int npcLifecycleRowCount;
        private DateTime nextFlushUtc;
        private DateTime nextSnapshotUtc;
        private DateTime nextExternalControlPollUtc;
        private DateTime captureStartUtc;
        private DateTime captureStartLocal;
        private DateTime lastPacketUtc;
        private DateTime localEnemyCombatContextUntilUtc;
        private string lastPlayfieldId = string.Empty;
        private string lastCapturePlayfieldIdentity = string.Empty;
        private string externalControlRequestPath;
        private string externalControlProcessingPath;
        private CombatLootSmoke combatLootSmoke;
        private MissionFlowCapture missionFlowCapture;
        private Pf127GeometryCapture pf127GeometryCapture;
        private int pf127CaptureRuntimeReady;
        private int callbackDispatchEnabled;
        private int pf127CollectionArmed;
        private int pf127CollectionArmedBeforeTeleport;
        private int teleportInProgress;
        private long teleportGeneration;
        private long playfieldInitGeneration = -1;
        private bool minimalPf127CaptureMode;
        private MinimalPf127Capture minimalPf127Capture;
        private bool initialized;
        private bool enemyFightCaptureEnabled;
        private bool enemyFightAutoCaptureEnabled = true;
        private bool enemyFightCaptureStarted;
        private bool respawnCaptureRequested;
        private bool lootCaptureRequested;

        public override void Run(string pluginDir)
        {
            bool initializationAttempted = false;
            this.callbackBoundary.ConfigureFallback(GetCallbackErrorFallbackPath(pluginDir));
            bool initializationSucceeded = this.callbackBoundary.Dispatch(
                "Run.Initialization",
                () =>
                {
                    lock (LifecycleSyncRoot)
                    {
                        if (activeInstance != null)
                        {
                            return;
                        }

                        activeInstance = this;
                        initializationAttempted = true;
                    }

                    if (MinimalPf127Capture.ConsumeRequestNoThrow(pluginDir))
                    {
                        this.StartMinimalPf127CaptureNoThrow(pluginDir);
                        return;
                    }

                    this.Initialize(pluginDir);
                });

            if (initializationAttempted && (!initializationSucceeded || !this.initialized))
            {
                this.DisableAfterInitializationFailureNoThrow();
            }
        }

        private void Initialize(string pluginDir)
        {
            this.pluginDirectory = pluginDir;
            this.externalControlRequestPath = Path.Combine(pluginDir, ExternalControlRequestFileName);
            this.externalControlProcessingPath = this.externalControlRequestPath + ".processing";
            Interlocked.Exchange(ref this.pf127CaptureRuntimeReady, 0);
            Interlocked.Exchange(ref this.callbackDispatchEnabled, 0);
            Interlocked.Exchange(ref this.pf127CollectionArmed, 0);
            Interlocked.Exchange(ref this.teleportInProgress, 0);
            lock (this.syncRoot)
            {
                this.OpenFreshCaptureSession(pluginDir, true, false);
                Interlocked.Exchange(ref this.callbackDispatchEnabled, 1);
                Network.PacketReceived += this.OnPacketReceivedBoundary;
                Network.PacketSent += this.OnPacketSentBoundary;
                this.ActivateCaptureSession();
            }

            this.combatLootSmoke = new CombatLootSmoke(pluginDir, this.LogSmokeEvent);
            this.missionFlowCapture = new MissionFlowCapture(this.LogEvent);
            this.missionFlowCapture.BindSession(this.sessionDirectory);

            Network.N3MessageReceived += this.OnN3MessageReceivedBoundary;
            Network.N3MessageSent += this.OnN3MessageSentBoundary;
            Network.ChatMessageReceived += this.OnChatMessageReceivedBoundary;
            DynelManager.DynelSpawned += this.OnDynelSpawnedBoundary;
            DynelManager.CharInPlay += this.OnCharInPlayBoundary;
            Game.PlayfieldInit += this.OnPlayfieldInitBoundary;
            Game.TeleportStarted += this.OnTeleportStartedBoundary;
            Game.TeleportEnded += this.OnTeleportEndedBoundary;
            Game.TeleportFailed += this.OnTeleportFailedBoundary;

            this.LogEvent("PLUGIN", "AOSharpLiveCapture loaded. session=" + this.sessionDirectory);
            this.LogEvent("PLUGIN", "Commands: /aocap start | stop | mark <text> | status | flush | snapshot | dynels [force] | fight start|stop|auto on|auto off|status");
            this.LogEvent("PLUGIN", "Smoke commands: /aosmoke start [mobAlias] | stop | status | log");
            this.LogEvent("PLUGIN", "External control fallback: " + this.externalControlRequestPath);
            this.LogEvent("PLUGIN", "Mission flow log: " + Path.Combine(this.sessionDirectory, "mission-flow.log"));
            this.LogEvent("PLUGIN", "ShopUpdate CSV: " + Path.Combine(this.sessionDirectory, "shop-updates.csv"));
            this.LogEvent("PLUGIN", "VendingMachineFullUpdate CSV: " + Path.Combine(this.sessionDirectory, "vendor-full-updates.csv"));
            this.LogEvent("PLUGIN", "System messages log: " + Path.Combine(this.sessionDirectory, "system-messages.log"));
            this.LogEvent("PLUGIN", "Chat/dialogue log: " + Path.Combine(this.sessionDirectory, "chat-dialogue.log"));
            this.LogEvent("PLUGIN", "NPC interactions log: " + Path.Combine(this.sessionDirectory, "npc-interactions.log"));
            this.LogEvent("PLUGIN", "Inventory update CSV: " + Path.Combine(this.sessionDirectory, "inventory-updates.csv"));
            this.LogEvent("PLUGIN", "Corpse loot observations CSV: " + Path.Combine(this.sessionDirectory, "corpse-loot-observations.csv"));
            this.LogEvent("PLUGIN", "Enemy state CSV: " + Path.Combine(this.sessionDirectory, "enemy-state.csv"));
            this.LogEvent("PLUGIN", "Enemy full update CSV: " + Path.Combine(this.sessionDirectory, "enemy-full-updates.csv"));
            this.LogEvent("PLUGIN", "Enemy combat CSV: " + Path.Combine(this.sessionDirectory, "enemy-combat.csv"));
            this.LogEvent("PLUGIN", "Enemy movement CSV: " + Path.Combine(this.sessionDirectory, "enemy-movement.csv"));
            this.LogEvent("PLUGIN", "Movement packet CSV: " + Path.Combine(this.sessionDirectory, "movement-packets.csv"));
            this.LogEvent("PLUGIN", "Enemy stat update CSV: " + Path.Combine(this.sessionDirectory, "enemy-stat-updates.csv"));
            this.LogEvent("PLUGIN", "Enemy fight events log: " + Path.Combine(this.sessionDirectory, "enemy-fight-events.log"));
            this.LogEvent("PLUGIN", "Enemy dossier JSON: " + Path.Combine(this.sessionDirectory, "enemy-dossier.json"));
            this.LogEvent("PLUGIN", "Enemy state JSON: " + Path.Combine(this.sessionDirectory, "enemy-state.json"));
            this.LogEvent(
                "PLUGIN",
                "PF127 native geometry probing is disabled in comprehensive mode; use the explicit geometry-only workflow.");
            this.LogEvent("PLUGIN", "Capture callback errors: " + Path.Combine(this.sessionDirectory, "capture-callback-errors.log"));
            this.LogEvent("PLUGIN", "Capture info: " + Path.Combine(this.sessionDirectory, "capture_info.json"));
            this.LogEvent("PLUGIN", "Capture session metadata: " + Path.Combine(this.sessionDirectory, "capture-session.json"));
            this.LogSnapshot("initial");
            Chat.WriteLine("AOSharpLiveCapture logging to " + this.sessionDirectory, ChatColor.Gold);
            Interlocked.Exchange(ref this.pf127CaptureRuntimeReady, 1);
            Game.OnUpdate += this.OnUpdateBoundary;

            // AOSharp has no command-unregister API. Register commands only after
            // the capture and every event callback are ready; the common boundary
            // makes these retained delegates inert as soon as the plugin is disabled.
            Chat.RegisterCommand("aocap", this.OnCommandBoundary);
            Chat.RegisterCommand("aosmoke", this.OnSmokeCommandBoundary);
            this.initialized = true;
            SignalCaptureBootstrapReady();
        }

        private static void SignalCaptureBootstrapReady()
        {
            string eventName = CaptureBootstrapReadyEventPrefix
                + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture)
                + CaptureBootstrapChannelSuffix;
            try
            {
                using (EventWaitHandle ready = EventWaitHandle.OpenExisting(eventName))
                {
                    ready.Set();
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The standard non-capture Bootstrap has no readiness event.
            }
        }

        public override void Teardown()
        {
            this.callbackBoundary.Dispatch("Plugin.Teardown", this.TeardownCore);
        }

        private void TeardownCore()
        {
            Interlocked.Exchange(ref this.pf127CaptureRuntimeReady, 0);
            Interlocked.Exchange(ref this.callbackDispatchEnabled, 0);
            if (this.minimalPf127CaptureMode)
            {
                this.TeardownMinimalPf127CaptureNoThrow();
                this.ClearActiveInstanceNoThrow();
                this.initialized = false;
                return;
            }

            this.UnsubscribeCallbacksNoThrow();
            this.callbackBoundary.Dispatch(
                "Teardown.CombatLootSmoke",
                () => this.combatLootSmoke?.Teardown());
            this.callbackBoundary.Dispatch(
                "Teardown.MissionFlowCapture",
                () => this.missionFlowCapture?.Teardown());
            this.callbackBoundary.Dispatch(
                "Teardown.FinalizeCapture",
                () =>
                {
                    if (!this.captureFinalized && !string.IsNullOrWhiteSpace(this.sessionDirectory))
                    {
                        this.TryLogEvent("PLUGIN", "AOSharpLiveCapture teardown.");
                        this.FinalizeCapture();
                    }
                });
            this.callbackBoundary.Dispatch("Teardown.FlushAndClose", this.FlushAndClose);
            this.ClearActiveInstanceNoThrow();
            this.initialized = false;
        }

        private void StartMinimalPf127CaptureNoThrow(string pluginDir)
        {
            this.minimalPf127CaptureMode = true;
            MinimalPf127Capture capture;
            string error;
            if (!MinimalPf127Capture.TryCreate(pluginDir, out capture, out error))
            {
                CaptureRuntimeSafety.InvokeFailSafe(
                    () => File.AppendAllText(
                        Path.Combine(pluginDir, "pf127-geometry-only-startup-error.log"),
                        DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                        + " "
                        + error
                        + Environment.NewLine));
                this.initialized = true;
                return;
            }

            this.minimalPf127Capture = capture;
            this.callbackBoundary.BeginSession(
                Path.Combine(capture.SessionDirectory, "capture-callback-errors.log"),
                GetCallbackErrorFallbackPath(pluginDir));
            Interlocked.Exchange(ref this.callbackDispatchEnabled, 1);
            Game.OnUpdate += this.OnMinimalPf127CaptureUpdateBoundary;
            Chat.WriteLine(
                "AOSharpLiveCapture PF127 geometry-only safe mode logging to " + capture.SessionDirectory,
                ChatColor.Gold);
            this.initialized = true;
            SignalCaptureBootstrapReady();
        }

        private void OnMinimalPf127CaptureUpdateBoundary(object sender, float deltaTime)
        {
            this.DispatchCallback(
                "Game.OnUpdate.MinimalPf127Capture",
                () => this.OnMinimalPf127CaptureUpdate(sender, deltaTime));
        }

        private void OnMinimalPf127CaptureUpdate(object sender, float deltaTime)
        {
            this.minimalPf127Capture?.UpdateNoThrow(DateTime.UtcNow);
        }

        private void TeardownMinimalPf127CaptureNoThrow()
        {
            this.callbackBoundary.Dispatch(
                "Unsubscribe.Game.OnUpdate.MinimalPf127Capture",
                () => Game.OnUpdate -= this.OnMinimalPf127CaptureUpdateBoundary);
            MinimalPf127Capture capture = this.minimalPf127Capture;
            this.minimalPf127Capture = null;
            this.callbackBoundary.Dispatch(
                "Teardown.MinimalPf127Capture",
                () => capture?.DisposeNoThrow());
        }

        private void DisableAfterInitializationFailureNoThrow()
        {
            Interlocked.Exchange(ref this.pf127CaptureRuntimeReady, 0);
            Interlocked.Exchange(ref this.callbackDispatchEnabled, 0);
            this.initialized = false;
            if (this.minimalPf127CaptureMode)
            {
                this.TeardownMinimalPf127CaptureNoThrow();
                this.ClearActiveInstanceNoThrow();
                return;
            }

            this.UnsubscribeCallbacksNoThrow();
            this.callbackBoundary.Dispatch(
                "Run.InitializationFailure.CombatLootSmoke",
                () => this.combatLootSmoke?.Teardown());
            this.callbackBoundary.Dispatch(
                "Run.InitializationFailure.MissionFlowCapture",
                () => this.missionFlowCapture?.Teardown());
            this.callbackBoundary.Dispatch(
                "Run.InitializationFailure.FinalizeCapture",
                () =>
                {
                    if (!this.captureFinalized && !string.IsNullOrWhiteSpace(this.sessionDirectory))
                    {
                        this.FinalizeCapture();
                    }
                });
            this.callbackBoundary.Dispatch(
                "Run.InitializationFailure.FlushAndClose",
                this.FlushAndClose);
            this.ClearActiveInstanceNoThrow();
        }

        private void UnsubscribeCallbacksNoThrow()
        {
            this.callbackBoundary.Dispatch(
                "Unsubscribe.Network.PacketReceived",
                () => Network.PacketReceived -= this.OnPacketReceivedBoundary);
            this.callbackBoundary.Dispatch(
                "Unsubscribe.Network.PacketSent",
                () => Network.PacketSent -= this.OnPacketSentBoundary);
            this.callbackBoundary.Dispatch(
                "Unsubscribe.Network.N3MessageReceived",
                () => Network.N3MessageReceived -= this.OnN3MessageReceivedBoundary);
            this.callbackBoundary.Dispatch(
                "Unsubscribe.Network.N3MessageSent",
                () => Network.N3MessageSent -= this.OnN3MessageSentBoundary);
            this.callbackBoundary.Dispatch(
                "Unsubscribe.Network.ChatMessageReceived",
                () => Network.ChatMessageReceived -= this.OnChatMessageReceivedBoundary);
            this.callbackBoundary.Dispatch(
                "Unsubscribe.DynelManager.DynelSpawned",
                () => DynelManager.DynelSpawned -= this.OnDynelSpawnedBoundary);
            this.callbackBoundary.Dispatch(
                "Unsubscribe.DynelManager.CharInPlay",
                () => DynelManager.CharInPlay -= this.OnCharInPlayBoundary);
            this.callbackBoundary.Dispatch(
                "Unsubscribe.Game.PlayfieldInit",
                () => Game.PlayfieldInit -= this.OnPlayfieldInitBoundary);
            this.callbackBoundary.Dispatch(
                "Unsubscribe.Game.TeleportStarted",
                () => Game.TeleportStarted -= this.OnTeleportStartedBoundary);
            this.callbackBoundary.Dispatch(
                "Unsubscribe.Game.TeleportEnded",
                () => Game.TeleportEnded -= this.OnTeleportEndedBoundary);
            this.callbackBoundary.Dispatch(
                "Unsubscribe.Game.TeleportFailed",
                () => Game.TeleportFailed -= this.OnTeleportFailedBoundary);
            this.callbackBoundary.Dispatch(
                "Unsubscribe.Game.OnUpdate",
                () => Game.OnUpdate -= this.OnUpdateBoundary);
        }

        private void ClearActiveInstanceNoThrow()
        {
            this.callbackBoundary.Dispatch(
                "Lifecycle.ClearActiveInstance",
                () =>
                {
                    lock (LifecycleSyncRoot)
                    {
                        if (ReferenceEquals(activeInstance, this))
                        {
                            activeInstance = null;
                        }
                    }
                });
        }

        private static string GetCallbackErrorFallbackPath(string pluginDir)
        {
            try
            {
                return string.IsNullOrWhiteSpace(pluginDir)
                    ? "capture-callback-errors.log"
                    : Path.Combine(pluginDir, "capture-callback-errors.log");
            }
            catch
            {
                return "capture-callback-errors.log";
            }
        }

        private void DispatchCallback(string callbackName, Action callback)
        {
            this.callbackBoundary.Dispatch(
                callbackName,
                () =>
                {
                    if (Volatile.Read(ref this.callbackDispatchEnabled) == 0)
                    {
                        return;
                    }

                    callback();
                });
        }

        private void OnCommandBoundary(string command, string[] args, ChatWindow chatWindow)
        {
            this.DispatchCallback("Chat.Command.aocap", () => this.OnCommand(command, args, chatWindow));
        }

        private void OnSmokeCommandBoundary(string command, string[] args, ChatWindow chatWindow)
        {
            this.DispatchCallback("Chat.Command.aosmoke", () => this.OnSmokeCommand(command, args, chatWindow));
        }

        private void OnPacketReceivedBoundary(object sender, byte[] packet)
        {
            this.DispatchCallback("Network.PacketReceived", () => this.OnPacketReceived(sender, packet));
        }

        private void OnPacketSentBoundary(object sender, byte[] packet)
        {
            this.DispatchCallback("Network.PacketSent", () => this.OnPacketSent(sender, packet));
        }

        private void OnN3MessageReceivedBoundary(object sender, N3Message message)
        {
            this.DispatchCallback(
                "Network.N3MessageReceived",
                () => this.OnN3MessageReceived(sender, message));
        }

        private void OnN3MessageSentBoundary(object sender, N3Message message)
        {
            this.DispatchCallback(
                "Network.N3MessageSent",
                () => this.OnN3MessageSent(sender, message));
        }

        private void OnChatMessageReceivedBoundary(object sender, ChatMessageBody message)
        {
            this.DispatchCallback(
                "Network.ChatMessageReceived",
                () => this.OnChatMessageReceived(sender, message));
        }

        private void OnDynelSpawnedBoundary(object sender, Dynel dynel)
        {
            this.DispatchCallback(
                "DynelManager.DynelSpawned",
                () => this.OnDynelSpawned(sender, dynel));
        }

        private void OnCharInPlayBoundary(object sender, SimpleChar character)
        {
            this.DispatchCallback(
                "DynelManager.CharInPlay",
                () => this.OnCharInPlay(sender, character));
        }

        private void OnPlayfieldInitBoundary(object sender, uint playfieldId)
        {
            this.DispatchCallback(
                "Game.PlayfieldInit",
                () => this.OnPlayfieldInit(sender, playfieldId));
        }

        private void OnTeleportStartedBoundary(object sender, EventArgs e)
        {
            this.DispatchCallback(
                "Game.TeleportStarted",
                () => this.OnTeleportStarted(sender, e));
        }

        private void OnTeleportEndedBoundary(object sender, EventArgs e)
        {
            this.DispatchCallback(
                "Game.TeleportEnded",
                () => this.OnTeleportEnded(sender, e));
        }

        private void OnTeleportFailedBoundary(object sender, EventArgs e)
        {
            this.DispatchCallback(
                "Game.TeleportFailed",
                () => this.OnTeleportFailed(sender, e));
        }

        private void OnUpdateBoundary(object sender, float deltaTime)
        {
            this.DispatchCallback("Game.OnUpdate", () => this.OnUpdate(sender, deltaTime));
        }

        private void OnCommand(string command, string[] args, ChatWindow chatWindow)
        {
            string subCommand = args.Length == 0 ? "status" : args[0].ToLowerInvariant();
            switch (subCommand)
            {
                case "start":
                    this.OpenFreshCaptureSession(this.pluginDirectory, true, true);
                    this.LogEvent("COMMAND", "capture started");
                    chatWindow.WriteLine("AO capture started: " + this.sessionDirectory, ChatColor.Gold);
                    break;

                case "stop":
                    if (this.RequestCaptureStop(DateTime.UtcNow, "COMMAND"))
                    {
                        chatWindow.WriteLine("AO capture stop requested; finalizing after the packet drain.", ChatColor.Gold);
                    }
                    break;

                case "mark":
                    string marker = args.Length > 1 ? string.Join(" ", args.Skip(1).ToArray()) : "(no text)";
                    this.RecordCaptureMarker(marker);
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
                            "AO capture {0}. fightManual={1} fightAuto={2} focusedEnemies={3} inRaw={4} outRaw={5} inN3={6} outN3={7} dir={8}",
                            this.enabled ? "running" : "stopped",
                            this.enemyFightCaptureEnabled ? "on" : "off",
                            this.enemyFightAutoCaptureEnabled ? "on" : "off",
                            this.focusedEnemyIdentities.Count,
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

                case "auto":
                    string autoMode = args.Length > 2 ? args[2].ToLowerInvariant() : "status";
                    if (autoMode == "on" || autoMode == "start")
                    {
                        this.enemyFightAutoCaptureEnabled = true;
                        this.LogEvent("COMMAND", "enemy fight auto capture enabled");
                    }
                    else if (autoMode == "off" || autoMode == "stop")
                    {
                        this.enemyFightAutoCaptureEnabled = false;
                        this.LogEvent("COMMAND", "enemy fight auto capture disabled");
                    }

                    this.TryWriteChat(
                        chatWindow,
                        "AO enemy fight auto capture " + (this.enemyFightAutoCaptureEnabled ? "running." : "stopped."),
                        ChatColor.Gold);
                    break;

                default:
                    this.TryWriteChat(
                        chatWindow,
                        "AO enemy fight capture manual="
                        + (this.enemyFightCaptureEnabled ? "running" : "stopped")
                        + " auto="
                        + (this.enemyFightAutoCaptureEnabled ? "running" : "stopped")
                        + " focusedEnemies="
                        + this.focusedEnemyIdentities.Count.ToString(CultureInfo.InvariantCulture),
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
            this.CaptureNetworkPacketNoThrow("IN", packet, true);
        }

        private void OnPacketSent(object sender, byte[] packet)
        {
            this.CaptureNetworkPacketNoThrow("OUT", packet, false);
        }

        private void CaptureNetworkPacketNoThrow(string direction, byte[] packet, bool inbound)
        {
            while (true)
            {
                int gateState = Volatile.Read(ref this.rawCaptureGateState);
                if (gateState == RawCaptureGateClosed)
                {
                    return;
                }

                if (gateState == RawCaptureGateTentative)
                {
                    Thread.Yield();
                    continue;
                }

                Interlocked.Increment(ref this.rawPacketCallbacksInFlight);
                if (Volatile.Read(ref this.rawCaptureGateState) == RawCaptureGateOpen)
                {
                    break;
                }

                this.ReleaseRawPacketCallbackRegistration();
            }

            try
            {
                lock (this.syncRoot)
                {
                    if (!this.enabled)
                    {
                        return;
                    }

                    int sequence;
                    if (inbound)
                    {
                        sequence = ++this.inboundPacketCount;
                    }
                    else
                    {
                        sequence = ++this.outboundPacketCount;
                    }

                    this.lastPacketUtc = DateTime.UtcNow;
                    this.LogPacket(direction, sequence, packet);
                }
            }
            catch
            {
                Interlocked.Increment(ref this.rawPacketWriteErrorCount);
                throw;
            }
            finally
            {
                this.ReleaseRawPacketCallbackRegistration();
            }
        }

        private void ReleaseRawPacketCallbackRegistration()
        {
            if (Interlocked.Decrement(ref this.rawPacketCallbacksInFlight) == 0)
            {
                lock (this.syncRoot)
                {
                    Monitor.PulseAll(this.syncRoot);
                }
            }
        }

        private void OnN3MessageReceived(object sender, N3Message message)
        {
            lock (this.syncRoot)
            {
                if (!this.enabled || message == null)
                {
                    return;
                }

                this.decodedInboundCount++;
                this.lastPacketUtc = DateTime.UtcNow;
                int sequence = this.decodedInboundCount;
                this.RunN3CaptureStage(
                    "IN-N3",
                    sequence,
                    message,
                    "combat-loot-smoke",
                    () => this.combatLootSmoke?.OnN3MessageReceived(message));
                this.RunN3CaptureStage(
                    "IN-N3",
                    sequence,
                    message,
                    "mission-flow",
                    () => this.missionFlowCapture?.OnN3MessageReceived(message));
                this.RunN3CaptureStage(
                    "IN-N3",
                    sequence,
                    message,
                    "decoded-message-pipeline",
                    () => this.LogN3Message("IN-N3", sequence, message));
            }
        }

        private void OnN3MessageSent(object sender, N3Message message)
        {
            lock (this.syncRoot)
            {
                if (!this.enabled || message == null)
                {
                    return;
                }

                this.decodedOutboundCount++;
                this.lastPacketUtc = DateTime.UtcNow;
                int sequence = this.decodedOutboundCount;
                this.RunN3CaptureStage(
                    "OUT-N3",
                    sequence,
                    message,
                    "combat-loot-smoke",
                    () => this.combatLootSmoke?.OnN3MessageSent(message));
                this.RunN3CaptureStage(
                    "OUT-N3",
                    sequence,
                    message,
                    "mission-flow",
                    () => this.missionFlowCapture?.OnN3MessageSent(message));
                this.RunN3CaptureStage(
                    "OUT-N3",
                    sequence,
                    message,
                    "decoded-message-pipeline",
                    () => this.LogN3Message("OUT-N3", sequence, message));
            }
        }

        private void OnChatMessageReceived(object sender, ChatMessageBody message)
        {
            lock (this.syncRoot)
            {
                if (!this.enabled || message == null)
                {
                    return;
                }

                this.LogEvent("CHAT", this.DescribeObject(message));
                this.LogChatDialogue("CHAT", 0, message.PacketType.ToString(), "chat-protocol", this.DescribeObject(message));
            }
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
            this.TrackEnemyFromCharacter(character, "spawn", "CHAR-IN-PLAY");
        }

        private void OnPlayfieldInit(object sender, uint playfieldId)
        {
            if (!this.enabled)
            {
                return;
            }

            this.lastPlayfieldId = playfieldId.ToString(CultureInfo.InvariantCulture);
            Interlocked.Exchange(
                ref this.playfieldInitGeneration,
                Interlocked.Read(ref this.teleportGeneration));
            Interlocked.Exchange(ref this.pf127CollectionArmed, 0);
            this.pf127GeometryCapture?.NotifyPlayfieldChanged(false);
            this.LogEvent("PLAYFIELD-INIT", this.lastPlayfieldId);
            this.missionFlowCapture?.OnPlayfieldInit(playfieldId);
            this.knownCharacters.Clear();
            this.knownCorpses.Clear();
        }

        private void OnTeleportStarted(object sender, EventArgs e)
        {
            if (!this.enabled)
            {
                return;
            }

            Interlocked.Exchange(
                ref this.pf127CollectionArmedBeforeTeleport,
                Volatile.Read(ref this.pf127CollectionArmed));
            Interlocked.Increment(ref this.teleportGeneration);
            Interlocked.Exchange(ref this.teleportInProgress, 1);
            Interlocked.Exchange(ref this.pf127CollectionArmed, 0);
            this.pf127GeometryCapture?.NotifyPlayfieldChanged(false);
            this.LogEvent("TELEPORT", "started");
            this.missionFlowCapture?.OnTeleportStarted();
        }

        private void OnTeleportEnded(object sender, EventArgs e)
        {
            if (!this.enabled)
            {
                return;
            }

            long generation = Interlocked.Read(ref this.teleportGeneration);
            bool matchingPlayfieldInit = Interlocked.Read(ref this.playfieldInitGeneration) == generation;
            bool isPf127 = matchingPlayfieldInit
                           && string.Equals(this.lastPlayfieldId, "127", StringComparison.Ordinal);
            Interlocked.Exchange(ref this.teleportInProgress, 0);
            Interlocked.Exchange(ref this.pf127CollectionArmed, isPf127 ? 1 : 0);
            this.LogEvent("TELEPORT", "ended");
            this.missionFlowCapture?.OnTeleportEnded();
            this.pf127GeometryCapture?.NotifyPlayfieldChanged(isPf127);
            this.pf127GeometryCapture?.RequestImmediateUpdate();
            this.LogSnapshot("teleport-ended");
        }

        private void OnTeleportFailed(object sender, EventArgs e)
        {
            if (!this.enabled)
            {
                return;
            }

            bool restorePf127 = Volatile.Read(ref this.pf127CollectionArmedBeforeTeleport) != 0;
            Interlocked.Exchange(ref this.teleportInProgress, 0);
            Interlocked.Exchange(ref this.pf127CollectionArmed, restorePf127 ? 1 : 0);
            this.pf127GeometryCapture?.NotifyPlayfieldChanged(restorePf127);
            if (restorePf127)
            {
                this.pf127GeometryCapture?.RequestImmediateUpdate();
            }

            this.LogEvent("TELEPORT", "failed");
        }

        private void OnUpdate(object sender, float deltaTime)
        {
            DateTime now = DateTime.UtcNow;
            this.PollExternalControl(now);
            if (!this.enabled)
            {
                return;
            }

            this.combatLootSmoke?.Update(deltaTime);

            Pf127GeometryCapture geometryCapture = this.pf127GeometryCapture;
            if (geometryCapture != null
                && Volatile.Read(ref this.pf127CaptureRuntimeReady) != 0
                && Volatile.Read(ref this.teleportInProgress) == 0
                && Volatile.Read(ref this.pf127CollectionArmed) != 0)
            {
                geometryCapture.ExecuteUpdateBoundary(
                    now,
                    () => this.IsDetectedResourcePlayfield127(),
                    () => this.GetDetectedPlayfieldId());
            }
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

            if (this.captureStopDrainRequested
                && this.captureStopQuietDeadlineUtc.HasValue
                && this.captureStopMaximumDeadlineUtc.HasValue)
            {
                bool captureBoundaryClosed = Volatile.Read(ref this.rawCaptureGateState)
                                             == RawCaptureGateClosed;
                bool quietPeriodPassed = now >= this.captureStopQuietDeadlineUtc.Value
                    && (now - this.lastPacketUtc).TotalSeconds >= CaptureStopQuietPeriodSeconds;
                bool maximumDrainReached = now >= this.captureStopMaximumDeadlineUtc.Value;
                if (captureBoundaryClosed || quietPeriodPassed || maximumDrainReached)
                {
                    this.CompleteCaptureStop(now, quietPeriodPassed, true);
                }
            }
        }

        private void PollExternalControl(DateTime now)
        {
            if (now < this.nextExternalControlPollUtc)
            {
                return;
            }

            this.nextExternalControlPollUtc = now.AddMilliseconds(250);
            if (string.IsNullOrEmpty(this.externalControlRequestPath)
                || !File.Exists(this.externalControlRequestPath))
            {
                return;
            }

            try
            {
                if (File.Exists(this.externalControlProcessingPath))
                {
                    return;
                }

                File.Move(this.externalControlRequestPath, this.externalControlProcessingPath);
                string request = File.ReadAllText(this.externalControlProcessingPath).Trim();
                File.Delete(this.externalControlProcessingPath);

                if (string.Equals(request, "start", StringComparison.OrdinalIgnoreCase))
                {
                    if (!this.enabled)
                    {
                        this.OpenFreshCaptureSession(this.pluginDirectory, true, true);
                        this.LogEvent("EXTERNAL-CONTROL", "capture started");
                    }

                    return;
                }

                if (!this.enabled)
                {
                    return;
                }

                if (string.Equals(request, "stop", StringComparison.OrdinalIgnoreCase))
                {
                    this.RequestCaptureStop(now, "EXTERNAL-CONTROL");
                    return;
                }

                if (string.Equals(request, "flush", StringComparison.OrdinalIgnoreCase))
                {
                    this.Flush();
                    this.LogEvent("EXTERNAL-CONTROL", "capture flushed");
                    return;
                }

                if (string.Equals(request, "snapshot", StringComparison.OrdinalIgnoreCase))
                {
                    this.LogSnapshot("external-control");
                    this.LogEvent("EXTERNAL-CONTROL", "snapshot written");
                    return;
                }

                if (string.Equals(request, "mark", StringComparison.OrdinalIgnoreCase)
                    || request.StartsWith("mark ", StringComparison.OrdinalIgnoreCase))
                {
                    string marker = request.Length > 4 ? request.Substring(5).Trim() : "(no text)";
                    if (string.IsNullOrEmpty(marker))
                    {
                        marker = "(no text)";
                    }

                    this.RecordCaptureMarker(marker);
                    this.LogEvent("EXTERNAL-CONTROL", "marker written");
                    return;
                }

                this.LogEvent("EXTERNAL-CONTROL", "unknown request ignored: " + request);
            }
            catch (Exception ex)
            {
                if (this.enabled)
                {
                    this.LogEvent("EXTERNAL-CONTROL", "request failed: " + ex);
                }

                try
                {
                    if (!string.IsNullOrEmpty(this.externalControlProcessingPath)
                        && File.Exists(this.externalControlProcessingPath))
                    {
                        File.Delete(this.externalControlProcessingPath);
                    }
                }
                catch
                {
                }
            }
        }

        private bool RequestCaptureStop(DateTime stopUtc, string source)
        {
            if (this.captureStopDrainRequested)
            {
                return false;
            }

            this.captureStopDrainRequested = true;
            this.captureStopRequestedUtc = stopUtc;
            this.captureStopQuietDeadlineUtc = stopUtc.AddSeconds(CaptureStopQuietPeriodSeconds);
            this.captureStopMaximumDeadlineUtc = stopUtc.AddSeconds(CaptureStopMaximumDrainSeconds);
            this.LogEvent(source, "capture stop requested; draining raw packets until quiet");
            this.Flush();
            return true;
        }

        private void RecordCaptureMarker(string marker)
        {
            if (marker.IndexOf("respawn", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                this.respawnCaptureRequested = true;
            }

            if (marker.IndexOf("loot", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                this.lootCaptureRequested = true;
            }

            this.LogEvent("MARK", marker);
        }

        private void CompleteCaptureStop(
            DateTime finalizedUtc,
            bool quietPeriodPassed,
            bool recheckDrainDeadline)
        {
            lock (this.syncRoot)
            {
                if (this.captureFinalized)
                {
                    return;
                }

                if (recheckDrainDeadline)
                {
                    int gateState = Volatile.Read(ref this.rawCaptureGateState);
                    if (gateState != RawCaptureGateClosed)
                    {
                        finalizedUtc = DateTime.UtcNow;
                        quietPeriodPassed = this.captureStopQuietDeadlineUtc.HasValue
                                            && finalizedUtc >= this.captureStopQuietDeadlineUtc.Value
                                            && (finalizedUtc - this.lastPacketUtc).TotalSeconds
                                            >= CaptureStopQuietPeriodSeconds;
                        bool maximumDrainReached = this.captureStopMaximumDeadlineUtc.HasValue
                                                   && finalizedUtc >= this.captureStopMaximumDeadlineUtc.Value;
                        if (!quietPeriodPassed && !maximumDrainReached)
                        {
                            return;
                        }

                        if (maximumDrainReached)
                        {
                            Interlocked.Exchange(
                                ref this.rawCaptureGateState,
                                RawCaptureGateClosed);
                        }
                        else
                        {
                            Interlocked.Exchange(
                                ref this.rawCaptureGateState,
                                RawCaptureGateTentative);
                            if (Volatile.Read(ref this.rawPacketCallbacksInFlight) != 0)
                            {
                                Interlocked.Exchange(
                                    ref this.rawCaptureGateState,
                                    RawCaptureGateOpen);
                                return;
                            }

                            Interlocked.Exchange(
                                ref this.rawCaptureGateState,
                                RawCaptureGateClosed);
                        }

                        this.captureQuietPeriodPassed = quietPeriodPassed;
                    }

                    quietPeriodPassed = this.captureQuietPeriodPassed;
                    if (Volatile.Read(ref this.rawPacketCallbacksInFlight) != 0)
                    {
                        return;
                    }

                    finalizedUtc = DateTime.UtcNow;
                }

                this.captureFinalized = true;
                this.captureFinalizedUtc = finalizedUtc;
                this.captureQuietPeriodPassed = quietPeriodPassed;
                this.captureStopDrainRequested = false;
                this.captureStopRequestedUtc = this.captureStopRequestedUtc ?? finalizedUtc;
                this.enabled = false;
                this.captureClock.Stop();

                this.FlushAndCloseRawWritersNoThrow();
                this.RunFinalizationStage("enemy-state-json", this.WriteEnemyStateJson);
                this.RunFinalizationStage("enemy-dossier-json", this.WriteEnemyDossierJson);
                this.RunFinalizationStage("movement-summary-json", this.WriteMovementSummaryJson);
                this.RunFinalizationStage("enemy-respawn-csv", () => this.WriteEnemyRespawnCsv(finalizedUtc));
                this.Flush();

                CaptureValidation validation;
                try
                {
                    validation = this.ValidateCapture();
                }
                catch (Exception ex)
                {
                    this.rawPacketProjectionErrorCount++;
                    bool recaptureRequired = this.IsCaptureRecaptureRequired();
                    validation = new CaptureValidation(
                        "incomplete",
                        false,
                        recaptureRequired,
                        !recaptureRequired,
                        new List<string>
                        {
                            "Capture validation failed: " + ex.GetType().Name + ": " + ex.Message
                        },
                        new List<string>());
                }

                this.RunFinalizationStage("capture-health", () => this.WriteCaptureHealth(validation));
                this.RunFinalizationStage("capture-info", () => this.WriteCaptureInfo(finalizedUtc, validation));
                this.LogEvent(
                    "CAPTURE-VALIDATION",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "status={0} processingAllowed={1} recaptureRequired={2} offlineDecodeRequired={3} issues={4} quietPeriodPassed={5}",
                        validation.Status,
                        validation.ProcessingAllowed,
                        validation.RecaptureRequired,
                        validation.OfflineDecodeRequired,
                        validation.Issues.Count,
                        quietPeriodPassed));
                this.FlushAndClose();
            }

            try
            {
                Chat.WriteLine("AO capture finalized: " + this.sessionDirectory, ChatColor.Gold);
            }
            catch
            {
            }
        }

        private void RunFinalizationStage(string stage, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                this.rawPacketProjectionErrorCount++;
                this.LogEvent(
                    "CAPTURE-FINALIZATION-ERROR",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "stage={0} error={1}: {2}",
                        stage,
                        ex.GetType().Name,
                        ex.Message));
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
                    bool firstSeen = this.knownCharacters.Add(key);
                    if (firstSeen)
                    {
                        this.LogEvent("CHAR-SEEN", this.DescribeCharacter(character));
                        this.LogNpcLifecycleRow(
                            "LOCAL",
                            0,
                            "character-seen",
                            "DynelSnapshot",
                            character.Identity.ToString(),
                            string.Empty,
                            character.Name,
                            this.DescribeCharacter(character));
                    }

                    // Dungeon playfields do not provide additional zone boundaries as the player
                    // moves between rooms. Sample every currently visible character so one traversal
                    // preserves later stat changes and a position history instead of only the first
                    // in-range snapshot.
                    this.TrackEnemyFromCharacter(
                        character,
                        firstSeen ? "spawn" : "survey",
                        firstSeen ? "CHAR-SEEN" : "VISIBLE-SURVEY");
                }

                foreach (string removed in this.knownCharacters.Except(currentCharacters).ToArray())
                {
                    this.LogEvent("CHAR-GONE", removed);
                    this.LogNpcLifecycleRow(
                        "LOCAL",
                        0,
                        "character-gone",
                        "DynelSnapshot",
                        removed,
                        string.Empty,
                        string.Empty,
                        string.Empty);
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
                        this.corpseSeenEventCount++;
                        this.LogEvent("CORPSE-SEEN", this.DescribeCorpse(corpse));
                        this.LogNpcLifecycleRow(
                            "LOCAL",
                            0,
                            "corpse-seen",
                            "DynelSnapshot",
                            corpse.Identity.ToString(),
                            string.Empty,
                            corpse.Name,
                            this.DescribeCorpse(corpse));
                    }
                }

                foreach (string removed in this.knownCorpses.Except(currentCorpses).ToArray())
                {
                    DateTime goneUtc = DateTime.UtcNow;
                    string normalizedCorpseIdentity = NormalizeIdentityKey(removed);
                    this.corpseGoneEventCount++;
                    this.LogEvent("CORPSE-GONE", removed);
                    this.LogNpcLifecycleRow(
                        "LOCAL",
                        0,
                        "corpse-gone",
                        "DynelSnapshot",
                        removed,
                        string.Empty,
                        string.Empty,
                        string.Empty);
                    lock (this.syncRoot)
                    {
                        foreach (CorpseLifecycleEvidence evidence in this.corpseEvidenceByDeadNpc.Values)
                        {
                            if (!evidence.CorpseGoneUtc.HasValue
                                && NormalizeIdentityKey(evidence.CorpseIdentity) == normalizedCorpseIdentity)
                            {
                                evidence.CorpseGoneUtc = goneUtc;
                            }
                        }
                        this.activeCorpseEvidenceByCorpse.Remove(normalizedCorpseIdentity);
                        this.corpseInventorySnapshotCounts.Remove(normalizedCorpseIdentity);
                    }
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
                string playfieldIdentity = Safe(() => Playfield.Identity.ToString());
                this.lastCapturePlayfieldIdentity = playfieldIdentity;

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
                        playfieldIdentity,
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
            DateTime capturedUtc = DateTime.UtcNow;
            double elapsedMilliseconds = this.captureClock.Elapsed.TotalMilliseconds;
            long globalOrdinal = Interlocked.Increment(ref this.rawPacketGlobalOrdinal);
            if (this.captureStopDrainRequested && this.captureStopMaximumDeadlineUtc.HasValue)
            {
                DateTime quietDeadline = capturedUtc.AddSeconds(CaptureStopQuietPeriodSeconds);
                this.captureStopQuietDeadlineUtc = quietDeadline < this.captureStopMaximumDeadlineUtc.Value
                                                       ? quietDeadline
                                                       : this.captureStopMaximumDeadlineUtc.Value;
            }

            int n3TypeValue = packet != null && packet.Length >= 20
                                  ? ReadInt32BigEndian(packet, 16)
                                  : 0;
            int identityType = packet != null && packet.Length >= 28
                                   ? ReadInt32BigEndian(packet, 20)
                                   : 0;
            int identityInstance = packet != null && packet.Length >= 28
                                       ? ReadInt32BigEndian(packet, 24)
                                       : 0;

            if (IsRawCombatEvidencePacket(packet))
            {
                this.rawCombatPacketCount++;
                this.pf127GeometryCapture?.RequestCombatSample();
            }

            if (packet != null
                && packet.Length >= 20
                && n3TypeValue == (int)N3MessageType.SimpleCharFullUpdate)
            {
                this.rawSimpleCharFullUpdatePacketCount++;
            }

            string rawHex = ToHex(packet);
            bool packetLogWritten = false;
            bool packetIndexWritten = false;

            try
            {
                if (this.packetsLog == null)
                {
                    throw new ObjectDisposedException("packets.hex.log");
                }

                this.packetsLog.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:o} {1} #{2} len={3} {4} hex={5}",
                        capturedUtc,
                        direction,
                        sequence,
                        packet == null ? 0 : packet.Length,
                        this.DescribeRawPacket(packet),
                        rawHex));
                if (packet != null)
                {
                    this.rawPacketLogRowCount++;
                    packetLogWritten = true;
                }
            }
            catch (Exception ex)
            {
                this.rawPacketWriteErrorCount++;
                this.LogEvent(
                    "RAW-PACKET-WRITE-ERROR",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "sink=packets.hex.log direction={0} sequence={1} ordinal={2} error={3}: {4}",
                        direction,
                        sequence,
                        globalOrdinal,
                        ex.GetType().Name,
                        ex.Message));
            }

            try
            {
                if (this.rawPacketsCsvLog == null)
                {
                    throw new ObjectDisposedException("raw-packets.csv");
                }

                this.rawPacketsCsvLog.WriteLine(
                    string.Join(
                        ",",
                        Csv(capturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                        elapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
                        Csv(direction),
                        globalOrdinal.ToString(CultureInfo.InvariantCulture),
                        sequence.ToString(CultureInfo.InvariantCulture),
                        (packet == null ? 0 : packet.Length).ToString(CultureInfo.InvariantCulture),
                        n3TypeValue.ToString(CultureInfo.InvariantCulture),
                        Csv(packet != null && packet.Length >= 20 ? ((N3MessageType)n3TypeValue).ToString() : string.Empty),
                        identityType.ToString(CultureInfo.InvariantCulture),
                        identityInstance.ToString(CultureInfo.InvariantCulture),
                        Csv(packet == null ? "raw_missing" : "raw_complete"),
                        Csv(rawHex)));
                if (packet != null)
                {
                    this.rawPacketIndexRowCount++;
                    packetIndexWritten = true;
                }
            }
            catch (Exception ex)
            {
                this.rawPacketWriteErrorCount++;
                this.LogEvent(
                    "RAW-PACKET-WRITE-ERROR",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "sink=raw-packets.csv direction={0} sequence={1} ordinal={2} error={3}: {4}",
                        direction,
                        sequence,
                        globalOrdinal,
                        ex.GetType().Name,
                        ex.Message));
            }

            if (packetLogWritten || packetIndexWritten)
            {
                this.rawPacketPreservedCount++;
            }
            else if (packet == null)
            {
                this.rawPacketWriteErrorCount++;
                this.LogEvent(
                    "RAW-PACKET-WRITE-ERROR",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "direction={0} sequence={1} ordinal={2} error=packet bytes were null",
                        direction,
                        sequence,
                        globalOrdinal));
            }

            if (n3TypeValue == (int)N3MessageType.SimpleCharFullUpdate)
            {
                this.RunRawPacketProjectionStage(
                    "scfu",
                    direction,
                    sequence,
                    globalOrdinal,
                    () => this.DecodeAndExportRawSimpleCharFullUpdate(
                        capturedUtc,
                        elapsedMilliseconds,
                        direction,
                        globalOrdinal,
                        sequence,
                        packet));
            }

            this.RunRawPacketProjectionStage(
                "movement",
                direction,
                sequence,
                globalOrdinal,
                () => this.ExportMovementPacket(direction, sequence, packet));
            this.RunRawPacketProjectionStage(
                "npc-lifecycle",
                direction,
                sequence,
                globalOrdinal,
                () => this.ExportNpcLifecyclePacket(direction, sequence, packet));
        }

        private void RunRawPacketProjectionStage(
            string stage,
            string direction,
            int sequence,
            long globalOrdinal,
            Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                this.rawPacketProjectionErrorCount++;
                this.LogEvent(
                    "RAW-PACKET-PROJECTION-ERROR",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "stage={0} direction={1} sequence={2} ordinal={3} error={4}: {5}",
                        stage,
                        direction,
                        sequence,
                        globalOrdinal,
                        ex.GetType().Name,
                        ex.Message));
            }
        }

        private void DecodeAndExportRawSimpleCharFullUpdate(
            DateTime capturedUtc,
            double elapsedMilliseconds,
            string direction,
            long globalOrdinal,
            int sequence,
            byte[] packet)
        {
            RawSimpleCharFullUpdate message;
            string decodeError;
            bool decoded = RawSimpleCharFullUpdateDecoder.TryDecodePacket(
                packet,
                out message,
                out decodeError);

            if (decoded)
            {
                this.rawSimpleCharFullUpdateDecodeCount++;
                if (!message.DecodeFullyConsumed)
                {
                    this.rawSimpleCharFullUpdateIncompleteDecodeCount++;
                }

                if (message.Npc != null)
                {
                    this.rawNpcSimpleCharFullUpdateCount++;
                }
            }
            else
            {
                this.rawSimpleCharFullUpdateDecodeErrorCount++;
            }

            var metadata = new RawScfuCaptureMetadata
            {
                CapturedUtc = capturedUtc.ToString("o", CultureInfo.InvariantCulture),
                ElapsedMilliseconds = elapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
                Direction = direction,
                GlobalOrdinal = globalOrdinal.ToString(CultureInfo.InvariantCulture),
                Sequence = sequence.ToString(CultureInfo.InvariantCulture)
            };

            lock (this.syncRoot)
            {
                this.scfuAppearanceLog.WriteLine(
                    RawScfuAppearanceCsv.FormatRow(
                        metadata,
                        packet,
                        message,
                        decodeError));
                this.scfuAppearanceLog.Flush();
                this.scfuAppearanceRowCount++;
            }

            if (decoded && message.Npc != null)
            {
                this.ExportEnemyFullUpdate(capturedUtc, direction, sequence, message);
                SimpleCharFullUpdateMessage adapted = AdaptRawSimpleCharFullUpdate(message);
                this.CacheEnemyFullUpdate(direction, sequence, adapted);
                this.TrackEnemyFromSimpleCharFullUpdate(direction, sequence, adapted);
            }

            if (!decoded || !message.DecodeFullyConsumed)
            {
                this.LogEvent(
                    decoded ? "RAW-SCFU-DECODE-PENDING" : "RAW-SCFU-DECODE-ERROR",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "direction={0} sequence={1} ordinal={2} error={3} undecodedTailBytes={4}",
                        direction,
                        sequence,
                        globalOrdinal,
                        decodeError,
                        message == null || message.UndecodedTail == null ? 0 : message.UndecodedTail.Length));
            }
        }

        private static SimpleCharFullUpdateMessage AdaptRawSimpleCharFullUpdate(
            RawSimpleCharFullUpdate raw)
        {
            var message = new SimpleCharFullUpdateMessage
            {
                Identity = ToAoIdentity(raw.Identity),
                Unknown = raw.HeaderUnknown,
                Version = raw.Version,
                Flags = (SimpleCharFullUpdateFlags)raw.Flags,
                PlayfieldId = raw.PlayfieldId,
                FightingTarget = raw.FightingTarget.HasValue
                                     ? (Identity?)ToAoIdentity(raw.FightingTarget.Value)
                                     : null,
                Position = new Vector3(raw.Position.X, raw.Position.Y, raw.Position.Z),
                Heading = new Quaternion(raw.Heading.X, raw.Heading.Y, raw.Heading.Z, raw.Heading.W),
                Appearance = new Appearance { Value = raw.AppearanceValue },
                Name = raw.Name,
                CharacterFlags = (CharacterFlags)raw.CharacterFlags,
                AccountFlags = raw.AccountFlags,
                Expansions = raw.Expansions,
                CharacterInfo = new SimpleCharInfo.NPCInfo
                {
                    Family = raw.Npc.Family,
                    LosHeight = raw.Npc.LosHeight
                },
                Level = raw.Level,
                Health = raw.Health,
                HealthDamage = raw.HealthDamage,
                MonsterData = raw.MonsterData,
                MonsterScale = raw.MonsterScale,
                VisualFlags = raw.VisualFlags,
                VisibleTitle = raw.VisibleTitle,
                ScfuUnk1 = raw.Unknown1,
                HeadMesh = raw.HeadMesh,
                RunSpeedBase = raw.RunSpeedBase,
                Flags2 = (ScfuFlags2)raw.Flags2,
                Owner = raw.Owner.HasValue ? (Identity?)ToAoIdentity(raw.Owner.Value) : null,
                ScfuUnk2 = raw.Unknown2,
                ScfuUnk4 = raw.Unknown4.GetValueOrDefault(),
                ScfuTowerUnk = raw.TowerUnknown.GetValueOrDefault()
            };

            return message;
        }

        private static Identity ToAoIdentity(RawScfuIdentity raw)
        {
            return new Identity((IdentityType)raw.Type, raw.Instance);
        }

        private void ExportNpcLifecyclePacket(string direction, int sequence, byte[] packet)
        {
            if (packet == null || packet.Length < 231)
            {
                return;
            }

            int messageType = ReadInt32BigEndian(packet, 16);
            if (messageType != (int)N3MessageType.CorpseFullUpdate)
            {
                return;
            }

            this.corpseFullUpdatePacketCount++;

            try
            {
                int nameOffset = FindAscii(packet, "Remains of ");
                if (nameOffset < 4)
                {
                    throw new InvalidDataException("CorpseFullUpdate has no encoded Remains name marker.");
                }

                int encodedNameLength = ReadInt32BigEndian(packet, nameOffset - 4);
                int suffixOffset = nameOffset + encodedNameLength;
                int monsterDataOffset = suffixOffset + CorpseFullUpdateMonsterDataSuffixOffset;
                int tailDeadNpcTypeOffset = suffixOffset + CorpseFullUpdateTailDeadNpcTypeSuffixOffset;
                int tailDeadNpcInstanceOffset = suffixOffset + CorpseFullUpdateTailDeadNpcInstanceSuffixOffset;

                if (encodedNameLength <= 0
                    || suffixOffset > packet.Length
                    || monsterDataOffset < suffixOffset
                    || tailDeadNpcInstanceOffset + 4 > packet.Length)
                {
                    throw new InvalidDataException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Invalid CorpseFullUpdate layout len={0} encodedNameLength={1} monsterDataOffset={2} tailOffset={3}.",
                            packet.Length,
                            encodedNameLength,
                            monsterDataOffset,
                            tailDeadNpcInstanceOffset));
                }

                uint corpseType = ReadUInt32BigEndian(packet, 20);
                uint corpseInstance = ReadUInt32BigEndian(packet, 24);
                uint deadNpcType = ReadUInt32BigEndian(packet, CorpseFullUpdateDeadNpcTypeOffset);
                uint deadNpcInstance = ReadUInt32BigEndian(packet, CorpseFullUpdateDeadNpcInstanceOffset);
                uint tailDeadNpcType = ReadUInt32BigEndian(packet, tailDeadNpcTypeOffset);
                uint tailDeadNpcInstance = ReadUInt32BigEndian(packet, tailDeadNpcInstanceOffset);
                string corpseName = Encoding.ASCII
                    .GetString(packet, nameOffset, encodedNameLength)
                    .TrimEnd('\0');
                DateTime capturedUtc = DateTime.UtcNow;
                string capturedUtcText = capturedUtc.ToString("o", CultureInfo.InvariantCulture);
                string corpseIdentity = FormatRawIdentity(corpseType, corpseInstance);
                string deadNpcIdentity = FormatRawIdentity(deadNpcType, deadNpcInstance);
                string deadNpcName = this.ResolveDynelName(deadNpcType, deadNpcInstance);

                lock (this.syncRoot)
                {
                    string normalizedCorpseIdentity = NormalizeIdentityKey(corpseIdentity);
                    string normalizedDeadNpcIdentity = NormalizeIdentityKey(deadNpcIdentity);
                    CorpseLifecycleEvidence priorGeneration;
                    bool isNewGeneration = !this.activeCorpseEvidenceByCorpse.TryGetValue(
                                               normalizedCorpseIdentity,
                                               out priorGeneration)
                                           || !string.Equals(
                                               NormalizeIdentityKey(priorGeneration.DeadNpcIdentity),
                                               normalizedDeadNpcIdentity,
                                               StringComparison.OrdinalIgnoreCase);
                    if (isNewGeneration)
                    {
                        if (priorGeneration != null && !priorGeneration.CorpseGoneUtc.HasValue)
                        {
                            priorGeneration.CorpseGoneUtc = capturedUtc;
                        }

                        this.corpseInventorySnapshotCounts.Remove(normalizedCorpseIdentity);
                    }

                    var corpseEvidence = new CorpseLifecycleEvidence
                    {
                        DeadNpcIdentity = deadNpcIdentity,
                        CorpseIdentity = corpseIdentity,
                        CorpseSeenUtc = capturedUtc,
                        PlayfieldId = ReadInt32BigEndian(packet, 73),
                        CorpseCredits = ReadInt32BigEndian(packet, 207),
                        CorpseMonsterData = ReadInt32BigEndian(packet, monsterDataOffset)
                    };
                    this.corpseEvidenceByDeadNpc[normalizedDeadNpcIdentity] = corpseEvidence;
                    this.activeCorpseEvidenceByCorpse[normalizedCorpseIdentity] = corpseEvidence;
                    this.corpseFullUpdateRowCount++;
                    this.corpseFullUpdatesLog.WriteLine(
                        string.Join(
                            ",",
                            Csv(capturedUtcText),
                            Csv(direction),
                            sequence.ToString(CultureInfo.InvariantCulture),
                            ReadUInt32BigEndian(packet, 12).ToString(CultureInfo.InvariantCulture),
                            Csv(FormatRawIdentityType(corpseType)),
                            Csv(FormatRawInstance(corpseInstance)),
                            Csv(corpseIdentity),
                            Csv(corpseName),
                            ReadInt32BigEndian(packet, 73).ToString(CultureInfo.InvariantCulture),
                            Csv(FormatFloat(ReadSingleBigEndian(packet, 45))),
                            Csv(FormatFloat(ReadSingleBigEndian(packet, 49))),
                            Csv(FormatFloat(ReadSingleBigEndian(packet, 53))),
                            ReadInt32BigEndian(packet, 143).ToString(CultureInfo.InvariantCulture),
                            ReadInt32BigEndian(packet, 159).ToString(CultureInfo.InvariantCulture),
                            ReadInt32BigEndian(packet, 167).ToString(CultureInfo.InvariantCulture),
                            ReadInt32BigEndian(packet, 175).ToString(CultureInfo.InvariantCulture),
                            Csv(FormatRawIdentityType(deadNpcType)),
                            Csv(FormatRawInstance(deadNpcInstance)),
                            Csv(deadNpcIdentity),
                            Csv(deadNpcName),
                            ReadInt32BigEndian(packet, 199).ToString(CultureInfo.InvariantCulture),
                            ReadInt32BigEndian(packet, 207).ToString(CultureInfo.InvariantCulture),
                            ReadInt32BigEndian(packet, monsterDataOffset).ToString(CultureInfo.InvariantCulture),
                            Csv(FormatRawIdentityType(tailDeadNpcType)),
                            Csv(FormatRawInstance(tailDeadNpcInstance)),
                            Csv(FormatRawIdentity(tailDeadNpcType, tailDeadNpcInstance)),
                            packet.Length.ToString(CultureInfo.InvariantCulture),
                            Csv(ToHex(packet))));
                    this.corpseFullUpdatesLog.Flush();
                }

                this.LogNpcLifecycleRow(
                    direction,
                    sequence,
                    "corpse-full-update",
                    N3MessageType.CorpseFullUpdate.ToString(),
                    deadNpcIdentity,
                    corpseIdentity,
                    corpseName,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "catMesh={0} monsterData={1} deadNpc={2} tailDeadNpc={3}",
                        ReadInt32BigEndian(packet, 199),
                        ReadInt32BigEndian(packet, monsterDataOffset),
                        deadNpcIdentity,
                        FormatRawIdentity(tailDeadNpcType, tailDeadNpcInstance)));
            }
            catch (Exception ex)
            {
                this.corpseFullUpdateDecodeErrorCount++;
                this.LogNpcLifecycleRow(
                    direction,
                    sequence,
                    "corpse-full-update-decode-error",
                    N3MessageType.CorpseFullUpdate.ToString(),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    ex.Message + " raw=" + ToHex(packet));
            }
        }

        private void LogNpcLifecycleRow(
            string direction,
            int sequence,
            string phase,
            string messageType,
            string primaryIdentity,
            string relatedIdentity,
            string name,
            string detail)
        {
            lock (this.syncRoot)
            {
                this.npcLifecycleRowCount++;
                this.npcLifecycleLog.WriteLine(
                    string.Join(
                        ",",
                        Csv(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                        Csv(direction),
                        sequence.ToString(CultureInfo.InvariantCulture),
                        Csv(phase),
                        Csv(messageType),
                        Csv(primaryIdentity),
                        Csv(relatedIdentity),
                        Csv(name),
                        Csv(detail)));
                this.npcLifecycleLog.Flush();
            }
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

        private void ExportMovementPacket(string direction, int sequence, byte[] packet)
        {
            if (packet == null || packet.Length < 29)
            {
                return;
            }

            try
            {
                int messageType = ReadInt32BigEndian(packet, 16);
                if (messageType == (int)N3MessageType.FollowTarget)
                {
                    this.ExportFollowTargetPacket(direction, sequence, packet);
                    return;
                }

                if (messageType == (int)N3MessageType.SetPos)
                {
                    this.ExportSetPosPacket(direction, sequence, packet);
                    return;
                }

                if (messageType == (int)N3MessageType.StopMovingCmd)
                {
                    this.ExportStopMovingCmdPacket(direction, sequence, packet);
                }
            }
            catch (Exception ex)
            {
                lock (this.syncRoot)
                {
                    this.movementDecodeErrorCount++;
                }

                if (this.movementDecodeErrorCount <= 5)
                {
                    this.LogEvent(
                        "MOVEMENT-DECODE-ERROR",
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} #{1}: {2}",
                            direction,
                            sequence,
                            ex.Message));
                }
            }
        }

        private void ExportFollowTargetPacket(string direction, int sequence, byte[] packet)
        {
            uint sourceType;
            uint sourceInstance;
            if (!TryReadRawIdentity(packet, 20, out sourceType, out sourceInstance))
            {
                return;
            }

            byte baseUnknown = packet[28];
            if (packet.Length < 31)
            {
                this.WriteMovementPacketRow(
                    direction,
                    sequence,
                    "FollowTarget",
                    sourceType,
                    sourceInstance,
                    this.ResolveDynelName(sourceType, sourceInstance),
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
                    "base_unknown=" + baseUnknown.ToString(CultureInfo.InvariantCulture),
                    GetRawTailHex(packet, 29));
                return;
            }

            byte followType = packet[29];
            byte followUnknown = packet[30];
            string flags = string.Format(
                CultureInfo.InvariantCulture,
                "base_unknown={0};follow_type={1};follow_unknown={2}",
                baseUnknown,
                followType,
                followUnknown);

            lock (this.syncRoot)
            {
                this.movementFollowTargetPacketCount++;
            }

            if (followType == 1)
            {
                this.ExportFollowTargetNpcPathPacket(direction, sequence, packet, sourceType, sourceInstance, followUnknown, flags);
                return;
            }

            if (followType == 2)
            {
                this.ExportFollowTargetTargetPacket(direction, sequence, packet, sourceType, sourceInstance, followUnknown, flags);
                return;
            }

            this.WriteMovementPacketRow(
                direction,
                sequence,
                "FollowTarget",
                sourceType,
                sourceInstance,
                this.ResolveDynelName(sourceType, sourceInstance),
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

        private void ExportFollowTargetNpcPathPacket(
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
                this.WriteMovementPacketRow(
                    direction,
                    sequence,
                    "FollowTarget",
                    sourceType,
                    sourceInstance,
                    this.ResolveDynelName(sourceType, sourceInstance),
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

            float? currentX = null;
            float? currentY = null;
            float? currentZ = null;
            float? destinationX = null;
            float? destinationY = null;
            float? destinationZ = null;

            if (decodedCoordinates > 0)
            {
                currentX = ReadSingleBigEndian(packet, coordinateOffset);
                currentY = ReadSingleBigEndian(packet, coordinateOffset + 4);
                currentZ = ReadSingleBigEndian(packet, coordinateOffset + 8);

                int destinationOffset = coordinateOffset + (decodedCoordinates - 1) * 12;
                destinationX = ReadSingleBigEndian(packet, destinationOffset);
                destinationY = ReadSingleBigEndian(packet, destinationOffset + 4);
                destinationZ = ReadSingleBigEndian(packet, destinationOffset + 8);
            }

            if (pathCount > 0 && decodedCoordinates == pathCount)
            {
                lock (this.syncRoot)
                {
                    this.movementUsableFollowTargetPacketCount++;
                }
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
                direction,
                sequence,
                "FollowTarget",
                sourceType,
                sourceInstance,
                this.ResolveDynelName(sourceType, sourceInstance),
                null,
                null,
                "NpcPath",
                FormatNullableFloat(currentX),
                FormatNullableFloat(currentY),
                FormatNullableFloat(currentZ),
                FormatNullableFloat(destinationX),
                FormatNullableFloat(destinationY),
                FormatNullableFloat(destinationZ),
                string.Empty,
                followUnknown.ToString(CultureInfo.InvariantCulture),
                flags,
                pathCount.ToString(CultureInfo.InvariantCulture),
                rawParams,
                GetRawTailHex(packet, tailOffset));
        }

        private void ExportFollowTargetTargetPacket(
            string direction,
            int sequence,
            byte[] packet,
            uint sourceType,
            uint sourceInstance,
            byte followUnknown,
            string flags)
        {
            uint targetType;
            uint targetInstance;
            uint? nullableTargetType = null;
            uint? nullableTargetInstance = null;
            string rawParams = flags;
            int tailOffset = 31;

            if (TryReadRawIdentity(packet, 31, out targetType, out targetInstance))
            {
                nullableTargetType = targetType;
                nullableTargetInstance = targetInstance;
                tailOffset = 39;
                rawParams += ";target=" + FormatRawIdentity(targetType, targetInstance);
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
                direction,
                sequence,
                "FollowTarget",
                sourceType,
                sourceInstance,
                this.ResolveDynelName(sourceType, sourceInstance),
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
        }

        private void ExportSetPosPacket(string direction, int sequence, byte[] packet)
        {
            if (packet.Length < 41)
            {
                return;
            }

            uint sourceType;
            uint sourceInstance;
            if (!TryReadRawIdentity(packet, 20, out sourceType, out sourceInstance))
            {
                return;
            }

            byte baseUnknown = packet[28];
            string flags = "base_unknown=" + baseUnknown.ToString(CultureInfo.InvariantCulture);

            lock (this.syncRoot)
            {
                this.movementSetPosPacketCount++;
            }

            this.WriteMovementPacketRow(
                direction,
                sequence,
                "SetPos",
                sourceType,
                sourceInstance,
                this.ResolveDynelName(sourceType, sourceInstance),
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

        private void ExportStopMovingCmdPacket(string direction, int sequence, byte[] packet)
        {
            if (packet.Length < 41)
            {
                return;
            }

            uint sourceType;
            uint sourceInstance;
            if (!TryReadRawIdentity(packet, 20, out sourceType, out sourceInstance))
            {
                return;
            }

            byte baseUnknown = packet[28];
            string rawParams = string.Format(
                CultureInfo.InvariantCulture,
                "base_unknown={0};unknown1={1};unknown2={2};unknown3={3}",
                baseUnknown,
                ReadInt32BigEndian(packet, 29),
                ReadInt32BigEndian(packet, 33),
                ReadInt32BigEndian(packet, 37));

            lock (this.syncRoot)
            {
                this.movementStopMovingCmdPacketCount++;
            }

            this.WriteMovementPacketRow(
                direction,
                sequence,
                "StopMovingCmd",
                sourceType,
                sourceInstance,
                this.ResolveDynelName(sourceType, sourceInstance),
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
                rawParams,
                GetRawTailHex(packet, 41));
        }

        private void WriteMovementPacketRow(
            string direction,
            int sequence,
            string messageType,
            uint sourceType,
            uint sourceInstance,
            string sourceName,
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
            lock (this.syncRoot)
            {
                this.movementPacketRowCount++;
                this.movementPacketsLog.WriteLine(
                    string.Join(
                        ",",
                        Csv(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                        Csv(direction),
                        sequence.ToString(CultureInfo.InvariantCulture),
                        Csv(messageType),
                        Csv(FormatRawIdentityType(sourceType)),
                        Csv(FormatRawInstance(sourceInstance)),
                        Csv(FormatRawIdentity(sourceType, sourceInstance)),
                        Csv(sourceName),
                        Csv(targetType.HasValue ? FormatRawIdentityType(targetType.Value) : string.Empty),
                        Csv(targetInstance.HasValue ? FormatRawInstance(targetInstance.Value) : string.Empty),
                        Csv(targetType.HasValue && targetInstance.HasValue ? FormatRawIdentity(targetType.Value, targetInstance.Value) : string.Empty),
                        Csv(targetType.HasValue && targetInstance.HasValue ? this.ResolveDynelName(targetType.Value, targetInstance.Value) : string.Empty),
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
                this.movementPacketsLog.Flush();
            }
        }

        private void LogN3Message(string direction, int sequence, N3Message message)
        {
            string messageName = Safe(() => message.N3MessageType.ToString());
            this.LogEvent(
                direction,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "#{0} type={1} identity={2}",
                    sequence,
                    messageName,
                    Safe(() => message.Identity.ToString())));
            this.decodedN3EventRowCount++;

            if (this.interestingMessageNames.Contains(messageName))
            {
                this.RunN3CaptureStage(
                    direction,
                    sequence,
                    message,
                    "decoded-detail",
                    () => this.LogEvent(direction + "-DETAIL", this.DescribeN3Message(message)));
            }

            this.RunN3CaptureStage(
                direction,
                sequence,
                message,
                "specialized-export",
                () => this.ExportSpecializedMessage(direction, sequence, message));
            this.RunN3CaptureStage(
                direction,
                sequence,
                message,
                "npc-lifecycle-export",
                () => this.ExportNpcLifecycleMessage(direction, sequence, message));
            this.RunN3CaptureStage(
                direction,
                sequence,
                message,
                "enemy-full-update-cache",
                () => this.CacheEnemyFullUpdate(direction, sequence, message));
            this.RunN3CaptureStage(
                direction,
                sequence,
                message,
                "enemy-fight-annotation",
                () =>
                {
                    if (this.ShouldCaptureEnemyFightEvidence(direction, sequence, message))
                    {
                        this.LogEnemyFightEvent(direction, sequence, message);
                    }
                });
            // Focus/manual modes only annotate. Independent stages preserve every
            // decoded combat/state message even if annotation or classification fails.
            this.RunN3CaptureStage(
                direction,
                sequence,
                message,
                "enemy-evidence-export",
                () => this.ExportEnemyN3Evidence(direction, sequence, message));
            this.RunN3CaptureStage(
                direction,
                sequence,
                message,
                "enemy-state-track",
                () => this.TrackEnemyStateFromMessage(direction, sequence, message));
            this.RunN3CaptureStage(
                direction,
                sequence,
                message,
                "shop-export",
                () =>
                {
                    ShopUpdateMessage shopUpdate = message as ShopUpdateMessage;
                    if (shopUpdate != null)
                    {
                        this.ExportShopUpdate(direction, sequence, shopUpdate);
                    }
                });
            this.RunN3CaptureStage(
                direction,
                sequence,
                message,
                "vendor-full-update-export",
                () =>
                {
                    if (string.Equals(
                        message.N3MessageType.ToString(),
                        "VendingMachineFullUpdate",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        this.ExportVendorFullUpdate(direction, sequence, message);
                    }
                });
            this.RunN3CaptureStage(
                direction,
                sequence,
                message,
                "inventory-export",
                () =>
                {
                    if (string.Equals(
                        message.N3MessageType.ToString(),
                        "InventoryUpdate",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        this.ExportInventoryUpdate(direction, sequence, message);
                    }
                });
        }

        private void RunN3CaptureStage(
            string direction,
            int sequence,
            N3Message message,
            string stage,
            Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                this.n3CaptureStageErrorCount++;
                try
                {
                    this.LogEvent(
                        "N3-STAGE-ERROR",
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "direction={0} sequence={1} type={2} stage={3} error={4}: {5}",
                            direction,
                            sequence,
                            message == null ? "<null>" : Safe(() => message.N3MessageType.ToString()),
                            stage,
                            ex.GetType().Name,
                            OneLine(ex.Message)));
                }
                catch
                {
                }
            }
        }

        private void ExportNpcLifecycleMessage(string direction, int sequence, N3Message message)
        {
            string messageName = message.N3MessageType.ToString();
            string identity = message.Identity.ToString();
            string detail = this.DescribeObject(message);
            string phase = string.Empty;
            string relatedIdentity = string.Empty;

            if (string.Equals(messageName, "CharacterAction", StringComparison.OrdinalIgnoreCase))
            {
                string action = GetMemberString(message, "Action");
                if (string.Equals(action, "99", StringComparison.OrdinalIgnoreCase)
                    || action.IndexOf("death", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    phase = "death-action";
                    relatedIdentity = GetMemberString(message, "Target");
                }
            }
            else if (string.Equals(messageName, "GenericCmd", StringComparison.OrdinalIgnoreCase)
                     && detail.IndexOf("(Corpse:", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                phase = "corpse-use";
                relatedIdentity = GetMemberString(message, "Target");
            }
            else if (string.Equals(messageName, "InventoryUpdate", StringComparison.OrdinalIgnoreCase)
                     && identity.IndexOf("(Corpse:", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                phase = "corpse-inventory";
            }
            else if (string.Equals(messageName, "ClientMoveItemToInventory", StringComparison.OrdinalIgnoreCase))
            {
                phase = "loot-move-request";
                relatedIdentity = GetMemberString(message, "SourceContainer");
            }
            else if (string.Equals(messageName, "ContainerAddItem", StringComparison.OrdinalIgnoreCase))
            {
                phase = "loot-move-result";
                relatedIdentity = GetMemberString(message, "Source");
            }
            else if (string.Equals(messageName, "Despawn", StringComparison.OrdinalIgnoreCase)
                     && (identity.IndexOf("(Corpse:", StringComparison.OrdinalIgnoreCase) >= 0
                         || this.focusedEnemyIdentities.Contains(identity)))
            {
                phase = identity.IndexOf("(Corpse:", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "corpse-despawn"
                    : "enemy-despawn";
            }

            if (string.IsNullOrEmpty(phase))
            {
                return;
            }

            this.LogNpcLifecycleRow(
                direction,
                sequence,
                phase,
                messageName,
                identity,
                relatedIdentity,
                this.ResolveDynelName(
                    unchecked((uint)(int)message.Identity.Type),
                    unchecked((uint)message.Identity.Instance)),
                detail);
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

        private void ExportVendorFullUpdate(string direction, int sequence, N3Message message)
        {
            GameTuple<Stat, int>[] stats = GetMemberValue(message, "Stats") as GameTuple<Stat, int>[]
                ?? new GameTuple<Stat, int>[0];
            int template = GetStatValue(stats, (Stat)23);
            int mesh = GetStatValue(stats, (Stat)12);
            int buyModifier = GetStatValue(stats, (Stat)426);
            int sellModifier = GetStatValue(stats, (Stat)427);
            object position = GetMemberValue(message, "Position");

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
                        Csv(GetMemberString(message, "OwnerType")),
                        Csv(GetMemberString(message, "OwnerInstance")),
                        Csv(GetMemberString(message, "PlayfieldId")),
                        Csv(MemberComponent(position, "X")),
                        Csv(MemberComponent(position, "Y")),
                        Csv(MemberComponent(position, "Z")),
                        Csv(GetMemberString(message, "Unknown7")),
                        template.ToString(CultureInfo.InvariantCulture),
                        mesh.ToString(CultureInfo.InvariantCulture),
                        buyModifier.ToString(CultureInfo.InvariantCulture),
                        sellModifier.ToString(CultureInfo.InvariantCulture),
                        stats.Length.ToString(CultureInfo.InvariantCulture)));
                this.vendorFullUpdatesLog.Flush();
            }
        }

        private void ExportInventoryUpdate(string direction, int sequence, N3Message message)
        {
            object itemsValue = GetMemberValue(message, "Items");
            IEnumerable enumerableItems = itemsValue as IEnumerable;
            if (itemsValue != null && enumerableItems == null)
            {
                throw new InvalidDataException("InventoryUpdate.Items was not enumerable.");
            }

            List<object> items = new List<object>();
            if (enumerableItems != null)
            {
                foreach (object item in enumerableItems)
                {
                    items.Add(item);
                }
            }

            lock (this.syncRoot)
            {
                this.inventoryUpdateMessageCount++;
                string capturedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                string inventoryIdentity = GetMemberString(message, "InventoryIdentity");
                bool isCorpseInventory = inventoryIdentity.IndexOf("Corpse:", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isCorpseInventory)
                {
                    this.corpseInventoryUpdateCount++;
                    string normalizedCorpseIdentity = NormalizeIdentityKey(inventoryIdentity);
                    int priorSnapshotCount;
                    this.corpseInventorySnapshotCounts.TryGetValue(normalizedCorpseIdentity, out priorSnapshotCount);
                    int openOrdinal = priorSnapshotCount + 1;
                    bool initialSnapshot = openOrdinal == 1;
                    this.corpseInventorySnapshotCounts[normalizedCorpseIdentity] = openOrdinal;
                    if (initialSnapshot)
                    {
                        this.corpseLootInitialSnapshotCount++;
                    }

                    CorpseLifecycleEvidence corpse;
                    this.activeCorpseEvidenceByCorpse.TryGetValue(
                        normalizedCorpseIdentity,
                        out corpse);
                    EnemyEntityState enemy = corpse == null
                        ? null
                        : this.enemyStates.Values.FirstOrDefault(
                            value => string.Equals(
                                NormalizeIdentityKey(value.EntityId),
                                NormalizeIdentityKey(corpse.DeadNpcIdentity),
                                StringComparison.OrdinalIgnoreCase));
                    if (corpse == null)
                    {
                        this.corpseLootUnlinkedSnapshotCount++;
                    }
                    else if (initialSnapshot)
                    {
                        this.corpseLootInitialEnemyKeys.Add(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "{0}|{1}",
                                corpse.CorpseMonsterData,
                                enemy == null ? string.Empty : enemy.Name));
                    }

                    LocalPlayer localPlayer = DynelManager.LocalPlayer;
                    string localPlayerIdentity = localPlayer == null
                        ? string.Empty
                        : Safe(() => localPlayer.Identity.ToString());
                    string localPlayerLevel = localPlayer == null
                        ? string.Empty
                        : SafeStat(localPlayer, Stat.Level);
                    int parsedLocalPlayerLevel;
                    if (initialSnapshot
                        && (string.IsNullOrWhiteSpace(localPlayerIdentity)
                            || !int.TryParse(
                                localPlayerLevel,
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out parsedLocalPlayerLevel)
                            || parsedLocalPlayerLevel <= 0
                            || (corpse == null && string.IsNullOrWhiteSpace(this.lastPlayfieldId))))
                    {
                        this.corpseLootMissingPlayerContextCount++;
                    }

                    string itemSummary = string.Join(
                        ";",
                        items.Select(
                            item => string.Format(
                                CultureInfo.InvariantCulture,
                                "{0}:{1}:{2}:{3}",
                                GetMemberString(item, "ItemLowId"),
                                GetMemberString(item, "ItemHighId"),
                                GetMemberString(item, "Quality"),
                                GetMemberString(item, "Count"))).ToArray());
                    this.corpseLootObservationRowCount++;
                    this.corpseLootObservationsLog.WriteLine(
                        string.Join(
                            ",",
                            Csv(capturedUtc),
                            Csv(direction),
                            sequence.ToString(CultureInfo.InvariantCulture),
                            Csv(inventoryIdentity),
                            openOrdinal.ToString(CultureInfo.InvariantCulture),
                            initialSnapshot ? "true" : "false",
                            items.Count.ToString(CultureInfo.InvariantCulture),
                            Csv(corpse == null ? string.Empty : corpse.DeadNpcIdentity),
                            Csv(enemy == null ? string.Empty : enemy.Name),
                            Csv(corpse == null ? string.Empty : corpse.CorpseMonsterData.ToString(CultureInfo.InvariantCulture)),
                            Csv(enemy == null || !enemy.Level.HasValue
                                ? string.Empty
                                : enemy.Level.Value.ToString(CultureInfo.InvariantCulture)),
                            Csv(corpse == null ? string.Empty : corpse.CorpseCredits.ToString(CultureInfo.InvariantCulture)),
                            Csv(localPlayerIdentity),
                            Csv(localPlayerLevel),
                            Csv(corpse == null
                                ? this.lastPlayfieldId
                                : corpse.PlayfieldId.ToString(CultureInfo.InvariantCulture)),
                            Csv(itemSummary),
                            Csv(corpse == null ? "unlinked" : "linked")));
                    this.corpseLootObservationsLog.Flush();
                }

                for (int i = 0; i < items.Count; i++)
                {
                    object item = items[i];
                    this.inventoryUpdatesLog.WriteLine(
                        string.Join(
                            ",",
                            Csv(capturedUtc),
                            Csv(direction),
                            sequence.ToString(CultureInfo.InvariantCulture),
                            Csv(inventoryIdentity),
                            Csv(GetMemberString(message, "Handle")),
                            i.ToString(CultureInfo.InvariantCulture),
                            Csv(GetMemberString(item, "Placement")),
                            Csv(GetMemberString(item, "Flags")),
                            Csv(GetMemberString(item, "Count")),
                            Csv(GetMemberString(item, "Identity")),
                            Csv(GetMemberString(item, "ItemLowId")),
                            Csv(GetMemberString(item, "ItemHighId")),
                            Csv(GetMemberString(item, "Quality")),
                            Csv(GetMemberString(item, "Unknown"))));
                    this.inventoryUpdateRowCount++;
                }

                this.inventoryUpdatesLog.Flush();
            }
        }

        private void ExportEnemyN3Evidence(string direction, int sequence, N3Message message)
        {
            SimpleCharFullUpdateMessage simpleCharFullUpdate = message as SimpleCharFullUpdateMessage;
            if (simpleCharFullUpdate != null)
            {
                // Raw packet handling owns SCFU export so it cannot be lost when the
                // AOSharp decoded-message callback is absent.
                return;
            }

            StatMessage stat = message as StatMessage;
            if (stat != null)
            {
                this.ExportEnemyStatUpdates(direction, sequence, message, message.Identity, GetMemberValue(stat, "Stats"), GetMemberValue(stat, "Position"));
                return;
            }

            if (string.Equals(
                message.N3MessageType.ToString(),
                "SimpleItemFullUpdate",
                StringComparison.OrdinalIgnoreCase))
            {
                this.ExportEnemyStatUpdates(
                    direction,
                    sequence,
                    message,
                    message.Identity,
                    GetMemberValue(message, "Stats"),
                    GetMemberValue(message, "Position"));
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

            if (string.Equals(message.N3MessageType.ToString(), "FollowTarget", StringComparison.OrdinalIgnoreCase))
            {
                this.ExportEnemyMovement(direction, sequence, message, message.Identity, "follow-target", null, null);
                return;
            }

            DespawnMessage despawn = message as DespawnMessage;
            if (despawn != null)
            {
                this.ExportEnemyMovement(direction, sequence, message, message.Identity, "despawn", null, null);
                this.ExportEnemyCombat(direction, sequence, message, null, null, null);
                return;
            }

            if (IsEnemyCombatEvidenceMessage(message))
            {
                object target = GetMemberValue(message, "Target")
                    ?? GetMemberValue(message, "Defender")
                    ?? GetMemberValue(message, "Victim")
                    ?? GetMemberValue(message, "Unknown4");
                object aux1 = GetMemberValue(message, "Attacker")
                    ?? GetMemberValue(message, "Source")
                    ?? GetMemberValue(message, "Caster")
                    ?? GetMemberValue(message, "Unknown3");
                object aux2 = GetMemberValue(message, "Weapon")
                    ?? GetMemberValue(message, "Nano")
                    ?? GetMemberValue(message, "Unknown4");

                this.ExportEnemyCombat(direction, sequence, message, target, aux1, aux2);
            }
        }

        private void CacheEnemyFullUpdate(string direction, int sequence, N3Message message)
        {
            SimpleCharFullUpdateMessage simpleCharFullUpdate = message as SimpleCharFullUpdateMessage;
            if (simpleCharFullUpdate == null || !this.IsNpcCharacterInfo(GetMemberValue(simpleCharFullUpdate, "CharacterInfo")))
            {
                return;
            }

            lock (this.syncRoot)
            {
                this.recentEnemyFullUpdates[simpleCharFullUpdate.Identity.ToString()] =
                    new RecentEnemyFullUpdateEvidence
                    {
                        Direction = direction,
                        Sequence = sequence,
                        Message = simpleCharFullUpdate
                    };
            }
        }

        private bool ShouldCaptureEnemyFightEvidence(string direction, int sequence, N3Message message)
        {
            if (message == null)
            {
                return false;
            }

            bool isCombatEvidence = IsEnemyCombatEvidenceMessage(message);
            if (isCombatEvidence)
            {
                // This flag describes observed evidence. Manual/auto modes only
                // control the focused human-readable fight log; they never gate
                // structured combat collection.
                this.enemyFightCaptureStarted = true;
            }

            if (this.enemyFightCaptureEnabled)
            {
                return true;
            }

            if (!this.enemyFightAutoCaptureEnabled)
            {
                return false;
            }

            bool registered = this.TryRegisterFocusedEnemyFromMessage(direction, sequence, message);
            if (isCombatEvidence)
            {
                return true;
            }

            SimpleCharFullUpdateMessage simpleCharFullUpdate = message as SimpleCharFullUpdateMessage;
            return registered
                || this.MessageTouchesFocusedEnemy(message)
                || (simpleCharFullUpdate != null && this.IsEnemySimpleCharUpdate(simpleCharFullUpdate))
                || this.IsTrackableEnemyIdentity(message.Identity);
        }

        private static bool IsEnemyCombatEvidenceMessage(N3Message message)
        {
            if (message == null)
            {
                return false;
            }

            string messageName = message.N3MessageType.ToString();
            return string.Equals(messageName, "Attack", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "AttackInfo", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "SpecialAttackWeapon", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "SpecialAttackInfo", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "CharSecSpecAttack", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "MissedAttackInfo", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "CastNanoSpell", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "CharacterAction", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "HealthDamage", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "Buff", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "Reload", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "StopFight", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryRegisterFocusedEnemyFromMessage(string direction, int sequence, N3Message message)
        {
            bool registered = false;
            Identity source = message.Identity;
            object targetObject = GetMemberValue(message, "Target")
                ?? GetMemberValue(message, "Defender")
                ?? GetMemberValue(message, "Unknown4");
            Identity target;
            if (TryGetIdentity(targetObject, out target))
            {
                if (this.IsLocalPlayerIdentity(source))
                {
                    registered |= this.RegisterFocusedEnemyIdentity(target, "local-player-target", direction, sequence);
                }

                if (this.IsLocalPlayerIdentity(target))
                {
                    registered |= this.RegisterFocusedEnemyIdentity(source, "local-player-targeted", direction, sequence);
                }
            }

            Identity fightingTarget;
            if (TryGetIdentity(GetMemberValue(message, "FightingTarget"), out fightingTarget)
                && this.IsLocalPlayerIdentity(fightingTarget))
            {
                registered |= this.RegisterFocusedEnemyIdentity(source, "fighting-local-player", direction, sequence);
            }

            return registered;
        }

        private bool RegisterFocusedEnemyIdentity(Identity identity, string reason, string direction, int sequence)
        {
            if (!this.IsSimpleNonLocalCharacterIdentity(identity))
            {
                return false;
            }

            string identityText = identity.ToString();
            bool added;
            lock (this.syncRoot)
            {
                added = this.focusedEnemyIdentities.Add(identityText);
                this.enemyFightCaptureStarted = true;
                this.localEnemyCombatContextUntilUtc = DateTime.UtcNow.AddSeconds(LocalEnemyCombatContextSeconds);
                if (added)
                {
                    DateTime timestamp = DateTime.UtcNow;
                    bool created;
                    EnemyEntityState state = this.GetOrCreateEnemyState(identity, timestamp, out created);
                    this.RecordEnemyStateEvent(state, timestamp, "focus", direction, sequence, "Focus", reason);
                }
            }

            if (added)
            {
                this.LogEvent(
                    "ENEMY-FIGHT-AUTO",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "focused identity={0} reason={1} direction={2} sequence={3}",
                        identityText,
                        reason,
                        direction,
                        sequence));

                Dynel dynel = DynelManager.GetDynel(identity);
                if (dynel != null)
                {
                    this.TrackEnemyFromDynel(dynel, "focus");
                }

                this.ReplayFocusedEnemyFullUpdate(identity);
            }

            return true;
        }

        private void ReplayFocusedEnemyFullUpdate(Identity identity)
        {
            RecentEnemyFullUpdateEvidence evidence;
            lock (this.syncRoot)
            {
                if (!this.recentEnemyFullUpdates.TryGetValue(identity.ToString(), out evidence))
                {
                    return;
                }
            }

            this.TrackEnemyFromSimpleCharFullUpdate(evidence.Direction, evidence.Sequence, evidence.Message);
        }

        private bool MessageTouchesFocusedEnemy(N3Message message)
        {
            return this.IsFocusedEnemyIdentity(message.Identity)
                || this.IsFocusedEnemyIdentityObject(GetMemberValue(message, "Target"))
                || this.IsFocusedEnemyIdentityObject(GetMemberValue(message, "Defender"))
                || this.IsFocusedEnemyIdentityObject(GetMemberValue(message, "Unknown3"))
                || this.IsFocusedEnemyIdentityObject(GetMemberValue(message, "Unknown4"))
                || this.IsFocusedEnemyIdentityObject(GetMemberValue(message, "FightingTarget"));
        }

        private void LogEnemyFightEvent(string direction, int sequence, N3Message message)
        {
            lock (this.syncRoot)
            {
                this.enemyFightEventsLog.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:o} {1} #{2} type={3} identity={4} {5}",
                        DateTime.UtcNow,
                        direction,
                        sequence,
                        message.N3MessageType,
                        message.Identity,
                        OneLine(this.DescribeObject(message))));
                this.enemyFightEventsLog.Flush();
            }
        }

        private void ExportEnemyFullUpdate(
            DateTime capturedUtc,
            string direction,
            int sequence,
            RawSimpleCharFullUpdate message)
        {
            if (message == null || message.Npc == null)
            {
                return;
            }

            string fightingTargetRole;
            string fightingTargetIdentity;
            object fightingTarget = message.FightingTarget.HasValue
                                        ? (object)ToAoIdentity(message.FightingTarget.Value)
                                        : null;
            this.DescribeIdentityForEnemyOutput(
                fightingTarget,
                out fightingTargetRole,
                out fightingTargetIdentity);

            lock (this.syncRoot)
            {
                this.enemyFullUpdatesLog.WriteLine(
                    string.Join(
                        ",",
                        Csv(capturedUtc.ToString("o", CultureInfo.InvariantCulture)),
                        Csv(direction),
                        sequence.ToString(CultureInfo.InvariantCulture),
                        Csv(message.Identity.ToString()),
                        Csv(message.Name),
                        Csv(message.PlayfieldId.HasValue ? message.PlayfieldId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty),
                        message.Position.X.ToString("R", CultureInfo.InvariantCulture),
                        message.Position.Y.ToString("R", CultureInfo.InvariantCulture),
                        message.Position.Z.ToString("R", CultureInfo.InvariantCulture),
                        message.Heading.X.ToString("R", CultureInfo.InvariantCulture),
                        message.Heading.Y.ToString("R", CultureInfo.InvariantCulture),
                        message.Heading.Z.ToString("R", CultureInfo.InvariantCulture),
                        message.Heading.W.ToString("R", CultureInfo.InvariantCulture),
                        Csv(fightingTargetRole),
                        Csv(fightingTargetIdentity),
                        Csv(message.Version.ToString(CultureInfo.InvariantCulture)),
                        Csv(message.FlagsText),
                        Csv(message.CharacterFlags.ToString(CultureInfo.InvariantCulture)),
                        Csv(message.AccountFlags.ToString(CultureInfo.InvariantCulture)),
                        Csv(message.Expansions.ToString(CultureInfo.InvariantCulture)),
                        Csv("NPCInfo"),
                        Csv(message.Npc.Family.ToString(CultureInfo.InvariantCulture)),
                        Csv(message.Npc.LosHeight.ToString(CultureInfo.InvariantCulture)),
                        Csv(message.Npc.UnknownData.ToString(CultureInfo.InvariantCulture)),
                        Csv(message.Npc.UnknownData2.ToString(CultureInfo.InvariantCulture)),
                        Csv(message.Npc.UnknownData3.HasValue ? message.Npc.UnknownData3.Value.ToString(CultureInfo.InvariantCulture) : string.Empty),
                        Csv(message.Level.ToString(CultureInfo.InvariantCulture)),
                        Csv(message.Health.ToString(CultureInfo.InvariantCulture)),
                        Csv(message.HealthDamage.ToString(CultureInfo.InvariantCulture)),
                        Csv(message.MonsterData.ToString(CultureInfo.InvariantCulture)),
                        Csv(message.MonsterScale.ToString(CultureInfo.InvariantCulture)),
                        Csv(message.VisualFlags.ToString(CultureInfo.InvariantCulture)),
                        Csv(message.VisibleTitle.ToString(CultureInfo.InvariantCulture)),
                        Csv((message.Unknown1 == null ? 0 : message.Unknown1.Length).ToString(CultureInfo.InvariantCulture)),
                        Csv(RawScfuFormatting.ToHex(message.Unknown1)),
                        Csv(message.HeadMesh.HasValue ? message.HeadMesh.Value.ToString(CultureInfo.InvariantCulture) : string.Empty),
                        Csv(message.RunSpeedBase.ToString(CultureInfo.InvariantCulture)),
                        Csv((message.ActiveNanos == null ? 0 : message.ActiveNanos.Length).ToString(CultureInfo.InvariantCulture)),
                        Csv(RawScfuFormatting.FormatActiveNanos(message.ActiveNanos)),
                        Csv((message.Waypoints == null ? 0 : message.Waypoints.Length).ToString(CultureInfo.InvariantCulture)),
                        Csv(message.WaypointOwner.HasValue ? message.WaypointOwner.Value.ToString() : string.Empty),
                        Csv(RawScfuFormatting.FormatWaypoints(message.Waypoints)),
                        Csv((message.TextureOverrides == null ? 0 : message.TextureOverrides.Length).ToString(CultureInfo.InvariantCulture)),
                        Csv(RawScfuFormatting.FormatTextureOverrides(message.TextureOverrides)),
                        Csv((message.Textures == null ? 0 : message.Textures.Length).ToString(CultureInfo.InvariantCulture)),
                        Csv(RawScfuFormatting.FormatTextures(message.Textures)),
                        Csv((message.Meshes == null ? 0 : message.Meshes.Length).ToString(CultureInfo.InvariantCulture)),
                        Csv(RawScfuFormatting.FormatMeshes(message.Meshes)),
                        Csv(message.Flags2Text),
                        Csv(message.Unknown2.ToString(CultureInfo.InvariantCulture)),
                        Csv(message.Unknown4.HasValue ? message.Unknown4.Value.ToString(CultureInfo.InvariantCulture) : string.Empty),
                        Csv(message.DecodeFullyConsumed ? "true" : "false"),
                        Csv(RawScfuFormatting.ToHex(message.UndecodedTail)),
                        Csv(RawScfuFormatting.ToHex(message.RawBody)),
                        Csv("raw-scfu flags=" + message.FlagsText + " flags2=" + message.Flags2Text)));
                this.enemyFullUpdatesLog.Flush();
                this.enemyFullUpdateRowCount++;
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
            IEnumerable stats = statsObject as IEnumerable;
            string capturedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            string statsCount = GetCountString(statsObject);
            string detail = this.DescribeObject(message);

            if (stats == null)
            {
                lock (this.syncRoot)
                {
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
                            Csv(string.Empty),
                            Csv(string.Empty),
                            Csv(string.Empty),
                            MemberComponent(position, "X"),
                            MemberComponent(position, "Y"),
                            MemberComponent(position, "Z"),
                            Csv("0"),
                            Csv(detail)));
                    this.enemyStatUpdatesLog.Flush();
                }

                return;
            }

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

            if (string.Equals(
                message.N3MessageType.ToString(),
                "SimpleItemFullUpdate",
                StringComparison.OrdinalIgnoreCase))
            {
                this.TrackEnemyFromSimpleItemFullUpdate(direction, sequence, message);
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
                    this.RecordEnemyStateEvent(state, timestamp, "spawn", direction, sequence, message.N3MessageType.ToString(), "CharacterAction");
                }

                this.RecordEnemyDeath(state, timestamp, direction, sequence, message.N3MessageType.ToString(), "CharacterAction");
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
                this.UpdateEnemyStaticStateFromSimpleCharFullUpdate(state, message);
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

                this.RecordEnemyStateEvent(
                    state,
                    timestamp,
                    state.DeathLogged && state.CurrentHealth.HasValue && state.CurrentHealth.Value > 0 ? "respawn" : created ? "spawn" : "update",
                    direction,
                    sequence,
                    message.N3MessageType.ToString(),
                    "SimpleCharFullUpdate");
                if (state.CurrentHealth.HasValue && state.CurrentHealth.Value > 0)
                {
                    state.DeathLogged = false;
                }

                this.RecordEnemyDeathIfNeeded(state, timestamp, direction, sequence, message.N3MessageType.ToString(), "SimpleCharFullUpdate");
            }
        }

        private void UpdateEnemyStaticStateFromSimpleCharFullUpdate(EnemyEntityState state, SimpleCharFullUpdateMessage message)
        {
            state.Name = PreferEnemyStateString(state.Name, GetMemberString(message, "Name"));
            state.MonsterData = PreferEnemyStateString(state.MonsterData, GetMemberString(message, "MonsterData"));
            state.MonsterScale = PreferEnemyStateString(state.MonsterScale, GetMemberString(message, "MonsterScale"));
            state.VisualFlags = PreferEnemyStateString(state.VisualFlags, GetMemberString(message, "VisualFlags"));
            state.HeadMesh = PreferEnemyStateString(state.HeadMesh, GetMemberString(message, "HeadMesh"));
            state.RunSpeed = PreferEnemyStateString(state.RunSpeed, GetMemberString(message, "RunSpeedBase"));
            object characterInfo = GetMemberValue(message, "CharacterInfo");
            state.NpcFamily = PreferEnemyStateString(state.NpcFamily, GetMemberString(characterInfo, "Family"));
            state.LosHeight = PreferEnemyStateString(state.LosHeight, GetMemberString(characterInfo, "LosHeight"));
        }

        private void UpdateEnemyStaticStateFromCharacter(EnemyEntityState state, SimpleChar character)
        {
            state.Name = PreferEnemyStateString(state.Name, Safe(() => character.Name));
            state.MonsterData = PreferEnemyStateString(state.MonsterData, SafeStat(character, Stat.MonsterData));
            state.CatMesh = PreferEnemyStateString(state.CatMesh, SafeStat(character, Stat.CATMesh));
            state.VisualFlags = PreferEnemyStateString(state.VisualFlags, SafeStat(character, Stat.VisualFlags));
            state.RunSpeed = PreferEnemyStateString(state.RunSpeed, SafeStat(character, Stat.RunSpeed));
            state.MinDamage = PreferEnemyStateString(state.MinDamage, SafeStat(character, Stat.MinDamage));
            state.MaxDamage = PreferEnemyStateString(state.MaxDamage, SafeStat(character, Stat.MaxDamage));
            state.DefaultAttackType = PreferEnemyStateString(state.DefaultAttackType, SafeStat(character, Stat.DefaultAttackType));
            state.AttackDelay = PreferEnemyStateString(state.AttackDelay, SafeStat(character, Stat.AttackDelay));
            state.RechargeDelay = PreferEnemyStateString(state.RechargeDelay, SafeStat(character, Stat.RechargeDelay));
        }

        private static string PreferEnemyStateString(string current, string value)
        {
            return string.IsNullOrEmpty(value) ? current : value;
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
                    this.RecordEnemyStateEvent(state, timestamp, created ? "spawn" : "update", direction, sequence, message.N3MessageType.ToString(), "Stat");
                    this.RecordEnemyDeathIfNeeded(state, timestamp, direction, sequence, message.N3MessageType.ToString(), "Stat");
                }
            }
        }

        private void TrackEnemyFromSimpleItemFullUpdate(string direction, int sequence, N3Message message)
        {
            if (!this.IsTrackableEnemyIdentity(message.Identity))
            {
                return;
            }

            GameTuple<Stat, int>[] stats = GetMemberValue(message, "Stats") as GameTuple<Stat, int>[]
                ?? new GameTuple<Stat, int>[0];
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
                foreach (GameTuple<Stat, int> stat in stats)
                {
                    changed |= this.ApplyEnemyStat(state, stat.Value1, stat.Value2);
                }

                if (changed)
                {
                    this.RecordEnemyStateEvent(state, timestamp, created ? "spawn" : "update", direction, sequence, message.N3MessageType.ToString(), "SimpleItemFullUpdate");
                    this.RecordEnemyDeathIfNeeded(state, timestamp, direction, sequence, message.N3MessageType.ToString(), "SimpleItemFullUpdate");
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
                    this.RecordEnemyStateEvent(state, timestamp, "spawn", direction, sequence, message.N3MessageType.ToString(), "HealthDamage");
                }

                this.RecordEnemyStateEvent(state, timestamp, "damage", direction, sequence, message.N3MessageType.ToString(), "HealthDamage");
                this.RecordEnemyDeathIfNeeded(state, timestamp, direction, sequence, message.N3MessageType.ToString(), "HealthDamage");
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
                    this.RecordEnemyStateEvent(state, timestamp, "spawn", direction, sequence, "CombatTarget", "CombatTarget");
                }

                this.RecordEnemyStateEvent(state, timestamp, eventType, direction, sequence, "CombatTarget", "CombatTarget");
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
                    this.RecordEnemyStateEvent(state, timestamp, created ? "spawn" : eventType, direction, sequence, "Movement", "Position");
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
                    this.RecordEnemyStateEvent(state, timestamp, "spawn", direction, sequence, "Despawn", "Despawn");
                }

                this.RecordEnemyStateEvent(state, timestamp, "despawn", direction, sequence, "Despawn", "Despawn");
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
                this.TrackEnemyFromCharacter(dynel.Cast<SimpleChar>(), requestedEventType, "DYNEL-SPAWNED");
            }
            catch
            {
                // Dynel snapshots are best-effort; decoded packets remain the capture source of truth.
            }
        }

        private void TrackEnemyFromCharacter(SimpleChar character, string requestedEventType, string evidenceSource)
        {
            if (!this.IsEnemyCharacter(character))
            {
                return;
            }

            bool isCombatOrFocused = this.enemyFightCaptureEnabled || this.IsFocusedEnemyIdentity(character.Identity);
            bool isPopulationEvidence = !isCombatOrFocused && this.IsBroadVisibleEnemyEvidence(character);
            if (!isCombatOrFocused && !isPopulationEvidence)
            {
                return;
            }

            DateTime timestamp = DateTime.UtcNow;
            lock (this.syncRoot)
            {
                bool created;
                EnemyEntityState state = this.GetOrCreateEnemyState(character.Identity, timestamp, out created);
                this.UpdateEnemyStaticStateFromCharacter(state, character);
                if (isPopulationEvidence)
                {
                    this.MarkEnemyPopulationEvidence(state, evidenceSource);
                }

                state.Level = TryGetCharacterStat(character, Stat.Level);
                state.CurrentHealth = TryGetCharacterStat(character, Stat.Health);
                state.MaxHealth = TryGetCharacterStat(character, Stat.MaxHealth);
                this.enemyHealthUpdateCount++;
                if (this.UpdateEnemyPosition(state, character.Position))
                {
                    this.enemyPositionUpdateCount++;
                }

                string eventType = created
                    ? (isPopulationEvidence ? "population" : "spawn")
                    : (requestedEventType == "spawn" ? (isPopulationEvidence ? "population-update" : "update") : requestedEventType);
                if (state.DeathLogged && state.CurrentHealth.HasValue && state.CurrentHealth.Value > 0)
                {
                    eventType = "respawn";
                    state.DeathLogged = false;
                }

                this.RecordEnemyStateEvent(state, timestamp, eventType, "LOCAL", 0, "DynelSnapshot", evidenceSource);
                this.RecordEnemyDeathIfNeeded(state, timestamp, "LOCAL", 0, "DynelSnapshot", evidenceSource);
            }
        }

        private void TrackEnemyGone(string entityId)
        {
            if (!this.enemyFightCaptureEnabled && !this.IsFocusedEnemyIdentityText(entityId) && !this.IsTrackedEnemyState(entityId))
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

                this.RecordEnemyStateEvent(state, timestamp, "despawn", "LOCAL", 0, "DynelSnapshot", "CHAR-GONE");
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

        private void RecordEnemyDeathIfNeeded(
            EnemyEntityState state,
            DateTime timestamp,
            string direction,
            int sequence,
            string messageType,
            string evidenceSource)
        {
            if (!state.CurrentHealth.HasValue || state.CurrentHealth.Value > 0 || state.DeathLogged)
            {
                return;
            }

            this.RecordEnemyDeath(state, timestamp, direction, sequence, messageType, evidenceSource);
        }

        private void RecordEnemyDeath(
            EnemyEntityState state,
            DateTime timestamp,
            string direction,
            int sequence,
            string messageType,
            string evidenceSource)
        {
            if (state.DeathLogged)
            {
                return;
            }

            state.DeathLogged = true;
            this.RecordEnemyStateEvent(state, timestamp, "death", direction, sequence, messageType, evidenceSource);
        }

        private void RecordEnemyStateEvent(
            EnemyEntityState state,
            DateTime timestamp,
            string eventType,
            string direction,
            int sequence,
            string messageType,
            string evidenceSource)
        {
            state.LastUpdateUtc = timestamp;
            EnemyStateEvent stateEvent = new EnemyStateEvent
            {
                TimestampUtc = timestamp,
                Direction = direction ?? string.Empty,
                Sequence = sequence,
                MessageType = messageType ?? string.Empty,
                EvidenceSource = evidenceSource ?? string.Empty,
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
            if (eventType == "spawn" || eventType == "respawn")
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
                        Csv(direction ?? string.Empty),
                        sequence.ToString(CultureInfo.InvariantCulture),
                        Csv(messageType ?? string.Empty),
                        Csv(evidenceSource ?? string.Empty),
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
            if (!this.IsSimpleNonLocalCharacterIdentity(message.Identity))
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
            if (!this.IsSimpleNonLocalCharacterIdentity(identity))
            {
                return false;
            }

            return this.IsFocusedEnemyIdentity(identity)
                || this.IsTrackedEnemyState(identity.ToString())
                || this.IsVisibleEnemyIdentity(identity);
        }

        private bool IsSimpleNonLocalCharacterIdentity(Identity identity)
        {
            return identity.Type == IdentityType.SimpleChar && !this.IsLocalPlayerIdentity(identity);
        }

        private bool IsFocusedEnemyIdentity(Identity identity)
        {
            if (!this.IsSimpleNonLocalCharacterIdentity(identity))
            {
                return false;
            }

            lock (this.syncRoot)
            {
                return this.focusedEnemyIdentities.Contains(identity.ToString());
            }
        }

        private bool IsFocusedEnemyIdentityObject(object identityValue)
        {
            Identity identity;
            return TryGetIdentity(identityValue, out identity) && this.IsFocusedEnemyIdentity(identity);
        }

        private bool IsFocusedEnemyIdentityText(string identityText)
        {
            if (string.IsNullOrEmpty(identityText))
            {
                return false;
            }

            lock (this.syncRoot)
            {
                return this.focusedEnemyIdentities.Contains(identityText);
            }
        }

        private bool IsTrackedEnemyState(string identityText)
        {
            if (string.IsNullOrEmpty(identityText))
            {
                return false;
            }

            lock (this.syncRoot)
            {
                return this.enemyStates.ContainsKey(identityText);
            }
        }

        private bool IsVisibleEnemyIdentity(Identity identity)
        {
            try
            {
                Dynel dynel = DynelManager.GetDynel(identity);
                if (dynel == null)
                {
                    return false;
                }

                return this.IsEnemyCharacter(dynel.Cast<SimpleChar>());
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetIdentity(object identityValue, out Identity identity)
        {
            if (identityValue is Identity)
            {
                identity = (Identity)identityValue;
                return true;
            }

            identity = default(Identity);
            return false;
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
                if (this.IsLocalPlayerPet(character))
                {
                    return false;
                }

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
            if (character == null
                || this.IsLocalPlayerIdentity(character.Identity)
                || this.IsLocalPlayerPet(character))
            {
                return false;
            }

            return SafeBool(() => character.IsNpc) || SafeBool(() => character.IsPet);
        }

        private bool IsLocalPlayerPet(SimpleChar character)
        {
            if (character == null || !SafeBool(() => character.IsPet) || DynelManager.LocalPlayer == null)
            {
                return false;
            }

            return Safe(() => character.PetOwnerId.ToString(CultureInfo.InvariantCulture))
                   == Safe(
                       () => DynelManager.LocalPlayer.Identity.Instance.ToString(
                           CultureInfo.InvariantCulture));
        }

        private bool IsBroadVisibleEnemyEvidence(SimpleChar character)
        {
            if (!this.IsEnemyCharacter(character) || SafeBool(() => character.IsPlayer))
            {
                return false;
            }

            return true;
        }

        private bool IsDungeonPopulationCaptureContext()
        {
            int runtimePlayfieldId;
            if (int.TryParse(this.lastPlayfieldId, NumberStyles.Integer, CultureInfo.InvariantCulture, out runtimePlayfieldId)
                && runtimePlayfieldId >= 1000000)
            {
                return true;
            }

            return string.Equals(this.GetDetectedResourcePlayfieldId(), "127", StringComparison.Ordinal);
        }

        private void MarkEnemyPopulationEvidence(EnemyEntityState state, string evidenceSource)
        {
            state.PopulationEvidenceObserved = true;
            state.PopulationEvidenceSource = PreferEnemyStateString(state.PopulationEvidenceSource, evidenceSource);
            state.ResourcePlayfieldId = PreferEnemyStateString(state.ResourcePlayfieldId, this.GetDetectedResourcePlayfieldId());
            state.RuntimePlayfieldId = PreferEnemyStateString(state.RuntimePlayfieldId, this.lastPlayfieldId);
            state.CapturePlayfieldIdentity = PreferEnemyStateString(state.CapturePlayfieldIdentity, this.GetCapturePlayfieldIdentity());
            state.CapturePlayfieldObjectId = PreferEnemyStateString(state.CapturePlayfieldObjectId, this.GetCapturePlayfieldObjectId());
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

            DateTime finalizedUtc = DateTime.UtcNow;
            if (!this.captureStopRequestedUtc.HasValue)
            {
                this.captureStopRequestedUtc = finalizedUtc;
            }

            // Teardown means the client/plugin is already leaving; flush everything
            // captured so far immediately. Normal /aocap stop uses the quiet drain.
            if (!this.CloseRawCaptureBoundaryAndWait(TimeSpan.FromSeconds(5)))
            {
                this.rawPacketCallbackDrainTimeoutCount++;
                this.LogEvent(
                    "RAW-PACKET-DRAIN-TIMEOUT",
                    "A registered raw packet callback did not finish before teardown finalization.");
            }

            this.CompleteCaptureStop(finalizedUtc, false, false);
        }

        private bool CloseRawCaptureBoundaryAndWait(TimeSpan timeout)
        {
            lock (this.syncRoot)
            {
                Interlocked.Exchange(
                    ref this.rawCaptureGateState,
                    RawCaptureGateClosed);

                DateTime deadlineUtc = DateTime.UtcNow.Add(timeout);
                while (Volatile.Read(ref this.rawPacketCallbacksInFlight) != 0)
                {
                    TimeSpan remaining = deadlineUtc - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero
                        || !Monitor.Wait(this.syncRoot, remaining))
                    {
                        return Volatile.Read(ref this.rawPacketCallbacksInFlight) == 0;
                    }
                }

                return true;
            }
        }

        private CaptureValidation ValidateCapture()
        {
            List<string> issues = new List<string>();
            List<string> notes = new List<string>();
            CaptureCallbackBoundarySnapshot callbackHealth = this.callbackBoundary.Snapshot();
            List<EnemyRespawnObservation> respawns = this.BuildEnemyRespawnObservations(DateTime.UtcNow);
            int completeRespawns = respawns.Count(x => x.Status == "complete");
            int ambiguousRespawns = respawns.Count(x => x.Status == "ambiguous");
            int incompleteRespawns = respawns.Count(x => x.Status == "incomplete");

            if (callbackHealth.TotalErrorCount > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Capture callbacks failed {0} time(s); inspect capture-callback-errors.log and callbackHealth counters.",
                        callbackHealth.TotalErrorCount));
            }

            if (callbackHealth.ErrorLogWriteFailureCount > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Capture callback error evidence could not be durably appended {0} time(s).",
                        callbackHealth.ErrorLogWriteFailureCount));
            }

            if (this.GetSessionFileLength("events.log") <= 0)
            {
                issues.Add("events.log is empty or missing.");
            }

            if (this.decodedInboundCount + this.decodedOutboundCount > 0 && this.decodedN3EventRowCount == 0)
            {
                issues.Add("Decoded N3 messages were observed, but events.log has no decoded-message rows.");
            }

            if (this.n3CaptureStageErrorCount > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Decoded N3 capture stages failed {0} time(s); inspect N3-STAGE-ERROR rows in events.log.",
                        this.n3CaptureStageErrorCount));
            }

            if (this.rawCombatPacketCount > 0 && this.enemyCombatRowCount == 0)
            {
                issues.Add("Raw combat packets were observed, but enemy-combat.csv has no rows.");
            }

            int observedRawPackets = this.inboundPacketCount + this.outboundPacketCount;
            bool recaptureRequired = this.IsCaptureRecaptureRequired();
            bool rawSinkIncomplete = this.rawPacketLogRowCount != observedRawPackets
                                     && this.rawPacketIndexRowCount != observedRawPackets
                                     && this.rawPacketPreservedCount != observedRawPackets;
            if (rawSinkIncomplete)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Neither authoritative raw sink is complete: observed={0}, packetLogRows={1}, packetIndexRows={2}, preservedAcrossSinks={3}.",
                        observedRawPackets,
                        this.rawPacketLogRowCount,
                        this.rawPacketIndexRowCount,
                        this.rawPacketPreservedCount));
            }

            if (this.rawPacketCallbackDrainTimeoutCount > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Raw packet callback drain timed out {0} time(s); one or more pre-teardown packets may be missing.",
                        this.rawPacketCallbackDrainTimeoutCount));
            }

            if (this.rawPacketLogRowCount != observedRawPackets)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "packets.hex.log row mismatch: observed={0}, rows={1}.",
                        observedRawPackets,
                        this.rawPacketLogRowCount));
            }

            if (this.rawPacketIndexRowCount != observedRawPackets)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "raw-packets.csv row mismatch: observed={0}, rows={1}.",
                        observedRawPackets,
                        this.rawPacketIndexRowCount));
            }

            if (this.rawPacketWriteErrorCount > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Raw packet sinks reported {0} write/flush/close error(s).",
                        this.rawPacketWriteErrorCount));
            }

            if (this.rawPacketProjectionErrorCount > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Raw packet projection/finalization stages reported {0} error(s).",
                        this.rawPacketProjectionErrorCount));
            }

            if (this.rawSimpleCharFullUpdatePacketCount
                != this.rawSimpleCharFullUpdateDecodeCount + this.rawSimpleCharFullUpdateDecodeErrorCount)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Raw SCFU accounting mismatch: raw={0}, decoded={1}, errors={2}.",
                        this.rawSimpleCharFullUpdatePacketCount,
                        this.rawSimpleCharFullUpdateDecodeCount,
                        this.rawSimpleCharFullUpdateDecodeErrorCount));
            }

            if (this.scfuAppearanceRowCount != this.rawSimpleCharFullUpdatePacketCount)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Raw SCFU projection mismatch: raw={0}, scfu-appearance.csv rows={1}.",
                        this.rawSimpleCharFullUpdatePacketCount,
                        this.scfuAppearanceRowCount));
            }

            if (this.rawSimpleCharFullUpdateDecodeErrorCount > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "SCFU decode failed for {0} raw packet(s). If either raw sink preserved the packet, repair the decoder offline; the capture-level recaptureRequired flag is authoritative.",
                        this.rawSimpleCharFullUpdateDecodeErrorCount));
            }

            if (this.rawSimpleCharFullUpdateIncompleteDecodeCount > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "SCFU decoding left undecoded tails for {0} raw packet(s); offline decoding is required.",
                        this.rawSimpleCharFullUpdateIncompleteDecodeCount));
            }

            if (this.enemyFullUpdateRowCount != this.rawNpcSimpleCharFullUpdateCount)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "NPC SCFU projection mismatch: decoded NPC SCFUs={0}, enemy-full-updates.csv rows={1}.",
                        this.rawNpcSimpleCharFullUpdateCount,
                        this.enemyFullUpdateRowCount));
            }

            if (observedRawPackets == 0)
            {
                notes.Add("No inbound or outbound raw packets were observed in this session.");
            }
            else if (this.rawPacketLogRowCount == 0 && this.rawPacketIndexRowCount == observedRawPackets)
            {
                notes.Add("packets.hex.log is empty, but raw-packets.csv contains the complete raw packet stream.");
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

            if (this.enemyCombatEventCount == 0 && this.enemyCombatRowCount == 0)
            {
                notes.Add("No decoded combat evidence packets were observed.");
            }
            else if (this.focusedEnemyIdentities.Count == 0 && !this.enemyFightCaptureEnabled)
            {
                notes.Add("Combat evidence was exported without a focused local-player fight annotation.");
            }

            if (this.movementFollowTargetPacketCount > 0 && this.movementUsableFollowTargetPacketCount == 0)
            {
                issues.Add("FollowTarget packets were observed, but none decoded with usable path coordinates.");
            }
            else if (this.movementUsableFollowTargetPacketCount > 0)
            {
                notes.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "FollowTarget movement decode usable: {0}/{1} packets had source and path coordinates.",
                        this.movementUsableFollowTargetPacketCount,
                        this.movementFollowTargetPacketCount));
            }
            else
            {
                notes.Add("No FollowTarget movement packets were observed.");
            }

            if (this.movementDecodeErrorCount > 0)
            {
                notes.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Movement packet decode errors: {0}.",
                        this.movementDecodeErrorCount));
            }

            if (this.corpseFullUpdatePacketCount > 0 && this.corpseFullUpdateRowCount == 0)
            {
                issues.Add("CorpseFullUpdate packets were observed, but corpse-full-updates.csv has no decoded rows.");
            }

            if (this.corpseFullUpdateDecodeErrorCount > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "CorpseFullUpdate decode errors: {0}.",
                        this.corpseFullUpdateDecodeErrorCount));
            }

            if ((this.corpseSeenEventCount > 0 || this.corpseInventoryUpdateCount > 0)
                && this.corpseFullUpdateRowCount == 0)
            {
                issues.Add("Corpse presence or inventory was observed, but no identity-linked CorpseFullUpdate was decoded.");
            }
            else if (this.corpseFullUpdateRowCount > 0)
            {
                notes.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Corpse lifecycle decode usable: {0}/{1} CorpseFullUpdate packets produced rows.",
                        this.corpseFullUpdateRowCount,
                        this.corpseFullUpdatePacketCount));
            }

            if (this.corpseInventoryUpdateCount > 0
                && this.corpseLootObservationRowCount != this.corpseInventoryUpdateCount)
            {
                issues.Add("Corpse inventory updates were observed, but corpse-loot-observations.csv is missing container snapshots.");
            }

            if (this.corpseLootUnlinkedSnapshotCount > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Corpse loot snapshots could not be identity-linked to CorpseFullUpdate evidence: {0}.",
                        this.corpseLootUnlinkedSnapshotCount));
            }

            if (this.corpseLootMissingPlayerContextCount > 0)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Initial corpse loot snapshots are missing local-player identity, player level, or playfield context: {0}.",
                        this.corpseLootMissingPlayerContextCount));
            }

            if (this.corpseLootInitialSnapshotCount > 0)
            {
                notes.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Identity-linked initial corpse loot snapshots: {0}.",
                        this.corpseLootInitialSnapshotCount));
            }

            if (this.lootCaptureRequested && this.corpseLootInitialSnapshotCount < 10)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Ten-kill loot capture was requested, but only {0} initial corpse snapshots were recorded.",
                        this.corpseLootInitialSnapshotCount));
            }

            if (this.lootCaptureRequested && this.corpseLootInitialEnemyKeys.Count != 1)
            {
                issues.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Ten-kill loot capture must contain one enemy type; correlated enemy-type count was {0}.",
                        this.corpseLootInitialEnemyKeys.Count));
            }

            if (this.enemyDeathEventCount > 0
                && this.corpseSeenEventCount == 0
                && this.corpseFullUpdateRowCount == 0)
            {
                notes.Add("Enemy death was observed without a corpse spawn; this may be valid for the captured archetype.");
            }

            if (respawns.Count > 0)
            {
                notes.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Enemy respawn correlation rows: complete={0} ambiguous={1} incomplete={2}.",
                        completeRespawns,
                        ambiguousRespawns,
                        incompleteRespawns));
            }

            if (this.respawnCaptureRequested && this.enemyDeathEventCount == 0)
            {
                issues.Add("Respawn capture was requested by marker, but no enemy death was observed.");
            }

            if (this.respawnCaptureRequested && completeRespawns == 0)
            {
                issues.Add("Respawn capture was requested by marker, but no same-archetype same-position respawn was correlated.");
            }

            if (this.respawnCaptureRequested && ambiguousRespawns > 0)
            {
                issues.Add("Respawn capture produced ambiguous same-position candidates; inspect enemy-respawns.csv before accepting timing.");
            }

            this.pf127GeometryCapture?.AppendValidation(issues, notes);

            bool offlineDecodeRequired = !recaptureRequired && this.HasOfflineDecodeWork(observedRawPackets);
            if (offlineDecodeRequired && issues.Count == 0)
            {
                issues.Add("One or more raw-to-derived decoders or projections require offline repair.");
            }

            bool processingAllowed = issues.Count == 0
                                     && !recaptureRequired
                                     && !offlineDecodeRequired;
            string status = processingAllowed ? "complete" : "incomplete";
            return new CaptureValidation(
                status,
                processingAllowed,
                recaptureRequired,
                offlineDecodeRequired,
                issues,
                notes);
        }

        private bool IsRawRecaptureRequired()
        {
            int observedRawPackets = this.inboundPacketCount + this.outboundPacketCount;
            return (this.rawPacketLogRowCount != observedRawPackets
                    && this.rawPacketIndexRowCount != observedRawPackets
                    && this.rawPacketPreservedCount != observedRawPackets)
                   || this.rawPacketCallbackDrainTimeoutCount > 0;
        }

        private bool IsCaptureRecaptureRequired()
        {
            CaptureCallbackBoundarySnapshot callbackHealth = this.callbackBoundary.Snapshot();
            return this.IsRawRecaptureRequired()
                   || callbackHealth.TotalErrorCount > 0
                   || callbackHealth.ErrorLogWriteFailureCount > 0
                   || (this.pf127GeometryCapture != null
                       && this.pf127GeometryCapture.RecaptureRequired);
        }

        private bool HasOfflineDecodeWork(int observedRawPackets)
        {
            return this.rawPacketLogRowCount != observedRawPackets
                   || this.rawPacketIndexRowCount != observedRawPackets
                   || this.rawPacketWriteErrorCount > 0
                   || this.rawPacketCallbackDrainTimeoutCount > 0
                   || this.rawPacketProjectionErrorCount > 0
                   || this.n3CaptureStageErrorCount > 0
                   || (this.decodedInboundCount + this.decodedOutboundCount > 0
                       && this.decodedN3EventRowCount == 0)
                   || (this.rawCombatPacketCount > 0 && this.enemyCombatRowCount == 0)
                   || this.rawSimpleCharFullUpdatePacketCount
                      != this.rawSimpleCharFullUpdateDecodeCount
                         + this.rawSimpleCharFullUpdateDecodeErrorCount
                   || this.scfuAppearanceRowCount != this.rawSimpleCharFullUpdatePacketCount
                   || this.rawSimpleCharFullUpdateDecodeErrorCount > 0
                   || this.rawSimpleCharFullUpdateIncompleteDecodeCount > 0
                   || this.enemyFullUpdateRowCount != this.rawNpcSimpleCharFullUpdateCount
                   || this.movementDecodeErrorCount > 0
                   || (this.movementFollowTargetPacketCount > 0
                       && this.movementUsableFollowTargetPacketCount == 0)
                   || this.corpseFullUpdateDecodeErrorCount > 0
                   || (this.corpseFullUpdatePacketCount > 0
                       && this.corpseFullUpdateRowCount == 0)
                   || (this.corpseInventoryUpdateCount > 0
                       && this.corpseLootObservationRowCount != this.corpseInventoryUpdateCount)
                   || this.corpseLootUnlinkedSnapshotCount > 0
                   || this.corpseLootMissingPlayerContextCount > 0
                   || (this.vendorInteractionAttemptCount > 0 && this.shopUpdateRowCount == 0)
                   || (this.vendorInteractionAttemptCount > 0
                       && this.vendorFullUpdateMessageCount == 0);
        }

        private void WriteEnemyRespawnCsv(DateTime captureEndUtc)
        {
            try
            {
                string path = Path.Combine(this.sessionDirectory, "enemy-respawns.csv");
                List<EnemyRespawnObservation> observations = this.BuildEnemyRespawnObservations(captureEndUtc);
                using (StreamWriter writer = CreateWriter(path))
                {
                    writer.WriteLine("GeneratedUtc,Status,DeathIdentity,Name,MonsterData,NpcFamily,DeathUtc,DeathX,DeathY,DeathZ,CorpseIdentity,CorpseSeenUtc,CorpseGoneUtc,RespawnIdentity,RespawnUtc,RespawnDelaySeconds,RespawnAfterCorpseGoneSeconds,RespawnX,RespawnY,RespawnZ,PositionDelta,ElapsedAfterDeathSeconds,CandidateCount,Detail");
                    string generatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                    foreach (EnemyRespawnObservation observation in observations)
                    {
                        writer.WriteLine(
                            string.Join(
                                ",",
                                Csv(generatedUtc),
                                Csv(observation.Status),
                                Csv(observation.DeathIdentity),
                                Csv(observation.Name),
                                Csv(observation.MonsterData),
                                Csv(observation.NpcFamily),
                                Csv(observation.DeathUtc.ToString("o", CultureInfo.InvariantCulture)),
                                NullableFloat(observation.DeathX),
                                NullableFloat(observation.DeathY),
                                NullableFloat(observation.DeathZ),
                                Csv(observation.CorpseIdentity),
                                observation.CorpseSeenUtc.HasValue
                                    ? Csv(observation.CorpseSeenUtc.Value.ToString("o", CultureInfo.InvariantCulture))
                                    : string.Empty,
                                observation.CorpseGoneUtc.HasValue
                                    ? Csv(observation.CorpseGoneUtc.Value.ToString("o", CultureInfo.InvariantCulture))
                                    : string.Empty,
                                Csv(observation.RespawnIdentity),
                                observation.RespawnUtc.HasValue
                                    ? Csv(observation.RespawnUtc.Value.ToString("o", CultureInfo.InvariantCulture))
                                    : string.Empty,
                                observation.RespawnDelaySeconds.HasValue
                                    ? observation.RespawnDelaySeconds.Value.ToString("0.###", CultureInfo.InvariantCulture)
                                    : string.Empty,
                                observation.RespawnAfterCorpseGoneSeconds.HasValue
                                    ? observation.RespawnAfterCorpseGoneSeconds.Value.ToString("0.###", CultureInfo.InvariantCulture)
                                    : string.Empty,
                                NullableFloat(observation.RespawnX),
                                NullableFloat(observation.RespawnY),
                                NullableFloat(observation.RespawnZ),
                                observation.PositionDelta.HasValue
                                    ? observation.PositionDelta.Value.ToString("0.###", CultureInfo.InvariantCulture)
                                    : string.Empty,
                                observation.ElapsedAfterDeathSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                                observation.CandidateCount.ToString(CultureInfo.InvariantCulture),
                                Csv(observation.Detail)));
                    }
                }
            }
            catch (Exception ex)
            {
                this.LogEvent("ENEMY-RESPAWN-CSV-ERROR", ex.ToString());
            }
        }

        private List<EnemyRespawnObservation> BuildEnemyRespawnObservations(DateTime captureEndUtc)
        {
            const double SamePositionThreshold = 2.0;
            List<EnemyRespawnObservation> observations = new List<EnemyRespawnObservation>();
            List<EnemyStateEvent> events = new List<EnemyStateEvent>();
            lock (this.syncRoot)
            {
                foreach (List<EnemyStateEvent> timeline in this.enemyStateTimeline.Values)
                {
                    events.AddRange(timeline);
                }
            }

            events = events.OrderBy(x => x.TimestampUtc).ToList();
            foreach (EnemyStateEvent death in events.Where(x => x.EventType == "death"))
            {
                EnemyEntityState deadState = this.GetEnemyStateSnapshot(death.EntityId);
                if (deadState == null)
                {
                    continue;
                }

                List<EnemyStateEvent> candidates = events
                    .Where(
                        x => x.TimestampUtc > death.TimestampUtc
                             && IsRespawnCandidateEvent(x)
                             && this.IsSameEnemyRespawnCandidate(deadState, death, x, SamePositionThreshold))
                    .OrderBy(x => x.TimestampUtc)
                    .ToList();

                EnemyStateEvent selected = candidates.FirstOrDefault();
                string status = selected == null ? "incomplete" : "complete";
                if (candidates.Count > 1
                    && Math.Abs((candidates[1].TimestampUtc - selected.TimestampUtc).TotalSeconds) <= 2.0)
                {
                    status = "ambiguous";
                }

                CorpseLifecycleEvidence corpse;
                this.corpseEvidenceByDeadNpc.TryGetValue(NormalizeIdentityKey(death.EntityId), out corpse);

                EnemyRespawnObservation observation = new EnemyRespawnObservation
                {
                    Status = status,
                    DeathIdentity = death.EntityId,
                    Name = deadState.Name,
                    MonsterData = deadState.MonsterData,
                    NpcFamily = deadState.NpcFamily,
                    DeathUtc = death.TimestampUtc,
                    DeathX = death.X,
                    DeathY = death.Y,
                    DeathZ = death.Z,
                    CorpseIdentity = corpse == null ? string.Empty : corpse.CorpseIdentity,
                    CorpseSeenUtc = corpse == null ? (DateTime?)null : corpse.CorpseSeenUtc,
                    CorpseGoneUtc = corpse == null ? (DateTime?)null : corpse.CorpseGoneUtc,
                    RespawnIdentity = selected == null ? string.Empty : selected.EntityId,
                    RespawnUtc = selected == null ? (DateTime?)null : selected.TimestampUtc,
                    RespawnDelaySeconds = selected == null
                        ? (double?)null
                        : Math.Max(0, (selected.TimestampUtc - death.TimestampUtc).TotalSeconds),
                    RespawnAfterCorpseGoneSeconds = selected == null || corpse == null || !corpse.CorpseGoneUtc.HasValue
                        ? (double?)null
                        : Math.Max(0, (selected.TimestampUtc - corpse.CorpseGoneUtc.Value).TotalSeconds),
                    RespawnX = selected == null ? null : selected.X,
                    RespawnY = selected == null ? null : selected.Y,
                    RespawnZ = selected == null ? null : selected.Z,
                    PositionDelta = selected == null ? null : this.PositionDelta(death, selected),
                    ElapsedAfterDeathSeconds = Math.Max(0, (captureEndUtc - death.TimestampUtc).TotalSeconds),
                    CandidateCount = candidates.Count,
                    Detail = selected == null
                        ? "No later same-name/same-monsterData/same-position spawn was observed before capture stop."
                        : "Matched later same-name/same-monsterData/same-position spawn."
                };
                observations.Add(observation);
            }

            return observations;
        }

        private EnemyEntityState GetEnemyStateSnapshot(string entityId)
        {
            lock (this.syncRoot)
            {
                EnemyEntityState state;
                return this.enemyStates.TryGetValue(entityId, out state) ? state : null;
            }
        }

        private bool IsSameEnemyRespawnCandidate(
            EnemyEntityState deadState,
            EnemyStateEvent death,
            EnemyStateEvent candidate,
            double samePositionThreshold)
        {
            EnemyEntityState candidateState = this.GetEnemyStateSnapshot(candidate.EntityId);
            if (candidateState == null)
            {
                return false;
            }

            if (!string.Equals(deadState.Name ?? string.Empty, candidateState.Name ?? string.Empty, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(deadState.MonsterData ?? string.Empty, candidateState.MonsterData ?? string.Empty, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(deadState.NpcFamily ?? string.Empty, candidateState.NpcFamily ?? string.Empty, StringComparison.Ordinal))
            {
                return false;
            }

            double? delta = this.PositionDelta(death, candidate);
            return delta.HasValue && delta.Value <= samePositionThreshold;
        }

        private static bool IsRespawnCandidateEvent(EnemyStateEvent stateEvent)
        {
            return stateEvent.EventType == "spawn"
                   || stateEvent.EventType == "population"
                   || stateEvent.EventType == "respawn";
        }

        private static string NormalizeIdentityKey(string identity)
        {
            if (string.IsNullOrWhiteSpace(identity))
            {
                return string.Empty;
            }

            string trimmed = identity.Trim().TrimStart('(').TrimEnd(')');
            int separator = trimmed.IndexOf(':');
            if (separator <= 0 || separator == trimmed.Length - 1)
            {
                return trimmed;
            }

            uint instance;
            if (!uint.TryParse(
                    trimmed.Substring(separator + 1),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out instance))
            {
                return trimmed;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1:X8}",
                trimmed.Substring(0, separator),
                instance);
        }

        private double? PositionDelta(EnemyStateEvent first, EnemyStateEvent second)
        {
            if (!first.X.HasValue || !first.Y.HasValue || !first.Z.HasValue
                || !second.X.HasValue || !second.Y.HasValue || !second.Z.HasValue)
            {
                return null;
            }

            double dx = first.X.Value - second.X.Value;
            double dy = first.Y.Value - second.Y.Value;
            double dz = first.Z.Value - second.Z.Value;
            return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
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

        private void WriteEnemyDossierJson()
        {
            try
            {
                string path = Path.Combine(this.sessionDirectory, "enemy-dossier.json");
                StringBuilder json = new StringBuilder();
                lock (this.syncRoot)
                {
                    json.AppendLine("{");
                    json.Append("  \"generatedUtc\": ");
                    json.Append(Json(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
                    json.AppendLine(",");
                    json.Append("  \"captureFolder\": ");
                    json.Append(Json(this.sessionDirectory));
                    json.AppendLine(",");
                    json.Append("  \"resourcePlayfieldId\": ");
                    json.Append(Json(this.GetDetectedResourcePlayfieldId()));
                    json.AppendLine(",");
                    json.Append("  \"runtimePlayfieldId\": ");
                    json.Append(Json(this.GetDetectedPlayfieldId()));
                    json.AppendLine(",");
                    json.Append("  \"capturePlayfieldIdentity\": ");
                    json.Append(Json(this.GetCapturePlayfieldIdentity()));
                    json.AppendLine(",");
                    json.Append("  \"capturePlayfieldObjectId\": ");
                    json.Append(Json(this.GetCapturePlayfieldObjectId()));
                    json.AppendLine(",");
                    json.Append("  \"autoCaptureEnabled\": ");
                    json.Append(this.enemyFightAutoCaptureEnabled ? "true" : "false");
                    json.AppendLine(",");
                    json.Append("  \"manualCaptureEnabled\": ");
                    json.Append(this.enemyFightCaptureEnabled ? "true" : "false");
                    json.AppendLine(",");
                    json.Append("  \"focusedEnemyIdentities\": ");
                    AppendJsonStringArray(json, this.focusedEnemyIdentities.OrderBy(value => value).ToArray());
                    json.AppendLine(",");
                    json.AppendLine("  \"enemies\": [");

                    EnemyEntityState[] states = this.enemyStates.Values.OrderBy(value => value.EntityId).ToArray();
                    for (int i = 0; i < states.Length; i++)
                    {
                        this.AppendEnemyDossierJson(json, states[i], "    ");
                        if (i < states.Length - 1)
                        {
                            json.Append(",");
                        }

                        json.AppendLine();
                    }

                    json.AppendLine("  ]");
                    json.AppendLine("}");
                }

                File.WriteAllText(path, json.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                this.LogEvent("ENEMY-DOSSIER-JSON-ERROR", ex.ToString());
            }
        }

        private void AppendEnemyDossierJson(StringBuilder json, EnemyEntityState state, string indent)
        {
            List<EnemyStateEvent> timeline;
            this.enemyStateTimeline.TryGetValue(state.EntityId, out timeline);
            int eventCount = timeline == null ? 0 : timeline.Count;

            json.Append(indent);
            json.AppendLine("{");
            AppendJsonField(json, indent + "  ", "identity", state.EntityId, true);
            AppendJsonField(json, indent + "  ", "name", state.Name, true);
            AppendJsonField(json, indent + "  ", "monsterData", state.MonsterData, true);
            AppendJsonField(json, indent + "  ", "monsterScale", state.MonsterScale, true);
            AppendJsonField(json, indent + "  ", "catMesh", state.CatMesh, true);
            AppendJsonField(json, indent + "  ", "visualFlags", state.VisualFlags, true);
            AppendJsonField(json, indent + "  ", "headMesh", state.HeadMesh, true);
            AppendJsonField(json, indent + "  ", "runSpeed", state.RunSpeed, true);
            AppendJsonField(json, indent + "  ", "npcFamily", state.NpcFamily, true);
            AppendJsonField(json, indent + "  ", "losHeight", state.LosHeight, true);
            AppendJsonField(json, indent + "  ", "minDamage", state.MinDamage, true);
            AppendJsonField(json, indent + "  ", "maxDamage", state.MaxDamage, true);
            AppendJsonField(json, indent + "  ", "defaultAttackType", state.DefaultAttackType, true);
            AppendJsonField(json, indent + "  ", "attackDelay", state.AttackDelay, true);
            AppendJsonField(json, indent + "  ", "rechargeDelay", state.RechargeDelay, true);
            AppendJsonField(json, indent + "  ", "populationEvidenceSource", state.PopulationEvidenceSource, true);
            AppendJsonField(json, indent + "  ", "resourcePlayfieldId", state.ResourcePlayfieldId, true);
            AppendJsonField(json, indent + "  ", "runtimePlayfieldId", state.RuntimePlayfieldId, true);
            AppendJsonField(json, indent + "  ", "capturePlayfieldIdentity", state.CapturePlayfieldIdentity, true);
            AppendJsonField(json, indent + "  ", "capturePlayfieldObjectId", state.CapturePlayfieldObjectId, true);
            json.Append(indent);
            json.Append("  \"level\": ");
            AppendJsonNullableInt(json, state.Level);
            json.AppendLine(",");
            json.Append(indent);
            json.Append("  \"currentHealth\": ");
            AppendJsonNullableInt(json, state.CurrentHealth);
            json.AppendLine(",");
            json.Append(indent);
            json.Append("  \"maxHealth\": ");
            AppendJsonNullableInt(json, state.MaxHealth);
            json.AppendLine(",");
            json.Append(indent);
            json.Append("  \"position\": { \"x\": ");
            AppendJsonNullableFloat(json, state.X);
            json.Append(", \"y\": ");
            AppendJsonNullableFloat(json, state.Y);
            json.Append(", \"z\": ");
            AppendJsonNullableFloat(json, state.Z);
            json.AppendLine(" },");
            json.Append(indent);
            json.Append("  \"firstSeenUtc\": ");
            json.Append(Json(state.FirstSeenUtc.ToString("o", CultureInfo.InvariantCulture)));
            json.AppendLine(",");
            json.Append(indent);
            json.Append("  \"lastUpdateUtc\": ");
            json.Append(Json(state.LastUpdateUtc.ToString("o", CultureInfo.InvariantCulture)));
            json.AppendLine(",");
            json.Append(indent);
            json.Append("  \"deathObserved\": ");
            json.Append(state.DeathLogged ? "true" : "false");
            json.AppendLine(",");
            json.Append(indent);
            json.Append("  \"populationEvidenceObserved\": ");
            json.Append(state.PopulationEvidenceObserved ? "true" : "false");
            json.AppendLine(",");
            json.Append(indent);
            json.Append("  \"eventCount\": ");
            json.Append(eventCount.ToString(CultureInfo.InvariantCulture));
            json.AppendLine();
            json.Append(indent);
            json.Append("}");
        }

        private void AppendEnemyStateEventJson(StringBuilder json, EnemyStateEvent stateEvent, string indent)
        {
            json.Append(indent);
            json.Append("{ ");
            json.Append("\"timestamp\": ");
            json.Append(Json(stateEvent.TimestampUtc.ToString("o", CultureInfo.InvariantCulture)));
            json.Append(", \"entityId\": ");
            json.Append(Json(stateEvent.EntityId));
            json.Append(", \"direction\": ");
            json.Append(Json(stateEvent.Direction));
            json.Append(", \"sequence\": ");
            json.Append(stateEvent.Sequence.ToString(CultureInfo.InvariantCulture));
            json.Append(", \"messageType\": ");
            json.Append(Json(stateEvent.MessageType));
            json.Append(", \"evidenceSource\": ");
            json.Append(Json(stateEvent.EvidenceSource));
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
                this.AppendCallbackHealthJson(json, "  ");
                json.AppendLine(",");
                if (this.pf127GeometryCapture != null)
                {
                    this.pf127GeometryCapture.AppendHealthJson(json, "  ");
                    json.AppendLine(",");
                }

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

        private void WriteMovementSummaryJson()
        {
            try
            {
                string path = Path.Combine(this.sessionDirectory, "movement-summary.json");
                StringBuilder json = new StringBuilder();
                json.AppendLine("{");
                json.Append("  \"generatedUtc\": ");
                json.Append(Json(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
                json.AppendLine(",");
                json.Append("  \"movementPacketsCsv\": ");
                json.Append(Json(Path.Combine(this.sessionDirectory, "movement-packets.csv")));
                json.AppendLine(",");
                json.Append("  \"followTargetDecodedWithUsablePath\": ");
                json.Append(this.movementUsableFollowTargetPacketCount > 0 ? "true" : "false");
                json.AppendLine(",");
                json.AppendLine("  \"counts\": {");
                json.Append("    \"movementPacketRows\": ");
                json.Append(this.movementPacketRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"followTargetPackets\": ");
                json.Append(this.movementFollowTargetPacketCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"usableFollowTargetPackets\": ");
                json.Append(this.movementUsableFollowTargetPacketCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"setPosPackets\": ");
                json.Append(this.movementSetPosPacketCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"stopMovingCmdPackets\": ");
                json.Append(this.movementStopMovingCmdPacketCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"decodeErrors\": ");
                json.Append(this.movementDecodeErrorCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine();
                json.AppendLine("  }");
                json.AppendLine("}");
                File.WriteAllText(path, json.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                this.LogEvent("MOVEMENT-SUMMARY-ERROR", ex.ToString());
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
                json.Append("  \"captureStopRequestedUtc\": ");
                json.Append(this.captureStopRequestedUtc.HasValue ? Json(this.captureStopRequestedUtc.Value.ToString("o", CultureInfo.InvariantCulture)) : "null");
                json.AppendLine(",");
                json.Append("  \"lastRawPacketUtc\": ");
                json.Append(Json(this.lastPacketUtc.ToString("o", CultureInfo.InvariantCulture)));
                json.AppendLine(",");
                json.Append("  \"captureFinalizedUtc\": ");
                json.Append(this.captureFinalizedUtc.HasValue ? Json(this.captureFinalizedUtc.Value.ToString("o", CultureInfo.InvariantCulture)) : "null");
                json.AppendLine(",");
                json.Append("  \"quietPeriodPassed\": ");
                json.Append(this.captureQuietPeriodPassed ? "true" : "false");
                json.AppendLine(",");
                json.Append("  \"sessionDurationSeconds\": ");
                json.Append(Math.Max(0, (durationEndUtc - this.captureStartUtc).TotalSeconds).ToString("0.###", CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("  \"captureFolderPath\": ");
                json.Append(Json(this.sessionDirectory));
                json.AppendLine(",");
                json.Append("  \"rawPacketLogPath\": ");
                json.Append(Json(Path.Combine(this.sessionDirectory, "packets.hex.log")));
                json.AppendLine(",");
                json.Append("  \"rawPacketIndexPath\": ");
                json.Append(Json(Path.Combine(this.sessionDirectory, "raw-packets.csv")));
                json.AppendLine(",");
                json.Append("  \"scfuAppearancePath\": ");
                json.Append(Json(Path.Combine(this.sessionDirectory, "scfu-appearance.csv")));
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
                json.AppendLine(",");
                json.Append("    \"decodedN3EventRows\": ");
                json.Append(this.decodedN3EventRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"decodedN3StageErrors\": ");
                json.Append(this.n3CaptureStageErrorCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"rawCombatPackets\": ");
                json.Append(this.rawCombatPacketCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"rawSimpleCharFullUpdatePackets\": ");
                json.Append(this.rawSimpleCharFullUpdatePacketCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"rawPacketLogRows\": ");
                json.Append(this.rawPacketLogRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"rawPacketIndexRows\": ");
                json.Append(this.rawPacketIndexRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"rawPacketPreserved\": ");
                json.Append(this.rawPacketPreservedCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"rawPacketWriteErrors\": ");
                json.Append(this.rawPacketWriteErrorCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"rawPacketCallbackDrainTimeouts\": ");
                json.Append(this.rawPacketCallbackDrainTimeoutCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"rawPacketProjectionErrors\": ");
                json.Append(this.rawPacketProjectionErrorCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"rawSimpleCharFullUpdateDecoded\": ");
                json.Append(this.rawSimpleCharFullUpdateDecodeCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"rawSimpleCharFullUpdateDecodeErrors\": ");
                json.Append(this.rawSimpleCharFullUpdateDecodeErrorCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"rawSimpleCharFullUpdateIncompleteDecodes\": ");
                json.Append(this.rawSimpleCharFullUpdateIncompleteDecodeCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"rawNpcSimpleCharFullUpdates\": ");
                json.Append(this.rawNpcSimpleCharFullUpdateCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"scfuAppearanceRows\": ");
                json.Append(this.scfuAppearanceRowCount.ToString(CultureInfo.InvariantCulture));
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
                json.Append("    \"enemyFightAutoCaptureEnabled\": ");
                json.Append(this.enemyFightAutoCaptureEnabled ? "true" : "false");
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
                json.Append("    \"movementPacketRows\": ");
                json.Append(this.movementPacketRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"movementFollowTargetPackets\": ");
                json.Append(this.movementFollowTargetPacketCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"movementUsableFollowTargetPackets\": ");
                json.Append(this.movementUsableFollowTargetPacketCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"movementSetPosPackets\": ");
                json.Append(this.movementSetPosPacketCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"movementStopMovingCmdPackets\": ");
                json.Append(this.movementStopMovingCmdPacketCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"movementDecodeErrors\": ");
                json.Append(this.movementDecodeErrorCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyStatUpdateRows\": ");
                json.Append(this.enemyStatUpdateRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"corpseFullUpdatePackets\": ");
                json.Append(this.corpseFullUpdatePacketCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"corpseFullUpdateRows\": ");
                json.Append(this.corpseFullUpdateRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"corpseFullUpdateDecodeErrors\": ");
                json.Append(this.corpseFullUpdateDecodeErrorCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"corpseInventoryUpdates\": ");
                json.Append(this.corpseInventoryUpdateCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"corpseLootObservationRows\": ");
                json.Append(this.corpseLootObservationRowCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"corpseLootInitialSnapshots\": ");
                json.Append(this.corpseLootInitialSnapshotCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"corpseLootUnlinkedSnapshots\": ");
                json.Append(this.corpseLootUnlinkedSnapshotCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"corpseLootMissingPlayerContext\": ");
                json.Append(this.corpseLootMissingPlayerContextCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"corpseSeenEvents\": ");
                json.Append(this.corpseSeenEventCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"corpseGoneEvents\": ");
                json.Append(this.corpseGoneEventCount.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"npcLifecycleRows\": ");
                json.Append(this.npcLifecycleRowCount.ToString(CultureInfo.InvariantCulture));
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
                List<EnemyRespawnObservation> respawns = this.BuildEnemyRespawnObservations(durationEndUtc);
                json.Append("    \"enemyRespawnRows\": ");
                json.Append(respawns.Count.ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"enemyRespawnCompleteRows\": ");
                json.Append(respawns.Count(x => x.Status == "complete").ToString(CultureInfo.InvariantCulture));
                json.AppendLine(",");
                json.Append("    \"respawnCaptureRequested\": ");
                json.Append(this.respawnCaptureRequested ? "true" : "false");
                json.AppendLine(",");
                json.Append("    \"lootCaptureRequested\": ");
                json.Append(this.lootCaptureRequested ? "true" : "false");
                json.AppendLine(",");
                json.Append("    \"lootCaptureEnemyTypes\": ");
                json.Append(this.corpseLootInitialEnemyKeys.Count.ToString(CultureInfo.InvariantCulture));
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
                json.Append("  \"focusedEnemyIdentities\": ");
                AppendJsonStringArray(json, this.focusedEnemyIdentities.OrderBy(value => value).ToArray());
                json.AppendLine(",");
                this.AppendCallbackHealthJson(json, "  ");
                json.AppendLine(",");
                if (this.pf127GeometryCapture != null)
                {
                    this.pf127GeometryCapture.AppendHealthJson(json, "  ");
                    json.AppendLine(",");
                }

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

        private void AppendCallbackHealthJson(StringBuilder json, string indent)
        {
            CaptureCallbackBoundarySnapshot snapshot = this.callbackBoundary.Snapshot();
            json.Append(indent);
            json.AppendLine("\"callbackHealth\": {");
            json.Append(indent);
            json.Append("  \"errorLogPath\": ");
            json.Append(Json(snapshot.ErrorLogPath));
            json.AppendLine(",");
            json.Append(indent);
            json.Append("  \"totalInvocations\": ");
            json.Append(snapshot.TotalInvocationCount.ToString(CultureInfo.InvariantCulture));
            json.AppendLine(",");
            json.Append(indent);
            json.Append("  \"totalErrors\": ");
            json.Append(snapshot.TotalErrorCount.ToString(CultureInfo.InvariantCulture));
            json.AppendLine(",");
            json.Append(indent);
            json.Append("  \"errorLogWriteFailures\": ");
            json.Append(snapshot.ErrorLogWriteFailureCount.ToString(CultureInfo.InvariantCulture));
            json.AppendLine(",");
            json.Append(indent);
            json.AppendLine("  \"callbacks\": [");
            for (int index = 0; index < snapshot.Counters.Length; index++)
            {
                CaptureCallbackCounterSnapshot counter = snapshot.Counters[index];
                json.Append(indent);
                json.Append("    { \"name\": ");
                json.Append(Json(counter.CallbackName));
                json.Append(", \"invocations\": ");
                json.Append(counter.InvocationCount.ToString(CultureInfo.InvariantCulture));
                json.Append(", \"errors\": ");
                json.Append(counter.ErrorCount.ToString(CultureInfo.InvariantCulture));
                json.Append(" }");
                json.AppendLine(index + 1 < snapshot.Counters.Length ? "," : string.Empty);
            }

            json.Append(indent);
            json.AppendLine("  ]");
            json.Append(indent);
            json.Append("}");
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
            json.Append("  \"recaptureRequired\": ");
            json.Append(validation.RecaptureRequired ? "true" : "false");
            json.AppendLine(",");
            json.Append(indent);
            json.Append("  \"offlineDecodeRequired\": ");
            json.Append(validation.OfflineDecodeRequired ? "true" : "false");
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

        private string GetCapturePlayfieldIdentity()
        {
            if (!string.IsNullOrWhiteSpace(this.lastCapturePlayfieldIdentity))
            {
                return this.lastCapturePlayfieldIdentity;
            }

            return Safe(() => Playfield.Identity.ToString());
        }

        private string GetCapturePlayfieldObjectId()
        {
            string identity = this.GetCapturePlayfieldIdentity();
            int separator = identity.IndexOf(':');
            int end = identity.IndexOf(')', separator + 1);
            if (separator >= 0 && end > separator)
            {
                return identity.Substring(separator + 1, end - separator - 1);
            }

            return Safe(() => Playfield.Identity.Instance.ToString(CultureInfo.InvariantCulture));
        }

        private string GetDetectedResourcePlayfieldId()
        {
            if (SafeBool(() => Playfield.ModelIdentity.Instance == 127))
            {
                return "127";
            }

            string capturePlayfieldObjectId = this.GetCapturePlayfieldObjectId();
            if (string.Equals(capturePlayfieldObjectId, "122002", StringComparison.Ordinal))
            {
                return "127";
            }

            return string.Empty;
        }

        private bool IsDetectedResourcePlayfield127()
        {
            return string.Equals(
                this.GetDetectedResourcePlayfieldId(),
                "127",
                StringComparison.Ordinal);
        }

        private sealed class CaptureValidation
        {
            public CaptureValidation(
                string status,
                bool processingAllowed,
                bool recaptureRequired,
                bool offlineDecodeRequired,
                List<string> issues,
                List<string> notes)
            {
                this.Status = status;
                this.ProcessingAllowed = processingAllowed;
                this.RecaptureRequired = recaptureRequired;
                this.OfflineDecodeRequired = offlineDecodeRequired;
                this.Issues = issues;
                this.Notes = notes;
            }

            public string Status { get; private set; }

            public bool ProcessingAllowed { get; private set; }

            public bool RecaptureRequired { get; private set; }

            public bool OfflineDecodeRequired { get; private set; }

            public List<string> Issues { get; private set; }

            public List<string> Notes { get; private set; }

            public static CaptureValidation Running()
            {
                return new CaptureValidation(
                    "running",
                    false,
                    false,
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

            public string Name { get; set; }

            public string MonsterData { get; set; }

            public string MonsterScale { get; set; }

            public string CatMesh { get; set; }

            public string VisualFlags { get; set; }

            public string HeadMesh { get; set; }

            public string RunSpeed { get; set; }

            public string NpcFamily { get; set; }

            public string LosHeight { get; set; }

            public string MinDamage { get; set; }

            public string MaxDamage { get; set; }

            public string DefaultAttackType { get; set; }

            public string AttackDelay { get; set; }

            public string RechargeDelay { get; set; }

            public int? Level { get; set; }

            public int? CurrentHealth { get; set; }

            public int? MaxHealth { get; set; }

            public float? X { get; set; }

            public float? Y { get; set; }

            public float? Z { get; set; }

            public DateTime FirstSeenUtc { get; set; }

            public DateTime LastUpdateUtc { get; set; }

            public bool DeathLogged { get; set; }

            public bool PopulationEvidenceObserved { get; set; }

            public string PopulationEvidenceSource { get; set; }

            public string ResourcePlayfieldId { get; set; }

            public string RuntimePlayfieldId { get; set; }

            public string CapturePlayfieldIdentity { get; set; }

            public string CapturePlayfieldObjectId { get; set; }
        }

        private sealed class EnemyStateEvent
        {
            public DateTime TimestampUtc { get; set; }

            public string Direction { get; set; }

            public int Sequence { get; set; }

            public string MessageType { get; set; }

            public string EvidenceSource { get; set; }

            public string EntityId { get; set; }

            public int? Level { get; set; }

            public int? CurrentHealth { get; set; }

            public int? MaxHealth { get; set; }

            public float? X { get; set; }

            public float? Y { get; set; }

            public float? Z { get; set; }

            public string EventType { get; set; }
        }

        private sealed class EnemyRespawnObservation
        {
            public string Status { get; set; }

            public string DeathIdentity { get; set; }

            public string Name { get; set; }

            public string MonsterData { get; set; }

            public string NpcFamily { get; set; }

            public DateTime DeathUtc { get; set; }

            public float? DeathX { get; set; }

            public float? DeathY { get; set; }

            public float? DeathZ { get; set; }

            public string CorpseIdentity { get; set; }

            public DateTime? CorpseSeenUtc { get; set; }

            public DateTime? CorpseGoneUtc { get; set; }

            public string RespawnIdentity { get; set; }

            public DateTime? RespawnUtc { get; set; }

            public double? RespawnDelaySeconds { get; set; }

            public double? RespawnAfterCorpseGoneSeconds { get; set; }

            public float? RespawnX { get; set; }

            public float? RespawnY { get; set; }

            public float? RespawnZ { get; set; }

            public double? PositionDelta { get; set; }

            public double ElapsedAfterDeathSeconds { get; set; }

            public int CandidateCount { get; set; }

            public string Detail { get; set; }
        }

        private sealed class CorpseLifecycleEvidence
        {
            public string DeadNpcIdentity { get; set; }

            public string CorpseIdentity { get; set; }

            public DateTime CorpseSeenUtc { get; set; }

            public DateTime? CorpseGoneUtc { get; set; }

            public int PlayfieldId { get; set; }

            public int CorpseCredits { get; set; }

            public int CorpseMonsterData { get; set; }
        }

        private sealed class RecentEnemyFullUpdateEvidence
        {
            public string Direction { get; set; }

            public int Sequence { get; set; }

            public SimpleCharFullUpdateMessage Message { get; set; }
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
            try
            {
                lock (this.syncRoot)
                {
                    if (this.eventsLog == null)
                    {
                        return;
                    }

                    this.eventsLog.WriteLine(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0:o} [{1}] {2}",
                            DateTime.UtcNow,
                            category,
                            OneLine(message)));
                }
            }
            catch
            {
                // Capture diagnostics must never escape into the AO client.
                Interlocked.Increment(ref this.rawPacketProjectionErrorCount);
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

        private static bool IsRawCombatEvidencePacket(byte[] packet)
        {
            if (packet == null || packet.Length < 20)
            {
                return false;
            }

            int typeValue = ReadInt32BigEndian(packet, 16);
            if (!Enum.IsDefined(typeof(N3MessageType), typeValue))
            {
                return false;
            }

            string messageName = ((N3MessageType)typeValue).ToString();
            return string.Equals(messageName, "Attack", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "AttackInfo", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "SpecialAttackWeapon", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "SpecialAttackInfo", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "MissedAttackInfo", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "HealthDamage", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(messageName, "StopFight", StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveDynelName(uint identityType, uint identityInstance)
        {
            try
            {
                if (DynelManager.AllDynels == null)
                {
                    return string.Empty;
                }

                foreach (Dynel dynel in DynelManager.AllDynels)
                {
                    if (dynel == null)
                    {
                        continue;
                    }

                    uint dynelType = unchecked((uint)(int)dynel.Identity.Type);
                    uint dynelInstance = unchecked((uint)dynel.Identity.Instance);
                    if (dynelType == identityType && dynelInstance == identityInstance)
                    {
                        return dynel.Name;
                    }
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static bool TryReadRawIdentity(byte[] bytes, int offset, out uint identityType, out uint identityInstance)
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

        private static string FormatRawIdentity(uint identityType, uint identityInstance)
        {
            return FormatRawIdentityType(identityType) + ":" + FormatRawInstance(identityInstance);
        }

        private static string FormatRawIdentityType(uint identityType)
        {
            if (identityType <= int.MaxValue && Enum.IsDefined(typeof(IdentityType), (int)identityType))
            {
                return ((IdentityType)(int)identityType).ToString();
            }

            return identityType.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatRawInstance(uint identityInstance)
        {
            return identityInstance.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static string FormatNullableFloat(float? value)
        {
            return value.HasValue ? FormatFloat(value.Value) : string.Empty;
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

        private static string GetByteArrayHexString(object value)
        {
            byte[] bytes = value as byte[];
            return bytes == null ? string.Empty : ToHex(bytes);
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

        private void OpenFreshCaptureSession(string pluginDir, bool resetState, bool activate)
        {
            lock (this.syncRoot)
            {
                if (this.enabled && !this.captureFinalized)
                {
                    this.CompleteCaptureStop(DateTime.UtcNow, false, false);
                }

                this.enabled = false;
                this.FlushAndClose();

            if (resetState)
            {
                this.ResetCaptureState();
            }

            this.ApplyExternalCaptureRequest(pluginDir);

            this.sessionDirectory = CreateSessionDirectory(pluginDir);
            this.callbackBoundary.BeginSession(
                Path.Combine(this.sessionDirectory, "capture-callback-errors.log"),
                GetCallbackErrorFallbackPath(pluginDir));
            this.eventsLog = CreateWriter(Path.Combine(this.sessionDirectory, "events.log"));
            this.packetsLog = CreateWriter(Path.Combine(this.sessionDirectory, "packets.hex.log"), true);
            this.rawPacketsCsvLog = CreateWriter(Path.Combine(this.sessionDirectory, "raw-packets.csv"), true);
            this.rawPacketsCsvLog.WriteLine("CapturedUtc,ElapsedMilliseconds,Direction,GlobalOrdinal,Sequence,PacketLength,N3TypeValue,N3TypeName,IdentityType,IdentityInstance,PreservationStatus,RawHex");
            this.scfuAppearanceLog = CreateWriter(Path.Combine(this.sessionDirectory, "scfu-appearance.csv"));
            this.scfuAppearanceLog.WriteLine(RawScfuAppearanceCsv.Header);
            this.shopUpdatesLog = CreateWriter(Path.Combine(this.sessionDirectory, "shop-updates.csv"));
            this.shopUpdatesLog.WriteLine("CapturedUtc,Direction,Sequence,TerminalIdentity,Slot,LowId,HighId,Quality");
            this.vendorFullUpdatesLog = CreateWriter(Path.Combine(this.sessionDirectory, "vendor-full-updates.csv"));
            this.vendorFullUpdatesLog.WriteLine("CapturedUtc,Direction,Sequence,Identity,OwnerType,OwnerInstance,PlayfieldId,PositionX,PositionY,PositionZ,Unknown7,Template,Mesh,BuyModifier,SellModifier,StatsCount");
            this.systemMessagesLog = CreateWriter(Path.Combine(this.sessionDirectory, "system-messages.log"));
            this.chatDialogueLog = CreateWriter(Path.Combine(this.sessionDirectory, "chat-dialogue.log"));
            this.npcInteractionsLog = CreateWriter(Path.Combine(this.sessionDirectory, "npc-interactions.log"));
            this.inventoryUpdatesLog = CreateWriter(Path.Combine(this.sessionDirectory, "inventory-updates.csv"));
            this.inventoryUpdatesLog.WriteLine("CapturedUtc,Direction,Sequence,InventoryIdentity,Handle,Slot,Placement,Flags,Count,ItemIdentity,LowId,HighId,Quality,Unknown");
            this.corpseLootObservationsLog = CreateWriter(Path.Combine(this.sessionDirectory, "corpse-loot-observations.csv"));
            this.corpseLootObservationsLog.WriteLine("CapturedUtc,Direction,Sequence,CorpseIdentity,OpenOrdinal,InitialSnapshot,ItemCount,DeadNpcIdentity,EnemyName,MonsterData,EnemyLevel,CorpseCredits,PlayerIdentity,PlayerLevel,PlayfieldId,Items,CorrelationStatus");
            this.enemyStateLog = CreateWriter(Path.Combine(this.sessionDirectory, "enemy-state.csv"));
            this.enemyStateLog.WriteLine("timestamp,direction,sequence,messageType,evidenceSource,entityId,level,currentHealth,maxHealth,x,y,z,eventType");
            this.enemyFullUpdatesLog = CreateWriter(Path.Combine(this.sessionDirectory, "enemy-full-updates.csv"));
            this.enemyFullUpdatesLog.WriteLine("CapturedUtc,Direction,Sequence,Identity,Name,PlayfieldId,PositionX,PositionY,PositionZ,HeadingX,HeadingY,HeadingZ,HeadingW,FightingTargetRole,FightingTargetIdentity,Version,Flags,CharacterFlags,AccountFlags,Expansions,CharacterInfoType,NPCFamily,LosHeight,NpcUnknownData,NpcUnknownData2,NpcUnknownData3,Level,Health,HealthDamage,MonsterData,MonsterScale,VisualFlags,VisibleTitle,Unknown1Length,Unknown1Hex,HeadMesh,RunSpeedBase,ActiveNanoCount,ActiveNanos,WaypointCount,WaypointOwner,Waypoints,TextureOverrideCount,TextureOverrides,TextureCount,Textures,MeshCount,Meshes,Flags2,Unknown2,Unknown4,DecodeFullyConsumed,UndecodedTailHex,RawBodyHex,Detail");
            this.enemyCombatLog = CreateWriter(Path.Combine(this.sessionDirectory, "enemy-combat.csv"));
            this.enemyCombatLog.WriteLine("CapturedUtc,Direction,Sequence,MessageType,SourceRole,SourceIdentity,TargetRole,TargetIdentity,AuxRole1,AuxIdentity1,AuxRole2,AuxIdentity2,Action,Amount,TargetHp,Unknown1,Unknown2,Unknown3,Unknown4,Unknown5,Unknown6,Detail");
            this.enemyMovementLog = CreateWriter(Path.Combine(this.sessionDirectory, "enemy-movement.csv"));
            this.enemyMovementLog.WriteLine("CapturedUtc,Direction,Sequence,MessageType,IdentityRole,Identity,MoveType,PositionX,PositionY,PositionZ,HeadingX,HeadingY,HeadingZ,HeadingW,Unknown1,Unknown2,Unknown3,Detail");
            this.movementPacketsLog = CreateWriter(Path.Combine(this.sessionDirectory, "movement-packets.csv"));
            this.movementPacketsLog.WriteLine("CapturedUtc,Direction,Sequence,MessageType,SourceType,SourceInstance,SourceIdentity,SourceName,TargetType,TargetInstance,TargetIdentity,TargetName,FollowKind,CurrentX,CurrentY,CurrentZ,DestinationX,DestinationY,DestinationZ,Speed,Animation,Flags,PathCount,RawParams,RawTailHex");
            this.enemyStatUpdatesLog = CreateWriter(Path.Combine(this.sessionDirectory, "enemy-stat-updates.csv"));
            this.enemyStatUpdatesLog.WriteLine("CapturedUtc,Direction,Sequence,MessageType,IdentityRole,Identity,Stat,StatId,Value,PositionX,PositionY,PositionZ,StatsCount,Detail");
            this.enemyFightEventsLog = CreateWriter(Path.Combine(this.sessionDirectory, "enemy-fight-events.log"));
            this.corpseFullUpdatesLog = CreateWriter(Path.Combine(this.sessionDirectory, "corpse-full-updates.csv"));
            this.corpseFullUpdatesLog.WriteLine("CapturedUtc,Direction,Sequence,ReceiverInstance,CorpseType,CorpseInstance,CorpseIdentity,CorpseName,PlayfieldId,PositionX,PositionY,PositionZ,MonsterScale,Sex,Breed,Race,DeadNpcType,DeadNpcInstance,DeadNpcIdentity,DeadNpcName,CorpseCatMesh,CorpseCredits,CorpseMonsterData,TailDeadNpcType,TailDeadNpcInstance,TailDeadNpcIdentity,PacketLength,RawHex");
            this.npcLifecycleLog = CreateWriter(Path.Combine(this.sessionDirectory, "npc-lifecycle.csv"));
            this.npcLifecycleLog.WriteLine("CapturedUtc,Direction,Sequence,Phase,MessageType,PrimaryIdentity,RelatedIdentity,Name,Detail");
            // Native surface loading/raycast/door probes are isolated to the
            // explicit MinimalPf127Capture workflow. They are not required for
            // gameplay evidence and cannot safely run in a comprehensive packet
            // capture callback inside the live client.
            this.pf127GeometryCapture = null;
            this.missionFlowCapture?.BindSession(this.sessionDirectory);
            this.WriteEnemyStateJson();
            this.WriteEnemyDossierJson();
            this.WriteMovementSummaryJson();
                if (activate)
                {
                    this.ActivateCaptureSession();
                }
            }
        }

        private void ActivateCaptureSession()
        {
            Interlocked.Exchange(
                ref this.rawCaptureGateState,
                RawCaptureGateOpen);

            this.captureStartUtc = DateTime.UtcNow;
            this.captureStartLocal = DateTime.Now;
            this.lastPacketUtc = this.captureStartUtc;
            this.WriteCaptureSessionMetadata(this.captureStartUtc, this.captureStartLocal);
            this.WriteCaptureInfo(null, CaptureValidation.Running());
            this.captureClock.Restart();
            if (this.lootCaptureRequested)
            {
                this.LogEvent("CAPTURE-MODE", "loot-10 armed by approved launcher");
            }

            this.enabled = true;
            this.nextFlushUtc = DateTime.UtcNow.AddSeconds(2);
            this.nextSnapshotUtc = DateTime.UtcNow.AddSeconds(1);
            this.nextExternalControlPollUtc = DateTime.UtcNow;
        }

        private void ResetCaptureState()
        {
            this.knownCharacters.Clear();
            this.knownCorpses.Clear();
            this.exportedShopUpdateFingerprints.Clear();
            this.vendorInteractionIdentities.Clear();
            this.shopUpdateIdentities.Clear();
            this.vendorFullUpdateIdentities.Clear();
            this.focusedEnemyIdentities.Clear();
            this.recentEnemyFullUpdates.Clear();
            this.enemyStates.Clear();
            this.enemyStateTimeline.Clear();
            this.corpseEvidenceByDeadNpc.Clear();
            this.activeCorpseEvidenceByCorpse.Clear();
            this.corpseInventorySnapshotCounts.Clear();
            this.corpseLootInitialEnemyKeys.Clear();

            this.captureFinalized = false;
            this.captureStopDrainRequested = false;
            this.captureStopRequestedUtc = null;
            this.captureStopQuietDeadlineUtc = null;
            this.captureStopMaximumDeadlineUtc = null;
            this.captureFinalizedUtc = null;
            this.captureQuietPeriodPassed = false;
            this.inboundPacketCount = 0;
            this.outboundPacketCount = 0;
            this.decodedInboundCount = 0;
            this.decodedOutboundCount = 0;
            this.decodedN3EventRowCount = 0;
            this.n3CaptureStageErrorCount = 0;
            this.rawCombatPacketCount = 0;
            this.rawSimpleCharFullUpdatePacketCount = 0;
            this.rawSimpleCharFullUpdateDecodeCount = 0;
            this.rawSimpleCharFullUpdateDecodeErrorCount = 0;
            this.rawSimpleCharFullUpdateIncompleteDecodeCount = 0;
            this.rawNpcSimpleCharFullUpdateCount = 0;
            this.scfuAppearanceRowCount = 0;
            this.rawPacketLogRowCount = 0;
            this.rawPacketIndexRowCount = 0;
            this.rawPacketPreservedCount = 0;
            this.rawPacketWriteErrorCount = 0;
            this.rawPacketCallbackDrainTimeoutCount = 0;
            this.rawPacketProjectionErrorCount = 0;
            this.rawPacketGlobalOrdinal = 0;
            this.captureClock.Reset();
            this.shopUpdateMessageCount = 0;
            this.shopUpdateRowCount = 0;
            this.vendorFullUpdateMessageCount = 0;
            this.systemMessageCount = 0;
            this.chatDialogueMessageCount = 0;
            this.npcInteractionCount = 0;
            this.inventoryUpdateMessageCount = 0;
            this.inventoryUpdateRowCount = 0;
            this.corpseLootObservationRowCount = 0;
            this.corpseLootInitialSnapshotCount = 0;
            this.corpseLootUnlinkedSnapshotCount = 0;
            this.corpseLootMissingPlayerContextCount = 0;
            this.vendorInteractionAttemptCount = 0;
            this.enemyStateRowCount = 0;
            this.enemyCombatEventCount = 0;
            this.enemyDamageEventCount = 0;
            this.enemyDeathEventCount = 0;
            this.enemySpawnEventCount = 0;
            this.enemyDespawnEventCount = 0;
            this.enemyHealthUpdateCount = 0;
            this.enemyPositionUpdateCount = 0;
            this.enemyFullUpdateRowCount = 0;
            this.enemyCombatRowCount = 0;
            this.enemyMovementRowCount = 0;
            this.movementPacketRowCount = 0;
            this.movementFollowTargetPacketCount = 0;
            this.movementUsableFollowTargetPacketCount = 0;
            this.movementSetPosPacketCount = 0;
            this.movementStopMovingCmdPacketCount = 0;
            this.movementDecodeErrorCount = 0;
            this.enemyStatUpdateRowCount = 0;
            this.corpseFullUpdatePacketCount = 0;
            this.corpseFullUpdateRowCount = 0;
            this.corpseFullUpdateDecodeErrorCount = 0;
            this.corpseInventoryUpdateCount = 0;
            this.corpseSeenEventCount = 0;
            this.corpseGoneEventCount = 0;
            this.npcLifecycleRowCount = 0;
            this.localEnemyCombatContextUntilUtc = default(DateTime);
            this.lastPlayfieldId = string.Empty;
            this.lastCapturePlayfieldIdentity = string.Empty;
            this.enemyFightCaptureEnabled = false;
            this.enemyFightCaptureStarted = false;
            this.respawnCaptureRequested = false;
            this.lootCaptureRequested = false;
        }

        private void ApplyExternalCaptureRequest(string pluginDir)
        {
            if (string.IsNullOrWhiteSpace(pluginDir))
            {
                return;
            }

            string requestPath = Path.Combine(pluginDir, LootCaptureRequestFileName);
            if (!File.Exists(requestPath))
            {
                return;
            }

            this.lootCaptureRequested = true;
            try
            {
                File.Delete(requestPath);
            }
            catch
            {
                // The capture is still armed. A stale marker is harmless and can be
                // removed by the next approved launcher invocation.
            }
        }

        private void Flush()
        {
            this.callbackBoundary.Dispatch("Capture.Flush", this.FlushCore);
        }

        private void FlushCore()
        {
            lock (this.syncRoot)
            {
                this.FlushWriterNoThrow(this.packetsLog, true);
                this.FlushWriterNoThrow(this.rawPacketsCsvLog, true);
                this.FlushWriterNoThrow(this.eventsLog, false);
                this.FlushWriterNoThrow(this.scfuAppearanceLog, false);
                this.FlushWriterNoThrow(this.shopUpdatesLog, false);
                this.FlushWriterNoThrow(this.vendorFullUpdatesLog, false);
                this.FlushWriterNoThrow(this.systemMessagesLog, false);
                this.FlushWriterNoThrow(this.chatDialogueLog, false);
                this.FlushWriterNoThrow(this.npcInteractionsLog, false);
                this.FlushWriterNoThrow(this.inventoryUpdatesLog, false);
                this.FlushWriterNoThrow(this.corpseLootObservationsLog, false);
                this.FlushWriterNoThrow(this.enemyStateLog, false);
                this.FlushWriterNoThrow(this.enemyFullUpdatesLog, false);
                this.FlushWriterNoThrow(this.enemyCombatLog, false);
                this.FlushWriterNoThrow(this.enemyMovementLog, false);
                this.FlushWriterNoThrow(this.movementPacketsLog, false);
                this.FlushWriterNoThrow(this.enemyStatUpdatesLog, false);
                this.FlushWriterNoThrow(this.enemyFightEventsLog, false);
                this.FlushWriterNoThrow(this.corpseFullUpdatesLog, false);
                this.FlushWriterNoThrow(this.npcLifecycleLog, false);
                this.pf127GeometryCapture?.Flush();
            }
        }

        private void FlushAndCloseRawWritersNoThrow()
        {
            this.packetsLog = this.CloseWriterNoThrow(this.packetsLog, true);
            this.rawPacketsCsvLog = this.CloseWriterNoThrow(this.rawPacketsCsvLog, true);
        }

        private void FlushAndClose()
        {
            this.callbackBoundary.Dispatch("Capture.FlushAndClose", this.FlushAndCloseCore);
        }

        private void FlushAndCloseCore()
        {
            lock (this.syncRoot)
            {
                this.FlushAndCloseRawWritersNoThrow();
                this.eventsLog = this.CloseWriterNoThrow(this.eventsLog, false);
                this.scfuAppearanceLog = this.CloseWriterNoThrow(this.scfuAppearanceLog, false);
                this.shopUpdatesLog = this.CloseWriterNoThrow(this.shopUpdatesLog, false);
                this.vendorFullUpdatesLog = this.CloseWriterNoThrow(this.vendorFullUpdatesLog, false);
                this.systemMessagesLog = this.CloseWriterNoThrow(this.systemMessagesLog, false);
                this.chatDialogueLog = this.CloseWriterNoThrow(this.chatDialogueLog, false);
                this.npcInteractionsLog = this.CloseWriterNoThrow(this.npcInteractionsLog, false);
                this.inventoryUpdatesLog = this.CloseWriterNoThrow(this.inventoryUpdatesLog, false);
                this.corpseLootObservationsLog = this.CloseWriterNoThrow(this.corpseLootObservationsLog, false);
                this.enemyStateLog = this.CloseWriterNoThrow(this.enemyStateLog, false);
                this.enemyFullUpdatesLog = this.CloseWriterNoThrow(this.enemyFullUpdatesLog, false);
                this.enemyCombatLog = this.CloseWriterNoThrow(this.enemyCombatLog, false);
                this.enemyMovementLog = this.CloseWriterNoThrow(this.enemyMovementLog, false);
                this.movementPacketsLog = this.CloseWriterNoThrow(this.movementPacketsLog, false);
                this.enemyStatUpdatesLog = this.CloseWriterNoThrow(this.enemyStatUpdatesLog, false);
                this.enemyFightEventsLog = this.CloseWriterNoThrow(this.enemyFightEventsLog, false);
                this.corpseFullUpdatesLog = this.CloseWriterNoThrow(this.corpseFullUpdatesLog, false);
                this.npcLifecycleLog = this.CloseWriterNoThrow(this.npcLifecycleLog, false);
                this.pf127GeometryCapture?.Dispose();
                this.pf127GeometryCapture = null;
            }
        }

        private void FlushWriterNoThrow(StreamWriter writer, bool rawSink)
        {
            if (writer == null)
            {
                return;
            }

            try
            {
                writer.Flush();
            }
            catch
            {
                if (rawSink)
                {
                    this.rawPacketWriteErrorCount++;
                }
                else
                {
                    this.rawPacketProjectionErrorCount++;
                }
            }
        }

        private StreamWriter CloseWriterNoThrow(StreamWriter writer, bool rawSink)
        {
            if (writer == null)
            {
                return null;
            }

            this.FlushWriterNoThrow(writer, rawSink);
            try
            {
                writer.Dispose();
            }
            catch
            {
                if (rawSink)
                {
                    this.rawPacketWriteErrorCount++;
                }
                else
                {
                    this.rawPacketProjectionErrorCount++;
                }
            }

            return null;
        }

        private static StreamWriter CreateWriter(string path, bool autoFlush = false)
        {
            return new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
            {
                AutoFlush = autoFlush
            };
        }

        private static string CreateSessionDirectory(string pluginDir)
        {
            string baseDirectory = string.IsNullOrWhiteSpace(pluginDir) ? Directory.GetCurrentDirectory() : pluginDir;
            string capturesDirectory = Path.Combine(baseDirectory, "captures");
            string stem = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string directory = Path.Combine(capturesDirectory, stem);
            int suffix = 1;
            while (Directory.Exists(directory))
            {
                directory = Path.Combine(
                    capturesDirectory,
                    stem + "-" + suffix.ToString("00", CultureInfo.InvariantCulture));
                suffix++;
            }

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

        private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24)
                | ((uint)bytes[offset + 1] << 16)
                | ((uint)bytes[offset + 2] << 8)
                | bytes[offset + 3];
        }

        private static int ReadInt32BigEndian(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24)
                | (bytes[offset + 1] << 16)
                | (bytes[offset + 2] << 8)
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
    }
}
