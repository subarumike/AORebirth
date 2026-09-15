namespace ZoneEngine_New.Core.Mobs
{
    using System.Collections.Generic;
    using System.Text.Json;

    public sealed class MobItemTableEntry
    {
        public string Hash { get; set; } = string.Empty;

        public int Repeats { get; set; }

        public int Chance { get; set; }

        public int LevelMod { get; set; }
    }

    /// <summary>
    /// Resolved NPC template used at spawn. Materialized from NpcTemplates.json.
    /// </summary>
    public sealed class MobTemplate
    {
        public bool HasHeadMesh { get; set; }

        public string Hash { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int TemplateId { get; set; }

        /// <summary>
        /// Reserved diagnostic placeholder; never a resolved runtime actor or fallback.
        /// </summary>
        public const string FallbackHash = "AAAA";

        /// <summary>
        /// Flat stats for this template. Applied last (after family and optional NpcStatTemplate
        /// curves), so anything set here wins outright for bosses and other one-offs.
        /// </summary>
        public Dictionary<int, int> Stats { get; set; } = new();

        /// <summary>
        /// Optional family curve id. Unused once NpcTemplates.json supplies per-band stats.
        /// </summary>

        /// <summary>
        /// Optional overlay curve id. Unused once NpcTemplates.json supplies per-band stats.
        /// </summary>

        /// <summary>When false, players cannot fight this NPC and it gets no combat brain.</summary>
        public bool Attackable { get; set; } = true;

        public int MinLevel { get; set; }

        public int MaxLevel { get; set; }

        public int? NpcFamily { get; set; }

        public int NpcStatTemplate { get; set; }

        /// <summary>Stats have already been materialized from the optional catalog's level bands.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool HasResolvedStatBands { get; internal set; }

        /// <summary>Historical evidence metadata is preserved but does not authorize runtime activation.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("ContentAcceptance")]
        public JsonElement? ContentProvenance { get; set; }

        public bool UnresolvedPlaceholder { get; set; }

        public List<NpcWeaponVariant> WeaponVariants { get; set; } = new();

        /// <summary>Per-slot AOID lists from the template Equipment jagged array.</summary>
        public List<List<int>> Equipment { get; set; } = new();

        /// <summary>SCFU texture overrides keyed by place. Empty omits the texture block.</summary>
        public Dictionary<int, int> Textures { get; set; } = new();

        public int KnuBotId { get; set; }

        public string RawFeatures { get; set; } = string.Empty;

        public JsonElement? Features { get; set; }

        public List<MobItemTableEntry> ItemTable { get; set; } = new();

        public string BinaryListData { get; set; } = string.Empty;

        public JsonElement? BinaryList { get; set; }
    }
}
