using System;
using System.Linq;
using AORebirth.Core.Items;

class Probe
{
    static void Main()
    {
        ItemLoader.CacheAllItems(@"C:\Users\nermi\source\repos\AORebirth\AORebirth\Datafiles\items.dat");

        int[] ids = { 117322, 96078, 96079, 96092, 96088, 96093 };
        foreach (int id in ids)
        {
            Dump(id);
        }

        Console.WriteLine("--- Placement histogram for IsVehicle=1 ---");
        var hist = new System.Collections.Generic.Dictionary<int, int>();
        int count = 0;
        foreach (var kv in ItemLoader.ItemList)
        {
            ItemTemplate t = kv.Value;
            int isV;
            if (t.Stats == null || !t.Stats.TryGetValue(658, out isV) || isV != 1)
            {
                continue;
            }

            int p;
            if (!t.Stats.TryGetValue(298, out p))
            {
                p = -1;
            }

            if (!hist.ContainsKey(p))
            {
                hist[p] = 0;
            }

            hist[p]++;
            count++;
        }

        Console.WriteLine("vehicleCount=" + count);
        foreach (var h in hist.OrderBy(x => x.Key))
        {
            Console.WriteLine("  Placement=" + h.Key + " count=" + h.Value);
        }
    }

    static void Dump(int id)
    {
        if (!ItemLoader.ItemList.ContainsKey(id))
        {
            Console.WriteLine(id + " MISSING");
            return;
        }

        ItemTemplate t = ItemLoader.ItemList[id];
        int p = t.Stats != null && t.Stats.ContainsKey(298) ? t.Stats[298] : -1;
        int v = t.Stats != null && t.Stats.ContainsKey(658) ? t.Stats[658] : -1;
        Console.WriteLine("id=" + id + " QL=" + t.Quality + " Placement=" + p + " IsVehicle=" + v);
    }
}
