#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Nanos;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Doja;
    using ZoneEngine.Core.MessageHandlers;

    using Utility;

    #endregion

    public sealed class PetShellItemService
    {
        private static readonly PetShellItemService DefaultInstance = new PetShellItemService();

        private readonly ConcurrentDictionary<PetShellKey, PetShellDefinition> shellsBySlot =
            new ConcurrentDictionary<PetShellKey, PetShellDefinition>();

        private PetShellItemService()
        {
        }

        public static PetShellItemService Default
        {
            get { return DefaultInstance; }
        }

        public bool TryGiveShellForNano(ICharacter character, int nanoId)
        {
            if (!PetShellCatalog.UsesShellOnSummon(
                character.Stats[StatIds.profession].Value,
                nanoId))
            {
                return false;
            }

            PetSummonParams summonParams;
            if (!PetSummonNanoCatalog.TryResolveShellSummonParams(nanoId, out summonParams)
                && !PetSummonNanoCatalog.TryResolve(character, nanoId, out summonParams))
            {
                return false;
            }

            PetShellDefinition shellTemplate;
            if (!PetShellCatalog.TryGet(
                PetShellCatalog.ResolveKind(character.Stats[StatIds.profession].Value),
                out shellTemplate))
            {
                return false;
            }

            int shellLowId = shellTemplate.DisplayItemLowId;
            int shellHighId = shellTemplate.DisplayItemHighId;
            int shellQuality = shellTemplate.DisplayQuality;
            CapturedBureaucratShellDisplay shellDisplay;
            if (PetEngineerSummonCatalog.TryGetShellDisplay(nanoId, out shellDisplay)
                || PetSummonNanoCatalog.TryGetBureaucratShellDisplay(nanoId, out shellDisplay))
            {
                shellLowId = shellDisplay.DisplayItemLowId;
                shellHighId = shellDisplay.DisplayItemHighId;
                shellQuality = shellDisplay.DisplayQuality;
            }
            else
            {
                CapturedBureaucratPetProfile profile;
                if (PetSummonNanoCatalog.TryGetBureaucratProfile(nanoId, out profile)
                    || PetEngineerSummonCatalog.TryGetProfile(nanoId, out profile))
                {
                    shellQuality = profile.Level;
                }
            }

            var definition = new PetShellDefinition(
                shellTemplate.Kind,
                shellLowId,
                shellQuality,
                nanoId,
                summonParams.PetHash,
                summonParams.PetTypeId,
                shellHighId);

            return this.TryGiveShell(character, definition);
        }

        public bool TryGiveShell(ICharacter character, PetShellKind kind)
        {
            PetShellDefinition definition;
            if (!PetShellCatalog.TryGet(kind, out definition))
            {
                return false;
            }

            return this.TryGiveShell(character, definition);
        }

        public bool TryUsePetShell(ICharacter character, Identity itemPosition, Item item)
        {
            if (character == null || item == null || IsNanoCrystalItem(item))
            {
                return false;
            }

            // DOJA chips are quest items; never treat as pet shells even if itemdata OnUse looks summon-like.
            if (DojaChipInteractionRules.IsKnownDojaChip(item.LowID, item.HighID))
            {
                return false;
            }

            PetShellDefinition definition;
            if (!this.TryResolveDefinition(character, itemPosition, item, out definition))
            {
                return false;
            }

            int shellPetStrain = PetSlotClassifier.ResolveStrain(definition.PetHash);
            if (shellPetStrain == PetSlotClassifier.RegularPetStrain
                && PetRuntimeService.Default.HasLivingAttackPet(character))
            {
                ChatTextMessageHandler.Default.Send(character, "You can have just 1 Attack Pet.");
                return true;
            }

            if (shellPetStrain == PetSlotClassifier.HealingPetStrain
                && PetRuntimeService.Default.HasLivingHealingPet(character))
            {
                ChatTextMessageHandler.Default.Send(character, "You can have just 1 Heal Pet.");
                return true;
            }

            if (PetSlotClassifier.IsSupportPetStrain(shellPetStrain)
                && PetRuntimeService.Default.HasLivingSupportPet(character))
            {
                ChatTextMessageHandler.Default.Send(character, "You can have just 1 Support Pet.");
                return true;
            }

            if (PetSlotClassifier.IsBureaucratCompanionStrain(shellPetStrain)
                && PetRuntimeService.Default.HasLivingBureaucratCompanionPet(character))
            {
                ChatTextMessageHandler.Default.Send(
                    character,
                    "You can have just 1 Bureaucrat Companion Pet.");
                return true;
            }

            bool summoned = PetRuntimeService.Default.SummonPet(
                character,
                definition.PetHash,
                definition.PetTypeId,
                PetSlotClassifier.ResolveStrain(definition.PetHash),
                definition.NanoId);

            if (!summoned)
            {
                return true;
            }

            this.ConsumeShell(character, itemPosition);

            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                string.Format(
                    "UsePetShell kind={0} owner={1} slot={2} item={3} hash={4} type={5} ok={6}",
                    definition.Kind,
                    character.Identity,
                    itemPosition.Instance,
                    item.LowID,
                    definition.PetHash,
                    definition.PetTypeId,
                    summoned));

            return true;
        }

        public void ConsumeShell(ICharacter character, Identity itemPosition)
        {
            if (character == null || character.BaseInventory == null)
            {
                return;
            }

            var shellKey = new PetShellKey(
                character.Identity.Instance,
                (int)itemPosition.Type,
                itemPosition.Instance);

            PetShellDefinition removedDefinition;
            this.shellsBySlot.TryRemove(shellKey, out removedDefinition);

            character.BaseInventory.RemoveItem((int)itemPosition.Type, itemPosition.Instance);
            character.BaseInventory.Write();

            CharacterActionMessageHandler.Default.SendDeleteItem(
                character,
                (int)itemPosition.Type,
                itemPosition.Instance);
        }

        public void GiveShellAfterNanoRestore(ICharacter character, int nanoId)
        {
            if (character == null || !NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId))
            {
                return;
            }

            if (!PetShellCatalog.UsesShellOnSummon(
                character.Stats[StatIds.profession].Value,
                nanoId))
            {
                return;
            }

            if (this.CharacterAlreadyHasShell(character, nanoId))
            {
                return;
            }

            this.TryGiveShellForNano(character, nanoId);
        }

        public void RegisterInventoryShells(ICharacter character)
        {
            if (character == null || character.BaseInventory == null || character.BaseInventory.Pages == null)
            {
                return;
            }

            int ownerInstance = character.Identity.Instance;
            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                IInventoryPage page = pageEntry.Value;
                if (page == null)
                {
                    continue;
                }

                foreach (KeyValuePair<int, IItem> slotEntry in page.List())
                {
                    Item inventoryItem = slotEntry.Value as Item;
                    if (inventoryItem == null || !IsPetShellItem(inventoryItem))
                    {
                        continue;
                    }

                    int slot = slotEntry.Key;
                    PetShellDefinition definition;
                    if (!this.TryBuildDefinitionFromShellItem(character, inventoryItem, out definition))
                    {
                        continue;
                    }

                    this.shellsBySlot[new PetShellKey(ownerInstance, pageEntry.Key, slot)] = definition;
                }
            }
        }

        private bool TryGiveShell(ICharacter character, PetShellDefinition definition)
        {
            if (character == null || character.BaseInventory == null || definition == null)
            {
                return false;
            }

            if (this.CharacterAlreadyHasShell(character, definition.NanoId))
            {
                ChatTextMessageHandler.Default.Send(
                    character,
                    "You already have a pet shell.");
                return false;
            }

            PetShellDisplayItemCatalog.EnsureRegistered(
                definition.DisplayItemLowId,
                definition.DisplayItemHighId,
                definition.NanoId);

            if (!ItemLoader.ItemList.ContainsKey(definition.DisplayItemLowId)
                || !ItemLoader.ItemList.ContainsKey(definition.DisplayItemHighId))
            {
                LogUtil.Debug(
                    DebugInfoDetail.GameFunctions,
                    string.Format(
                        "GivePetShell missing item template low={0} high={1}",
                        definition.DisplayItemLowId,
                        definition.DisplayItemHighId));
                return false;
            }

            this.EnsureNanoUploaded(character, definition.NanoId);

            int pageId = character.BaseInventory.StandardPage;
            IInventoryPage page = character.BaseInventory.Pages[pageId];
            int slot = page.FindFreeSlot();
            if (slot < 0)
            {
                return false;
            }

            Item item = new Item(
                definition.DisplayQuality,
                definition.DisplayItemLowId,
                definition.DisplayItemHighId);

            InventoryError err = character.BaseInventory.AddToPage(pageId, slot, item);
            if (err != InventoryError.OK)
            {
                return false;
            }

            character.BaseInventory.Write();

            var shellKey = new PetShellKey(character.Identity.Instance, pageId, slot);
            this.shellsBySlot[shellKey] = definition;

            AddTemplateMessageHandler.Default.Send(character, item);

            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                string.Format(
                    "GivePetShell kind={0} owner={1} slot={2} item={3} ql={4} hash={5} nano={6}",
                    definition.Kind,
                    character.Identity,
                    slot,
                    definition.DisplayItemLowId,
                    definition.DisplayQuality,
                    definition.PetHash,
                    definition.NanoId));

            return true;
        }

        private bool CharacterAlreadyHasShell(ICharacter character, int nanoId = 0)
        {
            if (character == null)
            {
                return false;
            }

            int ownerInstance = character.Identity.Instance;
            foreach (KeyValuePair<PetShellKey, PetShellDefinition> shellEntry in this.shellsBySlot)
            {
                if (shellEntry.Key.OwnerInstance != ownerInstance)
                {
                    continue;
                }

                if (nanoId <= 0 || shellEntry.Value.NanoId == nanoId)
                {
                    return true;
                }
            }

            if (character.BaseInventory == null || character.BaseInventory.Pages == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                IInventoryPage page = pageEntry.Value;
                if (page == null)
                {
                    continue;
                }

                foreach (KeyValuePair<int, IItem> slotEntry in page.List())
                {
                    Item inventoryItem = slotEntry.Value as Item;
                    if (inventoryItem == null || !IsPetShellItem(inventoryItem))
                    {
                        continue;
                    }

                    if (nanoId <= 0)
                    {
                        return true;
                    }

                    PetShellDefinition existingDefinition;
                    if (this.TryBuildDefinitionFromShellItem(character, inventoryItem, out existingDefinition)
                        && existingDefinition.NanoId == nanoId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryResolveDefinition(
            ICharacter character,
            Identity itemPosition,
            Item item,
            out PetShellDefinition definition)
        {
            var shellKey = new PetShellKey(
                character.Identity.Instance,
                (int)itemPosition.Type,
                itemPosition.Instance);

            if (this.shellsBySlot.TryGetValue(shellKey, out definition))
            {
                return true;
            }

            if (!this.TryBuildDefinitionFromShellItem(character, item, out definition))
            {
                definition = null;
                return false;
            }

            this.shellsBySlot[shellKey] = definition;
            return true;
        }

        private bool TryBuildDefinitionFromShellItem(
            ICharacter character,
            Item item,
            out PetShellDefinition definition)
        {
            definition = null;
            if (character == null || item == null)
            {
                return false;
            }

            PetSummonParams summonParams;
            PetShellDefinition shellTemplate;
            if (!PetSummonNanoCatalog.TryResolveShellSummonForItem(
                    character,
                    item.LowID,
                    item.HighID,
                    item.Quality,
                    character.Stats[StatIds.profession].Value,
                    out summonParams))
            {
                return false;
            }

            if (!PetShellCatalog.TryGetByDisplayLowId(item.LowID, out shellTemplate))
            {
                PetShellKind kind = PetShellCatalog.ResolveKind(
                    character.Stats[StatIds.profession].Value);
                if (!PetShellCatalog.TryGet(kind, out shellTemplate)
                    && !PetShellCatalog.TryGetBureaucratFallback(out shellTemplate))
                {
                    return false;
                }
            }

            definition = new PetShellDefinition(
                shellTemplate.Kind,
                item.LowID,
                item.Quality,
                summonParams.NanoId,
                summonParams.PetHash,
                summonParams.PetTypeId,
                item.HighID);
            return true;
        }

        public static bool IsPetShellItem(Item item)
        {
            return item != null
                && !IsNanoCrystalItem(item)
                && IsDisplayShellItem(item.LowID);
        }

        public static bool IsDisplayShellItem(int lowId)
        {
            if (PetSummonNanoCatalog.IsBureaucratShellItemLowId(lowId)
                || PetEngineerSummonCatalog.IsShellItemLowId(lowId))
            {
                return true;
            }

            PetShellDefinition ignored;
            return PetShellCatalog.TryGetByDisplayLowId(lowId, out ignored);
        }

        private static bool IsNanoCrystalItem(Item item)
        {
            if (item == null || item.Events == null)
            {
                return false;
            }

            const int uploadNanoFunctionId = (int)FunctionType.UploadNano;
            foreach (Event itemEvent in item.Events.Where(x => x.EventType == EventType.OnUse))
            {
                foreach (Function function in itemEvent.Functions)
                {
                    if (function.FunctionType == uploadNanoFunctionId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool TryEnsureNanoUploaded(ICharacter character, int nanoId)
        {
            this.EnsureNanoUploaded(character, nanoId);
            return character != null
                && character.UploadedNanos.Any(x => x.NanoId == nanoId);
        }

        private void EnsureNanoUploaded(ICharacter character, int nanoId)
        {
            Character characterEntity = character as Character;
            if (characterEntity == null || characterEntity.UploadedNanos.Any(x => x.NanoId == nanoId))
            {
                return;
            }

            if (!NanoLoader.NanoList.ContainsKey(nanoId))
            {
                return;
            }

            var uploadedNano = new UploadedNano { NanoId = nanoId };
            characterEntity.UploadedNanos.Add(uploadedNano);
            UploadedNanosDao.Instance.WriteNano(characterEntity.Identity.Instance, uploadedNano);

            if (characterEntity.Controller != null && characterEntity.Controller.Client != null)
            {
                var message = new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Action = CharacterActionType.UploadNano,
                    Target = character.Identity,
                    Parameter1 = (int)IdentityType.NanoProgram,
                    Parameter2 = nanoId,
                    Unknown = 0
                };

                characterEntity.Controller.Client.SendCompressed(message);
            }
        }

        private struct PetShellKey
        {
            public PetShellKey(int ownerInstance, int containerType, int slotInstance)
            {
                this.OwnerInstance = ownerInstance;
                this.ContainerType = containerType;
                this.SlotInstance = slotInstance;
            }

            public int OwnerInstance { get; private set; }

            public int ContainerType { get; private set; }

            public int SlotInstance { get; private set; }
        }
    }

    public enum PetShellKind
    {
        Engineer,
        Bureaucrat,
        MetaPhysicist,
        Unknown
    }

    internal sealed class PetShellDefinition
    {
        public PetShellDefinition(
            PetShellKind kind,
            int displayItemLowId,
            int displayQuality,
            int nanoId,
            string petHash,
            int petTypeId,
            int displayItemHighId = 0)
        {
            this.Kind = kind;
            this.DisplayItemLowId = displayItemLowId;
            this.DisplayItemHighId = displayItemHighId > 0 ? displayItemHighId : displayItemLowId;
            this.DisplayQuality = displayQuality;
            this.NanoId = nanoId;
            this.PetHash = petHash;
            this.PetTypeId = petTypeId;
        }

        public PetShellKind Kind { get; private set; }

        public int DisplayItemLowId { get; private set; }

        public int DisplayItemHighId { get; private set; }

        public int DisplayQuality { get; private set; }

        public int NanoId { get; private set; }

        public string PetHash { get; set; }

        public int PetTypeId { get; set; }
    }

    internal static class PetShellCatalog
    {
        private static readonly PetShellDefinition EngineerShell = new PetShellDefinition(
            PetShellKind.Engineer,
            displayItemLowId: 96196,
            displayQuality: 1,
            nanoId: 43325,
            petHash: "PT10",
            petTypeId: 1,
            displayItemHighId: 96196);

        private static readonly PetShellDefinition BureaucratShell = new PetShellDefinition(
            PetShellKind.Bureaucrat,
            displayItemLowId: 96235,
            displayQuality: 1,
            nanoId: 46397,
            petHash: "A020",
            petTypeId: 2,
            displayItemHighId: 150722);

        private static readonly PetShellDefinition MetaPhysicistShell = new PetShellDefinition(
            PetShellKind.MetaPhysicist,
            displayItemLowId: 204709,
            displayQuality: 1,
            nanoId: 43723,
            petHash: "PT52",
            petTypeId: 52);

        public static bool TryGet(PetShellKind kind, out PetShellDefinition definition)
        {
            switch (kind)
            {
                case PetShellKind.Engineer:
                    definition = EngineerShell;
                    return true;
                case PetShellKind.Bureaucrat:
                    definition = BureaucratShell;
                    return true;
                case PetShellKind.MetaPhysicist:
                    definition = MetaPhysicistShell;
                    return true;
                default:
                    definition = null;
                    return false;
            }
        }

        public static bool TryGetShellItemForProfession(int profession, out int shellItemId, out int shellQuality)
        {
            PetShellDefinition definition;
            if (!TryGet(ResolveKind(profession), out definition))
            {
                shellItemId = 0;
                shellQuality = 0;
                return false;
            }

            shellItemId = definition.DisplayItemLowId;
            shellQuality = definition.DisplayQuality;
            return true;
        }

        public static bool UsesShellOnSummon(int profession)
        {
            return UsesShellOnSummon(profession, 0);
        }

        public static bool UsesShellOnSummon(int profession, int nanoId)
        {
            if (PetSummonNanoCatalog.IsDirectSummonNano(nanoId))
            {
                return false;
            }

            switch ((Profession)profession)
            {
                case Profession.Engineer:
                case Profession.Bureaucrat:
                    return true;
                case Profession.Metaphysicist:
                    return false;
                default:
                    return true;
            }
        }

        public static PetShellKind ResolveKind(int profession)
        {
            switch ((Profession)profession)
            {
                case Profession.Engineer:
                    return PetShellKind.Engineer;
                case Profession.Bureaucrat:
                    return PetShellKind.Bureaucrat;
                case Profession.Metaphysicist:
                    return PetShellKind.MetaPhysicist;
                default:
                    return PetShellKind.Unknown;
            }
        }

        public static bool TryGetByDisplayLowId(int lowId, out PetShellDefinition definition)
        {
            if (lowId == EngineerShell.DisplayItemLowId
                || PetEngineerSummonCatalog.IsShellItemLowId(lowId))
            {
                definition = EngineerShell;
                return true;
            }

            if (lowId == BureaucratShell.DisplayItemLowId
                || PetSummonNanoCatalog.IsBureaucratShellItemLowId(lowId))
            {
                definition = BureaucratShell;
                return true;
            }

            if (lowId == MetaPhysicistShell.DisplayItemLowId)
            {
                definition = MetaPhysicistShell;
                return true;
            }

            definition = null;
            return false;
        }

        public static bool TryGetBureaucratFallback(out PetShellDefinition definition)
        {
            definition = BureaucratShell;
            return true;
        }
    }

    internal static class PetShellDisplayItemCatalog
    {
        public static void EnsureRegistered(int lowId, int highId, int nanoId = 0)
        {
            EnsureItem(lowId);
            if (highId != lowId)
            {
                EnsureItem(highId);
            }

            string shellName = null;
            if (nanoId > 0)
            {
                shellName = PetSummonNanoCatalog.GetBureaucratShellItemName(nanoId);
                if (string.IsNullOrWhiteSpace(shellName))
                {
                    CapturedBureaucratPetProfile engProfile;
                    if (PetEngineerSummonCatalog.TryGetProfile(nanoId, out engProfile))
                    {
                        shellName = engProfile.Name + " Shell";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(shellName))
            {
                return;
            }

            TradeSkill.Instance.ItemNames[lowId] = shellName;
            if (highId != lowId)
            {
                TradeSkill.Instance.ItemNames[highId] = shellName;
            }
        }

        private static void EnsureItem(int itemId)
        {
            if (ItemLoader.ItemList.ContainsKey(itemId))
            {
                return;
            }

            ItemLoader.ItemList[itemId] = new ItemTemplate
            {
                ID = itemId,
                Quality = 1,
                Flags = 0,
                ItemType = 0,
                Stats = new Dictionary<int, int>(),
                Attack = new Dictionary<int, int>(),
                Defend = new Dictionary<int, int>(),
            };
        }
    }
}

