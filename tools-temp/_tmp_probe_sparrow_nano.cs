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
        NanoFormula nano;
        if (!NanoLoader.NanoList.TryGetValue(82835, out nano))
        {
            Console.WriteLine("nano 82835 missing");
            return;
        }
        Console.WriteLine("nano 82835 events=" + nano.Events.Count);
        foreach (Event ev in nano.Events)
        {
            Console.WriteLine("EventType=" + ev.EventType + " funcs=" + (ev.Functions == null ? 0 : ev.Functions.Count));
            if (ev.Functions == null) continue;
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
                        reqs += " [stat=" + r.Statnumber + " op=" + r.Operator + " val=" + r.Value + " child=" + r.ChildOperator + "]";
                    }
                }
                Console.WriteLine("  FT=" + f.FunctionType + "(" + ((FunctionType)f.FunctionType) + ") Target=" + f.Target + " args=[" + args + "]" + reqs);
            }
        }
    }

    static string FormatArg(MessagePackObject o)
    {
        if (o.IsTypeOf<string>() == true) return "\"" + o.AsString() + "\"";
        if (o.IsTypeOf<int>() == true) return o.AsInt32().ToString();
        if (o.IsTypeOf<long>() == true) return o.AsInt64().ToString();
        return o.ToString();
    }
}
