namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    [TestClass]
    public sealed class UploadNanoTests
    {
        const int NanoId = 25984;

        [TestMethod]
        public void UsingACrystalAddsTheNanoAndTellsTheClient()
        {
            Player player = CreatePlayer(out RecordingZoneSession session);

            Assert.IsTrue(Execute(player));

            CollectionAssert.Contains(player.UploadedNanoIds, NanoId);
            Assert.IsTrue(player.HasDirtyUploadedNanos);

            CharacterActionMessage action = SingleAction(session);
            Assert.AreEqual(CharacterActionType.UploadNano, action.Action);
            Assert.AreEqual((int)IdentityType.NanoProgram, action.Parameter1);
            Assert.AreEqual(NanoId, action.Parameter2);
        }

        [TestMethod]
        public void AKnownNanoIsNotUploadedAgainSoTheCrystalIsNotSpent()
        {
            Player player = CreatePlayer(out RecordingZoneSession session);
            player.TryAddUploadedNano(NanoId);

            Assert.IsFalse(Execute(player));

            Assert.AreEqual(1, player.UploadedNanoIds.Count);
            Assert.IsFalse(player.HasDirtyUploadedNanos);
            Assert.AreEqual(0, CountOf<CharacterActionMessage>(session));
            Assert.AreEqual(1, CountOf<ChatTextMessage>(session));
        }

        [TestMethod]
        public void AnItemWithoutAUsableFunctionDoesNotCountAsUsed()
        {
            Player player = CreatePlayer(out _);
            ItemTemplate template = CrystalTemplate(nanoId: 0);

            Assert.IsFalse(
                template.ExecuteOnUseSpells(player, new StubInventoryRepository(), new StubItemBuilder()));
        }

        static bool Execute(Player player)
            => CrystalTemplate(NanoId)
                .ExecuteOnUseSpells(player, new StubInventoryRepository(), new StubItemBuilder());

        static ItemTemplate CrystalTemplate(int nanoId)
            => new()
            {
                Id = 26015,
                Name = "Nano Crystal",
                Quality = 1,
                SpellList = new Dictionary<EventType, List<ItemSpell>>
                {
                    [EventType.OnUse] =
                    [
                        new ItemSpell
                        {
                            FunctionType = (int)FunctionType.UploadNano,
                            Target = 2,
                            Arguments = new List<object> { nanoId },
                            Requirements = new List<ItemRequirement>()
                        }
                    ]
                }
            };

        static Player CreatePlayer(out RecordingZoneSession session)
        {
            Player player = TestWorld.CreatePlayer(1);
            session = new RecordingZoneSession();
            session.BindPlayer(player);
            player.Session = session;
            return player;
        }

        static int CountOf<T>(RecordingZoneSession session)
            where T : MessageBody
        {
            int count = 0;
            foreach (MessageBody body in session.Sent)
            {
                if (body is T)
                    count++;
            }

            return count;
        }

        static CharacterActionMessage SingleAction(RecordingZoneSession session)
        {
            CharacterActionMessage? found = null;
            foreach (MessageBody body in session.Sent)
            {
                if (body is not CharacterActionMessage action)
                    continue;

                Assert.IsNull(found);
                found = action;
            }

            Assert.IsNotNull(found);
            return found!;
        }
    }
}
