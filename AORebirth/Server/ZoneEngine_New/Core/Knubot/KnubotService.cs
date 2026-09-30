namespace ZoneEngine_New.Core.Knubot;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;

/// <summary>
/// Knubot conversations: open picks the first opener whose conditions pass, each line applies its effects
/// (all or none) and then sends its text, and each reply points at the next line. Inbound messages and
/// <see cref="Tick"/> run on the playfield owner; delays are owner-tick deadlines, never sleeps.
/// One conversation per player. Every step revalidates the session, playfield, NPC and range.
/// </summary>
public sealed class KnubotService(KnubotCatalog catalog, KnubotEffectServices effects, IZoneLogger logger, Func<long>? milliseconds = null)
{
    /// <summary>Gap between two KnuBot packets, as in the retail captures.</summary>
    const int PacketPacingMilliseconds = 20;

    /// <summary>A conversation with no answer for this long is closed.</summary>
    const long IdleMilliseconds = 10 * 60 * 1000;

    const int DefaultCloseSeconds = 3;

    readonly Func<long> _milliseconds = milliseconds ?? (() => Environment.TickCount64);
    readonly ConcurrentDictionary<int, Conversation> _sessions = new();
    readonly ConditionalWeakTable<Player, HashSet<string>> _met = new();

    public KnubotCatalog Catalog => catalog;

    /// <summary>
    /// One queued step, in the order written: a packet, an effect the text has reached, or the end of a line
    /// (its replies, goto or close). Each waits <see cref="DelayMilliseconds"/> after the previous one.
    /// </summary>
    sealed class Outgoing
    {
        public MessageBody? Body;
        public KnubotEffect? Effect;
        public KnubotLine? Line;
        public int Chain;
        public int DelayMilliseconds;

        public static Outgoing Packet(MessageBody body, int delay) => new() { Body = body, DelayMilliseconds = delay };

        public static Outgoing Apply(KnubotEffect effect, KnubotLine line, int chain, int delay)
            => new() { Effect = effect, Line = line, Chain = chain, DelayMilliseconds = delay };

        public static Outgoing Finish(KnubotLine line, int chain, int delay)
            => new() { Line = line, Chain = chain, DelayMilliseconds = delay };
    }

    sealed class Conversation(Player player, IZoneSession transport, NpcCharacter npc, Playfield playfield, KnubotScript script, bool met, long now)
    {
        public readonly Player Player = player;
        public readonly IZoneSession Transport = transport;
        public readonly NpcCharacter Npc = npc;
        public readonly Playfield Playfield = playfield;
        public readonly KnubotScript Script = script;
        public readonly bool Met = met;
        public readonly Queue<Outgoing> Packets = new();
        public KnubotReply[] Replies = [];
        public long LastSentAt;
        public long LastActivityAt = now;
        public bool Closing;
    }

    /// <summary>
    /// True when the NPC has a script or the player is already in a Knubot chat (refused); false when the
    /// NPC has nothing to say.
    /// </summary>
    public bool TryOpen(IZoneSession transport, Identity target)
    {
        if (!Current(transport, out Player player, out Playfield playfield))
            return false;

        lock (player.PersistenceGate)
        {
            if (_sessions.TryGetValue(player.Identity.Instance, out Conversation? previous))
            {
                if (Valid(previous))
                    return true;

                Remove(previous);
            }

            if (!playfield.GetRequiredService<DynelRegistry>().TryGet(target, out var dynel) || dynel is not NpcCharacter npc
                || !catalog.TryResolve(npc, out KnubotScript script))
                return false;

            if (!InReach(player, npc))
                return true;

            HashSet<string> met = _met.GetValue(player, _ => new HashSet<string>(StringComparer.Ordinal));
            var conversation = new Conversation(player, transport, npc, playfield, script, met.Contains(script.Id), _milliseconds());
            KnubotContext context = Context(conversation);
            KnubotOpener? opener = script.Openers.FirstOrDefault(x => x.Passes(context));
            if (opener == null)
                return true;

            if (opener.Vicinity != null)
            {
                met.Add(script.Id);
                SayNearby(npc, playfield, opener.Vicinity);
                return true;
            }

            if (!_sessions.TryAdd(player.Identity.Instance, conversation))
                return true;

            met.Add(script.Id);
            conversation.Packets.Enqueue(Outgoing.Packet(new KnuBotOpenChatWindowMessage
            {
                Identity = player.Identity, Target = npc.Identity, Unknown1 = 2, Unknown2 = 1
            }, 0));
            Say(conversation, opener.Say!, 0);
            Pump(conversation);
            return true;
        }
    }

