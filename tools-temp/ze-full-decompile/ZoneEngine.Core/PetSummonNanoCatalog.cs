using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Nanos;
using AORebirth.Core.Requirements;
using AORebirth.Enums;
using AORebirth.Stats;
using MsgPack;

namespace ZoneEngine.Core;

internal static class PetSummonNanoCatalog
{
	private static readonly int SummonPetFunctionId = 53167;

	private static readonly int SummonPetsFunctionId = 53181;

	private static readonly Dictionary<int, string> PreferredPetHashByNano = new Dictionary<int, string>
	{
		{ 125738, "MT01" },
		{ 125743, "MT04" },
		{ 125744, "MT03" },
		{ 125745, "MT02" },
		{ 125746, "BSLX" },
		{ 43324, "PT50" },
		{ 46350, "A020" },
		{ 46353, "A020" },
		{ 46354, "A020" },
		{ 46355, "A020" },
		{ 46357, "A020" },
		{ 46359, "A020" },
		{ 46362, "A020" },
		{ 46363, "A020" },
		{ 46374, "A020" },
		{ 46378, "A020" },
		{ 46379, "A020" },
		{ 46380, "A020" },
		{ 46382, "A020" },
		{ 46384, "A020" },
		{ 46386, "A020" },
		{ 46388, "A020" },
		{ 46389, "A020" },
		{ 46390, "A020" },
		{ 46391, "BCBG" },
		{ 46392, "A020" },
		{ 46397, "A020" },
		{ 46398, "A020" },
		{ 46399, "A020" },
		{ 46405, "A020" },
		{ 46407, "A020" },
		{ 46408, "A020" },
		{ 46409, "A020" },
		{ 46411, "A020" },
		{ 235386, "A141" },
		{ 273300, "A141" },
		{ 293899, "CRLT" },
		{ 258580, "A142" },
		{ 43733, "PT51" },
		{ 43723, "PT52" },
		{ 43734, "PT52" },
		{ 43735, "PT53" },
		{ 43732, "PT54" },
		{ 43737, "PT56" }
	};

	private static readonly Dictionary<int, int> PreferredPetTypeByNano = new Dictionary<int, int>
	{
		{ 125738, 14 },
		{ 125743, 77 },
		{ 125744, 55 },
		{ 125745, 33 },
		{ 125746, 192 },
		{ 43324, 10 },
		{ 46350, 111 },
		{ 46353, 84 },
		{ 46354, 63 },
		{ 46355, 42 },
		{ 46357, 24 },
		{ 46359, 18 },
		{ 46362, 3 },
		{ 46363, 6 },
		{ 46374, 20 },
		{ 46378, 87 },
		{ 46379, 66 },
		{ 46380, 45 },
		{ 46382, 26 },
		{ 46384, 115 },
		{ 46386, 14 },
		{ 46388, 91 },
		{ 46389, 69 },
		{ 46390, 48 },
		{ 46391, 200 },
		{ 46392, 28 },
		{ 46397, 2 },
		{ 46398, 107 },
		{ 46399, 8 },
		{ 46405, 16 },
		{ 46407, 81 },
		{ 46408, 60 },
		{ 46409, 39 },
		{ 46411, 22 },
		{ 235386, 205 },
		{ 273300, 215 },
		{ 293899, 215 },
		{ 258580, 220 },
		{ 43733, 32 },
		{ 43723, 52 },
		{ 43734, 62 },
		{ 43735, 95 },
		{ 43732, 137 },
		{ 43737, 200 }
	};

	private static readonly HashSet<int> DirectSummonNanos = new HashSet<int> { 293899, 258580 };

