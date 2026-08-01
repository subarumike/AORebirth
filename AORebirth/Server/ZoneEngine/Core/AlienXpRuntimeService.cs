namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Stats.SpecialStats;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Alien Invasion XP (AIXP) / Alien Level runtime.
    /// Alien detection: Flags bit 0x4000. Thresholds: <see cref="XPTable.TableAlienXP"/>.
    /// AIXP has no UnsavedXP/SavedXP and is never lost on death.
    /// Rubi-Ka min-level gates (AO-Universe): you may fill the bar toward the next AI level
    /// up to that level's need (e.g. AI2 at RK15 → fill to 22500 for AI3), then gain no more
    /// AIXP until the next RK gate; when RK reaches that gate, banked full bar auto-grants AI.
    /// Counters: InvadersKilled (615), KilledByInvaders (616).
    /// </summary>
    internal static class AlienXpRuntimeService
    {
        public const int AlienMobFlagsBit = 0x4000;

        private const int MaxAlienLevel = 30;

        private const int GreyMobMinLevelAdvantage = 7;

        private const uint UnsetStatSentinel = 1234567890u;

        /// <summary>
        /// Capture 20260726-230559: four Alien Spider - Zix kills each add 150 AIXP.
        /// </summary>
        private const int AreteAlienSpiderAixpReward = 150;

        /// <summary>
        /// Rubi-Ka level required for each Alien Level (AI 1..30). Index 0 unused.
        /// </summary>
        private static readonly int[] MinRubikaLevelForAlienLevel =
        {
            0,
            5, 15, 25, 35, 45, 55, 65, 75, 85, 95,
            105, 110, 115, 120, 125, 130, 135, 140, 145, 150,
            155, 160, 165, 170, 175, 180, 185, 190, 195, 200
        };

        internal static void AwardAlienXpOnKill(ICharacter attacker, ICharacter target)
        {
            if (attacker == null || target == null || !IsAlienTarget(target))
            {
                return;
            }

            ICharacter recipient = ResolveXpRecipient(attacker);
            if (recipient == null || !(recipient.Controller is Controllers.PlayerController))
            {
                return;
            }

            if (recipient.Controller.Client == null)
            {
                return;
            }

            // Flush any AI levels that RK already unlocks before deciding room in the bar.
            uint progress = GetAlienBarProgress(recipient);
            ClampProgressBehindRkGate(recipient, ref progress);
            int blockedByRkLevel;
            bool leveledBeforeAward = ApplyPendingAlienLevelUps(recipient, ref progress, out blockedByRkLevel);

            int axpReward = CalculateAlienXpReward(recipient, target);
            int awarded = CapRewardToBarRoom(recipient, progress, axpReward);
            if (awarded <= 0 && !leveledBeforeAward)
            {
                // Bar full until next RK gate — still count the invader kill, no AIXP.
                IncrementInvadersKilled(recipient);
                if (IsBarFullBehindRkGate(recipient, progress))
                {
                    int requiredRk;
                    if (TryGetBlockedNextAlienRkGate(recipient, out requiredRk))
                    {
                        ChatTextMessageHandler.Default.Send(
                            recipient,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Alien XP bar is full. Reach Rubi-Ka level {0} to advance.",
                                requiredRk));
                    }
                }

                recipient.Stats.Write();
                return;
            }

            int alienLevelBefore = GetAlienLevel(recipient);
            if (awarded > 0)
            {
                progress = AddClamped(progress, (uint)awarded);
            }

            bool leveledUp = ApplyPendingAlienLevelUps(recipient, ref progress, out blockedByRkLevel)
                             || leveledBeforeAward;
            ClampProgressBehindRkGate(recipient, ref progress);

            SetAlienStat(recipient, StatIds.alienxp, progress);
            EnsureAlienNextXp(recipient);
            IncrementInvadersKilled(recipient);

            StatMessageHandler.Default.SendSingle(recipient, (int)StatIds.alienxp, progress);
            StatMessageHandler.Default.SendSingle(
                recipient,
                (int)StatIds.aliennextxp,
                (uint)Math.Max(0, recipient.Stats[StatIds.aliennextxp].Value));
            if (leveledUp)
            {
                StatMessageHandler.Default.SendSingle(
                    recipient,
                    (int)StatIds.alienlevel,
                    (uint)GetAlienLevel(recipient));
            }

            if (awarded > 0)
            {
                ChatTextMessageHandler.Default.Send(
                    recipient,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "You gained {0} new Alien Experience Points.",
                        awarded));
            }

            if (leveledUp)
            {
                ChatTextMessageHandler.Default.Send(
                    recipient,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "You have advanced to Alien Level {0}.",
                        GetAlienLevel(recipient)));
            }
            else if (blockedByRkLevel > 0 && IsBarFullBehindRkGate(recipient, progress))
            {
                ChatTextMessageHandler.Default.Send(
                    recipient,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Alien XP bar is full. Reach Rubi-Ka level {0} to advance to Alien Level {1}.",
                        blockedByRkLevel,
                        GetAlienLevel(recipient) + 1));
            }

            recipient.Stats.Write();

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "AIXP kill char={0} target={1} reward={2} awarded={3} alienLevel={4}->{5} progress={6} leveledUp={7}",
                    recipient.Identity.ToString(true),
                    target.Name ?? string.Empty,
                    axpReward,
                    awarded,
                    alienLevelBefore,
                    GetAlienLevel(recipient),
                    progress,
                    leveledUp));
        }

        /// <summary>
        /// When a player dies to an alien invader, increment KilledByInvaders only.
        /// </summary>
        internal static void RecordPlayerKilledByInvader(ICharacter killer, ICharacter deadPlayer)
        {
            if (deadPlayer == null || !(deadPlayer.Controller is Controllers.PlayerController))
            {
                return;
            }

            if (killer == null || !IsAlienTarget(killer))
            {
                return;
            }

            int killedBy = Math.Max(0, deadPlayer.Stats[StatIds.killedbyinvaders].Value) + 1;
            SetAlienStat(deadPlayer, StatIds.killedbyinvaders, (uint)killedBy);
            StatMessageHandler.Default.SendSingle(
                deadPlayer,
                (int)StatIds.killedbyinvaders,
                (uint)killedBy);
            deadPlayer.Stats.Write();
        }

        /// <summary>
        /// Apply banked full AIXP bars into AI levels when Rubi-Ka gates unlock
        /// (RK level-up / login). Example: RK 25 with bar full at 22500 → AI3.
        /// </summary>
        internal static void TryApplyBankedAlienLevelUps(ICharacter character)
        {
            if (character == null || !(character.Controller is Controllers.PlayerController))
            {
                return;
            }

            if (character.Controller.Client == null)
            {
                return;
            }

            uint progress = GetAlienBarProgress(character);
            ClampProgressBehindRkGate(character, ref progress);
            int blockedByRkLevel;
            bool leveledUp = ApplyPendingAlienLevelUps(character, ref progress, out blockedByRkLevel);
            ClampProgressBehindRkGate(character, ref progress);
            if (!leveledUp)
            {
                SetAlienStat(character, StatIds.alienxp, progress);
                EnsureAlienNextXp(character);
                return;
            }

            SetAlienStat(character, StatIds.alienxp, progress);
            EnsureAlienNextXp(character);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.alienxp, progress);
            StatMessageHandler.Default.SendSingle(
                character,
                (int)StatIds.aliennextxp,
                (uint)Math.Max(0, character.Stats[StatIds.aliennextxp].Value));
            StatMessageHandler.Default.SendSingle(
                character,
                (int)StatIds.alienlevel,
                (uint)GetAlienLevel(character));
            ChatTextMessageHandler.Default.Send(
                character,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "You have advanced to Alien Level {0}.",
                    GetAlienLevel(character)));
            character.Stats.Write();
        }

        internal static bool IsAlienTarget(ICharacter target)
        {
            if (target == null || target.Controller is Controllers.PlayerController)
            {
                return false;
            }

            int flags = target.Stats[StatIds.flags].Value;
            if ((flags & AlienMobFlagsBit) != 0)
            {
                return true;
            }

            CombatTestMobArchetype.Entry entry;
            return CombatTestMobArchetype.TryGetByName(target.Name, out entry)
                   && entry == CombatTestMobArchetype.AlienSpiderZix;
        }

        private static bool IsAlienSpiderTestMob(ICharacter target)
        {
            CombatTestMobArchetype.Entry archetype;
            return CombatTestMobArchetype.TryGetByName(target.Name, out archetype)
                   && archetype == CombatTestMobArchetype.AlienSpiderZix;
        }

        private static int CalculateAlienXpReward(ICharacter attacker, ICharacter target)
        {
            if (IsAlienSpiderTestMob(target))
            {
                return AreteAlienSpiderAixpReward;
            }

            // The Arete corpus does not prove an AIXP value for the other alien
            // families. Keep those rewards closed instead of applying the
            // generic level-derived fallback.
            if (target.Playfield != null && target.Playfield.Identity.Instance == 6553)
            {
                return 0;
            }

            int targetLevel = Math.Max(1, (int)target.Stats[StatIds.level].BaseValue);
            int attackerLevel = Math.Max(1, (int)attacker.Stats[StatIds.level].BaseValue);

            if (attackerLevel - targetLevel >= GreyMobMinLevelAdvantage)
            {
                return 1;
            }

            int axpReward = Math.Max(10, targetLevel * 25);

            CombatTestMobArchetype.Entry archetype;
            if (CombatTestMobArchetype.TryGetByName(target.Name, out archetype) && archetype.XpReward > 0)
            {
                axpReward = Math.Max(axpReward, archetype.XpReward);
            }

            int targetXp = (int)target.Stats[StatIds.xp].BaseValue;
            if (targetXp > 0)
            {
                axpReward = Math.Max(axpReward, targetXp);
            }

            return axpReward;
        }

        /// <summary>
        /// When the next AI level is locked by Rubi-Ka, only allow filling up to that level's
        /// need (e.g. 22500 toward AI3 while at RK15 / AI2). Overflow is not earned.
        /// </summary>
        private static int CapRewardToBarRoom(ICharacter character, uint progress, int reward)
        {
            if (reward <= 0)
            {
                return 0;
            }

            int room;
            if (!TryGetFillCapWhileRkBlocked(character, out room))
            {
                return reward;
            }

            if (progress >= (uint)room)
            {
                return 0;
            }

            uint remaining = (uint)room - progress;
            return (int)Math.Min((uint)reward, remaining);
        }

        private static bool TryGetFillCapWhileRkBlocked(ICharacter character, out int fillCap)
        {
            fillCap = 0;
            int alienLevel = GetAlienLevel(character);
            if (alienLevel >= MaxAlienLevel)
            {
                return false;
            }

            int rkLevel = Math.Max(1, (int)character.Stats[StatIds.level].BaseValue);
            int nextAlienLevel = alienLevel + 1;
            int requiredRk = MinRubikaLevelForAlienLevel[nextAlienLevel];
            if (rkLevel >= requiredRk)
            {
                // Next AI is unlocked — earning past need triggers level-up, no soft cap.
                return false;
            }

            fillCap = GetNextAlienXpRequiredForLevel(alienLevel);
            return fillCap > 0;
        }

        private static bool TryGetBlockedNextAlienRkGate(ICharacter character, out int requiredRk)
        {
            requiredRk = 0;
            int alienLevel = GetAlienLevel(character);
            if (alienLevel >= MaxAlienLevel)
            {
                return false;
            }

            int rkLevel = Math.Max(1, (int)character.Stats[StatIds.level].BaseValue);
            int nextAlienLevel = alienLevel + 1;
            requiredRk = MinRubikaLevelForAlienLevel[nextAlienLevel];
            return rkLevel < requiredRk;
        }

        private static bool IsBarFullBehindRkGate(ICharacter character, uint progress)
        {
            int fillCap;
            return TryGetFillCapWhileRkBlocked(character, out fillCap)
                   && fillCap > 0
                   && progress >= (uint)fillCap;
        }

        private static void ClampProgressBehindRkGate(ICharacter character, ref uint progress)
        {
            int fillCap;
            if (TryGetFillCapWhileRkBlocked(character, out fillCap) && progress > (uint)fillCap)
            {
                progress = (uint)fillCap;
                SetAlienStat(character, StatIds.alienxp, progress);
            }
        }

        private static bool ApplyPendingAlienLevelUps(
            ICharacter character,
            ref uint progress,
            out int blockedByRkLevel)
        {
            bool leveled = false;
            int guard = 0;
            int rkLevel = Math.Max(1, (int)character.Stats[StatIds.level].BaseValue);
            blockedByRkLevel = 0;

            while (guard++ < MaxAlienLevel)
            {
                int alienLevel = GetAlienLevel(character);
                if (alienLevel >= MaxAlienLevel)
                {
                    break;
                }

                int nextAlienLevel = alienLevel + 1;
                int requiredRk = MinRubikaLevelForAlienLevel[nextAlienLevel];
                if (rkLevel < requiredRk)
                {
                    int nextNeeded = GetNextAlienXpRequiredForLevel(alienLevel);
                    if (nextNeeded > 0 && progress >= (uint)nextNeeded)
                    {
                        blockedByRkLevel = requiredRk;
                    }

                    break;
                }

                int needed = GetNextAlienXpRequiredForLevel(alienLevel);
                if (needed <= 0 || progress < (uint)needed)
                {
                    break;
                }

                progress -= (uint)needed;
                SetAlienStat(character, StatIds.alienlevel, (uint)nextAlienLevel);
                leveled = true;
            }

            SetAlienStat(character, StatIds.alienxp, progress);
            return leveled;
        }

        private static void IncrementInvadersKilled(ICharacter recipient)
        {
            int invadersKilled = Math.Max(0, recipient.Stats[StatIds.invaderskilled].Value) + 1;
            SetAlienStat(recipient, StatIds.invaderskilled, (uint)invadersKilled);
            StatMessageHandler.Default.SendSingle(
                recipient,
                (int)StatIds.invaderskilled,
                (uint)invadersKilled);
        }

        private static uint GetAlienBarProgress(ICharacter character)
        {
            return NormalizeStatValue(GetAlienXp(character));
        }

        private static int GetNextAlienXpRequiredForLevel(int alienLevel)
        {
            if (alienLevel < 0 || alienLevel >= MaxAlienLevel)
            {
                return 0;
            }

            return Convert.ToInt32(XPTable.TableAlienXP[alienLevel, 2]);
        }

        private static void EnsureAlienNextXp(ICharacter character)
        {
            int alienLevel = GetAlienLevel(character);
            uint nextXp = (uint)GetNextAlienXpRequiredForLevel(alienLevel);
            SetAlienStat(character, StatIds.aliennextxp, nextXp);
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

        private static int GetAlienLevel(ICharacter character)
        {
            int level = character.Stats[StatIds.alienlevel].Value;
            if (level < 0)
            {
                return 0;
            }

            if (level > MaxAlienLevel)
            {
                return MaxAlienLevel;
            }

            return level;
        }

        private static uint GetAlienXp(ICharacter character)
        {
            return NormalizeStatValue(character.Stats[StatIds.alienxp].BaseValue);
        }

        private static void SetAlienStat(ICharacter character, StatIds statId, uint newValue)
        {
            if (character == null)
            {
                return;
            }

            character.Stats[statId].Set(newValue);
        }

        private static uint AddClamped(uint value, uint delta)
        {
            if (value > uint.MaxValue - delta)
            {
                return uint.MaxValue;
            }

            return value + delta;
        }

        private static uint NormalizeStatValue(uint value)
        {
            return value == UnsetStatSentinel ? 0u : value;
        }
    }
}
