namespace ZoneEngine_New.Core.Dialogue;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Trade;

/// <summary>
/// Character/NPC-owned conversations. Inbound work and Tick run on the existing playfield owner;
/// packet pacing is an owner-tick deadline, not a sleeping handler or background callback.
/// No team, account, captured number or display name can inherit another conversation.
/// </summary>
public sealed class DialogueService(DialogueCatalog catalog, DialogueActionRouter actions, Func<long>? milliseconds = null)
{
    const int PacketPacingMilliseconds = 20; // ContentDrivenNpcDialogueRouter.KnuBotPacketPacingMilliseconds.
    readonly Func<long> _milliseconds = milliseconds ?? (() => Environment.TickCount64);
    readonly ConcurrentDictionary<int, Conversation> _sessions = new();
    readonly ConditionalWeakTable<Player, object> _tailorHistory = new();

    sealed class Conversation(Player player, IZoneSession transport, NpcCharacter npc, Playfield playfield,
        string contentIdentity, DialogueSession dialogue)
    {
        public readonly Player Player = player;
        public readonly IZoneSession Transport = transport;
        public readonly NpcCharacter Npc = npc;
        public readonly Playfield Playfield = playfield;
        public readonly string ContentIdentity = contentIdentity;
        public DialogueSession Dialogue = dialogue;
        public readonly Queue<MessageBody> Packets = new();
        public int[] WireOptions = [];
        public long NextPacketAt;
        public bool Closing;
        public DialogueActionOutcome Trade;
        public Identity StagedSlot;
        public Item? StagedItem;
    }

    public bool Open(IZoneSession transport, Identity target)
    {
        if (!Current(transport, out var player, out var playfield)) return false;
        lock (player.PersistenceGate)
        {
            if (!Current(transport, out _, out _) || !playfield.GetRequiredService<DynelRegistry>().TryGet(target, out var dynel)
                || dynel is not NpcCharacter npc || !TryCapability(player, npc, out var binding)
                || !catalog.TryGet(binding.ContentNpcIdentity, out _)) return false;
            if (_sessions.TryGetValue(player.Identity.Instance, out var previous))
            {
                if (Valid(previous)) return false; // Duplicate opens cannot reset a live trade or quest transition.
                Remove(previous);
            }
            bool priorOpen = _tailorHistory.TryGetValue(player, out _);
            if (!actions.TryOpen(player, binding.ContentNpcIdentity, priorOpen, out string? startNode)) return false;
            var result = catalog.Sessions.StartSessionAtNode(binding.ContentNpcIdentity, startNode);
            if (!SafeResult(result)) return false;
            var conversation = new Conversation(player, transport, npc, playfield, binding.ContentNpcIdentity, result.Session);
            if (!_sessions.TryAdd(player.Identity.Instance, conversation)) return false;
            if (binding.ContentNpcIdentity == DialogueActionRouter.Tailor) _tailorHistory.GetValue(player, _ => new object());
            conversation.Packets.Enqueue(DialogueWire.Open(player.Identity, target, binding.ContentNpcIdentity));
            QueueNode(conversation, result);
            Pump(conversation);
            return true;
        }
    }

    public bool Answer(IZoneSession transport, Identity target, int wireAnswer)
    {
        if (!TryConversation(transport, target, out var conversation)) return false;
        lock (conversation.Player.PersistenceGate)
        {
            if (!Valid(conversation) || conversation.Closing || conversation.Packets.Count != 0
                || conversation.Trade != DialogueActionOutcome.Continue || wireAnswer < 0
                || wireAnswer >= conversation.WireOptions.Length) return false;
            int contentAnswer = conversation.WireOptions[wireAnswer];
            var next = catalog.Sessions.SelectOption(DialogueCatalog.Copy(conversation.Dialogue), contentAnswer);
            if (!SafeResult(next)) return false;
            var effect = actions.ApplyAnswer(conversation.Player, conversation.ContentIdentity,
                conversation.Dialogue.CurrentNodeId, contentAnswer);
            if (effect == DialogueActionOutcome.Rejected || !Valid(conversation)) return false;
            if (effect == DialogueActionOutcome.Vendor)
            {
                if (!TryCapability(conversation.Player, conversation.Npc, out var binding) || !binding.HasVendor
                    || conversation.Npc.Shop is not { } shop
                    || !conversation.Playfield.GetRequiredService<TradeService>().TryOpenShop(conversation.Player, shop)) return false;
            }
            conversation.Dialogue = next.Session;
            conversation.WireOptions = [];
            if (effect is DialogueActionOutcome.StanTrade or DialogueActionOutcome.DojaTrade or DialogueActionOutcome.SarahTrade)
            {
                conversation.Trade = effect;
                // The accepted trade hold has no AppendText/AnswerList after StartTrade: those remove Accept/slots.
                conversation.Packets.Enqueue(DialogueWire.StartTrade(conversation.Player.Identity, target, effect));
            }
            else QueueNode(conversation, next);
            Pump(conversation);
            return true;
        }
    }

