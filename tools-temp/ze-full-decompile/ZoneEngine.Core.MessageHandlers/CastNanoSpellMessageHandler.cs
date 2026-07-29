using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class CastNanoSpellMessageHandler : BaseMessageHandler<CastNanoSpellMessage, CastNanoSpellMessageHandler>
{
	public void Send(ICharacter character, int nanoId, Identity target, bool announceToPlayfield = true)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<CastNanoSpellMessage>)(object)this).Send(character, Filler(character, nanoId, target), announceToPlayfield);
	}

	public void SendPetCast(ICharacter pet, int nanoId, Identity target, bool announceToPlayfield = true)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<CastNanoSpellMessage>)(object)this).Send(pet, PetCastFiller(pet, nanoId, target), announceToPlayfield);
	}

	public void SendNpcCast(ICharacter npc, int nanoId, Identity target, bool announceToPlayfield = true)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<CastNanoSpellMessage>)(object)this).Send(npc, PetCastFiller(npc, nanoId, target), announceToPlayfield);
	}

	public void SendTriggeredSelfCast(ICharacter character, int nanoId, bool announceToPlayfield = true)
	{
		((AbstractMessageHandler<CastNanoSpellMessage>)(object)this).Send(character, TriggeredSelfCastFiller(character, nanoId), announceToPlayfield);
	}

	private MessageDataFiller<CastNanoSpellMessage> Filler(ICharacter character, int nanoId, Identity target)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return delegate(CastNanoSpellMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Caster = ((IEntity)character).Identity;
			x.Target = target;
			x.NanoId = nanoId;
			((N3Message)x).Unknown = 0;
			x.Unknown1 = 0;
		};
	}

	private MessageDataFiller<CastNanoSpellMessage> PetCastFiller(ICharacter pet, int nanoId, Identity target)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return delegate(CastNanoSpellMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)pet).Identity;
			x.Caster = Identity.None;
			x.Target = target;
			x.NanoId = nanoId;
			((N3Message)x).Unknown = 0;
			x.Unknown1 = 0;
		};
	}

	private MessageDataFiller<CastNanoSpellMessage> TriggeredSelfCastFiller(ICharacter character, int nanoId)
	{
		return delegate(CastNanoSpellMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Caster = ((IEntity)character).Identity;
			x.Target = ((IEntity)character).Identity;
			x.NanoId = nanoId;
			((N3Message)x).Unknown = 0;
			x.Unknown1 = 1;
		};
	}
}
