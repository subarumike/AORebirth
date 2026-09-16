namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using AORebirth.Enums;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// Worn appearance comes from the OnWear spells of the pages a character shows: armor and
    /// social for players, the single inventory for NPCs.
    /// </summary>
    [TestClass]
    public sealed class WearAppearanceRebaseTests
    {
        const int ArmorBody = 0x11;
        const int ArmorHead = 0x12;
        const int ArmorBack = 0x13;
        const int SocialBody = 0x31;
        const int ShowSocial = 0x20;
        const int SocialOnly = 0x40;

        [TestMethod]
        public void Worn_texture_spell_lands_in_player_textures_and_leaves_with_the_item()
        {
            Player player = TestWorld.CreatePlayer(1);
            player.Inventory.Armor.Add(ArmorBody, TextureItem(place: 2, textureId: 99101));

            player.Rebase();

            CollectionAssert.AreEqual(new[] { (2, 99101) }, Places(player));
            Assert.AreEqual(99101, player.BuildSpawnMessage().Textures[2].Id);

            player.Inventory.Armor.Remove(ArmorBody);
            player.Rebase();

            Assert.AreEqual(0, player.Textures.Count);
        }

        [TestMethod]
        public void Social_page_overlays_armor_only_while_the_social_flag_is_set()
        {
            Player player = TestWorld.CreatePlayer(2);
            player.Inventory.Armor.Add(ArmorBody, TextureItem(place: 2, textureId: 1111));
            player.Inventory.Armor.Add(ArmorBack, TextureItem(place: 3, textureId: 3333));
            player.Inventory.Social.Add(SocialBody, TextureItem(place: 2, textureId: 2222));
            player.Rebase();

            CollectionAssert.AreEqual(new[] { (2, 1111), (3, 3333) }, Places(player));

            player.TryApplyVisualFlags(ShowSocial);
            CollectionAssert.AreEqual(new[] { (2, 2222), (3, 3333) }, Places(player));

            player.TryApplyVisualFlags(ShowSocial | SocialOnly);
            CollectionAssert.AreEqual(new[] { (2, 2222) }, Places(player));

            player.TryApplyVisualFlags(0);
            CollectionAssert.AreEqual(new[] { (2, 1111), (3, 3333) }, Places(player));
        }

        [TestMethod]
        public void Visual_flag_change_is_persisted_as_a_stat()
        {
            Player player = TestWorld.CreatePlayer(3);

            Assert.IsTrue(player.TryApplyVisualFlags(ShowSocial));

            Assert.AreEqual(ShowSocial, player.Stats.Get(CharacterStat.VisualFlags));
            Assert.IsFalse(player.TryApplyVisualFlags(-1));
        }

        [TestMethod]
        public void Texture_spell_is_skipped_until_its_requirement_is_met()
        {
            Player player = TestWorld.CreatePlayer(4);
            Item item = TextureItem(place: 2, textureId: 4242);
            item.Definition.SpellList[EventType.OnWear][0].Requirements =
            [
                new ItemRequirement
                {
                    StatNumber = (int)CharacterStat.Strength,
                    Operator = (int)Operator.GreaterThan,
                    Value = 50
                }
            ];
            player.Inventory.Armor.Add(ArmorBody, item);

            player.Stats.Set(CharacterStat.Strength, 10);
            player.Rebase();
            Assert.AreEqual(0, player.Textures.Count);

            player.Stats.Set(CharacterStat.Strength, 100);
            player.Rebase();
            CollectionAssert.AreEqual(new[] { (2, 4242) }, Places(player));
        }

        [TestMethod]
        public void Worn_head_mesh_takes_the_head_slot_from_the_head_mesh_stat()
        {
            Player player = TestWorld.CreatePlayer(5);
            player.Stats.Set(CharacterStat.HeadMesh, 40683);
            player.Rebase();
            Assert.AreEqual(40683u, HeadSlotMesh(player));

            player.Inventory.Armor.Add(ArmorHead, MeshItem(FunctionType.HeadMesh, meshId: 12345));
            player.Rebase();
            Assert.AreEqual(12345u, HeadSlotMesh(player));

            player.Inventory.Armor.Remove(ArmorHead);
            player.Rebase();
            Assert.AreEqual(40683u, HeadSlotMesh(player));
        }

        [TestMethod]
        public void Wear_page_slot_decides_the_mesh_position()
        {
            Player player = TestWorld.CreatePlayer(6);
            player.Inventory.Armor.Add(ArmorBack, MeshItem(FunctionType.BackMesh, meshId: 777, overrideTextureId: 888));
            player.Rebase();

            Mesh back = player.Meshes.Single();
            Assert.AreEqual(5, back.Position);
            Assert.AreEqual(777u, back.Id);
            Assert.AreEqual(888, back.OverrideTextureId);
            Assert.AreEqual((byte)MeshLayer.Equipment, back.Layer);
        }

        [TestMethod]
        public void Npc_wears_its_single_inventory_for_textures()
        {
            NpcCharacter npc = CreateNpc(7);
            npc.Equipment.Add(npc.Equipment.Offset, TextureItem(place: 4, textureId: 55501));

            npc.Rebase();

            CollectionAssert.AreEqual(new[] { (4, 55501) }, Places(npc));
        }

        [TestMethod]
        public void Spawn_textures_survive_a_rebase_and_lose_only_the_places_worn_items_claim()
        {
            NpcCharacter npc = CreateNpc(8);
            npc.SetSpawnTexture(0, 0);
            npc.SetSpawnTexture(1, 247966);
            npc.Equipment.Add(npc.Equipment.Offset, TextureItem(place: 1, textureId: 31337));

            npc.Rebase();

            CollectionAssert.AreEqual(new[] { (0, 0), (1, 31337) }, Places(npc));

            npc.Equipment.Remove(npc.Equipment.Offset);
            npc.Rebase();

            CollectionAssert.AreEqual(new[] { (0, 0), (1, 247966) }, Places(npc));
        }

        [TestMethod]
        public void Repeated_rebases_neither_duplicate_nor_drop_worn_appearance()
        {
            Player player = TestWorld.CreatePlayer(9);
            player.Inventory.Armor.Add(ArmorBody, TextureItem(place: 2, textureId: 99101));
            player.Inventory.Armor.Add(ArmorBack, MeshItem(FunctionType.BackMesh, meshId: 777));

            player.Rebase();
            player.Rebase();
            player.Rebase();

            CollectionAssert.AreEqual(new[] { (2, 99101) }, Places(player));
            Assert.AreEqual(777u, player.Meshes.Single().Id);
            // A rebase that changed nothing must not leave an announce pending.
            Assert.IsFalse(player.ConsumeAppearanceDirty());
        }

        static (int, int)[] Places(Character character)
            => character.Textures.Select(texture => (texture.place, texture.Texture)).ToArray();

        static uint HeadSlotMesh(Player player)
        {
            SimpleCharFullUpdateMessage spawn = player.BuildSpawnMessage();
            return spawn.Meshes
                .Single(mesh => mesh.Position == 0 && mesh.Layer == (byte)MeshLayer.Equipment)
                .Id;
        }

        static NpcCharacter CreateNpc(int instance)
            => new(new Identity { Type = IdentityType.CanbeAffected, Instance = instance }, new StubItemBuilder());

        static Item TextureItem(int place, int textureId)
            => WearItem(FunctionType.Texture, [textureId, place]);

        static Item MeshItem(FunctionType function, int meshId, int overrideTextureId = 0)
            => WearItem(function, [overrideTextureId, meshId]);

        static Item WearItem(FunctionType function, List<object> arguments)
            => new()
            {
                InstanceId = 1,
                LowId = 5000,
                HighId = 5000,
                Quality = 1,
                Definition = new ItemTemplate
                {
                    Id = 5000,
                    Name = "Appearance Item",
                    Quality = 1,
                    SpellList = new Dictionary<EventType, List<ItemSpell>>
                    {
                        [EventType.OnWear] =
                        [
                            new ItemSpell { FunctionType = (int)function, Arguments = arguments }
                        ]
                    }
                }
            };
    }
}
