namespace ZoneEngine.Core.Missions;

using System;

internal static class MissionHistoricalRollFixture
{
    static readonly string[] Bodies = MissionRollCaptureLibrary.CapturedRollBodiesHex;

    internal static int CapturedCount => Bodies.Length;
    internal static byte[] CapturedBody(int index) => Convert.FromHexString(Bodies[index]);
    internal static byte[] TemplateBody => Convert.FromHexString(MissionRollCaptureTemplate.CapturedPacketHex)[MissionRollCaptureTemplate.TransportHeaderLength..];
}
