namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Movement;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Ai;
using Quaternion = AORebirth.Core.Vector.Quaternion;
using Vector3 = AORebirth.Core.Vector.Vector3;

/// <summary>
/// An NPC that waits to be rescued (the Arete Wounded Dockworkers). Live (raw dump 2026-10-01 15:18:57, capture
/// 2026-10-07): it spawns lying (movement mode 8) at 12 of 32 health, which stays put (no regeneration), and moans now
/// and then. A player uses a rescue item on it (the item's own OnUse finishes the quest); 2.1 s later it says its thanks
/// and in the same frame stands up (CharacterAction StandUp) and is healed to full (HealthDamage +20). 2.8 s after that
/// comes CharacterAction 100 (target None:9), and 22.8 s after standing CharacterAction 86 lays it down again with a
/// HealthDamage of -20 (MeleeAC): wounded for the next player.
/// </summary>
public sealed class NpcRescueDefinition
{
    /// <summary>Movement mode while waiting (8 sit, which wounded humanoids show lying down).</summary>
    public int MovementMode { get; set; } = (int)MovementState.Sit;

    /// <summary>Health while waiting, in percent of max (live Wounded Dockworker: 12 of 32).</summary>
    public int HealthPercent { get; set; } = 37;

    /// <summary>Said aloud while waiting, one at random every MoanSeconds (give or take a quarter).</summary>
    public string[] Moans { get; set; } = [];

    public int MoanSeconds { get; set; } = 45;

    /// <summary>Item templates whose use on it is the rescue (the Health Regeneration Stim).</summary>
    public int[] RescueItems { get; set; } = [];

    /// <summary>Said aloud as it gets up (live: 2.1 s after the stim, "Thank you for saving me.").</summary>
    public string ThanksText { get; set; } = string.Empty;

    /// <summary>From the rescue to the thanks, the stand-up and the heal.</summary>
    public int ThanksDelayMs { get; set; } = 2100;

    /// <summary>CharacterAction sent after standing (live: 100 with target None:9, 2.8 s later); 0 for none.</summary>
    public int AfterStandAction { get; set; } = 100;

    public int AfterStandTarget { get; set; } = 9;

    public int AfterStandDelayMs { get; set; } = 2800;

    /// <summary>CharacterAction that lays it down again (live: 86).</summary>
    public int WoundAction { get; set; } = 86;

    /// <summary>Quests.json quests the rescue completes for the rescuer, when active (when the item does not).</summary>
    public string[] CompleteQuests { get; set; } = [];

    /// <summary>Seconds from standing up until it is wounded again (live: 22.8).</summary>
    public double ResetSeconds { get; set; } = 22.8;

    /// <summary>
    /// Turns to face the rescuer as it stands up. Not live (the raw dump has no turn for it); asked for so the
    /// thanks is aimed at the player.
    /// </summary>
    public bool FaceRescuer { get; set; } = true;
}

