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
            ItemTemplate t = ItemLoader.ItemList[244738];
            var onUse = t.Events.First(e => e.EventType == EventType.OnUse);
            var fn = onUse.Functions.First(f => f.FunctionType == (int)FunctionType.Teleport);
            Console.WriteLine("reqs=" + fn.Requirements.Count);
            foreach (var r in fn.Requirements)
            {
                Console.WriteLine("  target=" + r.Target + " stat=" + r.Statnumber + " op=" + r.Operator + " val=" + r.Value + " child=" + r.ChildOperator);
            }
            Console.WriteLine("args count=" + fn.Arguments.Values.Count);
            for (int i = 0; i < fn.Arguments.Values.Count; i++)
            {
                var a = fn.Arguments.Values[i];
                Console.WriteLine("  [" + i + "] Is=" + a.IsTypeOf() + " raw=" + a + " AsInt32=" + (a.IsTypeOf(MsgPack.MessagePackObjectType.Integer) ? a.AsInt32().ToString() : "n/a"));
                try { Console.WriteLine("    ToObject=" + a.ToObject() + " type=" + (a.ToObject() == null ? "null" : a.ToObject().GetType().Name)); } catch (Exception ex) { Console.WriteLine("    ToObject fail " + ex.Message); }
            }
        }
    }
}
