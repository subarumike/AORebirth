using System.Collections.Generic;
using System.Linq;
using System.Text;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Database.Dao;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

public sealed class CityControllerInteractionHandler
{
	public static readonly CityControllerInteractionHandler Default = new CityControllerInteractionHandler();

	private const int CapturedPrivateCityOrganizationInstance = 1370122;

	private const int CapturedOwnedPrivateCityOrganizationInstance = 1970177;

	private const int CapturedCityControllerInfoIdentityType = 50201;

	private const int CapturedCityControllerInfoIdentityInstance = 49152;

	private const int CapturedCityControllerBuildingType = 51102;

	private const int CapturedCityControllerBuildingInstance = 5002;

	private const int CapturedNonOrgCityControllerBuildingInstance = 6010;

	private const int CapturedMontroyalPrivateCityInstance = 1196045;

	private const int CapturedOwnedMontroyalPrivateCityInstance = 1196034;

	private const string CapturedCityControllerOwnedOrganizationText = "Est. 2024";

	private const string CapturedCityControllerNoOrganizationText = "no organization";

	private const string CapturedCityControllerNonOrgText = "Identifies As Clan";

	private const int CapturedCityControllerCloseWindowInstance = 49152;

	private CityControllerInteractionHandler()
	{
	}

	public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		if (!GenericCmdUseRouteClassifier.IsPrivateCityControllerTarget(target))
		{
			return false;
		}
		ICharacter character = client.Controller.Character;
		if (character == null)
		{
			((IClient)client).Server.Info((IClient)(object)client, "CityController use consumed without character target={0} count={1} temp4={2} evidence=live_capture_20260623-015602", new object[3] { target, message.Count, message.Temp4 });
			return true;
		}
		if (((IInstancedEntity)character).Playfield == null || !Playfield.IsPrivateCityPlayfieldCandidate(((IEntity)((IInstancedEntity)character).Playfield).Identity))
		{
			return false;
		}
		int num = ResolveCharacterOrganizationInstance(character);
		int num2 = ResolveCurrentPrivateCityOwningOrganizationInstance(character, num);
		CityControllerMenuMode cityControllerMenuMode = CityControllerInteractionRules.ResolveMenuMode(num, num2);
		if (cityControllerMenuMode == CityControllerMenuMode.OwnerMember)
		{
			SendCapturedCityControllerOpenSignals(client, character, num, hasOrganization: true);
		}
		else
		{
			SendCapturedCityControllerNonOrgOpenSignals(client, character, num2);
		}
		BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(character, message);
		((IClient)client).Server.Info((IClient)(object)client, "CityController use handled character={0} target={1} org={2} owningOrg={3} menu={4} count={5} temp4={6} feedbackSent=False aoTransportSignalSent=5 noCityAdvantages=1 noOwnershipChange=1 evidence=private_city_owned_entry_capture_20260623_021643/city_controller_non_org_capture_20260623_081344 runtime_target=009C6010", new object[7]
		{
			((IEntity)character).Identity,
			target,
			num,
			num2,
			(cityControllerMenuMode == CityControllerMenuMode.OwnerMember) ? "owner_member" : "non_org_limited",
			message.Count,
			message.Temp4
		});
		return true;
	}

	public bool TryHandleWindowClose(MessageWrapper<CityControllerWindowCloseMessage> messageWrapper)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Expected O, but got Unknown
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = messageWrapper.Client.Controller.Character;
		CityControllerWindowCloseMessage messageBody = messageWrapper.MessageBody;
		if (character == null || ((IInstancedEntity)character).Playfield == null || messageBody.WindowInstance != 49152 || !Playfield.IsPrivateCityPlayfieldCandidate(((IEntity)((IInstancedEntity)character).Playfield).Identity))
		{
			return false;
		}
		messageWrapper.Client.SendCompressed((MessageBody)new AOTransportSignalMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 1,
			Signal = 7,
			Payload = CreateCapturedCityControllerClosePayload(character)
		});
		((IClient)messageWrapper.Client).Server.Info((IClient)(object)messageWrapper.Client, "CityController window close handled character={0} windowInstance={1} signal=7 evidence=private_city_owned_entry_capture_20260623_021643", new object[2]
		{
			((IEntity)character).Identity,
			messageBody.WindowInstance
		});
		return true;
	}

	private static void SendCapturedCityControllerOpenSignals(IZoneClient client, ICharacter character, int organizationId, bool hasOrganization)
	{
		SendCapturedCityControllerSignal(client, character, 5, CreateCapturedCityControllerInfoPayload(character, organizationId, hasOrganization));
		SendCapturedCityControllerSignal(client, character, 10, new byte[4] { 0, 228, 225, 192 });
		SendCapturedCityControllerSignal(client, character, 13, (!hasOrganization) ? new byte[4] { 149, 197, 214, 189 } : new byte[4] { 0, 39, 136, 5 });
		SendCapturedCityControllerSignal(client, character, 14, (!hasOrganization) ? new byte[8] { 0, 0, 0, 0, 149, 197, 214, 189 } : new byte[8] { 0, 0, 0, 0, 149, 197, 204, 215 });
		SendCapturedCityControllerSignal(client, character, 15, new byte[4] { 63, 128, 0, 0 });
	}

	private static void SendCapturedCityControllerNonOrgOpenSignals(IZoneClient client, ICharacter character, int owningOrganizationId)
	{
		SendCapturedCityControllerSignal(client, character, 5, CreateCapturedCityControllerNonOrgInfoPayload(character, owningOrganizationId));
		SendCapturedCityControllerSignal(client, character, 10, new byte[4] { 3, 147, 135, 0 });
		SendCapturedCityControllerSignal(client, character, 13, new byte[4] { 0, 38, 50, 191 });
		SendCapturedCityControllerSignal(client, character, 14, new byte[8] { 0, 0, 0, 1, 149, 197, 121, 83 });
		SendCapturedCityControllerSignal(client, character, 15, new byte[4] { 63, 128, 0, 0 });
	}

	private static void SendCapturedCityControllerSignal(IZoneClient client, ICharacter character, int signal, byte[] payload)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		client.SendCompressed((MessageBody)new AOTransportSignalMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 1,
			Signal = signal,
			Payload = payload
		});
	}

	private static byte[] CreateCapturedCityControllerInfoPayload(ICharacter character, int organizationId, bool hasOrganization)
	{
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Expected I4, but got Unknown
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		string s = (hasOrganization ? "Est. 2024" : "no organization");
		byte[] bytes = Encoding.ASCII.GetBytes(s);
		List<byte> list = new List<byte>(58 + bytes.Length);
		AppendInt32(list, 50201);
		AppendInt32(list, 49152);
		AppendInt32(list, hasOrganization ? organizationId : 1970177);
		AppendInt32(list, 51102);
		AppendInt32(list, 5002);
		Identity identity = ((IEntity)character).Identity;
		AppendInt32(list, (int)((Identity)(ref identity)).Type);
		identity = ((IEntity)character).Identity;
		AppendInt32(list, ((Identity)(ref identity)).Instance);
		AppendInt32(list, (!hasOrganization) ? 1 : 2);
		AppendInt32(list, hasOrganization ? 3 : 2);
		AppendInt32(list, hasOrganization ? 1 : (-1));
		AppendInt16(list, bytes.Length);
		list.AddRange(bytes);
		return list.ToArray();
	}

	private static byte[] CreateCapturedCityControllerNonOrgInfoPayload(ICharacter character, int owningOrganizationId)
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected I4, but got Unknown
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		byte[] bytes = Encoding.ASCII.GetBytes("Identifies As Clan");
		List<byte> list = new List<byte>(58 + bytes.Length);
		AppendInt32(list, 50201);
		AppendInt32(list, 49152);
		AppendInt32(list, (owningOrganizationId > 0) ? owningOrganizationId : 1370122);
		AppendInt32(list, 51102);
		AppendInt32(list, ResolvePrivateCityControllerBuildingInstance(character));
		Identity identity = ((IEntity)character).Identity;
		AppendInt32(list, (int)((Identity)(ref identity)).Type);
		identity = ((IEntity)character).Identity;
		AppendInt32(list, ((Identity)(ref identity)).Instance);
		AppendInt32(list, 2);
		AppendInt32(list, 1);
		AppendInt32(list, -1);
		AppendInt16(list, bytes.Length);
		list.AddRange(bytes);
		return list.ToArray();
	}

	private static byte[] CreateCapturedCityControllerClosePayload(ICharacter character)
	{
		int value = ResolveCloseOrganizationInstance(character);
		List<byte> list = new List<byte>(20);
		AppendInt32(list, 50201);
		AppendInt32(list, 49152);
		AppendInt32(list, value);
		AppendInt32(list, 51102);
		AppendInt32(list, 5002);
		return list.ToArray();
	}

	private static int ResolvePrivateCityControllerBuildingInstance(ICharacter character)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return 6010;
		}
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		return (instance == 1196045 || instance == 1196034) ? 5002 : 6010;
	}

	private static int ResolveCurrentPrivateCityOwningOrganizationInstance(ICharacter character, int characterOrganizationId)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return 0;
		}
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (characterOrganizationId > 0 && ResolveOrganizationCityId(characterOrganizationId) == instance)
		{
			return characterOrganizationId;
		}
		try
		{
			DBOrganization val = ((Dao<DBOrganization, OrganizationDao>)(object)Dao<DBOrganization, OrganizationDao>.Instance).GetAll((object)new
			{
				CityId = instance
			}).FirstOrDefault();
			if (val != null)
			{
				return val.Id;
			}
		}
		catch
		{
		}
		return (instance == 1196034) ? 1970177 : 0;
	}

	private static int ResolveOrganizationCityId(int organizationInstance)
	{
		if (organizationInstance <= 0)
		{
			return 0;
		}
		try
		{
			DBOrganization val = ((Dao<DBOrganization, OrganizationDao>)(object)Dao<DBOrganization, OrganizationDao>.Instance).Get(organizationInstance);
			return (val != null) ? val.CityId : 0;
		}
		catch
		{
			return 0;
		}
	}

	private static int ResolveCharacterOrganizationInstance(ICharacter character)
	{
		uint baseValue = ((IStats)character).Stats[(StatIds)5].BaseValue;
		if (baseValue != 0 && baseValue <= int.MaxValue)
		{
			return (int)baseValue;
		}
		int value = ((IStats)character).Stats[(StatIds)5].Value;
		return (value > 0) ? value : 0;
	}

	private static int ResolveCloseOrganizationInstance(ICharacter character)
	{
		uint baseValue = ((IStats)character).Stats[(StatIds)5].BaseValue;
		if (baseValue != 0 && baseValue <= int.MaxValue)
		{
			return (int)baseValue;
		}
		int value = ((IStats)character).Stats[(StatIds)5].Value;
		return (value > 0) ? value : 1970177;
	}

	private static void AppendInt32(ICollection<byte> bytes, int value)
	{
		bytes.Add((byte)((uint)(value >> 24) & 0xFFu));
		bytes.Add((byte)((uint)(value >> 16) & 0xFFu));
		bytes.Add((byte)((uint)(value >> 8) & 0xFFu));
		bytes.Add((byte)((uint)value & 0xFFu));
	}

	private static void AppendInt16(ICollection<byte> bytes, int value)
	{
		bytes.Add((byte)((uint)(value >> 8) & 0xFFu));
		bytes.Add((byte)((uint)value & 0xFFu));
	}
}
