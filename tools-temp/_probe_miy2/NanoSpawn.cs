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
        int[] ids = new[]
        {
            43325, 45691, 45692, 45664, 45665, 45671, 45672, 45675, 45676, 45680,
            45681, 45682, 45686, 45693, 45694, 45697, 45698, 45699, 45700, 45701,
            45702, 45703, 45705, 45706, 45712, 45713, 45714, 45717, 45718, 45719,
            45720, 45723, 45725, 45726, 45730, 45731, 45732, 45733, 45736, 45737
        };
        foreach (int id in ids.OrderBy(x => x))
        {
            NanoFormula nano;
            if (!NanoLoader.NanoList.TryGetValue(id, out nano))
            {
                Console.WriteLine("missing " + id);
                continue;
            }

            string spawn = "";
            if (nano.Events != null)
            {
                foreach (Event ev in nano.Events)
                {
                    if (ev.Functions == null)
                    {
                        continue;
                    }

                    foreach (Function f in ev.Functions)
                    {
                        if (f.FunctionType != (int)FunctionType.SpawnItem)
                        {
                            continue;
                        }

                        if (f.Arguments == null || f.Arguments.Values == null
                            || f.Arguments.Values.Count < 2)
                        {
                            continue;
                        }

                        spawn = f.Arguments.Values[0].AsString() + ":"
                            + f.Arguments.Values[1].AsInt32();
                    }
                }
            }

            Console.WriteLine(id + "\t" + spawn);
        }
    }
}
