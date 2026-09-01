namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Net;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// ACG room reveal for Nascence D3 — props are visible for the whole grid cell (zone),
    /// not by proximity radius. Capture 20260830-140240 prop positions from NascenceDungeon3DoorCapture, 96m cells.
    /// </summary>
    internal static class NascenceDungeon3RevealZones
    {
        internal const float CellSize = 48.0f;

        private static readonly object Gate = new object();

        private static bool built;

        private static Dictionary<long, List<ZonePacket>> doorsByZone =
            new Dictionary<long, List<ZonePacket>>();

        private static Dictionary<long, List<ZonePacket>> terminalsByZone =
            new Dictionary<long, List<ZonePacket>>();

        private static Dictionary<long, List<ZonePacket>> chestsByZone =
            new Dictionary<long, List<ZonePacket>>();

        internal sealed class ZonePacket
        {
            internal string Hex;

            internal string Key;

            internal float X;

            internal float Z;
        }

        internal static long ResolveZoneKey(float x, float z)
        {
            int cellX = (int)Math.Floor(x / CellSize);
            int cellZ = (int)Math.Floor(z / CellSize);
            return Pack(cellX, cellZ);
        }

        internal static void Reset()
        {
            lock (Gate)
            {
                built = false;
                doorsByZone.Clear();
                terminalsByZone.Clear();
                chestsByZone.Clear();
            }
        }

        internal static void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            lock (Gate)
            {
                if (built)
                {
                    return;
                }

                IndexPackets(NascenceDungeon3DoorCapture.ZoneInDoorPacketHex, doorsByZone);
                IndexPackets(NascenceDungeon3DoorCapture.ZoneInTerminalPacketHex, terminalsByZone);
                IndexPackets(NascenceDungeon3DoorCapture.ZoneInChestPacketHex, chestsByZone);
                built = true;

                Utility.LogUtil.Debug(
                    Utility.DebugInfoDetail.Zoning,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "NascenceDungeon3RevealZones indexed doors={0} terminals={1} chests={2} doorZones={3} terminalZones={4} chestZones={5}",
                        CountIndexed(doorsByZone),
                        CountIndexed(terminalsByZone),
                        CountIndexed(chestsByZone),
                        doorsByZone.Count,
                        terminalsByZone.Count,
                        chestsByZone.Count));
            }
        }

        internal static bool IsKnownChestInstance(int containerInstance)
        {
            if (containerInstance == 0)
            {
                return false;
            }

            EnsureBuilt();
            foreach (KeyValuePair<long, List<ZonePacket>> entry in chestsByZone)
            {
                List<ZonePacket> list = entry.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    Identity identity;
                    if (TryParseContainerInstance(list[i].Hex, out identity)
                        && identity.Instance == containerInstance)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int CountIndexed(Dictionary<long, List<ZonePacket>> map)
        {
            int count = 0;
            foreach (KeyValuePair<long, List<ZonePacket>> entry in map)
            {
                count += entry.Value.Count;
            }

            return count;
        }

        internal static IEnumerable<ZonePacket> DoorsInZone(long zoneKey)
        {
            EnsureBuilt();
            return GetZoneList(doorsByZone, zoneKey);
        }

        internal static IEnumerable<ZonePacket> TerminalsInZone(long zoneKey)
        {
            EnsureBuilt();
            return GetZoneList(terminalsByZone, zoneKey);
        }

        internal static IEnumerable<ZonePacket> ChestsInZone(long zoneKey)
        {
            EnsureBuilt();
            return GetZoneList(chestsByZone, zoneKey);
        }

        internal static IEnumerable<long> AllZoneKeys()
        {
            EnsureBuilt();
            var keys = new HashSet<long>();
            foreach (long key in doorsByZone.Keys)
            {
                keys.Add(key);
            }

            foreach (long key in terminalsByZone.Keys)
            {
                keys.Add(key);
            }

            foreach (long key in chestsByZone.Keys)
            {
                keys.Add(key);
            }

            return keys;
        }

        /// <summary>Boss wing ACG cells (capture exit pads + Havaris room), X &lt; 300 on live.</summary>
        internal static IEnumerable<long> BossWingZoneKeys()
        {
            EnsureBuilt();
            var keys = new HashSet<long>();
            CollectBossWingZones(doorsByZone, keys);
            CollectBossWingZones(terminalsByZone, keys);
            CollectBossWingZones(chestsByZone, keys);
            return keys;
        }

        private static void CollectBossWingZones(
            Dictionary<long, List<ZonePacket>> map,
            HashSet<long> keys)
        {
            foreach (KeyValuePair<long, List<ZonePacket>> entry in map)
            {
                List<ZonePacket> list = entry.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].X < NascenceDungeon3Rules.BossWingMaxWorldX)
                    {
                        keys.Add(entry.Key);
                        break;
                    }
                }
            }
        }

        internal static bool TryFindChestHex(int containerInstance, out string hex)
        {
            hex = null;
            EnsureBuilt();
            foreach (KeyValuePair<long, List<ZonePacket>> entry in chestsByZone)
            {
                List<ZonePacket> list = entry.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    Identity identity;
                    if (!TryParseContainerInstance(list[i].Hex, out identity)
                        || identity.Instance != containerInstance)
                    {
                        continue;
                    }

                    hex = list[i].Hex;
                    return true;
                }
            }

            return false;
        }

        internal static long ZoneKeyForContainer(int containerInstance)
        {
            EnsureBuilt();
            foreach (KeyValuePair<long, List<ZonePacket>> entry in chestsByZone)
            {
                List<ZonePacket> list = entry.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    Identity identity;
                    if (TryParseContainerInstance(list[i].Hex, out identity)
                        && identity.Instance == containerInstance)
                    {
                        return entry.Key;
                    }
                }
            }

            return 0;
        }

        private static IEnumerable<ZonePacket> GetZoneList(Dictionary<long, List<ZonePacket>> map, long zoneKey)
        {
            List<ZonePacket> list;
            if (map.TryGetValue(zoneKey, out list))
            {
                return list;
            }

            return new ZonePacket[0];
        }

        private static void IndexPackets(string[] hexPackets, Dictionary<long, List<ZonePacket>> target)
        {
            if (hexPackets == null)
            {
                return;
            }

            for (int i = 0; i < hexPackets.Length; i++)
            {
                string hex = hexPackets[i];
                if (string.IsNullOrEmpty(hex))
                {
                    continue;
                }

                float x;
                float y;
                float z;
                if (!TryParseWorldPosition(hex, out x, out y, out z))
                {
                    continue;
                }

                long zoneKey = ResolveZoneKey(x, z);
                List<ZonePacket> list;
                if (!target.TryGetValue(zoneKey, out list))
                {
                    list = new List<ZonePacket>();
                    target[zoneKey] = list;
                }

                list.Add(
                    new ZonePacket
                    {
                        Hex = hex,
                        Key = hex.Length > 48 ? hex.Substring(hex.Length - 48) : hex,
                        X = x,
                        Z = z
                    });
            }
        }

        private static long Pack(int cellX, int cellZ)
        {
            return ((long)cellX << 32) | (uint)cellZ;
        }

        private static bool TryParseContainerInstance(string hex, out Identity identity)
        {
            identity = new Identity();
            if (string.IsNullOrEmpty(hex))
            {
                return false;
            }

            byte[] packet = HexToBytes(hex);
            for (int i = 0; i + 8 <= packet.Length; i++)
            {
                if (packet[i] == 0x00 && packet[i + 1] == 0x00 && packet[i + 2] == 0xC7 && packet[i + 3] == 0x49)
                {
                    int instance = (packet[i + 4] << 24) | (packet[i + 5] << 16) | (packet[i + 6] << 8)
                                   | packet[i + 7];
                    identity = new Identity { Type = IdentityType.Container, Instance = instance };
                    return true;
                }
            }

            return false;
        }

        internal static bool TryParseWorldPosition(string hex, out float x, out float y, out float z)
        {
            x = y = z = 0;
            if (string.IsNullOrEmpty(hex) || hex.Length < 80)
            {
                return false;
            }

            byte[] packet = HexToBytes(hex);
            return TryParseWorldPosition(packet, out x, out y, out z);
        }

        /// <summary>
        /// Capture 20260823-171238: after C748/C73D/C741/C749 identity, dword 0, then 0x0B
        /// marker byte, then BE XYZ at +8/+12/+16 from that skip.
        /// </summary>
        internal static bool TryParseWorldPosition(byte[] packet, out float x, out float y, out float z)
        {
            x = y = z = 0;
            if (packet == null || packet.Length < 32)
            {
                return false;
            }

            for (int i = 0; i + 28 <= packet.Length; i++)
            {
                if (packet[i] != 0x00 || packet[i + 1] != 0x00 || packet[i + 2] != 0xC7)
                {
                    continue;
                }

                byte kind = packet[i + 3];
                if (kind != 0x48 && kind != 0x3D && kind != 0x41 && kind != 0x49)
                {
                    continue;
                }

                int o = i + 8;
                if (packet[o] != 0 || packet[o + 1] != 0 || packet[o + 2] != 0 || packet[o + 3] != 0)
                {
                    continue;
                }

                o += 5;
                if (o + 20 > packet.Length)
                {
                    continue;
                }

                x = ReadFloatBe(packet, o + 8);
                y = ReadFloatBe(packet, o + 12);
                z = ReadFloatBe(packet, o + 16);
                // Capture 20260830-140240: entry wing doors/chests sit at X≈1200–1370
                // (D1/D2 layouts stayed under X=1100). Floor buttons also use Y≈64–76.
                if (y > 20f && y < 90f && x > 50f && x < 1500f && z > 0f && z < 400f)
                {
                    return true;
                }
            }

            return false;
        }

        private static float ReadFloatBe(byte[] packet, int offset)
        {
            int bits = (packet[offset] << 24) | (packet[offset + 1] << 16) | (packet[offset + 2] << 8)
                       | packet[offset + 3];
            return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
        }

        private static byte[] HexToBytes(string hex)
        {
            int length = hex.Length / 2;
            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
