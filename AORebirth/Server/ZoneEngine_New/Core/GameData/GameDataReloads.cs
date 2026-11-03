namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using AORebirth.Modules.Content;

    using ZoneEngine_New.Core.Knubot;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.NpcScripts;
    using ZoneEngine_New.Core.Quests;
    using ZoneEngine_New.Core.Tradeskills;

    using AORebirth.Core.GameData;

    /// <summary>
    /// A GameData catalog that can be read again while the zone runs. A catalog is reloadable only by implementing this
    /// and being registered; nothing becomes reloadable by accident. <see cref="Reload"/> reads its files through the
    /// GameData override folder (<see cref="GameDataPaths.ResolveFile"/>) and swaps an immutable snapshot in whole, so a
    /// lookup sees the old data or the new, never a mix; a file that fails to read or parse keeps the old snapshot.
    /// Called off the playfield tick.
    /// </summary>
    public interface IReloadableGameData
    {
        string Name { get; }

        /// <summary>The GameData-relative files or folders it reads.</summary>
        IReadOnlyList<string> Files { get; }

        GameDataReloadResult Reload();
    }

    /// <summary>The registered reloadable catalogs, by name. One reload runs at a time.</summary>
    public sealed class GameDataReloadRegistry
    {
        readonly Dictionary<string, IReloadableGameData> _byName = new(StringComparer.OrdinalIgnoreCase);
        readonly Lock _reloadLock = new();

        public GameDataReloadRegistry(IEnumerable<IReloadableGameData> catalogs)
        {
            ArgumentNullException.ThrowIfNull(catalogs);
            var names = new List<string>();
            foreach (IReloadableGameData catalog in catalogs)
            {
                if (!_byName.TryAdd(catalog.Name, catalog))
                    throw new InvalidOperationException("Two reloadable GameData catalogs are named " + catalog.Name + ".");
                names.Add(catalog.Name);
            }

            Names = names;
        }

        public IReadOnlyList<string> Names { get; }

        /// <summary>
        /// Reloads <paramref name="name"/>. An unknown name, or a catalog that throws, reports a problem instead of
        /// throwing. Reads files: never call it on a playfield tick.
        /// </summary>
        public GameDataReloadResult Reload(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !_byName.TryGetValue(name.Trim(), out IReloadableGameData? catalog))
            {
                return new GameDataReloadResult(name ?? string.Empty, false, 0,
                    ["'" + name + "' is not reloadable; reloadable: " + string.Join(", ", Names)]);
            }

            lock (_reloadLock)
            {
                try
                {
                    return catalog.Reload();
                }
                catch (Exception exception)
                {
                    return new GameDataReloadResult(catalog.Name, false, 0, [exception.GetType().Name + ": " + exception.Message]);
                }
            }
        }
    }

    /// <summary>MonsterCombatRules.json: NPC features (via NpcTemplates' RuleFeatures), hates and pets.</summary>
    public sealed class MonsterCombatRulesReload(IGameData gameData) : IReloadableGameData
    {
        public string Name => MonsterCombatRulesCatalog.ReloadName;

        public IReadOnlyList<string> Files { get; } = [MonsterCombatRulesCatalog.FileName];

        public GameDataReloadResult Reload() => MonsterCombatRulesCatalog.Reload(gameData.RootPath);
    }

    /// <summary>NpcTemplates.json, swapped inside <see cref="GameDataStore"/>.</summary>
    public sealed class NpcTemplatesReload(IGameData gameData) : IReloadableGameData
    {
        public string Name => "NpcTemplates";

        public IReadOnlyList<string> Files { get; } = [GameDataPaths.NpcTemplatesFileName];

        public GameDataReloadResult Reload()
            => gameData is GameDataStore store
                ? store.ReloadNpcTemplates()
                : new GameDataReloadResult(Name, false, 0, ["this zone's GameData does not reload NpcTemplates"]);
    }

    /// <summary>GameData/NpcScripts. Running scripts keep their program until the NPC respawns.</summary>
    public sealed class NpcScriptsReload(NpcScriptCatalog scripts) : IReloadableGameData
    {
        public string Name => NpcScriptCatalog.ReloadName;

        public IReadOnlyList<string> Files { get; } = [NpcScriptCatalog.DirectoryName];

        public GameDataReloadResult Reload() => scripts.Reload();
    }

    /// <summary>
    /// ItemTemplates.json, the hash catalog inside <see cref="GameDataStore"/>. Knubot item conditions resolve hashes
    /// when the scripts load, so a successful reload reloads Knubot too.
    /// </summary>
    public sealed class ItemTemplatesReload(IGameData gameData, KnubotCatalog knubot) : IReloadableGameData
    {
        public string Name => "ItemTemplates";

        public IReadOnlyList<string> Files { get; } = [GameDataPaths.ItemTemplatesFileName];

        public GameDataReloadResult Reload()
        {
            if (gameData is not GameDataStore store)
                return new GameDataReloadResult(Name, false, 0, ["this zone's GameData does not reload ItemTemplates"]);

            GameDataReloadResult items = store.ReloadHashItems();
            if (!items.Ok)
                return items;

            KnubotLoadResult scripts = knubot.Reload();
            if (scripts.Problems.Count == 0)
                return items;

            var problems = new List<string>(items.Problems);
            foreach (string problem in scripts.Problems)
                problems.Add("Knubot: " + problem);
            return new GameDataReloadResult(Name, true, items.Entries, problems);
        }
    }

    /// <summary>Tradeskills.json, swapped inside <see cref="TradeskillService"/>.</summary>
    public sealed class TradeskillsReload(TradeskillService tradeskills) : IReloadableGameData
    {
        public string Name => TradeskillCatalog.ReloadName;

        public IReadOnlyList<string> Files { get; } = [TradeskillCatalog.FileName];

        public GameDataReloadResult Reload() => tradeskills.ReloadCatalog();
    }

    /// <summary>
    /// Quests.json. A quest a player already holds keeps its template until it is accepted again. Knubot quest
    /// conditions and effects resolve quest hashes when the scripts load, so a successful reload reloads Knubot too.
    /// </summary>
    public sealed class QuestsReload(QuestCatalog quests, KnubotCatalog knubot) : IReloadableGameData
    {
        public string Name => QuestCatalog.ReloadName;

        public IReadOnlyList<string> Files { get; } = [QuestCatalog.FileName];

        public GameDataReloadResult Reload()
        {
            GameDataReloadResult loaded = quests.Reload();
            if (!loaded.Ok)
                return loaded;

            KnubotLoadResult scripts = knubot.Reload();
            if (scripts.Problems.Count == 0)
                return loaded;

            var problems = new List<string>(loaded.Problems);
            foreach (string problem in scripts.Problems)
                problems.Add("Knubot: " + problem);
            return new GameDataReloadResult(Name, true, loaded.Entries, problems);
        }
    }

    /// <summary>GameData/Knubot plus its own AO_REBIRTH_KNUBOT_PATH overlay (<see cref="KnubotCatalog.Reload"/>).</summary>
    public sealed class KnubotReload(KnubotCatalog knubot) : IReloadableGameData
    {
        public string Name => "Knubot";

        public IReadOnlyList<string> Files { get; } = ["Knubot"];

        public GameDataReloadResult Reload()
        {
            KnubotLoadResult result = knubot.Reload();
            return new GameDataReloadResult(Name, true, result.Loaded, result.Problems);
        }
    }
}
