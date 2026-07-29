using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChatEngine.Lists
{
    /// <summary>
    /// XP/SK share team level windows from Mike Desktop <c>team-levels.txt</c>
    /// (<c>lvl N</c> / <c>Team min-max</c>). Re-synced 2026-07-29 — matches file (0 diffs).
    /// Used for Looking-for-Team list filtering. Client Recruit warn is separate (GUI.dll).
    /// </summary>
    public static class TeamLevelRanges
    {
        private static readonly Dictionary<int, Tuple<int, int>> Ranges = LoadEmbedded();

        public static bool TryGetRange(int level, out int minLevel, out int maxLevel)
        {
            Tuple<int, int> range;
            if (Ranges.TryGetValue(level, out range))
            {
                minLevel = range.Item1;
                maxLevel = range.Item2;
                return true;
            }

            minLevel = Math.Max(1, level - 5);
            maxLevel = level + 5;
            return false;
        }

        /// <summary>
        /// True if <paramref name="candidateLevel"/> is inside the XP/SK share window
        /// for <paramref name="searcherLevel"/>. Not an invite permission check.
        /// </summary>
        public static bool IsCompatible(int searcherLevel, int candidateLevel)
        {
            int min;
            int max;
            TryGetRange(searcherLevel, out min, out max);
            return candidateLevel >= min && candidateLevel <= max;
        }

        private static Dictionary<int, Tuple<int, int>> LoadEmbedded()
        {
            var map = new Dictionary<int, Tuple<int, int>>();
            string[] lines = EmbeddedCsv.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string[] parts = line.Split(',');
                if (parts.Length != 3)
                {
                    continue;
                }

                int level;
                int min;
                int max;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out level)
                    || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out min)
                    || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out max))
                {
                    continue;
                }

                map[level] = Tuple.Create(min, max);
            }

            return map;
        }

        // level,teamMin,teamMax
        private const string EmbeddedCsv =
@"1,1,7
2,1,8
3,1,9
4,1,10
5,1,11
6,1,12
7,1,13
8,2,14
9,3,15
10,4,17
11,5,18
12,6,19
13,7,20
14,8,21
15,9,24
16,10,25
17,10,26
18,11,27
19,12,28
20,13,29
21,14,30
22,15,32
23,15,33
24,15,34
25,16,35
26,17,38
27,18,39
28,19,40
29,20,41
30,21,43
31,22,44
32,22,45
33,23,46
34,24,49
35,25,50
36,26,51
37,26,52
38,26,54
39,27,55
40,28,56
41,29,57
42,30,60
43,30,61
44,31,62
45,32,63
46,33,65
47,34,66
48,34,67
49,34,68
50,35,71
51,36,72
52,37,73
53,38,74
54,38,76
55,39,77
56,40,78
57,41,79
58,42,82
59,42,83
60,42,84
61,43,85
62,44,87
63,45,88
64,46,89
65,46,90
66,47,93
67,48,94
68,49,95
69,50,96
70,50,98
71,50,99
72,51,100
73,52,101
74,53,104
75,54,105
76,54,106
77,55,107
78,56,109
79,57,110
80,58,111
81,58,112
82,58,115
83,59,116
84,60,117
85,61,118
86,62,120
87,62,121
88,63,122
89,64,123
90,65,126
91,66,127
92,66,128
93,66,129
94,67,131
95,68,132
96,69,133
97,70,134
98,70,137
99,71,138
100,72,139
101,73,140
102,74,142
103,74,143
104,74,144
105,75,145
106,76,148
107,77,149
108,78,150
109,78,151
110,79,153
111,80,154
112,81,155
113,82,156
114,82,159
115,82,160
116,83,161
117,84,162
118,85,164
119,86,165
120,86,166
121,87,167
122,88,170
123,89,171
124,90,172
125,90,173
126,90,175
127,91,176
128,92,177
129,93,178
130,94,181
131,94,182
132,95,183
133,96,184
134,97,186
135,98,187
136,98,188
137,98,189
138,99,192
139,100,193
140,101,194
141,102,195
142,102,197
143,103,198
144,104,199
145,105,200
146,106,203
147,106,204
148,106,205
149,107,206
150,108,208
151,109,209
152,110,210
153,110,211
154,111,214
155,112,215
156,113,216
157,114,217
158,114,219
159,114,220
160,115,220
161,116,220
162,117,220
163,118,220
164,118,220
165,119,220
166,120,220
167,121,220
168,122,220
169,122,220
170,122,220
171,123,220
172,124,220
173,125,220
174,126,220
175,126,220
176,127,220
177,128,220
178,129,220
179,130,220
180,130,220
181,130,220
182,131,220
183,132,220
184,133,220
185,134,220
186,134,220
187,135,220
188,136,220
189,137,220
190,138,220
191,138,220
192,138,220
193,139,220
194,140,220
195,141,220
196,142,220
197,142,220
198,143,220
199,144,220
200,145,220
201,146,220
202,146,220
203,146,220
204,147,220
205,148,220
206,149,220
207,150,220
208,150,220
209,151,220
210,152,220
211,153,220
212,154,220
213,154,220
214,154,220
215,155,220
216,156,220
217,157,220
218,158,220
219,158,220
220,159,220";
    }
}

