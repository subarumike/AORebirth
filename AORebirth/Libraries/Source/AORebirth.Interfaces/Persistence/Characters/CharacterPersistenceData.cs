namespace AORebirth.Interfaces.Persistence.Characters
{
    using System;
    using System.Collections.Generic;

    /// <summary>Existing characters-row gameplay projection. Other character columns are never overwritten.</summary>
    public sealed class CharacterStateData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Playfield { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float HeadingW { get; set; }
        public float HeadingX { get; set; }
        public float HeadingY { get; set; }
        public float HeadingZ { get; set; }

        /// <summary>The owning account's login.AccountFlags (read at load; never saved from here).</summary>
        public int AccountFlags { get; set; }

        /// <summary>The owning account's login.GM (read at load; never saved from here).</summary>
        public int GmLevel { get; set; }
    }

    /// <summary>Persisted base value, not an effective value with equipment/nano modifiers applied.</summary>
    public sealed class CharacterStatData
    {
        public int StatId { get; set; }
        public int StatValue { get; set; }
    }

    public sealed class PersistedItemData
    {
        public int InstanceId { get; set; }
        public int ContainerType { get; set; }
        public int ContainerInstance { get; set; }
        public int ContainerPlacement { get; set; }
        public int ItemType { get; set; }
        public int LowId { get; set; }
        public int HighId { get; set; }
        public int Quality { get; set; }
        public int StackCount { get; set; }
        public int Source { get; set; }
    }

    public sealed class ItemLocationData
    {
        public int InstanceId { get; set; }
        public int ContainerType { get; set; }
        public int ContainerInstance { get; set; }
        public int ContainerPlacement { get; set; }
        /// <summary>Null preserves the stored count for a location-only operation.</summary>
        public int? StackCount { get; set; }
    }

    public sealed class ItemStackData
    {
        public int InstanceId { get; set; }
        public int ExpectedCount { get; set; }
        public int FinalCount { get; set; }
    }

    /// <summary>A stored item instance whose QL changes in place (a leveling item following its wearer's level).</summary>
    public sealed class ItemQualityData
    {
        public int InstanceId { get; set; }
        public int ExpectedQuality { get; set; }
        public int FinalQuality { get; set; }
    }

    public sealed class CharacterInventoryMutationData
    {
        public int CharacterId { get; set; }
        /// <summary>Exact durable source rows to lock and compare before moving a shared persisted container item.</summary>
        public IList<PersistedItemData> ExpectedSources { get; set; } = new List<PersistedItemData>();
        public IList<PersistedItemData> Inserts { get; set; } = new List<PersistedItemData>();
        public IList<ItemLocationData> Locations { get; set; } = new List<ItemLocationData>();
        public IList<ItemStackData> Stacks { get; set; } = new List<ItemStackData>();
        public IList<ItemQualityData> Qualities { get; set; } = new List<ItemQualityData>();
        public IList<int> UploadedNanoIds { get; set; } = new List<int>();
        public IList<CharacterStatData> FinalStats { get; set; } = new List<CharacterStatData>();
        public IList<int> EmptyContainersBeforeRetire { get; set; } = new List<int>();
    }

    public sealed class CharacterCreditData
    {
        public int CharacterId { get; set; }
        public int Cash { get; set; }
        public IList<int> UploadedNanoIds { get; set; } = new List<int>();

        /// <summary>Other currency stats (a VP shop's Victory Points) written in the same transaction as the cash.</summary>
        public IList<CharacterStatData> Currencies { get; set; } = new List<CharacterStatData>();
    }

    public sealed class ItemCreditMutationData
    {
        public IList<PersistedItemData> Inserts { get; set; } = new List<PersistedItemData>();
        public IList<ItemLocationData> Locations { get; set; } = new List<ItemLocationData>();
        public IList<CharacterCreditData> Characters { get; set; } = new List<CharacterCreditData>();
    }

    public sealed class PersistedActiveNanoData
    {
        public int NanoId { get; set; }
        public int Strain { get; set; }
        public int NanoInstance { get; set; }
        public int DurationCentiseconds { get; set; }
        public long ExpiresAtUtcTicks { get; set; }
    }

    public sealed class PersistedSkillLockData
    {
        public int StatId { get; set; }
        public long ExpiresAtUtcTicks { get; set; }
    }

    /// <summary>One LockPerk cooldown: perk id (charactersperks.PacketId) and absolute UTC expiry.</summary>
    public sealed class PersistedPerkLockData
    {
        public int PerkId { get; set; }
        public long ExpiresAtUtcTicks { get; set; }
    }

    /// <summary>One organizations row with its leader's character name (stats Clan = Id).</summary>
    public sealed class PersistedOrganizationData
    {
        public int Id { get; set; }
        public DateTime Creation { get; set; }
        public string Name { get; set; } = string.Empty;
        public int LeaderId { get; set; }
        public string LeaderName { get; set; } = string.Empty;
        public int GovernmentForm { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Objective { get; set; } = string.Empty;
        public string History { get; set; } = string.Empty;
        public long Tax { get; set; }
        public long Bank { get; set; }
    }

    /// <summary>The editable text columns of the organizations table.</summary>
    public enum OrganizationTextField
    {
        Name,
        Description,
        Objective,
        History
    }

    /// <summary>One /pet rename
    public sealed class PersistedPetNameData
    {
        public int PetType { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>One nano line cooldown: the locked nano strain (stat 75) and absolute UTC expiry.</summary>
    public sealed class PersistedNanoLineLockData
    {
        public int NanoStrain { get; set; }
        public long ExpiresAtUtcTicks { get; set; }
    }

    /// <summary>One personal research tier in progress: its research (perk) id and the research XP it still needs.</summary>
    public sealed class PersistedResearchData
    {
        public int ResearchId { get; set; }
        public int RemainingXp { get; set; }
    }

    /// <summary>Organization invite lockout: remaining seconds, decremented only while the character is online.</summary>
    public sealed class PersistedOrgLockoutData
    {
        public long SecondsRemaining { get; set; }
    }

    /// <summary>Result of one Anarchism kick request stored in org_anar_pend_kick.</summary>
    public enum AnarchistKickOutcome
    {
        /// <summary>The organization, or the membership of the kicker or the target, no longer exists; nothing was stored.</summary>
        Missing,
        /// <summary>The request is stored and the quorum is not reached yet.</summary>
        Recorded,
        /// <summary>The quorum is reached; the stored requests of this target were consumed.</summary>
        Reached
    }

    public sealed class PersistedAnarchistKickerData
    {
        public int KickerId { get; set; }
        public string KickerName { get; set; }
    }

    public sealed class PersistedAnarchistKickResult
    {
        public AnarchistKickOutcome Outcome { get; set; }
        public bool TargetIsLeader { get; set; }
        /// <summary>Requests consumed by a reached quorum, oldest first.</summary>
        public IList<PersistedAnarchistKickerData> Requesters { get; set; } = new List<PersistedAnarchistKickerData>();
    }

    public sealed class CharacterActiveNanoData
    {
        public int CharacterId { get; set; }
        public IList<PersistedActiveNanoData> ActiveNanos { get; set; } = new List<PersistedActiveNanoData>();
        public IList<CharacterStatData> BaseStats { get; set; } = new List<CharacterStatData>();
    }
}
