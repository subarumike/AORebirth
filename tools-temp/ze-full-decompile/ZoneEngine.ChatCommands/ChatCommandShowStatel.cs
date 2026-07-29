using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Playfields;
using AORebirth.Core.Statels;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Packets;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.ChatCommands;

public class ChatCommandShowStatel : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		return true;
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Usage: /command showstatel", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0314: Unknown result type (might be due to invalid IL or missing references)
		//IL_0319: Unknown result type (might be due to invalid IL or missing references)
		//IL_031d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0324: Expected I4, but got Unknown
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0342: Unknown result type (might be due to invalid IL or missing references)
		//IL_0548: Unknown result type (might be due to invalid IL or missing references)
		//IL_054d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0563: Unknown result type (might be due to invalid IL or missing references)
		//IL_0568: Unknown result type (might be due to invalid IL or missing references)
		//IL_043c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0441: Unknown result type (might be due to invalid IL or missing references)
		//IL_0445: Unknown result type (might be due to invalid IL or missing references)
		//IL_044a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0465: Unknown result type (might be due to invalid IL or missing references)
		//IL_046a: Unknown result type (might be due to invalid IL or missing references)
		//IL_046e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0475: Expected I4, but got Unknown
		//IL_048d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0492: Unknown result type (might be due to invalid IL or missing references)
		List<MessageBody> list = new List<MessageBody>();
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		string text = "Looking up for statel in playfield " + ((Identity)(ref identity)).Instance;
		list.Add((MessageBody)(object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Create(character, text, 0, 0));
		StatelData val = null;
		StaticDynel val2 = null;
		Vendor val3 = null;
		Coordinate val4 = ((IDynel)character).Coordinates();
		Dictionary<int, PlayfieldData> pFData = PlayfieldLoader.PFData;
		identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		if (!pFData.ContainsKey(((Identity)(ref identity)).Instance))
		{
			identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			text = "Could not find data for playfield " + ((Identity)(ref identity)).Instance;
			list.Add((MessageBody)(object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Create(character, text, 0, 0));
		}
		else
		{
			if (((object)(Identity)(ref target)).Equals((object)Identity.None))
			{
				Dictionary<int, PlayfieldData> pFData2 = PlayfieldLoader.PFData;
				identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
				PlayfieldData val5 = pFData2[((Identity)(ref identity)).Instance];
				foreach (StatelData statel in val5.Statels)
				{
					if (val == null)
					{
						val = statel;
					}
					else if (Coordinate.Distance2D(val4, statel.Coord()) < Coordinate.Distance2D(val4, val.Coord()))
					{
						val = statel;
					}
				}
				foreach (StaticDynel item in Pool.Instance.GetAll<StaticDynel>(((IEntity)((IInstancedEntity)character).Playfield).Identity))
				{
					if (val2 == null)
					{
						val2 = item;
					}
					else if (Coordinate.Distance2D(val4, item.Coordinate) < Coordinate.Distance2D(val4, val2.Coordinate))
					{
						val2 = item;
					}
				}
			}
			else
			{
				Dictionary<int, PlayfieldData> pFData3 = PlayfieldLoader.PFData;
				identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
				val = pFData3[((Identity)(ref identity)).Instance].Statels.FirstOrDefault((StatelData x) => x.Identity == target);
				val2 = Pool.Instance.GetAll<StaticDynel>(((IEntity)((IInstancedEntity)character).Playfield).Identity).FirstOrDefault((StaticDynel x) => ((PooledObject)x).Identity == target);
				val3 = Pool.Instance.GetAll<Vendor>(((IEntity)((IInstancedEntity)character).Playfield).Identity).FirstOrDefault((Vendor x) => ((PooledObject)x).Identity == target);
			}
			IdentityType type;
			if (val == null && val2 == null && val3 == null)
			{
				list.Add((MessageBody)(object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Create(character, "No statel/static dynel on this playfield... Very odd, where exactly are you???", 0, 0));
			}
			else if (val3 != null)
			{
				ChatTextMessageHandler @default = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
				string[] array = new string[5];
				identity = ((PooledObject)val3).Identity;
				type = ((Identity)(ref identity)).Type;
				array[0] = ((object)(IdentityType)(ref type)).ToString();
				array[1] = " ";
				identity = ((PooledObject)val3).Identity;
				array[2] = ((int)((Identity)(ref identity)).Type).ToString("X8");
				array[3] = ":";
				identity = ((PooledObject)val3).Identity;
				array[4] = ((Identity)(ref identity)).Instance.ToString("X8");
				list.Add((MessageBody)(object)@default.Create(character, string.Concat(array), 0, 0));
				list.Add((MessageBody)(object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Create(character, "Item Template Id: " + val3.Template.ID, 0, 0));
				foreach (Event @event in val3.Events)
				{
					list.Add((MessageBody)(object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Create(character, ((object)@event).ToString(), 0, 0));
				}
			}
			else if ((val != null && val2 == null) || (val != null && Coordinate.Distance2D(val4, val.Coord()) < Coordinate.Distance2D(val4, val2.Coordinate)))
			{
				ChatTextMessageHandler default2 = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
				string[] array2 = new string[5];
				identity = val.Identity;
				type = ((Identity)(ref identity)).Type;
				array2[0] = ((object)(IdentityType)(ref type)).ToString();
				array2[1] = " ";
				identity = val.Identity;
				array2[2] = ((int)((Identity)(ref identity)).Type).ToString("X8");
				array2[3] = ":";
				identity = val.Identity;
				array2[4] = ((Identity)(ref identity)).Instance.ToString("X8");
				list.Add((MessageBody)(object)default2.Create(character, string.Concat(array2), 0, 0));
				list.Add((MessageBody)(object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Create(character, "Item Template Id: " + val.TemplateId, 0, 0));
				foreach (Event event2 in val.Events)
				{
					list.Add((MessageBody)(object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Create(character, ((object)event2).ToString(), 0, 0));
				}
			}
			else
			{
				ChatTextMessageHandler default3 = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
				identity = ((PooledObject)val2).Identity;
				string text2 = ((object)(Identity)(ref identity)).ToString();
				identity = ((PooledObject)val2).Identity;
				list.Add((MessageBody)(object)default3.Create(character, text2 + " " + ((Identity)(ref identity)).ToString(true), 0, 0));
				list.Add((MessageBody)(object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Create(character, "Item template Id: " + val2.Stats[702], 0, 0));
				foreach (Event event3 in val2.Events)
				{
					list.Add((MessageBody)(object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Create(character, ((object)event3).ToString(), 0, 0));
				}
			}
		}
		((IInstancedEntity)character).Playfield.Publish((object)Bulk.CreateIM(((IDynel)character).Controller.Client, list.ToArray()));
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		List<string> list = new List<string>();
		list.Add("showstatel");
		return list;
	}
}
