namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Ai;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Movement;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using Quaternion = AORebirth.Core.Vector.Quaternion;
using Vector3 = AORebirth.Core.Vector.Vector3;

/// <summary>
/// One of an NPC's events (NpcTemplates.json "Events"): timed steps when something happens to it. The Wounded
/// Dockworker's moans and his rescue on the Health Regeneration Stim; the Omni-AF Private under Call to Arms 297247
/// (loyal when charmed, "a dead clammer?" at Henri Zure, the arrest and both leaving); the Burning Cleaning Robot's
/// flames and the Malfunctioning Cleaning Robot's sparks and self-destruct. The item and nano data carry none of this;
/// it is live's, from the captures.
/// </summary>
public sealed class NpcEventDefinition
{
    public const string Spawned = "Spawned";
    public const string ItemUsed = "ItemUsed";
    public const string Charmed = "Charmed";
    public const string Replaced = "Replaced";
    public const string Ended = "Ended";
    public const string FightStarted = "FightStarted";
    public const string FightEnded = "FightEnded";

    public const string Any = "Any";
    public const string Idle = "Idle";
    public const string Fighting = "Fighting";

    /// <summary>
    /// What starts it: Spawned (it spawned), ItemUsed (a player used one of <see cref="Items"/> on it), Charmed
    /// (CharmNpc landed), Replaced (a fresh copy of <see cref="Nano"/> landed over a running one), Ended
    /// (<see cref="Nano"/> left its NCU), FightStarted or FightEnded (<see cref="Character.IsInCombat"/> turned on or off).
    /// </summary>
    public string On { get; set; } = string.Empty;

    /// <summary>
    /// The state it plays in, checked each time it plays: Any, Idle (not in combat) or Fighting. Like
    /// <see cref="IfStat"/>, a play in the wrong state is skipped and a repeat keeps its timer.
    /// </summary>
    public string While { get; set; } = Any;

    public int Nano { get; set; }

    public int[] Items { get; set; } = [];

    /// <summary>
    /// With <see cref="IfValue"/>: only when the NPC's stat has that value - live's own flag in the item data, stat 62
    /// (the stim works on a Dockworker with 0; Call to Arms resolves where Henri Zure set it to 1).
    /// </summary>
    public int? IfStat { get; set; }

    public int IfValue { get; set; }

    /// <summary>How long after its trigger it first plays, picked at random in this range (0: at once).</summary>
    public int StartMinMs { get; set; }

    public int StartMaxMs { get; set; }

    /// <summary>
    /// With a value above 0 it plays again this long after it last started, picked at random: for the NPC's life, or
    /// after FightStarted until the fight ends (FightEnded: until the next fight starts).
    /// </summary>
    public int RepeatMinMs { get; set; }

    public int RepeatMaxMs { get; set; }

    public NpcEventStep[] Steps { get; set; } = [];
}

/// <summary>One step of an event, <see cref="DelayMs"/> after it plays; every field set is done, in this order.</summary>
public sealed class NpcEventStep
{
    public int DelayMs { get; set; }

    /// <summary>Spawn hash of a nearby NPC who does the step instead (Vanguard Henri Zure "VAHE": "Noo!").</summary>
    public string? Speaker { get; set; }

    public int? SetStat { get; set; }

    public int SetValue { get; set; }

    /// <summary>Said in vicinity chat (/say range).</summary>
    public string Say { get; set; } = string.Empty;

    /// <summary>One of these said at random in vicinity chat (the Dockworker's moans).</summary>
    public string[] SayOneOf { get; set; } = [];

    /// <summary>Gets up from sitting, sleeping or lounging (CharacterAction StandUp, as live).</summary>
    public bool StandUp { get; set; }

    /// <summary>MovementState it settles into: 8 sit, 11 sleep, 12 lounge (the Dockworker lies back down).</summary>
    public int Pose { get; set; }

    /// <summary>Health set to this percent of its maximum, shown as a heal or a hit of <see cref="DamageType"/>.</summary>
    public int HealthPercent { get; set; }

    public int DamageType { get; set; }

    /// <summary>Turns toward the player of the event.</summary>
    public bool FacePlayer { get; set; }

    /// <summary>Back onto the spot it spawned on.</summary>
    public bool ReturnHome { get; set; }

    /// <summary>CharacterAction it plays (live: 100 after the Dockworker stands, 86 as he lies down), with <see cref="ActionTarget"/>.</summary>
    public int Action { get; set; }

    public int ActionTarget { get; set; }

    /// <summary>Animation id played (NpcSocialAnim).</summary>
    public int Anim { get; set; }

