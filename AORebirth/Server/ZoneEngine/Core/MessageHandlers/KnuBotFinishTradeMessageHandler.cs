#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.KnuBot;
    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Doja;
    using ZoneEngine.Core.Nascence.Quests;
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

            KnuBotFinishTradeMessage body = messageWrapper.MessageBody;
            if (messageWrapper.Client.Controller == null || messageWrapper.Client.Controller.Character == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "KnuBotFinishTrade ignored because character is unavailable target="
                    + body.Target.ToString(true));
                return;
            }

            ICharacter source = messageWrapper.Client.Controller.Character;
            messageWrapper.Client.Server.Info(
                messageWrapper.Client,
                "KnuBotFinishTrade target={0} decline={1} by={2}",
                body.Target,
                body.Decline,
                source.Identity);

            // Disc turn-ins before Hiathlin body-part trade (same NPC; Hiathlin must not steal discs).
            if (RosenblattPapagenaTradeAdapter.TryFinishTrade(source, body))
            {
                return;
            }

            if (RosenblattCascadingSpiritTradeAdapter.TryFinishTrade(source, body))
            {
                return;
            }

            if (RosenblattSpinetoothTradeAdapter.TryFinishTrade(source, body))
            {
                return;
            }

            if (RosenblattDemonicTradeAdapter.TryFinishTrade(source, body))
            {
                return;
            }

            try
            {
                if (RosenblattHiathlinTradeAdapter.TryFinishTrade(source, body))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                LogUtil.ErrorException(
                    ex,
                    false,
                    "RosenblattHiathlin FinishTrade failed by=" + source.Identity.ToString(true));
            }

            if (WindcallerKarrecTradeAdapter.TryFinishTrade(source, body))
            {
                return;
            }

            if (NascenceAbanFalaTradeAdapter.TryFinishTrade(source, body))
            {
                return;
            }

            if (ThrakGardenKeyTradeAdapter.TryFinishTrade(source, body))
            {
                return;
            }

            if (DojaChipTradeAdapter.TryFinishTrade(source, body))
            {
                return;
            }

            // Alex Personalized Robot Brain inspect BEFORE BioCom Deliver.
            // ZoneEngineLog 2026-07-22 20:32:17: BioCom claimed FinishTrade while Tip 4
            // was active ("alex-finish ignored") → brain tip never completed / item not returned.
            if (PersonalizedRobotBrainQuestRuntime.TryFinishBrainTrade(
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

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(body.Target);
            if (npc == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "KnuBotFinishTrade unresolved npc target="
                    + body.Target.ToString(true)
                    + " by="
                    + source.Identity.ToString(true));
                return;
            }

            NPCController controller = npc.Controller as NPCController;
            if (controller == null || controller.KnuBot == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "KnuBotFinishTrade no knubot target="
                    + body.Target.ToString(true)
                    + " by="
                    + source.Identity.ToString(true));
                return;
            }

            BaseKnuBot knu = controller.KnuBot;
            knu.FinishTrade(body.Amount, body.Decline != 0);
        }
    }
}
