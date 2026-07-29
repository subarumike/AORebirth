using System;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyTransientNanoEffectState
{
	internal Identity RecipientIdentity { get; set; }

	internal int NanoId { get; set; }

	internal int Strain { get; set; }

	internal int ModifierDelta { get; set; }

	internal int[] StatIds { get; set; }

	internal int CasterInstance { get; set; }

	internal int ActiveNanoKey { get; set; }

	internal DateTime ExpiresAtUtc { get; set; }

	internal int PeriodicStatId { get; set; }

	internal int PeriodicStatDelta { get; set; }

	internal OrdinaryEnemyPeriodicNanoSchedule PeriodicSchedule { get; set; }
}
