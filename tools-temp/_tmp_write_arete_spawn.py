from pathlib import Path
import re

frag = Path(r"tools-temp/_tmp_arete_spawn_npcs.csfrag").read_text(encoding="utf-8")
frag = re.sub(r"\s*FixedIdentityInstance = .*\n", "", frag)

header = r'''namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Textures;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture-backed Arete Landing (PF 6553) humanoid NPCs.
    /// Capture 20260719-Rex-Markus-stone + arete-analysis Rex/Marcus positions.
    /// Cleaning robots remain owned by CapturedAreteRobotSpawnOrchestrator.
    /// </summary>
    internal static class AreteLandingSpawn
    {
        private const int AreteLandingPlayfieldId = 6553;

        private static readonly HashSet<int> SpawnedPlayfields = new HashSet<int>();

        private const string TemplateHash = "BART";

        private sealed class AreteNpc
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
            public int LosHeight;
            public int CharacterFlags;
            public int AppearanceValue;
            public int Side;
            public int Breed;
            public int Gender;
            public int Race;
            public int Fatness;
            public int MovementMode;
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

        private static readonly AreteNpc[] Npcs =
        {
'''

footer = r'''
        };

        internal static void ClearPlayfield(int playfieldInstance)
        {
            SpawnedPlayfields.Remove(playfieldInstance);
        }

        public static void SpawnForPlayfield(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null || activateNpc == null)
            {
                return;
            }

            if (playfieldIdentity.Instance != AreteLandingPlayfieldId)
            {
                return;
            }

            if (!SpawnedPlayfields.Add(playfieldIdentity.Instance))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "AreteLandingSpawn skip duplicate pf=" + playfieldIdentity.Instance);
                return;
            }

            int spawned = 0;
            foreach (AreteNpc def in Npcs)
            {
                if (SpawnOne(playfield, playfieldIdentity, activateNpc, def))
                {
                    spawned++;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "AreteLandingSpawn pf=" + playfieldIdentity.Instance + " spawned=" + spawned
                + "/" + Npcs.Length);
        }

        private static bool SpawnOne(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            AreteNpc def)
        {
            var npcController = new NPCController { AiProfile = NpcAiProfile.Social };
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                TemplateHash,
                playfieldIdentity,
                new Coordinate { x = def.X, y = def.Y, z = def.Z },
                new Quaternion(def.Hx, def.Hy, def.Hz, def.Hw),
                npcController,
                def.Level);

            if (mob == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AreteLandingSpawn FAILED template=" + TemplateHash + " npc=" + def.Name);
                return false;
            }

            mob.Name = def.Name;
            mob.FirstName = string.Empty;
            mob.LastName = string.Empty;
            mob.Playfield = playfield;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, (uint)def.MonsterData);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, (uint)def.Level);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, (uint)def.VisualFlags);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)def.NpcFamily);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.losheight, (uint)def.LosHeight);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.flags, (uint)def.CharacterFlags);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)def.Side);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.breed, (uint)def.Breed);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.sex, (uint)def.Gender);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.race, (uint)def.Race);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.fatness, (uint)def.Fatness);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.accountflags, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.expansion, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.profession, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualprofession, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.currentmovementmode, (uint)def.MovementMode);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.prevmovementmode, (uint)def.MovementMode);
            if (def.Scale > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterscale, (uint)def.Scale);
            }

            if (def.HeadMesh > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.headmesh, (uint)def.HeadMesh);
            }

            if (def.RunSpeed > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, (uint)def.RunSpeed);
            }

            ApplyAppearance(mob, def);
            mob.Coordinates(new Coordinate { x = def.X, y = def.Y, z = def.Z });
            mob.DoNotDoTimers = false;
            activateNpc(mob);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return true;
        }

        private static void ApplyAppearance(Character mob, AreteNpc def)
        {
            if (def.Textures != null && def.Textures.Length > 0)
            {
                mob.Textures.Clear();
                foreach (int[] t in def.Textures)
                {
                    mob.Textures.Add(new AOTextures(t[0], t[1]));
                }
            }

            if (def.Meshes != null && def.Meshes.Length > 0)
            {
                mob.MeshLayer.Clear();
                mob.SocialMeshLayer.Clear();
                foreach (int[] m in def.Meshes)
                {
                    mob.MeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                    mob.SocialMeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                }
            }
            else if (def.HeadMesh > 0)
            {
                mob.MeshLayer.Clear();
                mob.SocialMeshLayer.Clear();
                mob.MeshLayer.AddMesh(0, def.HeadMesh, 0, 4);
                mob.SocialMeshLayer.AddMesh(0, def.HeadMesh, 0, 4);
            }
        }
    }
}
'''

out = Path(r"AORebirth/Server/ZoneEngine/Core/Playfields/AreteLandingSpawn.cs")
out.write_text(header + frag + footer, encoding="utf-8")
print("wrote", out, "bytes", out.stat().st_size)
