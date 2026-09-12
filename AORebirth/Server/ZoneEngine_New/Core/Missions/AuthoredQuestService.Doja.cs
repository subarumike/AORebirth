namespace ZoneEngine_New.Core.Missions;

using System;
using System.Globalization;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Doja;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Network;
using MissionLifecycleState = AORebirth.Interfaces.Persistence.Missions.MissionLifecycleState;

public sealed partial class AuthoredQuestService
{
    bool TryUseDoja(Player player, Identity slot, Item item)
    {
        if (!IsCurrent(player) || !DojaChipInteractionRules.TryResolveChip(item.LowId, item.HighId, out var chip)) return false;
        string? account = Account(player);
        if (account == null) return false;
        return Mutate(player, account, (tx, service) =>
        {
            RequireSource(player, slot, item);
            if (!DojaChipInteractionRules.IsLevelEligible(chip, player.Stats.GetOrOne(CharacterStat.Level)))
                return () => SendChat(player, "You are not eligible to turn in this DOJA chip.");
            if (HasDojaCooldown(tx, player.Identity.Instance, out _))
                return () => SendChat(player, "You've already turned in a DOJA chip today.");
            if (!chip.IsImplemented) return () => SendChat(player, "DOJA Chip " + chip.ZoneName + " is not available yet.");
            if (tx.GetMission(new(player.Identity.Instance, DojaChipInteractionRules.QuestTurnIn))?.State == MissionLifecycleState.Active) return null;
            Accept(service, player.Identity.Instance, DojaChipInteractionRules.QuestTurnIn);
            // The shared service returns AlreadyApplied for a completed mission. That
            // preserves history; it does not authorize a fresh daily cycle or journal.
            if (tx.GetMission(new(player.Identity.Instance, DojaChipInteractionRules.QuestTurnIn))?.State != MissionLifecycleState.Active)
                throw new InvalidOperationException("DOJA turn-in has no active accepted cycle; completed history cannot be replayed.");
            return () =>
            {
                // Accepted Use projects the chip but never consumes it; trade is the later consumption boundary.
                player.Session?.Send(new TemplateActionMessage { Identity = player.Identity, ItemLowId = item.LowId, ItemHighId = item.HighId,
                    Quality = item.Quality, Placement = slot, Unknown1 = 1, Unknown2 = 3, Unknown3 = (int)player.Identity.Type, Unknown4 = player.Identity.Instance });
                SendDojaJournal(player, DojaChipInteractionRules.QuestTurnIn, 12 * 60 * 60);
            };
        });
    }

