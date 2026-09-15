namespace ZoneEngine_New.Core.GameData;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmokeLounge.AOtomation.Messaging.GameData;

public enum NanoMechanicKind { DurationOnly, ActiveStatOverlay, PeriodicTeamHeal, Morph, TeamTeleport, AreaTaunt }
public enum TeleportRecipientMode { Selected, Team }

/// <summary>Editable bindings and presentation for reusable nano mechanics. Provenance is descriptive.</summary>
public sealed class NanoMechanicDefinition
{
    public int NanoId { get; set; }
    public NanoMechanicKind Kind { get; set; }
    public string Provenance { get; set; } = string.Empty;
    public Dictionary<CharacterStat, int> Modifiers { get; set; } = new();
    public int[] ChildNanoIds { get; set; } = [];
    public int[] ExcludedPlayfields { get; set; } = [];
    public int[][] ExcludedPlayfieldRanges { get; set; } = [];
    public TeleportRecipientMode RecipientMode { get; set; }
    public double PulseSeconds { get; set; }
    public NanoHealTier[] HealTiers { get; set; } = [];
    public NanoVisualEffect? PulseVisual { get; set; }
    public NanoWireTemplate? ApplyVisual { get; set; }
    public NanoWireTemplate? RemoveVisual { get; set; }

    public bool AllowsPlayfield(int id) => id > 0 && !ExcludedPlayfields.Contains(id)
        && !ExcludedPlayfieldRanges.Any(range => id >= range[0] && id <= range[1]);
}

public sealed class NanoHealTier
{
    public int MinimumLevel { get; set; }
    public int NanoId { get; set; }
    public int Amount { get; set; }
}

public sealed class NanoVisualEffect
{
    public string Name { get; set; } = string.Empty;
    public int EffectType { get; set; }
    public int EffectId { get; set; }
    public int Hits { get; set; }
    public int Delay { get; set; }
    public int Life { get; set; }
    public int Size { get; set; }
    public int GraphicId { get; set; }
}

public sealed class NanoWireTemplate
{
    public string Hex { get; set; } = string.Empty;
    public int SourceActorId { get; set; }
    public int[] ActorOffsets { get; set; } = [];
    public NanoConditionalWireBlock? FlightBlock { get; set; }
}

public sealed class NanoConditionalWireBlock
{
    public int Offset { get; set; }
    public string Hex { get; set; } = string.Empty;
    public int CountOffset { get; set; }
    public int CountWithBlock { get; set; }
    public int CountWithoutBlock { get; set; }
}

