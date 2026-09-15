namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;

    #endregion

    internal sealed class MissionAcgSelectionInput
    {
        internal MissionAcgSelectionInput(
            int deterministicSeed,
            MissionRollType missionType,
            int missionQuality,
            MissionAcgIdentityRecord ownerOrTeamIdentity)
        {
            if (!Enum.IsDefined(typeof(MissionRollType), missionType)
                || missionType == MissionRollType.Unknown)
            {
                throw new ArgumentOutOfRangeException("missionType");
            }

            if (missionQuality <= 0)
            {
                throw new ArgumentOutOfRangeException("missionQuality");
            }

            if (ownerOrTeamIdentity == null
                || ownerOrTeamIdentity.Type == 0
                || ownerOrTeamIdentity.Instance == 0)
            {
                throw new ArgumentException(
                    "A concrete owner or team identity is required.",
                    "ownerOrTeamIdentity");
            }

            this.DeterministicSeed = deterministicSeed;
            this.MissionType = missionType;
            this.MissionQuality = missionQuality;
            this.OwnerOrTeamIdentity = ownerOrTeamIdentity;
        }

        internal int DeterministicSeed { get; private set; }

        internal MissionRollType MissionType { get; private set; }

        internal int MissionQuality { get; private set; }

        internal MissionAcgIdentityRecord OwnerOrTeamIdentity { get; private set; }
    }

    internal static class MissionAcgLayoutSelector
    {
        internal static MissionAcgLayoutBundle Select(
            MissionAcgLayoutCatalog catalog,
            MissionAcgSelectionInput input)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException("catalog");
            }

            return Select(catalog.Layouts, input);
        }

        internal static MissionAcgLayoutBundle Select(
            IEnumerable<MissionAcgLayoutBundle> pool,
            MissionAcgSelectionInput input)
        {
            if (pool == null)
            {
                throw new ArgumentNullException("pool");
            }

            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            var snapshot = new List<MissionAcgLayoutBundle>(pool);
            if (snapshot.Count == 0)
            {
                throw new InvalidOperationException("Mission ACG layout selection pool is empty.");
            }

            MissionAcgCatalogValidationResult validation =
                MissionAcgLayoutCatalogLoader.Validate(
                    snapshot,
                    new MissionAcgLayoutExclusion[0]);
            if (!validation.IsValid)
            {
                MissionAcgCatalogValidationIssue first = validation.Issues[0];
                throw new InvalidOperationException(
                    "Mission ACG layout selection pool is invalid: "
                    + first.Code
                    + " "
                    + first.Message);
            }

            var candidates = new List<MissionAcgLayoutBundle>();
            for (int i = 0; i < snapshot.Count; i++)
            {
                MissionAcgLayoutBundle layout = snapshot[i];
                if (layout.IsSelectable
                    && layout.Completeness.IsSelectionComplete
                    && layout.SupportsMission(input.MissionType, input.MissionQuality))
                {
                    candidates.Add(layout);
                }
            }

            candidates.Sort(
                delegate(MissionAcgLayoutBundle left, MissionAcgLayoutBundle right)
                {
                    return string.Compare(left.LayoutId, right.LayoutId, StringComparison.Ordinal);
                });
            if (candidates.Count == 0)
            {
                throw new InvalidOperationException(
                    "Mission ACG layout selection pool has no complete selectable bundle for "
                    + input.MissionType
                    + " at QL "
                    + input.MissionQuality
                    + ".");
            }

            ulong unsignedIndex = DeriveUnsignedIndex(input, candidates);
            return candidates[(int)(unsignedIndex % (ulong)candidates.Count)];
        }

        private static ulong DeriveUnsignedIndex(
            MissionAcgSelectionInput input,
            IEnumerable<MissionAcgLayoutBundle> candidates)
        {
            var canonical = new StringBuilder();
            canonical.Append(input.DeterministicSeed.ToString(CultureInfo.InvariantCulture));
            canonical.Append('|');
            canonical.Append(((int)input.MissionType).ToString(CultureInfo.InvariantCulture));
            canonical.Append('|');
            canonical.Append(input.MissionQuality.ToString(CultureInfo.InvariantCulture));
            canonical.Append('|');
            canonical.Append(input.OwnerOrTeamIdentity.Type.ToString(CultureInfo.InvariantCulture));
            canonical.Append(':');
            canonical.Append(input.OwnerOrTeamIdentity.Instance.ToString(CultureInfo.InvariantCulture));
            foreach (MissionAcgLayoutBundle candidate in candidates)
            {
                canonical.Append('|');
                canonical.Append(candidate.LayoutId);
                canonical.Append(':');
                canonical.Append(candidate.GeneratorPayloadSha256);
            }

            byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(bytes);
            }

            return ((ulong)digest[0] << 56)
                   | ((ulong)digest[1] << 48)
                   | ((ulong)digest[2] << 40)
                   | ((ulong)digest[3] << 32)
                   | ((ulong)digest[4] << 24)
                   | ((ulong)digest[5] << 16)
                   | ((ulong)digest[6] << 8)
                   | digest[7];
        }
    }
}
