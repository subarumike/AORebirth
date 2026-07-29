using System;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class lockskill : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53033;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		Character val = (Character)(object)((self is Character) ? self : null);
		if (val == null || !TryReadArguments(arguments, out var statId, out var durationSeconds))
		{
			return false;
		}
		val.LockSkill(statId, durationSeconds);
		BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendSkillUnavailable((ICharacter)(object)val, statId, durationSeconds);
		ScheduleSkillAvailable(val, statId, durationSeconds);
		LogUtil.Debug((DebugInfoDetail)256, $"LockSkill char={((PooledObject)val).Identity} stat={statId} duration={durationSeconds}");
		return true;
	}

	private static void ScheduleSkillAvailable(Character character, int statId, int durationSeconds)
	{
		if (character == null || durationSeconds <= 0)
		{
			return;
		}
		int delayMs = Math.Max(1, durationSeconds) * 1000;
		ThreadPool.QueueUserWorkItem(delegate
		{
			Thread.Sleep(delayMs);
			if (((Dynel)character).Controller != null && ((Dynel)character).Controller.Client != null && character.GetSkillLockRemainingSeconds(statId) <= 0)
			{
				BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendSkillAvailable((ICharacter)(object)character, statId);
			}
		});
	}

	public static bool TryReadArguments(MessagePackObject[] arguments, out int statId, out int durationSeconds)
	{
		statId = 0;
		durationSeconds = 0;
		if (arguments == null || arguments.Length < 2)
		{
			return false;
		}
		int num = ((MessagePackObject)(ref arguments[0])).AsInt32();
		int num2 = ((MessagePackObject)(ref arguments[1])).AsInt32();
		if (Enum.IsDefined(typeof(StatIds), num))
		{
			statId = num;
			durationSeconds = num2;
			return durationSeconds > 0;
		}
		if (arguments.Length >= 3)
		{
			statId = num2;
			durationSeconds = ((MessagePackObject)(ref arguments[2])).AsInt32();
			return durationSeconds > 0;
		}
		return false;
	}
}
