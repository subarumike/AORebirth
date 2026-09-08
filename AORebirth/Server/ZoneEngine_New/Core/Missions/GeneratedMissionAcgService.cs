namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using Vector3 = AORebirth.Core.Vector.Vector3;

public sealed record GeneratedMissionLoginPlan(int PlayfieldId, Vector3? Position, GeneratedMissionWorld? World, bool RequiresExteriorReturn);

/// <summary>SQL-owned accepted bundle/identity planner; never loads a Legacy sidecar or capture directory.</summary>
public sealed partial class GeneratedMissionAcgService
{
    readonly IGeneratedMissionDao _dao;
    readonly GeneratedMissionService _missions;
    readonly IGeneratedMissionNpcFactory _npcs;
    readonly IItemBuilder _items;
    readonly IItemTemplateCatalog _templates;
    readonly IItemInstanceIdAllocator _ids;
    readonly Lazy<PlayfieldManager> _playfields;
    readonly IZoneLogger _logger;
    readonly MissionAcgLayoutCatalog _catalog;

    // Exact nonselectable Legacy source PFs and Nascence leases; they must not be allocated as new worlds.
    static readonly HashSet<int> Reserved = [1441800,1443840,1460226,1456133,1419310,1419335,1419382,1419349,1441804,
        0x208038,0x2080D9,0x209103,0x2090C1,0x1F900B,0x208047];

    public GeneratedMissionAcgService(IGeneratedMissionDao dao, GeneratedMissionService missions,
        IGeneratedMissionNpcFactory npcs, IItemBuilder items, IItemInstanceIdAllocator ids,
        Lazy<PlayfieldManager> playfields, IZoneLogger logger, IItemTemplateCatalog templates)
    {
        _dao = dao; _missions = missions; _npcs = npcs; _items = items; _ids = ids; _playfields = playfields; _logger = logger; _templates = templates;
        // These are the same five selectable bundles used by Legacy; its eight older
        // audit-only bundles remain explicitly reserved/nonselectable, not promoted.
        _catalog = MissionAcgLayoutCatalogLoader.Load(MissionAcgCapturedLayoutCatalog.CreateBundles(), []);
    }

    public GeneratedMissionLoginPlan ResolveLogin(int characterId, int storedPlayfield)
    {
        var binding = _dao.ReadAccepted(characterId).SingleOrDefault(value => value.LivePlayfield == storedPlayfield)
            ?? throw new InvalidOperationException("Stored mission playfield has no exact owned SQL binding.");
        if (binding.State != GeneratedMissionState.Active || binding.ExpiresAtUtcTicks <= DateTime.UtcNow.Ticks)
            return new(binding.Offer.DestinationPlayfield, new Vector3(binding.Offer.DestinationX, binding.Offer.DestinationY, binding.Offer.DestinationZ), null, true);
        return new(storedPlayfield, null, PrepareWorld(binding), false);
    }

