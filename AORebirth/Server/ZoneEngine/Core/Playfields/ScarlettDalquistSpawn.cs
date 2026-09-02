namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Textures;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Doja;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture-backed Scarlett Dalquist spawn — DOJA Research / Lab R1 (PF 7010).
    /// Capture 20260821-222107 Nascense DOJA.
    /// </summary>
    internal static class ScarlettDalquistSpawn
    {
        internal const int DojaResearchPlayfieldId = 7010;

        // Human researcher meshes/textures (same family as ICC Shuttleport Adri Afeli / Vendor Antonio).
        private const string TemplateHash = "BART";

        public static void SpawnForPlayfield(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null || activateNpc == null)
            {
                return;
            }

            if (playfieldIdentity.Instance != DojaResearchPlayfieldId)
            {
                return;
            }

            if (SpawnScarlett(playfield, playfieldIdentity, activateNpc))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "ScarlettDalquistSpawn pf=" + playfieldIdentity.Instance + " spawned=1/1");
            }
        }

        private static bool SpawnScarlett(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            var npcController = new NPCController { AiProfile = NpcAiProfile.Social };
            Identity reservedIdentity = new Identity
                                        {
                                            Type = IdentityType.CanbeAffected,
                                            Instance = DojaChipInteractionRules.ScarlettInstance
                                        };

            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplateWithIdentity(
                TemplateHash,
                playfieldIdentity,
                new Coordinate { x = 104.180695f, y = 2.185f, z = 76.13117f },
                new Quaternion(0f, -0.342263281f, 0f, 0.9396041f),
                npcController,
                150,
                reservedIdentity);

            if (mob == null)
            {
                // Fallback if reserved identity collision — still try ordinary spawn.
                mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                    TemplateHash,
                    playfieldIdentity,
                    new Coordinate { x = 104.180695f, y = 2.185f, z = 76.13117f },
                    new Quaternion(0f, -0.342263281f, 0f, 0.9396041f),
                    npcController,
                    150);
            }

            if (mob == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ScarlettDalquistSpawn FAILED template=" + TemplateHash
                    + " npc=" + DojaChipInteractionRules.ScarlettName);
                return false;
            }

            mob.Name = DojaChipInteractionRules.ScarlettName;
            mob.FirstName = string.Empty;
            mob.LastName = string.Empty;
            mob.Playfield = playfield;

            // Capture SCFU: Level 150, Health 16042, MonsterData 26090, Scale 117,
            // VisualFlags 31, HeadMesh 223846, CharacterFlags 277352961, Side 0 Neutral,
            // NpcFamily 137, RunSpeed 432.
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, 26090u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, 16042u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, 16042u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, 150u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, 31u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.flags, 277352961u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, 0u);
            mob.Stats[StatIds.side].Value = 0;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, 137u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 432u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterscale, 117u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.headmesh, 223846u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.accountflags, 0u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.expansion, 0u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.profession, 0u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualprofession, 0u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.currentmovementmode, 3u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.prevmovementmode, 3u);

            mob.Textures.Clear();
            mob.Textures.Add(new AOTextures(0, 213851));
            mob.Textures.Add(new AOTextures(1, 213751));
            mob.Textures.Add(new AOTextures(2, 213807));
            mob.Textures.Add(new AOTextures(3, 213708));
            mob.Textures.Add(new AOTextures(4, 213925));

            mob.MeshLayer.Clear();
            mob.SocialMeshLayer.Clear();
            mob.MeshLayer.AddMesh(0, 223846, 0, 4);
            mob.SocialMeshLayer.AddMesh(0, 223846, 0, 4);
            mob.MeshLayer.AddMesh(1, 258990, 0, 2);
            mob.SocialMeshLayer.AddMesh(1, 258990, 0, 2);

            mob.Position = (new Coordinate { x = 104.180695f, y = 2.185f, z = 76.13117f }).coordinate;

            string combatFailure;
            CapturedEnemyCombatRuntime.Prepare(
                mob,
                npcController,
                CapturedEnemyCombatContract.Unresolved(
                    "20260821-222107 Scarlett Dalquist captured actor is a dialogue/trade NPC; npc="
                    + DojaChipInteractionRules.ScarlettName + " monsterData=26090 level=150",
                    true),
                out combatFailure);

            mob.DoNotDoTimers = false;
            activateNpc(mob);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return true;
        }
    }
}
