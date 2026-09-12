using System.Security.Cryptography;
using AORebirth.Database.Domain.Missions;
using AORebirth.Interfaces.Persistence.Missions;
using MySqlConnector;
using ZoneEngine_New.Core.Data;

/// <summary>Actual production DAO against the harness-owned disposable MySQL only.</summary>
static class GeneratedMissionSmoke
{
    const int Owner = 9401, Other = 9402, CharacterType = 50000, QuestType = 0xDAC3;
    const long Now = 639200000000000000;

    public static void Validate(DisposableSchemaDatabase fixture, MySqlConnection connection)
    {
        var dao = new MySqlMissionDao(() => fixture.Open());
        FixtureSql.Execute(connection, $"INSERT INTO characters (Id,Name,Online) VALUES ({Owner},'DisposableMissionA',0),({Other},'DisposableMissionB',0)");
        int offerId = dao.ReserveIdentities("offer", 5);
        Require(dao.ReserveIdentities("offer", 1) == offerId + 5, "mission-sql-identity-reservation-overlap");
        int questId = dao.ReserveIdentities("quest", 2);
        int livePf = dao.ReserveIdentities("playfield", 2);
        int firstItem = ReserveItemIds(fixture, 4);
        var batch = Batch(offerId, "disposable-offer-first");
        Require(dao.PublishOffers(batch).Status == GeneratedMissionResultStatus.Applied, "mission-offer-publish");
        string afterOffer = FixtureSql.Fingerprint(connection);
        Require(dao.PublishOffers(batch).Status == GeneratedMissionResultStatus.AlreadyApplied, "mission-offer-publish-idempotence");
        Require(afterOffer == FixtureSql.Fingerprint(connection), "mission-fee-repeated-publication-changed-database");
        batch.Offers[0].DestinationZ += 1;
        Require(dao.PublishOffers(batch).Status == GeneratedMissionResultStatus.Rejected, "mission-same-hash-different-typed-destination-accepted");
        batch.Offers[0].DestinationZ -= 1;
        Require(afterOffer == FixtureSql.Fingerprint(connection), "mission-conflicting-frozen-batch-changed-database");
        Require(dao.ReadOffers(Other).Count == 0, "mission-offer-owner-isolation");
        Require(FixtureSql.Scalar(connection, $"SELECT StatValue FROM stats WHERE Type={CharacterType} AND Instance={Owner} AND StatId=61") == 115, "mission-fee-not-exactly-once");

        var acceptance = Acceptance(offerId, questId, livePf, firstItem, Now + 1);
        acceptance.OwnerId = Other;
        Require(dao.Accept(acceptance).Status == GeneratedMissionResultStatus.Rejected, "mission-cross-owner-accept");
        acceptance.OwnerId = Owner;
        acceptance.Artifacts = [Item(firstItem, 64), Item(firstItem + 1, 64)];
        ExpectRollback(connection, () => dao.Accept(acceptance), 1062, "mission-accept-artifact-collision-rollback");
        acceptance.Artifacts = [Item(firstItem, 64)];
        var accepted = dao.Accept(acceptance);
        Require(accepted.Status == GeneratedMissionResultStatus.Applied, "mission-accept");
        string afterAccept = FixtureSql.Fingerprint(connection);
        Require(dao.Accept(acceptance).Status == GeneratedMissionResultStatus.AlreadyApplied, "mission-accept-idempotence");
        Require(afterAccept == FixtureSql.Fingerprint(connection), "mission-accept-duplicated-artifact-or-binding");
        FixtureSql.Execute(connection, $"UPDATE characters SET Online=1 WHERE Id={Owner}");
        Require(dao.SavePosition(Owner, QuestType, questId, livePf, 5, 6, 7, Now + 2).Status == GeneratedMissionResultStatus.Applied, "mission-owned-position-save");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM characters WHERE Id={Owner} AND Online=1 AND Playfield={livePf} AND X=5 AND Y=6 AND Z=7") == 1,
            "mission-position-save-overwrote-online-or-lost-coordinates");
        FixtureSql.Execute(connection, "ALTER TABLE characters ADD CONSTRAINT fixture_mission_position CHECK (Name <> 'DisposableMissionA' OR X <= 10)");
        ExpectRollback(connection, () => dao.SavePosition(Owner, QuestType, questId, livePf, 20, 30, 40, Now + 2), 3819, "mission-position-write-failure-changed-character");
        FixtureSql.Execute(connection, "ALTER TABLE characters DROP CHECK fixture_mission_position");
        FixtureSql.Execute(connection, $"UPDATE characters SET Online=0 WHERE Id={Owner}");
        Console.WriteLine("MISSION_POSITION_EXISTING_CHARACTER_AUTHORITY=PASS MISSION_POSITION_ONLINE_PRESERVED=PASS MISSION_POSITION_FAILURE_ROLLBACK=PASS");
        var objectState = dao.ReadObjects(Owner, QuestType, questId).Single();
        objectState.CurrentHealth = 75; objectState.X = 12.5f;
        Require(dao.UpdateObjects(Owner, QuestType, questId, [objectState], Now + 2).Status == GeneratedMissionResultStatus.Applied, "mission-object-update");
        var afterObject = FixtureSql.Fingerprint(connection);
        bool staleObjectRejected = false;
        try { dao.UpdateObjects(Owner, QuestType, questId, [objectState], Now + 3); }
        catch (InvalidOperationException) { staleObjectRejected = true; }
        Require(staleObjectRejected && afterObject == FixtureSql.Fingerprint(connection), "mission-object-stale-cas-changed-database");
        var restoredObject = new MySqlMissionDao(() => fixture.Open()).ReadObjects(Owner, QuestType, questId).Single();
        Require(restoredObject.CurrentHealth == 75 && restoredObject.X == 12.5f && restoredObject.Version == 2, "mission-object-hp-position-reset-on-restart");
        Require(dao.ReadObjects(Other, QuestType, questId).Count == 0, "mission-object-owner-isolation");
        Console.WriteLine("MISSION_OBJECT_STATE_ATOMIC_ACCEPT=PASS MISSION_OBJECT_STATE_RESTART=PASS MISSION_OBJECT_STATE_STALE_CAS=PASS");
        var restored = new MySqlMissionDao(() => fixture.Open()).ReadAccepted(Owner, QuestType, questId);
        Require(restored != null && restored.LivePlayfield == livePf && restored.KeyInstance == firstItem
            && restored.ExpiresAtUtcTicks == acceptance.AcceptedAtUtcTicks + TimeSpan.TicksPerHour * 48
            && restored.ExpiresAtUtcTicks > restored.Offer.ExpiresAtUtcTicks
            && restored.BundleId == acceptance.BundleId && restored.BundleSha256 == acceptance.BundleSha256
            && restored.Offer.DestinationType == 0xC9C6 && restored.Offer.DestinationInstance == 6011
            && restored.Offer.DestinationPlayfield == 127 && restored.Offer.DestinationX == 123.125f
            && restored.Offer.DestinationY == 7.5f && restored.Offer.DestinationZ == -92.25f
            && restored.Offer.EntranceLow == 111 && restored.Offer.EntranceHigh == 112,
            "mission-restart-exact-destination-key-acg-binding");
        Require(dao.ReadAccepted(Other, QuestType, questId) == null, "mission-binding-owner-isolation");

