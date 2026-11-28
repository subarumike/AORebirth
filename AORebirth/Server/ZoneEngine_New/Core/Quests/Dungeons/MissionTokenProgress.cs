namespace ZoneEngine_New.Core.Quests.Dungeons;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Helpers;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;

/// <summary>One accepted mission's frozen population and unique kills, in the existing mission DAO tables.</summary>
public sealed class MissionTokenProgress
{
    const string ObjectiveSlot = "objective:0";

    readonly IMissionDao _dao;
    readonly int _ownerId;
    readonly string _questId;
    readonly long _assignedAtTicks;
    readonly MissionTokenRewardPolicy _policy;
    readonly Dictionary<string, PopulationBranch[]> _storedPoints;
    readonly Dictionary<string, PopulationBranch> _census = new(StringComparer.Ordinal);
    readonly Dictionary<string, SpawnPoint> _points = new(StringComparer.Ordinal);
    readonly HashSet<string> _killed = new(StringComparer.Ordinal);
    readonly Dictionary<NpcCharacter, string> _slotsByActor = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<string, NpcCharacter> _actorsBySlot = new(StringComparer.Ordinal);
    readonly bool _hasStoredCensus;
    QuestDungeonPlayfield? _world;
    IZoneLogger? _logger;
    bool _sealed;
    bool _excludeObjectiveFromChance;

    public bool RequiresReconstruction { get; private set; }
    internal bool HasStoredCensus => _hasStoredCensus;

    sealed class PopulationBranch
    {
        public PopulationBranch() { }

        public string Point { get; set; } = string.Empty;
        public int Ordinal { get; set; }
        public string FamilyHash { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public int Level { get; set; }

        public string Slot => Point + ":" + Ordinal.ToString(CultureInfo.InvariantCulture);
    }

    internal sealed class SpawnPoint
    {
        public required string Key { get; init; }
        public required string Hash { get; init; }
        public required MobTemplate[] Templates { get; init; }
        public required int Level { get; init; }
    }

    public MissionTokenProgress(IMissionDao dao, int ownerId, string questId, long assignedAtTicks,
        MissionTokenRewardPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(dao);
        ArgumentNullException.ThrowIfNull(policy);
        if (ownerId <= 0 || string.IsNullOrWhiteSpace(questId) || assignedAtTicks <= 0 || !policy.IsValid)
            throw new ArgumentException("Mission token progress requires an exact accepted mission and policy.");
        _dao = dao;
        _ownerId = ownerId;
        _questId = questId;
        _assignedAtTicks = assignedAtTicks;
        _policy = policy;

        MissionCharacterSnapshotData snapshot = _dao.ReadCharacter(ownerId);
        string prefix = RunPrefix(assignedAtTicks);
        _hasStoredCensus = snapshot.Flags.Any(flag => flag.QuestId == questId && flag.FlagKey == prefix + "sealed");
        var storedBranches = new Dictionary<string, PopulationBranch>(StringComparer.Ordinal);
        foreach (MissionFlagData flag in snapshot.Flags.Where(flag => flag.QuestId == questId))
        {
            if (flag.FlagKey.StartsWith(prefix + "spawn:", StringComparison.Ordinal))
            {
                PopulationBranch? branch = JsonSerializer.Deserialize<PopulationBranch>(flag.Value ?? string.Empty);
                if (branch == null || string.IsNullOrEmpty(branch.Point) || branch.Ordinal < 0 || branch.Level <= 0
                    || string.IsNullOrEmpty(branch.Hash) || string.IsNullOrEmpty(branch.FamilyHash)
                    || flag.FlagKey != prefix + "spawn:" + branch.Slot)
                    throw new InvalidDataException("The accepted mission population has an invalid branch.");
                storedBranches.Add(branch.Slot, branch);
            }
            else if (flag.FlagKey.StartsWith(prefix + "dead:", StringComparison.Ordinal) && flag.Value == "1")
            {
                _killed.Add(flag.FlagKey[(prefix.Length + "dead:".Length)..]);
            }
        }
        if (!_hasStoredCensus && (storedBranches.Count != 0 || _killed.Count != 0))
            throw new InvalidDataException("The accepted mission population was not sealed.");
        if (_killed.Any(slot => !storedBranches.ContainsKey(slot)))
            throw new InvalidDataException("The accepted mission has a kill outside its frozen population.");
        if (_hasStoredCensus)
        {
            MissionObjectiveProgressData? progress = snapshot.Objectives.FirstOrDefault(row =>
                row.QuestId == questId && row.ObjectiveId == prefix + "population");
            if (progress == null || progress.RequiredCount != storedBranches.Count || progress.Progress != _killed.Count)
                throw new InvalidDataException("The accepted mission kill counters do not match its frozen population and kill markers.");
        }
        _storedPoints = storedBranches.Values.GroupBy(branch => branch.Point, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(branch => branch.Ordinal).ToArray(), StringComparer.Ordinal);
    }

