using System.Collections.Generic;

namespace ZoneEngine.Core.Missions;

internal static class MissionKeyStore
{
	private static readonly object Sync = new object();

	private static readonly Dictionary<int, List<int>> KeysByCharacter = new Dictionary<int, List<int>>();

	public static void Register(int characterInstance, int keyInstance)
	{
		lock (Sync)
		{
			if (!KeysByCharacter.TryGetValue(characterInstance, out var value) || value == null)
			{
				value = new List<int>();
				KeysByCharacter[characterInstance] = value;
			}
			value.Add(keyInstance);
		}
	}

	public static bool TryTakeLatest(int characterInstance, out int keyInstance)
	{
		keyInstance = 0;
		lock (Sync)
		{
			if (!KeysByCharacter.TryGetValue(characterInstance, out var value) || value == null || value.Count == 0)
			{
				return false;
			}
			int index = value.Count - 1;
			keyInstance = value[index];
			value.RemoveAt(index);
			return true;
		}
	}
}
