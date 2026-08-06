namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Thrak.Quests;

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

            if (awardCombatXp != null)
            {
                awardCombatXp(attacker, target);
            }
            else
            {
                MissionTokenProgressTracker.NotifyTrashKilled(attacker, target);
            }

            RexB18CObjectiveProgressTracker.TryObserveNpcDeath(attacker, target);
            FlintBioComQuestRuntime.TryObserveNpcDeath(attacker, target);
            KneecappingQuestRuntime.TryObserveNpcDeath(attacker, target);
            ThrakGardenKeySilvertailTransform.TryObserveCursedDeath(attacker, target);
        }
    }
}