    internal void Attach(QuestDungeonPlayfield world, IZoneLogger logger)
    {
        if (_world != null || world.QuestId != _questId)
            throw new InvalidOperationException("Mission token progress already belongs to a dungeon.");
        _world = world;
        _logger = logger;
    }

    internal SpawnPoint? ResolvePoint(string key, string familyHash, int level, IGameData gameData,
        NpcTemplateCatalog? retainedTemplates = null, int? selectionSeed = null)
    {
        if (_sealed)
            throw new InvalidOperationException("The mission population is already sealed.");
        if (_points.TryGetValue(key, out SpawnPoint? existing))
            return existing;

        MobTemplate[] templates;
        if (_hasStoredCensus)
        {
            if (!_storedPoints.TryGetValue(key, out PopulationBranch[]? branches))
                return null;
            familyHash = branches[0].FamilyHash;
            level = branches[0].Level;
            templates = new MobTemplate[branches.Length];
            for (int i = 0; i < branches.Length; i++)
            {
                PopulationBranch branch = branches[i];
                bool resolved = retainedTemplates == null
                    ? gameData.TryResolveMobTemplate(branch.Hash, level, out MobTemplate template)
                    : retainedTemplates.TryResolve(branch.Hash, level, out template);
                if (branch.Ordinal != i || branch.Level != level || branch.FamilyHash != familyHash || !resolved
                    || template.Hash != branch.Hash || template.UnresolvedPlaceholder || !NpcTemplateValidation.CanSpawn(template))
                    throw new InvalidDataException("A frozen mission NPC branch is unavailable.");
                templates[i] = template;
            }
        }
        else
        {
            var resolved = new List<MobTemplate>();
            if (retainedTemplates == null)
            {
                gameData.CollectMobSpawns(familyHash, level, resolved);
            }
            else
            {
                retainedTemplates.CollectSpawns(familyHash, level, resolved, selectionSeed is int seed ? new Random(seed) : null);
            }
            templates = resolved.Where(template => !template.UnresolvedPlaceholder && NpcTemplateValidation.CanSpawn(template)).ToArray();
            if (templates.Length == 0)
                return null;
        }

        var point = new SpawnPoint { Key = key, Hash = familyHash, Level = level, Templates = templates };
        _points.Add(key, point);
        return point;
    }

    internal void RegisterPoint(SpawnPoint point)
    {
        for (int i = 0; i < point.Templates.Length; i++)
        {
            var branch = new PopulationBranch
            {
                Point = point.Key, Ordinal = i, FamilyHash = point.Hash, Hash = point.Templates[i].Hash, Level = point.Level
            };
            _census.Add(branch.Slot, branch);
        }
    }