    public GeneratedMissionResult Accept(Player player, Identity originalOffer)
    {
        lock (player.PersistenceGate)
        {
            if (player.IsPersistenceQuarantined || player.Session is not IGameTimeSession { GameTimeSynchronizedAtUtc: not null })
                return Rejected("Mission session or synchronized clock is unavailable.");
            try
            {
                var previous = _dao.ReadAccepted(player.Identity.Instance).SingleOrDefault(value => value.OfferType == (int)originalOffer.Type && value.OfferInstance == originalOffer.Instance);
                if (previous != null)
                {
                    if (previous.State == GeneratedMissionState.Active && previous.ExpiresAtUtcTicks > DateTime.UtcNow.Ticks) ReplayJournal(player, previous);
                    return new() { Status = GeneratedMissionResultStatus.AlreadyApplied, Binding = previous };
                }
                var offer = _dao.ReadOffers(player.Identity.Instance).SingleOrDefault(value => value.OfferType == (int)originalOffer.Type && value.OfferInstance == originalOffer.Instance
                    && value.State == GeneratedMissionState.Offered && value.ExpiresAtUtcTicks > DateTime.UtcNow.Ticks);
                if (offer == null) return Rejected("No current owned offer with that exact identity.");
                var state = DecodeFrozenOffer(offer);
                int seed = Seed(offer);
                var owner = new MissionAcgIdentityRecord((int)player.Identity.Type, player.Identity.Instance);
                var bundle = MissionAcgLayoutSelector.Select(_catalog, new MissionAcgSelectionInput(seed, (MissionRollType)offer.MissionType, offer.Quality, owner));
                int quest = _dao.ReserveIdentities("quest", 1);
                int livePf = ReservePlayfield();
                int key = _ids.Allocate();
                long now = DateTime.UtcNow.Ticks;
                var binding = new GeneratedMissionBinding
                {
                    OwnerId = player.Identity.Instance, OfferType = offer.OfferType, OfferInstance = offer.OfferInstance,
                    QuestType = 0xDAC3, QuestInstance = quest, KeyInstance = key, BundleId = bundle.LayoutId,
                    BundleSha256 = bundle.GeneratorPayloadSha256, BuildingType = bundle.BuildingIdentity.Type, BuildingInstance = bundle.BuildingIdentity.Instance,
                    LivePlayfield = livePf, State = GeneratedMissionState.Active, AcceptedAtUtcTicks = now,
                    ExpiresAtUtcTicks = now + TimeSpan.TicksPerHour * 48, UpdatedAtUtcTicks = now, Version = 1, Offer = offer, RequiredCount = 1
                };
                var materialized = Materialize(binding);
                var objectiveSlot = bundle.ObjectiveSlots.Single();
                var objective = materialized.Objects.Single(value => value.Identity.CapturedIdentity.Equals(objectiveSlot.CapturedIdentity));
                binding.ObjectiveType = objective.Identity.RuntimeIdentity.Type; binding.ObjectiveInstance = objective.Identity.RuntimeIdentity.Instance;
                binding.ObjectiveTemplateId = objectiveSlot.TemplateId; binding.ObjectiveInteraction = (int)MissionAcgObjectiveContract.InteractionFor((MissionRollType)offer.MissionType);
                var objects = InitialObjects(binding, materialized);
                var keyItem = _items.Create(28577, 28577, 1, ItemSource.Other, 1, key, new Identity { Type = (IdentityType)0xC76D, Instance = key });
                var artifacts = new List<Item>();
                if (offer.MissionType == (int)MissionRollType.RepairMachine) artifacts.Add(CreateRepairComponent());
                artifacts.Add(keyItem);
                var acceptance = new GeneratedMissionAcceptance
                {
                    OwnerId = binding.OwnerId, OfferType = binding.OfferType, OfferInstance = binding.OfferInstance,
                    QuestType = binding.QuestType, QuestInstance = binding.QuestInstance, KeyInstance = key,
                    BundleId = binding.BundleId, BundleSha256 = binding.BundleSha256, BuildingType = binding.BuildingType, BuildingInstance = binding.BuildingInstance,
                    LivePlayfield = binding.LivePlayfield, ObjectiveType = binding.ObjectiveType, ObjectiveInstance = binding.ObjectiveInstance,
                    ObjectiveTemplateId = binding.ObjectiveTemplateId, ObjectiveInteraction = binding.ObjectiveInteraction, RequiredCount = 1,
                    AcceptedAtUtcTicks = now, ExpiresAtUtcTicks = binding.ExpiresAtUtcTicks, Objects = objects
                };
                var result = _missions.Accept(player, acceptance, artifacts);
                if (result.Status == GeneratedMissionResultStatus.Applied)
                {
                    try
                    {
                        foreach (var item in artifacts)
                            GeneratedMissionArtifactProjection.Send(player, item, player.Inventory.Inventory.Content.Single(entry => entry.Value.InstanceId == item.InstanceId).Key,
                                true, item.InstanceId == key ? "Mission key" : "Mission Repair Kit");
                        ReplayJournal(player, result.Binding);
                    }
                    catch (Exception exception)
                    {
                        // SQL and memory are committed. Do not report a retryable rejection
                        // after a partial wire publication; reconnect restores the same binding.
                        Quarantine(player, exception);
                    }
                }
                return result;
            }
            catch (MissionCommitOutcomeUnknownException exception) { Quarantine(player, exception); return Rejected("Mission commit requires reconciliation."); }
            catch (Exception exception) { _logger.Error(exception, "Generated mission acceptance failed closed."); return Rejected("Accepted bundle/artifact authority is unavailable."); }
        }
    }

