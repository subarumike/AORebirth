namespace ZoneEngine_New.Tests
{
    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Pets;
    using ZoneEngine_New.Core.Teams;

    public sealed partial class CastNanoTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BioRejuvenationHealsEligibleAlliesAndPetsButNotMobs(bool raid)
        {
            const int childId = 215579;
            NanoSpell heal = TestNanos.Create(childId, can: 0, durationCentiseconds: 0, modifiers:
            [
                new ItemSpell
                {
                    FunctionType = (int)FunctionType.Hit,
                    Target = (int)ItemTarget.Target,
                    Arguments = [(int)CharacterStat.Health, 250, 250, 0]
                }
            ]);
            heal.Actions.Add(new ItemAction
            {
                ActionType = (int)ActionType.ToUse,
                Requirements =
                [
                    new ItemRequirement { StatNumber = (int)CharacterStat.NPCFamily, Operator = (int)Operator.EqualTo, Value = 0 }
                ]
            });

            var items = new StubItemBuilder().Add(heal);
            using var world = new FanoutWorld(items);
            Player caster = world.Player(31, 0, 0, 0);
            Player teammate = world.Player(32, 1, 0, 0);
            Player other = world.Player(33, 2, 0, 0);
            Player distant = world.Player(34, 26, 0, 0);
            NpcCharacter pet = world.Npc(35, 3, 0, 0, attackable: true);
            NpcCharacter ineligiblePet = world.Npc(36, 4, 0, 0, attackable: true);
            NpcCharacter mob = world.Npc(37, 5, 0, 0, attackable: true);
            world.Join(caster, teammate);
            if (raid)
            {
                world.Join(caster, other);
                TeamService teams = world.Playfield.GetRequiredService<TeamService>();
                Assert.IsTrue(teams.ConvertToRaid(caster));
                Assert.IsTrue(teams.MoveRaidMember(caster, other.Identity.Instance, 1));
            }

            pet.BindPet(new PetController(teammate, 1, "heal-pet", 60, null));
            ineligiblePet.BindPet(new PetController(caster, 1, "ineligible-heal-pet", 60, null));
            foreach (Character character in new Character[] { caster, teammate, other, distant, pet, ineligiblePet, mob })
            {
                character.Stats.Set(CharacterStat.NPCFamily, 0);
                character.Stats.Set(CharacterStat.MaxHealth, 1000);
                character.Stats.Set(CharacterStat.Health, 100);
            }

            ineligiblePet.Stats.Set(CharacterStat.NPCFamily, 97);
            Assert.IsTrue(UseAreaOn(caster, caster, items, childId, radius: 25));

            foreach (Character character in new Character[] { caster, teammate, other, pet })
                Assert.AreEqual(350, character.Stats.GetOrZero(CharacterStat.Health));
            foreach (Character character in new Character[] { mob, ineligiblePet, distant })
                Assert.AreEqual(100, character.Stats.GetOrZero(CharacterStat.Health));
        }
    }
}
