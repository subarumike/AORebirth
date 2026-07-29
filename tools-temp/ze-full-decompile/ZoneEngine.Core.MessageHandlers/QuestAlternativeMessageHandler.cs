using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class QuestAlternativeMessageHandler : BaseMessageHandler<QuestAlternativeMessage, QuestAlternativeMessageHandler>
{
	protected override void Read(QuestAlternativeMessage message, IZoneClient client)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || client.Controller == null || client.Controller.Character == null)
		{
			return;
		}
		ICharacter character = client.Controller.Character;
		((IClient)client).Server.Info((IClient)(object)client, "QuestAlternative roll request terminal={0} sliders=[lvl={1} gb={2} oc={3} oh={4} pm={5} hs={6} me={7}] existingOffers={8}", new object[9]
		{
			message.MissionTerminalIdentity,
			message.LevelSlider,
			message.GoodBadSlider,
			message.OrderChaosSlider,
			message.OpenHiddenSlider,
			message.PhysicalMysticalSlider,
			message.HeadOnStealthSlider,
			message.MoneyExperienceSlider,
			(message.QuestInfos != null) ? message.QuestInfos.Length : 0
		});
		try
		{
			int value = ((IStats)character).Stats[(StatIds)54].Value;
			int missionQuality = MissionLevelTable.GetMissionQuality(value, message.LevelSlider);
			int num;
			Identity identity;
			if (((IInstancedEntity)character).Playfield == null)
			{
				num = 0;
			}
			else
			{
				identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
				num = ((Identity)(ref identity)).Instance;
			}
			int terminalPlayfieldId = num;
			QuestAlternativeMessage val = MissionRollService.BuildRollResponse(message, ((IEntity)character).Identity, value, terminalPlayfieldId);
			client.SendCompressed((MessageBody)(object)val);
			identity = ((IEntity)character).Identity;
			MissionOfferStore.StoreRoll(((Identity)(ref identity)).Instance, val.QuestInfos);
			((IClient)client).Server.Info((IClient)(object)client, "QuestAlternative roll response sent offers={0} charLvl={1} slider={2} ql={3} terminal={4}", new object[5]
			{
				(val.QuestInfos != null) ? val.QuestInfos.Length : 0,
				value,
				message.LevelSlider,
				missionQuality,
				val.MissionTerminalIdentity
			});
		}
		catch (Exception ex)
		{
			((IClient)client).Server.Info((IClient)(object)client, "QuestAlternative roll response failed: {0}", new object[1] { ex });
		}
	}
}
