namespace ZoneEngine.Core.Playfields.Hydration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class PlayfieldDefinitionValidator : IPlayfieldDefinitionValidator
    {
        private static readonly HashSet<string> RuntimeOnlyNames =
            new HashSet<string>(
                new[]
                {
                    "activeplayers",
                    "aggro",
                    "aggrotarget",
                    "corpses",
                    "currenthp",
                    "currentnano",
                    "dynamicidentity",
                    "lootrolls",
                    "pathingstate",
                    "runtimeid",
                    "temporaryid",
                    "timers"
                },
                StringComparer.Ordinal);

        public IList<PlayfieldHydrationDiagnostic> Validate(HydratedPlayfieldDefinition definition)
        {
            var diagnostics = new List<PlayfieldHydrationDiagnostic>();
            if (definition == null)
            {
                diagnostics.Add(Error("NULL_DEFINITION", "The hydrated definition is required."));
                return diagnostics;
            }

            if (definition.FormatVersion != HydratedPlayfieldDefinition.CurrentFormatVersion)
            {
                diagnostics.Add(Error("FORMAT_VERSION", "The definition format version is unsupported."));
            }

            if (definition.PlayfieldInstance <= 0 || definition.ResourceIdentity <= 0)
            {
                diagnostics.Add(Error("INVALID_IDENTITY", "Playfield and resource identities must be positive."));
            }

            foreach (IGrouping<string, HydratedPlayfieldRecord> duplicate in definition.Records
                .GroupBy(RecordKey, StringComparer.Ordinal)
                .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Error("DUPLICATE_RECORD", duplicate.Key));
            }

            foreach (HydratedPlayfieldRecord record in definition.Records)
            {
                if (string.IsNullOrWhiteSpace(record.Category) || string.IsNullOrWhiteSpace(record.Identity))
                {
                    diagnostics.Add(Error("MISSING_RECORD_IDENTITY", RecordKey(record)));
                }

                foreach (IGrouping<string, HydratedPlayfieldValue> duplicate in record.Values
                    .GroupBy(value => value.Name, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1))
                {
                    diagnostics.Add(Error("DUPLICATE_VALUE", RecordKey(record) + "/" + duplicate.Key));
                }

                foreach (HydratedPlayfieldValue value in record.Values)
                {
                    if (RuntimeOnlyNames.Contains(NormalizeName(value.Name)))
                    {
                        diagnostics.Add(
                            Error("RUNTIME_STATE_NOT_ALLOWED", RecordKey(record) + "/" + value.Name));
                    }

                    if (!value.IsCollection && value.Values.Count != 1)
                    {
                        diagnostics.Add(
                            Error("INVALID_SCALAR", RecordKey(record) + "/" + value.Name));
                    }
                }

                ValidateProvenance(record.Provenance, RecordKey(record), diagnostics);
            }

            ValidateProvenance(definition.Provenance, "definition", diagnostics);
            return diagnostics;
        }

        private static void ValidateProvenance(
            IEnumerable<PlayfieldSourceProvenance> provenance,
            string owner,
            ICollection<PlayfieldHydrationDiagnostic> diagnostics)
        {
            foreach (PlayfieldSourceProvenance source in provenance)
            {
                if (source.SourceKind == PlayfieldHydrationSourceKind.Runtime)
                {
                    diagnostics.Add(Error("RUNTIME_SOURCE_NOT_ALLOWED", owner + "/" + source.SourceIdentity));
                }

                if (source.Resolution == PlayfieldProvenanceResolution.Unresolved)
                {
                    diagnostics.Add(
                        Warning("UNRESOLVED_PROVENANCE", owner + "/" + source.SourceIdentity));
                }
            }
        }

        private static string RecordKey(HydratedPlayfieldRecord record)
        {
            return (record.Category ?? string.Empty) + ":" + (record.Identity ?? string.Empty);
        }

        private static string NormalizeName(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private static PlayfieldHydrationDiagnostic Error(string code, string message)
        {
            return new PlayfieldHydrationDiagnostic(PlayfieldHydrationDiagnosticSeverity.Error, code, message);
        }

        private static PlayfieldHydrationDiagnostic Warning(string code, string message)
        {
            return new PlayfieldHydrationDiagnostic(PlayfieldHydrationDiagnosticSeverity.Warning, code, message);
        }
    }
}
