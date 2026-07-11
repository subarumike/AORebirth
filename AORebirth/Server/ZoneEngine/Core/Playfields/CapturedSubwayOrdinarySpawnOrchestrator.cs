namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Textures;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Packets;

    internal sealed class CapturedSubwayOrdinarySpawnOrchestrator
    {
        private readonly CapturedSubwayOrdinaryContentProvider content;
        private readonly Action<ICharacter> activateNpc;

        internal CapturedSubwayOrdinarySpawnOrchestrator(
            CapturedSubwayOrdinaryContentProvider content,
            Action<ICharacter> activateNpc)
        {
            this.content = content;
            this.activateNpc = activateNpc;
        }

        internal void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity)
        {
            if (playfieldIdentity.Instance != CapturedSubwayOrdinaryContentProvider.SubwayPlayfieldInstance)
            {
                return;
            }

            foreach (CapturedSubwayOrdinarySpawnDefinition spawn in this.content.GetSpawns())
            {
                CapturedSubwayOrdinaryArchetypeDefinition archetype;
                if (!this.content.TryGetArchetype(spawn.ArchetypeKey, out archetype))
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "Captured Subway ordinary spawn has no archetype key=" + spawn.ArchetypeKey);
                    continue;
                }

                this.Spawn(playfield, playfieldIdentity, spawn, archetype);
            }
        }

        private void Spawn(
            Playfield playfield,
            Identity playfieldIdentity,
            CapturedSubwayOrdinarySpawnDefinition spawn,
            CapturedSubwayOrdinaryArchetypeDefinition archetype)
        {
            int instance = Pool.Instance.GetFreeInstance<Character>(1000000, IdentityType.CanbeAffected);
            var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
            var controller = new NPCController();
            var character = new Character(playfieldIdentity, identity, controller);
            character.Read();
            character.Playfield = playfield;
            character.Name = archetype.Name;
            character.FirstName = string.Empty;
            character.LastName = string.Empty;
            character.Coordinates(new Coordinate { x = spawn.X, y = spawn.Y, z = spawn.Z });
            character.RawHeading =
                new AORebirth.Core.Vector.Quaternion(
                    spawn.HeadingX,
                    spawn.HeadingY,
                    spawn.HeadingZ,
                    spawn.HeadingW);
            controller.Character = character;

            ApplyCapturedStats(character, spawn, archetype);
            ApplyCapturedAppearance(character, archetype);
            ApplyCapturedPath(character, controller, spawn);
            character.DoNotDoTimers = false;

            CapturedSubwayOrdinaryRuntimeRegistry.Register(character.Identity.Instance, spawn, archetype);
            var fullUpdate = SimpleCharFullUpdate.ConstructMessage(character);
            this.activateNpc(character);
            playfield.Announce(fullUpdate);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Captured Subway ordinary spawn sourceIdentity=SimpleChar:{0:X8} serverIdentity={1} name={2} monsterData={3} level={4} position=({5},{6},{7}) evidence={8}",
                    spawn.SourceInstance,
                    character.Identity,
                    archetype.Name,
                    archetype.MonsterData,
                    spawn.Level,
                    spawn.X,
                    spawn.Y,
                    spawn.Z,
                    spawn.EvidenceCapture));
        }

        private static void ApplyCapturedStats(
            Character character,
            CapturedSubwayOrdinarySpawnDefinition spawn,
            CapturedSubwayOrdinaryArchetypeDefinition archetype)
        {
            uint appearance = archetype.AppearanceValue;
            SetMobStat(character, StatIds.side, (int)(appearance & 7));
            SetMobStat(character, StatIds.fatness, (int)((appearance & 31) >> 3));
            SetMobStat(character, StatIds.breed, (int)((appearance & 255) >> 5));
            SetMobStat(character, StatIds.sex, (int)((appearance & 1023) >> 8));
            SetMobStat(character, StatIds.race, (int)(appearance >> 10));
            SetMobStat(character, StatIds.flags, archetype.CharacterFlags);
            SetMobStat(character, StatIds.accountflags, archetype.AccountFlags);
            SetMobStat(character, StatIds.expansion, archetype.Expansions);
            SetMobStat(character, StatIds.npcfamily, archetype.NpcFamily);
            SetMobStat(character, StatIds.losheight, archetype.NpcLosHeight);
            SetMobStat(character, StatIds.monsterdata, archetype.MonsterData);
            SetMobStat(character, StatIds.monsterscale, spawn.MonsterScale);
            SetMobStat(character, StatIds.visualflags, archetype.VisualFlags);
            SetMobStat(character, StatIds.currentmovementmode, (int)MoveModes.Run);
            SetMobStat(character, StatIds.prevmovementmode, (int)MoveModes.Run);
            SetMobStat(character, StatIds.runspeed, spawn.RunSpeed);
            SetMobStat(character, StatIds.level, spawn.Level);
            SetMobStat(character, StatIds.life, spawn.Health);
            SetMobStat(character, StatIds.health, Math.Max(0, spawn.Health - spawn.HealthDamage));
            SetMobStat(character, StatIds.headmesh, archetype.HeadMesh);

            if (archetype.Combat.Observed)
            {
                SetMobStat(character, StatIds.mindamage, archetype.Combat.MinDamage);
                SetMobStat(character, StatIds.maxdamage, archetype.Combat.MaxDamage);
            }
        }

        private static void ApplyCapturedAppearance(
            Character character,
            CapturedSubwayOrdinaryArchetypeDefinition archetype)
        {
            character.Textures.Clear();
            foreach (CapturedSubwayTextureDefinition texture in archetype.Textures)
            {
                character.Textures.Add(new AOTextures(texture.Place, texture.Id));
            }

            foreach (CapturedSubwayMeshDefinition mesh in archetype.Meshes)
            {
                character.MeshLayer.AddMesh(mesh.Position, (int)mesh.Id, mesh.OverrideTextureId, mesh.Layer);
                character.SocialMeshLayer.AddMesh(mesh.Position, (int)mesh.Id, mesh.OverrideTextureId, mesh.Layer);
            }
        }

        private static void ApplyCapturedPath(
            Character character,
            NPCController controller,
            CapturedSubwayOrdinarySpawnDefinition spawn)
        {
            character.Waypoints.Clear();
            foreach (CapturedSubwayWaypointDefinition waypoint in spawn.Waypoints)
            {
                character.AddWaypoint(
                    new AORebirth.Core.Vector.Vector3(waypoint.X, waypoint.Y, waypoint.Z),
                    false);
            }

            if (character.Waypoints.Count > 1)
            {
                controller.State = CharacterState.Patrolling;
            }
        }

        private static void SetMobStat(ICharacter character, StatIds stat, int value)
        {
            character.Stats.SetBaseValueWithoutTriggering(
                (int)stat,
                (uint)Math.Max(0, value));
        }
    }

    internal static class CapturedSubwayOrdinaryRuntimeRegistry
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<int, CapturedSubwayOrdinaryRuntimeDefinition> Definitions =
            new Dictionary<int, CapturedSubwayOrdinaryRuntimeDefinition>();

        internal static void Register(
            int serverInstance,
            CapturedSubwayOrdinarySpawnDefinition spawn,
            CapturedSubwayOrdinaryArchetypeDefinition archetype)
        {
            lock (Sync)
            {
                Definitions[serverInstance] =
                    new CapturedSubwayOrdinaryRuntimeDefinition(spawn, archetype);
            }
        }

        internal static bool TryGet(int serverInstance, out CapturedSubwayOrdinaryRuntimeDefinition definition)
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
    }

    internal sealed class CapturedSubwayOrdinaryRuntimeDefinition
    {
        internal CapturedSubwayOrdinaryRuntimeDefinition(
            CapturedSubwayOrdinarySpawnDefinition spawn,
            CapturedSubwayOrdinaryArchetypeDefinition archetype)
        {
            this.Spawn = spawn;
            this.Archetype = archetype;
        }

        internal CapturedSubwayOrdinarySpawnDefinition Spawn { get; private set; }
        internal CapturedSubwayOrdinaryArchetypeDefinition Archetype { get; private set; }
    }
}
