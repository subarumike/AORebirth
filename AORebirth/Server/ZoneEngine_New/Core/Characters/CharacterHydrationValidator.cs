namespace ZoneEngine_New.Core.Characters
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Fail-closed boundary between persistent character rows and player publication.
    /// The manifest is the smallest state consumed unconditionally by the retail player SCFU,
    /// FullCharacter construction, appearance, or vital-stat rebase paths.
    /// </summary>
    public static class CharacterHydrationValidator
    {
        public static readonly IReadOnlyList<CharacterStat> RequiredSpawnStats =
            new ReadOnlyCollection<CharacterStat>(
            [
                CharacterStat.Flags,
                CharacterStat.Breed,
                CharacterStat.Sex,
                CharacterStat.Profession,
                CharacterStat.Fatness,
                CharacterStat.Race,
                CharacterStat.HeadMesh,
                CharacterStat.VisualFlags,
                CharacterStat.Scale,
                CharacterStat.Level,
                CharacterStat.TitleLevel,
                CharacterStat.Side,
                CharacterStat.Expansion,
                CharacterStat.Strength,
                CharacterStat.Agility,
                CharacterStat.Stamina,
                CharacterStat.Intelligence,
                CharacterStat.Sense,
                CharacterStat.Psychic,
                CharacterStat.BodyDevelopment,
                CharacterStat.NanoPool,
                CharacterStat.Health,
                CharacterStat.MaxHealth,
                CharacterStat.CurrentNano,
                CharacterStat.MaxNanoEnergy,
                CharacterStat.RunSpeed,
            ]);

        public static CharacterHydrationValidationResult Validate(CharacterHydrationResult hydration)
        {
            ArgumentNullException.ThrowIfNull(hydration);

            var errors = new List<string>();
            CharacterRecord? character = hydration.Character;
            if (character == null)
            {
                errors.Add("character-row-missing");
                return new CharacterHydrationValidationResult(errors);
            }

            if (character.Id <= 0) errors.Add("identity-invalid");
            if (string.IsNullOrWhiteSpace(character.Name)) errors.Add("name-missing");
            if (character.Playfield <= 0) errors.Add("playfield-invalid");
            if (!Finite(character.X) || !Finite(character.Y) || !Finite(character.Z)) errors.Add("position-invalid");
            if (!Finite(character.HeadingW) || !Finite(character.HeadingX)
                || !Finite(character.HeadingY) || !Finite(character.HeadingZ)) errors.Add("heading-invalid");

            var stats = new Dictionary<CharacterStat, int>();
            foreach (StatRecord row in hydration.Stats)
            {
                CharacterStat stat = (CharacterStat)row.StatId;
                if (!stats.TryAdd(stat, row.StatValue))
                    errors.Add("duplicate-stat:" + row.StatId);
                if (StatCollection.IsUnset(row.StatValue))
                    errors.Add("unset-sentinel:" + row.StatId);
            }

            foreach (CharacterStat stat in RequiredSpawnStats)
            {
                if (!stats.ContainsKey(stat))
                    errors.Add("missing-stat:" + (int)stat + ":" + stat);
            }

            // Persistent maxima are base values (Legacy nano is only the breed
            // base), while current vitals include derived/equipment/nano effects.
            // Their relative bounds are enforced by PlayerSpawnPayloadValidator
            // after rebase and active-nano hydration, before player publication.

            if (errors.Any(error => error.StartsWith("missing-stat:", StringComparison.Ordinal)
                || error.StartsWith("unset-sentinel:", StringComparison.Ordinal)))
                return new CharacterHydrationValidationResult(errors);

            int flags = stats[CharacterStat.Flags];
            if (((CharacterFlags)flags).HasFlag(CharacterFlags.Tower)) errors.Add("player-flags-tower");
            Range(stats, CharacterStat.Breed, 1, 4, errors);
            Range(stats, CharacterStat.Sex, 0, 3, errors);
            Range(stats, CharacterStat.Profession, 1, (int)Profession.Shade, errors);
            Range(stats, CharacterStat.Fatness, 0, 2, errors);
            Positive(stats, CharacterStat.Race, errors);
            Positive(stats, CharacterStat.HeadMesh, errors);
            Range(stats, CharacterStat.VisualFlags, 0, short.MaxValue, errors);
            Range(stats, CharacterStat.Scale, 1, short.MaxValue, errors);
            Range(stats, CharacterStat.Level, 1, 220, errors);
            Range(stats, CharacterStat.TitleLevel, 1, 7, errors);
            Range(stats, CharacterStat.Side, 0, 2, errors);
            Range(stats, CharacterStat.Expansion, 0, short.MaxValue, errors);
            Positive(stats, CharacterStat.Strength, errors);
            Positive(stats, CharacterStat.Agility, errors);
            Positive(stats, CharacterStat.Stamina, errors);
            Positive(stats, CharacterStat.Intelligence, errors);
            Positive(stats, CharacterStat.Sense, errors);
            Positive(stats, CharacterStat.Psychic, errors);
            Positive(stats, CharacterStat.BodyDevelopment, errors);
            Positive(stats, CharacterStat.NanoPool, errors);
            Positive(stats, CharacterStat.MaxHealth, errors);
            NonNegative(stats, CharacterStat.Health, errors);
            Positive(stats, CharacterStat.MaxNanoEnergy, errors);
            NonNegative(stats, CharacterStat.CurrentNano, errors);
            NonNegative(stats, CharacterStat.RunSpeed, errors);

            return new CharacterHydrationValidationResult(errors);
        }

        public static void RequireValid(CharacterHydrationResult hydration)
        {
            CharacterHydrationValidationResult result = Validate(hydration);
            if (!result.IsValid)
                throw new InvalidOperationException("Character spawn aggregate rejected: " + string.Join(",", result.Errors));
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void Positive(Dictionary<CharacterStat, int> stats, CharacterStat stat, List<string> errors)
        {
            if (stats[stat] <= 0) errors.Add("stat-not-positive:" + (int)stat + ":" + stat);
        }

        private static void NonNegative(Dictionary<CharacterStat, int> stats, CharacterStat stat, List<string> errors)
        {
            if (stats[stat] < 0) errors.Add("stat-negative:" + (int)stat + ":" + stat);
        }

        private static void Range(Dictionary<CharacterStat, int> stats, CharacterStat stat, int minimum, int maximum, List<string> errors)
        {
            int value = stats[stat];
            if (value < minimum || value > maximum)
                errors.Add("stat-out-of-range:" + (int)stat + ":" + stat);
        }
    }

    public sealed class CharacterHydrationValidationResult
    {
        internal CharacterHydrationValidationResult(IReadOnlyList<string> errors)
            => Errors = errors;

        public IReadOnlyList<string> Errors { get; }

        public bool IsValid => Errors.Count == 0;
    }
}
