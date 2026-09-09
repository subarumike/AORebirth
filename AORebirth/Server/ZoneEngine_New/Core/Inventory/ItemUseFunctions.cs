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
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.WorldSimulation;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>OnUse FunctionType implementations that ZoneEngine_New can run today.</summary>
    internal static class ItemUseFunctions
    {
        public static bool TryExecute(
            int templateId,
            Character target,
            Character? source,
            ItemSpell spell,
            IInventoryRepository inventoryRepository,
            IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(spell);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(items);

            switch ((FunctionType)spell.FunctionType)
            {
                case FunctionType.OpenBank:
                    return target is Player bankPlayer
                        && OpenBank(bankPlayer, inventoryRepository, items);
                case FunctionType.Hit:
                    return Hit(target, source, spell);
                case FunctionType.Set:
                    return Set(target, spell);
                case FunctionType.SetFlag:
                    return SetFlag(target, spell);
                case FunctionType.ClearFlag:
                    return ClearFlag(target, spell);
                case FunctionType.SystemText:
                case FunctionType.Text:
                    return target is Player textPlayer && SystemText(textPlayer, spell);
                case FunctionType.SaveChar:
                    return true;
                case FunctionType.UploadNano:
                    return target is Player uploadPlayer && UploadNano(uploadPlayer, spell);
                case FunctionType.TeleportProxy2:
                    return target is Player proxyPlayer && TeleportProxy2(proxyPlayer, spell);
                default:
                    LogUtil.Debug(
                        DebugInfoDetail.Network,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Unhandled OnUse FunctionType={0} template={1} character={2}",
                            spell.FunctionType,
                            templateId,
                            target.Identity.Instance));
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

        static bool Hit(Character target, Character? source, ItemSpell spell)
        {
            if (!TryGetInt(spell.Arguments, 0, out int statId) || !TryGetInt(spell.Arguments, 1, out int minHit))
                return false;

            // Hit args: Stat, Min, Max [, AC]. Collapsed Stat, Amount, ACType when Amount<0 and third>0.
            int maxHit = minHit;
            int acStat = 0;
            if (TryGetInt(spell.Arguments, 2, out int third))
            {
                maxHit = third;
                if (spell.Arguments.Count == 3 && minHit < 0 && third > 0)
                {
                    acStat = third;
                    maxHit = minHit;
                }
                else if (spell.Arguments.Count >= 4)
                {
                    if (TryGetInt(spell.Arguments, 3, out int fourth))
                        acStat = fourth;

                    if (minHit < 0 && maxHit > 0)
                        maxHit = minHit;
                }
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
                return ApplyHealthDelta(target, source, delta, acStat);

            if (stat == CharacterStat.CurrentNano || stat == CharacterStat.NanoPool)
                return ApplyNanoDelta(target, delta);

            target.Stats.Set(stat, target.Stats.GetOrZero(stat, StatDetail.Base) + delta, StatDetail.Base, dirty: true);
            target.FlushDirtyStats();
            return true;
        }

        static bool ApplyHealthDelta(Character target, Character? source, int delta, int acStat)
        {
            Character caster = source ?? target;

            if (delta < 0)
            {
                int before = Math.Max(0, target.Stats.GetOrZero(CharacterStat.Health));
                target.ApplyDamage(caster, -delta, HitType.Normal);
                target.FlushDirtyStats();

                int after = Math.Max(0, target.Stats.GetOrZero(CharacterStat.Health));
                int actual = before - after;
                if (actual > 0)
                    target.AnnounceHealthDamage(caster, after, -actual, acStat);

                return true;
            }

            int maxHealth = Math.Max(1, target.Stats.GetOrZero(CharacterStat.MaxHealth));
            int current = Math.Max(0, target.Stats.GetOrZero(CharacterStat.Health));
            int applied = Math.Min(delta, Math.Max(0, maxHealth - current));
            if (applied <= 0)
                return true;

            int healed = current + applied;
            target.Stats.Set(CharacterStat.Health, healed, StatDetail.Base, dirty: true);
            target.FlushDirtyStats();
            target.AnnounceHealthDamage(caster, healed, applied, damageTypeStat: 0);
            return true;
        }

        static bool ApplyNanoDelta(Character target, int delta)
        {
            int maxNano = Math.Max(0, target.Stats.GetOrZero(CharacterStat.MaxNanoEnergy));
            int current = Math.Max(0, target.Stats.GetOrZero(CharacterStat.CurrentNano));
            int next = delta >= 0
                ? current + Math.Min(delta, Math.Max(0, maxNano - current))
                : Math.Max(0, current + delta);

            if (next == current)
                return true;

            target.Stats.Set(CharacterStat.CurrentNano, next, StatDetail.Base, dirty: true);
            target.FlushDirtyStats();
            return true;
        }

        static bool Set(Character target, ItemSpell spell)
        {
            if (!TryGetInt(spell.Arguments, 0, out int statId) || !TryGetInt(spell.Arguments, 1, out int value))
                return false;

            target.Stats.Set((CharacterStat)statId, value, StatDetail.Base, dirty: true);
            target.FlushDirtyStats();
            return true;
        }

        static bool SetFlag(Character target, ItemSpell spell)
        {
            if (!TryGetInt(spell.Arguments, 0, out int statId) || !TryGetInt(spell.Arguments, 1, out int bitIndex))
                return false;

            if (bitIndex < 0 || bitIndex > 31)
                return false;

            var stat = (CharacterStat)statId;
            int current = target.Stats.GetOrZero(stat, StatDetail.Base);
            target.Stats.Set(stat, current | (1 << bitIndex), StatDetail.Base, dirty: true);
            target.FlushDirtyStats();
            return true;
        }

        static bool ClearFlag(Character target, ItemSpell spell)
        {
            if (!TryGetInt(spell.Arguments, 0, out int statId) || !TryGetInt(spell.Arguments, 1, out int bitIndex))
                return false;

            if (bitIndex < 0 || bitIndex > 31)
                return false;

            var stat = (CharacterStat)statId;
            int current = target.Stats.GetOrZero(stat, StatDetail.Base);
            target.Stats.Set(stat, current & ~(1 << bitIndex), StatDetail.Base, dirty: true);
            target.FlushDirtyStats();
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

        /// <summary>
        /// One-way proxy teleport used by Grid enter terminals and similar OnUse machines.
        /// Destination is packed as PlayfieldDoor (playfield + door index); the landing dynel may
        /// be a Door or a Terminal with that packed instance.
        /// </summary>
        static bool TeleportProxy2(Player player, ItemSpell spell)
        {
            if (player.Session == null || player.Playfield == null)
                return false;

            if (!PortalDoorLandingResolver.TryParseProxyDestination(
                    spell.Arguments,
                    PortalDoorLandingResolver.Proxy2EntryDoorClearance,
                    recordsReturn: false,
                    out PortalDestination destination))
                return false;

            Playfield source = player.Playfield;
            if (destination.PlayfieldId == source.Identity.Instance)
                return false;

            IGameData gameData = source.GetRequiredService<IGameData>();
            if (!PortalDoorLandingResolver.TryResolveProxyLanding(
                    gameData.GetPlayfieldGeometry(destination.PlayfieldId),
                    destination.DoorInstance,
                    destination.DoorClearance,
                    out Vector3 landing,
                    out Quaternion heading))
                return false;

            // TeleportProxy2 is one-way: clear any stale return door so exit proxies cannot pull
            // the character back to an unrelated entry.
            player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
            player.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
            player.Rotation = heading;

            Playfield destPlayfield = source.GetRequiredService<PlayfieldManager>()
                .GetOrCreate(destination.PlayfieldId);
            player.Session.TransferToPlayfield(destPlayfield, landing);
            return true;
        }

        static bool UploadNano(Player player, ItemSpell spell)
        {
            if (!TryGetInt(spell.Arguments, 0, out int nanoId) || nanoId <= 0)
                return false;

            // A nano already in the list is not uploaded again, and the crystal is not spent.
            if (!player.TryAddUploadedNano(nanoId))
            {
                AlreadyUploaded(player);
                return false;
            }

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

        static void AlreadyUploaded(Player player)
        {
            if (player.Session == null)
                return;

            player.Session.Send(
                new ChatTextMessage
                {
                    Identity = player.Identity,
                    Text = "You already know that nano program.",
                    Unknown1 = 0,
                    Unknown2 = 0,
                    Unknown3 = 0
                });
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
