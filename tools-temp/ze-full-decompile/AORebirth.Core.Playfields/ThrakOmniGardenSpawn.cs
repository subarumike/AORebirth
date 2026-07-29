using System;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Textures;
using AORebirth.Core.Vector;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Controllers;

namespace AORebirth.Core.Playfields;

internal static class ThrakOmniGardenSpawn
{
	private sealed class GardenNpc
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

	internal const int ThrakOmniGardenPlayfieldId = 4677;

	private const string TemplateHash = "BART";

	private static readonly GardenNpc[] Npcs = new GardenNpc[10]
	{
		new GardenNpc
		{
			Name = "Craig-Or of Flaming Barrels",
			Level = 30,
			Health = 32800,
			MonsterData = 208640,
			Scale = 200,
			VisualFlags = 31,
			HeadMesh = 0,
			X = 490.97415f,
			Y = 33.01f,
			Z = 311.79987f,
			Hx = 0f,
			Hy = -0.83695865f,
			Hz = 0f,
			Hw = 0.5472661f,
			Textures = null,
			Meshes = new int[1][] { new int[4] { 1, 209532, 0, 2 } }
		},
		new GardenNpc
		{
			Name = "Craig-Or of Gear & Ammo",
			Level = 30,
			Health = 32800,
			MonsterData = 208640,
			Scale = 200,
			VisualFlags = 31,
			HeadMesh = 0,
			X = 491.04044f,
			Y = 33.01f,
			Z = 305.69257f,
			Hx = 0f,
			Hy = -0.55779654f,
			Hz = 0f,
			Hw = 0.82997775f,
			Textures = null,
			Meshes = new int[1][] { new int[4] { 1, 209541, 0, 2 } }
		},
		new GardenNpc
		{
			Name = "Craig-Or of Preservation",
			Level = 30,
			Health = 32800,
			MonsterData = 208640,
			Scale = 200,
			VisualFlags = 31,
			HeadMesh = 0,
			X = 490.87967f,
			Y = 33.06743f,
			Z = 317.17175f,
			Hx = 0f,
			Hy = -0.67338574f,
			Hz = 0f,
			Hw = 0.7392913f,
			Textures = null,
			Meshes = new int[1][] { new int[4] { 1, 209532, 0, 2 } }
		},
		new GardenNpc
		{
			Name = "Craig-Or of Protection",
			Level = 30,
			Health = 32800,
			MonsterData = 208640,
			Scale = 200,
			VisualFlags = 31,
			HeadMesh = 0,
			X = 491.14374f,
			Y = 33.01f,
			Z = 299.77136f,
			Hx = 0f,
			Hy = -0.8378405f,
			Hz = 0f,
			Hw = 0.5459151f,
			Textures = null,
			Meshes = new int[1][] { new int[4] { 1, 209532, 0, 2 } }
		},
		new GardenNpc
		{
			Name = "Craig-Or of the Furious Fists",
			Level = 30,
			Health = 32800,
			MonsterData = 208640,
			Scale = 200,
			VisualFlags = 31,
			HeadMesh = 0,
			X = 491.39716f,
			Y = 33.041553f,
			Z = 323.11288f,
			Hx = 0f,
			Hy = 0.65590674f,
			Hz = 0f,
			Hw = 0.7548419f,
			Textures = null,
			Meshes = new int[1][] { new int[4] { 1, 209532, 0, 2 } }
		},
		new GardenNpc
		{
			Name = "Garboil Ixi Thrak",
			Level = 40,
			Health = 2320,
			MonsterData = 208635,
			Scale = 150,
			VisualFlags = 31,
			HeadMesh = 0,
			X = 445.24f,
			Y = 33.01f,
			Z = 522.82874f,
			Hx = 0f,
			Hy = 0.9999998f,
			Hz = 0f,
			Hw = -0.0005906065f,
			Textures = null,
			Meshes = new int[1][] { new int[4] { 1, 233207, 0, 2 } }
		},
		new GardenNpc
		{
			Name = "Hypnagogic Urga-Lum Thrak",
			Level = 40,
			Health = 2320,
			MonsterData = 208640,
			Scale = 200,
			VisualFlags = 31,
			HeadMesh = 0,
			X = 463.39557f,
			Y = 33.388638f,
			Z = 359.4417f,
			Hx = 0f,
			Hy = 0.9436898f,
			Hz = 0f,
			Hw = 0.33083153f,
			Textures = null,
			Meshes = new int[1][] { new int[4] { 1, 209541, 0, 2 } }
		},
		new GardenNpc
		{
			Name = "Operator Pi-Ixi Thrak",
			Level = 40,
			Health = 2320,
			MonsterData = 208640,
			Scale = 200,
			VisualFlags = 31,
			HeadMesh = 0,
			X = 320.01587f,
			Y = 25.01f,
			Z = 342.41293f,
			Hx = 0f,
			Hy = 0.70815986f,
			Hz = 0f,
			Hw = 0.7060521f,
			Textures = null,
			Meshes = new int[1][] { new int[4] { 1, 209541, 0, 2 } }
		},
		new GardenNpc
		{
			Name = "Son-Len, Official of Power",
			Level = 40,
			Health = 46400,
			MonsterData = 208646,
			Scale = 200,
			VisualFlags = 31,
			HeadMesh = 0,
			X = 447.16275f,
			Y = 33.01f,
			Z = 318.59845f,
			Hx = 0f,
			Hy = -0.34553078f,
			Hz = 0f,
			Hw = 0.9384074f,
			Textures = null,
			Meshes = new int[1][] { new int[4] { 1, 209541, 0, 2 } }
		},
		new GardenNpc
		{
			Name = "Visionist Eckel-Lum Thrak",
			Level = 40,
			Health = 2320,
			MonsterData = 208640,
			Scale = 200,
			VisualFlags = 31,
			HeadMesh = 0,
			X = 418.79776f,
			Y = 33.551888f,
			Z = 359.83264f,
			Hx = 0f,
			Hy = 0.35995758f,
			Hz = 0f,
			Hw = 0.9329687f,
			Textures = null,
			Meshes = new int[1][] { new int[4] { 1, 209532, 0, 2 } }
		}
	};

