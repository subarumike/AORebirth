# Mission QL 1-250 Live Harvest Plan

Generated: 2026-09-01

## Validation summary

- TOTAL TARGET QLS = 250
- ROLLABLE = 250
- UNROLLABLE = 0 ([])
- ASSIGNED = 250
- DUPLICATE ASSIGNMENTS = 0
- MISSING = 0
- Helpbot-proven target QLs = 233
- High-level local-table target QLs awaiting live confirmation = [194, 203, 209, 213, 221, 228, 229, 230, 231, 233, 234, 240, 241, 242, 244, 247, 249]
- Mission table SHA-256 = `393308fe4ac80f7513743aaedabaaaf5c372d081f15f9afc489b3c4df8c03b6a`
- Helpbot raw source SHA-256 = `f8841253af7ed9b63aa2d9d1a2d48e487239b4f8e44e57b225cc7b3855c04488`
- Helpbot revision = https://wiki.aodb.us/index.php?title=Level_Parameters&oldid=44808

## Character rosters

Mathematical set-cover result:

- Certified lower bound = 36 characters
- Best valid upper bound = 40 characters
- Exact optimum is unresolved; do not call the 40-character witness minimal

Best-known valid roster (40 characters):

`2, 7, 17, 23, 25, 35, 46, 48, 67, 68, 71, 76, 78, 94, 103, 119, 121, 124, 129, 139, 154, 158, 165, 184, 188, 189, 190, 191, 192, 194, 195, 196, 197, 199, 201, 203, 204, 212, 219, 220`

Proof status: `ORTOOLS_CP_SAT_9_15_6755_FEASIBLE_40_BOUND_36_AFTER_1200_SECONDS`.

Recommended practical roster (54 characters):

`2, 7, 17, 18, 35, 42, 49, 51, 64, 68, 79, 87, 97, 105, 110, 112, 115, 118, 120, 121, 122, 124, 125, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 142, 143, 144, 146, 147, 149, 156, 163, 165, 177, 178, 180, 185, 201, 202, 208, 209`

Proof status: `SCIPY_HIGHS_MILP_OPTIMAL_ZERO_GAP_54_EVIDENCE_PRESERVING_CHARACTERS`.

The practical roster is the exact minimum-count evidence-preserving roster:
every Helpbot-proven target is assigned through a pinned Helpbot level/QL
edge, and only the 17 targets absent from Helpbot use high-level local-table
edges. Its mixed-integer solve finished optimal with zero gap. Character
level 2 is required for QL1 without relying on blocked level-1 terminal
access; level 201 is required by the current table for QL221.

## MissionHarvest contract

1. Log into the exact listed character level.
2. Select/use an ordinary Rubi-Ka mission terminal once.
3. Run the listed target-QL command. The plugin resolves the first exact
   matching one-based slot and sends nothing if the QL is absent.
4. Wait for `requested_count_completed` feedback, then run status.
5. Accept the target only when status reports `completeCohorts=1` and
   `harvestedOffers` is positive. Otherwise rerun that exact target.
6. `/missionharvest stop` safely stops a partial target and reports its
   session/output summary.

Output is written to
`<AOSharp plugin local-data>\sessions\<session-id>\events.jsonl`.
One request is one terminal refresh and normally records five offers.
Harvester 1.2 records the request-time terminal identity/playfield/coordinates,
mission destination playfield/coordinates, capture-backed mission type, reward
item low/high IDs and QL, title, description, credits, XP, and raw unknown fields
for every offer. Complete per-roll capture does not prove that a finite sample
has exhausted AO's possible items, destinations, or probabilities.

## Complete QL-to-character rollability matrix

