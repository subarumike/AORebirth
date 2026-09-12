namespace ZoneEngine_New.Core.Missions;

using System;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;
using ZoneEngine.Core.Doja;
using ZoneEngine.Core.Playfields;
using ZoneEngine_New.Core.Entities;

public sealed partial class AuthoredQuestService
{
    /// <summary>Read-only entry planning; the later turn-in still validates state in its durable transaction.</summary>
    public bool TryResolveStanDialogueStart(Player player, out string? startNode)
    {
        startNode = null;
        lock (player.PersistenceGate)
        {
            if (!IsCurrent(player)) return false;
            try
            {
                if (HasCarried(player, 248306) || _dao.GetMissions(player.Identity.Instance)
                    .Any(mission => mission.QuestId == DeliverFactory && mission.State == MissionLifecycleState.Active))
                    startNode = "stan_deliver_001";
                return true;
            }
            catch (Exception exception) { _logger.Error(exception, "Stan dialogue state could not be read."); return false; }
        }
    }

    public bool CanOpenDojaTrade(Player player)
    {
        lock (player.PersistenceGate)
        {
            if (!IsCurrent(player) || !HasCarried(player, DojaChipInteractionRules.NascenseChipItemId)) return false;
            string? account = Account(player);
            if (account == null) return false;
            try
            {
                // No writes or external effects in this read-only transaction. Completion does not trust this precheck.
                return _dao.Execute(player.Identity.Instance, account, tx =>
                    tx.GetMission(new(player.Identity.Instance, DojaChipInteractionRules.QuestTurnIn))?.State == MissionLifecycleState.Active
                    && !HasDojaCooldown(tx, player.Identity.Instance, out _));
            }
            catch (Exception exception) { _logger.Error(exception, "DOJA dialogue eligibility could not be read."); return false; }
        }
    }

    /// <summary>New consumer of the existing inventory transaction, not a second reward/persistence coordinator.</summary>
    public bool TryGrantTailorMeasurement(Player player, int answerIndex)
    {
        if (player.Playfield?.Identity.Instance != 127
            || !CapturedSubwayTailorDialogueContent.TryGetMeasurementItemId(answerIndex, out int itemId)) return false;
        return Mutate(player, null, (tx, service) =>
        {
            var item = CreateItem(itemId, 1);
            var plan = Plan(player, [item]);
            if (tx is not IMissionInventoryMutationTransaction inventory)
                throw new InvalidOperationException("Mission DAO lacks atomic item effects.");
            inventory.ApplyInventoryMutation(plan.Rows.Select(ToMissionItem).ToArray(), []);
            return () =>
            {
                plan.PublishAfterCommit(false);
                // CapturedSubwayTailorDialogueRuntime uses the existing exact overflow notification pair.
                SendOverflowGrant(player, item);
            };
        });
    }
}
