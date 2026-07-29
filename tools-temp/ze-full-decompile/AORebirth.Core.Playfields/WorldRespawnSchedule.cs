using System;

namespace AORebirth.Core.Playfields;

internal sealed class WorldRespawnSchedule
{
	internal string SpawnKey { get; set; }

	internal string GroupKey { get; set; }

	internal int PlayfieldId { get; set; }

	internal DateTime DueAtUtc { get; set; }

	internal int Generation { get; set; }
}