/// <summary>Runs the rescue NPCs of one playfield: their pose and health, moans, rescue and reset.</summary>
internal sealed class NpcRescueService(Playfield playfield, DynelRegistry registry,
    ZoneEngine_New.Core.Quests.QuestService? quests = null)
{
    const float MoanRange = 20f;
    const float SayRange = 10f;
    const float RescueRange = 5f;   // the stim is used standing by the wounded
    const double ThinkSeconds = 0.1;
    // After the stand-up has played.
    const double FaceDelaySeconds = 1;

    sealed class State
    {
        public DateTime NextMoanUtc;
        public DateTime? StandAtUtc;
        public DateTime? FaceAtUtc;
        public DateTime? AfterStandAtUtc;
        public DateTime? ResetAtUtc;
        public bool Rescued;
        public Player? Rescuer;
        public Vector3 Spot;
    }

    readonly Dictionary<NpcCharacter, State> _npcs = new();
    DateTime _nextThinkUtc;

    /// <summary>Puts a freshly spawned rescue NPC in its waiting state. Runs before its first spawn packet.</summary>
    internal static void Prepare(NpcCharacter npc)
    {
        if (npc.Rescue is not { } rescue)
            return;

        npc.Motor.ApplyAction(ModeAction(rescue.MovementMode));
        npc.Stats.Set(CharacterStat.Health, WoundedHealth(npc, rescue));
        // Its health only changes by the rescue and the reset, as live.
        npc.RegenHeld = true;
    }

    internal void Track(NpcCharacter npc)
    {
        if (npc.Rescue == null)
            return;
        _npcs[npc] = new State { NextMoanUtc = DateTime.UtcNow.AddSeconds(NextMoanDelay(npc.Rescue)), Spot = npc.Position };
    }

    /// <summary>An item used on a character (ItemUseService): the rescue when it is a waiting rescue NPC's item.</summary>
    internal void OnItemUsed(Player player, int lowId, int highId, Character target)
    {
        if (target is not NpcCharacter npc || !_npcs.TryGetValue(npc, out State? state) || state.Rescued || npc.IsDead
            || npc.Rescue is not { } rescue || !rescue.RescueItems.Contains(lowId) && !rescue.RescueItems.Contains(highId)
            || player.GetEdgeDistanceTo(npc) > RescueRange)
            return;

        state.Rescued = true;
        state.Rescuer = player;
        state.StandAtUtc = DateTime.UtcNow.AddMilliseconds(Math.Max(0, rescue.ThanksDelayMs));
        foreach (string quest in rescue.CompleteQuests)
            quests?.TryComplete(player, quest, out _);
    }

    internal void Tick()
    {
        DateTime now = DateTime.UtcNow;
        if (_npcs.Count == 0 || now < _nextThinkUtc)
            return;
        _nextThinkUtc = now.AddSeconds(ThinkSeconds);

        foreach (var pair in _npcs.ToArray())
        {
            NpcCharacter npc = pair.Key;
            State state = pair.Value;
            NpcRescueDefinition rescue = npc.Rescue!;
            if (npc.IsDead || !registry.TryGet(npc.Identity, out var current) || !ReferenceEquals(current, npc))
            {
                _npcs.Remove(npc);
                continue;
            }

            if (state.StandAtUtc is { } stand && now >= stand)
            {
                // Thanks, stands up and is healed to full in one frame.
                state.StandAtUtc = null;
                if (!string.IsNullOrEmpty(rescue.ThanksText))
                    Say(npc, rescue.ThanksText, SayRange);
                npc.Motor.ApplyAction(LeaveAction(npc.Motor.State));
                Act(npc, CharacterActionType.StandUp, Identity.None);
                if (rescue.FaceRescuer)
                    state.FaceAtUtc = now.AddSeconds(FaceDelaySeconds);
                SetHealth(npc, MaxHealth(npc), damageType: 0);
                if (rescue.AfterStandAction != 0)
                    state.AfterStandAtUtc = now.AddMilliseconds(Math.Max(0, rescue.AfterStandDelayMs));
                state.ResetAtUtc = now.AddSeconds(Math.Max(1, rescue.ResetSeconds));
            }

            if (state.FaceAtUtc is { } face && now >= face)
            {
                state.FaceAtUtc = null;
                if (state.Rescuer is { IsDead: false } rescuer && ReferenceEquals(rescuer.Playfield, npc.Playfield))
                    Face(npc, rescuer);
                state.Rescuer = null;
            }

            if (state.AfterStandAtUtc is { } after && now >= after)
            {
                state.AfterStandAtUtc = null;
                Act(npc, (CharacterActionType)rescue.AfterStandAction,
                    new Identity { Type = IdentityType.None, Instance = rescue.AfterStandTarget });
            }

            if (state.ResetAtUtc is { } reset && now >= reset)
            {
                // Lies down again, hurt, for the next player.
                state.ResetAtUtc = null;
                state.AfterStandAtUtc = null;
                state.FaceAtUtc = null;
                state.Rescuer = null;
                state.Rescued = false;
                // Back onto its spot (facing the rescuer stepped it off) before lying down.
                if (npc.Position.x != state.Spot.x || npc.Position.z != state.Spot.z)
                {
                    Vector3 from = npc.Position;
                    npc.Motor.Warp(state.Spot, npc.Rotation);
                    NpcFollowTarget.AnnounceCoordinatePath(npc, from, [state.Spot]);
                }
                npc.Motor.ApplyAction(ModeAction(rescue.MovementMode));
                if (rescue.WoundAction != 0)
                    Act(npc, (CharacterActionType)rescue.WoundAction, Identity.None);
                SetHealth(npc, WoundedHealth(npc, rescue), (int)CharacterStat.MeleeAC);
                state.NextMoanUtc = now.AddSeconds(NextMoanDelay(rescue));
            }

            if (!state.Rescued && rescue.Moans.Length != 0 && now >= state.NextMoanUtc)
            {
                state.NextMoanUtc = now.AddSeconds(NextMoanDelay(rescue));
                if (registry.PlayerEntities().Any(p => p.GetEdgeDistanceTo(npc) <= MoanRange))
                    Say(npc, rescue.Moans[Random.Shared.Next(rescue.Moans.Length)], MoanRange);
            }
        }
    }

    static int MaxHealth(NpcCharacter npc) => Math.Max(1, npc.Stats.GetOrZero(CharacterStat.MaxHealth));

    static int WoundedHealth(NpcCharacter npc, NpcRescueDefinition rescue)
        => Math.Max(1, MaxHealth(npc) * Math.Clamp(rescue.HealthPercent, 1, 100) / 100);

    static double NextMoanDelay(NpcRescueDefinition rescue)
        => Math.Max(5, rescue.MoanSeconds) * (0.75 + Random.Shared.NextDouble() * 0.5);

    static MovementAction ModeAction(int mode) => mode switch
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

    /// <summary>A CharacterAction by the NPC to the players around (live: StandUp, 100, 86).</summary>
    static void Act(NpcCharacter npc, CharacterActionType action, Identity target)
    {
        npc.Cell?.Announce(new CharacterActionMessage
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
    }

    void Say(NpcCharacter npc, string text, float range)
    {
        var message = new ChatTextMessage { Identity = npc.Identity, Text = text, Unknown1 = 0, Unknown2 = 0, Unknown3 = 0 };
        foreach (Player listener in registry.PlayerEntities())
        {
            if (listener.Session != null && listener.GetEdgeDistanceTo(npc) <= range)
                listener.Session.Send(message);
        }
    }
}
