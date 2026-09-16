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

            List<Buff> expired = character.DrainExpiredBuffs(nowUtc);
            for (int i = 0; i < expired.Count; i++)
                AnnounceBuffRemoved(character, expired[i]);
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
                Refuse(caster, NanoCastRules.Describe(NanoCastRefusal.NotUploaded));
                return NanoCastRefusal.NotUploaded;
            }

            Character? recipient = ResolveTarget(caster, target);
            int nanoCost = ResolveNanoCost(caster, spell);
            NanoCastAttempt attempt = BuildAttempt(caster, spell, recipient, nanoCost, nowUtc);
            NanoCastRefusal refusal = NanoCastRules.Evaluate(attempt);
            // Start-only: do not begin a cast the target cannot hold. Land-time NCU is checked
            // again in TryApplyBuff; a mid-cast capacity change still finishes the cast.
            if (refusal == NanoCastRefusal.None && !TargetCanHoldBuff(spell, recipient))
                refusal = NanoCastRefusal.NotEnoughNcu;

            if (refusal != NanoCastRefusal.None)
            {
                LogCastRefused(caster, nanoId, target, refusal, phase: "start", attempt: attempt, spell: spell);
                Refuse(caster, NanoCastRules.Describe(refusal));
                return refusal;
            }

            int attackTime = NanoDelayCalculator.AttackTimeCentiseconds(
                spell.AttackDelayCentiseconds,
                spell.AttackDelayCapCentiseconds,
                caster.Stats.GetOrZero(CharacterStat.AggDef),
                caster.Stats.GetOrZero(CharacterStat.NanoCInit));

            Identity resolvedTarget = recipient!.Identity;
            var cast = new PendingNanoCast(spell, resolvedTarget, nanoCost, attackTime, nowUtc);
            caster.BeginNanoCast(cast);
            AnnounceCastStarted(caster, spell.Id, resolvedTarget);

            // Instant nanos have no cast bar; land them in the same tick.
            if (cast.IsReady(nowUtc))
            {
                caster.CancelNanoCast();
                Complete(caster, cast, nowUtc);
            }

            return NanoCastRefusal.None;
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

        static void Complete(Character caster, PendingNanoCast cast, DateTime nowUtc)
        {
            NanoSpell spell = cast.Spell;
            Character? recipient = ResolveTarget(caster, cast.Target);

            // Everything the up-front gate checked can have changed while the bar ran.
            NanoCastAttempt attempt = BuildAttempt(caster, spell, recipient, cast.NanoCost, nowUtc);
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
                Refuse(caster, NanoCastRules.Describe(refusal));
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

            SpendNano(caster, cast.NanoCost);
            AnnounceCastFinished(caster, spell.Id);

            caster.StartNanoRecharge(
                NanoDelayCalculator.RechargeTimeCentiseconds(
                    spell.RechargeDelayCentiseconds,
                    spell.RechargeDelayCapCentiseconds,
                    caster.Stats.GetOrZero(CharacterStat.AggDef),
                    caster.Stats.GetOrZero(CharacterStat.NanoCInit)),
                nowUtc);

            if (!spell.IsBuff)
            {
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
                Refuse(caster, DescribeApplyRefusal(decision));
                return;
            }

            // Player casts use SetNanoDuration for NCU display (legacy never Buff-adds on land).
            // Buff remove is only for a different nano leaving the same strain.
            if (replaced != null && replaced.Id != spell.Id)
                AnnounceBuffRemoved(recipient, replaced);

            SendNanoDuration(caster, recipient, applied);
            ExecuteOnUseEffects(caster, recipient, spell, skipPassiveModifiers: true);
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
                    spell.RechargeDelayCentiseconds));
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
                CasterIsCasting = caster.IsCastingNano,
                CasterIsRecharging = caster.IsInNanoRecharge(nowUtc),
                // NPC casts are not upload gated; only players own a nano program list.
                IsUploaded = !caster.IsPlayer || caster.UploadedNanoIds.Contains(spell.Id),
                RequirementsMet = spell.MeetsActionRequirements(
                    stat => caster.Stats.Get(stat),
                    ActionType.ToUse),
                TargetExists = recipient != null,
                TargetIsDead = recipient?.IsDead == true,
                CurrentNano = caster.Stats.GetOrZero(CharacterStat.CurrentNano),
                NanoCost = nanoCost
            };

        /// <summary>
        /// Start-time NCU preview. Instant and hostile nanos skip it. Land still re-checks via
        /// <see cref="BuffApplyRules"/>; failure there finishes the cast without applying.
        /// </summary>
        static bool TargetCanHoldBuff(NanoSpell spell, Character? recipient)
        {
            if (recipient == null || !spell.IsBuff || spell.IsHostile)
                return true;

            BuffApplyDecision decision = BuffApplyRules.Evaluate(
                spell,
                recipient.Buffs,
                recipient.MaxNcu,
                out _);
            return decision != BuffApplyDecision.RefusedNotEnoughNcu;
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
                caster.Stats.GetOrZero(CharacterStat.NPCostModifier));

        static void SpendNano(Character caster, int cost)
        {
            if (cost <= 0)
                return;

            int remaining = Math.Max(0, caster.Stats.GetOrZero(CharacterStat.CurrentNano) - cost);
            caster.Stats.Set(CharacterStat.CurrentNano, remaining, StatDetail.Base, dirty: true);
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

        static string DescribeApplyRefusal(BuffApplyDecision decision) => decision switch
        {
            BuffApplyDecision.RefusedNotEnoughNcu => "Not enough NCU.",
            BuffApplyDecision.RefusedStrainStronger => "A stronger nano program of that type is already running.",
            _ => "That nano program had no effect.",
        };

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
            bool skipPassiveModifiers)
        {
            Playfield? playfield = recipient.Playfield ?? caster.Playfield;
            if (playfield == null)
                return;

            IInventoryRepository inventory = playfield.GetRequiredService<IInventoryRepository>();
            IItemBuilder items = playfield.GetRequiredService<IItemBuilder>();
            spell.ExecuteOnUseSpells(
                recipient,
                inventory,
                items,
                skipPassiveModifiers,
                source: caster);
        }

        static void ExecuteBuffEnd(Character owner, Buff buff)
        {
            //Shouldn't this happen as part of the rebase?
            buff.ReverseOnUseSetFlags(owner);

            Playfield? playfield = owner.Playfield;
            if (playfield != null)
            {
                IInventoryRepository inventory = playfield.GetRequiredService<IInventoryRepository>();
                IItemBuilder items = playfield.GetRequiredService<IItemBuilder>();
                buff.ExecuteTerminateSpells(owner, inventory, items);
            }

            if (owner is not Player)
                owner.MarkRebaseDirty();
        }

        static void AnnounceBuffRemoved(Character owner, Buff buff)
        {
            ExecuteBuffEnd(owner, buff);

            SessionOf(owner)?.Send(
                new BuffMessage
                {
                    Identity = owner.Identity,
                    Action = 0,
                    NanoProgram = new Identity
                    {
                        Type = IdentityType.NanoProgram,
                        Instance = buff.Id
                    }
                });
        }

        /// <summary>Duration drives the NCU countdown; both caster and target need it.</summary>
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
                reqDetail = DescribeFailedRequirements(caster, spell);

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

        static string DescribeFailedRequirements(Character caster, NanoSpell spell)
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
            foreach (ItemRequirement requirement in action.Requirements)
            {
                if (ItemTemplate.IsRequirementLinkOperator(requirement))
                    continue;

                int have = caster.Stats.Get((CharacterStat)requirement.StatNumber);
                if (ItemTemplate.EvaluateRequirement(have, requirement))
                    continue;

                parts.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "stat={0}({1}) have={2} op={3} need={4} child={5}",
                        (CharacterStat)requirement.StatNumber,
                        requirement.StatNumber,
                        have,
                        requirement.Operator,
                        requirement.Value,
                        requirement.ChildOperator));
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
