using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldAnnouncementRuntimeService
{
	internal void AnnounceToCharacterClients(IEnumerable<Character> characters, MessageBody messageBody, Action<IZoneClient, MessageBody> sendMessageBodyToClient)
	{
		Require(sendMessageBodyToClient, "sendMessageBodyToClient");
		foreach (Character character in characters)
		{
			object obj;
			if (character == null)
			{
				obj = null;
			}
			else
			{
				IController controller = ((Dynel)character).Controller;
				obj = ((controller != null) ? controller.Client : null);
			}
			if (obj != null)
			{
				sendMessageBodyToClient(((Dynel)character).Controller.Client, messageBody);
			}
		}
	}

	internal void AnnounceToOtherCharacterClients(IEnumerable<Character> characters, Identity excludedIdentity, MessageBody messageBody, Action<IZoneClient, MessageBody> sendMessageBodyToClient)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		Require(sendMessageBodyToClient, "sendMessageBodyToClient");
		foreach (Character character in characters)
		{
			if (character != null && ((PooledObject)character).Identity != excludedIdentity)
			{
				IController controller = ((Dynel)character).Controller;
				if (((controller != null) ? controller.Client : null) != null)
				{
					sendMessageBodyToClient(((Dynel)character).Controller.Client, messageBody);
				}
			}
		}
	}

	private static void Require(Delegate callback, string name)
	{
		if ((object)callback == null)
		{
			throw new ArgumentNullException(name);
		}
	}
}
