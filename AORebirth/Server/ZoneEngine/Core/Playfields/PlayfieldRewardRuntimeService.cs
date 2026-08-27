namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Nascence.Quests;
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
            RosenblattPapagenaQuestRuntime.TryObserveNpcDeath(attacker, target);
            RosenblattPapagenoQuestRuntime.TryObserveNpcDeath(attacker, target);
            RosenblattCascadingSpiritQuestRuntime.TryObserveNpcDeath(attacker, target);
            RosenblattSpinetoothQuestRuntime.TryObserveNpcDeath(attacker, target);
            RosenblattDemonicQuestRuntime.TryObserveNpcDeath(attacker, target);
            RosenblattHiathlinQuestRuntime.TryObserveNpcDeath(attacker, target);
            NascenceLifeJoshuaFalkerQuestRuntime.TryObserveNpcDeath(attacker, target);
            ThrakGardenKeySilvertailTransform.TryObserveCursedDeath(attacker, target);
        }
    }
}
