namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.IO;
    using System.Runtime.CompilerServices;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Network;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class CharacterSpawnHydrationTests
    {
        [TestMethod]
        public void Known_incomplete_23_stat_shape_is_rejected_before_player_publication()
        {
            CharacterHydrationValidationResult validation = CharacterHydrationValidator.Validate(IncompleteReportedHydration());
            Assert.IsFalse(validation.IsValid);
            Assert.IsTrue(validation.Errors.Contains("missing-stat:0:Flags"));
            Assert.IsTrue(validation.Errors.Contains("health-exceeds-max"));
            Assert.IsTrue(((CharacterFlags)(int)CharacterStat.Unset).HasFlag(CharacterFlags.Tower));
            Assert.AreEqual(-981, 19 - 1000);
            // Exact saved staging rows: pre-hydration-553981c8-stats.sql (23 rows).
            // Raw historical builder output is evidence; it must never cross publication.
            var malformed = PlayerFrom(IncompleteReportedHydration());
            var scfu = malformed.BuildSpawnMessage();
            Assert.AreEqual((CharacterFlags)(int)CharacterStat.Unset, scfu.CharacterFlags);
            Assert.AreEqual(-981, scfu.HealthDamage);
            Assert.ThrowsException<InvalidOperationException>(() =>
                PlayerSpawnPayloadValidator.RequireValidMessages(scfu, malformed.BuildFullCharacterMessage()));
        }

        [TestMethod]
        public void Complete_minimal_retail_player_aggregate_is_accepted_and_semantically_valid_on_wire()
        {
            CharacterHydrationResult hydration = ValidHydration();
            Assert.IsTrue(CharacterHydrationValidator.Validate(hydration).IsValid);
            Player player = PlayerFrom(hydration);
            PlayerSpawnPayloadValidator.RequireValid(player);
            SimpleCharFullUpdateMessage scfu = player.BuildSpawnMessage();
            FullCharacterMessage full = player.BuildFullCharacterMessage();
            PlayerSpawnPayloadValidator.RequireValidMessages(scfu, full);

            Assert.AreEqual(0, (int)scfu.CharacterFlags & (int)CharacterFlags.Tower);
            Assert.AreNotEqual((int)CharacterStat.Unset, (int)scfu.CharacterFlags);
            Assert.AreEqual(31, scfu.Health);
            Assert.AreEqual(6, scfu.HealthDamage);
            Assert.AreEqual(40683u, scfu.HeadMesh);
            Assert.AreEqual(31, scfu.VisualFlags);
            Assert.AreEqual(100, scfu.MonsterScale);
            Assert.AreEqual(171, scfu.Expansions);
            Assert.IsInstanceOfType<SimplePcInfo>(scfu.CharacterInfo);
            var pc = (SimplePcInfo)scfu.CharacterInfo;
            CollectionAssert.AreEqual(new short[] { 6, 6, 6, 6, 6, 6 },
                new[] { pc.StrengthBase, pc.AgilityBase, pc.StaminaBase, pc.IntelligenceBase, pc.SenseBase, pc.PsychicBase });
            Assert.AreEqual(25u, RequiredFullStat(full, CharacterStat.Health));
            Assert.AreEqual(31u, RequiredFullStat(full, CharacterStat.MaxHealth));
            Assert.AreEqual(100u, RequiredFullStat(full, CharacterStat.Scale));
            Assert.IsFalse(full.Stats1.Concat(full.Stats2).Any(stat => stat.Value2 == (uint)(int)CharacterStat.Unset));
        }

        [TestMethod]
        public void Missing_required_stats_and_unset_sentinels_fail_closed()
        {
            foreach (CharacterStat required in CharacterHydrationValidator.RequiredSpawnStats)
                Assert.IsFalse(CharacterHydrationValidator.Validate(Without(ValidHydration(), required)).IsValid, "missing " + required);

            CharacterHydrationValidationResult validation = CharacterHydrationValidator.Validate(
                With(ValidHydration(), CharacterStat.Flags, (int)CharacterStat.Unset));
            Assert.IsFalse(validation.IsValid);
            Assert.IsTrue(validation.Errors.Contains("unset-sentinel:0"));
        }

        [TestMethod]
        public void Health_contract_accepts_current_below_or_equal_to_max_and_rejects_invalid_variants()
        {
            Assert.IsTrue(CharacterHydrationValidator.Validate(With(ValidHydration(), CharacterStat.Health, 25)).IsValid);
            Assert.IsTrue(CharacterHydrationValidator.Validate(With(ValidHydration(), CharacterStat.Health, 31)).IsValid);
            Assert.IsFalse(CharacterHydrationValidator.Validate(With(ValidHydration(), CharacterStat.Health, 32)).IsValid);
            Assert.IsFalse(CharacterHydrationValidator.Validate(Without(ValidHydration(), CharacterStat.Health)).IsValid);
            Assert.IsFalse(CharacterHydrationValidator.Validate(Without(ValidHydration(), CharacterStat.MaxHealth)).IsValid);
        }

        [TestMethod]
        public void Complete_starter_vitals_remain_valid_after_player_rebase()
        {
            Player player = PlayerFrom(ValidHydration());

            player.Rebase();

            Assert.AreEqual(31, player.Stats.GetOrZero(CharacterStat.MaxHealth));
            Assert.AreEqual(25, player.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(29, player.Stats.GetOrZero(CharacterStat.MaxNanoEnergy));
            Assert.AreEqual(20, player.Stats.GetOrZero(CharacterStat.CurrentNano));
            PlayerSpawnPayloadValidator.RequireValid(player);
        }

        [TestMethod]
        public void Appearance_identity_primary_ability_and_tower_variants_are_rejected()
        {
            CharacterStat[] missing =
            [
                CharacterStat.HeadMesh, CharacterStat.VisualFlags, CharacterStat.Scale, CharacterStat.Race, CharacterStat.Breed,
                CharacterStat.Sex, CharacterStat.Strength, CharacterStat.Agility, CharacterStat.Stamina,
                CharacterStat.Intelligence, CharacterStat.Sense, CharacterStat.Psychic,
            ];
            foreach (CharacterStat stat in missing)
                Assert.IsFalse(CharacterHydrationValidator.Validate(Without(ValidHydration(), stat)).IsValid, stat.ToString());

            CharacterHydrationResult source = ValidHydration();
            CharacterHydrationResult tower = With(source, CharacterStat.Flags,
                Stat(source, CharacterStat.Flags) | (int)CharacterFlags.Tower);
            Assert.IsTrue(CharacterHydrationValidator.Validate(tower).Errors.Contains("player-flags-tower"));
        }

        [TestMethod]
        public void Every_missing_required_field_stops_actual_spawn_before_any_owner_or_wire_operation()
        {
            // No dependencies are initialized: reaching DAO/world/inventory means the guard failed.
            var service = (SpawnService)RuntimeHelpers.GetUninitializedObject(typeof(SpawnService));
            foreach (CharacterStat stat in CharacterHydrationValidator.RequiredSpawnStats)
            {
                var session = new RejectingSession();
                var error = Assert.ThrowsException<InvalidOperationException>(() =>
                    service.SpawnPlayer(session, Without(ValidHydration(), stat)));
                StringAssert.Contains(error.Message, "Character spawn aggregate rejected:");
                Assert.IsNull(session.Player);
                Assert.AreEqual(SessionState.Loading, session.State);
            }
        }

        [TestMethod]
        public void Sentinel_in_every_persisted_stat_fails_the_actual_publication_boundary()
        {
            var service = (SpawnService)RuntimeHelpers.GetUninitializedObject(typeof(SpawnService));
            foreach (CharacterStat stat in CharacterHydrationValidator.RequiredSpawnStats)
                Assert.ThrowsException<InvalidOperationException>(() => service.SpawnPlayer(new RejectingSession(),
                    With(ValidHydration(), stat, (int)CharacterStat.Unset)));
        }

        [TestMethod]
        public void Wire_validator_rejects_optional_sentinels_and_conflicting_duplicate_stats()
        {
            Player player = PlayerFrom(ValidHydration());
            var full = player.BuildFullCharacterMessage();
            full.Stats1 = full.Stats1.Concat(new[] { new GameTuple<int, uint>
                { Value1 = 10001, Value2 = (uint)(int)CharacterStat.Unset } }).ToArray();
            Assert.ThrowsException<InvalidOperationException>(() =>
                PlayerSpawnPayloadValidator.RequireValidMessages(player.BuildSpawnMessage(), full));
            full = player.BuildFullCharacterMessage();
            full.Stats2 = full.Stats2.Concat(new[] { new GameTuple<int, uint>
                { Value1 = (int)CharacterStat.Health, Value2 = 999 } }).ToArray();
            Assert.ThrowsException<InvalidOperationException>(() =>
                PlayerSpawnPayloadValidator.RequireValidMessages(player.BuildSpawnMessage(), full));
        }

        [TestMethod]
        public void Wire_validator_rejects_sentinel_mesh_and_tower_conditional_payload()
        {
            Player player = PlayerFrom(ValidHydration());
            var spawn = player.BuildSpawnMessage();
            spawn.Meshes[0].Id = (uint)(int)CharacterStat.Unset;
            Assert.ThrowsException<InvalidOperationException>(() =>
                PlayerSpawnPayloadValidator.RequireValidMessages(spawn, player.BuildFullCharacterMessage()));
            spawn = player.BuildSpawnMessage();
            spawn.CharacterFlags |= CharacterFlags.Tower;
            Assert.ThrowsException<InvalidOperationException>(() =>
                PlayerSpawnPayloadValidator.RequireValidMessages(spawn, player.BuildFullCharacterMessage()));
        }

        [TestMethod]
        public void Visible_names_use_the_loaded_character_names_in_the_conditional_pc_block()
        {
            Player player = PlayerFrom(With(ValidHydration(), CharacterStat.Flags,
                0x00081241 | (int)CharacterFlags.HasVisibleName));
            player.FirstName = "PersistedFirst";
            player.LastName = "PersistedLast";
            var spawn = player.BuildSpawnMessage();
            var pc = (SimplePcInfo)spawn.CharacterInfo;
            Assert.AreEqual("PersistedFirst", pc.FirstName);
            Assert.AreEqual("PersistedLast", pc.LastName);
            PlayerSpawnPayloadValidator.RequireValidMessages(spawn, player.BuildFullCharacterMessage());
        }

        [TestMethod]
        public void Large_health_uses_existing_scfu_presentation_without_changing_exact_fullcharacter_values()
        {
            Player player = PlayerFrom(With(With(ValidHydration(), CharacterStat.MaxHealth, 100000), CharacterStat.Health, 50000));
            var spawn = player.BuildSpawnMessage();
            Assert.AreEqual(65535, spawn.Health);
            Assert.AreEqual(32768, spawn.HealthDamage);
            PlayerSpawnPayloadValidator.RequireValidMessages(spawn, player.BuildFullCharacterMessage());
        }

        [TestMethod]
        public void Optional_fullcharacter_stat_absence_is_distinct_from_stored_zero_and_sentinel()
        {
            Player player = PlayerFrom(ValidHydration());
            Assert.IsFalse(player.BuildFullCharacterMessage().Stats1.Concat(player.BuildFullCharacterMessage().Stats2)
                .Any(row => row.Value1 == (int)CharacterStat.Cash));
            player.Stats.Set(CharacterStat.Cash, 0);
            Assert.AreEqual(0u, RequiredFullStat(player.BuildFullCharacterMessage(), CharacterStat.Cash));
            player.Stats.Set(CharacterStat.Cash, (int)CharacterStat.Unset);
            Assert.ThrowsException<InvalidOperationException>(() => player.BuildFullCharacterMessage());
        }

        [TestMethod]
        public void Fullcharacter_and_scfu_codec_roundtrip_preserves_semantic_contract()
        {
            Player player = PlayerFrom(ValidHydration());
            var serializer = new MessageSerializer();
            T RoundTrip<T>(T body) where T : MessageBody
            {
                using var stream = new MemoryStream();
                serializer.Serialize(stream, new Message { Header = new Header
                    { MessageId = 0xdfdf, PacketType = body.PacketType, Unknown = 1,
                      Sender = 4582, Receiver = player.Identity.Instance }, Body = body });
                stream.Position = 0;
                return (T)serializer.Deserialize(stream).Body;
            }
            var spawn = RoundTrip(player.BuildSpawnMessage());
            var full = RoundTrip(player.BuildFullCharacterMessage());
            Assert.IsTrue(spawn.TailFullyDecoded);
            PlayerSpawnPayloadValidator.RequireValidMessages(spawn, full);
        }

        private sealed class RejectingSession : IZoneSession
        {
            public SessionState State { get; set; } = SessionState.Loading;
            public Player? Player => null;
            public void BindPlayer(Player player) => throw new AssertFailedException("Player published before validation");
            public void UnbindPlayer() => throw new AssertFailedException("Unexpected session mutation");
            public void TransferToPlayfield(Playfield destination, Vector3 landing) => throw new AssertFailedException();
            public void Send(byte[] packet) => throw new AssertFailedException("Wire before validation");
            public void Send(Message message) => throw new AssertFailedException("Wire before validation");
            public void Send(MessageBody body) => throw new AssertFailedException("Wire before validation");
            public void Send(MessageBody body, int sender, int receiver) => throw new AssertFailedException("Wire before validation");
            public void SendInitiateCompression() => throw new AssertFailedException();
            public void Close() => State = SessionState.Closed;
        }

        private static CharacterHydrationResult ValidHydration()
        {
            var values = new Dictionary<CharacterStat, int>
            {
                [CharacterStat.Flags] = 0x00081241, [CharacterStat.Breed] = 1, [CharacterStat.Sex] = 2,
                [CharacterStat.Profession] = 1, [CharacterStat.Fatness] = 0, [CharacterStat.Race] = 1,
                [CharacterStat.HeadMesh] = 40683, [CharacterStat.VisualFlags] = 31,
                [CharacterStat.Scale] = 100,
                [CharacterStat.Level] = 1, [CharacterStat.TitleLevel] = 1, [CharacterStat.Side] = 0,
                [CharacterStat.Expansion] = 171,
                [CharacterStat.Strength] = 6, [CharacterStat.Agility] = 6, [CharacterStat.Stamina] = 6,
                [CharacterStat.Intelligence] = 6, [CharacterStat.Sense] = 6, [CharacterStat.Psychic] = 6,
                [CharacterStat.BodyDevelopment] = 5, [CharacterStat.NanoPool] = 5,
                [CharacterStat.Health] = 25, [CharacterStat.MaxHealth] = 31,
                [CharacterStat.CurrentNano] = 20, [CharacterStat.MaxNanoEnergy] = 29,
                [CharacterStat.RunSpeed] = 100,
            };
            return Result(values);
        }

        private static CharacterHydrationResult IncompleteReportedHydration()
        {
            var values = new Dictionary<CharacterStat, int>
            {
                [CharacterStat.MaxHealth] = 19, [CharacterStat.Breed] = 1, [CharacterStat.Team] = 0,
                [CharacterStat.Mesh] = 5907, [CharacterStat.Health] = 1000, [CharacterStat.CATMesh] = 111,
                [CharacterStat.Level] = 1, [CharacterStat.LastXP] = 0, [CharacterStat.Sex] = 2,
                [CharacterStat.Profession] = 1, [CharacterStat.Cash] = 1234, [CharacterStat.RunSpeed] = 100,
                [(CharacterStat)180] = 0, [CharacterStat.MaxNCU] = 100, [CharacterStat.TeamSide] = 0,
                [CharacterStat.CurrentNano] = 1000, [CharacterStat.MaxNanoEnergy] = 17,
                [CharacterStat.NextXP] = 1450, [(CharacterStat)359] = 0, [CharacterStat.DisplayCATMesh] = 222,
                [CharacterStat.SocialStatus] = 4, [CharacterStat.MapsC] = 0, [(CharacterStat)587] = 0,
            };
            return Result(values);
        }

        private static CharacterHydrationResult Result(Dictionary<CharacterStat, int> values)
            => new()
            {
                Character = new CharacterRecord
                {
                    Id = 9950, Name = "StagingFixture", Playfield = 4582,
                    X = 100, Y = 0, Z = 100, HeadingW = 1,
                },
                Stats = values.Select(pair => new StatRecord { StatId = (int)pair.Key, StatValue = pair.Value }).ToArray(),
            };

        private static CharacterHydrationResult Without(CharacterHydrationResult source, CharacterStat stat)
            => new()
            {
                Character = source.Character,
                Stats = source.Stats.Where(row => row.StatId != (int)stat).ToArray(),
                Items = source.Items,
                UploadedNanoIds = source.UploadedNanoIds,
            };

        private static CharacterHydrationResult With(CharacterHydrationResult source, CharacterStat stat, int value)
            => new()
            {
                Character = source.Character,
                Stats = source.Stats.Select(row => row.StatId == (int)stat
                    ? new StatRecord { StatId = row.StatId, StatValue = value }
                    : row).ToArray(),
                Items = source.Items,
                UploadedNanoIds = source.UploadedNanoIds,
            };

        private static int Stat(CharacterHydrationResult source, CharacterStat stat)
            => source.Stats.Single(row => row.StatId == (int)stat).StatValue;

        private static Player PlayerFrom(CharacterHydrationResult hydration)
        {
            Player player = TestWorld.CreatePlayer(hydration.Character.Id);
            player.Name = hydration.Character.Name;
            player.Position = new Vector3(hydration.Character.X, hydration.Character.Y, hydration.Character.Z);
            foreach (StatRecord stat in hydration.Stats)
                player.Stats.Set((CharacterStat)stat.StatId, stat.StatValue, StatDetail.Base);
            player.Playfield = BlankPlayfield(hydration.Character.Playfield);
            return player;
        }

        private static Playfield BlankPlayfield(int id)
        {
            var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(playfield, new Identity { Type = IdentityType.Playfield2, Instance = id });
            return playfield;
        }

        private static uint RequiredFullStat(FullCharacterMessage message, CharacterStat stat)
            => message.Stats1.Concat(message.Stats2).Single(row => row.Value1 == (int)stat).Value2;
    }
}
