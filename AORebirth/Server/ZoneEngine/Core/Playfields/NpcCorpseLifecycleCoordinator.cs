namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    #endregion

    internal sealed class NpcCorpseLifecycleCoordinator
    {
        private readonly Dictionary<int, DateTime> deadNpcDespawnTicks = new Dictionary<int, DateTime>();

        private readonly Playfield playfield;

        private readonly Action<Identity> removeNpcHome;

        internal NpcCorpseLifecycleCoordinator(Playfield playfield, Action<Identity> removeNpcHome)
        {
            this.playfield = playfield;
            this.removeNpcHome = removeNpcHome;
        }

        internal bool HasPendingDeadNpcDespawn(Identity identity)
        {
            return this.deadNpcDespawnTicks.ContainsKey(identity.Instance);
        }

        internal bool TryGetDeadNpcDespawn(Identity identity, out DateTime despawnTick)
        {
            return this.deadNpcDespawnTicks.TryGetValue(identity.Instance, out despawnTick);
        }

        internal void ScheduleDeadNpcDespawn(ICharacter target)
        {
            this.deadNpcDespawnTicks[target.Identity.Instance] =
                DateTime.UtcNow + NpcCorpseLifecycleRules.DeadNpcDespawnDelay;
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                PlayfieldLifecycleTrace.StageDeadNpcDespawnScheduled,
                "DeadNpcDespawnScheduled",
                target.Identity,
                "delayMs=" + ((int)NpcCorpseLifecycleRules.DeadNpcDespawnDelay.TotalMilliseconds));
        }

        internal void FinalizeNpcDespawn(ICharacter target)
        {
            target.DoNotDoTimers = true;
            this.playfield.ClearCombatTracking(target.Identity);
            this.deadNpcDespawnTicks.Remove(target.Identity.Instance);
            this.removeNpcHome(target.Identity);
            this.playfield.Despawn(target.Identity);
            Pool.Instance.RemoveObject((Character)target);

            LogUtil.Debug(DebugInfoDetail.Network, string.Format("NPC despawned target={0}", target.Identity));
        }
    }
}
