using System.Collections.Generic;

namespace ZoneEngine.Core;

public sealed class WeaponDamageObservationImportResult
{
	public bool Success { get; set; }

	public WeaponDamageObservation Observation { get; set; }

	public IList<string> Diagnostics { get; private set; }

	public WeaponDamageObservationImportResult()
	{
		Diagnostics = new List<string>();
	}
}
