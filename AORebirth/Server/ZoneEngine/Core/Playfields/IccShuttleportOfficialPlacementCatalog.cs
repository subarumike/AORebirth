namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    #endregion

    /// <summary>
    /// One official EP1 PF4582 HashSpawnPoint_t record. This evidence record has no
    /// runtime activation field and is not consumed by IccShuttleportSpawn.
    /// </summary>
    internal sealed class IccShuttleportOfficialPlacementRecord
    {
        internal IccShuttleportOfficialPlacementRecord(
            string officialRecordIdentity,
            int? sourceNpcId,
            int officialRecordIndex,
            int districtIndex,
            string districtName,
            int recordOrdinal,
            int recordRelativeOffset,
            int databaseOffset,
            string canonicalAcgHashText,
            string officialWireBytes,
            uint officialNativeUInt32,
            float positionX,
            float positionY,
            float positionZ,
            float radius,
            int rotationMidEncoded,
            int rotationWidthEncoded,
            int minLevel,
            int maxLevel,
            int respawnChance,
            float respawnTime,
            int assistanceRadius,
            int nativeFlags,
            int moreFlags,
            int serializedOptionalFlags,
            int unknownOptionalU8,
            int serializedSize)
        {
            this.OfficialRecordIdentity = officialRecordIdentity;
            this.SourceNpcId = sourceNpcId;
            this.OfficialRecordIndex = officialRecordIndex;
            this.DistrictIndex = districtIndex;
            this.DistrictName = districtName;
            this.RecordOrdinal = recordOrdinal;
            this.RecordRelativeOffset = recordRelativeOffset;
            this.DatabaseOffset = databaseOffset;
            this.CanonicalAcgHashText = canonicalAcgHashText;
            this.OfficialWireBytes = officialWireBytes;
            this.OfficialNativeUInt32 = officialNativeUInt32;
            this.PositionX = positionX;
            this.PositionY = positionY;
            this.PositionZ = positionZ;
            this.Radius = radius;
            this.RotationMidEncoded = rotationMidEncoded;
            this.RotationWidthEncoded = rotationWidthEncoded;
            this.MinLevel = minLevel;
            this.MaxLevel = maxLevel;
            this.RespawnChance = respawnChance;
            this.RespawnTime = respawnTime;
            this.AssistanceRadius = assistanceRadius;
            this.NativeFlags = nativeFlags;
            this.MoreFlags = moreFlags;
            this.SerializedOptionalFlags = serializedOptionalFlags;
            this.UnknownOptionalU8 = unknownOptionalU8;
            this.SerializedSize = serializedSize;
        }

        internal string OfficialRecordIdentity { get; private set; }
        internal int? SourceNpcId { get; private set; }
        internal int OfficialRecordIndex { get; private set; }
        internal int DistrictIndex { get; private set; }
        internal string DistrictName { get; private set; }
        internal int RecordOrdinal { get; private set; }
        internal int RecordRelativeOffset { get; private set; }
        internal int DatabaseOffset { get; private set; }
        internal string CanonicalAcgHashText { get; private set; }
        internal string OfficialWireBytes { get; private set; }
        internal uint OfficialNativeUInt32 { get; private set; }
        internal float PositionX { get; private set; }
        internal float PositionY { get; private set; }
        internal float PositionZ { get; private set; }
        internal float Radius { get; private set; }
        internal int RotationMidEncoded { get; private set; }
        internal int RotationWidthEncoded { get; private set; }
        internal int MinLevel { get; private set; }
        internal int MaxLevel { get; private set; }
        internal int RespawnChance { get; private set; }
        internal float RespawnTime { get; private set; }
        internal int AssistanceRadius { get; private set; }
        internal int NativeFlags { get; private set; }
        internal int MoreFlags { get; private set; }
        internal int SerializedOptionalFlags { get; private set; }
        internal int UnknownOptionalU8 { get; private set; }
        internal int SerializedSize { get; private set; }
    }

    internal static partial class IccShuttleportOfficialPlacementCatalog
    {
        private static readonly IccShuttleportOfficialPlacementRecord[] Records =
            CreateRecords();

        private static readonly Dictionary<string, IccShuttleportOfficialPlacementRecord>
            RecordsByIdentity = BuildIdentityIndex();

        private static readonly Dictionary<int, IccShuttleportOfficialPlacementRecord>
            RecordsBySourceNpcId = BuildSourceNpcIndex();

        static IccShuttleportOfficialPlacementCatalog()
        {
            ValidateOrThrow();
        }

        internal static int Count
        {
            get { return Records.Length; }
        }

        internal static bool TryGetByOfficialIdentity(
            string officialRecordIdentity,
            out IccShuttleportOfficialPlacementRecord record)
        {
            return RecordsByIdentity.TryGetValue(officialRecordIdentity, out record);
        }

        internal static bool TryGetBySourceNpcId(
            int sourceNpcId,
            out IccShuttleportOfficialPlacementRecord record)
        {
            return RecordsBySourceNpcId.TryGetValue(sourceNpcId, out record);
        }

        private static Dictionary<string, IccShuttleportOfficialPlacementRecord> BuildIdentityIndex()
        {
            return new Dictionary<string, IccShuttleportOfficialPlacementRecord>(
                Records.ToDictionary(record => record.OfficialRecordIdentity),
                StringComparer.Ordinal);
        }

        private static Dictionary<int, IccShuttleportOfficialPlacementRecord> BuildSourceNpcIndex()
        {
            return Records
                .Where(record => record.SourceNpcId.HasValue)
                .ToDictionary(record => record.SourceNpcId.Value);
        }

        private static void ValidateOrThrow()
        {
            if (Records.Length != OfficialRecordCount)
            {
                throw new InvalidOperationException("PF4582 official overlay record count drifted");
            }

            var identities = new HashSet<string>();
            var sourceNpcIds = new HashSet<int>();
            int linked = 0;
            int unlinked = 0;
            foreach (IccShuttleportOfficialPlacementRecord record in Records)
            {
                if (!identities.Add(record.OfficialRecordIdentity))
                {
                    throw new InvalidOperationException(
                        "Duplicate PF4582 official record identity " + record.OfficialRecordIdentity);
                }

                if (record.SourceNpcId.HasValue)
                {
                    if (!sourceNpcIds.Add(record.SourceNpcId.Value))
                    {
                        throw new InvalidOperationException(
                            "Duplicate PF4582 official SourceNpcId " + record.SourceNpcId.Value);
                    }

                    linked++;
                }
                else
                {
                    if (record.CanonicalAcgHashText != "NCNN")
                    {
                        throw new InvalidOperationException(
                            "Only official NCNN may remain without SourceNpcId");
                    }

                    unlinked++;
                }
            }

            if (linked != ReconciledSourceNpcIdCount || unlinked != WithoutSourceNpcIdCount)
            {
                throw new InvalidOperationException("PF4582 official overlay linkage count drifted");
            }
        }
    }
}