    public void ReplayJournal(Player player)
    {
        foreach (var binding in _missions.ReadAccepted(player).Where(value => value.State == GeneratedMissionState.Active && value.ExpiresAtUtcTicks > DateTime.UtcNow.Ticks))
            ReplayJournal(player, binding);
    }

    void ReplayJournal(Player player, GeneratedMissionBinding binding)
    {
        if (player.Session is not IGameTimeSession { GameTimeSynchronizedAtUtc: { } synchronized }) return;
        var materialized = Materialize(binding);
        var state = DecodeFrozenOffer(binding.Offer);
        var slot = materialized.Bundle.ObjectiveSlots.Single();
        var objective = new MissionAcgObjectiveBinding(MissionAcgObjectiveBinding.CurrentFormatVersion,
            materialized.BindingRecord.Binding.AcceptedQuestIdentity, materialized.BindingRecord.Binding.OwnerIdentity,
            null, true, (MissionRollType)binding.Offer.MissionType, binding.LivePlayfield, binding.BundleId, binding.BundleSha256,
            materialized.Bundle.BuildingIdentity, slot.Slot, slot.CapturedIdentity,
            new MissionAcgIdentityRecord(binding.ObjectiveType, binding.ObjectiveInstance), slot.TemplateId, slot.Name,
            (MissionAcgObjectiveInteraction)binding.ObjectiveInteraction,
            binding.Offer.MissionType == 4 ? materialized.BindingRecord.Binding.IssuingTerminalIdentity : null,
            binding.Offer.MissionType == 4 ? slot.TemplateId : 0, 0);
        var objectiveState = new MissionAcgObjectiveState(MissionAcgObjectiveLifecycle.Exposed, MissionAcgCompletionPhase.None,
            binding.MissionItem == null ? null : new MissionAcgIdentityRecord(binding.MissionItem.ItemType, binding.MissionItem.InstanceId),
            0, 0, 0, 0, 0, 0, MissionAcgGrantState.NotStarted, MissionAcgGrantState.NotStarted, MissionAcgGrantState.NotStarted,
            string.Empty, string.Empty, string.Empty, 0, false, false, false, false, false, DateTime.UtcNow);
        int expiry = MissionRollService.ResolveClientExpirySeconds(synchronized, DateTime.UtcNow, new DateTime(binding.ExpiresAtUtcTicks, DateTimeKind.Utc));
        var qfu = MissionAcgAcceptedQfuBuilder.Build(player.Identity, state, materialized.BindingRecord.Binding,
            new MissionAcgObjectiveRecord(objective, objectiveState, string.Empty), expiry);
        player.Session.Send(qfu.Message);
    }

    public bool TryEnter(Player player, Identity entrance, int low, int high)
    {
        lock (player.PersistenceGate)
        {
            if (player.IsPersistenceQuarantined || player.Playfield == null) return false;
            var matches = _dao.ReadAccepted(player.Identity.Instance).Where(binding => binding.State == GeneratedMissionState.Active && binding.ExpiresAtUtcTicks > DateTime.UtcNow.Ticks
                && binding.Offer.EntranceType == (int)entrance.Type && binding.Offer.EntranceInstance == entrance.Instance
                && binding.Offer.EntranceLow == low && binding.Offer.EntranceHigh == high
                && binding.Offer.DestinationPlayfield == player.Playfield.Identity.Instance
                && player.Inventory.Inventory.Content.Values.Any(item => item.InstanceId == binding.KeyInstance && item.LowId == 28577 && item.HighId == 28577)).ToArray();
            if (matches.Length != 1) return false;
            var b = matches[0];
            if (!WithinExteriorMarker(player, b)) return false;
            var world = PrepareWorld(b);
            player.Session?.TransferToPlayfield(_playfields.Value.GetOrCreateMission(world), world.Spawn);
            return true;
        }
    }

