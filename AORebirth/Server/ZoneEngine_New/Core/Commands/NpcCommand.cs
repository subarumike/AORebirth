namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.GameData;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Playfield;

    public sealed class NpcCommand : IGmCommand
    {
        private readonly IGameData _gameData;
        private readonly IItemTemplateCatalog _items;

        public NpcCommand(IGameData gameData, IItemTemplateCatalog items)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(items);
            _gameData = gameData;
            _items = items;
        }

        public string Name => "npc";

        public int RequiredGmLevel => 1;

        public string Usage => ".npc source|template|loot|equipment|position";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Args.Length < 1)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
                return;
            }

            if (!TryResolveNpc(context, out NpcCharacter npc))
                return;

            string verb = context.Args[0];
            if (string.Equals(verb, "source", StringComparison.OrdinalIgnoreCase))
            {
                GmCommandFeedback.SendLines(context.Session, context.Player, DumpSource(npc));
                return;
            }

            if (string.Equals(verb, "template", StringComparison.OrdinalIgnoreCase))
            {
                GmCommandFeedback.SendLines(context.Session, context.Player, DumpTemplate(npc));
                return;
            }

            if (string.Equals(verb, "loot", StringComparison.OrdinalIgnoreCase))
            {
                GmCommandFeedback.SendLines(context.Session, context.Player, DumpLoot(npc));
                return;
            }

            if (string.Equals(verb, "equipment", StringComparison.OrdinalIgnoreCase))
            {
                GmCommandFeedback.SendLines(context.Session, context.Player, DumpEquipment(npc));
                return;
            }

            if (string.Equals(verb, "position", StringComparison.OrdinalIgnoreCase))
            {
                GmCommandFeedback.SendLines(context.Session, context.Player, DumpPosition(context.Player, npc));
                return;
            }

            GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
        }

        static bool TryResolveNpc(GmCommandContext context, out NpcCharacter npc)
        {
            npc = null!;
            Playfield? playfield = context.Player.Playfield;
            if (playfield == null)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Not on a playfield.");
                return false;
            }

            if (context.Player.Target == Identity.None)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "No target.");
                return false;
            }

            DynelRegistry registry = playfield.GetRequiredService<DynelRegistry>();
            if (!registry.TryGet(context.Player.Target, out Dynel? dynel) || dynel is not NpcCharacter target)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Target is not an NPC.");
                return false;
            }

            npc = target;
            return true;
        }

        static List<string> DumpPosition(Character player, NpcCharacter npc)
        {
            Vector3 pos = npc.Position;
            Vector3 you = player.Position;
            double dx = pos.x - you.x;
            double dy = pos.y - you.y;
            double dz = pos.z - you.z;
            double dist = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            double planar = Math.Sqrt((dx * dx) + (dz * dz));
            int playfieldId = npc.Playfield?.Identity.Instance ?? 0;

            return new List<string>
            {
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Server position: {0} id={1} pf={2} ({3:F3},{4:F3},{5:F3})",
                    npc.Name ?? string.Empty,
                    npc.Identity.Instance,
                    playfieldId,
                    pos.xf,
                    pos.yf,
                    pos.zf),
                string.Format(
                    CultureInfo.InvariantCulture,
                    "You: ({0:F3},{1:F3},{2:F3}) delta=({3:F3},{4:F3},{5:F3}) dist={6:F3} xz={7:F3}",
                    you.xf,
                    you.yf,
                    you.zf,
                    (float)dx,
                    (float)dy,
                    (float)dz,
                    (float)dist,
                    (float)planar)
            };
        }

        List<string> DumpSource(NpcCharacter npc)
        {
            List<string> lines = new();
            MobTemplate? template = npc.MobTemplate;
            Vector3 pos = npc.Position;
            lines.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Source: {0} id={1} spawnSource={2} hash={3} level={4} pos=({5:F2},{6:F2},{7:F2}) cell={8}",
                    npc.Name ?? string.Empty,
                    npc.Identity.Instance,
                    npc.SpawnSource,
                    template?.Hash ?? string.Empty,
                    npc.Stats.GetOrZero(CharacterStat.Level),
                    pos.xf,
                    pos.yf,
                    pos.zf,
                    npc.Cell != null ? npc.Cell.Id.ToString(CultureInfo.InvariantCulture) : "-"));

            Playfield? playfield = npc.Playfield;
            if (playfield == null)
            {
                lines.Add("no playfield");
                return lines;
            }

            HashSpawnSystem hashSpawns = playfield.GetRequiredService<HashSpawnSystem>();
            if (!hashSpawns.TryGetSpawnPoint(npc, out HashSpawnPoint point))
            {
                lines.Add("no hash-spawn point");
                return lines;
            }

            PlayfieldSpawnEntry entry = point.Source;
            lines.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "  entry hash={0} hashText={1} manifest={2} district={3} cell={4}",
                    entry.Hash,
                    entry.HashText ?? string.Empty,
                    entry.ManifestHash,
                    entry.DistrictIndex,
                    point.CellId));
            if (!string.Equals(point.HashText, entry.HashText, StringComparison.Ordinal))
            {
                lines.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "  runtimeHash={0} (fallback)",
                        point.HashText));
            }

            lines.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "  levels={0}-{1} respawnTime={2}s respawnChance={3}% state={4} nextSpawn={5:o}",
                    point.MinLevel,
                    point.MaxLevel,
                    point.RespawnTimeSeconds,
                    point.RespawnChance,
                    point.State,
                    point.NextSpawnTime));
            lines.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "  flags={0} nativeFlags={1} moreFlags={2} assistRadius={3} angle={4} angleW={5}",
                    entry.Flags,
                    entry.NativeFlags,
                    entry.MoreFlags,
                    entry.AssistanceRadius,
                    entry.Angle,
                    entry.AngleW));

            for (int i = 0; i < point.Sites.Length; i++)
            {
                SpawnSite site = point.Sites[i];
                Quaternion heading = site.Heading;
                lines.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "  site[{0}] pos=({1:F2},{2:F2},{3:F2}) radius={4:F2} heading=({5:F3},{6:F3},{7:F3},{8:F3})",
                        i,
                        site.Centre.xf,
                        site.Centre.yf,
                        site.Centre.zf,
                        site.Radius,
                        heading.xf,
                        heading.yf,
                        heading.zf,
                        heading.wf));
            }

            PlayfieldHashSpawnExtensionBlock? extensions = entry.Extensions;
            if (extensions != null)
            {
                lines.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "  ext field0={0} field1={1} field2={2} trailing={3}",
                        extensions.Field0,
                        extensions.Field1,
                        extensions.Field2,
                        extensions.TrailingUnknown));
                PlayfieldHashSpawnExtensionEvent[]? events = extensions.Events;
                if (events != null)
                {
                    for (int i = 0; i < events.Length; i++)
                    {
                        PlayfieldHashSpawnExtensionEvent ev = events[i];
                        if (ev == null)
                            continue;
                        lines.Add(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "  ext.event[{0}] {1} u1={2} u2={3}",
                                i,
                                ev.Name ?? string.Empty,
                                ev.Unknown1,
                                ev.Unknown2));
                    }
                }
            }

            return lines;
        }

        List<string> DumpTemplate(NpcCharacter npc)
        {
            List<string> lines = new();
            MobTemplate? template = npc.MobTemplate;
            if (template == null)
            {
                lines.Add("No mob template on target.");
                return lines;
            }

            lines.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Template: {0} hash={1} templateId={2} knuBot={3} hasHeadMesh={4} attackable={5} levels={6}-{7}",
                    template.Name,
                    template.Hash,
                    template.TemplateId,
                    template.KnuBotId,
                    template.HasHeadMesh,
                    template.Attackable,
                    template.MinLevel,
                    template.MaxLevel));

            foreach (KeyValuePair<int, int> entry in template.Stats)
            {
                string statName = Enum.IsDefined(typeof(CharacterStat), entry.Key)
                    ? ((CharacterStat)entry.Key).ToString()
                    : entry.Key.ToString(CultureInfo.InvariantCulture);
                lines.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "  stat {0} ({1}) = {2}",
                        statName,
                        entry.Key,
                        entry.Value));
            }

            AppendIdLists(lines, "equipment", template.Equipment);

            Dictionary<int, int> textures = template.Textures;
            if (textures == null || textures.Count == 0)
            {
                lines.Add("  textures (empty)");
            }
            else
            {
                foreach (KeyValuePair<int, int> texture in textures)
                {
                    lines.Add(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "  texture place={0} id={1}",
                            texture.Key,
                            texture.Value));
                }
            }

            List<MobItemTableEntry> itemTable = template.ItemTable;
            if (itemTable == null || itemTable.Count == 0)
            {
                lines.Add("  itemTable (empty)");
                return lines;
            }

            for (int i = 0; i < itemTable.Count; i++)
            {
                MobItemTableEntry table = itemTable[i];
                if (table == null)
                    continue;
                lines.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "  itemTable[{0}] hash={1} repeats={2} chance={3} levelMod={4}",
                        i,
                        table.Hash,
                        table.Repeats,
                        table.Chance,
                        table.LevelMod));
            }

            return lines;
        }

        List<string> DumpEquipment(NpcCharacter npc)
        {
            List<string> lines = new();
            Container equipment = npc.Equipment;
            lines.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Equipment: {0} id={1} items={2}/{3}",
                    npc.Name ?? string.Empty,
                    npc.Identity.Instance,
                    equipment.Content.Count,
                    equipment.Capacity));

            if (equipment.Content.Count == 0)
            {
                lines.Add("  (empty)");
                return lines;
            }

            int last = equipment.Offset + equipment.Capacity;
            for (int slot = equipment.Offset; slot < last; slot++)
            {
                if (!equipment.Content.TryGetValue(slot, out Item? item) || item == null)
                    continue;

                int itemClass = item.GetStat(CharacterStat.ItemClass);
                string className = Enum.IsDefined(typeof(ItemClass), itemClass)
                    ? ((ItemClass)itemClass).ToString()
                    : itemClass.ToString(CultureInfo.InvariantCulture);
                string line = string.Format(
                    CultureInfo.InvariantCulture,
                    "  slot[{0}] {1} ql={2} class={3}",
                    slot,
                    FormatLiveItem(item),
                    item.Quality,
                    className);
                if (NpcCharacter.TryFindEquipMonsterWeaponHash(item, out string weaponHash))
                {
                    line = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} hash={1}{2}",
                        line,
                        weaponHash,
                        _gameData.TryGetMonsterWeapon(weaponHash, out _)
                            ? string.Empty
                            : " (unresolved)");
                }

                lines.Add(line);
            }

            return lines;
        }

        /// <summary>
        /// Popup link(s) listing every item each loot table entry can drop, by name.
        /// Categories are expanded to all reachable leaf instances.
        /// </summary>
        IReadOnlyList<string> DumpLoot(NpcCharacter npc)
        {
            MobTemplate? template = npc.MobTemplate;
            if (template == null)
                return ["No mob template on target."];

            string title = string.IsNullOrWhiteSpace(template.Name) ? "Loot" : template.Name + " Loot";
            List<MobItemTableEntry> itemTable = template.ItemTable;
            if (itemTable == null || itemTable.Count == 0)
                return [GetStatsAomlBuilder.BuildLink("(no item table)", title)];

            var rows = new List<string>();
            var instances = new List<HashInstance>();
            int itemCount = 0;
            foreach (MobItemTableEntry entry in itemTable)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Hash))
                    continue;

                if (rows.Count > 0)
                    rows.Add(string.Empty);
                rows.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Chance {0}% x{1} levelMod={2}",
                        entry.Chance,
                        entry.Repeats,
                        entry.LevelMod));

                instances.Clear();
                _gameData.CollectHashLeafInstances(entry.Hash, instances);
                var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < instances.Count; i++)
                    names.Add(LootItemName(instances[i]));

                if (names.Count == 0)
                {
                    rows.Add("  (nothing resolvable)");
                    continue;
                }

                foreach (string name in names)
                    rows.Add("  " + name);
                itemCount += names.Count;
            }

            title = string.Format(CultureInfo.InvariantCulture, "{0} ({1} items)", title, itemCount);
            IReadOnlyList<string> chunks = GetStatsAomlBuilder.ChunkRows(rows, GetStatsAomlBuilder.DefaultMaxBodyLength);
            var lines = new List<string>(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                string label = chunks.Count == 1
                    ? title
                    : string.Format(CultureInfo.InvariantCulture, "{0} ({1}/{2})", title, i + 1, chunks.Count);
                lines.Add(GetStatsAomlBuilder.BuildLink(chunks[i], label));
            }

            return lines;
        }

        string LootItemName(HashInstance instance)
        {
            int[] ids = instance.TemplateIds;
            string name = ids.Length > 0 ? ItemName(ids[0]) : "(none)";
            if (ids.Length > 1)
            {
                string highName = ItemName(ids[^1]);
                if (!string.Equals(name, highName, StringComparison.Ordinal))
                    name = name + " / " + highName;
            }

            // A double quote would terminate the text:// href.
            return name.Replace('"', '\'');
        }

        void AppendIdLists(List<string> lines, string label, List<List<int>> lists)
        {
            if (lists == null || lists.Count == 0)
            {
                lines.Add("  " + label + " (empty)");
                return;
            }

            for (int i = 0; i < lists.Count; i++)
            {
                List<int> ids = lists[i];
                if (ids == null || ids.Count == 0)
                {
                    lines.Add(
                        string.Format(CultureInfo.InvariantCulture, "  {0}[{1}] (empty)", label, i));
                    continue;
                }

                if (ids.Count >= 2)
                {
                    lines.Add(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "  {0}[{1}] {2}",
                            label,
                            i,
                            FormatItemPair(ids[0], ids[1])));
                    continue;
                }

                lines.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "  {0}[{1}] {2}",
                        label,
                        i,
                        FormatItemId(ids[0])));
            }
        }

        string FormatLiveItem(Item item)
        {
            string name = item.Name;
            if (string.IsNullOrEmpty(name))
                name = ItemName(item.LowId);

            if (item.LowId == item.HighId || item.HighId <= 0)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} {1}",
                    item.LowId,
                    name);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1} {2}",
                item.LowId,
                item.HighId,
                name);
        }

        string FormatItemPair(int lowId, int highId)
        {
            if (lowId == highId || highId <= 0)
                return FormatItemId(lowId);

            string lowName = ItemName(lowId);
            string highName = ItemName(highId);
            if (string.Equals(lowName, highName, StringComparison.Ordinal))
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}/{1} {2}",
                    lowId,
                    highId,
                    lowName);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1} {2} / {3}",
                lowId,
                highId,
                lowName,
                highName);
        }

        string FormatItemId(int aoid)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1}",
                aoid,
                ItemName(aoid));
        }

        string ItemName(int aoid)
        {
            if (aoid <= 0)
                return "(none)";
            if (_items.TryGet(aoid, out ItemTemplate template) && !string.IsNullOrEmpty(template.Name))
                return template.Name;
            return "(unknown)";
        }
    }
}
