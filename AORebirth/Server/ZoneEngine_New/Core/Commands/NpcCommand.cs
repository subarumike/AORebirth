namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.GameData;

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

        public string Usage => ".npc source|template|loot";

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
                    "Template: {0} hash={1} templateId={2} knuBot={3} hasHeadMesh={4} levels={5}-{6}",
                    template.Name,
                    template.Hash,
                    template.TemplateId,
                    template.KnuBotId,
                    template.HasHeadMesh,
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
            AppendIdLists(lines, "weapon", template.Weapons);

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

        List<string> DumpLoot(NpcCharacter npc)
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
                    "Loot: {0} hash={1}",
                    template.Name,
                    template.Hash));

            List<MobItemTableEntry> itemTable = template.ItemTable;
            if (itemTable == null || itemTable.Count == 0)
            {
                lines.Add("  (no item table)");
                return lines;
            }

            foreach (MobItemTableEntry entry in itemTable)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Hash))
                    continue;

                lines.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "  table {0} repeats={1} chance={2} levelMod={3}",
                        entry.Hash,
                        entry.Repeats,
                        entry.Chance,
                        entry.LevelMod));

                bool isCategory = _gameData.TryGetHashTemplate(entry.Hash, out IReadOnlyList<string> children)
                    && children.Count > 0;
                if (isCategory)
                    lines.Add("    category " + string.Join(", ", children));

                if (_gameData.TryGetHashInstance(entry.Hash, out HashInstance instance))
                {
                    lines.Add(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "    instance ql={0}-{1}",
                            instance.MinLevel,
                            instance.MaxLevel));
                    for (int i = 0; i < instance.TemplateIds.Length; i++)
                        lines.Add("    " + FormatItemId(instance.TemplateIds[i]));
                    continue;
                }

                if (!isCategory)
                    lines.Add("    (not found)");
            }

            return lines;
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
