using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Textures;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core;
using ZoneEngine.Core.Controllers;

namespace AORebirth.Core.Playfields;

internal static class AndromedaIccHqSpawn
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

		public int RunSpeed;

		public int NpcFamily;

		public int LosHeight;

		public int CharacterFlags;

		public int AppearanceValue;

		public int Side;

		public int Breed;

		public int Gender;

		public int Race;

		public int Fatness;

		public int MovementMode;

		public float X;

		public float Y;

		public float Z;

		public float Hx;

		public float Hy;

		public float Hz;

		public float Hw;

		public int[][] Textures;

		public int[][] Meshes;

		public float[][] Waypoints;
	}

	private const int AndromedaPlayfieldId = 655;

	private static readonly HashSet<int> SpawnedPlayfields = new HashSet<int>();

	private static readonly byte[] ConstadExtendedTextureOverrideData = new byte[48]
	{
		0, 0, 7, 226, 77, 97, 116, 101, 114, 105,
		97, 108, 32, 35, 52, 54, 56, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 4, 39, 214,
		0, 0, 0, 0, 0, 0, 0, 0
	};

	private const string TemplateHash = "BART";

	private static readonly CityNpc[] Npcs = new CityNpc[52]
	{
		new CityNpc
		{
			Name = "Leet",
			Level = 1,
			Health = 20,
			MonsterData = 17655,
			Scale = 90,
			VisualFlags = 31,
			HeadMesh = 0,
			RunSpeed = 5,
			NpcFamily = 36,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1483,
			Side = 3,
			Breed = 6,
			Gender = 1,
			Race = 1,
			Fatness = 1,
			MovementMode = 2,
			X = 3239.9912f,
			Y = 39.925f,
			Z = 861.58905f,
			Hx = 0f,
			Hy = -0.2101f,
			Hz = 0f,
			Hw = 0.97768f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 0 },
				new int[2] { 2, 0 },
				new int[2] { 3, 0 },
				new int[2] { 4, 0 }
			},
			Meshes = null,
			Waypoints = new float[2][]
			{
				new float[3] { 3239.9912f, 39.925f, 861.58905f },
				new float[3] { 3239.1316f, 39.92641f, 865.15796f }
			}
		},
		new CityNpc
		{
			Name = "Dockworker",
			Level = 5,
			Health = 115,
			MonsterData = 26074,
			Scale = 93,
			VisualFlags = 31,
			HeadMesh = 40691,
			RunSpeed = 19,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3150.3035f,
			Y = 35.11f,
			Z = 830.0021f,
			Hx = 0f,
			Hy = -0.00267f,
			Hz = 0f,
			Hw = 1f,
			Textures = new int[5][]
			{
				new int[2] { 0, 295555 },
				new int[2] { 1, 295553 },
				new int[2] { 2, 295554 },
				new int[2] { 3, 295552 },
				new int[2] { 4, 295556 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 205120, 0, 2 },
				new int[4] { 0, 40691, 0, 4 },
				new int[4] { 1, 258954, 0, 2 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "ICC Peacekeeper",
			Level = 250,
			Health = 200000,
			MonsterData = 26090,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 40629,
			RunSpeed = 1100,
			NpcFamily = 0,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 2,
			X = 3156.3455f,
			Y = 35.11f,
			Z = 800.0122f,
			Hx = 0f,
			Hy = -0.11249f,
			Hz = 0f,
			Hw = 0.99365f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[4][]
			{
				new int[4] { 0, 265787, 286562, 2 },
				new int[4] { 0, 40629, 0, 4 },
				new int[4] { 1, 262556, 0, 2 },
				new int[4] { 3, 286467, 0, 0 }
			},
			Waypoints = new float[2][]
			{
				new float[3] { 3156.3455f, 35.11f, 800.0122f },
				new float[3] { 3153.9346f, 35.11f, 811.0076f }
			}
		},
		new CityNpc
		{
			Name = "Kate Hayes - Rubi-Ka Tours",
			Level = 100,
			Health = 6829,
			MonsterData = 262895,
			Scale = 112,
			VisualFlags = 31,
			HeadMesh = 40650,
			RunSpeed = 346,
			NpcFamily = 3,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3262.4136f,
			Y = 39.925f,
			Z = 861.05286f,
			Hx = 0f,
			Hy = -0.55578f,
			Hz = 0f,
			Hw = 0.83133f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 247955 },
				new int[2] { 2, 247989 },
				new int[2] { 3, 247909 },
				new int[2] { 4, 248030 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 204941, 0, 0 },
				new int[4] { 0, 40650, 0, 4 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "ICC Peacekeeper Commander",
			Level = 250,
			Health = 200000,
			MonsterData = 26092,
			Scale = 110,
			VisualFlags = 31,
			HeadMesh = 40694,
			RunSpeed = 1100,
			NpcFamily = 0,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 2,
			X = 3240.4778f,
			Y = 36.4838f,
			Z = 881.81525f,
			Hx = 0f,
			Hy = -0.83571f,
			Hz = 0f,
			Hw = 0.54917f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[4][]
			{
				new int[4] { 0, 265793, 286562, 2 },
				new int[4] { 0, 40694, 0, 4 },
				new int[4] { 1, 264698, 0, 2 },
				new int[4] { 3, 286446, 0, 0 }
			},
			Waypoints = new float[2][]
			{
				new float[3] { 3240.4778f, 36.4838f, 881.81525f },
				new float[3] { 3236.0886f, 37.40736f, 879.9187f }
			}
		},
		new CityNpc
		{
			Name = "ICC Peacekeeper",
			Level = 250,
			Health = 200000,
			MonsterData = 26090,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 40629,
			RunSpeed = 1100,
			NpcFamily = 0,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 2,
			X = 3245.6562f,
			Y = 35.11f,
			Z = 910.61005f,
			Hx = 0f,
			Hy = 0.71215f,
			Hz = 0f,
			Hw = 0.70203f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[4][]
			{
				new int[4] { 0, 265787, 286562, 2 },
				new int[4] { 0, 40629, 0, 4 },
				new int[4] { 1, 262556, 0, 2 },
				new int[4] { 3, 286467, 0, 0 }
			},
			Waypoints = new float[2][]
			{
				new float[3] { 3245.6562f, 35.11f, 910.61005f },
				new float[3] { 3251.7014f, 35.10998f, 910.5235f }
			}
		},
		new CityNpc
		{
			Name = "Cody Monkie",
			Level = 89,
			Health = 6671,
			MonsterData = 26092,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 40704,
			RunSpeed = 318,
			NpcFamily = 0,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 8,
			X = 3231.8354f,
			Y = 36.165f,
			Z = 946.1915f,
			Hx = 0f,
			Hy = 0.66442f,
			Hz = 0f,
			Hw = 0.74736f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 42253 },
				new int[2] { 2, 81913 },
				new int[2] { 3, 42252 },
				new int[2] { 4, 42250 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40704, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "ICC Secretary",
			Level = 220,
			Health = 101861,
			MonsterData = 284218,
			Scale = 90,
			VisualFlags = 31,
			HeadMesh = 40171,
			RunSpeed = 749,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1640,
			Side = 0,
			Breed = 3,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 8,
			X = 3245.5125f,
			Y = 35.715f,
			Z = 935.02893f,
			Hx = 0f,
			Hy = 1f,
			Hz = 0f,
			Hw = 1E-05f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 164968 },
				new int[2] { 2, 0 },
				new int[2] { 3, 22537 },
				new int[2] { 4, 22618 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40171, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "ICC Secretary",
			Level = 220,
			Health = 101861,
			MonsterData = 284218,
			Scale = 90,
			VisualFlags = 31,
			HeadMesh = 40171,
			RunSpeed = 749,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1640,
			Side = 0,
			Breed = 3,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 8,
			X = 3245.593f,
			Y = 35.715f,
			Z = 953.962f,
			Hx = 0f,
			Hy = 0.00132f,
			Hz = 0f,
			Hw = 1f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 164968 },
				new int[2] { 2, 0 },
				new int[2] { 3, 22537 },
				new int[2] { 4, 22618 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40171, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Arbiter's Guardian",
			Level = 220,
			Health = 101861,
			MonsterData = 165196,
			Scale = 125,
			VisualFlags = 31,
			HeadMesh = 40117,
			RunSpeed = 749,
			NpcFamily = 137,
			LosHeight = 0,
			CharacterFlags = 269226497,
			AppearanceValue = 1672,
			Side = 0,
			Breed = 4,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3244.0105f,
			Y = 35.715f,
			Z = 939.70355f,
			Hx = 0f,
			Hy = 1f,
			Hz = 0f,
			Hw = 0.00016f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 40117, 0, 4 },
				new int[4] { 1, 99154, 0, 2 },
				new int[4] { 3, 286466, 0, 0 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Arbiter's Guardian",
			Level = 220,
			Health = 101861,
			MonsterData = 165196,
			Scale = 125,
			VisualFlags = 31,
			HeadMesh = 40117,
			RunSpeed = 749,
			NpcFamily = 137,
			LosHeight = 0,
			CharacterFlags = 269226497,
			AppearanceValue = 1672,
			Side = 0,
			Breed = 4,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3247.3674f,
			Y = 35.715f,
			Z = 939.7799f,
			Hx = 0f,
			Hy = 1f,
			Hz = 0f,
			Hw = -0.00287f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 40117, 0, 4 },
				new int[4] { 1, 99154, 0, 2 },
				new int[4] { 3, 286466, 0, 0 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Des Morck",
			Level = 102,
			Health = 8480,
			MonsterData = 274385,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 223890,
			RunSpeed = 351,
			NpcFamily = 0,
			LosHeight = 0,
			CharacterFlags = 279450113,
			AppearanceValue = 1608,
			Side = 0,
			Breed = 2,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 8,
			X = 3259.5298f,
			Y = 36.175f,
			Z = 941.8552f,
			Hx = 0f,
			Hy = -0.76827f,
			Hz = 0f,
			Hw = 0.64013f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 22587 },
				new int[2] { 2, 248878 },
				new int[2] { 3, 22558 },
				new int[2] { 4, 22646 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 20076, 0, 0 },
				new int[4] { 0, 223890, 0, 4 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Peacekeeper Constad",
			Level = 200,
			Health = 72868,
			MonsterData = 26092,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 40694,
			RunSpeed = 515,
			NpcFamily = 3,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3277.3628f,
			Y = 35.555f,
			Z = 921.896f,
			Hx = 0f,
			Hy = -0.76602f,
			Hz = 0f,
			Hw = 0.64282f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[4][]
			{
				new int[4] { 0, 20110, 0, 0 },
				new int[4] { 0, 40694, 0, 4 },
				new int[4] { 1, 268615, 0, 2 },
				new int[4] { 3, 286446, 0, 0 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Confused Colonist",
			Level = 177,
			Health = 17394,
			MonsterData = 204067,
			Scale = 119,
			VisualFlags = 31,
			HeadMesh = 40117,
			RunSpeed = 447,
			NpcFamily = 100,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1416,
			Side = 0,
			Breed = 4,
			Gender = 1,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3319.996f,
			Y = 35.915f,
			Z = 876.43054f,
			Hx = 0f,
			Hy = 1f,
			Hz = 0f,
			Hw = -0.00233f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 21825 },
				new int[2] { 2, 9619 },
				new int[2] { 3, 21820 },
				new int[2] { 4, 21832 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40117, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Merchant",
			Level = 190,
			Health = 19332,
			MonsterData = 165186,
			Scale = 120,
			VisualFlags = 31,
			HeadMesh = 40681,
			RunSpeed = 454,
			NpcFamily = 100,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3305.7207f,
			Y = 36.16026f,
			Z = 871.3621f,
			Hx = 0f,
			Hy = -0.39142f,
			Hz = 0f,
			Hw = 0.92021f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 9611 },
				new int[2] { 2, 14050 },
				new int[2] { 3, 9604 },
				new int[2] { 4, 14034 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40681, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Colonist",
			Level = 157,
			Health = 14413,
			MonsterData = 165196,
			Scale = 118,
			VisualFlags = 31,
			HeadMesh = 40117,
			RunSpeed = 436,
			NpcFamily = 100,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1416,
			Side = 0,
			Breed = 4,
			Gender = 1,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3319.0283f,
			Y = 35.915f,
			Z = 860.0258f,
			Hx = 0f,
			Hy = -0.00179f,
			Hz = 0f,
			Hw = 1f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 22571 },
				new int[2] { 2, 9407 },
				new int[2] { 3, 156739 },
				new int[2] { 4, 8813 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40117, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Young Man",
			Level = 174,
			Health = 16947,
			MonsterData = 204985,
			Scale = 119,
			VisualFlags = 31,
			HeadMesh = 40700,
			RunSpeed = 445,
			NpcFamily = 100,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3314.6213f,
			Y = 35.915f,
			Z = 855.3346f,
			Hx = 0f,
			Hy = -0.00122f,
			Hz = 0f,
			Hw = 1f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 22582 },
				new int[2] { 2, 9450 },
				new int[2] { 3, 22553 },
				new int[2] { 4, 22641 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40700, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Old Lady",
			Level = 186,
			Health = 18736,
			MonsterData = 165178,
			Scale = 120,
			VisualFlags = 31,
			HeadMesh = 40660,
			RunSpeed = 452,
			NpcFamily = 100,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3314.1516f,
			Y = 35.915f,
			Z = 860.12427f,
			Hx = 0f,
			Hy = -0f,
			Hz = 0f,
			Hw = 1f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 0 },
				new int[2] { 2, 0 },
				new int[2] { 3, 0 },
				new int[2] { 4, 0 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40660, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Colonist",
			Level = 32,
			Health = 1156,
			MonsterData = 165179,
			Scale = 102,
			VisualFlags = 31,
			HeadMesh = 40624,
			RunSpeed = 110,
			NpcFamily = 100,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3307.4685f,
			Y = 35.915f,
			Z = 859.61743f,
			Hx = 0f,
			Hy = -0.8402f,
			Hz = 0f,
			Hw = 0.54227f,
			Textures = new int[5][]
			{
				new int[2] { 0, 155956 },
				new int[2] { 1, 155954 },
				new int[2] { 2, 14045 },
				new int[2] { 3, 155953 },
				new int[2] { 4, 155957 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40624, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Old Geezer",
			Level = 53,
			Health = 2520,
			MonsterData = 165212,
			Scale = 106,
			VisualFlags = 31,
			HeadMesh = 40116,
			RunSpeed = 183,
			NpcFamily = 100,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1416,
			Side = 0,
			Breed = 4,
			Gender = 1,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3306.849f,
			Y = 36.16614f,
			Z = 873.7261f,
			Hx = 0f,
			Hy = 0f,
			Hz = 0f,
			Hw = 1f,
			Textures = new int[5][]
			{
				new int[2] { 0, 155947 },
				new int[2] { 1, 296298 },
				new int[2] { 2, 9619 },
				new int[2] { 3, 155946 },
				new int[2] { 4, 155943 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40116, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Young Woman",
			Level = 38,
			Health = 1526,
			MonsterData = 165181,
			Scale = 103,
			VisualFlags = 31,
			HeadMesh = 40648,
			RunSpeed = 131,
			NpcFamily = 100,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3313.809f,
			Y = 35.915f,
			Z = 856.26105f,
			Hx = 0f,
			Hy = -0f,
			Hz = 0f,
			Hw = 1f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 14027 },
				new int[2] { 2, 14045 },
				new int[2] { 3, 22543 },
				new int[2] { 4, 30885 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40648, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Natalia Akcora",
			Level = 15,
			Health = 393,
			MonsterData = 26076,
			Scale = 97,
			VisualFlags = 31,
			HeadMesh = 40635,
			RunSpeed = 52,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3286.631f,
			Y = 35.11f,
			Z = 860.87915f,
			Hx = 0f,
			Hy = 0.43681f,
			Hz = 0f,
			Hw = 0.89955f,
			Textures = new int[5][]
			{
				new int[2] { 0, 284555 },
				new int[2] { 1, 247933 },
				new int[2] { 2, 284553 },
				new int[2] { 3, 247887 },
				new int[2] { 4, 284556 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40635, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Hologram of Adeline Guerra",
			Level = 200,
			Health = 26675,
			MonsterData = 26155,
			Scale = 130,
			VisualFlags = 31,
			HeadMesh = 40138,
			RunSpeed = 515,
			NpcFamily = 1020,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1896,
			Side = 0,
			Breed = 3,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3311.442f,
			Y = 35.11f,
			Z = 840.4632f,
			Hx = 0f,
			Hy = -0.67649f,
			Hz = 0f,
			Hw = 0.73646f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 247933 },
				new int[2] { 2, 247977 },
				new int[2] { 3, 247887 },
				new int[2] { 4, 248016 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 204941, 0, 0 },
				new int[4] { 0, 40138, 0, 4 },
				new int[4] { 1, 29084, 0, 2 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Jacinto Clemente",
			Level = 46,
			Health = 2020,
			MonsterData = 26139,
			Scale = 105,
			VisualFlags = 31,
			HeadMesh = 40279,
			RunSpeed = 158,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1608,
			Side = 0,
			Breed = 2,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3304.4133f,
			Y = 36.485f,
			Z = 855.23676f,
			Hx = 0f,
			Hy = -0.68719f,
			Hz = 0f,
			Hw = 0.72648f,
			Textures = new int[5][]
			{
				new int[2] { 0, 155947 },
				new int[2] { 1, 155944 },
				new int[2] { 2, 155945 },
				new int[2] { 3, 155946 },
				new int[2] { 4, 155943 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 40279, 0, 4 },
				new int[4] { 1, 258990, 0, 2 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Bored Traveller",
			Level = 70,
			Health = 3955,
			MonsterData = 165182,
			Scale = 108,
			VisualFlags = 31,
			HeadMesh = 40666,
			RunSpeed = 246,
			NpcFamily = 100,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 2,
			X = 3316.8079f,
			Y = 35.915f,
			Z = 872.65027f,
			Hx = 0f,
			Hy = 0.15215f,
			Hz = 0f,
			Hw = 0.98836f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 81912 },
				new int[2] { 2, 40903 },
				new int[2] { 3, 87439 },
				new int[2] { 4, 40907 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 20110, 0, 0 },
				new int[4] { 0, 40666, 0, 4 }
			},
			Waypoints = new float[2][]
			{
				new float[3] { 3316.8079f, 35.915f, 872.65027f },
				new float[3] { 3317.2534f, 35.91179f, 874.06506f }
			}
		},
		new CityNpc
		{
			Name = "Curious Colonist",
			Level = 134,
			Health = 10984,
			MonsterData = 165193,
			Scale = 116,
			VisualFlags = 31,
			HeadMesh = 40158,
			RunSpeed = 424,
			NpcFamily = 100,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1896,
			Side = 0,
			Breed = 3,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3311.4143f,
			Y = 36.485f,
			Z = 881.3146f,
			Hx = 0f,
			Hy = -0.75404f,
			Hz = 0f,
			Hw = 0.65683f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 40946 },
				new int[2] { 2, 42235 },
				new int[2] { 3, 40925 },
				new int[2] { 4, 40913 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40158, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "ICC Peacekeeper",
			Level = 250,
			Health = 200000,
			MonsterData = 26090,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 40629,
			RunSpeed = 1100,
			NpcFamily = 0,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 2,
			X = 3298.858f,
			Y = 35.11f,
			Z = 929.8612f,
			Hx = 0f,
			Hy = 0.71507f,
			Hz = 0f,
			Hw = 0.69905f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[4][]
			{
				new int[4] { 0, 265787, 286562, 2 },
				new int[4] { 0, 40629, 0, 4 },
				new int[4] { 1, 262556, 0, 2 },
				new int[4] { 3, 286467, 0, 0 }
			},
			Waypoints = new float[2][]
			{
				new float[3] { 3298.858f, 35.11f, 929.8612f },
				new float[3] { 3314.681f, 35.11f, 929.5027f }
			}
		},
		new CityNpc
		{
			Name = "Colonist",
			Level = 186,
			Health = 18736,
			MonsterData = 165192,
			Scale = 120,
			VisualFlags = 31,
			HeadMesh = 40267,
			RunSpeed = 452,
			NpcFamily = 100,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1608,
			Side = 0,
			Breed = 2,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3322.9905f,
			Y = 35.915f,
			Z = 854.61896f,
			Hx = 0f,
			Hy = -0.00378f,
			Hz = 0f,
			Hw = 0.99999f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 37030 },
				new int[2] { 2, 22595 },
				new int[2] { 3, 37032 },
				new int[2] { 4, 22626 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 45772, 0, 0 },
				new int[4] { 0, 40267, 0, 4 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Helpful Colonist",
			Level = 28,
			Health = 910,
			MonsterData = 165185,
			Scale = 101,
			VisualFlags = 31,
			HeadMesh = 40710,
			RunSpeed = 97,
			NpcFamily = 100,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3331.9265f,
			Y = 35.915f,
			Z = 869.87225f,
			Hx = 0f,
			Hy = 0f,
			Hz = 0f,
			Hw = 1f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 42255 },
				new int[2] { 2, 162142 },
				new int[2] { 3, 81907 },
				new int[2] { 4, 22640 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40710, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Middle-aged Guy",
			Level = 18,
			Health = 493,
			MonsterData = 165187,
			Scale = 98,
			VisualFlags = 31,
			HeadMesh = 40687,
			RunSpeed = 62,
			NpcFamily = 100,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3329.5906f,
			Y = 35.915f,
			Z = 856.39197f,
			Hx = 0f,
			Hy = 0.33176f,
			Hz = 0f,
			Hw = 0.94337f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 9610 },
				new int[2] { 2, 9616 },
				new int[2] { 3, 9602 },
				new int[2] { 4, 22639 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40687, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "ICC Peacekeeper",
			Level = 250,
			Health = 200000,
			MonsterData = 26090,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 40629,
			RunSpeed = 1100,
			NpcFamily = 0,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3350.153f,
			Y = 35.11f,
			Z = 870.1036f,
			Hx = 0f,
			Hy = 1f,
			Hz = 0f,
			Hw = 0.0022f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[4][]
			{
				new int[4] { 0, 265787, 286562, 2 },
				new int[4] { 0, 40629, 0, 4 },
				new int[4] { 1, 262556, 0, 2 },
				new int[4] { 3, 286467, 0, 0 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "ICC Peacekeeper",
			Level = 250,
			Health = 200000,
			MonsterData = 26090,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 40629,
			RunSpeed = 1100,
			NpcFamily = 0,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3351.2935f,
			Y = 35.11f,
			Z = 862.2358f,
			Hx = 0f,
			Hy = 0.00434f,
			Hz = 0f,
			Hw = 0.99999f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[4][]
			{
				new int[4] { 0, 265787, 286562, 2 },
				new int[4] { 0, 40629, 0, 4 },
				new int[4] { 1, 262556, 0, 2 },
				new int[4] { 3, 286467, 0, 0 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Traveller",
			Level = 191,
			Health = 19481,
			MonsterData = 165180,
			Scale = 120,
			VisualFlags = 31,
			HeadMesh = 40628,
			RunSpeed = 454,
			NpcFamily = 100,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3330.1335f,
			Y = 36.485f,
			Z = 882.1767f,
			Hx = 0f,
			Hy = 1f,
			Hz = 0f,
			Hw = -0.00042f,
			Textures = new int[5][]
			{
				new int[2] { 0, 85939 },
				new int[2] { 1, 30846 },
				new int[2] { 2, 30865 },
				new int[2] { 3, 30828 },
				new int[2] { 4, 30877 }
			},
			Meshes = new int[1][] { new int[4] { 0, 40628, 0, 4 } },
			Waypoints = null
		},
		new CityNpc
		{
			Name = "ICC Peacekeeper",
			Level = 250,
			Health = 200000,
			MonsterData = 26090,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 40629,
			RunSpeed = 1100,
			NpcFamily = 0,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3333.869f,
			Y = 35.11f,
			Z = 946.94336f,
			Hx = 0f,
			Hy = 0.99989f,
			Hz = 0f,
			Hw = 0.01509f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[4][]
			{
				new int[4] { 0, 265787, 286562, 2 },
				new int[4] { 0, 40629, 0, 4 },
				new int[4] { 1, 262556, 0, 2 },
				new int[4] { 3, 286467, 0, 0 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "ICC Peacekeeper",
			Level = 250,
			Health = 200000,
			MonsterData = 26090,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 40629,
			RunSpeed = 1100,
			NpcFamily = 0,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 2,
			X = 3269.0732f,
			Y = 35.11f,
			Z = 820.8667f,
			Hx = 0f,
			Hy = -0.58331f,
			Hz = 0f,
			Hw = 0.81225f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[4][]
			{
				new int[4] { 0, 265787, 286562, 2 },
				new int[4] { 0, 40629, 0, 4 },
				new int[4] { 1, 262556, 0, 2 },
				new int[4] { 3, 286467, 0, 0 }
			},
			Waypoints = new float[2][]
			{
				new float[3] { 3269.0732f, 35.11f, 820.8667f },
				new float[3] { 3259.6145f, 35.10998f, 824.0559f }
			}
		},
		new CityNpc
		{
			Name = "Arbiter's Guardian",
			Level = 220,
			Health = 101861,
			MonsterData = 165196,
			Scale = 125,
			VisualFlags = 31,
			HeadMesh = 40117,
			RunSpeed = 749,
			NpcFamily = 137,
			LosHeight = 0,
			CharacterFlags = 269226497,
			AppearanceValue = 1672,
			Side = 0,
			Breed = 4,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3311.9033f,
			Y = 35.11f,
			Z = 837.6541f,
			Hx = 0f,
			Hy = -0.60102f,
			Hz = 0f,
			Hw = 0.79924f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 40117, 0, 4 },
				new int[4] { 1, 99154, 0, 2 },
				new int[4] { 3, 286466, 0, 0 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "ICC Peacekeeper",
			Level = 250,
			Health = 200000,
			MonsterData = 26090,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 40629,
			RunSpeed = 1100,
			NpcFamily = 0,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 2,
			X = 3282.052f,
			Y = 35.11f,
			Z = 819.01526f,
			Hx = 0f,
			Hy = -0.94112f,
			Hz = 0f,
			Hw = 0.33807f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[4][]
			{
				new int[4] { 0, 265787, 286562, 2 },
				new int[4] { 0, 40629, 0, 4 },
				new int[4] { 1, 262556, 0, 2 },
				new int[4] { 3, 286467, 0, 0 }
			},
			Waypoints = new float[2][]
			{
				new float[3] { 3282.052f, 35.11f, 819.01526f },
				new float[3] { 3277.121f, 35.11f, 813.0375f }
			}
		},
		new CityNpc
		{
			Name = "Cedrick Gaviglia",
			Level = 79,
			Health = 4715,
			MonsterData = 26139,
			Scale = 109,
			VisualFlags = 31,
			HeadMesh = 223900,
			RunSpeed = 280,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1608,
			Side = 0,
			Breed = 2,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3211.5642f,
			Y = 35.11f,
			Z = 847.1196f,
			Hx = 0f,
			Hy = -0.70913f,
			Hz = 0f,
			Hw = 0.70508f,
			Textures = new int[5][]
			{
				new int[2] { 0, 155947 },
				new int[2] { 1, 155944 },
				new int[2] { 2, 155945 },
				new int[2] { 3, 155946 },
				new int[2] { 4, 155943 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 223900, 0, 4 },
				new int[4] { 1, 258990, 0, 2 }
			},
			Waypoints = new float[2][]
			{
				new float[3] { 3211.5642f, 35.11f, 847.1196f },
				new float[3] { 3243.5654f, 36.04992f, 851.8295f }
			}
		},
		new CityNpc
		{
			Name = "Robin Marksward",
			Level = 20,
			Health = 559,
			MonsterData = 26101,
			Scale = 99,
			VisualFlags = 31,
			HeadMesh = 40105,
			RunSpeed = 69,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1672,
			Side = 0,
			Breed = 4,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3235.6116f,
			Y = 35.11f,
			Z = 918.99817f,
			Hx = 0f,
			Hy = -0.00039f,
			Hz = 0f,
			Hw = 1f,
			Textures = new int[5][]
			{
				new int[2] { 0, 35585 },
				new int[2] { 1, 35589 },
				new int[2] { 2, 35587 },
				new int[2] { 3, 35586 },
				new int[2] { 4, 35588 }
			},
			Meshes = new int[5][]
			{
				new int[4] { 0, 20003, 35590, 2 },
				new int[4] { 0, 40105, 0, 4 },
				new int[4] { 2, 288657, 0, 2 },
				new int[4] { 3, 291884, 0, 0 },
				new int[4] { 4, 291884, 0, 0 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Representative of IPS",
			Level = 25,
			Health = 724,
			MonsterData = 26088,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 40687,
			RunSpeed = 86,
			NpcFamily = 137,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3185.8599f,
			Y = 35.915f,
			Z = 861.4865f,
			Hx = 0f,
			Hy = 0.70486f,
			Hz = 0f,
			Hw = 0.70935f,
			Textures = new int[5][]
			{
				new int[2] { 0, 213851 },
				new int[2] { 1, 213751 },
				new int[2] { 2, 213807 },
				new int[2] { 3, 213708 },
				new int[2] { 4, 213925 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 214654, 0, 2 },
				new int[4] { 0, 40687, 0, 4 },
				new int[4] { 5, 214715, 0, 0 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Douglass Guynes",
			Level = 26,
			Health = 786,
			MonsterData = 26139,
			Scale = 101,
			VisualFlags = 31,
			HeadMesh = 40282,
			RunSpeed = 90,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1608,
			Side = 0,
			Breed = 2,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3195.7017f,
			Y = 36.52931f,
			Z = 857.5897f,
			Hx = 0f,
			Hy = -0.96262f,
			Hz = 0f,
			Hw = 0.27086f,
			Textures = new int[5][]
			{
				new int[2] { 0, 155947 },
				new int[2] { 1, 155944 },
				new int[2] { 2, 155945 },
				new int[2] { 3, 155946 },
				new int[2] { 4, 155943 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 40282, 0, 4 },
				new int[4] { 1, 29084, 0, 2 }
			},
			Waypoints = new float[2][]
			{
				new float[3] { 3195.7017f, 36.52931f, 857.5897f },
				new float[3] { 3197.9058f, 36.52911f, 862.3629f }
			}
		},
		new CityNpc
		{
			Name = "Transportation Officer Darren Plush",
			Level = 220,
			Health = 101861,
			MonsterData = 26088,
			Scale = 125,
			VisualFlags = 31,
			HeadMesh = 40687,
			RunSpeed = 749,
			NpcFamily = 137,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3176.1582f,
			Y = 35.915f,
			Z = 880.26483f,
			Hx = 0f,
			Hy = 0.91929f,
			Hz = 0f,
			Hw = 0.39359f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[4][]
			{
				new int[4] { 0, 20110, 0, 0 },
				new int[4] { 0, 40687, 0, 4 },
				new int[4] { 1, 268615, 0, 2 },
				new int[4] { 3, 286446, 0, 0 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "ICC Peacekeeper",
			Level = 250,
			Health = 200000,
			MonsterData = 26090,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 40629,
			RunSpeed = 1100,
			NpcFamily = 0,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 2,
			X = 3187.4768f,
			Y = 35.11f,
			Z = 890.35455f,
			Hx = 0f,
			Hy = 0.79556f,
			Hz = 0f,
			Hw = 0.60588f,
			Textures = new int[5][]
			{
				new int[2] { 0, 286229 },
				new int[2] { 1, 286227 },
				new int[2] { 2, 286228 },
				new int[2] { 3, 286226 },
				new int[2] { 4, 286225 }
			},
			Meshes = new int[4][]
			{
				new int[4] { 0, 265787, 286562, 2 },
				new int[4] { 0, 40629, 0, 4 },
				new int[4] { 1, 262556, 0, 2 },
				new int[4] { 3, 286467, 0, 0 }
			},
			Waypoints = new float[2][]
			{
				new float[3] { 3187.4768f, 35.11f, 890.35455f },
				new float[3] { 3189.396f, 35.11f, 889.8253f }
			}
		},
		new CityNpc
		{
			Name = "Engineer Automaton I",
			Level = 5,
			Health = 138,
			MonsterData = 17649,
			Scale = 93,
			VisualFlags = 31,
			HeadMesh = 0,
			RunSpeed = 32,
			NpcFamily = 95,
			LosHeight = 0,
			CharacterFlags = 403182081,
			AppearanceValue = 1514,
			Side = 0,
			Breed = 7,
			Gender = 1,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3181.492f,
			Y = 35.915f,
			Z = 877.11926f,
			Hx = 0f,
			Hy = 0f,
			Hz = 0f,
			Hw = 1f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 0 },
				new int[2] { 2, 0 },
				new int[2] { 3, 0 },
				new int[2] { 4, 0 }
			},
			Meshes = null,
			Waypoints = new float[2][]
			{
				new float[3] { 3181.492f, 35.915f, 877.11926f },
				new float[3] { 3234.0876f, 35.61f, 834.31366f }
			}
		},
		new CityNpc
		{
			Name = "Dockworker",
			Level = 5,
			Health = 115,
			MonsterData = 26074,
			Scale = 93,
			VisualFlags = 31,
			HeadMesh = 40691,
			RunSpeed = 19,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3186.671f,
			Y = 35.11f,
			Z = 834.5417f,
			Hx = 0f,
			Hy = 0.45529f,
			Hz = 0f,
			Hw = 0.89034f,
			Textures = new int[5][]
			{
				new int[2] { 0, 295555 },
				new int[2] { 1, 295553 },
				new int[2] { 2, 295554 },
				new int[2] { 3, 295552 },
				new int[2] { 4, 295556 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 205120, 0, 2 },
				new int[4] { 0, 40691, 0, 4 },
				new int[4] { 1, 81800, 0, 2 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Dockworker",
			Level = 5,
			Health = 115,
			MonsterData = 26143,
			Scale = 93,
			VisualFlags = 31,
			HeadMesh = 40137,
			RunSpeed = 19,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1896,
			Side = 0,
			Breed = 3,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3174.3003f,
			Y = 35.11f,
			Z = 772.5452f,
			Hx = 0f,
			Hy = 0.45133f,
			Hz = 0f,
			Hw = 0.89236f,
			Textures = new int[5][]
			{
				new int[2] { 0, 295555 },
				new int[2] { 1, 295553 },
				new int[2] { 2, 295554 },
				new int[2] { 3, 295552 },
				new int[2] { 4, 295556 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 205118, 0, 2 },
				new int[4] { 0, 40137, 0, 4 },
				new int[4] { 1, 264730, 0, 2 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Dockworker",
			Level = 5,
			Health = 115,
			MonsterData = 26074,
			Scale = 93,
			VisualFlags = 31,
			HeadMesh = 40691,
			RunSpeed = 19,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3175.012f,
			Y = 35.11f,
			Z = 795.6567f,
			Hx = 0f,
			Hy = 0.4566f,
			Hz = 0f,
			Hw = 0.88967f,
			Textures = new int[5][]
			{
				new int[2] { 0, 295555 },
				new int[2] { 1, 295553 },
				new int[2] { 2, 295554 },
				new int[2] { 3, 295552 },
				new int[2] { 4, 295556 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 205120, 0, 2 },
				new int[4] { 0, 40691, 0, 4 },
				new int[4] { 1, 264730, 0, 2 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Dockworker",
			Level = 5,
			Health = 115,
			MonsterData = 203740,
			Scale = 93,
			VisualFlags = 31,
			HeadMesh = 40127,
			RunSpeed = 19,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1416,
			Side = 0,
			Breed = 4,
			Gender = 1,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3164.6506f,
			Y = 35.11f,
			Z = 783.6056f,
			Hx = 0f,
			Hy = 0.00395f,
			Hz = 0f,
			Hw = 0.99999f,
			Textures = new int[5][]
			{
				new int[2] { 0, 295555 },
				new int[2] { 1, 295553 },
				new int[2] { 2, 295554 },
				new int[2] { 3, 295552 },
				new int[2] { 4, 295556 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 205110, 0, 2 },
				new int[4] { 0, 40127, 0, 4 },
				new int[4] { 1, 258954, 0, 2 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Dockworker",
			Level = 5,
			Health = 115,
			MonsterData = 203740,
			Scale = 93,
			VisualFlags = 31,
			HeadMesh = 40127,
			RunSpeed = 19,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1416,
			Side = 0,
			Breed = 4,
			Gender = 1,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3206.9377f,
			Y = 35.11f,
			Z = 775.53357f,
			Hx = 0f,
			Hy = 0.4511f,
			Hz = 0f,
			Hw = 0.89247f,
			Textures = new int[5][]
			{
				new int[2] { 0, 295555 },
				new int[2] { 1, 295553 },
				new int[2] { 2, 295554 },
				new int[2] { 3, 295552 },
				new int[2] { 4, 295556 }
			},
			Meshes = new int[3][]
			{
				new int[4] { 0, 205110, 0, 2 },
				new int[4] { 0, 40127, 0, 4 },
				new int[4] { 1, 258954, 0, 2 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Fia Lou",
			Level = 10,
			Health = 227,
			MonsterData = 26090,
			Scale = 95,
			VisualFlags = 31,
			HeadMesh = 223846,
			RunSpeed = 34,
			NpcFamily = 137,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 1,
			X = 3240.556f,
			Y = 36.34524f,
			Z = 772.71185f,
			Hx = 0f,
			Hy = -0.99472f,
			Hz = 0f,
			Hw = 0.10267f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 247971 },
				new int[2] { 2, 248000 },
				new int[2] { 3, 247924 },
				new int[2] { 4, 248037 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 223846, 0, 4 },
				new int[4] { 2, 95786, 0, 2 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Polly Delenick",
			Level = 83,
			Health = 5053,
			MonsterData = 26090,
			Scale = 110,
			VisualFlags = 31,
			HeadMesh = 40644,
			RunSpeed = 295,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3297.4932f,
			Y = 35.11f,
			Z = 765.3954f,
			Hx = 0f,
			Hy = 0.62913f,
			Hz = 0f,
			Hw = 0.7773f,
			Textures = new int[5][]
			{
				new int[2] { 0, 155947 },
				new int[2] { 1, 155944 },
				new int[2] { 2, 155945 },
				new int[2] { 3, 155946 },
				new int[2] { 4, 155943 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 40644, 0, 4 },
				new int[4] { 1, 258990, 0, 2 }
			},
			Waypoints = null
		},
		new CityNpc
		{
			Name = "Karima Bunke",
			Level = 179,
			Health = 17692,
			MonsterData = 26090,
			Scale = 119,
			VisualFlags = 31,
			HeadMesh = 40637,
			RunSpeed = 508,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1832,
			Side = 0,
			Breed = 1,
			Gender = 3,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3266.016f,
			Y = 17.11f,
			Z = 750.6178f,
			Hx = 0f,
			Hy = -0.92655f,
			Hz = 0f,
			Hw = 0.37618f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 296303 },
				new int[2] { 2, 155955 },
				new int[2] { 3, 245697 },
				new int[2] { 4, 296305 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 40637, 0, 4 },
				new int[4] { 1, 258990, 0, 2 }
			},
			Waypoints = null
		}
	};

	internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)
	{
		if (string.Equals(name, "Peacekeeper Constad", StringComparison.Ordinal))
		{
			data = (byte[])ConstadExtendedTextureOverrideData.Clone();
			return true;
		}
		data = null;
		return false;
	}

	internal static bool NeedsNataliaScfuFlag7(string name)
	{
		return string.Equals(name, "Natalia Akcora", StringComparison.Ordinal);
	}

	internal static bool IsAndromedaCityNpcPlayfield(int playfieldInstance)
	{
		return playfieldInstance == 655;
	}

	internal static void ClearPlayfield(int playfieldInstance)
	{
		SpawnedPlayfields.Remove(playfieldInstance);
	}

	public static void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 655)
		{
			return;
		}
		if (!SpawnedPlayfields.Add(((Identity)(ref playfieldIdentity)).Instance))
		{
			LogUtil.Debug((DebugInfoDetail)128, "AndromedaIccHqSpawn skip duplicate pf=" + ((Identity)(ref playfieldIdentity)).Instance);
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
		LogUtil.Debug((DebugInfoDetail)128, "AndromedaIccHqSpawn pf=" + ((Identity)(ref playfieldIdentity)).Instance + " spawned=" + num + "/" + Npcs.Length);
	}

	private static bool SpawnOne(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc, CityNpc def)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Expected O, but got Unknown
		//IL_006e: Expected O, but got Unknown
		//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ff: Expected O, but got Unknown
		//IL_0332: Unknown result type (might be due to invalid IL or missing references)
		NPCController nPCController = new NPCController
		{
			AiProfile = NpcAiProfile.Social
		};
		Character val = NonPlayerCharacterHandler.SpawnMobFromTemplate("BART", playfieldIdentity, new Coordinate
		{
			x = def.X,
			y = def.Y,
			z = def.Z
		}, new Quaternion((double)def.Hx, (double)def.Hy, (double)def.Hz, (double)def.Hw), (IController)(object)nPCController, def.Level);
		if (val == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "AndromedaIccHqSpawn FAILED template=BART npc=" + def.Name);
			return false;
		}
		((Dynel)val).Name = def.Name;
		val.FirstName = string.Empty;
		val.LastName = string.Empty;
		((Dynel)val).Playfield = (IPlayfield)(object)playfield;
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(359, (uint)def.MonsterData);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(1, (uint)def.Health);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(27, (uint)def.Health);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(54, (uint)def.Level);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(673, (uint)def.VisualFlags);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(455, (uint)def.NpcFamily);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(466, (uint)def.LosHeight);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(0, (uint)def.CharacterFlags);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(33, (uint)def.Side);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(4, (uint)def.Breed);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(59, (uint)def.Gender);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(89, (uint)def.Race);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(47, (uint)def.Fatness);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(660, 0u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(389, 0u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(60, 0u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(368, 0u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(173, (uint)def.MovementMode);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(174, (uint)def.MovementMode);
		if (def.Scale > 0)
		{
			((Dynel)val).Stats.SetBaseValueWithoutTriggering(360, (uint)def.Scale);
		}
		if (def.HeadMesh > 0)
		{
			((Dynel)val).Stats.SetBaseValueWithoutTriggering(64, (uint)def.HeadMesh);
		}
		if (def.RunSpeed > 0)
		{
			((Dynel)val).Stats.SetBaseValueWithoutTriggering(156, (uint)def.RunSpeed);
		}
		ApplyAppearance(val, def);
		ApplyWaypoints(val, nPCController, def);
		((Dynel)val).Coordinates(new Coordinate
		{
			x = def.X,
			y = def.Y,
			z = def.Z
		});
		((Dynel)val).DoNotDoTimers = false;
		activateNpc((ICharacter)(object)val);
		if (string.Equals(def.Name, "Natalia Akcora", StringComparison.Ordinal))
		{
			AndromedaIccHqIdleGestureRuntime.RegisterNatalia((ICharacter)(object)val);
		}
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
		else if (def.HeadMesh > 0)
		{
			((Dynel)mob).MeshLayer.Clear();
			mob.SocialMeshLayer.Clear();
			((Dynel)mob).MeshLayer.AddMesh(0, def.HeadMesh, 0, 4);
			mob.SocialMeshLayer.AddMesh(0, def.HeadMesh, 0, 4);
		}
	}

	private static void ApplyWaypoints(Character mob, NPCController controller, CityNpc def)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		if (def.Waypoints != null && def.Waypoints.Length >= 2)
		{
			mob.Waypoints.Clear();
			float[][] waypoints = def.Waypoints;
			foreach (float[] array in waypoints)
			{
				mob.AddWaypoint(new Vector3((double)array[0], (double)array[1], (double)array[2]), false);
			}
			controller.State = (CharacterState)4;
		}
	}
}
