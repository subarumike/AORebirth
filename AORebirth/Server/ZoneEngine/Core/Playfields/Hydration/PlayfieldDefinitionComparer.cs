namespace ZoneEngine.Core.Playfields.Hydration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class PlayfieldDefinitionComparer
    {
        private readonly IPlayfieldDefinitionValidator validator;

        internal PlayfieldDefinitionComparer(IPlayfieldDefinitionValidator validator)
        {
            this.validator = validator ?? throw new ArgumentNullException("validator");
        }

        internal IList<PlayfieldDefinitionDifference> Compare(
            HydratedPlayfieldDefinition expected,
            HydratedPlayfieldDefinition actual)
        {
            var differences = new List<PlayfieldDefinitionDifference>();
            if (expected == null || actual == null)
            {
                differences.Add(
                    new PlayfieldDefinitionDifference(
                        PlayfieldDefinitionDifferenceKind.UnresolvedComparison,
                        "definition",
                        "Both definitions are required."));
                return differences;
            }

            AddValidationDifferences(expected, "expected", differences);
            AddValidationDifferences(actual, "actual", differences);

            IDictionary<string, HydratedPlayfieldRecord> expectedRecords = UniqueRecords(expected.Records);
            IDictionary<string, HydratedPlayfieldRecord> actualRecords = UniqueRecords(actual.Records);
            foreach (string key in expectedRecords.Keys.Except(actualRecords.Keys).OrderBy(value => value, StringComparer.Ordinal))
            {
                differences.Add(new PlayfieldDefinitionDifference(PlayfieldDefinitionDifferenceKind.MissingRecord, key, string.Empty));
            }

            foreach (string key in actualRecords.Keys.Except(expectedRecords.Keys).OrderBy(value => value, StringComparer.Ordinal))
            {
                differences.Add(new PlayfieldDefinitionDifference(PlayfieldDefinitionDifferenceKind.UnexpectedRecord, key, string.Empty));
            }

            foreach (string key in expectedRecords.Keys.Intersect(actualRecords.Keys).OrderBy(value => value, StringComparer.Ordinal))
            {
                CompareRecord(expectedRecords[key], actualRecords[key], differences);
            }

            if (!ProvenanceEqual(expected.Provenance, actual.Provenance))
            {
                differences.Add(
                    new PlayfieldDefinitionDifference(
                        PlayfieldDefinitionDifferenceKind.ChangedSourceProvenance,
                        "definition",
                        string.Empty));
            }

            if (HasUnresolved(expected) || HasUnresolved(actual))
            {
                differences.Add(
                    new PlayfieldDefinitionDifference(
                        PlayfieldDefinitionDifferenceKind.UnresolvedComparison,
                        "provenance",
                        "One or more source identities remain unresolved."));
            }

            if (differences.Count == 0 && HasOrderingDifference(expected, actual))
            {
                differences.Add(
                    new PlayfieldDefinitionDifference(
                        PlayfieldDefinitionDifferenceKind.OrderingOnlyDifference,
                        "definition",
                        "Canonical content matches; only input ordering differs."));
            }

            return differences;
        }

        private void AddValidationDifferences(
            HydratedPlayfieldDefinition definition,
            string side,
            ICollection<PlayfieldDefinitionDifference> differences)
        {
            foreach (PlayfieldHydrationDiagnostic diagnostic in this.validator.Validate(definition)
                .Where(value => value.Code == "DUPLICATE_RECORD" || value.Code == "DUPLICATE_VALUE"))
            {
                differences.Add(
                    new PlayfieldDefinitionDifference(
                        PlayfieldDefinitionDifferenceKind.DuplicateIdentity,
                        side,
                        diagnostic.Message));
            }
        }

        private static void CompareRecord(
            HydratedPlayfieldRecord expected,
            HydratedPlayfieldRecord actual,
            ICollection<PlayfieldDefinitionDifference> differences)
        {
            string key = RecordKey(expected);
            IDictionary<string, HydratedPlayfieldValue> expectedValues = UniqueValues(expected.Values);
            IDictionary<string, HydratedPlayfieldValue> actualValues = UniqueValues(actual.Values);
            foreach (string name in expectedValues.Keys.Union(actualValues.Keys).OrderBy(value => value, StringComparer.Ordinal))
            {
                HydratedPlayfieldValue expectedValue;
                HydratedPlayfieldValue actualValue;
                if (!expectedValues.TryGetValue(name, out expectedValue) || !actualValues.TryGetValue(name, out actualValue))
                {
                    differences.Add(
                        new PlayfieldDefinitionDifference(
                            expectedValue != null && expectedValue.IsCollection
                                ? PlayfieldDefinitionDifferenceKind.ChangedCollectionMembership
                                : PlayfieldDefinitionDifferenceKind.ChangedScalarValue,
                            key + "/" + name,
                            "Value presence changed."));
                    continue;
                }

                bool equal = expectedValue.IsCollection == actualValue.IsCollection
                    && (expectedValue.IsCollection
                        ? expectedValue.Values.OrderBy(value => value, StringComparer.Ordinal)
                            .SequenceEqual(actualValue.Values.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal)
                        : expectedValue.Values.SequenceEqual(actualValue.Values, StringComparer.Ordinal));
                if (!equal)
                {
                    differences.Add(
                        new PlayfieldDefinitionDifference(
                            expectedValue.IsCollection || actualValue.IsCollection
                                ? PlayfieldDefinitionDifferenceKind.ChangedCollectionMembership
                                : PlayfieldDefinitionDifferenceKind.ChangedScalarValue,
                            key + "/" + name,
                            string.Empty));
                }
            }

            if (!ProvenanceEqual(expected.Provenance, actual.Provenance))
            {
                differences.Add(
                    new PlayfieldDefinitionDifference(
                        PlayfieldDefinitionDifferenceKind.ChangedSourceProvenance,
                        key,
                        string.Empty));
            }
        }

        private static IDictionary<string, HydratedPlayfieldRecord> UniqueRecords(IEnumerable<HydratedPlayfieldRecord> records)
        {
            return records.GroupBy(RecordKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }

        private static IDictionary<string, HydratedPlayfieldValue> UniqueValues(IEnumerable<HydratedPlayfieldValue> values)
        {
            return values.GroupBy(value => value.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }

        private static string RecordKey(HydratedPlayfieldRecord record)
        {
            return record.Category + ":" + record.Identity;
        }

        private static bool ProvenanceEqual(
            IEnumerable<PlayfieldSourceProvenance> expected,
            IEnumerable<PlayfieldSourceProvenance> actual)
        {
            return ProvenanceSignatures(expected).SequenceEqual(ProvenanceSignatures(actual), StringComparer.Ordinal);
        }

        private static IEnumerable<string> ProvenanceSignatures(IEnumerable<PlayfieldSourceProvenance> sources)
        {
            return sources.Select(
                    source => string.Join(
                        "|",
                        source.ContributionOrder,
                        source.SourceKind,
                        source.SourceIdentity,
                        source.SourceDigest,
                        source.Adapter,
                        source.Resolution))
                .OrderBy(value => value, StringComparer.Ordinal);
        }

        private static bool HasUnresolved(HydratedPlayfieldDefinition definition)
        {
            return definition.Provenance.Any(source => source.Resolution == PlayfieldProvenanceResolution.Unresolved)
                || definition.Records.SelectMany(record => record.Provenance)
                    .Any(source => source.Resolution == PlayfieldProvenanceResolution.Unresolved);
        }

        private static bool HasOrderingDifference(
            HydratedPlayfieldDefinition expected,
            HydratedPlayfieldDefinition actual)
        {
            return !expected.Records.Select(RecordKey).SequenceEqual(actual.Records.Select(RecordKey), StringComparer.Ordinal)
                || expected.Records.Zip(
                        actual.Records,
                        (left, right) => left.Values.Select(value => value.Name)
                            .SequenceEqual(right.Values.Select(value => value.Name), StringComparer.Ordinal))
                    .Any(equal => !equal);
        }
    }
}
