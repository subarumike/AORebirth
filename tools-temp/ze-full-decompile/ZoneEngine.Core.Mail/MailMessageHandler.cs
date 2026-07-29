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
using ZoneEngine.Core.GMI;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.Mail;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class MailMessageHandler : BaseMessageHandler<MailMessage, MailMessageHandler>
{
	public MailMessageHandler()
	{
		base.UpdateCharacterStatsOnReceive = false;
	}

	protected override void Read(MailMessage message, IZoneClient client)
	{
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Expected I4, but got Unknown
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Expected I4, but got Unknown
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Expected I4, but got Unknown
		ICharacter character = client.Controller.Character;
		try
		{
			GmiRuntimeService.ProcessPendingWithdrawals(character);
		}
		catch (Exception ex)
		{
			((IClient)client).Server.Info((IClient)(object)client, "GMI ProcessPendingWithdrawals during Mail failed: {0}", new object[1] { ex.Message });
		}
		((IClient)client).Server.Info((IClient)(object)client, "Mail action={0}({1}) requestId={2} recipient={3} subject={4} credits={5} express={6}", new object[7]
		{
			message.Action,
			(int)message.Action,
			message.RequestedMailId,
			message.Recipient,
			message.Subject,
			message.Credits,
			message.ExpressFlag
		});
		MailAction action = message.Action;
		MailAction val = action;
		switch (val - 1)
		{
		case 0:
			InventoryContainerRuntimeService.Default.PublishMailBlockedContainerLinks(character);
			if (message.RequestedMailId == 0)
			{
				SendMailboxList(character);
			}
			else
			{
				SendMailDetail(character, message.RequestedMailId);
			}
			break;
		case 5:
			HandleSendMail(character, message);
			break;
		case 2:
			HandleTakeAll(character, message.RequestedMailId);
			break;
		case 4:
			HandleDelete(character, message.RequestedMailId);
			break;
		case 6:
			HandleReturnToSender(character, message.RequestedMailId);
			break;
		default:
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, string.Format(CultureInfo.InvariantCulture, "Mail action {0} is not implemented yet.", (int)message.Action), 0, 0);
			break;
		}
	}

	private void HandleSendMail(ICharacter character, MailMessage message)
	{
		if (!MailRuntimeService.TrySendMail(character, message, out var failureReason, out var mailId))
		{
			SendMailFailureFeedback(character, failureReason ?? "Mail send failed.");
			return;
		}
		((AbstractMessageHandler<MailMessage>)(object)this).Send(character, (MessageDataFiller<MailMessage>)delegate(MailMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Action = (MailAction)8;
			x.EchoAction = 6;
			x.Unknown1 = 0;
			x.MailId = mailId;
			x.Unknown2 = 0;
		}, false);
		string arg = ((message.ExpressFlag != 0) ? "Express" : "Standard");
		string text = string.Empty;
		if (message.Credits > 0)
		{
			text = string.Format(CultureInfo.InvariantCulture, " Attached {0} credits.", message.Credits);
		}
		else if (message.Credits < 0)
		{
			text = string.Format(CultureInfo.InvariantCulture, " COD {0} credits.", -message.Credits);
		}
		if (message.ItemField1 != 0 || message.ItemField2 != 0)
		{
			text += " Item attached.";
		}
		BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, string.Format(CultureInfo.InvariantCulture, "Mail sent to {0} ({1}).{2}", message.Recipient, arg, text), 0, 0);
	}

	private void HandleTakeAll(ICharacter character, ulong mailId)
	{
		if (!MailRuntimeService.TryTakeAll(character, mailId, out var failureReason, out var updated))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, failureReason ?? "Take All failed.", 0, 0);
			return;
		}
		MailRuntimeService.SyncUnreadMailEnvelope(character);
		((AbstractMessageHandler<MailMessage>)(object)this).Send(character, (MessageDataFiller<MailMessage>)delegate(MailMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Action = (MailAction)2;
			x.Detail = updated;
		}, false);
		SendMailFlagsUpdate(character, mailId);
		SendMailboxList(character);
		BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Mail attachments taken.", 0, 0);
	}

	private void HandleDelete(ICharacter character, ulong mailId)
	{
		if (!MailRuntimeService.TryDeleteMail(((INamedEntity)character).Name, mailId, out var failureReason))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, failureReason ?? "Delete failed.", 0, 0);
			return;
		}
		MailRuntimeService.SyncUnreadMailEnvelope(character);
		SendMailboxList(character);
		BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Mail deleted.", 0, 0);
	}

	private void HandleReturnToSender(ICharacter character, ulong mailId)
	{
		if (!MailRuntimeService.TryReturnToSender(character, mailId, out var failureReason))
		{
			string text = failureReason ?? "Return to sender failed.";
			if (((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null)
			{
				((IClient)((IDynel)character).Controller.Client).Server.Info((IClient)(object)((IDynel)character).Controller.Client, "Mail ReturnToSender FAILED id={0}: {1}", new object[2] { mailId, text });
			}
			SendFormatFeedbackDialog(character, text);
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, text, 0, 0);
			SendMailboxList(character);
		}
		else
		{
			SendMailboxList(character);
			string text2 = ((!string.IsNullOrEmpty(failureReason)) ? failureReason : "Mail returned to sender.");
			if (((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null)
			{
				((IClient)((IDynel)character).Controller.Client).Server.Info((IClient)(object)((IDynel)character).Controller.Client, "Mail ReturnToSender OK id={0}: {1}", new object[2] { mailId, text2 });
			}
			SendFormatFeedbackDialog(character, text2);
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, text2, 0, 0);
		}
	}

	private void SendMailFailureFeedback(ICharacter character, string failure)
	{
		if (string.Equals(failure, "You can not send nodrop items through the mail system.", StringComparison.Ordinal) || string.Equals(failure, "You can not send container items through the mail system.", StringComparison.Ordinal))
		{
			SendFormatFeedbackDialog(character, failure);
		}
		else
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, failure, 0, 0);
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

	private void SendMailboxList(ICharacter character)
	{
		MailRuntimeService.SyncUnreadMailEnvelope(character);
		IList<MailListEntry> entries = MailRuntimeService.BuildMailboxListEntries(((INamedEntity)character).Name);
		((AbstractMessageHandler<MailMessage>)(object)this).Send(character, (MessageDataFiller<MailMessage>)delegate(MailMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Action = (MailAction)0;
			x.Entries = entries;
		}, false);
	}

	private void SendMailDetail(ICharacter character, ulong mailId)
	{
		if (!MailRuntimeService.TryBuildMailDetail(((INamedEntity)character).Name, mailId, out var detail))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Mail not found.", 0, 0);
			return;
		}
		MailRuntimeService.SyncUnreadMailEnvelope(character);
		((AbstractMessageHandler<MailMessage>)(object)this).Send(character, (MessageDataFiller<MailMessage>)delegate(MailMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Action = (MailAction)2;
			x.Detail = detail;
		}, false);
		SendMailFlagsUpdate(character, mailId);
		SendMailboxList(character);
	}

	private void SendMailFlagsUpdate(ICharacter character, ulong mailId)
	{
		if (MailRuntimeService.TryGetMailFlagsUpdate(((INamedEntity)character).Name, mailId, out var flags))
		{
			((AbstractMessageHandler<MailMessage>)(object)this).Send(character, (MessageDataFiller<MailMessage>)delegate(MailMessage x)
			{
				//IL_0008: Unknown result type (might be due to invalid IL or missing references)
				((N3Message)x).Identity = ((IEntity)character).Identity;
				((N3Message)x).Unknown = 0;
				x.Action = (MailAction)4;
				x.RequestedMailId = mailId;
				x.MailFlagsUpdate = flags;
			}, false);
		}
	}
}
