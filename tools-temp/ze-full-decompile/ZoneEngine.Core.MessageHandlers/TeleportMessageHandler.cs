using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class TeleportMessageHandler : BaseMessageHandler<N3TeleportMessage, TeleportMessageHandler>
{
	private const IdentityType LivePlayfieldProxyType = 51102;

	private const IdentityType LiveMissionBuildingType = 51103;

	private const int CapturedMissionBuildingInstance = 14079424;

	private const int CapturedMissionTeleportPlayfield2Type = 100002;

	private const int CapturedMissionTeleportPlayfield2Instance = 1;

	private const int CapturedPrivateCityBuildingInstance = 6010;

	private const int CapturedPrivateCityOrganizationInstance = 1370122;

	private const int CapturedPrivateCityTeleportPlayfield2Type = 100009;

	private const int CapturedPrivateCityTeleportPlayfield2Instance = -1073735814;

	public void Send(ICharacter character, Vector3 destination, Quaternion heading, Identity playfield)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<N3TeleportMessage>)(object)this).Send(character, NormalTeleport(character, destination, heading, playfield), false);
	}

	public void SendLocal(ICharacter character, Vector3 destination, Quaternion heading)
	{
		((AbstractMessageHandler<N3TeleportMessage>)(object)this).Send(character, LocalTeleport(character, destination, heading), false);
	}

	public void SendCapturedGatewayTransfer(ICharacter character, Vector3 envelopeDestination, Vector3 landingDestination, Quaternion heading, int destinationPlayfieldId)
	{
		((AbstractMessageHandler<N3TeleportMessage>)(object)this).Send(character, CapturedGatewayTransfer(character, envelopeDestination, landingDestination, heading, destinationPlayfieldId), false);
	}

	public void SendTeleportProxy(ICharacter character, Vector3 destination, Quaternion heading, int playfield, Identity playfieldInstance, int GS, int SG, Identity destinationidentity)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<N3TeleportMessage>)(object)this).Send(character, ProxyTeleport(character, destination, heading, playfield, playfieldInstance, GS, SG, destinationidentity), false);
	}

	private MessageDataFiller<N3TeleportMessage> ProxyTeleport(ICharacter character, Vector3 destination, Quaternion heading, int playfield, Identity playfieldInstance, int GS, int SG, Identity destinationidentity)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		return delegate(N3TeleportMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Expected O, but got Unknown
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Expected O, but got Unknown
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0131: Unknown result type (might be due to invalid IL or missing references)
			//IL_0152: Unknown result type (might be due to invalid IL or missing references)
			//IL_0119: Unknown result type (might be due to invalid IL or missing references)
			//IL_011e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0121: Unknown result type (might be due to invalid IL or missing references)
			//IL_0126: Invalid comparison between I4 and Unknown
			//IL_015b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0128: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Destination = new Vector3
			{
				X = (float)destination.x,
				Y = (float)destination.y,
				Z = (float)destination.z
			};
			x.Heading = new Quaternion
			{
				X = (float)heading.x,
				Y = (float)heading.y,
				Z = (float)heading.z,
				W = (float)heading.w
			};
			x.Unknown1 = 97;
			x.Playfield = playfieldInstance;
			x.GameServerId = GS;
			x.SgId = SG;
			int num = playfield;
			Identity val = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			Identity changePlayfield;
			if (num == ((Identity)(ref val)).Instance)
			{
				val = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
				if (51101 == (int)((Identity)(ref val)).Type)
				{
					changePlayfield = Identity.None;
					goto IL_0153;
				}
			}
			val = default(Identity);
			((Identity)(ref val)).Type = (IdentityType)40016;
			((Identity)(ref val)).Instance = playfield;
			changePlayfield = val;
			goto IL_0153;
			IL_0153:
			x.ChangePlayfield = changePlayfield;
			x.Playfield2 = destinationidentity;
			x.Payload = BuildDestinationPayload(destination);
		};
	}

	private MessageDataFiller<N3TeleportMessage> NormalTeleport(ICharacter character, Vector3 destination, Quaternion heading, Identity playfield)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		return delegate(N3TeleportMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Expected O, but got Unknown
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Expected O, but got Unknown
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0188: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			//IL_0137: Unknown result type (might be due to invalid IL or missing references)
			//IL_0151: Unknown result type (might be due to invalid IL or missing references)
			//IL_016d: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0196: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_0202: Unknown result type (might be due to invalid IL or missing references)
			//IL_0225: Unknown result type (might be due to invalid IL or missing references)
			//IL_024b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0264: Unknown result type (might be due to invalid IL or missing references)
			//IL_0279: Unknown result type (might be due to invalid IL or missing references)
			//IL_0299: Unknown result type (might be due to invalid IL or missing references)
			//IL_0270: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Destination = new Vector3
			{
				X = (float)destination.x,
				Y = (float)destination.y,
				Z = (float)destination.z
			};
			x.Heading = new Quaternion
			{
				X = (float)heading.x,
				Y = (float)heading.y,
				Z = (float)heading.z,
				W = (float)heading.w
			};
			x.Unknown1 = 97;
			Identity val;
			if (IsMissionInstanceDestination(playfield))
			{
				val = default(Identity);
				((Identity)(ref val)).Type = (IdentityType)51103;
				((Identity)(ref val)).Instance = 14079424;
				x.Playfield = val;
				x.GameServerId = 0;
				x.SgId = 0;
				val = default(Identity);
				((Identity)(ref val)).Type = (IdentityType)40016;
				((Identity)(ref val)).Instance = ((Identity)(ref playfield)).Instance;
				x.ChangePlayfield = val;
				x.Unknown4 = 0;
				x.Unknown5 = 0;
				val = default(Identity);
				((Identity)(ref val)).Type = (IdentityType)100002;
				((Identity)(ref val)).Instance = 1;
				x.Playfield2 = val;
				x.Payload = new byte[0];
			}
			else
			{
				Identity playfield2;
				if (!IsPrivateCityDestination(playfield))
				{
					val = default(Identity);
					((Identity)(ref val)).Type = (IdentityType)51102;
					((Identity)(ref val)).Instance = ((Identity)(ref playfield)).Instance;
					playfield2 = val;
				}
				else
				{
					val = default(Identity);
					((Identity)(ref val)).Type = (IdentityType)51102;
					((Identity)(ref val)).Instance = 6010;
					playfield2 = val;
				}
				x.Playfield = playfield2;
				x.GameServerId = ((!IsPrivateCityDestination(playfield)) ? 1 : 0);
				x.SgId = (IsPrivateCityDestination(playfield) ? ResolvePrivateCityOrganizationInstance(character) : 0);
				val = default(Identity);
				((Identity)(ref val)).Type = (IdentityType)40016;
				((Identity)(ref val)).Instance = ((Identity)(ref playfield)).Instance;
				x.ChangePlayfield = val;
				x.Unknown4 = 0;
				x.Unknown5 = 0;
				Identity playfield3;
				if (!IsPrivateCityDestination(playfield))
				{
					playfield3 = Identity.None;
				}
				else
				{
					val = default(Identity);
					((Identity)(ref val)).Type = (IdentityType)100009;
					((Identity)(ref val)).Instance = -1073735814;
					playfield3 = val;
				}
				x.Playfield2 = playfield3;
				x.Payload = (IsPrivateCityDestination(playfield) ? new byte[0] : BuildDestinationPayload(destination));
			}
		};
	}

	private MessageDataFiller<N3TeleportMessage> LocalTeleport(ICharacter character, Vector3 destination, Quaternion heading)
	{
		return delegate(N3TeleportMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Expected O, but got Unknown
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Expected O, but got Unknown
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_010f: Unknown result type (might be due to invalid IL or missing references)
			//IL_012b: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Destination = new Vector3
			{
				X = (float)destination.x,
				Y = (float)destination.y,
				Z = (float)destination.z
			};
			x.Heading = new Quaternion
			{
				X = (float)heading.x,
				Y = (float)heading.y,
				Z = (float)heading.z,
				W = (float)heading.w
			};
			x.Unknown1 = 97;
			Identity playfield = default(Identity);
			((Identity)(ref playfield)).Type = (IdentityType)51102;
			Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			((Identity)(ref playfield)).Instance = ((Identity)(ref identity)).Instance;
			x.Playfield = playfield;
			x.GameServerId = 1;
			x.SgId = 0;
			x.ChangePlayfield = Identity.None;
			x.Unknown4 = 0;
			x.Unknown5 = 0;
			x.Playfield2 = Identity.None;
			x.Payload = BuildDestinationPayload(destination);
		};
	}

	private MessageDataFiller<N3TeleportMessage> CapturedGatewayTransfer(ICharacter character, Vector3 envelopeDestination, Vector3 landingDestination, Quaternion heading, int destinationPlayfieldId)
	{
		return delegate(N3TeleportMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Expected O, but got Unknown
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Expected O, but got Unknown
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0120: Unknown result type (might be due to invalid IL or missing references)
			//IL_0138: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Destination = new Vector3
			{
				X = (float)envelopeDestination.x,
				Y = (float)envelopeDestination.y,
				Z = (float)envelopeDestination.z
			};
			x.Heading = new Quaternion
			{
				X = (float)heading.x,
				Y = (float)heading.y,
				Z = (float)heading.z,
				W = (float)heading.w
			};
			x.Unknown1 = 97;
			Identity val = default(Identity);
			((Identity)(ref val)).Type = (IdentityType)51100;
			((Identity)(ref val)).Instance = destinationPlayfieldId;
			x.Playfield = val;
			x.GameServerId = 0;
			x.SgId = 0;
			val = default(Identity);
			((Identity)(ref val)).Type = (IdentityType)40016;
			((Identity)(ref val)).Instance = destinationPlayfieldId;
			x.ChangePlayfield = val;
			x.Unknown4 = 0;
			x.Unknown5 = 0;
			x.Playfield2 = Identity.None;
			x.Payload = BuildDestinationPayload(landingDestination);
		};
	}

	private static byte[] BuildDestinationPayload(Vector3 destination)
	{
		byte[] array = new byte[12];
		WriteSingle(array, 0, (float)destination.x);
		WriteSingle(array, 4, (float)destination.y);
		WriteSingle(array, 8, (float)destination.z);
		return array;
	}

	private static void WriteSingle(byte[] buffer, int offset, float value)
	{
		byte[] bytes = BitConverter.GetBytes(value);
		Array.Reverse(bytes);
		Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
	}

	private static bool IsPrivateCityDestination(Identity playfield)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		Identity playfieldIdentity = default(Identity);
		((Identity)(ref playfieldIdentity)).Type = (IdentityType)40016;
		((Identity)(ref playfieldIdentity)).Instance = ((Identity)(ref playfield)).Instance;
		return Playfield.IsPrivateCityPlayfieldCandidate(playfieldIdentity);
	}

	private static bool IsMissionInstanceDestination(Identity playfield)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Invalid comparison between Unknown and I4
		if ((int)((Identity)(ref playfield)).Type != 51101 && (int)((Identity)(ref playfield)).Type != 40016)
		{
			return false;
		}
		return MissionInstanceService.IsMissionInstancePlayfield(((Identity)(ref playfield)).Instance);
	}

	private static int ResolvePrivateCityOrganizationInstance(ICharacter character)
	{
		int value = ((IStats)character).Stats[(StatIds)5].Value;
		return (value > 0) ? value : 1370122;
	}
}