        var observation = new GeneratedMissionObservation
        {
            OwnerId = Owner, QuestType = QuestType, QuestInstance = questId, LivePlayfield = livePf,
            ObjectiveType = CharacterType, ObjectiveInstance = 7001, ObjectiveTemplateId = 9001,
            Interaction = 1, ObservationIdentity = "disposable:death:7001:1", ObservedAtUtcTicks = Now + 2,
            CompletionToken = new() { ProgressPercent = 100, CharacterLevel = 25, Side = 0, Disposition = 1, Count = 0 }
        };
        observation.ObjectiveInstance++;
        Require(dao.Observe(observation).Status == GeneratedMissionResultStatus.Rejected, "mission-unrelated-objective-accepted");
        observation.ObjectiveInstance--;
        observation.LivePlayfield++;
        Require(dao.Observe(observation).Status == GeneratedMissionResultStatus.Rejected, "mission-wrong-live-pf-objective-accepted");
        observation.LivePlayfield--;
        var death = dao.ReadObjects(Owner, QuestType, questId).Single();
        death.CurrentHealth = 0; death.IsDead = true;
        death.DeathActorId = Owner; death.DiedAtUtcTicks = Now + 2;
        death.CorpseCredits = 21; death.CorpseExpiresAtUtcTicks = Now + 2 + 60600 * TimeSpan.TicksPerMillisecond;
        observation.Objects = [death];
        Require(dao.Observe(observation).Status == GeneratedMissionResultStatus.Applied, "mission-matched-objective-not-applied");
        string afterObjective = FixtureSql.Fingerprint(connection);
        Require(dao.Observe(observation).Status == GeneratedMissionResultStatus.AlreadyApplied, "mission-observation-idempotence");
        Require(afterObjective == FixtureSql.Fingerprint(connection), "mission-duplicate-objective-changed-database");

