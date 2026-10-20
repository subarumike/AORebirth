namespace ZoneEngine_New.Core.Quests;

using System;
using System.Globalization;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;

/// <summary>
/// Recurring quests (Quests.json Recurring) and level bands (MinLevel/MaxLevel). A recurring quest's cooldown starts in
/// the same transaction that records its completion, and is checked again there, so two characters of one account
/// cannot both slip in before the other's turn-in lands. Account cooldowns live in missionaccountflags, character
/// cooldowns in missionflags, both under "quest-cooldown:" + the cooldown group (or the quest's hash).
/// </summary>
public sealed partial class QuestService
{
    const string CooldownFlagPrefix = "quest-cooldown:";
    const string CooldownUntilFlag = "until";
    const string DefaultCooldownText = "You cannot do this mission again yet. Time left: {time}.";
    const string LevelBandText = "You are not the right level for this mission.";

    /// <summary>The recurring quest's cooldown was found running inside the completion transaction itself.</summary>
    sealed class QuestCooldownActiveException(string message) : InvalidOperationException(message);

    static string CooldownKey(QuestTemplate template)
        => CooldownFlagPrefix + (string.IsNullOrWhiteSpace(template.Recurring?.CooldownGroup)
            ? template.Hash
            : template.Recurring.CooldownGroup.Trim());

    static bool InLevelBand(Player player, QuestTemplate template)
    {
        int level = player.Stats.GetOrOne(CharacterStat.Level);
        return level >= (template.MinLevel ?? 1) && (template.MaxLevel is not int max || level <= max);
    }

    /// <summary>
    /// Why <paramref name="player"/> may not take or finish <paramref name="template"/> now (wrong level, cooldown
    /// running), as the text to tell them; null when they may. Reads the cooldown from the database.
    /// </summary>
    public string? WhyNotEligible(Player player, QuestTemplate template)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(template);
        if (!InLevelBand(player, template))
            return LevelBandText;
        if (template.Recurring is not { } recurring)
            return null;

        DateTime? until = ReadCooldown(player, template);
        DateTime now = _now();
        return until is DateTime end && end > now ? CooldownText(recurring, end - now) : null;
    }

    DateTime? ReadCooldown(Player player, QuestTemplate template)
    {
        if (_completionDao == null)
            throw new InvalidOperationException("Recurring quests need the mission DAO for their cooldown.");
        return template.Recurring!.IsPerAccount
            ? _completionDao.Execute(player.Identity.Instance, Account(player), tx => ReadCooldown(tx, player, template))
            : _completionDao.Execute(player.Identity.Instance, tx => ReadCooldown(tx, player, template));
    }

    static DateTime? ReadCooldown(IMissionDaoTransaction tx, Player player, QuestTemplate template)
    {
        string key = CooldownKey(template);
        string? value = template.Recurring!.IsPerAccount
            ? tx.GetAccountFlag(tx.AccountKey, key)?.Value
            : tx.GetFlag(new MissionKeyData(player.Identity.Instance, key), CooldownUntilFlag)?.Value;
        return value != null && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime until)
            ? until
            : null;
    }

    /// <summary>Inside the transaction that earns the quest: refuses while the cooldown runs, otherwise starts it.</summary>
    void StartCooldown(IMissionDaoTransaction tx, Player player, QuestTemplate template)
    {
        QuestRecurring recurring = template.Recurring!;
        DateTime now = _now();
        if (ReadCooldown(tx, player, template) is DateTime running && running > now)
            throw new QuestCooldownActiveException(CooldownText(recurring, running - now));

        string key = CooldownKey(template);
        string until = now.AddSeconds(recurring.CooldownSeconds).ToString("o", CultureInfo.InvariantCulture);
        if (recurring.IsPerAccount)
        {
            MissionAccountFlagData flag = tx.GetAccountFlag(tx.AccountKey, key)
                ?? new MissionAccountFlagData { AccountKey = tx.AccountKey, FlagKey = key, CreatedAtUtcTicks = now.Ticks };
            flag.Value = until;
            flag.SourceQuestId = template.Hash;
            flag.UpdatedAtUtcTicks = now.Ticks;
            tx.SaveAccountFlag(tx.AccountKey, flag);
            return;
        }

        var owner = new MissionKeyData(player.Identity.Instance, key);
        MissionFlagData characterFlag = tx.GetFlag(owner, CooldownUntilFlag)
            ?? new MissionFlagData { CharacterId = player.Identity.Instance, QuestId = key, FlagKey = CooldownUntilFlag, CreatedAtUtcTicks = now.Ticks };
        characterFlag.Value = until;
        characterFlag.UpdatedAtUtcTicks = now.Ticks;
        tx.SaveFlag(owner, characterFlag);
    }

    string Account(Player player)
    {
        string account = _completionDao!.ResolveCharacterAccountKey(player.Identity.Instance);
        return string.IsNullOrWhiteSpace(account)
            ? throw new InvalidOperationException("Character " + player.Identity.Instance + " has no account for an account-wide quest cooldown.")
            : account;
    }

    static string CooldownText(QuestRecurring recurring, TimeSpan left)
    {
        int minutes = Math.Max(1, (int)Math.Ceiling(left.TotalMinutes));
        string time = minutes >= 60
            ? string.Format(CultureInfo.InvariantCulture, "{0}h {1}m", minutes / 60, minutes % 60)
            : string.Format(CultureInfo.InvariantCulture, "{0}m", minutes);
        return (string.IsNullOrWhiteSpace(recurring.CooldownText) ? DefaultCooldownText : recurring.CooldownText).Replace("{time}", time);
    }
}
