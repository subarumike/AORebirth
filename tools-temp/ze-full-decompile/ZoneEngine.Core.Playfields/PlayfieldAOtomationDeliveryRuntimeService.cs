using System;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using Utility;
using ZoneEngine.Core.InternalMessages;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldAOtomationDeliveryRuntimeService
{
	internal void SendMessageToClient(IMSendAOtomationMessageToClient clientMessage)
	{
		LogUtil.Debug((DebugInfoDetail)2048, ((object)clientMessage.message.Body).GetType().ToString());
		clientMessage.client.SendCompressed(clientMessage.message.Body);
	}

	internal void SendMessageBodyToClient(IMSendAOtomationMessageBodyToClient message)
	{
		if (message.client != null)
		{
			try
			{
				LogUtil.Debug((DebugInfoDetail)2048, ((object)message.Body).GetType().ToString());
				message.client.SendCompressed(message.Body);
			}
			catch (Exception ex)
			{
				LogUtil.Debug((DebugInfoDetail)512, ((object)message.Body).GetType().ToString() + Environment.NewLine + ex.Message);
			}
		}
	}

	internal void SendMessageBodiesToClient(IMSendAOtomationMessageBodiesToClient message)
	{
		MessageBody[] bodies = message.Bodies;
		foreach (MessageBody val in bodies)
		{
			message.client.SendCompressed(val);
		}
	}

	internal void SendMessageToPlayfield(IMSendAOtomationMessageToPlayfield clientMessage, Action<MessageBody> dispatchToPlayfield)
	{
		dispatchToPlayfield(clientMessage.Body);
	}

	internal void SendMessageToPlayfieldOthers(IMSendAOtomationMessageToPlayfieldOthers clientMessage, Action<MessageBody, Identity> dispatchToPlayfieldOthers)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		dispatchToPlayfieldOthers(clientMessage.Body, clientMessage.Identity);
	}
}
