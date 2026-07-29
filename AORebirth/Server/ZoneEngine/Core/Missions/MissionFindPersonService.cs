namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    /// <summary>
    /// FindPerson objective: named person inside the instance.
    /// Capture 20260724-mission-find-person completes on CharacterAction.InfoRequest
    /// (inspect/info the target), not GenericCmd Use.
    /// </summary>
    internal static class MissionFindPersonService
    {
        private static readonly object Sync = new object();

        private static readonly HashSet<long> Targets = new HashSet<long>();

        private static long Key(Identity identity)
        {
            return ((long)(int)identity.Type << 32) | (uint)identity.Instance;
        }

        public static void Register(Identity npcIdentity)
        {
            if ((int)npcIdentity.Type == 0 || npcIdentity.Instance == 0)
            {
                return;
            }

            lock (Sync)
            {
                Targets.Add(Key(npcIdentity));
            }
        }

        public static bool IsFindPersonTarget(Identity npcIdentity)
        {
            lock (Sync)
            {
                return Targets.Contains(Key(npcIdentity));
            }
        }

        public static void Unregister(Identity npcIdentity)
        {
            lock (Sync)
            {
                Targets.Remove(Key(npcIdentity));
            }
        }

        public static bool IsFindPersonOffer(QuestInfo offer)
        {
            return offer != null && offer.MissionIconId == MissionTypeCatalog.FindPersonIcon;
        }

        public static bool IsFindPersonMission(MissionAcceptedStore.AcceptedMission entry)
        {
            return entry != null && entry.MissionIconId == MissionTypeCatalog.FindPersonIcon;
        }

        /// <summary>
        /// Capture: InfoRequest on Levi McDannold → InfoPacket → mission complete/delete.
        /// </summary>
        public static bool TryHandleInfoRequest(IZoneClient client, Identity target)
        {
            if (client == null || target == null)
            {
                return false;
            }

            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character != null
                && character.Playfield != null
                && MissionAcgBindingRuntime.IsBoundLivePlayfield(
                    character.Playfield.Identity.Instance)
                && MissionAcgRuntimeManager.IsRuntimeIdentityCandidate(
                    character.Playfield.Identity.Instance,
                    target))
            {
                return MissionAcgObjectiveInteractionService.TryHandleInfoRequest(
                    client,
                    target);
            }

            if (!IsFindPersonTarget(target))
            {
                return false;
            }

            if (character == null || character.Playfield == null
                || !MissionInstanceService.IsMissionInstancePlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            // Unregister only after complete succeeds — early Unregister blocked retry + Delete.
            bool completed = MissionCompleteService.TryCompleteFindPerson(
                client,
                character,
                "FindPersonInfoRequest");
            if (completed)
            {
                Unregister(target);
            }

            return completed;
        }

        public static bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            // Kept as fallback; live Find-Person capture used InfoRequest.
            return TryHandleInfoRequest(client, target);
        }
    }
}
