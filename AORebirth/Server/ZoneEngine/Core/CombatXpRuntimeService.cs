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
        private const int MaxLevel = 220;
        private const int MaxRubikaLevel = 200;
        /// <summary>First level that uses Shadowknowledge (SK) instead of XP for progression.</summary>
        private const int ShadowLevelFloor = 200;
        /// <summary>AO-Universe chart convention: SK * 1000 ≈ XP for comparison.</summary>
        private const int SkToXpFactor = 1000;
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
            if (attacker == null || target == null)
            {
                return;
            }

            ICharacter xpRecipient = ResolveXpRecipient(attacker);
            if (xpRecipient == null || !(xpRecipient.Controller is Controllers.PlayerController))
            {
                return;
            }

            IZoneClient client = xpRecipient.Controller.Client;
            if (client == null)
            {
                return;
            }

            int xpReward = CalculateCombatXpReward(xpRecipient, target);
            if (xpReward <= 0)
            {
                LogXpTrace(xpRecipient, "kill-skip", "reason=zero-reward");
                AlienXpRuntimeService.AwardAlienXpOnKill(attacker, target);
                return;
            }

            LogXpRewardSource(xpRecipient, target, xpReward);

            int levelBefore = GetCurrentLevel(xpRecipient);
            if (IsShadowLevelProgression(levelBefore))
            {
                AwardCombatSk(xpRecipient, client, xpReward, attacker.Identity.ToString());
                AlienXpRuntimeService.AwardAlienXpOnKill(attacker, target);
                return;
            }

            if (levelBefore >= MaxLevel)
            {
                LogXpTrace(xpRecipient, "kill-skip", "reason=max-level-220");
                AlienXpRuntimeService.AwardAlienXpOnKill(attacker, target);
                return;
            }

            LogXpTrace(
                xpRecipient,
                "kill-start",
                "reward=" + xpReward.ToString(CultureInfo.InvariantCulture)
                + " sourceAttacker=" + attacker.Identity);

            uint floorXp = GetCumulativeXpForLevelStart(levelBefore);
            uint progressBefore = GetBarProgress(xpRecipient);
            uint deathPoolBefore = GetDeathXpPool(xpRecipient);
            int recoveredFromPool = 0;
            if (levelBefore < MaxLevel && deathPoolBefore > 0)
            {
                // Mike: each kill recovers 5% of remaining UnsavedXP death pool + mob XP.
                recoveredFromPool = (int)(deathPoolBefore * 5u / 100u);
                if (recoveredFromPool <= 0 && deathPoolBefore > 0)
                {
                    recoveredFromPool = 1;
                }

                if ((uint)recoveredFromPool > deathPoolBefore)
                {
                    recoveredFromPool = (int)deathPoolBefore;
                }
            }

            int totalGain = xpReward + recoveredFromPool;
            uint newProgress = AddClamped(progressBefore, totalGain);
            uint deathPoolAfter = deathPoolBefore > (uint)recoveredFromPool
                ? deathPoolBefore - (uint)recoveredFromPool
                : 0;

            SetXpStat(xpRecipient, StatIds.xp, floorXp + newProgress, "kill-add-cumulative");
            SetXpStat(xpRecipient, StatIds.lastxp, (uint)totalGain, "kill-add-lastxp");
            if (deathPoolAfter > 0)
            {
                // Keep UnsavedXP as remaining death pool until drained.
                SetXpStat(xpRecipient, StatIds.unsavedxp, deathPoolAfter, "kill-death-pool-remain");
            }
            else
            {
                // Mirror bar progress once pool is empty (client UnsavedXP wire).
                SetXpStat(xpRecipient, StatIds.unsavedxp, newProgress, "kill-add-unsaved");
            }

            EnsureLevelXpThresholds(xpRecipient, "kill-add-thresholds");

            LogXpTrace(
                xpRecipient,
                "kill-after-add",
                "progressBefore=" + progressBefore.ToString(CultureInfo.InvariantCulture)
                + " progressAfter=" + newProgress.ToString(CultureInfo.InvariantCulture)
                + " poolBefore=" + deathPoolBefore.ToString(CultureInfo.InvariantCulture)
                + " poolRecover=" + recoveredFromPool.ToString(CultureInfo.InvariantCulture)
                + " poolAfter=" + deathPoolAfter.ToString(CultureInfo.InvariantCulture)
                + " cumulative=" + xpRecipient.Stats[StatIds.xp].BaseValue.ToString(CultureInfo.InvariantCulture));

            bool leveledUp = ApplyPendingLevelUps(xpRecipient, levelBefore);

            if (sendFeedback != null)
            {
                LogXpTrace(xpRecipient, "xp-chat-deferred", "source=captured-feedback-message");
            }

            if (leveledUp)
            {
                SendLevelUpPreFeedbackPackets(client, xpRecipient, levelBefore);
                PersistLevelStat(xpRecipient);
            }
            else
            {
                if (GetBarProgress(xpRecipient) >= (uint)GetNextXpRequiredForLevel(levelBefore))
                {
                    LogXpTrace(
                        xpRecipient,
                        "levelup-missed",
                        "reason=progress-met-but-ApplyPendingLevelUps-returned-false"
                        + " progress=" + GetBarProgress(xpRecipient).ToString(CultureInfo.InvariantCulture)
                        + " required=" + GetNextXpRequiredForLevel(levelBefore).ToString(CultureInfo.InvariantCulture));
                }

                SendNormalKillXpPacket(client, xpRecipient);
            }

            ClearManualXpWireStatChangedFlags(xpRecipient, leveledUp);
            WriteXpStatsToDb(xpRecipient, leveledUp ? "kill-complete-levelup" : "kill-complete");

            AlienXpRuntimeService.AwardAlienXpOnKill(attacker, target);
            if (leveledUp)
            {
                // Banked AIXP may unlock when Rubi-Ka level crosses an AI gate.
                AlienXpRuntimeService.TryApplyBankedAlienLevelUps(xpRecipient);
            }

            LogXpTrace(
                xpRecipient,
                leveledUp ? "kill-complete-levelup" : "kill-complete",
                "levelBefore=" + levelBefore.ToString(CultureInfo.InvariantCulture)
                + " levelAfter=" + GetCurrentLevel(xpRecipient).ToString(CultureInfo.InvariantCulture)
                + " leveledUp=" + leveledUp.ToString(CultureInfo.InvariantCulture)
                + " wire=" + (leveledUp ? "levelup-packets" : "xp-only"));
        }

        /// <summary>
        /// XP still needed to finish the current level bar (0 at max Rubi-Ka level).
        /// </summary>
        internal static int GetXpNeededForNextLevel(ICharacter character)
        {
            if (character == null)
            {
                return 0;
            }

            int level = GetCurrentLevel(character);
            int required = GetNextXpRequiredForLevel(level);
            if (required <= 0)
            {
                return 0;
            }

            uint progress = GetBarProgress(character);
            if (progress >= (uint)required)
            {
                return required;
            }

            return required - (int)progress;
        }

        /// <summary>
        /// After a GM <c>/set sk</c> (or similar) writes cumulative SK, apply any earned
        /// shadowlevels and refresh the SK bar wire. Plain stat set does not level by itself.
        /// </summary>
        /// <returns>True when level increased.</returns>
        internal static bool ReconcileAfterManualSkSet(ICharacter character)
        {
            if (character == null || !(character.Controller is Controllers.PlayerController))
            {
                return false;
            }

            IZoneClient client = character.Controller.Client;
            int levelBefore = GetCurrentLevel(character);
            if (levelBefore < ShadowLevelFloor)
            {
                LogXpTrace(
                    character,
                    "gm-sk-set-skip",
                    "reason=below-shadow-floor level=" + levelBefore.ToString(CultureInfo.InvariantCulture));
                if (client != null)
                {
                    SendSkProgressPackets(client, character);
                }

                ClearStatChangedFlag(character, StatIds.sk);
                ClearStatChangedFlag(character, StatIds.nextsk);
                WriteXpStatsToDb(character, "gm-sk-set-below-floor");
                return false;
            }

            if (levelBefore >= MaxLevel)
            {
                if (client != null)
                {
                    StatMessageHandler.Default.SendSingle(character, (int)StatIds.nextsk, 0);
                }

                ClearStatChangedFlag(character, StatIds.sk);
                ClearStatChangedFlag(character, StatIds.nextsk);
                WriteXpStatsToDb(character, "gm-sk-set-max");
                return false;
            }

            bool leveledUp = ApplyPendingSkLevelUps(character, levelBefore);
            if (leveledUp && client != null)
            {
                SendLevelUpPreFeedbackPackets(client, character, levelBefore);
                PersistLevelStat(character);
            }

            if (client != null)
            {
                SendSkProgressPackets(client, character);
            }

            ClearManualXpWireStatChangedFlags(character, leveledUp);
            ClearStatChangedFlag(character, StatIds.sk);
            ClearStatChangedFlag(character, StatIds.nextsk);
            WriteXpStatsToDb(character, leveledUp ? "gm-sk-set-levelup" : "gm-sk-set");
            LogXpTrace(
                character,
                leveledUp ? "gm-sk-set-levelup" : "gm-sk-set",
                "levelBefore=" + levelBefore.ToString(CultureInfo.InvariantCulture)
                + " levelAfter=" + GetCurrentLevel(character).ToString(CultureInfo.InvariantCulture)
                + " sk=" + NormalizeStatValue(character.Stats[StatIds.sk].BaseValue)
                    .ToString(CultureInfo.InvariantCulture));
            return leveledUp;
        }

        /// <summary>
        /// After a GM <c>/set xp</c>, apply any earned Rubi-Ka levels and refresh the XP bar.
        /// </summary>
        internal static bool ReconcileAfterManualXpSet(ICharacter character)
        {
            if (character == null || !(character.Controller is Controllers.PlayerController))
            {
                return false;
            }

            IZoneClient client = character.Controller.Client;
            int levelBefore = GetCurrentLevel(character);
            if (levelBefore >= ShadowLevelFloor)
            {
                // At 200+ progression is SK; XP set does not advance shadowlevels.
                return ReconcileAfterManualSkSet(character);
            }

            bool leveledUp = ApplyPendingLevelUps(character, levelBefore);
            if (leveledUp && client != null)
            {
                SendLevelUpPreFeedbackPackets(client, character, levelBefore);
                PersistLevelStat(character);
            }

            ClearManualXpWireStatChangedFlags(character, leveledUp);
            WriteXpStatsToDb(character, leveledUp ? "gm-xp-set-levelup" : "gm-xp-set");
            if (client != null)
            {
                SyncXpBarStatsOnLogin(character);
            }

            return leveledUp;
        }

        /// <summary>
        /// After a GM <c>/set level</c>. Jumping to 200+ leaves NextXP=0 and never syncs
        /// SK/NextSK — client shows Experience 0/0. Clamp level, set SK/XP floors, resync bar.
        /// </summary>
        internal static void ReconcileAfterManualLevelSet(ICharacter character)
        {
            if (character == null || !(character.Controller is Controllers.PlayerController))
            {
                return;
            }

            int level = GetCurrentLevel(character);
            if (level < 1)
            {
                level = 1;
                SetXpStat(character, StatIds.level, 1, "gm-level-set-clamp-min");
            }
            else if (level > MaxLevel)
            {
                level = MaxLevel;
                SetXpStat(character, StatIds.level, (uint)MaxLevel, "gm-level-set-clamp-max");
            }

            // Force NextXP/NextSK Value cache to match the new level.
            character.Stats[StatIds.nextxp].ReCalculate = true;
            character.Stats[StatIds.nextsk].ReCalculate = true;
            int _nx = character.Stats[StatIds.nextxp].Value;
            int _ns = character.Stats[StatIds.nextsk].Value;

            if (level >= ShadowLevelFloor)
            {
                SetXpStat(character, StatIds.nextxp, 0, "gm-level-set-clear-nextxp");
                uint skFloor = GetCumulativeSkForLevelStart(level);
                uint sk = NormalizeStatValue(character.Stats[StatIds.sk].BaseValue);
                if (sk < skFloor)
                {
                    SetXpStat(character, StatIds.sk, skFloor, "gm-level-set-sk-floor");
                }

                // RK XP bar is unused at 200+; keep cumulative XP at the level-200 ceiling.
                uint xpCap = GetCumulativeXpForLevelStart(MaxRubikaLevel);
                SetXpStat(character, StatIds.xp, xpCap, "gm-level-set-xp-cap");
                SetXpStat(character, StatIds.unsavedxp, 0, "gm-level-set-clear-unsaved");
            }
            else
            {
                uint floorXp = GetCumulativeXpForLevelStart(level);
                uint xp = NormalizeStatValue(character.Stats[StatIds.xp].BaseValue);
                if (xp < floorXp)
                {
                    SetXpStat(character, StatIds.xp, floorXp, "gm-level-set-xp-floor");
                    SetXpStat(character, StatIds.unsavedxp, 0, "gm-level-set-unsaved-floor");
                }

                EnsureLevelXpThresholds(character, "gm-level-set-thresholds");
            }

            character.CalculateSkills();
            int maxLife = Math.Max(1, character.Stats[StatIds.life].Value);
            int maxNano = Math.Max(0, character.Stats[StatIds.maxnanoenergy].Value);
            character.Stats[StatIds.health].Set((uint)maxLife);
            character.Stats[StatIds.currentnano].Set((uint)maxNano);

            PersistLevelStat(character);
            WriteXpStatsToDb(character, "gm-level-set");

            IZoneClient client = character.Controller.Client;
            if (client != null)
            {
                // Same post-FullCharacter bar wire used on login (SK path at 200-219).
                // NOTE: SyncXpBarStatsOnLogin skips NewLevelMessage for shadowlevels (200+).
                // Team Recruit XP warn uses Level from NewLevel — without it, UI can show 200
                // while Recruit still treats the old level (200→25 = silent; 25→200 = warn).
                SyncXpBarStatsOnLogin(character);
                SendManualLevelNewLevelMessage(client, character, level);
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.level, (uint)level);
                StatMessageHandler.Default.AnnounceSingle(character, (int)StatIds.level, (uint)level);
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.life, (uint)maxLife);
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.health, (uint)maxLife);
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.maxnanoenergy, (uint)maxNano);
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.currentnano, (uint)maxNano);
                try
                {
                    Packets.SimpleCharFullUpdate.SendToPlayfield(client);
                }
                catch
                {
                    // SCFU refresh is best-effort so nearby clients see the new Level.
                }
            }

            ClearManualXpWireStatChangedFlags(character, true);
            ClearStatChangedFlag(character, StatIds.sk);
            ClearStatChangedFlag(character, StatIds.nextsk);
            ClearStatChangedFlag(character, StatIds.nextxp);
            LogXpTrace(
                character,
                "gm-level-set",
                "level=" + level.ToString(CultureInfo.InvariantCulture)
                + " nextXp=" + _nx.ToString(CultureInfo.InvariantCulture)
                + " nextSk=" + _ns.ToString(CultureInfo.InvariantCulture)
                + " sk=" + NormalizeStatValue(character.Stats[StatIds.sk].BaseValue)
                    .ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// GM <c>/set level</c> must always push one <see cref="NewLevelMessage"/> even at 200+,
        /// because <see cref="SendLoginXpBarSync"/> skips NewLevel on the SK path.
        /// </summary>
        private static void SendManualLevelNewLevelMessage(
            IZoneClient client,
            ICharacter character,
            int level)
        {
            if (client == null || character == null || level < 1)
            {
                return;
            }

            uint floorXp = GetCumulativeXpForLevelStart(Math.Min(level, MaxRubikaLevel));
            uint cumulativeXp = NormalizeStatValue(character.Stats[StatIds.xp].BaseValue);
            if (level >= ShadowLevelFloor)
            {
                cumulativeXp = GetCumulativeXpForLevelStart(MaxRubikaLevel);
            }

            uint nextLevelCumulative = level >= MaxRubikaLevel
                ? 0
                : GetCumulativeXpForLevelStart(level + 1);

            var newLevelMessage = new NewLevelMessage
                                  {
                                      Identity = character.Identity,
                                      Unknown = 0,
                                      Level = level,
                                      Ip = Math.Max(0, character.Stats[StatIds.ip].Value),
                                      Xp = (int)cumulativeXp,
                                      LastSaveXp = (int)floorXp,
                                      NextLevelXp = (int)nextLevelCumulative,
                                      Unknown1 = 0,
                                      Unknown2 = CapturedNewLevelUnknown2,
                                      LastXp = Math.Max(0, character.Stats[StatIds.lastxp].Value)
                                  };
            LogXpWireNewLevel(
                "CombatXpRuntimeService",
                "gm-level-set-newlevel",
                character,
                newLevelMessage);
            client.SendCompressed(newLevelMessage);
            LogXpTrace(
                character,
                "gm-level-set-newlevel",
                "level=" + level.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Non-combat XP grant using the same cumulative/level-up wire as kills.
        /// </summary>
        internal static bool AwardDirectXp(ICharacter character, int xpReward, string source)
        {
            if (character == null || xpReward <= 0
                || !(character.Controller is Controllers.PlayerController))
            {
                return false;
            }

            IZoneClient client = character.Controller.Client;
            if (client == null)
            {
                return false;
            }

            int levelBefore = GetCurrentLevel(character);
            if (levelBefore >= MaxLevel)
            {
                return false;
            }

            string sourceTag = string.IsNullOrWhiteSpace(source) ? "direct" : source.Trim();
            if (IsShadowLevelProgression(levelBefore))
            {
                return AwardDirectSk(character, client, xpReward, sourceTag);
            }

            uint floorXp = GetCumulativeXpForLevelStart(levelBefore);
            uint progressBefore = GetBarProgress(character);
            uint newProgress = AddClamped(progressBefore, xpReward);

            SetXpStat(character, StatIds.xp, floorXp + newProgress, sourceTag + "-add-cumulative");
            SetXpStat(character, StatIds.lastxp, (uint)xpReward, sourceTag + "-add-lastxp");
            SetXpStat(character, StatIds.unsavedxp, newProgress, sourceTag + "-add-unsaved");
            EnsureLevelXpThresholds(character, sourceTag + "-thresholds");

            bool leveledUp = ApplyPendingLevelUps(character, levelBefore);
            if (leveledUp)
            {
                SendLevelUpPreFeedbackPackets(client, character, levelBefore);
                PersistLevelStat(character);
            }
            else
            {
                SendNormalKillXpPacket(client, character);
            }

            ClearManualXpWireStatChangedFlags(character, leveledUp);
            WriteXpStatsToDb(character, leveledUp ? sourceTag + "-levelup" : sourceTag + "-complete");
            LogXpTrace(
                character,
                leveledUp ? sourceTag + "-levelup" : sourceTag + "-complete",
                "reward=" + xpReward.ToString(CultureInfo.InvariantCulture)
                + " levelBefore=" + levelBefore.ToString(CultureInfo.InvariantCulture)
                + " levelAfter=" + GetCurrentLevel(character).ToString(CultureInfo.InvariantCulture));
            return true;
        }

        /// <summary>
        /// On death /terminate: clip XP to Insurance SavedXP watermark (or level floor), and for
        /// levels under 220 move the lost amount into UnsavedXP as a death recovery pool.
        /// Each later kill recovers 5% of that pool + mob XP until the pool hits 0.
        /// </summary>
        internal static void ApplyDeathUninsuredXpLoss(ICharacter character)
        {
            if (character == null || !(character.Controller is Controllers.PlayerController))
            {
                return;
            }

            int level = GetCurrentLevel(character);
            uint floor = GetCumulativeXpForLevelStart(level);
            uint currentXp = NormalizeStatValue(character.Stats[StatIds.xp].BaseValue);
            uint watermark = NormalizeStatValue(character.Stats[StatIds.savedxp].BaseValue);
            uint existingPool = GetDeathXpPool(character);

            uint protectedXp = watermark > floor ? watermark : floor;
            if (protectedXp > currentXp)
            {
                protectedXp = currentXp;
            }

            uint lost = currentXp > protectedXp ? currentXp - protectedXp : 0;
            uint newProgress = protectedXp >= floor ? protectedXp - floor : 0;
            uint newPool = existingPool;
            if (level < MaxLevel && lost > 0)
            {
                newPool = AddClamped(existingPool, (int)lost);
            }

            LogXpTrace(
                character,
                "death-xp-loss",
                "level=" + level.ToString(CultureInfo.InvariantCulture)
                + " xpBefore=" + currentXp.ToString(CultureInfo.InvariantCulture)
                + " watermark=" + watermark.ToString(CultureInfo.InvariantCulture)
                + " floor=" + floor.ToString(CultureInfo.InvariantCulture)
                + " xpAfter=" + protectedXp.ToString(CultureInfo.InvariantCulture)
                + " lost=" + lost.ToString(CultureInfo.InvariantCulture)
                + " poolAfter=" + newPool.ToString(CultureInfo.InvariantCulture));

            SetXpStat(character, StatIds.xp, protectedXp, "death-xp-loss");
            if (newPool > 0)
            {
                SetXpStat(character, StatIds.unsavedxp, newPool, "death-xp-pool");
            }
            else
            {
                SetXpStat(character, StatIds.unsavedxp, newProgress, "death-xp-progress");
            }

            IZoneClient client = character.Controller.Client;
            if (client != null)
            {
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.xp, protectedXp);
                StatMessageHandler.Default.SendSingle(
                    character,
                    (int)StatIds.unsavedxp,
                    newPool > 0 ? newPool : newProgress);
            }

            WriteXpStatsToDb(character, "death-xp-loss");
        }

        /// <summary>
        /// Insurance Terminal SaveChar: move current XP into SavedXP watermark and current SK into
        /// LastSK. UnsavedXP death pool is kept for kill recovery; otherwise UnsavedXP mirrors bar
        /// progress (same convention as kill/login). Capture: 20260716-141512.
        /// </summary>
        /// <returns>SavedXP watermark value used in the "XP saved" feedback.</returns>
        internal static uint ApplyInsuranceTerminalSave(ICharacter character, out uint savedSk)
        {
            savedSk = 0;
            if (character == null || !(character.Controller is Controllers.PlayerController))
            {
                return 0;
            }

            uint cumulativeXp = NormalizeStatValue(character.Stats[StatIds.xp].BaseValue);
            uint deathPool = GetDeathXpPool(character);
            int level = GetCurrentLevel(character);
            uint floor = GetCumulativeXpForLevelStart(level);
            uint progress = cumulativeXp >= floor ? cumulativeXp - floor : 0;

            // Unsaved/at-risk XP → SavedXP (death watermark).
            SetXpStat(character, StatIds.savedxp, cumulativeXp, "insurance-save-watermark");

            if (deathPool > 0)
            {
                SetXpStat(character, StatIds.unsavedxp, deathPool, "insurance-save-keep-death-pool");
            }
            else
            {
                SetXpStat(character, StatIds.unsavedxp, progress, "insurance-save-unsaved-progress");
            }

            // SK → LastSK (saved Shadowknowledge place).
            savedSk = NormalizeStatValue(character.Stats[StatIds.sk].BaseValue);
            SetXpStat(character, StatIds.lastsk, savedSk, "insurance-save-lastsk");

            LogXpTrace(
                character,
                "insurance-save",
                "savedXp=" + cumulativeXp.ToString(CultureInfo.InvariantCulture)
                + " progress=" + progress.ToString(CultureInfo.InvariantCulture)
                + " deathPool=" + deathPool.ToString(CultureInfo.InvariantCulture)
                + " savedSk=" + savedSk.ToString(CultureInfo.InvariantCulture));

            WriteXpStatsToDb(character, "insurance-save");

            IZoneClient client = character.Controller.Client;
            if (client != null)
            {
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.savedxp, cumulativeXp);
                StatMessageHandler.Default.SendSingle(
                    character,
                    (int)StatIds.unsavedxp,
                    deathPool > 0 ? deathPool : progress);
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.lastsk, savedSk);
            }

            // Clear manual XP-wire changed flags (same as the kill path) so later CharDCMove stat
            // flushes do not re-push XP/SK every frame — that spammed blank reward chat lines while
            // standing on the garden save pad.
            ClearManualXpWireStatChangedFlags(character, false);
            ClearStatChangedFlag(character, StatIds.lastsk);
            ClearStatChangedFlag(character, StatIds.sk);

            return cumulativeXp;
        }

        /// <summary>Save-feedback: level 201+ show SK only (classic shadowlevels).</summary>
        private const int ShadowLevelStart = 201;

        /// <summary>
        /// Save-reward feedback text shared by the insurance terminal and the garden save pad.
        /// - Level 201+ (Shadowlevels) earn SK, not XP, so never show the huge cumulative XP total.
        /// - Level 220 (max) earns neither XP nor SK, so only confirm the store with no number.
        /// </summary>
        internal static string BuildSaveRewardText(int level, uint savedXp, uint savedSk)
        {
            if (level >= MaxLevel)
            {
                return "Character stored.";
            }

            if (level >= ShadowLevelStart)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Character stored. {0} Shadowknowledge saved.",
                    savedSk);
            }

            if (savedSk > 0)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Character stored. {0} XP saved. {1} Shadowknowledge saved.",
                    savedXp,
                    savedSk);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "Character stored. {0} XP saved.",
                savedXp);
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
            AlienXpRuntimeService.TryApplyBankedAlienLevelUps(character);
            LogXpWireSnapshot(character, "CombatXpRuntimeService", "login-prepare-after");
        }

        internal static bool ReconcilePersistedMissionXpRewardState(ICharacter character, int levelBefore)
        {
            if (character == null || !(character.Controller is Controllers.PlayerController))
            {
                return false;
            }

            LogXpWireSnapshot(character, "CombatXpRuntimeService", "mission-reward-before");
            NormalizeXpStatsFromPersistedLevel(character);

            int levelAfter = GetCurrentLevel(character);
            bool leveledUp = levelAfter > levelBefore;
            if (leveledUp)
            {
                PersistLevelStat(character);
            }

            ClearManualXpWireStatChangedFlags(character, leveledUp);
            WriteXpStatsToDb(character, leveledUp ? "mission-reward-levelup" : "mission-reward-reconcile");

            LogXpWireSnapshot(
                character,
                "CombatXpRuntimeService",
                "mission-reward-state-after",
                "levelBefore=" + levelBefore.ToString(CultureInfo.InvariantCulture)
                + " levelAfter=" + levelAfter.ToString(CultureInfo.InvariantCulture));
            return leveledUp;
        }

        internal static bool TryProjectPersistedMissionXpReward(ICharacter character, int levelBefore)
        {
            if (character == null
                || !(character.Controller is Controllers.PlayerController)
                || character.Controller.Client == null)
            {
                return false;
            }

            int levelAfter = GetCurrentLevel(character);
            int expectedLevelAfter = Math.Min(MaxRubikaLevel, levelBefore + 1);
            if (levelAfter < expectedLevelAfter)
            {
                return false;
            }

            if (levelAfter > expectedLevelAfter)
            {
                LogXpWireSnapshot(
                    character,
                    "CombatXpRuntimeService",
                    "mission-reward-projection-skipped-stale",
                    "levelBefore=" + levelBefore.ToString(CultureInfo.InvariantCulture)
                    + " expectedLevelAfter=" + expectedLevelAfter.ToString(CultureInfo.InvariantCulture)
                    + " currentLevel=" + levelAfter.ToString(CultureInfo.InvariantCulture));
                return true;
            }

            SendLevelUpPreFeedbackPackets(
                character.Controller.Client,
                character,
                levelBefore,
                expectedLevelAfter);
            AlienXpRuntimeService.TryApplyBankedAlienLevelUps(character);
            LogXpWireSnapshot(
                character,
                "CombatXpRuntimeService",
                "mission-reward-projected",
                "levelBefore=" + levelBefore.ToString(CultureInfo.InvariantCulture)
                + " levelAfter=" + levelAfter.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        internal static void SendLoginXpBarSync(ICharacter character)
        {
            if (character == null || character.Controller?.Client == null)
            {
                return;
            }

            IZoneClient client = character.Controller.Client;
            int level = GetCurrentLevel(character);

            // Level 200-219: client XP tooltip becomes "Experience 0/0" unless SK + NextSK
            // are wired. Skip the RK XP NewLevel replay and sync Shadowknowledge instead.
            if (IsShadowLevelProgression(level))
            {
                EnsureSkFloorOnLogin(character);
                // Experience bar reads NextXP; keep it 0 so the client uses SK/NextSK instead.
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.nextxp, 0);
                SendSkProgressPackets(client, character);
                // Prevent a later SendChangedStats from re-pushing NextSK BaseValue 0 (0/0 bar).
                ClearStatChangedFlag(character, StatIds.nextxp);
                ClearStatChangedFlag(character, StatIds.sk);
                ClearStatChangedFlag(character, StatIds.nextsk);
                ClearStatChangedFlag(character, StatIds.lastsk);
                LogXpTrace(
                    character,
                    "login-bar-sync-sk",
                    "level=" + level.ToString(CultureInfo.InvariantCulture)
                    + " sk=" + NormalizeStatValue(character.Stats[StatIds.sk].BaseValue)
                        .ToString(CultureInfo.InvariantCulture)
                    + " nextSk=" + GetNextSkRequiredForLevel(level).ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (level >= MaxLevel)
            {
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.nextxp, 0);
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.nextsk, 0);
                ClearStatChangedFlag(character, StatIds.nextxp);
                ClearStatChangedFlag(character, StatIds.nextsk);
                LogXpTrace(character, "login-bar-sync-max", "level=220 nextXp=0 nextSk=0");
                return;
            }

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
            uint nextLevelCumulative = level >= MaxRubikaLevel
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

        /// <summary>
        /// Level 200 SK floor is 0; higher shadowlevels must not sit below their table floor
        /// or the client bar shows garbage until the first SK award.
        /// </summary>
        private static void EnsureSkFloorOnLogin(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            int level = GetCurrentLevel(character);
            uint floor = GetCumulativeSkForLevelStart(level);
            uint sk = NormalizeStatValue(character.Stats[StatIds.sk].BaseValue);
            if (sk < floor)
            {
                SetXpStat(character, StatIds.sk, floor, "login-sk-floor");
            }
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

        private static ICharacter ResolveXpRecipient(ICharacter attacker)
        {
            if (attacker == null)
            {
                return null;
            }

            if (attacker.Controller is Controllers.PlayerController)
            {
                return attacker;
            }

            if (PetCombatRules.IsPlayerOwnedPet(attacker))
            {
                return PetCombatRules.ResolvePetOwner(attacker);
            }

            return null;
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
                // Capture before XP/UnsavedXP rewrite so bar remainder is not mistaken for a pool.
                uint deathPool = GetDeathXpPool(character);

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
                SetXpStat(character, StatIds.xp, newFloor + remainder, "levelup-apply-cumulative");

                if (deathPool > 0)
                {
                    SetXpStat(character, StatIds.unsavedxp, deathPool, "levelup-keep-death-pool");
                }
                else
                {
                    SetXpStat(character, StatIds.unsavedxp, remainder, "levelup-apply-unsaved");
                }

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

            // LastSaveXP (372) is level-up floor wire only. SavedXP (334) is Insurance watermark —
            // do not clear it here.
            ClearDbManagedFloorStats(character, "login-normalize-clear-db-floor");

            // Prefer cumulative XP as bar source of truth. UnsavedXP may hold a death recovery
            // pool (level under 220) after insurance/death — do not fold that back into XP.
            uint progressFromXp = xp >= floor ? xp - floor : 0;
            uint deathPool = 0;
            if (level < MaxLevel && unsavedXp > 0 && unsavedXp != progressFromXp)
            {
                deathPool = unsavedXp;
            }

            uint progress = progressFromXp;
            if (deathPool == 0 && unsavedXp > 0 && unsavedXp == progressFromXp)
            {
                progress = unsavedXp;
            }
            else if (deathPool == 0 && unsavedXp > 0 && progressFromXp == 0 && xp == 0)
            {
                progress = ResolveStoredProgress(level, floor, xp, unsavedXp);
            }

            SetXpStat(character, StatIds.xp, floor + progress, "login-normalize-cumulative");
            if (deathPool > 0)
            {
                SetXpStat(character, StatIds.unsavedxp, deathPool, "login-normalize-death-pool");
            }
            else
            {
                SetXpStat(character, StatIds.unsavedxp, progress, "login-normalize-unsaved");
            }

            EnsureLevelXpThresholds(character, "login-normalize-thresholds");
            if (GetCurrentLevel(character) >= ShadowLevelFloor)
            {
                EnsureLevelSkThresholds(character, "login-normalize-sk-thresholds");
            }

            LogXpTrace(
                character,
                "login-normalize-after",
                "resolvedProgress=" + progress.ToString(CultureInfo.InvariantCulture)
                + " deathPool=" + deathPool.ToString(CultureInfo.InvariantCulture));

            ApplyPendingLevelUps(character, level);
            int levelAfterXp = GetCurrentLevel(character);
            if (levelAfterXp >= ShadowLevelFloor)
            {
                ApplyPendingSkLevelUps(character, levelAfterXp);
            }
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
            uint xp = NormalizeStatValue(character.Stats[StatIds.xp].BaseValue);
            if (xp >= floor)
            {
                return xp - floor;
            }

            // Fallback when XP not yet set: only use UnsavedXP if it is not a death pool.
            uint unsaved = NormalizeStatValue(character.Stats[StatIds.unsavedxp].BaseValue);
            if (unsaved > 0 && GetDeathXpPool(character) == 0)
            {
                return unsaved;
            }

            return xp;
        }

        /// <summary>
        /// Death recovery pool lives in UnsavedXP while it differs from live bar progress.
        /// </summary>
        private static uint GetDeathXpPool(ICharacter character)
        {
            if (character == null)
            {
                return 0;
            }

            int level = GetCurrentLevel(character);
            if (level >= MaxLevel)
            {
                return 0;
            }

            uint floor = GetCumulativeXpForLevelStart(level);
            uint xp = NormalizeStatValue(character.Stats[StatIds.xp].BaseValue);
            uint progress = xp >= floor ? xp - floor : 0;
            uint unsaved = NormalizeStatValue(character.Stats[StatIds.unsavedxp].BaseValue);
            if (unsaved > 0 && unsaved != progress)
            {
                return unsaved;
            }

            return 0;
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
        /// LastSaveXP (372) is the temporary level-up floor wire value; compute live from the RK
        /// table and clear it after use. SavedXP (334) is the Insurance Terminal watermark from
        /// SaveChar and must not be wiped on kill/login normalize.
        /// </summary>
        private static void ClearDbManagedFloorStats(ICharacter character, string source)
        {
            SetXpStat(character, StatIds.lastsavexp, 0, source + ":lastsave");
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
            // Zone client SendCompressed — StatMessageHandler.Default.Send can buffer until zone-out
            // (mission finish reward stayed invisible until leave).
            if (client != null)
            {
                client.SendCompressed(
                    new StatMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        Stats =
                            new[]
                            {
                                new GameTuple<CharacterStat, uint>
                                {
                                    Value1 = (CharacterStat)StatIds.xp,
                                    Value2 = cumulativeXp
                                }
                            }
                    });
            }
            else
            {
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.xp, cumulativeXp);
            }

            LogXpWireFeedbackOutbound(
                "CombatXpRuntimeService",
                "kill-xp-feedback",
                character,
                LevelUpFeedbackCategoryId,
                XpFeedbackMessageId);
            if (client != null)
            {
                client.SendCompressed(
                    new FeedbackMessage
                    {
                        Identity = character.Identity,
                        Unknown = 1,
                        Unknown1 = 0,
                        CategoryId = LevelUpFeedbackCategoryId,
                        MessageId = XpFeedbackMessageId
                    });
            }
            else
            {
                FeedbackMessageHandler.Default.Send(
                    character,
                    LevelUpFeedbackCategoryId,
                    XpFeedbackMessageId);
            }
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

            if (level > MaxRubikaLevel)
            {
                return (uint)XPTable.TableRKXP[MaxRubikaLevel - 1, 1];
            }

            return (uint)XPTable.TableRKXP[level - 1, 1];
        }

        private static int GetNextXpRequiredForLevel(int level)
        {
            if (level < 1 || level >= MaxRubikaLevel)
            {
                return 0;
            }

            return (int)XPTable.TableRKXP[level - 1, 2];
        }

        /// <summary>
        /// Levels 200-219 progress via Shadowknowledge (SK), not XP.
        /// Table: XPTable.TableShadowLandsSK — AO-Universe / AOWiki values.
        /// </summary>
        private static bool IsShadowLevelProgression(int level)
        {
            return level >= ShadowLevelFloor && level < MaxLevel;
        }

        private static int ConvertXpRewardToSk(int xpReward)
        {
            if (xpReward <= 0)
            {
                return 0;
            }

            int sk = xpReward / SkToXpFactor;
            return sk > 0 ? sk : 1;
        }

        private static uint GetCumulativeSkForLevelStart(int level)
        {
            if (level < ShadowLevelFloor)
            {
                return 0;
            }

            if (level > MaxLevel)
            {
                level = MaxLevel;
            }

            return (uint)XPTable.TableShadowLandsSK[level - ShadowLevelFloor, 1];
        }

        private static int GetNextSkRequiredForLevel(int level)
        {
            if (level < ShadowLevelFloor || level >= MaxLevel)
            {
                return 0;
            }

            return XPTable.TableShadowLandsSK[level - ShadowLevelFloor, 2];
        }

        private static uint GetSkBarProgress(ICharacter character)
        {
            int level = GetCurrentLevel(character);
            uint floor = GetCumulativeSkForLevelStart(level);
            uint sk = NormalizeStatValue(character.Stats[StatIds.sk].BaseValue);
            if (sk >= floor)
            {
                return sk - floor;
            }

            return sk;
        }

        private static void AwardCombatSk(
            ICharacter character,
            IZoneClient client,
            int xpEquivalentReward,
            string attackerIdentity)
        {
            int skReward = ConvertXpRewardToSk(xpEquivalentReward);
            if (skReward <= 0)
            {
                LogXpTrace(character, "sk-kill-skip", "reason=zero-sk-reward");
                return;
            }

            int levelBefore = GetCurrentLevel(character);
            uint floorSk = GetCumulativeSkForLevelStart(levelBefore);
            uint progressBefore = GetSkBarProgress(character);
            uint newProgress = AddClamped(progressBefore, skReward);

            LogXpTrace(
                character,
                "sk-kill-start",
                "xpReward=" + xpEquivalentReward.ToString(CultureInfo.InvariantCulture)
                + " skReward=" + skReward.ToString(CultureInfo.InvariantCulture)
                + " sourceAttacker=" + attackerIdentity
                + " level=" + levelBefore.ToString(CultureInfo.InvariantCulture));

            SetXpStat(character, StatIds.sk, floorSk + newProgress, "sk-kill-add-cumulative");
            EnsureLevelSkThresholds(character, "sk-kill-thresholds");

            bool leveledUp = ApplyPendingSkLevelUps(character, levelBefore);
            if (leveledUp)
            {
                SendLevelUpPreFeedbackPackets(client, character, levelBefore);
                SendSkProgressPackets(client, character);
                PersistLevelStat(character);
            }
            else
            {
                SendSkProgressPackets(client, character);
            }

            ClearManualXpWireStatChangedFlags(character, leveledUp);
            ClearStatChangedFlag(character, StatIds.sk);
            ClearStatChangedFlag(character, StatIds.nextsk);
            WriteXpStatsToDb(character, leveledUp ? "sk-kill-levelup" : "sk-kill-complete");
            LogXpTrace(
                character,
                leveledUp ? "sk-kill-levelup" : "sk-kill-complete",
                "levelBefore=" + levelBefore.ToString(CultureInfo.InvariantCulture)
                + " levelAfter=" + GetCurrentLevel(character).ToString(CultureInfo.InvariantCulture)
                + " sk=" + character.Stats[StatIds.sk].BaseValue.ToString(CultureInfo.InvariantCulture));
        }

        private static bool AwardDirectSk(
            ICharacter character,
            IZoneClient client,
            int xpEquivalentReward,
            string sourceTag)
        {
            int skReward = ConvertXpRewardToSk(xpEquivalentReward);
            if (skReward <= 0)
            {
                return false;
            }

            int levelBefore = GetCurrentLevel(character);
            uint floorSk = GetCumulativeSkForLevelStart(levelBefore);
            uint progressBefore = GetSkBarProgress(character);
            uint newProgress = AddClamped(progressBefore, skReward);

            SetXpStat(character, StatIds.sk, floorSk + newProgress, sourceTag + "-sk-add");
            EnsureLevelSkThresholds(character, sourceTag + "-sk-thresholds");

            bool leveledUp = ApplyPendingSkLevelUps(character, levelBefore);
            if (leveledUp)
            {
                SendLevelUpPreFeedbackPackets(client, character, levelBefore);
                PersistLevelStat(character);
            }

            SendSkProgressPackets(client, character);
            ClearManualXpWireStatChangedFlags(character, leveledUp);
            ClearStatChangedFlag(character, StatIds.sk);
            ClearStatChangedFlag(character, StatIds.nextsk);
            WriteXpStatsToDb(character, leveledUp ? sourceTag + "-sk-levelup" : sourceTag + "-sk-complete");
            LogXpTrace(
                character,
                leveledUp ? sourceTag + "-sk-levelup" : sourceTag + "-sk-complete",
                "skReward=" + skReward.ToString(CultureInfo.InvariantCulture)
                + " levelBefore=" + levelBefore.ToString(CultureInfo.InvariantCulture)
                + " levelAfter=" + GetCurrentLevel(character).ToString(CultureInfo.InvariantCulture));
            return true;
        }

        private static bool ApplyPendingSkLevelUps(ICharacter character, int levelBefore)
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

                if (!IsShadowLevelProgression(currentLevel))
                {
                    break;
                }

                int nextSkRequired = GetNextSkRequiredForLevel(currentLevel);
                if (nextSkRequired <= 0)
                {
                    break;
                }

                uint barProgress = GetSkBarProgress(character);
                if (barProgress < (uint)nextSkRequired)
                {
                    LogXpTrace(
                        character,
                        "sk-levelup-skip",
                        "currentLevel=" + currentLevel.ToString(CultureInfo.InvariantCulture)
                        + " progress=" + barProgress.ToString(CultureInfo.InvariantCulture)
                        + " required=" + nextSkRequired.ToString(CultureInfo.InvariantCulture));
                    break;
                }

                int newLevel = currentLevel + 1;
                uint remainder = barProgress - (uint)nextSkRequired;
                uint newFloor = GetCumulativeSkForLevelStart(newLevel);

                LogXpTrace(
                    character,
                    "sk-levelup-apply",
                    "fromLevel=" + currentLevel.ToString(CultureInfo.InvariantCulture)
                    + " toLevel=" + newLevel.ToString(CultureInfo.InvariantCulture)
                    + " progressBefore=" + barProgress.ToString(CultureInfo.InvariantCulture)
                    + " threshold=" + nextSkRequired.ToString(CultureInfo.InvariantCulture)
                    + " remainder=" + remainder.ToString(CultureInfo.InvariantCulture)
                    + " newFloor=" + newFloor.ToString(CultureInfo.InvariantCulture));

                SetXpStat(character, StatIds.level, (uint)newLevel, "sk-levelup-apply-level");
                SetXpStat(character, StatIds.sk, newFloor + remainder, "sk-levelup-apply-cumulative");
                SetXpStat(character, StatIds.nextxp, 0, "sk-levelup-clear-nextxp");
                EnsureLevelSkThresholds(character, "sk-levelup-thresholds");
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

        private static void EnsureLevelSkThresholds(ICharacter character, string source)
        {
            if (character == null)
            {
                return;
            }

            // NextSK is computed from level (StatNextSK). Do not overwrite its base value.
            LogXpTrace(
                character,
                "sk-thresholds",
                "source=" + source
                + " level=" + GetCurrentLevel(character).ToString(CultureInfo.InvariantCulture)
                + " nextSk=" + GetNextSkRequiredForLevel(GetCurrentLevel(character)).ToString(CultureInfo.InvariantCulture)
                + " sk=" + NormalizeStatValue(character.Stats[StatIds.sk].BaseValue).ToString(CultureInfo.InvariantCulture));
        }

        private static void SendSkProgressPackets(IZoneClient client, ICharacter character)
        {
            if (client == null || character == null)
            {
                return;
            }

            int level = GetCurrentLevel(character);
            uint sk = NormalizeStatValue(character.Stats[StatIds.sk].BaseValue);
            uint nextSk = (uint)GetNextSkRequiredForLevel(level);
            // Force computed NextSK into the Value cache so any later changed-stat flush
            // (sendBaseValue=false) still emits the table amount, not a stale 0.
            character.Stats[StatIds.nextsk].ReCalculate = true;
            int _ = character.Stats[StatIds.nextsk].Value;
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.sk, sk);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.nextsk, nextSk);
            LogXpTrace(
                character,
                "sk-progress-wire",
                "level=" + level.ToString(CultureInfo.InvariantCulture)
                + " sk=" + sk.ToString(CultureInfo.InvariantCulture)
                + " nextSk=" + nextSk.ToString(CultureInfo.InvariantCulture));
        }

        private static void SendLevelUpPreFeedbackPackets(
            IZoneClient client,
            ICharacter character,
            int levelBefore)
        {
            SendLevelUpPreFeedbackPackets(client, character, levelBefore, GetCurrentLevel(character));
        }

        private static void SendLevelUpPreFeedbackPackets(
            IZoneClient client,
            ICharacter character,
            int levelBefore,
            int levelAfter)
        {
            int maxLife = Math.Max(1, character.Stats[StatIds.life].Value);
            int maxNano = Math.Max(0, character.Stats[StatIds.maxnanoenergy].Value);

            StatMessageHandler.Default.SendSingle(character, (int)StatIds.life, (uint)maxLife);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.maxnanoenergy, (uint)maxNano);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.currentnano, (uint)maxNano);

            for (int level = levelBefore + 1; level <= levelAfter; level++)
            {
                uint cumulativeXp = character.Stats[StatIds.xp].BaseValue;
                uint lastSaveXp = GetCumulativeXpForLevelStart(level);
                uint nextLevelXp = level >= MaxRubikaLevel
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
            uint nextLevelXp = level >= MaxRubikaLevel ? 0 : GetCumulativeXpForLevelStart(level + 1);

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
                "level54={0} xp52={1} unsaved592={2} lastsave372={3} saved334={4} next350={5} lastxp57={6} sk573={7}",
                character.Stats[StatIds.level].BaseValue,
                character.Stats[StatIds.xp].BaseValue,
                character.Stats[StatIds.unsavedxp].BaseValue,
                character.Stats[StatIds.lastsavexp].BaseValue,
                character.Stats[StatIds.savedxp].BaseValue,
                character.Stats[StatIds.nextxp].BaseValue,
                character.Stats[StatIds.lastxp].BaseValue,
                character.Stats[StatIds.sk].BaseValue);
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
                case StatIds.sk:
                    return "SK";
                case StatIds.lastsk:
                    return "LastSK";
                case StatIds.nextsk:
                    return "NextSK";
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
