using System;

namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyPeriodicNanoSchedule
{
	internal DateTime ExpiresAtUtc { get; private set; }

	internal DateTime NextTickAtUtc { get; private set; }

	internal int RemainingTicks { get; private set; }

	internal double TickSeconds { get; private set; }

	internal OrdinaryEnemyPeriodicNanoSchedule(OrdinaryEnemySupportNanoProfile profile, DateTime appliedAtUtc)
	{
		Refresh(profile, appliedAtUtc);
	}

	internal void Refresh(OrdinaryEnemySupportNanoProfile profile, DateTime appliedAtUtc)
	{
		if (profile == null || !profile.HasPeriodicStatHit || profile.PeriodicTickCount <= 0 || profile.PeriodicTickSeconds <= 0.0 || profile.EffectLifetimeSeconds <= 0.0)
		{
			throw new InvalidOperationException("Periodic support nano profile is incomplete.");
		}
		ExpiresAtUtc = appliedAtUtc.AddSeconds(profile.EffectLifetimeSeconds);
		TickSeconds = profile.PeriodicTickSeconds;
		RemainingTicks = profile.PeriodicTickCount - 1;
		NextTickAtUtc = appliedAtUtc.AddSeconds(TickSeconds);
	}

	internal int ConsumeDueTicks(DateTime utcNow)
	{
		int num = 0;
		while (RemainingTicks > 0 && NextTickAtUtc <= utcNow && NextTickAtUtc < ExpiresAtUtc)
		{
			num++;
			RemainingTicks--;
			NextTickAtUtc = NextTickAtUtc.AddSeconds(TickSeconds);
		}
		return num;
	}
}
