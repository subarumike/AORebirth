#!/usr/bin/env python3
"""Aggregate capture-backed combat contracts for every observed Subway archetype."""

from __future__ import annotations

import csv
import json
import math
import re
from collections import Counter, defaultdict
from datetime import datetime
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
CAPTURE_ROOT = REPO / "tools-temp" / "AOSharpLiveCapture" / "bin" / "Debug" / "captures"
CAPTURES = (
    "20260708-004038",
    "20260708-143600",
    "20260709-193914",
    "20260709-205921",
    "20260709-210452",
    "20260709-212115",
    "20260709-212336",
    "20260709-213711",
    "20260709-220439",
    "20260709-222339",
    "20260709-225408",
    "20260710-202132",
    "20260710-205400",
    "20260710-211430",
    "20260712-153918",
    "20260713-014714",
    "20260713-033511",
    "20260716-033326",
    "20260716-034104",
    "20260716-034433",
    "20260716-034559",
    "20260716-034656",
    "20260716-220400",
    "20260716-221358",
    "20260716-222201",
    "20260717-214612",
    "20260717-214751",
    "20260717-215250",
    "20260719-020104",
    "20260719-021022",
)
CAPTURE_ENEMY_FILTERS = {
    "20260708-004038": frozenset({"Filth Flea"}),
    "20260708-143600": frozenset(
        {
            "Deranged Shopper",
            "Discarded Pet",
            "Disobedient Bot",
            "Violent Vagabond",
        }
    ),
    "20260709-213711": frozenset({"Architect Striker", "Workman Striker"}),
    "20260710-202132": frozenset({"Deranged Shopper"}),
    "20260712-153918": frozenset({"Disobedient Bot"}),
    "20260713-014714": frozenset({"Disobedient Bot"}),
    "20260713-033511": frozenset({"Disobedient Bot"}),
    "20260716-034433": frozenset({"Vergil Aeneid"}),
    "20260716-034559": frozenset({"Melded Patterns"}),
    "20260716-034656": frozenset({"Slum Runner"}),
    "20260716-220400": frozenset({"Abmouth Supremus"}),
    "20260717-214612": frozenset({"Eumenides"}),
    "20260717-214751": frozenset({"Eumenides"}),
    "20260717-215250": frozenset({"Eumenides"}),
    "20260719-020104": frozenset(
        {"Disobedient Bot", "Violent Vagabond"}
    ),
    "20260719-021022": frozenset({"Mugger"}),
}
ENEMY_ATTACK_CAPTURE_FILTERS = {
    "Filth Flea": frozenset({"20260708-004038", "20260709-193914"}),
}
# Admit only the reviewed source from a broader capture whose other combat
# rows are outside the focused enemy evidence boundary.
REVIEWED_ATTACK_INFO_SOURCES = {
    ("Filth Flea", "20260709-205921"): frozenset(
        {"(SimpleChar:79531748)"}
    ),
}
# Keep local-player, player-owned-pet, and other-player targets separate.  Only
# local-player hits feed the player-facing aggregate used by runtime reviews.
TARGET_ROLE_EVIDENCE_ENEMIES = frozenset(
    {
        "Abmouth Supremus",
        "Architect Striker",
        "Disobedient Bot",
        "Empty Shell",
        "Infected Attendant",
        "Lost Thought",
        "Premature Pattern",
        "Strike Foreman",
        "Uncontrollable Anger",
        "Vergil Aeneid",
        "Violent Vagabond",
        "Workman Striker",
    }
)
PLAYER_OWNED_PET_TARGETS = {
    "20260708-143600": frozenset({"(SimpleChar:794DF23C)"}),
    "20260709-210452": frozenset({"(SimpleChar:7953AE99)"}),
    "20260709-213711": frozenset({"(SimpleChar:7953AE99)"}),
    "20260709-222339": frozenset({"(SimpleChar:7954523C)"}),
    "20260716-034433": frozenset({"(SimpleChar:796D400B)"}),
    "20260716-220400": frozenset(
        {"(SimpleChar:7970253A)", "(SimpleChar:7970253C)"}
    ),
}
OTHER_PLAYER_TARGETS = {
    "20260709-222339": frozenset({"(SimpleChar:794D8062)"}),
    "20260709-225408": frozenset({"(SimpleChar:7730002E)"}),
    "20260712-153918": frozenset({"(SimpleChar:795AB07F)"}),
}
REVIEWED_PLAYER_OWNED_PET_TARGETS_BY_ENEMY = {
    ("20260709-220439", "Infected Attendant"): frozenset(
        {"(SimpleChar:7953AE99)"}
    ),
    ("20260709-225408", "Infected Attendant"): frozenset(
        {"(SimpleChar:7954523C)"}
    ),
    ("20260710-211430", "Premature Pattern"): frozenset(
        {"(SimpleChar:7958802A)"}
    ),
}
REVIEWED_OTHER_PLAYER_TARGETS_BY_ENEMY = {
    ("20260709-220439", "Infected Attendant"): frozenset(
        {"(SimpleChar:7730002E)"}
    ),
    ("20260709-225408", "Empty Shell"): frozenset(
        {"(SimpleChar:77300149)"}
    ),
    ("20260710-211430", "Premature Pattern"): frozenset(
        {"(SimpleChar:77300149)"}
    ),
}
REVIEWED_PROACTIVE_LOCAL_ACQUISITIONS = {
    "Empty Shell": (
        {
            "capture": "20260709-222339",
            "sourceIdentity": "(SimpleChar:79545178)",
            "targetIdentity": "(SimpleChar:7944C065)",
            "capturedUtc": "2026-07-10T03:29:39.9995508Z",
        },
        {
            "capture": "20260709-222339",
            "sourceIdentity": "(SimpleChar:79545182)",
            "targetIdentity": "(SimpleChar:7944C065)",
            "capturedUtc": "2026-07-10T03:29:45.1172128Z",
        },
        {
            "capture": "20260709-222339",
            "sourceIdentity": "(SimpleChar:79545183)",
            "targetIdentity": "(SimpleChar:7944C065)",
            "capturedUtc": "2026-07-10T03:29:45.7667310Z",
        },
        {
            "capture": "20260709-222339",
            "sourceIdentity": "(SimpleChar:79545175)",
            "targetIdentity": "(SimpleChar:7944C065)",
            "capturedUtc": "2026-07-10T03:29:53.9664901Z",
        },
        {
            "capture": "20260709-222339",
            "sourceIdentity": "(SimpleChar:7954519B)",
            "targetIdentity": "(SimpleChar:7944C065)",
            "capturedUtc": "2026-07-10T03:29:58.6995895Z",
        },
        {
            "capture": "20260709-222339",
            "sourceIdentity": "(SimpleChar:79545179)",
            "targetIdentity": "(SimpleChar:7944C065)",
            "capturedUtc": "2026-07-10T03:29:59.5655333Z",
        },
    ),
    "Premature Pattern": (
        {
            "capture": "20260709-222339",
            "sourceIdentity": "(SimpleChar:7954516B)",
            "targetIdentity": "(SimpleChar:7944C065)",
            "capturedUtc": "2026-07-10T03:29:27.4343340Z",
        },
        {
            "capture": "20260709-222339",
            "sourceIdentity": "(SimpleChar:7954516C)",
            "targetIdentity": "(SimpleChar:7944C065)",
            "capturedUtc": "2026-07-10T03:29:27.6339293Z",
        },
    ),
}
REVIEWED_HOSTILE_NANO_NAMES = {
    26414: "Drain Abilities",
    81998: "Subsonic Blast",
    82482: "Weight of the Guilty",
}
REVIEWED_EVENT_IDENTITIES = {
    "20260709-213711": {
        "(SimpleChar:7953AE99)": {
            "name": "Killer",
            "monsterData": 96195,
            "level": 13,
            "player": False,
            "npc": False,
            "pet": True,
        },
        "(SimpleChar:7953AFBC)": {
            "name": "Workman Striker",
            "monsterData": 203854,
            "level": 16,
            "player": False,
            "npc": True,
            "pet": False,
        },
        "(SimpleChar:7953AFDA)": {
            "name": "Architect Striker",
            "monsterData": 203743,
            "level": 15,
            "player": False,
            "npc": True,
            "pet": False,
        },
        "(SimpleChar:7953AFDD)": {
            "name": "Workman Striker",
            "monsterData": 203854,
            "level": 16,
            "player": False,
            "npc": True,
            "pet": False,
        },
    }
}
REVIEWED_RAW_TARGET_ROLE_PACKETS = {
    "20260709-222339": (
        {
            "capturedUtc": "2026-07-10T03:28:45.1013226Z",
            "sequence": 4771,
            "length": 38,
            "messageType": "Attack",
            "rawHex": "133D000A0001002600000DB97944C065284940700000C3507954512E000000C350794D806200",
            "source": "(SimpleChar:7954512E)",
            "target": "(SimpleChar:794D8062)",
            "targetRole": "otherPlayer",
        },
        {
            "capturedUtc": "2026-07-10T03:28:50.6684934Z",
            "sequence": 4870,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "13A0000A0001003D00000DB97944C06546002F160000C3507954512E000000002800000013000000060000C350794D8062000000000000000400000000",
            "source": "(SimpleChar:7954512E)",
            "target": "(SimpleChar:794D8062)",
            "targetRole": "otherPlayer",
            "amount": 40,
            "weaponSlot": 6,
            "attackInfoUnknown": 0,
            "hitType": "Critical",
            "weaponInstance": 0,
        },
        {
            "capturedUtc": "2026-07-10T03:28:55.5176374Z",
            "sequence": 4963,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "13FD000A0001003D00000DB97944C06546002F160000C3507954512E000000001200000012000000060000C350794D8062000000000000000300000000",
            "source": "(SimpleChar:7954512E)",
            "target": "(SimpleChar:794D8062)",
            "targetRole": "otherPlayer",
            "amount": 18,
            "weaponSlot": 6,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0,
        },
        {
            "capturedUtc": "2026-07-10T03:29:00.5184911Z",
            "sequence": 5107,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "148D000A0001003D00000DB97944C06546002F160000C3507954512E000000001200000011000000060000C350794D8062000000000000000300000000",
            "source": "(SimpleChar:7954512E)",
            "target": "(SimpleChar:794D8062)",
            "targetRole": "otherPlayer",
            "amount": 18,
            "weaponSlot": 6,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0,
        },
    ),
    "20260709-225408": (
        {
            "capturedUtc": "2026-07-10T03:55:04.8152525Z",
            "sequence": 1954,
            "length": 38,
            "messageType": "Attack",
            "rawHex": "0B2E000A0001002600000DB97944C065284940700000C3507953AD4C000000C3507730002E00",
            "enemyName": "Violent Vagabond",
            "monsterData": 203733,
            "source": "(SimpleChar:7953AD4C)",
            "target": "(SimpleChar:7730002E)",
            "targetRole": "otherPlayer",
        },
        {
            "capturedUtc": "2026-07-10T03:55:04.8152525Z",
            "sequence": 1956,
            "length": 38,
            "messageType": "Attack",
            "rawHex": "0B30000A0001002600000DB97944C065284940700000C3507953AD4A000000C3507730002E00",
            "enemyName": "Violent Vagabond",
            "monsterData": 203733,
            "source": "(SimpleChar:7953AD4A)",
            "target": "(SimpleChar:7730002E)",
            "targetRole": "otherPlayer",
        },
        {
            "capturedUtc": "2026-07-10T03:59:37.9799584Z",
            "sequence": 8437,
            "length": 38,
            "messageType": "Attack",
            "rawHex": "2481000A0001002600000DB97944C065284940700000C3507954519B000000C3507730014900",
            "enemyName": "Empty Shell",
            "monsterData": 203731,
            "source": "(SimpleChar:7954519B)",
            "target": "(SimpleChar:77300149)",
            "targetRole": "otherPlayer",
        },
        {
            "capturedUtc": "2026-07-10T03:59:41.7687763Z",
            "sequence": 8547,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "24EF000A0001003D00000DB97944C06546002F160000C3507954519B0000000013FFFFFFFF000000000000C35077300149000000000000000353495731",
            "enemyName": "Empty Shell",
            "monsterData": 203731,
            "source": "(SimpleChar:7954519B)",
            "target": "(SimpleChar:77300149)",
            "targetRole": "otherPlayer",
            "amount": 19,
            "weaponSlot": 0,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0x53495731,
        },
    ),
    "20260710-211430": (
        {
            "capturedUtc": "2026-07-11T02:17:53.3703315Z",
            "sequence": 6076,
            "length": 38,
            "messageType": "Attack",
            "rawHex": "18F0000A0001002600000DB47944C065284940700000C3507957E65A000000C3507730014900",
            "enemyName": "Premature Pattern",
            "monsterData": 203727,
            "source": "(SimpleChar:7957E65A)",
            "target": "(SimpleChar:77300149)",
            "targetRole": "otherPlayer",
        },
        {
            "capturedUtc": "2026-07-11T02:18:01.3966646Z",
            "sequence": 6268,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "19B0000A0001003D00000DB47944C06546002F160000C3507957E65A0000000010FFFFFFFF000000000000C35077300149000000000000000353495731",
            "enemyName": "Premature Pattern",
            "monsterData": 203727,
            "source": "(SimpleChar:7957E65A)",
            "target": "(SimpleChar:77300149)",
            "targetRole": "otherPlayer",
            "amount": 16,
            "weaponSlot": 0,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0x53495731,
        },
        {
            "capturedUtc": "2026-07-11T02:18:03.6765617Z",
            "sequence": 6335,
            "length": 38,
            "messageType": "Attack",
            "rawHex": "19F3000A0001002600000DB47944C065284940700000C3507957E65A000000C3507958802A00",
            "enemyName": "Premature Pattern",
            "monsterData": 203727,
            "source": "(SimpleChar:7957E65A)",
            "target": "(SimpleChar:7958802A)",
            "targetRole": "playerOwnedPet",
        },
        {
            "capturedUtc": "2026-07-11T02:18:05.2377313Z",
            "sequence": 6383,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "1A23000A0001003D00000DB47944C06546002F160000C3507957E65A0000000026FFFFFFFF000000000000C3507958802A000000000000000353495731",
            "enemyName": "Premature Pattern",
            "monsterData": 203727,
            "source": "(SimpleChar:7957E65A)",
            "target": "(SimpleChar:7958802A)",
            "targetRole": "playerOwnedPet",
            "amount": 38,
            "weaponSlot": 0,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0x53495731,
        },
        {
            "capturedUtc": "2026-07-11T02:18:07.7031157Z",
            "sequence": 6457,
            "length": 38,
            "messageType": "Attack",
            "rawHex": "1A6D000A0001002600000DB47944C065284940700000C3507957E65A000000C3507730014900",
            "enemyName": "Premature Pattern",
            "monsterData": 203727,
            "source": "(SimpleChar:7957E65A)",
            "target": "(SimpleChar:77300149)",
            "targetRole": "otherPlayer",
        },
    ),
    "20260712-153918": (
        {
            "capturedUtc": "2026-07-12T20:43:16.7626155Z",
            "sequence": 3662,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "0AA3000A0001003D00000DB47944C06546002F160000C350795EC78D0000000008FFFFFFFF000000000000C350795AB07F000000000000000353495731",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:795EC78D)",
            "target": "(SimpleChar:795AB07F)",
            "targetRole": "otherPlayer",
            "amount": 8,
            "weaponSlot": 0,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0x53495731,
        },
        {
            "capturedUtc": "2026-07-12T20:43:22.5774603Z",
            "sequence": 3743,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "0AF4000A0001003D00000DB47944C06546002F160000C350795EC78D0000000008FFFFFFFF000000000000C350795AB07F000000000000000353495731",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:795EC78D)",
            "target": "(SimpleChar:795AB07F)",
            "targetRole": "otherPlayer",
            "amount": 8,
            "weaponSlot": 0,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0x53495731,
        },
        {
            "capturedUtc": "2026-07-12T20:43:28.3824738Z",
            "sequence": 3828,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "0B49000A0001003D00000DB47944C06546002F160000C350795EC78D0000000008FFFFFFFF000000000000C350795AB07F000000000000000353495731",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:795EC78D)",
            "target": "(SimpleChar:795AB07F)",
            "targetRole": "otherPlayer",
            "amount": 8,
            "weaponSlot": 0,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0x53495731,
        },
    ),
    "20260713-014714": (
        {
            "capturedUtc": "2026-07-13T06:47:19.2700007Z",
            "sequence": 124,
            "length": 38,
            "messageType": "Attack",
            "rawHex": "0224000A0001002600000DB47944C065284940700000C35079607CD0000000C3507944C06500",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:79607CD0)",
            "target": "(SimpleChar:7944C065)",
            "targetRole": "localPlayer",
        },
        {
            "capturedUtc": "2026-07-13T06:47:22.5404450Z",
            "sequence": 207,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "0277000A0001003D00000DB47944C06546002F160000C35079607CD0000000000AFFFFFFFF000000000000C3507944C065000000000000000353495731",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:79607CD0)",
            "target": "(SimpleChar:7944C065)",
            "targetRole": "localPlayer",
            "amount": 10,
            "weaponSlot": 0,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0x53495731,
        },
        {
            "capturedUtc": "2026-07-13T06:47:28.5141679Z",
            "sequence": 346,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "0302000A0001003D00000DB47944C06546002F160000C35079607CD0000000000BFFFFFFFF000000000000C3507944C065000000000000000353495731",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:79607CD0)",
            "target": "(SimpleChar:7944C065)",
            "targetRole": "localPlayer",
            "amount": 11,
            "weaponSlot": 0,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0x53495731,
        },
        {
            "capturedUtc": "2026-07-13T06:47:34.5932972Z",
            "sequence": 496,
            "length": 57,
            "messageType": "MissedAttackInfo",
            "rawHex": "0398000A0001003900000DB47944C0655C654B280000C3507944C06501FFFFFFFF000000000000C35079607CD00000C3507944C06500000000",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:79607CD0)",
            "target": "(SimpleChar:7944C065)",
            "targetRole": "localPlayer",
            "ammoCount": -1,
            "weaponSlot": 0,
            "unknown": 0,
        },
        {
            "capturedUtc": "2026-07-13T06:47:40.4668227Z",
            "sequence": 639,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "0427000A0001003D00000DB47944C06546002F160000C35079607CD0000000000BFFFFFFFF000000000000C3507944C065000000000000000353495731",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:79607CD0)",
            "target": "(SimpleChar:7944C065)",
            "targetRole": "localPlayer",
            "amount": 11,
            "weaponSlot": 0,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0x53495731,
        },
    ),
    "20260713-033511": (
        {
            "capturedUtc": "2026-07-13T08:35:17.0173119Z",
            "sequence": 123,
            "length": 38,
            "messageType": "Attack",
            "rawHex": "031B000A0001002600000DB47944C065284940700000C35079607E2C000000C3507944C06500",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:79607E2C)",
            "target": "(SimpleChar:7944C065)",
            "targetRole": "localPlayer",
        },
        {
            "capturedUtc": "2026-07-13T08:35:19.5148109Z",
            "sequence": 190,
            "length": 57,
            "messageType": "MissedAttackInfo",
            "rawHex": "035E000A0001003900000DB47944C0655C654B280000C3507944C06501FFFFFFFF000000000000C35079607E2C0000C3507944C06500000000",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:79607E2C)",
            "target": "(SimpleChar:7944C065)",
            "targetRole": "localPlayer",
            "ammoCount": -1,
            "weaponSlot": 0,
            "unknown": 0,
        },
        {
            "capturedUtc": "2026-07-13T08:35:25.5173816Z",
            "sequence": 345,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "03F9000A0001003D00000DB47944C06546002F160000C35079607E2C000000000AFFFFFFFF000000000000C3507944C065000000000000000353495731",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:79607E2C)",
            "target": "(SimpleChar:7944C065)",
            "targetRole": "localPlayer",
            "amount": 10,
            "weaponSlot": 0,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0x53495731,
        },
        {
            "capturedUtc": "2026-07-13T08:35:31.6572511Z",
            "sequence": 503,
            "length": 57,
            "messageType": "MissedAttackInfo",
            "rawHex": "0497000A0001003900000DB47944C0655C654B280000C3507944C06501FFFFFFFF000000000000C35079607E2C0000C3507944C06500000000",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:79607E2C)",
            "target": "(SimpleChar:7944C065)",
            "targetRole": "localPlayer",
            "ammoCount": -1,
            "weaponSlot": 0,
            "unknown": 0,
        },
        {
            "capturedUtc": "2026-07-13T08:35:37.6402997Z",
            "sequence": 637,
            "length": 57,
            "messageType": "MissedAttackInfo",
            "rawHex": "051D000A0001003900000DB47944C0655C654B280000C3507944C06501FFFFFFFF000000000000C35079607E2C0000C3507944C06500000000",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:79607E2C)",
            "target": "(SimpleChar:7944C065)",
            "targetRole": "localPlayer",
            "ammoCount": -1,
            "weaponSlot": 0,
            "unknown": 0,
        },
        {
            "capturedUtc": "2026-07-13T08:35:49.5411768Z",
            "sequence": 938,
            "length": 57,
            "messageType": "MissedAttackInfo",
            "rawHex": "064A000A0001003900000DB47944C0655C654B280000C3507944C06501FFFFFFFF000000000000C35079607E2C0000C3507944C06500000000",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:79607E2C)",
            "target": "(SimpleChar:7944C065)",
            "targetRole": "localPlayer",
            "ammoCount": -1,
            "weaponSlot": 0,
            "unknown": 0,
        },
        {
            "capturedUtc": "2026-07-13T08:35:55.5457139Z",
            "sequence": 1067,
            "length": 57,
            "messageType": "MissedAttackInfo",
            "rawHex": "06CB000A0001003900000DB47944C0655C654B280000C3507944C06501FFFFFFFF000000000000C35079607E2C0000C3507944C06500000000",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:79607E2C)",
            "target": "(SimpleChar:7944C065)",
            "targetRole": "localPlayer",
            "ammoCount": -1,
            "weaponSlot": 0,
            "unknown": 0,
        },
        {
            "capturedUtc": "2026-07-13T08:36:01.7428204Z",
            "sequence": 1205,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "0755000A0001003D00000DB47944C06546002F160000C35079607E2C000000000AFFFFFFFF000000000000C3507944C065000000000000000353495731",
            "enemyName": "Disobedient Bot",
            "monsterData": 17649,
            "source": "(SimpleChar:79607E2C)",
            "target": "(SimpleChar:7944C065)",
            "targetRole": "localPlayer",
            "amount": 10,
            "weaponSlot": 0,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0x53495731,
        },
    ),
}
CADENCE_UNRESOLVED_ENEMIES = frozenset({"Vergil Aeneid"})
OUTPUT = REPO / "docs" / "generated" / "subway_enemy_combat_contracts.json"

