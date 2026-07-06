namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Vector;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    internal sealed class PlayfieldPlayerDeathRespawnRuntimeService
    {
        internal void ProcessPlayerRespawn(
            ICharacter character,
            Dynel dynel,
            Identity corpseIdentity,
            Coordinate destination,
            Identity destinationPlayfield,
            Action<ICharacter, Identity> logCorpseVisualSkipped,
            Action<ICharacter> sendDeathSocialStatus,
            Action<ICharacter> markPlayerRespawned,
            Action<ICharacter> sendDeathRespawnStateStats,
            Action<ICharacter> stopMovement,
            Action<ICharacter> cleanupDeathCombat,
            Action<ICharacter> sendChangedStats,
            Action<ICharacter, Identity, Identity, Coordinate> logRespawnRequested,
            Action<ICharacter> enableTimers,
            Func<Dynel, Coordinate, IQuaternion, Identity, bool> tryCompleteCurrentPlayfieldRespawn,
            Action<Dynel, Coordinate, IQuaternion, Identity> transferToRespawnPlayfield)
        {
            Require(logCorpseVisualSkipped, "logCorpseVisualSkipped");
            Require(sendDeathSocialStatus, "sendDeathSocialStatus");
            Require(markPlayerRespawned, "markPlayerRespawned");
            Require(sendDeathRespawnStateStats, "sendDeathRespawnStateStats");
            Require(stopMovement, "stopMovement");
            Require(cleanupDeathCombat, "cleanupDeathCombat");
            Require(sendChangedStats, "sendChangedStats");
            Require(logRespawnRequested, "logRespawnRequested");
            Require(enableTimers, "enableTimers");
            Require(tryCompleteCurrentPlayfieldRespawn, "tryCompleteCurrentPlayfieldRespawn");
            Require(transferToRespawnPlayfield, "transferToRespawnPlayfield");

            logCorpseVisualSkipped(character, corpseIdentity);
            sendDeathSocialStatus(character);
            markPlayerRespawned(character);
            sendDeathRespawnStateStats(character);
            stopMovement(character);
            cleanupDeathCombat(character);
            sendChangedStats(character);
            logRespawnRequested(character, corpseIdentity, destinationPlayfield, destination);
            enableTimers(character);

            if (tryCompleteCurrentPlayfieldRespawn(dynel, destination, character.RawHeading, destinationPlayfield))
            {
                return;
            }

            transferToRespawnPlayfield(dynel, destination, character.RawHeading, destinationPlayfield);
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
