namespace ZoneEngine_New.Core.Mobs;

using System;
using AORebirth.Core.Playfields;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Helpers;
using ZoneEngine_New.Core.Inventory;

/// <summary>Owner-tick execution of the existing mission fixed/pistol policy only.</summary>
internal sealed class MissionNpcCombatRuntime
{
    readonly CapturedEnemyCombatContract _contract;
    readonly Item? _weapon;
    double _untilAttack, _untilHit;
    bool _attackSent;
    int _damageCursor, _intervalCursor;
    Identity _target;

    internal MissionNpcCombatRuntime(CapturedEnemyCombatContract contract, Item? weapon)
    {
        if (!contract.IsCombatReady || !contract.CapturedAttackRange.HasValue
            || contract.RechargeSeconds <= 0 || contract.RequiresDamageLineOfSight
            || contract.ParallelAttackSequence != null || contract.SpecialAttackSequence != null
            || contract.BasicCombat != null || contract.UsesEquippedWeaponTiming)
            throw new InvalidOperationException("Mission combat requires its complete accepted fixed/pistol policy.");
        if (contract.WeaponDefinition != null && (weapon == null || weapon.LowId != contract.WeaponDefinition.LowId
            || weapon.HighId != contract.WeaponDefinition.HighId || weapon.Quality != contract.WeaponDefinition.Quality))
            throw new InvalidOperationException("Mission WIFU does not match its actual equipped template.");
        _contract = contract; _weapon = weapon;
    }

    internal CapturedEnemyCombatContract Contract => _contract;
    internal Item? Weapon => _weapon;

    internal void Start(NpcCharacter actor, Identity target)
    {
        _target = target; _attackSent = false; _damageCursor = 0; _intervalCursor = 0;
        _untilAttack = _contract.AttackStartDelaySeconds;
        _untilHit = _contract.FirstHitDelaySeconds;
        actor.SetFightingTarget(target);
        if (_contract.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
            actor.Cell?.Announce(CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(actor.Identity, _contract));
        if (_untilAttack <= 0) SendAttack(actor);
    }

    void SendAttack(NpcCharacter actor)
    {
        if (_contract.AttackModel != CapturedEnemyAttackModel.FixedAttackInfo)
            actor.Cell?.Announce(CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(actor.Identity, _contract));
        actor.Cell?.Announce(CapturedEnemyCombatPacketFactory.CreateAttack(actor.Identity, _target, _contract));
        _attackSent = true;
        // Like the Legacy coordinator, anchor first-hit delay to actual Attack release,
        // not an overdue timer, so a late heartbeat cannot collapse packet ordering.
        _untilHit = _contract.FirstHitDelaySeconds;
    }

    internal void Tick(NpcCharacter actor, double delta)
    {
        var target = actor.TryResolveFightingTarget();
        if (actor.IsDead || target == null || actor.Playfield != target.Playfield) return;
        if (!_target.Equals(target.Identity)) { Start(actor, target.Identity); return; }
        if (!_attackSent)
        {
            _untilAttack -= delta;
            if (_untilAttack <= 0) SendAttack(actor);
            return;
        }
        _untilHit -= delta;
        if (_untilHit > 0) return;
        if (actor.Distance3D(target) > _contract.CapturedAttackRange!.Value)
        {
            actor.Motor.SetPath([target.Position]);
            _untilHit = 1.0; // Existing NpcCombatAttackRules.OutOfRangeRetrySeconds.
            return;
        }
        actor.Motor.Halt();
        int damage;
        if (_contract.CapturedDamageObservations.Length > 0)
            damage = _contract.CapturedDamageObservations[_damageCursor++ % _contract.CapturedDamageObservations.Length];
        else
        {
            var result = DamageCalculator.CalculateFromWeapon(actor, target, _weapon,
                minDamageOverride: _contract.MinDamage, maxDamageOverride: _contract.MaxDamage,
                critBonusOverride: _contract.CapturedDamageBonus);
            if (!result.IsHit)
            {
                actor.Cell?.Announce(new MissedAttackInfoMessage
                { Identity = actor.Identity, Unknown1 = -1, Unknown2 = _contract.AttackInfoWeaponSlot,
                    Unknown3 = actor.Identity, Unknown4 = target.Identity, Unknown5 = 0 });
                ScheduleNext(); return;
            }
            damage = result.Damage;
        }
        target.ApplyDamage(actor, damage, (HitType)_contract.AttackInfoHitType);
        actor.Cell?.Announce(CapturedEnemyCombatPacketFactory.CreateAttackInfo(actor.Identity, target.Identity,
            damage, _contract.AttackInfoAmmoCount, _contract.AttackInfoWeaponSlot, _contract.AttackInfoUnknown,
            _contract.AttackInfoHitType, _contract.AttackInfoWeaponInstance, _contract.AttackInfoN3Unknown));
        actor.Cell?.Announce(new HealthDamageMessage
        {
            Identity = target.Identity, Unknown1 = target.Stats.GetOrZero(CharacterStat.Health),
            Unknown2 = damage, Unknown3 = (int)CharacterStat.Health, Unknown4 = 0, Target = actor.Identity, Unknown5 = 0
        });
        ScheduleNext();
    }

    void ScheduleNext()
    {
        var intervals = _contract.CapturedLandedIntervalObservationsSeconds;
        _untilHit = intervals.Length == 0 ? _contract.RechargeSeconds : intervals[_intervalCursor++ % intervals.Length];
    }
}
