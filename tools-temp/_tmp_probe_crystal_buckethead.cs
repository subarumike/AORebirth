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
        foreach (int id in new[] { 300440, 300444, 300636, 300635 })
        {
            if (!ItemLoader.ItemList.ContainsKey(id))
            {
                Console.WriteLine("item " + id + " MISSING");
                continue;
            }

            var t = ItemLoader.ItemList[id];
            Console.WriteLine("=== item " + id + " events=" + t.Events.Count);
            foreach (Event ev in t.Events)
            {
                Console.WriteLine(" EventType=" + ev.EventType);
                if (ev.Functions == null) continue;
                foreach (Function f in ev.Functions)
                {
                    string a = "";
                    if (f.Arguments != null && f.Arguments.Values != null)
                    {
                        a = string.Join(",", f.Arguments.Values.Select(o =>
                        {
                            if (o.IsTypeOf<string>() == true) return "\"" + o.AsString() + "\"";
                            if (o.IsTypeOf<int>() == true) return o.AsInt32().ToString();
                            return o.ToString();
                        }));
                    }
                    Console.WriteLine("  FT=" + f.FunctionType + "(" + ((FunctionType)f.FunctionType) + ") args=[" + a + "]");
                }
            }
        }
    }
}
