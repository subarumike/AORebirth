namespace ZoneEngine_New.Core.Quests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Helpers;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Missions;

public interface IRewardExecutionModule
{
    IReadOnlyList<QuestRewardItem> ResolveItems(Player player, PlayerQuest quest, QuestReward? reward = null);
    QuestRewardExecutionPlan? Plan(Player player, PlayerQuest quest, CharacterQuestCompletionSnapshotData earned,
        QuestRewardItem? tokenReward, long nowUtcTicks);
    CharacterQuestCompletionResult Commit(Player player, QuestRewardExecutionPlan plan);
    IList<MissionStatValueData> ApplyAuthoredStats(IMissionDaoTransaction transaction, Player player,
        string quest, string rewardKey, int xp, int cash, string evidence, long nowUtcTicks);
}

/// <summary>The reward projection and the exact DAO input for one earned quest assignment.</summary>
public sealed class QuestRewardExecutionPlan
{
    internal QuestRewardExecutionPlan(CharacterQuestCompletionRequest request,
        InventoryGrantPlan inventory, DirectXpRewardPlan experience, IReadOnlyDictionary<CharacterStat, int> stats,
        IReadOnlyList<Action> virtualPublications, IReadOnlyList<Item> immediateVirtualItems)
    {
        Request = request;
        Inventory = inventory;
        Experience = experience;
        Stats = stats;
        VirtualPublications = virtualPublications;
        ImmediateVirtualItems = immediateVirtualItems;
    }

    internal CharacterQuestCompletionRequest Request { get; }
    internal InventoryGrantPlan Inventory { get; }
    internal DirectXpRewardPlan Experience { get; }
    internal IReadOnlyDictionary<CharacterStat, int> Stats { get; }
    internal IReadOnlyList<Action> VirtualPublications { get; }
    internal IReadOnlyList<Item> ImmediateVirtualItems { get; }
}

