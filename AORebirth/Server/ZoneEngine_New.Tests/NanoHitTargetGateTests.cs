namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    [TestClass]
    public sealed class NanoHitTargetGateTests
    {
        [TestMethod]
        public void OnUseAppliesTargetDirectedHitOnPlayer()
        {
            var player = TestWorld.CreatePlayer(28608);
            player.Stats.Set(CharacterStat.Health, 100, StatDetail.Base);
            player.Stats.Set(CharacterStat.MaxHealth, 100, StatDetail.Base);

            var template = new ItemTemplate
            {
                Id = 28608,
                SpellList = new Dictionary<EventType, List<ItemSpell>>
                {
                    [EventType.OnUse] =
                    [
                        new ItemSpell
                        {
                            FunctionType = (int)FunctionType.Hit,
                            Target = (int)ItemTarget.Target,
                            TickCount = 1,
                            Arguments = [(int)CharacterStat.Health, -25, -25, 0]
                        }
                    ]
                }
            };

            Assert.IsTrue(
                template.ExecuteOnUseSpells(player, new StubInventoryRepository(), new StubItemBuilder()));
            Assert.AreEqual(75, player.Stats.GetOrZero(CharacterStat.Health));
        }
    }
}
