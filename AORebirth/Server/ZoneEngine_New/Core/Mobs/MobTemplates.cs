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
        /// Placeholder leaf used when a spawn hash is not in <c>NpcTemplates.json</c>.
        /// </summary>
        public const string FallbackHash = "AAAA";

        /// <summary>Per-band stats copied from NpcTemplates.json.</summary>
        public Dictionary<int, int> Stats { get; set; } = new();

        /// <summary>When false, players cannot fight this NPC and it gets no combat brain.</summary>
        public bool Attackable { get; set; } = true;

        public int MinLevel { get; set; }

        public int MaxLevel { get; set; }

        /// <summary>Historical evidence metadata is preserved but does not authorize runtime activation.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("ContentAcceptance")]
        public JsonElement? ContentProvenance { get; set; }

        public bool UnresolvedPlaceholder { get; set; }

        public List<NpcWeaponVariant> WeaponVariants { get; set; } = new();

        /// <summary>Per-slot AOID lists from the template Equipment jagged array.</summary>
        public List<List<int>> Equipment { get; set; } = new();

        /// <summary>SCFU texture overrides keyed by place. Empty omits the texture block.</summary>
        public Dictionary<int, int> Textures { get; set; } = new();

        /// <summary>Opaque SCFU extended texture override payload copied from template data.</summary>
        public byte[] ExtendedTextureOverrideData { get; set; } = [];

        /// <summary>Optional raw CorpseFullUpdate template for NPCs whose corpse visuals are not codec-representable.</summary>
        public MobCorpseFullUpdateTemplate? CorpseFullUpdateTemplate { get; set; }

        public int KnuBotId { get; set; }

        public string RawFeatures { get; set; } = string.Empty;

        public JsonElement? Features { get; set; }

        public List<MobItemTableEntry> ItemTable { get; set; } = new();

        public string BinaryListData { get; set; } = string.Empty;

        public JsonElement? BinaryList { get; set; }
    }

    public sealed class MobCorpseFullUpdateTemplate
    {
        public byte[] PacketTemplate { get; set; } = [];

        public int MessageId { get; set; }

        public int MessageIdOffset { get; set; } = -1;

        public int PacketLengthOffset { get; set; } = -1;

        public int SenderInstanceOffset { get; set; } = -1;

        public int ReceiverInstanceOffset { get; set; } = -1;

        public int CorpseInstanceOffset { get; set; } = -1;

        public int PositionXOffset { get; set; } = -1;

        public int PositionYOffset { get; set; } = -1;

        public int PositionZOffset { get; set; } = -1;

        public int PlayfieldIdOffset { get; set; } = -1;

        public int DeadNpcInstanceOffset { get; set; } = -1;

        public int CatMeshOffset { get; set; } = -1;

        public int CashOffset { get; set; } = -1;

        public int MonsterDataOffset { get; set; } = -1;

        public int TailDeadNpcInstanceOffset { get; set; } = -1;
    }
}
