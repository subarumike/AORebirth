#region License

// Copyright (c) 2005-2014, CellAO Team
// All rights reserved.

#endregion

namespace ZoneEngine.Core.GMI
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Entities;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;
    using AORebirth.Stats;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Mail;
    using ZoneEngine.Core.MessageHandlers;

    /// <summary>
    /// In-memory GMI personal vault (credits + items). Deposit only from capture 20260715-GMI.
    /// Persists to MySQL (gmi_vault / gmi_vault_item). Pending withdraw/purchase mail stays JSON files.
    /// </summary>
    internal static class GmiRuntimeService
    {
        /// <summary>
        /// Reject NoDrop / Unique / backpack-container deposits (mail-style FormatFeedback popup).
        /// </summary>
        public const string FailureForbiddenItem =
            "Cannot Sell Container Backpack Nodrop Unique items";

        private static readonly ConcurrentDictionary<string, GmiVault> ByCharacter =
            new ConcurrentDictionary<string, GmiVault>(StringComparer.OrdinalIgnoreCase);

        private const string LocalWebVaultDataDir = @"C:\xampp\htdocs\market\data";

        private const string LocalWebPendingDir = @"C:\xampp\htdocs\market\data\pending";

        private static readonly object PendingLock = new object();

        /// <summary>.NET Encoding.UTF8 writes a BOM that breaks PHP json_decode.</summary>
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public sealed class GmiVaultItem
        {
            public int LowId { get; set; }

            public int HighId { get; set; }

            public int Quality { get; set; }

            public int Count { get; set; }

            public string Name { get; set; }

            public int Icon { get; set; }
        }

        public sealed class GmiVault
        {
            public long Credits { get; set; }

            public List<GmiVaultItem> Items { get; private set; }

            public GmiVault()
            {
                this.Items = new List<GmiVaultItem>();
            }
        }

        public static GmiVault GetOrCreate(ICharacter character)
        {
            string key = character != null ? (character.Name ?? string.Empty) : string.Empty;
            return ByCharacter.GetOrAdd(key, _ => CreateVaultFromDisk(character));
        }

        /// <summary>Legacy name-only path — prefer GetOrCreate(ICharacter).</summary>
        public static GmiVault GetOrCreate(string characterName)
        {
            return ByCharacter.GetOrAdd(
                characterName ?? string.Empty,
                name => CreateVaultFromDisk(name, 0));
        }

        private static GmiVault CreateVaultFromDisk(ICharacter character)
        {
            string name = character != null ? character.Name : null;
            int instance = character != null ? character.Identity.Instance : 0;
            return CreateVaultFromDisk(name, instance);
        }

        private static GmiVault CreateVaultFromDisk(string characterName, int characterInstance)
        {
            var vault = new GmiVault();
            if (characterInstance != 0 && GmiVaultDao.CanUseVaultSchema())
            {
                ApplyDbSnapshot(GmiVaultDao.Load(characterInstance), vault);
            }
            else
            {
                TryLoadVaultMirror(characterName, characterInstance, vault);
            }

            EnrichVaultItems(vault);
            return vault;
        }

        private static void ApplyDbSnapshot(GmiVaultDao.VaultSnapshot snap, GmiVault vault)
        {
            if (snap == null || vault == null)
            {
                return;
            }

            vault.Credits = snap.Credits;
            vault.Items.Clear();
            if (snap.Items == null)
            {
                return;
            }

            for (int i = 0; i < snap.Items.Count; i++)
            {
                GmiVaultDao.VaultItemRow row = snap.Items[i];
                if (row == null)
                {
                    continue;
                }

                vault.Items.Add(
                    new GmiVaultItem
                        {
                            LowId = row.LowId,
                            HighId = row.HighId,
                            Quality = row.Quality,
                            Count = row.StackCount,
                            Icon = row.Icon,
                            Name = row.ItemName ?? string.Empty
                        });
            }
        }

        private static void ReloadVaultFromDatabase(ICharacter character, GmiVault vault)
        {
            if (character == null || vault == null || character.Identity.Instance == 0)
            {
                return;
            }

            if (!GmiVaultDao.CanUseVaultSchema())
            {
                return;
            }

            ApplyDbSnapshot(GmiVaultDao.Load(character.Identity.Instance), vault);
            EnrichVaultItems(vault);
        }

        public static bool TryDepositCredits(ICharacter character, int credits, out string failureReason)
        {
            failureReason = null;
            if (character == null)
            {
                failureReason = "No character.";
                return false;
            }

            if (credits <= 0)
            {
                failureReason = "Credits must be positive.";
                return false;
            }

            if (!GmiVaultDao.CanUseVaultSchema())
            {
                failureReason = "Market vault unavailable.";
                return false;
            }

            int cash = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
            if (cash < credits)
            {
                failureReason = "Not enough credits.";
                return false;
            }

            int cashAfter = CashStatRules.Clamp((long)cash - credits);
            character.Stats[StatIds.cash].Set((uint)cashAfter);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)cashAfter);

            character.Stats[StatIds.socialstatus].Set(4);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.socialstatus, 4);

            GmiVault vault = GetOrCreate(character);
            ReloadVaultFromDatabase(character, vault);
            vault.Credits += credits;
            PersistVaultMirror(character, vault);
            return true;
        }

        /// <summary>
        /// Capture MarketSend item deposit: credits=0, clientItemId, containerType (Inventory=0x68), placement.
        /// Live follows with DeleteItem(Inventory, placement). CellAO template IDs can disagree with the
        /// client-sent id (seen: client 20182018 vs server 125754 in that slot) — trust placement first.
        /// </summary>
        public static bool TryDepositItem(
            ICharacter character,
            int clientItemId,
            int containerType,
            int placement,
            out string failureReason)
        {
            failureReason = null;
            if (character == null)
            {
                failureReason = "No character.";
                return false;
            }

            if (placement < 0)
            {
                failureReason = "Invalid item deposit.";
                return false;
            }

            if (!GmiVaultDao.CanUseVaultSchema())
            {
                failureReason = "Market vault unavailable.";
                return false;
            }

            // Capture order is (lowId, containerType, placement). Some deposit-UI sends look like
            // (lowId, placement, containerType) — e.g. container field = 81 (inventory slot).
            NormalizeMarketSendRefs(ref containerType, ref placement);

            IInventoryPage inventoryPage;
            int pageType = containerType;
            if (!IsGmiDepositSourcePage(pageType))
            {
                // Last resort: numeric "container" in inventory slot range → Inventory page.
                if (containerType >= 64 && containerType < 200)
                {
                    placement = containerType;
                    pageType = (int)IdentityType.Inventory;
                    containerType = pageType;
                }
                else
                {
                    failureReason = string.Format(
                        CultureInfo.InvariantCulture,
                        "GMI items must come from Inventory (got container {0}).",
                        containerType);
                    return false;
                }
            }

            if (!TryGetPage(character, pageType, out inventoryPage) || inventoryPage == null)
            {
                // Fallback to main Inventory page when client tags Armor/Weapon page oddly.
                if (!TryGetInventoryPage(character, out inventoryPage) || inventoryPage == null)
                {
                    failureReason = "No inventory.";
                    return false;
                }

                pageType = (int)IdentityType.Inventory;
            }

            int contentKey;
            IItem item;

            // Capture-backed: placement is authoritative (DeleteItem used Inventory:placement).
            if (!TryFindInventorySlot(inventoryPage, placement, out contentKey, out item) || item == null)
            {
                // Fallback: client template id may still match some slot (inventory / wear / backpack).
                if (!TryFindItemByClientId(character, clientItemId, placement, out pageType, out contentKey, out item)
                    || item == null)
                {
                    failureReason = BuildNotFoundReason(inventoryPage, clientItemId, placement);
                    return false;
                }

                if (!TryGetPage(character, pageType, out inventoryPage) || inventoryPage == null)
                {
                    failureReason = "No inventory page for deposit.";
                    return false;
                }
            }

            // GMI: YesDrop OK; block real backpacks/bags, Unique, and NoDrop.
            bool forbiddenContainer = InventoryItemRules.IsGmiForbiddenContainerItem(item);
            bool forbiddenUnique = InventoryItemRules.IsUnique(item);
            bool forbiddenNoDrop = IsGmiNoDrop(item);
            if (forbiddenContainer || forbiddenUnique || forbiddenNoDrop)
            {
                failureReason = FailureForbiddenItem;
                return false;
            }

            GmiVault vault = GetOrCreate(character);
            ReloadVaultFromDatabase(character, vault);
            if (vault.Items != null && vault.Items.Count >= 21)
            {
                failureReason = "Market inventory full (21/21).";
                return false;
            }

            int depositCount = Math.Max(1, item.MultipleCount);
            int quality = item.Quality;
            int highId = item.HighID;
            int lowId = item.LowID;
            int resolvedPlacement = contentKey >= inventoryPage.FirstSlotNumber
                                        ? contentKey
                                        : inventoryPage.FirstSlotNumber + contentKey;

            try
            {
                character.BaseInventory.RemoveItem(pageType, contentKey);
                CharacterActionMessageHandler.Default.SendDeleteItem(
                    character,
                    pageType,
                    resolvedPlacement);
            }
            catch (Exception)
            {
                failureReason = "Failed to remove item from inventory.";
                return false;
            }

            character.BaseInventory.Write();

            character.Stats[StatIds.socialstatus].Set(4);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.socialstatus, 4);

            var vaultItem = new GmiVaultItem
                {
                    LowId = lowId,
                    HighId = highId,
                    Quality = quality,
                    Count = depositCount
                };
            ApplyItemMeta(vaultItem, item);
            vault.Items.Add(vaultItem);
            PersistVaultMirror(character, vault);
            return true;
        }

        /// <summary>
        /// Capture 20260715-143838: live withdraw is web-side; deliveries arrive as Omni-Trade mail.
        /// Process PHP pending withdraw requests → deduct vault → mail attachment.
        /// </summary>
        public static int ProcessPendingWithdrawals(ICharacter character)
        {
            if (character == null || string.IsNullOrEmpty(character.Name))
            {
                return 0;
            }

            if (!GmiVaultDao.CanUseVaultSchema())
            {
                return 0;
            }

            // Ensure vault is loaded from MySQL before applying withdraws.
            GmiVault vault = GetOrCreate(character);
            ReloadVaultFromDatabase(character, vault);
            int processed = 0;

            lock (PendingLock)
            {
                if (!Directory.Exists(LocalWebPendingDir))
                {
                    return 0;
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(LocalWebPendingDir, "*.json");
                }
                catch
                {
                    return 0;
                }

                Array.Sort(files);
                string safeName = SanitizeFileToken(character.Name);
                int instance = character.Identity.Instance;

                for (int i = 0; i < files.Length; i++)
                {
                    string path = files[i];
                    string json;
                    try
                    {
                        json = File.ReadAllText(path, Encoding.UTF8);
                    }
                    catch
                    {
                        continue;
                    }

                    string fileChar = ReadJsonStringLoose(json, "character");
                    string fileId = ReadJsonStringLoose(json, "characterId");
                    bool forThis = (!string.IsNullOrEmpty(fileChar)
                                    && string.Equals(
                                        SanitizeFileToken(fileChar),
                                        safeName,
                                        StringComparison.OrdinalIgnoreCase))
                                   || CharacterIdMatches(fileId, instance);
                    if (!forThis)
                    {
                        continue;
                    }

                    string kind = ReadJsonStringLoose(json, "kind") ?? string.Empty;
                    string failure = null;
                    bool ok = false;
                    bool preDebited = ReadJsonIntLoose(json, "preDebited") != 0;
                    if (string.Equals(kind, "credits", StringComparison.OrdinalIgnoreCase))
                    {
                        int amount = ReadJsonIntLoose(json, "amount");
                        if (preDebited)
                        {
                            ok = TryMailWithdrawCreditsOnly(character, amount, out failure);
                        }
                        else
                        {
                            ok = TryWithdrawCredits(character, amount, out failure);
                        }
                    }
                    else if (string.Equals(kind, "item", StringComparison.OrdinalIgnoreCase))
                    {
                        int index = ReadJsonIntLoose(json, "index");
                        int count = ReadJsonIntLoose(json, "count");
                        if (count <= 0)
                        {
                            count = 1;
                        }

                        if (preDebited)
                        {
                            ok = TryMailWithdrawItemOnly(character, json, count, out failure);
                        }
                        else
                        {
                            ok = TryWithdrawItem(character, index, count, out failure);
                        }
                    }
                    else if (string.Equals(kind, "purchase_item", StringComparison.OrdinalIgnoreCase))
                    {
                        // Capture mail datetime: Market purchase successful → item via mail, not vault.
                        int count = ReadJsonIntLoose(json, "count");
                        if (count <= 0)
                        {
                            count = 1;
                        }

                        ok = TryMailPurchaseItemOnly(character, json, count, out failure);
                    }
                    else
                    {
                        failure = "Unknown withdraw kind.";
                    }

                    if (ok)
                    {
                        processed++;
                        try
                        {
                            File.Delete(path);
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        try
                        {
                            string failPath = path + ".failed";
                            if (File.Exists(failPath))
                            {
                                File.Delete(failPath);
                            }

                            File.Move(path, failPath);
                            File.WriteAllText(failPath + ".txt", failure ?? "withdraw failed", Encoding.UTF8);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return processed;
        }

        public static bool TryWithdrawCredits(ICharacter character, int credits, out string failureReason)
        {
            failureReason = null;
            if (character == null)
            {
                failureReason = "No character.";
                return false;
            }

            if (credits <= 0)
            {
                failureReason = "Credits must be positive.";
                return false;
            }

            if (!GmiVaultDao.CanUseVaultSchema())
            {
                failureReason = "Market vault unavailable.";
                return false;
            }

            GmiVault vault = GetOrCreate(character);
            ReloadVaultFromDatabase(character, vault);
            if (vault.Credits < credits)
            {
                failureReason = "Not enough market credits.";
                return false;
            }

            string subject = "Credit withdrawal";
            string body = string.Format(
                CultureInfo.InvariantCulture,
                "You withdrew {0} credits from the Omni-Trade GMI. "
                + "Deliveries use the mail system and expire within 48 hours.",
                credits);
            if (!MailRuntimeService.TryEnqueueGmiDelivery(
                    character.Name,
                    credits,
                    0,
                    0,
                    0,
                    0,
                    subject,
                    body,
                    out failureReason))
            {
                return false;
            }

            vault.Credits -= credits;
            PersistVaultMirror(character, vault);
            return true;
        }

        public static bool TryWithdrawItem(
            ICharacter character,
            int itemIndex,
            int count,
            out string failureReason)
        {
            failureReason = null;
            if (character == null)
            {
                failureReason = "No character.";
                return false;
            }

            if (!GmiVaultDao.CanUseVaultSchema())
            {
                failureReason = "Market vault unavailable.";
                return false;
            }

            GmiVault vault = GetOrCreate(character);
            ReloadVaultFromDatabase(character, vault);
            if (itemIndex < 0 || itemIndex >= vault.Items.Count)
            {
                failureReason = "Market inventory slot is empty.";
                return false;
            }

            GmiVaultItem item = vault.Items[itemIndex];
            if (item == null || item.Count <= 0)
            {
                failureReason = "Market inventory slot is empty.";
                return false;
            }

            int sendCount = count <= 0 ? item.Count : Math.Min(count, item.Count);
            string itemName = string.IsNullOrEmpty(item.Name)
                                  ? ("Item " + item.LowId.ToString(CultureInfo.InvariantCulture))
                                  : item.Name;
            string subject = "Item transfer";
            string body = string.Format(
                CultureInfo.InvariantCulture,
                "You withdrew {0} x {1} (QL {2}) from the Omni-Trade GMI. "
                + "Deliveries use the mail system and expire within 48 hours.",
                sendCount,
                itemName,
                item.Quality);

            if (!MailRuntimeService.TryEnqueueGmiDelivery(
                    character.Name,
                    0,
                    item.LowId,
                    item.HighId,
                    item.Quality,
                    sendCount,
                    subject,
                    body,
                    out failureReason))
            {
                return false;
            }

            if (sendCount >= item.Count)
            {
                vault.Items.RemoveAt(itemIndex);
            }
            else
            {
                item.Count -= sendCount;
            }

            PersistVaultMirror(character, vault);
            return true;
        }

        /// <summary>
        /// Web already debited vault JSON (preDebited=1). Only enqueue Omni-Trade mail.
        /// </summary>
        public static bool TryMailWithdrawCreditsOnly(ICharacter character, int credits, out string failureReason)
        {
            failureReason = null;
            if (character == null)
            {
                failureReason = "No character.";
                return false;
            }

            if (credits <= 0)
            {
                failureReason = "Credits must be positive.";
                return false;
            }

            // Reload disk vault so in-memory matches PHP debit (do not deduct again).
            ReloadVaultFromDisk(character);

            string subject = "Credit withdrawal";
            string body = string.Format(
                CultureInfo.InvariantCulture,
                "You withdrew {0} credits from the Omni-Trade GMI. "
                + "Deliveries use the mail system and expire within 48 hours.",
                credits);
            return MailRuntimeService.TryEnqueueGmiDelivery(
                character.Name,
                credits,
                0,
                0,
                0,
                0,
                subject,
                body,
                out failureReason);
        }

        public static bool TryMailWithdrawItemOnly(
            ICharacter character,
            string pendingJson,
            int count,
            out string failureReason)
        {
            failureReason = null;
            if (character == null)
            {
                failureReason = "No character.";
                return false;
            }

            int lowId = ReadJsonIntLoose(pendingJson, "lowId");
            int highId = ReadJsonIntLoose(pendingJson, "highId");
            int quality = ReadJsonIntLoose(pendingJson, "quality");
            string itemName = ReadJsonStringLoose(pendingJson, "name");
            if (lowId <= 0 && highId <= 0)
            {
                failureReason = "Pending item withdraw missing template ids.";
                return false;
            }

            if (highId <= 0)
            {
                highId = lowId;
            }

            if (lowId <= 0)
            {
                lowId = highId;
            }

            if (quality <= 0)
            {
                quality = 1;
            }

            int sendCount = count <= 0 ? 1 : count;
            if (string.IsNullOrEmpty(itemName))
            {
                itemName = "Item " + lowId.ToString(CultureInfo.InvariantCulture);
            }

            ReloadVaultFromDisk(character);

            string subject = "Item transfer";
            string body = string.Format(
                CultureInfo.InvariantCulture,
                "You withdrew {0} x {1} (QL {2}) from the Omni-Trade GMI. "
                + "Deliveries use the mail system and expire within 48 hours.",
                sendCount,
                itemName,
                quality);
            return MailRuntimeService.TryEnqueueGmiDelivery(
                character.Name,
                0,
                lowId,
                highId,
                quality,
                sendCount,
                subject,
                body,
                out failureReason);
        }

        /// <summary>
        /// Capture Market purchase successful: item arrives as mail from Market (not GMI vault).
        /// </summary>
        public static bool TryMailPurchaseItemOnly(
            ICharacter character,
            string pendingJson,
            int count,
            out string failureReason)
        {
            failureReason = null;
            if (character == null)
            {
                failureReason = "No character.";
                return false;
            }

            int lowId = ReadJsonIntLoose(pendingJson, "lowId");
            int highId = ReadJsonIntLoose(pendingJson, "highId");
            int quality = ReadJsonIntLoose(pendingJson, "quality");
            string itemName = ReadJsonStringLoose(pendingJson, "name");
            if (lowId <= 0 && highId <= 0)
            {
                failureReason = "Pending purchase missing template ids.";
                return false;
            }

            if (highId <= 0)
            {
                highId = lowId;
            }

            if (lowId <= 0)
            {
                lowId = highId;
            }

            if (quality <= 0)
            {
                quality = 1;
            }

            int sendCount = count <= 0 ? 1 : count;
            if (string.IsNullOrEmpty(itemName))
            {
                itemName = "Item " + lowId.ToString(CultureInfo.InvariantCulture);
            }

            string subject = "Market purchase successful";
            string body = string.Format(
                CultureInfo.InvariantCulture,
                "Item purchased from market. {0} x {1} (QL {2}).",
                sendCount,
                itemName,
                quality);
            return MailRuntimeService.TryEnqueueGmiDelivery(
                character.Name,
                0,
                lowId,
                highId,
                quality,
                sendCount,
                subject,
                body,
                "Market",
                out failureReason);
        }

        private static void ReloadVaultFromDisk(ICharacter character)
        {
            if (character == null || string.IsNullOrEmpty(character.Name))
            {
                return;
            }

            GmiVault fresh = CreateVaultFromDisk(character.Name, character.Identity.Instance);
            ByCharacter[character.Name] = fresh;
        }

        private static int ReadJsonIntLoose(string json, string key)
        {
            return ReadJsonInt(json, key);
        }

        private static string ReadJsonStringLoose(string json, string key)
        {
            return ReadJsonString(json, key);
        }

        private static string BuildNotFoundReason(IInventoryPage page, int clientItemId, int placement)
        {
            var sb = new StringBuilder(128);
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "item id {0} / slot {1} not found.",
                clientItemId,
                placement);

            int hintKey;
            IItem hintItem;
            if (page != null && TryFindInventorySlot(page, placement, out hintKey, out hintItem) && hintItem != null)
            {
                sb.AppendFormat(
                    CultureInfo.InvariantCulture,
                    " Slot has {0}/{1} QL{2} x{3}.",
                    hintItem.LowID,
                    hintItem.HighID,
                    hintItem.Quality,
                    hintItem.MultipleCount);
                return sb.ToString();
            }

            if (page != null)
            {
                int n = 0;
                sb.Append(" Inventory LowIDs:");
                foreach (KeyValuePair<int, IItem> entry in page.List())
                {
                    if (entry.Value == null)
                    {
                        continue;
                    }

                    sb.AppendFormat(
                        CultureInfo.InvariantCulture,
                        " [{0}]={1}x{2}",
                        entry.Key,
                        entry.Value.LowID,
                        entry.Value.MultipleCount);
                    n++;
                    if (n >= 8)
                    {
                        sb.Append("…");
                        break;
                    }
                }

                if (n == 0)
                {
                    sb.Append(" (empty)");
                }
            }

            return sb.ToString();
        }

        private static bool IsGmiDepositSourcePage(int containerType)
        {
            return containerType == (int)IdentityType.Inventory
                   || containerType == (int)IdentityType.WeaponPage
                   || containerType == (int)IdentityType.ArmorPage
                   || containerType == (int)IdentityType.ImplantPage
                   || containerType == (int)IdentityType.SocialPage
                   || containerType == (int)IdentityType.OverflowWindow;
        }

        /// <summary>
        /// Capture uses (containerType, placement). Deposit UI sometimes sends (placement, containerType)
        /// so the "container" looks like an inventory slot (e.g. 81) and placement looks like 0x68.
        /// </summary>
        private static void NormalizeMarketSendRefs(ref int containerType, ref int placement)
        {
            bool containerIsPage = IsGmiDepositSourcePage(containerType);
            bool placementIsPage = IsGmiDepositSourcePage(placement);
            if (!containerIsPage && placementIsPage)
            {
                int swap = containerType;
                containerType = placement;
                placement = swap;
            }
        }

        private static bool IsGmiNoDrop(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            ItemTemplate template;
            if (ItemLoader.ItemList.TryGetValue(item.LowID, out template) && template != null && template.IsNoDrop())
            {
                return true;
            }

            if (item.HighID != item.LowID
                && ItemLoader.ItemList.TryGetValue(item.HighID, out template)
                && template != null
                && template.IsNoDrop())
            {
                return true;
            }

            if ((item.Flags & (int)ItemFlags.NoDrop) != 0)
            {
                return true;
            }

            return false;
        }

        private static void PersistVaultMirror(ICharacter character, GmiVault vault)
        {
            if (character == null || vault == null)
            {
                return;
            }

            try
            {
                EnrichVaultItems(vault);
                int characterId = character.Identity.Instance;
                if (characterId == 0)
                {
                    return;
                }

                var rows = new List<GmiVaultDao.VaultItemRow>();
                if (vault.Items != null)
                {
                    for (int i = 0; i < vault.Items.Count; i++)
                    {
                        GmiVaultItem item = vault.Items[i];
                        if (item == null)
                        {
                            continue;
                        }

                        rows.Add(
                            new GmiVaultDao.VaultItemRow
                                {
                                    LowId = item.LowId,
                                    HighId = item.HighId,
                                    Quality = item.Quality,
                                    StackCount = item.Count,
                                    Icon = item.Icon,
                                    ItemName = item.Name ?? string.Empty,
                                    SlotIndex = (short)i
                                });
                    }
                }

                GmiVaultDao.Save(characterId, character.Name ?? string.Empty, vault.Credits, rows);
            }
            catch
            {
            }
        }

        private static void TryWriteVaultFiles(string characterName, int characterInstance, GmiVault vault)
        {
            // Legacy JSON mirror — unused after MySQL cutover; kept for one-shot migration fallback tools.
            if (vault == null)
            {
                return;
            }

            try
            {
                if (!Directory.Exists(LocalWebVaultDataDir))
                {
                    Directory.CreateDirectory(LocalWebVaultDataDir);
                }

                string idHex = characterInstance.ToString("X", CultureInfo.InvariantCulture);
                string json = BuildVaultJson(characterName, idHex, vault);
                var paths = new List<string>();
                if (!string.IsNullOrEmpty(characterName))
                {
                    paths.Add(
                        Path.Combine(
                            LocalWebVaultDataDir,
                            "name_" + SanitizeFileToken(characterName) + ".json"));
                }

                if (characterInstance != 0)
                {
                    foreach (string token in ExpandCharacterIdTokens(characterInstance))
                    {
                        paths.Add(Path.Combine(LocalWebVaultDataDir, "char_" + token + ".json"));
                    }
                }

                for (int i = 0; i < paths.Count; i++)
                {
                    File.WriteAllText(paths[i], json, Utf8NoBom);
                }
            }
            catch
            {
            }
        }

        private static void TryLoadVaultMirror(string characterName, int characterInstance, GmiVault vault)
        {
            if (vault == null)
            {
                return;
            }

            try
            {
                if (!Directory.Exists(LocalWebVaultDataDir))
                {
                    return;
                }

                var paths = new List<string>();
                if (characterInstance != 0)
                {
                    foreach (string token in ExpandCharacterIdTokens(characterInstance))
                    {
                        paths.Add(Path.Combine(LocalWebVaultDataDir, "char_" + token + ".json"));
                    }
                }

                if (!string.IsNullOrEmpty(characterName))
                {
                    paths.Add(
                        Path.Combine(
                            LocalWebVaultDataDir,
                            "name_" + SanitizeFileToken(characterName) + ".json"));
                }

                // Prefer newest file among aliases (avoids stale char_decimal vs char_hex mismatch).
                string bestPath = null;
                DateTime bestWrite = DateTime.MinValue;
                for (int i = 0; i < paths.Count; i++)
                {
                    if (!File.Exists(paths[i]))
                    {
                        continue;
                    }

                    DateTime write = File.GetLastWriteTimeUtc(paths[i]);
                    if (bestPath == null || write >= bestWrite)
                    {
                        bestWrite = write;
                        bestPath = paths[i];
                    }
                }

                if (bestPath == null)
                {
                    return;
                }

                string json = File.ReadAllText(bestPath, Encoding.UTF8);
                TryParseVaultJson(json, vault);
            }
            catch
            {
            }
        }

        /// <summary>
        /// AO client X-Anarchy-CharacterID may be decimal (18) while Zone historically wrote hex (12).
        /// Always mirror both so GMI web Refresh finds the same vault.
        /// </summary>
        private static IEnumerable<string> ExpandCharacterIdTokens(int characterInstance)
        {
            string hex = characterInstance.ToString("X", CultureInfo.InvariantCulture);
            string dec = characterInstance.ToString(CultureInfo.InvariantCulture);
            yield return hex;
            if (!string.Equals(hex, dec, StringComparison.OrdinalIgnoreCase))
            {
                yield return dec;
            }

            string hexLower = hex.ToLowerInvariant();
            if (!string.Equals(hexLower, hex, StringComparison.Ordinal))
            {
                yield return hexLower;
            }
        }

        private static bool CharacterIdMatches(string fileId, int characterInstance)
        {
            if (string.IsNullOrEmpty(fileId))
            {
                return false;
            }

            foreach (string token in ExpandCharacterIdTokens(characterInstance))
            {
                if (string.Equals(fileId, token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseVaultJson(string json, GmiVault vault)
        {
            if (string.IsNullOrEmpty(json) || vault == null)
            {
                return false;
            }

            try
            {
                System.Text.RegularExpressions.Match creditsMatch =
                    System.Text.RegularExpressions.Regex.Match(
                        json,
                        "\"credits\"\\s*:\\s*(-?\\d+)");
                if (creditsMatch.Success)
                {
                    long credits;
                    if (long.TryParse(
                        creditsMatch.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out credits))
                    {
                        vault.Credits = credits;
                    }
                }

                vault.Items.Clear();
                System.Text.RegularExpressions.MatchCollection itemMatches =
                    System.Text.RegularExpressions.Regex.Matches(
                        json,
                        "\\{([^{}]+)\\}");
                for (int i = 0; i < itemMatches.Count; i++)
                {
                    string body = itemMatches[i].Groups[1].Value;
                    if (body.IndexOf("lowId", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    var item = new GmiVaultItem();
                    item.LowId = ReadJsonInt(body, "lowId");
                    item.HighId = ReadJsonInt(body, "highId");
                    item.Quality = ReadJsonInt(body, "quality");
                    item.Count = ReadJsonInt(body, "count");
                    item.Icon = ReadJsonInt(body, "icon");
                    item.Name = ReadJsonString(body, "name");
                    if (item.LowId > 0 || item.HighId > 0)
                    {
                        vault.Items.Add(item);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int ReadJsonInt(string body, string key)
        {
            System.Text.RegularExpressions.Match m =
                System.Text.RegularExpressions.Regex.Match(
                    body,
                    "\"" + key + "\"\\s*:\\s*(-?\\d+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            int value;
            if (m.Success
                && int.TryParse(
                    m.Groups[1].Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return value;
            }

            return 0;
        }

        private static string ReadJsonString(string body, string key)
        {
            System.Text.RegularExpressions.Match m =
                System.Text.RegularExpressions.Regex.Match(
                    body,
                    "\"" + key + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                return null;
            }

            return m.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        private static void EnrichVaultItems(GmiVault vault)
        {
            if (vault == null || vault.Items == null)
            {
                return;
            }

            for (int i = 0; i < vault.Items.Count; i++)
            {
                ApplyItemMeta(vault.Items[i], null);
            }
        }

        private static void ApplyItemMeta(GmiVaultItem vaultItem, IItem liveItem)
        {
            if (vaultItem == null)
            {
                return;
            }

            if (liveItem != null)
            {
                try
                {
                    int icon = liveItem.GetAttribute((int)StatIds.icon);
                    if (icon > 0)
                    {
                        vaultItem.Icon = icon;
                    }
                }
                catch
                {
                }
            }

            try
            {
                AORebirth.Database.Entities.DBItemName named =
                    AORebirth.Database.Dao.ItemNamesDao.Instance.Get(vaultItem.LowId);
                if (named == null && vaultItem.HighId != vaultItem.LowId)
                {
                    named = AORebirth.Database.Dao.ItemNamesDao.Instance.Get(vaultItem.HighId);
                }

                if (named != null)
                {
                    if (string.IsNullOrEmpty(vaultItem.Name) && !string.IsNullOrEmpty(named.Name))
                    {
                        vaultItem.Name = named.Name;
                    }

                    int iconFromDb;
                    if (vaultItem.Icon <= 0 && !string.IsNullOrEmpty(named.Icon)
                        && int.TryParse(named.Icon, NumberStyles.Integer, CultureInfo.InvariantCulture, out iconFromDb)
                        && iconFromDb > 0)
                    {
                        vaultItem.Icon = iconFromDb;
                    }
                }
            }
            catch
            {
            }

            if (vaultItem.Icon <= 0)
            {
                try
                {
                    if (ItemLoader.ItemList.ContainsKey(vaultItem.LowId))
                    {
                        ItemTemplate template = ItemLoader.ItemList[vaultItem.LowId];
                        if (template.Stats != null && template.Stats.ContainsKey((int)StatIds.icon))
                        {
                            vaultItem.Icon = template.Stats[(int)StatIds.icon];
                        }
                    }
                }
                catch
                {
                }
            }

            if (string.IsNullOrEmpty(vaultItem.Name))
            {
                vaultItem.Name = "Item " + vaultItem.LowId.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string BuildVaultJson(string characterName, string characterIdHex, GmiVault vault)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"character\":\"");
            sb.Append(EscapeJson(characterName ?? string.Empty));
            sb.Append("\",\"characterId\":\"");
            sb.Append(EscapeJson(characterIdHex ?? string.Empty));
            sb.Append("\",\"credits\":");
            sb.Append(vault.Credits.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"items\":[");
            for (int i = 0; i < vault.Items.Count; i++)
            {
                GmiVaultItem item = vault.Items[i];
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append("{\"lowId\":");
                sb.Append(item.LowId.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"highId\":");
                sb.Append(item.HighId.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"quality\":");
                sb.Append(item.Quality.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"count\":");
                sb.Append(item.Count.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"icon\":");
                sb.Append(item.Icon.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"name\":\"");
                sb.Append(EscapeJson(item.Name ?? string.Empty));
                sb.Append("\"}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static string SanitizeFileToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "unknown";
            }

            var sb = new StringBuilder(value.Length);
            foreach (char c in value.Trim().ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_')
                {
                    sb.Append(c);
                }
                else if (c == ' ')
                {
                    sb.Append('_');
                }
            }

            return sb.Length == 0 ? "unknown" : sb.ToString();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static bool TryGetPage(ICharacter character, int pageType, out IInventoryPage page)
        {
            page = null;
            if (character == null || character.BaseInventory == null || character.BaseInventory.Pages == null)
            {
                return false;
            }

            try
            {
                if (!character.BaseInventory.Pages.ContainsKey(pageType))
                {
                    return false;
                }

                page = character.BaseInventory.Pages[pageType];
                return page != null;
            }
            catch
            {
                page = null;
                return false;
            }
        }

        private static bool TryGetInventoryPage(ICharacter character, out IInventoryPage page)
        {
            return TryGetPage(character, (int)IdentityType.Inventory, out page);
        }

        /// <summary>
        /// Fallback when placement is empty: match LowID/HighID/icon, prefer count==placement hint.
        /// Searches Inventory then backpack pages.
        /// </summary>
        private static bool TryFindItemByClientId(
            ICharacter character,
            int clientItemId,
            int placementHint,
            out int pageType,
            out int contentKey,
            out IItem item)
        {
            pageType = (int)IdentityType.Inventory;
            contentKey = -1;
            item = null;
            if (character == null || character.BaseInventory == null || clientItemId <= 0)
            {
                return false;
            }

            var pageTypes = new List<int>();
            pageTypes.Add((int)IdentityType.Inventory);
            pageTypes.Add((int)IdentityType.WeaponPage);
            pageTypes.Add((int)IdentityType.ArmorPage);
            pageTypes.Add((int)IdentityType.ImplantPage);
            pageTypes.Add((int)IdentityType.SocialPage);
            foreach (KeyValuePair<int, IInventoryPage> kv in character.BaseInventory.Pages)
            {
                if (kv.Value is BackPackInventoryPage && !pageTypes.Contains(kv.Key))
                {
                    pageTypes.Add(kv.Key);
                }
            }

            for (int p = 0; p < pageTypes.Count; p++)
            {
                IInventoryPage page;
                if (!TryGetPage(character, pageTypes[p], out page) || page == null)
                {
                    continue;
                }

                foreach (KeyValuePair<int, IItem> entry in page.List())
                {
                    IItem candidate = entry.Value;
                    if (!ItemMatchesClientId(candidate, clientItemId))
                    {
                        continue;
                    }

                    if (candidate.MultipleCount == placementHint)
                    {
                        pageType = pageTypes[p];
                        contentKey = entry.Key;
                        item = candidate;
                        return true;
                    }
                }

                foreach (KeyValuePair<int, IItem> entry in page.List())
                {
                    IItem candidate = entry.Value;
                    if (!ItemMatchesClientId(candidate, clientItemId))
                    {
                        continue;
                    }

                    pageType = pageTypes[p];
                    contentKey = entry.Key;
                    item = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool ItemMatchesClientId(IItem candidate, int clientItemId)
        {
            if (candidate == null || clientItemId <= 0)
            {
                return false;
            }

            if (candidate.LowID == clientItemId || candidate.HighID == clientItemId)
            {
                return true;
            }

            try
            {
                // Icon id sometimes equals what the market UI sends when LowID differs.
                if (candidate.GetAttribute((int)StatIds.icon) == clientItemId)
                {
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        /// <summary>
        /// Same slot resolution as Mail attach: absolute Inventory slot, or relative key.
        /// </summary>
        private static bool TryFindInventorySlot(
            IInventoryPage page,
            int placement,
            out int contentKey,
            out IItem item)
        {
            contentKey = placement;
            item = null;
            if (page == null)
            {
                return false;
            }

            if (page.ValidSlot(placement) && page[placement] != null)
            {
                contentKey = placement;
                item = page[placement];
                return true;
            }

            if (placement >= 0 && placement < page.MaxSlots)
            {
                int absolute = page.FirstSlotNumber + placement;
                if (page.ValidSlot(absolute) && page[absolute] != null)
                {
                    contentKey = absolute;
                    item = page[absolute];
                    return true;
                }

                if (page[placement] != null)
                {
                    contentKey = placement;
                    item = page[placement];
                    return true;
                }
            }

            if (placement >= page.FirstSlotNumber
                && placement < page.FirstSlotNumber + page.MaxSlots)
            {
                int relative = placement - page.FirstSlotNumber;
                if (page[relative] != null)
                {
                    contentKey = relative;
                    item = page[relative];
                    return true;
                }
            }

            return false;
        }
    }
}
