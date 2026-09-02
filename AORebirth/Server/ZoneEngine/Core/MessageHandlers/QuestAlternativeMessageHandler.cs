namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Handles the client's mission-terminal "roll" request. The client sends a
    /// QuestAlternativeMessage (with an empty offer list and the current slider settings) whenever it
    /// wants a fresh set of missions; the server answers with a QuestAlternativeMessage carrying the
    /// generated offers. Without this reply the mission terminal window stays empty.
    /// </summary>
    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class QuestAlternativeMessageHandler :
        BaseMessageHandler<QuestAlternativeMessage, QuestAlternativeMessageHandler>
    {
        protected override void Read(QuestAlternativeMessage message, IZoneClient client)
        {
            if (client == null || client.Controller == null || client.Controller.Character == null)
            {
                return;
            }

            ICharacter character = client.Controller.Character;
            var zoneClient = client as ZoneEngine.Core.ZoneClient;
            if (zoneClient == null)
            {
                return;
            }

            client.Server.Info(
                client,
                "QuestAlternative roll request terminal={0} sliders=[lvl={1} gb={2} oc={3} oh={4} pm={5} hs={6} me={7}] existingOffers={8}",
                message.MissionTerminalIdentity,
                message.LevelSlider,
                message.GoodBadSlider,
                message.OrderChaosSlider,
                message.OpenHiddenSlider,
                message.PhysicalMysticalSlider,
                message.HeadOnStealthSlider,
                message.MoneyExperienceSlider,
                message.QuestInfos == null ? 0 : message.QuestInfos.Length);

            bool rollFeeWasCharged = false;
            try
            {
                lock (MissionOfferStore.AuthorityGate)
                {
                int characterLevel = character.Stats[StatIds.level].Value;
                int terminalPlayfieldId = character.Playfield != null
                                             ? character.Playfield.Identity.Instance
                                             : 0;
                MissionLocationSide characterSide = MissionLocationPool.ResolveCharacterSide(
                    character.Stats[StatIds.side].Value);
                if (!MissionLocationPool.CanCharacterRollAtTerminal(characterSide, terminalPlayfieldId))
                {
                    client.Server.Info(
                        client,
                        "QuestAlternative roll blocked — charSide={0} terminalPf={1}",
                        characterSide,
                        terminalPlayfieldId);
                    character.Send(
                        new FormatFeedbackMessage
                        {
                            Identity = character.Identity,
                            Unknown = 1,
                            Unknown1 = 0,
                            Unknown2 = 0,
                            FormattedMessage = TokenBoardRuntime.ToYellowSystemFeedback(
                                MissionLocationPool.FormatSideRestrictedRollFeedback(terminalPlayfieldId))
                        });
                    return;
                }

                int missionQuality;
                MissionSliderProfile sliderProfile;
                string sliderError = null;
                string graphError;
                if (!MissionLevelTable.TryGetMissionQuality(
                        characterLevel,
                        message.LevelSlider,
                        out missionQuality,
                        out graphError))
                {
                    bool graphUnavailable =
                        !string.IsNullOrEmpty(graphError);
                    client.Server.Info(
                        client,
                        graphUnavailable
                            ? "QuestAlternative roll blocked — official mission-level graph unavailable lvl={0} slider={1} err={2}"
                            : "QuestAlternative roll blocked — unsupported difficulty detent lvl={0} slider={1}",
                        characterLevel,
                        message.LevelSlider,
                        graphUnavailable
                            ? graphError
                            : string.Empty);
                    character.Send(
                        new FormatFeedbackMessage
                        {
                            Identity = character.Identity,
                            Unknown = 1,
                            Unknown1 = 0,
                            Unknown2 = 0,
                            FormattedMessage = TokenBoardRuntime.ToYellowSystemFeedback(
                                graphUnavailable
                                    ? "The mission terminal's official level table is unavailable or invalid. No credits were deducted."
                                    : "The mission terminal rejected an unsupported difficulty value.")
                        });
                    return;
                }

                if (!MissionSliderProfile.TryCreate(
                        message,
                        out sliderProfile,
                        out sliderError))
                {
                    client.Server.Info(
                        client,
                        "QuestAlternative roll blocked — unsupported slider encoding lvl={0} err={1}",
                        message.LevelSlider,
                        sliderError ?? "invalid difficulty detent");
                    character.Send(
                        new FormatFeedbackMessage
                        {
                            Identity = character.Identity,
                            Unknown = 1,
                            Unknown1 = 0,
                            Unknown2 = 0,
                            FormattedMessage = TokenBoardRuntime.ToYellowSystemFeedback(
                                "The mission terminal rejected an unsupported slider value.")
                        });
                    return;
                }

                float terminalX = 0f;
                float terminalZ = 0f;
                try
                {
                    Coordinate coords = character.CalculatePredictedPosition();
                    terminalX = coords.x;
                    terminalZ = coords.z;
                }
                catch
                {
                }

                int rollSeed;
                int responseNonce;
                QuestAlternativeMessage response = MissionRollService.BuildRollResponse(
                    message,
                    character.Identity,
                    characterLevel,
                    terminalPlayfieldId,
                    terminalX,
                    terminalZ,
                    characterSide,
                    MissionRollService.ResolveClientClockNowSeconds(
                        zoneClient.LastGameTimeSyncUtc,
                        DateTime.UtcNow),
                    out rollSeed,
                    out responseNonce);

                if (response == null
                    || response.QuestInfos == null
                    || response.QuestInfos.Length == 0)
                {
                    client.Server.Info(client, "QuestAlternative roll blocked — empty offer set");
                    character.Send(
                        new FormatFeedbackMessage
                        {
                            Identity = character.Identity,
                            Unknown = 1,
                            Unknown1 = 0,
                            Unknown2 = 0,
                            FormattedMessage = TokenBoardRuntime.ToYellowSystemFeedback(
                                "The mission terminal failed to prepare missions. No credits were deducted.")
                        });
                    return;
                }

                byte[] serializedRollPayload =
                    MissionRollService.SerializeBody(response);
                DateTime issuedUtc = DateTime.UtcNow;
                MissionOfferBatchHandle storedBatch;
                string offerStoreFailure;
                if (!MissionOfferStore.TryStoreRoll(
                    character.Identity,
                    response,
                    message,
                    issuedUtc,
                    rollSeed,
                    responseNonce,
                    serializedRollPayload,
                    out storedBatch,
                    out offerStoreFailure))
                {
                    client.Server.Info(
                        client,
                        "QuestAlternative roll blocked - durable offer authority unavailable: {0}",
                        offerStoreFailure);
                    character.Send(
                        new FormatFeedbackMessage
                        {
                            Identity = character.Identity,
                            Unknown = 1,
                            Unknown1 = 0,
                            Unknown2 = 0,
                            FormattedMessage = TokenBoardRuntime.ToYellowSystemFeedback(
                                "The mission terminal could not preserve this roll. No credits were deducted.")
                        });
                    return;
                }

                // Capture order: fee deduct feedback, then the 5-offer QuestAlternative.
                // Never charge unless we have a non-empty roll ready to send.
                int fee = MissionRollFeeRules.FeeForLevel(characterLevel);
                string feeClaimFailure;
                if (!MissionOfferStore.TryBeginFeeCharge(
                        storedBatch,
                        fee,
                        DateTime.UtcNow,
                        out feeClaimFailure))
                {
                    client.Server.Info(
                        client,
                        "QuestAlternative roll-fee claim could not be durably reserved: {0}",
                        feeClaimFailure);
                    character.Send(
                        new FormatFeedbackMessage
                        {
                            Identity = character.Identity,
                            Unknown = 1,
                            Unknown1 = 0,
                            Unknown2 = 0,
                            FormattedMessage = TokenBoardRuntime.ToYellowSystemFeedback(
                                "The mission terminal could not preserve the roll fee. No credits were deducted.")
                        });
                    return;
                }

                int appliedFee;
                int cashBefore;
                int cashAfter;
                bool insufficientCredits;
                if (!MissionRollFeeService.TryChargeRollFee(
                        character,
                        storedBatch.BatchIdentity,
                        fee,
                        out appliedFee,
                        out cashBefore,
                        out cashAfter,
                        out insufficientCredits,
                        out feeClaimFailure))
                {
                    string discardFailure;
                    bool batchDiscarded = MissionOfferStore.TryDiscardBatch(
                            storedBatch,
                            DateTime.UtcNow,
                            "RollFeeRejected",
                            out discardFailure);
                    if (!batchDiscarded)
                    {
                        client.Server.Info(
                            client,
                            "QuestAlternative rejected batch could not be durably discarded: {0}",
                            discardFailure);
                        return;
                    }

                    if (batchDiscarded && insufficientCredits)
                    {
                        MissionRollFeeService.NotifyRollFeeRejected(
                            character,
                            appliedFee,
                            cashBefore);
                    }

                    client.Server.Info(
                        client,
                        string.IsNullOrEmpty(feeClaimFailure)
                            ? "QuestAlternative roll blocked — need {0} credits"
                            : "QuestAlternative durable roll-fee claim failed: {0}",
                        string.IsNullOrEmpty(feeClaimFailure)
                            ? (object)fee
                            : feeClaimFailure);
                    return;
                }

                rollFeeWasCharged = true;
                MissionRollFeeService.NotifyRollFeeApplied(
                    character,
                    appliedFee,
                    cashBefore,
                    cashAfter);

                string publicationFailure;
                if (!MissionOfferStore.TryPublishBatch(
                        storedBatch,
                        DateTime.UtcNow,
                        out publicationFailure))
                {
                    client.Server.Info(
                        client,
                        "QuestAlternative paid batch could not be durably published: {0}",
                        publicationFailure);
                    character.Send(
                        new FormatFeedbackMessage
                        {
                            Identity = character.Identity,
                            Unknown = 1,
                            Unknown1 = 0,
                            Unknown2 = 0,
                            FormattedMessage = TokenBoardRuntime.ToYellowSystemFeedback(
                                "The mission roll could not be preserved after the fee was deducted.")
                        });
                    return;
                }

                client.SendCompressed(response);

                client.Server.Info(
                    client,
                    "QuestAlternative roll response sent offers={0} charLvl={1} slider={2} ql={3} fee={4} terminal={5}",
                    response.QuestInfos.Length,
                    characterLevel,
                    message.LevelSlider,
                    missionQuality,
                    fee,
                    response.MissionTerminalIdentity);
                }
            }
            catch (Exception ex)
            {
                MissionDiagnostics.Log("ROLL-FAIL {0}", ex);
                client.Server.Info(client, "QuestAlternative roll response failed: {0}", ex);
                character.Send(
                    new FormatFeedbackMessage
                    {
                        Identity = character.Identity,
                        Unknown = 1,
                        Unknown1 = 0,
                        Unknown2 = 0,
                        FormattedMessage = TokenBoardRuntime.ToYellowSystemFeedback(
                            rollFeeWasCharged
                                ? "The mission roll could not be delivered after the fee was deducted."
                                : "The mission terminal failed to prepare missions. No credits were deducted.")
                    });
            }
        }
    }
}
