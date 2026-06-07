# Playfield Remap Candidates

Generated: 2026-06-07 00:11:28

Source of truth used:
- Current-client playfield data: C:\Users\Mike\Documents\Cellao-Clean\CellAO\Built\Debug\playfields.dat
- Teleport audit CSV: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\playfield-teleport-audit.csv
- Wall exit audit CSV: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\playfield-wall-exit-audit.csv

## Purpose

This report identifies old CellAO mapping data that does not line up cleanly with the current AO client playfield data. It is a repair-planning report, not an auto-patch list.

## Summary

| Category | Count | Meaning |
| --- | ---: | --- |
| Definite teleport problems | 67 | Missing source/destination playfield or statel identity in current playfields.dat |
| Wall exit problems | 124 | Wall exit points at missing/null/invalid destination data |
| Interior paired-door candidates | 11 | Current target has a nearby same-template sibling door that may be the real exit/entry |
| Auto-fix safe candidates | 0 | Candidate matches a verified family pattern closely enough for a focused patch + spot-test |
| Needs one live confirmation | 2 | Candidate has strong geometry but no direct verified family representative yet |
| Needs family review | 6 | Candidate is related to a known family but has weaker geometry than the verified pattern |
| Verified fixed/reference candidates | 3 | Already-fixed pairs retained as evidence references |

## Confirmed Pattern

The confirmed failures so far were stale destination-door mappings:

| Interior | Old Door | Corrected Door | Evidence |
| --- | --- | --- | --- |
| Neutral Supermarket Advanced / Superior-style interior 1187 | C00004A3 | C00204A3 | Old target was in/near main room; correct current-client exterior door is at 205,5,120 |
| Neutral Basic Implant Shop 2064 | C0000810 | C0010810 | Old target was the inner-room doorway; real exterior exit is C0010810 at 191,5,164 |
| Neutral Advanced Implant Shop 2073 | C0000819 | C0010819 | Same paired-door pattern as 2064; Borealis entrance playtested successfully after remap |

## Ranked Repair Plan

This ranking is intentionally conservative. Auto-fix safe still means patch plus one spot-test, not a blind mass rewrite.

### Auto-Fix Safe

No auto-fix-safe candidates found.

### Needs One Live Confirmation

| Destination | Inbound Teleport Ids | Current Target | Candidate Target | Distance | Next Action |
| --- | --- | --- | --- | ---: | --- |
| 1183 ord_smarket_omni_basic | 45, 61, 748, 763, 778, 790, 800, 806, 807, 808, 809, 810, 811, 812, 813, 828, 856, 870 | 0xC000049F | 0xC004049F | 4.244 | Confirm one matching supermarket/Fair Trade entrance-exit pair, then patch the family. |
| 4704 Tower Shop (dungeon) | 79, 862, 966 | 0xC0001260 | 0xC0011260 | 16.479 | Capture or test one representative before patching this family. |

### Needs Family Review

| Destination | Inbound Teleport Ids | Current Target | Candidate Target | Distance | Next Action |
| --- | --- | --- | --- | ---: | --- |
| 1186 Neutral Supermarket Basic | 117, 123, 273, 274, 1249, 767, 768, 924, 956, 957, 958 | 0xC00404A2 | 0xC00304A2 | 20.245 | Review as a supermarket-family candidate; distance is larger than the verified close-door pattern. |
| 1181 ord_smarket_clan_advanced | 17, 69, 70, 71, 97, 112, 114, 367 | 0xC000049D | 0xC002049D | 23.693 | Review as a supermarket-family candidate; distance is larger than the verified close-door pattern. |
| 1180 ord_smarket_clan_basic | 18, 66, 67, 68, 90, 115, 491, 496, 502, 581, 582, 589, 590, 591 | 0xC000049C | 0xC002049C | 21.932 | Review as a supermarket-family candidate; distance is larger than the verified close-door pattern. |
| 1182 ord_smarket_clan_sup | 72, 73, 98, 214, 391, 471 | 0xC000049E | 0xC002049E | 17.027 | Review as a supermarket-family candidate; distance is larger than the verified close-door pattern. |
| 1184 ord_smarket_omni_advanced | 44, 764, 765, 779, 801, 814, 815, 816, 817, 857, 871, 928 | 0xC00004A0 | 0xC00204A0 | 25.491 | Review as a supermarket-family candidate; distance is larger than the verified close-door pattern. |
| 1185 ord_smarket_omni_sup | 253, 307, 780, 797, 818, 822, 858, 872 | 0xC00004A1 | 0xC00204A1 | 21.951 | Review as a supermarket-family candidate; distance is larger than the verified close-door pattern. |

