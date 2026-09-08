namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using Vector3 = AORebirth.Core.Vector.Vector3;
using Quaternion = AORebirth.Core.Vector.Quaternion;

public interface IGeneratedMissionNpcFactory
{
    NpcCharacter Create(GeneratedMissionNpcEvidence evidence, GeneratedMissionObject persistedState, IItemBuilder items);
}

/// <summary>Exact accepted bundle NPC evidence, not a name/template similarity lookup.</summary>
public sealed class GeneratedMissionNpcEvidence
{
    readonly byte[] _packet;
    internal GeneratedMissionNpcEvidence(MissionAcgRuntimeObject source, MissionAcgLayoutBundle bundle, int quality, int missionType)
    {
        CapturedType = source.Identity.CapturedIdentity.Type; CapturedInstance = source.Identity.CapturedIdentity.Instance;
        RuntimeType = source.Identity.RuntimeIdentity.Type; RuntimeInstance = source.Identity.RuntimeIdentity.Instance;
        TemplateId = source.TemplateId; Name = source.Name; MissionQuality = quality;
        IsFindPerson = missionType == (int)MissionRollType.FindPerson && source.Identity.Kind == MissionAcgRuntimeObjectKind.ObjectiveNpc;
        BundleId = bundle.LayoutId; BundleSha256 = bundle.GeneratorPayloadSha256; SourcePlayfield2 = bundle.SourcePlayfield2; _packet = source.CopyPacket();
        var slot = bundle.NpcSlots.Single(value => value.CapturedIdentity.Equals(source.Identity.CapturedIdentity));
        CapturedSlot = slot.Slot; CapturedLevel = slot.CapturedLevel; CapturedHealth = slot.CapturedHealth;
        CapturedHealthDamage = slot.CapturedHealthDamage; MonsterData = slot.MonsterData;
    }
    public int CapturedType { get; }
    public int CapturedInstance { get; }
    public int RuntimeType { get; }
    public int RuntimeInstance { get; }
    public int TemplateId { get; }
    public string Name { get; }
    public int MissionQuality { get; }
    public bool IsFindPerson { get; }
    public string BundleId { get; }
    public string BundleSha256 { get; }
    public int SourcePlayfield2 { get; }
    public int CapturedSlot { get; }
    public int CapturedLevel { get; }
    public int CapturedHealth { get; }
    public int CapturedHealthDamage { get; }
    public int MonsterData { get; }
    public SimpleCharFullUpdateMessage CopySpawnMessage()
        => new ZoneMessageCodec().Deserialize((byte[])_packet.Clone())?.Body as SimpleCharFullUpdateMessage
            ?? throw new InvalidOperationException("Accepted NPC evidence is not a complete SCFU.");
}

/// <summary>One immutable SQL binding plus its exact accepted bundle and durable object snapshot.</summary>
public sealed class GeneratedMissionWorld
{
    readonly MissionAcgMaterializedInstance _instance;
    readonly GeneratedMissionObject[] _objects;
    readonly IGeneratedMissionNpcFactory _npcs;
    readonly Func<Player, GeneratedMissionObject, bool> _use;
    readonly MissionAcgSpatialEnvelope _envelope;
    readonly float _spawnX, _spawnY, _spawnZ, _exteriorX, _exteriorY, _exteriorZ;

