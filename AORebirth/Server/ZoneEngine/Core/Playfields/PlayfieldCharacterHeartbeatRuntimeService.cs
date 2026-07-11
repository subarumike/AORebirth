namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Stats.SpecialStats;

    #endregion

    internal sealed class PlayfieldCharacterHeartbeatRuntimeService
    {
        internal void ProcessRegeneration(ICharacter dynel, Action<ICharacter> sendChangedStats)
        {
            Require(sendChangedStats, "sendChangedStats");

            bool changed = false;
            StatHealInterval healInterval = (StatHealInterval)dynel.Stats[StatIds.healinterval];
            int healIntervalSeconds = healInterval.Value;
            int healDelta = dynel.Stats[StatIds.healdelta].Value;
            if (healIntervalSeconds > 0
                && healDelta != 0
                && healInterval.LastTick < DateTime.UtcNow)
            {
                dynel.Stats[StatIds.health].Value =
                    Math.Min(dynel.Stats[StatIds.life].Value, dynel.Stats[StatIds.health].Value + healDelta);
                healInterval.LastTick = DateTime.UtcNow + TimeSpan.FromSeconds(healIntervalSeconds);
                changed = true;
            }

            StatNanoInterval nanoInterval = (StatNanoInterval)dynel.Stats[StatIds.nanointerval];
            int nanoIntervalSeconds = nanoInterval.Value;
            int nanoDelta = dynel.Stats[StatIds.nanodelta].Value;
            if (nanoIntervalSeconds > 0
                && nanoDelta != 0
                && nanoInterval.LastTick < DateTime.UtcNow)
            {
                dynel.Stats[StatIds.currentnano].Value += nanoDelta;
                nanoInterval.LastTick = DateTime.UtcNow + TimeSpan.FromSeconds(nanoIntervalSeconds);
                changed = true;
            }

            if (changed)
            {
                sendChangedStats(dynel);
            }
        }

        internal void ProcessFollow(ICharacter dynel)
        {
            if (dynel.Controller.IsFollowing())
            {
                dynel.Controller.DoFollow();
            }
        }

        internal void ProcessPlayerCollisionChecks(
            ICharacter dynel,
            Action<ICharacter> checkWallCollision,
            Action<ICharacter> checkStatelCollision)
        {
            Require(checkWallCollision, "checkWallCollision");
            Require(checkStatelCollision, "checkStatelCollision");

            checkWallCollision(dynel);
            checkStatelCollision(dynel);
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
