using System;
using System.Net;
using System.Text;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Packets;

public static class CorpseFullUpdate
{
	private const int OriginalEncodedNameLength = 27;

	private const int NameOffset = 231;

	private const int NameLengthOffset = 227;

	private const int OriginalSuffixOffset = 258;

	private const int ServerIdOffset = 8;

	private const int ReceiverInstanceOffset = 12;

	private const int CorpseInstanceOffset = 24;

	private const int PositionXOffset = 45;

	private const int PositionYOffset = 49;

	private const int PositionZOffset = 53;

	private const int PlayfieldIdOffset = 73;

	private const int MonsterScaleOffset = 143;

	private const int SexOffset = 159;

	private const int BreedOffset = 167;

	private const int RaceOffset = 175;

	private const int DeadNpcInstanceOffset = 191;

	private const int CorpseCatMeshOffset = 199;

	private const int CorpseCashValueOffset = 207;

	private const int CorpseMonsterDataOffset = 330;

	private const int TailDeadNpcInstanceOffset = 342;

	private const int CapturedSubwayFilthFleaPacketLength = 457;

	private const int CapturedSubwayFilthFleaMonsterDataOffset = 325;

	private const int CapturedSubwayFilthFleaTailDeadNpcInstanceOffset = 337;

	private const int CapturedSubwayThiefPacketLength = 412;

	private const int CapturedSubwayThiefMonsterDataOffset = 324;

	private const int CapturedSubwayThiefTailDeadNpcInstanceOffset = 336;

	private const int CapturedSubwayAbmouthPacketLength = 415;

	private const int CapturedSubwayAbmouthMonsterDataOffset = 331;

	private const int CapturedSubwayAbmouthTailDeadNpcInstanceOffset = 343;

	private const int CapturedSubwayVergilPacketLength = 420;

	private const int CapturedSubwayVergilMonsterDataOffset = 336;

	private const int CapturedSubwayVergilTailDeadNpcInstanceOffset = 348;

	private const int CapturedSubwayEumenidesPacketLength = 416;

	private const int CapturedSubwayEumenidesMonsterDataOffset = 332;

	private const int CapturedSubwayEumenidesTailDeadNpcInstanceOffset = 344;

	private static readonly byte[] Template = HexToBytes("0000000a0001019e000000003cac6f144f474e050000c76a00f0f00100000000080000000b00000000000000004504a4df41c5ea1244cb530d000000003e8fb30a000000003f75b5e0000002350000000000000000006f000046f200000000001818050000001700000000000002bd00000000000002be00000000000002bf000000000000019c000000010000016800000062000000df000000000000003b00000003000000040000000700000059000000010000019f0000c350000001a0776b95780000002a0000797e0000003d000000000000000800004650000000220000003c0000001b52656d61696e73206f66205268696e6f6d616e204d6f74686572000000000200000032000003f100000003000007e20000cf2738f46cbe0000000400000000000000010000000000000000000000000000000000000000000001f700000001000000040000798a000000000000c350776b9578000017a600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000000");

	private static readonly byte[] CapturedSubwayFilthFleaTemplate = HexToBytes("0000000A000101C900000DB970CBBEF34F474E050000C76A00F6E00900000000080000000B000000000000000042B45D5F42E73AE14397BF7900000000BED08F91800000003F69CC57001220020000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000082000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A079528CA70000002A00003B7F0000003D000000170000000800004650000000220000003C0000001652656D61696E73206F662046696C746820466C6561000000000200000032000003F100000003000007E20000CF273975F70E0000000400000000000000010000000000000000000000000000000000000000000001F70000000100000004000044F9000000000000C35079528CA7000017A600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000001000007E24D6174657269616C20233900000000000000000000000000000000000000000000003B810000000000000001");

