namespace ZoneEngine_New.Core.Helpers
{
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    public enum AwardCredit
    {
        Shared,
        Full
    }

    /// <summary>
    /// Fight-scoped player HP-removed ledger. Today one player is one bucket;
    /// a later team id can group records without changing death or loot stamping.
    /// </summary>
    public sealed class KillRewardResolver
    {
        public const int MinimumContributionPercent = 20;

        readonly Dictionary<int, int> _damage = new();
        readonly Dictionary<int, Identity> _identities = new();
        readonly Dictionary<int, int> _firstCreditOrder = new();
        int _nextOrder;

        public void Record(Identity identity, int hpRemoved)
        {
            int instance = identity.Instance;
            if (instance == 0 || hpRemoved <= 0)
                return;

            if (!_damage.TryGetValue(instance, out int current))
            {
                _identities[instance] = identity;
                _firstCreditOrder[instance] = _nextOrder++;
                _damage[instance] = hpRemoved;
                return;
            }

            _damage[instance] = current + hpRemoved;
        }

        public void Clear()
        {
            _damage.Clear();
            _identities.Clear();
            _firstCreditOrder.Clear();
            _nextOrder = 0;
        }

        public bool TryGetLootWinner(out Identity identity)
        {
            identity = Identity.None;
            int bestDamage = 0;
            int bestOrder = int.MaxValue;
            int winner = 0;

            foreach (KeyValuePair<int, int> pair in _damage)
            {
                int order = _firstCreditOrder[pair.Key];
                if (pair.Value < bestDamage)
                    continue;
                if (pair.Value == bestDamage && order >= bestOrder)
                    continue;

                bestDamage = pair.Value;
                bestOrder = order;
                winner = pair.Key;
            }

            if (winner == 0)
                return false;

            identity = _identities[winner];
            return true;
        }

        public List<int> GetQualifyingPlayerInstances()
        {
            var qualifiers = new List<int>();
            if (_damage.Count == 0)
                return qualifiers;

            long total = 0;
            foreach (int damage in _damage.Values)
                total += damage;

            if (total <= 0)
                return qualifiers;

            foreach (KeyValuePair<int, int> pair in _damage)
            {
                if (pair.Value * 100L >= total * MinimumContributionPercent)
                    qualifiers.Add(pair.Key);
            }

            if (qualifiers.Count > 0)
                return qualifiers;

            int max = 0;
            foreach (int damage in _damage.Values)
            {
                if (damage > max)
                    max = damage;
            }

            foreach (KeyValuePair<int, int> pair in _damage)
            {
                if (pair.Value == max)
                    qualifiers.Add(pair.Key);
            }

            return qualifiers;
        }

        public bool TryGetIdentity(int playerInstance, out Identity identity)
            => _identities.TryGetValue(playerInstance, out identity);

        public List<AwardShare> Divide(int pool, IReadOnlyList<int> recipients, AwardCredit credit)
        {
            if (credit == AwardCredit.Full)
                return FullAward(pool, recipients);

            return SplitAward(pool, recipients);
        }

        public List<AwardShare> SplitAward(int pool, IReadOnlyList<int> recipients)
        {
            var shares = new List<AwardShare>();
            if (pool <= 0 || recipients.Count == 0)
                return shares;

            long qualifyingDamage = 0;
            int highestInstance = 0;
            int highestDamage = -1;
            int highestOrder = int.MaxValue;

            for (int i = 0; i < recipients.Count; i++)
            {
                int instance = recipients[i];
                if (!_damage.TryGetValue(instance, out int damage) || damage <= 0)
                    continue;

                qualifyingDamage += damage;
                int order = _firstCreditOrder.TryGetValue(instance, out int recorded) ? recorded : int.MaxValue;
                if (damage < highestDamage)
                    continue;
                if (damage == highestDamage && order >= highestOrder)
                    continue;

                highestDamage = damage;
                highestOrder = order;
                highestInstance = instance;
            }

            if (qualifyingDamage <= 0)
                return shares;

            int assigned = 0;
            for (int i = 0; i < recipients.Count; i++)
            {
                int instance = recipients[i];
                if (!_damage.TryGetValue(instance, out int damage) || damage <= 0)
                    continue;

                int amount = (int)(pool * (long)damage / qualifyingDamage);
                shares.Add(new AwardShare(instance, amount));
                assigned += amount;
            }

            int leftover = pool - assigned;
            if (leftover <= 0 || highestInstance == 0)
                return shares;

            for (int i = 0; i < shares.Count; i++)
            {
                if (shares[i].PlayerInstance != highestInstance)
                    continue;

                shares[i] = new AwardShare(highestInstance, shares[i].Amount + leftover);
                break;
            }

            return shares;
        }

        List<AwardShare> FullAward(int pool, IReadOnlyList<int> recipients)
        {
            var shares = new List<AwardShare>();
            if (pool <= 0 || recipients.Count == 0)
                return shares;

            for (int i = 0; i < recipients.Count; i++)
            {
                int instance = recipients[i];
                if (!_damage.TryGetValue(instance, out int damage) || damage <= 0)
                    continue;

                shares.Add(new AwardShare(instance, pool));
            }

            return shares;
        }
    }

    public readonly struct AwardShare
    {
        public AwardShare(int playerInstance, int amount)
        {
            PlayerInstance = playerInstance;
            Amount = amount;
        }

        public int PlayerInstance { get; }

        public int Amount { get; }
    }
}
