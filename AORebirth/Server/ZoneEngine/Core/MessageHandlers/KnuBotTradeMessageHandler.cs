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
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Doja;
    using ZoneEngine.Core.Nascence.Quests;
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
            ICharacter character = client.Controller.Character;
            if (RosenblattHiathlinTradeAdapter.ShouldClaimTradeMessage(character, message)
                && RosenblattHiathlinTradeAdapter.TryStageTradeItem(character, message))
            {
                return;
            }

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

            // Aban before Thrak: Dreaming Silvertail is shared; Thrak staging stole Aban soul trades.
            if (NascenceAbanFalaTradeAdapter.ShouldClaimTradeBeforeThrak(
                    client.Controller.Character,
                    message)
                && NascenceAbanFalaTradeAdapter.TryStageTradeItem(client.Controller.Character, message))
            {
                return;
            }

            // Thrak: generic Remove permanently ate Ancient Pattern Analyzer on Hyp inspection.
            if (ThrakGardenKeyTradeAdapter.TryStageTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (NascenceAbanFalaTradeAdapter.TryStageTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (NascenceAbanFalaTradeAdapter.IsAbanChainTradeNpc(
                client.Controller.Character,
                message.Target))
            {
                return;
            }

            if (ThrakGardenKeyTradeAdapter.IsThrakTradeNpc(
                client.Controller.Character,
                message.Target))
            {
                return;
            }

            if (DojaChipTradeAdapter.TryStageTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (DojaChipTradeAdapter.IsDojaTradeNpc(
                client.Controller.Character,
                message.Target))
            {
                return;
            }

            if (RosenblattHiathlinTradeAdapter.TryStageTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (RosenblattHiathlinTradeAdapter.HasActiveSession(client.Controller.Character)
                && RosenblattHiathlinTradeAdapter.IsRosenblattTradeNpc(
                    client.Controller.Character,
                    message.Target))
            {
                return;
            }

            if (RosenblattPapagenaTradeAdapter.TryStageTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (RosenblattPapagenaTradeAdapter.HasActiveSession(client.Controller.Character)
                && RosenblattPapagenaTradeAdapter.IsRosenblattDiscTradeNpc(
                    client.Controller.Character,
                    message.Target))
            {
                return;
            }

            if (RosenblattCascadingSpiritTradeAdapter.TryStageTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (RosenblattCascadingSpiritTradeAdapter.HasActiveSession(client.Controller.Character)
                && RosenblattCascadingSpiritTradeAdapter.IsRosenblattTradeNpc(
                    client.Controller.Character,
                    message.Target))
            {
                return;
            }

            if (RosenblattSpinetoothTradeAdapter.TryStageTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (RosenblattSpinetoothTradeAdapter.HasActiveSession(client.Controller.Character)
                && RosenblattSpinetoothTradeAdapter.IsRosenblattDiscTradeNpc(
                    client.Controller.Character,
                    message.Target))
            {
                return;
            }

            if (RosenblattDemonicTradeAdapter.TryStageTradeItem(client.Controller.Character, message))
            {
                return;
            }

            if (RosenblattDemonicTradeAdapter.HasActiveSession(client.Controller.Character)
                && RosenblattDemonicTradeAdapter.IsRosenblattDiscTradeNpc(
                    client.Controller.Character,
                    message.Target))
            {
                return;
            }

            if (RosenblattHiathlinTradeAdapter.IsRosenblattTradeNpc(
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
