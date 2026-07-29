using System;
using System.Linq;
using AORebirth.Core.Items;
using AORebirth.Enums;

namespace TmpCheck
{
    class Program
    {
        static void Main()
        {
            Environment.CurrentDirectory = @"C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug";
            ItemLoader.CacheAllItems("items.dat");
            foreach (int id in new[] { 222955, 214789, 224051 })
            {
                ItemTemplate t;
                if (!ItemLoader.ItemList.TryGetValue(id, out t)) { Console.WriteLine(id + " missing"); continue; }
                Console.WriteLine("=== " + id + " ===");
                foreach (var ev in t.Events)
                {
                    Console.WriteLine(" event=" + ev.EventType);
                    foreach (var f in ev.Functions)
                    {
                        Console.WriteLine("  fn=" + f.FunctionType + " (" + (FunctionType)f.FunctionType + ") target=" + f.Target);
                    }
                }
            }
        }
    }
}
