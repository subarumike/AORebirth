using System;
using System.Linq;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Nanos;
using AORebirth.Enums;
using MsgPack;

class Probe
{
    static void Main(string[] args)
    {
        NanoLoader.CacheAllNanos(@"C:\Users\nermi\source\repos\AORebirth\AORebirth\Datafiles\nanos.dat");
        int[] ids = args.Length > 0
            ? args.Select(int.Parse).ToArray()
            : new[] { 300439, 300440 };
        foreach (int id in ids)
        {
            NanoFormula nano;
            if (!NanoLoader.NanoList.TryGetValue(id, out nano))
            {
                Console.WriteLine("nano " + id + " MISSING");
                continue;
            }

            Console.WriteLine(
                "=== nano " + id
                + " duration=" + nano.getItemAttribute(8)
                + " cost=" + nano.getItemAttribute(407)
                + " events=" + nano.Events.Count);
            foreach (Event ev in nano.Events)
            {
                Console.WriteLine(" EventType=" + ev.EventType + " funcs=" + (ev.Functions == null ? 0 : ev.Functions.Count));
                if (ev.Functions == null)
                {
                    continue;
                }

                foreach (Function f in ev.Functions)
                {
                    string a = "";
                    if (f.Arguments != null && f.Arguments.Values != null)
                    {
                        a = string.Join(",", f.Arguments.Values.Select(FormatArg));
                    }

                    Console.WriteLine(
                        "  FT=" + f.FunctionType + "(" + ((FunctionType)f.FunctionType)
                        + ") Target=" + f.Target + " args=[" + a + "]");
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
