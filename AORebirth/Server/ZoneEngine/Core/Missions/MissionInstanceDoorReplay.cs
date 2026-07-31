namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    /// <summary>
    /// Capture-backed Door/Chest/Terminal replay.
    /// Map-at-start gold screenshot: black fog, only start room lit, yellow door icons nearby.
    /// Zone-in sends nearby doors only; walk streams the rest. Full flood on remapped PFs lit the
    /// entire grey floorplan (unlike native live PF ids in capture 080425).
    /// </summary>
    internal static class MissionInstanceDoorReplay
    {
        internal const int BrokenMachineTemplateId = 0x027B47;

        // Zone-in: gold 184103 sends exactly 10 DoorFullUpdates (D7–E0) with PAF.
        // Flooding walk doors (E1–E4) at enter lit the grey open floorplan.
        private const float ZoneInRevealRadius = 500.0f;

        private const int ZoneInMaxDoors = 10;

        // Walk: gold 143126 / 141740 — next-segment doors at (230–260, 190–220), 40–65m out.
        private const float WalkRevealRadius = 70.0f;

        private const int WalkMaxDoorsPerTick = 7;

        private const int MoveThrottleMs = 400;

        private static readonly object Gate = new object();

        private static readonly Dictionary<int, HashSet<string>> SentByCharacter =
            new Dictionary<int, HashSet<string>>();

        private static readonly Dictionary<int, int> LastMoveTickByCharacter = new Dictionary<int, int>();

        private static readonly Dictionary<int, int> ZoneInTickByCharacter = new Dictionary<int, int>();

        // After zone-in force, ignore walk stream briefly so CharDCMove does not flood next
        // segment while still in the entrance bubble (diag: early radius flood → full PF Map).
        private const int ZoneInWalkSuppressMs = 2000;

        // Gold 143126: OUT move 298→290.3 then IN door burst. Trigger at ~8m from spawn.
        private const float ZoneInWalkMinLeaveSpawn = 8.0f;

        private static string NameForChest(int staticInstance, int templateId)
        {
            if (templateId == BrokenMachineTemplateId)
            {
                return "Broken Machine";
            }

            switch (staticInstance)
            {
                case 0x1841:
                    return "Small Crate";
                case 0x1801:
                    return "Barrel";
                case 0x1C61:
                case 0x1C21:
                    return "Treasure";
                default:
                    return "container";
            }
        }

        public static void ClearSent(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            lock (Gate)
            {
                SentByCharacter.Remove(character.Identity.Instance);
                LastMoveTickByCharacter.Remove(character.Identity.Instance);
                ZoneInTickByCharacter.Remove(character.Identity.Instance);
            }
        }

        public static void SendForCharacter(IZoneClient client, ICharacter character)
        {
            if (character != null
                && character.Playfield != null
                && MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(
                    character.Playfield.Identity.Instance))
            {
                return;
            }

            // Force flood must always retransmit. Early post-PAF sends are often ignored by the
            // client (see ClientConnected); if we keep those keys in SentByCharacter, the later
            // FullCharacter / CharInPlay retries log sent=0 and the instance stays doorless
            // (grey polygon map, no Door mesh). Gold 080425 delivers doors that stick — we
            // re-flood until CharInPlay so the client actually accepts them.
            ClearSent(character);
            SendNearbyForCharacter(client, character, true);
        }

        public static void TrySendNearbyOnMove(IZoneClient client, ICharacter character)
        {
            if (client == null || character == null || character.Playfield == null)
            {
                return;
            }

            if (!MissionInstanceService.IsMissionInstancePlayfield(character.Playfield.Identity.Instance))
            {
                return;
            }

            if (MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(
                character.Playfield.Identity.Instance))
            {
                return;
            }

            int now = Environment.TickCount;
            lock (Gate)
            {
                int zoneInTick;
                if (ZoneInTickByCharacter.TryGetValue(character.Identity.Instance, out zoneInTick)
                    && unchecked(now - zoneInTick) < ZoneInWalkSuppressMs)
                {
                    return;
                }

                int last;
                if (LastMoveTickByCharacter.TryGetValue(character.Identity.Instance, out last)
                    && unchecked(now - last) < MoveThrottleMs)
                {
                    return;
                }

                LastMoveTickByCharacter[character.Identity.Instance] = now;
            }

            // Gold 135121: next-segment flood only after leaving the entrance bubble.
            MissionShape shape = MissionInstanceShapeCatalog.PickShape(
                character.Playfield.Identity.Instance,
                null);
            if (shape != null)
            {
                float dx = (float)character.RawCoordinates.X - shape.SpawnX;
                float dz = (float)character.RawCoordinates.Z - shape.SpawnZ;
                if (((dx * dx) + (dz * dz)) < (ZoneInWalkMinLeaveSpawn * ZoneInWalkMinLeaveSpawn))
                {
                    return;
                }
            }

            SendNearbyForCharacter(client, character, false);
        }

        private static void SendNearbyForCharacter(IZoneClient client, ICharacter character, bool force)
        {
            if (client == null || character == null || character.Playfield == null)
            {
                return;
            }

            if (!MissionInstanceService.IsMissionInstancePlayfield(character.Playfield.Identity.Instance))
            {
                return;
            }

            var zoneClient = client as ZoneClient;
            if (zoneClient == null)
            {
                return;
            }

            int pf = character.Playfield.Identity.Instance;
            MissionShape shape = MissionInstanceShapeCatalog.PickShape(pf, null);
            int shapePf = shape != null ? shape.CapturedPlayfieldId : pf;
            MissionRollType objective = MissionRollType.KillPerson;
            bool hasObjective = MissionInstanceService.TryGetStampedObjective(pf, out objective);
            bool repair = hasObjective && objective == MissionRollType.RepairMachine;
            bool findItemReturn = (hasObjective && objective == MissionRollType.FindItemReturn)
                                  || MissionFindItemService.CharacterHasActiveReturnMission(character);

            // Zone-in (force): anchor to shape entrance — RawCoordinates can still be outdoor.
            float px;
            float pz;
            if (force && shape != null)
            {
                px = shape.SpawnX;
                pz = shape.SpawnZ;
            }
            else
            {
                px = (float)character.RawCoordinates.X;
                pz = (float)character.RawCoordinates.Z;
            }

            HashSet<string> sent;
            lock (Gate)
            {
                if (!SentByCharacter.TryGetValue(character.Identity.Instance, out sent))
                {
                    sent = new HashSet<string>(StringComparer.Ordinal);
                    SentByCharacter[character.Identity.Instance] = sent;
                }
            }

            float doorRadius = force ? ZoneInRevealRadius : WalkRevealRadius;
            int doorMax = force ? ZoneInMaxDoors : WalkMaxDoorsPerTick;

            // Capture 20260728-093557: ~8 doors within ~40m of spawn (298,255); do not flood all 18.
            if (force && (repair || shapePf == 1441792))
            {
                doorRadius = 40.0f;
                doorMax = 8;
            }

            if (force)
            {
                lock (Gate)
                {
                    ZoneInTickByCharacter[character.Identity.Instance] = Environment.TickCount;
                }
            }

            int sentNow = 0;
            int chestsRegistered = 0;
            int machinesRegistered = 0;
            try
            {
                string[] doors = MissionInstanceDynelCapture.GetDoors(shapePf);
                if (doors == null || doors.Length == 0)
                {
                    doors = MissionInstanceDoorCapture.CapturedDoorPacketHex;
                }

                sentNow += SendNearestPackets(
                    zoneClient,
                    character,
                    doors,
                    MissionInstanceDynelCapture.CapturedCharacterInstance,
                    false,
                    repair,
                    findItemReturn,
                    px,
                    pz,
                    doorRadius,
                    doorMax,
                    sent,
                    ref chestsRegistered,
                    ref machinesRegistered);

                // Gold 184103: 8 ChestFullUpdate with enter flood for PF 1419349.
                string[] chests = MissionInstanceDynelCapture.GetChests(shapePf);
                string[] terminals = MissionInstanceDynelCapture.GetTerminals(shapePf);
                if (findItemReturn)
                {
                    // Capture 20260728-095215: Encrypted Info Capsule is a world Terminal, not a person.
                    terminals = MissionInstanceDynelCapture.Terminals_FindItemReturn;
                }

                float propRadius = force ? ZoneInRevealRadius : WalkRevealRadius;
                int propMax = force ? 8 : 4;
                if (findItemReturn && force)
                {
                    propRadius = 200.0f;
                    propMax = 4;
                }

                sentNow += SendNearestPackets(
                    zoneClient,
                    character,
                    chests,
                    MissionInstanceDynelCapture.CapturedCharacterInstance,
                    true,
                    repair,
                    findItemReturn,
                    px,
                    pz,
                    propRadius,
                    propMax,
                    sent,
                    ref chestsRegistered,
                    ref machinesRegistered);
                sentNow += SendNearestPackets(
                    zoneClient,
                    character,
                    terminals,
                    MissionInstanceDynelCapture.CapturedCharacterInstance,
                    false,
                    repair,
                    findItemReturn,
                    px,
                    pz,
                    propRadius,
                    propMax,
                    sent,
                    ref chestsRegistered,
                    ref machinesRegistered);

                if (findItemReturn)
                {
                    // Do not re-spawn capsule if the player already carries it (re-enter spam).
                    if (MissionFindItemService.CharacterAlreadyHoldingReturnItem(character))
                    {
                        MissionDiagnostics.Log(
                            "FINDITEM-RETURN-SKIP-SIFU char={0} alreadyHolding=1",
                            character.Identity.Instance);
                    }
                    else
                    {
                        // Gold 20260728-095215: capsule ~45m into the mish, not on the exit door.
                        float sx;
                        float sy;
                        float sz;
                        ResolveFindItemReturnCapsulePosition(shape, px, pz, out sx, out sy, out sz);
                        if (MissionFindItemService.TrySendWorldCapsule(
                            zoneClient,
                            character,
                            sx,
                            sy,
                            sz))
                        {
                            sentNow++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.ErrorException(ex);
            }

            if (force || sentNow > 0)
            {
                MissionDiagnostics.Log(
                    "DOOR-CHEST-REPLAY char={0} pf={1} shape={2} sent={3} lootChests={4} machines={5} force={6} radius={7:0.#} max={8}",
                    character.Identity.Instance,
                    pf,
                    shapePf,
                    sentNow,
                    chestsRegistered,
                    machinesRegistered,
                    force ? 1 : 0,
                    doorRadius,
                    doorMax);
            }
        }

        /// <summary>
        /// Send up to <paramref name="maxCount"/> nearest unsent packets within radius (nearest first).
        /// </summary>
        private static int SendNearestPackets(
            ZoneClient zoneClient,
            ICharacter character,
            string[] hexPackets,
            int capturedCharacterInstance,
            bool registerChests,
            bool repairObjective,
            bool findItemReturnObjective,
            float playerX,
            float playerZ,
            float radius,
            int maxCount,
            HashSet<string> sent,
            ref int lootRegistered,
            ref int machinesRegistered)
        {
            if (hexPackets == null || hexPackets.Length == 0 || maxCount <= 0)
            {
                return 0;
            }

            float radiusSq = radius * radius;
            var candidates = new List<DoorCandidate>(hexPackets.Length);
            for (int i = 0; i < hexPackets.Length; i++)
            {
                string hex = hexPackets[i];
                if (string.IsNullOrEmpty(hex))
                {
                    continue;
                }

                string key = hex.Length > 48 ? hex.Substring(hex.Length - 48) : hex;
                lock (Gate)
                {
                    if (sent.Contains(key))
                    {
                        continue;
                    }
                }

                float dx;
                float dy;
                float dz;
                float distSq;
                bool remap = false;
                if (findItemReturnObjective && !registerChests
                    && hex.IndexOf("C73D", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Hex capsule replay is superseded by TrySendWorldCapsule; skip so we do not
                    // stack a second Terminal (or remap onto the exit door).
                    continue;
                }
                else if (!TryParseWorldPosition(hex, out dx, out dy, out dz))
                {
                    continue;
                }
                else
                {
                    float ddx = dx - playerX;
                    float ddz = dz - playerZ;
                    distSq = (ddx * ddx) + (ddz * ddz);
                    if (distSq > radiusSq)
                    {
                        continue;
                    }
                }

                candidates.Add(
                    new DoorCandidate
                    {
                        Hex = hex,
                        Key = key,
                        DistSq = distSq,
                        RemapX = dx,
                        RemapY = dy,
                        RemapZ = dz,
                        RemapPosition = remap
                    });
            }

            if (candidates.Count == 0)
            {
                return 0;
            }

            candidates.Sort((a, b) => a.DistSq.CompareTo(b.DistSq));

            int sentCount = 0;
            int limit = Math.Min(maxCount, candidates.Count);
            for (int i = 0; i < limit; i++)
            {
                DoorCandidate c = candidates[i];
                byte[] packet = HexToBytes(c.Hex);
                ReplaceInstance(packet, capturedCharacterInstance, character.Identity.Instance);
                ReplaceInstance(
                    packet,
                    MissionInstanceDoorCapture.CapturedCharacterInstance,
                    character.Identity.Instance);
                ReplaceInstance(packet, unchecked((int)0x797E30D7), character.Identity.Instance);
                ReplaceInstance(packet, unchecked((int)0x765A6D34), character.Identity.Instance);
                RetargetPlayfieldIds(packet, character.Playfield.Identity.Instance);
                if (c.RemapPosition)
                {
                    TryWriteWorldPosition(packet, c.RemapX, c.RemapY, c.RemapZ);
                }

                zoneClient.SendCompressed(packet);
                lock (Gate)
                {
                    sent.Add(c.Key);
                }

                sentCount++;

                if (!registerChests)
                {
                    // Capture 20260728-093557: Repair finish target is Terminal Radar Display (not Container).
                    Identity terminal;
                    if (TryParseTerminal(packet, out terminal))
                    {
                        if (repairObjective)
                        {
                            MissionMachineTracker.Register(terminal);
                            machinesRegistered++;
                        }

                        if (findItemReturnObjective)
                        {
                            MissionFindItemService.RegisterWorldPickup(terminal);
                            MissionDiagnostics.Log(
                                "FINDITEM-RETURN-TERMINAL char={0} pf={1} terminal={2}",
                                character.Identity.Instance,
                                character.Playfield.Identity.Instance,
                                terminal);
                        }
                    }

                    continue;
                }

                Identity container;
                int staticInstance;
                int templateId;
                if (!TryParseContainer(packet, out container, out staticInstance, out templateId))
                {
                    continue;
                }

                string name = NameForChest(staticInstance, templateId);
                if (templateId == BrokenMachineTemplateId)
                {
                    if (repairObjective)
                    {
                        MissionMachineTracker.Register(container);
                        machinesRegistered++;
                    }
                    else
                    {
                        MissionLootPropService.Register(container, name);
                        lootRegistered++;
                    }
                }
                else
                {
                    MissionLootPropService.Register(container, name);
                    lootRegistered++;
                }
            }

            return sentCount;
        }

        private struct DoorCandidate
        {
            public string Hex;

            public string Key;

            public float DistSq;

            public float RemapX;

            public float RemapY;

            public float RemapZ;

            public bool RemapPosition;
        }

        /// <summary>
        /// Capture 20260728-095215: capsule ~45m into the layout from spawn, not on the exit door.
        /// </summary>
        private static void ResolveFindItemReturnCapsulePosition(
            MissionShape shape,
            float fallbackX,
            float fallbackZ,
            out float x,
            out float y,
            out float z)
        {
            if (shape != null)
            {
                x = shape.SpawnX + 45.6f;
                y = shape.SpawnY > 0.1f ? shape.SpawnY : 5.1f;
                z = shape.SpawnZ + 18.6f;
                return;
            }

            x = fallbackX + 45.6f;
            y = 5.1f;
            z = fallbackZ + 18.6f;
        }

        private static bool TryWriteWorldPosition(byte[] packet, float x, float y, float z)
        {
            if (packet == null || packet.Length < 40)
            {
                return false;
            }

            for (int i = 0; i + 28 <= packet.Length; i++)
            {
                if (packet[i] != 0x00 || packet[i + 1] != 0x00 || packet[i + 2] != 0xC7
                    || packet[i + 3] != 0x3D)
                {
                    continue;
                }

                // Terminal identity at i; MsgVersion (0x0000000B), owner type/instance zeros, then XYZ.
                // Gold 20260728-095215 Encrypted Info Capsule SIFU.
                int xyz = i + 20;
                if (xyz + 12 > packet.Length)
                {
                    return false;
                }

                WriteFloatBe(packet, xyz, x);
                WriteFloatBe(packet, xyz + 4, y);
                WriteFloatBe(packet, xyz + 8, z);
                return true;
            }

            return false;
        }

        private static void WriteFloatBe(byte[] packet, int offset, float value)
        {
            byte[] bits = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                packet[offset] = bits[3];
                packet[offset + 1] = bits[2];
                packet[offset + 2] = bits[1];
                packet[offset + 3] = bits[0];
            }
            else
            {
                packet[offset] = bits[0];
                packet[offset + 1] = bits[1];
                packet[offset + 2] = bits[2];
                packet[offset + 3] = bits[3];
            }
        }

        private static bool TryParseWorldPosition(string hex, out float x, out float y, out float z)
        {
            x = y = z = 0;
            if (string.IsNullOrEmpty(hex) || hex.Length < 80)
            {
                return false;
            }

            byte[] packet = HexToBytes(hex);
            for (int i = 0; i + 28 <= packet.Length; i++)
            {
                if (packet[i] != 0x00 || packet[i + 1] != 0x00 || packet[i + 2] != 0xC7)
                {
                    continue;
                }

                byte kind = packet[i + 3];
                if (kind != 0x48 && kind != 0x49 && kind != 0x3D)
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
                if (y > 1f && y < 30f && x > -50f && x < 800f && z > -50f && z < 800f)
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

        private static bool TryParseTerminal(byte[] packet, out Identity identity)
        {
            identity = new Identity();
            if (packet == null || packet.Length < 20)
            {
                return false;
            }

            for (int i = 0; i + 8 <= packet.Length; i++)
            {
                if (packet[i] == 0x00 && packet[i + 1] == 0x00 && packet[i + 2] == 0xC7 && packet[i + 3] == 0x3D)
                {
                    int instance = (packet[i + 4] << 24) | (packet[i + 5] << 16) | (packet[i + 6] << 8)
                                   | packet[i + 7];
                    identity = new Identity { Type = IdentityType.Terminal, Instance = instance };
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseContainer(
            byte[] packet,
            out Identity identity,
            out int staticInstance,
            out int templateId)
        {
            identity = new Identity();
            staticInstance = 0;
            templateId = 0;
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

            for (int i = 0; i + 8 <= packet.Length; i++)
            {
                if (packet[i] == 0x00 && packet[i + 1] == 0x00 && packet[i + 2] == 0x02 && packet[i + 3] == 0xBE)
                {
                    templateId = (packet[i + 4] << 24) | (packet[i + 5] << 16) | (packet[i + 6] << 8)
                                 | packet[i + 7];
                    break;
                }
            }

            return true;
        }

        private static void RetargetPlayfieldIds(byte[] packet, int livePlayfieldId)
        {
            int[] captured = MissionInstanceDynelCapture.ShapePlayfieldIds;
            for (int c = 0; c < captured.Length; c++)
            {
                ReplaceInstance(packet, captured[c], livePlayfieldId);
            }

            ReplaceInstance(packet, 1413198, livePlayfieldId);
            ReplaceInstance(packet, 1413191, livePlayfieldId);
        }

        private static void ReplaceInstance(byte[] packet, int from, int to)
        {
            if (packet == null || from == to)
            {
                return;
            }

            byte f0 = (byte)(from >> 24);
            byte f1 = (byte)(from >> 16);
            byte f2 = (byte)(from >> 8);
            byte f3 = (byte)from;

            // Door PF ids sit at unaligned offsets (e.g. 69) — must scan every byte, not i+=4.
            for (int i = 0; i + 4 <= packet.Length; i++)
            {
                if (packet[i] == f0 && packet[i + 1] == f1 && packet[i + 2] == f2 && packet[i + 3] == f3)
                {
                    packet[i] = (byte)(to >> 24);
                    packet[i + 1] = (byte)(to >> 16);
                    packet[i + 2] = (byte)(to >> 8);
                    packet[i + 3] = (byte)to;
                    i += 3;
                }
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(
                    hex.Substring(i * 2, 2),
                    System.Globalization.NumberStyles.HexNumber);
            }

            return bytes;
        }
    }
}
