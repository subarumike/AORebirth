using System;
using System.IO;
using System.Linq;
using AORebirth.Core.Nanos;

class ListSummonPetNanos
{
    static void Main()
    {
        string nanoPath = Path.GetFullPath(args.Length > 0 ? args[0] : @"AORebirth\Built\Debug\nanos.dat");
        NanoLoader.CacheAllNanos(nanoPath);
        int count = 0;
        foreach (NanoFormula nano in NanoLoader.NanoList.Values.OrderBy(x => x.ID))
        {
            if (nano.Events == null)
            {
                continue;
            }

            foreach (var ev in nano.Events)
            {
                foreach (var fn in ev.Functions)
                {
                    if (fn.FunctionType != 53167 && fn.FunctionType != 53181)
                    {
                        continue;
                    }

                    string argText = string.Join(",", fn.Arguments.Values.Select(v => v.ToString()));
                    Console.WriteLine("nano={0} event={1} fn={2} args={3}", nano.ID, ev.EventType, fn.FunctionType, argText);
                    count++;
                }
            }
        }

        Console.WriteLine("total={0}", count);
    }
}
