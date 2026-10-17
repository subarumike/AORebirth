namespace ZoneEngine_New.Core.Mobs
{
    using System.Collections.Generic;
    using System.Text.Json;

    using SmokeLounge.AOtomation.Messaging.GameData;

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

        /// <summary>
        /// Behaviour switches from the level entry's "Features" (plus NoCombat for the unresolved placeholder and for
        /// blue-named NPCs).
        /// </summary>
        public HashSet<NpcFeature> Features { get; set; } = new();

        public bool Has(NpcFeature feature) => Features.Contains(feature);

        public int MinLevel { get; set; }

        public int MaxLevel { get; set; }

        /// <summary>Historical evidence metadata is preserved but does not authorize runtime activation.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("ContentAcceptance")]
        public JsonElement? ContentProvenance { get; set; }

        public bool UnresolvedPlaceholder { get; set; }

        public List<NpcWeaponVariant> WeaponVariants { get; set; } = new();

        /// <summary>Per-slot AOID lists from the template Equipment jagged array.</summary>
        public List<List<int>> Equipment { get; set; } = new();

        /// <summary>Monster weapon hashes armed as attack cycles (see <see cref="NpcCharacter.FillEquipment"/>).</summary>
        public List<string> MonsterWeapons { get; set; } = new();

        /// <summary>Weapon items [low, high] armed as attack cycles as they are.</summary>
        public List<List<int>> CombatWeapons { get; set; } = new();

        /// <summary>NPCs it fights on its own (see <see cref="NpcDefenseService"/>).</summary>
        public NpcDefenseDefinition? Defense { get; set; }

        /// <summary>Waits to be rescued (see <see cref="NpcRescueService"/>).</summary>
        public NpcRescueDefinition? Rescue { get; set; }

        /// <summary>SCFU texture overrides keyed by place. Empty omits the texture block.</summary>
        public Dictionary<int, int> Textures { get; set; } = new();

        /// <summary>Opaque SCFU extended texture override payload copied from template data.</summary>
        public List<TextureOverride> TextureOverrides { get; set; } = new();

        /// <summary>Optional raw CorpseFullUpdate template for NPCs whose corpse visuals are not codec-representable.</summary>
        public MobCorpseFullUpdateTemplate? CorpseFullUpdateTemplate { get; set; }

        public int KnuBotId { get; set; }

        public List<MobItemTableEntry> ItemTable { get; set; } = new();

        /// <summary>Nano programs running on it from the moment it spawns (bosses: size, glow, damage type).</summary>
        public List<int> Nanos { get; set; } = new();

        /// <summary>Visual effects it plays over and over (a Burning Cleaning Robot's flames).</summary>
        public List<NpcGfxEffect> GfxEffects { get; set; } = new();

        /// <summary>Where it walks while idle; null = stands, or wanders when its district does.</summary>
        public NpcPatrol? Patrol { get; set; }

        public string BinaryListData { get; set; } = string.Empty;

        public JsonElement? BinaryList { get; set; }
    }

    /// <summary>
    /// A GfxEffect live sends on the NPC again and again (SpellList with function 53030): every
    /// <see cref="MinSeconds"/>..<see cref="MaxSeconds"/> all of <see cref="Gfx"/> play together.
    /// </summary>
    public sealed class NpcGfxEffect
    {
        public int[] Gfx { get; set; } = [];

        public float MinSeconds { get; set; } = 5f;

        public float MaxSeconds { get; set; } = 5f;
    }

    /// <summary>
    /// The waypoints live NPCs of this kind walked between, as a directed graph: from a waypoint the next is picked by
    /// how often live walked each edge, so dominant edges make a loop and even ones a random walk. An NPC uses the part
    /// of the graph around its spawn point.
    /// </summary>
    public sealed class NpcPatrol
    {
        /// <summary>[x, y, z] per waypoint.</summary>
        public float[][] Nodes { get; set; } = [];

        /// <summary>[from, to, times walked].</summary>
        public int[][] Edges { get; set; } = [];

        /// <summary>Runs between waypoints instead of walking (live mode 25: Rollerrats).</summary>
        public bool Run { get; set; }

        /// <summary>Pause at each waypoint, seconds.</summary>
        public float MinPauseSeconds { get; set; } = 0.5f;

        public float MaxPauseSeconds { get; set; } = 2f;
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
