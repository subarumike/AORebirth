namespace AORebirth.CaptureProtocol
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    internal static class RawSimpleCharFullUpdateDecoder
    {
        internal const int N3BodyOffset = 16;
        internal const int SimpleCharFullUpdateType = 0x271B3A6B;

        private const int IsNpc = 0x00000001;
        private const int HasExtendedTextures = 0x00000010;
        private const int HasFightingTarget = 0x00000020;
        private const int HasPlayfieldId = 0x00000040;
        private const int HasHeadMesh = 0x00000080;
        private const int HasHeading = 0x00000200;
        private const int IsUnderAttack = 0x00000400;
        private const int HasSmallHealth = 0x00000800;
        private const int HasExtendedLevel = 0x00001000;
        private const int HasExtendedRunSpeed = 0x00002000;
        private const int HasSmallHealthDamage = 0x00004000;
        private const int HasWaypoints = 0x00010000;
        private const int HasSmallNpcFamily = 0x00020000;
        private const int HasSmallNpcLosHeight = 0x00080000;
        private const int IsImmune = 0x00800000;
        private const int UnknownFlag3 = 0x01000000;
        private const int UnknownDataFlag = 0x02000000;
        private const int HasOrgName = 0x04000000;
        private const int IsPet = 0x08000000;

        private const int CharacterTower = 0x00020000;
        private const int CharacterHasVisibleName = 0x00400000;

        private const int Flags2Unknown1 = 0x00000002;
        private const int Flags2HasOwner = 0x00000004;
        private const int Flags2Unknown3 = 0x00000040;
        private const int SimpleCharIdentityType = 0x0000C350;

        private static readonly byte[] PlayerFlags2Fc4OpaqueExtension =
        {
            0x12, 0x95, 0x00, 0x00, 0x00, 0x8E, 0x42, 0x52,
            0x41, 0x57, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly byte[] PetFlags2Bd3OpaqueExtension =
        {
            0x3D, 0x00, 0x01, 0xD7, 0x3E, 0x4D, 0x45, 0x57,
            0x31, 0x4D, 0x45, 0x57, 0x31, 0x00, 0x00, 0x00,
            0x04, 0x79, 0x1C, 0x10, 0x0D, 0x00
        };

        private static readonly byte[] PetFlags27e2OpaqueExtension =
        {
            0x04, 0x79, 0x1C, 0x10, 0x0D, 0x00
        };

        internal static bool TryDecodePacket(
            byte[] packet,
            out RawSimpleCharFullUpdate result,
            out string error)
        {
            try
            {
                result = DecodePacket(packet);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                result = null;
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        internal static RawSimpleCharFullUpdate DecodePacket(byte[] packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException("packet");
            }

            if (packet.Length <= N3BodyOffset)
            {
                throw new InvalidDataException("SCFU packet does not contain an N3 body after offset 16.");
            }

            int declaredPacketLength = (packet[6] << 8) | packet[7];
            if (declaredPacketLength != packet.Length)
            {
                throw new InvalidDataException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "SCFU frame length mismatch: header={0}, actual={1}.",
                        declaredPacketLength,
                        packet.Length));
            }

            byte[] rawPacket = Copy(packet, 0, packet.Length);
            byte[] rawBody = Copy(packet, N3BodyOffset, packet.Length - N3BodyOffset);
            var reader = new BigEndianReader(rawBody);
            var result = new RawSimpleCharFullUpdate
            {
                RawPacket = rawPacket,
                RawBody = rawBody,
                N3MessageType = reader.ReadInt32("N3MessageType")
            };

            if (result.N3MessageType != SimpleCharFullUpdateType)
            {
                throw new InvalidDataException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Expected SCFU message type 0x{0:X8}, received 0x{1:X8}.",
                        SimpleCharFullUpdateType,
                        result.N3MessageType));
            }

            result.Identity = ReadIdentity(reader, "Identity");
            result.HeaderUnknown = reader.ReadByte("HeaderUnknown");
            result.Version = reader.ReadByte("Version");
            result.Flags = reader.ReadInt32("Flags");

            if (Has(result.Flags, HasPlayfieldId))
            {
                result.PlayfieldId = reader.ReadInt32("PlayfieldId");
            }

            if (Has(result.Flags, HasFightingTarget))
            {
                result.PrePositionFightingTarget = ReadIdentity(reader, "PrePositionFightingTarget");
            }

            result.Position = ReadVector3(reader, "Position");
            result.Heading = Has(result.Flags, HasHeading)
                                 ? ReadQuaternion(reader, "Heading")
                                 : new RawScfuQuaternion { W = 1.0f };
            result.AppearanceValue = reader.ReadUInt32("Appearance");
            result.Name = reader.ReadAscii(reader.ReadByte("NameLength"), true, "Name");
            result.CharacterFlags = reader.ReadInt32("CharacterFlags");
            result.AccountFlags = reader.ReadInt16("AccountFlags");
            result.Expansions = reader.ReadInt16("Expansions");

            if (Has(result.Flags, IsNpc))
            {
                var npc = new RawScfuNpcInfo();
                npc.Family = Has(result.Flags, HasSmallNpcFamily)
                                 ? reader.ReadByte("NpcFamily")
                                 : reader.ReadInt16("NpcFamily");
                npc.LosHeight = Has(result.Flags, HasSmallNpcLosHeight)
                                    ? reader.ReadByte("NpcLosHeight")
                                    : reader.ReadInt16("NpcLosHeight");
                npc.UnknownDataWidth = Has(result.Flags, UnknownDataFlag) ? 1 : 2;
                npc.UnknownData = npc.UnknownDataWidth == 1
                                      ? reader.ReadByte("NpcUnknownData")
                                      : reader.ReadInt16("NpcUnknownData");
                npc.UnknownData2 = reader.ReadInt16("NpcUnknownData2");
                if (npc.UnknownData2 > 0)
                {
                    npc.UnknownData3 = reader.ReadByte("NpcUnknownData3");
                }

                result.Npc = npc;
            }
            else
            {
                var player = new RawScfuPlayerInfo
                {
                    CurrentNano = reader.ReadUInt32("PlayerCurrentNano"),
                    Team = reader.ReadInt32("PlayerTeam"),
                    Swim = reader.ReadInt16("PlayerSwim"),
                    StrengthBase = reader.ReadInt16("PlayerStrengthBase"),
                    AgilityBase = reader.ReadInt16("PlayerAgilityBase"),
                    StaminaBase = reader.ReadInt16("PlayerStaminaBase"),
                    IntelligenceBase = reader.ReadInt16("PlayerIntelligenceBase"),
                    SenseBase = reader.ReadInt16("PlayerSenseBase"),
                    PsychicBase = reader.ReadInt16("PlayerPsychicBase")
                };

                if (result.Version == 57)
                {
                    if (Has(result.CharacterFlags, CharacterHasVisibleName))
                    {
                        player.FirstName = reader.ReadAscii(
                            reader.ReadInt16("LegacyPlayerFirstNameLength"),
                            true,
                            "LegacyPlayerFirstName");
                        player.LastName = reader.ReadAscii(
                            reader.ReadInt16("LegacyPlayerLastNameLength"),
                            true,
                            "LegacyPlayerLastName");
                    }

                    if (Has(result.Flags, HasOrgName))
                    {
                        player.OrgName = reader.ReadAscii(
                            reader.ReadInt16("LegacyPlayerOrgNameLength"),
                            true,
                            "LegacyPlayerOrgName");
                    }
                }
                else
                {
                    if (Has(result.Flags, HasOrgName))
                    {
                        player.OrgMetadata = reader.ReadInt32("PlayerOrgMetadata");
                        player.OrgMetadataUnknown = reader.ReadByte("PlayerOrgMetadataUnknown");
                    }

                    if (Has(result.CharacterFlags, CharacterHasVisibleName))
                    {
                        player.FirstName = reader.ReadAscii(
                            reader.ReadByte("PlayerFirstNameLength"),
                            true,
                            "PlayerFirstName");
                        player.LastName = reader.ReadAscii(
                            reader.ReadByte("PlayerLastNameLength"),
                            true,
                            "PlayerLastName");
                    }

                    if (Has(result.Flags, HasOrgName))
                    {
                        player.OrgName = reader.ReadAscii(
                            reader.ReadByte("PlayerOrgNameLength"),
                            true,
                            "PlayerOrgName");
                    }
                }

                result.Player = player;
            }

            if (Has(result.CharacterFlags, CharacterTower)
                && !HasValidUnknown1LengthAtLevelOffset(reader, result.Flags, 0)
                && HasValidUnknown1LengthAtLevelOffset(reader, result.Flags, 1))
            {
                result.TowerUnknown = reader.ReadByte("TowerUnknown");
            }

            result.Level = Has(result.Flags, HasExtendedLevel)
                               ? reader.ReadInt16("Level")
                               : (short)reader.ReadByte("Level");
            result.Health = Has(result.Flags, HasSmallHealth)
                                ? reader.ReadUInt16("Health")
                                : reader.ReadInt32("Health");
            result.HealthDamage = Has(result.Flags, HasSmallHealthDamage)
                                      ? reader.ReadByte("HealthDamage")
                                      : Has(result.Flags, HasSmallHealth)
                                            ? reader.ReadUInt16("HealthDamage")
                                            : reader.ReadInt32("HealthDamage");
            result.MonsterData = reader.ReadUInt32("MonsterData");
            result.MonsterScale = reader.ReadInt16("MonsterScale");
            result.VisualFlags = reader.ReadInt16("VisualFlags");
            result.VisibleTitle = reader.ReadByte("VisibleTitle");

            int unknown1Length = reader.ReadInt32("ScfuUnknown1Length");
            result.Unknown1 = reader.ReadBytes(unknown1Length, "ScfuUnknown1");

            if (Has(result.Flags, HasHeadMesh))
            {
                result.HeadMesh = reader.ReadInt32("HeadMesh");
            }

            if (Has(result.Flags, HasExtendedRunSpeed))
            {
                result.RunSpeedBase = reader.ReadInt16("RunSpeedBase");
            }
            else if (HasLegacyExtendedRunSpeedAlignment(reader, result.Flags))
            {
                // Some preserved legacy SCFUs contain a two-byte run speed while
                // omitting HasExtendedRunSpeed. Only accept the alternate width
                // when it is proven by the immediately following X3F1 marker.
                result.RunSpeedBase = reader.ReadInt16("LegacyExtendedRunSpeedBase");
                result.LegacyExtendedRunSpeedAlignment = true;
            }
            else
            {
                result.RunSpeedBase = reader.ReadByte("RunSpeedBase");
            }

            if (Has(result.Flags, IsUnderAttack))
            {
                result.FightingTarget = ReadIdentity(reader, "FightingTarget");
            }

            if (Has(result.Flags, HasExtendedTextures))
            {
                int count = ReadX3F1Count(reader, "TextureOverrides");
                EnsureElementBytes(reader, count, 44, "TextureOverrides");
                var values = new RawScfuTextureOverride[count];
                for (int i = 0; i < count; i++)
                {
                    byte[] rawName = reader.ReadBytes(32, "TextureOverrideName");
                    values[i] = new RawScfuTextureOverride
                    {
                        RawName = rawName,
                        Name = Encoding.ASCII.GetString(rawName),
                        TextureId = reader.ReadInt32("TextureOverrideTextureId"),
                        Unknown1 = reader.ReadInt32("TextureOverrideUnknown1"),
                        Unknown2 = reader.ReadInt32("TextureOverrideUnknown2")
                    };
                }

                result.TextureOverrides = values;
            }
            else
            {
                result.TextureOverrides = new RawScfuTextureOverride[0];
            }

            if (Has(result.Flags, IsImmune))
            {
                result.ImmuneUnknown = reader.ReadByte("ImmuneUnknown");
            }

            if (Has(result.Flags, UnknownFlag3))
            {
                result.UnknownFlag3Data = reader.ReadByte("UnknownFlag3Data");
            }

            int activeNanoCount = ReadX3F1Count(reader, "ActiveNanos");
            EnsureElementBytes(reader, activeNanoCount, 20, "ActiveNanos");
            var activeNanos = new RawScfuActiveNano[activeNanoCount];
            for (int i = 0; i < activeNanoCount; i++)
            {
                activeNanos[i] = new RawScfuActiveNano
                {
                    Identity = ReadIdentity(reader, "ActiveNanoIdentity"),
                    NanoInstance = reader.ReadInt32("ActiveNanoInstance"),
                    Time1 = reader.ReadInt32("ActiveNanoTime1"),
                    Time2 = reader.ReadInt32("ActiveNanoTime2")
                };
            }

            result.ActiveNanos = activeNanos;

            if (Has(result.Flags, HasWaypoints))
            {
                result.WaypointOwner = ReadIdentity(reader, "WaypointOwner");
                int count = reader.ReadInt32("WaypointCount");
                ValidateCount(count, "Waypoints");
                EnsureElementBytes(reader, count, 12, "Waypoints");
                var waypoints = new RawScfuVector3[count];
                for (int i = 0; i < count; i++)
                {
                    waypoints[i] = ReadVector3(reader, "Waypoint");
                }

                result.Waypoints = waypoints;
            }
            else
            {
                result.Waypoints = new RawScfuVector3[0];
            }

            int textureCount = ReadX3F1Count(reader, "Textures");
            EnsureElementBytes(reader, textureCount, 12, "Textures");
            var textures = new RawScfuTexture[textureCount];
            for (int i = 0; i < textureCount; i++)
            {
                textures[i] = new RawScfuTexture
                {
                    Place = reader.ReadInt32("TexturePlace"),
                    Id = reader.ReadInt32("TextureId"),
                    Unknown = reader.ReadInt32("TextureUnknown")
                };
            }

            result.Textures = textures;

            int meshCount = ReadX3F1Count(reader, "Meshes");
            EnsureElementBytes(reader, meshCount, 10, "Meshes");
            var meshes = new RawScfuMesh[meshCount];
            for (int i = 0; i < meshCount; i++)
            {
                meshes[i] = new RawScfuMesh
                {
                    Position = reader.ReadByte("MeshPosition"),
                    Id = reader.ReadUInt32("MeshId"),
                    OverrideTextureId = reader.ReadInt32("MeshOverrideTextureId"),
                    Layer = reader.ReadByte("MeshLayer")
                };
            }

            result.Meshes = meshes;
            result.Flags2 = reader.ReadInt32("Flags2");
            if (Has(result.Flags2, Flags2HasOwner))
            {
                result.Owner = new RawScfuIdentity
                {
                    Type = SimpleCharIdentityType,
                    Instance = reader.ReadInt32("OwnerInstance")
                };
            }

            result.Unknown2 = reader.ReadByte("ScfuUnknown2");
            if (Has(result.Flags2, Flags2Unknown3))
            {
                int count = reader.ReadByte("SpecialAttackCount");
                var attacks = new RawScfuSpecialAttack[count];
                for (int i = 0; i < count; i++)
                {
                    if (result.TerminalSpecialAttackSlotOmitted)
                    {
                        break;
                    }

                    if (IsObservedOpaqueExtensionBeforeDeclaredSpecialAttackSlots(result, reader))
                    {
                        result.TerminalSpecialAttackSlotOmitted = true;
                        break;
                    }

                    if (IsObservedTerminalSpecialAttackSlotOmission(result, reader, i, count))
                    {
                        // The final byte is the flags2 Unknown1 field which follows
                        // the attack list. Preserve that byte for the normal field
                        // decoder instead of treating it as a truncated attack slot.
                        result.TerminalSpecialAttackSlotOmitted = true;
                        break;
                    }

                    short unknown1 = reader.ReadInt16("SpecialAttackUnknown1");
                    if (unknown1 == 0)
                    {
                        continue;
                    }

                    attacks[i] = new RawScfuSpecialAttack
                    {
                        Unknown1 = unknown1,
                        Unknown2 = reader.ReadInt16("SpecialAttackUnknown2"),
                        Unknown3 = reader.ReadInt16("SpecialAttackUnknown3"),
                        Unknown4 = reader.ReadInt16("SpecialAttackUnknown4"),
                        Unknown5 = reader.ReadInt16("SpecialAttackUnknown5"),
                        Name = reader.ReadAscii(4, false, "SpecialAttackName"),
                        Unknown6 = ReadSpecialAttackUnknown6(result, reader, i, count)
                    };
                }

                result.SpecialAttacks = attacks;
            }
            else
            {
                result.SpecialAttacks = new RawScfuSpecialAttack[0];
            }

            if (Has(result.Flags2, Flags2Unknown1))
            {
                result.Unknown4 = reader.ReadByte("ScfuUnknown4");
            }

            result.OpaqueExtension = ReadObservedOpaqueExtension(result, reader);

            result.BytesConsumed = reader.Position;
            result.UndecodedTail = reader.ReadRemaining();
            result.DecodeFullyConsumed = result.UndecodedTail.Length == 0;
            return result;
        }

        private static RawScfuIdentity ReadIdentity(BigEndianReader reader, string field)
        {
            return new RawScfuIdentity
            {
                Type = reader.ReadInt32(field + "Type"),
                Instance = reader.ReadInt32(field + "Instance")
            };
        }

        private static RawScfuVector3 ReadVector3(BigEndianReader reader, string field)
        {
            return new RawScfuVector3
            {
                X = reader.ReadSingle(field + "X"),
                Y = reader.ReadSingle(field + "Y"),
                Z = reader.ReadSingle(field + "Z")
            };
        }

        private static RawScfuQuaternion ReadQuaternion(BigEndianReader reader, string field)
        {
            return new RawScfuQuaternion
            {
                X = reader.ReadSingle(field + "X"),
                Y = reader.ReadSingle(field + "Y"),
                Z = reader.ReadSingle(field + "Z"),
                W = reader.ReadSingle(field + "W")
            };
        }

        private static int ReadX3F1Count(BigEndianReader reader, string field)
        {
            int marker = reader.ReadInt32(field + "Marker");
            if (marker < 0x3F1 || marker % 0x3F1 != 0)
            {
                throw new InvalidDataException("Invalid SCFU " + field + " marker.");
            }

            int count = marker / 0x3F1 - 1;
            ValidateCount(count, field);
            return count;
        }

        private static bool HasLegacyExtendedRunSpeedAlignment(BigEndianReader reader, int flags)
        {
            if (Has(flags, IsUnderAttack)
                || Has(flags, HasExtendedTextures)
                || Has(flags, IsImmune)
                || Has(flags, UnknownFlag3)
                || reader.Remaining < 6
                || reader.PeekByte(0) != 0)
            {
                return false;
            }

            return !IsValidX3F1Marker(reader.PeekInt32(1))
                   && IsValidX3F1Marker(reader.PeekInt32(2));
        }

        private static bool HasValidUnknown1LengthAtLevelOffset(
            BigEndianReader reader,
            int flags,
            int prefixBytes)
        {
            int levelWidth = Has(flags, HasExtendedLevel) ? 2 : 1;
            int healthWidth = Has(flags, HasSmallHealth) ? 2 : 4;
            int healthDamageWidth = Has(flags, HasSmallHealthDamage)
                                        ? 1
                                        : Has(flags, HasSmallHealth) ? 2 : 4;
            int lengthOffset = prefixBytes
                               + levelWidth
                               + healthWidth
                               + healthDamageWidth
                               + 4
                               + 2
                               + 2
                               + 1;
            if (reader.Remaining < lengthOffset + 4)
            {
                return false;
            }

            int length = reader.PeekInt32(lengthOffset);
            return length >= 0 && length <= reader.Remaining - lengthOffset - 4;
        }

        private static bool IsValidX3F1Marker(int marker)
        {
            if (marker < 0x3F1 || marker % 0x3F1 != 0)
            {
                return false;
            }

            int count = marker / 0x3F1 - 1;
            return count >= 0 && count <= 4096;
        }

        private static bool IsObservedTerminalSpecialAttackSlotOmission(
            RawSimpleCharFullUpdate result,
            BigEndianReader reader,
            int index,
            int count)
        {
            return Has(result.Flags2, Flags2Unknown1)
                   && index < count
                   && reader.Remaining == 1
                   && reader.PeekByte(0) == 0;
        }

        private static bool IsObservedOpaqueExtensionBeforeDeclaredSpecialAttackSlots(
            RawSimpleCharFullUpdate result,
            BigEndianReader reader)
        {
            return result.Flags2 == 0x00000FC4
                   && reader.RemainingEquals(PlayerFlags2Fc4OpaqueExtension);
        }

        private static short ReadSpecialAttackUnknown6(
            RawSimpleCharFullUpdate result,
            BigEndianReader reader,
            int index,
            int count)
        {
            if (reader.Remaining == 1
                && reader.PeekByte(0) == 0)
            {
                if (index < count - 1)
                {
                    result.TerminalSpecialAttackSlotOmitted = true;
                }

                if (!Has(result.Flags2, Flags2Unknown1))
                {
                    reader.ReadByte("TerminalSpecialAttackUnknown6");
                }

                return 0;
            }

            if (result.Flags2 == 0x00000FC4
                && Has(result.Flags, IsNpc)
                && Has(result.Flags, IsPet)
                && index == count - 1
                && reader.Remaining == 1
                && reader.PeekByte(0) == 0)
            {
                reader.ReadByte("TerminalSpecialAttackUnknown6");
                return 0;
            }

            return reader.ReadInt16("SpecialAttackUnknown6");
        }

        private static byte[] ReadObservedOpaqueExtension(
            RawSimpleCharFullUpdate result,
            BigEndianReader reader)
        {
            bool observedFamily =
                (result.Flags2 == 0x00000FC4
                    && reader.RemainingEquals(PlayerFlags2Fc4OpaqueExtension))
                || (result.Flags2 == 0x00000BD3
                    && Has(result.Flags, IsPet)
                    && reader.RemainingEquals(PetFlags2Bd3OpaqueExtension))
                || (result.Flags2 == 0x000007E2
                    && Has(result.Flags, IsPet)
                    && reader.RemainingEquals(PetFlags27e2OpaqueExtension))
                || (result.Flags2 == 0x00000BD3
                    && !Has(result.Flags, IsPet)
                    && reader.RemainingEquals(FromHex("DA0001D8DB524F5731524F57310000000000")))
                || (result.Flags2 == 0x000013B5
                    && reader.RemainingEquals(FromHex("03D1EA48564A5048564A500003D1E60003D1E756514857565148570003D1E30003D1E44B4C475A4B4C475A0003D1E00003D1E150514541505145410000000000")))
                || (result.Flags2 == 0x000017A6
                    && reader.RemainingEquals(FromHex("D4544E4D4C4B4E4D4C4B0003D4500003D45152444D5552444D550003D44D0003D44E44474755444747550003D44A0003D44B445A5447445A54470003D4470003D44854535A5354535A530000000000")))
                || (result.Flags2 == 0x000017A6
                    && reader.RemainingEquals(FromHex("D46353485951534859510003D45F0003D460434E4859434E48590003D45C0003D45D4F494E444F494E440003D4590003D45A4D41434A4D41434A0003D4560003D45742444542424445420000000000")));
            return observedFamily ? reader.ReadRemaining() : new byte[0];
        }

        private static void ValidateCount(int count, string field)
        {
            if (count < 0 || count > 4096)
            {
                throw new InvalidDataException("Invalid SCFU " + field + " count.");
            }
        }

        private static void EnsureElementBytes(BigEndianReader reader, int count, int elementSize, string field)
        {
            int required;
            try
            {
                required = checked(count * elementSize);
            }
            catch (OverflowException)
            {
                throw new InvalidDataException("Invalid SCFU " + field + " byte count.");
            }

            reader.EnsureRemaining(required, field);
        }

        private static bool Has(int value, int flag)
        {
            return (value & flag) == flag;
        }

        private static byte[] FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return new byte[0];
            }

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        private static byte[] Copy(byte[] value, int offset, int count)
        {
            var result = new byte[count];
            Buffer.BlockCopy(value, offset, result, 0, count);
            return result;
        }

        private sealed class BigEndianReader
        {
            private readonly byte[] bytes;
            private int position;

            internal BigEndianReader(byte[] bytes)
            {
                this.bytes = bytes ?? new byte[0];
            }

            internal int Position
            {
                get { return this.position; }
            }

            internal int Remaining
            {
                get { return this.bytes.Length - this.position; }
            }

            internal byte PeekByte(int offset)
            {
                this.EnsureRemaining(offset + 1, "PeekByte");
                return this.bytes[this.position + offset];
            }

            internal int PeekInt32(int offset)
            {
                this.EnsureRemaining(offset + 4, "PeekInt32");
                int start = this.position + offset;
                return (this.bytes[start] << 24)
                       | (this.bytes[start + 1] << 16)
                       | (this.bytes[start + 2] << 8)
                       | this.bytes[start + 3];
            }

            internal bool RemainingEquals(byte[] expected)
            {
                if (expected == null || this.Remaining != expected.Length)
                {
                    return false;
                }

                for (int i = 0; i < expected.Length; i++)
                {
                    if (this.bytes[this.position + i] != expected[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            internal void EnsureRemaining(int count, string field)
            {
                if (count < 0 || count > this.Remaining)
                {
                    throw new InvalidDataException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Truncated SCFU {0} data at body offset {1}; need {2}, have {3}.",
                            field,
                            this.position,
                            count,
                            this.Remaining));
                }
            }

            internal byte ReadByte(string field)
            {
                this.EnsureRemaining(1, field);
                return this.bytes[this.position++];
            }

            internal short ReadInt16(string field)
            {
                this.EnsureRemaining(2, field);
                int value = (this.bytes[this.position] << 8) | this.bytes[this.position + 1];
                this.position += 2;
                return unchecked((short)value);
            }

            internal ushort ReadUInt16(string field)
            {
                return unchecked((ushort)this.ReadInt16(field));
            }

            internal int ReadInt32(string field)
            {
                this.EnsureRemaining(4, field);
                int value = (this.bytes[this.position] << 24)
                            | (this.bytes[this.position + 1] << 16)
                            | (this.bytes[this.position + 2] << 8)
                            | this.bytes[this.position + 3];
                this.position += 4;
                return value;
            }

            internal uint ReadUInt32(string field)
            {
                return unchecked((uint)this.ReadInt32(field));
            }

            internal float ReadSingle(string field)
            {
                int bits = this.ReadInt32(field);
                return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
            }

            internal byte[] ReadBytes(int count, string field)
            {
                this.EnsureRemaining(count, field);
                byte[] result = Copy(this.bytes, this.position, count);
                this.position += count;
                return result;
            }

            internal string ReadAscii(int count, bool trimNull, string field)
            {
                string value = Encoding.ASCII.GetString(this.ReadBytes(count, field));
                return trimNull ? value.TrimEnd('\0') : value;
            }

            internal byte[] ReadRemaining()
            {
                return this.ReadBytes(this.Remaining, "UndecodedTail");
            }
        }
    }

    internal sealed class RawSimpleCharFullUpdate
    {
        internal int N3MessageType { get; set; }
        internal RawScfuIdentity Identity { get; set; }
        internal byte HeaderUnknown { get; set; }
        internal byte Version { get; set; }
        internal int Flags { get; set; }
        internal int? PlayfieldId { get; set; }
        internal RawScfuIdentity? PrePositionFightingTarget { get; set; }
        internal RawScfuVector3 Position { get; set; }
        internal RawScfuQuaternion Heading { get; set; }
        internal uint AppearanceValue { get; set; }
        internal string Name { get; set; }
        internal int CharacterFlags { get; set; }
        internal short AccountFlags { get; set; }
        internal short Expansions { get; set; }
        internal RawScfuNpcInfo Npc { get; set; }
        internal RawScfuPlayerInfo Player { get; set; }
        internal byte? TowerUnknown { get; set; }
        internal short Level { get; set; }
        internal int Health { get; set; }
        internal int HealthDamage { get; set; }
        internal uint MonsterData { get; set; }
        internal short MonsterScale { get; set; }
        internal short VisualFlags { get; set; }
        internal byte VisibleTitle { get; set; }
        internal byte[] Unknown1 { get; set; }
        internal int? HeadMesh { get; set; }
        internal short RunSpeedBase { get; set; }
        internal RawScfuIdentity? FightingTarget { get; set; }
        internal RawScfuTextureOverride[] TextureOverrides { get; set; }
        internal byte? ImmuneUnknown { get; set; }
        internal byte? UnknownFlag3Data { get; set; }
        internal RawScfuActiveNano[] ActiveNanos { get; set; }
        internal RawScfuIdentity? WaypointOwner { get; set; }
        internal RawScfuVector3[] Waypoints { get; set; }
        internal RawScfuTexture[] Textures { get; set; }
        internal RawScfuMesh[] Meshes { get; set; }
        internal int Flags2 { get; set; }
        internal RawScfuIdentity? Owner { get; set; }
        internal byte Unknown2 { get; set; }
        internal float? Unknown3 { get; set; }
        internal byte? Unknown4 { get; set; }
        internal RawScfuSpecialAttack[] SpecialAttacks { get; set; }
        internal bool TerminalSpecialAttackSlotOmitted { get; set; }
        internal bool LegacyExtendedRunSpeedAlignment { get; set; }
        internal byte[] OpaqueExtension { get; set; }
        internal int BytesConsumed { get; set; }
        internal bool DecodeFullyConsumed { get; set; }
        internal byte[] UndecodedTail { get; set; }
        internal byte[] RawBody { get; set; }
        internal byte[] RawPacket { get; set; }

        internal string FlagsText
        {
            get { return RawScfuFormatting.FormatFlags(this.Flags); }
        }

        internal string Flags2Text
        {
            get { return RawScfuFormatting.FormatFlags2(this.Flags2); }
        }

        internal int AppearanceSide
        {
            get { return (int)(this.AppearanceValue & 7); }
        }

        internal int AppearanceFatness
        {
            get { return (int)((this.AppearanceValue & 31) >> 3); }
        }

        internal int AppearanceBreed
        {
            get { return (int)((this.AppearanceValue & 255) >> 5); }
        }

        internal int AppearanceGender
        {
            get { return (int)((this.AppearanceValue & 1023) >> 8); }
        }

        internal uint AppearanceRace
        {
            get { return this.AppearanceValue >> 10; }
        }
    }

    internal struct RawScfuIdentity
    {
        internal int Type { get; set; }
        internal int Instance { get; set; }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0}:{1})",
                RawScfuFormatting.IdentityTypeName(this.Type),
                this.Instance.ToString("X4", CultureInfo.InvariantCulture));
        }
    }

    internal struct RawScfuVector3
    {
        internal float X { get; set; }
        internal float Y { get; set; }
        internal float Z { get; set; }
    }

    internal struct RawScfuQuaternion
    {
        internal float X { get; set; }
        internal float Y { get; set; }
        internal float Z { get; set; }
        internal float W { get; set; }
    }

    internal sealed class RawScfuNpcInfo
    {
        internal short Family { get; set; }
        internal short LosHeight { get; set; }
        internal int UnknownDataWidth { get; set; }
        internal short UnknownData { get; set; }
        internal short UnknownData2 { get; set; }
        internal byte? UnknownData3 { get; set; }
    }

    internal sealed class RawScfuPlayerInfo
    {
        internal uint CurrentNano { get; set; }
        internal int Team { get; set; }
        internal short Swim { get; set; }
        internal short StrengthBase { get; set; }
        internal short AgilityBase { get; set; }
        internal short StaminaBase { get; set; }
        internal short IntelligenceBase { get; set; }
        internal short SenseBase { get; set; }
        internal short PsychicBase { get; set; }
        internal int? OrgMetadata { get; set; }
        internal byte? OrgMetadataUnknown { get; set; }
        internal string FirstName { get; set; }
        internal string LastName { get; set; }
        internal string OrgName { get; set; }
    }

    internal sealed class RawScfuActiveNano
    {
        internal RawScfuIdentity Identity { get; set; }
        internal int NanoInstance { get; set; }
        internal int Time1 { get; set; }
        internal int Time2 { get; set; }
    }

    internal sealed class RawScfuTextureOverride
    {
        internal byte[] RawName { get; set; }
        internal string Name { get; set; }
        internal int TextureId { get; set; }
        internal int Unknown1 { get; set; }
        internal int Unknown2 { get; set; }
    }

    internal sealed class RawScfuTexture
    {
        internal int Place { get; set; }
        internal int Id { get; set; }
        internal int Unknown { get; set; }
    }

    internal sealed class RawScfuMesh
    {
        internal byte Position { get; set; }
        internal uint Id { get; set; }
        internal int OverrideTextureId { get; set; }
        internal byte Layer { get; set; }
    }

    internal sealed class RawScfuSpecialAttack
    {
        internal short Unknown1 { get; set; }
        internal short Unknown2 { get; set; }
        internal short Unknown3 { get; set; }
        internal short Unknown4 { get; set; }
        internal short Unknown5 { get; set; }
        internal string Name { get; set; }
        internal short Unknown6 { get; set; }
    }

    internal sealed class RawScfuCaptureMetadata
    {
        internal string CapturedUtc { get; set; }
        internal string ElapsedMilliseconds { get; set; }
        internal string Direction { get; set; }
        internal string GlobalOrdinal { get; set; }
        internal string Sequence { get; set; }
    }

    internal static class RawScfuAppearanceCsv
    {
        internal static readonly string Header = string.Join(
            ",",
            new[]
            {
                "CapturedUtc",
                "ElapsedMilliseconds",
                "Direction",
                "GlobalOrdinal",
                "Sequence",
                "PacketLength",
                "DecodeStatus",
                "DecodeError",
                "BytesConsumed",
                "UndecodedTailHex",
                "RawPacketHex",
                "RawBodyHex",
                "Identity",
                "Name",
                "PlayfieldId",
                "PositionX",
                "PositionY",
                "PositionZ",
                "HeadingX",
                "HeadingY",
                "HeadingZ",
                "HeadingW",
                "PrePositionFightingTarget",
                "FightingTarget",
                "Version",
                "FlagsNumeric",
                "Flags",
                "CharacterFlags",
                "AccountFlags",
                "Expansions",
                "CharacterInfoType",
                "NpcFamily",
                "NpcLosHeight",
                "NpcUnknownData",
                "NpcUnknownData2",
                "NpcUnknownData3",
                "Level",
                "Health",
                "HealthDamage",
                "MonsterData",
                "MonsterScale",
                "VisualFlags",
                "VisibleTitle",
                "AppearanceValue",
                "Side",
                "Fatness",
                "Breed",
                "Gender",
                "Race",
                "ScfuUnknown1Hex",
                "HeadMesh",
                "RunSpeedBase",
                "ActiveNanos",
                "WaypointOwner",
                "Waypoints",
                "TextureOverrides",
                "Textures",
                "Meshes",
                "Flags2Numeric",
                "Flags2",
                "Owner",
                "ScfuUnknown2",
                "ScfuUnknown4",
                "SpecialAttacks",
                "ImmuneUnknown",
                "UnknownFlag3Data",
                "HeaderUnknown",
                "TowerUnknown",
                "PlayerInfo",
                "N3MessageTypeNumeric",
                "DecodeFullyConsumed",
                "NpcUnknownDataWidth",
                "ScfuUnknown3",
                "LegacyExtendedRunSpeedAlignment",
                "TerminalSpecialAttackSlotOmitted",
                "OpaqueExtensionHex"
            });

        internal static string FormatRow(
            RawScfuCaptureMetadata metadata,
            byte[] packet,
            RawSimpleCharFullUpdate message,
            string decodeError)
        {
            metadata = metadata ?? new RawScfuCaptureMetadata();
            RawScfuNpcInfo npc = message == null ? null : message.Npc;
            string decodeStatus = message == null
                                      ? "decode_failed"
                                      : message.DecodeFullyConsumed
                                            ? "decoded_complete"
                                            : "raw_complete_decode_pending";
            byte[] rawBody = message == null ? CopyBody(packet) : message.RawBody;
            var fields = new List<string>
            {
                metadata.CapturedUtc,
                metadata.ElapsedMilliseconds,
                metadata.Direction,
                metadata.GlobalOrdinal,
                metadata.Sequence,
                FormatInt(packet == null ? 0 : packet.Length),
                decodeStatus,
                decodeError,
                message == null ? string.Empty : FormatInt(message.BytesConsumed),
                message == null ? string.Empty : RawScfuFormatting.ToHex(message.UndecodedTail),
                RawScfuFormatting.ToHex(packet),
                RawScfuFormatting.ToHex(rawBody),
                message == null ? string.Empty : message.Identity.ToString(),
                message == null ? string.Empty : message.Name,
                message == null || !message.PlayfieldId.HasValue ? string.Empty : FormatInt(message.PlayfieldId.Value),
                message == null ? string.Empty : FormatFloat(message.Position.X),
                message == null ? string.Empty : FormatFloat(message.Position.Y),
                message == null ? string.Empty : FormatFloat(message.Position.Z),
                message == null ? string.Empty : FormatFloat(message.Heading.X),
                message == null ? string.Empty : FormatFloat(message.Heading.Y),
                message == null ? string.Empty : FormatFloat(message.Heading.Z),
                message == null ? string.Empty : FormatFloat(message.Heading.W),
                message == null || !message.PrePositionFightingTarget.HasValue
                    ? string.Empty
                    : message.PrePositionFightingTarget.Value.ToString(),
                message == null || !message.FightingTarget.HasValue
                    ? string.Empty
                    : message.FightingTarget.Value.ToString(),
                message == null ? string.Empty : FormatInt(message.Version),
                message == null ? string.Empty : FormatInt(message.Flags),
                message == null ? string.Empty : message.FlagsText,
                message == null ? string.Empty : FormatInt(message.CharacterFlags),
                message == null ? string.Empty : FormatInt(message.AccountFlags),
                message == null ? string.Empty : FormatInt(message.Expansions),
                message == null ? string.Empty : npc != null ? "NPCInfo" : "PlayerInfo",
                npc == null ? string.Empty : FormatInt(npc.Family),
                npc == null ? string.Empty : FormatInt(npc.LosHeight),
                npc == null ? string.Empty : FormatInt(npc.UnknownData),
                npc == null ? string.Empty : FormatInt(npc.UnknownData2),
                npc == null || !npc.UnknownData3.HasValue ? string.Empty : FormatInt(npc.UnknownData3.Value),
                message == null ? string.Empty : FormatInt(message.Level),
                message == null ? string.Empty : FormatInt(message.Health),
                message == null ? string.Empty : FormatInt(message.HealthDamage),
                message == null ? string.Empty : FormatUInt(message.MonsterData),
                message == null ? string.Empty : FormatInt(message.MonsterScale),
                message == null ? string.Empty : FormatInt(message.VisualFlags),
                message == null ? string.Empty : FormatInt(message.VisibleTitle),
                message == null ? string.Empty : FormatUInt(message.AppearanceValue),
                message == null ? string.Empty : RawScfuFormatting.SideName(message.AppearanceSide),
                message == null ? string.Empty : RawScfuFormatting.FatnessName(message.AppearanceFatness),
                message == null ? string.Empty : RawScfuFormatting.BreedName(message.AppearanceBreed),
                message == null ? string.Empty : RawScfuFormatting.GenderName(message.AppearanceGender),
                message == null ? string.Empty : FormatUInt(message.AppearanceRace),
                message == null ? string.Empty : RawScfuFormatting.ToHex(message.Unknown1),
                message == null || !message.HeadMesh.HasValue ? string.Empty : FormatInt(message.HeadMesh.Value),
                message == null ? string.Empty : FormatInt(message.RunSpeedBase),
                message == null ? string.Empty : RawScfuFormatting.FormatActiveNanos(message.ActiveNanos),
                message == null || !message.WaypointOwner.HasValue ? string.Empty : message.WaypointOwner.Value.ToString(),
                message == null ? string.Empty : RawScfuFormatting.FormatWaypoints(message.Waypoints),
                message == null ? string.Empty : RawScfuFormatting.FormatTextureOverrides(message.TextureOverrides),
                message == null ? string.Empty : RawScfuFormatting.FormatTextures(message.Textures),
                message == null ? string.Empty : RawScfuFormatting.FormatMeshes(message.Meshes),
                message == null ? string.Empty : FormatInt(message.Flags2),
                message == null ? string.Empty : message.Flags2Text,
                message == null || !message.Owner.HasValue ? string.Empty : message.Owner.Value.ToString(),
                message == null ? string.Empty : FormatInt(message.Unknown2),
                message == null || !message.Unknown4.HasValue ? string.Empty : FormatInt(message.Unknown4.Value),
                message == null ? string.Empty : RawScfuFormatting.FormatSpecialAttacks(message.SpecialAttacks),
                message == null || !message.ImmuneUnknown.HasValue ? string.Empty : FormatInt(message.ImmuneUnknown.Value),
                message == null || !message.UnknownFlag3Data.HasValue ? string.Empty : FormatInt(message.UnknownFlag3Data.Value),
                message == null ? string.Empty : FormatInt(message.HeaderUnknown),
                message == null || !message.TowerUnknown.HasValue ? string.Empty : FormatInt(message.TowerUnknown.Value),
                message == null ? string.Empty : RawScfuFormatting.FormatPlayerInfo(message.Player),
                message == null ? string.Empty : FormatInt(message.N3MessageType),
                message == null ? string.Empty : message.DecodeFullyConsumed ? "true" : "false",
                npc == null ? string.Empty : FormatInt(npc.UnknownDataWidth),
                message == null || !message.Unknown3.HasValue ? string.Empty : FormatFloat(message.Unknown3.Value),
                message == null ? string.Empty : message.LegacyExtendedRunSpeedAlignment ? "true" : "false",
                message == null ? string.Empty : message.TerminalSpecialAttackSlotOmitted ? "true" : "false",
                message == null ? string.Empty : RawScfuFormatting.ToHex(message.OpaqueExtension)
            };

            for (int i = 0; i < fields.Count; i++)
            {
                fields[i] = Csv(fields[i]);
            }

            return string.Join(",", fields.ToArray());
        }

        private static byte[] CopyBody(byte[] packet)
        {
            if (packet == null || packet.Length <= RawSimpleCharFullUpdateDecoder.N3BodyOffset)
            {
                return new byte[0];
            }

            int length = packet.Length - RawSimpleCharFullUpdateDecoder.N3BodyOffset;
            var result = new byte[length];
            Buffer.BlockCopy(packet, RawSimpleCharFullUpdateDecoder.N3BodyOffset, result, 0, length);
            return result;
        }

        private static string FormatInt(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatUInt(uint value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }
    }

    internal static class RawScfuFormatting
    {
        internal static string IdentityTypeName(int value)
        {
            switch (value)
            {
                case 0:
                    return "None";
                case 0xC350:
                    return "SimpleChar";
                default:
                    return value.ToString(CultureInfo.InvariantCulture);
            }
        }

        internal static string SideName(int value)
        {
            string[] values = { "Neutral", "Clan", "OmniTek", "Monster", "Advisor", "Guardian", "Gm", "Mixed" };
            return NameOrNumber(values, value);
        }

        internal static string FatnessName(int value)
        {
            string[] values = { "Thin", "Normal", "Fat" };
            return NameOrNumber(values, value);
        }

        internal static string BreedName(int value)
        {
            string[] values = { "None", "Solitus", "Opifex", "Nanomage", "Atrox", "Special", "Monster", "HumanMonster" };
            return NameOrNumber(values, value);
        }

        internal static string GenderName(int value)
        {
            string[] values = { "None", "Uni", "Male", "Female" };
            return NameOrNumber(values, value);
        }

        internal static string FormatFlags(int value)
        {
            var names = new List<string>();
            AddFlag(names, value, 0x00000001, "IsNpc");
            AddFlag(names, value, 0x00000002, "UnknownFlag");
            AddFlag(names, value, 0x00000008, "UnknownFlag6");
            AddFlag(names, value, 0x00000010, "HasExtendedTextures");
            AddFlag(names, value, 0x00000020, "HasFightingTarget");
            AddFlag(names, value, 0x00000040, "HasPlayfieldId");
            AddFlag(names, value, 0x00000080, "HasHeadMesh");
            AddFlag(names, value, 0x00000100, "HasNoWeaponPairs");
            AddFlag(names, value, 0x00000200, "HasHeading");
            AddFlag(names, value, 0x00000400, "IsUnderAttack");
            AddFlag(names, value, 0x00000800, "HasSmallHealth");
            AddFlag(names, value, 0x00001000, "HasExtendedLevel");
            AddFlag(names, value, 0x00002000, "HasExtendedRunSpeed");
            AddFlag(names, value, 0x00004000, "HasSmallHealthDamage");
            AddFlag(names, value, 0x00010000, "HasWaypoints");
            AddFlag(names, value, 0x00020000, "HasSmallNpcFamily");
            AddFlag(names, value, 0x00080000, "HasSmallNpcLosHeight");
            AddFlag(names, value, 0x00200000, "UnknownFlag7");
            AddFlag(names, value, 0x00800000, "IsImmune");
            AddFlag(names, value, 0x01000000, "UnknownFlag3");
            AddFlag(names, value, 0x02000000, "UnknownDataFlag");
            AddFlag(names, value, 0x04000000, "HasOrgName");
            AddFlag(names, value, 0x08000000, "IsPet");
            AddFlag(names, value, 0x10000000, "UnknownFlag5");
            AddFlag(names, value, 0x20000000, "UnknownFlag4");
            return names.Count == 0 ? "0" : string.Join(", ", names.ToArray());
        }

        internal static string FormatFlags2(int value)
        {
            var names = new List<string>();
            AddFlag(names, value, 0x2, "Unknown1");
            AddFlag(names, value, 0x4, "HasOwner");
            AddFlag(names, value, 0x40, "Unknown3");
            AddFlag(names, value, 0x80, "Unknown4");
            AddFlag(names, value, 0x100, "Unknown5");
            AddFlag(names, value, 0x200, "Unknown6");
            AddFlag(names, value, 0x400, "Unknown7");
            AddFlag(names, value, 0x800, "Unknown8");
            return names.Count == 0 ? "0" : string.Join(", ", names.ToArray());
        }

        internal static string FormatPlayerInfo(RawScfuPlayerInfo value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "CurrentNano={0};Team={1};Swim={2};Strength={3};Agility={4};Stamina={5};Intelligence={6};Sense={7};Psychic={8};FirstName={9};LastName={10};OrgMetadata={11};OrgMetadataUnknown={12};OrgName={13}",
                value.CurrentNano,
                value.Team,
                value.Swim,
                value.StrengthBase,
                value.AgilityBase,
                value.StaminaBase,
                value.IntelligenceBase,
                value.SenseBase,
                value.PsychicBase,
                value.FirstName ?? string.Empty,
                value.LastName ?? string.Empty,
                value.OrgMetadata.HasValue ? value.OrgMetadata.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                value.OrgMetadataUnknown.HasValue ? value.OrgMetadataUnknown.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                value.OrgName ?? string.Empty);
        }

        internal static string FormatActiveNanos(IEnumerable<RawScfuActiveNano> values)
        {
            var result = new List<string>();
            foreach (RawScfuActiveNano value in values ?? new RawScfuActiveNano[0])
            {
                result.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}:{1}:{2}:{3}",
                        value.Identity,
                        value.NanoInstance,
                        value.Time1,
                        value.Time2));
            }

            return string.Join("|", result.ToArray());
        }

        internal static string FormatWaypoints(IEnumerable<RawScfuVector3> values)
        {
            var result = new List<string>();
            foreach (RawScfuVector3 value in values ?? new RawScfuVector3[0])
            {
                result.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:R}:{1:R}:{2:R}",
                        value.X,
                        value.Y,
                        value.Z));
            }

            return string.Join("|", result.ToArray());
        }

        internal static string FormatTextureOverrides(IEnumerable<RawScfuTextureOverride> values)
        {
            var result = new List<string>();
            foreach (RawScfuTextureOverride value in values ?? new RawScfuTextureOverride[0])
            {
                result.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}:{1}:{2}:{3}",
                        value.Name,
                        value.TextureId,
                        value.Unknown1,
                        value.Unknown2));
            }

            return string.Join("|", result.ToArray());
        }

        internal static string FormatTextures(IEnumerable<RawScfuTexture> values)
        {
            var result = new List<string>();
            foreach (RawScfuTexture value in values ?? new RawScfuTexture[0])
            {
                result.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}:{1}:{2}",
                        value.Place,
                        value.Id,
                        value.Unknown));
            }

            return string.Join("|", result.ToArray());
        }

        internal static string FormatMeshes(IEnumerable<RawScfuMesh> values)
        {
            var result = new List<string>();
            foreach (RawScfuMesh value in values ?? new RawScfuMesh[0])
            {
                result.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}:{1}:{2}:{3}",
                        value.Position,
                        value.Id,
                        value.OverrideTextureId,
                        value.Layer));
            }

            return string.Join("|", result.ToArray());
        }

        internal static string FormatSpecialAttacks(IEnumerable<RawScfuSpecialAttack> values)
        {
            var result = new List<string>();
            foreach (RawScfuSpecialAttack value in values ?? new RawScfuSpecialAttack[0])
            {
                if (value == null)
                {
                    result.Add("null");
                    continue;
                }

                result.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}:{1}:{2}:{3}:{4}:{5}:{6}",
                        value.Unknown1,
                        value.Unknown2,
                        value.Unknown3,
                        value.Unknown4,
                        value.Unknown5,
                        value.Name,
                        value.Unknown6));
            }

            return string.Join("|", result.ToArray());
        }

        internal static string ToHex(IEnumerable<byte> bytes)
        {
            if (bytes == null)
            {
                return string.Empty;
            }

            var result = new StringBuilder();
            foreach (byte value in bytes)
            {
                result.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }

        private static string NameOrNumber(string[] values, int value)
        {
            return value >= 0 && value < values.Length
                       ? values[value]
                       : value.ToString(CultureInfo.InvariantCulture);
        }

        private static void AddFlag(List<string> names, int value, int flag, string name)
        {
            if ((value & flag) == flag)
            {
                names.Add(name);
            }
        }
    }
}
