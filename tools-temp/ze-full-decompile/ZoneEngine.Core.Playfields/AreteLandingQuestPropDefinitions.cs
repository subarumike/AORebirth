using System;
using System.Collections.Generic;
using AORebirth.Core.Items;
using AORebirth.Core.Vector;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace ZoneEngine.Core.Playfields;

internal static class AreteLandingQuestPropDefinitions
{
	private sealed class PropDefinition
	{
		public int Instance;

		public int TemplateId;

		public int Flags;

		public float X;

		public float Y;

		public float Z;

		public float Hx;

		public float Hy;

		public float Hz;

		public float Hw;

		public string Evidence;
	}

	private const int AreteLandingPlayfieldId = 6553;

	private const int CargoBoxTemplateId = 297277;

	private const int GasFireTemplateId = 295883;

	private const int JunkTemplateId = 297284;

	private const int PrizedHouseplantTemplateId = 295738;

	private const int CargoBoxFlags = 139265;

	private const int GasFireFlags = -2146819551;

	private const int JunkFlags = -2146967039;

	private const int PrizedHouseplantFlags = -2147470847;

	private static readonly PropDefinition[] Props = new PropDefinition[11]
	{
		new PropDefinition
		{
			Instance = 1457108143,
			TemplateId = 297277,
			Flags = 139265,
			X = 3621.576f,
			Y = 51.745f,
			Z = 780.4768f,
			Hx = 0f,
			Hy = (float)Math.E * -29f / 111f,
			Hz = 0f,
			Hw = 0.7040185f,
			Evidence = "20260614-205724 SIFU Terminal:56D9B4AF"
		},
		new PropDefinition
		{
			Instance = 1469456042,
			TemplateId = 295883,
			Flags = -2146819551,
			X = 3599.267f,
			Y = 42.75448f,
			Z = 843.9763f,
			Hx = 0f,
			Hy = 0.003129918f,
			Hz = 0f,
			Hw = 0.9999951f,
			Evidence = "20260719-Rex-Markus-stone Gas Fire"
		},
		new PropDefinition
		{
			Instance = 1469819060,
			TemplateId = 295883,
			Flags = -2146819551,
			X = 3636.222f,
			Y = 43.58711f,
			Z = 845.9906f,
			Hx = 0f,
			Hy = 0.003037463f,
			Hz = 0f,
			Hw = 0.9999954f,
			Evidence = "20260720-061810 Gas Fire Terminal:579BA8B4"
		},
		new PropDefinition
		{
			Instance = 1469456043,
			TemplateId = 295883,
			Flags = -2146819551,
			X = 3602.093f,
			Y = 42.86554f,
			Z = 842.4749f,
			Hx = 0f,
			Hy = 0.002859163f,
			Hz = 0f,
			Hw = 0.9999959f,
			Evidence = "20260719-Rex-Markus-stone Gas Fire"
		},
		new PropDefinition
		{
			Instance = 1469480401,
			TemplateId = 295883,
			Flags = -2146819551,
			X = 3607.675f,
			Y = 42.24637f,
			Z = 840.8735f,
			Hx = 0f,
			Hy = 0.003388073f,
			Hz = 0f,
			Hw = -0.9999943f,
			Evidence = "20260719-Rex-Markus-stone Gas Fire"
		},
		new PropDefinition
		{
			Instance = 1469766465,
			TemplateId = 295883,
			Flags = -2146819551,
			X = 3629.709f,
			Y = 42.90778f,
			Z = 832.0861f,
			Hx = 0f,
			Hy = 0.003335144f,
			Hz = 0f,
			Hw = -0.9999945f,
			Evidence = "20260719-Rex-Markus-stone Gas Fire"
		},
		new PropDefinition
		{
			Instance = 1469766456,
			TemplateId = 295883,
			Flags = -2146819551,
			X = 3629.344f,
			Y = 41.5f,
			Z = 829.9046f,
			Hx = 0f,
			Hy = 0.003335144f,
			Hz = 0f,
			Hw = -0.9999945f,
			Evidence = "20260719-Rex-Markus-stone extinguish Terminal:579ADB38"
		},
		new PropDefinition
		{
			Instance = 1469766462,
			TemplateId = 297284,
			Flags = -2146967039,
			X = 3620.871f,
			Y = 51.61641f,
			Z = 784.1057f,
			Hx = 0f,
			Hy = 0.0008167704f,
			Hz = 0f,
			Hw = 0.9999996f,
			Evidence = "20260719-Rex-Markus-stone Junk 297284"
		},
		new PropDefinition
		{
			Instance = 1469766463,
			TemplateId = 297284,
			Flags = -2146967039,
			X = 3611.681f,
			Y = 52.11217f,
			Z = 781.9929f,
			Hx = 0f,
			Hy = 0.00161186f,
			Hz = 0f,
			Hw = -0.9999987f,
			Evidence = "20260719-Rex-Markus-stone Junk 297284"
		},
		new PropDefinition
		{
			Instance = 1469766464,
			TemplateId = 297284,
			Flags = -2146967039,
			X = 3602.17f,
			Y = 51.74875f,
			Z = 775.8182f,
			Hx = 0f,
			Hy = 0.0005814155f,
			Hz = 0f,
			Hw = -0.9999998f,
			Evidence = "20260719-Rex-Markus-stone Junk 297284"
		},
		new PropDefinition
		{
			Instance = 1463912423,
			TemplateId = 295738,
			Flags = -2147470847,
			X = 3611.686f,
			Y = 8.15207f,
			Z = 814.9171f,
			Hx = 0f,
			Hy = 0.7282565f,
			Hz = 0f,
			Hw = -0.6853046f,
			Evidence = "20260720-105157 Prized Houseplant Terminal:574187E7"
		}
	};

