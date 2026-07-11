#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System.Collections.Concurrent;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Nanos;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

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
            if (!PetShellCatalog.UsesShellOnSummon(character.Stats[StatIds.profession].Value))
            {
                return false;
            }

            PetSummonParams summonParams;
            if (!PetSummonNanoCatalog.TryResolve(character, nanoId, out summonParams))
            {
                return false;
            }

            int shellItemId;
            int shellQuality;
            if (!PetShellCatalog.TryGetShellItemForProfession(
                character.Stats[StatIds.profession].Value,
                out shellItemId,
                out shellQuality))
            {
                return false;
            }

            var definition = new PetShellDefinition(
                PetShellCatalog.ResolveKind(character.Stats[StatIds.profession].Value),
                shellItemId,
                shellQuality,
                nanoId,
                summonParams.PetHash,
                summonParams.PetTypeId);

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
            if (character == null || item == null)
            {
                return false;
            }

            PetShellDefinition definition;
            if (!this.TryResolveDefinition(character, itemPosition, item, out definition))
            {
                return false;
            }

            bool summoned = PetRuntimeService.Default.SummonPet(
                character,
                definition.PetHash,
                definition.PetTypeId,
                this.ResolveShellPetStrain(definition.NanoId),
                definition.NanoId);

            if (!summoned)
            {
                ChatTextMessageHandler.Default.Send(character, "Pet shell failed to summon a pet.");
            }
            else
            {
                this.ConsumeShell(character, itemPosition);
            }

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

            if (!PetShellCatalog.UsesShellOnSummon(character.Stats[StatIds.profession].Value))
            {
                return;
            }

            if (this.CharacterAlreadyHasRegisteredShell(character))
            {
                return;
            }

            this.TryGiveShellForNano(character, nanoId);
        }

        private bool TryGiveShell(ICharacter character, PetShellDefinition definition)
        {
            if (character == null || character.BaseInventory == null || definition == null)
            {
                return false;
            }

            if (!ItemLoader.ItemList.ContainsKey(definition.DisplayItemLowId))
            {
                LogUtil.Debug(
                    DebugInfoDetail.GameFunctions,
                    "GivePetShell missing item template " + definition.DisplayItemLowId);
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
                definition.DisplayItemLowId);

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

        private bool CharacterAlreadyHasRegisteredShell(ICharacter character)
        {
            int ownerInstance = character.Identity.Instance;
            return this.shellsBySlot.Keys.Any(x => x.OwnerInstance == ownerInstance);
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

            PetSummonParams summonParams;
            if (PetShellCatalog.TryGetByDisplayLowId(item.LowID, out definition)
                && PetSummonNanoCatalog.TryResolve(character, definition.NanoId, out summonParams))
            {
                definition = new PetShellDefinition(
                    definition.Kind,
                    definition.DisplayItemLowId,
                    definition.DisplayQuality,
                    definition.NanoId,
                    summonParams.PetHash,
                    summonParams.PetTypeId);
                return true;
            }

            definition = null;
            return false;
        }

        private int ResolveShellPetStrain(int nanoId)
        {
            NanoFormula nano;
            if (NanoLoader.NanoList.TryGetValue(nanoId, out nano))
            {
                return nano.NanoStrain();
            }

            return 0;
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
            int petTypeId)
        {
            this.Kind = kind;
            this.DisplayItemLowId = displayItemLowId;
            this.DisplayQuality = displayQuality;
            this.NanoId = nanoId;
            this.PetHash = petHash;
            this.PetTypeId = petTypeId;
        }

        public PetShellKind Kind { get; private set; }

        public int DisplayItemLowId { get; private set; }

        public int DisplayQuality { get; private set; }

        public int NanoId { get; private set; }

        public string PetHash { get; set; }

        public int PetTypeId { get; set; }
    }

    internal static class PetShellCatalog
    {
        private static readonly PetShellDefinition EngineerShell = new PetShellDefinition(
            PetShellKind.Engineer,
            displayItemLowId: 43328,
            displayQuality: 1,
            nanoId: 43324,
            petHash: "PT50",
            petTypeId: 1);

        private static readonly PetShellDefinition BureaucratShell = new PetShellDefinition(
            PetShellKind.Bureaucrat,
            displayItemLowId: 96235,
            displayQuality: 1,
            nanoId: 43718,
            petHash: "PT51",
            petTypeId: 19);

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
            if (lowId == EngineerShell.DisplayItemLowId)
            {
                definition = EngineerShell;
                return true;
            }

            if (lowId == BureaucratShell.DisplayItemLowId)
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
    }
}

