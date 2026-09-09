namespace ZoneEngine_New.Tests;

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Playfields;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Mobs;

[TestClass]
public sealed class AcceptedSubwayMerchantTests
{
    [TestMethod]
    public void AllSixCompiledPlacementsKeepExactSourceCapabilitiesAndCapturedScfu()
    {
        Assert.AreEqual(6, AcceptedSubwayMerchantCatalog.Definitions.Count);
        Assert.AreEqual(1, AcceptedSubwayMerchantCatalog.Definitions.Count(definition => definition.Binding.HasDialogue));
        foreach (var pair in AcceptedSubwayMerchantCatalog.Definitions.Zip(CapturedSubwayVendorContentProvider.Definitions))
        {
            var definition = pair.First; var expected = pair.Second;
            var npc = definition.Create(new StubItemBuilder());
            Assert.AreEqual(127, definition.Binding.PlayfieldId);
            Assert.AreEqual("SimpleChar:" + expected.SourceNpcInstance.ToString("X8"), definition.Binding.ContentNpcIdentity);
            Assert.AreEqual(expected.SourceNpcInstance, npc.Identity.Instance);
            Assert.AreEqual(expected.HasCapturedStock, definition.Binding.HasVendor);
            Assert.AreEqual(expected.SourceNpcInstance == 0x79135F51, definition.Binding.HasDialogue);
            Assert.IsTrue(definition.Binding.SourceIdentity.Contains(expected.Evidence, StringComparison.Ordinal));
            Assert.IsTrue(definition.Binding.SourceIdentity.Contains(expected.SourceVendorInstance.ToString("X8"), StringComparison.Ordinal));
            Assert.AreEqual(expected.X, npc.Position.xf); Assert.AreEqual(expected.Y, npc.Position.yf); Assert.AreEqual(expected.Z, npc.Position.zf);
            var wire = npc.BuildSpawnMessage();
            Assert.AreEqual(expected.DisplayName, wire.Name);
            Assert.AreEqual((uint)expected.AppearanceValue, wire.Appearance.Value);
            Assert.AreEqual(expected.Level, (int)wire.Level); Assert.AreEqual(expected.Health, wire.Health);
            Assert.AreEqual(0, wire.HealthDamage); Assert.AreEqual(expected.RunSpeed, (int)wire.RunSpeedBase);
            Assert.AreEqual(expected.MonsterData, (int)wire.MonsterData); Assert.AreEqual(expected.HeadMesh, (int)wire.HeadMesh);
            Assert.AreEqual(expected.CharacterFlags, (int)wire.CharacterFlags);
            Assert.IsInstanceOfType<SimpleNpcInfo>(wire.CharacterInfo);
            Assert.AreEqual(0, ((SimpleNpcInfo)wire.CharacterInfo).Family);
            Assert.AreEqual((SimpleCharFullUpdateFlags)expected.CapturedScfuFlags, wire.AdditionalFlags);
            Assert.AreEqual(~(SimpleCharFullUpdateFlags)expected.CapturedScfuFlags, wire.SuppressedFlags);
            CollectionAssert.AreEqual(expected.CapturedScfuUnknown1.ToArray(), wire.Unknown1);
            CollectionAssert.AreEqual(expected.Textures.Select(t => (t.Place, t.Id, t.Unknown)).ToArray(),
                wire.Textures.Select(t => (t.Place, t.Id, t.Unknown)).ToArray());
            CollectionAssert.AreEqual(expected.Meshes.Select(m => ((byte)m.Position, m.Id, m.OverrideTextureId, (byte)m.Layer)).ToArray(),
                wire.Meshes.Select(m => (m.Position, m.Id, m.OverrideTextureId, m.Layer)).ToArray());
            CollectionAssert.AreEqual(expected.Waypoints.Select(p => (p.X, p.Y, p.Z)).ToArray(),
                wire.Waypoints.Select(p => (p.X, p.Y, p.Z)).ToArray());
        }
    }