### Verified Fixed / Reference

| Destination | Inbound Teleport Ids | Correct Target | Paired Door | Distance | Next Action |
| --- | --- | --- | --- | ---: | --- |
| 2073 neut_advanced_implants_shop | 278, 633, 965 | 0xC0010819 | 0xC0000819 | 9.971 | No patch. Keep as reference evidence for this interior family. |
| 2064 neut_basic_implants_shop | 126, 954 | 0xC0010810 | 0xC0000810 | 9.971 | No patch. Keep as reference evidence for this interior family. |
| 1187 Neutral Supermarket Advanced | 116, 120, 122, 271, 923 | 0xC00204A3 | 0xC00404A3 | 20.224 | No patch. Keep as reference evidence for this interior family. |

## Definite Teleport Problems

| Id | Source | Source Statel | Destination | Destination Statel | Warnings |
| ---: | --- | --- | --- | --- | --- |
| 358 | 640 Tir | 0xC03F0280 | 1329 tir clanbuilding6 dng | 0xC0000531 | missing source statel |
| 372 | 640 Tir | 0xC04A0280 | 1328 tir clanbuilding5 dng | 0xC0000530 | missing source statel |
| 417 | 640 Tir | 0xC07C0280 | 1329 tir clanbuilding6 dng | 0xC0000531 | missing source statel |
| 430 | 640 Tir | 0xC0890280 | 1329 tir clanbuilding6 dng | 0xC0000531 | missing source statel |
| 433 | 640 Tir | 0xC08E0280 | 1328 tir clanbuilding5 dng | 0xC0000530 | missing source statel |
| 436 | 640 Tir | 0xC0910280 | 1326 tir clanbuilding3 | 0xC000052E | missing source statel |
| 447 | 640 Tir | 0xC0A00280 | 1330 tir clanbuilding7 dng | 0xC0000532 | missing source statel |
| 451 | 640 Tir | 0xC0A40280 | 1328 tir clanbuilding5 dng | 0xC0000530 | missing source statel |
| 452 | 640 Tir | 0xC0A50280 | 1328 tir clanbuilding5 dng | 0xC0000530 | missing source statel |
| 474 | 640 Tir | 0xC0BB0280 | 1328 tir clanbuilding5 dng | 0xC0000530 | missing source statel |
| 475 | 640 Tir | 0xC0BC0280 | 1328 tir clanbuilding5 dng | 0xC0000530 | missing source statel |
| 478 | 640 Tir | 0xC0BF0280 | 1322 tir clanbuilding1 dng | 0xC000052A | missing source statel |
| 489 | 640 Tir | 0xC0CB0280 | 1328 tir clanbuilding5 dng | 0xC0000530 | missing source statel |
| 609 | 655 Andromeda | 0xC027028F | 2032 omni_basic_pharmacist | 0xC00007F0 | missing source statel |
| 617 | 655 Andromeda | 0xC028028F | 2031 omni_basic_armor_shop | 0xC00007EF | missing source statel |
| 618 | 655 Andromeda | 0xC024028F | 4376 ICC Assembly Hall (dng) | 0xC0001118 | missing source statel |
| 619 | 655 Andromeda | 0xC00E028F | 0  | 0x00000000 | missing destination playfield; missing destination statel |
| 620 | 655 Andromeda | 0xC00D028F | 0  | 0x00000000 | missing destination playfield; missing destination statel |
| 742 | 670 Clondyke | 0xC096029E | 1644 lowtech_building4 VP dng | 0xC000066C | missing source statel |
| 1247 | 740 Rome Green | 0xC00502E4 | 4530 Jobe Platform | 0xC00511B2 | missing source statel |
| 933 | 795 The Longest Road | 0xC005031B | 2062 neut_basic_pharmacist_shop | 0xC000080E | missing destination statel |
| 959 | 800 Borealis | 0xC0000320 | 2062 neut_basic_pharmacist_shop | 0xC000080E | missing destination statel |
| 1153 | 4524  | 0xC00211AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1154 | 4524  | 0xC00411AC | 1613 swamp_house03 VP dng | 0xC000064D | missing source playfield; missing source statel |
| 1155 | 4524  | 0xC00B11AC | 1611 Swamp_house01 VP dng | 0xC000064B | missing source playfield; missing source statel |
| 1156 | 4524  | 0xC00D11AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1157 | 4524  | 0xC00711AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1158 | 4524  | 0xC00811AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1159 | 4524  | 0xC00911AC | 1613 swamp_house03 VP dng | 0xC000064D | missing source playfield; missing source statel |
| 1160 | 4524  | 0xC00A11AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1161 | 4524  | 0xC00C11AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1162 | 4524  | 0xC00E11AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1163 | 4524  | 0xC00F11AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1164 | 4524  | 0xC01011AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1165 | 4524  | 0xC01111AC | 1613 swamp_house03 VP dng | 0xC000064D | missing source playfield; missing source statel |
| 1166 | 4524  | 0xC01211AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1167 | 4524  | 0xC01311AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1168 | 4524  | 0xC01411AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1169 | 4524  | 0xC01511AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1170 | 4524  | 0xC01611AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1171 | 4524  | 0xC0A111AC | 1702 Home VP dng 2 big | 0xC00006A6 | missing source playfield; missing source statel |
| 1172 | 4524  | 0xC02311AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1173 | 4524  | 0xC02411AC | 1613 swamp_house03 VP dng | 0xC000064D | missing source playfield; missing source statel |
| 1174 | 4524  | 0xC02511AC | 1614 Swamp_house04 VP dng | 0xC001064E | missing source playfield; missing source statel |
| 1175 | 4524  | 0xC02611AC | 1611 Swamp_house01 VP dng | 0xC000064B | missing source playfield; missing source statel |
| 1176 | 4524  | 0xC02711AC | 1614 Swamp_house04 VP dng | 0xC001064E | missing source playfield; missing source statel |
| 1177 | 4524  | 0xC02811AC | 1613 swamp_house03 VP dng | 0xC000064D | missing source playfield; missing source statel |
| 1178 | 4524  | 0xC02911AC | 1614 Swamp_house04 VP dng | 0xC001064E | missing source playfield; missing source statel |
| 1179 | 4524  | 0xC02A11AC | 1611 Swamp_house01 VP dng | 0xC000064B | missing source playfield; missing source statel |
| 1180 | 4524  | 0xC02B11AC | 1613 swamp_house03 VP dng | 0xC000064D | missing source playfield; missing source statel |
| 1181 | 4524  | 0xC02C11AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1182 | 4524  | 0xC02D11AC | 1611 Swamp_house01 VP dng | 0xC000064B | missing source playfield; missing source statel |
| 1183 | 4524  | 0xC02E11AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1184 | 4524  | 0xC03011AC | 1614 Swamp_house04 VP dng | 0xC001064E | missing source playfield; missing source statel |
| 1185 | 4524  | 0xC03111AC | 1613 swamp_house03 VP dng | 0xC000064D | missing source playfield; missing source statel |
| 1186 | 4524  | 0xC02211AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1187 | 4524  | 0xC07611AC | 1613 swamp_house03 VP dng | 0xC000064D | missing source playfield; missing source statel |
| 1188 | 4524  | 0xC0BF11AC | 1614 Swamp_house04 VP dng | 0xC001064E | missing source playfield; missing source statel |
| 1189 | 4524  | 0xC02F11AC | 1614 Swamp_house04 VP dng | 0xC001064E | missing source playfield; missing source statel |
| 1190 | 4524  | 0xC0C011AC | 1614 Swamp_house04 VP dng | 0xC001064E | missing source playfield; missing source statel |
| 1191 | 4524  | 0xC00011AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1192 | 4524  | 0xC00111AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1193 | 4524  | 0xC00311AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1194 | 4524  | 0xC00511AC | 1612 Swamp_house02 | 0xC000064C | missing source playfield; missing source statel |
| 1243 | 6553 Arete Landing | 0xC0031999 | 6131 ICC Holodeck Alien Training | 0xC00117F3 | missing source statel |
| 1244 | 6553 Arete Landing | 0xC0041999 | 1623 wood_shack_03 VP dng | 0xC0000657 | missing source statel |
| 1245 | 6553 Arete Landing | 0xC0051999 | 1627 wood_shack_07 VP dng | 0xC000065B | missing source statel |

