namespace ZoneEngine_New.Core.Quests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

public sealed partial class QuestService
{
    static bool RequiresBoundObjective(PlayerQuest quest) => quest.Source == QuestSource.Generated
        && (quest.Template.IsFindItem || quest.Template.IsReturnItem || quest.Template.IsRepair
            || QuestDungeonParameters.TryParse(quest.AcgBuildingGeneratorJson, out var parameters)
                && parameters.ObjectiveNpc != null);

    /// <summary>Retail Find Person completes on inspection of the accepted actor, not ordinary targeting.</summary>
    public void OnNpcInspected(Player player, NpcCharacter npc)
    {
        foreach (var quest in GetLog(player).Quests.Values)
            if (quest.IsActive && quest.Template.Action == QuestTemplate.TargetNpcAction && RequiresBoundObjective(quest))
                TryEarnNpcObjective(player, quest, npc, inspected: true);
    }

    void TryEarnNpcObjective(Player player, PlayerQuest quest, NpcCharacter npc, bool inspected)
    {
        lock (player.PersistenceGate)
        {
            if (!CanDeliver(player) || _completionDao == null || !quest.IsActive || quest.IsRewardPending
                || !QuestDungeonParameters.TryParse(quest.AcgBuildingGeneratorJson, out var parameters)
                || parameters.ObjectiveNpc is not { IsValid: true } binding
                || binding.MissionType != (inspected ? 1 : 0) || binding.OwnerId != player.Identity.Instance
                || binding.QuestId != quest.QuestId || npc.Identity != binding.Identity
                || npc.Playfield is not ZoneEngine_New.Core.Playfield.QuestDungeonPlayfield world
                || !ReferenceEquals(player.Playfield, world) || world.Identity.Instance != parameters.DungeonPlayfield
                || world.QuestId != quest.QuestId
                || !world.GetRequiredService<ZoneEngine_New.Core.Playfield.DynelRegistry>().TryGet(npc.Identity, out var registered)
                || !ReferenceEquals(registered, npc)
                || inspected && (player.GetEdgeDistanceTo(npc) > LootableDynel.OpenRange || !player.HasLineOfSightTo(npc)))
                return;
            if (!TryEarnBoundObjective(player, quest, parameters, null) && !inspected && !player.IsPersistenceQuarantined)
            {
                quest.NpcObjectiveCommitPending = true;
                quest.NextRewardRetryUtcTicks = _now().AddSeconds(5).Ticks;
            }
        }
    }

