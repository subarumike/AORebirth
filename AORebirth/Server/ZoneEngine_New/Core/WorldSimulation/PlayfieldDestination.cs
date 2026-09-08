namespace ZoneEngine_New.Core.WorldSimulation
{
    /// <summary>
    /// Wall Destinations landing segment (MessagePack Destinations.dat).
    /// Field layout matches AORebirth.Core.Playfields.PlayfieldDestination for MsgPack wire compatibility.
    /// </summary>
    public sealed class PlayfieldDestination
    {
        public int DestinationId = 0;

        public float StartX;

        public float StartY;

        public float StartZ;

        public float EndX;

        public float EndY;

        public float EndZ;
    }
}
