namespace ZoneEngine_New.Core.Missions;

using System;
using System.Globalization;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Network;
using MissionLifecycleState = AORebirth.Interfaces.Persistence.Missions.MissionLifecycleState;

public sealed partial class AuthoredQuestService
{
    bool TryUseTimedItem(Player player, Identity slot, Item item, TimedTurnInDefinition rule)
    {
        if (!IsCurrent(player) || !TryResolveTimedItem(rule, item, out var chip)) return false;
        string? account = Account(player);
        if (account == null) return false;
        return Mutate(player, account, tx =>
        {
            RequireSource(player, slot, item);
            if (!LevelEligible(chip, player.Stats.GetOrOne(CharacterStat.Level)))
                return () => SendChat(player, rule.IneligibleText);
            if (HasCooldown(tx, player.Identity.Instance, rule, out _))
                return () => SendChat(player, rule.CooldownText);
            if (!chip.Enabled) return () => SendChat(player, rule.UnavailableText.Replace("{name}", chip.Name));
            if (tx.GetMission(new(player.Identity.Instance, rule.Quest))?.State == MissionLifecycleState.Active) return null;
            Accept(tx, rule.Quest);
            // The shared service returns AlreadyApplied for a completed mission. That
            // preserves history; it does not authorize a fresh daily cycle or journal.
            if (tx.GetMission(new(player.Identity.Instance, rule.Quest))?.State != MissionLifecycleState.Active)
                throw new InvalidOperationException("Timed turn-in turn-in has no active accepted cycle; completed history cannot be replayed.");
            return () =>
            {
                // Accepted Use projects the chip but never consumes it; trade is the later consumption boundary.
                player.Session?.Send(new TemplateActionMessage { Identity = player.Identity, ItemLowId = item.LowId, ItemHighId = item.HighId,
                    Quality = item.Quality, Placement = slot, Unknown1 = 1, Action = TemplateActionType.Use, Unknown3 = (int)player.Identity.Type, Unknown4 = player.Identity.Instance });
                SendTimedJournal(player, rule.Quest, Content.Journals[rule.Quest].DurationSeconds);
            };
        });
    }

    void RestoreTimedTurnIn(Player player, TimedTurnInDefinition rule)
    {
        string? account = Account(player);
        if (account == null) return;
        Mutate(player, account, tx =>
        {
            bool turnIn = tx.GetMission(new(player.Identity.Instance, rule.Quest))?.State == MissionLifecycleState.Active;
            bool active = tx.GetMission(new(player.Identity.Instance, rule.CooldownQuest))?.State == MissionLifecycleState.Active;
            DateTime? until = CharacterCooldown(tx, player.Identity.Instance, rule);
            if (until.HasValue && until.Value <= _now())
            {
                if (active) CompleteTimed(tx, player, rule, rule.CooldownQuest, rule.CooldownObjective, "cooldown-expired-login");
                return () =>
                {
                    if (turnIn) SendTimedJournal(player, rule.Quest, Content.Journals[rule.Quest].DurationSeconds);
                    if (active) SendTimedDelete(player, rule.CooldownQuest);
                };
            }
            if (!active && until.HasValue) Accept(tx, rule.CooldownQuest);
            int remaining = until.HasValue ? Math.Clamp((int)Math.Ceiling((until.Value - _now()).TotalSeconds), 1, rule.CooldownSeconds) : rule.CooldownSeconds;
            return () =>
            {
                if (turnIn) SendTimedJournal(player, rule.Quest, Content.Journals[rule.Quest].DurationSeconds);
                if (active || until.HasValue) SendTimedJournal(player, rule.CooldownQuest, remaining);
            };
        });
    }

    string? Account(Player player)
    {
        try
        {
            string account = _dao.ResolveCharacterAccountKey(player.Identity.Instance);
            return string.IsNullOrWhiteSpace(account) ? null : account;
        }
        catch (Exception exception) { _logger.Error(exception, "Authored account ownership could not be resolved."); return null; }
    }

    bool HasCooldown(IMissionDaoTransaction tx, int owner, TimedTurnInDefinition rule, out DateTime? until)
    {
        until = CharacterCooldown(tx, owner, rule);
        if (tx.GetMission(new(owner, rule.CooldownQuest))?.State == MissionLifecycleState.Active || until > _now()) return true;
        var accountFlag = tx.GetAccountFlag(tx.AccountKey, rule.CooldownFlag);
        return accountFlag != null && (!DateTime.TryParse(accountFlag.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var accountUntil) || accountUntil > _now());
    }

