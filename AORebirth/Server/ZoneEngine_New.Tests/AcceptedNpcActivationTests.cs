namespace ZoneEngine_New.Tests;

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

[TestClass]
public sealed class AcceptedNpcActivationTests
{
    [TestMethod]
    public void ScarlettUsesExactAcceptedIdentityLevelStatsAppearanceAndDialogueKey()
    {
        var f = new Fixture(7010); f.Service.Activate();
        var npc = f.Registry.Dynels().OfType<NpcCharacter>().Single();
        Assert.IsTrue(f.Service.TryGetBinding(npc, out var binding));
        Assert.AreEqual("SimpleChar:7A18B924", binding.ContentNpcIdentity);
        Assert.AreEqual(unchecked((int)0x7A18B924), npc.Identity.Instance);
        Assert.IsTrue(binding.HasDialogue); Assert.IsFalse(binding.HasVendor); // DOJA hand-in is dialogue trade, not a shop.
        Assert.AreEqual(150, npc.Stats.GetOrZero(CharacterStat.Level));
        Assert.AreEqual(16042, npc.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(16042, npc.Stats.GetOrZero(CharacterStat.MaxHealth));
        Assert.AreEqual(26090, npc.Stats.GetOrZero(CharacterStat.MonsterData));
        Assert.AreEqual(117, npc.Stats.GetOrZero(CharacterStat.Scale));
        Assert.AreEqual(432, npc.Stats.GetOrZero(CharacterStat.RunSpeed));
        Assert.AreEqual(277352961, npc.Stats.GetOrZero(CharacterStat.Flags));
        Assert.AreEqual(104.180695f, npc.Position.xf);
        var spawn = npc.BuildSpawnMessage();
        Assert.AreEqual(5, spawn.Textures.Length);
        Assert.AreEqual(2, spawn.Meshes.Length);
        Assert.AreEqual(223846u, spawn.HeadMesh);
        Assert.AreEqual(7010, spawn.PlayfieldId);
        Assert.IsTrue(binding.SourceIdentity.Contains("capture:20260821-222107", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RepeatedActivationNeverCreatesAnotherActorForThePlacement()
    {
        var f = new Fixture(7010); f.Service.Activate(); var first = f.Registry.Dynels().Single();
        f.Service.Activate(); f.Service.Tick(); f.Service.Activate();
        Assert.AreSame(first, f.Registry.Dynels().Single());
    }

    [TestMethod]
    public void WrongPlayfieldNeverActivatesAcceptedPlacement()
    {
        var f = new Fixture(7011); f.Service.Activate();
        Assert.AreEqual(0, f.Registry.Dynels().Count());
    }

    [TestMethod]
    public void FixedIdentityCollisionFailsWithoutOverwritingOrAllocatingFallback()
    {
        var f = new Fixture(7010);
        var existing = Scarlett().Create(new StubItemBuilder());
        f.Registry.Register(existing);
        Assert.ThrowsException<InvalidOperationException>(() => f.Service.Activate());
        Assert.AreSame(existing, f.Registry.Dynels().Single());
        Assert.IsFalse(f.Service.TryGetBinding(existing, out _));
    }

    [TestMethod]
    public void SameIdentityAndNameCannotInheritReplacedActorsDialogueCapability()
    {
        var f = new Fixture(7010); f.Service.Activate();
        var first = (NpcCharacter)f.Registry.Dynels().Single();
        var replacement = Scarlett().Create(new StubItemBuilder());
        replacement.Playfield = f.Playfield; f.Registry.Register(replacement);
        Assert.IsFalse(f.Service.TryGetBinding(first, out _));
        Assert.IsFalse(f.Service.TryGetBinding(replacement, out _));
        Assert.IsFalse(f.Registry.UnregisterExact(first));
        Assert.AreSame(replacement, f.Registry.Dynels().Single());
        f.Service.Tick();
        Assert.IsFalse(f.Service.TryGetBinding(replacement, out _));
    }

    [TestMethod]
    public void DespawnAndTransferInvalidateBindingAndDoNotRespawnOnRepeatedActivation()
    {
        var f = new Fixture(7010); f.Service.Activate(); var npc = (NpcCharacter)f.Registry.Dynels().Single();
        npc.Playfield = new Fixture(7010).Playfield;
        Assert.IsFalse(f.Service.TryGetBinding(npc, out _));
        npc.Playfield = f.Playfield; f.Registry.UnregisterExact(npc);
        Assert.IsFalse(f.Service.TryGetBinding(npc, out _));
        f.Service.Tick(); f.Service.Activate();
        Assert.AreEqual(0, f.Registry.Dynels().Count());
    }

    [TestMethod]
    public void ShutdownInvalidatesCapabilitiesAndCannotActivateAgain()
    {
        var f = new Fixture(7010); f.Service.Activate(); var npc = (NpcCharacter)f.Registry.Dynels().Single();
        f.Service.Shutdown(); f.Service.Shutdown(); f.Service.Activate();
        Assert.IsFalse(f.Service.TryGetBinding(npc, out _));
        Assert.AreEqual(1, f.Registry.Dynels().Count());
    }

    [TestMethod]
    public void AcceptedSocialPlacementDoesNotGainGenericUnarmedCombatOrWifu()
    {
        var npc = Scarlett().Create(new StubItemBuilder());
        npc.Rebase(); npc.RebaseWeapons();
        npc.StartFighting(new Identity { Type = IdentityType.CanbeAffected, Instance = 1 }, 0);
        Assert.AreEqual(Identity.None, npc.FightingTarget);
        Assert.AreEqual(0, npc.Weapons.Count);
        Assert.AreEqual(0, npc.BuildWeaponInstanceMessages().Count);
    }

    static AcceptedSocialNpcCatalog.Definition Scarlett() => AcceptedSocialNpcCatalog.Definitions.Single(d => d.Binding.PlayfieldId == 7010);

    sealed class Fixture
    {
        internal readonly Playfield Playfield;
        internal readonly DynelRegistry Registry = new();
        internal readonly AcceptedNpcActivationService Service;
        internal Fixture(int id)
        {
            // No listener, database, heartbeat or client is required for owner-reference tests.
            Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Playfield, new Identity { Type = IdentityType.Playfield2, Instance = id });
            Service = new AcceptedNpcActivationService(Playfield, Registry, new PlayfieldLocality(id, null), new StubItemBuilder(), new StubCatalog());
        }
    }
}
