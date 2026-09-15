namespace AORebirth.Core.Entities
{
    using System;

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
    /// Per-slot auto-attack clock (Lost Eden Weapon timing model).
    /// </summary>
    public sealed class CharacterWeapon
    {
        public const double DefaultAttackSpeedSeconds = 1.0;

        public const double DefaultRechargeSpeedSeconds = 1.0;

        private double timer;

        public double AttackSpeed { get; set; }

        public double RechargeSpeed { get; set; }

        public WeaponState State { get; set; }

        public Character Wielder { get; set; }

        public event Action Attacked;

        public CharacterWeapon()
        {
            this.AttackSpeed = DefaultAttackSpeedSeconds;
            this.RechargeSpeed = DefaultRechargeSpeedSeconds;
            this.State = WeaponState.Attacking;
        }

        public void Tick(double deltaTime)
        {
            if (deltaTime <= 0.0)
            {
                return;
            }

            this.timer += deltaTime;

            if (this.State == WeaponState.Attacking && this.timer >= this.AttackSpeed)
            {
                this.timer = 0.0;
                Action attacked = this.Attacked;
                if (attacked != null)
                {
                    attacked();
                }

                this.State = WeaponState.Recharging;
            }

            if (this.State == WeaponState.Recharging && this.timer >= this.RechargeSpeed)
            {
                this.timer = 0.0;
                this.State = WeaponState.Attacking;
            }
        }

        public void ResetAttack()
        {
            this.timer = 0.0;
            this.State = WeaponState.Attacking;
        }

        public void ConfigureSpeeds(double attackSpeedSeconds, double rechargeSpeedSeconds)
        {
            this.AttackSpeed = attackSpeedSeconds > 0.0
                                   ? attackSpeedSeconds
                                   : DefaultAttackSpeedSeconds;
            this.RechargeSpeed = rechargeSpeedSeconds > 0.0
                                     ? rechargeSpeedSeconds
                                     : DefaultRechargeSpeedSeconds;
        }
    }
}
