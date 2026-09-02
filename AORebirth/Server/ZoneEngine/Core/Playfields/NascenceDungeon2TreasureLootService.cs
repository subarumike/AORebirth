namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;
    using ZoneEngine.Core;
    using ZoneEngine.Core.MessageHandlers;

    /// <summary>
    /// Capture 20260823-171238: Treasure (C749) Use opens container UI + loot — not credits+despawn.
    /// Inventory observations: Container bag + handle 114+, flags 0x21, GenericCmd ACK Temp1=1.
    /// </summary>
    internal static class NascenceDungeon2TreasureLootService
    {
        // Capture 20260825-094236 InventoryUpdate on C749: NumberOfSlots=0x15, Unknown1=2.
        private const int ContainerSlotCount = 21;

        private const int TreasureEntryFlags = 0x21;

        private const int InventoryUpdateUnknown1 = 2;

        private static readonly TimeSpan RespawnDelay = NascenceDungeon2Rules.TreasureRespawnDelay;

        private static readonly object Sync = new object();

        private static readonly Dictionary<long, TreasureChestState> Chests = new Dictionary<long, TreasureChestState>();

        private static readonly List<PendingTreasureRespawn> PendingRespawns = new List<PendingTreasureRespawn>();

        private static readonly Dictionary<long, int> LootGenerationByChest = new Dictionary<long, int>();

        // Keep above corpse handle wrap range (0x70..0xFF) so ContainerAddItem Backpack
        // packed handles cannot match a living corpse inventory handle.
        private static int nextInventoryHandle = 0x17F;

        internal static void ProcessDue(Playfield playfield, DateTime utcNow)
        {
            if (playfield == null
                || !NascenceDungeon2Rules.IsDungeonPlayfield(playfield.Identity.Instance))
            {
                return;
            }

            List<PendingTreasureRespawn> due = null;
            lock (Sync)
            {
                for (int i = PendingRespawns.Count - 1; i >= 0; i--)
                {
                    PendingTreasureRespawn pending = PendingRespawns[i];
                    if (pending.DueUtc > utcNow)
                    {
                        continue;
                    }

                    if (due == null)
                    {
                        due = new List<PendingTreasureRespawn>();
                    }

                    due.Add(pending);
                    PendingRespawns.RemoveAt(i);
                }
            }

            if (due == null)
            {
                return;
            }

            for (int i = 0; i < due.Count; i++)
            {
                PendingTreasureRespawn pending = due[i];
                Register(pending.ContainerIdentity);
                NascenceDungeon2DoorReplay.RespawnTreasureChestInZone(
                    playfield,
                    pending.ZoneKey,
                    pending.ContainerIdentity.Instance);

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "NascenceDungeon2 treasure respawn chest={0:X8} zone={1:X16} pf={2}",
                        pending.ContainerIdentity.Instance,
                        pending.ZoneKey,
                        playfield.Identity.Instance));
            }
        }

        internal static void Register(Identity containerIdentity)
        {
            if (containerIdentity == null
                || containerIdentity.Instance == 0)
            {
                return;
            }

            if (containerIdentity.Type != IdentityType.Container)
            {
                containerIdentity = new Identity
                {
                    Type = IdentityType.Container,
                    Instance = containerIdentity.Instance
                };
            }

            long key = Key(containerIdentity);
            lock (Sync)
            {
                if (Chests.ContainsKey(key))
                {
                    return;
                }

                Chests[key] = new TreasureChestState
                {
                    ContainerIdentity = containerIdentity,
                    InventoryHandle = AllocateInventoryHandle(),
                    Items = BuildLoot(containerIdentity.Instance)
                };
            }
        }

        internal static bool IsTreasureChest(Identity identity)
        {
            if (identity == null || identity.Type != IdentityType.Container)
            {
                return false;
            }

            lock (Sync)
            {
                return Chests.ContainsKey(Key(identity));
            }
        }

        internal static bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (client == null || message == null || target == null)
            {
                return false;
            }

            TryRegisterIfKnownChest(target);

            if (!IsTreasureChest(target))
            {
                return false;
            }

            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null
                || character.Playfield == null
                || !NascenceDungeon2Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            TreasureChestState chest;
            lock (Sync)
            {
                if (!Chests.TryGetValue(Key(target), out chest))
                {
                    return false;
                }
            }

            // Capture 20260825-094236:
            //   open  = InventoryUpdate (slots=21, Unknown1=2) then GenericCmd ACK — no Action 0x64
            //   close = Action 0x66 then ACK on a later Use
            // Sending backpack SendOpen (0x64) + Unknown1=3 made the UI flash open then shut.
            if (chest.Opened && chest.OpenedByCharacterInstance == character.Identity.Instance)
            {
                BackpackContainerActionMessageHandler.Default.SendClose(character, chest.ContainerIdentity);
                GenericCmdMessageHandler.Default.Acknowledge(character, message);
                chest.Opened = false;
                chest.OpenedByCharacterInstance = 0;
                return true;
            }

            SendInventoryUpdate(character, chest);
            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            chest.Opened = true;
            chest.OpenedByCharacterInstance = character.Identity.Instance;

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon2 treasure open chest={0:X8} handle={1} items={2} char={3}",
                    chest.ContainerIdentity.Instance,
                    chest.InventoryHandle,
                    chest.Items.Count(x => !x.Looted),
                    character.Identity.Instance));
            return true;
        }

        internal static bool TryLootItem(
            IZoneClient client,
            Identity sourceContainer,
            Identity target,
            int targetPlacement)
        {
            if (client == null || client.Controller == null || sourceContainer == null)
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (character == null
                || character.Playfield == null
                || !NascenceDungeon2Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            TreasureChestState chest;
            int lootSlot;
            TryRegisterIfKnownChest(sourceContainer);
            if (!TryResolveLootRequest(sourceContainer, out chest, out lootSlot))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "NascenceDungeon2 treasure loot unresolved source={0} type={1} instance={2:X8}",
                        sourceContainer,
                        sourceContainer.Type,
                        sourceContainer.Instance));
                return false;
            }

            if (target != null && target != Identity.None && target != character.Identity)
            {
                return true;
            }

            TreasureLootSlot slot;
            lock (Sync)
            {
                slot = chest.Items.FirstOrDefault(x => x.Slot == lootSlot && !x.Looted);
                if (slot == null)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "NascenceDungeon2 treasure loot miss-slot chest={0:X8} handle={1} slot={2} source={3}",
                            chest.ContainerIdentity.Instance,
                            chest.InventoryHandle,
                            lootSlot,
                            sourceContainer));
                    SendUseActionFinished(character);
                    return true;
                }
            }

            Item item;
            if (!TryCreateLootItem(slot, out item))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "NascenceDungeon2 treasure loot bad-template chest={0:X8} low={1} high={2} ql={3}",
                        chest.ContainerIdentity.Instance,
                        slot.LowId,
                        slot.HighId,
                        slot.Quality));
                ChatTextMessageHandler.Default.Send(
                    character,
                    "Treasure item template is missing on the server.");
                SendUseActionFinished(character);
                return true;
            }

            CorpseLootInventoryTransferResult transfer =
                InventoryContainerRuntimeService.Default.TryAddCorpseLootItem(character, item, targetPlacement);
            if (transfer.Status != CorpseLootInventoryTransferStatus.Success)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "NascenceDungeon2 treasure loot transfer-fail chest={0:X8} status={1} invErr={2} ex={3}",
                        chest.ContainerIdentity.Instance,
                        transfer.Status,
                        transfer.InventoryError,
                        transfer.ExceptionMessage ?? string.Empty));
                SendUseActionFinished(character);
                return true;
            }

            slot.Looted = true;
            // Live ACK uses the client Backpack(handle<<16|slot) source + inventory target slot.
            ContainerAddItemMessageHandler.Default.Send(
                character,
                sourceContainer,
                transfer.TargetSlot);
            ChatTextMessageHandler.Default.Send(
                character,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "You looted {0}.",
                    ResolveLootItemDisplayName(item)));

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon2 treasure loot ok chest={0:X8} low={1} high={2} invSlot={3}",
                    chest.ContainerIdentity.Instance,
                    item.LowID,
                    item.HighID,
                    transfer.TargetSlot));

            SendInventoryUpdate(character, chest);

            if (chest.Items.All(x => x.Looted))
            {
                Playfield playfield = character.Playfield as Playfield;
                if (playfield != null)
                {
                    playfield.Despawn(chest.ContainerIdentity);
                }

                long chestKey = Key(chest.ContainerIdentity);
                long zoneKey = NascenceDungeon2RevealZones.ZoneKeyForContainer(chest.ContainerIdentity.Instance);
                lock (Sync)
                {
                    Chests.Remove(chestKey);
                    PendingRespawns.Add(
                        new PendingTreasureRespawn
                        {
                            ContainerIdentity = chest.ContainerIdentity,
                            ZoneKey = zoneKey,
                            DueUtc = DateTime.UtcNow.Add(RespawnDelay)
                        });
                }
            }

            SendUseActionFinished(character);
            return true;
        }

        private static string ResolveLootItemDisplayName(Item item)
        {
            if (item == null)
            {
                return "an item";
            }

            DBItemName itemName = ItemNamesDao.Instance.Get(item.LowID);
            if (itemName != null && !string.IsNullOrWhiteSpace(itemName.Name))
            {
                return itemName.Name;
            }

            itemName = ItemNamesDao.Instance.Get(item.HighID);
            if (itemName != null && !string.IsNullOrWhiteSpace(itemName.Name))
            {
                return itemName.Name;
            }

            if (item.LowID == 220519 || item.HighID == 220519)
            {
                return "Cracked Crystal (Shock Absorber)";
            }

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "item {0}",
                item.LowID);
        }

        private static bool TryCreateLootItem(TreasureLootSlot slot, out Item item)
        {
            item = null;
            if (slot == null || ItemLoader.ItemList == null)
            {
                return false;
            }

            // D1-stable path: always instantiate lowId==highId. Dual SL templates
            // (e.g. 218744/218745) blow up Item.GetAttribute Convert.ToInt32 during AddToPage
            // ("Value was either too large or too small for an Int32").
            int templateId = 0;
            if (ItemLoader.ItemList.ContainsKey(slot.LowId))
            {
                templateId = slot.LowId;
            }
            else if (ItemLoader.ItemList.ContainsKey(slot.HighId))
            {
                templateId = slot.HighId;
            }
            else if (ItemLoader.ItemList.ContainsKey(21605))
            {
                templateId = 21605;
            }
            else
            {
                return false;
            }

            int count = slot.Count > 0 ? slot.Count : 1;
            int quality = slot.Quality > 0 ? slot.Quality : 1;
            try
            {
                ItemTemplate template = ItemLoader.ItemList[templateId];
                if (template != null && template.Quality > 0)
                {
                    quality = template.Quality;
                }

                item = new Item(quality, templateId, templateId)
                {
                    MultipleCount = count
                };
                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NascenceDungeon2 treasure Item create failed id="
                    + templateId
                    + " err="
                    + ex.Message);
                item = null;
                return false;
            }
        }

        private static bool TryRegisterIfKnownChest(Identity target)
        {
            if (target == null || target.Instance == 0)
            {
                return false;
            }

            if (!NascenceDungeon2RevealZones.IsKnownChestInstance(target.Instance))
            {
                return false;
            }

            Register(
                new Identity
                {
                    Type = IdentityType.Container,
                    Instance = target.Instance
                });
            return true;
        }

        internal static bool TryRegisterChestOnUse(Identity target)
        {
            return TryRegisterIfKnownChest(target);
        }

        private static bool TryResolveLootRequest(
            Identity sourceContainer,
            out TreasureChestState chest,
            out int lootSlot)
        {
            chest = null;
            lootSlot = 0;
            if (sourceContainer == null)
            {
                return false;
            }

            if (sourceContainer.Type == IdentityType.Container)
            {
                lock (Sync)
                {
                    if (!Chests.TryGetValue(Key(sourceContainer), out chest))
                    {
                        return false;
                    }

                    TreasureLootSlot first = chest.Items.FirstOrDefault(x => !x.Looted);
                    if (first == null)
                    {
                        return false;
                    }

                    lootSlot = first.Slot;
                    return true;
                }
            }

            if (sourceContainer.Type == IdentityType.Backpack)
            {
                int handle = (int)(((uint)sourceContainer.Instance >> 16) & 0xffff);
                lootSlot = (int)((uint)sourceContainer.Instance & 0xffff);
                lock (Sync)
                {
                    chest = Chests.Values.FirstOrDefault(x => x.InventoryHandle == handle);
                    return chest != null;
                }
            }

            return false;
        }

        private static void SendInventoryUpdate(ICharacter character, TreasureChestState chest)
        {
            if (character == null || chest == null)
            {
                return;
            }

            InventoryEntry[] entries = chest.Items
                .Where(x => !x.Looted)
                .Select(
                    x => new InventoryEntry
                    {
                        Slotnumber = x.Slot,
                        UnknownFlags = TreasureEntryFlags,
                        Unknown1 = (short)(x.Count > 0 ? x.Count : 1),
                        Identity = Identity.None,
                        LowId = x.LowId,
                        HighId = x.HighId,
                        Quality = x.Quality,
                        Unknown2 = 0
                    })
                .ToArray();

            character.Send(
                new InventoryUpdateMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    NumberOfSlots = ContainerSlotCount,
                    Unknown1 = InventoryUpdateUnknown1,
                    Entries = entries,
                    BagIdentity = chest.ContainerIdentity,
                    SlotnumberInMainInventory = chest.InventoryHandle,
                    Unknown2 = 1
                });
        }

        private static void SendUseActionFinished(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            character.Send(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.UseActionFinished,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = 0,
                    Unknown2 = 0
                });
        }

        private static int AllocateInventoryHandle()
        {
            lock (Sync)
            {
                return ++nextInventoryHandle;
            }
        }

        private static long Key(Identity identity)
        {
            return ((long)(int)identity.Type << 32) | (uint)identity.Instance;
        }

        private static List<TreasureLootSlot> BuildLoot(int containerInstance)
        {
            long chestKey = ((long)(int)IdentityType.Container << 32) | (uint)containerInstance;
            int generation;
            lock (Sync)
            {
                if (!LootGenerationByChest.TryGetValue(chestKey, out generation))
                {
                    generation = 0;
                }

                LootGenerationByChest[chestKey] = generation + 1;
            }

            List<TreasureLootDefinition> defs;
            if (generation == 0
                && CapturedLootByContainer.TryGetValue(containerInstance, out defs))
            {
                // First open uses capture-backed loot for that static chest.
            }
            else
            {
                int poolIndex = Math.Abs(containerInstance + generation) % FallbackLootPools.Length;
                defs = FallbackLootPools[poolIndex];
            }

            var items = new List<TreasureLootSlot>(defs.Count);
            for (int i = 0; i < defs.Count; i++)
            {
                TreasureLootDefinition def = defs[i];
                items.Add(
                    new TreasureLootSlot
                    {
                        Slot = i,
                        LowId = def.LowId,
                        HighId = def.HighId,
                        Quality = def.Quality,
                        Count = def.Count
                    });
            }

            return items;
        }

        // Capture 20260825-094236 InventoryUpdate on C749:0BAE12xx (+ legacy 0BAC58xx rows).
        private static readonly Dictionary<int, List<TreasureLootDefinition>> CapturedLootByContainer =
            new Dictionary<int, List<TreasureLootDefinition>>
            {
                {
                    unchecked((int)0x0BAC58FF),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 21605, HighId = 21605, Quality = 1, Count = 100 }
                    }
                },
                {
                    unchecked((int)0x0BAC5905),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 218744, HighId = 218745, Quality = 22, Count = 25 }
                    }
                },
                {
                    unchecked((int)0x0BAC590F),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 21601, HighId = 21601, Quality = 1, Count = 100 },
                        new TreasureLootDefinition { LowId = 218824, HighId = 218825, Quality = 22, Count = 100 }
                    }
                },
                {
                    unchecked((int)0x0BAE1217),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 126757, HighId = 126757, Quality = 1, Count = 100 },
                        new TreasureLootDefinition { LowId = 218780, HighId = 218781, Quality = 22, Count = 25 }
                    }
                },
                {
                    unchecked((int)0x0BAE121B),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 136636, HighId = 136637, Quality = 25, Count = 1 },
                        new TreasureLootDefinition { LowId = 218824, HighId = 218825, Quality = 22, Count = 100 },
                        new TreasureLootDefinition { LowId = 300437, HighId = 300438, Quality = 23, Count = 1 }
                    }
                },
                {
                    unchecked((int)0x0BAE121D),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 21613, HighId = 21613, Quality = 1, Count = 100 },
                        new TreasureLootDefinition { LowId = 218353, HighId = 218354, Quality = 22, Count = 100 }
                    }
                },
                {
                    unchecked((int)0x0BAE121F),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 218744, HighId = 218745, Quality = 22, Count = 25 }
                    }
                },
                {
                    unchecked((int)0x0BAE1221),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 235383, HighId = 235384, Quality = 25, Count = 1 }
                    }
                },
                {
                    unchecked((int)0x0BAE1225),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 218780, HighId = 218781, Quality = 22, Count = 25 },
                        new TreasureLootDefinition { LowId = 218744, HighId = 218745, Quality = 27, Count = 25 }
                    }
                },
                {
                    unchecked((int)0x0BAE1229),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 218744, HighId = 218745, Quality = 22, Count = 25 },
                        new TreasureLootDefinition { LowId = 232828, HighId = 232829, Quality = 27, Count = 1 }
                    }
                },
                {
                    unchecked((int)0x0BAE122B),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 218824, HighId = 218825, Quality = 22, Count = 100 }
                    }
                },
                {
                    unchecked((int)0x0BAE1231),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 218744, HighId = 218745, Quality = 22, Count = 25 }
                    }
                },
                {
                    unchecked((int)0x0BAE1233),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 218744, HighId = 218745, Quality = 22, Count = 25 }
                    }
                },
                {
                    unchecked((int)0x0BAE1235),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 218744, HighId = 218745, Quality = 22, Count = 25 },
                        new TreasureLootDefinition { LowId = 221204, HighId = 221204, Quality = 20, Count = 1 }
                    }
                },
                {
                    unchecked((int)0x0BAE1239),
                    new List<TreasureLootDefinition>
                    {
                        new TreasureLootDefinition { LowId = 218744, HighId = 218745, Quality = 25, Count = 25 }
                    }
                }
            };

        private static readonly List<TreasureLootDefinition>[] FallbackLootPools =
        {
            new List<TreasureLootDefinition>
            {
                new TreasureLootDefinition { LowId = 21605, HighId = 21605, Quality = 1, Count = 100 }
            },
            new List<TreasureLootDefinition>
            {
                new TreasureLootDefinition { LowId = 218744, HighId = 218745, Quality = 22, Count = 25 }
            },
            new List<TreasureLootDefinition>
            {
                new TreasureLootDefinition { LowId = 83920, HighId = 83919, Quality = 4, Count = 1 }
            },
            new List<TreasureLootDefinition>
            {
                new TreasureLootDefinition { LowId = 21601, HighId = 21601, Quality = 1, Count = 100 },
                new TreasureLootDefinition { LowId = 218824, HighId = 218825, Quality = 22, Count = 50 }
            }
        };

        private sealed class TreasureChestState
        {
            internal Identity ContainerIdentity;

            internal int InventoryHandle;

            internal List<TreasureLootSlot> Items;

            internal bool Opened;

            internal int OpenedByCharacterInstance;
        }

        private sealed class PendingTreasureRespawn
        {
            internal Identity ContainerIdentity;

            internal long ZoneKey;

            internal DateTime DueUtc;
        }

        private sealed class TreasureLootSlot
        {
            internal int Slot;

            internal int LowId;

            internal int HighId;

            internal int Quality;

            internal int Count;

            internal bool Looted;
        }

        private struct TreasureLootDefinition
        {
            internal int LowId;

            internal int HighId;

            internal int Quality;

            internal int Count;
        }
    }
}
