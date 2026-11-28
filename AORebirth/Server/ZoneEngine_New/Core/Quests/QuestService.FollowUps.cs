namespace ZoneEngine_New.Core.Quests;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;

public sealed partial class QuestService
{
    void QueueQuestFollowUp(Player player, MissionRewardStageData? stage)
    {
        if (stage != null && player.QuestLog is QuestLog log
            && !log.PendingFollowUps.Any(pending => pending.QuestId == stage.QuestId && pending.RewardKey == stage.RewardKey))
            log.PendingFollowUps.Add(stage);
    }

    void RetryQuestFollowUps(Player player, bool force = false)
    {
        if (!CanDeliver(player) || _completionDao is not ICharacterQuestFollowUpReader reader)
            return;

        QuestLog log = player.QuestLog ??= Load(player);
        if ((!force && log.NextFollowUpRetryUtcTicks > _now().Ticks)
            || (log.FollowUpsLoaded && log.PendingFollowUps.Count == 0))
            return;

        lock (player.PersistenceGate)
        {
            log.NextFollowUpRetryUtcTicks = _now().AddSeconds(5).Ticks;
            if (!log.FollowUpsLoaded)
            {
                try
                {
                    foreach (var stage in reader.GetPendingCharacterQuestFollowUps(player.Identity.Instance))
                        QueueQuestFollowUp(player, stage);
                    log.FollowUpsLoaded = true;
                }
                catch (Exception error)
                {
                    _logger.Error(error, "Quest follow-up recovery could not read the player's pending ledger.");
                    return;
                }
            }
            foreach (var stage in log.PendingFollowUps.ToArray())
            {
                if (!CanDeliver(player)) return;
                if (TryApplyQuestFollowUp(player, log, stage))
                    log.PendingFollowUps.Remove(stage);
            }
        }
    }

    bool TryApplyQuestFollowUp(Player player, QuestLog log, MissionRewardStageData stage)
    {
        if (!_catalog.TryGet(stage.EffectReference, out QuestTemplate template))
        {
            _logger.Warn("Pending quest follow-up definition is unavailable char=" + player.Identity.Instance
                + " quest=" + stage.QuestId + " followUp=" + stage.EffectReference);
            return false;
        }

        bool committed = false;
        try
        {
            // Ordinary template assignment expires due entries before deciding whether the next quest is already held.
            ExpireDue(player, log);
            bool held = log.Quests.TryGetValue(template.Hash, out var existing) && existing.IsActive;
            var items = new List<Item>();
            if (!held && template.StartItems is { Count: > 0 } startItems)
            {
                _flush.HardFlush(player);
                foreach (QuestRewardItem definition in startItems)
                {
                    if (definition.Id <= 0 || definition.HighId is <= 0 || definition.Quality <= 0)
                    {
                        _logger.Warn("Quest follow-up start item is unresolved char=" + player.Identity.Instance
                            + " quest=" + template.Hash + " item=" + definition.Hash);
                        continue; // Ordinary template assignment leaves unresolved optional start items out.
                    }
                    items.Add(_items.CreateWithNewInstance(definition.Id, definition.HighId ?? definition.Id,
                        definition.Quality, ItemSource.Quest));
                }
            }
            if (!QuestStarterItemPlan.TryCreate(player, items, out var grant))
                return false;
            var request = new CharacterQuestFollowUpRequest
            {
                Key = new MissionRewardKeyData(new MissionKeyData(player.Identity.Instance, stage.QuestId), stage.RewardKey),
                QuestId = template.Hash, RequiredCount = template.RequiredCount, NowUtcTicks = _now().Ticks, ExpectedActive = held,
                StarterGrants = grant.StarterGrants, RetiredOverflow = grant.RetiredOverflow, StackMerges = grant.StackMerges
            };
            var result = _completionDao!.Execute(player.Identity.Instance,
                tx => RequireCompletion(tx).TryApplyCharacterQuestFollowUp(request));
            if (result.Status == CharacterQuestCompletionStatus.Rejected)
            {
                _logger.Warn("Quest follow-up intent remains pending char=" + player.Identity.Instance
                    + " quest=" + stage.QuestId + " reason=" + result.Reason);
                return false;
            }
            committed = true;
            if (result.Status == CharacterQuestCompletionStatus.AlreadyApplied || (result.Quest == null && !held))
            {
                // Another publication may have committed a child assignment and grants. Reload instead of
                // letting this stale actor publish or save its earlier inventory and quest state.
                player.QuarantinePersistence();
                player.Session?.Close();
                return true;
            }
            if (result.Quest != null)
            {
                if (held)
                    throw new InvalidOperationException("The follow-up state changed after starter items were planned.");
                DateTime assignedAt = new(result.Quest.AssignedAtUtcTicks, DateTimeKind.Utc);
                var quest = new PlayerQuest
                {
                    QuestId = template.Hash, Source = QuestSource.Template, Template = template,
                    State = QuestState.Active, RequiredCount = result.Quest.RequiredCount,
                    AssignedAtUtc = assignedAt, ExpiresAtUtc = template.ExpiresAt(assignedAt)
                };
                log.Quests[quest.QuestId] = quest;
                QuestJournal.Send(player, quest, announce: true);
                grant.PublishAfterCommit();
            }
            return true;
        }
        catch (Exception error)
        {
            CompletionFailure(player, error, committed);
            return false;
        }
    }
}
