using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Textures;
using AORebirth.Core.Vector;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core;
using ZoneEngine.Core.Controllers;

namespace AORebirth.Core.Playfields;

internal static class HoloDeckSpawn
{
	private sealed class HoloNpc
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

		public int CharacterFlags;

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

	private const int HoloDeckPlayfieldId = 7001;

	private static readonly HashSet<int> SpawnedPlayfields = new HashSet<int>();

	private const string TemplateHash = "BART";

	private static readonly HoloNpc[] Npcs = new HoloNpc[8]
	{
		new HoloNpc
		{
			Name = "Adeline Guerra [Freelancers Inc.]",
			Level = 210,
			Health = 31900,
			MonsterData = 26155,
			Scale = 130,
			VisualFlags = 31,
			HeadMesh = 40138,
			RunSpeed = 632,
			NpcFamily = 1020,
			CharacterFlags = 277615105,
			X = 213.4695f,
			Y = 1.02f,
			Z = 207.9243f,
			Hx = 0f,
			Hy = 0.000318f,
			Hz = 0f,
			Hw = 1f,
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
			}
		},
		new HoloNpc
		{
			Name = "Arbiter Vincenzo Palmiero",
			Level = 220,
			Health = 203721,
			MonsterData = 26092,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 40694,
			RunSpeed = 749,
			NpcFamily = 3,
			CharacterFlags = 277615105,
			X = 229.31f,
			Y = 1.02f,
			Z = 198.0397f,
			Hx = 0f,
			Hy = 0.002199f,
			Hz = 0f,
			Hw = 0.999998f,
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
				new int[4] { 0, 40694, 0, 4 },
				new int[4] { 1, 258459, 0, 2 },
				new int[4] { 3, 286446, 0, 0 }
			}
		},
		new HoloNpc
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
			CharacterFlags = 269226497,
			X = 221.8891f,
			Y = 1.02f,
			Z = 205.7225f,
			Hx = 0f,
			Hy = -0.926655f,
			Hz = 0f,
			Hw = 0.375912f,
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
			}
		},
		new HoloNpc
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
			CharacterFlags = 269226497,
			X = 221.9611f,
			Y = 1.02f,
			Z = 188.022f,
			Hx = 0f,
			Hy = -0.393288f,
			Hz = 0f,
			Hw = 0.919415f,
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
			}
		},
		new HoloNpc
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
			CharacterFlags = 269226497,
			X = 181.0215f,
			Y = 1.02f,
			Z = 193.9571f,
			Hx = 0f,
			Hy = 0.701076f,
			Hz = 0f,
			Hw = 0.713087f,
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
			}
		},
		new HoloNpc
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
			CharacterFlags = 269226497,
			X = 181.0337f,
			Y = 1.02f,
			Z = 199.8694f,
			Hx = 0f,
			Hy = 0.708745f,
			Hz = 0f,
			Hw = 0.705465f,
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
			}
		},
		new HoloNpc
		{
			Name = "Arbitration Drone",
			Level = 100,
			Health = 13658,
			MonsterData = 260229,
			Scale = 100,
			VisualFlags = 31,
			HeadMesh = 0,
			RunSpeed = 346,
			NpcFamily = 3,
			CharacterFlags = 277369345,
			X = 227.2182f,
			Y = 1.02f,
			Z = 193.0427f,
			Hx = 0f,
			Hy = -0.682641f,
			Hz = 0f,
			Hw = 0.730759f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 0 },
				new int[2] { 2, 0 },
				new int[2] { 3, 0 },
				new int[2] { 4, 0 }
			},
			Meshes = new int[0][]
		},
		new HoloNpc
		{
			Name = "RALPH",
			Level = 200,
			Health = 36434,
			MonsterData = 96056,
			Scale = 110,
			VisualFlags = 31,
			HeadMesh = 0,
			RunSpeed = 515,
			NpcFamily = 103,
			CharacterFlags = 277615105,
			X = 191.6574f,
			Y = 1.02f,
			Z = 197.0204f,
			Hx = 0f,
			Hy = -0.709236f,
			Hz = 0f,
			Hw = 0.704971f,
			Textures = new int[5][]
			{
				new int[2],
				new int[2] { 1, 0 },
				new int[2] { 2, 0 },
				new int[2] { 3, 0 },
				new int[2] { 4, 0 }
			},
			Meshes = new int[0][]
		}
	};

	internal static void ClearPlayfield(int playfieldInstance)
	{
		SpawnedPlayfields.Remove(playfieldInstance);
	}

	public static void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 7001)
		{
			return;
		}
		if (!SpawnedPlayfields.Add(((Identity)(ref playfieldIdentity)).Instance))
		{
			LogUtil.Debug((DebugInfoDetail)128, "HoloDeckSpawn skip duplicate pf=" + ((Identity)(ref playfieldIdentity)).Instance);
			return;
		}
		int num = 0;
		HoloNpc[] npcs = Npcs;
		foreach (HoloNpc def in npcs)
		{
			if (SpawnOne(playfield, playfieldIdentity, activateNpc, def))
			{
				num++;
			}
		}
		LogUtil.Debug((DebugInfoDetail)128, "HoloDeckSpawn pf=" + ((Identity)(ref playfieldIdentity)).Instance + " spawned=" + num + "/" + Npcs.Length);
	}

	private static bool SpawnOne(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc, HoloNpc def)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Expected O, but got Unknown
		//IL_006e: Expected O, but got Unknown
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Expected O, but got Unknown
		//IL_0285: Unknown result type (might be due to invalid IL or missing references)
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
			LogUtil.Debug((DebugInfoDetail)512, "HoloDeckSpawn FAILED template=BART npc=" + def.Name);
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
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(0, (uint)def.CharacterFlags);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(660, 0u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(389, 0u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(60, 0u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(368, 0u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(173, 3u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(174, 3u);
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

	private static void ApplyAppearance(Character mob, HoloNpc def)
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
