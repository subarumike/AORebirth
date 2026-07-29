using System;
using System.Globalization;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldVisibilityInterestPolicy
{
	internal const string EnterRadiusEnvironmentVariable = "AO_REBIRTH_VISIBILITY_ENTER_RADIUS";

	internal const string LeaveRadiusEnvironmentVariable = "AO_REBIRTH_VISIBILITY_LEAVE_RADIUS";

	internal const string CellSizeEnvironmentVariable = "AO_REBIRTH_VISIBILITY_CELL_SIZE";

	internal const float MinimumEnterRadius = 16f;

	internal const float MaximumEnterRadius = 256f;

	internal const float MaximumLeaveRadius = 384f;

	internal const float MinimumCellSize = 8f;

	internal const float MaximumCellSize = 128f;

	private const float DefaultEnterRadius = 80f;

	private const float DefaultLeaveRadius = 100f;

	private const float DefaultCellSize = 32f;

	internal static readonly PlayfieldVisibilityInterestPolicy Default = Create(80f, 100f, 32f);

	internal float EnterRadius { get; private set; }

	internal float LeaveRadius { get; private set; }

	internal float CellSize { get; private set; }

	private PlayfieldVisibilityInterestPolicy(float enterRadius, float leaveRadius, float cellSize)
	{
		EnterRadius = enterRadius;
		LeaveRadius = leaveRadius;
		CellSize = cellSize;
	}

	internal static PlayfieldVisibilityInterestPolicy Create(float enterRadius, float leaveRadius, float cellSize)
	{
		ValidateFiniteRange(enterRadius, 16f, 256f, "enterRadius");
		ValidateFiniteRange(leaveRadius, 16f, 384f, "leaveRadius");
		if (leaveRadius <= enterRadius)
		{
			throw new ArgumentOutOfRangeException("leaveRadius", leaveRadius, "Visibility leave radius must be greater than the enter radius.");
		}
		ValidateFiniteRange(cellSize, 8f, 128f, "cellSize");
		return new PlayfieldVisibilityInterestPolicy(enterRadius, leaveRadius, cellSize);
	}

	internal static PlayfieldVisibilityInterestPolicy FromEnvironment()
	{
		return FromSettings(Environment.GetEnvironmentVariable);
	}

	internal static PlayfieldVisibilityInterestPolicy FromSettings(Func<string, string> readSetting)
	{
		if (readSetting == null)
		{
			throw new ArgumentNullException("readSetting");
		}
		float enterRadius = ReadOverride(readSetting, "AO_REBIRTH_VISIBILITY_ENTER_RADIUS", Default.EnterRadius);
		float leaveRadius = ReadOverride(readSetting, "AO_REBIRTH_VISIBILITY_LEAVE_RADIUS", Default.LeaveRadius);
		float cellSize = ReadOverride(readSetting, "AO_REBIRTH_VISIBILITY_CELL_SIZE", Default.CellSize);
		return Create(enterRadius, leaveRadius, cellSize);
	}

	private static float ReadOverride(Func<string, string> readSetting, string name, float defaultValue)
	{
		string text = readSetting(name);
		if (string.IsNullOrWhiteSpace(text))
		{
			return defaultValue;
		}
		if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || float.IsNaN(result) || float.IsInfinity(result))
		{
			throw new InvalidOperationException("Visibility policy setting is not a finite invariant number: " + name);
		}
		return result;
	}

	private static void ValidateFiniteRange(float value, float minimum, float maximum, string name)
	{
		if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
		{
			throw new ArgumentOutOfRangeException(name, value, string.Format(CultureInfo.InvariantCulture, "Visibility policy value must be within {0}..{1}.", minimum, maximum));
		}
	}
}
