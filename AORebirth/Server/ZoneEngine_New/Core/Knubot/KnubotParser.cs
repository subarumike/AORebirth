namespace ZoneEngine_New.Core.Knubot;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using ZoneEngine_New.Core.Inventory;

/// <summary>
/// Turns line text into ordered pieces and effects. Tags:
/// text <c>{name}</c> <c>{npc}</c> <c>{itemref LOW HIGH QL}</c> <c>{emote}…{/emote}</c> <c>{delay SECONDS}</c>;
/// effects (applied when the text reaches them) <c>{givequest HASH}</c> <c>{completequest HASH}</c> <c>{spawn ITEMHASH QL [count]}</c> <c>{shop}</c>; flow <c>{goto LINE}</c> <c>{close [SECONDS]}</c>.
/// <c>{{</c> and <c>}}</c> write literal braces. Reply text allows only the text tags without delay.
/// </summary>
public static class KnubotParser
{
    public const double MaxDelaySeconds = 10;
    public const int MaxItemCount = 10;
    public const int MaxCloseSeconds = 60;

    public sealed class Parsed
    {
        public List<KnubotPiece> Pieces { get; } = new();
        public List<KnubotEffect> Effects { get; } = new();
        public string? Goto { get; set; }
        public int? CloseSeconds { get; set; }
    }

    public static Parsed Parse(string text, bool reply, KnubotLoadContext load, List<string> errors)
    {
        var parsed = new Parsed();
        var literal = new StringBuilder();
        bool emote = false;
        string source = text ?? string.Empty;

        void Flush()
        {
            if (literal.Length == 0)
                return;

            parsed.Pieces.Add(new KnubotTextPiece(KnubotTextKind.Literal, literal.ToString(), emote));
            literal.Clear();
        }

        void Effect(KnubotEffect effect)
        {
            Flush();
            parsed.Pieces.Add(new KnubotEffectPiece(effect));
            parsed.Effects.Add(effect);
        }

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (c == '}' && i + 1 < source.Length && source[i + 1] == '}')
            {
                literal.Append('}');
                i++;
                continue;
            }

            if (c != '{')
            {
                literal.Append(c);
                continue;
            }

            if (i + 1 < source.Length && source[i + 1] == '{')
            {
                literal.Append('{');
                i++;
                continue;
            }

            int end = source.IndexOf('}', i + 1);
            if (end < 0)
            {
                errors.Add("unclosed '{' at character " + i);
                break;
            }

            string body = source.Substring(i + 1, end - i - 1).Trim();
            i = end;
            string[] words = body.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string tag = words.Length == 0 ? string.Empty : words[0].ToLowerInvariant();
            if (reply && tag is not ("name" or "player" or "npc" or "itemref"))
            {
                errors.Add("replies only allow {name}, {npc} and {itemref}: {" + body + "}");
                continue;
            }

            switch (tag)
            {
                case "name":
                case "player":
                    Flush();
                    parsed.Pieces.Add(new KnubotTextPiece(KnubotTextKind.PlayerName, string.Empty, emote));
                    Expect(words, 1, 1, body, errors);
                    break;

                case "npc":
                    Flush();
                    parsed.Pieces.Add(new KnubotTextPiece(KnubotTextKind.NpcName, string.Empty, emote));
                    Expect(words, 1, 1, body, errors);
                    break;

                case "itemref":
                    if (words.Length != 4 || !KnubotCondition.TryPositive(words[1], out int low)
                        || !KnubotCondition.TryPositive(words[2], out int high) || !KnubotCondition.TryPositive(words[3], out int ql))
                    {
                        errors.Add("expected {itemref LOW HIGH QL}: {" + body + "}");
                        break;
                    }

                    if (!load.Items.TryGet(low, out ItemTemplate template))
                    {
                        errors.Add("unknown item " + low + " in {" + body + "}");
                        break;
                    }

                    // The client renders itemref:// anchors as clickable item links.
                    literal.Append(string.Format(CultureInfo.InvariantCulture,
                        "<a href=\"itemref://{0}/{1}/{2}\">{3}</a>", low, high, ql, template.Name));
                    break;

                case "emote":
                    Expect(words, 1, 1, body, errors);
                    if (emote)
                        errors.Add("{emote} inside {emote}");
                    Flush();
                    emote = true;
                    break;

                case "/emote":
                    Expect(words, 1, 1, body, errors);
                    if (!emote)
                        errors.Add("{/emote} without {emote}");
                    Flush();
                    emote = false;
                    break;

                case "delay":
                    if (words.Length != 2 || !double.TryParse(words[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
                        || seconds <= 0 || seconds > MaxDelaySeconds)
                    {
                        errors.Add("expected {delay SECONDS} with 0 < SECONDS <= " + MaxDelaySeconds + ": {" + body + "}");
                        break;
                    }

                    Flush();
                    parsed.Pieces.Add(new KnubotDelayPiece((int)Math.Round(seconds * 1000)));
                    break;

                case "givequest":
                case "completequest":
                    if (words.Length != 2 || !load.Quests.TryGet(words[1], out _))
                    {
                        errors.Add("expected {" + tag + " HASH} with a Quests.json hash: {" + body + "}");
                        break;
                    }

                    Effect(tag == "givequest" ? new KnubotAcceptQuest(words[1]) : new KnubotCompleteQuest(words[1]));
                    break;

                case "spawn":
                    int spawnCount = 1;
                    if (words.Length < 3 || words.Length > 4 || !load.GameData.CanResolveItemHash(words[1])
                        || !KnubotCondition.TryPositive(words[2], out int quality)
                        || (words.Length == 4 && (!KnubotCondition.TryPositive(words[3], out spawnCount) || spawnCount > MaxItemCount)))
                    {
                        errors.Add("expected {spawn ITEMHASH QL [count<=" + MaxItemCount + "]} with a known item hash: {" + body + "}");
                        break;
                    }

                    Effect(new KnubotSpawnItem(words[1], quality, spawnCount));
                    break;

                case "shop":
                    Expect(words, 1, 1, body, errors);
                    Effect(new KnubotOpenShop());
                    break;

                case "goto":
                    if (words.Length != 2 || parsed.Goto != null)
                    {
                        errors.Add("expected one {goto LINE} per line: {" + body + "}");
                        break;
                    }

                    parsed.Goto = words[1];
                    break;

                case "close":
                    int closeSeconds = 3;
                    if (words.Length > 2 || parsed.CloseSeconds != null
                        || (words.Length == 2 && (!int.TryParse(words[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out closeSeconds)
                            || closeSeconds < 0 || closeSeconds > MaxCloseSeconds)))
                    {
                        errors.Add("expected one {close [SECONDS<=" + MaxCloseSeconds + "]} per line: {" + body + "}");
                        break;
                    }

                    parsed.CloseSeconds = closeSeconds;
                    break;

                default:
                    errors.Add("unknown tag {" + body + "}");
                    break;
            }
        }

        if (emote)
            errors.Add("{emote} without {/emote}");

        Flush();
        return parsed;
    }

    static void Expect(string[] words, int min, int max, string body, List<string> errors)
    {
        if (words.Length < min || words.Length > max)
            errors.Add("wrong argument count in {" + body + "}");
    }
}
