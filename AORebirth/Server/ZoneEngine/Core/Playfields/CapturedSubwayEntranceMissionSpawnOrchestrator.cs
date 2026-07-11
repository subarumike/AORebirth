namespace AORebirth.Core.Playfields
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Textures;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Packets;

    internal sealed class CapturedSubwayEntranceMissionSpawnOrchestrator
    {
        private readonly Action<ICharacter> activateNpc;

        internal CapturedSubwayEntranceMissionSpawnOrchestrator(Action<ICharacter> activateNpc)
        {
            this.activateNpc = activateNpc;
        }

        internal void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity)
        {
            if (playfieldIdentity.Instance != 655)
            {
                return;
            }

            int instance = Pool.Instance.GetFreeInstance<Character>(1000000, IdentityType.CanbeAffected);
            var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
            var controller = new NPCController();
            var character = new Character(playfieldIdentity, identity, controller);
            character.Read();
            character.Playfield = playfield;
            character.Name = "Natalia Akcora";
            character.FirstName = "Natalia";
            character.LastName = "Akcora";
            character.Coordinates(new Coordinate { x = 3287.04688f, y = 35.11f, z = 860.1282f });
            character.RawHeading = new AORebirth.Core.Vector.Quaternion(0, 0.4396549f, 0, 0.8981668f);
            controller.Character = character;

            int templateHeadMesh = character.Stats[StatIds.headmesh].Value;

            SetStat(character, StatIds.side, 0);
            SetStat(character, StatIds.fatness, 0);
            SetStat(character, StatIds.breed, 1);
            SetStat(character, StatIds.sex, 2);
            SetStat(character, StatIds.race, 1);
            SetStat(character, StatIds.flags, 277352961);
            SetStat(character, StatIds.npcfamily, 103);
            SetStat(character, StatIds.losheight, 0);
            SetStat(character, StatIds.monsterdata, 26076);
            SetStat(character, StatIds.monsterscale, 97);
            SetStat(character, StatIds.runspeed, 52);
            SetStat(character, StatIds.level, 15);
            SetStat(character, StatIds.life, 393);
            SetStat(character, StatIds.health, 393);
            SetStat(character, StatIds.headmesh, 40635);

            character.Textures.Clear();
            character.Textures.Add(new AOTextures(0, 284555));
            character.Textures.Add(new AOTextures(1, 247933));
            character.Textures.Add(new AOTextures(2, 284553));
            character.Textures.Add(new AOTextures(3, 247887));
            character.Textures.Add(new AOTextures(4, 284556));
            character.MeshLayer.RemoveMesh(0, templateHeadMesh, 0, 4);
            character.SocialMeshLayer.RemoveMesh(0, templateHeadMesh, 0, 4);
            character.MeshLayer.AddMesh(0, 40635, 0, 4);
            character.SocialMeshLayer.AddMesh(0, 40635, 0, 4);
            character.DoNotDoTimers = false;

            SimpleCharFullUpdateMessage fullUpdate = SimpleCharFullUpdate.ConstructMessage(character);
            this.activateNpc(character);
            playfield.Announce(fullUpdate);
        }

        private static void SetStat(ICharacter character, StatIds stat, int value)
        {
            character.Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
        }
    }
}
