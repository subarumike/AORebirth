namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine.Core.Missions;

/// <summary>
/// Online adapter for the existing mission DAO. All database/economic authority is
/// serialized with inventory, trades and logout; client packets never supply SQL DTOs.
/// ACG materialization and journal packet projection remain separate typed consumers.
/// </summary>
public sealed class GeneratedMissionService
{
    readonly IGeneratedMissionDao _dao;
    readonly InventoryFlushService _flush;
    readonly IItemBuilder _items;
    readonly IItemInstanceIdAllocator _ids;
    readonly IZoneLogger _logger;
    readonly Func<long> _now;

    public GeneratedMissionService(IGeneratedMissionDao dao, InventoryFlushService flush,
        IItemBuilder items, IItemInstanceIdAllocator ids, IZoneLogger logger)
        : this(dao, flush, items, ids, logger, () => DateTime.UtcNow.Ticks) { }

    public GeneratedMissionService(IGeneratedMissionDao dao, InventoryFlushService flush,
        IItemBuilder items, IItemInstanceIdAllocator ids, IZoneLogger logger, Func<long> utcTicks)
    {
        _dao = dao ?? throw new ArgumentNullException(nameof(dao));
        _flush = flush ?? throw new ArgumentNullException(nameof(flush));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _now = utcTicks ?? throw new ArgumentNullException(nameof(utcTicks));
    }

    public GeneratedMissionResult PublishOffers(Player player, GeneratedMissionOfferBatch batch)
        => WithPlayer(player, () =>
        {
            if (batch.OwnerId != player.Identity.Instance || batch.OwnerType != (int)player.Identity.Type
                || batch.TerminalPlayfield != player.Playfield?.Identity.Instance)
                return Rejected("Offer request owner or terminal playfield mismatch.");
            batch.CurrentCash = player.Stats.GetOrZero(CharacterStat.Cash, StatDetail.Base);
            return CommitAndPublish(player, () => _dao.PublishOffers(batch), result => SetStat(player, CharacterStat.Cash, result.Cash));
        });

    public IReadOnlyList<GeneratedMissionOffer> ReadOffers(Player player)
    {
        lock (player.PersistenceGate)
            return player.IsPersistenceQuarantined ? [] : _dao.ReadOffers(player.Identity.Instance)
                .Where(offer => offer.State == GeneratedMissionState.Offered && offer.ExpiresAtUtcTicks > _now()).ToArray();
    }

    public IReadOnlyList<GeneratedMissionBinding> ReadAccepted(Player player)
    {
        lock (player.PersistenceGate)
            return player.IsPersistenceQuarantined ? [] : _dao.ReadAccepted(player.Identity.Instance).ToArray();
    }

    /// <summary>The ACG planner supplies a fully proven binding and unpersisted key/component items.</summary>
    public GeneratedMissionResult Accept(Player player, GeneratedMissionAcceptance acceptance, IReadOnlyList<Item> artifacts)
        => WithPlayer(player, () =>
        {
            if (acceptance.OwnerId != player.Identity.Instance) return Rejected("Acceptance owner mismatch.");
            // Replays restore the original binding; they must not allocate or publish another key.
            var existing = _dao.ReadAccepted(player.Identity.Instance).SingleOrDefault(binding => binding.OfferType == acceptance.OfferType && binding.OfferInstance == acceptance.OfferInstance);
            if (existing != null) return new() { Status = GeneratedMissionResultStatus.AlreadyApplied, Binding = existing };
            _flush.HardFlush(player);
            if (!InventoryGrantPlan.TryCreate(player, artifacts, out var plan)) return Rejected("No durable capacity for the complete mission artifact plan.");
            acceptance.Artifacts = plan.Rows.Select(ToMissionItem).ToArray();
            acceptance.AcceptedAtUtcTicks = _now();
            // Accepted Legacy missions receive a fresh 48h duration, not the roll's remaining window.
            acceptance.ExpiresAtUtcTicks = checked(acceptance.AcceptedAtUtcTicks + TimeSpan.TicksPerHour * 48);
            // Accepted artifacts have their own captured SIFU/container/template route.
            return CommitAndPublish(player, () => _dao.Accept(acceptance), _ => plan.PublishAfterCommit(notify: false));
        });

