namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    #endregion

    internal sealed class PlayfieldStatUpdateRuntimeService
    {
        internal void SendChangedStats(ICharacter character, Action<ICharacter> sendChangedStats)
        {
            Require(sendChangedStats, "sendChangedStats");

            sendChangedStats(character);
        }

        internal void SendChangedStatsIfChanged(
            ICharacter character,
            bool changed,
            Action<ICharacter> sendChangedStats)
        {
            Require(sendChangedStats, "sendChangedStats");

            if (changed)
            {
                sendChangedStats(character);
            }
        }

        internal void SendChangedStatsIfClient(
            ICharacter character,
            Func<ICharacter, bool> hasClient,
            Action<ICharacter> sendChangedStats)
        {
            Require(hasClient, "hasClient");
            Require(sendChangedStats, "sendChangedStats");

            if (hasClient(character))
            {
                sendChangedStats(character);
            }
        }

        internal void RunPlayerDeathStatUpdateSequence(
            ICharacter target,
            Action<ICharacter> sendChangedStats,
            Action<ICharacter> cleanupDeathCombat,
            Action<ICharacter> sendDeathAnimation)
        {
            Require(sendChangedStats, "sendChangedStats");
            Require(cleanupDeathCombat, "cleanupDeathCombat");
            Require(sendDeathAnimation, "sendDeathAnimation");

            sendChangedStats(target);
            cleanupDeathCombat(target);
            sendDeathAnimation(target);
        }

        private static void Require(Delegate callback, string name)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
