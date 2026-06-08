# Playfield Teleport Audit

Generated: 2026-06-06 23:26:51

Inputs:
- Database: cellao_codex_clean
- Playfield data: C:\Users\Mike\Documents\Cellao-Clean\CellAO\Built\Debug\playfields.dat
- Teleport CSV: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\playfield-teleport-audit.csv
- Wall exit CSV: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\playfield-wall-exit-audit.csv

## Summary

| Area | Count |
| --- | ---: |
| Teleport rows audited | 1245 |
| Teleport rows with warnings | 67 |
| Wall exit segments audited | 528 |
| Wall exit segments with warnings | 124 |

## Teleport Warnings

| Id | Source | Source Statel | Source Coords | Destination | Destination Statel | Destination Coords | Warnings |
| ---: | --- | --- | --- | --- | --- | --- | --- |
| 358 | 640 Tir | 0xC03F0280 |  | 1329 tir clanbuilding6 dng | 0xC0000531 | 125.002,0,209.99 | missing source statel |
| 372 | 640 Tir | 0xC04A0280 |  | 1328 tir clanbuilding5 dng | 0xC0000530 | 262.998,0,272.01 | missing source statel |
| 417 | 640 Tir | 0xC07C0280 |  | 1329 tir clanbuilding6 dng | 0xC0000531 | 125.002,0,209.99 | missing source statel |
| 430 | 640 Tir | 0xC0890280 |  | 1329 tir clanbuilding6 dng | 0xC0000531 | 125.002,0,209.99 | missing source statel |
| 433 | 640 Tir | 0xC08E0280 |  | 1328 tir clanbuilding5 dng | 0xC0000530 | 262.998,0,272.01 | missing source statel |
| 436 | 640 Tir | 0xC0910280 |  | 1326 tir clanbuilding3 | 0xC000052E | 215.99,0,285.002 | missing source statel |
| 447 | 640 Tir | 0xC0A00280 |  | 1330 tir clanbuilding7 dng | 0xC0000532 | 186.996,0,179.991 | missing source statel |
| 451 | 640 Tir | 0xC0A40280 |  | 1328 tir clanbuilding5 dng | 0xC0000530 | 262.998,0,272.01 | missing source statel |
| 452 | 640 Tir | 0xC0A50280 |  | 1328 tir clanbuilding5 dng | 0xC0000530 | 262.998,0,272.01 | missing source statel |
| 474 | 640 Tir | 0xC0BB0280 |  | 1328 tir clanbuilding5 dng | 0xC0000530 | 262.998,0,272.01 | missing source statel |
| 475 | 640 Tir | 0xC0BC0280 |  | 1328 tir clanbuilding5 dng | 0xC0000530 | 262.998,0,272.01 | missing source statel |
| 478 | 640 Tir | 0xC0BF0280 |  | 1322 tir clanbuilding1 dng | 0xC000052A | 210.998,0,193.99 | missing source statel |
| 489 | 640 Tir | 0xC0CB0280 |  | 1328 tir clanbuilding5 dng | 0xC0000530 | 262.998,0,272.01 | missing source statel |
| 609 | 655 Andromeda | 0xC027028F |  | 2032 omni_basic_pharmacist | 0xC00007F0 | 117.002,5,215.99 | missing source statel |
| 617 | 655 Andromeda | 0xC028028F |  | 2031 omni_basic_armor_shop | 0xC00007EF | 113.002,5,221.99 | missing source statel |
| 618 | 655 Andromeda | 0xC024028F |  | 4376 ICC Assembly Hall (dng) | 0xC0001118 | 175,6.02,170.015 | missing source statel |
| 619 | 655 Andromeda | 0xC00E028F | 782.401,16.109,637.362 | 0  | 0x00000000 |  | missing destination playfield; missing destination statel |
| 620 | 655 Andromeda | 0xC00D028F | 789.356,6.635,649.866 | 0  | 0x00000000 |  | missing destination playfield; missing destination statel |
| 742 | 670 Clondyke | 0xC096029E |  | 1644 lowtech_building4 VP dng | 0xC000066C | 217.002,5,167.99 | missing source statel |
| 1247 | 740 Rome Green | 0xC00502E4 |  | 4530 Jobe Platform | 0xC00511B2 | 285.157,201.36,620.65 | missing source statel |
| 933 | 795 The Longest Road | 0xC005031B | 3669.381,30.599,550.637 | 2062 neut_basic_pharmacist_shop | 0xC000080E |  | missing destination statel |
| 959 | 800 Borealis | 0xC0000320 | 624.976,69.653,427.178 | 2062 neut_basic_pharmacist_shop | 0xC000080E |  | missing destination statel |
| 1153 | 4524  | 0xC00211AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1154 | 4524  | 0xC00411AC |  | 1613 swamp_house03 VP dng | 0xC000064D | 201.002,5,131.99 | missing source playfield; missing source statel |
| 1155 | 4524  | 0xC00B11AC |  | 1611 Swamp_house01 VP dng | 0xC000064B | 153.002,5,171.99 | missing source playfield; missing source statel |
| 1156 | 4524  | 0xC00D11AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1157 | 4524  | 0xC00711AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1158 | 4524  | 0xC00811AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1159 | 4524  | 0xC00911AC |  | 1613 swamp_house03 VP dng | 0xC000064D | 201.002,5,131.99 | missing source playfield; missing source statel |
| 1160 | 4524  | 0xC00A11AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1161 | 4524  | 0xC00C11AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1162 | 4524  | 0xC00E11AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1163 | 4524  | 0xC00F11AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1164 | 4524  | 0xC01011AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1165 | 4524  | 0xC01111AC |  | 1613 swamp_house03 VP dng | 0xC000064D | 201.002,5,131.99 | missing source playfield; missing source statel |
| 1166 | 4524  | 0xC01211AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1167 | 4524  | 0xC01311AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1168 | 4524  | 0xC01411AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1169 | 4524  | 0xC01511AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1170 | 4524  | 0xC01611AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1171 | 4524  | 0xC0A111AC |  | 1702 Home VP dng 2 big | 0xC00006A6 | 235.001,5,105.99 | missing source playfield; missing source statel |
| 1172 | 4524  | 0xC02311AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1173 | 4524  | 0xC02411AC |  | 1613 swamp_house03 VP dng | 0xC000064D | 201.002,5,131.99 | missing source playfield; missing source statel |
| 1174 | 4524  | 0xC02511AC |  | 1614 Swamp_house04 VP dng | 0xC001064E | 187.002,5,91.99 | missing source playfield; missing source statel |
| 1175 | 4524  | 0xC02611AC |  | 1611 Swamp_house01 VP dng | 0xC000064B | 153.002,5,171.99 | missing source playfield; missing source statel |
| 1176 | 4524  | 0xC02711AC |  | 1614 Swamp_house04 VP dng | 0xC001064E | 187.002,5,91.99 | missing source playfield; missing source statel |
| 1177 | 4524  | 0xC02811AC |  | 1613 swamp_house03 VP dng | 0xC000064D | 201.002,5,131.99 | missing source playfield; missing source statel |
| 1178 | 4524  | 0xC02911AC |  | 1614 Swamp_house04 VP dng | 0xC001064E | 187.002,5,91.99 | missing source playfield; missing source statel |
| 1179 | 4524  | 0xC02A11AC |  | 1611 Swamp_house01 VP dng | 0xC000064B | 153.002,5,171.99 | missing source playfield; missing source statel |
| 1180 | 4524  | 0xC02B11AC |  | 1613 swamp_house03 VP dng | 0xC000064D | 201.002,5,131.99 | missing source playfield; missing source statel |
| 1181 | 4524  | 0xC02C11AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1182 | 4524  | 0xC02D11AC |  | 1611 Swamp_house01 VP dng | 0xC000064B | 153.002,5,171.99 | missing source playfield; missing source statel |
| 1183 | 4524  | 0xC02E11AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1184 | 4524  | 0xC03011AC |  | 1614 Swamp_house04 VP dng | 0xC001064E | 187.002,5,91.99 | missing source playfield; missing source statel |
| 1185 | 4524  | 0xC03111AC |  | 1613 swamp_house03 VP dng | 0xC000064D | 201.002,5,131.99 | missing source playfield; missing source statel |
| 1186 | 4524  | 0xC02211AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1187 | 4524  | 0xC07611AC |  | 1613 swamp_house03 VP dng | 0xC000064D | 201.002,5,131.99 | missing source playfield; missing source statel |
| 1188 | 4524  | 0xC0BF11AC |  | 1614 Swamp_house04 VP dng | 0xC001064E | 187.002,5,91.99 | missing source playfield; missing source statel |
| 1189 | 4524  | 0xC02F11AC |  | 1614 Swamp_house04 VP dng | 0xC001064E | 187.002,5,91.99 | missing source playfield; missing source statel |
| 1190 | 4524  | 0xC0C011AC |  | 1614 Swamp_house04 VP dng | 0xC001064E | 187.002,5,91.99 | missing source playfield; missing source statel |
| 1191 | 4524  | 0xC00011AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1192 | 4524  | 0xC00111AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1193 | 4524  | 0xC00311AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1194 | 4524  | 0xC00511AC |  | 1612 Swamp_house02 | 0xC000064C | 185.002,5,131.99 | missing source playfield; missing source statel |
| 1243 | 6553 Arete Landing | 0xC0031999 |  | 6131 ICC Holodeck Alien Training | 0xC00117F3 | 186.9,6.017,114 | missing source statel |
| 1244 | 6553 Arete Landing | 0xC0041999 |  | 1623 wood_shack_03 VP dng | 0xC0000657 | 200.998,10,143.99 | missing source statel |
| 1245 | 6553 Arete Landing | 0xC0051999 |  | 1627 wood_shack_07 VP dng | 0xC000065B | 202.992,5,155.994 | missing source statel |

