namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    public sealed class GuestKeyGeneratorInteractionHandler
    {
        public static readonly GuestKeyGeneratorInteractionHandler Default =
            new GuestKeyGeneratorInteractionHandler();

        private const int CapturedCityAccessCardIdentityType = 0x0000C770;

        private const int CapturedCityAccessCardInstance = 0x006D780D;

        private const int CapturedCityAccessCardStateMachineType = 0x000F424F;

        private const int CapturedCityAccessCardBuildingType = 0x0000C79E;

        private const int CapturedCityAccessCardBuildingInstance = 0x0000177A;

        private const int CapturedCityAccessCardOwnerType = 0x000186A1;

        private const int CapturedCityAccessCardOwnerInstance = 0x0001177A;

        private const uint CapturedCityAccessCardBuildingComplexInstance = 0xC017028F;

        private const int CapturedCityAccessCardTimeExist = 90000;

        private const int CityAccessCardExpiresAtUnixSecondsStat = 0x6B657931;

        private const int CapturedPrivateCityOrganizationInstance = 1370122;

        private static readonly object CityAccessCardExpirationSync = new object();

        private static readonly Dictionary<ulong, Timer> CityAccessCardExpirationTimers =
            new Dictionary<ulong, Timer>();

        private static readonly DateTime UnixEpochUtc =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static int cityAccessCardInstanceSeed =
            Math.Max(
                CapturedCityAccessCardInstance,
                unchecked((int)(DateTime.UtcNow.Ticks & 0x3fffffff)));

        private GuestKeyGeneratorInteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            ICharacter character = client.Controller.Character;
            if (character == null
                || character.Playfield == null
                || !GuestKeyGeneratorInteractionRules.IsPrivateCityGuestKeyTerminalTarget(target)
                || !AORebirth.Core.Playfields.Playfield.IsPrivateCityPlayfieldCandidate(character.Playfield.Identity))
            {
                return false;
            }

            Item cityAccessCard;
            int inventorySlot;
            InventoryError inventoryError;
            if (!TryCreateAndPersistCityAccessCard(character, out cityAccessCard, out inventorySlot, out inventoryError))
            {
                ChatTextMessageHandler.Default.Send(
                    character,
                    "Could not generate a guest key. (" + inventoryError + ")");
                GenericCmdMessageHandler.Default.Acknowledge(character, message);
                return true;
            }

            client.SendCompressed(CreateCapturedCityAccessCardItem(character, cityAccessCard.Identity));
            client.SendCompressed(
                new ContainerAddItemMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Target = new Identity { Type = IdentityType.OverflowWindow, Instance = character.Identity.Instance },
                    TargetPlacement = GuestKeyGeneratorInteractionRules.CapturedCityAccessCardOverflowSlot
                });
            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            client.Server.Info(
                client,
                "Private city guest key terminal created captured City Access Card character={0} terminal={1} template={2} overflowSlot={3} inventorySlot={4} item={5} lifetimeMs={6} evidence=private_city_capture_20260623_012720 runtime_target=574B84AB persisted=1",
                character.Identity,
                target,
                GuestKeyGeneratorInteractionRules.CapturedCityAccessCardTemplateId,
                GuestKeyGeneratorInteractionRules.CapturedCityAccessCardOverflowSlot,
                inventorySlot,
                cityAccessCard.Identity,
                GuestKeyGeneratorInteractionRules.CityAccessCardLifetimeMilliseconds);

            return true;
        }

        public static void ProcessCityAccessCardLifetimes(ICharacter character)
        {
            if (character == null || character.BaseInventory == null)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List().ToList())
                {
                    if (!IsCityAccessCard(itemEntry.Value))
                    {
                        continue;
                    }

                    DateTime expiresAtUtc = GetCityAccessCardExpirationUtc(itemEntry.Value);
                    if (expiresAtUtc <= nowUtc)
                    {
                        TryRemoveCityAccessCard(character, itemEntry.Value.Identity.Long(), false);
                        continue;
                    }

                    RegisterCityAccessCardExpiration(itemEntry.Value.Identity, expiresAtUtc);
                }
            }
        }

        private static bool TryCreateAndPersistCityAccessCard(
            ICharacter character,
            out Item cityAccessCard,
            out int inventorySlot,
            out InventoryError inventoryError)
        {
            cityAccessCard = null;
            inventorySlot = -1;
            inventoryError = InventoryError.Invalid;

            if (character == null || character.BaseInventory == null)
            {
                return false;
            }

            IInventoryPage inventoryPage;
            if (!character.BaseInventory.Pages.TryGetValue(character.BaseInventory.StandardPage, out inventoryPage))
            {
                return false;
            }

            inventorySlot = inventoryPage.FindFreeSlot();
            if (inventorySlot == -1)
            {
                inventoryError = InventoryError.InventoryIsFull;
                return false;
            }

            try
            {
                cityAccessCard = CreateCapturedCityAccessCardInventoryItem(character);
            }
            catch
            {
                inventoryError = InventoryError.Invalid;
                return false;
            }

            inventoryError = inventoryPage.Add(inventorySlot, cityAccessCard);
            if (inventoryError != InventoryError.OK)
            {
                return false;
            }

            try
            {
                if (!character.BaseInventory.Write())
                {
                    TryRemoveInventorySlot(inventoryPage, inventorySlot);
                    inventoryError = InventoryError.Invalid;
                    return false;
                }
            }
            catch
            {
                TryRemoveInventorySlot(inventoryPage, inventorySlot);
                inventoryError = InventoryError.Invalid;
                return false;
            }

            RegisterCityAccessCardExpiration(cityAccessCard.Identity);
            return true;
        }

        private static void TryRemoveInventorySlot(IInventoryPage inventoryPage, int inventorySlot)
        {
            try
            {
                if (inventoryPage != null && inventoryPage[inventorySlot] != null)
                {
                    inventoryPage.Remove(inventorySlot);
                }
            }
            catch
            {
            }
        }

        private static Item CreateCapturedCityAccessCardInventoryItem(ICharacter character)
        {
            var item = new Item(
                           1,
                           GuestKeyGeneratorInteractionRules.CapturedCityAccessCardTemplateId,
                           GuestKeyGeneratorInteractionRules.CapturedCityAccessCardTemplateId)
                       {
                           Identity =
                               new Identity
                               {
                                   Type = (IdentityType)CapturedCityAccessCardIdentityType,
                                   Instance = CreateCityAccessCardInstance()
                               },
                           Flags = 1
                       };

            foreach (GameTuple<CharacterStat, uint> stat in CreateCapturedCityAccessCardStats(
                         ResolvePrivateCityOrganizationInstance(character)))
            {
                item.SetAttribute((int)stat.Value1, unchecked((int)stat.Value2));
            }

            item.MultipleCount = 1;
            item.SetAttribute(
                CityAccessCardExpiresAtUnixSecondsStat,
                GetUnixTimeSecondsUtc(
                    DateTime.UtcNow.AddMilliseconds(
                        GuestKeyGeneratorInteractionRules.CityAccessCardLifetimeMilliseconds)));
            return item;
        }

        private static int CreateCityAccessCardInstance()
        {
            int instance = Interlocked.Increment(ref cityAccessCardInstanceSeed) & 0x7fffffff;
            return instance == 0 ? CreateCityAccessCardInstance() : instance;
        }

        private static void RegisterCityAccessCardExpiration(Identity itemIdentity)
        {
            RegisterCityAccessCardExpiration(
                itemIdentity,
                DateTime.UtcNow.AddMilliseconds(
                    GuestKeyGeneratorInteractionRules.CityAccessCardLifetimeMilliseconds));
        }

        private static void RegisterCityAccessCardExpiration(Identity itemIdentity, DateTime expiresAtUtc)
        {
            if (itemIdentity == null || itemIdentity.Type == IdentityType.None)
            {
                return;
            }

            ulong itemKey = itemIdentity.Long();
            int dueTime = GetTimerDueTimeMilliseconds(expiresAtUtc);
            Timer oldTimer = null;
            lock (CityAccessCardExpirationSync)
            {
                if (CityAccessCardExpirationTimers.TryGetValue(itemKey, out oldTimer))
                {
                    CityAccessCardExpirationTimers.Remove(itemKey);
                }

                CityAccessCardExpirationTimers[itemKey] =
                    new Timer(ExpireCityAccessCard, itemKey, dueTime, Timeout.Infinite);
            }

            if (oldTimer != null)
            {
                oldTimer.Dispose();
            }
        }

        private static void ExpireCityAccessCard(object state)
        {
            ulong itemKey = (ulong)state;
            Timer timer = null;
            lock (CityAccessCardExpirationSync)
            {
                if (CityAccessCardExpirationTimers.TryGetValue(itemKey, out timer))
                {
                    CityAccessCardExpirationTimers.Remove(itemKey);
                }
            }

            if (timer != null)
            {
                timer.Dispose();
            }

            foreach (ICharacter character in Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected))
            {
                if (TryRemoveCityAccessCard(character, itemKey, true))
                {
                    return;
                }
            }
        }

        private static bool TryRemoveCityAccessCard(ICharacter character, ulong itemKey, bool notifyClient)
        {
            if (character == null || character.BaseInventory == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List().ToList())
                {
                    if (!IsCityAccessCard(itemEntry.Value, itemKey))
                    {
                        continue;
                    }

                    try
                    {
                        pageEntry.Value.Remove(itemEntry.Key);
                        character.BaseInventory.Write();

                        if (notifyClient && character.Controller != null && character.Controller.Client != null)
                        {
                            CharacterActionMessageHandler.Default.SendDeleteItem(
                                character,
                                pageEntry.Value.Page,
                                itemEntry.Key);
                        }
                    }
                    catch
                    {
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }

        private static bool IsCityAccessCard(IItem item, ulong itemKey)
        {
            return IsCityAccessCard(item)
                   && item.Identity.Long() == itemKey;
        }

        private static bool IsCityAccessCard(IItem item)
        {
            return item != null
                   && item.LowID == GuestKeyGeneratorInteractionRules.CapturedCityAccessCardTemplateId
                   && item.HighID == GuestKeyGeneratorInteractionRules.CapturedCityAccessCardTemplateId
                   && item.Identity != null
                   && item.Identity.Type == (IdentityType)CapturedCityAccessCardIdentityType;
        }

        private static DateTime GetCityAccessCardExpirationUtc(IItem item)
        {
            int expiresAtUnixSeconds = item.GetAttribute(CityAccessCardExpiresAtUnixSecondsStat);
            if (expiresAtUnixSeconds <= 0)
            {
                return DateTime.UtcNow.AddMilliseconds(
                    GuestKeyGeneratorInteractionRules.CityAccessCardLifetimeMilliseconds);
            }

            return UnixEpochUtc.AddSeconds(expiresAtUnixSeconds);
        }

        private static int GetUnixTimeSecondsUtc(DateTime value)
        {
            return (int)Math.Max(0, (value.ToUniversalTime() - UnixEpochUtc).TotalSeconds);
        }

        private static int GetTimerDueTimeMilliseconds(DateTime expiresAtUtc)
        {
            double milliseconds = (expiresAtUtc.ToUniversalTime() - DateTime.UtcNow).TotalMilliseconds;
            if (milliseconds <= 0)
            {
                return 0;
            }

            return milliseconds > int.MaxValue ? int.MaxValue : (int)milliseconds;
        }

        private static SimpleItemFullUpdateMessage CreateCapturedCityAccessCardItem(
            ICharacter character,
            Identity itemIdentity)
        {
            return new SimpleItemFullUpdateMessage
                   {
                       Identity =
                           new Identity
                           {
                               Type = itemIdentity.Type,
                               Instance = itemIdentity.Instance
                           },
                       Unknown = 0,
                       MsgVersion = 0x0B,
                       Identitytype = (int)character.Identity.Type,
                       Instance = character.Identity.Instance,
                       Playfield = character.Playfield.Identity.Instance,
                       Unknown1 = new Identity { Type = (IdentityType)CapturedCityAccessCardStateMachineType, Instance = 0 },
                       Unknown2 = 0x71,
                       Unknown3 = GuestKeyGeneratorInteractionRules.CapturedCityAccessCardOverflowSlot,
                       Stats = CreateCapturedCityAccessCardStats(ResolvePrivateCityOrganizationInstance(character)),
                       Name = string.Empty
                   };
        }

        private static GameTuple<CharacterStat, uint>[] CreateCapturedCityAccessCardStats(int organizationInstance)
        {
            return new[]
                   {
                       CityAccessCardStat(CharacterStat.Flags, 1),
                       CityAccessCardStat(
                           CharacterStat.StaticInstance,
                           GuestKeyGeneratorInteractionRules.CapturedCityAccessCardTemplateId),
                       CityAccessCardStat(CharacterStat.ACGItemLevel, 1),
                       CityAccessCardStat(
                           CharacterStat.ACGItemTemplateID,
                           GuestKeyGeneratorInteractionRules.CapturedCityAccessCardTemplateId),
                       CityAccessCardStat(
                           CharacterStat.ACGItemTemplateID2,
                           GuestKeyGeneratorInteractionRules.CapturedCityAccessCardTemplateId),
                       CityAccessCardStat(CharacterStat.MultipleCount, 1),
                       CityAccessCardStat(CharacterStat.BuildingType, CapturedCityAccessCardBuildingType),
                       CityAccessCardStat(CharacterStat.BuildingInstance, CapturedCityAccessCardBuildingInstance),
                       CityAccessCardStat(CharacterStat.CardOwnerType, CapturedCityAccessCardOwnerType),
                       CityAccessCardStat(CharacterStat.CardOwnerInstance, CapturedCityAccessCardOwnerInstance),
                       CityAccessCardStat(CharacterStat.BuildingComplexInst, CapturedCityAccessCardBuildingComplexInstance),
                       CityAccessCardStat(CharacterStat.AccessKey, 0),
                       CityAccessCardStat(CharacterStat.ExternalPlayfieldInstance, organizationInstance),
                       CityAccessCardStat(CharacterStat.TimeExist, CapturedCityAccessCardTimeExist)
                   };
        }

        private static GameTuple<CharacterStat, uint> CityAccessCardStat(CharacterStat stat, int value)
        {
            return CityAccessCardStat(stat, unchecked((uint)value));
        }

        private static GameTuple<CharacterStat, uint> CityAccessCardStat(CharacterStat stat, uint value)
        {
            return new GameTuple<CharacterStat, uint> { Value1 = stat, Value2 = value };
        }

        private static int ResolvePrivateCityOrganizationInstance(ICharacter character)
        {
            int organizationInstance = ResolveCharacterOrganizationInstance(character);
            return organizationInstance > 0 ? organizationInstance : CapturedPrivateCityOrganizationInstance;
        }

        private static int ResolveCharacterOrganizationInstance(ICharacter character)
        {
            uint baseOrganizationInstance = character.Stats[StatIds.clan].BaseValue;
            if (baseOrganizationInstance > 0 && baseOrganizationInstance <= int.MaxValue)
            {
                return (int)baseOrganizationInstance;
            }

            int organizationInstance = character.Stats[StatIds.clan].Value;
            return organizationInstance > 0 ? organizationInstance : 0;
        }
    }
}
