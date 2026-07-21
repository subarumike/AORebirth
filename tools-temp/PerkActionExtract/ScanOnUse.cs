// Temporary helper compiled into PerkActionExtract - scan OnUse fn distribution
namespace PerkActionExtract
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;

    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using MsgPack;

    internal static class ScanOnUse
    {
        public static void Run(string root)
        {
            string itemsDat = Path.Combine(root, "AORebirth", "Built", "Debug", "items.dat");
            string csv = Path.Combine(root, "AORebirth", "Server", "ZoneEngine", "XML Data", "PerkActions.csv");
            string outPath = Path.Combine(root, "tools-temp", "perk-action-onuse-scan.txt");

            if (ItemLoader.ItemList == null || ItemLoader.ItemList.Count == 0)
            {
                ItemLoader.CacheAllItems(itemsDat);
            }

            var fnCounts = new Dictionary<int, int>();
            var samples = new StringBuilder();
            int actions = 0;
            int withOnUse = 0;

            foreach (string line in File.ReadAllLines(csv).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] p = line.Split(',');
                if (p.Length < 3)
                {
                    continue;
                }

                int actionId;
                if (!int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out actionId))
                {
                    continue;
                }

                actions++;
                ItemTemplate t;
                if (!ItemLoader.ItemList.TryGetValue(actionId, out t) || t.Events == null)
                {
                    continue;
                }

                bool any = false;
                foreach (Event ev in t.Events)
                {
                    if (ev.EventType != EventType.OnUse && ev.EventType != EventType.OnFailure)
                    {
                        continue;
                    }

                    any = true;
                    foreach (Function f in ev.Functions)
                    {
                        int fn = f.FunctionType;
                        if (!fnCounts.ContainsKey(fn))
                        {
                            fnCounts[fn] = 0;
                        }

                        fnCounts[fn]++;
                    }
                }

                if (any)
                {
                    withOnUse++;
                }
            }

            samples.AppendLine("actionTemplates=" + actions + " withOnUseOrFailure=" + withOnUse);
            samples.AppendLine("=== Function frequency on perk action OnUse/OnFailure ===");
            foreach (KeyValuePair<int, int> kv in fnCounts.OrderByDescending(x => x.Value))
            {
                string name = Enum.IsDefined(typeof(FunctionType), kv.Key)
                                  ? ((FunctionType)kv.Key).ToString()
                                  : "?";
                samples.AppendLine(kv.Key + " " + name + " count=" + kv.Value);
            }

            // Sample dumps for unknown 53240 targets
            samples.AppendLine();
            samples.AppendLine("=== Sample item 227683 (Channel Rage tier buff?) ===");
            Dump(samples, 227683);
            samples.AppendLine("=== Sample item 241086 (Spirit of Blessing CastNano) ===");
            Dump(samples, 241086);
            samples.AppendLine("=== Sample nano if in items ===");
            samples.AppendLine("=== Full OnUse dump Channel Rage action 227693 ===");
            Dump(samples, 227693);
            samples.AppendLine("=== Full OnUse dump Quick Bash / Blunt Mastery 2 action 226519 ===");
            Dump(samples, 226519);
            samples.AppendLine("=== Soothing Spirits 2 action 241166 ===");
            Dump(samples, 241166);
            samples.AppendLine("=== Spirit Phylactery 1 action 225439 ===");
            Dump(samples, 225439);
            samples.AppendLine("=== Channel Rage action 227693 ===");
            Dump(samples, 227693);
            samples.AppendLine("=== Channel Rage tier wrappers 227683-227692 ===");
            for (int id = 227683; id <= 227692; id++)
            {
                Dump(samples, id);
            }

            File.WriteAllText(outPath, samples.ToString());
            Console.WriteLine(samples.ToString());
            Console.WriteLine("Wrote " + outPath);
        }

        private static void Dump(StringBuilder sb, int id)
        {
            ItemTemplate t;
            if (!ItemLoader.ItemList.TryGetValue(id, out t))
            {
                sb.AppendLine("MISSING " + id);
                return;
            }

            sb.AppendLine("--- " + id + " type=" + t.ItemType + " flags=" + t.Flags);
            foreach (KeyValuePair<int, int> st in t.Stats.OrderBy(x => x.Key))
            {
                sb.AppendLine("  Stat " + st.Key + "=" + st.Value);
            }

            foreach (Event e in t.Events)
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
                                    return o.AsInt32().ToString(CultureInfo.InvariantCulture);
                                }

                                if (o.IsTypeOf(typeof(string)) == true)
                                {
                                    return o.AsStringUtf8();
                                }

                                return o.ToString();
                            }).ToArray());
                    sb.AppendLine(
                        "  " + e.EventType + " Fn " + f.FunctionType + " target=" + f.Target + " [" + args
                        + "] reqs=" + f.Requirements.Count);
                    foreach (var r in f.Requirements)
                    {
                        sb.AppendLine(
                            "    Req target=" + r.Target + " stat=" + r.Statnumber + " op=" + r.Operator
                            + " val=" + r.Value + " child=" + r.ChildOperator);
                    }
                }
            }
        }
    }
}
