namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;

    using ZoneEngine.Core.InternalMessages;

    #endregion

    internal sealed class PlayfieldPublishFanoutRuntimeService
    {
        internal void PublishMessageBodyToClient(IZoneClient client, MessageBody body, Action<object> publish)
        {
            Require(publish, "publish");

            publish(new IMSendAOtomationMessageBodyToClient { client = client, Body = body });
        }

        internal void PublishMessageToClient(IZoneClient client, Message message, Action<object> publish)
        {
            Require(publish, "publish");

            publish(new IMSendAOtomationMessageToClient { client = client, message = message });
        }

        internal void DispatchMessageToPlayfield(MessageBody body, Action<MessageBody> announce)
        {
            Require(announce, "announce");

            announce(body);
        }

        internal void DispatchMessageToPlayfieldOthers(
            MessageBody body,
            Identity excludedIdentity,
            Action<MessageBody, Identity> announceOthers)
        {
            Require(announceOthers, "announceOthers");

            announceOthers(body, excludedIdentity);
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
