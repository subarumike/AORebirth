using System;
using System.Net;
using AORebirth.Core.Entities;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core;

internal static class PetSummonCaptureWireReplayer
{
	private const int ZoneServerSenderId = 854;

	private const int HeaderReceiverOffset = 12;

	private const int HeaderSenderOffset = 8;

	private const int N3IdentityInstanceOffset = 24;

	private const int AddPetUnknownOffset = 28;

	private const int AddPetPetIdentityOffset = 31;

	private const int StatValueOffset = 36;

	private const int ScfuPlayfieldOffset = 34;

	private const int ScfuCoordOffset = 38;

	private const int SetPosCoordOffset = 31;

	private const int SpellListBodyStartOffset = 31;

	private const int OwnerSpellListBodyIdentityOffset1 = 124;

	private const int OwnerSpellListBodyIdentityOffset2 = 132;

	private const int PetSpellListBodyIdentityOffset = 56;

	private static readonly byte[] ScfuBelamorte = Hex("00C7000A0001010B00000DAD35FE2868271B3A6B0000C3507957F058003A0A2A6A530015300B4372FE6C40D2C26C42D2494A0000000000000000000000003F800000000005E80A42656C616D6F72746500100812010000000060000B0000C0265700000177C10078001F000000001C0000000000000000000000000301000100010001000100000002000002E1000007E26D6574617065745F6865616C696E670000000000000000000000000000000000000467DA0000000000000001000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000003F1000000020000");

	private static readonly byte[] StatPetmaster = Hex("00C8000A0001002900000DAD35FE28682B333D6E0000C3507957F0580000000001000000C435FE2868");

	private static readonly byte[] AddPet = Hex("00C9000A0001002500000DAD35FE2868194E4F760000C35035FE2868010000C3507957F058");

	private static readonly byte[] StatFlags = Hex("00CA000A0001002900000DAD35FE28682B333D6E0000C3507957F05800000000010000000018081201");

	private static readonly byte[] SpellListOwner = Hex("00CB000A000100C600000DAD35FE28684D4501140000C35035FE286800000007E20000CFAF0001EB32000000040000000200000000000002D00000005D00000000000000000000002A000000010000000000000001000000094D543039000000C0FFFFFFFF00000000000000030000000000000000000000000001ADB1000000010000008300000080000000000000035100000351000000000000C35035FE28680000C35035FE286800001443616C6C696E67206F662042656C616D6F727465000000000000");

	private static readonly byte[] StatPetState = Hex("00CD000A0001002900000DAD35FE28682B333D6E0000C3507957F05800000000010000000500232801");

	private static readonly byte[] StatSide = Hex("00CE000A0001002900000DAD35FE28682B333D6E0000C3507957F05800000000010000002100000002");

	private static readonly byte[] StatBattleStationSide = Hex("00CF000A0001002900000DAD35FE28682B333D6E0000C3507957F05800000000010000029C00000000");

	private static readonly byte[] StatRunSpeed = Hex("00D0000A0001002900000DAD35FE28682B333D6E0000C3507957F05800000000010000009C0000054F");

	private static readonly byte[] StatExpansion = Hex("00D1000A0001002900000DAD35FE28682B333D6E0000C3507957F05800000000010000018500000001");

	private static readonly byte[] StatUnknownA = Hex("00D2000A0001002900000DAD35FE28682B333D6E0000C3507957F0580000000001000004A500000000");

	private static readonly byte[] StatUnknownB = Hex("00D3000A0001002900000DAD35FE28682B333D6E0000C3507957F0580000000001000004A000000000");

	private static readonly byte[] SetWantedDirection = Hex("00D4000A0001002900000DAD35FE286860201D0E0000C3507957F05800BF8000000000000000000000");

	private static readonly byte[] SpellListPetA = Hex("00D5000A0001006600000DAD35FE28684D4501140000C3507957F05800000007E20000CF2218620D8500000004000000000000000100000000000000000000000000000000000002B10000009600000000000000000000C3507957F058000000000000000000");

	private static readonly byte[] SpellListPetB = Hex("00D6000A0001006600000DAD35FE28684D4501140000C3507957F05800000007E20000CF2218620D8600000004000000000000000100000000000000000000000000000000000002170000007D00000000000000000000C3507957F058000000000000000000");

	private static readonly byte[] SetPos = Hex("005C000A0001002F00000DBC35FE2868195E496E0000C350795625EA00436830DA40C0A3D8432383BB010000000000");

