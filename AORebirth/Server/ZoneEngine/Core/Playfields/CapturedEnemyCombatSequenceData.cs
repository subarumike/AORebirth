namespace AORebirth.Core.Playfields
{
    using System;
    using System.Linq;

    internal sealed class CapturedEnemyCombatAttackDefinition
    {
        internal CapturedEnemyCombatAttackDefinition(
            int minDamage,
            int maxDamage,
            int damageBonus,
            double range,
            double rechargeSeconds,
            bool usesEquippedWeapon,
            int attackInfoAmmoCount,
            int attackInfoWeaponSlot,
            int attackInfoUnknown,
            int attackInfoHitType,
            int attackInfoWeaponInstance,
            byte attackInfoN3Unknown,
            bool sendAttackInfo,
            int[] capturedDamageObservations = null,
            int? lethalAttackInfoUnknown = null)
        {
            this.MinDamage = minDamage;
            this.MaxDamage = maxDamage;
            this.DamageBonus = damageBonus;
            this.Range = range;
            this.RechargeSeconds = rechargeSeconds;
            this.UsesEquippedWeapon = usesEquippedWeapon;
            this.AttackInfoAmmoCount = attackInfoAmmoCount;
            this.AttackInfoWeaponSlot = attackInfoWeaponSlot;
            this.AttackInfoUnknown = attackInfoUnknown;
            this.AttackInfoHitType = attackInfoHitType;
            this.AttackInfoWeaponInstance = attackInfoWeaponInstance;
            this.AttackInfoN3Unknown = attackInfoN3Unknown;
            this.SendAttackInfo = sendAttackInfo;
            this.CapturedDamageObservations = capturedDamageObservations == null
                                                  ? new int[0]
                                                  : capturedDamageObservations.ToArray();
            this.LethalAttackInfoUnknown = lethalAttackInfoUnknown;
        }

        internal int MinDamage { get; private set; }

        internal int MaxDamage { get; private set; }

        internal int DamageBonus { get; private set; }

        internal double Range { get; private set; }

        internal double RechargeSeconds { get; private set; }

        internal bool UsesEquippedWeapon { get; private set; }

        internal int AttackInfoAmmoCount { get; private set; }

        internal int AttackInfoWeaponSlot { get; private set; }

        internal int AttackInfoUnknown { get; private set; }

        internal int AttackInfoHitType { get; private set; }

        internal int AttackInfoWeaponInstance { get; private set; }

        internal byte AttackInfoN3Unknown { get; private set; }

        internal bool SendAttackInfo { get; private set; }

        internal int[] CapturedDamageObservations { get; private set; }

        internal int? LethalAttackInfoUnknown { get; private set; }

        internal CapturedEnemyCombatAttackDefinition WithCapturedDamageObservations(
            int[] capturedDamageObservations,
            int? lethalAttackInfoUnknown = null)
        {
            return new CapturedEnemyCombatAttackDefinition(
                this.MinDamage,
                this.MaxDamage,
                this.DamageBonus,
                this.Range,
                this.RechargeSeconds,
                this.UsesEquippedWeapon,
                this.AttackInfoAmmoCount,
                this.AttackInfoWeaponSlot,
                this.AttackInfoUnknown,
                this.AttackInfoHitType,
                this.AttackInfoWeaponInstance,
                this.AttackInfoN3Unknown,
                this.SendAttackInfo,
                capturedDamageObservations,
                lethalAttackInfoUnknown ?? this.LethalAttackInfoUnknown);
        }

        internal bool IsValid
        {
            get
            {
                return this.MinDamage > 0
                       && this.MaxDamage >= this.MinDamage
                       && this.RechargeSeconds > 0;
            }
        }

        internal bool IsValidOneShot
        {
            get
            {
                return this.MinDamage > 0
                       && this.MaxDamage >= this.MinDamage
                       && this.RechargeSeconds == 0.0d;
            }
        }
    }

    internal sealed class CapturedEnemySpecialAttackSequenceDefinition
    {
        internal CapturedEnemySpecialAttackSequenceDefinition(
            double initialAttackDelaySeconds,
            CapturedEnemyCombatAttackDefinition openingAttack,
            CapturedEnemyCombatAttackDefinition repeatingAttack,
            CapturedEnemySpecialAttackDefinition[] specialAttacks,
            int specialAttackWeaponUnknown1,
            int specialAttackWeaponUnknown2,
            int specialAttackWeaponUnknown3,
            int specialAttackWeaponUnknown4,
            int specialAttackWeaponUnknown5,
            byte specialAttackWeaponN3Unknown,
            byte attackN3Unknown,
            byte attackAction)
        {
            this.InitialAttackDelaySeconds = initialAttackDelaySeconds;
            this.OpeningAttack = openingAttack;
            this.RepeatingAttack = repeatingAttack;
            this.SpecialAttacks = specialAttacks ?? new CapturedEnemySpecialAttackDefinition[0];
            this.SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1;
            this.SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2;
            this.SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3;
            this.SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4;
            this.SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5;
            this.SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown;
            this.AttackN3Unknown = attackN3Unknown;
            this.AttackAction = attackAction;
        }

        internal double InitialAttackDelaySeconds { get; private set; }

        internal CapturedEnemyCombatAttackDefinition OpeningAttack { get; private set; }

        internal CapturedEnemyCombatAttackDefinition RepeatingAttack { get; private set; }

        internal CapturedEnemySpecialAttackDefinition[] SpecialAttacks { get; private set; }

        internal int SpecialAttackWeaponUnknown1 { get; private set; }

        internal int SpecialAttackWeaponUnknown2 { get; private set; }

        internal int SpecialAttackWeaponUnknown3 { get; private set; }

        internal int SpecialAttackWeaponUnknown4 { get; private set; }

        internal int SpecialAttackWeaponUnknown5 { get; private set; }

        internal byte SpecialAttackWeaponN3Unknown { get; private set; }

        internal byte AttackN3Unknown { get; private set; }

        internal byte AttackAction { get; private set; }

        internal bool IsValid
        {
            get
            {
                return this.InitialAttackDelaySeconds >= 0
                       && (this.OpeningAttack == null || this.OpeningAttack.IsValid)
                       && this.RepeatingAttack != null
                       && this.RepeatingAttack.IsValid;
            }
        }
    }

    internal sealed class CapturedEnemyParallelAttackStreamDefinition
    {
        internal CapturedEnemyParallelAttackStreamDefinition(
            double initialDelaySeconds,
            CapturedEnemyCombatAttackDefinition attack,
            bool repeats = true)
        {
            this.InitialDelaySeconds = initialDelaySeconds;
            this.Attack = attack;
            this.Repeats = repeats;
        }

        internal double InitialDelaySeconds { get; private set; }

        internal CapturedEnemyCombatAttackDefinition Attack { get; private set; }

        internal bool Repeats { get; private set; }

        internal DateTime ResolveNextTickAfterHit(DateTime now)
        {
            return this.Repeats
                       ? now + TimeSpan.FromSeconds(this.Attack.RechargeSeconds)
                       : DateTime.MaxValue;
        }

        internal bool IsValid
        {
            get
            {
                return this.InitialDelaySeconds >= 0
                       && this.Attack != null
                       && (this.Repeats
                               ? this.Attack.IsValid
                               : this.Attack.IsValidOneShot);
            }
        }
    }

    internal sealed class CapturedEnemyParallelAttackSequenceDefinition
    {
        internal CapturedEnemyParallelAttackSequenceDefinition(
            CapturedEnemyParallelAttackStreamDefinition[] streams,
            CapturedEnemySpecialAttackDefinition[] specialAttacks,
            int specialAttackWeaponUnknown1,
            int specialAttackWeaponUnknown2,
            int specialAttackWeaponUnknown3,
            int specialAttackWeaponUnknown4,
            int specialAttackWeaponUnknown5,
            byte specialAttackWeaponN3Unknown,
            byte attackN3Unknown,
            byte attackAction,
            double attackStartDelaySeconds = 0.0d)
        {
            this.Streams = streams ?? new CapturedEnemyParallelAttackStreamDefinition[0];
            this.SpecialAttacks = specialAttacks ?? new CapturedEnemySpecialAttackDefinition[0];
            this.SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1;
            this.SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2;
            this.SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3;
            this.SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4;
            this.SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5;
            this.SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown;
            this.AttackN3Unknown = attackN3Unknown;
            this.AttackAction = attackAction;
            this.AttackStartDelaySeconds = attackStartDelaySeconds;
        }

        internal CapturedEnemyParallelAttackStreamDefinition[] Streams { get; private set; }

        internal CapturedEnemySpecialAttackDefinition[] SpecialAttacks { get; private set; }

        internal int SpecialAttackWeaponUnknown1 { get; private set; }

        internal int SpecialAttackWeaponUnknown2 { get; private set; }

        internal int SpecialAttackWeaponUnknown3 { get; private set; }

        internal int SpecialAttackWeaponUnknown4 { get; private set; }

        internal int SpecialAttackWeaponUnknown5 { get; private set; }

        internal byte SpecialAttackWeaponN3Unknown { get; private set; }

        internal byte AttackN3Unknown { get; private set; }

        internal byte AttackAction { get; private set; }

        internal double AttackStartDelaySeconds { get; private set; }

        internal bool IsValid
        {
            get
            {
                if (this.Streams.Length == 0)
                {
                    return false;
                }

                if (double.IsNaN(this.AttackStartDelaySeconds)
                    || double.IsInfinity(this.AttackStartDelaySeconds)
                    || this.AttackStartDelaySeconds < 0.0d)
                {
                    return false;
                }

                foreach (CapturedEnemyParallelAttackStreamDefinition stream in this.Streams)
                {
                    if (stream == null || !stream.IsValid)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

}
