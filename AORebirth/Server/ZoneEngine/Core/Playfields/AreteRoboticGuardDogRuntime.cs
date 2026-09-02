namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Capture 20260722-212421: Robotic Guard Dog Attack after CharDCMove LeaveSneak.
    /// While StartedSneaking, dog does not aggro; after LeaveSneak (MoveType 0x24) it does.
    /// </summary>
    internal static class AreteRoboticGuardDogRuntime
    {
        internal const string DogName = "Robotic Guard Dog";

        private const int DogMonsterData = 17720;

        internal const int DogCombatEvidenceSourceIdentity = unchecked((int)0x78E0FCE9);

        internal const string DogCombatProfileSelector =
            "resource=6553|md=17720|level=13|name=Robotic Guard Dog";

        // Capture: LeaveSneak at ~7m from dog spawn → Attack ~3s later.
        // Keep aggro inside the short home leash so the dog does not yo-yo.
        private const float AggroRadiusMeters = 8.0f;

        // Capture 20260722-212421: brief FollowTarget near spawn then StopFight —
        // not a zone-wide chase. Couple meters of travel from home.
        internal const double MaximumNpcDistanceFromHomeMeters = 8.0;

        // Capture CharDCMove LeaveSneak MoveType byte 0x24.
        private const byte LeaveSneakMoveType = 0x24;

        private static readonly object Gate = new object();

        private static readonly HashSet<int> SneakingCharacterInstances = new HashSet<int>();

        private static readonly HashSet<int> DogNpcInstances = new HashSet<int>();

        internal static void NoteSneakStarted(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            lock (Gate)
            {
                SneakingCharacterInstances.Add(character.Identity.Instance);
            }
        }

        internal static void NoteMoveType(ICharacter character, byte moveType)
        {
            if (character == null)
            {
                return;
            }

            if (moveType != LeaveSneakMoveType)
            {
                return;
            }

            lock (Gate)
            {
                SneakingCharacterInstances.Remove(character.Identity.Instance);
            }
        }

        internal static bool IsPlayerSneaking(ICharacter character)
        {
            if (character == null)
            {
                return false;
            }

            lock (Gate)
            {
                return SneakingCharacterInstances.Contains(character.Identity.Instance);
            }
        }

        internal static void RegisterDog(ICharacter dog, bool combatReady)
        {
            if (dog == null)
            {
                return;
            }

            lock (Gate)
            {
                DogNpcInstances.Add(dog.Identity.Instance);
            }

            if (combatReady)
            {
                MissionInstanceMobCombat.RegisterAggressive(dog.Identity);
            }
        }

        internal static void PrepareSpawnedDog(
            Character dog,
            NPCController controller,
            string combatProfileSelector,
            int combatEvidenceSourceIdentity)
        {
            if (dog == null || controller == null)
            {
                return;
            }

            CapturedEnemyCombatContract contract = AreteRegularMobCombatProfileSelector.Create(
                "arete-guard-dog-20260722-212421",
                combatProfileSelector,
                combatEvidenceSourceIdentity,
                0,
                0,
                NpcAiProfile.Aggressive);
            string combatFailure;
            bool combatReady = CapturedEnemyCombatRuntime.PrepareAndRequireCombatReady(
                dog,
                controller,
                contract,
                out combatFailure);
            RegisterDog(dog, combatReady);
        }

        internal static bool IsRegisteredDog(ICharacter npc)
        {
            if (npc == null)
            {
                return false;
            }

            lock (Gate)
            {
                if (DogNpcInstances.Contains(npc.Identity.Instance))
                {
                    return true;
                }
            }

            return string.Equals(npc.Name, DogName, StringComparison.OrdinalIgnoreCase)
                   && npc.Stats[StatIds.monsterdata].Value == DogMonsterData;
        }

        public static ICharacter FindAutomaticAggroTarget(ICharacter npc)
        {
            if (npc == null || npc.Playfield == null || npc.Stats[StatIds.health].Value <= 0)
            {
                return null;
            }

            CapturedEnemyCombatContract preparedContract;
            if (!CapturedEnemyCombatRuntimeRegistry.TryGet(npc.Identity.Instance, out preparedContract)
                || preparedContract == null
                || !preparedContract.IsCombatReady)
            {
                return null;
            }

            if (!IsRegisteredDog(npc))
            {
                return null;
            }

            if (npc.FightingTarget.Instance != 0)
            {
                return null;
            }

            Playfield playfield = npc.Playfield as Playfield;
            if (playfield == null)
            {
                return null;
            }

            if (npc.Position == null)
            {
                return null;
            }

            Coordinate npcCoord = npc.CalculatePredictedPosition();
            ICharacter best = null;
            double bestDistance = AggroRadiusMeters;
            List<ICharacter> inRange;
            try
            {
                inRange = playfield.FindCharacterInRange(npc, AggroRadiusMeters);
            }
            catch
            {
                // Never let aggro scanning abort the Arete playfield heartbeat.
                return null;
            }

            for (int i = 0; i < inRange.Count; i++)
            {
                ICharacter candidate = inRange[i];
                if (candidate == null
                    || candidate.Position == null
                    || candidate.Identity.Instance == npc.Identity.Instance
                    || !(candidate.Controller is PlayerController)
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                if (IsPlayerSneaking(candidate))
                {
                    continue;
                }

                double distance = candidate.CalculatePredictedPosition().Distance3D(npcCoord);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