    static DateTime? CharacterCooldown(IMissionDaoTransaction tx, int owner, TimedTurnInDefinition rule)
    {
        var flag = tx.GetFlag(new(owner, rule.CooldownQuest), rule.CooldownFlag)
            ?? tx.GetFlag(new(owner, rule.Quest), rule.CooldownFlag);
        return flag != null && DateTime.TryParse(flag.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var until) ? until : null;
    }

    void SendTimedJournal(Player player, string quest, int remaining)
    {
        var now = _now();
        long elapsed = player.Session is IGameTimeSession { GameTimeSynchronizedAtUtc: { } anchor } ? Math.Max(0, (long)(now - anchor).TotalSeconds) : 0;
        AuthoredQuestJournal.Send(player, quest, now, Content, remaining);
    }
    void SendTimedDelete(Player player, string quest)
    { AuthoredQuestJournal.Delete(player, quest, Content); }
    static void SendChat(Player player, string text) => player.Session?.Send(new ChatTextMessage { Identity = player.Identity, Text = text });

        public bool TryTurnInTimedItem(Player player, Identity slot, Item chip, Action publishAcceptedTrade, TimedTurnInDefinition rule)
    {
        if (!IsCurrent(player) || !rule.TurnInPlayfields.Contains(player.Playfield!.Identity.Instance)
            || !TryResolveTimedItem(rule, chip, out var turnInItem) || !turnInItem.Enabled) return false;
        ArgumentNullException.ThrowIfNull(publishAcceptedTrade);
        string? account = Account(player);
        if (account == null) return false;
        return Mutate(player, account, tx =>
        {
            RequireSource(player, slot, chip);
            if (!TryResolveTimedItem(rule, chip, out var definition)
                || !LevelEligible(definition, player.Stats.GetOrOne(CharacterStat.Level))
                || tx.GetMission(new(player.Identity.Instance, rule.Quest))?.State != AORebirth.Interfaces.Persistence.Missions.MissionLifecycleState.Active
                || HasCooldown(tx, player.Identity.Instance, rule, out _)) throw new InvalidOperationException("Timed turn-in turn-in is not eligible.");
            if (!ProgressionRewardRules.TryCreateCompletionSnapshot(player.Stats.GetOrOne(CharacterStat.Level), player.Stats.GetOrZero(CharacterStat.Side), out var snapshot))
                throw new InvalidOperationException("Accepted daily reward snapshot is unavailable.");
            var progression = DirectXpRewardPlan.CreatePersistedReward(player, snapshot.XpReward);
            if (progression.LevelAfter != progression.LevelBefore + 1) throw new InvalidOperationException("Timed turn-in full-level reward cannot project exactly one level from this persisted XP state.");
            CompleteTimed(tx, player, rule, rule.Quest, rule.Objective, "complete-quest");
            long tokens = 0;
            if (snapshot.SideTokenReward > 0)
            {
                tokens = Math.Min(int.MaxValue, (long)player.Stats.GetOrZero((CharacterStat)snapshot.SideTokenStatId, StatDetail.Base) + snapshot.SideTokenReward);
                RequireNewReward(tx.TryApplyCharacterStatReward(new(new(player.Identity.Instance, rule.Quest), rule.TokenRewardKey), "character-stats",
                    [new() { StatIdentityType = (int)player.Identity.Type, StatId = snapshot.SideTokenStatId, Kind = AORebirth.Interfaces.Persistence.Missions.MissionStatMutationKind.Set,
                        Value = tokens, MinimumValue = 0, MaximumValue = int.MaxValue }],
                    ProgressionRewardRules.CreateSideTokenEffectReference(snapshot.SideTokenStatId, snapshot.SideTokenReward), _now().Ticks));
            }
            RequireNewReward(tx.TryApplyCharacterStatReward(new(new(player.Identity.Instance, rule.Quest), rule.XpRewardKey), "character-stats",
                progression.Stats.Select(value => new MissionStatMutationData { StatIdentityType = (int)player.Identity.Type, StatId = (int)value.Key,
                    Kind = AORebirth.Interfaces.Persistence.Missions.MissionStatMutationKind.Set, Value = value.Value, MinimumValue = 0, MaximumValue = int.MaxValue }).ToArray(),
                ProgressionRewardRules.CreateFullLevelXpEffectReference(snapshot.LevelBefore, snapshot.XpReward), _now().Ticks));
            Accept(tx, rule.CooldownQuest);
            string until = _now().AddSeconds(rule.CooldownSeconds).ToString("o", CultureInfo.InvariantCulture);
            AuthoredMissionProgression.SetFlag(tx, Definition(rule.CooldownQuest), rule.CooldownFlag, until, _now().Ticks);
            AuthoredMissionProgression.SetFlag(tx, Definition(rule.Quest), rule.CooldownFlag, until, _now().Ticks);
            SaveAccountCooldown(tx, account, until, rule);
            AuthoredMissionProgression.SetFlag(tx, Definition(rule.CooldownQuest), rule.GrantedFlag, "1", _now().Ticks);
            ApplyRows(tx, Plan(player, []), player, slot, chip);
            return () =>
            {
                publishAcceptedTrade();
                player.Inventory.Inventory.Content.Remove(slot.Instance);
                player.Session?.Send(new CharacterActionMessage { Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot });
                if (snapshot.SideTokenReward > 0) player.Stats.Set((CharacterStat)snapshot.SideTokenStatId, (int)tokens, StatDetail.Base, dirty: false);
                progression.PublishAfterCommit(player);
                if (snapshot.SideTokenReward > 0) SendTokens(player, snapshot.SideTokenStatId, snapshot.SideTokenReward, tokens, rule.TokenText);
                SendTimedDelete(player, rule.Quest);
                SendTimedJournal(player, rule.CooldownQuest, rule.CooldownSeconds);
            };
        });
    }

