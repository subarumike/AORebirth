using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using Utility;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class PetCommandMessageHandler : BaseMessageHandler<PetCommandMessage, PetCommandMessageHandler>
{
	protected override void Read(PetCommandMessage message, IZoneClient client)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = ((client.Controller != null) ? client.Controller.Character : null);
		if (val != null && message != null)
		{
			int unknown = message.Unknown2;
			bool flag = message.Unknown1 == 1;
			Identity val2 = ResolvePetIdentity(message);
			Identity val3 = ResolveCommandTarget(message);
			PetCommandService.CommitHealTargetFromPacket(val, val2, val3);
			Identity val4 = val3;
			if (((Identity)(ref val4)).Instance == 0 && unknown != 12)
			{
				val4 = ((ITargetingEntity)val).SelectedTarget;
			}
			if (unknown == 12)
			{
				val4 = PetCommandService.ResolveHealCommandTarget(val, val2, val3);
				PetCommandService.SyncOwnerHealSelectedTarget(val, val4);
			}
			else if (PetCommandService.HasActiveHealCommand(val) && ((Identity)(ref val4)).Instance != 0)
			{
				PetCommandService.SyncOwnerHealSelectedTarget(val, val4);
			}
			LogUtil.Debug((DebugInfoDetail)256, $"PetCommandMessage owner={((IEntity)val).Identity} pet={val2} commandId={unknown} all={flag} target={val4} u1={message.Unknown1} u3={message.Unknown3} u4={message.Unknown4} idCount={((message.Identities != null) ? message.Identities.Length : 0)} name={message.Name ?? string.Empty}");
			PetCommandService.HandlePetCommandMessage(client, val, unknown, flag, val2, val4);
		}
	}

	private Identity ResolvePetIdentity(PetCommandMessage message)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		if (message.Identities != null && message.Identities.Length != 0 && ((Identity)(ref message.Identities[0])).Instance != 0)
		{
			return message.Identities[0];
		}
		return Identity.None;
	}

	private Identity ResolveCommandTarget(PetCommandMessage message)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		if (message.Identities != null && message.Identities.Length > 1 && ((Identity)(ref message.Identities[1])).Instance != 0)
		{
			return message.Identities[1];
		}
		return Identity.None;
	}
}
