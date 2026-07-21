#region License

// Copyright (c) 2005-2014, CellAO Team
// All rights reserved.

#endregion

namespace ZoneEngine.Core.GMI
{
    using System;
    using System.Globalization;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;

    /// <summary>
    /// GMI vault deposit (MarketSend). Capture 20260715-GMI.
    /// Deposit UI: credits + up to 8 item slots in one Send.
    /// </summary>
    [MessageHandler(MessageHandlerDirection.All)]
    public class MarketSendMessageHandler : BaseMessageHandler<MarketSendMessage, MarketSendMessageHandler>
    {
        public MarketSendMessageHandler()
        {
            this.UpdateCharacterStatsOnReceive = false;
        }

        protected override void Read(MarketSendMessage message, IZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            GmiRuntimeService.ProcessPendingWithdrawals(character);

            int itemCount = 0;
            if (message.Items != null)
            {
                itemCount = message.Items.Count;
            }
            else if (message.IsItemDeposit)
            {
                itemCount = 1;
            }

            client.Server.Info(
                client,
                "MarketSend credits={0} items={1} firstLow={2} container={3} placement={4} status={5}",
                message.Credits,
                itemCount,
                message.ItemLowId,
                message.ContainerType,
                message.Placement,
                message.StatusCode);

            string failure;
            bool anyOk = false;

            if (message.IsCreditDeposit)
            {
                if (!GmiRuntimeService.TryDepositCredits(character, message.Credits, out failure))
                {
                    this.SendFailure(character, failure ?? "GMI credit deposit failed.");
                    // Still try items if present (partial deposit).
                }
                else
                {
                    anyOk = true;
                }
            }

            if (message.IsItemDeposit)
            {
                if (message.Items != null && message.Items.Count > 0)
                {
                    int deposited = 0;
                    string lastFail = null;
                    for (int i = 0; i < message.Items.Count && i < MarketSendMessage.MaxDepositItems; i++)
                    {
                        MarketSendItemEntry entry = message.Items[i];
                        if (entry == null || (entry.ItemLowId == 0 && entry.Placement < 0))
                        {
                            continue;
                        }

                        if (GmiRuntimeService.TryDepositItem(
                                character,
                                entry.ItemLowId,
                                entry.ContainerType,
                                entry.Placement,
                                out failure))
                        {
                            deposited++;
                            anyOk = true;
                        }
                        else
                        {
                            lastFail = failure;
                            // Forbidden items get the popup; keep depositing later slots.
                            if (string.Equals(failure, GmiRuntimeService.FailureForbiddenItem, StringComparison.Ordinal))
                            {
                                this.SendFailure(character, failure);
                            }
                        }
                    }

                    if (deposited == 0 && !anyOk)
                    {
                        this.SendFailure(character, lastFail ?? "GMI item deposit failed.");
                        return;
                    }

                    if (deposited > 0 && lastFail != null
                        && !string.Equals(lastFail, GmiRuntimeService.FailureForbiddenItem, StringComparison.Ordinal))
                    {
                        ChatTextMessageHandler.Default.Send(
                            character,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "GMI: deposited {0} item(s); some slots failed: {1}",
                                deposited,
                                lastFail));
                    }
                }
                else if (!GmiRuntimeService.TryDepositItem(
                             character,
                             message.ItemLowId,
                             message.ContainerType,
                             message.Placement,
                             out failure))
                {
                    this.SendFailure(character, failure ?? "GMI item deposit failed.");
                    if (!anyOk)
                    {
                        return;
                    }
                }
                else
                {
                    anyOk = true;
                }
            }

            if (anyOk)
            {
                this.SendAck(character);
            }
        }

        private void SendAck(ICharacter character)
        {
            this.Send(
                character,
                x =>
                {
                    x.Identity = character.Identity;
                    x.Unknown = 0;
                    x.Character = character.Identity;
                    x.Credits = 0;
                    x.ItemLowId = 0;
                    x.ContainerType = 0;
                    x.Placement = 0;
                    x.StatusCode = MarketSendMessage.CaptureCreditStatusCode;
                    x.Items = new System.Collections.Generic.List<MarketSendItemEntry>();
                });
        }

        private void SendFailure(ICharacter character, string text)
        {
            if (string.Equals(text, GmiRuntimeService.FailureForbiddenItem, StringComparison.Ordinal))
            {
                this.SendFormatFeedbackDialog(character, text);
                return;
            }

            ChatTextMessageHandler.Default.Send(
                character,
                string.Format(CultureInfo.InvariantCulture, "GMI: {0}", text));
        }

        /// <summary>
        /// Same FormatFeedback dialog prefix used by mail NoDrop/NoChests rejects.
        /// </summary>
        private void SendFormatFeedbackDialog(ICharacter character, string text)
        {
            if (character == null || character.Controller == null || character.Controller.Client == null)
            {
                return;
            }

            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = "~&!!!\":!!!)<sH" + text,
                    Unknown2 = 0
                },
                character.Identity.Instance);
        }
    }
}
