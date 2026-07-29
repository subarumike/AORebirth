using System.Collections.Generic;
using System.Data;
using System.Globalization;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Playfields;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Script;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldDbMobSpawnRuntimeService
{
	internal IEnumerable<DBMobSpawn> LoadMobSpawnDefinitions(Identity playfieldIdentity)
	{
		return ((Dao<DBMobSpawn, MobSpawnDao>)(object)Dao<DBMobSpawn, MobSpawnDao>.Instance).GetWhere((object)new
		{
			Playfield = ((Identity)(ref playfieldIdentity)).Instance
		}, (IDbConnection)null, (IDbTransaction)null);
	}

	internal IEnumerable<DBMobSpawnStat> LoadMobSpawnStats(DBMobSpawn mob)
	{
		return ((Dao<DBMobSpawnStat, MobSpawnStatDao>)(object)Dao<DBMobSpawnStat, MobSpawnStatDao>.Instance).GetWhere((object)new { mob.Id, mob.Playfield }, (IDbConnection)null, (IDbTransaction)null);
	}

	internal ICharacter InstantiateDbMobSpawn(DBMobSpawn mob, DBMobSpawnStat[] stats, Playfield playfield)
	{
		return NonPlayerCharacterHandler.InstantiateMobSpawn(mob, stats, (IController)(object)new NPCController(), (IPlayfield)(object)playfield);
	}

	internal WorldSpawnDefinition AdaptDefinition(DBMobSpawn mob)
	{
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		WorldSpawnDefinition obj = new WorldSpawnDefinition
		{
			SpawnKey = "legacy-db." + mob.Playfield.ToString(CultureInfo.InvariantCulture) + "." + mob.Id.ToString(CultureInfo.InvariantCulture),
			EnemyProfileKey = "legacy-db.unresolved." + mob.Id.ToString(CultureInfo.InvariantCulture)
		};
		Identity configuredIdentity = default(Identity);
		((Identity)(ref configuredIdentity)).Type = (IdentityType)50000;
		((Identity)(ref configuredIdentity)).Instance = mob.Id;
		obj.ConfiguredIdentity = configuredIdentity;
		obj.PlayfieldId = mob.Playfield;
		obj.X = mob.X;
		obj.Y = mob.Y;
		obj.Z = mob.Z;
		obj.OrientationX = mob.HeadingX;
		obj.OrientationY = mob.HeadingY;
		obj.OrientationZ = mob.HeadingZ;
		obj.OrientationW = mob.HeadingW;
		obj.SpawnGroupKey = "legacy-db.playfield." + mob.Playfield.ToString(CultureInfo.InvariantCulture);
		obj.RespawnPolicyKey = "legacy-db.unresolved";
		obj.ActivationPolicy = WorldSpawnActivationPolicy.Disabled;
		obj.Classification = WorldPopulationClassification.Unsupported;
		obj.Enabled = false;
		obj.Evidence = "legacy-database";
		obj.Confidence = "LEGACY_ACTIVE_WITH_TRACKED_OWNER";
		obj.Source = "mobspawns";
		return obj;
	}

	internal void AttachMobSpawnKnuBot(DBMobSpawn mob, ICharacter cmob)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (mob.KnuBotScriptName != "")
		{
			((NPCController)(object)((IDynel)cmob).Controller).SetKnuBot(ScriptCompiler.Instance.CreateKnuBot(mob.KnuBotScriptName, ((IEntity)cmob).Identity));
		}
	}
}
