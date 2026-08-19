#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Web.Script.Serialization;

    using AORebirth.Communication.Messages;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;

    /// <summary>
    /// Capture 20260806-063619 daily claim wire (Overflow + SpecialUsed + PrivateMsg).
    /// Item/amount come from pending web claim (dayN-itemId-amount.png via rewards.json).
    /// Never grants a hardcoded fallback item ID.
    /// </summary>
    internal static class DailyLoginRewardRuntime
    {
        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 111;

        private const int CapturedSpecialUsedParameter1 = 140;

        private const int CapturedSpecialUsedParameter2 = 60;

        private const int PrivateMsgUnk1 = 3;

        private const int PrivateMsgUnk2 = 0;

        private const int TotalDays = 28;

        private const string EmptyPendingMessage = "You currently have no pending reward items.";

        private static readonly object SyncRoot = new object();

        private static readonly Random Random = new Random();

        /// <summary>Day 1: random Phasefront Phantom hoverboard (Mike 20260807).</summary>
        private static readonly int[] Day1PhantomPool =
            {
                288815, 288813, 288809, 281906, 281685, 281669, 274273, 270547, 270545,
                270543, 270541, 270539, 270432
            };

        /// <summary>Day 28: random Phasefront Wraith / Banshee (Mike 20260807).</summary>
        private static readonly int[] Day28PhasefrontPool =
            {
                288800, 288798, 284062, 277425, 272321, 270780, 270765, 270739, 270732, 270712,
                270646, 288796, 273469, 270996, 270994, 270992, 270987, 270985, 270983
            };

        private const string ClaimRootsEnvironmentVariableName = "AO_REBIRTH_DAILY_LOGIN_CLAIMS_ROOTS";
        private const string RewardsJsonEnvironmentVariableName = "AO_REBIRTH_DAILY_LOGIN_REWARDS_JSON";
        private const string ZoneStateEnvironmentVariableName = "AO_REBIRTH_ZONE_STATE_DIR";

        private static readonly string[] LegacyWindowsClaimRoots =
            {
                @"C:\xampp\htdocs\daily\data\claims",
                @"C:\xampp\htdocs\uwg.daily.icc-rk\data\claims"
            };

        private static readonly string[] LegacyWindowsRewardsJsonPaths =
            {
                @"C:\xampp\htdocs\uwg.daily.icc-rk\rewards.json",
                @"C:\xampp\htdocs\daily\rewards.json"
            };

        /// <summary>
        /// Publish this character's account daily board for the web UI (AO browser often has no CharacterID).
        /// </summary>
        internal static void PublishActiveAccountBoard(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            try
            {
                string accountKey = ResolveAccountKey(source);
                string month = UtcMonth();
                ClaimState state = LoadState(accountKey, month);
                var row = new Dictionary<string, object>
                          {
                              { "ok", true },
                              { "accountKey", accountKey ?? string.Empty },
                              { "characterId", source.Identity.Instance },
                              { "hasIdentity", true },
                              { "boardAccount", accountKey ?? string.Empty },
                              { "taken", state.Taken ?? new int[0] },
                              { "claimedCount", state.Taken != null ? state.Taken.Length : 0 },
                              { "lastClaimUtc", state.LastClaimUtc ?? string.Empty },
                              { "lastGrantedUtc", state.LastGrantedUtc ?? string.Empty },
                              { "nextDay", ResolveNextQueueDay(state) },
                              {
                                  "claimedToday",
                                  !string.IsNullOrEmpty(state.LastGrantedUtc)
                                  && string.Equals(state.LastGrantedUtc, UtcDay(), StringComparison.Ordinal)
                              },
                              { "utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) }
                          };

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(row);
                foreach (string root in GetClaimRoots())
                {
                    try
                    {
                        if (!Directory.Exists(root))
                        {
                            Directory.CreateDirectory(root);
                        }

                        File.WriteAllText(Path.Combine(root, "last-active-account.json"), json);
                    }
                    catch (Exception ex)
                    {
                        Log("publish active board failed root=" + root + " err=" + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("publish active board failed: " + ex.Message);
            }
        }

        internal static bool TryHandleClaim(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            string accountKey = ResolveAccountKey(source);
            string today = UtcDay();
            string month = today.Length >= 7 ? today.Substring(0, 7) : UtcMonth();
            RewardsConfig config = LoadRewardsConfig();
            bool freeTest = config.FreeTestMode;

            // Pending = user clicked CLAIM REWARD (web often has no CharacterID).
            PendingClaim pending = LoadPending(source.Identity.Instance, accountKey);
            if (pending == null)
            {
                SendPrivateFeedback(source, EmptyPendingMessage);
                Log("no pending claim account=" + accountKey + " character=" + source.Identity.Instance);
                return true;
            }

            ClaimState state;
            int day;
            DayReward dayReward;
            int amount;
            int itemId;
            int quality;
            lock (SyncRoot)
            {
                state = LoadState(accountKey, month);

                // Account-wide: one reward/day, first day not marked taken.png.
                if (!string.IsNullOrEmpty(state.LastGrantedUtc) && state.LastGrantedUtc == today)
                {
                    WriteClaimResult(pending, false, accountKey, source, 0, 0, 0, 1, state, "Already claimed today.");
                    SendPrivateFeedback(source, "You already claimed today's daily reward on this account.");
                    ClearPending(source.Identity.Instance, accountKey);
                    Log("already granted today account=" + accountKey + " freeTest=" + freeTest);
                    return true;
                }

                day = ResolveNextQueueDay(state);
                if (day < 1)
                {
                    WriteClaimResult(pending, false, accountKey, source, 0, 0, 0, 1, state, "All 28 rewards claimed.");
                    SendPrivateFeedback(source, "All 28 daily rewards are claimed. Board resets tomorrow.");
                    ClearPending(source.Identity.Instance, accountKey);
                    Log("month complete account=" + accountKey);
                    return true;
                }

                // Authoritative: rewards.json for the next untaken day (ignore web-selected day).
                dayReward = ResolveDayReward(config, day);
                amount = ResolveGrantAmount(day, dayReward, pending);
                if (!TryResolveGrantItemAndQuality(source, day, dayReward, pending, out itemId, out quality))
                {
                    WriteClaimResult(
                        pending,
                        false,
                        accountKey,
                        source,
                        day,
                        0,
                        0,
                        1,
                        state,
                        "Day " + day + " has no reward configured.");
                    SendPrivateFeedback(source, "Day " + day + " has no reward configured yet.");
                    ClearPending(source.Identity.Instance, accountKey);
                    Log("day " + day + " has no resolvable reward item/quality");
                    return true;
                }

                Log(
                    "claim resolve account="
                    + accountKey
                    + " day="
                    + day
                    + " item="
                    + itemId
                    + " amount="
                    + amount
                    + " ql="
                    + quality
                    + " token="
                    + (pending.ClaimToken ?? string.Empty));
            }

            lock (SyncRoot)
            {
                // Re-load inside lock before grant mutation (state may have changed).
                state = LoadState(accountKey, month);
                if (!string.IsNullOrEmpty(state.LastGrantedUtc) && state.LastGrantedUtc == today)
                {
                    WriteClaimResult(pending, false, accountKey, source, 0, 0, 0, 1, state, "Already claimed today.");
                    SendPrivateFeedback(source, "You already claimed today's daily reward on this account.");
                    ClearPending(source.Identity.Instance, accountKey);
                    return true;
                }

                if (ResolveNextQueueDay(state) != day || IsDayTaken(state, day))
                {
                    WriteClaimResult(pending, false, accountKey, source, day, 0, 0, 1, state, "Reward no longer available.");
                    SendPrivateFeedback(source, EmptyPendingMessage);
                    ClearPending(source.Identity.Instance, accountKey);
                    Log("queue changed before grant account=" + accountKey + " day=" + day);
                    return true;
                }

                if (!ItemLoader.ItemList.ContainsKey(itemId))
                {
                    SendPrivateFeedback(
                        source,
                        "Daily reward item #" + itemId + " is missing from the item database.");
                    ClearPending(source.Identity.Instance, accountKey);
                    Log("reward item missing from ItemLoader id=" + itemId);
                    return true;
                }

                // Stackables (recharger/stim/ammo): one grant with MultipleCount=amount.
                // Non-stackables: amount separate items of count 1.
                // Day 2 ammo boxes must always stack to the configured amount (50000).
                bool stackable = day == 2;
                ItemTemplate grantTemplate;
                if (!stackable
                    && ItemLoader.ItemList.TryGetValue(itemId, out grantTemplate)
                    && grantTemplate != null)
                {
                    stackable = grantTemplate.IsStackable();
                }

                int grantLoops = stackable ? 1 : Math.Max(1, amount);
                int stackCount = stackable ? Math.Max(1, amount) : 1;
                int grantedStacks = 0;
                for (int n = 0; n < grantLoops; n++)
                {
                    Item grantItem;
                    try
                    {
                        if (IsFixedPhasefrontRandomRewardDay(day))
                        {
                            grantItem = new Item(quality, itemId, itemId) { MultipleCount = stackCount };
                        }
                        // Day 10 crystals are fixed-QL templates; do not remap via Relations.
                        else if (day == 10
                            || (dayReward != null
                                && string.Equals(
                                    dayReward.QualityMode,
                                    "professionVendorNano",
                                    StringComparison.OrdinalIgnoreCase)))
                        {
                            grantItem = new Item(quality, itemId, itemId) { MultipleCount = stackCount };
                        }
                        else
                        {
                            grantItem = CreateScaledRewardItem(
                                quality,
                                itemId,
                                ResolveRandomPool(day, dayReward));
                            grantItem.MultipleCount = stackCount;
                        }
                    }
                    catch (Exception ex)
                    {
                        SendPrivateFeedback(source, EmptyPendingMessage);
                        Log("reward create failed id=" + itemId + " ql=" + quality + " err=" + ex.Message);
                        ClearPending(source.Identity.Instance, accountKey);
                        return true;
                    }

                    // Ensure stack count sticks after template interpolation.
                    grantItem.MultipleCount = stackCount;
                    Log(
                        "grant item ready idLow="
                        + grantItem.LowID
                        + " idHigh="
                        + grantItem.HighID
                        + " ql="
                        + grantItem.Quality
                        + " multipleCount="
                        + grantItem.MultipleCount
                        + " stackableRequested="
                        + stackCount);

                    QuestRewardInventoryGrantResult grant =
                        InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, grantItem);
                    if (grant == null || grant.Status != QuestRewardInventoryGrantStatus.Success)
                    {
                        if (grantedStacks == 0)
                        {
                            SendPrivateFeedback(source, EmptyPendingMessage);
                            Log(
                                "reward grant failed id="
                                + itemId
                                + " status="
                                + (grant == null ? "null" : grant.Status.ToString()));
                            ClearPending(source.Identity.Instance, accountKey);
                            return true;
                        }

                        break;
                    }

                    // Client inspect uses Low/High/QL from overflow — must match scaled item,
                    // not the high-endpoint seed alone (e.g. 293297 QL400 → looks maxed).
                    // TryGrantQuestRewardItem inserts and persists this item on the
                    // standard inventory page. Do not advertise a synthetic overflow
                    // move: that gives the client a container identity which does not
                    // match the authoritative in-memory and persisted placement.
                    TrySendInventoryUpdate(source);
                    itemId = grantItem.LowID > 0 ? grantItem.LowID : itemId;
                    quality = grantItem.Quality;
                    grantedStacks += stackCount;
                }

                if (grantedStacks <= 0)
                {
                    SendPrivateFeedback(source, EmptyPendingMessage);
                    ClearPending(source.Identity.Instance, accountKey);
                    return true;
                }

                SendSpecialUsed(source);
                AppendTestGrantLog(
                    accountKey,
                    source,
                    day,
                    itemId,
                    grantedStacks,
                    quality,
                    freeTest);

                // Always record taken day so web can show taken.png for THIS account only.
                if (state.Taken == null)
                {
                    state.Taken = new int[0];
                }

                bool hasDay = false;
                for (int i = 0; i < state.Taken.Length; i++)
                {
                    if (state.Taken[i] == day)
                    {
                        hasDay = true;
                        break;
                    }
                }

                if (!hasDay)
                {
                    int[] nextTaken = new int[state.Taken.Length + 1];
                    Array.Copy(state.Taken, nextTaken, state.Taken.Length);
                    nextTaken[state.Taken.Length] = day;
                    state.Taken = nextTaken;
                }

                state.ClaimedCount = state.Taken.Length;
                state.Month = month;
                state.LastClaimUtc = today;
                // Account-wide once/day lock (shared by all characters on the account).
                state.LastGrantedUtc = today;

                // Full 28 claimed → board resets tomorrow at 00:00 (next calendar day).
                if (state.Taken.Length >= TotalDays)
                {
                    state.CycleCompletedOn = today;
                }
                else
                {
                    state.CycleCompletedOn = string.Empty;
                }

                state.LastCharacterId = source.Identity.Instance;
                SaveState(accountKey, state);

                string itemName = ResolveItemName(config, day, itemId);
                WriteClaimResult(
                    pending,
                    true,
                    accountKey,
                    source,
                    day,
                    itemId,
                    grantedStacks,
                    quality,
                    state,
                    "You received " + itemName + " x" + grantedStacks + ".");
                ClearPending(source.Identity.Instance, accountKey);

                string feedback = "You received " + itemName + " x" + grantedStacks + ".";
                SendPrivateFeedback(source, feedback);
                Log(
                    "granted account="
                    + accountKey
                    + " character="
                    + source.Identity.Instance
                    + " day="
                    + day
                    + " item="
                    + itemId
                    + " name="
                    + itemName
                    + " amount="
                    + grantedStacks
                    + " ql="
                    + quality
                    + " freeTest="
                    + freeTest);
            }

            return true;
        }

        private static void WriteClaimResult(
            PendingClaim pending,
            bool ok,
            string accountKey,
            ICharacter source,
            int day,
            int itemId,
            int amount,
            int quality,
            ClaimState state,
            string message)
        {
            if (pending == null || string.IsNullOrEmpty(pending.ClaimToken))
            {
                return;
            }

            string token = SafeFileKey(pending.ClaimToken);
            if (string.IsNullOrEmpty(token) || token == "unknown")
            {
                return;
            }

            var row = new Dictionary<string, object>
                      {
                          { "ok", ok },
                          { "utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) },
                          { "accountKey", accountKey ?? string.Empty },
                          { "characterId", source != null ? source.Identity.Instance : 0 },
                          { "day", day },
                          { "itemId", itemId },
                          { "amount", amount },
                          { "quality", quality },
                          { "message", message ?? string.Empty },
                          {
                              "claimedToday",
                              state != null
                              && !string.IsNullOrEmpty(state.LastGrantedUtc)
                              && string.Equals(state.LastGrantedUtc, UtcDay(), StringComparison.Ordinal)
                          },
                          {
                              "lastGrantedUtc",
                              state != null ? (state.LastGrantedUtc ?? string.Empty) : string.Empty
                          },
                          {
                              "taken",
                              state != null && state.Taken != null ? state.Taken : new int[0]
                          },
                          {
                              "nextDay",
                              state != null ? ResolveNextQueueDay(state) : 0
                          },
                          {
                              "claimedCount",
                              state != null && state.Taken != null ? state.Taken.Length : 0
                          }
                      };

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(row);
            string fileName = "result-" + token + ".json";
            foreach (string root in GetClaimRoots())
            {
                try
                {
                    if (!Directory.Exists(root))
                    {
                        Directory.CreateDirectory(root);
                    }

                    File.WriteAllText(Path.Combine(root, fileName), json);
                }
                catch (Exception ex)
                {
                    Log("claim result write failed root=" + root + " err=" + ex.Message);
                }
            }
        }

        private static string ResolveItemName(RewardsConfig config, int day, int itemId)
        {
            // Random Day 1 / 28: show the actual granted item name, not the cell label.
            DayReward reward = ResolveDayReward(config, day);
            if (ResolveRandomPool(day, reward) == null
                && reward != null
                && !string.IsNullOrEmpty(reward.ItemName))
            {
                return reward.ItemName;
            }

            try
            {
                DBItemName named = ItemNamesDao.Instance.Get(itemId);
                if (named != null && !string.IsNullOrEmpty(named.Name))
                {
                    return named.Name;
                }
            }
            catch
            {
            }

            if (reward != null && !string.IsNullOrEmpty(reward.ItemName))
            {
                return reward.ItemName;
            }

            return "Item #" + itemId.ToString(CultureInfo.InvariantCulture);
        }

        private static void TrySendInventoryUpdate(ICharacter source)
        {
            try
            {
                if (source == null || source.BaseInventory == null)
                {
                    return;
                }

                IInventoryPage page = source.BaseInventory.Pages[source.BaseInventory.StandardPage];
                if (page != null)
                {
                    InventoryUpdateMessageHandler.Default.Send(source, page);
                }
            }
            catch (Exception ex)
            {
                Log("inventory update after grant failed: " + ex.Message);
            }
        }

        private static void SendSpecialUsed(ICharacter source)
        {
            source.Send(
                new CharacterActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.SpecialUsed,
                    Unknown1 = 0,
                    Target = new Identity { Type = IdentityType.None, Instance = 0 },
                    Parameter1 = CapturedSpecialUsedParameter1,
                    Parameter2 = CapturedSpecialUsedParameter2,
                    Unknown2 = 0
                });
        }

        private static void SendPrivateFeedback(ICharacter source, string text)
        {
            if (source == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            // Always show in Zone chat window (reliable even if ChatEngine PrivateMsg misses).
            try
            {
                ChatTextMessageHandler.Default.Send(source, text);
            }
            catch (Exception ex)
            {
                Log("chat text feedback failed: " + ex.Message);
            }

            try
            {
                if (Program.ISComClient == null)
                {
                    return;
                }

                string characterName =
                    ((source.FirstName ?? string.Empty) + " " + (source.LastName ?? string.Empty)).Trim();
                Program.ISComClient.TrySend(
                    new PrivateSystemMessage
                    {
                        CharacterId = source.Identity.Instance,
                        CharacterName = characterName,
                        Text = text,
                        Unk1 = PrivateMsgUnk1,
                        Unk2 = PrivateMsgUnk2
                    });
            }
            catch (Exception ex)
            {
                Log("private feedback failed: " + ex.Message);
            }
        }

        private static void AppendTestGrantLog(
            string accountKey,
            ICharacter source,
            int day,
            int itemId,
            int amount,
            int quality,
            bool freeTest)
        {
            var row = new Dictionary<string, object>
                      {
                          { "utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) },
                          { "accountKey", accountKey },
                          { "characterId", source.Identity.Instance },
                          {
                              "characterName",
                              ((source.FirstName ?? string.Empty) + " " + (source.LastName ?? string.Empty)).Trim()
                          },
                          { "day", day },
                          { "itemId", itemId },
                          { "amount", amount },
                          { "quality", quality },
                          { "freeTestMode", freeTest },
                          { "cellNaming", "day" + day + "-" + itemId + "-" + amount + ".png" }
                      };

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string line = serializer.Serialize(row);
            foreach (string root in GetClaimRoots())
            {
                try
                {
                    if (!Directory.Exists(root))
                    {
                        Directory.CreateDirectory(root);
                    }

                    File.AppendAllText(Path.Combine(root, "test-grants.jsonl"), line + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Log("test grant log failed root=" + root + " err=" + ex.Message);
                }
            }
        }

        private static PendingClaim LoadPending(int characterId, string accountKey)
        {
            // Prefer character pending, then account pending, then latest (only if same account).
            string[] names =
                {
                    "pending-" + characterId.ToString(CultureInfo.InvariantCulture) + ".json",
                    "pending-account-" + SafeFileKey(accountKey) + ".json",
                    "pending-latest.json"
                };

            for (int i = 0; i < names.Length; i++)
            {
                PendingClaim claim = TryReadPending(names[i]);
                if (claim == null || claim.ItemId <= 0 || claim.Day < 1)
                {
                    continue;
                }

                // pending-latest must belong to this account when AccountKey is present.
                if (i == 2
                    && !string.IsNullOrEmpty(claim.AccountKey)
                    && !string.IsNullOrEmpty(accountKey)
                    && !string.Equals(claim.AccountKey, accountKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return claim;
            }

            return null;
        }

        private static PendingClaim TryReadPending(string name)
        {
            foreach (string root in GetClaimRoots())
            {
                string path = Path.Combine(root, name);
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    return serializer.Deserialize<PendingClaim>(File.ReadAllText(path));
                }
                catch (Exception ex)
                {
                    Log("pending load failed path=" + path + " err=" + ex.Message);
                }
            }

            return null;
        }

        private static void ClearPending(int characterId, string accountKey)
        {
            string[] names =
                {
                    "pending-" + characterId.ToString(CultureInfo.InvariantCulture) + ".json",
                    "pending-account-" + SafeFileKey(accountKey) + ".json",
                    "pending-latest.json"
                };

            foreach (string root in GetClaimRoots())
            {
                foreach (string name in names)
                {
                    try
                    {
                        string path = Path.Combine(root, name);
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static string SafeFileKey(string accountKey)
        {
            string safe = accountKey ?? "unknown";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(c, '_');
            }

            return safe.Trim().ToLowerInvariant();
        }

        private static string[] GetClaimRoots()
        {
            var roots = new List<string>();
            AddConfiguredPaths(roots, Environment.GetEnvironmentVariable(ClaimRootsEnvironmentVariableName));

            string zoneStateRoot = Environment.GetEnvironmentVariable(ZoneStateEnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(zoneStateRoot))
            {
                AddPath(roots, Path.Combine(zoneStateRoot.Trim(), "daily-login", "claims"));
            }

            if (IsWindowsRuntime())
            {
                foreach (string root in LegacyWindowsClaimRoots)
                {
                    AddPath(roots, root);
                }
            }

            return roots.ToArray();
        }

        private static string[] GetRewardsJsonPaths()
        {
            var paths = new List<string>();
            AddConfiguredPaths(paths, Environment.GetEnvironmentVariable(RewardsJsonEnvironmentVariableName));
            AddPath(paths, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Daily", "rewards.json"));
            AddPath(paths, Path.Combine(Environment.CurrentDirectory, "Content", "Daily", "rewards.json"));

            if (IsWindowsRuntime())
            {
                foreach (string path in LegacyWindowsRewardsJsonPaths)
                {
                    AddPath(paths, path);
                }
            }

            return paths.ToArray();
        }

        private static void AddConfiguredPaths(List<string> paths, string configured)
        {
            if (string.IsNullOrWhiteSpace(configured))
            {
                return;
            }

            string[] parts = configured.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                AddPath(paths, part);
            }
        }

        private static void AddPath(List<string> paths, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string trimmed = path.Trim();
            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            paths.Add(trimmed);
        }

        private static bool IsWindowsRuntime()
        {
            PlatformID platform = Environment.OSVersion.Platform;
            return platform == PlatformID.Win32NT
                   || platform == PlatformID.Win32S
                   || platform == PlatformID.Win32Windows
                   || platform == PlatformID.WinCE;
        }
        private static RewardsConfig LoadRewardsConfig()
        {
            var config = new RewardsConfig { FreeTestMode = false, Days = new Dictionary<string, DayReward>() };
            foreach (string path in GetRewardsJsonPaths())
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    RewardsConfig loaded = serializer.Deserialize<RewardsConfig>(File.ReadAllText(path));
                    if (loaded != null)
                    {
                        if (loaded.Days == null)
                        {
                            loaded.Days = new Dictionary<string, DayReward>();
                        }

                        DayReward day3;
                        int day3Amount = 0;
                        if (loaded.Days.TryGetValue("3", out day3) && day3 != null)
                        {
                            day3Amount = day3.Amount;
                        }

                        Log(
                            "rewards.json loaded path="
                            + path
                            + " days="
                            + loaded.Days.Count
                            + " day3Amount="
                            + day3Amount
                            + " freeTest="
                            + loaded.FreeTestMode);
                        return loaded;
                    }
                }
                catch (Exception ex)
                {
                    Log("rewards.json load failed path=" + path + " err=" + ex.Message);
                }
            }

            Log("rewards.json not loaded — using empty config (amounts fall back to pending/hardcoded)");
            return config;
        }

        private static DayReward ResolveDayReward(RewardsConfig config, int day)
        {
            if (config != null && config.Days != null)
            {
                DayReward reward;
                if (config.Days.TryGetValue(day.ToString(CultureInfo.InvariantCulture), out reward) && reward != null)
                {
                    return reward;
                }
            }

            // Amount 0 so ResolveGrantAmount can prefer pending / hardcoded stacks.
            return new DayReward { ItemId = 0, Amount = 0, Quality = 1 };
        }

        /// <summary>
        /// Prefer rewards.json amount, then pending, then known stack days (recharger x50 / stim x25).
        /// </summary>
        private static int ResolveGrantAmount(int day, DayReward dayReward, PendingClaim pending)
        {
            int amount = 0;
            if (dayReward != null && dayReward.Amount > 0)
            {
                amount = dayReward.Amount;
            }

            if (pending != null && pending.Amount > amount)
            {
                amount = pending.Amount;
            }

            if (amount <= 0)
            {
                if (day == 3 || day == 17)
                {
                    amount = 50;
                }
                else if (day == 4 || day == 9 || day == 23 || day == 26)
                {
                    amount = 25;
                }
                else
                {
                    amount = 1;
                }
            }

            // Free Movement (day 9 / 23): always x25.
            if ((day == 9 || day == 23) && amount < 25)
            {
                amount = 25;
            }

            // Day 2 random ammo box: always x50000.
            if (day == 2 && amount < 50000)
            {
                amount = 50000;
            }

            return amount;
        }

        private static bool TryResolveGrantItemAndQuality(
            ICharacter source,
            int day,
            DayReward dayReward,
            PendingClaim pending,
            out int itemId,
            out int quality)
        {
            itemId = 0;
            quality = 1;

            string mode = dayReward != null ? dayReward.QualityMode : null;
            if (day == 10
                || string.Equals(mode, "professionVendorNano", StringComparison.OrdinalIgnoreCase))
            {
                return TryPickProfessionVendorNano(source, dayReward, out itemId, out quality);
            }

            itemId = ResolveGrantItemId(day, dayReward, pending);
            quality = ResolveGrantQuality(source, dayReward, pending);
            return itemId > 0;
        }

        private static int ResolveGrantItemId(int day, DayReward dayReward, PendingClaim pending)
        {
            int[] pool = ResolveRandomPool(day, dayReward);
            if (pool != null && pool.Length > 0)
            {
                int picked = PickRandomExistingItemId(pool);
                if (picked > 0)
                {
                    Log(
                        "random reward day="
                        + day
                        + " picked="
                        + picked
                        + " poolSize="
                        + pool.Length);
                    return picked;
                }
            }

            if (dayReward != null && dayReward.ItemId > 0)
            {
                return dayReward.ItemId;
            }

            return pending != null ? pending.ItemId : 0;
        }

        private static int ResolveGrantQuality(ICharacter source, DayReward dayReward, PendingClaim pending)
        {
            string mode = dayReward != null ? dayReward.QualityMode : null;
            int level = ResolveCharacterLevel(source);

            // Days that must match character level (ignore pending/web ql=1).
            if (pending != null
                && (pending.Day == 3
                    || pending.Day == 4
                    || pending.Day == 11
                    || pending.Day == 12
                    || pending.Day == 17
                    || pending.Day == 23
                    || pending.Day == 24
                    || pending.Day == 25
                    || pending.Day == 26))
            {
                return level;
            }

            if (!string.IsNullOrEmpty(mode))
            {
                if (string.Equals(mode, "characterLevel", StringComparison.OrdinalIgnoreCase))
                {
                    return level;
                }

                if (string.Equals(mode, "characterLevelPlusMinus", StringComparison.OrdinalIgnoreCase))
                {
                    int delta = dayReward.QualityDelta > 0 ? dayReward.QualityDelta : 10;
                    int minQl = Math.Max(1, level - delta);
                    int maxQl = level + delta;
                    lock (Random)
                    {
                        return Random.Next(minQl, maxQl + 1);
                    }
                }

                if (string.Equals(mode, "fixed", StringComparison.OrdinalIgnoreCase)
                    && dayReward != null
                    && dayReward.Quality > 0)
                {
                    return dayReward.Quality;
                }
            }

            if (dayReward != null && dayReward.Quality > 0)
            {
                return dayReward.Quality;
            }

            if (pending != null && pending.Quality > 0)
            {
                return pending.Quality;
            }

            return 1;
        }

        private static int ResolveCharacterLevel(ICharacter source)
        {
            try
            {
                Character character = source as Character;
                if (character != null)
                {
                    return Math.Max(1, character.Stats[StatIds.level].Value);
                }
            }
            catch
            {
            }

            return 1;
        }

        private static int ResolveCharacterProfession(ICharacter source)
        {
            try
            {
                Character character = source as Character;
                if (character != null)
                {
                    return character.Stats[StatIds.profession].Value;
                }
            }
            catch
            {
            }

            return 0;
        }

        /// <summary>
        /// Day 10: 1 random Nano Crystal for the character's profession from
        /// Desktop\zadaily\nanos.txt (fallback: Content/Daily/day10-profession-nanos.json),
        /// only if nano QL is within character level +- delta (default 10).
        /// </summary>
        private static bool TryPickProfessionVendorNano(
            ICharacter source,
            DayReward dayReward,
            out int itemId,
            out int quality)
        {
            itemId = 0;
            quality = 1;

            Dictionary<int, List<Day10NanoEntry>> catalog = LoadDay10ProfessionNanoCatalog();
            if (catalog == null || catalog.Count == 0)
            {
                Log("day10 profession nano catalog missing");
                return false;
            }

            int profession = ResolveCharacterProfession(source);
            int level = ResolveCharacterLevel(source);
            int delta = dayReward != null && dayReward.QualityDelta > 0 ? dayReward.QualityDelta : 10;

            List<Day10NanoEntry> pool;
            if (!catalog.TryGetValue(profession, out pool) || pool == null || pool.Count == 0)
            {
                Log("day10 no nano pool for profession=" + profession);
                return false;
            }

            int minQl = Math.Max(1, level - delta);
            int maxQl = level + delta;
            var inRange = new List<Day10NanoEntry>();
            var usable = new List<Day10NanoEntry>();
            for (int i = 0; i < pool.Count; i++)
            {
                Day10NanoEntry entry = pool[i];
                if (entry == null || entry.ItemId <= 0 || !ItemLoader.ItemList.ContainsKey(entry.ItemId))
                {
                    continue;
                }

                // Skip sealed profession package accidentally listed in every profession block.
                if (entry.ItemId == 302080)
                {
                    continue;
                }

                int entryQl = ResolveDay10NanoQuality(entry);
                entry.Quality = entryQl;
                usable.Add(entry);
                if (entryQl >= minQl && entryQl <= maxQl)
                {
                    inRange.Add(entry);
                }
            }

            if (inRange.Count == 0)
            {
                // Fall back to nearest QL band for this profession so claim still works.
                int bestDistance = int.MaxValue;
                for (int i = 0; i < usable.Count; i++)
                {
                    Day10NanoEntry entry = usable[i];
                    int entryQl = entry.Quality;
                    int distance = entryQl < minQl
                                       ? minQl - entryQl
                                       : (entryQl > maxQl ? entryQl - maxQl : 0);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        inRange.Clear();
                        inRange.Add(entry);
                    }
                    else if (distance == bestDistance)
                    {
                        inRange.Add(entry);
                    }
                }
            }

            if (inRange.Count == 0)
            {
                Log("day10 profession=" + profession + " level=" + level + " had no usable nanos");
                return false;
            }

            Day10NanoEntry picked;
            lock (Random)
            {
                picked = inRange[Random.Next(inRange.Count)];
            }

            itemId = picked.ItemId;
            quality = Math.Max(1, picked.Quality);
            Log(
                "day10 profession nano profession="
                + profession
                + " level="
                + level
                + " picked="
                + itemId
                + " ql="
                + quality
                + " candidates="
                + inRange.Count);
            return true;
        }

        private static int ResolveDay10NanoQuality(Day10NanoEntry entry)
        {
            if (entry == null || entry.ItemId <= 0)
            {
                return 1;
            }

            if (entry.Quality > 0)
            {
                return entry.Quality;
            }

            ItemTemplate template;
            if (ItemLoader.ItemList.TryGetValue(entry.ItemId, out template) && template != null)
            {
                return Math.Max(1, template.Quality);
            }

            return 1;
        }

        /// <summary>
        /// Build QL-scaled items via template Relations (Miy: low QL1 + high QL300).
        /// Never grant high-endpoint alone — client then shows QL1 with max stats.
        /// </summary>
        private static Item CreateScaledRewardItem(int quality, int seedItemId, int[] randomPool)
        {
            int lowId;
            int highId;
            if (!TryResolveScalePair(seedItemId, quality, randomPool, out lowId, out highId))
            {
                lowId = seedItemId;
                highId = seedItemId;
            }

            Item item = new Item(Math.Max(1, quality), lowId, highId) { MultipleCount = 1 };

            if (item.LowID == item.HighID)
            {
                int pairLow;
                int pairHigh;
                if (TryGetRelationPair(item.LowID, out pairLow, out pairHigh))
                {
                    item = new Item(Math.Max(1, quality), pairLow, pairHigh) { MultipleCount = 1 };
                    lowId = pairLow;
                    highId = pairHigh;
                }
            }

            Log(
                "scaled reward item seed="
                + seedItemId
                + " low="
                + lowId
                + " high="
                + highId
                + " qlRequested="
                + quality
                + " qlFinal="
                + item.Quality);
            return item;
        }

        private static bool IsFixedPhasefrontRandomRewardDay(int day)
        {
            return day == 1 || day == 28;
        }

        private static bool TryResolveScalePair(
            int seedItemId,
            int quality,
            int[] randomPool,
            out int lowId,
            out int highId)
        {
            lowId = seedItemId;
            highId = seedItemId;

            ItemTemplate seed;
            if (!ItemLoader.ItemList.TryGetValue(seedItemId, out seed) || seed == null)
            {
                return false;
            }

            int pairLow;
            int pairHigh;
            if (TryGetRelationPair(seedItemId, out pairLow, out pairHigh))
            {
                lowId = pairLow;
                highId = pairHigh;
                return true;
            }

            int resolvedLow = seed.GetLowId(Math.Max(1, quality));
            if (resolvedLow > 0 && ItemLoader.ItemList.ContainsKey(resolvedLow))
            {
                if (TryGetRelationPair(resolvedLow, out pairLow, out pairHigh))
                {
                    lowId = pairLow;
                    highId = pairHigh;
                    return true;
                }

                int resolvedHigh = ItemLoader.ItemList[resolvedLow].GetHighId(Math.Max(1, quality));
                if (resolvedHigh > 0
                    && resolvedHigh != 1234567890
                    && ItemLoader.ItemList.ContainsKey(resolvedHigh))
                {
                    lowId = resolvedLow;
                    highId = resolvedHigh;
                    return true;
                }
            }

            if (randomPool != null && randomPool.Length > 0)
            {
                int sibling = FindPairedSiblingId(seedItemId, randomPool);
                if (sibling > 0 && ItemLoader.ItemList.ContainsKey(sibling))
                {
                    ItemTemplate a = ItemLoader.ItemList[seedItemId];
                    ItemTemplate b = ItemLoader.ItemList[sibling];
                    if (a.Quality <= b.Quality)
                    {
                        lowId = seedItemId;
                        highId = sibling;
                    }
                    else
                    {
                        lowId = sibling;
                        highId = seedItemId;
                    }

                    return true;
                }
            }

            return false;
        }

        private static bool TryGetRelationPair(int itemId, out int lowId, out int highId)
        {
            lowId = itemId;
            highId = itemId;
            ItemTemplate template;
            if (!ItemLoader.ItemList.TryGetValue(itemId, out template)
                || template == null
                || template.Relations == null
                || template.Relations.Count < 2)
            {
                return false;
            }

            int bestLow = 0;
            int bestHigh = 0;
            int bestLowQl = int.MaxValue;
            int bestHighQl = int.MinValue;
            for (int i = 0; i < template.Relations.Count; i++)
            {
                int relId = template.Relations[i];
                ItemTemplate rel;
                if (!ItemLoader.ItemList.TryGetValue(relId, out rel) || rel == null)
                {
                    continue;
                }

                if (rel.Quality < bestLowQl)
                {
                    bestLowQl = rel.Quality;
                    bestLow = relId;
                }

                if (rel.Quality > bestHighQl)
                {
                    bestHighQl = rel.Quality;
                    bestHigh = relId;
                }
            }

            if (bestLow <= 0 || bestHigh <= 0 || bestLow == bestHigh || bestLowQl >= bestHighQl)
            {
                return false;
            }

            lowId = bestLow;
            highId = bestHigh;
            return true;
        }

        private static int FindPairedSiblingId(int seedItemId, int[] pool)
        {
            if (pool == null)
            {
                return 0;
            }

            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != seedItemId)
                {
                    continue;
                }

                if (i + 1 < pool.Length && Math.Abs(pool[i + 1] - seedItemId) == 1)
                {
                    return pool[i + 1];
                }

                if (i > 0 && Math.Abs(pool[i - 1] - seedItemId) == 1)
                {
                    return pool[i - 1];
                }
            }

            return 0;
        }

        private static Dictionary<int, List<Day10NanoEntry>> day10ProfessionNanoCatalog;

        private static readonly string[] Day10NanosTxtPaths =
            {
                @"C:\Users\nermi\Desktop\zadaily\nanos.txt",
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "zadaily",
                    "nanos.txt")
            };

        private static Dictionary<int, List<Day10NanoEntry>> LoadDay10ProfessionNanoCatalog()
        {
            if (day10ProfessionNanoCatalog != null)
            {
                return day10ProfessionNanoCatalog;
            }

            lock (SyncRoot)
            {
                if (day10ProfessionNanoCatalog != null)
                {
                    return day10ProfessionNanoCatalog;
                }

                for (int i = 0; i < Day10NanosTxtPaths.Length; i++)
                {
                    string path = Day10NanosTxtPaths[i];
                    Dictionary<int, List<Day10NanoEntry>> fromTxt;
                    if (TryLoadDay10NanosTxt(path, out fromTxt) && fromTxt.Count > 0)
                    {
                        day10ProfessionNanoCatalog = fromTxt;
                        Log(
                            "loaded day10 profession nano catalog from nanos.txt path="
                            + path
                            + " professions="
                            + fromTxt.Count);
                        return day10ProfessionNanoCatalog;
                    }
                }

                string[] jsonPaths =
                    {
                        Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "Content",
                            "Daily",
                            "day10-profession-nanos.json"),
                        Path.Combine(
                            Environment.CurrentDirectory,
                            "Content",
                            "Daily",
                            "day10-profession-nanos.json")
                    };

                for (int i = 0; i < jsonPaths.Length; i++)
                {
                    string path = jsonPaths[i];
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    try
                    {
                        string json = File.ReadAllText(path);
                        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                        Day10NanoCatalogFile loaded = serializer.Deserialize<Day10NanoCatalogFile>(json);
                        var map = new Dictionary<int, List<Day10NanoEntry>>();
                        if (loaded != null && loaded.Professions != null)
                        {
                            foreach (KeyValuePair<string, Day10NanoEntry[]> pair in loaded.Professions)
                            {
                                int professionId;
                                if (!int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out professionId)
                                    || pair.Value == null)
                                {
                                    continue;
                                }

                                map[professionId] = new List<Day10NanoEntry>(pair.Value);
                            }
                        }

                        day10ProfessionNanoCatalog = map;
                        Log("loaded day10 profession nano catalog path=" + path + " professions=" + map.Count);
                        return day10ProfessionNanoCatalog;
                    }
                    catch (Exception ex)
                    {
                        Log("day10 catalog load failed path=" + path + " err=" + ex.Message);
                    }
                }

                day10ProfessionNanoCatalog = new Dictionary<int, List<Day10NanoEntry>>();
                return day10ProfessionNanoCatalog;
            }
        }

        /// <summary>
        /// Parse Desktop\zadaily\nanos.txt:
        /// ProfessionName
        /// id id id ...
        /// </summary>
        private static bool TryLoadDay10NanosTxt(string path, out Dictionary<int, List<Day10NanoEntry>> map)
        {
            map = new Dictionary<int, List<Day10NanoEntry>>();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(path);
                int currentProfession = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i] == null ? string.Empty : lines[i].Trim();
                    if (line.Length == 0
                        || line.StartsWith("Profession Nano", StringComparison.OrdinalIgnoreCase)
                        || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int professionId;
                    if (TryMapProfessionName(line, out professionId))
                    {
                        currentProfession = professionId;
                        if (!map.ContainsKey(currentProfession))
                        {
                            map[currentProfession] = new List<Day10NanoEntry>();
                        }

                        continue;
                    }

                    if (currentProfession <= 0)
                    {
                        continue;
                    }

                    string[] parts = line.Split(
                        new[] { ' ', '\t', ',', ';' },
                        StringSplitOptions.RemoveEmptyEntries);
                    for (int p = 0; p < parts.Length; p++)
                    {
                        int itemId;
                        if (!int.TryParse(parts[p], NumberStyles.Integer, CultureInfo.InvariantCulture, out itemId)
                            || itemId <= 0)
                        {
                            continue;
                        }

                        map[currentProfession].Add(new Day10NanoEntry { ItemId = itemId, Quality = 0 });
                    }
                }

                return map.Count > 0;
            }
            catch (Exception ex)
            {
                Log("day10 nanos.txt load failed path=" + path + " err=" + ex.Message);
                map = new Dictionary<int, List<Day10NanoEntry>>();
                return false;
            }
        }

        private static bool TryMapProfessionName(string name, out int professionId)
        {
            professionId = 0;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string key = name.Trim().ToLowerInvariant().Replace("-", string.Empty).Replace(" ", string.Empty);
            switch (key)
            {
                case "soldier":
                    professionId = 1;
                    return true;
                case "martialartist":
                    professionId = 2;
                    return true;
                case "engineer":
                    professionId = 3;
                    return true;
                case "fixer":
                    professionId = 4;
                    return true;
                case "agent":
                    professionId = 5;
                    return true;
                case "adventurer":
                    professionId = 6;
                    return true;
                case "trader":
                    professionId = 7;
                    return true;
                case "bureaucrat":
                    professionId = 8;
                    return true;
                case "enforcer":
                    professionId = 9;
                    return true;
                case "doctor":
                    professionId = 10;
                    return true;
                case "nanotechnician":
                    professionId = 11;
                    return true;
                case "metaphysicist":
                    professionId = 12;
                    return true;
                case "keeper":
                    professionId = 14;
                    return true;
                case "shade":
                    professionId = 15;
                    return true;
                default:
                    return false;
            }
        }

        private static int[] ResolveRandomPool(int day, DayReward dayReward)
        {
            if (dayReward != null && dayReward.RandomItemIds != null && dayReward.RandomItemIds.Length > 0)
            {
                return dayReward.RandomItemIds;
            }

            if (day == 1)
            {
                return Day1PhantomPool;
            }

            if (day == 28)
            {
                return Day28PhasefrontPool;
            }

            return null;
        }

        private static int PickRandomExistingItemId(int[] pool)
        {
            if (pool == null || pool.Length == 0)
            {
                return 0;
            }

            var valid = new List<int>(pool.Length);
            var lowEndpoints = new List<int>();
            for (int i = 0; i < pool.Length; i++)
            {
                int id = pool[i];
                if (id <= 0 || !ItemLoader.ItemList.ContainsKey(id))
                {
                    continue;
                }

                valid.Add(id);

                // Prefer QL1 endpoints (Miy 268894 not 268895) so scaling always has a pair.
                int pairLow;
                int pairHigh;
                if (TryGetRelationPair(id, out pairLow, out pairHigh) && id == pairLow)
                {
                    lowEndpoints.Add(id);
                }
            }

            List<int> choose = lowEndpoints.Count > 0 ? lowEndpoints : valid;
            if (choose.Count == 0)
            {
                return 0;
            }

            lock (Random)
            {
                return choose[Random.Next(choose.Count)];
            }
        }

        private static string ResolveAccountKey(ICharacter source)
        {
            int characterId = source.Identity.Instance;
            string accountKey = MissionRuntime.ResolveAccountKey(characterId);
            if (!string.IsNullOrWhiteSpace(accountKey))
            {
                return accountKey.Trim().ToLowerInvariant();
            }

            return "character:" + characterId.ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsDayTaken(ClaimState state, int day)
        {
            if (state == null || state.Taken == null || day < 1)
            {
                return false;
            }

            for (int i = 0; i < state.Taken.Length; i++)
            {
                if (state.Taken[i] == day)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>First day 1..28 not in Taken (account queue order).</summary>
        private static int ResolveNextQueueDay(ClaimState state)
        {
            for (int day = 1; day <= TotalDays; day++)
            {
                if (!IsDayTaken(state, day))
                {
                    return day;
                }
            }

            return 0;
        }

        private static string UtcDay()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string UtcMonth()
        {
            return DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        private static ClaimState LoadState(string accountKey, string month)
        {
            ClaimState state = new ClaimState
                               {
                                   Month = month,
                                   ClaimedCount = 0,
                                   LastClaimUtc = string.Empty,
                                   LastGrantedUtc = string.Empty,
                                   CycleCompletedOn = string.Empty,
                                   Taken = new int[0],
                                   LastCharacterId = 0
                               };

            string path = FindExistingStatePath(accountKey);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return state;
            }

            try
            {
                string json = File.ReadAllText(path);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                ClaimState loaded = serializer.Deserialize<ClaimState>(json);
                if (loaded == null)
                {
                    return state;
                }

                if (loaded.Taken == null)
                {
                    loaded.Taken = new int[0];
                }

                if (loaded.LastGrantedUtc == null)
                {
                    loaded.LastGrantedUtc = string.Empty;
                }

                if (loaded.CycleCompletedOn == null)
                {
                    loaded.CycleCompletedOn = string.Empty;
                }

                string today = UtcDay();

                // After all 28 claimed, reset board at next calendar day 00:00.
                if (!string.IsNullOrEmpty(loaded.CycleCompletedOn)
                    && string.CompareOrdinal(today, loaded.CycleCompletedOn) > 0)
                {
                    Log("cycle reset account=" + accountKey + " completedOn=" + loaded.CycleCompletedOn);
                    state.Month = month;
                    SaveState(accountKey, state);
                    return state;
                }

                // Also treat full taken without CycleCompletedOn (legacy) as complete.
                if (loaded.Taken.Length >= TotalDays
                    && !string.IsNullOrEmpty(loaded.LastClaimUtc)
                    && string.CompareOrdinal(today, loaded.LastClaimUtc) > 0)
                {
                    Log("cycle reset (legacy full) account=" + accountKey);
                    state.Month = month;
                    SaveState(accountKey, state);
                    return state;
                }

                loaded.Month = month;
                loaded.ClaimedCount = loaded.Taken.Length;
                return loaded;
            }
            catch (Exception ex)
            {
                Log("load state failed account=" + accountKey + " err=" + ex.Message);
                return state;
            }
        }

        private static void SaveState(string accountKey, ClaimState state)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(state);
            foreach (string root in GetClaimRoots())
            {
                try
                {
                    if (!Directory.Exists(root))
                    {
                        Directory.CreateDirectory(root);
                    }

                    string path = Path.Combine(root, AccountFileName(accountKey));
                    File.WriteAllText(path, json);
                }
                catch (Exception ex)
                {
                    Log("save state failed root=" + root + " err=" + ex.Message);
                }
            }
        }

        private static string FindExistingStatePath(string accountKey)
        {
            string name = AccountFileName(accountKey);
            foreach (string root in GetClaimRoots())
            {
                string path = Path.Combine(root, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private static string AccountFileName(string accountKey)
        {
            return "account-" + SafeFileKey(accountKey) + ".json";
        }

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, "[DailyLogin] " + message);
        }

        private sealed class ClaimState
        {
            public string Month { get; set; }

            public int ClaimedCount { get; set; }

            public string LastClaimUtc { get; set; }

            public string LastGrantedUtc { get; set; }

            /// <summary>UTC date (yyyy-MM-dd) when the 28th reward was claimed. Reset after next day.</summary>
            public string CycleCompletedOn { get; set; }

            public int[] Taken { get; set; }

            public int LastCharacterId { get; set; }
        }

        private sealed class PendingClaim
        {
            public int Day { get; set; }

            public int ItemId { get; set; }

            public int Amount { get; set; }

            public int Quality { get; set; }

            public int CharacterId { get; set; }

            public string AccountKey { get; set; }

            /// <summary>Web poll token (AO browser often has no CharacterID).</summary>
            public string ClaimToken { get; set; }
        }

        private sealed class RewardsConfig
        {
            // JavaScriptSerializer is case-insensitive — do NOT add camelCase aliases
            // (Amount/amount etc. throw AmbiguousMatchException and rewards.json never loads).
            public bool FreeTestMode { get; set; }

            public Dictionary<string, DayReward> Days { get; set; }
        }

        private sealed class DayReward
        {
            public int ItemId { get; set; }

            public int Amount { get; set; }

            public int Quality { get; set; }

            /// <summary>
            /// fixed (default) | characterLevel | characterLevelPlusMinus (uses QualityDelta, default 10)
            /// | professionVendorNano (day 10: profession shop nanos within level +- QualityDelta).
            /// </summary>
            public string QualityMode { get; set; }

            public int QualityDelta { get; set; }

            public string ItemName { get; set; }

            /// <summary>Optional pool; Zone picks one existing ID at claim time (Day 1 / 28).</summary>
            public int[] RandomItemIds { get; set; }
        }

        private sealed class Day10NanoCatalogFile
        {
            public int QualityDelta { get; set; }

            public Dictionary<string, Day10NanoEntry[]> Professions { get; set; }
        }

        private sealed class Day10NanoEntry
        {
            public int ItemId { get; set; }

            public int Quality { get; set; }
        }
    }
}