| Mission QL | Ordinary-terminal eligible character levels | Evidence |
| ---: | --- | --- |
| 1 | 2 | `PROVEN_HELPBOT_ROLLABLE` |
| 2 | 2, 3, 4 | `PROVEN_HELPBOT_ROLLABLE` |
| 3 | 2, 3, 4, 5 | `PROVEN_HELPBOT_ROLLABLE` |
| 4 | 3, 4, 5, 6, 7 | `PROVEN_HELPBOT_ROLLABLE` |
| 5 | 3, 4, 5, 6, 7, 8 | `PROVEN_HELPBOT_ROLLABLE` |
| 6 | 4, 5, 6, 7, 8, 9 | `PROVEN_HELPBOT_ROLLABLE` |
| 7 | 4, 5, 6, 7, 8, 9, 10, 11 | `PROVEN_HELPBOT_ROLLABLE` |
| 8 | 5, 7, 8, 9, 10, 11, 12 | `PROVEN_HELPBOT_ROLLABLE` |
| 9 | 6, 7, 8, 9, 10, 11, 12, 13, 14 | `PROVEN_HELPBOT_ROLLABLE` |
| 10 | 6, 7, 8, 9, 10, 12, 13, 14, 15 | `PROVEN_HELPBOT_ROLLABLE` |
| 11 | 9, 10, 11, 13, 14, 15, 16, 17 | `PROVEN_HELPBOT_ROLLABLE` |
| 12 | 7, 8, 10, 11, 12, 14, 15, 16, 17, 18 | `PROVEN_HELPBOT_ROLLABLE` |
| 13 | 9, 10, 11, 12, 13, 15, 16, 17, 18, 19 | `PROVEN_HELPBOT_ROLLABLE` |
| 14 | 8, 11, 12, 13, 14, 16, 17, 18, 19, 20, 21 | `PROVEN_HELPBOT_ROLLABLE` |
| 15 | 10, 12, 13, 14, 15, 17, 18, 19, 20, 21, 22 | `PROVEN_HELPBOT_ROLLABLE` |
| 16 | 9, 11, 13, 14, 15, 16, 18, 19, 20, 21, 22, 23, 24 | `PROVEN_HELPBOT_ROLLABLE` |
| 17 | 16, 17, 19, 20, 21, 22, 23, 25 | `PROVEN_HELPBOT_ROLLABLE` |
| 18 | 10, 12, 14, 15, 17, 18, 20, 21, 22, 23, 24, 25, 26, 27 | `PROVEN_HELPBOT_ROLLABLE` |
| 19 | 11, 13, 15, 16, 18, 19, 22, 23, 24, 26, 28 | `PROVEN_HELPBOT_ROLLABLE` |
| 20 | 16, 17, 19, 20, 23, 24, 25, 26, 27, 29 | `PROVEN_HELPBOT_ROLLABLE` |
| 21 | 12, 14, 18, 21, 24, 25, 27, 28, 29, 30, 31 | `PROVEN_HELPBOT_ROLLABLE` |
| 22 | 13, 15, 17, 19, 20, 22, 25, 26, 27, 28, 30, 32 | `PROVEN_HELPBOT_ROLLABLE` |
| 23 | 18, 21, 23, 26, 28, 29, 31, 33, 34 | `PROVEN_HELPBOT_ROLLABLE` |
| 24 | 14, 16, 19, 20, 22, 24, 27, 29, 30, 31, 32, 33, 35 | `PROVEN_HELPBOT_ROLLABLE` |
| 25 | 17, 21, 23, 25, 28, 30, 32, 34, 36, 37 | `PROVEN_HELPBOT_ROLLABLE` |
| 26 | 15, 20, 22, 24, 26, 29, 31, 33, 35, 38 | `PROVEN_HELPBOT_ROLLABLE` |
| 27 | 18, 21, 23, 25, 27, 30, 31, 32, 34, 36, 37, 39 | `PROVEN_HELPBOT_ROLLABLE` |
| 28 | 16, 19, 22, 24, 26, 28, 32, 33, 34, 35, 36, 38, 40, 41 | `PROVEN_HELPBOT_ROLLABLE` |
| 29 | 23, 27, 29, 33, 35, 37, 39, 42 | `PROVEN_HELPBOT_ROLLABLE` |
| 30 | 17, 20, 25, 28, 30, 34, 36, 38, 40, 41, 43, 44 | `PROVEN_HELPBOT_ROLLABLE` |
| 31 | 21, 24, 26, 29, 31, 35, 37, 39, 42, 45 | `PROVEN_HELPBOT_ROLLABLE` |
| 32 | 18, 25, 27, 32, 36, 38, 40, 41, 43, 46, 47 | `PROVEN_HELPBOT_ROLLABLE` |
| 33 | 22, 26, 28, 30, 33, 37, 39, 42, 44, 45, 48 | `PROVEN_HELPBOT_ROLLABLE` |
| 34 | 19, 23, 29, 31, 34, 38, 40, 41, 43, 46, 49 | `PROVEN_HELPBOT_ROLLABLE` |
| 35 | 27, 32, 35, 39, 42, 44, 47, 50, 51 | `PROVEN_HELPBOT_ROLLABLE` |
| 36 | 20, 24, 28, 30, 33, 36, 40, 41, 43, 45, 46, 48, 49, 52 | `PROVEN_HELPBOT_ROLLABLE` |
| 37 | 21, 25, 29, 31, 34, 37, 42, 44, 47, 50, 53, 54 | `PROVEN_HELPBOT_ROLLABLE` |
| 38 | 22, 32, 35, 38, 43, 45, 48, 51, 55 | `PROVEN_HELPBOT_ROLLABLE` |
| 39 | 26, 30, 33, 36, 39, 44, 46, 47, 49, 52, 53, 56, 57 | `PROVEN_HELPBOT_ROLLABLE` |
| 40 | 27, 31, 34, 37, 40, 45, 48, 50, 51, 54, 58 | `PROVEN_HELPBOT_ROLLABLE` |
| 41 | 23, 32, 38, 41, 46, 49, 52, 55, 59 | `PROVEN_HELPBOT_ROLLABLE` |
| 42 | 24, 28, 33, 35, 39, 42, 47, 50, 53, 56, 57, 60, 61 | `PROVEN_HELPBOT_ROLLABLE` |
| 43 | 29, 36, 43, 48, 51, 54, 58, 62 | `PROVEN_HELPBOT_ROLLABLE` |
| 44 | 25, 34, 37, 40, 44, 49, 52, 55, 56, 59, 63, 64 | `PROVEN_HELPBOT_ROLLABLE` |
| 45 | 30, 35, 38, 41, 45, 50, 51, 53, 54, 57, 60, 61, 65 | `PROVEN_HELPBOT_ROLLABLE` |
| 46 | 26, 31, 36, 39, 42, 46, 52, 55, 58, 62, 66, 67 | `PROVEN_HELPBOT_ROLLABLE` |
| 47 | 43, 47, 53, 56, 59, 63, 68 | `PROVEN_HELPBOT_ROLLABLE` |
| 48 | 27, 32, 37, 40, 44, 48, 54, 57, 60, 61, 64, 65, 69 | `PROVEN_HELPBOT_ROLLABLE` |
| 49 | 33, 38, 41, 45, 49, 55, 58, 62, 66, 70, 71 | `PROVEN_HELPBOT_ROLLABLE` |
| 50 | 28, 39, 42, 46, 50, 56, 59, 63, 67, 72 | `PROVEN_HELPBOT_ROLLABLE` |
| 51 | 29, 34, 43, 47, 51, 57, 60, 61, 64, 68, 69, 73, 74 | `PROVEN_HELPBOT_ROLLABLE` |
| 52 | 35, 40, 44, 48, 52, 58, 62, 65, 66, 70, 75 | `PROVEN_HELPBOT_ROLLABLE` |
| 53 | 30, 41, 49, 53, 59, 63, 67, 71, 76, 77 | `PROVEN_HELPBOT_ROLLABLE` |
| 54 | 36, 42, 45, 54, 60, 61, 64, 68, 72, 73, 78 | `PROVEN_HELPBOT_ROLLABLE` |
| 55 | 31, 37, 43, 46, 50, 55, 62, 65, 69, 74, 79 | `PROVEN_HELPBOT_ROLLABLE` |
| 56 | 32, 47, 51, 56, 63, 66, 67, 70, 71, 75, 80, 81 | `PROVEN_HELPBOT_ROLLABLE` |
| 57 | 38, 44, 48, 52, 57, 64, 68, 72, 76, 77, 82 | `PROVEN_HELPBOT_ROLLABLE` |
| 58 | 33, 39, 45, 49, 53, 58, 65, 69, 73, 78, 83, 84 | `PROVEN_HELPBOT_ROLLABLE` |
| 59 | 46, 54, 59, 66, 70, 74, 79, 85 | `PROVEN_HELPBOT_ROLLABLE` |
| 60 | 34, 40, 50, 55, 60, 67, 71, 75, 76, 77, 80, 81, 86, 87 | `PROVEN_HELPBOT_ROLLABLE` |
| 61 | 41, 47, 51, 56, 61, 68, 72, 82, 88 | `PROVEN_HELPBOT_ROLLABLE` |
| 62 | 35, 48, 52, 57, 62, 69, 73, 74, 78, 83, 89 | `PROVEN_HELPBOT_ROLLABLE` |
| 63 | 42, 49, 53, 58, 63, 70, 71, 75, 79, 84, 85, 90, 91 | `PROVEN_HELPBOT_ROLLABLE` |
| 64 | 36, 43, 54, 59, 64, 72, 76, 77, 80, 81, 86, 92 | `PROVEN_HELPBOT_ROLLABLE` |
| 65 | 50, 65, 73, 82, 87, 93, 94 | `PROVEN_HELPBOT_ROLLABLE` |
| 66 | 37, 44, 51, 55, 60, 66, 74, 78, 83, 88, 89, 95 | `PROVEN_HELPBOT_ROLLABLE` |
| 67 | 45, 52, 56, 61, 67, 75, 79, 84, 90, 96, 97 | `PROVEN_HELPBOT_ROLLABLE` |
| 68 | 38, 53, 57, 62, 68, 76, 80, 81, 85, 86, 91, 98 | `PROVEN_HELPBOT_ROLLABLE` |
| 69 | 39, 46, 58, 63, 69, 77, 82, 87, 92, 93, 99 | `PROVEN_HELPBOT_ROLLABLE` |
| 70 | 47, 54, 59, 64, 70, 78, 83, 88, 94, 100, 101 | `PROVEN_HELPBOT_ROLLABLE` |
| 71 | 40, 55, 65, 71, 79, 84, 89, 95, 102 | `PROVEN_HELPBOT_ROLLABLE` |
| 72 | 48, 56, 60, 66, 72, 80, 81, 85, 90, 91, 96, 97, 103, 104 | `PROVEN_HELPBOT_ROLLABLE` |
| 73 | 41, 49, 61, 67, 73, 82, 86, 87, 92, 98, 105 | `PROVEN_HELPBOT_ROLLABLE` |
| 74 | 57, 62, 68, 74, 83, 88, 93, 99, 106, 107 | `PROVEN_HELPBOT_ROLLABLE` |
| 75 | 42, 50, 58, 63, 69, 75, 84, 89, 94, 100, 101, 108 | `PROVEN_HELPBOT_ROLLABLE` |
| 76 | 51, 59, 64, 76, 85, 90, 95, 96, 102, 109 | `PROVEN_HELPBOT_ROLLABLE` |
| 77 | 43, 70, 77, 86, 91, 97, 103, 110, 111 | `PROVEN_HELPBOT_ROLLABLE` |
| 78 | 44, 52, 60, 65, 71, 78, 87, 92, 98, 104, 105, 112 | `PROVEN_HELPBOT_ROLLABLE` |
| 79 | 53, 61, 66, 72, 79, 88, 93, 94, 99, 106, 113, 114 | `PROVEN_HELPBOT_ROLLABLE` |
| 80 | 45, 62, 67, 73, 80, 89, 95, 100, 101, 107, 115 | `PROVEN_HELPBOT_ROLLABLE` |
| 81 | 54, 63, 68, 74, 81, 90, 91, 96, 102, 108, 109, 116, 117 | `PROVEN_HELPBOT_ROLLABLE` |
| 82 | 46, 55, 69, 75, 82, 92, 97, 103, 110, 118 | `PROVEN_HELPBOT_ROLLABLE` |
| 83 | 64, 76, 83, 93, 98, 104, 111, 119 | `PROVEN_HELPBOT_ROLLABLE` |
| 84 | 47, 56, 65, 70, 77, 84, 94, 99, 105, 106, 112, 113, 120, 121 | `PROVEN_HELPBOT_ROLLABLE` |
| 85 | 48, 57, 66, 71, 78, 85, 95, 100, 101, 107, 114, 122 | `PROVEN_HELPBOT_ROLLABLE` |
| 86 | 72, 79, 86, 96, 102, 108, 115, 123, 124 | `PROVEN_HELPBOT_ROLLABLE` |
| 87 | 49, 58, 67, 73, 87, 97, 103, 109, 116, 117, 125 | `PROVEN_HELPBOT_ROLLABLE` |
| 88 | 59, 68, 74, 80, 88, 98, 104, 110, 111, 118, 126, 127 | `PROVEN_HELPBOT_ROLLABLE` |
| 89 | 50, 69, 81, 89, 99, 105, 112, 119, 128 | `PROVEN_HELPBOT_ROLLABLE` |
| 90 | 60, 75, 82, 90, 100, 101, 106, 107, 113, 120, 121, 129 | `PROVEN_HELPBOT_ROLLABLE` |
| 91 | 51, 61, 70, 76, 77, 83, 91, 102, 108, 114, 122, 130, 131 | `PROVEN_HELPBOT_ROLLABLE` |
| 92 | 52, 71, 84, 92, 103, 109, 115, 116, 123, 132 | `PROVEN_HELPBOT_ROLLABLE` |
| 93 | 62, 72, 78, 85, 93, 104, 110, 117, 124, 125, 133, 134 | `PROVEN_HELPBOT_ROLLABLE` |
| 94 | 63, 73, 79, 86, 94, 105, 111, 118, 126, 135 | `PROVEN_HELPBOT_ROLLABLE` |
| 95 | 53, 87, 95, 106, 112, 119, 127, 136, 137 | `PROVEN_HELPBOT_ROLLABLE` |
| 96 | 64, 74, 80, 88, 96, 107, 113, 114, 120, 121, 128, 129, 138 | `PROVEN_HELPBOT_ROLLABLE` |
| 97 | 54, 65, 75, 81, 89, 97, 108, 115, 122, 130, 139 | `PROVEN_HELPBOT_ROLLABLE` |
| 98 | 55, 76, 82, 98, 109, 116, 123, 131, 140, 141 | `PROVEN_HELPBOT_ROLLABLE` |
| 99 | 66, 83, 90, 99, 110, 111, 117, 124, 132, 133, 142 | `PROVEN_HELPBOT_ROLLABLE` |
| 100 | 56, 67, 77, 84, 91, 100, 112, 118, 125, 126, 134, 143, 144 | `PROVEN_HELPBOT_ROLLABLE` |
| 101 | 78, 92, 101, 113, 119, 127, 135, 145 | `PROVEN_HELPBOT_ROLLABLE` |
| 102 | 57, 68, 79, 85, 93, 102, 114, 120, 121, 128, 136, 137, 146, 147 | `PROVEN_HELPBOT_ROLLABLE` |
| 103 | 58, 69, 86, 94, 103, 115, 122, 129, 138, 148 | `PROVEN_HELPBOT_ROLLABLE` |
| 104 | 80, 87, 95, 104, 116, 123, 130, 131, 139, 149 | `PROVEN_HELPBOT_ROLLABLE` |
| 105 | 59, 70, 81, 88, 96, 105, 117, 124, 132, 140, 141, 150, 151 | `PROVEN_HELPBOT_ROLLABLE` |
| 106 | 71, 82, 89, 97, 106, 118, 125, 133, 142, 152 | `PROVEN_HELPBOT_ROLLABLE` |
| 107 | 83, 98, 107, 119, 126, 127, 134, 143, 153, 154 | `PROVEN_HELPBOT_ROLLABLE` |
| 108 | 60, 72, 90, 99, 108, 120, 121, 128, 135, 136, 144, 145, 155 | `PROVEN_HELPBOT_ROLLABLE` |
| 109 | 61, 73, 84, 91, 109, 122, 129, 137, 146, 156, 157 | `PROVEN_HELPBOT_ROLLABLE` |
| 110 | 62, 85, 92, 100, 110, 123, 130, 138, 147, 158 | `PROVEN_HELPBOT_ROLLABLE` |
| 111 | 74, 86, 93, 101, 111, 124, 131, 139, 148, 149, 159 | `PROVEN_HELPBOT_ROLLABLE` |
| 112 | 63, 75, 94, 102, 112, 125, 132, 140, 141, 150, 160, 161 | `PROVEN_HELPBOT_ROLLABLE` |
| 113 | 87, 103, 113, 126, 133, 134, 142, 151, 162 | `PROVEN_HELPBOT_ROLLABLE` |
| 114 | 64, 76, 88, 95, 104, 114, 127, 135, 143, 152, 153, 163, 164 | `PROVEN_HELPBOT_ROLLABLE` |
| 115 | 77, 89, 96, 105, 115, 128, 136, 144, 154, 165 | `PROVEN_HELPBOT_ROLLABLE` |
| 116 | 65, 97, 106, 116, 129, 137, 145, 146, 155, 166, 167 | `PROVEN_HELPBOT_ROLLABLE` |
| 117 | 78, 90, 98, 107, 117, 130, 131, 138, 147, 156, 157, 168 | `PROVEN_HELPBOT_ROLLABLE` |
| 118 | 66, 79, 91, 99, 108, 118, 132, 139, 148, 158, 169 | `PROVEN_HELPBOT_ROLLABLE` |
| 119 | 92, 109, 119, 133, 140, 141, 149, 159, 170, 171 | `PROVEN_HELPBOT_ROLLABLE` |
| 120 | 67, 80, 93, 100, 120, 134, 142, 150, 151, 160, 161, 172 | `PROVEN_HELPBOT_ROLLABLE` |
| 121 | 68, 81, 101, 110, 121, 135, 143, 152, 162, 173, 174 | `PROVEN_HELPBOT_ROLLABLE` |
| 122 | 94, 102, 111, 112, 122, 136, 144, 153, 163, 175 | `PROVEN_HELPBOT_ROLLABLE` |
| 123 | 69, 82, 95, 103, 123, 137, 145, 154, 164, 165, 176, 177 | `PROVEN_HELPBOT_ROLLABLE` |
| 124 | 83, 96, 104, 113, 124, 138, 146, 147, 155, 156, 166, 178 | `PROVEN_HELPBOT_ROLLABLE` |
| 125 | 70, 114, 125, 139, 148, 157, 167, 179 | `PROVEN_HELPBOT_ROLLABLE` |
| 126 | 84, 97, 105, 115, 126, 140, 141, 149, 158, 168, 169, 180, 181 | `PROVEN_HELPBOT_ROLLABLE` |
| 127 | 71, 85, 98, 106, 116, 127, 142, 150, 159, 170, 182 | `PROVEN_HELPBOT_ROLLABLE` |
| 128 | 72, 99, 107, 117, 128, 143, 151, 160, 161, 171, 183, 184 | `PROVEN_HELPBOT_ROLLABLE` |
| 129 | 86, 108, 118, 129, 144, 152, 162, 172, 173, 185 | `PROVEN_HELPBOT_ROLLABLE` |
| 130 | 73, 87, 100, 109, 119, 130, 145, 153, 154, 163, 174, 186, 187 | `PROVEN_HELPBOT_ROLLABLE` |
| 131 | 101, 131, 146, 155, 164, 175, 188 | `PROVEN_HELPBOT_ROLLABLE` |
| 132 | 74, 88, 102, 110, 120, 132, 147, 156, 165, 166, 176, 177, 189 | `PROVEN_HELPBOT_ROLLABLE` |
| 133 | 89, 103, 111, 121, 133, 148, 157, 167, 178, 190, 191 | `PROVEN_HELPBOT_ROLLABLE` |
| 134 | 75, 112, 122, 134, 149, 158, 168, 179, 192 | `PROVEN_HELPBOT_ROLLABLE` |
| 135 | 76, 90, 104, 113, 123, 135, 150, 151, 159, 169, 180, 181, 193, 194 | `PROVEN_HELPBOT_ROLLABLE` |
| 136 | 91, 105, 114, 124, 136, 152, 160, 161, 170, 171, 182, 195 | `PROVEN_HELPBOT_ROLLABLE` |
| 137 | 77, 106, 125, 137, 153, 162, 172, 183, 196, 197 | `PROVEN_HELPBOT_ROLLABLE` |
| 138 | 78, 92, 115, 126, 138, 154, 163, 173, 184, 185, 198 | `PROVEN_HELPBOT_ROLLABLE` |
| 139 | 79, 93, 107, 116, 127, 139, 155, 164, 174, 186, 199 | `PROVEN_HELPBOT_ROLLABLE` |
| 140 | 108, 117, 128, 140, 156, 165, 175, 176, 187, 200, 201 | `PROVEN_HELPBOT_ROLLABLE` |
| 141 | 94, 109, 118, 129, 141, 157, 166, 167, 177, 188, 189, 202 | `PROVEN_HELPBOT_ROLLABLE` |
| 142 | 95, 119, 142, 158, 168, 178, 190, 203, 204 | `PROVEN_HELPBOT_ROLLABLE` |
| 143 | 110, 130, 143, 159, 169, 179, 191, 205 | `PROVEN_HELPBOT_ROLLABLE` |
| 144 | 80, 96, 111, 120, 131, 144, 160, 161, 170, 180, 181, 192, 193, 206, 207 | `PROVEN_HELPBOT_ROLLABLE` |
| 145 | 81, 97, 112, 121, 132, 145, 162, 171, 182, 194, 208 | `PROVEN_HELPBOT_ROLLABLE` |
| 146 | 82, 113, 122, 133, 146, 163, 172, 183, 195, 209 | `PROVEN_HELPBOT_ROLLABLE` |
| 147 | 98, 123, 134, 147, 164, 173, 174, 184, 196, 197, 210, 211 | `PROVEN_HELPBOT_ROLLABLE` |
| 148 | 83, 99, 114, 124, 135, 148, 165, 175, 185, 186, 198, 212 | `PROVEN_HELPBOT_ROLLABLE` |
| 149 | 84, 115, 136, 149, 166, 176, 187, 199, 213, 214 | `PROVEN_HELPBOT_ROLLABLE` |
| 150 | 100, 116, 125, 137, 150, 167, 177, 188, 200, 201, 215 | `PROVEN_HELPBOT_ROLLABLE` |
| 151 | 85, 101, 126, 138, 151, 168, 178, 189, 202, 216, 217 | `PROVEN_HELPBOT_ROLLABLE` |
| 152 | 117, 127, 139, 152, 169, 179, 190, 191, 203, 218 | `PROVEN_HELPBOT_ROLLABLE` |
| 153 | 102, 118, 128, 153, 170, 171, 180, 181, 192, 204, 205, 219 | `PROVEN_HELPBOT_ROLLABLE` |
| 154 | 86, 103, 119, 129, 140, 154, 172, 182, 193, 206, 220 | `PROVEN_HELPBOT_ROLLABLE` |
| 155 | 87, 141, 155, 173, 183, 194, 207 | `PROVEN_HELPBOT_ROLLABLE` |
| 156 | 104, 120, 130, 142, 156, 174, 184, 195, 196, 208, 209 | `PROVEN_HELPBOT_ROLLABLE` |
| 157 | 88, 105, 121, 131, 143, 157, 175, 185, 197, 210 | `PROVEN_HELPBOT_ROLLABLE` |
| 158 | 122, 132, 144, 158, 176, 186, 187, 198, 211 | `PROVEN_HELPBOT_ROLLABLE` |
| 159 | 89, 106, 123, 133, 145, 159, 177, 188, 199, 212, 213 | `PROVEN_HELPBOT_ROLLABLE` |
| 160 | 107, 134, 146, 160, 178, 189, 200, 201, 214 | `PROVEN_HELPBOT_ROLLABLE` |
| 161 | 90, 124, 147, 161, 179, 190, 202, 215 | `PROVEN_HELPBOT_ROLLABLE` |
| 162 | 108, 125, 135, 148, 162, 180, 181, 191, 203, 216, 217 | `PROVEN_HELPBOT_ROLLABLE` |
| 163 | 91, 109, 126, 136, 149, 163, 182, 192, 204, 218 | `PROVEN_HELPBOT_ROLLABLE` |
| 164 | 92, 137, 164, 183, 193, 194, 205, 206, 219 | `PROVEN_HELPBOT_ROLLABLE` |
| 165 | 110, 127, 138, 150, 165, 184, 195, 207, 220 | `PROVEN_HELPBOT_ROLLABLE` |
| 166 | 93, 111, 128, 139, 151, 166, 185, 196, 208 | `PROVEN_HELPBOT_ROLLABLE` |
| 167 | 129, 152, 167, 186, 197, 209 | `PROVEN_HELPBOT_ROLLABLE` |
| 168 | 94, 112, 140, 153, 168, 187, 198, 210, 211 | `PROVEN_HELPBOT_ROLLABLE` |
| 169 | 113, 130, 141, 154, 169, 188, 199, 212 | `PROVEN_HELPBOT_ROLLABLE` |
| 170 | 95, 131, 142, 155, 170, 189, 200, 201, 213 | `PROVEN_HELPBOT_ROLLABLE` |
| 171 | 96, 114, 132, 143, 156, 171, 190, 191, 202, 214 | `PROVEN_HELPBOT_ROLLABLE` |
| 172 | 115, 133, 144, 157, 172, 192, 203, 215, 216 | `PROVEN_HELPBOT_ROLLABLE` |
| 173 | 97, 158, 173, 193, 204, 217 | `PROVEN_HELPBOT_ROLLABLE` |
| 174 | 116, 134, 145, 159, 174, 194, 205, 218 | `PROVEN_HELPBOT_ROLLABLE` |
| 175 | 98, 117, 135, 146, 175, 195, 206, 207, 219 | `PROVEN_HELPBOT_ROLLABLE` |
| 176 | 136, 147, 160, 176, 196, 208, 220 | `PROVEN_HELPBOT_ROLLABLE` |
| 177 | 99, 118, 148, 161, 177, 197, 209 | `PROVEN_HELPBOT_ROLLABLE` |
| 178 | 119, 137, 149, 162, 178, 198, 210 | `PROVEN_HELPBOT_ROLLABLE` |
| 179 | 100, 138, 163, 179, 199, 211 | `PROVEN_HELPBOT_ROLLABLE` |
| 180 | 120, 139, 150, 164, 180, 200, 201, 212 | `PROVEN_HELPBOT_ROLLABLE` |
| 181 | 121, 151, 165, 181, 202, 213, 214 | `PROVEN_HELPBOT_ROLLABLE` |
| 182 | 140, 152, 166, 182, 203, 215 | `PROVEN_HELPBOT_ROLLABLE` |
| 183 | 122, 141, 153, 167, 183, 204, 216 | `PROVEN_HELPBOT_ROLLABLE` |
| 184 | 101, 123, 142, 154, 168, 184, 205, 217 | `PROVEN_HELPBOT_ROLLABLE` |
| 185 | 103, 143, 169, 185, 206, 218 | `PROVEN_HELPBOT_ROLLABLE` |
| 186 | 102, 124, 155, 186, 207, 219 | `PROVEN_HELPBOT_ROLLABLE` |
| 187 | 104, 125, 144, 156, 170, 187, 208, 220 | `PROVEN_HELPBOT_ROLLABLE` |
| 188 | 105, 145, 157, 171, 188, 209 | `PROVEN_HELPBOT_ROLLABLE` |
| 189 | 106, 126, 146, 158, 172, 189, 210, 211 | `PROVEN_HELPBOT_ROLLABLE` |
| 190 | 127, 159, 173, 190, 212 | `PROVEN_HELPBOT_ROLLABLE` |
| 191 | 107, 147, 174, 191, 213 | `PROVEN_HELPBOT_ROLLABLE` |
| 192 | 128, 148, 160, 175, 192, 214 | `PROVEN_HELPBOT_ROLLABLE` |
| 193 | 108, 129, 149, 161, 176, 193, 215 | `PROVEN_HELPBOT_ROLLABLE` |
| 194 | 162, 177, 194, 216 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 195 | 109, 130, 150, 163, 178, 195, 217 | `PROVEN_HELPBOT_ROLLABLE` |
| 196 | 131, 151, 164, 179, 196, 218 | `PROVEN_HELPBOT_ROLLABLE` |
| 197 | 110, 152, 197, 219 | `PROVEN_HELPBOT_ROLLABLE` |
| 198 | 111, 132, 153, 165, 180, 198, 220 | `PROVEN_HELPBOT_ROLLABLE` |
| 199 | 133, 166, 181, 199 | `PROVEN_HELPBOT_ROLLABLE` |
| 200 | 112, 154, 167, 182, 200 | `PROVEN_HELPBOT_ROLLABLE` |
| 201 | 134, 155, 168, 183, 201 | `PROVEN_HELPBOT_ROLLABLE` |
| 202 | 113, 135, 156, 169, 184, 202 | `PROVEN_HELPBOT_ROLLABLE` |
| 203 | 185, 203 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 204 | 114, 136, 157, 170, 186, 204 | `PROVEN_HELPBOT_ROLLABLE` |
| 205 | 137, 158, 171, 187, 205 | `PROVEN_HELPBOT_ROLLABLE` |
| 206 | 115, 159, 172, 188, 206 | `PROVEN_HELPBOT_ROLLABLE` |
| 207 | 138, 173, 189, 207 | `PROVEN_HELPBOT_ROLLABLE` |
| 208 | 116, 139, 160, 174, 208 | `PROVEN_HELPBOT_ROLLABLE` |
| 209 | 161, 190, 209 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 210 | 117, 140, 162, 175, 191, 210 | `PROVEN_HELPBOT_ROLLABLE` |
| 211 | 118, 141, 163, 176, 192, 211 | `PROVEN_HELPBOT_ROLLABLE` |
| 212 | 142, 177, 193, 212 | `PROVEN_HELPBOT_ROLLABLE` |
| 213 | 164, 178, 194, 213 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 214 | 119, 143, 165, 179, 195, 214 | `PROVEN_HELPBOT_ROLLABLE` |
| 215 | 120, 166, 196, 215 | `PROVEN_HELPBOT_ROLLABLE` |
| 216 | 144, 180, 197, 216 | `PROVEN_HELPBOT_ROLLABLE` |
| 217 | 121, 145, 167, 181, 198, 217 | `PROVEN_HELPBOT_ROLLABLE` |
| 218 | 122, 168, 182, 199, 218 | `PROVEN_HELPBOT_ROLLABLE` |
| 219 | 146, 169, 183, 219 | `PROVEN_HELPBOT_ROLLABLE` |
| 220 | 123, 147, 170, 184, 200, 220 | `PROVEN_HELPBOT_ROLLABLE` |
| 221 | 201 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 222 | 124, 148, 171, 185, 202 | `PROVEN_HELPBOT_ROLLABLE` |
| 223 | 149, 172, 186, 203 | `PROVEN_HELPBOT_ROLLABLE` |
| 224 | 125, 173, 187, 204 | `PROVEN_HELPBOT_ROLLABLE` |
| 225 | 126, 127, 150, 188, 205 | `PROVEN_HELPBOT_ROLLABLE` |
| 226 | 128, 151, 174, 189, 206 | `PROVEN_HELPBOT_ROLLABLE` |
| 227 | 129, 175, 207 | `PROVEN_HELPBOT_ROLLABLE` |
| 228 | 152, 176, 190, 208 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 229 | 153, 191, 209 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 230 | 177, 192 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 231 | 154, 178, 193, 210 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 232 | 130, 155, 179, 194, 211 | `PROVEN_HELPBOT_ROLLABLE` |
| 233 | 180, 212 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 234 | 156, 195, 213 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 235 | 131, 157, 181, 196, 214 | `PROVEN_HELPBOT_ROLLABLE` |
| 236 | 132, 182, 197, 215 | `PROVEN_HELPBOT_ROLLABLE` |
| 237 | 133, 158, 183, 198, 216 | `PROVEN_HELPBOT_ROLLABLE` |
| 238 | 134, 159, 199, 217 | `PROVEN_HELPBOT_ROLLABLE` |
| 239 | 135, 184, 218 | `PROVEN_HELPBOT_ROLLABLE` |
| 240 | 160, 185, 200, 219 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 241 | 161, 186, 201 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 242 | 202, 220 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 243 | 136, 162, 187, 203 | `PROVEN_HELPBOT_ROLLABLE` |
| 244 | 163, 188, 204 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 245 | 137, 189 | `PROVEN_HELPBOT_ROLLABLE` |
| 246 | 138, 164, 190, 205 | `PROVEN_HELPBOT_ROLLABLE` |
| 247 | 165, 206 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 248 | 139, 191, 207 | `PROVEN_HELPBOT_ROLLABLE` |
| 249 | 166, 192, 208 | `INFERRED_HIGH_LEVEL_TABLE_ROLLABLE` |
| 250 | 140, 141, 142, 143, 144, 145, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, 189, 190, 191, 192, 193, 194, 195, 196, 197, 198, 199, 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215, 216, 217, 218, 219, 220 | `PROVEN_HELPBOT_ROLLABLE` |

