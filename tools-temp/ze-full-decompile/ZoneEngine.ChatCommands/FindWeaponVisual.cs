using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Actions;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Items;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.ChatCommands;

public class FindWeaponVisual : AOChatCommand
{
	private class WeaponVisualCandidate
	{
		public int Id { get; set; }

		public int Quality { get; set; }

		public int MeshRight { get; set; }

		public int MeshLeft { get; set; }

		public bool HasToWield { get; set; }

		public bool HasMeshFunction { get; set; }
	}

	public override bool CheckCommandArguments(string[] args)
	{
		if (args.Length == 1)
		{
			return true;
		}
		int result;
		if (args.Length == 2)
		{
			return int.TryParse(args[1], out result);
		}
		return false;
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Usage: /command findweaponvisual [count]", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		int result = 12;
		if (args.Length > 1)
		{
			int.TryParse(args[1], out result);
		}
		if (result < 1)
		{
			result = 1;
		}
		if (result > 50)
		{
			result = 50;
		}
		List<WeaponVisualCandidate> list = (from x in ItemLoader.ItemList.Values
			where x != null
			select new WeaponVisualCandidate
			{
				Id = x.ID,
				Quality = x.Quality,
				MeshRight = NormalizeVisualValue(x.getItemAttribute(1006)),
				MeshLeft = NormalizeVisualValue(x.getItemAttribute(1007)),
				HasToWield = x.Actions.Any((AOAction a) => (int)a.ActionType == 8),
				HasMeshFunction = x.Events.Where((Event e) => (int)e.EventType == 14 || (int)e.EventType == 2).SelectMany((Event e) => e.Functions).Any((Function f) => f.FunctionType == 53004 || f.FunctionType == 53037 || f.FunctionType == 53035 || f.FunctionType == 53054)
			} into x
			where x.MeshRight > 0 || x.MeshLeft > 0 || x.HasMeshFunction
			orderby x.HasToWield descending, x.MeshRight descending, x.Id
			select x).Take(result).ToList();
		int num = ItemLoader.ItemList.Values.Count((ItemTemplate x) => x != null && (NormalizeVisualValue(x.getItemAttribute(1006)) > 0 || NormalizeVisualValue(x.getItemAttribute(1007)) > 0));
		int num2 = ItemLoader.ItemList.Values.Count((ItemTemplate x) => x != null && x.Events.Where((Event e) => (int)e.EventType == 14 || (int)e.EventType == 2).SelectMany((Event e) => e.Functions).Any((Function f) => f.FunctionType == 53004 || f.FunctionType == 53037 || f.FunctionType == 53035 || f.FunctionType == 53054));
		int num3 = ItemLoader.ItemList.Values.Count((ItemTemplate x) => x != null && x.Actions.Any((AOAction a) => (int)a.ActionType == 8));
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, $"Weapon visual candidates: showing {list.Count}. withMeshStat={num}, withMeshFunction={num2}, withToWield={num3}. Use /command giveitem <id> <ql>", 0, 0));
		foreach (WeaponVisualCandidate item in list)
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, $"id={item.Id} ql={item.Quality} meshR={item.MeshRight} meshL={item.MeshLeft} toWield={(item.HasToWield ? 1 : 0)} meshFunc={(item.HasMeshFunction ? 1 : 0)} name={GetItemName(item.Id)}", 0, 0));
		}
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "findweaponvisual" };
	}

	private static int NormalizeVisualValue(int value)
	{
		if (value <= 0 || value == 1234567890)
		{
			return 0;
		}
		return value;
	}

	private static string GetItemName(int itemId)
	{
		DBItemName val = ((Dao<DBItemName, ItemNamesDao>)(object)Dao<DBItemName, ItemNamesDao>.Instance).Get(itemId);
		if (val == null || string.IsNullOrEmpty(val.Name))
		{
			return "(no name)";
		}
		return val.Name;
	}
}
