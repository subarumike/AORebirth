namespace ZoneEngine.Core.Arete.Dialogue
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Arete;
    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;

    #endregion

    public static class ContentDrivenNpcDialogueRouter
    {
        public const string RexLarssonGateEnvironmentVariableName =
            "AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING";

        private const int AreteLandingPlayfieldId = 6553;

        private const int RexLarssonInstance = unchecked((int)0x782DE568);

        private const int MarcusStoneInstance = unchecked((int)0x782DE567);

        private const string RexLarssonNpcIdentity = "SimpleChar:782DE568";

        private const string MarcusStoneNpcIdentity = "SimpleChar:782DE567";

        private const string RexB18EReturnNodeId = "rex_194454_006";

        private const string RexManifestRelativePath =
            @"Server\ZoneEngine\Content\Arete\rex-larsson\manifest.json";

        private const string MarcusManifestRelativePath =
            @"Server\ZoneEngine\Content\Arete\marcus-stone\manifest.json";

        private const int KnuBotPacketPacingMilliseconds = 20;

        private static readonly ContentDrivenNpcDialogueRegistration RexLarssonRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Rex Larsson",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = RexLarssonInstance
                    },
                NpcIdentityText = RexLarssonNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                ManifestRelativePath = RexManifestRelativePath,
                LogPrefix = "ARETE_REX_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration MarcusStoneRegistration =
            new ContentDrivenNpcDialogueRegistration
            {
                Name = "Marcus Stone",
                NpcIdentity =
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = MarcusStoneInstance
                    },
                NpcIdentityText = MarcusStoneNpcIdentity,
                PlayfieldId = AreteLandingPlayfieldId,
                GateEnvironmentVariableName = RexLarssonGateEnvironmentVariableName,
                ManifestRelativePath = MarcusManifestRelativePath,
                LogPrefix = "ARETE_MARCUS_DIALOGUE"
            };

        private static readonly ContentDrivenNpcDialogueRegistration[] Registrations =
        {
            RexLarssonRegistration,
            MarcusStoneRegistration
        };

        private static readonly Dictionary<string, DialogueSessionRecord> SessionsByCharacter =
            new Dictionary<string, DialogueSessionRecord>(StringComparer.OrdinalIgnoreCase);

        private static readonly object SyncRoot = new object();

        public static bool IsRexLarssonRoutingEnabled
        {
            get
            {
                return IsRegistrationEnabled(RexLarssonRegistration);
            }
        }

        public static bool TryStartDialogue(ICharacter npc, Identity sourceIdentity)
        {
            ContentDrivenNpcDialogueRegistration registration = FindRegistration(npc);
            if (registration == null)
            {
                return false;
            }

            if (!IsRegistrationEnabled(registration))
            {
                return false;
            }

            if (!IsExpectedPlayfield(npc, registration))
            {
                LogSkipped(
                    registration,
                    "routing skipped because NPC is not in expected playfield "
                    + registration.PlayfieldId + ".");
                return false;
            }

            ICharacter source = ResolveCharacter(npc, sourceIdentity);
            if (source == null)
            {
                LogSkipped(registration, "routing skipped because source character was not found.");
                return false;
            }

            if (!IsExpectedPlayfield(source, registration))
            {
                LogSkipped(
                    registration,
                    "routing skipped because source character is not in expected playfield "
                    + registration.PlayfieldId + ".");
                return false;
            }

            return TryStartDialogueForSource(source, npc, registration);
        }

        public static bool TryStartDialogueForTarget(ICharacter source, Identity targetIdentity)
        {
            ContentDrivenNpcDialogueRegistration registration = FindRegistration(targetIdentity);
            if (registration == null)
            {
                return false;
            }

            if (!IsRegistrationEnabled(registration))
            {
                LogSkipped(
                    registration,
                    "direct trade routing skipped because "
                    + registration.GateEnvironmentVariableName + " is not enabled.");
                return false;
            }

            if (source == null)
            {
                return false;
            }

            if (!IsExpectedPlayfield(source, registration))
            {
                return false;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, targetIdentity);
            if (!IsRegisteredNpc(npc, registration) || !IsExpectedPlayfield(npc, registration))
            {
                LogSkipped(
                    registration,
                    "direct trade routing skipped because registered target was not found in expected playfield.");
                return false;
            }

            return TryStartDialogueForSource(source, npc, registration);
        }

        public static bool ShouldSuppressCombat(ICharacter target)
        {
            ContentDrivenNpcDialogueRegistration registration = FindRegistration(target);
            return registration != null
                   && IsRegistrationEnabled(registration)
                   && IsExpectedPlayfield(target, registration);
        }

        public static bool TryHandleAnswer(ICharacter source, Identity targetIdentity, int answerIndex)
        {
            if (source == null)
            {
                return false;
            }

            ContentDrivenNpcDialogueRegistration registration = FindRegistration(targetIdentity);
            if (registration == null)
            {
                registration = FindActiveSessionRegistration(source);
                if (registration == null)
                {
                    return false;
                }
            }

            if (!IsRegistrationEnabled(registration) || !IsExpectedPlayfield(source, registration))
            {
                return false;
            }

            bool isRegisteredTarget = IsRegisteredIdentity(targetIdentity, registration);
            if (!isRegisteredTarget && !HasActiveSession(source, registration))
            {
                return false;
            }

            DialogueSessionService service;
            if (!TryGetSessionService(registration, out service))
            {
                return false;
            }

            string sessionKey = CreateSessionKey(source.Identity, registration);
            DialogueSession session;
            lock (SyncRoot)
            {
                DialogueSessionRecord record;
                SessionsByCharacter.TryGetValue(sessionKey, out record);
                session = record == null ? null : record.Session;
            }

            if (session == null)
            {
                LogDialogue(
                    registration,
                    "answer ignored because no routed session exists for character="
                    + source.Identity.ToString(true)
                    + " target=" + targetIdentity.ToString(true)
                    + " answer=" + answerIndex);
                return false;
            }

            string previousNodeId = session.CurrentNodeId;
            LogDialogue(
                registration,
                "answer received character=" + source.Identity.ToString(true)
                + " target=" + targetIdentity.ToString(true)
                + " answer=" + answerIndex
                + " node=" + previousNodeId);

            DialogueSessionResult result = service.SelectOption(session, answerIndex);
            if (!result.IsValid)
            {
                LogValidation(registration, "dialogue option failed", result.Validation);
                CloseSession(source, sessionKey, registration, true);
                return true;
            }

            LogRecordedActions(source, result, registration);
            Action emitQuestPreviewAfterPrompt = delegate
            {
                LogQuestPreviewResult(
                    TryHandleDialogueSideEffect(
                        source,
                        registration,
                        previousNodeId,
                        answerIndex),
                    registration);
            };

            if (result.Session == null || !result.Session.IsActive)
            {
                LogDialogue(
                    registration,
                    "answer closed session character=" + source.Identity.ToString(true)
                    + " previousNode=" + previousNodeId
                    + " answer=" + answerIndex);
                CloseSession(source, sessionKey, registration, true);
                return true;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter[sessionKey] =
                    new DialogueSessionRecord { Registration = registration, Session = result.Session };
            }

            LogDialogue(
                registration,
                "answer advanced character=" + source.Identity.ToString(true)
                + " from=" + previousNodeId
                + " to=" + result.Session.CurrentNodeId
                + " answer=" + answerIndex);
            SendDialogueNode(source, result, registration, emitQuestPreviewAfterPrompt);
            return true;
        }

        public static bool TryHandleClose(ICharacter source, Identity targetIdentity)
        {
            if (source == null)
            {
                return false;
            }

            ContentDrivenNpcDialogueRegistration registration = FindRegistration(targetIdentity);
            if (registration == null)
            {
                registration = FindActiveSessionRegistration(source);
                if (registration == null)
                {
                    return false;
                }
            }

            if (!IsRegistrationEnabled(registration) || !IsExpectedPlayfield(source, registration))
            {
                return false;
            }

            if (!IsRegisteredIdentity(targetIdentity, registration) && !HasActiveSession(source, registration))
            {
                return false;
            }

            string sessionKey = CreateSessionKey(source.Identity, registration);
            bool hadSession;
            lock (SyncRoot)
            {
                hadSession = SessionsByCharacter.Remove(sessionKey);
            }

            if (hadSession)
            {
                LogDialogue(registration, "session closed by client character=" + source.Identity.ToString(true));
                return true;
            }

            return false;
        }

        private static bool TryStartDialogueForSource(
            ICharacter source,
            ICharacter npc,
            ContentDrivenNpcDialogueRegistration registration)
        {
            DialogueSessionService service;
            if (!TryGetSessionService(registration, out service))
            {
                return false;
            }

            string requestedStartNodeId = ResolveRequestedStartNodeId(source, registration);
            DialogueSessionResult result = string.IsNullOrWhiteSpace(requestedStartNodeId)
                                               ? service.StartSession(registration.NpcIdentityText)
                                               : service.StartSessionAtNode(
                                                   registration.NpcIdentityText,
                                                   requestedStartNodeId);
            if (!result.IsValid || result.Session == null)
            {
                LogValidation(registration, "dialogue start failed", result.Validation);
                if (!string.IsNullOrWhiteSpace(requestedStartNodeId))
                {
                    KnuBotOpenChatWindowMessageHandler.Default.Send(source, registration.NpcIdentity);
                    PaceKnuBotPackets();
                    KnuBotCloseChatWindowMessageHandler.Default.Send(source, registration.NpcIdentity);
                    LogDialogue(
                        registration,
                        "return-state start node unavailable; closed safely character="
                        + source.Identity.ToString(true)
                        + " requestedNode=" + requestedStartNodeId
                        + " chainState=" + DescribeChainState(source, registration));
                    return true;
                }

                return false;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter[CreateSessionKey(source.Identity, registration)] =
                    new DialogueSessionRecord { Registration = registration, Session = result.Session };
            }

            FaceNpcTowardSource(npc, source);
            KnuBotOpenChatWindowMessageHandler.Default.Send(source, registration.NpcIdentity);
            PaceKnuBotPackets();

            if (string.Equals(requestedStartNodeId, RexB18EReturnNodeId, StringComparison.OrdinalIgnoreCase))
            {
                LogB18ECompletionResult(
                    RexB18ECompletionHandler.TryCompleteOnReturn(
                        source,
                        registration.NpcIdentity,
                        IsRegistrationEnabled(registration)),
                    registration);
                PaceKnuBotPackets();
            }

            SendDialogueNode(source, result, registration);

            LogDialogue(
                registration,
                "started character=" + source.Identity.ToString(true)
                + " node=" + result.Session.CurrentNodeId
                + " requestedStartNode=" + (string.IsNullOrWhiteSpace(requestedStartNodeId)
                                                ? "<default>"
                                                : requestedStartNodeId)
                + " chainState=" + DescribeChainState(source, registration));

            return true;
        }

        private static RexQuestPreviewEmissionResult TryHandleDialogueSideEffect(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration,
            string previousNodeId,
            int answerIndex)
        {
            if (registration == RexLarssonRegistration)
            {
                return RexQuestPreviewEmitter.TryEmitB18CPreview(
                    source,
                    registration.NpcIdentity,
                    previousNodeId,
                    answerIndex,
                    IsRegistrationEnabled(registration));
            }

            return RexQuestPreviewEmissionResult.NotApplicable();
        }

        private static string ResolveRequestedStartNodeId(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (registration != RexLarssonRegistration)
            {
                return null;
            }

            RexMissionChainState chainState = RexMissionChainStateStore.GetState(source);
            if (chainState >= RexMissionChainState.B18EPreviewed)
            {
                return RexB18EReturnNodeId;
            }

            return null;
        }

        private static string DescribeChainState(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (registration == RexLarssonRegistration)
            {
                return RexMissionChainStateStore.GetState(source).ToString();
            }

            return "<none>";
        }

        private static void FaceNpcTowardSource(ICharacter npc, ICharacter source)
        {
            NPCController controller = npc == null ? null : npc.Controller as NPCController;
            if (controller != null)
            {
                controller.FaceDialoguePartner(source);
            }
        }

        private static bool HasActiveSession(
            ICharacter source,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (source == null || registration == null)
            {
                return false;
            }

            DialogueSessionRecord record;
            lock (SyncRoot)
            {
                SessionsByCharacter.TryGetValue(CreateSessionKey(source.Identity, registration), out record);
            }

            return record != null && record.Session != null && record.Session.IsActive;
        }

        private static ContentDrivenNpcDialogueRegistration FindActiveSessionRegistration(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            foreach (ContentDrivenNpcDialogueRegistration registration in Registrations)
            {
                if (HasActiveSession(source, registration))
                {
                    return registration;
                }
            }

            return null;
        }

        private static bool TryGetSessionService(
            ContentDrivenNpcDialogueRegistration registration,
            out DialogueSessionService service)
        {
            lock (SyncRoot)
            {
                if (!registration.LoadAttempted)
                {
                    registration.LoadAttempted = true;
                    registration.LoadSucceeded = TryLoadContent(registration);
                }

                service = registration.DialogueSessionService;
                return registration.LoadSucceeded && service != null;
            }
        }

        private static bool TryLoadContent(ContentDrivenNpcDialogueRegistration registration)
        {
            string manifestPath = ResolveManifestPath(registration);
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                LogSkipped(registration, "routing disabled because content manifest was not found.");
                return false;
            }

            AreteValidationResult aggregateValidation =
                new AreteAggregateContentValidator().ValidateManifest(manifestPath);
            if (!aggregateValidation.IsValid)
            {
                LogValidation(registration, "aggregate validation failed", aggregateValidation);
                return false;
            }

            var registry = new DialogueContentRegistry();
            AreteValidationResult dialogueValidation = registry.LoadFromManifest(manifestPath);
            if (!dialogueValidation.IsValid)
            {
                LogValidation(registration, "dialogue manifest load failed", dialogueValidation);
                return false;
            }

            registration.DialogueRegistry = registry;
            registration.DialogueSessionService = new DialogueSessionService(registry);
            registration.LoadedManifestPath = manifestPath;

            LogUtil.Debug(
                DebugInfoDetail.KnuBot,
                "Content-driven NPC dialogue loaded for " + registration.Name
                + " from " + registration.LoadedManifestPath);

            return true;
        }

        private static void SendDialogueNode(
            ICharacter source,
            DialogueSessionResult result,
            ContentDrivenNpcDialogueRegistration registration,
            Action afterPromptBeforeOptions = null)
        {
            if (result.CurrentNode != null && !string.IsNullOrWhiteSpace(result.CurrentNode.PromptText))
            {
                KnuBotAppendTextMessageHandler.Default.Send(
                    source,
                    registration.NpcIdentity,
                    result.CurrentNode.PromptText);
                PaceKnuBotPackets();
            }

            if (afterPromptBeforeOptions != null)
            {
                afterPromptBeforeOptions();
                PaceKnuBotPackets();
            }

            string[] choices = result.AvailableOptions
                .OrderBy(option => option.Index)
                .Select(option => option.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray();

            if (choices.Length == 0)
            {
                KnuBotCloseChatWindowMessageHandler.Default.Send(source, registration.NpcIdentity);
                return;
            }

            KnuBotAnswerListMessageHandler.Default.Send(source, registration.NpcIdentity, choices);
            LogDialogue(
                registration,
                "sent node=" + (result.CurrentNode == null ? "<none>" : result.CurrentNode.Id)
                + " options=" + choices.Length
                + " character=" + source.Identity.ToString(true));
        }

        private static void CloseSession(
            ICharacter source,
            string sessionKey,
            ContentDrivenNpcDialogueRegistration registration,
            bool sendClose)
        {
            lock (SyncRoot)
            {
                SessionsByCharacter.Remove(sessionKey);
            }

            if (sendClose)
            {
                KnuBotCloseChatWindowMessageHandler.Default.Send(source, registration.NpcIdentity);
            }

            LogDialogue(registration, "session ended character=" + source.Identity.ToString(true));
        }

        private static void LogRecordedActions(
            ICharacter source,
            DialogueSessionResult result,
            ContentDrivenNpcDialogueRegistration registration)
        {
            int actionCount = result.RecordedActions == null ? 0 : result.RecordedActions.Count;
            if (actionCount == 0)
            {
                return;
            }

            LogDialogue(
                registration,
                "recorded " + actionCount
                + " no-op action(s) for character=" + source.Identity.ToString(true));
        }

        private static void LogQuestPreviewResult(
            RexQuestPreviewEmissionResult result,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (result == null || !result.IsApplicable || string.IsNullOrWhiteSpace(result.Message))
            {
                return;
            }

            LogDialogue(registration, result.Message);
        }

        private static void LogB18ECompletionResult(
            RexB18ECompletionResult result,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (result == null || !result.IsApplicable || string.IsNullOrWhiteSpace(result.Message))
            {
                return;
            }

            LogDialogue(registration, result.Message);
        }

        private static void PaceKnuBotPackets()
        {
            Thread.Sleep(KnuBotPacketPacingMilliseconds);
        }

        private static void LogDialogue(ContentDrivenNpcDialogueRegistration registration, string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, registration.LogPrefix + " " + message);
        }

        private static void LogSkipped(ContentDrivenNpcDialogueRegistration registration, string message)
        {
            LogUtil.Debug(
                DebugInfoDetail.KnuBot,
                "Content-driven NPC dialogue for " + registration.Name + " " + message);
        }

        private static void LogValidation(
            ContentDrivenNpcDialogueRegistration registration,
            string prefix,
            AreteValidationResult validation)
        {
            if (validation == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.KnuBot,
                    "Content-driven NPC dialogue for " + registration.Name
                    + " " + prefix + ": validation result was missing.");
                return;
            }

            foreach (string error in validation.Errors)
            {
                LogUtil.Debug(
                    DebugInfoDetail.KnuBot,
                    "Content-driven NPC dialogue for " + registration.Name
                    + " " + prefix + ": " + error);
            }
        }

        private static ICharacter ResolveCharacter(ICharacter npc, Identity sourceIdentity)
        {
            if (npc == null || npc.Playfield == null)
            {
                return null;
            }

            return Pool.Instance.GetObject<ICharacter>(npc.Playfield.Identity, sourceIdentity);
        }

        private static ContentDrivenNpcDialogueRegistration FindRegistration(ICharacter npc)
        {
            return npc == null ? null : FindRegistration(npc.Identity);
        }

        private static ContentDrivenNpcDialogueRegistration FindRegistration(Identity identity)
        {
            foreach (ContentDrivenNpcDialogueRegistration registration in Registrations)
            {
                if (IsRegisteredIdentity(identity, registration))
                {
                    return registration;
                }
            }

            return null;
        }

        private static bool IsRegisteredNpc(
            ICharacter npc,
            ContentDrivenNpcDialogueRegistration registration)
        {
            return npc != null && IsRegisteredIdentity(npc.Identity, registration);
        }

        private static bool IsRegisteredIdentity(
            Identity identity,
            ContentDrivenNpcDialogueRegistration registration)
        {
            return registration != null
                   && identity.Type == registration.NpcIdentity.Type
                   && identity.Instance == registration.NpcIdentity.Instance;
        }

        private static bool IsExpectedPlayfield(
            ICharacter character,
            ContentDrivenNpcDialogueRegistration registration)
        {
            if (registration == null || !registration.PlayfieldId.HasValue)
            {
                return true;
            }

            return character != null
                   && character.Playfield != null
                   && character.Playfield.Identity.Instance == registration.PlayfieldId.Value;
        }

        private static string CreateSessionKey(
            Identity characterIdentity,
            ContentDrivenNpcDialogueRegistration registration)
        {
            return characterIdentity.Type + ":" + characterIdentity.Instance + "|" + registration.NpcIdentityText;
        }

        private static string ResolveManifestPath(ContentDrivenNpcDialogueRegistration registration)
        {
            foreach (string candidate in GetManifestPathCandidates(registration))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetManifestPathCandidates(
            ContentDrivenNpcDialogueRegistration registration)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string currentDirectory = Directory.GetCurrentDirectory();

            yield return Path.GetFullPath(
                Path.Combine(baseDirectory, @"..\..\", registration.ManifestRelativePath));
            yield return Path.GetFullPath(
                Path.Combine(currentDirectory, @"AORebirth\", registration.ManifestRelativePath));
            yield return Path.GetFullPath(
                Path.Combine(currentDirectory, registration.ManifestRelativePath));
        }

        private static bool IsRegistrationEnabled(ContentDrivenNpcDialogueRegistration registration)
        {
            return registration != null
                   && AreteEnvironmentGate.IsDefaultEnabled(registration.GateEnvironmentVariableName);
        }

        private sealed class ContentDrivenNpcDialogueRegistration
        {
            public string Name { get; set; }

            public Identity NpcIdentity { get; set; }

            public string NpcIdentityText { get; set; }

            public int? PlayfieldId { get; set; }

            public string GateEnvironmentVariableName { get; set; }

            public string ManifestRelativePath { get; set; }

            public string LogPrefix { get; set; }

            public bool LoadAttempted { get; set; }

            public bool LoadSucceeded { get; set; }

            public string LoadedManifestPath { get; set; }

            public DialogueContentRegistry DialogueRegistry { get; set; }

            public DialogueSessionService DialogueSessionService { get; set; }
        }

        private sealed class DialogueSessionRecord
        {
            public ContentDrivenNpcDialogueRegistration Registration { get; set; }

            public DialogueSession Session { get; set; }
        }
    }
}
