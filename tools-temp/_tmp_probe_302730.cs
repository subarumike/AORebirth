using System;
using System.Linq;
using AORebirth.Core.Items;
using AORebirth.Enums;

class Program
{
    static void Main(string[] args)
    {
        string itemsDat = args.Length > 0
            ? args[0]
            : @"C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat";
        int n = ItemLoader.CacheAllItems(itemsDat);
        Console.WriteLine("loaded=" + n);
        ItemTemplate t;
        if (!ItemLoader.ItemList.TryGetValue(302730, out t))
        {
            Console.WriteLine("MISSING 302730");
            return;
        }

        Console.WriteLine("ID=" + t.ID);
        foreach (var a in t.Stats.OrderBy(x => x.Key))
        {
            Console.WriteLine("stat " + a.Key + "=" + a.Value);
        }

        foreach (var ev in t.Events)
        {
            Console.WriteLine("event type=" + ev.EventType);
            foreach (var f in ev.Functions)
            {
                int ft = f.FunctionType;
                if (ft == (int)FunctionType.BackMesh
                    || ft == (int)FunctionType.Texture
                    || ft == (int)FunctionType.HeadMesh
                    || ft == (int)FunctionType.Shouldermesh
                    || ft == (int)FunctionType.ChangeBodyMesh
                    || ft == (int)FunctionType.StrTexture)
                {
                    string argsText = string.Join(",", f.Arguments.Values.Select(x => x.ToString()));
                    Console.WriteLine("  fn type=" + ft + " (" + (FunctionType)ft + ") args=" + argsText);
                }
                else
                {
                    // also print all functions briefly for OnWear
                    string argsText = string.Join(",", f.Arguments.Values.Select(x => x.ToString()));
                    Console.WriteLine("  other fn type=" + ft + " args=" + argsText);
                }
            }
        }
    }
}