    internal GeneratedMissionWorld(GeneratedMissionBinding binding, MissionAcgMaterializedInstance instance,
        IList<GeneratedMissionObject> objects, IGeneratedMissionNpcFactory npcs, Func<Player, GeneratedMissionObject, bool> use)
    {
        OwnerId = binding.OwnerId; QuestType = binding.QuestType; QuestInstance = binding.QuestInstance;
        LivePlayfield = binding.LivePlayfield; BundleId = binding.BundleId; BundleSha256 = binding.BundleSha256;
        BuildingType = binding.BuildingType; BuildingInstance = binding.BuildingInstance; ExpiresAtUtcTicks = binding.ExpiresAtUtcTicks;
        ExteriorPlayfield = binding.Offer.DestinationPlayfield;
        _exteriorX = binding.Offer.DestinationX; _exteriorY = binding.Offer.DestinationY; _exteriorZ = binding.Offer.DestinationZ;
        _spawnX = instance.Spawn.X; _spawnY = instance.Spawn.Y; _spawnZ = instance.Spawn.Z;
        _instance = instance; _objects = objects.ToArray(); _npcs = npcs; _use = use;
        if (!MissionAcgSpatialEnvelope.TryDerive(instance.Bundle, out _envelope, out string spatialFailure))
            throw new InvalidOperationException(spatialFailure);
        if (_objects.Length != instance.Objects.Count) throw new InvalidOperationException("Durable mission object set differs from its accepted bundle.");
        foreach (var source in instance.Objects)
        {
            var state = _objects.SingleOrDefault(row => row.RuntimeType == source.Identity.RuntimeIdentity.Type && row.RuntimeInstance == source.Identity.RuntimeIdentity.Instance);
            if (state == null || state.OwnerId != OwnerId || state.QuestType != QuestType || state.QuestInstance != QuestInstance
                || state.CapturedType != source.Identity.CapturedIdentity.Type || state.CapturedInstance != source.Identity.CapturedIdentity.Instance
                || state.Kind != (int)source.Identity.Kind || state.TemplateId != source.TemplateId)
                throw new InvalidOperationException("Durable mission object identity or template differs from the frozen bundle.");
        }
    }
    public int OwnerId { get; }
    public int QuestType { get; }
    public int QuestInstance { get; }
    public int LivePlayfield { get; }
    public string BundleId { get; }
    public string BundleSha256 { get; }
    public int BuildingType { get; }
    public int BuildingInstance { get; }
    public int ExteriorPlayfield { get; }
    public long ExpiresAtUtcTicks { get; }
    public Vector3 Spawn => new(_spawnX, _spawnY, _spawnZ);
    public Vector3 ExteriorPosition => new(_exteriorX, _exteriorY, _exteriorZ);
    public bool ContainsPosition(Vector3 position) => _envelope.Contains(position.xf, position.yf, position.zf);
    public bool AcceptsMovement(Character character, Vector3 proposed)
        => character.Playfield is MissionPlayfield field && field.World.Matches(this)
            && (!character.IsPlayer || character.Identity.Instance == OwnerId)
            && DateTime.UtcNow.Ticks < ExpiresAtUtcTicks && ContainsPosition(character.Position) && ContainsPosition(proposed)
            && Vector3.Abs(proposed - character.Position) <= _envelope.MaximumInternalDistance;
    public bool Matches(GeneratedMissionWorld other) => other != null && OwnerId == other.OwnerId && QuestType == other.QuestType
        && QuestInstance == other.QuestInstance && LivePlayfield == other.LivePlayfield && BundleId == other.BundleId
        && BundleSha256 == other.BundleSha256 && BuildingType == other.BuildingType && BuildingInstance == other.BuildingInstance
        && ExpiresAtUtcTicks == other.ExpiresAtUtcTicks && ExteriorPlayfield == other.ExteriorPlayfield
        && _exteriorX.Equals(other._exteriorX) && _exteriorY.Equals(other._exteriorY) && _exteriorZ.Equals(other._exteriorZ);
    internal bool Matches(GeneratedMissionBinding binding) => binding != null && OwnerId == binding.OwnerId && QuestType == binding.QuestType
        && QuestInstance == binding.QuestInstance && LivePlayfield == binding.LivePlayfield && BundleId == binding.BundleId
        && BundleSha256 == binding.BundleSha256 && BuildingType == binding.BuildingType && BuildingInstance == binding.BuildingInstance
        && ExpiresAtUtcTicks == binding.ExpiresAtUtcTicks && ExteriorPlayfield == binding.Offer.DestinationPlayfield
        && _exteriorX.Equals(binding.Offer.DestinationX) && _exteriorY.Equals(binding.Offer.DestinationY) && _exteriorZ.Equals(binding.Offer.DestinationZ);

    internal PlayfieldAnarchyFMessage CreateZoneMessage(SmokeLounge.AOtomation.Messaging.GameData.Vector3 position) => new()
    {
        Identity = new() { Type = IdentityType.Playfield2, Instance = LivePlayfield }, CharacterCoordinates = position,
        PlayfieldId1 = new() { Type = (IdentityType)BuildingType, Instance = BuildingInstance },
        PlayfieldId2 = new() { Type = IdentityType.Playfield2, Instance = LivePlayfield },
        PlayfieldX = 0, PlayfieldZ = 0, Unknown3 = 0, Unknown4 = 0, GeneratorPayload = _instance.Bundle.CopyGeneratorPayload()
    };

