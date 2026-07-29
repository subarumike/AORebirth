from pathlib import Path

root = Path(r"AORebirth/Server/ZoneEngine")

# --- DynelCapture ---
dynel = root / "Core/Missions/MissionInstanceDynelCapture.cs"
frag = Path("tools-temp/_tmp_dynel_1493063.csfrag").read_text(encoding="utf-8")
src = dynel.read_text(encoding="utf-8")

old_ids = "public static readonly int[] ShapePlayfieldIds = { 1441800, 1443840, 1460226, 1456133, 1419310, 1419335, 1419382, 1419349 };"
new_ids = "public static readonly int[] ShapePlayfieldIds = { 1441800, 1443840, 1460226, 1456133, 1419310, 1419335, 1419382, 1419349, 1493063 };"
if old_ids not in src:
    raise SystemExit("ShapePlayfieldIds marker missing")
src = src.replace(old_ids, new_ids, 1)

marker = "        public static string[] GetDoors(int playfieldId)"
if marker not in src:
    raise SystemExit("GetDoors marker missing")
if "Doors_1493063" not in src:
    src = src.replace(marker, frag + marker, 1)

src = src.replace(
    "                case 1419349: return Doors_1419349;\n                default: return Doors_1419310;",
    "                case 1419349: return Doors_1419349;\n                case 1493063: return Doors_1493063;\n                default: return Doors_1419310;",
    1,
)
src = src.replace(
    "                case 1419349: return Chests_1419349;\n                default: return Chests_1419310;",
    "                case 1419349: return Chests_1419349;\n                case 1493063: return Chests_1493063;\n                default: return Chests_1419310;",
    1,
)

old_term = """        public static string[] GetTerminals(int playfieldId)
        {
            // Radar Display replay from a foreign layout crashed some clients on zone-in.
            // Keep Archive Storage only for its captured shape; no always-on hologram flood.
            if (playfieldId == 1419310)
            {
                return Terminals_1419310;
            }

            return new string[0];
        }"""
new_term = """        public static string[] GetTerminals(int playfieldId)
        {
            // Radar Display replay from a foreign layout crashed some clients on zone-in.
            // Keep Archive Storage only for its captured shape; no always-on hologram flood.
            if (playfieldId == 1419310)
            {
                return Terminals_1419310;
            }

            // Repair Machine: Theft Secure Food Dispenser (capture 20260727-mission-repair-machine-new).
            if (playfieldId == 1493063)
            {
                return Terminals_1493063;
            }

            return new string[0];
        }"""
if old_term not in src:
    raise SystemExit("GetTerminals block missing")
src = src.replace(old_term, new_term, 1)
dynel.write_text(src, encoding="utf-8")
print("patched", dynel)

# --- Shape catalog: insert shape after 1419349 block ---
cat = root / "Core/Playfields/MissionInstanceShapeCatalog.cs"
csrc = cat.read_text(encoding="utf-8")
shape_insert = """
        // Shape playfield 1493063 from capture 20260727-mission-repair-machine-new (ACG D7425E)
        new MissionShape
        {
            CapturedPlayfieldId = 1493063,
            // Gold PAF CharacterCoordinates (enter).
            SpawnX = 1.801f, SpawnY = 10.01f, SpawnZ = 265.01f,
            Npcs = new[]
            {
                new MissionNpc
                {
                    Name = "Fresh Clan Trader",
                    Role = MissionNpcRole.Trash,
                    Level = 10, Health = 227, MonsterData = 26092, Scale = 92, HeadMesh = 40694,
                    X = 45.934f, Y = 10.01f, Z = 275.055f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 40975 }, new[] { 1, 21824 }, new[] { 2, 9615 }, new[] { 3, 21819 }, new[] { 4, 21831 } },
                    Meshes = new[] { new[] { 0, 20108, 17998, 2 }, new[] { 0, 40694, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Fresh Clan Bureaucrat",
                    Role = MissionNpcRole.Trash,
                    Level = 10, Health = 182, MonsterData = 26159, Scale = 92, HeadMesh = 40173,
                    X = 40.652f, Y = 10.01f, Z = 284.477f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40173, 0, 4 } },
                },
                new MissionNpc
                {
                    Name = "Fresh Clan Bureaucrat",
                    Role = MissionNpcRole.Trash,
                    Level = 10, Health = 182, MonsterData = 26159, Scale = 92, HeadMesh = 40173,
                    X = 36.188f, Y = 10.01f, Z = 246.215f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40173, 0, 4 } },
                },
            },
        },

"""
anchor = "// Shape playfield 1441800 from capture 20260725-151009 (fog building D7417D)"
if "CapturedPlayfieldId = 1493063" not in csrc:
    if anchor not in csrc:
        raise SystemExit("shape anchor missing")
    csrc = csrc.replace(anchor, shape_insert + anchor, 1)

