namespace AORebirth.Core.Playfields
{
    #region Usings ...

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

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Packets;
    using ZoneEngine.Core.Playfields;

    #endregion

    internal sealed class CapturedSubwaySpawnOrchestrator
    {
        private const int CapturedSubwayThiefMonsterData = 26092;
        private const int CapturedSubwayMuggerMonsterData = 203734;
        private const int CapturedSubwayViolentVagabondMonsterData = 203733;
        private const int CapturedSubwayThiefBodyMesh = 160561;
        private const int CapturedSubwayThiefBackMesh = 7777;
        private const int CapturedSubwayViolentVagabondBackMesh = 136583;

        private readonly CapturedSubwayContentProvider capturedSubwayContent;

        private readonly NpcPatrolReplayCoordinator patrolReplay;

        private readonly Action<ICharacter> activateNpc;

        private readonly Dictionary<int, CapturedSubwaySpawnDefinition> activeSpawnDefinitions =
            new Dictionary<int, CapturedSubwaySpawnDefinition>();

        private readonly Dictionary<int, CapturedSubwayRespawnState> pendingRespawns =
            new Dictionary<int, CapturedSubwayRespawnState>();

        internal CapturedSubwaySpawnOrchestrator(
            CapturedSubwayContentProvider capturedSubwayContent,
            NpcPatrolReplayCoordinator patrolReplay,
            Action<ICharacter> activateNpc)
        {
            this.capturedSubwayContent = capturedSubwayContent;
            this.patrolReplay = patrolReplay;
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

        internal void ScheduleRespawnAfterDespawn(ICharacter target, DateTime despawnedAtUtc)
        {
            if (target == null)
            {
                return;
            }

            CapturedSubwaySpawnDefinition spawn;
            if (!this.activeSpawnDefinitions.TryGetValue(target.Identity.Instance, out spawn))
            {
                return;
            }

            this.activeSpawnDefinitions.Remove(target.Identity.Instance);
            if (!spawn.HasRespawnDelay)
            {
                return;
            }

            DateTime dueAtUtc = despawnedAtUtc.AddSeconds(spawn.RespawnDelaySeconds.Value);
            this.pendingRespawns[spawn.SourceInstance] =
                new CapturedSubwayRespawnState(spawn, dueAtUtc);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Captured Subway respawn scheduled sourceIdentity=SimpleChar:{0:X8} serverIdentity={1} name={2} dueAtUtc={3:o}",
                    spawn.SourceInstance,
                    target.Identity,
                    spawn.Name,
                    dueAtUtc));
        }