    internal void RegisterObjective(MissionNpcObjectiveBinding binding, IGameData gameData, NpcTemplateCatalog? retainedTemplates = null)
    {
        if (!binding.IsValid || binding.OwnerId != _ownerId || binding.QuestId != _questId)
            throw new InvalidDataException("The mission population's objective binding is invalid.");
        SpawnPoint? point = ResolvePoint("objective", binding.NpcHash, binding.Quality, gameData, retainedTemplates);
        if (point == null || point.Templates.Length != 1 || point.Templates[0].Hash != binding.NpcHash
            || point.Level != binding.Quality)
            throw new InvalidDataException("The exact accepted mission objective NPC is unavailable.");
        RegisterPoint(point);
        _excludeObjectiveFromChance = binding.MissionType == 1;
    }

    internal bool IsKilled(string point, int branch) => _killed.Contains(Slot(point, branch));

    internal void BindActor(string point, NpcCharacter actor, int branch)
    {
        string slot = Slot(point, branch);
        if (!_census.ContainsKey(slot) || _killed.Contains(slot) || !ReferenceEquals(actor.Playfield, _world))
            throw new InvalidOperationException("An NPC does not belong to an available mission population slot.");
        if (_actorsBySlot.TryGetValue(slot, out NpcCharacter? previous))
        {
            _slotsByActor.Remove(previous);
        }
        _actorsBySlot[slot] = actor;
        _slotsByActor[actor] = slot;
    }

