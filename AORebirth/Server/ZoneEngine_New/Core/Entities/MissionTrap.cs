namespace ZoneEngine_New.Core.Entities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

[Flags]
public enum MissionTrapState
{
    Armed = 0,
    Detected = 1,
    Revealed = 2,
    Triggered = 4,
    Disarmed = 8,
    Disabled = 16,
    Destroyed = 32
}

/// <summary>Explicit AORebirth decisions where the original server's rules are unavailable.</summary>
public sealed class MissionTrapInteractionPolicy
{
    public const string DisabledMode = "Disabled";
    public const string SkillAtLeastDifficultyMode = "AORebirthSkillAtLeastDifficulty";
    public const string ResourceDifficulty = "ResourceTrapDifficulty";
    public const string ConfiguredDifficulty = "ConfiguredDifficulty";

    public string DetectionMode { get; set; } = DisabledMode;
    public string DisarmMode { get; set; } = DisabledMode;
    public int DetectionDifficulty { get; set; }
    public int DisarmDifficulty { get; set; }
    public string DetectionDifficultySource { get; set; } = ResourceDifficulty;
    public string DisarmDifficultySource { get; set; } = ResourceDifficulty;
    public float DetectionRange { get; set; } = 5;
    public float InteractionRange { get; set; } = 5;
    public bool ConsumeOnTrigger { get; set; } = true;

    [JsonIgnore]
    public bool IsValid => IsMode(DetectionMode) && IsMode(DisarmMode)
        && IsDifficultySource(DetectionDifficultySource) && IsDifficultySource(DisarmDifficultySource)
        && DetectionDifficulty >= 0 && DisarmDifficulty >= 0
        && float.IsFinite(DetectionRange) && DetectionRange > 0
        && float.IsFinite(InteractionRange) && InteractionRange > 0
        // Repeating traps require a separately evidenced activation/cooldown policy.
        && ConsumeOnTrigger;

    public MissionTrapInteractionPolicy Clone() => (MissionTrapInteractionPolicy)MemberwiseClone();

    static bool IsMode(string? mode) => mode is DisabledMode or SkillAtLeastDifficultyMode;
    static bool IsDifficultySource(string? source) => source is ResourceDifficulty or ConfiguredDifficulty;
}

/// <summary>A nonblocking, parented mission trap. The owning population commits every state transition.</summary>
public sealed class MissionTrap : Dynel
{
    const int DisarmEvent = 108;
    const int TriggerEvent = 109;
    const int RevealEvent = 112;
    const byte EnabledFlag = 2;
    const MissionTrapState Inactive = MissionTrapState.Disarmed | MissionTrapState.Disabled | MissionTrapState.Destroyed;
    const MissionTrapState KnownStates = MissionTrapState.Detected | MissionTrapState.Revealed
        | MissionTrapState.Triggered | Inactive;

    readonly Dynel _parent;
    readonly TrapItemFullUpdateMessage _spawn;
    readonly int _nanoId;
    readonly MissionTrapInteractionPolicy _policy;
    readonly Func<MissionTrap, Player, MissionTrapState, bool> _tryCommitState;
    readonly Dictionary<int, IZoneSession> _notifiedPlayers = [];
    readonly List<Player> _players = [];
    readonly object _gate = new();
    volatile bool _retired;

