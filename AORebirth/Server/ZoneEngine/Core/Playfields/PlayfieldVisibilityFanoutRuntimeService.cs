namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    internal sealed class PlayfieldVisibilityFanoutRuntimeService
    {
        internal void AnnounceToCharacterClients(
            IEnumerable<Character> characters,
            Action<Character> publishToCharacterClient)
        {
            Require(publishToCharacterClient, "publishToCharacterClient");

            foreach (Character entity in characters)
            {
                if (entity != null)
                {
                    if (entity.Controller.Client != null)
                    {
                        publishToCharacterClient(entity);
                    }
                }
            }
        }

        internal void AnnounceToOtherCharacterClients(
            IEnumerable<Character> characters,
            Identity excludedIdentity,
            Action<Character> publishToCharacterClient)
        {
            Require(publishToCharacterClient, "publishToCharacterClient");

            foreach (Character entity in characters)
            {
                if (entity != null)
                {
                    if (entity.Identity != excludedIdentity)
                    {
                        publishToCharacterClient(entity);
                    }
                }
            }
        }

        internal void FanoutExistingCharactersForScfu(
            ICharacter recipient,
            IEnumerable<ICharacter> characters,
            Func<ICharacter, bool> sendExistingCharacter,
            Action<ICharacter, bool, bool, bool> logVisibilityCandidate)
        {
            Require(sendExistingCharacter, "sendExistingCharacter");
            Require(logVisibilityCandidate, "logVisibilityCandidate");

            Identity dontSendTo = recipient.Identity;
            Identity playfieldIdentity = recipient.Playfield.Identity;
            foreach (ICharacter entity in characters)
            {
                bool senderEqualsRecipient = entity.Identity == dontSendTo;
                bool senderInRecipientPlayfield = entity.InPlayfield(playfieldIdentity);
                bool sent = false;
                if (senderInRecipientPlayfield && !senderEqualsRecipient)
                {
                    sent = sendExistingCharacter(entity);
                }

                bool senderIsPlayer = entity.Controller != null && entity.Controller.Client != null;
                if (senderIsPlayer || senderEqualsRecipient)
                {
                    logVisibilityCandidate(entity, senderEqualsRecipient, senderInRecipientPlayfield, sent);
                }
            }
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
