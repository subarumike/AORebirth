using System;
using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace AORebirth.Core.Playfields;

internal static class WorldPopulationDefinitionValidator
{
	internal static void Validate(IEnumerable<WorldSpawnDefinition> spawns, IEnumerable<SpawnGroupDefinition> groups, IEnumerable<RespawnPolicyDefinition> policies, IEnumerable<string> profileKeys)
	{
		//IL_028f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		WorldSpawnDefinition[] array = (spawns ?? Enumerable.Empty<WorldSpawnDefinition>()).OrderBy((WorldSpawnDefinition x) => x.SpawnKey, StringComparer.Ordinal).ToArray();
		SpawnGroupDefinition[] array2 = (groups ?? Enumerable.Empty<SpawnGroupDefinition>()).OrderBy((SpawnGroupDefinition x) => x.SpawnGroupKey, StringComparer.Ordinal).ToArray();
		RespawnPolicyDefinition[] array3 = (policies ?? Enumerable.Empty<RespawnPolicyDefinition>()).OrderBy((RespawnPolicyDefinition x) => x.RespawnPolicyKey, StringComparer.Ordinal).ToArray();
		HashSet<string> hashSet = new HashSet<string>(profileKeys ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
		RejectDuplicates(array.Select((WorldSpawnDefinition x) => x.SpawnKey), "spawn key");
		RejectDuplicates(array2.Select((SpawnGroupDefinition x) => x.SpawnGroupKey), "group key");
		RejectDuplicates(array3.Select((RespawnPolicyDefinition x) => x.RespawnPolicyKey), "respawn policy key");
		RejectDuplicates(array.Select(delegate(WorldSpawnDefinition x)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			Identity configuredIdentity2 = x.ConfiguredIdentity;
			return ((Identity)(ref configuredIdentity2)).Instance.ToString();
		}), "configured identity");
		HashSet<string> hashSet2 = new HashSet<string>(array3.Select((RespawnPolicyDefinition x) => x.RespawnPolicyKey), StringComparer.Ordinal);
		HashSet<string> spawnKeys = new HashSet<string>(array.Select((WorldSpawnDefinition x) => x.SpawnKey), StringComparer.Ordinal);
		RespawnPolicyDefinition[] array4 = array3;
		foreach (RespawnPolicyDefinition respawnPolicyDefinition in array4)
		{
			if (!WorldRespawnPolicyValidator.IsValid(respawnPolicyDefinition))
			{
				throw new InvalidOperationException("Invalid respawn policy: " + respawnPolicyDefinition.RespawnPolicyKey);
			}
		}
		WorldSpawnDefinition[] array5 = array;
		int num = 0;
		while (num < array5.Length)
		{
			WorldSpawnDefinition worldSpawnDefinition = array5[num];
			if (!string.IsNullOrWhiteSpace(worldSpawnDefinition.SpawnKey) && hashSet.Contains(worldSpawnDefinition.EnemyProfileKey) && hashSet2.Contains(worldSpawnDefinition.RespawnPolicyKey) && worldSpawnDefinition.PlayfieldId > 0)
			{
				Identity configuredIdentity = worldSpawnDefinition.ConfiguredIdentity;
				if (((Identity)(ref configuredIdentity)).Instance > 0 && Finite(worldSpawnDefinition.X) && Finite(worldSpawnDefinition.Y) && Finite(worldSpawnDefinition.Z) && Finite(worldSpawnDefinition.OrientationX) && Finite(worldSpawnDefinition.OrientationY) && Finite(worldSpawnDefinition.OrientationZ) && Finite(worldSpawnDefinition.OrientationW) && Enum.IsDefined(typeof(WorldPopulationClassification), worldSpawnDefinition.Classification) && worldSpawnDefinition.Classification != 0 && !worldSpawnDefinition.OwnedSummon && !worldSpawnDefinition.BossOrScripted && (worldSpawnDefinition.Enabled || worldSpawnDefinition.Quarantined || worldSpawnDefinition.ActivationPolicy != WorldSpawnActivationPolicy.PlayfieldStart))
				{
					num++;
					continue;
				}
			}
			throw new InvalidOperationException("Invalid world spawn definition: " + worldSpawnDefinition.SpawnKey);
		}
		SpawnGroupDefinition[] array6 = array2;
		foreach (SpawnGroupDefinition spawnGroupDefinition in array6)
		{
			if (string.IsNullOrWhiteSpace(spawnGroupDefinition.SpawnGroupKey) || spawnGroupDefinition.PlayfieldId <= 0 || spawnGroupDefinition.MinimumAlive < 0 || spawnGroupDefinition.MaximumAlive < spawnGroupDefinition.MinimumAlive || (!string.IsNullOrWhiteSpace(spawnGroupDefinition.SharedRespawnPolicyKey) && !hashSet2.Contains(spawnGroupDefinition.SharedRespawnPolicyKey)) || (spawnGroupDefinition.SpawnKeys ?? new string[0]).Any((string x) => !spawnKeys.Contains(x)))
			{
				throw new InvalidOperationException("Invalid spawn group: " + spawnGroupDefinition.SpawnGroupKey);
			}
		}
	}

	private static bool Finite(float value)
	{
		return !float.IsNaN(value) && !float.IsInfinity(value);
	}

	private static void RejectDuplicates(IEnumerable<string> values, string label)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (string value in values)
		{
			if (string.IsNullOrWhiteSpace(value) || !hashSet.Add(value))
			{
				throw new InvalidOperationException("Duplicate or missing " + label + ": " + value);
			}
		}
	}
}
