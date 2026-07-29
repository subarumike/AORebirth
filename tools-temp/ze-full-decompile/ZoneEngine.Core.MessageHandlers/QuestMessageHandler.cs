using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Interfaces;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class QuestMessageHandler : BaseMessageHandler<QuestMessage, QuestMessageHandler>
{
	protected override void Read(QuestMessage message, IZoneClient client)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Invalid comparison between Unknown and I4
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Expected O, but got Unknown
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || client.Controller == null || client.Controller.Character == null || (int)message.Action != 1)
		{
			return;
		}
		ICharacter character = client.Controller.Character;
		try
		{
			client.SendCompressed((MessageBody)new QuestMessage
			{
				Identity = ((IEntity)character).Identity,
				Unknown = 0,
				Action = (QuestAction)1,
				Unknown1 = 0,
				Mission = message.Mission,
				Unknown2 = 0,
				Unknown3 = 0
			});
			bool flag = false;
			Identity identity = ((IEntity)character).Identity;
			if (MissionKeyStore.TryTakeLatest(((Identity)(ref identity)).Instance, out var keyInstance))
			{
				flag = MissionKeyGrantService.TryRemoveMissionKey(client, character, keyInstance);
			}
			identity = ((IEntity)character).Identity;
			bool flag2 = MissionAcceptedStore.Remove(((Identity)(ref identity)).Instance, message.Mission);
			((IClient)client).Server.Info((IClient)(object)client, "Quest delete mission={0} keyRemoved={1} storeRemoved={2}", new object[3] { message.Mission, flag, flag2 });
		}
		catch (Exception ex)
		{
			((IClient)client).Server.Info((IClient)(object)client, "Quest delete failed: {0}", new object[1] { ex });
		}
	}
}