	public static void EnqueueHealingPetSummonLink(ZoneClient ownerClient, ICharacter owner, Character petCharacter, uint mobFlags)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		if (ownerClient != null && owner != null && petCharacter != null)
		{
			Identity identity = ((IEntity)owner).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			identity = ((PooledObject)petCharacter).Identity;
			int instance2 = ((Identity)(ref identity)).Instance;
			EnqueueStatPetmaster(ownerClient, instance, instance2);
			EnqueueAddPet(ownerClient, instance, instance2);
			EnqueueStatFlags(ownerClient, instance2, mobFlags);
		}
	}

	public static void EnqueueHealingPetSummonPostStats(ZoneClient ownerClient, Character petCharacter)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (ownerClient != null && petCharacter != null)
		{
			Identity identity = ((PooledObject)petCharacter).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			EnqueueStatPetState(ownerClient, instance);
			EnqueueStatSide(ownerClient, instance);
			EnqueueStatTemplate(ownerClient, StatBattleStationSide, instance, "battlestationside");
			EnqueueStatTemplate(ownerClient, StatRunSpeed, instance, "runspeed");
			EnqueueStatTemplate(ownerClient, StatExpansion, instance, "expansion");
			EnqueueStatTemplate(ownerClient, StatUnknownA, instance, "statA");
			EnqueueStatTemplate(ownerClient, StatUnknownB, instance, "statB");
		}
	}

	public static void SendBelamorteScfuToOwner(ZoneClient ownerClient, ICharacter owner, Character petCharacter)
	{
		SendHealingPetScfuToOwner(ownerClient, owner, petCharacter, "BSLX");
	}

	public static void SendHealingPetScfuToOwner(ZoneClient ownerClient, ICharacter owner, Character petCharacter, string petHash)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if (ownerClient != null && owner != null && petCharacter != null)
		{
			Identity identity = ((IEntity)owner).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			identity = ((PooledObject)petCharacter).Identity;
			int instance2 = ((Identity)(ref identity)).Instance;
			identity = ((IEntity)((IInstancedEntity)owner).Playfield).Identity;
			int instance3 = ((Identity)(ref identity)).Instance;
			Coordinate petCoord = ((Dynel)petCharacter).Coordinates();
			EnqueueScfuForRebirth(ownerClient, owner, instance, instance2, instance3, petCoord, petHash, null);
		}
	}

	public static void SendHealingPetScfuToOwner(ZoneClient ownerClient, ICharacter owner, Character petCharacter, string spawnPetHash, string scfuTemplateHash)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if (ownerClient != null && owner != null && petCharacter != null)
		{
			Identity identity = ((IEntity)owner).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			identity = ((PooledObject)petCharacter).Identity;
			int instance2 = ((Identity)(ref identity)).Instance;
			identity = ((IEntity)((IInstancedEntity)owner).Playfield).Identity;
			int instance3 = ((Identity)(ref identity)).Instance;
			Coordinate petCoord = ((Dynel)petCharacter).Coordinates();
			EnqueueScfuForRebirth(ownerClient, owner, instance, instance2, instance3, petCoord, spawnPetHash, scfuTemplateHash);
		}
	}

	public static void ReplayBelamorteSummonPostScfu(ZoneClient ownerClient, ICharacter owner, Character petCharacter, uint mobFlags)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (ownerClient != null && owner != null && petCharacter != null)
		{
			Identity identity = ((IEntity)owner).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			identity = ((PooledObject)petCharacter).Identity;
			int instance2 = ((Identity)(ref identity)).Instance;
			EnqueueOwnerSpellList(ownerClient, instance);
			EnqueueStatPetState(ownerClient, instance2);
			EnqueueStatSide(ownerClient, instance2);
			EnqueueStatTemplate(ownerClient, StatBattleStationSide, instance2, "battlestationside");
			EnqueueStatTemplate(ownerClient, StatRunSpeed, instance2, "runspeed");
			EnqueueStatTemplate(ownerClient, StatExpansion, instance2, "expansion");
			EnqueueStatTemplate(ownerClient, StatUnknownA, instance2, "statA");
			EnqueueStatTemplate(ownerClient, StatUnknownB, instance2, "statB");
			EnqueueSetWantedDirection(ownerClient, instance, instance2);
			EnqueuePetSpellList(ownerClient, instance, instance2, SpellListPetA);
			EnqueuePetSpellList(ownerClient, instance, instance2, SpellListPetB);
		}
	}

	public static void ReplayBelamorteSummonSafe(ZoneClient ownerClient, ICharacter owner, Character petCharacter, uint mobFlags)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		if (ownerClient != null && owner != null && petCharacter != null)
		{
			Identity identity = ((IEntity)owner).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			identity = ((PooledObject)petCharacter).Identity;
			int instance2 = ((Identity)(ref identity)).Instance;
			Coordinate val = ((Dynel)petCharacter).Coordinates();
			EnqueueScfuMinimal(ownerClient, instance, instance2);
			EnqueueStatPetmaster(ownerClient, instance, instance2);
			EnqueueAddPet(ownerClient, instance, instance2);
			EnqueueStatFlags(ownerClient, instance2, mobFlags);
			EnqueueOwnerSpellList(ownerClient, instance);
			EnqueueStatPetState(ownerClient, instance2);
			EnqueueStatSide(ownerClient, instance2);
			EnqueueStatTemplate(ownerClient, StatBattleStationSide, instance2, "battlestationside");
			EnqueueStatTemplate(ownerClient, StatRunSpeed, instance2, "runspeed");
			EnqueueStatTemplate(ownerClient, StatExpansion, instance2, "expansion");
			EnqueueStatTemplate(ownerClient, StatUnknownA, instance2, "statA");
			EnqueueStatTemplate(ownerClient, StatUnknownB, instance2, "statB");
			EnqueueSetWantedDirection(ownerClient, instance, instance2);
			EnqueuePetSpellList(ownerClient, instance, instance2, SpellListPetA);
			EnqueuePetSpellList(ownerClient, instance, instance2, SpellListPetB);
		}
	}

	public static void ReplayBelamorteSummonAfterScfu(ZoneClient ownerClient, ICharacter owner, Character petCharacter)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (ownerClient != null && owner != null && petCharacter != null)
		{
			Identity identity = ((IEntity)owner).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			identity = ((PooledObject)petCharacter).Identity;
			int instance2 = ((Identity)(ref identity)).Instance;
			EnqueueOwnerSpellList(ownerClient, instance);
			EnqueueStatPetState(ownerClient, instance2);
			EnqueueStatSide(ownerClient, instance2);
			EnqueueStatTemplate(ownerClient, StatBattleStationSide, instance2, "battlestationside");
			EnqueueStatTemplate(ownerClient, StatRunSpeed, instance2, "runspeed");
			EnqueueStatTemplate(ownerClient, StatExpansion, instance2, "expansion");
			EnqueueStatTemplate(ownerClient, StatUnknownA, instance2, "statA");
			EnqueueStatTemplate(ownerClient, StatUnknownB, instance2, "statB");
			EnqueueSetWantedDirection(ownerClient, instance, instance2);
			EnqueuePetSpellList(ownerClient, instance, instance2, SpellListPetA);
			EnqueuePetSpellList(ownerClient, instance, instance2, SpellListPetB);
		}
	}

	public static void ReplayBelamorteSummon(ZoneClient ownerClient, ICharacter owner, Character petCharacter, uint mobFlags)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		if (ownerClient != null && owner != null && petCharacter != null)
		{
			Identity identity = ((IEntity)owner).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			identity = ((PooledObject)petCharacter).Identity;
			int instance2 = ((Identity)(ref identity)).Instance;
			identity = ((IEntity)((IInstancedEntity)owner).Playfield).Identity;
			int instance3 = ((Identity)(ref identity)).Instance;
			Coordinate petCoord = ((Dynel)petCharacter).Coordinates();
			EnqueueScfu(ownerClient, instance, instance2, instance3, petCoord);
			EnqueueStatPetmaster(ownerClient, instance, instance2);
			EnqueueAddPet(ownerClient, instance, instance2);
			EnqueueStatFlags(ownerClient, instance2, mobFlags);
			EnqueueOwnerSpellList(ownerClient, instance);
			EnqueueStatPetState(ownerClient, instance2);
			EnqueueStatSide(ownerClient, instance2);
			EnqueueStatTemplate(ownerClient, StatBattleStationSide, instance2, "battlestationside");
			EnqueueStatTemplate(ownerClient, StatRunSpeed, instance2, "runspeed");
			EnqueueStatTemplate(ownerClient, StatExpansion, instance2, "expansion");
			EnqueueStatTemplate(ownerClient, StatUnknownA, instance2, "statA");
			EnqueueStatTemplate(ownerClient, StatUnknownB, instance2, "statB");
			EnqueueSetWantedDirection(ownerClient, instance, instance2);
			EnqueuePetSpellList(ownerClient, instance, instance2, SpellListPetA);
			EnqueuePetSpellList(ownerClient, instance, instance2, SpellListPetB);
		}
	}

	private static void EnqueueScfuForRebirth(ZoneClient ownerClient, ICharacter owner, int ownerInstance, int petInstance, int playfieldId, Coordinate petCoord, string spawnPetHash, string scfuTemplateHash)
	{
		string text = scfuTemplateHash;
		if (string.IsNullOrWhiteSpace(text))
		{
			text = (SoothingSpiritsHealPetLadder.IsSoothingSpiritsUpgradeHash(spawnPetHash) ? "MT02" : spawnPetHash);
		}
		if (!PetHealingPetScfuCatalog.TryGetScfuWire(text, out var scfuWire))
		{
			scfuWire = (byte[])ScfuBelamorte.Clone();
		}
		byte[] array = (byte[])scfuWire.Clone();
		int textureId = SoothingSpiritsHealPetLadder.ResolveTextureIdFromSpawnHash(spawnPetHash, owner);
		SoothingSpiritsHealPetLadder.TryPatchMetapetHealingTexture(array, textureId);
		PatchHeader(array, ownerInstance);
		WriteInt32BigEndian(array, 24, petInstance);
		WriteInt32BigEndian(array, 34, playfieldId);
		WriteFloat(array, 38, petCoord.x);
		WriteFloat(array, 42, petCoord.y);
		WriteFloat(array, 46, petCoord.z);
		Enqueue(ownerClient, array, "SCFU");
	}

	private static void EnqueueScfuMinimal(ZoneClient ownerClient, int ownerInstance, int petInstance)
	{
		byte[] array = (byte[])ScfuBelamorte.Clone();
		PatchHeader(array, ownerInstance);
		WriteInt32BigEndian(array, 24, petInstance);
		Enqueue(ownerClient, array, "SCFU");
	}

	private static void EnqueueScfu(ZoneClient ownerClient, int ownerInstance, int petInstance, int playfieldId, Coordinate petCoord)
	{
		byte[] array = (byte[])ScfuBelamorte.Clone();
		PatchHeader(array, ownerInstance);
		WriteInt32BigEndian(array, 24, petInstance);
		WriteInt32BigEndian(array, 34, playfieldId);
		WriteFloat(array, 38, petCoord.x);
		WriteFloat(array, 42, petCoord.y);
		WriteFloat(array, 46, petCoord.z);
		Enqueue(ownerClient, array, "SCFU");
	}

	private static void EnqueueStatPetmaster(ZoneClient ownerClient, int ownerInstance, int petInstance)
	{
		byte[] array = (byte[])StatPetmaster.Clone();
		PatchHeader(array, ownerInstance);
		WriteInt32BigEndian(array, 24, petInstance);
		WriteInt32BigEndian(array, 36, ownerInstance);
		Enqueue(ownerClient, array, "Stat-petmaster");
	}

	private static void EnqueueAddPet(ZoneClient ownerClient, int ownerInstance, int petInstance)
	{
		byte[] array = (byte[])AddPet.Clone();
		PatchHeader(array, ownerInstance);
		WriteInt32BigEndian(array, 24, ownerInstance);
		WriteUInt16LittleEndian(array, 28, 1);
		PatchCompactBodyIdentity(array, 31, petInstance);
		Enqueue(ownerClient, array, "AddPet");
	}

	private static void EnqueueStatFlags(ZoneClient ownerClient, int petInstance, uint mobFlags)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)ownerClient.Controller.Character).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		byte[] array = (byte[])StatFlags.Clone();
		PatchHeader(array, instance);
		WriteInt32BigEndian(array, 24, petInstance);
		WriteInt32BigEndian(array, 36, (int)mobFlags);
		Enqueue(ownerClient, array, "Stat-flags");
	}

	private static void EnqueueOwnerSpellList(ZoneClient ownerClient, int ownerInstance)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = (byte[])SpellListOwner.Clone();
		PatchHeader(array, ownerInstance);
		WriteInt32BigEndian(array, 24, ownerInstance);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = ownerInstance;
		Identity identity = val;
		PatchBodyIdentity(array, 155, identity);
		PatchBodyIdentity(array, 163, identity);
		Enqueue(ownerClient, array, "SpellList-owner");
	}

	private static void EnqueueStatPetState(ZoneClient ownerClient, int petInstance)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = (byte[])StatPetState.Clone();
		Identity identity = ((IEntity)ownerClient.Controller.Character).Identity;
		PatchHeader(array, ((Identity)(ref identity)).Instance);
		WriteInt32BigEndian(array, 24, petInstance);
		Enqueue(ownerClient, array, "Stat-petstate");
	}

	private static void EnqueueStatSide(ZoneClient ownerClient, int petInstance)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = (byte[])StatSide.Clone();
		Identity identity = ((IEntity)ownerClient.Controller.Character).Identity;
		PatchHeader(array, ((Identity)(ref identity)).Instance);
		WriteInt32BigEndian(array, 24, petInstance);
		Enqueue(ownerClient, array, "Stat-side");
	}

	private static void EnqueueStatTemplate(ZoneClient ownerClient, byte[] template, int petInstance, string label)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = (byte[])template.Clone();
		Identity identity = ((IEntity)ownerClient.Controller.Character).Identity;
		PatchHeader(array, ((Identity)(ref identity)).Instance);
		WriteInt32BigEndian(array, 24, petInstance);
		Enqueue(ownerClient, array, "Stat-" + label);
	}

	public static void EnqueueSetWantedDirection(ZoneClient ownerClient, int ownerInstance, int petInstance)
	{
		byte[] array = (byte[])SetWantedDirection.Clone();
		PatchHeader(array, ownerInstance);
		WriteInt32BigEndian(array, 24, petInstance);
		Enqueue(ownerClient, array, "SetWantedDirection");
	}

	private static void EnqueuePetSpellList(ZoneClient ownerClient, int ownerInstance, int petInstance, byte[] template)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = (byte[])template.Clone();
		PatchHeader(array, ownerInstance);
		WriteInt32BigEndian(array, 24, petInstance);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = petInstance;
		Identity identity = val;
		PatchBodyIdentity(array, 87, identity);
		Enqueue(ownerClient, array, "SpellList-pet");
	}

	private static void EnqueueSetPos(ZoneClient ownerClient, int ownerInstance, int petInstance, Coordinate petCoord)
	{
		byte[] array = (byte[])SetPos.Clone();
		PatchHeader(array, ownerInstance);
		WriteInt32BigEndian(array, 24, petInstance);
		WriteFloat(array, 31, petCoord.x);
		WriteFloat(array, 35, petCoord.y);
		WriteFloat(array, 39, petCoord.z);
		Enqueue(ownerClient, array, "SetPos");
	}

	private static void Enqueue(ZoneClient ownerClient, byte[] packet, string label)
	{
		((ClientBase)ownerClient).Server.Info((IClient)(object)ownerClient, "SummonWireSend {0} len={1}", new object[2] { label, packet.Length });
		ownerClient.EnqueueOutboundCompressedBuffer(packet);
	}

	private static void PatchHeader(byte[] packet, int ownerInstance)
	{
		WriteInt32BigEndian(packet, 8, 854);
		WriteInt32BigEndian(packet, 12, ownerInstance);
		ushort num = (ushort)packet.Length;
		packet[6] = (byte)(num >> 8);
		packet[7] = (byte)num;
	}

	private static void PatchCompactBodyIdentity(byte[] packet, int offset, int instance)
	{
		WriteUInt16BigEndian(packet, offset, 50000);
		WriteInt32LittleEndian(packet, offset + 2, instance);
	}

	private static void WriteUInt16LittleEndian(byte[] buffer, int offset, ushort value)
	{
		buffer[offset] = (byte)value;
		buffer[offset + 1] = (byte)(value >> 8);
	}

	private static void PatchBodyIdentity(byte[] packet, int offset, Identity identity)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		WriteUInt16BigEndian(packet, offset, (ushort)((Identity)(ref identity)).Type);
		WriteInt32LittleEndian(packet, offset + 2, ((Identity)(ref identity)).Instance);
		WriteUInt16BigEndian(packet, offset + 6, 0);
	}

	private static void WriteIdentityBigEndian(byte[] packet, int offset, IdentityType type, int instance)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Expected I4, but got Unknown
		WriteInt32BigEndian(packet, offset, (int)type);
		WriteInt32BigEndian(packet, offset + 4, instance);
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
