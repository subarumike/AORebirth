namespace ZoneEngine_New.Core.Characters
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;

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

            int ip = stats.GetOrZero(CharacterStat.IP, StatDetail.Base);
            if (cost > ip)
                return Reject(out rejection, $"cost {cost} exceeds ip {ip} (short {cost - ip})");

            foreach (GameTuple<CharacterStat, uint> pair in pairs)
                stats.Set(pair.Value1, (int)pair.Value2, StatDetail.Base);
            stats.Set(CharacterStat.IP, ip - (int)cost, StatDetail.Base);
            player.RebaseStats();
            return true;
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

            reply.Add(Tuple(CharacterStat.IP, player.Stats.GetOrZero(CharacterStat.IP, StatDetail.Base)));
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
