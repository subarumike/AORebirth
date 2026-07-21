namespace PerkActionExtract
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;

    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Nanos;
    using AORebirth.Core.Requirements;

    using MsgPack;

    internal static class NanoLookup
    {
        public static void Run(string root, params int[] ids)
        {
            string nanosDat = Path.Combine(root, "AORebirth", "Built", "Debug", "nanos.dat");
            Console.WriteLine("Loading " + nanosDat);
            NanoLoader.CacheAllNanos(nanosDat);
            Console.WriteLine("nanos=" + NanoLoader.NanoList.Count);

            var sb = new StringBuilder();
            foreach (int id in ids)
            {
                NanoFormula n;
                if (!NanoLoader.NanoList.TryGetValue(id, out n))
                {
                    sb.AppendLine("MISSING nano " + id);
                    continue;
                }

                sb.AppendLine("--- Nano " + id + " NCU=" + n.NanoStrain() + " events=" + n.Events.Count);
                try
                {
                    sb.AppendLine("  Attr8(duration)=" + n.getItemAttribute(8));
                    sb.AppendLine("  Attr88(?)=" + n.getItemAttribute(88));
                    sb.AppendLine("  Attr287(range)=" + n.getItemAttribute(287));
                }
                catch
                {
                }

                foreach (Event e in n.Events)
                {
                    foreach (Function f in e.Functions)
                    {
                        string args = string.Join(
                            ",",
                            f.Arguments.Values.Select(
                                o =>
                                {
                                    if (o.IsTypeOf(typeof(int)) == true)
                                    {
                                        return o.AsInt32().ToString();
                                    }

                                    if (o.IsTypeOf(typeof(string)) == true)
                                    {
                                        return o.AsStringUtf8();
                                    }

                                    return o.ToString();
                                }).ToArray());
                        int reqCount = f.Requirements == null ? -1 : f.Requirements.Count;
                        sb.AppendLine(
                            "  " + e.EventType + " Fn " + f.FunctionType + " target=" + f.Target
                            + " reqs=" + reqCount + " [" + args + "]");
                        if (f.Requirements != null)
                        {
                            foreach (Requirement req in f.Requirements)
                            {
                                sb.AppendLine(
                                    "    Req op=" + req.Operator + " stat=" + req.Statnumber
                                    + " val=" + req.Value + " child=" + req.ChildOperator
                                    + " tgt=" + req.Target);
                            }
                        }
                    }
                }
            }

            string outPath = Path.Combine(root, "tools-temp", "perk-action-nanos.txt");
            File.WriteAllText(outPath, sb.ToString());
            Console.WriteLine(sb.ToString());
            Console.WriteLine("Wrote " + outPath);
        }
    }
}
