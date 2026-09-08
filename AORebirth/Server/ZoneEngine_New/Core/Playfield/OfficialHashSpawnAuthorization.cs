namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Linq;
    using AORebirth.Core.GameData;
    using AORebirth.Core.Playfields.OfficialPlacements;

    /// <summary>
    /// Extraction proves a placement, not a NewEngine behavior/profile identity.
    /// Both the accepted placement authority and an explicit New template bridge
    /// are required. No hash reversal, name matching, proximity, or fallback mob.
    /// </summary>
    internal static class OfficialHashSpawnAuthorization
    {
        private static readonly Lazy<OfficialPlayfieldPlacementCatalog> Catalog = new(() =>
            new OfficialPlayfieldPlacementCatalog(
                OfficialPlayfieldPlacementCatalog.ResolveRuntimeCorpusRoot(AppContext.BaseDirectory)));

        internal static bool TryAuthorize(int playfieldId, int ordinal, PlayfieldSpawnEntry entry, out string hash)
        {
            hash = string.Empty;
            if (!Catalog.Value.TryGetPlayfield(playfieldId, out var shard, out _)) return false;
            var matches = shard.Records.Where(record => record.DistrictIndex == entry.DistrictIndex
                && record.DistrictRecordOrdinal == ordinal).Take(2).ToArray();
            if (matches.Length != 1 || !Matches(matches[0], playfieldId, ordinal, entry)) return false;
            hash = matches[0].ResolvedMobTemplateHash;
            return true;
        }

        internal static bool Matches(OfficialPlayfieldPlacement record, int playfieldId,
            int ordinal, PlayfieldSpawnEntry entry)
        {
            return record.RuntimeActivationAuthorized == true && record.IdentityResolved == true
                && record.BehaviorReady == true && !string.IsNullOrWhiteSpace(record.ExistingAoRebirthProfile)
                && !string.IsNullOrWhiteSpace(record.ResolvedMobTemplateHash)
                && !string.IsNullOrWhiteSpace(record.MobTemplateEvidenceSource)
                && record.PlayfieldId == playfieldId && record.DistrictIndex == entry.DistrictIndex
                && record.DistrictRecordOrdinal == ordinal
                && record.OfficialAcgHashNativeUInt32 == entry.Hash
                && string.Equals(record.CanonicalAcgHashText, entry.HashText, StringComparison.Ordinal)
                && entry.Position is { Length: 3 } && record.PositionX.HasValue && record.PositionY.HasValue
                && record.PositionZ.HasValue && (float)record.PositionX.Value == entry.Position[0]
                && (float)record.PositionY.Value == entry.Position[1] && (float)record.PositionZ.Value == entry.Position[2]
                && record.LevelMinimum == entry.MinLevel && record.LevelMaximum == entry.MaxLevel
                && record.RespawnChance == entry.RespawnChance && record.RespawnTime == entry.RespawnTime
                && record.RotationMidEncoded == entry.Angle && record.RotationWidthEncoded == entry.AngleW
                && record.Radius.HasValue && (float)record.Radius.Value == entry.Radius
                // These unbridged alternative-site/event structures must not gain authority implicitly.
                && (entry.AdditionalPoints == null || entry.AdditionalPoints.Length == 0) && entry.Extensions == null;
        }
    }
}
