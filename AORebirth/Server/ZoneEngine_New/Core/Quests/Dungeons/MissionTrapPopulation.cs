namespace ZoneEngine_New.Core.Quests.Dungeons;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

/// <summary>One accepted run's attached traps, sealed in the existing mission flag transaction.</summary>
public sealed class MissionTrapPopulation
{
    readonly IMissionDao _dao;
    readonly int _owner;
    readonly string _quest;
    readonly long _assigned;
    readonly string _frozen;
    readonly QuestDungeonParameters _parameters;
    readonly MissionTrapPolicy? _policy;
    readonly IZoneLogger _logger;
    bool _materialized;
    volatile bool _retired;
    readonly List<(Chest Parent, MissionTrap Trap, Action<Player> Open)> _attachments = [];

    public MissionTrapPopulation(IMissionDao dao, int owner, string quest, long assigned,
        string frozen, QuestDungeonParameters parameters, IZoneLogger logger)
    {
        ArgumentNullException.ThrowIfNull(dao);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(logger);
        if (owner <= 0 || assigned <= 0 || string.IsNullOrWhiteSpace(quest) || string.IsNullOrEmpty(frozen))
            throw new ArgumentException("Trap persistence requires an exact accepted mission run.");
        _dao = dao; _owner = owner; _quest = quest; _assigned = assigned;
        _frozen = frozen;
        _parameters = JsonSerializer.Deserialize<QuestDungeonParameters>(frozen,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("The accepted mission trap inputs are unreadable.");
        _policy = _parameters.Traps?.Clone(); _logger = logger;
        if (_policy != null && !_policy.IsValid)
            throw new InvalidDataException("Invalid frozen mission trap policy.");
    }

    public void Retire()
    {
        _retired = true;
        foreach (var entry in _attachments)
        {
            entry.Parent.AfterOpen -= entry.Open;
            entry.Trap.Retire();
        }
    }

    internal bool Populate(QuestDungeonPlayfield world)
    {
        if (_materialized || _retired || _policy?.Enabled != true)
            return true;
        var registry = world.GetRequiredService<DynelRegistry>();
        Chest[] parents = registry.Dynels().OfType<Chest>().OrderBy(parent => parent.Identity.Instance).ToArray();
        IItemBuilder items = world.GetRequiredService<IItemBuilder>();
        foreach (MissionTrapProfile profile in _policy.Profiles)
            if (!NanoRuntime.SupportsEnvironmentalPassiveBuff(items.CreateTemplate(profile.NanoId, profile.NanoId, 1)))
                throw new InvalidDataException("A configured mission trap effect is unsupported or missing.");
        var ids = world.GetRequiredService<IItemInstanceIdAllocator>();
        IReadOnlyList<MissionTrapSnapshot> stored = LoadOrInitialize(parents, ids);
        Materialize(world, stored, parents);
        _materialized = true;
        _logger.Info("Mission traps loaded quest=" + _quest + " count=" + stored.Count + " owner=" + _owner);
        return true;
    }

    internal void Materialize(Playfield world, IReadOnlyList<MissionTrapSnapshot> stored, IReadOnlyList<Chest> parents)
    {
        var registry = world.GetRequiredService<DynelRegistry>();
        var locality = world.GetRequiredService<PlayfieldLocality>();
        var staged = new List<(Chest Parent, MissionTrap Trap, Action<Player> Open)>();
        foreach (MissionTrapSnapshot snapshot in stored)
        {
            Chest? parent = parents.SingleOrDefault(chest => chest.Identity.Instance == snapshot.ParentInstance);
            MissionTrapProfile profile = _policy!.Profiles.Single(profile => profile.Id == snapshot.ProfileId);
            if (parent == null || snapshot.ParentType != (int)parent.Identity.Type)
                throw new InvalidDataException("A frozen mission trap parent is unavailable.");
            var identity = new Identity { Type = IdentityType.Trap, Instance = snapshot.Instance };
            var trap = new MissionTrap(identity, parent, profile.Spawn.Create(identity, parent.Identity, world.ClientPlayfieldId),
                profile.NanoId, _policy.Interaction, snapshot.State, TryCommitState,
                snapshot.RevealedByInstance > 0 ? new Identity
                    { Type = (IdentityType)snapshot.RevealedByType, Instance = snapshot.RevealedByInstance } : null)
            {
                Playfield = world, Position = parent.Position, Rotation = parent.Rotation,
                SpawnSource = SpawnSource.ContentPlacement,
                AccessValidator = _ => !_retired
            };
            staged.Add((parent, trap, actor => trap.TryTrigger(actor)));
        }
        try
        {
            foreach (var entry in staged)
            {
                if (!registry.TryRegister(entry.Trap))
                    throw new InvalidDataException("A frozen mission trap identity is already registered.");
                _attachments.Add(entry);
                entry.Parent.AfterOpen += entry.Open;
                locality.RegisterDynel(entry.Trap);
            }
        }
        catch (Exception failure)
        {
            // Deferred admission can already have observers. Remove only this attempt's
            // exact dynels and callbacks; locality retracts any already-published spawn.
            foreach (var entry in _attachments.AsEnumerable().Reverse())
            {
                entry.Parent.AfterOpen -= entry.Open;
                entry.Trap.Retire();
                try { locality.UnregisterDynel(entry.Trap); }
                catch (Exception cleanup) { failure.Data["MissionTrap.LocalityCleanup"] = cleanup; }
                registry.UnregisterExact(entry.Trap);
                entry.Trap.Playfield = null;
            }
            _attachments.Clear();
            throw;
        }
    }

    internal IReadOnlyList<MissionTrapSnapshot> LoadOrInitialize(IReadOnlyList<Chest> parents, IItemInstanceIdAllocator ids)
    {
        if (_policy?.Enabled != true)
            return [];
        List<MissionTrapSnapshot>? existing = _dao.Execute(_owner, transaction =>
        {
            RequireQuest(transaction);
            return ReadStored(transaction);
        });
        if (existing != null)
            return existing;
        var random = new Random(RandomNumberGenerator.GetInt32(int.MaxValue));
        // IDs and candidates are prepared before the transaction. A competing committed seal always wins.
        var planned = new List<MissionTrapSnapshot>();
        foreach (Chest parent in parents.OrderBy(_ => random.Next()))
        {
            if (planned.Count >= _policy.MaximumCount)
                break;
            if (random.NextDouble() >= _policy.ChestChance)
                continue;
            MissionTrapProfile? profile = _policy.Select(_parameters.Quality, random);
            if (profile == null)
                continue;
            planned.Add(new MissionTrapSnapshot
            {
                Instance = ids.Allocate(), ParentType = (int)parent.Identity.Type,
                ParentInstance = parent.Identity.Instance, ProfileId = profile.Id
            });
        }
        return _dao.Execute(_owner, transaction =>
        {
            RequireQuest(transaction);
            List<MissionTrapSnapshot>? stored = ReadStored(transaction);
            if (stored != null)
                return stored;
            var key = new MissionKeyData(_owner, _quest);
            for (int index = 0; index < planned.Count; index++)
                Save(transaction, key, SnapshotKey(index), JsonSerializer.Serialize(planned[index]));
            Save(transaction, key, SealKey, planned.Count.ToString(CultureInfo.InvariantCulture));
            return planned;
        });
    }

    internal bool TryCommitState(MissionTrap trap, Player actor, MissionTrapState next)
    {
        if (_retired)
            return false;
        return _dao.Execute(_owner, transaction =>
        {
            RequireQuest(transaction);
            List<MissionTrapSnapshot> stored = ReadStored(transaction)
                ?? throw new InvalidDataException("Mission trap state has no durable seal.");
            int index = stored.FindIndex(snapshot => snapshot.Instance == trap.Identity.Instance);
            if (index < 0 || stored[index].State != trap.State
                || (next & trap.State) != trap.State || ((int)next & ~63) != 0
                || stored[index].ParentType != (int)trap.ParentIdentity.Type
                || stored[index].ParentInstance != trap.ParentIdentity.Instance)
                return false;
            MissionTrapSnapshot snapshot = stored[index];
            snapshot.State = next;
            if ((next & MissionTrapState.Revealed) != 0 && snapshot.RevealedByInstance == 0)
            {
                snapshot.RevealedByType = (int)actor.Identity.Type;
                snapshot.RevealedByInstance = actor.Identity.Instance;
            }
            Save(transaction, new MissionKeyData(_owner, _quest), SnapshotKey(index), JsonSerializer.Serialize(snapshot));
            return true;
        });
    }

    void RequireQuest(IMissionDaoTransaction transaction)
    {
        if (_retired || transaction is not ICharacterQuestCompletionTransaction completion)
            throw new InvalidOperationException("Mission trap ownership validation is unavailable.");
        CharacterQuestCompletionSnapshotData? quest = completion.ReadCharacterQuest(_quest);
        if (quest == null || quest.CharacterId != _owner || quest.OwnerId != _owner
            || quest.OwnerType != (int)QuestOwnerType.Character || quest.Source != (int)QuestSource.Generated
            || quest.State != (int)QuestState.Active || quest.AssignedAtUtcTicks != _assigned
            || quest.ExpiresAtUtcTicks <= DateTime.UtcNow.Ticks
            || !string.Equals(quest.AcgBuildingGeneratorJson, _frozen, StringComparison.Ordinal))
            throw new InvalidOperationException("The exact accepted mission run is no longer active.");
    }

    List<MissionTrapSnapshot>? ReadStored(IMissionDaoTransaction transaction)
    {
        var key = new MissionKeyData(_owner, _quest);
        MissionFlagData? seal = transaction.GetFlag(key, SealKey);
        if (seal == null)
            return null;
        if (!int.TryParse(seal.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int count)
            || count < 0 || count > _policy!.MaximumCount)
            throw new InvalidDataException("Invalid mission trap population seal.");
        var result = new List<MissionTrapSnapshot>();
        var identities = new HashSet<int>();
        var parents = new HashSet<int>();
        for (int index = 0; index < count; index++)
        {
            MissionFlagData? flag = transaction.GetFlag(key, SnapshotKey(index));
            MissionTrapSnapshot? snapshot = flag == null ? null : JsonSerializer.Deserialize<MissionTrapSnapshot>(flag.Value);
            if (snapshot == null || snapshot.Instance <= 0 || snapshot.ParentInstance <= 0
                || snapshot.ParentType != (int)IdentityType.Container || !identities.Add(snapshot.Instance)
                || !parents.Add(snapshot.ParentInstance) || !_policy.Profiles.Any(profile => profile.Id == snapshot.ProfileId)
                || ((snapshot.State & MissionTrapState.Revealed) != 0
                    && (snapshot.RevealedByType != (int)IdentityType.CanbeAffected || snapshot.RevealedByInstance <= 0))
                || ((int)snapshot.State & ~63) != 0)
                throw new InvalidDataException("Invalid frozen mission trap snapshot.");
            result.Add(snapshot);
        }
        return result;
    }

    static void Save(IMissionDaoTransaction transaction, MissionKeyData key, string name, string value)
    {
        if (name.Length > 128 || value.Length > 1024)
            throw new InvalidDataException("Mission trap state exceeds existing mission flag limits.");
        MissionFlagData? row = transaction.GetFlag(key, name);
        long now = DateTime.UtcNow.Ticks;
        row ??= new MissionFlagData { CharacterId = key.CharacterId, QuestId = key.QuestId, FlagKey = name, CreatedAtUtcTicks = now };
        row.Value = value;
        row.UpdatedAtUtcTicks = now;
        transaction.SaveFlag(key, row);
    }

    string Prefix => "mission-traps:" + _assigned.ToString(CultureInfo.InvariantCulture) + ":";
    string SealKey => Prefix + "sealed";
    string SnapshotKey(int index) => Prefix + "trap:" + index.ToString(CultureInfo.InvariantCulture);
}

public sealed class MissionTrapSnapshot
{
    public int Instance { get; set; }
    public int ParentType { get; set; }
    public int ParentInstance { get; set; }
    public string ProfileId { get; set; } = string.Empty;
    public MissionTrapState State { get; set; }
    public int RevealedByType { get; set; }
    public int RevealedByInstance { get; set; }
}
