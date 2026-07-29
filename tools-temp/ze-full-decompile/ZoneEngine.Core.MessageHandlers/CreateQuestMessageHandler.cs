using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Enums;
using AORebirth.Interfaces;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class CreateQuestMessageHandler : BaseMessageHandler<CreateQuestMessage, CreateQuestMessageHandler>
{
	protected override void Read(CreateQuestMessage message, IZoneClient client)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || client.Controller == null || client.Controller.Character == null)
		{
			return;
		}
		ICharacter character = client.Controller.Character;
		Identity questIdentity = message.QuestIdentity;
		Identity identity = ((IEntity)character).Identity;
		QuestInfo offer;
		bool flag = MissionOfferStore.TryGetOffer(((Identity)(ref identity)).Instance, questIdentity, out offer);
		((IClient)client).Server.Info((IClient)(object)client, "CreateQuest accept quest={0} matchedOffer={1}", new object[2] { questIdentity, flag });
		try
		{
			int keyInstance;
			InventoryError inventoryError;
			bool flag2 = MissionKeyGrantService.TryGrantMissionKey(client, character, "Mission key", out keyInstance, out inventoryError);
			if (flag2)
			{
				identity = ((IEntity)character).Identity;
				MissionKeyStore.Register(((Identity)(ref identity)).Instance, keyInstance);
			}
			else
			{
				((IClient)client).Server.Info((IClient)(object)client, "CreateQuest mission key grant failed: {0}", new object[1] { inventoryError });
			}
			bool flag3 = false;
			int itemInstance = 0;
			InventoryError inventoryError2 = (InventoryError)(-1);
			if (MissionRepairService.IsRepairOffer(offer))
			{
				int quality = ((offer == null || offer.Quality <= 0) ? 1 : offer.Quality);
				flag3 = MissionKeyGrantService.TryGrantRepairItem(client, character, quality, out itemInstance, out inventoryError2);
				if (!flag3)
				{
					((IClient)client).Server.Info((IClient)(object)client, "CreateQuest repair kit grant failed: {0}", new object[1] { inventoryError2 });
				}
			}
			bool flag4 = MissionAcceptService.SendAcceptedMission(character, offer);
			MissionDiagnostics.Log("ACCEPT quest={0} matchedOffer={1} keyGranted={2} keyInstance={3} keyError={4} repairGranted={5} repairInstance={6} windowSent={7}", questIdentity, flag, flag2, keyInstance, inventoryError, flag3, itemInstance, flag4);
			((IClient)client).Server.Info((IClient)(object)client, "CreateQuest accept complete quest={0} keyGranted={1} windowSent={2}", new object[3] { questIdentity, flag2, flag4 });
		}
		catch (Exception ex)
		{
			((IClient)client).Server.Info((IClient)(object)client, "CreateQuest accept failed: {0}", new object[1] { ex });
		}
	}
}
