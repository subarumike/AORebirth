namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    using AORebirth.Core.GameData;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.WorldSimulation;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;
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
            IItemBuilder items,
            SpellCriteria? criteria = null,
            bool isTick = false,
            int templateDamageType = 0,
            ItemTemplate? sourceTemplate = null)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(spell);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(items);

            // Add Damage applies to a direct hit as it lands, never to a timed nano's later ticks.
            bool addDamage = !isTick;
            switch ((FunctionType)spell.FunctionType)
            {
                case FunctionType.OpenBank:
                    return target is Player bankPlayer
                        && OpenBank(bankPlayer, inventoryRepository, items);
                case FunctionType.Hit:
                    return Hit(target, source, spell, addDamage);
                // SpecialHit (perk attacks such as Pulverize [Health, -72, -145, 0]) carries Hit's arguments; with
                // no damage type of its own it hits as the template's DamageType (Pulverize 436 = MeleeAC, which
                // live HealthDamage reports as type 91).
                case FunctionType.SpecialHit:
                    return Hit(target, source, spell, addDamage, templateDamageType);
                case FunctionType.AreaHit:
                    return AreaHit(target, source, spell, addDamage);
                case FunctionType.DrainHit:
                    return DrainHit(target, source, spell, addDamage);
                case FunctionType.Set:
                    return Set(target, spell);
                case FunctionType.SetFlag:
                    return SetFlag(target, spell);
                case FunctionType.ClearFlag:
                    return ClearFlag(target, spell);
                case FunctionType.SystemText:
                case FunctionType.Text:
                    // The text goes to whoever the function names: a perk attack's Wearer text ("You successfully
                    // perform ...") belongs to the attacker, not the target it hit.
                    return ResolveApplyOn(target, source, spell) is not Player textPlayer || SystemText(textPlayer, spell);
                case FunctionType.SaveChar:
                    // Insurance terminal (ICC Cell Structure Scanner 300813 and the like): fee, save point, XP saved.
                    return (source as Player ?? target as Player) is Player saver && saver.InsuranceSave();
                case FunctionType.LockSkill:
                    return LockSkill(target, source, spell);
                case FunctionType.TimedEffect:
                    return TimedEffect(templateId, target, source, spell);
                case FunctionType.LockPerk:
                    return LockPerk(target, source, spell);
                case FunctionType.UploadNano:
                    return target is Player uploadPlayer && UploadNano(uploadPlayer, spell);
                case FunctionType.CastNano:
                case FunctionType.AreaCastNano:
                case FunctionType.TeamCastNano:
                case FunctionType.PlayfieldNano:
                case FunctionType.CastOnPets:
                    return NanoCastFunctions.TryExecute(target, source, spell, items, inventoryRepository, NamesNpc(spell));
                case FunctionType.NpcSayRobotSpeech:
                    // [text key] such as {stock_pet_init}: the key's text is not known here, and it is only a line
                    // the NPC says. Counts as done so the functions after it run as live's do.
                    return true;
                case FunctionType.Teleport:
                case FunctionType.TeleportOut:
                    return target is Player teleportPlayer && Teleport(teleportPlayer, spell);
                case FunctionType.LineTeleport:
                    return target is Player linePlayer && LineTeleport(linePlayer, spell);
                case FunctionType.TeleportProxy:
                    return target is Player proxyDoorPlayer && TeleportProxy(proxyDoorPlayer, spell, criteria);
                case FunctionType.TeleportProxy2:
                    return target is Player proxyPlayer && TeleportProxy2(proxyPlayer, spell);
                case FunctionType.SpawnMonster2:
                    return SpawnMonster2(target, spell);
                case FunctionType.SummonPet:
                    return SummonPet(target, spell, sourceTemplate);
                case FunctionType.DestroyItem:
                    return DestroySubject(target, criteria);
                case FunctionType.ToggleFlag:
                    return ToggleFlag(target, spell);
                case FunctionType.CastNanoIfPossible:
                case FunctionType.NpcCastNanoIfPossible:
                case FunctionType.CastNanoIfPossibleOnFightTarget:
                case FunctionType.NpcCastNanoIfPossibleOnFightTarget:
                    return NanoCastFunctions.TryExecute(target, source, spell, items, inventoryRepository);
                case FunctionType.CastChance:
                    return CastChance(target, source, spell, items, inventoryRepository, criteria);
                case FunctionType.AddOffProc:
                case FunctionType.AddDefProc:
                    // Registrations, not effects: ProcRules reads them from the buff, worn item or weapon on each
                    // weapon hit. Landing the buff (or a weapon's OnHit) must not fire them.
                    return true;
                case FunctionType.RemoveNano:
                    return RemoveNano(target, spell);
                case FunctionType.RemoveNanoStrain:
                    return RemoveNanoStrain(target, spell);
                case FunctionType.ReduceNanoStrainDuration:
                    return ReduceNanoStrainDuration(target, spell);
                case FunctionType.CharmNpc:
                    return CharmNpc(templateId, target, source, sourceTemplate);
                case FunctionType.RemoveBuffs:
                    return NanoRuntime.StripAllBuffs(target);
                case FunctionType.SpawnItem:
                    return SpawnItem(target, spell);
                case FunctionType.NpcSocialAnim:
                    return NpcSocialAnim(target, spell);
                case FunctionType.NpcWipeHateList:
                    return NpcWipeHateList(target);
                case FunctionType.TauntNpc:
                    return TauntNpc(target, source, spell);
                case FunctionType.NpcStopMoving:
                    return NpcStopMoving(target);
                case FunctionType.NpcTeleportToSpawnPoint:
                    return NpcTeleportToSpawnPoint(target);
                case FunctionType.NpcFightSelected:
                    return NpcFightSelected(target, source);
                case FunctionType.SolveQuest:
                    return SolveQuest(target, source, spell);
                case FunctionType.SpawnQuest:
                    return ResolveApplyOn(target, source, spell) is Player questPlayer && SpawnQuest(questPlayer, spell);
                case FunctionType.FailQuestByQas:
                    return ResolveApplyOn(target, source, spell) is Player failPlayer && FailQuest(failPlayer, spell);
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

        static bool Hit(Character target, Character? source, ItemSpell spell, bool addDamage, int defaultAcStat = 0)
        {
            if (!spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out int minHit))
                return false;

            // Hit args: Stat, Min, Max [, AC]. Collapsed Stat, Amount, ACType when Amount<0 and third>0.
            int maxHit = minHit;
            int acStat = 0;
            if (spell.TryReadInt(2, out int third))
            {
                maxHit = third;
                if (spell.ArgumentCount == 3 && minHit < 0 && third > 0)
                {
                    acStat = third;
                    maxHit = minHit;
                }
                else if (spell.ArgumentCount >= 4)
                {
                    if (spell.TryReadInt(3, out int fourth))
                        acStat = fourth;

                    if (minHit < 0 && maxHit > 0)
                        maxHit = minHit;
                }
            }

            if (acStat == 0)
                acStat = defaultAcStat;

            var stat = (CharacterStat)statId;

            // Max 0 with a nonzero Min is a percentage of the stat's maximum: Complete Healing [27,100,0,0] heals fully,
            // Minor Regeneration of Health and Nano [27,5,0,91] / [214,5,0,91] restores 5%, Wall Hacks! [27,-30,0,91]
            // takes 30%. The damage type still names the hit; caster Add Damage does not apply to a percentage.
            if (spell.ArgumentCount >= 3 && third == 0 && minHit != 0
                && TryPercentOfMax(target, stat, minHit, out int percentDelta))
            {
                if (stat == CharacterStat.Health)
                    return ApplyHealthDelta(target, source, percentDelta, acStat, addDamage: false, direct: addDamage);
                return ApplyNanoDelta(target, percentDelta);
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

            if (stat == CharacterStat.Health)
                return ApplyHealthDelta(target, source, delta, acStat, addDamage, direct: addDamage);

            if (stat == CharacterStat.CurrentNano || stat == CharacterStat.NanoPool)
                return ApplyNanoDelta(target, delta);

            target.Stats.Set(stat, target.Stats.GetOrZero(stat, StatDetail.Base) + delta, StatDetail.Base, dirty: true);
            return true;
        }

        /// <summary>
        /// DrainHit args: Stat, Min, Max, AC, Percent (Shade Dimach 213269-213274: Health, Melee AC, 70-80).
        /// Damages <paramref name="target"/> like a hostile Hit, then heals the source by Percent of the
        /// damage actually dealt. Only Health damage is supported.
        /// </summary>
        static bool DrainHit(Character target, Character? source, ItemSpell spell, bool addDamage)
        {
            if (!spell.TryReadInt(0, out int statId) || (CharacterStat)statId != CharacterStat.Health
                || !spell.TryReadInt(1, out int minHit) || !spell.TryReadInt(2, out int maxHit))
                return false;
            spell.TryReadInt(3, out int acStat);
            spell.TryReadInt(4, out int percent);
            if (minHit > maxHit)
                (minHit, maxHit) = (maxHit, minHit);
            if (maxHit >= 0 || source == null || ReferenceEquals(source, target) || target.IsDead)
                return false;

            int before = Math.Max(0, target.Stats.GetOrZero(CharacterStat.Health));
            ApplyHealthDelta(target, source, Random.Shared.Next(minHit, maxHit + 1), acStat, addDamage, direct: addDamage);
            int dealt = before - Math.Max(0, target.Stats.GetOrZero(CharacterStat.Health));
            int drained = (int)((long)dealt * Math.Clamp(percent, 0, 100) / 100);
            // The drain returns a share of the damage dealt; healing efficiency and reactivity do not change it.
            if (drained > 0 && !source.IsDead)
                ApplyHealthDelta(source, source, drained, 0, healModifiers: false);
            return true;
        }

        /// <summary>
        /// AreaHit args: Stat, Min, Max, AC, Radius (item 129650: Health -40..-141 Chemical, 3 m).
        /// Damages <paramref name="center"/> (the function's ApplyOn character) and everything the
        /// user may attack within Radius metres of it, each with its own roll. Only hostile Health hits.
        /// </summary>
        static bool AreaHit(Character center, Character? source, ItemSpell spell, bool addDamage)
        {
            if (!spell.TryReadInt(0, out int statId) || (CharacterStat)statId != CharacterStat.Health
                || !spell.TryReadInt(1, out int minHit) || !spell.TryReadInt(2, out int maxHit)
                || !spell.TryReadInt(4, out int radius) || radius < 0)
                return false;
            spell.TryReadInt(3, out int acStat);
            if (minHit > maxHit)
                (minHit, maxHit) = (maxHit, minHit);
            if (maxHit >= 0)
                return false;

            Character caster = source ?? center;
            Playfield? playfield = center.Playfield;
            DynelRegistry? registry = playfield?.GetService<DynelRegistry>();
            if (registry == null)
                return false;

            foreach (Dynel dynel in registry.Dynels())
            {
                if (dynel is not Character candidate || candidate.IsDead || ReferenceEquals(candidate, caster)
                    || !ReferenceEquals(candidate.Playfield, playfield)
                    || center.Distance3D(candidate) > radius
                    || !Helpers.CombatRules.CanAttack(caster, candidate)
                    // The blast reaches only what its centre can see: a wall or a closed door shelters the candidate.
                    || !center.HasLineOfSightTo(candidate))
                    continue;

                ApplyHealthDelta(candidate, caster, Random.Shared.Next(minHit, maxHit + 1), acStat, addDamage, direct: addDamage);
            }

            return true;
        }

        /// <summary>
        /// Weapon OnHit proc (item 205012): nano id, then chance percent. A roll of 1–100
        /// lands the nano when it is less than or equal to the chance. ApplyOn selects
        /// the recipient; <see cref="ItemTarget.Target"/> is the character who was hit.
        /// </summary>
        static bool CastChance(
            Character target,
            Character? source,
            ItemSpell spell,
            IItemBuilder items,
            IInventoryRepository inventory,
            SpellCriteria? criteria)
        {
            if (!spell.TryReadInt(0, out int nanoId) || nanoId <= 0)
                return false;
            if (!spell.TryReadInt(1, out int chance))
                return false;
            if (chance <= 0)
                return true;

            if (chance > SpellCriteria.RollMaxInclusive)
                chance = SpellCriteria.RollMaxInclusive;

            criteria ??= new SpellCriteria();
            int roll = criteria.Resolve(CharacterStat.Rnd, static _ => 0);
            if (roll > chance)
                return true;

            Character caster = source ?? target;
            Character recipient = ResolveApplyOn(target, source, spell);
            return NanoRuntime.TryApplyImmediate(
                caster,
                recipient,
                nanoId,
                items,
                inventory,
                DateTime.UtcNow);
        }

        /// <summary>
        /// Character a function's ApplyOn names: User / Wearer / Self is whoever used the item or cast
        /// the nano, Target is the event target.
        /// </summary>
        internal static Character ResolveApplyOn(Character eventTarget, Character? source, ItemSpell spell)
        {
            switch ((ItemTarget)spell.Target)
            {
                case ItemTarget.User:
                case ItemTarget.Wearer:
                case ItemTarget.Self:
                    return source ?? eventTarget;
                case ItemTarget.Fightingtarget:
                    Character? fighting = source?.TryResolveFightingTarget();
                    return fighting ?? eventTarget;
                default:
                    return eventTarget;
            }
        }

        /// <summary>
        /// A Hit on Health. <paramref name="direct"/> is a landing hit rather than a timed tick; <paramref name="addDamage"/>
        /// lets the caster's Add Damage in (not for a percentage hit). Nano or item alike: direct damage on someone else gains
        /// the caster's Direct Nano Damage Efficiency (536) percent before Add Damage, a direct heal the caster's Healing
        /// Efficiency (535) percent, and every heal, ticks included, the recipient's Heal Reactivity (689) percent.
        /// <paramref name="healModifiers"/> false (a drain's own heal) leaves a heal as it is.
        /// </summary>
        static bool ApplyHealthDelta(Character target, Character? source, int delta, int acStat, bool addDamage = false,
            bool direct = false, bool healModifiers = true)
        {
            Character caster = source ?? target;

            if (delta < 0)
            {
                // A hostile nano hit from someone else can break crowd control before its threat lands.
                if (!ReferenceEquals(caster, target))
                {
                    // A hostile nano hit (a nuke or a damage tick) gives a sneaking target away.
                    target.BreakSneak();
                    target.RollBuffBreaks(BuffBreakCause.SpellAttack, caster);

                    if (direct)
                        delta = -ScaleByPercent(-delta, caster, CharacterStat.NanoDamageMultiplier);

                    // Direct hits (nukes, DrainHit, AreaHit) gain the caster's Add Damage for their damage type. It can be
                    // negative (retail); the hit still does at least 1, as a weapon hit does.
                    if (addDamage && Helpers.DamageCalculator.TryGetAddDamageStat(acStat, out CharacterStat addDamageStat))
                        delta = Math.Min(-1, delta - StatCollection.Normalize(caster.Stats.GetOrZero(addDamageStat)));
                }

                // The target's reflect for the hit's damage type (its AC stat id) applies to nano damage too; a damage
                // shield answers weapon hits only.
                int damage = DamageReturn.Resolve(caster, target, caster.ScaleOutgoingDamage(target, -delta), acStat, shield: false,
                    out DamageReturn.Returned returned);
                DamageReturn.Announce(caster, target, returned);
                int before = Math.Max(0, target.Stats.GetOrZero(CharacterStat.Health));
                target.ApplyDamage(caster, damage, HitType.Normal);

                int after = Math.Max(0, target.Stats.GetOrZero(CharacterStat.Health));
                int actual = before - after;
                // HealthDamage carries post-hit HP; clear dirty Health so Tick's Stat flush
                // does not also print a second "unknown damage" line.
                target.Stats.ClearDirty(CharacterStat.Health);
                if (actual > 0)
                    target.AnnounceHealthDamage(caster, after, -actual, acStat);

                DamageReturn.Apply(caster, target, returned);
                return true;
            }

            if (healModifiers)
            {
                if (direct)
                    delta = ScaleByPercent(delta, caster, CharacterStat.HealMultiplier);
                delta = ScaleByPercent(delta, target, CharacterStat.HealReactivity);
            }

            int maxHealth = Math.Max(1, target.Stats.GetOrZero(CharacterStat.MaxHealth));
            int current = Math.Max(0, target.Stats.GetOrZero(CharacterStat.Health));
            int applied = Math.Min(delta, Math.Max(0, maxHealth - current));
            if (applied <= 0)
                return true;

            int healed = current + applied;
            target.Stats.Set(CharacterStat.Health, healed, StatDetail.Base, dirty: true);
            target.Stats.ClearDirty(CharacterStat.Health);
            target.AnnounceHealthDamage(caster, healed, applied, damageTypeStat: 0);
            return true;
        }

        /// <summary><paramref name="amount"/> raised (or lowered) by <paramref name="owner"/>'s percent stat; never below 0.</summary>
        static int ScaleByPercent(int amount, Character owner, CharacterStat percentStat)
        {
            int percent = StatCollection.Normalize(owner.Stats.GetOrZero(percentStat));
            return percent == 0 ? amount : (int)Math.Max(0, (long)amount * (100 + percent) / 100);
        }

        /// <summary>
        /// <paramref name="percent"/> of the target's MaxHealth (Health) or MaxNanoEnergy (CurrentNano / NanoPool),
        /// at least 1 point either way. False for any other stat, which keeps the flat Hit roll.
        /// </summary>
        static bool TryPercentOfMax(Character target, CharacterStat stat, int percent, out int delta)
        {
            delta = 0;
            CharacterStat maxStat;
            if (stat == CharacterStat.Health)
                maxStat = CharacterStat.MaxHealth;
            else if (stat is CharacterStat.CurrentNano or CharacterStat.NanoPool)
                maxStat = CharacterStat.MaxNanoEnergy;
            else
                return false;

            long max = Math.Max(0, target.Stats.GetOrZero(maxStat));
            long amount = Math.Max(1, max * Math.Abs(percent) / 100);
            delta = (int)Math.Min(int.MaxValue, amount) * Math.Sign(percent);
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
            return true;
        }

        /// <summary>
        /// TimedEffect [stat, amount, duration in centiseconds]: the stat gains the amount until the duration runs out
        /// (Computer Literacy Tutoring Device 55686: [161, 50, 12000] = +50 Computer Literacy for 2 minutes). Not
        /// saved: a logout ends it.
        /// </summary>
        static bool TimedEffect(int templateId, Character target, Character? source, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out int amount)
                || !spell.TryReadInt(2, out int durationCentiseconds) || durationCentiseconds <= 0 || amount == 0)
                return false;

            var stat = (CharacterStat)statId;
            if (stat is CharacterStat.Health or CharacterStat.Cash)
                return false;

            ResolveApplyOn(target, source, spell).AddTimedEffect(
                templateId, stat, amount, DateTime.UtcNow.AddMilliseconds(durationCentiseconds * 10.0));
            return true;
        }

        /// <summary>LockSkill args: Stat, Value (seconds). The leading Action word is not stored in items.dat.</summary>
        internal static bool TryReadSkillLock(ItemSpell spell, out int statId, out int durationSeconds)
        {
            durationSeconds = 0;
            return spell.TryReadInt(0, out statId) && spell.TryReadInt(1, out durationSeconds);
        }

        static bool LockSkill(Character target, Character? source, ItemSpell spell)
        {
            if (!TryReadSkillLock(spell, out int statId, out int durationSeconds))
                return false;

            Character locked = ResolveApplyOn(target, source, spell);
            int seconds = locked.LockSkill(statId, durationSeconds, DateTime.UtcNow);

            // Health and Nano Stim LockSkill [123, 40] shows as SpecialUsed, then SpecialAvailable when it runs out.
            // The client scales the duration by SkillLockModifier itself: it is told the scripted one.
            if (locked is Player player)
                player.ShowSkillLock(statId, durationSeconds, seconds);

            return true;
        }

        /// <summary>
        /// LockPerk [3, perk id, seconds]: the perk's actions stay unusable (IsPerkUnlocked fails) until the lock
        /// expires, e.g. Heal [3, 401, 240] on use and [3, 401, 20] on failure. Only players hold perks.
        /// </summary>
        internal static bool TryReadPerkLock(ItemSpell spell, out int perkId, out int durationSeconds)
        {
            durationSeconds = 0;
            return spell.TryReadInt(1, out perkId) && spell.TryReadInt(2, out durationSeconds);
        }

        static bool LockPerk(Character target, Character? source, ItemSpell spell)
        {
            if (!TryReadPerkLock(spell, out int perkId, out int durationSeconds))
                return false;

            if (ResolveApplyOn(target, source, spell) is Player player)
                player.LockPerk(perkId, durationSeconds, DateTime.UtcNow);
            return true;
        }

        static bool Set(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out int value))
                return false;

            target.Stats.Set((CharacterStat)statId, value, StatDetail.Base, dirty: true);
            return true;
        }

        static bool SetFlag(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out int bitIndex))
                return false;

            if (bitIndex < 0 || bitIndex > 31)
                return false;

            var stat = (CharacterStat)statId;
            int current = target.Stats.GetOrZero(stat, StatDetail.Base);
            target.Stats.Set(stat, current | (1 << bitIndex), StatDetail.Base, dirty: true);
            return true;
        }

        static bool ClearFlag(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out int bitIndex))
                return false;

            if (bitIndex < 0 || bitIndex > 31)
                return false;

            var stat = (CharacterStat)statId;
            int current = target.Stats.GetOrZero(stat, StatDetail.Base);
            target.Stats.Set(stat, current & ~(1 << bitIndex), StatDetail.Base, dirty: true);
            return true;
        }

        static bool ToggleFlag(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out int bitIndex))
                return false;

            if (bitIndex < 0 || bitIndex > 31)
                return false;

            var stat = (CharacterStat)statId;
            int current = target.Stats.GetOrZero(stat, StatDetail.Base);
            target.Stats.Set(stat, current ^ (1 << bitIndex), StatDetail.Base, dirty: true);
            return true;
        }

        static bool RemoveNano(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int nanoId))
                return false;

            return NanoRuntime.TryStripNano(target, nanoId);
        }

        static bool RemoveNanoStrain(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int strain))
                return false;

            return NanoRuntime.TryStripStrain(target, strain);
        }

        /// <summary>
        /// Catalog shape: strain, seconds. The target's NCU entries of that strain end that much sooner; one this runs
        /// out leaves NCU. Free Movement 204104 takes root (146) down by 15 s, snare (145) by 150 s and 147 by 30 s; a
        /// charm's OnTerminate (231008: strain 225, 2000000) clears its line outright.
        /// </summary>
        static bool ReduceNanoStrainDuration(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int strain) || !spell.TryReadInt(1, out int seconds))
                return false;

            return NanoRuntime.ReduceStrainDuration(target, strain, seconds);
        }

        /// <summary>
        /// No arguments: the target NPC serves the caster as a pet while the charm nano (<paramref name="templateId"/>,
        /// already in the target's NCU when its OnUse runs) lasts. Its pet type is the mindcontrolled place the nano's
        /// TestNumPets lets it fill (<see cref="Pets.PetTypes.CharmType"/>: 99192 7000, 99209 7001). The PetType stat
        /// the nano's ChangeVariable sets (12 / 11) is the client's pet kind and stays as the data has it. Over-equip
        /// judges the caster against the nano's ToUse rows, as for a summoned pet.
        /// </summary>
        static bool CharmNpc(int templateId, Character target, Character? source, ItemTemplate? sourceTemplate)
        {
            if (target is not NpcCharacter npc || source is not Player owner || npc.Playfield == null)
                return false;

            IReadOnlyList<ItemRequirement> requirements =
                sourceTemplate?.Actions.Find(action => action.ActionType == (int)ActionType.ToUse)?.Requirements ?? [];
            return npc.Playfield.GetRequiredService<Pets.PetService>()
                .Charm(owner, npc, templateId, Pets.PetTypes.CharmType(requirements), requirements);
        }

        /// <summary>
        /// Catalog shape: hash, quality, and a 0/1 third integer. Both third values grant one item; any other third
        /// value is refused. The item is the hash's highest QL at or below the requested quality
        /// (<see cref="HashItemMinter.TryMintHighestUpTo"/>; the quality scales with the casting item's QL). A player
        /// gets it in inventory; an NPC carries it and its corpse drops it alongside the item table loot.
        /// </summary>
        /// <summary>SpawnItem Flags: the item goes to the user's inventory rather than the world.</summary>
        const int SpawnItemIntoInventory = 1;

        /// <summary>SpawnItem Flags: RelX/Y/Z is a playfield position, not an offset from the user.</summary>
        const int SpawnItemAbsolutePosition = 4;

        static bool SpawnItem(Character target, ItemSpell spell)
        {
            if (!spell.TryReadString(0, out string hash) || hash.Length == 0)
                return false;
            if (!spell.TryReadInt(1, out int quality) || quality <= 0)
                return false;
            if (spell.TryReadInt(2, out int mode) && mode is not 0 and not 1)
                return false;

            if (target is NpcCharacter npc)
            {
                if (npc.Playfield == null
                    || !npc.Playfield.GetRequiredService<HashItemMinter>().TryMintHighestUpTo(hash, quality, ItemSource.Loot, out Item carried))
                {
                    return false;
                }

                return npc.TryCarryLoot(carried);
            }

            if (target is not Player player || player.Playfield == null || player.Session == null)
                return false;

            if (!player.Playfield.GetRequiredService<HashItemMinter>().TryMintHighestUpTo(hash, quality, ItemSource.Other, out Item item))
                return false;

            // Other outcomes were possible (the use was not refused up front), but this roll is a Unique already held.
            if (Trade.TradeRules.IsUnique(item) && Trade.TradeRules.WouldDuplicateUnique(player, item.LowId, item.HighId))
            {
                ClientFeedback.SendFormatted(player, ClientFeedback.AlreadyHaveUniqueItem, item.Name);
                return true;
            }

            // Arguments: hash, max QL, min QL, TimeExist (seconds), Flags, RelX/Y/Z. Flags bit 1 puts the item in the
            // inventory (nano 160979, Bronze Chest of the Collector 286438: 1 and 3); without it the item is placed in the
            // world for TimeExist (Bonfire Kit 223773 and Portable Market Interface 257958: 0, at the user plus Rel;
            // Laser Tagging Device 259782: 8). Bit 4 makes Rel an absolute position (Unicorn Scriptstarter 257352).
            // An items.dat without these arguments keeps the inventory behaviour.
            if (spell.TryReadInt(4, out int spawnFlags) && (spawnFlags & SpawnItemIntoInventory) == 0)
            {
                spell.TryReadInt(3, out int timeExist);
                spell.TryReadInt(5, out int relX);
                spell.TryReadInt(6, out int relY);
                spell.TryReadInt(7, out int relZ);
                AORebirth.Core.Vector.Vector3 at = player.Position;
                var position = (spawnFlags & SpawnItemAbsolutePosition) != 0
                    ? new AORebirth.Core.Vector.Vector3(relX, relY, relZ)
                    : new AORebirth.Core.Vector.Vector3(at.x + relX, at.y + relY, at.z + relZ);
                player.Playfield.GetRequiredService<SpawnService>().SpawnDroppedItem(
                    item,
                    position,
                    player.Rotation,
                    player.Identity,
                    timeExist > 0 ? TimeSpan.FromSeconds(timeExist) : WorldItemTransfers.DroppedItemLifetime);
                return true;
            }

            if (InventoryStacking.TryGrantOntoStack(player, item, player.Playfield.GetService<InventoryFlushService>()))
                return true;
            if (!player.Inventory.TryPlace(item, out Container page, out int slot))
                return false;

            player.Inventory.MarkDirty(item, page, slot);
            player.Playfield.GetService<InventoryFlushService>()?.NotifyDirty(player);
            if (ReferenceEquals(page, player.Inventory.Overflow))
            {
                // A full inventory: it waits in overflow, which keeps only its newest items.
                OverflowGrants.Send(player, item);
                OverflowGrants.Trim(player);
                return true;
            }

            player.Session.Send(
                new AddTemplateMessage
                {
                    Identity = player.Identity,
                    HighId = item.HighId,
                    LowId = item.LowId,
                    Quality = item.Quality,
                    Count = item.StackCount
                });
            return true;
        }

        /// <summary>
        /// Catalog shape is one animation id. <see cref="CharacterActionType.NpcSocialAnim"/>
        /// carries that id in <c>Target.Instance</c>.
        /// </summary>
        static bool NpcSocialAnim(Character target, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int animId) || animId <= 0)
                return false;

            var message = new CharacterActionMessage
            {
                Identity = target.Identity,
                Unknown = 0,
                Action = CharacterActionType.NpcSocialAnim,
                Target = new Identity { Type = IdentityType.None, Instance = animId }
            };

            if (target.Cell != null)
                target.Cell.Announce(message);
            else if (target is Player player)
                player.Session?.Send(message);

            return true;
        }

        static bool NpcWipeHateList(Character target)
        {
            if (target is not NpcCharacter npc || npc.Brain == null)
                return false;

            npc.Brain.StopFighting();
            npc.Brain.ClearHate();
            return true;
        }

        /// <summary>
        /// TauntNpc args: Amount. The NPC hates whoever taunted it by that much, on the same scale as damage taken
        /// (Mongo Slam! 100194: 2000 to 4000 by the caster's skill). Threat comes from the same sources as damage
        /// threat (NpcCharacter.OnDamaged): a player or a player's pet. True means the taunt was
        /// delivered; a pacified or evading NPC still keeps no hate. Negative amounts (detaunts) are not implemented.
        /// </summary>
        static bool TauntNpc(Character target, Character? source, ItemSpell spell)
        {
            if (target is not NpcCharacter npc || npc.Brain == null || source == null || ReferenceEquals(source, npc))
                return false;
            if (!source.IsPlayer && source is not NpcCharacter { PetOwner: Player })
                return false;
            if (!spell.TryReadInt(0, out int amount) || amount <= 0)
                return false;

            npc.Brain.AddThreatFrom(source, amount);
            return true;
        }

        static bool NpcStopMoving(Character target)
        {
            if (target is not NpcCharacter npc)
                return false;

            if (npc.Brain != null)
                npc.Brain.StopPathing();
            else
                npc.Motor.ClearPath();
            return true;
        }

        static bool NpcTeleportToSpawnPoint(Character target)
        {
            if (target is not NpcCharacter npc || npc.Brain is not NpcBrain brain || brain.Home is not Vector3 home)
                return false;

            brain.StopPathing();
            npc.Motor.Warp(home);
            return true;
        }

        /// <summary>
        /// True when the function's requirements name its target NPC by spawn hash (Operator.IsNpcHash): it is written
        /// for that NPC, a quest effect rather than an attack (OTAF Official Notice 297243 on "OIUR").
        /// </summary>
        static bool NamesNpc(ItemSpell spell)
            => spell.Requirements.Exists(r => r.Operator == (int)Operator.IsNpcHash);

        /// <summary>
        /// SolveQuest [hash]: completes the user's active quest of that hash (or template alias) as an NPC turn-in does.
        /// Health Regeneration Stim 297044 used on a Wounded Dockworker: SolveQuest "82OD" finishes JTEV.
        /// </summary>
        static bool SolveQuest(Character target, Character? source, ItemSpell spell)
        {
            // Solved for the player of the use: the user of an item (Health Regeneration Stim on a Wounded Dockworker),
            // or the player a creature's nano landed on (Resolve 2PZ7, area-cast by the Omni-AF Private).
            if ((source as Player ?? target as Player) is not Player player
                || player.Playfield?.GetService<Quests.QuestService>() is not { } quests)
                return false;
            string hash = spell.TryReadString(0, out string text) ? text.Trim()
                : spell.TryReadInt(0, out int packed) && NpcCharacter.TryDecodeAoHash(packed, out string decoded) ? decoded
                : string.Empty;
            if (hash.Length == 0 || player.QuestLog?.FindActive(hash) is not { } quest)
                return false;
            return quests.TryComplete(player, quest.QuestId, out _);
        }

        /// <summary>
        /// SpawnQuest [hash, ?, ?]: gives the player the Quests.json quest of that hash or template alias, as a quest
        /// starter item does. Bank of Rubi-Ka Credit Card 297315: KY6D (Steal the credits) and QHL3 (Deliver the Lost
        /// Credit Card). True when the quest is held afterwards.
        /// </summary>
        static bool SpawnQuest(Player player, ItemSpell spell)
        {
            if (player.Playfield?.GetService<Quests.QuestService>() is not { } quests)
                return false;
            string hash = spell.TryReadString(0, out string text) ? text.Trim()
                : spell.TryReadInt(0, out int packed) && NpcCharacter.TryDecodeAoHash(packed, out string decoded) ? decoded
                : string.Empty;
            if (hash.Length == 0)
                return false;
            bool started = quests.TryStartFromItem(player, hash, out string? why);
            if (why != null)
                ClientFeedback.SendFormatted(player, ClientFeedback.PlainText, why);
            return started;
        }

        static bool FailQuest(Player player, ItemSpell spell)
        {
            if (player.Playfield?.GetService<Quests.QuestService>() is not { } quests || !spell.TryReadInt(0, out int qas) || qas <= 0)
                return false;
            foreach (Quests.PlayerQuest quest in quests.GetLog(player).Quests.Values)
            {
                if (quest.IsActive && quest.Template.QasInstance == qas)
                    return quests.TryFail(player, quest);
            }

            // Not held (deleted already): nothing left to end.
            return true;
        }

        static bool NpcFightSelected(Character target, Character? source)
        {
            NpcCharacter? npc = target as NpcCharacter ?? source as NpcCharacter;
            if (npc?.Brain == null)
                return false;

            Character? opponent = ReferenceEquals(npc, target) ? source : target;
            if (opponent == null || ReferenceEquals(opponent, npc) || opponent.IsDead)
                return false;
            if (!ReferenceEquals(opponent.Playfield, npc.Playfield))
                return false;

            npc.Brain.AddThreatFrom(opponent, NpcAiRules.ProximityHate);
            npc.StartFighting(opponent.Identity, 0);
            return true;
        }

        /// <summary>Sends a Text or SystemText function's message to the player, as an item use does.</summary>
        internal static bool SendText(Player player, ItemSpell spell) => SystemText(player, spell);

        static bool SystemText(Player player, ItemSpell spell)
        {
            if (player.Session == null || !spell.TryReadString(0, out string text) || text.Length == 0)
                return false;

            if (spell.Is(FunctionType.SystemText))
            {
                text = FormatSystemText(player, spell, text);
                // Live: FormatFeedback 110/707 with the text as its one argument.
                ClientFeedback.SendFormatted(player, ClientFeedback.PlainText, text);
                return true;
            }

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

        static string FormatSystemText(Player player, ItemSpell spell, string text)
        {
            // RDB SystemText carries Text, four stat arguments, then ToClient. Older items.dat
            // exports discarded those arguments; leave their text unchanged rather than guess a stat.
            if (spell.ArgumentCount < 5 || !text.Contains('%'))
                return text;

            var formatted = new StringBuilder(text.Length);
            int argument = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '%')
                {
                    formatted.Append(text[i]);
                    continue;
                }

                if (++i == text.Length)
                    return text;
                if (text[i] == '%')
                {
                    formatted.Append('%');
                    continue;
                }

                // Only the integer stat substitution evidenced in the RDB is handled here.
                // Unknown formats retain the original text instead of consuming unrelated arguments.
                if (text[i] != 'd' || argument > 4
                    || !spell.TryReadInt(argument++, out int statId) || statId < 0)
                    return text;

                formatted.Append(player.Stats.GetOrZero((CharacterStat)statId).ToString(CultureInfo.InvariantCulture));
            }

            return formatted.ToString();
        }

        /// <summary>
        /// Absolute OnUse teleport. Positional args are RelX, RelY, RelZ, destination playfield.
        /// A destination playfield of 0 is a soft in-place teleport on the current playfield
        /// (e.g. 258912 Entrance to Hangar), like LineTeleport's intrazone lift.
        /// One-way: it clears any stored proxy return door.
        /// </summary>
        static bool Teleport(Player player, ItemSpell spell)
        {
            if (player.Session == null || player.Playfield == null)
                return false;

            if (!spell.TryReadInt(0, out int x)
                || !spell.TryReadInt(1, out int y)
                || !spell.TryReadInt(2, out int z)
                || !spell.TryReadInt(3, out int playfieldId))
                return false;

            Playfield source = player.Playfield;
            bool samePlayfield = playfieldId == 0 || playfieldId == source.Identity.Instance;
            if (!samePlayfield)
            {
                if (playfieldId < 0
                    || playfieldId >= ZoneEngine.Core.Missions.GeneratedMissionIdentitySpace.MinimumLivePlayfield2)
                    return false;

                IGameData gameData = source.GetRequiredService<IGameData>();
                string playfieldDir = Path.Combine(
                    gameData.RootPath,
                    GameDataPaths.PlayfieldRelativeDirectory(playfieldId));
                if (!Directory.Exists(playfieldDir))
                    return false;
            }

            ClearProxyReturn(player);
            source.GetService<WorldSimulationAccess>()?.Instance?.ForgetCharacterTriggers(player.Identity.Instance);

            if (samePlayfield)
            {
                Vector3 local = source.SnapFeetToFloor(new Vector3(x, y, z));
                FinishSamePlayfieldMove(player, source, local, heading: null, source.Identity.Instance);
                return true;
            }

            // An unloaded destination builds in the background; the move completes on this player's tick.
            return source.GetRequiredService<PlayfieldManager>().WithPlayfield(playfieldId, player, destination =>
            {
                if (!ReferenceEquals(player.Playfield, source) || player.Session is not { } session)
                    return;
                session.TransferToPlayfield(destination, destination.SnapFeetToFloor(new Vector3(x, y, z)));
            }, new ZoneEngine_New.Core.World.RemoteArrival(new Vector3(x, y, z), Kind: AORebirth.World.Contracts.V1.ArrivalKind.ArrivalTeleport));
        }

        /// <summary>
        /// LineTeleport names a Destinations.dat line: <c>{Playfield3, packed, destPlayfield}</c>.
        /// A destination playfield of 0 stays on the character's current playfield. One-way.
        /// </summary>
        static bool LineTeleport(Player player, ItemSpell spell)
        {
            if (player.Session == null || player.Playfield == null)
                return false;

            if (!PortalDoorLandingResolver.TryParseLineDestination(spell.Arguments, out PortalDestination destination))
                return false;

            Playfield source = player.Playfield;
            int playfieldId = destination.PlayfieldId == 0
                ? source.Identity.Instance
                : destination.PlayfieldId;
            if (playfieldId <= 0
                || playfieldId >= ZoneEngine.Core.Missions.GeneratedMissionIdentitySpace.MinimumLivePlayfield2)
                return false;

            IGameData gameData = source.GetRequiredService<IGameData>();
            if (playfieldId != source.Identity.Instance)
            {
                string playfieldDir = Path.Combine(
                    gameData.RootPath,
                    GameDataPaths.PlayfieldRelativeDirectory(playfieldId));
                if (!Directory.Exists(playfieldDir))
                    return false;
            }

            if (!PortalDoorLandingResolver.TryResolveLineLanding(
                    DestinationsCatalog.Instance,
                    playfieldId,
                    destination.DestinationIndex,
                    out Vector3 landing,
                    out Quaternion heading))
                return false;

            ClearProxyReturn(player);
            // Drops the pad overlap and holds triggers off briefly; afterwards the landing's
            // overlaps are adopted, so a return beam beside the landing only fires once walked off.
            source.GetService<WorldSimulationAccess>()?.Instance?.ForgetCharacterTriggers(player.Identity.Instance);

            if (destination.PlayfieldId == 0)
            {
                FinishIntrazoneLineTeleport(player, source, landing, destination.DestinationKey);
                return true;
            }

            if (playfieldId == source.Identity.Instance)
            {
                FinishSamePlayfieldMove(player, source, landing, heading, destination.DestinationKey);
                return true;
            }

            player.Rotation = heading;
            return source.GetRequiredService<PlayfieldManager>().WithPlayfield(playfieldId, player, destPlayfield =>
            {
                if (!ReferenceEquals(player.Playfield, source) || player.Session is not { } session)
                    return;
                session.TransferToPlayfield(destPlayfield, landing, heading);
            }, new ZoneEngine_New.Core.World.RemoteArrival(landing, heading, AORebirth.World.Contracts.V1.ArrivalKind.ArrivalTeleport));
        }

        static void ClearProxyReturn(Player player)
        {
            player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
            player.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
        }

        /// <summary>
        /// A teleport that names the playfield the character is already on. It stays a soft in-place move: the
        /// zone-change N3Teleport (ChangePlayfield set) would make the client unload and reload the zone.
        /// </summary>
        static void FinishSamePlayfieldMove(Player player, Playfield source, Vector3 landing, Quaternion? heading, int destinationKey)
        {
            if (heading != null)
                player.Motor.Warp(landing, heading);
            else
                player.Motor.Warp(landing);

            player.Session!.SendIntrazoneTeleport(landing, player.Rotation, destinationKey);
            AnnounceArrival(player, source, landing);
        }

        /// <summary>
        /// Same-playfield lift (LineTeleport or Teleport with destination playfield 0). The client snaps in place
        /// from a soft N3Teleport and keeps its heading; no zone transfer and no redirect.
        /// </summary>
        static void FinishIntrazoneLineTeleport(Player player, Playfield source, Vector3 landing, int destinationKey)
        {
            player.Motor.Warp(landing);
            player.Session!.SendIntrazoneTeleport(landing, player.Rotation, destinationKey);
            AnnounceArrival(player, source, landing);
        }

        /// <summary>Other clients only see the N3Teleport's effect through a normal move.</summary>
        static void AnnounceArrival(Player player, Playfield source, Vector3 landing)
        {
            PlayfieldLocality? locality = source.GetService<PlayfieldLocality>();
            if (locality == null)
                return;

            locality.Announce(
                player,
                new CharDCMoveMessage
                {
                    Identity = player.Identity,
                    Unknown = 0x00,
                    MoveType = (byte)MovementAction.FullStop,
                    Heading = new MsgQuaternion
                    {
                        X = player.Rotation.xf,
                        Y = player.Rotation.yf,
                        Z = player.Rotation.zf,
                        W = player.Rotation.wf
                    },
                    Coordinates = new MsgVector3
                    {
                        X = landing.xf,
                        Y = landing.yf,
                        Z = landing.zf
                    },
                    Unknown1 = 0,
                    AuxA = 0,
                    AuxB = 0
                },
                includeSelf: false);
        }

        /// <summary>
        /// Walk-in proxy. Args are <c>{PlayfieldDoor, destPlayfield, doorIndex, sourceDoor, ...}</c>.
        /// A source door of 0 uses <see cref="SpellCriteria.SourceDoorInstance"/>. The destination
        /// door becomes the way back, unless that door already has its own vicinity teleport.
        /// Jobe Platform's return rings instead name a destination line and land on its midpoint.
        /// </summary>
        static bool TeleportProxy(Player player, ItemSpell spell, SpellCriteria? criteria)
        {
            if (player.Session == null || player.Playfield == null)
                return false;

            if (!PortalDoorLandingResolver.TryParseProxyDestination(
                    spell.Arguments,
                    PortalDoorLandingResolver.ProxyEntryDoorClearance,
                    recordsReturn: true,
                    out PortalDestination destination))
                return false;

            Playfield source = player.Playfield;
            IGameData gameData = source.GetRequiredService<IGameData>();
            int sourceDoor = criteria?.SourceDoorInstance ?? 0;
            if (spell.TryReadInt(3, out int namedDoor) && namedDoor != 0)
                sourceDoor = namedDoor;

            int destPlayfield = destination.PlayfieldId;
            int destDoor = destination.DoorInstance;
            bool landsOnLine = destination.Kind == PortalLandingKind.DestinationMidpoint;
            if (sourceDoor != 0
                && gameData.TryGetTeleportRoute(
                    source.Identity.Instance,
                    (int)IdentityType.Door,
                    unchecked((uint)sourceDoor),
                    out int routedPlayfield,
                    out int routedType,
                    out uint routedInstance))
            {
                // A route replaces the raw door. Door 0 on Jobe Platform is a teleporting ring,
                // which is where every unrouted proxy was landing.
                if (routedType != (int)IdentityType.Door
                    || routedPlayfield <= 0
                    || routedPlayfield > 0xFFFF
                    || (routedInstance & 0xFF000000u) != 0xC0000000u
                    || (routedInstance & 0xFFFFu) != (uint)routedPlayfield)
                    return false;

                destPlayfield = routedPlayfield;
                destDoor = unchecked((int)routedInstance);
                landsOnLine = false;
            }

            if (destPlayfield == source.Identity.Instance)
                return false;

            Vector3 landing;
            Quaternion heading;
            if (landsOnLine)
            {
                if (!PortalDoorLandingResolver.TryResolveMidpointLanding(
                        DestinationsCatalog.Instance,
                        destPlayfield,
                        destination.DestinationIndex,
                        out landing,
                        out heading))
                    return false;
                ClearProxyReturn(player);
            }
            else
            {
                if (!PortalDoorLandingResolver.TryResolveDoorLanding(
                        gameData.GetPlayfieldGeometry(destPlayfield),
                        destDoor,
                        destination.DoorClearance,
                        out landing,
                        out heading))
                    return false;
                player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, source.Identity.Instance, StatDetail.Base, dirty: true);
                player.Stats.Set(CharacterStat.ExternalDoorInstance, sourceDoor, StatDetail.Base, dirty: true);
            }

            player.Rotation = heading;

            // An interior or backyard behind a walk-in door is a per-door instance (live's proxy playfields); the
            // landing above was resolved on the interior's own geometry, which the instance shares.
            int targetPlayfield = landsOnLine
                ? destPlayfield
                : ProxyPlayfieldIds.ResolveTarget(gameData, source.ResourceId, sourceDoor, destPlayfield);

            source.GetService<WorldSimulationAccess>()?.Instance?.ForgetCharacterTriggers(player.Identity.Instance);
            return source.GetRequiredService<PlayfieldManager>().WithPlayfield(targetPlayfield, player, arrival =>
            {
                if (!ReferenceEquals(player.Playfield, source) || player.Session is not { } session)
                    return;
                if (!landsOnLine && arrival is ACGPlayfield acg)
                    acg.World?.RegisterExitProxyDoor(destDoor);
                session.TransferToPlayfield(arrival, landing, heading);
            }, new ZoneEngine_New.Core.World.RemoteArrival(landing, heading, AORebirth.World.Contracts.V1.ArrivalKind.ArrivalTeleport, landsOnLine ? 0 : destDoor));
        }

        /// <summary>
        /// One-way proxy teleport used by Grid enter terminals and similar OnUse machines. A full
        /// argument list names a Destinations.dat line and lands on its midpoint; otherwise the
        /// destination is packed as PlayfieldDoor (playfield + door index) and the landing dynel
        /// may be a Door or a Terminal with that packed instance.
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

            Vector3 landing;
            Quaternion heading;
            if (destination.Kind == PortalLandingKind.DestinationMidpoint)
            {
                if (!PortalDoorLandingResolver.TryResolveMidpointLanding(
                        DestinationsCatalog.Instance,
                        destination.PlayfieldId,
                        destination.DestinationIndex,
                        out landing,
                        out heading))
                    return false;
            }
            else if (!PortalDoorLandingResolver.TryResolveProxyLanding(
                    source.GetRequiredService<IGameData>().GetPlayfieldGeometry(destination.PlayfieldId),
                    destination.DoorInstance,
                    destination.DoorClearance,
                    out landing,
                    out heading))
                return false;

            // TeleportProxy2 is one-way: clear any stale return door so exit proxies cannot pull
            // the character back to an unrelated entry.
            player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
            player.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
            player.Rotation = heading;

            return source.GetRequiredService<PlayfieldManager>().WithPlayfield(destination.PlayfieldId, player, destPlayfield =>
            {
                if (!ReferenceEquals(player.Playfield, source) || player.Session is not { } session)
                    return;
                session.TransferToPlayfield(destPlayfield, landing, heading);
            }, new ZoneEngine_New.Core.World.RemoteArrival(landing, heading, AORebirth.World.Contracts.V1.ArrivalKind.ArrivalTeleport));
        }

        /// <summary>
        /// Catalog shape from nano 205606: SpawnMonster2("KHAL", 73, 3600) — hash, level, lifetime.
        /// Lifetime is stored on the function; one-shot summons do not hash-respawn.
        /// </summary>
        /// <summary>
        /// SummonPet (mob hash, level, duration seconds; -1 or 0: until dismissed): the character the function runs
        /// on gets the pet (the summoner: shell items and pet nanos target Self).
        /// </summary>
        static bool SummonPet(Character target, ItemSpell spell, ItemTemplate? sourceTemplate)
        {
            if (target.Playfield == null || target.IsDead)
                return false;
            if (!spell.TryReadString(0, out string hash) || hash.Length == 0)
                return false;
            if (!spell.TryReadInt(1, out int level))
                level = 0;
            if (!spell.TryReadInt(2, out int duration))
                duration = -1;

            // The summoning nano's / item's ToUse rows decide later whether the pet is over-equipped.
            IReadOnlyList<ItemRequirement> summonRequirements =
                sourceTemplate?.Actions.Find(action => action.ActionType == (int)ActionType.ToUse)?.Requirements ?? [];
            return target.Playfield.GetRequiredService<Pets.PetService>()
                .Summon(target, hash, level, duration, summonRequirements) != null;
        }

        static bool SpawnMonster2(Character target, ItemSpell spell)
        {
            Playfield? playfield = target.Playfield;
            if (playfield == null)
                return false;

            if (!spell.TryReadString(0, out string hash) || hash.Length == 0)
                return false;
            if (!spell.TryReadInt(1, out int level) || level <= 0)
                return false;

            IGameData gameData = playfield.GetRequiredService<IGameData>();
            if (!gameData.CanResolveMobHash(hash))
                return false;

            try
            {
                IReadOnlyList<NpcCharacter> spawned = playfield.GetRequiredService<SpawnService>().SpawnBranches(
                    hash,
                    target.Position,
                    target.Rotation,
                    level,
                    SpawnSource.Summoned);
                return spawned.Count > 0;
            }
            catch (Exception exception) when (exception is KeyNotFoundException
                or InvalidOperationException
                or ArgumentException)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "SpawnMonster2 failed hash={0} level={1} character={2}: {3}",
                        hash,
                        level,
                        target.Identity.Instance,
                        exception.Message));
                return false;
            }
        }

        /// <summary>
        /// Removes the item this event was aimed at: the item used on a target, or the item
        /// whose own OnUse is running. Criteria on the function decide when it fires.
        /// </summary>
        static bool DestroySubject(Character target, SpellCriteria? criteria)
        {
            if (target is not Player player || criteria?.Subject == null)
                return false;

            return criteria.Subject.DestroyOne(player, criteria.SubjectSlot);
        }

        static bool UploadNano(Player player, ItemSpell spell)
        {
            if (!spell.TryReadInt(0, out int nanoId) || nanoId <= 0)
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

    }
}
