namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;
    using ZoneEngine.Core;
    using ZoneEngine.Core.MessageHandlers;

    /// <summary>
    /// Capture 20260823-171238: Treasure (C749) Use opens container UI + loot — not credits+despawn.
    /// Inventory observations: Container bag + handle 114+, flags 0x21, GenericCmd ACK Temp1=1.
    /// </summary>
    internal static class NascenceDungeon1TreasureLootService
    {
        private const int ContainerSlotCount = 50;

        private const int TreasureEntryFlags = 0x21;

        private static readonly TimeSpan RespawnDelay = NascenceDungeon1Rules.TreasureRespawnDelay;

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
                || !NascenceDungeon1Rules.IsDungeonPlayfield(playfield.Identity.Instance))
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
                NascenceDungeon1DoorReplay.RespawnTreasureChestInZone(
                    playfield,
                    pending.ZoneKey,
                    pending.ContainerIdentity.Instance);

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "NascenceDungeon1 treasure respawn chest={0:X8} zone={1:X16} pf={2}",
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
                || !NascenceDungeon1Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
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

            if (chest.Opened && chest.OpenedByCharacterInstance == character.Identity.Instance)
            {
                GenericCmdMessageHandler.Default.Acknowledge(character, message);
                BackpackContainerActionMessageHandler.Default.SendClose(character, chest.ContainerIdentity);
                SendUseActionFinished(character);
                chest.Opened = false;
                chest.OpenedByCharacterInstance = 0;
                return true;
            }

            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            BackpackContainerActionMessageHandler.Default.SendOpen(character, chest.ContainerIdentity);
            SendInventoryUpdate(character, chest);
            chest.Opened = true;
            chest.OpenedByCharacterInstance = character.Identity.Instance;

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon1 treasure open chest={0:X8} handle={1} items={2} char={3}",
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
                || !NascenceDungeon1Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            TreasureChestState chest;
            int lootSlot;
            TryRegisterIfKnownChest(sourceContainer);
            if (!TryResolveLootRequest(sourceContainer, out chest, out lootSlot))
            {
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
                    SendUseActionFinished(character);
                    return true;
                }
            }

            Item item;
            try
            {
                // Match D1-stable mono-template instantiate (low==high). Dual SL AO templates
                // overflow Item.GetAttribute Convert.ToInt32 inside AddToPage.
                int templateId = 0;
                if (ItemLoader.ItemList != null && ItemLoader.ItemList.ContainsKey(slot.LowId))
                {
                    templateId = slot.LowId;
                }
                else if (ItemLoader.ItemList != null && ItemLoader.ItemList.ContainsKey(slot.HighId))
                {
                    templateId = slot.HighId;
                }
                else if (ItemLoader.ItemList != null && ItemLoader.ItemList.ContainsKey(21605))
                {
                    templateId = 21605;
                }
                else
                {
                    SendUseActionFinished(character);
                    return true;
                }

                int quality = slot.Quality > 0 ? slot.Quality : 1;
                ItemTemplate template = ItemLoader.ItemList[templateId];
                if (template != null && template.Quality > 0)
                {
                    quality = template.Quality;
                }

                item = new Item(quality, templateId, templateId)
                {
                    MultipleCount = slot.Count > 0 ? slot.Count : 1
                };
            }
            catch (Exception)
            {
                SendUseActionFinished(character);
                return true;
            }

            CorpseLootInventoryTransferResult transfer =
                InventoryContainerRuntimeService.Default.TryAddCorpseLootItem(character, item, targetPlacement);
            if (transfer.Status != CorpseLootInventoryTransferStatus.Success)
            {
                SendUseActionFinished(character);
                return true;
            }

            slot.Looted = true;
            ContainerAddItemMessageHandler.Default.Send(
                character,
                sourceContainer,
                transfer.TargetSlot);

            SendInventoryUpdate(character, chest);

            if (chest.Items.All(x => x.Looted))
            {
                Playfield playfield = character.Playfield as Playfield;
                if (playfield != null)
                {
                    playfield.Despawn(chest.ContainerIdentity);
                }

                long chestKey = Key(chest.ContainerIdentity);
                long zoneKey = NascenceDungeon1RevealZones.ZoneKeyForContainer(chest.ContainerIdentity.Instance);
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

        private static bool TryRegisterIfKnownChest(Identity target)
        {
            if (target == null || target.Instance == 0)
            {
                return false;
            }

            if (!NascenceDungeon1RevealZones.IsKnownChestInstance(target.Instance))
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
                int handle = (sourceContainer.Instance >> 16) & 0xffff;
                lootSlot = sourceContainer.Instance & 0xffff;
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
                    Unknown1 = 3,
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

        // Capture 20260823-171238 inventory observations on live dyn ACG PF.
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
