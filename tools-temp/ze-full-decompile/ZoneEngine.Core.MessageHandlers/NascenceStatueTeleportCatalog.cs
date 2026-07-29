using System;
using System.Collections.Generic;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;

namespace ZoneEngine.Core.MessageHandlers;

public static class NascenceStatueTeleportCatalog
{
	public const int ThrakInsigniaTemplateId = 214789;

	private const int ShadowlandsGardenPlayfieldMin = 4676;

	private const int ShadowlandsGardenPlayfieldMax = 4699;

	private static readonly int[] ShadowlandsZonePlayfieldIds = new int[18]
	{
		4310, 4311, 4312, 4313, 4320, 4321, 4322, 4328, 4540, 4541,
		4542, 4543, 4544, 4605, 4872, 4873, 4880, 4881
	};

	private static readonly ShadowlandsReturnKeyPair[] ReturnKeyPairs = BuildReturnKeyPairs();

	private static readonly Dictionary<int, GardenReturnPosition> GardenReturnPositionsByPlayfieldId = BuildGardenReturnPositionsByPlayfieldId();

	private static readonly Dictionary<string, NascenceGardenPassageRoute> GardenPassageRoutesByName = BuildGardenPassageRoutesByName();

	private static readonly int[] GardenReturnInsigniaTemplateIds = new int[12]
	{
		224051, 224050, 224049, 214881, 214880, 214855, 214840, 214789, 226994, 214788,
		214782, 214781
	};

	public static bool IsShadowlandsGardenPlayfield(int playfieldId)
	{
		return playfieldId >= 4676 && playfieldId <= 4699;
	}

	public static bool IsShadowlandsZonePlayfield(int playfieldId)
	{
		for (int i = 0; i < ShadowlandsZonePlayfieldIds.Length; i++)
		{
			if (ShadowlandsZonePlayfieldIds[i] == playfieldId)
			{
				return true;
			}
		}
		return false;
	}

