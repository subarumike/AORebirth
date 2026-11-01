namespace ZoneEngine_New.Core.Organizations
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    /// <summary>
    /// Catalog references the retail server sends for /org replies (captures 20261031 and 20261101, see
    /// docs/generated/org_commands_scenario_capture_20261031.md). The client owns the sentences; the server only sends
    /// <c>~&amp;!!!&amp;t&lt;ref&gt;&lt;params&gt;~</c>. Refs are protocol identifiers, not game content.
    /// </summary>
    public static class OrgChatRefs
    {
        public const string NotInOrganization = "#*:Xm";
        public const string InfoSelfNoOrganization = "!Y3)a";
        public const string InfoTargetNoOrganization = "#0%m>";
        public const string Ranks = "$c[u1s";
        public const string TaxValue = "#ge1Du";
        public const string BankBalance = "\"L&(Yu";
        public const string GoverningFormChanged = "$0:X;";
        public const string GoverningFormList = "#V)]p";
        public const string NotLeaderName = "#L8'X";
        public const string NotLeaderDescription = "#L.-2";
        public const string NotLeaderObjective = "$cZ=l";
        public const string NotLeaderHistory = "\"!Qr$";
        public const string NotLeaderGoverningForm = "!Y9V.";

    }

    /// <summary>Builds the retail org ChatText payloads (refs, string and base-85 integer parameters).</summary>
    public static class OrgChatText
    {
        // Captured retail framing of every ref text: Unknown1 0x10, Unknown2 0, Unknown3 1.
        const byte RefFlags = 0x10;
        const int RefTrailer = 1;

        /// <summary>Integer parameter: five base-85 digits, each offset by '!' (0 = "!!!!!", 1 = "!!!!\"").</summary>
        public static string EncodeInt(uint value)
        {
            Span<char> digits = stackalloc char[5];
            for (int i = 4; i >= 0; i--)
            {
                digits[i] = (char)('!' + value % 85);
                value /= 85;
            }

            return new string(digits);
        }

        /// <summary>Wraps a ref (already followed by its parameters) in the captured text envelope.</summary>
        public static string Wrap(string refAndParameters) => "~&!!!&t" + refAndParameters + "~";

        public static string WithInt(string refWithTypeMarker, long value)
            => Wrap(refWithTypeMarker + EncodeInt((uint)Math.Clamp(value, 0, uint.MaxValue)));

        /// <summary>
        /// Ranks reply, byte layout copied from the captures: the form name is preceded by a length byte (name length + 1,
        /// counting its '~'), then the comma-separated ranks and a line feed; unlike other refs there is no closing '~'.
        /// </summary>
        public static string RanksReply(string formName, string[] ranks)
            => "~&!!!&t" + OrgChatRefs.Ranks + (char)(formName.Length + 1) + formName + "~"
               + string.Join(", ", ranks) + "\n";
        /// <summary>
        /// /org governingform without arguments: captured layout is ref, '~', the names joined by ", ", a full stop and
        /// a line feed, with no closing '~'.
        /// </summary>
        public static string GoverningFormList(string[] names)
            => "~&!!!&t" + OrgChatRefs.GoverningFormList + "~" + string.Join(", ", names) + ".\n";

        public static string GoverningFormNotice(string leaderName, string formName)
            => Wrap("#Y8r9s" + (char)(leaderName.Length + 1) + leaderName + "s" + (char)(formName.Length + 1) + formName);

        /// <summary>The plain-text confirmation retail sends to every online member after a name/description/objective/history change.</summary>
        public static ChatTextMessage InfoUpdated(Identity recipient) => new()
        {
            Identity = recipient,
            Text = "Organization info updated.",
            Unknown1 = RefFlags,
            Unknown2 = 0,
            Unknown3 = 0
        };
        public static ChatTextMessage Message(Identity recipient, string text) => new()
        {
            Identity = recipient,
            Text = text,
            Unknown1 = RefFlags,
            Unknown2 = 0,
            Unknown3 = RefTrailer
        };
    }
}
