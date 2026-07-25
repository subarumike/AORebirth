namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    #endregion

    /// <summary>
    /// FindItem / FindItemReturn objectives.
    /// Mission Cube Use (or corpse loot of the objective item) grants the real item.
    /// FindItem finishes on take; FindItemReturn finishes on UseItemOnItem (item → mission terminal).
    /// </summary>
    internal static class MissionFindItemService
    {
        private static readonly object Sync = new object();

        private static readonly HashSet<long> Cubes = new HashSet<long>();

        private static readonly HashSet<int> ReturnReadyCharacters = new HashSet<int>();

        private static long Key(Identity identity)
        {
            return ((long)(int)identity.Type << 32) | (uint)identity.Instance;
        }

        public static void RegisterCube(Identity npcIdentity)
        {
            if ((int)npcIdentity.Type == 0 || npcIdentity.Instance == 0)
            {
                return;
            }

            lock (Sync)
            {
                Cubes.Add(Key(npcIdentity));
            }
        }

        public static bool IsMissionCube(Identity npcIdentity)
        {
            lock (Sync)
            {
                return Cubes.Contains(Key(npcIdentity));
            }
        }

        public static void UnregisterCube(Identity npcIdentity)
        {
            lock (Sync)
            {
                Cubes.Remove(Key(npcIdentity));
            }
        }

        public static bool IsFindItemOffer(QuestInfo offer)
        {
            return offer != null
                   && (offer.MissionIconId == MissionTypeCatalog.FindItemIconA
                       || offer.MissionIconId == MissionTypeCatalog.FindItemIconB);
        }

        public static bool IsFindItemReturnOffer(QuestInfo offer)
        {
            return offer != null && offer.MissionIconId == MissionTypeCatalog.FindItemIconB;
        }

        public static bool IsFindItemKeepMission(MissionAcceptedStore.AcceptedMission entry)
        {
            return entry != null && entry.MissionIconId == MissionTypeCatalog.FindItemIconA;
        }

        public static bool IsFindItemReturnMission(MissionAcceptedStore.AcceptedMission entry)
        {
            return entry != null && entry.MissionIconId == MissionTypeCatalog.FindItemIconB;
        }

        public static bool IsObjectiveItem(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            return IsObjectiveTemplate(item.LowID, item.HighID);
        }

        public static bool IsObjectiveTemplate(int lowId, int highId)
        {
            MissionInstanceLootCatalog.LootDrop a = MissionInstanceLootCatalog.FindItemA;
            MissionInstanceLootCatalog.LootDrop b = MissionInstanceLootCatalog.FindItemB;
            return (lowId == a.LowId && highId == a.HighId)
                   || (lowId == b.LowId && highId == b.HighId);
        }

        /// <summary>
        /// GenericCmd Use on the Mission Cube: grant real item, then complete or arm return.
        /// </summary>
        public static bool TryHandleCubeUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (client == null || target == null || !IsMissionCube(target))
            {
                return false;
            }

            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null || character.Playfield == null
                || !MissionInstanceService.IsMissionInstancePlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            MissionAcceptedStore.AcceptedMission entry = ResolveActiveFindItemMission(character);
            if (entry == null)
            {
                return false;
            }

            MissionInstanceLootCatalog.LootDrop drop =
                MissionInstanceLootCatalog.ResolveFindItemDrop(target.Instance);
            int itemInstance;
            InventoryError error;
            if (!MissionKeyGrantService.TryGrantNamedItem(
                client,
                character,
                drop.LowId,
                drop.HighId,
                drop.Quality > 0 ? drop.Quality : entry.Quality > 0 ? entry.Quality : 1,
                ResolveItemDisplayName(drop),
                out itemInstance,
                out error))
            {
                client.Server.Info(client, "Mission FindItem cube grant failed: {0}", error);
                return true;
            }

            UnregisterCube(target);
            return FinishAfterItemTaken(client, character, entry, "FindItemCube");
        }

        /// <summary>
        /// After corpse loot of the objective item (backup path if the cube host was killed).
        /// When <paramref name="looted"/> is null, scans inventory for a known objective template.
        /// </summary>
        public static bool TryHandleAfterLoot(IZoneClient client, ICharacter character, IItem looted)
        {
            if (client == null || character == null)
            {
                return false;
            }

            if (looted != null && !IsObjectiveItem(looted))
            {
                return false;
            }

            if (looted == null && !CharacterHasObjectiveItem(character))
            {
                return false;
            }

            MissionAcceptedStore.AcceptedMission entry = ResolveActiveFindItemMission(character);
            if (entry == null)
            {
                return false;
            }

            return FinishAfterItemTaken(client, character, entry, "FindItemLoot");
        }

        private static bool CharacterHasObjectiveItem(ICharacter character)
        {
            if (character == null || character.BaseInventory == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List())
                {
                    if (IsObjectiveItem(itemEntry.Value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// L-click objective item + R-click mission terminal (UseItemOnItem).
        /// </summary>
        public static bool TryHandleReturnToTerminal(IZoneClient client, GenericCmdMessage message)
        {
            if (client == null || message == null || message.Target == null || message.Target.Length < 2)
            {
                return false;
            }

            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null)
            {
                return false;
            }

            Identity terminal = message.Target[1];
            if (!IsMissionTerminal(terminal))
            {
                return false;
            }

            MissionAcceptedStore.AcceptedMission entry = null;
            List<MissionAcceptedStore.AcceptedMission> all =
                MissionAcceptedStore.GetAll(character.Identity.Instance);
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (IsFindItemReturnMission(all[i]))
                {
                    entry = all[i];
                    break;
                }
            }

            if (entry == null)
            {
                return false;
            }

            bool returnReady;
            lock (Sync)
            {
                returnReady = ReturnReadyCharacters.Contains(character.Identity.Instance);
            }

            IInventoryPage sourcePage =
                Pool.Instance.GetObject<IInventoryPage>(
                    new Identity
                    {
                        Type = (IdentityType)character.Identity.Instance,
                        Instance = (int)message.Target[0].Type
                    });
            if (sourcePage == null)
            {
                return false;
            }

            IItem item = sourcePage[message.Target[0].Instance];
            if (!IsObjectiveItem(item))
            {
                if (!returnReady)
                {
                    client.Server.Info(client, "Mission FindItemReturn: need the mission item on the terminal");
                }

                return returnReady;
            }

            if (!TryConsumeObjectiveItem(client, character, item))
            {
                return true;
            }

            lock (Sync)
            {
                ReturnReadyCharacters.Remove(character.Identity.Instance);
            }

            return MissionCompleteService.TryComplete(client, character, entry, "FindItemReturn");
        }

        public static void ClearCharacter(int characterInstance)
        {
            lock (Sync)
            {
                ReturnReadyCharacters.Remove(characterInstance);
            }
        }

        private static bool FinishAfterItemTaken(
            IZoneClient client,
            ICharacter character,
            MissionAcceptedStore.AcceptedMission entry,
            string reason)
        {
            if (IsFindItemReturnMission(entry))
            {
                lock (Sync)
                {
                    ReturnReadyCharacters.Add(character.Identity.Instance);
                }

                character.Send(
                    new FormatFeedbackMessage
                    {
                        Identity = character.Identity,
                        Unknown = 1,
                        Unknown1 = 0,
                        Unknown2 = 0,
                        FormattedMessage =
                            "Mission item acquired. Return it to the mission terminal (L-click item, R-click terminal)."
                    });
                MissionDiagnostics.Log(
                    "FINDITEM-RETURN-ARMED char={0} reason={1}",
                    character.Identity.Instance,
                    reason);
                return true;
            }

            return MissionCompleteService.TryComplete(client, character, entry, reason);
        }

        private static MissionAcceptedStore.AcceptedMission ResolveActiveFindItemMission(ICharacter character)
        {
            List<MissionAcceptedStore.AcceptedMission> all =
                MissionAcceptedStore.GetAll(character.Identity.Instance);
            for (int i = all.Count - 1; i >= 0; i--)
            {
                MissionAcceptedStore.AcceptedMission entry = all[i];
                if (IsFindItemKeepMission(entry) || IsFindItemReturnMission(entry))
                {
                    return entry;
                }
            }

            return null;
        }

        private static bool IsMissionTerminal(Identity identity)
        {
            if (identity == null)
            {
                return false;
            }

            // Live MissionTerminal type is 0xDAC1 (enum MissionTerminal is wrong in IdentityType).
            const int MissionTerminalTypeRaw = 0x0000DAC1;
            return (int)identity.Type == MissionTerminalTypeRaw
                   || identity.Type == IdentityType.MissionTerminal;
        }

        private static string ResolveItemDisplayName(MissionInstanceLootCatalog.LootDrop drop)
        {
            if (drop == null)
            {
                return "Mission Item";
            }

            if (drop.LowId == MissionInstanceLootCatalog.FindItemA.LowId)
            {
                return "Radioactive Isotope Container";
            }

            if (drop.LowId == MissionInstanceLootCatalog.FindItemB.LowId)
            {
                return "Encrypted Info Capsule";
            }

            return "Mission Item";
        }

        private static bool TryConsumeObjectiveItem(IZoneClient client, ICharacter character, IItem item)
        {
            if (client == null || character == null || character.BaseInventory == null || item == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List().ToList())
                {
                    IItem candidate = itemEntry.Value;
                    if (candidate == null || candidate.Identity == null
                        || candidate.Identity.Instance != item.Identity.Instance)
                    {
                        continue;
                    }

                    try
                    {
                        pageEntry.Value.Remove(itemEntry.Key);
                        character.BaseInventory.Write();
                    }
                    catch
                    {
                        return false;
                    }

                    client.SendCompressed(
                        new DespawnMessage
                        {
                            Identity = candidate.Identity,
                            Unknown = 1
                        });
                    return true;
                }
            }

            return false;
        }
    }
}
