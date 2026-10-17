namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using ZoneEngine_New.Core.Ai;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Helpers;
using ZoneEngine_New.Core.Playfield;

/// <summary>
/// An NPC that fights other NPCs on its own: listed enemies (Marcus Stone and the Burning Cleaning Robots), or, with
/// AssistPlayers, any NPC fighting a player near it (town guards). Players cannot attack it, even when it is
/// blue-named; the NPCs it fights can.
/// </summary>
public sealed class NpcDefenseDefinition
{
    /// <summary>Spawn or template hashes it attacks on sight.</summary>
    public string[] Enemies { get; set; } = [];

    /// <summary>
    /// When set, only Enemies that spawned from a hash point at one of these positions [x, y, z] count (Marcus Stone
    /// fights the one Burning Cleaning Robot spawning beside him, not the others of its hash further off).
    /// </summary>
    public float[][] EnemyPoints { get; set; } = [];

    /// <summary>Also attacks NPCs that are fighting a player (or a player's pet) within Radius.</summary>
    public bool AssistPlayers { get; set; }

    /// <summary>Metres around the defender it looks for enemies in.</summary>
    public float Radius { get; set; } = 15f;
}

/// <summary>Lets idle defenders on one playfield engage the NPCs their definition names.</summary>
internal sealed class NpcDefenseService(DynelRegistry registry, Playfield playfield)
{
    const float EnemyPointTolerance = 1f;
    const double ThinkSeconds = 0.5;
    DateTime _nextThinkUtc;
    readonly List<NpcCharacter> _defenders = new();
    readonly List<NpcCharacter> _npcs = new();

    internal void Tick()
    {
        DateTime now = DateTime.UtcNow;
        if (now < _nextThinkUtc)
            return;
        _nextThinkUtc = now.AddSeconds(ThinkSeconds);

        _defenders.Clear();
        _npcs.Clear();
        foreach (var dynel in registry.Dynels())
        {
            if (dynel is not NpcCharacter npc || npc.IsDead)
                continue;
            _npcs.Add(npc);
            if (npc.Defense != null)
                _defenders.Add(npc);
        }

        foreach (NpcCharacter defender in _defenders)
        {
            if (IsEngaged(defender))
                continue;

            NpcCharacter? target = PickTarget(defender);
            if (target == null || !CombatRules.CanAttack(defender, target))
                continue;

            // A defender draws only a little threat: a player who joins in takes the NPC's attention at once.
            if (defender.Brain != null)
                defender.Brain.AddThreat(target.Identity, NpcAiRules.DefenderThreat);
            else
                defender.StartFighting(target.Identity, 0);
            target.Brain?.AddThreat(defender.Identity, NpcAiRules.DefenderThreat);
        }
    }

    /// <summary>Fighting something still alive here; a fight whose target is gone is ended.</summary>
    bool IsEngaged(NpcCharacter defender)
    {
        if (defender.FightingTarget.Instance == 0)
            return false;
        if (registry.TryGet(defender.FightingTarget, out var current) && current is Character { IsDead: false })
            return true;

        defender.SetFightingTarget(SmokeLounge.AOtomation.Messaging.GameData.Identity.None);
        return false;
    }

    NpcCharacter? PickTarget(NpcCharacter defender)
    {
        NpcDefenseDefinition defense = defender.Defense!;
        NpcCharacter? best = null;
        float bestDistance = float.MaxValue;
        foreach (NpcCharacter npc in _npcs)
        {
            if (ReferenceEquals(npc, defender) || npc.Defense != null || npc.PetOwner is Player)
                continue;

            float distance = (float)defender.GetEdgeDistanceTo(npc);
            if (distance > defense.Radius || distance >= bestDistance)
                continue;

            if (IsEnemy(defense, npc) || defense.AssistPlayers && IsFightingPlayer(npc))
            {
                best = npc;
                bestDistance = distance;
            }
        }

        return best;
    }

    bool IsEnemy(NpcDefenseDefinition defense, NpcCharacter npc)
    {
        if (Array.IndexOf(defense.Enemies, npc.SpawnHash) < 0
            && (npc.MobTemplate == null || Array.IndexOf(defense.Enemies, npc.MobTemplate.Hash) < 0))
            return false;
        if (defense.EnemyPoints.Length == 0)
            return true;

        if (!playfield.GetRequiredService<HashSpawnSystem>().TryGetSpawnPoint(npc, out HashSpawnPoint point)
            || point.Source.Position is not { Length: >= 3 } at)
            return false;

        foreach (float[] wanted in defense.EnemyPoints)
        {
            float dx = wanted[0] - at[0], dy = wanted[1] - at[1], dz = wanted[2] - at[2];
            if (dx * dx + dy * dy + dz * dz <= EnemyPointTolerance * EnemyPointTolerance)
                return true;
        }

        return false;
    }

    bool IsFightingPlayer(NpcCharacter npc)
        => npc.FightingTarget.Instance != 0 && registry.TryGet(npc.FightingTarget, out var target)
            && (target is Player || target is NpcCharacter { PetOwner: Player });
}
