namespace ZoneEngine_New.Core.Network
{
    using SmokeLounge.AOtomation.Messaging.Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Playfield;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    public interface IZoneSession
    {
        SessionState State { get; set; }

        Player? Player { get; }

        bool IsClosed => State == SessionState.Closed;

        void BindPlayer(Player player);

        /// <summary>Clears session→player without world teardown (used after LinkDead / steal / despawn).</summary>
        void UnbindPlayer();

        void TransferToPlayfield(Playfield destination, Vector3 landing);

        /// <summary>Transfers with an explicit destination heading; implementations must not silently discard it.</summary>
        void TransferToPlayfield(Playfield destination, Vector3 landing, AORebirth.Core.Vector.Quaternion heading)
            => throw new System.NotSupportedException("This session does not support an explicit transfer heading.");
        /// <summary>
        /// In-zone death respawn: N3Teleport with ChangePlayfield set to the current playfield.
        /// </summary>
        void SendSamePlayfieldRespawnTeleport(Vector3 landing)
            => throw new System.NotSupportedException("This session does not support a same-playfield respawn.");

        /// <summary>
        /// Intrazone LineTeleport: N3Teleport with ChangePlayfield instance 0, which the client
        /// applies as SetRelPosRot without unloading the playfield. Sent to the moving client only.
        /// </summary>
        void SendIntrazoneTeleport(Vector3 landing, AORebirth.Core.Vector.Quaternion heading, int destinationKey)
            => throw new System.NotSupportedException("This session does not support an intrazone teleport.");

        void Send(byte[] packet);

        void Send(Message message);

        void Send(MessageBody body);

        void Send(MessageBody body, int sender, int receiver);

        void SendInitiateCompression();

        void Close();
    }
}