    /// <summary>Only a trusted server target event may call this; packet receipt is not objective proof.</summary>
    public GeneratedMissionResult Observe(Player player, GeneratedMissionObservation observation)
        => WithPlayer(player, () =>
        {
            if (observation.OwnerId != player.Identity.Instance || observation.LivePlayfield != player.Playfield?.Identity.Instance)
                return Rejected("Objective observation owner or live playfield mismatch.");
            if (observation.Interaction is 3 or 4 or 5) return Rejected("Artifact objectives require the complete inventory plan.");
            observation.ObservedAtUtcTicks = _now();
            if (observation.AdvanceProgress) observation.CompletionToken = PlanCompletionToken(player, observation);
            return CommitAndPublish(player, () => _dao.Observe(observation), _ => { });
        });

    internal GeneratedMissionResult ObserveArtifact(Player player, GeneratedMissionObservation observation,
        IReadOnlyList<Item> grants, int? consumeSlot)
        => WithPlayer(player, () =>
        {
            if (observation.OwnerId != player.Identity.Instance
                || (observation.Interaction == 4 ? observation.ActualPlayfield : observation.LivePlayfield) != player.Playfield?.Identity.Instance)
                return Rejected("Artifact objective owner/playfield mismatch.");
            _flush.HardFlush(player);
            if (!InventoryGrantPlan.TryCreate(player, grants, out var plan)) return Rejected("No capacity for the complete objective artifact.");
            Item? consumed = null;
            if (consumeSlot.HasValue)
            {
                if (!player.Inventory.Inventory.Content.TryGetValue(consumeSlot.Value, out consumed) || !consumed.IsPersisted || consumed.Locked)
                    return Rejected("Exact objective item is unavailable.");
                observation.ConsumeItem = ToMissionItem(InventoryActionService.ToRecord(consumed, player.Inventory.Inventory.Identity, consumeSlot.Value, consumed.StackCount));
            }
            observation.Grants = plan.Rows.Select(ToMissionItem).ToArray();
            observation.ObservedAtUtcTicks = _now();
            if (observation.AdvanceProgress) observation.CompletionToken = PlanCompletionToken(player, observation);
            return CommitAndPublish(player, () => _dao.Observe(observation), _ =>
            {
                plan.PublishAfterCommit(notify: false);
                foreach (var item in grants)
                    GeneratedMissionArtifactProjection.Send(player, item, plan.Rows.Single(row => row.InstanceId == item.InstanceId).ContainerPlacement, false, "Mission Item");
                if (consumed != null)
                {
                    if (!ReferenceEquals(player.Inventory.Inventory.Content[consumeSlot!.Value], consumed)) throw new InvalidOperationException("Committed consumed artifact memory changed.");
                    player.Inventory.Inventory.Content.Remove(consumeSlot.Value);
                    player.Session?.Send(new DespawnMessage { Identity = consumed.Identity, Unknown = 1 });
                }
            });
        });

    public GeneratedMissionResult Complete(Player player, Identity quest)
        => WithPlayer(player, () =>
        {
            var binding = _dao.ReadAccepted(player.Identity.Instance, (int)quest.Type, quest.Instance);
            if (binding == null) return Rejected("Mission is not owned.");
            if (binding.State == GeneratedMissionState.Completed) return new() { Status = GeneratedMissionResultStatus.AlreadyApplied, Binding = binding };
            if (binding.State != GeneratedMissionState.Active || binding.ExpiresAtUtcTicks <= _now() || binding.Progress != binding.RequiredCount)
                return Rejected("Mission is inactive, expired, or incomplete.");
            if (binding.CompletionFrozenAtUtcTicks <= 0 || !binding.TokenDisposition.HasValue)
                return Rejected("Mission completion token claim has not been durably sealed.");
            _flush.HardFlush(player);
            var grant = new List<Item>();
            if (binding.Offer.RewardCount > 0)
            {
                // The frozen endpoint/QL/count is authoritative. Never reroll on completion.
                grant.Add(_items.Create(binding.Offer.RewardLowId, binding.Offer.RewardHighId, binding.Offer.RewardQuality,
                    ItemSource.Other, binding.Offer.RewardCount, _ids.Allocate()));
            }
            Item? token = null;
            if (binding.TokenDisposition == 2)
            {
                int low = binding.TokenClaimSide == 1 ? 103910 : 103908;
                token = _items.Create(low, low + 1, 1, ItemSource.Other, binding.TokenCount!.Value, _ids.Allocate());
                grant.Add(token);
            }
            if (!InventoryGrantPlan.TryCreate(player, grant, out var plan)) return Rejected("No durable capacity for the frozen reward; mission remains incomplete.");
            var progression = DirectXpRewardPlan.Create(player, binding.Offer.ExperienceReward);
            var completion = new GeneratedMissionCompletion
            {
                OwnerId = player.Identity.Instance, CharacterType = (int)player.Identity.Type,
                QuestType = (int)quest.Type, QuestInstance = quest.Instance, CompletedAtUtcTicks = _now(),
                CurrentCash = player.Stats.GetOrZero(CharacterStat.Cash, StatDetail.Base),
                CurrentExperience = player.Stats.GetOrZero((CharacterStat)StatIds.xp, StatDetail.Base),
                RequestedExperienceReward = progression.RequestedReward, FinalExperience = progression.ExperienceAfter,
                CurrentLevel = progression.LevelBefore,
                ProgressionStats = progression.Stats.Select(pair => new MissionStatValueData
                {
                    StatIdentityType = (int)player.Identity.Type, StatId = (int)pair.Key, Value = pair.Value
                }).ToArray(),
                Items = plan.Rows.Where(row => row.InstanceId != token?.InstanceId).Select(ToMissionItem).ToArray(),
                TokenItem = token == null ? null : ToMissionItem(plan.Rows.Single(row => row.InstanceId == token.InstanceId))
            };
            return CommitAndPublish(player, () => _dao.Complete(completion), result =>
            {
                plan.PublishAfterCommit(notify: false);
                foreach (var item in grant)
                    GeneratedMissionArtifactProjection.Send(player, item, plan.Rows.Single(row => row.InstanceId == item.InstanceId).ContainerPlacement, false,
                        ReferenceEquals(item, token) ? binding.TokenClaimSide == 1 ? "Clan Token" : "Omni Token" : "Mission Reward");
                SetStat(player, CharacterStat.Cash, result.Cash);
                progression.PublishAfterCommit(player);
            });
        });

