namespace ZoneEngine_New.Core.Quests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Helpers;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Quests.Dungeons;
using ZoneEngine_New.Core.Playfield;

public sealed partial class QuestService
{
    static bool RequiresBoundObjective(PlayerQuest quest) => MissionObjectiveModule.RequiresBoundObjective(quest);

    internal bool CanProgressObjective(Player player) => _completionDao != null && CanDeliver(player);

    internal bool TryEarnBoundObjective(Player player, PlayerQuest quest, QuestDungeonParameters parameters, MissionItemInstanceData? removal)
    {
        if (!Monitor.IsEntered(player.PersistenceGate))
            throw new InvalidOperationException("Mission objective earning requires the player's persistence gate.");
        bool committed = false;
        try
        {
            _flush.HardFlush(player);
            var result = _completionDao!.Execute(player.Identity.Instance, null!, tx =>
            {
                var completion = RequireCompletion(tx);
                var stored = completion.ReadCharacterQuest(quest.QuestId);
                RequireCurrent(stored, quest, player);
                if (stored.State != (int)QuestState.Active || stored.Progress >= stored.RequiredCount
                    || !QuestDungeonParameters.TryParse(stored.AcgBuildingGeneratorJson, out var durable)
                    || durable.ToJson() != parameters.ToJson()
                    || quest.Template.IsReturnItem && stored.Progress != 1)
                    throw new InvalidOperationException("Mission objective is no longer collectible.");
                return EarnWithToken(tx, completion, player, quest, new()
                {
                    QuestId = quest.QuestId, AssignedAtUtcTicks = stored.AssignedAtUtcTicks,
                    ExpectedDefinitionJson = stored.DefinitionJson,
                    ExpectedAcgBuildingGeneratorJson = stored.AcgBuildingGeneratorJson,
                    NowUtcTicks = _now().Ticks, Removals = removal == null ? [] : [removal]
                });
            });
            if (result.Status != CharacterQuestCompletionStatus.Earned)
                return false;
            committed = true;
            quest.Progress = quest.RequiredCount;
            quest.NpcObjectiveCommitPending = false;
            quest.NextRewardRetryUtcTicks = 0;
            return true;
        }
        catch (Exception exception)
        {
            CompletionFailure(player, exception, committed);
            return false;
        }
    }

    internal bool TryCommitObjectivePickup(Player player, PlayerQuest quest, QuestDungeonPlayfield world,
        MissionItemObjectiveBinding binding, InventoryGrantPlan grant)
    {
        if (!Monitor.IsEntered(player.PersistenceGate))
            throw new InvalidOperationException("Mission objective collection requires the player's persistence gate.");
        bool isReturn = quest.Template.IsReturnItem;
        _flush.HardFlush(player);
        var result = _completionDao!.Execute(player.Identity.Instance, null!, tx =>
        {
            var completion = RequireCompletion(tx);
            var stored = completion.ReadCharacterQuest(quest.QuestId);
            RequireCurrent(stored, quest, player);
            if (stored.Source != (int)QuestSource.Generated || stored.OwnerId != player.Identity.Instance
                || !QuestDungeonParameters.TryParse(stored.AcgBuildingGeneratorJson, out var durable)
                || durable.ObjectiveItem?.SameAs(binding) != true
                || durable.DungeonPlayfield != world.Identity.Instance)
                throw new InvalidOperationException("Objective does not match the accepted mission.");
            var request = new CharacterQuestEarnRequest
            {
                QuestId = quest.QuestId, AssignedAtUtcTicks = stored.AssignedAtUtcTicks,
                ExpectedDefinitionJson = stored.DefinitionJson,
                ExpectedAcgBuildingGeneratorJson = stored.AcgBuildingGeneratorJson,
                NowUtcTicks = _now().Ticks, Grants = grant.Rows.Select(ToMissionItem).ToArray()
            };
            return isReturn
                ? (tx as ICharacterQuestObjectiveCollectionTransaction
                    ?? throw new InvalidOperationException("Mission DAO cannot persist partial objective collection."))
                    .TryCollectCharacterQuestItem(request)
                : EarnWithToken(tx, completion, player, quest, request);
        });
        return result.Status == (isReturn ? CharacterQuestCompletionStatus.Collected : CharacterQuestCompletionStatus.Earned);
    }

    internal bool TryCollectFindItem(Player player, MissionObjectiveItem objective)
        => _missionObjectives.TryCollectItem(player, objective);

