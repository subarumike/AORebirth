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
            // Marcus FIRST: generic Remove permanently ate Compact Fire Suppressant when claim missed.
            if (RexMarcusChainCoordinator.TryStageMarcusTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (PersonalizedRobotBrainQuestRuntime.TryStageBrainTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (FlintBioComQuestRuntime.TryStageAlexTradeItem(client.Controller.Character, message))
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