# These directories are overlapping projections of the same running official
# client.  The first tuple member is the canonical provenance.  A 20 ms bound
# covers the audited maximum 16.871 ms logger skew across 212115/212336 and
# the 17 ms Workman critical skew in 213711.  No other capture pair is eligible
# for cross-capture event deduplication.
OVERLAPPING_COMBAT_CAPTURE_RULES = {
    ("20260709-212115", "20260709-212336"): 0.020,
    ("20260709-212115", "20260709-213711"): 0.020,
}

ATTACK_DETAIL = re.compile(
    r"WeaponSlot=(?P<slot>-?\d+).*Unk1=(?P<unknown>-?\d+).*"
    r"HitType=(?P<hit_type>\w+).*WeaponInstance=(?P<instance>-?\d+)"
)
ATTACK_TARGET_DETAIL = re.compile(
    r"\bTarget=(?P<target>\(SimpleChar:[0-9A-F]+\))"
)
MESSAGE_IDENTITY_DETAIL = re.compile(
    r"\bIdentity=(?P<identity>\(SimpleChar:[0-9A-F]+\))"
)
WEAPON_UPDATE = re.compile(
    r"WeaponItemFullUpdateMessage .*"
    r"Owner=(?P<owner>\(SimpleChar:[0-9A-F]+\)).*"
    r"ACGItemLevel=(?P<quality>\d+).*ACGItemTemplateID=(?P<template>\d+).*"
    r"ACGItemTemplateID2=(?P<template2>\d+).*"
    r"Identity=\(WeaponInstance:(?P<weapon>[0-9A-F]+)\)"
)
MISSED_ATTACK_INFO = re.compile(
    r"^(?P<captured_utc>\S+).*MissedAttackInfoMessage .*"
    r"Unknown1=(?P<ammo>-?\d+).*Unknown2=(?P<slot>-?\d+).*"
    r"Attacker=(?P<attacker>\(SimpleChar:[0-9A-F]+\)).*"
    r"Defender=(?P<defender>\(SimpleChar:[0-9A-F]+\)).*"
    r"Unknown3=(?P<unknown>-?\d+)"
)
FINISH_NANO_CASTING = re.compile(
    r"^(?P<captured_utc>\S+) \[IN-N3\].*type=CharacterAction "
    r"identity=(?P<source>\(SimpleChar:[0-9A-F]+\)).*"
    r"Action=FinishNanoCasting .*Parameter2=(?P<nano_id>\d+)"
)
SET_NANO_DURATION = re.compile(
    r"^(?P<captured_utc>\S+) \[IN-N3\].*type=CharacterAction "
    r"identity=(?P<target>\(SimpleChar:[0-9A-F]+\)).*"
    r"Action=SetNanoDuration .*Target=\(NanoProgram:(?P<nano_id>[0-9A-F]+)\) "
    r"Parameter1=-?\d+ Parameter2=(?P<duration_ms>\d+)"
)
DESPAWN_EVENT = re.compile(
    r"^(?P<captured_utc>\S+) \[IN-N3\].*type=Despawn "
    r"identity=(?P<identity>\(SimpleChar:[0-9A-F]+\))"
)
MONSTER_DATA_DETAIL = re.compile(r"\bmonsterData=(?P<monster_data>\d+)")
CHARACTER_EVENT = re.compile(
    r"\[(?:CHAR-SEEN|DYNEL-SPAWNED)\] "
    r"identity=(?P<identity>\(SimpleChar:[0-9A-F]+\)) "
    r"name=(?P<name>.*?) "
    r"player=(?P<player>True|False) npc=(?P<npc>True|False) "
    r"pet=(?P<pet>True|False).*?\blevel=(?P<level>\d+).*?"
    r"\bmonsterData=(?P<monster_data>\d+)"
)


