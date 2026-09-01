namespace ZoneEngine.Core.Subway.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Textures;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    #endregion

    internal sealed class WindcallerKarrecNpcRuntimeService
    {
        private readonly List<Character> capturedCharacters = new List<Character>();

        internal void Spawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            Action<Identity> deactivateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || deactivateNpc == null
                || playfieldIdentity.Instance != WindcallerKarrecNpcContent.PlayfieldId
                || WindcallerKarrecNpcRuntimeRegistry.ContainsPlayfield(playfieldIdentity))
            {
                return;
            }

            var pending = new List<Character>();
            try
            {
                foreach (WindcallerKarrecNpcDefinition definition in WindcallerKarrecNpcContent.Definitions)
                {
                    pending.Add(this.CreateCharacter(playfield, playfieldIdentity, definition));
                }

                this.capturedCharacters.AddRange(pending);
                for (int index = 0; index < pending.Count; index++)
                {
                    Character character = pending[index];
                    WindcallerKarrecNpcDefinition definition = WindcallerKarrecNpcContent.Definitions[index];
                    WindcallerKarrecNpcRuntimeRegistry.Register(
                        new WindcallerKarrecNpcRuntimeDefinition(
                            playfieldIdentity,
                            character.Identity,
                            definition));
                    activateNpc(character);
                    playfield.AnnounceSpawnedCharacterVisibility(character, Identity.None);

                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        "Windcaller Karrec quest NPC spawned sourceNpc="
                        + definition.SourceNpcIdentity
                        + " runtimeNpc=" + character.Identity
                        + " name=" + definition.DisplayName
                        + " patrolSegments=" + definition.PatrolSegments.Count
                        + " evidence=" + definition.Evidence);
                }
            }
            catch (Exception exception)
            {
                foreach (Character character in pending)
                {
                    if (!this.capturedCharacters.Contains(character))
                    {
                        Pool.Instance.RemoveObject(character);
                    }
                }

                this.Clear(playfieldIdentity, deactivateNpc);

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Windcaller Karrec quest NPC population failed playfield="
                    + playfieldIdentity
                    + " reason=" + exception.Message);
            }
        }

        internal void Clear(Identity playfieldIdentity, Action<Identity> deactivateNpc)
        {
            WindcallerKarrecNpcRuntimeRegistry.RemoveForPlayfield(playfieldIdentity);
            foreach (Character character in this.capturedCharacters)
            {
                deactivateNpc(character.Identity);
                Pool.Instance.RemoveObject(character);
            }

            this.capturedCharacters.Clear();
        }

        private Character CreateCharacter(
            Playfield playfield,
            Identity playfieldIdentity,
            WindcallerKarrecNpcDefinition definition)
        {
            int instance = Pool.Instance.GetFreeInstance<Character>(1000000, IdentityType.CanbeAffected);
            var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
            var controller = new NPCController { AiProfile = NpcAiProfile.Social };
            var character = new NpcCharacter(playfieldIdentity, identity, controller);
            character.Read();
            controller.Character = character;
            character.Playfield = playfield;
            character.Name = definition.DisplayName;
            character.FirstName = string.Empty;
            character.LastName = string.Empty;
            character.Coordinates(
                new Coordinate { x = definition.X, y = definition.Y, z = definition.Z });
            character.RawHeading =
                new AORebirth.Core.Vector.Quaternion(
                    definition.HeadingX,
                    definition.HeadingY,
                    definition.HeadingZ,
                    definition.HeadingW);

            SetStat(character, StatIds.side, definition.Side);
            SetStat(character, StatIds.fatness, definition.Fatness);
            SetStat(character, StatIds.breed, definition.Breed);
            SetStat(character, StatIds.sex, definition.Sex);
            SetStat(character, StatIds.race, definition.Race);
            SetStat(character, StatIds.flags, definition.CharacterFlags);
            SetStat(character, StatIds.accountflags, 0);
            SetStat(character, StatIds.expansion, 0);
            SetStat(character, StatIds.npcfamily, definition.NpcFamily);
            SetStat(character, StatIds.losheight, definition.NpcLosHeight);
            SetStat(character, StatIds.monsterdata, definition.MonsterData);
            SetStat(character, StatIds.monsterscale, definition.MonsterScale);
            SetStat(character, StatIds.headmesh, definition.HeadMesh);
            SetStat(character, StatIds.visualflags, definition.VisualFlags);
            SetStat(character, StatIds.currentmovementmode, (int)MoveModes.Sit);
            SetStat(character, StatIds.prevmovementmode, (int)MoveModes.Sit);
            SetStat(character, StatIds.runspeed, definition.RunSpeed);
            SetStat(character, StatIds.level, definition.Level);
            SetStat(character, StatIds.life, definition.Health);
            SetStat(character, StatIds.health, definition.Health);

            character.Textures.Clear();
            foreach (WindcallerKarrecNpcTextureDefinition texture in definition.Textures)
            {
                character.Textures.Add(new AOTextures(texture.Place, texture.Id));
            }

            foreach (WindcallerKarrecNpcMeshDefinition mesh in definition.Meshes)
            {
                character.MeshLayer.AddMesh(
                    mesh.Position,
                    (int)mesh.Id,
                    mesh.OverrideTextureId,
                    mesh.Layer);
                character.SocialMeshLayer.AddMesh(
                    mesh.Position,
                    (int)mesh.Id,
                    mesh.OverrideTextureId,
                    mesh.Layer);
            }

            character.Waypoints.Clear();
            foreach (WindcallerKarrecNpcWaypointDefinition waypoint in definition.ScfuWaypoints)
            {
                character.AddWaypoint(
                    new AORebirth.Core.Vector.Vector3(waypoint.X, waypoint.Y, waypoint.Z),
                    false);
            }

            if (definition.HasPatrol)
            {
                controller.SetCapturedPatrolReplaySegments(BuildPatrolReplay(definition));
                controller.State = CharacterState.Patrolling;
            }

            character.DoNotDoTimers = !definition.HasPatrol;
            return character;
        }

        private static NpcPatrolReplaySegment[] BuildPatrolReplay(
            WindcallerKarrecNpcDefinition definition)
        {
            return definition.PatrolSegments.Select(
                segment =>
                    new NpcPatrolReplaySegment(
                        segment.DelayAfterSeconds,
                        segment.StartX,
                        segment.StartY,
                        segment.StartZ,
                        segment.EndX,
                        segment.EndY,
                        segment.EndZ,
                        segment.MoveMode)).ToArray();
        }

        private static void SetStat(ICharacter character, StatIds stat, int value)
        {
            character.Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
        }
    }

}
