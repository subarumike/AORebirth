namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture 20260727-204902 SANDSTORM Marauders east of Arete market (Remi Hellfyre quest).
    /// </summary>
    internal static class AreteSandstormMarauderRuntime
    {
        private const int AreteLandingPlayfieldId = 6553;

        private const string MarauderName = "SANDSTORM Marauder";

        private const int MarauderMonsterData = 265822;

        private const int MarauderLevel = 7;

        private const int MarauderHealth = 650;

        private const int MarauderScale = 94;

        private const int MarauderNpcFamily = 0;

        private const int MarauderRunSpeed = 24;

        private const int MarauderCharacterFlags = 268964353;

        private const int MarauderHeadMesh = 40101;

        // Capture corpse CATMesh (not MonsterData). MD-as-CATMesh crashes the client.
        private const int MarauderCorpseCatMesh = 265819;

        private const double RespawnSeconds = 45.0;

        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

        private static readonly Dictionary<int, DateTime[]> NextRespawnUtcBySlot = new Dictionary<int, DateTime[]>();

        // Initial cluster from enemy-full-updates 20260727-204902 (~4032–4039, 667–697).
        private static readonly float[][] SpawnSlots =
            {
                new[] { 4033.099f, 0.01f, 677.291f },
                new[] { 4032.111f, 0.01f, 667.514f },
                new[] { 4039.592f, 0.675f, 696.701f },
                new[] { 4038.502f, 0.01f, 688.275f },
                new[] { 4054.383f, 1.538f, 651.418f }
            };

        public static void StartForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId
                || !LinkedPlayfields.Add(playfieldIdentity.Instance))
            {
                return;
            }

            DateTime[] timers = new DateTime[SpawnSlots.Length];
            NextRespawnUtcBySlot[playfieldIdentity.Instance] = timers;
            int spawned = 0;
            for (int i = 0; i < SpawnSlots.Length; i++)
            {
                try
                {
                    if (SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)
                    {
                        timers[i] = DateTime.MaxValue;
                        spawned++;
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "AreteSandstormMarauderRuntime spawn slot=" + i + " failed: "
                        + ex.GetType().Name + ": " + ex.Message);
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "AreteSandstormMarauderRuntime spawned="
                + spawned
                + "/"
                + SpawnSlots.Length
                + " pf="
                + playfieldIdentity.Instance
                + " source=20260727-204902");
            if (spawned == 0)
            {
                LinkedPlayfields.Remove(playfieldIdentity.Instance);
                NextRespawnUtcBySlot.Remove(playfieldIdentity.Instance);
            }
        }

        public static void ClearPlayfield(int playfieldInstance)
        {
            LinkedPlayfields.Remove(playfieldInstance);
            NextRespawnUtcBySlot.Remove(playfieldInstance);
        }

        public static void TickRespawn(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId)
            {
                return;
            }

            LinkedPlayfields.Add(playfieldIdentity.Instance);
            DateTime[] timers;
            if (!NextRespawnUtcBySlot.TryGetValue(playfieldIdentity.Instance, out timers)
                || timers == null
                || timers.Length != SpawnSlots.Length)
            {
                timers = new DateTime[SpawnSlots.Length];
                NextRespawnUtcBySlot[playfieldIdentity.Instance] = timers;
            }

            for (int i = 0; i < SpawnSlots.Length; i++)
            {
                if (HasLivingMarauderNear(playfield, SpawnSlots[i]))
                {
                    timers[i] = DateTime.MaxValue;
                }
                else if (timers[i] == DateTime.MaxValue)
                {
                    timers[i] = DateTime.UtcNow + TimeSpan.FromSeconds(RespawnSeconds);
                }
                else if (!(timers[i] > DateTime.UtcNow))
                {
                    try
                    {
                        if (SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)
                        {
                            timers[i] = DateTime.MaxValue;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private static Character SpawnSlot(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            int slotIndex)
        {
            float[] pos = SpawnSlots[slotIndex];
            NPCController controller = new NPCController { AiProfile = NpcAiProfile.Aggressive };
            Character marauder = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                "A004",
                playfieldIdentity,
                new Coordinate { x = pos[0], y = pos[1], z = pos[2] },
                new Quaternion(0.0, 0.0, 0.0, 1.0),
                controller,
                MarauderLevel);
            if (marauder == null)
            {
                return null;
            }

            marauder.Name = MarauderName;
            marauder.Playfield = playfield;
            ApplyCaptureStats(marauder);
            marauder.Name = MarauderName;
            CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttackOnSight(
                "arete-sandstorm-20260727-204902",
                8,
                18,
                2.0,
                1,
                0,
                1279612721,
                0,
                0,
                0,
                0,
                0,
                0);
            string unused;
            CapturedEnemyCombatRuntime.Prepare(marauder, controller, contract, out unused);
            controller.AiProfile = NpcAiProfile.Aggressive;
            marauder.Coordinates(new Coordinate { x = pos[0], y = pos[1], z = pos[2] });
            marauder.DoNotDoTimers = false;
            activateNpc(marauder);
            playfield.AnnounceSpawnedCharacterVisibility(marauder, Identity.None);
            return marauder;
        }

        private static void ApplyCaptureStats(Character marauder)
        {
            SetStat(marauder, StatIds.monsterdata, MarauderMonsterData);
            SetStat(marauder, StatIds.life, MarauderHealth);
            SetStat(marauder, StatIds.health, MarauderHealth);
            SetStat(marauder, StatIds.level, MarauderLevel);
            SetStat(marauder, StatIds.npcfamily, MarauderNpcFamily);
            SetStat(marauder, StatIds.monsterscale, MarauderScale);
            SetStat(marauder, StatIds.runspeed, MarauderRunSpeed);
            SetStat(marauder, StatIds.flags, MarauderCharacterFlags);
            SetStat(marauder, StatIds.visualflags, 31);
            SetStat(marauder, StatIds.side, 3);
            SetStat(marauder, StatIds.breed, 1);
            SetStat(marauder, StatIds.sex, 2);
            SetStat(marauder, StatIds.race, 1);
            SetStat(marauder, StatIds.fatness, 1);
            SetStat(marauder, StatIds.headmesh, MarauderHeadMesh);
            // Usable corpse CATMesh for CorpseCatMeshFor(); living body still uses MonsterData.
            SetStat(marauder, StatIds.catmesh, MarauderCorpseCatMesh);
            SetStat(marauder, StatIds.displaycatmesh, MarauderCorpseCatMesh);
        }

        private static void SetStat(ICharacter mob, StatIds stat, int value)
        {
            mob.Stats[stat].Value = value;
            mob.Stats[stat].BaseValue = (uint)value;
        }

        private static bool HasLivingMarauderNear(Playfield playfield, float[] pos)
        {
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller is PlayerController
                    || !string.Equals(candidate.Name, MarauderName, StringComparison.OrdinalIgnoreCase)
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                float dx = candidate.Coordinates().x - pos[0];
                float dz = candidate.Coordinates().z - pos[2];
                if (dx * dx + dz * dz <= 6.25f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
