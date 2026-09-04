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
    /// Full mob template as stored in GameData/MobTemplates.json.
    /// </summary>
    public sealed class MobTemplate
    {
        public bool HasHeadMesh { get; set; }

        public string Hash { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int TemplateId { get; set; }

        public Dictionary<int, int> Stats { get; set; } = new();

        public int MinLevel { get; set; }

        public int MaxLevel { get; set; }

        /// <summary>Per-slot AOID lists from the template Equipment jagged array.</summary>
        public List<List<int>> Equipment { get; set; } = new();

        /// <summary>Combat weapons as [lowId, highId] pairs (index 0 = main, 1 = off).</summary>
        public List<List<int>> Weapons { get; set; } = new();

        public int KnuBotId { get; set; }

        public string RawFeatures { get; set; } = string.Empty;

        public JsonElement? Features { get; set; }

        public List<MobItemTableEntry> ItemTable { get; set; } = new();

        public string BinaryListData { get; set; } = string.Empty;

        public JsonElement? BinaryList { get; set; }
    }

    /// <summary>One [lowId, highId] loot pair under a loot-table hash.</summary>
    public readonly struct LootItemPair
    {
        public LootItemPair(int lowId, int highId)
        {
            LowId = lowId;
            HighId = highId;
        }

        public int LowId { get; }

        public int HighId { get; }
    }
}
