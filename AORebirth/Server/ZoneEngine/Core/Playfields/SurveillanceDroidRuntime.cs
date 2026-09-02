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
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    internal static class SurveillanceDroidRuntime
    {
        internal const string NpcName = "Surveillance Droid";

        internal const uint CapturedScfuFlags = 170543699u;

        internal const int CaptureInstance = 2028010634;

        private const int AreteLandingPlayfieldId = 6553;

        private const int MonsterDataId = 210238;

        private const float SpawnX = 3567.518f;

        private const float SpawnY = 5.1100006f;

        private const float SpawnZ = 820.3735f;

        private const float Hy = 0.5793964f;

        private const float Hw = 0.8150459f;

        internal static readonly byte[] CapturedUnknown1 =
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 3, 1, 0, 1, 0, 1, 0, 1,
                0, 1, 0, 0, 0, 3, 0, 0
            };

        private static readonly byte[] ExtendedTextureOverrideData =
            {
                20, 0, 0, 15, 196, 99, 97, 109, 101, 114,
                97, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 3, 53,
                27, 0, 0, 0, 0, 0, 0, 0, 0, 99,
                97, 109, 101, 114, 97, 32, 103, 108, 111, 119,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 3, 168, 166, 0, 0, 0, 0, 0,
                0, 0, 0, 99, 97, 109, 101, 114, 97, 32,
                108, 101, 110, 115, 101, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 3, 168, 168, 0,
                0, 0, 0, 0, 0, 0, 0
            };

        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

        internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)
        {
            if (string.Equals(name, NpcName, StringComparison.Ordinal))
            {
                data = (byte[])ExtendedTextureOverrideData.Clone();
                return true;
            }

            data = null;
            return false;
        }

        public static void StartForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId)
            {
                return;
            }

            LinkedPlayfields.Add(playfieldIdentity.Instance);
            Character droid = SpawnDroid(playfield, playfieldIdentity, activateNpc);
            if (droid != null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "SurveillanceDroidRuntime SPAWNED pf="
                    + playfieldIdentity.Instance
                    + " id="
                    + droid.Identity
                    + " monsterdata="
                    + MonsterDataId
                    + " template=A004+wire");
            }
            else
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "SurveillanceDroidRuntime START produced no mob (A004 / already present)");
            }
        }

        public static void ClearPlayfield(int playfieldInstance)
        {
            LinkedPlayfields.Remove(playfieldInstance);
        }

        public static void TickEnsurePresent(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId)
            {
                return;
            }

            LinkedPlayfields.Add(playfieldIdentity.Instance);
            try
            {
                ICharacter existing = FindLivingDroid(playfield);
                if (existing != null)
                {
                    if (IsCaptureCorrectDroid(existing))
                    {
                        return;
                    }

                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        "SurveillanceDroidRuntime replacing invalid droid id="
                        + existing.Identity
                        + " monsterdata="
                        + existing.Stats[StatIds.monsterdata].Value
                        + " breed="
                        + existing.Stats[StatIds.breed].Value);
                    playfield.DespawnNpcImmediately(existing);
                }

                if (SpawnDroid(playfield, playfieldIdentity, activateNpc) != null)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        "SurveillanceDroidRuntime respawned pf=" + playfieldIdentity.Instance);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "SurveillanceDroidRuntime ensure exception " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        internal static ICharacter FindLivingDroid(Playfield playfield)
        {
            if (playfield == null)
            {
                return null;
            }

            foreach (ICharacter candidate in playfield.EnumerateActiveCharacters())
            {
                if (candidate == null || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                if (!string.Equals(candidate.Name, NpcName, StringComparison.OrdinalIgnoreCase)
                    && candidate.Stats[StatIds.monsterdata].Value != MonsterDataId)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private static bool IsCaptureCorrectDroid(ICharacter npc)
        {
            return npc != null
                   && npc.Stats[StatIds.monsterdata].Value == MonsterDataId
                   && npc.Stats[StatIds.breed].Value == 6;
        }

        private static Character SpawnDroid(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            ICharacter existing = FindLivingDroid(playfield);
            if (existing != null && IsCaptureCorrectDroid(existing))
            {
                return null;
            }

            if (existing != null)
            {
                playfield.DespawnNpcImmediately(existing);
            }

            NPCController controller = new NPCController { AiProfile = NpcAiProfile.Social };
            Character droid;
            try
            {
                droid = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                    "A004",
                    playfieldIdentity,
                    new Coordinate { x = SpawnX, y = SpawnY, z = SpawnZ },
                    new Quaternion(0.0, Hy, 0.0, Hw),
                    controller,
                    6);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "SurveillanceDroidRuntime SpawnMobFromTemplate threw "
                    + ex.GetType().Name
                    + ": "
                    + ex.Message);
                return null;
            }

            if (droid == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "SurveillanceDroidRuntime spawn FAILED template=A004 source=20260720-151642");
                return null;
            }

            CombatTestMobArchetype.Prepare(droid, CombatTestMobArchetype.DuneFlea);
            droid.Name = NpcName;
            droid.FirstName = string.Empty;
            droid.LastName = string.Empty;
            droid.Playfield = playfield;
            droid.MeshLayer.Clear();
            droid.SocialMeshLayer.Clear();
            droid.Textures.Clear();
            for (int i = 0; i < 5; i++)
            {
                droid.Textures.Add(new AOTextures(i, 0));
            }

            SetStat(droid, StatIds.monsterdata, MonsterDataId);
            SetStat(droid, StatIds.life, 69);
            SetStat(droid, StatIds.health, 69);
            SetStat(droid, StatIds.level, 6);
            SetStat(droid, StatIds.visualflags, 31);
            SetStat(droid, StatIds.npcfamily, 137);
            SetStat(droid, StatIds.losheight, 0);
            SetStat(droid, StatIds.flags, 268964353);
            SetStat(droid, StatIds.side, 0);
            SetStat(droid, StatIds.breed, 6);
            SetStat(droid, StatIds.sex, 1);
            SetStat(droid, StatIds.race, 1);
            SetStat(droid, StatIds.fatness, 1);
            SetStat(droid, StatIds.headmesh, 0);
            SetStat(droid, StatIds.monsterscale, 110);
            SetStat(droid, StatIds.runspeed, 20);
            SetStat(droid, StatIds.currentmovementmode, 3);
            SetStat(droid, StatIds.prevmovementmode, 3);
            SetStat(droid, StatIds.accountflags, 0);
            SetStat(droid, StatIds.expansion, 0);
            SetStat(droid, StatIds.profession, 0);
            SetStat(droid, StatIds.visualprofession, 0);
            droid.Position = (new Coordinate { x = SpawnX, y = SpawnY, z = SpawnZ }).coordinate;
            droid.DoNotDoTimers = false;
            activateNpc(droid);
            playfield.AnnounceSpawnedCharacterVisibility(droid, Identity.None);
            LogUtil.Debug(
                DebugInfoDetail.Error,
                "SurveillanceDroidRuntime SPAWNED name="
                + NpcName
                + " monsterdata="
                + MonsterDataId
                + " id="
                + droid.Identity
                + " at="
                + SpawnX
                + ","
                + SpawnY
                + ","
                + SpawnZ);
            return droid;
        }

        private static void SetStat(Character mob, StatIds stat, int value)
        {
            mob.Stats.SetBaseValueWithoutTriggering((int)stat, (uint)value);
            mob.Stats[stat].Value = value;
        }
    }
}
