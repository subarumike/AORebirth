namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    internal enum SubwayLootPoolKind
    {
        Dungeon = 0,
        EnemyType = 1,
        Named = 2,
        Boss = 3
    }

    internal sealed class SubwayLootRollContext
    {
        internal SubwayLootRollContext(
            int playfieldId,
            string enemyName,
            string enemyTypeKey,
            int monsterData,
            int enemyLevel,
            int playerLevel,
            bool isNamed,
            bool isBoss)
        {
            if (playfieldId <= 0)
            {
                throw new ArgumentOutOfRangeException("playfieldId");
            }

            if (string.IsNullOrWhiteSpace(enemyName))
            {
                throw new ArgumentException("Enemy name is required.", "enemyName");
            }

            if (!IsSafeEnemyTypeKey(enemyTypeKey))
            {
                throw new ArgumentException(
                    "Enemy type key must start with a lowercase letter and contain only lowercase letters, digits, or underscores.",
                    "enemyTypeKey");
            }

            if (monsterData <= 0)
            {
                throw new ArgumentOutOfRangeException("monsterData");
            }

            if (enemyLevel <= 0)
            {
                throw new ArgumentOutOfRangeException("enemyLevel");
            }

            if (playerLevel <= 0)
            {
                throw new ArgumentOutOfRangeException("playerLevel");
            }

            this.PlayfieldId = playfieldId;
            this.EnemyName = enemyName;
            this.EnemyTypeKey = enemyTypeKey;
            this.MonsterData = monsterData;
            this.EnemyLevel = enemyLevel;
            this.PlayerLevel = playerLevel;
            this.IsNamed = isNamed;
            this.IsBoss = isBoss;
        }

        internal int PlayfieldId { get; private set; }

        internal string EnemyName { get; private set; }

        internal string EnemyTypeKey { get; private set; }

        internal int MonsterData { get; private set; }

        internal int EnemyLevel { get; private set; }

        internal int PlayerLevel { get; private set; }

        internal bool IsNamed { get; private set; }

        internal bool IsBoss { get; private set; }

        private static bool IsSafeEnemyTypeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value[0] < 'a'
                || value[0] > 'z')
            {
                return false;
            }

            for (int index = 1; index < value.Length; index++)
            {
                char character = value[index];
                if ((character < 'a' || character > 'z')
                    && (character < '0' || character > '9')
                    && character != '_')
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal sealed class SubwayLootPoolReference
    {
        internal SubwayLootPoolReference(string key, SubwayLootPoolKind kind)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Pool key is required.", "key");
            }

            this.Key = key;
            this.Kind = kind;
        }

        internal string Key { get; private set; }

        internal SubwayLootPoolKind Kind { get; private set; }
    }

    internal sealed class SubwayLootPoolSelectionPlan
    {
        internal SubwayLootPoolSelectionPlan(
            SubwayLootRollContext context,
            SubwayLootPoolReference[] pools)
        {
            this.Context = context;
            this.Pools = pools ?? new SubwayLootPoolReference[0];
        }

        internal SubwayLootRollContext Context { get; private set; }

        internal SubwayLootPoolReference[] Pools { get; private set; }
    }

    internal sealed class SubwayLootPoolCandidate
    {
        private SubwayLootPoolCandidate(
            string candidateKey,
            int lowId,
            int highId,
            int minimumQuality,
            int maximumQuality,
            int weight,
            int observedCount,
            int observedKills,
            bool explicitlyGuaranteed,
            string evidence)
        {
            if (string.IsNullOrWhiteSpace(candidateKey))
            {
                throw new ArgumentException("Candidate key is required.", "candidateKey");
            }

            if (lowId <= 0)
            {
                throw new ArgumentOutOfRangeException("lowId");
            }

            if (highId <= 0)
            {
                throw new ArgumentOutOfRangeException("highId");
            }

            if (minimumQuality <= 0 || maximumQuality < minimumQuality)
            {
                throw new ArgumentOutOfRangeException("minimumQuality");
            }

            if (weight < 0)
            {
                throw new ArgumentOutOfRangeException("weight");
            }

            if (string.IsNullOrWhiteSpace(evidence))
            {
                throw new ArgumentException("Evidence is required.", "evidence");
            }

            this.CandidateKey = candidateKey;
            this.LowId = lowId;
            this.HighId = highId;
            this.MinimumQuality = minimumQuality;
            this.MaximumQuality = maximumQuality;
            this.Weight = weight;
            this.ObservedCount = observedCount;
            this.ObservedKills = observedKills;
            this.ExplicitlyGuaranteed = explicitlyGuaranteed;
            this.Evidence = evidence;
        }

        internal string CandidateKey { get; private set; }

        internal int LowId { get; private set; }

        internal int HighId { get; private set; }

        internal int MinimumQuality { get; private set; }

        internal int MaximumQuality { get; private set; }

        internal int Weight { get; private set; }

        internal int ObservedCount { get; private set; }

        internal int ObservedKills { get; private set; }

        internal bool ExplicitlyGuaranteed { get; private set; }

        internal string Evidence { get; private set; }

        internal static SubwayLootPoolCandidate FromObservedSample(
            string candidateKey,
            int lowId,
            int highId,
            int minimumQuality,
            int maximumQuality,
            int observedCount,
            int observedKills,
            int weight,
            string evidence)
        {
            if (observedCount <= 0)
            {
                throw new ArgumentOutOfRangeException("observedCount");
            }

            if (observedKills <= 0)
            {
                throw new ArgumentOutOfRangeException("observedKills");
            }

            if (weight <= 0)
            {
                throw new ArgumentOutOfRangeException("weight");
            }

            return new SubwayLootPoolCandidate(
                candidateKey,
                lowId,
                highId,
                minimumQuality,
                maximumQuality,
                weight,
                observedCount,
                observedKills,
                false,
                evidence);
        }

        internal static SubwayLootPoolCandidate ExplicitGuaranteed(
            string candidateKey,
            int lowId,
            int highId,
            int minimumQuality,
            int maximumQuality,
            string evidence)
        {
            return new SubwayLootPoolCandidate(
                candidateKey,
                lowId,
                highId,
                minimumQuality,
                maximumQuality,
                0,
                0,
                0,
                true,
                evidence);
        }
    }

    internal sealed class SubwayLootPoolDefinition
    {
        internal SubwayLootPoolDefinition(
            string key,
            SubwayLootPoolKind kind,
            int emptyWeight,
            SubwayLootPoolCandidate[] candidates)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Pool key is required.", "key");
            }

            if (emptyWeight < 0)
            {
                throw new ArgumentOutOfRangeException("emptyWeight");
            }

            SubwayLootPoolCandidate[] safeCandidates =
                candidates ?? new SubwayLootPoolCandidate[0];
            var candidateKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (SubwayLootPoolCandidate candidate in safeCandidates)
            {
                if (candidate == null)
                {
                    throw new ArgumentException("Pool candidates cannot contain null.", "candidates");
                }

                if (!candidateKeys.Add(candidate.CandidateKey))
                {
                    throw new ArgumentException(
                        "Duplicate pool candidate key: " + candidate.CandidateKey,
                        "candidates");
                }
            }

            this.Key = key;
            this.Kind = kind;
            this.EmptyWeight = emptyWeight;
            this.Candidates = new SubwayLootPoolCandidate[safeCandidates.Length];
            Array.Copy(safeCandidates, this.Candidates, safeCandidates.Length);
        }

        internal string Key { get; private set; }

        internal SubwayLootPoolKind Kind { get; private set; }

        internal int EmptyWeight { get; private set; }

        internal SubwayLootPoolCandidate[] Candidates { get; private set; }
    }

    internal sealed class SubwayLootPoolRollResult
    {
        internal SubwayLootPoolRollResult(
            SubwayLootPoolCandidate[] guaranteedCandidates,
            SubwayLootPoolCandidate weightedCandidate)
        {
            this.GuaranteedCandidates =
                guaranteedCandidates ?? new SubwayLootPoolCandidate[0];
            this.WeightedCandidate = weightedCandidate;
        }

        internal SubwayLootPoolCandidate[] GuaranteedCandidates { get; private set; }

        internal SubwayLootPoolCandidate WeightedCandidate { get; private set; }
    }

    internal static class SubwayLootPoolRules
    {
        internal const int SubwayPlayfieldId = 127;

        internal static SubwayLootPoolSelectionPlan BuildSelectionPlan(
            SubwayLootRollContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (context.PlayfieldId != SubwayPlayfieldId)
            {
                throw new ArgumentException(
                    "Subway loot rules only accept playfield 127.",
                    "context");
            }

            if (context.IsBoss)
            {
                return DedicatedPlan(context, SubwayLootPoolKind.Boss, "boss");
            }

            if (context.IsNamed)
            {
                return DedicatedPlan(context, SubwayLootPoolKind.Named, "named");
            }

            return new SubwayLootPoolSelectionPlan(
                context,
                new[]
                    {
                        new SubwayLootPoolReference(
                            PoolKey(context.PlayfieldId, "dungeon", null),
                            SubwayLootPoolKind.Dungeon),
                        new SubwayLootPoolReference(
                            PoolKey(context.PlayfieldId, "enemy", context.EnemyTypeKey),
                            SubwayLootPoolKind.EnemyType)
                    });
        }

        internal static SubwayLootPoolRollResult Roll(
            SubwayLootPoolDefinition pool,
            Func<int, int> nextRandom)
        {
            if (pool == null)
            {
                throw new ArgumentNullException("pool");
            }

            if (nextRandom == null)
            {
                throw new ArgumentNullException("nextRandom");
            }

            var guaranteed = new List<SubwayLootPoolCandidate>();
            var weighted = new List<SubwayLootPoolCandidate>();
            long totalWeight = pool.EmptyWeight;

            foreach (SubwayLootPoolCandidate candidate in pool.Candidates)
            {
                if (candidate.ExplicitlyGuaranteed)
                {
                    guaranteed.Add(candidate);
                    continue;
                }

                if (candidate.Weight <= 0)
                {
                    continue;
                }

                weighted.Add(candidate);
                totalWeight += candidate.Weight;
            }

            if (totalWeight > int.MaxValue)
            {
                throw new InvalidOperationException("Loot pool weight exceeds Int32 range.");
            }

            if (totalWeight <= 0)
            {
                return new SubwayLootPoolRollResult(guaranteed.ToArray(), null);
            }

            int roll = nextRandom((int)totalWeight);
            if (roll < 0 || roll >= totalWeight)
            {
                throw new InvalidOperationException("Loot random source returned an invalid value.");
            }

            if (roll < pool.EmptyWeight)
            {
                return new SubwayLootPoolRollResult(guaranteed.ToArray(), null);
            }

            int weightedRoll = roll - pool.EmptyWeight;
            foreach (SubwayLootPoolCandidate candidate in weighted)
            {
                if (weightedRoll < candidate.Weight)
                {
                    return new SubwayLootPoolRollResult(guaranteed.ToArray(), candidate);
                }

                weightedRoll -= candidate.Weight;
            }

            throw new InvalidOperationException("Loot pool weights did not resolve a candidate.");
        }

        private static SubwayLootPoolSelectionPlan DedicatedPlan(
            SubwayLootRollContext context,
            SubwayLootPoolKind kind,
            string category)
        {
            return new SubwayLootPoolSelectionPlan(
                context,
                new[]
                    {
                        new SubwayLootPoolReference(
                            PoolKey(context.PlayfieldId, category, context.EnemyTypeKey),
                            kind)
                    });
        }

        private static string PoolKey(int playfieldId, string category, string enemyTypeKey)
        {
            return !string.IsNullOrEmpty(enemyTypeKey)
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "subway.{0}.{1}.{2}",
                    playfieldId,
                    category,
                    enemyTypeKey)
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "subway.{0}.{1}",
                    playfieldId,
                    category);
        }
    }
}
