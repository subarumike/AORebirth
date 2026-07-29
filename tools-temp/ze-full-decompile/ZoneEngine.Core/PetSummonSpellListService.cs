using System;
using System.Net;
using System.Threading;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core;

public static class PetSummonSpellListService
{
	private const int ZoneServerSenderId = 854;

	private const int HeaderReceiverOffset = 12;

	private const int N3IdentityInstanceOffset = 24;

	private const int BodyStartOffset = 31;

	private const int OwnerBodyIdentityOffset1 = 124;

	private const int OwnerBodyIdentityOffset2 = 132;

	private const int PetBodyIdentityOffset = 56;

	private static readonly byte[] OwnerHealCaptureWire = HexToBytes("004D000A000100C600000DBC35FE28684D4501140000C35035FE286800000007E20000CFAF0001EB32000000040000000200000000000002D00000005D00000000000000000000002A000000010000000000000001000000094D543039000000C0FFFFFFFF00000000000000030000000000000000000000000001ADB1000000010000008300000080000000000000035100000351000000000000C35035FE28680000C35035FE286800001443616C6C696E67206F662042656C616D6F727465000000000000");

	private const int OwnerHealCaptureHeaderLength = 31;

	private static readonly byte[] OwnerHealCaptureHeader = CopyHeader(OwnerHealCaptureWire, 31);

	private static readonly byte[] OwnerHealTierCaptureHeader = HexToBytes("004D000A000100C600000DBD35FE28684D4501140000C35035FE2868000000");

	private static readonly byte[] OwnerAttackCaptureWire = HexToBytes("0070000A000100E200000DBC35FE28684D4501140000C35035FE286800000007E20000CFAF0000AAD90000000400000005000000830000034E00000002000000820000034E00000002000000000000000000000004000000FB00000001000000420000000000000000000000040000000100000000000000010000000950543536000000BFFFFFFFFF0000000000000002000000000000000000000000000186A1000000010000008300000082000000000000034E0000034E000000000000C35035FE28680000C35035FE286800000C53756D6D6F6E2044656D6F6E000000000000");

	private static readonly byte[] PetHealCaptureWireA = HexToBytes("005A000A0001006600000DBC35FE28684D4501140000C350795625EA00000007E20000CF2218620D8500000004000000000000000100000000000000000000000000000000000002B10000009600000000000000000000C350795625EA000000000000000000");

	private static readonly byte[] PetHealCaptureWireB = HexToBytes("005B000A0001006600000DBC35FE28684D4501140000C350795625EA00000007E20000CF2218620D8600000004000000000000000100000000000000000000000000000000000002170000007D00000000000000000000C350795625EA000000000000000000");

	private static readonly byte[] BureaucratAttackPetSpellListWire = HexToBytes("00C6000A0001006600000DB3762ABC214D4501140000C3507962366700000007E20000CF221864840D00000004000000000000000100000000000000000000000000000000000002B10000009600000000000000000000C35079623667000000000000000000");

	private const int BureaucratSpellListSubIdOffset = 40;

	private static int bureaucratWorkerSpellListSeq = 33216;

	private static int bureaucratGuardianSpellListSeq = 33804;