	public static bool TryGetGardenPassageRouteByName(string passageName, out NascenceGardenPassageRoute route)
	{
		route = null;
		if (string.IsNullOrEmpty(passageName))
		{
			return false;
		}
		if (GardenPassageRoutesByName.TryGetValue(passageName.Trim(), out var value))
		{
			route = value;
			return true;
		}
		foreach (KeyValuePair<string, NascenceGardenPassageRoute> item in GardenPassageRoutesByName)
		{
			if (passageName.IndexOf(item.Key, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				route = item.Value;
				return true;
			}
		}
		return false;
	}

	public static bool TryGetGardenPassageRouteByTemplateId(int templateId, out NascenceGardenPassageRoute route)
	{
		route = null;
		string passageName = null;
		try
		{
			DBItemName val = ((Dao<DBItemName, ItemNamesDao>)(object)Dao<DBItemName, ItemNamesDao>.Instance).Get(templateId);
			if (val != null)
			{
				passageName = val.Name;
			}
		}
		catch
		{
		}
		return TryGetGardenPassageRouteByName(passageName, out route);
	}

	public static bool TryMatchReturnKey(int statueTemplateId, int insigniaTemplateId)
	{
		for (int i = 0; i < ReturnKeyPairs.Length; i++)
		{
			ShadowlandsReturnKeyPair shadowlandsReturnKeyPair = ReturnKeyPairs[i];
			if (shadowlandsReturnKeyPair.StatueTemplateId == statueTemplateId && shadowlandsReturnKeyPair.InsigniaTemplateId == insigniaTemplateId)
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsZoneReturnStatueTemplate(int templateId)
	{
		for (int i = 0; i < ReturnKeyPairs.Length; i++)
		{
			if (ReturnKeyPairs[i].StatueTemplateId == templateId)
			{
				return true;
			}
		}
		return false;
	}

	public static int ResolveReturnGardenPlayfieldId(int sourceZonePlayfieldId, int otUnredeemedValue)
	{
		bool flag = otUnredeemedValue != 0;
		if (sourceZonePlayfieldId >= 4310 && sourceZonePlayfieldId <= 4313)
		{
			return flag ? 4677 : 4676;
		}
		if (sourceZonePlayfieldId >= 4540 && sourceZonePlayfieldId <= 4544)
		{
			return flag ? 4680 : 4678;
		}
		if (sourceZonePlayfieldId == 4880 || sourceZonePlayfieldId == 4881)
		{
			return flag ? 4683 : 4682;
		}
		if (sourceZonePlayfieldId == 4872 || sourceZonePlayfieldId == 4873)
		{
			return flag ? 4686 : 4684;
		}
		if (sourceZonePlayfieldId >= 4320 && sourceZonePlayfieldId <= 4322)
		{
			return flag ? 4690 : 4688;
		}
		return sourceZonePlayfieldId switch
		{
			4605 => flag ? 4694 : 4692, 
			4328 => flag ? 4697 : 4696, 
			_ => flag ? 4677 : 4676, 
		};
	}

	public static void ResolveReturnGardenPosition(int gardenPlayfieldId, out float x, out float y, out float z)
	{
		if (GardenReturnPositionsByPlayfieldId.TryGetValue(gardenPlayfieldId, out var value))
		{
			x = value.X;
			y = value.Y;
			z = value.Z;
		}
		else
		{
			x = 456.68f;
			y = 40.152f;
			z = 424.161f;
		}
	}

	public static bool IsGardenReturnInsignia(int insigniaTemplateId)
	{
		for (int i = 0; i < ReturnKeyPairs.Length; i++)
		{
			if (ReturnKeyPairs[i].InsigniaTemplateId == insigniaTemplateId)
			{
				return true;
			}
		}
		for (int j = 0; j < GardenReturnInsigniaTemplateIds.Length; j++)
		{
			if (GardenReturnInsigniaTemplateIds[j] == insigniaTemplateId)
			{
				return true;
			}
		}
		return false;
	}

	private static ShadowlandsReturnKeyPair[] BuildReturnKeyPairs()
	{
		return new ShadowlandsReturnKeyPair[27]
		{
			new ShadowlandsReturnKeyPair(222955, 214789),
			new ShadowlandsReturnKeyPair(222955, 226994),
			new ShadowlandsReturnKeyPair(222954, 214788),
			new ShadowlandsReturnKeyPair(223577, 214782),
			new ShadowlandsReturnKeyPair(244831, 214782),
			new ShadowlandsReturnKeyPair(223559, 214781),
			new ShadowlandsReturnKeyPair(244830, 214781),
			new ShadowlandsReturnKeyPair(223578, 214855),
			new ShadowlandsReturnKeyPair(245044, 214855),
			new ShadowlandsReturnKeyPair(264069, 214855),
			new ShadowlandsReturnKeyPair(223565, 214840),
			new ShadowlandsReturnKeyPair(245043, 214840),
			new ShadowlandsReturnKeyPair(264070, 214840),
			new ShadowlandsReturnKeyPair(223589, 214881),
			new ShadowlandsReturnKeyPair(245046, 214881),
			new ShadowlandsReturnKeyPair(223574, 214880),
			new ShadowlandsReturnKeyPair(245045, 214880),
			new ShadowlandsReturnKeyPair(224017, 224049),
			new ShadowlandsReturnKeyPair(245048, 224049),
			new ShadowlandsReturnKeyPair(223982, 224052),
			new ShadowlandsReturnKeyPair(245047, 224052),
			new ShadowlandsReturnKeyPair(223981, 224050),
			new ShadowlandsReturnKeyPair(245050, 224050),
			new ShadowlandsReturnKeyPair(224018, 224051),
			new ShadowlandsReturnKeyPair(245049, 224051),
			new ShadowlandsReturnKeyPair(227466, 231154),
			new ShadowlandsReturnKeyPair(227466, 231155)
		};
	}

	private static Dictionary<int, GardenReturnPosition> BuildGardenReturnPositionsByPlayfieldId()
	{
		return new Dictionary<int, GardenReturnPosition>
		{
			{
				4676,
				new GardenReturnPosition(345.121f, 119.989f, 381.021f)
			},
			{
				4677,
				new GardenReturnPosition(462.3f, 45.4f, 422.2f)
			},
			{
				4678,
				new GardenReturnPosition(347.838f, 116.446f, 387.168f)
			},
			{
				4679,
				new GardenReturnPosition(347.609f, 119.202f, 397.899f)
			},
			{
				4680,
				new GardenReturnPosition(464.28f, 40.07f, 409.273f)
			},
			{
				4681,
				new GardenReturnPosition(461.489f, 40.143f, 413.082f)
			},
			{
				4682,
				new GardenReturnPosition(376.287f, 119.19f, 395.672f)
			},
			{
				4683,
				new GardenReturnPosition(491.036f, 40.065f, 410.499f)
			},
			{
				4684,
				new GardenReturnPosition(367.756f, 119.844f, 424.454f)
			},
			{
				4685,
				new GardenReturnPosition(370.495f, 119.841f, 416.171f)
			},
			{
				4686,
				new GardenReturnPosition(494.782f, 40.1f, 411.14f)
			},
			{
				4687,
				new GardenReturnPosition(494.91f, 40.067f, 411.759f)
			},
			{
				4688,
				new GardenReturnPosition(372.971f, 119.513f, 391.241f)
			},
			{
				4689,
				new GardenReturnPosition(373.276f, 119.572f, 392.195f)
			},
			{
				4690,
				new GardenReturnPosition(494.736f, 40.058f, 411.072f)
			},
			{
				4691,
				new GardenReturnPosition(496.894f, 40.07f, 410.004f)
			},
			{
				4692,
				new GardenReturnPosition(372.929f, 119.577f, 392.849f)
			},
			{
				4693,
				new GardenReturnPosition(369.599f, 119.404f, 394.313f)
			},
			{
				4694,
				new GardenReturnPosition(495.774f, 40.053f, 410.609f)
			},
			{
				4695,
				new GardenReturnPosition(496.284f, 40.064f, 411.012f)
			},
			{
				4696,
				new GardenReturnPosition(379.017f, 119.837f, 396.285f)
			},
			{
				4697,
				new GardenReturnPosition(495.768f, 40.069f, 409.867f)
			},
			{
				4698,
				new GardenReturnPosition(379.017f, 119.837f, 396.285f)
			},
			{
				4699,
				new GardenReturnPosition(495.768f, 40.069f, 409.867f)
			}
		};
	}

	private static Dictionary<string, NascenceGardenPassageRoute> BuildGardenPassageRoutesByName()
	{
		return new Dictionary<string, NascenceGardenPassageRoute>(StringComparer.OrdinalIgnoreCase)
		{
			{
				"Passage to Acme",
				new NascenceGardenPassageRoute(4544, 1024.91f, 41f, 576.827f, "cellao_codex_test")
			},
			{
				"Passage to Archbile",
				new NascenceGardenPassageRoute(4543, 609.399f, 31.8f, 529.4f, "cellao_codex_test")
			},
			{
				"Passage to Barter",
				new NascenceGardenPassageRoute(4543, 609.399f, 31.8f, 529.4f, "cellao_codex_test")
			},
			{
				"Passage to Blue Mist",
				new NascenceGardenPassageRoute(4320, 1159.48f, 71.9999f, 1042.93f, "cellao_codex_test")
			},
			{
				"Passage to Brawl",
				new NascenceGardenPassageRoute(4311, 242f, 105.01f, 1035f, "cellao_codex_test")
			},
			{
				"Passage to Cape Callous",
				new NascenceGardenPassageRoute(4544, 1024.91f, 41f, 576.827f, "cellao_codex_test")
			},
			{
				"Passage to Chronos Canyon",
				new NascenceGardenPassageRoute(4540, 874.956f, 2.97528f, 825.293f, "cellao_codex_test")
			},
			{
				"Passage to City North",
				new NascenceGardenPassageRoute(4872, 1888.2f, 115.47f, 200.075f, "cellao_codex_test")
			},
			{
				"Passage to City South",
				new NascenceGardenPassageRoute(4872, 1888.2f, 115.47f, 200.075f, "cellao_codex_test")
			},
			{
				"Passage to Cold Rock",
				new NascenceGardenPassageRoute(4544, 1024.91f, 41f, 576.827f, "cellao_codex_test")
			},
			{
				"Passage to Coral Raft",
				new NascenceGardenPassageRoute(4873, 1653.47f, 38f, 1929.15f, "cellao_codex_test")
			},
			{
				"Passage to Corona",
				new NascenceGardenPassageRoute(4544, 1024.91f, 41f, 576.827f, "cellao_codex_test")
			},
			{
				"Passage to Cutching Light",
				new NascenceGardenPassageRoute(4880, 1451.56f, 156.114f, 1546.37f, "cellao_codex_test")
			},
			{
				"Passage to Dark Hill",
				new NascenceGardenPassageRoute(4322, 1070.83f, 83.1564f, 307.288f, "cellao_codex_test")
			},
			{
				"Passage to Dark Marshes",
				new NascenceGardenPassageRoute(4605, 3020.98f, 46.819f, 975.049f, "cellao_codex_test")
			},
			{
				"Passage to Dead Ends",
				new NascenceGardenPassageRoute(4873, 1653.47f, 38f, 1929.15f, "cellao_codex_test")
			},
			{
				"Passage to Domeview",
				new NascenceGardenPassageRoute(4544, 1024.91f, 41f, 576.827f, "cellao_codex_test")
			},
			{
				"Passage to Eastern Brink",
				new NascenceGardenPassageRoute(4881, 1150.77f, 189.4f, 1306.9f, "cellao_codex_test")
			},
			{
				"Passage to Eastfang",
				new NascenceGardenPassageRoute(4541, 850.9f, 102.1f, 1190.9f, "cellao_codex_test")
			},
			{
				"Passage to Enclave",
				new NascenceGardenPassageRoute(4542, 1171.33f, 83.1f, 1480.43f, "cellao_codex_test")
			},
			{
				"Passage to Enig",
				new NascenceGardenPassageRoute(4541, 850.9f, 102.1f, 1190.9f, "cellao_codex_test")
			},
			{
				"Passage to Frontier Border",
				new NascenceGardenPassageRoute(4311, 608f, 13.81f, 556f, "cellao_codex_test")
			},
			{
				"Passage to Frontier Bridge",
				new NascenceGardenPassageRoute(4310, 792f, 31.81f, 1149f, "cellao_codex_test")
			},
			{
				"Passage to Frontier Outskirts",
				new NascenceGardenPassageRoute(4310, 684f, 29.41f, 1898f, "cellao_codex_test")
			},
			{
				"Passage to Giant's Hoof",
				new NascenceGardenPassageRoute(4880, 1451.56f, 156.114f, 1546.37f, "cellao_codex_test")
			},
			{
				"Passage to Glacier Hill",
				new NascenceGardenPassageRoute(4321, 2280.82f, 67.1237f, 1070.62f, "cellao_codex_test")
			},
			{
				"Passage to Godstrand Cliffs",
				new NascenceGardenPassageRoute(4541, 850.9f, 102.1f, 1190.9f, "cellao_codex_test")
			},
			{
				"Passage to Halls of Scheol",
				new NascenceGardenPassageRoute(4881, 1150.77f, 189.4f, 1306.9f, "cellao_codex_test")
			},
			{
				"Passage to Inferno Barracks",
				new NascenceGardenPassageRoute(4605, 3020.98f, 46.819f, 975.049f, "cellao_codex_test")
			},
			{
				"Passage to Inferno Frontier",
				new NascenceGardenPassageRoute(4328, 239.474f, 38.445f, 58.7122f, "cellao_codex_test")
			},
			{
				"Passage to Lament Lagoon",
				new NascenceGardenPassageRoute(4872, 1888.2f, 115.47f, 200.075f, "cellao_codex_test")
			},
			{
				"Passage to Marble Orchards",
				new NascenceGardenPassageRoute(4881, 1150.77f, 189.4f, 1306.9f, "cellao_codex_test")
			},
			{
				"Passage to Mil",
				new NascenceGardenPassageRoute(4311, 274f, 77.716f, 1665f, "cellao_codex_test")
			},
			{
				"Passage to Mirador",
				new NascenceGardenPassageRoute(4880, 1451.56f, 156.114f, 1546.37f, "cellao_codex_test")
			},
			{
				"Passage to Misty Dreams Border",
				new NascenceGardenPassageRoute(4312, 1544f, 52.83f, 680f, "cellao_codex_test")
			},
			{
				"Passage to Misty Marshes",
				new NascenceGardenPassageRoute(4322, 1070.83f, 83.1564f, 307.288f, "cellao_codex_test")
			},
			{
				"Passage to Monopolis",
				new NascenceGardenPassageRoute(4543, 609.399f, 31.8f, 529.4f, "cellao_codex_test")
			},
			{
				"Passage to Nascence Wilds",
				new NascenceGardenPassageRoute(4311, 607.315f, 14.2f, 568.572f, "cellao_codex_test")
			},
			{
				"Passage to Necropolis",
				new NascenceGardenPassageRoute(4881, 1150.77f, 189.4f, 1306.9f, "cellao_codex_test")
			},
			{
				"Passage to Nero",
				new NascenceGardenPassageRoute(4542, 1171.33f, 83.1f, 1480.43f, "cellao_codex_test")
			},
			{
				"Passage to Oasis",
				new NascenceGardenPassageRoute(4605, 3020.98f, 46.819f, 975.049f, "cellao_codex_test")
			},
			{
				"Passage to Old Frontier",
				new NascenceGardenPassageRoute(4310, 858f, 31.52f, 1479f, "cellao_codex_test")
			},
			{
				"Passage to Path to Fire",
				new NascenceGardenPassageRoute(4322, 1070.83f, 83.1564f, 307.288f, "cellao_codex_test")
			},
			{
				"Passage to Penumbra Fortress",
				new NascenceGardenPassageRoute(4321, 2280.82f, 67.1237f, 1070.62f, "cellao_codex_test")
			},
			{
				"Passage to Piercing Tundra",
				new NascenceGardenPassageRoute(4873, 1653.47f, 38f, 1929.15f, "cellao_codex_test")
			},
			{
				"Passage to Purity",
				new NascenceGardenPassageRoute(4321, 2280.82f, 67.1237f, 1070.62f, "cellao_codex_test")
			},
			{
				"Passage to Razor's Lair",
				new NascenceGardenPassageRoute(4322, 1070.83f, 83.1564f, 307.288f, "cellao_codex_test")
			},
			{
				"Passage to Remnans",
				new NascenceGardenPassageRoute(4540, 874.956f, 2.97528f, 825.293f, "cellao_codex_test")
			},
			{
				"Passage to Ripwell",
				new NascenceGardenPassageRoute(4542, 1171.33f, 83.1f, 1480.43f, "cellao_codex_test")
			},
			{
				"Passage to Sabre's Cradle",
				new NascenceGardenPassageRoute(4543, 609.399f, 31.8f, 529.4f, "cellao_codex_test")
			},
			{
				"Passage to Shattered Heartlands",
				new NascenceGardenPassageRoute(4541, 850.9f, 102.1f, 1190.9f, "cellao_codex_test")
			},
			{
				"Passage to Shell Beach",
				new NascenceGardenPassageRoute(4540, 874.956f, 2.97528f, 825.293f, "cellao_codex_test")
			},
			{
				"Passage to Shunpike",
				new NascenceGardenPassageRoute(4543, 609.399f, 31.8f, 529.4f, "cellao_codex_test")
			},
			{
				"Passage to Silence",
				new NascenceGardenPassageRoute(4311, 607.315f, 14.2f, 568.572f, "cellao_codex_test")
			},
			{
				"Passage to Sorrow",
				new NascenceGardenPassageRoute(4605, 3020.98f, 46.819f, 975.049f, "cellao_codex_test")
			},
			{
				"Passage to Sorrow Outlook",
				new NascenceGardenPassageRoute(4605, 3020.98f, 46.819f, 975.049f, "cellao_codex_test")
			},
			{
				"Passage to Spade",
				new NascenceGardenPassageRoute(4541, 850.9f, 102.1f, 1190.9f, "cellao_codex_test")
			},
			{
				"Passage to Steppe of Dispair",
				new NascenceGardenPassageRoute(4312, 1540.6f, 55.3f, 676.701f, "cellao_codex_test")
			},
			{
				"Passage to Stormshelter",
				new NascenceGardenPassageRoute(4542, 1171.33f, 83.1f, 1480.43f, "cellao_codex_test")
			},
			{
				"Passage to The Approach",
				new NascenceGardenPassageRoute(4881, 1150.77f, 189.4f, 1306.9f, "cellao_codex_test")
			},
			{
				"Passage to The Bastion",
				new NascenceGardenPassageRoute(4881, 1150.77f, 189.4f, 1306.9f, "cellao_codex_test")
			},
			{
				"Passage to The Core",
				new NascenceGardenPassageRoute(4312, 1630f, 43.96f, 1469f, "cellao_codex_test")
			},
			{
				"Passage to The Court",
				new NascenceGardenPassageRoute(4880, 1451.56f, 156.114f, 1546.37f, "cellao_codex_test")
			},
			{
				"Passage to The Divide",
				new NascenceGardenPassageRoute(4540, 874.956f, 2.97528f, 825.293f, "cellao_codex_test")
			},
			{
				"Passage to The Fallen Forest",
				new NascenceGardenPassageRoute(4540, 874.956f, 2.97528f, 825.293f, "cellao_codex_test")
			},
			{
				"Passage to The Highlands",
				new NascenceGardenPassageRoute(4881, 1150.77f, 189.4f, 1306.9f, "cellao_codex_test")
			},
			{
				"Passage to The Jagged Coast",
				new NascenceGardenPassageRoute(4544, 1024.91f, 41f, 576.827f, "cellao_codex_test")
			},
			{
				"Passage to The Outer Isles",
				new NascenceGardenPassageRoute(4544, 1024.91f, 41f, 576.827f, "cellao_codex_test")
			},
			{
				"Passage to The Outmost Yard",
				new NascenceGardenPassageRoute(4872, 1888.2f, 115.47f, 200.075f, "cellao_codex_test")
			},
			{
				"Passage to The Pipe",
				new NascenceGardenPassageRoute(4321, 2280.82f, 67.1237f, 1070.62f, "cellao_codex_test")
			},
			{
				"Passage to The Pool",
				new NascenceGardenPassageRoute(4872, 1888.2f, 115.47f, 200.075f, "cellao_codex_test")
			},
			{
				"Passage to The Ravine",
				new NascenceGardenPassageRoute(4320, 1159.48f, 71.9999f, 1042.93f, "cellao_codex_test")
			},
			{
				"Passage to The Scoop",
				new NascenceGardenPassageRoute(4542, 1171.33f, 83.1f, 1480.43f, "cellao_codex_test")
			},
			{
				"Passage to The Sink",
				new NascenceGardenPassageRoute(4541, 850.9f, 102.1f, 1190.9f, "cellao_codex_test")
			},
			{
				"Passage to The Temple Bog",
				new NascenceGardenPassageRoute(4881, 1150.77f, 189.4f, 1306.9f, "cellao_codex_test")
			},
			{
				"Passage to The Twilight Basin",
				new NascenceGardenPassageRoute(4881, 1150.77f, 189.4f, 1306.9f, "cellao_codex_test")
			},
			{
				"Passage to The Wetlands",
				new NascenceGardenPassageRoute(4311, 607.315f, 14.2f, 568.572f, "cellao_codex_test")
			},
			{
				"Passage to Time's Tide",
				new NascenceGardenPassageRoute(4540, 874.956f, 2.97528f, 825.293f, "cellao_codex_test")
			},
			{
				"Passage to Tinker Tower",
				new NascenceGardenPassageRoute(4543, 609.399f, 31.8f, 529.4f, "cellao_codex_test")
			},
			{
				"Passage to Two Mountains",
				new NascenceGardenPassageRoute(4312, 1540.6f, 55.3f, 676.701f, "cellao_codex_test")
			},
			{
				"Passage to Utopolis",
				new NascenceGardenPassageRoute(4542, 1171.33f, 83.1f, 1480.43f, "cellao_codex_test")
			},
			{
				"Passage to Watcher's Ocular",
				new NascenceGardenPassageRoute(4872, 1888.2f, 115.47f, 200.075f, "cellao_codex_test")
			},
			{
				"Passage to Whispervale",
				new NascenceGardenPassageRoute(4540, 874.956f, 2.97528f, 825.293f, "cellao_codex_test")
			},
			{
				"Passage to White Citadel",
				new NascenceGardenPassageRoute(4321, 2280.82f, 67.1237f, 1070.62f, "cellao_codex_test")
			},
			{
				"Passage to Xark's Lair",
				new NascenceGardenPassageRoute(4605, 3020.98f, 46.819f, 975.049f, "cellao_codex_test")
			},
			{
				"Passage to Yutto Marshes",
				new NascenceGardenPassageRoute(4605, 3020.98f, 46.819f, 975.049f, "cellao_codex_test")
			},
			{
				"Passage to Yutto Wasteland",
				new NascenceGardenPassageRoute(4320, 1159.48f, 71.9999f, 1042.93f, "cellao_codex_test")
			}
		};
	}
}