    /// <summary>Claims mission-owned item interactions before generic item spells can handle them.</summary>
    public bool TryUseGeneratedMissionItem(Player player, Identity sourceSlot, Identity target, out bool handled)
    {
        handled = false;
        lock (player.PersistenceGate)
        {
            // A mission machine never falls through to its ordinary item-template spells,
            // including when a key holder has no ownership of this mission.
            if (player.Playfield is { } currentWorld
                && currentWorld.GetRequiredService<ZoneEngine_New.Core.Playfield.DynelRegistry>()
                    .TryGet(target, out var registeredTarget) && registeredTarget is MissionObjectiveItem)
                handled = true;
            if (!CanDeliver(player) || _completionDao == null)
                return false;
            player.Inventory.TryGetUseItem(sourceSlot, out Item source);
            foreach (var quest in GetLog(player).Quests.Values)
            {
                if (quest.Source != QuestSource.Generated
                    || !QuestDungeonParameters.TryParse(quest.AcgBuildingGeneratorJson, out var parameters))
                    continue;
                MissionItemObjectiveBinding? binding = quest.Template.IsRepair ? parameters.ObjectiveTool
                    : quest.Template.IsReturnItem ? parameters.ObjectiveItem : null;
                bool ownsTarget = quest.Template.IsRepair && parameters.ObjectiveItem?.Identity == target;
                if (binding?.IsValid != true || (source?.Identity != binding.Identity && !ownsTarget))
                    continue;
                handled = true;
                if (!quest.IsActive || quest.IsRewardPending || source == null || source.Locked || !source.IsPersisted
                    || binding.OwnerId != player.Identity.Instance || binding.QuestId != quest.QuestId
                    || source.Identity != binding.Identity || source.InstanceId != binding.Instance
                    || source.LowId != binding.LowId || source.HighId != binding.HighId || source.Quality != binding.Quality
                    || source.StackCount != 1)
                    return false;
                var world = player.Playfield!;
                if (!world.GetRequiredService<ZoneEngine_New.Core.Playfield.DynelRegistry>().TryGet(target, out var destination))
                    return false;
                if (quest.Template.IsRepair)
                {
                    if (world is not ZoneEngine_New.Core.Playfield.QuestDungeonPlayfield dungeon
                        || dungeon.QuestId != quest.QuestId || dungeon.Identity.Instance != parameters.DungeonPlayfield
                        || destination is not MissionObjectiveItem machine
                        || parameters.ObjectiveItem?.SameAs(machine.Binding) != true
                        || player.GetEdgeDistanceTo(machine) > LootableDynel.OpenRange)
                        return false;
                }
                else if (quest.Progress != 1 || parameters.OriginPlayfield != world.Identity.Instance
                    || parameters.OriginTerminal != target || destination is not MissionTerminal terminal
                    || player.GetEdgeDistanceTo(terminal) > LootableDynel.OpenRange)
                    return false;

                if (sourceSlot.Type != IdentityType.Inventory)
                {
                    ClientFeedback.Send(player, "Move the mission item into your main inventory before using it.");
                    return false;
                }
                Container page = player.Inventory.Inventory;
                int placement = sourceSlot.Instance;
                if (!TryEarnBoundObjective(player, quest, parameters, ToMissionItem(source.ToRecord(page.Identity, placement, source.StackCount))))
                    return false;
                try
                {
                    if (!ReferenceEquals(page.Remove(placement), source))
                        throw new InvalidOperationException("Committed mission source changed before publication.");
                    player.Session?.Send(new TemplateActionMessage
                    {
                        Identity = player.Identity, ItemLowId = source.LowId, ItemHighId = source.HighId,
                        Quality = source.Quality, Placement = sourceSlot, Unknown1 = 1,
                        Action = TemplateActionType.Use, Unknown3 = (int)player.Identity.Type, Unknown4 = player.Identity.Instance
                    });
                    player.Session?.Send(new CharacterActionMessage
                    {
                        Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = sourceSlot
                    });
                    return true;
                }
                catch (Exception exception)
                {
                    CompletionFailure(player, exception, committed: true);
                    return false;
                }
            }
            return false;
        }
    }

