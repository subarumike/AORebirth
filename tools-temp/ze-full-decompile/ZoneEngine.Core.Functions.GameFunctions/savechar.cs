using System;
using System.Globalization;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class savechar : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53032;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		ICharacter val = (ICharacter)(object)((self is ICharacter) ? self : null);
		if (val == null)
		{
			return false;
		}
		int num = Math.Max(1, ((IStats)val).Stats[(StatIds)54].Value);
		int num2 = num * 100;
		int num3 = CashStatRules.Clamp(((IStats)val).Stats[(StatIds)61].BaseValue);
		if (num3 < num2)
		{
			SendFeedback(val, string.Format(CultureInfo.InvariantCulture, "Insurance Terminal requires {0} credits (level × 100).", num2));
			return false;
		}
		int num4 = CashStatRules.Clamp((long)num3 - (long)num2);
		((IStats)val).Stats[(StatIds)61].Set((uint)num4, false);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(val, 61, (uint)num4);
		SendFeedback(val, string.Format(CultureInfo.InvariantCulture, "{0} credits were deducted from your account.", num2));
		SendSocialStatus(val, 12);
		SaveRespawnPoint(val);
		uint savedSk;
		uint savedXp = CombatXpRuntimeService.ApplyInsuranceTerminalSave(val, out savedSk);
		SendFeedback(val, CombatXpRuntimeService.BuildSaveRewardText(num, savedXp, savedSk));
		return true;
	}

	private static void SaveRespawnPoint(ICharacter character)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		if (((IInstancedEntity)character).Playfield != null)
		{
			Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			int val = (int)Math.Round(((IDynel)character).RawCoordinates.X);
			int val2 = (int)Math.Round(((IDynel)character).RawCoordinates.Z);
			((IStats)character).Stats[(StatIds)595].Set((uint)Math.Max(0, instance), false);
			((IStats)character).Stats[(StatIds)596].Set((uint)Math.Max(0, val), false);
			((IStats)character).Stats[(StatIds)597].Set((uint)Math.Max(0, val2), false);
			((IStats)character).Stats[(StatIds)236].Set(100u, false);
			((IStats)character).Stats[(StatIds)49].Set((uint)Math.Max(0, Environment.TickCount), false);
			((IDatabaseObject)((IStats)character).Stats).Write();
		}
	}

	private static void SendSocialStatus(ICharacter character, int value)
	{
		((IStats)character).Stats[(StatIds)521].Set((uint)value, false);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 521, (uint)value);
	}

	private static void SendFeedback(ICharacter character, string text)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Expected O, but got Unknown
		BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, text, 0, 0);
		if (((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null)
		{
			IZoneClient client = ((IDynel)character).Controller.Client;
			FormatFeedbackMessage val = new FormatFeedbackMessage
			{
				Identity = ((IEntity)character).Identity,
				Unknown = 1,
				Unknown1 = 0,
				FormattedMessage = text,
				Unknown2 = 0
			};
			Identity identity = ((IEntity)character).Identity;
			client.SendCompressed((MessageBody)val, ((Identity)(ref identity)).Instance);
		}
	}
}
