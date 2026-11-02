namespace AORebirth.Modules.World
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>What an operation acts on (module API 1.5).</summary>
    public enum TargetKind
    {
        None,
        Character,
        Account,
        Npc,
        Playfield
    }

    /// <summary>The target of <see cref="IZoneActions.Invoke"/>.</summary>
    public readonly record struct OperationTarget(TargetKind Kind, int Id = 0, int PlayfieldId = 0, string Account = "")
    {
        public static OperationTarget None => new(TargetKind.None);

        /// <summary>A player, by character id.</summary>
        public static OperationTarget Character(int characterId) => new(TargetKind.Character, characterId);

        /// <summary>A login account, by name.</summary>
        public static OperationTarget ForAccount(string account) => new(TargetKind.Account, Account: account ?? string.Empty);

        /// <summary>A spawned NPC, by playfield and identity instance.</summary>
        public static OperationTarget Npc(int playfieldId, int instance) => new(TargetKind.Npc, instance, playfieldId);

        public static OperationTarget Playfield(int playfieldId) => new(TargetKind.Playfield, playfieldId, playfieldId);
    }

    /// <summary>The value types an operation parameter can have.</summary>
    public enum OperationValueType
    {
        Int,
        Float,
        Bool,
        String
    }

    public sealed record OperationParameter(string Name, OperationValueType Type, bool Required = true, string Summary = "");

    /// <summary>
    /// One operation this engine offers (module API 1.5). <see cref="Capability"/> is what a module's Modules.json
    /// capability entry must list to invoke it; <see cref="Granted"/> says whether the calling module has it.
    /// </summary>
    public sealed record OperationDescriptor(
        string Name,
        TargetKind Target,
        IReadOnlyList<OperationParameter> Parameters,
        string Capability,
        string Summary)
    {
        public bool Granted { get; init; } = true;
    }

    /// <summary>Named arguments for <see cref="IZoneActions.Invoke"/>: int, float, bool and string values.</summary>
    public sealed class OperationArgs
    {
        readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);

        public static OperationArgs Empty => new();

        public static OperationArgs Of(params (string Name, object Value)[] values)
        {
            var args = new OperationArgs();
            foreach ((string name, object value) in values ?? Array.Empty<(string, object)>())
                args.Set(name, value);
            return args;
        }

        public IEnumerable<string> Names => _values.Keys;

        public OperationArgs Set(string name, object value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(value);
            if (value is not (int or float or double or bool or string))
                throw new ArgumentException("An operation argument is an int, float, bool or string: " + name);
            _values[name] = value;
            return this;
        }

        public bool Has(string name) => _values.ContainsKey(name);

        public bool TryGetInt(string name, out int value)
        {
            value = 0;
            if (!_values.TryGetValue(name, out object? raw) || raw is not int number)
                return false;
            value = number;
            return true;
        }

        /// <summary>A float, or an int widened to one.</summary>
        public bool TryGetFloat(string name, out float value)
        {
            value = 0f;
            if (!_values.TryGetValue(name, out object? raw))
                return false;
            switch (raw)
            {
                case float f: value = f; return true;
                case double d: value = (float)d; return true;
                case int i: value = i; return true;
                default: return false;
            }
        }

        public bool TryGetBool(string name, out bool value)
        {
            value = false;
            if (!_values.TryGetValue(name, out object? raw) || raw is not bool flag)
                return false;
            value = flag;
            return true;
        }

        public bool TryGetString(string name, out string value)
        {
            value = string.Empty;
            if (!_values.TryGetValue(name, out object? raw) || raw is not string text)
                return false;
            value = text;
            return true;
        }

        public string GetString(string name, string fallback = "") => TryGetString(name, out string value) ? value : fallback;

        public override string ToString()
        {
            var parts = new List<string>();
            foreach ((string name, object value) in _values)
                parts.Add(name + "=" + Convert.ToString(value, CultureInfo.InvariantCulture));
            return string.Join(" ", parts);
        }
    }

    /// <summary>
    /// What an operation did. <see cref="Code"/> is <c>ok</c> on success, otherwise one of <see cref="OperationCodes"/>;
    /// <see cref="Message"/> says why, for logs.
    /// </summary>
    public sealed record OperationResult(bool Ok, string Code, string Message = "")
    {
        public static OperationResult Success(string message = "") => new(true, OperationCodes.Ok, message);

        public static OperationResult Fail(string code, string message) => new(false, code, message);
    }

    public static class OperationCodes
    {
        public const string Ok = "ok";

        /// <summary>No operation has that name on this engine.</summary>
        public const string UnknownOperation = "unknown_operation";

        /// <summary>The target is the wrong kind for the operation.</summary>
        public const string BadTarget = "bad_target";

        /// <summary>A required argument is missing, has the wrong type, or is out of range.</summary>
        public const string BadArgs = "bad_args";

        /// <summary>The target is not on this zone instance (or not in play).</summary>
        public const string NotHere = "not_here";

        /// <summary>The operation ran and could not complete.</summary>
        public const string Failed = "failed";
    }

    /// <summary>Names of the operations this engine registers (module API 1.5).</summary>
    public static class OperationNames
    {
        /// <summary>Sends the NPC's spawn extras again (same as <see cref="IZoneActions.RefreshNpc"/>).</summary>
        public const string NpcRefresh = "npc.refresh";

        /// <summary>Respawns the NPC's spawn point now (same as <see cref="IZoneActions.RespawnNpc"/>).</summary>
        public const string NpcRespawn = "npc.respawn";

        /// <summary>A line of system text in the player's chat window: <c>text</c>.</summary>
        public const string PlayerMessage = "player.message";

        /// <summary>A client feedback message by id: <c>id</c>.</summary>
        public const string PlayerFeedback = "player.feedback";

        /// <summary>Moves the player: <c>playfield</c>, <c>x</c>, <c>y</c>, <c>z</c>.</summary>
        public const string PlayerTeleport = "player.teleport";

        /// <summary>Logs the player out, with the normal logout save: <c>reason</c>.</summary>
        public const string PlayerKick = "player.kick";

        /// <summary>
        /// Bans the account permanently: <c>reason</c>. From 1.8 it is recorded as a sanction (see
        /// <see cref="IZoneActions.AccountSanctions"/>) and the account's characters in play are removed.
        /// </summary>
        public const string AccountBan = "account.ban";

        /// <summary>
        /// Suspends the account for <c>seconds</c> (60 to ten years), with an optional <c>reason</c> (module API 1.8). It
        /// cannot log in until the time passes or <see cref="AccountUnban"/> lifts it; its characters in play are removed.
        /// </summary>
        public const string AccountSuspend = "account.suspend";

        /// <summary>Lifts every active suspension and ban of the account: optional <c>reason</c> (module API 1.8).</summary>
        public const string AccountUnban = "account.unban";

        /// <summary>
        /// Shuts this zone instance down gracefully after a countdown every online player is told about (module API 1.7):
        /// <c>seconds</c>, optional <c>message</c> (shown after each countdown line) and <c>reason</c> (for the log).
        /// Scheduling again replaces the countdown. Target <see cref="TargetKind.None"/>.
        /// </summary>
        public const string ServerShutdown = "server.shutdown";

        /// <summary>Cancels a scheduled shutdown and tells players (module API 1.7); fails when none is scheduled.</summary>
        public const string ServerShutdownAbort = "server.shutdown-abort";
    }
}
