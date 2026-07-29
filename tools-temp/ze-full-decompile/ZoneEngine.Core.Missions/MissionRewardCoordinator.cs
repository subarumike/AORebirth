using System;
using System.Collections.Generic;

namespace ZoneEngine.Core.Missions;

public sealed class MissionRewardCoordinator
{
	private static readonly TimeSpan DefaultClaimLease = TimeSpan.FromMinutes(2.0);

	private readonly Func<string> claimTokenFactory;

	private readonly TimeSpan claimLease;

	private readonly IMissionRepository repository;

	private readonly Func<long> utcNowTicks;

	public MissionRewardCoordinator(IMissionRepository repository)
		: this(repository, () => DateTime.UtcNow.Ticks, () => Guid.NewGuid().ToString("N"), DefaultClaimLease)
	{
	}

	public MissionRewardCoordinator(IMissionRepository repository, Func<long> utcNowTicks, Func<string> claimTokenFactory, TimeSpan claimLease)
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

	public MissionRewardExecutionResult ExecuteExternal(int characterId, string questId, MissionRewardDefinition definition, IMissionRewardEffect effect)
	{
		MissionRewardKey key;
		MissionRewardExecutionResult missionRewardExecutionResult = ResolveReward(characterId, questId, definition, out key);
		if (missionRewardExecutionResult != null)
		{
			return missionRewardExecutionResult;
		}
		if (effect == null)
		{
			return Result(MissionRewardExecutionStatus.Unresolved, null, null, "Reward effect is unresolved.");
		}
		long claimedAt = Now();
		string claimToken = claimTokenFactory();
		if (string.IsNullOrWhiteSpace(claimToken))
		{
			return Result(MissionRewardExecutionStatus.Unresolved, null, null, "Reward claim token is unresolved.");
		}
		long expiresAt = AddTicksClamped(claimedAt, claimLease.Ticks);
		MissionRewardClaimResult claim = repository.Execute(characterId, (IMissionRepositoryTransaction transaction) => transaction.TryClaimReward(key, definition.RewardType, claimToken, claimedAt, expiresAt));
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
			effectResult = effect.Apply(new MissionRewardExecutionContext
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
		catch (Exception ex)
		{
			effectResult = MissionRewardEffectResult.RetryableFailure(ex.Message);
		}
		if (effectResult == null)
		{
			effectResult = MissionRewardEffectResult.RetryableFailure("Reward effect returned no result.");
		}
		long finishedAt = Now();
		if (effectResult.Status == MissionRewardEffectStatus.Applied || effectResult.Status == MissionRewardEffectStatus.AlreadyApplied)
		{
			MissionRewardStageRecord appliedStage = null;
			if (!repository.Execute(characterId, (IMissionRepositoryTransaction transaction) => transaction.TryMarkRewardApplied(key, claimToken, claim.Stage.Version, effectResult.EffectReference, finishedAt, out appliedStage)))
			{
				return Result(MissionRewardExecutionStatus.RetryableFailure, appliedStage, null, "Reward effect succeeded but durable completion conflicted; retry requires an idempotent effect adapter.");
			}
			return Result((effectResult.Status != MissionRewardEffectStatus.AlreadyApplied) ? MissionRewardExecutionStatus.Applied : MissionRewardExecutionStatus.AlreadyApplied, appliedStage, null, "Reward effect and durable stage completed.");
		}
		MissionRewardStageRecord failedStage = null;
		bool flag = repository.Execute(characterId, (IMissionRepositoryTransaction transaction) => transaction.TryMarkRewardFailed(key, claimToken, claim.Stage.Version, effectResult.Error, finishedAt, out failedStage));
		return Result(MissionRewardExecutionStatus.RetryableFailure, failedStage, null, flag ? "Reward effect failed and remains retryable." : "Reward effect failed and durable failure recording conflicted.");
	}

	public MissionRewardExecutionResult ExecuteAtomicCharacterStats(int characterId, string questId, MissionRewardDefinition definition, string effectReference)
	{
		MissionRewardKey key;
		MissionRewardExecutionResult missionRewardExecutionResult = ResolveReward(characterId, questId, definition, out key);
		if (missionRewardExecutionResult != null)
		{
			return missionRewardExecutionResult;
		}
		if (definition.StatMutations == null || definition.StatMutations.Count == 0)
		{
			return Result(MissionRewardExecutionStatus.Unresolved, null, null, "Character-stat reward has no resolved mutations.");
		}
		MissionAtomicStatRewardResult missionAtomicStatRewardResult = repository.Execute(characterId, (IMissionRepositoryTransaction transaction) => transaction.TryApplyCharacterStatReward(key, definition.RewardType, definition.StatMutations, effectReference, Now()));
		if (missionAtomicStatRewardResult.Status == MissionAtomicRewardStatus.Applied)
		{
			return Result(MissionRewardExecutionStatus.Applied, missionAtomicStatRewardResult.Stage, missionAtomicStatRewardResult.StatValues, missionAtomicStatRewardResult.Message);
		}
		if (missionAtomicStatRewardResult.Status == MissionAtomicRewardStatus.AlreadyApplied)
		{
			return Result(MissionRewardExecutionStatus.AlreadyApplied, missionAtomicStatRewardResult.Stage, missionAtomicStatRewardResult.StatValues, missionAtomicStatRewardResult.Message);
		}
		return Result(MissionRewardExecutionStatus.Rejected, missionAtomicStatRewardResult.Stage, missionAtomicStatRewardResult.StatValues, missionAtomicStatRewardResult.Message);
	}

	private long Now()
	{
		long num = utcNowTicks();
		if (num <= 0)
		{
			throw new InvalidOperationException("Mission reward clock returned an invalid UTC tick value.");
		}
		return num;
	}

	private static MissionRewardExecutionResult ResolveReward(int characterId, string questId, MissionRewardDefinition definition, out MissionRewardKey key)
	{
		if (characterId <= 0 || string.IsNullOrWhiteSpace(questId))
		{
			key = default(MissionRewardKey);
			return Result(MissionRewardExecutionStatus.Unresolved, null, null, "Stable character and quest identities are required.");
		}
		if (definition == null || !definition.IsResolved || string.IsNullOrWhiteSpace(definition.RewardKey) || string.IsNullOrWhiteSpace(definition.RewardType))
		{
			key = default(MissionRewardKey);
			return Result(MissionRewardExecutionStatus.Unresolved, null, null, "Reward definition is unresolved.");
		}
		key = new MissionRewardKey(new MissionKey(characterId, questId), definition.RewardKey);
		return null;
	}

	private static MissionRewardExecutionResult Result(MissionRewardExecutionStatus status, MissionRewardStageRecord stage, IList<MissionCharacterStatValue> statValues, string message)
	{
		return new MissionRewardExecutionResult
		{
			Status = status,
			Stage = stage?.Clone(),
			StatValues = (statValues ?? new List<MissionCharacterStatValue>()),
			Message = message
		};
	}

	private static long AddTicksClamped(long value, long delta)
	{
		return (value > long.MaxValue - delta) ? long.MaxValue : (value + delta);
	}
}
