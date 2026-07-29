using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.KnuBot;
using ZoneEngine.Core.Perks;

namespace ZoneEngine.Scripts;

public class PerkResetServiceKnu : BaseKnuBot
{
	private const string PaidResetOption = "I just had a full perk reset... but I want another one... right now!";

	private const string FreeResetOption = "I want a full perk reset.";

	private const string TradePrompt = "Drag and drop the item(s) you want to give to Perk-Reset Service Provider into one of the slots available and press \"accept\"";

	private const string PaidPitch = "Perhaps I'd be willing to bend the rules a little... if you were willing to make a contribution to my retirement fund... or just give me a lot of credits... either option would work. Let's say 20 million... take it or leave it.";

	private bool awaitingPaidTrade;

	public PerkResetServiceKnu(Identity identity)
		: base(identity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		KnuBotDialogTree knuBotDialogTree = new KnuBotDialogTree("0", RootCondition, new KnuBotActionStruct[3]
		{
			CAS(MainDialog, "self"),
			CAS(GoAction, "action"),
			CAS(GoodBye, "self")
		});
		SetRootNode(knuBotDialogTree);
		knuBotDialogTree.AddNode(new KnuBotDialogTree("action", ActionCondition, new KnuBotActionStruct[2]
		{
			CAS(DoResetOrStartTrade, "self"),
			CAS(GoodBye, "self")
		}));
	}

	public override void FinishTrade(int amount, bool decline)
	{
		if (!awaitingPaidTrade)
		{
			return;
		}
		awaitingPaidTrade = false;
		ICharacter character = GetCharacter();
		Character val = (Character)(object)((character is Character) ? character : null);
		if (val == null)
		{
			CloseChatWindow();
			return;
		}
		RejectItems(new List<Item>());
		if (decline || amount < 20000000)
		{
			WriteLine("Come back when you're ready to pay.");
			SendAnswerList("Goodbye");
			return;
		}
		if (!PerkRuntimeService.Default.TryResetAllPerks(val, chargeEarlyFee: true))
		{
			WriteLine("You don't have enough credits.");
			SendAnswerList("Goodbye");
			return;
		}
		WriteLine("Credit well spent... ");
		WriteLine("there you go... your perks are cleared.");
		WriteLine(" Come back any time!");
		awaitingPaidTrade = false;
		SendAnswerList("Goodbye");
	}

	private KnuBotAction RootCondition(KnuBotOptionId id)
	{
		return id switch
		{
			KnuBotOptionId.DialogStart => MainDialog, 
			KnuBotOptionId.Option1 => GoAction, 
			KnuBotOptionId.Option2 => GoodBye, 
			_ => null, 
		};
	}

	private KnuBotAction ActionCondition(KnuBotOptionId id)
	{
		return id switch
		{
			KnuBotOptionId.DialogStart => DoResetOrStartTrade, 
			KnuBotOptionId.Option1 => GoodBye, 
			_ => null, 
		};
	}

	private void MainDialog()
	{
		ICharacter character = GetCharacter();
		Character val = (Character)(object)((character is Character) ? character : null);
		WriteLine("Hi there.");
		WriteLine();
		if (val != null && PerkRuntimeService.Default.IsFullPerkResetFree(val))
		{
			SendAnswerList("I want a full perk reset.", "Goodbye");
		}
		else
		{
			SendAnswerList("I just had a full perk reset... but I want another one... right now!", "Goodbye");
		}
	}

	private void GoAction()
	{
	}

	private void DoResetOrStartTrade()
	{
		if (awaitingPaidTrade)
		{
			return;
		}
		ICharacter character = GetCharacter();
		Character val = (Character)(object)((character is Character) ? character : null);
		if (val == null)
		{
			CloseChatWindow();
		}
		else if (PerkRuntimeService.Default.IsFullPerkResetFree(val))
		{
			if (!PerkRuntimeService.Default.TryResetAllPerks(val, chargeEarlyFee: false))
			{
				WriteLine("Unable to reset perks right now.");
				SendAnswerList("Goodbye");
			}
			else
			{
				WriteLine("there you go... your perks are cleared.");
				WriteLine(" Come back any time!");
				CloseChatWindow();
			}
		}
		else
		{
			WriteLine("Perhaps I'd be willing to bend the rules a little... if you were willing to make a contribution to my retirement fund... or just give me a lot of credits... either option would work. Let's say 20 million... take it or leave it.");
			awaitingPaidTrade = true;
			StartTrade("Drag and drop the item(s) you want to give to Perk-Reset Service Provider into one of the slots available and press \"accept\"", 0);
		}
	}

	private void GoodBye()
	{
		awaitingPaidTrade = false;
		CloseChatWindow();
	}
}
