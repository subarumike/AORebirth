namespace ZoneEngine.Core;

public interface IDamageRandomSource
{
	int NextInclusive(int minimumInclusive, int maximumInclusive);

	bool NextChance(int chanceBasisPoints);
}