    public GeneratedMissionResult End(Player player, Identity quest, GeneratedMissionState state)
        => WithPlayer(player, () => CommitAndPublish(player,
            () => _dao.End(player.Identity.Instance, (int)quest.Type, quest.Instance, state, _now()), _ => { }));

    // Legacy mission possession scans the character's top-level Pages, including bank,
    // but not the separately stored backpack interiors. SQL proves an unopened bank
    // row's exact owner; it must not be confused with an unverified container parent.
    internal bool HasPhysicalKey(Player player, GeneratedMissionBinding binding)
        => WithPlayer(player, () =>
        {
            if (binding.OwnerId != player.Identity.Instance || binding.KeyInstance <= 0)
                return Rejected("Mission key owner mismatch.");
            _flush.HardFlush(player);
            var key = _dao.ReadArtifacts(player.Identity.Instance, binding.QuestType, binding.QuestInstance)
                .SingleOrDefault(row => row.InstanceId == binding.KeyInstance);
            return key != null && key.LowId == 28577 && key.HighId == 28577
                && TryResolveArtifactPage(player, key, out _, out _)
                ? new() { Status = GeneratedMissionResultStatus.Applied }
                : Rejected("The exact mission key is not in an owned top-level page.");
        }).Status == GeneratedMissionResultStatus.Applied;

    internal GeneratedMissionResult CleanupArtifacts(Player player, Identity quest)
        => WithPlayer(player, () =>
        {
            _flush.HardFlush(player);
            var rows = _dao.ReadArtifacts(player.Identity.Instance, (int)quest.Type, quest.Instance).Where(row => row.ContainerType != 0).ToArray();
            var removals = new List<(MissionItemInstanceData Row, Container Page, Item? Item)>();
            foreach (var row in rows)
            {
                if (!TryResolveArtifactPage(player, row, out var page, out var item))
                    return Rejected("Mission cleanup needs exact owned inventory-page reconciliation; no artifact removed.");
                removals.Add((row, page, item));
            }
            return CommitAndPublish(player, () => _dao.CleanupArtifacts(player.Identity.Instance, (int)quest.Type, quest.Instance, _now()), _ =>
            {
                foreach (var removal in removals)
                {
                    if (removal.Item != null)
                    {
                        if (!removal.Page.Content.TryGetValue(removal.Row.ContainerPlacement, out var current)
                            || !ReferenceEquals(current, removal.Item))
                            throw new InvalidOperationException("Committed cleanup inventory identity changed.");
                        removal.Page.Content.Remove(removal.Row.ContainerPlacement);
                    }
                    // A never-opened bank has no in-memory item to remove. Its next hydration
                    // reads the committed retirement; do not create a partial bank snapshot.
                    player.Session?.Send(new DespawnMessage
                    {
                        Identity = new() { Type = (IdentityType)removal.Row.ItemType, Instance = removal.Row.InstanceId }, Unknown = 1
                    });
                }
            });
        });

