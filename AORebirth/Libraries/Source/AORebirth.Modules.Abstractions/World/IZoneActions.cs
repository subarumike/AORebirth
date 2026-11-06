namespace AORebirth.Modules.World
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Things a module may ask this zone instance to do (module API 1.2). Each action is queued onto the playfield tick
    /// that owns its target and runs there; the returned task completes after it ran (or with false when it could not).
    /// No call blocks the caller or takes an engine lock, so these are safe from any thread. Awaiting one inside an event
    /// handler is pointless: the handler already runs on a tick, and the action runs on a later one.
    /// Only this instance's players and NPCs can be reached.
    /// </summary>
    public interface IZoneActions
    {
        /// <summary>
        /// Sends the NPC's spawn extras again to everyone who sees it: its shop pane and whether it talks through a Knubot
        /// script (HasKnubotData 1, or 0 when it no longer does). False when the NPC is not spawned on that playfield.
        /// Needs <c>refresh-npc</c>.
        /// </summary>
        Task<bool> RefreshNpc(int playfieldId, int instance);

        /// <summary>A line of system text in the player's chat window. False when the player is not in play here. Needs <c>send-messages</c>.</summary>
        Task<bool> SendSystemMessage(int characterId, string text);

        /// <summary>A client feedback message by id (the client's text table). False when the player is not in play here. Needs <c>send-messages</c>.</summary>
        Task<bool> SendFeedback(int characterId, int feedbackId);

        /// <summary>
        /// Removes the NPC as if it died (no corpse, loot or XP) and spawns its spawn point again at once, skipping the
        /// respawn timer and chance (module API 1.3). The new NPC is built from the current catalogs, so it picks up a
        /// GameData reload. Every NPC of that spawn point respawns together. False when the NPC is not spawned on that
        /// playfield or has no spawn point that respawns (GM-spawned, pets, content placements, quest objectives); it is
        /// then left in place. Needs <c>respawn-npc</c>.
        /// </summary>
        Task<bool> RespawnNpc(int playfieldId, int instance);

        /// <summary>
        /// Every operation this engine offers (module API 1.5), with its parameters and the capability it needs.
        /// <see cref="OperationDescriptor.Granted"/> says whether this module may invoke it.
        /// </summary>
        IReadOnlyList<OperationDescriptor> Operations { get; }

        /// <summary>
        /// Runs the named operation (module API 1.5; names in <see cref="OperationNames"/>). Player and NPC operations run on
        /// the target's playfield tick, account operations on a worker thread; the task completes after it ran. Never throws
        /// for an unknown name, a wrong target or bad arguments: the result says why. Throws
        /// <see cref="System.UnauthorizedAccessException"/> when the module is not granted the operation's capability.
        /// </summary>
        Task<OperationResult> Invoke(string operation, OperationTarget target, OperationArgs args);

        /// <summary>
        /// The account's suspensions and bans, newest first, active or not (module API 1.8): at most <paramref name="limit"/>
        /// (1 to 500). Empty when it has none or no account has that name. Reads the database on a worker thread. Needs
        /// <c>read-sanctions</c>.
        /// </summary>
        Task<IReadOnlyList<AccountSanctionInfo>> AccountSanctions(string account, int limit = 20);
    }

    /// <summary>
    /// One account suspension or ban (module API 1.8). Times are UTC. <see cref="EndsAtUtc"/> is null for a permanent ban.
    /// <see cref="IssuedBy"/> is <c>gm:Name</c> for a GM command and <c>module:Id</c> for a module.
    /// </summary>
    public sealed record AccountSanctionInfo(
        long Id,
        string Account,
        System.DateTime IssuedAtUtc,
        System.DateTime? EndsAtUtc,
        string IssuedBy,
        string Reason,
        System.DateTime? LiftedAtUtc,
        string? LiftedBy,
        string? LiftReason,
        bool Active)
    {
        public bool Permanent => EndsAtUtc == null;
    }

    /// <summary>Capability names for the "Capabilities" map in Config/Modules.json.</summary>
    public static class ModuleCapabilities
    {
        /// <summary><see cref="IZoneWorld"/>.</summary>
        public const string ReadWorld = "read-world";

        /// <summary><see cref="IZoneActions.RefreshNpc"/>.</summary>
        public const string RefreshNpc = "refresh-npc";

        /// <summary><see cref="IZoneActions.SendSystemMessage"/> and <see cref="IZoneActions.SendFeedback"/>.</summary>
        public const string SendMessages = "send-messages";

        /// <summary><see cref="Content.IZoneGameData.Reload"/> (module API 1.3).</summary>
        public const string GameDataReload = "gamedata-reload";

        /// <summary><see cref="IZoneActions.RespawnNpc"/> (module API 1.3).</summary>
        public const string RespawnNpc = "respawn-npc";

        /// <summary><see cref="Network.IMessageObserver"/> (module API 1.5).</summary>
        public const string ObserveMessages = "observe-messages";

        /// <summary><see cref="IZoneWorld.Session"/> (module API 1.5) and <see cref="IZoneWorld.MessageCounts"/> (1.6).</summary>
        public const string ReadSessions = "read-sessions";

        /// <summary>The action and cooldown events (module API 1.6).</summary>
        public const string ObserveActions = "observe-actions";

        /// <summary><see cref="Events.IVisibilityObserver"/> (module API 1.6).</summary>
        public const string ObserveVisibility = "observe-visibility";

        /// <summary><see cref="Events.IZoneEvents.PlayerSpoke"/> without the text (module API 1.6).</summary>
        public const string ObserveChat = "observe-chat";

        /// <summary>The text of <see cref="Events.IZoneEvents.PlayerSpoke"/> (module API 1.6).</summary>
        public const string ReadChatText = "read-chat-text";

        /// <summary><see cref="IZoneActions.AccountSanctions"/> (module API 1.8).</summary>
        public const string ReadSanctions = "read-sanctions";
    }
}
