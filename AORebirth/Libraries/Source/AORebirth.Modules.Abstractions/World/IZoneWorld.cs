namespace AORebirth.Modules.World
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A read-only view of this zone instance's world (module API 1.2). Only the playfields this ZoneEngine_New process
    /// runs are visible: a player or NPC on another zone instance is not found here.
    /// <para>
    /// Every member is safe to call from any thread and returns detached copies; nothing returned changes after it is
    /// returned, and nothing in it refers to a live entity. Values come from snapshots each playfield publishes at the end
    /// of its tick, so they may be up to one tick stale. Called from a module thread, a read whose playfield's last
    /// snapshot is more than a tick old waits for that playfield's next tick; called from an event handler (already on a
    /// tick) it never waits.
    /// </para>
    /// <para>Needs the <c>read-world</c> capability when the module has a capability entry in Modules.json.</para>
    /// </summary>
    public interface IZoneWorld
    {
        /// <summary>Every player in play on this instance's playfields.</summary>
        IReadOnlyList<PlayerInfo> Players();

        /// <summary>The player with this character id on this instance, or null.</summary>
        PlayerInfo? Player(int characterId);

        /// <summary>Ids of the playfields this instance runs right now.</summary>
        IReadOnlyList<int> Playfields();

        /// <summary>Every spawned NPC on <paramref name="playfieldId"/>; empty when this instance does not run it.</summary>
        IReadOnlyList<NpcInfo> Npcs(int playfieldId);

        /// <summary>The spawned NPC with this identity instance on <paramref name="playfieldId"/>, or null.</summary>
        NpcInfo? Npc(int playfieldId, int instance);

        /// <summary>
        /// The current value of raw stat <paramref name="statId"/> on a player or NPC of this instance, or null when the
        /// entity is not found or does not have the stat. The first read of a stat on an entity subscribes it to the
        /// tick snapshot for a while, so a read from a module thread may wait up to a few ticks.
        /// </summary>
        int? Stat(EntityRef entity, int statId);

        /// <summary>
        /// The connection of the character with this id on this instance (module API 1.5), or null when it is not in play
        /// here or is link-dead. Read directly, never from a snapshot, so it never waits. Needs <c>read-sessions</c>,
        /// not <c>read-world</c>: it carries the account and address.
        /// </summary>
        SessionInfo? Session(int characterId);

        /// <summary>
        /// The locks running on this player now (module API 1.6), including those restored at login or a zone change;
        /// empty when the character is not in play here. Snapshot rules as <see cref="Stat"/>. Needs <c>observe-actions</c>.
        /// </summary>
        IReadOnlyList<CooldownInfo> Cooldowns(int characterId);

        /// <summary>
        /// Messages counted on this connection since it opened, by N3 message type and direction (module API 1.6), or null
        /// when the connection is not on this instance. Live counters, never a snapshot, so it never waits. Needs
        /// <c>read-sessions</c>.
        /// </summary>
        IReadOnlyDictionary<int, MessageCount>? MessageCounts(Guid sessionId);
    }

    /// <summary>A lock running on a player (module API 1.6).</summary>
    public sealed record CooldownInfo(Events.CooldownKey Key, TimeSpan Duration, long Deadline);

    /// <summary>Messages of one type on one connection, each way (module API 1.6).</summary>
    public readonly record struct MessageCount(long Inbound, long Outbound);

    /// <summary>An item by its template ids and quality (module API 1.6); default for none.</summary>
    public readonly record struct ItemRef(int LowId, int HighId, int Quality);

    /// <summary>A character's connection (module API 1.5).</summary>
    public sealed record SessionInfo
    {
        /// <summary>The connection, the same id as <see cref="Network.MessageSample.SessionId"/>.</summary>
        public Guid SessionId { get; init; }

        public int CharacterId { get; init; }

        /// <summary>The account that owns the character, or empty when the zone does not know it.</summary>
        public string AccountName { get; init; } = string.Empty;

        /// <summary>The client's IP address, or empty once the socket is gone.</summary>
        public string RemoteAddress { get; init; } = string.Empty;

        /// <summary>When this connection was accepted (a zone change opens a new one).</summary>
        public DateTime ConnectedAtUtc { get; init; }
    }

    /// <summary>An entity identity: the AO identity type (50000 for characters) and instance.</summary>
    public readonly record struct EntityRef(int Type, int Instance)
    {
        /// <summary>The identity type of players and NPCs.</summary>
        public const int CharacterType = 50000;

        /// <summary>A player or NPC.</summary>
        public static EntityRef Character(int instance) => new(CharacterType, instance);
    }

    /// <summary>A world position.</summary>
    public readonly record struct WorldPosition(float X, float Y, float Z);

    /// <summary>A player as one playfield tick last saw them.</summary>
    public sealed record PlayerInfo
    {
        public int CharacterId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string FirstName { get; init; } = string.Empty;

        public string LastName { get; init; } = string.Empty;

        public int PlayfieldId { get; init; }

        public WorldPosition Position { get; init; }

        public float HeadingDegrees { get; init; }

        public int Level { get; init; }

        public int Health { get; init; }

        public int MaxHealth { get; init; }

        public int Nano { get; init; }

        public int MaxNano { get; init; }

        public int ProfessionId { get; init; }

        public int BreedId { get; init; }

        /// <summary>The character's GM level; 0 for an ordinary player.</summary>
        public int GmLevel { get; init; }

        public bool IsDead { get; init; }

        /// <summary>The selected target, or default when none.</summary>
        public EntityRef Target { get; init; }

        /// <summary>False while link-dead (the character stays in the world without a connection).</summary>
        public bool IsConnected { get; init; }

        /// <summary>Fighting, or on an enemy's hate list (module API 1.6).</summary>
        public bool InCombat { get; init; }

        /// <summary>Sitting (module API 1.6).</summary>
        public bool IsSitting { get; init; }

        /// <summary>The motor has the character moving (module API 1.6).</summary>
        public bool IsMoving { get; init; }

        /// <summary>
        /// The team the character is in, or 0 when none (module API 1.7). Two players share it exactly when they are in
        /// the same team; it is stable while that team exists.
        /// </summary>
        public int TeamId { get; init; }

        /// <summary>The character leads its team; false when not in one (module API 1.7).</summary>
        public bool TeamLeader { get; init; }

        /// <summary>The character's organization id, or 0 when none (module API 1.7).</summary>
        public int OrgId { get; init; }
    }

    /// <summary>A spawned NPC as one playfield tick last saw it.</summary>
    public sealed record NpcInfo
    {
        public int Instance { get; init; }

        /// <summary>The NPC's template hash, or empty when it has none.</summary>
        public string TemplateHash { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public int Level { get; init; }

        public int PlayfieldId { get; init; }

        public WorldPosition Position { get; init; }

        public bool IsDead { get; init; }

        /// <summary>A Knubot script talks for this NPC, so using it opens the Knubot chat.</summary>
        public bool HasKnubotScript { get; init; }

        /// <summary>When the NPC spawned, on <see cref="IZoneClock"/> (module API 1.6).</summary>
        public long SpawnedAt { get; init; }

        /// <summary>The NPC attacks players on sight (module API 1.6).</summary>
        public bool Hostile { get; init; }
    }
}
