using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.InternalMessages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class CharDCMoveMessageHandler : BaseMessageHandler<CharDCMoveMessage, CharDCMoveMessageHandler>
{
	public CharDCMoveMessageHandler()
	{
		base.UpdateCharacterStatsOnReceive = true;
	}

	protected override void Read(CharDCMoveMessage message, IZoneClient client)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Expected O, but got Unknown
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Expected O, but got Unknown
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Expected O, but got Unknown
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Expected O, but got Unknown
		if (!((IInstancedEntity)client.Controller.Character).DoNotDoTimers)
		{
			byte moveType = message.MoveType;
			byte b = NormalizeMoveTypeForServer(moveType, client);
			Quaternion val = new Quaternion((double)message.Heading.X, (double)message.Heading.Y, (double)message.Heading.Z, (double)message.Heading.W);
			Coordinate val2 = new Coordinate(message.Coordinates);
			int unknown = message.Unknown1;
			float auxA = message.AuxA;
			float auxB = message.AuxB;
			((IClient)client).Server.Info((IClient)(object)client, "CharDCMove moveTypeRaw={0} moveTypeNormalized={1} coords={2} tick={3} auxA={4} auxB={5}", new object[6] { moveType, b, val2, unknown, auxA, auxB });
			client.Controller.Move((int)b, val2, val);
			ShadowlandsGardenSaveRuntimeService.TryApplyWhenOnSavePad(client.Controller.Character, "CharDCMove");
			CharDCMoveMessage body = new CharDCMoveMessage
			{
				Identity = ((IEntity)client.Controller.Character).Identity,
				Unknown = 0,
				MoveType = moveType,
				Heading = new Quaternion
				{
					X = val.xf,
					Y = val.yf,
					Z = val.zf,
					W = val.wf
				},
				Coordinates = new Vector3
				{
					X = val2.x,
					Y = val2.y,
					Z = val2.z
				},
				Unknown1 = unknown,
				AuxA = auxA,
				AuxB = auxB
			};
			((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)new IMSendAOtomationMessageToPlayfield
			{
				Body = (MessageBody)(object)body
			});
		}
	}

	private static byte NormalizeMoveTypeForServer(byte moveType, IZoneClient client)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Invalid comparison between Unknown and I4
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Invalid comparison between Unknown and I4
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Invalid comparison between Unknown and I4
		switch (moveType)
		{
		case 36:
			return 37;
		case 43:
			return 22;
		case 22:
			if ((int)client.Controller.Character.MoveMode == 8 || (int)client.Controller.Character.MoveMode == 11 || (int)client.Controller.Character.MoveMode == 12)
			{
				return 37;
			}
			return moveType;
		default:
			return moveType;
		}
	}
}
