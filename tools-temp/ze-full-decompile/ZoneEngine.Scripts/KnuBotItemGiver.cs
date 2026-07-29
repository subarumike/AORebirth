using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.KnuBot;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Scripts;

public class KnuBotItemGiver : BaseKnuBot
{
	public class ItemSet
	{
		private string setName = "";

		public List<int> ItemIds = new List<int>();

		public ItemSet(params int[] ids)
		{
			for (int i = 0; i < ids.Length; i++)
			{
				int num = ids[i];
				if (ItemLoader.ItemList.ContainsKey(num))
				{
					ItemIds.Add(num);
					continue;
				}
				Exception ex = new Exception("Script initilization error, item with Id " + num + " not found.");
				LogUtil.ErrorException(ex);
				throw ex;
			}
		}

		public int GetMinQl()
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Expected O, but got Unknown
			int num = 0;
			foreach (int itemId in ItemIds)
			{
				Item val = new Item(1, itemId, itemId);
				num = ((num < val.Quality) ? val.Quality : num);
			}
			return num;
		}

		public int GetMaxQl()
		{
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Expected O, but got Unknown
			int num = 400;
			foreach (int itemId in ItemIds)
			{
				Item val = new Item(400, itemId, ItemLoader.ItemList[itemId].Relations.Last());
				num = ((num > val.Quality) ? val.Quality : num);
			}
			return num;
		}

		public string GetName()
		{
			int num = ItemIds[0];
			if (setName == "")
			{
				setName = ((Dao<DBItemName, ItemNamesDao>)(object)Dao<DBItemName, ItemNamesDao>.Instance).Get(num).Name;
			}
			return setName;
		}

		public string GetIconAndName()
		{
			string name = GetName();
			return $"<img src=rdb://{ItemLoader.ItemList[ItemIds[0]].getItemAttribute(79)}> {name}";
		}

