namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    public sealed class ItemRequirement
    {
        public int ChildOperator { get; set; }

        public int Operator { get; set; }

        public int StatNumber { get; set; }

        public int Target { get; set; }

        public int Value { get; set; }

        public ItemRequirement Copy()
        {
            return new ItemRequirement
            {
                ChildOperator = ChildOperator,
                Operator = Operator,
                StatNumber = StatNumber,
                Target = Target,
                Value = Value
            };
        }
    }

    public sealed class ItemSpell
    {
        public int FunctionType { get; set; }

        public int Target { get; set; }

        public int TickCount { get; set; }

        public uint TickInterval { get; set; }

        public List<object> Arguments { get; set; } = new();

        public List<ItemRequirement> Requirements { get; set; } = new();

        public int ArgumentCount => Arguments.Count;

        public bool Is(FunctionType function) => FunctionType == (int)function;

        public bool MeetsRequirements(StatCollection stats)
        {
            ArgumentNullException.ThrowIfNull(stats);

            return MeetsRequirements(stat => stats.Get(stat));
        }

        public bool MeetsRequirements(Func<CharacterStat, int> getStat)
        {
            ArgumentNullException.ThrowIfNull(getStat);

            return ItemTemplate.MeetsRequirements(Requirements, getStat);
        }

        public bool TryReadInt(int index, out int value)
        {
            value = 0;
            if (index < 0 || index >= Arguments.Count)
                return false;

            switch (Arguments[index])
            {
                case int i:
                    value = i;
                    return true;
                case long l when l >= int.MinValue && l <= int.MaxValue:
                    value = (int)l;
                    return true;
                case uint u when u <= int.MaxValue:
                    value = (int)u;
                    return true;
                case short s:
                    value = s;
                    return true;
                case byte b:
                    value = b;
                    return true;
                default:
                    return false;
            }
        }

        public bool TryReadString(int index, out string value)
        {
            value = string.Empty;
            if (index < 0 || index >= Arguments.Count)
                return false;

            if (Arguments[index] is not string text)
                return false;

            value = text;
            return true;
        }

        /// <summary>
        /// Texture spell payload: argument 0 is the texture id, argument 1 the texture place.
        /// </summary>
        public bool TryReadTexture(out int place, out int textureId)
        {
            place = 0;
            return TryReadInt(0, out textureId) && TryReadInt(1, out place);
        }

        /// <summary>
        /// Mesh spell payload. Two or more arguments carry the override texture first and the mesh
        /// id second; a single argument is the mesh id with no override.
        /// </summary>
        public bool TryReadMesh(out int meshId, out int overrideTextureId)
        {
            meshId = 0;
            overrideTextureId = 0;
            if (Arguments.Count >= 2)
                return TryReadInt(0, out overrideTextureId) && TryReadInt(1, out meshId);

            return TryReadInt(0, out meshId);
        }

        public ItemSpell Copy()
        {
            var copy = new ItemSpell
            {
                FunctionType = FunctionType,
                Target = Target,
                TickCount = TickCount,
                TickInterval = TickInterval,
                Arguments = new List<object>(Arguments),
                Requirements = new List<ItemRequirement>(Requirements.Count)
            };

            foreach (ItemRequirement requirement in Requirements)
                copy.Requirements.Add(requirement.Copy());

            return copy;
        }
    }

    /// <summary>
    /// Stats that exist only while one event runs. <see cref="CharacterStat.Rnd"/> rolls 1–100
    /// and that value stays available as <see cref="CharacterStat.LastRnd"/> for later functions
    /// in the same event. <see cref="CharacterStat.SecondaryItemTemplate"/> is the low id of the
    /// item used on the target.
    /// </summary>
    public sealed class SpellCriteria
    {
        public const int RollMin = 1;
        public const int RollMaxInclusive = 100;

        public int SecondaryItemTemplate { get; init; }

        /// <summary>
        /// Door the character walked into. Vicinity <see cref="FunctionType.TeleportProxy"/> records
        /// this as the way back when the spell itself does not name a source door.
        /// </summary>
        public int SourceDoorInstance { get; init; }

        /// <summary>Item the event may destroy (the one used on the target, or the item being used).</summary>
        public Item? Subject { get; init; }

        public Identity SubjectSlot { get; init; }

        /// <summary>Test hook. Production rolls <see cref="RollMin"/> through <see cref="RollMaxInclusive"/>.</summary>
        public Func<int>? NextRoll { get; init; }

        int _lastRnd;

        public int Resolve(CharacterStat stat, Func<CharacterStat, int> characterStat)
        {
            ArgumentNullException.ThrowIfNull(characterStat);

            switch (stat)
            {
                case CharacterStat.SecondaryItemTemplate:
                    return SecondaryItemTemplate;
                case CharacterStat.Rnd:
                    _lastRnd = NextRoll != null
                        ? NextRoll()
                        : Random.Shared.Next(RollMin, RollMaxInclusive + 1);
                    return _lastRnd;
                case CharacterStat.LastRnd:
                    return _lastRnd;
                default:
                    return characterStat(stat);
            }
        }
    }

    public sealed class ItemAction
    {
        public int ActionType { get; set; }

        public List<ItemRequirement> Requirements { get; set; } = new();

        public ItemAction Copy()
        {
            var copy = new ItemAction
            {
                ActionType = ActionType,
                Requirements = new List<ItemRequirement>(Requirements.Count)
            };

            foreach (ItemRequirement requirement in Requirements)
                copy.Requirements.Add(requirement.Copy());

            return copy;
        }
    }
}
