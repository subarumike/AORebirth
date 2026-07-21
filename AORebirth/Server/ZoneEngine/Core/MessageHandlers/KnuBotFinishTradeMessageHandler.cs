#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.KnuBot;
    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Subway.Quests;
    using ZoneEngine.Core.Thrak.Quests;

    #endregion

    /// <summary>
    /// Capture 20260716-Reset-perks: client FinishTrade with Amount (credits) for Perk-Reset Service Provider.
    /// </summary>
    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class KnuBotFinishTradeMessageHandler :
        BaseMessageHandler<KnuBotFinishTradeMessage, KnuBotFinishTradeMessageHandler>
    {
        public override void Receive(MessageWrapper<KnuBotFinishTradeMessage> messageWrapper)
        {
            if (messageWrapper == null || messageWrapper.MessageBody == null || messageWrapper.Client == null)
            {
                return;
            }

            if (WindcallerKarrecTradeAdapter.TryFinishTrade(
                messageWrapper.Client.Controller.Character,
                messageWrapper.MessageBody))
            {
                return;
            }

            if (ThrakGardenKeyTradeAdapter.TryFinishTrade(
                messageWrapper.Client.Controller.Character,
                messageWrapper.MessageBody))
            {
                return;
            }

            // Alex BioCom before Marcus: B196 returnTip must not steal Alex Accept.
            if (FlintBioComQuestRuntime.TryFinishAlexTrade(
                messageWrapper.Client.Controller.Character,
                messageWrapper.MessageBody))
            {
                return;
            }

            if (PersonalizedRobotBrainQuestRuntime.TryFinishBrainTrade(
                messageWrapper.Client.Controller.Character,
                messageWrapper.MessageBody))
            {
                return;
            }

            if (RexMarcusChainCoordinator.TryFinishMarcusTrade(
                messageWrapper.Client.Controller.Character,
                messageWrapper.MessageBody))
            {
                return;
            }

            // Stan before Bill: prior Bill tip/HC-12 greed stole Stan Accept
            // (ZoneEngineLog 2026-07-21 01:33:42 bill-turnin ABORTED during Stan deliver).
            if (StanGoodmanQuestRuntime.TryFinishStanTrade(
                messageWrapper.Client.Controller.Character,
                messageWrapper.MessageBody))
            {
                return;
            }

            if (SarahGreeneQuestRuntime.TryFinishSarahTrade(
                messageWrapper.Client.Controller.Character,
                messageWrapper.MessageBody))
            {
                return;
            }

            if (VernonGodfrayQuestRuntime.TryFinishVernonTrade(
                messageWrapper.Client.Controller.Character,
                messageWrapper.MessageBody))
            {
                return;
            }

            if (DoctorMasonQuestRuntime.TryFinishMasonTrade(
                messageWrapper.Client.Controller.Character,
                messageWrapper.MessageBody))
            {
                return;
            }

            if (LoreleiQuestRuntime.TryFinishLoreleiTrade(
                messageWrapper.Client.Controller.Character,
                messageWrapper.MessageBody))
            {
                return;
            }

            if (VaughnHammondQuestRuntime.TryFinishVaughnTrade(
                messageWrapper.Client.Controller.Character,
                messageWrapper.MessageBody))
            {
                return;
            }

            if (ShippingManifestTerminalQuestRuntime.TryFinishTerminalTrade(
                messageWrapper.Client.Controller.Character,
                messageWrapper.MessageBody))
            {
                return;
            }

            if (SurveillanceUplinkQuestRuntime.TryFinishBillTrade(
                messageWrapper.Client.Controller.Character,
                messageWrapper.MessageBody))
            {
                return;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(messageWrapper.MessageBody.Target);
            if (npc == null)
            {
                return;
            }

            NPCController controller = npc.Controller as NPCController;
            if (controller == null || controller.KnuBot == null)
            {
                return;
            }

            BaseKnuBot knu = controller.KnuBot;
            KnuBotFinishTradeMessage body = messageWrapper.MessageBody;
            knu.FinishTrade(body.Amount, body.Decline != 0);
        }
    }
}
