using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Entities;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.KnuBot;

public class KnuBotDialogTree
{
	private readonly List<KnuBotDialogTree> nodes = new List<KnuBotDialogTree>();

	private KnuBotDialogTree parent = null;

	private readonly KnuBotCondition condition;

	private BaseKnuBot knuBot = null;

	public readonly string id = string.Empty;

	private readonly List<KnuBotActionStruct> knuBotActions = new List<KnuBotActionStruct>();

	public KnuBotDialogTree Parent
	{
		get
		{
			KnuBotDialogTree knuBotDialogTree = parent;
			return parent;
		}
		protected set
		{
			parent = value;
		}
	}

	private ICharacter Character => GetBaseKnuBot().GetCharacter();

	private Identity KnuBotIdentity => GetBaseKnuBot().KnuBotIdentity;

	public KnuBotDialogTree(string id, KnuBotCondition condition, IEnumerable<KnuBotActionStruct> actions)
	{
		this.id = id;
		this.condition = condition;
		knuBotActions.AddRange(actions);
	}

	private BaseKnuBot GetBaseKnuBot()
	{
		KnuBotDialogTree knuBotDialogTree = this;
		while (knuBotDialogTree.Parent != null)
		{
			knuBotDialogTree = knuBotDialogTree.Parent;
		}
		BaseKnuBot baseKnuBot = knuBot;
		if (baseKnuBot == null)
		{
			throw new Exception("Base KnuBot gone away.");
		}
		return baseKnuBot;
	}

	internal void SetKnuBot(BaseKnuBot knu)
	{
		knuBot = knu;
	}

	public string Execute(KnuBotOptionId optionId = KnuBotOptionId.DialogStart)
	{
		string result = string.Empty;
		if (Character != null)
		{
			KnuBotAction knuBotAction = condition(optionId);
			if (knuBotAction != null)
			{
				string actionId = knuBotAction.Method.Name;
				if (actionId != string.Empty)
				{
					(from x in knuBotActions
						where x.ActionId == actionId
						select x.BotAction).First()();
					result = knuBotActions.First((KnuBotActionStruct x) => x.ActionId == actionId).NextDialogId;
				}
			}
		}
		return result;
	}

	public KnuBotDialogTree AddNode(KnuBotDialogTree dialogTree)
	{
		nodes.Add(dialogTree);
		dialogTree.Parent = this;
		dialogTree.SetKnuBot(GetBaseKnuBot());
		KnuBotDialogTree knuBotDialogTree = this;
		while (knuBotDialogTree.Parent != null)
		{
			knuBotDialogTree = knuBotDialogTree.Parent;
		}
		knuBotDialogTree.ValidateTree();
		return dialogTree;
	}

	public bool ValidateTree()
	{
		if (Parent != null)
		{
			throw new Exception("Please use the root node for validation only");
		}
		bool flag = true;
		string[] array = FlattenNextDialogIds();
		string[] array2 = FlattenDialogIds();
		if ((from n in array2
			group n by n).Any((IGrouping<string, string> c) => c.Count() > 1))
		{
			throw new Exception("Please check your Dialog Ids: " + Environment.NewLine + string.Join(Environment.NewLine, from n in array2
				group n by n into c
				select c.Count() > 1));
		}
		string[] array3 = array2;
		foreach (string dialogId in array3)
		{
			flag &= array.Any((string c) => c == dialogId);
		}
		string[] array4 = array;
		foreach (string nextDialogId in array4)
		{
			flag &= array2.Any((string c) => c == nextDialogId);
		}
		foreach (KnuBotActionStruct knuBotAction in knuBotActions)
		{
			if (knuBotAction.NextDialogId == "parent")
			{
				throw new Exception("'parent' called from root node, huh?");
			}
			if (string.IsNullOrEmpty(knuBotAction.ActionId))
			{
				throw new Exception("Action id is null or empty.");
			}
		}
		return flag;
	}

	public string[] FlattenNextDialogIds()
	{
		List<string> list = new List<string>();
		foreach (KnuBotActionStruct knuBotAction in knuBotActions)
		{
			if (knuBotAction.NextDialogId != "parent" && knuBotAction.NextDialogId != "root" && knuBotAction.NextDialogId != "self")
			{
				list.Add(knuBotAction.NextDialogId);
			}
		}
		foreach (KnuBotDialogTree node in nodes)
		{
			list.AddRange(node.FlattenNextDialogIds());
		}
		return list.ToArray();
	}

	public string[] FlattenDialogIds()
	{
		List<string> list = new List<string>();
		foreach (KnuBotDialogTree node in nodes)
		{
			list.Add(node.id);
			list.AddRange(node.FlattenDialogIds());
		}
		return list.ToArray();
	}

	internal KnuBotDialogTree GetNode(string nextId)
	{
		return nodes.FirstOrDefault((KnuBotDialogTree x) => x.id == nextId);
	}
}
