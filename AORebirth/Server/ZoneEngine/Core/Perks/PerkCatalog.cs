namespace ZoneEngine.Core.Perks
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Xml.Linq;

    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using MsgPack;

    /// <summary>
    /// Loads Perks.xml plus Perk Action grants.
    /// Grants come from PerkActions.csv (extracted from items.dat OnWear FunctionType.AddAction=53182)
    /// and can also be resolved live from ItemLoader when items.dat is loaded.
    /// Capture 20260715-194155 validates CNRE/STOB/DZWI/ELTE mappings.
    /// </summary>
    public static class PerkCatalog
    {
        private static readonly object Sync = new object();

        private static Dictionary<int, PerkDefinition> byPacketId;

        private static Dictionary<int, PerkDefinition> byActionHash;

        public static bool TryGet(int packetId, out PerkDefinition definition)
        {
            EnsureLoaded();
            if (!byPacketId.TryGetValue(packetId, out definition))
            {
                return false;
            }

            if (!definition.GrantsPerkAction)
            {
                TryResolveActionFromItem(definition);
            }

            return true;
        }

        /// <summary>
        /// Capture UsePerk Parameter2 is the 4-char action hash (e.g. CNRE/QUBS).
        /// Preferred when AddAction slotId != 10000+PacketID (Blunt Mastery 2 slot 10320).
        /// </summary>
        public static bool TryGetByActionHash(int actionHash, out PerkDefinition definition)
        {
            EnsureLoaded();
            if (byActionHash != null && byActionHash.TryGetValue(actionHash, out definition))
            {
                return true;
            }

            definition = null;
            return false;
        }

        public static IEnumerable<PerkDefinition> All
        {
            get
            {
                EnsureLoaded();
                return byPacketId.Values;
            }
        }

        private static void EnsureLoaded()
        {
            if (byPacketId != null)
            {
                return;
            }

            lock (Sync)
            {
                if (byPacketId != null)
                {
                    return;
                }

                byPacketId = new Dictionary<int, PerkDefinition>();
                byActionHash = new Dictionary<int, PerkDefinition>();
                LoadPerksXml();
                LoadPerkActionsCsv();
            }
        }

        private static void LoadPerksXml()
        {
            string path = FindDataFile("Perks.xml");
            if (path == null || !File.Exists(path))
            {
                return;
            }

            XDocument doc = XDocument.Load(path);
            foreach (XElement el in doc.Descendants("Perk"))
            {
                string packetRaw = (string)el.Attribute("PacketID");
                string aoidRaw = (string)el.Attribute("AOID");
                string name = (string)el.Attribute("Name") ?? string.Empty;
                if (string.IsNullOrEmpty(packetRaw))
                {
                    continue;
                }

                int packetId;
                if (!int.TryParse(packetRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out packetId))
                {
                    continue;
                }

                int aoid = 0;
                if (!string.IsNullOrEmpty(aoidRaw))
                {
                    int.TryParse(aoidRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out aoid);
                }

                byPacketId[packetId] = new PerkDefinition
                {
                    PacketId = packetId,
                    Aoid = aoid,
                    Name = name
                };
            }
        }

        private static void LoadPerkActionsCsv()
        {
            string path = FindDataFile("PerkActions.csv");
            if (path == null || !File.Exists(path))
            {
                return;
            }

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (i == 0 && line.StartsWith("PacketId", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // PacketId,Aoid,ActionTemplateId,ActionHash,SlotId,Name
                string[] parts = SplitCsv(line);
                if (parts.Length < 5)
                {
                    continue;
                }

                int packetId;
                int aoid;
                int actionTemplateId;
                int slotId;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out packetId)
                    || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out aoid)
                    || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out actionTemplateId)
                    || !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out slotId))
                {
                    continue;
                }

                string hashText = parts[3];
                int actionHash = Hash(hashText);

                PerkDefinition def;
                if (!byPacketId.TryGetValue(packetId, out def))
                {
                    def = new PerkDefinition { PacketId = packetId, Aoid = aoid, Name = parts.Length > 5 ? Unquote(parts[5]) : ("Perk " + packetId) };
                    byPacketId[packetId] = def;
                }

                def.ActionTemplateId = actionTemplateId;
                def.ActionHash = actionHash;
                def.ActionSlotIdOverride = slotId;
                if (def.Aoid == 0)
                {
                    def.Aoid = aoid;
                }

                byActionHash[actionHash] = def;
            }
        }

        private static void TryResolveActionFromItem(PerkDefinition def)
        {
            if (def == null || def.Aoid <= 0 || ItemLoader.ItemList == null || ItemLoader.ItemList.Count == 0)
            {
                return;
            }

            ItemTemplate perk;
            if (!ItemLoader.ItemList.TryGetValue(def.Aoid, out perk) || perk.Events == null)
            {
                return;
            }

            foreach (Event ev in perk.Events)
            {
                foreach (Function f in ev.Functions)
                {
                    if (f.FunctionType != (int)FunctionType.AddAction || f.Arguments == null
                        || f.Arguments.Values == null || f.Arguments.Values.Count < 4)
                    {
                        continue;
                    }

                    string hashText = AsString(f.Arguments.Values[1]);
                    int actionTemplateId = AsInt(f.Arguments.Values[3]);
                    if (string.IsNullOrEmpty(hashText) || hashText.Length != 4 || actionTemplateId <= 0)
                    {
                        continue;
                    }

                    int slotId = AsInt(f.Arguments.Values[0]);
                    def.ActionTemplateId = actionTemplateId;
                    def.ActionHash = Hash(hashText);
                    if (slotId > 0)
                    {
                        def.ActionSlotIdOverride = slotId;
                    }

                    byActionHash[def.ActionHash.Value] = def;
                    return;
                }
            }
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

            return string.Empty;
        }

        private static string[] SplitCsv(string line)
        {
            var parts = new List<string>();
            bool inQuotes = false;
            var cur = new System.Text.StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    parts.Add(cur.ToString());
                    cur.Clear();
                    continue;
                }

                cur.Append(c);
            }

            parts.Add(cur.ToString());
            return parts.ToArray();
        }

        private static string Unquote(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            return s.Trim().Trim('"').Replace("''", "\"");
        }

        private static string FindDataFile(string fileName)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "XML Data", fileName),
                Path.Combine(baseDir, fileName),
                Path.Combine(Directory.GetCurrentDirectory(), "XML Data", fileName),
            };
            foreach (string c in candidates)
            {
                if (File.Exists(c))
                {
                    return c;
                }
            }

            return null;
        }

        private static int Hash(string fourChars)
        {
            if (fourChars == null || fourChars.Length != 4)
            {
                throw new ArgumentException("Action hash must be 4 ASCII chars.", "fourChars");
            }

            return (fourChars[0] << 24) | (fourChars[1] << 16) | (fourChars[2] << 8) | fourChars[3];
        }
    }
}
