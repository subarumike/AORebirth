namespace ZoneEngine.Core.Playfields
{
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
