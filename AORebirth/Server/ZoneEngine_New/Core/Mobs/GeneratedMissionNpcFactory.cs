namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Playfields;
using AORebirth.Core.Textures;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Playfield;

public sealed class GeneratedMissionNpcFactory(IItemTemplateCatalog catalog, Lazy<GeneratedMissionAcgService> missions) : IGeneratedMissionNpcFactory
{
    public NpcCharacter Create(GeneratedMissionNpcEvidence evidence, GeneratedMissionObject state, IItemBuilder items)
    {
        if (state.Level is not > 0 || state.MaxHealth is not > 0 || state.CurrentHealth is not > 0
            || state.CurrentHealth > state.MaxHealth || state.IsDead
            || state.RuntimeInstance != evidence.RuntimeInstance || state.RuntimeType != evidence.RuntimeType)
            throw new InvalidOperationException("Mission NPC requires its exact live durable level/health and identity.");
        var source = evidence.CopySpawnMessage();
        if (!source.TailFullyDecoded || source.UndecodedTail?.Length > 0)
            throw new InvalidOperationException("Mission NPC appearance must be a completely decoded accepted spawn.");
        var npc = new GeneratedMissionNpcCharacter(new Identity { Type = (IdentityType)state.RuntimeType,
            Instance = state.RuntimeInstance }, items, missions) { Name = evidence.Name };

        // Exact BART production-shell fields from SqlTables/mobtemplate.sql, selected
        // explicitly by Legacy MissionAcgOperationalRuntime, not by NPC appearance.
        // Level and life are replaced with durable mission difficulty state below.
        npc.Stats.Set(CharacterStat.NPCFamily, 137); npc.Stats.Set(CharacterStat.Side, 0);
        npc.Stats.Set(CharacterStat.Fatness, 1); npc.Stats.Set(CharacterStat.Breed, 1);
        npc.Stats.Set(CharacterStat.Sex, 2); npc.Stats.Set(CharacterStat.Race, 1);
        npc.Stats.Set(CharacterStat.Flags, 271061505); npc.Stats.Set(CharacterStat.Profession, 15);
        npc.Stats.Set(CharacterStat.VisualProfession, 15); npc.Stats.Set(CharacterStat.AccountFlags, 0);
        npc.Stats.Set(CharacterStat.Expansion, 0); npc.Stats.Set(CharacterStat.RunSpeed, 513);
        npc.Stats.Set((CharacterStat)466, 15);
        npc.Stats.Set(CharacterStat.Level, state.Level.Value);
        npc.Stats.Set(CharacterStat.MaxHealth, state.MaxHealth.Value);
        npc.Stats.Set(CharacterStat.Health, state.CurrentHealth.Value);
        npc.Stats.Set(CharacterStat.MonsterData, evidence.MonsterData);
        npc.Stats.Set(CharacterStat.Scale, source.MonsterScale);
        npc.Stats.Set(CharacterStat.HeadMesh, checked((int)(source.HeadMesh ?? 0)));
        foreach (var texture in source.Textures ?? []) npc.Textures.Add(new AOTextures(texture.Place, texture.Id));
        foreach (var mesh in source.Meshes ?? []) npc.Meshes.Add(mesh);
        if (!evidence.IsFindPerson)
        {
            var contract = MissionNpcCombatPolicy.Create(state.RuntimeInstance, state.Level.Value,
                (source.Meshes ?? []).Any(mesh => mesh.Layer == 2 && mesh.Id > 0), items, catalog, out var weapon);
            npc.Combat = new MissionNpcCombatRuntime(contract, weapon);
        }
        npc.Motor.RefreshFromStats();
        return npc;
    }
}

internal sealed class GeneratedMissionNpcCharacter(Identity identity, IItemBuilder items,
    Lazy<GeneratedMissionAcgService> missions) : NpcCharacter(identity, items)
{
    internal MissionNpcCombatRuntime? Combat { get; set; }
    bool _deathCommitted;
    protected override bool UsesPassiveRegen => false;
    protected override int DeathAnimationKey => 501; // MissionInstanceMobCombat.DeathParameter2.
    protected override int CorpseSpawnDelayMilliseconds => 600; // Accepted NpcCorpseLifecycleRules.
    protected override void SpawnDeathCorpse() => missions.Value.SpawnDeathCorpse(this);
    public override void Rebase() { }
    public override void RebaseWeapons() { }

    public override List<WeaponItemFullUpdateMessage> BuildWeaponInstanceMessages()
        => Combat?.Contract.WeaponDefinition is { } definition && Combat.Weapon is { } weapon
            ? [CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(Identity,
                Playfield?.Identity.Instance ?? 0, weapon.Identity, definition)] : [];

    public override void StartFighting(Identity target, byte action)
    {
        if (Combat != null && !IsDead) Combat.Start(this, target);
    }
    protected override void TickCombat(double deltaTime) => Combat?.Tick(this, deltaTime);

    public override void Tick(double deltaTime)
    {
        if (IsDead) missions.Value.PollNpcCorpseLifetime(this);
        if (!IsDead && Combat != null && FightingTarget.Instance == 0 && Playfield != null)
        {
            var target = Playfield.GetRequiredService<DynelRegistry>().PlayerEntities()
                .Where(player => !player.IsDead && !player.IsPersistenceQuarantined && player.Playfield == Playfield
                    && Distance3D(player) <= MissionNpcCombatPolicy.AggroRadius)
                .OrderBy(player => Distance3D(player)).ThenBy(player => player.Identity.Instance).FirstOrDefault();
            if (target != null) Combat.Start(this, target.Identity);
        }
        base.Tick(deltaTime);
    }

    public override bool ApplyDamage(Character attacker, int damage, HitType hitType)
    {
        if (IsDead || damage <= 0) return false;
        int next = Math.Max(0, Stats.GetOrZero(CharacterStat.Health) - damage);
        if (!missions.Value.TryPersistNpc(this, attacker as Player, next))
            throw new InvalidOperationException("Mission NPC damage could not be durably committed.");
        _deathCommitted = next == 0;
        bool killed = base.ApplyDamage(attacker, damage, hitType);
        if (!killed && Combat != null && FightingTarget.Instance == 0 && attacker.Playfield == Playfield)
            Combat.Start(this, attacker.Identity);
        return killed;
    }

    public override void OnDeath(Character? killer = null)
    {
        if (IsDead) return;
        if (!_deathCommitted && !missions.Value.TryPersistNpc(this, killer as Player, 0))
            throw new InvalidOperationException("Mission NPC death could not be durably committed.");
        _deathCommitted = true;
        base.OnDeath(killer);
        if (Combat?.Contract.SendStopFightOnDeath == true)
            Cell?.Announce(new StopFightMessage { Identity = Identity, Unknown1 = 1 });
        missions.Value.OnNpcDeathPublished(this);
    }
}
