namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Globalization;
    using System.Text;

    internal sealed class SubwayVisibilitySpatialInterestMetrics
    {
        private SubwayVisibilitySpatialInterestMetrics(
            int totalPlayfieldCharacters,
            int totalPlayfieldNpcs,
            int spatialQueryInspectedCandidates,
            int withinEnterRadiusCount,
            int alreadyVisibleCount,
            int newlyVisibleCount,
            int leavingVisibleCount,
            int filteredOutCount)
        {
            this.TotalPlayfieldCharacters = totalPlayfieldCharacters;
            this.TotalPlayfieldNpcs = totalPlayfieldNpcs;
            this.SpatialQueryInspectedCandidates = spatialQueryInspectedCandidates;
            this.WithinEnterRadiusCount = withinEnterRadiusCount;
            this.AlreadyVisibleCount = alreadyVisibleCount;
            this.NewlyVisibleCount = newlyVisibleCount;
            this.LeavingVisibleCount = leavingVisibleCount;
            this.FilteredOutCount = filteredOutCount;
        }

        internal int TotalPlayfieldCharacters { get; private set; }
        internal int TotalPlayfieldNpcs { get; private set; }
        internal int SpatialQueryInspectedCandidates { get; private set; }
        internal int WithinEnterRadiusCount { get; private set; }
        internal int AlreadyVisibleCount { get; private set; }
        internal int NewlyVisibleCount { get; private set; }
        internal int LeavingVisibleCount { get; private set; }
        internal int FilteredOutCount { get; private set; }

        internal void AppendJsonFields(StringBuilder builder, bool first)
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }

            AppendJsonNumber(builder, "total_playfield_characters", this.TotalPlayfieldCharacters, first);
            AppendJsonNumber(builder, "total_playfield_npcs", this.TotalPlayfieldNpcs, false);
            AppendJsonNumber(
                builder,
                "spatial_query_inspected_candidates",
                this.SpatialQueryInspectedCandidates,
                false);
            AppendJsonNumber(builder, "within_enter_radius_count", this.WithinEnterRadiusCount, false);
            AppendJsonNumber(builder, "already_visible_count", this.AlreadyVisibleCount, false);
            AppendJsonNumber(builder, "newly_visible_count", this.NewlyVisibleCount, false);
            AppendJsonNumber(builder, "leaving_visible_count", this.LeavingVisibleCount, false);
            AppendJsonNumber(builder, "filtered_out_count", this.FilteredOutCount, false);
        }

        internal static SubwayVisibilitySpatialInterestMetrics ForInitialSnapshot(
            int totalPlayfieldCharacters,
            int totalPlayfieldNpcs,
            int visibilityEligibleCharacters,
            int spatialQueryInspectedCandidates,
            int withinEnterRadiusCount)
        {
            RequireNonnegative(totalPlayfieldCharacters, "totalPlayfieldCharacters");
            RequireNonnegative(totalPlayfieldNpcs, "totalPlayfieldNpcs");
            RequireNonnegative(visibilityEligibleCharacters, "visibilityEligibleCharacters");
            RequireNonnegative(spatialQueryInspectedCandidates, "spatialQueryInspectedCandidates");
            RequireNonnegative(withinEnterRadiusCount, "withinEnterRadiusCount");

            if (totalPlayfieldNpcs > totalPlayfieldCharacters)
            {
                throw new ArgumentOutOfRangeException("totalPlayfieldNpcs");
            }

            if (visibilityEligibleCharacters > totalPlayfieldCharacters)
            {
                throw new ArgumentOutOfRangeException("visibilityEligibleCharacters");
            }

            if (withinEnterRadiusCount > visibilityEligibleCharacters)
            {
                throw new ArgumentOutOfRangeException("withinEnterRadiusCount");
            }

            if (withinEnterRadiusCount > spatialQueryInspectedCandidates)
            {
                throw new ArgumentOutOfRangeException("spatialQueryInspectedCandidates");
            }

            return new SubwayVisibilitySpatialInterestMetrics(
                totalPlayfieldCharacters,
                totalPlayfieldNpcs,
                spatialQueryInspectedCandidates,
                withinEnterRadiusCount,
                0,
                withinEnterRadiusCount,
                0,
                visibilityEligibleCharacters - withinEnterRadiusCount);
        }

        private static void RequireNonnegative(int value, string name)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static void AppendJsonNumber(StringBuilder builder, string name, int value, bool first)
        {
            if (!first)
            {
                builder.Append(',');
            }

            builder.Append('"')
                .Append(name)
                .Append("\":")
                .Append(value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
