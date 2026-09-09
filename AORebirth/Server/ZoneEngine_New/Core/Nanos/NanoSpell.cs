namespace ZoneEngine_New.Core.Nanos
{
    using System;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// A nano program read out of an <see cref="ItemTemplate"/>.
    /// Item attribute ids 54 and 75 carry NCU cost and strain on nano templates, so the
    /// <see cref="CharacterStat"/> names for those ids (Level, StrainOmniTokens) do not
    /// describe what the value means here; the constants below are the readable spelling.
    /// </summary>
    public class NanoSpell : ItemTemplate
    {
        public const CharacterStat NcuCostStat = (CharacterStat)54;

        public const CharacterStat NanoStrainStat = (CharacterStat)75;

        protected NanoSpell(ItemTemplate template)
            : base(template)
        {
            NanoStrain = ReadStat(NanoStrainStat);
            StackingOrder = ReadStat(CharacterStat.StackingOrder);
            NcuCost = Math.Max(0, ReadStat(NcuCostStat));
            NanoPointCost = Math.Max(0, ReadStat(CharacterStat.NanoPoints));
            DurationCentiseconds = Math.Max(0, ReadStat(CharacterStat.TimeExist));
            AttackDelayCentiseconds = Math.Max(0, ReadStat(CharacterStat.AttackDelay));
            AttackDelayCapCentiseconds = Math.Max(0, ReadStat(CharacterStat.AttackDelayCap));
            RechargeDelayCentiseconds = Math.Max(0, ReadStat(CharacterStat.RechargeDelay));
            RechargeDelayCapCentiseconds = Math.Max(0, ReadStat(CharacterStat.RechargeDelayCap));

            CanFlags can = (CanFlags)(uint)ReadStat(CharacterStat.Can);
            IsHostile = (can & CanFlags.ApplyOnHostile) != 0
                && (can & (CanFlags.ApplyOnFriendly | CanFlags.ApplyOnSelf)) == 0;

            // A nano that occupies NCU for a while is a buff; instant nanos just fire and end.
            IsBuff = template.IsBuff || DurationCentiseconds > 0;
            CanCancel = template.CanCancel && !IsHostile;
        }

        /// <summary>Nano strain; nanos of one strain never stack. 0 means strainless.</summary>
        public int NanoStrain { get; }

        /// <summary>Higher wins within a strain.</summary>
        public int StackingOrder { get; }

        public int NcuCost { get; }

        /// <summary>Base nano point cost before <see cref="CharacterStat.NPCostModifier"/>.</summary>
        public int NanoPointCost { get; }

        /// <summary>Buff lifetime; 0 for instant nanos.</summary>
        public int DurationCentiseconds { get; }

        public int AttackDelayCentiseconds { get; }

        public int AttackDelayCapCentiseconds { get; }

        public int RechargeDelayCentiseconds { get; }

        public int RechargeDelayCapCentiseconds { get; }

        /// <summary>True for nanos that only land on enemies (debuffs); these bypass NCU.</summary>
        public bool IsHostile { get; }

        public static NanoSpell From(ItemTemplate template)
        {
            ArgumentNullException.ThrowIfNull(template);
            return template as NanoSpell ?? new NanoSpell(template);
        }

        /// <summary>Template stats may hold the Unset sentinel; read them as 0.</summary>
        protected int ReadStat(CharacterStat stat)
            => Stats.TryGetValue(stat, out int value) ? StatCollection.Normalize(value) : 0;
    }
}
