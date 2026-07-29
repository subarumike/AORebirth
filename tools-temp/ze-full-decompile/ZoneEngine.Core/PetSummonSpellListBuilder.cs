using System;
using System.Net;
using System.Text;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core;

internal static class PetSummonSpellListBuilder
{
	private const int OwnerIdentityOffset1 = 124;

	private const int OwnerIdentityOffset2 = 132;

	private const int OwnerNameLengthOffset = 140;

	private const int OwnerPetHashLengthOffset = 57;

	private const int OwnerPetHashStringOffset = 58;

	private const int OwnerPetTypeIdOffset = 62;

	private const int PetIdentityOffset = 56;

	private static readonly byte[] OwnerHealBodyTemplate = HexToBytes("07E20000CFAF0001EB31000000040000000200000000000002D00000005D00000000000000000000002A000000010000000000000001000000094D54303200000021FFFFFFFF00000000000000030000000000000000000000000001ADB100000001000000830000008000000000000000AE000000AE000000000000C35035FE28680000C35035FE286800001443616C6C696E67206F662053616C76696E6F7573000000000000");

	private static readonly byte[] OwnerAttackBodyTemplate = HexToBytes("07E20000CFAF0000AAD90000000400000005000000830000034E00000002000000820000034E00000002000000000000000000000004000000FB00000001000000420000000000000000000000040000000100000000000000010000000950543536000000BFFFFFFFFF0000000000000002000000000000000000000000000186A1000000010000008300000082000000000000034E0000034E000000000000C35035FE28680000C35035FE286800000C53756D6D6F6E2044656D6F6E000000000000");

	private static readonly byte[] PetHealSpellListBodyTemplateA = HexToBytes("07E20000CF221860BA6600000004000000000000000100000000000000000000000000000000000002B10000009600000000000000000000C350795625EA000000000000000000");

	private static readonly byte[] PetHealSpellListBodyTemplateB = HexToBytes("07E20000CF221860BA6700000004000000000000000100000000000000000000000000000000000002170000007D00000000000000000000C350795625EA000000000000000000");

	public static byte[] BuildOwnerPayload(Identity owner, int nanoId, string petHash, int petTypeId, int spellListSlot, string nanoName)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = ((spellListSlot == 2) ? ((byte[])OwnerHealBodyTemplate.Clone()) : ((byte[])OwnerAttackBodyTemplate.Clone()));
		PatchNanoId(array, nanoId);
		PatchSpellListSlot(array, spellListSlot);
		PatchIdentity(array, 124, owner);
		PatchIdentity(array, 132, owner);
		PatchPetHash(array, petHash);
		PatchPetTypeId(array, petTypeId);
		PatchName(array, 140, nanoName ?? string.Empty);
		return array;
	}

	public static byte[][] BuildPetPayloads(Identity petIdentity)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		return new byte[2][]
		{
			BuildPetPayload(PetHealSpellListBodyTemplateA, petIdentity),
			BuildPetPayload(PetHealSpellListBodyTemplateB, petIdentity)
		};
	}

	private static byte[] BuildPetPayload(byte[] template, Identity petIdentity)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = (byte[])template.Clone();
		PatchIdentity(array, 56, petIdentity);
		return array;
	}

	private static void PatchNanoId(byte[] body, int nanoId)
	{
		if (nanoId <= 65535)
		{
			WriteUInt16BigEndian(body, 6, 0);
			WriteUInt16BigEndian(body, 8, (ushort)nanoId);
			WriteUInt16BigEndian(body, 10, 0);
		}
		else
		{
			WriteInt32BigEndian(body, 6, nanoId);
		}
	}

	private static void PatchSpellListSlot(byte[] body, int spellListSlot)
	{
		WriteInt32BigEndian(body, 14, spellListSlot);
	}

	private static void PatchIdentity(byte[] body, int offset, Identity identity)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		WriteUInt16BigEndian(body, offset, (ushort)((Identity)(ref identity)).Type);
		WriteInt32LittleEndian(body, offset + 2, ((Identity)(ref identity)).Instance);
		WriteUInt16BigEndian(body, offset + 6, 0);
	}

	private static void PatchName(byte[] body, int lengthOffset, string value)
	{
		byte[] bytes = Encoding.ASCII.GetBytes(value);
		if (bytes.Length > 255)
		{
			throw new InvalidOperationException("Pet summon SpellList nano name is too long.");
		}
		body[lengthOffset] = (byte)bytes.Length;
		int num = lengthOffset + 1;
		Array.Clear(body, num, body.Length - num);
		Buffer.BlockCopy(bytes, 0, body, num, bytes.Length);
	}

	private static void PatchPetHash(byte[] body, string petHash)
	{
		if (!string.IsNullOrWhiteSpace(petHash))
		{
			byte[] bytes = Encoding.ASCII.GetBytes(petHash);
			if (bytes.Length > 4)
			{
				throw new InvalidOperationException("Pet summon SpellList pet hash is too long.");
			}
			body[57] = 9;
			Array.Clear(body, 58, 4);
			Buffer.BlockCopy(bytes, 0, body, 58, bytes.Length);
		}
	}

	private static void PatchPetTypeId(byte[] body, int petTypeId)
	{
		WriteInt32BigEndian(body, 62, petTypeId);
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

	private static byte[] HexToBytes(string hex)
	{
		if (hex.Length % 2 != 0)
		{
			throw new InvalidOperationException("Invalid hex template length.");
		}
		byte[] array = new byte[hex.Length / 2];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
		}
		return array;
	}
}
