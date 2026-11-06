namespace ChatEngine.Channels
{
    using System.Collections.Generic;

    using AORebirth.Database.Dao;

    using ChatEngine.CoreClient;

    /// <summary>
    /// One character's values for the join checks of every static channel. A stat is read from the database once per
    /// login, however many channels test it.
    /// </summary>
    public sealed class ChannelJoinContext
    {
        /// <summary>Stat owner type of a player character.</summary>
        private const int CharacterStatType = 50000;

        private readonly Dictionary<int, int> stats = new Dictionary<int, int>();

        public ChannelJoinContext(Client client)
        {
            this.Client = client;
        }

        public Client Client { get; }

        public int Side => this.Client.Character.CharacterSide;

        public int GmLevel => this.Client.Character.CharacterGMLevel;

        public int Stat(int statId)
        {
            if (!this.stats.TryGetValue(statId, out int value))
            {
                value = StatDao.Instance.GetById(CharacterStatType, (int)this.Client.Character.CharacterId, statId).StatValue;
                this.stats[statId] = value;
            }

            return value;
        }
    }

    /// <summary>A rule a character must pass to join a static channel (GameData ChatChannels.json "Conditions").</summary>
    public abstract class ChannelCondition
    {
        public abstract bool IsMet(ChannelJoinContext context);

        internal static bool AllMet(IReadOnlyList<ChannelCondition> conditions, ChannelJoinContext context)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                if (!conditions[i].IsMet(context))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>A character stat inside an inclusive range, and equal to one of the listed values when there are any.</summary>
    internal sealed class StatChannelCondition : ChannelCondition
    {
        private readonly int statId;
        private readonly int min;
        private readonly int max;
        private readonly HashSet<int> values;

        public StatChannelCondition(int statId, int min, int max, HashSet<int> values)
        {
            this.statId = statId;
            this.min = min;
            this.max = max;
            this.values = values;
        }

        public override bool IsMet(ChannelJoinContext context)
        {
            int value = context.Stat(this.statId);
            return value >= this.min && value <= this.max && (this.values == null || this.values.Contains(value));
        }
    }

    /// <summary>The character's side is one of the listed sides.</summary>
    internal sealed class SideChannelCondition : ChannelCondition
    {
        private readonly HashSet<int> sides;

        public SideChannelCondition(HashSet<int> sides)
        {
            this.sides = sides;
        }

        public override bool IsMet(ChannelJoinContext context) => this.sides.Contains(context.Side);
    }

    /// <summary>The account GM level inside an inclusive range.</summary>
    internal sealed class GmLevelChannelCondition : ChannelCondition
    {
        private readonly int min;
        private readonly int max;

        public GmLevelChannelCondition(int min, int max)
        {
            this.min = min;
            this.max = max;
        }

        public override bool IsMet(ChannelJoinContext context)
        {
            int level = context.GmLevel;
            return level >= this.min && level <= this.max;
        }
    }

    /// <summary>At least one nested condition passes.</summary>
    internal sealed class AnyOfChannelCondition : ChannelCondition
    {
        private readonly IReadOnlyList<ChannelCondition> conditions;

        public AnyOfChannelCondition(IReadOnlyList<ChannelCondition> conditions)
        {
            this.conditions = conditions;
        }

        public override bool IsMet(ChannelJoinContext context)
        {
            for (int i = 0; i < this.conditions.Count; i++)
            {
                if (this.conditions[i].IsMet(context))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>None of the nested conditions pass.</summary>
    internal sealed class NotChannelCondition : ChannelCondition
    {
        private readonly AnyOfChannelCondition any;

        public NotChannelCondition(IReadOnlyList<ChannelCondition> conditions)
        {
            this.any = new AnyOfChannelCondition(conditions);
        }

        public override bool IsMet(ChannelJoinContext context) => !this.any.IsMet(context);
    }
}
