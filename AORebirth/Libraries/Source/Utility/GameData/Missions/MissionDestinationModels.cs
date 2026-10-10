namespace Utility.GameData.Missions
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    /// <summary>An exact captured condition, not a reconstructed retail eligibility rule.</summary>
    public sealed class MissionDestinationCondition : IEquatable<MissionDestinationCondition>
    {
        public MissionDestinationCondition(int characterLevel, int expectedMissionQl, int difficultyDetent,
            int factionSide, int breed, int profession, int terminalPlayfieldId, uint terminalIdentityType,
            uint terminalIdentityInstance, byte[] secondarySliderBytes, string missionType)
        {
            if (secondarySliderBytes == null || secondarySliderBytes.Length != 6)
                throw new ArgumentException("Six raw secondary slider bytes are required.", nameof(secondarySliderBytes));
            if (string.IsNullOrWhiteSpace(missionType))
                throw new ArgumentException("A mission type is required.", nameof(missionType));
            CharacterLevel = characterLevel;
            ExpectedMissionQl = expectedMissionQl;
            DifficultyDetent = difficultyDetent;
            FactionSide = factionSide;
            Breed = breed;
            Profession = profession;
            TerminalPlayfieldId = terminalPlayfieldId;
            TerminalIdentityType = terminalIdentityType;
            TerminalIdentityInstance = terminalIdentityInstance;
            SecondarySliderBytes = new ReadOnlyCollection<byte>((byte[])secondarySliderBytes.Clone());
            MissionType = missionType;
        }

        public int CharacterLevel { get; }
        public int ExpectedMissionQl { get; }
        public int DifficultyDetent { get; }
        public int FactionSide { get; }
        public int Breed { get; }
        public int Profession { get; }
        public int TerminalPlayfieldId { get; }
        public uint TerminalIdentityType { get; }
        public uint TerminalIdentityInstance { get; }
        /// <summary>GoodBad, OrderChaos, OpenHidden, PhysicalMystical, HeadOnStealth, MoneyXp; no normalization.</summary>
        public IReadOnlyList<byte> SecondarySliderBytes { get; }
        public string MissionType { get; }

        public bool Equals(MissionDestinationCondition other)
        {
            return other != null && CharacterLevel == other.CharacterLevel && ExpectedMissionQl == other.ExpectedMissionQl
                && DifficultyDetent == other.DifficultyDetent && FactionSide == other.FactionSide && Breed == other.Breed
                && Profession == other.Profession && TerminalPlayfieldId == other.TerminalPlayfieldId
                && TerminalIdentityType == other.TerminalIdentityType && TerminalIdentityInstance == other.TerminalIdentityInstance
                && string.Equals(MissionType, other.MissionType, StringComparison.Ordinal)
                && SecondarySliderBytes.SequenceEqual(other.SecondarySliderBytes);
        }

        public override bool Equals(object obj) { return Equals(obj as MissionDestinationCondition); }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CharacterLevel;
                foreach (int value in new[] { ExpectedMissionQl, DifficultyDetent, FactionSide, Breed, Profession,
                    TerminalPlayfieldId, (int)TerminalIdentityType, (int)TerminalIdentityInstance })
                {
                    hash = hash * 397 ^ value;
                }
                foreach (byte value in SecondarySliderBytes)
                {
                    hash = hash * 397 ^ value;
                }
                return hash * 397 ^ StringComparer.Ordinal.GetHashCode(MissionType);
            }
        }
    }

    /// <summary>Captured WorldPos representation for one exact placement; not an operational entrance key.</summary>
    public sealed class MissionDestinationWorldPosition
    {
        internal MissionDestinationWorldPosition(MissionWorldPositionData data)
        {
            Identity = new MissionPlacementIdentity(data.IdentityType, data.IdentityInstance);
            PlayfieldIdentityType = data.PlayfieldIdentityType;
            WorldOffsetX = data.WorldOffsetX;
            WorldOffsetZ = data.WorldOffsetZ;
        }

        public MissionPlacementIdentity Identity { get; }
        public uint PlayfieldIdentityType { get; }
        public int WorldOffsetX { get; }
        public int WorldOffsetZ { get; }
    }

    /// <summary>The complete client placement identity; names and coordinates are not identity.</summary>
    public readonly struct MissionPlacementIdentity : IEquatable<MissionPlacementIdentity>
    {
        public MissionPlacementIdentity(uint identityType, uint identityInstance)
        {
            IdentityType = identityType;
            IdentityInstance = identityInstance;
        }

        public uint IdentityType { get; }
        public uint IdentityInstance { get; }

        public bool Equals(MissionPlacementIdentity other)
        {
            return IdentityType == other.IdentityType && IdentityInstance == other.IdentityInstance;
        }

        public override bool Equals(object obj)
        {
            return obj is MissionPlacementIdentity && Equals((MissionPlacementIdentity)obj);
        }

        public override int GetHashCode()
        {
            unchecked { return ((int)IdentityType * 397) ^ (int)IdentityInstance; }
        }

        public override string ToString()
        {
            return IdentityType.ToString("X8") + ":" + IdentityInstance.ToString("X8");
        }
    }

    public sealed class MissionPlacementNameProvenance
    {
        internal MissionPlacementNameProvenance(NameProvenanceData data)
        {
            Encoding = data.Encoding;
            ResolutionPath = data.ResolutionPath;
            ResourceType = data.ResourceType;
            ResourceInstance = data.ResourceInstance;
            SourceOffset = data.SourceOffset;
        }

        public string Encoding { get; }
        public string ResolutionPath { get; }
        public uint ResourceType { get; }
        public uint ResourceInstance { get; }
        public long SourceOffset { get; }
    }

    public sealed class MissionPlacementContainer
    {
        internal MissionPlacementContainer(PlacementContainerData data)
        {
            ResourceType = data.ResourceType;
            ResourceInstance = data.ResourceInstance;
        }

        public uint ResourceType { get; }
        public uint ResourceInstance { get; }
    }

    /// <summary>One placement only. Operational entrance binding and effective stat BD are unresolved.</summary>
    public sealed class MissionEntrancePlacement
    {
        internal MissionEntrancePlacement(PlacementData data)
        {
            Identity = new MissionPlacementIdentity(data.IdentityType, data.IdentityInstance);
            PlayfieldId = data.PlayfieldId;
            LocalX = (float)data.LocalX;
            LocalY = (float)data.LocalY;
            LocalZ = (float)data.LocalZ;
            LocalXBits = data.LocalXBits;
            LocalYBits = data.LocalYBits;
            LocalZBits = data.LocalZBits;
            RotationComponent0 = (float)data.RotationComponent0;
            RotationComponent1 = (float)data.RotationComponent1;
            RotationComponent2 = (float)data.RotationComponent2;
            RotationComponent3 = (float)data.RotationComponent3;
            DisplayName = data.DisplayName;
            RawNameHex = data.RawNameHex;
            NameProvenance = new MissionPlacementNameProvenance(data.NameProvenance);
            TemplateInstance = data.TemplateInstance;
            SourcePlacementContainer = new MissionPlacementContainer(data.SourcePlacementContainer);
            SourceRecordOffset = data.SourceRecordOffset;
            SourceRecordLength = data.SourceRecordLength;
            SourceRecordSha256 = data.SourceRecordSha256;
            SourceDatabaseSha256 = data.SourceDatabaseSha256;
            Classification = data.Classification;
            ObservationClassification = data.ObservationClassification;
        }

        public MissionPlacementIdentity Identity { get; }
        public uint IdentityType { get { return Identity.IdentityType; } }
        public uint IdentityInstance { get { return Identity.IdentityInstance; } }
        public int PlayfieldId { get; }
        public float LocalX { get; }
        public float LocalY { get; }
        public float LocalZ { get; }
        public uint LocalXBits { get; }
        public uint LocalYBits { get; }
        public uint LocalZBits { get; }
        public float RotationComponent0 { get; }
        public float RotationComponent1 { get; }
        public float RotationComponent2 { get; }
        public float RotationComponent3 { get; }
        public string DisplayName { get; }
        public string RawNameHex { get; }
        public MissionPlacementNameProvenance NameProvenance { get; }
        public uint TemplateInstance { get; }
        public MissionPlacementContainer SourcePlacementContainer { get; }
        public long SourceRecordOffset { get; }
        public int SourceRecordLength { get; }
        public string SourceRecordSha256 { get; }
        public string SourceDatabaseSha256 { get; }
        public string Classification { get; }
        public string ObservationClassification { get; }
        public int? OperationalEntranceKey { get { return null; } }
        public int? EffectiveStatBd { get { return null; } }
    }

    /// <summary>Observed associations only; none defines an eligibility restriction or probability.</summary>
    public sealed class ObservedMissionDestinationEvidence
    {
        internal ObservedMissionDestinationEvidence(ObservedDestinationData data, int playfieldId)
        {
            Identity = new MissionPlacementIdentity(data.IdentityType, data.IdentityInstance);
            PlayfieldId = playfieldId;
            Classification = data.Classification;
            ObservationCount = data.ObservationCount;
            RequestCount = data.RequestCount;
            CohortCount = data.CohortCount;
            SessionCount = data.SessionCount;
            ObservedExpectedMissionQls = Freeze(data.ObservedExpectedMissionQls);
            ObservedCharacterLevels = Freeze(data.ObservedCharacterLevels);
            ObservedMissionTypes = Freeze(data.ObservedMissionTypes);
            ObservedTerminalPlayfields = Freeze(data.ObservedTerminalPlayfields);
            ObservedFactionSides = Freeze(data.ObservedFactionSides);
            ObservedFactionSideValues = Freeze(data.ObservedFactionSideValues);
            FactionEvidenceClassification = data.FactionEvidenceClassification;
        }

        public MissionPlacementIdentity Identity { get; }
        public uint IdentityType { get { return Identity.IdentityType; } }
        public uint IdentityInstance { get { return Identity.IdentityInstance; } }
        public int PlayfieldId { get; }
        public string Classification { get; }
        public int ObservationCount { get; }
        public int RequestCount { get; }
        public int CohortCount { get; }
        public int SessionCount { get; }
        public IReadOnlyList<int> ObservedExpectedMissionQls { get; }
        public IReadOnlyList<int> ObservedCharacterLevels { get; }
        public IReadOnlyList<string> ObservedMissionTypes { get; }
        public IReadOnlyList<int> ObservedTerminalPlayfields { get; }
        public IReadOnlyList<string> ObservedFactionSides { get; }
        public IReadOnlyList<int> ObservedFactionSideValues { get; }
        public string FactionEvidenceClassification { get; }

        private static IReadOnlyList<T> Freeze<T>(T[] values)
        {
            return new ReadOnlyCollection<T>((T[])values.Clone());
        }
    }
}
