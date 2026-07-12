namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;
    using AORebirth.Stats.SpecialStats;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// RK combat XP (capture-backed):
    /// - Normal kill wire: XP = cumulative total only (Unknown=0).
    /// - Level-up wire: LastSaveXP=floor (Unknown=1), XP=floor (Unknown=0), UnsavedXP=overflow, NextXP.
    /// - Server bar progress = cumulative XP - floor; level-up when progress >= next threshold.
    /// </summary>
    internal static class CombatXpRuntimeService
    {
        private const int MaxLevel = 200;
        private const int UnsetStatSentinel = 1234567890;
        private const int CapturedMalfunctioningCleaningRobotXp = 260;
        private const byte CapturedLevelUpStatUnknown = 1;
        private const int GreyMobMinLevelAdvantage = 7;
        private const int LevelUpFeedbackCategoryId = 110;
        private const int XpFeedbackMessageId = 249817907;
        private const int CapturedNewLevelUnknown2 = 4;
        private const string XpTracePrefix = "COMBAT_XP_TRACE";
        internal const string XpWireTracePrefix = "XP_WIRE_TRACE";

        private static readonly int[] WireManagedXpStatIds =
        {
            (int)StatIds.xp,
            (int)StatIds.level,
            (int)StatIds.lastxp,
            (int)StatIds.savedxp,
            (int)StatIds.nextxp,
            (int)StatIds.lastsavexp,
            (int)StatIds.unsavedxp
        };

        internal static void RemoveWireManagedStatsFromBulk(Dictionary<int, uint> stats)
        {
            if (stats == null || stats.Count == 0)
            {
                return;
            }

            for (int i = 0; i < WireManagedXpStatIds.Length; i++)
            {
                stats.Remove(WireManagedXpStatIds[i]);
            }
        }

        internal static void AwardCombatXp(
            ICharacter attacker,
            ICharacter target,
            Action<ICharacter, string> sendFeedback)
        {
            if (attacker == null || target == null || !(attacker.Controller is Controllers.PlayerController))
            {
                return;
            }

            IZoneClient client = attacker.Controller.Client;
            if (client == null)
            {
                return;
            }

            int xpReward = CalculateCombatXpReward(attacker, target);
            if (xpReward <= 0)
            {
                LogXpTrace(attacker, "kill-skip", "reason=zero-reward");
                return;
            }

            LogXpRewardSource(attacker, target, xpReward);

            int levelBefore = GetCurrentLevel(attacker);

            LogXpTrace(
                attacker,
                "kill-start",
                "reward=" + xpReward.ToString(CultureInfo.InvariantCulture));

            uint floorXp = GetCumulativeXpForLevelStart(levelBefore);
            uint progressBefore = GetBarProgress(attacker);
            uint newProgress = AddClamped(progressBefore, xpReward);

            SetXpStat(attacker, StatIds.unsavedxp, newProgress, "kill-add-unsaved");
            SetXpStat(attacker, StatIds.xp, floorXp + newProgress, "kill-add-cumulative");
            SetXpStat(attacker, StatIds.lastxp, (uint)xpReward, "kill-add-lastxp");
            EnsureLevelXpThresholds(attacker, "kill-add-thresholds");

            LogXpTrace(
                attacker,
                "kill-after-add",
                "progressBefore=" + progressBefore.ToString(CultureInfo.InvariantCulture)
                + " progressAfter=" + newProgress.ToString(CultureInfo.InvariantCulture)
                + " cumulative=" + attacker.Stats[StatIds.xp].BaseValue.ToString(CultureInfo.InvariantCulture));

            bool leveledUp = ApplyPendingLevelUps(attacker, levelBefore);

            if (sendFeedback != null)
            {
                LogXpTrace(attacker, "xp-chat-deferred", "source=captured-feedback-message");
            }

            if (leveledUp)
            {
                SendLevelUpPreFeedbackPackets(client, attacker, levelBefore);
                PersistLevelStat(attacker);
            }
            else
            {
                if (GetBarProgress(attacker) >= (uint)GetNextXpRequiredForLevel(levelBefore))
                {
                    LogXpTrace(
                        attacker,
                        "levelup-missed",
                        "reason=progress-met-but-ApplyPendingLevelUps-returned-false"
                        + " progress=" + GetBarProgress(attacker).ToString(CultureInfo.InvariantCulture)
                        + " required=" + GetNextXpRequiredForLevel(levelBefore).ToString(CultureInfo.InvariantCulture));
                }

                SendNormalKillXpPacket(client, attacker);
            }

            ClearManualXpWireStatChangedFlags(attacker, leveledUp);
            WriteXpStatsToDb(attacker, leveledUp ? "kill-complete-levelup" : "kill-complete");

            LogXpTrace(
                attacker,
                leveledUp ? "kill-complete-levelup" : "kill-complete",
                "levelBefore=" + levelBefore.ToString(CultureInfo.InvariantCulture)
                + " levelAfter=" + GetCurrentLevel(attacker).ToString(CultureInfo.InvariantCulture)
                + " leveledUp=" + leveledUp.ToString(CultureInfo.InvariantCulture)
                + " wire=" + (leveledUp ? "levelup-packets" : "xp-only"));
        }

        internal static void PrepareXpStatsForLogin(ICharacter character)
        {
            if (character == null || !(character.Controller is Controllers.PlayerController))
            {
                return;
            }

            LogXpWireSnapshot(character, "CombatXpRuntimeService", "login-prepare-before");

            int levelBefore = GetCurrentLevel(character);
            NormalizeXpStatsFromPersistedLevel(character);

            int levelAfter = GetCurrentLevel(character);
            IZoneClient client = character.Controller?.Client;
            if (levelAfter > levelBefore && client != null)
            {
                LogXpWireSnapshot(
                    character,
                    "CombatXpRuntimeService",
                    "login-prepare-levelup-wire",
                    "levelBefore=" + levelBefore.ToString(CultureInfo.InvariantCulture)
                    + " levelAfter=" + levelAfter.ToString(CultureInfo.InvariantCulture));
                SendLevelUpPreFeedbackPackets(client, character, levelBefore);
                PersistLevelStat(character);
            }

            WriteXpStatsToDb(character, "login-complete");
            LogXpWireSnapshot(character, "CombatXpRuntimeService", "login-prepare-after");
        }

        internal static void SendLoginXpBarSync(ICharacter character)
        {
            if (character == null || character.Controller?.Client == null)
            {
                return;
            }

            IZoneClient client = character.Controller.Client;
            int level = GetCurrentLevel(character);
            uint floorXp = GetCumulativeXpForLevelStart(level);
            uint progress = GetBarProgress(character);
            uint cumulative = floorXp + progress;
            uint nextXp = (uint)GetNextXpRequiredForLevel(level);

            LogXpWireSnapshot(
                character,
                "CombatXpRuntimeService",
                "login-bar-sync-before",
                "cumulative=" + cumulative.ToString(CultureInfo.InvariantCulture));

            // After FullCharacter (unsaved/next/level only): replay the capture-backed level-up
            // XP wire for the *current* level. Logs show XP(52) alone makes the bar show
            // cumulative (7280/4000); NewLevel + LastSaveXP + XP matches in-zone level-up
            // (130/4000). No FeedbackMessage on login — that triggers bogus reward chat.
            uint nextLevelCumulative = level >= MaxLevel
                ? 0
                : GetCumulativeXpForLevelStart(level + 1);
            var loginNewLevelMessage = new NewLevelMessage
                                       {
                                           Identity = character.Identity,
                                           Unknown = 0,
                                           Level = level,
                                           Ip = Math.Max(0, character.Stats[StatIds.ip].Value),
                                           Xp = (int)cumulative,
                                           LastSaveXp = (int)floorXp,
                                           NextLevelXp = (int)nextLevelCumulative,
                                           Unknown1 = 0,
                                           Unknown2 = CapturedNewLevelUnknown2,
                                           LastXp = Math.Max(0, character.Stats[StatIds.lastxp].Value)
                                       };
            LogXpWireNewLevel(
                "CombatXpRuntimeService",
                "login-bar-sync-newlevel",
                character,
                loginNewLevelMessage);
            client.SendCompressed(loginNewLevelMessage);
            SendClientStatWithUnknown(
                client,
                character,
                CharacterStat.LastSaveXP,
                floorXp,
                CapturedLevelUpStatUnknown,
                "login-bar-sync");
            SendSocialStatusReset(client, character);
            SendClientStatWithUnknown(
                client,
                character,
                CharacterStat.XP,
                cumulative,
                0,
                "login-bar-sync-xp-baseline");
            LogXpTrace(
                character,
                "login-bar-sync",
                "wire=after-fullchar cumulative=" + cumulative.ToString(CultureInfo.InvariantCulture)
                + " floor=" + floorXp.ToString(CultureInfo.InvariantCulture)
                + " progress=" + progress.ToString(CultureInfo.InvariantCulture)
                + " next=" + nextXp.ToString(CultureInfo.InvariantCulture)
                + " level=" + level.ToString(CultureInfo.InvariantCulture)
                + " newLevelReplay=true feedback=none");
        }

        internal static void SyncXpBarStatsOnLogin(ICharacter character)
        {
            SendLoginXpBarSync(character);
        }

        private static int CalculateCombatXpReward(ICharacter attacker, ICharacter target)
        {
            int xpReward = ResolveBaseCombatXpReward(target);
            return ApplyGreyMobXpCap(attacker, target, xpReward);
        }

        private static int ResolveBaseCombatXpReward(ICharacter target)
        {
            CombatTestMobArchetype.Entry archetype;
            if (CombatTestMobArchetype.TryGetByName(target.Name, out archetype) && archetype.XpReward > 0)
            {
                return archetype.XpReward;
            }

            int targetXp = (int)target.Stats[StatIds.xp].BaseValue;
            if (targetXp > 0)
            {
                return targetXp;
            }

            return CapturedMalfunctioningCleaningRobotXp;
        }

        private static int ApplyGreyMobXpCap(ICharacter attacker, ICharacter target, int xpReward)
        {
            int targetLevel = Math.Max(1, (int)target.Stats[StatIds.level].BaseValue);
            int attackerLevel = GetCurrentLevel(attacker);
            if (attackerLevel - targetLevel >= GreyMobMinLevelAdvantage)
            {
                return 1;
            }

            return xpReward;
        }

        private static bool ApplyPendingLevelUps(ICharacter character, int levelBefore)
        {
            int highestLevelReached = levelBefore;
            int guard = 0;

            while (guard++ < 20)
            {
                int currentLevel = GetCurrentLevel(character);
                if (currentLevel >= MaxLevel)
                {
                    break;
                }

                int nextXpRequired = GetNextXpRequiredForLevel(currentLevel);
                if (nextXpRequired <= 0)
                {
                    break;
                }

                uint barProgress = GetBarProgress(character);
                if (barProgress < (uint)nextXpRequired)
                {
                    LogXpTrace(
                        character,
                        "levelup-skip",
                        "currentLevel=" + currentLevel.ToString(CultureInfo.InvariantCulture)
                        + " progress=" + barProgress.ToString(CultureInfo.InvariantCulture)
                        + " required=" + nextXpRequired.ToString(CultureInfo.InvariantCulture));
                    break;
                }

                int newLevel = currentLevel + 1;
                uint remainder = barProgress - (uint)nextXpRequired;
                uint newFloor = GetCumulativeXpForLevelStart(newLevel);

                LogXpTrace(
                    character,
                    "levelup-apply",
                    "fromLevel=" + currentLevel.ToString(CultureInfo.InvariantCulture)
                    + " toLevel=" + newLevel.ToString(CultureInfo.InvariantCulture)
                    + " progressBefore=" + barProgress.ToString(CultureInfo.InvariantCulture)
                    + " threshold=" + nextXpRequired.ToString(CultureInfo.InvariantCulture)
                    + " remainder=" + remainder.ToString(CultureInfo.InvariantCulture)
                    + " newFloor=" + newFloor.ToString(CultureInfo.InvariantCulture));

                SetXpStat(character, StatIds.level, (uint)newLevel, "levelup-apply-level");
                ClearDbManagedFloorStats(character, "levelup-apply");
                SetXpStat(character, StatIds.nextxp, (uint)GetNextXpRequiredForLevel(newLevel), "levelup-apply-next");
                SetXpStat(character, StatIds.unsavedxp, remainder, "levelup-apply-unsaved");
                SetXpStat(character, StatIds.xp, newFloor + remainder, "levelup-apply-cumulative");

                highestLevelReached = newLevel;
            }

            if (highestLevelReached <= levelBefore)
            {
                return false;
            }

            character.CalculateSkills();

            int maxLife = Math.Max(1, character.Stats[StatIds.life].Value);
            int maxNano = Math.Max(0, character.Stats[StatIds.maxnanoenergy].Value);
            character.Stats[StatIds.health].Set((uint)maxLife);
            character.Stats[StatIds.currentnano].Set((uint)maxNano);

            return true;
        }

        private static void NormalizeXpStatsFromPersistedLevel(ICharacter character)
        {
            int level = GetCurrentLevel(character);
            uint floor = GetCumulativeXpForLevelStart(level);
            uint xp = NormalizeStatValue(character.Stats[StatIds.xp].BaseValue);
            uint unsavedXp = NormalizeStatValue(character.Stats[StatIds.unsavedxp].BaseValue);

            LogXpTrace(
                character,
                "login-normalize-before",
                "dbXp=" + xp.ToString(CultureInfo.InvariantCulture)
                + " dbUnsaved=" + unsavedXp.ToString(CultureInfo.InvariantCulture)
                + " dbLastsave=" + NormalizeStatValue(character.Stats[StatIds.lastsavexp].BaseValue).ToString(CultureInfo.InvariantCulture)
                + " dbSaved=" + NormalizeStatValue(character.Stats[StatIds.savedxp].BaseValue).ToString(CultureInfo.InvariantCulture)
                + " floor=" + floor.ToString(CultureInfo.InvariantCulture));

            // Stats 334/372 are not used for manual save XP yet; discard any legacy DB floor values.
            ClearDbManagedFloorStats(character, "login-normalize-clear-db-floor");

            uint progress = ResolveStoredProgress(level, floor, xp, unsavedXp);

            SetXpStat(character, StatIds.unsavedxp, progress, "login-normalize-unsaved");
            SetXpStat(character, StatIds.xp, floor + progress, "login-normalize-cumulative");
            EnsureLevelXpThresholds(character, "login-normalize-thresholds");

            LogXpTrace(
                character,
                "login-normalize-after",
                "resolvedProgress=" + progress.ToString(CultureInfo.InvariantCulture));

            ApplyPendingLevelUps(character, level);
        }

        private static uint ResolveStoredProgress(int level, uint floor, uint xp, uint unsavedXp)
        {
            if (unsavedXp > 0)
            {
                return unsavedXp;
            }

            if (xp == 0)
            {
                return 0;
            }

            if (xp >= floor)
            {
                return xp - floor;
            }

            return xp;
        }

        private static uint NormalizeStatValue(uint value)
        {
            if (value == UnsetStatSentinel)
            {
                return 0;
            }

            return value;
        }

        private static uint GetBarProgress(ICharacter character)
        {
            int level = GetCurrentLevel(character);
            uint floor = GetCumulativeXpForLevelStart(level);
            uint unsaved = NormalizeStatValue(character.Stats[StatIds.unsavedxp].BaseValue);
            if (unsaved > 0)
            {
                return unsaved;
            }

            uint xp = NormalizeStatValue(character.Stats[StatIds.xp].BaseValue);
            if (xp >= floor)
            {
                return xp - floor;
            }

            return xp;
        }

        private static void ClearManualXpWireStatChangedFlags(ICharacter character, bool leveled)
        {
            ClearStatChangedFlag(character, StatIds.xp);
            ClearStatChangedFlag(character, StatIds.level);
            ClearStatChangedFlag(character, StatIds.lastxp);
            ClearStatChangedFlag(character, StatIds.savedxp);
            ClearStatChangedFlag(character, StatIds.nextxp);
            ClearStatChangedFlag(character, StatIds.lastsavexp);
            ClearStatChangedFlag(character, StatIds.unsavedxp);

            if (leveled)
            {
                ClearStatChangedFlag(character, StatIds.life);
                ClearStatChangedFlag(character, StatIds.maxnanoenergy);
                ClearStatChangedFlag(character, StatIds.currentnano);
                ClearStatChangedFlag(character, StatIds.health);
                ClearStatChangedFlag(character, StatIds.ip);
            }
        }

        private static void ClearStatChangedFlag(ICharacter character, StatIds statId)
        {
            character.Stats[(int)statId].Changed = false;
        }

        private static void EnsureLevelXpThresholds(ICharacter character, string source)
        {
            int level = GetCurrentLevel(character);
            ClearDbManagedFloorStats(character, source);
            SetXpStat(character, StatIds.nextxp, (uint)GetNextXpRequiredForLevel(level), source + ":next");
        }

        /// <summary>
        /// SavedXP (334) and LastSaveXP (372) are not persisted until manual save XP exists.
        /// Level floor for bar/kill wire is computed from the RK table at send time.
        /// </summary>
        private static void ClearDbManagedFloorStats(ICharacter character, string source)
        {
            SetXpStat(character, StatIds.lastsavexp, 0, source + ":lastsave");
            SetXpStat(character, StatIds.savedxp, 0, source + ":saved");
        }

        private static void SendNormalKillXpPacket(IZoneClient client, ICharacter character)
        {
            uint cumulativeXp = character.Stats[StatIds.xp].BaseValue;
            LogXpTrace(character, "wire-normal-kill", "stat=52 value=" + cumulativeXp.ToString(CultureInfo.InvariantCulture));
            LogXpWireOutbound(
                "CombatXpRuntimeService",
                "kill-xp-stat",
                character,
                (int)StatIds.xp,
                cumulativeXp,
                "StatMessage",
                "unknown=0");
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.xp, cumulativeXp);
            LogXpWireFeedbackOutbound(
                "CombatXpRuntimeService",
                "kill-xp-feedback",
                character,
                LevelUpFeedbackCategoryId,
                XpFeedbackMessageId);
            FeedbackMessageHandler.Default.Send(
                character,
                LevelUpFeedbackCategoryId,
                XpFeedbackMessageId);
        }

        private static void SendSocialStatusReset(IZoneClient client, ICharacter character)
        {
            SendClientStatWithUnknown(client, character, CharacterStat.SocialStatus, 0, CapturedLevelUpStatUnknown);
        }

        private static uint GetCumulativeXpForLevelStart(int level)
        {
            if (level <= 1)
            {
                return 0;
            }

            if (level > MaxLevel)
            {
                return (uint)XPTable.TableRKXP[MaxLevel - 1, 1];
            }

            return (uint)XPTable.TableRKXP[level - 1, 1];
        }

        private static int GetNextXpRequiredForLevel(int level)
        {
            if (level < 1 || level >= MaxLevel)
            {
                return 0;
            }

            return (int)XPTable.TableRKXP[level - 1, 2];
        }

        private static void SendLevelUpPreFeedbackPackets(
            IZoneClient client,
            ICharacter character,
            int levelBefore)
        {
            int maxLife = Math.Max(1, character.Stats[StatIds.life].Value);
            int maxNano = Math.Max(0, character.Stats[StatIds.maxnanoenergy].Value);

            StatMessageHandler.Default.SendSingle(character, (int)StatIds.life, (uint)maxLife);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.maxnanoenergy, (uint)maxNano);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.currentnano, (uint)maxNano);

            int levelAfter = GetCurrentLevel(character);
            for (int level = levelBefore + 1; level <= levelAfter; level++)
            {
                uint cumulativeXp = character.Stats[StatIds.xp].BaseValue;
                uint lastSaveXp = GetCumulativeXpForLevelStart(level);
                uint nextLevelXp = level >= MaxLevel
                    ? 0
                    : GetCumulativeXpForLevelStart(level + 1);

                var newLevelMessage = new NewLevelMessage
                                      {
                                          Identity = character.Identity,
                                          Unknown = 0,
                                          Level = level,
                                          Ip = Math.Max(0, character.Stats[StatIds.ip].Value),
                                          Xp = (int)cumulativeXp,
                                          LastSaveXp = (int)lastSaveXp,
                                          NextLevelXp = (int)nextLevelXp,
                                          Unknown1 = 0,
                                          Unknown2 = CapturedNewLevelUnknown2,
                                          LastXp = Math.Max(0, character.Stats[StatIds.lastxp].Value)
                                      };
                LogXpWireNewLevel("CombatXpRuntimeService", "levelup-newlevel", character, newLevelMessage);
                client.SendCompressed(newLevelMessage);

                LogXpTrace(
                    character,
                    "wire-newlevel",
                    "level=" + level.ToString(CultureInfo.InvariantCulture)
                    + " ip=" + character.Stats[StatIds.ip].Value.ToString(CultureInfo.InvariantCulture)
                    + " xp=" + cumulativeXp.ToString(CultureInfo.InvariantCulture)
                    + " lastSaveXp=" + lastSaveXp.ToString(CultureInfo.InvariantCulture)
                    + " nextLevelXp=" + nextLevelXp.ToString(CultureInfo.InvariantCulture)
                    + " lastXp=" + character.Stats[StatIds.lastxp].Value.ToString(CultureInfo.InvariantCulture)
                    + " index=" + (level - levelBefore).ToString(CultureInfo.InvariantCulture)
                    + " totalGained=" + (levelAfter - levelBefore).ToString(CultureInfo.InvariantCulture));
            }

            SendLevelUpXpWireSync(client, character);
        }

        private static void PersistLevelStat(ICharacter character)
        {
            int level = GetCurrentLevel(character);
            int characterId = character.Identity.Instance;
            const int CharacterStatType = 50000;

            DBStats stat = StatDao.Instance
                .GetAll(new { Type = CharacterStatType, Instance = characterId, StatId = (int)StatIds.level })
                .FirstOrDefault();

            if (stat == null)
            {
                StatDao.Instance.Add(
                    new DBStats
                    {
                        Type = CharacterStatType,
                        Instance = characterId,
                        StatId = (int)StatIds.level,
                        StatValue = level
                    });
            }
            else
            {
                stat.StatValue = level;
                StatDao.Instance.Save(stat);
            }

            LogXpTrace(character, "db-level-persist", "stat54=" + level.ToString(CultureInfo.InvariantCulture));
        }

        private static void SendLevelUpXpWireSync(IZoneClient client, ICharacter character)
        {
            int level = GetCurrentLevel(character);
            uint floorXp = GetCumulativeXpForLevelStart(level);
            uint progress = GetBarProgress(character);
            uint cumulativeXp = character.Stats[StatIds.xp].BaseValue;
            uint nextLevelXp = level >= MaxLevel ? 0 : GetCumulativeXpForLevelStart(level + 1);

            // Capture 20260712-131331:
            // NewLevel payload, LastSaveXP Unknown=1, SocialStatus=0 Unknown=1,
            // XP cumulative Unknown=0, Feedback 110/249817907.
            LogXpTrace(
                character,
                "wire-levelup",
                "lastsave372=" + floorXp.ToString(CultureInfo.InvariantCulture)
                + " xp52wire=" + cumulativeXp.ToString(CultureInfo.InvariantCulture)
                + " unsaved592=" + progress.ToString(CultureInfo.InvariantCulture)
                + " nextCumulative=" + nextLevelXp.ToString(CultureInfo.InvariantCulture)
                + " xp52db=" + character.Stats[StatIds.xp].BaseValue.ToString(CultureInfo.InvariantCulture));

            SendClientStatWithUnknown(
                client,
                character,
                CharacterStat.LastSaveXP,
                floorXp,
                CapturedLevelUpStatUnknown,
                "levelup-wire-sync");
            SendSocialStatusReset(client, character);
            LogXpWireOutbound(
                "CombatXpRuntimeService",
                "levelup-xp-stat",
                character,
                (int)StatIds.xp,
                cumulativeXp,
                "StatMessage",
                "unknown=0");
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.xp, cumulativeXp);
            LogXpWireFeedbackOutbound(
                "CombatXpRuntimeService",
                "levelup-xp-feedback",
                character,
                LevelUpFeedbackCategoryId,
                XpFeedbackMessageId);
            FeedbackMessageHandler.Default.Send(
                character,
                LevelUpFeedbackCategoryId,
                XpFeedbackMessageId);
        }

        private static void SendClientStatWithUnknown(
            IZoneClient client,
            ICharacter character,
            CharacterStat stat,
            uint value,
            byte unknown,
            string stage = "stat-unknown")
        {
            LogXpWireOutbound(
                "CombatXpRuntimeService",
                stage,
                character,
                (int)stat,
                value,
                "StatMessage",
                "unknown=" + unknown.ToString(CultureInfo.InvariantCulture));
            client.SendCompressed(
                new StatMessage
                {
                    Identity = character.Identity,
                    Unknown = unknown,
                    Stats =
                        new[]
                        {
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = stat,
                                Value2 = value
                            }
                        }
                });
        }

        private static int GetCurrentLevel(ICharacter character)
        {
            uint raw = character.Stats[StatIds.level].BaseValue;
            if (raw == UnsetStatSentinel || raw == 0 || raw > MaxLevel)
            {
                return 1;
            }

            return (int)raw;
        }

        private static uint AddClamped(uint value, int delta)
        {
            uint safeDelta = (uint)Math.Max(0, delta);
            if (value > uint.MaxValue - safeDelta)
            {
                return uint.MaxValue;
            }

            return value + safeDelta;
        }

        private static void LogXpRewardSource(ICharacter attacker, ICharacter target, int xpReward)
        {
            string source;
            CombatTestMobArchetype.Entry archetype;
            if (CombatTestMobArchetype.TryGetByName(target.Name, out archetype) && archetype.XpReward > 0)
            {
                source = "archetype:" + target.Name + "=" + archetype.XpReward.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                int targetXp = (int)target.Stats[StatIds.xp].BaseValue;
                if (targetXp > 0)
                {
                    source = "target-stat-xp:" + targetXp.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    source = "fallback-robot:" + CapturedMalfunctioningCleaningRobotXp.ToString(CultureInfo.InvariantCulture);
                }
            }

            int attackerLevel = GetCurrentLevel(attacker);
            int targetLevel = Math.Max(1, (int)target.Stats[StatIds.level].BaseValue);
            if (attackerLevel - targetLevel >= GreyMobMinLevelAdvantage)
            {
                source += " grey-cap-applied final=" + xpReward.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                source += " final=" + xpReward.ToString(CultureInfo.InvariantCulture);
            }

            LogXpTrace(attacker, "reward-source", source + " target=\"" + (target.Name ?? string.Empty) + "\"");
        }

        private static void SetXpStat(ICharacter character, StatIds statId, uint newValue, string source)
        {
            if (character == null)
            {
                return;
            }

            uint before = NormalizeStatValue(character.Stats[statId].BaseValue);
            character.Stats[statId].Set(newValue);
            if (before == newValue)
            {
                return;
            }

            LogXpStatChange(character, source, statId, before, newValue);
        }

        private static void LogXpStatChange(
            ICharacter character,
            string source,
            StatIds statId,
            uint before,
            uint after)
        {
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} stage=stat-set source={1} char={2} stat={3} statId={4} before={5} after={6} delta={7}",
                    XpTracePrefix,
                    source,
                    character.Identity,
                    GetXpStatName(statId),
                    (int)statId,
                    before,
                    after,
                    (long)after - before));
        }

        private static void WriteXpStatsToDb(ICharacter character, string source)
        {
            if (character == null)
            {
                return;
            }

            LogXpTrace(
                character,
                "db-write-before",
                "source=" + source
                + " snapshot="
                + BuildXpStatSnapshot(character));

            character.Stats.Write();

            LogXpTrace(character, "db-write-after", "source=" + source + " persisted=true");
        }

        private static string BuildXpStatSnapshot(ICharacter character)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "level54={0} xp52={1} unsaved592={2} lastsave372={3} saved334={4} next350={5} lastxp57={6}",
                character.Stats[StatIds.level].BaseValue,
                character.Stats[StatIds.xp].BaseValue,
                character.Stats[StatIds.unsavedxp].BaseValue,
                character.Stats[StatIds.lastsavexp].BaseValue,
                character.Stats[StatIds.savedxp].BaseValue,
                character.Stats[StatIds.nextxp].BaseValue,
                character.Stats[StatIds.lastxp].BaseValue);
        }

        private static string GetXpStatName(StatIds statId)
        {
            switch (statId)
            {
                case StatIds.xp:
                    return "XP";
                case StatIds.ip:
                    return "IP";
                case StatIds.level:
                    return "Level";
                case StatIds.lastxp:
                    return "LastXP";
                case StatIds.savedxp:
                    return "SavedXP";
                case StatIds.nextxp:
                    return "NextXP";
                case StatIds.lastsavexp:
                    return "LastSaveXP";
                case StatIds.unsavedxp:
                    return "UnsavedXP";
                default:
                    return statId.ToString();
            }
        }

        private static void LogXpTrace(ICharacter character, string stage, string details)
        {
            if (character == null)
            {
                return;
            }

            int level = GetCurrentLevel(character);
            uint levelRaw = character.Stats[StatIds.level].BaseValue;
            uint xp = character.Stats[StatIds.xp].BaseValue;
            uint unsaved = character.Stats[StatIds.unsavedxp].BaseValue;
            uint lastsave = character.Stats[StatIds.lastsavexp].BaseValue;
            uint saved = character.Stats[StatIds.savedxp].BaseValue;
            uint next = character.Stats[StatIds.nextxp].BaseValue;
            uint lastxp = character.Stats[StatIds.lastxp].BaseValue;
            uint progress = GetBarProgress(character);
            int nextRequired = GetNextXpRequiredForLevel(level);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} stage={1} char={2} level54={3} levelRaw={4} xp52={5} unsaved592={6} lastsave372={7} saved334={8} next350={9} lastxp57={10} progress={11} nextRequired={12} bar={13}/{14} {15}",
                    XpTracePrefix,
                    stage,
                    character.Identity,
                    level,
                    levelRaw,
                    xp,
                    unsaved,
                    lastsave,
                    saved,
                    next,
                    lastxp,
                    progress,
                    nextRequired,
                    progress,
                    nextRequired,
                    details ?? string.Empty));
        }

        internal static bool IsXpWireStatId(int statId)
        {
            return statId == (int)StatIds.xp
                   || statId == (int)StatIds.ip
                   || statId == (int)StatIds.level
                   || statId == (int)StatIds.lastxp
                   || statId == (int)StatIds.savedxp
                   || statId == (int)StatIds.nextxp
                   || statId == (int)StatIds.lastsavexp
                   || statId == (int)StatIds.unsavedxp;
        }

        internal static bool IsXpFeedbackMessage(int categoryId, int messageId)
        {
            return categoryId == LevelUpFeedbackCategoryId && messageId == XpFeedbackMessageId;
        }

        internal static void LogXpWireOutbound(
            string source,
            string stage,
            ICharacter character,
            int statId,
            uint value,
            string wireKind,
            string details = "")
        {
            if (character == null || !IsXpWireStatId(statId))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} source={1} stage={2} char={3} wire={4} statId={5} stat={6} value={7} {8}",
                    XpWireTracePrefix,
                    source ?? string.Empty,
                    stage ?? string.Empty,
                    character.Identity,
                    wireKind ?? string.Empty,
                    statId,
                    GetXpStatName((StatIds)statId),
                    value,
                    details ?? string.Empty));
        }

        internal static void LogXpWireFeedbackOutbound(
            string source,
            string stage,
            ICharacter character,
            int categoryId,
            int messageId,
            string details = "")
        {
            if (character == null || !IsXpFeedbackMessage(categoryId, messageId))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} source={1} stage={2} char={3} wire=FeedbackMessage category={4} messageId={5} {6}",
                    XpWireTracePrefix,
                    source ?? string.Empty,
                    stage ?? string.Empty,
                    character.Identity,
                    categoryId,
                    messageId,
                    details ?? string.Empty));
        }

        internal static void LogXpWireNewLevel(
            string source,
            string stage,
            ICharacter character,
            NewLevelMessage message)
        {
            if (character == null || message == null)
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} source={1} stage={2} char={3} wire=NewLevel level={4} ip={5} xp={6} lastSaveXp={7} nextLevelXp={8} lastXp={9}",
                    XpWireTracePrefix,
                    source ?? string.Empty,
                    stage ?? string.Empty,
                    character.Identity,
                    message.Level,
                    message.Ip,
                    message.Xp,
                    message.LastSaveXp,
                    message.NextLevelXp,
                    message.LastXp));
        }

        internal static void LogXpWireSnapshot(
            ICharacter character,
            string source,
            string stage,
            string details = "")
        {
            if (character == null)
            {
                return;
            }

            int level = GetCurrentLevel(character);
            uint levelRaw = character.Stats[StatIds.level].BaseValue;
            uint xp = character.Stats[StatIds.xp].BaseValue;
            uint unsaved = character.Stats[StatIds.unsavedxp].BaseValue;
            uint lastsave = character.Stats[StatIds.lastsavexp].BaseValue;
            uint saved = character.Stats[StatIds.savedxp].BaseValue;
            uint next = character.Stats[StatIds.nextxp].BaseValue;
            uint lastxp = character.Stats[StatIds.lastxp].BaseValue;
            uint progress = GetBarProgress(character);
            int nextRequired = GetNextXpRequiredForLevel(level);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} source={1} stage={2} char={3} level54={4} levelRaw={5} xp52={6} unsaved592={7} lastsave372={8} saved334={9} next350={10} lastxp57={11} progress={12} nextRequired={13} bar={14}/{15} {16}",
                    XpWireTracePrefix,
                    source ?? string.Empty,
                    stage ?? string.Empty,
                    character.Identity,
                    level,
                    levelRaw,
                    xp,
                    unsaved,
                    lastsave,
                    saved,
                    next,
                    lastxp,
                    progress,
                    nextRequired,
                    progress,
                    nextRequired,
                    details ?? string.Empty));
        }
    }
}
