namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;
    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;

    /// <summary>
    /// Capture-backed ACG interior reveal: doors, terminals (floor buttons), and treasure
    /// for the whole grid-cell zone when the player enters it — no proximity radius.
    /// </summary>
    internal static class NascenceDungeon1DoorReplay
    {
        private static readonly Dictionary<int, long> LastZoneByCharacter = new Dictionary<int, long>();

        private static readonly object Gate = new object();

        private static readonly Dictionary<int, HashSet<string>> SentByCharacter =
            new Dictionary<int, HashSet<string>>();

        internal static void ClearSent(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            lock (Gate)
            {
                int id = character.Identity.Instance;
                SentByCharacter.Remove(id);
                LastZoneByCharacter.Remove(id);
            }
        }

        internal static void SendForCharacter(IZoneClient client, ICharacter character)
        {
            ClearSent(character);
            foreach (long zoneKey in NascenceDungeon1RevealZones.AllZoneKeys())
            {
                SendZoneForCharacter(client, character, zoneKey, true);
            }

            SendAllTreasureChests(client, character);
        }

        internal static void SendAllTreasureChests(IZoneClient client, ICharacter character)
        {
            var zoneClient = client as ZoneClient;
            if (zoneClient == null || character == null)
            {
                return;
            }

            string[] chestHex = NascenceDungeon1DoorCapture.ZoneInChestPacketHex;
            if (chestHex == null)
            {
                return;
            }

            for (int i = 0; i < chestHex.Length; i++)
            {
                if (!string.IsNullOrEmpty(chestHex[i]))
                {
                    SendSingleChest(zoneClient, character, chestHex[i]);
                }
            }
        }

        internal static void RevealBossWingForCharacter(IZoneClient client, ICharacter character)
        {
            if (client == null || character == null)
            {
                return;
            }

            foreach (long zoneKey in NascenceDungeon1RevealZones.BossWingZoneKeys())
            {
                SendZoneForCharacter(client, character, zoneKey, false);
            }
        }

        internal static void RevealZoneAtPosition(IZoneClient client, ICharacter character, float x, float z)
        {
            if (client == null || character == null)
            {
                return;
            }

            long zoneKey = NascenceDungeon1RevealZones.ResolveZoneKey(x, z);
            lock (Gate)
            {
                LastZoneByCharacter.Remove(character.Identity.Instance);
            }

            SendZoneForCharacter(client, character, zoneKey, false);
        }

        internal static void TrySendNearbyOnMove(IZoneClient client, ICharacter character)
        {
            if (client == null
                || character == null
                || character.Playfield == null
                || !NascenceDungeon1Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
            {
                return;
            }

            int id = character.Identity.Instance;
            float px = (float)character.RawCoordinates.X;
            float pz = (float)character.RawCoordinates.Z;
            long zoneKey = NascenceDungeon1RevealZones.ResolveZoneKey(px, pz);
            lock (Gate)
            {
                long lastZone;
                if (LastZoneByCharacter.TryGetValue(id, out lastZone) && lastZone == zoneKey)
                {
                    return;
                }

                LastZoneByCharacter[id] = zoneKey;
            }

            SendZoneForCharacter(client, character, zoneKey, false);
            if (px < NascenceDungeon1Rules.BossWingMaxWorldX)
            {
                var playfield = character.Playfield as Playfield;
                if (playfield != null)
                {
                    NascenceDungeon1BossRoomRuntime.ForceHavarisVisible(playfield, character);
                }
            }
        }

        internal static void RespawnTreasureChestInZone(
            Playfield playfield,
            long zoneKey,
            int containerInstance)
        {
            if (playfield == null || zoneKey == 0)
            {
                return;
            }

            string hex;
            if (!NascenceDungeon1RevealZones.TryFindChestHex(containerInstance, out hex))
            {
                return;
            }

            NascenceDungeon1RevealZones.EnsureBuilt();
            foreach (ICharacter character in playfield.EnumerateActiveCharacters())
            {
                if (character == null
                    || character.Controller == null
                    || !(character.Controller is PlayerController))
                {
                    continue;
                }

                var zoneClient = character.Controller.Client as ZoneClient;
                if (zoneClient == null)
                {
                    continue;
                }

                float px = (float)character.RawCoordinates.X;
                float pz = (float)character.RawCoordinates.Z;
                if (NascenceDungeon1RevealZones.ResolveZoneKey(px, pz) != zoneKey)
                {
                    continue;
                }

                SendSingleChest(zoneClient, character, hex);
            }
        }

        private static void SendZoneForCharacter(
            IZoneClient client,
            ICharacter character,
            long zoneKey,
            bool force)
        {
            if (client == null || character == null || character.Playfield == null || zoneKey == 0)
            {
                return;
            }

            if (!NascenceDungeon1Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
            {
                return;
            }

            var zoneClient = client as ZoneClient;
            if (zoneClient == null)
            {
                return;
            }

            float px = force
                ? NascenceDungeon1Rules.InteriorLandingX
                : (float)character.RawCoordinates.X;
            float pz = force
                ? NascenceDungeon1Rules.InteriorLandingZ
                : (float)character.RawCoordinates.Z;

            int characterInstance = character.Identity.Instance;
            int playfieldInstance = character.Playfield.Identity.Instance;

            HashSet<string> sent;
            lock (Gate)
            {
                if (!SentByCharacter.TryGetValue(characterInstance, out sent))
                {
                    sent = new HashSet<string>(StringComparer.Ordinal);
                    SentByCharacter[characterInstance] = sent;
                }

                if (force)
                {
                    LastZoneByCharacter[characterInstance] = zoneKey;
                }
            }

            NascenceDungeon1RevealZones.EnsureBuilt();
            int chestsRegistered = 0;
            int doors = SendZonePacketList(
                zoneClient,
                NascenceDungeon1RevealZones.DoorsInZone(zoneKey),
                characterInstance,
                playfieldInstance,
                sent,
                false,
                ref chestsRegistered);
            int terminals = SendZonePacketList(
                zoneClient,
                NascenceDungeon1RevealZones.TerminalsInZone(zoneKey),
                characterInstance,
                playfieldInstance,
                sent,
                false,
                ref chestsRegistered);
            int chests = SendZonePacketList(
                zoneClient,
                NascenceDungeon1RevealZones.ChestsInZone(zoneKey),
                characterInstance,
                playfieldInstance,
                sent,
                true,
                ref chestsRegistered);

            if (force || doors + terminals + chests > 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "NascenceDungeon1DoorReplay force={0} zone={1:X16} doors={2} terminals={3} chests={4} lootChests={5} char={6} pf={7} at=({8:0.#},{9:0.#})",
                        force ? 1 : 0,
                        zoneKey,
                        doors,
                        terminals,
                        chests,
                        chestsRegistered,
                        characterInstance,
                        playfieldInstance,
                        px,
                        pz));
            }

            var playfield = character.Playfield as Playfield;
            if (playfield != null)
            {
                playfield.RefreshCharacterVisibility(character);
            }
        }

        private static void SendSingleChest(ZoneClient zoneClient, ICharacter character, string hex)
        {
            if (zoneClient == null || character == null || string.IsNullOrEmpty(hex))
            {
                return;
            }

            int characterInstance = character.Identity.Instance;
            int playfieldInstance = character.Playfield.Identity.Instance;
            string key = hex.Length > 48 ? hex.Substring(hex.Length - 48) : hex;

            byte[] packet = HexToBytes(hex);
            ReplaceCharacterAndPlayfieldStamps(packet, characterInstance, playfieldInstance);
            zoneClient.SendCompressed(packet);

            lock (Gate)
            {
                HashSet<string> sent;
                if (!SentByCharacter.TryGetValue(characterInstance, out sent))
                {
                    sent = new HashSet<string>(StringComparer.Ordinal);
                    SentByCharacter[characterInstance] = sent;
                }

                sent.Add(key);
            }

            Identity container;
            int staticInstance;
            if (TryParseContainer(packet, out container, out staticInstance))
            {
                NascenceDungeon1TreasureLootService.Register(container);
            }
        }

        private static int SendZonePacketList(
            ZoneClient zoneClient,
            IEnumerable<NascenceDungeon1RevealZones.ZonePacket> packets,
            int characterInstance,
            int playfieldInstance,
            HashSet<string> sent,
            bool registerChests,
            ref int lootRegistered)
        {
            int sentNow = 0;
            foreach (NascenceDungeon1RevealZones.ZonePacket entry in packets)
            {
                lock (Gate)
                {
                    if (sent.Contains(entry.Key))
                    {
                        continue;
                    }

                    sent.Add(entry.Key);
                }

                byte[] packet = HexToBytes(entry.Hex);
                ReplaceCharacterAndPlayfieldStamps(packet, characterInstance, playfieldInstance);
                zoneClient.SendCompressed(packet);
                sentNow++;

                if (registerChests)
                {
                    Identity container;
                    int staticInstance;
                    if (TryParseContainer(packet, out container, out staticInstance))
                    {
                        NascenceDungeon1TreasureLootService.Register(container);
                        lootRegistered++;
                    }
                }
            }

            return sentNow;
        }

        internal static void RefreshFloorButtonsAfterTeleport(
            ZoneClient zoneClient,
            ICharacter character,
            int buttonInstanceUsed)
        {
            if (zoneClient == null || character == null)
            {
                return;
            }

            int[] despawnInstances;
            int[] spawnInstances;
            if (!TryResolveFloorButtonSwap(buttonInstanceUsed, out despawnInstances, out spawnInstances))
            {
                return;
            }

            int characterInstance = character.Identity.Instance;
            int playfieldInstance = character.Playfield.Identity.Instance;

            for (int i = 0; i < despawnInstances.Length; i++)
            {
                character.Send(
                    DespawnMessageHandler.Default.Create(
                        new Identity
                        {
                            Type = IdentityType.Terminal,
                            Instance = despawnInstances[i]
                        }));
            }

            HashSet<string> sent;
            lock (Gate)
            {
                if (!SentByCharacter.TryGetValue(characterInstance, out sent))
                {
                    sent = new HashSet<string>(StringComparer.Ordinal);
                    SentByCharacter[characterInstance] = sent;
                }
            }

            for (int i = 0; i < spawnInstances.Length; i++)
            {
                string hex = FindTerminalHex(spawnInstances[i]);
                if (string.IsNullOrEmpty(hex))
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "NascenceDungeon1 floor button spawn missing hex terminal="
                        + spawnInstances[i].ToString("X8"));
                    continue;
                }

                string key = hex.Length > 48 ? hex.Substring(hex.Length - 48) : hex;
                lock (Gate)
                {
                    // Force re-send after local teleport — client may have despawned far dynels.
                    sent.Remove(key);
                    sent.Add(key);
                }

                byte[] packet = HexToBytes(hex);
                ReplaceCharacterAndPlayfieldStamps(packet, characterInstance, playfieldInstance);
                zoneClient.EnqueueOutboundCompressedBuffer(packet);
            }

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon1 floor buttons swapped button={0:X8} despawn={1} spawn={2} char={3}",
                    buttonInstanceUsed,
                    despawnInstances.Length,
                    spawnInstances.Length,
                    characterInstance));
        }

        private static bool TryResolveFloorButtonSwap(
            int buttonInstance,
            out int[] despawnInstances,
            out int[] spawnInstances)
        {
            despawnInstances = null;
            spawnInstances = null;
            switch (buttonInstance)
            {
                case unchecked((int)0x57EC93CE):
                    despawnInstances = new[]
                    {
                        unchecked((int)0x57EC93CC),
                        unchecked((int)0x57EC93CE)
                    };
                    spawnInstances = new[]
                    {
                        unchecked((int)0x57EC93CD),
                        unchecked((int)0x57EC93CF)
                    };
                    return true;
                case unchecked((int)0x57EC93CF):
                    despawnInstances = new[]
                    {
                        unchecked((int)0x57EC93CD),
                        unchecked((int)0x57EC93CF)
                    };
                    spawnInstances = new[]
                    {
                        unchecked((int)0x57EC93CC),
                        unchecked((int)0x57EC93CE)
                    };
                    return true;
                case unchecked((int)0x57EC93D2):
                    despawnInstances = new[]
                    {
                        unchecked((int)0x57EC93D0),
                        unchecked((int)0x57EC93D2)
                    };
                    spawnInstances = new[]
                    {
                        unchecked((int)0x57EC93D1),
                        unchecked((int)0x57EC93D3)
                    };
                    return true;
                case unchecked((int)0x57EC93D3):
                    despawnInstances = new[]
                    {
                        unchecked((int)0x57EC93D1),
                        unchecked((int)0x57EC93D3)
                    };
                    spawnInstances = new[]
                    {
                        unchecked((int)0x57EC93D0),
                        unchecked((int)0x57EC93D2)
                    };
                    return true;
                default:
                    return false;
            }
        }

        private static string FindTerminalHex(int terminalInstance)
        {
            string[] packets = NascenceDungeon1DoorCapture.ZoneInTerminalPacketHex;
            if (packets == null)
            {
                return null;
            }

            byte b0 = (byte)((terminalInstance >> 24) & 0xFF);
            byte b1 = (byte)((terminalInstance >> 16) & 0xFF);
            byte b2 = (byte)((terminalInstance >> 8) & 0xFF);
            byte b3 = (byte)(terminalInstance & 0xFF);
            for (int i = 0; i < packets.Length; i++)
            {
                string hex = packets[i];
                if (string.IsNullOrEmpty(hex))
                {
                    continue;
                }

                byte[] packet = HexToBytes(hex);
                for (int o = 0; o + 8 <= packet.Length; o++)
                {
                    if (packet[o] == 0x00 && packet[o + 1] == 0x00 && packet[o + 2] == 0xC7 && packet[o + 3] == 0x3D
                        && packet[o + 4] == b0 && packet[o + 5] == b1 && packet[o + 6] == b2 && packet[o + 7] == b3)
                    {
                        return hex;
                    }
                }
            }

            return null;
        }

        private static bool TryParseWorldPosition(string hex, out float x, out float y, out float z)
        {
            return NascenceDungeon1RevealZones.TryParseWorldPosition(hex, out x, out y, out z);
        }

        private static float ReadFloatBe(byte[] packet, int offset)
        {
            int bits = (packet[offset] << 24) | (packet[offset + 1] << 16) | (packet[offset + 2] << 8)
                       | packet[offset + 3];
            return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
        }

        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return new byte[0];
            }

            int length = hex.Length / 2;
            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        private static void ReplaceCharacterAndPlayfieldStamps(
            byte[] packet,
            int characterInstance,
            int playfieldInstance)
        {
            ReplaceInstance(packet, NascenceDungeon1DoorCapture.CapturedCharacterInstance, characterInstance);
            ReplaceInstance(packet, NascenceDungeon1DoorCapture.CapturedPlayfieldId, playfieldInstance);
            ReplaceInstance(packet, NascenceDungeon1Rules.LegacyCapturedPlayfieldId, playfieldInstance);
            ReplaceInstance(packet, NascenceDungeon1Rules.LiveCapturedPlayfieldId220326, playfieldInstance);
            ReplaceInstance(packet, NascenceDungeon1Rules.ReservedDungeonPlayfieldId, playfieldInstance);
            ReplaceInstance(packet, NascenceDungeon1Rules.DungeonPlayfieldId, playfieldInstance);
        }

        private static void ReplaceInstance(byte[] packet, int oldInstance, int newInstance)
        {
            if (packet == null || packet.Length < 4 || oldInstance == newInstance)
            {
                return;
            }

            byte[] oldBytes =
            {
                (byte)((oldInstance >> 24) & 0xFF),
                (byte)((oldInstance >> 16) & 0xFF),
                (byte)((oldInstance >> 8) & 0xFF),
                (byte)(oldInstance & 0xFF)
            };
            byte[] newBytes =
            {
                (byte)((newInstance >> 24) & 0xFF),
                (byte)((newInstance >> 16) & 0xFF),
                (byte)((newInstance >> 8) & 0xFF),
                (byte)(newInstance & 0xFF)
            };

            for (int i = 0; i <= packet.Length - 4; i++)
            {
                if (packet[i] == oldBytes[0]
                    && packet[i + 1] == oldBytes[1]
                    && packet[i + 2] == oldBytes[2]
                    && packet[i + 3] == oldBytes[3])
                {
                    packet[i] = newBytes[0];
                    packet[i + 1] = newBytes[1];
                    packet[i + 2] = newBytes[2];
                    packet[i + 3] = newBytes[3];
                }
            }
        }

        private static bool TryParseContainer(byte[] packet, out Identity identity, out int staticInstance)
        {
            identity = new Identity();
            staticInstance = 0;
            if (packet == null || packet.Length < 20)
            {
                return false;
            }

            bool found = false;
            for (int i = 0; i + 8 <= packet.Length; i++)
            {
                if (packet[i] == 0x00 && packet[i + 1] == 0x00 && packet[i + 2] == 0xC7 && packet[i + 3] == 0x49)
                {
                    int instance = (packet[i + 4] << 24) | (packet[i + 5] << 16) | (packet[i + 6] << 8)
                                   | packet[i + 7];
                    identity = new Identity { Type = IdentityType.Container, Instance = instance };
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }

            for (int i = 0; i + 8 <= packet.Length; i++)
            {
                if (packet[i] == 0x00 && packet[i + 1] == 0x00 && packet[i + 2] == 0x00 && packet[i + 3] == 0x20)
                {
                    staticInstance = (packet[i + 4] << 24) | (packet[i + 5] << 16) | (packet[i + 6] << 8)
                                     | packet[i + 7];
                    break;
                }
            }

            return true;
        }

        private static bool TryParseDynelIdentity(string hex, out IdentityType identityType, out int identityInstance)
        {
            identityType = 0;
            identityInstance = 0;
            byte[] packet = HexToBytes(hex);
            for (int i = 0; i + 8 <= packet.Length; i++)
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

                identityType = (IdentityType)((packet[i + 2] << 8) | packet[i + 3]);
                identityInstance = (packet[i + 4] << 24)
                                   | (packet[i + 5] << 16)
                                   | (packet[i + 6] << 8)
                                   | packet[i + 7];
                return identityInstance != 0;
            }

            return false;
        }
    }
}
