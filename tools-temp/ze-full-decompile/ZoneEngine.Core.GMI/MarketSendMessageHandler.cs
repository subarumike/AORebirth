using System;
using System.Collections.Generic;
using System.Globalization;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Interfaces;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.GMI;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class MarketSendMessageHandler : BaseMessageHandler<MarketSendMessage, MarketSendMessageHandler>
{
	public MarketSendMessageHandler()
	{
		base.UpdateCharacterStatsOnReceive = false;
	}

	protected override void Read(MarketSendMessage message, IZoneClient client)
	{
		ICharacter character = client.Controller.Character;
		GmiRuntimeService.ProcessPendingWithdrawals(character);
		int num = 0;
		if (message.Items != null)
		{
			num = message.Items.Count;
		}
		else if (message.IsItemDeposit)
		{
			num = 1;
		}
		((IClient)client).Server.Info((IClient)(object)client, "MarketSend credits={0} items={1} firstLow={2} container={3} placement={4} status={5}", new object[6] { message.Credits, num, message.ItemLowId, message.ContainerType, message.Placement, message.StatusCode });
		bool flag = false;
		string failureReason;
		if (message.IsCreditDeposit)
		{
			if (!GmiRuntimeService.TryDepositCredits(character, message.Credits, out failureReason))
			{
				SendFailure(character, failureReason ?? "GMI credit deposit failed.");
			}
			else
			{
				flag = true;
			}
		}
		if (message.IsItemDeposit)
		{
			if (message.Items != null && message.Items.Count > 0)
			{
				int num2 = 0;
				string text = null;
				for (int i = 0; i < message.Items.Count && i < 8; i++)
				{
					MarketSendItemEntry val = message.Items[i];
					if (val == null || (val.ItemLowId == 0 && val.Placement < 0))
					{
						continue;
					}
					if (GmiRuntimeService.TryDepositItem(character, val.ItemLowId, val.ContainerType, val.Placement, out failureReason))
					{
						num2++;
						flag = true;
						continue;
					}
					text = failureReason;
					if (string.Equals(failureReason, "Cannot Sell Container Backpack Nodrop Unique items", StringComparison.Ordinal))
					{
						SendFailure(character, failureReason);
					}
				}
				if (num2 == 0 && !flag)
				{
					SendFailure(character, text ?? "GMI item deposit failed.");
					return;
				}
				if (num2 > 0 && text != null && !string.Equals(text, "Cannot Sell Container Backpack Nodrop Unique items", StringComparison.Ordinal))
				{
					BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, string.Format(CultureInfo.InvariantCulture, "GMI: deposited {0} item(s); some slots failed: {1}", num2, text), 0, 0);
				}
			}
			else if (!GmiRuntimeService.TryDepositItem(character, message.ItemLowId, message.ContainerType, message.Placement, out failureReason))
			{
				SendFailure(character, failureReason ?? "GMI item deposit failed.");
				if (!flag)
				{
					return;
				}
			}
			else
			{
				flag = true;
			}
		}
		if (flag)
		{
			SendAck(character);
		}
	}

	private void SendAck(ICharacter character)
	{
		((AbstractMessageHandler<MarketSendMessage>)(object)this).Send(character, (MessageDataFiller<MarketSendMessage>)delegate(MarketSendMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Character = ((IEntity)character).Identity;
			x.Credits = 0;
			x.ItemLowId = 0;
			x.ContainerType = 0;
			x.Placement = 0;
			x.StatusCode = 1009;
			x.Items = new List<MarketSendItemEntry>();
		}, false);
	}

	private void SendFailure(ICharacter character, string text)
	{
		if (string.Equals(text, "Cannot Sell Container Backpack Nodrop Unique items", StringComparison.Ordinal))
		{
			SendFormatFeedbackDialog(character, text);
		}
		else
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, string.Format(CultureInfo.InvariantCulture, "GMI: {0}", text), 0, 0);
		}
	}

	private void SendFormatFeedbackDialog(ICharacter character, string text)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		if (character != null && ((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null)
		{
			IZoneClient client = ((IDynel)character).Controller.Client;
			FormatFeedbackMessage val = new FormatFeedbackMessage
			{
				Identity = ((IEntity)character).Identity,
				Unknown = 1,
				Unknown1 = 0,
				FormattedMessage = "~&!!!\":!!!)<sH" + text,
				Unknown2 = 0
			};
			Identity identity = ((IEntity)character).Identity;
			client.SendCompressed((MessageBody)val, ((Identity)(ref identity)).Instance);
		}
	}
}
