namespace AORebirth.Core.Playfields;

internal sealed class CapturedEnemySpecialAttackDefinition
{
	internal int LowTemplate { get; private set; }

	internal int HighTemplate { get; private set; }

	internal int Tag { get; private set; }

	internal string Name { get; private set; }

	internal CapturedEnemySpecialAttackDefinition(int lowTemplate, int highTemplate, int tag, string name)
	{
		LowTemplate = lowTemplate;
		HighTemplate = highTemplate;
		Tag = tag;
		Name = name;
	}
}
