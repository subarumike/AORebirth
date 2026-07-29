using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.InternalMessages;

namespace ZoneEngine.Core.Packets;

public static class Stat
{
	public static void Send(IZoneClient client, int stat, int value, bool announce)
	{
		Send(client, stat, (uint)value, announce);
	}

	public static void Send(Dynel dynel, int stat, uint value, bool announce)
	{
		Send(dynel, stat, (int)value, announce);
	}

	public static void Send(Dynel dynel, int stat, int value, bool announce)
	{
		ICharacter val = (ICharacter)(object)((dynel is ICharacter) ? dynel : null);
		if (val != null)
		{
			Send(val, stat, value, announce);
		}
	}

	public static void Send(IZoneClient client, int stat, uint value, bool announce)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected O, but got Unknown
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		StatMessage val = new StatMessage();
		((N3Message)val).Identity = ((IEntity)client.Controller.Character).Identity;
		val.Stats = new GameTuple<CharacterStat, uint>[1]
		{
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)stat,
				Value2 = value
			}
		};
		StatMessage val2 = val;
		Message message = new Message
		{
			Body = (MessageBody)(object)val2
		};
		if (!((IInstancedEntity)client.Controller.Character).DoNotDoTimers)
		{
			((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)new IMSendAOtomationMessageToClient
			{
				client = client,
				message = message
			});
		}
		if (announce)
		{
			((IInstancedEntity)client.Controller.Character).Playfield.AnnounceOthers((MessageBody)(object)val2, ((IEntity)client.Controller.Character).Identity);
		}
	}

	public static void Send(ICharacter character, int stat, uint value, bool announce)
	{
		if (((IDynel)character).Controller.Client != null)
		{
			Send(((IDynel)character).Controller.Client, stat, value, announce);
		}
	}

	public static void Send(ICharacter character, int stat, int value, bool announce)
	{
		if (((IDynel)character).Controller.Client != null)
		{
			Send(((IDynel)character).Controller.Client, stat, value, announce);
		}
	}

	public static void SendBulk(ICharacter ch, Dictionary<int, uint> statsToUpdate)
	{
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Expected O, but got Unknown
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		if (statsToUpdate.Count == 0)
		{
			return;
		}
		List<int> list = new List<int>();
		foreach (KeyValuePair<int, uint> item in statsToUpdate)
		{
			if (((IStats)ch).Stats[item.Key].AnnounceToPlayfield)
			{
				list.Add(item.Key);
			}
		}
		List<GameTuple<CharacterStat, uint>> list2 = new List<GameTuple<CharacterStat, uint>>();
		foreach (KeyValuePair<int, uint> item2 in statsToUpdate)
		{
			if (list.Contains(item2.Key))
			{
				list2.Add(new GameTuple<CharacterStat, uint>
				{
					Value1 = (CharacterStat)item2.Key,
					Value2 = item2.Value
				});
			}
		}
		if (list.Any())
		{
			StatMessage val = new StatMessage
			{
				Identity = ((IEntity)ch).Identity,
				Stats = list2.ToArray()
			};
			((IInstancedEntity)ch).Playfield.AnnounceOthers((MessageBody)(object)val, ((IEntity)ch).Identity);
		}
	}

	public static void SendBulk(IZoneClient client, Dictionary<int, uint> statsToUpdate)
	{
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Expected O, but got Unknown
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		if (statsToUpdate.Count == 0)
		{
			return;
		}
		List<int> list = new List<int>();
		foreach (KeyValuePair<int, uint> item2 in statsToUpdate)
		{
			if (((IStats)client.Controller.Character).Stats[item2.Key].AnnounceToPlayfield)
			{
				list.Add(item2.Key);
			}
		}
		List<GameTuple<CharacterStat, uint>> list2 = new List<GameTuple<CharacterStat, uint>>();
		List<GameTuple<CharacterStat, uint>> list3 = new List<GameTuple<CharacterStat, uint>>();
		foreach (KeyValuePair<int, uint> item3 in statsToUpdate)
		{
			GameTuple<CharacterStat, uint> item = new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)item3.Key,
				Value2 = item3.Value
			};
			list3.Add(item);
			if (list.Contains(item3.Key))
			{
				list2.Add(item);
			}
		}
		StatMessage val = new StatMessage
		{
			Identity = ((IEntity)client.Controller.Character).Identity,
			Stats = list3.ToArray()
		};
		client.SendCompressed((MessageBody)(object)val);
		if (list.Count > 0)
		{
			val.Stats = list2.ToArray();
			((IInstancedEntity)client.Controller.Character).Playfield.AnnounceOthers((MessageBody)(object)val, ((IEntity)client.Controller.Character).Identity);
		}
	}

	public static void SendDirect(IZoneClient client, int stat, uint value, bool announce)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected O, but got Unknown
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		StatMessage val = new StatMessage();
		((N3Message)val).Identity = ((IEntity)client.Controller.Character).Identity;
		val.Stats = new GameTuple<CharacterStat, uint>[1]
		{
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)stat,
				Value2 = value
			}
		};
		StatMessage val2 = val;
		client.SendCompressed((MessageBody)(object)val2);
	}

	public static uint Set(IZoneClient client, int stat, uint value, bool announce)
	{
		uint value2 = (uint)((IStats)client.Controller.Character).Stats[stat].Value;
		Send(client, stat, value, announce);
		return value2;
	}
}
