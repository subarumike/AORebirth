namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Packets;
    using ZoneEngine.Core.Playfields;

    #endregion

    internal sealed class CapturedAreteRobotSpawnOrchestrator
    {
        private const int PrivateAretePlayfieldInstance = 6553;

        private readonly CapturedAreteRobotContentProvider capturedRobotContent;

        private readonly NpcPatrolReplayCoordinator patrolReplay;

        internal CapturedAreteRobotSpawnOrchestrator(
            CapturedAreteRobotContentProvider capturedRobotContent,
            NpcPatrolReplayCoordinator patrolReplay)
        {
            this.capturedRobotContent = capturedRobotContent;
            this.patrolReplay = patrolReplay;
        }

        internal void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity)
        {
            if (playfieldIdentity.Instance != PrivateAretePlayfieldInstance)
            {
                return;
            }

            CapturedAreteRobotSpawnDefinition[] spawns = this.capturedRobotContent.GetSpawnDefinitions();
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnRowsLoaded,
                PlayfieldLifecycleTrace.MessageCapturedAreteRobotSpawnRowsLoaded,
                playfieldIdentity,
                PlayfieldLifecycleTrace.FormatCapturedAreteRobotSpawnRowsDetail(
                    spawns.Length,
                    CapturedAreteRobotContentProvider.MonsterData));

            foreach (CapturedAreteRobotSpawnDefinition spawn in spawns)
            {
                this.SpawnCapturedAreteCleaningRobot(playfield, playfieldIdentity, spawn);
            }
        }

        private void SpawnCapturedAreteCleaningRobot(
            Playfield playfield,
            Identity playfieldIdentity,
            CapturedAreteRobotSpawnDefinition spawn)
        {
            var npcController = new NPCController();
            Character mobCharacter = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                CombatTestMobArchetype.TemplateHash,
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
                        "Captured Arete robot spawn failed source=20260629-193121 sourceIdentity=SimpleChar:{0:X8}",
                        spawn.SourceInstance));
                return;
            }

            mobCharacter.Name = CapturedAreteRobotContentProvider.RobotName;
            mobCharacter.Playfield = playfield;
            CombatTestMobArchetype.Prepare(mobCharacter, CombatTestMobArchetype.MalfunctioningCleaningRobot);
            SetCapturedMobStat(mobCharacter, StatIds.life, spawn.Health);
            SetCapturedMobStat(mobCharacter, StatIds.health, spawn.Health);
            SetCapturedMobStat(mobCharacter, StatIds.level, spawn.Level);
            SetCapturedMobStat(mobCharacter, StatIds.runspeed, spawn.RunSpeed);
            mobCharacter.Coordinates(new Coordinate { x = spawn.X, y = spawn.Y, z = spawn.Z });
            AssignCapturedPatrolWaypoints(mobCharacter, spawn);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated,
                PlayfieldLifecycleTrace.MessageCapturedAreteRobotSpawnCreated,
                mobCharacter.Identity,
                PlayfieldLifecycleTrace.FormatCapturedAreteRobotSpawnCreatedDetail(
                    spawn.SourceInstance,
                    CapturedAreteRobotContentProvider.MonsterData,
                    spawn.Health,
                    spawn.Level,
                    spawn.RunSpeed,
                    spawn.X,
                    spawn.Y,
                    spawn.Z,
                    spawn.PatrolX,
                    spawn.PatrolY,
                    spawn.PatrolZ));

            int replaySegmentCount = 0;
            this.patrolReplay.AssignCapturedAreteRobotReplay(
                spawn.SourceInstance,
                segments =>
                {
                    replaySegmentCount = segments == null ? 0 : segments.Length;
                    npcController.SetCapturedPatrolReplaySegments(segments);
                });
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned,
                PlayfieldLifecycleTrace.MessageCapturedAreteRobotPatrolReplayAssigned,
                mobCharacter.Identity,
                PlayfieldLifecycleTrace.FormatCapturedAreteRobotPatrolReplayAssignedDetail(
                    spawn.SourceInstance,
                    replaySegmentCount));

            mobCharacter.DoNotDoTimers = false;
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                PlayfieldLifecycleTrace.StageCapturedAreteRobotSimpleCharFullUpdateBroadcast,
                PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate,
                mobCharacter.Identity,
                PlayfieldLifecycleTrace.FormatCapturedAreteRobotSimpleCharFullUpdateDetail(spawn.SourceInstance));
            playfield.Announce(SimpleCharFullUpdate.ConstructMessage(mobCharacter));

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Captured Arete robot spawned source=20260629-193121 sourceIdentity=SimpleChar:{0:X8} serverIdentity={1} pos=({2},{3},{4}) health={5} level={6} runSpeed={7}",
                    spawn.SourceInstance,
                    mobCharacter.Identity,
                    spawn.X,
                    spawn.Y,
                    spawn.Z,
                    spawn.Health,
                    spawn.Level,
                    spawn.RunSpeed));
        }

        private static void AssignCapturedPatrolWaypoints(
            ICharacter mobCharacter,
            CapturedAreteRobotSpawnDefinition spawn)
        {
            mobCharacter.Waypoints.Clear();
            mobCharacter.AddWaypoint(
                new AORebirth.Core.Vector.Vector3(spawn.X, spawn.Y, spawn.Z),
                false);
            mobCharacter.AddWaypoint(
                new AORebirth.Core.Vector.Vector3(spawn.PatrolX, spawn.PatrolY, spawn.PatrolZ),
                false);
            mobCharacter.Controller.State = CharacterState.Patrolling;
        }

        private static void SetCapturedMobStat(ICharacter mobCharacter, StatIds stat, int value)
        {
            mobCharacter.Stats[stat].Value = value;
            mobCharacter.Stats[stat].BaseValue = (uint)value;
        }
    }
}
