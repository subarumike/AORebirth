namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using AORebirth.Core.Entities;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Playfields;

    #endregion

    internal sealed class NPCRuntimeService
    {
        private readonly NpcCorpseLifecycleCoordinator corpseLifecycle;

        private readonly NpcCombatTickCoordinator combatTick;

        internal NPCRuntimeService(Playfield playfield)
        {
            this.corpseLifecycle = new NpcCorpseLifecycleCoordinator(playfield);
            this.combatTick = new NpcCombatTickCoordinator(playfield);
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
    }
}