    /// <summary>Persist the complete census before the playfield starts its heartbeat or admits a player.</summary>
    internal void Seal()
    {
        if (_sealed)
            throw new InvalidOperationException("The mission population is already sealed.");
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(_census.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value)))));
        _dao.Execute(_ownerId, tx =>
        {
            CharacterQuestCompletionSnapshotData quest = RequireQuest(tx);
            var key = new MissionKeyData(_ownerId, _questId);
            string prefix = RunPrefix(_assignedAtTicks);
            MissionFlagData? sealedFlag = tx.GetFlag(key, prefix + "sealed");
            if (sealedFlag != null)
            {
                (int killed, int total) = ReadCounts(tx, _questId, _assignedAtTicks);
                if (sealedFlag.Value != fingerprint || total != _census.Count)
                    throw new InvalidDataException("The rebuilt mission population differs from its frozen census.");
                if (killed != _killed.Count)
                    throw new InvalidDataException("The accepted mission recorded a kill during population reconstruction. Retry entry to restore current progress.");
                return true;
            }
            if (quest.State != (int)QuestState.Active || quest.Progress >= quest.RequiredCount || quest.ExpiresAtUtcTicks <= DateTime.UtcNow.Ticks)
                throw new InvalidOperationException("The mission can no longer initialize token progress.");
            long now = DateTime.UtcNow.Ticks;
            foreach ((string slot, PopulationBranch branch) in _census)
            {
                SaveFlag(tx, key, prefix + "spawn:" + slot, JsonSerializer.Serialize(branch), now);
            }
            tx.SaveObjective(new MissionObjectiveKeyData(key, prefix + "population"), new MissionObjectiveProgressData
            {
                CharacterId = _ownerId, QuestId = _questId, ObjectiveId = prefix + "population",
                Progress = 0, RequiredCount = _census.Count, CreatedAtUtcTicks = now, UpdatedAtUtcTicks = now
            });
            SaveFlag(tx, key, prefix + "sealed", fingerprint, now);
            return true;
        });
        _sealed = true;
    }

    public bool RecordKill(NpcCharacter npc, IReadOnlyList<Player> recipients)
    {
        if (RequiresReconstruction)
            return false;
        if (!_sealed || _world == null || !npc.IsDead || !ReferenceEquals(npc.Playfield, _world)
            || !_slotsByActor.TryGetValue(npc, out string? slot))
            return true;
        var registry = _world.GetRequiredService<DynelRegistry>();
        if (!registry.TryGet(npc.Identity, out Dynel? registered) || !ReferenceEquals(registered, npc))
            return true;
        Player? owner = recipients.FirstOrDefault(player => player.Identity.Instance == _ownerId
            && !player.IsDead && ReferenceEquals(player.Playfield, _world));
        if (owner == null || owner.Session?.State != SessionState.InPlay || owner.IsPersistenceQuarantined
            || !registry.TryGet(owner.Identity, out Dynel? registeredOwner) || !ReferenceEquals(registeredOwner, owner))
            return true;

        lock (owner.PersistenceGate)
        {
            bool added;
            int killed;
            int total;
            try
            {
                (added, killed, total) = _dao.Execute(_ownerId, tx =>
                {
                    CharacterQuestCompletionSnapshotData quest = RequireQuest(tx);
                    (int previous, int censusTotal) = ReadCounts(tx, _questId, _assignedAtTicks);
                    if (quest.State != (int)QuestState.Active || quest.Progress >= quest.RequiredCount || quest.ExpiresAtUtcTicks <= DateTime.UtcNow.Ticks)
                        return (false, previous, censusTotal);
                    string prefix = RunPrefix(_assignedAtTicks);
                    var key = new MissionKeyData(_ownerId, _questId);
                    if (tx.GetFlag(key, prefix + "dead:" + slot) != null)
                        return (false, previous, censusTotal);
                    if (tx.GetFlag(key, prefix + "spawn:" + slot) == null || previous >= censusTotal)
                        throw new InvalidDataException("The killed NPC is outside the sealed mission population.");
                    long now = DateTime.UtcNow.Ticks;
                    bool observed = tx.TryAddObservation(new MissionObjectiveObservationData
                    {
                        CharacterId = _ownerId, QuestId = _questId, ObjectiveId = prefix + "population", ObservationKey = slot,
                        EventType = "MissionNpcKilled", SourceIdentity = owner.Identity.Instance.ToString(CultureInfo.InvariantCulture),
                        TargetIdentity = npc.Identity.Instance.ToString(CultureInfo.InvariantCulture), ObservedAtUtcTicks = now
                    });
                    if (!observed)
                        throw new InvalidDataException("A mission kill observation has no matching durable kill marker.");
                    MissionObjectiveKeyData objectiveKey = new(key, prefix + "population");
                    MissionObjectiveProgressData progress = tx.GetObjective(objectiveKey);
                    progress.Progress++;
                    progress.LastObservationKey = slot;
                    progress.UpdatedAtUtcTicks = now;
                    tx.SaveObjective(objectiveKey, progress);
                    SaveFlag(tx, key, prefix + "dead:" + slot, "1", now);
                    return (true, progress.Progress, progress.RequiredCount);
                });
            }
            catch (Exception exception)
            {
                // A killed actor cannot safely retry an unknown transaction outcome or complete from stale odds.
                RequiresReconstruction = true;
                owner.QuarantinePersistence();
                owner.Session?.Close();
                _logger?.Error(exception, "Mission kill progress could not be reconciled; reconnect required for " + _questId);
                return false;
            }
            if (!added)
                return true;
            _killed.Add(slot);
            _slotsByActor.Remove(npc);
            _actorsBySlot.Remove(slot);
            (int eligibleKilled, int eligibleTotal) = EligibleCounts(killed, total, _excludeObjectiveFromChance, _killed.Contains(ObjectiveSlot));
            _logger?.Info("Mission token progress quest=" + _questId + " killed=" + killed + " total=" + total
                + " eligibleKilled=" + eligibleKilled + " eligibleTotal=" + eligibleTotal);
            if (!(_excludeObjectiveFromChance && slot == ObjectiveSlot) && eligibleKilled >= _policy.FeedbackStartingKill
                && eligibleTotal > 0 && _policy.SidedItems.ContainsKey(owner.Stats.GetOrZero(CharacterStat.Side)))
            {
                try
                {
                    ClientFeedback.SendFormatted(owner, _policy.FeedbackMessageId, (float)(100.0 * eligibleKilled / eligibleTotal));
                }
                catch (Exception exception)
                {
                    _logger?.Error(exception, "Mission token feedback failed after durable progress for " + _questId);
                }
            }
            return true;
        }
    }

    public static (int Killed, int Total) ReadCounts(IMissionDaoTransaction tx, string questId, long assignedAtTicks)
    {
        var key = new MissionKeyData(tx.CharacterId, questId);
        string prefix = RunPrefix(assignedAtTicks);
        MissionObjectiveProgressData? progress = tx.GetObjective(new MissionObjectiveKeyData(key, prefix + "population"));
        if (tx.GetFlag(key, prefix + "sealed") == null || progress == null || progress.Progress < 0
            || progress.RequiredCount < 0 || progress.Progress > progress.RequiredCount)
            throw new InvalidDataException("The mission has no valid sealed token population.");
        return (progress.Progress, progress.RequiredCount);
    }

    /// <summary>Finding a person leaves that objective alive; it is not an enemy required for full clearance.</summary>
    public static (int Killed, int Total) ReadEligibleCounts(IMissionDaoTransaction tx, string questId, long assignedAtTicks,
        QuestDungeonParameters parameters)
    {
        (int killed, int total) = ReadCounts(tx, questId, assignedAtTicks);
        if (parameters.MissionType != 1 || parameters.ObjectiveNpc is not { MissionType: 1 } binding)
            return (killed, total);
        if (!binding.IsValid || binding.OwnerId != tx.CharacterId || binding.QuestId != questId)
            throw new InvalidDataException("The Find Person token objective binding is invalid.");
        MissionFlagData? flag = tx.GetFlag(new MissionKeyData(tx.CharacterId, questId),
            RunPrefix(assignedAtTicks) + "spawn:" + ObjectiveSlot);
        PopulationBranch? objective = flag == null ? null : JsonSerializer.Deserialize<PopulationBranch>(flag.Value ?? string.Empty);
        if (objective == null || objective.Slot != ObjectiveSlot || objective.Hash != binding.NpcHash || objective.Level != binding.Quality)
            throw new InvalidDataException("The Find Person objective is absent from the frozen population.");
        return EligibleCounts(killed, total, excludeObjective: true, IsObjectiveKilled(tx, questId, assignedAtTicks));
    }

    static (int Killed, int Total) EligibleCounts(int killed, int total, bool excludeObjective, bool objectiveKilled)
    {
        if (excludeObjective)
        {
            total--;
            if (objectiveKilled)
            {
                killed--;
            }
        }
        if (killed < 0 || total < 0 || killed > total)
            throw new InvalidDataException("The mission token eligibility counts are invalid.");
        return (killed, total);
    }

    public static bool IsObjectiveKilled(IMissionDaoTransaction tx, string questId, long assignedAtTicks)
        => tx.GetFlag(new MissionKeyData(tx.CharacterId, questId), RunPrefix(assignedAtTicks) + "dead:" + ObjectiveSlot)?.Value == "1";

    CharacterQuestCompletionSnapshotData RequireQuest(IMissionDaoTransaction tx)
    {
        if (tx is not ICharacterQuestCompletionTransaction completion)
            throw new InvalidOperationException("The mission DAO cannot validate accepted quest ownership.");
        CharacterQuestCompletionSnapshotData? quest = completion.ReadCharacterQuest(_questId);
        if (quest == null || quest.CharacterId != _ownerId || quest.Source != (int)QuestSource.Generated || quest.OwnerType != (int)QuestOwnerType.Character
            || quest.OwnerId != _ownerId || quest.AssignedAtUtcTicks != _assignedAtTicks)
            throw new InvalidOperationException("The accepted mission ownership changed.");
        return quest;
    }

    static void SaveFlag(IMissionDaoTransaction tx, MissionKeyData key, string flagKey, string value, long now)
        => tx.SaveFlag(key, new MissionFlagData
        {
            CharacterId = key.CharacterId, QuestId = key.QuestId, FlagKey = flagKey, Value = value,
            CreatedAtUtcTicks = now, UpdatedAtUtcTicks = now
        });

    static string RunPrefix(long assignedAtTicks) => "mission-token:" + assignedAtTicks.ToString(CultureInfo.InvariantCulture) + ":";
    static string Slot(string point, int branch) => point + ":" + branch.ToString(CultureInfo.InvariantCulture);
}
