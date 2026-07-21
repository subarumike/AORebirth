#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Textures;
    using AORebirth.Core.Vector;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.KnuBot;
    using ZoneEngine.Script;
    using ZoneEngine.Scripts;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture-backed Perk-Reset Service Provider on Jobe Platform (PF 4530 / 0x11B2).
    /// Capture 20260716-Reset-perks: pos (281.1949, 194.145, 564.8134), monsterData 26092, level 220.
    /// </summary>
    internal static class PerkResetServiceProviderSpawn
    {
        private const int JobePlatformPlayfieldId = 4530;

        // Same monsterData as capture; body textures come from this template.
        private const string TemplateHash = "BART";

        private const string NpcName = "Perk-Reset Service Provider";

        private const int CapturedLevel = 220;

        private const int CapturedMonsterData = 26092;

        private const int CapturedHealth = 203721;

        private const int CapturedVisualFlags = 31;

        private const float CapturedX = 281.1949f;

        private const float CapturedY = 194.145f;

        private const float CapturedZ = 564.8134f;

        public static void SpawnForPlayfield(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null || activateNpc == null)
            {
                return;
            }

            if (playfieldIdentity.Instance != JobePlatformPlayfieldId)
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "Perk-Reset Service Provider spawn attempt pf=" + playfieldIdentity.Instance
                + " pos=(" + CapturedX + "," + CapturedY + "," + CapturedZ + ")");

            var npcController = new NPCController();
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                TemplateHash,
                playfieldIdentity,
                new Coordinate { x = CapturedX, y = CapturedY, z = CapturedZ },
                new Quaternion(0, 0, 0, 1),
                npcController,
                CapturedLevel);

            if (mob == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Perk-Reset Service Provider spawn FAILED template=" + TemplateHash
                    + " pf=" + playfieldIdentity.Instance);
                return;
            }

            mob.Name = NpcName;
            mob.Playfield = playfield;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, (uint)CapturedMonsterData);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)CapturedHealth);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)CapturedHealth);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, (uint)CapturedLevel);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, (uint)CapturedVisualFlags);
            mob.Coordinates(new Coordinate { x = CapturedX, y = CapturedY, z = CapturedZ });
            ApplyTemplateTextures(mob, TemplateHash);

            BaseKnuBot knu = ScriptCompiler.Instance.CreateKnuBot("PerkResetServiceKnu", mob.Identity);
            if (knu == null)
            {
                knu = new PerkResetServiceKnu(mob.Identity);
            }

            npcController.SetKnuBot(knu);
            mob.DoNotDoTimers = false;
            activateNpc(mob);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "Spawned Perk-Reset Service Provider on PF " + JobePlatformPlayfieldId
                + " id=" + mob.Identity.ToString(true)
                + " knu=" + (knu != null) + " textures=" + mob.Textures.Count);
        }

        private static void ApplyTemplateTextures(Character mob, string hash)
        {
            if (mob == null)
            {
                return;
            }

            DBMobTemplate template = MobTemplateDao.Instance.GetMobTemplateByHash(hash);
            if (template == null)
            {
                return;
            }

            mob.Textures.Clear();
            AddTexture(mob, 0, template.TextureHands);
            AddTexture(mob, 1, template.TextureBody);
            AddTexture(mob, 2, template.TextureFeet);
            AddTexture(mob, 3, template.TextureArms);
            AddTexture(mob, 4, template.TextureLegs);

            if (template.HeadMesh > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.headmesh, (uint)template.HeadMesh);
                mob.MeshLayer.Clear();
                mob.SocialMeshLayer.Clear();
                mob.MeshLayer.AddMesh(0, template.HeadMesh, 0, 4);
                mob.SocialMeshLayer.AddMesh(0, template.HeadMesh, 0, 4);
            }

            if (template.Flags != 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.flags, (uint)template.Flags);
            }

            if (template.MonsterScale > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterscale, (uint)template.MonsterScale);
            }
        }

        private static void AddTexture(Character mob, int place, int textureId)
        {
            if (textureId <= 0)
            {
                return;
            }

            mob.Textures.Add(new AOTextures(place, textureId));
        }
    }
}
