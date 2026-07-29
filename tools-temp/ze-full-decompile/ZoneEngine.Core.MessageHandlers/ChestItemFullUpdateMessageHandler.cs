using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Enums;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class ChestItemFullUpdateMessageHandler : BaseMessageHandler<ChestItemFullUpdateMessage, ChestItemFullUpdateMessageHandler>
{
	private const int MissingItemStatValue = 1234567890;

	public void Send(ICharacter character, Item item, Identity sourceInventorySlot, Identity containerIdentity)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<ChestItemFullUpdateMessage>)(object)this).Send(character, FillData(character, item, sourceInventorySlot, containerIdentity), false);
	}

	private MessageDataFiller<ChestItemFullUpdateMessage> FillData(ICharacter character, Item item, Identity sourceInventorySlot, Identity containerIdentity)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		return delegate(ChestItemFullUpdateMessage x)
		{
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			ICharacter obj = character;
			Character val = (Character)(object)((obj is Character) ? obj : null);
			((N3Message)x).Identity = containerIdentity;
			((N3Message)x).Unknown = 0;
			x.Unknown1 = 11;
			x.Owner = ((IEntity)character).Identity;
			int playfieldId;
			Identity stateMachine;
			if (val == null || ((Dynel)val).Playfield == null)
			{
				playfieldId = 0;
			}
			else
			{
				stateMachine = ((IEntity)((Dynel)val).Playfield).Identity;
				playfieldId = ((Identity)(ref stateMachine)).Instance;
			}
			x.PlayfieldId = playfieldId;
			stateMachine = default(Identity);
			((Identity)(ref stateMachine)).Type = (IdentityType)1000015;
			((Identity)(ref stateMachine)).Instance = 0;
			x.StateMachine = stateMachine;
			x.Unknown5 = (short)(0x100 | (((Identity)(ref sourceInventorySlot)).Instance & 0xFF));
			x.Stats = new GameTuple<CharacterStat, uint>[6]
			{
				StatTuple((CharacterStat)0, ItemStat(item, (StatIds)0, 0)),
				StatTuple((CharacterStat)23, (uint)item.HighID),
				StatTuple((CharacterStat)701, (uint)item.Quality),
				StatTuple((CharacterStat)702, (uint)item.LowID),
				StatTuple((CharacterStat)703, (uint)item.HighID),
				StatTuple((CharacterStat)412, (uint)Math.Max(1, item.MultipleCount))
			};
			x.Unknown6 = 0;
			x.Unknown7 = 2;
			x.Unknown8 = 50;
			x.UnknownArray = new int[0];
			x.Unknown9 = 3;
		};
	}

	private GameTuple<CharacterStat, uint> StatTuple(CharacterStat stat, uint value)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return new GameTuple<CharacterStat, uint>
		{
			Value1 = stat,
			Value2 = value
		};
	}

	private uint ItemStat(Item item, StatIds stat, int fallback)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected I4, but got Unknown
		int num = item.GetAttribute((int)stat);
		if (num == 1234567890)
		{
			num = fallback;
		}
		return (uint)num;
	}
}