public sealed class RewardExecutionModule(
    IMissionDao? dao,
    IItemBuilder items,
    IZoneLogger logger,
    QuestRewardTable? rewards = null,
    QuestCatalog? catalog = null) : IRewardExecutionModule
{
    public QuestRewardExecutionPlan? Plan(Player player, PlayerQuest quest, CharacterQuestCompletionSnapshotData earned,
        QuestRewardItem? tokenReward, long nowUtcTicks)
    {
        QuestTemplate rewardTemplate = quest.Source == QuestSource.Generated
            ? JsonSerializer.Deserialize<QuestTemplate>(earned.DefinitionJson, QuestTemplate.JsonOptions)
                ?? throw new InvalidOperationException("Stored reward definition is unavailable.")
            : quest.Template;
        var reward = rewardTemplate.Reward;
        var definitions = ResolveItems(player, quest, reward).ToList();
        if (tokenReward != null)
            definitions.Add(tokenReward);
        var physicalItems = new List<Item>();
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
                || physicalItems.Any(existing => existing.LowId == definition.Id || existing.HighId == highId)))
            {
                skippedUnique.Add(string.IsNullOrEmpty(definition.Name) ? definition.Id.ToString(CultureInfo.InvariantCulture) : definition.Name);
                continue;
            }
            // Live's virtual reward items (Daily Mission XP Reward, Token Add, Set Flag items) carry the Null
            // flag: they are used on the player, never placed in the inventory, so no instance is minted.
            if ((items.CreateTemplate(definition.Id, highId, definition.Quality).Flags & (int)ItemFlags.Null) != 0)
            {
                used.Add(items.Create(definition.Id, highId, definition.Quality, ItemSource.Quest));
                continue;
            }
            Item minted = items.CreateWithNewInstance(definition.Id, highId, definition.Quality, ItemSource.Quest);
            // A configured Count replaces the template's mint count, as a daily login reward's does.
            if (definition.Count is int count)
                minted.StackCount = count;
            physicalItems.Add(minted);
        }
        if (skippedUnique.Count > 0)
            logger.Info("Quest reward unique items already held, left out char=" + player.Identity.Instance
                + " quest=" + quest.QuestId + " items=" + string.Join(", ", skippedUnique));
        if (!InventoryGrantPlan.TryCreate(player, physicalItems, out var grant, allowOverflow: true))
            return null;
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
        Dictionary<CharacterStat, int> durableValues = values;
        List<Action> virtualPublications = [];
        IReadOnlyList<Item> immediateVirtualItems = used;
        if (used.Count != 0)
        {
            var projected = new Dictionary<CharacterStat, int>(values);
            bool supported = true;
            foreach (Item item in used)
            {
                if (!VirtualQuestRewardPlan.TryAdd(item, player, projected, out Action? publish))
                {
                    supported = false;
                    break;
                }
                if (publish != null)
                    virtualPublications.Add(publish);
            }
            if (supported)
            {
                durableValues = projected;
                immediateVirtualItems = [];
            }
            else
            {
                // An unplanned OnUse can change inputs to later items. Keep the complete sequence together.
                virtualPublications.Clear();
            }
        }
        var expected = durableValues.Keys.Select(stat => new MissionStatValueData
        {
            StatIdentityType = (int)player.Identity.Type, StatId = (int)stat,
            Value = player.Stats.GetOrZero(stat, StatDetail.Base)
        }).ToArray();
        var request = new CharacterQuestCompletionRequest
        {
            QuestId = quest.QuestId, AssignedAtUtcTicks = earned.AssignedAtUtcTicks,
            ExpectedDefinitionJson = earned.DefinitionJson,
            ExpectedAcgBuildingGeneratorJson = earned.AcgBuildingGeneratorJson, NowUtcTicks = nowUtcTicks,
            ResearchMutation = xp.ResearchMutation,
            FollowUpQuestId = catalog != null && !string.IsNullOrEmpty(rewardTemplate.NextStep)
                && catalog.TryGet(rewardTemplate.NextStep, out _) ? rewardTemplate.NextStep : null,
            Grants = grant.Rows.Select(ToMissionItem).ToArray(), ExpectedStatValues = expected,
            StatMutations = durableValues.Select(pair => new MissionStatMutationData
            {
                StatIdentityType = (int)player.Identity.Type, StatId = (int)pair.Key,
                Kind = MissionStatMutationKind.Set, Value = pair.Value,
                MinimumValue = int.MinValue, MaximumValue = int.MaxValue
            }).ToArray()
        };
        return new QuestRewardExecutionPlan(request, grant, xp, values, virtualPublications, immediateVirtualItems);
    }

    public CharacterQuestCompletionResult Commit(Player player, QuestRewardExecutionPlan plan)
    {
        if (dao == null)
            throw new InvalidOperationException("The character quest reward DAO is unavailable.");
        return dao.Execute(player.Identity.Instance, null!, transaction =>
        {
            if (transaction is not ICharacterQuestCompletionTransaction completion)
                throw new InvalidOperationException("Mission DAO does not support atomic character quest rewards.");
            return completion.TryCompleteCharacterQuest(plan.Request);
        });
    }

    /// <summary>
    /// The items a quest pays this player now. A Quests.json quest's come only from QuestRewards.json, by its reward
    /// hash: none when the hash has no entry or no entry matches. A generated quest's are the record stored with it.
    /// </summary>
    public IReadOnlyList<QuestRewardItem> ResolveItems(Player player, PlayerQuest quest, QuestReward? reward = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(quest);
        reward ??= quest.Template.Reward;
        if (quest.Source == QuestSource.Generated)
            return SelectRewardRecord(quest, reward);

        return rewards != null && rewards.TryResolve(reward?.Hash, player, quest, out IReadOnlyList<QuestRewardItem> mapped)
            ? mapped
            : [];
    }

    /// <summary>A generated quest's stored record; records are alternatives, one chosen per accepted run.</summary>
    static IReadOnlyList<QuestRewardItem> SelectRewardRecord(PlayerQuest quest, QuestReward? reward)
    {
        if (reward?.Records is not { Count: > 0 }) return [];
        return reward.Records[QuestRewardTable.StableIndex(quest, reward.Records.Count)];
    }

    /// <summary>The reward template carries the Unique flag (read from the template, nothing minted).</summary>
    bool IsUniqueTemplate(int templateId)
    {
        try
        {
            return (items.CreateTemplate(templateId, templateId, 1).Flags & (int)ItemFlags.Unique) != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public IList<MissionStatValueData> ApplyAuthoredStats(IMissionDaoTransaction tx, Player player, string quest, string rewardKey, int xp, int cash, string evidence, long now)
    {
        // Use the owner-gated live values, not potentially older snapshot rows. The accepted
        // AddClamped arithmetic is unchanged; its final values join the same ledger transaction.
        var mutations = new[] { (StatIds.cash, cash), (StatIds.xp, xp), (StatIds.unsavedxp, xp), (StatIds.lastxp, xp) }
            .Select(value => new MissionStatMutationData { StatIdentityType = (int)player.Identity.Type, StatId = (int)value.Item1,
                Kind = MissionStatMutationKind.Set,
                Value = value.Item1 == StatIds.lastxp ? value.Item2 : Math.Min(uint.MaxValue,
                    (long)unchecked((uint)player.Stats.GetOrZero((CharacterStat)value.Item1, StatDetail.Base)) + value.Item2),
                MinimumValue = 0, MaximumValue = uint.MaxValue }).ToArray();
        var result = tx.TryApplyCharacterStatReward(new(new(player.Identity.Instance, quest), rewardKey), "character-stats", mutations, evidence, now);
        // The caller's transaction also owns authored item grants, item consumption and quest transitions.
        // Roll the whole action back if this reward key already paid.
        if (result.Status == MissionAtomicRewardStatus.AlreadyApplied)
            throw new InvalidOperationException("The authored quest reward has already been applied.");
        if (result.Status != MissionAtomicRewardStatus.Applied)
            throw new InvalidOperationException(result.Message);
        return result.StatValues;
    }

    static MissionItemInstanceData ToMissionItem(ItemInstanceRecord row) => new()
    {
        InstanceId = row.InstanceId, ContainerType = row.ContainerType, ContainerInstance = row.ContainerInstance,
        ContainerPlacement = row.ContainerPlacement, ItemType = row.ItemType, LowId = row.LowId, HighId = row.HighId,
        Quality = row.Quality, StackCount = row.StackCount, Source = (byte)row.Source
    };

}
