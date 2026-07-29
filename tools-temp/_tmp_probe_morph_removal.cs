using System;
using System.Linq;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Nanos;
using AORebirth.Enums;
using MsgPack;

class Probe
{
    static void Main()
    {
        NanoLoader.CacheAllNanos(@"C:\Users\nermi\source\repos\AORebirth\AORebirth\Datafiles\nanos.dat");
        foreach (int id in new[] { 273292, 82835 })
        {
            NanoFormula nano;
            if (!NanoLoader.NanoList.TryGetValue(id, out nano))
            {
                Console.WriteLine(id + " missing");
                continue;
            }
            Console.WriteLine("=== " + id + " ===");
            foreach (Event ev in nano.Events)
            {
                Console.WriteLine("EventType=" + ev.EventType);
                if (ev.Functions == null) continue;
                foreach (Function f in ev.Functions)
                {
                    string args = "";
                    if (f.Arguments != null && f.Arguments.Values != null)
                        args = string.Join(",", f.Arguments.Values.Select(o => o.ToString()));
                    Console.WriteLine("  FT=" + f.FunctionType + "(" + ((FunctionType)f.FunctionType) + ") T=" + f.Target + " args=[" + args + "] reqs=" + f.Requirements.Count);
                    foreach (var r in f.Requirements)
                        Console.WriteLine("    req stat=" + r.Statnumber + " op=" + r.Operator + " val=" + r.Value);
                }
            }
        }
    }
}