	public static void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 4677)
		{
			return;
		}
		int num = 0;
		for (int i = 0; i < Npcs.Length; i++)
		{
			if (SpawnOne(playfield, playfieldIdentity, activateNpc, Npcs[i]))
			{
				num++;
			}
		}
		LogUtil.Debug((DebugInfoDetail)128, "ThrakOmniGardenSpawn pf=" + ((Identity)(ref playfieldIdentity)).Instance + " spawned=" + num + "/" + Npcs.Length);
	}

	private static bool SpawnOne(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc, GardenNpc def)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected O, but got Unknown
		//IL_0066: Expected O, but got Unknown
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Expected O, but got Unknown
		NPCController nPCController = new NPCController();
		Character val = NonPlayerCharacterHandler.SpawnMobFromTemplate("BART", playfieldIdentity, new Coordinate
		{
			x = def.X,
			y = def.Y,
			z = def.Z
		}, new Quaternion((double)def.Hx, (double)def.Hy, (double)def.Hz, (double)def.Hw), (IController)(object)nPCController, def.Level);
		if (val == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "ThrakOmniGardenSpawn FAILED template=BART npc=" + def.Name);
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
		if (def.Textures != null && def.Textures.Length != 0)
		{
			((Dynel)val).Textures.Clear();
			int[][] textures = def.Textures;
			foreach (int[] array in textures)
			{
				if (array != null && array.Length >= 2 && array[1] > 0)
				{
					((Dynel)val).Textures.Add(new AOTextures(array[0], array[1]));
				}
			}
		}
		if (def.Meshes != null && def.Meshes.Length != 0)
		{
			((Dynel)val).MeshLayer.Clear();
			val.SocialMeshLayer.Clear();
			int[][] meshes = def.Meshes;
			foreach (int[] array2 in meshes)
			{
				if (array2 != null && array2.Length >= 4 && array2[1] > 0)
				{
					((Dynel)val).MeshLayer.AddMesh(array2[0], array2[1], array2[2], array2[3]);
					val.SocialMeshLayer.AddMesh(array2[0], array2[1], array2[2], array2[3]);
				}
			}
		}
		activateNpc((ICharacter)(object)val);
		return true;
	}
}
