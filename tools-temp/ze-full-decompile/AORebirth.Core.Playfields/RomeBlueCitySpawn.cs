using System;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Textures;
using AORebirth.Core.Vector;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Controllers;

namespace AORebirth.Core.Playfields;

internal static class RomeBlueCitySpawn
{
	private sealed class CityNpc
	{
		public string Name;

		public int Level;

		public int Health;

		public int MonsterData;

		public int Scale;

		public int VisualFlags;

		public int HeadMesh;

		public float X;

		public float Y;

		public float Z;

		public float Hx;

		public float Hy;

		public float Hz;

		public float Hw;

		public int[][] Textures;

		public int[][] Meshes;
	}

	private const int RomeBluePlayfieldId = 735;

	private const string TemplateHash = "BART";

	private static readonly CityNpc[] Npcs = new CityNpc[22]
	{
		new CityNpc
		{
			Name = "Male Lieutenant",
			Level = 156,
			Health = 14263,
			MonsterData = 26088,
			Scale = 117,
			VisualFlags = 31,
			HeadMesh = 40687,
			X = 536.45905f,
			Y = 17.41f,
			Z = 446.56003f,
			Hx = 0f,
			Hy = -0.46784f,
			Hz = 0f,
			Hw = 0.88381f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 15809 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 20106, 0, 2 },
				new int[4] { 0, 40687, 0, 4 },
				new int[4] { 1, 35566, 0, 2 }
			}
		},
		new CityNpc
		{
			Name = "Male Lieutenant",
			Level = 161,
			Health = 15009,
			MonsterData = 26088,
			Scale = 118,
			VisualFlags = 31,
			HeadMesh = 40687,
			X = 536.373f,
			Y = 17.41f,
			Z = 438.6647f,
			Hx = 0f,
			Hy = -0.95597f,
			Hz = 0f,
			Hw = 0.29346f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 15809 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 20106, 0, 2 },
				new int[4] { 0, 40687, 0, 4 },
				new int[4] { 1, 35566, 0, 2 }
			}
		},
		new CityNpc
		{
			Name = "Master OT Robotbuilder",
			Level = 196,
			Health = 23349,
			MonsterData = 26092,
			Scale = 121,
			VisualFlags = 31,
			HeadMesh = 40694,
			X = 574.5374f,
			Y = 17.41f,
			Z = 375.21033f,
			Hx = 0f,
			Hy = 0.96891f,
			Hz = 0f,
			Hw = 0.24743f,
			Textures = new int[5][]
			{
				new int[2] { 0, 22605 },
				new int[2] { 1, 8731 },
				new int[2] { 2, 9457 },
				new int[2] { 3, 22541 },
				new int[2] { 4, 9456 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 20098, 0, 2 },
				new int[4] { 0, 40694, 0, 4 },
				new int[4] { 1, 7777, 0, 2 }
			}
		},
		new CityNpc
		{
			Name = "Male Lieutenant",
			Level = 150,
			Health = 13369,
			MonsterData = 26088,
			Scale = 117,
			VisualFlags = 31,
			HeadMesh = 40687,
			X = 639.61176f,
			Y = 17.41f,
			Z = 397.18427f,
			Hx = 0f,
			Hy = 0.94245f,
			Hz = 0f,
			Hw = 0.33434f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 15809 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 20106, 0, 2 },
				new int[4] { 0, 40687, 0, 4 },
				new int[4] { 1, 35566, 0, 2 }
			}
		},
		new CityNpc
		{
			Name = "Mr. Blake",
			Level = 200,
			Health = 36434,
			MonsterData = 245897,
			Scale = 121,
			VisualFlags = 31,
			HeadMesh = 40694,
			X = 645.99994f,
			Y = 21.415f,
			Z = 425.99957f,
			Hx = 0f,
			Hy = 0.99953f,
			Hz = 0f,
			Hw = 0.03056f,
			Textures = new int[5][]
			{
				new int[2] { 0, 40976 },
				new int[2] { 1, 14038 },
				new int[2] { 2, 40903 },
				new int[2] { 3, 14036 },
				new int[2] { 4, 14034 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 20110, 0, 0 },
				new int[4] { 0, 40694, 0, 4 }
			}
		},
		new CityNpc
		{
			Name = "Male Lieutenant",
			Level = 157,
			Health = 14413,
			MonsterData = 26088,
			Scale = 118,
			VisualFlags = 31,
			HeadMesh = 40687,
			X = 709.5055f,
			Y = 17.41f,
			Z = 384.98563f,
			Hx = 0f,
			Hy = 1f,
			Hz = 0f,
			Hw = 0.00079f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 15809 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 20106, 0, 2 },
				new int[4] { 0, 40687, 0, 4 },
				new int[4] { 1, 35566, 0, 2 }
			}
		},
		new CityNpc
		{
			Name = "Seasoned OT Mercenary",
			Level = 135,
			Health = 11133,
			MonsterData = 26103,
			Scale = 116,
			VisualFlags = 31,
			HeadMesh = 40103,
			X = 709.1299f,
			Y = 17.41f,
			Z = 381.69974f,
			Hx = 0f,
			Hy = -0.58586f,
			Hz = 0f,
			Hw = 0.81041f,
			Textures = new int[5][]
			{
				new int[2] { 0, 9620 },
				new int[2] { 1, 8729 },
				new int[2] { 2, 9424 },
				new int[2] { 3, 9423 },
				new int[2] { 4, 9625 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 19994, 0, 2 },
				new int[4] { 0, 40103, 0, 4 },
				new int[4] { 1, 15839, 0, 2 }
			}
		},
		new CityNpc
		{
			Name = "Male Lieutenant",
			Level = 162,
			Health = 15158,
			MonsterData = 26088,
			Scale = 118,
			VisualFlags = 31,
			HeadMesh = 40687,
			X = 661.55994f,
			Y = 17.41f,
			Z = 346.5112f,
			Hx = 0f,
			Hy = 0.39127f,
			Hz = 0f,
			Hw = 0.92028f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 15809 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 20106, 0, 2 },
				new int[4] { 0, 40687, 0, 4 },
				new int[4] { 1, 35566, 0, 2 }
			}
		},
		new CityNpc
		{
			Name = "Omni-AF Urban Trooper",
			Level = 250,
			Health = 200000,
			MonsterData = 26151,
			Scale = 130,
			VisualFlags = 31,
			HeadMesh = 40171,
			X = 723.45966f,
			Y = 17.41f,
			Z = 373.3245f,
			Hx = 0f,
			Hy = -0.99402f,
			Hz = 0f,
			Hw = 0.10919f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 204160 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[6][]
			{
				new int[4] { 0, 20038, 0, 2 },
				new int[4] { 0, 40171, 0, 4 },
				new int[4] { 1, 209529, 0, 2 },
				new int[4] { 3, 11535, 206969, 0 },
				new int[4] { 4, 11535, 206969, 0 },
				new int[4] { 5, 11543, 206969, 0 }
			}
		},
		new CityNpc
		{
			Name = "Seasoned OT Techhunter",
			Level = 112,
			Health = 8253,
			MonsterData = 26147,
			Scale = 113,
			VisualFlags = 31,
			HeadMesh = 40172,
			X = 727.7471f,
			Y = 17.41f,
			Z = 349.29074f,
			Hx = 0f,
			Hy = 0.94405f,
			Hz = 0f,
			Hw = 0.32981f,
			Textures = new int[5][]
			{
				new int[2] { 0, 22605 },
				new int[2] { 1, 8731 },
				new int[2] { 2, 22592 },
				new int[2] { 3, 22541 },
				new int[2] { 4, 9456 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 20030, 0, 2 },
				new int[4] { 0, 40172, 0, 4 },
				new int[4] { 1, 7777, 0, 2 }
			}
		},
		new CityNpc
		{
			Name = "Male Captain",
			Level = 165,
			Health = 15605,
			MonsterData = 26151,
			Scale = 118,
			VisualFlags = 31,
			HeadMesh = 40171,
			X = 727.3877f,
			Y = 21.415f,
			Z = 311.26212f,
			Hx = 0f,
			Hy = 0.70037f,
			Hz = 0f,
			Hw = 0.71378f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 15809 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 20038, 0, 2 },
				new int[4] { 0, 40171, 0, 4 },
				new int[4] { 1, 35566, 0, 2 }
			}
		},
		new CityNpc
		{
			Name = "Omni-AF Urban Trooper",
			Level = 250,
			Health = 200000,
			MonsterData = 26151,
			Scale = 130,
			VisualFlags = 31,
			HeadMesh = 40171,
			X = 727.77496f,
			Y = 21.415f,
			Z = 317.17163f,
			Hx = 0f,
			Hy = 0.70041f,
			Hz = 0f,
			Hw = 0.71374f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 204160 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[6][]
			{
				new int[4] { 0, 20038, 0, 2 },
				new int[4] { 0, 40171, 0, 4 },
				new int[4] { 1, 209529, 0, 2 },
				new int[4] { 3, 11535, 206969, 0 },
				new int[4] { 4, 11535, 206969, 0 },
				new int[4] { 5, 11543, 206969, 0 }
			}
		},
		new CityNpc
		{
			Name = "Omni-AF Urban Trooper",
			Level = 250,
			Health = 200000,
			MonsterData = 26151,
			Scale = 130,
			VisualFlags = 31,
			HeadMesh = 40171,
			X = 742.5093f,
			Y = 17.41f,
			Z = 358.15842f,
			Hx = 0f,
			Hy = -0.73748f,
			Hz = 0f,
			Hw = 0.67536f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 204160 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[6][]
			{
				new int[4] { 0, 20038, 0, 2 },
				new int[4] { 0, 40171, 0, 4 },
				new int[4] { 1, 209529, 0, 2 },
				new int[4] { 3, 11535, 206969, 0 },
				new int[4] { 4, 11535, 206969, 0 },
				new int[4] { 5, 11543, 206969, 0 }
			}
		},
		new CityNpc
		{
			Name = "Omni-AF Urban Trooper",
			Level = 250,
			Health = 200000,
			MonsterData = 26151,
			Scale = 130,
			VisualFlags = 31,
			HeadMesh = 40171,
			X = 742.28754f,
			Y = 17.41f,
			Z = 260.8515f,
			Hx = 0f,
			Hy = -0.73592f,
			Hz = 0f,
			Hw = 0.67707f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 204160 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[6][]
			{
				new int[4] { 0, 20038, 0, 2 },
				new int[4] { 0, 40171, 0, 4 },
				new int[4] { 1, 209529, 0, 2 },
				new int[4] { 3, 11535, 206969, 0 },
				new int[4] { 4, 11535, 206969, 0 },
				new int[4] { 5, 11543, 206969, 0 }
			}
		},
		new CityNpc
		{
			Name = "Omni-AF Urban Trooper",
			Level = 250,
			Health = 200000,
			MonsterData = 26151,
			Scale = 130,
			VisualFlags = 31,
			HeadMesh = 40171,
			X = 742.403f,
			Y = 17.41f,
			Z = 269.5621f,
			Hx = 0f,
			Hy = -0.73876f,
			Hz = 0f,
			Hw = 0.67397f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 204160 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[6][]
			{
				new int[4] { 0, 20038, 0, 2 },
				new int[4] { 0, 40171, 0, 4 },
				new int[4] { 1, 209529, 0, 2 },
				new int[4] { 3, 11535, 206969, 0 },
				new int[4] { 4, 11535, 206969, 0 },
				new int[4] { 5, 11543, 206969, 0 }
			}
		},
		new CityNpc
		{
			Name = "Male Lieutenant",
			Level = 156,
			Health = 14263,
			MonsterData = 26088,
			Scale = 117,
			VisualFlags = 31,
			HeadMesh = 40687,
			X = 713.3038f,
			Y = 17.41f,
			Z = 246.4288f,
			Hx = 0f,
			Hy = 0.99999f,
			Hz = 0f,
			Hw = -0.00382f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 15809 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 20106, 0, 2 },
				new int[4] { 0, 40687, 0, 4 },
				new int[4] { 1, 35566, 0, 2 }
			}
		},
		new CityNpc
		{
			Name = "Rookie OT Mercenary",
			Level = 47,
			Health = 1665,
			MonsterData = 26097,
			Scale = 105,
			VisualFlags = 31,
			HeadMesh = 40111,
			X = 659.0563f,
			Y = 17.41f,
			Z = 242.46013f,
			Hx = 0f,
			Hy = 0.94218f,
			Hz = 0f,
			Hw = 0.3351f,
			Textures = new int[5][]
			{
				new int[2] { 0, 27656 },
				new int[2] { 1, 9404 },
				new int[2] { 2, 9407 },
				new int[2] { 3, 22546 },
				new int[2] { 4, 9401 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 20004, 40993, 2 },
				new int[4] { 0, 40111, 0, 4 },
				new int[4] { 1, 15839, 0, 2 }
			}
		},
		new CityNpc
		{
			Name = "Omni-AF Urban Trooper",
			Level = 250,
			Health = 200000,
			MonsterData = 26137,
			Scale = 130,
			VisualFlags = 31,
			HeadMesh = 40209,
			X = 642.60187f,
			Y = 17.41f,
			Z = 314.65375f,
			Hx = 0f,
			Hy = -0.72214f,
			Hz = 0f,
			Hw = 0.69174f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 204160 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[6][]
			{
				new int[4] { 0, 20055, 0, 2 },
				new int[4] { 0, 40209, 0, 4 },
				new int[4] { 1, 209529, 0, 2 },
				new int[4] { 3, 20715, 206969, 0 },
				new int[4] { 4, 20715, 206969, 0 },
				new int[4] { 5, 20714, 206969, 0 }
			}
		},
		new CityNpc
		{
			Name = "Male Lieutenant",
			Level = 151,
			Health = 13518,
			MonsterData = 26088,
			Scale = 117,
			VisualFlags = 31,
			HeadMesh = 40687,
			X = 615.8673f,
			Y = 17.41f,
			Z = 280.82257f,
			Hx = 0f,
			Hy = 0.31487f,
			Hz = 0f,
			Hw = 0.94913f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 15809 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 20106, 0, 2 },
				new int[4] { 0, 40687, 0, 4 },
				new int[4] { 1, 35566, 0, 2 }
			}
		},
		new CityNpc
		{
			Name = "Male Lieutenant",
			Level = 154,
			Health = 13965,
			MonsterData = 26088,
			Scale = 117,
			VisualFlags = 31,
			HeadMesh = 40687,
			X = 635.2291f,
			Y = 17.41f,
			Z = 230.10336f,
			Hx = 0f,
			Hy = 0.72727f,
			Hz = 0f,
			Hw = 0.68636f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 15809 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 20106, 0, 2 },
				new int[4] { 0, 40687, 0, 4 },
				new int[4] { 1, 35566, 0, 2 }
			}
		},
		new CityNpc
		{
			Name = "Male Lieutenant",
			Level = 150,
			Health = 13369,
			MonsterData = 26088,
			Scale = 117,
			VisualFlags = 31,
			HeadMesh = 40687,
			X = 551.75903f,
			Y = 17.41f,
			Z = 313.58978f,
			Hx = 0f,
			Hy = 1f,
			Hz = 0f,
			Hw = 0.00299f,
			Textures = new int[5][]
			{
				new int[2] { 0, 15806 },
				new int[2] { 1, 15809 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 15808 },
				new int[2] { 4, 15805 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 20106, 0, 2 },
				new int[4] { 0, 40687, 0, 4 },
				new int[4] { 1, 35566, 0, 2 }
			}
		},
		new CityNpc
		{
			Name = "Veteran OT Marksman",
			Level = 179,
			Health = 17692,
			MonsterData = 26147,
			Scale = 119,
			VisualFlags = 31,
			HeadMesh = 40172,
			X = 583.15234f,
			Y = 17.41f,
			Z = 337.49597f,
			Hx = 0f,
			Hy = 0.54306f,
			Hz = 0f,
			Hw = 0.8397f,
			Textures = new int[5][]
			{
				new int[2] { 0, 9418 },
				new int[2] { 1, 8736 },
				new int[2] { 2, 9420 },
				new int[2] { 3, 9605 },
				new int[2] { 4, 9425 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 20037, 0, 2 },
				new int[4] { 0, 40172, 0, 4 },
				new int[4] { 1, 15839, 0, 2 }
			}
		}
	};

	public static void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 735)
		{
			return;
		}
		int num = 0;
		CityNpc[] npcs = Npcs;
		foreach (CityNpc def in npcs)
		{
			if (SpawnOne(playfield, playfieldIdentity, activateNpc, def))
			{
				num++;
			}
		}
		LogUtil.Debug((DebugInfoDetail)128, "RomeBlueCitySpawn pf=" + ((Identity)(ref playfieldIdentity)).Instance + " spawned=" + num + "/" + Npcs.Length);
	}

	private static bool SpawnOne(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc, CityNpc def)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected O, but got Unknown
		//IL_0066: Expected O, but got Unknown
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Expected O, but got Unknown
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		NPCController nPCController = new NPCController();
		Character val = NonPlayerCharacterHandler.SpawnMobFromTemplate("BART", playfieldIdentity, new Coordinate
		{
			x = def.X,
			y = def.Y,
			z = def.Z
		}, new Quaternion((double)def.Hx, (double)def.Hy, (double)def.Hz, (double)def.Hw), (IController)(object)nPCController, def.Level);
		if (val == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "RomeBlueCitySpawn FAILED template=BART npc=" + def.Name);
			return false;
		}
		((Dynel)val).Name = def.Name;
		((Dynel)val).Playfield = (IPlayfield)(object)playfield;
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(359, (uint)def.MonsterData);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(1, (uint)def.Health);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(27, (uint)def.Health);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(54, (uint)def.Level);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(673, (uint)def.VisualFlags);
		if (def.Scale > 0)
		{
			((Dynel)val).Stats.SetBaseValueWithoutTriggering(360, (uint)def.Scale);
		}
		if (def.HeadMesh > 0)
		{
			((Dynel)val).Stats.SetBaseValueWithoutTriggering(64, (uint)def.HeadMesh);
		}
		ApplyAppearance(val, def);
		((Dynel)val).Coordinates(new Coordinate
		{
			x = def.X,
			y = def.Y,
			z = def.Z
		});
		((Dynel)val).DoNotDoTimers = false;
		activateNpc((ICharacter)(object)val);
		playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val, Identity.None);
		return true;
	}

	private static void ApplyAppearance(Character mob, CityNpc def)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		if (def.Textures != null && def.Textures.Length != 0)
		{
			((Dynel)mob).Textures.Clear();
			int[][] textures = def.Textures;
			foreach (int[] array in textures)
			{
				((Dynel)mob).Textures.Add(new AOTextures(array[0], array[1]));
			}
		}
		if (def.Meshes != null && def.Meshes.Length != 0)
		{
			((Dynel)mob).MeshLayer.Clear();
			mob.SocialMeshLayer.Clear();
			int[][] meshes = def.Meshes;
			foreach (int[] array2 in meshes)
			{
				((Dynel)mob).MeshLayer.AddMesh(array2[0], array2[1], array2[2], array2[3]);
				mob.SocialMeshLayer.AddMesh(array2[0], array2[1], array2[2], array2[3]);
			}
		}
	}
}
