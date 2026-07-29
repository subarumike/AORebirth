using System;
using System.Collections.Generic;
using System.Data;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Statels;
using AORebirth.Core.Vector;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.ChatCommands;

public class SaveProxy : AOChatCommand
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
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Expected O, but got Unknown
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Expected I4, but got Unknown
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		if (((IStats)character).Stats[(StatIds)193].Value == 0 || ((IStats)character).Stats[(StatIds)192].Value == 0)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Create(character, "Please enter a proxyfied playfield first.", 0, 0);
		}
		Coordinate val = ((IDynel)character).Coordinates();
		Dictionary<int, PlayfieldData> pFData = PlayfieldLoader.PFData;
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		PlayfieldData val2 = pFData[((Identity)(ref identity)).Instance];
		StatelData val3 = null;
		foreach (StatelData statel in val2.Statels)
		{
			if (val3 == null)
			{
				val3 = statel;
			}
			else if (Coordinate.Distance2D(val, statel.Coord()) < Coordinate.Distance2D(val, val3.Coord()))
			{
				val3 = statel;
			}
		}
		if (val3 == null)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Create(character, "No statel on this playfield... Very odd, where exactly are you???", 0, 0);
			return;
		}
		DBTeleport val4 = new DBTeleport();
		val4.playfield = ((IStats)character).Stats[(StatIds)192].Value;
		val4.statelType = 51016;
		val4.statelInstance = ((IStats)character).Stats[(StatIds)193].BaseValue;
		val4.destinationPlayfield = val3.PlayfieldId;
		identity = val3.Identity;
		val4.destinationType = (int)((Identity)(ref identity)).Type;
		identity = val3.Identity;
		val4.destinationInstance = BitConverter.ToUInt32(BitConverter.GetBytes(((Identity)(ref identity)).Instance), 0);
		IEnumerable<DBTeleport> where = ((Dao<DBTeleport, TeleportDao>)(object)Dao<DBTeleport, TeleportDao>.Instance).GetWhere((object)new { val4.playfield, val4.statelType, val4.statelInstance }, (IDbConnection)null, (IDbTransaction)null);
		foreach (DBTeleport item in where)
		{
			((Dao<DBTeleport, TeleportDao>)(object)Dao<DBTeleport, TeleportDao>.Instance).Delete(item.Id, (IDbConnection)null, (IDbTransaction)null);
		}
		((Dao<DBTeleport, TeleportDao>)(object)Dao<DBTeleport, TeleportDao>.Instance).Add(val4, (IDbConnection)null, (IDbTransaction)null, true);
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Proxy saved", 0, 0));
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "saveproxy" };
	}
}
