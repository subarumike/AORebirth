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

    using MsgPack;

    /// <summary>
    /// Extracts AddPerkAction grants from items.dat via OnWear function 53182:
    /// args = [slotId(=10000+PacketID), hashString, flag, actionTemplateId]
    /// </summary>
    internal static class Program
    {
        private const int AddPerkActionFunction = 53182;

        private static int Main(string[] args)
        {
            string root = FindRepoRoot();
            string itemsDat = Path.Combine(root, "AORebirth", "Built", "Debug", "items.dat");
            string perksXml = Path.Combine(root, "AORebirth", "Server", "ZoneEngine", "XML Data", "Perks.xml");
            string outCsv = Path.Combine(root, "AORebirth", "Server", "ZoneEngine", "XML Data", "PerkActions.csv");
            string outTxt = Path.Combine(root, "tools-temp", "perk-actions-catalog.txt");

            Console.WriteLine("Loading " + itemsDat);
            ItemLoader.CacheAllItems(itemsDat);

            // Optional mode: scan OnUse function distribution across PerkActions.csv
            if (args != null && args.Length > 0 && string.Equals(args[0], "scan-onuse", StringComparison.OrdinalIgnoreCase))
            {
                ScanOnUse.Run(root);
                return 0;
            }

            if (args != null && args.Length > 0 && string.Equals(args[0], "nano-lookup", StringComparison.OrdinalIgnoreCase))
            {
                var ids = args.Skip(1).Select(int.Parse).ToArray();
                NanoLookup.Run(root, ids);
                return 0;
            }

            var perkRows = new List<PerkRow>();
            foreach (XElement el in XDocument.Load(perksXml).Descendants("Perk"))
            {
                perkRows.Add(
                    new PerkRow
                    {
                        PacketId = ParseInt((string)el.Attribute("PacketID")),
                        Aoid = ParseInt((string)el.Attribute("AOID")),
                        Name = (string)el.Attribute("Name") ?? string.Empty
                    });
            }

            var grants = new List<Grant>();
            var report = new StringBuilder();
            int missingTemplate = 0;
            int noGrant = 0;

            foreach (PerkRow row in perkRows)
            {
                ItemTemplate perk;
                if (!ItemLoader.ItemList.TryGetValue(row.Aoid, out perk))
                {
                    missingTemplate++;
                    continue;
                }

                Grant g = FindGrant(perk, row);
                if (g == null)
                {
                    noGrant++;
                    continue;
                }

                grants.Add(g);
                report.AppendLine(
                    "P" + g.PacketId + " AOID=" + g.Aoid + " action=" + g.ActionTemplateId + " hash=" + g.Hash
                    + " slot=" + g.SlotId + " " + g.Name);
            }

            // Write CSV for runtime catalog
            var csv = new StringBuilder();
            csv.AppendLine("PacketId,Aoid,ActionTemplateId,ActionHash,SlotId,Name");
            foreach (Grant g in grants.OrderBy(x => x.PacketId))
            {
                csv.AppendLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},\"{5}\"",
                        g.PacketId,
                        g.Aoid,
                        g.ActionTemplateId,
                        g.Hash,
                        g.SlotId,
                        g.Name.Replace("\"", "''")));
            }

            File.WriteAllText(outCsv, csv.ToString());
            File.WriteAllText(
                outTxt,
                "grants=" + grants.Count + " noGrant=" + noGrant + " missingTemplate=" + missingTemplate
                + Environment.NewLine + report);

            Console.WriteLine("grants=" + grants.Count + " noGrant=" + noGrant + " missingTemplate=" + missingTemplate);
            Console.WriteLine("Wrote " + outCsv);
            Console.WriteLine("Wrote " + outTxt);

            // Validate capture-known four
            Validate(grants, 250, 0x3796D, "CNRE");
            Validate(grants, 721, 0x3AE0E, "STOB");
            Validate(grants, 760, 0x37814, "DZWI");
            Validate(grants, 240, 0x3785D, "ELTE");
            return 0;
        }

        private static void Validate(List<Grant> grants, int packetId, int actionId, string hash)
        {
            Grant g = grants.FirstOrDefault(x => x.PacketId == packetId);
            if (g == null)
            {
                Console.WriteLine("VALIDATE FAIL missing P" + packetId);
                return;
            }

            bool ok = g.ActionTemplateId == actionId && g.Hash == hash;
            Console.WriteLine(
                (ok ? "VALIDATE OK" : "VALIDATE FAIL") + " P" + packetId + " got action=" + g.ActionTemplateId
                + " hash=" + g.Hash + " expected " + actionId + "/" + hash);
        }

        private static Grant FindGrant(ItemTemplate perk, PerkRow row)
        {
            if (perk.Events == null)
            {
                return null;
            }

            foreach (Event ev in perk.Events)
            {
                foreach (Function f in ev.Functions)
                {
                    if (f.FunctionType != AddPerkActionFunction || f.Arguments == null
                        || f.Arguments.Values == null || f.Arguments.Values.Count < 4)
                    {
                        continue;
                    }

                    int slotId = AsInt(f.Arguments.Values[0]);
                    string hash = AsString(f.Arguments.Values[1]);
                    int actionTemplateId = AsInt(f.Arguments.Values[3]);
                    if (string.IsNullOrEmpty(hash) || actionTemplateId <= 0)
                    {
                        continue;
                    }

                    // Prefer packet id from Perks.xml; slot may confirm 10000+PacketID
                    int packetId = row.PacketId;
                    if (slotId >= 10000)
                    {
                        int fromSlot = slotId - 10000;
                        if (fromSlot != packetId)
                        {
                            // Keep XML packet id as authority; note mismatch in name field suffix
                            row.Name = row.Name + " [slotPacket=" + fromSlot + "]";
                        }
                    }

                    return new Grant
                    {
                        PacketId = packetId,
                        Aoid = row.Aoid,
                        Name = row.Name,
                        SlotId = slotId > 0 ? slotId : (10000 + packetId),
                        Hash = hash.Length == 4 ? hash : Ascii(AsInt(f.Arguments.Values[1])),
                        ActionTemplateId = actionTemplateId
                    };
                }
            }

            return null;
        }

        private static int AsInt(MessagePackObject o)
        {
            if (o.IsTypeOf(typeof(int)) == true)
            {
                return o.AsInt32();
            }

            if (o.IsTypeOf(typeof(string)) == true)
            {
                int v;
                if (int.TryParse(o.AsStringUtf8(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                {
                    return v;
                }
            }

            return 0;
        }

        private static string AsString(MessagePackObject o)
        {
            if (o.IsTypeOf(typeof(string)) == true)
            {
                return o.AsStringUtf8();
            }

            if (o.IsTypeOf(typeof(int)) == true)
            {
                return Ascii(o.AsInt32());
            }

            return string.Empty;
        }

        private static int ParseInt(string s)
        {
            int v;
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
            return v;
        }

        private static string Ascii(int v)
        {
            try
            {
                char c0 = (char)((v >> 24) & 0xFF);
                char c1 = (char)((v >> 16) & 0xFF);
                char c2 = (char)((v >> 8) & 0xFF);
                char c3 = (char)(v & 0xFF);
                string s = string.Concat(c0, c1, c2, c3);
                if (s.All(ch => ch >= 32 && ch <= 126))
                {
                    return s;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string FindRepoRoot()
        {
            string dir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "AGENTS.md"))
                    && Directory.Exists(Path.Combine(dir, "AORebirth")))
                {
                    return dir;
                }

                dir = Path.GetDirectoryName(dir);
            }

            return Directory.GetCurrentDirectory();
        }

        private sealed class PerkRow
        {
            public int PacketId;

            public int Aoid;

            public string Name;
        }

        private sealed class Grant
        {
            public int PacketId;

            public int Aoid;

            public int ActionTemplateId;

            public string Hash;

            public int SlotId;

            public string Name;
        }
    }
}