    /// <summary>GfxEffect played on it (43101 as the Private and Henri Zure leave, 43121 the robots' flames).</summary>
    public int Gfx { get; set; }

    /// <summary>It leaves as if it had died; its spawn point brings it back on its usual timer.</summary>
    public bool Despawn { get; set; }
}

/// <summary>How an NPC is at spawn (NpcTemplates.json "SpawnState"): the Wounded Dockworker sitting at 37 % health.</summary>
public sealed class NpcSpawnState
{
    /// <summary>MovementState it spawns in (8 sit), 0 standing.</summary>
    public int Pose { get; set; }

    /// <summary>Spawn health as a percent of its maximum; health then changes only by its events (no regeneration).</summary>
    public int HealthPercent { get; set; }
}

/// <summary>Plays this playfield's NPC events and spawn states (see <see cref="NpcEventDefinition"/>).</summary>
internal sealed class NpcEventService(Playfield playfield, DynelRegistry registry)
{
    const float SpeakerRange = 30f;
    const double ThinkSeconds = 0.1;

    sealed class Tracked
    {
        public Vector3 Spot = new(0, 0, 0);

        /// <summary><see cref="Character.IsInCombat"/> at the last think, for FightStarted and FightEnded.</summary>
        public bool Fighting;

        /// <summary>Events waiting to play, a start delay or a repeat: when each plays next and for which player.</summary>
        public readonly List<(NpcEventDefinition Definition, DateTime NextUtc, Player? Player)> Timed = new();
    }

    readonly Dictionary<NpcCharacter, Tracked> _npcs = new();
    readonly List<(DateTime At, NpcCharacter Npc, Player? Player, NpcEventStep Step)> _queue = new();
    DateTime _nextThinkUtc;

    /// <summary>Puts a freshly spawned NPC in its <see cref="NpcSpawnState"/>. Runs before its first spawn packet.</summary>
    internal static void Prepare(NpcCharacter npc)
    {
        if (npc.MobTemplate?.SpawnState is not { } state)
            return;
        if (state.Pose != 0)
            npc.Motor.ApplyAction(PoseAction(state.Pose));
        if (state.HealthPercent > 0)
        {
            npc.Stats.Set(CharacterStat.Health, PercentHealth(npc, state.HealthPercent));
            npc.RegenHeld = true;
        }
    }

    /// <summary>Remembers a spawned NPC with events: where it stands, and starts its Spawned events.</summary>
    internal void Track(NpcCharacter npc)
    {
        if (npc.MobTemplate?.Events is not { Count: > 0 })
            return;
        _npcs[npc] = new Tracked
        {
            Spot = new Vector3(npc.Position.x, npc.Position.y, npc.Position.z),
            Fighting = npc.IsInCombat
        };
        Start(npc, null, d => d.On == NpcEventDefinition.Spawned);
    }

    /// <summary>An item used on a character (ItemUseService): its ItemUsed event when it has one for that item.</summary>
    internal void OnItemUsed(Player player, int lowId, int highId, Character target)
    {
        if (target is NpcCharacter npc)
            Start(npc, player, d => d.On == NpcEventDefinition.ItemUsed && (d.Items.Contains(lowId) || d.Items.Contains(highId)));
    }

    /// <summary>A nano event on <paramref name="npc"/>: Charmed, Replaced or Ended.</summary>
    internal void OnNanoEvent(NpcCharacter npc, int nanoId, string on, Player? player = null)
        => Start(npc, player, d => d.Nano == nanoId && string.Equals(d.On, on, StringComparison.Ordinal));

    void Start(NpcCharacter npc, Player? player, Func<NpcEventDefinition, bool> matches)
    {
        if (npc.IsDead || npc.MobTemplate?.Events is not { Count: > 0 } definitions)
            return;

        DateTime now = DateTime.UtcNow;
        foreach (NpcEventDefinition definition in definitions)
        {
            if (!matches(definition))
                continue;
            if (definition.StartMinMs > 0 || definition.StartMaxMs > 0)
            {
                TrackedOf(npc).Timed.Add((definition, now.AddMilliseconds(Between(definition.StartMinMs, definition.StartMaxMs)), player));
                continue;
            }

            Queue(npc, player, definition, now);
            if (definition.RepeatMinMs > 0)
                TrackedOf(npc).Timed.Add((definition, now.AddMilliseconds(Between(definition.RepeatMinMs, definition.RepeatMaxMs)), player));
        }

        // Steps due now run now: an event's flag (SetStat at 0 ms) is set before the next use can see it.
        Run(now);
    }

