namespace AORebirth.Database.Domain.Missions
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using AORebirth.Enums;
    using AORebirth.Interfaces.Persistence.Missions;
    using Dapper;

    /// <summary>Generated missions share the existing DAO connection factory and reward ledger.</summary>
    public sealed partial class MySqlMissionDao : IGeneratedMissionDao
    {
        private const string OfferColumns = "OwnerId,BatchIdentity,OfferIndex,OfferType,OfferInstance,MissionType,Quality,DestinationType,DestinationInstance,DestinationPlayfield,DestinationX,DestinationY,DestinationZ,EntranceType,EntranceInstance,EntranceLow,EntranceHigh,CashReward,ExperienceReward,RewardLowId,RewardHighId,RewardQuality,RewardCount,Title,Description,FrozenWireBody,FrozenWireSha256,State,OfferedAtUtcTicks,ExpiresAtUtcTicks,Version";
        private const string BindingColumns = "OwnerId,OfferType,OfferInstance,QuestType,QuestInstance,TeamType,TeamInstance,KeyInstance,BundleId,BundleSha256,BuildingType,BuildingInstance,LivePlayfield,ObjectiveType,ObjectiveInstance,ObjectiveTemplateId,ObjectiveInteraction,RequiredCount,Progress,State,CleanupCheckpoints,TokenProgressPercent,TokenClaimLevel,TokenClaimSide,TokenDisposition,TokenCount,CompletionFrozenAtUtcTicks,AcceptedAtUtcTicks,ExpiresAtUtcTicks,CompletedAtUtcTicks,UpdatedAtUtcTicks,Version";

        public int ReserveIdentities(string sequence, int count)
        {
            if ((sequence != "offer" && sequence != "quest" && sequence != "playfield") || count < 1 || count > 128)
                throw new ArgumentException("Invalid generated mission identity reservation.");
            return GeneratedTransaction(0, (connection, transaction) =>
            {
                var row = connection.Query<GeneratedSequence>("SELECT NextIdentity,MaximumIdentity FROM generatedmissionsequences WHERE SequenceName=@sequence FOR UPDATE", new { sequence }, transaction).SingleOrDefault();
                if (row == null || row.NextIdentity <= 0 || (long)row.NextIdentity + count - 1 > row.MaximumIdentity)
                    throw new InvalidOperationException("Generated mission identity namespace is absent or exhausted; no fallback identity is allowed.");
                connection.Execute("UPDATE generatedmissionsequences SET NextIdentity=NextIdentity+@count WHERE SequenceName=@sequence", new { sequence, count }, transaction);
                return row.NextIdentity;
            });
        }

        public GeneratedMissionResult PublishOffers(GeneratedMissionOfferBatch batch)
        {
            ValidateBatch(batch);
            return GeneratedTransaction(batch.OwnerId, (connection, transaction) =>
            {
                var prior = connection.Query<GeneratedBatchCash>("SELECT CashAfter FROM generatedmissionbatches WHERE OwnerId=@OwnerId AND BatchIdentity=@BatchIdentity", batch, transaction).SingleOrDefault();
                if (prior != null)
                {
                    var frozen = connection.Query<GeneratedMissionOffer>("SELECT " + OfferColumns + " FROM generatedmissionoffers WHERE OwnerId=@OwnerId AND BatchIdentity=@BatchIdentity ORDER BY OfferIndex", batch, transaction).ToList();
                    var original = connection.Query<GeneratedMissionOfferBatch>("SELECT * FROM generatedmissionbatches WHERE OwnerId=@OwnerId AND BatchIdentity=@BatchIdentity", batch, transaction).Single();
                    if (!SameBatch(original, batch) || frozen.Count != batch.Offers.Count || frozen.Where((value, index) => !SameOffer(value, batch.Offers[index])).Any())
                        return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Batch identity belongs to different frozen offers.");
                    return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.AlreadyApplied, Cash = prior.CashAfter };
                }
                int before = batch.CurrentCash;
                if (before < batch.Fee) return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Insufficient credits; no offer or fee was published.");
                var parameters = new DynamicParameters(batch);
                parameters.Add("CashBefore", before); parameters.Add("CashAfter", before - batch.Fee);
                connection.Execute("INSERT INTO generatedmissionbatches (OwnerId,OwnerType,BatchIdentity,RollSeed,ResponseNonce,Fee,TerminalType,TerminalInstance,TerminalPlayfield,LevelSlider,GoodBadSlider,OrderChaosSlider,OpenHiddenSlider,PhysicalMysticalSlider,HeadOnStealthSlider,MoneyExperienceSlider,OfferedAtUtcTicks,ExpiresAtUtcTicks,CashBefore,CashAfter) VALUES (@OwnerId,@OwnerType,@BatchIdentity,@RollSeed,@ResponseNonce,@Fee,@TerminalType,@TerminalInstance,@TerminalPlayfield,@LevelSlider,@GoodBadSlider,@OrderChaosSlider,@OpenHiddenSlider,@PhysicalMysticalSlider,@HeadOnStealthSlider,@MoneyExperienceSlider,@OfferedAtUtcTicks,@ExpiresAtUtcTicks,@CashBefore,@CashAfter)", parameters, transaction);
                connection.Execute("UPDATE generatedmissionoffers SET State=@replaced,Version=Version+1 WHERE OwnerId=@owner AND State=@offered", new { owner = batch.OwnerId, replaced = (int)GeneratedMissionState.Replaced, offered = (int)GeneratedMissionState.Offered }, transaction);
                foreach (var offer in batch.Offers)
                    connection.Execute("INSERT INTO generatedmissionoffers (" + OfferColumns + ") VALUES (" + string.Join(",", OfferColumns.Split(',').Select(column => "@" + column)) + ")", offer, transaction);
                WriteGeneratedStat(connection, transaction, batch.OwnerType, batch.OwnerId, (int)StatIds.cash, before - batch.Fee);
                WriteGeneratedRewardLedger(connection, transaction, batch.OwnerId, "generated-offer:" + batch.BatchIdentity, "roll-fee", "GeneratedMissionRollFee", "fee=" + batch.Fee.ToString(CultureInfo.InvariantCulture), batch.OfferedAtUtcTicks);
                return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.Applied, Cash = before - batch.Fee };
            });
        }

        public GeneratedMissionResult Accept(GeneratedMissionAcceptance acceptance)
        {
            ValidateAcceptance(acceptance);
            return GeneratedTransaction(acceptance.OwnerId, (connection, transaction) =>
            {
                var existing = connection.Query<GeneratedMissionBinding>("SELECT " + BindingColumns + " FROM generatedmissionbindings WHERE OwnerId=@OwnerId AND OfferType=@OfferType AND OfferInstance=@OfferInstance", acceptance, transaction).SingleOrDefault();
                if (existing != null)
                {
                    HydrateOffer(connection, transaction, existing);
                    return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.AlreadyApplied, Binding = existing };
                }
                var offer = ReadGeneratedOffer(connection, transaction, acceptance.OwnerId, acceptance.OfferType, acceptance.OfferInstance);
                if (offer == null || offer.State != GeneratedMissionState.Offered || acceptance.AcceptedAtUtcTicks >= offer.ExpiresAtUtcTicks)
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Offer is not current and owned, or its frozen expiry is invalid.");
                int expectedInteraction = new[] { 1, 2, 3, 5, 4 }[offer.MissionType];
                if (acceptance.ObjectiveInteraction != expectedInteraction)
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Objective interaction does not match the frozen mission type.");
                var binding = new GeneratedMissionBinding
                {
                    OwnerId = acceptance.OwnerId, OfferType = acceptance.OfferType, OfferInstance = acceptance.OfferInstance,
                    QuestType = acceptance.QuestType, QuestInstance = acceptance.QuestInstance, TeamType = acceptance.TeamType, TeamInstance = acceptance.TeamInstance,
                    KeyInstance = acceptance.KeyInstance, BundleId = acceptance.BundleId, BundleSha256 = acceptance.BundleSha256,
                    BuildingType = acceptance.BuildingType, BuildingInstance = acceptance.BuildingInstance, LivePlayfield = acceptance.LivePlayfield,
                    ObjectiveType = acceptance.ObjectiveType, ObjectiveInstance = acceptance.ObjectiveInstance, ObjectiveTemplateId = acceptance.ObjectiveTemplateId,
                    ObjectiveInteraction = acceptance.ObjectiveInteraction, RequiredCount = acceptance.RequiredCount,
                    State = GeneratedMissionState.Active, AcceptedAtUtcTicks = acceptance.AcceptedAtUtcTicks, ExpiresAtUtcTicks = acceptance.ExpiresAtUtcTicks,
                    UpdatedAtUtcTicks = acceptance.AcceptedAtUtcTicks, Version = 1, Offer = offer
                };
                connection.Execute("INSERT INTO generatedmissionbindings (" + BindingColumns + ",ActivePlayfield) VALUES (" + string.Join(",", BindingColumns.Split(',').Select(column => "@" + column)) + ",@LivePlayfield)", binding, transaction);
                foreach (var value in acceptance.Objects)
                {
                    if (value.OwnerId != acceptance.OwnerId || value.QuestType != acceptance.QuestType || value.QuestInstance != acceptance.QuestInstance || value.Version != 1)
                        throw new ArgumentException("Materialized object does not belong to the accepted binding.");
                    ValidateObject(value);
                    connection.Execute("INSERT INTO generatedmissionobjects (" + ObjectColumns + ") VALUES (" + string.Join(",", ObjectColumns.Split(',').Select(column => "@" + column)) + ")", value, transaction);
                }
                foreach (var item in acceptance.Artifacts)
                {
                    InsertGeneratedItem(connection, transaction, item, acceptance.OwnerId);
                    WriteGeneratedArtifact(connection, transaction, binding, item.InstanceId, item.InstanceId == acceptance.KeyInstance ? 1 : 2, acceptance.AcceptedAtUtcTicks);
                    if (item.InstanceId != acceptance.KeyInstance) binding.MissionItem = item;
                }
                connection.Execute("UPDATE generatedmissionoffers SET State=@state,Version=Version+1 WHERE OfferType=@OfferType AND OfferInstance=@OfferInstance", new { state = (int)GeneratedMissionState.Active, acceptance.OfferType, acceptance.OfferInstance }, transaction);
                return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.Applied, Binding = binding };
            });
        }

        public IList<GeneratedMissionOffer> ReadOffers(int ownerId)
        {
            if (ownerId <= 0) throw new ArgumentOutOfRangeException("ownerId");
            using (var connection = OpenConnection())
            {
                var offers = connection.Query<GeneratedMissionOffer>(OfferReadSql + " WHERE o.OwnerId=@ownerId ORDER BY o.OfferedAtUtcTicks,o.OfferIndex", new { ownerId }).ToList();
                foreach (var offer in offers) ValidateFrozenOffer(offer);
                return offers;
            }
        }

        public IList<GeneratedMissionBinding> ReadAccepted(int ownerId)
        {
            if (ownerId <= 0) throw new ArgumentOutOfRangeException("ownerId");
            using (var connection = OpenConnection())
            {
                var bindings = connection.Query<GeneratedMissionBinding>("SELECT " + BindingColumns + " FROM generatedmissionbindings WHERE OwnerId=@ownerId ORDER BY QuestType,QuestInstance", new { ownerId }).ToList();
                foreach (var binding in bindings) HydrateOffer(connection, null, binding);
                return bindings;
            }
        }

        public GeneratedMissionBinding ReadAccepted(int ownerId, int questType, int questInstance)
        {
            if (ownerId <= 0 || questType <= 0 || questInstance <= 0) return null;
            using (var connection = OpenConnection()) return ReadGeneratedBinding(connection, null, ownerId, questType, questInstance);
        }

        public GeneratedMissionResult Observe(GeneratedMissionObservation observation)
        {
            if (observation == null || observation.OwnerId <= 0 || string.IsNullOrWhiteSpace(observation.ObservationIdentity) || observation.ObservationIdentity.Length > 191 || observation.ObservedAtUtcTicks <= 0)
                throw new ArgumentException("Stable server observation identity is required.");
            return GeneratedTransaction(observation.OwnerId, (connection, transaction) =>
            {
                var binding = ReadGeneratedBinding(connection, transaction, observation.OwnerId, observation.QuestType, observation.QuestInstance);
                if (binding == null || binding.State != GeneratedMissionState.Active || observation.ObservedAtUtcTicks >= binding.ExpiresAtUtcTicks
                    || binding.LivePlayfield != observation.LivePlayfield || binding.ObjectiveType != observation.ObjectiveType || binding.ObjectiveInstance != observation.ObjectiveInstance
                    || binding.ObjectiveTemplateId != observation.ObjectiveTemplateId
                    || (binding.ObjectiveInteraction != observation.Interaction && !(binding.Offer.MissionType == 4 && observation.Interaction == 3 && !observation.AdvanceProgress)))
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Observation does not match an active owned objective.");
                if (connection.Query<int>("SELECT 1 FROM generatedmissionobservations WHERE OwnerId=@OwnerId AND QuestType=@QuestType AND QuestInstance=@QuestInstance AND ObservationIdentity=@ObservationIdentity", observation, transaction).Any())
                    return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.AlreadyApplied, Binding = binding };
                if (binding.Progress >= binding.RequiredCount)
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Objective is already satisfied.");
                if (observation.Objects == null || observation.Grants == null
                    || (!observation.AdvanceProgress && !(binding.Offer.MissionType == 4 && observation.Interaction == 3)))
                    throw new ArgumentException("Complete typed objective plan is required.");
                WriteObjects(connection, transaction, binding.OwnerId, binding.QuestType, binding.QuestInstance, observation.Objects, observation.ObservedAtUtcTicks);
                var target = connection.Query<GeneratedMissionObject>("SELECT " + ObjectColumns + " FROM generatedmissionobjects WHERE OwnerId=@OwnerId AND QuestType=@QuestType AND QuestInstance=@QuestInstance AND RuntimeType=@ObjectiveType AND RuntimeInstance=@ObjectiveInstance FOR UPDATE", observation, transaction).Single();
                if ((observation.Interaction == 1 && (!target.IsDead || target.CurrentHealth != 0))
                    || (observation.Interaction == 2 && target.IsDead)
                    || ((observation.Interaction == 3 || observation.Interaction == 5) && !target.ObjectiveConsumed))
                    throw new InvalidOperationException("Objective lacks its durable target-state witness.");
                if (observation.Interaction == 3)
                {
                    if (binding.MissionItem != null || observation.Grants.Count != 1 || observation.ConsumeItem != null)
                        throw new InvalidOperationException("Pickup must grant exactly one previously unissued bound artifact.");
                    var item = observation.Grants[0];
                    if (item.LowId != binding.ObjectiveTemplateId || item.HighId != binding.ObjectiveTemplateId || item.Quality != binding.Offer.Quality || item.StackCount != 1 || item.ItemType != 0xC76D)
                        throw new InvalidOperationException("Pickup artifact differs from the frozen objective.");
                    InsertGeneratedItem(connection, transaction, item, binding.OwnerId);
                    WriteGeneratedArtifact(connection, transaction, binding, item.InstanceId, 4, observation.ObservedAtUtcTicks);
                    binding.MissionItem = item;
                }
                else if (observation.Grants.Count != 0) throw new ArgumentException("Only static pickup may issue an objective item.");
                if (observation.Interaction == 4 || observation.Interaction == 5)
                {
                    if (binding.MissionItem == null || observation.ConsumeItem == null || binding.MissionItem.InstanceId != observation.ConsumeItem.InstanceId)
                        throw new InvalidOperationException("Objective consumption lacks the exact accepted artifact.");
                    if (observation.Interaction == 4 && (!target.ObjectiveConsumed || observation.TerminalType != binding.Offer.IssuingTerminalType
                        || observation.TerminalInstance != binding.Offer.IssuingTerminalInstance || observation.ActualPlayfield != binding.Offer.IssuingTerminalPlayfield))
                        throw new InvalidOperationException("Return requires the exact issuing terminal and playfield.");
                    RetireGeneratedItem(connection, transaction, observation.ConsumeItem, binding.OwnerId);
                }
                else if (observation.ConsumeItem != null) throw new ArgumentException("This objective cannot consume an artifact.");
                if (observation.AdvanceProgress && binding.Progress + 1 == binding.RequiredCount)
                    FreezeGeneratedToken(connection, transaction, binding, observation.CompletionToken, observation.ObservedAtUtcTicks);
                connection.Execute("INSERT INTO generatedmissionobservations (OwnerId,QuestType,QuestInstance,ObservationIdentity,LivePlayfield,ObjectiveType,ObjectiveInstance,ObjectiveTemplateId,Interaction,ObservedAtUtcTicks) VALUES (@OwnerId,@QuestType,@QuestInstance,@ObservationIdentity,@LivePlayfield,@ObjectiveType,@ObjectiveInstance,@ObjectiveTemplateId,@Interaction,@ObservedAtUtcTicks)", observation, transaction);
                connection.Execute("UPDATE generatedmissionbindings SET Progress=Progress+@AdvanceProgress,Version=Version+1,UpdatedAtUtcTicks=@ObservedAtUtcTicks WHERE QuestType=@QuestType AND QuestInstance=@QuestInstance", observation, transaction);
                if (observation.AdvanceProgress) binding.Progress++;
                binding.Version++;
                return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.Applied, Binding = binding };
            });
        }

        public GeneratedMissionResult Complete(GeneratedMissionCompletion completion)
        {
            if (completion == null || completion.OwnerId <= 0 || completion.CharacterType != 50000 || completion.CompletedAtUtcTicks <= 0 || completion.Items == null || completion.Items.Any(item => item == null || item.StackCount <= 0) || completion.CurrentCash < 0 || completion.CurrentExperience < 0)
                throw new ArgumentException("A complete owner-scoped reward plan is required.");
            return GeneratedTransaction(completion.OwnerId, (connection, transaction) =>
            {
                var binding = ReadGeneratedBinding(connection, transaction, completion.OwnerId, completion.QuestType, completion.QuestInstance);
                if (binding == null) return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Mission is not owned.");
                if (binding.State == GeneratedMissionState.Completed)
                    return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.AlreadyApplied, Binding = binding };
                if (binding.State != GeneratedMissionState.Active || completion.CompletedAtUtcTicks >= binding.ExpiresAtUtcTicks || binding.Progress != binding.RequiredCount
                    || binding.CompletionFrozenAtUtcTicks <= 0 || !binding.TokenDisposition.HasValue)
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Mission is inactive, expired, or incomplete.");
                var offer = binding.Offer;
                ValidateProgression(completion, offer);
                if (completion.Items.Sum(item => (long)item.StackCount) != offer.RewardCount
                    || completion.Items.Any(item => item.LowId != offer.RewardLowId || item.HighId != offer.RewardHighId || item.Quality != offer.RewardQuality))
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Item grant plan differs from the frozen reward.");
                foreach (var item in completion.Items)
                {
                    InsertGeneratedItem(connection, transaction, item, completion.OwnerId);
                    WriteGeneratedArtifact(connection, transaction, binding, item.InstanceId, 3, completion.CompletedAtUtcTicks);
                }
                if (binding.TokenDisposition == 2)
                {
                    var token = completion.TokenItem;
                    int low = binding.TokenClaimSide == 1 ? 103910 : 103908;
                    if (token == null || token.LowId != low || token.HighId != low + 1 || token.Quality != 1
                        || token.StackCount != binding.TokenCount || token.ContainerType != 104 || token.ContainerInstance != binding.OwnerId)
                        throw new InvalidOperationException("Token item differs from the sealed completion claim.");
                    InsertGeneratedItem(connection, transaction, token, completion.OwnerId);
                    WriteGeneratedArtifact(connection, transaction, binding, token.InstanceId, 5, completion.CompletedAtUtcTicks);
                    WriteGeneratedRewardLedger(connection, transaction, binding.OwnerId, GeneratedQuestKey(binding.QuestType, binding.QuestInstance), "token", "GeneratedMissionToken", token.InstanceId.ToString(CultureInfo.InvariantCulture), completion.CompletedAtUtcTicks);
                }
                else if (completion.TokenItem != null) throw new InvalidOperationException("Unresolved/explicit-none token claim cannot grant an item.");
                int cash = (int)System.Math.Min(int.MaxValue, (long)completion.CurrentCash + offer.CashReward);
                int xp = completion.FinalExperience;
                WriteGeneratedStat(connection, transaction, completion.CharacterType, completion.OwnerId, (int)StatIds.cash, cash);
                foreach (var stat in completion.ProgressionStats)
                    WriteGeneratedStat(connection, transaction, completion.CharacterType, completion.OwnerId, stat.StatId, checked((int)stat.Value));
                WriteGeneratedRewardLedger(connection, transaction, completion.OwnerId, GeneratedQuestKey(binding.QuestType, binding.QuestInstance), "completion", "GeneratedMissionCompletion", "offer=" + offer.OfferInstance.ToString(CultureInfo.InvariantCulture), completion.CompletedAtUtcTicks);
                connection.Execute("UPDATE generatedmissionbindings SET State=@state,CompletedAtUtcTicks=@CompletedAtUtcTicks,UpdatedAtUtcTicks=@CompletedAtUtcTicks,Version=Version+1 WHERE QuestType=@QuestType AND QuestInstance=@QuestInstance", new { state = (int)GeneratedMissionState.Completed, completion.CompletedAtUtcTicks, completion.QuestType, completion.QuestInstance }, transaction);
                binding.State = GeneratedMissionState.Completed; binding.CompletedAtUtcTicks = completion.CompletedAtUtcTicks; binding.Version++;
                return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.Applied, Binding = binding, Cash = cash, Experience = xp };
            });
        }

        public GeneratedMissionResult End(int ownerId, int questType, int questInstance, GeneratedMissionState state, long nowUtcTicks)
        {
            if (state != GeneratedMissionState.Abandoned && state != GeneratedMissionState.Expired) throw new ArgumentException("Only abandon/expiry are terminal cleanup requests.");
            if (ownerId <= 0 || nowUtcTicks <= 0) throw new ArgumentException("Stable owner/time required.");
            return GeneratedTransaction(ownerId, (connection, transaction) =>
            {
                var binding = ReadGeneratedBinding(connection, transaction, ownerId, questType, questInstance);
                if (binding == null) return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Mission is not owned.");
                if (binding.State == state) return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.AlreadyApplied, Binding = binding };
                if (binding.State != GeneratedMissionState.Active || (state == GeneratedMissionState.Expired && nowUtcTicks < binding.ExpiresAtUtcTicks))
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Terminal transition is not eligible.");
                connection.Execute("UPDATE generatedmissionbindings SET State=@state,UpdatedAtUtcTicks=@nowUtcTicks,Version=Version+1 WHERE QuestType=@questType AND QuestInstance=@questInstance", new { state = (int)state, nowUtcTicks, questType, questInstance }, transaction);
                binding.State = state; binding.Version++;
                return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.Applied, Binding = binding };
            });
        }

        public GeneratedMissionResult AdvanceCleanup(int ownerId, int questType, int questInstance, long expectedVersion, long checkpointMask, long nowUtcTicks)
        {
            const long allCheckpoints = (1L << 17) - 1;
            if (ownerId <= 0 || expectedVersion <= 0 || checkpointMask < 0 || (checkpointMask & ~allCheckpoints) != 0 || nowUtcTicks <= 0)
                throw new ArgumentException("Invalid cleanup checkpoint.");
            return GeneratedTransaction(ownerId, (connection, transaction) =>
            {
                var binding = ReadGeneratedBinding(connection, transaction, ownerId, questType, questInstance);
                if (binding == null || binding.State == GeneratedMissionState.Active || binding.Version != expectedVersion || (checkpointMask & binding.CleanupCheckpoints) != binding.CleanupCheckpoints)
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Cleanup owner, lifecycle, revision or monotonicity mismatch.");
                if ((checkpointMask & (1L << 16)) != 0 && checkpointMask != allCheckpoints)
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Cleanup cannot release a playfield before every checkpoint.");
                connection.Execute("UPDATE generatedmissionbindings SET CleanupCheckpoints=@checkpointMask,ActivePlayfield=CASE WHEN @checkpointMask=@allCheckpoints THEN NULL ELSE ActivePlayfield END,UpdatedAtUtcTicks=@nowUtcTicks,Version=Version+1 WHERE QuestType=@questType AND QuestInstance=@questInstance", new { checkpointMask, allCheckpoints, nowUtcTicks, questType, questInstance }, transaction);
                binding.CleanupCheckpoints = checkpointMask; binding.Version++;
                return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.Applied, Binding = binding };
            });
        }

        private T GeneratedTransaction<T>(int ownerId, Func<IDbConnection, IDbTransaction, T> operation)
        {
            using (var connection = OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                T result;
                try
                {
                    if (ownerId > 0 && !connection.Query<int>("SELECT Id FROM characters WHERE Id=@ownerId FOR UPDATE", new { ownerId }, transaction).Any())
                        throw new InvalidOperationException("Mission owner does not exist.");
                    result = operation(connection, transaction);
                }
                catch (Exception operationFailure)
                {
                    RollbackAfterFailure(transaction, operationFailure);
                    throw;
                }
                try { transaction.Commit(); }
                catch (Exception exception) { throw new MissionCommitOutcomeUnknownException(exception); }
                return result;
            }
        }

        private const string ObjectColumns = "OwnerId,QuestType,QuestInstance,RuntimeType,RuntimeInstance,CapturedType,CapturedInstance,Kind,TemplateId,X,Y,Z,HeadingX,HeadingY,HeadingZ,HeadingW,CurrentHealth,MaxHealth,Level,IsDead,IsOpen,IsLocked,LootResolved,ObjectiveConsumed,DeathActorId,DiedAtUtcTicks,CorpseCredits,CorpseClaimed,CorpseExpiresAtUtcTicks,Version,UpdatedAtUtcTicks";

        public GeneratedMissionResult ClaimCorpseCredits(int ownerId, int questType, int questInstance, int runtimeNpcInstance, int currentCash, long nowUtcTicks)
            => GeneratedTransaction(ownerId, (connection, transaction) =>
            {
                var binding = ReadGeneratedBinding(connection, transaction, ownerId, questType, questInstance);
                if (binding == null || (binding.State != GeneratedMissionState.Active && binding.State != GeneratedMissionState.Completed)
                    || binding.CleanupCheckpoints != 0 || nowUtcTicks >= binding.ExpiresAtUtcTicks || currentCash < 0)
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Mission corpse binding is not accessible.");
                var corpse = connection.Query<GeneratedMissionObject>("SELECT " + ObjectColumns + " FROM generatedmissionobjects WHERE OwnerId=@ownerId AND QuestType=@questType AND QuestInstance=@questInstance AND RuntimeType=50000 AND RuntimeInstance=@runtimeNpcInstance FOR UPDATE", new { ownerId, questType, questInstance, runtimeNpcInstance }, transaction).SingleOrDefault();
                if (corpse == null || !corpse.IsDead || corpse.CorpseCredits < 21 || corpse.CorpseCredits > 87 || corpse.CorpseExpiresAtUtcTicks <= nowUtcTicks
                    || nowUtcTicks < corpse.DiedAtUtcTicks + 600 * System.TimeSpan.TicksPerMillisecond)
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Exact durable mission corpse is unavailable.");
                if (corpse.CorpseClaimed) return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.AlreadyApplied, Binding = binding };
                int cash = (int)System.Math.Min(int.MaxValue, (long)currentCash + corpse.CorpseCredits);
                WriteGeneratedStat(connection, transaction, 50000, ownerId, 61, cash);
                WriteGeneratedRewardLedger(connection, transaction, ownerId, GeneratedQuestKey(questType, questInstance), "corpse:" + runtimeNpcInstance.ToString(CultureInfo.InvariantCulture), "credits", corpse.CorpseCredits.ToString(CultureInfo.InvariantCulture), nowUtcTicks);
                connection.Execute("UPDATE generatedmissionobjects SET CorpseClaimed=1,LootResolved=1,Version=Version+1,UpdatedAtUtcTicks=@nowUtcTicks WHERE OwnerId=@ownerId AND QuestType=@questType AND QuestInstance=@questInstance AND RuntimeType=50000 AND RuntimeInstance=@runtimeNpcInstance", new { ownerId, questType, questInstance, runtimeNpcInstance, nowUtcTicks }, transaction);
                return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.Applied, Binding = binding, Cash = cash };
            });

        public GeneratedMissionResult SavePosition(int ownerId, int questType, int questInstance, int livePlayfield, float x, float y, float z, long nowUtcTicks)
        {
            if (!Finite(x) || !Finite(y) || !Finite(z) || nowUtcTicks <= 0) throw new ArgumentException("Finite owned mission position required.");
            return GeneratedTransaction(ownerId, (connection, transaction) =>
            {
                var binding = ReadGeneratedBinding(connection, transaction, ownerId, questType, questInstance);
                if (binding == null || binding.LivePlayfield != livePlayfield
                    || (binding.State != GeneratedMissionState.Active && binding.State != GeneratedMissionState.Completed)
                    || binding.CleanupCheckpoints != 0 || nowUtcTicks >= binding.ExpiresAtUtcTicks)
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Mission position ownership is no longer active.");
                // Existing characters location remains the sole durable player position. Do not
                // invoke the logout snapshot or overwrite Online, cash, XP or other live stats.
                connection.Execute("UPDATE characters SET Playfield=@livePlayfield,X=@x,Y=@y,Z=@z WHERE Id=@ownerId", new { livePlayfield, x, y, z, ownerId }, transaction);
                return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.Applied, Binding = binding };
            });
        }

        public IList<MissionItemInstanceData> ReadArtifacts(int ownerId, int questType, int questInstance)
        {
            if (ownerId <= 0) throw new ArgumentOutOfRangeException("ownerId");
            using (var connection = OpenConnection())
                return ReadGeneratedArtifacts(connection, null, ownerId, questType, questInstance);
        }

        public GeneratedMissionResult CleanupArtifacts(int ownerId, int questType, int questInstance, long nowUtcTicks)
            => GeneratedTransaction(ownerId, (connection, transaction) =>
            {
                var binding = ReadGeneratedBinding(connection, transaction, ownerId, questType, questInstance);
                if (binding == null || binding.State == GeneratedMissionState.Active || (binding.CleanupCheckpoints & 255) != 255)
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "World evacuation/removal checkpoints must precede artifact cleanup.");
                if ((binding.CleanupCheckpoints & 256) != 0)
                    return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.AlreadyApplied, Binding = binding };
                var items = ReadGeneratedArtifacts(connection, transaction, ownerId, questType, questInstance).Where(item => item.ContainerType != 0).ToArray();
                if (items.Any(item => !IsOwnedTopLevelArtifact(item, ownerId)))
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Mission artifact is outside verified owned top-level pages; retain pending cleanup until owned page reconciliation.");
                foreach (var item in items) RetireGeneratedCleanupArtifact(connection, transaction, item, ownerId);
                connection.Execute("UPDATE generatedmissionbindings SET CleanupCheckpoints=CleanupCheckpoints|256,Version=Version+1,UpdatedAtUtcTicks=@nowUtcTicks WHERE OwnerId=@ownerId AND QuestType=@questType AND QuestInstance=@questInstance", new { ownerId, questType, questInstance, nowUtcTicks }, transaction);
                binding.CleanupCheckpoints |= 256; binding.Version++;
                return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.Applied, Binding = binding };
            });

        private static IList<MissionItemInstanceData> ReadGeneratedArtifacts(IDbConnection connection, IDbTransaction transaction, int ownerId, int questType, int questInstance)
            => connection.Query<MissionItemInstanceData>("SELECT i.* FROM generatedmissionartifacts a JOIN item_instances i ON i.InstanceId=a.InstanceId WHERE a.OwnerId=@ownerId AND a.QuestType=@questType AND a.QuestInstance=@questInstance AND a.ArtifactRole IN (1,2,4) ORDER BY i.InstanceId", new { ownerId, questType, questInstance }, transaction).ToList();

        public IList<GeneratedMissionObject> ReadObjects(int ownerId, int questType, int questInstance)
        {
            if (ownerId <= 0) throw new ArgumentOutOfRangeException("ownerId");
            using (var connection = OpenConnection())
                return connection.Query<GeneratedMissionObject>("SELECT " + ObjectColumns + " FROM generatedmissionobjects WHERE OwnerId=@ownerId AND QuestType=@questType AND QuestInstance=@questInstance ORDER BY RuntimeType,RuntimeInstance", new { ownerId, questType, questInstance }).ToList();
        }

        public GeneratedMissionResult UpdateObjects(int ownerId, int questType, int questInstance, IList<GeneratedMissionObject> objects, long nowUtcTicks)
        {
            if (ownerId <= 0 || nowUtcTicks <= 0 || objects == null || objects.Count == 0) throw new ArgumentException("Owned world mutation required.");
            return GeneratedTransaction(ownerId, (connection, transaction) =>
            {
                var binding = ReadGeneratedBinding(connection, transaction, ownerId, questType, questInstance);
                if (binding == null || (binding.State != GeneratedMissionState.Active && binding.State != GeneratedMissionState.Completed)
                    || binding.CleanupCheckpoints != 0 || nowUtcTicks >= binding.ExpiresAtUtcTicks)
                    return GeneratedResult(GeneratedMissionResultStatus.Rejected, "Mission world is no longer active.");
                WriteObjects(connection, transaction, ownerId, questType, questInstance, objects, nowUtcTicks);
                return new GeneratedMissionResult { Status = GeneratedMissionResultStatus.Applied, Binding = binding };
            });
        }

        private static void WriteObjects(IDbConnection connection, IDbTransaction transaction, int ownerId, int questType, int questInstance, IList<GeneratedMissionObject> objects, long nowUtcTicks)
        {
                foreach (var value in objects)
                {
                    ValidateObject(value);
                    if (value.OwnerId != ownerId || value.QuestType != questType || value.QuestInstance != questInstance)
                        throw new ArgumentException("World mutation owner mismatch.");
                    var parameters = new DynamicParameters(value); parameters.Add("now", nowUtcTicks);
                    int affected = connection.Execute("UPDATE generatedmissionobjects SET X=@X,Y=@Y,Z=@Z,HeadingX=@HeadingX,HeadingY=@HeadingY,HeadingZ=@HeadingZ,HeadingW=@HeadingW,CurrentHealth=@CurrentHealth,IsDead=@IsDead,IsOpen=@IsOpen,IsLocked=@IsLocked,LootResolved=@LootResolved,ObjectiveConsumed=@ObjectiveConsumed,DeathActorId=@DeathActorId,DiedAtUtcTicks=@DiedAtUtcTicks,CorpseCredits=@CorpseCredits,CorpseExpiresAtUtcTicks=@CorpseExpiresAtUtcTicks,Version=Version+1,UpdatedAtUtcTicks=@now WHERE OwnerId=@OwnerId AND QuestType=@QuestType AND QuestInstance=@QuestInstance AND RuntimeType=@RuntimeType AND RuntimeInstance=@RuntimeInstance AND CapturedType=@CapturedType AND CapturedInstance=@CapturedInstance AND Kind=@Kind AND TemplateId=@TemplateId AND Version=@Version AND Level <=> @Level AND MaxHealth <=> @MaxHealth AND UpdatedAtUtcTicks<=@now AND (IsDead=0 OR @IsDead=1) AND (IsDead=0 OR (DeathActorId=@DeathActorId AND DiedAtUtcTicks=@DiedAtUtcTicks AND CorpseCredits=@CorpseCredits AND CorpseExpiresAtUtcTicks=@CorpseExpiresAtUtcTicks)) AND CorpseClaimed=@CorpseClaimed AND (LootResolved=0 OR @LootResolved=1) AND (ObjectiveConsumed=0 OR @ObjectiveConsumed=1)", parameters, transaction);
                    if (affected != 1) throw new InvalidOperationException("World object identity/revision or monotonic lifecycle conflict; complete batch rolled back.");
                }
        }

        private static void ValidateObject(GeneratedMissionObject value)
        {
            if (value == null || value.OwnerId <= 0 || value.QuestType != 0xDAC3 || value.QuestInstance <= 0 || value.RuntimeType <= 0 || value.RuntimeInstance <= 0
                || value.CapturedType <= 0 || value.CapturedInstance == 0 || value.Kind < 1 || value.Kind > 8 || value.TemplateId < 0 || value.Version <= 0
                || !Finite(value.X) || !Finite(value.Y) || !Finite(value.Z) || !Finite(value.HeadingX) || !Finite(value.HeadingY) || !Finite(value.HeadingZ) || !Finite(value.HeadingW)
                || value.Level <= 0 || value.CurrentHealth < 0 || value.MaxHealth <= 0 || (value.CurrentHealth.HasValue != value.MaxHealth.HasValue) || value.CurrentHealth > value.MaxHealth
                || (value.IsDead && value.CurrentHealth.HasValue && (value.CurrentHealth != 0 || value.DiedAtUtcTicks <= 0
                    || value.DeathActorId < 0 || value.CorpseCredits < 21 || value.CorpseCredits > 87
                    || value.CorpseExpiresAtUtcTicks - value.DiedAtUtcTicks != 60600 * System.TimeSpan.TicksPerMillisecond))
                || (!value.IsDead && (value.DiedAtUtcTicks != 0 || value.DeathActorId != 0 || value.CorpseCredits != 0 || value.CorpseClaimed || value.CorpseExpiresAtUtcTicks != 0)))
                throw new ArgumentException("Invalid typed mission object state.");
        }

        private static GeneratedMissionBinding ReadGeneratedBinding(IDbConnection connection, IDbTransaction transaction, int ownerId, int questType, int questInstance)
        {
            var binding = connection.Query<GeneratedMissionBinding>("SELECT " + BindingColumns + " FROM generatedmissionbindings WHERE OwnerId=@ownerId AND QuestType=@questType AND QuestInstance=@questInstance", new { ownerId, questType, questInstance }, transaction).SingleOrDefault();
            if (binding != null) HydrateOffer(connection, transaction, binding);
            return binding;
        }
        private static void HydrateOffer(IDbConnection connection, IDbTransaction transaction, GeneratedMissionBinding binding)
        {
            binding.Offer = ReadGeneratedOffer(connection, transaction, binding.OwnerId, binding.OfferType, binding.OfferInstance);
            if (binding.Offer == null) throw new InvalidOperationException("Accepted mission lost its frozen offer.");
            binding.MissionItem = connection.Query<MissionItemInstanceData>("SELECT i.* FROM generatedmissionartifacts a JOIN item_instances i ON i.InstanceId=a.InstanceId WHERE a.OwnerId=@OwnerId AND a.QuestType=@QuestType AND a.QuestInstance=@QuestInstance AND a.ArtifactRole IN (2,4)", binding, transaction).SingleOrDefault();
        }
        private static void RetireGeneratedItem(IDbConnection connection, IDbTransaction transaction, MissionItemInstanceData item, int ownerId)
        {
            if (item.ContainerInstance != ownerId || item.ContainerType != 104) throw new ArgumentException("Objective consumption requires the exact owned main-inventory row.");
            RetireGeneratedCleanupArtifact(connection, transaction, item, ownerId);
        }
        private static bool IsOwnedTopLevelArtifact(MissionItemInstanceData item, int ownerId)
            // IdentityType Weapon/Armor/Implant/Inventory/Bank/Social: exactly the
            // Legacy character Pages, not backpack interiors or another owner.
            => item.ContainerInstance == ownerId && (item.ContainerType == 101 || item.ContainerType == 102
                || item.ContainerType == 103 || item.ContainerType == 104 || item.ContainerType == 105 || item.ContainerType == 115);
        private static void RetireGeneratedCleanupArtifact(IDbConnection connection, IDbTransaction transaction, MissionItemInstanceData item, int ownerId)
        {
            if (!IsOwnedTopLevelArtifact(item, ownerId)) throw new ArgumentException("Cleanup requires the exact owned top-level inventory row.");
            if (connection.Execute("UPDATE item_instances SET ContainerType=0,ContainerPlacement=InstanceId WHERE InstanceId=@InstanceId AND ContainerType=@ContainerType AND ContainerInstance=@ContainerInstance AND ContainerPlacement=@ContainerPlacement AND ItemType=@ItemType AND LowId=@LowId AND HighId=@HighId AND Quality=@Quality AND StackCount=@StackCount AND Source=@Source", item, transaction) != 1)
                throw new InvalidOperationException("Bound artifact ownership/location/shape changed; complete objective transaction rolled back.");
        }
        private static GeneratedMissionOffer ReadGeneratedOffer(IDbConnection connection, IDbTransaction transaction, int ownerId, int offerType, int offerInstance)
        {
            var offer = connection.Query<GeneratedMissionOffer>(OfferReadSql + " WHERE o.OwnerId=@ownerId AND o.OfferType=@offerType AND o.OfferInstance=@offerInstance", new { ownerId, offerType, offerInstance }, transaction).SingleOrDefault();
            if (offer != null) ValidateFrozenOffer(offer);
            return offer;
        }
        private static void InsertGeneratedItem(IDbConnection connection, IDbTransaction transaction, MissionItemInstanceData item, int ownerId)
        {
            if (item == null || item.InstanceId <= 0 || item.ContainerInstance != ownerId || item.ContainerType <= 0 || item.ContainerPlacement < 0 || item.LowId <= 0 || item.HighId <= 0 || item.Quality <= 0 || item.StackCount <= 0)
                throw new ArgumentException("Invalid owned item-instance grant plan.");
            connection.Execute("INSERT INTO item_instances (InstanceId,ContainerType,ContainerInstance,ContainerPlacement,ItemType,LowId,HighId,Quality,StackCount,Source) VALUES (@InstanceId,@ContainerType,@ContainerInstance,@ContainerPlacement,@ItemType,@LowId,@HighId,@Quality,@StackCount,@Source)", item, transaction);
        }
        private static void WriteGeneratedArtifact(IDbConnection connection, IDbTransaction transaction, GeneratedMissionBinding binding, int instanceId, int role, long now)
            => connection.Execute("INSERT INTO generatedmissionartifacts (OwnerId,QuestType,QuestInstance,InstanceId,ArtifactRole,CreatedAtUtcTicks) VALUES (@OwnerId,@QuestType,@QuestInstance,@instanceId,@role,@now)", new { binding.OwnerId, binding.QuestType, binding.QuestInstance, instanceId, role, now }, transaction);
        private static void WriteGeneratedStat(IDbConnection connection, IDbTransaction transaction, int type, int ownerId, int statId, int value)
            => connection.Execute("INSERT INTO stats (Type,Instance,StatId,StatValue) VALUES (@type,@ownerId,@statId,@value) ON DUPLICATE KEY UPDATE StatValue=@value", new { type, ownerId, statId, value }, transaction);
        private static void WriteGeneratedRewardLedger(IDbConnection connection, IDbTransaction transaction, int ownerId, string questId, string rewardKey, string rewardType, string effect, long now)
            => connection.Execute("INSERT INTO missionrewardledger (CharacterId,QuestId,RewardKey,RewardType,Status,Attempts,EffectReference,AppliedAtUtcTicks,CreatedAtUtcTicks,UpdatedAtUtcTicks,Version) VALUES (@ownerId,@questId,@rewardKey,@rewardType,3,1,@effect,@now,@now,@now,1)", new { ownerId, questId, rewardKey, rewardType, effect, now }, transaction);
        private static string GeneratedQuestKey(int type, int instance) => "generated:" + type.ToString(CultureInfo.InvariantCulture) + ":" + instance.ToString(CultureInfo.InvariantCulture);
        private static void FreezeGeneratedToken(IDbConnection connection, IDbTransaction transaction, GeneratedMissionBinding binding, GeneratedMissionTokenClaim claim, long now)
        {
            if (claim == null || binding.CompletionFrozenAtUtcTicks != 0 || binding.TeamType != 0 || binding.TeamInstance != 0
                || claim.CharacterLevel < 1 || claim.CharacterLevel > 220 || claim.Side < 0 || claim.Side > 2)
                throw new InvalidOperationException("Exact solo completion token claim is required.");
            var ambient = connection.Query<GeneratedMissionObject>("SELECT " + ObjectColumns + " FROM generatedmissionobjects WHERE OwnerId=@OwnerId AND QuestType=@QuestType AND QuestInstance=@QuestInstance AND Kind=8", binding, transaction).ToArray();
            int killed = ambient.Count(value => value.IsDead && value.DeathActorId == binding.OwnerId && value.DiedAtUtcTicks > 0);
            int percent = ambient.Length == 0 ? 100 : (int)((long)killed * 100 / ambient.Length);
            int disposition = percent < 100 ? 0 : claim.Side == 0 ? 1 : 2;
            if (claim.ProgressPercent != percent || claim.Disposition != disposition || (disposition == 2 ? claim.Count <= 0 : claim.Count != 0))
                throw new InvalidOperationException("Token claim does not match the exact durable ambient-death population.");
            binding.TokenProgressPercent = percent; binding.TokenClaimLevel = claim.CharacterLevel; binding.TokenClaimSide = claim.Side;
            binding.TokenDisposition = disposition; binding.TokenCount = claim.Count; binding.CompletionFrozenAtUtcTicks = now;
            connection.Execute("UPDATE generatedmissionbindings SET TokenProgressPercent=@TokenProgressPercent,TokenClaimLevel=@TokenClaimLevel,TokenClaimSide=@TokenClaimSide,TokenDisposition=@TokenDisposition,TokenCount=@TokenCount,CompletionFrozenAtUtcTicks=@CompletionFrozenAtUtcTicks WHERE QuestType=@QuestType AND QuestInstance=@QuestInstance", binding, transaction);
        }
        private static GeneratedMissionResult GeneratedResult(GeneratedMissionResultStatus status, string reason) => new GeneratedMissionResult { Status = status, Reason = reason };
        private static void ValidateBatch(GeneratedMissionOfferBatch batch)
        {
            if (batch == null || batch.OwnerId <= 0 || batch.OwnerType != 50000 || string.IsNullOrWhiteSpace(batch.BatchIdentity) || batch.BatchIdentity.Length > 64
                || batch.Fee < 0 || batch.CurrentCash < 0 || batch.TerminalType != 0xDAC1 || batch.TerminalInstance <= 0 || batch.TerminalPlayfield <= 0 || batch.OfferedAtUtcTicks <= 0 || batch.ExpiresAtUtcTicks <= batch.OfferedAtUtcTicks
                || batch.Offers == null || batch.Offers.Count < 1 || batch.Offers.Count > 5)
                throw new ArgumentException("Invalid frozen mission offer batch.");
            for (int index = 0; index < batch.Offers.Count; index++)
            {
                var offer = batch.Offers[index]; ValidateFrozenOffer(offer);
                if (offer.OwnerId != batch.OwnerId || offer.BatchIdentity != batch.BatchIdentity || offer.OfferIndex != index || offer.State != GeneratedMissionState.Offered || offer.Version != 1
                    || offer.OfferedAtUtcTicks != batch.OfferedAtUtcTicks || offer.ExpiresAtUtcTicks != batch.ExpiresAtUtcTicks)
                    throw new ArgumentException("Offer does not belong to its immutable batch.");
            }
        }
        private static void ValidateFrozenOffer(GeneratedMissionOffer offer)
        {
            if (offer == null || offer.OwnerId <= 0 || offer.OfferType != 0xDAC3 || offer.OfferInstance <= 0 || offer.MissionType < 0 || offer.MissionType > 4 || offer.Quality <= 0
                || offer.DestinationType <= 0 || offer.DestinationInstance <= 0 || offer.DestinationPlayfield <= 0 || offer.EntranceType <= 0 || offer.EntranceInstance <= 0
                || offer.EntranceLow <= 0 || offer.EntranceHigh <= 0 || !Finite(offer.DestinationX) || !Finite(offer.DestinationY) || !Finite(offer.DestinationZ)
                || offer.CashReward < 0 || offer.ExperienceReward < 0 || offer.RewardCount < 0 || (offer.RewardCount > 0 && (offer.RewardLowId <= 0 || offer.RewardHighId <= 0 || offer.RewardQuality <= 0))
                || offer.FrozenWireBody == null || offer.FrozenWireBody.Length == 0 || offer.FrozenWireBody.Length > 1024 * 1024 || offer.Title == null || offer.Title.Length > 1024 || offer.Description == null)
                throw new ArgumentException("Frozen mission offer identity, destination, reward or projection is invalid.");
            using (var hash = SHA256.Create())
                if (BitConverter.ToString(hash.ComputeHash(offer.FrozenWireBody)).Replace("-", string.Empty).ToLowerInvariant() != offer.FrozenWireSha256)
                    throw new ArgumentException("Frozen mission wire projection hash mismatch.");
        }
        private static void ValidateAcceptance(GeneratedMissionAcceptance value)
        {
            if (value == null || value.OwnerId <= 0 || value.OfferType != 0xDAC3 || value.OfferInstance <= 0 || value.QuestType != 0xDAC3 || value.QuestInstance <= 0 || value.KeyInstance <= 0
                || (value.TeamType == 0) != (value.TeamInstance == 0) || value.TeamType < 0 || value.TeamInstance < 0 || string.IsNullOrWhiteSpace(value.BundleId) || value.BundleId.Length > 128
                || value.BundleSha256 == null || value.BundleSha256.Length != 64 || value.BundleSha256.Any(c => !(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) || value.BuildingType <= 0 || value.BuildingInstance <= 0 || value.LivePlayfield <= 0
                || value.ObjectiveType <= 0 || value.ObjectiveInstance <= 0 || value.ObjectiveTemplateId <= 0 || value.ObjectiveInteraction < 1 || value.ObjectiveInteraction > 5 || value.RequiredCount <= 0
                || value.AcceptedAtUtcTicks <= 0 || value.ExpiresAtUtcTicks - value.AcceptedAtUtcTicks != TimeSpan.TicksPerHour * 48 || value.Artifacts == null || value.Artifacts.Any(item => item == null) || value.Artifacts.Count(item => item.InstanceId == value.KeyInstance) != 1
                || value.Objects == null || value.Objects.Count == 0 || !value.Objects.Any(item => item.RuntimeType == value.ObjectiveType && item.RuntimeInstance == value.ObjectiveInstance && item.TemplateId == value.ObjectiveTemplateId))
                throw new ArgumentException("Incomplete accepted mission/key/ACG/objective binding.");
        }
        private static bool SameBatch(GeneratedMissionOfferBatch a, GeneratedMissionOfferBatch b)
            => a.OwnerId == b.OwnerId && a.OwnerType == b.OwnerType && a.BatchIdentity == b.BatchIdentity && a.RollSeed == b.RollSeed && a.ResponseNonce == b.ResponseNonce && a.Fee == b.Fee
                && a.TerminalType == b.TerminalType && a.TerminalInstance == b.TerminalInstance && a.TerminalPlayfield == b.TerminalPlayfield
                && a.LevelSlider == b.LevelSlider && a.GoodBadSlider == b.GoodBadSlider && a.OrderChaosSlider == b.OrderChaosSlider && a.OpenHiddenSlider == b.OpenHiddenSlider
                && a.PhysicalMysticalSlider == b.PhysicalMysticalSlider && a.HeadOnStealthSlider == b.HeadOnStealthSlider && a.MoneyExperienceSlider == b.MoneyExperienceSlider
                && a.OfferedAtUtcTicks == b.OfferedAtUtcTicks && a.ExpiresAtUtcTicks == b.ExpiresAtUtcTicks;
        private static bool SameOffer(GeneratedMissionOffer a, GeneratedMissionOffer b)
            => a.OwnerId == b.OwnerId && a.BatchIdentity == b.BatchIdentity && a.OfferIndex == b.OfferIndex && a.OfferType == b.OfferType && a.OfferInstance == b.OfferInstance
                && a.MissionType == b.MissionType && a.Quality == b.Quality && a.DestinationType == b.DestinationType && a.DestinationInstance == b.DestinationInstance
                && a.DestinationPlayfield == b.DestinationPlayfield && a.DestinationX.Equals(b.DestinationX) && a.DestinationY.Equals(b.DestinationY) && a.DestinationZ.Equals(b.DestinationZ)
                && a.EntranceType == b.EntranceType && a.EntranceInstance == b.EntranceInstance && a.EntranceLow == b.EntranceLow && a.EntranceHigh == b.EntranceHigh
                && a.CashReward == b.CashReward && a.ExperienceReward == b.ExperienceReward && a.RewardLowId == b.RewardLowId && a.RewardHighId == b.RewardHighId
                && a.RewardQuality == b.RewardQuality && a.RewardCount == b.RewardCount && a.Title == b.Title && a.Description == b.Description
                && a.FrozenWireSha256 == b.FrozenWireSha256 && a.FrozenWireBody.SequenceEqual(b.FrozenWireBody) && a.OfferedAtUtcTicks == b.OfferedAtUtcTicks && a.ExpiresAtUtcTicks == b.ExpiresAtUtcTicks;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static readonly HashSet<int> ProgressionStatIds = new HashSet<int>
        {
            (int)StatIds.xp, (int)StatIds.lastxp, (int)StatIds.lastsavexp, (int)StatIds.unsavedxp, (int)StatIds.nextxp,
            (int)StatIds.sk, (int)StatIds.nextsk, (int)StatIds.level, (int)StatIds.titlelevel, (int)StatIds.ip,
            (int)StatIds.life, (int)StatIds.health, (int)StatIds.maxnanoenergy, (int)StatIds.currentnano
        };
        private static void ValidateProgression(GeneratedMissionCompletion completion, GeneratedMissionOffer offer)
        {
            var stats = completion.ProgressionStats;
            if (completion.RequestedExperienceReward != offer.ExperienceReward || completion.FinalExperience < 0 || stats == null
                || completion.CurrentLevel < 1 || completion.CurrentLevel > 220
                || stats.Any(stat => stat == null || stat.StatIdentityType != completion.CharacterType || !ProgressionStatIds.Contains(stat.StatId) || stat.Value < 0 || stat.Value > int.MaxValue)
                || stats.Select(stat => stat.StatId).Distinct().Count() != stats.Count)
                throw new ArgumentException("Complete allowlisted direct-XP progression plan must match the frozen reward.");
            var xp = stats.SingleOrDefault(stat => stat.StatId == (int)StatIds.xp);
            bool shadow = stats.Any(stat => stat.StatId == (int)StatIds.sk);
            if ((xp != null && shadow) || (xp == null ? completion.FinalExperience != completion.CurrentExperience : xp.Value != completion.FinalExperience)
                || (offer.ExperienceReward > 0 && completion.CurrentLevel < 220 && xp == null && !shadow)
                || (completion.CurrentLevel == 220 && stats.Count != 0) || (offer.ExperienceReward == 0 && stats.Count != 0))
                throw new ArgumentException("Final experience and direct XP/SK projection are inconsistent.");
        }
        private static readonly string OfferReadSql = "SELECT " + string.Join(",", OfferColumns.Split(',').Select(column => "o." + column))
            + ",b.TerminalType AS IssuingTerminalType,b.TerminalInstance AS IssuingTerminalInstance,b.TerminalPlayfield AS IssuingTerminalPlayfield FROM generatedmissionoffers o JOIN generatedmissionbatches b ON b.OwnerId=o.OwnerId AND b.BatchIdentity=o.BatchIdentity";
        private sealed class GeneratedSequence { public int NextIdentity { get; set; } public int MaximumIdentity { get; set; } }
        private sealed class GeneratedBatchCash { public int CashAfter { get; set; } }
    }
}
