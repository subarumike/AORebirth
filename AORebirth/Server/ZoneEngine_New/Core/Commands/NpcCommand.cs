namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.GameData;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;

    public sealed class NpcCommand : IGmCommand
    {
        /// <summary>Upper bound for one .npc loot roll simulation.</summary>
        const int MaxLootRolls = 10000;

        private readonly IGameData _gameData;
        private readonly IItemTemplateCatalog _items;
        private readonly HashItemMinter _minter;

        public NpcCommand(IGameData gameData, IItemTemplateCatalog items, HashItemMinter minter)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(minter);
            _gameData = gameData;
            _items = items;
            _minter = minter;
        }

        public string Name => "npc";

        public int RequiredGmLevel => 1;

        public string Usage => ".npc source|template|loot [roll [count]]|equipment|nanos|position|respawn";

        public GmCommandHelp Help { get; } = new(
            "Inspects your selected NPC.",
            [
                new(".npc source", "Where it came from: spawn point, hash and spawn source."),
                new(".npc template", "Its mob template."),
                new(".npc loot", "Its loot table."),
                new(".npc loot roll [count]", "Simulates its loot drops count times."),
                new(".npc equipment", "Its equipment."),
                new(".npc nanos", "Its uploaded and running nanos."),
                new(".npc position", "Its position, home and movement state."),
                new(".npc respawn", "Despawns it (no corpse, loot or XP) and respawns its spawn point now, from the current GameData."),
            ],
            [
                new(".npc template", "Template of your target."),
                new(".npc loot roll 20", "Twenty simulated drops."),
            ],
            "Select an NPC first.");

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Args.Length < 1)
            {
                context.SendUsage();
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
                if (context.Args.Length >= 2 && string.Equals(context.Args[1], "roll", StringComparison.OrdinalIgnoreCase))
                {
                    int rolls = 1;
                    if (context.Args.Length >= 3
                        && (!int.TryParse(context.Args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out rolls)
                            || rolls < 1 || rolls > MaxLootRolls))
                    {
                        context.SendUsage("Usage: .npc loot roll [1-" + MaxLootRolls.ToString(CultureInfo.InvariantCulture) + "]");
                        return;
                    }

                    GmCommandFeedback.SendLines(context.Session, context.Player, SimulateLoot(npc, rolls));
                    return;
                }

                GmCommandFeedback.SendLines(context.Session, context.Player, DumpLoot(npc));
                return;
            }

            if (string.Equals(verb, "equipment", StringComparison.OrdinalIgnoreCase))
            {
                GmCommandFeedback.SendLines(context.Session, context.Player, DumpEquipment(npc));
                return;
            }

            if (string.Equals(verb, "nanos", StringComparison.OrdinalIgnoreCase))
            {
                GmCommandFeedback.SendLines(context.Session, context.Player, DumpNanos(npc));
                return;
            }

            if (string.Equals(verb, "position", StringComparison.OrdinalIgnoreCase))
            {
                GmCommandFeedback.SendLines(context.Session, context.Player, DumpPosition(context.Player, npc));
                return;
            }

            if (string.Equals(verb, "respawn", StringComparison.OrdinalIgnoreCase))
            {
                Respawn(context, npc);
                return;
            }

            context.SendUsage();
        }

        static void Respawn(GmCommandContext context, NpcCharacter npc)
        {
            string name = npc.Name ?? npc.Identity.Instance.ToString(CultureInfo.InvariantCulture);
            HashSpawnSystem? spawns = npc.Playfield?.GetService<HashSpawnSystem>();
            if (spawns == null || !spawns.TryRespawnNow(npc))
            {
                GmCommandFeedback.SendError(context.Session, context.Player,
                    name + " has no spawn point that respawns (GM-spawned, pet, content placement or quest objective), or is dead; not respawned.");
                return;
            }

            context.Player.SetTarget(Identity.None);
            GmCommandFeedback.Send(context.Session, context.Player, "Respawned " + name + " at its spawn point.");
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

        /// <summary>
        /// <c>.npc source</c>: where the NPC came from, as a popup. Labels are cyan, values white, section headings gold;
        /// what needs a look (a fallback hash, a dead or retired point, a held roll) is orange.
        /// </summary>
        IReadOnlyList<string> DumpSource(NpcCharacter npc)
        {
            static string F(string label, object? value)
                => DiagAoml.Field(label, DiagAoml.Name(DiagAoml.Safe(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)));
            static string Coords(double x, double y, double z)
                => string.Format(CultureInfo.InvariantCulture, "{0:F1}, {1:F1}, {2:F1}", x, y, z);

            MobTemplate? template = npc.MobTemplate;
            Vector3 pos = npc.Position;
            string title = "Source: " + DiagAoml.Safe(npc.Name ?? string.Empty);
            var blocks = new List<string>
            {
                DiagAoml.Color(DiagAoml.Gold, "NPC") + "<br>"
                    + F("id", npc.Identity.Instance) + F("spawn", npc.SpawnSource) + F("level", npc.Stats.GetOrZero(CharacterStat.Level))
                    + "<br>" + F("template", string.IsNullOrEmpty(template?.Hash) ? "-" : template!.Hash)
                    + F("pos", Coords(pos.xf, pos.yf, pos.zf))
                    + F("cell", npc.Cell != null ? npc.Cell.Id.ToString(CultureInfo.InvariantCulture) : "-")
            };

            Playfield? playfield = npc.Playfield;
            HashSpawnPoint? point = null;
            if (playfield == null)
                blocks.Add(DiagAoml.Color(DiagAoml.Orange, "Not on a playfield."));
            else if (!playfield.GetRequiredService<HashSpawnSystem>().TryGetSpawnPoint(npc, out HashSpawnPoint found))
                blocks.Add(DiagAoml.Muted("No hash-spawn point (GM-spawned, pet, content placement or quest objective)."));
            else
                point = found;

            if (point != null)
            {
                PlayfieldSpawnEntry entry = point.Source;
                string hash = DiagAoml.Field("hash", DiagAoml.Name(DiagAoml.Safe(entry.HashText ?? string.Empty)))
                    + DiagAoml.Muted("(" + entry.Hash.ToString(CultureInfo.InvariantCulture) + ")") + "&#160;&#160;";
                if (!string.Equals(point.HashText, entry.HashText, StringComparison.Ordinal))
                    hash += DiagAoml.Field("runtime", DiagAoml.Color(DiagAoml.Orange, DiagAoml.Safe(point.HashText) + " (fallback)"));

                string state = point.Retired ? DiagAoml.Color(DiagAoml.Orange, "retired")
                    : point.State == HashSpawnState.Alive ? DiagAoml.Color(DiagAoml.Green, "alive")
                    : DiagAoml.Color(DiagAoml.Orange, "dead");
                double untilSpawn = (point.NextSpawnTime - DateTime.UtcNow).TotalSeconds;
                string next = point.State == HashSpawnState.Alive || point.NextSpawnTime == default ? "-"
                    : untilSpawn <= 0 ? "due"
                    : string.Format(CultureInfo.InvariantCulture, "in {0:F0}s", untilSpawn);

                string spawnPoint = DiagAoml.Color(DiagAoml.Gold, "Spawn point") + "<br>"
                    + hash + F("district", entry.DistrictIndex) + F("cell", point.CellId)
                    + "<br>" + F("levels", point.MinLevel + "-" + point.MaxLevel)
                    + F("respawn", point.RespawnTimeSeconds + "s") + F("chance", point.RespawnChance + "%")
                    + "<br>" + DiagAoml.Field("state", state) + F("next", next)
                    + (point.IsStatic ? DiagAoml.Field("static", DiagAoml.Name("yes")) : string.Empty)
                    + (point.RespawnsAfterDeath ? string.Empty : DiagAoml.Field("respawns", DiagAoml.Color(DiagAoml.Orange, "no")));
                if (point.HoldsRoll)
                    spawnPoint += "<br>" + DiagAoml.Field("held roll", DiagAoml.Color(DiagAoml.Orange,
                        "level " + (point.RolledLevel?.ToString(CultureInfo.InvariantCulture) ?? "-")));
                blocks.Add(spawnPoint);

                blocks.Add(DiagAoml.Color(DiagAoml.Gold, "Entry") + "<br>"
                    + DiagAoml.Muted(string.Format(CultureInfo.InvariantCulture,
                        "manifest {0}  flags {1}  native {2}  more {3}  assist {4}  angle {5}/{6}",
                        entry.ManifestHash, entry.Flags, entry.NativeFlags, entry.MoreFlags, entry.AssistanceRadius,
                        entry.Angle, entry.AngleW)));

                var sites = new System.Text.StringBuilder(DiagAoml.Color(DiagAoml.Gold, "Sites"));
                for (int i = 0; i < point.Sites.Length; i++)
                {
                    SpawnSite site = point.Sites[i];
                    Quaternion heading = site.Heading;
                    sites.Append("<br>").Append(DiagAoml.Indent)
                        .Append(DiagAoml.Label("[" + i.ToString(CultureInfo.InvariantCulture) + "]")).Append(' ')
                        .Append(DiagAoml.Name(Coords(site.Centre.xf, site.Centre.yf, site.Centre.zf))).Append("&#160;&#160;")
                        .Append(F("radius", site.Radius.ToString("F2", CultureInfo.InvariantCulture)))
                        .Append(DiagAoml.Muted(string.Format(CultureInfo.InvariantCulture, "heading {0:F3} {1:F3} {2:F3} {3:F3}",
                            heading.xf, heading.yf, heading.zf, heading.wf)));
                }

                blocks.Add(sites.ToString());

                PlayfieldHashSpawnExtensionBlock? extensions = entry.Extensions;
                if (extensions != null)
                {
                    var ext = new System.Text.StringBuilder(DiagAoml.Color(DiagAoml.Gold, "Extensions") + "<br>"
                        + DiagAoml.Muted(string.Format(CultureInfo.InvariantCulture, "field0 {0}  field1 {1}  field2 {2}  trailing {3}",
                            extensions.Field0, extensions.Field1, extensions.Field2, extensions.TrailingUnknown)));
                    PlayfieldHashSpawnExtensionEvent[]? events = extensions.Events;
                    for (int i = 0; events != null && i < events.Length; i++)
                    {
                        PlayfieldHashSpawnExtensionEvent ev = events[i];
                        if (ev == null)
                            continue;
                        ext.Append("<br>").Append(DiagAoml.Indent)
                            .Append(DiagAoml.Label("event[" + i.ToString(CultureInfo.InvariantCulture) + "]")).Append(' ')
                            .Append(DiagAoml.Name(DiagAoml.Safe(ev.Name ?? string.Empty))).Append("&#160;&#160;")
                            .Append(DiagAoml.Muted(string.Format(CultureInfo.InvariantCulture, "u1 {0}  u2 {1}", ev.Unknown1, ev.Unknown2)));
                    }

                    blocks.Add(ext.ToString());
                }
            }

            return DiagAoml.Pages(title, null, blocks, "<br><br>");
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
                    "Template: {0} hash={1} templateId={2} knuBot={3} hasHeadMesh={4} features={5} levels={6}-{7}",
                    template.Name,
                    template.Hash,
                    template.TemplateId,
                    template.KnuBotId,
                    template.HasHeadMesh,
                    template.Features.Count == 0 ? "none" : string.Join(",", template.Features),
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

        /// <summary>Uploaded nanos (from equipped crystals), the nano pool, cast state and running buffs.</summary>
        List<string> DumpNanos(NpcCharacter npc)
        {
            DateTime now = DateTime.UtcNow;
            List<string> lines =
            [
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Nanos: {0} uploaded, nano {1}/{2}, casting={3} recharging={4} ncu={5}",
                    npc.UploadedNanoIds.Count,
                    npc.Stats.GetOrZero(CharacterStat.CurrentNano),
                    npc.Stats.GetOrZero(CharacterStat.MaxNanoEnergy),
                    npc.PendingCast?.Spell.Id.ToString(CultureInfo.InvariantCulture) ?? "no",
                    npc.IsInNanoRecharge(now),
                    npc.MaxNcu)
            ];

            for (int i = 0; i < npc.UploadedNanoIds.Count; i++)
            {
                int nanoId = npc.UploadedNanoIds[i];
                if (!NanoRuntime.TryGetSpell(npc, nanoId, out NanoSpell? spell) || spell == null)
                {
                    lines.Add("  " + FormatItemId(nanoId) + " (unresolved)");
                    continue;
                }

                lines.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "  {0} kind={1} cost={2} reqsMet={3}{4}",
                        FormatNano(npc, nanoId, spell),
                        spell.IsHostile ? "hostile" : spell.IsBuff ? "buff" : "friendly",
                        spell.NanoPointCost,
                        spell.MeetsActionRequirements(stat => npc.Stats.Get(stat), ActionType.ToUse),
                        // Every cast gate for a self-cast right now (nano, recharge, NCU, requirements).
                        spell.IsHostile ? string.Empty : " selfCast=" + NanoRuntime.CanStartCast(npc, spell, npc, now)));
            }

            IReadOnlyList<Buff> buffs = npc.Buffs;
            lines.Add(string.Format(CultureInfo.InvariantCulture, "Running: {0}", buffs.Count));
            for (int i = 0; i < buffs.Count; i++)
            {
                lines.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "  {0} from={1} {2}s left",
                        FormatItemId(buffs[i].Id),
                        buffs[i].Source.Instance,
                        buffs[i].RemainingCentiseconds(now) / 100));
            }

            return lines;
        }

        /// <summary>
        /// Nano id with its name. Nano templates are often missing from itemnames, so fall back to the
        /// name of the equipped crystal that uploaded it.
        /// </summary>
        string FormatNano(NpcCharacter npc, int nanoId, NanoSpell spell)
        {
            string name = !string.IsNullOrWhiteSpace(spell.Name) ? spell.Name : ItemName(nanoId);
            if (!string.IsNullOrWhiteSpace(name) && name != "(unknown)")
                return string.Format(CultureInfo.InvariantCulture, "{0} {1}", nanoId, name);

            foreach (KeyValuePair<int, Item> slot in npc.Equipment.EnumerateSlots())
            {
                foreach (List<ItemSpell> spells in slot.Value.SpellList.Values)
                {
                    for (int i = 0; spells != null && i < spells.Count; i++)
                    {
                        if (spells[i].Is(FunctionType.UploadNano) && spells[i].TryReadInt(0, out int uploaded)
                            && uploaded == nanoId)
                            return string.Format(CultureInfo.InvariantCulture, "{0} via {1}", nanoId, slot.Value.Name);
                    }
                }
            }

            return FormatItemId(nanoId);
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
            var rows = new List<string>();
            var instances = new List<HashInstance>();
            int itemCount = 0;
            bool useReconstructedPools = npc.SharesEquipmentLootQuality;

            // What the equipment spawns onto the corpse (EquipmentLoot), ahead of the item table.
            foreach (Item worn in npc.Equipment.Content.Values)
            {
                foreach (ItemSpell spell in EquipmentLoot.SpawnFunctions(worn))
                {
                    if (!EquipmentLoot.TryReadSpawn(spell, out string hash, out int quality))
                        continue;

                    IReadOnlyList<EquipmentLootMember>? reconstructed = useReconstructedPools
                        ? _gameData.GetEquipmentLootMembers(hash) : null;
                    if (rows.Count > 0)
                        rows.Add(string.Empty);
                    rows.Add(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            reconstructed == null ? "{0} spawns {1} up to QL {2}" : "{0} rolls reconstructed {1} around QL {2}",
                            (worn.Name ?? ItemName(worn.LowId)).Replace('"', '\''),
                            hash.Replace('"', '\''),
                            quality));

                    instances.Clear();
                    if (reconstructed == null)
                    {
                        _gameData.CollectHashLeafInstances(hash, instances);
                    }
                    else
                    {
                        foreach (EquipmentLootMember member in reconstructed)
                        {
                            if (quality >= member.MinCenter && quality <= member.MaxCenter)
                            {
                                instances.Add(member.Family);
                            }
                        }
                    }
                    var spawnNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < instances.Count; i++)
                        spawnNames.Add(LootItemName(instances[i]));
                    foreach (string name in spawnNames)
                        rows.Add("  " + name);
                    itemCount += spawnNames.Count;
                }
            }

            List<MobItemTableEntry> itemTable = template.ItemTable;
            if ((itemTable == null || itemTable.Count == 0) && rows.Count == 0)
                return [GetStatsAomlBuilder.BuildLink("(no item table)", title)];

            foreach (MobItemTableEntry entry in itemTable ?? [])
            {
                if (entry == null || string.IsNullOrEmpty(entry.Hash))
                    continue;

                if (rows.Count > 0)
                    rows.Add(string.Empty);
                rows.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} Chance {1}% x{2} levelMod={3}",
                        entry.Hash.Replace('"', '\''),
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

        /// <summary>
        /// Rolls the target's loot table <paramref name="rolls"/> times with the corpse's own roll (no items minted)
        /// and lists each item and quality with its drop count and effective drop rate (drops per corpse).
        /// </summary>
        IReadOnlyList<string> SimulateLoot(NpcCharacter npc, int rolls)
        {
            const string Gold = "#FFD700";
            const string Grey = "#A0A0A0";
            const string Green = "#66FF66";
            const string White = "#FFFFFF";

            MobTemplate? template = npc.MobTemplate;
            if (template == null)
                return ["No mob template on target."];

            string npcName = string.IsNullOrWhiteSpace(template.Name) ? npc.Name ?? "NPC" : template.Name;
            int level = npc.Stats.GetOrOne(CharacterStat.Level);
            int variance = npc.Playfield?.GetService<Configuration.GameplaySettings>()?.Loot.QualityVariancePercent
                ?? new Configuration.GameplayLoot().QualityVariancePercent;
            var drops = new Dictionary<(int LowId, int HighId, int Quality), int>();
            var rolled = new List<(int LowId, int HighId, int Quality)>();
            var carried = npc.CarriedLoot.Select(item => (item.LowId, item.HighId, item.Quality)).ToArray();
            bool shareScalingQuality = npc.SharesEquipmentLootQuality;
            int emptyCorpses = 0;
            int totalItems = 0;
            for (int roll = 0; roll < rolls; roll++)
            {
                rolled.Clear();
                rolled.AddRange(carried);
                // Match corpse order and remaining capacity: carried items, equipment, then the item table.
                EquipmentLoot.Roll(npc.Equipment.Content.Values, _minter, Corpse.LootCapacity - rolled.Count, variance, rolled,
                    shareScalingQuality);
                LootableDynel.RollItemTable(template.ItemTable, level, _minter, Corpse.LootCapacity - rolled.Count, variance, rolled);
                if (rolled.Count == 0)
                    emptyCorpses++;
                totalItems += rolled.Count;
                foreach ((int LowId, int HighId, int Quality) item in rolled)
                    drops[item] = drops.GetValueOrDefault(item) + 1;
            }

            var rows = new List<string>
            {
                Font(White, npcName.Replace('"', '\'')) + Font(Grey, string.Format(
                    CultureInfo.InvariantCulture, "  level {0}, {1} {2}", level, rolls, rolls == 1 ? "roll" : "rolls")),
                Font(Grey, string.Format(
                    CultureInfo.InvariantCulture,
                    "Items: {0} ({1:0.##} per corpse)   Empty corpses: {2} ({3:0.#}%)",
                    totalItems,
                    (double)totalItems / rolls,
                    emptyCorpses,
                    100.0 * emptyCorpses / rolls)),
                string.Empty
            };

            if (drops.Count == 0)
                rows.Add(Font(Grey, "Nothing dropped."));

            // One row per item name: the quality range it dropped at, its drop count and effective rate.
            var byName = new Dictionary<string, (int MinQl, int MaxQl, int Drops)>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<(int LowId, int HighId, int Quality), int> drop in drops)
            {
                string name = LootDropName(drop.Key.LowId, drop.Key.HighId, drop.Key.Quality);
                int quality = drop.Key.Quality;
                byName[name] = byName.TryGetValue(name, out var seen)
                    ? (Math.Min(seen.MinQl, quality), Math.Max(seen.MaxQl, quality), seen.Drops + drop.Value)
                    : (quality, quality, drop.Value);
            }

            foreach (KeyValuePair<string, (int MinQl, int MaxQl, int Drops)> item in byName
                         .OrderByDescending(d => d.Value.Drops)
                         .ThenBy(d => d.Key, StringComparer.OrdinalIgnoreCase))
            {
                (int minQl, int maxQl, int count) = item.Value;
                string quality = minQl == maxQl
                    ? minQl.ToString(CultureInfo.InvariantCulture)
                    : string.Format(CultureInfo.InvariantCulture, "{0}-{1}", minQl, maxQl);
                rows.Add(
                    Font(Gold, "QL " + quality) + "  "
                    + Font(White, item.Key) + "  "
                    + Font(Grey, string.Format(
                        CultureInfo.InvariantCulture, "{0} {1}", count, count == 1 ? "drop" : "drops")) + "  "
                    + Font(Green, string.Format(CultureInfo.InvariantCulture, "{0:0.##}%", 100.0 * count / rolls)));
            }

            string title = string.Format(
                CultureInfo.InvariantCulture, "{0} loot roll x{1} ({2} items)", npcName, rolls, byName.Count);
            IReadOnlyList<string> chunks = GetStatsAomlBuilder.ChunkRows(rows, GetStatsAomlBuilder.DefaultMaxBodyLength);
            var lines = new List<string>(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                string label = chunks.Count == 1
                    ? title
                    : string.Format(CultureInfo.InvariantCulture, "{0} ({1}/{2})", title, i + 1, chunks.Count);
                lines.Add(GetStatsAomlBuilder.BuildLink(chunks[i], label.Replace('"', '\'')));
            }

            return lines;

            static string Font(string color, string text) => "<font color=" + color + ">" + text + "</font>";
        }

        /// <summary>The name the rolled item would carry at its quality (built without an instance id).</summary>
        string LootDropName(int lowId, int highId, int quality)
        {
            string name;
            try
            {
                name = _minter.Create(lowId, highId, quality, ItemSource.Loot).Name;
            }
            catch (Exception)
            {
                name = string.Empty;
            }

            if (string.IsNullOrEmpty(name))
                name = ItemName(lowId);

            // A double quote would terminate the text:// href.
            return name.Replace('"', '\'');
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