## Wall Exit Problems

| Playfield | Segment | Start | End | Destination | Warnings |
| --- | ---: | --- | --- | --- | --- |
| 505 Avalon | 3 | 2331.004,196.747 | 3059.559,196.747 | 551 Wailing Wastes index 8 | invalid destination index |
| 550 Athen Shire | 36 | 2839.993,1549 | 2840,2150 | 585 Aegean index 23 | invalid destination index |
| 550 Athen Shire | 38 | 2691,2170 | 2466.5,2110 | 551 Wailing Wastes index 11 | invalid destination index |
| 550 Athen Shire | 42 | 1849,1980 | 1794,1980 | 551 Wailing Wastes index 9 | invalid destination index |
| 560 Mort | 11 | 286.05,199.996 | 3580.293,199.996 | 565 Newland Desert index 16 | invalid destination index |
| 565 Newland Desert | 0 | 200,1488 | 1985,1488 | 567 Newland index 15 | invalid destination index |
| 565 Newland Desert | 2 | 2000,1473 | 2010.128,333.27 | 567 Newland index 16 | invalid destination index |
| 566 Newland City | 0 | 233.576,313.607 | 233.643,299.668 | 567 Newland index 13 | invalid destination index |
| 566 Newland City | 19 | 354.156,352.474 | 343.152,357.736 | 567 Newland index 14 | invalid destination index |
| 567 Newland | 2 | 1800,323.653 | 1800,1268 | 565 Newland Desert index 14 | invalid destination index |
| 567 Newland | 4 | 1791.018,1280 | 200,1280.004 | 565 Newland Desert index 13 | invalid destination index |
| 585 Aegean | 51 | 949.643,1120.171 | 689.643,1120.171 | 586 Wartorn Valley index 17 | invalid destination index |
| 585 Aegean | 57 | 199.643,1090.171 | 199.643,600.171 | 550 Athen Shire index 12 | invalid destination index |
| 586 Wartorn Valley | 12 | 690,200.1 | 950.1,200 | 585 Aegean index 21 | invalid destination index |
| 586 Wartorn Valley | 14 | 980,200 | 980,640 | 585 Aegean index 20 | invalid destination index |
| 600 Varmint Woods | 10 | 3637.915,3199.849 | 1794.615,3199.849 | 565 Newland Desert index 15 | invalid destination index |
| 600 Varmint Woods | 12 | 1731.315,3199.849 | 257.415,3199.859 | 567 Newland index 17 | invalid destination index |
| 625 Milky Way | 6 | 5040,150 | 5040,673 | 620 Eastern Fouls Plains index 3 | invalid destination index |
| 630 Pleasant Meadows | 28 | 1223.7,200 | 2156.5,200 | 716 Omni Forest index 49 | invalid destination index |
| 630 Pleasant Meadows | 35 | 2252.3,200 | 3349.14,200 | 717 Greater Omni Forest index 44 | invalid destination index |
| 640 Tir | 7 | 643.9,478.5 | 649.8,478.5 | 641 Tir Arena index 4 | invalid destination index |
| 641 Tir Arena | 2 | 649.8,478.5 | 643.9,478.5 | 640 Tir index 33 | invalid destination index |
| 641 Tir Arena | 4 | 596.1,477.4 | 583.9,478.7 | 640 Tir index 32 | invalid destination index |
| 641 Tir Arena | 6 | 582.7,439.6 | 582.4,427.7 | 640 Tir index 31 | invalid destination index |
| 641 Tir Arena | 8 | 644,426.3 | 657,426.1 | 640 Tir index 30 | invalid destination index |
| 646 Tir County | 7 | 3419.243,1482.492 | 3319.243,1486.201 | 647 Greater Tir County index 40 | invalid destination index |
| 646 Tir County | 8 | 3319.243,1486.201 | 3263.079,1439.592 | 647 Greater Tir County index 39 | invalid destination index |
| 646 Tir County | 9 | 3263.079,1439.592 | 3199.243,1417.463 | 647 Greater Tir County index 38 | invalid destination index |
| 646 Tir County | 10 | 3199.243,1417.463 | 3099.215,1359.592 | 647 Greater Tir County index 37 | invalid destination index |
| 647 Greater Tir County | 3 | 228.356,1547.168 | 521.956,1547.11 | 646 Tir County index 40 | invalid destination index |
| 655 Andromeda | 3 | 4930.314,1067.611 | 4933.049,2195.754 | 625 Milky Way index 5 | invalid destination index |
| 656 Coast of Tranquility | 10 | 2514.891,2199.962 | 1938.258,2200 | 695 Lush Fields index 12 | invalid destination index |
| 656 Coast of Tranquility | 12 | 1364.313,2200 | 222.054,2200 | 670 Clondyke index 5 | invalid destination index |
| 685 Galway County | 131 | 366.812,2066.413 | 640,2061.981 | 687 Galway Shire index 21 | invalid destination index |
| 685 Galway County | 132 | 640,2061.981 | 640,1861.981 | 687 Galway Shire index 20 | invalid destination index |
| 685 Galway County | 133 | 640,1861.981 | 680,1788.258 | 687 Galway Shire index 19 | invalid destination index |
| 687 Galway Shire | 5 | 639.27,202.41 | 644.703,283.05 | 685 Galway County index 21 | null destination entry |
| 687 Galway Shire | 6 | 644.703,283.05 | 727.346,443.05 | 685 Galway County index 20 | null destination entry |
| 687 Galway Shire | 9 | 1004.594,883.05 | 997.487,1043.05 | 685 Galway County index 17 | null destination entry |
| 687 Galway Shire | 10 | 997.487,1043.05 | 982.659,1203.05 | 685 Galway County index 16 | null destination entry |
| 687 Galway Shire | 11 | 982.659,1203.05 | 972.331,1243.05 | 685 Galway County index 15 | null destination entry |
| 687 Galway Shire | 12 | 972.331,1243.05 | 966.911,1283.05 | 685 Galway County index 14 | null destination entry |
| 687 Galway Shire | 13 | 966.911,1283.05 | 960.022,1302.724 | 685 Galway County index 13 | null destination entry |
| 687 Galway Shire | 18 | 651.545,1683.05 | 643.69,1803.05 | 685 Galway County index 8 | null destination entry |
| 687 Galway Shire | 19 | 643.69,1803.05 | 625.131,1883.05 | 685 Galway County index 7 | null destination entry |
| 687 Galway Shire | 20 | 625.131,1883.05 | 611.79,2083.05 | 685 Galway County index 6 | null destination entry |
| 705 Omni1 Entertainment | 8 | 583.656,536.132 | 579.849,536.156 | 706 Omni Entertainment Arena index 4 | invalid destination index |
| 706 Omni Entertainment Arena | 8 | 140.339,220.098 | 140.057,215.937 | 705 Omni1 Entertainment index 34 | invalid destination index |
| 717 Greater Omni Forest | 3 | 1213.527,3371.9 | 1200,3256.382 | 716 Omni Forest index 48 | invalid destination index |
| 717 Greater Omni Forest | 4 | 1200,3256.382 | 1160,3219.351 | 716 Omni Forest index 47 | invalid destination index |
| 730 Rome Park | 0 | 386.314,439.077 | 386.537,445.69 | 735 Rome Blue index 14 | invalid destination index |
| 730 Rome Park | 4 | 226.224,444.943 | 226.557,438.357 | 740 Rome Green index 16 | invalid destination index |
| 730 Rome Park | 6 | 226.724,188.885 | 226.796,182.437 | 740 Rome Green index 15 | invalid destination index |
| 730 Rome Park | 10 | 386.837,183.079 | 386.828,189.698 | 735 Rome Blue index 15 | invalid destination index |
| 790 Stret West Bank | 5 | 1110,3300 | 1100,3280 | 791 Holes in the Wall index 18 | invalid destination index |
| 790 Stret West Bank | 117 | 2602.404,222.137 | 2759.419,3469.9 | 650 Upper Stret East Bank index 5 | invalid destination index |
| 4001 Jobe Research | 0 | 855.914,965.129 | 848.092,965.164 | 4310 Nascense Frontier index 4 | invalid destination index |
| 4001 Jobe Research | 1 | 668.561,563.449 | 684.361,532.378 | 4311 Nascense Wilds index 5 | invalid destination index |
| 4001 Jobe Research | 1 | 1104.267,802.479 | 1092.232,818.464 | 4312 Nascense Swamp index 4 | invalid destination index |
| 4310 Nascense Frontier | 1 | 534.985,1826.566 | 562.611,1798.813 | 4311 Nascense Wilds index 4 | invalid destination index |
| 4310 Nascense Frontier | 1 | 1212.383,1723.093 | 1217.352,1740.459 | 4312 Nascense Swamp index 5 | invalid destination index |
| 4311 Nascense Wilds | 1 | 561.954,1798.068 | 543.173,1828.747 | 4310 Nascense Frontier index 5 | invalid destination index |
| 4311 Nascense Wilds | 1 | 683.903,531.846 | 669.453,563.219 | 4001 Jobe Research index 7 | invalid destination index |
| 4312 Nascense Swamp | 1 | 1216.899,1739.19 | 1212.86,1723.556 | 4310 Nascense Frontier index 6 | invalid destination index |
| 4504 West Buggy (DONT DELETE) | 1 | 1277.6,777.3 | 1271,774.3 | 4505 South Buggy (DONT DELETE) index 24 | invalid destination index |
| 4504 West Buggy (DONT DELETE) | 1 | 1263,976.7 | 1255.5,976.3 | 4505 South Buggy (DONT DELETE) index 25 | invalid destination index |
| 4504 West Buggy (DONT DELETE) | 1 | 1186.512,1293.608 | 1185.692,1286.017 | 4504 West Buggy (DONT DELETE) index 29 | invalid destination index |
| 4504 West Buggy (DONT DELETE) | 1 | 1156.9,1759 | 1152,1755.8 | 4504 West Buggy (DONT DELETE) index 38 | invalid destination index |
| 4505 South Buggy (DONT DELETE) | 2 | 799.8,1611.4 | 796,1615.2 | 4504 West Buggy (DONT DELETE) index 23 | invalid destination index |
| 4505 South Buggy (DONT DELETE) | 2 | 1274.9,1583.3 | 1271.6,1575.7 | 4504 West Buggy (DONT DELETE) index 25 | invalid destination index |
| 4505 South Buggy (DONT DELETE) | 2 | 1262.6,1783 | 1256.7,1777.2 | 4504 West Buggy (DONT DELETE) index 28 | invalid destination index |
| 4505 South Buggy (DONT DELETE) | 2 | 1908.6,1488.4 | 1903.6,1493.2 | 4543 East of Elysium index 36 | invalid destination index |
| 4505 South Buggy (DONT DELETE) | 2 | 2074.6,1411.4 | 2070.9,1415.2 | 4543 East of Elysium index 34 | invalid destination index |
| 4505 South Buggy (DONT DELETE) | 2 | 2314.4,1358 | 2309.1,1358 | 4543 East of Elysium index 32 | invalid destination index |
| 4505 South Buggy (DONT DELETE) | 2 | 2401,1281.5 | 2394.8,1286.6 | 4543 East of Elysium index 31 | invalid destination index |
| 4540 South of Elysium | 2 | 799.8,1611.4 | 796,1615.2 | 4541 West of Elysium index 23 | invalid destination index |
| 4540 South of Elysium | 2 | 1274.9,1583.3 | 1271.6,1575.7 | 4541 West of Elysium index 25 | invalid destination index |
| 4540 South of Elysium | 2 | 1262.6,1783 | 1256.7,1777.2 | 4541 West of Elysium index 28 | invalid destination index |
| 4540 South of Elysium | 2 | 1908.6,1488.4 | 1903.6,1493.2 | 4543 East of Elysium index 36 | invalid destination index |
| 4540 South of Elysium | 2 | 2074.6,1411.4 | 2070.9,1415.2 | 4543 East of Elysium index 34 | invalid destination index |

