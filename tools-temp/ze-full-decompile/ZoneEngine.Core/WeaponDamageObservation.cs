using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ZoneEngine.Core;

public sealed class WeaponDamageObservation
{
	public WeaponDamageObservationSource Source { get; private set; }

	public WeaponDamageObservationInput Input { get; private set; }

	public WeaponDamageObservationResult Result { get; private set; }

	public ReadOnlyCollection<WeaponDamageObservationIssue> Issues { get; private set; }

	public WeaponDamageObservationValidationStatus ValidationStatus { get; private set; }

	public bool IsComplete => ValidationStatus == WeaponDamageObservationValidationStatus.Complete;

	internal WeaponDamageObservation(WeaponDamageObservationSource source, WeaponDamageObservationInput input, WeaponDamageObservationResult result, IEnumerable<WeaponDamageObservationIssue> issues, WeaponDamageObservationValidationStatus status)
	{
		Source = source;
		Input = input;
		Result = result;
		Issues = new ReadOnlyCollection<WeaponDamageObservationIssue>((issues ?? new WeaponDamageObservationIssue[0]).ToList());
		ValidationStatus = status;
	}
}
