namespace ZoneEngine.Core.MessageHandlers
{
    using System;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Actions;
    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Textures;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Packets;

    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class ClientMoveItemToInventoryMessageHandler :
        BaseMessageHandler<ClientMoveItemToInventoryMessage, ClientMoveItemToInventoryMessageHandler>
    {
        protected override void Read(ClientMoveItemToInventoryMessage message, IZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "ClientMoveItemToInventory received char={0} source={1} targetPlacement={2}",
                    character.Identity,
                    message.SourceContainer,
                    message.TargetPlacement));

            if (character.Playfield.TryLootCorpseItem(
                character,
                message.SourceContainer,
                character.Identity,
                message.TargetPlacement))
            {
                return;
            }

            if (InventoryContainerRuntimeService.Default.TryMoveBackpackItemToInventory(character, message))
            {
                return;
            }

            if (InventoryContainerRuntimeService.Default.TryMoveOwnedInventoryItem(character, message, client))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "Unhandled ClientMoveItemToInventory source={0} targetPlacement={1} character={2}",
                    message.SourceContainer,
                    message.TargetPlacement,
                    character.Identity));
        }

        private bool IsWeaponPage(IInventoryPage page)
        {
            return page is WeaponInventoryPage;
        }

        public static void EnsureWeaponVisualMeshes(ICharacter character, bool announceAppearanceUpdate)
        {
            IInventoryPage weaponPage;
            if (!character.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage))
            {
                return;
            }

            bool changed = false;
            changed |= EnsureWeaponMesh(
                character,
                weaponPage,
                (int)WeaponSlots.Righthand,
                1,
                StatIds.weaponmeshright,
                StatIds.overridetextureweaponright);
            changed |= EnsureWeaponMesh(
                character,
                weaponPage,
                (int)WeaponSlots.LeftHand,
                2,
                StatIds.weaponmeshleft,
                StatIds.overridetextureweaponleft);

            if (changed)
            {
                character.ChangedAppearance = true;
                if (announceAppearanceUpdate)
                {
                    character.Playfield.AnnounceAppearanceUpdate(character);
                }
            }
        }

        private static bool EnsureWeaponMesh(
            ICharacter character,
            IInventoryPage weaponPage,
            int slot,
            int meshPosition,
            StatIds meshStat,
            StatIds overrideTextureStat)
        {
            IItem equippedItem = weaponPage[slot];
            if (equippedItem == null)
            {
                return false;
            }

            AOMeshs existing = character.MeshLayer.GetMeshAtPosition(meshPosition);

            int meshId = NormalizeItemVisualValue(equippedItem.GetAttribute((int)meshStat));
            if (meshId <= 0)
            {
                meshId = NormalizeItemVisualValue(equippedItem.GetAttribute(209));
            }

            if (meshId <= 0)
            {
                bool hasToWieldAction = equippedItem.ItemActions.Any(x => x.ActionType == ActionType.ToWield);
                string wearFunctions = string.Join(
                    ",",
                    equippedItem.Events
                        .Where(x => x.EventType == EventType.OnWear || x.EventType == EventType.OnWield)
                        .SelectMany(x => x.Functions)
                        .Select(x => x.FunctionType.ToString())
                        .ToArray());

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "EnsureWeaponMesh skipped: item has no valid mesh stat char={0} slot={1} meshStat={2} raw={3} item={4}/{5} ql={6} hasToWield={7} wearFuncs=[{8}] meshR={9} meshL={10} ovR={11} ovL={12} weaponMeshHolder={13}",
                        character.Identity,
                        slot,
                        meshStat,
                        equippedItem.GetAttribute((int)meshStat),
                        equippedItem.LowID,
                        equippedItem.HighID,
                        equippedItem.Quality,
                        hasToWieldAction ? 1 : 0,
                        wearFunctions,
                        equippedItem.GetAttribute((int)StatIds.weaponmeshright),
                        equippedItem.GetAttribute((int)StatIds.weaponmeshleft),
                        equippedItem.GetAttribute((int)StatIds.overridetextureweaponright),
                        equippedItem.GetAttribute((int)StatIds.overridetextureweaponleft),
                        equippedItem.GetAttribute(209)));
                return false;
            }

            if (existing != null)
            {
                if (existing.Mesh > 0 && existing.Mesh != 1234567890)
                {
                    return false;
                }

                // Existing hand mesh entry is present but invalid (0/sentinel); overwrite it.
                character.MeshLayer.RemoveMesh(existing.Position, existing.Mesh, existing.OverrideTexture, existing.Layer);
            }

            int overrideTexture = NormalizeItemVisualValue(equippedItem.GetAttribute((int)overrideTextureStat));
            int layer = MeshLayers.GetLayer(slot);
            character.MeshLayer.AddMesh(meshPosition, meshId, overrideTexture, layer);
            character.Stats[meshStat].Value = meshId;

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "EnsureWeaponMesh applied char={0} slot={1} position={2} mesh={3} override={4} layer={5}",
                    character.Identity,
                    slot,
                    meshPosition,
                    meshId,
                    overrideTexture,
                    layer));
            return true;
        }

        private static int NormalizeItemVisualValue(int value)
        {
            if (value <= 0 || value == 1234567890)
            {
                return 0;
            }

            return value;
        }

        private void SyncEquippedWeaponCombatStats(ICharacter character)
        {
            IInventoryPage weaponPage;
            if (!character.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage))
            {
                return;
            }

            IItem equippedWeapon = weaponPage[(int)WeaponSlots.Righthand] ?? weaponPage[(int)WeaponSlots.LeftHand];
            bool changed = false;

            int currentWeaponType = character.Stats[StatIds.weapontype].Value;
            int currentDefaultAttackType = character.Stats[StatIds.defaultattacktype].Value;
            int currentEquippedWeapons = character.Stats[StatIds.equippedweapons].Value;

            int weaponType = currentWeaponType;
            int defaultAttackType = currentDefaultAttackType;
            int equippedWeapons = currentEquippedWeapons;
            if (equippedWeapon != null)
            {
                int itemWeaponType = NormalizeItemVisualValue(equippedWeapon.GetAttribute((int)StatIds.weapontype));
                int itemDefaultAttackType =
                    NormalizeItemVisualValue(equippedWeapon.GetAttribute((int)StatIds.defaultattacktype));

                if (itemWeaponType > 0)
                {
                    weaponType = itemWeaponType;
                }

                if (itemDefaultAttackType > 0)
                {
                    defaultAttackType = itemDefaultAttackType;
                }

                // Non-zero "weapon equipped" state gate for client-side combat profile selection.
                if (equippedWeapons <= 0)
                {
                    equippedWeapons = 1;
                }
            }
            else
            {
                weaponType = 0;
                defaultAttackType = 0;
                equippedWeapons = 0;
            }

            if (character.Stats[StatIds.weapontype].Value != weaponType)
            {
                character.Stats[StatIds.weapontype].Value = weaponType;
                changed = true;
            }

            if (character.Stats[StatIds.defaultattacktype].Value != defaultAttackType)
            {
                character.Stats[StatIds.defaultattacktype].Value = defaultAttackType;
                changed = true;
            }

            if (character.Stats[StatIds.equippedweapons].Value != equippedWeapons)
            {
                character.Stats[StatIds.equippedweapons].Value = equippedWeapons;
                changed = true;
            }

            if (changed)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "SyncEquippedWeaponCombatStats char={0} item={1} weapontype={2} defaultattack={3} equippedweapons={4}",
                        character.Identity,
                        equippedWeapon != null ? equippedWeapon.LowID.ToString() : "none",
                        weaponType,
                        defaultAttackType,
                        equippedWeapons));
                character.SendChangedStats();
            }
        }
    }
}
