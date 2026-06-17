namespace ZoneEngine.Core.Arete.Dialogue
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;

    #endregion

    public static class AreteRexDialogueRouter
    {
        public const string EnableEnvironmentVariableName = "AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING";

        private const int AreteLandingPlayfieldId = 6553;

        private const int RexLarssonInstance = unchecked((int)0x782DE568);

        private const string RexLarssonNpcIdentity = "SimpleChar:782DE568";

        private const string RexManifestRelativePath =
            @"Server\ZoneEngine\Content\Arete\rex-larsson\manifest.json";

        private static readonly Identity RexLarssonIdentity =
            new Identity { Type = IdentityType.CanbeAffected, Instance = RexLarssonInstance };

        private static readonly Dictionary<string, DialogueSession> SessionsByCharacter =
            new Dictionary<string, DialogueSession>(StringComparer.OrdinalIgnoreCase);

        private static readonly object SyncRoot = new object();

        private static bool loadAttempted;

        private static bool loadSucceeded;

        private static string loadedManifestPath;

        private static DialogueContentRegistry dialogueRegistry;

        private static DialogueSessionService dialogueSessionService;

        public static bool IsEnabled
        {
            get
            {
                return IsTruthy(Environment.GetEnvironmentVariable(EnableEnvironmentVariableName));
            }
        }

        public static bool TryStartDialogue(ICharacter npc, Identity sourceIdentity)
        {
            if (!IsEnabled || !IsRexLarsson(npc))
            {
                return false;
            }

            if (!IsAreteLandingPlayfield(npc))
            {
                LogUtil.Debug(
                    DebugInfoDetail.KnuBot,
                    "Arete Rex routing skipped because Rex is not in Arete Landing playfield 6553.");
                return false;
            }

            ICharacter source = ResolveCharacter(npc, sourceIdentity);
            if (source == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.KnuBot,
                    "Arete Rex routing skipped because source character was not found.");
                return false;
            }

            if (!IsAreteLandingPlayfield(source))
            {
                LogUtil.Debug(
                    DebugInfoDetail.KnuBot,
                    "Arete Rex routing skipped because source character is not in Arete Landing playfield 6553.");
                return false;
            }

            return TryStartDialogueForSource(source, npc);
        }

        public static bool TryStartDialogueForTarget(ICharacter source, Identity targetIdentity)
        {
            if (!IsRexLarsson(targetIdentity))
            {
                return false;
            }

            if (!IsEnabled)
            {
                LogUtil.Debug(
                    DebugInfoDetail.KnuBot,
                    "Arete Rex direct trade routing skipped because "
                    + EnableEnvironmentVariableName + " is not enabled.");
                return false;
            }

            if (source == null)
            {
                return false;
            }

            if (!IsAreteLandingPlayfield(source))
            {
                return false;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, targetIdentity);
            if (!IsRexLarsson(npc) || !IsAreteLandingPlayfield(npc))
            {
                LogUtil.Debug(
                    DebugInfoDetail.KnuBot,
                    "Arete Rex direct trade routing skipped because Rex target was not found in Arete Landing.");
                return false;
            }

            return TryStartDialogueForSource(source, npc);
        }

        public static bool ShouldSuppressCombat(ICharacter target)
        {
            return IsEnabled && IsRexLarsson(target) && IsAreteLandingPlayfield(target);
        }

        private static bool TryStartDialogueForSource(ICharacter source, ICharacter npc)
        {
            DialogueSessionService service;
            if (!TryGetSessionService(out service))
            {
                return false;
            }

            DialogueSessionResult result = service.StartSession(RexLarssonNpcIdentity);
            if (!result.IsValid || result.Session == null)
            {
                LogValidation("Arete Rex dialogue start failed", result.Validation);
                return false;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter[CreateSessionKey(source.Identity)] = result.Session;
            }

            FaceNpcTowardSource(npc, source);
            KnuBotOpenChatWindowMessageHandler.Default.Send(source, RexLarssonIdentity);
            SendDialogueNode(source, result);

            LogUtil.Debug(
                DebugInfoDetail.KnuBot,
                "Arete Rex dialogue routing started for character=" + source.Identity.ToString(true)
                + " node=" + result.Session.CurrentNodeId);

            return true;
        }

        private static void FaceNpcTowardSource(ICharacter npc, ICharacter source)
        {
            NPCController controller = npc == null ? null : npc.Controller as NPCController;
            if (controller != null)
            {
                controller.FaceDialoguePartner(source);
            }
        }

        public static bool TryHandleAnswer(ICharacter source, Identity targetIdentity, int answerIndex)
        {
            if (!IsEnabled)
            {
                return false;
            }

            if (source == null)
            {
                return false;
            }

            if (!IsAreteLandingPlayfield(source))
            {
                return false;
            }

            bool isRexTarget = IsRexLarsson(targetIdentity);
            if (!isRexTarget && !HasActiveSession(source))
            {
                return false;
            }

            DialogueSessionService service;
            if (!TryGetSessionService(out service))
            {
                return false;
            }

            DialogueSession session;
            string sessionKey = CreateSessionKey(source.Identity);
            lock (SyncRoot)
            {
                SessionsByCharacter.TryGetValue(sessionKey, out session);
            }

            if (session == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.KnuBot,
                    "Arete Rex dialogue answer ignored because no routed session exists for character="
                    + source.Identity.ToString(true)
                    + " target=" + targetIdentity.ToString(true)
                    + " answer=" + answerIndex);
                return false;
            }

            LogUtil.Debug(
                DebugInfoDetail.KnuBot,
                "Arete Rex dialogue answer received character=" + source.Identity.ToString(true)
                + " target=" + targetIdentity.ToString(true)
                + " answer=" + answerIndex
                + " node=" + session.CurrentNodeId);

            DialogueSessionResult result = service.SelectOption(session, answerIndex);
            if (!result.IsValid)
            {
                LogValidation("Arete Rex dialogue option failed", result.Validation);
                CloseSession(source, sessionKey, true);
                return true;
            }

            LogRecordedActions(source, result);

            if (result.Session == null || !result.Session.IsActive)
            {
                CloseSession(source, sessionKey, true);
                return true;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter[sessionKey] = result.Session;
            }

            SendDialogueNode(source, result);
            return true;
        }

        public static bool TryHandleClose(ICharacter source, Identity targetIdentity)
        {
            if (!IsEnabled || source == null || !IsAreteLandingPlayfield(source))
            {
                return false;
            }

            if (!IsRexLarsson(targetIdentity) && !HasActiveSession(source))
            {
                return false;
            }

            string sessionKey = CreateSessionKey(source.Identity);
            bool hadSession;
            lock (SyncRoot)
            {
                hadSession = SessionsByCharacter.Remove(sessionKey);
            }

            if (hadSession)
            {
                LogUtil.Debug(
                    DebugInfoDetail.KnuBot,
                    "Arete Rex dialogue session closed by client for character=" + source.Identity.ToString(true));
                return true;
            }

            return false;
        }

        private static bool HasActiveSession(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            DialogueSession session;
            lock (SyncRoot)
            {
                SessionsByCharacter.TryGetValue(CreateSessionKey(source.Identity), out session);
            }

            return session != null && session.IsActive;
        }

        private static bool TryGetSessionService(out DialogueSessionService service)
        {
            lock (SyncRoot)
            {
                if (!loadAttempted)
                {
                    loadAttempted = true;
                    loadSucceeded = TryLoadRexContent();
                }

                service = dialogueSessionService;
                return loadSucceeded && service != null;
            }
        }

        private static bool TryLoadRexContent()
        {
            string manifestPath = ResolveRexManifestPath();
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                LogUtil.Debug(
                    DebugInfoDetail.KnuBot,
                    "Arete Rex dialogue routing disabled because Rex content manifest was not found.");
                return false;
            }

            AreteValidationResult aggregateValidation =
                new AreteAggregateContentValidator().ValidateManifest(manifestPath);
            if (!aggregateValidation.IsValid)
            {
                LogValidation("Arete Rex aggregate validation failed", aggregateValidation);
                return false;
            }

            var registry = new DialogueContentRegistry();
            AreteValidationResult dialogueValidation = registry.LoadFromManifest(manifestPath);
            if (!dialogueValidation.IsValid)
            {
                LogValidation("Arete Rex dialogue manifest load failed", dialogueValidation);
                return false;
            }

            dialogueRegistry = registry;
            dialogueSessionService = new DialogueSessionService(dialogueRegistry);
            loadedManifestPath = manifestPath;

            LogUtil.Debug(
                DebugInfoDetail.KnuBot,
                "Arete Rex dialogue content loaded from " + loadedManifestPath);

            return true;
        }

        private static void SendDialogueNode(ICharacter source, DialogueSessionResult result)
        {
            if (result.CurrentNode != null && !string.IsNullOrWhiteSpace(result.CurrentNode.PromptText))
            {
                KnuBotAppendTextMessageHandler.Default.Send(source, RexLarssonIdentity, result.CurrentNode.PromptText);
            }

            string[] choices = result.AvailableOptions
                .OrderBy(option => option.Index)
                .Select(option => option.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray();

            if (choices.Length == 0)
            {
                KnuBotCloseChatWindowMessageHandler.Default.Send(source, RexLarssonIdentity);
                return;
            }

            KnuBotAnswerListMessageHandler.Default.Send(source, RexLarssonIdentity, choices);
            LogUtil.Debug(
                DebugInfoDetail.KnuBot,
                "Arete Rex dialogue sent " + choices.Length + " captured option(s) to character="
                + source.Identity.ToString(true));
        }

        private static void CloseSession(ICharacter source, string sessionKey, bool sendClose)
        {
            lock (SyncRoot)
            {
                SessionsByCharacter.Remove(sessionKey);
            }

            if (sendClose)
            {
                KnuBotCloseChatWindowMessageHandler.Default.Send(source, RexLarssonIdentity);
            }

            LogUtil.Debug(
                DebugInfoDetail.KnuBot,
                "Arete Rex dialogue session ended for character=" + source.Identity.ToString(true));
        }

        private static void LogRecordedActions(ICharacter source, DialogueSessionResult result)
        {
            int actionCount = result.RecordedActions == null ? 0 : result.RecordedActions.Count;
            if (actionCount == 0)
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.KnuBot,
                "Arete Rex dialogue recorded " + actionCount
                + " no-op action(s) for character=" + source.Identity.ToString(true));
        }

        private static void LogValidation(string prefix, AreteValidationResult validation)
        {
            if (validation == null)
            {
                LogUtil.Debug(DebugInfoDetail.KnuBot, prefix + ": validation result was missing.");
                return;
            }

            foreach (string error in validation.Errors)
            {
                LogUtil.Debug(DebugInfoDetail.KnuBot, prefix + ": " + error);
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

        private static bool IsRexLarsson(ICharacter npc)
        {
            return npc != null && IsRexLarsson(npc.Identity);
        }

        private static bool IsRexLarsson(Identity identity)
        {
            return identity.Type == RexLarssonIdentity.Type && identity.Instance == RexLarssonIdentity.Instance;
        }

        private static bool IsAreteLandingPlayfield(ICharacter character)
        {
            return character != null
                   && character.Playfield != null
                   && character.Playfield.Identity.Instance == AreteLandingPlayfieldId;
        }

        private static string CreateSessionKey(Identity characterIdentity)
        {
            return characterIdentity.Type + ":" + characterIdentity.Instance + "|" + RexLarssonNpcIdentity;
        }

        private static string ResolveRexManifestPath()
        {
            foreach (string candidate in GetManifestPathCandidates())
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetManifestPathCandidates()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string currentDirectory = Directory.GetCurrentDirectory();

            yield return Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\", RexManifestRelativePath));
            yield return Path.GetFullPath(Path.Combine(currentDirectory, @"AORebirth\", RexManifestRelativePath));
            yield return Path.GetFullPath(Path.Combine(currentDirectory, RexManifestRelativePath));
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }
    }
}