public sealed class NanoMechanicCatalog
{
    private readonly Dictionary<int, NanoMechanicDefinition> _definitions;
    public IReadOnlyCollection<NanoMechanicDefinition> Definitions => _definitions.Values;
    public NanoMechanicCatalog(IEnumerable<NanoMechanicDefinition> definitions)
    {
        _definitions = new();
        foreach (var definition in definitions ?? throw new InvalidDataException("Missing nano mechanic content."))
        {
            if (definition == null || definition.NanoId <= 0 || !Enum.IsDefined(definition.Kind)
                || !Enum.IsDefined(definition.RecipientMode) || definition.Modifiers == null
                || definition.ChildNanoIds == null || definition.ExcludedPlayfields == null
                || definition.ExcludedPlayfieldRanges == null || definition.HealTiers == null
                || definition.HealTiers.Any(t => t == null)
                || !_definitions.TryAdd(definition.NanoId, definition)
                || definition.ChildNanoIds.Any(id => id <= 0 || id == definition.NanoId)
                || definition.ChildNanoIds.Distinct().Count() != definition.ChildNanoIds.Length
                || definition.ExcludedPlayfields.Any(id => id <= 0)
                || definition.ExcludedPlayfieldRanges.Any(range => range == null || range.Length != 2 || range[0] <= 0 || range[1] < range[0]))
                throw new InvalidDataException("Invalid or duplicate nano mechanic definition.");
            // This mechanic projects transient map capability bits. Durable actor stats
            // require their own transactional mechanics, never a base-value projection.
            if (definition.Kind == NanoMechanicKind.ActiveStatOverlay
                && (definition.Modifiers.Count == 0 || definition.Modifiers.Any(stat => stat.Key != CharacterStat.MapsC || stat.Value < 0)))
                throw new InvalidDataException("Unsupported transient nano bitmask projection.");
            if (definition.Kind == NanoMechanicKind.PeriodicTeamHeal
                && (!double.IsFinite(definition.PulseSeconds) || definition.PulseSeconds < 1.0 / TimeSpan.TicksPerSecond
                    || definition.HealTiers.Length == 0 || definition.HealTiers.Min(t => t.MinimumLevel) > 0
                    || definition.HealTiers.Any(t => t.NanoId <= 0 || t.Amount < 0)
                    || definition.HealTiers.Select(t => t.MinimumLevel).Distinct().Count() != definition.HealTiers.Length))
                throw new InvalidDataException("Invalid periodic heal definition.");
            ValidateVisual(definition.ApplyVisual);
            ValidateVisual(definition.RemoveVisual);
        }
    }
    public bool TryGet(int nanoId, NanoMechanicKind kind, out NanoMechanicDefinition definition)
        => _definitions.TryGetValue(nanoId, out definition!) && definition.Kind == kind;
    public NanoMechanicDefinition Get(int nanoId, NanoMechanicKind kind)
        => TryGet(nanoId, kind, out var definition) ? definition : throw new InvalidDataException("Missing nano mechanic definition.");
    public static NanoMechanicCatalog Load(string path)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new JsonStringEnumConverter());
        return new(JsonSerializer.Deserialize<NanoMechanicDefinition[]>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException("Missing nano mechanic content."));
    }
    public static NanoMechanicCatalog LoadDefault() => Load(Path.Combine(AppContext.BaseDirectory, "GameData", "NanoMechanics.json"));
    private static void ValidateVisual(NanoWireTemplate? visual)
    {
        if (visual == null) return;
        byte[] bytes = ReadHex(visual.Hex);
        if (bytes.Length < 33 || bytes.Length > ushort.MaxValue || visual.SourceActorId <= 0
            || visual.ActorOffsets == null || visual.ActorOffsets.Length == 0
            || BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) != 0x4D450114
            || visual.ActorOffsets.Any(offset => offset != 12 && offset < 24 || offset > bytes.Length - 4)
            || visual.ActorOffsets.Order().Zip(visual.ActorOffsets.Order().Skip(1), (a, b) => b - a < 4).Any(overlaps => overlaps))
            throw new InvalidDataException("Invalid nano visual template.");
        foreach (int offset in visual.ActorOffsets)
            if (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4)) != visual.SourceActorId
                || offset != 12 && BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset - 4, 4)) != (int)IdentityType.CanbeAffected)
                throw new InvalidDataException("Invalid nano visual actor patch location.");
        if (visual.FlightBlock is { } block)
        {
            byte[] effect = ReadHex(block.Hex);
            if (effect.Length == 0 || block.Offset < 33 || block.Offset > bytes.Length - effect.Length
                || block.CountOffset < 29 || block.CountOffset > bytes.Length - 4
                || Overlaps(block.CountOffset, 4, block.Offset, effect.Length)
                || visual.ActorOffsets.Any(offset => Overlaps(offset == 12 ? offset : offset - 4, offset == 12 ? 4 : 8, block.Offset, effect.Length)
                    || Overlaps(offset, 4, block.CountOffset, 4))
                || block.CountWithoutBlock < 0 || block.CountWithBlock <= block.CountWithoutBlock
                || !bytes.AsSpan(block.Offset, effect.Length).SequenceEqual(effect)
                || BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(block.CountOffset, 4)) != block.CountWithBlock)
                throw new InvalidDataException("Invalid conditional nano visual block.");
        }
    }

    private static bool Overlaps(int first, int firstLength, int second, int secondLength)
        => (long)first < (long)second + secondLength && (long)second < (long)first + firstLength;
    private static byte[] ReadHex(string hex)
    {
        try { return Convert.FromHexString(hex ?? throw new FormatException()); }
        catch (FormatException ex) { throw new InvalidDataException("Invalid nano visual hex content.", ex); }
    }
}
