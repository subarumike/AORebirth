using System;
using System.Linq;
using AORebirth.Core.Actions;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Items;
using AORebirth.Core.Requirements;
using AORebirth.Enums;
using MsgPack;

class Probe
{
    static void Main()
    {
        ItemLoader.CacheAllItems(@"C:\Users\nermi\source\repos\AORebirth\AORebirth\Datafiles\items.dat");
        int[] ids = { 117322 };
        foreach (int id in ids)
        {
            DumpItem(id);
        }

        // Also find a few items that look like air/water/ground vehicles by name token if present.
        int shown = 0;
        foreach (var kv in ItemLoader.ItemList.OrderBy(x => x.Key))
        {
            ItemTemplate t = kv.Value;
            if (t.Stats == null)
            {
                continue;
            }

            int isVehicle = GetStat(t, 658);
            if (isVehicle == 1234567890 || isVehicle == 0)
            {
                continue;
            }

            Console.WriteLine(
                "VEHICLE id=" + kv.Key + " QL=" + t.Quality + " IsVehicle=" + isVehicle
                + " nameStat=" + GetStat(t, 0));
            DumpActions(t);
            shown++;
            if (shown >= 8)
            {
                break;
            }
        }
    }

    static void DumpItem(int id)
    {
        if (!ItemLoader.ItemList.ContainsKey(id))
        {
            Console.WriteLine(id + " MISSING");
            return;
        }

        ItemTemplate t = ItemLoader.ItemList[id];
        Console.WriteLine("==== " + id + " QL=" + t.Quality + " Rel=[" + string.Join(",", t.Relations) + "] ====");
        if (t.Stats != null)
        {
            foreach (var s in t.Stats.OrderBy(x => x.Key))
            {
                Console.WriteLine("  STAT " + s.Key + "=" + s.Value);
            }
        }

        DumpActions(t);

        if (t.Events == null)
        {
            return;
        }

        foreach (Event ev in t.Events)
        {
            Console.WriteLine("  EVENT " + ev.EventType);
            if (ev.Functions == null)
            {
                continue;
            }

            foreach (Function f in ev.Functions)
            {
                string args = "";
                if (f.Arguments != null && f.Arguments.Values != null)
                {
                    args = string.Join(",", f.Arguments.Values.Select(FormatArg));
                }

                Console.WriteLine(
                    "    FT=" + f.FunctionType + "(" + ((FunctionType)f.FunctionType)
                    + ") args=[" + args + "]" + FormatReqs(f.Requirements));
            }
        }
    }

    static void DumpActions(ItemTemplate t)
    {
        if (t.Actions == null)
        {
            return;
        }

        foreach (AOAction a in t.Actions)
        {
            Console.WriteLine("  ACTION " + a.ActionType + FormatReqs(a.Requirements));
        }
    }

    static string FormatReqs(System.Collections.Generic.List<Requirement> reqs)
    {
        if (reqs == null || reqs.Count == 0)
        {
            return "";
        }

        string s = "";
        foreach (Requirement r in reqs)
        {
            s += " [stat=" + r.Statnumber + " op=" + r.Operator + " val=" + r.Value + "]";
        }

        return s;
    }

    static int GetStat(ItemTemplate t, int id)
    {
        if (t.Stats == null || !t.Stats.ContainsKey(id))
        {
            return 1234567890;
        }

        return t.Stats[id];
    }

    static string FormatArg(MessagePackObject o)
    {
        if (o.IsTypeOf<string>() == true)
        {
            return "\"" + o.AsString() + "\"";
        }

        if (o.IsTypeOf<int>() == true)
        {
            return o.AsInt32().ToString();
        }

        if (o.IsTypeOf<long>() == true)
        {
            return o.AsInt64().ToString();
        }

        return o.ToString();
    }
}
