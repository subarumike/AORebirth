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

        const string CombinedText = "You combined \"{0}\" with \"{1}\" and the result is a quality level {2} \"{3}\".";
        const int OverflowNextFreeSlot = 0x6f;

        readonly TradeskillCatalog _catalog;
        readonly InventoryActionService _inventory;
        readonly IItemBuilder _items;
        readonly IItemTemplateCatalog _templates;
        readonly IZoneLogger _logger;
        readonly ConditionalWeakTable<Player, Window> _windows = new();

        sealed class Window
        {
            public int SourceSlot = -1;
            public int TargetSlot = -1;
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
            public string? Refusal;
            public List<(int Stat, int Needed)> MissingSkills = new();
        }

        public TradeskillService(TradeskillCatalog catalog, InventoryActionService inventory, IItemBuilder items,
            IItemTemplateCatalog templates, IZoneLogger logger)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _templates = templates ?? throw new ArgumentNullException(nameof(templates));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public TradeskillCatalog Catalog => _catalog;

        // ------------------------------------------------------------------ window

        public void SourceChanged(Player player, int container, int slot) => SlotChanged(player, container, slot, isSource: true);

        public void TargetChanged(Player player, int container, int slot) => SlotChanged(player, container, slot, isSource: false);

        void SlotChanged(Player player, int container, int slot, bool isSource)
        {
            Window window = _windows.GetOrCreateValue(player);
            Item? item = container == (int)IdentityType.Inventory ? InventoryItem(player, slot) : null;
            if (isSource) window.SourceSlot = item == null ? -1 : slot;
            else window.TargetSlot = item == null ? -1 : slot;
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
            if (!_windows.TryGetValue(player, out Window? window) || window.SourceSlot < 0 || window.TargetSlot < 0)
            {
                Feedback(player, "It is not possible to assemble those two items. Maybe the order was wrong?");
                return;
            }

            Combine(player, window.SourceSlot, window.TargetSlot, quality > 0 ? quality : null);
        }

        // ------------------------------------------------------------------ drag

        /// <summary>An inventory item used on another inventory item (GenericCmd UseItemOnItem).</summary>
        public CombineOutcome TryCombineDrag(Player player, Identity source, Identity target)
        {
            if (source.Type != IdentityType.Inventory || target.Type != IdentityType.Inventory || source.Instance == target.Instance)
                return CombineOutcome.NoRecipe;
            Item? sourceItem = InventoryItem(player, source.Instance);
            Item? targetItem = InventoryItem(player, target.Instance);
            if (sourceItem == null || targetItem == null
                || !_catalog.TryMatch(sourceItem.LowId, sourceItem.HighId, targetItem.LowId, targetItem.HighId, out _, out _))
                return CombineOutcome.NoRecipe;

            return Combine(player, source.Instance, target.Instance, requestedQl: null);
        }

        // ------------------------------------------------------------------ combine

        CombineOutcome Combine(Player player, int sourceSlot, int targetSlot, int? requestedQl)
        {
            if (player.IsDead || player.IsPersistenceQuarantined || !player.Inventory.IsHydrated || sourceSlot == targetSlot)
                return CombineOutcome.Refused;

            Item? source = InventoryItem(player, sourceSlot);
            Item? target = InventoryItem(player, targetSlot);
            if (source == null || target == null || source.Locked || target.Locked)
            {
                Feedback(player, "It is not possible to assemble those two items. Maybe the order was wrong?");
                return CombineOutcome.Refused;
            }

            Plan? plan = MakePlan(player, source, target, requestedQl);
            if (plan == null)
            {
                Feedback(player, "It is not possible to assemble those two items. Maybe the order was wrong?");
                return CombineOutcome.NoRecipe;
            }
            if (plan.Refusal != null)
            {
                Feedback(player, plan.Refusal);
                return CombineOutcome.Refused;
            }

            TradeskillRecipe recipe = plan.Recipe;
            int quality = requestedQl is int asked ? Math.Clamp(asked, plan.MinQl, plan.MaxQl) : plan.MaxQl;
            (int low, int high) = ResultIds(recipe);
            if (Trade.TradeRules.WouldDuplicateUnique(player, low, high))
            {
                Feedback(player, "You can only have one of the resulting item.");
                return CombineOutcome.Refused;
            }

            var consumed = new List<(int Slot, Item Item)>();
            if (recipe.ConsumeSource) consumed.Add((SlotOf(plan.RecipeSource, sourceSlot, targetSlot, source), plan.RecipeSource));
            if (recipe.ConsumeTarget) consumed.Add((SlotOf(plan.RecipeTarget, sourceSlot, targetSlot, source), plan.RecipeTarget));
            if (consumed.Any(c => c.Item.StackCount > 1))
            {
                Feedback(player, "Split the stack first: only one item can be used in a combine.");
                return CombineOutcome.Refused;
            }

            // The client deletes the used items, then puts the overflow result in its first free slot.
            Container page = player.Inventory.Inventory;
            var freed = new HashSet<int>(consumed.Select(c => c.Slot));
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
                    foreach ((int slot, Item _) in consumed)
                    {
                        page.Content.Remove(slot);
                        player.Session?.Send(new CharacterActionMessage
                        {
                            Identity = player.Identity, Action = CharacterActionType.DeleteItem,
                            Target = new Identity { Type = IdentityType.Inventory, Instance = slot }
                        });
                    }
                    if (!page.Add(resultSlot, result))
                        throw new InvalidOperationException("Reserved tradeskill result slot changed.");
                    Feedback(player, string.Format(CultureInfo.InvariantCulture, CombinedText, sourceName, targetName, result.Quality, NameOf(result)));
                    player.Session?.Send(new TemplateActionMessage
                    {
                        Identity = player.Identity, ItemLowId = result.LowId, ItemHighId = result.HighId, Quality = result.Quality,
                        Unknown1 = 1, Action = TemplateActionType.Overflow,
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

            int xp = Xp(recipe, result.Quality);
            if (xp > 0)
                player.AwardXp(xp, XpSource.Quest);
            player.Playfield?.GetService<Quests.QuestService>()?.OnItemAssembled(player, result.LowId, result.HighId);
            if (_windows.TryGetValue(player, out Window? window))
            {
                if (recipe.ConsumeSource || recipe.ConsumeTarget)
                    window.SourceSlot = window.TargetSlot = -1;
            }
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Tradeskill char={0} recipe={1} ({2}) {3}/{4} ql{5} + {6}/{7} ql{8} -> {9}/{10} ql{11} slot={12} xp={13}",
                player.Identity.Instance, recipe.Key, recipe.Origin, source.LowId, source.HighId, source.Quality,
                target.LowId, target.HighId, target.Quality, result.LowId, result.HighId, result.Quality, resultSlot, xp));
            return CombineOutcome.Combined;
        }

        static int SlotOf(Item item, int sourceSlot, int targetSlot, Item sourceItem)
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
                plan.Refusal = string.Format(CultureInfo.InvariantCulture, "The target must be at least quality level {0}.", recipe.MinTargetQl);

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

        (int Min, int Max) ResultQlRange(TradeskillRecipe recipe)
        {
            (int low, int high) = ResultIds(recipe);
            int min = _templates.TryGet(low, out ItemTemplate l) && l.Quality > 0 ? l.Quality : 1;
            int max = _templates.TryGet(high, out ItemTemplate h) && h.Quality > 0 ? h.Quality : min;
            return (Math.Min(min, max), Math.Max(min, max));
        }

        /// <summary>CellAO: MinXp at the result's lowest QL up to MaxXp at its highest.</summary>
        int Xp(TradeskillRecipe recipe, int quality)
        {
            if (recipe.MaxXp <= 0)
                return 0;
            (int min, int max) = ResultQlRange(recipe);
            if (max <= min)
                return recipe.MaxXp;
            return (int)Math.Floor(recipe.MinXp + (recipe.MaxXp - recipe.MinXp) * (double)(Math.Clamp(quality, min, max) - min) / (max - min));
        }

        string NameOf(Item item)
        {
            if (!string.IsNullOrEmpty(item.Definition?.Name))
                return item.Definition!.Name;
            return _templates.TryGet(item.LowId, out ItemTemplate template) && !string.IsNullOrEmpty(template.Name) ? template.Name : "item";
        }

        static Item? InventoryItem(Player player, int slot)
            => slot >= 0 && player.Inventory.Inventory.Content.TryGetValue(slot, out Item? item) ? item : null;

        static void Send(Player player, CharacterActionType action, Identity target, int parameter1, int parameter2)
            => player.Session?.Send(new CharacterActionMessage
            {
                Identity = player.Identity, Action = action, Target = target, Parameter1 = parameter1, Parameter2 = parameter2
            });

        static void Feedback(Player player, string text)
            => player.Session?.Send(new FormatFeedbackMessage
            {
                Identity = player.Identity, Unknown = 1, Unknown1 = 0, FormattedMessage = text
            });
    }
}
