namespace ZoneEngine_New.Core.Tradeskills
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// Combines two inventory items by a <see cref="TradeskillCatalog"/> recipe, from the tradeskill window or from an
    /// item dragged onto another.
    /// Window (CellAO's handler, which ran against the live client): the client sends CharacterAction
    /// TradeskillSourceChanged / TradeskillTargetChanged with Parameter1 = container type, Parameter2 = slot
    /// (container 0 clears), and TradeskillBuildPressed with the chosen QL in Target.Instance. The server answers a
    /// changed slot with TradeskillSource / TradeskillTarget (Parameter2 = recipe count), and once both are set with
    /// TradeskillResult (Parameter1 = min QL, Parameter2 = result low id, Target = max QL : result high id),
    /// TradeskillNotValid, TradeskillOutOfRange (Parameter2 = lowest source QL) or TradeskillRequirement
    /// (Parameter1 = skill, Parameter2 = needed).
    /// Result (captures 2026-07-21/26, Arete combines): inputs deleted, FormatFeedback "You combined ...", then the
    /// result as an Overflow TemplateAction and ContainerAddItem; the client moves it to its first free slot, which is
    /// where it is persisted.
    /// Feedback is category 110 text the client formats (retail 2026-10-24): CombinedResult on success; CombineNeedsSkill
    /// then CombineFailed when skill is short; NoCombination for a drop no recipe takes.
    /// </summary>
    public sealed class TradeskillService
    {
        public enum CombineOutcome
        {
            /// <summary>No recipe for the two items; another use-item-on-item handler may take them.</summary>
            NoRecipe,

            /// <summary>A recipe matched but the combine was refused (feedback sent).</summary>
            Refused,

            Combined
        }

        const int OverflowNextFreeSlot = 0x6f;

        /// <summary>UseItemOnItemNoCombination's Target and Parameter1 as live sends them (meaning unknown).</summary>
        static readonly Identity NoCombinationTarget = new() { Type = (IdentityType)2, Instance = 0 };
        const int NoCombinationParameter1 = 2;

        /// <summary>Swapped whole by <see cref="ReloadCatalog"/>; each lookup reads it once.</summary>
        volatile TradeskillCatalog _catalog;
        readonly string _gameDataRoot;
        readonly InventoryActionService _inventory;
        readonly IItemBuilder _items;
        readonly IItemTemplateCatalog _templates;
        readonly IZoneLogger _logger;
        readonly Lazy<IReadOnlyDictionary<int, string>> _skillNames;
        readonly ConditionalWeakTable<Player, Window> _windows = new();

        sealed class Window
        {
            public Identity SourceSlot = Identity.None;
            public Identity TargetSlot = Identity.None;
        }

        /// <summary>What a recipe makes from these two items for this player, before anything changes.</summary>
        sealed class Plan
        {
            public TradeskillRecipe Recipe = null!;
            public bool Swapped;
            public Item RecipeSource = null!;
            public Item RecipeTarget = null!;
            public int MinQl;
            public int MaxQl;
            public int OutOfRangeMinQl;
            /// <summary>The target is below the recipe's MinTargetQl.</summary>
            public bool TargetBelowMinQl;
            public string? Refusal;
            public bool SkillRefusal;
            public List<(int Stat, int Needed)> MissingSkills = new();
        }

        public TradeskillService(TradeskillCatalog catalog, InventoryActionService inventory, IItemBuilder items,
            IItemTemplateCatalog templates, IZoneLogger logger, GameData.IGameData gameData)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _templates = templates ?? throw new ArgumentNullException(nameof(templates));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ArgumentNullException.ThrowIfNull(gameData);
            _gameDataRoot = gameData.RootPath;
            _skillNames = new Lazy<IReadOnlyDictionary<int, string>>(() => LoadSkillNames(gameData.RootPath));
        }

        public TradeskillCatalog Catalog => _catalog;

        /// <summary>
        /// Reads Tradeskills.json again (through the GameData override folder) and swaps the recipes in whole; a file
        /// that fails to read or parse keeps the old recipes. Reads a file: never call it on a playfield tick.
        /// </summary>
        public AORebirth.Modules.Content.GameDataReloadResult ReloadCatalog()
        {
            TradeskillCatalog loaded;
            try
            {
                loaded = TradeskillCatalog.Load(_gameDataRoot, text => _logger.Info("Tradeskills: " + text));
            }
            catch (Exception exception) when (exception is System.Text.Json.JsonException or System.IO.IOException)
            {
                _logger.Warn("Tradeskills.json not reloaded; recipes unchanged: " + exception.Message);
                return new(TradeskillCatalog.ReloadName, false, _catalog.Count, [exception.Message]);
            }

            _catalog = loaded;
            return new(TradeskillCatalog.ReloadName, true, loaded.Count, []);
        }

        // ------------------------------------------------------------------ window

        public void SourceChanged(Player player, int container, int slot) => SlotChanged(player, container, slot, isSource: true);

        public void TargetChanged(Player player, int container, int slot) => SlotChanged(player, container, slot, isSource: false);

        void SlotChanged(Player player, int container, int slot, bool isSource)
        {
            Window window = _windows.GetOrCreateValue(player);
            var picked = new Identity { Type = IdentityType.Inventory, Instance = slot };
            Item? item = container == (int)IdentityType.Inventory ? InventoryItem(player, picked) : null;
            if (isSource) window.SourceSlot = item == null ? Identity.None : picked;
            else window.TargetSlot = item == null ? Identity.None : picked;
            if (item == null)
                return;

            bool usable = isSource ? _catalog.CanBeSource(item.LowId, item.HighId) : _catalog.CanBeTarget(item.LowId, item.HighId);
            Send(player, isSource ? CharacterActionType.TradeskillSource : CharacterActionType.TradeskillTarget, new Identity(), 0, usable ? 1 : 0);
            Preview(player, window);
        }

        void Preview(Player player, Window window)
        {
            Item? source = InventoryItem(player, window.SourceSlot);
            Item? target = InventoryItem(player, window.TargetSlot);
            if (source == null || target == null)
                return;

            Plan? plan = MakePlan(player, source, target, requestedQl: null);
            if (plan == null)
            {
                Send(player, CharacterActionType.TradeskillNotValid, new Identity(), 0, 0);
                return;
            }
            if (plan.OutOfRangeMinQl > 0)
            {
                Send(player, CharacterActionType.TradeskillOutOfRange, new Identity(), 0, plan.OutOfRangeMinQl);
                return;
            }

            foreach ((int stat, int needed) in plan.MissingSkills)
                Send(player, CharacterActionType.TradeskillRequirement, new Identity(), stat, needed);
            (int low, int high) = ResultIds(plan.Recipe);
            Send(player, CharacterActionType.TradeskillResult, new Identity { Type = (IdentityType)plan.MaxQl, Instance = high },
                plan.MinQl, low);
        }

        public void BuildPressed(Player player, int quality)
        {
            if (!_windows.TryGetValue(player, out Window? window) || window.SourceSlot == Identity.None
                || window.TargetSlot == Identity.None)
            {
                ClientFeedback.Send(player, ClientFeedback.CombineNotPossible);
                return;
            }

            Combine(player, window.SourceSlot, window.TargetSlot, quality > 0 ? quality : null);
        }

        // ------------------------------------------------------------------ drag

        /// <summary>
        /// An item used on another (GenericCmd UseItemOnItem), each in the main inventory or a carried bag (packed
        /// Backpack handle/slot). Never an item on itself.
        /// </summary>
        public CombineOutcome TryCombineDrag(Player player, Identity source, Identity target)
        {
            if (!IsCombineSlot(source) || !IsCombineSlot(target) || source == target)
                return CombineOutcome.NoRecipe;
            Item? sourceItem = InventoryItem(player, source);
            Item? targetItem = InventoryItem(player, target);
            if (ReferenceEquals(sourceItem, targetItem))
                return CombineOutcome.NoRecipe;
            if (sourceItem == null || targetItem == null)
                return CombineOutcome.NoRecipe;
            if (!_catalog.TryMatch(sourceItem.LowId, sourceItem.HighId, targetItem.LowId, targetItem.HighId, out _, out _))
            {
                // Module API 1.6: an item used on another item is ItemUseOn; with no recipe it is refused.
                Modules.ModuleHooks.ActionAttempted(player, AORebirth.Modules.Events.PlayerActionKind.ItemUseOn, sourceItem.LowId,
                    AORebirth.Modules.Events.PlayerActionOutcome.Refused, AORebirth.Modules.Events.ActionRefusal.Other, sourceItem,
                    detail: "no combination");
                return CombineOutcome.NoRecipe;
            }

            CombineOutcome outcome = Combine(player, source, target, requestedQl: null);
            // A combine runs at once: refused (feedback sent), or started and done.
            if (outcome == CombineOutcome.Combined)
            {
                Modules.ModuleHooks.ActionAttempted(player, AORebirth.Modules.Events.PlayerActionKind.ItemUseOn, sourceItem.LowId,
                    AORebirth.Modules.Events.PlayerActionOutcome.Started, item: sourceItem);
                Modules.ModuleHooks.ActionResolved(player, AORebirth.Modules.Events.PlayerActionKind.ItemUseOn, sourceItem.LowId,
                    AORebirth.Modules.Events.PlayerActionOutcome.Executed, item: sourceItem);
            }
            else
                Modules.ModuleHooks.ActionAttempted(player, AORebirth.Modules.Events.PlayerActionKind.ItemUseOn, sourceItem.LowId,
                    AORebirth.Modules.Events.PlayerActionOutcome.Refused, AORebirth.Modules.Events.ActionRefusal.Requirements,
                    sourceItem, detail: "combine refused");
            return outcome;
        }

        /// <summary>
        /// An inventory item dropped on another (CharacterAction UseItemOnItem) that no recipe takes. Live answers
        /// UseItemOnItemNoCombination, then Feedback "No combination found!" (retail 2026-10-24T17:01:44Z).
        /// </summary>
        public static void NoCombination(Player player)
        {
            Send(player, CharacterActionType.UseItemOnItemNoCombination, NoCombinationTarget, NoCombinationParameter1, 0);
            ClientFeedback.Send(player, ClientFeedback.NoCombinationFound);
        }

        // ------------------------------------------------------------------ combine

        CombineOutcome Combine(Player player, Identity sourceSlot, Identity targetSlot, int? requestedQl)
        {
            if (player.IsDead || player.IsPersistenceQuarantined || !player.Inventory.IsHydrated || sourceSlot == targetSlot)
                return CombineOutcome.Refused;

            Item? source = InventoryItem(player, sourceSlot);
            Item? target = InventoryItem(player, targetSlot);
            if (source == null || target == null || ReferenceEquals(source, target) || source.Locked || target.Locked)
            {
                ClientFeedback.Send(player, ClientFeedback.CombineNotPossible);
                return CombineOutcome.Refused;
            }

            Plan? plan = MakePlan(player, source, target, requestedQl);
            if (plan == null)
            {
                ClientFeedback.Send(player, ClientFeedback.CombineNotPossible);
                return CombineOutcome.NoRecipe;
            }
            if (plan.SkillRefusal)
            {
                // Live: one FormatFeedback naming the skill short, then "Combine failed!" (retail 2026-10-24).
                foreach ((int stat, int needed) in plan.MissingSkills)
                    ClientFeedback.SendFormatted(player, ClientFeedback.CombineNeedsSkill,
                        NameOf(plan.RecipeSource), NameOf(plan.RecipeTarget), needed, SkillName(stat));
                ClientFeedback.Send(player, ClientFeedback.CombineFailed);
                return CombineOutcome.Refused;
            }
            // The client's own texts for a QL that is too low (a raw string here showed as an empty chat line).
            if (plan.TargetBelowMinQl)
            {
                ClientFeedback.SendFormatted(player, ClientFeedback.CombineItemQlTooLow, NameOf(plan.RecipeTarget), plan.Recipe.MinTargetQl);
                return CombineOutcome.Refused;
            }
            if (plan.OutOfRangeMinQl > 0)
            {
                ClientFeedback.SendFormatted(player, ClientFeedback.CombineSourceQlTooLow, NameOf(plan.RecipeSource),
                    NameOf(plan.RecipeTarget), plan.RecipeTarget.Quality, NameOf(plan.RecipeSource), plan.OutOfRangeMinQl);
                return CombineOutcome.Refused;
            }
            if (plan.Refusal != null)
            {
                Feedback(player, plan.Refusal);
                return CombineOutcome.Refused;
            }

            TradeskillRecipe recipe = plan.Recipe;
            int quality = requestedQl is int asked ? Math.Clamp(asked, plan.MinQl, plan.MaxQl) : plan.MaxQl;
            (int low, int high) = ResultIds(recipe);
            if (IsUniqueResult(low, high) && Trade.TradeRules.WouldDuplicateUnique(player, low, high))
            {
                Feedback(player, "You can only have one of the resulting item.");
                return CombineOutcome.Refused;
            }

            var consumed = new List<(Identity Slot, Item Item)>();
            if (recipe.ConsumeSource) consumed.Add((SlotOf(plan.RecipeSource, sourceSlot, targetSlot, source), plan.RecipeSource));
            if (recipe.ConsumeTarget) consumed.Add((SlotOf(plan.RecipeTarget, sourceSlot, targetSlot, source), plan.RecipeTarget));
            if (consumed.Any(c => c.Item.StackCount > 1))
            {
                Feedback(player, "Split the stack first: only one item can be used in a combine.");
                return CombineOutcome.Refused;
            }

            // The client deletes the used items, then puts the overflow result in its first free slot. The result must fit in
            // the main inventory: with no room there (counting a slot a used item frees) the combine is refused, never parked
            // in overflow.
            Container page = player.Inventory.Inventory;
            // Only a used item's main-inventory slot can take the result; a bag slot it leaves stays in its bag.
            var freed = new HashSet<int>(consumed.Where(c => c.Slot.Type == IdentityType.Inventory).Select(c => c.Slot.Instance));
            int resultSlot = Enumerable.Range(page.Offset, page.Capacity)
                .FirstOrDefault(slot => !page.Content.ContainsKey(slot) || freed.Contains(slot), -1);
            if (resultSlot < 0)
            {
                Feedback(player, "Your inventory is full. Free a slot and try the combine again.");
                return CombineOutcome.Refused;
            }

            Item result = _items.CreateWithNewInstance(low, high, quality, ItemSource.Other);
            var graveyard = new Identity { Type = IdentityType.None, Instance = player.Identity.Instance };
            var changes = consumed
                .Select(c => new InventoryRowChange(c.Item, graveyard, c.Item.InstanceId, c.Item.StackCount, Retired: true))
                .Append(new InventoryRowChange(result, page.Identity, resultSlot, result.StackCount))
                .ToList();
            string sourceName = NameOf(source), targetName = NameOf(target);

            bool committed = _inventory.TryCommit(player, changes,
                () => ReferenceEquals(InventoryItem(player, sourceSlot), source)
                    && ReferenceEquals(InventoryItem(player, targetSlot), target)
                    && (!page.Content.ContainsKey(resultSlot) || freed.Contains(resultSlot)),
                () =>
                {
                    foreach ((Identity slot, Item _) in consumed)
                    {
                        if (!TryResolveCombineSlot(player, slot, out Container usedPage, out int placement, out _))
                            throw new InvalidOperationException("Validated tradeskill ingredient slot changed.");
                        usedPage.Content.Remove(placement);
                        // A bag slot is dropped by the packed Backpack identity the client used for it.
                        player.Session?.Send(new CharacterActionMessage
                        {
                            Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot
                        });
                    }
                    if (!page.Add(resultSlot, result))
                        throw new InvalidOperationException("Reserved tradeskill result slot changed.");
                    ClientFeedback.SendFormatted(player, ClientFeedback.CombinedResult, sourceName, targetName, result.Quality, NameOf(result));
                    player.Session?.Send(new TemplateActionMessage
                    {
                        Identity = player.Identity, ItemLowId = result.LowId, ItemHighId = result.HighId, Quality = result.Quality,
                        Count = result.StackCount, Action = TemplateActionType.Overflow,
                        Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 }
                    });
                    player.Session?.Send(new ContainerAddItemMessage
                    {
                        Identity = player.Identity,
                        SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                        Target = new Identity { Type = IdentityType.OverflowWindow, Instance = player.Identity.Instance },
                        TargetPlacement = OverflowNextFreeSlot
                    });
                });
            if (!committed)
            {
                Feedback(player, "The combine could not be completed.");
                return CombineOutcome.Refused;
            }

            foreach ((Identity _, Item used) in consumed)
                Modules.ModuleHooks.ItemMoved(player, used, Modules.ModuleHooks.Place(AORebirth.Modules.Events.ItemPlaceKind.Inventory),
                    Modules.ModuleHooks.Place(AORebirth.Modules.Events.ItemPlaceKind.Destroyed));
            Modules.ModuleHooks.ItemMoved(player, result, Modules.ModuleHooks.Place(AORebirth.Modules.Events.ItemPlaceKind.None),
                Modules.ModuleHooks.Place(AORebirth.Modules.Events.ItemPlaceKind.Inventory));

            int xp = Xp(player, result.Quality);
            if (xp > 0)
                player.AwardXp(xp, XpSource.Quest);
            player.Playfield?.GetService<Quests.QuestService>()?.OnItemAssembled(player, result);
            if (_windows.TryGetValue(player, out Window? window))
            {
                if (recipe.ConsumeSource || recipe.ConsumeTarget)
                    window.SourceSlot = window.TargetSlot = Identity.None;
            }
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Tradeskill char={0} recipe={1} ({2}) {3}/{4} ql{5} + {6}/{7} ql{8} -> {9}/{10} ql{11} slot={12} xp={13}",
                player.Identity.Instance, recipe.Key, recipe.Origin, source.LowId, source.HighId, source.Quality,
                target.LowId, target.HighId, target.Quality, result.LowId, result.HighId, result.Quality, resultSlot, xp));
            return CombineOutcome.Combined;
        }

        static Identity SlotOf(Item item, Identity sourceSlot, Identity targetSlot, Item sourceItem)
            => ReferenceEquals(item, sourceItem) ? sourceSlot : targetSlot;

        Plan? MakePlan(Player player, Item source, Item target, int? requestedQl)
        {
            if (!_catalog.TryMatch(source.LowId, source.HighId, target.LowId, target.HighId, out TradeskillRecipe recipe, out bool swapped))
                return null;

            var plan = new Plan
            {
                Recipe = recipe, Swapped = swapped,
                RecipeSource = swapped ? target : source,
                RecipeTarget = swapped ? source : target
            };
            int targetQl = plan.RecipeTarget.Quality;
            int sourceQl = plan.RecipeSource.Quality;

            if (recipe.MinTargetQl > 0 && targetQl < recipe.MinTargetQl)
            {
                plan.TargetBelowMinQl = true;
                plan.Refusal = string.Format(CultureInfo.InvariantCulture, "The target must be at least quality level {0}.", recipe.MinTargetQl);
            }

            if (!InRange(recipe, sourceQl, targetQl))
            {
                plan.OutOfRangeMinQl = Math.Max(1, recipe.QlRangePercent == 1
                    ? targetQl
                    : (int)Math.Ceiling(targetQl - recipe.QlRangePercent * targetQl / 100.0));
                plan.Refusal ??= string.Format(CultureInfo.InvariantCulture,
                    "The source must be at least quality level {0} for a quality level {1} target.", plan.OutOfRangeMinQl, targetQl);
            }

            int bumps = int.MaxValue;
            foreach (TradeskillSkill skill in recipe.Skills)
            {
                if (skill.Stat <= 0)
                    continue;
                int needed = (int)Math.Ceiling(skill.Percent / 100.0 * targetQl);
                int have = player.Stats.GetOrZero((CharacterStat)skill.Stat);
                if (have < needed)
                    plan.MissingSkills.Add((skill.Stat, needed));
                if (skill.PerBump > 0)
                    bumps = Math.Min(bumps, Math.Max(0, (have - needed) / skill.PerBump));
            }
            if (plan.MissingSkills.Count > 0 && plan.Refusal == null)
                plan.SkillRefusal = true;
            if (plan.MissingSkills.Count > 0)
                plan.Refusal ??= string.Format(CultureInfo.InvariantCulture, "You need {0} more skill to combine these.",
                    string.Join(", ", plan.MissingSkills.Select(m => m.Needed + " " + (CharacterStat)m.Stat)));
            if (bumps == int.MaxValue)
                bumps = 0;
            bumps = Math.Min(bumps, MaxBump(recipe, targetQl));

            (int resultMin, int resultMax) = ResultQlRange(recipe);
            switch (recipe.ResultQl)
            {
                case TradeskillResultQl.Fixed:
                    plan.MinQl = plan.MaxQl = Math.Max(1, recipe.FixedQl);
                    break;
                case TradeskillResultQl.Source:
                    plan.MinQl = plan.MaxQl = Math.Clamp(sourceQl, resultMin, resultMax);
                    break;
                default:
                    plan.MinQl = Math.Clamp(targetQl, resultMin, resultMax);
                    plan.MaxQl = Math.Clamp(targetQl + bumps, plan.MinQl, resultMax);
                    break;
            }

            return plan;
        }

        static bool InRange(TradeskillRecipe recipe, int sourceQl, int targetQl)
        {
            if (recipe.QlRangePercent == 0 || targetQl <= 0)
                return true;
            if (recipe.QlRangePercent == 1)
                return sourceQl >= targetQl;
            return (targetQl - (double)sourceQl) / targetQl <= recipe.QlRangePercent / 100.0;
        }

        /// <summary>Implant recipes: the AO-Universe QL tier cap (CellAO), or the recipe's MaxBump when lower.</summary>
        static int MaxBump(TradeskillRecipe recipe, int targetQl)
        {
            if (!recipe.Implant)
                return Math.Max(0, recipe.MaxBump);
            int tier = targetQl >= 250 ? 5 : targetQl >= 201 ? 4 : targetQl >= 150 ? 3 : targetQl >= 100 ? 2 : targetQl >= 50 ? 1 : 0;
            return recipe.MaxBump > 0 && recipe.MaxBump < tier ? recipe.MaxBump : tier;
        }

        (int Low, int High) ResultIds(TradeskillRecipe recipe)
        {
            int low = recipe.Result.LowId;
            int high = recipe.Result.HighId > 0 ? recipe.Result.HighId : low;
            // The lower-QL template first (items.dat pairs are not always ascending by id).
            if (_templates.TryGet(low, out ItemTemplate lowTemplate) && _templates.TryGet(high, out ItemTemplate highTemplate)
                && lowTemplate.Quality > highTemplate.Quality)
                (low, high) = (high, low);
            return (low, high);
        }

        /// <summary>The result template carries the Unique item flag (either end of its QL pair).</summary>
        bool IsUniqueResult(int low, int high)
            => (_templates.TryGet(low, out ItemTemplate l) && (l.Flags & (int)AORebirth.Enums.ItemFlags.Unique) != 0)
                || (_templates.TryGet(high, out ItemTemplate h) && (h.Flags & (int)AORebirth.Enums.ItemFlags.Unique) != 0);

        (int Min, int Max) ResultQlRange(TradeskillRecipe recipe)
        {
            (int low, int high) = ResultIds(recipe);
            int min = _templates.TryGet(low, out ItemTemplate l) && l.Quality > 0 ? l.Quality : 1;
            int max = _templates.TryGet(high, out ItemTemplate h) && h.Quality > 0 ? h.Quality : min;
            return (Math.Min(min, max), Math.Max(min, max));
        }

        /// <summary>
        /// XP for a combine: the result's QL x Gameplay.json xp.tradeskillXpPerQl, only while xp.tradeskillGrantsXp is on;
        /// 0 otherwise.
        /// </summary>
        static int Xp(Player player, int quality)
        {
            Configuration.GameplayXp rules = player.Playfield?.GetService<Configuration.GameplaySettings>()?.Xp
                ?? new Configuration.GameplayXp();
            if (!rules.TradeskillGrantsXp || quality <= 0)
                return 0;
            return (int)Math.Min(int.MaxValue, (long)quality * rules.TradeskillXpPerQl);
        }

        string NameOf(Item item)
        {
            if (!string.IsNullOrEmpty(item.Definition?.Name))
                return item.Definition!.Name;
            return _templates.TryGet(item.LowId, out ItemTemplate template) && !string.IsNullOrEmpty(template.Name) ? template.Name : "item";
        }

        /// <summary>The client's short skill name (Text.json category 2003, e.g. 159 "Pharma Tech").</summary>
        string SkillName(int stat)
            => _skillNames.Value.TryGetValue(stat, out string? name) ? name : ((CharacterStat)stat).ToString();

        static IReadOnlyDictionary<int, string> LoadSkillNames(string gameDataRoot)
        {
            var names = new Dictionary<int, string>();
            string path = System.IO.Path.Combine(gameDataRoot, "Text.json");
            if (!System.IO.File.Exists(path))
                return names;
            using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
            foreach (System.Text.Json.JsonElement entry in document.RootElement.EnumerateArray())
            {
                if (entry.TryGetProperty("category", out var category) && category.GetInt32() == ClientFeedback.StatNameCategory
                    && entry.TryGetProperty("id", out var id) && entry.TryGetProperty("text", out var text)
                    && !string.IsNullOrEmpty(text.GetString()))
                    names.TryAdd(id.GetInt32(), text.GetString()!);
            }
            return names;
        }

        static bool IsCombineSlot(Identity slot) => slot.Type is IdentityType.Inventory or IdentityType.Backpack;

        /// <summary>
        /// A combine ingredient's page, placement and item: a main-inventory slot, or a carried bag's packed Backpack slot
        /// (hydrated, removable, not locked). Bank, wear pages and overflow are never combined from.
        /// </summary>
        static bool TryResolveCombineSlot(Player player, Identity slot, out Container page, out int placement, out Item item)
        {
            if (slot.Type == IdentityType.Backpack)
                return player.Inventory.TryGetCarriedBackpackItem(slot, out page, out placement, out item);

            page = player.Inventory.Inventory;
            placement = slot.Instance;
            item = null!;
            return slot.Type == IdentityType.Inventory && placement >= 0 && page.Content.TryGetValue(placement, out item!);
        }

        static Item? InventoryItem(Player player, Identity slot)
            => TryResolveCombineSlot(player, slot, out _, out _, out Item item) ? item : null;

        static void Send(Player player, CharacterActionType action, Identity target, int parameter1, int parameter2)
            => player.Session?.Send(new CharacterActionMessage
            {
                Identity = player.Identity, Action = action, Target = target, Parameter1 = parameter1, Parameter2 = parameter2
            });

        /// <summary>
        /// A server-worded line as plain-text feedback (category 110 / <see cref="ClientFeedback.PlainText"/>). A bare
        /// FormatFeedback string is not a format the client reads: it showed an empty chat line.
        /// </summary>
        static void Feedback(Player player, string text)
            => ClientFeedback.SendFormatted(player, ClientFeedback.PlainText, text);
    }
}
