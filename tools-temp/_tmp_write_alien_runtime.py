# -*- coding: utf-8 -*-
from pathlib import Path

root = Path(r"C:\Users\nermi\source\repos\AORebirth")
slots = root / "tools-temp" / "_tmp_tll_alien_slots.csfrag"
out = root / "AORebirth" / "Server" / "ZoneEngine" / "Core" / "Playfields" / "AreteAlienAreaMobRuntime.cs"

slot_lines = []
for line in slots.read_text(encoding="utf-8").splitlines():
    if "new MobSlot" in line:
        slot_lines.append(line.rstrip())
slot_block = "\n".join(slot_lines)

cs = r'''namespace ZoneEngine.Core.Playfields
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
    using ZoneEngine.Core.Missions;

    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture 20260726-spawn-mob-tll-alien: Alien Spider - Zix, Angry Minibull / Harvey,
    /// Saltworm, and extra Rollerrats east of the Lorelei oasis cluster.
    /// Oasis Desert Reet / Lolly / local Rollerrats / Gnarl remain in LoreleiOasisMobRuntime.
    /// </summary>
    internal static class AreteAlienAreaMobRuntime
    {
        private enum MobKind
        {
            Spider,

            Saltworm,

            Rollerrat,

            Minibull
        }

        private sealed class MobSlot
        {
            public string Name { get; private set; }

            public MobKind Kind { get; private set; }

            public int MonsterData { get; private set; }

            public int Level { get; private set; }

            public int Health { get; private set; }

            public int NpcFamily { get; private set; }

            public int Scale { get; private set; }

            public int RunSpeed { get; private set; }

            public NpcAiProfile AiProfile { get; private set; }

            public float AggroRadiusMeters { get; private set; }

            public float X { get; private set; }

            public float Y { get; private set; }

            public float Z { get; private set; }

            public MobSlot(
                string name,
                MobKind kind,
                int monsterData,
                int level,
                int health,
                int npcFamily,
                int scale,
                int runSpeed,
                NpcAiProfile aiProfile,
                float aggroRadiusMeters,
                float x,
                float y,
                float z)
            {
                this.Name = name;
                this.Kind = kind;
                this.MonsterData = monsterData;
                this.Level = level;
                this.Health = health;
                this.NpcFamily = npcFamily;
                this.Scale = scale;
                this.RunSpeed = runSpeed;
                this.AiProfile = aiProfile;
                this.AggroRadiusMeters = aggroRadiusMeters;
                this.X = x;
                this.Y = y;
                this.Z = z;
            }
        }

        private const int AreteLandingPlayfieldId = 6553;

        private const int MissingVisualId = 1234567890;

        // Capture complete respawns ~40–152s; use 60s soft timer until more complete rows.
        private const double RespawnSeconds = 60.0;

        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

        private static readonly Dictionary<int, DateTime[]> NextRespawnUtcBySlot = new Dictionary<int, DateTime[]>();

        private static readonly object AggroGate = new object();

        private static readonly Dictionary<int, float> AggroRadiusByNpcInstance = new Dictionary<int, float>();

        // Capture 20260726-spawn-mob-tll-alien enemy-dossier clustered slots (oasis rats excluded).
        private static readonly MobSlot[] Slots =
            {
''' + slot_block + r'''
            };

        public static ICharacter FindAutomaticAggroTarget(ICharacter npc)
        {
            if (npc == null || npc.Playfield == null || npc.Stats[StatIds.health].Value <= 0)
            {
                return null;
            }

            if (npc.FightingTarget.Instance != 0)
            {
                return null;
            }

            float radius;
            lock (AggroGate)
            {
                if (!AggroRadiusByNpcInstance.TryGetValue(npc.Identity.Instance, out radius) || radius <= 0f)
                {
                    return null;
                }
            }

            Playfield playfield = npc.Playfield as Playfield;
            if (playfield == null || npc.RawCoordinates == null)
            {
                return null;
            }

            Coordinate npcCoord = npc.Coordinates();
            ICharacter best = null;
            double bestDistance = radius;
            List<ICharacter> inRange = playfield.FindCharacterInRange(npc, radius);
            for (int i = 0; i < inRange.Count; i++)
            {
                ICharacter candidate = inRange[i];
                if (candidate == null
                    || candidate.Identity.Instance == npc.Identity.Instance
                    || !(candidate.Controller is PlayerController)
                    || candidate.Stats[StatIds.health].Value <= 0
                    || candidate.RawCoordinates == null)
                {
                    continue;
                }

                double distance = candidate.Coordinates().Distance3D(npcCoord);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
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

            NextRespawnUtcBySlot[playfieldIdentity.Instance] = new DateTime[Slots.Length];
            DateTime[] timers = NextRespawnUtcBySlot[playfieldIdentity.Instance];
            int spawned = 0;
            for (int i = 0; i < Slots.Length; i++)
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
                        "AreteAlienAreaMobRuntime slot=" + i + " failed: "
                        + ex.GetType().Name + ": " + ex.Message);
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "AreteAlienAreaMobRuntime spawned="
                + spawned
                + "/"
                + Slots.Length
                + " pf="
                + playfieldIdentity.Instance
                + " source=20260726-spawn-mob-tll-alien");
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
            lock (AggroGate)
            {
                AggroRadiusByNpcInstance.Clear();
            }
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
                || timers.Length != Slots.Length)
            {
                timers = new DateTime[Slots.Length];
                NextRespawnUtcBySlot[playfieldIdentity.Instance] = timers;
            }

            for (int i = 0; i < Slots.Length; i++)
            {
                if (HasLivingMobNear(playfield, Slots[i]))
                {
                    timers[i] = DateTime.MaxValue;
                }
                else if (timers[i] == DateTime.MaxValue)
                {
                    timers[i] = DateTime.UtcNow + TimeSpan.FromSeconds(RespawnSeconds);
                }
                else if (!(timers[i] > DateTime.UtcNow)
                         && SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)
                {
                    timers[i] = DateTime.MaxValue;
                }
            }
        }

        private static Character SpawnSlot(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            int slotIndex)
        {
            MobSlot slot = Slots[slotIndex];
            NPCController controller = new NPCController { AiProfile = slot.AiProfile };
            string templateHash = ResolveTemplateHash(slot);
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                templateHash,
                playfieldIdentity,
                new Coordinate { x = slot.X, y = slot.Y, z = slot.Z },
                new Quaternion(0.0, 0.0, 0.0, 1.0),
                controller,
                slot.Level);
            if (mob == null)
            {
                return null;
            }

            mob.Name = slot.Name;
            mob.Playfield = playfield;
            PrepareArchetype(mob, slot);
            mob.Name = slot.Name;
            ApplyCaptureStats(mob, slot);
            controller.AiProfile = slot.AiProfile;

            int minDamage;
            int maxDamage;
            ResolveCaptureDamage(slot, out minDamage, out maxDamage);
            CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttackOnSight(
                "arete-alien-area-20260726-spawn-mob-tll-alien",
                minDamage,
                maxDamage,
                2.0,
                0,
                0,
                1279612721,
                0,
                0,
                0,
                0,
                0,
                0);
            string unused;
            CapturedEnemyCombatRuntime.Prepare(mob, controller, contract, out unused);
            controller.AiProfile = slot.AiProfile;
            mob.Coordinates(new Coordinate { x = slot.X, y = slot.Y, z = slot.Z });
            mob.DoNotDoTimers = false;
            activateNpc(mob);
            if (slot.AggroRadiusMeters > 0f)
            {
                RegisterAggro(mob.Identity.Instance, slot.AggroRadiusMeters);
                MissionInstanceMobCombat.RegisterAggressive(mob.Identity);
            }

            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return mob;
        }

        private static string ResolveTemplateHash(MobSlot slot)
        {
            switch (slot.Kind)
            {
                case MobKind.Spider:
                    return CombatTestMobArchetype.AlienSpiderZix.TemplateHash;
                case MobKind.Rollerrat:
                    return CombatTestMobArchetype.StowawayRollerrat.TemplateHash;
                default:
                    return CombatTestMobArchetype.TemplateHash;
            }
        }

        private static void PrepareArchetype(Character mob, MobSlot slot)
        {
            switch (slot.Kind)
            {
                case MobKind.Spider:
                    CombatTestMobArchetype.Prepare(mob, CombatTestMobArchetype.AlienSpiderZix);
                    return;
                case MobKind.Rollerrat:
                    CombatTestMobArchetype.Prepare(mob, CombatTestMobArchetype.StowawayRollerrat);
                    return;
                default:
                    CombatTestMobArchetype.Prepare(mob, CombatTestMobArchetype.IslandReet);
                    return;
            }
        }

        private static void ResolveCaptureDamage(MobSlot slot, out int minDamage, out int maxDamage)
        {
            // Capture 20260726-spawn-mob-tll-alien AttackInfo Amounts vs local player.
            switch (slot.Kind)
            {
                case MobKind.Saltworm:
                    minDamage = 12;
                    maxDamage = 21;
                    return;
                case MobKind.Minibull:
                    minDamage = 5;
                    maxDamage = 12;
                    return;
                case MobKind.Rollerrat:
                    minDamage = 5;
                    maxDamage = 10;
                    return;
                case MobKind.Spider:
                default:
                    minDamage = 6;
                    maxDamage = 10;
                    return;
            }
        }

        private static int ResolveCaptureXp(MobSlot slot)
        {
            // Capture Stat XP deltas after kills (side-bonus tips excluded).
            if (string.Equals(slot.Name, "Harvey the Bully", StringComparison.OrdinalIgnoreCase))
            {
                return 890;
            }

            switch (slot.Kind)
            {
                case MobKind.Saltworm:
                    return 830;
                case MobKind.Minibull:
                    return slot.Level >= 10 ? 890 : 830;
                case MobKind.Rollerrat:
                    return 1;
                case MobKind.Spider:
                default:
                    return 400;
            }
        }

        private static void ApplyCaptureStats(Character mob, MobSlot slot)
        {
            SetStat(mob, StatIds.monsterdata, slot.MonsterData);
            SetStat(mob, StatIds.life, slot.Health);
            SetStat(mob, StatIds.health, slot.Health);
            SetStat(mob, StatIds.level, slot.Level);
            SetStat(mob, StatIds.npcfamily, slot.NpcFamily);
            SetStat(mob, StatIds.monsterscale, slot.Scale);
            SetStat(mob, StatIds.runspeed, slot.RunSpeed);
            SetStat(mob, StatIds.flags, 268964353);
            SetStat(mob, StatIds.visualflags, 31);
            SetStat(mob, StatIds.side, 3);
            SetStat(mob, StatIds.breed, 6);
            SetStat(mob, StatIds.sex, 1);
            SetStat(mob, StatIds.race, 1);
            SetStat(mob, StatIds.fatness, 1);
            SetStat(mob, StatIds.xp, ResolveCaptureXp(slot));
            int minDamage;
            int maxDamage;
            ResolveCaptureDamage(slot, out minDamage, out maxDamage);
            SetStat(mob, StatIds.mindamage, minDamage);
            SetStat(mob, StatIds.maxdamage, maxDamage);
            SetStat(mob, StatIds.damagetype, 1);
            SetStat(mob, StatIds.defaultattacktype, 1);
            if (slot.Kind == MobKind.Spider)
            {
                SetStat(mob, StatIds.catmesh, CombatTestMobArchetype.AlienSpiderZix.CorpseCatMesh);
                SetStat(mob, StatIds.displaycatmesh, CombatTestMobArchetype.AlienSpiderZix.CorpseCatMesh);
            }
            else if (slot.Kind == MobKind.Rollerrat)
            {
                SetStat(mob, StatIds.catmesh, CombatTestMobArchetype.StowawayRollerrat.CorpseCatMesh);
                SetStat(mob, StatIds.displaycatmesh, CombatTestMobArchetype.StowawayRollerrat.CorpseCatMesh);
            }
            else
            {
                SetStat(mob, StatIds.catmesh, MissingVisualId);
                SetStat(mob, StatIds.displaycatmesh, MissingVisualId);
            }

            if (mob.Textures != null)
            {
                mob.Textures.Clear();
                for (int i = 0; i < 5; i++)
                {
                    mob.Textures.Add(new AORebirth.Core.Textures.AOTextures(i, 0));
                }
            }

            if (mob.MeshLayer != null)
            {
                mob.MeshLayer.Clear();
            }

            if (mob.SocialMeshLayer != null)
            {
                mob.SocialMeshLayer.Clear();
            }
        }

        private static bool HasLivingMobNear(Playfield playfield, MobSlot slot)
        {
            if (playfield == null)
            {
                return false;
            }

            foreach (ICharacter character in playfield.FindInstancedCharactersInPlayfield())
            {
                if (character == null
                    || !(character.Controller is NPCController)
                    || character.Stats[StatIds.health].Value <= 0
                    || character.RawCoordinates == null
                    || !string.Equals(character.Name, slot.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Coordinate c = character.Coordinates();
                double dx = c.x - slot.X;
                double dz = c.z - slot.Z;
                if ((dx * dx) + (dz * dz) < (3.0 * 3.0))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RegisterAggro(int npcInstance, float radiusMeters)
        {
            lock (AggroGate)
            {
                AggroRadiusByNpcInstance[npcInstance] = radiusMeters;
            }
        }

        private static void SetStat(Character mob, StatIds stat, int value)
        {
            if (mob == null || mob.Stats == null || mob.Stats[stat] == null)
            {
                return;
            }

            mob.Stats[stat].Value = value;
            mob.Stats[stat].BaseValue = (uint)value;
        }
    }
}
'''

out.write_text(cs, encoding="utf-8")
print("Wrote", out, "bytes", out.stat().st_size)
