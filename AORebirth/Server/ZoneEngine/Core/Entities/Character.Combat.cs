namespace AORebirth.Core.Entities
{
    #region Usings ...

    using System;

    using AORebirth.Core.Combat;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using Utility;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    public partial class Character
    {
        public event EventHandler<CharacterDamagedEventArgs> Damaged;

        public event EventHandler<CharacterDeathEventArgs> Died;

        private bool deathEventRaised;

        public void ProcessWeaponSwing(WeaponSlot slot)
        {
            ICharacter target = this.TryResolveFightingTarget();
            if (target == null)
            {
                return;
            }

            CombatStrikeContext context = CharacterCombatStrikeBuilder.Build(this, slot);
            if (context == null)
            {
                return;
            }

            this.Strike(target, context);
        }

        public CharacterSpecialAttackResult ProcessSpecialAttack(ICharacter target, StatIds specialStatId)
        {
            var result = new CharacterSpecialAttackResult
                         {
                             SpecialStatId = (int)specialStatId
                         };

            if (target == null || !CharacterSpecialAttackRules.IsSupportedSpecial((int)specialStatId))
            {
                result.Outcome = StrikeOutcome.RejectedInvalidTarget;
                return result;
            }

            CombatStrikeContext context = CharacterCombatStrikeBuilder.Build(this, WeaponSlot.MainHand);
            if (context == null)
            {
                result.Outcome = StrikeOutcome.RejectedNoWeapon;
                return result;
            }

            context.SpecialAttackStat = specialStatId;
            context.DamageSource = CombatDamageSource.Special;
            result.AmmoCount = context.AttackInfoAmmoCount;
            result.EquipSlot = context.AttackInfoWeaponSlot;

            int hitCount = CharacterSpecialAttackRules.ResolveHitCount((int)specialStatId);
            int damageScale = CharacterSpecialAttackRules.ResolveDamageScale((int)specialStatId);
            if (specialStatId == StatIds.dimach)
            {
                context.OutgoingDamageScale = damageScale;
                hitCount = 1;
            }

            int totalDamage = 0;
            StrikeOutcome lastOutcome = StrikeOutcome.RejectedInvalidTarget;
            CombatStrikeContext strikeContext = context;
            // if (specialStatId == StatIds.burst)
            // {
            //     strikeContext = CloneContext(context);
            //     strikeContext.SpecialAttackStat = null;
            // }

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                CombatStrikeResult strikeResult = this.Strike(target, strikeContext);
                lastOutcome = strikeResult.Outcome;
                if (strikeResult.Outcome != StrikeOutcome.Applied)
                {
                    result.Outcome = strikeResult.Outcome;
                    result.Damage = totalDamage;
                    return result;
                }

                totalDamage += strikeResult.Damage;
            }

            result.Damage = Math.Max(1, totalDamage);
            result.Outcome = lastOutcome;
            return result;
        }

        private static CombatStrikeContext CloneContext(CombatStrikeContext source)
        {
            return new CombatStrikeContext
                   {
                       MinDamage = source.MinDamage,
                       MaxDamage = source.MaxDamage,
                       DamageBonus = source.DamageBonus,
                       Range = source.Range,
                       UsesEquippedWeapon = source.UsesEquippedWeapon,
                       AttackInfoAmmoCount = source.AttackInfoAmmoCount,
                       AttackInfoWeaponSlot = source.AttackInfoWeaponSlot,
                       AttackInfoHitType = source.AttackInfoHitType,
                       AttackInfoWeaponInstance = source.AttackInfoWeaponInstance,
                       AttackInfoUnknown = source.AttackInfoUnknown,
                       AttackInfoN3Unknown = source.AttackInfoN3Unknown,
                       LethalAttackInfoUnknown = source.LethalAttackInfoUnknown,
                       PreserveAttackInfoWireValues = source.PreserveAttackInfoWireValues,
                       FixedDamage = source.FixedDamage,
                       DamageSource = source.DamageSource,
                       WeaponSlot = source.WeaponSlot,
                       WeaponLowId = source.WeaponLowId,
                       WeaponHighId = source.WeaponHighId,
                       WeaponQualityLevel = source.WeaponQualityLevel,
                       RawDamageType = source.RawDamageType,
                       AttackSkillDefinitions = source.AttackSkillDefinitions,
                       AttackSkillValues = source.AttackSkillValues,
                       EffectiveAttackRating = source.EffectiveAttackRating,
                       AddAllOff = source.AddAllOff,
                       SendAttackInfo = source.SendAttackInfo,
                       SpecialAttackStat = source.SpecialAttackStat,
                       OutgoingDamageScale = source.OutgoingDamageScale
                   };
        }

        public CombatStrikeResult Strike(ICharacter target, CombatStrikeContext context)
        {
            if (target == null || context == null)
            {
                return this.LogAndReturnStrike(
                    target,
                    context,
                    new CombatStrikeResult { Outcome = StrikeOutcome.RejectedInvalidTarget });
            }

            bool hasFixedDamage = context.FixedDamage.HasValue && context.FixedDamage.Value > 0;
            bool hasEquippedWeapon = context.UsesEquippedWeapon && context.WeaponLowId > 0;
            bool hasUnarmedDamage = !context.UsesEquippedWeapon && context.MinDamage > 0;
            if (!hasFixedDamage && !hasEquippedWeapon && !hasUnarmedDamage)
            {
                return this.LogAndReturnStrike(
                    target,
                    context,
                    new CombatStrikeResult { Outcome = StrikeOutcome.RejectedNoWeapon });
            }

            if (!this.CanStrikeTarget(target))
            {
                return this.LogAndReturnStrike(
                    target,
                    context,
                    new CombatStrikeResult { Outcome = StrikeOutcome.RejectedInvalidTarget });
            }

            double distance = this.CalculatePredictedPosition().Distance3D(
                new AORebirth.Core.Vector.Coordinate(target.Position));
            switch (CharacterCombatRangeRules.EvaluatePlayerRange(distance, context.Range))
            {
                case PlayerCombatRangeDecision.HardCancel:
                    if (this.IsPlayerCombatant())
                    {
                        this.SetFightingTarget(Identity.None);
                        this.ClearWeapons();
                        return this.LogAndReturnStrike(
                            target,
                            context,
                            new CombatStrikeResult { Outcome = StrikeOutcome.RejectedHardRange });
                    }

                    return this.LogAndReturnStrike(
                        target,
                        context,
                        new CombatStrikeResult { Outcome = StrikeOutcome.SkippedSoftRange });
                case PlayerCombatRangeDecision.SoftSkip:
                    return this.LogAndReturnStrike(
                        target,
                        context,
                        new CombatStrikeResult { Outcome = StrikeOutcome.SkippedSoftRange });
            }

            Character targetCharacter = target as Character;
            Character attackerCharacter = this;
            if (targetCharacter == null)
            {
                return this.LogAndReturnStrike(
                    target,
                    context,
                    new CombatStrikeResult { Outcome = StrikeOutcome.RejectedInvalidTarget });
            }

            CombatStrikeDamageResult damageResult =
                CombatStrikeDamageCalculator.Calculate(attackerCharacter, targetCharacter, context);
            context.EffectiveAttackRating = damageResult.CappedAttackRating;

            if (!damageResult.IsHit)
            {
                this.AnnounceMissedStrike(target, context);
                return this.LogAndReturnStrike(
                    target,
                    context,
                    new CombatStrikeResult
                    {
                        Outcome = StrikeOutcome.Missed,
                        IsHit = false,
                        Damage = 0
                    });
            }

            int damage = damageResult.Damage;
            int previousHealth = target.Stats[StatIds.health].Value;
            int newHealth = Math.Max(0, previousHealth - damage);
            bool killingHit = newHealth == 0;

            targetCharacter.ReceiveStrike(
                this,
                context,
                damage,
                previousHealth,
                newHealth,
                killingHit,
                damageResult.HitType);
            this.AnnounceStrike(target, context, damageResult, killingHit);

            return this.LogAndReturnStrike(
                target,
                context,
                new CombatStrikeResult
                {
                    Outcome = StrikeOutcome.Applied,
                    Damage = damage,
                    PreviousHealth = previousHealth,
                    NewHealth = newHealth,
                    KillingHit = killingHit,
                    IsHit = true,
                    HitType = damageResult.HitType
                });
        }

        private CombatStrikeResult LogAndReturnStrike(
            ICharacter target,
            CombatStrikeContext context,
            CombatStrikeResult result)
        {
            LogUtil.Debug(
                DebugInfoDetail.Combat,
                CombatStrikeDebugFormatter.Format(this, target, context, result));
            return result;
        }

        internal void ReceiveStrike(
            ICharacter attacker,
            CombatStrikeContext context,
            int damage,
            int previousHealth,
            int newHealth,
            bool killingHit,
            HitType hitType)
        {
            this.Stats[StatIds.health].Value = newHealth;

            EventHandler<CharacterDamagedEventArgs> damaged = this.Damaged;
            if (damaged != null)
            {
                damaged(
                    this,
                    new CharacterDamagedEventArgs
                    {
                        Attacker = attacker,
                        Target = this,
                        Context = context,
                        Damage = damage,
                        PreviousHealth = previousHealth,
                        NewHealth = newHealth,
                        KillingHit = killingHit,
                        HitType = hitType
                    });
            }

            if (killingHit && !this.deathEventRaised)
            {
                this.deathEventRaised = true;
                EventHandler<CharacterDeathEventArgs> died = this.Died;
                if (died != null)
                {
                    died(
                        this,
                        new CharacterDeathEventArgs
                        {
                            Victim = this,
                            Killer = attacker,
                            Cause = CharacterDeathCause.Combat,
                            Context = context
                        });
                }
            }
        }

        internal void ForceDeath(CharacterDeathEventArgs args)
        {
            if (args == null || this.deathEventRaised)
            {
                return;
            }

            this.deathEventRaised = true;
            EventHandler<CharacterDeathEventArgs> died = this.Died;
            if (died != null)
            {
                died(this, args);
            }
        }

        private ICharacter TryResolveFightingTarget()
        {
            if (this.FightingTarget.Instance == 0)
            {
                return null;
            }

            return Pool.Instance.GetObject<ICharacter>(this.Playfield.Identity, this.FightingTarget);
        }

        private bool CanStrikeTarget(ICharacter target)
        {
            if (target == null
                || target.Identity.Instance == this.Identity.Instance
                || target.Stats[StatIds.health].Value <= 0
                || this.Playfield == null
                || !target.InPlayfield(this.Playfield.Identity))
            {
                return false;
            }

            if (this.IsPlayerCombatant() && CharacterEngageRules.IsPlayerCombatant(target))
            {
                return CharacterEngageRules.CanEngagePlayerVersusPlayer(this, target);
            }

            return true;
        }

        private void AnnounceMissedStrike(ICharacter target, CombatStrikeContext context)
        {
            if (!context.SendAttackInfo || this.Playfield == null)
            {
                return;
            }

            this.Playfield.Announce(
                new MissedAttackInfoMessage
                {
                    Identity = this.Identity,
                    Unknown1 = -1,
                    Unknown2 = context.AttackInfoWeaponSlot,
                    Unknown3 = this.Identity,
                    Unknown4 = target.Identity,
                    Unknown5 = 0
                });
        }

        private void AnnounceStrike(
            ICharacter target,
            CombatStrikeContext context,
            CombatStrikeDamageResult damageResult,
            bool killingHit)
        {
            if (damageResult == null || !context.SendAttackInfo || this.Playfield == null)
            {
                return;
            }

            this.Playfield.Announce(
                new AttackInfoMessage
                {
                    Identity = this.Identity,
                    Unknown = context.AttackInfoN3Unknown,
                    Target = target.Identity,
                    Unknown1 = damageResult.Damage,
                    Unknown2 = context.AttackInfoAmmoCount,
                    Unknown3 = context.AttackInfoWeaponSlot,
                    Unknown4 = killingHit && context.LethalAttackInfoUnknown.HasValue
                                   ? context.LethalAttackInfoUnknown.Value
                                   : (context.PreserveAttackInfoWireValues
                                          ? context.AttackInfoUnknown
                                          : (killingHit ? 4 : context.AttackInfoUnknown)),
                    Unknown5 = context.PreserveAttackInfoWireValues
                                   ? context.AttackInfoHitType
                                   : (int)damageResult.HitType,
                    Unknown6 = context.AttackInfoWeaponInstance
                });
        }

        private bool IsPlayerCombatant()
        {
            return this.Controller != null && this.Controller.Client != null;
        }
    }
}
