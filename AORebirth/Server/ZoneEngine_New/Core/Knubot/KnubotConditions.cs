namespace ZoneEngine_New.Core.Knubot;

using System;
using System.Collections.Generic;
using System.Globalization;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Quests;

/// <summary>The player and NPC one conversation step is evaluated for.</summary>
public sealed class KnubotContext(Player player, NpcCharacter npc, KnubotScript script, bool met, QuestService quests)
{
    public Player Player { get; } = player;
    public NpcCharacter Npc { get; } = npc;
    public KnubotScript Script { get; } = script;

    /// <summary>The player opened this script's chat before (this server run only; durable flags are not decided yet).</summary>
    public bool Met { get; } = met;

    public QuestService Quests { get; } = quests;
}

/// <summary>Catalogs a script is resolved against when it loads.</summary>
public sealed class KnubotLoadContext(QuestCatalog quests, IGameData gameData, IItemTemplateCatalog items)
{
    public QuestCatalog Quests { get; } = quests;
    public IGameData GameData { get; } = gameData;
    public IItemTemplateCatalog Items { get; } = items;

    /// <summary>Every AOID an item hash can produce, so carried items match whatever QL they were minted at.</summary>
    public bool TryItemTemplates(string hash, out HashSet<int> templateIds)
    {
        templateIds = new HashSet<int>();
        if (string.IsNullOrEmpty(hash) || !GameData.CanResolveItemHash(hash))
            return false;

        var leaves = new List<HashInstance>();
        GameData.CollectHashLeafInstances(hash, leaves);
        foreach (HashInstance leaf in leaves)
            templateIds.UnionWith(leaf.TemplateIds);

        return templateIds.Count > 0;
    }
}

/// <summary>
/// One check from an <c>If</c> list. Prefix <c>!</c> to negate. Forms:
/// <c>quest HASH none|active|done|failed|expired|abandoned</c>, <c>has ITEMHASH [count]</c>,
/// <c>level MIN [MAX]</c>, <c>stat NAME = != &lt; &lt;= &gt; &gt;= VALUE</c>, <c>met</c>.
/// </summary>
public abstract record KnubotCondition(bool Negated)
{
    public bool Passes(KnubotContext context) => Test(context) != Negated;

    protected abstract bool Test(KnubotContext context);

    public static bool All(KnubotCondition[] conditions, KnubotContext context)
    {
        foreach (KnubotCondition condition in conditions)
        {
            if (!condition.Passes(context))
                return false;
        }

        return true;
    }

    public static bool TryParse(string text, KnubotLoadContext load, out KnubotCondition condition, out string error)
    {
        condition = null!;
        error = string.Empty;
        string trimmed = (text ?? string.Empty).Trim();
        bool negated = trimmed.StartsWith('!');
        string[] words = trimmed.TrimStart('!').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            error = "empty condition";
            return false;
        }

        switch (words[0].ToLowerInvariant())
        {
            case "quest":
                if (words.Length != 3 || !load.Quests.TryGet(words[1], out _))
                {
                    error = "expected 'quest HASH state' with a Quests.json hash: " + trimmed;
                    return false;
                }

                if (!TryQuestState(words[2], out QuestState? state))
                {
                    error = "unknown quest state '" + words[2] + "' (none, active, done, failed, expired, abandoned)";
                    return false;
                }

                condition = new KnubotQuestCondition(negated, words[1], state);
                return true;

            case "has":
                int count = 1;
                if (words.Length < 2 || words.Length > 3 || !load.TryItemTemplates(words[1], out HashSet<int> templates)
                    || (words.Length == 3 && !TryPositive(words[2], out count)))
                {
                    error = "expected 'has ITEMHASH [count]' with a known item hash: " + trimmed;
                    return false;
                }

                condition = new KnubotHasCondition(negated, templates, count);
                return true;

            case "level":
                int max = int.MaxValue;
                if (words.Length < 2 || words.Length > 3 || !TryPositive(words[1], out int min)
                    || (words.Length == 3 && (!TryPositive(words[2], out max) || max < min)))
                {
                    error = "expected 'level MIN [MAX]': " + trimmed;
                    return false;
                }

                condition = new KnubotStatCondition(negated, CharacterStat.Level, ">=", min, max);
                return true;

            case "stat":
                if (words.Length != 4 || !TryStat(words[1], out CharacterStat stat) || !IsOperator(words[2])
                    || !int.TryParse(words[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    error = "expected 'stat NAME OP VALUE' (OP is = != < <= > >=): " + trimmed;
                    return false;
                }

                condition = new KnubotStatCondition(negated, stat, words[2], value, null);
                return true;

            case "met":
                if (words.Length != 1)
                {
                    error = "'met' takes no arguments";
                    return false;
                }

                condition = new KnubotMetCondition(negated);
                return true;

            default:
                error = "unknown condition '" + words[0] + "'";
                return false;
        }
    }

    static bool TryQuestState(string word, out QuestState? state)
    {
        state = null;
        switch (word.ToLowerInvariant())
        {
            case "none": return true;
            case "active": state = QuestState.Active; return true;
            case "done": state = QuestState.Completed; return true;
            case "failed": state = QuestState.Failed; return true;
            case "expired": state = QuestState.Expired; return true;
            case "abandoned": state = QuestState.Abandoned; return true;
            default: return false;
        }
    }

    static bool TryStat(string word, out CharacterStat stat)
    {
        if (int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && Enum.IsDefined(typeof(CharacterStat), id))
        {
            stat = (CharacterStat)id;
            return true;
        }

        return Enum.TryParse(word, ignoreCase: true, out stat) && Enum.IsDefined(stat);
    }

    static bool IsOperator(string op) => op is "=" or "!=" or "<" or "<=" or ">" or ">=";

    internal static bool TryPositive(string word, out int value)
        => int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0;
}

/// <summary><see cref="State"/> null means the player never held the quest.</summary>
public sealed record KnubotQuestCondition(bool Negated, string Hash, QuestState? State) : KnubotCondition(Negated)
{
    protected override bool Test(KnubotContext context)
    {
        bool held = context.Quests.GetLog(context.Player).Quests.TryGetValue(Hash, out PlayerQuest? quest);
        return State == null ? !held : held && quest!.State == State;
    }
}

public sealed record KnubotHasCondition(bool Negated, HashSet<int> TemplateIds, int Count) : KnubotCondition(Negated)
{
    protected override bool Test(KnubotContext context) => KnubotItems.CountCarried(context.Player, TemplateIds) >= Count;
}

/// <summary><see cref="Max"/> set: an inclusive range (level); otherwise a comparison.</summary>
public sealed record KnubotStatCondition(bool Negated, CharacterStat Stat, string Operator, int Value, int? Max) : KnubotCondition(Negated)
{
    protected override bool Test(KnubotContext context)
    {
        int current = context.Player.Stats.GetOrZero(Stat);
        if (Max is int max)
            return current >= Value && current <= max;

        return Operator switch
        {
            "=" => current == Value,
            "!=" => current != Value,
            "<" => current < Value,
            "<=" => current <= Value,
            ">" => current > Value,
            _ => current >= Value
        };
    }
}

public sealed record KnubotMetCondition(bool Negated) : KnubotCondition(Negated)
{
    protected override bool Test(KnubotContext context) => context.Met;
}
