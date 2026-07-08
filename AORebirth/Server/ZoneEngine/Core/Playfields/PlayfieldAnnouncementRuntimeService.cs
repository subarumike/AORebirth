namespace ZoneEngine.Core.Playfields
{
    #region Usings

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;

    #endregion

    internal sealed class PlayfieldAnnouncementRuntimeService
    {
        internal void AnnounceToCharacterClients(
            IEnumerable<Character> characters,
            MessageBody messageBody,
            Action<IZoneClient, MessageBody> sendMessageBodyToClient)
        {
            Require(sendMessageBodyToClient, "sendMessageBodyToClient");

            foreach (Character entity in characters)
            {
                if (entity != null)
                {
                    if (entity.Controller.Client != null)
                    {
                        sendMessageBodyToClient(entity.Controller.Client, messageBody);
                    }
                }
            }
        }

        internal void AnnounceToOtherCharacterClients(
            IEnumerable<Character> characters,
            Identity excludedIdentity,
            MessageBody messageBody,
            Action<IZoneClient, MessageBody> sendMessageBodyToClient)
        {
            Require(sendMessageBodyToClient, "sendMessageBodyToClient");

            foreach (Character entity in characters)
            {
                if (entity != null)
                {
                    if (entity.Identity != excludedIdentity)
                    {
                        sendMessageBodyToClient(entity.Controller.Client, messageBody);
                    }
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
