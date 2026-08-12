using System;
using System.IO;

namespace ZoneEngine.Core.Missions
{
    internal static class MissionStateDirectory
    {
        private const string MissionStateEnvironmentVariableName = "AO_REBIRTH_MISSION_STATE_DIR";

        private const string ZoneStateEnvironmentVariableName = "AO_REBIRTH_ZONE_STATE_DIR";

        internal static string Resolve()
        {
            string configured = Environment.GetEnvironmentVariable(MissionStateEnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.GetFullPath(configured);
            }

            configured = Environment.GetEnvironmentVariable(ZoneStateEnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.GetFullPath(configured);
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "mission-state");
        }
    }
}
