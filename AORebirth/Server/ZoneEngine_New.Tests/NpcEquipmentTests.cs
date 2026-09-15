namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

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
    }
}