	private static readonly byte[] CapturedSubwayThiefTemplate = HexToBytes("0000000A0001019C00000DB47944C0654F474E050000C76A00F6C00400000000080000000B0000000000000000428C26A642E73ACB439F59EA000000003F19FB87000000003F4C83360014D00E0000000000000000006F00004AE300000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C00000001000001680000005D000000DF000000010000003B00000002000000040000000100000059000000010000019F0000C350000001A07957E61A0000002A000017130000003D0000001D0000000800004650000000220000003C00000040000273310000001152656D61696E73206F66205468696566000000000200000032000003F100000003000007E20000CF273978332B0000000400000000000000010000000000000000000000000000000000000000000001F70000000100000004000065EC000000000000C3507957E61A000017A600000000000024CA0000000000000001000022190000000000000002000024CC0000000000000003000024CB0000000000000004000024CD0000000000000000");

	private static readonly byte[] CapturedSubwayAbmouthTemplate = HexToBytes("0E6B000A0001019F00000DB47944C0654F474E050000C76A00F6C00200000000080000000B000000000000000043AA09E642933C6442C459E000000000BF7C21E7000000003E3152F8001530080000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C0000000100000168000000A2000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A079607A350000002A00025F9C0000003D0000024B000000080002BF20000000220000003C0000001C52656D61696E73206F662041626D6F7574682053757072656D7573000000000200000032000003F100000003000007E20000CF27397C27680000000400000000000000010000000000000000000000000000000000000000000001F400000001000000040002613A000000000000C35079607A35000017A600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000000");

	private static readonly byte[] CapturedSubwayVergilTemplate = HexToBytes("06ED000A000101A400000DB47944C0654F474E050000C76A00F6C01400000000080000000B0000000000000000438C819A4292093142C62FE200000000BF2D43E6800000003F3C7465001530080000000000000000006F00004AE300000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000084000000DF000000010000003B00000002000000040000000300000059000000010000019F0000C350000001A079607AE50000002A000017210000003D0000024B000000080002BF20000000220000003C0000004000009CEB0000001952656D61696E73206F662056657267696C2041656E656964000000000200000032000003F100000003000007E20000CF27397C279A0000000400000000000000010000000000000000000000000000000000000000000001F4000000010000000400031BE4000000000000C35079607AE5000017A6000000000001CB9500000000000000010000258900000000000000020000258F0000000000000003000025870000000000000004000025960000000000000000");

	private static readonly byte[] CapturedSubwayEumenidesTemplate = HexToBytes("03FA000A000101A000000DAD7944C0654F474E050000C76A00F6900600000000080000000B000000000000000043687A414291A1FB4235869100000000BF24B2D5800000003F43FC550015781E0000000000000000006F00004AE300000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000082000000DF000000010000003B00000002000000040000000300000059000000010000019F0000C350000001A0797022340000002A000045F10000003D000000BA0000000800007210000000220000003C000000400000740C0000001552656D61696E73206F662045756D656E69646573000000000200000032000003F100000003000007E20000CF273983C39B0000000400000000000000010000000000000000000000000000000000000000000001F6000000010000000400031BCE000000000000C35079702234000017A6000000000000259400000000000000010000258C0000000000000002000025920000000000000003000185C30000000000000004000025990000000000000000");