    public GeneratedMissionWorld PrepareWorld(GeneratedMissionBinding binding)
        => new(binding, Materialize(binding), _dao.ReadObjects(binding.OwnerId, binding.QuestType, binding.QuestInstance), _npcs, TryUse);

    bool TryUse(Player player, GeneratedMissionObject stale)
    {
        lock (player.PersistenceGate)
        {
            if (player.IsPersistenceQuarantined || player.Playfield is not MissionPlayfield world || world.World.OwnerId != player.Identity.Instance) return false;
            var binding = _dao.ReadAccepted(player.Identity.Instance, stale.QuestType, stale.QuestInstance);
            if (binding == null || binding.LivePlayfield != player.Playfield.Identity.Instance) return false;
            var state = _dao.ReadObjects(player.Identity.Instance, stale.QuestType, stale.QuestInstance).Single(value => value.RuntimeType == stale.RuntimeType && value.RuntimeInstance == stale.RuntimeInstance);
            if (state.Kind == (int)MissionAcgRuntimeObjectKind.Exit)
            {
                player.Session?.TransferToPlayfield(_playfields.Value.GetOrCreate(world.World.ExteriorPlayfield), world.World.ExteriorPosition);
                return true;
            }
            // Exact owner exit remains available after completion/expiry; only further
            // objective/world mutations require a live mission.
            if (binding.State != GeneratedMissionState.Active || binding.ExpiresAtUtcTicks <= DateTime.UtcNow.Ticks) return false;
            if (state.Kind == (int)MissionAcgRuntimeObjectKind.StaticObjective) return TryPickup(player, binding, state);
            if (state.Kind == (int)MissionAcgRuntimeObjectKind.Chest)
            {
                if (state.IsOpen) return true;
                state.IsOpen = true;
            }
            else if (state.Kind == (int)MissionAcgRuntimeObjectKind.Door && !state.IsLocked) state.IsOpen = !state.IsOpen;
            else return false;
            try { return _dao.UpdateObjects(player.Identity.Instance, state.QuestType, state.QuestInstance, [state], DateTime.UtcNow.Ticks).Status == GeneratedMissionResultStatus.Applied; }
            catch (MissionCommitOutcomeUnknownException exception) { Quarantine(player, exception); return false; }
        }
    }

    IList<GeneratedMissionObject> InitialObjects(GeneratedMissionBinding binding, MissionAcgMaterializedInstance instance)
    {
        var result = new List<GeneratedMissionObject>();
        foreach (var source in instance.Objects)
        {
            var state = new GeneratedMissionObject
            {
                OwnerId = binding.OwnerId, QuestType = binding.QuestType, QuestInstance = binding.QuestInstance,
                RuntimeType = source.Identity.RuntimeIdentity.Type, RuntimeInstance = source.Identity.RuntimeIdentity.Instance,
                CapturedType = source.Identity.CapturedIdentity.Type, CapturedInstance = source.Identity.CapturedIdentity.Instance,
                Kind = (int)source.Identity.Kind, TemplateId = source.TemplateId,
                X = source.Position.X, Y = source.Position.Y, Z = source.Position.Z,
                HeadingX = source.Heading.X, HeadingY = source.Heading.Y, HeadingZ = source.Heading.Z, HeadingW = source.Heading.W,
                Version = 1, UpdatedAtUtcTicks = binding.AcceptedAtUtcTicks
            };
            if (source.Identity.Kind is MissionAcgRuntimeObjectKind.ObjectiveNpc or MissionAcgRuntimeObjectKind.AmbientNpc)
            {
                var evidence = new GeneratedMissionNpcEvidence(source, instance.Bundle, binding.Offer.Quality, binding.Offer.MissionType);
                // Same current Legacy BuildState policy: the accepted deterministic seed and
                // captured slot/identity choose difficulty once, before the binding transaction.
                var rng = new Random(unchecked(Seed(binding.Offer) ^ evidence.CapturedSlot * 397 ^ evidence.CapturedInstance));
                state.Level = MissionNpcDifficultyPolicy.ResolveLevel(binding.Offer.Quality, rng);
                state.MaxHealth = MissionNpcDifficultyPolicy.ResolveHealth(state.Level.Value, rng);
                state.CurrentHealth = state.MaxHealth;
                var npc = _npcs.Create(evidence, state, _items);
                if (npc.Stats.GetOrZero(CharacterStat.Health) != state.CurrentHealth || npc.Stats.GetOrZero(CharacterStat.MaxHealth) != state.MaxHealth)
                    throw new InvalidOperationException("Mission NPC adapter did not preserve the frozen difficulty health policy.");
            }
            else if (new ZoneMessageCodec().Deserialize(source.CopyPacket())?.Body == null)
                throw new InvalidOperationException("Accepted static object wire is unavailable.");
            result.Add(state);
        }
        return result;
    }

