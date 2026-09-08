namespace AORebirth.Enums
{
    using System;

    [Flags]
    public enum WeaponFlags
    {
        None = 0,
        Unarmed = 1 << 0,
        Melee = 1 << 1,
        Ranged = 1 << 2,
        OneHanded = 1 << 3,
        TwoHanded = 1 << 4,
    }
}
