using System.Collections.Generic;
using System.Globalization;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class StatMessageHandler : BaseMessageHandler<StatMessage, StatMessageHandler>
{
	public void SendChanged(ICharacter character)
	{
		Dictionary<int, uint> dictionary = new Dictionary<int, uint>();
		Dictionary<int, uint> dictionary2 = new Dictionary<int, uint>();
		((IStats)character).Stats.GetChangedStats(dictionary, dictionary2);
		CombatXpRuntimeService.RemoveWireManagedStatsFromBulk(dictionary);
		CombatXpRuntimeService.RemoveWireManagedStatsFromBulk(dictionary2);
		SendBulk(character, dictionary, dictionary2);
	}

	public void SendBulk(ICharacter character, Dictionary<int, uint> statsToClient, Dictionary<int, uint> statsToPlayfield)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		if (statsToClient.TryGetValue(61, out var _))
		{
			LogUtil.Debug((DebugInfoDetail)128, string.Format(arg1: statsToClient[61] = CashStatRules.Normalize(character), provider: CultureInfo.InvariantCulture, format: "Cash stat bulk send (client) char={0} cash={1} statCount={2}", arg0: ((IEntity)character).Identity, arg2: statsToClient.Count));
		}
		if (statsToClient.Count > 0)
		{
			foreach (KeyValuePair<int, uint> item in statsToClient)
			{
				CombatXpRuntimeService.LogXpWireOutbound("StatMessageHandler", "bulk-client", character, item.Key, item.Value, "StatMessage", "unknown=default");
			}
			CombatStartPacketDiagnostics.LogStatBulk("StatMessageHandler.SendBulk.client", character, statsToClient, announceToPlayfield: false);
			((AbstractMessageHandler<StatMessage>)(object)this).Send(character, FillerBulk(character, statsToClient), false);
		}
		if (statsToPlayfield.TryGetValue(61, out var _))
		{
			LogUtil.Debug((DebugInfoDetail)128, string.Format(arg1: statsToPlayfield[61] = CashStatRules.Normalize(character), provider: CultureInfo.InvariantCulture, format: "Cash stat bulk send (playfield) char={0} cash={1} statCount={2}", arg0: ((IEntity)character).Identity, arg2: statsToPlayfield.Count));
		}
		if (statsToPlayfield.Count <= 0)
		{
			return;
		}
		foreach (KeyValuePair<int, uint> item2 in statsToPlayfield)
		{
			CombatXpRuntimeService.LogXpWireOutbound("StatMessageHandler", "bulk-playfield", character, item2.Key, item2.Value, "StatMessage", "unknown=default playfield=true");
		}
		CombatStartPacketDiagnostics.LogStatBulk("StatMessageHandler.SendBulk.playfield", character, statsToPlayfield, announceToPlayfield: true);
		((AbstractMessageHandler<StatMessage>)(object)this).Send(character, FillerBulk(character, statsToPlayfield), true);
	}

	private MessageDataFiller<StatMessage> FillerBulk(ICharacter character, Dictionary<int, uint> statsToClient)
	{
		return delegate(StatMessage x)
		{
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			GameTuple<CharacterStat, uint>[] array = new GameTuple<CharacterStat, uint>[statsToClient.Count];
			int num = 0;
			foreach (KeyValuePair<int, uint> item in statsToClient)
			{
				array[num] = new GameTuple<CharacterStat, uint>
				{
					Value1 = (CharacterStat)item.Key,
					Value2 = item.Value
				};
				num++;
			}
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Stats = array;
		};
	}

	public void SendSingle(ICharacter character, int statId, uint statValue)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (statId == 61)
		{
			statValue = CashStatRules.Normalize(character);
			LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "Cash stat single send char={0} cash={1}", ((IEntity)character).Identity, statValue));
		}
		else
		{
			CombatXpRuntimeService.LogXpWireOutbound("StatMessageHandler", "single-client", character, statId, statValue, "StatMessage", "unknown=default");
		}
		((AbstractMessageHandler<StatMessage>)(object)this).Send(character, Filler(character, statId, statValue), false);
	}

	public void AnnounceSingle(ICharacter character, int statId, uint statValue)
	{
		if (statId == 61)
		{
			statValue = CashStatRules.Normalize(character);
		}
		((AbstractMessageHandler<StatMessage>)(object)this).Send(character, Filler(character, statId, statValue), true);
	}

	private MessageDataFiller<StatMessage> Filler(ICharacter character, int statId, uint statValue)
	{
		return delegate(StatMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Stats = new GameTuple<CharacterStat, uint>[1]
			{
				new GameTuple<CharacterStat, uint>
				{
					Value1 = (CharacterStat)statId,
					Value2 = statValue
				}
			};
		};
	}
}
