using System;
using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.Playfields;

internal sealed class PrivateCityReadyInitCoordinator
{
	private readonly Identity playfieldIdentity;

	private readonly Func<Identity, bool> isPrivateCityPlayfieldCandidate;

	private readonly Func<int, bool> isCapturedMontroyalPrivateCityInstance;

	private readonly Func<ICharacter, int> resolveCharacterOrganizationInstance;

	private readonly Func<int, string> resolveOrganizationName;

	private readonly Func<ICharacter, StatIds, uint> resolveCharacterStatWireValue;

	public PrivateCityReadyInitCoordinator(Identity playfieldIdentity, Func<Identity, bool> isPrivateCityPlayfieldCandidate, Func<int, bool> isCapturedMontroyalPrivateCityInstance, Func<ICharacter, int> resolveCharacterOrganizationInstance, Func<int, string> resolveOrganizationName, Func<ICharacter, StatIds, uint> resolveCharacterStatWireValue)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		this.playfieldIdentity = playfieldIdentity;
		this.isPrivateCityPlayfieldCandidate = isPrivateCityPlayfieldCandidate;
		this.isCapturedMontroyalPrivateCityInstance = isCapturedMontroyalPrivateCityInstance;
		this.resolveCharacterOrganizationInstance = resolveCharacterOrganizationInstance;
		this.resolveOrganizationName = resolveOrganizationName;
		this.resolveCharacterStatWireValue = resolveCharacterStatWireValue;
	}

	public void SendPlayfieldReadyBlock(ZoneClient client, ICharacter character)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		if (client != null && character != null && isPrivateCityPlayfieldCandidate(playfieldIdentity))
		{
			Func<int, bool> func = isCapturedMontroyalPrivateCityInstance;
			Identity val = playfieldIdentity;
			if (func(((Identity)(ref val)).Instance))
			{
				SendEmptyPlayfieldTowersAndCities(client);
			}
			else
			{
				SendPlayfieldTowersAndCities(client, 1, CreateCapturedPrivateCityAllCitiesPayload());
			}
			((ClientBase)client).Server.Info((IClient)(object)client, "Private city ready block sent character={0} playfield={1} evidence=live_capture_20260622-092054 live_capture_20260622-093540 live_capture_20260622-101935 live_capture_20260623-021643", new object[2]
			{
				((IEntity)character).Identity,
				playfieldIdentity
			});
		}
	}

	public void SendPreFullCharacterReadyBlock(ZoneClient client, ICharacter character)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || character == null || !isPrivateCityPlayfieldCandidate(playfieldIdentity))
		{
			return;
		}
		int organizationInstance = resolveCharacterOrganizationInstance(character);
		if (organizationInstance <= 0)
		{
			return;
		}
		string organizationName = resolveOrganizationName(organizationInstance);
		client.PacketSequencing.RunPrivateCityPreFullCharacterOrgInitSequence(delegate
		{
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Expected O, but got Unknown
			if (!string.IsNullOrEmpty(organizationName))
			{
				PlayfieldLifecycleTrace.Record("private-city-ready-init", "private-city-org-info-packet", "OrgInfoPacket", ((IEntity)character).Identity, organizationName);
				client.SendCompressed((MessageBody)new OrgInfoPacketMessage
				{
					Identity = ((IEntity)character).Identity,
					Name = organizationName
				});
			}
		}, delegate
		{
			SendPrivateCityStatValue(client, character, (StatIds)521, 4u, 1);
		}, delegate
		{
			SendPrivateCityStat(client, character, (StatIds)5, 0);
		}, delegate
		{
			SendPrivateCityStat(client, character, (StatIds)48, 0);
		}, delegate
		{
			SendPrivateCityStatValue(client, character, (StatIds)521, 4u, 1);
		}, delegate
		{
			SendPrivateCityStatValue(client, character, (StatIds)521, 4u, 1);
		}, delegate
		{
			SendPrivateCityStatValue(client, character, (StatIds)521, 4u, 1);
		}, delegate
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			PlayfieldLifecycleTrace.Record("private-city-ready-init", "private-city-org-init-sent", "PrivateCityOrgInitSent", ((IEntity)character).Identity, "org=" + organizationInstance + " orgName=" + organizationName + " socialStatus=4 repeats=4");
		});
		((ClientBase)client).Server.Info((IClient)(object)client, "Private city owned org init sent character={0} playfield={1} org={2} orgInfoSent={3} socialStatus=4 repeats=4 evidence=live_capture_20260623-021643 live_capture_20260623-042326", new object[4]
		{
			((IEntity)character).Identity,
			playfieldIdentity,
			organizationInstance,
			!string.IsNullOrEmpty(organizationName)
		});
	}

	private void SendEmptyPlayfieldTowersAndCities(ZoneClient client)
	{
		SendPlayfieldTowersAndCities(client, 0, new byte[0]);
	}

	private void SendPlayfieldTowersAndCities(ZoneClient client, byte cityUnknown, byte[] cityPayload)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)40016;
		Identity val2 = this.playfieldIdentity;
		((Identity)(ref val)).Instance = ((Identity)(ref val2)).Instance;
		Identity playfieldIdentity = val;
		client.PacketSequencing.RunPrivateCityPlayfieldReadyBlockSequence(delegate
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Expected O, but got Unknown
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			ZoneClient zoneClient2 = client;
			PlayfieldAllTowersMessage val4 = new PlayfieldAllTowersMessage();
			((N3Message)val4).Identity = playfieldIdentity;
			val4.Unknown1 = (TowerProxyBase[])(object)new TowerProxyBase[0];
			zoneClient2.SendCompressed((MessageBody)(object)val4);
		}, delegate
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			PlayfieldLifecycleTrace.Record("private-city-ready-init", "private-city-playfield-all-towers", "PlayfieldAllTowers", playfieldIdentity);
		}, delegate
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Expected O, but got Unknown
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			ZoneClient zoneClient = client;
			PlayfieldAllCitiesMessage val3 = new PlayfieldAllCitiesMessage();
			((N3Message)val3).Identity = playfieldIdentity;
			((N3Message)val3).Unknown = cityUnknown;
			val3.Payload = cityPayload ?? new byte[0];
			zoneClient.SendCompressed((MessageBody)(object)val3);
		}, delegate
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			PlayfieldLifecycleTrace.Record("private-city-ready-init", "private-city-playfield-all-cities", "PlayfieldAllCities", playfieldIdentity);
		}, delegate
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			PlayfieldLifecycleTrace.Record("private-city-ready-init", "private-city-towers-cities-sent", "PrivateCityTowersCitiesSent", playfieldIdentity, "cityUnknown=" + cityUnknown + " cityPayloadBytes=" + ((cityPayload != null) ? cityPayload.Length : 0));
		});
	}

	private void SendPrivateCityStat(ZoneClient client, ICharacter character, StatIds statId, byte unknown)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		SendPrivateCityStatValue(client, character, statId, resolveCharacterStatWireValue(character, statId), unknown);
	}

	private void SendPrivateCityStatValue(ZoneClient client, ICharacter character, StatIds statId, uint value, byte unknown)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Invalid comparison between Unknown and I4
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Invalid comparison between Unknown and I4
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected O, but got Unknown
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		string stage = "private-city-social-status";
		if ((int)statId == 5)
		{
			stage = "private-city-clan";
		}
		else if ((int)statId == 48)
		{
			stage = "private-city-clan-level";
		}
		PlayfieldLifecycleTrace.Record("private-city-ready-init", stage, "Stat", ((IEntity)character).Identity, ((object)(StatIds)(ref statId)).ToString() + "=" + value);
		StatMessage val = new StatMessage();
		((N3Message)val).Identity = ((IEntity)character).Identity;
		((N3Message)val).Unknown = unknown;
		val.Stats = new GameTuple<CharacterStat, uint>[1]
		{
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)statId,
				Value2 = value
			}
		};
		client.SendCompressed((MessageBody)(object)val);
	}

	private static byte[] CreateCapturedPrivateCityAllCitiesPayload()
	{
		return new byte[129]
		{
			0, 0, 0, 5, 68, 92, 0, 0, 64, 160,
			0, 0, 68, 181, 128, 0, 0, 0, 0, 180,
			0, 0, 15, 66, 104, 0, 106, 0, 109, 68,
			98, 0, 0, 64, 160, 0, 0, 68, 176, 128,
			0, 0, 0, 0, 180, 0, 0, 15, 66, 104,
			0, 106, 0, 110, 68, 128, 128, 0, 64, 160,
			0, 0, 68, 169, 128, 0, 0, 0, 0, 0,
			0, 0, 15, 66, 104, 0, 106, 0, 104, 68,
			133, 128, 0, 64, 160, 0, 0, 68, 176, 128,
			0, 0, 0, 0, 90, 0, 0, 15, 66, 104,
			0, 106, 0, 102, 68, 135, 128, 0, 64, 160,
			0, 0, 68, 169, 128, 0, 0, 0, 0, 0,
			0, 0, 15, 66, 104, 0, 106, 0, 117
		};
	}
}
