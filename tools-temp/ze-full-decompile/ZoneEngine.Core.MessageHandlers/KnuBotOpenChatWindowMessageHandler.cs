using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.Controllers;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class KnuBotOpenChatWindowMessageHandler : BaseMessageHandler<KnuBotOpenChatWindowMessage, KnuBotOpenChatWindowMessageHandler>
{
	public override void Receive(MessageWrapper<KnuBotOpenChatWindowMessage> messageWrapper)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		if (messageWrapper == null || messageWrapper.MessageBody == null || messageWrapper.Client == null || messageWrapper.Client.Controller == null || messageWrapper.Client.Controller.Character == null)
		{
			return;
		}
		ICharacter character = messageWrapper.Client.Controller.Character;
		Identity target = messageWrapper.MessageBody.Target;
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)character).Playfield).Identity, target);
		if (@object == null)
		{
			LogUtil.Debug((DebugInfoDetail)4096, "KnuBotOpenChatWindow inbound: NPC not found " + ((Identity)(ref target)).ToString(true));
		}
		else if (!ContentDrivenNpcDialogueRouter.TryStartDialogueForTarget(character, target))
		{
			if (((IDynel)@object).Controller is NPCController { KnuBot: not null } nPCController)
			{
				((IClient)messageWrapper.Client).Server.Info((IClient)(object)messageWrapper.Client, "KnuBotOpenChatWindow inbound player={0} npc={1}", new object[2]
				{
					((IEntity)character).Identity,
					((IEntity)@object).Identity
				});
				nPCController.FaceDialoguePartner(character);
				nPCController.KnuBot.Character = new WeakReference<ICharacter>((ICharacter)null);
				nPCController.KnuBot.StartDialog(character);
			}
			else
			{
				LogUtil.Debug((DebugInfoDetail)4096, "KnuBotOpenChatWindow inbound: no KnuBot on " + ((INamedEntity)@object).Name);
			}
		}
	}

	public void Send(ICharacter character, Identity knubotTarget)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		Send(character, knubotTarget, 1);
	}

	public void Send(ICharacter character, Identity knubotTarget, int unknown2)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<KnuBotOpenChatWindowMessage>)(object)this).Send(character, KnuBotOpenWindow(character, knubotTarget, unknown2), false);
	}

	private MessageDataFiller<KnuBotOpenChatWindowMessage> KnuBotOpenWindow(ICharacter character, Identity knubotTarget, int unknown2)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return delegate(KnuBotOpenChatWindowMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Target = knubotTarget;
			x.Unknown1 = 2;
			x.Unknown2 = unknown2;
		};
	}
}
