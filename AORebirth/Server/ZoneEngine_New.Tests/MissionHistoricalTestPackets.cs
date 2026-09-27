namespace SmokeLounge.AOtomation.Messaging.Tests;

using System;
using System.IO;
using System.Text.Json;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Missions;

// Historical evidence access is test-only; never put these accessors back on
// GeneratedMissionWire or package these inputs as runtime content.
internal static class MissionHistoricalTestPackets
{
    internal static string[] HexBodies => JsonSerializer.Deserialize<string[]>(Read("RollBodies.json"))!;
    internal static int CapturedCount => HexBodies.Length;
    internal static byte[] CapturedBody(int index) => Convert.FromHexString(HexBodies[index]);
    internal static byte[] TemplateBody => Convert.FromHexString(JsonSerializer.Deserialize<string>(Read("RollTemplate.json"))!)[16..];
    internal static QuestAlternativeMessage DecodeTemplate() => GeneratedMissionWire.Read(TemplateBody);
    static string Read(string file)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AI_START_HERE.md"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("Offline test fixture root missing.");
        return File.ReadAllText(Path.Combine(root.FullName, "AORebirth", "GameData", "Missions", file));
    }
}
