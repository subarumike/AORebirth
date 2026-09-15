namespace ZoneEngine.Core.Missions;
internal static class MissionAcgCapturedLayoutCatalog
{
    internal static MissionAcgLayoutBundle[] CreateBundles() => MissionContentJson.Read<MissionAcgLayoutBundle[]>("Layouts.json");
}
