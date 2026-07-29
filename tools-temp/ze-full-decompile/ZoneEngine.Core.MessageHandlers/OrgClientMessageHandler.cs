using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Interfaces;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.PacketHandlers;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class OrgClientMessageHandler : BaseMessageHandler<OrgClientMessage, OrgClientMessageHandler>
{
	protected override void Read(OrgClientMessage message, IZoneClient client)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Invalid comparison between Unknown and I4
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Invalid comparison between Unknown and I4
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		if (client is ZoneClient client2 && !OrgClient.TryHandleCapturedOrgInfo(message, client2) && !OrgClient.TryHandleCapturedCityControllerBankAdd(message, client2))
		{
			if ((int)message.Command == 31)
			{
				SendCapturedCityAdvantages(message, client2);
			}
			else if ((int)message.Command == 19)
			{
				((IClient)client).Server.Info((IClient)(object)client, "OrgClient BankAdd ignored target={0} unknown1={1} args={2} evidence_scope=private_city_compat", new object[3]
				{
					message.Target,
					message.Unknown1,
					message.CommandArgs ?? string.Empty
				});
			}
		}
	}

	private static void SendCapturedCityAdvantages(OrgClientMessage message, ZoneClient client)
	{
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Expected O, but got Unknown
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		if (client.Controller == null || client.Controller.Character == null)
		{
			((ClientBase)client).Server.Info((IClient)(object)client, "OrgClient CityAdvantages ignored target={0} unknown1={1} args={2} reason=no_character evidence=live_capture_20260622-093102", new object[3]
			{
				message.Target,
				message.Unknown1,
				message.CommandArgs ?? string.Empty
			});
		}
		else
		{
			ICharacter character = client.Controller.Character;
			client.SendCompressed((MessageBody)new CityAdvantagesMessage
			{
				Identity = ((IEntity)character).Identity,
				Unknown = 1,
				Advantages = CreateCapturedCityAdvantages()
			});
			((ClientBase)client).Server.Info((IClient)(object)client, "OrgClient CityAdvantages responded character={0} count=4 evidence=live_capture_20260622-093102 no_state_change=1", new object[1] { ((IEntity)character).Identity });
		}
	}

	private static CityAdvantage[] CreateCapturedCityAdvantages()
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Expected O, but got Unknown
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Expected O, but got Unknown
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Expected O, but got Unknown
		return (CityAdvantage[])(object)new CityAdvantage[4]
		{
			new CityAdvantage
			{
				LowId = 254403,
				HighId = 254403,
				QualityLevel = 300,
				Unknown = 0
			},
			new CityAdvantage
			{
				LowId = 254387,
				HighId = 254387,
				QualityLevel = 300,
				Unknown = 0
			},
			new CityAdvantage
			{
				LowId = 254406,
				HighId = 254406,
				QualityLevel = 300,
				Unknown = 0
			},
			new CityAdvantage
			{
				LowId = 254395,
				HighId = 254395,
				QualityLevel = 300,
				Unknown = 0
			}
		};
	}
}
