using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class BuffMessageHandler : BaseMessageHandler<BuffMessage, BuffMessageHandler>
{
	public void SendAddNanoBuff(ICharacter character, int nanoId, bool announceToPlayfield = true)
	{
		((AbstractMessageHandler<BuffMessage>)(object)this).Send(character, AddNanoBuffFiller(character, nanoId), announceToPlayfield);
	}

	public void SendRemoveNanoBuff(ICharacter character, int nanoId)
	{
		((AbstractMessageHandler<BuffMessage>)(object)this).Send(character, RemoveNanoBuffFiller(character, nanoId), false);
	}

	private MessageDataFiller<BuffMessage> AddNanoBuffFiller(ICharacter character, int nanoId)
	{
		return delegate(BuffMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Action = 0;
			Identity nanoProgram = default(Identity);
			Identity identity = ((IEntity)character).Identity;
			((Identity)(ref nanoProgram)).Type = (IdentityType)((Identity)(ref identity)).Instance;
			((Identity)(ref nanoProgram)).Instance = nanoId;
			x.NanoProgram = nanoProgram;
		};
	}

	private MessageDataFiller<BuffMessage> RemoveNanoBuffFiller(ICharacter character, int nanoId)
	{
		return delegate(BuffMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Action = 0;
			Identity nanoProgram = default(Identity);
			((Identity)(ref nanoProgram)).Type = (IdentityType)53019;
			((Identity)(ref nanoProgram)).Instance = nanoId;
			x.NanoProgram = nanoProgram;
		};
	}
}