    /// <summary>An NPC with events that was not spawned through <see cref="Track"/> (placed): tracked from where it stands now.</summary>
    Tracked TrackedOf(NpcCharacter npc)
    {
        if (!_npcs.TryGetValue(npc, out Tracked? tracked))
        {
            tracked = new Tracked { Spot = new Vector3(npc.Position.x, npc.Position.y, npc.Position.z), Fighting = npc.IsInCombat };
            _npcs[npc] = tracked;
        }

        return tracked;
    }

    /// <summary>Queues the event's steps from <paramref name="now"/>, unless its While or IfStat says otherwise.</summary>
    void Queue(NpcCharacter npc, Player? player, NpcEventDefinition definition, DateTime now)
    {
        if (!InState(npc, definition.While)
            || definition.IfStat is int stat && npc.Stats.GetOrZero((CharacterStat)stat) != definition.IfValue)
            return;
        foreach (NpcEventStep step in definition.Steps)
            _queue.Add((now.AddMilliseconds(Math.Max(0, step.DelayMs)), npc, player, step));
    }

    static bool InState(NpcCharacter npc, string state) => state switch
    {
        NpcEventDefinition.Idle => !npc.IsInCombat,
        NpcEventDefinition.Fighting => npc.IsInCombat,
        _ => true
    };

    internal void Tick()
    {
        DateTime now = DateTime.UtcNow;
        Run(now);
        if (now < _nextThinkUtc)
            return;
        _nextThinkUtc = now.AddSeconds(ThinkSeconds);

        foreach (var (npc, tracked) in _npcs.ToArray())
        {
            if (npc.IsDead || !registry.TryGet(npc.Identity, out var current) || !ReferenceEquals(current, npc))
            {
                _npcs.Remove(npc);
                continue;
            }

            bool fighting = npc.IsInCombat;
            if (fighting != tracked.Fighting)
            {
                tracked.Fighting = fighting;
                // The other edge's repeats end with it: a fight's barks stop as the fight ends, and the other way round.
                string ending = fighting ? NpcEventDefinition.FightEnded : NpcEventDefinition.FightStarted;
                tracked.Timed.RemoveAll(t => string.Equals(t.Definition.On, ending, StringComparison.Ordinal));
                string on = fighting ? NpcEventDefinition.FightStarted : NpcEventDefinition.FightEnded;
                Start(npc, null, d => string.Equals(d.On, on, StringComparison.Ordinal));
            }

            PlayTimed(npc, tracked, now);
        }

        Run(now);
    }

    /// <summary>Plays the NPC's events whose start delay or repeat is due; a repeating one is set to play again.</summary>
    void PlayTimed(NpcCharacter npc, Tracked tracked, DateTime now)
    {
        for (int i = 0; i < tracked.Timed.Count; i++)
        {
            var (definition, nextUtc, player) = tracked.Timed[i];
            if (now < nextUtc)
                continue;
            Queue(npc, player, definition, now);
            if (definition.RepeatMinMs > 0)
                tracked.Timed[i] = (definition, now.AddMilliseconds(Between(definition.RepeatMinMs, definition.RepeatMaxMs)), player);
            else
                tracked.Timed.RemoveAt(i--);
        }
    }

    void Run(DateTime now)
    {
        for (int i = 0; i < _queue.Count; i++)
        {
            var (at, npc, player, step) = _queue[i];
            if (at > now)
                continue;
            _queue.RemoveAt(i--);
            if (npc.IsDead || !ReferenceEquals(npc.Playfield, playfield))
                continue;

            NpcCharacter? actor = step.Speaker is { Length: > 0 } hash ? FindSpeaker(npc, hash) : npc;
            if (actor != null)
                Do(actor, player, step);
        }
    }

