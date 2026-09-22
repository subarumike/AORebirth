namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Linq;
using AORebirth.Enums;
using AORebirth.Core.Textures;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Playfield;

public sealed class GeneratedMissionNpcFactory(IItemTemplateCatalog catalog, Lazy<GeneratedMissionAcgService> missions,
    IGameData? gameData = null) : IGeneratedMissionNpcFactory
{
    readonly MissionNpcContent _content = MissionNpcContent.Load(gameData?.RootPath);
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
            Instance = state.RuntimeInstance }, items, missions) { Name = evidence.Name, AggroRadius = _content.AggroRadius };

        foreach (var stat in _content.Stats) npc.Stats.Set(stat.Key, stat.Value);
        npc.Stats.Set(CharacterStat.Level, state.Level.Value);
        npc.Stats.Set(CharacterStat.MaxHealth, state.MaxHealth.Value);
        npc.Stats.Set(CharacterStat.Health, state.CurrentHealth.Value);
        npc.Stats.Set(CharacterStat.MonsterData, evidence.MonsterData);
        npc.Stats.Set(CharacterStat.Scale, source.MonsterScale);
        npc.Stats.Set(CharacterStat.HeadMesh, checked((int)(source.HeadMesh ?? 0)));
        foreach (var texture in source.Textures ?? []) npc.SetSpawnTexture(texture.Place, texture.Id);
        foreach (var mesh in source.Meshes ?? []) npc.AddSpawnMesh(mesh);
        if (!evidence.IsFindPerson)
        {
            var weapon = CreateCombatWeapon(state.RuntimeInstance, state.Level.Value,
                (source.Meshes ?? []).Any(mesh => mesh.Layer == _content.WeaponMeshLayer && mesh.Id > 0), items, catalog, _content);
            npc.Equipment.Add(npc.Equipment.Offset, weapon);
            npc.CombatEnabled = true;
            npc.RebaseWeapons();
        }
        npc.Motor.RefreshFromStats();
        return npc;
    }

    internal static Item CreateCombatWeapon(int runtimeIdentity, int level, bool hasGunMesh,
        IItemBuilder items, IItemTemplateCatalog catalog, MissionNpcContent content)
    {
        if (runtimeIdentity < 1_000_000 || level < 1 || level > 220)
            throw new ArgumentOutOfRangeException(nameof(level));
        if (hasGunMesh)
        {
            for (int offset = 0; offset < content.Weapons.Length; offset++)
            {
                var selected = content.Weapons[(int)(Math.Abs((long)runtimeIdentity + offset) % content.Weapons.Length)];
                if (!catalog.TryGet(selected.LowId, out _) || !catalog.TryGet(selected.HighId, out _)) continue;
                return BuildWeapon(selected.LowId, selected.HighId, Math.Min(level, selected.MaximumQuality), items, catalog);
            }
        }

        return BuildWeapon(content.Melee.SpecialLowId, content.Melee.SpecialHighId, level, items, catalog);
    }

    static Item BuildWeapon(int lowId, int highId, int quality, IItemBuilder items, IItemTemplateCatalog catalog)
    {
        if (!catalog.TryGet(lowId, out _) || !catalog.TryGet(highId, out _))
            throw new InvalidOperationException("The configured mission weapon templates are unavailable.");
        var weapon = items.Create(lowId, highId, quality, ItemSource.Other);
        if (weapon.Quality <= 0 || weapon.LowId != lowId || weapon.HighId != highId || !weapon.IsWieldableCombatWeapon())
            throw new InvalidOperationException("The configured mission weapon did not resolve to a native combat item.");
        return weapon;
    }
}

internal sealed class GeneratedMissionNpcCharacter(Identity identity, IItemBuilder items,
    Lazy<GeneratedMissionAcgService> missions) : NpcCharacter(identity, items)
{
    internal bool CombatEnabled { get; set; }
    internal double AggroRadius { get; set; }
    public override bool AcceptsPlayerCombatNanos => CombatEnabled;
    bool _deathCommitted;
    protected override bool UsesPassiveRegen => false;
    protected override int DeathAnimationKey => 501; // MissionInstanceMobCombat.DeathParameter2.
    protected override int CorpseSpawnDelayMilliseconds => 600; // Accepted NpcCorpseLifecycleRules.
    protected override void SpawnDeathCorpse() => missions.Value.SpawnDeathCorpse(this);
    protected override void RemoveFromWorldAfterDeath()
    {
    }
    public override void Rebase() { }
    public override void RebaseWeapons()
    {
        if (CombatEnabled) base.RebaseWeapons();
    }

    public override void StartFighting(Identity target, byte action)
    {
        if (CombatEnabled && !IsDead) base.StartFighting(target, action);
    }
    protected override void TickCombat(double deltaTime)
    {
        if (!CombatEnabled) return;
        var target = TryResolveFightingTarget();
        if (target == null || target.Playfield != Playfield) return;
        if (!Weapons.Values.Any(weapon => GetEdgeDistanceTo(target) <= weapon.GetAttackRange()))
        {
            Motor.NavigateTo(target.Position);
            return;
        }
        Motor.Halt();
        base.TickCombat(deltaTime);
    }

    public override void Tick(double deltaTime)
    {
        if (IsDead) missions.Value.PollNpcCorpseLifetime(this);
        if (!IsDead && CombatEnabled && FightingTarget.Instance == 0 && Playfield != null)
        {
            var target = Playfield.GetRequiredService<DynelRegistry>().PlayerEntities()
                .Where(player => !player.IsDead && !player.IsPersistenceQuarantined && player.Playfield == Playfield
                    && GetEdgeDistanceTo(player) <= AggroRadius)
                .OrderBy(player => GetEdgeDistanceTo(player)).ThenBy(player => player.Identity.Instance).FirstOrDefault();
            if (target != null) StartFighting(target.Identity, 0);
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
        if (!killed && CombatEnabled && FightingTarget.Instance == 0 && attacker.Playfield == Playfield)
            StartFighting(attacker.Identity, 0);
        return killed;
    }

    public override void OnDeath(Character? killer = null)
    {
        if (IsDead) return;
        if (!_deathCommitted && !missions.Value.TryPersistNpc(this, killer as Player, 0))
            throw new InvalidOperationException("Mission NPC death could not be durably committed.");
        _deathCommitted = true;
        base.OnDeath(killer);
        if (CombatEnabled)
            Cell?.Announce(new StopFightMessage { Identity = Identity, Unknown1 = 1 });
        missions.Value.OnNpcDeathPublished(this);
    }
}
