namespace ZoneEngine_New.Core.Helpers
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Bits from <see cref="FunctionType.ChangeActionRestriction"/>.
    /// 214879 sets <see cref="PvPEnabled"/>; 202732 sets <see cref="PvPEnabled_Tower"/>.
    /// </summary>
    [Flags]
    public enum ActionRestrictionFlags
    {
        None = 0,

        PvPEnabled = 1,

        PvPEnabled_Tower = 67108864,
    }

    /// <summary>Whether one character may engage another.</summary>
    public static class CombatRules
    {
        const ActionRestrictionFlags PlayerAttackable =
            ActionRestrictionFlags.PvPEnabled | ActionRestrictionFlags.PvPEnabled_Tower;

        public static bool CanAttack(Character attacker, Character target)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            ArgumentNullException.ThrowIfNull(target);

            if (attacker.IsDead || target.IsDead || ReferenceEquals(attacker, target))
                return false;

            if (IsShieldedByGas(target) || IsInRestrictedGas(attacker))
                return false;

            // A NoCombat NPC does not attack, and neither does anyone whose fighting a buff restricts.
            if (attacker is NpcCharacter { Attackable: false } || attacker.CombatRestricted)
                return false;

            // A pet and its owner (and the owner's other pets) are one side.
            Character attackerSide = attacker is NpcCharacter { PetOwner: Character attackerOwner } ? attackerOwner : attacker;
            Character targetSide = target is NpcCharacter { PetOwner: Character targetOwner } ? targetOwner : target;
            if (ReferenceEquals(attackerSide, targetSide))
                return false;

            // A player's pet only fights players (or their pets) its owner could fight.
            if (attackerSide is Player && !ReferenceEquals(attackerSide, attacker))
                return CanAttack(attackerSide, targetSide) || (targetSide is not Player && CanAttackNonPlayer(target));

            // A player's pet is attacked under the same rules as its owner (PvP flags for players, mobs always).
            if (targetSide is Player && !ReferenceEquals(targetSide, target))
                return CanAttack(attacker, targetSide);

            if (target is Player player)
            {
                // A GM hidden with .visibility off is ignored by NPCs.
                if (!attacker.IsPlayer && player.HiddenFromPlayers)
                    return false;

                // Nor do they attack a player still zoning in.
                if (!attacker.IsPlayer && player.InZoneInGrace(DateTime.UtcNow))
                    return false;

                // Mobs can still hit players. Only another player needs the gas or the PvP flags, and the level range.
                if (!attacker.IsPlayer)
                    return true;

                // Team and raid members never hit each other, whatever the gas or PvP flags. A raid is one team; pets
                // reach here through their owners above.
                if (attacker is Player attackingPlayer && AreTeammates(attackingPlayer, player))
                    return false;

                return IsPlayerVulnerable(attacker, player) && PvpLevelRange.Allows(attacker, player);
            }

            return target is not NpcCharacter npc || npc.Attackable;
        }

        static bool CanAttackNonPlayer(Character target) => target is not NpcCharacter npc || npc.Attackable;

        /// <summary>Same team or raid (<see cref="Teams.TeamService.AreTeammates"/>); false off a playfield.</summary>
        static bool AreTeammates(Player first, Player second)
            => (first.Playfield ?? second.Playfield)?.GetService<Teams.TeamService>()?.AreTeammates(first, second) == true;

        /// <summary>A player other players may attack: a running buff sets PvPEnabled or PvPEnabled_Tower.</summary>
        public static bool IsPvpEnabled(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            return (player.ActionRestrictionFlags & PlayerAttackable) != 0;
        }

        /// <summary>
        /// A blue-named NPC (Flags stat bit <see cref="SmokeLounge.AOtomation.Messaging.GameData.CharacterFlags.HasBlueName"/>):
        /// the friendly, non-combat NPCs (receptionists, recruiters, vendors, quest givers). They are not attackable.
        /// </summary>
        public static bool IsBlueNameNpc(int flags)
            => !Entities.StatCollection.IsUnset(flags)
               && (flags & (int)SmokeLounge.AOtomation.Messaging.GameData.CharacterFlags.HasBlueName) != 0;

        /// <summary>
        /// True when the only reason <see cref="CanAttack"/> fails is that the target player is not PvP-flagged.
        /// </summary>
        public static bool IsPvpAttackBlocked(Character attacker, Character target)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            ArgumentNullException.ThrowIfNull(target);

            if (!attacker.IsPlayer || attacker.IsDead || target.IsDead || ReferenceEquals(attacker, target))
                return false;
            if (IsShieldedByGas(target) || IsInRestrictedGas(attacker))
                return false;

            // A player's pet is blocked exactly when its owner is.
            if (target is NpcCharacter { PetOwner: Player petOwner })
            {
                if (ReferenceEquals(petOwner, attacker))
                    return false;
                target = petOwner;
            }

            return target is Player player && !IsPlayerVulnerable(attacker, player);
        }

        /// <summary>100% suppression gas (<see cref="FightMode.NoFight"/>) where the character stands: no fighting.</summary>
        public static bool IsInRestrictedGas(Character character)
        {
            ArgumentNullException.ThrowIfNull(character);
            return FightModeOf(character) == FightMode.NoFight;
        }

        /// <summary>
        /// 100% gas protects the target, except a player still in its disgrace: it keeps the PvP gas it left until the
        /// disgrace ends.
        /// </summary>
        static bool IsShieldedByGas(Character target)
            => IsInRestrictedGas(target) && target is not Player { SuppressionGas.InDisgrace: true };

        /// <summary>The suppression gas where <paramref name="character"/> stands; 75% off a playfield.</summary>
        public static FightMode FightModeOf(Character character)
        {
            ArgumentNullException.ThrowIfNull(character);
            return character.Playfield?.FightModeOf(character) ?? FightMode.Normal;
        }

        /// <summary>
        /// Whether a player may attack <paramref name="target"/>, as the client decides it from the target's gas
        /// (Gamecode.dll N3Msg_HasVulnerableFightMode, 0x10017052): 25% another side, 5% anyone outside the target's
        /// organization, 0% anyone. During a grace or disgrace the target is judged by the gas it came from
        /// (<see cref="SuppressionGasPeriod"/>). A PvP-flagged target can be attacked in any gas that allows fighting.
        /// </summary>
        static bool IsPlayerVulnerable(Character attacker, Player target)
        {
            if ((target.ActionRestrictionFlags & PlayerAttackable) != 0)
                return true;

            switch (target.SuppressionGas.Effective(FightModeOf(target)))
            {
                case FightMode.Side:
                    return attacker.Stats.GetOrZero(CharacterStat.Side) != target.Stats.GetOrZero(CharacterStat.Side);
                case FightMode.Organization:
                    int clan = target.Stats.GetOrZero(CharacterStat.Clan);
                    return clan == 0 || attacker.Stats.GetOrZero(CharacterStat.Clan) != clan;
                case FightMode.All:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Flags granted by active buffs. Arg1 of 0 means the bits last as long as the buff.
        /// </summary>
        public static ActionRestrictionFlags CollectActionRestrictions(IReadOnlyList<Buff> buffs, StatCollection stats)
        {
            ArgumentNullException.ThrowIfNull(buffs);
            ArgumentNullException.ThrowIfNull(stats);

            ActionRestrictionFlags flags = ActionRestrictionFlags.None;
            for (int i = 0; i < buffs.Count; i++)
            {
                IReadOnlyList<ItemSpell> spells = buffs[i].ModifierSpells;
                for (int spellIndex = 0; spellIndex < spells.Count; spellIndex++)
                {
                    ItemSpell spell = spells[spellIndex];
                    if (!spell.Is(FunctionType.ChangeActionRestriction))
                        continue;
                    if (!spell.MeetsRequirements(stats))
                        continue;
                    if (!spell.TryReadInt(0, out int bits))
                        continue;
                    if (spell.TryReadInt(1, out int mode) && mode != 0)
                        continue;

                    flags |= (ActionRestrictionFlags)bits;
                }
            }

            return flags;
        }
    }
}
