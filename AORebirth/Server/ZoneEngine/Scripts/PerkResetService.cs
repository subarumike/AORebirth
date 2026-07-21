#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Scripts
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.KnuBot;
    using ZoneEngine.Core.Perks;
    using ZoneEngine.Script;

    #endregion

    /// <summary>
    /// Capture-backed Perk-Reset Service Provider (20260716-Reset-perks).
    /// Free full reset every 2 days; early reset = 20,000,000 credits via FinishTrade.
    /// </summary>
    public class PerkResetService : IAOScript
    {
        public void Main(string[] args)
        {
        }

        public void InitPerkResetService(ICharacter character)
        {
            var knu = new PerkResetServiceKnu(character.Identity);
            var controller = character.Controller as NPCController;
            if (controller != null)
            {
                controller.SetKnuBot(knu);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Initialized PerkResetService with npc " + character.Identity.ToString(true));
            }
        }
    }

    public class PerkResetServiceKnu : BaseKnuBot
    {
        private const string PaidResetOption =
            "I just had a full perk reset... but I want another one... right now!";

        private const string FreeResetOption = "I want a full perk reset.";

        private const string TradePrompt =
            "Drag and drop the item(s) you want to give to Perk-Reset Service Provider into one of the slots available and press \"accept\"";

        private const string PaidPitch =
            "Perhaps I'd be willing to bend the rules a little... if you were willing to make a contribution to my retirement fund... or just give me a lot of credits... either option would work. Let's say 20 million... take it or leave it.";

        private bool awaitingPaidTrade;

        public PerkResetServiceKnu(Identity identity)
            : base(identity)
        {
            KnuBotDialogTree root = new KnuBotDialogTree(
                "0",
                this.RootCondition,
                new[]
                {
                    this.CAS(this.MainDialog, "self"),
                    this.CAS(this.GoAction, "action"),
                    this.CAS(this.GoodBye, "self")
                });
            this.SetRootNode(root);

            root.AddNode(
                new KnuBotDialogTree(
                    "action",
                    this.ActionCondition,
                    new[]
                    {
                        this.CAS(this.DoResetOrStartTrade, "self"),
                        this.CAS(this.GoodBye, "self")
                    }));
        }

        public override void FinishTrade(int amount, bool decline)
        {
            if (!this.awaitingPaidTrade)
            {
                return;
            }

            this.awaitingPaidTrade = false;
            Character player = this.GetCharacter() as Character;
            if (player == null)
            {
                this.CloseChatWindow();
                return;
            }

            this.RejectItems(new List<Item>());
            if (decline || amount < PerkRuntimeService.EarlyFullPerkResetCreditCost)
            {
                this.WriteLine("Come back when you're ready to pay.");
                this.SendAnswerList("Goodbye");
                return;
            }

            if (!PerkRuntimeService.Default.TryResetAllPerks(player, chargeEarlyFee: true))
            {
                this.WriteLine("You don't have enough credits.");
                this.SendAnswerList("Goodbye");
                return;
            }

            this.WriteLine("Credit well spent... ");
            this.WriteLine("there you go... your perks are cleared.");
            this.WriteLine(" Come back any time!");
            this.awaitingPaidTrade = false;

            // Only the trade ("Give Item") window closes (client-side on FinishTrade); keep the chat
            // dialog open so the player sees the confirmation instead of the whole window closing.
            this.SendAnswerList("Goodbye");
        }

        private KnuBotAction RootCondition(KnuBotOptionId id)
        {
            switch (id)
            {
                case KnuBotOptionId.DialogStart:
                    return this.MainDialog;
                case KnuBotOptionId.Option1:
                    return this.GoAction;
                case KnuBotOptionId.Option2:
                    return this.GoodBye;
            }

            return null;
        }

        private KnuBotAction ActionCondition(KnuBotOptionId id)
        {
            switch (id)
            {
                case KnuBotOptionId.DialogStart:
                    return this.DoResetOrStartTrade;
                case KnuBotOptionId.Option1:
                    return this.GoodBye;
            }

            return null;
        }

        private void MainDialog()
        {
            Character player = this.GetCharacter() as Character;
            this.WriteLine("Hi there.");
            this.WriteLine();
            if (player != null && PerkRuntimeService.Default.IsFullPerkResetFree(player))
            {
                this.SendAnswerList(FreeResetOption, "Goodbye");
            }
            else
            {
                this.SendAnswerList(PaidResetOption, "Goodbye");
            }
        }

        private void GoAction()
        {
            // Transition only; DoResetOrStartTrade runs via DialogStart on action node.
        }

        private void DoResetOrStartTrade()
        {
            if (this.awaitingPaidTrade)
            {
                return;
            }

            Character player = this.GetCharacter() as Character;
            if (player == null)
            {
                this.CloseChatWindow();
                return;
            }

            if (PerkRuntimeService.Default.IsFullPerkResetFree(player))
            {
                if (!PerkRuntimeService.Default.TryResetAllPerks(player, chargeEarlyFee: false))
                {
                    this.WriteLine("Unable to reset perks right now.");
                    this.SendAnswerList("Goodbye");
                    return;
                }

                this.WriteLine("there you go... your perks are cleared.");
                this.WriteLine(" Come back any time!");
                this.CloseChatWindow();
                return;
            }

            this.WriteLine(PaidPitch);
            this.awaitingPaidTrade = true;
            this.StartTrade(TradePrompt, 0);
        }

        private void GoodBye()
        {
            this.awaitingPaidTrade = false;
            this.CloseChatWindow();
        }
    }
}
