namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    /// <summary>
    /// Drives nano casts and NCU entries: gates a cast, runs the cast bar, lands or refuses the
    /// buff, starts the recharge lockout, and tells the clients involved.
    /// State lives on <see cref="Character"/>; this type owns the sequencing and the packets.
    /// </summary>
    public static class NanoRuntime
    {
        /// <summary>Advances the cast bar and expires NCU entries. Called from the character tick.</summary>
        public static void Tick(Character character, DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(character);

            PendingNanoCast? cast = character.PendingCast;
            if (cast != null && cast.IsReady(nowUtc))
            {
                // Clear first: the execute-time gate must not see this cast as "already casting".
                character.CancelNanoCast();
                Complete(character, cast, nowUtc);
            }

            TickPeriodicEffects(character, nowUtc);

            List<Buff> expired = character.DrainExpiredBuffs(nowUtc);
            for (int i = 0; i < expired.Count; i++)
                AnnounceBuffRemoved(character, expired[i]);
        }

        /// <summary>
        /// Repeated hits and child-nano casts due on this character's buffs. Ticks are collected first: a
        /// killing tick empties NCU, so no buff list is walked while effects run.
        /// </summary>
        static void TickPeriodicEffects(Character character, DateTime nowUtc)
        {
            IReadOnlyList<Buff> buffs = character.Buffs;
            if (buffs.Count == 0 || character.IsDead)
                return;

            List<(Buff Buff, ItemSpell Spell)>? due = null;
            var spells = new List<ItemSpell>();
            for (int i = 0; i < buffs.Count; i++)
            {
                spells.Clear();
                buffs[i].CollectDueTicks(nowUtc, spells);
                for (int s = 0; s < spells.Count; s++)
                    (due ??= new()).Add((buffs[i], spells[s]));
            }

            if (due == null)
                return;

            Playfield? playfield = character.Playfield;
            if (playfield == null)
                return;

            IInventoryRepository inventory = playfield.GetRequiredService<IInventoryRepository>();
            IItemBuilder items = playfield.GetRequiredService<IItemBuilder>();
            DynelRegistry registry = playfield.GetRequiredService<DynelRegistry>();
            for (int i = 0; i < due.Count; i++)
            {
                if (character.IsDead)
                    return;

                (Buff buff, ItemSpell spell) = due[i];
                if (!character.TryGetBuff(buff.Id, out Buff? active) || !ReferenceEquals(buff, active))
                    continue;
                // Attributed to the caster while it is still here; otherwise the owner takes it.
                Character? source = registry.TryGet(buff.Source, out Dynel? dynel) && dynel is Character caster
                    ? caster
                    : null;
                buff.ExecutePeriodicSpell(character, source, spell, inventory, items);
            }
        }

        /// <summary>Death empties NCU and drops a cast in flight, ignoring CanCancel.</summary>
        public static void ClearBuffsOnDeath(Character character)
        {
            ArgumentNullException.ThrowIfNull(character);

            character.CancelNanoCast();
            List<Buff> removed = character.RemoveAllBuffs(BuffRemovalReason.Death);
            for (int i = 0; i < removed.Count; i++)
                AnnounceBuffRemoved(character, removed[i]);
        }

        /// <summary>
        /// A charm's end empties the released NPC's NCU: buffs its charmer and their friends ran on it while it served
        /// must not stay on a monster that turns back on them. Removed as stripped, so no buff end effect runs.
        /// <paramref name="keepNanoId"/>: a copy of the charming nano that replaced the charm (CastNano2 over it, Henri
        /// Zure's Call to Arms recast) stays and runs out.
        /// </summary>
        public static void ClearBuffsOnCharmEnd(Character character, int keepNanoId = 0)
        {
            ArgumentNullException.ThrowIfNull(character);

            character.CancelNanoCast();
            List<Buff> removed = keepNanoId == 0
                ? character.RemoveAllBuffs(BuffRemovalReason.Stripped)
                : character.RemoveBuffs(buff => buff.Id != keepNanoId);
            for (int i = 0; i < removed.Count; i++)
                AnnounceBuffRemoved(character, removed[i]);
        }

        /// <summary>
        /// Up-front gate for a client cast request. On success the cast bar starts and the world
        /// sees CastNanoSpell; the buff itself lands only when the bar finishes.
        /// </summary>
        public static NanoCastRefusal TryStartCast(
            Character caster,
            int nanoId,
            Identity target,
            DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(caster);

            if (nanoId <= 0)
            {
                LogCastRefused(caster, nanoId, target, NanoCastRefusal.NotUploaded, phase: "start", detail: "nanoId<=0");
                return NanoCastRefusal.NotUploaded;
            }

            //This is smelly! - Delmus
            // Same click often delivers two start packets; the second must not refresh/remove.
            // if (caster.IsDuplicateRecentNanoLand(nanoId, target, nowUtc))
            //     return NanoCastRefusal.None;

            if (!TryResolveSpell(caster, nanoId, out NanoSpell? spell) || spell == null)
            {
                LogCastRefused(
                    caster,
                    nanoId,
                    target,
                    NanoCastRefusal.NotUploaded,
                    phase: "start",
                    detail: "spell resolve failed");
                Refuse(caster, NanoCastRefusal.NotUploaded);
                return NanoCastRefusal.NotUploaded;
            }

            // A self-only nano lands on the caster whatever the client had targeted.
            if (spell.IsSelfOnly)
                target = caster.Identity;

            Character? recipient = ResolveTarget(caster, target);
            int nanoCost = ResolveNanoCost(caster, spell);
            NanoCastAttempt attempt = BuildAttempt(caster, spell, recipient, nanoCost, nowUtc);
            NanoCastRefusal refusal = NanoCastRules.Evaluate(attempt);
            if (refusal == NanoCastRefusal.None && caster is Player)
                refusal = PlayerCastGate(caster, spell, recipient);
            // Start-only: do not begin a cast the target cannot hold. Land-time NCU is checked
            // again in TryApplyBuff; a mid-cast capacity change still finishes the cast.
            if (refusal == NanoCastRefusal.None && !TargetCanHoldBuff(spell, recipient))
                refusal = NanoCastRefusal.NotEnoughNcu;

            // A stronger nano of the same strain already on the target: refused before the cast, so nothing is spent
            // (a lower root on a better-rooted target used to cast, cost its nano and fail on landing).
            if (refusal == NanoCastRefusal.None && TargetRunsBetterNano(spell, recipient))
                refusal = NanoCastRefusal.BetterNanoRunning;

            if (refusal == NanoCastRefusal.None && spell.IsHostile && !IsHostileNanoTarget(caster, recipient))
                refusal = NanoCastRefusal.InvalidTarget;

            if (refusal == NanoCastRefusal.None && !spell.IsHostile)
                refusal = FriendlyNanoTargetRefusal(caster, recipient);

            if (refusal == NanoCastRefusal.None && (IsNoCombat(caster) || IsNoCombat(recipient)))
                refusal = NanoCastRefusal.InvalidTarget;

            // A nano whose SpawnItem can only make a Unique the caster already holds is refused before anything is spent.
            string? heldUnique = null;
            if (refusal == NanoCastRefusal.None && caster is Player uniqueHolder && caster.Playfield is { } castPlayfield
                && Inventory.UniqueSpawnCheck.TryFindHeldUniqueOnly(spell, uniqueHolder,
                    castPlayfield.GetRequiredService<GameData.IGameData>(),
                    castPlayfield.GetRequiredService<Inventory.IItemTemplateCatalog>(), out string uniqueName))
            {
                refusal = NanoCastRefusal.UniqueItemHeld;
                heldUnique = uniqueName;
            }

            if (refusal != NanoCastRefusal.None)
            {
                LogCastRefused(caster, nanoId, target, refusal, phase: "start", attempt: attempt, spell: spell);
                if (heldUnique != null)
                    ClientFeedback.SendFormatted(caster, ClientFeedback.AlreadyHaveUniqueItem, heldUnique);
                else
                    Refuse(caster, refusal);
                return refusal;
            }

            int attackTime = NanoDelayCalculator.AttackTimeCentiseconds(
                spell.AttackDelayCentiseconds,
                spell.AttackDelayCapCentiseconds,
                caster.Stats.GetOrZero(CharacterStat.AggDef),
                caster.Stats.GetOrZero(CharacterStat.NanoCInit));

            // A hostile cast at a player, or a friendly one at a PvP-enabled player, flags the caster for PvP.
            if (caster is Player castingPlayer)
            {
                if (spell.IsHostile)
                    PvpAttackerFlag.OnHostileAction(castingPlayer, recipient!);
                else
                    PvpAttackerFlag.OnFriendlyAction(castingPlayer, recipient!);
            }

            // Casting a nano gives a sneaking caster away, as an aimed shot or a sneak attack does.
            caster.BreakSneak();

            Identity resolvedTarget = recipient!.Identity;
            var cast = new PendingNanoCast(spell, resolvedTarget, nanoCost, attackTime, nowUtc);
            caster.BeginNanoCast(cast);

            // The nano cost is paid as the cast starts; an interrupted or refused cast does not refund it.
            SpendNano(caster, nanoCost);
            AnnounceCastStarted(caster, spell.Id, resolvedTarget);

            // Instant nanos have no cast bar; land them in the same tick.
            if (cast.IsReady(nowUtc))
            {
                caster.CancelNanoCast();
                Complete(caster, cast, nowUtc);
            }

            return NanoCastRefusal.None;
        }

        /// <summary>Reads a nano template. False when the id is not a template.</summary>
        public static bool TryGetSpell(Character caster, int nanoId, out NanoSpell? spell)
        {
            ArgumentNullException.ThrowIfNull(caster);
            spell = null;
            return nanoId > 0 && TryResolveSpell(caster, nanoId, out spell) && spell != null;
        }

        /// <summary>
        /// The <see cref="TryStartCast"/> gates without refusing, logging or starting anything:
        /// caster state, requirements, nano cost, target NCU and hostile-target legality.
        /// </summary>
        public static bool CanStartCast(Character caster, NanoSpell spell, Character recipient, DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(caster);
            ArgumentNullException.ThrowIfNull(spell);
            ArgumentNullException.ThrowIfNull(recipient);

            if (IsNoCombat(caster) || IsNoCombat(recipient))
                return false;

            // A self-only nano would land on the caster instead.
            if (spell.IsSelfOnly && !ReferenceEquals(caster, recipient))
                return false;

            NanoCastAttempt attempt = BuildAttempt(caster, spell, recipient, ResolveNanoCost(caster, spell), nowUtc);
            if (NanoCastRules.Evaluate(attempt) != NanoCastRefusal.None)
                return false;
            if (!TargetCanHoldBuff(spell, recipient))
                return false;
            return spell.IsHostile
                ? IsHostileNanoTarget(caster, recipient)
                : FriendlyNanoTargetRefusal(caster, recipient) == NanoCastRefusal.None;
        }

        /// <summary>
        /// Spell removal of one nano id. Absent ids are a successful no-op.
        /// Uncancellable and hostile buffs are removed.
        /// </summary>
        public static bool TryStripNano(Character owner, int nanoId)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (nanoId <= 0)
                return false;

            if (owner.TryRemoveBuff(nanoId, BuffRemovalReason.Stripped, out Buff? removed)
                == BuffRemovalOutcome.Removed
                && removed != null)
                AnnounceBuffRemoved(owner, removed);

            return true;
        }

        /// <summary>
        /// Spell removal of every buff whose strain matches. Strain 0 is refused so a
        /// strainless buff is not wiped by an empty argument.
        /// </summary>
        public static bool TryStripStrain(Character owner, int strain)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (strain <= 0)
                return false;

            var matching = new List<int>();
            foreach (Buff buff in owner.Buffs)
            {
                if (buff.NanoStrain == strain)
                    matching.Add(buff.Id);
            }

            for (int i = 0; i < matching.Count; i++)
            {
                if (owner.TryRemoveBuff(matching[i], BuffRemovalReason.Stripped, out Buff? removed)
                    == BuffRemovalOutcome.Removed
                    && removed != null)
                    AnnounceBuffRemoved(owner, removed);
            }

            return true;
        }

        /// <summary>
        /// ReduceNanoStrainDuration: every NCU entry of <paramref name="strain"/> on <paramref name="owner"/> ends
        /// <paramref name="seconds"/> sooner. One that this runs out leaves NCU as a strip does (its OnTerminate runs);
        /// the rest keep running, and the client is told the time they have left.
        /// </summary>
        public static bool ReduceStrainDuration(Character owner, int strain, int seconds)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (strain <= 0 || seconds <= 0)
                return false;

            DateTime nowUtc = DateTime.UtcNow;
            var amount = TimeSpan.FromSeconds(seconds);
            var matching = new List<Buff>();
            foreach (Buff buff in owner.Buffs)
            {
                if (buff.NanoStrain == strain)
                    matching.Add(buff);
            }

            for (int i = 0; i < matching.Count; i++)
            {
                Buff buff = matching[i];
                if (buff.ExpiresAtUtc - amount <= nowUtc)
                {
                    if (owner.TryRemoveBuff(buff.Id, BuffRemovalReason.Stripped, out Buff? removed)
                        == BuffRemovalOutcome.Removed
                        && removed != null)
                        AnnounceBuffRemoved(owner, removed);
                    continue;
                }

                owner.ShortenBuff(buff, amount);
                SendRemainingDuration(owner, buff, nowUtc);
            }

            return true;
        }

        /// <summary>
        /// A zone transfer ends every <see cref="NanoSpell.RemovesOnZone"/> NCU entry and re-checks every other entry's
        /// PlayShiftRequirements (action 136) against <paramref name="owner"/> where it now stands: one it no longer
        /// meets is removed as a strip removes it (274416 [Level > 200]). Returns how many went.
        /// </summary>
        public static int StripPlayshiftFailures(Character owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            var failing = new List<int>();
            foreach (Buff buff in owner.Buffs)
            {
                if (buff.RemovesOnZone
                    || !buff.MeetsActionRequirements(stat => owner.Stats.Get(stat), ActionType.PlayShiftRequirements,
                        owner.ResolveRequirement))
                    failing.Add(buff.Id);
            }

            int removed = 0;
            foreach (int nanoId in failing)
            {
                if (StripBuff(owner, nanoId))
                    removed++;
            }

            return removed;
        }

        /// <summary>
        /// Removes one NCU entry whatever its CanCancel says, as a strip does: its OnTerminate runs and observers see
        /// it go. False when <paramref name="owner"/> does not have it.
        /// </summary>
        public static bool StripBuff(Character owner, int nanoId)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (owner.TryRemoveBuff(nanoId, BuffRemovalReason.Stripped, out Buff? removed) != BuffRemovalOutcome.Removed
                || removed == null)
                return false;

            AnnounceBuffRemoved(owner, removed);
            return true;
        }

        /// <summary>
        /// Removes every NCU entry another character put on <paramref name="owner"/>: debuffs, DoTs and
        /// buffs cast by others. What the owner or its own items cast on it (source = owner) stays, as do
        /// entries with no recorded source.
        /// </summary>
        public static int StripForeignBuffs(Character owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            Identity self = owner.Identity;
            List<Buff> removed = owner.RemoveBuffs(
                buff => buff.Source.Instance != 0 && buff.Source != self);
            for (int i = 0; i < removed.Count; i++)
                AnnounceBuffRemoved(owner, removed[i]);
            return removed.Count;
        }

        /// <summary>Spell removal of every NCU entry, including uncancellable buffs.</summary>
        public static bool StripAllBuffs(Character owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            List<Buff> removed = owner.RemoveAllBuffs(BuffRemovalReason.Stripped);
            for (int i = 0; i < removed.Count; i++)
                AnnounceBuffRemoved(owner, removed[i]);
            return true;
        }

        /// <summary>
        /// Owner-driven NCU removal. Refuses buffs that are absent, hostile, or flagged
        /// uncancellable instead of silently dropping the request.
        /// </summary>
        public static BuffRemovalOutcome TryCancelBuff(Character owner, int nanoId)
        {
            ArgumentNullException.ThrowIfNull(owner);

            BuffRemovalOutcome outcome = owner.TryRemoveBuff(
                nanoId,
                BuffRemovalReason.Cancelled,
                out Buff? removed);

            switch (outcome)
            {
                case BuffRemovalOutcome.Removed when removed != null:
                    AnnounceBuffRemoved(owner, removed);
                    break;

                case BuffRemovalOutcome.NotCancellable:
                    Refuse(owner, "That nano program cannot be cancelled.");
                    break;
            }

            return outcome;
        }

        /// <summary>
        /// Drops a cast bar in flight and tells nearby clients. Successful cast completion
        /// clears via <see cref="Character.CancelNanoCast"/> without this packet.
        /// </summary>
        public static bool InterruptCast(Character character)
        {
            ArgumentNullException.ThrowIfNull(character);

            PendingNanoCast? cast = character.PendingCast;
            if (cast == null)
                return false;

            int nanoId = cast.Spell.Id;
            character.CancelNanoCast();
            AnnounceCastInterrupted(character, nanoId);
            return true;
        }

        /// <summary>
        /// Lands a nano immediately: no cast bar, recharge, nano spend, or upload check.
        /// Buffs occupy NCU; instant nanos just run their OnUse effects.
        /// </summary>
        public static bool TryApplyImmediate(
            Character source,
            Character target,
            int nanoId,
            IItemBuilder items,
            IInventoryRepository inventory,
            DateTime nowUtc,
            bool gmApply = false)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(inventory);

            if (nanoId <= 0 || target.IsDead)
                return false;

            ItemTemplate template = items.CreateTemplate(nanoId, nanoId, quality: 1);
            if (template.Id != nanoId)
                return false;

            return LandImmediate(source, target, NanoSpell.From(template), inventory, items, nowUtc, gmApply);
        }

        /// <summary>Lands an already built <paramref name="spell"/> (CastNano2's retimed copy) at once.</summary>
        public static bool TryApplyImmediate(
            Character source,
            Character target,
            NanoSpell spell,
            IItemBuilder items,
            IInventoryRepository inventory,
            DateTime nowUtc,
            bool gmApply = false)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(spell);
            return !target.IsDead && LandImmediate(source, target, spell, inventory, items, nowUtc, gmApply);
        }

        /// <summary>
        /// Lands <paramref name="spell"/> at once. <paramref name="gmApply"/> (a GM's .give nano): combat legality does
        /// not apply, so a hostile-flagged nano such as PvP Enabled (202732) lands on any living character, and the
        /// target does not treat the GM as an attacker. NCU and strain rules still apply.
        /// </summary>
        static bool LandImmediate(
            Character source,
            Character target,
            NanoSpell spell,
            IInventoryRepository inventory,
            IItemBuilder items,
            DateTime nowUtc,
            bool gmApply = false)
        {
            if (!gmApply)
            {
                if (IsNoCombat(source) || IsNoCombat(target))
                    return false;

                if (spell.IsHostile && !IsHostileNanoTarget(source, target))
                    return false;

                if (spell.IsHostile && target.IsEvading)
                    return false;

                // A proc or a nano's child resists by strain like a cast does.
                if (spell.IsHostile && target.ResistsNanoStrain(spell.NanoStrain))
                    return false;

                // Team buffs, auras and other nanos a player's nano lands: helping a PvP-enabled player flags them.
                if (!spell.IsHostile && source is Player helper)
                    PvpAttackerFlag.OnFriendlyAction(helper, target);
            }

            AnnounceImmediateLand(source, target, spell.Id);
            AnnounceCastFinished(source, spell.Id);

            if (!spell.IsBuff)
            {
                ExecuteOnUseEffects(source, target, spell, skipPassiveModifiers: false, inventory, items);
                return true;
            }

            BuffApplyDecision decision = target.TryApplyBuff(
                spell,
                source.Identity,
                nowUtc,
                out Buff? applied,
                out Buff? replaced);

            if (applied == null)
            {
                if (source is Player)
                    Refuse(source, decision);
                return false;
            }

            if (replaced != null && replaced.Id != spell.Id)
                AnnounceBuffRemoved(target, replaced);

            SendNanoDuration(source, target, applied);
            ExecuteOnUseEffects(source, target, spell, skipPassiveModifiers: true, inventory, items);
            if (spell.IsHostile && !gmApply)
                OnDebuffLanded(source, target, applied);
            return true;
        }

        static void Complete(Character caster, PendingNanoCast cast, DateTime nowUtc)
        {
            NanoSpell spell = cast.Spell;
            Character? recipient = ResolveTarget(caster, cast.Target);

            // Everything the up-front gate checked can have changed while the bar ran. The nano cost was
            // already paid at the start, so it is not checked again.
            NanoCastAttempt attempt = BuildAttempt(caster, spell, recipient, nanoCost: 0, nowUtc);
            NanoCastRefusal refusal = NanoCastRules.Evaluate(attempt);
            // Cast bar already went out via CastNanoSpell; refusal must clear it.
            if (refusal != NanoCastRefusal.None)
            {
                LogCastRefused(
                    caster,
                    spell.Id,
                    cast.Target,
                    refusal,
                    phase: "complete",
                    attempt: attempt,
                    spell: spell);
                AnnounceCastInterrupted(caster, spell.Id);
                Refuse(caster, refusal);
                return;
            }

            if ((spell.IsHostile && !IsHostileNanoTarget(caster, recipient)) || IsNoCombat(recipient))
            {
                AnnounceCastInterrupted(caster, spell.Id);
                Refuse(caster, NanoCastRefusal.InvalidTarget);
                return;
            }

            NanoCastRefusal friendlyRefusal = spell.IsHostile
                ? NanoCastRefusal.None
                : FriendlyNanoTargetRefusal(caster, recipient);
            if (friendlyRefusal != NanoCastRefusal.None)
            {
                AnnounceCastInterrupted(caster, spell.Id);
                Refuse(caster, friendlyRefusal);
                return;
            }

            // Duplicate instant-cast packets can both reach Complete before the land window closes.
            if (!caster.TryClaimNanoLand(spell.Id, cast.Target, nowUtc))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Nano cast duplicate land suppressed caster={0} nano={1} target={2}",
                        caster.Identity.Instance,
                        spell.Id,
                        cast.Target.Instance));
                return;
            }

            AnnounceCastFinished(caster, spell.Id);

            // NoTimerNotify nanos finish with no lockout. Everyone else waits the raw template delay.
            int recharge = (spell.Flags & (int)NanoFlags.NoTimerNotify) != 0
                ? 0
                : NanoDelayCalculator.RechargeTimeCentiseconds(spell.RechargeDelayCentiseconds);
            caster.StartNanoRecharge(recharge, nowUtc);

            // A nano with a line cooldown (stat 643, or the longer local cooldown 254) locks its strain for the caster from
            // the finished cast, resisted or not. Persisted with an absolute expiry, so it keeps running while the player
            // is logged out, and FullCharacter shows the client what is left after a zone change or login.
            if (spell.NanoLineLockCentiseconds > 0 && spell.NanoStrain > 0)
                caster.LockNanoLine(spell.NanoStrain, (int)Math.Ceiling(spell.NanoLineLockCentiseconds / 100.0), nowUtc);

            // A finished hostile cast pulls the NPC whether it lands, is resisted or is refused; a resetting NPC ignores it.
            if (spell.IsHostile)
                AddHostileThreat(caster, recipient!);

            // Resisted: an evading NPC is untouchable on its way home. The cast is spent; nothing lands.
            if (spell.IsHostile && recipient!.IsEvading)
            {
                ClientFeedback.Send(caster, ClientFeedback.TargetResisted);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Nano resisted by evading target caster={0} nano={1} target={2}",
                        caster.Identity.Instance,
                        spell.Id,
                        cast.Target.Instance));
                return;
            }

            // Resisted: the target's Resist Nano for this strain (e.g. 10% Snare) won the roll.
            if (spell.IsHostile && recipient!.ResistsNanoStrain(spell.NanoStrain))
            {
                ClientFeedback.Send(caster, ClientFeedback.TargetResisted);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Nano resisted by strain caster={0} nano={1} target={2} strain={3} chance={4}%",
                        caster.Identity.Instance,
                        spell.Id,
                        cast.Target.Instance,
                        spell.NanoStrain,
                        recipient.NanoStrainResistPercent(spell.NanoStrain)));
                return;
            }

            // Resisted: the target's nano defense (Nano Resist + Add All Def.) beat the nano's attack skills + Add All
            // Off. See DamageCalculator.ResolveNanoLand; the curve may need tuning.
            if (spell.IsHostile
                && !DamageCalculator.ResolveNanoLand(caster, recipient!, spell, out int attackRating, out int defenseRating, out int landChance))
            {
                ClientFeedback.Send(caster, ClientFeedback.TargetResisted);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Nano resisted caster={0} nano={1} target={2} attack={3} defense={4} landChance={5}%",
                        caster.Identity.Instance,
                        spell.Id,
                        cast.Target.Instance,
                        attackRating,
                        defenseRating,
                        landChance));
                return;
            }

            if (!spell.IsBuff)
            {
                ClientFeedback.Send(caster, ClientFeedback.NanoExecutedSuccessfully);
                ExecuteOnUseEffects(caster, recipient!, spell, skipPassiveModifiers: false);
                return;
            }

            BuffApplyDecision decision = recipient!.TryApplyBuff(
                spell,
                caster.Identity,
                nowUtc,
                out Buff? applied,
                out Buff? replaced);

            // Cast already finished and spent nano. Apply refusal (e.g. NCU) does not interrupt it.
            if (applied == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Nano apply refused caster={0} nano={1} target={2} decision={3} ncu={4}/{5} strain={6} stacking={7} ncuCost={8}",
                        caster.Identity.Instance,
                        spell.Id,
                        recipient.Identity.Instance,
                        decision,
                        recipient.UsedNcu,
                        recipient.MaxNcu,
                        spell.NanoStrain,
                        spell.StackingOrder,
                        spell.NcuCost));
                Refuse(caster, decision);
                return;
            }

            // Player casts use SetNanoDuration for NCU display (legacy never Buff-adds on land).
            // Buff remove is only for a different nano leaving the same strain.
            if (replaced != null && replaced.Id != spell.Id)
                AnnounceBuffRemoved(recipient, replaced);

            ClientFeedback.Send(caster, ClientFeedback.NanoExecutedSuccessfully);
            SendNanoDuration(caster, recipient, applied);
            ExecuteOnUseEffects(caster, recipient, spell, skipPassiveModifiers: true);
            if (spell.IsHostile)
                OnDebuffLanded(caster, recipient, applied, addThreat: false);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Nano landed caster={0} nano={1} target={2} ncu={3}/{4} duration={5}cs recharge={6}cs",
                    caster.Identity.Instance,
                    spell.Id,
                    recipient.Identity.Instance,
                    recipient.UsedNcu,
                    recipient.MaxNcu,
                    applied.DurationCentiseconds,
                    recharge));
        }

        static NanoCastAttempt BuildAttempt(
            Character caster,
            NanoSpell spell,
            Character? recipient,
            int nanoCost,
            DateTime nowUtc)
            => new()
            {
                CasterIsDead = caster.IsDead,
                CasterIsStunned = caster.IsStunned,
                CasterIsCasting = caster.IsCastingNano,
                CasterIsRecharging = caster.IsInNanoRecharge(nowUtc),
                NanoLineLocked = spell.NanoStrain > 0 && caster.NanoLineLocks.IsLocked(spell.NanoStrain, nowUtc),
                // NPC casts are not upload gated; an NPC's list only holds its equipped crystals.
                IsUploaded = !caster.IsPlayer || caster.UploadedNanoIds.Contains(spell.Id),
                // OnTarget leaves the target's stats cannot answer (a running nano line, a perk, a worn item) read the
                // target itself, as item use does: Gallant Slave 230372 needs the charm (line 202) on the target.
                RequirementsMet = spell.MeetsActionRequirements(
                    stat => caster.Stats.Get(stat),
                    ActionType.ToUse,
                    resolve: ItemTemplate.WithActionTarget(
                        caster is Player casterPlayer ? casterPlayer.ResolvePerkRequirement : null, caster, recipient),
                    getTargetStat: recipient == null ? null : stat => recipient.Stats.Get(stat),
                    resolveTarget: recipient == null
                        ? null
                        : ItemTemplate.WithActionTarget(recipient.ResolveRequirement, caster, recipient)),
                TargetExists = recipient != null,
                TargetIsDead = recipient?.IsDead == true,
                CurrentNano = caster.Stats.GetOrZero(CharacterStat.CurrentNano),
                NanoCost = nanoCost
            };

        /// <summary>
        /// Start-time NCU preview. Instant, hostile, and NoRemoveNoNCUFriendly nanos skip it.
        /// Land still re-checks via <see cref="BuffApplyRules"/>; failure there finishes the
        /// cast without applying.
        /// </summary>
        /// <summary>
        /// A debuff landing can break crowd control already on the target, and a player's debuff
        /// always puts the player on an NPC's hate list, so debuffing an idle mob pulls it.
        /// A pacified NPC still ignores that threat. <paramref name="addThreat"/> is false when the
        /// finished cast already added it.
        /// </summary>
        static void OnDebuffLanded(Character caster, Character recipient, Buff applied, bool addThreat = true)
        {
            recipient.RollBuffBreaks(BuffBreakCause.Debuff, caster, except: applied);
            if (addThreat)
                AddHostileThreat(caster, recipient);
        }

        /// <summary>
        /// A player's hostile nano puts the player on the NPC's hate list (ignored while resetting or pacified). A
        /// player's pet's does the same for the pet, and for its owner (NpcBrain.AddThreatFrom).
        /// </summary>
        static void AddHostileThreat(Character caster, Character recipient)
        {
            if (recipient is NpcCharacter npc && !ReferenceEquals(caster, recipient)
                && (caster.IsPlayer || caster is NpcCharacter { PetOwner: Player }))
                npc.Brain?.AddThreatFrom(caster, Ai.NpcAiRules.ProximityHate);
        }

        static bool TargetCanHoldBuff(NanoSpell spell, Character? recipient)
        {
            if (recipient == null || !spell.IsBuff || !spell.ConsumesNcu)
                return true;

            // Same cap the landing uses, so a cast is never refused for NCU the buff would not need.
            BuffApplyDecision decision = BuffApplyRules.Evaluate(
                spell,
                recipient.Buffs,
                recipient.BuffNcuCapacity,
                out _);
            return decision != BuffApplyDecision.RefusedNotEnoughNcu;
        }

        /// <summary>The landing's stacking rule, asked up front: a stronger nano of the strain already runs on the target.</summary>
        static bool TargetRunsBetterNano(NanoSpell spell, Character? recipient)
        {
            if (recipient == null || !spell.IsBuff || spell.NanoStrain <= 0)
                return false;

            return BuffApplyRules.Evaluate(spell, recipient.Buffs, recipient.BuffNcuCapacity, out _)
                == BuffApplyDecision.RefusedStrainStronger;
        }

        static bool TryResolveSpell(Character caster, int nanoId, out NanoSpell? spell)
        {
            spell = null;
            Playfield? playfield = caster.Playfield;
            if (playfield == null)
                return false;

            ItemTemplate template = playfield
                .GetRequiredService<IItemBuilder>()
                .CreateTemplate(nanoId, nanoId, quality: 1);
            if (template.Id != nanoId)
                return false;

            spell = NanoSpell.From(template);
            return true;
        }

        static int ResolveNanoCost(Character caster, NanoSpell spell)
            => NanoCostCalculator.Compute(
                spell.NanoPointCost,
                caster.Stats.GetOrZero(CharacterStat.NPCostModifier),
                (Breed)caster.Stats.GetOrZero(CharacterStat.Breed));

        static void SpendNano(Character caster, int cost)
        {
            if (cost <= 0)
                return;

            int remaining = Math.Max(0, caster.Stats.GetOrZero(CharacterStat.CurrentNano) - cost);
            caster.Stats.Set(CharacterStat.CurrentNano, remaining, StatDetail.Base, dirty: true);
        }

        /// <summary>A NoCombat NPC (not attackable): it neither casts nanos nor has nanos cast on it.</summary>
        static bool IsNoCombat(Character? character) => character is NpcCharacter { Attackable: false };

        /// <summary>
        /// Hostile nanos may land on the caster. Other targets still have to be legal to attack.
        /// </summary>
        static bool IsHostileNanoTarget(Character caster, Character? target)
            => target != null && (ReferenceEquals(caster, target) || CombatRules.CanAttack(caster, target));

        /// <summary>Side (stat 33) of monsters.</summary>
        const int MonsterSide = 3;

        /// <summary>The client's nano range ceiling in metres (Gamecode.dll 1008636b).</summary>
        const int MaxNanoRange = 40;

        /// <summary>
        /// The client's own gates on a player starting a nano (Gamecode.dll 1004f9c4), in its order, so a modified client
        /// cannot skip them: not in a vehicle, standing still, not swimming, crawling or falling, standing up, a target not
        /// in a vehicle, a CannotRefresh nano only when its strain is not already running on the caster (1004f96d), the
        /// target in range (1008636b) and in line of sight (10058c2f). A cast on oneself skips range and line of sight.
        /// </summary>
        static NanoCastRefusal PlayerCastGate(Character caster, NanoSpell spell, Character? recipient)
        {
            if (caster.Stats.GetOrZero(CharacterStat.MechData) != 0)
                return NanoCastRefusal.CasterInVehicle;
            if (caster.Motor.IsMoving)
                return NanoCastRefusal.MustStandStill;

            switch ((MovementState)caster.Stats.GetOrZero(CharacterStat.CurrentMovementMode))
            {
                case MovementState.Swim:
                    return NanoCastRefusal.Swimming;
                case MovementState.Crawl:
                    return NanoCastRefusal.Crawling;
            }

            if (caster.Motor.IsFalling)
                return NanoCastRefusal.Falling;

            switch ((MovementState)caster.Stats.GetOrZero(CharacterStat.CurrentMovementMode))
            {
                case MovementState.Sit:
                case MovementState.RootedCanSit:
                case MovementState.Sleep:
                case MovementState.Lounge:
                    return NanoCastRefusal.MustStandUp;
            }

            if (recipient == null || ReferenceEquals(recipient, caster))
                return recipient == null ? NanoCastRefusal.None : CantReplaceRefusal(caster, spell);
            if (recipient.Stats.GetOrZero(CharacterStat.MechData) != 0)
                return NanoCastRefusal.TargetInVehicle;

            NanoCastRefusal replace = CantReplaceRefusal(caster, spell);
            if (replace != NanoCastRefusal.None)
                return replace;

            if (Math.Floor(caster.GetEdgeDistanceTo(recipient)) > NanoRange(caster, spell))
                return NanoCastRefusal.OutOfRange;
            if (!caster.HasLineOfSightTo(recipient))
                return NanoCastRefusal.NoLineOfSight;
            return NanoCastRefusal.None;
        }

        /// <summary>A CannotRefresh nano cannot go up while its strain already runs on the caster (client 1004f96d).</summary>
        static NanoCastRefusal CantReplaceRefusal(Character caster, NanoSpell spell)
        {
            if (((NanoFlags)spell.Flags & NanoFlags.CannotRefresh) == 0 || spell.NanoStrain <= 0)
                return NanoCastRefusal.None;
            foreach (Buff running in caster.Buffs)
            {
                if (running.NanoStrain == spell.NanoStrain)
                    return NanoCastRefusal.CantReplaceOther;
            }

            return NanoCastRefusal.None;
        }

        /// <summary>
        /// Client 1008636b: the nano's AttackRange (stat 287) raised by the caster's NanoRange percent (stat 381),
        /// truncated, at most <see cref="MaxNanoRange"/>.
        /// </summary>
        static int NanoRange(Character caster, NanoSpell spell)
        {
            int baseRange = spell.Stats.TryGetValue(CharacterStat.AttackRange, out int value)
                ? StatCollection.Normalize(value)
                : 0;
            double range = baseRange + baseRange * caster.Stats.GetOrZero(CharacterStat.NanoRange) / 100.0;
            return Math.Min((int)range, MaxNanoRange);
        }

        /// <summary>
        /// The client's friendly-nano target checks, in its order. Self and the caster's own pets (N3Msg_IsMyPetID's
        /// list) always pass; null (target gone) is left to the other gates.
        /// <list type="number">
        ///   <item>Gamecode.dll 1004f9c4: a player may not run a friendly nano on any other NPC
        ///     ("Feedback_MolecularStructurePreventsUse"). An NPC caster is exempt.</item>
        ///   <item>1004eb0f: nor on a monster-side target unless the caster is monster-side too
        ///     ("Feedback_YouAreNotAllowedToExecuteFriendlyOnEnemy").</item>
        /// </list>
        /// </summary>
        internal static NanoCastRefusal FriendlyNanoTargetRefusal(Character caster, Character? target)
        {
            if (target == null || ReferenceEquals(caster, target)
                || (target is NpcCharacter pet && ReferenceEquals(pet.PetOwner, caster)))
                return NanoCastRefusal.None;
            if (target is NpcCharacter && caster is Player)
                return NanoCastRefusal.FriendlyOnNpc;
            if (target.Stats.GetOrZero(CharacterStat.Side) == MonsterSide
                && caster.Stats.GetOrZero(CharacterStat.Side) != MonsterSide)
                return NanoCastRefusal.FriendlyOnEnemy;
            return NanoCastRefusal.None;
        }

        /// <summary>Self, or a character in the same playfield. Null means the target is gone.</summary>
        static Character? ResolveTarget(Character caster, Identity target)
        {
            if (target.Instance == 0 || target.Instance == caster.Identity.Instance)
                return caster;

            Playfield? playfield = caster.Playfield;
            if (playfield == null)
                return null;

            return playfield.GetRequiredService<DynelRegistry>().TryGet(target, out Dynel? dynel)
                && dynel is Character character
                ? character
                : null;
        }

        internal static void Refuse(Character caster, NanoCastRefusal refusal)
        {
            switch (refusal)
            {
                case NanoCastRefusal.CasterDead:
                    ClientFeedback.Send(caster, "Feedback_TargetIsAlreadyDead");
                    return;
                case NanoCastRefusal.RequirementsNotMet:
                    ClientFeedback.Send(caster, ClientFeedback.CheckItemRequirements);
                    return;
                case NanoCastRefusal.AlreadyCasting:
                    ClientFeedback.Send(caster, "Feedback_WaitForCurrentNanoprogram");
                    return;
                case NanoCastRefusal.Recharging:
                    ClientFeedback.Send(caster, "Feedback_NanobotsAreRecharging");
                    return;
                case NanoCastRefusal.NotEnoughNano:
                    ClientFeedback.Send(caster, "Feedback_NanoprogramDidNotActivateNotEnoughNanoenergy");
                    return;
                case NanoCastRefusal.NotEnoughNcu:
                    ClientFeedback.Send(caster, ClientFeedback.TargetNotEnoughNcu);
                    return;
                case NanoCastRefusal.InvalidTarget:
                    ClientFeedback.Send(caster, "Feedback_UnableToExecuteOnThisTarget");
                    return;
                case NanoCastRefusal.TargetDead:
                    ClientFeedback.Send(caster, "Feedback_TargetIsAlreadyDead");
                    return;
                case NanoCastRefusal.CasterInVehicle:
                    ClientFeedback.Send(caster, "Feedback_NotInVehicle");
                    return;
                case NanoCastRefusal.MustStandStill:
                    ClientFeedback.Send(caster, "Feedback_UnableToExecuteProgramsMustStandStill");
                    return;
                case NanoCastRefusal.Swimming:
                    ClientFeedback.Send(caster, "Feedback_YouCantExecuteNanoprogramsWhileSwimming");
                    return;
                case NanoCastRefusal.Crawling:
                    ClientFeedback.Send(caster, "Feedback_YouCantExecuteNanoprogramsWhileCrawling");
                    return;
                case NanoCastRefusal.Falling:
                    ClientFeedback.Send(caster, "Feedback_YouCantExecuteNanoprogramsWhileFalling");
                    return;
                case NanoCastRefusal.MustStandUp:
                    ClientFeedback.Send(caster, "Feedback_YouMustBeStandingUpToExecuteNanoprogram");
                    return;
                case NanoCastRefusal.TargetInVehicle:
                    ClientFeedback.Send(caster, "Feedback_YouCantUseOnVehicles");
                    return;
                case NanoCastRefusal.CantReplaceOther:
                    ClientFeedback.Send(caster, "Feedback_NCUErrorNanoprogramCantReplaceOther");
                    return;
                case NanoCastRefusal.OutOfRange:
                    ClientFeedback.Send(caster, "Feedback_TargetOutOfRangeForNanoprogram");
                    return;
                case NanoCastRefusal.NoLineOfSight:
                    ClientFeedback.Send(caster, "Feedback_TargetNotInLineOfSight");
                    return;
                case NanoCastRefusal.FriendlyOnNpc:
                    ClientFeedback.Send(caster, "Feedback_MolecularStructurePreventsUse");
                    return;
                case NanoCastRefusal.FriendlyOnEnemy:
                    ClientFeedback.Send(caster, "Feedback_YouAreNotAllowedToExecuteFriendlyOnEnemy");
                    return;
                case NanoCastRefusal.BetterNanoRunning:
                    // "NCU error: Better nano program already running." then "Unable to execute nano program. You can't
                    // execute this nano on the target." (110/101968183, 110/184471956).
                    ClientFeedback.Send(caster, "Feedback_NCUErrorBetterProgramRunning");
                    ClientFeedback.Send(caster, "Feedback_UnableToExecuteOnThisTarget");
                    return;
                default:
                    Refuse(caster, NanoCastRules.Describe(refusal));
                    return;
            }
        }

        static void Refuse(Character caster, BuffApplyDecision decision)
        {
            switch (decision)
            {
                case BuffApplyDecision.RefusedNotEnoughNcu:
                    ClientFeedback.Send(caster, ClientFeedback.TargetNotEnoughNcu);
                    return;
                case BuffApplyDecision.RefusedStrainStronger:
                    ClientFeedback.Send(caster, "Feedback_NCUErrorBetterProgramRunning");
                    return;
                default:
                    Refuse(caster, "That nano program had no effect.");
                    return;
            }
        }

        static void AnnounceCastStarted(Character caster, int nanoId, Identity target)
            => Announce(
                caster,
                new CastNanoSpellMessage
                {
                    Identity = caster.Identity,
                    // NPC casts leave Caster empty; player casts name themselves.
                    Caster = caster.IsPlayer ? caster.Identity : Identity.None,
                    Target = target,
                    NanoId = nanoId,
                    Unknown = 0,
                    Unknown1 = 0
                });

        /// <summary>
        /// The effect of a nano an item or perk function lands with no cast (CastNano / AreaCastNano...): live sends
        /// CastNanoSpell addressed to the recipient, Target = recipient, Unknown1 = 1, Caster = the user, then
        /// SetNanoDuration for a buff (captures 2026-10-02T03:54:58Z Impale on a mob, 15:12:44Z Dance of Fools on
        /// self). FinishNanoCasting(1, nano) follows (live 2026-10-02T15:28:59Z): client case 0x1d (Gamecode.dll
        /// 0x1004f322) closes the pending cast entry CastNanoSpell opened and records the success result.
        /// </summary>
        static void AnnounceImmediateLand(Character source, Character target, int nanoId)
            => Announce(
                target,
                new CastNanoSpellMessage
                {
                    Identity = target.Identity,
                    NanoId = nanoId,
                    Target = target.Identity,
                    Unknown = 0,
                    Unknown1 = 1,
                    Caster = source.Identity
                });

        static void AnnounceCastFinished(Character caster, int nanoId)
            => Announce(
                caster,
                new CharacterActionMessage
                {
                    Identity = caster.Identity,
                    Unknown = 0x00,
                    Action = CharacterActionType.FinishNanoCasting,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = 1,
                    Parameter2 = nanoId,
                    Unknown2 = 0
                });

        // Live IN InterruptNanoCasting: Parameter1=nanoId, Parameter2=reason (observed 7 / 4).
        // FinishNanoCasting keeps Parameter1=1, Parameter2=nanoId — do not reuse that layout here.
        const int DefaultInterruptReason = 7;

        static void AnnounceCastInterrupted(Character caster, int nanoId, int reason = DefaultInterruptReason)
        {
            var message = new CharacterActionMessage
            {
                Identity = caster.Identity,
                Unknown = 0x00,
                Action = CharacterActionType.InterruptNanoCasting,
                Unknown1 = 0,
                Target = Identity.None,
                Parameter1 = nanoId,
                Parameter2 = reason,
                Unknown2 = 0
            };

            // Cast bar lives on the caster client; always deliver there first.
            SessionOf(caster)?.Send(message);

            Cell? cell = caster.Cell;
            if (cell != null)
                cell.Announce(message, exclude: caster);
        }

        static void ExecuteOnUseEffects(
            Character caster,
            Character recipient,
            NanoSpell spell,
            bool skipPassiveModifiers,
            IInventoryRepository? inventory = null,
            IItemBuilder? items = null)
        {
            if (inventory == null || items == null)
            {
                Playfield? playfield = recipient.Playfield ?? caster.Playfield;
                if (playfield == null)
                    return;

                inventory ??= playfield.GetRequiredService<IInventoryRepository>();
                items ??= playfield.GetRequiredService<IItemBuilder>();
            }

            spell.ExecuteOnUseSpells(
                recipient,
                inventory,
                items,
                skipPassiveModifiers,
                source: caster);
        }

        static void ExecuteBuffEnd(Character owner, Buff buff)
        {
            Playfield? playfield = owner.Playfield;
            // A charm lasts as long as its nano: when the nano runs out or is stripped, the NPC is its own again
            // before the nano's OnTerminate runs on it (so its Hit lands on the NPC, not on a pet), and it settles
            // after that Hit has had its say. A charm ended any other way already let go of the NPC.
            Pets.PetService? pets = playfield?.GetService<Pets.PetService>();
            NpcCharacter? released = null;
            if (pets != null && owner is NpcCharacter { Pet.Charm.NanoId: int charmNano } charmed && charmNano == buff.Id)
            {
                pets.EndCharm(charmed, "charm ended", settle: false);
                released = charmed;
            }

            if (playfield != null)
            {
                IInventoryRepository inventory = playfield.GetRequiredService<IInventoryRepository>();
                IItemBuilder items = playfield.GetRequiredService<IItemBuilder>();
                // Attributed to the caster while it is still here, as each tick is.
                Character? source = playfield.GetRequiredService<DynelRegistry>().TryGet(buff.Source, out Dynel? dynel)
                    && dynel is Character caster
                    ? caster
                    : null;
                buff.ExecuteTerminateSpells(owner, inventory, items, source);
            }

            if (released != null)
                pets!.SettleAfterCharm(released, turnOnOwner: true);

            if (owner is not Player)
                owner.MarkRebaseDirty();

            // NpcScripts: a program may react to one of its nanos running out.
            (owner as NpcCharacter)?.Scripts?.Raise(NpcScripts.NpcScriptTrigger.NanoEnded, buff.Id);
        }

        static void AnnounceBuffRemoved(Character owner, Buff buff)
        {
            var message = new BuffMessage
            {
                Identity = owner.Identity,
                Action = 0,
                NanoProgram = new Identity
                {
                    Type = IdentityType.NanoProgram,
                    Instance = buff.Id
                }
            };

            SessionOf(owner)?.Send(message);
            // Owner already has the direct copy. Everyone else who can see them needs the clear.
            owner.Cell?.Announce(message, exclude: owner);

            // End effects run after the clear: an OnTerminate that recasts the same nano (Minor Regeneration of
            // Health and Nano 291081) lands a fresh copy the client must not then see removed.
            ExecuteBuffEnd(owner, buff);
        }

        /// <summary>
        /// Duration is the buff landing. Caster and owner get a direct copy for NCU.
        /// The owner's cell gets the same packet so other players see the buff.
        /// </summary>
        /// <summary>
        /// A running NCU entry's time changed: the owner and everyone who sees it get SetNanoDuration with what is
        /// left, from now, as a spawn packet's NCU list would carry it.
        /// </summary>
        static void SendRemainingDuration(Character owner, Buff buff, DateTime nowUtc)
        {
            var message = new CharacterActionMessage
            {
                Identity = owner.Identity,
                Unknown = 0x00,
                Action = CharacterActionType.SetNanoDuration,
                Unknown1 = 0,
                Target = new Identity
                {
                    Type = IdentityType.NanoProgram,
                    Instance = buff.Id
                },
                Parameter1 = buff.Source.Instance,
                Parameter2 = buff.RemainingCentiseconds(nowUtc),
                Unknown2 = 0
            };

            SessionOf(owner)?.Send(message);
            owner.Cell?.Announce(message, exclude: owner);
        }

        static void SendNanoDuration(Character caster, Character owner, Buff buff)
        {
            var message = new CharacterActionMessage
            {
                Identity = owner.Identity,
                Unknown = 0x00,
                Action = CharacterActionType.SetNanoDuration,
                Unknown1 = 0,
                Target = new Identity
                {
                    Type = IdentityType.NanoProgram,
                    Instance = buff.Id
                },
                Parameter1 = caster.Identity.Instance,
                Parameter2 = buff.DurationCentiseconds,
                Unknown2 = 0
            };

            SessionOf(caster)?.Send(message);
            if (!ReferenceEquals(caster, owner))
                SessionOf(owner)?.Send(message);

            owner.Cell?.Announce(
                message,
                exclude: owner,
                alsoExclude: ReferenceEquals(caster, owner) ? null : caster);
        }

        static void LogCastRefused(
            Character caster,
            int nanoId,
            Identity target,
            NanoCastRefusal refusal,
            string phase,
            NanoCastAttempt? attempt = null,
            NanoSpell? spell = null,
            string? detail = null)
        {
            string attemptText = attempt is NanoCastAttempt a
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    " dead={0} casting={1} recharging={2} uploaded={3} reqs={4} targetOk={5} targetDead={6} nano={7}/{8}",
                    a.CasterIsDead,
                    a.CasterIsCasting,
                    a.CasterIsRecharging,
                    a.IsUploaded,
                    a.RequirementsMet,
                    a.TargetExists,
                    a.TargetIsDead,
                    a.CurrentNano,
                    a.NanoCost)
                : string.Empty;

            string spellText = spell != null
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    " templateId={0} name={1} actions={2} isBuff={3}",
                    spell.Id,
                    spell.Name ?? string.Empty,
                    spell.Actions.Count,
                    spell.IsBuff)
                : string.Empty;

            string uploadedText = string.Format(
                CultureInfo.InvariantCulture,
                " uploadedCount={0} hasNano={1}",
                caster.UploadedNanoIds.Count,
                caster.UploadedNanoIds.Contains(nanoId));

            string reqDetail = string.Empty;
            if (refusal == NanoCastRefusal.RequirementsNotMet && spell != null)
                reqDetail = DescribeFailedRequirements(caster, spell, ResolveTarget(caster, target));

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Nano cast refused phase={0} caster={1} nano={2} target={3}:{4} reason={5}{6}{7}{8}{9}{10}",
                    phase,
                    caster.Identity.Instance,
                    nanoId,
                    target.Type,
                    target.Instance,
                    refusal,
                    attemptText,
                    spellText,
                    uploadedText,
                    reqDetail,
                    detail != null ? " detail=" + detail : string.Empty));
        }

        /// <summary>Failing leaves, each read from the subject its selector picked (OnTarget: the target).</summary>
        static string DescribeFailedRequirements(Character caster, NanoSpell spell, Character? recipient)
        {
            ItemAction? action = null;
            foreach (ItemAction candidate in spell.Actions)
            {
                if (candidate.ActionType == (int)ActionType.ToUse)
                {
                    action = candidate;
                    break;
                }
            }

            if (action == null)
                return " failedReqs=(no ToUse action)";

            var parts = new List<string>();
            Character subject = caster;
            foreach (ItemRequirement requirement in action.Requirements)
            {
                if (ItemTemplate.IsRequirementLinkOperator(requirement))
                    continue;
                if (ItemTemplate.IsSubjectSelector(requirement))
                {
                    subject = (Operator)requirement.Operator == Operator.OnTarget ? recipient ?? caster : caster;
                    continue;
                }

                Character reader = subject;
                int have = reader.Stats.Get((CharacterStat)requirement.StatNumber);
                if (ItemTemplate.EvaluateLeaf(requirement, stat => reader.Stats.Get(stat)))
                    continue;

                parts.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "stat={0}({1}) have={2} op={3} need={4} child={5}{6}",
                        (CharacterStat)requirement.StatNumber,
                        requirement.StatNumber,
                        have,
                        requirement.Operator,
                        requirement.Value,
                        requirement.ChildOperator,
                        ReferenceEquals(reader, caster) ? string.Empty : " onTarget"));
            }

            return parts.Count == 0
                ? " failedReqs=(expression/link fold)"
                : " failedReqs=[" + string.Join("; ", parts) + "]";
        }

        static void Refuse(Character caster, string text)
        {
            if (text.Length == 0)
                return;

            SessionOf(caster)?.Send(
                new ChatTextMessage
                {
                    Identity = caster.Identity,
                    Text = text,
                    Unknown1 = 0,
                    Unknown2 = 0,
                    Unknown3 = 0
                });
        }

        static IZoneSession? SessionOf(Character character)
            => character is Player player ? player.Session : null;

        static void Announce(Character character, MessageBody body)
        {
            Cell? cell = character.Cell;
            if (cell != null)
            {
                cell.Announce(body);
                return;
            }

            SessionOf(character)?.Send(body);
        }
    }
}
