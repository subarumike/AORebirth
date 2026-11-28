namespace AORebirth.Tools.MissionDaoValidation
{
    using System;
    using System.Linq;
    using AORebirth.Database.Domain.Missions;
    using AORebirth.Database.Domain.Quests;
    using AORebirth.Interfaces.Persistence.Missions;
    using MySqlConnector;
    using SmokeLounge.AOtomation.Messaging.GameData;

    internal static partial class Program
    {
        private static void ValidateRewardResearchDurability(MySqlMissionDao dao, string connectionString)
        {
            const string ProgressQuest = "completion.research-progress";
            const string FinishQuest = "completion.research-finish";
            const int Tier = 73001;
            const int NextTier = 73002;

            var quests = new MySqlCharacterQuestDao(() => new MySqlConnection(connectionString));
            SeedCompletionQuest(quests, ProgressQuest);
            ResetCompletionStats(connectionString);
            dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                .TryEarnCharacterQuest(EarnRequest(ProgressQuest)));

            var progress = CompleteRequest(ProgressQuest, CompletionItem(920040, 90));
            AddRewardStat(progress, CharacterStat.DeathXpPool, 0, 50);
            AddRewardStat(progress, CharacterStat.UnsavedXP, 0, 50);
            progress.ResearchMutation = new MissionResearchMutationData
            {
                ResearchId = Tier, ExpectedRemainingXp = 100, RemainingXp = 25
            };
            Expect<InvalidOperationException>(() => dao.Execute(CompletionOwner, tx =>
                    ((ICharacterQuestCompletionTransaction)tx).TryCompleteCharacterQuest(progress)),
                "quest-research-stale-base-rolls-back");
            Require(ReadCompletionStat(connectionString, CharacterStat.Cash) == 100
                && Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=920040") == 0
                && Scalar(connectionString, "SELECT Status FROM missionrewardledger WHERE QuestId='completion.research-progress'") == 1,
                "quest-research-rejection-leaves-primary-reward-pending");

            progress.ResearchMutation.ExpectedRemainingXp = null;
            var beforeCommit = new MySqlMissionDao(() => new FaultConnection(connectionString) { FailurePoint = "commit-before" });
            Expect<MissionCommitOutcomeUnknownException>(() => beforeCommit.Execute(CompletionOwner, tx =>
                    ((ICharacterQuestCompletionTransaction)tx).TryCompleteCharacterQuest(progress)),
                "quest-research-commit-before-fault-reported");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM characterresearch WHERE ResearchId=73001 AND CharacterId=103") == 0,
                "quest-research-commit-before-fault-no-partial-research");
            Require(dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                    .TryCompleteCharacterQuest(progress)).Status == CharacterQuestCompletionStatus.Applied
                && Scalar(connectionString, "SELECT RemainingXp FROM characterresearch WHERE ResearchId=73001 AND CharacterId=103") == 25
                && ReadCompletionStat(connectionString, CharacterStat.DeathXpPool) == 50
                && ReadCompletionStat(connectionString, CharacterStat.UnsavedXP) == 50,
                "quest-research-and-death-pool-applied-in-one-reward");

            SeedCompletionQuest(quests, FinishQuest);
            ResetCompletionStats(connectionString);
            dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                .TryEarnCharacterQuest(EarnRequest(FinishQuest)));
            var finish = CompleteRequest(FinishQuest);
            AddRewardStat(finish, CharacterStat.PersonalResearchGoal, 0, NextTier);
            finish.ResearchMutation = new MissionResearchMutationData
            {
                ResearchId = Tier, ExpectedRemainingXp = 25, RemainingXp = 0
            };
            var afterCommit = new MySqlMissionDao(() => new FaultConnection(connectionString) { FailurePoint = "commit-after" });
            Expect<MissionCommitOutcomeUnknownException>(() => afterCommit.Execute(CompletionOwner, tx =>
                    ((ICharacterQuestCompletionTransaction)tx).TryCompleteCharacterQuest(finish)),
                "quest-research-commit-after-fault-reported");
            Require(dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                    .TryCompleteCharacterQuest(finish)).Status == CharacterQuestCompletionStatus.AlreadyApplied,
                "quest-research-retry-rereads-completed-reward");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM charactersperks WHERE PacketId=73001 AND CharacterId=103") == 1
                && Scalar(connectionString, "SELECT COUNT(*) FROM characterresearch WHERE ResearchId=73001 AND CharacterId=103") == 0
                && ReadCompletionStat(connectionString, CharacterStat.PersonalResearchGoal) == NextTier,
                "quest-research-tier-perk-and-next-goal-persisted-exactly-once");
        }

        private static void AddRewardStat(CharacterQuestCompletionRequest request, CharacterStat stat, int expected, int value)
        {
            request.StatMutations = request.StatMutations.Concat(new[] { CompletionStat(stat, value) }).ToArray();
            request.ExpectedStatValues = request.ExpectedStatValues.Concat(new[]
            {
                new MissionStatValueData { StatIdentityType = 50000, StatId = (int)stat, Value = expected }
            }).ToArray();
        }
    }
}
