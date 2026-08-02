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

    /// <summary>
    /// Capture-backed Marcus Stone vs Burning Cleaning Robot ambient fight.
    /// Capture 20260731-174302: standing flamethrower (SAW 121/121/121/83/50 + AttackInfo slot 6).
    /// Driven here directly — NpcCombatTickCoordinator path-nav gates block this ranged pad fight.
    /// </summary>
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

        // Capture 20260731-174302: AttackInfo cadence ~6.25s (15:44:15 → 21 → 27 → 33).
        private const double FlamethrowerRechargeSeconds = 6.25;

        // Capture 20260731-174302 AttackInfo Amount sequence: 13, 23, 12, 23.
        private static readonly int[] FlamethrowerDamageObservations = { 13, 23, 12, 23 };

        // Capture 20260731-174302: SAW+Attack at 15:44:12.28, first AttackInfo at 15:44:15.25.
        private const double FlamethrowerInitialAttackDelaySeconds = 3.0;

        private const int RobotMinDamage = 1;

        private const int RobotMaxDamage = 3;

        private const double RobotRechargeSeconds = 4.0;

        private const double RobotInitialAttackDelaySeconds = 0.2;

        // Mike: soft-respawn ~60s after Burning Cleaning Robot death.
        private const double RobotRespawnSeconds = 60.0;

        private const int MarcusSpecialAttackWeaponUnknown1 = 121;

        private const int MarcusSpecialAttackWeaponUnknown2 = 121;

        private const int MarcusSpecialAttackWeaponUnknown3 = 121;

        private const int MarcusSpecialAttackWeaponUnknown4 = 83;

        private const int MarcusSpecialAttackWeaponUnknown5 = 50;

        private const int MarcusFlamethrowerMeshId = 292936;

        private const int NormalAttackInfoHitType = 3;

        private static readonly CapturedEnemyCombatContract MarcusCombatContract =
            CapturedEnemyCombatContract.CapturedSpecialSequence(
                "20260731-174302: Marcus Stone standing flamethrower packet sequence",
                new CapturedEnemySpecialAttackSequenceDefinition(
                    FlamethrowerInitialAttackDelaySeconds,
                    null,
                    new CapturedEnemyCombatAttackDefinition(
                        12,
                        23,
                        0,
                        11.0d,
                        FlamethrowerRechargeSeconds,
                        false,
                        0,
                        6,
                        0,
                        NormalAttackInfoHitType,
                        0,
                        0,
                        true,
                        FlamethrowerDamageObservations),
                    new CapturedEnemySpecialAttackDefinition[0],
                    MarcusSpecialAttackWeaponUnknown1,
                    MarcusSpecialAttackWeaponUnknown2,
                    MarcusSpecialAttackWeaponUnknown3,
                    MarcusSpecialAttackWeaponUnknown4,
                    MarcusSpecialAttackWeaponUnknown5,
                    0,
                    0,
                    0));

        private static readonly CapturedEnemyCombatContract RobotCombatContract =
            CapturedEnemyCombatContract.CapturedSpecialSequence(
                "20260731-174302: Burning Cleaning Robot ambient return attack packet sequence",
                new CapturedEnemySpecialAttackSequenceDefinition(
                    RobotInitialAttackDelaySeconds,
                    null,
                    new CapturedEnemyCombatAttackDefinition(
                        RobotMinDamage,
                        RobotMaxDamage,
                        0,
                        NpcCombatAttackRules.MaxMeleeCombatDistance,
                        RobotRechargeSeconds,
                        false,
                        -1,
                        0,
                        0,
                        NormalAttackInfoHitType,
                        0,
                        0,
                        true),
                    new[]
                    {
                        new CapturedEnemySpecialAttackDefinition(43, 43, 43, string.Empty)
                    },
                    43,
                    43,
                    43,
                    3,
                    0,
                    0,
                    0,
                    0));

        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

        private static readonly Dictionary<int, DateTime> NextRobotRespawnUtc = new Dictionary<int, DateTime>();

        private static readonly Dictionary<int, DateTime> NextFireSpellListUtc = new Dictionary<int, DateTime>();

        private static readonly Dictionary<int, DateTime> NextMarcusAttackUtc = new Dictionary<int, DateTime>();

        private static readonly Dictionary<int, DateTime> NextRobotAttackUtc = new Dictionary<int, DateTime>();

        private static readonly Dictionary<int, int> MarcusDamageCursor = new Dictionary<int, int>();

        private static readonly Dictionary<int, long> LastTickHeartbeat = new Dictionary<int, long>();

        /// <summary>
        /// Capture fight is standing ranged — Marcus stays put and uses weapon slot 6.
        /// Combat/patrol ticks must not chase either actor toward the other.
        /// </summary>
        public static bool IsStandingPadAmbientCombatant(ICharacter character)
        {
            if (character == null || string.IsNullOrEmpty(character.Name))
            {
                return false;
            }

            return string.Equals(character.Name, MarcusName, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(character.Name, BurningRobotName, StringComparison.OrdinalIgnoreCase);
        }

        public static void ClearPlayfield(int playfieldInstance)
        {
            LinkedPlayfields.Remove(playfieldInstance);
            NextRobotRespawnUtc.Remove(playfieldInstance);
            NextFireSpellListUtc.Remove(playfieldInstance);
            NextMarcusAttackUtc.Remove(playfieldInstance);
            NextRobotAttackUtc.Remove(playfieldInstance);
            MarcusDamageCursor.Remove(playfieldInstance);
            LastTickHeartbeat.Remove(playfieldInstance);
        }

        public static void StartForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId
                || LinkedPlayfields.Contains(playfieldIdentity.Instance))
            {
                return;
            }

            Character marcus = FindNamedNpc(playfield, MarcusName);
            if (marcus == null)
            {
                // Marcus may spawn slightly after batch start; TickRespawn will link when present.
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "MarcusPadAmbientCombat: Marcus Stone not found yet pf=" + playfieldIdentity.Instance);
                return;
            }

            Character robot = SpawnBurningRobot(playfield, playfieldIdentity, activateNpc);
            if (robot == null)
            {
                return;
            }

            LinkedPlayfields.Add(playfieldIdentity.Instance);
            LinkFight(playfield, playfieldIdentity, marcus, robot);
            CapturedSpellListVisualEffects.AnnounceBurningRobotFire(robot);
            NextFireSpellListUtc[playfieldIdentity.Instance] =
                DateTime.UtcNow + TimeSpan.FromSeconds(CapturedSpellListVisualEffects.BurningFireIntervalSeconds);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "MarcusPadAmbientCombat linked Marcus="
                + marcus.Identity.ToString(true)
                + " robot="
                + robot.Identity.ToString(true)
                + " source=20260731-174302");
        }

        public static void TickRespawn(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId)
            {
                return;
            }

            // ProcessPatrolTick runs per NPC; only drive this ambient fight once per heartbeat.
            long heartbeat = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond / 10;
            long lastHeartbeat;
            if (LastTickHeartbeat.TryGetValue(playfieldIdentity.Instance, out lastHeartbeat)
                && lastHeartbeat == heartbeat)
            {
                return;
            }

            LastTickHeartbeat[playfieldIdentity.Instance] = heartbeat;

            Character marcus = FindNamedNpc(playfield, MarcusName);
            Character robot = FindNamedNpc(playfield, BurningRobotName);

            if (robot != null && robot.Stats[StatIds.health].Value > 0)
            {
                if (!LinkedPlayfields.Contains(playfieldIdentity.Instance))
                {
                    LinkedPlayfields.Add(playfieldIdentity.Instance);
                }

                NextRobotRespawnUtc.Remove(playfieldIdentity.Instance);
                if (marcus != null && marcus.Stats[StatIds.health].Value > 0)
                {
                    if (marcus.FightingTarget.Instance == 0
                        || marcus.FightingTarget.Instance != robot.Identity.Instance
                        || !NextMarcusAttackUtc.ContainsKey(playfieldIdentity.Instance))
                    {
                        LinkFight(playfield, playfieldIdentity, marcus, robot);
                        CapturedSpellListVisualEffects.AnnounceBurningRobotFire(robot);
                        NextFireSpellListUtc[playfieldIdentity.Instance] =
                            DateTime.UtcNow
                            + TimeSpan.FromSeconds(CapturedSpellListVisualEffects.BurningFireIntervalSeconds);
                    }

                    HoldStationary(playfield, marcus);
                    HoldStationary(playfield, robot);
                    ProcessMarcusFlamethrowerAttack(playfield, playfieldIdentity, marcus, robot);
                    ProcessRobotAttack(playfield, playfieldIdentity, marcus, robot);
                    MaybeEnsureMarcusFlamethrowerMesh(marcus);
                    MaybeRefreshBurningFireSpellList(playfieldIdentity, robot);
                }

                return;
            }

            // No living robot — schedule / perform respawn.
            NextMarcusAttackUtc.Remove(playfieldIdentity.Instance);
            NextRobotAttackUtc.Remove(playfieldIdentity.Instance);

            if (marcus == null)
            {
                return;
            }

            DateTime nextRespawn;
            if (!NextRobotRespawnUtc.TryGetValue(playfieldIdentity.Instance, out nextRespawn))
            {
                // Never linked yet → spawn immediately; after a kill wait RobotRespawnSeconds.
                nextRespawn = LinkedPlayfields.Contains(playfieldIdentity.Instance)
                                  ? DateTime.UtcNow + TimeSpan.FromSeconds(RobotRespawnSeconds)
                                  : DateTime.UtcNow;
                NextRobotRespawnUtc[playfieldIdentity.Instance] = nextRespawn;
            }

            if (nextRespawn > DateTime.UtcNow)
            {
                return;
            }

            Character spawned = SpawnBurningRobot(playfield, playfieldIdentity, activateNpc);
            if (spawned == null)
            {
                return;
            }

            LinkedPlayfields.Add(playfieldIdentity.Instance);
            LinkFight(playfield, playfieldIdentity, marcus, spawned);
            NextRobotRespawnUtc.Remove(playfieldIdentity.Instance);
            CapturedSpellListVisualEffects.AnnounceBurningRobotFire(spawned);
            NextFireSpellListUtc[playfieldIdentity.Instance] =
                DateTime.UtcNow + TimeSpan.FromSeconds(CapturedSpellListVisualEffects.BurningFireIntervalSeconds);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "MarcusPadAmbientCombat respawned robot="
                + spawned.Identity.ToString(true)
                + " source=20260731-174302");
        }

        private static void LinkFight(
            Playfield playfield,
            Identity playfieldIdentity,
            Character marcus,
            Character robot)
        {
            if (playfield == null || marcus == null || robot == null)
            {
                return;
            }

            // Do not use CapturedEnemyCombatRuntime / ResetCombatTick here:
            // PF path-nav gates block standing ranged AttackInfo for this pad fight.
            // Stay put — capture shows ~11m flamethrower, not melee chase.
            HoldStationary(playfield, marcus);
            HoldStationary(playfield, robot);
            EnsureMarcusFlamethrowerWeaponMesh(marcus);
            marcus.SetFightingTarget(robot.Identity);
            robot.SetFightingTarget(marcus.Identity);
            FaceToward(marcus, robot);
            FaceToward(robot, marcus);

            AnnounceMarcusFlamethrowerTextureVfx(playfield, marcus);
            playfield.Announce(
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    marcus.Identity,
                    robot.Identity,
                    MarcusCombatContract));
            playfield.Announce(
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    robot.Identity,
                    RobotCombatContract));
            playfield.Announce(
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    robot.Identity,
                    marcus.Identity,
                    RobotCombatContract));

            DateTime now = DateTime.UtcNow;
            NextMarcusAttackUtc[playfieldIdentity.Instance] =
                now + TimeSpan.FromSeconds(FlamethrowerInitialAttackDelaySeconds);
            NextRobotAttackUtc[playfieldIdentity.Instance] =
                now + TimeSpan.FromSeconds(RobotInitialAttackDelaySeconds);
            MarcusDamageCursor[playfieldIdentity.Instance] = 0;

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "MarcusPadAmbientCombat LinkFight self-driven Marcus="
                + marcus.Identity.ToString(true)
                + " robot="
                + robot.Identity.ToString(true));
        }

        private static void ProcessMarcusFlamethrowerAttack(
            Playfield playfield,
            Identity playfieldIdentity,
            Character marcus,
            Character robot)
        {
            DateTime nextAttack;
            if (!NextMarcusAttackUtc.TryGetValue(playfieldIdentity.Instance, out nextAttack)
                || nextAttack > DateTime.UtcNow)
            {
                return;
            }

            EnsureMarcusFlamethrowerWeaponMesh(marcus);

            int cursor;
            if (!MarcusDamageCursor.TryGetValue(playfieldIdentity.Instance, out cursor))
            {
                cursor = 0;
            }

            int damage = FlamethrowerDamageObservations[cursor % FlamethrowerDamageObservations.Length];
            MarcusDamageCursor[playfieldIdentity.Instance] = cursor + 1;

            // Capture AttackInfo: Amount / AmmoCount=0 / WeaponSlot=6 / Unk1=0 / HitType=Normal(3) / WeaponInstance=0
            playfield.Announce(
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    marcus.Identity,
                    robot.Identity,
                    damage,
                    0,
                    MarcusCombatContract.SpecialAttackSequence.RepeatingAttack));

            int currentHealth = robot.Stats[StatIds.health].Value;
            int newHealth = Math.Max(0, currentHealth - damage);
            robot.Stats[StatIds.health].Value = newHealth;
            robot.SendChangedStats();

            NextMarcusAttackUtc[playfieldIdentity.Instance] =
                DateTime.UtcNow + TimeSpan.FromSeconds(FlamethrowerRechargeSeconds);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                "MarcusPadAmbientCombat MarcusAttackInfo dmg="
                + damage
                + " robotHp="
                + newHealth
                + "/"
                + robot.Stats[StatIds.life].Value);

            if (newHealth > 0)
            {
                return;
            }

            NextMarcusAttackUtc.Remove(playfieldIdentity.Instance);
            NextRobotAttackUtc.Remove(playfieldIdentity.Instance);
            playfield.Announce(
                new StopFightMessage
                {
                    Identity = marcus.Identity,
                    Unknown = 0,
                    Unknown1 = 1
                });
            marcus.SetFightingTarget(Identity.None);
            playfield.HandleCombatKillingHit(marcus, robot);
            NextRobotRespawnUtc[playfieldIdentity.Instance] =
                DateTime.UtcNow + TimeSpan.FromSeconds(RobotRespawnSeconds);
        }

        private static void ProcessRobotAttack(
            Playfield playfield,
            Identity playfieldIdentity,
            Character marcus,
            Character robot)
        {
            DateTime nextAttack;
            if (!NextRobotAttackUtc.TryGetValue(playfieldIdentity.Instance, out nextAttack)
                || nextAttack > DateTime.UtcNow
                || marcus.Stats[StatIds.health].Value <= 0
                || robot.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            int damage = RobotMinDamage
                         + ((int)(DateTime.UtcNow.Ticks & 0xffff) % (RobotMaxDamage - RobotMinDamage + 1));
            playfield.Announce(
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    robot.Identity,
                    marcus.Identity,
                    damage,
                    -1,
                    RobotCombatContract.SpecialAttackSequence.RepeatingAttack));

            // Marcus is effectively immortal for this ambient demo (117800 HP); still apply tiny chips.
            int currentHealth = marcus.Stats[StatIds.health].Value;
            marcus.Stats[StatIds.health].Value = Math.Max(1, currentHealth - damage);
            marcus.SendChangedStats();

            NextRobotAttackUtc[playfieldIdentity.Instance] =
                DateTime.UtcNow + TimeSpan.FromSeconds(RobotRechargeSeconds);
        }

        private static void MaybeEnsureMarcusFlamethrowerMesh(Character marcus)
        {
            EnsureMarcusFlamethrowerWeaponMesh(marcus);
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

        private static void AnnounceMarcusFlamethrowerTextureVfx(Playfield playfield, Character marcus)
        {
            if (playfield == null || marcus == null)
            {
                return;
            }

            playfield.Announce(
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    marcus.Identity,
                    MarcusCombatContract));
        }

        private static void EnsureMarcusFlamethrowerWeaponMesh(Character marcus)
        {
            if (marcus == null)
            {
                return;
            }

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

        private static void HoldStationary(Playfield playfield, Character character)
        {
            if (character == null)
            {
                return;
            }

            NPCController npcController = character.Controller as NPCController;
            if (npcController != null)
            {
                npcController.StopFollow();
                npcController.SnapshotCurrentMotionPosition();
                npcController.State = CharacterState.Fighting;
            }

            if (playfield != null)
            {
                playfield.ClearNpcCombatTracking(character.Identity);
            }
        }

        private static void FaceToward(Character character, Character target)
        {
            if (character == null || target == null)
            {
                return;
            }

            AORebirth.Core.Vector.Vector3 from = character.RawCoordinates;
            AORebirth.Core.Vector.Vector3 to = target.RawCoordinates;
            if (from.Distance2D(to) < 0.001)
            {
                return;
            }

            AORebirth.Core.Vector.Vector3 direction = to - from;
            direction.y = 0;
            character.Heading = (Quaternion)Quaternion.GenerateRotationFromDirectionVector(direction.Normalize());
            character.RawHeading = character.Heading;
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
