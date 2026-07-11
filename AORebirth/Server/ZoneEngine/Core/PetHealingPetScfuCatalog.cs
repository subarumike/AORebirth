#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Text;

    #endregion

    /// <summary>
    /// Capture-backed MP healing pet SCFU wire templates from 20260711-195926 (MT02-MT04)
    /// and 20260711-013417 (BSLX). MT01 is derived from MT02 with patched tier stats.
    /// </summary>
    internal static class PetHealingPetScfuCatalog
    {
        private const int ScfuLevelOffsetFromNpcFamilyMarker = 5;

        private const int ScfuHealthOffsetFromNpcFamilyMarker = 6;

        private const int ScfuScaleOffsetFromNpcFamilyMarker = 14;

        private static readonly byte[] NpcFamilyMarker = { 0x60, 0x00, 0x0B, 0x00 };

        internal sealed class Template
        {
            public byte[] ScfuWire { get; set; }

            public byte[] PetSpellListWireA { get; set; }

            public byte[] PetSpellListWireB { get; set; }
        }

        private static readonly Dictionary<string, Template> TemplatesByHash =
            new Dictionary<string, Template>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "BSLX",
                    new Template
                    {
                        ScfuWire = Hex(
                            "00C7000A0001010B00000DAD35FE2868271B3A6B0000C3507957F058003A0A2A6A530015300B4372FE6C40D2C26C42D2494A0000000000000000000000003F800000000005E80A42656C616D6F72746500100812010000000060000B0000C0265700000177C10078001F000000001C0000000000000000000000000301000100010001000100000002000002E1000007E26D6574617065745F6865616C696E670000000000000000000000000000000000000467DA0000000000000001000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000003F1000000020000"),
                    }
                },
                {
                    "MT02",
                    new Template
                    {
                        ScfuWire = Hex(
                            "0019000A0001010A00000DAD35FE2868271B3A6B0000C350795A8330003A0A2A4A530000027643CB6AF741867AE2443793AA0000000000000000000000003F800000000005E80A53616C76696E6F757300100812010000000060000B000021026100000177C10066001F000000001C00000000000000000000000003010001000100010001000000020000A8000007E26D6574617065745F6865616C696E670000000000000000000000000000000000000467DA0000000000000001000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000003F1000000020000"),
                        PetSpellListWireA = Hex(
                            "0028000A0001006600000DAD35FE28684D4501140000C350795A833000000007E20000CF2218615BEE00000004000000000000000100000000000000000000000000000000000002B10000009600000000000000000000C350795A8330000000000000000000"),
                        PetSpellListWireB = Hex(
                            "0029000A0001006600000DAD35FE28684D4501140000C350795A833000000007E20000CF2218615BEF00000004000000000000000100000000000000000000000000000000000002170000007D00000000000000000000C350795A8330000000000000000000"),
                    }
                },
                {
                    "MT03",
                    new Template
                    {
                        ScfuWire = Hex(
                            "0010000A0001010B00000DAD35FE2868271B3A6B0000C350795A8338003A0A2A6A530000027643CB6AF741867AE2443793AA0000000000000000000000003F800000000005E80A56616C656E7479696100100812010000000060000B000037054100000177C1006A001F000000001C000000000000000000000000030100010001000100010000000200000117000007E26D6574617065745F6865616C696E670000000000000000000000000000000000000467DA0000000000000001000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000003F1000000020000"),
                    }
                },
                {
                    "MT04",
                    new Template
                    {
                        ScfuWire = Hex(
                            "0003000A0001010700000DAD35FE2868271B3A6B0000C350795A835F003A0A2A6A530000027643CB6AF741867AE2443793AA0000000000000000000000003F800000000005E80653616E6F6F00100812010000000060000B00004D08E200000177C1006D001F000000001C00000000000000000000000003010001000100010001000000020000018E000007E26D6574617065745F6865616C696E670000000000000000000000000000000000000467DA0000000000000001000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000003F1000000020000"),
                    }
                },
            };

        public static bool TryGetScfuWire(string petHash, out byte[] scfuWire)
        {
            scfuWire = null;
            Template template = ResolveTemplate(petHash);
            if (template == null || template.ScfuWire == null)
            {
                return false;
            }

            scfuWire = (byte[])template.ScfuWire.Clone();
            return true;
        }

        public static bool TryGetPetSpellListWires(string petHash, out byte[] wireA, out byte[] wireB)
        {
            wireA = null;
            wireB = null;
            Template template = ResolveTemplate(petHash);
            if (template == null)
            {
                return false;
            }

            if (template.PetSpellListWireA != null)
            {
                wireA = (byte[])template.PetSpellListWireA.Clone();
            }

            if (template.PetSpellListWireB != null)
            {
                wireB = (byte[])template.PetSpellListWireB.Clone();
            }

            return wireA != null || wireB != null;
        }

        private static Template ResolveTemplate(string petHash)
        {
            if (string.IsNullOrWhiteSpace(petHash))
            {
                return null;
            }

            Template template;
            if (TemplatesByHash.TryGetValue(petHash, out template))
            {
                return template;
            }

            if (string.Equals(petHash, "MT01", StringComparison.OrdinalIgnoreCase))
            {
                return BuildMedinosTemplate();
            }

            return null;
        }

        private static Template BuildMedinosTemplate()
        {
            Template salvinous;
            if (!TemplatesByHash.TryGetValue("MT02", out salvinous))
            {
                return null;
            }

            byte[] scfu = (byte[])salvinous.ScfuWire.Clone();
            PatchAsciiName(scfu, "Salvinous\0", "Medinos\0\0\0");
            PatchTierStats(scfu, 14, 181, 100);
            return new Template
            {
                ScfuWire = scfu,
                PetSpellListWireA = salvinous.PetSpellListWireA,
                PetSpellListWireB = salvinous.PetSpellListWireB,
            };
        }

        private static void PatchAsciiName(byte[] packet, string oldName, string newName)
        {
            byte[] oldBytes = Encoding.ASCII.GetBytes(oldName);
            byte[] newBytes = Encoding.ASCII.GetBytes(newName);
            if (newBytes.Length != oldBytes.Length)
            {
                throw new InvalidOperationException("Healing pet SCFU name patch requires equal-length names.");
            }

            int offset = IndexOf(packet, oldBytes);
            if (offset < 0)
            {
                return;
            }

            Buffer.BlockCopy(newBytes, 0, packet, offset, newBytes.Length);
        }

        private static void PatchTierStats(byte[] packet, int level, int health, int scale)
        {
            int markerOffset = IndexOf(packet, NpcFamilyMarker);
            if (markerOffset < 0)
            {
                return;
            }

            int levelOffset = markerOffset + ScfuLevelOffsetFromNpcFamilyMarker;
            int healthOffset = markerOffset + ScfuHealthOffsetFromNpcFamilyMarker;
            int scaleOffset = markerOffset + ScfuScaleOffsetFromNpcFamilyMarker;
            packet[levelOffset] = (byte)Math.Max(0, Math.Min(level, byte.MaxValue));
            WriteHealthBigEndianUInt16(packet, healthOffset, health);
            WriteUInt16LittleEndian(packet, scaleOffset, (ushort)Math.Max(0, scale));
        }

        private static void WriteHealthBigEndianUInt16(byte[] buffer, int offset, int health)
        {
            ushort encoded = (ushort)Math.Max(0, Math.Min(health, ushort.MaxValue));
            buffer[offset] = (byte)(encoded >> 8);
            buffer[offset + 1] = (byte)encoded;
            buffer[offset + 2] = 0;
            buffer[offset + 3] = 0;
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            if (haystack == null || needle == null || needle.Length == 0 || haystack.Length < needle.Length)
            {
                return -1;
            }

            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void WriteInt32LittleEndian(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt16LittleEndian(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }

        private static byte[] Hex(string hex)
        {
            if (hex.Length % 2 != 0)
            {
                throw new InvalidOperationException("Invalid hex template length.");
            }

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