    [TestMethod]
    public void EveryCapturedStockRowAttachesToItsExactOwnerAndEndpointWithoutMintingItems()
    {
        var catalog = Catalog();
        int total = 0;
        foreach (var pair in AcceptedSubwayMerchantCatalog.Definitions.Zip(CapturedSubwayVendorContentProvider.Definitions))
        {
            var npc = pair.First.Create(new StubItemBuilder());
            Assert.IsTrue(AcceptedSubwayMerchantCatalog.TryAttachShop(npc, new StubItemBuilder(), catalog, out string failure), failure);
            Assert.IsNotNull(npc.Shop); Assert.AreSame(npc, npc.Shop.OwnerNpc);
            Assert.AreEqual(pair.Second.SourceVendorInstance, npc.Shop.Identity.Instance);
            Assert.AreEqual(pair.Second.VendorTemplateId, npc.Shop.Template.Id);
            Assert.IsTrue(npc.Shop.Stock.IsAcceptedSnapshot);
            Assert.AreEqual(pair.Second.Stock.Count, npc.Shop.Stock.Slots.Count);
            for (int slot = 0; slot < pair.Second.Stock.Count; slot++)
            {
                Assert.AreEqual(slot, pair.Second.Stock[slot].Slot);
                var actual = npc.Shop.Stock.Slots[slot]; var expected = pair.Second.Stock[slot];
                Assert.AreEqual(expected.LowId, actual.LowId); Assert.AreEqual(expected.HighId, actual.HighId);
                Assert.AreEqual(expected.Quality, actual.Quality);
            }
            Assert.AreEqual(pair.Second.CharacterFlags, npc.Stats.GetOrZero(CharacterStat.Flags));
            Assert.AreEqual(npc.Identity, ((SmokeLounge.AOtomation.Messaging.Messages.N3Messages.VendingMachineFullUpdateMessage)npc.Shop.BuildSpawnMessage()).NpcIdentity);
            Assert.IsFalse(AcceptedSubwayMerchantCatalog.TryAttachShop(npc, new StubItemBuilder(), catalog, out _), "Cannot replace a live endpoint.");
            total += npc.Shop.Stock.Slots.Count;
        }
        Assert.AreEqual(202, total, "Every accepted baseline stock row must be consumed exactly once.");
    }

    [TestMethod]
    public void MissingVendorOrStockEndpointRefusesWholeShopButDoesNotSuppressSocialActor()
    {
        var content = CapturedSubwayVendorContentProvider.Definitions[0];
        foreach (int missing in new[] { content.VendorTemplateId, content.Stock[0].LowId, content.Stock[0].HighId })
        {
            var npc = AcceptedSubwayMerchantCatalog.Definitions[0].Create(new StubItemBuilder());
            Assert.IsFalse(AcceptedSubwayMerchantCatalog.TryAttachShop(npc, new StubItemBuilder(), Catalog(missing), out string failure));
            Assert.IsFalse(string.IsNullOrWhiteSpace(failure)); Assert.IsNull(npc.Shop);
            Assert.AreEqual(content.DisplayName, npc.BuildSpawnMessage().Name);
            Assert.AreEqual(content.Health, npc.Stats.GetOrZero(CharacterStat.Health));
        }
        var impostor = new NpcCharacter(new() { Type = IdentityType.CanbeAffected, Instance = content.SourceNpcInstance }, new StubItemBuilder())
        { Name = content.DisplayName };
        Assert.IsFalse(AcceptedSubwayMerchantCatalog.TryAttachShop(impostor, new StubItemBuilder(), Catalog(), out _));
    }

    [TestMethod]
    public void CombatUnresolvedMerchantsRemainVisibleWithoutFallbackRebasePatrolOrRegeneration()
    {
        foreach (var definition in AcceptedSubwayMerchantCatalog.Definitions)
        {
            var npc = definition.Create(new StubItemBuilder());
            var original = (npc.Position.xf, npc.Position.yf, npc.Position.zf);
            npc.Stats.Set(CharacterStat.Health, 100);
            npc.Rebase(); npc.RebaseWeapons();
            npc.StartFighting(new() { Type = IdentityType.CanbeAffected, Instance = 123 }, 0);
            npc.Tick(60);
            Assert.AreEqual(Identity.None, npc.FightingTarget); Assert.AreEqual(0, npc.Weapons.Count);
            Assert.AreEqual(180, npc.Stats.GetOrZero(CharacterStat.Level));
            Assert.AreEqual(100, npc.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(original, (npc.Position.xf, npc.Position.yf, npc.Position.zf));
            Assert.IsNotNull(npc.BuildSpawnMessage());
        }
    }

    static StubCatalog Catalog(int missing = 0)
    {
        var result = new StubCatalog();
        foreach (var content in CapturedSubwayVendorContentProvider.Definitions)
        {
            if (content.VendorTemplateId != missing) result.Add(content.VendorTemplateId, 1);
            foreach (var row in content.Stock)
            {
                if (row.LowId != missing) result.Add(row.LowId, row.Quality);
                if (row.HighId != missing) result.Add(row.HighId, row.Quality);
            }
        }
        return result;
    }
}