    MissionAcgMaterializedInstance Materialize(GeneratedMissionBinding b)
    {
        var offer = b.Offer;
        var bundle = _catalog.FindByLayoutId(b.BundleId) ?? throw new InvalidOperationException("Accepted bundle is absent.");
        var binding = new MissionAcgInstanceBinding(MissionAcgInstanceBinding.CurrentFormatVersion,
            new(b.QuestType, b.QuestInstance), new(b.OfferType, b.OfferInstance), new(50000, b.OwnerId),
            b.TeamInstance == 0 ? null : new(b.TeamType, b.TeamInstance), (MissionRollType)offer.MissionType, offer.Quality, Seed(offer),
            new(0xC76D, b.KeyInstance), new(offer.EntranceType, offer.EntranceInstance), offer.EntranceLow, offer.EntranceHigh,
            offer.DestinationX, offer.DestinationY, offer.DestinationZ, new(offer.IssuingTerminalType, offer.IssuingTerminalInstance),
            b.BundleId, b.BundleSha256, new(b.BuildingType, b.BuildingInstance), b.LivePlayfield,
            new DateTime(b.AcceptedAtUtcTicks, DateTimeKind.Utc), new DateTime(b.ExpiresAtUtcTicks, DateTimeKind.Utc), b.TeamInstance == 0);
        var state = new MissionAcgInstanceState(MissionAcgLifecycleState.Active, MissionAcgCleanupState.None, DateTime.UtcNow, null);
        if (!MissionAcgRuntimeMaterializer.TryMaterialize(new(binding, state, string.Empty), bundle, null, DateTime.UtcNow, out var instance, out string failure))
            throw new InvalidOperationException(failure);
        return instance;
    }

