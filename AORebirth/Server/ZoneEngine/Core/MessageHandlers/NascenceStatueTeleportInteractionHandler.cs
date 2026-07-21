namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Vector;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Thrak.Quests;

    #endregion

    /// <summary>
    /// Shadowlands garden/zone statues — CellAO path:
    /// garden Use → item OnUse Teleport(53016); zone UseItemOnItem → stamp insignia + OnUseItemOn Teleport.
    /// Teleport args are taken from items.dat (same as Event.Perform would call).
    /// </summary>
    public sealed class NascenceStatueTeleportInteractionHandler
    {
        public static readonly NascenceStatueTeleportInteractionHandler Default =
            new NascenceStatueTeleportInteractionHandler();

        private const int ShadowlandsExpansionBit = 2;

        private NascenceStatueTeleportInteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            ICharacter character = client.Controller.Character;
            if (character == null
                || character.Playfield == null
                || target == null
                || target.Type != IdentityType.Terminal)
            {
                return false;
            }

            int sourcePlayfieldId = character.Playfield.Identity.Instance;
            if (!NascenceStatueTeleportCatalog.IsShadowlandsGardenPlayfield(sourcePlayfieldId))
            {
                return false;
            }

            client.Server.Info(
                client,
                "Shadowlands garden Use enter char={0} pf={1} target={2}",
                character.Identity,
                sourcePlayfieldId,
                target);

            this.EnsureShadowlandsExpansion(character);

            StaticDynel staticDynel = this.TryResolveStaticDynel(character, target);
            int templateId = 0;
            Event onUse = null;

            if (staticDynel != null && staticDynel.Template != null)
            {
                templateId = staticDynel.Template.ID;
                if (staticDynel.Events != null)
                {
                    onUse = staticDynel.Events.FirstOrDefault(x => x.EventType == EventType.OnUse);
                }
            }
            else
            {
                templateId = this.TryResolveTemplateIdFromDatabase(sourcePlayfieldId, target.Instance);
                client.Server.Info(
                    client,
                    "Shadowlands garden Use: Pool miss target={0} dbTemplate={1}",
                    target,
                    templateId);
                onUse = this.TryGetTemplateEvent(templateId, EventType.OnUse);
            }

            int destPf;
            float destX;
            float destY;
            float destZ;
            if (this.TryGetTeleportDestination(onUse, out destPf, out destX, out destY, out destZ))
            {
                this.TeleportCharacter(
                    client,
                    character,
                    message,
                    destPf,
                    destX,
                    destY,
                    destZ,
                    "ShadowlandsGardenOnUseTeleport",
                    target,
                    "template=" + templateId);
                return true;
            }

            string passageName = this.TryGetItemName(templateId);
            NascenceGardenPassageRoute route;
            if (!NascenceStatueTeleportCatalog.TryGetGardenPassageRouteByName(passageName, out route)
                && !NascenceStatueTeleportCatalog.TryGetGardenPassageRouteByTemplateId(templateId, out route))
            {
                client.Server.Info(
                    client,
                    "Shadowlands garden Use: no Teleport/catalog template={0} name={1}",
                    templateId,
                    passageName);
                return false;
            }

            this.TeleportCharacter(
                client,
                character,
                message,
                route.DestinationPlayfieldId,
                route.DestinationX,
                route.DestinationY,
                route.DestinationZ,
                "ShadowlandsGardenPassageCatalog",
                target,
                route.Evidence + " template=" + templateId + " name=" + passageName);

            return true;
        }

        public bool TryHandleUseItemOnItem(IZoneClient client, GenericCmdMessage message)
        {
            if (UseItemOnItemInteractionRules.ResolveRouteMode(message.Action)
                != UseItemOnItemInteractionRouteMode.UseItemOnItem)
            {
                return false;
            }

            if (message.Target == null
                || message.Target.Length < 2
                || message.Target[1] == null
                || message.Target[1].Type != IdentityType.Terminal)
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            int sourcePlayfieldId = character.Playfield.Identity.Instance;
            Identity terminalTarget = message.Target[1];
            if (!NascenceStatueTeleportCatalog.IsShadowlandsZonePlayfield(sourcePlayfieldId))
            {
                return false;
            }

            this.EnsureShadowlandsExpansion(character);

            IItem sourceItem = this.TryResolveSourceItem(character, message);
            if (sourceItem == null)
            {
                return false;
            }

            StaticDynel staticDynel = this.TryResolveStaticDynel(character, terminalTarget);
            int statueTemplateId = 0;
            Event onUseItemOn = null;

            if (staticDynel != null && staticDynel.Template != null)
            {
                statueTemplateId = staticDynel.Template.ID;
                if (staticDynel.Events != null)
                {
                    onUseItemOn = staticDynel.Events.FirstOrDefault(x => x.EventType == EventType.OnUseItemOn);
                }
            }
            else
            {
                statueTemplateId = this.TryResolveTemplateIdFromDatabase(sourcePlayfieldId, terminalTarget.Instance);
                onUseItemOn = this.TryGetTemplateEvent(statueTemplateId, EventType.OnUseItemOn);
            }

            if (statueTemplateId == 0)
            {
                return false;
            }

            // CellAO: stamp insignia onto secondaryitemtemplate before OnUseItemOn requirements.
            character.Stats[StatIds.secondaryitemtemplate].Value = sourceItem.LowID;

            if (!NascenceStatueTeleportCatalog.IsZoneReturnStatueTemplate(statueTemplateId)
                || !NascenceStatueTeleportCatalog.TryMatchReturnKey(statueTemplateId, sourceItem.LowID))
            {
                return false;
            }

            // Consume matched insignia used on the return statue (before teleport/dispose).
            // Sacred Thrak garden key (226994) is permanent.
            if (!ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(sourceItem.LowID, sourceItem.HighID))
            {
                this.ConsumeSourceInsignia(character, message, sourceItem);
            }

            // Insignia of Thrak on statue: journal advances to Garden stage (capture 20260718-185306).
            if (sourceItem.LowID == NascenceStatueTeleportCatalog.ThrakInsigniaTemplateId
                || sourceItem.HighID == NascenceStatueTeleportCatalog.ThrakInsigniaTemplateId)
            {
                ThrakGardenKeyQuestRuntime.TryAdvanceToGardenOnStatueEntry(character);
            }

            int destPf;
            float destX;
            float destY;
            float destZ;
            if (this.TryGetTeleportDestination(onUseItemOn, out destPf, out destX, out destY, out destZ))
            {
                this.TeleportCharacter(
                    client,
                    character,
                    message,
                    destPf,
                    destX,
                    destY,
                    destZ,
                    "ShadowlandsZoneOnUseItemOnTeleport",
                    terminalTarget,
                    "insignia=" + sourceItem.LowID + " statue=" + statueTemplateId);
                return true;
            }

            int gardenPlayfieldId = NascenceStatueTeleportCatalog.ResolveReturnGardenPlayfieldId(
                sourcePlayfieldId,
                character.Stats[StatIds.otunredeemed].Value);
            float gardenX;
            float gardenY;
            float gardenZ;
            NascenceStatueTeleportCatalog.ResolveReturnGardenPosition(
                gardenPlayfieldId,
                out gardenX,
                out gardenY,
                out gardenZ);

            this.TeleportCharacter(
                client,
                character,
                message,
                gardenPlayfieldId,
                gardenX,
                gardenY,
                gardenZ,
                "ShadowlandsZoneReturnCatalog",
                terminalTarget,
                "insignia=" + sourceItem.LowID + " template=" + statueTemplateId);

            return true;
        }

        private void EnsureShadowlandsExpansion(ICharacter character)
        {
            try
            {
                int expansion = character.Stats[StatIds.expansion].Value;
                if ((expansion & ShadowlandsExpansionBit) == 0)
                {
                    character.Stats[StatIds.expansion].Value = expansion | ShadowlandsExpansionBit;
                }
            }
            catch
            {
            }
        }

        private bool TryGetTeleportDestination(
            Event eventData,
            out int playfieldId,
            out float x,
            out float y,
            out float z)
        {
            playfieldId = 0;
            x = 0;
            y = 0;
            z = 0;
            if (eventData == null || eventData.Functions == null)
            {
                return false;
            }

            foreach (Function function in eventData.Functions)
            {
                if (function == null || function.FunctionType != (int)FunctionType.Teleport)
                {
                    continue;
                }

                if (function.Arguments == null || function.Arguments.Values == null
                    || function.Arguments.Values.Count < 4)
                {
                    continue;
                }

                List<MessagePackObject> values = function.Arguments.Values;
                x = Convert.ToSingle(values[0].ToObject());
                y = Convert.ToSingle(values[1].ToObject());
                z = Convert.ToSingle(values[2].ToObject());
                playfieldId = Convert.ToInt32(values[3].ToObject());
                if (playfieldId == 0)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private Event TryGetTemplateEvent(int templateId, EventType eventType)
        {
            if (templateId == 0)
            {
                return null;
            }

            ItemTemplate template;
            if (!ItemLoader.ItemList.TryGetValue(templateId, out template) || template.Events == null)
            {
                return null;
            }

            return template.Events.FirstOrDefault(x => x.EventType == eventType);
        }

        private int TryResolveTemplateIdFromDatabase(int playfieldId, int instance)
        {
            try
            {
                IEnumerable<DBStaticDynel> rows =
                    StaticDynelDao.Instance.GetWhere(new { Playfield = playfieldId, Instance = instance });
                DBStaticDynel row = rows == null ? null : rows.FirstOrDefault();
                if (row == null || row.stats == null)
                {
                    return 0;
                }

                List<GameTuple<CharacterStat, uint>> stats =
                    MessagePackZip.DeserializeData<GameTuple<CharacterStat, uint>>(row.stats.ToArray());
                GameTuple<CharacterStat, uint> templateStat =
                    stats.FirstOrDefault(x => x.Value1 == (CharacterStat)StatIds.acgitemtemplateid);
                return templateStat == null ? 0 : (int)templateStat.Value2;
            }
            catch
            {
                return 0;
            }
        }

        private string TryGetItemName(int templateId)
        {
            try
            {
                DBItemName row = ItemNamesDao.Instance.Get(templateId);
                return row == null ? string.Empty : row.Name;
            }
            catch
            {
                return string.Empty;
            }
        }

        private StaticDynel TryResolveStaticDynel(ICharacter character, Identity terminalTarget)
        {
            try
            {
                return Pool.Instance.GetObject<StaticDynel>(
                    character.Playfield.Identity,
                    terminalTarget);
            }
            catch (Exception)
            {
            }

            try
            {
                foreach (StaticDynel dynel in Pool.Instance.GetAll<StaticDynel>(character.Playfield.Identity))
                {
                    if (dynel.Identity.Type == terminalTarget.Type
                        && dynel.Identity.Instance == terminalTarget.Instance)
                    {
                        return dynel;
                    }
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private IItem TryResolveSourceItem(ICharacter character, GenericCmdMessage message)
        {
            if (character == null || message.Target == null || message.Target.Length < 1)
            {
                return null;
            }

            Identity sourceIdentity = message.Target[0];
            IItem item = null;

            try
            {
                IInventoryPage page;
                if (character.BaseInventory != null
                    && character.BaseInventory.Pages.TryGetValue((int)sourceIdentity.Type, out page)
                    && page != null)
                {
                    item = page[sourceIdentity.Instance];
                }

                if (item == null)
                {
                    item = Pool.Instance.GetObject<IInventoryPage>(
                               new Identity
                               {
                                   Type = (IdentityType)character.Identity.Instance,
                                   Instance = (int)sourceIdentity.Type
                               })[sourceIdentity.Instance];
                }
            }
            catch
            {
                item = null;
            }

            // HUD wear: client may report WeaponPage:Hud1 while the server still has the sacred
            // key only in carried inventory (phantom / desync). Fall back to a bag/HUD scan.
            if (item == null)
            {
                return this.TryFindSacredGardenKey(character);
            }

            return item;
        }

        private IItem TryFindSacredGardenKey(ICharacter character)
        {
            if (character == null || character.BaseInventory == null)
            {
                return null;
            }

            int[] pageTypes =
            {
                (int)IdentityType.WeaponPage,
                (int)IdentityType.Inventory
            };

            for (int i = 0; i < pageTypes.Length; i++)
            {
                IInventoryPage page;
                if (!character.BaseInventory.Pages.TryGetValue(pageTypes[i], out page) || page == null)
                {
                    continue;
                }

                for (int slot = page.FirstSlotNumber;
                     slot < page.FirstSlotNumber + page.MaxSlots;
                     slot++)
                {
                    IItem candidate = page[slot];
                    if (candidate != null
                        && ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(
                            candidate.LowID,
                            candidate.HighID))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private void ConsumeSourceInsignia(ICharacter character, GenericCmdMessage message, IItem sourceItem)
        {
            Item concrete = sourceItem as Item;
            if (concrete == null || message.Target == null || message.Target.Length < 1)
            {
                return;
            }

            concrete.MultipleCount--;
            if (concrete.MultipleCount <= 0)
            {
                character.BaseInventory.RemoveItem(
                    (int)message.Target[0].Type,
                    message.Target[0].Instance);
                CharacterActionMessageHandler.Default.SendDeleteItem(
                    character,
                    (int)message.Target[0].Type,
                    message.Target[0].Instance);
                return;
            }

            IInventoryPage page;
            if (character.BaseInventory.Pages.TryGetValue((int)message.Target[0].Type, out page))
            {
                page.Write();
            }
        }

        private void TeleportCharacter(
            IZoneClient client,
            ICharacter character,
            GenericCmdMessage message,
            int destinationPlayfieldId,
            float destinationX,
            float destinationY,
            float destinationZ,
            string routeKind,
            Identity target,
            string evidence)
        {
            // Playfield.Teleport no-ops while DoNotDoTimers is set (zoning lock).
            character.DoNotDoTimers = false;
            character.StopMovement();
            character.Stats[StatIds.externaldoorinstance].BaseValue = 0;
            character.Stats[StatIds.externalplayfieldinstance].BaseValue = 0;

            Dynel dynel = character as Dynel;
            if (dynel == null)
            {
                return;
            }

            var destination = new Coordinate(destinationX, destinationY, destinationZ);
            character.Playfield.Teleport(
                dynel,
                destination,
                character.Heading,
                new Identity { Type = IdentityType.Playfield, Instance = destinationPlayfieldId });

            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            client.Server.Info(
                client,
                "Shadowlands statue teleport handled char={0} target={1} sourcePf={2} destPf={3} dest=({4:F3},{5:F3},{6:F3}) route={7} evidence={8}",
                character.Identity,
                target,
                character.Playfield.Identity.Instance,
                destinationPlayfieldId,
                destinationX,
                destinationY,
                destinationZ,
                routeKind,
                evidence);
        }
    }
}
