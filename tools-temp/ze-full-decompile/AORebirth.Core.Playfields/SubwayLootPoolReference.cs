using System;

namespace AORebirth.Core.Playfields;

internal sealed class SubwayLootPoolReference
{
	internal string Key { get; private set; }

	internal SubwayLootPoolKind Kind { get; private set; }

	internal SubwayLootPoolReference(string key, SubwayLootPoolKind kind)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("Pool key is required.", "key");
		}
		Key = key;
		Kind = kind;
	}
}
