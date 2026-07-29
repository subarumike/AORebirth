using System;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Textures;
using AORebirth.Core.Vector;
using AORebirth.Database.Dao;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.KnuBot;
using ZoneEngine.Script;
using ZoneEngine.Scripts;

namespace AORebirth.Core.Playfields;

internal static class PerkResetServiceProviderSpawn
{
	private const int JobePlatformPlayfieldId = 4530;

	private const string TemplateHash = "BART";

	private const string NpcName = "Perk-Reset Service Provider";

	private const int CapturedLevel = 220;

	private const int CapturedMonsterData = 26092;

	private const int CapturedHealth = 203721;

	private const int CapturedVisualFlags = 31;

	private const float CapturedX = 281.1949f;

	private const float CapturedY = 194.145f;

	private const float CapturedZ = 564.8134f;

	public static void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Expected O, but got Unknown
		//IL_0120: Expected O, but got Unknown
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Expected O, but got Unknown
		//IL_0212: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0289: Unknown result type (might be due to invalid IL or missing references)
		//IL_028e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 4530)
		{
			return;
		}
		LogUtil.Debug((DebugInfoDetail)128, "Perk-Reset Service Provider spawn attempt pf=" + ((Identity)(ref playfieldIdentity)).Instance + " pos=(" + 281.1949f + "," + 194.145f + "," + 564.8134f + ")");
		NPCController nPCController = new NPCController();
		Character val = NonPlayerCharacterHandler.SpawnMobFromTemplate("BART", playfieldIdentity, new Coordinate
		{
			x = 281.1949f,
			y = 194.145f,
			z = 564.8134f
		}, new Quaternion(0.0, 0.0, 0.0, 1.0), (IController)(object)nPCController, 220);
		if (val == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "Perk-Reset Service Provider spawn FAILED template=BART pf=" + ((Identity)(ref playfieldIdentity)).Instance);
			return;
		}
		((Dynel)val).Name = "Perk-Reset Service Provider";
		((Dynel)val).Playfield = (IPlayfield)(object)playfield;
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(359, 26092u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(1, 203721u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(27, 203721u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(54, 220u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(673, 31u);
		((Dynel)val).Coordinates(new Coordinate
		{
			x = 281.1949f,
			y = 194.145f,
			z = 564.8134f
		});
		ApplyTemplateTextures(val, "BART");
		BaseKnuBot baseKnuBot = ScriptCompiler.Instance.CreateKnuBot("PerkResetServiceKnu", ((PooledObject)val).Identity);
		if (baseKnuBot == null)
		{
			baseKnuBot = new PerkResetServiceKnu(((PooledObject)val).Identity);
		}
		nPCController.SetKnuBot(baseKnuBot);
		((Dynel)val).DoNotDoTimers = false;
		activateNpc((ICharacter)(object)val);
		playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val, Identity.None);
		string[] obj = new string[8]
		{
			"Spawned Perk-Reset Service Provider on PF ",
			4530.ToString(),
			" id=",
			null,
			null,
			null,
			null,
			null
		};
		Identity identity = ((PooledObject)val).Identity;
		obj[3] = ((Identity)(ref identity)).ToString(true);
		obj[4] = " knu=";
		obj[5] = (baseKnuBot != null).ToString();
		obj[6] = " textures=";
		obj[7] = ((Dynel)val).Textures.Count.ToString();
		LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj));
	}

	private static void ApplyTemplateTextures(Character mob, string hash)
	{
		if (mob == null)
		{
			return;
		}
		DBMobTemplate mobTemplateByHash = Dao<DBMobTemplate, MobTemplateDao>.Instance.GetMobTemplateByHash(hash);
		if (mobTemplateByHash != null)
		{
			((Dynel)mob).Textures.Clear();
			AddTexture(mob, 0, mobTemplateByHash.TextureHands);
			AddTexture(mob, 1, mobTemplateByHash.TextureBody);
			AddTexture(mob, 2, mobTemplateByHash.TextureFeet);
			AddTexture(mob, 3, mobTemplateByHash.TextureArms);
			AddTexture(mob, 4, mobTemplateByHash.TextureLegs);
			if (mobTemplateByHash.HeadMesh > 0)
			{
				((Dynel)mob).Stats.SetBaseValueWithoutTriggering(64, (uint)mobTemplateByHash.HeadMesh);
				((Dynel)mob).MeshLayer.Clear();
				mob.SocialMeshLayer.Clear();
				((Dynel)mob).MeshLayer.AddMesh(0, mobTemplateByHash.HeadMesh, 0, 4);
				mob.SocialMeshLayer.AddMesh(0, mobTemplateByHash.HeadMesh, 0, 4);
			}
			if (mobTemplateByHash.Flags != 0)
			{
				((Dynel)mob).Stats.SetBaseValueWithoutTriggering(0, (uint)mobTemplateByHash.Flags);
			}
			if (mobTemplateByHash.MonsterScale > 0)
			{
				((Dynel)mob).Stats.SetBaseValueWithoutTriggering(360, (uint)mobTemplateByHash.MonsterScale);
			}
		}
	}

	private static void AddTexture(Character mob, int place, int textureId)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		if (textureId > 0)
		{
			((Dynel)mob).Textures.Add(new AOTextures(place, textureId));
		}
	}
}
