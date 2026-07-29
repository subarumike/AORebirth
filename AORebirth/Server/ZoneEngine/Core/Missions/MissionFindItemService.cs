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

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// FindItem / FindItemReturn objectives.
    /// FindItem (keep): Mission Cube Use grants the real item and finishes.
    /// FindItemReturn (capture 20260728-095215): world Terminal 100361 Encrypted Info Capsule
    /// is PickUp'd into inventory; finish on UseItemOnItem (item → mission terminal).
    /// </summary>
    internal static class MissionFindItemService
    {
        private static readonly object Sync = new object();

        private static readonly HashSet<long> Cubes = new HashSet<long>();

        private static readonly HashSet<long> WorldPickups = new HashSet<long>();

        private static readonly HashSet<int> ReturnReadyCharacters = new HashSet<int>();

        // Capture 20260728-095215 CharacterAction Action=146 on world capsule Terminal.
        private const int WorldPickUpAction = 146;

        private const int CapsuleIdentityBase = unchecked((int)0x57AC323C);

        private static long Key(Identity identity)
        {
            return ((long)(int)identity.Type << 32) | (uint)identity.Instance;
        }

        internal static int ExpectedCapsuleInstance(int playfieldInstance)
        {
            int terminalInstance = CapsuleIdentityBase ^ (playfieldInstance & 0x0FFF);
            return terminalInstance == 0 ? CapsuleIdentityBase : terminalInstance;
        }

        private static bool IsExpectedCapsuleTerminal(Identity target, ICharacter character)
        {
            if (target == null || target.Type != IdentityType.Terminal || character == null
                || character.Playfield == null)
            {
                return false;
            }

            if (!CharacterHasActiveReturnMission(character))
            {
                return false;
            }

            return target.Instance == ExpectedCapsuleInstance(character.Playfield.Identity.Instance);
        }

        /// <summary>
        /// Exit-door handler must not treat the return capsule Terminal as a near-spawn exit click.
        /// </summary>
        public static bool IsExpectedCapsuleForExitGuard(Identity target, ICharacter character)
        {
            return IsExpectedCapsuleTerminal(target, character);
        }

        public static bool CharacterHasActiveReturnMission(ICharacter character)
        {
            if (character == null)
            {
                return false;
            }

            List<MissionAcceptedStore.AcceptedMission> all =
                MissionAcceptedStore.GetAll(character.Identity.Instance);
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (IsFindItemReturnMission(all[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool CharacterAlreadyHoldingReturnItem(ICharacter character)
        {
            return CharacterHasObjectiveItem(character);
        }

        /// <summary>
        /// Capture 20260728-095215: send Encrypted Info Capsule as world Terminal SIFU near spawn.
        /// </summary>
        public static bool TrySendWorldCapsule(
            ZoneClient zoneClient,
            ICharacter character,
            float x,
            float y,
            float z)
        {
            if (zoneClient == null || character == null || character.Playfield == null)
            {
                return false;
            }

            // Stable per-playfield identity so zone-in retries refresh the same dynel.
            int terminalInstance = ExpectedCapsuleInstance(character.Playfield.Identity.Instance);

            var terminal = new Identity
                           {
                               Type = IdentityType.Terminal,
                               Instance = terminalInstance
                           };

            int templateId = MissionInstanceDynelCapture.FindItemReturnCapsuleTemplateId;
            uint flags = 0x80000003;
            var message = new SimpleItemFullUpdateMessage
                          {
                              Identity = terminal,
                              Unknown = 0,
                              MsgVersion = 0x0B,
                              Identitytype = 0,
                              Instance = 0,
                              Coordinate = new Vector3(x, y, z),
                              Heading = new Quaternion { X = 0f, Y = 0f, Z = 0f, W = 1f },
                              Playfield = character.Playfield.Identity.Instance,
                              Unknown1 = new Identity { Type = (IdentityType)1000015, Instance = 0 },
                              Unknown2 = 0,
                              Unknown3 = 0x6F,
                              Stats =
                                  new[]
                                  {
                                      new GameTuple<CharacterStat, uint>
                                      {
                                          Value1 = CharacterStat.Flags,
                                          Value2 = flags
                                      },
                                      new GameTuple<CharacterStat, uint>
                                      {
                                          Value1 = CharacterStat.StaticInstance,
                                          Value2 = (uint)templateId
                                      },
                                      new GameTuple<CharacterStat, uint>
                                      {
                                          Value1 = CharacterStat.ACGItemLevel,
                                          Value2 = 1
                                      },
                                      new GameTuple<CharacterStat, uint>
                                      {
                                          Value1 = CharacterStat.ACGItemTemplateID,
                                          Value2 = (uint)templateId
                                      },
                                      new GameTuple<CharacterStat, uint>
                                      {
                                          Value1 = CharacterStat.ACGItemTemplateID2,
                                          Value2 = (uint)templateId
                                      },
                                      new GameTuple<CharacterStat, uint>
                                      {
                                          Value1 = CharacterStat.MultipleCount,
                                          Value2 = 1
                                      }
                                  },
                              Name = "Encrypted Info Capsule\0"
                          };

            try
            {
                zoneClient.SendCompressed(message);
            }
            catch
            {
                return false;
            }

            RegisterWorldPickup(terminal);
            MissionDiagnostics.Log(
                "FINDITEM-RETURN-SIFU char={0} pf={1} terminal={2} xyz=({3:0.#},{4:0.#},{5:0.#})",
                character.Identity.Instance,
                character.Playfield.Identity.Instance,
                terminal,
                x,
                y,
                z);
            return true;
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

        public static void RegisterWorldPickup(Identity terminalIdentity)
        {
            if (terminalIdentity == null || (int)terminalIdentity.Type == 0 || terminalIdentity.Instance == 0)
            {
                return;
            }

            lock (Sync)
            {
                WorldPickups.Add(Key(terminalIdentity));
            }
        }

        public static bool IsMissionCube(Identity npcIdentity)
        {
            lock (Sync)
            {
                return Cubes.Contains(Key(npcIdentity));
            }
        }

        public static bool IsWorldPickup(Identity identity)
        {
            if (identity == null)
            {
                return false;
            }

            lock (Sync)
            {
                return WorldPickups.Contains(Key(identity));
            }
        }

        public static void UnregisterCube(Identity npcIdentity)
        {
            lock (Sync)
            {
                Cubes.Remove(Key(npcIdentity));
            }
        }

        public static void UnregisterWorldPickup(Identity identity)
        {
            if (identity == null)
            {
                return;
            }

            lock (Sync)
            {
                WorldPickups.Remove(Key(identity));
            }
        }

        public static bool IsFindItemOffer(QuestInfo offer)
        {
            return offer != null
                   && (offer.MissionIconId == MissionTypeCatalog.FindItemIcon
                       || offer.MissionIconId == MissionTypeCatalog.ReturnItemIcon);
        }

        public static bool IsFindItemReturnOffer(QuestInfo offer)
        {
            return offer != null && offer.MissionIconId == MissionTypeCatalog.ReturnItemIcon;
        }

        public static bool IsFindItemKeepMission(MissionAcceptedStore.AcceptedMission entry)
        {
            return entry != null && entry.MissionIconId == MissionTypeCatalog.FindItemIcon;
        }

        public static bool IsFindItemReturnMission(MissionAcceptedStore.AcceptedMission entry)
        {
            return entry != null && entry.MissionIconId == MissionTypeCatalog.ReturnItemIcon;
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
                   || (lowId == b.LowId && highId == b.HighId)
                   || (lowId == MissionInstanceDynelCapture.FindItemReturnCapsuleTemplateId
                       && highId == MissionInstanceDynelCapture.FindItemReturnCapsuleTemplateId);
        }

        /// <summary>
        /// Capture 20260728-095215: CharacterAction 146 / GenericCmd Get/Use on Encrypted Info Capsule Terminal.
        /// Double-click and right-click PickUp both land here.
        /// </summary>
        public static bool TryHandleWorldPickUp(IZoneClient client, Identity target)
        {
            if (client == null || target == null)
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
            if (entry == null || !IsFindItemReturnMission(entry))
            {
                return false;
            }

            // Prefer registered capsule; also accept the stable per-PF capsule identity
            // (zone-in retry can refresh SIFU before RegisterWorldPickup is observed).
            bool registered = IsWorldPickup(target);
            int expectedInstance = ExpectedCapsuleInstance(character.Playfield.Identity.Instance);
            bool expectedTerminal = target.Type == IdentityType.Terminal
                                    && target.Instance == expectedInstance;
            if (!registered && !expectedTerminal)
            {
                return false;
            }

            // Already holding the capsule — despawn world prop only (no duplicate grants).
            if (CharacterHasObjectiveItem(character))
            {
                UnregisterWorldPickup(target);
                try
                {
                    client.SendCompressed(
                        new DespawnMessage
                        {
                            Identity = target,
                            Unknown = 1
                        });
                }
                catch
                {
                }

                lock (Sync)
                {
                    ReturnReadyCharacters.Add(character.Identity.Instance);
                }

                MissionDiagnostics.Log(
                    "FINDITEM-RETURN-ALREADY-HELD char={0} terminal={1}",
                    character.Identity.Instance,
                    target);
                return true;
            }

            MissionInstanceLootCatalog.LootDrop drop = MissionInstanceLootCatalog.FindItemB;
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
                client.Server.Info(client, "Mission FindItemReturn pickup grant failed: {0}", error);
                return true;
            }

            UnregisterWorldPickup(target);
            try
            {
                client.SendCompressed(
                    new DespawnMessage
                    {
                        Identity = target,
                        Unknown = 1
                    });
            }
            catch
            {
            }

            return FinishAfterItemTaken(client, character, entry, "FindItemReturnPickUp");
        }

        public static bool TryHandleWorldPickUpAction(IZoneClient client, CharacterActionMessage message)
        {
            if (message == null || (int)message.Action != WorldPickUpAction)
            {
                return false;
            }

            return TryHandleWorldPickUp(client, message.Target);
        }

        /// <summary>
        /// GenericCmd Use on Mission Cube or return-capsule Terminal (double-click / right-click Use).
        /// </summary>
        public static bool TryHandleCubeUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (client == null || target == null)
            {
                return false;
            }

            if (IsWorldPickup(target)
                || IsExpectedCapsuleTerminal(target, client.Controller != null ? client.Controller.Character : null))
            {
                bool picked = TryHandleWorldPickUp(client, target);
                if (picked)
                {
                    TryAcknowledge(client, message);
                }

                return picked;
            }

            if (!IsMissionCube(target))
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
                IsFindItemReturnMission(entry)
                    ? MissionInstanceLootCatalog.FindItemB
                    : MissionInstanceLootCatalog.ResolveFindItemDrop(target.Instance);
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
            bool finished = FinishAfterItemTaken(client, character, entry, "FindItemCube");
            if (finished)
            {
                TryAcknowledge(client, message);
            }

            return finished;
        }

        private static void TryAcknowledge(IZoneClient client, GenericCmdMessage message)
        {
            if (client == null || message == null || client.Controller == null
                || client.Controller.Character == null)
            {
                return;
            }

            try
            {
                ZoneEngine.Core.MessageHandlers.GenericCmdMessageHandler.Default.Acknowledge(
                    client.Controller.Character,
                    message);
            }
            catch
            {
            }
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
        /// Capture 20260728-095215: consume capsule → complete (Action 59, Quest Delete, key delete, reward).
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

            IItem clicked = ResolveInventoryItem(character, message.Target[0]);
            if (clicked != null && !IsObjectiveItem(clicked))
            {
                // Clicked some other inventory item on the mission terminal.
                return false;
            }

            if (!CharacterHasObjectiveItem(character))
            {
                client.Server.Info(client, "Mission FindItemReturn: need the Encrypted Info Capsule on the terminal");
                return true;
            }

            int removed = TryConsumeAllObjectiveItems(client, character);
            if (removed <= 0)
            {
                client.Server.Info(client, "Mission FindItemReturn: failed to remove mission item");
                return true;
            }

            lock (Sync)
            {
                ReturnReadyCharacters.Remove(character.Identity.Instance);
            }

            ClearWorldPickupsForCharacter(character.Identity.Instance);

            try
            {
                ZoneEngine.Core.MessageHandlers.GenericCmdMessageHandler.Default.Acknowledge(
                    character,
                    message);
            }
            catch
            {
            }

            bool completed = MissionCompleteService.TryComplete(client, character, entry, "FindItemReturn");
            MissionDiagnostics.Log(
                "FINDITEM-RETURN-FINISH char={0} removed={1} completed={2} mission={3:X8}",
                character.Identity.Instance,
                removed,
                completed,
                entry.QuestIdentity != null ? entry.QuestIdentity.Instance : 0);
            return true;
        }

        public static void ClearCharacter(int characterInstance)
        {
            lock (Sync)
            {
                ReturnReadyCharacters.Remove(characterInstance);
            }

            ClearWorldPickupsForCharacter(characterInstance);
        }

        private static void ClearWorldPickupsForCharacter(int characterInstance)
        {
            // World pickups are Terminal identities (not char-keyed). Clear all registered capsules
            // for this finish — single-player mission instances only carry one capsule.
            lock (Sync)
            {
                WorldPickups.Clear();
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

        private static IItem ResolveInventoryItem(ICharacter character, Identity itemIdentity)
        {
            if (character == null || character.BaseInventory == null || itemIdentity == null)
            {
                return null;
            }

            IInventoryPage sourcePage;
            if (character.BaseInventory.Pages.TryGetValue((int)itemIdentity.Type, out sourcePage)
                && sourcePage != null)
            {
                return sourcePage[itemIdentity.Instance];
            }

            // Legacy Pool page layout used by repair UseItemOnItem.
            sourcePage = Pool.Instance.GetObject<IInventoryPage>(
                new Identity
                {
                    Type = (IdentityType)character.Identity.Instance,
                    Instance = (int)itemIdentity.Type
                });
            if (sourcePage != null)
            {
                return sourcePage[itemIdentity.Instance];
            }

            return null;
        }

        private static int TryConsumeAllObjectiveItems(IZoneClient client, ICharacter character)
        {
            if (client == null || character == null || character.BaseInventory == null)
            {
                return 0;
            }

            int removed = 0;
            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages.ToList())
            {
                IInventoryPage page = pageEntry.Value;
                if (page == null)
                {
                    continue;
                }

                foreach (KeyValuePair<int, IItem> itemEntry in page.List().ToList())
                {
                    IItem candidate = itemEntry.Value;
                    if (candidate == null || !IsObjectiveItem(candidate))
                    {
                        continue;
                    }

                    try
                    {
                        page.Remove(itemEntry.Key);
                        character.BaseInventory.Write();
                    }
                    catch
                    {
                        continue;
                    }

                    try
                    {
                        ZoneEngine.Core.MessageHandlers.CharacterActionMessageHandler.Default.SendDeleteItem(
                            character,
                            pageEntry.Key,
                            itemEntry.Key);
                    }
                    catch
                    {
                    }

                    if (candidate.Identity != null)
                    {
                        try
                        {
                            client.SendCompressed(
                                new DespawnMessage
                                {
                                    Identity = candidate.Identity,
                                    Unknown = 1
                                });
                        }
                        catch
                        {
                        }
                    }

                    removed++;
                }
            }

            return removed;
        }
    }
}
