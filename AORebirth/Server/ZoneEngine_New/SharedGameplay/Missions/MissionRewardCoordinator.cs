namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    #endregion

    public enum MissionRewardExecutionStatus
    {
        Applied = 1,
        AlreadyApplied = 2,
        Busy = 3,
        RetryableFailure = 4,
        Rejected = 5,
        Unresolved = 6
    }

    public enum MissionRewardEffectStatus
    {
        Applied = 1,
        AlreadyApplied = 2,
        RetryableFailure = 3
    }

    public sealed class MissionRewardDefinition
    {
        public MissionRewardDefinition()
        {
            this.StatMutations = new MissionCharacterStatMutation[0];
        }

        public string RewardKey { get; set; }

        public string RewardType { get; set; }

        public bool IsResolved { get; set; }

        public IList<MissionCharacterStatMutation> StatMutations { get; set; }
    }

    public sealed class MissionRewardExecutionContext
    {
        public int CharacterId { get; set; }

        public string QuestId { get; set; }

        public string RewardKey { get; set; }

        public string RewardType { get; set; }

        public string ClaimToken { get; set; }

        public int Attempt { get; set; }

        public string PriorEffectReference { get; set; }
    }

    public sealed class MissionRewardEffectResult
    {
        public MissionRewardEffectStatus Status { get; set; }

        public string EffectReference { get; set; }

        public string Error { get; set; }

        public static MissionRewardEffectResult Applied(string effectReference)
        {
            return new MissionRewardEffectResult
                   {
                       Status = MissionRewardEffectStatus.Applied,
                       EffectReference = effectReference
                   };
        }

        public static MissionRewardEffectResult AlreadyApplied(string effectReference)
        {
            return new MissionRewardEffectResult
                   {
                       Status = MissionRewardEffectStatus.AlreadyApplied,
                       EffectReference = effectReference
                   };
        }

        public static MissionRewardEffectResult RetryableFailure(string error)
        {
            return new MissionRewardEffectResult
                   {
                       Status = MissionRewardEffectStatus.RetryableFailure,
                       Error = error
                   };
        }
    }

    public interface IMissionRewardEffect
    {
        MissionRewardEffectResult Apply(MissionRewardExecutionContext context);
    }

    public sealed class MissionRewardExecutionResult
    {
        public MissionRewardExecutionStatus Status { get; set; }

        public MissionRewardStageRecord Stage { get; set; }

        public IList<MissionCharacterStatValue> StatValues { get; set; }

        public string Message { get; set; }

        public bool Succeeded
        {
            get
            {
                return this.Status == MissionRewardExecutionStatus.Applied
                       || this.Status == MissionRewardExecutionStatus.AlreadyApplied;
            }
        }
    }

    /// <summary>
    /// Durable reward-stage coordinator. Character-stat rewards can be committed with the reward ledger in one
    /// repository transaction. External effects (such as inventory persistence) run between durable claim and
    /// durable completion; their adapters must be idempotent by the supplied stable reward/claim context.
    /// </summary>
    public sealed class MissionRewardCoordinator
    {
        private static readonly TimeSpan DefaultClaimLease = TimeSpan.FromMinutes(2);

        private readonly Func<string> claimTokenFactory;
        private readonly TimeSpan claimLease;
        private readonly IMissionRepository repository;
        private readonly Func<long> utcNowTicks;

        public MissionRewardCoordinator(IMissionRepository repository)
            : this(
                repository,
                () => DateTime.UtcNow.Ticks,
                () => Guid.NewGuid().ToString("N"),
                DefaultClaimLease)
        {
        }

        public MissionRewardCoordinator(
            IMissionRepository repository,
            Func<long> utcNowTicks,
            Func<string> claimTokenFactory,
            TimeSpan claimLease)
        {
            this.repository = repository ?? throw new ArgumentNullException("repository");
            this.utcNowTicks = utcNowTicks ?? throw new ArgumentNullException("utcNowTicks");
            this.claimTokenFactory = claimTokenFactory ?? throw new ArgumentNullException("claimTokenFactory");
            if (claimLease <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("claimLease");
            }

            this.claimLease = claimLease;
        }

        public MissionRewardExecutionResult ExecuteExternal(
            int characterId,
            string questId,
            MissionRewardDefinition definition,
            IMissionRewardEffect effect)
        {
            MissionRewardKey key;
            MissionRewardExecutionResult invalid = ResolveReward(
                characterId,
                questId,
                definition,
                out key);
            if (invalid != null)
            {
                return invalid;
            }

            if (effect == null)
            {
                return Result(MissionRewardExecutionStatus.Unresolved, null, null, "Reward effect is unresolved.");
            }

            long claimedAt = this.Now();
            string claimToken = this.claimTokenFactory();
            if (string.IsNullOrWhiteSpace(claimToken))
            {
                return Result(MissionRewardExecutionStatus.Unresolved, null, null, "Reward claim token is unresolved.");
            }

            long expiresAt = AddTicksClamped(claimedAt, this.claimLease.Ticks);
            MissionRewardClaimResult claim = this.repository.Execute(
                characterId,
                transaction => transaction.TryClaimReward(
                    key,
                    definition.RewardType,
                    claimToken,
                    claimedAt,
                    expiresAt));
            if (claim.Status == MissionRewardClaimStatus.AlreadyApplied)
            {
                return Result(MissionRewardExecutionStatus.AlreadyApplied, claim.Stage, null, claim.Message);
            }

            if (claim.Status == MissionRewardClaimStatus.Busy)
            {
                return Result(MissionRewardExecutionStatus.Busy, claim.Stage, null, claim.Message);
            }

            if (claim.Status != MissionRewardClaimStatus.Claimed || claim.Stage == null)
            {
                return Result(MissionRewardExecutionStatus.Rejected, claim.Stage, null, claim.Message);
            }

            MissionRewardEffectResult effectResult;
            try
            {
                effectResult = effect.Apply(
                    new MissionRewardExecutionContext
                    {
                        CharacterId = characterId,
                        QuestId = key.Mission.QuestId,
                        RewardKey = key.RewardKey,
                        RewardType = definition.RewardType,
                        ClaimToken = claimToken,
                        Attempt = claim.Stage.Attempts,
                        PriorEffectReference = claim.Stage.EffectReference
                    });
            }
            catch (Exception e)
            {
                effectResult = MissionRewardEffectResult.RetryableFailure(e.Message);
            }

            if (effectResult == null)
            {
                effectResult = MissionRewardEffectResult.RetryableFailure("Reward effect returned no result.");
            }

            long finishedAt = this.Now();
            if (effectResult.Status == MissionRewardEffectStatus.Applied
                || effectResult.Status == MissionRewardEffectStatus.AlreadyApplied)
            {
                MissionRewardStageRecord appliedStage = null;
                bool markedApplied = this.repository.Execute(
                    characterId,
                    transaction => transaction.TryMarkRewardApplied(
                        key,
                        claimToken,
                        claim.Stage.Version,
                        effectResult.EffectReference,
                        finishedAt,
                        out appliedStage));
                if (!markedApplied)
                {
                    return Result(
                        MissionRewardExecutionStatus.RetryableFailure,
                        appliedStage,
                        null,
                        "Reward effect succeeded but durable completion conflicted; retry requires an idempotent effect adapter.");
                }

                return Result(
                    effectResult.Status == MissionRewardEffectStatus.AlreadyApplied
                        ? MissionRewardExecutionStatus.AlreadyApplied
                        : MissionRewardExecutionStatus.Applied,
                    appliedStage,
                    null,
                    "Reward effect and durable stage completed.");
            }

            MissionRewardStageRecord failedStage = null;
            bool markedFailed = this.repository.Execute(
                characterId,
                transaction => transaction.TryMarkRewardFailed(
                    key,
                    claimToken,
                    claim.Stage.Version,
                    effectResult.Error,
                    finishedAt,
                    out failedStage));
            return Result(
                MissionRewardExecutionStatus.RetryableFailure,
                failedStage,
                null,
                markedFailed
                    ? "Reward effect failed and remains retryable."
                    : "Reward effect failed and durable failure recording conflicted.");
        }

        public MissionRewardExecutionResult ExecuteAtomicCharacterStats(
            int characterId,
            string questId,
            MissionRewardDefinition definition,
            string effectReference)
        {
            MissionRewardKey key;
            MissionRewardExecutionResult invalid = ResolveReward(
                characterId,
                questId,
                definition,
                out key);
            if (invalid != null)
            {
                return invalid;
            }

            if (definition.StatMutations == null || definition.StatMutations.Count == 0)
            {
                return Result(MissionRewardExecutionStatus.Unresolved, null, null, "Character-stat reward has no resolved mutations.");
            }

            MissionAtomicStatRewardResult result = this.repository.Execute(
                characterId,
                transaction => transaction.TryApplyCharacterStatReward(
                    key,
                    definition.RewardType,
                    definition.StatMutations,
                    effectReference,
                    this.Now()));
            if (result.Status == MissionAtomicRewardStatus.Applied)
            {
                return Result(MissionRewardExecutionStatus.Applied, result.Stage, result.StatValues, result.Message);
            }

            if (result.Status == MissionAtomicRewardStatus.AlreadyApplied)
            {
                return Result(MissionRewardExecutionStatus.AlreadyApplied, result.Stage, result.StatValues, result.Message);
            }

            return Result(MissionRewardExecutionStatus.Rejected, result.Stage, result.StatValues, result.Message);
        }

        private long Now()
        {
            long value = this.utcNowTicks();
            if (value <= 0)
            {
                throw new InvalidOperationException("Mission reward clock returned an invalid UTC tick value.");
            }

            return value;
        }

        private static MissionRewardExecutionResult ResolveReward(
            int characterId,
            string questId,
            MissionRewardDefinition definition,
            out MissionRewardKey key)
        {
            if (characterId <= 0 || string.IsNullOrWhiteSpace(questId))
            {
                key = default(MissionRewardKey);
                return Result(MissionRewardExecutionStatus.Unresolved, null, null, "Stable character and quest identities are required.");
            }

            if (definition == null || !definition.IsResolved || string.IsNullOrWhiteSpace(definition.RewardKey)
                || string.IsNullOrWhiteSpace(definition.RewardType))
            {
                key = default(MissionRewardKey);
                return Result(MissionRewardExecutionStatus.Unresolved, null, null, "Reward definition is unresolved.");
            }

            key = new MissionRewardKey(
                new MissionKey(characterId, questId),
                definition.RewardKey);
            return null;
        }

        private static MissionRewardExecutionResult Result(
            MissionRewardExecutionStatus status,
            MissionRewardStageRecord stage,
            IList<MissionCharacterStatValue> statValues,
            string message)
        {
            return new MissionRewardExecutionResult
                   {
                       Status = status,
                       Stage = stage == null ? null : stage.Clone(),
                       StatValues = statValues ?? new List<MissionCharacterStatValue>(),
                       Message = message
                   };
        }

        private static long AddTicksClamped(long value, long delta)
        {
            return value > long.MaxValue - delta ? long.MaxValue : value + delta;
        }
    }
}
