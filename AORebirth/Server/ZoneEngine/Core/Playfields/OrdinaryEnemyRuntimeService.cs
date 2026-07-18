namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Textures;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    internal sealed class OrdinaryEnemyRuntimeService
    {
        private readonly OrdinaryEnemyCatalog catalog;

        private readonly NpcPatrolReplayCoordinator patrolReplay;

        private readonly PlayfieldDynelRegistry dynelRegistry;

        private readonly Action<ICharacter> activateNpc;

        private readonly Random spawnRandom;

        private readonly Func<int, int> levelSelector;

        private readonly Dictionary<int, OrdinaryEnemyLevelSelectionState> levelSelectionBySource =
            new Dictionary<int, OrdinaryEnemyLevelSelectionState>();

        private readonly Dictionary<int, OrdinaryEnemyRuntimeDefinition> activeByRuntimeIdentity =
            new Dictionary<int, OrdinaryEnemyRuntimeDefinition>();

        private readonly Dictionary<int, int> activeRuntimeIdentityBySource =
            new Dictionary<int, int>();

        internal OrdinaryEnemyRuntimeService(
            OrdinaryEnemyCatalog catalog,
            NpcPatrolReplayCoordinator patrolReplay,
            PlayfieldDynelRegistry dynelRegistry,
            Action<ICharacter> activateNpc,
            Func<int, int> levelSelector = null)
        {
            this.catalog = catalog;
            this.patrolReplay = patrolReplay;
            this.dynelRegistry = dynelRegistry;
            this.activateNpc = activateNpc;
            if (levelSelector == null)
            {
                this.spawnRandom = new Random();
                this.levelSelector = this.spawnRandom.Next;
            }
            else
            {
                this.levelSelector = levelSelector;
            }
        }

        internal bool SpawnFromPopulation(
            Playfield playfield,
            Identity playfieldIdentity,
            OrdinaryEnemySpawnDefinition spawn,
            int generation,
            out Identity runtimeIdentity,
            out OrdinaryEnemySpawnGeneration selectedGeneration)
        {
            runtimeIdentity = Identity.None;
            selectedGeneration = null;
            if (spawn == null)
            {
                return false;
            }

            if (this.activeRuntimeIdentityBySource.ContainsKey(spawn.SourceIdentity))
            {
                return false;
            }

            OrdinaryEnemyProfile profile;
            if (!this.catalog.TryGetProfile(spawn.ProfileKey, out profile))
            {
                SubwayVisibilityDiagnosticSelection.RecordPopulationFailure(
                    spawn.SourceIdentity,
                    "profile lookup failed");
                return false;
            }
            bool spawned;
            OrdinaryEnemySpawnGeneration spawnGeneration;
            try
            {
                OrdinaryEnemyLevelSelectionState selectionState;
                if (!this.levelSelectionBySource.TryGetValue(spawn.SourceIdentity, out selectionState))
                {
                    selectionState = new OrdinaryEnemyLevelSelectionState();
                    this.levelSelectionBySource.Add(spawn.SourceIdentity, selectionState);
                }

                spawnGeneration = selectionState.ResolveForGeneration(
                    spawn.LevelDefinition,
                    generation,
                    this.levelSelector);
                spawned = this.Spawn(
                    playfield,
                    playfieldIdentity,
                    spawn,
                    profile,
                    spawnGeneration,
                    out runtimeIdentity);
            }
            catch (Exception exception)
            {
                SubwayVisibilityDiagnosticSelection.RecordPopulationFailure(
                    spawn.SourceIdentity,
                    "materialization exception: " + exception.GetType().Name);
                throw;
            }

            if (spawned)
            {
                selectedGeneration = spawnGeneration;
            }
            else
            {
                SubwayVisibilityDiagnosticSelection.RecordPopulationFailure(
                    spawn.SourceIdentity,
                    "runtime materialization returned false");
            }

            return spawned;
        }

        internal void ClearRuntimeState(int playfieldInstance)
        {
            foreach (int runtimeIdentity in this.activeByRuntimeIdentity.Keys.ToArray())
            {
                OrdinaryEnemyRuntimeRegistry.Remove(runtimeIdentity);
                SubwayVisibilityDiagnosticSelection.RemoveRuntimeIdentity(runtimeIdentity);
                CapturedEnemyCombatRuntimeRegistry.Remove(runtimeIdentity);
            }

            this.activeByRuntimeIdentity.Clear();
            this.activeRuntimeIdentityBySource.Clear();
            this.levelSelectionBySource.Clear();
            OrdinaryEnemyRuntimeRegistry.RemoveForPlayfield(playfieldInstance);
        }

        internal bool ReleasePopulationRuntime(
            ICharacter target,
            out OrdinaryEnemyRuntimeDefinition definition)
        {
            definition = null;
            if (target == null || !this.activeByRuntimeIdentity.TryGetValue(target.Identity.Instance, out definition)) return false;

            this.activeByRuntimeIdentity.Remove(target.Identity.Instance);
            this.activeRuntimeIdentityBySource.Remove(definition.Spawn.SourceIdentity);
            return true;
        }

        internal ICharacter FindAutomaticAggroTarget(ICharacter npc)
        {
            OrdinaryEnemyRuntimeDefinition definition;
            if (npc == null
                || !OrdinaryEnemyRuntimeRegistry.TryGet(npc.Identity.Instance, out definition)
                || definition.Profile.Aggression.Mode != OrdinaryEnemyAggressionMode.Auto
                || !definition.Profile.Aggression.AutomaticAggroRadius.HasValue)
            {
                return null;
            }

            return this.dynelRegistry
                .FindCharactersInRange(
                    npc,
                    (float)definition.Profile.Aggression.AutomaticAggroRadius.Value)
                .Where(
                    candidate => candidate != null
                                 && candidate.Identity != npc.Identity
                                 && candidate.Controller is PlayerController
                                 && candidate.Stats[StatIds.health].Value > 0)
                .OrderBy(candidate => candidate.Coordinates().coordinate.Distance2D(npc.Coordinates().coordinate))
                .ThenBy(candidate => candidate.Identity.Instance)
                .FirstOrDefault();
        }

        internal void TryReturnToSpawn(ICharacter npc)
        {
            OrdinaryEnemyRuntimeDefinition definition;
            NPCController controller = npc == null ? null : npc.Controller as NPCController;
            if (controller == null
                || npc.FightingTarget.Instance != 0
                || controller.IsFollowing()
                || !OrdinaryEnemyRuntimeRegistry.TryGet(npc.Identity.Instance, out definition)
                || !definition.Profile.Aggression.ReturnToSpawn
                || definition.Spawn.MovementMode != OrdinaryEnemyMovementMode.Static)
            {
                return;
            }

            var home = new AORebirth.Core.Vector.Vector3(
                definition.Spawn.X,
                definition.Spawn.Y,
                definition.Spawn.Z);
            if (npc.Coordinates().coordinate.Distance2D(home) <= 0.5)
            {
                return;
            }

            controller.MoveTo(
                new SmokeLounge.AOtomation.Messaging.GameData.Vector3
                {
                    X = definition.Spawn.X,
                    Y = definition.Spawn.Y,
                    Z = definition.Spawn.Z
                });
        }

        private bool Spawn(
            Playfield playfield,
            Identity playfieldIdentity,
            OrdinaryEnemySpawnDefinition spawn,
            OrdinaryEnemyProfile profile,
            OrdinaryEnemySpawnGeneration spawnGeneration,
            out Identity runtimeIdentity)
        {
            runtimeIdentity = Identity.None;
            var controller = new NPCController();
            OrdinaryEnemySpawnVariant variant = spawnGeneration.SelectedVariant;
            Character character = this.ConstructCharacter(
                playfield,
                playfieldIdentity,
                spawn,
                variant,
                profile,
                controller);
            if (character == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Ordinary enemy spawn construction failed profile=" + profile.ProfileKey);
                return false;
            }

            ApplyStats(character, variant, profile);
            ApplyAppearance(character, profile);
            this.ApplyMovement(character, controller, spawn);

            string combatFailure;
            bool combatReady = CapturedEnemyCombatRuntime.Prepare(
                character,
                controller,
                profile.Combat.Contract,
                out combatFailure);
            if (!combatReady)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Ordinary enemy combat contract incomplete sourceIdentity=SimpleChar:{0:X8} profile={1} reason={2}",
                        spawn.SourceIdentity,
                        profile.ProfileKey,
                        combatFailure));
            }

            character.DoNotDoTimers = false;
            var runtimeDefinition = new OrdinaryEnemyRuntimeDefinition(
                spawn,
                profile,
                spawnGeneration);
            OrdinaryEnemyRuntimeRegistry.Register(character.Identity.Instance, runtimeDefinition);
            this.activateNpc(character);
            this.activeByRuntimeIdentity[character.Identity.Instance] = runtimeDefinition;
            this.activeRuntimeIdentityBySource[spawn.SourceIdentity] = character.Identity.Instance;
            SubwayVisibilityDiagnosticSelection.RegisterRuntimeIdentity(
                character.Identity.Instance,
                spawn.SourceIdentity);
            playfield.AnnounceSpawnedCharacterVisibility(character, Identity.None);
            runtimeIdentity = character.Identity;

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Ordinary enemy spawned sourceIdentity=SimpleChar:{0:X8} serverIdentity={1} profile={2} name={3} monsterData={4} level={5} position=({6},{7},{8}) combatModel={9} combatReady={10}",
                    spawn.SourceIdentity,
                    character.Identity,
                    profile.ProfileKey,
                    profile.DisplayName,
                    profile.MonsterData,
                    variant.Level,
                    spawn.X,
                    spawn.Y,
                    spawn.Z,
                    profile.Combat.Contract.AttackModel,
                    combatReady));
            return true;
        }

        private Character ConstructCharacter(
            Playfield playfield,
            Identity playfieldIdentity,
            OrdinaryEnemySpawnDefinition spawn,
            OrdinaryEnemySpawnVariant variant,
            OrdinaryEnemyProfile profile,
            NPCController controller)
        {
            Character character;
            if (profile.ConstructionMode == OrdinaryEnemyConstructionMode.TemplateBacked)
            {
                character = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                    profile.TemplateHash,
                    playfieldIdentity,
                    new Coordinate { x = spawn.X, y = spawn.Y, z = spawn.Z },
                    new AORebirth.Core.Vector.Quaternion(0, 0, 0, 1),
                    controller,
                    variant.Level);
            }
            else
            {
                int instance = Pool.Instance.GetFreeInstance<Character>(1000000, IdentityType.CanbeAffected);
                var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
                character = new Character(playfieldIdentity, identity, controller);
                character.Read();
                controller.Character = character;
            }

            if (character == null)
            {
                return null;
            }

            character.Playfield = playfield;
            character.Name = profile.DisplayName;
            character.FirstName = string.Empty;
            character.LastName = string.Empty;
            character.Coordinates(new Coordinate { x = spawn.X, y = spawn.Y, z = spawn.Z });
            character.RawHeading =
                new AORebirth.Core.Vector.Quaternion(
                    spawn.HeadingX,
                    spawn.HeadingY,
                    spawn.HeadingZ,
                    spawn.HeadingW);
            return character;
        }

        private void ApplyMovement(
            Character character,
            NPCController controller,
            OrdinaryEnemySpawnDefinition spawn)
        {
            character.Waypoints.Clear();
            foreach (OrdinaryEnemyWaypoint waypoint in spawn.Waypoints)
            {
                character.AddWaypoint(
                    new AORebirth.Core.Vector.Vector3(waypoint.X, waypoint.Y, waypoint.Z),
                    false);
            }

            if (character.Waypoints.Count > 1)
            {
                controller.State = CharacterState.Patrolling;
            }

            if (!spawn.UseCapturedPatrolReplay)
            {
                return;
            }

            this.patrolReplay.AssignCapturedSubwayReplay(
                spawn.SourceIdentity,
                segments =>
                {
                    if (segments == null || segments.Length == 0)
                    {
                        return;
                    }

                    var start = spawn.UseSpawnAsPatrolStart
                        ? new AORebirth.Core.Vector.Vector3(spawn.X, spawn.Y, spawn.Z)
                        : new AORebirth.Core.Vector.Vector3(
                            segments[0].StartX,
                            segments[0].StartY,
                            segments[0].StartZ);
                    var end = new AORebirth.Core.Vector.Vector3(
                        segments[0].EndX,
                        segments[0].EndY,
                        segments[0].EndZ);
                    character.Coordinates(start);
                    character.Waypoints.Clear();
                    character.AddWaypoint(start, false);
                    character.AddWaypoint(end, false);
                    controller.SetCapturedPatrolReplaySegments(
                        segments,
                        false,
                        true,
                        spawn.UseSpawnAsPatrolStart);
                    controller.State = CharacterState.Patrolling;
                });
        }

        private static void ApplyStats(
            Character character,
            OrdinaryEnemySpawnVariant variant,
            OrdinaryEnemyProfile profile)
        {
            OrdinaryEnemyAppearanceProfile appearance = profile.Appearance;
            SetMobStat(character, StatIds.side, appearance.Side, profile.ConstructionMode);
            SetMobStat(character, StatIds.fatness, appearance.Fatness, profile.ConstructionMode);
            SetMobStat(character, StatIds.breed, appearance.Breed, profile.ConstructionMode);
            SetMobStat(character, StatIds.sex, appearance.Sex, profile.ConstructionMode);
            SetMobStat(character, StatIds.race, appearance.Race, profile.ConstructionMode);
            SetMobStat(character, StatIds.flags, appearance.CharacterFlags, profile.ConstructionMode);
            SetMobStat(character, StatIds.accountflags, appearance.AccountFlags, profile.ConstructionMode);
            SetMobStat(character, StatIds.expansion, appearance.Expansions, profile.ConstructionMode);
            SetMobStat(character, StatIds.npcfamily, appearance.NpcFamily, profile.ConstructionMode);
            SetMobStat(character, StatIds.losheight, appearance.NpcLosHeight, profile.ConstructionMode);
            SetMobStat(character, StatIds.monsterdata, profile.MonsterData, profile.ConstructionMode);
            SetMobStat(character, StatIds.monsterscale, variant.MonsterScale, profile.ConstructionMode);
            SetMobStat(character, StatIds.visualflags, appearance.VisualFlags, profile.ConstructionMode);
            SetMobStat(character, StatIds.currentmovementmode, (int)MoveModes.Run, profile.ConstructionMode);
            SetMobStat(character, StatIds.prevmovementmode, (int)MoveModes.Run, profile.ConstructionMode);
            SetMobStat(character, StatIds.runspeed, variant.RunSpeed, profile.ConstructionMode);
            SetMobStat(character, StatIds.profession, 1, profile.ConstructionMode);
            SetMobStat(character, StatIds.titlelevel, 1, profile.ConstructionMode);
            SetMobStat(character, StatIds.level, variant.Level, profile.ConstructionMode);
            SetMobStat(character, StatIds.life, variant.Health, profile.ConstructionMode);
            SetMobStat(
                character,
                StatIds.health,
                Math.Max(0, variant.Health - variant.HealthDamage),
                profile.ConstructionMode);
            if (profile.ConstructionMode == OrdinaryEnemyConstructionMode.CapturedDirect)
            {
                SetMobStat(character, StatIds.headmesh, appearance.HeadMesh, profile.ConstructionMode);
            }
        }

        private static void ApplyAppearance(Character character, OrdinaryEnemyProfile profile)
        {
            OrdinaryEnemyAppearanceProfile appearance = profile.Appearance;
            if (profile.ConstructionMode == OrdinaryEnemyConstructionMode.TemplateBacked)
            {
                if (appearance.HeadMesh > 0)
                {
                    SetHeadMesh(character, appearance.HeadMesh);
                }
                else if (appearance.ClearTemplateHeadWhenZero)
                {
                    character.MeshLayer.RemoveMesh(0, 0, 0, 4);
                    character.SocialMeshLayer.RemoveMesh(0, 0, 0, 4);
                }
            }

            if (appearance.ReplaceTextures)
            {
                character.Textures.Clear();
            }

            foreach (OrdinaryEnemyTextureProfile texture in appearance.Textures)
            {
                character.Textures.Add(new AOTextures(texture.Place, texture.Id));
            }

            foreach (OrdinaryEnemyMeshProfile mesh in appearance.Meshes)
            {
                character.MeshLayer.AddMesh(mesh.Position, (int)mesh.Id, mesh.OverrideTextureId, mesh.Layer);
                character.SocialMeshLayer.AddMesh(
                    mesh.Position,
                    (int)mesh.Id,
                    mesh.OverrideTextureId,
                    mesh.Layer);
            }
        }

        private static void SetHeadMesh(Character character, int headMesh)
        {
            int existingHeadMesh = character.Stats[StatIds.headmesh].Value;
            if (existingHeadMesh != 0 && existingHeadMesh != headMesh)
            {
                character.MeshLayer.RemoveMesh(0, existingHeadMesh, 0, 4);
                character.SocialMeshLayer.RemoveMesh(0, existingHeadMesh, 0, 4);
            }

            character.Stats[StatIds.headmesh].Value = headMesh;
            character.Stats[StatIds.headmesh].BaseValue = (uint)headMesh;
            character.MeshLayer.AddMesh(0, headMesh, 0, 4);
            character.SocialMeshLayer.AddMesh(0, headMesh, 0, 4);
        }

        private static void SetMobStat(
            ICharacter character,
            StatIds stat,
            int value,
            OrdinaryEnemyConstructionMode constructionMode)
        {
            if (constructionMode == OrdinaryEnemyConstructionMode.TemplateBacked)
            {
                character.Stats[stat].Value = value;
                character.Stats[stat].BaseValue = (uint)value;
                return;
            }

            character.Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
        }

    }

    internal sealed class OrdinaryEnemyRuntimeDefinition
    {
        internal OrdinaryEnemyRuntimeDefinition(
            OrdinaryEnemySpawnDefinition spawn,
            OrdinaryEnemyProfile profile,
            OrdinaryEnemySpawnGeneration spawnGeneration)
        {
            this.Spawn = spawn;
            this.Profile = profile;
            this.SpawnGeneration = spawnGeneration;
        }

        internal OrdinaryEnemySpawnDefinition Spawn { get; private set; }
        internal OrdinaryEnemyProfile Profile { get; private set; }
        internal OrdinaryEnemySpawnGeneration SpawnGeneration { get; private set; }
    }

    internal static class OrdinaryEnemyRuntimeRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, OrdinaryEnemyRuntimeDefinition> Definitions =
            new Dictionary<int, OrdinaryEnemyRuntimeDefinition>();

        internal static void Register(int serverInstance, OrdinaryEnemyRuntimeDefinition definition)
        {
            lock (Sync)
            {
                Definitions[serverInstance] = definition;
            }
        }

        internal static bool TryGet(int serverInstance, out OrdinaryEnemyRuntimeDefinition definition)
        {
            lock (Sync)
            {
                return Definitions.TryGetValue(serverInstance, out definition);
            }
        }

        internal static void Remove(int serverInstance)
        {
            lock (Sync)
            {
                Definitions.Remove(serverInstance);
            }
        }

        internal static void RemoveForPlayfield(int playfieldInstance)
        {
            lock (Sync)
            {
                foreach (int serverInstance in Definitions
                    .Where(value => value.Value.Spawn.PlayfieldInstance == playfieldInstance)
                    .Select(value => value.Key)
                    .ToArray())
                {
                    Definitions.Remove(serverInstance);
                }
            }
        }
    }
}
