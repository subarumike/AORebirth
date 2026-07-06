namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using ZoneEngine.Core.Arete.Quests;

    #endregion

    internal sealed class PlayfieldRewardRuntimeService
    {
        internal void RunNpcDeathRewardHooks(
            ICharacter attacker,
            ICharacter target,
            Action<ICharacter, ICharacter> awardCombatXp)
        {
            if (attacker == null)
            {
                return;
            }

            RexB18CObjectiveProgressTracker.TryObserveNpcDeath(attacker, target);

            if (awardCombatXp != null)
            {
                awardCombatXp(attacker, target);
            }
        }
    }
}
