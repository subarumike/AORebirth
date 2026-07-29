using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Textures;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core;
using ZoneEngine.Core.Controllers;

namespace AORebirth.Core.Playfields;

internal static class AreteLandingSpawn
{
	private sealed class AreteNpc
	{
		public string Name;

		public int Level;

		public int Health;

		public int CurrentHealth;

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

		public int CaptureInstance;
	}

	private const int AreteLandingPlayfieldId = 6553;

	private static readonly HashSet<int> SpawnedPlayfields = new HashSet<int>();

	private const string TemplateHash = "BART";

	private static readonly AreteNpc[] Npcs = new AreteNpc[21]
	{
		new AreteNpc
		{
			Name = "Rex Larsson",
			Level = 15,
			Health = 511,
			MonsterData = 26074,
			Scale = 97,
			VisualFlags = 31,
			HeadMesh = 40691,
			RunSpeed = 52,
			NpcFamily = 137,
			LosHeight = 3000,
			CharacterFlags = 277615105,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3624.0613f,
			Y = 51.745f,
			Z = 787.76465f,
			Hx = 0f,
			Hy = -0.70806825f,
			Hz = 0f,
			Hw = 0.706144f,
			Textures = new int[5][]
			{
				new int[2] { 0, 295555 },
				new int[2] { 1, 295553 },
				new int[2] { 2, 295554 },
				new int[2] { 3, 295552 },
				new int[2] { 4, 295556 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 205120, 0, 2 },
				new int[4] { 0, 40691, 0, 4 }
			}
		},
		new AreteNpc
		{
			Name = "Marcus Stone",
			Level = 15,
			Health = 117800,
			MonsterData = 258744,
			Scale = 105,
			VisualFlags = 31,
			HeadMesh = 40667,
			RunSpeed = 52,
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
			X = 3630.1306f,
			Y = 40.984997f,
			Z = 824.1919f,
			Hx = 0f,
			Hy = -0.2588223f,
			Hz = 0f,
			Hw = -0.965926f,
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
				new int[4] { 0, 40667, 0, 4 },
				new int[4] { 1, 292936, 0, 2 }
			}
		},
		new AreteNpc
		{
			Name = "Flint Novak",
			Level = 20,
			Health = 559,
			MonsterData = 26133,
			Scale = 90,
			VisualFlags = 31,
			HeadMesh = 40251,
			RunSpeed = 69,
			NpcFamily = 137,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1608,
			Side = 0,
			Breed = 2,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3598.331f,
			Y = 5.1100006f,
			Z = 862.9781f,
			Hx = 0f,
			Hy = 0.49389103f,
			Hz = 0f,
			Hw = 0.8695238f,
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
				new int[4] { 0, 205116, 0, 2 },
				new int[4] { 0, 40251, 0, 4 },
				new int[4] { 1, 258983, 0, 2 }
			}
		},
		new AreteNpc
		{
			Name = "Alex Gibbs",
			CaptureInstance = 2028010593,
			Level = 20,
			Health = 559,
			MonsterData = 263050,
			Scale = 115,
			VisualFlags = 31,
			HeadMesh = 40137,
			RunSpeed = 73,
			NpcFamily = 137,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1896,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3520.7844f,
			Y = 5.1100006f,
			Z = 856.6935f,
			Hx = 0f,
			Hy = -0.6912034f,
			Hz = 0f,
			Hw = 0.72266024f,
			Textures = new int[5][]
			{
				new int[2] { 0, 265571 },
				new int[2] { 1, 265567 },
				new int[2] { 2, 265569 },
				new int[2] { 3, 265575 },
				new int[2] { 4, 265573 }
			},
			Meshes = new int[4][]
			{
				new int[4] { 0, 265714, 0, 2 },
				new int[4] { 0, 40137, 0, 4 },
				new int[4] { 1, 268617, 0, 2 },
				new int[4] { 5, 267981, 0, 0 }
			}
		},
		new AreteNpc
		{
			Name = "ICC Immigration Officer Bill",
			CaptureInstance = 2028010598,
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
			AppearanceValue = 6054,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3510.2f,
			Y = 5.1100006f,
			Z = 826.2723f,
			Hx = 0f,
			Hy = 0.6667155f,
			Hz = 0f,
			Hw = 0.74531233f,
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
				new int[4] { 0, 40687, 0, 4 },
				new int[4] { 1, 99154, 0, 2 },
				new int[4] { 3, 286446, 0, 0 }
			}
		},
		new AreteNpc
		{
			Name = "Bodyguard Logan Fixx",
			CaptureInstance = 2028010614,
			Level = 100,
			Health = 13658,
			MonsterData = 247041,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 0,
			RunSpeed = 346,
			NpcFamily = 105,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 1578,
			Side = 2,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3602.789f,
			Y = 8.145f,
			Z = 819.8485f,
			Hx = 0f,
			Hy = -0.6498167f,
			Hz = 0f,
			Hw = 0.76009095f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 0 },
				new int[2] { 2, 0 },
				new int[2] { 3, 0 },
				new int[2] { 4, 0 }
			},
			Meshes = new int[1][] { new int[4] { 1, 233232, 0, 2 } }
		},
		new AreteNpc
		{
			Name = "Desmond Calitri",
			CaptureInstance = 2028010615,
			Level = 20,
			Health = 559,
			MonsterData = 295565,
			Scale = 120,
			VisualFlags = 31,
			HeadMesh = 236703,
			RunSpeed = 69,
			NpcFamily = 137,
			LosHeight = 0,
			CharacterFlags = 277352961,
			AppearanceValue = 1672,
			Side = 0,
			Breed = 4,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3605.2034f,
			Y = 8.075f,
			Z = 826.8822f,
			Hx = 0f,
			Hy = -0.7337265f,
			Hz = 0f,
			Hw = 0.6794449f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 284557 },
				new int[2] { 2, 247977 },
				new int[2] { 3, 247887 },
				new int[2] { 4, 248016 }
			},
			Meshes = new int[1][] { new int[4] { 0, 236703, 0, 4 } }
		},
		new AreteNpc
		{
			Name = "Barry the Food Vendor",
			CaptureInstance = 2028010621,
			Level = 10,
			Health = 227,
			MonsterData = 26139,
			Scale = 95,
			VisualFlags = 31,
			HeadMesh = 40249,
			RunSpeed = 34,
			NpcFamily = 0,
			LosHeight = 0,
			CharacterFlags = 279450113,
			AppearanceValue = 1608,
			Side = 0,
			Breed = 2,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3516.7314f,
			Y = 6.8050003f,
			Z = 826.9838f,
			Hx = 0f,
			Hy = -0.6416705f,
			Hz = 0f,
			Hw = 0.76698154f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 30862 },
				new int[2] { 2, 40903 },
				new int[2] { 3, 30839 },
				new int[2] { 4, 30886 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 40249, 0, 4 },
				new int[4] { 1, 7777, 0, 2 }
			}
		},
		new AreteNpc
		{
			Name = "Bruiser",
			CaptureInstance = 2038539587,
			Level = 5,
			Health = 138,
			MonsterData = 26088,
			Scale = 93,
			VisualFlags = 31,
			HeadMesh = 40687,
			RunSpeed = 19,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 269226497,
			AppearanceValue = 1576,
			Side = 0,
			Breed = 1,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3555.9756f,
			Y = 5.1100006f,
			Z = 820.7616f,
			Hx = 0f,
			Hy = -0.9218881f,
			Hz = 0f,
			Hw = 0.3874562f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 81912 },
				new int[2] { 2, 81914 },
				new int[2] { 3, 81909 },
				new int[2] { 4, 81917 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 40687, 0, 4 },
				new int[4] { 1, 7826, 0, 2 }
			}
		},
		new AreteNpc
		{
			Name = "Kneebreaker Alfonzo Rizzolo",
			CaptureInstance = 2038559756,
			Level = 4,
			Health = 28,
			MonsterData = 165196,
			Scale = 110,
			VisualFlags = 31,
			HeadMesh = 40117,
			RunSpeed = 17,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 269226497,
			AppearanceValue = 1672,
			Side = 0,
			Breed = 4,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3580.7346f,
			Y = 8.055f,
			Z = 833.1199f,
			Hx = 0f,
			Hy = 0f,
			Hz = 0f,
			Hw = 1f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 81912 },
				new int[2] { 2, 81914 },
				new int[2] { 3, 81909 },
				new int[2] { 4, 81917 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 40117, 0, 4 },
				new int[4] { 1, 7826, 0, 2 }
			}
		},
		new AreteNpc
		{
			Name = "Obedience Enforcement",
			CaptureInstance = 2038539581,
			Level = 5,
			Health = 138,
			MonsterData = 165196,
			Scale = 110,
			VisualFlags = 31,
			HeadMesh = 40117,
			RunSpeed = 19,
			NpcFamily = 103,
			LosHeight = 0,
			CharacterFlags = 269226497,
			AppearanceValue = 1672,
			Side = 0,
			Breed = 4,
			Gender = 2,
			Race = 1,
			Fatness = 1,
			MovementMode = 3,
			X = 3573.0537f,
			Y = 5.1100006f,
			Z = 817.9967f,
			Hx = 0f,
			Hy = -0.57726145f,
			Hz = 0f,
			Hw = 0.8165594f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 81912 },
				new int[2] { 2, 81914 },
				new int[2] { 3, 81909 },
				new int[2] { 4, 81917 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 40117, 0, 4 },
				new int[4] { 1, 7826, 0, 2 }
			}
		},
		new AreteNpc
		{
			Name = "Protester",
			CaptureInstance = 2038330955,
			Level = 2,
			Health = 48,
			MonsterData = 203740,
			Scale = 91,
			VisualFlags = 31,
			HeadMesh = 40127,
			RunSpeed = 10,
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
			X = 3575.2505f,
			Y = 5.1100006f,
			Z = 825.5049f,
			Hx = 0f,
			Hy = 0.9942326f,
			Hz = 0f,
			Hw = 0.107245035f,
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
				new int[4] { 1, 284183, 0, 2 }
			}
		},
		new AreteNpc
		{
			Name = "Protester",
			CaptureInstance = 2038420827,
			Level = 2,
			Health = 48,
			MonsterData = 203740,
			Scale = 91,
			VisualFlags = 31,
			HeadMesh = 40127,
			RunSpeed = 10,
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
			X = 3566.3982f,
			Y = 5.1100006f,
			Z = 822.3082f,
			Hx = 0f,
			Hy = 0.9999995f,
			Hz = 0f,
			Hw = -0.0009882333f,
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
				new int[4] { 1, 284183, 0, 2 }
			}
		},
		new AreteNpc
		{
			Name = "Protester",
			CaptureInstance = 2038539602,
			Level = 2,
			Health = 48,
			MonsterData = 203740,
			Scale = 91,
			VisualFlags = 31,
			HeadMesh = 40127,
			RunSpeed = 10,
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
			X = 3592.0066f,
			Y = 5.1100006f,
			Z = 820.00696f,
			Hx = 0f,
			Hy = 0.63831466f,
			Hz = 0f,
			Hw = 0.76977557f,
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
				new int[4] { 1, 284183, 0, 2 }
			}
		},
		new AreteNpc
		{
			Name = "Protester",
			CaptureInstance = 2038539604,
			Level = 2,
			Health = 48,
			MonsterData = 203740,
			Scale = 91,
			VisualFlags = 31,
			HeadMesh = 40127,
			RunSpeed = 10,
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
			X = 3591.9934f,
			Y = 5.1100006f,
			Z = 824.00366f,
			Hx = 0f,
			Hy = 0.75312877f,
			Hz = 0f,
			Hw = 0.65787315f,
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
				new int[4] { 1, 284183, 0, 2 }
			}
		},
		new AreteNpc
		{
			Name = "Wounded Dockworker",
			Level = 1,
			Health = 32,
			CurrentHealth = 12,
			MonsterData = 296008,
			Scale = 90,
			VisualFlags = 31,
			HeadMesh = 40130,
			RunSpeed = 30,
			NpcFamily = 137,
			LosHeight = 3000,
			CharacterFlags = 277615105,
			AppearanceValue = 1416,
			Side = 0,
			Breed = 4,
			Gender = 1,
			Race = 1,
			Fatness = 1,
			MovementMode = 8,
			X = 3583.531f,
			Y = 40.965f,
			Z = 831.2881f,
			Hx = 0f,
			Hy = 0f,
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
			Meshes = new int[2][]
			{
				new int[4] { 0, 205110, 0, 2 },
				new int[4] { 0, 40130, 0, 4 }
			}
		},
		new AreteNpc
		{
			Name = "Wounded Dockworker",
			Level = 1,
			Health = 32,
			CurrentHealth = 12,
			MonsterData = 296008,
			Scale = 90,
			VisualFlags = 31,
			HeadMesh = 40130,
			RunSpeed = 30,
			NpcFamily = 137,
			LosHeight = 3000,
			CharacterFlags = 277615105,
			AppearanceValue = 1416,
			Side = 0,
			Breed = 4,
			Gender = 1,
			Race = 1,
			Fatness = 1,
			MovementMode = 8,
			X = 3605.379f,
			Y = 40.965f,
			Z = 838.2296f,
			Hx = 0f,
			Hy = 0f,
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
			Meshes = new int[2][]
			{
				new int[4] { 0, 205110, 0, 2 },
				new int[4] { 0, 40130, 0, 4 }
			}
		},
		new AreteNpc
		{
			Name = "Wounded Dockworker",
			Level = 1,
			Health = 32,
			CurrentHealth = 12,
			MonsterData = 296008,
			Scale = 90,
			VisualFlags = 31,
			HeadMesh = 40130,
			RunSpeed = 30,
			NpcFamily = 137,
			LosHeight = 3000,
			CharacterFlags = 277615105,
			AppearanceValue = 1416,
			Side = 0,
			Breed = 4,
			Gender = 1,
			Race = 1,
			Fatness = 1,
			MovementMode = 8,
			X = 3599.1118f,
			Y = 25.585f,
			Z = 878.1775f,
			Hx = 0f,
			Hy = 0.0021625846f,
			Hz = 0f,
			Hw = 0.9999977f,
			Textures = new int[5][]
			{
				new int[2] { 0, 295555 },
				new int[2] { 1, 295553 },
				new int[2] { 2, 295554 },
				new int[2] { 3, 295552 },
				new int[2] { 4, 295556 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 205110, 0, 2 },
				new int[4] { 0, 40130, 0, 4 }
			}
		},
		new AreteNpc
		{
			Name = "Dockworker",
			Level = 3,
			Health = 3495,
			MonsterData = 26137,
			Scale = 92,
			VisualFlags = 31,
			HeadMesh = 40209,
			RunSpeed = 13,
			NpcFamily = 137,
			LosHeight = 0,
			CharacterFlags = 268964353,
			AppearanceValue = 42824,
			Side = 0,
			Breed = 2,
			Gender = 3,
			Race = 41,
			Fatness = 1,
			MovementMode = 3,
			X = 3586.6052f,
			Y = 40.964996f,
			Z = 844.243f,
			Hx = 0f,
			Hy = -0.7891955f,
			Hz = 0f,
			Hw = 0.6141474f,
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
				new int[4] { 0, 205112, 0, 2 },
				new int[4] { 0, 40209, 0, 4 },
				new int[4] { 1, 292936, 0, 2 }
			}
		},
		new AreteNpc
		{
			Name = "Wounded Dockworker",
			Level = 1,
			Health = 32,
			CurrentHealth = 12,
			MonsterData = 296008,
			Scale = 90,
			VisualFlags = 31,
			HeadMesh = 40130,
			RunSpeed = 30,
			NpcFamily = 137,
			LosHeight = 3000,
			CharacterFlags = 277615105,
			AppearanceValue = 1416,
			Side = 0,
			Breed = 4,
			Gender = 1,
			Race = 1,
			Fatness = 1,
			MovementMode = 8,
			X = 3621.2922f,
			Y = 37.565f,
			Z = 855.1413f,
			Hx = 0f,
			Hy = 0.0012998787f,
			Hz = 0f,
			Hw = 0.99999917f,
			Textures = new int[5][]
			{
				new int[2] { 0, 295555 },
				new int[2] { 1, 295553 },
				new int[2] { 2, 295554 },
				new int[2] { 3, 295552 },
				new int[2] { 4, 295556 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 205110, 0, 2 },
				new int[4] { 0, 40130, 0, 4 }
			}
		},
		new AreteNpc
		{
			Name = "Wounded Dockworker",
			Level = 1,
			Health = 32,
			CurrentHealth = 12,
			MonsterData = 296008,
			Scale = 90,
			VisualFlags = 31,
			HeadMesh = 40130,
			RunSpeed = 30,
			NpcFamily = 137,
			LosHeight = 3000,
			CharacterFlags = 277615105,
			AppearanceValue = 1416,
			Side = 0,
			Breed = 4,
			Gender = 1,
			Race = 1,
			Fatness = 1,
			MovementMode = 8,
			X = 3620.278f,
			Y = 31.205f,
			Z = 873.66583f,
			Hx = 0f,
			Hy = -0.0020042963f,
			Hz = 0f,
			Hw = 0.999998f,
			Textures = new int[5][]
			{
				new int[2] { 0, 295555 },
				new int[2] { 1, 295553 },
				new int[2] { 2, 295554 },
				new int[2] { 3, 295552 },
				new int[2] { 4, 295556 }
			},
			Meshes = new int[2][]
			{
				new int[4] { 0, 205110, 0, 2 },
				new int[4] { 0, 40130, 0, 4 }
			}
		}
	};

	internal static void ClearPlayfield(int playfieldInstance)
	{
		SpawnedPlayfields.Remove(playfieldInstance);
	}

	public static void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 6553)
		{
			return;
		}
		if (!SpawnedPlayfields.Add(((Identity)(ref playfieldIdentity)).Instance))
		{
			TickEnsureMissingNpcs(playfield, playfieldIdentity, activateNpc);
			return;
		}
		int num = 0;
		try
		{
			AreteNpc[] npcs = Npcs;
			foreach (AreteNpc areteNpc in npcs)
			{
				try
				{
					if (SpawnOne(playfield, playfieldIdentity, activateNpc, areteNpc))
					{
						num++;
					}
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "AreteLandingSpawn exception npc=" + areteNpc.Name + " " + ex.GetType().Name + ": " + ex.Message);
				}
			}
		}
		finally
		{
			LogUtil.Debug((DebugInfoDetail)128, "AreteLandingSpawn pf=" + ((Identity)(ref playfieldIdentity)).Instance + " spawned=" + num + "/" + Npcs.Length + " source=20260720-105157");
			if (num == 0)
			{
				SpawnedPlayfields.Remove(((Identity)(ref playfieldIdentity)).Instance);
			}
		}
	}

	internal static void TickEnsureMissingNpcs(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 6553)
		{
			return;
		}
		AreteNpc[] npcs = Npcs;
		foreach (AreteNpc areteNpc in npcs)
		{
			if (!IsNpcPresent(playfield, areteNpc))
			{
				try
				{
					SpawnOne(playfield, playfieldIdentity, activateNpc, areteNpc);
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "AreteLandingSpawn ensure exception npc=" + areteNpc.Name + " " + ex.GetType().Name + ": " + ex.Message);
				}
			}
		}
	}

	internal static void TickEnsureQuestNpcs(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		TickEnsureMissingNpcs(playfield, playfieldIdentity, activateNpc);
	}

	private static bool IsNpcPresent(Playfield playfield, AreteNpc def)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		if (def.CaptureInstance != 0)
		{
			Identity identity = default(Identity);
			((Identity)(ref identity)).Type = (IdentityType)50000;
			((Identity)(ref identity)).Instance = def.CaptureInstance;
			ICharacter val = playfield.FindByIdentity<ICharacter>(identity);
			if (val != null && ((IStats)val).Stats[(StatIds)27].Value > 0)
			{
				return true;
			}
		}
		foreach (ICharacter item in playfield.EnumerateActiveCharacters())
		{
			if (item != null && ((IStats)item).Stats[(StatIds)27].Value > 0 && string.Equals(((INamedEntity)item).Name, def.Name, StringComparison.OrdinalIgnoreCase))
			{
				Coordinate val2 = ((IDynel)item).Coordinates();
				float num = val2.x - def.X;
				float num2 = val2.z - def.Z;
				if (num * num + num2 * num2 <= 6.25f)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool SpawnOne(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc, AreteNpc def)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected O, but got Unknown
		//IL_0085: Expected O, but got Unknown
		//IL_0421: Unknown result type (might be due to invalid IL or missing references)
		//IL_0426: Unknown result type (might be due to invalid IL or missing references)
		//IL_0433: Unknown result type (might be due to invalid IL or missing references)
		//IL_0440: Unknown result type (might be due to invalid IL or missing references)
		//IL_0452: Expected O, but got Unknown
		//IL_0465: Unknown result type (might be due to invalid IL or missing references)
		if (IsNpcPresent(playfield, def))
		{
			return true;
		}
		NPCController nPCController = new NPCController
		{
			AiProfile = NpcAiProfile.Social
		};
		Character val;
		try
		{
			val = NonPlayerCharacterHandler.SpawnMobFromTemplate("BART", playfieldIdentity, new Coordinate
			{
				x = def.X,
				y = def.Y,
				z = def.Z
			}, new Quaternion((double)def.Hx, (double)def.Hy, (double)def.Hz, (double)def.Hw), (IController)(object)nPCController, def.Level);
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, "AreteLandingSpawn SpawnMobFromTemplate threw npc=" + def.Name + " " + ex.GetType().Name + ": " + ex.Message);
			return false;
		}
		if (val == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "AreteLandingSpawn FAILED template=BART npc=" + def.Name);
			return false;
		}
		((Dynel)val).Name = def.Name;
		val.FirstName = string.Empty;
		val.LastName = string.Empty;
		((Dynel)val).Playfield = (IPlayfield)(object)playfield;
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(359, (uint)def.MonsterData);
		((Dynel)val).Stats[(StatIds)359].Value = def.MonsterData;
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(1, (uint)def.Health);
		int num = ((def.CurrentHealth > 0) ? def.CurrentHealth : def.Health);
		if (num > def.Health)
		{
			num = def.Health;
		}
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(27, (uint)num);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(54, (uint)def.Level);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(673, (uint)def.VisualFlags);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(455, (uint)def.NpcFamily);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(466, (uint)def.LosHeight);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(0, (uint)def.CharacterFlags);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(33, (uint)def.Side);
		((Dynel)val).Stats[(StatIds)33].Value = def.Side;
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(4, (uint)def.Breed);
		((Dynel)val).Stats[(StatIds)4].Value = def.Breed;
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(59, (uint)def.Gender);
		((Dynel)val).Stats[(StatIds)59].Value = def.Gender;
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(89, (uint)def.Race);
		((Dynel)val).Stats[(StatIds)89].Value = def.Race;
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(47, (uint)def.Fatness);
		((Dynel)val).Stats[(StatIds)47].Value = def.Fatness;
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(660, 0u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(389, 0u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(60, 0u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(368, 0u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(173, (uint)def.MovementMode);
		uint num2 = ((def.MovementMode == 8) ? 3u : ((uint)def.MovementMode));
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(174, num2);
		if (def.Scale > 0)
		{
			((Dynel)val).Stats.SetBaseValueWithoutTriggering(360, (uint)def.Scale);
		}
		if (def.HeadMesh > 0)
		{
			((Dynel)val).Stats.SetBaseValueWithoutTriggering(64, (uint)def.HeadMesh);
		}
		else
		{
			((Dynel)val).Stats.SetBaseValueWithoutTriggering(64, 0u);
		}
		if (def.RunSpeed > 0)
		{
			((Dynel)val).Stats.SetBaseValueWithoutTriggering(156, (uint)def.RunSpeed);
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

	private static void ApplyAppearance(Character mob, AreteNpc def)
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
		if (def.Meshes != null)
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
}
