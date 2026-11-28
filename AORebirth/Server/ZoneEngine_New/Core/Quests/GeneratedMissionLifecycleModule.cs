namespace ZoneEngine_New.Core.Quests;

using System;
using System.Globalization;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Quests.Dungeons;
using ZoneEngine_New.Core.World;
using Vector3 = AORebirth.Core.Vector.Vector3;

public interface IGeneratedMissionLifecycleModule
{
    bool TryAbandon(Player player, int journalInstance);
    bool TryEnter(Player player, Identity entrance);
    bool TryDuplicateKey(Player player, Identity sourceSlot, Identity targetSlot);
    void RetryPendingRewards(Player player);
    void PrepareLogin(int characterId);
    /// <summary>Null leaves login to ordinary playfield admission. A result without a world reports a mission with no safe exit.</summary>
    (Playfield? Playfield, Vector3? Position, IDisposable? Admission)? ResolveLogin(int characterId, int storedPlayfield);
    void Restore(Player player);
}

internal sealed class GeneratedMissionLifecycleModule(
    QuestService quests,
    QuestDungeonService dungeons,
    IGeneratedMissionDao acceptedMissions,
    Lazy<PlayfieldManager> playfields,
    IZoneLogger logger) : IGeneratedMissionLifecycleModule
{
    public bool TryAbandon(Player player, int journalInstance)
        => !player.IsDead && !player.IsPersistenceQuarantined && quests.TryAbandon(player, journalInstance);

    public bool TryEnter(Player player, Identity entrance) => dungeons.TryEnter(player, entrance);

    public bool TryDuplicateKey(Player player, Identity sourceSlot, Identity targetSlot)
        => dungeons.TryDuplicate(player, sourceSlot, targetSlot);

    public void RetryPendingRewards(Player player) => quests.RetryPendingRewards(player);

    public void PrepareLogin(int characterId) => dungeons.RetireDeadKeysOnLogin(characterId);

    public (Playfield? Playfield, Vector3? Position, IDisposable? Admission)? ResolveLogin(int characterId, int storedPlayfield)
    {
        if (QuestDungeonIds.IsDungeonPlayfield(storedPlayfield))
            return dungeons.ResolveLogin(storedPlayfield);

        if (storedPlayfield < GeneratedMissionIdentitySpace.MinimumLivePlayfield2
            || storedPlayfield > GeneratedMissionIdentitySpace.MaximumLivePlayfield2)
            return null;

        // Players saved in a retired mission world return to the exact destination on its accepted offer.
        var offer = acceptedMissions.ReadAccepted(characterId)
            .FirstOrDefault(value => value.LivePlayfield == storedPlayfield)?.Offer;
        if (offer == null || offer.DestinationPlayfield <= 0)
            return (null, null, null);

        var landing = new Vector3(offer.DestinationX, offer.DestinationY, offer.DestinationZ);
        try
        {
            var admission = playfields.Value.AcquirePlayfield(offer.DestinationPlayfield);
            return (admission.Playfield, landing, admission);
        }
        catch (PlayfieldHostedElsewhereException)
        {
            throw new LoginRelocationException(offer.DestinationPlayfield, landing);
        }
    }

    public void Restore(Player player)
    {
        try
        {
            // QuestService retries earned rewards, restores the journal and raises QuestEnded for cleanup.
            quests.Restore(player);
        }
        catch (Exception error)
        {
            logger.Error(error, string.Format(CultureInfo.InvariantCulture,
                "Quest restore failed character={0}; continuing with world visibility", player.Identity.Instance));
        }

        try
        {
            // Inventory hydration provides the template name. The dungeon owner restores each surviving key's name.
            dungeons.RestoreKeys(player);
        }
        catch (Exception error)
        {
            logger.Error(error, string.Format(CultureInfo.InvariantCulture,
                "Mission key restore failed character={0}; continuing with world visibility", player.Identity.Instance));
        }
    }
}
