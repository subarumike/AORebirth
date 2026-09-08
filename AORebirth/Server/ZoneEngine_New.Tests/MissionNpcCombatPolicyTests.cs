namespace ZoneEngine_New.Tests;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using AORebirth.Core.Playfields;
using ZoneEngine_New.Core.Mobs;

[TestClass]
public sealed class MissionNpcCombatPolicyTests
{
    [DataTestMethod]
    [DataRow(1, 2, 4)]
    [DataRow(10, 10, 17)]
    [DataRow(220, 220, 332)]
    public void MeleePreservesExistingMissionDifficultyAndPacketPolicy(int level, int minimum, int maximum)
    {
        var contract = MissionNpcCombatPolicy.Create(1_000_001, level, false, new StubItemBuilder(), new StubCatalog(), out var weapon);
        Assert.IsNull(weapon); Assert.IsTrue(contract.IsCombatReady);
        Assert.AreEqual(minimum, contract.MinDamage); Assert.AreEqual(maximum, contract.MaxDamage);
        Assert.AreEqual(0.0, contract.AttackStartDelaySeconds); Assert.AreEqual(0.25, contract.FirstHitDelaySeconds);
        Assert.AreEqual(2.0, contract.RechargeSeconds); Assert.AreEqual(8.0, contract.CapturedAttackRange);
        CollectionAssert.AreEqual(new[] { minimum, maximum }, contract.CapturedDamageObservations);
        var saw = CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(new() { Type = IdentityType.CanbeAffected, Instance = 1_000_001 }, contract);
        Assert.AreEqual(20, saw.CloseCombatInitiative); Assert.AreEqual(20, saw.DistanceWeaponInitiative);
        Assert.AreEqual(0, saw.AggDef); Assert.AreEqual(1, saw.Specials.Length);
        Assert.AreEqual(0x53495731, contract.AttackInfoWeaponInstance);
        Assert.AreEqual(3, contract.AttackInfoHitType); Assert.AreEqual(-1, contract.AttackInfoAmmoCount);
    }

    [TestMethod]
    public void AttachedGunSelectsOnlyApprovedPresentTemplateAndActualClampedQuality()
    {
        var catalog = new StubCatalog().Add(121570, 23);
        var contract = MissionNpcCombatPolicy.Create(1_000_001, 100, true, new StubItemBuilder(), catalog, out var weapon);
        Assert.IsNotNull(weapon); Assert.IsTrue(contract.IsCombatReady);
        Assert.AreEqual(121570, weapon.LowId); Assert.AreEqual(23, weapon.Quality);
        Assert.AreEqual(weapon.Quality, contract.WeaponQuality);
        Assert.AreEqual(67110401, contract.WeaponDefinition.SignedStatValue(CharacterStat.Flags));
        Assert.AreEqual(235, contract.WeaponDefinition.SignedStatValue(CharacterStat.AttackDelay));
        Assert.AreEqual(0.25, contract.AttackStartDelaySeconds); Assert.AreEqual(0.5, contract.FirstHitDelaySeconds);
        Assert.AreEqual(30, contract.SpecialAttackWeaponUnknown1);
        Assert.AreEqual(6, contract.AttackInfoWeaponSlot); Assert.AreEqual(0, contract.AttackInfoWeaponInstance);
        Assert.IsNotNull(new MissionNpcCombatRuntime(contract, weapon));
        var wrong = new StubItemBuilder().Create(121568, 121568, 23, AORebirth.Enums.ItemSource.Other);
        Assert.ThrowsException<InvalidOperationException>(() => new MissionNpcCombatRuntime(contract, wrong));
    }

    [TestMethod]
    public void ExistingGunUnavailablePolicyUsesSiw1ButBodyMeshDoesNotSelectGun()
    {
        var missing = MissionNpcCombatPolicy.Create(1_000_001, 10, true, new StubItemBuilder(), new StubCatalog(), out var noWeapon);
        Assert.IsNull(noWeapon); Assert.AreEqual(0x53495731, missing.AttackInfoWeaponInstance);
        var bodyOnly = MissionNpcCombatPolicy.Create(1_000_001, 10, false, new StubItemBuilder(), new StubCatalog().Add(121570, 23), out var bodyWeapon);
        Assert.IsNull(bodyWeapon); Assert.AreEqual(0x53495731, bodyOnly.AttackInfoWeaponInstance);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(221)]
    public void InvalidMissionLevelDoesNotSilentlySelectAnotherVariant(int level)
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => MissionNpcCombatPolicy.Create(1_000_001, level, false,
            new StubItemBuilder(), new StubCatalog(), out _));
}
