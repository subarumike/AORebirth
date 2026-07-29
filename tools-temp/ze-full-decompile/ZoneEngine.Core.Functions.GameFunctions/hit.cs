using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class hit : FunctionPrototype
{
	private const FunctionType functionId = 53002;

	private const int UnarmedAttackInfoAmmoCount = -1;

	private const int AttackInfoWeaponSlot = 0;

	private const int AttackInfoUnk1 = 4;

	private const int AttackInfoHitType = 1;

	public override FunctionType FunctionId => (FunctionType)53002;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		if (target == null)
		{
			return false;
		}
		lock (target)
		{
			return FunctionExecute(self, caller, target, arguments);
		}
	}

	public bool FunctionExecute(INamedEntity Self, IEntity Caller, IInstancedEntity Target, MessagePackObject[] Arguments)
	{
		if (Arguments == null || Arguments.Length < 2)
		{
			return false;
		}
		Character val = (Character)(object)((Target is Character) ? Target : null);
		if (val == null)
		{
			return false;
		}
		Character val2 = (Character)(object)((Self is Character) ? Self : null);
		if (val2 == null)
		{
			val2 = (Character)(object)((Caller is Character) ? Caller : null);
		}
		int num = ((MessagePackObject)(ref Arguments[0])).AsInt32();
		int num2 = ResolveHitDelta(Arguments);
		switch (num)
		{
		case 27:
			return ApplyHealthDelta(val2, val, num2);
		default:
			if (num != 132)
			{
				IStat obj = ((Dynel)val).Stats[num];
				obj.Value += num2;
				SendStats(val);
				return true;
			}
			goto case 214;
		case 214:
			return ApplyNanoDelta(val, num2);
		}
	}

	internal static int ResolveHitDelta(MessagePackObject[] arguments)
	{
		int num = ((MessagePackObject)(ref arguments[1])).AsInt32();
		int num2 = num;
		if (arguments.Length >= 3)
		{
			num2 = ((MessagePackObject)(ref arguments[2])).AsInt32();
			if (arguments.Length == 3 && num < 0 && num2 > 0)
			{
				num2 = num;
			}
			else if (arguments.Length >= 4 && num < 0 && num2 > 0)
			{
				num2 = num;
			}
		}
		if (num > num2)
		{
			int num3 = num;
			num = num2;
			num2 = num3;
		}
		return (num == num2) ? num : new Random().Next(num, num2 + 1);
	}

	private static bool ApplyHealthDelta(Character source, Character affected, int delta)
	{
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Expected O, but got Unknown
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		int num = Math.Max(1, ((Dynel)affected).Stats[(StatIds)1].Value);
		int value = ((Dynel)affected).Stats[(StatIds)27].Value;
		if (delta >= 0)
		{
			int val = Math.Max(0, num - value);
			int num2 = Math.Min(delta, val);
			if (num2 <= 0)
			{
				return true;
			}
			((Dynel)affected).Stats[(StatIds)27].Value = value + num2;
			SendStats(affected);
			AnnounceHeal(source, affected, num2);
			return true;
		}
		int num3 = Math.Max(0, value + delta);
		int num4 = value - num3;
		((Dynel)affected).Stats[(StatIds)27].Value = num3;
		SendStats(affected);
		if (num4 <= 0 || source == null || ((Dynel)affected).Playfield == null)
		{
			return true;
		}
		if (!(((Dynel)affected).Playfield is Playfield playfield))
		{
			return true;
		}
		playfield.Announce((MessageBody)new AttackInfoMessage
		{
			Identity = ((PooledObject)source).Identity,
			Unknown = 0,
			Target = ((PooledObject)affected).Identity,
			Unknown1 = num4,
			Unknown2 = -1,
			Unknown3 = 0,
			Unknown4 = 4,
			Unknown5 = 1,
			Unknown6 = 0
		});
		if (((Dynel)source).Controller != null && ((Dynel)source).Controller.Client != null)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send((ICharacter)(object)source, string.Format("You hit {0} for {1} points of energy damage.", string.IsNullOrWhiteSpace(((Dynel)affected).Name) ? "target" : ((Dynel)affected).Name, num4), 0, 0);
		}
		if (((PooledObject)source).Identity != ((PooledObject)affected).Identity)
		{
			playfield.AcquireNpcAggro((ICharacter)(object)source, (ICharacter)(object)affected);
			playfield.SuspendNpcRegen((ICharacter)(object)affected);
		}
		if (num3 == 0)
		{
			playfield.HandleCombatKillingHit((ICharacter)(object)source, (ICharacter)(object)affected);
		}
		return true;
	}

	private static bool ApplyNanoDelta(Character affected, int delta)
	{
		int num = Math.Max(0, ((Dynel)affected).Stats[(StatIds)221].Value);
		int value = ((Dynel)affected).Stats[(StatIds)214].Value;
		if (delta >= 0)
		{
			int val = Math.Max(0, num - value);
			int num2 = Math.Min(delta, val);
			if (num2 <= 0)
			{
				return true;
			}
			((Dynel)affected).Stats[(StatIds)214].Value = value + num2;
		}
		else
		{
			((Dynel)affected).Stats[(StatIds)214].Value = Math.Max(0, value + delta);
		}
		SendStats(affected);
		return true;
	}

	private static void AnnounceHeal(Character source, Character affected, int healAmount)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		if (healAmount > 0)
		{
			if (source != null && ((Dynel)source).Controller != null && ((Dynel)source).Controller.Client != null)
			{
				string arg = ((((PooledObject)source).Identity == ((PooledObject)affected).Identity) ? "yourself" : (string.IsNullOrWhiteSpace(((Dynel)affected).Name) ? "target" : ((Dynel)affected).Name));
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send((ICharacter)(object)source, $"You healed {arg} for {healAmount} points.", 0, 0);
			}
			SendStats(affected);
		}
	}

	private static void SendStats(Character character)
	{
		if (((Dynel)character).Controller != null)
		{
			((Dynel)character).Controller.SendChangedStats();
		}
		else
		{
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendChanged((ICharacter)(object)character);
		}
	}
}
