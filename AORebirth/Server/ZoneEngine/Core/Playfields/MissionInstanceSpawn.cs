namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

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
    /// Capture-backed RK mission interior population.
    /// Layout/doors/ACG come from a stamped shape. Trash may swap to another complete catalog
    /// template (name+mesh+MonsterData together) — never mix a Scout name onto a Slydroid body.
    /// Find Person contact uses a fixed humanoid shell + briefing TargetName.
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

            if (MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(
                playfieldIdentity.Instance))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Legacy MissionInstanceSpawn rejected generated mission pf="
                    + playfieldIdentity.Instance);
                return;
            }

            MissionShape shape = MissionInstanceShapeCatalog.PickShape(
                playfieldIdentity.Instance,
                null);
            if (shape == null || shape.Npcs == null || shape.Npcs.Length == 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "MissionInstanceSpawn no shape for pf=" + playfieldIdentity.Instance);
                return;
            }

            // Gold reuses pf 1441800 every enter — clear leftover NPCs from a prior run.
            ClearExistingMissionNpcs(playfield);

            MissionRollType objective = ResolveObjectiveType(playfieldIdentity.Instance);
            int missionQl = ResolveMissionQuality(playfieldIdentity.Instance);
            var levelRng = new Random(
                unchecked(playfieldIdentity.Instance * 397) ^ missionQl ^ Environment.TickCount);

            // Never promote a far catalog trash as the objective — always spawn near entrance.
            MissionNpc killTarget = null;
            string killName = null;
            string findName = null;
            int objectiveSide = (int)Side.Neutral;
            MissionInstanceService.TryGetStampedTargetSide(playfieldIdentity.Instance, out objectiveSide);
            if (objective == MissionRollType.KillPerson)
            {
                if (!MissionInstanceService.TryGetStampedTargetName(playfieldIdentity.Instance, out killName)
                    || string.IsNullOrEmpty(killName))
                {
                    killName = MissionTargetNameCatalog.PickKillName(
                        new Random(unchecked(playfieldIdentity.Instance * 911) ^ Environment.TickCount));
                }

                if (objectiveSide < (int)Side.Clan || objectiveSide > (int)Side.Monster)
                {
                    objectiveSide = (int)Side.Monster;
                }
            }
            else if (objective == MissionRollType.FindPerson)
            {
                if (!MissionInstanceService.TryGetStampedTargetName(playfieldIdentity.Instance, out findName)
                    || string.IsNullOrEmpty(findName))
                {
                    findName = MissionTargetNameCatalog.PickFindName(
                        new Random(unchecked(playfieldIdentity.Instance * 733) ^ Environment.TickCount));
                }

                if (objectiveSide != (int)Side.Omni && objectiveSide != (int)Side.Clan)
                {
                    // Find Person contact matches player side (Omni=blue / Clan=yellow). Default Clan only
                    // when stamp missing — enter path stamps from character side.
                    objectiveSide = (int)Side.Clan;
                }
            }

            MissionNpc[] appearancePool = CollectAppearancePool();
            int spawned = 0;
            int trashCount = 0;
            bool spawnedObjective = false;
            Character findItemCube = null;

            MissionTokenProgressTracker.ClearGreyTrash();

            foreach (MissionNpc slot in shape.Npcs)
            {
                if (slot == null || IsPlayerPetCapture(slot) || IsSpawnPointPlayerCapture(slot, shape))
                {
                    continue;
                }

                // KillPerson: invent objective near entrance — skip catalog FindTarget shells.
                // FindPerson: spawn catalog FindTarget (Levi) renamed to briefing contact name.
                if (slot.Role == MissionNpcRole.FindTarget
                    && objective == MissionRollType.KillPerson)
                {
                    continue;
                }

                if (!ShouldSpawnNpc(slot, objective, killTarget, ref spawnedObjective))
                {
                    continue;
                }

                MissionNpc def = CloneNpc(slot);
                if (objective == MissionRollType.FindPerson
                    && def.Role == MissionNpcRole.FindTarget
                    && !string.IsNullOrEmpty(findName))
                {
                    def.Name = findName;
                }

                // Keep capture meshes/textures. Remix only empty/grey shells.
                if (def.Role == MissionNpcRole.Trash
                    && appearancePool.Length > 0
                    && (def.IsGrey || def.Meshes == null || def.Meshes.Length == 0))
                {
                    ApplyRandomAppearance(def, appearancePool, levelRng);
                }

                Character mob;
                if (SpawnOne(
                    playfield,
                    playfieldIdentity,
                    activateNpc,
                    def,
                    objective,
                    killTarget,
                    missionQl,
                    levelRng,
                    objectiveSide,
                    out mob))
                {
                    spawned++;
                    if (IsTrashForTokenCount(def, objective, killTarget))
                    {
                        trashCount++;
                    }

                    if (objective == MissionRollType.FindPerson
                        && def.Role == MissionNpcRole.FindTarget)
                    {
                        spawnedObjective = true;
                    }
                }
            }

            // KillPerson: gold humanoid look (ACG 20260728-001044 Pedro Peasley) near entrance.
            // Do not remix trash body meshes — that wrong texture also crashes the client on death.
            if (objective == MissionRollType.KillPerson)
            {
                string contactName = string.IsNullOrEmpty(killName)
                                         ? MissionTargetNameCatalog.PickKillName(levelRng)
                                         : killName;
                MissionNpc killDef = BuildGoldKillPerson(contactName, shape, 8f);

                Character killMob;
                if (SpawnOne(
                    playfield,
                    playfieldIdentity,
                    activateNpc,
                    killDef,
                    objective,
                    killDef,
                    missionQl,
                    levelRng,
                    objectiveSide,
                    out killMob))
                {
                    spawned++;
                    trashCount++;
                    spawnedObjective = true;
                    MissionTargetTracker.Register(killMob.Identity);
                }
            }

            // FindPerson: gold contact look = MonsterData 26103 + head 40103 (Malcom 002423).
            // Body is client-side from MonsterData; inventing trash body meshes looked like trash.
            if (objective == MissionRollType.FindPerson && !spawnedObjective)
            {
                string contactName = string.IsNullOrEmpty(findName)
                                         ? MissionTargetNameCatalog.PickFindName(levelRng)
                                         : findName;
                MissionNpc findDef;
                if (!TryCloneCatalogFindTarget(shape, contactName, out findDef))
                {
                    findDef = BuildGoldFindPerson(contactName, shape);
                    if (!TryPlaceAtCatalogFindTarget(findDef, shape))
                    {
                        PlaceDeepInMission(findDef, shape, 55f);
                    }
                }
                else
                {
                    ApplyGoldFindPersonLook(findDef);
                }

                Character findMob;
                if (SpawnOne(
                    playfield,
                    playfieldIdentity,
                    activateNpc,
                    findDef,
                    objective,
                    null,
                    missionQl,
                    levelRng,
                    objectiveSide,
                    out findMob))
                {
                    spawned++;
                    spawnedObjective = true;
                    MissionDiagnostics.Log(
                        "FIND-PERSON-SPAWN charPf={0} name={1} xyz=({2:0.#},{3:0.#},{4:0.#}) md={5} head={6}",
                        playfieldIdentity.Instance,
                        findDef.Name ?? string.Empty,
                        findDef.X,
                        findDef.Y,
                        findDef.Z,
                        findDef.MonsterData,
                        findDef.HeadMesh);
                }
            }

            // FindItem (keep only): Mission Cube SimpleChar. FindItemReturn uses world Terminal
            // only (capture 20260728-095215) — a cube SimpleChar looked like a person on top of
            // the capsule and blocked PickUp / exit door at spawn.
            if (objective == MissionRollType.FindItem && findItemCube == null)
            {
                MissionNpc cubeDef = BuildMissionCube(shape);
                Character cube;
                if (SpawnOne(
                    playfield,
                    playfieldIdentity,
                    activateNpc,
                    cubeDef,
                    objective,
                    null,
                    missionQl,
                    levelRng,
                    0,
                    out cube))
                {
                    spawned++;
                    findItemCube = cube;
                    spawnedObjective = true;
                }
            }

            if (findItemCube != null)
            {
                MissionFindItemService.RegisterCube(findItemCube.Identity);
                MissionInstanceMobCombat.RegisterFindItemHost(findItemCube.Identity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "MissionInstanceSpawn FindItem objective id=" + findItemCube.Identity
                    + " name=" + findItemCube.Name
                    + " type=" + MissionTypeCatalog.TypeName(objective));
            }

            if (objective == MissionRollType.FindItemReturn)
            {
                spawnedObjective = true;
            }

            // RepairMachine Broken Machine is a Container (ChestFullUpdate), sent on zone-in by
            // MissionInstanceDoorReplay — never SpawnMobFromTemplate (that made it look human).
            if (objective == MissionRollType.RepairMachine)
            {
                spawnedObjective = true;
            }

            // Capture loot props remapped onto the active shape spawn (barrels / treasure / skeletons).
            int lootSpawned = SpawnLootProps(
                playfield,
                playfieldIdentity,
                activateNpc,
                missionQl,
                levelRng,
                shape);
            spawned += lootSpawned;

            MissionTokenProgressTracker.Begin(playfieldIdentity.Instance, trashCount);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "MissionInstanceSpawn pf=" + playfieldIdentity.Instance
                + " shape=" + shape.CapturedPlayfieldId
                + " objective=" + MissionTypeCatalog.TypeName(objective)
                + " ql=" + missionQl
                + " spawned=" + spawned
                + " trash=" + trashCount
                + " lootProps=" + lootSpawned
                + " hasObjective=" + spawnedObjective);
        }

        private static int ResolveMissionQuality(int playfieldInstance)
        {
            int ql;
            if (MissionInstanceService.TryGetStampedMissionQuality(playfieldInstance, out ql) && ql > 0)
            {
                return ql;
            }

            // Never fall back to capture trash levels (~150) — that made lvl-19 mishs spawn 151 bosses.
            return 1;
        }

        private static int ScaleLevelToMission(int missionQl, Random rng)
        {
            return MissionNpcDifficultyPolicy.ResolveLevel(missionQl, rng);
        }

        /// <summary>
        /// QL165 capture catalog HP must never drive low-QL mish trash.
        /// L7 gold 20260725-002423: L3=70, L4=93, L5=115 (~23–25 HP/level).
        /// </summary>
        private static int HealthForMissionLevel(int level, Random rng)
        {
            return MissionNpcDifficultyPolicy.ResolveHealth(level, rng);
        }

        private static int SpawnLootProps(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            int missionQl,
            Random levelRng,
            MissionShape shape)
        {
            // Live containers are IdentityType.Container (SimpleItemFullUpdate), NOT SimpleChar.
            // Spawning barrels/treasure/Shadow Rifts via SpawnMobFromTemplate made them look like
            // floating people. Disabled until capture-backed Container StaticDynel templates land.
            if (playfield == null || playfieldIdentity.Instance == 0 || activateNpc == null
                || levelRng == null || shape == null || missionQl < 0)
            {
                return 0;
            }

            return 0;
        }

        private static bool IsTrashForTokenCount(
            MissionNpc def,
            MissionRollType objective,
            MissionNpc killTarget)
        {
            if (def == null)
            {
                return false;
            }

            if (def.Role == MissionNpcRole.BrokenMachine
                || def.Role == MissionNpcRole.FindTarget
                || def.Role == MissionNpcRole.LootProp)
            {
                return false;
            }

            // Grey trash (no side textures) does not raise token % — only colored trash counts.
            if (def.Role == MissionNpcRole.Trash)
            {
                return !def.IsGrey;
            }

            return false;
        }

        private static MissionNpc BuildFictionalPerson(
            string name,
            MissionNpcRole role,
            MissionShape shape,
            float offset)
        {
            if (role == MissionNpcRole.FindTarget)
            {
                MissionNpc gold = BuildGoldFindPerson(name, shape);
                gold.X = shape.SpawnX + offset;
                gold.Y = shape.SpawnY;
                gold.Z = shape.SpawnZ + (offset * 0.5f);
                return gold;
            }

            return new MissionNpc
                   {
                       Name = name,
                       Role = role,
                       Level = 150,
                       Health = 20000,
                       MonsterData = 26137,
                       Scale = 100,
                       HeadMesh = 40209,
                       X = shape.SpawnX + offset,
                       Y = shape.SpawnY,
                       Z = shape.SpawnZ + (offset * 0.5f),
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
        }

        /// <summary>
        /// L7 gold Find Person (Malcom Thompon, capture 20260725-002423): MD 26103, head 40103,
        /// head-only mesh — body from MonsterData. Never invent trash body meshes here.
        /// </summary>
        private static MissionNpc BuildGoldFindPerson(string name, MissionShape shape)
        {
            var dest = new MissionNpc
                       {
                           Name = name,
                           Role = MissionNpcRole.FindTarget,
                           Level = 4,
                           Health = 50000,
                           X = shape != null ? shape.SpawnX - 55f : 0f,
                           Y = shape != null ? shape.SpawnY : 5.01f,
                           Z = shape != null ? shape.SpawnZ - 30f : 0f,
                           Hx = 0f,
                           Hy = -0.9164343f,
                           Hz = 0f,
                           Hw = 0.4001852f,
                           IsGrey = false
                       };
            ApplyGoldFindPersonLook(dest);
            return dest;
        }

        private static void ApplyGoldFindPersonLook(MissionNpc dest)
        {
            if (dest == null)
            {
                return;
            }

            dest.Role = MissionNpcRole.FindTarget;
            dest.MonsterData = 26103;
            dest.HeadMesh = 40103;
            if (dest.Scale <= 0)
            {
                dest.Scale = 92;
            }

            dest.Textures = new[]
                            {
                                new[] { 0, 0 }, new[] { 1, 81911 }, new[] { 2, 81913 },
                                new[] { 3, 81908 }, new[] { 4, 81916 }
                            };
            // Head only — body is client MonsterData 26103 (gold Malcom).
            dest.Meshes = new[] { new[] { 0, 40103, 0, 4 } };
            dest.IsGrey = false;
        }

        /// <summary>
        /// Kill Person objective (ACG capture 20260728-001044 Pedro Peasley / Kill icon 11330):
        /// MonsterData 26097, head mesh 40111, empty textures, head-only mesh layer.
        /// Body comes from MonsterData — do not invent trash body meshes or remix appearance pools.
        /// </summary>
        private static MissionNpc BuildGoldKillPerson(string name, MissionShape shape, float offset)
        {
            var dest = new MissionNpc
                       {
                           Name = name,
                           Role = MissionNpcRole.Trash,
                           Level = 42,
                           Health = 1773,
                           X = shape != null ? shape.SpawnX + offset : 0f,
                           Y = shape != null ? shape.SpawnY : 5.01f,
                           Z = shape != null ? shape.SpawnZ + (offset * 0.5f) : 0f,
                           Hx = 0f,
                           Hy = -0.111751281f,
                           Hz = 0f,
                           Hw = 0.9937362f,
                           IsGrey = false
                       };
            ApplyGoldKillPersonLook(dest);
            return dest;
        }

        private static void ApplyGoldKillPersonLook(MissionNpc dest)
        {
            if (dest == null)
            {
                return;
            }

            dest.MonsterData = 26097;
            dest.HeadMesh = 40111;
            dest.Scale = 104;
            dest.Textures = new[]
                            {
                                new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 },
                                new[] { 3, 0 }, new[] { 4, 0 }
                            };
            // Head only — body is client MonsterData 26097 (gold Pedro Peasley).
            dest.Meshes = new[] { new[] { 0, 40111, 0, 4 } };
            dest.IsGrey = false;
        }

        /// <summary>
        /// Gold Find Person contacts use catalog FindTarget MonsterData + head mesh + textures
        /// (Malcom: MD 26103, head 40103). Body is client-side from MonsterData — do not invent trash body meshes.
        /// </summary>
        private static bool TryCloneCatalogFindTarget(MissionShape shape, string displayName, out MissionNpc dest)
        {
            dest = null;
            if (shape == null || shape.Npcs == null)
            {
                return false;
            }

            MissionNpc best = null;
            float bestDistSq = -1f;
            MissionNpc anyFind = null;
            for (int i = 0; i < shape.Npcs.Length; i++)
            {
                MissionNpc npc = shape.Npcs[i];
                if (npc == null || npc.Role != MissionNpcRole.FindTarget)
                {
                    continue;
                }

                if (anyFind == null)
                {
                    anyFind = npc;
                }

                float dx = npc.X - shape.SpawnX;
                float dz = npc.Z - shape.SpawnZ;
                float distSq = (dx * dx) + (dz * dz);
                // Prefer deepest FindTarget; never discard near-spawn contacts (Levi ~87m ok,
                // but older shapes parked contact near entrance).
                if (distSq > bestDistSq)
                {
                    bestDistSq = distSq;
                    best = npc;
                }
            }

            if (best == null)
            {
                best = anyFind;
            }

            if (best == null)
            {
                return false;
            }

            dest = CloneNpc(best);
            dest.Name = displayName;
            dest.Role = MissionNpcRole.FindTarget;
            dest.IsGrey = false;
            return true;
        }

        /// <summary>
        /// Gold Find Person contacts sit at catalog FindTarget XYZ (Jeanne/Lanny) when the shape has one.
        /// </summary>
        private static bool TryPlaceAtCatalogFindTarget(MissionNpc dest, MissionShape shape)
        {
            if (dest == null || shape == null || shape.Npcs == null)
            {
                return false;
            }

            for (int i = 0; i < shape.Npcs.Length; i++)
            {
                MissionNpc npc = shape.Npcs[i];
                if (npc == null || npc.Role != MissionNpcRole.FindTarget)
                {
                    continue;
                }

                float dx = npc.X - shape.SpawnX;
                float dz = npc.Z - shape.SpawnZ;
                if (((dx * dx) + (dz * dz)) < (40f * 40f))
                {
                    // Too close to door — keep searching.
                    continue;
                }

                dest.X = npc.X;
                dest.Y = npc.Y;
                dest.Z = npc.Z;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gold Find Person contacts sit well inside the layout (Jeanne ~80m from spawn), not on the door.
        /// Prefer the farthest catalog trash / FindTarget XYZ; fall back to a deep offset from spawn.
        /// </summary>
        private static void PlaceDeepInMission(MissionNpc dest, MissionShape shape, float minDistance)
        {
            if (dest == null || shape == null)
            {
                return;
            }

            float bestDistSq = -1f;
            float bestX = shape.SpawnX - minDistance;
            float bestY = shape.SpawnY;
            float bestZ = shape.SpawnZ;
            if (shape.Npcs != null)
            {
                for (int i = 0; i < shape.Npcs.Length; i++)
                {
                    MissionNpc npc = shape.Npcs[i];
                    if (npc == null || IsPlayerPetCapture(npc))
                    {
                        continue;
                    }

                    if (npc.Role != MissionNpcRole.Trash && npc.Role != MissionNpcRole.FindTarget)
                    {
                        continue;
                    }

                    float dx = npc.X - shape.SpawnX;
                    float dz = npc.Z - shape.SpawnZ;
                    float distSq = (dx * dx) + (dz * dz);
                    if (distSq > bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestX = npc.X;
                        bestY = npc.Y;
                        bestZ = npc.Z;
                    }
                }
            }

            float minSq = minDistance * minDistance;
            if (bestDistSq < minSq)
            {
                // No far catalog slot — push deeper along -X (common mish corridor from spawn).
                bestX = shape.SpawnX - minDistance;
                bestY = shape.SpawnY;
                bestZ = shape.SpawnZ + (minDistance * 0.25f);
            }

            dest.X = bestX;
            dest.Y = bestY;
            dest.Z = bestZ;
        }

        /// <summary>
        /// Contact/kill appearance: only remix from humanoids that already have body mesh + non-zero textures.
        /// Never picks robot / droid / cyborg catalog shells. Keeps dest.Name.
        /// Falls back to BuildFictionalPerson defaults (already on dest) when the pool is unsafe.
        /// </summary>
        private static void ApplySafeContactAppearance(MissionNpc dest, MissionNpc[] pool, Random rng)
        {
            if (dest == null || pool == null || pool.Length == 0 || rng == null)
            {
                return;
            }

            var safe = new List<MissionNpc>(32);
            for (int i = 0; i < pool.Length; i++)
            {
                MissionNpc src = pool[i];
                if (src == null || src.Meshes == null || src.Meshes.Length < 2
                    || src.Textures == null || src.Textures.Length < 3
                    || IsNonHumanoidCatalogName(src.Name))
                {
                    continue;
                }

                bool hasBody = false;
                bool hasHead = false;
                bool hasTex = false;
                for (int m = 0; m < src.Meshes.Length; m++)
                {
                    int[] mesh = src.Meshes[m];
                    if (mesh == null || mesh.Length < 2 || mesh[1] <= 0)
                    {
                        continue;
                    }

                    int slot = mesh.Length >= 4 ? mesh[3] : 0;
                    if (slot == 2)
                    {
                        hasBody = true;
                    }

                    if (slot == 4)
                    {
                        hasHead = true;
                    }
                }

                for (int t = 0; t < src.Textures.Length; t++)
                {
                    int[] tex = src.Textures[t];
                    if (tex != null && tex.Length >= 2 && tex[0] == 0 && tex[1] > 0)
                    {
                        hasTex = true;
                        break;
                    }
                }

                if (hasBody && hasHead && hasTex)
                {
                    safe.Add(src);
                }
            }

            if (safe.Count == 0)
            {
                return;
            }

            MissionNpc pick = safe[rng.Next(safe.Count)];
            dest.MonsterData = pick.MonsterData > 0 ? pick.MonsterData : dest.MonsterData;
            dest.Scale = pick.Scale > 0 ? pick.Scale : 100;
            if (dest.Scale > 110)
            {
                dest.Scale = 100;
            }

            dest.HeadMesh = pick.HeadMesh;
            dest.Textures = pick.Textures;
            dest.Meshes = pick.Meshes;
            dest.IsGrey = false;
        }

        private static bool IsNonHumanoidCatalogName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return true;
            }

            string n = name;
            return n.IndexOf("droid", StringComparison.OrdinalIgnoreCase) >= 0
                   || n.IndexOf("robot", StringComparison.OrdinalIgnoreCase) >= 0
                   || n.IndexOf("cyborg", StringComparison.OrdinalIgnoreCase) >= 0
                   || n.IndexOf("bileswarm", StringComparison.OrdinalIgnoreCase) >= 0
                   || n.IndexOf("drainer", StringComparison.OrdinalIgnoreCase) >= 0
                   || n.IndexOf("beast", StringComparison.OrdinalIgnoreCase) >= 0
                   || n.IndexOf("creature", StringComparison.OrdinalIgnoreCase) >= 0
                   || n.IndexOf("heckler", StringComparison.OrdinalIgnoreCase) >= 0
                   || n.IndexOf("pit demon", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static MissionNpc CloneNpc(MissionNpc src)
        {
            if (src == null)
            {
                return null;
            }

            return new MissionNpc
                   {
                       Name = src.Name,
                       Role = src.Role,
                       Level = src.Level,
                       Health = src.Health,
                       MonsterData = src.MonsterData,
                       Scale = src.Scale,
                       HeadMesh = src.HeadMesh,
                       X = src.X,
                       Y = src.Y,
                       Z = src.Z,
                       Hx = src.Hx,
                       Hy = src.Hy,
                       Hz = src.Hz,
                       Hw = src.Hw,
                       Textures = src.Textures,
                       Meshes = src.Meshes,
                       IsGrey = src.IsGrey
                   };
        }

        private static MissionNpc[] CollectAppearancePool()
        {
            var list = new List<MissionNpc>(128);
            MissionShape[] shapes = MissionInstanceShapeCatalog.Shapes;
            if (shapes == null)
            {
                return new MissionNpc[0];
            }

            for (int s = 0; s < shapes.Length; s++)
            {
                MissionShape shape = shapes[s];
                if (shape == null || shape.Npcs == null)
                {
                    continue;
                }

                for (int i = 0; i < shape.Npcs.Length; i++)
                {
                    MissionNpc npc = shape.Npcs[i];
                    if (npc == null
                        || IsPlayerPetCapture(npc)
                        || npc.Role == MissionNpcRole.BrokenMachine
                        || npc.Role == MissionNpcRole.LootProp
                        || npc.MonsterData <= 0)
                    {
                        continue;
                    }

                    if (npc.Role == MissionNpcRole.Trash
                        || npc.Role == MissionNpcRole.FindTarget
                        || npc.Role == MissionNpcRole.KillBoss
                        || npc.Role == MissionNpcRole.KillGuard)
                    {
                        // Prefer textured humanoids for remix variety.
                        if (npc.Textures != null && npc.Textures.Length > 0)
                        {
                            list.Add(npc);
                        }
                    }
                }
            }

            return list.ToArray();
        }

        private static void ApplyRandomAppearance(MissionNpc dest, MissionNpc[] pool, Random rng)
        {
            if (dest == null || pool == null || pool.Length == 0 || rng == null)
            {
                return;
            }

            MissionNpc src = pool[rng.Next(pool.Length)];
            if (src == null || string.IsNullOrEmpty(src.Name) || src.MonsterData <= 0)
            {
                return;
            }

            // Keep XYZ / Role from the slot; take the full visual identity from src.
            dest.Name = src.Name;
            dest.MonsterData = src.MonsterData;
            dest.Scale = src.Scale > 0 ? src.Scale : dest.Scale;
            dest.HeadMesh = src.HeadMesh;
            dest.Textures = src.Textures;
            dest.Meshes = src.Meshes;
            dest.IsGrey = src.IsGrey;
        }

        private static MissionNpc BuildMissionCube(MissionShape shape)
        {
            // Compact cube-like host; Use grants the real FindItem template.
            return new MissionNpc
                   {
                       Name = "Mission Cube",
                       Role = MissionNpcRole.FindTarget,
                       Level = 1,
                       Health = 999999,
                       MonsterData = 26092,
                       Scale = 40,
                       HeadMesh = 0,
                       X = shape.SpawnX + 5f,
                       Y = shape.SpawnY,
                       Z = shape.SpawnZ + 5f,
                       Hx = 0f,
                       Hy = 0f,
                       Hz = 0f,
                       Hw = 1f,
                       Textures = null,
                       Meshes = null
                   };
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
        /// L7 gold 1443840 embeds the capturing player (Getkeep) as Trash at the exact spawn XYZ.
        /// Spawning that shell puts a hostile on the entrance door.
        /// </summary>
        private static bool IsSpawnPointPlayerCapture(MissionNpc def, MissionShape shape)
        {
            if (def == null || shape == null || def.Role != MissionNpcRole.Trash)
            {
                return false;
            }

            float dx = def.X - shape.SpawnX;
            float dz = def.Z - shape.SpawnZ;
            return ((dx * dx) + (dz * dz)) < 4.0f;
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
                    return true;

                case MissionNpcRole.KillGuard:
                case MissionNpcRole.KillBoss:
                    // Catalog entries for these roles were Crat pets in the capture — never spawn.
                    return false;

                case MissionNpcRole.FindTarget:
                    // Find Person contact only. FindItemReturn uses world Terminal 100361
                    // (capture 20260728-095215); never spawn catalog humans as the objective.
                    if (objective == MissionRollType.FindPerson && !spawnedObjective)
                    {
                        return true;
                    }

                    return false;

                case MissionNpcRole.BrokenMachine:
                    if (objective == MissionRollType.RepairMachine && !spawnedObjective)
                    {
                        return true;
                    }

                    return false;

                case MissionNpcRole.LootProp:
                    return true;

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
            int missionQl,
            Random levelRng,
            int objectiveSide,
            out Character mob)
        {
            mob = null;
            bool isObjectiveProp = def.Role == MissionNpcRole.BrokenMachine
                                   || def.Role == MissionNpcRole.FindTarget
                                   || def.Role == MissionNpcRole.LootProp;
            // Never fall back to catalog Level (QL165 shells are ~150+).
            int ql = missionQl > 0 ? missionQl : 1;
            int spawnLevel = isObjectiveProp && def.Level <= 1
                                 ? 1
                                 : ScaleLevelToMission(ql, levelRng);
            int spawnHealth;
            if (isObjectiveProp && def.Health >= 999999)
            {
                spawnHealth = def.Health;
            }
            else if (def.Role == MissionNpcRole.FindTarget)
            {
                // Contact: tough enough not to die to splash, not a QL165 sponge.
                spawnHealth = HealthForMissionLevel(spawnLevel, levelRng) * 2;
            }
            else
            {
                spawnHealth = HealthForMissionLevel(spawnLevel, levelRng);
            }

            var npcController = new NPCController();
            mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                TemplateHash,
                playfieldIdentity,
                new Coordinate { x = def.X, y = def.Y, z = def.Z },
                new Quaternion(def.Hx, def.Hy, def.Hz, def.Hw),
                npcController,
                spawnLevel);

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
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)spawnHealth);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)spawnHealth);
            // Prevent ~1s HP snap-back from template healinterval/healdelta while fighting.
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.healinterval, 0u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.healdelta, 0u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, (uint)spawnLevel);
            bool isKillObjective = objective == MissionRollType.KillPerson
                                   && killTarget != null
                                   && ReferenceEquals(def, killTarget);
            bool isFindObjective = objective == MissionRollType.FindPerson
                                   && def.Role == MissionNpcRole.FindTarget;
            int mapSide = ResolveMapSide(def, isKillObjective || isFindObjective, objectiveSide);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)mapSide);
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
            mob.SetFightingTarget(Identity.None);

            bool hostile = def.Role != MissionNpcRole.BrokenMachine
                           && def.Role != MissionNpcRole.FindTarget
                           && def.Role != MissionNpcRole.LootProp;

            if (hostile)
            {
                bool combatReady = MissionInstanceMobCombat.TryPrepareCombat(mob, npcController, spawnLevel);
                if (!combatReady)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "MissionInstanceSpawn combat prepare failed npc=" + def.Name
                        + " id=" + mob.Identity);
                }

                // Combat prep/WIFU must not restore full HP after damage starts.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)spawnHealth);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)spawnHealth);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.healinterval, 0u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.healdelta, 0u);
                playfield.SuspendNpcRegen(mob);

                MissionInstanceMobCombat.RegisterAggressive(mob.Identity);
                npcController.AiProfile = NpcAiProfile.Aggressive;
                MissionDiagnostics.Log(
                    "SPAWN-HOSTILE name={0} id={1} md={2} lvl={3} gun={4} combatReady={5}",
                    def.Name ?? string.Empty,
                    mob.Identity.Instance,
                    def.MonsterData,
                    spawnLevel,
                    MissionInstanceMobCombat.HasGunMesh(mob),
                    combatReady);
            }
            else
            {
                npcController.AiProfile = NpcAiProfile.Passive;
            }

            activateNpc(mob);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);

            if (isKillObjective)
            {
                // Gold Kill Person death (20260728-211947 Zack): Parameter2=503, not trash 501.
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.corpseanimkey, 503u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.dieanim, 503u);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.itemanim, 503u);
                MissionTargetTracker.Register(mob.Identity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "MissionInstanceSpawn KillPerson target id=" + mob.Identity + " name=" + def.Name
                    + " lvl=" + spawnLevel + " md=" + def.MonsterData + " head=" + def.HeadMesh);
            }

            if (def.Role == MissionNpcRole.FindTarget && objective == MissionRollType.FindPerson)
            {
                MissionFindPersonService.Register(mob.Identity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "MissionInstanceSpawn FindPerson tag target id=" + mob.Identity + " name=" + def.Name);
            }

            if (def.Role == MissionNpcRole.BrokenMachine)
            {
                MissionMachineTracker.Register(mob.Identity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "MissionInstanceSpawn Broken Machine registered id=" + mob.Identity);
            }

            if (def.Role == MissionNpcRole.Trash && def.IsGrey)
            {
                MissionTokenProgressTracker.RegisterGreyTrash(mob.Identity);
            }

            return true;
        }

        /// <summary>
        /// Map-dot colors: Monster=red, Clan=yellow, Omni=blue, Neutral=white.
        /// </summary>
        private static int ResolveMapSide(MissionNpc def, bool isObjectivePerson, int objectiveSide)
        {
            if (def == null)
            {
                return (int)Side.Neutral;
            }

            if (isObjectivePerson && objectiveSide >= (int)Side.Neutral && objectiveSide <= (int)Side.Monster)
            {
                return objectiveSide;
            }

            if (def.IsGrey)
            {
                return (int)Side.Neutral;
            }

            string name = def.Name ?? string.Empty;
            if (name.IndexOf("Clan", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return (int)Side.Clan;
            }

            if (name.IndexOf("Omni", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return (int)Side.Omni;
            }

            if (IsMonsterMapName(name))
            {
                return (int)Side.Monster;
            }

            return (int)Side.Neutral;
        }

        private static bool IsMonsterMapName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            // Animals / mystics / borgs / robots — red map dots on live AO.
            string[] markers =
                {
                    "Blubbag", "Leet", "Rollerrat", "Wolf", "Dog", "Snake", "Spider", "Insect",
                    "Beast", "Borg", "Mystic", "Robot", "Drone", "Guard Dog", "Chimera", "Anima",
                    "Monster", "Creature", "Heckler", "Pit Demon", "Sl thrash", "Slither"
                };
            for (int i = 0; i < markers.Length; i++)
            {
                if (name.IndexOf(markers[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ClearExistingMissionNpcs(Playfield playfield)
        {
            if (playfield == null)
            {
                return;
            }

            List<ICharacter> snapshot = new List<ICharacter>();
            foreach (ICharacter character in playfield.EnumerateActiveCharacters())
            {
                if (character != null)
                {
                    snapshot.Add(character);
                }
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                ICharacter existing = snapshot[i];
                if (existing == null || !(existing.Controller is NPCController))
                {
                    continue;
                }

                try
                {
                    // Reused dynel ids keep fight/aggro state across entries.
                    MissionInstanceMobCombat.UnregisterAggressive(existing.Identity);
                    existing.SetFightingTarget(Identity.None);
                    playfield.DespawnNpcImmediately(existing);
                }
                catch
                {
                }
            }
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
