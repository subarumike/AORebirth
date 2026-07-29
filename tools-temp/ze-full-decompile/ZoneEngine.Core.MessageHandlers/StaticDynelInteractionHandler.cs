using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Network;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

public sealed class StaticDynelInteractionHandler
{
	public static readonly StaticDynelInteractionHandler Default = new StaticDynelInteractionHandler();

	private static readonly IDictionary<string, Profession> OfabProfessionVendorRequirements = new Dictionary<string, Profession>(StringComparer.OrdinalIgnoreCase)
	{
		{
			"OFADV",
			(Profession)6
		},
		{
			"OFAGT",
			(Profession)5
		},
		{
			"OFCRT",
			(Profession)8
		},
		{
			"OFDOC",
			(Profession)10
		},
		{
			"OFENF",
			(Profession)9
		},
		{
			"OFENG",
			(Profession)3
		},
		{
			"OFFIX",
			(Profession)4
		},
		{
			"OFKEE",
			(Profession)14
		},
		{
			"OFMA",
			(Profession)2
		},
		{
			"OFNT",
			(Profession)11
		},
		{
			"OFPMQ3T",
			(Profession)12
		},
		{
			"OFSHD",
			(Profession)15
		},
		{
			"OFSOL",
			(Profession)1
		},
		{
			"OFTRD",
			(Profession)7
		}
	};

	private static readonly IDictionary<Profession, string> ProfessionFeedbackNames = new Dictionary<Profession, string>
	{
		{
			(Profession)6,
			"Adventurer"
		},
		{
			(Profession)5,
			"Agent"
		},
		{
			(Profession)8,
			"Bureaucrat"
		},
		{
			(Profession)10,
			"Doctor"
		},
		{
			(Profession)9,
			"Enforcer"
		},
		{
			(Profession)3,
			"Engineer"
		},
		{
			(Profession)4,
			"Fixer"
		},
		{
			(Profession)14,
			"Keeper"
		},
		{
			(Profession)2,
			"Martial Artist"
		},
		{
			(Profession)12,
			"Meta-Physicist"
		},
		{
			(Profession)11,
			"Nano-Technician"
		},
		{
			(Profession)15,
			"Shade"
		},
		{
			(Profession)1,
			"Soldier"
		},
		{
			(Profession)7,
			"Trader"
		}
	};

	private const string OfabGmRequirementFeedback = "Your GM capabilities is required to be at least 1!";

	private StaticDynelInteractionHandler()
	{
	}

	public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Expected O, but got Unknown
		if (StaticDynelInteractionRules.ResolveRouteMode(Pool.Instance.Contains(target)) != StaticDynelInteractionRouteMode.PoolOnUseOrTrade)
		{
			return false;
		}
		IEventHolder val = null;
		try
		{
			val = Pool.Instance.GetObject<IEventHolder>(((IEntity)((IInstancedEntity)client.Controller.Character).Playfield).Identity, target);
		}
		catch (Exception)
		{
		}
		if (val != null)
		{
			IEntity val2 = (IEntity)(object)((val is IEntity) ? val : null);
			if (val2 != null)
			{
				Event val3 = val.Events.FirstOrDefault((Event x) => (int)x.EventType == 0);
				if (val3 != null)
				{
					ICharacter character = client.Controller.Character;
					((IInstancedEntity)character).DoNotDoTimers = false;
					try
					{
						((IStats)character).Stats[(StatIds)389].Value = ((IStats)character).Stats[(StatIds)389].Value | 2;
					}
					catch
					{
					}
					val3.Perform(character, val2);
					BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(character, message);
					return true;
				}
				val3 = val.Events.FirstOrDefault((Event x) => (int)x.EventType == 37);
				if (val3 != null)
				{
					Vendor val4 = (Vendor)(object)((val2 is Vendor) ? val2 : null);
					if (val4 != null && TryDenyOfabProfessionVendor(client, message, val4))
					{
						return true;
					}
					val3.Perform(client.Controller.Character, val2);
					Identity identity = ((IEntity)client.Controller.Character).Identity;
					Identity val5 = default(Identity);
					((Identity)(ref val5)).Type = (IdentityType)51047;
					((Identity)(ref val5)).Instance = Pool.Instance.GetFreeInstance<TemporaryBag>(0, (IdentityType)51047);
					TemporaryBag val6 = new TemporaryBag(identity, val5, ((IEntity)client.Controller.Character).Identity, target, 255);
					client.Controller.Character.ShoppingBag = val6;
					BaseMessageHandler<TradeMessage, TradeMessageHandler>.Default.Send(client.Controller.Character, val6);
					BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(client.Controller.Character, message);
					return true;
				}
			}
		}
		return false;
	}

	private bool TryDenyOfabProfessionVendor(IZoneClient client, GenericCmdMessage message, Vendor vendor)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrEmpty(vendor.TemplateHash) || !OfabProfessionVendorRequirements.TryGetValue(vendor.TemplateHash, out var value))
		{
			return false;
		}
		ICharacter character = client.Controller.Character;
		Profession val = (Profession)((IStats)character).Stats[(StatIds)60].Value;
		if (val == value)
		{
			return false;
		}
		((IClient)client).Server.Info((IClient)(object)client, "OFAB profession vendor denied character={0} profession={1} required={2} vendor={3} hash={4}", new object[5]
		{
			((IEntity)character).Identity,
			val,
			value,
			((PooledObject)vendor).Identity,
			vendor.TemplateHash
		});
		SendOfabProfessionDeniedFeedback(character, value);
		BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.AcknowledgeDenied(character, message);
		return true;
	}

	private void SendOfabProfessionDeniedFeedback(ICharacter character, Profession requiredProfession)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		if (!ProfessionFeedbackNames.TryGetValue(requiredProfession, out var value))
		{
			value = ((object)(Profession)(ref requiredProfession)).ToString();
		}
		BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "This effect can only be utilitized by " + value + ".", 0, 0);
		BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Your GM capabilities is required to be at least 1!", 0, 0);
	}
}