	private static readonly Dictionary<int, CapturedBureaucratPetProfile> BureaucratProfiles = new Dictionary<int, CapturedBureaucratPetProfile>
	{
		{
			46397,
			new CapturedBureaucratPetProfile("Bureaucrat Worker", 2, 38, 96056, 91, 15)
		},
		{
			46362,
			new CapturedBureaucratPetProfile("Bureaucrat Worker", 3, 56, 96056, 92, 21)
		},
		{
			46363,
			new CapturedBureaucratPetProfile("Bureaucrat Worker", 6, 110, 96056, 93, 33)
		},
		{
			46399,
			new CapturedBureaucratPetProfile("Bureaucrat Worker", 8, 146, 96056, 94, 42)
		},
		{
			46386,
			new CapturedBureaucratPetProfile("Bureaucrat Worker", 14, 288, 96056, 97, 73)
		},
		{
			46405,
			new CapturedBureaucratPetProfile("Bureaucrat Helper", 16, 341, 96056, 97, 83)
		},
		{
			46359,
			new CapturedBureaucratPetProfile("Bureaucrat Helper", 18, 394, 96056, 98, 93)
		},
		{
			46374,
			new CapturedBureaucratPetProfile("Bureaucrat Helper", 20, 447, 96056, 99, 103)
		},
		{
			46411,
			new CapturedBureaucratPetProfile("Bureaucrat Helper", 22, 500, 96056, 99, 113)
		},
		{
			46357,
			new CapturedBureaucratPetProfile("Bureaucrat Helper", 24, 553, 96056, 100, 123)
		},
		{
			46382,
			new CapturedBureaucratPetProfile("Bureaucrat Helper", 26, 629, 96056, 101, 133)
		},
		{
			46392,
			new CapturedBureaucratPetProfile("Bureaucrat Helper", 28, 728, 96056, 101, 143)
		},
		{
			46409,
			new CapturedBureaucratPetProfile("Bureaucrat Attendant", 39, 1271, 96056, 103, 197)
		},
		{
			46355,
			new CapturedBureaucratPetProfile("Bureaucrat Attendant", 42, 1419, 96056, 104, 213)
		},
		{
			46380,
			new CapturedBureaucratPetProfile("Bureaucrat Attendant", 45, 1567, 96056, 104, 228)
		},
		{
			46390,
			new CapturedBureaucratPetProfile("Bureaucrat Attendant", 48, 1715, 96056, 105, 243)
		},
		{
			46408,
			new CapturedBureaucratPetProfile("Bureaucrat Assistant", 60, 2489, 96056, 107, 307)
		},
		{
			46354,
			new CapturedBureaucratPetProfile("Bureaucrat Assistant", 63, 2691, 96056, 107, 324)
		},
		{
			46379,
			new CapturedBureaucratPetProfile("Bureaucrat Assistant", 66, 2894, 96056, 107, 338)
		},
		{
			46389,
			new CapturedBureaucratPetProfile("Bureaucrat Assistant", 69, 3096, 96056, 108, 356)
		},
		{
			46407,
			new CapturedBureaucratPetProfile("Bureaucrat Aide", 81, 3907, 96056, 109, 419)
		},
		{
			46353,
			new CapturedBureaucratPetProfile("Bureaucrat Aide", 84, 4109, 96056, 110, 436)
		},
		{
			46378,
			new CapturedBureaucratPetProfile("Bureaucrat Aide", 87, 4312, 96056, 110, 452)
		},
		{
			46388,
			new CapturedBureaucratPetProfile("Bureaucrat Aide", 91, 4609, 96056, 111, 472)
		},
		{
			46398,
			new CapturedBureaucratPetProfile("Bureaucrat Secretary", 107, 6128, 96056, 113, 537)
		},
		{
			46350,
			new CapturedBureaucratPetProfile("Bureaucrat Secretary", 111, 6507, 96056, 113, 552)
		},
		{
			46384,
			new CapturedBureaucratPetProfile("Bureaucrat Secretary", 115, 6887, 96056, 114, 568)
		},
		{
			46391,
			new CapturedBureaucratPetProfile("Bureaucrat Bodyguard", 200, 29148, 17627, 121, 821)
		},
		{
			235386,
			new CapturedBureaucratPetProfile("Corporate Guardian", 205, 29288, 227701, 130, 909)
		},
		{
			273300,
			new CapturedBureaucratPetProfile("CEO Guardian", 215, 34513, 227701, 125, 1062)
		},
		{
			293899,
			new CapturedBureaucratPetProfile("Carlita Desposito", 215, 51768, 293901, 100, 1062, 223867, 97)
		},
		{
			258580,
			new CapturedBureaucratPetProfile("Carlo Pinnetti", 220, 55687, 258209, 130, 1138, 40121, 97)
		}
	};

