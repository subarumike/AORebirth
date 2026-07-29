# Assemble MissionRollCaptureLibrary.cs from extracted hex fragment.
from __future__ import print_function

frag = open(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_mission_roll_library.csfrag", encoding="utf-8").read()
# The fragment uses private static readonly — rewrite as public internal const array field.
frag = frag.replace(
    "private static readonly string[] CapturedRollBodiesHex =",
    "        internal static readonly string[] CapturedRollBodiesHex ="
)

out = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Missions\MissionRollCaptureLibrary.cs"
text = """namespace ZoneEngine.Core.Missions
{
    /// <summary>
    /// Library of live server->client QuestAlternative N3 bodies (no transport header).
    /// Source: capture 20260719-Rolling different mishes — 13 distinct 5-offer pulls covering
    /// KillPerson, FindPerson, FindItem, and RepairMachine (Broken Machine) with matching texts.
    /// MissionRollService picks a whole roll so icons stay paired with their captured ShortInfo/Info.
    /// </summary>
    internal static class MissionRollCaptureLibrary
    {
%s
        internal static int Count
        {
            get { return CapturedRollBodiesHex.Length; }
        }
    }
}
""" % frag

open(out, "w", encoding="utf-8", newline="\n").write(text)
print("wrote", out, "bytes", len(text))
