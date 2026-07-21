namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Textures;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture-backed RK mission interior population from
    /// <c>20260719-5-different-shape-fo-mish</c> (3 distinct shapes).
    /// Spawns trash + type-specific objective (Kill boss / Find target / Broken Machine).
    /// Skips Crat pet SCFU pollution from capture (Carlo Pinnetti + CEO Guardian).
    /// </summary>
    internal static class MissionInstanceSpawn
    {
        private const string TemplateHash = "BART";

        // Capture 20260719-5-different-shape-fo-mish: both pet=True on the capturing Crat.
        private const int CarloPetMonsterData = 258209;

        private const int CeoGuardianPetMonsterData = 227701;

        public static void SpawnForPlayfield(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null || activateNpc == null)
            {
                return;
            }

            if (!MissionInstanceService.IsMissionInstancePlayfield(playfieldIdentity.Instance))
            {
                return;
            }

            MissionShape shape = MissionInstanceShapeCatalog.PickShape(
                playfieldIdentity.Instance,
                new Random(playfieldIdentity.Instance));
            if (shape == null || shape.Npcs == null || shape.Npcs.Length == 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "MissionInstanceSpawn no shape for pf=" + playfieldIdentity.Instance);
                return;
            }

            MissionRollType objective = ResolveObjectiveType(playfieldIdentity.Instance);
            MissionNpc killTarget = null;
            if (objective == MissionRollType.KillPerson)
            {
                killTarget = PickKillTargetFromTrash(shape);
            }

            int spawned = 0;
            bool spawnedObjective = false;
            Character findItemHost = null;

            foreach (MissionNpc def in shape.Npcs)
            {
                if (def == null || IsPlayerPetCapture(def))
                {
                    continue;
                }

                if (!ShouldSpawnNpc(def, objective, killTarget, ref spawnedObjective))
                {
                    continue;
                }

                Character mob;
                if (SpawnOne(
                    playfield,
                    playfieldIdentity,
                    activateNpc,
                    def,
                    objective,
                    killTarget,
                    out mob))
                {
                    spawned++;
                    if (objective == MissionRollType.FindItem
                        && findItemHost == null
                        && def.Role == MissionNpcRole.FindTarget)
                    {
                        findItemHost = mob;
                    }
                }
            }

            // FindItem: if no named FindTarget, put the item on first trash.
            if (objective == MissionRollType.FindItem && findItemHost == null)
            {
                // Re-scan spawned is hard; stamp on next spawn pass — spawn a small host near spawn.
                var hostDef = new MissionNpc
                              {
                                  Name = "Mission Cache",
                                  Role = MissionNpcRole.Trash,
                                  Level = 150,
                                  Health = 15000,
                                  MonsterData = 26137,
                                  Scale = 117,
                                  HeadMesh = 40209,
                                  X = shape.SpawnX + 6f,
                                  Y = shape.SpawnY,
                                  Z = shape.SpawnZ + 6f,
                                  Hx = 0f,
                                  Hy = 0f,
                                  Hz = 0f,
                                  Hw = 1f,
                                  Textures = new[]
                                             {
                                                 new[] { 0, 9418 }, new[] { 1, 8729 }, new[] { 2, 15807 },
                                                 new[] { 3, 9419 }, new[] { 4, 9421 }
                                             },
                                  Meshes = new[]
                                           {
                                               new[] { 0, 20055, 0, 2 }, new[] { 0, 40209, 0, 4 },
                                               new[] { 1, 7826, 0, 2 }
                                           }
                              };
                Character host;
                if (SpawnOne(
                    playfield,
                    playfieldIdentity,
                    activateNpc,
                    hostDef,
                    objective,
                    null,
                    out host))
                {
                    spawned++;
                    findItemHost = host;
                    spawnedObjective = true;
                }
            }

            if (findItemHost != null)
            {
                MissionInstanceMobCombat.RegisterFindItemHost(findItemHost.Identity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "MissionInstanceSpawn FindItem host id=" + findItemHost.Identity
                    + " name=" + findItemHost.Name);
            }

            // RepairMachine: ensure Broken Machine exists even when the shape capture lacked one.
            if (objective == MissionRollType.RepairMachine && !spawnedObjective)
            {
                var machine = new MissionNpc
                              {
                                  Name = "Broken Machine",
                                  Role = MissionNpcRole.BrokenMachine,
                                  Level = 1,
                                  Health = 999999,
                                  MonsterData = 26092,
                                  Scale = 150,
                                  HeadMesh = 0,
                                  X = shape.SpawnX + 8f,
                                  Y = shape.SpawnY,
                                  Z = shape.SpawnZ + 8f,
                                  Hx = 0f,
                                  Hy = 0f,
                                  Hz = 0f,
                                  Hw = 1f,
                                  Textures = null,
                                  Meshes = null
                              };
                Character machineMob;
                if (SpawnOne(
                    playfield,
                    playfieldIdentity,
                    activateNpc,
                    machine,
                    objective,
                    null,
                    out machineMob))
                {
                    spawned++;
                    spawnedObjective = true;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "MissionInstanceSpawn pf=" + playfieldIdentity.Instance
                + " shape=" + shape.CapturedPlayfieldId
                + " objective=" + MissionTypeCatalog.TypeName(objective)
                + " spawned=" + spawned
                + " hasObjective=" + spawnedObjective);
        }

        private static bool IsPlayerPetCapture(MissionNpc def)
        {
            if (def.MonsterData == CarloPetMonsterData || def.MonsterData == CeoGuardianPetMonsterData)
            {
                return true;
            }

            if (string.Equals(def.Name, "Carlo Pinnetti", StringComparison.OrdinalIgnoreCase)
                || string.Equals(def.Name, "CEO Guardian", StringComparison.OrdinalIgnoreCase)
                || string.Equals(def.Name, "Corporate Guardian", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Highest-level real trash NPC becomes the KillPerson target (pets excluded).
        /// </summary>
        private static MissionNpc PickKillTargetFromTrash(MissionShape shape)
        {
            MissionNpc best = null;
            for (int i = 0; i < shape.Npcs.Length; i++)
            {
                MissionNpc def = shape.Npcs[i];
                if (def == null || IsPlayerPetCapture(def) || def.Role != MissionNpcRole.Trash)
                {
                    continue;
                }

                if (best == null
                    || def.Level > best.Level
                    || (def.Level == best.Level && def.Health > best.Health))
                {
                    best = def;
                }
            }

            return best;
        }

        private static MissionRollType ResolveObjectiveType(int playfieldInstance)
        {
            MissionRollType fallback = MissionRollType.KillPerson;
            try
            {
                MissionRollType stamped;
                if (MissionInstanceService.TryGetStampedObjective(playfieldInstance, out stamped))
                {
                    return stamped;
                }
            }
            catch
            {
            }

            return fallback;
        }

        private static bool ShouldSpawnNpc(
            MissionNpc def,
            MissionRollType objective,
            MissionNpc killTarget,
            ref bool spawnedObjective)
        {
            switch (def.Role)
            {
                case MissionNpcRole.Trash:
                    if (objective == MissionRollType.KillPerson
                        && killTarget != null
                        && ReferenceEquals(def, killTarget)
                        && !spawnedObjective)
                    {
                        spawnedObjective = true;
                    }

                    return true;

                case MissionNpcRole.KillGuard:
                case MissionNpcRole.KillBoss:
                    // Catalog entries for these roles were Crat pets in the capture — never spawn.
                    return false;

                case MissionNpcRole.FindTarget:
                    if (objective == MissionRollType.FindPerson && !spawnedObjective)
                    {
                        spawnedObjective = true;
                        return true;
                    }

                    // FindItem: named person still appears; they hold the objective item on corpse.
                    if (objective == MissionRollType.FindItem && !spawnedObjective)
                    {
                        spawnedObjective = true;
                        return true;
                    }

                    return false;

                case MissionNpcRole.BrokenMachine:
                    if (objective == MissionRollType.RepairMachine && !spawnedObjective)
                    {
                        spawnedObjective = true;
                        return true;
                    }

                    return false;

                default:
                    return true;
            }
        }

        private static bool SpawnOne(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            MissionNpc def,
            MissionRollType objective,
            MissionNpc killTarget,
            out Character mob)
        {
            mob = null;
            var npcController = new NPCController();
            mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                TemplateHash,
                playfieldIdentity,
                new Coordinate { x = def.X, y = def.Y, z = def.Z },
                new Quaternion(def.Hx, def.Hy, def.Hz, def.Hw),
                npcController,
                def.Level);

            if (mob == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "MissionInstanceSpawn FAILED template=" + TemplateHash + " npc=" + def.Name);
                return false;
            }

            mob.Name = def.Name;
            mob.Playfield = playfield;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, (uint)def.MonsterData);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, (uint)def.Level);
            // Hostile side (monster) — not BART neutral 0.
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, 0);
            // Capture SCFUs used vf=31 for mission NPCs (not pet-only).
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, 31);
            if (def.Scale > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterscale, (uint)def.Scale);
            }

            if (def.HeadMesh > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.headmesh, (uint)def.HeadMesh);
            }

            ApplyAppearance(mob, def);
            mob.Coordinates(new Coordinate { x = def.X, y = def.Y, z = def.Z });
            mob.DoNotDoTimers = false;

            bool hostile = def.Role != MissionNpcRole.BrokenMachine
                           && def.Role != MissionNpcRole.FindTarget;
            // FindTarget for FindPerson stays non-aggro; FindItem host is hostile trash-like if Trash role.
            if (objective == MissionRollType.FindItem && def.Role == MissionNpcRole.FindTarget)
            {
                hostile = false;
            }

            if (hostile)
            {
                MissionInstanceMobCombat.TryPrepareCombat(mob, npcController, def.Level);
                MissionInstanceMobCombat.RegisterAggressive(mob.Identity);
            }
            else
            {
                npcController.AiProfile = NpcAiProfile.Passive;
            }

            activateNpc(mob);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);

            bool isKillObjective = objective == MissionRollType.KillPerson
                                   && killTarget != null
                                   && ReferenceEquals(def, killTarget);
            bool isFindPersonObjective = def.Role == MissionNpcRole.FindTarget
                                         && objective == MissionRollType.FindPerson;
            if (isKillObjective || isFindPersonObjective)
            {
                MissionTargetTracker.Register(mob.Identity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "MissionInstanceSpawn objective-target role=" + def.Role + " id=" + mob.Identity
                    + " name=" + def.Name);
            }

            if (def.Role == MissionNpcRole.BrokenMachine)
            {
                MissionMachineTracker.Register(mob.Identity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "MissionInstanceSpawn Broken Machine registered id=" + mob.Identity);
            }

            return true;
        }

        private static void ApplyAppearance(Character mob, MissionNpc def)
        {
            if (def.Textures != null && def.Textures.Length > 0)
            {
                mob.Textures.Clear();
                foreach (int[] t in def.Textures)
                {
                    if (t != null && t.Length >= 2 && t[1] > 0)
                    {
                        mob.Textures.Add(new AOTextures(t[0], t[1]));
                    }
                }
            }

            if (def.Meshes != null && def.Meshes.Length > 0)
            {
                mob.MeshLayer.Clear();
                mob.SocialMeshLayer.Clear();
                foreach (int[] m in def.Meshes)
                {
                    if (m != null && m.Length >= 4 && m[1] > 0)
                    {
                        mob.MeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                        mob.SocialMeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                    }
                }
            }
        }
    }
}