    static bool TryResolveArtifactPage(Player player, MissionItemInstanceData row, out Container page, out Item? item)
    {
        item = null;
        page = (IdentityType)row.ContainerType switch
        {
            IdentityType.Inventory => player.Inventory.Inventory,
            IdentityType.WeaponPage => player.Inventory.Equipment,
            IdentityType.ArmorPage => player.Inventory.Armor,
            IdentityType.ImplantPage => player.Inventory.Implant,
            IdentityType.SocialPage => player.Inventory.Social,
            IdentityType.BankByRef => player.Inventory.Bank,
            _ => null!
        };
        if (page == null || row.ContainerInstance != player.Identity.Instance || page.Identity.Instance != player.Identity.Instance
            || row.InstanceId <= 0 || row.ItemType <= 0 || row.LowId <= 0 || row.HighId <= 0 || row.Quality <= 0 || row.StackCount <= 0
            || row.ContainerPlacement < page.Offset || row.ContainerPlacement >= page.Offset + page.Capacity)
            return false;
        if (!page.IsHydrated)
            return page == player.Inventory.Bank && page.Content.Count == 0;
        return page.Content.TryGetValue(row.ContainerPlacement, out item)
            && item.IsPersisted && !item.Locked && item.InstanceId == row.InstanceId
            && (int)item.Identity.Type == row.ItemType && item.LowId == row.LowId && item.HighId == row.HighId
            && item.Quality == row.Quality && item.StackCount == row.StackCount && (int)item.Source == row.Source;
    }

    GeneratedMissionResult WithPlayer(Player player, Func<GeneratedMissionResult> action)
    {
        ArgumentNullException.ThrowIfNull(player);
        lock (player.PersistenceGate)
        {
            if (player.IsPersistenceQuarantined || !player.Inventory.IsHydrated) return Rejected("Player persistence is unavailable.");
            try { return action(); }
            catch (Exception exception) when (exception is MissionCommitOutcomeUnknownException or DatabaseCommitOutcomeUnknownException)
            {
                Quarantine(player, exception);
                return Rejected("Commit outcome is unknown; reconnect after database reconciliation.");
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Mission operation failed before publication; no automatic retry.");
                return Rejected("Mission persistence failed; no reward or mission packet was published.");
            }
        }
    }

    GeneratedMissionResult CommitAndPublish(Player player, Func<GeneratedMissionResult> commit, Action<GeneratedMissionResult> publish)
    {
        var result = commit();
        if (result.Status == GeneratedMissionResultStatus.Applied)
        {
            try { publish(result); }
            catch (Exception exception)
            {
                // A known database commit cannot be undone by a failed memory/packet update.
                Quarantine(player, exception);
                throw;
            }
        }
        return result;
    }

    void Quarantine(Player player, Exception exception)
    {
        player.QuarantinePersistence(); player.Session?.Close();
        _logger.Error(exception, "Mission commit/publication requires authoritative reload; player persistence quarantined.");
    }

    static void SetStat(Player player, CharacterStat stat, int value)
    {
        player.Stats.Set(stat, value, StatDetail.Base, dirty: true);
        player.FlushDirtyStats();
    }

    static MissionItemInstanceData ToMissionItem(ItemInstanceRecord row) => new()
    {
        InstanceId = row.InstanceId, ContainerType = row.ContainerType, ContainerInstance = row.ContainerInstance,
        ContainerPlacement = row.ContainerPlacement, ItemType = row.ItemType, LowId = row.LowId,
        HighId = row.HighId, Quality = row.Quality, StackCount = row.StackCount, Source = (byte)row.Source
    };

    GeneratedMissionTokenClaim PlanCompletionToken(Player player, GeneratedMissionObservation observation)
    {
        var ambient = _dao.ReadObjects(player.Identity.Instance, observation.QuestType, observation.QuestInstance)
            .Where(row => row.Kind == (int)MissionAcgRuntimeObjectKind.AmbientNpc).ToArray();
        int percent = MissionAcgTokenRewardPolicy.CalculatePercent(ambient.Count(row => row.IsDead && row.DeathActorId == player.Identity.Instance && row.DiedAtUtcTicks > 0), ambient.Length);
        int level = player.Stats.GetOrOne(CharacterStat.Level, StatDetail.Base);
        int side = player.Stats.GetOrZero((CharacterStat)StatIds.side, StatDetail.Base);
        if (!MissionAcgTokenRewardPolicy.TryResolve(percent, level, (Side)side, out var token, out string failure))
            throw new InvalidOperationException(failure);
        return new() { ProgressPercent = percent, CharacterLevel = level, Side = side, Disposition = token.Disposition, Count = token.Count };
    }

    static GeneratedMissionResult Rejected(string reason) => new() { Status = GeneratedMissionResultStatus.Rejected, Reason = reason };
}
