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
    internal static class NascenceDungeon2DoorReplay
    {
        private static readonly Dictionary<int, long> LastZoneByCharacter = new Dictionary<int, long>();

        private static readonly Dictionary<int, HashSet<long>> RevealedZonesByCharacter =
            new Dictionary<int, HashSet<long>>();

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
                RevealedZonesByCharacter.Remove(id);
            }
        }

        internal static void SendForCharacter(IZoneClient client, ICharacter character)
        {
            ClearSent(character);
            foreach (long zoneKey in NascenceDungeon2RevealZones.AllZoneKeys())
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

            string[] chestHex = NascenceDungeon2DoorCapture.ZoneInChestPacketHex;
            if (chestHex == null)
            {
                return;
            }

            int sent = 0;
            for (int i = 0; i < chestHex.Length; i++)
            {
                if (!string.IsNullOrEmpty(chestHex[i]))
                {
                    // Prefer closed-looking packets (001821 + quality 0x32) over opened (001861 + 0x7D).
                    string hex = NormalizeTreasureChestHex(chestHex[i]);
                    SendSingleChest(zoneClient, character, hex, true);
                    sent++;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                "NascenceDungeon2DoorReplay SendAllTreasureChests count=" + sent
                + " char=" + character.Identity.Instance.ToString("X8"));
        }

        internal static void RevealBossWingForCharacter(IZoneClient client, ICharacter character)
        {
            if (client == null || character == null)
            {
                return;
            }

            foreach (long zoneKey in NascenceDungeon2RevealZones.BossWingZoneKeys())
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

            long zoneKey = NascenceDungeon2RevealZones.ResolveZoneKey(x, z);
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
                || !NascenceDungeon2Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
            {
                return;
            }

            int id = character.Identity.Instance;
            float px = (float)character.RawCoordinates.X;
            float pz = (float)character.RawCoordinates.Z;
            long zoneKey = NascenceDungeon2RevealZones.ResolveZoneKey(px, pz);
            bool zoneChanged;
            lock (Gate)
            {
                long lastZone;
                zoneChanged = !LastZoneByCharacter.TryGetValue(id, out lastZone) || lastZone != zoneKey;
                if (zoneChanged)
                {
                    LastZoneByCharacter[id] = zoneKey;
                }
            }

            if (zoneChanged)
            {
                SendZoneForCharacter(client, character, zoneKey, false);
                // Re-push doors from every previously revealed cell so client distance-cull
                // cannot hide them after the player walks into a new room.
                ResendRevealedDoors(client, character);
                // Do NOT re-flood closed ChestFullUpdate here (D1 does not either):
                // resending closed CFU while the loot UI is open snaps the chest shut
                // and breaks subsequent loot. Zone-in SendForCharacter already floods chests.
            }

            // Do not re-ForceHavarisVisible on every move: SCFU re-send flashes the HP bar.
            // PlayfieldVisibilityInterestRuntimeService pins all D2 NPCs instead.
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
            if (!NascenceDungeon2RevealZones.TryFindChestHex(containerInstance, out hex))
            {
                return;
            }

            NascenceDungeon2RevealZones.EnsureBuilt();
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
                if (NascenceDungeon2RevealZones.ResolveZoneKey(px, pz) != zoneKey)
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

            if (!NascenceDungeon2Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
            {
                return;
            }

            var zoneClient = client as ZoneClient;
            if (zoneClient == null)
            {
                return;
            }

            float px = force
                ? NascenceDungeon2Rules.InteriorLandingX
                : (float)character.RawCoordinates.X;
            float pz = force
                ? NascenceDungeon2Rules.InteriorLandingZ
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

                HashSet<long> revealed;
                if (!RevealedZonesByCharacter.TryGetValue(characterInstance, out revealed))
                {
                    revealed = new HashSet<long>();
                    RevealedZonesByCharacter[characterInstance] = revealed;
                }

                revealed.Add(zoneKey);
            }

            NascenceDungeon2RevealZones.EnsureBuilt();
            int chestsRegistered = 0;
            int doors = SendZonePacketList(
                zoneClient,
                NascenceDungeon2RevealZones.DoorsInZone(zoneKey),
                characterInstance,
                playfieldInstance,
                sent,
                false,
                ref chestsRegistered);
            int terminals = SendZonePacketList(
                zoneClient,
                NascenceDungeon2RevealZones.TerminalsInZone(zoneKey),
                characterInstance,
                playfieldInstance,
                sent,
                false,
                ref chestsRegistered);
            int chests = SendZonePacketList(
                zoneClient,
                NascenceDungeon2RevealZones.ChestsInZone(zoneKey),
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
                        "NascenceDungeon2DoorReplay force={0} zone={1:X16} doors={2} terminals={3} chests={4} lootChests={5} char={6} pf={7} at=({8:0.#},{9:0.#})",
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
                // Zone change already re-sends chest/door interest; full reconcile re-SCFU's pinned NPCs.
            }
        }

        // Capture packs some chests as already-opened (flag 001861 + QL 0x7D).
        // Replay closed form (001821 + QL 0x32) so treasure is interactable on zone-in.
        private static string NormalizeTreasureChestHex(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return hex;
            }

            return hex.Replace("001861", "001821").Replace("0000007D", "00000032");
        }

        private static void SendSingleChest(
            ZoneClient zoneClient,
            ICharacter character,
            string hex)
        {
            SendSingleChest(zoneClient, character, hex, false);
        }

        private static void SendSingleChest(
            ZoneClient zoneClient,
            ICharacter character,
            string hex,
            bool forceImmediate)
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
            if (forceImmediate)
            {
                zoneClient.SendCompressed(packet);
            }
            else
            {
                zoneClient.EnqueueOutboundCompressedBuffer(packet);
            }

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
                NascenceDungeon2TreasureLootService.Register(container);
            }
        }

        private static void ResendRevealedDoors(IZoneClient client, ICharacter character)
        {
            var zoneClient = client as ZoneClient;
            if (zoneClient == null || character == null || character.Playfield == null)
            {
                return;
            }

            int characterInstance = character.Identity.Instance;
            int playfieldInstance = character.Playfield.Identity.Instance;
            long[] zoneKeys;
            lock (Gate)
            {
                HashSet<long> revealed;
                if (!RevealedZonesByCharacter.TryGetValue(characterInstance, out revealed)
                    || revealed.Count == 0)
                {
                    return;
                }

                zoneKeys = new long[revealed.Count];
                revealed.CopyTo(zoneKeys);
            }

            NascenceDungeon2RevealZones.EnsureBuilt();
            for (int z = 0; z < zoneKeys.Length; z++)
            {
                foreach (NascenceDungeon2RevealZones.ZonePacket entry in
                    NascenceDungeon2RevealZones.DoorsInZone(zoneKeys[z]))
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Hex))
                    {
                        continue;
                    }

                    byte[] packet = HexToBytes(entry.Hex);
                    ReplaceCharacterAndPlayfieldStamps(packet, characterInstance, playfieldInstance);
                    zoneClient.EnqueueOutboundCompressedBuffer(packet);
                }

                // Do not re-flood chests here: closed ChestFullUpdate snaps open loot UI shut
                // and leaves the client unable to reopen. Zone-in SendAllTreasureChests is enough.
            }
        }

        private static int SendZonePacketList(
            ZoneClient zoneClient,
            IEnumerable<NascenceDungeon2RevealZones.ZonePacket> packets,
            int characterInstance,
            int playfieldInstance,
            HashSet<string> sent,
            bool registerChests,
            ref int lootRegistered)
        {
            int sentNow = 0;
            foreach (NascenceDungeon2RevealZones.ZonePacket entry in packets)
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
                zoneClient.EnqueueOutboundCompressedBuffer(packet);
                sentNow++;

                if (registerChests)
                {
                    Identity container;
                    int staticInstance;
                    if (TryParseContainer(packet, out container, out staticInstance))
                    {
                        NascenceDungeon2TreasureLootService.Register(container);
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
                        "NascenceDungeon2 floor button spawn missing hex terminal="
                        + spawnInstances[i].ToString("X8"));
                    continue;
                }

                string key = hex.Length > 48 ? hex.Substring(hex.Length - 48) : hex;
                lock (Gate)
                {
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
                    "NascenceDungeon2 floor buttons swapped button={0:X8} despawn={1} spawn={2} char={3}",
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
                // Capture 20260823-182854 platform+button pairs (keep live IDs).
                case unchecked((int)0x57EC6ADF): // Button (down)
                    despawnInstances = new[]
                    {
                        unchecked((int)0x57EC6ADD),
                        unchecked((int)0x57EC6ADF)
                    };
                    spawnInstances = new[]
                    {
                        unchecked((int)0x57EC6ADE),
                        unchecked((int)0x57EC6AE0)
                    };
                    return true;
                case unchecked((int)0x57EC6AE0): // Button (up) lower
                    despawnInstances = new[]
                    {
                        unchecked((int)0x57EC6ADE),
                        unchecked((int)0x57EC6AE0)
                    };
                    spawnInstances = new[]
                    {
                        unchecked((int)0x57EC6ADD),
                        unchecked((int)0x57EC6ADF)
                    };
                    return true;
                case unchecked((int)0x57EC6AE3): // Button (boss)
                    despawnInstances = new[]
                    {
                        unchecked((int)0x57EC6AE1),
                        unchecked((int)0x57EC6AE3)
                    };
                    spawnInstances = new[]
                    {
                        unchecked((int)0x57EC6AE2),
                        unchecked((int)0x57EC6AE4)
                    };
                    return true;
                case unchecked((int)0x57EC6AE4): // Button (up) boss
                    despawnInstances = new[]
                    {
                        unchecked((int)0x57EC6AE2),
                        unchecked((int)0x57EC6AE4)
                    };
                    spawnInstances = new[]
                    {
                        unchecked((int)0x57EC6AE1),
                        unchecked((int)0x57EC6AE3)
                    };
                    return true;
                default:
                    return false;
            }
        }

        private static string FindTerminalHex(int terminalInstance)
        {
            string[] packets = NascenceDungeon2DoorCapture.ZoneInTerminalPacketHex;
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
            return NascenceDungeon2RevealZones.TryParseWorldPosition(hex, out x, out y, out z);
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
            ReplaceInstance(packet, NascenceDungeon2DoorCapture.CapturedCharacterInstance, characterInstance);
            ReplaceInstance(packet, NascenceDungeon2DoorCapture.CapturedPlayfieldId, playfieldInstance);
            ReplaceInstance(packet, NascenceDungeon2Rules.DungeonPlayfieldId, playfieldInstance);
            ReplaceInstance(packet, NascenceDungeon2Rules.LegacyDungeonPlayfieldId, playfieldInstance);
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
