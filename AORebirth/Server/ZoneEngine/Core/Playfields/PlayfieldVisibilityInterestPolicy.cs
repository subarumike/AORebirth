namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Globalization;

    internal sealed class PlayfieldVisibilityInterestPolicy
    {
        internal const string EnterRadiusEnvironmentVariable = "AO_REBIRTH_VISIBILITY_ENTER_RADIUS";
        internal const string LeaveRadiusEnvironmentVariable = "AO_REBIRTH_VISIBILITY_LEAVE_RADIUS";
        internal const string CellSizeEnvironmentVariable = "AO_REBIRTH_VISIBILITY_CELL_SIZE";

        internal const float MinimumEnterRadius = 16.0f;
        internal const float MaximumEnterRadius = 4096.0f;
        internal const float MaximumLeaveRadius = 4096.0f;
        internal const float MinimumCellSize = 8.0f;
        internal const float MaximumCellSize = 128.0f;

        private const float DefaultEnterRadius = 80.0f;
        private const float DefaultLeaveRadius = 100.0f;
        private const float DefaultCellSize = 32.0f;

        internal static readonly PlayfieldVisibilityInterestPolicy Default =
            Create(DefaultEnterRadius, DefaultLeaveRadius, DefaultCellSize);

        private PlayfieldVisibilityInterestPolicy(float enterRadius, float leaveRadius, float cellSize)
        {
            this.EnterRadius = enterRadius;
            this.LeaveRadius = leaveRadius;
            this.CellSize = cellSize;
        }

        internal float EnterRadius { get; private set; }

        internal float LeaveRadius { get; private set; }

        internal float CellSize { get; private set; }

        internal static PlayfieldVisibilityInterestPolicy Create(
            float enterRadius,
            float leaveRadius,
            float cellSize)
        {
            ValidateFiniteRange(
                enterRadius,
                MinimumEnterRadius,
                MaximumEnterRadius,
                "enterRadius");
            ValidateFiniteRange(
                leaveRadius,
                MinimumEnterRadius,
                MaximumLeaveRadius,
                "leaveRadius");
            if (leaveRadius <= enterRadius)
            {
                throw new ArgumentOutOfRangeException(
                    "leaveRadius",
                    leaveRadius,
                    "Visibility leave radius must be greater than the enter radius.");
            }
            ValidateFiniteRange(
                cellSize,
                MinimumCellSize,
                MaximumCellSize,
                "cellSize");

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

            float enterRadius = ReadOverride(
                readSetting,
                EnterRadiusEnvironmentVariable,
                Default.EnterRadius);
            float leaveRadius = ReadOverride(
                readSetting,
                LeaveRadiusEnvironmentVariable,
                Default.LeaveRadius);
            float cellSize = ReadOverride(
                readSetting,
                CellSizeEnvironmentVariable,
                Default.CellSize);
            return Create(enterRadius, leaveRadius, cellSize);
        }

        private static float ReadOverride(
            Func<string, string> readSetting,
            string name,
            float defaultValue)
        {
            string text = readSetting(name);
            if (string.IsNullOrWhiteSpace(text))
            {
                return defaultValue;
            }

            float value;
            if (!float.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value)
                || float.IsNaN(value)
                || float.IsInfinity(value))
            {
                throw new InvalidOperationException(
                    "Visibility policy setting is not a finite invariant number: " + name);
            }

            return value;
        }

        private static void ValidateFiniteRange(float value, float minimum, float maximum, string name)
        {
            if (float.IsNaN(value)
                || float.IsInfinity(value)
                || value < minimum
                || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    value,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Visibility policy value must be within {0}..{1}.",
                        minimum,
                        maximum));
            }
        }
    }
}
