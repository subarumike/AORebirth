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
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    internal static class MarcusPadAmbientCombat
    {
        private const int AreteLandingPlayfieldId = 6553;

        private const string MarcusName = "Marcus Stone";

        private const string BurningRobotName = "Burning Cleaning Robot";

        private const float RobotX = 3636.5132f;

        private const float RobotY = 40.984997f;

        private const float RobotZ = 832.7695f;

        private const int RobotHealth = 58;

        private const int RobotLevel = 5;

        private const int RobotCharacterFlags = 269226497;

        private const int RobotScale = 200;

        private const double FlamethrowerRange = 15.0;

        private const double FlamethrowerRechargeSeconds = 6.0;

        private const int FlamethrowerMinDamage = 7;

        private const int FlamethrowerMaxDamage = 17;

        private const int RobotMinDamage = 1;

        private const int RobotMaxDamage = 3;

        private const double RobotRechargeSeconds = 4.0;

        // Mike: soft-respawn ~60s after Burning Cleaning Robot death.
        private const double RobotRespawnSeconds = 60.0;

        private const double FlamethrowerAnimRefreshSeconds = 6.0;

        // Capture 20260721-marcus-animation-texture-dialogtext SpecialAttackWeapon on Marcus.
        private const int MarcusSpecialAttackWeaponUnknown1 = 121;

        private const int MarcusSpecialAttackWeaponUnknown2 = 121;

        private const int MarcusSpecialAttackWeaponUnknown3 = 121;

        private const int MarcusSpecialAttackWeaponUnknown4 = 83;

        private const int MarcusSpecialAttackWeaponUnknown5 = 50;

        private const int MarcusFlamethrowerMeshId = 292936;

        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

        private static readonly Dictionary<int, DateTime> NextRobotRespawnUtc = new Dictionary<int, DateTime>();

        private static readonly Dictionary<int, DateTime> NextFlamethrowerAnimUtc = new Dictionary<int, DateTime>();

        private static readonly Dictionary<int, DateTime> NextFireSpellListUtc = new Dictionary<int, DateTime>();

        public static void ClearPlayfield(int playfieldInstance)
        {
            LinkedPlayfields.Remove(playfieldInstance);
            NextRobotRespawnUtc.Remove(playfieldInstance);
            NextFlamethrowerAnimUtc.Remove(playfieldInstance);
            NextFireSpellListUtc.Remove(playfieldInstance);
        }

        public static void StartForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId
                || !LinkedPlayfields.Add(playfieldIdentity.Instance))
            {
                return;
            }

            Character marcus = FindNamedNpc(playfield, MarcusName);
            if (marcus == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "MarcusPadAmbientCombat: Marcus Stone not found pf=" + playfieldIdentity.Instance);
                return;
            }

            Character robot = SpawnBurningRobot(playfield, playfieldIdentity, activateNpc);
            if (robot == null)
            {
                return;
            }

            LinkFight(playfield, marcus, robot);
            CapturedSpellListVisualEffects.AnnounceBurningRobotFire(robot);
            NextFireSpellListUtc[playfieldIdentity.Instance] =
                DateTime.UtcNow + TimeSpan.FromSeconds(CapturedSpellListVisualEffects.BurningFireIntervalSeconds);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "MarcusPadAmbientCombat linked Marcus="
                + marcus.Identity.ToString(true)
                + " robot="
                + robot.Identity.ToString(true)
                + " source=20260720-064523");
        }

        public static void TickRespawn(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId
                || !LinkedPlayfields.Contains(playfieldIdentity.Instance))
            {
                return;
            }

            Character robot = FindNamedNpc(playfield, BurningRobotName);
            if (robot != null && robot.Stats[StatIds.health].Value > 0)
            {
                NextRobotRespawnUtc.Remove(playfieldIdentity.Instance);
                Character marcus = FindNamedNpc(playfield, MarcusName);
                if (marcus != null && marcus.Stats[StatIds.health].Value > 0)
                {
                    if (marcus.FightingTarget.Instance == 0
                        || marcus.FightingTarget.Instance != robot.Identity.Instance)
                    {
                        LinkFight(playfield, marcus, robot);
                        CapturedSpellListVisualEffects.AnnounceBurningRobotFire(robot);
                        NextFireSpellListUtc[playfieldIdentity.Instance] =
                            DateTime.UtcNow
                            + TimeSpan.FromSeconds(CapturedSpellListVisualEffects.BurningFireIntervalSeconds);
                        return;
                    }
                }

                MaybeRefreshFlamethrowerAnim(playfield, playfieldIdentity, marcus, robot);
                MaybeRefreshBurningFireSpellList(playfieldIdentity, robot);
                return;
            }

            DateTime nextRespawn;
            if (!NextRobotRespawnUtc.TryGetValue(playfieldIdentity.Instance, out nextRespawn))
            {
                NextRobotRespawnUtc[playfieldIdentity.Instance] =
                    DateTime.UtcNow + TimeSpan.FromSeconds(RobotRespawnSeconds);
                return;
            }

            if (nextRespawn > DateTime.UtcNow)
            {
                return;
            }

            Character marcusForRespawn = FindNamedNpc(playfield, MarcusName);
            if (marcusForRespawn == null)
            {
                return;
            }

            Character spawned = SpawnBurningRobot(playfield, playfieldIdentity, activateNpc);
            if (spawned == null)
            {
                return;
            }

            LinkFight(playfield, marcusForRespawn, spawned);
            NextRobotRespawnUtc.Remove(playfieldIdentity.Instance);
            CapturedSpellListVisualEffects.AnnounceBurningRobotFire(spawned);
            NextFireSpellListUtc[playfieldIdentity.Instance] =
                DateTime.UtcNow + TimeSpan.FromSeconds(CapturedSpellListVisualEffects.BurningFireIntervalSeconds);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "MarcusPadAmbientCombat respawned robot="
                + spawned.Identity.ToString(true)
                + " source=20260720-064523");
        }

        private static void MaybeRefreshFlamethrowerAnim(
            Playfield playfield,
            Identity playfieldIdentity,
            Character marcus,
            Character robot)
        {
            if (marcus == null || robot == null)
            {
                return;
            }

            DateTime nextAnim;
            if (NextFlamethrowerAnimUtc.TryGetValue(playfieldIdentity.Instance, out nextAnim)
                && nextAnim > DateTime.UtcNow)
            {
                return;
            }

            EnsureMarcusFlamethrowerWeaponMesh(marcus);
            // Capture: SpecialAttackWeapon (texture VFX) then AttackInfo WeaponSlot=6 (~6s cadence).
            AnnounceMarcusFlamethrowerTextureVfx(playfield, marcus);
            playfield.Announce(
                new AttackInfoMessage
                {
                    Identity = marcus.Identity,
                    Unknown = 0,
                    Target = robot.Identity,
                    Unknown1 = 7,
                    Unknown2 = 0,
                    Unknown3 = 6,
                    Unknown4 = 0,
                    Unknown5 = 3,
                    Unknown6 = 0
                });
            NextFlamethrowerAnimUtc[playfieldIdentity.Instance] =
                DateTime.UtcNow + TimeSpan.FromSeconds(FlamethrowerAnimRefreshSeconds);
        }

        private static void MaybeRefreshBurningFireSpellList(Identity playfieldIdentity, Character robot)
        {
            if (robot == null || robot.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            DateTime nextFire;
            if (NextFireSpellListUtc.TryGetValue(playfieldIdentity.Instance, out nextFire)
                && nextFire > DateTime.UtcNow)
            {
                return;
            }

            CapturedSpellListVisualEffects.AnnounceBurningRobotFire(robot);
            NextFireSpellListUtc[playfieldIdentity.Instance] =
                DateTime.UtcNow
                + TimeSpan.FromSeconds(CapturedSpellListVisualEffects.BurningFireIntervalSeconds);
        }

        private static void LinkFight(Playfield playfield, Character marcus, Character robot)
        {
            string failure;
            CapturedEnemyCombatRuntime.Prepare(
                marcus,
                marcus.Controller as NPCController,
                CreateMarcusFlamethrowerContract(),
                out failure);
            if (!string.IsNullOrEmpty(failure))
            {
                LogUtil.Debug(DebugInfoDetail.Error, "MarcusPadAmbientCombat Marcus combat prepare: " + failure);
            }

            CapturedEnemyCombatRuntime.Prepare(
                robot,
                robot.Controller as NPCController,
                CreateBurningRobotContract(),
                out failure);
            if (!string.IsNullOrEmpty(failure))
            {
                LogUtil.Debug(DebugInfoDetail.Error, "MarcusPadAmbientCombat robot combat prepare: " + failure);
            }

            EnsureMarcusFlamethrowerWeaponMesh(marcus);
            marcus.SetFightingTarget(robot.Identity);
            robot.SetFightingTarget(marcus.Identity);
            playfield.ResetCombatTick(marcus.Identity);
            playfield.ResetCombatTick(robot.Identity);
            // Capture 20260721: SpecialAttackWeapon then Attack (Marcus texture VFX 121/121/121/83/50).
            AnnounceMarcusFlamethrowerTextureVfx(playfield, marcus);
            playfield.Announce(
                new AttackMessage
                {
                    Identity = marcus.Identity,
                    Unknown = 0,
                    Target = robot.Identity,
                    Action = 0
                });
            playfield.Announce(
                new AttackInfoMessage
                {
                    Identity = marcus.Identity,
                    Unknown = 0,
                    Target = robot.Identity,
                    Unknown1 = 7,
                    Unknown2 = 0,
                    Unknown3 = 6,
                    Unknown4 = 0,
                    Unknown5 = 3,
                    Unknown6 = 0
                });
            playfield.Announce(
                new AttackMessage
                {
                    Identity = robot.Identity,
                    Unknown = 0,
                    Target = marcus.Identity,
                    Action = 0
                });
        }

        private static void AnnounceMarcusFlamethrowerTextureVfx(Playfield playfield, Character marcus)
        {
            if (playfield == null || marcus == null)
            {
                return;
            }

            playfield.Announce(
                new SpecialAttackWeaponMessage
                {
                    Identity = marcus.Identity,
                    Unknown = 0,
                    Specials = new SpecialAttack[0],
                    Unknown1 = MarcusSpecialAttackWeaponUnknown1,
                    Unknown2 = MarcusSpecialAttackWeaponUnknown2,
                    Unknown3 = MarcusSpecialAttackWeaponUnknown3,
                    Unknown4 = MarcusSpecialAttackWeaponUnknown4,
                    Unknown5 = MarcusSpecialAttackWeaponUnknown5
                });
        }

        private static void EnsureMarcusFlamethrowerWeaponMesh(Character marcus)
        {
            if (marcus == null)
            {
                return;
            }

            // AttackInfo WeaponSlot=6 (Righthand) needs WeaponMeshRight for client texture animation.
            if (marcus.Stats[StatIds.weaponmeshright].Value != MarcusFlamethrowerMeshId)
            {
                marcus.Stats.SetBaseValueWithoutTriggering(
                    (int)StatIds.weaponmeshright,
                    (uint)MarcusFlamethrowerMeshId);
                marcus.Stats[StatIds.weaponmeshright].Value = MarcusFlamethrowerMeshId;
            }

            AOMeshs existing = marcus.MeshLayer.GetMeshAtPosition(1);
            if (existing == null || existing.Mesh != MarcusFlamethrowerMeshId)
            {
                if (existing != null && existing.Mesh > 0 && existing.Mesh != 1234567890)
                {
                    marcus.MeshLayer.RemoveMesh(
                        existing.Position,
                        existing.Mesh,
                        existing.OverrideTexture,
                        existing.Layer);
                }

                marcus.MeshLayer.AddMesh(1, MarcusFlamethrowerMeshId, 0, 2);
                marcus.SocialMeshLayer.AddMesh(1, MarcusFlamethrowerMeshId, 0, 2);
            }
        }

        private static CapturedEnemyCombatContract CreateMarcusFlamethrowerContract()
        {
            CapturedEnemyCombatAttackDefinition repeatingAttack = new CapturedEnemyCombatAttackDefinition(
                FlamethrowerMinDamage,
                FlamethrowerMaxDamage,
                0,
                FlamethrowerRange,
                FlamethrowerRechargeSeconds,
                false,
                0,
                6,
                0,
                3,
                0,
                0,
                true);
            // Capture 20260721-marcus-animation-texture-dialogtext:
            // SpecialAttackWeapon Specials=[] Unknown1/2/3=121 Unknown4=83 Unknown5=50
            // (texture animation VFX on Marcus while fighting the Burning Cleaning Robot).
            return CapturedEnemyCombatContract.CapturedSpecialSequence(
                "20260721-marcus-animation-texture-dialogtext Marcus SpecialAttackWeapon 121/121/121/83/50 + AttackInfo WeaponSlot=6",
                new CapturedEnemySpecialAttackSequenceDefinition(
                    0.5,
                    null,
                    repeatingAttack,
                    new CapturedEnemySpecialAttackDefinition[0],
                    MarcusSpecialAttackWeaponUnknown1,
                    MarcusSpecialAttackWeaponUnknown2,
                    MarcusSpecialAttackWeaponUnknown3,
                    MarcusSpecialAttackWeaponUnknown4,
                    MarcusSpecialAttackWeaponUnknown5,
                    0,
                    0,
                    0));
        }

        private static CapturedEnemyCombatContract CreateBurningRobotContract()
        {
            CapturedEnemyCombatAttackDefinition repeatingAttack = new CapturedEnemyCombatAttackDefinition(
                RobotMinDamage,
                RobotMaxDamage,
                0,
                FlamethrowerRange,
                RobotRechargeSeconds,
                false,
                -1,
                0,
                0,
                3,
                0,
                0,
                false);
            return CapturedEnemyCombatContract.CapturedSpecialSequence(
                "20260720-064523 Burning Cleaning Robot SpecialAttackWeapon 43/43/43/3/0 + Attack Marcus",
                new CapturedEnemySpecialAttackSequenceDefinition(
                    0.2,
                    null,
                    repeatingAttack,
                    new[] { new CapturedEnemySpecialAttackDefinition(43, 43, 43, string.Empty) },
                    43,
                    43,
                    43,
                    3,
                    0,
                    0,
                    0,
                    0));
        }

        private static Character SpawnBurningRobot(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            NPCController controller = new NPCController { AiProfile = NpcAiProfile.Passive };
            Character robot = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                "A004",
                playfieldIdentity,
                new Coordinate { x = RobotX, y = RobotY, z = RobotZ },
                new Quaternion(0.0, 0.9414477, 0.0, 0.3371589),
                controller,
                RobotLevel);
            if (robot == null)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "MarcusPadAmbientCombat: Burning Cleaning Robot spawn failed");
                return null;
            }

            robot.Name = BurningRobotName;
            robot.Playfield = playfield;
            CombatTestMobArchetype.Prepare(robot, CombatTestMobArchetype.MalfunctioningCleaningRobot);
            robot.Name = BurningRobotName;
            robot.Stats[StatIds.life].Value = RobotHealth;
            robot.Stats[StatIds.life].BaseValue = (uint)RobotHealth;
            robot.Stats[StatIds.health].Value = RobotHealth;
            robot.Stats[StatIds.health].BaseValue = (uint)RobotHealth;
            robot.Stats[StatIds.level].Value = RobotLevel;
            robot.Stats[StatIds.level].BaseValue = (uint)RobotLevel;
            robot.Stats[StatIds.monsterscale].Value = RobotScale;
            robot.Stats[StatIds.monsterscale].BaseValue = (uint)RobotScale;
            robot.Stats[StatIds.flags].Value = RobotCharacterFlags;
            robot.Stats[StatIds.flags].BaseValue = (uint)RobotCharacterFlags;
            robot.Stats[StatIds.visualflags].Value = 31;
            robot.Stats[StatIds.visualflags].BaseValue = 31u;
            robot.Coordinates(new Coordinate { x = RobotX, y = RobotY, z = RobotZ });
            robot.DoNotDoTimers = false;
            activateNpc(robot);
            playfield.AnnounceSpawnedCharacterVisibility(robot, Identity.None);
            return robot;
        }

        private static Character FindNamedNpc(Playfield playfield, string name)
        {
            if (playfield == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller == null
                    || candidate.Controller is PlayerController
                    || !string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return candidate as Character;
            }

            return null;
        }
    }
}
