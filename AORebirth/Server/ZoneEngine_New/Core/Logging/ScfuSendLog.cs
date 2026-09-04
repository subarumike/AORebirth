namespace ZoneEngine_New.Core.Logging
{
    using System;
    using System.Globalization;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    /// <summary>Debug dump of SCFU fields (one field per log line) at send time.</summary>
    public static class ScfuSendLog
    {
        public static void Write(SimpleCharFullUpdateMessage scfu)
        {
            if (scfu == null)
                return;

            Line("Identity", FormatIdentity(scfu.Identity));
            Line("Version", scfu.Version);
            Line("Flags", scfu.Flags);
            Line("AdditionalFlags", scfu.AdditionalFlags);
            Line("SuppressedFlags", scfu.SuppressedFlags);
            Line("PlayfieldId", scfu.PlayfieldId);
            Line("FightingTarget", scfu.FightingTarget.HasValue ? FormatIdentity(scfu.FightingTarget.Value) : "null");
            Line(
                "Coordinates",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "({0},{1},{2})",
                    scfu.Coordinates.X,
                    scfu.Coordinates.Y,
                    scfu.Coordinates.Z));
            Line(
                "Heading",
                scfu.Heading == null
                    ? "null"
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "({0},{1},{2},{3})",
                        scfu.Heading.X,
                        scfu.Heading.Y,
                        scfu.Heading.Z,
                        scfu.Heading.W));
            Line(
                "Appearance",
                scfu.Appearance == null
                    ? "null"
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "Value=0x{0:X8} Side={1} Fatness={2} Breed={3} Gender={4} Race={5}",
                        scfu.Appearance.Value,
                        scfu.Appearance.Side,
                        scfu.Appearance.Fatness,
                        scfu.Appearance.Breed,
                        scfu.Appearance.Gender,
                        scfu.Appearance.Race));
            Line("Name", scfu.Name ?? string.Empty);
            Line("CharacterFlags", scfu.CharacterFlags);
            Line("AccountFlags", scfu.AccountFlags);
            Line("Expansions", scfu.Expansions);
            WriteCharacterInfo(scfu.CharacterInfo);
            Line("Level", scfu.Level);
            Line("Health", scfu.Health);
            Line("HealthDamage", scfu.HealthDamage);
            Line("MonsterData", scfu.MonsterData);
            Line("MonsterScale", scfu.MonsterScale);
            Line("VisualFlags", scfu.VisualFlags);
            Line("VisibleTitle", scfu.VisibleTitle);
            Line("Unknown1", FormatBytes(scfu.Unknown1));
            Line("HeadMesh", scfu.HeadMesh);
            Line("RunSpeedBase", scfu.RunSpeedBase);
            Line("Flags2", scfu.Flags2);
            Line("OwnerInstance", scfu.OwnerInstance);
            Line("Unknown2", scfu.Unknown2);
            Line("Unknown4", scfu.Unknown4);
            Line("ScfuTowerUnk", scfu.ScfuTowerUnk);
            Line("IsImmunePadding", scfu.IsImmunePadding);
            Line("UnknownFlag3Padding", scfu.UnknownFlag3Padding);
            Line("ExtendedTextureOverrideData", FormatBytes(scfu.ExtendedTextureOverrideData));

            ActiveNano[] nanos = scfu.ActiveNanos ?? [];
            Line("ActiveNanos.Count", nanos.Length);
            for (int i = 0; i < nanos.Length; i++)
            {
                ActiveNano nano = nanos[i];
                Line(
                    "ActiveNanos[" + i + "]",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} instance={1} t1={2} t2={3}",
                        FormatIdentity(nano.NanoIdentity),
                        nano.NanoInstance,
                        nano.Time1,
                        nano.Time2));
            }

            Vector3[] waypoints = scfu.Waypoints ?? [];
            Line("Waypoints.Count", waypoints.Length);
            for (int i = 0; i < waypoints.Length; i++)
            {
                Vector3 wp = waypoints[i];
                Line(
                    "Waypoints[" + i + "]",
                    string.Format(CultureInfo.InvariantCulture, "({0},{1},{2})", wp.X, wp.Y, wp.Z));
            }

            Texture[] textures = scfu.Textures ?? [];
            Line("Textures.Count", textures.Length);
            for (int i = 0; i < textures.Length; i++)
            {
                Texture texture = textures[i];
                Line(
                    "Textures[" + i + "]",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Place={0} Id={1} Unknown={2}",
                        texture.Place,
                        texture.Id,
                        texture.Unknown));
            }

            Mesh[] meshes = scfu.Meshes ?? [];
            Line("Meshes.Count", meshes.Length);
            for (int i = 0; i < meshes.Length; i++)
            {
                Mesh mesh = meshes[i];
                Line(
                    "Meshes[" + i + "]",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Position={0} Id=0x{1:X8} OverrideTextureId={2} Layer={3}",
                        mesh.Position,
                        mesh.Id,
                        mesh.OverrideTextureId,
                        mesh.Layer));
            }
        }

        private static void WriteCharacterInfo(SimpleCharacterInfo? info)
        {
            if (info is SimpleNpcInfo npc)
            {
                Line("CharacterInfo.Type", "Npc");
                Line("CharacterInfo.Family", npc.Family);
                Line("CharacterInfo.LosHeight", npc.LosHeight);
                Line("CharacterInfo.UnknownData", npc.UnknownData);
                Line("CharacterInfo.UnknownData2", npc.UnknownData2);
                Line("CharacterInfo.UnknownData3", npc.UnknownData3);
                return;
            }

            if (info is SimplePcInfo pc)
            {
                Line("CharacterInfo.Type", "Pc");
                Line("CharacterInfo.CurrentNano", pc.CurrentNano);
                Line("CharacterInfo.Team", pc.Team);
                Line("CharacterInfo.Swim", pc.Swim);
                Line("CharacterInfo.StrengthBase", pc.StrengthBase);
                Line("CharacterInfo.AgilityBase", pc.AgilityBase);
                Line("CharacterInfo.StaminaBase", pc.StaminaBase);
                Line("CharacterInfo.IntelligenceBase", pc.IntelligenceBase);
                Line("CharacterInfo.SenseBase", pc.SenseBase);
                Line("CharacterInfo.PsychicBase", pc.PsychicBase);
                Line("CharacterInfo.FirstName", pc.FirstName ?? string.Empty);
                Line("CharacterInfo.LastName", pc.LastName ?? string.Empty);
                Line("CharacterInfo.OrgName", pc.OrgName ?? string.Empty);
                Line("CharacterInfo.OrgId", pc.OrgId);
                return;
            }

            Line("CharacterInfo.Type", info == null ? "null" : info.GetType().Name);
        }

        private static void Line(string field, object? value)
        {
            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(CultureInfo.InvariantCulture, "SCFU {0}={1}", field, value));
        }

        private static string FormatIdentity(Identity identity)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}",
                identity.Type,
                identity.Instance);
        }

        private static string FormatBytes(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return "len=0";

            var sb = new StringBuilder(16 + bytes.Length * 3);
            sb.Append("len=");
            sb.Append(bytes.Length);
            sb.Append(" [");
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0)
                    sb.Append(' ');
                sb.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            sb.Append(']');
            return sb.ToString();
        }
    }
}