    void RestoreDoja(Player player)
    {
        string? account = Account(player);
        if (account == null) return;
        Mutate(player, account, (tx, service) =>
        {
            bool turnIn = tx.GetMission(new(player.Identity.Instance, DojaChipInteractionRules.QuestTurnIn))?.State == MissionLifecycleState.Active;
            bool active = tx.GetMission(new(player.Identity.Instance, DojaChipInteractionRules.QuestCooldown))?.State == MissionLifecycleState.Active;
            DateTime? until = CharacterDojaCooldown(tx, player.Identity.Instance);
            if (until.HasValue && until.Value <= _now())
            {
                if (active) CompleteDoja(service, player, DojaChipInteractionRules.QuestCooldown, "mission_55AA2803_cooldown", "cooldown-expired-login");
                return () =>
                {
                    if (turnIn) SendDojaJournal(player, DojaChipInteractionRules.QuestTurnIn, 12 * 60 * 60);
                    if (active) SendDojaDelete(player, DojaChipInteractionRules.QuestCooldown);
                };
            }
            if (!active && until.HasValue) Accept(service, player.Identity.Instance, DojaChipInteractionRules.QuestCooldown);
            int remaining = until.HasValue ? Math.Clamp((int)Math.Ceiling((until.Value - _now()).TotalSeconds), 1, 18 * 60 * 60) : 18 * 60 * 60;
            return () =>
            {
                if (turnIn) SendDojaJournal(player, DojaChipInteractionRules.QuestTurnIn, 12 * 60 * 60);
                if (active || until.HasValue) SendDojaJournal(player, DojaChipInteractionRules.QuestCooldown, remaining);
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

    bool HasDojaCooldown(IMissionDaoTransaction tx, int owner, out DateTime? until)
    {
        until = CharacterDojaCooldown(tx, owner);
        if (tx.GetMission(new(owner, DojaChipInteractionRules.QuestCooldown))?.State == MissionLifecycleState.Active || until > _now()) return true;
        var accountFlag = tx.GetAccountFlag(tx.AccountKey, DojaChipInteractionRules.CooldownFlag);
        return accountFlag != null && (!DateTime.TryParse(accountFlag.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var accountUntil) || accountUntil > _now());
    }

    static DateTime? CharacterDojaCooldown(IMissionDaoTransaction tx, int owner)
    {
        var flag = tx.GetFlag(new(owner, DojaChipInteractionRules.QuestCooldown), DojaChipInteractionRules.CooldownFlag)
            ?? tx.GetFlag(new(owner, DojaChipInteractionRules.QuestTurnIn), DojaChipInteractionRules.CooldownFlag);
        return flag != null && DateTime.TryParse(flag.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var until) ? until : null;
    }

    void SendDojaJournal(Player player, string quest, int remaining)
    {
        var now = _now();
        long elapsed = player.Session is IGameTimeSession { GameTimeSynchronizedAtUtc: { } anchor } ? Math.Max(0, (long)(now - anchor).TotalSeconds) : 0;
        player.Session?.Send(DojaChipPacketSender.CreateJournalPacket(player.Identity.Instance, quest, remaining, elapsed));
    }
    static void SendDojaDelete(Player player, string quest)
    { foreach (var packet in DojaChipPacketSender.CreateDeletePackets(player.Identity.Instance, quest)) player.Session?.Send(packet); }
    static void SendChat(Player player, string text) => player.Session?.Send(new ChatTextMessage { Identity = player.Identity, Text = text });

    /// <summary>Called only for an exact accepted Scarlett trade session on its playfield owner thread.</summary>
    public bool TryTurnInDoja(Player player, Identity slot, Item chip, Action publishAcceptedTrade)
    {
        if (!IsCurrent(player) || player.Playfield?.Identity.Instance != DojaChipInteractionRules.ScarlettPlayfieldId
            || !DojaChipInteractionRules.IsNascenseChip(chip.LowId, chip.HighId)) return false;
        ArgumentNullException.ThrowIfNull(publishAcceptedTrade);
        string? account = Account(player);
        if (account == null) return false;
        return Mutate(player, account, (tx, service) =>
        {
            RequireSource(player, slot, chip);
            if (!DojaChipInteractionRules.TryResolveChip(chip.LowId, chip.HighId, out var definition)
                || !DojaChipInteractionRules.IsLevelEligible(definition, player.Stats.GetOrOne(CharacterStat.Level))
                || tx.GetMission(new(player.Identity.Instance, DojaChipInteractionRules.QuestTurnIn))?.State != AORebirth.Interfaces.Persistence.Missions.MissionLifecycleState.Active
                || HasDojaCooldown(tx, player.Identity.Instance, out _)) throw new InvalidOperationException("DOJA turn-in is not eligible.");
            if (!DailyMissionRewardRules.TryCreateCompletionSnapshot(player.Stats.GetOrOne(CharacterStat.Level), player.Stats.GetOrZero(CharacterStat.Side), out var snapshot))
                throw new InvalidOperationException("Accepted daily reward snapshot is unavailable.");
            var progression = DirectXpRewardPlan.CreatePersistedReward(player, snapshot.XpReward);
            if (progression.LevelAfter != progression.LevelBefore + 1) throw new InvalidOperationException("DOJA full-level reward cannot project exactly one level from this persisted XP state.");
            CompleteDoja(service, player, DojaChipInteractionRules.QuestTurnIn, "mission_55AA2421_turnin", "complete-quest");
            long tokens = 0;
            if (snapshot.SideTokenReward > 0)
            {
                tokens = Math.Min(int.MaxValue, (long)player.Stats.GetOrZero((CharacterStat)snapshot.SideTokenStatId, StatDetail.Base) + snapshot.SideTokenReward);
                RequireNewReward(tx.TryApplyCharacterStatReward(new(new(player.Identity.Instance, DojaChipInteractionRules.QuestTurnIn), "doja-nascense-side-tokens-v1"), "character-stats",
                    [new() { StatIdentityType = (int)player.Identity.Type, StatId = snapshot.SideTokenStatId, Kind = AORebirth.Interfaces.Persistence.Missions.MissionStatMutationKind.Set,
                        Value = tokens, MinimumValue = 0, MaximumValue = int.MaxValue }],
                    DailyMissionRewardRules.CreateSideTokenEffectReference(snapshot.SideTokenStatId, snapshot.SideTokenReward), _now().Ticks));
            }
            RequireNewReward(tx.TryApplyCharacterStatReward(new(new(player.Identity.Instance, DojaChipInteractionRules.QuestTurnIn), "doja-nascense-full-level-xp-v1"), "character-stats",
                progression.Stats.Select(value => new MissionStatMutationData { StatIdentityType = (int)player.Identity.Type, StatId = (int)value.Key,
                    Kind = AORebirth.Interfaces.Persistence.Missions.MissionStatMutationKind.Set, Value = value.Value, MinimumValue = 0, MaximumValue = int.MaxValue }).ToArray(),
                DailyMissionRewardRules.CreateFullLevelXpEffectReference(snapshot.LevelBefore, snapshot.XpReward), _now().Ticks));
            Accept(service, player.Identity.Instance, DojaChipInteractionRules.QuestCooldown);
            string until = _now().AddHours(18).ToString("o", CultureInfo.InvariantCulture);
            RequireSuccess(service.SetFlag(player.Identity.Instance, DojaChipInteractionRules.QuestCooldown, DojaChipInteractionRules.CooldownFlag, until));
            RequireSuccess(service.SetFlag(player.Identity.Instance, DojaChipInteractionRules.QuestTurnIn, DojaChipInteractionRules.CooldownFlag, until));
            SaveDojaAccountCooldown(tx, account, until);
            RequireSuccess(service.SetFlag(player.Identity.Instance, DojaChipInteractionRules.QuestCooldown, DojaChipInteractionRules.TurnInGrantedFlag, "1"));
            ApplyRows(tx, Plan(player, []), player, slot, chip);
            return () =>
            {
                publishAcceptedTrade();
                player.Inventory.Inventory.Content.Remove(slot.Instance);
                player.Session?.Send(new CharacterActionMessage { Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot });
                if (snapshot.SideTokenReward > 0) player.Stats.Set((CharacterStat)snapshot.SideTokenStatId, (int)tokens, StatDetail.Base, dirty: false);
                progression.PublishAfterCommit(player);
                if (snapshot.SideTokenReward > 0) SendDojaTokens(player, snapshot.SideTokenStatId, snapshot.SideTokenReward, tokens);
                SendDojaDelete(player, DojaChipInteractionRules.QuestTurnIn);
                SendDojaJournal(player, DojaChipInteractionRules.QuestCooldown, 18 * 60 * 60);
            };
        });
    }

    static void RequireNewReward(MissionAtomicStatRewardResultData result)
    {
        if (result.Status != AORebirth.Interfaces.Persistence.Missions.MissionAtomicRewardStatus.Applied)
            throw new InvalidOperationException("DOJA has a previously applied or rejected reward stage; reconcile before consuming another chip.");
    }

    void SaveDojaAccountCooldown(IMissionDaoTransaction tx, string account, string until)
    {
        // SetAccountFlag grants permanent access from a completed source quest. Legacy
        // called it with the active cooldown quest and ignored its rejection. A timed
        // DOJA restriction instead joins this already validated completion transaction,
        // retaining the accepted cooldown SourceQuestId and the existing row's CAS.
        if (!string.Equals(account, tx.AccountKey, StringComparison.OrdinalIgnoreCase)
            || tx.GetMission(new(tx.CharacterId, DojaChipInteractionRules.QuestTurnIn))?.State != MissionLifecycleState.Completed
            || tx.GetMission(new(tx.CharacterId, DojaChipInteractionRules.QuestCooldown))?.State != MissionLifecycleState.Active)
            throw new InvalidOperationException("Account cooldown requires this owner's completed turn-in and active cooldown.");
        var flag = tx.GetAccountFlag(account, DojaChipInteractionRules.CooldownFlag)
            ?? new MissionAccountFlagData { AccountKey = account, FlagKey = DojaChipInteractionRules.CooldownFlag, CreatedAtUtcTicks = _now().Ticks };
        flag.Value = until; flag.SourceQuestId = DojaChipInteractionRules.QuestCooldown; flag.UpdatedAtUtcTicks = _now().Ticks;
        tx.SaveAccountFlag(account, flag);
    }

    static void CompleteDoja(PersistentMissionService service, Player player, string quest, string objective, string reason)
    {
        RequireSuccess(service.ObserveObjective(new MissionObjectiveObservation { CharacterId = player.Identity.Instance, QuestId = quest, ObjectiveId = objective,
            ObservationKey = "doja:" + reason + ":" + quest, Amount = 1, EventType = "DojaChip:ForceClose", SourceIdentity = player.Identity.ToString(), TargetIdentity = quest }));
        RequireSuccess(service.CompleteMission(player.Identity.Instance, quest));
    }

    static void SendDojaTokens(Player player, int stat, int reward, long finalValue)
    {
        // Exact accepted WindcallerKarrecPacketSender side-token projection used by DOJA.
        long previous = finalValue - reward;
        for (int pulse = 1; pulse <= reward / 2; pulse++)
            player.Session?.Send(new StatMessage { Identity = player.Identity,
                Stats = [new() { Value1 = (CharacterStat)stat, Value2 = (uint)(previous + pulse * 2) }] });
        player.Session?.Send(new FormatFeedbackMessage { Identity = player.Identity, Unknown = 1, FormattedMessage = "Side tokens collected: " + finalValue + "." });
        player.Session?.Send(new FeedbackMessage { Identity = player.Identity, Unknown = 1, CategoryId = 110, MessageId = 108871108 });
    }
}
