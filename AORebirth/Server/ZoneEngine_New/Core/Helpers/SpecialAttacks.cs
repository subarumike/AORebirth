namespace ZoneEngine_New.Core.Helpers
{
    using System;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Nanos;

    using ModuleHooks = ZoneEngine_New.Core.Modules.ModuleHooks;
    using ModuleCooldowns = ZoneEngine_New.Core.Modules.ModuleCooldowns;
    using CooldownKind = AORebirth.Modules.Events.CooldownKind;
    using CooldownKey = AORebirth.Modules.Events.CooldownKey;
    using PlayerActionKind = AORebirth.Modules.Events.PlayerActionKind;
    using PlayerActionOutcome = AORebirth.Modules.Events.PlayerActionOutcome;
    using ActionRefusal = AORebirth.Modules.Events.ActionRefusal;
    using CombatSource = AORebirth.Modules.Events.CombatSource;
    using CombatResult = AORebirth.Modules.Events.CombatResult;

    /// <summary>
    /// Weapon special attacks requested by CharSecSpecAttack: Brawl, Fast Attack, Burst, Fling Shot, Aimed Shot,
    /// Full Auto, Sneak Attack (which becomes Backstab for Shades and Adventurers) and Dimach.
    /// Rules from AOWiki "Special Attack" and AO-Universe "Attack Rating, Weapon Damage and Special Attacks".
    /// </summary>
    public static class SpecialAttacks
    {
        /// <summary>Brawl locks for a flat 15 seconds.</summary>
        const int FlatRechargeSeconds = 15;

        /// <summary>Full Auto: 5 bullets plus one per 100 skill, at most the clip.</summary>
        const int FullAutoBaseBullets = 5;

        /// <summary>Full Auto total damage: halved past 10000, again past 11500, 13000 and 14500; at most 15000.</summary>
        static readonly int[] FullAutoDamageSteps = [10000, 11500, 13000, 14500];

        const int FullAutoMaxDamage = 15000;

        /// <summary>Aimed Shot, Sneak Attack and Backstab deal at most 13000.</summary>
        const int StealthStrikeMaxDamage = 13000;

        /// <summary>Burst fires up to three shots, each rolled on its own.</summary>
        const int BurstShots = 3;

        /// <summary>
        /// Backstab gates on Sneak Attack without nano buffs (equipment and implants count), by profession: usable,
        /// gains the skill multiplier, can crit (AOWiki, AO-Universe Shade guide).
        /// </summary>
        sealed record BackstabGates(int Usable, int Multiplier, int Critical);

        static readonly BackstabGates AdventurerBackstab = new(350, 540, 625);

        static readonly BackstabGates ShadeBackstab = new(100, 235, 420);

        /// <summary>What a queued special did on one try.</summary>
        public enum FireResult
        {
            /// <summary>It ran (hit or missed) and locked its skill.</summary>
            Fired,

            /// <summary>Not yet: out of reach or line of sight, or a gun special while its attacker moves.</summary>
            Waiting,

            /// <summary>It never will: refused, its fight ended or moved to another target.</summary>
            Dropped,
        }

        /// <summary>
        /// A special attack request: it starts the fight with <paramref name="target"/> and goes into the attacker's
        /// special queue (<see cref="Character.TickQueuedSpecials"/>), where it fires when it can. Live waits with gun
        /// specials (Aimed Shot, Fling Shot, Burst, Full Auto) until the shooter stops moving, and a queued special that
        /// has not fired when the attack stops never fires. A locked or unusable special is refused here.
        /// </summary>
        public static bool Queue(Character attacker, Character target, CharacterStat special)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            ArgumentNullException.ThrowIfNull(target);

            if (!TryGetCanFlag(special, out _))
                return false;

            TimeSpan remaining = attacker.SkillLocks.Remaining((int)special, DateTime.UtcNow);
            if (remaining > TimeSpan.Zero)
            {
                attacker.SendSkillLocked((int)special, remaining);
                return Refused(attacker, target, special, ActionRefusal.Cooldown, "skill locked",
                    new CooldownKey(CooldownKind.SkillLock, (int)special), remaining);
            }

            // Every special attack, player or NPC, needs a target its attacker may engage.
            if (!CombatRules.CanAttack(attacker, target))
                return Refused(attacker, target, special, ActionRefusal.Target, "cannot attack target");

            // A player's special that could not go off now is refused with live's reason, and starts no fight.
            if (attacker is Player && Blocker(attacker, target, special) is int blocked)
            {
                ClientFeedback.Send(attacker, blocked);
                return Refused(attacker, target, special,
                    blocked == ClientFeedback.SpecialOutOfRange ? ActionRefusal.Range
                    : blocked == ClientFeedback.SpecialNoLineOfSight ? ActionRefusal.LineOfSight
                    : ActionRefusal.Target, "blocked");
            }

            if (attacker.FightingTarget.Instance == 0)
                attacker.StartFightingForSpecial(target.Identity);

            attacker.SetFightingTarget(target.Identity);
            // Module API 1.6: it waits in the special queue; TickQueuedSpecials / TryFire resolve it.
            if (attacker is Player player)
                ModuleHooks.ActionAttempted(player, PlayerActionKind.SpecialAttack, (int)special, PlayerActionOutcome.Queued,
                    target: target.Identity);
            attacker.QueueSpecial(special, target.Identity, ModuleHooks.RequestNow);
            return true;
        }

        /// <summary>
        /// Why the special cannot go off at <paramref name="target"/> right now, as a feedback message; null when it
        /// can. Live prints only one reason: range, then line of sight, then the target noticing a stealth strike.
        /// </summary>
        static int? Blocker(Character attacker, Character target, CharacterStat special)
        {
            if (TryGetCanFlag(special, out CanFlags canFlag) && TryPickWeapon(attacker, canFlag, out CharacterWeapon weapon, out _)
                && attacker.GetEdgeDistanceTo(target) > weapon.GetAttackRange())
                return ClientFeedback.SpecialOutOfRange;
            if (!attacker.HasLineOfSightTo(target))
                return ClientFeedback.SpecialNoLineOfSight;
            if (IsNoticedStealthStrike(attacker, target, special))
                return ClientFeedback.SpecialTargetAware;
            return null;
        }

        /// <summary>Aimed Shot or Sneak Attack the target would notice (a Sneak Attack that can backstab instead is fine).</summary>
        static bool IsNoticedStealthStrike(Character attacker, Character target, CharacterStat special)
        {
            bool noticed = !IsSneaking(attacker) || CanSee(target, attacker);
            return special switch
            {
                CharacterStat.AimedShot => noticed,
                CharacterStat.SneakAttack => noticed && !CanBackstab(attacker, target),
                _ => false
            };
        }

        /// <summary>Gun specials hold while their shooter moves.</summary>
        static bool WaitsForStandingStill(CharacterStat special)
            => special is CharacterStat.AimedShot or CharacterStat.FlingShot or CharacterStat.Burst or CharacterStat.FullAuto;

        /// <summary>
        /// One try at a queued special from <paramref name="attacker"/> against <paramref name="target"/>.
        /// </summary>
        /// <param name="requestTimestamp">The receive time of the request that queued it, for the module Resolved.</param>
        public static FireResult TryFire(Character attacker, Character target, CharacterStat special, long requestTimestamp = -1)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            ArgumentNullException.ThrowIfNull(target);

            if (!TryGetCanFlag(special, out CanFlags canFlag))
                return Dropped(attacker, target, special, requestTimestamp, ActionRefusal.Other, "not a special");

            DateTime now = DateTime.UtcNow;
            TimeSpan locked = attacker.SkillLocks.Remaining((int)special, now);
            if (locked > TimeSpan.Zero)
                return Dropped(attacker, target, special, requestTimestamp, ActionRefusal.Cooldown, "skill locked",
                    new CooldownKey(CooldownKind.SkillLock, (int)special), locked);

            // Stopping the attack, or attacking something else, takes the queued specials with it.
            if (attacker.FightingTarget != target.Identity)
                return Dropped(attacker, target, special, requestTimestamp, ActionRefusal.None, "fight ended or moved",
                    outcome: PlayerActionOutcome.Cancelled);
            if (!CombatRules.CanAttack(attacker, target))
                return Dropped(attacker, target, special, requestTimestamp, ActionRefusal.Target, "cannot attack target");

            // A stun pauses specials as it pauses the weapon bars; they fire once it ends.
            if (attacker.IsStunned || (WaitsForStandingStill(special) && attacker.Motor.IsMoving))
                return FireResult.Waiting;

            if (IsNoticedStealthStrike(attacker, target, special))
            {
                ClientFeedback.Send(attacker, ClientFeedback.SpecialTargetAware);
                return Dropped(attacker, target, special, requestTimestamp, ActionRefusal.Target, "target aware");
            }

            // Sneak Attack from stealth on an unaware target; otherwise a Shade or Adventurer behind a target that
            // someone else is fighting backstabs it, without needing stealth.
            bool backstab = special == CharacterStat.SneakAttack && (!IsSneaking(attacker) || CanSee(target, attacker));

            if (!TryPickWeapon(attacker, canFlag, out CharacterWeapon weapon, out int slot))
                return Dropped(attacker, target, special, requestTimestamp, ActionRefusal.Requirements, "no weapon for it");

            // A reloading gun fires no special (the client holds them too); an empty one with no ammo never will.
            if (attacker is Player shooter && !WeaponAmmo.CanFire(shooter, weapon))
                return weapon.IsReloading
                    ? FireResult.Waiting
                    : Dropped(attacker, target, special, requestTimestamp, ActionRefusal.Resources, "no ammo");

            // Same reach rules as an auto-attack swing; out of reach it stays queued and does not lock.
            if (!attacker.HasLineOfSightTo(target) || attacker.GetEdgeDistanceTo(target) > weapon.GetAttackRange())
                return FireResult.Waiting;

            // Firing any special gives the attacker away. A special that opens the fight keeps stealth until here
            // (StartFightingForSpecial), so one that kills outright gets no swing to break it.
            attacker.BreakSneak();
            // Being attacked gives a sneaker away.
            target.BreakSneak();

            // Brawl and Dimach strike with a skill-tier item instead of the hand weapon's damage.
            Item? specialItem = special switch
            {
                CharacterStat.Brawl => BrawlItem(attacker),
                CharacterStat.Dimach => DimachItem(attacker),
                _ => null
            };
            if (special == CharacterStat.Dimach && specialItem == null)
                return Dropped(attacker, target, special, requestTimestamp, ActionRefusal.Requirements, "no dimach item");

            // Retail order (Full Auto capture 2026-10-22): SpecialUsed first, then the hit's own messages (FullAuto
            // rounds feedback, SpecialAttackInfo), then the target's health.
            int seconds = RechargeSeconds(attacker, special, specialItem ?? weapon.DamageItem, backstab);
            using (ModuleCooldowns.During(PlayerActionKind.SpecialAttack, (int)special))
                attacker.LockSkill((int)special, seconds, now);
            attacker.ScheduleSpecialAvailable((int)special, now.AddSeconds(seconds));
            if (attacker is Player player)
                player.Session?.Send(Action(player, CharacterActionType.SpecialUsed, (int)special, seconds));

            bool landed = Resolve(attacker, target, special, weapon, slot, backstab, specialItem);
            if (attacker is Player user)
                ModuleHooks.ActionResolved(user, PlayerActionKind.SpecialAttack, (int)special,
                    landed ? PlayerActionOutcome.Executed : PlayerActionOutcome.Missed, target: target.Identity,
                    requestTimestamp: requestTimestamp);
            return FireResult.Fired;
        }

        /// <summary>Module API 1.6: a player's special attack request refused before it was queued.</summary>
        static bool Refused(Character attacker, Character target, CharacterStat special, ActionRefusal refusal, string detail,
            CooldownKey? cooldown = null, TimeSpan readyIn = default)
        {
            if (attacker is Player player)
                ModuleHooks.ActionAttempted(player, PlayerActionKind.SpecialAttack, (int)special, PlayerActionOutcome.Refused,
                    refusal, target: target.Identity, cooldown: cooldown, readyIn: readyIn, detail: detail);
            return false;
        }

        /// <summary>Module API 1.6: a queued special that will never fire: Failed (refused at fire time) or Cancelled.</summary>
        static FireResult Dropped(Character attacker, Character target, CharacterStat special, long requestTimestamp,
            ActionRefusal refusal, string detail, CooldownKey? cooldown = null, TimeSpan readyIn = default,
            PlayerActionOutcome outcome = PlayerActionOutcome.Failed)
        {
            if (attacker is Player player)
                ModuleHooks.ActionResolved(player, PlayerActionKind.SpecialAttack, (int)special, outcome, refusal,
                    target: target.Identity, cooldown: cooldown, readyIn: readyIn, detail: detail, requestTimestamp: requestTimestamp);
            return FireResult.Dropped;
        }

        /// <summary>Tells the client the special's skill lock has run out (CharacterAction 0xA4, Parameter2 = stat).</summary>
        public static void SendAvailable(Player player, int statId)
            => player.Session?.Send(Action(player, CharacterActionType.SpecialAvailable, 0, statId));

        /// <summary>Rolls and applies the special. True when it landed (a heal/drain Dimach always does), false on a miss.</summary>
        static bool Resolve(
            Character attacker, Character target, CharacterStat special, CharacterWeapon weapon, int slot, bool backstab,
            Item? specialItem)
        {
            Item? item = weapon.DamageItem;
            DamageCalculator.DamageResult result;
            int bullets;
            if (special == CharacterStat.Dimach && specialItem != null && HealsOrDrains(specialItem))
            {
                // Keeper heal / Shade drain: the item's own functions are the whole effect.
                RunItemFunctions(attacker, specialItem);
                return true;
            }

            // A player's gun fires from its clip (WeaponAmmo); anything else from a full magazine.
            Player? shooter = attacker as Player;
            int clip = shooter != null && WeaponAmmo.UsesAmmo(item)
                ? WeaponAmmo.Rounds(item!)
                : Math.Max(0, item?.GetStat(CharacterStat.MaxEnergy) ?? 0);
            int roundsFired = 1;

            if (special == CharacterStat.Dimach)
            {
                // Dimach never misses; attack rating still scales its damage.
                result = DamageCalculator.CalculateFromWeapon(attacker, target, specialItem, special, alwaysHits: true);
                bullets = -1;
                roundsFired = 0;
            }
            else if (special == CharacterStat.FullAuto)
            {
                // Full Auto empties the magazine (retail's ammo field reads 0 after it).
                result = RollFullAuto(attacker, target, item, clip, out bullets);
                SendFullAutoRounds(attacker, bullets);
                roundsFired = clip;
            }
            else if (special == CharacterStat.Burst)
            {
                result = RollBurst(attacker, target, item, clip, out bullets);
                roundsFired = bullets;
            }
            else if (special == CharacterStat.Brawl)
            {
                result = DamageCalculator.CalculateFromWeapon(attacker, target, specialItem ?? item, special);
                bullets = -1;
                roundsFired = 0;
            }
            else if (special is CharacterStat.AimedShot or CharacterStat.SneakAttack)
            {
                result = RollStealthStrike(attacker, target, item, special, backstab);
                bullets = AttackInfoRules.UsesMeleeAmmo(item) ? -1 : 1;
            }
            else
            {
                result = DamageCalculator.CalculateFromWeapon(attacker, target, item, special);
                bullets = AttackInfoRules.UsesMeleeAmmo(item) ? -1 : 1;
            }

            // The rounds go whether the special lands or not; the ammo field then shows what is left (retail Burst: 12
            // before, 9 after, capture 2026-10-23).
            int? roundsLeft = shooter != null ? WeaponAmmo.Spend(weapon, roundsFired) : null;

            if (!result.IsHit || target.IsEvading)
            {
                attacker.Cell?.Announce(new MissedAttackInfoMessage
                {
                    Identity = attacker.Identity,
                    Unknown1 = -1,
                    Unknown2 = slot,
                    Unknown3 = attacker.Identity,
                    Unknown4 = target.Identity,
                    Unknown5 = (int)special
                });
                ModuleHooks.CombatResolved(attacker, target, CombatSource.Special, (int)special,
                    result.IsHit ? CombatResult.Evaded : CombatResult.Miss, 0, 0);
                if (shooter != null)
                    WeaponAmmo.ReloadIfEmptied(shooter, weapon);
                return false;
            }

            int healthBefore = target.Stats.GetOrZero(CharacterStat.Health);
            target.RollBuffBreaks(BuffBreakCause.Attack, attacker);
            // The special hits as the item it was rolled from; the target's reflect for that type applies.
            Item? damageItem = special == CharacterStat.Dimach ? specialItem
                : special == CharacterStat.Brawl ? specialItem ?? item
                : item;
            int damageType = DamageCalculator.ResolveDamageType(attacker, damageItem);
            int damage = DamageReturn.Resolve(attacker, target, attacker.ScaleOutgoingDamage(target, result.Damage), damageType,
                shield: true, out DamageReturn.Returned returned);
            DamageReturn.Announce(attacker, target, returned);
            // The shown number is the damage taken: what the target's absorb layer and the PvP hit cap leave.
            damage = target.TakeOffShields(attacker, damage, damageType);
            target.ApplyDamage(attacker, damage, result.HitType);
            if (damage == 0 || target.Stats.GetOrZero(CharacterStat.Health) < healthBefore)
                AnnounceSpecialHit(attacker, target, special, slot, roundsLeft ?? bullets, damage);
            ModuleHooks.CombatResolved(attacker, target, CombatSource.Special, (int)special,
                result.HitType == HitType.Critical ? CombatResult.Critical : CombatResult.Hit, damage, damageType);

            // A special is a weapon hit too: offensive and defensive procs roll on it.
            Nanos.ProcRules.OnWeaponHit(attacker, target, specialItem ?? weapon.Item);
            DamageReturn.Apply(attacker, target, returned);
            if (shooter != null)
                WeaponAmmo.ReloadIfEmptied(shooter, weapon);

            // A landed Brawl or Dimach also runs its item's OnUse on the struck target (Brawl Item: 20% stun 211405 on
            // the fighting target).
            if (specialItem != null && special is CharacterStat.Brawl or CharacterStat.Dimach)
                RunItemFunctions(attacker, specialItem, target);
            return true;
        }

        /// <summary>
        /// The Full Auto bullets feedback (retail capture 2026-10-22: header byte 1, then the count twice). The
        /// client (Gamecode.dll FullAutoIIR_t apply, 0x100a0208) formats its text 110 with the first number and
        /// ignores the second: the "You hit with N bullets" line, so the count is the bullets that can hit. Sent to
        /// the shooter only, before the SpecialAttackInfo.
        /// </summary>
        static void SendFullAutoRounds(Character attacker, int rounds)
        {
            if (attacker is not Player player || rounds <= 0)
                return;

            player.Session?.Send(new FullAutoMessage
            {
                Identity = player.Identity,
                Unknown = 1,
                Unknown1 = rounds,
                Unknown2 = rounds
            });
        }

        /// <param name="bullets">The ammo field: a player's gun sends the rounds left in its clip.</param>
        static void AnnounceSpecialHit(Character attacker, Character target, CharacterStat special, int slot, int bullets,
            int damage)
        {
            // Client reader Gamecode.dll FUN_100a1c4f: slot, damage, ammo, target, stat, then a state value the
            // client applies to the target (FUN_1005b1b8); 4 as on a killing AttackInfo.
            // Retail Full Auto (capture 2026-10-22) sends 0 in the ammo field, the emptied magazine, and reports the
            // bullets through the FullAuto message instead.
            if (special == CharacterStat.FullAuto)
                bullets = 0;
            attacker.Cell?.Announce(new SpecialAttackInfo
            {
                Identity = attacker.Identity,
                Unknown = 0,
                // Retail Brawl (capture 2026-10-01): slot 0, ammo -1; the Brawl Item is not a hand weapon.
                Unknown1 = special == CharacterStat.Brawl ? 0 : slot,
                Unknown2 = damage,
                Unknown3 = bullets,
                Target = target.Identity,
                Unknown4 = (int)special,
                Unknown5 = target.IsDead ? 4 : 0
            });
        }

        /// <summary>
        /// Every bullet rolls its own hit, damage and crit; the hits are summed and the total softened past 10000.
        /// A miss only when every bullet misses. <paramref name="shots"/> is the bullets that can hit: 5 plus one
        /// per 100 skill, at most the rounds in the clip.
        /// </summary>
        static DamageCalculator.DamageResult RollFullAuto(
            Character attacker, Character target, Item? weapon, int clip, out int shots)
        {
            clip = Math.Max(1, clip);
            int skill = Math.Max(0, attacker.Stats.GetOrZero(CharacterStat.FullAuto));
            shots = Math.Min(FullAutoBaseBullets + skill / 100, clip);

            int total = 0;
            bool anyHit = false;
            HitType hitType = HitType.Normal;
            for (int i = 0; i < shots; i++)
            {
                DamageCalculator.DamageResult shot = DamageCalculator.CalculateFromWeapon(attacker, target, weapon, CharacterStat.FullAuto);
                if (!shot.IsHit)
                    continue;

                anyHit = true;
                total += shot.Damage;
                if (shot.HitType == HitType.Critical)
                    hitType = HitType.Critical;
            }

            return new DamageCalculator.DamageResult(anyHit, SoftCapFullAuto(total), hitType);
        }

        /// <summary>
        /// Aimed Shot and Sneak Attack always hit; Backstab rolls to hit with Sneak Attack as its attack rating. All
        /// three ignore armour and start from the weapon's max damage (max + crit bonus on a crit), times a random
        /// multiplier rolled from the special's skill (StealthMultiplierTable), capped at 13000. Backstab only gains the
        /// multiplier, and can only crit, once its unbuffed skill passes the profession's gates.
        /// </summary>
        static DamageCalculator.DamageResult RollStealthStrike(
            Character attacker, Character target, Item? weapon, CharacterStat special, bool backstab)
        {
            bool multiplied = true;
            bool canCrit = true;
            if (backstab && TryGetBackstabGates(attacker, out BackstabGates gates))
            {
                int skill = UnbuffedSneakAttack(attacker);
                multiplied = skill >= gates.Multiplier;
                canCrit = skill >= gates.Critical;
            }

            int specialSkill = Math.Max(0, attacker.Stats.GetOrZero(special));
            DamageCalculator.DamageResult roll = DamageCalculator.CalculateFromWeapon(
                attacker, target, weapon, special, alwaysHits: !backstab, canCrit: canCrit,
                attackSkillOverride: backstab ? specialSkill : null);
            if (!roll.IsHit)
                return roll;

            string? gameDataRoot = attacker.Playfield?.GetService<IGameData>()?.RootPath;
            int multiplier = multiplied && gameDataRoot != null ? StealthMultiplierTable.Roll(gameDataRoot, specialSkill) : 1;
            int damage = (int)Math.Min(StealthStrikeMaxDamage, (long)roll.Damage * multiplier);
            return new DamageCalculator.DamageResult(true, Math.Max(1, damage), roll.HitType);
        }

        /// <summary>
        /// Burst: up to three shots (limited by the clip), each rolled on its own for hit and damage, landing together.
        /// Burst never crits (DamageCalculator). <paramref name="bullets"/> is the rounds used.
        /// </summary>
        static DamageCalculator.DamageResult RollBurst(
            Character attacker, Character target, Item? weapon, int clip, out int bullets)
        {
            bullets = clip > 0 ? Math.Min(BurstShots, clip) : BurstShots;

            int total = 0;
            bool anyHit = false;
            for (int i = 0; i < bullets; i++)
            {
                DamageCalculator.DamageResult shot = DamageCalculator.CalculateFromWeapon(attacker, target, weapon, CharacterStat.Burst);
                if (!shot.IsHit)
                    continue;

                anyHit = true;
                total += shot.Damage;
            }

            return new DamageCalculator.DamageResult(anyHit, total, HitType.Normal);
        }

        /// <summary>
        /// The Brawl Item for the attacker's Brawl skill: each 1000 skill is a QL 1-500 template pair
        /// (ItemBehavior.json BrawlWeapons). Its damage and attack skill drive Brawl.
        /// </summary>
        static Item? BrawlItem(Character attacker)
        {
            IItemBuilder? items = attacker.Playfield?.GetService<IItemBuilder>();
            if (items == null)
                return null;

            try
            {
                (int lowId, int highId, int quality) =
                    MartialArtsFistResolver.ResolveBrawl(attacker.Stats.GetOrZero(CharacterStat.Brawl));
                return items.Create(lowId, highId, quality, ItemSource.Other);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The Dimach Item for the attacker's profession and Dimach skill (ItemBehavior.json DimachWeapons):
        /// Martial Artist and everyone else deal damage, Shade drains, Keeper heals itself.
        /// </summary>
        static Item? DimachItem(Character attacker)
        {
            IItemBuilder? items = attacker.Playfield?.GetService<IItemBuilder>();
            if (items == null)
                return null;

            try
            {
                (int lowId, int highId, int quality) = MartialArtsFistResolver.ResolveDimach(
                    (Profession)attacker.Stats.GetOrZero(CharacterStat.Profession),
                    attacker.Stats.GetOrZero(CharacterStat.Dimach));
                return items.Create(lowId, highId, quality, ItemSource.Other);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A Dimach Item whose OnUse heals (Hit) or drains (DrainHit) instead of rolling weapon damage.</summary>
        static bool HealsOrDrains(Item item)
            => item.SpellList.TryGetValue(EventType.OnUse, out System.Collections.Generic.List<ItemSpell>? spells)
                && spells.Exists(spell => spell.Is(FunctionType.Hit) || spell.Is(FunctionType.DrainHit));

        /// <summary>
        /// Runs the item's OnUse functions with the attacker as user. Target functions land on
        /// <paramref name="eventTarget"/> (the attacker when null); Fightingtarget is the attacker's fight target.
        /// </summary>
        static void RunItemFunctions(Character attacker, Item item, Character? eventTarget = null)
        {
            var playfield = attacker.Playfield;
            IInventoryRepository? inventory = playfield?.GetService<IInventoryRepository>();
            IItemBuilder? items = playfield?.GetService<IItemBuilder>();
            if (inventory == null || items == null)
                return;

            item.Definition.ExecuteOnUseSpells(eventTarget ?? attacker, inventory, items, source: attacker);
        }

        /// <summary>
        /// Backstab: a Shade or Adventurer with enough unbuffed Sneak Attack, standing behind a target that is fighting
        /// someone else. It stops working as soon as the target turns on the attacker.
        /// </summary>
        static bool CanBackstab(Character attacker, Character target)
        {
            if (!TryGetBackstabGates(attacker, out BackstabGates gates) || UnbuffedSneakAttack(attacker) < gates.Usable)
                return false;

            Identity fighting = target.FightingTarget;
            return fighting.Instance != 0 && fighting != attacker.Identity && IsBehind(attacker, target);
        }

        /// <summary>Sneak Attack without what active nano buffs add; equipment, implants and base skill stay.</summary>
        static int UnbuffedSneakAttack(Character attacker)
        {
            int skill = attacker.Stats.GetOrZero(CharacterStat.SneakAttack);
            foreach (Buff buff in attacker.Buffs)
                skill -= StatModifierSpells.SumModify(buff.ModifierSpells, CharacterStat.SneakAttack);
            return Math.Max(0, skill);
        }

        static bool TryGetBackstabGates(Character attacker, out BackstabGates gates)
        {
            switch ((Profession)attacker.Stats.GetOrZero(CharacterStat.Profession))
            {
                case Profession.Adventurer: gates = AdventurerBackstab; return true;
                case Profession.Shade: gates = ShadeBackstab; return true;
                default: gates = null!; return false;
            }
        }

        /// <summary>True when <paramref name="attacker"/> is in the half-space behind <paramref name="target"/>'s facing.</summary>
        internal static bool IsBehind(Character attacker, Character target)
        {
            var forward = (AORebirth.Core.Vector.Vector3)target.Rotation.RotateVector3(AORebirth.Core.Vector.Vector3.AxisZ);
            double toAttackerX = attacker.Position.x - target.Position.x;
            double toAttackerZ = attacker.Position.z - target.Position.z;
            return (forward.x * toAttackerX) + (forward.z * toAttackerZ) < 0;
        }

        static int SoftCapFullAuto(int damage)
        {
            int capped = 0;
            double rate = 1.0;
            int from = 0;
            foreach (int step in FullAutoDamageSteps)
            {
                if (damage <= step)
                    break;
                capped += (int)((step - from) * rate);
                from = step;
                rate /= 2;
            }

            capped += (int)((damage - from) * rate);
            return Math.Min(capped, FullAutoMaxDamage);
        }

        /// <summary>
        /// Skill lock after a special, in whole seconds (AOWiki "Special Attack", AO-Universe). Attack and Recharge are the
        /// weapon's AttackDelay / RechargeDelay; Burst and Full Auto delays are the weapon's BurstRecharge /
        /// FullAutoRecharge. All of those are centiseconds.
        /// </summary>
        static int RechargeSeconds(Character attacker, CharacterStat special, Item? weapon, bool backstab)
        {
            double attackSeconds = Centiseconds(weapon, CharacterStat.AttackDelay);
            double rechargeSeconds = Centiseconds(weapon, CharacterStat.RechargeDelay);
            int skill = Math.Max(0, attacker.Stats.GetOrZero(special));

            switch (special)
            {
                // (Attack x 16) - skill / 100, minimum 6 seconds + Attack.
                case CharacterStat.FlingShot:
                case CharacterStat.FastAttack:
                    return AtLeast((attackSeconds * 16) - (skill / 100.0), 6 + attackSeconds);

                // (Recharge x 20) + Burst Delay - skill / 25, minimum 8 seconds + Attack.
                case CharacterStat.Burst:
                    return AtLeast(
                        (rechargeSeconds * 20) + Centiseconds(weapon, CharacterStat.BurstRecharge) - (skill / 25.0),
                        8 + attackSeconds);

                // (Recharge x 40) + Full Auto Delay - skill / 25, minimum 10 seconds + Attack.
                case CharacterStat.FullAuto:
                    return AtLeast(
                        (rechargeSeconds * 40) + Centiseconds(weapon, CharacterStat.FullAutoRecharge) - (skill / 25.0),
                        10 + attackSeconds);

                // (Recharge x 40) - 3 x skill / 100, minimum 10 seconds + Attack.
                case CharacterStat.AimedShot:
                    return AtLeast((rechargeSeconds * 40) - (3 * skill / 100.0), 10 + attackSeconds);

                // 40 - skill / 150, minimum 10 seconds + Attack; Backstab is (40 - skill / 150) / 2.
                case CharacterStat.SneakAttack:
                {
                    int seconds = AtLeast(40 - (skill / 150.0), 10 + attackSeconds);
                    return backstab ? Math.Max(1, (int)((40 - (skill / 150.0)) / 2)) : seconds;
                }

                // Dimach: the Dimach Item's own RechargeDelay (30 min Martial Artist, 8 min others, 5 min Keeper/Shade).
                case CharacterStat.Dimach:
                    return Math.Max(1, (int)rechargeSeconds);

                // Brawl: a fixed 15 seconds.
                default:
                    return FlatRechargeSeconds;
            }
        }

        static double Centiseconds(Item? weapon, CharacterStat stat)
            => Math.Max(0, weapon?.GetStat(stat) ?? 0) / 100.0;

        static int AtLeast(double seconds, double minimum)
            => (int)Math.Max(seconds, minimum);

        /// <summary>Right hand if its template has the special's Can bit, else the left hand.</summary>
        static bool TryPickWeapon(Character attacker, CanFlags canFlag, out CharacterWeapon weapon, out int slot)
        {
            if (attacker.Weapons.TryGetValue(WeaponSlot.MainHand, out CharacterWeapon? right) && right?.Item?.Can(canFlag) == true)
            {
                weapon = right;
                slot = (int)WeaponSlots.Righthand;
                return true;
            }

            if (attacker.Weapons.TryGetValue(WeaponSlot.OffHand, out CharacterWeapon? left) && left?.Item?.Can(canFlag) == true)
            {
                weapon = left;
                slot = (int)WeaponSlots.LeftHand;
                return true;
            }

            weapon = null!;
            slot = 0;
            return false;
        }

        static bool TryGetCanFlag(CharacterStat special, out CanFlags flag)
        {
            switch (special)
            {
                case CharacterStat.Brawl: flag = CanFlags.Brawl; return true;
                case CharacterStat.FastAttack: flag = CanFlags.FastAttack; return true;
                case CharacterStat.Burst: flag = CanFlags.Burst; return true;
                case CharacterStat.FlingShot: flag = CanFlags.FlingShot; return true;
                case CharacterStat.AimedShot: flag = CanFlags.AimedShot; return true;
                case CharacterStat.FullAuto: flag = CanFlags.FullAuto; return true;
                case CharacterStat.SneakAttack: flag = CanFlags.SneakAttack; return true;
                case CharacterStat.Dimach: flag = CanFlags.Dimach; return true;
                default: flag = 0; return false;
            }
        }

        static bool IsSneaking(Character character) => character.Motor.State == MovementState.Sneak;

        /// <summary>
        /// Whether <paramref name="observer"/> notices <paramref name="sneaker"/>: hidden when the sneaker's Concealment
        /// beats the observer's Perception, the rule that also keeps a sneaker off other players' clients
        /// (LocalityVisibility). An observer without Perception falls back to its level: hidden when Concealment beats
        /// 5x the level (up to level 200) or 7x (up to 240); above 240 always seen.
        /// </summary>
        internal static bool CanSee(Character observer, Character sneaker)
        {
            int concealment = sneaker.Stats.GetOrZero(CharacterStat.Concealment);
            int perception = observer.Stats.GetOrZero(CharacterStat.Perception);
            if (perception > 0)
                return concealment <= perception;

            int level = observer.Stats.GetOrOne(CharacterStat.Level);
            if (level <= 200 && concealment > 5 * level)
                return false;
            if (level <= 240 && concealment > 7 * level)
                return false;
            return true;
        }


        static CharacterActionMessage Action(Player player, CharacterActionType action, int parameter1, int parameter2)
            => new()
            {
                Identity = player.Identity,
                Action = action,
                Target = Identity.None,
                Parameter1 = parameter1,
                Parameter2 = parameter2
            };
    }
}
