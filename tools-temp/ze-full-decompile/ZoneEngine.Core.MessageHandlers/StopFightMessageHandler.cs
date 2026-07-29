using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Interfaces;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class StopFightMessageHandler : BaseMessageHandler<StopFightMessage, StopFightMessageHandler>
{
	protected override void Read(StopFightMessage message, IZoneClient client)
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		((IClient)client).Server.Info((IClient)(object)client, "StopFight unknown1={0} fightingTarget={1}", new object[2]
		{
			message.Unknown1,
			((ITargetingEntity)character).FightingTarget
		});
		((ITargetingEntity)character).SetFightingTarget(Identity.None);
		ResetCombatTick(character);
		base.SendToPlayfield(character, (MessageDataFiller<StopFightMessage>)delegate(StopFightMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Unknown1 = message.Unknown1;
		});
	}

	private void ResetCombatTick(ICharacter character)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (((IInstancedEntity)character).Playfield is Playfield playfield)
		{
			playfield.ResetCombatTick(((IEntity)character).Identity);
		}
	}
}
