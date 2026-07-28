namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Security.Cryptography;
    using System.Text;

    #endregion

    internal enum MissionAcgWireCategory
    {
        Unknown = 0,

        Door = 1,

        Chest = 2,

        Terminal = 3
    }

    /// <summary>
    /// Describes a captured four-byte value that a later instance materializer may retarget.
    /// This foundation records the evidence only; it does not perform production retargeting.
    /// </summary>
    internal enum MissionAcgRetargetCategory
    {
        Unknown = 0,

        CharacterInstance = 1,

        Playfield2Instance = 2,

        ParentIdentityType = 3,

        ParentIdentityInstance = 4,

        DynelIdentityType = 5,

        DynelIdentityInstance = 6,

        BuildingIdentityType = 7,

        BuildingIdentityInstance = 8
    }

    internal enum MissionAcgLayoutCompletenessState
    {
        CompleteSelectable = 1,

        StructurallyCompleteObjectiveIncomplete = 2,

        IncompleteNonSelectable = 3,

        ConflictingRejected = 4
    }

    internal sealed class MissionAcgIdentityRecord : IEquatable<MissionAcgIdentityRecord>
    {
        internal MissionAcgIdentityRecord(int type, int instance)
        {
            this.Type = type;
            this.Instance = instance;
        }

        internal int Type { get; private set; }

        internal int Instance { get; private set; }

        public bool Equals(MissionAcgIdentityRecord other)
        {
            return other != null && this.Type == other.Type && this.Instance == other.Instance;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as MissionAcgIdentityRecord);
        }

        public override int GetHashCode()
        {
            return unchecked((this.Type * 397) ^ this.Instance);
        }

        public override string ToString()
        {
            return this.Type.ToString("X8") + ":" + this.Instance.ToString("X8");
        }
    }

    internal sealed class MissionAcgPointRecord
    {
        internal MissionAcgPointRecord(float x, float y, float z)
        {
            RequireFinite(x, "x");
            RequireFinite(y, "y");
            RequireFinite(z, "z");
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        internal float X { get; private set; }

        internal float Y { get; private set; }

        internal float Z { get; private set; }

        private static void RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    internal sealed class MissionAcgRotationRecord
    {
        internal MissionAcgRotationRecord(float x, float y, float z, float w)
        {
            RequireFinite(x, "x");
            RequireFinite(y, "y");
            RequireFinite(z, "z");
            RequireFinite(w, "w");
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.W = w;
        }

        internal float X { get; private set; }

        internal float Y { get; private set; }

        internal float Z { get; private set; }

        internal float W { get; private set; }

        private static void RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    internal sealed class MissionAcgRetargetSlotRecord
    {
        internal MissionAcgRetargetSlotRecord(
            MissionAcgRetargetCategory category,
            int slot,
            int byteOffset,
            int capturedValue)
        {
            if (!Enum.IsDefined(typeof(MissionAcgRetargetCategory), category)
                || category == MissionAcgRetargetCategory.Unknown)
            {
                throw new ArgumentOutOfRangeException("category");
            }

            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException("slot");
            }

            if (byteOffset < 0)
            {
                throw new ArgumentOutOfRangeException("byteOffset");
            }

            this.Category = category;
            this.Slot = slot;
            this.ByteOffset = byteOffset;
            this.CapturedValue = capturedValue;
        }

        internal MissionAcgRetargetCategory Category { get; private set; }

        internal int Slot { get; private set; }

        internal int ByteOffset { get; private set; }

        internal int CapturedValue { get; private set; }
    }

    internal sealed class MissionAcgWireRecord
    {
        private readonly byte[] packetBytes;

        internal MissionAcgWireRecord(
            MissionAcgWireCategory category,
            int slot,
            string packetHex,
            MissionAcgIdentityRecord capturedIdentity,
            int? capturedPlayfield2,
            MissionAcgIdentityRecord capturedParentIdentity,
            IEnumerable<MissionAcgRetargetSlotRecord> retargetSlots)
        {
            if (!Enum.IsDefined(typeof(MissionAcgWireCategory), category)
                || category == MissionAcgWireCategory.Unknown)
            {
                throw new ArgumentOutOfRangeException("category");
            }

            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException("slot");
            }

            this.packetBytes = MissionAcgHash.ParseHex(packetHex, "packetHex");
            this.Category = category;
            this.Slot = slot;
            this.PacketHex = MissionAcgHash.ToHex(this.packetBytes);
            this.PacketSha256 = MissionAcgHash.ComputeSha256(this.packetBytes);
            this.CapturedIdentity = capturedIdentity;
            this.CapturedPlayfield2 = capturedPlayfield2;
            this.CapturedParentIdentity = capturedParentIdentity;
            this.RetargetSlots = CopyRecords(retargetSlots);
        }

        internal MissionAcgWireCategory Category { get; private set; }

        internal int Slot { get; private set; }

        internal string PacketHex { get; private set; }

        internal string PacketSha256 { get; private set; }

        internal MissionAcgIdentityRecord CapturedIdentity { get; private set; }

        internal int? CapturedPlayfield2 { get; private set; }

        internal MissionAcgIdentityRecord CapturedParentIdentity { get; private set; }

        internal ReadOnlyCollection<MissionAcgRetargetSlotRecord> RetargetSlots { get; private set; }

        internal byte[] CopyPacketBytes()
        {
            return (byte[])this.packetBytes.Clone();
        }

        private static ReadOnlyCollection<MissionAcgRetargetSlotRecord> CopyRecords(
            IEnumerable<MissionAcgRetargetSlotRecord> records)
        {
            var copy = new List<MissionAcgRetargetSlotRecord>();
            if (records != null)
            {
                foreach (MissionAcgRetargetSlotRecord record in records)
                {
                    if (record == null)
                    {
                        throw new ArgumentException("Retarget slots cannot contain null.", "records");
                    }

                    copy.Add(record);
                }
            }

            return copy.AsReadOnly();
        }
    }

    internal sealed class MissionAcgProvenanceRecord
    {
        internal MissionAcgProvenanceRecord(string captureId, string source, string notes)
            : this(
                captureId,
                source,
                notes,
                0,
                0,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                string.Empty,
                string.Empty)
        {
        }

        internal MissionAcgProvenanceRecord(
            string captureId,
            string source,
            string notes,
            int csvLine,
            long globalOrdinal,
            int sequence,
            string direction,
            string capturedUtc,
            string messageType,
            string preservationStatus,
            int rawPacketLength,
            string rawPacketSha256,
            string parseStatus)
        {
            if (string.IsNullOrWhiteSpace(captureId))
            {
                throw new ArgumentException("Capture id is required.", "captureId");
            }

            this.CaptureId = captureId.Trim();
            this.Source = (source ?? string.Empty).Trim();
            this.Notes = (notes ?? string.Empty).Trim();
            this.CsvLine = csvLine;
            this.GlobalOrdinal = globalOrdinal;
            this.Sequence = sequence;
            this.Direction = (direction ?? string.Empty).Trim();
            this.CapturedUtc = (capturedUtc ?? string.Empty).Trim();
            this.MessageType = (messageType ?? string.Empty).Trim();
            this.PreservationStatus = (preservationStatus ?? string.Empty).Trim();
            this.RawPacketLength = rawPacketLength;
            this.RawPacketSha256 = (rawPacketSha256 ?? string.Empty).Trim();
            this.ParseStatus = (parseStatus ?? string.Empty).Trim();
        }

        internal string CaptureId { get; private set; }

        internal string Source { get; private set; }

        internal string Notes { get; private set; }

        internal int CsvLine { get; private set; }

        internal long GlobalOrdinal { get; private set; }

        internal int Sequence { get; private set; }

        internal string Direction { get; private set; }

        internal string CapturedUtc { get; private set; }

        internal string MessageType { get; private set; }

        internal string PreservationStatus { get; private set; }

        internal int RawPacketLength { get; private set; }

        internal string RawPacketSha256 { get; private set; }

        internal string ParseStatus { get; private set; }
    }

    internal sealed class MissionAcgDynelRecord
    {
        internal MissionAcgDynelRecord(
            MissionAcgWireCategory category,
            int slot,
            MissionAcgIdentityRecord capturedIdentity,
            int? capturedPlayfield2,
            MissionAcgIdentityRecord capturedParentIdentity,
            MissionAcgPointRecord position,
            MissionAcgRotationRecord heading,
            int templateId,
            string name,
            string rawPacketHex,
            IEnumerable<MissionAcgRetargetSlotRecord> retargetSlots,
            IEnumerable<MissionAcgProvenanceRecord> provenance)
        {
            if (!Enum.IsDefined(typeof(MissionAcgWireCategory), category)
                || category == MissionAcgWireCategory.Unknown)
            {
                throw new ArgumentOutOfRangeException("category");
            }

            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException("slot");
            }

            this.Category = category;
            this.Slot = slot;
            this.CapturedIdentity = capturedIdentity;
            this.CapturedPlayfield2 = capturedPlayfield2;
            this.CapturedParentIdentity = capturedParentIdentity;
            this.Position = position;
            this.Heading = heading;
            this.TemplateId = templateId;
            this.Name = (name ?? string.Empty).Trim();
            this.Provenance = MissionAcgRecordCopies.CopyProvenance(provenance);
            if (!string.IsNullOrWhiteSpace(rawPacketHex))
            {
                this.Wire = new MissionAcgWireRecord(
                    category,
                    slot,
                    rawPacketHex,
                    capturedIdentity,
                    capturedPlayfield2,
                    capturedParentIdentity,
                    retargetSlots);
            }
        }

        internal MissionAcgWireCategory Category { get; private set; }

        internal int Slot { get; private set; }

        internal MissionAcgIdentityRecord CapturedIdentity { get; private set; }

        internal int? CapturedPlayfield2 { get; private set; }

        internal MissionAcgIdentityRecord CapturedParentIdentity { get; private set; }

        internal MissionAcgPointRecord Position { get; private set; }

        internal MissionAcgRotationRecord Heading { get; private set; }

        internal int TemplateId { get; private set; }

        internal string Name { get; private set; }

        internal MissionAcgWireRecord Wire { get; private set; }

        internal ReadOnlyCollection<MissionAcgProvenanceRecord> Provenance { get; private set; }
    }

    internal sealed class MissionAcgNpcSlotRecord
    {
        private readonly byte[] rawPacket;

        internal MissionAcgNpcSlotRecord(
            int slot,
            MissionAcgIdentityRecord capturedIdentity,
            int? capturedPlayfield2,
            MissionAcgIdentityRecord capturedParentIdentity,
            MissionAcgPointRecord position,
            MissionAcgRotationRecord heading,
            int templateId,
            int monsterData,
            string name,
            string role,
            string rawPacketHex,
            IEnumerable<MissionAcgProvenanceRecord> provenance)
            : this(
                slot,
                capturedIdentity,
                capturedPlayfield2,
                capturedParentIdentity,
                position,
                heading,
                templateId,
                monsterData,
                0,
                0,
                0,
                0,
                name,
                role,
                new MissionAcgNpcTextureRecord[0],
                new MissionAcgNpcMeshRecord[0],
                rawPacketHex,
                provenance)
        {
        }

        internal MissionAcgNpcSlotRecord(
            int slot,
            MissionAcgIdentityRecord capturedIdentity,
            int? capturedPlayfield2,
            MissionAcgIdentityRecord capturedParentIdentity,
            MissionAcgPointRecord position,
            MissionAcgRotationRecord heading,
            int templateId,
            int monsterData,
            int capturedLevel,
            int capturedHealth,
            int scale,
            int? headMesh,
            string name,
            string role,
            IEnumerable<MissionAcgNpcTextureRecord> textures,
            IEnumerable<MissionAcgNpcMeshRecord> meshes,
            string rawPacketHex,
            IEnumerable<MissionAcgProvenanceRecord> provenance)
            : this(
                slot,
                capturedIdentity,
                capturedPlayfield2,
                capturedParentIdentity,
                position,
                heading,
                templateId,
                monsterData,
                capturedLevel,
                capturedHealth,
                0,
                scale,
                headMesh,
                name,
                role,
                textures,
                meshes,
                rawPacketHex,
                provenance)
        {
        }

        internal MissionAcgNpcSlotRecord(
            int slot,
            MissionAcgIdentityRecord capturedIdentity,
            int? capturedPlayfield2,
            MissionAcgIdentityRecord capturedParentIdentity,
            MissionAcgPointRecord position,
            MissionAcgRotationRecord heading,
            int templateId,
            int monsterData,
            int capturedLevel,
            int capturedHealth,
            int capturedHealthDamage,
            int scale,
            int? headMesh,
            string name,
            string role,
            IEnumerable<MissionAcgNpcTextureRecord> textures,
            IEnumerable<MissionAcgNpcMeshRecord> meshes,
            string rawPacketHex,
            IEnumerable<MissionAcgProvenanceRecord> provenance)
        {
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException("slot");
            }

            this.Slot = slot;
            this.CapturedIdentity = capturedIdentity;
            this.CapturedPlayfield2 = capturedPlayfield2;
            this.CapturedParentIdentity = capturedParentIdentity;
            this.Position = position;
            this.Heading = heading;
            this.TemplateId = templateId;
            this.MonsterData = monsterData;
            this.CapturedLevel = capturedLevel;
            this.CapturedHealth = capturedHealth;
            this.CapturedHealthDamage = capturedHealthDamage;
            this.Scale = scale;
            this.HeadMesh = headMesh;
            this.Name = (name ?? string.Empty).Trim();
            this.Role = (role ?? string.Empty).Trim();
            this.Textures = MissionAcgRecordCopies.CopyNpcTextures(textures);
            this.Meshes = MissionAcgRecordCopies.CopyNpcMeshes(meshes);
            this.rawPacket = MissionAcgRecordCopies.ParseOptionalHex(rawPacketHex);
            this.RawPacketHex =
                this.rawPacket.Length == 0 ? string.Empty : MissionAcgHash.ToHex(this.rawPacket);
            this.RawPacketSha256 =
                this.rawPacket.Length == 0
                    ? string.Empty
                    : MissionAcgHash.ComputeSha256(this.rawPacket);
            this.Provenance = MissionAcgRecordCopies.CopyProvenance(provenance);
        }

        internal int Slot { get; private set; }

        internal MissionAcgIdentityRecord CapturedIdentity { get; private set; }

        internal int? CapturedPlayfield2 { get; private set; }

        internal MissionAcgIdentityRecord CapturedParentIdentity { get; private set; }

        internal MissionAcgPointRecord Position { get; private set; }

        internal MissionAcgRotationRecord Heading { get; private set; }

        internal int TemplateId { get; private set; }

        internal int MonsterData { get; private set; }

        internal int CapturedLevel { get; private set; }

        internal int CapturedHealth { get; private set; }

        internal int CapturedHealthDamage { get; private set; }

        internal int Scale { get; private set; }

        internal int? HeadMesh { get; private set; }

        internal string Name { get; private set; }

        internal string Role { get; private set; }

        internal string RawPacketHex { get; private set; }

        internal string RawPacketSha256 { get; private set; }

        internal ReadOnlyCollection<MissionAcgNpcTextureRecord> Textures { get; private set; }

        internal ReadOnlyCollection<MissionAcgNpcMeshRecord> Meshes { get; private set; }

        internal ReadOnlyCollection<MissionAcgProvenanceRecord> Provenance { get; private set; }

        internal byte[] CopyRawPacket()
        {
            return (byte[])this.rawPacket.Clone();
        }
    }

    internal sealed class MissionAcgNpcTextureRecord
    {
        internal MissionAcgNpcTextureRecord(int slot, int textureId)
            : this(slot, textureId, 0)
        {
        }

        internal MissionAcgNpcTextureRecord(int slot, int textureId, int unknown)
        {
            this.Slot = slot;
            this.TextureId = textureId;
            this.Unknown = unknown;
        }

        internal int Slot { get; private set; }

        internal int TextureId { get; private set; }

        internal int Unknown { get; private set; }
    }

    internal sealed class MissionAcgNpcMeshRecord
    {
        internal MissionAcgNpcMeshRecord(int position, int meshId, int unknown1, int unknown2)
        {
            this.Position = position;
            this.MeshId = meshId;
            this.Unknown1 = unknown1;
            this.Unknown2 = unknown2;
        }

        internal int Position { get; private set; }

        internal int MeshId { get; private set; }

        internal int Unknown1 { get; private set; }

        internal int Unknown2 { get; private set; }
    }

    internal sealed class MissionAcgObjectiveSlotRecord
    {
        private readonly byte[] rawPacket;

        internal MissionAcgObjectiveSlotRecord(
            int slot,
            IEnumerable<MissionRollType> compatibleMissionTypes,
            MissionAcgIdentityRecord capturedIdentity,
            int? capturedPlayfield2,
            MissionAcgIdentityRecord capturedParentIdentity,
            MissionAcgPointRecord position,
            MissionAcgRotationRecord heading,
            int templateId,
            string name,
            string rawPacketHex,
            IEnumerable<MissionAcgProvenanceRecord> provenance)
        {
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException("slot");
            }

            this.Slot = slot;
            this.CompatibleMissionTypes =
                MissionAcgRecordCopies.CopyMissionTypes(compatibleMissionTypes);
            this.CapturedIdentity = capturedIdentity;
            this.CapturedPlayfield2 = capturedPlayfield2;
            this.CapturedParentIdentity = capturedParentIdentity;
            this.Position = position;
            this.Heading = heading;
            this.TemplateId = templateId;
            this.Name = (name ?? string.Empty).Trim();
            this.rawPacket = MissionAcgRecordCopies.ParseOptionalHex(rawPacketHex);
            this.RawPacketHex =
                this.rawPacket.Length == 0 ? string.Empty : MissionAcgHash.ToHex(this.rawPacket);
            this.RawPacketSha256 =
                this.rawPacket.Length == 0
                    ? string.Empty
                    : MissionAcgHash.ComputeSha256(this.rawPacket);
            this.Provenance = MissionAcgRecordCopies.CopyProvenance(provenance);
        }

        internal int Slot { get; private set; }

        internal ReadOnlyCollection<MissionRollType> CompatibleMissionTypes { get; private set; }

        internal MissionAcgIdentityRecord CapturedIdentity { get; private set; }

        internal int? CapturedPlayfield2 { get; private set; }

        internal MissionAcgIdentityRecord CapturedParentIdentity { get; private set; }

        internal MissionAcgPointRecord Position { get; private set; }

        internal MissionAcgRotationRecord Heading { get; private set; }

        internal int TemplateId { get; private set; }

        internal string Name { get; private set; }

        internal string RawPacketHex { get; private set; }

        internal string RawPacketSha256 { get; private set; }

        internal ReadOnlyCollection<MissionAcgProvenanceRecord> Provenance { get; private set; }

        internal byte[] CopyRawPacket()
        {
            return (byte[])this.rawPacket.Clone();
        }
    }

    internal sealed class MissionAcgExitRecord
    {
        private readonly byte[] rawPacket;

        internal MissionAcgExitRecord(
            MissionAcgIdentityRecord capturedIdentity,
            int? capturedPlayfield2,
            MissionAcgIdentityRecord capturedParentIdentity,
            MissionAcgPointRecord position,
            MissionAcgRotationRecord heading,
            int templateId,
            string name,
            string rawPacketHex,
            IEnumerable<MissionAcgProvenanceRecord> provenance)
        {
            this.CapturedIdentity = capturedIdentity;
            this.CapturedPlayfield2 = capturedPlayfield2;
            this.CapturedParentIdentity = capturedParentIdentity;
            this.Position = position;
            this.Heading = heading;
            this.TemplateId = templateId;
            this.Name = (name ?? string.Empty).Trim();
            this.rawPacket = MissionAcgRecordCopies.ParseOptionalHex(rawPacketHex);
            this.RawPacketHex =
                this.rawPacket.Length == 0 ? string.Empty : MissionAcgHash.ToHex(this.rawPacket);
            this.RawPacketSha256 =
                this.rawPacket.Length == 0
                    ? string.Empty
                    : MissionAcgHash.ComputeSha256(this.rawPacket);
            this.Provenance = MissionAcgRecordCopies.CopyProvenance(provenance);
        }

        internal MissionAcgIdentityRecord CapturedIdentity { get; private set; }

        internal int? CapturedPlayfield2 { get; private set; }

        internal MissionAcgIdentityRecord CapturedParentIdentity { get; private set; }

        internal MissionAcgPointRecord Position { get; private set; }

        internal MissionAcgRotationRecord Heading { get; private set; }

        internal int TemplateId { get; private set; }

        internal string Name { get; private set; }

        internal string RawPacketHex { get; private set; }

        internal string RawPacketSha256 { get; private set; }

        internal ReadOnlyCollection<MissionAcgProvenanceRecord> Provenance { get; private set; }

        internal byte[] CopyRawPacket()
        {
            return (byte[])this.rawPacket.Clone();
        }
    }

    internal sealed class MissionAcgCompatibilityRecord
    {
        internal MissionAcgCompatibilityRecord(
            int minimumMissionQuality,
            int maximumMissionQuality,
            IEnumerable<MissionRollType> missionTypes)
        {
            if (minimumMissionQuality <= 0)
            {
                throw new ArgumentOutOfRangeException("minimumMissionQuality");
            }

            if (maximumMissionQuality < minimumMissionQuality)
            {
                throw new ArgumentOutOfRangeException("maximumMissionQuality");
            }

            ReadOnlyCollection<MissionRollType> copiedMissionTypes =
                MissionAcgRecordCopies.CopyMissionTypes(missionTypes);
            for (int i = 0; i < copiedMissionTypes.Count; i++)
            {
                MissionRollType missionType = copiedMissionTypes[i];
                if (!Enum.IsDefined(typeof(MissionRollType), missionType)
                    || missionType == MissionRollType.Unknown)
                {
                    throw new ArgumentOutOfRangeException("missionTypes");
                }
            }

            this.MinimumMissionQuality = minimumMissionQuality;
            this.MaximumMissionQuality = maximumMissionQuality;
            this.MissionTypes = copiedMissionTypes;
        }

        internal int MinimumMissionQuality { get; private set; }

        internal int MaximumMissionQuality { get; private set; }

        internal ReadOnlyCollection<MissionRollType> MissionTypes { get; private set; }

        internal bool Supports(MissionRollType missionType, int missionQuality)
        {
            if (missionQuality < this.MinimumMissionQuality
                || missionQuality > this.MaximumMissionQuality)
            {
                return false;
            }

            for (int i = 0; i < this.MissionTypes.Count; i++)
            {
                if (this.MissionTypes[i] == missionType)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Keeps raw capture observation totals distinct from the deduplicated runtime slot totals.
    /// Raw SimpleChar observations include the captured player update; normalized NPC slots do not.
    /// </summary>
    internal sealed class MissionAcgCaptureCountsRecord
    {
        internal MissionAcgCaptureCountsRecord(
            int doorObservationCount,
            int chestObservationCount,
            int terminalObservationCount,
            int simpleCharObservationCount,
            int npcObservationCount,
            int objectiveObservationCount,
            int normalizedDoorSlotCount,
            int normalizedChestSlotCount,
            int normalizedTerminalSlotCount,
            int normalizedNpcSlotCount,
            int normalizedObjectiveSlotCount)
        {
            RequireNonNegative(doorObservationCount, "doorObservationCount");
            RequireNonNegative(chestObservationCount, "chestObservationCount");
            RequireNonNegative(terminalObservationCount, "terminalObservationCount");
            RequireNonNegative(simpleCharObservationCount, "simpleCharObservationCount");
            RequireNonNegative(npcObservationCount, "npcObservationCount");
            RequireNonNegative(objectiveObservationCount, "objectiveObservationCount");
            RequireNonNegative(normalizedDoorSlotCount, "normalizedDoorSlotCount");
            RequireNonNegative(normalizedChestSlotCount, "normalizedChestSlotCount");
            RequireNonNegative(normalizedTerminalSlotCount, "normalizedTerminalSlotCount");
            RequireNonNegative(normalizedNpcSlotCount, "normalizedNpcSlotCount");
            RequireNonNegative(normalizedObjectiveSlotCount, "normalizedObjectiveSlotCount");

            if (normalizedDoorSlotCount > doorObservationCount)
            {
                throw new ArgumentOutOfRangeException("normalizedDoorSlotCount");
            }

            if (normalizedChestSlotCount > chestObservationCount)
            {
                throw new ArgumentOutOfRangeException("normalizedChestSlotCount");
            }

            if (normalizedTerminalSlotCount > terminalObservationCount)
            {
                throw new ArgumentOutOfRangeException("normalizedTerminalSlotCount");
            }

            if (normalizedNpcSlotCount > npcObservationCount)
            {
                throw new ArgumentOutOfRangeException("normalizedNpcSlotCount");
            }

            if (normalizedObjectiveSlotCount > objectiveObservationCount)
            {
                throw new ArgumentOutOfRangeException("normalizedObjectiveSlotCount");
            }

            this.DoorObservationCount = doorObservationCount;
            this.ChestObservationCount = chestObservationCount;
            this.TerminalObservationCount = terminalObservationCount;
            this.SimpleCharObservationCount = simpleCharObservationCount;
            this.NpcObservationCount = npcObservationCount;
            this.ObjectiveObservationCount = objectiveObservationCount;
            this.NormalizedDoorSlotCount = normalizedDoorSlotCount;
            this.NormalizedChestSlotCount = normalizedChestSlotCount;
            this.NormalizedTerminalSlotCount = normalizedTerminalSlotCount;
            this.NormalizedNpcSlotCount = normalizedNpcSlotCount;
            this.NormalizedObjectiveSlotCount = normalizedObjectiveSlotCount;
        }

        internal int DoorObservationCount { get; private set; }

        internal int ChestObservationCount { get; private set; }

        internal int TerminalObservationCount { get; private set; }

        internal int SimpleCharObservationCount { get; private set; }

        internal int NpcObservationCount { get; private set; }

        internal int ObjectiveObservationCount { get; private set; }

        internal int NormalizedDoorSlotCount { get; private set; }

        internal int NormalizedChestSlotCount { get; private set; }

        internal int NormalizedTerminalSlotCount { get; private set; }

        internal int NormalizedNpcSlotCount { get; private set; }

        internal int NormalizedObjectiveSlotCount { get; private set; }

        private static void RequireNonNegative(int value, string parameterName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    internal sealed class MissionAcgCompletenessRecord
    {
        internal MissionAcgCompletenessRecord(
            MissionAcgLayoutCompletenessState state,
            bool hasGeneratorPayload,
            bool hasBuildingIdentity,
            bool hasEntryPoint,
            bool hasExit,
            bool hasDoorWire,
            bool hasChestWire,
            bool hasNpcSlots,
            bool hasObjectiveSlots,
            bool hasLifecycleCorrelation)
        {
            if (!Enum.IsDefined(typeof(MissionAcgLayoutCompletenessState), state))
            {
                throw new ArgumentOutOfRangeException("state");
            }

            this.State = state;
            this.HasGeneratorPayload = hasGeneratorPayload;
            this.HasBuildingIdentity = hasBuildingIdentity;
            this.HasEntryPoint = hasEntryPoint;
            this.HasExit = hasExit;
            this.HasDoorWire = hasDoorWire;
            this.HasChestWire = hasChestWire;
            this.HasNpcSlots = hasNpcSlots;
            this.HasObjectiveSlots = hasObjectiveSlots;
            this.HasLifecycleCorrelation = hasLifecycleCorrelation;
        }

        internal MissionAcgLayoutCompletenessState State { get; private set; }

        internal bool HasGeneratorPayload { get; private set; }

        internal bool HasBuildingIdentity { get; private set; }

        internal bool HasEntryPoint { get; private set; }

        internal bool HasExit { get; private set; }

        internal bool HasDoorWire { get; private set; }

        internal bool HasChestWire { get; private set; }

        internal bool HasNpcSlots { get; private set; }

        internal bool HasObjectiveSlots { get; private set; }

        internal bool HasLifecycleCorrelation { get; private set; }

        internal bool IsSelectionComplete
        {
            get
            {
                return this.State == MissionAcgLayoutCompletenessState.CompleteSelectable
                       && this.HasGeneratorPayload
                       && this.HasBuildingIdentity
                       && this.HasEntryPoint
                       && this.HasExit
                       && this.HasDoorWire
                       && this.HasChestWire
                       && this.HasNpcSlots
                       && this.HasObjectiveSlots
                       && this.HasLifecycleCorrelation;
            }
        }
    }

    internal sealed class MissionAcgLayoutExclusion
    {
        internal MissionAcgLayoutExclusion(int sourcePlayfield2, string reason)
            : this(string.Empty, sourcePlayfield2, reason)
        {
        }

        internal MissionAcgLayoutExclusion(string layoutId, int sourcePlayfield2, string reason)
        {
            if (sourcePlayfield2 <= 0)
            {
                throw new ArgumentOutOfRangeException("sourcePlayfield2");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Exclusion reason is required.", "reason");
            }

            this.LayoutId = (layoutId ?? string.Empty).Trim();
            this.SourcePlayfield2 = sourcePlayfield2;
            this.Reason = reason.Trim();
        }

        internal string LayoutId { get; private set; }

        internal int SourcePlayfield2 { get; private set; }

        internal string Reason { get; private set; }
    }

    internal static class MissionAcgHash
    {
        internal static string ComputeSha256(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }

            using (SHA256 sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(bytes));
            }
        }

        internal static byte[] ParseHex(string hex, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                throw new ArgumentException("Hex payload is required.", parameterName);
            }

            string normalized = hex.Trim();
            if ((normalized.Length & 1) != 0)
            {
                throw new ArgumentException("Hex payload length must be even.", parameterName);
            }

            var bytes = new byte[normalized.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                int high = HexNibble(normalized[i * 2]);
                int low = HexNibble(normalized[(i * 2) + 1]);
                if (high < 0 || low < 0)
                {
                    throw new ArgumentException("Hex payload contains a non-hex character.", parameterName);
                }

                bytes[i] = (byte)((high << 4) | low);
            }

            return bytes;
        }

        internal static string ToHex(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }

            const string Digits = "0123456789ABCDEF";
            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(Digits[bytes[i] >> 4]);
                builder.Append(Digits[bytes[i] & 0x0F]);
            }

            return builder.ToString();
        }

        internal static int ReadInt32BigEndian(byte[] bytes, int offset)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }

            if (offset < 0 || offset + 4 > bytes.Length)
            {
                throw new ArgumentOutOfRangeException("offset");
            }

            return (bytes[offset] << 24)
                   | (bytes[offset + 1] << 16)
                   | (bytes[offset + 2] << 8)
                   | bytes[offset + 3];
        }

        private static int HexNibble(char value)
        {
            if (value >= '0' && value <= '9')
            {
                return value - '0';
            }

            if (value >= 'A' && value <= 'F')
            {
                return value - 'A' + 10;
            }

            if (value >= 'a' && value <= 'f')
            {
                return value - 'a' + 10;
            }

            return -1;
        }
    }

    internal static class MissionAcgRecordCopies
    {
        internal static ReadOnlyCollection<MissionAcgProvenanceRecord> CopyProvenance(
            IEnumerable<MissionAcgProvenanceRecord> records)
        {
            var copy = new List<MissionAcgProvenanceRecord>();
            if (records != null)
            {
                foreach (MissionAcgProvenanceRecord record in records)
                {
                    if (record == null)
                    {
                        throw new ArgumentException("Provenance cannot contain null.", "records");
                    }

                    copy.Add(record);
                }
            }

            return copy.AsReadOnly();
        }

        internal static ReadOnlyCollection<MissionRollType> CopyMissionTypes(
            IEnumerable<MissionRollType> missionTypes)
        {
            var copy = new List<MissionRollType>();
            if (missionTypes != null)
            {
                foreach (MissionRollType missionType in missionTypes)
                {
                    copy.Add(missionType);
                }
            }

            return copy.AsReadOnly();
        }

        internal static byte[] ParseOptionalHex(string rawPacketHex)
        {
            return string.IsNullOrWhiteSpace(rawPacketHex)
                       ? new byte[0]
                       : MissionAcgHash.ParseHex(rawPacketHex, "rawPacketHex");
        }

        internal static ReadOnlyCollection<MissionAcgNpcTextureRecord> CopyNpcTextures(
            IEnumerable<MissionAcgNpcTextureRecord> records)
        {
            var copy = new List<MissionAcgNpcTextureRecord>();
            if (records != null)
            {
                foreach (MissionAcgNpcTextureRecord record in records)
                {
                    if (record == null)
                    {
                        throw new ArgumentException("NPC textures cannot contain null.", "records");
                    }

                    copy.Add(record);
                }
            }

            return copy.AsReadOnly();
        }

        internal static ReadOnlyCollection<MissionAcgNpcMeshRecord> CopyNpcMeshes(
            IEnumerable<MissionAcgNpcMeshRecord> records)
        {
            var copy = new List<MissionAcgNpcMeshRecord>();
            if (records != null)
            {
                foreach (MissionAcgNpcMeshRecord record in records)
                {
                    if (record == null)
                    {
                        throw new ArgumentException("NPC meshes cannot contain null.", "records");
                    }

                    copy.Add(record);
                }
            }

            return copy.AsReadOnly();
        }
    }
}
