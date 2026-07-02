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

            if (this.TryMoveOwnedInventoryItem(character, message, client))
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

        private bool TryMoveOwnedInventoryItem(
            ICharacter character,
            ClientMoveItemToInventoryMessage message,
            IZoneClient client)
        {
            IInventoryPage sendingPage;
            if (!this.TryGetSourcePage(character, message.SourceContainer, out sendingPage))
            {
                return false;
            }

            int fromPlacement = message.SourceContainer.Instance;
            IItem itemFrom = sendingPage[fromPlacement];
            if (itemFrom == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "ClientMoveItemToInventory source slot is empty source={0} targetPlacement={1} character={2}",
                        message.SourceContainer,
                        message.TargetPlacement,
                        character.Identity));
                return true;
            }

            if (message.SourceContainer.Type == IdentityType.Inventory)
            {
                Identity backpackContainerIdentity;
                InventoryItemRules.TryEnsureBackpackContainerIdentity(
                    itemFrom,
                    character.Identity,
                    message.SourceContainer,
                    out backpackContainerIdentity);
            }

            IInventoryPage receivingPage = this.GetTargetPage(character, message.TargetPlacement);
            if (receivingPage == null)
            {
                return false;
            }

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "ClientMoveItemToInventory resolved char={0} fromPage={1} fromSlot={2} toPage={3} rawTarget={4} item={5}/{6} ql={7}",
                    character.Identity,
                    sendingPage.GetType().Name,
                    fromPlacement,
                    receivingPage.GetType().Name,
                    message.TargetPlacement,
                    itemFrom.LowID,
                    itemFrom.HighID,
                    itemFrom.Quality));

            int ackTargetPlacement = message.TargetPlacement;
            int toPlacement = message.TargetPlacement;
            if (toPlacement == (int)IdentityType.TradeWindow)
            {
                toPlacement = receivingPage.FindFreeSlot();
            }

            if (toPlacement < 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "ClientMoveItemToInventory target inventory is full source={0} targetPlacement={1} character={2}",
                        message.SourceContainer,
                        message.TargetPlacement,
                        character.Identity));
                return true;
            }

            IItemSlotHandler equipTo = receivingPage as IItemSlotHandler;
            IItemSlotHandler unequipFrom = sendingPage as IItemSlotHandler;
            IItem itemTo = receivingPage[toPlacement];
            bool affectsAppearance = this.IsAppearanceEquipmentPage(receivingPage) || this.IsAppearanceEquipmentPage(sendingPage);

            if (equipTo != null)
            {
                if (this.RequiresImplantAccess(receivingPage) && !this.HasImplantAccess(character))
                {
                    this.SendImplantAccessDenied(character);
                    return true;
                }

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "ClientMoveItemToInventory equip path char={0} targetSlot={1} itemToPresent={2}",
                        character.Identity,
                        toPlacement,
                        itemTo != null ? 1 : 0));

                if (receivingPage.NeedsItemCheck && !this.CanEquipToPage(character, receivingPage, itemFrom))
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        string.Format(
                            "ClientMoveItemToInventory equip requirements failed item={0}/{1}:{2} source={3} targetPlacement={4} character={5}",
                            itemFrom.LowID,
                            itemFrom.HighID,
                            itemFrom.Quality,
                            message.SourceContainer,
                            toPlacement,
                            character.Identity));
                    return true;
                }

                WeaponItemFullUpdate.SendWeaponDefinition(character, itemFrom);

                if (itemTo != null)
                {
                    if (affectsAppearance)
                    {
                        this.WaitForEquipVisualSync(itemFrom, itemTo, receivingPage is SocialArmorInventoryPage);
                    }

                    UnEquip.Send(client, receivingPage, toPlacement);
                    equipTo.HotSwap(sendingPage, fromPlacement, toPlacement);
                }
                else
                {
                    if (affectsAppearance)
                    {
                        this.WaitForEquipVisualSync(itemFrom, null, receivingPage is SocialArmorInventoryPage);
                    }

                    if (sendingPage == receivingPage)
                    {
                        UnEquip.Send(client, sendingPage, fromPlacement);
                    }

                    equipTo.Equip(sendingPage, fromPlacement, toPlacement);
                }

                InventoryContainerRuntimeService.Default.SendMoveItemToInventoryAck(
                    character,
                    message.SourceContainer,
                    ackTargetPlacement);
                Equip.Send(client, receivingPage, toPlacement);
                character.CalculateSkills();
                EnsureWeaponVisualMeshes(character, true);
                InventoryContainerRuntimeService.Default.PersistClientMoveItemToInventory(character, "equip");
                return true;
            }

            if (unequipFrom != null)
            {
                if (this.RequiresImplantAccess(sendingPage) && !this.HasImplantAccess(character))
                {
                    this.SendImplantAccessDenied(character);
                    return true;
                }

                if (affectsAppearance)
                {
                    this.WaitForEquipVisualSync(itemFrom, null, sendingPage is SocialArmorInventoryPage);
                }

                UnEquip.Send(client, sendingPage, fromPlacement);
                unequipFrom.Unequip(fromPlacement, receivingPage, toPlacement);
                InventoryContainerRuntimeService.Default.SendMoveItemToInventoryAck(
                    character,
                    message.SourceContainer,
                    ackTargetPlacement);
                character.CalculateSkills();
                EnsureWeaponVisualMeshes(character, true);
                InventoryContainerRuntimeService.Default.PersistClientMoveItemToInventory(character, "unequip");
                return true;
            }

            sendingPage.Remove(fromPlacement);
            receivingPage.Add(toPlacement, itemFrom);
            InventoryContainerRuntimeService.Default.SendMoveItemToInventoryAck(
                character,
                message.SourceContainer,
                ackTargetPlacement);
            InventoryContainerRuntimeService.Default.PersistClientMoveItemToInventory(character, "move");
            return true;
        }

        private bool TryGetSourcePage(ICharacter character, Identity sourceContainer, out IInventoryPage sendingPage)
        {
            sendingPage = null;

            if (character.BaseInventory.Pages.ContainsKey((int)sourceContainer.Type))
            {
                sendingPage = character.BaseInventory.Pages[(int)sourceContainer.Type];
                return true;
            }

            try
            {
                sendingPage = character.BaseInventory.PageFromSlot(sourceContainer.Instance);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private IInventoryPage GetTargetPage(ICharacter character, int targetPlacement)
        {
            if (targetPlacement == (int)IdentityType.TradeWindow)
            {
                return character.BaseInventory.Pages[character.BaseInventory.StandardPage];
            }

            try
            {
                return character.BaseInventory.PageFromSlot(targetPlacement);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private bool CanEquipToPage(ICharacter character, IInventoryPage page, IItem item)
        {
            AOAction action = null;
            if ((page is ArmorInventoryPage) || (page is ImplantInventoryPage))
            {
                action = item.ItemActions.SingleOrDefault(x => x.ActionType == ActionType.ToWear);
            }
            else if (page is WeaponInventoryPage)
            {
                action = item.ItemActions.SingleOrDefault(x => x.ActionType == ActionType.ToWield);
            }

            return action == null || action.CheckRequirements(character);
        }

        private bool RequiresImplantAccess(IInventoryPage page)
        {
            return page is ImplantInventoryPage;
        }

        private bool HasImplantAccess(ICharacter character)
        {
            Character concreteCharacter = character as Character;
            return concreteCharacter != null && concreteCharacter.HasImplantAccess();
        }

        private void SendImplantAccessDenied(ICharacter character)
        {
            ChatTextMessageHandler.Default.Send(character, "Accessing implants requires technical supervision.");
        }

        private bool IsAppearanceEquipmentPage(IInventoryPage page)
        {
            return page is WeaponInventoryPage || page is ArmorInventoryPage || page is SocialArmorInventoryPage;
        }

        private bool IsWeaponPage(IInventoryPage page)
        {
            return page is WeaponInventoryPage;
        }

        private void WaitForEquipVisualSync(IItem primary, IItem secondary, bool isSocial)
        {
            int delay = this.GetEquipDelay(primary, isSocial);
            if (secondary != null)
            {
                delay += this.GetEquipDelay(secondary, isSocial);
            }

            Thread.Sleep(delay * 10);
        }

        private int GetEquipDelay(IItem item, bool isSocial)
        {
            if (item == null || isSocial)
            {
                return 20;
            }

            int delay = item.GetAttribute(211);
            return delay == 1234567890 ? 20 : delay;
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