    /// <summary>Say range for players (/say), used for NPC lines spoken aloud.</summary>
    const float VicinitySayRange = 10f;

    /// <summary>The NPC says <paramref name="text"/> aloud to every player within say range.</summary>
    static void SayNearby(NpcCharacter npc, Playfield playfield, string text)
    {
        var message = new ChatTextMessage { Identity = npc.Identity, Text = text, Unknown1 = 0, Unknown2 = 0, Unknown3 = 0 };
        foreach (Player listener in playfield.GetRequiredService<DynelRegistry>().PlayerEntities())
        {
            if (listener.Session != null && listener.GetEdgeDistanceTo(npc) <= VicinitySayRange)
                listener.Session.Send(message);
        }
    }

    /// <summary>True when the answer belongs to this player's Knubot chat with <paramref name="target"/>.</summary>
    public bool TryAnswer(IZoneSession transport, Identity target, int wireAnswer)
    {
        if (!TryConversation(transport, target, out Conversation conversation))
            return false;

        lock (conversation.Player.PersistenceGate)
        {
            // Answers are only taken once the whole line is on screen, and only for a listed reply.
            if (!Valid(conversation) || conversation.Closing || conversation.Packets.Count != 0
                || wireAnswer < 0 || wireAnswer >= conversation.Replies.Length)
                return true;

            KnubotReply reply = conversation.Replies[wireAnswer];
            conversation.LastActivityAt = _milliseconds();
            // State may have changed since the list was sent (trade, another NPC); recheck before acting.
            if (!KnubotCondition.All(reply.If, Context(conversation)))
                return true;

            KnubotReply[] offered = conversation.Replies;
            conversation.Replies = [];
            if (conversation.Script.IsDeadEnd(reply))
            {
                // No content behind this reply yet: say so and put the same choices back.
                conversation.Packets.Enqueue(Outgoing.Packet(Append(conversation, string.Format(CultureInfo.InvariantCulture,
                    "<font color=#FF0000>[Debug] Dead end: '{0}' has no content yet.</font>", Render(conversation, reply.Text)), 0), 0));
                QueueReplies(conversation, offered);
            }
            else if (reply.Goto == KnubotScript.CloseTarget)
                QueueClose(conversation, DefaultCloseSeconds);
            else if (reply.Goto == KnubotScript.OpenerTarget)
            {
                KnubotContext context = Context(conversation);
                KnubotOpener? opener = conversation.Script.Openers.FirstOrDefault(x => x.Passes(context));
                if (opener?.Say == null)
                {
                    if (opener?.Vicinity != null)
                        SayNearby(conversation.Npc, conversation.Playfield, opener.Vicinity);
                    QueueClose(conversation, DefaultCloseSeconds);
                }
                else
                    Say(conversation, opener.Say, 0);
            }
            else
                Say(conversation, reply.Goto, 0);

            Pump(conversation);
            return true;
        }
    }

    /// <summary>True when the close belongs to this player's Knubot chat with <paramref name="target"/>.</summary>
    public bool TryClose(IZoneSession transport, Identity target)
    {
        if (!TryConversation(transport, target, out Conversation conversation))
            return false;

        lock (conversation.Player.PersistenceGate)
        {
            Remove(conversation);
            transport.Send(CloseMessage(conversation, DefaultCloseSeconds));
            return true;
        }
    }

    public bool HasSession(Player player)
        => _sessions.TryGetValue(player.Identity.Instance, out Conversation? conversation) && ReferenceEquals(conversation.Player, player);

    public void Tick(Playfield playfield)
    {
        foreach (Conversation conversation in _sessions.Values)
        {
            if (!ReferenceEquals(conversation.Playfield, playfield))
                continue;

            lock (conversation.Player.PersistenceGate)
            {
                if (!Valid(conversation))
                {
                    // Walked away, NPC died, zoned or logged off: close the window if the player is still here.
                    Remove(conversation);
                    if (Current(conversation.Transport, out Player player, out _) && ReferenceEquals(player, conversation.Player))
                        conversation.Transport.Send(CloseMessage(conversation, 0));
                }
                else if (!conversation.Closing && _milliseconds() - conversation.LastActivityAt > IdleMilliseconds)
                {
                    Remove(conversation);
                    conversation.Transport.Send(CloseMessage(conversation, 0));
                }
                else
                    Pump(conversation);
            }
        }
    }

    public void Shutdown(Playfield playfield)
    {
        foreach (Conversation conversation in _sessions.Values)
        {
            if (ReferenceEquals(conversation.Playfield, playfield))
                lock (conversation.Player.PersistenceGate) Remove(conversation);
        }
    }