    public bool Close(IZoneSession transport, Identity target)
    {
        if (!TryConversation(transport, target, out var conversation)) return false;
        lock (conversation.Player.PersistenceGate)
        {
            if (!Valid(conversation)) { Remove(conversation); return false; }
            // Source items were never moved into a temporary bag, so close has no inventory rollback or write.
            Remove(conversation);
            transport.Send(DialogueWire.Close(conversation.Player.Identity, target));
            return true;
        }
    }

    public bool StageTrade(IZoneSession transport, KnuBotTradeMessage message)
    {
        if (message == null || !TryConversation(transport, message.Target, out var conversation)) return false;
        lock (conversation.Player.PersistenceGate)
        {
            if (!ValidTrade(conversation) || message.Container.Type != IdentityType.Inventory
                || !conversation.Player.Inventory.Inventory.Content.TryGetValue(message.Container.Instance, out var item)
                || !item.IsPersisted || item.Locked || item.StackCount != 1 || InventoryMoveService.IsBagItem(item)) return false;
            int expectedTemplate = conversation.Trade switch
            {
                DialogueActionOutcome.StanTrade => 248306,
                DialogueActionOutcome.SarahTrade => 295618,
                DialogueActionOutcome.DojaTrade => ZoneEngine.Core.Doja.DojaChipInteractionRules.NascenseChipItemId,
                _ => 0
            };
            if (expectedTemplate == 0 || (item.LowId != expectedTemplate && item.HighId != expectedTemplate)) return false;
            // Exact object and exact slot are revalidated at FinishTrade and again by the durable service.
            conversation.StagedSlot = message.Container;
            conversation.StagedItem = item;
            return true;
        }
    }

    public bool FinishTrade(IZoneSession transport, KnuBotFinishTradeMessage message)
    {
        if (message == null || !TryConversation(transport, message.Target, out var conversation)) return false;
        lock (conversation.Player.PersistenceGate)
        {
            if (!ValidTrade(conversation)) return false;
            if (message.Decline != 0)
            {
                Remove(conversation);
                transport.Send(DialogueWire.AcceptedTrade(conversation.Player.Identity, message.Target));
                return true;
            }
            var item = conversation.StagedItem;
            if (item == null || !conversation.Player.Inventory.Inventory.Content.TryGetValue(conversation.StagedSlot.Instance, out var current)
                || !ReferenceEquals(item, current)) return false;
            if (!actions.CompleteTrade(conversation.Player, conversation.Trade, conversation.StagedSlot, item, () =>
                {
                    if (!Valid(conversation)) throw new InvalidOperationException("Dialogue owner changed during durable NPC trade completion.");
                    transport.Send(DialogueWire.AcceptedTrade(conversation.Player.Identity, message.Target));
                }))
            { if (!Valid(conversation)) Remove(conversation); return false; }
            conversation.StagedItem = null;
            conversation.Trade = DialogueActionOutcome.Continue;
            if (!Valid(conversation)) { Remove(conversation); return true; }
            // Only a known committed trade may traverse the content's hidden continuation.
            var next = catalog.Sessions.SelectOption(DialogueCatalog.Copy(conversation.Dialogue), 0);
            if (!SafeResult(next)) { Remove(conversation); return true; }
            conversation.Dialogue = next.Session;
            QueueNode(conversation, next);
            Pump(conversation);
            return true;
        }
    }

    public void Tick(Playfield playfield)
    {
        foreach (var conversation in _sessions.Values.Where(value => ReferenceEquals(value.Playfield, playfield)))
            lock (conversation.Player.PersistenceGate)
            {
                if (!Valid(conversation)) Remove(conversation);
                else Pump(conversation);
            }
    }

    public void Detached(Player player)
    {
        lock (player.PersistenceGate)
            if (_sessions.TryGetValue(player.Identity.Instance, out var conversation) && ReferenceEquals(conversation.Player, player)) Remove(conversation);
    }

    public void Detached(NpcCharacter npc)
    {
        foreach (var conversation in _sessions.Values.Where(value => ReferenceEquals(value.Npc, npc)))
            lock (conversation.Player.PersistenceGate) Remove(conversation);
    }