	private static readonly Dictionary<int, CapturedBureaucratShellDisplay> BureaucratShellDisplayByNano = new Dictionary<int, CapturedBureaucratShellDisplay>
	{
		{
			46397,
			new CapturedBureaucratShellDisplay(96235, 150722, 2)
		},
		{
			46362,
			new CapturedBureaucratShellDisplay(96235, 150722, 3)
		},
		{
			46363,
			new CapturedBureaucratShellDisplay(150722, 150721, 6)
		},
		{
			46399,
			new CapturedBureaucratShellDisplay(150721, 150720, 8)
		},
		{
			46405,
			new CapturedBureaucratShellDisplay(150736, 150735, 16)
		},
		{
			46386,
			new CapturedBureaucratShellDisplay(150721, 150720, 14)
		},
		{
			46374,
			new CapturedBureaucratShellDisplay(150735, 150735, 20)
		},
		{
			46359,
			new CapturedBureaucratShellDisplay(150736, 150735, 18)
		},
		{
			46411,
			new CapturedBureaucratShellDisplay(150735, 150734, 22)
		},
		{
			46357,
			new CapturedBureaucratShellDisplay(150735, 150734, 24)
		},
		{
			46382,
			new CapturedBureaucratShellDisplay(150734, 150733, 26)
		},
		{
			46392,
			new CapturedBureaucratShellDisplay(150734, 150733, 28)
		},
		{
			46409,
			new CapturedBureaucratShellDisplay(150744, 150743, 39)
		},
		{
			46355,
			new CapturedBureaucratShellDisplay(150743, 150742, 42)
		},
		{
			46380,
			new CapturedBureaucratShellDisplay(150742, 150742, 45)
		},
		{
			46390,
			new CapturedBureaucratShellDisplay(150742, 96201, 48)
		},
		{
			46408,
			new CapturedBureaucratShellDisplay(150747, 150747, 60)
		},
		{
			46354,
			new CapturedBureaucratShellDisplay(150747, 150746, 63)
		},
		{
			46379,
			new CapturedBureaucratShellDisplay(150746, 150745, 66)
		},
		{
			46389,
			new CapturedBureaucratShellDisplay(150746, 150745, 69)
		},
		{
			46407,
			new CapturedBureaucratShellDisplay(150753, 150752, 81)
		},
		{
			46353,
			new CapturedBureaucratShellDisplay(150753, 150752, 84)
		},
		{
			46378,
			new CapturedBureaucratShellDisplay(150752, 150751, 87)
		},
		{
			46388,
			new CapturedBureaucratShellDisplay(150751, 150750, 91)
		},
		{
			46398,
			new CapturedBureaucratShellDisplay(150726, 150725, 107)
		},
		{
			46350,
			new CapturedBureaucratShellDisplay(150725, 150724, 111)
		},
		{
			46384,
			new CapturedBureaucratShellDisplay(150724, 150724, 115)
		},
		{
			46391,
			new CapturedBureaucratShellDisplay(96213, 96213, 200)
		},
		{
			235386,
			new CapturedBureaucratShellDisplay(239828, 239828, 205)
		},
		{
			273300,
			new CapturedBureaucratShellDisplay(273301, 273301, 215)
		}
	};

	private static readonly Dictionary<string, int> BureaucratNanoByShellDisplay = BuildBureaucratNanoByShellDisplay();

	private static readonly HashSet<int> BureaucratShellItemLowIds = BuildBureaucratShellItemLowIds();

	private static readonly Dictionary<int, string> SummonNanoDisplayName = new Dictionary<int, string>
	{
		{ 125738, "Calling of Medinos" },
		{ 125743, "Calling of Sanoo" },
		{ 125744, "Calling of Valentyia" },
		{ 125745, "Calling of Salvinous" },
		{ 125746, "Calling of Belamorte" }
	};

	public static bool IsCatalogSummonNano(int nanoId)
	{
		return PreferredPetHashByNano.ContainsKey(nanoId);
	}

