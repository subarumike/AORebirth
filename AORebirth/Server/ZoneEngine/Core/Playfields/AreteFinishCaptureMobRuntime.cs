namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Textures;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture 20260721-finish Arete monster-body NPCs that BART cannot show (Engineer Automaton I).
    /// </summary>
    internal static class AreteFinishCaptureMobRuntime
    {
        private const int AreteLandingPlayfieldId = 6553;

        private const int AutomatonMonsterData = 17649;

        private const int AutomatonCombatEvidenceSourceIdentity = unchecked((int)0x7985CD86);

        private const string AutomatonCombatProfileSelector =
            "resource=6553|md=17649|level=5|name=Engineer Automaton I";

        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

        public static void StartForPlayfield(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId
                || !LinkedPlayfields.Add(playfieldIdentity.Instance))
            {
                return;
            }

            try
            {
                SpawnAutomaton(playfield, playfieldIdentity, activateNpc);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AreteFinishCaptureMobRuntime spawn failed: "
                    + ex.GetType().Name
                    + ": "
                    + ex.Message);
                LinkedPlayfields.Remove(playfieldIdentity.Instance);
            }
        }

        public static void ClearPlayfield(int playfieldInstance)
        {
            LinkedPlayfields.Remove(playfieldInstance);
        }

        private static void SpawnAutomaton(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            // Capture 20260721-finish SimpleChar:7985CD86 near Vernon / Remi.
            const float x = 3439.90454f;
            const float y = 11.965f;
            const float z = 813.694f;

            foreach (ICharacter existing in playfield.EnumerateActiveCharacters())
            {
                if (existing == null
                    || existing.Stats[StatIds.health].Value <= 0
                    || !string.Equals(existing.Name, "Engineer Automaton I", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Coordinate pos = existing.Coordinates();
                float dx = pos.x - x;
                float dz = pos.z - z;
                if ((dx * dx) + (dz * dz) <= 4f)
                {
                    return;
                }
            }

            NPCController controller = new NPCController { AiProfile = NpcAiProfile.Passive };
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                CombatTestMobArchetype.TemplateHash,
                playfieldIdentity,
                new Coordinate { x = x, y = y, z = z },
                new Quaternion(0.0, -0.498718232, 0.0, 0.8667642),
                controller,
                5);
            if (mob == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AreteFinishCaptureMobRuntime Engineer Automaton I FAILED template=A004");
                return;
            }

            mob.Name = "Engineer Automaton I";
            mob.Playfield = playfield;
            CombatTestMobArchetype.Prepare(mob, CombatTestMobArchetype.MalfunctioningCleaningRobot);
            mob.Name = "Engineer Automaton I";
            SetStat(mob, StatIds.monsterdata, AutomatonMonsterData);
            SetStat(mob, StatIds.life, 138);
            SetStat(mob, StatIds.health, 138);
            SetStat(mob, StatIds.level, 5);
            SetStat(mob, StatIds.npcfamily, 95);
            SetStat(mob, StatIds.monsterscale, 93);
            SetStat(mob, StatIds.runspeed, 41);
            SetStat(mob, StatIds.flags, 403182081);
            SetStat(mob, StatIds.visualflags, 31);
            SetStat(mob, StatIds.side, 0);
            SetStat(mob, StatIds.breed, 7);
            SetStat(mob, StatIds.sex, 1);
            SetStat(mob, StatIds.race, 1);
            SetStat(mob, StatIds.headmesh, 0);
            if (mob.Textures != null)
            {
                mob.Textures.Clear();
                for (int i = 0; i < 5; i++)
                {
                    mob.Textures.Add(new AOTextures(i, 0));
                }
            }

            if (mob.MeshLayer != null)
            {
                mob.MeshLayer.Clear();
            }

            mob.Coordinates(new Coordinate { x = x, y = y, z = z });
            string combatFailure;
            bool combatReady = CapturedEnemyCombatRuntime.PrepareAndRequireCombatReady(
                mob,
                controller,
                AreteRegularMobCombatProfileSelector.Create(
                    "20260721-finish Engineer Automaton I 0x7985CD86 has no exact source-local combat profile",
                    AutomatonCombatProfileSelector,
                    AutomatonCombatEvidenceSourceIdentity,
                    0,
                    0,
                    NpcAiProfile.Passive),
                out combatFailure);
            if (!combatReady)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Engineer Automaton I intentionally quarantined reason=" + combatFailure);
            }

            mob.DoNotDoTimers = false;
            activateNpc(mob);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "AreteFinishCaptureMobRuntime spawned Engineer Automaton I source=20260721-finish");
        }

        private static void SetStat(Character mob, StatIds id, int value)
        {
            mob.Stats.SetBaseValueWithoutTriggering((int)id, (uint)value);
            mob.Stats[id].Value = value;
        }
    }
}
