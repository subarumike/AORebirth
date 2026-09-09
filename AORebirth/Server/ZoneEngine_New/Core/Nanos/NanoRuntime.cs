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
                return NanoCastRefusal.NotUploaded;

            if (!TryResolveSpell(caster, nanoId, out NanoSpell? spell) || spell == null)
            {
                Refuse(caster, NanoCastRules.Describe(NanoCastRefusal.NotUploaded));
                return NanoCastRefusal.NotUploaded;
            }

            Character? recipient = ResolveTarget(caster, target);
            int nanoCost = ResolveNanoCost(caster, spell);
            NanoCastRefusal refusal = NanoCastRules.Evaluate(
                BuildAttempt(caster, spell, recipient, nanoCost, nowUtc));

            if (refusal != NanoCastRefusal.None)
            {
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

        static void Complete(Character caster, PendingNanoCast cast, DateTime nowUtc)
        {
            NanoSpell spell = cast.Spell;
            Character? recipient = ResolveTarget(caster, cast.Target);

            // Everything the up-front gate checked can have changed while the bar ran.
            NanoCastRefusal refusal = NanoCastRules.Evaluate(
                BuildAttempt(caster, spell, recipient, cast.NanoCost, nowUtc));
            if (refusal != NanoCastRefusal.None)
            {
                Refuse(caster, NanoCastRules.Describe(refusal));
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
                //TODO: Instant nano effects (heals, damage, teleports) once nano functions land.
                return;
            }

            BuffApplyDecision decision = recipient!.TryApplyBuff(
                spell,
                caster.Identity,
                nowUtc,
                out Buff? applied,
                out Buff? replaced);

            if (replaced != null)
                AnnounceBuffRemoved(recipient, replaced);

            if (applied == null)
            {
                Refuse(caster, DescribeApplyRefusal(decision));
                return;
            }

            AnnounceBuffAdded(recipient, applied);
            SendNanoDuration(caster, recipient, applied);
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

        /// <summary>NCU icons are client-local, so buff add/remove goes to the owner only.</summary>
        static void AnnounceBuffAdded(Character owner, Buff buff)
            => SessionOf(owner)?.Send(
                new BuffMessage
                {
                    Identity = owner.Identity,
                    Action = 0,
                    // Live add uses the owner instance as the identity type; remove uses NanoProgram.
                    NanoProgram = new Identity
                    {
                        Type = (IdentityType)owner.Identity.Instance,
                        Instance = buff.Id
                    }
                });

        static void AnnounceBuffRemoved(Character owner, Buff buff)
            => SessionOf(owner)?.Send(
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
