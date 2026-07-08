namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Packets;
    using ZoneEngine.Core.Playfields;

    #endregion

    internal sealed class CapturedSubwaySpawnOrchestrator
    {
        private readonly CapturedSubwayContentProvider capturedSubwayContent;

        private readonly Action<ICharacter> activateNpc;

        internal CapturedSubwaySpawnOrchestrator(
            CapturedSubwayContentProvider capturedSubwayContent,
            Action<ICharacter> activateNpc)
        {
            this.capturedSubwayContent = capturedSubwayContent;
            this.activateNpc = activateNpc;
        }

        internal void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity)
        {
            if (playfieldIdentity.Instance != CapturedSubwayContentProvider.SubwayPlayfieldInstance)
            {
                return;
            }

            CapturedSubwaySpawnDefinition[] spawns = this.capturedSubwayContent.GetSpawnDefinitions();
            foreach (CapturedSubwaySpawnDefinition spawn in spawns)
            {
                this.SpawnCapturedSubwayMob(playfield, playfieldIdentity, spawn);
            }
        }

        private void SpawnCapturedSubwayMob(
            Playfield playfield,
            Identity playfieldIdentity,
            CapturedSubwaySpawnDefinition spawn)
        {
            var npcController = new NPCController();
            Character mobCharacter = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                spawn.TemplateHash,
                playfieldIdentity,
                new Coordinate { x = spawn.X, y = spawn.Y, z = spawn.Z },
                new AORebirth.Core.Vector.Quaternion(0, 0, 0, 1),
                npcController,
                spawn.Level);

            if (mobCharacter == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Captured Subway spawn failed sourceIdentity=SimpleChar:{0:X8} name={1}",
                        spawn.SourceInstance,
                        spawn.Name));
                return;
            }

            mobCharacter.Name = spawn.Name;
            mobCharacter.Playfield = playfield;
            mobCharacter.Coordinates(new Coordinate { x = spawn.X, y = spawn.Y, z = spawn.Z });
            PrepareCapturedSubwayMob(mobCharacter, spawn);
            AssignCapturedPatrolWaypoint(mobCharacter, spawn);
            mobCharacter.DoNotDoTimers = false;
            this.activateNpc(mobCharacter);
            playfield.Announce(SimpleCharFullUpdate.ConstructMessage(mobCharacter));

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Captured Subway mob spawned sourceIdentity=SimpleChar:{0:X8} serverIdentity={1} name={2} monsterData={3} pos=({4},{5},{6}) health={7} level={8} runSpeed={9}",
                    spawn.SourceInstance,
                    mobCharacter.Identity,
                    spawn.Name,
                    spawn.MonsterData,
                    spawn.X,
                    spawn.Y,
                    spawn.Z,
                    spawn.Health,
                    spawn.Level,
                    spawn.RunSpeed));
        }

        private static void PrepareCapturedSubwayMob(Character mobCharacter, CapturedSubwaySpawnDefinition spawn)
        {
            SetMobStat(mobCharacter, StatIds.side, 3);
            SetMobStat(mobCharacter, StatIds.fatness, 1);
            SetMobStat(mobCharacter, StatIds.breed, spawn.Breed);
            SetMobStat(mobCharacter, StatIds.sex, spawn.Sex);
            SetMobStat(mobCharacter, StatIds.race, 1);
            SetMobStat(mobCharacter, StatIds.flags, spawn.CharacterFlags);
            SetMobStat(mobCharacter, StatIds.accountflags, 0);
            SetMobStat(mobCharacter, StatIds.expansion, 0);
            SetMobStat(mobCharacter, StatIds.npcfamily, spawn.NpcFamily);
            SetMobStat(mobCharacter, StatIds.losheight, 0);
            SetMobStat(mobCharacter, StatIds.monsterdata, spawn.MonsterData);
            SetMobStat(mobCharacter, StatIds.monsterscale, spawn.MonsterScale);
            SetMobStat(mobCharacter, StatIds.visualflags, 31);
            SetMobStat(mobCharacter, StatIds.catmesh, spawn.MonsterData);
            SetMobStat(mobCharacter, StatIds.displaycatmesh, spawn.MonsterData);
            SetMobStat(mobCharacter, StatIds.currentmovementmode, (int)MoveModes.Run);
            SetMobStat(mobCharacter, StatIds.prevmovementmode, (int)MoveModes.Run);
            SetMobStat(mobCharacter, StatIds.runspeed, spawn.RunSpeed);
            SetMobStat(mobCharacter, StatIds.level, spawn.Level);
            SetMobStat(mobCharacter, StatIds.life, spawn.Health);
            SetMobStat(mobCharacter, StatIds.health, spawn.Health);

            if (spawn.HeadMesh > 0)
            {
                SetHeadMesh(mobCharacter, spawn.HeadMesh);
            }
        }

        private static void AssignCapturedPatrolWaypoint(Character mobCharacter, CapturedSubwaySpawnDefinition spawn)
        {
            mobCharacter.Waypoints.Clear();
            if (!spawn.HasPatrolWaypoint)
            {
                return;
            }

            mobCharacter.AddWaypoint(new AORebirth.Core.Vector.Vector3(spawn.X, spawn.Y, spawn.Z), false);
            mobCharacter.AddWaypoint(
                new AORebirth.Core.Vector.Vector3(spawn.PatrolX.Value, spawn.PatrolY.Value, spawn.PatrolZ.Value),
                false);
            mobCharacter.Controller.State = CharacterState.Patrolling;
        }

        private static void SetHeadMesh(Character mobCharacter, int headMesh)
        {
            int existingHeadMesh = mobCharacter.Stats[StatIds.headmesh].Value;
            if (existingHeadMesh != 0 && existingHeadMesh != headMesh)
            {
                mobCharacter.MeshLayer.RemoveMesh(0, existingHeadMesh, 0, 4);
                mobCharacter.SocialMeshLayer.RemoveMesh(0, existingHeadMesh, 0, 4);
            }

            SetMobStat(mobCharacter, StatIds.headmesh, headMesh);
            mobCharacter.MeshLayer.AddMesh(0, headMesh, 0, 4);
            mobCharacter.SocialMeshLayer.AddMesh(0, headMesh, 0, 4);
        }

        private static void SetMobStat(ICharacter mobCharacter, StatIds stat, int value)
        {
            mobCharacter.Stats[stat].Value = value;
            mobCharacter.Stats[stat].BaseValue = (uint)value;
        }
    }
}
