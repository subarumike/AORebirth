namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Concurrent;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using Vector3 = AORebirth.Core.Vector.Vector3;

public sealed partial class GeneratedMissionAcgService
{
    readonly ConcurrentDictionary<int, long> _nextLifecycle = new();
    readonly ConcurrentDictionary<int, (int Playfield, Vector3 Position, long Ticks)> _savedPositions = new();

    public bool TryPersistPlayerPosition(Player player, Vector3 proposed)
    {
        if (player.Playfield is not MissionPlayfield world) return true;
        lock (player.PersistenceGate)
        {
            if (player.IsPersistenceQuarantined || !world.World.AcceptsMovement(player, proposed)) return false;
            long now = DateTime.UtcNow.Ticks;
            if (_savedPositions.TryGetValue(player.Identity.Instance, out var previous) && previous.Playfield == world.Identity.Instance
                && now - previous.Ticks < TimeSpan.TicksPerSecond * 5 && Vector3.Abs(proposed - previous.Position) < 2.0) return true;
            try
            {
                var result = _dao.SavePosition(player.Identity.Instance, world.World.QuestType, world.World.QuestInstance,
                    world.Identity.Instance, proposed.xf, proposed.yf, proposed.zf, now);
                if (result.Status != GeneratedMissionResultStatus.Applied) return false;
                _savedPositions[player.Identity.Instance] = (world.Identity.Instance, new Vector3(proposed.x, proposed.y, proposed.z), now);
                return true;
            }
            catch (MissionCommitOutcomeUnknownException exception) { Quarantine(player, exception); return false; }
            catch (Exception exception) { _logger.Error(exception, "Mission movement persistence failed before accepting the position."); return false; }
        }
    }

    public void Abandon(Player player, Identity quest)
    {
        var result = _missions.End(player, quest, GeneratedMissionState.Abandoned);
        if (result.Status != GeneratedMissionResultStatus.Rejected) PollLifecycle(player, force: true);
    }

    // Executed from the owning player/playfield dispatcher, not a separate timer thread.
    public void PollLifecycle(Player player, bool force = false)
    {
        if (player.IsPersistenceQuarantined || player.Session?.State != SessionState.InPlay || player.Playfield == null) return;
        long now = DateTime.UtcNow.Ticks;
        if (!force && _nextLifecycle.TryGetValue(player.Identity.Instance, out long next) && next > now) return;
        _nextLifecycle[player.Identity.Instance] = now + TimeSpan.TicksPerSecond;
        lock (player.PersistenceGate)
        {
            try
            {
                foreach (var row in _dao.ReadAccepted(player.Identity.Instance))
                {
                    var binding = row;
                    if (binding.State == GeneratedMissionState.Active && now >= binding.ExpiresAtUtcTicks)
                    {
                        var ended = _missions.End(player, Quest(binding), GeneratedMissionState.Expired);
                        if (ended.Status == GeneratedMissionResultStatus.Rejected) continue;
                        binding = ended.Binding;
                    }
                    if (binding.State == GeneratedMissionState.Active)
                    {
                        if (player.Playfield is MissionPlayfield active && active.World.QuestInstance == binding.QuestInstance)
                        {
                            TryPersistPlayerPosition(player, player.Position);
                            RestorePendingCorpses(player, binding);
                        }
                        continue;
                    }
                    if (binding.CleanupCheckpoints == (1L << 17) - 1) continue;
                    // Gold completion remains inside the world until its owner exits. Expiry
                    // and abandon evacuate; neither permits an unowned fallback destination.
                    if (binding.State == GeneratedMissionState.Completed && player.Playfield.Identity.Instance == binding.LivePlayfield && now < binding.ExpiresAtUtcTicks)
                    {
                        TryPersistPlayerPosition(player, player.Position);
                        RestorePendingCorpses(player, binding);
                        continue;
                    }
                    binding = Checkpoint(binding, binding.CleanupCheckpoints | 7, now);
                    if (player.Playfield.Identity.Instance == binding.LivePlayfield)
                    {
                        player.Session.TransferToPlayfield(_playfields.Value.GetOrCreate(binding.Offer.DestinationPlayfield),
                            new Vector3(binding.Offer.DestinationX, binding.Offer.DestinationY, binding.Offer.DestinationZ));
                        return; // Occupant removal must be observed after the transfer completes.
                    }
                    if (!_playfields.Value.TryReleaseMission(binding)) continue;
                    binding = Checkpoint(binding, binding.CleanupCheckpoints | 255, now);
                    var artifacts = _missions.CleanupArtifacts(player, Quest(binding));
                    if (artifacts.Status == GeneratedMissionResultStatus.Rejected) continue;
                    binding = artifacts.Binding;
                    binding = Checkpoint(binding, binding.CleanupCheckpoints | 2047, now);
                    if ((binding.CleanupCheckpoints & 2048) == 0)
                    {
                        player.Session.Send(new QuestMessage { Identity = player.Identity, Unknown = 0, Action = QuestAction.Delete,
                            Mission = Quest(binding), Unknown1 = 0, Unknown2 = 0, Unknown3 = 0 });
                        binding = Checkpoint(binding, binding.CleanupCheckpoints | 4095, now);
                    }
                    if (_dao.ReadArtifacts(binding.OwnerId, binding.QuestType, binding.QuestInstance).Any(item => item.ContainerType != 0)
                        || !_playfields.Value.TryReleaseMission(binding)) continue;
                    Checkpoint(binding, (1L << 17) - 1, now);
                }
            }
            catch (MissionCommitOutcomeUnknownException exception) { Quarantine(player, exception); }
            catch (Exception exception) { _logger.Error(exception, "Mission lifecycle remains pending at its last durable checkpoint."); }
        }
    }

    GeneratedMissionBinding Checkpoint(GeneratedMissionBinding binding, long mask, long now)
    {
        if (binding.CleanupCheckpoints == mask) return binding;
        var result = _dao.AdvanceCleanup(binding.OwnerId, binding.QuestType, binding.QuestInstance, binding.Version, mask, now);
        if (result.Status != GeneratedMissionResultStatus.Applied) throw new InvalidOperationException("Mission cleanup checkpoint changed; retain reservation.");
        return result.Binding;
    }
    static Identity Quest(GeneratedMissionBinding binding) => new() { Type = (IdentityType)binding.QuestType, Instance = binding.QuestInstance };
}
