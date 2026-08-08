using System;
using System.Linq;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Nanos;
using AORebirth.Enums;

class Program
{
    static void Main(string[] args)
    {
        NanoLoader.CacheAllNanos("nanos.dat");
        int[] ids = new[] { 154913, 154914, 154915, 154916, 284689 };
        foreach (int id in ids)
        {
            NanoFormula nano;
            if (!NanoLoader.NanoList.TryGetValue(id, out nano))
            {
                Console.WriteLine("missing " + id);
                continue;
            }

            Console.WriteLine("==== " + id + " atr8=" + nano.getItemAttribute(8)
                + " strain=" + nano.NanoStrain() + " ncu=" + nano.NCUCost() + " ====");
            if (nano.Events == null)
            {
                continue;
            }

            foreach (Event ev in nano.Events)
            {
                if (ev.Functions == null)
                {
                    continue;
                }

                foreach (Function f in ev.Functions)
                {
                    string ft;
                    try
                    {
                        ft = ((FunctionType)f.FunctionType).ToString();
                    }
                    catch
                    {
                        ft = f.FunctionType.ToString();
                    }

                    Console.WriteLine(
                        "  " + ev.EventType + " fn=" + f.FunctionType + "(" + ft + ") target="
                        + f.Target);
                }
            }
        }
    }
}
