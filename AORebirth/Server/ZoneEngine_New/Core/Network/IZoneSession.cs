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

        void BindPlayer(Player player);

        /// <summary>Clears session→player without world teardown (used after LinkDead / steal / despawn).</summary>
        void UnbindPlayer();

        void TransferToPlayfield(Playfield destination, Vector3 landing);

        void Send(byte[] packet);

        void Send(Message message);

        void Send(MessageBody body);

        void Send(MessageBody body, int sender, int receiver);

        void SendInitiateCompression();

        void Close();
    }
}
