using System;
using System.Collections.Generic;

namespace ZoneEngine.Core;

internal static class PetAttackPetCombatCatalog
{
	internal sealed class Profile
	{
		public int MinDamage { get; set; }

		public int MaxDamage { get; set; }
	}

	private static readonly Dictionary<string, Profile> ProfilesByHashPrefix = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase)
	{
		{
			"PT50",
			new Profile
			{
				MinDamage = 12,
				MaxDamage = 18
			}
		},
		{
			"PT51",
			new Profile
			{
				MinDamage = 53,
				MaxDamage = 53
			}
		},
		{
			"PT52",
			new Profile
			{
				MinDamage = 126,
				MaxDamage = 156
			}
		},
		{
			"PT53",
			new Profile
			{
				MinDamage = 220,
				MaxDamage = 344
			}
		},
		{
			"PT54",
			new Profile
			{
				MinDamage = 419,
				MaxDamage = 642
			}
		},
		{
			"PT56",
			new Profile
			{
				MinDamage = 850,
				MaxDamage = 930
			}
		}
	};

	public static bool TryGet(string petHash, out Profile profile)
	{
		profile = null;
		if (string.IsNullOrWhiteSpace(petHash))
		{
			return false;
		}
		string key = ((petHash.Length >= 4) ? petHash.Substring(0, 4) : petHash);
		return ProfilesByHashPrefix.TryGetValue(key, out profile);
	}
}
