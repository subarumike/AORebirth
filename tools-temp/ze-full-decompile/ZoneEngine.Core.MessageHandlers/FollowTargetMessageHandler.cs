using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Enums;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.InternalMessages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class FollowTargetMessageHandler : BaseMessageHandler<FollowTargetMessage, FollowTargetMessageHandler>
{
	public FollowTargetMessageHandler()
	{
		base.UpdateCharacterStatsOnReceive = true;
	}

	protected override void Read(FollowTargetMessage followTargetMessage, IZoneClient client)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Expected O, but got Unknown
		FollowTargetMessage val = new FollowTargetMessage
		{
			Identity = ((IEntity)client.Controller.Character).Identity,
			Unknown = 0
		};
		FollowInfo info = followTargetMessage.Info;
		FollowTargetInfo val2 = (FollowTargetInfo)(object)((info is FollowTargetInfo) ? info : null);
		if (val2 != null)
		{
			val.Info = (FollowInfo)new FollowTargetInfo
			{
				MoveType = 0,
				Target = val2.Target,
				Dummy = 64,
				Dummy1 = 536870912
			};
		}
		((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)new IMSendAOtomationMessageToPlayfield
		{
			Body = (MessageBody)(object)val
		});
	}

	public void Send(ICharacter character, Vector3 stopPosition)
	{
		SendOfficialPositionStop(character, stopPosition);
	}

	public void SendOfficialPositionStop(ICharacter character, Vector3 stopPosition)
	{
		base.SendToPlayfield(character, FillerOfficialPositionStop(character, stopPosition));
	}

	private MessageDataFiller<FollowTargetMessage> FillerOfficialPositionStop(ICharacter character, Vector3 stopPosition)
	{
		return delegate(FollowTargetMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Expected O, but got Unknown
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Info = (FollowInfo)new FollowPositionInfo
			{
				MoveType = 25,
				Unknown1 = 0,
				Unknown2 = 0,
				Unknown3 = 1073741824,
				Coordinates = stopPosition,
				Unknown4 = 0
			};
		};
	}

	public void Send(ICharacter character, Identity toFollow)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		base.SendToPlayfield(character, FillerFollowTarget(character, toFollow));
	}

	public void Send(ICharacter character, Vector3 start, Vector3 end)
	{
		base.SendToPlayfield(character, FillerCoordinates(character, start, end));
	}

	private MessageDataFiller<FollowTargetMessage> FillerFollowTarget(ICharacter character, Identity toFollow)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return delegate(FollowTargetMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Expected O, but got Unknown
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Info = (FollowInfo)new FollowTargetInfo
			{
				MoveType = 0,
				Target = toFollow,
				Dummy = 64,
				Dummy1 = 536870912
			};
			((N3Message)x).Unknown = 0;
		};
	}

	private MessageDataFiller<FollowTargetMessage> FillerCoordinates(ICharacter character, Vector3 start, Vector3 end)
	{
		return delegate(FollowTargetMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Expected I4, but got Unknown
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Expected O, but got Unknown
			((N3Message)x).Identity = ((IEntity)character).Identity;
			byte b = 0;
			MoveModes moveMode = character.MoveMode;
			MoveModes val = moveMode;
			b = (val - 2) switch
			{
				3 => 27, 
				1 => 25, 
				_ => 24, 
			};
			x.Info = (FollowInfo)new FollowCoordinateInfo
			{
				CurrentCoordinates = start,
				EndCoordinates = end,
				CoordinateCount = 2,
				MoveMode = b,
				FollowInfoType = 1
			};
			((N3Message)x).Unknown = 0;
		};
	}
}