    public void RetryPendingRewards(Player player, bool force = false)
    {
        if (!CanDeliver(player) || _completionDao == null)
            return;
        var log = player.QuestLog ??= Load(player);
        long now = _now().Ticks;
        foreach (var quest in log.Quests.Values.Where(q => q.IsActive && q.NpcObjectiveCommitPending
            && (force || q.NextRewardRetryUtcTicks <= now)).ToArray())
            _missionObjectives.RetryPendingKill(player, quest);
        foreach (var quest in log.Quests.Values.Where(q => q.IsRewardPending
            && (force || q.NextRewardRetryUtcTicks <= now)).ToArray())
            Complete(player, quest);
    }

    static bool CanDeliver(Player player) => !player.IsDead && !player.IsPersistenceQuarantined
        && player.Inventory.IsHydrated && player.Playfield != null
        && player.Session is { State: SessionState.InPlay } session && ReferenceEquals(session.Player, player);

    void Complete(Player player, PlayerQuest quest)
    {
        lock (player.PersistenceGate)
        {
            if (!CanDeliver(player) || _completionDao == null || !quest.IsActive)
                return;
            quest.NextRewardRetryUtcTicks = _now().AddSeconds(5).Ticks;
            bool committed = false;
            try
            {
                _flush.HardFlush(player);
                // Earning is durable even when capacity, content availability or delivery fails later.
                FrozenMissionTokenReward? tokenReward = null;
                var earned = _completionDao.Execute(player.Identity.Instance, null!, tx =>
                {
                    var completion = RequireCompletion(tx);
                    var stored = completion.ReadCharacterQuest(quest.QuestId);
                    RequireCurrent(stored, quest, player);
                    if (RequiresBoundObjective(quest) && stored.Progress < stored.RequiredCount)
                        throw new InvalidOperationException("Mission completion requires its committed exact objective event.");
                    var result = EarnWithToken(tx, completion, player, quest, new()
                    {
                        QuestId = quest.QuestId, AssignedAtUtcTicks = stored.AssignedAtUtcTicks,
                        ExpectedDefinitionJson = stored.DefinitionJson,
                        ExpectedAcgBuildingGeneratorJson = stored.AcgBuildingGeneratorJson, NowUtcTicks = _now().Ticks
                    });
                    if (result.Status is CharacterQuestCompletionStatus.Earned or CharacterQuestCompletionStatus.AlreadyEarned)
                    {
                        tokenReward = ReadTokenReward(tx, player, quest, result.Quest);
                    }
                    return result;
                });
                if (earned.Status == CharacterQuestCompletionStatus.AlreadyApplied)
                {
                    // A different/uncertain publication already paid; never overwrite its durable stats with this actor.
                    player.QuarantinePersistence();
                    player.Session?.Close();
                    return;
                }
                if (earned.Status is not (CharacterQuestCompletionStatus.Earned or CharacterQuestCompletionStatus.AlreadyEarned))
                    return;
                quest.Progress = quest.RequiredCount;
                if (_snapshot == null)
                    throw new InvalidOperationException("Character snapshot service is unavailable; reward remains pending.");
                _snapshot.CommitOnline(player);
                QuestTemplate rewardTemplate = quest.Source == QuestSource.Generated
                    ? JsonSerializer.Deserialize<QuestTemplate>(earned.Quest.DefinitionJson, QuestTemplate.JsonOptions)
                        ?? throw new InvalidOperationException("Stored reward definition is unavailable.")
                    : quest.Template;
                var reward = rewardTemplate.Reward;
                var definitions = RewardItemsFor(player, quest, reward).ToList();
                if (tokenReward?.Won == true)
                {
                    definitions.Add(new QuestRewardItem { Id = tokenReward.ItemId, HighId = tokenReward.ItemId, Quality = tokenReward.Quality });
                }
                var items = new List<Item>();
                var skippedUnique = new List<string>();
                var used = new List<Item>();
                foreach (var definition in definitions)
                {
                    if (definition.Id <= 0 || definition.HighId is <= 0 || definition.Quality <= 0)
                        throw new InvalidOperationException("Reward item definition is invalid; delivery remains pending.");
                    int highId = definition.HighId ?? definition.Id;
                    // A Unique reward the character already holds is left out: the quest still completes on its dialog
                    // and pays the rest, rather than staying open until the held copy is gone. Checked on the template,
                    // so no item is minted for it.
                    if (IsUniqueTemplate(definition.Id) && (ZoneEngine_New.Core.Trade.TradeRules.WouldDuplicateUnique(player, definition.Id, highId)
                        || items.Any(existing => existing.LowId == definition.Id || existing.HighId == highId)))
                    {
                        skippedUnique.Add(string.IsNullOrEmpty(definition.Name) ? definition.Id.ToString(CultureInfo.InvariantCulture) : definition.Name);
                        continue;
                    }
                    // Live's virtual reward items (Daily Mission XP Reward, Token Add, Set Flag items) carry the Null
                    // flag: they are used on the player, never placed in the inventory, so no instance is minted.
                    if (IsUsedOnReward(definition.Id, highId, definition.Quality))
                    {
                        used.Add(_items.Create(definition.Id, highId, definition.Quality, ItemSource.Quest));
                        continue;
                    }
                    Item minted = _items.CreateWithNewInstance(definition.Id, highId, definition.Quality, ItemSource.Quest);
                    // A configured Count replaces the template's mint count, as a daily login reward's does.
                    if (definition.Count is int count)
                        minted.StackCount = count;
                    items.Add(minted);
                }
                if (skippedUnique.Count > 0)
                    _logger.Info("Quest reward unique items already held, left out char=" + player.Identity.Instance
                        + " quest=" + quest.QuestId + " items=" + string.Join(", ", skippedUnique));
                if (!InventoryGrantPlan.TryCreate(player, items, out var grant, allowOverflow: true))
                {
                    NotifyPending(player, quest);
                    return;
                }
                var xp = DirectXpRewardPlan.CreateQuestReward(player, Math.Max(0, reward?.Xp ?? 0));
                // Runtime vitals are published by the full XP plan; only durable stats belong in the reward transaction.
                var values = xp.Stats.Where(pair => !StatCollection.IsRuntimeOnly(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                if (reward?.Credits > 0)
                    values[CharacterStat.Cash] = Characters.CashLimit.Clamp(
                        (long)player.Stats.GetOrZero(CharacterStat.Cash, StatDetail.Base) + reward.Credits);
                if (rewardTemplate.MissionBit is int bit && MissionBits.IsValid(bit))
                {
                    var (stat, mask) = MissionBits.Locate(bit);
                    values[stat] = player.Stats.GetOrZero(stat, StatDetail.Base) | mask;
                }
                var expected = values.Keys.Select(stat => new MissionStatValueData
                {
                    StatIdentityType = (int)player.Identity.Type, StatId = (int)stat,
                    Value = player.Stats.GetOrZero(stat, StatDetail.Base)
                }).ToArray();
                var result = _completionDao.Execute(player.Identity.Instance, null!, tx => RequireCompletion(tx).TryCompleteCharacterQuest(new()
                {
                    QuestId = quest.QuestId, AssignedAtUtcTicks = earned.Quest.AssignedAtUtcTicks,
                    ExpectedDefinitionJson = earned.Quest.DefinitionJson,
                    ExpectedAcgBuildingGeneratorJson = earned.Quest.AcgBuildingGeneratorJson, NowUtcTicks = _now().Ticks,
                    Grants = grant.Rows.Select(ToMissionItem).ToArray(), ExpectedStatValues = expected,
                    StatMutations = values.Select(pair => new MissionStatMutationData
                    {
                        StatIdentityType = (int)player.Identity.Type, StatId = (int)pair.Key,
                        Kind = MissionStatMutationKind.Set, Value = pair.Value,
                        MinimumValue = int.MinValue, MaximumValue = int.MaxValue
                    }).ToArray()
                }));
                if (result.Status != CharacterQuestCompletionStatus.Applied)
                {
                    _logger.Warn("Quest reward delivery not applied char=" + player.Identity.Instance
                        + " quest=" + quest.QuestId + " status=" + result.Status + " reason=" + result.Reason);
                    if (result.Status == CharacterQuestCompletionStatus.AlreadyApplied)
                    {
                        player.QuarantinePersistence();
                        player.Session?.Close();
                    }
                    return;
                }
                committed = true;
                // Retire any queued pre-reward checkpoint before publishing, so it cannot overwrite paid stats.
                player.SaveState.MarkCommitted(player.SaveState.NextSequence(), values.Select(pair => new StatRecord
                {
                    StatId = (int)pair.Key, StatValue = pair.Value
                }).ToArray(), location: null);
                quest.State = QuestState.Completed;
                // Local playtest: AddTemplate fills the first free client slot, so publish before key deletion opens a hole.
                try
                {
                    grant.PublishAfterCommit();
                }
                finally
                {
                    // Durable lifecycle cleanup must still run if reward publication fails.
                    QuestEnded?.Invoke(player, quest);
                }
                xp.PublishAfterCommit(player);
                foreach (var pair in values.Where(pair => !xp.Stats.ContainsKey(pair.Key)))
                    player.Stats.Set(pair.Key, pair.Value, StatDetail.Base, dirty: true);
                player.FlushDirtyStats();
                if (used.Count > 0 && player.Playfield?.GetService<IInventoryRepository>() is IInventoryRepository inventory)
                    foreach (Item item in used)
                        item.Definition.ExecuteOnUseSpells(player, inventory, _items, source: player);
                QuestJournal.SendEnded(player, quest, CharacterActionType.MissionAccomplished);
                QuestJournal.Delete(player, quest);
                _logger.Info("Quest completed and rewards committed char=" + player.Identity.Instance + " quest=" + quest.QuestId
                    + (tokenReward == null ? string.Empty : " tokenWon=" + tokenReward.Won + " tokenItem=" + tokenReward.ItemId
                        + " tokenKills=" + tokenReward.Killed + "/" + tokenReward.Total));
                if (!string.IsNullOrEmpty(rewardTemplate.NextStep) && _catalog.TryGet(rewardTemplate.NextStep, out _))
                    TryAssignTemplate(player, rewardTemplate.NextStep, out _);
            }
            catch (Exception exception)
            {
                CompletionFailure(player, exception, committed);
                if (quest.IsRewardPending && !player.IsPersistenceQuarantined)
                    NotifyPending(player, quest);
            }
        }
    }

    /// <summary>The reward template carries the Unique flag (read from the template, nothing minted).</summary>
    bool IsUniqueTemplate(int templateId)
    {
        try
        {
            return (_items.CreateTemplate(templateId, templateId, 1).Flags & (int)ItemFlags.Unique) != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// The items a quest pays this player now. A Quests.json quest's come only from QuestRewards.json, by its reward
    /// hash: none when the hash has no entry or no entry matches. A generated quest's are the record stored with it.
    /// </summary>
    public IReadOnlyList<QuestRewardItem> RewardItemsFor(Player player, PlayerQuest quest, QuestReward? reward = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(quest);
        reward ??= quest.Template.Reward;
        if (quest.Source == QuestSource.Generated)
            return SelectRewardRecord(quest, reward);

        return _rewards != null && _rewards.TryResolve(reward?.Hash, player, quest, out IReadOnlyList<QuestRewardItem> mapped)
            ? mapped
            : [];
    }

    /// <summary>A generated quest's stored record; records are alternatives, one chosen per accepted run.</summary>
    static IReadOnlyList<QuestRewardItem> SelectRewardRecord(PlayerQuest quest, QuestReward? reward)
    {
        if (reward?.Records is not { Count: > 0 }) return [];
        return reward.Records[QuestRewardTable.StableIndex(quest, reward.Records.Count)];
    }

    /// <summary>A reward item live uses on the player instead of handing over: its template has the Null flag.</summary>
    bool IsUsedOnReward(int lowId, int highId, int quality)
    {
        try
        {
            return (_items.CreateTemplate(lowId, highId, quality).Flags & (int)ItemFlags.Null) != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static ICharacterQuestCompletionTransaction RequireCompletion(IMissionDaoTransaction transaction)
        => transaction as ICharacterQuestCompletionTransaction
            ?? throw new InvalidOperationException("Mission DAO does not support atomic character quest rewards.");

    static void NotifyPending(Player player, PlayerQuest quest, bool uniqueConflict = false)
    {
        if (quest.RewardPendingNotified) return;
        quest.RewardPendingNotified = true;
        ClientFeedback.SendFormatted(player, ClientFeedback.PlainText, uniqueConflict
            ? "Mission objective complete. Your reward is saved. Use or remove the matching Unique item you already hold and keep inventory space free; delivery retries automatically and after reconnect."
            : "Mission objective complete. Your reward is saved and pending delivery. Make inventory space; delivery retries automatically and after reconnect.");
    }

    static void RequireCurrent(CharacterQuestCompletionSnapshotData stored, PlayerQuest quest, Player player)
    {
        if (stored == null || stored.CharacterId != player.Identity.Instance || stored.Source != (int)quest.Source
            || stored.AssignedAtUtcTicks != quest.AssignedAtUtc.Ticks
            || (quest.Source == QuestSource.Generated && (stored.OwnerType != (int)QuestOwnerType.Character
                || stored.OwnerId != player.Identity.Instance || stored.AcgBuildingGeneratorJson != quest.AcgBuildingGeneratorJson)))
            throw new InvalidOperationException("Accepted quest authority changed.");
    }

    internal static MissionItemInstanceData ToMissionItem(ItemInstanceRecord row) => new()
    {
        InstanceId = row.InstanceId, ContainerType = row.ContainerType, ContainerInstance = row.ContainerInstance,
        ContainerPlacement = row.ContainerPlacement, ItemType = row.ItemType, LowId = row.LowId, HighId = row.HighId,
        Quality = row.Quality, StackCount = row.StackCount, Source = (byte)row.Source
    };

    internal void CompletionFailure(Player player, Exception exception, bool committed)
    {
        if (committed || exception is MissionCommitOutcomeUnknownException or DatabaseCommitOutcomeUnknownException
            || player.IsPersistenceQuarantined)
        {
            player.QuarantinePersistence();
            player.Session?.Close();
        }
        _logger.Error(exception, "Quest collection/reward delivery failed; no uncommitted success published and earned rewards remain recoverable.");
    }
}
