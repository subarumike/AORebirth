using AORebirth.Core.Components;
using AORebirth.Core.Network;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class LookAtMessageHandler : BaseMessageHandler<LookAtMessage, LookAtMessageHandler>
{
	public LookAtMessageHandler()
	{
		base.UpdateCharacterStatsOnReceive = true;
	}

	protected override void Read(LookAtMessage message, IZoneClient client)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		Identity target = message.Target;
		LogUtil.Debug((DebugInfoDetail)512, $"LookAt target={((Identity)(ref target)).ToString(true)} returnInfo={message.ReturnInfo}");
		PetCommandService.OnOwnerLookAtTarget(client.Controller.Character, message.Target);
		if (client.Controller.LookAt(message.Target))
		{
			PetCommandService.ResolveFriendlyHealTargetForSelection(client.Controller.Character, message.Target);
			if (message.ReturnInfo != 1)
			{
				BaseMessageHandler<InfoPacketMessage, CharacterInfoPacketMessageHandler>.Default.Send(client.Controller.Character, message.Target);
			}
		}
	}
}
