namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Controllers;

    #endregion

    internal sealed class PlayfieldTimedLifecycleRuntimeService
    {
        internal void ProcessHeartbeatLifecycle(
            Identity playfieldIdentity,
            Func<IEnumerable<ICharacter>> characters,
            Func<Identity, bool> hasPendingDeadNpcDespawn,
            Action processPendingCorpseSpawns,
            Action processCorpseDespawns,
            Action processPendingCorpseCreditAwards,
            Func<ICharacter, bool> processDeadNpcDespawn,
            Action<ICharacter> processRegeneration,
            Action<ICharacter> processCombatTick,
            Action<ICharacter> processNpcPatrolTick,
            Action<ICharacter> processFollow,
            Action<ICharacter> processPlayerCollision)
        {
            Require(characters, "characters");
            Require(hasPendingDeadNpcDespawn, "hasPendingDeadNpcDespawn");
            Require(processPendingCorpseSpawns, "processPendingCorpseSpawns");
            Require(processCorpseDespawns, "processCorpseDespawns");
            Require(processPendingCorpseCreditAwards, "processPendingCorpseCreditAwards");
            Require(processDeadNpcDespawn, "processDeadNpcDespawn");
            Require(processRegeneration, "processRegeneration");
            Require(processCombatTick, "processCombatTick");
            Require(processNpcPatrolTick, "processNpcPatrolTick");
            Require(processFollow, "processFollow");
            Require(processPlayerCollision, "processPlayerCollision");

            processPendingCorpseSpawns();
            processCorpseDespawns();
            processPendingCorpseCreditAwards();

            IEnumerable<ICharacter> dynels =
                characters()
                    .Where(
                        xx =>
                            xx.InPlayfield(playfieldIdentity)
                            && (!xx.DoNotDoTimers
                                || hasPendingDeadNpcDespawn(xx.Identity)))
                    .ToList();

            foreach (ICharacter dynel in dynels)
            {
                if (dynel != null)
                {
                    if (dynel.Starting)
                    {
                        continue;
                    }

                    if (processDeadNpcDespawn(dynel))
                    {
                        continue;
                    }

                    if (dynel.DoNotDoTimers)
                    {
                        continue;
                    }

                    processRegeneration(dynel);
                    processCombatTick(dynel);

                    if (dynel.Controller is NPCController)
                    {
                        processNpcPatrolTick(dynel);
                    }
                    else
                    {
                        processFollow(dynel);
                    }

                    if (dynel.Controller is PlayerController)
                    {
                        processPlayerCollision(dynel);
                    }
                }
            }
        }

        private static void Require(Delegate callback, string name)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
