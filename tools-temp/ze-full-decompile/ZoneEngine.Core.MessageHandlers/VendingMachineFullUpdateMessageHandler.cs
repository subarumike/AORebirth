using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class VendingMachineFullUpdateMessageHandler : BaseMessageHandler<VendingMachineFullUpdateMessage, VendingMachineFullUpdateMessageHandler>
{
	public void Send(ICharacter character, Vendor vendor)
	{
		((AbstractMessageHandler<VendingMachineFullUpdateMessage>)(object)this).Send(character, Filler(vendor), false);
	}

	private MessageDataFiller<VendingMachineFullUpdateMessage> Filler(Vendor vendor)
	{
		return delegate(VendingMachineFullUpdateMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ac: Expected O, but got Unknown
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0114: Expected O, but got Unknown
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0137: Unknown result type (might be due to invalid IL or missing references)
			//IL_013c: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((PooledObject)vendor).Identity;
			((N3Message)x).Unknown = 0;
			if (((Identity)(ref vendor.NpcIdentity)).Instance != 0)
			{
				x.Coordinates = null;
				x.Heading = null;
				x.NpcIdentity = vendor.NpcIdentity;
			}
			else
			{
				x.Coordinates = new Vector3
				{
					X = ((Dynel)vendor).Coordinates().x,
					Y = ((Dynel)vendor).Coordinates().y,
					Z = ((Dynel)vendor).Coordinates().z
				};
				x.Heading = new Quaternion
				{
					X = ((Dynel)vendor).Heading.xf,
					Y = ((Dynel)vendor).Heading.yf,
					Z = ((Dynel)vendor).Heading.zf,
					W = ((Dynel)vendor).Heading.wf
				};
				x.NpcIdentity = Identity.None;
			}
			x.TypeIdentifier = 11;
			Identity identity = ((IEntity)((Dynel)vendor).Playfield).Identity;
			x.PlayfieldId = ((Identity)(ref identity)).Instance;
			x.Unknown4 = 1000015;
			x.Unknown5 = 0;
			x.Unknown6 = 111;
			List<GameTuple<CharacterStat, uint>> list = new List<GameTuple<CharacterStat, uint>>();
			Dictionary<int, uint> statValues = ((Dynel)vendor).Stats.GetStatValues();
			SortedDictionary<int, uint> sortedDictionary = new SortedDictionary<int, uint>();
			foreach (KeyValuePair<int, uint> item in statValues)
			{
				sortedDictionary.Add(item.Key, item.Value);
			}
			foreach (KeyValuePair<int, uint> item2 in sortedDictionary)
			{
				list.Add(new GameTuple<CharacterStat, uint>
				{
					Value1 = (CharacterStat)item2.Key,
					Value2 = item2.Value
				});
			}
			x.Stats = list.ToArray();
			x.Unknown7 = ((Dynel)vendor).Name + "\0";
			x.Unknown8 = 2;
			x.Unknown9 = 50;
			x.Unknown10 = (Identity[])(object)new Identity[0];
			x.Unknown11 = 3;
		};
	}
}
