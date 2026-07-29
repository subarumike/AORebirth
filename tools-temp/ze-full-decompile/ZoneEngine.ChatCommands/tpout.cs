using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Statels;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.ChatCommands;

public class tpout : AOChatCommand
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
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Expected O, but got Unknown
		//IL_00be: Expected O, but got Unknown
		uint baseValue = ((IStats)character).Stats[(StatIds)192].BaseValue;
		uint baseValue2 = ((IStats)character).Stats[(StatIds)193].BaseValue;
		int num = (int)baseValue;
		int num2 = BitConverter.ToInt32(BitConverter.GetBytes(baseValue2), 0);
		StatelData door = PlayfieldLoader.PFData[num].GetDoor(num2);
		if (door == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "No statel found");
			return;
		}
		IPlayfield playfield = ((IInstancedEntity)character).Playfield;
		Dynel val = (Dynel)character;
		Coordinate val2 = new Coordinate(door.X, door.Y, door.Z);
		Quaternion heading = ((IDynel)character).Heading;
		Identity val3 = default(Identity);
		((Identity)(ref val3)).Type = (IdentityType)51101;
		((Identity)(ref val3)).Instance = num;
		playfield.Teleport(val, val2, (IQuaternion)(object)heading, val3);
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "tpo" };
	}
}
