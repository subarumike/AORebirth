using System;
using System.IO;
using AORebirth.Core.Items;

class Program
{
    static void Main()
    {
        string dir = @"C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug";
        Directory.SetCurrentDirectory(dir);
        ItemLoader.CacheAllItems(Path.Combine(dir, "items.dat"));
        int[] ids = { 293297, 291082, 291045, 291043 };
        foreach (int id in ids)
        {
            if (!ItemLoader.ItemList.ContainsKey(id))
            {
                Console.WriteLine(id + " MISSING");
                continue;
            }

            ItemTemplate t = ItemLoader.ItemList[id];
            string rel = t.Relations == null ? "" : string.Join(",", t.Relations);
            Console.WriteLine(
                "id=" + id
                + " ql=" + t.Quality
                + " stackable=" + t.IsStackable()
                + " hasMC=" + t.HasMultipleCount()
                + " templateMC=" + t.MultipleCount
                + " rel=[" + rel + "]");

            try
            {
                Item solo = new Item(50, id, id) { MultipleCount = 50 };
                Console.WriteLine(
                    "  solo50 setMC50 -> Q=" + solo.Quality
                    + " MC=" + solo.MultipleCount
                    + " low=" + solo.LowID
                    + " high=" + solo.HighID);

                int pairLow = 0, pairHigh = 0, bestLow = int.MaxValue, bestHigh = int.MinValue;
                if (t.Relations != null)
                {
                    foreach (int r in t.Relations)
                    {
                        if (!ItemLoader.ItemList.ContainsKey(r)) continue;
                        int q = ItemLoader.ItemList[r].Quality;
                        if (q < bestLow) { bestLow = q; pairLow = r; }
                        if (q > bestHigh) { bestHigh = q; pairHigh = r; }
                    }
                }

                if (pairLow > 0 && pairHigh > 0)
                {
                    Item pair = new Item(50, pairLow, pairHigh) { MultipleCount = 50 };
                    Console.WriteLine(
                        "  pair50 setMC50 -> Q=" + pair.Quality
                        + " MC=" + pair.MultipleCount
                        + " low=" + pair.LowID
                        + " high=" + pair.HighID);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  err " + ex.Message);
            }
        }
    }
}