def read_csv(path: Path):
    if not path.exists():
        return []
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def read_json(path: Path):
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8-sig"))


def parse_time(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def parse_precise_seconds(value: str) -> Decimal:
    normalized = value.removesuffix("Z")
    date_text, time_text = normalized.split("T", 1)
    year, month, day = (int(part) for part in date_text.split("-"))
    hour_text, minute_text, second_text = time_text.split(":")
    day_number = datetime(year, month, day).toordinal()
    return (
        Decimal(day_number * 86400)
        + Decimal(int(hour_text) * 3600)
        + Decimal(int(minute_text) * 60)
        + Decimal(second_text)
    )


def rounded_capture_seconds(value: Decimal) -> float:
    return float(value.quantize(Decimal("0.000001"), rounding=ROUND_HALF_UP))


def precise_capture_seconds(value: Decimal) -> float:
    return float(value.quantize(Decimal("0.0000001"), rounding=ROUND_HALF_UP))


def first_value(row: dict[str, str], *names: str) -> str:
    for name in names:
        value = row.get(name, "").strip()
        if value:
            return value
    return ""


def simple_char_identity(value: str) -> str:
    value = value.strip()
    if value.startswith("(SimpleChar:") and value.endswith(")"):
        return value
    if value.startswith("SimpleChar:"):
        return f"({value})"
    if re.fullmatch(r"[0-9A-Fa-f]{8}", value):
        return f"(SimpleChar:{value.upper()})"
    return ""


def attack_target_identity(row: dict[str, str]) -> str:
    target = simple_char_identity(row.get("TargetIdentity", ""))
    if target:
        return target
    target_match = ATTACK_TARGET_DETAIL.search(row.get("Detail", ""))
    return target_match.group("target") if target_match else ""


def add_identity(identities: dict[str, dict[str, object]], row: dict[str, str]):
    identity = simple_char_identity(
        first_value(
            row,
            "Identity",
            "NpcIdentity",
            "NPCIdentity",
            "EnemyIdentity",
            "CharacterIdentity",
            "PrimaryIdentity",
            "identity",
        )
    )
    name = first_value(row, "Name", "NpcName", "NPCName", "EnemyName", "name")
    if not identity or not name or name.startswith("Remains of "):
        return
    monster_data_value = first_value(
        row, "MonsterData", "MonsterDataId", "monsterData"
    )
    if not monster_data_value:
        match = MONSTER_DATA_DETAIL.search(row.get("Detail", ""))
        monster_data_value = match.group("monster_data") if match else ""
    monster_data = int(monster_data_value or 0)
    current = identities.get(identity)
    if current is None or (not current["monsterData"] and monster_data):
        identities[identity] = {
            "name": name,
            "monsterData": monster_data,
        }


def add_reviewed_event_identities(
    capture_name: str,
    events_path: Path,
    identities: dict[str, dict[str, object]],
) -> None:
    reviewed = REVIEWED_EVENT_IDENTITIES.get(capture_name, {})
    if not reviewed or not events_path.exists():
        return
    for line in events_path.read_text(
        encoding="utf-8-sig", errors="replace"
    ).splitlines():
        match = CHARACTER_EVENT.search(line)
        if not match:
            continue
        identity = match.group("identity")
        expected = reviewed.get(identity)
        if expected is None:
            continue
        observed = {
            "name": match.group("name"),
            "monsterData": int(match.group("monster_data")),
            "level": int(match.group("level")),
            "player": match.group("player") == "True",
            "npc": match.group("npc") == "True",
            "pet": match.group("pet") == "True",
        }
        if observed != expected or expected["pet"] or expected["player"]:
            continue
        identities[identity] = {
            "name": expected["name"],
            "monsterData": expected["monsterData"],
        }


def capture_includes_enemy(capture_name: str, enemy_name: str) -> bool:
    allowed_enemies = CAPTURE_ENEMY_FILTERS.get(capture_name)
    return allowed_enemies is None or enemy_name in allowed_enemies


def attack_evidence(
    row: dict[str, str],
    capture_name: str,
    source: str,
    provenance_captures: set[str] | None = None,
):
    amount = int(row.get("Amount") or 0)
    detail = row.get("Detail", "")
    match = ATTACK_DETAIL.search(detail)
    if amount <= 0 or not match:
        return None
    return {
        "capture": capture_name,
        "identity": source,
        "targetIdentity": row.get("TargetIdentity", ""),
        "capturedUtc": row["CapturedUtc"],
        "amount": amount,
        "weaponSlot": int(match.group("slot")),
        "attackInfoUnknown": int(match.group("unknown")),
        "hitType": match.group("hit_type"),
        "weaponInstance": int(match.group("instance")),
        "provenanceCaptures": provenance_captures or {capture_name},
    }


def target_evidence_role(
    capture_name: str, row: dict[str, str], enemy_name: str
) -> str:
    target_identity = attack_target_identity(row)
    if row.get("TargetRole") == "local-player":
        return "localPlayer"
    if target_identity in PLAYER_OWNED_PET_TARGETS.get(
        capture_name, frozenset()
    ) or target_identity in REVIEWED_PLAYER_OWNED_PET_TARGETS_BY_ENEMY.get(
        (capture_name, enemy_name), frozenset()
    ):
        return "playerOwnedPet"
    if target_identity in OTHER_PLAYER_TARGETS.get(
        capture_name, frozenset()
    ) or target_identity in REVIEWED_OTHER_PLAYER_TARGETS_BY_ENEMY.get(
        (capture_name, enemy_name), frozenset()
    ):
        return "otherPlayer"
    return ""


def combat_event_fingerprint(
    row: dict[str, str], evidence_role: str
) -> tuple[object, ...] | None:
    message_type = row.get("MessageType", "")
    if message_type not in {"Attack", "AttackInfo", "MissedAttackInfo"}:
        return None
    source = simple_char_identity(row.get("SourceIdentity", ""))
    if not source:
        return None
    target = attack_target_identity(row)
    target_role = evidence_role or row.get("TargetRole", "")
    if message_type == "AttackInfo":
        detail_match = ATTACK_DETAIL.search(row.get("Detail", ""))
        amount = int(row.get("Amount") or 0)
        if detail_match is None or amount <= 0:
            return None
        hit_shape = (
            int(detail_match.group("slot")),
            int(detail_match.group("unknown")),
            detail_match.group("hit_type"),
            int(detail_match.group("instance")),
        )
    elif message_type == "MissedAttackInfo":
        amount = 0
        hit_shape = (
            int(row.get("AmmoCount") or 0),
            int(row.get("WeaponSlot") or 0),
            int(row.get("Unknown") or 0),
            0,
        )
    else:
        amount = int(row.get("Amount") or 0)
        hit_shape = (0, 0, "", 0)
    return (source, target, target_role, message_type, amount, *hit_shape)


class OverlappingCombatEventDeduplicator:
    """One-to-one dedup across only the audited overlapping capture pairs."""

    def __init__(self) -> None:
        capture_order = {capture: index for index, capture in enumerate(CAPTURES)}
        for canonical, duplicate in OVERLAPPING_COMBAT_CAPTURE_RULES:
            if capture_order[canonical] >= capture_order[duplicate]:
                raise ValueError(
                    "combat overlap canonical capture order drifted: "
                    + canonical
                    + " -> "
                    + duplicate
                )
        self._events = defaultdict(list)
        self._ordinal = 0
        self.exclusions = []

    def observe(
        self,
        capture_name: str,
        row: dict[str, str],
        evidence_role: str,
    ) -> tuple[dict[str, object] | None, bool]:
        fingerprint = combat_event_fingerprint(row, evidence_role)
        captured_utc = row.get("CapturedUtc", "")
        if fingerprint is None or not captured_utc:
            return None, False
        captured_time = parse_time(captured_utc)
        candidates = []
        for canonical in self._events[fingerprint]:
            tolerance = OVERLAPPING_COMBAT_CAPTURE_RULES.get(
                (canonical["capture"], capture_name)
            )
            if (
                tolerance is None
                or capture_name in canonical["provenanceCaptures"]
            ):
                continue
            delta = abs((captured_time - canonical["capturedTime"]).total_seconds())
            if delta <= tolerance:
                candidates.append((delta, canonical["ordinal"], canonical))
        if candidates:
            _, _, canonical = min(candidates, key=lambda item: (item[0], item[1]))
            canonical["provenanceCaptures"].add(capture_name)
            self.exclusions.append(
                {
                    "canonicalCapture": canonical["capture"],
                    "duplicateCapture": capture_name,
                    "canonicalCapturedUtc": canonical["capturedUtc"],
                    "duplicateCapturedUtc": captured_utc,
                    "fingerprint": fingerprint,
                }
            )
            return canonical, True
        event = {
            "capture": capture_name,
            "capturedUtc": captured_utc,
            "capturedTime": captured_time,
            "ordinal": self._ordinal,
            "provenanceCaptures": {capture_name},
        }
        self._ordinal += 1
        self._events[fingerprint].append(event)
        return event, False

    def validate(self) -> None:
        for exclusion in self.exclusions:
            pair = (
                exclusion["canonicalCapture"],
                exclusion["duplicateCapture"],
            )
            if pair not in OVERLAPPING_COMBAT_CAPTURE_RULES:
                raise ValueError(
                    "unreviewed capture pair reached combat dedup: "
                    + " -> ".join(pair)
                )


def validate_workman_striker_distinct_combat(
    attacks: list[dict[str, object]],
    report_entry: dict[str, object],
) -> None:
    expected_counts = {
        "attackInfoRows": 53,
        "normalAttackInfoRows": 47,
        "criticalAttackInfoRows": 6,
    }
    for field, expected in expected_counts.items():
        if report_entry[field] != expected:
            raise ValueError(
                f"Workman Striker distinct {field} drifted: "
                f"expected={expected} actual={report_entry[field]}"
            )
    if report_entry["medianRechargeSeconds"] != 5.092328:
        raise ValueError("Workman Striker distinct cadence drifted")
    shapes = report_entry["attackShapes"]
    if (
        len(shapes) != 1
        or shapes[0]["rows"] != 53
        or shapes[0]["intervalRows"] != 41
        or shapes[0]["medianIntervalSeconds"] != 5.092328
    ):
        raise ValueError("Workman Striker distinct attack-shape cadence drifted")
    canonical_critical = [
        row
        for row in attacks
        if row["capture"] == "20260709-212115"
        and row["identity"] == "(SimpleChar:7953AFBC)"
        and row["capturedUtc"] == "2026-07-10T02:37:39.1002433Z"
        and row["hitType"] == "Critical"
        and row["amount"] == 42
    ]
    if (
        len(canonical_critical) != 1
        or canonical_critical[0]["provenanceCaptures"]
        != {"20260709-212115", "20260709-213711"}
    ):
        raise ValueError("Workman Striker critical canonical provenance drifted")
    unrelated_rows = [
        row for row in attacks if row["capture"] == "20260709-220439"
    ]
    if (
        Counter(row["hitType"] for row in unrelated_rows)
        != Counter({"Normal": 14, "Critical": 2})
        or any(
            row["provenanceCaptures"] != {"20260709-220439"}
            for row in unrelated_rows
        )
    ):
        raise ValueError("non-overlapping Workman Striker combat rows changed")


def validate_discarded_pet_combat(report_entry: dict[str, object]) -> None:
    required_captures = {"20260708-143600", "20260709-210452"}
    if not required_captures.issubset(set(report_entry["captures"])):
        raise ValueError("Discarded Pet focused combat captures are missing")
    if (
        report_entry["normalAttackInfoRows"] != 37
        or report_entry["normalMinDamage"] != 9
        or report_entry["normalMaxDamage"] != 18
        or report_entry["criticalAttackInfoRows"] != 4
        or report_entry["criticalMinDamage"] != 30
        or report_entry["criticalMaxDamage"] != 33
        or report_entry["weaponSlot"] != 0
        or report_entry["attackInfoUnknown"] != 0
        or report_entry["attackInfoWeaponInstance"] != 0x53495731
    ):
        raise ValueError("Discarded Pet SIW1 local-player evidence drifted")
    shapes = report_entry["attackShapes"]
    if (
        len(shapes) != 1
        or shapes[0]["rows"] != 41
        or shapes[0]["intervalRows"] != 25
        or shapes[0]["minIntervalSeconds"] != 4.609299
        or shapes[0]["medianIntervalSeconds"] != 5.079568
        or shapes[0]["maxIntervalSeconds"] != 5.950416
    ):
        raise ValueError("Discarded Pet SIW1 local-player cadence drifted")


def validate_disobedient_bot_combat(report_entry: dict[str, object]) -> None:
    required_captures = {
        "20260708-143600",
        "20260709-205921",
        "20260709-210452",
        "20260712-153918",
        "20260713-014714",
        "20260713-033511",
        "20260719-020104",
    }
    if not required_captures.issubset(set(report_entry["captures"])):
        raise ValueError("Disobedient Bot reviewed combat captures are missing")
    if (
        report_entry["retaliationRows"] != 13
        or report_entry["specialAttackWeaponRows"] != 1
        or report_entry["normalAttackInfoRows"] != 15
        or report_entry["normalMinDamage"] != 6
        or report_entry["normalMaxDamage"] != 15
        or report_entry["criticalAttackInfoRows"] != 0
        or report_entry["missedAttackInfoRows"] != 10
        or report_entry["weaponSlot"] != 0
        or report_entry["attackInfoUnknown"] != 0
        or report_entry["attackInfoWeaponInstance"] != 0x53495731
        or report_entry["medianRechargeSeconds"] != 5.479593
    ):
        raise ValueError("Disobedient Bot local-player SIW1 evidence drifted")
    special_weapon_shapes = report_entry["specialAttackWeaponShapes"]
    if (
        len(special_weapon_shapes) != 1
        or special_weapon_shapes[0]["unknown1"] != 35
        or special_weapon_shapes[0]["unknown2"] != 35
        or special_weapon_shapes[0]["unknown3"] != 35
        or special_weapon_shapes[0]["unknown4"] != 35
        or special_weapon_shapes[0]["unknown5"] != 0
        or special_weapon_shapes[0]["rows"] != 1
        or special_weapon_shapes[0]["captures"] != ["20260719-020104"]
        or special_weapon_shapes[0]["owners"] != ["(SimpleChar:797AD6E4)"]
    ):
        raise ValueError("Disobedient Bot special attack-weapon evidence drifted")
    miss_shapes = report_entry["missedAttackShapes"]
    if (
        len(miss_shapes) != 1
        or miss_shapes[0]["ammoCount"] != -1
        or miss_shapes[0]["weaponSlot"] != 0
        or miss_shapes[0]["unknown"] != 0
        or miss_shapes[0]["rows"] != 10
    ):
        raise ValueError("Disobedient Bot local-player miss evidence drifted")
    target_roles = report_entry["targetRoleEvidence"]
    role_expectations = {
        "localPlayer": (15, 6, 15),
        "playerOwnedPet": (2, 8, 19),
        "otherPlayer": (3, 8, 8),
    }
    for role, expected in role_expectations.items():
        evidence = target_roles[role]
        actual = (
            evidence["attackInfoRows"],
            evidence["minDamage"],
            evidence["maxDamage"],
        )
        if actual != expected:
            raise ValueError(
                "Disobedient Bot target-role evidence drifted role={0} actual={1}".format(
                    role, actual
                )
            )
    focused = [
        row
        for row in report_entry["reviewedRawAttemptCadence"]
        if row["capture"] == "20260713-014714"
        and row["identity"] == "(SimpleChar:79607CD0)"
    ]
    if (
        len(focused) != 1
        or focused[0]["initialDelaySeconds"] != 3.270444
        or focused[0]["attemptRows"] != 4
        or focused[0]["intervalRows"] != 3
        or focused[0]["minIntervalSeconds"] != 5.873526
        or focused[0]["medianIntervalSeconds"] != 5.973723
        or focused[0]["maxIntervalSeconds"] != 6.079129
    ):
        raise ValueError(
            "Disobedient Bot focused attempt cadence drifted: {0}".format(focused)
        )


def validate_mugger_combat(report_entry: dict[str, object]) -> None:
    capture = "20260719-021022"
    identity = "(SimpleChar:797B889D)"
    special_weapon_shapes = report_entry["specialAttackWeaponShapes"]
    miss_shapes = report_entry["missedAttackShapes"]
    if (
        capture not in report_entry["captures"]
        or identity not in report_entry["identities"]
        or report_entry["retaliationRows"] != 20
        or report_entry["attackInfoRows"] != 41
        or report_entry["missedAttackInfoRows"] != 4
        or report_entry["specialAttackWeaponRows"] != 1
        or any(capture in shape["captures"] for shape in report_entry["attackShapes"])
        or len(miss_shapes) != 1
        or miss_shapes[0]["ammoCount"] != -1
        or miss_shapes[0]["weaponSlot"] != 6
        or miss_shapes[0]["unknown"] != 0
        or miss_shapes[0]["rows"] != 4
        or capture not in miss_shapes[0]["captures"]
        or len(special_weapon_shapes) != 1
        or special_weapon_shapes[0]["unknown1"] != 28
        or special_weapon_shapes[0]["unknown2"] != 31
        or special_weapon_shapes[0]["unknown3"] != 17
        or special_weapon_shapes[0]["unknown4"] != 28
        or special_weapon_shapes[0]["unknown5"] != 0
        or special_weapon_shapes[0]["rows"] != 1
        or special_weapon_shapes[0]["captures"] != [capture]
        or special_weapon_shapes[0]["owners"] != [identity]
    ):
        raise ValueError("Mugger reviewed incidental combat evidence drifted")


def reviewed_uncontrollable_anger_cadence(
    group: dict[str, object],
) -> dict[str, object]:
    reviewed = [
        row
        for row in group["targetRoleEvidence"]["playerOwnedPet"]["attacks"]
        if row["capture"] == "20260709-222339"
        and row["identity"] == "(SimpleChar:79545202)"
    ]
    reviewed.sort(key=lambda row: parse_precise_seconds(row["capturedUtc"]))
    expected = (
        ("2026-07-10T03:29:02.8010362Z", 42),
        ("2026-07-10T03:29:07.9175875Z", 25),
        ("2026-07-10T03:29:13.0847400Z", 27),
        ("2026-07-10T03:29:23.1850889Z", 27),
    )
    actual = tuple((row["capturedUtc"], row["amount"]) for row in reviewed)
    if actual != expected or any(
        row["weaponSlot"] != 0
        or row["attackInfoUnknown"] != 0
        or row["hitType"] != "Normal"
        or row["weaponInstance"] != 0x53495731
        for row in reviewed
    ):
        raise ValueError(
            "Uncontrollable Anger reviewed Killer-pet cadence rows drifted: {0}".format(
                actual
            )
        )
    intervals = [
        parse_precise_seconds(current["capturedUtc"])
        - parse_precise_seconds(previous["capturedUtc"])
        for previous, current in zip(reviewed, reviewed[1:])
    ]
    ordered_intervals = sorted(intervals)
    median = ordered_intervals[len(ordered_intervals) // 2]
    return {
        "capture": "20260709-222339",
        "sourceIdentity": "(SimpleChar:79545202)",
        "targetIdentity": "(SimpleChar:7954523C)",
        "targetRole": "playerOwnedPet",
        "attackInfoRows": len(reviewed),
        "intervalSeconds": [float(value) for value in intervals],
        "medianIntervalSeconds": float(median),
        "runtimeRechargeSeconds": rounded_capture_seconds(median),
    }


def validate_uncontrollable_anger_combat(report_entry: dict[str, object]) -> None:
    if (
        report_entry["normalAttackInfoRows"] != 2
        or report_entry["normalMinDamage"] != 11
        or report_entry["normalMaxDamage"] != 18
        or report_entry["criticalAttackInfoRows"] != 0
        or report_entry["weaponSlot"] != 0
        or report_entry["attackInfoUnknown"] != 0
        or report_entry["attackInfoWeaponInstance"] != 0x53495731
        or report_entry["medianRechargeSeconds"] != 5.167153
    ):
        raise ValueError("Uncontrollable Anger local-player combat evidence drifted")
    target_roles = report_entry["targetRoleEvidence"]
    role_expectations = {
        "localPlayer": (2, 11, 18),
        "playerOwnedPet": (4, 25, 42),
        "otherPlayer": (1, 19, 19),
    }
    for role, expected in role_expectations.items():
        evidence = target_roles[role]
        actual = (
            evidence["attackInfoRows"],
            evidence["minDamage"],
            evidence["maxDamage"],
        )
        if actual != expected:
            raise ValueError(
                "Uncontrollable Anger target-role evidence drifted role={0} actual={1}".format(
                    role, actual
                )
            )
    cadence = report_entry["reviewedTargetCadence"]
    if (
        cadence["capture"] != "20260709-222339"
        or cadence["sourceIdentity"] != "(SimpleChar:79545202)"
        or cadence["targetIdentity"] != "(SimpleChar:7954523C)"
        or cadence["targetRole"] != "playerOwnedPet"
        or cadence["attackInfoRows"] != 4
        or cadence["intervalSeconds"] != [5.1165513, 5.1671525, 10.1003489]
        or cadence["medianIntervalSeconds"] != 5.1671525
        or cadence["runtimeRechargeSeconds"] != 5.167153
    ):
        raise ValueError("Uncontrollable Anger reviewed cadence drifted")


def validate_target_role(
    report_entry: dict[str, object],
    enemy_name: str,
    role: str,
    expected: tuple[int, int, int, int],
) -> None:
    evidence = report_entry["targetRoleEvidence"][role]
    actual = (
        evidence["retaliationRows"],
        evidence["attackInfoRows"],
        evidence["minDamage"],
        evidence["maxDamage"],
    )
    if actual != expected:
        raise ValueError(
            "{0} target-role evidence drifted role={1} actual={2}".format(
                enemy_name, role, actual
            )
        )


def proactive_local_acquisition_key(row: dict[str, object]) -> tuple[str, ...]:
    return (
        str(row["capture"]),
        str(row["capturedUtc"]),
        str(row["sourceIdentity"]),
        str(row["targetIdentity"]),
    )


def reviewed_proactive_local_acquisition_evidence(
    enemy_name: str,
    group: dict[str, object],
    local_attack_starts_by_capture: dict[str, list[dict[str, str]]],
) -> list[dict[str, object]]:
    expected = sorted(
        (dict(row) for row in REVIEWED_PROACTIVE_LOCAL_ACQUISITIONS[enemy_name]),
        key=proactive_local_acquisition_key,
    )
    actual = sorted(
        group["reviewedLocalAcquisitionStarts"],
        key=proactive_local_acquisition_key,
    )
    if actual != expected:
        raise ValueError(enemy_name + " proactive local acquisition rows drifted")

    evidence = []
    for row in expected:
        prior_local_attacks = [
            attack
            for attack in local_attack_starts_by_capture.get(row["capture"], ())
            if attack["targetIdentity"] == row["sourceIdentity"]
            and attack["capturedUtc"] < row["capturedUtc"]
        ]
        if prior_local_attacks:
            raise ValueError(
                enemy_name
                + " proactive acquisition claim has a prior local-player attack"
            )
        evidence.append(
            {
                **row,
                "priorLocalAttackAgainstSourceObserved": False,
            }
        )
    return evidence


def validate_proactive_local_acquisition(
    report_entry: dict[str, object], enemy_name: str
) -> None:
    expected = [
        {
            **dict(row),
            "priorLocalAttackAgainstSourceObserved": False,
        }
        for row in sorted(
            REVIEWED_PROACTIVE_LOCAL_ACQUISITIONS[enemy_name],
            key=proactive_local_acquisition_key,
        )
    ]
    if (
        report_entry.get("proactiveLocalAcquisitionEvidence") != expected
        or report_entry.get("automaticAggroRadiusStatus") != "unresolved"
    ):
        raise ValueError(enemy_name + " proactive acquisition evidence drifted")


def validate_infected_attendant_combat(report_entry: dict[str, object]) -> None:
    if (
        report_entry["retaliationRows"] != 2
        or report_entry["normalAttackInfoRows"] != 1
        or report_entry["normalMinDamage"] != 11
        or report_entry["normalMaxDamage"] != 11
        or report_entry["criticalAttackInfoRows"] != 0
        or report_entry["medianRechargeSeconds"] != 0.0
    ):
        raise ValueError("Infected Attendant local-player evidence drifted")
    validate_target_role(report_entry, "Infected Attendant", "localPlayer", (2, 1, 11, 11))
    validate_target_role(report_entry, "Infected Attendant", "playerOwnedPet", (2, 0, 0, 0))
    validate_target_role(report_entry, "Infected Attendant", "otherPlayer", (4, 0, 0, 0))


def validate_lost_thought_combat(report_entry: dict[str, object]) -> None:
    if report_entry["attackInfoRows"] != 0 or report_entry["retaliationRows"] != 0:
        raise ValueError("Lost Thought local-player evidence must remain empty")
    validate_target_role(report_entry, "Lost Thought", "localPlayer", (0, 0, 0, 0))
    validate_target_role(report_entry, "Lost Thought", "playerOwnedPet", (0, 0, 0, 0))
    validate_target_role(report_entry, "Lost Thought", "otherPlayer", (4, 11, 15, 20))
    other = report_entry["targetRoleEvidence"]["otherPlayer"]
    cadence = other.get("landedHitCadence")
    shapes = other["attackShapes"]
    if (
        cadence is None
        or cadence["intervalRows"] != 7
        or cadence["medianIntervalSeconds"] != 4.5320703
        or len(shapes) != 1
        or shapes[0]["weaponSlot"] != 0
        or shapes[0]["attackInfoUnknown"] != 0
        or shapes[0]["hitType"] != "Normal"
        or shapes[0]["weaponInstance"] != 0x53495731
        or shapes[0]["rows"] != 11
    ):
        raise ValueError("Lost Thought other-player SIW1 evidence drifted")


def validate_empty_shell_combat(report_entry: dict[str, object]) -> None:
    if (
        report_entry["retaliationRows"] != 6
        or report_entry["normalAttackInfoRows"] != 1
        or report_entry["normalMinDamage"] != 15
        or report_entry["normalMaxDamage"] != 15
        or report_entry["criticalAttackInfoRows"] != 0
        or report_entry["missedAttackInfoRows"] != 2
    ):
        raise ValueError("Empty Shell local-player evidence drifted")
    validate_proactive_local_acquisition(report_entry, "Empty Shell")
    validate_target_role(report_entry, "Empty Shell", "localPlayer", (6, 1, 15, 15))
    validate_target_role(report_entry, "Empty Shell", "playerOwnedPet", (0, 0, 0, 0))
    validate_target_role(report_entry, "Empty Shell", "otherPlayer", (1, 1, 19, 19))
    nanos = {row["nanoId"]: row for row in report_entry["hostileNanoEvidence"]}
    if set(nanos) != set(REVIEWED_HOSTILE_NANO_NAMES):
        raise ValueError("Empty Shell hostile nano identities drifted")
    expected_nanos = {
        26414: {
            "rows": 4,
            "captures": ["20260709-222339", "20260709-225408"],
            "sourceIdentities": [
                "(SimpleChar:79545178)",
                "(SimpleChar:79545179)",
                "(SimpleChar:79545182)",
            ],
            "localDurationRows": 3,
            "localDurationMilliseconds": [3000],
            "localDurationTargetIdentities": ["(SimpleChar:7944C065)"],
        },
        81998: {
            "rows": 4,
            "captures": ["20260709-222339"],
            "sourceIdentities": [
                "(SimpleChar:79545175)",
                "(SimpleChar:79545178)",
                "(SimpleChar:79545182)",
                "(SimpleChar:79545183)",
            ],
            "localDurationRows": 0,
            "localDurationMilliseconds": [],
            "localDurationTargetIdentities": [],
        },
        82482: {
            "rows": 7,
            "captures": ["20260709-222339", "20260709-225408"],
            "sourceIdentities": [
                "(SimpleChar:79545175)",
                "(SimpleChar:79545183)",
                "(SimpleChar:7954519B)",
            ],
            "localDurationRows": 6,
            "localDurationMilliseconds": [39000],
            "localDurationTargetIdentities": ["(SimpleChar:7944C065)"],
        },
    }
    for nano_id, expected in expected_nanos.items():
        actual = nanos[nano_id]
        for field, value in expected.items():
            if actual[field] != value:
                raise ValueError(
                    "Empty Shell hostile nano evidence drifted nano={0} field={1}".format(
                        nano_id, field
                    )
                )
        if (
            actual["runtimeUsable"]
            or actual["runtimeBlocker"]
            != "effects-selection-cadence-and-range-unresolved"
        ):
            raise ValueError("Empty Shell hostile nano runtime boundary drifted")


def validate_premature_pattern_combat(report_entry: dict[str, object]) -> None:
    if (
        report_entry["retaliationRows"] != 2
        or report_entry["normalAttackInfoRows"] != 1
        or report_entry["normalMinDamage"] != 22
        or report_entry["normalMaxDamage"] != 22
        or report_entry["criticalAttackInfoRows"] != 1
        or report_entry["criticalMinDamage"] != 41
        or report_entry["criticalMaxDamage"] != 41
    ):
        raise ValueError("Premature Pattern local-player evidence drifted")
    validate_proactive_local_acquisition(report_entry, "Premature Pattern")
    validate_target_role(report_entry, "Premature Pattern", "localPlayer", (2, 2, 22, 41))
    validate_target_role(report_entry, "Premature Pattern", "playerOwnedPet", (1, 1, 38, 38))
    validate_target_role(report_entry, "Premature Pattern", "otherPlayer", (2, 1, 16, 16))


def validate_violent_vagabond_combat(report_entry: dict[str, object]) -> None:
    cadence = report_entry.get("reviewedMissAttemptCadence")
    shapes = report_entry["missedAttackShapes"]
    behavior = report_entry["reviewedBehaviorEvidence"]
    if (
        report_entry["retaliationRows"] != 19
        or report_entry["attackInfoRows"] != 0
        or report_entry["missedAttackInfoRows"] != 40
        or report_entry["specialAttackWeaponRows"] != 3
        or len(shapes) != 1
        or shapes[0]["ammoCount"] != 0
        or shapes[0]["weaponSlot"] != 6
        or shapes[0]["unknown"] != 0
        or shapes[0]["rows"] != 40
        or cadence is None
        or cadence["attemptRows"] != 40
        or cadence["intervalRows"] != 25
        or cadence["minIntervalSeconds"] != 3.7795296
        or cadence["medianIntervalSeconds"] != 4.5802404
        or cadence["maxIntervalSeconds"] != 5.0595685
        or report_entry["equippedWeaponTemplateId"] != 130590
        or report_entry["equippedWeaponCombatUsable"]
        or behavior["acquisitionDistanceLowerBound"] != 16.606338
        or behavior["runtimePolicyDelaySeconds"] != 450.0
        or behavior["npcDespawnToReplacementSeconds"] != 449.759588
    ):
        raise ValueError(
            "Violent Vagabond reviewed combat/behavior evidence drifted: "
            + repr(
                {
                    "attackInfoRows": report_entry["attackInfoRows"],
                    "missedAttackInfoRows": report_entry["missedAttackInfoRows"],
                    "missedAttackShapes": shapes,
                    "cadence": cadence,
                    "weaponTemplate": report_entry["equippedWeaponTemplateId"],
                    "weaponCombatUsable": report_entry["equippedWeaponCombatUsable"],
                    "behavior": behavior,
                }
            )
        )
    special_weapon_shapes = report_entry["specialAttackWeaponShapes"]
    if (
        len(special_weapon_shapes) != 1
        or special_weapon_shapes[0]["unknown1"] != 32
        or special_weapon_shapes[0]["unknown2"] != 35
        or special_weapon_shapes[0]["unknown3"] != 29
        or special_weapon_shapes[0]["unknown4"] != 31
        or special_weapon_shapes[0]["unknown5"] != 0
        or special_weapon_shapes[0]["rows"] != 3
        or special_weapon_shapes[0]["captures"] != ["20260719-020104"]
        or special_weapon_shapes[0]["owners"]
        != ["(SimpleChar:797B885C)", "(SimpleChar:797B885D)"]
    ):
        raise ValueError("Violent Vagabond special attack-weapon evidence drifted")
    validate_target_role(
        report_entry, "Violent Vagabond", "otherPlayer", (2, 0, 0, 0)
    )


def validate_strike_foreman_combat(report_entry: dict[str, object]) -> None:
    behavior = report_entry["reviewedBehaviorEvidence"]
    weapon = report_entry["reviewedLifecycleBoundWeaponEvidence"]
    other_player = report_entry["targetRoleEvidence"]["otherPlayer"]
    cadence = other_player.get("landedHitCadence")
    weapon_shapes = {
        (
            shape["lowId"],
            shape["highId"],
            shape["quality"],
            tuple(shape["captures"]),
            tuple(shape["owners"]),
        )
        for shape in report_entry["equippedWeaponShapes"]
    }
    expected_weapon_shapes = {
        (
            122767,
            122768,
            17,
            ("20260709-212336",),
            ("(SimpleChar:79545109)",),
        ),
        (
            122767,
            122768,
            19,
            ("20260709-220439",),
            ("(SimpleChar:7954512E)",),
        ),
    }
    expected_behavior = {
        "automaticAggressionObserved": True,
        "chaseObserved": True,
        "acquisitionDistanceLowerBound": 20.250672,
        "sourceIdentity": "(SimpleChar:7954512E)",
        "targetIdentity": "(SimpleChar:794D8062)",
        "sourcePosition": {"x": 333.5607, "y": 109.015, "z": 206.3987},
        "targetPosition": {"x": 335.2865, "y": 107.015, "z": 186.2217},
        "aggressionCapture": "20260709-222339",
        "radiusStatus": "observed-lower-bound-not-exact-threshold",
        "runtimeStatus": "report-only-dormant",
    }
    expected_lifecycle_bound_weapon = {
        "sourceIdentity": "(SimpleChar:7954512E)",
        "weaponIdentity": "(WeaponInstance:25713A73)",
        "weaponCapture": "20260709-220439",
        "deathCapture": "20260709-222339",
        "corpseIdentity": "(Corpse:00F6E017)",
        "lowId": 122767,
        "highId": 122768,
        "quality": 19,
        "bindingStatus": "exact-source-identity-across-captures",
        "runtimeSelectionStatus": "unresolved-report-only",
    }
    if (
        report_entry["attackInfoRows"] != 0
        or report_entry["equippedWeaponAggregateResolved"]
        or weapon_shapes != expected_weapon_shapes
        or behavior != expected_behavior
        or weapon != expected_lifecycle_bound_weapon
        or cadence is None
        or cadence["intervalRows"] != 2
        or cadence["minIntervalSeconds"] != 4.849144
        or cadence["medianIntervalSeconds"] != 4.9249989
        or cadence["maxIntervalSeconds"] != 5.0008537
    ):
        raise ValueError(
            "Strike Foreman reviewed combat/behavior evidence drifted: "
            + repr(
                {
                    "attackInfoRows": report_entry["attackInfoRows"],
                    "weaponAggregateResolved": report_entry[
                        "equippedWeaponAggregateResolved"
                    ],
                    "weaponShapes": report_entry["equippedWeaponShapes"],
                    "behavior": behavior,
                    "lifecycleBoundWeapon": weapon,
                    "otherPlayerCadence": cadence,
                }
            )
        )
    validate_target_role(report_entry, "Strike Foreman", "localPlayer", (0, 0, 0, 0))
    validate_target_role(
        report_entry, "Strike Foreman", "playerOwnedPet", (0, 0, 0, 0)
    )
    validate_target_role(
        report_entry, "Strike Foreman", "otherPlayer", (1, 3, 18, 40)
    )


def reviewed_raw_attempt_cadence(group: dict[str, object]) -> list[dict[str, object]]:
    starts_by_source = defaultdict(list)
    for row in group["reviewedRawAttackStarts"]:
        starts_by_source[(row["capture"], row["identity"])].append(row["capturedUtc"])
    attempts_by_source = defaultdict(list)
    for row in group["reviewedRawAttempts"]:
        attempts_by_source[(row["capture"], row["identity"])].append(row["capturedUtc"])
    result = []
    for (capture, identity), attempts in sorted(attempts_by_source.items()):
        attempts.sort(key=parse_precise_seconds)
        starts = sorted(
            starts_by_source.get((capture, identity), ()), key=parse_precise_seconds
        )
        first_attempt_seconds = parse_precise_seconds(attempts[0])
        eligible_starts = [
            value
            for value in starts
            if parse_precise_seconds(value) <= first_attempt_seconds
        ]
        attack_start = eligible_starts[-1] if eligible_starts else None
        intervals = [
            parse_precise_seconds(current) - parse_precise_seconds(previous)
            for previous, current in zip(attempts, attempts[1:])
            if parse_precise_seconds(current) > parse_precise_seconds(previous)
        ]
        intervals.sort()
        if not intervals:
            continue
        middle = len(intervals) // 2
        median = (
            intervals[middle]
            if len(intervals) % 2 == 1
            else (intervals[middle - 1] + intervals[middle]) / Decimal(2)
        )
        result.append(
            {
                "capture": capture,
                "identity": identity,
                "attackStartUtc": attack_start if attack_start is not None else "",
                "firstAttemptUtc": attempts[0],
                "initialDelaySeconds": rounded_capture_seconds(
                    first_attempt_seconds - parse_precise_seconds(attack_start)
                )
                if attack_start is not None
                else None,
                "attemptRows": len(attempts),
                "intervalRows": len(intervals),
                "minIntervalSeconds": rounded_capture_seconds(intervals[0]),
                "medianIntervalSeconds": rounded_capture_seconds(median),
                "maxIntervalSeconds": rounded_capture_seconds(intervals[-1]),
            }
        )
    return result


def interval_summary(
    rows: list[dict[str, object]],
    context_fields: tuple[str, ...],
) -> dict[str, object] | None:
    by_context = defaultdict(list)
    for row in rows:
        context = tuple(row.get(field, "") for field in context_fields)
        by_context[context].append(parse_precise_seconds(row["capturedUtc"]))
    intervals = []
    for times in by_context.values():
        times.sort()
        intervals.extend(
            current - previous
            for previous, current in zip(times, times[1:])
            if Decimal("0.5") <= current - previous <= Decimal("10.0")
        )
    intervals.sort()
    if not intervals:
        return None
    middle = len(intervals) // 2
    median = (
        intervals[middle]
        if len(intervals) % 2 == 1
        else (intervals[middle - 1] + intervals[middle]) / Decimal(2)
    )
    return {
        "intervalRows": len(intervals),
        "minIntervalSeconds": precise_capture_seconds(intervals[0]),
        "medianIntervalSeconds": precise_capture_seconds(median),
        "maxIntervalSeconds": precise_capture_seconds(intervals[-1]),
    }


def target_role_attack_shapes(
    attacks: list[dict[str, object]],
) -> list[dict[str, object]]:
    shapes = Counter(
        (
            row["weaponSlot"],
            row["attackInfoUnknown"],
            row["hitType"],
            row["weaponInstance"],
        )
        for row in attacks
    )
    result = []
    for (slot, unknown, hit_type, instance), count in sorted(
        shapes.items(), key=lambda item: (-item[1], item[0])
    ):
        matching = [
            row
            for row in attacks
            if (
                row["weaponSlot"],
                row["attackInfoUnknown"],
                row["hitType"],
                row["weaponInstance"],
            )
            == (slot, unknown, hit_type, instance)
        ]
        result.append(
            {
                "weaponSlot": slot,
                "attackInfoUnknown": unknown,
                "hitType": hit_type,
                "weaponInstance": instance,
                "rows": count,
                "minDamage": min(row["amount"] for row in matching),
                "maxDamage": max(row["amount"] for row in matching),
                "captures": sorted(
                    {
                        capture
                        for row in matching
                        for capture in row["provenanceCaptures"]
                    }
                ),
            }
        )
    return result


def reviewed_miss_attempt_cadence(
    misses: list[dict[str, object]],
) -> dict[str, object] | None:
    summary = interval_summary(
        misses,
        (
            "capture",
            "identity",
            "defender",
            "ammoCount",
            "weaponSlot",
            "unknown",
        ),
    )
    if summary is None:
        return None
    summary.update(
        {
            "attemptRows": len(misses),
            "provenanceCaptures": sorted(
                {
                    capture
                    for row in misses
                    for capture in row["provenanceCaptures"]
                }
            ),
        }
    )
    return summary


def reviewed_violent_vagabond_behavior() -> dict[str, object]:
    aggression_capture = "20260709-225408"
    aggression_lines = (
        CAPTURE_ROOT / aggression_capture / "events.log"
    ).read_text(encoding="utf-8-sig", errors="replace").splitlines()
    required_fragments = (
        "#1747 type=Attack identity=(SimpleChar:7953AD4C) AttackMessage { Target=(SimpleChar:7730002E)",
        "#1749 type=Attack identity=(SimpleChar:7953AD4A) AttackMessage { Target=(SimpleChar:7730002E)",
        "#1751 type=FollowTarget identity=(SimpleChar:7953AD4C) FollowTargetMessage { Type=Target",
        "#1756 type=FollowTarget identity=(SimpleChar:7953AD4A) FollowTargetMessage { Type=Target",
        "#1752 type=SetPos identity=(SimpleChar:7953AD4C) SetPosMessage { Position=(165.705, 107.6164, 167.2476)",
        "#1757 type=SetPos identity=(SimpleChar:7953AD4A) SetPosMessage { Position=(165.0867, 107.6164, 164.3835)",
        "#1760 type=CharDCMove identity=(SimpleChar:7730002E) CharDCMoveMessage { MoveType=Update",
        "Position=(149.1889, 107.6164, 169.1825)",
    )
    for fragment in required_fragments:
        if not any(fragment in line for line in aggression_lines):
            raise ValueError(
                "Violent Vagabond reviewed aggression evidence drifted: " + fragment
            )
    target_x, target_z = 149.1889, 169.1825
    source_positions = ((165.705, 167.2476), (165.0867, 164.3835))
    distances = sorted(
        math.hypot(source_x - target_x, source_z - target_z)
        for source_x, source_z in source_positions
    )

    respawn_capture = "20260708-143600"
    respawn_rows = [
        row
        for row in read_csv(CAPTURE_ROOT / respawn_capture / "enemy-respawns.csv")
        if row.get("Status") == "complete"
        and row.get("DeathIdentity") == "(SimpleChar:794CD74B)"
        and row.get("RespawnIdentity") == "(SimpleChar:794DF301)"
        and row.get("Name") == "Violent Vagabond"
        and row.get("MonsterData") == "203733"
    ]
    if len(respawn_rows) != 1:
        raise ValueError("Violent Vagabond complete respawn generation drifted")
    respawn = respawn_rows[0]
    respawn_events = (
        CAPTURE_ROOT / respawn_capture / "events.log"
    ).read_text(encoding="utf-8-sig", errors="replace").splitlines()
    despawns = [
        match
        for line in respawn_events
        for match in [DESPAWN_EVENT.search(line)]
        if match is not None
        and match.group("identity") == respawn["DeathIdentity"]
    ]
    if len(despawns) != 1:
        raise ValueError("Violent Vagabond dead-NPC despawn evidence drifted")
    after_npc_despawn = parse_precise_seconds(respawn["RespawnUtc"]) - parse_precise_seconds(
        despawns[0].group("captured_utc")
    )
    return {
        "automaticAggressionObserved": True,
        "chaseObserved": True,
        "acquisitionDistanceLowerBound": rounded_capture_seconds(
            Decimal(str(min(distances)))
        ),
        "observedSourceDistances": [
            rounded_capture_seconds(Decimal(str(value))) for value in distances
        ],
        "aggressionCapture": aggression_capture,
        "respawnCapture": respawn_capture,
        "deathIdentity": respawn["DeathIdentity"],
        "replacementIdentity": respawn["RespawnIdentity"],
        "deathToReplacementSeconds": float(respawn["RespawnDelaySeconds"]),
        "npcDespawnToReplacementSeconds": rounded_capture_seconds(
            after_npc_despawn
        ),
        "runtimePolicyDelaySeconds": 450.0,
        "positionDelta": float(respawn["PositionDelta"]),
        "radiusStatus": "observed-lower-bound-not-exact-threshold",
        "respawnStatus": "capture-bounded-450-second-policy",
    }


def reviewed_strike_foreman_evidence() -> tuple[dict[str, object], dict[str, object]]:
    aggression_capture = "20260709-222339"
    source_identity = "(SimpleChar:7954512E)"
    target_identity = "(SimpleChar:794D8062)"
    aggression_lines = (
        CAPTURE_ROOT / aggression_capture / "events.log"
    ).read_text(encoding="utf-8-sig", errors="replace").splitlines()
    target_position_matches = [
        line
        for line in aggression_lines
        if "#4371 type=CharDCMove identity=(SimpleChar:794D8062) CharDCMoveMessage { MoveType=TurnRightStop"
        in line
        and "Position=(335.2865, 107.015, 186.2217)" in line
    ]
    if len(target_position_matches) != 1:
        raise ValueError(
            "Strike Foreman target movement/position evidence drifted"
        )
    required_fragments = (
        "#4421 type=SpecialAttackWeapon identity=(SimpleChar:7954512E)",
        "#4422 type=Attack identity=(SimpleChar:7954512E) AttackMessage { Target=(SimpleChar:794D8062)",
        "#4423 type=SetPos identity=(SimpleChar:7954512E) SetPosMessage { Position=(333.5607, 109.015, 206.3987)",
        "#4424 type=FollowTarget identity=(SimpleChar:7954512E) FollowTargetMessage { Type=NpcPath",
        "#4779 type=CharacterAction identity=(SimpleChar:7954512E) CharacterActionMessage { Action=Death",
    )
    for fragment in required_fragments:
        if not any(fragment in line for line in aggression_lines):
            raise ValueError(
                "Strike Foreman reviewed aggression/death evidence drifted: "
                + fragment
            )

    source_position = (333.5607, 109.015, 206.3987)
    target_position = (335.2865, 107.015, 186.2217)
    acquisition_distance = math.hypot(
        source_position[0] - target_position[0],
        source_position[2] - target_position[2],
    )

    weapon_capture = "20260709-220439"
    weapon_identity = "(WeaponInstance:25713A73)"
    weapon_lines = (
        CAPTURE_ROOT / weapon_capture / "events.log"
    ).read_text(encoding="utf-8-sig", errors="replace").splitlines()
    weapon_matches = [
        line
        for line in weapon_lines
        if "#11443 type=WeaponItemFullUpdate identity=(WeaponInstance:25713A73)"
        in line
        and "Owner=(SimpleChar:7954512E)" in line
        and "StaticInstance=122767" in line
        and "ACGItemLevel=19" in line
        and "ACGItemTemplateID=122767" in line
        and "ACGItemTemplateID2=122768" in line
    ]
    if len(weapon_matches) != 1:
        raise ValueError("Strike Foreman QL19 weapon generation evidence drifted")

    corpse_rows = [
        row
        for row in read_csv(
            CAPTURE_ROOT / aggression_capture / "corpse-full-updates.csv"
        )
        if row.get("CorpseIdentity") == "(Corpse:00F6E017)"
        and row.get("CorpseName") == "Remains of Strike Foreman"
        and row.get("DeadNpcIdentity") == source_identity
        and row.get("CorpseMonsterData") == "203744"
        and row.get("CorpseCatMesh") == "17870"
        and row.get("CorpseCredits") == "176"
    ]
    if len(corpse_rows) != 1:
        raise ValueError("Strike Foreman death-to-corpse identity evidence drifted")

    behavior = {
        "automaticAggressionObserved": True,
        "chaseObserved": True,
        "acquisitionDistanceLowerBound": rounded_capture_seconds(
            Decimal(str(acquisition_distance))
        ),
        "sourceIdentity": source_identity,
        "targetIdentity": target_identity,
        "sourcePosition": {
            "x": source_position[0],
            "y": source_position[1],
            "z": source_position[2],
        },
        "targetPosition": {
            "x": target_position[0],
            "y": target_position[1],
            "z": target_position[2],
        },
        "aggressionCapture": aggression_capture,
        "radiusStatus": "observed-lower-bound-not-exact-threshold",
        "runtimeStatus": "report-only-dormant",
    }
    lifecycle_bound_weapon = {
        "sourceIdentity": source_identity,
        "weaponIdentity": weapon_identity,
        "weaponCapture": weapon_capture,
        "deathCapture": aggression_capture,
        "corpseIdentity": corpse_rows[0]["CorpseIdentity"],
        "lowId": 122767,
        "highId": 122768,
        "quality": 19,
        "bindingStatus": "exact-source-identity-across-captures",
        "runtimeSelectionStatus": "unresolved-report-only",
    }
    return behavior, lifecycle_bound_weapon


def add_reviewed_raw_target_role_evidence(
    capture_name: str,
    folder: Path,
    identities: dict[str, dict[str, object]],
    grouped,
    derived_event_keys: set[tuple[str, str, str, str]],
) -> None:
    reviewed = REVIEWED_RAW_TARGET_ROLE_PACKETS.get(capture_name, ())
    packets_path = folder / "packets.hex.log"
    if not reviewed or not packets_path.exists():
        return
    raw_lines = set(
        packets_path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
    )
    for packet in reviewed:
        source = packet["source"]
        enemy = identities.get(source)
        expected_name = packet.get("enemyName", "Strike Foreman")
        expected_monster_data = int(packet.get("monsterData", 203744))
        if (
            not enemy
            or enemy["name"] != expected_name
            or enemy["monsterData"] != expected_monster_data
            or (
                source,
                packet["messageType"],
                packet["target"],
                packet["capturedUtc"],
            )
            in derived_event_keys
        ):
            continue
        expected_line = (
            f"{packet['capturedUtc']} IN #{packet['sequence']} "
            f"len={packet['length']} n3={packet['messageType']} "
            f"hex={packet['rawHex']}"
        )
        if expected_line not in raw_lines:
            continue
        group = grouped[enemy["name"]]
        group["identities"].add(source)
        group["captures"].add(capture_name)
        group["monsterData"].add(enemy["monsterData"])
        role_evidence = group["targetRoleEvidence"][packet["targetRole"]]
        role_evidence["captures"].add(capture_name)
        role_evidence["targetIdentities"].add(packet["target"])
        if packet["messageType"] == "Attack":
            role_evidence["retaliationRows"] += 1
            if packet["targetRole"] == "localPlayer":
                group["retaliationRows"] += 1
                group["reviewedRawAttackStarts"].append(
                    {
                        "capture": capture_name,
                        "identity": source,
                        "capturedUtc": packet["capturedUtc"],
                    }
                )
            continue
        if packet["messageType"] == "MissedAttackInfo":
            if packet["targetRole"] == "localPlayer":
                group["misses"].append(
                    {
                        "capture": capture_name,
                        "identity": source,
                        "capturedUtc": packet["capturedUtc"],
                        "ammoCount": packet["ammoCount"],
                        "weaponSlot": packet["weaponSlot"],
                        "unknown": packet["unknown"],
                    }
                )
                group["reviewedRawAttempts"].append(
                    {
                        "capture": capture_name,
                        "identity": source,
                        "capturedUtc": packet["capturedUtc"],
                        "messageType": packet["messageType"],
                    }
                )
            continue
        parsed_attack = {
            "capture": capture_name,
            "identity": source,
            "targetIdentity": packet["target"],
            "capturedUtc": packet["capturedUtc"],
            "amount": packet["amount"],
            "weaponSlot": packet["weaponSlot"],
            "attackInfoUnknown": packet["attackInfoUnknown"],
            "hitType": packet["hitType"],
            "weaponInstance": packet["weaponInstance"],
            "provenanceCaptures": {capture_name},
        }
        role_evidence["attacks"].append(parsed_attack)
        if packet["targetRole"] == "localPlayer":
            group["attacks"].append(parsed_attack)
            group["reviewedRawAttempts"].append(
                {
                    "capture": capture_name,
                    "identity": source,
                    "capturedUtc": packet["capturedUtc"],
                    "messageType": packet["messageType"],
                }
            )


def main():
    grouped = defaultdict(
        lambda: {
            "identities": set(),
            "captures": set(),
            "retaliationRows": 0,
            "attacks": [],
            "misses": [],
            "weapons": [],
            "weaponKeys": set(),
            "specialAttackWeapons": [],
            "reviewedRawAttackStarts": [],
            "reviewedRawAttempts": [],
            "reviewedLocalAcquisitionStarts": [],
            "hostileNanos": [],
            "monsterData": set(),
            "targetRoleEvidence": defaultdict(
                lambda: {
                    "captures": set(),
                    "targetIdentities": set(),
                    "retaliationRows": 0,
                    "attacks": [],
                }
            ),
        }
    )
    combat_deduplicator = OverlappingCombatEventDeduplicator()
    local_attack_starts_by_capture = defaultdict(list)

    for capture_name in CAPTURES:
        folder = CAPTURE_ROOT / capture_name
        events_path = folder / "events.log"
        identities = {}
        for row in read_csv(folder / "enemy-full-updates.csv"):
            add_identity(identities, row)
        for row in read_csv(folder / "npc-lifecycle.csv"):
            add_identity(identities, row)
        for row in read_json(folder / "enemy-dossier.json").get("enemies", []):
            add_identity(identities, row)
        for row in read_csv(folder / "corpse-full-updates.csv"):
            corpse_name = first_value(row, "DeadNpcName", "CorpseName")
            if corpse_name.startswith("Remains of "):
                corpse_name = corpse_name[len("Remains of ") :]
            add_identity(
                identities,
                {
                    "Identity": first_value(
                        row, "DeadNpcIdentity", "TailDeadNpcIdentity"
                    ),
                    "Name": corpse_name,
                    "MonsterData": first_value(row, "CorpseMonsterData"),
                },
            )
        add_reviewed_event_identities(capture_name, events_path, identities)

        derived_event_keys = set()
        combat_rows = read_csv(folder / "enemy-combat.csv")
        local_player_identities = set()
        for row in combat_rows:
            detail = row.get("Detail", "")
            if row.get("SourceRole") == "local-player":
                identity_match = MESSAGE_IDENTITY_DETAIL.search(detail)
                if identity_match:
                    local_player_identities.add(identity_match.group("identity"))
                if row.get("MessageType") == "Attack":
                    local_attack_target = row.get("TargetIdentity", "")
                    if not local_attack_target:
                        target_match = ATTACK_TARGET_DETAIL.search(detail)
                        local_attack_target = (
                            target_match.group("target") if target_match else ""
                        )
                    if local_attack_target:
                        local_attack_starts_by_capture[capture_name].append(
                            {
                                "targetIdentity": local_attack_target,
                                "capturedUtc": row.get("CapturedUtc", ""),
                            }
                        )
            if row.get("TargetRole") == "local-player":
                target_match = ATTACK_TARGET_DETAIL.search(detail)
                if target_match:
                    local_player_identities.add(target_match.group("target"))

        for row in combat_rows:
            source = row.get("SourceIdentity", "")
            enemy = identities.get(source)
            if (
                not enemy
                or row.get("SourceRole") != "enemy"
                or not capture_includes_enemy(capture_name, enemy["name"])
            ):
                continue
            message_type = row.get("MessageType")
            derived_target = row.get("TargetIdentity", "")
            if not derived_target:
                target_match = ATTACK_TARGET_DETAIL.search(row.get("Detail", ""))
                derived_target = target_match.group("target") if target_match else ""
            derived_event_keys.add(
                (
                    source,
                    message_type,
                    derived_target,
                    row.get("CapturedUtc", ""),
                )
            )
            group = grouped[enemy["name"]]
            group["identities"].add(source)
            group["captures"].add(capture_name)
            group["monsterData"].add(enemy["monsterData"])
            if message_type == "SpecialAttackWeapon":
                group["specialAttackWeapons"].append(
                    {
                        "capture": capture_name,
                        "identity": source,
                        "capturedUtc": row.get("CapturedUtc", ""),
                        "unknown1": int(row.get("Unknown1") or 0),
                        "unknown2": int(row.get("Unknown2") or 0),
                        "unknown3": int(row.get("Unknown3") or 0),
                        "unknown4": int(row.get("Unknown4") or 0),
                        "unknown5": int(row.get("Unknown5") or 0),
                    }
                )
            reviewed_attack_sources = REVIEWED_ATTACK_INFO_SOURCES.get(
                (enemy["name"], capture_name), frozenset()
            )
            attack_info_allowed = (
                message_type != "AttackInfo"
                or capture_name
                in ENEMY_ATTACK_CAPTURE_FILTERS.get(
                    enemy["name"], frozenset({capture_name})
                )
                or source in reviewed_attack_sources
            )
            evidence_role = ""
            role_evidence = None
            if (
                enemy["name"] in TARGET_ROLE_EVIDENCE_ENEMIES
                and message_type in {"Attack", "AttackInfo"}
            ):
                evidence_role = target_evidence_role(
                    capture_name, row, enemy["name"]
                )
                if evidence_role:
                    role_evidence = group["targetRoleEvidence"][evidence_role]
                    role_evidence["captures"].add(capture_name)
                    target_identity = attack_target_identity(row)
                    if target_identity:
                        role_evidence["targetIdentities"].add(target_identity)
            combat_event = None
            duplicate_event = False
            if message_type == "Attack" or (
                message_type == "AttackInfo" and attack_info_allowed
            ):
                combat_event, duplicate_event = combat_deduplicator.observe(
                    capture_name, row, evidence_role
                )
            if duplicate_event:
                continue
            provenance_captures = (
                combat_event["provenanceCaptures"]
                if combat_event is not None
                else {capture_name}
            )
            parsed_attack = (
                attack_evidence(
                    row,
                    capture_name,
                    source,
                    provenance_captures,
                )
                if message_type == "AttackInfo" and attack_info_allowed
                else None
            )
            if role_evidence is not None:
                if message_type == "Attack":
                    role_evidence["retaliationRows"] += 1
                    if evidence_role == "localPlayer":
                        group["reviewedLocalAcquisitionStarts"].append(
                            {
                                "capture": capture_name,
                                "sourceIdentity": source,
                                "targetIdentity": derived_target,
                                "capturedUtc": row.get("CapturedUtc", ""),
                            }
                        )
                elif parsed_attack is not None:
                    role_evidence["attacks"].append(parsed_attack)
            if (
                message_type in {"Attack", "AttackInfo"}
                and row.get("TargetRole") != "local-player"
            ):
                continue
            if message_type == "Attack":
                group["retaliationRows"] += 1
            if message_type != "AttackInfo":
                continue
            if parsed_attack is None:
                continue
            group["attacks"].append(parsed_attack)

        add_reviewed_raw_target_role_evidence(
            capture_name,
            folder,
            identities,
            grouped,
            derived_event_keys,
        )
        if events_path.exists():
            event_lines = events_path.read_text(
                encoding="utf-8-sig", errors="replace"
            ).splitlines()
            local_durations_by_timestamp_and_nano = defaultdict(set)
            for line in event_lines:
                duration = SET_NANO_DURATION.search(line)
                if (
                    duration is None
                    or duration.group("target") not in local_player_identities
                ):
                    continue
                local_durations_by_timestamp_and_nano[
                    (
                        duration.group("captured_utc"),
                        int(duration.group("nano_id"), 16),
                    )
                ].add(
                    (
                        duration.group("target"),
                        int(duration.group("duration_ms")),
                    )
                )
            for line in event_lines:
                nano_cast = FINISH_NANO_CASTING.search(line)
                if nano_cast is not None:
                    enemy = identities.get(nano_cast.group("source"))
                    nano_id = int(nano_cast.group("nano_id"))
                    if (
                        enemy
                        and enemy["name"] == "Empty Shell"
                        and nano_id in REVIEWED_HOSTILE_NANO_NAMES
                        and capture_includes_enemy(capture_name, enemy["name"])
                    ):
                        group = grouped[enemy["name"]]
                        group["identities"].add(nano_cast.group("source"))
                        group["captures"].add(capture_name)
                        group["monsterData"].add(enemy["monsterData"])
                        group["hostileNanos"].append(
                            {
                                "capture": capture_name,
                                "identity": nano_cast.group("source"),
                                "capturedUtc": nano_cast.group("captured_utc"),
                                "nanoId": nano_id,
                                "localDurationMilliseconds": sorted(
                                    {
                                        duration_ms
                                        for _, duration_ms in local_durations_by_timestamp_and_nano.get(
                                            (
                                                nano_cast.group("captured_utc"),
                                                nano_id,
                                            ),
                                            (),
                                        )
                                    }
                                ),
                                "localDurationTargetIdentities": sorted(
                                    {
                                        target_identity
                                        for target_identity, _ in local_durations_by_timestamp_and_nano.get(
                                            (
                                                nano_cast.group("captured_utc"),
                                                nano_id,
                                            ),
                                            (),
                                        )
                                    }
                                ),
                                "localDurationRows": len(
                                    local_durations_by_timestamp_and_nano.get(
                                        (
                                            nano_cast.group("captured_utc"),
                                            nano_id,
                                        ),
                                        (),
                                    )
                                ),
                            }
                        )
                match = WEAPON_UPDATE.search(line)
                if match:
                    enemy = identities.get(match.group("owner"))
                    if enemy and capture_includes_enemy(capture_name, enemy["name"]):
                        group = grouped[enemy["name"]]
                        weapon_key = (
                            capture_name,
                            match.group("owner"),
                            match.group("weapon"),
                            int(match.group("template")),
                            int(match.group("template2")),
                            int(match.group("quality")),
                        )
                        if weapon_key not in group["weaponKeys"]:
                            group["weaponKeys"].add(weapon_key)
                            group["identities"].add(match.group("owner"))
                            group["captures"].add(capture_name)
                            group["monsterData"].add(enemy["monsterData"])
                            group["weapons"].append(
                                {
                                    "capture": capture_name,
                                    "owner": match.group("owner"),
                                    "weaponIdentity": match.group("weapon"),
                                    "templateId": int(match.group("template")),
                                    "templateId2": int(match.group("template2")),
                                    "quality": int(match.group("quality")),
                                }
                            )

                miss = MISSED_ATTACK_INFO.search(line)
                if not miss or miss.group("defender") not in local_player_identities:
                    continue
                enemy = identities.get(miss.group("attacker"))
                if not enemy or not capture_includes_enemy(capture_name, enemy["name"]):
                    continue
                group = grouped[enemy["name"]]
                group["identities"].add(miss.group("attacker"))
                group["captures"].add(capture_name)
                group["monsterData"].add(enemy["monsterData"])
                miss_event, duplicate_miss = combat_deduplicator.observe(
                    capture_name,
                    {
                        "CapturedUtc": miss.group("captured_utc"),
                        "MessageType": "MissedAttackInfo",
                        "SourceIdentity": miss.group("attacker"),
                        "TargetIdentity": miss.group("defender"),
                        "TargetRole": "local-player",
                        "AmmoCount": miss.group("ammo"),
                        "WeaponSlot": miss.group("slot"),
                        "Unknown": miss.group("unknown"),
                    },
                    "localPlayer",
                )
                if duplicate_miss:
                    continue
                group["misses"].append(
                    {
                        "capture": capture_name,
                        "identity": miss.group("attacker"),
                        "defender": miss.group("defender"),
                        "capturedUtc": miss.group("captured_utc"),
                        "ammoCount": int(miss.group("ammo")),
                        "weaponSlot": int(miss.group("slot")),
                        "unknown": int(miss.group("unknown")),
                        "provenanceCaptures": miss_event["provenanceCaptures"],
                    }
                )

    combat_deduplicator.validate()
    report = {}
    for name, group in sorted(grouped.items()):
        attacks = group["attacks"]
        normal_attacks = [row for row in attacks if row["hitType"] == "Normal"]
        critical_attacks = [row for row in attacks if row["hitType"] == "Critical"]
        shape_attacks = normal_attacks if name == "Filth Flea" else attacks
        intervals = []
        by_identity_shape = defaultdict(list)
        if name not in CADENCE_UNRESOLVED_ENEMIES:
            for attack in shape_attacks:
                by_identity_shape[
                    (
                        attack["capture"],
                        attack["identity"],
                        attack["weaponSlot"],
                        attack["attackInfoUnknown"],
                        attack["weaponInstance"],
                    )
                ].append(parse_time(attack["capturedUtc"]))
            for times in by_identity_shape.values():
                times.sort()
                for previous, current in zip(times, times[1:]):
                    seconds = (current - previous).total_seconds()
                    if 0.5 <= seconds <= 10.0:
                        intervals.append(seconds)
        intervals.sort()
        attack_shapes = Counter(
            (row["weaponSlot"], row["attackInfoUnknown"], row["weaponInstance"])
            for row in shape_attacks
        )
        critical_shapes = Counter(
            (row["weaponSlot"], row["attackInfoUnknown"], row["weaponInstance"])
            for row in critical_attacks
        )
        weapon_shapes = Counter(
            (row["templateId"], row["templateId2"], row["quality"])
            for row in group["weapons"]
        )
        weapon_shape_evidence = []
        for (low_id, high_id, weapon_quality), rows in sorted(
            weapon_shapes.items(),
            key=lambda item: (-item[1], item[0]),
        ):
            matching = [
                row
                for row in group["weapons"]
                if (
                    row["templateId"],
                    row["templateId2"],
                    row["quality"],
                )
                == (low_id, high_id, weapon_quality)
            ]
            weapon_shape_evidence.append(
                {
                    "lowId": low_id,
                    "highId": high_id,
                    "quality": weapon_quality,
                    "rows": rows,
                    "captures": sorted({row["capture"] for row in matching}),
                    "owners": sorted({row["owner"] for row in matching}),
                }
            )
        attack_shape_evidence = []
        for (shape_slot, shape_unknown, shape_instance), rows in sorted(
            attack_shapes.items(),
            key=lambda item: (-item[1], item[0]),
        ):
            matching = [
                row
                for row in shape_attacks
                if (
                    row["weaponSlot"],
                    row["attackInfoUnknown"],
                    row["weaponInstance"],
                )
                == (shape_slot, shape_unknown, shape_instance)
            ]
            shape_intervals = []
            if name not in CADENCE_UNRESOLVED_ENEMIES:
                matching_by_identity = defaultdict(list)
                for row in matching:
                    matching_by_identity[(row["capture"], row["identity"])].append(
                        parse_time(row["capturedUtc"])
                    )
                for times in matching_by_identity.values():
                    times.sort()
                    for previous, current in zip(times, times[1:]):
                        seconds = (current - previous).total_seconds()
                        if 0.5 <= seconds <= 10.0:
                            shape_intervals.append(seconds)
            shape_intervals.sort()
            attack_shape_evidence.append(
                {
                    "weaponSlot": shape_slot,
                    "attackInfoUnknown": shape_unknown,
                    "weaponInstance": shape_instance,
                    "rows": rows,
                    "captures": sorted(
                        {
                            capture
                            for row in matching
                            for capture in row["provenanceCaptures"]
                        }
                    ),
                    "minDamage": min(row["amount"] for row in matching),
                    "maxDamage": max(row["amount"] for row in matching),
                    "intervalRows": len(shape_intervals),
                    "minIntervalSeconds": min(shape_intervals) if shape_intervals else None,
                    "medianIntervalSeconds": (
                        shape_intervals[(len(shape_intervals) - 1) // 2]
                        if shape_intervals
                        else None
                    ),
                    "maxIntervalSeconds": max(shape_intervals) if shape_intervals else None,
                }
            )
        slot, unknown, instance = attack_shapes.most_common(1)[0][0] if attack_shapes else (0, 0, 0)
        critical_shape_evidence = []
        for (shape_slot, shape_unknown, shape_instance), rows in sorted(
            critical_shapes.items(),
            key=lambda item: (-item[1], item[0]),
        ):
            matching = [
                row
                for row in critical_attacks
                if (
                    row["weaponSlot"],
                    row["attackInfoUnknown"],
                    row["weaponInstance"],
                )
                == (shape_slot, shape_unknown, shape_instance)
            ]
            critical_shape_evidence.append(
                {
                    "weaponSlot": shape_slot,
                    "attackInfoUnknown": shape_unknown,
                    "weaponInstance": shape_instance,
                    "rows": rows,
                    "captures": sorted(
                        {
                            capture
                            for row in matching
                            for capture in row["provenanceCaptures"]
                        }
                    ),
                    "minDamage": min(row["amount"] for row in matching),
                    "maxDamage": max(row["amount"] for row in matching),
                }
            )
        template_id, template_id2, quality = (
            next(iter(weapon_shapes)) if len(weapon_shapes) == 1 else (0, 0, 0)
        )
        special_attack_weapon_shapes = Counter(
            (
                row["unknown1"],
                row["unknown2"],
                row["unknown3"],
                row["unknown4"],
                row["unknown5"],
            )
            for row in group["specialAttackWeapons"]
        )
        raw_attempt_cadence = reviewed_raw_attempt_cadence(group)
        miss_attempt_cadence = (
            reviewed_miss_attempt_cadence(group["misses"])
            if name == "Violent Vagabond"
            else None
        )
        hostile_nano_evidence = []
        hostile_nano_counts = Counter(
            row["nanoId"] for row in group["hostileNanos"]
        )
        for nano_id, rows in sorted(hostile_nano_counts.items()):
            matching_nanos = [
                row for row in group["hostileNanos"] if row["nanoId"] == nano_id
            ]
            hostile_nano_evidence.append(
                {
                    "nanoId": nano_id,
                    "name": REVIEWED_HOSTILE_NANO_NAMES[nano_id],
                    "rows": rows,
                    "captures": sorted(
                        {row["capture"] for row in matching_nanos}
                    ),
                    "sourceIdentities": sorted(
                        {row["identity"] for row in matching_nanos}
                    ),
                    "localDurationMilliseconds": sorted(
                        {
                            duration
                            for row in matching_nanos
                            for duration in row["localDurationMilliseconds"]
                        }
                    ),
                    "localDurationTargetIdentities": sorted(
                        {
                            target_identity
                            for row in matching_nanos
                            for target_identity in row[
                                "localDurationTargetIdentities"
                            ]
                        }
                    ),
                    "localDurationRows": sum(
                        row["localDurationRows"] for row in matching_nanos
                    ),
                    "runtimeUsable": False,
                    "runtimeBlocker": "effects-selection-cadence-and-range-unresolved",
                }
            )
        reviewed_target_cadence = (
            reviewed_uncontrollable_anger_cadence(group)
            if name == "Uncontrollable Anger"
            else None
        )
        median_recharge_seconds = (
            intervals[(len(intervals) - 1) // 2] if intervals else 0.0
        )
        if reviewed_target_cadence is not None:
            median_recharge_seconds = reviewed_target_cadence[
                "runtimeRechargeSeconds"
            ]
        report_entry = {
            "monsterData": sorted(group["monsterData"]),
            "captures": sorted(group["captures"]),
            "identities": sorted(group["identities"]),
            "retaliationObserved": group["retaliationRows"] > 0,
            "retaliationRows": group["retaliationRows"],
            "attackInfoObserved": bool(attacks),
            "attackInfoRows": len(attacks),
            "minDamage": min((row["amount"] for row in attacks), default=0),
            "maxDamage": max((row["amount"] for row in attacks), default=0),
            "normalAttackInfoRows": len(normal_attacks),
            "normalMinDamage": min((row["amount"] for row in normal_attacks), default=0),
            "normalMaxDamage": max((row["amount"] for row in normal_attacks), default=0),
            "criticalAttackInfoRows": len(critical_attacks),
            "criticalMinDamage": min((row["amount"] for row in critical_attacks), default=0),
            "criticalMaxDamage": max((row["amount"] for row in critical_attacks), default=0),
            "missedAttackInfoRows": len(group["misses"]),
            "missedAttackShapes": [
                {
                    "ammoCount": ammo_count,
                    "weaponSlot": miss_slot,
                    "unknown": miss_unknown,
                    "rows": rows,
                    "captures": sorted(
                        {
                            capture
                            for row in group["misses"]
                            for capture in row.get(
                                "provenanceCaptures", {row["capture"]}
                            )
                            if (
                                row["ammoCount"],
                                row["weaponSlot"],
                                row["unknown"],
                            )
                            == (ammo_count, miss_slot, miss_unknown)
                        }
                    ),
                }
                for (ammo_count, miss_slot, miss_unknown), rows in sorted(
                    Counter(
                        (
                            row["ammoCount"],
                            row["weaponSlot"],
                            row["unknown"],
                        )
                        for row in group["misses"]
                    ).items(),
                    key=lambda item: (-item[1], item[0]),
                )
            ],
            "specialAttackWeaponRows": len(group["specialAttackWeapons"]),
            "specialAttackWeaponShapes": [
                {
                    "unknown1": unknown1,
                    "unknown2": unknown2,
                    "unknown3": unknown3,
                    "unknown4": unknown4,
                    "unknown5": unknown5,
                    "rows": rows,
                    "captures": sorted(
                        {
                            row["capture"]
                            for row in group["specialAttackWeapons"]
                            if (
                                row["unknown1"],
                                row["unknown2"],
                                row["unknown3"],
                                row["unknown4"],
                                row["unknown5"],
                            )
                            == (unknown1, unknown2, unknown3, unknown4, unknown5)
                        }
                    ),
                    "owners": sorted(
                        {
                            row["identity"]
                            for row in group["specialAttackWeapons"]
                            if (
                                row["unknown1"],
                                row["unknown2"],
                                row["unknown3"],
                                row["unknown4"],
                                row["unknown5"],
                            )
                            == (unknown1, unknown2, unknown3, unknown4, unknown5)
                        }
                    ),
                }
                for (unknown1, unknown2, unknown3, unknown4, unknown5), rows in sorted(
                    special_attack_weapon_shapes.items(),
                    key=lambda item: (-item[1], item[0]),
                )
            ],
            "medianRechargeSeconds": median_recharge_seconds,
            "weaponSlot": slot,
            "attackInfoUnknown": unknown,
            "attackInfoWeaponInstance": instance,
            "attackShapes": attack_shape_evidence,
        }
        if raw_attempt_cadence:
            report_entry["reviewedRawAttemptCadence"] = raw_attempt_cadence
        if miss_attempt_cadence:
            report_entry["reviewedMissAttemptCadence"] = miss_attempt_cadence
        if reviewed_target_cadence is not None:
            report_entry["reviewedTargetCadence"] = reviewed_target_cadence
        if hostile_nano_evidence:
            report_entry["hostileNanoEvidence"] = hostile_nano_evidence
        if name in REVIEWED_PROACTIVE_LOCAL_ACQUISITIONS:
            report_entry["proactiveLocalAcquisitionEvidence"] = (
                reviewed_proactive_local_acquisition_evidence(
                    name,
                    group,
                    local_attack_starts_by_capture,
                )
            )
            report_entry["automaticAggroRadiusStatus"] = "unresolved"
        if name == "Filth Flea":
            report_entry["criticalAttackShapes"] = critical_shape_evidence
        if name in TARGET_ROLE_EVIDENCE_ENEMIES:
            target_role_evidence = {}
            evidence_roles = ["localPlayer", "playerOwnedPet"]
            if group["targetRoleEvidence"]["otherPlayer"]["captures"]:
                evidence_roles.append("otherPlayer")
            for evidence_role in evidence_roles:
                role_evidence = group["targetRoleEvidence"][evidence_role]
                role_attacks = role_evidence["attacks"]
                role_entry = {
                    "captures": sorted(role_evidence["captures"]),
                    "targetIdentities": sorted(role_evidence["targetIdentities"]),
                    "retaliationRows": role_evidence["retaliationRows"],
                    "attackInfoRows": len(role_attacks),
                    "minDamage": min(
                        (row["amount"] for row in role_attacks), default=0
                    ),
                    "maxDamage": max(
                        (row["amount"] for row in role_attacks), default=0
                    ),
                    "attackShapes": target_role_attack_shapes(role_attacks),
                }
                landed_cadence = interval_summary(
                    role_attacks,
                    (
                        "capture",
                        "identity",
                        "targetIdentity",
                        "weaponSlot",
                        "attackInfoUnknown",
                        "weaponInstance",
                    ),
                )
                if landed_cadence is not None:
                    role_entry["landedHitCadence"] = landed_cadence
                target_role_evidence[evidence_role] = role_entry
            report_entry["targetRoleEvidence"] = target_role_evidence
        if name in CADENCE_UNRESOLVED_ENEMIES:
            report_entry["cadenceStatus"] = "unresolved-mixed-target-fight"
        report_entry.update(
            {
                "equippedWeaponObserved": bool(weapon_shapes),
                "equippedWeaponAggregateResolved": len(weapon_shapes) == 1,
                "equippedWeaponTemplateId": template_id,
                "equippedWeaponHighTemplateId": template_id2,
                "equippedWeaponQuality": quality,
                "equippedWeaponShapes": weapon_shape_evidence,
            }
        )
        if name == "Violent Vagabond":
            report_entry["equippedWeaponCombatUsable"] = False
            report_entry["equippedWeaponCombatBlocker"] = (
                "template 130590 is Red Wine and proves held-item identity only"
            )
            report_entry["reviewedBehaviorEvidence"] = (
                reviewed_violent_vagabond_behavior()
            )
        if name == "Strike Foreman":
            behavior, lifecycle_bound_weapon = reviewed_strike_foreman_evidence()
            report_entry["reviewedBehaviorEvidence"] = behavior
            report_entry["reviewedLifecycleBoundWeaponEvidence"] = (
                lifecycle_bound_weapon
            )
        if name == "Workman Striker":
            validate_workman_striker_distinct_combat(attacks, report_entry)
        if name == "Discarded Pet":
            validate_discarded_pet_combat(report_entry)
        if name == "Disobedient Bot":
            validate_disobedient_bot_combat(report_entry)
        if name == "Mugger":
            validate_mugger_combat(report_entry)
        if name == "Uncontrollable Anger":
            validate_uncontrollable_anger_combat(report_entry)
        if name == "Infected Attendant":
            validate_infected_attendant_combat(report_entry)
        if name == "Lost Thought":
            validate_lost_thought_combat(report_entry)
        if name == "Empty Shell":
            validate_empty_shell_combat(report_entry)
        if name == "Premature Pattern":
            validate_premature_pattern_combat(report_entry)
        if name == "Strike Foreman":
            validate_strike_foreman_combat(report_entry)
        if name == "Violent Vagabond":
            validate_violent_vagabond_combat(report_entry)
        report[name] = report_entry

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"archetypes={len(report)} output={OUTPUT}")


if __name__ == "__main__":
    main()
