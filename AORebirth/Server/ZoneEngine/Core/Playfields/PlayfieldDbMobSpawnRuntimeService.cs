namespace ZoneEngine.Core.Playfields
{
    using System.Globalization;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Playfields;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Functions;
    using ZoneEngine.Script;

    internal sealed class PlayfieldDbMobSpawnRuntimeService
    {
        internal IEnumerable<DBMobSpawn> LoadMobSpawnDefinitions(Identity playfieldIdentity)
        {
            return MobSpawnDao.Instance.GetWhere(new { Playfield = playfieldIdentity.Instance });
        }

        internal IEnumerable<DBMobSpawnStat> LoadMobSpawnStats(DBMobSpawn mob)
        {
            return MobSpawnStatDao.Instance.GetWhere(new { mob.Id, mob.Playfield });
        }

        internal ICharacter InstantiateDbMobSpawn(DBMobSpawn mob, DBMobSpawnStat[] stats, Playfield playfield)
        {
            return NonPlayerCharacterHandler.InstantiateMobSpawn(
                mob,
                stats,
                new NPCController(),
                playfield);
        }

        internal WorldSpawnDefinition AdaptDefinition(DBMobSpawn mob)
        {
            return new WorldSpawnDefinition
            {
                SpawnKey = "legacy-db." + mob.Playfield.ToString(CultureInfo.InvariantCulture) + "." + mob.Id.ToString(CultureInfo.InvariantCulture),
                EnemyProfileKey = "legacy-db.unresolved." + mob.Id.ToString(CultureInfo.InvariantCulture),
                ConfiguredIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = mob.Id },
                PlayfieldId = mob.Playfield,
                X = mob.X,
                Y = mob.Y,
                Z = mob.Z,
                OrientationX = mob.HeadingX,
                OrientationY = mob.HeadingY,
                OrientationZ = mob.HeadingZ,
                OrientationW = mob.HeadingW,
                SpawnGroupKey = "legacy-db.playfield." + mob.Playfield.ToString(CultureInfo.InvariantCulture),
                RespawnPolicyKey = "legacy-db.unresolved",
                ActivationPolicy = WorldSpawnActivationPolicy.Disabled,
                Classification = WorldPopulationClassification.Unsupported,
                Enabled = false,
                Evidence = "legacy-database",
                Confidence = "LEGACY_ACTIVE_WITH_TRACKED_OWNER",
                Source = "mobspawns"
            };
        }

        internal void AttachMobSpawnKnuBot(DBMobSpawn mob, ICharacter cmob)
        {
            if (mob.KnuBotScriptName != "")
            {
                ((NPCController)cmob.Controller).SetKnuBot(
                    ScriptCompiler.Instance.CreateKnuBot(mob.KnuBotScriptName, cmob.Identity));
            }
        }
    }
}