	public static void SendOwnerPetSummon(ICharacter owner, int nanoId, string petHash, int petTypeId, int petSlotStrain)
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		Character val = (Character)(object)((owner is Character) ? owner : null);
		if (val != null && ((Dynel)val).Controller != null && ((Dynel)val).Controller.Client != null && nanoId > 0)
		{
			byte[] array;
			if (petSlotStrain == 1016 && nanoId != 125746)
			{
				byte[] body = PetSummonSpellListBuilder.BuildOwnerPayload(((PooledObject)val).Identity, nanoId, petHash, petTypeId, 2, PetSummonNanoCatalog.GetSummonNanoDisplayName(nanoId));
				array = CombineHeaderAndBody(OwnerHealTierCaptureHeader, body);
			}
			else
			{
				array = ((petSlotStrain == 1016) ? OwnerHealCaptureWire : OwnerAttackCaptureWire);
			}
			byte[] captureWire = array;
			Identity identity = ((IEntity)owner).Identity;
			Identity identity2 = ((IEntity)owner).Identity;
			SendPatchedCaptureWire(val, captureWire, identity, ((Identity)(ref identity2)).Instance, patchOwnerBodyIdentities: true);
		}
	}

	public static void SendPetSummonSpellLists(ICharacter owner, Identity petIdentity, int petSlotStrain, string petHash = null)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Invalid comparison between Unknown and I4
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		Character val = (Character)(object)((owner is Character) ? owner : null);
		if (val == null || ((Dynel)val).Controller == null || ((Dynel)val).Controller.Client == null || (int)((Identity)(ref petIdentity)).Type == 0 || petSlotStrain != 1016)
		{
			return;
		}
		Identity identity;
		if (PetHealingPetScfuCatalog.TryGetPetSpellListWires(petHash, out var wireA, out var wireB))
		{
			if (wireA != null)
			{
				byte[] captureWire = wireA;
				Identity messageIdentity = petIdentity;
				identity = ((PooledObject)val).Identity;
				SendPatchedCaptureWire(val, captureWire, messageIdentity, ((Identity)(ref identity)).Instance, patchOwnerBodyIdentities: false);
			}
			if (wireB != null)
			{
				byte[] captureWire2 = wireB;
				Identity messageIdentity2 = petIdentity;
				identity = ((PooledObject)val).Identity;
				SendPatchedCaptureWire(val, captureWire2, messageIdentity2, ((Identity)(ref identity)).Instance, patchOwnerBodyIdentities: false);
			}
		}
		else
		{
			byte[] petHealCaptureWireA = PetHealCaptureWireA;
			Identity messageIdentity3 = petIdentity;
			identity = ((PooledObject)val).Identity;
			SendPatchedCaptureWire(val, petHealCaptureWireA, messageIdentity3, ((Identity)(ref identity)).Instance, patchOwnerBodyIdentities: false);
			byte[] petHealCaptureWireB = PetHealCaptureWireB;
			Identity messageIdentity4 = petIdentity;
			identity = ((PooledObject)val).Identity;
			SendPatchedCaptureWire(val, petHealCaptureWireB, messageIdentity4, ((Identity)(ref identity)).Instance, patchOwnerBodyIdentities: false);
		}
	}

	private static void SendPatchedCaptureWire(Character ownerCharacter, byte[] captureWire, Identity messageIdentity, int receiverInstance, bool patchOwnerBodyIdentities)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = (byte[])captureWire.Clone();
		WriteInt32BigEndian(array, 12, receiverInstance);
		WriteInt32BigEndian(array, 24, ((Identity)(ref messageIdentity)).Instance);
		if (patchOwnerBodyIdentities)
		{
			PatchBodyIdentity(array, 155, messageIdentity);
			PatchBodyIdentity(array, 163, messageIdentity);
		}
		else
		{
			PatchBodyIdentity(array, 87, messageIdentity);
		}
		WriteInt32BigEndian(array, 8, 854);
		ushort num = (ushort)array.Length;
		array[6] = (byte)(num >> 8);
		array[7] = (byte)num;
		if (((Dynel)ownerCharacter).Controller.Client is ZoneClient zoneClient)
		{
			((ClientBase)zoneClient).Server.Info((IClient)(object)zoneClient, "SpellListSend identity={0} len={1} mode=capture-wire", new object[2] { messageIdentity, array.Length });
			zoneClient.EnqueueOutboundCompressedBuffer(array);
		}
	}

	private static int AllocateBureaucratSpellListSubId(string petHash)
	{
		ushort num = ((!string.Equals(petHash, "A141", StringComparison.OrdinalIgnoreCase) && !string.Equals(petHash, "BCBG", StringComparison.OrdinalIgnoreCase)) ? ((ushort)Interlocked.Increment(ref bureaucratWorkerSpellListSeq)) : ((ushort)Interlocked.Increment(ref bureaucratGuardianSpellListSeq)));
		return 0x18640000 | num;
	}

	private static void PatchBodyIdentity(byte[] packet, int offset, Identity identity)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		WriteUInt16BigEndian(packet, offset, (ushort)((Identity)(ref identity)).Type);
		WriteInt32LittleEndian(packet, offset + 2, ((Identity)(ref identity)).Instance);
		WriteUInt16BigEndian(packet, offset + 6, 0);
	}

	private static void WriteUInt16BigEndian(byte[] buffer, int offset, ushort value)
	{
		buffer[offset] = (byte)(value >> 8);
		buffer[offset + 1] = (byte)value;
	}

	private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
	{
		byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
		Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
	}

	private static void WriteInt32LittleEndian(byte[] buffer, int offset, int value)
	{
		byte[] bytes = BitConverter.GetBytes(value);
		Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
	}

	private static byte[] CopyHeader(byte[] captureWire, int headerLength)
	{
		byte[] array = new byte[headerLength];
		Buffer.BlockCopy(captureWire, 0, array, 0, headerLength);
		return array;
	}

	private static byte[] CombineHeaderAndBody(byte[] header, byte[] body)
	{
		byte[] array = new byte[header.Length + body.Length];
		Buffer.BlockCopy(header, 0, array, 0, header.Length);
		Buffer.BlockCopy(body, 0, array, header.Length, body.Length);
		ushort num = (ushort)array.Length;
		array[6] = (byte)(num >> 8);
		array[7] = (byte)num;
		return array;
	}

	private static byte[] HexToBytes(string hex)
	{
		if (hex.Length % 2 != 0)
		{
			throw new InvalidOperationException("Invalid capture hex length.");
		}
		byte[] array = new byte[hex.Length / 2];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
		}
		return array;
	}
}