## Complete copy/paste runbook

### Character level 2

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 1 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 1 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 2 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 2 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 3 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 3 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 7

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 4 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 4 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 5 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 5 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 6 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 6 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 7 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 7 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 8 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 8 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 9 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 9 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 10 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 10 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 12 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 12 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 17

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 11 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 11 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 13 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 13 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 15 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 15 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 17 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 17 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 20 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 20 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 22 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 22 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 25 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 25 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 30 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 30 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 18

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 14 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 14 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 16 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 16 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 18 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 18 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 19 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 19 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 21 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 21 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 23 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 23 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 27 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 27 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 32 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 32 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 35

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 24 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 24 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 26 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 26 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 28 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 28 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 29 — exact slot 4

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 29 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 42 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 42 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 52 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 52 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 62 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 62 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 42

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 31 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 31 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 33 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 33 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 35 — exact slot 4

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 35 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 37 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 37 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 46 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 46 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 50 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 50 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 75 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 75 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 49

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 34 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 34 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 36 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 36 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 39 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 39 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 41 — exact slot 4

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 41 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 49 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 49 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 53 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 53 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 58 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 58 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 51

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 38 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 38 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 40 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 40 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 43 — exact slot 4

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 43 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 45 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 45 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 51 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 51 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 56 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 56 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 66 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 66 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 64

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 44 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 44 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 48 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 48 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 54 — exact slot 4

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 54 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 64 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 64 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 70 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 70 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 76 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 76 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 83 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 83 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 68

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 47 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 47 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 57 — exact slot 4

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 57 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 61 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 61 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 68 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 68 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 74 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 74 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 81 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 81 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 79

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 55 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 55 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 59 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 59 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 63 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 63 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 71 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 71 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 79 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 79 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 87

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 60 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 60 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 65 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 65 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 69 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 69 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 95 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 95 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 155 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 155 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 97

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 67 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 67 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 72 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 72 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 87 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 87 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 97 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 97 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 173 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 173 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 105

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 73 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 73 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 78 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 78 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 84 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 84 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 94 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 94 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 115 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 115 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 188 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 188 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 110

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 77 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 77 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 88 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 88 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 143 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 143 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 165 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 165 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 197 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 197 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 112

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 89 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 89 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 112 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 112 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 122 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 122 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 168 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 168 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 200 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 200 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 115

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 80 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 80 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 103 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 103 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 138 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 138 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 149 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 149 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 206 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 206 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 118

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 82 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 82 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 118 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 118 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 153 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 153 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 177 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 177 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 211 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 211 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 120

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 90 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 90 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 120 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 120 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 144 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 144 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 156 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 156 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 180 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 180 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 215 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 215 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 121

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 121 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 121 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 133 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 133 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 145 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 145 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 181 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 181 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 217 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 217 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 122

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 85 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 85 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 91 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 91 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 109 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 109 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 183 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 183 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 218 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 218 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 124

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 86 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 86 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 136 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 136 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 148 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 148 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 186 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 186 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 222 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 222 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 125

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 125 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 125 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 137 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 137 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 150 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 150 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 162 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 162 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 224 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 224 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 127

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 107 — exact slot 4

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 107 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 114 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 114 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 139 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 139 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 190 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 190 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 225 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 225 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 128

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 96 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 96 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 140 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 140 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 166 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 166 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 192 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 192 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 226 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 226 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 129

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 116 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 116 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 141 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 141 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 154 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 154 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 167 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 167 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 227 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 227 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 130

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 104 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 104 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 130 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 130 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 169 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 169 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 195 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 195 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 232 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 232 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 131

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 98 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 98 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 117 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 117 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 170 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 170 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 196 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 196 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 235 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 235 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 132

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 92 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 92 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 99 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 99 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 105 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 105 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 198 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 198 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 236 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 236 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 133

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 93 — exact slot 1

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 93 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 113 — exact slot 4

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 113 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 159 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 159 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 199 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 199 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 237 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 237 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 134

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 100 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 100 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 147 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 147 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 174 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 174 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 201 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 201 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 238 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 238 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 135

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 101 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 101 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 135 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 135 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 175 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 175 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 202 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 202 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 239 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 239 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 136

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 108 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 108 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 163 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 163 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 176 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 176 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 204 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 204 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 243 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 243 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 137

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 102 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 102 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 123 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 123 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 164 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 164 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 205 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 205 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 245 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 245 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 138

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 124 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 124 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 151 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 151 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 179 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 179 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 207 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 207 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 246 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 246 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 139

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 111 — exact slot 3

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 111 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 152 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 152 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 208 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 208 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 248 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 248 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 140

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 119 — exact slot 4

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 119 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 126 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 126 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 182 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 182 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 210 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 210 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 250 — exact slot 11

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 250 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 142

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 106 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 106 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 127 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 127 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 142 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 142 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 184 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 184 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 212 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 212 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 143

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 128 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 128 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 157 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 157 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 171 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 171 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 185 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 185 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 214 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 214 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 144

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 129 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 129 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 158 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 158 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 172 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 172 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 187 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 187 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 216 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 216 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 146

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 131 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 131 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 146 — exact slot 6

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 146 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 160 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 160 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 189 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 189 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 219 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 219 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 147

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 110 — exact slot 2

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 110 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 132 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 132 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 161 — exact slot 7

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 161 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 191 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 191 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 220 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 220 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 149

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 134 — exact slot 5

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 134 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 178 — exact slot 8

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 178 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 193 — exact slot 9

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 193 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 223 — exact slot 10

Evidence status: `PROVEN_HELPBOT`.

```text
/missionharvest start 223 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 156

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 234 — exact slot 10

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 234 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 163

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 244 — exact slot 10

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 244 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 165

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 247 — exact slot 10

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 247 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 177

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 194 — exact slot 7

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 194 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 230 — exact slot 9

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 230 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 178

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 213 — exact slot 8

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 213 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 231 — exact slot 9

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 231 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 180

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 233 — exact slot 9

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 233 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 185

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 203 — exact slot 7

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 203 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 240 — exact slot 9

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 240 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 201

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 221 — exact slot 7

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 221 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 241 — exact slot 8

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 241 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 202

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 242 — exact slot 8

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 242 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 208

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 228 — exact slot 7

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 228 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 249 — exact slot 8

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 249 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

### Character level 209

Select/use an ordinary Rubi-Ka mission terminal, then run each target
below separately.

#### Target QL 209 — exact slot 6

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 209 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```

#### Target QL 229 — exact slot 7

Evidence status: `INFERRED_LOCAL_TABLE`.

```text
/missionharvest start 229 1
```

Wait for completion feedback, then:

```text
/missionharvest status
```
