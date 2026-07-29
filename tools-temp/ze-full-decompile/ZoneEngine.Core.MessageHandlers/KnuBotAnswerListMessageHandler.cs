using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
internal class KnuBotAnswerListMessageHandler : BaseMessageHandler<KnuBotAnswerListMessage, KnuBotAnswerListMessageHandler>
{
	public void Send(ICharacter character, Identity knubotTarget, string[] choices)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<KnuBotAnswerListMessage>)(object)this).Send(character, KnuBotAnswerList(character, knubotTarget, choices), false);
	}

	private MessageDataFiller<KnuBotAnswerListMessage> KnuBotAnswerList(ICharacter character, Identity knubotTarget, string[] choices)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return delegate(KnuBotAnswerListMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Expected O, but got Unknown
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Target = knubotTarget;
			List<KnuBotDialogOption> list = new List<KnuBotDialogOption>();
			string[] array = choices;
			foreach (string text in array)
			{
				list.Add(new KnuBotDialogOption
				{
					Text = text
				});
			}
			x.DialogOptions = list.ToArray();
			x.Unknown1 = 2;
		};
	}
}
