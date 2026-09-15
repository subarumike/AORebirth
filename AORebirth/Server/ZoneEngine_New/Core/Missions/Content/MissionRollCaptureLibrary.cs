namespace ZoneEngine.Core.Missions;
internal static class MissionRollCaptureLibrary
{
    internal static string[] CapturedRollBodiesHex => MissionContentJson.Read<string[]>("RollBodies.json");
    internal static int Count => CapturedRollBodiesHex.Length;
}
