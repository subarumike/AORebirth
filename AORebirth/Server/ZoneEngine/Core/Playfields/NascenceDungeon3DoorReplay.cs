namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;
    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Playfields;

    /// <summary>
    /// Capture-backed ACG interior reveal: doors, terminals (floor buttons), and treasure
    /// for the whole grid-cell zone when the player enters it — no proximity radius.
    /// Evidence=20260830-140240 via NascenceDungeon3DoorCapture / NascenceDungeon3Rules.
    /// </summary>
    internal static class NascenceDungeon3DoorReplay
    {
        private static readonly Dictionary<int, long> LastZoneByCharacter = new Dictionary<int, long>();

        private static readonly Dictionary<int, HashSet<long>> RevealedZonesByCharacter =
            new Dictionary<int, HashSet<long>>();

        private static readonly object Gate = new object();

        private static readonly Dictionary<int, HashSet<string>> SentByCharacter =
            new Dictionary<int, HashSet<string>>();

        private static readonly HashSet<int> FloodedByCharacter = new HashSet<int>();

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
                FloodedByCharacter.Remove(id);
            }
        }

        internal static void SendForCharacter(IZoneClient client, ICharacter character)
        {
            ClearSent(character);
            foreach (long zoneKey in NascenceDungeon3RevealZones.AllZoneKeys())
            {
                SendZoneForCharacter(client, character, zoneKey, true);
            }

            // Capture 20260830-140240 MARKs: Button (down/up/boss) + Treasure chests.
            // SendCompressed like D2 chests — enqueue-only can miss SimpleItem/Chest mesh after PAF.
            SendAllFloorButtons(client, character);
            SendAllTreasureChests(client, character);

            // Interest wave at landing / current cell (capture first chests are ~150m from
            // InteriorLanding — full flood is client-culled; near-zone push still helps wing pads).
            if (character != null && character.Playfield != null)
            {
                float px = (float)character.RawCoordinates.X;
                float pz = (float)character.RawCoordinates.Z;
                if (px == 0f && pz == 0f)
                {
                    px = NascenceDungeon3Rules.InteriorLandingX;
                    pz = NascenceDungeon3Rules.InteriorLandingZ;
                }

                ResendChestsNearZone(
                    client,
                    character,
                    NascenceDungeon3RevealZones.ResolveZoneKey(px, pz));
            }

            if (character != null)
            {
                lock (Gate)
                {
                    FloodedByCharacter.Add(character.Identity.Instance);
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceDungeon3DoorReplay SendForCharacter done char="
                + (character == null ? 0 : character.Identity.Instance).ToString("X8")
                + " pf="
                + (character == null || character.Playfield == null
                       ? 0
                       : character.Playfield.Identity.Instance));

            // ACG land / CharInPlay can leave the client Use-busy so Attack never leaves the
            // client ("Please wait until previous action has finished"). Clear after flood.
            ClientActionBusyRuntime.Clear(character);
        }

        /// <summary>
        /// Capture MARK Button (down/up/boss) + paired Platform terminals — all 12 SIU hex.
        /// </summary>
        internal static void SendAllFloorButtons(IZoneClient client, ICharacter character)
        {
            var zoneClient = client as ZoneClient;
            if (zoneClient == null || character == null)
            {
                return;
            }

            string[] terminalHex = NascenceDungeon3DoorCapture.ZoneInTerminalPacketHex;
            if (terminalHex == null)
            {
                return;
            }

            int characterInstance = character.Identity.Instance;
            int playfieldInstance = character.Playfield.Identity.Instance;
            int sent = 0;
            for (int i = 0; i < terminalHex.Length; i++)
            {
                string hex = terminalHex[i];
                if (string.IsNullOrEmpty(hex))
                {
                    continue;
                }

                string key = hex.Length > 48 ? hex.Substring(hex.Length - 48) : hex;
                lock (Gate)
                {
                    HashSet<string> sentKeys;
                    if (!SentByCharacter.TryGetValue(characterInstance, out sentKeys))
                    {
                        sentKeys = new HashSet<string>(StringComparer.Ordinal);
                        SentByCharacter[characterInstance] = sentKeys;
                    }

                    sentKeys.Remove(key);
                    sentKeys.Add(key);
                }

                byte[] packet = HexToBytes(hex);
                ReplaceCharacterAndPlayfieldStamps(packet, characterInstance, playfieldInstance);
                zoneClient.SendCompressed(packet);
                sent++;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceDungeon3DoorReplay SendAllFloorButtons count=" + sent
                + " char=" + characterInstance.ToString("X8"));
        }

        internal static void SendAllTreasureChests(IZoneClient client, ICharacter character)
        {
            var zoneClient = client as ZoneClient;
            if (zoneClient == null || character == null)
            {
                return;
            }

            string[] chestHex = NascenceDungeon3DoorCapture.ZoneInChestPacketHex;
            if (chestHex == null)
            {
                return;
            }

            int sent = 0;
            for (int i = 0; i < chestHex.Length; i++)
            {
                if (string.IsNullOrEmpty(chestHex[i]) || !IsWorldTreasureChestHex(chestHex[i]))
                {
                    continue;
                }

                // Prefer closed-looking packets (001821 + quality 0x32) over opened (001861 + 0x7D).
                string hex = NormalizeTreasureChestHex(chestHex[i]);
                SendSingleChest(zoneClient, character, hex, true);
                sent++;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceDungeon3DoorReplay SendAllTreasureChests count=" + sent
                + " char=" + character.Identity.Instance.ToString("X8"));
        }

        // World Treasure uses template band 0BAF4Dxx; skip inventory/bank bags (e.g. 0BAB299A).
        private static bool IsWorldTreasureChestHex(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return false;
            }

            return hex.IndexOf("0000C7490BAF4D", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static void RevealBossWingForCharacter(IZoneClient client, ICharacter character)
        {
            if (client == null || character == null)
            {
                return;
            }

            foreach (long zoneKey in NascenceDungeon3RevealZones.BossWingZoneKeys())
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

            long zoneKey = NascenceDungeon3RevealZones.ResolveZoneKey(x, z);
            lock (Gate)
            {
                LastZoneByCharacter[character.Identity.Instance] = zoneKey;
            }

            SendZoneForCharacter(client, character, zoneKey, false);
            ResendChestsNearZone(client, character, zoneKey);
        }

        internal static void TrySendNearbyOnMove(IZoneClient client, ICharacter character)
        {
            if (client == null
                || character == null
                || character.Playfield == null
                || !NascenceDungeon3Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
            {
                return;
            }

            // Client often skips CharInPlay after ACG transfer — flood buttons/chests on first move.
            bool needsFlood;
            lock (Gate)
            {
                needsFlood = !FloodedByCharacter.Contains(character.Identity.Instance);
            }

            if (needsFlood)
            {
                SendForCharacter(client, character);
                return;
            }

            int id = character.Identity.Instance;
            float px = (float)character.RawCoordinates.X;
            float pz = (float)character.RawCoordinates.Z;
            long zoneKey = NascenceDungeon3RevealZones.ResolveZoneKey(px, pz);
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
                // Capture 20260830-140240: Treasures appear in interest waves as the player
                // walks (first wave ~1300,x — not at interior landing 1201,105). Zone-in flood
                // is distance-culled by the client; re-send closed chests for this cell +
                // neighbors (skip any with loot UI open).
                ResendChestsNearZone(client, character, zoneKey);
                ClientActionBusyRuntime.Clear(character);
            }

            // Do not re-ForceHavarisVisible on every move: SCFU re-send flashes the HP bar.
            // PlayfieldVisibilityInterestRuntimeService pins all D3 NPCs instead.
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
            if (!NascenceDungeon3RevealZones.TryFindChestHex(containerInstance, out hex))
            {
                return;
            }

            NascenceDungeon3RevealZones.EnsureBuilt();
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
                if (NascenceDungeon3RevealZones.ResolveZoneKey(px, pz) != zoneKey)
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

            if (!NascenceDungeon3Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
            {
                return;
            }

            var zoneClient = client as ZoneClient;
            if (zoneClient == null)
            {
                return;
            }

            float px = force
                ? NascenceDungeon3Rules.InteriorLandingX
                : (float)character.RawCoordinates.X;
            float pz = force
                ? NascenceDungeon3Rules.InteriorLandingZ
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

            NascenceDungeon3RevealZones.EnsureBuilt();
            int chestsRegistered = 0;
            int doors = SendZonePacketList(
                zoneClient,
                NascenceDungeon3RevealZones.DoorsInZone(zoneKey),
                characterInstance,
                playfieldInstance,
                sent,
                false,
                ref chestsRegistered);
            int terminals = SendZonePacketList(
                zoneClient,
                NascenceDungeon3RevealZones.TerminalsInZone(zoneKey),
                characterInstance,
                playfieldInstance,
                sent,
                false,
                ref chestsRegistered);
            int chests = SendZonePacketList(
                zoneClient,
                NascenceDungeon3RevealZones.ChestsInZone(zoneKey),
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
                        "NascenceDungeon3DoorReplay force={0} zone={1:X16} doors={2} terminals={3} chests={4} lootChests={5} char={6} pf={7} at=({8:0.#},{9:0.#})",
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

        // Capture packs some chests as already-opened (flag 001861 + QL 0x7D/0xE1).
        // Replay closed form (001821 + QL 0x32) so treasure is interactable on zone-in.
        private static string NormalizeTreasureChestHex(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return hex;
            }

            // Only rewrite opened-looking packets (flag 001861). D3 capture uses QL 0xE1;
            // D2-style opened used 0x7D. Blind E1 replace on closed packets is unsafe.
            if (hex.IndexOf("001861", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return hex;
            }

            return hex.Replace("001861", "001821")
                .Replace("0000007D", "00000032")
                .Replace("000000E1", "00000032");
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
            // Same restamp as doors/buttons: LowId 0x209103 is the ACG playfield stamp on
            // world Treasure CFU and must match the dyn lease (capture LowId == Playfield2).
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
                NascenceDungeon3TreasureLootService.Register(container);
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

            NascenceDungeon3RevealZones.EnsureBuilt();
            for (int z = 0; z < zoneKeys.Length; z++)
            {
                foreach (NascenceDungeon3RevealZones.ZonePacket entry in
                    NascenceDungeon3RevealZones.DoorsInZone(zoneKeys[z]))
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Hex))
                    {
                        continue;
                    }

                    byte[] packet = HexToBytes(entry.Hex);
                    ReplaceCharacterAndPlayfieldStamps(packet, characterInstance, playfieldInstance);
                    zoneClient.EnqueueOutboundCompressedBuffer(packet);
                }

            }

            // Chests: interest re-send is handled by ResendChestsNearZone on cell change.
        }

        /// <summary>
        /// Capture interest waves: push closed Treasures for this cell and 8 neighbors.
        /// Skip chests whose loot UI is open for this character.
        /// </summary>
        private static void ResendChestsNearZone(IZoneClient client, ICharacter character, long centerZoneKey)
        {
            var zoneClient = client as ZoneClient;
            if (zoneClient == null || character == null || character.Playfield == null || centerZoneKey == 0)
            {
                return;
            }

            int characterInstance = character.Identity.Instance;
            int playfieldInstance = character.Playfield.Identity.Instance;
            int cellX = (int)(centerZoneKey >> 32);
            int cellZ = (int)(centerZoneKey & 0xFFFFFFFFL);

            NascenceDungeon3RevealZones.EnsureBuilt();
            int sent = 0;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    long zoneKey = (((long)(cellX + dx)) << 32) | ((long)(cellZ + dz) & 0xFFFFFFFFL);
                    foreach (NascenceDungeon3RevealZones.ZonePacket entry in
                        NascenceDungeon3RevealZones.ChestsInZone(zoneKey))
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.Hex))
                        {
                            continue;
                        }

                        Identity container;
                        int staticInstance;
                        byte[] probe = HexToBytes(entry.Hex);
                        if (!TryParseContainer(probe, out container, out staticInstance))
                        {
                            continue;
                        }

                        if (NascenceDungeon3TreasureLootService.IsLootUiOpenFor(
                            characterInstance,
                            container.Instance))
                        {
                            continue;
                        }

                        string hex = NormalizeTreasureChestHex(entry.Hex);
                        SendSingleChest(zoneClient, character, hex, true);
                        sent++;
                    }
                }
            }

            if (sent > 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "NascenceDungeon3DoorReplay ResendChestsNearZone count=" + sent
                    + " char=" + characterInstance.ToString("X8")
                    + " pf=" + playfieldInstance.ToString("X8"));
            }
        }

        private static int SendZonePacketList(
            ZoneClient zoneClient,
            IEnumerable<NascenceDungeon3RevealZones.ZonePacket> packets,
            int characterInstance,
            int playfieldInstance,
            HashSet<string> sent,
            bool registerChests,
            ref int lootRegistered)
        {
            int sentNow = 0;
            foreach (NascenceDungeon3RevealZones.ZonePacket entry in packets)
            {
                // Chests are interest-gated via ResendChestsNearZone / SendAllTreasureChests.
                // Skip them in the one-shot zone list so Sent keys do not permanently block
                // capture-style wave re-sends when the player walks into a room.
                if (registerChests)
                {
                    Identity container;
                    int staticInstance;
                    byte[] probe = HexToBytes(NormalizeTreasureChestHex(entry.Hex));
                    if (TryParseContainer(probe, out container, out staticInstance))
                    {
                        NascenceDungeon3TreasureLootService.Register(container);
                        lootRegistered++;
                    }

                    continue;
                }

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
                        "NascenceDungeon3 floor button spawn missing hex terminal="
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
                zoneClient.SendCompressed(packet);
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon3 floor buttons swapped button={0:X8} despawn={1} spawn={2} char={3}",
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
                // Capture 20260830-140240 paired platforms (keep live IDs).
                // Entry: down 57FC32E6 @1362 ↔ up 57FC32E7 @1082
                case unchecked((int)0x57FC32E6):
                    despawnInstances = new[]
                    {
                        unchecked((int)0x57FC32E4),
                        unchecked((int)0x57FC32E6)
                    };
                    spawnInstances = new[]
                    {
                        unchecked((int)0x57FC32E5),
                        unchecked((int)0x57FC32E7)
                    };
                    return true;
                case unchecked((int)0x57FC32E7):
                    despawnInstances = new[]
                    {
                        unchecked((int)0x57FC32E5),
                        unchecked((int)0x57FC32E7)
                    };
                    spawnInstances = new[]
                    {
                        unchecked((int)0x57FC32E4),
                        unchecked((int)0x57FC32E6)
                    };
                    return true;
                // Mid: down 57FC32EA @1008 ↔ up 57FC32EB @440
                case unchecked((int)0x57FC32EA):
                    despawnInstances = new[]
                    {
                        unchecked((int)0x57FC32E8),
                        unchecked((int)0x57FC32EA)
                    };
                    spawnInstances = new[]
                    {
                        unchecked((int)0x57FC32E9),
                        unchecked((int)0x57FC32EB)
                    };
                    return true;
                case unchecked((int)0x57FC32EB):
                    despawnInstances = new[]
                    {
                        unchecked((int)0x57FC32E9),
                        unchecked((int)0x57FC32EB)
                    };
                    spawnInstances = new[]
                    {
                        unchecked((int)0x57FC32E8),
                        unchecked((int)0x57FC32EA)
                    };
                    return true;
                // Boss: boss 57FC32EE @661 ↔ up 57FC32EF @130
                case unchecked((int)0x57FC32EE):
                    despawnInstances = new[]
                    {
                        unchecked((int)0x57FC32EC),
                        unchecked((int)0x57FC32EE)
                    };
                    spawnInstances = new[]
                    {
                        unchecked((int)0x57FC32ED),
                        unchecked((int)0x57FC32EF)
                    };
                    return true;
                case unchecked((int)0x57FC32EF):
                    despawnInstances = new[]
                    {
                        unchecked((int)0x57FC32ED),
                        unchecked((int)0x57FC32EF)
                    };
                    spawnInstances = new[]
                    {
                        unchecked((int)0x57FC32EC),
                        unchecked((int)0x57FC32EE)
                    };
                    return true;
                default:
                    return false;
            }
        }

        private static string FindTerminalHex(int terminalInstance)
        {
            string[] packets = NascenceDungeon3DoorCapture.ZoneInTerminalPacketHex;
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
            return NascenceDungeon3RevealZones.TryParseWorldPosition(hex, out x, out y, out z);
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
            ReplaceInstance(packet, NascenceDungeon3DoorCapture.CapturedCharacterInstance, characterInstance);
            ReplaceInstance(packet, NascenceDungeon3DoorCapture.CapturedPlayfieldId, playfieldInstance);
            ReplaceInstance(packet, NascenceDungeon3Rules.DungeonPlayfieldId, playfieldInstance);
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