    static QuestInfo DecodeFrozenOffer(GeneratedMissionOffer offer)
    {
        var roll = MissionRollService.DeserializeBody(offer.FrozenWireBody);
        if (roll.Identity.Instance != offer.OwnerId || offer.OfferIndex < 0 || offer.OfferIndex >= roll.QuestInfos.Length)
            throw new InvalidOperationException("Frozen roll owner/index mismatch.");
        var state = roll.QuestInfos[offer.OfferIndex];
        var action = state.QuestActions.Single();
        var reward = state.ItemRewards?.SingleOrDefault();
        if ((int)state.QuestIdentity.Type != offer.OfferType || state.QuestIdentity.Instance != offer.OfferInstance || state.Quality != offer.Quality
            || (int)MissionTypeCatalog.TypeFromIcon(state.MissionIconId) != offer.MissionType
            || (int)action.Playfield.Type != offer.DestinationType || action.Playfield.Instance != offer.DestinationInstance
            || !action.X.Equals(offer.DestinationX) || !action.Y.Equals(offer.DestinationY) || !action.Z.Equals(offer.DestinationZ)
            || action.Unknown18 != offer.EntranceLow || action.Unknown19 != offer.EntranceHigh
            || state.CashReward != offer.CashReward || state.ExperienceReward != offer.ExperienceReward
            || (state.ShortInfo ?? string.Empty) != offer.Title || (state.Info ?? string.Empty) != offer.Description
            || (reward?.LowId ?? 0) != offer.RewardLowId || (reward?.HighId ?? 0) != offer.RewardHighId
            || (reward?.Quality ?? 0) != offer.RewardQuality || (reward == null ? 0 : 1) != offer.RewardCount
            || (int)roll.MissionTerminalIdentity.Type != offer.IssuingTerminalType
            || roll.MissionTerminalIdentity.Instance != offer.IssuingTerminalInstance)
            throw new InvalidOperationException("Derived wire projection differs from authoritative typed offer.");
        return state;
    }
    int ReservePlayfield()
    {
        for (int attempt = 0; attempt <= 65535; attempt++)
        {
            int value = _dao.ReserveIdentities("playfield", 1);
            if (!Reserved.Contains(value) && !_catalog.Layouts.Any(bundle => bundle.SourcePlayfield2 == value)) return value;
        }
        throw new InvalidOperationException("No governed mission playfield lease remains.");
    }
    static int Seed(GeneratedMissionOffer offer)
    {
        unchecked
        {
            int value = 17; value = value * 31 + offer.OfferType; value = value * 31 + offer.OfferInstance;
            value = value * 31 + 50000; value = value * 31 + offer.OwnerId; value = value * 31 + offer.MissionType; return value * 31 + offer.Quality;
        }
    }
    static void SendKeyProjection(Player player, GeneratedMissionBinding binding)
    {
        GameTuple<CharacterStat,uint> Stat(CharacterStat stat, uint value) => new() { Value1 = stat, Value2 = value };
        player.Session?.Send(new SimpleItemFullUpdateMessage
        {
            Identity = new() { Type = (IdentityType)0xC76D, Instance = binding.KeyInstance }, Unknown = 0, MsgVersion = 0x0B,
            Identitytype = (int)player.Identity.Type, Instance = player.Identity.Instance, Playfield = player.Playfield!.Identity.Instance,
            Unknown1 = new() { Type = (IdentityType)0xF424F, Instance = 0 }, Unknown2 = 0x71, Unknown3 = 0x6F,
            Name = "Mission key\0", Stats = [Stat(CharacterStat.Flags,0x80000205),Stat(CharacterStat.StaticInstance,28577),
                Stat(CharacterStat.ACGItemLevel,1),Stat(CharacterStat.ACGItemTemplateID,28577),Stat(CharacterStat.ACGItemTemplateID2,28577),Stat(CharacterStat.MultipleCount,1)]
        });
        int slot = player.Inventory.Inventory.Content.Single(entry => entry.Value.InstanceId == binding.KeyInstance).Key;
        player.Session?.Send(new ContainerAddItemMessage
        {
            Identity = player.Identity, Unknown = 0,
            SourceContainer = new() { Type = IdentityType.OverflowWindow, Instance = 0 },
            Target = new() { Type = IdentityType.OverflowWindow, Instance = player.Identity.Instance }, TargetPlacement = 0x6F
        });
        player.Session?.Send(new TemplateActionMessage
        {
            Identity = player.Identity, Unknown = 0, ItemLowId = 28577, ItemHighId = 28577, Quality = 1,
            Unknown1 = 1, Unknown2 = 87, Placement = new() { Type = IdentityType.OverflowWindow, Instance = 0 }, Unknown3 = 0, Unknown4 = 0
        });
        player.Session?.Send(new TemplateActionMessage
        {
            Identity = player.Identity, Unknown = 0, ItemLowId = 28577, ItemHighId = 28577, Quality = 1,
            Unknown1 = 1, Unknown2 = 3, Placement = new() { Type = IdentityType.Inventory, Instance = slot },
            Unknown3 = (int)player.Identity.Type, Unknown4 = player.Identity.Instance
        });
    }
    void Quarantine(Player player, Exception exception) { player.QuarantinePersistence(); player.Session?.Close(); _logger.Error(exception, "Mission world commit outcome requires reconciliation."); }
    static GeneratedMissionResult Rejected(string reason) => new() { Status = GeneratedMissionResultStatus.Rejected, Reason = reason };
}