    bool TryEarnBoundObjective(Player player, PlayerQuest quest, QuestDungeonParameters parameters, MissionItemInstanceData? removal)
    {
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

    /// <summary>Only the registered owner-bound world objective can call this collection path.</summary>
    internal bool TryCollectFindItem(Player player, MissionObjectiveItem objective)
    {
        lock (player.PersistenceGate)
        {
            if (!CanDeliver(player) || _completionDao == null
                || objective.Playfield is not ZoneEngine_New.Core.Playfield.QuestDungeonPlayfield world
                || !ReferenceEquals(player.Playfield, world)
                || player.Identity.Instance != objective.Binding.OwnerId
                || !GetLog(player).Quests.TryGetValue(objective.Binding.QuestId, out PlayerQuest? quest)
                || !quest.IsActive || quest.IsRewardPending || !(quest.Template.IsFindItem || quest.Template.IsReturnItem)
                || quest.Progress != 0
                || !QuestDungeonParameters.TryParse(quest.AcgBuildingGeneratorJson, out var parameters)
                || parameters.DungeonPlayfield != world.Identity.Instance
                || parameters.ObjectiveItem?.SameAs(objective.Binding) != true
                || player.GetEdgeDistanceTo(objective) > LootableDynel.OpenRange
                || !world.GetRequiredService<ZoneEngine_New.Core.Playfield.DynelRegistry>().TryGet(objective.Identity, out var current)
                || !ReferenceEquals(current, objective))
                return false;

            bool committed = false;
            try
            {
                Item item = _items.Create(objective.LowId, objective.HighId, objective.Quality,
                    ItemSource.Quest, 1, objective.Identity.Instance, objective.Identity);
                if (!InventoryGrantPlan.TryCreate(player, [item], out var grant))
                {
                    ClientFeedback.Send(player, "Make inventory space before picking up the mission item.");
                    return false;
                }
                _flush.HardFlush(player);
                var result = _completionDao.Execute(player.Identity.Instance, null!, tx =>
                {
                    var completion = RequireCompletion(tx);
                    var stored = completion.ReadCharacterQuest(quest.QuestId);
                    RequireCurrent(stored, quest, player);
                    if (stored.Source != (int)QuestSource.Generated || stored.OwnerId != player.Identity.Instance
                        || !QuestDungeonParameters.TryParse(stored.AcgBuildingGeneratorJson, out var durable)
                        || durable.ObjectiveItem?.SameAs(objective.Binding) != true
                        || durable.DungeonPlayfield != world.Identity.Instance)
                        throw new InvalidOperationException("Objective does not match the accepted mission.");
                    var request = new CharacterQuestEarnRequest
                    {
                        QuestId = quest.QuestId, AssignedAtUtcTicks = stored.AssignedAtUtcTicks,
                        ExpectedDefinitionJson = stored.DefinitionJson,
                        ExpectedAcgBuildingGeneratorJson = stored.AcgBuildingGeneratorJson,
                        NowUtcTicks = _now().Ticks, Grants = grant.Rows.Select(ToMissionItem).ToArray()
                    };
                    return quest.Template.IsReturnItem
                        ? (tx as ICharacterQuestObjectiveCollectionTransaction
                            ?? throw new InvalidOperationException("Mission DAO cannot persist partial objective collection."))
                            .TryCollectCharacterQuestItem(request)
                        : EarnWithToken(tx, completion, player, quest, request);
                });
                if (result.Status != (quest.Template.IsReturnItem ? CharacterQuestCompletionStatus.Collected : CharacterQuestCompletionStatus.Earned))
                    return false;
                committed = true;
                grant.PublishAfterCommit(notify: false);
                quest.Progress = quest.Template.IsReturnItem ? 1 : quest.RequiredCount;
                quest.NextRewardRetryUtcTicks = 0;
                // The objective dynel publishes the captured pickup acknowledgement first.
                // The player's next tick then delivers the independent terminal rewards.
                return true;
            }
            catch (Exception exception)
            {
                CompletionFailure(player, exception, committed);
                return false;
            }
        }
    }

    public void RetryPendingRewards(Player player, bool force = false)
    {
        if (!CanDeliver(player) || _completionDao == null)
            return;
        var log = player.QuestLog ??= Load(player);
        long now = _now().Ticks;
        foreach (var quest in log.Quests.Values.Where(q => q.IsActive && q.NpcObjectiveCommitPending
            && (force || q.NextRewardRetryUtcTicks <= now)).ToArray())
        {
            lock (player.PersistenceGate)
            {
                quest.NextRewardRetryUtcTicks = _now().AddSeconds(5).Ticks;
                if (QuestDungeonParameters.TryParse(quest.AcgBuildingGeneratorJson, out var parameters)
                    && parameters.ObjectiveNpc is { IsValid: true, MissionType: 0 })
                    TryEarnBoundObjective(player, quest, parameters, null);
            }
        }
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
            // Template quests carry their level band and cooldown; checked before earning, and the cooldown once more
            // inside the earning transaction. A reward already earned is only delivered, never checked again.
            QuestTemplate? recurring = quest.Source == QuestSource.Template && quest.Template.Recurring != null ? quest.Template : null;
            try
            {
                if (quest.Source == QuestSource.Template && !quest.IsRewardPending && WhyNotEligible(player, quest.Template) is string why)
                {
                    ClientFeedback.SendFormatted(player, ClientFeedback.PlainText, why);
                    return;
                }

                _flush.HardFlush(player);
                // Earning is durable even when capacity, content availability or delivery fails later.
                FrozenMissionTokenReward? tokenReward = null;
                string? account = recurring?.Recurring!.IsPerAccount == true ? Account(player) : null;
                var earned = _completionDao.Execute(player.Identity.Instance, account!, tx =>
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
                    if (result.Status == CharacterQuestCompletionStatus.Earned && recurring != null)
                        StartCooldown(tx, player, recurring);
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
                var definitions = SelectRewardRecord(quest, reward).ToList();
                if (!string.IsNullOrEmpty(rewardTemplate.NextStep) && _catalog.TryGet(rewardTemplate.NextStep, out QuestTemplate nextStep)
                    && nextStep.StartItems is { Count: > 0 } startItems)
                    definitions.AddRange(startItems);
                if (tokenReward?.Won == true)
                {
                    definitions.Add(new QuestRewardItem { Id = tokenReward.ItemId, HighId = tokenReward.ItemId, Quality = tokenReward.Quality });
                }
                var items = new List<Item>();
                foreach (var definition in definitions)
                {
                    if (definition.Id <= 0 || definition.HighId is <= 0 || definition.Quality <= 0)
                        throw new InvalidOperationException("Reward item definition is invalid; delivery remains pending.");
                    var item = _items.CreateWithNewInstance(definition.Id, definition.HighId ?? definition.Id,
                        definition.Quality, ItemSource.Quest);
                    if (ZoneEngine_New.Core.Trade.TradeRules.IsUnique(item) && (ZoneEngine_New.Core.Trade.TradeRules.WouldDuplicateUnique(player, item.LowId, item.HighId)
                        || items.Any(existing => existing.LowId == item.LowId || existing.HighId == item.HighId)))
                    {
                        NotifyPending(player, quest, uniqueConflict: true);
                        return;
                    }
                    items.Add(item);
                }
                if (!InventoryGrantPlan.TryCreate(player, items, out var grant))
                {
                    NotifyPending(player, quest);
                    return;
                }
                int level = player.Stats.GetOrOne(CharacterStat.Level);
                int xpReward = Math.Max(0, reward?.Xp ?? 0)
                    + (reward?.FullLevelXp == true ? ProgressionRewardRules.GetFullRubikaLevelXpReward(level) : 0);
                var xp = DirectXpRewardPlan.CreateQuestReward(player, xpReward);
                // Runtime vitals are published by the full XP plan; only durable stats belong in the reward transaction.
                var values = xp.Stats.Where(pair => !StatCollection.IsRuntimeOnly(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                if (reward?.Credits > 0)
                    values[CharacterStat.Cash] = (int)Math.Min(int.MaxValue,
                        (long)player.Stats.GetOrZero(CharacterStat.Cash, StatDetail.Base) + reward.Credits);
                int sideTokens = 0;
                CharacterStat sideTokenStat = default;
                if (reward?.SideTokens == true
                    && ProgressionRewardRules.TryGetSideTokenStatId(player.Stats.GetOrZero(CharacterStat.Side), out int tokenStatId)
                    && (sideTokens = ProgressionRewardRules.GetSideTokenReward(level, player.Stats.GetOrZero(CharacterStat.Side))) > 0)
                {
                    sideTokenStat = (CharacterStat)tokenStatId;
                    values[sideTokenStat] = (int)Math.Min(int.MaxValue,
                        (long)player.Stats.GetOrZero(sideTokenStat, StatDetail.Base) + sideTokens);
                }
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
                if (sideTokens > 0)
                    ClientFeedback.SendFormatted(player, ClientFeedback.PlainText, string.Format(CultureInfo.InvariantCulture,
                        "Side tokens collected: {0}.", values[sideTokenStat]));
                QuestJournal.Delete(player, quest);
                ClientFeedback.Send(player, "Feedback_MissionAccomplished");
                _logger.Info("Quest completed and rewards committed char=" + player.Identity.Instance + " quest=" + quest.QuestId
                    + (tokenReward == null ? string.Empty : " tokenWon=" + tokenReward.Won + " tokenItem=" + tokenReward.ItemId
                        + " tokenKills=" + tokenReward.Killed + "/" + tokenReward.Total));
                if (!string.IsNullOrEmpty(rewardTemplate.NextStep) && _catalog.TryGet(rewardTemplate.NextStep, out _))
                    TryAssignTemplate(player, rewardTemplate.NextStep, out _);
            }
            catch (QuestCooldownActiveException cooldown)
            {
                // Another character of the account finished it first: nothing was earned, the quest stays active.
                ClientFeedback.SendFormatted(player, ClientFeedback.PlainText, cooldown.Message);
            }
            catch (Exception exception)
            {
                CompletionFailure(player, exception, committed);
                if (quest.IsRewardPending && !player.IsPersistenceQuarantined)
                    NotifyPending(player, quest);
            }
        }
    }

    static IReadOnlyList<QuestRewardItem> SelectRewardRecord(PlayerQuest quest, QuestReward? reward)
    {
        if (reward?.Records is not { Count: > 0 }) return [];
        // Stable per accepted run: reconnects and failed deliveries cannot reroll an alternative.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(quest.QuestId + ":" + quest.AssignedAtUtc.Ticks));
        int index = (int)(BitConverter.ToUInt32(hash, 0) % (uint)reward.Records.Count);
        return reward.Records[index];
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

    static MissionItemInstanceData ToMissionItem(ItemInstanceRecord row) => new()
    {
        InstanceId = row.InstanceId, ContainerType = row.ContainerType, ContainerInstance = row.ContainerInstance,
        ContainerPlacement = row.ContainerPlacement, ItemType = row.ItemType, LowId = row.LowId, HighId = row.HighId,
        Quality = row.Quality, StackCount = row.StackCount, Source = (byte)row.Source
    };

    void CompletionFailure(Player player, Exception exception, bool committed)
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