    void Do(NpcCharacter npc, Player? player, NpcEventStep step)
    {
        if (step.SetStat is int stat)
            npc.Stats.Set((CharacterStat)stat, step.SetValue);
        if (step.Say.Length > 0)
            Say(npc, step.Say);
        if (step.SayOneOf.Length > 0)
            Say(npc, step.SayOneOf[Random.Shared.Next(step.SayOneOf.Length)]);
        if (step.StandUp)
        {
            npc.Motor.ApplyAction(LeaveAction(npc.Motor.State));
            Act(npc, CharacterActionType.StandUp, Identity.None);
        }
        if (step.ReturnHome && _npcs.TryGetValue(npc, out Tracked? tracked)
            && (npc.Position.x != tracked.Spot.x || npc.Position.z != tracked.Spot.z))
        {
            Vector3 from = npc.Position;
            npc.Motor.Warp(tracked.Spot, npc.Rotation);
            NpcFollowTarget.AnnounceCoordinatePath(npc, from, [tracked.Spot]);
        }
        if (step.Pose != 0)
        {
            // A server-side mode change never reaches the client (and a FacePlayer step re-sent it standing), so the
            // spawn packet goes out again with the new pose, as Face does.
            npc.Motor.ApplyAction(PoseAction(step.Pose));
            playfield.GetRequiredService<PlayfieldLocality>().Announce(npc, npc.BuildSpawnMessage());
        }
        if (step.HealthPercent > 0)
            SetHealth(npc, PercentHealth(npc, step.HealthPercent), step.DamageType);
        if (step.FacePlayer && player is { IsDead: false } && ReferenceEquals(player.Playfield, npc.Playfield))
            Face(npc, player);
        if (step.Action != 0)
            Act(npc, (CharacterActionType)step.Action, new Identity { Type = IdentityType.None, Instance = step.ActionTarget });
        if (step.Anim > 0)
            Act(npc, CharacterActionType.NpcSocialAnim, new Identity { Type = IdentityType.None, Instance = step.Anim });
        if (step.Gfx > 0)
            npc.AnnounceGfxEffect(step.Gfx);
        if (step.Despawn && npc.Playfield != null)
            npc.DespawnAsDeath();
    }

    NpcCharacter? FindSpeaker(NpcCharacter near, string hash)
    {
        NpcCharacter? best = null;
        double bestDistance = SpeakerRange;
        foreach (Dynel dynel in registry.Dynels())
        {
            if (dynel is not NpcCharacter npc || npc.IsDead || !ReferenceEquals(npc.Playfield, playfield)
                || !string.Equals(npc.SpawnHash, hash, StringComparison.Ordinal))
                continue;
            double distance = npc.Distance3D(near);
            if (distance <= bestDistance)
            {
                best = npc;
                bestDistance = distance;
            }
        }

        return best;
    }

    static int PercentHealth(NpcCharacter npc, int percent)
        => Math.Max(1, Math.Max(1, npc.Stats.GetOrZero(CharacterStat.MaxHealth)) * Math.Clamp(percent, 1, 100) / 100);

    /// <summary>Milliseconds picked at random in <paramref name="min"/>..<paramref name="max"/>.</summary>
    static double Between(int min, int max)
    {
        int low = Math.Max(0, min);
        return low + Random.Shared.NextDouble() * Math.Max(0, max - low);
    }

    static MovementAction PoseAction(int pose) => pose switch
    {
        (int)MovementState.Sleep => MovementAction.SwitchToSleep,
        (int)MovementState.Lounge => MovementAction.SwitchToLounge,
        _ => MovementAction.SwitchToSit
    };

    static MovementAction LeaveAction(MovementState state) => state switch
    {
        MovementState.Sleep => MovementAction.LeaveSleep,
        MovementState.Lounge => MovementAction.LeaveLounge,
        _ => MovementAction.LeaveSit
    };

    /// <summary>Sets its health and shows the change as live does: a HealthDamage (heal, or a hit of that damage type).</summary>
    static void SetHealth(NpcCharacter npc, int health, int damageType)
    {
        int before = npc.Stats.GetOrZero(CharacterStat.Health);
        npc.Stats.Set(CharacterStat.Health, health, StatDetail.Base, dirty: true);
        npc.AnnounceHealthDamage(null, health, health - before, damageType);
    }

    /// <summary>
    /// Turns the NPC toward <paramref name="other"/>. The client takes no heading packet for an NPC (a CharDCMove is
    /// ignored), and a short FollowTarget step did not move it either (it was still sitting for the client after the
    /// StandUp action), so it gets its full spawn packet again: standing, with the new heading.
    /// </summary>
    void Face(NpcCharacter npc, Character other)
    {
        float dx = other.Position.xf - npc.Position.xf, dz = other.Position.zf - npc.Position.zf;
        if (Math.Abs(dx) < 1e-3f && Math.Abs(dz) < 1e-3f)
            return;
        float half = MathF.Atan2(dx, dz) * 0.5f;
        npc.Motor.Warp(npc.Position, new Quaternion(0, MathF.Sin(half), 0, MathF.Cos(half)));
        playfield.GetRequiredService<PlayfieldLocality>().Announce(npc, npc.BuildSpawnMessage());
    }

    static void Act(NpcCharacter npc, CharacterActionType action, Identity target)
        => npc.Cell?.Announce(new CharacterActionMessage
        {
            Identity = npc.Identity,
            Unknown = 0x00,
            Action = action,
            Unknown1 = 0,
            Target = target,
            Parameter1 = 0,
            Parameter2 = 0,
            Unknown2 = 0
        });

    void Say(NpcCharacter npc, string text)
        => playfield.GetService<Chat.VicinityChatRelay>()?.Speak(npc, text);
}
