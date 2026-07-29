using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Enums;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.InternalMessages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class SkillMessageHandler : BaseMessageHandler<SkillMessage, SkillMessageHandler>
{
	protected override void Read(SkillMessage skillMessage, IZoneClient client)
	{
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Expected I4, but got Unknown
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Expected I4, but got Unknown
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Expected O, but got Unknown
		uint num = 0u;
		uint num2 = ((IStats)client.Controller.Character).Stats[(StatIds)54].BaseValue;
		if (num2 > 204)
		{
			num += (num2 - 204) * 600000;
			num2 = 204u;
		}
		if (num2 > 189)
		{
			num += (num2 - 189) * 150000;
			num2 = 189u;
		}
		if (num2 > 149)
		{
			num += (num2 - 149) * 80000;
			num2 = 149u;
		}
		if (num2 > 99)
		{
			num += (num2 - 99) * 40000;
			num2 = 99u;
		}
		if (num2 > 49)
		{
			num += (num2 - 49) * 20000;
			num2 = 49u;
		}
		if (num2 > 14)
		{
			num += (num2 - 14) * 10000;
			num2 = 14u;
		}
		num += 1500 + (num2 - 1) * 4000;
		int num3 = skillMessage.Skills.Length;
		List<int> list = new List<int>();
		while (num3 > 0)
		{
			num3--;
			GameTuple<CharacterStat, uint> val = skillMessage.Skills[num3];
			((IStats)client.Controller.Character).Stats[(int)val.Value1].Value = (int)val.Value2;
			list.Add((int)val.Value1);
		}
		list.Add(53);
		uint baseValue = num - (uint)Math.Floor(SkillUpdate.CalculateIP(((IStats)client.Controller.Character).Stats));
		((IStats)client.Controller.Character).Stats[(StatIds)53].BaseValue = baseValue;
		num3 = 0;
		List<GameTuple<CharacterStat, uint>> list2 = new List<GameTuple<CharacterStat, uint>>();
		for (; num3 < list.Count; num3++)
		{
			int num4 = list[num3];
			uint baseValue2 = ((IStats)client.Controller.Character).Stats[num4].BaseValue;
			list2.Add(new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)num4,
				Value2 = baseValue2
			});
		}
		SkillMessage body = new SkillMessage
		{
			Identity = ((N3Message)skillMessage).Identity,
			Unknown = 0,
			Skills = list2.ToArray()
		};
		((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)new IMSendAOtomationMessageBodyToClient
		{
			client = client,
			Body = (MessageBody)(object)body
		});
		((IDynel)client.Controller.Character).WriteStats();
	}
}