    /// <summary>
    /// Checks all of the line's effects, then queues its text and effects in the order written, and its end.
    /// A line whose effects cannot all be applied says its Fail line instead.
    /// </summary>
    void Say(Conversation conversation, string lineId, int chain)
    {
        if (chain > KnubotCatalog.MaxChain || !conversation.Script.Lines.TryGetValue(lineId, out KnubotLine? line))
        {
            logger.Warn(string.Format(CultureInfo.InvariantCulture, "Knubot {0}: chain stopped at line '{1}' for char={2}",
                conversation.Script.Id, lineId, conversation.Player.Identity.Instance));
            QueueClose(conversation, DefaultCloseSeconds);
            return;
        }

        if (!KnubotEffect.CanApplyAll(line.Effects, Context(conversation), effects))
        {
            Fail(conversation, line, chain);
            return;
        }

        int pendingDelay = QueueText(conversation, line, chain);
        conversation.Packets.Enqueue(Outgoing.Finish(line, chain, pendingDelay));
    }

    void Fail(Conversation conversation, KnubotLine line, int chain)
    {
        if (line.Fail != null)
            Say(conversation, line.Fail, chain + 1);
        else
            QueueClose(conversation, DefaultCloseSeconds);
    }

    /// <summary>The line's text and effects are done: continue to its goto, or send its replies, or close.</summary>
    void Finish(Conversation conversation, KnubotLine line, int chain)
    {
        if (line.Goto != null)
        {
            Say(conversation, line.Goto, chain + 1);
            return;
        }

        // Evaluated after the line's effects, so a reply can depend on what the line just gave.
        KnubotContext context = Context(conversation);
        KnubotReply[] visible = line.Replies.Where(x => KnubotCondition.All(x.If, context)).ToArray();
        if (line.CloseSeconds != null || visible.Length == 0)
        {
            QueueClose(conversation, line.CloseSeconds ?? DefaultCloseSeconds);
            return;
        }

        QueueReplies(conversation, [.. visible, KnubotScript.Goodbye]);
    }

    /// <summary>The wire index is the position in this list; hidden replies can never be picked.</summary>
    void QueueReplies(Conversation conversation, KnubotReply[] replies)
    {
        conversation.Replies = replies;
        conversation.Packets.Enqueue(Outgoing.Packet(new KnuBotAnswerListMessage
        {
            Identity = conversation.Player.Identity,
            Target = conversation.Npc.Identity,
            Unknown1 = 2,
            DialogOptions = replies.Select(x => new KnuBotDialogOption { Text = Render(conversation, x.Text) }).ToArray()
        }, 0));
    }

    /// <summary>
    /// One AppendText per run of spoken or emote text; a delay starts a new packet held back that long, and an
    /// effect is queued where it was written. Text is sent as written: the client joins packets without a
    /// separator, so line breaks are the author's \n. Returns a delay left over after the last piece.
    /// </summary>
    int QueueText(Conversation conversation, KnubotLine line, int chain)
    {
        var text = new StringBuilder();
        bool emote = false;
        int delay = 0;

        void Flush()
        {
            string value = text.ToString();
            // Whitespace alone (a \n before a delay) rides along with the next packet.
            if (value.Trim().Length == 0)
                return;

            text.Clear();
            conversation.Packets.Enqueue(Outgoing.Packet(Append(conversation, value, emote ? 1 : 0), delay));
            delay = 0;
        }

        foreach (KnubotPiece piece in line.Pieces)
        {
            switch (piece)
            {
                case KnubotDelayPiece pause:
                    Flush();
                    delay += pause.Milliseconds;
                    break;

                case KnubotEffectPiece effect:
                    Flush();
                    conversation.Packets.Enqueue(Outgoing.Apply(effect.Effect, line, chain, delay));
                    delay = 0;
                    break;

                case KnubotTextPiece part:
                    if (part.Emote != emote)
                    {
                        Flush();
                        emote = part.Emote;
                    }

                    text.Append(Render(conversation, part));
                    break;
            }
        }

        Flush();
        return delay;
    }

    void QueueClose(Conversation conversation, int seconds)
    {
        conversation.Replies = [];
        conversation.Closing = true;
        conversation.Packets.Enqueue(Outgoing.Packet(CloseMessage(conversation, seconds), 0));
    }