        internal void ProcessDueRespawns(Playfield playfield, Identity playfieldIdentity, DateTime utcNow)
        {
            if (playfieldIdentity.Instance != CapturedSubwayContentProvider.SubwayPlayfieldInstance)
            {
                return;
            }

            foreach (CapturedSubwayRespawnState state in this.pendingRespawns
                .Where(x => x.Value.DueAtUtc <= utcNow)
                .Select(x => x.Value)
                .ToArray())
            {
                this.pendingRespawns.Remove(state.Spawn.SourceInstance);
                if (this.SpawnCapturedSubwayMob(playfield, playfieldIdentity, state.Spawn))
                {
                    continue;
                }

                DateTime retryAtUtc = utcNow.AddSeconds(5.0);
                this.pendingRespawns[state.Spawn.SourceInstance] =
                    new CapturedSubwayRespawnState(state.Spawn, retryAtUtc);

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Captured Subway respawn retry scheduled sourceIdentity=SimpleChar:{0:X8} name={1} retryAtUtc={2:o}",
                        state.Spawn.SourceInstance,
                        state.Spawn.Name,
                        retryAtUtc));
            }
        }

        private bool SpawnCapturedSubwayMob(
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
                return false;
            }

            mobCharacter.Name = spawn.Name;
            mobCharacter.Playfield = playfield;
            mobCharacter.Coordinates(new Coordinate { x = spawn.X, y = spawn.Y, z = spawn.Z });
            PrepareCapturedSubwayMob(mobCharacter, spawn);
            AssignCapturedPatrolWaypoint(mobCharacter, spawn);
            this.AssignCapturedPatrolReplay(mobCharacter, npcController, spawn);
            mobCharacter.DoNotDoTimers = false;
            var fullUpdate = SimpleCharFullUpdate.ConstructMessage(mobCharacter);
            this.activateNpc(mobCharacter);
            this.activeSpawnDefinitions[mobCharacter.Identity.Instance] = spawn;
            playfield.Announce(fullUpdate);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Captured Subway mob spawned sourceIdentity=SimpleChar:{0:X8} serverIdentity={1} name={2} monsterData={3} pos=({4},{5},{6}) health={7} level={8} runSpeed={9} section={10}",
                    spawn.SourceInstance,
                    mobCharacter.Identity,
                    spawn.Name,
                    spawn.MonsterData,
                    spawn.X,
                    spawn.Y,
                    spawn.Z,
                    spawn.Health,
                    spawn.Level,
                    spawn.RunSpeed,
                    spawn.ContentSection));

            return true;
        }

        private void AssignCapturedPatrolReplay(
            Character mobCharacter,
            NPCController npcController,
            CapturedSubwaySpawnDefinition spawn)
        {
            int replaySegmentCount = 0;
            this.patrolReplay.AssignCapturedSubwayReplay(
                spawn.SourceInstance,
                segments =>
                {
                    replaySegmentCount = segments == null ? 0 : segments.Length;
                    if (replaySegmentCount == 0)
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
                    mobCharacter.Coordinates(start);
                    mobCharacter.Waypoints.Clear();
                    mobCharacter.AddWaypoint(start, false);
                    mobCharacter.AddWaypoint(end, false);
                    npcController.SetCapturedPatrolReplaySegments(
                        segments,
                        false,
                        true,
                        spawn.UseSpawnAsPatrolStart);
                });

            if (replaySegmentCount > 0)
            {
                npcController.State = CharacterState.Patrolling;
            }
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
            else
            {
                ClearTemplateHeadMesh(mobCharacter);
            }

            if (spawn.MonsterData == CapturedSubwayThiefMonsterData
                || spawn.MonsterData == CapturedSubwayMuggerMonsterData)
            {
                ApplyCapturedSubwayThiefVisuals(mobCharacter, spawn.HeadMesh);
            }
            else if (spawn.MonsterData == CapturedSubwayViolentVagabondMonsterData)
            {
                ApplyCapturedSubwayViolentVagabondVisuals(mobCharacter, spawn.HeadMesh);
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

        private static void ClearTemplateHeadMesh(Character mobCharacter)
        {
            mobCharacter.MeshLayer.RemoveMesh(0, 0, 0, 4);
            mobCharacter.SocialMeshLayer.RemoveMesh(0, 0, 0, 4);
        }

        private static void ApplyCapturedSubwayThiefVisuals(Character mobCharacter, int headMesh)
        {
            mobCharacter.Textures.Clear();
            mobCharacter.Textures.Add(new AOTextures(0, 0x24CA));
            mobCharacter.Textures.Add(new AOTextures(1, 0x2219));
            mobCharacter.Textures.Add(new AOTextures(2, 0x24CC));
            mobCharacter.Textures.Add(new AOTextures(3, 0x24CB));
            mobCharacter.Textures.Add(new AOTextures(4, 0x24CD));

            mobCharacter.MeshLayer.AddMesh(0, CapturedSubwayThiefBodyMesh, 0, 2);
            mobCharacter.MeshLayer.AddMesh(0, headMesh, 0, 4);
            mobCharacter.MeshLayer.AddMesh(1, CapturedSubwayThiefBackMesh, 0, 2);
            mobCharacter.SocialMeshLayer.AddMesh(0, CapturedSubwayThiefBodyMesh, 0, 2);
            mobCharacter.SocialMeshLayer.AddMesh(0, headMesh, 0, 4);
            mobCharacter.SocialMeshLayer.AddMesh(1, CapturedSubwayThiefBackMesh, 0, 2);
        }

        private static void ApplyCapturedSubwayViolentVagabondVisuals(
            Character mobCharacter,
            int headMesh)
        {
            mobCharacter.Textures.Clear();
            mobCharacter.Textures.Add(new AOTextures(0, 0));
            mobCharacter.Textures.Add(new AOTextures(1, 21824));
            mobCharacter.Textures.Add(new AOTextures(2, 0));
            mobCharacter.Textures.Add(new AOTextures(3, 21819));
            mobCharacter.Textures.Add(new AOTextures(4, 21831));

            mobCharacter.MeshLayer.AddMesh(0, headMesh, 0, 4);
            mobCharacter.MeshLayer.AddMesh(1, CapturedSubwayViolentVagabondBackMesh, 0, 2);
            mobCharacter.SocialMeshLayer.AddMesh(0, headMesh, 0, 4);
            mobCharacter.SocialMeshLayer.AddMesh(1, CapturedSubwayViolentVagabondBackMesh, 0, 2);
        }

        private static void SetMobStat(ICharacter mobCharacter, StatIds stat, int value)
        {
            mobCharacter.Stats[stat].Value = value;
            mobCharacter.Stats[stat].BaseValue = (uint)value;
        }

        private sealed class CapturedSubwayRespawnState
        {
            public CapturedSubwayRespawnState(CapturedSubwaySpawnDefinition spawn, DateTime dueAtUtc)
            {
                this.Spawn = spawn;
                this.DueAtUtc = dueAtUtc;
            }

            public CapturedSubwaySpawnDefinition Spawn { get; private set; }

            public DateTime DueAtUtc { get; private set; }
        }
    }
}
