namespace ZoneEngine.Core;

public interface IDamageAttackStrategy
{
	string Name { get; }

	DamageCalculationResult Calculate(DamageCalculationRequest request, IDamageRandomSource randomSource);
}
