namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Logging;

/// <summary>
/// Online adapter for the mission terminal's offer roll. Database/economic authority is serialized with inventory,
/// trades and logout; client packets never supply SQL DTOs. Accepting an offer turns it into a generated quest
/// (<see cref="Quests.Dungeons.QuestDungeonService"/>); nothing else of the old mission runtime remains.
/// </summary>
public sealed class GeneratedMissionService
{
    readonly IGeneratedMissionDao _dao;
    readonly IZoneLogger _logger;
    readonly Func<long> _now;

    public GeneratedMissionService(IGeneratedMissionDao dao, IZoneLogger logger)
        : this(dao, logger, () => DateTime.UtcNow.Ticks) { }

    public GeneratedMissionService(IGeneratedMissionDao dao, IZoneLogger logger, Func<long> utcTicks)
    {
        _dao = dao ?? throw new ArgumentNullException(nameof(dao));
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

    static GeneratedMissionResult Rejected(string reason) => new() { Status = GeneratedMissionResultStatus.Rejected, Reason = reason };
}
