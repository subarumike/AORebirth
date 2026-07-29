using System;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemySupportNanoRuntimeState
{
	internal DateTime NextCastAtUtc { get; set; }

	internal bool CastInProgress { get; set; }

	internal Identity TargetIdentity { get; set; }

	internal DateTime FinishAtUtc { get; set; }
}
