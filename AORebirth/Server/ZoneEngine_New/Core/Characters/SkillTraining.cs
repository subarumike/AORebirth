namespace ZoneEngine_New.Core.Characters
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// Applies a client SkillMessage: absolute ability and skill bases, paid from the stored IP balance.
    /// </summary>
    public static class SkillTraining
    {
        /// <summary>Six abilities plus the 69 trainable skills.</summary>
        public const int MaxPairs = 75;

        /// <summary>
        /// Pricing bound, not a game cap: reaching it costs far more than level-cap IP, and it keeps
        /// the cost loops from walking an arbitrary client uint.
        /// </summary>
        public const int MaxBase = 10000;

        /// <summary>
        /// All or nothing, and at least one pair must raise something, so a packet that changes nothing
        /// never costs a rebase. Every pair must be a raise of a trainable stat within its level cap and, for
        /// skills, within the cap its abilities allow after this packet's ability raises. The character's
        /// IP must cover the whole packet, which is charged once; otherwise nothing is written.
        /// Bases already above a cap may stay where they are but not rise.
        /// </summary>
        public static bool TryTrain(Player player, IReadOnlyList<GameTuple<CharacterStat, uint>>? pairs, SkillCatalog catalog)
            => TryTrain(player, pairs, catalog, out _);

        /// <inheritdoc cref="TryTrain(Player, IReadOnlyList{GameTuple{CharacterStat, uint}}?, SkillCatalog)"/>
        /// <param name="rejection">Why the packet was refused, with the numbers behind it; null when trained.</param>
        public static bool TryTrain(
            Player player,
            IReadOnlyList<GameTuple<CharacterStat, uint>>? pairs,
            SkillCatalog catalog,
            out string? rejection)
        {
            rejection = null;
            if (pairs is not { Count: > 0 and <= MaxPairs })
                return Reject(out rejection, $"pair count {pairs?.Count ?? 0} outside 1-{MaxPairs}");

            StatCollection stats = player.Stats;
            var breed = (Breed)stats.GetOrZero(CharacterStat.Breed, StatDetail.Base);
            var profession = (Profession)stats.GetOrZero(CharacterStat.Profession, StatDetail.Base);
            if (!catalog.Trains(breed, profession))
                return Reject(out rejection, $"breed {breed} profession {profession} not trainable");

            int level = stats.GetOrZero(CharacterStat.Level, StatDetail.Base);
            var seen = new HashSet<CharacterStat>();
            var abilityRaises = new Dictionary<CharacterStat, int>();
            var skillRaises = new List<(CharacterStat Skill, int Target)>();
            long cost = 0;
            foreach (GameTuple<CharacterStat, uint> pair in pairs)
            {
                if (pair == null)
                    return Reject(out rejection, $"null pair");
                if (pair.Value2 > MaxBase)
                    return Reject(out rejection, $"{pair.Value1} target {pair.Value2} above {MaxBase}");
                if (!seen.Add(pair.Value1))
                    return Reject(out rejection, $"{pair.Value1} sent twice");

                CharacterStat stat = pair.Value1;
                int target = (int)pair.Value2;
                int current = CurrentBase(stats, stat);
                if (target < current)
                    return Reject(out rejection, $"{stat} {current}->{target} lowers the base");
                int levelCap = target > current ? catalog.LevelCap(breed, profession, stat, level) : target;
                if (target > levelCap)
                    return Reject(out rejection, $"{stat} {current}->{target} over level cap {levelCap} at level {level}");
                if (!catalog.TryGetRaiseCost(breed, profession, stat, current, target, out long raise))
                    return Reject(out rejection, $"{stat} is not trainable");
                cost += raise;

                if (target == current)
                    continue;
                if (SkillCatalog.IsAbility(stat))
                    abilityRaises[stat] = target - current;
                else
                    skillRaises.Add((stat, target));
            }

            if (abilityRaises.Count == 0 && skillRaises.Count == 0)
                return Reject(out rejection, $"no raise");

            foreach ((CharacterStat skill, int target) in skillRaises)
            {
                int abilityCap = catalog.AbilityCap(stats, profession, skill, level, abilityRaises);
                if (target > abilityCap)
                    return Reject(out rejection, $"{skill} target {target} over ability cap {abilityCap}");
            }

            int ip = player.AvailableIp;
            if (cost > ip)
                return Reject(out rejection, $"cost {cost} exceeds ip {ip} (short {cost - ip})");

            foreach (GameTuple<CharacterStat, uint> pair in pairs)
                stats.Set(pair.Value1, (int)pair.Value2, StatDetail.Base);
            stats.Set(CharacterStat.UsedIP, stats.GetOrZero(CharacterStat.UsedIP, StatDetail.Base) + (int)cost, StatDetail.Base);
            player.RebaseStats();
            return true;
        }

        /// <summary>Why <see cref="TryReset"/> refused, for the client's feedback line.</summary>
        public enum ResetRefusal
        {
            None,
            NotResettable,
            MapNavigation,
            NoResetPoint,
            ItemsEquipped,
            NothingSpent,
            NoFullReset
        }

        /// <summary><see cref="CharacterStat.SpecialCondition"/> bit set once the character's free Full IP Reset is spent.</summary>
        public const int FreeFullResetUsedBit = 0x4;

        /// <summary>
        /// Skill reset (CharacterAction ResetSkill): costs one Reset Point (<see cref="CharacterStat.RP"/>), needs every
        /// equipment page empty, puts the skill's base back to <see cref="SkillCatalog.SkillFloor"/> and returns the IP
        /// that buying it from there costs. Prices depend only on profession and skill, so that is what was paid; the
        /// refund is still capped at <see cref="CharacterStat.UsedIP"/>, so no reset can make IP. Abilities are not
        /// reset, and nothing changes unless every check passes.
        /// </summary>
        public static bool TryReset(Player player, CharacterStat skill, SkillCatalog catalog, out int refund, out ResetRefusal refusal)
        {
            refund = 0;
            refusal = ResetRefusal.None;
            if (skill == CharacterStat.MapNavigation)
                return Refuse(out refusal, ResetRefusal.MapNavigation);
            if (!SkillCatalog.IsSkill(skill))
                return Refuse(out refusal, ResetRefusal.NotResettable);

            StatCollection stats = player.Stats;
            int resetPoints = stats.GetOrZero(CharacterStat.RP, StatDetail.Base);
            if (resetPoints <= 0)
                return Refuse(out refusal, ResetRefusal.NoResetPoint);

            if (!EquipmentEmpty(player))
                return Refuse(out refusal, ResetRefusal.ItemsEquipped);

            var breed = (Breed)stats.GetOrZero(CharacterStat.Breed, StatDetail.Base);
            var profession = (Profession)stats.GetOrZero(CharacterStat.Profession, StatDetail.Base);
            int current = CurrentBase(stats, skill);
            int used = stats.GetOrZero(CharacterStat.UsedIP, StatDetail.Base);
            if (current <= SkillCatalog.SkillFloor
                || !catalog.TryGetRaiseCost(breed, profession, skill, SkillCatalog.SkillFloor, current, out long cost)
                || cost <= 0 || used <= 0)
                return Refuse(out refusal, ResetRefusal.NothingSpent);

            refund = (int)Math.Min(cost, used);
            stats.Set(skill, SkillCatalog.SkillFloor, StatDetail.Base);
            stats.Set(CharacterStat.UsedIP, used - refund, StatDetail.Base);
            stats.Set(CharacterStat.RP, resetPoints - 1, StatDetail.Base);
            player.RebaseStats();
            return true;
        }

        /// <summary>
        /// Full IP Reset (CharacterAction ResetSkill with skill 0, the skill window's "Reset all skills"). Stock client
        /// (GUI.dll): the button is enabled while (<see cref="CharacterStat.SpecialCondition"/> bit
        /// <see cref="FreeFullResetUsedBit"/> clear ? 1 : 0) + <see cref="CharacterStat.FullIPRPoints"/> is above 0, and
        /// its confirm (502 "SkillView_ResetAllConfirm") says all abilities and skills go to their minimum and IP returns
        /// to the default for the level. So it does not cost Reset Points: it spends the free reset first, then one
        /// FullIPRPoints. Every ability goes back to the breed base and every skill, Map Navigation included, to
        /// <see cref="SkillCatalog.SkillFloor"/>; all of <see cref="CharacterStat.UsedIP"/> is returned. Needs every
        /// equipment page empty and something spent, so an empty reset never uses it up. Nothing changes unless every
        /// check passes.
        /// </summary>
        /// <param name="changed">The abilities and skills whose base was lowered; empty when refused.</param>
        public static bool TryFullReset(
            Player player,
            SkillCatalog catalog,
            out int refund,
            out List<CharacterStat> changed,
            out ResetRefusal refusal)
        {
            refund = 0;
            changed = [];
            refusal = ResetRefusal.None;

            StatCollection stats = player.Stats;
            int specialCondition = stats.GetOrZero(CharacterStat.SpecialCondition, StatDetail.Base);
            bool freeReset = (specialCondition & FreeFullResetUsedBit) == 0;
            int fullResetPoints = stats.GetOrZero(CharacterStat.FullIPRPoints, StatDetail.Base);
            if (!freeReset && fullResetPoints <= 0)
                return Refuse(out refusal, ResetRefusal.NoFullReset);

            if (!EquipmentEmpty(player))
                return Refuse(out refusal, ResetRefusal.ItemsEquipped);

            var breed = (Breed)stats.GetOrZero(CharacterStat.Breed, StatDetail.Base);
            var profession = (Profession)stats.GetOrZero(CharacterStat.Profession, StatDetail.Base);
            var lowered = new List<(CharacterStat Stat, int Floor)>();
            for (CharacterStat ability = CharacterStat.Strength; ability <= CharacterStat.Psychic; ability++)
            {
                if (catalog.TryGetAbilityFloor(breed, ability, out int floor)
                    && stats.TryGetValue(ability, out int current, StatDetail.Base) && !StatCollection.IsUnset(current)
                    && current > floor)
                    lowered.Add((ability, floor));
            }

            for (CharacterStat skill = CharacterStat.MartialArts; skill <= CharacterStat.NanoAC; skill++)
            {
                if (CurrentBase(stats, skill) > SkillCatalog.SkillFloor
                    && catalog.TryGetRaiseCost(breed, profession, skill, SkillCatalog.SkillFloor, SkillCatalog.SkillFloor, out _))
                    lowered.Add((skill, SkillCatalog.SkillFloor));
            }

            int used = stats.GetOrZero(CharacterStat.UsedIP, StatDetail.Base);
            if (lowered.Count == 0 && used <= 0)
                return Refuse(out refusal, ResetRefusal.NothingSpent);

            foreach ((CharacterStat stat, int floor) in lowered)
            {
                stats.Set(stat, floor, StatDetail.Base);
                changed.Add(stat);
            }

            refund = Math.Max(0, used);
            stats.Set(CharacterStat.UsedIP, 0, StatDetail.Base);
            if (freeReset)
                stats.Set(CharacterStat.SpecialCondition, specialCondition | FreeFullResetUsedBit, StatDetail.Base);
            else
                stats.Set(CharacterStat.FullIPRPoints, fullResetPoints - 1, StatDetail.Base);
            player.RebaseStats();
            return true;
        }

        static bool EquipmentEmpty(Player player)
        {
            PlayerInventory inventory = player.Inventory;
            return inventory.IsHydrated && inventory.Equipment.Content.Count == 0 && inventory.Armor.Content.Count == 0
                && inventory.Implant.Content.Count == 0 && inventory.Social.Content.Count == 0;
        }

        static bool Refuse(out ResetRefusal refusal, ResetRefusal reason)
        {
            refusal = reason;
            return false;
        }

        /// <summary>
        /// Diagnostic view of a request: each pair as current->target with its own IP price ("?" when it
        /// cannot be priced). At most <see cref="MaxPairs"/> pairs are described.
        /// </summary>
        public static string DescribeRequest(Player player, IReadOnlyList<GameTuple<CharacterStat, uint>>? pairs, SkillCatalog catalog)
        {
            if (pairs == null)
                return string.Empty;

            StatCollection stats = player.Stats;
            var breed = (Breed)stats.GetOrZero(CharacterStat.Breed, StatDetail.Base);
            var profession = (Profession)stats.GetOrZero(CharacterStat.Profession, StatDetail.Base);
            var parts = new List<string>(Math.Min(pairs.Count, MaxPairs));
            for (int i = 0; i < pairs.Count && i < MaxPairs; i++)
            {
                GameTuple<CharacterStat, uint> pair = pairs[i];
                if (pair == null)
                {
                    parts.Add("null");
                    continue;
                }

                int current = CurrentBase(stats, pair.Value1);
                string price = pair.Value2 <= MaxBase
                    && catalog.TryGetRaiseCost(breed, profession, pair.Value1, current, (int)pair.Value2, out long cost)
                        ? cost.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : "?";
                parts.Add(FormattableString.Invariant($"{pair.Value1} {current}->{pair.Value2} cost {price}"));
            }

            return string.Join(", ", parts);
        }

        static bool Reject(out string rejection, FormattableString reason)
        {
            rejection = FormattableString.Invariant(reason);
            return false;
        }

        /// <summary>
        /// Server bases for the trainable ids the client asked about, plus remaining IP. Sent after
        /// both accepted and rejected packets so the client's trainer never keeps an unpaid value.
        /// </summary>
        public static GameTuple<CharacterStat, uint>[] BuildReply(Player player, IReadOnlyList<GameTuple<CharacterStat, uint>>? pairs)
        {
            var reply = new List<GameTuple<CharacterStat, uint>>();
            var seen = new HashSet<CharacterStat>();
            if (pairs != null)
            {
                foreach (GameTuple<CharacterStat, uint> pair in pairs)
                {
                    if (reply.Count == MaxPairs)
                        break;
                    if (pair == null || !(SkillCatalog.IsAbility(pair.Value1) || SkillCatalog.IsSkill(pair.Value1)) || !seen.Add(pair.Value1))
                        continue;
                    reply.Add(Tuple(pair.Value1, CurrentBase(player.Stats, pair.Value1)));
                }
            }

            reply.Add(Tuple(CharacterStat.IP, Math.Max(0, player.AvailableIp)));
            return [.. reply];
        }

        static int CurrentBase(StatCollection stats, CharacterStat stat)
            => stats.TryGetValue(stat, out int value, StatDetail.Base) && !StatCollection.IsUnset(value)
                ? value
                : SkillCatalog.SkillFloor;

        static GameTuple<CharacterStat, uint> Tuple(CharacterStat stat, int value)
            => new() { Value1 = stat, Value2 = (uint)value };
    }
}
