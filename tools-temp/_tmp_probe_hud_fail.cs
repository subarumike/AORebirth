using System;
using System.Linq;
using AORebirth.Core.Actions;
using AORebirth.Core.Items;
using AORebirth.Core.Requirements;
using AORebirth.Enums;

class Probe
{
    static void Main()
    {
        ItemLoader.CacheAllItems(@"C:\Users\nermi\source\repos\AORebirth\AORebirth\Datafiles\items.dat");
        int[] ids = { 253577, 280773, 117322, 226994 };
        foreach (int id in ids)
        {
            if (!ItemLoader.ItemList.ContainsKey(id))
            {
                Console.WriteLine(id + " MISSING");
                continue;
            }

            ItemTemplate t = ItemLoader.ItemList[id];
            int placement = Get(t, 298);
            int isVehicle = Get(t, 658);
            Console.WriteLine(
                "==== " + id + " QL=" + t.Quality + " Placement=" + placement + " IsVehicle=" + isVehicle + " ====");
            if (t.Actions == null)
            {
                continue;
            }

            foreach (AOAction a in t.Actions)
            {
                Console.WriteLine("  ACTION " + a.ActionType);
                if (a.Requirements == null)
                {
                    continue;
                }

                foreach (Requirement r in a.Requirements)
                {
                    Console.WriteLine(
                        "    stat=" + r.Statnumber + " op=" + r.Operator + "(" + (int)r.Operator
                        + ") val=" + r.Value + " target=" + r.Target);
                }
            }
        }
    }

    static int Get(ItemTemplate t, int id)
    {
        int v;
        if (t.Stats == null || !t.Stats.TryGetValue(id, out v))
        {
            return -1;
        }

        return v;
    }
}
