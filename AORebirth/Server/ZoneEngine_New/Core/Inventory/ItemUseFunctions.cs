namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>OnUse FunctionType implementations that ZoneEngine_New can run today.</summary>
    internal static class ItemUseFunctions
    {
        public static bool TryExecute(
            int templateId,
            Player player,
            ItemSpell spell,
            IInventoryRepository inventoryRepository,
            IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(spell);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(items);

            switch ((FunctionType)spell.FunctionType)
            {
                case FunctionType.OpenBank:
                    return OpenBank(player, inventoryRepository, items);
                case FunctionType.Hit:
                    return Hit(player, spell);
                case FunctionType.Set:
                    return Set(player, spell);
                case FunctionType.SetFlag:
                    return SetFlag(player, spell);
                case FunctionType.ClearFlag:
                    return ClearFlag(player, spell);
                case FunctionType.SystemText:
                    return SystemText(player, spell);
                case FunctionType.SaveChar:
                    return true;
                case FunctionType.UploadNano:
                    return UploadNano(player, spell);
                default:
                    LogUtil.Debug(
                        DebugInfoDetail.Network,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Unhandled OnUse FunctionType={0} template={1} character={2}",
                            spell.FunctionType,
                            templateId,
                            player.Identity.Instance));
                    return false;
            }
        }

        static bool OpenBank(Player player, IInventoryRepository inventoryRepository, IItemBuilder items)
        {
            if (player.Session == null)
                return false;

            int characterId = player.Identity.Instance;
            player.Inventory.EnsureBankHydrated(characterId, inventoryRepository, items);
            player.Session.Send(player.Inventory.BuildBankMessage(player.Identity));
            return true;
        }

        static bool Hit(Player player, ItemSpell spell)
        {
            if (!TryGetInt(spell.Arguments, 0, out int statId) || !TryGetInt(spell.Arguments, 1, out int minHit))
                return false;

            int maxHit = minHit;
            if (TryGetInt(spell.Arguments, 2, out int third))
            {
                maxHit = third;
                if (spell.Arguments.Count == 3 && minHit < 0 && maxHit > 0)
                    maxHit = minHit;
                else if (spell.Arguments.Count >= 4 && minHit < 0 && maxHit > 0)
                    maxHit = minHit;
            }

            if (minHit > maxHit)
            {
                int swap = minHit;
                minHit = maxHit;
                maxHit = swap;
            }

            int delta = minHit == maxHit
                ? minHit
                : Random.Shared.Next(minHit, maxHit + 1);

            var stat = (CharacterStat)statId;
            if (stat == CharacterStat.Health)
                return ApplyHealthDelta(player, delta);

            if (stat == CharacterStat.CurrentNano || stat == CharacterStat.NanoPool)
                return ApplyNanoDelta(player, delta);

            player.Stats.Set(stat, player.Stats.GetOrZero(stat, StatDetail.Base) + delta, StatDetail.Base, dirty: true);
            player.FlushDirtyStats();
            return true;
        }

        static bool ApplyHealthDelta(Player player, int delta)
        {
            if (delta < 0)
            {
                player.ApplyDamage(player, -delta, HitType.Normal);
                player.FlushDirtyStats();
                return true;
            }

            int maxHealth = Math.Max(1, player.Stats.GetOrZero(CharacterStat.MaxHealth));
            int current = Math.Max(0, player.Stats.GetOrZero(CharacterStat.Health));
            int applied = Math.Min(delta, Math.Max(0, maxHealth - current));
            if (applied <= 0)
                return true;

            player.Stats.Set(CharacterStat.Health, current + applied, StatDetail.Base, dirty: true);
            player.FlushDirtyStats();

            if (player.Session != null)
            {
                player.Session.Send(
                    new ChatTextMessage
                    {
                        Identity = player.Identity,
                        Text = string.Format(
                            CultureInfo.InvariantCulture,
                            "You healed yourself for {0} points.",
                            applied),
                        Unknown1 = 0,
                        Unknown2 = 0,
                        Unknown3 = 0
                    });
            }

            return true;
        }

        static bool ApplyNanoDelta(Player player, int delta)
        {
            int maxNano = Math.Max(0, player.Stats.GetOrZero(CharacterStat.MaxNanoEnergy));
            int current = Math.Max(0, player.Stats.GetOrZero(CharacterStat.CurrentNano));
            int next = delta >= 0
                ? current + Math.Min(delta, Math.Max(0, maxNano - current))
                : Math.Max(0, current + delta);

            if (next == current)
                return true;

            player.Stats.Set(CharacterStat.CurrentNano, next, StatDetail.Base, dirty: true);
            player.FlushDirtyStats();
            return true;
        }

        static bool Set(Player player, ItemSpell spell)
        {
            if (!TryGetInt(spell.Arguments, 0, out int statId) || !TryGetInt(spell.Arguments, 1, out int value))
                return false;

            player.Stats.Set((CharacterStat)statId, value, StatDetail.Base, dirty: true);
            player.FlushDirtyStats();
            return true;
        }

        static bool SetFlag(Player player, ItemSpell spell)
        {
            if (!TryGetInt(spell.Arguments, 0, out int statId) || !TryGetInt(spell.Arguments, 1, out int bitIndex))
                return false;

            if (bitIndex < 0 || bitIndex > 31)
                return false;

            var stat = (CharacterStat)statId;
            int current = player.Stats.GetOrZero(stat, StatDetail.Base);
            player.Stats.Set(stat, current | (1 << bitIndex), StatDetail.Base, dirty: true);
            player.FlushDirtyStats();
            return true;
        }

        static bool ClearFlag(Player player, ItemSpell spell)
        {
            if (!TryGetInt(spell.Arguments, 0, out int statId) || !TryGetInt(spell.Arguments, 1, out int bitIndex))
                return false;

            if (bitIndex < 0 || bitIndex > 31)
                return false;

            var stat = (CharacterStat)statId;
            int current = player.Stats.GetOrZero(stat, StatDetail.Base);
            player.Stats.Set(stat, current & ~(1 << bitIndex), StatDetail.Base, dirty: true);
            player.FlushDirtyStats();
            return true;
        }

        static bool SystemText(Player player, ItemSpell spell)
        {
            if (player.Session == null || !TryGetString(spell.Arguments, 0, out string text) || text.Length == 0)
                return false;

            player.Session.Send(
                new ChatTextMessage
                {
                    Identity = player.Identity,
                    Text = text,
                    Unknown1 = 0,
                    Unknown2 = 0,
                    Unknown3 = 0
                });
            return true;
        }

        static bool UploadNano(Player player, ItemSpell spell)
        {
            if (!TryGetInt(spell.Arguments, 0, out int nanoId) || nanoId <= 0)
                return false;

            if (!player.TryAddUploadedNano(nanoId))
                return true;

            player.MarkUploadedNanoDirty(nanoId);
            player.Playfield?.GetRequiredService<InventoryFlushService>().NotifyDirty(player);

            if (player.Session == null)
                return true;

            player.Session.Send(
                new CharacterActionMessage
                {
                    Identity = player.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.UploadNano,
                    Unknown1 = 0,
                    Target = player.Identity,
                    Parameter1 = (int)IdentityType.NanoProgram,
                    Parameter2 = nanoId,
                    Unknown2 = 0
                });
            return true;
        }

        static bool TryGetInt(System.Collections.Generic.List<object> arguments, int index, out int result)
        {
            result = 0;
            if (arguments == null || index < 0 || index >= arguments.Count)
                return false;

            switch (arguments[index])
            {
                case int i:
                    result = i;
                    return true;
                case long l:
                    result = (int)l;
                    return true;
                case uint u:
                    result = (int)u;
                    return true;
                case short s:
                    result = s;
                    return true;
                case byte b:
                    result = b;
                    return true;
                default:
                    return false;
            }
        }

        static bool TryGetString(System.Collections.Generic.List<object> arguments, int index, out string result)
        {
            result = string.Empty;
            if (arguments == null || index < 0 || index >= arguments.Count)
                return false;

            if (arguments[index] is string text)
            {
                result = text;
                return true;
            }

            return false;
        }
    }
}
