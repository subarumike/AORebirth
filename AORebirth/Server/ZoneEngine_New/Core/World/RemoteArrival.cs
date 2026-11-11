namespace ZoneEngine_New.Core.World
{
    using System;

    using AORebirth.World.Contracts.V1;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Where a move lands when its destination runs on another zone instance. The destination snaps the landing to
    /// its floor when the character spawns. <see cref="Prepare"/> runs on the player's tick just before the move
    /// (a respawn revives first); false cancels it.
    /// </summary>
    public sealed record RemoteArrival(Vector3 Landing, Quaternion? Heading = null, ArrivalKind Kind = ArrivalKind.ArrivalZone,
        int ExitProxyDoor = 0, Func<bool>? Prepare = null, bool ClearReturnDoor = false);

    /// <summary>
    /// A login resolved to a playfield another instance runs (a dungeon that ended, a retired mission world): the
    /// character is saved there and logs in again, which World routes to that instance.
    /// </summary>
    public sealed class LoginRelocationException : InvalidOperationException
    {
        public LoginRelocationException(int playfieldId, Vector3 position)
            : base("Login relocates to playfield " + playfieldId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " on another zone instance.")
        {
            PlayfieldId = playfieldId;
            Position = position;
        }

        public int PlayfieldId { get; }

        public Vector3 Position { get; }
    }

    /// <summary>World runs this playfield on another instance; it must not be built here.</summary>
    public sealed class PlayfieldHostedElsewhereException : InvalidOperationException
    {
        public PlayfieldHostedElsewhereException(int playfieldId, string instance)
            : base("Playfield " + playfieldId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " runs on zone instance " + instance + ".")
        {
            PlayfieldId = playfieldId;
            Instance = instance;
        }

        public int PlayfieldId { get; }

        public string Instance { get; }
    }
}
