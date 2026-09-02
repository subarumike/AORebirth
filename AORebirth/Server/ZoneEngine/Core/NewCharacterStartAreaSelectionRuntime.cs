namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Interfaces.Persistence.Missions;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.MessageHandlers;

    /// <summary>
    /// Presents a durable, one-time choice between Arete and ICC Shuttleport to
    /// newly created Rubi-Ka characters. The official Shadowlands selector remains
    /// owned by character creation and never enters this workflow.
    /// </summary>
    internal static class NewCharacterStartAreaSelectionRuntime
    {
        internal const int AretePlayfieldId = 6553;

        internal const int IccShuttleportPlayfieldId = 4582;

        internal const float IccShuttleportX = 939.0f;

        internal const float IccShuttleportY = 20.3f;

        internal const float IccShuttleportZ = 732.0f;

        internal const string AreteOption = "Arete";

        internal const string IccShuttleportOption = "ICC Shuttleport";

        internal const string PromptSpeakerName = "ICC Shuttleport Commander";

        private const int PromptDelayMilliseconds = 750;

        private const int KnuBotPacketPacingMilliseconds = 20;

        private static readonly object SyncRoot = new object();

        private static readonly Dictionary<int, Identity> ActiveTargetsByCharacter =
            new Dictionary<int, Identity>();

        private static IMissionDao missionDao;

        internal static void Initialize(IMissionDao dao)
        {
            if (dao == null)
            {
                throw new ArgumentNullException("dao");
            }

            lock (SyncRoot)
            {
                missionDao = dao;
            }
        }

        internal static void Schedule(IZoneClient client)
        {
            if (client == null || client.Controller == null || client.Controller.Character == null)
            {
                return;
            }

            int characterId = client.Controller.Character.Identity.Instance;
            lock (SyncRoot)
            {
                ActiveTargetsByCharacter.Remove(characterId);
            }

            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    Thread.Sleep(PromptDelayMilliseconds);

                    ICharacter character = client.Controller == null ? null : client.Controller.Character;
                    if (character == null
                        || character.Identity.Instance != characterId
                        || character.Controller == null
                        || character.Controller.Client == null
                        || character.Playfield == null
                        || character.Playfield.Identity.Instance != AretePlayfieldId
                        || !string.Equals(
                            GetMissionDao().GetStartAreaSelectionState(characterId),
                            MissionStartAreaSelectionStates.Pending,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    Identity target = ResolvePromptTarget(character);
                    lock (SyncRoot)
                    {
                        ActiveTargetsByCharacter[characterId] = target;
                    }

                    KnuBotOpenChatWindowMessageHandler.Default.Send(character, target, 1);
                    Thread.Sleep(KnuBotPacketPacingMilliseconds);
                    KnuBotAppendTextMessageHandler.Default.Send(
                        character,
                        target,
                        "Choose where this character should begin. This choice is permanent.");
                    Thread.Sleep(KnuBotPacketPacingMilliseconds);
                    KnuBotAnswerListMessageHandler.Default.Send(
                        character,
                        target,
                        new[] { AreteOption, IccShuttleportOption });
                });
        }

        internal static bool TryHandleAnswer(ICharacter character, Identity target, int answerIndex)
        {
            if (!HasActiveSession(character, target))
            {
                return false;
            }

            string selectedState;
            if (answerIndex == 0)
            {
                selectedState = MissionStartAreaSelectionStates.Arete;
            }
            else if (answerIndex == 1)
            {
                selectedState = MissionStartAreaSelectionStates.IccShuttleport;
            }
            else
            {
                SendChoices(character, target, "Please choose Arete or ICC Shuttleport.");
                return true;
            }

            if (!GetMissionDao().TryCompleteStartAreaSelection(character.Identity.Instance, selectedState))
            {
                SendChoices(character, target, "Your choice could not be saved. Please try again.");
                return true;
            }

            RemoveSession(character.Identity.Instance);
            KnuBotCloseChatWindowMessageHandler.Default.Send(character, target);

            if (string.Equals(
                selectedState,
                MissionStartAreaSelectionStates.IccShuttleport,
                StringComparison.Ordinal))
            {
                TeleportToIccShuttleport(character);
            }

            return true;
        }

        internal static bool TryHandleClose(ICharacter character, Identity target)
        {
            if (!HasActiveSession(character, target))
            {
                return false;
            }

            RemoveSession(character.Identity.Instance);
            return true;
        }

        private static Identity ResolvePromptTarget(ICharacter character)
        {
            Playfield playfield = character.Playfield as Playfield;
            if (playfield == null)
            {
                return character.Identity;
            }

            ICharacter commander = playfield.EnumerateActiveCharacters()
                .FirstOrDefault(
                    candidate => candidate != null
                                 && string.Equals(
                                     candidate.Name,
                                     PromptSpeakerName,
                                     StringComparison.OrdinalIgnoreCase));
            return commander == null ? character.Identity : commander.Identity;
        }

        private static void SendChoices(ICharacter character, Identity target, string prompt)
        {
            KnuBotAppendTextMessageHandler.Default.Send(character, target, prompt);
            Thread.Sleep(KnuBotPacketPacingMilliseconds);
            KnuBotAnswerListMessageHandler.Default.Send(
                character,
                target,
                new[] { AreteOption, IccShuttleportOption });
        }

        private static bool HasActiveSession(ICharacter character, Identity target)
        {
            if (character == null)
            {
                return false;
            }

            lock (SyncRoot)
            {
                Identity activeTarget;
                return ActiveTargetsByCharacter.TryGetValue(character.Identity.Instance, out activeTarget)
                       && activeTarget.Type == target.Type
                       && activeTarget.Instance == target.Instance;
            }
        }

        private static void RemoveSession(int characterId)
        {
            lock (SyncRoot)
            {
                ActiveTargetsByCharacter.Remove(characterId);
            }
        }

        private static IMissionDao GetMissionDao()
        {
            lock (SyncRoot)
            {
                if (missionDao == null)
                {
                    throw new InvalidOperationException(
                        "New-character start-area persistence has not been initialized.");
                }

                return missionDao;
            }
        }

        private static void TeleportToIccShuttleport(ICharacter character)
        {
            Dynel dynel = character as Dynel;
            Playfield sourcePlayfield = character == null ? null : character.Playfield as Playfield;
            if (dynel == null || sourcePlayfield == null)
            {
                return;
            }

            character.DoNotDoTimers = false;
            sourcePlayfield.Teleport(
                dynel,
                new Coordinate(IccShuttleportX, IccShuttleportY, IccShuttleportZ),
                character.Heading,
                new Identity { Type = IdentityType.Playfield, Instance = IccShuttleportPlayfieldId });
        }
    }
}
