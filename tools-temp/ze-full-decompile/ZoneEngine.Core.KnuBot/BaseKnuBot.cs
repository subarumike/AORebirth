using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.KnuBot;

public class BaseKnuBot
{
	public WeakReference<ICharacter> Character;

	public Identity KnuBotIdentity;

	private KnuBotDialogTree rootNode;

	private KnuBotDialogTree selectedNode;

	protected BaseKnuBot(Identity knubotIdentity, KnuBotDialogTree root)
		: this(knubotIdentity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		SetRootNode(root);
	}

	protected BaseKnuBot(Identity knubotIdentity)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		Character = new WeakReference<ICharacter>((ICharacter)null);
		KnuBotIdentity = knubotIdentity;
	}

	protected void SetRootNode(KnuBotDialogTree node)
	{
		node.ValidateTree();
		rootNode = node;
		selectedNode = rootNode;
		rootNode.SetKnuBot(this);
	}

	public ICharacter GetCharacter()
	{
		return Character.Target;
	}

	public bool StartDialog(ICharacter character)
	{
		bool result = false;
		if (character != null)
		{
			result = true;
			Character.Target = character;
			selectedNode = rootNode;
			OpenWindow();
			Answer(KnuBotOptionId.DialogStart);
			LogUtil.Debug((DebugInfoDetail)4096, $"KnuBut Start Dialog");
		}
		return result;
	}

	public void Answer(KnuBotOptionId id)
	{
		Answer((int)id);
	}

	public void Answer(int answer)
	{
		KnuBotDialogTree knuBotDialogTree = selectedNode;
		if (answer != -1)
		{
			string text = selectedNode.Execute((KnuBotOptionId)answer);
			LogUtil.Debug((DebugInfoDetail)4096, $"Received KnuBot Answer {answer} for node {selectedNode.id} -> {text}");
			if (text == "parent")
			{
				selectedNode = selectedNode.Parent;
			}
			else if (text == "root")
			{
				selectedNode = rootNode;
			}
			else if (text != "self")
			{
				KnuBotDialogTree node = selectedNode.GetNode(text);
				if (node == null)
				{
					throw new Exception("Could not find dialog id '" + text + "' in tree '" + string.Join(Environment.NewLine, selectedNode.FlattenDialogIds()) + "'");
				}
				selectedNode = node;
			}
			if (Character.Target != null && (answer != 99 || knuBotDialogTree != selectedNode))
			{
				Answer(KnuBotOptionId.DialogStart);
			}
		}
		else
		{
			Character = new WeakReference<ICharacter>((ICharacter)null);
		}
	}

	protected KnuBotActionStruct CAS(KnuBotAction action, string nextId)
	{
		KnuBotActionStruct result = default(KnuBotActionStruct);
		result.ActionId = action.Method.Name;
		result.BotAction = action;
		result.NextDialogId = nextId;
		return result;
	}

	protected void SendAnswerList(params string[] choices)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		BaseMessageHandler<KnuBotAnswerListMessage, KnuBotAnswerListMessageHandler>.Default.Send(GetCharacter(), KnuBotIdentity, choices);
		LogUtil.Debug((DebugInfoDetail)4096, $"Sending KnuBot Choice List ({choices.Length} choices)");
	}

	protected void Write(string text)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		BaseMessageHandler<KnuBotAppendTextMessage, KnuBotAppendTextMessageHandler>.Default.Send(GetCharacter(), KnuBotIdentity, text);
		LogUtil.Debug((DebugInfoDetail)4096, $"KnuBut Write");
		Thread.Sleep(20);
	}

	protected void WriteLine(string text = "")
	{
		Write(text + "\n");
	}

	protected void OpenWindow()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		BaseMessageHandler<KnuBotOpenChatWindowMessage, KnuBotOpenChatWindowMessageHandler>.Default.Send(GetCharacter(), KnuBotIdentity);
		LogUtil.Debug((DebugInfoDetail)4096, "Opening KnuBot window");
	}

	protected void RejectItems(IEnumerable<Item> items)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		BaseMessageHandler<KnuBotRejectedItemsMessage, KnuBotRejectedItemsMessageHandler>.Default.Send(GetCharacter(), KnuBotIdentity, items);
		LogUtil.Debug((DebugInfoDetail)4096, $"KnuBut Reject {items.Count()} items");
	}

	protected void StartTrade(string message, int numberOfSlots = 6)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		BaseMessageHandler<KnuBotStartTradeMessage, KnuBotStartTradeMessageHandler>.Default.Send(GetCharacter(), KnuBotIdentity, message, numberOfSlots);
		LogUtil.Debug((DebugInfoDetail)4096, $"KnuBut Start trade ({numberOfSlots} slots)");
	}

	public virtual void FinishTrade(int amount, bool decline)
	{
		Answer(KnuBotOptionId.FinishTrade);
	}

	protected void Trade(IdentityType container, int slotNumber)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		IItem knuBotTradeItem = InventoryContainerRuntimeService.Default.GetKnuBotTradeItem(GetCharacter(), container, slotNumber);
		LogUtil.Debug((DebugInfoDetail)4096, $"KnuBut Trade item in container {((object)(IdentityType)(ref container)).ToString()} slot {slotNumber}");
	}

	public void CloseChatWindow()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		BaseMessageHandler<KnuBotCloseChatWindowMessage, KnuBotCloseChatWindowMessageHandler>.Default.Send(Character.Target, KnuBotIdentity);
		Character = new WeakReference<ICharacter>((ICharacter)null);
		LogUtil.Debug((DebugInfoDetail)4096, $"Close KnuBot window");
	}
}