        var completion = new GeneratedMissionCompletion
        {
            OwnerId = Owner, CharacterType = CharacterType, QuestType = QuestType, QuestInstance = questId,
            CurrentCash = 115, CurrentExperience = 200, CompletedAtUtcTicks = Now + 3,
            RequestedExperienceReward = 100, FinalExperience = 300,
            CurrentLevel = 25,
            ProgressionStats = [new MissionStatValueData { StatIdentityType = CharacterType, StatId = 52, Value = 300 }],
            Items = [Item(firstItem + 1, 65, 99, 100, 25)]
        };
        FixtureSql.Execute(connection, $"ALTER TABLE stats ADD CONSTRAINT fixture_mission_xp_limit CHECK (Instance <> {Owner} OR StatId <> 52 OR StatValue <= 200)");
        ExpectRollback(connection, () => dao.Complete(completion), 3819, "mission-reward-late-xp-failure-rolls-back-item-cash-ledger-and-completion");
        FixtureSql.Execute(connection, "ALTER TABLE stats DROP CHECK fixture_mission_xp_limit");
        var completed = dao.Complete(completion);
        Require(completed.Status == GeneratedMissionResultStatus.Applied && completed.Cash == 165 && completed.Experience == 300, "mission-atomic-reward-stat-values");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE InstanceId={firstItem + 1} AND ContainerInstance={Owner} AND LowId=99 AND HighId=100 AND Quality=25 AND StackCount=1") == 1, "mission-reward-item-not-durable");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM missionrewardledger WHERE CharacterId={Owner}") == 2, "mission-reward-existing-ledger-not-used");
        string afterComplete = FixtureSql.Fingerprint(connection);
        completion.CurrentCash = 999; completion.CurrentExperience = 999;
        Require(dao.Complete(completion).Status == GeneratedMissionResultStatus.AlreadyApplied, "mission-completion-idempotence");
        Require(afterComplete == FixtureSql.Fingerprint(connection), "mission-completion-replay-changed-rewards");
        Require(dao.SavePosition(Owner, QuestType, questId, livePf, 8, 6, 7, Now + 4).Status == GeneratedMissionResultStatus.Applied, "completed-mission-owner-can-walk-to-exit");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM characters WHERE Id={Owner} AND Playfield={livePf} AND X=8 AND Y=6 AND Z=7") == 1, "completed-mission-position-not-durable");
        Console.WriteLine("MISSION_SQL_FROZEN_OFFERS_AND_FEE=PASS MISSION_ACCEPT_ARTIFACT_ROLLBACK=PASS MISSION_EXACT_DESTINATION_RESTART=PASS MISSION_OBJECTIVE_OWNER_IDEMPOTENCE=PASS MISSION_REWARD_ATOMIC_ROLLBACK=PASS MISSION_REWARD_EXACTLY_ONCE=PASS");

        var secondBatch = Batch(offerId + 1, "disposable-offer-expiry");
        secondBatch.CurrentCash = 165;
        Require(dao.PublishOffers(secondBatch).Status == GeneratedMissionResultStatus.Applied, "mission-expiry-offer");
        var secondAccept = Acceptance(offerId + 1, questId + 1, livePf + 1, firstItem + 2, Now + 1);
        secondAccept.Artifacts = [Item(firstItem + 2, 66)];
        Require(dao.Accept(secondAccept).Status == GeneratedMissionResultStatus.Applied, "mission-expiry-accept");
        Require(dao.End(Owner, QuestType, questId + 1, GeneratedMissionState.Expired, Now + 10).Status == GeneratedMissionResultStatus.Rejected, "mission-premature-expiry");
        var expired = dao.End(Owner, QuestType, questId + 1, GeneratedMissionState.Expired, secondAccept.ExpiresAtUtcTicks);
        Require(expired.Status == GeneratedMissionResultStatus.Applied, "mission-expiry-transition");
        string afterExpiry = FixtureSql.Fingerprint(connection);
        Require(dao.End(Owner, QuestType, questId + 1, GeneratedMissionState.Expired, secondAccept.ExpiresAtUtcTicks).Status == GeneratedMissionResultStatus.AlreadyApplied, "mission-expiry-idempotence");
        Require(afterExpiry == FixtureSql.Fingerprint(connection), "mission-expiry-replay-changed-database");
        Require(dao.AdvanceCleanup(Owner, QuestType, questId + 1, expired.Binding.Version, 1L << 16, Now + 2000).Status == GeneratedMissionResultStatus.Rejected, "mission-early-pf-release");
        var checkpoint = dao.AdvanceCleanup(Owner, QuestType, questId + 1, expired.Binding.Version, 1, Now + 2000);
        Require(checkpoint.Status == GeneratedMissionResultStatus.Applied, "mission-cleanup-first-checkpoint");
        Require(dao.AdvanceCleanup(Owner, QuestType, questId + 1, expired.Binding.Version, 3, Now + 2001).Status == GeneratedMissionResultStatus.Rejected, "mission-cleanup-stale-cas");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM generatedmissionbindings WHERE QuestInstance={questId + 1} AND ActivePlayfield={livePf + 1}") == 1, "mission-pf-released-before-cleanup-complete");
        var removedWorld = dao.AdvanceCleanup(Owner, QuestType, questId + 1, checkpoint.Binding.Version, 255, secondAccept.ExpiresAtUtcTicks + 1);
        Require(removedWorld.Status == GeneratedMissionResultStatus.Applied, "mission-cleanup-world-order");
        FixtureSql.Execute(connection, $"UPDATE item_instances SET ContainerType=105,ContainerInstance={Owner + 1} WHERE InstanceId={firstItem + 2}");
        string nonMain = FixtureSql.Fingerprint(connection);
        Require(dao.CleanupArtifacts(Owner, QuestType, questId + 1, secondAccept.ExpiresAtUtcTicks + 2).Status == GeneratedMissionResultStatus.Rejected
            && nonMain == FixtureSql.Fingerprint(connection), "mission-cleanup-foreign-bank-must-remain-pending");
        FixtureSql.Execute(connection, $"UPDATE item_instances SET ContainerType=51017,ContainerInstance={Owner} WHERE InstanceId={firstItem + 2}");
        nonMain = FixtureSql.Fingerprint(connection);
        Require(dao.CleanupArtifacts(Owner, QuestType, questId + 1, secondAccept.ExpiresAtUtcTicks + 2).Status == GeneratedMissionResultStatus.Rejected
            && nonMain == FixtureSql.Fingerprint(connection), "mission-cleanup-unverified-container-must-remain-pending");
        FixtureSql.Execute(connection, $"UPDATE item_instances SET ContainerType=105 WHERE InstanceId={firstItem + 2}");
        FixtureSql.Execute(connection, $"ALTER TABLE generatedmissionbindings ADD CONSTRAINT fixture_mission_bank_cleanup CHECK (QuestInstance <> {questId + 1} OR CleanupCheckpoints < 256)");
        ExpectRollback(connection, () => dao.CleanupArtifacts(Owner, QuestType, questId + 1, secondAccept.ExpiresAtUtcTicks + 2), 3819,
            "mission-bank-cleanup-late-checkpoint-failure-changed-artifacts");
        FixtureSql.Execute(connection, "ALTER TABLE generatedmissionbindings DROP CHECK fixture_mission_bank_cleanup");
        var cleanedItems = dao.CleanupArtifacts(Owner, QuestType, questId + 1, secondAccept.ExpiresAtUtcTicks + 2);
        Require(cleanedItems.Status == GeneratedMissionResultStatus.Applied && cleanedItems.Binding.CleanupCheckpoints == 511, "mission-cleanup-item-checkpoint-atomic");
        Require(dao.ReadArtifacts(Owner, QuestType, questId + 1).All(item => item.ContainerType == 0), "mission-cleanup-key-not-retired");
        string afterItems = FixtureSql.Fingerprint(connection);
        Require(dao.CleanupArtifacts(Owner, QuestType, questId + 1, secondAccept.ExpiresAtUtcTicks + 3).Status == GeneratedMissionResultStatus.AlreadyApplied
            && afterItems == FixtureSql.Fingerprint(connection), "mission-cleanup-retry-changed-retired-artifacts");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE InstanceId={firstItem + 1} AND ContainerType=104") == 1,
            "mission-cleanup-deleted-earned-reward");
        Console.WriteLine("MISSION_EXPIRY_RESTART_IDEMPOTENCE=PASS MISSION_CLEANUP_MONOTONIC_CAS=PASS");
        Console.WriteLine("MISSION_CLEANUP_ARTIFACT_CHECKPOINT_ATOMIC=PASS MISSION_CLEANUP_FOREIGN_PAGE_PENDING=PASS MISSION_CLEANUP_REWARD_PRESERVED=PASS");
        Console.WriteLine("MISSION_BANK_CLEANUP_ATOMIC=PASS MISSION_BANK_CLEANUP_LATE_FAILURE_ROLLBACK=PASS MISSION_BANK_CLEANUP_RESTART_IDEMPOTENCE=PASS");
        ValidatePickupReturn(fixture, connection, dao, offerId + 2);
        int corpseCash = (int)FixtureSql.Scalar(connection, $"SELECT StatValue FROM stats WHERE Type=50000 AND Instance={Owner} AND StatId=61");
        long claimTime = Now + TimeSpan.TicksPerSecond;
        string beforeCorpse = FixtureSql.Fingerprint(connection);
        Require(dao.ClaimCorpseCredits(Owner, QuestType, questId, 7002, corpseCash, claimTime).Status == GeneratedMissionResultStatus.Rejected
            && beforeCorpse == FixtureSql.Fingerprint(connection), "mission-foreign-corpse-claim-mutated-state");
        FixtureSql.Execute(connection, "ALTER TABLE generatedmissionobjects ADD CONSTRAINT fixture_mission_claim CHECK (RuntimeInstance <> 7001 OR CorpseClaimed=0)");
        ExpectRollback(connection, () => dao.ClaimCorpseCredits(Owner, QuestType, questId, 7001, corpseCash, claimTime), 3819, "mission-corpse-late-failure-changed-cash-or-ledger");
        FixtureSql.Execute(connection, "ALTER TABLE generatedmissionobjects DROP CHECK fixture_mission_claim");
        var claimed = dao.ClaimCorpseCredits(Owner, QuestType, questId, 7001, corpseCash, claimTime);
        Require(claimed.Status == GeneratedMissionResultStatus.Applied && claimed.Cash == corpseCash + 21, "mission-corpse-credits-not-atomic");
        string afterCorpse = FixtureSql.Fingerprint(connection);
        Require(dao.ClaimCorpseCredits(Owner, QuestType, questId, 7001, corpseCash, claimTime + 1).Status == GeneratedMissionResultStatus.AlreadyApplied
            && afterCorpse == FixtureSql.Fingerprint(connection), "mission-corpse-replay-duplicated-credits");
        Require(dao.ReadObjects(Owner, QuestType, questId).Single().CorpseClaimed, "mission-corpse-claim-not-restored");
        Console.WriteLine("MISSION_CORPSE_CREDITS_ATOMIC=PASS MISSION_CORPSE_LATE_FAILURE_ROLLBACK=PASS MISSION_CORPSE_RESTART_EXACTLY_ONCE=PASS");
        ValidateTokenFreeze(fixture, connection, dao);
    }

    static void ValidateTokenFreeze(DisposableSchemaDatabase fixture, MySqlConnection connection, MySqlMissionDao dao)
    {
        int count = (int)typeof(ZoneEngine_New.Core.Missions.GeneratedMissionService).Assembly
            .GetType("ZoneEngine.Core.Missions.MissionLevelTable", true)!.GetMethod("GetTokenReward")!.Invoke(null, [25])!;
        foreach (bool full in new[] { false, true })
        {
            int offer = dao.ReserveIdentities("offer", 1), quest = dao.ReserveIdentities("quest", 1), pf = dao.ReserveIdentities("playfield", 1);
            int item = ReserveItemIds(fixture, 2), objective = full ? 7201 : 7101, slot = full ? 74 : 71;
            int cash = (int)FixtureSql.Scalar(connection, $"SELECT StatValue FROM stats WHERE Type=50000 AND Instance={Owner} AND StatId=61");
            var batch = Batch(offer, "disposable-token-" + full); batch.Fee = 0; batch.CurrentCash = cash;
            batch.Offers[0].MissionType = 1; batch.Offers[0].CashReward = 0; batch.Offers[0].ExperienceReward = full ? 100 : 0;
            batch.Offers[0].RewardCount = 0; batch.Offers[0].RewardLowId = 0; batch.Offers[0].RewardHighId = 0; batch.Offers[0].RewardQuality = 0;
            Require(dao.PublishOffers(batch).Status == GeneratedMissionResultStatus.Applied, "token-offer");
            var accepted = Acceptance(offer, quest, pf, item, Now + 1); accepted.ObjectiveInteraction = 2; accepted.ObjectiveInstance = objective;
            accepted.Artifacts = [Item(item, slot)]; accepted.Objects[0].RuntimeInstance = objective; accepted.Objects[0].CapturedInstance = objective + 100;
            var ambient = new GeneratedMissionObject
            {
                OwnerId = Owner, QuestType = QuestType, QuestInstance = quest, RuntimeType = CharacterType, RuntimeInstance = objective + 1,
                CapturedType = CharacterType, CapturedInstance = objective + 101, Kind = 8, TemplateId = 9002,
                HeadingW = 1, Level = 25, CurrentHealth = 100, MaxHealth = 100, Version = 1, UpdatedAtUtcTicks = Now + 1
            };
            accepted.Objects = accepted.Objects.Concat([ambient]).ToArray();
            Require(dao.Accept(accepted).Status == GeneratedMissionResultStatus.Applied, "token-accept");
            if (full)
            {
                ambient = dao.ReadObjects(Owner, QuestType, quest).Single(row => row.Kind == 8);
                ambient.IsDead = true; ambient.CurrentHealth = 0; ambient.DeathActorId = Owner; ambient.DiedAtUtcTicks = Now + 2;
                ambient.CorpseCredits = 21; ambient.CorpseExpiresAtUtcTicks = ambient.DiedAtUtcTicks + 60600 * TimeSpan.TicksPerMillisecond;
                Require(dao.UpdateObjects(Owner, QuestType, quest, [ambient], Now + 2).Status == GeneratedMissionResultStatus.Applied, "token-countable-death");
            }
            var observation = new GeneratedMissionObservation
            {
                OwnerId = Owner, QuestType = QuestType, QuestInstance = quest, LivePlayfield = pf,
                ObjectiveType = CharacterType, ObjectiveInstance = objective, ObjectiveTemplateId = 9001,
                Interaction = 2, ObservationIdentity = "token-info:" + objective, ObservedAtUtcTicks = Now + 3,
                CompletionToken = new() { ProgressPercent = full ? 100 : 0, CharacterLevel = 25, Side = 1, Disposition = full ? 2 : 0, Count = full ? count : 0 }
            };
            Require(dao.Observe(observation).Status == GeneratedMissionResultStatus.Applied, "token-completion-freeze");
            if (!full)
            {
                ambient = dao.ReadObjects(Owner, QuestType, quest).Single(row => row.Kind == 8);
                ambient.IsDead = true; ambient.CurrentHealth = 0; ambient.DeathActorId = Owner; ambient.DiedAtUtcTicks = Now + 4;
                ambient.CorpseCredits = 21; ambient.CorpseExpiresAtUtcTicks = ambient.DiedAtUtcTicks + 60600 * TimeSpan.TicksPerMillisecond;
                Require(dao.UpdateObjects(Owner, QuestType, quest, [ambient], Now + 4).Status == GeneratedMissionResultStatus.Applied, "token-later-death");
                var frozen = dao.ReadAccepted(Owner, QuestType, quest);
                Require(frozen.TokenProgressPercent == 0 && frozen.TokenDisposition == 0 && frozen.TokenCount == 0 && frozen.TokenClaimLevel == 25 && frozen.TokenClaimSide == 1,
                    "token-claim-changed-after-objective-verification");
            }
            int xp = (int)FixtureSql.Scalar(connection, $"SELECT StatValue FROM stats WHERE Type=50000 AND Instance={Owner} AND StatId=52");
            var completion = new GeneratedMissionCompletion
            {
                OwnerId = Owner, CharacterType = CharacterType, QuestType = QuestType, QuestInstance = quest,
                CurrentCash = cash, CurrentExperience = xp, FinalExperience = xp, CompletedAtUtcTicks = Now + 5,
                CurrentLevel = full ? 220 : 25, RequestedExperienceReward = full ? 100 : 0,
                Items = [], TokenItem = full ? Item(item + 1, slot + 1, 103910, 103911) : null
            };
            if (full)
            {
                completion.TokenItem!.StackCount = count;
                FixtureSql.Execute(connection, "ALTER TABLE item_instances ADD CONSTRAINT fixture_token_item CHECK (LowId<>103910)");
                ExpectRollback(connection, () => dao.Complete(completion), 3819, "token-grant-failure-changed-frozen-claim-or-ledger");
                FixtureSql.Execute(connection, "ALTER TABLE item_instances DROP CHECK fixture_token_item");
            }
            Require(dao.Complete(completion).Status == GeneratedMissionResultStatus.Applied, "token-completion-atomic");
            string after = FixtureSql.Fingerprint(connection);
            Require(dao.Complete(completion).Status == GeneratedMissionResultStatus.AlreadyApplied && after == FixtureSql.Fingerprint(connection), "token-reward-replay-changed-state");
            Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM generatedmissionartifacts WHERE QuestInstance={quest} AND ArtifactRole=5") == (full ? 1 : 0), "token-physical-grant-count");
            var survivor = dao.ReadObjects(Owner, QuestType, quest).Single(row => row.Kind == 7); survivor.CurrentHealth = 50;
            Require(dao.UpdateObjects(Owner, QuestType, quest, [survivor], Now + 6).Status == GeneratedMissionResultStatus.Applied, "completed-world-npc-state-failed");
            var ended = dao.ReadAccepted(Owner, QuestType, quest);
            Require(dao.AdvanceCleanup(Owner, QuestType, quest, ended.Version, 1, Now + 7).Status == GeneratedMissionResultStatus.Applied, "completed-world-cleanup-start");
            after = FixtureSql.Fingerprint(connection);
            Require(dao.UpdateObjects(Owner, QuestType, quest, [survivor], Now + 8).Status == GeneratedMissionResultStatus.Rejected && after == FixtureSql.Fingerprint(connection), "cleanup-world-accepted-npc-mutation");
        }
        Console.WriteLine("MISSION_TOKEN_OBJECTIVE_TIME_FREEZE=PASS MISSION_TOKEN_ATOMIC_GRANT_ROLLBACK=PASS MISSION_TOKEN_RESTART_EXACTLY_ONCE=PASS COMPLETED_MISSION_NPC_LIFETIME_GUARD=PASS MAX_LEVEL_MISSION_XP_NOOP=PASS");
    }

    static void ValidatePickupReturn(DisposableSchemaDatabase fixture, MySqlConnection connection, MySqlMissionDao dao, int offer)
    {
        int quest = dao.ReserveIdentities("quest", 1), pf = dao.ReserveIdentities("playfield", 1), item = ReserveItemIds(fixture, 2);
        var batch = Batch(offer, "disposable-return"); batch.Fee = 0; batch.CurrentCash = 160; batch.Offers[0].MissionType = 4;
        Require(dao.PublishOffers(batch).Status == GeneratedMissionResultStatus.Applied, "return-offer");
        var accept = Acceptance(offer, quest, pf, item, Now + 1);
        accept.Artifacts = [Item(item, 68)]; accept.ObjectiveInstance = 7003; accept.ObjectiveInteraction = 4;
        accept.Objects[0].RuntimeInstance = 7003; accept.Objects[0].CapturedInstance = 7013;
        accept.Objects[0].CurrentHealth = null; accept.Objects[0].MaxHealth = null;
        Require(dao.Accept(accept).Status == GeneratedMissionResultStatus.Applied, "return-accept");
        Console.WriteLine("MISSION_RETURN_FIXTURE_ACCEPT=PASS");
        var target = dao.ReadObjects(Owner, QuestType, quest).Single(); target.ObjectiveConsumed = true;
        var pickup = new GeneratedMissionObservation
        {
            OwnerId = Owner, QuestType = QuestType, QuestInstance = quest, LivePlayfield = pf,
            ObjectiveType = CharacterType, ObjectiveInstance = 7003, ObjectiveTemplateId = 9001,
            Interaction = 3, AdvanceProgress = false, ObservationIdentity = "pickup:7003", ObservedAtUtcTicks = Now + 2,
            Objects = [target], Grants = [Item(item + 1, 69, 9001, 9001, 25)]
        };
        FixtureSql.Execute(connection, "ALTER TABLE item_instances ADD CONSTRAINT fixture_pickup_failure CHECK (LowId <> 9001)");
        ExpectRollback(connection, () => dao.Observe(pickup), 3819, "pickup-late-item-failure-must-restore-object-and-observation");
        FixtureSql.Execute(connection, "ALTER TABLE item_instances DROP CHECK fixture_pickup_failure");
        Console.WriteLine("MISSION_RETURN_FIXTURE_PICKUP_FAILURE_ROLLBACK=PASS");
        Require(dao.Observe(pickup).Status == GeneratedMissionResultStatus.Applied, "pickup-commit");
        var restored = new MySqlMissionDao(() => fixture.Open()).ReadAccepted(Owner, QuestType, quest);
        Require(restored.Progress == 0 && restored.MissionItem?.InstanceId == item + 1, "return-pickup-must-freeze-item-without-completing");
        string afterPickup = FixtureSql.Fingerprint(connection);
        Require(dao.Observe(pickup).Status == GeneratedMissionResultStatus.AlreadyApplied && afterPickup == FixtureSql.Fingerprint(connection), "pickup-replay-changed-state");
        var returned = new GeneratedMissionObservation
        {
            OwnerId = Owner, QuestType = QuestType, QuestInstance = quest, LivePlayfield = pf,
            ObjectiveType = CharacterType, ObjectiveInstance = 7003, ObjectiveTemplateId = 9001,
            Interaction = 4, ObservationIdentity = "return:7003", ObservedAtUtcTicks = Now + 3,
            ConsumeItem = Item(item + 1, 69, 9001, 9001, 25), TerminalType = 0xDAC1, TerminalInstance = 56, ActualPlayfield = 500,
            CompletionToken = new() { ProgressPercent = 100, CharacterLevel = 25, Side = 0, Disposition = 1, Count = 0 }
        };
        bool denied = false;
        try { dao.Observe(returned); } catch (InvalidOperationException) { denied = true; }
        Require(denied && afterPickup == FixtureSql.Fingerprint(connection), "return-wrong-terminal-changed-state");
        returned.TerminalInstance = 55; returned.ConsumeItem.InstanceId++;
        denied = false;
        try { dao.Observe(returned); } catch (InvalidOperationException) { denied = true; }
        Require(denied && afterPickup == FixtureSql.Fingerprint(connection), "return-same-template-wrong-instance-changed-state");
        returned.ConsumeItem.InstanceId--;
        Require(dao.Observe(returned).Status == GeneratedMissionResultStatus.Applied, "return-consume-and-objective-commit");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE InstanceId={item + 1} AND ContainerType=0 AND ContainerPlacement=InstanceId") == 1,
            "return-artifact-history-not-retained");
        string afterReturn = FixtureSql.Fingerprint(connection);
        Require(dao.Observe(returned).Status == GeneratedMissionResultStatus.AlreadyApplied && afterReturn == FixtureSql.Fingerprint(connection), "return-replay-changed-state");
        Console.WriteLine("MISSION_PICKUP_OBJECT_ITEM_ATOMIC_ROLLBACK=PASS MISSION_RETURN_EXACT_ITEM_AND_TERMINAL=PASS MISSION_RETURN_RESTART_IDEMPOTENCE=PASS");
    }

    static GeneratedMissionOfferBatch Batch(int offerId, string identity)
    {
        byte[] wire = [1, 2, 3, 4]; // Test-only projection bytes; never a game/world fixture.
        return new()
        {
            OwnerId = Owner, OwnerType = CharacterType, BatchIdentity = identity, RollSeed = 77, ResponseNonce = 88,
            Fee = 5, CurrentCash = 120, TerminalType = 0xDAC1, TerminalInstance = 55, TerminalPlayfield = 500,
            OfferedAtUtcTicks = Now, ExpiresAtUtcTicks = Now + 1000,
            Offers = [new GeneratedMissionOffer
            {
                OwnerId = Owner, BatchIdentity = identity, OfferIndex = 0, OfferType = QuestType, OfferInstance = offerId,
                MissionType = 0, Quality = 25, DestinationType = 0xC9C6, DestinationInstance = 6011, DestinationPlayfield = 127,
                DestinationX = 123.125f, DestinationY = 7.5f, DestinationZ = -92.25f,
                EntranceType = 0xC9C6, EntranceInstance = 6011, EntranceLow = 111, EntranceHigh = 112,
                CashReward = 50, ExperienceReward = 100, RewardLowId = 99, RewardHighId = 100, RewardQuality = 25, RewardCount = 1,
                Title = "Disposable normalized mission", Description = "Synthetic DAO fixture; no gameplay promotion.",
                FrozenWireBody = wire, FrozenWireSha256 = Convert.ToHexString(SHA256.HashData(wire)).ToLowerInvariant(),
                State = GeneratedMissionState.Offered, OfferedAtUtcTicks = Now, ExpiresAtUtcTicks = Now + 1000, Version = 1
            }]
        };
    }

    static GeneratedMissionAcceptance Acceptance(int offer, int quest, int livePf, int key, long now) => new()
    {
        OwnerId = Owner, OfferType = QuestType, OfferInstance = offer, QuestType = QuestType, QuestInstance = quest,
        KeyInstance = key, BundleId = "disposable-known-bundle", BundleSha256 = new string('a', 64),
        BuildingType = 0xC9C6, BuildingInstance = 6001, LivePlayfield = livePf,
        ObjectiveType = CharacterType, ObjectiveInstance = 7001 + quest % 2, ObjectiveTemplateId = 9001, ObjectiveInteraction = 1, RequiredCount = 1,
        AcceptedAtUtcTicks = now, ExpiresAtUtcTicks = now + TimeSpan.TicksPerHour * 48, Artifacts = [Item(key, 64)],
        Objects = [new GeneratedMissionObject
        {
            OwnerId = Owner, QuestType = QuestType, QuestInstance = quest, RuntimeType = CharacterType, RuntimeInstance = 7001 + quest % 2,
            CapturedType = CharacterType, CapturedInstance = 7011, Kind = 7, TemplateId = 9001, HeadingW = 1,
            CurrentHealth = 100, MaxHealth = 100, Version = 1, UpdatedAtUtcTicks = now
        }]
    };

    static MissionItemInstanceData Item(int instance, int slot, int low = 10, int high = 10, int quality = 1) => new()
    {
        InstanceId = instance, ContainerType = 104, ContainerInstance = Owner, ContainerPlacement = slot,
        ItemType = 0xC76D, LowId = low, HighId = high, Quality = quality, StackCount = 1, Source = 0
    };

    static int ReserveItemIds(DisposableSchemaDatabase fixture, int count)
    {
        string? before = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", fixture.ConnectionString);
            return new MySqlInventoryRepository(new SilentLogger()).LeaseInstanceIdBlock(count);
        }
        finally { Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", before); }
    }

    static void ExpectRollback(MySqlConnection connection, Action action, int number, string code)
    {
        string before = FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false);
        bool failed = false;
        try { action(); }
        catch (MySqlException exception) when (exception.Number == number) { failed = true; }
        Require(failed && before == FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false), code);
    }

    static void Require(bool condition, string code) { if (!condition) throw new FixtureFailure(code); }
}
