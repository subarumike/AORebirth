using System;
using System.Collections.Generic;
using AORebirth.Database.Dao;

namespace ZoneEngine.Core;

internal static class PetMobTemplateResolver
{
	private static readonly Dictionary<string, string> PrefixFallbacks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ "PT50", "A120" },
		{ "PT51", "A020" },
		{ "PT52", "A120" },
		{ "PT53", "A120" },
		{ "PT54", "A020" },
		{ "PT55", "A120" },
		{ "PT56", "A120" }
	};

	private static readonly Dictionary<string, string> SoothingSpiritsHashFallbacks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ "LYNX", "MT02" },
		{ "JBOB", "MT02" },
		{ "DKEL", "MT02" },
		{ "QRMT", "MT02" },
		{ "MNKW", "MT02" },
		{ "RHEF", "MT02" }
	};

	public static string Resolve(string petHash)
	{
		return Resolve(petHash, null);
	}

	public static string Resolve(string petHash, string preferredBaseHash)
	{
		if (string.IsNullOrWhiteSpace(petHash))
		{
			return null;
		}
		if (Dao<DBMobTemplate, MobTemplateDao>.Instance.GetMobTemplateByHash(petHash) != null)
		{
			return petHash;
		}
		if (!string.IsNullOrWhiteSpace(preferredBaseHash) && SoothingSpiritsHealPetLadder.IsSoothingSpiritsUpgradeHash(petHash) && Dao<DBMobTemplate, MobTemplateDao>.Instance.GetMobTemplateByHash(preferredBaseHash) != null)
		{
			return preferredBaseHash;
		}
		if (SoothingSpiritsHashFallbacks.TryGetValue(petHash, out var value) && Dao<DBMobTemplate, MobTemplateDao>.Instance.GetMobTemplateByHash(value) != null)
		{
			return value;
		}
		string key = ((petHash.Length >= 4) ? petHash.Substring(0, 4) : petHash);
		if (PrefixFallbacks.TryGetValue(key, out var value2) && Dao<DBMobTemplate, MobTemplateDao>.Instance.GetMobTemplateByHash(value2) != null)
		{
			return value2;
		}
		return null;
	}
}