    static void RequireNewReward(MissionAtomicStatRewardResultData result)
    {
        if (result.Status != AORebirth.Interfaces.Persistence.Missions.MissionAtomicRewardStatus.Applied)
            throw new InvalidOperationException("Timed turn-in has a previously applied or rejected reward stage; reconcile before consuming another chip.");
    }

    void SaveAccountCooldown(IMissionDaoTransaction tx, string account, string until, TimedTurnInDefinition rule)
    {
                // called it with the active cooldown quest and ignored its rejection. A timed
        // Timed turn-in restriction instead joins this already validated completion transaction,
                if (!string.Equals(account, tx.AccountKey, StringComparison.OrdinalIgnoreCase)
            || tx.GetMission(new(tx.CharacterId, rule.Quest))?.State != MissionLifecycleState.Completed
            || tx.GetMission(new(tx.CharacterId, rule.CooldownQuest))?.State != MissionLifecycleState.Active)
            throw new InvalidOperationException("Account cooldown requires this owner's completed turn-in and active cooldown.");
        var flag = tx.GetAccountFlag(account, rule.CooldownFlag)
            ?? new MissionAccountFlagData { AccountKey = account, FlagKey = rule.CooldownFlag, CreatedAtUtcTicks = _now().Ticks };
        flag.Value = until; flag.SourceQuestId = rule.CooldownQuest; flag.UpdatedAtUtcTicks = _now().Ticks;
        tx.SaveAccountFlag(account, flag);
    }

    void CompleteTimed(IMissionDaoTransaction tx, Player player, TimedTurnInDefinition rule, string quest, string objective, string reason)
    {
        AuthoredMissionProgression.Complete(tx, Definition(quest), objective, rule.ObservationPrefix + reason + ":" + quest,
            rule.EventType, player.Identity.ToString(), quest, _now().Ticks);
    }

    static void SendTokens(Player player, int stat, int reward, long finalValue, string text)
    {
                long previous = finalValue - reward;
        for (int pulse = 1; pulse <= reward / 2; pulse++)
            player.Session?.Send(new StatMessage { Identity = player.Identity,
                Stats = [new() { Value1 = (CharacterStat)stat, Value2 = (uint)(previous + pulse * 2) }] });
        player.Session?.Send(new FormatFeedbackMessage { Identity = player.Identity, Unknown = 1, FormattedMessage = text.Replace("{value}", finalValue.ToString(CultureInfo.InvariantCulture)) });
        player.Session?.Send(new FeedbackMessage { Identity = player.Identity, Unknown = 1, CategoryId = 110, MessageId = 108871108 });
    }

    static bool TryResolveTimedItem(TimedTurnInDefinition rule, Item item, out TimedItem definition)
    { definition = rule.Items.FirstOrDefault(x => x.ItemId == item.LowId || x.ItemId == item.HighId)!; return definition != null; }
    static bool LevelEligible(TimedItem item, int level) => level >= item.MinLevel && level <= item.MaxLevel;
    bool CanOpenTimedTrade(Player player, TimedTurnInDefinition rule)
    {
        lock (player.PersistenceGate)
        {
            if (!IsCurrent(player) || !rule.Items.Any(x => x.Enabled && HasCarried(player, x.ItemId))) return false;
            string? account = Account(player); if (account == null) return false;
            try { return _dao.Execute(player.Identity.Instance, account, tx =>
                tx.GetMission(new(player.Identity.Instance, rule.Quest))?.State == MissionLifecycleState.Active
                && !HasCooldown(tx, player.Identity.Instance, rule, out _)); }
            catch (Exception e) { _logger.Error(e, "Timed turn-in eligibility could not be read."); return false; }
        }
    }
}
