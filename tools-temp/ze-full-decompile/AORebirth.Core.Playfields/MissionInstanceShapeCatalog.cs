using System;

namespace AORebirth.Core.Playfields;

internal static class MissionInstanceShapeCatalog
{
	internal const int CapturedBuildingType = 51103;

	internal const int CapturedBuildingInstance = 14106692;

	internal static readonly MissionShape[] Shapes = new MissionShape[3]
	{
		new MissionShape
		{
			CapturedPlayfieldId = 1419310,
			SpawnX = 298.19897f,
			SpawnY = 5.01f,
			SpawnZ = 115.01001f,
			Npcs = new MissionNpc[33]
			{
				new MissionNpc
				{
					Name = "Berneice Cornelius",
					Role = MissionNpcRole.FindTarget,
					Level = 154,
					Health = 13965,
					MonsterData = 26076,
					Scale = 117,
					HeadMesh = 40635,
					X = 241.1f,
					Y = 5.0100017f,
					Z = 138.59999f,
					Hx = 0f,
					Hy = -0.74905443f,
					Hz = 0f,
					Hw = 0.66250855f,
					Textures = new int[4][]
					{
						new int[2] { 1, 81911 },
						new int[2] { 2, 81913 },
						new int[2] { 3, 81908 },
						new int[2] { 4, 81916 }
					},
					Meshes = new int[1][] { new int[4] { 0, 40635, 0, 4 } }
				},
				new MissionNpc
				{
					Name = "Bileswarm Breeder",
					Role = MissionNpcRole.Trash,
					Level = 157,
					Health = 14413,
					MonsterData = 31907,
					Scale = 118,
					HeadMesh = 0,
					X = 74.45385f,
					Y = 5.315f,
					Z = 183.65213f,
					Hx = 0f,
					Hy = 0.9989062f,
					Hz = 0f,
					Hw = 0.04675881f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Bileswarm Breeder",
					Role = MissionNpcRole.Trash,
					Level = 157,
					Health = 14413,
					MonsterData = 31907,
					Scale = 118,
					HeadMesh = 0,
					X = 34.641346f,
					Y = 5.0100017f,
					Z = 194.63011f,
					Hx = 0f,
					Hy = 0.094461694f,
					Hz = 0f,
					Hw = 0.9955285f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Bileswarm Breeder",
					Role = MissionNpcRole.Trash,
					Level = 156,
					Health = 14263,
					MonsterData = 31907,
					Scale = 117,
					HeadMesh = 0,
					X = 44.812546f,
					Y = 5.0604467f,
					Z = 242.73956f,
					Hx = 0f,
					Hy = 0.2377207f,
					Hz = 0f,
					Hw = 0.97133356f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Bileswarm Breeder",
					Role = MissionNpcRole.Trash,
					Level = 156,
					Health = 14263,
					MonsterData = 31907,
					Scale = 117,
					HeadMesh = 0,
					X = 46.444046f,
					Y = 5.01f,
					Z = 194.85928f,
					Hx = 0f,
					Hy = -0.5537137f,
					Hz = 0f,
					Hw = 0.8327071f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Bileswarm Breeder",
					Role = MissionNpcRole.Trash,
					Level = 156,
					Health = 14263,
					MonsterData = 31907,
					Scale = 117,
					HeadMesh = 0,
					X = 23.755198f,
					Y = 5.629463f,
					Z = 182.60423f,
					Hx = 0f,
					Hy = 0.7084841f,
					Hz = 0f,
					Hw = 0.7057268f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Bioarranged Beast - Model 666",
					Role = MissionNpcRole.Trash,
					Level = 157,
					Health = 14413,
					MonsterData = 17720,
					Scale = 118,
					HeadMesh = 0,
					X = 55.765907f,
					Y = 5.010002f,
					Z = 194.61255f,
					Hx = 0f,
					Hy = -0.5505029f,
					Hz = 0f,
					Hw = 0.83483326f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Bioarranged Beast - Model 666",
					Role = MissionNpcRole.Trash,
					Level = 140,
					Health = 11878,
					MonsterData = 17720,
					Scale = 116,
					HeadMesh = 0,
					X = 63.485733f,
					Y = 6.3625736f,
					Z = 203.34659f,
					Hx = -0.049754072f,
					Hy = 0.9975491f,
					Hz = 0.002450672f,
					Hw = 0.0491352f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Bioarranged Beast - Model 666",
					Role = MissionNpcRole.Trash,
					Level = 143,
					Health = 12325,
					MonsterData = 17720,
					Scale = 116,
					HeadMesh = 0,
					X = 70.47762f,
					Y = 5.315f,
					Z = 195.37454f,
					Hx = 0f,
					Hy = 0.9830113f,
					Hz = 0f,
					Hw = 0.18354493f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "CEO Guardian",
					Role = MissionNpcRole.KillGuard,
					Level = 215,
					Health = 34513,
					MonsterData = 227701,
					Scale = 125,
					HeadMesh = 0,
					X = 297.77545f,
					Y = 5.01f,
					Z = 113.04025f,
					Hx = 0f,
					Hy = 0f,
					Hz = 0f,
					Hw = 1f,
					Textures = null,
					Meshes = new int[1][] { new int[4] { 1, 273304, 0, 2 } }
				},
				new MissionNpc
				{
					Name = "Carlo Pinnetti",
					Role = MissionNpcRole.KillBoss,
					Level = 220,
					Health = 55687,
					MonsterData = 258209,
					Scale = 130,
					HeadMesh = 40121,
					X = 297.2772f,
					Y = 5.01f,
					Z = 118.65713f,
					Hx = 0f,
					Hy = 0f,
					Hz = 0f,
					Hw = 1f,
					Textures = new int[4][]
					{
						new int[2] { 1, 284557 },
						new int[2] { 2, 247977 },
						new int[2] { 3, 247887 },
						new int[2] { 4, 248016 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 204896, 0, 0 },
						new int[4] { 0, 40121, 0, 4 },
						new int[4] { 1, 29084, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Hellhound",
					Role = MissionNpcRole.Trash,
					Level = 148,
					Health = 26141,
					MonsterData = 17720,
					Scale = 117,
					HeadMesh = 0,
					X = 63.022923f,
					Y = 6.370332f,
					Z = 203.57503f,
					Hx = 0.00753578f,
					Hy = -0.9872682f,
					Hz = 0.049237866f,
					Hw = -0.15106435f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Hellhound",
					Role = MissionNpcRole.Trash,
					Level = 141,
					Health = 24054,
					MonsterData = 17720,
					Scale = 116,
					HeadMesh = 0,
					X = 82.9325f,
					Y = 5.0731044f,
					Z = 175.65854f,
					Hx = -0.01346237f,
					Hy = -0.5569894f,
					Hz = 0.068885095f,
					Hw = 0.82754844f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Hellhound",
					Role = MissionNpcRole.Trash,
					Level = 153,
					Health = 27632,
					MonsterData = 17720,
					Scale = 117,
					HeadMesh = 0,
					X = 64.49255f,
					Y = 5.142084f,
					Z = 253.75677f,
					Hx = 0.015101567f,
					Hy = 0.8406672f,
					Hz = -0.068579845f,
					Hw = 0.5369799f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Hellhound",
					Role = MissionNpcRole.Trash,
					Level = 150,
					Health = 26738,
					MonsterData = 17720,
					Scale = 117,
					HeadMesh = 0,
					X = 75.45261f,
					Y = 5.093064f,
					Z = 237.33803f,
					Hx = -0.021507995f,
					Hy = -0.455298f,
					Hz = 0.06681166f,
					Hw = 0.88756824f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Hellhound",
					Role = MissionNpcRole.Trash,
					Level = 152,
					Health = 27334,
					MonsterData = 17720,
					Scale = 117,
					HeadMesh = 0,
					X = 35.80704f,
					Y = 5.0100017f,
					Z = 154.87393f,
					Hx = 0f,
					Hy = -0.9503959f,
					Hz = 0f,
					Hw = 0.31104293f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Master Virusbuilder",
					Role = MissionNpcRole.Trash,
					Level = 155,
					Health = 14114,
					MonsterData = 26151,
					Scale = 117,
					HeadMesh = 40171,
					X = 53.682f,
					Y = 5.010039f,
					Z = 214.99f,
					Hx = 0f,
					Hy = 0.67991453f,
					Hz = 0f,
					Hw = 0.7332914f,
					Textures = new int[5][]
					{
						new int[2] { 0, 14048 },
						new int[2] { 1, 8731 },
						new int[2] { 2, 9457 },
						new int[2] { 3, 9455 },
						new int[2] { 4, 9456 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20030, 0, 2 },
						new int[4] { 0, 40171, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Master Virusbuilder",
					Role = MissionNpcRole.Trash,
					Level = 149,
					Health = 13220,
					MonsterData = 26151,
					Scale = 117,
					HeadMesh = 40171,
					X = 71.75723f,
					Y = 5.325133f,
					Z = 177.32295f,
					Hx = 0f,
					Hy = 0.77026314f,
					Hz = 0f,
					Hw = 0.6377262f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9454 },
						new int[2] { 1, 8731 },
						new int[2] { 2, 9457 },
						new int[2] { 3, 9455 },
						new int[2] { 4, 9456 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20030, 0, 2 },
						new int[4] { 0, 40171, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Medium Intestine Horror",
					Role = MissionNpcRole.Trash,
					Level = 152,
					Health = 19134,
					MonsterData = 40484,
					Scale = 117,
					HeadMesh = 0,
					X = 65.39021f,
					Y = 5.0698094f,
					Z = 217.16788f,
					Hx = -0.024404032f,
					Hy = -0.41606006f,
					Hz = 0.06580293f,
					Hw = 0.90662473f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Medium Intestine Horror",
					Role = MissionNpcRole.Trash,
					Level = 154,
					Health = 19551,
					MonsterData = 40484,
					Scale = 117,
					HeadMesh = 0,
					X = 84.01891f,
					Y = 5.2276745f,
					Z = 225.19487f,
					Hx = 0.1321659f,
					Hy = -0.9875636f,
					Hz = 0.01129481f,
					Hw = 0.08439653f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Medium Intestine Horror",
					Role = MissionNpcRole.Trash,
					Level = 148,
					Health = 18299,
					MonsterData = 40484,
					Scale = 117,
					HeadMesh = 0,
					X = 12.99f,
					Y = 5.0100036f,
					Z = 244.81317f,
					Hx = 0f,
					Hy = -0.8210568f,
					Hz = 0f,
					Hw = 0.5708465f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Medium Intestine Horror",
					Role = MissionNpcRole.Trash,
					Level = 157,
					Health = 20178,
					MonsterData = 40484,
					Scale = 118,
					HeadMesh = 0,
					X = 84.6421f,
					Y = 5.0100017f,
					Z = 205.97382f,
					Hx = 0f,
					Hy = -0.15028393f,
					Hz = 0f,
					Hw = 0.9886429f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Seasoned Bountyhunter",
					Role = MissionNpcRole.Trash,
					Level = 144,
					Health = 12475,
					MonsterData = 26097,
					Scale = 117,
					HeadMesh = 40111,
					X = 235.89929f,
					Y = 5.0100017f,
					Z = 113.23545f,
					Hx = 0f,
					Hy = -0.48240215f,
					Hz = 0f,
					Hw = 0.87594986f,
					Textures = new int[5][]
					{
						new int[2] { 0, 8745 },
						new int[2] { 1, 15813 },
						new int[2] { 2, 8743 },
						new int[2] { 3, 8730 },
						new int[2] { 4, 8747 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20002, 0, 2 },
						new int[4] { 0, 40111, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Seasoned Engineer",
					Role = MissionNpcRole.Trash,
					Level = 140,
					Health = 11878,
					MonsterData = 26103,
					Scale = 116,
					HeadMesh = 40103,
					X = 254.48994f,
					Y = 5.0100017f,
					Z = 104.654205f,
					Hx = 0f,
					Hy = -0.8778746f,
					Hz = 0f,
					Hw = 0.47889057f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9454 },
						new int[2] { 1, 8731 },
						new int[2] { 2, 22592 },
						new int[2] { 3, 9455 },
						new int[2] { 4, 22622 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 19997, 31719, 2 },
						new int[4] { 0, 40103, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Seasoned Hunter",
					Role = MissionNpcRole.Trash,
					Level = 142,
					Health = 12176,
					MonsterData = 26076,
					Scale = 116,
					HeadMesh = 40635,
					X = 233.71112f,
					Y = 5.0100017f,
					Z = 124.50838f,
					Hx = 0f,
					Hy = 0.4312552f,
					Hz = 0f,
					Hw = 0.90222996f,
					Textures = new int[5][]
					{
						new int[2] { 0, 8745 },
						new int[2] { 1, 8739 },
						new int[2] { 2, 8743 },
						new int[2] { 3, 15812 },
						new int[2] { 4, 8747 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20090, 0, 2 },
						new int[4] { 0, 40635, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Seasoned Trader",
					Role = MissionNpcRole.Trash,
					Level = 148,
					Health = 13071,
					MonsterData = 26082,
					Scale = 117,
					HeadMesh = 40634,
					X = 275.50824f,
					Y = 5.0100017f,
					Z = 94.40512f,
					Hx = 0f,
					Hy = 0.92865646f,
					Hz = 0f,
					Hw = 0.37094092f,
					Textures = new int[5][]
					{
						new int[2] { 0, 8816 },
						new int[2] { 1, 42244 },
						new int[2] { 2, 9450 },
						new int[2] { 3, 8815 },
						new int[2] { 4, 8813 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20082, 0, 2 },
						new int[4] { 0, 40634, 0, 4 },
						new int[4] { 1, 99154, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Skilled Clan Nanoshifter",
					Role = MissionNpcRole.Trash,
					Level = 146,
					Health = 12773,
					MonsterData = 26076,
					Scale = 117,
					HeadMesh = 40635,
					X = 232.67406f,
					Y = 5.0100017f,
					Z = 146.80197f,
					Hx = 0f,
					Hy = 0.9060736f,
					Hz = 0f,
					Hw = 0.42312017f,
					Textures = new int[5][]
					{
						new int[2] { 0, 8816 },
						new int[2] { 1, 42244 },
						new int[2] { 2, 8814 },
						new int[2] { 3, 42246 },
						new int[2] { 4, 42245 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20082, 0, 2 },
						new int[4] { 0, 40635, 0, 4 },
						new int[4] { 1, 99154, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Skilled Clan Robotbuilder",
					Role = MissionNpcRole.Trash,
					Level = 140,
					Health = 11878,
					MonsterData = 26082,
					Scale = 116,
					HeadMesh = 40634,
					X = 273.90112f,
					Y = 5.0100017f,
					Z = 134.07843f,
					Hx = 0f,
					Hy = -0.5244444f,
					Hz = 0f,
					Hw = 0.85144466f,
					Textures = new int[5][]
					{
						new int[2] { 0, 22605 },
						new int[2] { 1, 8731 },
						new int[2] { 2, 22592 },
						new int[2] { 3, 9455 },
						new int[2] { 4, 9456 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20081, 31719, 2 },
						new int[4] { 0, 40634, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Small Intestine Horror",
					Role = MissionNpcRole.Trash,
					Level = 144,
					Health = 17464,
					MonsterData = 40484,
					Scale = 117,
					HeadMesh = 0,
					X = 23.264277f,
					Y = 5.4691863f,
					Z = 181.9833f,
					Hx = -0.108457446f,
					Hy = 0.94683975f,
					Hz = -0.016884426f,
					Hw = 0.30240104f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Small Intestine Horror",
					Role = MissionNpcRole.Trash,
					Level = 140,
					Health = 16629,
					MonsterData = 40484,
					Scale = 116,
					HeadMesh = 0,
					X = 4.511465f,
					Y = 5.0100036f,
					Z = 233.65384f,
					Hx = 0f,
					Hy = -0.45377114f,
					Hz = 0f,
					Hw = 0.8911183f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Small Intestine Horror",
					Role = MissionNpcRole.Trash,
					Level = 142,
					Health = 17047,
					MonsterData = 40484,
					Scale = 116,
					HeadMesh = 0,
					X = 73.651474f,
					Y = 5.010039f,
					Z = 225.13136f,
					Hx = 0f,
					Hy = -0.95697016f,
					Hz = 0f,
					Hw = 0.29018638f,
					Textures = null,
					Meshes = null
				},
				new MissionNpc
				{
					Name = "Veteran Ruffian",
					Role = MissionNpcRole.Trash,
					Level = 156,
					Health = 19969,
					MonsterData = 26137,
					Scale = 117,
					HeadMesh = 40209,
					X = 240.4633f,
					Y = 5.0100017f,
					Z = 136.82443f,
					Hx = 0f,
					Hy = -0.7644909f,
					Hz = 0f,
					Hw = 0.6446345f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9418 },
						new int[2] { 1, 8729 },
						new int[2] { 2, 15807 },
						new int[2] { 3, 9419 },
						new int[2] { 4, 9421 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20055, 0, 2 },
						new int[4] { 0, 40209, 0, 4 },
						new int[4] { 1, 7826, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Veteran Ruffian",
					Role = MissionNpcRole.Trash,
					Level = 157,
					Health = 20178,
					MonsterData = 26137,
					Scale = 118,
					HeadMesh = 40209,
					X = 261.70084f,
					Y = 5.0100017f,
					Z = 146.90392f,
					Hx = 0f,
					Hy = 0.6260152f,
					Hz = 0f,
					Hw = 0.77981085f,
					Textures = new int[5][]
					{
						new int[2] { 0, 15806 },
						new int[2] { 1, 8729 },
						new int[2] { 2, 9420 },
						new int[2] { 3, 9419 },
						new int[2] { 4, 15805 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20055, 0, 2 },
						new int[4] { 0, 40209, 0, 4 },
						new int[4] { 1, 7826, 0, 2 }
					}
				}
			}
		},
		new MissionShape
		{
			CapturedPlayfieldId = 1419335,
			SpawnX = 298.19897f,
			SpawnY = 5.01f,
			SpawnZ = 145.01001f,
			Npcs = new MissionNpc[28]
			{
				new MissionNpc
				{
					Name = "Boosted Slugger",
					Role = MissionNpcRole.Trash,
					Level = 152,
					Health = 19134,
					MonsterData = 26137,
					Scale = 117,
					HeadMesh = 40209,
					X = 207.36794f,
					Y = 5.01f,
					Z = 117.61582f,
					Hx = 0f,
					Hy = -0.8912741f,
					Hz = 0f,
					Hw = 0.45346498f,
					Textures = new int[5][]
					{
						new int[2] { 0, 15806 },
						new int[2] { 1, 8729 },
						new int[2] { 2, 9420 },
						new int[2] { 3, 9419 },
						new int[2] { 4, 9421 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20055, 0, 2 },
						new int[4] { 0, 40209, 0, 4 },
						new int[4] { 1, 7826, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "CEO Guardian",
					Role = MissionNpcRole.KillGuard,
					Level = 215,
					Health = 34513,
					MonsterData = 227701,
					Scale = 125,
					HeadMesh = 0,
					X = 296.51007f,
					Y = 5.01f,
					Z = 147.65158f,
					Hx = 0f,
					Hy = 0f,
					Hz = 0f,
					Hw = 1f,
					Textures = null,
					Meshes = new int[1][] { new int[4] { 1, 273304, 0, 2 } }
				},
				new MissionNpc
				{
					Name = "Carlo Pinnetti",
					Role = MissionNpcRole.KillBoss,
					Level = 220,
					Health = 55687,
					MonsterData = 258209,
					Scale = 130,
					HeadMesh = 40121,
					X = 296.84543f,
					Y = 5.01f,
					Z = 148.63716f,
					Hx = 0f,
					Hy = 0f,
					Hz = 0f,
					Hw = 1f,
					Textures = new int[4][]
					{
						new int[2] { 1, 284557 },
						new int[2] { 2, 247977 },
						new int[2] { 3, 247887 },
						new int[2] { 4, 248016 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 204896, 0, 0 },
						new int[4] { 0, 40121, 0, 4 },
						new int[4] { 1, 29084, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Hardened Nanohoarder",
					Role = MissionNpcRole.Trash,
					Level = 142,
					Health = 9742,
					MonsterData = 26139,
					Scale = 116,
					HeadMesh = 40249,
					X = 275.11484f,
					Y = 5.01f,
					Z = 68.08682f,
					Hx = 0f,
					Hy = -0.70104617f,
					Hz = 0f,
					Hw = 0.7131159f,
					Textures = new int[5][]
					{
						new int[2] { 0, 40975 },
						new int[2] { 1, 9410 },
						new int[2] { 2, 9413 },
						new int[2] { 3, 9603 },
						new int[2] { 4, 9411 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20063, 0, 2 },
						new int[4] { 0, 40249, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Hardened Techrejecter",
					Role = MissionNpcRole.Trash,
					Level = 148,
					Health = 13071,
					MonsterData = 26135,
					Scale = 117,
					HeadMesh = 40271,
					X = 231.45473f,
					Y = 5.01f,
					Z = 134.75569f,
					Hx = 0f,
					Hy = -0.6502726f,
					Hz = 0f,
					Hw = 0.75970095f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9452 },
						new int[2] { 1, 9611 },
						new int[2] { 2, 9450 },
						new int[2] { 3, 9604 },
						new int[2] { 4, 9624 }
					},
					Meshes = new int[2][]
					{
						new int[4] { 0, 40271, 0, 4 },
						new int[4] { 1, 30238, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Master Virusbuilder",
					Role = MissionNpcRole.Trash,
					Level = 153,
					Health = 13816,
					MonsterData = 26151,
					Scale = 117,
					HeadMesh = 40171,
					X = 287.88126f,
					Y = 5.01f,
					Z = 173.47607f,
					Hx = 0f,
					Hy = -0.46716613f,
					Hz = 0f,
					Hw = 0.8841696f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9454 },
						new int[2] { 1, 8731 },
						new int[2] { 2, 9457 },
						new int[2] { 3, 9455 },
						new int[2] { 4, 9456 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20030, 0, 2 },
						new int[4] { 0, 40171, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Nichole Orender",
					Role = MissionNpcRole.FindTarget,
					Level = 154,
					Health = 13965,
					MonsterData = 26137,
					Scale = 117,
					HeadMesh = 40209,
					X = 225.01f,
					Y = 5.0100017f,
					Z = 115.01f,
					Hx = 0f,
					Hy = 0.97519225f,
					Hz = 0f,
					Hw = 0.22136015f,
					Textures = new int[4][]
					{
						new int[2] { 1, 81911 },
						new int[2] { 2, 81913 },
						new int[2] { 3, 81908 },
						new int[2] { 4, 81916 }
					},
					Meshes = new int[1][] { new int[4] { 0, 40209, 0, 4 } }
				},
				new MissionNpc
				{
					Name = "Rough Clan Informer",
					Role = MissionNpcRole.Trash,
					Level = 157,
					Health = 14413,
					MonsterData = 26139,
					Scale = 118,
					HeadMesh = 40249,
					X = 229.58536f,
					Y = 4.01f,
					Z = 105.451004f,
					Hx = 0f,
					Hy = -0.22136009f,
					Hz = 0f,
					Hw = 0.9751921f,
					Textures = new int[5][]
					{
						new int[2] { 0, 8816 },
						new int[2] { 1, 8740 },
						new int[2] { 2, 9450 },
						new int[2] { 3, 8815 },
						new int[2] { 4, 8813 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20075, 0, 2 },
						new int[4] { 0, 40249, 0, 4 },
						new int[4] { 1, 35542, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Seasoned Clan Bountyhunter",
					Role = MissionNpcRole.Trash,
					Level = 142,
					Health = 12176,
					MonsterData = 26088,
					Scale = 116,
					HeadMesh = 40687,
					X = 213.27684f,
					Y = 5.01f,
					Z = 135.01822f,
					Hx = 0f,
					Hy = -0.8209091f,
					Hz = 0f,
					Hw = 0.5710589f,
					Textures = new int[5][]
					{
						new int[2] { 0, 8745 },
						new int[2] { 1, 8739 },
						new int[2] { 2, 15811 },
						new int[2] { 3, 15812 },
						new int[2] { 4, 8747 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20107, 0, 2 },
						new int[4] { 0, 40687, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Seasoned Clan Doctor",
					Role = MissionNpcRole.Trash,
					Level = 141,
					Health = 12027,
					MonsterData = 26151,
					Scale = 116,
					HeadMesh = 40171,
					X = 254.58383f,
					Y = 5.01f,
					Z = 164.61899f,
					Hx = 0f,
					Hy = 0.9624015f,
					Hz = 0f,
					Hw = 0.27163094f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9454 },
						new int[2] { 1, 8731 },
						new int[2] { 2, 9616 },
						new int[2] { 3, 9455 },
						new int[2] { 4, 9623 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20030, 0, 2 },
						new int[4] { 0, 40171, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Seasoned Clan Soldier",
					Role = MissionNpcRole.Trash,
					Level = 144,
					Health = 12475,
					MonsterData = 26074,
					Scale = 117,
					HeadMesh = 40691,
					X = 274.97916f,
					Y = 5.01f,
					Z = 166.13135f,
					Hx = 0f,
					Hy = -0.34360078f,
					Hz = 0f,
					Hw = 0.9391158f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9620 },
						new int[2] { 1, 8729 },
						new int[2] { 2, 9420 },
						new int[2] { 3, 9605 },
						new int[2] { 4, 9425 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20095, 0, 2 },
						new int[4] { 0, 40691, 0, 4 },
						new int[4] { 1, 15839, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Seasoned Clan Soldier",
					Role = MissionNpcRole.Trash,
					Level = 145,
					Health = 12624,
					MonsterData = 26074,
					Scale = 117,
					HeadMesh = 40691,
					X = 230.97101f,
					Y = 5.01f,
					Z = 166.76198f,
					Hx = 0f,
					Hy = 0.9984639f,
					Hz = 0f,
					Hw = -0.055406805f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9418 },
						new int[2] { 1, 8729 },
						new int[2] { 2, 9420 },
						new int[2] { 3, 9419 },
						new int[2] { 4, 9421 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20095, 0, 2 },
						new int[4] { 0, 40691, 0, 4 },
						new int[4] { 1, 15839, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Skilled Clan Nanoshifter",
					Role = MissionNpcRole.Trash,
					Level = 143,
					Health = 12325,
					MonsterData = 26076,
					Scale = 116,
					HeadMesh = 40635,
					X = 274.638f,
					Y = 5.01f,
					Z = 94.10261f,
					Hx = 0f,
					Hy = -0.6587472f,
					Hz = 0f,
					Hw = 0.75236434f,
					Textures = new int[5][]
					{
						new int[2] { 0, 8816 },
						new int[2] { 1, 8740 },
						new int[2] { 2, 8814 },
						new int[2] { 3, 42246 },
						new int[2] { 4, 42245 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20082, 0, 2 },
						new int[4] { 0, 40635, 0, 4 },
						new int[4] { 1, 99154, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Skilled Clan Nanoshifter",
					Role = MissionNpcRole.Trash,
					Level = 142,
					Health = 12176,
					MonsterData = 26076,
					Scale = 116,
					HeadMesh = 40635,
					X = 222.24538f,
					Y = 5.01f,
					Z = 96.487144f,
					Hx = 0f,
					Hy = -0.30969062f,
					Hz = 0f,
					Hw = 0.9508374f,
					Textures = new int[5][]
					{
						new int[2] { 0, 8816 },
						new int[2] { 1, 42244 },
						new int[2] { 2, 8814 },
						new int[2] { 3, 8815 },
						new int[2] { 4, 42245 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20082, 0, 2 },
						new int[4] { 0, 40635, 0, 4 },
						new int[4] { 1, 99154, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Skilled Clan Robotbuilder",
					Role = MissionNpcRole.Trash,
					Level = 140,
					Health = 11878,
					MonsterData = 26082,
					Scale = 116,
					HeadMesh = 40634,
					X = 282.62366f,
					Y = 5.01f,
					Z = 163.25601f,
					Hx = 0f,
					Hy = 0.026496416f,
					Hz = 0f,
					Hw = 0.9996489f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9454 },
						new int[2] { 1, 8731 },
						new int[2] { 2, 22592 },
						new int[2] { 3, 9455 },
						new int[2] { 4, 9456 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20081, 0, 2 },
						new int[4] { 0, 40634, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Skilled Clan Robotbuilder",
					Role = MissionNpcRole.Trash,
					Level = 140,
					Health = 11878,
					MonsterData = 26082,
					Scale = 116,
					HeadMesh = 40634,
					X = 275.6466f,
					Y = 5.01f,
					Z = 119.56344f,
					Hx = 0f,
					Hy = -0.42183292f,
					Hz = 0f,
					Hw = 0.9066736f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9454 },
						new int[2] { 1, 8731 },
						new int[2] { 2, 9457 },
						new int[2] { 3, 9455 },
						new int[2] { 4, 9456 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20081, 0, 2 },
						new int[4] { 0, 40634, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Skilled Gridrunner",
					Role = MissionNpcRole.Trash,
					Level = 141,
					Health = 12027,
					MonsterData = 26092,
					Scale = 116,
					HeadMesh = 40694,
					X = 225.37433f,
					Y = 4.8105173f,
					Z = 147.07106f,
					Hx = 0f,
					Hy = -0.26302415f,
					Hz = 0f,
					Hw = 0.9647893f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9452 },
						new int[2] { 1, 22570 },
						new int[2] { 2, 22594 },
						new int[2] { 3, 9451 },
						new int[2] { 4, 22625 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20099, 0, 2 },
						new int[4] { 0, 40694, 0, 4 },
						new int[4] { 1, 15839, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Skilled Lasersniper",
					Role = MissionNpcRole.Trash,
					Level = 146,
					Health = 12773,
					MonsterData = 26101,
					Scale = 117,
					HeadMesh = 40105,
					X = 229.63072f,
					Y = 5.01f,
					Z = 159.65918f,
					Hx = 0f,
					Hy = 0.03183827f,
					Hz = 0f,
					Hw = 0.99949306f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9418 },
						new int[2] { 1, 8729 },
						new int[2] { 2, 9420 },
						new int[2] { 3, 9419 },
						new int[2] { 4, 9421 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20004, 0, 2 },
						new int[4] { 0, 40105, 0, 4 },
						new int[4] { 1, 15839, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Skilled Nanoshifter",
					Role = MissionNpcRole.Trash,
					Level = 140,
					Health = 11878,
					MonsterData = 26074,
					Scale = 116,
					HeadMesh = 40691,
					X = 272.68387f,
					Y = 5.01f,
					Z = 134.09315f,
					Hx = 0f,
					Hy = -0.72292846f,
					Hz = 0f,
					Hw = 0.6909229f,
					Textures = new int[5][]
					{
						new int[2] { 0, 8816 },
						new int[2] { 1, 42244 },
						new int[2] { 2, 9450 },
						new int[2] { 3, 8815 },
						new int[2] { 4, 8813 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20099, 0, 2 },
						new int[4] { 0, 40691, 0, 4 },
						new int[4] { 1, 99154, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Skilled Nanoshifter",
					Role = MissionNpcRole.Trash,
					Level = 140,
					Health = 11878,
					MonsterData = 26074,
					Scale = 116,
					HeadMesh = 40691,
					X = 242.33699f,
					Y = 5.01f,
					Z = 105.350746f,
					Hx = 0f,
					Hy = -0.6253892f,
					Hz = 0f,
					Hw = 0.78031296f,
					Textures = new int[5][]
					{
						new int[2] { 0, 8816 },
						new int[2] { 1, 8740 },
						new int[2] { 2, 9450 },
						new int[2] { 3, 42246 },
						new int[2] { 4, 8813 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20109, 0, 2 },
						new int[4] { 0, 40691, 0, 4 },
						new int[4] { 1, 99154, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Tough Bully",
					Role = MissionNpcRole.Trash,
					Level = 156,
					Health = 19969,
					MonsterData = 26101,
					Scale = 118,
					HeadMesh = 40105,
					X = 203.72089f,
					Y = 5.01f,
					Z = 106.86795f,
					Hx = 0f,
					Hy = -0.32806495f,
					Hz = 0f,
					Hw = 0.9446552f,
					Textures = new int[5][]
					{
						new int[2] { 0, 15806 },
						new int[2] { 1, 8729 },
						new int[2] { 2, 15807 },
						new int[2] { 3, 15808 },
						new int[2] { 4, 15805 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20005, 0, 2 },
						new int[4] { 0, 40105, 0, 4 },
						new int[4] { 1, 7826, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Tough Criminal",
					Role = MissionNpcRole.Trash,
					Level = 157,
					Health = 14413,
					MonsterData = 26090,
					Scale = 118,
					HeadMesh = 40629,
					X = 226.59564f,
					Y = 4.0099983f,
					Z = 118.08077f,
					Hx = 0f,
					Hy = 0.99979335f,
					Hz = 0f,
					Hw = 0.020328425f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9452 },
						new int[2] { 1, 9611 },
						new int[2] { 2, 9617 },
						new int[2] { 3, 9451 },
						new int[2] { 4, 9624 }
					},
					Meshes = new int[2][]
					{
						new int[4] { 0, 40629, 0, 4 },
						new int[4] { 1, 30238, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Tough Nanogun",
					Role = MissionNpcRole.Trash,
					Level = 156,
					Health = 11411,
					MonsterData = 26090,
					Scale = 117,
					HeadMesh = 40629,
					X = 242.85446f,
					Y = 5.01f,
					Z = 117.17839f,
					Hx = 0f,
					Hy = 0.47256672f,
					Hz = 0f,
					Hw = 0.8812949f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9409 },
						new int[2] { 1, 9410 },
						new int[2] { 2, 9413 },
						new int[2] { 3, 9603 },
						new int[2] { 4, 9411 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20080, 0, 2 },
						new int[4] { 0, 40629, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Tough Nanogun",
					Role = MissionNpcRole.Trash,
					Level = 156,
					Health = 11411,
					MonsterData = 26090,
					Scale = 117,
					HeadMesh = 40629,
					X = 266.5529f,
					Y = 5.01f,
					Z = 95.18013f,
					Hx = 0f,
					Hy = 0.75236446f,
					Hz = 0f,
					Hw = 0.65874714f,
					Textures = new int[5][]
					{
						new int[2] { 0, 40975 },
						new int[2] { 1, 9410 },
						new int[2] { 2, 9413 },
						new int[2] { 3, 9603 },
						new int[2] { 4, 9411 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20080, 0, 2 },
						new int[4] { 0, 40629, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Tough Plunderer",
					Role = MissionNpcRole.Trash,
					Level = 157,
					Health = 14413,
					MonsterData = 26135,
					Scale = 118,
					HeadMesh = 40271,
					X = 295.17554f,
					Y = 5.01f,
					Z = 153.8352f,
					Hx = 0f,
					Hy = -0.9983632f,
					Hz = 0f,
					Hw = 0.05719237f,
					Textures = new int[5][]
					{
						new int[2] { 0, 8816 },
						new int[2] { 1, 8732 },
						new int[2] { 2, 9450 },
						new int[2] { 3, 8815 },
						new int[2] { 4, 9453 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20065, 0, 2 },
						new int[4] { 0, 40271, 0, 4 },
						new int[4] { 1, 35542, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Tough Plunderer",
					Role = MissionNpcRole.Trash,
					Level = 157,
					Health = 14413,
					MonsterData = 26135,
					Scale = 118,
					HeadMesh = 40271,
					X = 257.76144f,
					Y = 5.01f,
					Z = 94.38502f,
					Hx = 0f,
					Hy = 0.8780799f,
					Hz = 0f,
					Hw = 0.47851408f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9452 },
						new int[2] { 1, 8740 },
						new int[2] { 2, 9450 },
						new int[2] { 3, 9451 },
						new int[2] { 4, 8813 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20065, 0, 2 },
						new int[4] { 0, 40271, 0, 4 },
						new int[4] { 1, 35542, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Tough Rascal",
					Role = MissionNpcRole.Trash,
					Level = 155,
					Health = 19761,
					MonsterData = 26101,
					Scale = 118,
					HeadMesh = 40105,
					X = 293.32196f,
					Y = 5.01f,
					Z = 95.395454f,
					Hx = 0f,
					Hy = 0.8237906f,
					Hz = 0f,
					Hw = 0.5668942f,
					Textures = new int[5][]
					{
						new int[2] { 0, 15806 },
						new int[2] { 1, 8729 },
						new int[2] { 2, 15807 },
						new int[2] { 3, 9419 },
						new int[2] { 4, 9421 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20005, 0, 2 },
						new int[4] { 0, 40105, 0, 4 },
						new int[4] { 1, 7826, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Tough Torpedo",
					Role = MissionNpcRole.Trash,
					Level = 154,
					Health = 19552,
					MonsterData = 26137,
					Scale = 118,
					HeadMesh = 40209,
					X = 293.68127f,
					Y = 5.01f,
					Z = 103.89765f,
					Hx = 0f,
					Hy = 0.42142805f,
					Hz = 0f,
					Hw = 0.90686184f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9418 },
						new int[2] { 1, 8729 },
						new int[2] { 2, 15807 },
						new int[2] { 3, 9419 },
						new int[2] { 4, 15805 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20055, 0, 2 },
						new int[4] { 0, 40209, 0, 4 },
						new int[4] { 1, 7826, 0, 2 }
					}
				}
			}
		},
		new MissionShape
		{
			CapturedPlayfieldId = 1419382,
			SpawnX = 1.8010254f,
			SpawnY = 5.01f,
			SpawnZ = 195.01001f,
			Npcs = new MissionNpc[16]
			{
				new MissionNpc
				{
					Name = "CEO Guardian",
					Role = MissionNpcRole.KillGuard,
					Level = 215,
					Health = 34513,
					MonsterData = 227701,
					Scale = 125,
					HeadMesh = 0,
					X = 5.6419463f,
					Y = 5.01f,
					Z = 196.77148f,
					Hx = 0f,
					Hy = 0f,
					Hz = 0f,
					Hw = 1f,
					Textures = null,
					Meshes = new int[1][] { new int[4] { 1, 273304, 0, 2 } }
				},
				new MissionNpc
				{
					Name = "Carlo Pinnetti",
					Role = MissionNpcRole.KillBoss,
					Level = 220,
					Health = 55687,
					MonsterData = 258209,
					Scale = 130,
					HeadMesh = 40121,
					X = 3.95614f,
					Y = 5.01f,
					Z = 196.73357f,
					Hx = 0f,
					Hy = 0f,
					Hz = 0f,
					Hw = 1f,
					Textures = new int[4][]
					{
						new int[2] { 1, 284557 },
						new int[2] { 2, 247977 },
						new int[2] { 3, 247887 },
						new int[2] { 4, 248016 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 204896, 0, 0 },
						new int[4] { 0, 40121, 0, 4 },
						new int[4] { 1, 29084, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Chae Aronstein",
					Role = MissionNpcRole.FindTarget,
					Level = 154,
					Health = 13965,
					MonsterData = 26137,
					Scale = 117,
					HeadMesh = 40209,
					X = 61.300003f,
					Y = 5.0100017f,
					Z = 151.29999f,
					Hx = 0f,
					Hy = 0f,
					Hz = 0f,
					Hw = 1f,
					Textures = new int[4][]
					{
						new int[2] { 1, 81911 },
						new int[2] { 2, 81913 },
						new int[2] { 3, 81908 },
						new int[2] { 4, 81916 }
					},
					Meshes = new int[1][] { new int[4] { 0, 40209, 0, 4 } }
				},
				new MissionNpc
				{
					Name = "Master Virusbuilder",
					Role = MissionNpcRole.Trash,
					Level = 157,
					Health = 14413,
					MonsterData = 26151,
					Scale = 118,
					HeadMesh = 40171,
					X = 37.71422f,
					Y = 5.0100017f,
					Z = 183.3908f,
					Hx = 0f,
					Hy = 0.7844009f,
					Hz = 0f,
					Hw = 0.6202542f,
					Textures = new int[5][]
					{
						new int[2] { 0, 14048 },
						new int[2] { 1, 8731 },
						new int[2] { 2, 9457 },
						new int[2] { 3, 9455 },
						new int[2] { 4, 9456 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20030, 0, 2 },
						new int[4] { 0, 40171, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Rough Clan Informer",
					Role = MissionNpcRole.Trash,
					Level = 154,
					Health = 13965,
					MonsterData = 26139,
					Scale = 117,
					HeadMesh = 40249,
					X = 44.684437f,
					Y = 5.01f,
					Z = 217.85327f,
					Hx = 0f,
					Hy = -0.06551641f,
					Hz = 0f,
					Hw = 0.9978515f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9452 },
						new int[2] { 1, 8732 },
						new int[2] { 2, 8814 },
						new int[2] { 3, 9451 },
						new int[2] { 4, 8813 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20075, 0, 2 },
						new int[4] { 0, 40249, 0, 4 },
						new int[4] { 1, 35542, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Seasoned Clan Agent",
					Role = MissionNpcRole.Trash,
					Level = 142,
					Health = 12176,
					MonsterData = 26133,
					Scale = 116,
					HeadMesh = 40251,
					X = 57.342514f,
					Y = 5.0100017f,
					Z = 168.07785f,
					Hx = 0f,
					Hy = -0.73064923f,
					Hz = 0f,
					Hw = 0.682753f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9452 },
						new int[2] { 1, 22570 },
						new int[2] { 2, 9450 },
						new int[2] { 3, 9451 },
						new int[2] { 4, 22625 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20065, 0, 2 },
						new int[4] { 0, 40251, 0, 4 },
						new int[4] { 1, 15839, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Seasoned Clan Bodyguard",
					Role = MissionNpcRole.Trash,
					Level = 141,
					Health = 16838,
					MonsterData = 26082,
					Scale = 116,
					HeadMesh = 40634,
					X = 74.6288f,
					Y = 5.01f,
					Z = 172.71416f,
					Hx = 0f,
					Hy = -0.9025372f,
					Hz = 0f,
					Hw = 0.43061182f,
					Textures = new int[5][]
					{
						new int[2] { 0, 15806 },
						new int[2] { 1, 8729 },
						new int[2] { 2, 15807 },
						new int[2] { 3, 15808 },
						new int[2] { 4, 15805 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20089, 0, 2 },
						new int[4] { 0, 40634, 0, 4 },
						new int[4] { 1, 7826, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Seasoned Clan Bountyhunter",
					Role = MissionNpcRole.Trash,
					Level = 142,
					Health = 12176,
					MonsterData = 26088,
					Scale = 116,
					HeadMesh = 40687,
					X = 63.92965f,
					Y = 5.01f,
					Z = 182.17554f,
					Hx = 0f,
					Hy = -0.92562914f,
					Hz = 0f,
					Hw = 0.3784319f,
					Textures = new int[5][]
					{
						new int[2] { 0, 15810 },
						new int[2] { 1, 8739 },
						new int[2] { 2, 15811 },
						new int[2] { 3, 8730 },
						new int[2] { 4, 15804 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20103, 0, 2 },
						new int[4] { 0, 40687, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Seasoned Clan Scout",
					Role = MissionNpcRole.Trash,
					Level = 149,
					Health = 13220,
					MonsterData = 26133,
					Scale = 117,
					HeadMesh = 40251,
					X = 53.330994f,
					Y = 5.0100017f,
					Z = 172.75266f,
					Hx = 0f,
					Hy = -0.80386555f,
					Hz = 0f,
					Hw = 0.594811f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9452 },
						new int[2] { 1, 8732 },
						new int[2] { 2, 9450 },
						new int[2] { 3, 9604 },
						new int[2] { 4, 9624 }
					},
					Meshes = new int[2][]
					{
						new int[4] { 0, 40251, 0, 4 },
						new int[4] { 1, 30238, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Seasoned Clan Spy",
					Role = MissionNpcRole.Trash,
					Level = 144,
					Health = 12475,
					MonsterData = 26076,
					Scale = 117,
					HeadMesh = 40635,
					X = 45.632477f,
					Y = 5.01f,
					Z = 186.44348f,
					Hx = 0f,
					Hy = -0.45154354f,
					Hz = 0f,
					Hw = 0.8922491f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9452 },
						new int[2] { 1, 8732 },
						new int[2] { 2, 22594 },
						new int[2] { 3, 9451 },
						new int[2] { 4, 9453 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20082, 31720, 2 },
						new int[4] { 0, 40635, 0, 4 },
						new int[4] { 1, 15839, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Skilled Clan Assassin",
					Role = MissionNpcRole.Trash,
					Level = 144,
					Health = 12475,
					MonsterData = 26135,
					Scale = 117,
					HeadMesh = 40271,
					X = 54.72754f,
					Y = 5.0100017f,
					Z = 162.41345f,
					Hx = 0f,
					Hy = 0.21456873f,
					Hz = 0f,
					Hw = 0.9767089f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9452 },
						new int[2] { 1, 8732 },
						new int[2] { 2, 9450 },
						new int[2] { 3, 9451 },
						new int[2] { 4, 22625 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20065, 31720, 2 },
						new int[4] { 0, 40271, 0, 4 },
						new int[4] { 1, 15839, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Skilled Clan Assassin",
					Role = MissionNpcRole.Trash,
					Level = 145,
					Health = 12624,
					MonsterData = 26135,
					Scale = 117,
					HeadMesh = 40271,
					X = 65.674614f,
					Y = 5.0100017f,
					Z = 204.46371f,
					Hx = 0f,
					Hy = -0.851207f,
					Hz = 0f,
					Hw = 0.52483004f,
					Textures = new int[5][]
					{
						new int[2] { 0, 22607 },
						new int[2] { 1, 22570 },
						new int[2] { 2, 9450 },
						new int[2] { 3, 22543 },
						new int[2] { 4, 9453 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20065, 0, 2 },
						new int[4] { 0, 40271, 0, 4 },
						new int[4] { 1, 15839, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Skilled Clan Nanoshifter",
					Role = MissionNpcRole.Trash,
					Level = 148,
					Health = 13071,
					MonsterData = 26076,
					Scale = 117,
					HeadMesh = 40635,
					X = 41.852818f,
					Y = 5.01f,
					Z = 236.80528f,
					Hx = 0f,
					Hy = -0.3048885f,
					Hz = 0f,
					Hw = 0.95238805f,
					Textures = new int[5][]
					{
						new int[2] { 0, 8816 },
						new int[2] { 1, 8740 },
						new int[2] { 2, 9450 },
						new int[2] { 3, 8815 },
						new int[2] { 4, 42245 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20092, 0, 2 },
						new int[4] { 0, 40635, 0, 4 },
						new int[4] { 1, 99154, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Tough Clan Diversionist",
					Role = MissionNpcRole.Trash,
					Level = 155,
					Health = 14115,
					MonsterData = 26125,
					Scale = 118,
					HeadMesh = 40215,
					X = 24.265875f,
					Y = 5.0100017f,
					Z = 185.32057f,
					Hx = 0f,
					Hy = 0.995621f,
					Hz = 0f,
					Hw = 0.093481325f,
					Textures = new int[5][]
					{
						new int[2] { 0, 22607 },
						new int[2] { 1, 8732 },
						new int[2] { 2, 22594 },
						new int[2] { 3, 22543 },
						new int[2] { 4, 22625 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20048, 31720, 2 },
						new int[4] { 0, 40215, 0, 4 },
						new int[4] { 1, 15839, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Tough Clan Diversionist",
					Role = MissionNpcRole.Trash,
					Level = 156,
					Health = 14264,
					MonsterData = 26125,
					Scale = 118,
					HeadMesh = 40215,
					X = 44.567753f,
					Y = 5.01f,
					Z = 218.73802f,
					Hx = 0f,
					Hy = 0.99785155f,
					Hz = 0f,
					Hw = 0.06551641f,
					Textures = new int[5][]
					{
						new int[2] { 0, 22607 },
						new int[2] { 1, 8732 },
						new int[2] { 2, 9450 },
						new int[2] { 3, 22543 },
						new int[2] { 4, 22625 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20048, 31720, 2 },
						new int[4] { 0, 40215, 0, 4 },
						new int[4] { 1, 15839, 0, 2 }
					}
				},
				new MissionNpc
				{
					Name = "Veteran Functionary",
					Role = MissionNpcRole.Trash,
					Level = 156,
					Health = 11411,
					MonsterData = 26155,
					Scale = 117,
					HeadMesh = 40138,
					X = 27.459465f,
					Y = 5.0100017f,
					Z = 172.9627f,
					Hx = 0f,
					Hy = -0.92278683f,
					Hz = 0f,
					Hw = 0.3853109f,
					Textures = new int[5][]
					{
						new int[2] { 0, 9452 },
						new int[2] { 1, 8732 },
						new int[2] { 2, 9450 },
						new int[2] { 3, 9451 },
						new int[2] { 4, 9623 }
					},
					Meshes = new int[3][]
					{
						new int[4] { 0, 20014, 0, 2 },
						new int[4] { 0, 40138, 0, 4 },
						new int[4] { 1, 7777, 0, 2 }
					}
				}
			}
		}
	};

	internal static MissionShape PickShape(int playfieldInstance, Random rng)
	{
		if (Shapes == null || Shapes.Length == 0)
		{
			return null;
		}
		for (int i = 0; i < Shapes.Length; i++)
		{
			if (Shapes[i].CapturedPlayfieldId == playfieldInstance)
			{
				return Shapes[i];
			}
		}
		if (rng == null)
		{
			rng = new Random(playfieldInstance);
		}
		return Shapes[Math.Abs(playfieldInstance) % Shapes.Length];
	}

	internal static byte[] GetGeneratorPayload(int playfieldInstance)
	{
		return (PickShape(playfieldInstance, null)?.CapturedPlayfieldId ?? playfieldInstance) switch
		{
			1419310 => new byte[165]
			{
				0, 0, 199, 159, 0, 215, 64, 72, 0, 0,
				0, 2, 0, 3, 0, 30, 0, 30, 0, 64,
				0, 0, 1, 85, 30, 30, 30, 0, 0, 0,
				21, 0, 19, 0, 0, 9, 3, 0, 100, 0,
				1, 7, 1, 0, 2, 0, 3, 11, 3, 0,
				32, 0, 2, 10, 0, 0, 97, 0, 4, 5,
				3, 0, 24, 0, 1, 6, 3, 0, 34, 0,
				5, 9, 3, 0, 21, 0, 6, 10, 3, 0,
				32, 0, 6, 12, 1, 0, 28, 0, 1, 13,
				0, 0, 11, 0, 3, 10, 3, 0, 51, 0,
				6, 4, 0, 0, 56, 0, 6, 8, 1, 0,
				59, 0, 8, 7, 1, 0, 56, 0, 7, 6,
				1, 0, 56, 0, 4, 5, 3, 0, 56, 0,
				1, 5, 0, 0, 56, 0, 0, 6, 3, 0,
				11, 0, 8, 9, 1, 0, 59, 0, 9, 12,
				1, 0, 11, 0, 3, 14, 1, 255, 255, 255,
				255, 255, 255, 255, 255
			}, 
			1419335 => new byte[189]
			{
				0, 0, 199, 159, 0, 215, 64, 70, 0, 0,
				0, 2, 0, 3, 0, 30, 0, 30, 0, 64,
				0, 0, 1, 64, 100, 100, 100, 0, 0, 0,
				25, 0, 33, 0, 29, 15, 2, 0, 76, 0,
				25, 13, 3, 0, 52, 0, 24, 16, 1, 0,
				54, 0, 27, 16, 0, 0, 62, 0, 22, 12,
				2, 0, 21, 0, 28, 13, 3, 0, 70, 0,
				21, 17, 2, 0, 58, 0, 28, 17, 0, 0,
				80, 0, 26, 19, 0, 0, 46, 0, 22, 15,
				3, 0, 18, 0, 22, 11, 0, 0, 16, 0,
				28, 12, 0, 0, 27, 0, 29, 14, 3, 0,
				6, 0, 24, 19, 1, 0, 6, 0, 20, 19,
				3, 0, 32, 0, 24, 18, 3, 0, 16, 0,
				20, 18, 3, 0, 14, 0, 20, 17, 3, 0,
				18, 0, 23, 16, 0, 0, 6, 0, 22, 20,
				2, 0, 1, 0, 21, 16, 0, 0, 6, 0,
				29, 19, 2, 0, 1, 0, 25, 20, 3, 0,
				1, 0, 29, 20, 1, 0, 6, 0, 27, 23,
				2, 255, 255, 255, 255, 255, 255, 255, 255
			}, 
			1419382 => new byte[105]
			{
				0, 0, 199, 159, 0, 215, 64, 69, 0, 0,
				0, 2, 0, 3, 0, 30, 0, 30, 0, 64,
				0, 0, 1, 65, 150, 150, 150, 0, 0, 0,
				11, 0, 52, 0, 0, 10, 0, 0, 65, 0,
				1, 9, 1, 0, 21, 0, 5, 13, 0, 0,
				30, 0, 4, 7, 3, 0, 34, 0, 3, 11,
				2, 0, 34, 0, 2, 12, 2, 0, 16, 0,
				6, 9, 1, 0, 53, 0, 6, 11, 3, 0,
				53, 0, 4, 11, 1, 0, 58, 0, 7, 12,
				3, 0, 15, 0, 4, 6, 0, 255, 255, 255,
				255, 255, 255, 255, 255
			}, 
			_ => null, 
		};
	}

	internal static int GetBuildingInstance(byte[] payload)
	{
		if (payload == null || payload.Length < 8)
		{
			return 14106692;
		}
		return (payload[4] << 24) | (payload[5] << 16) | (payload[6] << 8) | payload[7];
	}

	internal static bool IsCapturedShapePlayfield(int playfieldInstance)
	{
		for (int i = 0; i < Shapes.Length; i++)
		{
			if (Shapes[i].CapturedPlayfieldId == playfieldInstance)
			{
				return true;
			}
		}
		return false;
	}
}
