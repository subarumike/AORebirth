using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Core.Statels;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core.MessageHandlers;

public sealed class SurgeryClinicInteractionHandler
{
	public static readonly SurgeryClinicInteractionHandler Default = new SurgeryClinicInteractionHandler();

	private const string SurgeryClinicFeedback = "~&!!!\":!!!)<sHYou have 5 minutes (or until you leave the playfield) to swap implants.";

	private SurgeryClinicInteractionHandler()
	{
	}

	public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		StatelData statelData = GetStatelData(character, target);
		if (!SurgeryClinicInteractionRules.IsCapturedSurgeryClinicTerminal(target, statelData?.TemplateId ?? 0))
		{
			return false;
		}
		int num = CashStatRules.Clamp(((IStats)character).Stats[(StatIds)61].BaseValue);
		if (num < 300)
		{
			((IClient)client).Server.Info((IClient)(object)client, "Surgery clinic terminal use blocked by insufficient captured-state support char={0} target={1} cash={2} cost={3}", new object[4]
			{
				((IEntity)character).Identity,
				target,
				num,
				300
			});
			return false;
		}
		int num2 = CashStatRules.Clamp((long)num - 300L);
		((IStats)character).Stats[(StatIds)61].Set((uint)num2, false);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 61, (uint)num2);
		SendSurgeryClinicFeedback(character);
		SendSurgeryClinicCastNano(character);
		BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SetNanoDuration(character, ((IEntity)character).Identity, 157490, 90000);
		GrantSurgeryClinicImplantAccess(character);
		SendSurgeryClinicSpecialUsed(character);
		BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(character, message);
		SendSurgeryClinicSpecialAvailableDelayed(character);
		((IClient)client).Server.Info((IClient)(object)client, "Surgery clinic terminal use handled char={0} target={1} statelTemplate={2} cashBefore={3} cashAfter={4} nano={5} duration={6} implantAccessSeconds={7} evidence={8}", new object[9]
		{
			((IEntity)character).Identity,
			target,
			statelData?.TemplateId ?? 0,
			num,
			num2,
			157490.ToString("X", CultureInfo.InvariantCulture),
			90000,
			300,
			"captures/20260620-213807/events.log:51-52;captures/20260621-062224/events.log:52-71"
		});
		return true;
	}

	private static StatelData GetStatelData(ICharacter character, Identity target)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return null;
		}
		Dictionary<int, PlayfieldData> pFData = PlayfieldLoader.PFData;
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		if (!pFData.TryGetValue(((Identity)(ref identity)).Instance, out var value))
		{
			return null;
		}
		return value.Statels.FirstOrDefault(delegate(StatelData x)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			Identity identity2 = x.Identity;
			int result;
			if (((Identity)(ref identity2)).Type == ((Identity)(ref target)).Type)
			{
				identity2 = x.Identity;
				result = ((((Identity)(ref identity2)).Instance == ((Identity)(ref target)).Instance) ? 1 : 0);
			}
			else
			{
				result = 0;
			}
			return (byte)result != 0;
		});
	}

	private static void GrantSurgeryClinicImplantAccess(ICharacter character)
	{
		Character val = (Character)(object)((character is Character) ? character : null);
		if (val != null)
		{
			val.GrantImplantAccess(300);
		}
	}

	private static void SendSurgeryClinicFeedback(ICharacter character)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Expected O, but got Unknown
		IZoneClient client = ((IDynel)character).Controller.Client;
		FormatFeedbackMessage val = new FormatFeedbackMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 1,
			Unknown1 = 0,
			FormattedMessage = "~&!!!\":!!!)<sHYou have 5 minutes (or until you leave the playfield) to swap implants.",
			Unknown2 = 0
		};
		Identity identity = ((IEntity)character).Identity;
		client.SendCompressed((MessageBody)val, ((Identity)(ref identity)).Instance);
	}

	private static void SendSurgeryClinicCastNano(ICharacter character)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected O, but got Unknown
		((IDynel)character).Controller.Client.SendCompressed((MessageBody)new CastNanoSpellMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0,
			NanoId = 157490,
			Target = ((IEntity)character).Identity,
			Unknown1 = 1,
			Caster = ((IEntity)character).Identity
		});
	}

	private static void SendSurgeryClinicSpecialUsed(ICharacter character)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		((IDynel)character).Controller.Client.SendCompressed((MessageBody)new CharacterActionMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0,
			Action = (CharacterActionType)170,
			Unknown1 = 0,
			Target = Identity.None,
			Parameter1 = 124,
			Parameter2 = 5,
			Unknown2 = 0
		});
	}

	private static void SendSurgeryClinicSpecialAvailableDelayed(ICharacter character)
	{
		ThreadPool.QueueUserWorkItem(delegate
		{
			Thread.Sleep(3500);
			if (character != null && ((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null)
			{
				BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendSkillAvailable(character, 124);
			}
		});
	}
}