## Newland / Shop-Related Rows

| Id | Source | Source Statel | Source Coords | Destination | Destination Statel | Destination Coords | Warnings |
| ---: | --- | --- | --- | --- | --- | --- | --- |
| 113 | 560 Mort | 0xC00D0230 | 2835.162,37.167,1931.276 | 4102 Beer And Booze (dng) | 0xC0001006 | 163.004,6.001,166.015 |  |
| 114 | 560 Mort | 0xC0070230 | 1866.855,47.695,1102.495 | 1181 ord_smarket_clan_advanced | 0xC000049D | 187.001,5,152.01 |  |
| 115 | 560 Mort | 0xC0080230 | 1874.302,48.142,1110.562 | 1180 ord_smarket_clan_basic | 0xC000049C | 201.001,3.01,154.015 |  |
| 116 | 560 Mort | 0xC0090230 | 2829.719,36.792,1891.635 | 1187 Neutral Supermarket Advanced | 0xC00204A3 | 205.001,5.01,120.015 |  |
| 117 | 560 Mort | 0xC00A0230 | 2844.081,36.995,1888.5 | 1186 Neutral Supermarket Basic | 0xC00404A2 | 175.001,5.01,108.015 |  |
| 118 | 560 Mort | 0xC00B0230 | 1858.855,47.609,1110.457 | 2010 clan_advanced_weapons_shop | 0xC00007DA | 195.001,5,186.01 |  |
| 119 | 565 Newland Desert | 0xC0000235 | 1546.475,31.502,2717.811 | 4354 Uncle Bazzits Workshop (Dng) | 0xC0001102 | 183.002,6.02,155.985 |  |
| 120 | 565 Newland Desert | 0xC0070235 | 2210.813,22.74,1567.062 | 1187 Neutral Supermarket Advanced | 0xC00204A3 | 205.001,5.01,120.015 |  |
| 121 | 566 Newland City | 0xC0070236 | 305.851,34.813,305.458 | 1137 mir shop omni | 0xC0000471 | 151.001,5,180.015 |  |
| 122 | 566 Newland City | 0xC0090236 | 295.808,32.665,323.171 | 1187 Neutral Supermarket Advanced | 0xC00204A3 | 205.001,5.01,120.015 |  |
| 123 | 566 Newland City | 0xC0080236 | 287.108,32.673,327.558 | 1186 Neutral Supermarket Basic | 0xC00404A2 | 175.001,5.01,108.015 |  |
| 124 | 566 Newland City | 0xC0040236 | 289.804,32.585,313.208 | 1193 spec_smarket_neut_basic | 0xC00004A9 | 201.001,5,112.01 |  |
| 125 | 566 Newland City | 0xC0020236 | 330.081,32.813,313.895 | 2063 neut_basic_clothes_shop | 0xC000080F | 217.002,5,179.99 |  |
| 126 | 566 Newland City | 0xC0060236 | 256.119,34.813,325.373 | 2064 neut_basic_implants_shop | 0xC0010810 | 191.004,5.01,163.985 |  |
| 127 | 566 Newland City | 0xC0200236 | 444.443,25.217,340.344 | 1902 Neuters 'R' Us | 0xC002076E | 159.007,0.011,219.993 |  |
| 271 | 630 Pleasant Meadows | 0xC00F0276 | 1150,10.813,2355.951 | 1187 Neutral Supermarket Advanced | 0xC00204A3 | 205.001,5.01,120.015 |  |
| 272 | 630 Pleasant Meadows | 0xC0100276 | 1190.175,8.591,2353.387 | 1193 spec_smarket_neut_basic | 0xC00004A9 | 201.001,5,112.01 |  |
| 273 | 630 Pleasant Meadows | 0xC0110276 | 2268.147,3.191,1855.982 | 1186 Neutral Supermarket Basic | 0xC00404A2 | 175.001,5.01,108.015 |  |
| 274 | 630 Pleasant Meadows | 0xC0120276 | 1158.223,8.591,2330.389 | 1186 Neutral Supermarket Basic | 0xC00404A2 | 175.001,5.01,108.015 |  |
| 1249 | 655 Andromeda | 0xC021028F | 3208.815,36.277,949.251 | 1186 Neutral Supermarket Basic | 0xC00404A2 | 175.001,5.01,108.015 |  |
| 752 | 685 Galway County | 0xC00102AD | 2395.066,3.402,1295.501 | 1137 mir shop omni | 0xC0000471 | 151.001,5,180.015 |  |
| 767 | 695 Lush Fields | 0xC01902B7 | 1590.851,47.413,524.4 | 1186 Neutral Supermarket Basic | 0xC00404A2 | 175.001,5.01,108.015 |  |
| 768 | 695 Lush Fields | 0xC01C02B7 | 2825.349,12.413,2871.5 | 1186 Neutral Supermarket Basic | 0xC00404A2 | 175.001,5.01,108.015 |  |
| 769 | 695 Lush Fields | 0xC01A02B7 | 1511.893,46.822,524.146 | 1193 spec_smarket_neut_basic | 0xC00004A9 | 201.001,5,112.01 |  |
| 786 | 705 Omni1 Entertainment | 0xC01402C1 | 665.759,22.762,313.058 | 1137 mir shop omni | 0xC0000471 | 151.001,5,180.015 |  |
| 787 | 705 Omni1 Entertainment | 0xC01502C1 | 665.76,22.762,361.058 | 1137 mir shop omni | 0xC0000471 | 151.001,5,180.015 |  |
| 823 | 710 Omni-1 Trade | 0xC03002C6 | 482.123,17.617,419.594 | 1137 mir shop omni | 0xC0000471 | 151.001,5,180.015 |  |
| 824 | 710 Omni-1 Trade | 0xC03102C6 | 325.877,17.617,410.406 | 1137 mir shop omni | 0xC0000471 | 151.001,5,180.015 |  |
| 861 | 735 Rome Blue | 0xC00502DF | 675.329,22.257,314.683 | 1137 mir shop omni | 0xC0000471 | 151.001,5,180.015 |  |
| 863 | 740 Rome Green | 0xC01102E4 | 283.639,22.256,313.89 | 1137 mir shop omni | 0xC0000471 | 151.001,5,180.015 |  |
| 923 | 790 Stret West Bank | 0xC0060316 | 1115.249,1.735,2764.312 | 1187 Neutral Supermarket Advanced | 0xC00204A3 | 205.001,5.01,120.015 |  |
| 924 | 790 Stret West Bank | 0xC0000316 | 1177.731,6.212,2848.34 | 1186 Neutral Supermarket Basic | 0xC00404A2 | 175.001,5.01,108.015 |  |
| 956 | 800 Borealis | 0xC0150320 | 650.218,68.486,575.725 | 1186 Neutral Supermarket Basic | 0xC00404A2 | 175.001,5.01,108.015 |  |
| 957 | 800 Borealis | 0xC0160320 | 650.253,68.486,612.863 | 1186 Neutral Supermarket Basic | 0xC00404A2 | 175.001,5.01,108.015 |  |
| 958 | 800 Borealis | 0xC0170320 | 650.265,68.486,649.818 | 1186 Neutral Supermarket Basic | 0xC00404A2 | 175.001,5.01,108.015 |  |