	public static bool TryResolveShellSummonParams(int nanoId, out PetSummonParams summonParams)
	{
		summonParams = null;
		if (!PreferredPetHashByNano.TryGetValue(nanoId, out var value))
		{
			return false;
		}
		int petTypeId = ResolvePreferredPetType(nanoId);
		if (TryGetBureaucratProfile(nanoId, out var profile))
		{
			petTypeId = profile.Level;
		}
		summonParams = new PetSummonParams
		{
			NanoId = nanoId,
			PetHash = value,
			PetTypeId = petTypeId
		};
		return true;
	}

	public static bool IsDirectSummonNano(int nanoId)
	{
		return DirectSummonNanos.Contains(nanoId);
	}

	public static int ResolveCarlitaCompanionLevel(int ownerLevel, int petTypeId = 0)
	{
		if (petTypeId >= 200 && petTypeId <= 215)
		{
			return petTypeId;
		}
		if (ownerLevel < 205)
		{
			return 200;
		}
		if (ownerLevel < 210)
		{
			return 205;
		}
		if (ownerLevel < 215)
		{
			return 210;
		}
		return 215;
	}

	public static int ResolveBureaucratCompanionLevel(int nanoId, int ownerLevel, int petTypeId)
	{
		return nanoId switch
		{
			258580 => 220, 
			293899 => ResolveCarlitaCompanionLevel(ownerLevel, petTypeId), 
			_ => (petTypeId > 0) ? petTypeId : ownerLevel, 
		};
	}

	public static bool TryGetBureaucratProfile(int nanoId, out CapturedBureaucratPetProfile profile)
	{
		return BureaucratProfiles.TryGetValue(nanoId, out profile);
	}

	public static bool TryGetBureaucratShellDisplay(int nanoId, out CapturedBureaucratShellDisplay shellDisplay)
	{
		return BureaucratShellDisplayByNano.TryGetValue(nanoId, out shellDisplay);
	}

	public static string GetBureaucratShellItemName(int nanoId)
	{
		CapturedBureaucratPetProfile profile;
		return TryGetBureaucratProfile(nanoId, out profile) ? (profile.Name + " Shell") : null;
	}

	public static bool IsBureaucratShellItemLowId(int lowId)
	{
		return BureaucratShellItemLowIds.Contains(lowId);
	}

	public static bool TryResolveBureaucratShellNano(int shellItemLowId, int shellItemHighId, int shellQuality, out int nanoId)
	{
		return BureaucratNanoByShellDisplay.TryGetValue(BuildShellDisplayKey(shellItemLowId, shellItemHighId, shellQuality), out nanoId);
	}

