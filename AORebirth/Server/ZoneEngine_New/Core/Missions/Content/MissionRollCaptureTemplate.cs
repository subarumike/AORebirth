namespace ZoneEngine.Core.Missions
{
    /// <summary>
    /// Captured server->client QuestAlternative response (five rolled missions).
    /// Source: capture 20260717-Mission terminal2, raw-packets.csv line 2319 (seed 0x65F31253).
    /// CapturedPacketHex is the full transport packet; the N3 message body begins
    /// after the 16-byte transport header (TransportHeaderLength).
    /// </summary>
    internal static class MissionRollCaptureTemplate
    {
        public const int TransportHeaderLength = 16;

        public static string CapturedPacketHex => MissionContentJson.Read<string>("RollTemplate.json");
    }
}