payload_case = """                case 1493063:
                    // Repair Machine ACG D7425E — capture 20260727-mission-repair-machine-new.
                    return new byte[]
                    {
                       0x00, 0x00, 0xC7, 0x9F, 0x00, 0xD7, 0x42, 0x5E,
                       0x00, 0x00, 0x00, 0x02, 0x00, 0x03, 0x00, 0x1E,
                       0x00, 0x1E, 0x00, 0x40, 0x00, 0x00, 0x01, 0x5A,
                       0x64, 0x64, 0x64, 0x00, 0x00, 0x00, 0x09, 0x00,
                       0x30, 0x00, 0x00, 0x03, 0x03, 0x00, 0x41, 0x00,
                       0x01, 0x02, 0x01, 0x00, 0x3E, 0x00, 0x02, 0x00,
                       0x00, 0x00, 0x28, 0x00, 0x01, 0x02, 0x00, 0x00,
                       0x02, 0x00, 0x05, 0x03, 0x01, 0x00, 0x01, 0x00,
                       0x03, 0x05, 0x01, 0x00, 0x00, 0x00, 0x01, 0x05,
                       0x03, 0x00, 0x00, 0x00, 0x05, 0x00, 0x01, 0x00,
                       0x36, 0x00, 0x00, 0x02, 0x01, 0xFF, 0xFF, 0xFF,
                       0xFF, 0xFF, 0xFF, 0xFF, 0xFF
                    };
"""
if "case 1493063:" not in csrc:
    csrc = csrc.replace(
        "                case 1419349:\n                    // Fog gold ACG D7418B — capture 20260725-184103.",
        payload_case + "                case 1419349:\n                    // Fog gold ACG D7418B — capture 20260725-184103.",
        1,
    )
cat.write_text(csrc, encoding="utf-8")
print("patched", cat)

# --- MissionInstanceService ResolveInstancePlayfieldId ---
svc = root / "Core/Missions/MissionInstanceService.cs"
ssrc = svc.read_text(encoding="utf-8")
old_resolve = """        /// <summary>
        /// Fog gold 20260725-184103: Playfield2 = 1419349 with ACG D7418B.
        /// </summary>
        internal static int ResolveInstancePlayfieldId(ICharacter character)
        {
            int[] doorShapes = MissionInstanceDynelCapture.ShapePlayfieldIds;
            if (doorShapes == null || doorShapes.Length == 0)
            {
                return InstancePlayfieldId;
            }

            // Exact gold PF id + building; remap / foreign ACG → open grey map.
            const int fogShapePf = 1419349;
            StampShapeSource(fogShapePf, fogShapePf);
            return fogShapePf;
        }"""
new_resolve = """        /// <summary>
        /// Fog gold: Find-Person style uses 1419349/D7418B; RepairMachine uses
        /// capture 20260727-mission-repair-machine-new PF 1493063 / ACG D7425E.
        /// </summary>
        internal static int ResolveInstancePlayfieldId(ICharacter character)
        {
            int[] doorShapes = MissionInstanceDynelCapture.ShapePlayfieldIds;
            if (doorShapes == null || doorShapes.Length == 0)
            {
                return InstancePlayfieldId;
            }

            MissionRollType objective = ResolveCharacterObjective(character);
            if (objective == MissionRollType.RepairMachine)
            {
                const int repairShapePf = 1493063;
                StampShapeSource(repairShapePf, repairShapePf);
                return repairShapePf;
            }

            // Exact gold PF id + building; remap / foreign ACG → open grey map.
            const int fogShapePf = 1419349;
            StampShapeSource(fogShapePf, fogShapePf);
            return fogShapePf;
        }"""
if old_resolve not in ssrc:
    raise SystemExit("ResolveInstancePlayfieldId block missing")
ssrc = ssrc.replace(old_resolve, new_resolve, 1)

old_clear = """                case 1419349:
                    // Gold 184103 PAF spawn (1.8,95) already clear of exit door — no nudge.
                    break;"""
