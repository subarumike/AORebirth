namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using MsgPack.Serialization;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Inventory.Dat;

    public interface INanoCatalog
    {
        bool TryGet(int nanoId, out NanoDefinition definition);
    }

    /// <summary>Declared NanoFormula data, with the existing zero-for-absent attribute semantics.</summary>
    public sealed class NanoDefinition(ItemTemplate template)
    {
        public ItemTemplate Template { get; } = template;
        public int Id => Template.Id;
        public int Attribute(int id) => Template.Stats.GetValueOrDefault((CharacterStat)id);
        public int DurationCentiseconds => Attribute(8);
        public int NcuCost => Attribute(54);
        public int Strain => Attribute(75);
        public int RechargeCentiseconds => Attribute(210);
        public int RangeMeters => Attribute(287);
        public int AttackCentiseconds => Attribute(294);
        public int NanoCost => Attribute(407);
        public int AttackCapCentiseconds => Attribute(523);

        /// <summary>Legacy Character.CalculateNanoAttackTime, using wide arithmetic before bounds checks.</summary>
        public bool TryCalculateAttackTime(int aggDef, int nanoInit, out int centiseconds)
        {
            centiseconds = 0;
            if (AttackCentiseconds < 0 || AttackCapCentiseconds < 0 || RechargeCentiseconds < 0
                || NanoCost < 0 || NcuCost < 0 || DurationCentiseconds < 0) return false;
            long effectiveInit = nanoInit;
            if (effectiveInit > 1200) effectiveInit = (effectiveInit - 1200) / 3 + 1200;
            long delay = Math.Min(Math.Max((long)AttackCentiseconds - ((long)aggDef - 25)
                - (effectiveInit >> 1), AttackCapCentiseconds), AttackCentiseconds);
            if (delay < 0 || delay > int.MaxValue) return false;
            centiseconds = (int)delay;
            return true;
        }
    }

    /// <summary>
    /// Reads the existing packaged nanos.dat. Its MessagePack NanoFormula layout is deliberately
    /// distinct from ItemTemplate; using ItemsDatReader for this file would reinterpret fields.
    /// No download, name-only fallback, schema action, or historical capture input.
    /// </summary>
    public sealed class NanoCatalog : INanoCatalog
    {
        private readonly Dictionary<int, NanoDefinition> _definitions = new();
        public int Count => _definitions.Count;

        public NanoCatalog(IEnumerable<NanoDefinition> definitions)
        {
            ArgumentNullException.ThrowIfNull(definitions);
            foreach (NanoDefinition definition in definitions)
            {
                if (definition.Id <= 0 || !_definitions.TryAdd(definition.Id, definition))
                    throw new InvalidDataException("Duplicate or invalid nano identity.");
            }
        }

        public bool TryGet(int nanoId, out NanoDefinition definition)
            => _definitions.TryGetValue(nanoId, out definition!);

        public static NanoCatalog Load(string path)
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(file);
            int versionLength = reader.ReadByte();
            if (reader.ReadBytes(versionLength).Length != versionLength)
                throw new EndOfStreamException("Truncated nanos.dat version.");
            int packCount = reader.ReadInt32();
            int expectedCount = reader.ReadInt32();
            int sliceCount = reader.ReadInt32();
            if (packCount <= 0 || expectedCount <= 0 || sliceCount <= 0 || sliceCount > expectedCount)
                throw new InvalidDataException("Invalid nanos.dat inventory header.");
            var definitions = new List<NanoDefinition>();
            MessagePackSerializer<List<DatNanoFormula>> serializer = MessagePackSerializer.Get<List<DatNanoFormula>>();
            for (int i = 0; i < sliceCount; i++)
            {
                int size = reader.ReadInt32();
                if (size <= 0 || size > file.Length - file.Position)
                    throw new InvalidDataException("Truncated or invalid nanos.dat slice.");
                using var compressed = new MemoryStream(reader.ReadBytes(size));
                using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
                using var unpacked = new MemoryStream();
                zlib.CopyTo(unpacked);
                unpacked.Position = 0;
                List<DatNanoFormula> formulas = serializer.Unpack(unpacked)
                    ?? throw new InvalidDataException("Null nanos.dat slice.");
                foreach (DatNanoFormula formula in formulas)
                {
                    var item = new DatItemTemplate
                    {
                        ID = formula.ID, ItemType = formula.ItemType, Flags = formula.flags,
                        Stats = formula.Stats, Attack = formula.Attack, Defend = formula.Defend,
                        Actions = formula.Actions, Events = formula.Events
                    };
                    definitions.Add(new NanoDefinition(DatItemMapper.ToTemplate(item, string.Empty)));
                }
            }
            if (definitions.Count != expectedCount || file.Position != file.Length)
                throw new InvalidDataException("nanos.dat inventory count or trailing content mismatch.");
            return new NanoCatalog(definitions);
        }
    }

    // Exact public serialized fields/properties of AORebirth.Core.Nanos.NanoFormula, not its
    // gameplay methods or Legacy process dependencies. Shared action/event wire models are reused.
    [Serializable]
    public sealed class DatNanoFormula
    {
        public List<DatAction> Actions = new();
        public Dictionary<int, int> Attack = new();
        public Dictionary<int, int> Defend = new();
        public List<DatEvent> Events { get; set; } = new();
        public int ID;
        public int Instance;
        public int ItemType;
        public Dictionary<int, int> Stats = new();
        public int Type;
        public int flags;
    }
}
