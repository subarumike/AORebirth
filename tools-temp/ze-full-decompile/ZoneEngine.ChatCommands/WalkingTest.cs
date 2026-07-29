using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.ChatCommands;

public class WalkingTest : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		return true;
	}

	public override void CommandHelp(ICharacter character)
	{
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Expected O, but got Unknown
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Expected O, but got Unknown
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a6: Invalid comparison between Unknown and I4
		//IL_02b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f6: Unknown result type (might be due to invalid IL or missing references)
		if (args[0].ToLower() == "walktest")
		{
			ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)character).Playfield).Identity, ((ITargetingEntity)character).SelectedTarget);
			if (@object != null)
			{
				Vector3 val = new Vector3();
				val.X = ((IDynel)@object).RawCoordinates.X;
				val.Y = ((IDynel)@object).RawCoordinates.Y;
				val.Z = ((IDynel)@object).RawCoordinates.Z;
				val.X += 20f;
				((IDynel)@object).Controller.MoveTo(val);
			}
		}
		if (args[0].ToLower() == "walkback")
		{
			ICharacter object2 = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)character).Playfield).Identity, ((ITargetingEntity)character).SelectedTarget);
			if (object2 != null)
			{
				Vector3 val2 = new Vector3();
				val2.X = ((IDynel)object2).RawCoordinates.X;
				val2.Y = ((IDynel)object2).RawCoordinates.Y;
				val2.Z = ((IDynel)object2).RawCoordinates.Z;
				val2.X -= 20f;
				((IDynel)object2).Controller.MoveTo(val2);
			}
		}
		if (args[0].ToLower() == "followtest")
		{
			ICharacter object3 = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)character).Playfield).Identity, ((ITargetingEntity)character).SelectedTarget);
			if (object3 != null)
			{
				((IDynel)object3).Controller.Follow(((IEntity)character).Identity);
			}
		}
		Identity selectedTarget;
		if (args[0].ToLower() == "showcoords")
		{
			ICharacter object4 = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)character).Playfield).Identity, ((ITargetingEntity)character).SelectedTarget);
			if (object4 != null)
			{
				IPlayfield playfield = ((IInstancedEntity)character).Playfield;
				ChatTextMessageHandler @default = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
				selectedTarget = ((ITargetingEntity)character).SelectedTarget;
				playfield.Publish((object)@default.CreateIM(character, "Coordinates of " + ((Identity)(ref selectedTarget)).ToString(true) + ": " + ((object)((IDynel)object4).Coordinates()).ToString(), 0, 0));
				IPlayfield playfield2 = ((IInstancedEntity)character).Playfield;
				ChatTextMessageHandler default2 = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
				selectedTarget = ((ITargetingEntity)character).SelectedTarget;
				playfield2.Publish((object)default2.CreateIM(character, "Heading of " + ((Identity)(ref selectedTarget)).ToString(true) + ": " + ((object)((IDynel)object4).Heading).ToString(), 0, 0));
			}
		}
		if (args[0].ToLower() == "addwp")
		{
			Vector3 coordinate = ((IDynel)character).Coordinates().coordinate;
			bool flag = (int)character.MoveMode == 3;
			ICharacter object5 = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)character).Playfield).Identity, ((ITargetingEntity)character).SelectedTarget);
			if (object5 != null)
			{
				object5.AddWaypoint(coordinate, flag);
				IPlayfield playfield3 = ((IInstancedEntity)character).Playfield;
				ChatTextMessageHandler default3 = BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default;
				selectedTarget = ((ITargetingEntity)character).SelectedTarget;
				playfield3.Publish((object)default3.CreateIM(character, "Waypoint added: " + ((Identity)(ref selectedTarget)).ToString(true) + ": " + ((object)((IDynel)character).Coordinates()).ToString(), 0, 0));
			}
		}
	}

	public override int GMLevelNeeded()
	{
		return 0;
	}

	public override List<string> ListCommands()
	{
		return new List<string>(new string[5] { "walktest", "followtest", "walkback", "showcoords", "addwp" });
	}
}
