using System;
using System.Globalization;
using System.Text;

namespace ZoneEngine.Core.Playfields;

internal sealed class SubwayVisibilitySpatialInterestMetrics
{
	internal int TotalPlayfieldCharacters { get; private set; }

	internal int TotalPlayfieldNpcs { get; private set; }

	internal int SpatialQueryInspectedCandidates { get; private set; }

	internal int WithinEnterRadiusCount { get; private set; }

	internal int AlreadyVisibleCount { get; private set; }

	internal int NewlyVisibleCount { get; private set; }

	internal int LeavingVisibleCount { get; private set; }

	internal int FilteredOutCount { get; private set; }

	private SubwayVisibilitySpatialInterestMetrics(int totalPlayfieldCharacters, int totalPlayfieldNpcs, int spatialQueryInspectedCandidates, int withinEnterRadiusCount, int alreadyVisibleCount, int newlyVisibleCount, int leavingVisibleCount, int filteredOutCount)
	{
		TotalPlayfieldCharacters = totalPlayfieldCharacters;
		TotalPlayfieldNpcs = totalPlayfieldNpcs;
		SpatialQueryInspectedCandidates = spatialQueryInspectedCandidates;
		WithinEnterRadiusCount = withinEnterRadiusCount;
		AlreadyVisibleCount = alreadyVisibleCount;
		NewlyVisibleCount = newlyVisibleCount;
		LeavingVisibleCount = leavingVisibleCount;
		FilteredOutCount = filteredOutCount;
	}

	internal void AppendJsonFields(StringBuilder builder, bool first)
	{
		if (builder == null)
		{
			throw new ArgumentNullException("builder");
		}
		AppendJsonNumber(builder, "total_playfield_characters", TotalPlayfieldCharacters, first);
		AppendJsonNumber(builder, "total_playfield_npcs", TotalPlayfieldNpcs, first: false);
		AppendJsonNumber(builder, "spatial_query_inspected_candidates", SpatialQueryInspectedCandidates, first: false);
		AppendJsonNumber(builder, "within_enter_radius_count", WithinEnterRadiusCount, first: false);
		AppendJsonNumber(builder, "already_visible_count", AlreadyVisibleCount, first: false);
		AppendJsonNumber(builder, "newly_visible_count", NewlyVisibleCount, first: false);
		AppendJsonNumber(builder, "leaving_visible_count", LeavingVisibleCount, first: false);
		AppendJsonNumber(builder, "filtered_out_count", FilteredOutCount, first: false);
	}

	internal static SubwayVisibilitySpatialInterestMetrics ForInitialSnapshot(int totalPlayfieldCharacters, int totalPlayfieldNpcs, int visibilityEligibleCharacters, int spatialQueryInspectedCandidates, int withinEnterRadiusCount)
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
		return new SubwayVisibilitySpatialInterestMetrics(totalPlayfieldCharacters, totalPlayfieldNpcs, spatialQueryInspectedCandidates, withinEnterRadiusCount, 0, withinEnterRadiusCount, 0, visibilityEligibleCharacters - withinEnterRadiusCount);
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
		builder.Append('"').Append(name).Append("\":")
			.Append(value.ToString(CultureInfo.InvariantCulture));
	}
}
