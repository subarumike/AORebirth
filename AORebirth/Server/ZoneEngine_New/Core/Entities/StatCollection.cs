namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    public enum StatDetail
    {
        Base,
        Bonus,
        Full
    }

    internal sealed class StatValue
    {
        private int _base;
        private int _bonus;

        public StatValue()
        {
        }

        internal StatValue(int @base, int bonus)
        {
            Base = @base;
            Bonus = bonus;
        }

        internal int Base
        {
            get => _base;
            set
            {
                _base = value;
                Full = _base + _bonus;
            }
        }

        internal int Bonus
        {
            get => _bonus;
            set
            {
                _bonus = value;
                Full = _base + _bonus;
            }
        }

        internal int Full { get; private set; }
    }

    /// <summary>
    /// Base/bonus/full stat store for dynels. Applies values from Stat, FullCharacter, and SCFU messages.
    /// </summary>
    public class StatCollection
    {
        public const int Unset = 1234567890;

        readonly Dictionary<CharacterStat, StatValue> _values = new();
        readonly HashSet<CharacterStat> _dirty = new();

        public event Action<CharacterStat, int, int, bool>? StatChanged;

        public static bool IsUnset(int value) => value == Unset;

        public bool TryGetValue(CharacterStat stat, out int value, StatDetail detail = StatDetail.Full)
        {
            if (!_values.TryGetValue(stat, out StatValue? statValue))
            {
                value = Unset;
                return false;
            }

            value = GetDetail(statValue, detail);
            return true;
        }

        public int Get(CharacterStat stat, StatDetail detail = StatDetail.Full)
            => _values.TryGetValue(stat, out StatValue? statValue) ? GetDetail(statValue, detail) : Unset;

        public IEnumerable<(CharacterStat Stat, int Base, int Bonus, int Full)> GetEntries()
        {
            foreach (KeyValuePair<CharacterStat, StatValue> pair in _values)
                yield return (pair.Key, pair.Value.Base, pair.Value.Bonus, pair.Value.Full);
        }

        public void Set(CharacterStat stat, int value, StatDetail detail = StatDetail.Base, bool dirty = false)
        {
            bool isInitialSet = !_values.TryGetValue(stat, out StatValue? existing);
            existing ??= new StatValue();

            int previousFull = existing.Full;

            if (detail == StatDetail.Bonus)
                existing.Bonus = value;
            else
                existing.Base = value;

            if (!isInitialSet && previousFull == existing.Full)
                return;

            _values[stat] = existing;
            if (dirty)
                _dirty.Add(stat);

            StatChanged?.Invoke(stat, previousFull, existing.Full, isInitialSet);
        }

        /// <summary>
        /// Takes ownership of all dirty stats (latest full values) and clears the dirty set.
        /// </summary>
        public GameTuple<CharacterStat, uint>[] DrainDirty()
        {
            if (_dirty.Count == 0)
                return [];

            var drained = new GameTuple<CharacterStat, uint>[_dirty.Count];
            int index = 0;
            foreach (CharacterStat stat in _dirty)
            {
                drained[index++] = new GameTuple<CharacterStat, uint>
                {
                    Value1 = stat,
                    Value2 = (uint)Get(stat)
                };
            }

            _dirty.Clear();
            return drained;
        }

        static int GetDetail(StatValue statValue, StatDetail detail) => detail switch
        {
            StatDetail.Base => statValue.Base,
            StatDetail.Bonus => statValue.Bonus,
            _ => statValue.Full,
        };
    }
}
