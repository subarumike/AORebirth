namespace ZoneEngine_New.Core.Inventory
{
    using System.Collections.Generic;

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