	public static byte[] Build(ICharacter deadNpc, Identity corpseIdentity, Identity receiver, int serverId, int corpseCatMesh, int corpseMonsterData, int corpseCredits)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_034b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0350: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		if (deadNpc != null && corpseMonsterData == 203748)
		{
			return BuildCapturedSubwayVergil(deadNpc, corpseIdentity, receiver, serverId, corpseCatMesh, corpseMonsterData, corpseCredits);
		}
		if (deadNpc != null && corpseMonsterData == 203726)
		{
			return BuildCapturedSubwayEumenides(deadNpc, corpseIdentity, receiver, serverId, corpseCatMesh, corpseMonsterData, corpseCredits);
		}
		Identity identity;
		if (deadNpc != null)
		{
			identity = ((IEntity)deadNpc).Identity;
			if (CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition) && definition.IsBoss && string.Equals(definition.ProfileKey, "subway.127.boss.abmouth-supremus", StringComparison.Ordinal))
			{
				return BuildCapturedSubwayAbmouth(deadNpc, corpseIdentity, receiver, serverId, corpseCatMesh, corpseMonsterData, corpseCredits);
			}
		}
		OrdinaryEnemyRuntimeDefinition definition2 = null;
		int num;
		if (deadNpc != null)
		{
			identity = ((IEntity)deadNpc).Identity;
			num = (OrdinaryEnemyRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out definition2) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		bool flag = (byte)num != 0;
		if (flag && definition2.Profile.Corpse.PacketProfile == OrdinaryEnemyCorpsePacketProfile.CapturedThief)
		{
			return BuildCapturedSubwayThief(deadNpc, corpseIdentity, receiver, serverId, corpseCatMesh, corpseMonsterData, corpseCredits);
		}
		if (flag && definition2.Profile.Corpse.PacketProfile == OrdinaryEnemyCorpsePacketProfile.CapturedFilthFlea)
		{
			return BuildCapturedSubwayFilthFlea(deadNpc, corpseIdentity, receiver, serverId, corpseCatMesh, corpseMonsterData, corpseCredits);
		}
		string s = "Remains of " + ((INamedEntity)deadNpc).Name;
		byte[] bytes = Encoding.ASCII.GetBytes(s);
		int num2 = bytes.Length + 1;
		int num3 = 231 + num2;
		int num4 = num3 - 258;
		byte[] array = new byte[Template.Length + num4];
		Buffer.BlockCopy(Template, 0, array, 0, 231);
		Buffer.BlockCopy(bytes, 0, array, 231, bytes.Length);
		Buffer.BlockCopy(Template, 258, array, num3, Template.Length - 258);
		WritePacketLength(array, array.Length);
		WriteInt32(array, 8, serverId);
		WriteInt32(array, 12, ((Identity)(ref receiver)).Instance);
		WriteInt32(array, 24, ((Identity)(ref corpseIdentity)).Instance);
		WriteSingle(array, 45, ((IDynel)deadNpc).RawCoordinates.X);
		WriteSingle(array, 49, ((IDynel)deadNpc).RawCoordinates.Y);
		WriteSingle(array, 53, ((IDynel)deadNpc).RawCoordinates.Z);
		identity = ((IEntity)((IInstancedEntity)deadNpc).Playfield).Identity;
		WriteInt32(array, 73, ((Identity)(ref identity)).Instance);
		WriteInt32(array, 143, ((IStats)deadNpc).Stats[(StatIds)360].Value);
		WriteInt32(array, 159, ((IStats)deadNpc).Stats[(StatIds)59].Value);
		WriteInt32(array, 167, ((IStats)deadNpc).Stats[(StatIds)4].Value);
		WriteInt32(array, 175, ((IStats)deadNpc).Stats[(StatIds)89].Value);
		identity = ((IEntity)deadNpc).Identity;
		WriteInt32(array, 191, ((Identity)(ref identity)).Instance);
		WriteInt32(array, 199, corpseCatMesh);
		WriteInt32(array, 207, Math.Max(0, corpseCredits));
		WriteInt32(array, 227, num2);
		WriteInt32(array, 330 + num4, corpseMonsterData);
		int offset = 342 + num4;
		identity = ((IEntity)deadNpc).Identity;
		WriteInt32(array, offset, ((Identity)(ref identity)).Instance);
		return array;
	}

	private static byte[] BuildCapturedSubwayVergil(ICharacter deadNpc, Identity corpseIdentity, Identity receiver, int serverId, int corpseCatMesh, int corpseMonsterData, int corpseCredits)
	{
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = (byte[])CapturedSubwayVergilTemplate.Clone();
		if (array.Length != 420)
		{
			throw new InvalidOperationException("Captured Subway Vergil corpse template length changed.");
		}
		WritePacketLength(array, array.Length);
		WriteInt32(array, 8, serverId);
		WriteInt32(array, 12, ((Identity)(ref receiver)).Instance);
		WriteInt32(array, 24, ((Identity)(ref corpseIdentity)).Instance);
		WriteSingle(array, 45, ((IDynel)deadNpc).RawCoordinates.X);
		WriteSingle(array, 49, ((IDynel)deadNpc).RawCoordinates.Y);
		WriteSingle(array, 53, ((IDynel)deadNpc).RawCoordinates.Z);
		Identity identity = ((IEntity)((IInstancedEntity)deadNpc).Playfield).Identity;
		WriteInt32(array, 73, ((Identity)(ref identity)).Instance);
		WriteInt32(array, 143, ((IStats)deadNpc).Stats[(StatIds)360].Value);
		identity = ((IEntity)deadNpc).Identity;
		WriteInt32(array, 191, ((Identity)(ref identity)).Instance);
		WriteInt32(array, 199, corpseCatMesh);
		WriteInt32(array, 207, Math.Max(0, corpseCredits));
		WriteInt32(array, 336, corpseMonsterData);
		identity = ((IEntity)deadNpc).Identity;
		WriteInt32(array, 348, ((Identity)(ref identity)).Instance);
		return array;
	}

	private static byte[] BuildCapturedSubwayEumenides(ICharacter deadNpc, Identity corpseIdentity, Identity receiver, int serverId, int corpseCatMesh, int corpseMonsterData, int corpseCredits)
	{
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = (byte[])CapturedSubwayEumenidesTemplate.Clone();
		if (array.Length != 416)
		{
			throw new InvalidOperationException("Captured Subway Eumenides corpse template length changed.");
		}
		WritePacketLength(array, array.Length);
		WriteInt32(array, 8, serverId);
		WriteInt32(array, 12, ((Identity)(ref receiver)).Instance);
		WriteInt32(array, 24, ((Identity)(ref corpseIdentity)).Instance);
		WriteSingle(array, 45, ((IDynel)deadNpc).RawCoordinates.X);
		WriteSingle(array, 49, ((IDynel)deadNpc).RawCoordinates.Y);
		WriteSingle(array, 53, ((IDynel)deadNpc).RawCoordinates.Z);
		Identity identity = ((IEntity)((IInstancedEntity)deadNpc).Playfield).Identity;
		WriteInt32(array, 73, ((Identity)(ref identity)).Instance);
		WriteInt32(array, 143, ((IStats)deadNpc).Stats[(StatIds)360].Value);
		WriteInt32(array, 159, ((IStats)deadNpc).Stats[(StatIds)59].Value);
		WriteInt32(array, 167, ((IStats)deadNpc).Stats[(StatIds)4].Value);
		WriteInt32(array, 175, ((IStats)deadNpc).Stats[(StatIds)89].Value);
		identity = ((IEntity)deadNpc).Identity;
		WriteInt32(array, 191, ((Identity)(ref identity)).Instance);
		WriteInt32(array, 199, corpseCatMesh);
		WriteInt32(array, 207, Math.Max(0, corpseCredits));
		WriteInt32(array, 332, corpseMonsterData);
		identity = ((IEntity)deadNpc).Identity;
		WriteInt32(array, 344, ((Identity)(ref identity)).Instance);
		return array;
	}

	private static byte[] BuildCapturedSubwayAbmouth(ICharacter deadNpc, Identity corpseIdentity, Identity receiver, int serverId, int corpseCatMesh, int corpseMonsterData, int corpseCredits)
	{
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = (byte[])CapturedSubwayAbmouthTemplate.Clone();
		if (array.Length != 415)
		{
			throw new InvalidOperationException("Captured Subway Abmouth corpse template length changed.");
		}
		WritePacketLength(array, array.Length);
		WriteInt32(array, 8, serverId);
		WriteInt32(array, 12, ((Identity)(ref receiver)).Instance);
		WriteInt32(array, 24, ((Identity)(ref corpseIdentity)).Instance);
		WriteSingle(array, 45, ((IDynel)deadNpc).RawCoordinates.X);
		WriteSingle(array, 49, ((IDynel)deadNpc).RawCoordinates.Y);
		WriteSingle(array, 53, ((IDynel)deadNpc).RawCoordinates.Z);
		Identity identity = ((IEntity)((IInstancedEntity)deadNpc).Playfield).Identity;
		WriteInt32(array, 73, ((Identity)(ref identity)).Instance);
		identity = ((IEntity)deadNpc).Identity;
		WriteInt32(array, 191, ((Identity)(ref identity)).Instance);
		WriteInt32(array, 199, corpseCatMesh);
		WriteInt32(array, 207, Math.Max(0, corpseCredits));
		WriteInt32(array, 331, corpseMonsterData);
		identity = ((IEntity)deadNpc).Identity;
		WriteInt32(array, 343, ((Identity)(ref identity)).Instance);
		return array;
	}

	private static byte[] BuildCapturedSubwayThief(ICharacter deadNpc, Identity corpseIdentity, Identity receiver, int serverId, int corpseCatMesh, int corpseMonsterData, int corpseCredits)
	{
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = (byte[])CapturedSubwayThiefTemplate.Clone();
		if (array.Length != 412)
		{
			throw new InvalidOperationException("Captured Subway Thief corpse template length changed.");
		}
		WritePacketLength(array, array.Length);
		WriteInt32(array, 8, serverId);
		WriteInt32(array, 12, ((Identity)(ref receiver)).Instance);
		WriteInt32(array, 24, ((Identity)(ref corpseIdentity)).Instance);
		WriteSingle(array, 45, ((IDynel)deadNpc).RawCoordinates.X);
		WriteSingle(array, 49, ((IDynel)deadNpc).RawCoordinates.Y);
		WriteSingle(array, 53, ((IDynel)deadNpc).RawCoordinates.Z);
		Identity identity = ((IEntity)((IInstancedEntity)deadNpc).Playfield).Identity;
		WriteInt32(array, 73, ((Identity)(ref identity)).Instance);
		identity = ((IEntity)deadNpc).Identity;
		WriteInt32(array, 191, ((Identity)(ref identity)).Instance);
		WriteInt32(array, 199, corpseCatMesh);
		WriteInt32(array, 207, Math.Max(0, corpseCredits));
		WriteInt32(array, 324, corpseMonsterData);
		identity = ((IEntity)deadNpc).Identity;
		WriteInt32(array, 336, ((Identity)(ref identity)).Instance);
		return array;
	}

	private static byte[] BuildCapturedSubwayFilthFlea(ICharacter deadNpc, Identity corpseIdentity, Identity receiver, int serverId, int corpseCatMesh, int corpseMonsterData, int corpseCredits)
	{
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = (byte[])CapturedSubwayFilthFleaTemplate.Clone();
		if (array.Length != 457)
		{
			throw new InvalidOperationException("Captured Subway Filth Flea corpse template length changed.");
		}
		WritePacketLength(array, array.Length);
		WriteInt32(array, 8, serverId);
		WriteInt32(array, 12, ((Identity)(ref receiver)).Instance);
		WriteInt32(array, 24, ((Identity)(ref corpseIdentity)).Instance);
		WriteSingle(array, 45, ((IDynel)deadNpc).RawCoordinates.X);
		WriteSingle(array, 49, ((IDynel)deadNpc).RawCoordinates.Y);
		WriteSingle(array, 53, ((IDynel)deadNpc).RawCoordinates.Z);
		Identity identity = ((IEntity)((IInstancedEntity)deadNpc).Playfield).Identity;
		WriteInt32(array, 73, ((Identity)(ref identity)).Instance);
		identity = ((IEntity)deadNpc).Identity;
		WriteInt32(array, 191, ((Identity)(ref identity)).Instance);
		WriteInt32(array, 199, corpseCatMesh);
		WriteInt32(array, 207, Math.Max(0, corpseCredits));
		WriteInt32(array, 325, corpseMonsterData);
		identity = ((IEntity)deadNpc).Identity;
		WriteInt32(array, 337, ((Identity)(ref identity)).Instance);
		return array;
	}

	private static byte[] HexToBytes(string hex)
	{
		byte[] array = new byte[hex.Length / 2];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
		}
		return array;
	}

	private static void WriteInt32(byte[] buffer, int offset, int value)
	{
		byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
		Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
	}

	private static void WritePacketLength(byte[] buffer, int length)
	{
		buffer[6] = (byte)((uint)(length >> 8) & 0xFFu);
		buffer[7] = (byte)((uint)length & 0xFFu);
	}

	private static void WriteSingle(byte[] buffer, int offset, float value)
	{
		byte[] bytes = BitConverter.GetBytes(value);
		if (BitConverter.IsLittleEndian)
		{
			Array.Reverse(bytes);
		}
		Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
	}
}
