namespace ZoneEngine_New.Core.Entities
{
    using System;

    using ZoneEngine_New.Core.Inventory;

    using SmokeLounge.AOtomation.Messaging.GameData;

    public enum WeaponSlot : byte
    {
        None = 0,
        MainHand = 1,
        OffHand = 2,
        CombinedMA = 3
    }

    public enum WeaponState
    {
        Attacking,
        Recharging
    }

    /// <summary>
    /// Per-slot auto-attack clock. Base delays come from the armed <see cref="Item"/>;
    /// effective speeds are refreshed from the wielder's AggDef.
    /// </summary>
    public sealed class CharacterWeapon
    {
        public const double DefaultAttackSpeedSeconds = 1.0;

        public const double DefaultRechargeSpeedSeconds = 1.0;

        const double MinCycleSeconds = 1.0;

        double _timer;

        public Item? Item { get; set; }

        public double BaseAttackSpeed { get; private set; } = DefaultAttackSpeedSeconds;

        public double BaseRechargeSpeed { get; private set; } = DefaultRechargeSpeedSeconds;

        /// <summary>Effective attack phase length after AggDef (and later initiative).</summary>
        public double AttackSpeed { get; private set; } = DefaultAttackSpeedSeconds;

        /// <summary>Effective recharge phase length after AggDef (and later initiative).</summary>
        public double RechargeSpeed { get; private set; } = DefaultRechargeSpeedSeconds;

        public WeaponState State { get; set; } = WeaponState.Attacking;

        public Character? Wielder { get; set; }

        public event Action? Attacked;

        public void Tick(double deltaTime)
        {
            if (deltaTime <= 0.0)
                return;

            _timer += deltaTime;

            if (State == WeaponState.Attacking && _timer >= AttackSpeed)
            {
                _timer = 0.0;
                Attacked?.Invoke();
                State = WeaponState.Recharging;
            }

            if (State == WeaponState.Recharging && _timer >= RechargeSpeed)
            {
                _timer = 0.0;
                State = WeaponState.Attacking;
            }
        }

        public void ResetAttack()
        {
            _timer = 0.0;
            State = WeaponState.Attacking;
        }

        public void ConfigureBaseSpeeds(double attackSpeedSeconds, double rechargeSpeedSeconds)
        {
            BaseAttackSpeed = attackSpeedSeconds > 0.0
                ? attackSpeedSeconds
                : DefaultAttackSpeedSeconds;
            BaseRechargeSpeed = rechargeSpeedSeconds > 0.0
                ? rechargeSpeedSeconds
                : DefaultRechargeSpeedSeconds;
            RefreshEffectiveSpeeds();
        }

        /// <summary>
        /// Recompute effective cycle from base delays and the wielder's current AggDef.
        /// </summary>
        public void RefreshEffectiveSpeeds()
        {
            double skewSeconds = 0.0;
            if (Wielder != null)
            {
                int aggDef = Wielder.Stats.Get(CharacterStat.AggDef);
                if (!StatCollection.IsUnset(aggDef))
                {
                    aggDef = Math.Clamp(aggDef, -100, 100);
                    skewSeconds = (aggDef - 75) / 100.0;
                }
            }

            // TODO: Initiative reduction — AdjustedAttack = Base - Init/600 - AggDefSkew;
            // AdjustedRecharge = Base - Init/300 - AggDefSkew; then floor at weapon/1s cap.
            // Apply live from the weapon's initiative skill (Melee/Ranged/Physical) like AggDef.

            AttackSpeed = Math.Max(MinCycleSeconds, BaseAttackSpeed - skewSeconds);
            RechargeSpeed = Math.Max(MinCycleSeconds, BaseRechargeSpeed - skewSeconds);
        }
    }
}