    public void Shutdown(Playfield playfield)
    {
        foreach (var conversation in _sessions.Values.Where(value => ReferenceEquals(value.Playfield, playfield)))
            lock (conversation.Player.PersistenceGate) Remove(conversation);
    }

    public bool HasSession(Player player) => _sessions.TryGetValue(player.Identity.Instance, out var session)
        && ReferenceEquals(session.Player, player);

    void QueueNode(Conversation conversation, DialogueSessionResult result)
    {
        conversation.Trade = DialogueActionOutcome.Continue;
        if (!result.Session.IsActive)
        {
            conversation.WireOptions = [];
            conversation.Closing = true;
            conversation.Packets.Enqueue(DialogueWire.Close(conversation.Player.Identity, conversation.Npc.Identity));
            return;
        }
        conversation.WireOptions = DialogueCatalog.VisibleOptions(result).Select(option => option.Index).ToArray();
        foreach (var packet in DialogueWire.Node(conversation.Player.Identity, conversation.Npc.Identity, conversation.Player.Name, result))
            conversation.Packets.Enqueue(packet);
        if (conversation.WireOptions.Length == 0)
        {
            conversation.Closing = true;
            conversation.Packets.Enqueue(DialogueWire.Close(conversation.Player.Identity, conversation.Npc.Identity));
        }
    }

    void Pump(Conversation conversation)
    {
        if (conversation.Packets.Count == 0 || _milliseconds() < conversation.NextPacketAt) return;
        if (!Valid(conversation)) { Remove(conversation); return; }
        conversation.Transport.Send(conversation.Packets.Dequeue());
        conversation.NextPacketAt = _milliseconds() + PacketPacingMilliseconds;
        if (conversation.Closing && conversation.Packets.Count == 0) Remove(conversation);
    }

    void Remove(Conversation conversation)
    {
        _sessions.TryRemove(new KeyValuePair<int, Conversation>(conversation.Player.Identity.Instance, conversation));
        conversation.Packets.Clear();
        conversation.StagedItem = null;
        conversation.Dialogue.IsActive = false;
    }

    bool TryConversation(IZoneSession transport, Identity target, out Conversation conversation)
    {
        conversation = null!;
        return Current(transport, out var player, out _) && _sessions.TryGetValue(player.Identity.Instance, out conversation!)
            && ReferenceEquals(conversation.Player, player) && ReferenceEquals(conversation.Transport, transport)
            && conversation.Npc.Identity == target;
    }

    bool ValidTrade(Conversation conversation) => Valid(conversation) && !conversation.Closing && conversation.Packets.Count == 0
        && conversation.Trade is DialogueActionOutcome.StanTrade or DialogueActionOutcome.DojaTrade or DialogueActionOutcome.SarahTrade;

    bool Valid(Conversation conversation) => Current(conversation.Transport, out var player, out var playfield)
        && ReferenceEquals(player, conversation.Player) && ReferenceEquals(playfield, conversation.Playfield)
        && TryCapability(player, conversation.Npc, out var binding) && binding.ContentNpcIdentity == conversation.ContentIdentity
        && _sessions.TryGetValue(player.Identity.Instance, out var current) && ReferenceEquals(current, conversation);

    static bool Current(IZoneSession transport, out Player player, out Playfield playfield)
    {
        player = transport?.Player!;
        playfield = player?.Playfield!;
        return transport != null && transport.State == SessionState.InPlay && player != null && playfield != null
            && ReferenceEquals(player.Session, transport) && !player.IsDead && !player.IsPersistenceQuarantined
            && player.Inventory.IsHydrated && playfield.GetRequiredService<DynelRegistry>().TryGet(player.Identity, out var current)
            && ReferenceEquals(current, player);
    }

    static bool TryCapability(Player player, NpcCharacter npc, out AcceptedNpcBinding binding)
    {
        binding = null!;
        return npc.Playfield != null && ReferenceEquals(player.Playfield, npc.Playfield) && !npc.IsDead
            && player.Distance3D(npc) <= LootableDynel.OpenRange
            && npc.Playfield.GetRequiredService<AcceptedNpcActivationService>().TryGetBinding(npc, out binding)
            && binding.HasDialogue && DialogueCatalog.IsEnabled(binding.ContentNpcIdentity);
    }

    static bool SafeResult(DialogueSessionResult result) => result.IsValid && result.Session != null
        && result.RecordedActions.All(action => string.Equals(action.ActionType, "EndDialogue", StringComparison.OrdinalIgnoreCase));
}
