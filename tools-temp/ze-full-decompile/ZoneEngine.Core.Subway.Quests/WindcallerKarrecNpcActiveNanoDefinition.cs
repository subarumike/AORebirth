namespace ZoneEngine.Core.Subway.Quests;

internal sealed class WindcallerKarrecNpcActiveNanoDefinition
{
	internal int NanoIdentityType { get; private set; }

	internal int NanoIdentityInstance { get; private set; }

	internal int NanoInstance { get; private set; }

	internal int Time1 { get; private set; }

	internal int Time2 { get; private set; }

	internal WindcallerKarrecNpcActiveNanoDefinition(int nanoIdentityType, int nanoIdentityInstance, int nanoInstance, int time1, int time2)
	{
		NanoIdentityType = nanoIdentityType;
		NanoIdentityInstance = nanoIdentityInstance;
		NanoInstance = nanoInstance;
		Time1 = time1;
		Time2 = time2;
	}
}
