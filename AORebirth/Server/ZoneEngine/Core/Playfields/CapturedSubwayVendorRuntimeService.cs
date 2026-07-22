namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Textures;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;

    internal sealed class CapturedSubwayVendorRuntimeService
    {
        private readonly List<IEntity> capturedEntities = new List<IEntity>();

        internal void Spawn(
            Playfield playfield,
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry,
            Action<ICharacter> registerNpc)
        {
            if (playfieldIdentity.Instance != CapturedSubwayVendorContentProvider.SubwayPlayfieldResource
                || CapturedSubwayVendorRuntimeRegistry.ContainsPlayfield(playfieldIdentity))
            {
                return;
            }

            foreach (CapturedSubwayVendorDefinition definition in CapturedSubwayVendorContentProvider.Definitions)
            {
                Character character = this.CreateCharacter(playfield, playfieldIdentity, definition);
                if (character == null)
                {
                    continue;
                }

                registerNpc(character);
                this.capturedEntities.Add(character);

                Vendor vendor = definition.HasCapturedStock
                    ? this.TryCreateVendor(playfield, playfieldIdentity, character, definition)
                    : null;
                Identity vendorIdentity = vendor == null ? Identity.None : vendor.Identity;
                if (vendor != null)
                {
                    dynelRegistry.Register(vendor);
                    this.capturedEntities.Add(vendor);
                }

                CapturedSubwayVendorRuntimeRegistry.Register(
                    new CapturedSubwayVendorRuntimeDefinition(
                        playfieldIdentity,
                        character.Identity,
                        vendorIdentity,
                        definition));
                playfield.AnnounceSpawnedCharacterVisibility(character, Identity.None);

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Captured Subway merchant spawned sourceNpc=SimpleChar:"
                    + definition.SourceNpcInstance.ToString("X8")
                    + " runtimeNpc=" + character.Identity
                    + " runtimeVendor=" + vendorIdentity
                    + " name=" + definition.DisplayName
                    + " stockRows=" + definition.Stock.Count
                    + " evidence=" + definition.Evidence);
            }
        }

        internal void Clear(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
        {
            CapturedSubwayVendorRuntimeRegistry.RemoveForPlayfield(playfieldIdentity);
            foreach (IEntity entity in this.capturedEntities)
            {
                dynelRegistry.Unregister(entity.Identity);
                Character character = entity as Character;
                if (character != null)
                {
                    CapturedEnemyCombatRuntimeRegistry.Remove(character.Identity.Instance);
                    Pool.Instance.RemoveObject(character);
                    continue;
                }

                Vendor vendor = entity as Vendor;
                if (vendor != null)
                {
                    Pool.Instance.RemoveObject(vendor);
                }
            }

            this.capturedEntities.Clear();
        }

        private Character CreateCharacter(
            Playfield playfield,
            Identity playfieldIdentity,
            CapturedSubwayVendorDefinition definition)
        {
            Character character = null;
            try
            {
                int instance = Pool.Instance.GetFreeInstance<Character>(1000000, IdentityType.CanbeAffected);
                var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
                var controller = new NPCController();
                character = new Character(playfieldIdentity, identity, controller);
                character.Read();
                controller.Character = character;
                character.Playfield = playfield;
                character.Name = definition.DisplayName;
                character.FirstName = string.Empty;
                character.LastName = string.Empty;
                character.Coordinates(
                    new Coordinate { x = definition.X, y = definition.Y, z = definition.Z });
                character.RawHeading =
                    new AORebirth.Core.Vector.Quaternion(
                        definition.HeadingX,
                        definition.HeadingY,
                        definition.HeadingZ,
                        definition.HeadingW);

                SetStat(character, StatIds.side, definition.Side);
                SetStat(character, StatIds.fatness, definition.Fatness);
                SetStat(character, StatIds.breed, definition.Breed);
                SetStat(character, StatIds.sex, definition.Sex);
                SetStat(character, StatIds.race, definition.Race);
                SetStat(character, StatIds.flags, definition.CharacterFlags);
                SetStat(character, StatIds.accountflags, 0);
                SetStat(character, StatIds.expansion, 0);
                SetStat(character, StatIds.npcfamily, 0);
                SetStat(character, StatIds.losheight, 0);
                SetStat(character, StatIds.monsterdata, definition.MonsterData);
                SetStat(character, StatIds.monsterscale, definition.MonsterScale);
                SetStat(character, StatIds.headmesh, definition.HeadMesh);
                SetStat(character, StatIds.visualflags, definition.VisualFlags);
                SetStat(character, StatIds.currentmovementmode, 3);
                SetStat(character, StatIds.prevmovementmode, 3);
                SetStat(character, StatIds.runspeed, definition.RunSpeed);
                SetStat(character, StatIds.level, definition.Level);
                SetStat(character, StatIds.life, definition.Health);
                SetStat(character, StatIds.health, definition.Health);

                character.Textures.Clear();
                foreach (CapturedSubwayVendorTextureDefinition texture in definition.Textures)
                {
                    character.Textures.Add(new AOTextures(texture.Place, texture.Id));
                }

                foreach (CapturedSubwayVendorMeshDefinition mesh in definition.Meshes)
                {
                    character.MeshLayer.AddMesh(
                        mesh.Position,
                        (int)mesh.Id,
                        mesh.OverrideTextureId,
                        mesh.Layer);
                    character.SocialMeshLayer.AddMesh(
                        mesh.Position,
                        (int)mesh.Id,
                        mesh.OverrideTextureId,
                        mesh.Layer);
                }

                character.Waypoints.Clear();
                foreach (CapturedSubwayVendorWaypointDefinition waypoint in definition.Waypoints)
                {
                    character.AddWaypoint(
                        new AORebirth.Core.Vector.Vector3(waypoint.X, waypoint.Y, waypoint.Z),
                        false);
                }

                string combatFailure;
                CapturedEnemyCombatRuntime.Prepare(
                    character,
                    controller,
                    CapturedEnemyCombatContract.Unresolved(
                        "Captured Subway merchant 0x"
                        + definition.SourceNpcInstance.ToString("X8")
                        + " has no source-local WIFU/attack-start/AttackInfo contract mapped; evidence="
                        + definition.Evidence,
                        true),
                    out combatFailure);

                character.DoNotDoTimers = true;
                return character;
            }
            catch (Exception exception)
            {
                if (character != null)
                {
                    Pool.Instance.RemoveObject(character);
                }

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Captured Subway merchant NPC refused sourceNpc=SimpleChar:"
                    + definition.SourceNpcInstance.ToString("X8")
                    + " reason=" + exception.Message);
                return null;
            }
        }

        private Vendor TryCreateVendor(
            Playfield playfield,
            Identity playfieldIdentity,
            Character character,
            CapturedSubwayVendorDefinition definition)
        {
            Vendor vendor = null;
            try
            {
                if (!ItemLoader.ItemList.ContainsKey(definition.VendorTemplateId))
                {
                    throw new InvalidOperationException(
                        "missing vendor template item " + definition.VendorTemplateId);
                }

                var items = new List<KeyValuePair<int, Item>>();
                foreach (CapturedSubwayVendorStockDefinition stock in definition.Stock)
                {
                    if (!ItemLoader.ItemList.ContainsKey(stock.LowId)
                        || !ItemLoader.ItemList.ContainsKey(stock.HighId))
                    {
                        throw new InvalidOperationException(
                            "missing stock item low=" + stock.LowId + " high=" + stock.HighId);
                    }

                    items.Add(
                        new KeyValuePair<int, Item>(
                            stock.Slot,
                            new Item(stock.Quality, stock.LowId, stock.HighId)));
                }

                var identity =
                    new Identity
                    {
                        Type = IdentityType.VendingMachine,
                        Instance = Pool.Instance.GetFreeInstance<Vendor>(0x70000000, IdentityType.VendingMachine)
                    };
                vendor = new Vendor(playfieldIdentity, identity, definition.VendorTemplateId);
                vendor.NpcIdentity = character.Identity;
                vendor.RawCoordinates = new AORebirth.Core.Vector.Vector3(definition.X, definition.Y, definition.Z);
                vendor.Heading =
                    new AORebirth.Core.Vector.Quaternion(
                        definition.HeadingX,
                        definition.HeadingY,
                        definition.HeadingZ,
                        definition.HeadingW);
                vendor.Playfield = playfield;

                int page = vendor.BaseInventory.StandardPage;
                vendor.BaseInventory[page].List().Clear();
                foreach (KeyValuePair<int, Item> item in items)
                {
                    vendor.BaseInventory.AddToPage(page, item.Key, item.Value);
                }

                return vendor;
            }
            catch (Exception exception)
            {
                if (vendor != null)
                {
                    Pool.Instance.RemoveObject(vendor);
                }

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Captured Subway merchant endpoint refused atomically sourceVendor=VendingMachine:"
                    + definition.SourceVendorInstance.ToString("X8")
                    + " name=" + definition.DisplayName
                    + " reason=" + exception.Message);
                return null;
            }
        }

        private static void SetStat(ICharacter character, StatIds stat, int value)
        {
            character.Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
        }
    }
}