new_clear = """                case 1419349:
                    // Gold 184103 PAF spawn (1.8,95) already clear of exit door — no nudge.
                    break;
                case 1493063:
                    // Gold repair spawn (1.8,265) already clear of exit door — no nudge.
                    break;"""
if "case 1493063:" not in ssrc:
    if old_clear not in ssrc:
        raise SystemExit("ApplySpawnDoorClearance marker missing")
    ssrc = ssrc.replace(old_clear, new_clear, 1)
svc.write_text(ssrc, encoding="utf-8")
print("patched", svc)

# --- DoorReplay ---
door = root / "Core/Missions/MissionInstanceDoorReplay.cs"
dsrc = door.read_text(encoding="utf-8")
if "TheftSecureFoodDispenserTemplateId" not in dsrc:
    dsrc = dsrc.replace(
        "        internal const int BrokenMachineTemplateId = 0x027B47;",
        "        internal const int BrokenMachineTemplateId = 0x027B47;\n\n"
        "        // Capture 20260727-mission-repair-machine-new: Theft Secure Food Dispenser.\n"
        "        internal const int TheftSecureFoodDispenserTemplateId = 100345;",
        1,
    )

# character instance replace for repair capture
old_rep = """                ReplaceInstance(packet, capturedCharacterInstance, character.Identity.Instance);
                ReplaceInstance(
                    packet,
                    MissionInstanceDoorCapture.CapturedCharacterInstance,
                    character.Identity.Instance);
                ReplaceInstance(packet, unchecked((int)0x797E30D7), character.Identity.Instance);"""
new_rep = """                ReplaceInstance(packet, capturedCharacterInstance, character.Identity.Instance);
                ReplaceInstance(
                    packet,
                    MissionInstanceDoorCapture.CapturedCharacterInstance,
                    character.Identity.Instance);
                ReplaceInstance(packet, unchecked((int)0x797E30D7), character.Identity.Instance);
                // Repair capture 20260727-mission-repair-machine-new character instance.
                ReplaceInstance(packet, unchecked((int)0x7996C028), character.Identity.Instance);"""
if "0x7996C028" not in dsrc:
    if old_rep not in dsrc:
        raise SystemExit("ReplaceInstance block missing")
    dsrc = dsrc.replace(old_rep, new_rep, 1)

old_reg = """                if (!registerChests)
                {
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
                }"""
new_reg = """                // Register loot chests when asked; always try machine registration on repair
                // (Food Dispenser is a Terminal packet with registerChests=false).
                Identity container;
                int staticInstance;
                int templateId;
                if (!TryParseContainer(packet, out container, out staticInstance, out templateId))
                {
                    continue;
                }

                bool isRepairMachine = templateId == BrokenMachineTemplateId
                                       || templateId == TheftSecureFoodDispenserTemplateId;
                if (isRepairMachine && repairObjective)
                {
                    MissionMachineTracker.Register(container);
                    machinesRegistered++;
                    continue;
                }

                if (!registerChests)
                {
                    continue;
                }

                string name = NameForChest(staticInstance, templateId);
                if (isRepairMachine)
                {
                    MissionLootPropService.Register(container, name);
                    lootRegistered++;
                }
                else
                {
                    MissionLootPropService.Register(container, name);
                    lootRegistered++;
                }"""
if "TheftSecureFoodDispenserTemplateId" not in dsrc or "isRepairMachine" not in dsrc:
    if old_reg not in dsrc:
        raise SystemExit("registration block missing")
    dsrc = dsrc.replace(old_reg, new_reg, 1)

old_parse = """            bool found = false;
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
            }"""
new_parse = """            bool found = false;
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

                // Repair Machine dispenser is Terminal 0xC73D (capture 20260727...).
                if (packet[i] == 0x00 && packet[i + 1] == 0x00 && packet[i + 2] == 0xC7 && packet[i + 3] == 0x3D)
                {
                    int instance = (packet[i + 4] << 24) | (packet[i + 5] << 16) | (packet[i + 6] << 8)
                                   | packet[i + 7];
                    identity = new Identity { Type = IdentityType.Terminal, Instance = instance };
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }"""
if "IdentityType.Terminal" not in dsrc:
    if old_parse not in dsrc:
        raise SystemExit("TryParseContainer identity loop missing")
    dsrc = dsrc.replace(old_parse, new_parse, 1)

door.write_text(dsrc, encoding="utf-8")
print("patched", door)
print("DONE")
