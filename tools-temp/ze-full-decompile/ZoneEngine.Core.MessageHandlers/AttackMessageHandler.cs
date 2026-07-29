using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Arete.Dialogue;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class AttackMessageHandler : BaseMessageHandler<AttackMessage, AttackMessageHandler>
{
	private const int SimpleCharFullUpdateIsImmuneFlag = 8388608;

	private const int RangedCombatStartSpecialAttackUnknown1 = -53;

	private const int RangedCombatStartSpecialAttackUnknown2 = 1306;

	private const int RangedCombatStartSpecialAttackUnknown3 = -53;

	private const int RangedCombatStartSpecialAttackUnknown4 = 2439;

	private const int RangedCombatStartSpecialAttackUnknown5 = -100;

	private const int CombatStartSpecialAttackUnknown1 = 13;

	private const int CombatStartSpecialAttackUnknown2 = 25;

	private const int CombatStartSpecialAttackUnknown3 = 13;

	private const int CombatStartSpecialAttackUnknown4 = 33;

	private const int CombatStartSpecialAttackUnknown5 = 100;

	protected override void Read(AttackMessage message, IZoneClient client)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)character).Playfield).Identity, message.Target);
		((IClient)client).Server.Info((IClient)(object)client, "Attack action={0} target={1} targetFound={2} targetHealth={3}", new object[4]
		{
			message.Action,
			message.Target,
			@object != null,
			(@object != null) ? ((IStats)@object).Stats[(StatIds)27].Value : 0
		});
		CombatStartPacketDiagnostics.LogAttackCommand(character, message.Target, message.Action, @object);
		if (@object == null)
		{
			CancelPlayerAttack(character);
			SendAttackState(character, Identity.None, 0);
		}
		else if (ContentDrivenNpcDialogueRouter.ShouldSuppressCombat(@object) || IsImmuneTarget(@object))
		{
			CancelPlayerAttack(character);
			SendAttackState(character, Identity.None, 0);
			((IClient)client).Server.Info((IClient)(object)client, "Attack ignored for non-attackable target.", Array.Empty<object>());
		}
		else if (!PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat(character, @object))
		{
			CancelPlayerAttack(character);
			SendAttackState(character, Identity.None, 0);
			((IClient)client).Server.Info((IClient)(object)client, "Attack ignored: suppression gas / PvP flag rules.", Array.Empty<object>());
		}
		else
		{
			StartPlayerAttack(character, message.Target);
			EngageNpcTarget(character, @object);
			SendCombatStartSpecialAttackWeapon(character);
			SendAttackState(character, message.Target, message.Action);
		}
	}

	private void StartPlayerAttack(ICharacter character, Identity target)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (((IInstancedEntity)character).Playfield is Playfield playfield)
		{
			playfield.StartPlayerAttack(character, target);
			return;
		}
		((ITargetingEntity)character).SetTarget(target);
		((ITargetingEntity)character).SetFightingTarget(target);
		ResetCombatTick(character);
	}

	private void CancelPlayerAttack(ICharacter character)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		if (((IInstancedEntity)character).Playfield is Playfield playfield)
		{
			playfield.CancelPlayerAttack(character);
			return;
		}
		((ITargetingEntity)character).SetFightingTarget(Identity.None);
		ResetCombatTick(character);
	}

	private void ResetCombatTick(ICharacter character)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (((IInstancedEntity)character).Playfield is Playfield playfield)
		{
			playfield.ResetCombatTick(((IEntity)character).Identity);
		}
	}

	private static bool IsImmuneTarget(ICharacter target)
	{
		return target != null && (((IStats)target).Stats[(StatIds)0].Value & 0x800000) == 8388608;
	}

	private void EngageNpcTarget(ICharacter character, ICharacter target)
	{
		if (((IInstancedEntity)target).Playfield is Playfield playfield)
		{
			playfield.AcquireNpcAggro(character, target);
		}
	}

	private void SendAttackState(ICharacter character, Identity target, byte action)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		CombatStartPacketDiagnostics.LogOutbound("AttackMessageHandler.SendAttackState", (MessageBody)new AttackMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0,
			Target = target,
			Action = action
		}, Identity.None);
		base.SendToPlayfield(character, (MessageDataFiller<AttackMessage>)delegate(AttackMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Target = target;
			x.Action = action;
		});
	}

	private void SendCombatStartSpecialAttackWeapon(ICharacter character)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Expected O, but got Unknown
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		bool flag = WeaponSupportsRangedSpecials(character);
		SpecialAttackWeaponMessage val = new SpecialAttackWeaponMessage
		{
			Identity = ((IEntity)character).Identity,
			Specials = CreateDefaultPlayerSpecialAttacks(),
			Unknown1 = (flag ? (-53) : 13),
			Unknown2 = (flag ? 1306 : 25),
			Unknown3 = (flag ? (-53) : 13),
			Unknown4 = (flag ? 2439 : 33),
			Unknown5 = (flag ? (-100) : 100)
		};
		CombatStartPacketDiagnostics.LogOutbound("AttackMessageHandler.SendCombatStartSpecialAttackWeapon", (MessageBody)(object)val, Identity.None);
		((IInstancedEntity)character).Playfield.Announce((MessageBody)(object)val);
	}

	private static bool WeaponSupportsRangedSpecials(ICharacter character)
	{
		if (character == null || ((IItemContainer)character).BaseInventory == null)
		{
			return false;
		}
		if (!((IItemContainer)character).BaseInventory.Pages.TryGetValue(101, out var value))
		{
			return false;
		}
		IItem item = value[6];
		IItem item2 = value[8];
		return ItemSupportsRangedSpecial(item) || ItemSupportsRangedSpecial(item2);
	}

	private static bool ItemSupportsRangedSpecial(IItem item)
	{
		if (item == null)
		{
			return false;
		}
		int attribute = item.GetAttribute(30);
		return ((uint)attribute & 0x1000u) != 0 || (attribute & 0x800) != 0;
	}

	private static SpecialAttack[] CreateDefaultPlayerSpecialAttacks()
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Expected O, but got Unknown
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Expected O, but got Unknown
		return (SpecialAttack[])(object)new SpecialAttack[3]
		{
			new SpecialAttack
			{
				Unknown1 = 43712,
				Unknown2 = 144745,
				Unknown3 = 100,
				Unknown4 = "MAAT"
			},
			new SpecialAttack
			{
				Unknown1 = 42033,
				Unknown2 = 42032,
				Unknown3 = 144,
				Unknown4 = "DIIT"
			},
			new SpecialAttack
			{
				Unknown1 = 70292,
				Unknown2 = 70293,
				Unknown3 = 142,
				Unknown4 = "BRAW"
			}
		};
	}
}
