#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Components;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Subway.Quests;
    using ZoneEngine.Core.Thrak.Quests;

    #endregion

    /// <summary>
    /// </summary>
    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class KnuBotTradeMessageHandler : BaseMessageHandler<KnuBotTradeMessage, KnuBotTradeMessageHandler>
    {
        /// <summary>
        /// </summary>
        /// <param name="message">
        /// </param>
        /// <param name="client">
        /// </param>
        protected override void Read(KnuBotTradeMessage message, IZoneClient client)
        {
            // Alex brain Tip 4 inspect BEFORE BioCom Deliver (same steal as FinishTrade).
            if (PersonalizedRobotBrainQuestRuntime.TryStageBrainTradeItem(client.Controller.Character, message))
            {
                return;
            }

            // Alex BioCom / brain BEFORE Marcus: Marcus B196 returnTip stole Alex drag
            // (ZoneEngineLog 2026-07-21 13:02:42 marcus-trade-turnin on Alex + BioCom 156020).
            if (FlintBioComQuestRuntime.TryStageAlexTradeItem(client.Controller.Character, message))
            {
                return;
            }

            // Marcus: generic Remove permanently ate Compact Fire Suppressant when claim missed.
            if (RexMarcusChainCoordinator.TryStageMarcusTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (SurveillanceUplinkQuestRuntime.TryStageBillTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (StanGoodmanQuestRuntime.TryStageStanTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (SarahGreeneQuestRuntime.TryStageSarahTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (VernonGodfrayQuestRuntime.TryStageVernonTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (DoctorMasonQuestRuntime.TryStageMasonTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (LoreleiQuestRuntime.TryStageLoreleiTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (VaughnHammondQuestRuntime.TryStageVaughnTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (LeonoraMartyQuestRuntime.TryStageLeonoraTradeItem(client.Controller.Character, message)
                || ShinySwordQuestRuntime.TryStageSwordTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (ShippingManifestTerminalQuestRuntime.TryStageTerminalTradeItem(
                    client.Controller.Character,
                    message))
            {
                return;
            }

            if (SurveillanceUplinkQuestRuntime.ShouldSuppressGenericBillTradeRemove(
                    client.Controller.Character,
                    message.Target))
            {
                return;
            }

            if (StanGoodmanQuestRuntime.ShouldSuppressGenericStanTradeRemove(
                    client.Controller.Character,
                    message.Target))
            {
                return;
            }

            if (SarahGreeneQuestRuntime.ShouldSuppressGenericSarahTradeRemove(
                    client.Controller.Character,
                    message))
            {
                return;
            }

            if (VernonGodfrayQuestRuntime.ShouldSuppressGenericVernonTradeRemove(
                    client.Controller.Character,
                    message))
            {
                return;
            }

            if (DoctorMasonQuestRuntime.ShouldSuppressGenericMasonTradeRemove(
                    client.Controller.Character,
                    message))
            {
                return;
            }

            if (LoreleiQuestRuntime.ShouldSuppressGenericLoreleiTradeRemove(
                    client.Controller.Character,
                    message))
            {
                return;
            }

            if (VaughnHammondQuestRuntime.ShouldSuppressGenericVaughnTradeRemove(
                    client.Controller.Character,
                    message))
            {
                return;
            }

            if (LeonoraMartyQuestRuntime.ShouldSuppressGenericLeonoraTradeRemove(
                    client.Controller.Character,
                    message)
                || ShinySwordQuestRuntime.ShouldSuppressGenericSwordTradeRemove(
                    client.Controller.Character,
                    message))
            {
                return;
            }

            if (ShippingManifestTerminalQuestRuntime.ShouldSuppressGenericTerminalTradeRemove(
                    client.Controller.Character,
                    message))
            {
                return;
            }

            if (PersonalizedRobotBrainQuestRuntime.ShouldSuppressGenericAlexTradeRemove(
                    client.Controller.Character,
                    message.Target))
            {
                return;
            }

            // Thrak: generic Remove permanently ate Ancient Pattern Analyzer on Hyp inspection.
            if (ThrakGardenKeyTradeAdapter.TryStageTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (ThrakGardenKeyTradeAdapter.IsThrakTradeNpc(
                client.Controller.Character,
                message.Target))
            {
                return;
            }

            if (WindcallerKarrecTradeAdapter.TryStageTradeItem(client.Controller.Character, message))
            {
                return;
            }

            InventoryContainerRuntimeService.Default.HandleKnuBotTradeItemRemove(client, message);
        }
    }
}
