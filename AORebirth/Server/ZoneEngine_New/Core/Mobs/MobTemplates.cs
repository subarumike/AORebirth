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

    /// <summary>Level keypoint used to lerp scaling stats between adjacent bands.</summary>
    public sealed class MobStatBand
    {
        public int Level { get; set; }

        public Dictionary<int, int> Stats { get; set; } = new();
    }

    /// <summary>NPC template combat weapon: low/high AOID + 4-char SAW hash.</summary>
    public sealed class MobWeaponEntry
    {
        public int LowId { get; set; }

        public int HighId { get; set; }

        public string Hash { get; set; } = string.Empty;
    }

    /// <summary>
    /// Full NPC template as stored in GameData/NpcTemplate.json.
    /// </summary>
    public sealed class MobTemplate
    {
        public bool HasHeadMesh { get; set; }

        public string Hash { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int TemplateId { get; set; }

        public Dictionary<int, int> Stats { get; set; } = new();

        public List<MobStatBand> StatBands { get; set; } = new();

        /// <summary>When false, players cannot fight this NPC and it gets no combat brain.</summary>
        public bool Attackable { get; set; } = true;

        public int MinLevel { get; set; }

        public int MaxLevel { get; set; }

        /// <summary>Per-slot AOID lists from the template Equipment jagged array.</summary>
        public List<List<int>> Equipment { get; set; } = new();

        /// <summary>Combat weapons as LowId/HighId/Hash for SAW + AttackInfo.</summary>
        public List<MobWeaponEntry> Weapons { get; set; } = new();

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