First 80 rows shown. Use playfield-wall-exit-audit.csv for the full list.

## Interior Paired-Door Candidates

These are not guaranteed bad. They are current-client door pairs that match the class of bug we just fixed: the mapped destination door has a nearby same-template sibling door in the same interior.

| Confidence | Destination | Inbound Teleport Ids | Current Target | Current Coords | Candidate Sibling | Candidate Coords | Distance | Reason |
| --- | --- | --- | --- | --- | --- | --- | ---: | --- |
| High-review | 2073 neut_advanced_implants_shop | 278, 633, 965 | 0xC0010819 | 205.004,5.01,205.985 | 0xC0000819 | 205.003,5.01,196.015 | 9.971 | same-template paired door within 12m; matches known implant-shop failure pattern |
| High-review | 2064 neut_basic_implants_shop | 126, 954 | 0xC0010810 | 191.004,5.01,163.985 | 0xC0000810 | 191.003,5.01,154.015 | 9.971 | same-template paired door within 12m; matches known implant-shop failure pattern |
| High-review | 1183 ord_smarket_omni_basic | 45, 61, 748, 763, 778, 790, 800, 806, 807, 808, 809, 810, 811, 812, 813, 828, 856, 870 | 0xC000049F | 187.001,5,116.01 | 0xC004049F | 189.997,5.01,119.015 | 4.244 | same-template paired door within 12m; matches known implant-shop failure pattern |
| Medium | 1187 Neutral Supermarket Advanced | 116, 120, 122, 271, 923 | 0xC00204A3 | 205.001,5.01,120.015 | 0xC00404A3 | 215.986,5.01,136.996 | 20.224 | same-template sibling door within 30m |
| Medium | 1186 Neutral Supermarket Basic | 117, 123, 273, 274, 1249, 767, 768, 924, 956, 957, 958 | 0xC00404A2 | 175.001,5.01,108.015 | 0xC00304A2 | 186.015,5.01,125.003 | 20.245 | same-template sibling door within 30m |
| Medium | 1181 ord_smarket_clan_advanced | 17, 69, 70, 71, 97, 112, 114, 367 | 0xC000049D | 187.001,5,152.01 | 0xC002049D | 197.99,5.001,173 | 23.693 | same-template sibling door within 30m |
| Medium | 1180 ord_smarket_clan_basic | 18, 66, 67, 68, 90, 115, 491, 496, 502, 581, 582, 589, 590, 591 | 0xC000049C | 201.001,3.01,154.015 | 0xC002049C | 190.014,5.01,172.996 | 21.932 | same-template sibling door within 30m |
| Medium | 1182 ord_smarket_clan_sup | 72, 73, 98, 214, 391, 471 | 0xC000049E | 187,6.101,152.012 | 0xC002049E | 198.011,7.101,165 | 17.027 | same-template sibling door within 30m |
| Medium | 1184 ord_smarket_omni_advanced | 44, 764, 765, 779, 801, 814, 815, 816, 817, 857, 871, 928 | 0xC00004A0 | 213.001,5,96.01 | 0xC00204A0 | 201.99,5.001,119 | 25.491 | same-template sibling door within 30m |
| Medium | 1185 ord_smarket_omni_sup | 253, 307, 780, 797, 818, 822, 858, 872 | 0xC00004A1 | 205.001,5,108.01 | 0xC00204A1 | 193.99,5.001,127 | 21.951 | same-template sibling door within 30m |
| Medium | 4704 Tower Shop (dungeon) | 79, 862, 966 | 0xC0001260 | 193.988,9.006,153.992 | 0xC0011260 | 178,9.005,157.986 | 16.479 | same-template sibling door within 30m |

## Repair Rules

1. Patch definite missing destination statels only after checking whether a nearby same-template replacement exists in current `playfields.dat`.
2. Patch repeated interior families as groups once one representative is confirmed in-game.
3. Do not fix wall exits through `teleports.sql`; wall exits need `WallCollision`/playfield destination handling or separate remap logic.
4. Every patch must update both `CellAO/Libraries/Source/CellAO.Database/SqlTables/teleports.sql` and `cellao_codex_clean.teleports`.
