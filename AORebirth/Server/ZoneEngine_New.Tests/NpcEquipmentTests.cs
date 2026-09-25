namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Mobs;

    using AORebirth.Enums;

    [TestClass]
    public sealed class NpcEquipmentTests
    {
        [TestMethod]
        public void EquipLeetBite1HasEquipMonsterWeaponHash()
        {
            string gameDataRoot = FindGameDataRoot();
            var catalog = new ItemTemplateCatalog(
                new EmptyNames(),
                new StubGameData(new HashItemCatalog(new Dictionary<string, string[]>(), new Dictionary<string, HashInstance>()), rootPath: gameDataRoot),
                new StubLogger());

            Assert.IsTrue(catalog.TryGet(120912, out ItemTemplate equipper), "items.dat missing 120912");
            Assert.IsTrue(catalog.TryGet(120910, out ItemTemplate weapon), "items.dat missing 120910");
            Assert.AreEqual(ItemClass.Npc, (ItemClass)GetStat(equipper, CharacterStat.ItemClass));

            string dump = "equipper " + DumpSpells(equipper) + " | weapon " + DumpSpells(weapon);
            Assert.IsTrue(
                TryFindEquipMonsterWeaponHash(equipper, out string hash),
                "120912 has no EquipMonsterWeapon hash. " + dump);
            Assert.AreEqual("LEW1", hash, dump);
        }

        [TestMethod]
        public void FillEquipmentExpandsMonsterWeaponHash()
        {
            var catalog = new StubCatalog()
                .AddNpcEquipper(120912, "LEW1")
                .AddWeapon(120910, 1)
                .AddWeapon(120911, 400);
            var items = new CatalogItemBuilder(catalog);
            var gameData = new StubGameData(
                new HashItemCatalog(new Dictionary<string, string[]>(), new Dictionary<string, HashInstance>()),
                monsterWeapons: new Dictionary<string, int[]> { ["LEW1"] = [120910, 120911] });

            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 1 }, items)
            {
                MobTemplate = new MobTemplate
                {
                    Name = "Beach Leet",
                    Equipment = [[120912, 120912]]
                }
            };
            npc.Stats.Set(CharacterStat.Level, 1);

            npc.FillEquipment(gameData);

            Assert.AreEqual(2, npc.Equipment.Content.Count);
            Assert.AreEqual(120912, npc.Equipment.Content[0].LowId);
            Assert.AreEqual(120910, npc.Equipment.Content[1].LowId);
            Assert.AreEqual(ItemClass.Weapon, (ItemClass)npc.Equipment.Content[1].GetStat(CharacterStat.ItemClass));
        }

        [TestMethod]
        public void OnlyFirstVisualWeaponIsShown()
        {
            var catalog = new StubCatalog()
                .AddWeapon(9, 1)
                .AddMeleeWeapon(10, mesh: 501)
                .AddMeleeWeapon(11, mesh: 502)
                .AddMeleeWeapon(12, mesh: 503);

            NpcCharacter npc = CreateNpc(catalog, 2, [[9, 9], [10, 10], [11, 11], [12, 12]]);
            npc.FillEquipment(new StubGameData(HashItemCatalog.Parse("{}")));
            npc.RebaseWeapons();

            Assert.AreEqual(10, npc.VisualRightHand!.LowId);
            Assert.AreEqual(501, HandMesh(npc, 1));
            Assert.AreEqual(0, HandMesh(npc, 2));
            Assert.AreEqual(1, npc.BuildWeaponInstanceMessages().Count);
        }

        [TestMethod]
        public void MonsterWeaponsAttackWhileFirstVisualWeaponSendsRightHandInstance()
        {
            var catalog = new StubCatalog()
                .AddMeleeWeapon(30, mesh: 501)
                .AddMeleeWeapon(31, mesh: 502)
                .AddNpcEquipper(20, "SIW1")
                .AddNpcEquipper(21, "SIW2")
                .AddWeapon(11, 1)
                .AddWeapon(12, 1);
            catalog.Require(30).Stats[CharacterStat.AttackRange] = 25;
            var gameData = new StubGameData(
                HashItemCatalog.Parse("{}"),
                monsterWeapons: new Dictionary<string, int[]> { ["SIW1"] = [11, 11], ["SIW2"] = [12, 12] });

            NpcCharacter npc = CreateNpc(catalog, 3, [[30, 30], [20, 20], [31, 31], [21, 21]]);
            npc.FillEquipment(gameData);
            npc.RebaseWeapons();

            List<WeaponItemFullUpdateMessage> instances = npc.BuildWeaponInstanceMessages();
            Assert.AreEqual(1, instances.Count);
            Assert.AreEqual((short)(0x0100 | (int)WeaponSlots.Righthand), instances[0].Unknown2);
            Assert.AreEqual(0, HandMesh(npc, 2));

            Assert.AreEqual(2, npc.Weapons.Count);
            CharacterWeapon first = npc.Weapons[WeaponSlot.Npc0];
            CharacterWeapon second = npc.Weapons[WeaponSlot.Npc1];
            Assert.AreEqual(11, first.Item!.LowId);
            Assert.AreEqual(12, second.Item!.LowId);
            Assert.AreSame(npc.VisualRightHand, first.DamageOverride);
            Assert.AreSame(npc.VisualRightHand, second.DamageOverride);
            Assert.AreEqual(25.0, first.GetAttackRange());
            Assert.AreEqual(0, first.WireSlot);
            Assert.AreEqual(1, second.WireSlot);
        }

        [TestMethod]
        public void VisualWeaponWithoutMonsterWeaponDoesNotAttack()
        {
            var catalog = new StubCatalog().AddMeleeWeapon(10, mesh: 501);

            NpcCharacter npc = CreateNpc(catalog, 4, [[10, 10]]);
            npc.FillEquipment(new StubGameData(HashItemCatalog.Parse("{}")));
            npc.RebaseWeapons();

            Assert.AreEqual(501, HandMesh(npc, 1));
            Assert.AreEqual(0, npc.Weapons.Count);
            Assert.AreEqual(1, npc.BuildWeaponInstanceMessages().Count);
        }

        [TestMethod]
        public void RepeatedRebaseDoesNotAccumulateMonsterWeaponsOrKeepStaleHands()
        {
            var catalog = new StubCatalog()
                .AddMeleeWeapon(10, mesh: 501)
                .AddNpcEquipper(20, "SIW1")
                .AddWeapon(11, 1);
            var gameData = new StubGameData(
                HashItemCatalog.Parse("{}"),
                monsterWeapons: new Dictionary<string, int[]> { ["SIW1"] = [11, 11] });

            NpcCharacter npc = CreateNpc(catalog, 6, [[10, 10], [20, 20]]);
            npc.FillEquipment(gameData);
            npc.Rebase();
            npc.Rebase();
            Assert.AreEqual(1, npc.Weapons.Count);

            npc.Equipment.Remove(0);
            npc.Rebase();
            Assert.IsNull(npc.VisualRightHand);
            Assert.AreEqual(0, HandMesh(npc, 1));
            Assert.IsNull(npc.Weapons[WeaponSlot.Npc0].DamageOverride);
        }

        [TestMethod]
        public void EquipmentDoesNotModifyNpcStats()
        {
            var catalog = new StubCatalog().AddWeapon(10, 1);
            catalog.Require(10).SpellList[EventType.OnWear] =
            [
                new ItemSpell
                {
                    FunctionType = (int)FunctionType.Modify,
                    Arguments = [(int)CharacterStat.Strength, 5]
                }
            ];

            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 5 }, new CatalogItemBuilder(catalog))
            {
                MobTemplate = new MobTemplate
                {
                    Name = "Buffed Gear",
                    Equipment = [[10, 10]]
                }
            };
            npc.Stats.Set(CharacterStat.Level, 1);
            npc.Stats.Set(CharacterStat.Strength, 10);
            npc.FillEquipment(new StubGameData(HashItemCatalog.Parse("{}")));
            npc.RebaseStats();

            Assert.AreEqual(10, npc.Stats.GetOrZero(CharacterStat.Strength));
            Assert.AreEqual(0, npc.Stats.GetOrZero(CharacterStat.Strength, StatDetail.Bonus));
        }

        [TestMethod]
        public void LootgiverItemRunsItsOnWearModifiers()
        {
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 7 }, new CatalogItemBuilder(new StubCatalog()));
            npc.Stats.Set(CharacterStat.Strength, 10);
            npc.Equipment.Add(0, new Item
            {
                LowId = 1,
                HighId = 1,
                Quality = 1,
                Definition = new ItemTemplate
                {
                    Id = 1,
                    Name = "NPC Lootgiver 10",
                    SpellList = new Dictionary<EventType, List<ItemSpell>>
                    {
                        [EventType.OnWear] =
                        [
                            new ItemSpell
                            {
                                FunctionType = (int)FunctionType.Modify,
                                Arguments = [(int)CharacterStat.Strength, 5]
                            }
                        ]
                    }
                }
            });

            npc.RebaseStats();

            Assert.AreEqual(15, npc.Stats.GetOrZero(CharacterStat.Strength));
        }

        static NpcCharacter CreateNpc(StubCatalog catalog, int instance, List<List<int>> equipment)
        {
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = instance }, new CatalogItemBuilder(catalog))
            {
                MobTemplate = new MobTemplate
                {
                    Name = "Armed",
                    Equipment = equipment
                }
            };
            npc.Stats.Set(CharacterStat.Level, 1);
            return npc;
        }

        static int HandMesh(NpcCharacter npc, int position)
        {
            foreach (Mesh mesh in npc.Meshes)
            {
                if (mesh.Position == position)
                    return (int)mesh.Id;
            }

            return 0;
        }

        static bool TryFindEquipMonsterWeaponHash(ItemTemplate template, out string hash)
        {
            hash = string.Empty;
            foreach (KeyValuePair<EventType, List<ItemSpell>> pair in template.SpellList)
            {
                List<ItemSpell>? spells = pair.Value;
                if (spells == null)
                    continue;

                for (int i = 0; i < spells.Count; i++)
                {
                    ItemSpell spell = spells[i];
                    if ((FunctionType)spell.FunctionType != FunctionType.EquipMonsterWeapon)
                        continue;

                    hash = "found-no-arg";
                    foreach (object argument in spell.Arguments)
                    {
                        if (argument is string text && text.Length > 0)
                        {
                            hash = text;
                            return true;
                        }

                        if (argument is int packed)
                        {
                            char a = (char)((packed >> 24) & 0xFF);
                            char b = (char)((packed >> 16) & 0xFF);
                            char c = (char)((packed >> 8) & 0xFF);
                            char d = (char)(packed & 0xFF);
                            hash = packed.ToString(CultureInfo.InvariantCulture) + "=" + string.Concat(a, b, c, d);
                            return true;
                        }

                        hash = argument?.GetType().FullName + "=" + argument;
                    }

                    return false;
                }
            }

            return false;
        }

        static int GetStat(ItemTemplate template, CharacterStat stat)
            => template.Stats.TryGetValue(stat, out int value) ? value : 0;

        static string DumpSpells(ItemTemplate template)
        {
            var sb = new StringBuilder();
            sb.Append("name=").Append(template.Name);
            sb.Append(" events=").Append(template.SpellList.Count);
            foreach (KeyValuePair<EventType, List<ItemSpell>> pair in template.SpellList)
            {
                sb.Append(" [").Append(pair.Key).Append(':');
                List<ItemSpell>? spells = pair.Value;
                if (spells == null)
                {
                    sb.Append("null]");
                    continue;
                }

                for (int i = 0; i < spells.Count; i++)
                {
                    ItemSpell spell = spells[i];
                    sb.Append(spell.FunctionType);
                    if (spell.Arguments.Count > 0)
                    {
                        sb.Append('(');
                        for (int a = 0; a < spell.Arguments.Count; a++)
                        {
                            if (a > 0)
                                sb.Append(',');
                            object argument = spell.Arguments[a];
                            sb.Append(argument?.GetType().Name).Append('=').Append(argument);
                        }

                        sb.Append(')');
                    }

                    sb.Append(';');
                }

                sb.Append(']');
            }

            return sb.ToString();
        }

        static string FindGameDataRoot()
        {
            string? dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                string candidate = Path.Combine(dir, "GameData");
                if (File.Exists(Path.Combine(candidate, "items.dat")))
                    return candidate;
                dir = Path.GetDirectoryName(dir);
            }

            throw new DirectoryNotFoundException("GameData/items.dat not found from " + AppContext.BaseDirectory);
        }

        sealed class EmptyNames : IItemNameRepository
        {
            public bool TryGetName(int aoid, out string name)
            {
                name = string.Empty;
                return false;
            }

            public IReadOnlyDictionary<int, string> GetAllNames()
                => new Dictionary<int, string>();
        }

        sealed class CatalogItemBuilder : IItemBuilder
        {
            readonly IItemTemplateCatalog _catalog;

            public CatalogItemBuilder(IItemTemplateCatalog catalog)
            {
                _catalog = catalog;
            }

            public Item Create(
                int lowId,
                int highId,
                int quality,
                ItemSource source,
                int stackCount = 1,
                int instanceId = 0,
                Identity? identity = null,
                byte[]? statsBlob = null)
            {
                if (!_catalog.TryGet(lowId, out ItemTemplate template))
                    throw new KeyNotFoundException(lowId.ToString(CultureInfo.InvariantCulture));

                return new Item
                {
                    LowId = lowId,
                    HighId = highId,
                    Quality = quality,
                    Source = source,
                    InstanceId = instanceId,
                    Definition = template
                };
            }

            public Item CreateWithNewInstance(
                int lowId,
                int highId,
                int quality,
                ItemSource source,
                int stackCount = 1)
                => Create(lowId, highId, quality, source, stackCount);

            public ItemTemplate CreateTemplate(int lowId, int highId, int quality)
                => _catalog.Require(lowId);

            public bool TryFromInstanceRecord(ItemInstanceRecord row, out Item item)
                => throw new NotSupportedException();
        }
    }

    static class StubCatalogEquipmentExtensions
    {
        public static StubCatalog AddNpcEquipper(this StubCatalog catalog, int id, string hash)
        {
            catalog.Add(id, 1);
            ItemTemplate template = catalog.Require(id);
            template.Stats[CharacterStat.ItemClass] = (int)ItemClass.Npc;
            template.SpellList[EventType.OnWear] =
            [
                new ItemSpell
                {
                    FunctionType = (int)FunctionType.EquipMonsterWeapon,
                    Arguments = [hash]
                }
            ];
            return catalog;
        }

        public static StubCatalog AddWeapon(this StubCatalog catalog, int id, int quality)
        {
            catalog.Add(id, quality);
            catalog.Require(id).Stats[CharacterStat.ItemClass] = (int)ItemClass.Weapon;
            return catalog;
        }

        public static StubCatalog AddMeleeWeapon(this StubCatalog catalog, int id, int mesh)
        {
            catalog.AddWeapon(id, 1);
            ItemTemplate template = catalog.Require(id);
            template.Stats[CharacterStat.InitiativeType] = (int)CharacterStat.MeleeInit;
            template.Stats[CharacterStat.WeaponMesh] = mesh;
            return catalog;
        }
    }
}
