using System;
using System.Net;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Controllers;

namespace ZoneEngine.Core;

internal static class PetBureaucratGuardianScfuWire
{
	private const int CorporateGuardianNanoId = 235386;

	private const int CeoGuardianNanoId = 273300;

	private const int ZoneServerSenderId = 854;

	private const int HeaderReceiverOffset = 12;

	private const int HeaderSenderOffset = 8;

	private const int N3IdentityInstanceOffset = 24;

	private const int ScfuPlayfieldOffset = 34;

	private const int ScfuCoordOffset = 38;

	private static readonly byte[] ScfuCorporateGuardian = Hex("00B6000A0001017600000DB3762ABC21271B3A6B0000C35079623667003A0A0A6A530015902D4345E2AE40A051EC42FC602D0000000000000000000000003F800000000005E813436F72706F7261746520477561726469616E0010081201000000005F000A0000CD726800000379750082001F000000001C00000000000000000000000003010001000100010001000000020000038D00000FC468656C6C6661636532000000000000000000000000000000000000000000000000036C55000000000000000168656C6C3200000000000000000000000000000000000000000000000000000000036C56000000000000000068656C6C3100000000000000000000000000000000000000000000000000000000036C560000000000000000000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000007E20100042B980000000002000000020000");

	private static readonly byte[] ScfuCeoGuardian = Hex("00C3000A0001017000000DB1762ABC21271B3A6B0000C3507962B1E3003A0A0A6A530015781E4344C80E40A051EC42FAE48D0000000000000000000000003F800000000005E80D43454F20477561726469616E0010081201000000005F000A0000D786D10000037975007D001F000000001C00000000000000000000000003010001000100010001000000020000042600000FC468656C6C6661636532000000000000000000000000000000000000000000000000036C55000000000000000168656C6C3200000000000000000000000000000000000000000000000000000000036C56000000000000000068656C6C3100000000000000000000000000000000000000000000000000000000036C560000000000000000000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000007E20100042B980000000002000000020000");

	public static void SendToOwner(ZoneClient ownerClient, ICharacter owner, Character petCharacter, int summonNanoId)
	{
		if (ownerClient != null)
		{
			SendToRecipient(ownerClient, owner, petCharacter, summonNanoId);
		}
	}

	public static void SendToOtherPlayers(ICharacter owner, Character petCharacter, int summonNanoId)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || petCharacter == null || ((IInstancedEntity)owner).Playfield == null || !(((IInstancedEntity)owner).Playfield is Playfield playfield))
		{
			return;
		}
		foreach (ICharacter item in playfield.EnumerateActiveCharacters())
		{
			if (item != null && !(((IEntity)item).Identity == ((IEntity)owner).Identity) && ((IDynel)item).Controller is PlayerController && ((IDynel)item).Controller.Client is ZoneClient recipientClient)
			{
				SendToRecipient(recipientClient, owner, petCharacter, summonNanoId);
			}
		}
	}

	public static void SendToRecipient(ZoneClient recipientClient, ICharacter owner, Character petCharacter, int summonNanoId)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		object obj;
		if (recipientClient == null)
		{
			obj = null;
		}
		else
		{
			IController controller = recipientClient.Controller;
			obj = ((controller != null) ? controller.Character : null);
		}
		if (obj != null)
		{
			Identity identity = ((IEntity)recipientClient.Controller.Character).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			byte[] array = BuildPacket(owner, petCharacter, summonNanoId, instance);
			if (array != null)
			{
				((ClientBase)recipientClient).Server.Info((IClient)(object)recipientClient, "SummonWireSend GuardianSCFU recipient={0} pet={1} len={2} nano={3}", new object[4]
				{
					instance,
					((PooledObject)petCharacter).Identity,
					array.Length,
					summonNanoId
				});
				recipientClient.EnqueueOutboundCompressedBuffer(array);
			}
		}
	}

	internal static byte[] BuildPacket(ICharacter owner, Character petCharacter, int summonNanoId, int recipientInstance)
	{
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || petCharacter == null || ((IInstancedEntity)owner).Playfield == null)
		{
			return null;
		}
		byte[] array = summonNanoId switch
		{
			235386 => ScfuCorporateGuardian, 
			273300 => ScfuCeoGuardian, 
			_ => null, 
		};
		if (array == null)
		{
			return null;
		}
		byte[] array2 = (byte[])array.Clone();
		Identity identity = ((PooledObject)petCharacter).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		identity = ((IEntity)((IInstancedEntity)owner).Playfield).Identity;
		int instance2 = ((Identity)(ref identity)).Instance;
		Coordinate val = ((Dynel)petCharacter).Coordinates();
		PatchHeader(array2, recipientInstance);
		WriteInt32BigEndian(array2, 24, instance);
		WriteInt32BigEndian(array2, 34, instance2);
		WriteFloat(array2, 38, val.x);
		WriteFloat(array2, 42, val.y);
		WriteFloat(array2, 46, val.z);
		return array2;
	}

	private static void PatchHeader(byte[] packet, int recipientInstance)
	{
		WriteInt32BigEndian(packet, 8, 854);
		WriteInt32BigEndian(packet, 12, recipientInstance);
		ushort num = (ushort)packet.Length;
		packet[6] = (byte)(num >> 8);
		packet[7] = (byte)num;
	}

	private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
	{
		byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
		Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
	}

	private static void WriteFloat(byte[] buffer, int offset, float value)
	{
		byte[] bytes = BitConverter.GetBytes(value);
		if (BitConverter.IsLittleEndian)
		{
			Array.Reverse(bytes);
		}
		Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
	}

	private static byte[] Hex(string hex)
	{
		byte[] array = new byte[hex.Length / 2];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
		}
		return array;
	}
}
