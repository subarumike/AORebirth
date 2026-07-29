using System;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
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
public class CharSecSpecAttackMessageHandler : BaseMessageHandler<CharSecSpecAttackMessage, CharSecSpecAttackMessageHandler>
{
	private const int SimpleCharFullUpdateIsImmuneFlag = 8388608;

	protected override void Read(CharSecSpecAttackMessage message, IZoneClient client)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Expected O, but got Unknown
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Expected O, but got Unknown
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Expected O, but got Unknown
		if (client == null || client.Controller == null || client.Controller.Character == null)
		{
			return;
		}
		ICharacter character = client.Controller.Character;
		int stat = message.Stat;
		Identity target = message.Target;
		((IClient)client).Server.Info((IClient)(object)client, "CharSecSpecAttack stat={0} target={1}", new object[2] { stat, target });
		if (!PlayerSpecialAttackRules.IsSupportedSpecial(stat))
		{
			((IClient)client).Server.Info((IClient)(object)client, "CharSecSpecAttack ignored: unsupported special={0}", new object[1] { stat });
			return;
		}
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)character).Playfield).Identity, target);
		if (@object == null || ContentDrivenNpcDialogueRouter.ShouldSuppressCombat(@object) || IsImmuneTarget(@object) || !PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat(character, @object))
		{
			((IClient)client).Server.Info((IClient)(object)client, "CharSecSpecAttack ignored: invalid/immune target.", Array.Empty<object>());
		}
		else if (((IInstancedEntity)character).Playfield is Playfield playfield)
		{
			if (!playfield.TryApplyPlayerSpecialAttack(character, @object, stat, out var damage, out var ammoCount, out var equipSlot))
			{
				((IClient)client).Server.Info((IClient)(object)client, "CharSecSpecAttack failed: no weapon damage source.", Array.Empty<object>());
				return;
			}
			int num = PlayerSpecialAttackRules.ResolveLockSeconds(stat);
			playfield.Announce((MessageBody)new CharSecSpecAttackMessage
			{
				Identity = ((IEntity)character).Identity,
				Unknown = 0,
				Target = target,
				Stat = stat
			});
			client.SendCompressed((MessageBody)new CharacterActionMessage
			{
				Identity = ((IEntity)character).Identity,
				Unknown = 0,
				Action = (CharacterActionType)170,
				Unknown1 = 0,
				Target = Identity.None,
				Parameter1 = stat,
				Parameter2 = num,
				Unknown2 = 0
			});
			playfield.Announce((MessageBody)new SpecialAttackInfo
			{
				Identity = ((IEntity)character).Identity,
				Unknown = 0,
				Unknown1 = equipSlot,
				Unknown2 = damage,
				Unknown3 = ammoCount,
				Target = target,
				Unknown4 = stat,
				Unknown5 = 0
			});
			ScheduleSpecialAvailable(character, stat, num);
		}
	}

	private static void ScheduleSpecialAvailable(ICharacter character, int specialStatId, int lockSeconds)
	{
		int delayMs = Math.Max(1, lockSeconds) * 1000;
		ThreadPool.QueueUserWorkItem(delegate
		{
			Thread.Sleep(delayMs);
			if (character != null && ((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null)
			{
				BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendSkillAvailable(character, specialStatId);
			}
		});
	}

	private static bool IsImmuneTarget(ICharacter target)
	{
		return target != null && (((IStats)target).Stats[(StatIds)0].Value & 0x800000) == 8388608;
	}
}
