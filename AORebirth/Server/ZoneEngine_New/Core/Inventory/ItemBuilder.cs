namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Logging;

    public sealed class ItemBuilder : IItemBuilder
    {
        private readonly IItemTemplateCatalog _catalog;
        private readonly IZoneLogger _logger;

        public ItemBuilder(IItemTemplateCatalog catalog, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(logger);
            _catalog = catalog;
            _logger = logger;
        }

        public Item Create(
            int lowId,
            int highId,
            int quality,
            int stackCount = 1,
            int instanceId = 0,
            Identity? identity = null,
            byte[]? statsBlob = null)
        {
            ItemTemplate low = ResolveTemplate(lowId);
            ItemTemplate high = highId == lowId || !_catalog.TryGet(highId, out ItemTemplate? highTemplate)
                ? low
                : highTemplate!;

            int clampedQuality = ClampQuality(quality, low.Quality, high.Quality);
            ItemTemplate definition = BuildEffectiveDefinition(low, high, clampedQuality);
            ApplyStatsBlob(definition, statsBlob);

            int resolvedStack = stackCount;
            if (definition.Stats.TryGetValue(CharacterStat.MultipleCount, out int stackFromStats) && stackFromStats > 0)
                resolvedStack = stackFromStats;

            return new Item
            {
                InstanceId = instanceId,
                Identity = identity ?? Identity.None,
                LowId = lowId,
                HighId = highId,
                Quality = clampedQuality,
                StackCount = Math.Max(1, resolvedStack),
                Definition = definition
            };
        }

        public bool TryFromInstanceRecord(ItemInstanceRecord row, out Item item)
        {
            ArgumentNullException.ThrowIfNull(row);
            try
            {
                Identity identity = row.ItemType != 0
                    ? new Identity { Type = (IdentityType)row.ItemType, Instance = row.InstanceId }
                    : Identity.None;

                item = Create(
                    row.LowId,
                    row.HighId,
                    row.Quality,
                    row.StackCount,
                    row.InstanceId,
                    identity);
                item.IsPersisted = true;
                item.ApplyContainerIdentityIfBag();
                return true;
            }
            catch (Exception exception)
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ItemBuilder.TryFromInstanceRecord failed id={0} low={1}: {2}",
                        row.InstanceId,
                        row.LowId,
                        exception.Message));
                item = null!;
                return false;
            }
        }

        private ItemTemplate ResolveTemplate(int aoid)
        {
            if (_catalog.TryGet(aoid, out ItemTemplate? template))
                return template!;

            return new ItemTemplate
            {
                Id = aoid,
                Name = string.Format(CultureInfo.InvariantCulture, "Unknown {0}", aoid),
                Quality = 1
            };
        }

        private static int ClampQuality(int quality, int lowQl, int highQl)
        {
            int min = Math.Min(lowQl, highQl);
            int max = Math.Max(lowQl, highQl);
            if (max < 1)
                max = 1;
            if (min < 1)
                min = 1;

            if (quality < min)
                return min;
            if (quality > max)
                return max;
            return quality;
        }

        private static ItemTemplate BuildEffectiveDefinition(ItemTemplate low, ItemTemplate high, int quality)
        {
            if (ReferenceEquals(low, high) || low.Quality == high.Quality || low.Id == high.Id)
                return DeepCopy(low, quality);

            if (quality == low.Quality)
                return DeepCopy(low, quality);

            if (quality == high.Quality)
                return DeepCopy(high, quality);

            float factor = (quality - low.Quality) / (float)(high.Quality - low.Quality);

            return new ItemTemplate
            {
                Id = low.Id,
                Name = low.Name,
                Quality = quality,
                Flags = low.Flags,
                ItemType = low.ItemType,
                MultipleCount = LerpInt(low.MultipleCount, high.MultipleCount, factor),
                Stats = LerpIntMap(low.Stats, high.Stats, factor),
                Attack = LerpIntMap(low.Attack, high.Attack, factor),
                Defend = LerpIntMap(low.Defend, high.Defend, factor),
                SpellList = LerpSpellList(low.SpellList, high.SpellList, factor),
                Actions = LerpActions(low.Actions, high.Actions, factor),
                Relations = new List<int>(low.Relations)
            };
        }

        private static ItemTemplate DeepCopy(ItemTemplate source, int quality)
        {
            var spellList = new Dictionary<AORebirth.Enums.EventType, List<ItemSpell>>();
            foreach (KeyValuePair<AORebirth.Enums.EventType, List<ItemSpell>> pair in source.SpellList)
            {
                var spells = new List<ItemSpell>(pair.Value.Count);
                foreach (ItemSpell spell in pair.Value)
                    spells.Add(spell.Copy());
                spellList[pair.Key] = spells;
            }

            var actions = new List<ItemAction>(source.Actions.Count);
            foreach (ItemAction action in source.Actions)
                actions.Add(action.Copy());

            return new ItemTemplate
            {
                Id = source.Id,
                Name = source.Name,
                Quality = quality,
                Flags = source.Flags,
                ItemType = source.ItemType,
                MultipleCount = source.MultipleCount,
                Stats = new Dictionary<CharacterStat, int>(source.Stats),
                Attack = new Dictionary<CharacterStat, int>(source.Attack),
                Defend = new Dictionary<CharacterStat, int>(source.Defend),
                SpellList = spellList,
                Actions = actions,
                Relations = new List<int>(source.Relations)
            };
        }

        private static Dictionary<CharacterStat, int> LerpIntMap(
            Dictionary<CharacterStat, int> low,
            Dictionary<CharacterStat, int> high,
            float factor)
        {
            var result = new Dictionary<CharacterStat, int>();
            foreach (KeyValuePair<CharacterStat, int> pair in low)
            {
                int highValue = high.TryGetValue(pair.Key, out int hv) ? hv : pair.Value;
                result[pair.Key] = LerpInt(pair.Value, highValue, factor);
            }

            foreach (KeyValuePair<CharacterStat, int> pair in high)
            {
                if (!result.ContainsKey(pair.Key))
                    result[pair.Key] = pair.Value;
            }

            return result;
        }

        private static Dictionary<AORebirth.Enums.EventType, List<ItemSpell>> LerpSpellList(
            Dictionary<AORebirth.Enums.EventType, List<ItemSpell>> low,
            Dictionary<AORebirth.Enums.EventType, List<ItemSpell>> high,
            float factor)
        {
            var result = new Dictionary<AORebirth.Enums.EventType, List<ItemSpell>>();
            foreach (KeyValuePair<AORebirth.Enums.EventType, List<ItemSpell>> pair in low)
            {
                if (!high.TryGetValue(pair.Key, out List<ItemSpell>? highSpells))
                {
                    var copied = new List<ItemSpell>(pair.Value.Count);
                    foreach (ItemSpell spell in pair.Value)
                        copied.Add(spell.Copy());
                    result[pair.Key] = copied;
                    continue;
                }

                int count = Math.Min(pair.Value.Count, highSpells.Count);
                var lerped = new List<ItemSpell>(count);
                for (int i = 0; i < count; i++)
                    lerped.Add(LerpSpell(pair.Value[i], highSpells[i], factor));
                result[pair.Key] = lerped;
            }

            return result;
        }

        private static ItemSpell LerpSpell(ItemSpell low, ItemSpell high, float factor)
        {
            ItemSpell copy = low.Copy();
            int reqCount = Math.Min(copy.Requirements.Count, high.Requirements.Count);
            for (int i = 0; i < reqCount; i++)
                copy.Requirements[i].Value = LerpInt(low.Requirements[i].Value, high.Requirements[i].Value, factor);

            int argCount = Math.Min(copy.Arguments.Count, high.Arguments.Count);
            for (int i = 0; i < argCount; i++)
            {
                if (low.Arguments[i] is int lowInt && high.Arguments[i] is int highInt)
                    copy.Arguments[i] = LerpInt(lowInt, highInt, factor);
                else if (low.Arguments[i] is float lowFloat && high.Arguments[i] is float highFloat)
                    copy.Arguments[i] = lowFloat + (factor * (highFloat - lowFloat));
            }

            return copy;
        }

        private static List<ItemAction> LerpActions(List<ItemAction> low, List<ItemAction> high, float factor)
        {
            var result = new List<ItemAction>(low.Count);
            foreach (ItemAction lowAction in low)
            {
                ItemAction highAction = high.Find(a => a.ActionType == lowAction.ActionType) ?? lowAction;
                ItemAction copy = lowAction.Copy();
                int reqCount = Math.Min(copy.Requirements.Count, highAction.Requirements.Count);
                for (int i = 0; i < reqCount; i++)
                {
                    copy.Requirements[i].Value = LerpInt(
                        lowAction.Requirements[i].Value,
                        highAction.Requirements[i].Value,
                        factor);
                }

                result.Add(copy);
            }

            return result;
        }

        private static int LerpInt(int low, int high, float factor)
            => Convert.ToInt32(low + (factor * (high - low)));

        private static void ApplyStatsBlob(ItemTemplate definition, byte[]? statsBlob)
        {
            if (statsBlob == null || statsBlob.Length < 8)
                return;

            for (int offset = 0; offset + 8 <= statsBlob.Length; offset += 8)
            {
                var attrId = (CharacterStat)BitConverter.ToInt32(statsBlob, offset);
                int value = BitConverter.ToInt32(statsBlob, offset + 4);
                definition.Stats[attrId] = value;
            }
        }
    }
}
