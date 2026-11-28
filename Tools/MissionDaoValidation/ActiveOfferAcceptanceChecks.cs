namespace AORebirth.Tools.MissionDaoValidation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using AORebirth.Database.Domain.Missions;
    using AORebirth.Database.Domain.Quests;
    using AORebirth.Interfaces.Persistence.Missions;
    using MySqlConnector;
    using SmokeLounge.AOtomation.Messaging.GameData;

    internal static partial class Program
    {
        private const int AcceptanceOwner = 101;
        private const int OfferType = (int)IdentityType.Mission;
        private const int FirstMeasuredOffer = 610000;
        private const long AcceptAt = 1500;

        private static void ValidateActiveOfferAcceptance(MySqlMissionDao dao, string connectionString)
        {
            const int historyBatches = 500;
            for (int batch = 0; batch < historyBatches; batch++)
                PublishAcceptanceBatch(dao, batch);

            var quests = new MySqlCharacterQuestDao(() => Open(connectionString));
            MeasureAcceptance(dao, quests, connectionString, historyBatches);

            int current = PublishAcceptanceBatch(dao, historyBatches + 12);
            int replaced = FirstMeasuredOffer;
            var selected = dao.ReadCurrentOffer(AcceptanceOwner, OfferType, current, AcceptAt);
            Require(selected != null && selected.OfferInstance == current && selected.IssuingTerminalType == 0xDAC1
                && selected.EntranceType == (int)IdentityType.MissionEntrance
                && unchecked((uint)selected.EntranceInstance) == 0xC00001F9u
                && BitConverter.SingleToUInt32Bits(selected.DestinationX) == BitConverter.SingleToUInt32Bits(123.125f),
                "indexed-lookup-retains-issuing-terminal-and-exact-offer");
            Require(dao.ReadCurrentOffer(AcceptanceOwner, OfferType, selected.OfferInstance, selected.ExpiresAtUtcTicks) == null,
                "expired-at-boundary");
            Require(dao.ReadCurrentOffer(102, OfferType, current, AcceptAt) == null,
                "foreign-owned-offer-hidden");
            Require(dao.ReadCurrentOffer(AcceptanceOwner, (int)IdentityType.None, current, AcceptAt) == null
                && dao.ReadCurrentOffer(AcceptanceOwner, OfferType, 0, AcceptAt) == null,
                "malformed-identity-refused");
            Require(dao.ReadCurrentOffer(AcceptanceOwner, OfferType, replaced, AcceptAt) == null,
                "replaced-offer-not-current");
            Require(dao.ReadOffers(AcceptanceOwner).Single(offer => offer.OfferInstance == replaced).State == GeneratedMissionState.Replaced,
                "historical-offer-remains-in-diagnostics");

            CorruptOfferHash(connectionString, replaced);
            Expect<ArgumentException>(() => dao.ReadOffers(AcceptanceOwner), "baseline-read-rejects-unrelated-bad-historical-row");
            CorruptOfferHash(connectionString, current + 1);
            Expect<ArgumentException>(() => dao.ReadCurrentOffer(AcceptanceOwner, OfferType, current + 1, AcceptAt),
                "exact-malformed-offer-rejected-before-claim");
            Require(dao.ReadCurrentOffer(AcceptanceOwner, OfferType, current, AcceptAt) != null,
                "valid-offer-readable-during-unrelated-history-corruption");

            long lastCash = Scalar(connectionString, "SELECT StatValue FROM stats WHERE Type=50000 AND Instance=101 AND StatId=61");
            long lastFeeRows = Scalar(connectionString, "SELECT COUNT(*) FROM missionrewardledger WHERE CharacterId=101 AND RewardKey='roll-fee'");
            Task<bool> one = Task.Run(() => ClaimAcceptance(quests, current, "offer-race-a", 961001, 66, 67));
            Task<bool> two = Task.Run(() => ClaimAcceptance(quests, current, "offer-race-b", 961101, 66, 67));
            Task.WaitAll(one, two);
            Require(one.Result != two.Result, "only-one-atomic-accept-wins");
            RequireAcceptance(quests, one.Result ? "offer-race-a" : "offer-race-b", one.Result ? 961001 : 961101, committed: true);
            RequireAcceptance(quests, one.Result ? "offer-race-b" : "offer-race-a", one.Result ? 961101 : 961001, committed: false);
            Require(dao.ReadCurrentOffer(AcceptanceOwner, OfferType, current, AcceptAt) == null
                && !ClaimAcceptance(quests, current, "offer-retry", 961201, 68, 69),
                "claimed-offer-cannot-repeat");

            // The exact inventoriable tool slot from the winning accept is already occupied. Force the
            // final insert to fail after the offer, quest, and key writes have already run.
            Expect<MySqlException>(() => ClaimAcceptance(quests, current + 2, "offer-tool-fail", 961301, 68, 67),
                "tool-slot-collision-interrupts-actual-acceptance");
            RequireAcceptance(quests, "offer-tool-fail", 961301, committed: false);
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId IN (961301,961302)") == 0
                && dao.ReadCurrentOffer(AcceptanceOwner, OfferType, current + 2, AcceptAt) != null,
                "interrupted-tool-grant-rolls-back-offer-key-and-tool");
            Require(ClaimAcceptance(quests, current + 2, "offer-tool-retry", 961301, 68, 69),
                "interrupted-accept-can-retry-exact-offer");

            foreach (bool afterCommit in new[] { false, true })
            {
                int offer = current + (afterCommit ? 4 : 3);
                int key = afterCommit ? 961501 : 961401;
                int slot = afterCommit ? 72 : 70;
                string quest = afterCommit ? "offer-commit-after" : "offer-commit-before";
                var faulted = new MySqlCharacterQuestDao(() =>
                    new FaultConnection(connectionString) { FailurePoint = afterCommit ? "commit-after" : "commit-before" });
                Expect<MissionCommitOutcomeUnknownException>(() => ClaimAcceptance(faulted, offer, quest, key, slot, slot + 1),
                    "acceptance-unknown-commit-acknowledgement");
                RequireAcceptance(quests, quest, key, committed: afterCommit);
                Require((dao.ReadCurrentOffer(AcceptanceOwner, OfferType, offer, AcceptAt) == null) == afterCommit,
                    "authoritative-offer-read-reconciles-commit-outcome");
                Require(ClaimAcceptance(quests, offer, quest, key, slot, slot + 1) != afterCommit,
                    "acceptance-retry-does-not-duplicate-keys-or-quests");
                RequireAcceptance(quests, quest, key, committed: true);
                Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId IN (" + key + "," + (key + 1) + ")") == 2,
                    "exact-key-and-tool-committed-together");
            }

            Require(Scalar(connectionString, "SELECT StatValue FROM stats WHERE Type=50000 AND Instance=101 AND StatId=61") == lastCash
                && Scalar(connectionString, "SELECT COUNT(*) FROM missionrewardledger WHERE CharacterId=101 AND RewardKey='roll-fee'") == lastFeeRows,
                "acceptance-does-not-charge-or-replay-roll-fees");
        }

        private static void MeasureAcceptance(MySqlMissionDao dao, MySqlCharacterQuestDao quests, string connectionString, int historyBatches)
        {
            const int warmup = 2;
            const int repetitions = 8;
            var baseline = new List<AcceptanceMeasurement>();
            var current = new List<AcceptanceMeasurement>();
            for (int i = 0; i < warmup + repetitions; i++)
            {
                int first = PublishAcceptanceBatch(dao, historyBatches + i);
                // Each pair sees the same row count. Alternate execution order to reduce warm-cache bias.
                foreach (bool indexed in i % 2 == 0 ? new[] { false, true } : new[] { true, false })
                {
                    int offset = 2 * i + (indexed ? 1 : 0);
                    int key = 960000 + 2 * offset;
                    string quest = "offer-perf-" + offset;
                    var measurement = MeasureOneAcceptance(connectionString, first + (indexed ? 1 : 0), quest, key, indexed);
                    if (i >= warmup)
                        (indexed ? current : baseline).Add(measurement);

                    quests.EndQuestKeys(quest, AcceptanceItem(key + 1, 65));
                }
            }
            Require(current.All(sample => sample.Rows == 1 && sample.LookupCommands == 1)
                && baseline.Zip(current, (before, after) => before.Rows >= 2500 && before.LookupCommands == 1
                    && before.ClaimCommands == after.ClaimCommands).All(equal => equal),
                "indexed-acceptance-bounds-rows-without-extra-queries");
            WriteMeasurements("BEFORE", baseline);
            WriteMeasurements("AFTER", current);
        }

        private static AcceptanceMeasurement MeasureOneAcceptance(string connectionString, int offerId, string quest, int key, bool indexed)
        {
            var lookup = new FaultConnection(connectionString);
            var claims = new FaultConnection(connectionString);
            var dao = new MySqlMissionDao(() => lookup);
            var quests = new MySqlCharacterQuestDao(() => claims);
            var timer = Stopwatch.StartNew();
            GeneratedMissionOffer offer;
            int rows;
            if (indexed)
            {
                offer = dao.ReadCurrentOffer(AcceptanceOwner, OfferType, offerId, AcceptAt);
                rows = offer == null ? 0 : 1;
            }
            else
            {
                // Exact lookup performed before this change. ReadOffers itself remains unchanged.
                var offers = dao.ReadOffers(AcceptanceOwner);
                rows = offers.Count;
                offer = offers.FirstOrDefault(value => value.OfferType == OfferType && value.OfferInstance == offerId
                    && value.State == GeneratedMissionState.Offered && value.ExpiresAtUtcTicks > AcceptAt);
            }
            double lookupMs = timer.Elapsed.TotalMilliseconds;
            bool accepted = offer != null && ClaimAcceptance(quests, offer.OfferInstance, quest, key, 64, 65);
            timer.Stop();
            Require(accepted, "measured-lookup-and-claim-complete");
            return new AcceptanceMeasurement(rows, lookup.CommandCount, claims.CommandCount, lookupMs, timer.Elapsed.TotalMilliseconds);
        }

        private static void WriteMeasurements(string label, List<AcceptanceMeasurement> samples)
            => Console.WriteLine(FormattableString.Invariant(
                $"MISSION_ACCEPTANCE_PERF_{label} samples={samples.Count} rows={samples.Min(sample => sample.Rows)}..{samples.Max(sample => sample.Rows)} lookup_commands={samples[0].LookupCommands} claim_commands={samples[0].ClaimCommands} lookup_median_ms={Median(samples.Select(sample => sample.LookupMs)):F3} dao_total_median_ms={Median(samples.Select(sample => sample.TotalMs)):F3}"));

        private static double Median(IEnumerable<double> values)
        {
            double[] ordered = values.OrderBy(value => value).ToArray();
            return (ordered[(ordered.Length - 1) / 2] + ordered[ordered.Length / 2]) / 2;
        }

        private static int PublishAcceptanceBatch(MySqlMissionDao dao, int index)
        {
            string batchId = "acceptance-history-" + index.ToString(CultureInfo.InvariantCulture);
            int first = FirstMeasuredOffer + index * 5;
            var batch = EntranceIdentityBatch(first, batchId, unchecked((int)0xC00001F9u));
            batch.Offers = Enumerable.Range(0, 5).Select(offset =>
            {
                var offer = EntranceIdentityBatch(first + offset, batchId, unchecked((int)0xC00001F9u)).Offers.Single();
                offer.OfferIndex = offset;
                return offer;
            }).ToArray();
            if (dao.PublishOffers(batch).Status != GeneratedMissionResultStatus.Applied)
                throw new InvalidOperationException("Disposable roll history could not be seeded.");
            return first;
        }

        private static void CorruptOfferHash(string connectionString, int offer)
        {
            using (var connection = Open(connectionString))
            using (var command = new MySqlCommand("UPDATE generatedmissionoffers SET FrozenWireSha256=REPEAT('f',64) WHERE OfferType=@type AND OfferInstance=@offer", connection))
            {
                command.Parameters.AddWithValue("type", OfferType);
                command.Parameters.AddWithValue("offer", offer);
                Require(command.ExecuteNonQuery() == 1, "fixture-corrupts-exact-offer");
            }
        }

        private static bool ClaimAcceptance(MySqlCharacterQuestDao quests, int offer, string questId, int key, int keySlot, int toolSlot)
            => quests.TryAcceptOfferQuest(AcceptanceOwner, OfferType, offer,
                new GeneratedQuestRow { QuestId = questId, OwnerId = AcceptanceOwner, DefinitionJson = "{}", AcgBuildingGeneratorJson = "{}",
                    CreatedAtUtcTicks = AcceptAt, UpdatedAtUtcTicks = AcceptAt, ExpiresAtUtcTicks = AcceptAt + TimeSpan.TicksPerHour * 48 },
                new CharacterQuestRow { CharacterId = AcceptanceOwner, QuestId = questId, State = 1, Source = 1,
                    RequiredCount = 1, AssignedAtUtcTicks = AcceptAt, UpdatedAtUtcTicks = AcceptAt },
                AcceptanceItem(key, keySlot), AcceptanceItem(key + 1, toolSlot), AcceptAt);

        private static QuestDungeonKeyItemRow AcceptanceItem(int instance, int slot)
            => new QuestDungeonKeyItemRow { InstanceId = instance, ContainerType = (int)IdentityType.Inventory, ContainerInstance = AcceptanceOwner,
                ContainerPlacement = slot, ItemType = 0xC76D, LowId = 10, HighId = 10, Quality = 1, Source = 0, CreatedAtUtcTicks = AcceptAt };

        private static void RequireAcceptance(MySqlCharacterQuestDao quests, string quest, int key, bool committed)
            => Require((quests.LoadGenerated(quest) != null) == committed
                && quests.LoadForCharacter(AcceptanceOwner).Any(entry => entry.QuestId == quest) == committed
                && quests.LoadKeyQuests(new[] { key }).TryGetValue(key, out string linked) == committed
                && (!committed || linked == quest), "accepted-quest-character-and-key-share-durable-outcome");

        private sealed class AcceptanceMeasurement
        {
            internal AcceptanceMeasurement(int rows, int lookupCommands, int claimCommands, double lookupMs, double totalMs)
            {
                Rows = rows;
                LookupCommands = lookupCommands;
                ClaimCommands = claimCommands;
                LookupMs = lookupMs;
                TotalMs = totalMs;
            }

            internal int Rows { get; }
            internal int LookupCommands { get; }
            internal int ClaimCommands { get; }
            internal double LookupMs { get; }
            internal double TotalMs { get; }
        }
    }
}
