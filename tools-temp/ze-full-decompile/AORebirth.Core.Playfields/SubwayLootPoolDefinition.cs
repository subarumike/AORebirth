using System;
using System.Collections.Generic;

namespace AORebirth.Core.Playfields;

internal sealed class SubwayLootPoolDefinition
{
	internal string Key { get; private set; }

	internal SubwayLootPoolKind Kind { get; private set; }

	internal int EmptyWeight { get; private set; }

	internal SubwayLootPoolCandidate[] Candidates { get; private set; }

	internal SubwayLootPoolDefinition(string key, SubwayLootPoolKind kind, int emptyWeight, SubwayLootPoolCandidate[] candidates)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("Pool key is required.", "key");
		}
		if (emptyWeight < 0)
		{
			throw new ArgumentOutOfRangeException("emptyWeight");
		}
		SubwayLootPoolCandidate[] array = candidates ?? new SubwayLootPoolCandidate[0];
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		SubwayLootPoolCandidate[] array2 = array;
		foreach (SubwayLootPoolCandidate subwayLootPoolCandidate in array2)
		{
			if (subwayLootPoolCandidate == null)
			{
				throw new ArgumentException("Pool candidates cannot contain null.", "candidates");
			}
			if (!hashSet.Add(subwayLootPoolCandidate.CandidateKey))
			{
				throw new ArgumentException("Duplicate pool candidate key: " + subwayLootPoolCandidate.CandidateKey, "candidates");
			}
		}
		Key = key;
		Kind = kind;
		EmptyWeight = emptyWeight;
		Candidates = new SubwayLootPoolCandidate[array.Length];
		Array.Copy(array, Candidates, array.Length);
	}
}
