namespace ZoneEngine.Core.Playfields
{
    #region Usings

    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;

    using Utility;

    using ZoneEngine.Core.InternalMessages;

    #endregion

    internal sealed class PlayfieldAOtomationDeliveryRuntimeService
    {
        internal void SendMessageToClient(IMSendAOtomationMessageToClient clientMessage)
        {
            LogUtil.Debug(DebugInfoDetail.AoTomation, clientMessage.message.Body.GetType().ToString());
            clientMessage.client.SendCompressed(clientMessage.message.Body);
        }

        internal void SendMessageBodyToClient(IMSendAOtomationMessageBodyToClient message)
        {
            if (message.client != null)
            {
                try
                {
                    LogUtil.Debug(DebugInfoDetail.AoTomation, message.Body.GetType().ToString());
                    message.client.SendCompressed(message.Body);
                }
                catch (Exception exception)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        message.Body.GetType().ToString() + Environment.NewLine + exception.Message);
                    // /!\ This happens sometimes, dont know why tho, need more investigation
                    // throw;
                }
            }
        }

        internal void SendMessageBodiesToClient(IMSendAOtomationMessageBodiesToClient message)
        {
            foreach (MessageBody messageBody in message.Bodies)
            {
                message.client.SendCompressed(messageBody);
            }
        }

        internal void SendMessageToPlayfield(
            IMSendAOtomationMessageToPlayfield clientMessage,
            Action<MessageBody> dispatchToPlayfield)
        {
            dispatchToPlayfield(clientMessage.Body);
        }

        internal void SendMessageToPlayfieldOthers(
            IMSendAOtomationMessageToPlayfieldOthers clientMessage,
            Action<MessageBody, Identity> dispatchToPlayfieldOthers)
        {
            dispatchToPlayfieldOthers(clientMessage.Body, clientMessage.Identity);
        }
    }
}
