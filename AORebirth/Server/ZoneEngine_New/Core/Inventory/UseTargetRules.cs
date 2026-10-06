namespace ZoneEngine_New.Core.Inventory
{
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Who an item or perk action is used on, from its Can flags:
    /// <list type="bullet">
    /// <item><see cref="CanFlags.ApplyOnFightingTarget"/>: only the character the user is fighting right now.</item>
    /// <item><see cref="CanFlags.ApplyOnHostile"/>: any combat-enabled character the user may attack (selected, else
    /// the fighting target), without having to be fighting it; using it does not start auto-attack.</item>
    /// </list>
    /// Templates with neither flag keep their own targeting.
    /// </summary>
    internal static class UseTargetRules
    {
        /// <summary>
        /// Why a delayed use was dropped when it lost its fighting target (attack stopped, target changed); answered with
        /// <see cref="ClientFeedback.RequiresFightingTarget"/> instead of the generic failure line.
        /// </summary>
        public const string LostFightingTarget = "no longer the fighting target";

        public enum Failure
        {
            None,
            NoFightingTarget,
            NoHostileTarget,
            NotAttackable
        }

        public static CanFlags CanOf(ItemTemplate template)
            => template.Stats.TryGetValue(CharacterStat.Can, out int value) ? (CanFlags)(uint)value : 0;

        public static bool NeedsFightingTarget(CanFlags can) => (can & CanFlags.ApplyOnFightingTarget) != 0;

        public static bool AppliesToHostile(CanFlags can) => !NeedsFightingTarget(can) && (can & CanFlags.ApplyOnHostile) != 0;

        /// <summary>True when <paramref name="can"/> aims the use at a hostile (either rule above).</summary>
        public static bool IsHostileTargeted(CanFlags can) => NeedsFightingTarget(can) || AppliesToHostile(can);

        /// <summary>
        /// Resolves the target of a hostile-targeted use. False with <paramref name="failure"/> when there is no valid
        /// target; <paramref name="target"/> is then the user.
        /// </summary>
        public static bool TryResolve(Player user, CanFlags can, out Character target, out Failure failure)
        {
            target = user;
            failure = Failure.None;

            Character? chosen;
            if (NeedsFightingTarget(can))
            {
                chosen = user.TryResolveFightingTarget();
                if (chosen == null)
                {
                    failure = Failure.NoFightingTarget;
                    return false;
                }
            }
            else
            {
                chosen = SelectedCharacter(user) ?? user.TryResolveFightingTarget();
                if (chosen == null)
                {
                    failure = Failure.NoHostileTarget;
                    return false;
                }
            }

            if (!CombatRules.CanAttack(user, chosen))
            {
                failure = Failure.NotAttackable;
                return false;
            }

            target = chosen;
            return true;
        }

        /// <summary>
        /// Rechecks a resolved target when a delayed use finishes: still here and attackable, and for a fighting-target use
        /// still the one being fought. Null when still valid.
        /// </summary>
        public static Failure? Revalidate(Player user, CanFlags can, Character target)
        {
            if (ReferenceEquals(target, user) || !IsHostileTargeted(can))
                return null;
            if (target.IsDead || !ReferenceEquals(target.Playfield, user.Playfield))
                return Failure.NoHostileTarget;
            if (NeedsFightingTarget(can) && !ReferenceEquals(user.TryResolveFightingTarget(), target))
                return Failure.NoFightingTarget;
            if (!CombatRules.CanAttack(user, target))
                return Failure.NotAttackable;
            return null;
        }

        public static void SendFailure(Player user, Failure failure, Character? target)
        {
            switch (failure)
            {
                case Failure.NoFightingTarget:
                    ClientFeedback.Send(user, ClientFeedback.RequiresFightingTarget);
                    break;
                case Failure.NoHostileTarget:
                    RequirementFeedback.SendText(user, "You need a hostile target to use this.");
                    break;
                case Failure.NotAttackable:
                    ClientFeedback.Send(user, target != null && CombatRules.IsPvpAttackBlocked(user, target)
                        ? "Feedback_PvpNotAllowedInThisDistrict"
                        : "Feedback_StartingAttackFailed");
                    break;
            }
        }

        /// <summary>The user's selected living character other than themselves, or null.</summary>
        static Character? SelectedCharacter(Player user)
        {
            Identity selected = user.Target;
            if (selected.Instance == 0 || selected == user.Identity || user.Playfield == null)
                return null;

            DynelRegistry registry = user.Playfield.GetRequiredService<DynelRegistry>();
            if (!registry.TryGet(selected, out Dynel? dynel)
                && !registry.TryGet(new Identity { Type = IdentityType.CanbeAffected, Instance = selected.Instance }, out dynel))
                return null;

            return dynel is Character character && !character.IsDead ? character : null;
        }
    }
}
