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

        private static int NormalizeItemVisualValue(int value)
        {
            if (value <= 0 || value == 1234567890)
            {
                return 0;
            }

            return value;
        }
    }
}