    internal IReadOnlyList<Dynel> CreateDynels(MissionPlayfield playfield, IItemBuilder items)
    {
        var result = new List<Dynel>();
        foreach (var source in _instance.Objects)
        {
            var state = _objects.Single(row => row.RuntimeType == source.Identity.RuntimeIdentity.Type && row.RuntimeInstance == source.Identity.RuntimeIdentity.Instance);
            if (state.ObjectiveConsumed) continue;
            if (state.IsDead)
            {
                var corpse = CreateCorpse(state, playfield);
                if (corpse != null) result.Add(corpse);
                continue;
            }
            Dynel dynel;
            if (source.Identity.Kind is MissionAcgRuntimeObjectKind.ObjectiveNpc or MissionAcgRuntimeObjectKind.AmbientNpc)
            {
                dynel = _npcs.Create(new GeneratedMissionNpcEvidence(source, _instance.Bundle, _instance.BindingRecord.Binding.MissionQuality, (int)_instance.BindingRecord.Binding.MissionType), state, items);
                if (dynel.Identity.Type != (IdentityType)state.RuntimeType || dynel.Identity.Instance != state.RuntimeInstance)
                    throw new InvalidOperationException("Mission NPC adapter changed authoritative runtime identity.");
            }
            else dynel = new MissionStaticDynel(state, source.CopyPacket(), _use);
            dynel.Playfield = playfield; dynel.Position = new Vector3(state.X, state.Y, state.Z);
            dynel.Rotation = new Quaternion(state.HeadingX, state.HeadingY, state.HeadingZ, state.HeadingW);
            result.Add(dynel);
        }
        return result;
    }

    internal GeneratedMissionCorpseDynel? CreateCorpse(GeneratedMissionObject state, MissionPlayfield playfield)
    {
        long now = DateTime.UtcNow.Ticks;
        if (!state.IsDead || state.CorpseClaimed || state.CorpseExpiresAtUtcTicks <= now
            || now < state.DiedAtUtcTicks + MissionAcgCorpseCreditPolicy.SpawnDelayMilliseconds * TimeSpan.TicksPerMillisecond) return null;
        if (state.OwnerId != OwnerId || state.QuestType != QuestType || state.QuestInstance != QuestInstance || state.RuntimeType != 50000)
            throw new InvalidOperationException("Mission corpse does not belong to this exact world.");
        var source = _instance.Objects.Single(value => value.Identity.RuntimeIdentity.Instance == state.RuntimeInstance && value.Identity.RuntimeIdentity.Type == state.RuntimeType);
        var evidence = new GeneratedMissionNpcEvidence(source, _instance.Bundle, _instance.BindingRecord.Binding.MissionQuality, (int)_instance.BindingRecord.Binding.MissionType);
        return new GeneratedMissionCorpseDynel(evidence, state, LivePlayfield)
        {
            Playfield = playfield, Position = new Vector3(state.X, state.Y, state.Z),
            Rotation = new Quaternion(state.HeadingX, state.HeadingY, state.HeadingZ, state.HeadingW)
        };
    }

    sealed class MissionStaticDynel : Dynel, IUsableDynel
    {
        readonly GeneratedMissionObject _state;
        readonly byte[] _packet;
        readonly Func<Player, GeneratedMissionObject, bool> _use;
        public MissionStaticDynel(GeneratedMissionObject state, byte[] packet, Func<Player, GeneratedMissionObject, bool> use)
            : base(new Identity { Type = (IdentityType)state.RuntimeType, Instance = state.RuntimeInstance })
        { _state = state; _packet = packet; _use = use; }
        public override MessageBody BuildSpawnMessage()
            => new ZoneMessageCodec().Deserialize((byte[])_packet.Clone())?.Body
                ?? throw new InvalidOperationException("Mission static object has no complete accepted wire projection.");
        public bool TryUse(Player player) => player.Identity.Instance == _state.OwnerId && player.Playfield == Playfield
            && Distance3D(player) <= 8.0 && _use(player, _state);
    }
}
