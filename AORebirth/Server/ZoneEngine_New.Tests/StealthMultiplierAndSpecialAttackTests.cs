namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

using AORebirth.Core.GameData;
using AORebirth.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Helpers;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Movement;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.WorldSimulation;

[TestClass]
public sealed class StealthMultiplierAndSpecialAttackTests
{
    [TestMethod]
    public void MissingStealthMultiplierIsRejectedAndDoesNotCacheAnEmptyTable()
    {
        string staged = RuntimeCatalogTests.StagedRoot();
        DirectoryInfo root = Directory.CreateTempSubdirectory("stealth-missing-");
        try
        {
            string path = Path.Combine(root.FullName, StealthMultiplierTable.FileName);
            AssertRejected(path, () => StealthMultiplierTable.Validate(root.FullName));
            AssertRejected(path, () => StealthMultiplierTable.Roll(root.FullName, 1000, 0));
            File.Copy(Path.Combine(staged, StealthMultiplierTable.FileName), path);
            StealthMultiplierTable.Validate(root.FullName);
            Assert.AreEqual(2, StealthMultiplierTable.Roll(root.FullName, 1000, 0));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void ZonePackagePreflightRejectsMissingStealthMultiplier()
    {
        AssertNoGameDataOverride();
        DirectoryInfo package = Directory.CreateTempSubdirectory("stealth-package-");
        try
        {
            string root = Path.Combine(package.FullName, "GameData");
            Directory.CreateDirectory(Path.Combine(root, "Playfields"));
            foreach (string file in new[] { "ItemTemplates.json", "VendingMachines.json", "MonsterData.json", "Xp.json", "AlienXp.json" })
                File.WriteAllText(Path.Combine(root, file), "{}");
            File.WriteAllBytes(Path.Combine(root, "items.dat"), [1]);

            AssertRejected(Path.Combine(root, StealthMultiplierTable.FileName),
                () => RuntimeStartup.ValidatePackage(package.FullName, skipPlayfieldPackagePin: true));
        }
        finally
        {
            package.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("{")]
    [DataRow("null")]
    [DataRow("{\"rows\":[]}")]
    [DataRow("{\"rows\":[{\"skill\":100,\"weights\":[]}]}")]
    [DataRow("{\"rows\":[{\"skill\":100,\"weights\":[1,-0.1]}]}")]
    [DataRow("{\"rows\":[{\"skill\":100,\"weights\":[1.1]}]}")]
    [DataRow("{\"rows\":[{\"skill\":100,\"weights\":[0,0]}]}")]
    [DataRow("{\"rows\":[{\"skill\":100,\"weights\":[1e400]}]}")]
    [DataRow("{\"rows\":[{\"skill\":-1,\"weights\":[1]}]}")]
    [DataRow("{\"rows\":[{\"skill\":100,\"weights\":[1]},{\"skill\":100,\"weights\":[0,1]}]}")]
    public void InvalidStealthMultiplierReportsEffectivePathAndReason(string contents)
        => CheckRejectedCatalog(path => File.WriteAllText(path, contents));

    [TestMethod]
    public void UnreadableStealthMultiplierReportsEffectivePathAndReason()
        => CheckRejectedCatalog(path => Directory.CreateDirectory(path));

    [TestMethod]
    [DataRow(0, 100, HitType.Normal)]
    [DataRow(100, 140, HitType.Critical)]
    public void AimedShotKeepsWeaponMaximumAndExistingCriticalBonus(int criticalChance, int expectedDamage, HitType expectedHitType)
    {
        using var fight = new SpecialAttackFight(RuntimeCatalogTests.StagedRoot(), maxDamage: 100, attackRating: 0,
            criticalChance: criticalChance);
        fight.Attacker.Stats.Set(CharacterStat.AimedShot, 99);
        fight.Fire(CharacterStat.AimedShot);
        Assert.AreEqual(expectedDamage, fight.DamageToTarget);
        Assert.AreEqual<HitType?>(expectedHitType, fight.Target.LastHitType);
        Assert.AreEqual(9, WeaponAmmo.Rounds(fight.Weapon));
    }

    [TestMethod]
    public void HighSkillAimedShotAppliesItsExistingCapBeforeAbsorb()
    {
        using var fight = new SpecialAttackFight(RuntimeCatalogTests.StagedRoot(), maxDamage: 7000, attackRating: 0, criticalChance: 0);
        fight.Attacker.Stats.Set(CharacterStat.AimedShot, 1000);
        fight.Target.Stats.Set(CharacterStat.AbsorbProjectileAC, 1800);

        // All positive bands at 1000 skill are at least 2x, so even the smallest roll reaches the existing 13000 cap.
        fight.Fire(CharacterStat.AimedShot);
        Assert.AreEqual(11200, fight.DamageToTarget);
        Assert.AreEqual(0, fight.Target.Stats.GetOrZero(CharacterStat.AbsorbProjectileAC));
        Assert.AreEqual<HitType?>(HitType.Normal, fight.Target.LastHitType);
    }

    [TestMethod]
    [DataRow(CharacterStat.FlingShot, 0, 20, 9)]
    [DataRow(CharacterStat.Burst, 100, 60, 7)]
    public void OtherRangedSpecialsRetainArmorAmmoAndBurstCriticalRules(CharacterStat special, int criticalChance,
        int expectedDamage, int expectedRounds)
    {
        AssertNoGameDataOverride();
        DirectoryInfo root = Directory.CreateTempSubdirectory("ordinary-special-");
        try
        {
            using var fight = new SpecialAttackFight(root.FullName, maxDamage: 100, attackRating: 400,
                criticalChance: criticalChance);
            fight.Fire(special);
            Assert.AreEqual(expectedDamage, fight.DamageToTarget);
            Assert.AreEqual<HitType?>(HitType.Normal, fight.Target.LastHitType);
            Assert.AreEqual(expectedRounds, WeaponAmmo.Rounds(fight.Weapon));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    static void CheckRejectedCatalog(Action<string> write)
    {
        AssertNoGameDataOverride();
        DirectoryInfo root = Directory.CreateTempSubdirectory("stealth-invalid-");
        try
        {
            string path = Path.Combine(root.FullName, StealthMultiplierTable.FileName);
            write(path);
            AssertRejected(path, () => StealthMultiplierTable.Validate(root.FullName));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    static void AssertRejected(string path, Action validate)
    {
        InvalidDataException error = Assert.ThrowsExactly<InvalidDataException>(validate);
        StringAssert.Contains(error.Message, path);
        Assert.IsNotNull(error.InnerException);
        Assert.IsFalse(string.IsNullOrWhiteSpace(error.InnerException.Message));
        StringAssert.Contains(error.Message, error.InnerException.Message);
    }

    static void AssertNoGameDataOverride()
        => Assert.IsTrue(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(GameDataPaths.OverrideEnvironmentVariableName)));

    sealed class SpecialAttackFight : IDisposable
    {
        const int TargetHealth = 20000;
        readonly ServiceProvider _services;

        internal Player Attacker { get; }
        internal DamageTarget Target { get; }
        internal Item Weapon { get; }
        internal int DamageToTarget => TargetHealth - Target.Stats.GetOrZero(CharacterStat.Health);

        internal SpecialAttackFight(string root, int maxDamage, int attackRating, int criticalChance)
        {
            _services = new ServiceCollection()
                .AddSingleton<IGameData>(new StubGameData(HashItemCatalog.Parse("{}"), rootPath: root))
                .AddSingleton<DynelRegistry>()
                .AddSingleton<WorldSimulationAccess>()
                .BuildServiceProvider();
            var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(Playfield).GetField("_serviceProvider", InstancePrivate)!.SetValue(playfield, _services);
            typeof(Playfield).GetField("<FightModes>k__BackingField", InstancePrivate)!
                .SetValue(playfield, DistrictFightModes.None);

            Attacker = TestWorld.CreatePlayer(1);
            Attacker.Playfield = playfield;
            Attacker.Stats.Set(CharacterStat.AMSModifier, attackRating);
            Attacker.Stats.Set(CharacterStat.CriticalIncrease, criticalChance);
            Attacker.Stats.Set(CharacterStat.ProjectileDamageModifier, 0);
            Attacker.Stats.Set(CharacterStat.DamageOverrideType, 0);
            Attacker.Stats.Set(CharacterStat.Concealment, 500);
            Target = new DamageTarget { Playfield = playfield };
            Target.Stats.Set(CharacterStat.MaxHealth, TargetHealth);
            Target.Stats.Set(CharacterStat.Health, TargetHealth);
            Target.Stats.Set(CharacterStat.Perception, 1);
            Target.Stats.Set(CharacterStat.DMSModifier, 0);
            Target.Stats.Set(CharacterStat.CritialResistance, 0);
            Target.Stats.Set(CharacterStat.ProjectileAC, 10000);

            Weapon = TestWorld.CreateItem(can: CanFlags.AimedShot | CanFlags.FlingShot | CanFlags.Burst);
            Weapon.Definition.Stats[CharacterStat.MinDamage] = 10;
            Weapon.Definition.Stats[CharacterStat.MaxDamage] = maxDamage;
            Weapon.Definition.Stats[CharacterStat.DamageBonus] = 40;
            Weapon.Definition.Stats[CharacterStat.DamageType] = (int)CharacterStat.ProjectileAC;
            Weapon.Definition.Stats[CharacterStat.MaxEnergy] = 10;
            Weapon.Definition.Stats[CharacterStat.AmmoType] = (int)AmmoType.Bullets;
            Attacker.Weapons[WeaponSlot.MainHand] = new CharacterWeapon { Item = Weapon, Wielder = Attacker, LogicalSlot = WeaponSlot.MainHand };
        }

        internal void Fire(CharacterStat special)
        {
            if (special == CharacterStat.AimedShot)
                Assert.IsTrue(Attacker.Motor.ApplyAction(MovementAction.SwitchToSneak));
            Attacker.SetFightingTarget(Target.Identity);
            Assert.AreEqual(SpecialAttacks.FireResult.Fired, SpecialAttacks.TryFire(Attacker, Target, special));
        }

        public void Dispose() => _services.Dispose();
    }

    sealed class DamageTarget() : NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 2 }, new StubItemBuilder())
    {
        internal HitType? LastHitType { get; private set; }

        protected override void OnDamaged(Character attacker, int hpRemoved, HitType hitType)
            => LastHitType = hitType;
    }
}