    public MissionTrap(Identity identity, Dynel parent, TrapItemFullUpdateMessage spawn, int nanoId,
        MissionTrapInteractionPolicy policy, MissionTrapState state,
        Func<MissionTrap, Player, MissionTrapState, bool> tryCommitState, Identity? revealedBy = null) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(spawn);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(tryCommitState);
        if (identity.Type != IdentityType.Trap || identity.Instance <= 0 || nanoId <= 0
            || spawn.Identity != identity || spawn.Parent != parent.Identity || !policy.IsValid
            || (state & ~KnownStates) != 0
            || ((state & MissionTrapState.Revealed) != 0
                && (revealedBy is not Identity actor || actor.Type != IdentityType.CanbeAffected || actor.Instance <= 0)))
            throw new ArgumentException("A mission trap requires its exact parent, supported policy and frozen spawn.");
        _parent = parent;
        _spawn = CopySpawn(spawn);
        _nanoId = nanoId;
        _policy = policy.Clone();
        _tryCommitState = tryCommitState;
        State = state;
        RevealedBy = revealedBy ?? Identity.None;
        Position = parent.Position;
        Rotation = parent.Rotation;
        Playfield = parent.Playfield;
        SpawnSource = SpawnSource.ContentPlacement;
        foreach (GameTuple<CharacterStat, uint> stat in _spawn.Stats)
            Stats.Set(stat.Value1, unchecked((int)stat.Value2));
    }

    public Identity ParentIdentity => _parent.Identity;
    internal Dynel ParentDynel => _parent;
    public MissionTrapState State { get; private set; }
    public Identity RevealedBy { get; private set; }
    public bool LastEffectApplied { get; private set; }
    public bool IsArmed => !_retired && (_spawn.TrapFlags & EnabledFlag) != 0 && (State & Inactive) == 0
        && (!_policy.ConsumeOnTrigger || (State & MissionTrapState.Triggered) == 0);
    public Func<Player, bool>? AccessValidator { get; set; }

    public void Retire() => _retired = true;

    public override double GetCollisionRadius() => 0;

    public override MessageBody BuildSpawnMessage()
    {
        lock (_gate)
        {
            TrapItemFullUpdateMessage spawn = CopySpawn(_spawn);
            // Native flag 0x02 is enabled. Clearing it after a consumed trigger is
            // our explicit one-shot policy, not a recovered event-109 consumption rule.
            if (!IsArmed)
                spawn.TrapFlags &= unchecked((byte)~EnabledFlag);
            return spawn;
        }
    }

    public override IEnumerable<MessageBody> BuildSpawnCompanionMessages()
    {
        lock (_gate)
        {
            var messages = new List<MessageBody>();
            // Ordinary event 112 assigns its resource mesh and enables visibility.
            // Replaying the saved reveal after reconstruction is our explicit publication
            // policy, not a visibility bit in TrapFlags or a captured retail reconnect rule.
            if ((State & MissionTrapState.Revealed) != 0)
                messages.Add(CreateEvent(RevealEvent, RevealedBy));
            if ((State & MissionTrapState.Disarmed) != 0)
                messages.Add(new TrapDisarmedMessage { Identity = Identity, Unknown = 0 });
            return messages;
        }
    }

    public bool TryTrigger(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        lock (_gate)
        {
            if (!IsArmed || !CanInteract(player, _policy.InteractionRange))
                return false;
            IItemBuilder items = Playfield!.GetRequiredService<IItemBuilder>();
            try
            {
                if (!NanoRuntime.CanApplyEnvironmentalPassiveBuff(player, _nanoId, items))
                    return false;
            }
            catch (Exception exception)
            {
                Playfield.GetService<IZoneLogger>()?.Error(exception, "Mission trap effect validation failed: " + Identity);
                return false;
            }
            if (!Commit(player, State | MissionTrapState.Triggered))
                return false;

            bool eventPublished = false;
            try
            {
                LastEffectApplied = NanoRuntime.TryApplyEnvironmentalPassiveBuff(Identity, player, _nanoId,
                    items, DateTime.UtcNow, () =>
                    {
                        PublishEvent(TriggerEvent, player);
                        eventPublished = true;
                    });
                if (!eventPublished)
                    PublishEvent(TriggerEvent, player);
                if (!LastEffectApplied)
                    Playfield.GetService<IZoneLogger>()?.Warn("Committed mission trap could not land its passive effect; it remains consumed: " + Identity);
            }
            catch (Exception exception)
            {
                // State committed before publication. Never retry a potentially delivered effect.
                Playfield.GetService<IZoneLogger>()?.Error(exception,
                    "Committed mission trap effect/publication failed; it remains consumed: " + Identity);
            }
            return true;
        }
    }

    public bool TryDetect(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        lock (_gate)
        {
            if (_policy.DetectionMode != MissionTrapInteractionPolicy.SkillAtLeastDifficultyMode
                || !IsArmed || !CanInteract(player, _policy.DetectionRange)
                || WasNotified(player)
                || !TryDifficulty(_policy.DetectionDifficultySource, _policy.DetectionDifficulty, out int difficulty)
                || player.Stats.GetOrZero(CharacterStat.Perception) < difficulty)
                return false;
            if ((State & MissionTrapState.Detected) == 0 && !Commit(player, State | MissionTrapState.Detected))
                return false;

            // Native action 190 reads its SECOND identity, represented by Parameter1/2.
            // Zero scalar/first identity are AORebirth's unused-field policy, not a captured 190 control.
            player.Session!.Send(new CharacterActionMessage
            {
                Identity = player.Identity, Unknown = 0, Action = CharacterActionType.TrapDetected, Unknown1 = 0,
                Target = Identity.None, Parameter1 = (int)Identity.Type, Parameter2 = Identity.Instance, Unknown2 = 0
            });
            _notifiedPlayers[player.Identity.Instance] = player.Session;
            return true;
        }
    }

    /// <summary>Separate authoritative reveal; detection feedback alone never calls this.</summary>
    public bool TryReveal(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        lock (_gate)
        {
            if (_policy.DetectionMode != MissionTrapInteractionPolicy.SkillAtLeastDifficultyMode
                || !IsArmed || !CanInteract(player, _policy.DetectionRange)
                || !WasNotified(player)
                || (State & MissionTrapState.Revealed) != 0
                || !Commit(player, State | MissionTrapState.Revealed))
                return false;
            RevealedBy = player.Identity;
            PublishEvent(RevealEvent, player);
            return true;
        }
    }

    public bool TryDisarm(Player player, Item tool)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(tool);
        lock (_gate)
        {
            if (_policy.DisarmMode != MissionTrapInteractionPolicy.SkillAtLeastDifficultyMode
                || !IsArmed || !CanInteract(player, _policy.InteractionRange)
                || !player.Inventory.IsHydrated || tool.Locked || !tool.Can(CanFlags.DisarmTraps)
                || !player.Inventory.Inventory.Content.Values.Any(item => ReferenceEquals(item, tool))
                || !TryDifficulty(_policy.DisarmDifficultySource, _policy.DisarmDifficulty, out int difficulty)
                || player.Stats.GetOrZero(CharacterStat.TrapDisarm) < difficulty
                || !Commit(player, State | MissionTrapState.Disarmed))
                return false;
            // Tool consumption and failed-disarm penalties are unknown and are not invented.
            PublishEvent(DisarmEvent, player);
            Playfield!.GetRequiredService<PlayfieldLocality>().Announce(this,
                new TrapDisarmedMessage { Identity = Identity, Unknown = 0 }, includeSelf: true);
            return true;
        }
    }

    public override void Tick(double deltaTime)
    {
        base.Tick(deltaTime);
        if (_policy.DetectionMode != MissionTrapInteractionPolicy.SkillAtLeastDifficultyMode || !IsArmed || Playfield == null)
            return;
        Playfield.GetRequiredService<DynelRegistry>().CopyPlayers(_players);
        foreach (Player player in _players)
            TryDetect(player);
    }

    bool Commit(Player player, MissionTrapState state)
    {
        try
        {
            if (!_tryCommitState(this, player, state))
                return false;
        }
        catch (Exception exception)
        {
            // The commit may have succeeded. Withhold all effects and retire this
            // projection until reconstruction; a chest's completed open must remain valid.
            _retired = true;
            Playfield?.GetService<IZoneLogger>()?.Error(exception,
                "Mission trap transition outcome is unknown; reconstruction required: " + Identity);
            return false;
        }
        State = state;
        return true;
    }

    bool WasNotified(Player player) => _notifiedPlayers.TryGetValue(player.Identity.Instance, out IZoneSession? session)
        && ReferenceEquals(session, player.Session);

    bool TryDifficulty(string source, int configured, out int difficulty)
    {
        difficulty = configured;
        if (source == MissionTrapInteractionPolicy.ConfiguredDifficulty)
            return true;
        if (!_spawn.Stats.Any(stat => stat.Value1 == CharacterStat.TrapDifficulty))
            return false;
        difficulty = Stats.GetOrZero(CharacterStat.TrapDifficulty);
        return difficulty >= 0;
    }

    bool CanInteract(Player player, float range)
    {
        if (_retired || Playfield == null || player.IsDead || player.IsPersistenceQuarantined
            || player.Session?.State != SessionState.InPlay || !ReferenceEquals(player.Session.Player, player)
            || !ReferenceEquals(player.Playfield, Playfield) || !ReferenceEquals(_parent.Playfield, Playfield)
            || _parent.GetEdgeDistanceTo(player) > range || AccessValidator?.Invoke(player) == false)
            return false;
        var registry = Playfield.GetRequiredService<DynelRegistry>();
        return registry.TryGet(Identity, out Dynel? trap) && ReferenceEquals(trap, this)
            && registry.TryGet(ParentIdentity, out Dynel? parent) && ReferenceEquals(parent, _parent);
    }

    void PublishEvent(int eventId, Player actor)
        => Playfield!.GetRequiredService<PlayfieldLocality>().Announce(this,
            CreateEvent(eventId, actor.Identity),
            includeSelf: true);

    ActionMessage CreateEvent(int eventId, Identity actor)
        // All thirteen retained event109 controls carry pass-on zero. Other optional
        // state notifications use the same explicit AORebirth envelope policy.
        => new() { Identity = Identity, Unknown = 0, ActionCode = 1, ActionIdentity = eventId, Target = actor };

    static TrapItemFullUpdateMessage CopySpawn(TrapItemFullUpdateMessage value) => new()
    {
        Identity = value.Identity, Unknown = value.Unknown, CommonMarker = value.CommonMarker,
        Parent = value.Parent, Coordinate = value.Coordinate, Heading = value.Heading,
        PlayfieldId = value.PlayfieldId, NeutralIdentity = value.NeutralIdentity,
        HeaderStatBytes = value.HeaderStatBytes,
        Stats = value.Stats.Select(stat => new GameTuple<CharacterStat, uint>
            { Value1 = stat.Value1, Value2 = stat.Value2 }).ToArray(),
        OpaqueData = (byte[])value.OpaqueData.Clone(), TrapMarker = value.TrapMarker,
        Raw74 = value.Raw74, Identity78 = value.Identity78, Identity80 = value.Identity80, TrapFlags = value.TrapFlags
    };
}
