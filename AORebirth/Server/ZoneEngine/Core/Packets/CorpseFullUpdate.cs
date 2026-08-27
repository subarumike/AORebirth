namespace ZoneEngine.Core.Packets
{
    using System;
    using System.Net;
    using System.Text;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Playfields;

    public static class CorpseFullUpdate
    {
        private const int OriginalEncodedNameLength = 27;
        private const int NameOffset = 231;
        private const int NameLengthOffset = 227;
        private const int OriginalSuffixOffset = NameOffset + OriginalEncodedNameLength;

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
        // The captured template encodes corpse cash as stat id 61 at offset 203
        // followed by its 32-bit value at offset 207. The old template value was
        // hardcoded to 111, so this offset must patch the value word, not the
        // following word at 211.
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

        // Capture 20260722-210219 / 20260722-cap-mob-drop-cred: Waste Collector corpse.
        // Generic Rhinoman template lacks Material #22 → client shows male body / unusable corpse.
        private const int CapturedAreteWasteMonsterData = 17714;
        private const int CapturedAreteWasteOriginalEncodedNameLength = 38;
        private const int CapturedAreteWasteMonsterDataOffset = 341;
        private const int CapturedAreteWasteTailDeadNpcInstanceOffset = 353;
        private const int CapturedAreteWasteOriginalSuffixOffset =
            NameOffset + CapturedAreteWasteOriginalEncodedNameLength;

        // Capture 20260723-225021 corpse-full-updates Remains of Barking Chimera (PacketLength=462).
        // Generic Rhinoman template ends without the ExtTex/material tail; CatMesh 208966 then skins
        // as default lava orange/red. Live corpse packet carries the same "low2" ExtTex as living SCFU.
        private const int CapturedBarkingChimeraMonsterData = 209173;
        private const int CapturedBarkingChimeraOriginalEncodedNameLength = 27;
        private const int CapturedBarkingChimeraMonsterDataOffset = 330;
        private const int CapturedBarkingChimeraTailDeadNpcInstanceOffset = 342;
        private const int CapturedBarkingChimeraOriginalSuffixOffset =
            NameOffset + CapturedBarkingChimeraOriginalEncodedNameLength;

        private static readonly byte[] Template = HexToBytes(
            "0000000a0001019e000000003cac6f144f474e050000c76a00f0f00100000000080000000b00000000000000004504a4df41c5ea1244cb530d000000003e8fb30a000000003f75b5e0000002350000000000000000006f000046f200000000001818050000001700000000000002bd00000000000002be00000000000002bf000000000000019c000000010000016800000062000000df000000000000003b00000003000000040000000700000059000000010000019f0000c350000001a0776b95780000002a0000797e0000003d000000000000000800004650000000220000003c0000001b52656d61696e73206f66205268696e6f6d616e204d6f74686572000000000200000032000003f100000003000007e20000cf2738f46cbe0000000400000000000000010000000000000000000000000000000000000000000001f700000001000000040000798a000000000000c350776b9578000017a600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000000");

        // Official live Subway capture 20260709-164414, CorpseFullUpdate packet #587.
        // The flea corpse requires the captured Material #9 visual tail; the generic
        // corpse template is shorter and does not create a visible flea body.
        private static readonly byte[] CapturedSubwayFilthFleaTemplate = HexToBytes(
            "0000000A000101C900000DB970CBBEF34F474E050000C76A00F6E00900000000080000000B000000000000000042B45D5F42E73AE14397BF79000000"
            + "00BED08F91800000003F69CC57001220020000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00"
            + "000000000002BF000000000000019C000000010000016800000082000000DF000000000000003B000000000000000400000006000000590000000100"
            + "00019F0000C350000001A079528CA70000002A00003B7F0000003D000000170000000800004650000000220000003C0000001652656D61696E73206F"
            + "662046696C746820466C6561000000000200000032000003F100000003000007E20000CF273975F70E00000004000000000000000100000000000000"
            + "00000000000000000000000000000001F70000000100000004000044F9000000000000C35079528CA7000017A6000000000000000000000000000000"
            + "01000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000001000007E24D617465726961"
            + "6C20233900000000000000000000000000000000000000000000003B810000000000000001");

        // Official live Subway capture 20260710-205400, CorpseFullUpdate packet #1580.
        // This exact Thief corpse includes the captured CATMesh and material tail. The
        // generic MonsterData-as-CATMesh fallback crashes the current client renderer.
        private static readonly byte[] CapturedSubwayThiefTemplate = HexToBytes(
            "0000000A0001019C00000DB47944C0654F474E050000C76A00F6C00400000000080000000B0000000000000000428C26A642E73ACB439F59EA00000000"
            + "3F19FB87000000003F4C83360014D00E0000000000000000006F00004AE300000000001818050000001700000000000002BD00000000000002BE00000000"
            + "000002BF000000000000019C00000001000001680000005D000000DF000000010000003B00000002000000040000000100000059000000010000019F0000"
            + "C350000001A07957E61A0000002A000017130000003D0000001D0000000800004650000000220000003C00000040000273310000001152656D61696E73206F"
            + "66205468696566000000000200000032000003F100000003000007E20000CF273978332B000000040000000000000001000000000000000000000000000000"
            + "0000000000000001F70000000100000004000065EC000000000000C3507957E61A000017A600000000000024CA000000000000000100002219000000000000"
            + "0002000024CC0000000000000003000024CB0000000000000004000024CD0000000000000000");

        // Official live Subway capture 20260712-232137, CorpseFullUpdate packet #3124.
        // Keep the captured boss body and material tail intact; only runtime identity,
        // position, playfield, CATMesh, credits, and MonsterData fields are patched.
        private static readonly byte[] CapturedSubwayAbmouthTemplate = HexToBytes(
            "0E6B000A0001019F00000DB47944C0654F474E050000C76A00F6C00200000000080000000B000000000000000043AA09E642933C6442C459E000000000"
            + "BF7C21E7000000003E3152F8001530080000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000"
            + "000002BF000000000000019C0000000100000168000000A2000000DF000000000000003B00000000000000040000000600000059000000010000019F0000"
            + "C350000001A079607A350000002A00025F9C0000003D0000024B000000080002BF20000000220000003C0000001C52656D61696E73206F662041626D6F7574"
            + "682053757072656D7573000000000200000032000003F100000003000007E20000CF27397C2768000000040000000000000001000000000000000000000000"
            + "0000000000000000000001F400000001000000040002613A000000000000C35079607A35000017A60000000000000000000000000000000100000000000000"
            + "0000000002000000000000000000000003000000000000000000000004000000000000000000000000");

        // Official live Subway capture 20260712-234401, CorpseFullUpdate packet #735.
        // Preserve Vergil's exact 420-byte boss corpse, CATMesh 5921, body fields,
        // and visual tail while patching runtime identity, location, credits, and
        // MonsterData fields.
        private static readonly byte[] CapturedSubwayVergilTemplate = HexToBytes(
            "06ED000A000101A400000DB47944C0654F474E050000C76A00F6C01400000000080000000B0000000000000000438C819A4292093142C62FE2000000"
            + "00BF2D43E6800000003F3C7465001530080000000000000000006F00004AE300000000001818050000001700000000000002BD00000000000002BE00"
            + "000000000002BF000000000000019C000000010000016800000084000000DF000000010000003B000000020000000400000003000000590000000100"
            + "00019F0000C350000001A079607AE50000002A000017210000003D0000024B000000080002BF20000000220000003C0000004000009CEB0000001952"
            + "656D61696E73206F662056657267696C2041656E656964000000000200000032000003F100000003000007E20000CF27397C279A0000000400000000"
            + "000000010000000000000000000000000000000000000000000001F4000000010000000400031BE4000000000000C35079607AE5000017A600000000"
            + "0001CB9500000000000000010000258900000000000000020000258F0000000000000003000025870000000000000004000025960000000000000000");

        // L7 gold 20260725-002423 Remains of Tilda Konecny (CATMesh 5934, MD 26137, 420 bytes).
        // Same field offsets as Vergil; Thief body mismatched mission trash and corpses vanished.
        private static readonly byte[] CapturedMissionTrashCorpseTemplate = HexToBytes(
            "03E8000A000101A400000DB4797E30D74F474E050000C76A00F7482700000000080000000B0000000000000000437FA2D540"
            + "A051EC436AFD7100000000BF484630000000003F1F74C5001608000000000000000000006F00004AE3000000000018180500"
            + "00001700000000000002BD00000000000002BE00000000000002BF000000000000019C00000001000001680000005D000000"
            + "DF000000010000003B00000003000000040000000200000059000000010000019F0000C350000001A0799361EA0000002A00"
            + "00172E0000003D0000001D0000000800004650000000220000003C0000004000009D110000001952656D61696E73206F6620"
            + "54696C6461204B6F6E65636E79000000000200000032000003F100000003000007E20000CF273995AE700000000400000000"
            + "000000010000000000000000000000000000000000000000000001F5000000010000000400006619000000000000C3507993"
            + "61EA000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030001558E"
            + "0000000000000004000058630000000000000000");

        // Official live Subway capture 20260716-222007, CorpseFullUpdate packet #198.
        // Preserve Eumenides' exact 416-byte body, CATMesh 17905, MonsterData,
        // scale, breed/sex/race fields, and visual tail while patching runtime state.
        private static readonly byte[] CapturedSubwayEumenidesTemplate = HexToBytes(
            "03FA000A000101A000000DAD7944C0654F474E050000C76A00F6900600000000080000000B000000000000000043687A414291A1FB42358691000000"
            + "00BF24B2D5800000003F43FC550015781E0000000000000000006F00004AE300000000001818050000001700000000000002BD00000000000002BE00"
            + "000000000002BF000000000000019C000000010000016800000082000000DF000000010000003B000000020000000400000003000000590000000100"
            + "00019F0000C350000001A0797022340000002A000045F10000003D000000BA0000000800007210000000220000003C000000400000740C0000001552"
            + "656D61696E73206F662045756D656E69646573000000000200000032000003F100000003000007E20000CF273983C39B000000040000000000000001"
            + "0000000000000000000000000000000000000000000001F6000000010000000400031BCE000000000000C35079702234000017A60000000000002594"
            + "00000000000000010000258C0000000000000002000025920000000000000003000185C30000000000000004000025990000000000000000");

        // Capture 20260722-210219 corpse-full-updates Supreme Collector of Waste (Material #22).
        // Leading 0000 pads AOSharp seq-stripped 000A… frame to CorpseFullUpdate template layout.
        private static readonly byte[] CapturedAreteWasteTemplate = HexToBytes(
            "0000000A000101D900000DC1797E30D74F474E050000C76A00F5F80B00000000080000000B00000000000000004559EB4F410825574469553700000000"
            + "BF5BD8DB000000003F032943000FF02D0000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE000000"
            + "00000002BF000000000000019C0000000100000168000000A0000000DF000000000000003B00000001000000040000000600000059000000010000019F"
            + "0000C350000001A0798A239E0000002A000043A40000003D000000000000000800020788000000220000003C0000002652656D61696E73206F66205375"
            + "7072656D6520436F6C6C6563746F72206F66205761737465000000000200000032000003F100000003000007E20000CF2739917EDF0000000400000000"
            + "000000010000000000000000000000000000000000000000000001F6000000010000000400004532000000000000C350798A239E000017A60000000000"
            + "00000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000000010000"
            + "07E24D6174657269616C2023323200000000000000000000000000000000000000000001768D0000000000000001");

        // Capture 20260723-225021 corpse-full-updates row 798C1F4F. Leading 0000 pads AOSharp
        // seq-stripped 000A… frame. Tail ExtTex "low2" + 0x33049 matches living Barking Chimera SCFU.
        private static readonly byte[] CapturedBarkingChimeraTemplate = HexToBytes(
            "0000000A000101CE00000DB0797E30D74F474E050000C76A00F4980300000000080000000B0000000000000000444910B441F123074493DD47000000"
            + "003EFFC87A000000003F5DC3DC000010D60000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00"
            + "000000000002BF000000000000019C00000001000001680000005E000000DF000000000000003B000000000000000400000006000000590000000100"
            + "00019F0000C350000001A0798C1F4F0000002A000330460000003D000000000000000800004650000000220000003C0000001B52656D61696E73206F"
            + "66204261726B696E67204368696D657261000000000200000032000003F100000003000007E20000CF273993159D0000000400000000000000010000"
            + "000000000000000000000000000000000000000001F5000000010000000400033115000000000000C350798C1F4F000017A600000000000000000000"
            + "000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000001000007E26C6F"
            + "773200000000000000000000000000000000000000000000000000000000000330490000000000000001");

        public static byte[] Build(
            ICharacter deadNpc,
            Identity corpseIdentity,
            Identity receiver,
            int serverId,
            int corpseCatMesh,
            int corpseMonsterData,
            int corpseCredits)
        {
            if (deadNpc != null
                && corpseMonsterData == NpcCombatAttackRules.CapturedSubwayVergilMonsterData)
            {
                return BuildCapturedSubwayVergil(
                    deadNpc,
                    corpseIdentity,
                    receiver,
                    serverId,
                    corpseCatMesh,
                    corpseMonsterData,
                    corpseCredits);
            }

            if (deadNpc != null
                && corpseMonsterData == NpcCombatAttackRules.CapturedSubwayEumenidesMonsterData)
            {
                return BuildCapturedSubwayEumenides(
                    deadNpc,
                    corpseIdentity,
                    receiver,
                    serverId,
                    corpseCatMesh,
                    corpseMonsterData,
                    corpseCredits);
            }

            CapturedEncounterRuntimeDefinition encounterRuntime;
            if (deadNpc != null
                && CapturedEncounterRuntimeRegistry.TryGet(
                    deadNpc.Identity.Instance,
                    out encounterRuntime)
                && encounterRuntime.IsBoss
                && string.Equals(
                    encounterRuntime.ProfileKey,
                    AbmouthEncounterRuntimeService.AbmouthProfileKey,
                    StringComparison.Ordinal))
            {
                return BuildCapturedSubwayAbmouth(
                    deadNpc,
                    corpseIdentity,
                    receiver,
                    serverId,
                    corpseCatMesh,
                    corpseMonsterData,
                    corpseCredits);
            }

            OrdinaryEnemyRuntimeDefinition ordinaryRuntime = null;
            bool hasOrdinaryRuntime = deadNpc != null
                && OrdinaryEnemyRuntimeRegistry.TryGet(
                    deadNpc.Identity.Instance,
                    out ordinaryRuntime);
            if (hasOrdinaryRuntime
                && ordinaryRuntime.Profile.Corpse.PacketProfile
                == OrdinaryEnemyCorpsePacketProfile.CapturedThief)
            {
                return BuildCapturedSubwayThief(
                    deadNpc,
                    corpseIdentity,
                    receiver,
                    serverId,
                    corpseCatMesh,
                    corpseMonsterData,
                    corpseCredits);
            }

            if (hasOrdinaryRuntime
                && ordinaryRuntime.Profile.Corpse.PacketProfile
                == OrdinaryEnemyCorpsePacketProfile.CapturedFilthFlea)
            {
                return BuildCapturedSubwayFilthFlea(
                    deadNpc,
                    corpseIdentity,
                    receiver,
                    serverId,
                    corpseCatMesh,
                    corpseMonsterData,
                    corpseCredits);
            }

            if (deadNpc != null && corpseMonsterData == CapturedAreteWasteMonsterData)
            {
                return BuildCapturedAreteWaste(
                    deadNpc,
                    corpseIdentity,
                    receiver,
                    serverId,
                    corpseCatMesh,
                    corpseMonsterData,
                    corpseCredits);
            }

            if (deadNpc != null && corpseMonsterData == CapturedBarkingChimeraMonsterData)
            {
                return BuildCapturedBarkingChimera(
                    deadNpc,
                    corpseIdentity,
                    receiver,
                    serverId,
                    corpseCatMesh,
                    corpseMonsterData,
                    corpseCredits);
            }

            // Prefer D2 capture templates on the D2 playfield (shared names/MDs with D1).
            byte[] nascenceDungeonCorpse = NascenceDungeon2CorpseCapture.TryBuild(
                deadNpc,
                corpseIdentity,
                receiver,
                serverId,
                corpseCatMesh,
                corpseMonsterData,
                corpseCredits);
            if (nascenceDungeonCorpse != null)
            {
                return nascenceDungeonCorpse;
            }

            nascenceDungeonCorpse = NascenceDungeon1CorpseCapture.TryBuild(
                deadNpc,
                corpseIdentity,
                receiver,
                serverId,
                corpseCatMesh,
                corpseMonsterData,
                corpseCredits);
            if (nascenceDungeonCorpse != null)
            {
                return nascenceDungeonCorpse;
            }

            // L7 gold 20260725-002423 Tilda Konecny corpse body (not Thief — corpses vanished).
            if (deadNpc != null
                && ZoneEngine.Core.Missions.MissionInstanceMobCombat.IsAggressive(deadNpc.Identity))
            {
                return BuildCapturedMissionTrashCorpse(
                    deadNpc,
                    corpseIdentity,
                    receiver,
                    serverId,
                    corpseCatMesh,
                    corpseMonsterData,
                    corpseCredits);
            }

            string corpseName = "Remains of " + deadNpc.Name;
            byte[] nameBytes = Encoding.ASCII.GetBytes(corpseName);
            int encodedNameLength = nameBytes.Length + 1;

            // CorpseFullUpdate resumes immediately after the encoded string's trailing null.
            // Padding this to four bytes shifts the animation/identity tail and the client
            // never registers the spawned corpse dynel.
            int newSuffixOffset = NameOffset + encodedNameLength;
            int afterNameDelta = newSuffixOffset - OriginalSuffixOffset;
            byte[] buffer = new byte[Template.Length + afterNameDelta];

            Buffer.BlockCopy(Template, 0, buffer, 0, NameOffset);
            Buffer.BlockCopy(nameBytes, 0, buffer, NameOffset, nameBytes.Length);
            Buffer.BlockCopy(
                Template,
                OriginalSuffixOffset,
                buffer,
                newSuffixOffset,
                Template.Length - OriginalSuffixOffset);

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, ServerIdOffset, serverId);
            WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);
            WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);
            WriteSingle(buffer, PositionXOffset, deadNpc.RawCoordinates.X);
            WriteSingle(buffer, PositionYOffset, deadNpc.RawCoordinates.Y);
            WriteSingle(buffer, PositionZOffset, deadNpc.RawCoordinates.Z);
            WriteInt32(buffer, PlayfieldIdOffset, deadNpc.Playfield.Identity.Instance);
            WriteInt32(buffer, MonsterScaleOffset, deadNpc.Stats[StatIds.monsterscale].Value);
            WriteInt32(buffer, SexOffset, deadNpc.Stats[StatIds.sex].Value);
            WriteInt32(buffer, BreedOffset, deadNpc.Stats[StatIds.breed].Value);
            WriteInt32(buffer, RaceOffset, deadNpc.Stats[StatIds.race].Value);
            WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);
            WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh);
            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
            WriteInt32(buffer, NameLengthOffset, encodedNameLength);
            WriteInt32(buffer, CorpseMonsterDataOffset + afterNameDelta, corpseMonsterData);
            WriteInt32(buffer, TailDeadNpcInstanceOffset + afterNameDelta, deadNpc.Identity.Instance);

            return buffer;
        }

        private static byte[] BuildCapturedSubwayVergil(
            ICharacter deadNpc,
            Identity corpseIdentity,
            Identity receiver,
            int serverId,
            int corpseCatMesh,
            int corpseMonsterData,
            int corpseCredits)
        {
            byte[] buffer = (byte[])CapturedSubwayVergilTemplate.Clone();
            if (buffer.Length != CapturedSubwayVergilPacketLength)
            {
                throw new InvalidOperationException("Captured Subway Vergil corpse template length changed.");
            }

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, ServerIdOffset, serverId);
            WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);
            WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);
            WriteSingle(buffer, PositionXOffset, deadNpc.RawCoordinates.X);
            WriteSingle(buffer, PositionYOffset, deadNpc.RawCoordinates.Y);
            WriteSingle(buffer, PositionZOffset, deadNpc.RawCoordinates.Z);
            WriteInt32(buffer, PlayfieldIdOffset, deadNpc.Playfield.Identity.Instance);
            WriteInt32(buffer, MonsterScaleOffset, deadNpc.Stats[StatIds.monsterscale].Value);
            WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);
            WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh);
            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
            WriteInt32(buffer, CapturedSubwayVergilMonsterDataOffset, corpseMonsterData);
            WriteInt32(
                buffer,
                CapturedSubwayVergilTailDeadNpcInstanceOffset,
                deadNpc.Identity.Instance);

            return buffer;
        }

        private static byte[] BuildCapturedMissionTrashCorpse(
            ICharacter deadNpc,
            Identity corpseIdentity,
            Identity receiver,
            int serverId,
            int corpseCatMesh,
            int corpseMonsterData,
            int corpseCredits)
        {
            // Tilda / Vergil-family templates are 420 bytes: name at 239, length at 235.
            // Shared NameOffset/NameLengthOffset (231/227) are for the 414-byte generic Template
            // and destroy 8 real bytes when reused here — client drops the corpse dynel.
            const int missionTrashNameOffset = 239;
            const int missionTrashNameLengthOffset = 235;
            const int missionTrashOriginalEncodedNameLength = 25;
            int missionTrashOriginalSuffixOffset =
                missionTrashNameOffset + missionTrashOriginalEncodedNameLength;

            string corpseName = "Remains of " + (deadNpc.Name ?? "Unknown");
            byte[] nameBytes = Encoding.ASCII.GetBytes(corpseName);
            int encodedNameLength = nameBytes.Length + 1;
            int newSuffixOffset = missionTrashNameOffset + encodedNameLength;
            int afterNameDelta = newSuffixOffset - missionTrashOriginalSuffixOffset;
            byte[] template = CapturedMissionTrashCorpseTemplate;
            byte[] buffer = new byte[template.Length + afterNameDelta];

            Buffer.BlockCopy(template, 0, buffer, 0, missionTrashNameOffset);
            Buffer.BlockCopy(nameBytes, 0, buffer, missionTrashNameOffset, nameBytes.Length);
            buffer[missionTrashNameOffset + nameBytes.Length] = 0;
            Buffer.BlockCopy(
                template,
                missionTrashOriginalSuffixOffset,
                buffer,
                newSuffixOffset,
                template.Length - missionTrashOriginalSuffixOffset);

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, ServerIdOffset, serverId);
            WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);
            WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);
            WriteSingle(buffer, PositionXOffset, deadNpc.RawCoordinates.X);
            WriteSingle(buffer, PositionYOffset, deadNpc.RawCoordinates.Y);
            WriteSingle(buffer, PositionZOffset, deadNpc.RawCoordinates.Z);
            WriteInt32(buffer, PlayfieldIdOffset, deadNpc.Playfield.Identity.Instance);
            WriteInt32(buffer, MonsterScaleOffset, deadNpc.Stats[StatIds.monsterscale].Value);
            WriteInt32(buffer, SexOffset, deadNpc.Stats[StatIds.sex].Value);
            WriteInt32(buffer, BreedOffset, deadNpc.Stats[StatIds.breed].Value);
            WriteInt32(buffer, RaceOffset, deadNpc.Stats[StatIds.race].Value);
            WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);
            WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh > 0 ? corpseCatMesh : 5934);
            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
            WriteInt32(buffer, missionTrashNameLengthOffset, encodedNameLength);
            WriteInt32(
                buffer,
                CapturedSubwayVergilMonsterDataOffset + afterNameDelta,
                corpseMonsterData > 0 ? corpseMonsterData : 26137);
            WriteInt32(
                buffer,
                CapturedSubwayVergilTailDeadNpcInstanceOffset + afterNameDelta,
                deadNpc.Identity.Instance);

            return buffer;
        }

        private static byte[] BuildCapturedSubwayEumenides(
            ICharacter deadNpc,
            Identity corpseIdentity,
            Identity receiver,
            int serverId,
            int corpseCatMesh,
            int corpseMonsterData,
            int corpseCredits)
        {
            byte[] buffer = (byte[])CapturedSubwayEumenidesTemplate.Clone();
            if (buffer.Length != CapturedSubwayEumenidesPacketLength)
            {
                throw new InvalidOperationException("Captured Subway Eumenides corpse template length changed.");
            }

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, ServerIdOffset, serverId);
            WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);
            WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);
            WriteSingle(buffer, PositionXOffset, deadNpc.RawCoordinates.X);
            WriteSingle(buffer, PositionYOffset, deadNpc.RawCoordinates.Y);
            WriteSingle(buffer, PositionZOffset, deadNpc.RawCoordinates.Z);
            WriteInt32(buffer, PlayfieldIdOffset, deadNpc.Playfield.Identity.Instance);
            WriteInt32(buffer, MonsterScaleOffset, deadNpc.Stats[StatIds.monsterscale].Value);
            WriteInt32(buffer, SexOffset, deadNpc.Stats[StatIds.sex].Value);
            WriteInt32(buffer, BreedOffset, deadNpc.Stats[StatIds.breed].Value);
            WriteInt32(buffer, RaceOffset, deadNpc.Stats[StatIds.race].Value);
            WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);
            WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh);
            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
            WriteInt32(buffer, CapturedSubwayEumenidesMonsterDataOffset, corpseMonsterData);
            WriteInt32(
                buffer,
                CapturedSubwayEumenidesTailDeadNpcInstanceOffset,
                deadNpc.Identity.Instance);

            return buffer;
        }

        private static byte[] BuildCapturedSubwayAbmouth(
            ICharacter deadNpc,
            Identity corpseIdentity,
            Identity receiver,
            int serverId,
            int corpseCatMesh,
            int corpseMonsterData,
            int corpseCredits)
        {
            byte[] buffer = (byte[])CapturedSubwayAbmouthTemplate.Clone();
            if (buffer.Length != CapturedSubwayAbmouthPacketLength)
            {
                throw new InvalidOperationException("Captured Subway Abmouth corpse template length changed.");
            }

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, ServerIdOffset, serverId);
            WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);
            WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);
            WriteSingle(buffer, PositionXOffset, deadNpc.RawCoordinates.X);
            WriteSingle(buffer, PositionYOffset, deadNpc.RawCoordinates.Y);
            WriteSingle(buffer, PositionZOffset, deadNpc.RawCoordinates.Z);
            WriteInt32(buffer, PlayfieldIdOffset, deadNpc.Playfield.Identity.Instance);
            WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);
            WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh);
            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
            WriteInt32(buffer, CapturedSubwayAbmouthMonsterDataOffset, corpseMonsterData);
            WriteInt32(
                buffer,
                CapturedSubwayAbmouthTailDeadNpcInstanceOffset,
                deadNpc.Identity.Instance);

            return buffer;
        }

        private static byte[] BuildCapturedSubwayThief(
            ICharacter deadNpc,
            Identity corpseIdentity,
            Identity receiver,
            int serverId,
            int corpseCatMesh,
            int corpseMonsterData,
            int corpseCredits)
        {
            byte[] buffer = (byte[])CapturedSubwayThiefTemplate.Clone();
            if (buffer.Length != CapturedSubwayThiefPacketLength)
            {
                throw new InvalidOperationException("Captured Subway Thief corpse template length changed.");
            }

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, ServerIdOffset, serverId);
            WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);
            WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);
            WriteSingle(buffer, PositionXOffset, deadNpc.RawCoordinates.X);
            WriteSingle(buffer, PositionYOffset, deadNpc.RawCoordinates.Y);
            WriteSingle(buffer, PositionZOffset, deadNpc.RawCoordinates.Z);
            WriteInt32(buffer, PlayfieldIdOffset, deadNpc.Playfield.Identity.Instance);
            WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);
            WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh);
            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
            WriteInt32(buffer, CapturedSubwayThiefMonsterDataOffset, corpseMonsterData);
            WriteInt32(
                buffer,
                CapturedSubwayThiefTailDeadNpcInstanceOffset,
                deadNpc.Identity.Instance);

            return buffer;
        }

        private static byte[] BuildCapturedSubwayFilthFlea(
            ICharacter deadNpc,
            Identity corpseIdentity,
            Identity receiver,
            int serverId,
            int corpseCatMesh,
            int corpseMonsterData,
            int corpseCredits)
        {
            byte[] buffer = (byte[])CapturedSubwayFilthFleaTemplate.Clone();
            if (buffer.Length != CapturedSubwayFilthFleaPacketLength)
            {
                throw new InvalidOperationException("Captured Subway Filth Flea corpse template length changed.");
            }

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, ServerIdOffset, serverId);
            WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);
            WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);
            WriteSingle(buffer, PositionXOffset, deadNpc.RawCoordinates.X);
            WriteSingle(buffer, PositionYOffset, deadNpc.RawCoordinates.Y);
            WriteSingle(buffer, PositionZOffset, deadNpc.RawCoordinates.Z);
            WriteInt32(buffer, PlayfieldIdOffset, deadNpc.Playfield.Identity.Instance);
            WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);
            WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh);
            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
            WriteInt32(buffer, CapturedSubwayFilthFleaMonsterDataOffset, corpseMonsterData);
            WriteInt32(
                buffer,
                CapturedSubwayFilthFleaTailDeadNpcInstanceOffset,
                deadNpc.Identity.Instance);

            return buffer;
        }

        private static byte[] BuildCapturedAreteWaste(
            ICharacter deadNpc,
            Identity corpseIdentity,
            Identity receiver,
            int serverId,
            int corpseCatMesh,
            int corpseMonsterData,
            int corpseCredits)
        {
            string corpseName = "Remains of " + deadNpc.Name;
            byte[] nameBytes = Encoding.ASCII.GetBytes(corpseName);
            int encodedNameLength = nameBytes.Length + 1;
            int newSuffixOffset = NameOffset + encodedNameLength;
            int afterNameDelta = newSuffixOffset - CapturedAreteWasteOriginalSuffixOffset;
            byte[] buffer = new byte[CapturedAreteWasteTemplate.Length + afterNameDelta];

            Buffer.BlockCopy(CapturedAreteWasteTemplate, 0, buffer, 0, NameOffset);
            Buffer.BlockCopy(nameBytes, 0, buffer, NameOffset, nameBytes.Length);
            Buffer.BlockCopy(
                CapturedAreteWasteTemplate,
                CapturedAreteWasteOriginalSuffixOffset,
                buffer,
                newSuffixOffset,
                CapturedAreteWasteTemplate.Length - CapturedAreteWasteOriginalSuffixOffset);

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, ServerIdOffset, serverId);
            WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);
            WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);
            WriteSingle(buffer, PositionXOffset, deadNpc.RawCoordinates.X);
            WriteSingle(buffer, PositionYOffset, deadNpc.RawCoordinates.Y);
            WriteSingle(buffer, PositionZOffset, deadNpc.RawCoordinates.Z);
            WriteInt32(buffer, PlayfieldIdOffset, deadNpc.Playfield.Identity.Instance);
            WriteInt32(buffer, MonsterScaleOffset, deadNpc.Stats[StatIds.monsterscale].Value);
            WriteInt32(buffer, SexOffset, deadNpc.Stats[StatIds.sex].Value);
            WriteInt32(buffer, BreedOffset, deadNpc.Stats[StatIds.breed].Value);
            WriteInt32(buffer, RaceOffset, deadNpc.Stats[StatIds.race].Value);
            WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);
            WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh);
            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
            WriteInt32(buffer, NameLengthOffset, encodedNameLength);
            WriteInt32(buffer, CapturedAreteWasteMonsterDataOffset + afterNameDelta, corpseMonsterData);
            WriteInt32(
                buffer,
                CapturedAreteWasteTailDeadNpcInstanceOffset + afterNameDelta,
                deadNpc.Identity.Instance);

            return buffer;
        }

        private static byte[] BuildCapturedBarkingChimera(
            ICharacter deadNpc,
            Identity corpseIdentity,
            Identity receiver,
            int serverId,
            int corpseCatMesh,
            int corpseMonsterData,
            int corpseCredits)
        {
            // Same MonsterData/ExtTex path as Yuttos Nascence Geosurvey Dog; rewrite name, keep low2 tail.
            string corpseName = "Remains of " + deadNpc.Name;
            byte[] nameBytes = Encoding.ASCII.GetBytes(corpseName);
            int encodedNameLength = nameBytes.Length + 1;
            int newSuffixOffset = NameOffset + encodedNameLength;
            int afterNameDelta = newSuffixOffset - CapturedBarkingChimeraOriginalSuffixOffset;
            byte[] buffer = new byte[CapturedBarkingChimeraTemplate.Length + afterNameDelta];

            Buffer.BlockCopy(CapturedBarkingChimeraTemplate, 0, buffer, 0, NameOffset);
            Buffer.BlockCopy(nameBytes, 0, buffer, NameOffset, nameBytes.Length);
            Buffer.BlockCopy(
                CapturedBarkingChimeraTemplate,
                CapturedBarkingChimeraOriginalSuffixOffset,
                buffer,
                newSuffixOffset,
                CapturedBarkingChimeraTemplate.Length - CapturedBarkingChimeraOriginalSuffixOffset);

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, ServerIdOffset, serverId);
            WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);
            WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);
            WriteSingle(buffer, PositionXOffset, deadNpc.RawCoordinates.X);
            WriteSingle(buffer, PositionYOffset, deadNpc.RawCoordinates.Y);
            WriteSingle(buffer, PositionZOffset, deadNpc.RawCoordinates.Z);
            WriteInt32(buffer, PlayfieldIdOffset, deadNpc.Playfield.Identity.Instance);
            WriteInt32(buffer, MonsterScaleOffset, deadNpc.Stats[StatIds.monsterscale].Value);
            WriteInt32(buffer, SexOffset, deadNpc.Stats[StatIds.sex].Value);
            WriteInt32(buffer, BreedOffset, deadNpc.Stats[StatIds.breed].Value);
            WriteInt32(buffer, RaceOffset, deadNpc.Stats[StatIds.race].Value);
            WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);
            WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh);
            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
            WriteInt32(buffer, NameLengthOffset, encodedNameLength);
            WriteInt32(
                buffer,
                CapturedBarkingChimeraMonsterDataOffset + afterNameDelta,
                corpseMonsterData);
            WriteInt32(
                buffer,
                CapturedBarkingChimeraTailDeadNpcInstanceOffset + afterNameDelta,
                deadNpc.Identity.Instance);

            return buffer;
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        }

        private static void WritePacketLength(byte[] buffer, int length)
        {
            buffer[6] = (byte)((length >> 8) & 0xff);
            buffer[7] = (byte)(length & 0xff);
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
}