	public static bool TryResolveShellSummonForItem(ICharacter character, int shellItemLowId, int shellItemHighId, int shellQuality, int profession, out PetSummonParams summonParams)
	{
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Expected O, but got Unknown
		summonParams = null;
		if (character == null)
		{
			return false;
		}
		if (profession == 8)
		{
			if (!TryResolveBureaucratShellNano(shellItemLowId, shellItemHighId, shellQuality, out var nanoId) || !TryResolveShellSummonParams(nanoId, out summonParams))
			{
				return false;
			}
			return true;
		}
		foreach (UploadedNano uploadedNano in character.UploadedNanos)
		{
			UploadedNano val = uploadedNano;
			int nanoId = val.NanoId;
			if (!NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId) || !PetShellCatalog.UsesShellOnSummon(profession, nanoId) || !TryResolveShellSummonParams(nanoId, out summonParams) || (profession == 8 && TryGetBureaucratShellDisplay(nanoId, out var shellDisplay) && (shellDisplay.DisplayItemLowId != shellItemLowId || shellDisplay.DisplayItemHighId != shellItemHighId || shellDisplay.DisplayQuality != shellQuality)))
			{
				continue;
			}
			return true;
		}
		return false;
	}

	private static Dictionary<string, int> BuildBureaucratNanoByShellDisplay()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (KeyValuePair<int, CapturedBureaucratShellDisplay> item in BureaucratShellDisplayByNano)
		{
			string key = BuildShellDisplayKey(item.Value.DisplayItemLowId, item.Value.DisplayItemHighId, item.Value.DisplayQuality);
			dictionary[key] = item.Key;
		}
		return dictionary;
	}

	private static HashSet<int> BuildBureaucratShellItemLowIds()
	{
		HashSet<int> hashSet = new HashSet<int>();
		foreach (CapturedBureaucratShellDisplay value in BureaucratShellDisplayByNano.Values)
		{
			hashSet.Add(value.DisplayItemLowId);
		}
		return hashSet;
	}

	private static string BuildShellDisplayKey(int lowId, int highId, int quality)
	{
		return $"{lowId}:{highId}:{quality}";
	}

	public static bool TryResolve(ICharacter character, int nanoId, out PetSummonParams summonParams)
	{
		summonParams = null;
		if (character == null || !NanoLoader.NanoList.TryGetValue(nanoId, out var value))
		{
			return false;
		}
		if (PreferredPetHashByNano.TryGetValue(nanoId, out var preferredHash))
		{
			List<PetSummonParams> candidates = CollectQualifiedCandidates(character, nanoId, value);
			PetSummonParams petSummonParams = SelectBestCandidate(character, candidates, preferredHash);
			if (petSummonParams == null)
			{
				List<PetSummonParams> source = CollectCandidates(nanoId, value);
				List<PetSummonParams> candidates2 = source.Where((PetSummonParams x) => string.Equals(x.PetHash, preferredHash, StringComparison.OrdinalIgnoreCase)).ToList();
				petSummonParams = SelectBestCandidate(character, candidates2, preferredHash);
			}
			if (petSummonParams == null)
			{
				int value2 = ((IStats)character).Stats[(StatIds)54].Value;
				petSummonParams = new PetSummonParams
				{
					NanoId = nanoId,
					PetHash = preferredHash,
					PetTypeId = ResolveBureaucratCompanionLevel(nanoId, value2, ResolvePreferredPetType(nanoId))
				};
			}
			summonParams = petSummonParams;
			return true;
		}
		List<PetSummonParams> list = CollectCandidates(nanoId, value);
		if (list.Count == 0)
		{
			return false;
		}
		List<PetSummonParams> list2 = CollectQualifiedCandidates(character, nanoId, value);
		List<PetSummonParams> candidates3 = ((list2.Count > 0) ? list2 : list);
		PetSummonParams petSummonParams2 = SelectBestCandidate(character, candidates3, null);
		if (petSummonParams2 == null)
		{
			return false;
		}
		summonParams = petSummonParams2;
		return true;
	}

	public static string GetPreferredPetHash(int nanoId)
	{
		string value;
		return PreferredPetHashByNano.TryGetValue(nanoId, out value) ? value : null;
	}

	public static string GetSummonNanoDisplayName(int nanoId)
	{
		string value;
		return SummonNanoDisplayName.TryGetValue(nanoId, out value) ? value : "Calling";
	}

	private static int ResolvePreferredPetType(int nanoId)
	{
		int value;
		return (!PreferredPetTypeByNano.TryGetValue(nanoId, out value)) ? 1 : value;
	}

	private static List<PetSummonParams> CollectCandidates(int nanoId, NanoFormula nano)
	{
		List<PetSummonParams> list = new List<PetSummonParams>();
		foreach (Event item in nano.Events.Where((Event x) => (int)x.EventType == 0))
		{
			if (item.Functions == null)
			{
				continue;
			}
			foreach (Function item2 in item.Functions.Where(IsSummonFunction))
			{
				PetSummonParams petSummonParams = BuildSummonParams(nanoId, item2);
				if (petSummonParams != null)
				{
					list.Add(petSummonParams);
				}
			}
		}
		return list;
	}

	private static List<PetSummonParams> CollectQualifiedCandidates(ICharacter character, int nanoId, NanoFormula nano)
	{
		List<PetSummonParams> list = new List<PetSummonParams>();
		foreach (Event item in nano.Events.Where((Event x) => (int)x.EventType == 0))
		{
			if (item.Functions == null)
			{
				continue;
			}
			foreach (Function item2 in item.Functions.Where(IsSummonFunction))
			{
				PetSummonParams petSummonParams = BuildSummonParams(nanoId, item2);
				if (petSummonParams != null && FunctionRequirementsPass(character, item2))
				{
					list.Add(petSummonParams);
				}
			}
		}
		return list;
	}

	private static PetSummonParams SelectBestCandidate(ICharacter character, List<PetSummonParams> candidates, string preferredBaseHash)
	{
		if (candidates == null || candidates.Count == 0)
		{
			return null;
		}
		int ownerLevel = ((IStats)character).Stats[(StatIds)54].Value;
		List<PetSummonParams> list = candidates.Where((PetSummonParams x) => !string.IsNullOrWhiteSpace(PetMobTemplateResolver.Resolve(x.PetHash, preferredBaseHash))).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		List<PetSummonParams> list2 = list.Where((PetSummonParams x) => SoothingSpiritsHealPetLadder.IsSoothingSpiritsUpgradeHash(x.PetHash) || string.Equals(x.PetHash, preferredBaseHash, StringComparison.OrdinalIgnoreCase) || IsHealPetBaseHash(x.PetHash)).ToList();
		List<PetSummonParams> list3 = ((list2.Count > 0) ? list2 : list);
		List<PetSummonParams> list4 = list3.Where((PetSummonParams x) => x.PetTypeId <= ownerLevel).ToList();
		if (list4.Count > 0)
		{
			return PickHighestSoothingTier(list4);
		}
		return PickHighestSoothingTier(list3);
	}

	private static bool IsHealPetBaseHash(string petHash)
	{
		return string.Equals(petHash, "MT01", StringComparison.OrdinalIgnoreCase) || string.Equals(petHash, "MT02", StringComparison.OrdinalIgnoreCase) || string.Equals(petHash, "MT03", StringComparison.OrdinalIgnoreCase) || string.Equals(petHash, "MT04", StringComparison.OrdinalIgnoreCase) || string.Equals(petHash, "BSLX", StringComparison.OrdinalIgnoreCase);
	}

	private static PetSummonParams PickHighestSoothingTier(List<PetSummonParams> pool)
	{
		if (pool == null || pool.Count == 0)
		{
			return null;
		}
		int num = int.MinValue;
		PetSummonParams result = null;
		foreach (PetSummonParams item in pool)
		{
			int num2 = ScoreSoothingSpawnHash(item.PetHash) * 100000 + item.PetTypeId;
			if (num2 >= num)
			{
				num = num2;
				result = item;
			}
		}
		return result;
	}

	private static int ScoreSoothingSpawnHash(string petHash)
	{
		if (string.IsNullOrWhiteSpace(petHash))
		{
			return 0;
		}
		return petHash.ToUpperInvariant() switch
		{
			"RHEF" => 6, 
			"MNKW" => 5, 
			"QRMT" => 4, 
			"DKEL" => 3, 
			"JBOB" => 2, 
			"LYNX" => 1, 
			_ => 0, 
		};
	}

	private static bool IsSummonFunction(Function function)
	{
		return function.FunctionType == SummonPetFunctionId || function.FunctionType == SummonPetsFunctionId;
	}

	private static PetSummonParams BuildSummonParams(int nanoId, Function function)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		if (function == null || function.Arguments == null || function.Arguments.Values.Count < 2)
		{
			return null;
		}
		MessagePackObject val = function.Arguments.Values[0];
		string text = ((MessagePackObject)(ref val)).AsString();
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		PetSummonParams obj = new PetSummonParams
		{
			NanoId = nanoId,
			PetHash = text
		};
		val = function.Arguments.Values[1];
		obj.PetTypeId = ((MessagePackObject)(ref val)).AsInt32();
		return obj;
	}

	private static bool FunctionRequirementsPass(ICharacter character, Function function)
	{
		if (function.Requirements == null || function.Requirements.Count == 0)
		{
			return true;
		}
		bool flag = true;
		foreach (Requirement requirement in function.Requirements)
		{
			flag &= requirement.CheckRequirement((IInstancedEntity)(object)character);
			if (!flag)
			{
				break;
			}
		}
		return flag;
	}
}
