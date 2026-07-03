namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Vector;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    #endregion

    internal sealed class NPCRuntimeService
    {
        private readonly Playfield playfield;

        private readonly PlayfieldDynelRegistry dynelRegistry;

        private readonly NpcCorpseLifecycleCoordinator corpseLifecycle;

        private readonly NpcCombatTickCoordinator combatTick;

        private readonly CapturedAreteRobotContentProvider capturedAreteRobotContent;

        private readonly NpcPatrolReplayCoordinator patrolReplay;

        private readonly CapturedAreteRobotSpawnOrchestrator capturedAreteRobotSpawns;

        private readonly Dictionary<int, NpcHomeState> npcHomeStates = new Dictionary<int, NpcHomeState>();

        internal NPCRuntimeService(Playfield playfield, PlayfieldDynelRegistry dynelRegistry)
        {
            this.playfield = playfield;
            this.dynelRegistry = dynelRegistry;
            this.corpseLifecycle = new NpcCorpseLifecycleCoordinator(playfield);
            this.combatTick = new NpcCombatTickCoordinator(playfield);
            this.capturedAreteRobotContent = new CapturedAreteRobotContentProvider(LogCapturedAreteRobotContent);
            this.patrolReplay = new NpcPatrolReplayCoordinator(this.capturedAreteRobotContent);
            this.capturedAreteRobotSpawns =
                new CapturedAreteRobotSpawnOrchestrator(this.capturedAreteRobotContent, this.patrolReplay);
        }

        internal void ActivateNpc(ICharacter character)
        {
            this.dynelRegistry.Register(character);
        }

        internal void RegisterNpcHome(ICharacter character)
        {
            if (character == null || !(character.Controller is NPCController))
            {
                return;
            }

            this.npcHomeStates[character.Identity.Instance] =
                new NpcHomeState
                {
                    Coordinates = new Coordinate(character.Coordinates())
                };
        }

        internal void RemoveNpcHome(Identity identity)
        {
            this.npcHomeStates.Remove(identity.Instance);
        }

        internal void SpawnCapturedNpcContent(Identity playfieldIdentity)
        {
            this.capturedAreteRobotSpawns.SpawnForPlayfield(this.playfield, playfieldIdentity);
        }

        internal bool HasPendingDeadNpcDespawn(Identity identity)
        {
            return this.corpseLifecycle.HasPendingDeadNpcDespawn(identity);
        }

        internal void BeginNpcDeath(ICharacter attacker, ICharacter target)
        {
            this.corpseLifecycle.BeginNpcDeath(attacker, target);
        }

        internal bool ProcessDeadNpc(ICharacter character)
        {
            return this.corpseLifecycle.ProcessDeadNpc(character);
        }

        internal void FinalizeNpcDespawn(ICharacter target)
        {
            this.corpseLifecycle.FinalizeNpcDespawn(target);
            this.dynelRegistry.Unregister(target.Identity);
        }

        internal void ResetCombatTick(ICharacter attacker)
        {
            this.combatTick.ResetCombatTick(attacker);
        }

        internal void ProcessCombatTick(ICharacter attacker)
        {
            this.combatTick.ProcessCombatTick(attacker);
        }

        internal void ClearCombatTracking(Identity identity)
        {
            this.combatTick.ClearTracking(identity);
        }

        private static void LogCapturedAreteRobotContent(bool isError, string message)
        {
            LogUtil.Debug(isError ? DebugInfoDetail.Error : DebugInfoDetail.Engine, message);
        }

        private class NpcHomeState
        {
            public Coordinate Coordinates { get; set; }
        }
    }
}
