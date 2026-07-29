from pathlib import Path

rows = []
for line in Path(r"tools-temp/_team_level_ranges.csv").read_text(encoding="utf-8").splitlines():
    line = line.strip()
    if not line:
        continue
    parts = line.split(",")
    rows.append((int(parts[0]), int(parts[1]), int(parts[2])))

# Build TeamXpShareWindow.cs with full exact table (same as ChatEngine TeamLevelRanges)
lines = []
for i, (lvl, mn, mx) in enumerate(rows):
    comma = "," if i < len(rows) - 1 else ""
    lines.append(f"                {{ {lvl}, {mn}, {mx} }}{comma}")

table_body = "\n".join(lines)

cs = f'''namespace ZoneEngine.Core
{{
    using System;

    /// <summary>
    /// XP/SK share windows from Mike Desktop <c>team-levels.txt</c> (full 1..220).
    /// Same data as ChatEngine <c>TeamLevelRanges</c>. Used for XP-share helpers later.
    /// Does not draw the client Recruit warn (that is client-local).
    /// </summary>
    public static class TeamXpShareWindow
    {{
        private static readonly int[,] Table =
        {{
{table_body}
        }};

        /// <summary>
        /// True when candidate is above the inviter's XP share max.
        /// </summary>
        public static bool IsTooHighForXpShare(int inviterLevel, int candidateLevel)
        {{
            if (inviterLevel < 1)
            {{
                inviterLevel = 1;
            }}

            if (candidateLevel < 1)
            {{
                return false;
            }}

            int min;
            int max;
            TryGetRange(inviterLevel, out min, out max);
            return candidateLevel > max;
        }}

        /// <summary>
        /// True when candidate is below the inviter's XP share min.
        /// </summary>
        public static bool IsTooLowForXpShare(int inviterLevel, int candidateLevel)
        {{
            if (inviterLevel < 1)
            {{
                inviterLevel = 1;
            }}

            if (candidateLevel < 1)
            {{
                return true;
            }}

            int min;
            int max;
            TryGetRange(inviterLevel, out min, out max);
            return candidateLevel < min;
        }}

        public static bool IsCompatible(int inviterLevel, int candidateLevel)
        {{
            int min;
            int max;
            TryGetRange(inviterLevel, out min, out max);
            return candidateLevel >= min && candidateLevel <= max;
        }}

        public static bool TryGetRange(int level, out int minLevel, out int maxLevel)
        {{
            if (level < 1)
            {{
                level = 1;
            }}

            if (level > 220)
            {{
                level = 220;
            }}

            for (int i = 0; i < Table.GetLength(0); i++)
            {{
                if (Table[i, 0] == level)
                {{
                    minLevel = Table[i, 1];
                    maxLevel = Table[i, 2];
                    return true;
                }}
            }}

            minLevel = Math.Max(1, level - 5);
            maxLevel = level + 5;
            return false;
        }}
    }}
}}
'''

out = Path(r"AORebirth/Server/ZoneEngine/Core/TeamXpShareWindow.cs")
out.write_text(cs, encoding="utf-8", newline="\r\n")
print(f"wrote {out} rows={len(rows)}")

# Update TeamLevelRanges comment source name only
tlr = Path(r"AORebirth/Server/ChatEngine/Lists/TeamLevelRanges.cs")
text = tlr.read_text(encoding="utf-8")
text2 = text.replace(
    "XP/SK share team level windows from Mike Desktop <c>mission level.txt</c>\n    /// (<c>!lvl N</c> → <c>Team min-max</c>).",
    "XP/SK share team level windows from Mike Desktop <c>team-levels.txt</c>\n    /// (<c>lvl N</c> / <c>Team min-max</c>). Re-synced 2026-07-29 (0 diffs vs prior embed).",
)
if text2 != text:
    tlr.write_text(text2, encoding="utf-8", newline="\r\n")
    print("updated TeamLevelRanges comment")
else:
    print("TeamLevelRanges comment unchanged or pattern miss")
