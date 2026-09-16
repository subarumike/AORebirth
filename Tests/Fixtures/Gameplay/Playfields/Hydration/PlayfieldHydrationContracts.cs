namespace ZoneEngine.Core.Playfields.Hydration
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Playfields;

    internal enum PlayfieldHydrationMode
    {
        Legacy = 0,
        Shadow = 1,
        AllowList = 2
    }

    internal enum PlayfieldHydrationSourceKind
    {
        Database,
        ExtractedBinary,
        Xml,
        Json,
        Csv,
        GeneratedCapture,
        HardcodedCompatibility,
        Runtime
    }

    internal enum PlayfieldProvenanceResolution
    {
        Accepted,
        Compatibility,
        Unresolved,
        Rejected
    }

    internal enum PlayfieldHydrationDiagnosticSeverity
    {
        Warning,
        Error
    }

    internal enum PlayfieldDefinitionDifferenceKind
    {
        MissingRecord,
        UnexpectedRecord,
        ChangedScalarValue,
        ChangedCollectionMembership,
        ChangedSourceProvenance,
        DuplicateIdentity,
        OrderingOnlyDifference,
        UnresolvedComparison
    }

    [Serializable]
    internal sealed class PlayfieldHydrationRequest
    {
        internal PlayfieldHydrationRequest(int playfieldInstance, int resourceIdentity)
        {
            this.PlayfieldInstance = playfieldInstance;
            this.ResourceIdentity = resourceIdentity;
        }

        internal int PlayfieldInstance { get; private set; }

        internal int ResourceIdentity { get; private set; }
    }

    [Serializable]
    internal sealed class PlayfieldSourceProvenance
    {
        internal PlayfieldSourceProvenance(
            PlayfieldHydrationSourceKind sourceKind,
            string sourceIdentity,
            string sourceDigest,
            string adapter,
            int contributionOrder,
            PlayfieldProvenanceResolution resolution)
        {
            this.SourceKind = sourceKind;
            this.SourceIdentity = sourceIdentity ?? string.Empty;
            this.SourceDigest = sourceDigest ?? string.Empty;
            this.Adapter = adapter ?? string.Empty;
            this.ContributionOrder = contributionOrder;
            this.Resolution = resolution;
        }

        internal PlayfieldHydrationSourceKind SourceKind { get; private set; }

        internal string SourceIdentity { get; private set; }

        internal string SourceDigest { get; private set; }

        internal string Adapter { get; private set; }

        internal int ContributionOrder { get; private set; }

        internal PlayfieldProvenanceResolution Resolution { get; private set; }
    }

    [Serializable]
    internal sealed class HydratedPlayfieldValue
    {
        internal HydratedPlayfieldValue(string name, bool isCollection, IEnumerable<string> values)
        {
            this.Name = name ?? string.Empty;
            this.IsCollection = isCollection;
            this.Values = new List<string>(values ?? new string[0]);
        }

        internal string Name { get; private set; }

        internal bool IsCollection { get; private set; }

        internal List<string> Values { get; private set; }

        internal static HydratedPlayfieldValue Scalar(string name, string value)
        {
            return new HydratedPlayfieldValue(name, false, new[] { value ?? string.Empty });
        }

        internal static HydratedPlayfieldValue Float(string name, float value)
        {
            return Scalar(name, value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }

        internal static HydratedPlayfieldValue Collection(string name, IEnumerable<string> values)
        {
            return new HydratedPlayfieldValue(name, true, values);
        }
    }

    [Serializable]
    internal sealed class HydratedPlayfieldRecord
    {
        internal HydratedPlayfieldRecord(string category, string identity)
        {
            this.Category = category ?? string.Empty;
            this.Identity = identity ?? string.Empty;
            this.Values = new List<HydratedPlayfieldValue>();
            this.Provenance = new List<PlayfieldSourceProvenance>();
        }

        internal string Category { get; private set; }

        internal string Identity { get; private set; }

        internal List<HydratedPlayfieldValue> Values { get; private set; }

        internal List<PlayfieldSourceProvenance> Provenance { get; private set; }
    }

    [Serializable]
    internal sealed class HydratedPlayfieldDefinition
    {
        internal const int CurrentFormatVersion = 1;

        internal HydratedPlayfieldDefinition(int playfieldInstance, int resourceIdentity, string name)
        {
            this.FormatVersion = CurrentFormatVersion;
            this.PlayfieldInstance = playfieldInstance;
            this.ResourceIdentity = resourceIdentity;
            this.Name = name ?? string.Empty;
            this.Records = new List<HydratedPlayfieldRecord>();
            this.Provenance = new List<PlayfieldSourceProvenance>();
            this.Warnings = new List<string>();
            this.Conflicts = new List<string>();
        }

        internal int FormatVersion { get; private set; }

        internal int PlayfieldInstance { get; private set; }

        internal int ResourceIdentity { get; private set; }

        internal string Name { get; private set; }

        internal List<HydratedPlayfieldRecord> Records { get; private set; }

        internal List<PlayfieldSourceProvenance> Provenance { get; private set; }

        internal List<string> Warnings { get; private set; }

        internal List<string> Conflicts { get; private set; }
    }

    [Serializable]
    internal sealed class PlayfieldHydrationDiagnostic
    {
        internal PlayfieldHydrationDiagnostic(
            PlayfieldHydrationDiagnosticSeverity severity,
            string code,
            string message)
        {
            this.Severity = severity;
            this.Code = code ?? string.Empty;
            this.Message = message ?? string.Empty;
        }

        internal PlayfieldHydrationDiagnosticSeverity Severity { get; private set; }

        internal string Code { get; private set; }

        internal string Message { get; private set; }
    }

    [Serializable]
    internal sealed class PlayfieldHydrationResult
    {
        internal PlayfieldHydrationResult(
            HydratedPlayfieldDefinition definition,
            IEnumerable<PlayfieldHydrationDiagnostic> diagnostics)
        {
            this.Definition = definition;
            this.Diagnostics = new List<PlayfieldHydrationDiagnostic>(
                diagnostics ?? new PlayfieldHydrationDiagnostic[0]);
        }

        internal HydratedPlayfieldDefinition Definition { get; private set; }

        internal List<PlayfieldHydrationDiagnostic> Diagnostics { get; private set; }
    }

    [Serializable]
    internal sealed class PlayfieldDefinitionDifference
    {
        internal PlayfieldDefinitionDifference(
            PlayfieldDefinitionDifferenceKind kind,
            string identity,
            string detail)
        {
            this.Kind = kind;
            this.Identity = identity ?? string.Empty;
            this.Detail = detail ?? string.Empty;
        }

        internal PlayfieldDefinitionDifferenceKind Kind { get; private set; }

        internal string Identity { get; private set; }

        internal string Detail { get; private set; }
    }

    internal interface IPlayfieldDefinitionHydrator
    {
        PlayfieldHydrationResult Hydrate(PlayfieldHydrationRequest request);
    }

    internal interface IPlayfieldDefinitionValidator
    {
        IList<PlayfieldHydrationDiagnostic> Validate(HydratedPlayfieldDefinition definition);
    }

    [Serializable]
    internal sealed class PlayfieldRuntimeMaterializationRequest
    {
        internal PlayfieldRuntimeMaterializationRequest(int playfieldInstance)
        {
            this.PlayfieldInstance = playfieldInstance;
        }

        internal int PlayfieldInstance { get; private set; }
    }

    internal interface IPlayfieldRuntimeMaterializer
    {
        IPlayfield Materialize(PlayfieldRuntimeMaterializationRequest request);
    }
}