	internal static IEnumerable<PlayfieldStaticDynelDefinition> ResolveMissingProps(Identity playfieldIdentity, IEnumerable<PlayfieldStaticDynelDefinition> existing)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		if (((Identity)(ref playfieldIdentity)).Instance != 6553)
		{
			yield break;
		}
		HashSet<ulong> existingKeys = new HashSet<ulong>();
		Identity val;
		if (existing != null)
		{
			foreach (PlayfieldStaticDynelDefinition dynel in existing)
			{
				int num;
				if (dynel != null)
				{
					val = dynel.Identity;
					num = (((int)((Identity)(ref val)).Type == 51005) ? 1 : 0);
				}
				else
				{
					num = 0;
				}
				if (num != 0)
				{
					val = dynel.Identity;
					existingKeys.Add(((Identity)(ref val)).Long());
				}
			}
		}
		int spawned = 0;
		PropDefinition[] props = Props;
		foreach (PropDefinition prop in props)
		{
			val = default(Identity);
			((Identity)(ref val)).Type = (IdentityType)51005;
			((Identity)(ref val)).Instance = prop.Instance;
			Identity identity = val;
			if (!existingKeys.Contains(((Identity)(ref identity)).Long()))
			{
				if (!ItemLoader.ItemList.TryGetValue(prop.TemplateId, out var template) || template == null)
				{
					LogUtil.Debug((DebugInfoDetail)512, "AreteLandingQuestProp missing item template=" + prop.TemplateId + " evidence=" + prop.Evidence);
					continue;
				}
				spawned++;
				yield return new PlayfieldStaticDynelDefinition(identity, template, BuildStats(prop), new Coordinate(prop.X, prop.Y, prop.Z), new Quaternion
				{
					X = prop.Hx,
					Y = prop.Hy,
					Z = prop.Hz,
					W = prop.Hw
				});
				identity = default(Identity);
				template = null;
			}
		}
		if (spawned > 0)
		{
			LogUtil.Debug((DebugInfoDetail)128, "AreteLandingQuestProp injected count=" + spawned + " pf=" + 6553);
		}
	}

	private static List<GameTuple<CharacterStat, uint>> BuildStats(PropDefinition prop)
	{
		return new List<GameTuple<CharacterStat, uint>>
		{
			Stat((CharacterStat)0, (uint)prop.Flags),
			Stat((CharacterStat)23, (uint)prop.TemplateId),
			Stat((CharacterStat)701, 1u),
			Stat((CharacterStat)702, (uint)prop.TemplateId),
			Stat((CharacterStat)703, (uint)prop.TemplateId),
			Stat((CharacterStat)412, 1u),
			Stat((CharacterStat)501, 0u),
			Stat((CharacterStat)500, 0u)
		};
	}

	private static GameTuple<CharacterStat, uint> Stat(CharacterStat id, uint value)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return new GameTuple<CharacterStat, uint>
		{
			Value1 = id,
			Value2 = value
		};
	}
}