    /// <summary>
    /// Runs every step that is due. Packets are paced; an effect or a line end runs as soon as its delay has passed.
    /// </summary>
    void Pump(Conversation conversation)
    {
        while (conversation.Packets.Count != 0)
        {
            long now = _milliseconds();
            Outgoing next = conversation.Packets.Peek();
            int wait = next.Body != null ? Math.Max(PacketPacingMilliseconds, next.DelayMilliseconds) : next.DelayMilliseconds;
            if (now - conversation.LastSentAt < wait)
                return;

            conversation.Packets.Dequeue();
            if (next.Body != null)
            {
                conversation.Transport.Send(next.Body);
                conversation.LastSentAt = now;
                conversation.LastActivityAt = now;
                if (conversation.Closing && conversation.Packets.Count == 0)
                    Remove(conversation);
                return;
            }

            // A served delay counts from here for whatever follows.
            if (next.DelayMilliseconds > 0)
                conversation.LastSentAt = now;

            if (next.Effect == null)
                Finish(conversation, next.Line!, next.Chain);
            else if (!KnubotEffect.TryApply(next.Effect, Context(conversation), effects))
            {
                // The player's state changed since the line started (full inventory, quest taken elsewhere):
                // the rest of the line is dropped and its Fail line is said instead.
                logger.Info(string.Format(CultureInfo.InvariantCulture, "Knubot {0}: {1} no longer applies on line '{2}' for char={3}",
                    conversation.Script.Id, next.Effect, next.Line!.Id, conversation.Player.Identity.Instance));
                conversation.Packets.Clear();
                Fail(conversation, next.Line, next.Chain);
            }
        }
    }

    void Remove(Conversation conversation)
    {
        _sessions.TryRemove(new KeyValuePair<int, Conversation>(conversation.Player.Identity.Instance, conversation));
        conversation.Packets.Clear();
        conversation.Replies = [];
    }

    KnubotContext Context(Conversation conversation)
        => new(conversation.Player, conversation.Npc, conversation.Script, conversation.Met, effects.Quests);

    static string Render(Conversation conversation, KnubotTextPiece[] pieces)
    {
        var text = new StringBuilder();
        foreach (KnubotTextPiece piece in pieces)
            text.Append(Render(conversation, piece));
        return text.ToString().Trim();
    }

    static string Render(Conversation conversation, KnubotTextPiece piece) => piece.Kind switch
    {
        KnubotTextKind.PlayerName => string.IsNullOrWhiteSpace(conversation.Player.Name) ? "stranger" : conversation.Player.Name,
        KnubotTextKind.NpcName => conversation.Npc.Name ?? string.Empty,
        _ => piece.Text
    };

    static KnuBotAppendTextMessage Append(Conversation conversation, string text, int kind) => new()
    {
        Identity = conversation.Player.Identity, Target = conversation.Npc.Identity, Unknown1 = 2, Unknown2 = kind, Text = text
    };

    static KnuBotCloseChatWindowMessage CloseMessage(Conversation conversation, int seconds) => new()
    {
        Identity = conversation.Player.Identity, Target = conversation.Npc.Identity, Unknown = 0, Unknown1 = 2, Seconds = seconds, Unknown3 = 0
    };

    bool TryConversation(IZoneSession transport, Identity target, out Conversation conversation)
    {
        conversation = null!;
        return Current(transport, out Player player, out _) && _sessions.TryGetValue(player.Identity.Instance, out conversation!)
            && ReferenceEquals(conversation.Player, player) && ReferenceEquals(conversation.Transport, transport)
            && conversation.Npc.Identity == target;
    }

    bool Valid(Conversation conversation) => Current(conversation.Transport, out Player player, out Playfield playfield)
        && ReferenceEquals(player, conversation.Player) && ReferenceEquals(playfield, conversation.Playfield)
        && InReach(player, conversation.Npc)
        && _sessions.TryGetValue(player.Identity.Instance, out Conversation? current) && ReferenceEquals(current, conversation);

    static bool InReach(Player player, NpcCharacter npc)
        => npc.Playfield != null && ReferenceEquals(player.Playfield, npc.Playfield) && !npc.IsDead
            && player.GetEdgeDistanceTo(npc) <= LootableDynel.OpenRange;

    static bool Current(IZoneSession transport, out Player player, out Playfield playfield)
    {
        player = transport?.Player!;
        playfield = player?.Playfield!;
        return transport != null && transport.State == SessionState.InPlay && player != null && playfield != null
            && ReferenceEquals(player.Session, transport) && !player.IsDead && !player.IsPersistenceQuarantined
            && player.Inventory.IsHydrated && playfield.GetRequiredService<DynelRegistry>().TryGet(player.Identity, out var current)
            && ReferenceEquals(current, player);
    }
}