		public int[] GetQLs()
		{
			int i = GetMinQl();
			int maxQl = GetMaxQl();
			List<int> list = new List<int>();
			for (; i < maxQl; i += 25)
			{
				list.Add(i);
				if (i == 1)
				{
					i--;
				}
			}
			list.Add(maxQl);
			return list.ToArray();
		}
	}

	private readonly List<ItemSet> RKArmorSets = new List<ItemSet>();

	private readonly List<ItemSet> SLArmorSets = new List<ItemSet>();

	private bool isGM = false;

	private ItemSet selectedSet = null;

	private int selectedQL = 0;

	public KnuBotItemGiver(Identity identity)
		: base(identity)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		InitializeItemSets();
		KnuBotDialogTree knuBotDialogTree = new KnuBotDialogTree("0", Condition0, new KnuBotActionStruct[4]
		{
			CAS(DialogGM0, "self"),
			CAS(TransferToRKArmorSet, "RKArmorSet"),
			CAS(TransferToSLArmorSet, "SLArmorSet"),
			CAS(GoodBye, "self")
		});
		SetRootNode(knuBotDialogTree);
		KnuBotDialogTree knuBotDialogTree2 = knuBotDialogTree.AddNode(new KnuBotDialogTree("RKArmorSet", Condition01, new KnuBotActionStruct[3]
		{
			CAS(DialogShowRKArmorSets, "self"),
			CAS(ChooseQlFromSet, "QLChoiceRKArmorSet"),
			CAS(BackToRoot, "root")
		}));
		knuBotDialogTree2.AddNode(new KnuBotDialogTree("QLChoiceRKArmorSet", QLCondition, new KnuBotActionStruct[3]
		{
			CAS(ShowQLs, "self"),
			CAS(GiveItemSet, "root"),
			CAS(BackToRoot, "parent")
		}));
		knuBotDialogTree2 = knuBotDialogTree.AddNode(new KnuBotDialogTree("SLArmorSet", ConditionSL, new KnuBotActionStruct[3]
		{
			CAS(DialogShowSLArmorSets, "self"),
			CAS(ChooseQlFromSet, "QLChoiceSLArmorSet"),
			CAS(BackToRoot, "root")
		}));
		knuBotDialogTree2.AddNode(new KnuBotDialogTree("QLChoiceSLArmorSet", QLCondition, new KnuBotActionStruct[3]
		{
			CAS(ShowQLs, "self"),
			CAS(GiveItemSet, "root"),
			CAS(BackToRoot, "parent")
		}));
	}

	private void InitializeItemSets()
	{
		RKArmorSets.Add(new ItemSet(162431, 162429, 162429, 162435, 162427, 162426, 162433));
		RKArmorSets.Add(new ItemSet(208286, 208284, 208284, 208288, 208290, 208292, 208294));
		RKArmorSets.Add(new ItemSet(208255, 208253, 208253, 208257, 208259, 208261, 208263));
		RKArmorSets.Add(new ItemSet(164997, 165005, 165005, 164999, 165001, 165007, 165003));
		RKArmorSets.Add(new ItemSet(245964, 245972, 245972, 245966, 245968, 245974, 245970));
		RKArmorSets.Add(new ItemSet(245123, 245119, 245119, 245125, 245125, 245122, 245118, 245124, 245120));
		RKArmorSets.Add(new ItemSet(164816, 164800, 164800, 164810, 164808, 164819, 164812));
		RKArmorSets.Add(new ItemSet(163945, 163943, 163943, 163941, 163947, 163949, 163951));
		RKArmorSets.Add(new ItemSet(205951, 205954, 205954, 205955, 205953, 205950, 205952));
		RKArmorSets.Add(new ItemSet(268850, 268854, 268854, 268858, 270338, 268852, 268848, 268856));
		RKArmorSets.Add(new ItemSet(245177, 245175, 245175, 245185, 245185, 245183, 245179, 245187, 245181));
		SLArmorSets.Add(new ItemSet(245891, 245880, 245880, 260681, 260680, 260680, 245889, 245884, 245866));
		SLArmorSets.Add(new ItemSet(225542, 225543, 225543, 225546, 225546, 225545, 225544, 257115));
		SLArmorSets.Add(new ItemSet(215462, 215470, 215470, 215472, 215476, 215466, 215468, 215474));
	}

	private KnuBotAction Condition0(KnuBotOptionId id)
	{
		isGM = ((IStats)GetCharacter()).Stats[(StatIds)215].Value > 0;
		return id switch
		{
			KnuBotOptionId.DialogStart => DialogGM0, 
			KnuBotOptionId.Option1 => TransferToRKArmorSet, 
			KnuBotOptionId.Option2 => TransferToSLArmorSet, 
			KnuBotOptionId.Option3 => GoodBye, 
			_ => null, 
		};
	}

	private void TransferToSLArmorSet()
	{
		WriteLine("Shadowlands Armor sets:");
	}

	private void DialogGM0()
	{
		string text = "Hi " + (isGM ? "<font color=#FF0000>GM</font> " : "") + ((INamedEntity)GetCharacter()).Name;
		WriteLine(text);
		WriteLine();
		WriteLine("How may i help you?");
		SendAnswerList("Rubi-Ka Armor sets", "Shadowland Armor sets", "Good bye");
	}

	private void TransferToRKArmorSet()
	{
		WriteLine("Rubi-Ka Armor sets:");
	}

	private void GoodBye()
	{
		WriteLine("Good bye");
		CloseChatWindow();
	}

	private KnuBotAction Condition01(KnuBotOptionId id)
	{
		if (id == KnuBotOptionId.DialogStart)
		{
			return DialogShowRKArmorSets;
		}
		if (id >= KnuBotOptionId.Option1 && (int)id < RKArmorSets.Count)
		{
			selectedSet = RKArmorSets[(int)id];
			return ChooseQlFromSet;
		}
		if (id == (KnuBotOptionId)RKArmorSets.Count)
		{
			return BackToRoot;
		}
		return BackToRoot;
	}

	private void DialogShowRKArmorSets()
	{
		string[] array = RKArmorSets.Select((ItemSet x) => x.GetIconAndName()).ToArray();
		string[] array2 = array;
		foreach (string text in array2)
		{
			WriteLine(text);
		}
		List<string> list = new List<string>();
		list.AddRange(RKArmorSets.Select((ItemSet x) => x.GetName()).ToArray());
		list.Add("Back");
		SendAnswerList(list.ToArray());
	}

	private void BackToRoot()
	{
		WriteLine("Too bad...");
		WriteLine();
	}

	private void ChooseQlFromSet()
	{
	}

	private KnuBotAction QLCondition(KnuBotOptionId id)
	{
		if (id == KnuBotOptionId.DialogStart)
		{
			return ShowQLs;
		}
		int[] qLs = selectedSet.GetQLs();
		if (id >= KnuBotOptionId.Option1 && (int)id < qLs.Length)
		{
			selectedQL = qLs[(int)id];
			return GiveItemSet;
		}
		if (id == (KnuBotOptionId)qLs.Length)
		{
			return BackToRoot;
		}
		return null;
	}

	private void ShowQLs()
	{
		List<string> list = new List<string>();
		string[] collection = (from x in selectedSet.GetQLs()
			select x.ToString()).ToArray();
		list.AddRange(collection);
		list.Add("Back");
		SendAnswerList(list.ToArray());
	}

	private void GiveItem(int qualityLevel, int id)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Invalid comparison between Unknown and I4
		int lowId = ItemLoader.ItemList[id].GetLowId(qualityLevel);
		int highId = ItemLoader.ItemList[id].GetHighId(qualityLevel);
		Item val = new Item(qualityLevel, lowId, highId);
		if ((int)((IItemContainer)GetCharacter()).BaseInventory.TryAdd((IItem)(object)val) == 0)
		{
			BaseMessageHandler<AddTemplateMessage, AddTemplateMessageHandler>.Default.Send(GetCharacter(), val);
		}
	}

	private void GiveItemSet()
	{
		foreach (int itemId in selectedSet.ItemIds)
		{
			GiveItem(selectedQL, itemId);
		}
	}

	private void DialogShowSLArmorSets()
	{
		string[] array = SLArmorSets.Select((ItemSet x) => x.GetIconAndName()).ToArray();
		string[] array2 = array;
		foreach (string text in array2)
		{
			WriteLine(text);
		}
		List<string> list = new List<string>();
		list.AddRange(SLArmorSets.Select((ItemSet x) => x.GetName()).ToArray());
		list.Add("Back");
		SendAnswerList(list.ToArray());
	}

	private KnuBotAction ConditionSL(KnuBotOptionId id)
	{
		if (id == KnuBotOptionId.DialogStart)
		{
			return DialogShowSLArmorSets;
		}
		if (id >= KnuBotOptionId.Option1 && (int)id < SLArmorSets.Count)
		{
			selectedSet = SLArmorSets[(int)id];
			return ChooseQlFromSet;
		}
		if (id == (KnuBotOptionId)SLArmorSets.Count)
		{
			return BackToRoot;
		}
		return BackToRoot;
	}
}
