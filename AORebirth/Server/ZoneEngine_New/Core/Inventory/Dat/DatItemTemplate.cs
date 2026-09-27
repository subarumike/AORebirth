#nullable enable

namespace ZoneEngine_New.Core.Inventory.Dat
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using MsgPack;
    using MsgPack.Serialization;

    /// <summary>
    /// MessagePack mirror of legacy Core.ItemTemplate for items.dat slices.
    /// Member ids 0-12 pin the alphabetical order existing items.dat files were written in;
    /// new fields go on the end so those files still unpack with the new field left empty.
    /// </summary>
    [Serializable]
    public sealed class DatItemTemplate
    {
        [MessagePackMember(1)]
        public Dictionary<int, int> Attack = new();

        [MessagePackMember(2)]
        public Dictionary<int, int> Defend = new();

        [MessagePackMember(5)]
        public int Flags;

        [MessagePackMember(6)]
        public int ID;

        [MessagePackMember(7)]
        public int ItemType;

        [MessagePackMember(8)]
        public int MultipleCount;

        [MessagePackMember(9)]
        public int Nothing;

        [MessagePackMember(10)]
        public int Quality;

        [MessagePackMember(11)]
        public List<int> Relations = new();

        [MessagePackMember(12)]
        public Dictionary<int, int> Stats = new();

        [MessagePackMember(0)]
        public List<DatAction> Actions { get; set; } = new();

        [MessagePackMember(4)]
        public List<DatEvent> Events { get; set; } = new();

        /// <summary>RDB record DynelType (e.g. Container = 51017).</summary>
        [MessagePackMember(3)]
        public int DynelType;

        /// <summary>RDB item or nano name. Null in files exported before names were written.</summary>
        [MessagePackMember(13)]
        public string? Name;
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
