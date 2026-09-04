namespace ZoneEngine_New.Core.Inventory.Dat
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using MsgPack;
    using MsgPack.Serialization;

    /// <summary>
    /// MessagePack mirror of legacy Core.ItemTemplate for items.dat slices.
    /// </summary>
    [Serializable]
    public sealed class DatItemTemplate
    {
        public Dictionary<int, int> Attack = new();

        public Dictionary<int, int> Defend = new();

        public int Flags;

        public int ID;

        public int ItemType;

        public int MultipleCount;

        public int Nothing;

        public int Quality;

        public List<int> Relations = new();

        public Dictionary<int, int> Stats = new();

        public List<DatAction> Actions { get; set; } = new();

        public List<DatEvent> Events { get; set; } = new();
    }

    [Serializable]
    public sealed class DatEvent
    {
        public EventType EventType { get; set; }

        public List<DatFunction> Functions { get; set; } = new();
    }

    [Serializable]
    public sealed class DatFunction
    {
        public DatFunctionArguments Arguments { get; set; } = new();

        public int FunctionType { get; set; }

        public List<DatRequirement> Requirements { get; set; } = new();

        public int Target { get; set; }

        public int TickCount { get; set; }

        public uint TickInterval { get; set; }

        public bool dolocalstats { get; set; } = true;
    }

    [Serializable]
    public sealed class DatFunctionArguments : IPackable, IUnpackable
    {
        public List<MessagePackObject> Values { get; set; } = new();

        public void PackToMessage(Packer packer, PackingOptions options)
        {
            packer.PackArrayHeader(Values.Count);
            foreach (MessagePackObject value in Values)
                packer.Pack(value);
        }

        public void UnpackFromMessage(Unpacker unpacker)
        {
            Values = new List<MessagePackObject>();
            if (!unpacker.IsArrayHeader)
                return;

            long count = unpacker.LastReadData.AsInt64();
            for (int i = 0; i < count; i++)
            {
                unpacker.Read();
                Values.Add(unpacker.LastReadData);
            }
        }
    }

    [Serializable]
    public sealed class DatAction
    {
        public ActionType ActionType { get; set; }

        public List<DatRequirement> Requirements { get; set; } = new();
    }

    [Serializable]
    public sealed class DatRequirement
    {
        public Operator ChildOperator { get; set; }

        public Operator Operator { get; set; }

        public int Statnumber { get; set; }

        public ItemTarget Target { get; set; }

        public int Value { get; set; }
    }
}
