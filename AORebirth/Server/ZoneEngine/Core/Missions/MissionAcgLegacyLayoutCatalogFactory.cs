namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Read-only migration adapter over the eight coherent legacy layout/door/chest captures.
    /// They remain audit-visible but non-selectable until lifecycle-correlated objective and exit
    /// evidence is promoted by generated catalog data.
    /// </summary>
    internal static class MissionAcgLegacyLayoutCatalogFactory
    {
        private const int CapturedBrokenMachineTemplateId = 0x027B47;

        private static readonly int[] LegacySourcePlayfield2s =
        {
            1441800,
            1443840,
            1460226,
            1456133,
            1419310,
            1419335,
            1419382,
            1419349
        };

        private static readonly MissionRollType[] AllMissionTypes =
        {
            MissionRollType.KillPerson,
            MissionRollType.FindPerson,
            MissionRollType.FindItem,
            MissionRollType.FindItemReturn,
            MissionRollType.RepairMachine
        };

        internal static MissionAcgLayoutCatalog Create()
        {
            return Create(MissionAcgCapturedLayoutCatalog.CreateBundles());
        }

        internal static MissionAcgLayoutCatalog Create(
            IEnumerable<MissionAcgLayoutBundle> generatedBundles)
        {
            var layouts = new List<MissionAcgLayoutBundle>(CreateLegacyBundles());
            if (generatedBundles != null)
            {
                foreach (MissionAcgLayoutBundle generatedBundle in generatedBundles)
                {
                    layouts.Add(generatedBundle);
                }
            }

            return MissionAcgLayoutCatalogLoader.Load(layouts, CreateLegacyExclusions());
        }

        internal static ReadOnlyCollection<MissionAcgLayoutBundle> CreateLegacyBundles()
        {
            var layouts = new List<MissionAcgLayoutBundle>();
            for (int i = 0; i < LegacySourcePlayfield2s.Length; i++)
            {
                layouts.Add(CreateLegacyBundle(LegacySourcePlayfield2s[i]));
            }

            return layouts.AsReadOnly();
        }

        internal static ReadOnlyCollection<MissionAcgLayoutExclusion> CreateLegacyExclusions()
        {
            return new List<MissionAcgLayoutExclusion>
                   {
                       new MissionAcgLayoutExclusion(
                           "legacy-rk-acg-1441804",
                           MissionAcgLayoutCatalogLoader.ExplicitlyIncompleteShapePlayfield2,
                           "PF2 1441804 has NPC shape evidence only; no correlated generator, door, "
                           + "chest, objective, exit, QFU, and teleport lifecycle bundle.")
                   }.AsReadOnly();
        }

        private static MissionAcgLayoutBundle CreateLegacyBundle(int sourcePlayfield2)
        {
            MissionShape shape = MissionInstanceShapeCatalog.PickShape(sourcePlayfield2, null);
            if (shape == null || shape.CapturedPlayfieldId != sourcePlayfield2)
            {
                throw new InvalidOperationException(
                    "Legacy ACG shape is missing for PF2 " + sourcePlayfield2 + ".");
            }

            byte[] generatorPayload = MissionInstanceShapeCatalog.GetGeneratorPayload(sourcePlayfield2);
            if (generatorPayload == null || generatorPayload.Length < 8)
            {
                throw new InvalidOperationException(
                    "Legacy ACG generator payload is missing for PF2 " + sourcePlayfield2 + ".");
            }

            var provenance = new[]
                             {
                                 ProvenanceFor(sourcePlayfield2)
                             };
            var dynels = new List<MissionAcgDynelRecord>();
            AddDynels(
                dynels,
                MissionAcgWireCategory.Door,
                MissionInstanceDynelCapture.GetDoors(sourcePlayfield2),
                sourcePlayfield2,
                provenance);
            AddDynels(
                dynels,
                MissionAcgWireCategory.Chest,
                MissionInstanceDynelCapture.GetChests(sourcePlayfield2),
                sourcePlayfield2,
                provenance);
            AddDynels(
                dynels,
                MissionAcgWireCategory.Terminal,
                MissionInstanceDynelCapture.GetTerminals(sourcePlayfield2),
                sourcePlayfield2,
                provenance);

            List<MissionAcgNpcSlotRecord> npcSlots =
                CreateNpcSlots(shape, sourcePlayfield2, provenance);
            bool hasDoors = HasCategory(dynels, MissionAcgWireCategory.Door);
            bool hasChests = HasCategory(dynels, MissionAcgWireCategory.Chest);
            var completeness = new MissionAcgCompletenessRecord(
                MissionAcgLayoutCompletenessState.StructurallyCompleteObjectiveIncomplete,
                true,
                true,
                true,
                false,
                hasDoors,
                hasChests,
                npcSlots.Count > 0,
                false,
                false);

            return new MissionAcgLayoutBundle(
                MissionAcgLayoutBundle.CurrentFormatVersion,
                "legacy-rk-acg-" + sourcePlayfield2,
                sourcePlayfield2,
                new MissionAcgIdentityRecord(
                    MissionAcgHash.ReadInt32BigEndian(generatorPayload, 0),
                    MissionAcgHash.ReadInt32BigEndian(generatorPayload, 4)),
                generatorPayload,
                ExpectedGeneratorPayloadSha256(sourcePlayfield2),
                new MissionAcgPointRecord(shape.SpawnX, shape.SpawnY, shape.SpawnZ),
                null,
                dynels,
                npcSlots,
                new MissionAcgObjectiveSlotRecord[0],
                null,
                new MissionAcgIdentityRecord(
                    0x0000C350,
                    MissionInstanceDynelCapture.CapturedCharacterInstance),
                new MissionAcgCompatibilityRecord(1, 250, AllMissionTypes),
                provenance,
                completeness,
                false,
                "Legacy capture lacks lifecycle-correlated exit/QFU/teleport/objective provenance.");
        }

        private static void AddDynels(
            ICollection<MissionAcgDynelRecord> destination,
            MissionAcgWireCategory category,
            string[] packets,
            int sourcePlayfield2,
            IEnumerable<MissionAcgProvenanceRecord> provenance)
        {
            if (packets == null)
            {
                return;
            }

            int expectedType = IdentityTypeFor(category);
            for (int i = 0; i < packets.Length; i++)
            {
                string packetHex = packets[i];
                byte[] packet = MissionAcgHash.ParseHex(packetHex, "packetHex");
                bool validEnvelope =
                    HasValidEnvelope(packet, category, expectedType, sourcePlayfield2);
                MissionAcgIdentityRecord capturedIdentity =
                    validEnvelope ? ReadIdentity(packet, 20) : null;
                MissionAcgIdentityRecord parentIdentity =
                    validEnvelope ? ReadOptionalIdentity(packet, 33) : null;
                var retargetSlots = new List<MissionAcgRetargetSlotRecord>();
                if (validEnvelope)
                {
                    if (MissionAcgHash.ReadInt32BigEndian(packet, 12)
                        == MissionInstanceDynelCapture.CapturedCharacterInstance)
                    {
                        AddRetargetSlot(
                            retargetSlots,
                            packet,
                            MissionAcgRetargetCategory.CharacterInstance,
                            0,
                            12,
                            MissionInstanceDynelCapture.CapturedCharacterInstance);
                    }
                    AddRetargetSlot(
                        retargetSlots,
                        packet,
                        MissionAcgRetargetCategory.DynelIdentityType,
                        0,
                        20,
                        capturedIdentity.Type);
                    AddRetargetSlot(
                        retargetSlots,
                        packet,
                        MissionAcgRetargetCategory.DynelIdentityInstance,
                        0,
                        24,
                        capturedIdentity.Instance);
                    if (parentIdentity != null)
                    {
                        AddRetargetSlot(
                            retargetSlots,
                            packet,
                            MissionAcgRetargetCategory.ParentIdentityType,
                            0,
                            33,
                            parentIdentity.Type);
                        AddRetargetSlot(
                            retargetSlots,
                            packet,
                            MissionAcgRetargetCategory.ParentIdentityInstance,
                            0,
                            37,
                            parentIdentity.Instance);
                    }

                    AddRetargetSlot(
                        retargetSlots,
                        packet,
                        MissionAcgRetargetCategory.Playfield2Instance,
                        0,
                        69,
                        sourcePlayfield2);
                }

                int templateId = validEnvelope ? TryReadTemplateId(packet) : 0;
                destination.Add(
                    new MissionAcgDynelRecord(
                        category,
                        i,
                        capturedIdentity,
                        validEnvelope ? (int?)sourcePlayfield2 : null,
                        parentIdentity,
                        validEnvelope ? TryReadPosition(packet) : null,
                        validEnvelope ? TryReadHeading(packet) : null,
                        templateId,
                        NameFor(category, templateId),
                        packetHex,
                        retargetSlots,
                        provenance));
            }
        }

        private static List<MissionAcgNpcSlotRecord> CreateNpcSlots(
            MissionShape shape,
            int sourcePlayfield2,
            IEnumerable<MissionAcgProvenanceRecord> provenance)
        {
            var slots = new List<MissionAcgNpcSlotRecord>();
            if (shape.Npcs == null)
            {
                return slots;
            }

            for (int i = 0; i < shape.Npcs.Length; i++)
            {
                MissionNpc npc = shape.Npcs[i];
                if (npc == null)
                {
                    continue;
                }

                slots.Add(
                    new MissionAcgNpcSlotRecord(
                        i,
                        null,
                        sourcePlayfield2,
                        null,
                        new MissionAcgPointRecord(npc.X, npc.Y, npc.Z),
                        new MissionAcgRotationRecord(npc.Hx, npc.Hy, npc.Hz, npc.Hw),
                        0,
                        npc.MonsterData,
                        npc.Level,
                        npc.Health,
                        npc.Scale,
                        npc.HeadMesh,
                        npc.Name,
                        npc.Role.ToString(),
                        ConvertTextures(npc.Textures),
                        ConvertMeshes(npc.Meshes),
                        string.Empty,
                        AppendNpcRawUnavailableProvenance(provenance)));
            }

            return slots;
        }

        private static IEnumerable<MissionAcgProvenanceRecord> AppendNpcRawUnavailableProvenance(
            IEnumerable<MissionAcgProvenanceRecord> provenance)
        {
            var result = new List<MissionAcgProvenanceRecord>(provenance);
            result.Add(
                new MissionAcgProvenanceRecord(
                    "legacy-shape-catalog",
                    "MissionInstanceShapeCatalog",
                    "Structured NPC fields are preserved; the raw captured SCFU is unavailable."));
            return result;
        }

        private static IEnumerable<MissionAcgNpcTextureRecord> ConvertTextures(int[][] textures)
        {
            var result = new List<MissionAcgNpcTextureRecord>();
            if (textures != null)
            {
                for (int i = 0; i < textures.Length; i++)
                {
                    int[] texture = textures[i];
                    if (texture != null && texture.Length >= 2)
                    {
                        result.Add(new MissionAcgNpcTextureRecord(texture[0], texture[1]));
                    }
                }
            }

            return result;
        }

        private static IEnumerable<MissionAcgNpcMeshRecord> ConvertMeshes(int[][] meshes)
        {
            var result = new List<MissionAcgNpcMeshRecord>();
            if (meshes != null)
            {
                for (int i = 0; i < meshes.Length; i++)
                {
                    int[] mesh = meshes[i];
                    if (mesh != null && mesh.Length >= 2)
                    {
                        result.Add(
                            new MissionAcgNpcMeshRecord(
                                mesh[0],
                                mesh[1],
                                mesh.Length > 2 ? mesh[2] : 0,
                                mesh.Length > 3 ? mesh[3] : 0));
                    }
                }
            }

            return result;
        }

        private static MissionAcgProvenanceRecord ProvenanceFor(int sourcePlayfield2)
        {
            switch (sourcePlayfield2)
            {
                case 1419310:
                case 1419335:
                case 1419382:
                    return new MissionAcgProvenanceRecord(
                        "20260719-5-different-shape-fo-mish",
                        "MissionInstanceShapeCatalog/MissionInstanceDynelCapture",
                        "Legacy layout, NPC, door, and chest evidence.");
                case 1419349:
                    return new MissionAcgProvenanceRecord(
                        "20260724-mission-find-person;20260725-184103",
                        "MissionInstanceShapeCatalog/MissionInstanceDynelCapture",
                        "Legacy find-person/fog-gold layout evidence.");
                case 1441800:
                    return new MissionAcgProvenanceRecord(
                        "20260725-151009;20260725-080425",
                        "MissionInstanceShapeCatalog/MissionInstanceDynelCapture",
                        "Legacy fog-gold layout evidence.");
                case 1443840:
                    return new MissionAcgProvenanceRecord(
                        "20260725-002423",
                        "MissionInstanceShapeCatalog/MissionInstanceDynelCapture",
                        "Legacy low-QL find-person layout evidence.");
                case 1460226:
                case 1456133:
                    return new MissionAcgProvenanceRecord(
                        "20260724-224228",
                        "MissionInstanceShapeCatalog/MissionInstanceDynelCapture",
                        "Legacy find-person gold layout evidence.");
                default:
                    throw new InvalidOperationException(
                        "No legacy ACG provenance for PF2 " + sourcePlayfield2 + ".");
            }
        }

        private static string ExpectedGeneratorPayloadSha256(int sourcePlayfield2)
        {
            switch (sourcePlayfield2)
            {
                case 1419310:
                    return "98c751f02761462822529c22db9c9283ee35eff94c234c204fc4a3090ec46b61";
                case 1419335:
                    return "3474cfb1fa440db6dd000a39266e1bd2ae3da8d232a64d77162494ceb791987a";
                case 1419349:
                    return "90b7b1c3d0ad91458dbd216b7a0e8e44c1170ff9c1c820a0d331d3b3294441d1";
                case 1419382:
                    return "d582a232f6e679a4577c1d78817179309efe14dba5509b9c7ec0821c753b8e88";
                case 1441800:
                    return "bb1496de86189c8b7f243fb6d62c6ce2ba8e239b6221cfc99b731026b7ce34fa";
                case 1443840:
                    return "380365994ada8254562eb218c13528a6f823c1522df900dbaf1bea5fbc6d1a0e";
                case 1456133:
                    return "d203af58be52a5f48fccac9aa89a6c4c74759c40e1fe52df30f98f63c2639d18";
                case 1460226:
                    return "c6dfd691a47ec463e6f3afad76913dc275baea057b0dfdc8b857088ceac8c665";
                default:
                    throw new InvalidOperationException(
                        "No expected legacy generator hash for PF2 "
                        + sourcePlayfield2
                        + ".");
            }
        }

        private static bool HasValidEnvelope(
            byte[] packet,
            MissionAcgWireCategory category,
            int expectedIdentityType,
            int sourcePlayfield2)
        {
            return packet != null
                   && packet.Length >= 87
                   && ((packet[6] << 8) | packet[7]) == packet.Length
                   && MissionAcgHash.ReadInt32BigEndian(packet, 16)
                   == N3TypeFor(category)
                   && MissionAcgHash.ReadInt32BigEndian(packet, 20)
                   == expectedIdentityType
                   && MissionAcgHash.ReadInt32BigEndian(packet, 69)
                   == sourcePlayfield2;
        }

        private static MissionAcgIdentityRecord ReadIdentity(byte[] packet, int offset)
        {
            return new MissionAcgIdentityRecord(
                MissionAcgHash.ReadInt32BigEndian(packet, offset),
                MissionAcgHash.ReadInt32BigEndian(packet, offset + 4));
        }

        private static MissionAcgIdentityRecord ReadOptionalIdentity(byte[] packet, int offset)
        {
            MissionAcgIdentityRecord identity = ReadIdentity(packet, offset);
            return identity.Type == 0 && identity.Instance == 0 ? null : identity;
        }

        private static MissionAcgPointRecord TryReadPosition(byte[] packet)
        {
            return new MissionAcgPointRecord(
                ReadFloatBigEndian(packet, 41),
                ReadFloatBigEndian(packet, 45),
                ReadFloatBigEndian(packet, 49));
        }

        private static MissionAcgRotationRecord TryReadHeading(byte[] packet)
        {
            return new MissionAcgRotationRecord(
                ReadFloatBigEndian(packet, 53),
                ReadFloatBigEndian(packet, 57),
                ReadFloatBigEndian(packet, 61),
                ReadFloatBigEndian(packet, 65));
        }

        private static float ReadFloatBigEndian(byte[] packet, int offset)
        {
            int bits = MissionAcgHash.ReadInt32BigEndian(packet, offset);
            return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
        }

        private static int TryReadTemplateId(byte[] packet)
        {
            const int AcgItemTemplateIdStat = 0x000002BE;
            const int EncodedArrayUnit = 0x000003F1;
            const int StatArrayOffset = 83;
            if (packet.Length < StatArrayOffset + 4)
            {
                return 0;
            }

            int encodedCount =
                MissionAcgHash.ReadInt32BigEndian(packet, StatArrayOffset);
            if (encodedCount < EncodedArrayUnit
                || encodedCount % EncodedArrayUnit != 0)
            {
                return 0;
            }

            int count = (encodedCount / EncodedArrayUnit) - 1;
            int offset = StatArrayOffset + 4;
            if (count < 0 || count > 4096 || offset + (count * 8) > packet.Length)
            {
                return 0;
            }

            for (int i = 0; i < count; i++, offset += 8)
            {
                if (MissionAcgHash.ReadInt32BigEndian(packet, offset)
                    == AcgItemTemplateIdStat)
                {
                    return MissionAcgHash.ReadInt32BigEndian(packet, offset + 4);
                }
            }

            return 0;
        }

        private static void AddRetargetSlot(
            ICollection<MissionAcgRetargetSlotRecord> destination,
            byte[] packet,
            MissionAcgRetargetCategory category,
            int slot,
            int byteOffset,
            int capturedValue)
        {
            if (byteOffset < 0
                || byteOffset + 4 > packet.Length
                || MissionAcgHash.ReadInt32BigEndian(packet, byteOffset) != capturedValue)
            {
                throw new InvalidOperationException(
                    "Legacy ACG retarget offset does not match its decoded envelope value.");
            }

            destination.Add(
                new MissionAcgRetargetSlotRecord(
                    category,
                    slot,
                    byteOffset,
                    capturedValue));
        }

        private static bool HasCategory(
            IEnumerable<MissionAcgDynelRecord> dynels,
            MissionAcgWireCategory category)
        {
            foreach (MissionAcgDynelRecord dynel in dynels)
            {
                if (dynel.Category == category)
                {
                    return true;
                }
            }

            return false;
        }

        private static int N3TypeFor(MissionAcgWireCategory category)
        {
            switch (category)
            {
                case MissionAcgWireCategory.Door:
                    return unchecked((int)0x365A5071);
                case MissionAcgWireCategory.Chest:
                    return unchecked((int)0x465A5D73);
                case MissionAcgWireCategory.Terminal:
                    return unchecked((int)0x3B11256F);
                default:
                    throw new ArgumentOutOfRangeException("category");
            }
        }

        private static int IdentityTypeFor(MissionAcgWireCategory category)
        {
            switch (category)
            {
                case MissionAcgWireCategory.Door:
                    return (int)IdentityType.Door;
                case MissionAcgWireCategory.Chest:
                    return (int)IdentityType.Container;
                case MissionAcgWireCategory.Terminal:
                    return (int)IdentityType.Terminal;
                default:
                    throw new ArgumentOutOfRangeException("category");
            }
        }

        private static string NameFor(MissionAcgWireCategory category, int templateId)
        {
            if (category == MissionAcgWireCategory.Door)
            {
                return "Door";
            }

            if (category == MissionAcgWireCategory.Terminal)
            {
                return "Terminal";
            }

            return templateId == CapturedBrokenMachineTemplateId
                       ? "Broken Machine"
                       : "Container";
        }
    }
}
