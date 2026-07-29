using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class PlayfieldAnarchyFMessageHandler : BaseMessageHandler<PlayfieldAnarchyFMessage, PlayfieldAnarchyFMessageHandler>
{
	private const IdentityType CapturedPrivateCityPlayfieldProxyType = 51102;

	private const IdentityType CapturedMissionBuildingType = 51103;

	private const int CapturedPrivateCityBuildingInstance = 6010;

	private const int CapturedMissionBuildingInstance = 14079424;

	private const int CapturedPrivateCityOrganizationInstance = 1370122;

	private const int CapturedMontroyalPrivateCityInstance = 1196045;

	private const int CapturedOwnedMontroyalPrivateCityInstance = 1196034;

	private const int CapturedMontroyalPrivateCityBuildingInstance = 5002;

	public void Send(ICharacter character)
	{
		((AbstractMessageHandler<PlayfieldAnarchyFMessage>)(object)this).Send(character, Filler(character), false);
	}

	private static MessageDataFiller<PlayfieldAnarchyFMessage> Filler(ICharacter character)
	{
		return delegate(PlayfieldAnarchyFMessage x)
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Expected O, but got Unknown
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0103: Unknown result type (might be due to invalid IL or missing references)
			//IL_0108: Unknown result type (might be due to invalid IL or missing references)
			//IL_0127: Unknown result type (might be due to invalid IL or missing references)
			//IL_012c: Unknown result type (might be due to invalid IL or missing references)
			//IL_014a: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0206: Unknown result type (might be due to invalid IL or missing references)
			//IL_0171: Unknown result type (might be due to invalid IL or missing references)
			//IL_0176: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_021a: Unknown result type (might be due to invalid IL or missing references)
			//IL_023a: Unknown result type (might be due to invalid IL or missing references)
			//IL_023f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0252: Unknown result type (might be due to invalid IL or missing references)
			//IL_0277: Unknown result type (might be due to invalid IL or missing references)
			//IL_027c: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
			Identity val = default(Identity);
			((Identity)(ref val)).Type = (IdentityType)40016;
			Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			((Identity)(ref val)).Instance = ((Identity)(ref identity)).Instance;
			((N3Message)x).Identity = val;
			Coordinate val2 = ((IDynel)character).Coordinates();
			x.CharacterCoordinates = new Vector3
			{
				X = val2.x,
				Y = val2.y,
				Z = val2.z
			};
			val = default(Identity);
			((Identity)(ref val)).Type = (IdentityType)51100;
			identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			((Identity)(ref val)).Instance = ((Identity)(ref identity)).Instance;
			x.PlayfieldId1 = val;
			val = default(Identity);
			((Identity)(ref val)).Type = (IdentityType)40016;
			identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			((Identity)(ref val)).Instance = ((Identity)(ref identity)).Instance;
			x.PlayfieldId2 = val;
			val = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			x.PlayfieldX = ZoneEngine.Core.Playfields.Playfields.GetPlayfieldX(((Identity)(ref val)).Instance);
			val = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			x.PlayfieldZ = ZoneEngine.Core.Playfields.Playfields.GetPlayfieldZ(((Identity)(ref val)).Instance);
			val = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			if (MissionInstanceService.IsMissionInstancePlayfield(((Identity)(ref val)).Instance))
			{
				val = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
				int instance = ((Identity)(ref val)).Instance;
				byte[] array = MissionInstanceShapeCatalog.GetGeneratorPayload(instance);
				int instance2 = MissionInstanceShapeCatalog.GetBuildingInstance(array);
				if (array == null || array.Length == 0)
				{
					array = CreateCapturedMissionGeneratorPayload();
					instance2 = 14079424;
				}
				val = default(Identity);
				((Identity)(ref val)).Type = (IdentityType)51103;
				((Identity)(ref val)).Instance = instance2;
				x.PlayfieldId1 = val;
				x.Unknown3 = 0;
				x.Unknown4 = 0;
				x.GeneratorPayload = array;
			}
			else if (Playfield.IsPrivateCityPlayfieldCandidate(((IEntity)((IInstancedEntity)character).Playfield).Identity))
			{
				val = default(Identity);
				((Identity)(ref val)).Type = (IdentityType)51102;
				identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
				((Identity)(ref val)).Instance = ResolvePrivateCityBuildingInstance(((Identity)(ref identity)).Instance);
				x.PlayfieldId1 = val;
				x.Unknown4 = ResolvePrivateCityOrganizationInstance(character);
				val = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
				x.GeneratorPayload = CreateCapturedPrivateCityGeneratorPayload(((Identity)(ref val)).Instance);
			}
			IEnumerable<Vendor> all = Pool.Instance.GetAll<Vendor>(((IEntity)((IInstancedEntity)character).Playfield).Identity, 51035);
		};
	}

	private static int ResolvePrivateCityOrganizationInstance(ICharacter character)
	{
		int num = ResolveCharacterOrganizationInstance(character);
		return (num > 0) ? num : 1370122;
	}

	private static int ResolveCharacterOrganizationInstance(ICharacter character)
	{
		if (character == null)
		{
			return 0;
		}
		uint baseValue = ((IStats)character).Stats[(StatIds)5].BaseValue;
		if (baseValue != 0 && baseValue <= int.MaxValue)
		{
			return (int)baseValue;
		}
		return ((IStats)character).Stats[(StatIds)5].Value;
	}

	private static int ResolvePrivateCityBuildingInstance(int playfieldInstance)
	{
		return IsCapturedMontroyalPrivateCityInstance(playfieldInstance) ? 5002 : 6010;
	}

	private static byte[] CreateCapturedPrivateCityGeneratorPayload(int playfieldId)
	{
		byte[] result;
		switch (playfieldId)
		{
		case 1196034:
			return CreateCapturedOwnedMontroyalPrivateCityGeneratorPayload();
		default:
			result = CreateCapturedPrivateCityGeneratorPayload();
			break;
		case 1196045:
			result = CreateCapturedMontroyalPrivateCityGeneratorPayload();
			break;
		}
		return result;
	}

	private static bool IsCapturedMontroyalPrivateCityInstance(int playfieldInstance)
	{
		return playfieldInstance == 1196045 || playfieldInstance == 1196034;
	}

	private static byte[] CreateCapturedMissionGeneratorPayload()
	{
		return new byte[153]
		{
			0, 0, 199, 159, 0, 214, 213, 192, 0, 0,
			0, 2, 0, 3, 0, 30, 0, 30, 0, 64,
			0, 0, 1, 68, 100, 100, 100, 0, 0, 0,
			19, 0, 43, 0, 0, 21, 0, 0, 47, 0,
			1, 20, 1, 0, 0, 0, 6, 18, 1, 0,
			30, 0, 6, 22, 3, 0, 18, 0, 3, 18,
			3, 0, 20, 0, 4, 22, 2, 0, 5, 0,
			2, 22, 2, 0, 21, 0, 6, 23, 3, 0,
			5, 0, 3, 23, 3, 0, 5, 0, 8, 17,
			0, 0, 23, 0, 8, 21, 2, 0, 6, 0,
			7, 17, 0, 0, 41, 0, 7, 21, 0, 0,
			13, 0, 6, 17, 0, 0, 10, 0, 9, 18,
			1, 0, 42, 0, 5, 18, 1, 0, 23, 0,
			5, 19, 3, 0, 42, 0, 5, 20, 1, 0,
			42, 0, 3, 17, 2, 255, 255, 255, 255, 255,
			255, 255, 255
		};
	}

	private static byte[] CreateCapturedPrivateCityGeneratorPayload()
	{
		return new byte[88]
		{
			0, 0, 199, 125, 0, 0, 0, 1, 0, 0,
			0, 1, 0, 0, 0, 1, 0, 0, 0, 3,
			0, 0, 196, 24, 0, 0, 0, 1, 0, 0,
			0, 0, 0, 0, 0, 1, 0, 156, 160, 11,
			0, 0, 199, 61, 0, 0, 0, 1, 0, 0,
			0, 1, 0, 0, 0, 1, 87, 77, 248, 187,
			0, 0, 199, 72, 0, 0, 0, 1, 0, 0,
			0, 2, 0, 0, 0, 1, 16, 142, 188, 33,
			255, 255, 255, 255, 255, 255, 255, 255
		};
	}

	private static byte[] CreateCapturedMontroyalPrivateCityGeneratorPayload()
	{
		return new byte[88]
		{
			0, 0, 199, 125, 0, 0, 0, 1, 0, 0,
			0, 1, 0, 0, 0, 1, 0, 0, 0, 3,
			0, 0, 196, 24, 0, 0, 0, 1, 0, 0,
			0, 0, 0, 0, 0, 1, 0, 156, 96, 16,
			0, 0, 199, 61, 0, 0, 0, 1, 0, 0,
			0, 1, 0, 0, 0, 1, 87, 75, 132, 171,
			0, 0, 199, 72, 0, 0, 0, 1, 0, 0,
			0, 2, 0, 0, 0, 1, 16, 142, 202, 144,
			255, 255, 255, 255, 255, 255, 255, 255
		};
	}

	private static byte[] CreateCapturedOwnedMontroyalPrivateCityGeneratorPayload()
	{
		return new byte[88]
		{
			0, 0, 199, 125, 0, 0, 0, 1, 0, 0,
			0, 1, 0, 0, 0, 1, 0, 0, 0, 3,
			0, 0, 196, 24, 0, 0, 0, 1, 0, 0,
			0, 0, 0, 0, 0, 1, 0, 156, 24, 46,
			0, 0, 199, 61, 0, 0, 0, 1, 0, 0,
			0, 1, 0, 0, 0, 1, 87, 81, 83, 139,
			0, 0, 199, 72, 0, 0, 0, 1, 0, 0,
			0, 2, 0, 0, 0, 1, 16, 141, 150, 237,
			255, 255, 255, 255, 255, 255, 255, 255
		};
	}
}
