namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// How a stock entry's <see cref="VendingMachineStockEntry.Hash"/> is turned into shop slots.
    /// </summary>
    public enum StockExpansion
    {
        /// <summary>Roll one random item out of the bucket (honours Repeats and Chance).</summary>
        RandomOne = 0,

        /// <summary>Stock every leaf item in the bucket, one slot each. Repeats and Chance are ignored.</summary>
        ExpandAll = 1
    }

    /// <summary>One line of a vending machine's stock table in GameData/VendingMachines.json.</summary>
    public sealed class VendingMachineStockEntry
    {
        public string Hash { get; set; } = string.Empty;

        public int MinLevel { get; set; }

        public int MaxLevel { get; set; }

        public int Repeats { get; set; }

        public int Chance { get; set; }

        /// <summary>0 rolls one random item; any other value stocks the whole bucket.</summary>
        public int Flags { get; set; }

        [JsonIgnore]
        public StockExpansion Expansion => Flags == 0 ? StockExpansion.RandomOne : StockExpansion.ExpandAll;
    }

    /// <summary>
    /// Stock table for one vending machine template id, as stored in GameData/VendingMachines.json.
    /// </summary>
    public sealed class VendingMachineDefinition
    {
        public List<VendingMachineStockEntry> Inventory { get; set; } = new();

        /// <summary>
        /// Upper bound on generated slots. The client shop pane is a fixed grid, and
        /// <see cref="StockExpansion.ExpandAll"/> over a large category can produce far more
        /// entries than it can show.
        /// </summary>
        public const int MaxSlots = 64;

        /// <summary>Entries with a usable hash and a sane repeat count.</summary>
        public IEnumerable<VendingMachineStockEntry> UsableEntries()
        {
            List<VendingMachineStockEntry> entries = Inventory;
            for (int i = 0; i < entries.Count; i++)
            {
                VendingMachineStockEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Hash))
                    continue;

                if (entry.Expansion == StockExpansion.RandomOne && entry.Repeats <= 0)
                    continue;

                yield return entry;
            }
        }

        /// <summary>Inclusive quality band for an entry, normalized so Min &lt;= Max and both &gt;= 1.</summary>
        public static (int Min, int Max) QualityBand(VendingMachineStockEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            int min = entry.MinLevel;
            int max = entry.MaxLevel;
            if (max < min)
                (min, max) = (max, min);

            return (Math.Max(1, min), Math.Max(1, max));
        }
    }
}