## Wall Exit Warnings

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
| 4540 South of Elysium | 2 | 2314.4,1358 | 2309.1,1358 | 4543 East of Elysium index 32 | invalid destination index |
| 4540 South of Elysium | 2 | 2401,1281.5 | 2394.8,1286.6 | 4543 East of Elysium index 31 | invalid destination index |
| 4541 West of Elysium | 2 | 1271,774.3 | 1274.8,783.2 | 4540 South of Elysium index 24 | invalid destination index |
| 4541 West of Elysium | 2 | 1255.5,976.3 | 1262.6,983.2 | 4540 South of Elysium index 25 | invalid destination index |
| 4542 Central Elysium | 2 | 861.333,500.833 | 866.033,503.233 | 4540 South of Elysium index 27 | invalid destination index |
| 4542 Central Elysium | 2 | 687.8,546.9 | 694.6,542.9 | 4540 South of Elysium index 26 | invalid destination index |
| 4542 Central Elysium | 2 | 416.9,1015.3 | 427.9,1003.8 | 4541 West of Elysium index 29 | invalid destination index |
| 4542 Central Elysium | 2 | 392,1481.9 | 392.1,1476.2 | 4541 West of Elysium index 38 | invalid destination index |
| 4542 Central Elysium | 2 | 590.25,1864.384 | 586.289,1863.088 | 4544 North of Elysium index 27 | invalid destination index |
| 4542 Central Elysium | 2 | 709.7,1857 | 709.7,1850.8 | 4544 North of Elysium index 30 | invalid destination index |
| 4542 Central Elysium | 2 | 782.5,1964.1 | 777.3,1964.1 | 4544 North of Elysium index 31 | invalid destination index |
| 4542 Central Elysium | 2 | 1024.9,1855.1 | 1020.8,1859.1 | 4544 North of Elysium index 33 | invalid destination index |
| 4542 Central Elysium | 2 | 1086.8,1789.8 | 1086.4,1795 | 4544 North of Elysium index 35 | invalid destination index |
| 4542 Central Elysium | 2 | 1283,1584.4 | 1277.3,1584.3 | 4543 East of Elysium index 46 | invalid destination index |
| 4542 Central Elysium | 2 | 1338.4,1445.5 | 1335.6,1457.8 | 4543 East of Elysium index 44 | invalid destination index |
| 4542 Central Elysium | 2 | 1269.7,1172.5 | 1269.7,1175 | 4543 East of Elysium index 42 | invalid destination index |
| 4542 Central Elysium | 2 | 1322.7,1096.7 | 1312.2,1108.6 | 4543 East of Elysium index 39 | invalid destination index |
| 4542 Central Elysium | 2 | 1183.9,629.2 | 1195.3,640.5 | 4543 East of Elysium index 38 | invalid destination index |
| 4543 East of Elysium | 2 | 755.4,365.6 | 760.1,362.5 | 4540 South of Elysium index 31 | invalid destination index |
| 4543 East of Elysium | 2 | 667.7,438 | 675.7,438 | 4540 South of Elysium index 30 | invalid destination index |

First 100 warnings shown. Use the CSV for the full list.

## How To Use

- Statel teleport bugs are repaired in CellAO/Libraries/Source/CellAO.Database/SqlTables/teleports.sql and cellao_codex_clean.teleports.
- Wall collision bugs are not repaired through 	eleports.sql; inspect playfields.dat wall exits and WallCollision handling.
- For any bad in-game zone, capture/log the exact trigger first: Statel collision firing means a statel mapping issue; Wall collision zoning means a wall exit issue.
