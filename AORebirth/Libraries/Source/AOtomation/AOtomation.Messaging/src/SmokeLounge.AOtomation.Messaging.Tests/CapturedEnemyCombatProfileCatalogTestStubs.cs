namespace AORebirth.Enums
{
    internal enum StatIds
    {
        monsterdata = 0,
        level = 1
    }
}

namespace AORebirth.Core.Entities
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    internal sealed class Character
    {
        internal Character()
        {
            this.Stats = new CapturedEnemyCombatTestStats();
        }

        internal CapturedEnemyCombatTestPlayfield Playfield { get; set; }

        internal string Name { get; set; }

        internal CapturedEnemyCombatTestStats Stats { get; private set; }
    }

    internal sealed class CapturedEnemyCombatTestPlayfield
    {
        internal CapturedEnemyCombatTestIdentity Identity { get; set; }
    }

    internal sealed class CapturedEnemyCombatTestIdentity
    {
        internal int Instance { get; set; }
    }

    internal sealed class CapturedEnemyCombatTestStat
    {
        internal int Value { get; set; }
    }

    internal sealed class CapturedEnemyCombatTestStats
    {
        private readonly Dictionary<StatIds, CapturedEnemyCombatTestStat> values =
            new Dictionary<StatIds, CapturedEnemyCombatTestStat>();

        internal CapturedEnemyCombatTestStat this[StatIds stat]
        {
            get
            {
                CapturedEnemyCombatTestStat value;
                if (!this.values.TryGetValue(stat, out value))
                {
                    value = new CapturedEnemyCombatTestStat();
                    this.values.Add(stat, value);
                }

                return value;
            }
        }
    }
}

namespace ZoneEngine.Core
{
    public enum NpcAiProfile
    {
        Passive,
        Aggressive,
        Social
    }
}

namespace ZoneEngine.Core.Controllers
{
    internal static class CapturedEnemyCombatProfileCatalogTestControllerNamespace
    {
    }
}
