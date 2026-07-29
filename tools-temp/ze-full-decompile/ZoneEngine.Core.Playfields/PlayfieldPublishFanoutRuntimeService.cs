using System;
using AORebirth.Core.Network;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using ZoneEngine.Core.InternalMessages;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldPublishFanoutRuntimeService
{
	internal void PublishMessageBodyToClient(IZoneClient client, MessageBody body, Action<object> publish)
	{
		Require(publish, "publish");
		publish(new IMSendAOtomationMessageBodyToClient
		{
			client = client,
			Body = body
		});
	}

	internal void PublishMessageToClient(IZoneClient client, Message message, Action<object> publish)
	{
		Require(publish, "publish");
		publish(new IMSendAOtomationMessageToClient
		{
			client = client,
			message = message
		});
	}

	internal void DispatchMessageToPlayfield(MessageBody body, Action<MessageBody> announce)
	{
		Require(announce, "announce");
		announce(body);
	}

	internal void DispatchMessageToPlayfieldOthers(MessageBody body, Identity excludedIdentity, Action<MessageBody, Identity> announceOthers)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		Require(announceOthers, "announceOthers");
		announceOthers(body, excludedIdentity);
	}

	private static void Require(Delegate callback, string name)
	{
		if ((object)callback == null)
		{
			throw new ArgumentNullException(name);
		}
	}
}
