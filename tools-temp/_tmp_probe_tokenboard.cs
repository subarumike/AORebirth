using System;
using System.Linq;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Items;
using AORebirth.Enums;
using MsgPack;

class Probe
{
    static void Main()
    {
        ItemLoader.CacheAllItems(@"C:\Users\nermi\source\repos\AORebirth\AORebirth\Datafiles\items.dat");
        int[] ids = { 296363, 296364, 296370, 296371, 296378 };
        foreach (int id in ids)
        {
            if (!ItemLoader.ItemList.ContainsKey(id))
            {
                Console.WriteLine(id + " MISSING");
                continue;
            }

            ItemTemplate t = ItemLoader.ItemList[id];
            Console.WriteLine("==== " + id + " QL=" + t.Quality + " Rel=[" + string.Join(",", t.Relations) + "] ====");
            if (t.Events == null)
            {
                continue;
            }

            foreach (Event ev in t.Events)
            {
                if (ev.EventType != EventType.OnUse || ev.Functions == null)
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

                    string reqs = "";
                    if (f.Requirements != null)
                    {
                        foreach (var r in f.Requirements)
                        {
                            reqs += " [stat=" + r.Statnumber + " op=" + r.Operator + " val=" + r.Value + "]";
                        }
                    }

                    Console.WriteLine(
                        "  FT=" + f.FunctionType + "(" + ((FunctionType)f.FunctionType)
                        + ") args=[" + args + "]" + reqs);
                }
            }
        }
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
