from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import os
import re
import tempfile
from collections import Counter, defaultdict
from datetime import datetime
from decimal import Decimal
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
CAPTURE_ROOT = REPO / "tools-temp" / "AOSharpLiveCapture" / "bin" / "Debug" / "captures"
CAPTURES = (
    "20260709-205921",
    "20260709-210452",
    "20260709-212115",
    "20260709-212336",
    "20260709-213711",
    "20260709-220439",
    "20260709-222339",
    "20260709-225408",
    "20260710-202132",
    "20260710-211430",
    "20260716-033326",
    "20260716-034104",
    "20260716-034559",
    "20260716-034656",
    "20260716-221358",
    "20260716-222201",
)
SPAWN_CAPTURES = (
    "20260709-212336",
    "20260709-222339",
    "20260709-225408",
    "20260710-202132",
)
CAPTURE_ARCHETYPE_FILTERS = {
    "20260709-213711": frozenset(("Architect Striker", "Workman Striker")),
    "20260710-202132": frozenset(("Looter", "Stim Fiend", "Deranged Shopper")),
    "20260716-034559": frozenset(("Melded Patterns",)),
    "20260716-034656": frozenset(("Slum Runner",)),
    "20260716-221358": frozenset(("Molested Molecules", "Neural Burnout")),
    "20260716-222201": frozenset(
        (
            "Fragmented Soul",
            "Incomplete Rebuild",
            "Melded Patterns",
            "Molested Molecules",
            "Neural Burnout",
            "Redundant Scan",
            "Slum Runner",
        )
    ),
}
CAPTURE_IDENTITY_NAME_OVERRIDES = {
    # This combat-only capture has no matching SCFU/name row.  Its two slot-6
    # sources are the observed Workman Strikers and its slot-0 source is the
    # observed Architect Striker; the generated combat contract corpus carries
    # the same identity ownership.
    "20260709-213711": {
        "(SimpleChar:7953AFBC)": "Workman Striker",
        "(SimpleChar:7953AFDD)": "Workman Striker",
        "(SimpleChar:7953AFDA)": "Architect Striker",
    }
}

# These directories overlap one running official client.  The first tuple
# member is retained as canonical provenance.  A 20 ms bound covers the
# audited maximum 16.871 ms logger skew in 212336 and the 17 ms Workman
# critical skew in 213711.  Every other capture pair remains ineligible.
OVERLAPPING_COMBAT_CAPTURE_RULES = {
    ("20260709-212115", "20260709-212336"): 0.020,
    ("20260709-212115", "20260709-213711"): 0.020,
}
COMBAT_ATTACK_DETAIL = re.compile(
    r"WeaponSlot=(?P<slot>-?\d+).*Unk1=(?P<unknown>-?\d+).*"
    r"HitType=(?P<hit_type>\w+).*WeaponInstance=(?P<instance>-?\d+)"
)
COMBAT_TARGET_DETAIL = re.compile(
    r"\bTarget=(?P<target>\(SimpleChar:[0-9A-F]+\))"
)
WEAPON_OWNER_DETAIL = re.compile(
    r"\bOwner=\(SimpleChar:(?P<owner>[0-9A-F]+)\)"
)
WEAPON_QUALITY_DETAIL = re.compile(r"\bACGItemLevel=(?P<quality>\d+)")
WEAPON_LOW_TEMPLATE_DETAIL = re.compile(
    r"\bACGItemTemplateID=(?P<low>\d+)"
)
WEAPON_HIGH_TEMPLATE_DETAIL = re.compile(
    r"\bACGItemTemplateID2=(?P<high>\d+)"
)
RAW_INVENTORY_UPDATE_LINE = re.compile(
    r"^(?P<captured_utc>\S+) IN #(?P<sequence>\d+) len=(?P<length>\d+) "
    r"n3=InventoryUpdate hex=(?P<hex>[0-9A-F]+)$"
)

# The two capture directories overlap one running AO process and project the
# same first two item-bearing Workman corpse generations twice.  Keep the
# later, complete directory as the canonical legacy-outcome source.
WORKMAN_STRIKER_DUPLICATE_LOOT_CAPTURE = "20260709-212115"
WORKMAN_STRIKER_CANONICAL_LOOT_CAPTURE = "20260709-212336"
WORKMAN_STRIKER_DUPLICATE_LOOT_ROWS = 4

# Source-specific equipped enemies below have one exact owner-linked
# WeaponItemFullUpdate tuple per reviewed runtime row.  Keep the expected review
# boundary explicit so a missing, duplicate-conflicting, or silently changed
# tuple stops generation instead of collapsing the family to one representative
# QL.
EXPECTED_SOURCE_WEAPON_EVIDENCE = {
    "Deranged Shopper": {
        0x79574527: (125454, 125455, 8),
    },
    "Incomplete Rebuild": {
        0x79545170: (122653, 122654, 18),
        0x79545172: (122653, 122654, 14),
        0x79545177: (122653, 122654, 18),
        0x79545181: (122654, 122654, 20),
        0x79545188: (122653, 122654, 17),
        0x795451BC: (122653, 122654, 18),
        0x795451C1: (122655, 122655, 21),
        0x795451CB: (122655, 122656, 24),
        0x795451FD: (122654, 122654, 20),
        0x79545241: (122654, 122654, 20),
    },
    "Looter": {
        0x795312DC: (123038, 123039, 12),
        0x795313CB: (123038, 123039, 9),
        0x7954501B: (123038, 123039, 8),
        0x79545029: (123038, 123039, 9),
        0x79545034: (123038, 123039, 12),
        0x7954503C: (123038, 123039, 11),
        0x79557CB8: (123038, 123039, 8),
        0x7957E5CD: (123038, 123039, 9),
    },
    "Mugger": {
        0x7953AA11: (121567, 121567, 1),
        0x7953AD6B: (121567, 121567, 1),
        0x795450D4: (121567, 121567, 1),
        0x795451FE: (121567, 121567, 1),
        0x79557F14: (121567, 121567, 1),
        0x7957E5C6: (121567, 121567, 1),
        0x7957E5C7: (121567, 121567, 1),
        0x7957E5C8: (121567, 121567, 1),
        0x7957E5CA: (121567, 121567, 1),
    },
    "Redundant Scan": {
        0x7953AF85: (122027, 122027, 20),
        0x795451BF: (122026, 122027, 14),
        0x795451C4: (122028, 122029, 25),
        0x795451D3: (122026, 122027, 16),
    },
    "Workman Striker": {
        0x7953A84F: (122905, 122906, 19),
        0x7953A9F0: (122905, 122906, 17),
        0x7953AA0D: (122905, 122906, 18),
        0x7953AA16: (122905, 122906, 15),
        0x7953AA77: (122905, 122906, 14),
        0x7953AABE: (122905, 122906, 13),
        0x7953AAE9: (122905, 122906, 14),
        0x7953AB03: (122905, 122906, 16),
        0x7953AF95: (122905, 122906, 12),
        0x7953AFB8: (122905, 122906, 17),
        0x7953AFBC: (122905, 122906, 19),
        0x7953AFDD: (122905, 122906, 12),
        0x7953AFF9: (122905, 122906, 16),
        0x79545000: (122906, 122906, 20),
        0x7954501A: (122905, 122906, 16),
        0x79545108: (122905, 122906, 15),
        0x795451CA: (122907, 122908, 27),
        0x79545205: (122905, 122906, 11),
        0x79545213: (122905, 122906, 14),
        0x79545219: (122905, 122906, 19),
        0x79545224: (122905, 122906, 14),
    }
}

SUPPORTED_SOURCE_WEAPON_MONSTER_DATA = {
    "Mugger": 203734,
}

# Reviewed complete SCFU + owner WeaponItemFullUpdate pairs for every safe
# Incomplete Rebuild generation variant. Each tuple is attached to one
# canonical PF127 anchor; same-level weapon rerolls remain distinct atomic
# variants, while repeated observations of the same exact tuple only extend
# provenance. Ambiguous or level-only generations are deliberately excluded.
INCOMPLETE_REBUILD_GENERATION_EVIDENCE = {
    0x79545170: (
        ("20260709-222339", "(SimpleChar:79545170)"),
        ("20260716-034559", "(SimpleChar:796D4020)"),
    ),
    0x79545172: (
        ("20260709-222339", "(SimpleChar:79545172)"),
        ("20260716-034559", "(SimpleChar:796D401E)"),
    ),
    0x79545177: (
        ("20260709-222339", "(SimpleChar:79545177)"),
        ("20260716-034559", "(SimpleChar:796D4010)"),
        ("20260716-222007", "(SimpleChar:79702459)"),
    ),
    0x79545181: (
        ("20260709-222339", "(SimpleChar:79545181)"),
        ("20260716-034559", "(SimpleChar:796D4017)"),
        ("20260716-222007", "(SimpleChar:79702463)"),
        ("20260717-215250", "(SimpleChar:79748620)"),
    ),
    0x79545188: (
        ("20260709-222339", "(SimpleChar:79545188)"),
        ("20260716-034656", "(SimpleChar:796D4003)"),
        ("20260717-215250", "(SimpleChar:79748630)"),
    ),
    0x795451BC: (
        ("20260709-222339", "(SimpleChar:795451BC)"),
    ),
    0x795451C1: (
        ("20260709-222339", "(SimpleChar:795451C1)"),
    ),
    0x795451CB: (
        ("20260709-222339", "(SimpleChar:795451CB)"),
        ("20260716-034559", "(SimpleChar:796CD7DA)"),
        ("20260716-222007", "(SimpleChar:797024C6)"),
    ),
    0x795451FD: (
        ("20260709-222339", "(SimpleChar:795451FD)"),
        ("20260716-033326", "(SimpleChar:796D403C)"),
        ("20260716-222007", "(SimpleChar:7970254C)"),
    ),
    0x79545241: (
        ("20260709-222339", "(SimpleChar:79545241)"),
        ("20260709-225408", "(SimpleChar:79545352)"),
    ),
}

# Reviewed complete SCFU + owner WeaponItemFullUpdate pairs for Redundant Scan.
# Three stationary anchors are associated only by a unique <=1.5-unit source
# position. Source 0x795451C4 is the sole captured Redundant Scan patrol anchor;
# its later rows must retain the unique HasWaypoints shape. Rows without both a
# complete SCFU and owner weapon update remain report-only.
REDUNDANT_SCAN_GENERATION_EVIDENCE = {
    0x7953AF85: (
        ("20260709-222339", "(SimpleChar:7953AF85)"),
        ("20260716-034559", "(SimpleChar:796CD6EF)"),
        ("20260716-222007", "(SimpleChar:79702286)"),
        ("20260717-214612", "(SimpleChar:7973F090)"),
    ),
    0x795451BF: (
        ("20260709-222339", "(SimpleChar:795451BF)"),
    ),
    0x795451C4: (
        ("20260709-222339", "(SimpleChar:795451C4)"),
        ("20260716-034559", "(SimpleChar:796CD7D0)"),
        ("20260716-222007", "(SimpleChar:797024B6)"),
    ),
    0x795451D3: (
        ("20260709-222339", "(SimpleChar:795451D3)"),
        ("20260716-220400", "(SimpleChar:7970250F)"),
    ),
}

REDUNDANT_SCAN_PATROL_SOURCE = 0x795451C4

# Reviewed complete SCFU + owner WeaponItemFullUpdate pairs for Fragmented
# Soul. Every later identity is attached to one canonical PF127 anchor only by
# a unique <=1.5-unit position. Source 0x7954517A is the sole captured patrol
# anchor; unmatched identity 0x7970245D remains report-only.
FRAGMENTED_SOUL_GENERATION_EVIDENCE = {
    0x7954516A: (
        ("20260709-222339", "(SimpleChar:7954516A)"),
        ("20260716-033326", "(SimpleChar:796D403E)"),
    ),
    0x7954516F: (
        ("20260709-222339", "(SimpleChar:7954516F)"),
        ("20260716-034559", "(SimpleChar:796D401F)"),
    ),
    0x7954517A: (
        ("20260709-222339", "(SimpleChar:7954517A)"),
        ("20260716-034559", "(SimpleChar:796D4013)"),
    ),
    0x7954518A: (
        ("20260709-222339", "(SimpleChar:7954518A)"),
        ("20260716-034656", "(SimpleChar:796D4002)"),
        ("20260717-215250", "(SimpleChar:79748629)"),
    ),
    0x7954518B: (
        ("20260709-222339", "(SimpleChar:7954518B)"),
        ("20260716-034656", "(SimpleChar:796D4004)"),
        ("20260717-215250", "(SimpleChar:7974862E)"),
    ),
    0x7954518E: (
        ("20260709-222339", "(SimpleChar:7954518E)"),
        ("20260717-215250", "(SimpleChar:7974862B)"),
    ),
    0x795451AA: (
        ("20260709-222339", "(SimpleChar:795451AA)"),
    ),
    0x795451AE: (
        ("20260709-222339", "(SimpleChar:795451AE)"),
    ),
    0x79545248: (
        ("20260709-222339", "(SimpleChar:79545248)"),
        ("20260710-211430", "(SimpleChar:7957E5F7)"),
    ),
    0x79545367: (
        ("20260709-225408", "(SimpleChar:79545367)"),
        ("20260716-033326", "(SimpleChar:796D403F)"),
    ),
}

FRAGMENTED_SOUL_PATROL_SOURCE = 0x7954517A

# Audited complete-open denominator for Workman Striker.  The legacy capture
# predates corpse-loot-observations.csv, so the item rows are recovered from
# the identity-linked first inventory snapshots below.  The four canonical
# 20260709-212336 generations contain two positive and two explicit empty
# opens; 20260709-220439 contributes six further positive complete opens.
WORKMAN_STRIKER_STRICT_LOOT_CAPTURES = frozenset(
    ("20260709-212336", "20260709-220439")
)
WORKMAN_STRIKER_STRICT_OPENED_CORPSES = 10
WORKMAN_STRIKER_STRICT_POSITIVE_CORPSES = 8
WORKMAN_STRIKER_STRICT_EMPTY_CORPSES = 2
WORKMAN_STRIKER_STRICT_ITEM_COUNTS = Counter(
    {
        (234877, 234877, 1): 1,
        (130087, 130088, 16): 1,
        (202719, 202720, 17): 1,
        (124025, 124026, 12): 1,
        (202719, 202720, 14): 2,
        (234874, 234874, 1): 1,
        (124263, 124264, 13): 1,
        (301714, 301714, 1): 2,
        (202719, 202720, 12): 1,
        (85562, 85561, 14): 1,
    }
)
WORKMAN_STRIKER_EMPTY_INVENTORY_GENERATIONS = (
    (
        "2026-07-10T02:30:03.3896799Z [SMOKE] IN InventoryUpdate "
        "identity=(SimpleChar:7944C065) Unknown1=21 Unknown2=2 Items=count=0[] "
        "InventoryIdentity=(Corpse:F6E002) Handle=168 Unknown3=1 "
        "N3MessageType=InventoryUpdate Unknown=1 PacketType=N3Message",
        "2026-07-10T02:29:58.0753630Z",
        "(Corpse:00F6E002)",
        "(SimpleChar:7953AA16)",
    ),
    (
        "2026-07-10T02:36:21.5234655Z [SMOKE] IN InventoryUpdate "
        "identity=(SimpleChar:7944C065) Unknown1=21 Unknown2=2 Items=count=0[] "
        "InventoryIdentity=(Corpse:F6E005) Handle=181 Unknown3=1 "
        "N3MessageType=InventoryUpdate Unknown=1 PacketType=N3Message",
        "2026-07-10T02:36:15.8378611Z",
        "(Corpse:00F6E005)",
        "(SimpleChar:7953AABE)",
    ),
)

# Each reviewed generation is pinned to one exact CorpseFullUpdate identity
# and the first raw corpse InventoryUpdate before that corpse identity is
# reused.  This applies equally to legacy captures and newer captures that
# also emitted derived corpse-loot observations.  Empty packets are
# first-class denominator evidence; unopened and snapshot-only corpses never
# enter this table.  The resulting basis points are private existing-capture
# policy, not an official-live probability claim.
REVIEWED_LEGACY_STRICT_LOOT_DEFINITIONS = {
    "Shadow": {
        "monster_data": 30464,
        "captures": ("20260709-212336", "20260712-223719"),
        "opened": 15,
        "positive": 8,
        "empty": 7,
        "overlap": ("20260709-212115", "20260709-212336", 8),
        "item_counts": Counter(
            {
                (234875, 234875, 1): 2,
                (21601, 21601, 1): 1,
                (27199, 27199, 10): 1,
                (121931, 121932, 15): 1,
                (122007, 122008, 12): 1,
                (123666, 123667, 9): 1,
                (124364, 124365, 10): 1,
                (124512, 124513, 28): 1,
                (152279, 152280, 18): 1,
                (234876, 234876, 1): 1,
            }
        ),
        "generations": (
            ("20260709-212336", "2026-07-10T02:28:25.7622393Z", "(Corpse:00F6E00C)", "(SimpleChar:79528829)", "2026-07-10T02:28:56.0412549Z", 7679, ()),
            ("20260709-212336", "2026-07-10T02:28:36.4939023Z", "(Corpse:00F6E004)", "(SimpleChar:7952882A)", "2026-07-10T02:28:57.1917604Z", 7706, ()),
            ("20260709-212336", "2026-07-10T02:28:53.4426925Z", "(Corpse:00F6E00F)", "(SimpleChar:79528828)", "2026-07-10T02:28:59.8077575Z", 7775, ((234875, 234875, 1), (124364, 124365, 10))),
            ("20260709-212336", "2026-07-10T02:29:03.6073827Z", "(Corpse:00F6E005)", "(SimpleChar:79528817)", "2026-07-10T02:29:10.2412491Z", 8049, ((123666, 123667, 9),)),
            ("20260709-212336", "2026-07-10T02:29:12.5751669Z", "(Corpse:00F6E006)", "(SimpleChar:7952880B)", "2026-07-10T02:29:20.9409728Z", 8298, ()),
            ("20260709-212336", "2026-07-10T02:30:24.1547653Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA55)", "2026-07-10T02:30:35.4252842Z", 10170, ()),
            ("20260709-212336", "2026-07-10T02:30:39.5878831Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA56)", "2026-07-10T02:30:42.4304304Z", 10353, ()),
            ("20260709-212336", "2026-07-10T02:31:01.5684243Z", "(Corpse:00F6E00B)", "(SimpleChar:7953AA1C)", "2026-07-10T02:31:05.1351807Z", 10937, ()),
            ("20260709-212336", "2026-07-10T02:31:18.8360694Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA53)", "2026-07-10T02:31:21.7859328Z", 11368, ((122007, 122008, 12),)),
            ("20260709-212336", "2026-07-10T02:32:30.6241496Z", "(Corpse:00F6E005)", "(SimpleChar:7953AA2B)", "2026-07-10T02:32:33.1666683Z", 12638, ()),
            ("20260709-212336", "2026-07-10T02:33:04.8836498Z", "(Corpse:00F6E00F)", "(SimpleChar:7953AA33)", "2026-07-10T02:33:07.6113092Z", 13131, ((234875, 234875, 1), (27199, 27199, 10))),
            ("20260709-212336", "2026-07-10T02:33:36.1981049Z", "(Corpse:00F6E010)", "(SimpleChar:7953AA2A)", "2026-07-10T02:33:37.8124962Z", 13538, ((234876, 234876, 1), (121931, 121932, 15))),
            ("20260712-223719", "2026-07-13T03:39:03.3427964Z", "(Corpse:00F6C011)", "(SimpleChar:79607876)", "2026-07-13T03:39:35.6502502Z", 2914, ((152279, 152280, 18),)),
            ("20260712-223719", "2026-07-13T03:38:49.5373589Z", "(Corpse:00F6C004)", "(SimpleChar:79607875)", "2026-07-13T03:39:37.0377259Z", 2941, ((124512, 124513, 28),)),
            ("20260712-223719", "2026-07-13T03:39:33.0802477Z", "(Corpse:00F6C007)", "(SimpleChar:79607838)", "2026-07-13T03:39:52.8441413Z", 3180, ((21601, 21601, 1),)),
        ),
    },
    "Infector": {
        "monster_data": 31909,
        "captures": ("20260709-222339", "20260709-225408", "20260710-211430"),
        "opened": 7,
        "positive": 3,
        "empty": 4,
        "overlap": None,
        "item_counts": Counter(
            {
                (101507, 101508, 20): 1,
                (101735, 101736, 21): 1,
                (107491, 107492, 15): 1,
                (234875, 234875, 1): 1,
            }
        ),
        "generations": (
            ("20260709-222339", "2026-07-10T03:26:18.6915380Z", "(Corpse:00F6E002)", "(SimpleChar:7954514F)", "2026-07-10T03:26:22.3934221Z", 2748, ((101735, 101736, 21),)),
            ("20260709-222339", "2026-07-10T03:26:52.8902204Z", "(Corpse:00F6E015)", "(SimpleChar:79545150)", "2026-07-10T03:26:56.3893801Z", 3186, ()),
            ("20260709-222339", "2026-07-10T03:27:38.3547556Z", "(Corpse:00F6E017)", "(SimpleChar:79545153)", "2026-07-10T03:27:42.4041111Z", 3799, ()),
            ("20260709-222339", "2026-07-10T03:28:21.1703196Z", "(Corpse:00F6E00B)", "(SimpleChar:79545154)", "2026-07-10T03:28:24.6360935Z", 4382, ()),
            ("20260709-225408", "2026-07-10T04:04:09.1808342Z", "(Corpse:00F6E011)", "(SimpleChar:795451C9)", "2026-07-10T04:04:34.3726986Z", 16067, ((101507, 101508, 20),)),
            ("20260710-211430", "2026-07-11T02:16:47.3405240Z", "(Corpse:00F6C003)", "(SimpleChar:7957E648)", "2026-07-11T02:16:50.6906828Z", 4579, ()),
            ("20260710-211430", "2026-07-11T02:17:17.8568258Z", "(Corpse:00F6C004)", "(SimpleChar:7957E658)", "2026-07-11T02:17:22.2251371Z", 5302, ((234875, 234875, 1), (107491, 107492, 15))),
        ),
    },
    "Architect Striker": {
        "monster_data": 203743,
        "captures": ("20260709-212336", "20260709-220439"),
        "opened": 4,
        "positive": 3,
        "empty": 1,
        "overlap": None,
        "item_counts": Counter(
            {
                (122482, 122483, 14): 1,
                (124422, 124423, 13): 1,
                (128890, 128891, 14): 1,
                (234877, 234877, 1): 1,
            }
        ),
        "generations": (
            ("20260709-212336", "2026-07-10T02:34:59.2293267Z", "(Corpse:00F6E004)", "(SimpleChar:7953A9BD)", "2026-07-10T02:35:02.4566325Z", 14593, ()),
            ("20260709-220439", "2026-07-10T03:12:42.4436898Z", "(Corpse:00F6E015)", "(SimpleChar:7953A9B6)", "2026-07-10T03:13:25.1256509Z", 13935, ((128890, 128891, 14),)),
            ("20260709-220439", "2026-07-10T03:12:35.4312488Z", "(Corpse:00F6E004)", "(SimpleChar:7953A9B3)", "2026-07-10T03:13:27.1095766Z", 13977, ((124422, 124423, 13),)),
            ("20260709-220439", "2026-07-10T03:12:45.3434043Z", "(Corpse:00F6E016)", "(SimpleChar:7953AAEB)", "2026-07-10T03:13:29.0253712Z", 14004, ((234877, 234877, 1), (122482, 122483, 14))),
        ),
    },
    "Melded Patterns": {
        "monster_data": 203747,
        "captures": ("20260709-225408", "20260712-223719"),
        "opened": 4,
        "positive": 3,
        "empty": 1,
        "overlap": None,
        "item_counts": Counter(
            {
                (122672, 122673, 15): 1,
                (144067, 144068, 23): 1,
                (152328, 152329, 24): 1,
                (234874, 234874, 1): 1,
                (301710, 301710, 1): 1,
            }
        ),
        "generations": (
            ("20260709-225408", "2026-07-10T04:00:25.3985330Z", "(Corpse:00F6E013)", "(SimpleChar:79545190)", "2026-07-10T04:00:32.9353828Z", 10086, ((234874, 234874, 1), (122672, 122673, 15))),
            ("20260709-225408", "2026-07-10T04:00:47.0436246Z", "(Corpse:00F6E001)", "(SimpleChar:79545196)", "2026-07-10T04:00:52.9341347Z", 10944, ((152328, 152329, 24),)),
            ("20260712-223719", "2026-07-13T03:39:17.3969861Z", "(Corpse:00F6C014)", "(SimpleChar:79607872)", "2026-07-13T03:39:40.2441054Z", 2997, ((144067, 144068, 23), (301710, 301710, 1))),
            ("20260712-223719", "2026-07-13T03:39:21.8903988Z", "(Corpse:00F6C006)", "(SimpleChar:79607878)", "2026-07-13T03:39:50.5304202Z", 3148, ()),
        ),
    },
}


def reviewed_strict_loot_definition(
    monster_data: int,
    captures: tuple[str, ...],
    capture_allocations: dict[str, int],
    opened: int,
    positive: int,
    empty: int,
    generation_digest: str,
    item_counts: dict[tuple[int, int, int], int],
    overlap: tuple[str, str, int] | None = None,
) -> dict[str, object]:
    return {
        "monster_data": monster_data,
        "captures": captures,
        "capture_allocations": Counter(capture_allocations),
        "opened": opened,
        "positive": positive,
        "empty": empty,
        "overlap": overlap,
        "item_counts": Counter(item_counts),
        "generation_digest": generation_digest,
    }


REVIEWED_LEGACY_STRICT_LOOT_DEFINITIONS.update(
    {
        "Mugger": reviewed_strict_loot_definition(
            203734,
            (
                "20260708-143600",
                "20260709-205921",
                "20260709-210452",
                "20260709-212336",
                "20260710-202132",
            ),
            {
                "20260708-143600": 6,
                "20260709-205921": 1,
                "20260709-210452": 5,
                "20260709-212336": 4,
                "20260710-202132": 1,
            },
            17,
            14,
            3,
            "18c7678d6b6b3f31f76d87d55316a9dc32c554d7b37f18ef7416858082f3b5fc",
            {
                (25822, 25831, 5): 1,
                (85711, 22014, 8): 1,
                (123704, 123705, 9): 1,
                (123723, 123724, 6): 1,
                (123976, 123977, 9): 1,
                (124348, 124349, 7): 1,
                (124545, 124546, 10): 1,
                (128636, 128637, 8): 1,
                (128839, 128840, 9): 1,
                (130060, 130061, 5): 1,
                (130060, 130061, 9): 1,
                (131605, 131606, 7): 1,
                (136638, 136639, 9): 1,
                (136638, 136639, 12): 1,
                (136640, 136641, 7): 1,
                (136640, 136641, 8): 1,
                (136640, 136641, 9): 1,
                (136646, 136647, 9): 1,
                (160224, 160225, 10): 1,
                (234875, 234875, 1): 2,
                (234876, 234876, 1): 1,
            },
            ("20260709-212115", "20260709-212336", 7),
        ),
        "Discarded Pet": reviewed_strict_loot_definition(
            17720,
            ("20260708-143600", "20260709-210452"),
            {"20260708-143600": 13, "20260709-210452": 3},
            16,
            13,
            3,
            "7a038d4800544dd8f29f108bba8fb7f35f945abbdf6b84dc5a82373de4831aa6",
            {
                (101681, 101682, 7): 1,
                (102283, 102284, 9): 1,
                (103973, 103974, 10): 1,
                (106005, 106006, 11): 1,
                (107283, 107284, 10): 1,
                (109520, 109521, 7): 1,
                (111623, 111624, 8): 1,
                (112160, 112161, 6): 1,
                (112798, 112799, 6): 1,
                (234874, 234874, 1): 3,
                (234876, 234876, 1): 3,
                (234877, 234877, 1): 1,
                (290619, 202727, 9): 1,
            },
        ),
        "Stim Fiend": reviewed_strict_loot_definition(
            203739,
            ("20260708-143600", "20260709-210452", "20260709-212336"),
            {
                "20260708-143600": 6,
                "20260709-210452": 6,
                "20260709-212336": 1,
            },
            13,
            13,
            0,
            "f2e9afd7eefb65b8c7b1e33103d08f7ff876f44b8e1421561259f1b02d96edb1",
            {
                (102055, 102056, 11): 1,
                (112232, 112233, 11): 1,
                (234874, 234874, 1): 1,
                (234876, 234876, 1): 1,
                (234877, 234877, 1): 1,
                (291043, 291044, 9): 6,
                (291043, 291044, 10): 2,
                (291043, 291044, 11): 1,
                (291043, 291044, 12): 2,
                (291043, 291044, 13): 1,
                (291043, 291044, 15): 1,
                (291082, 291083, 9): 6,
                (291082, 291083, 10): 2,
                (291082, 291083, 11): 1,
                (291082, 291083, 12): 2,
                (291082, 291083, 13): 1,
                (291082, 291083, 15): 1,
            },
            ("20260709-212115", "20260709-212336", 2),
        ),
        "Looter": reviewed_strict_loot_definition(
            203745,
            ("20260708-143600", "20260709-210452"),
            {"20260708-143600": 6, "20260709-210452": 5},
            11,
            6,
            5,
            "e33606057496b305707490580c6c2628f83092eb8529ee817a67d83e04aa13cb",
            {
                (21605, 21605, 1): 1,
                (85501, 22343, 12): 1,
                (124422, 124422, 12): 1,
                (144082, 144083, 7): 1,
                (234874, 234874, 1): 1,
                (234875, 234875, 1): 1,
                (234877, 234877, 1): 1,
                (301713, 301713, 1): 1,
                (301714, 301714, 1): 1,
            },
        ),
        "Violent Vagabond": reviewed_strict_loot_definition(
            203733,
            ("20260708-143600", "20260709-210452", "20260709-225408"),
            {
                "20260708-143600": 6,
                "20260709-210452": 4,
                "20260709-225408": 1,
            },
            11,
            10,
            1,
            "33efb5b56c8c9120aff3a3f718a3e944d5c42410415f91c1a6e76f0a6a90ae12",
            {
                (85531, 22289, 8): 1,
                (122140, 122141, 7): 1,
                (123704, 123705, 12): 1,
                (128715, 128716, 6): 1,
                (130586, 130586, 1): 4,
                (130592, 130592, 1): 2,
                (130621, 130621, 1): 1,
                (152326, 152327, 6): 1,
                (234876, 234876, 1): 1,
                (258543, 258543, 1): 7,
                (273381, 204397, 8): 1,
            },
        ),
        "Bloodcreeper": reviewed_strict_loot_definition(
            30379,
            (
                "20260716-033326",
                "20260716-034104",
                "20260716-221358",
                "20260717-214751",
            ),
            {
                "20260716-033326": 1,
                "20260716-034104": 1,
                "20260716-221358": 1,
                "20260717-214751": 1,
            },
            4,
            1,
            3,
            "0920f73b01e6961b8be84945307a8a36c9b2546ed1efef302b0cf9e0e0365d51",
            {(42640, 42641, 30): 1},
        ),
        "Infected Attendant": reviewed_strict_loot_definition(
            96056,
            ("20260709-220439", "20260709-225408"),
            {"20260709-220439": 3, "20260709-225408": 1},
            4,
            3,
            1,
            "ca54bf62cea4862f6b0255d0525d200f18cb120fc0fda4a3e853076aa048870b",
            {
                (101695, 101696, 24): 1,
                (109194, 109195, 12): 1,
                (112823, 112824, 17): 1,
                (234875, 234875, 1): 1,
                (290619, 202727, 12): 1,
            },
        ),
        "Fragmented Soul": reviewed_strict_loot_definition(
            203729,
            ("20260709-225408",),
            {"20260709-225408": 4},
            4,
            4,
            0,
            "b659cb5707c6616b644993e3bf43a0ad6457bb328779931f9b208f114d6e395f",
            {
                (26471, 26471, 14): 3,
                (85691, 22004, 18): 1,
                (85732, 21963, 17): 1,
                (124304, 124305, 17): 1,
                (234877, 234877, 1): 2,
                (301712, 301712, 1): 1,
            },
        ),
        "Deranged Shopper": reviewed_strict_loot_definition(
            203736,
            ("20260708-143600", "20260709-210452"),
            {"20260708-143600": 1, "20260709-210452": 1},
            2,
            2,
            0,
            "b8b38b0fc4613cb3bcabaa811388e980b8ca63eb5480d4a37900d8959286c7c5",
            {(123019, 123020, 6): 1, (124465, 124466, 10): 1},
        ),
        "Incomplete Rebuild": reviewed_strict_loot_definition(
            203728,
            ("20260709-225408", "20260710-211430"),
            {"20260709-225408": 1, "20260710-211430": 1},
            2,
            2,
            0,
            "b6c9f072d13195abb684afcf77bd53dabce096fb3c59300720c6ada86edc2c10",
            {(26503, 26503, 14): 1, (142817, 142818, 16): 1},
        ),
        "Redundant Scan": reviewed_strict_loot_definition(
            204178,
            ("20260709-225408", "20260716-222201"),
            {"20260709-225408": 1, "20260716-222201": 1},
            2,
            1,
            1,
            "b7acb4c6b6e02c366e496f47581d3c2cb433bd5e673d24637ef76ab73e22a6b1",
            {(27263, 27263, 10): 1},
        ),
        "Uncontrollable Anger": reviewed_strict_loot_definition(
            96195,
            ("20260709-225408", "20260710-211430"),
            {"20260709-225408": 1, "20260710-211430": 1},
            2,
            2,
            0,
            "d6cb96590fc22adab7cffc3e90ac137cb9acc16697c3f32a45a44d4d20d3540e",
            {
                (101809, 101810, 24): 1,
                (109366, 109367, 9): 1,
                (290619, 202727, 19): 1,
            },
        ),
        "Lost Thought": reviewed_strict_loot_definition(
            96193,
            ("20260709-225408",),
            {"20260709-225408": 1},
            1,
            1,
            0,
            "6b2e02b3c2587fdfebf627159930d52fbd2e66855f6c173e6569e8aba2dd20ad",
            {(101675, 101676, 25): 1},
        ),
        "Neural Burnout": reviewed_strict_loot_definition(
            203730,
            (
                "20260709-225408",
                "20260710-211430",
                "20260716-034104",
                "20260716-221358",
            ),
            {
                "20260709-225408": 1,
                "20260710-211430": 1,
                "20260716-034104": 1,
                "20260716-221358": 1,
            },
            4,
            2,
            2,
            "10fd92c4c311009c7f4fcc8e605fa63b2e95c414efd78d743373cddf8d819c17",
            {
                (26471, 26471, 14): 1,
                (123021, 123021, 21): 1,
                (124560, 124561, 16): 1,
            },
        ),
    }
)
CAPTURE_CORPSE_EVIDENCE_FILTERS = {
    "20260708-004038": frozenset(("Discarded Pet", "Filth Flea", "Thief")),
    "20260708-143600": frozenset(
        (
            "Deranged Shopper",
            "Discarded Pet",
            "Disobedient Bot",
            "Filth Flea",
            "Looter",
            "Mugger",
            "Stim Fiend",
            "Violent Vagabond",
        )
    ),
    "20260709-210452": frozenset(
        (
            "Deranged Shopper",
            "Discarded Pet",
            "Disobedient Bot",
            "Filth Flea",
            "Looter",
            "Mugger",
            "Stim Fiend",
            "Violent Vagabond",
        )
    ),
    "20260709-212336": frozenset(
        (
            "Architect Striker",
            "Mugger",
            "Shadow",
            "Stim Fiend",
            "Violent Vagabond",
            "Workman Striker",
        )
    ),
    "20260709-220439": frozenset(
        (
            "Architect Striker",
            "Discarded Pet",
            "Disobedient Bot",
            "Filth Flea",
            "Infected Attendant",
            "Mugger",
            "Shadow",
            "Slum Runner",
            "Stim Fiend",
            "Uncontrollable Anger",
            "Workman Striker",
        )
    ),
    "20260709-222339": frozenset(
        (
            "Fragmented Soul",
            "Incomplete Rebuild",
            "Infector",
            "Mugger",
            "Neural Burnout",
            "Slum Runner",
            "Uncontrollable Anger",
            "Workman Striker",
        )
    ),
    "20260709-225408": frozenset(
        (
            "Empty Shell",
            "Filth Flea",
            "Fragmented Soul",
            "Incomplete Rebuild",
            "Infected Attendant",
            "Infector",
            "Lost Thought",
            "Melded Patterns",
            "Molested Molecules",
            "Mugger",
            "Neural Burnout",
            "Redundant Scan",
            "Shadow",
            "Slum Runner",
            "Stim Fiend",
            "Uncontrollable Anger",
            "Violent Vagabond",
            "Workman Striker",
        )
    ),
    "20260709-205921": frozenset(("Discarded Pet", "Disobedient Bot")),
    "20260712-153918": frozenset(
        (
            "Discarded Pet",
            "Disobedient Bot",
            "Filth Flea",
            "Mugger",
            "Thief",
            "Violent Vagabond",
        )
    ),
    "20260710-202132": frozenset(("Mugger",)),
    "20260710-211430": frozenset(
        (
            "Infector",
            "Neural Burnout",
            "Premature Pattern",
            "Slum Runner",
            "Uncontrollable Anger",
        )
    ),
    "20260712-155528": frozenset(("Filth Flea",)),
    "20260712-160257": frozenset(("Disobedient Bot",)),
    "20260712-161506": frozenset(("Filth Flea", "Thief")),
    "20260713-013906": frozenset(("Discarded Pet", "Mugger")),
    "20260713-014714": frozenset(("Disobedient Bot",)),
    "20260713-033511": frozenset(("Disobedient Bot",)),
    "20260712-223719": frozenset(
        (
            "Bloodcreeper",
            "Infector",
            "Melded Patterns",
            "Molested Molecules",
            "Neural Burnout",
            "Premature Pattern",
            "Shadow",
        )
    ),
    "20260712-224608": frozenset(("Fragmented Soul", "Premature Pattern")),
    "20260712-232137": frozenset(("Infector",)),
    "20260716-034104": frozenset(("Neural Burnout",)),
    "20260716-034656": frozenset(("Slum Runner",)),
    "20260716-215947": frozenset(
        (
            "Melded Patterns",
            "Molested Molecules",
            "Premature Pattern",
            "Shadow",
            "Slum Runner",
        )
    ),
    "20260716-221748": frozenset(("Neural Burnout",)),
    "20260716-221358": frozenset(("Molested Molecules", "Neural Burnout")),
    "20260716-222007": frozenset(
        ("Fragmented Soul", "Incomplete Rebuild", "Molested Molecules")
    ),
    "20260716-222201": frozenset(("Redundant Scan", "Slum Runner")),
}
CAPTURE_CORPSE_IDENTITY_FILTERS = {
    "20260709-205921": frozenset(
        ("(SimpleChar:795310FB)", "(SimpleChar:7953178A)")
    ),
    "20260709-220439": frozenset(
        (
            "(SimpleChar:79513A87)",
            "(SimpleChar:79513A8F)",
            "(SimpleChar:79513AAF)",
            "(SimpleChar:79513AC2)",
            "(SimpleChar:7953A830)",
            "(SimpleChar:7953A84F)",
            "(SimpleChar:7953A880)",
            "(SimpleChar:7953A884)",
            "(SimpleChar:7953A96C)",
            "(SimpleChar:7953A97A)",
            "(SimpleChar:7953A993)",
            "(SimpleChar:7953A9B3)",
            "(SimpleChar:7953A9B6)",
            "(SimpleChar:7953A9E1)",
            "(SimpleChar:7953A9E6)",
            "(SimpleChar:7953A9E7)",
            "(SimpleChar:7953A9EA)",
            "(SimpleChar:7953A9F7)",
            "(SimpleChar:7953A9FC)",
            "(SimpleChar:7953AA04)",
            "(SimpleChar:7953AA0D)",
            "(SimpleChar:7953AA19)",
            "(SimpleChar:7953AA1A)",
            "(SimpleChar:7953AA1B)",
            "(SimpleChar:7953AA32)",
            "(SimpleChar:7953AA81)",
            "(SimpleChar:7953AA82)",
            "(SimpleChar:7953AAE9)",
            "(SimpleChar:7953AAEB)",
            "(SimpleChar:7953AB08)",
            "(SimpleChar:7953AB2D)",
            "(SimpleChar:7953ABAF)",
            "(SimpleChar:7953ABC0)",
            "(SimpleChar:7953ABC3)",
            "(SimpleChar:7953AD65)",
            "(SimpleChar:7953AD69)",
            "(SimpleChar:7953AD6B)",
            "(SimpleChar:7953AE95)",
            "(SimpleChar:7953AFB8)",
            "(SimpleChar:79545000)",
            "(SimpleChar:79545136)",
        )
    ),
    "20260709-222339": frozenset(
        (
            "(SimpleChar:7953A9F0)",
            "(SimpleChar:7954514F)",
            "(SimpleChar:79545150)",
            "(SimpleChar:79545153)",
            "(SimpleChar:79545154)",
            "(SimpleChar:795451FE)",
            "(SimpleChar:79545201)",
            "(SimpleChar:7954520E)",
            "(SimpleChar:79545212)",
            "(SimpleChar:79545216)",
            "(SimpleChar:79545219)",
            "(SimpleChar:79545224)",
            "(SimpleChar:79545241)",
            "(SimpleChar:79545248)",
            "(SimpleChar:7954524A)",
        )
    ),
    "20260709-225408": frozenset(
        (
            "(SimpleChar:795317F5)",
            "(SimpleChar:7953A9C2)",
            "(SimpleChar:7953AA0C)",
            "(SimpleChar:7953AD4A)",
            "(SimpleChar:7953AD4C)",
            "(SimpleChar:7953AD64)",
            "(SimpleChar:7953AD70)",
            "(SimpleChar:7953AD71)",
            "(SimpleChar:7953AECD)",
            "(SimpleChar:7953AED2)",
            "(SimpleChar:7953AF6D)",
            "(SimpleChar:7953AF71)",
            "(SimpleChar:7953AF76)",
            "(SimpleChar:7953AF7B)",
            "(SimpleChar:7953AF7F)",
            "(SimpleChar:7953AF85)",
            "(SimpleChar:7953AFF7)",
            "(SimpleChar:795450E5)",
            "(SimpleChar:795450FE)",
            "(SimpleChar:79545142)",
            "(SimpleChar:7954514E)",
            "(SimpleChar:79545179)",
            "(SimpleChar:7954517C)",
            "(SimpleChar:7954517D)",
            "(SimpleChar:79545187)",
            "(SimpleChar:79545190)",
            "(SimpleChar:79545191)",
            "(SimpleChar:79545196)",
            "(SimpleChar:79545198)",
            "(SimpleChar:7954519B)",
            "(SimpleChar:795451A2)",
            "(SimpleChar:795451A4)",
            "(SimpleChar:795451AA)",
            "(SimpleChar:795451AC)",
            "(SimpleChar:795451AE)",
            "(SimpleChar:795451B5)",
            "(SimpleChar:795451B7)",
            "(SimpleChar:795451B9)",
            "(SimpleChar:795451BC)",
            "(SimpleChar:795451BF)",
            "(SimpleChar:795451C0)",
            "(SimpleChar:795451C1)",
            "(SimpleChar:795451C2)",
            "(SimpleChar:795451C4)",
            "(SimpleChar:795451C9)",
            "(SimpleChar:795451CA)",
            "(SimpleChar:795451CB)",
            "(SimpleChar:795451D8)",
            "(SimpleChar:795451DD)",
            "(SimpleChar:795451FD)",
            "(SimpleChar:79545231)",
            "(SimpleChar:795452E5)",
            "(SimpleChar:79545306)",
            "(SimpleChar:79545309)",
            "(SimpleChar:7954530F)",
            "(SimpleChar:79545313)",
            "(SimpleChar:79545314)",
            "(SimpleChar:79545319)",
            "(SimpleChar:7954531C)",
            "(SimpleChar:79545329)",
            "(SimpleChar:7954532B)",
        )
    ),
    "20260712-223719": frozenset(
        (
            "(SimpleChar:795F9516)",
            "(SimpleChar:795F951A)",
            "(SimpleChar:79607838)",
            "(SimpleChar:7960785D)",
            "(SimpleChar:79607872)",
            "(SimpleChar:79607873)",
            "(SimpleChar:79607874)",
            "(SimpleChar:79607875)",
            "(SimpleChar:79607876)",
            "(SimpleChar:79607877)",
            "(SimpleChar:79607878)",
            "(SimpleChar:7960787E)",
            "(SimpleChar:7960787F)",
        )
    ),
    "20260712-160257": frozenset(("(SimpleChar:795EC78A)",)),
    "20260713-014714": frozenset(("(SimpleChar:79607CD0)",)),
    "20260713-033511": frozenset(("(SimpleChar:79607E2C)",)),
    "20260710-211430": frozenset(
        (
            "(SimpleChar:7957E62C)",
            "(SimpleChar:7957E630)",
            "(SimpleChar:7957E648)",
            "(SimpleChar:7957E653)",
            "(SimpleChar:7957E656)",
            "(SimpleChar:7957E65A)",
        )
    ),
    "20260712-232137": frozenset(
        (
            "(SimpleChar:79607AC5)",
            "(SimpleChar:79607AC6)",
            "(SimpleChar:79607AD0)",
            "(SimpleChar:79607AD1)",
            "(SimpleChar:79607AD2)",
        )
    ),
    "20260716-034104": frozenset(("(SimpleChar:796CD74A)",)),
    "20260716-221358": frozenset(
        ("(SimpleChar:79702517)", "(SimpleChar:7970251A)")
    ),
    "20260716-222007": frozenset(
        (
            "(SimpleChar:79702438)",
            "(SimpleChar:79702459)",
            "(SimpleChar:7970245D)",
            "(SimpleChar:7970245E)",
        )
    ),
    "20260716-222201": frozenset(
        ("(SimpleChar:797024DA)", "(SimpleChar:7970250F)")
    ),
}
CAPTURE_LIFECYCLE_DEATH_LEVEL_FILTERS = {
    "20260712-160257": frozenset(("(SimpleChar:795EC78A)",)),
}
# Legacy capture 004038 predates enemy-state death rows, but its complete SCFU
# identifies the same living NPC that the exact corpse full update later names.
# Keep this join explicit and fail closed so the L10 credit outcome cannot be
# inferred from credits alone.
SCFU_CORPSE_LEVEL_EVIDENCE = {
    "20260708-004038": {
        "(SimpleChar:794A16EE)": {
            "name": "Discarded Pet",
            "monster_data": 17720,
            "level": 10,
            "health": 227,
        }
    }
}
# The same official-live PF127 instance stayed alive across these adjacent
# captures. The earlier dossier pins the living NPC's identity and level; the
# later CFU pins that exact dead identity to its positive initial corpse credit
# outcome. This recovers evidence that a single capture-local death table cannot
# join without inventing a level.
CROSS_SESSION_CORPSE_LEVEL_EVIDENCE = {
    "20260716-222007": {
        "(SimpleChar:79702438)": {
            "dossier_capture": "20260716-221358",
            "name": "Incomplete Rebuild",
            "monster_data": "203728",
            "level": 18,
            "health": 394,
        }
    }
}
ARCHETYPE_CAPTURE_FILTERS = {
    "Deranged Shopper": frozenset(("20260710-202132",)),
}
ARCHETYPE_SPAWN_IDENTITY_FILTERS = {
    "Bloodcreeper": frozenset(("(SimpleChar:795451C5)",)),
}
CAPTURE_SPAWN_IDENTITY_FILTERS = {
    "20260709-225408": frozenset(
        ("(SimpleChar:79545356)", "(SimpleChar:79545367)")
    ),
}
OUTPUT = (
    REPO
    / "AORebirth"
    / "Server"
    / "ZoneEngine"
    / "Core"
    / "Playfields"
    / "CapturedSubwayOrdinaryContentProvider.cs"
)

ARCHETYPES = {
    "Shadow": ("shadow", "shadow"),
    "Stim Fiend": ("stim_fiend", "stim_fiend"),
    "Workman Striker": ("workman_striker", "striker"),
    "Architect Striker": ("architect_striker", "striker"),
    "Infected Attendant": ("infected_attendant", "infected_attendant"),
    "Slum Runner": ("slum_runner", "slum_runner"),
    "Looter": ("looter", "looter"),
    "Infector": ("infector", "infector"),
    "Lost Thought": ("lost_thought", "lost_thought"),
    "Neural Burnout": ("neural_burnout", "neural_burnout"),
    "Bloodcreeper": ("bloodcreeper", "bloodcreeper"),
    "Deranged Shopper": ("deranged_shopper", "deranged_shopper"),
    "Empty Shell": ("empty_shell", "empty_shell"),
    "Fragmented Soul": ("fragmented_soul", "fragmented_soul"),
    "Incomplete Rebuild": ("incomplete_rebuild", "incomplete_rebuild"),
    "Melded Patterns": ("melded_patterns", "melded_patterns"),
    "Molested Molecules": ("molested_molecules", "molested_molecules"),
    "Premature Pattern": ("premature_pattern", "premature_pattern"),
    "Redundant Scan": ("redundant_scan", "redundant_scan"),
    "Uncontrollable Anger": ("uncontrollable_anger", "uncontrollable_anger"),
}

SUPPORTED_CORPSE_NAMES_BY_MONSTER_DATA = {
    17649: "Disobedient Bot",
    17657: "Filth Flea",
    17720: "Discarded Pet",
    26092: "Thief",
    203733: "Violent Vagabond",
    203734: "Mugger",
}
CORPSE_EVIDENCE_NAMES = frozenset(ARCHETYPES) | frozenset(
    SUPPORTED_CORPSE_NAMES_BY_MONSTER_DATA.values()
)
EXPECTED_CORPSE_MONSTER_DATA = {
    "Architect Striker": 203743,
    "Bloodcreeper": 30379,
    "Deranged Shopper": 203736,
    "Discarded Pet": 17720,
    "Disobedient Bot": 17649,
    "Empty Shell": 203731,
    "Filth Flea": 17657,
    "Fragmented Soul": 203729,
    "Incomplete Rebuild": 203728,
    "Infected Attendant": 96056,
    "Infector": 31909,
    "Lost Thought": 96193,
    "Melded Patterns": 203747,
    "Molested Molecules": 203746,
    "Mugger": 203734,
    "Neural Burnout": 203730,
    "Premature Pattern": 203727,
    "Redundant Scan": 204178,
    "Shadow": 30464,
    "Slum Runner": 55648,
    "Stim Fiend": 203739,
    "Thief": 26092,
    "Uncontrollable Anger": 96195,
    "Looter": 203745,
    "Violent Vagabond": 203733,
    "Workman Striker": 203854,
}

NAMED_BOSSES = frozenset(
    (
        "Abmouth Supremus",
        "Bitaxel",
        "Eumenides",
        "Strike Foreman",
        "Vergil Aeneid",
    )
)
OWNED_SUMMON_NAMES = frozenset(("Healer",))

CANDIDATE_ACCEPTED = "ACCEPTED_ORDINARY"
CANDIDATE_NAMED_BOSS = "NAMED_BOSS_EXCLUDED"
CANDIDATE_OWNED_SUMMON = "OWNED_SUMMON_EXCLUDED"
CANDIDATE_UNSUPPORTED = "UNSUPPORTED_EXCLUDED"
CANDIDATE_MALFORMED = "MALFORMED_EXCLUDED"

FLAG_VALUES = {
    "IsNpc": 0x00000001,
    "UnknownFlag": 0x00000002,
    "UnknownFlag6": 0x00000008,
    "HasExtendedTextures": 0x00000010,
    "HasFightingTarget": 0x00000020,
    "HasPlayfieldId": 0x00000040,
    "HasHeadMesh": 0x00000080,
    "HasNoWeaponPairs": 0x00000100,
    "HasHeading": 0x00000200,
    "IsUnderAttack": 0x00000400,
    "HasSmallHealth": 0x00000800,
    "HasExtendedLevel": 0x00001000,
    "HasExtendedRunSpeed": 0x00002000,
    "HasSmallHealthDamage": 0x00004000,
    "HasWaypoints": 0x00010000,
    "HasSmallNpcFamily": 0x00020000,
    "HasSmallNpcLosHeight": 0x00080000,
    "UnknownFlag7": 0x00200000,
    "UnknownFlag2": 0x00200000,
    "IsImmune": 0x00800000,
    "UnknownFlag3": 0x01000000,
    "UnknownDataFlag": 0x02000000,
    "HasOrgName": 0x04000000,
    "IsPet": 0x08000000,
    "UnknownFlag5": 0x10000000,
    "UnknownFlag4": 0x20000000,
}


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


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


def reviewed_uncontrollable_anger_recharge(
    rows: list[dict[str, object]],
) -> float:
    ordered = sorted(rows, key=lambda row: parse_precise_seconds(row["capturedUtc"]))
    expected = (
        ("2026-07-10T03:29:02.8010362Z", 42, 0, 0, 0x53495731),
        ("2026-07-10T03:29:07.9175875Z", 25, 0, 0, 0x53495731),
        ("2026-07-10T03:29:13.0847400Z", 27, 0, 0, 0x53495731),
        ("2026-07-10T03:29:23.1850889Z", 27, 0, 0, 0x53495731),
    )
    actual = tuple(
        (
            row["capturedUtc"],
            row["amount"],
            row["slot"],
            row["unknown"],
            row["instance"],
        )
        for row in ordered
    )
    if actual != expected:
        raise ValueError(
            "Uncontrollable Anger reviewed Killer-pet cadence rows drifted: "
            + repr(actual)
        )
    intervals = [
        parse_precise_seconds(current["capturedUtc"])
        - parse_precise_seconds(previous["capturedUtc"])
        for previous, current in zip(ordered, ordered[1:])
    ]
    if intervals != [
        Decimal("5.1165513"),
        Decimal("5.1671525"),
        Decimal("10.1003489"),
    ]:
        raise ValueError(
            "Uncontrollable Anger reviewed Killer-pet cadence intervals drifted: "
            + repr(intervals)
        )
    return float(sorted(intervals)[1])


def combat_event_fingerprint(row: dict[str, object]) -> tuple[object, ...]:
    return (
        row["identity"],
        row["target"],
        row["targetRole"],
        row["messageType"],
        row["amount"],
        row["slot"],
        row["unknown"],
        row["hitType"],
        row["instance"],
    )


def deduplicate_overlapping_combat_events(
    rows: list[dict[str, object]],
) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    capture_order = {capture: index for index, capture in enumerate(CAPTURES)}
    for canonical, duplicate in OVERLAPPING_COMBAT_CAPTURE_RULES:
        if capture_order[canonical] >= capture_order[duplicate]:
            raise ValueError(
                "combat overlap canonical capture order drifted: "
                + canonical
                + " -> "
                + duplicate
            )

    kept = []
    by_fingerprint = defaultdict(list)
    exclusions = []
    for row in sorted(rows, key=lambda value: value["order"]):
        fingerprint = combat_event_fingerprint(row)
        candidates = []
        for canonical in by_fingerprint[fingerprint]:
            tolerance = OVERLAPPING_COMBAT_CAPTURE_RULES.get(
                (canonical["capture"], row["capture"])
            )
            if (
                tolerance is None
                or row["capture"] in canonical["provenanceCaptures"]
            ):
                continue
            delta = abs((row["time"] - canonical["time"]).total_seconds())
            if delta <= tolerance:
                candidates.append((delta, canonical["order"], canonical))
        if candidates:
            _, _, canonical = min(candidates, key=lambda value: (value[0], value[1]))
            canonical["provenanceCaptures"].add(row["capture"])
            exclusions.append(
                {
                    "canonicalCapture": canonical["capture"],
                    "duplicateCapture": row["capture"],
                    "canonicalOrder": canonical["order"],
                    "duplicateOrder": row["order"],
                }
            )
            continue
        kept.append(row)
        by_fingerprint[fingerprint].append(row)
    return kept, exclusions


def combat_intervals(rows: list[dict[str, object]]) -> list[float]:
    intervals = []
    by_identity = defaultdict(list)
    for row in rows:
        by_identity[(row["capture"], row["identity"])].append(row["time"])
    for times in by_identity.values():
        times.sort()
        for previous, current in zip(times, times[1:]):
            seconds = (current - previous).total_seconds()
            if 0.5 <= seconds <= 10.0:
                intervals.append(seconds)
    intervals.sort()
    return intervals


def validate_combat_overlap_dedup(
    raw_rows: list[dict[str, object]],
    rows: list[dict[str, object]],
    exclusions: list[dict[str, object]],
) -> None:
    for exclusion in exclusions:
        pair = (
            exclusion["canonicalCapture"],
            exclusion["duplicateCapture"],
        )
        if pair not in OVERLAPPING_COMBAT_CAPTURE_RULES:
            raise ValueError(
                "unreviewed capture pair reached ordinary combat dedup: "
                + " -> ".join(pair)
            )
    duplicate_captures = {
        duplicate for _, duplicate in OVERLAPPING_COMBAT_CAPTURE_RULES
    }
    for capture in CAPTURES:
        if capture in duplicate_captures:
            continue
        raw_orders = [row["order"] for row in raw_rows if row["capture"] == capture]
        kept_orders = [row["order"] for row in rows if row["capture"] == capture]
        if raw_orders != kept_orders:
            raise ValueError(
                "non-overlapping combat rows changed for capture=" + capture
            )

    raw_workman = [row for row in raw_rows if row["name"] == "Workman Striker"]
    workman = [row for row in rows if row["name"] == "Workman Striker"]
    if Counter(row["hitType"] for row in raw_workman) != Counter(
        {"normal": 80, "critical": 10}
    ):
        raise ValueError("Workman Striker raw combat evidence drifted")
    if Counter(row["hitType"] for row in workman) != Counter(
        {"normal": 47, "critical": 6}
    ):
        raise ValueError("Workman Striker distinct hit counts drifted")
    intervals = combat_intervals(workman)
    if (
        len(intervals) != 41
        or intervals[0] != 4.747813
        or intervals[(len(intervals) - 1) // 2] != 5.092328
        or intervals[-1] != 5.733061
    ):
        raise ValueError("Workman Striker distinct cadence evidence drifted")
    canonical_critical = [
        row
        for row in workman
        if row["capture"] == "20260709-212115"
        and row["identity"] == "(SimpleChar:7953AFBC)"
        and row["capturedUtc"] == "2026-07-10T02:37:39.1002433Z"
        and row["hitType"] == "critical"
        and row["amount"] == 42
    ]
    if (
        len(canonical_critical) != 1
        or canonical_critical[0]["provenanceCaptures"]
        != {"20260709-212115", "20260709-213711"}
    ):
        raise ValueError("Workman Striker critical canonical provenance drifted")


def stable_row_key(row: dict[str, str]) -> tuple:
    return (
        parse_time(row["CapturedUtc"]),
        row.get("EvidenceCapture", ""),
        int(row.get("EvidenceRowIndex", "0")),
        row.get("Identity", ""),
    )


def identity_hex(identity: str) -> str:
    return identity.removeprefix("(SimpleChar:").removesuffix(")")


def parse_flags(value: str) -> int:
    result = 0
    for name in (part.strip() for part in value.split(",")):
        if name:
            result |= FLAG_VALUES.get(name, 0)
    return result


def parse_triplets(value: str) -> list[tuple[str, str, str]]:
    result = []
    for part in value.split("|") if value else []:
        fields = part.split(":")
        if len(fields) == 3:
            result.append((fields[0], fields[1], fields[2]))
    return result


def parse_textures(value: str) -> list[tuple[int, int, int]]:
    return [(int(a), int(b), int(c)) for a, b, c in parse_triplets(value)]


def parse_meshes(value: str) -> list[tuple[int, int, int, int]]:
    result = []
    for part in value.split("|") if value else []:
        fields = part.split(":")
        if len(fields) == 4:
            result.append(tuple(int(field) for field in fields))
    return result


def canonical_position(row: dict[str, str]) -> tuple[float, float, float]:
    waypoints = parse_triplets(row.get("Waypoints", ""))
    if waypoints:
        return tuple(float(value) for value in waypoints[0])
    return (float(row["PositionX"]), float(row["PositionY"]), float(row["PositionZ"]))


def capture_allows_archetype(capture: str, name: str) -> bool:
    allowed_names = CAPTURE_ARCHETYPE_FILTERS.get(capture)
    if allowed_names is not None and name not in allowed_names:
        return False
    allowed_captures = ARCHETYPE_CAPTURE_FILTERS.get(name)
    return allowed_captures is None or capture in allowed_captures


def load_raw_scfu_rows(captures: tuple[str, ...]) -> list[dict[str, str]]:
    rows = []
    for capture in captures:
        path = CAPTURE_ROOT / capture / "scfu-appearance.csv"
        if not path.exists():
            path = CAPTURE_ROOT / capture / "scfu-appearance.pending.csv"
        if not path.exists():
            continue
        for row_index, row in enumerate(
            read_csv(path)
        ):
            if path.name.endswith(".pending.csv") and (
                row.get("DecodeStatus") != "decoded_complete"
                or row.get("DecodeFullyConsumed", "").lower() != "true"
            ):
                continue
            row = dict(row)
            row.setdefault("RunSpeed", row.get("RunSpeedBase", ""))
            row.setdefault("ScfuFlags", row.get("Flags", ""))
            row.setdefault("ScfuFlags2", row.get("Flags2Numeric", ""))
            row["EvidenceCapture"] = capture
            row["EvidenceRowIndex"] = str(row_index)
            rows.append(row)
    return rows


def capture_identity_names(capture: str) -> dict[str, str]:
    identities: dict[str, str] = {}
    for row in load_raw_scfu_rows((capture,)):
        identity = row.get("Identity", "")
        name = row.get("Name", "")
        if identity and name:
            identities[identity] = name
    full_updates_path = CAPTURE_ROOT / capture / "enemy-full-updates.csv"
    if full_updates_path.exists():
        for row in read_csv(full_updates_path):
            identity = row.get("Identity", "")
            name = row.get("Name", "")
            if identity and name:
                identities[identity] = name
    lifecycle_path = CAPTURE_ROOT / capture / "npc-lifecycle.csv"
    if lifecycle_path.exists():
        for row in read_csv(lifecycle_path):
            identity = row.get("PrimaryIdentity", "")
            name = row.get("Name", "")
            if identity and name and not name.startswith("Remains of "):
                identities[identity] = name
    identities.update(CAPTURE_IDENTITY_NAME_OVERRIDES.get(capture, {}))
    return identities


def classify_spawn_candidate(row: dict[str, str]) -> str:
    if not row.get("Identity") or not row.get("Name"):
        return CANDIDATE_MALFORMED
    if row.get("Owner") or row["Name"] in OWNED_SUMMON_NAMES:
        return CANDIDATE_OWNED_SUMMON
    if row["Name"] in NAMED_BOSSES:
        return CANDIDATE_NAMED_BOSS
    if row["Name"] not in ARCHETYPES:
        return CANDIDATE_UNSUPPORTED
    if not capture_allows_archetype(row["EvidenceCapture"], row["Name"]):
        return CANDIDATE_UNSUPPORTED
    allowed_identities = ARCHETYPE_SPAWN_IDENTITY_FILTERS.get(row["Name"])
    if allowed_identities is not None and row["Identity"] not in allowed_identities:
        return CANDIDATE_UNSUPPORTED
    allowed_capture_identities = CAPTURE_SPAWN_IDENTITY_FILTERS.get(
        row["EvidenceCapture"]
    )
    if (
        allowed_capture_identities is not None
        and row["Identity"] not in allowed_capture_identities
    ):
        return CANDIDATE_UNSUPPORTED
    return CANDIDATE_ACCEPTED


def load_scfu_rows(captures: tuple[str, ...]) -> list[dict[str, str]]:
    return [
        row
        for row in load_raw_scfu_rows(captures)
        if row.get("Name") in ARCHETYPES
        and capture_allows_archetype(row["EvidenceCapture"], row["Name"])
    ]


def first_rows_by_identity(rows: list[dict[str, str]]) -> list[dict[str, str]]:
    result = {}
    for row in sorted(rows, key=stable_row_key):
        result.setdefault(row["Identity"], row)
    return list(result.values())


def select_spawns() -> list[dict[str, str]]:
    rows = first_rows_by_identity(
        [
            row
            for row in load_scfu_rows(SPAWN_CAPTURES)
            if classify_spawn_candidate(row) == CANDIDATE_ACCEPTED
        ]
    )
    selected: list[dict[str, str]] = []
    for row in sorted(rows, key=stable_row_key):
        position = canonical_position(row)
        duplicate = False
        for prior in selected:
            if prior["Name"] != row["Name"]:
                continue
            prior_position = canonical_position(prior)
            distance = math.sqrt(sum((a - b) ** 2 for a, b in zip(position, prior_position)))
            if distance <= 1.5:
                duplicate = True
                break
        if not duplicate:
            selected.append(row)
    return sorted(selected, key=lambda value: (value["Name"], value["Identity"]))


def source_weapon_evidence_profiles(
    spawns: list[dict[str, str]],
) -> dict[str, list[dict[str, object]]]:
    active_sources_by_name = {
        name: (
            set(expected)
            if name in SUPPORTED_SOURCE_WEAPON_MONSTER_DATA
            else {
                int(identity_hex(row["Identity"]), 16)
                for row in spawns
                if row["Name"] == name
            }
        )
        for name, expected in EXPECTED_SOURCE_WEAPON_EVIDENCE.items()
    }
    observations: dict[
        str, dict[int, dict[tuple[int, int, int], set[str]]]
    ] = {
        name: defaultdict(lambda: defaultdict(set))
        for name in EXPECTED_SOURCE_WEAPON_EVIDENCE
    }
    source_to_name = {
        source: name
        for name, sources in active_sources_by_name.items()
        for source in sources
    }

    for capture in CAPTURES:
        events_path = CAPTURE_ROOT / capture / "events.log"
        if not events_path.exists():
            continue
        with events_path.open("r", encoding="utf-8-sig", errors="replace") as stream:
            for line in stream:
                if "type=WeaponItemFullUpdate" not in line:
                    continue
                owner_match = WEAPON_OWNER_DETAIL.search(line)
                if owner_match is None:
                    continue
                source = int(owner_match.group("owner"), 16)
                name = source_to_name.get(source)
                if name is None:
                    continue
                quality_match = WEAPON_QUALITY_DETAIL.search(line)
                low_match = WEAPON_LOW_TEMPLATE_DETAIL.search(line)
                high_match = WEAPON_HIGH_TEMPLATE_DETAIL.search(line)
                if quality_match is None or low_match is None or high_match is None:
                    raise ValueError(
                        "source weapon evidence is incomplete for {0} 0x{1:08X} in {2}".format(
                            name, source, capture
                        )
                    )
                weapon = (
                    int(low_match.group("low")),
                    int(high_match.group("high")),
                    int(quality_match.group("quality")),
                )
                observations[name][source][weapon].add(capture)

    result: dict[str, list[dict[str, object]]] = {}
    for name, expected in EXPECTED_SOURCE_WEAPON_EVIDENCE.items():
        active_sources = active_sources_by_name[name]
        if active_sources != set(expected):
            missing = sorted(set(expected) - active_sources)
            unexpected = sorted(active_sources - set(expected))
            raise ValueError(
                "{0} active source weapon coverage drifted; missing={1} unexpected={2}".format(
                    name,
                    ["0x{0:08X}".format(value) for value in missing],
                    ["0x{0:08X}".format(value) for value in unexpected],
                )
            )
        observed_sources = observations[name]
        if set(observed_sources) != active_sources:
            missing = sorted(active_sources - set(observed_sources))
            unexpected = sorted(set(observed_sources) - active_sources)
            raise ValueError(
                "{0} captured source weapon coverage drifted; missing={1} unexpected={2}".format(
                    name,
                    ["0x{0:08X}".format(value) for value in missing],
                    ["0x{0:08X}".format(value) for value in unexpected],
                )
            )

        records: list[dict[str, object]] = []
        for source in sorted(active_sources):
            captured_weapons = observed_sources[source]
            if len(captured_weapons) != 1:
                raise ValueError(
                    "{0} source 0x{1:08X} has {2} conflicting weapon tuples: {3}".format(
                        name,
                        source,
                        len(captured_weapons),
                        sorted(captured_weapons),
                    )
                )
            weapon = next(iter(captured_weapons))
            if weapon != expected[source]:
                raise ValueError(
                    "{0} source 0x{1:08X} weapon tuple drifted; expected={2} captured={3}".format(
                        name, source, expected[source], weapon
                    )
                )
            records.append(
                {
                    "source": source,
                    "low": weapon[0],
                    "high": weapon[1],
                    "quality": weapon[2],
                    "captures": sorted(captured_weapons[weapon]),
                }
            )
        result[name] = records
    return result


def captured_weapon_tuples(capture: str, identity: str) -> set[tuple[int, int, int]]:
    captured_weapons = set()
    events_path = CAPTURE_ROOT / capture / "events.log"
    if events_path.exists():
        with events_path.open("r", encoding="utf-8-sig", errors="replace") as stream:
            for line in stream:
                if "type=WeaponItemFullUpdate" not in line:
                    continue
                owner_match = WEAPON_OWNER_DETAIL.search(line)
                if owner_match is None:
                    continue
                owner = "(SimpleChar:{0:08X})".format(
                    int(owner_match.group("owner"), 16)
                )
                if owner != identity:
                    continue
                quality_match = WEAPON_QUALITY_DETAIL.search(line)
                low_match = WEAPON_LOW_TEMPLATE_DETAIL.search(line)
                high_match = WEAPON_HIGH_TEMPLATE_DETAIL.search(line)
                if quality_match is None or low_match is None or high_match is None:
                    raise ValueError(
                        "captured weapon is incomplete capture={0} identity={1}".format(
                            capture, identity
                        )
                    )
                captured_weapons.add(
                    (
                        int(low_match.group("low")),
                        int(high_match.group("high")),
                        int(quality_match.group("quality")),
                    )
                )
        if captured_weapons:
            return captured_weapons

    raw_packets_path = CAPTURE_ROOT / capture / "raw-packets.csv"
    if not raw_packets_path.exists():
        raise ValueError(
            "captured weapon has no decoded events or raw packets capture={0} identity={1}".format(
                capture, identity
            )
        )

    identity_instance = int(identity[len("(SimpleChar:") : -1], 16)
    raw_weapon_rows = 0
    with raw_packets_path.open("r", encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            if row.get("N3TypeName") != "WeaponItemFullUpdate":
                continue
            raw_weapon_rows += 1
            packet = bytes.fromhex(row["RawHex"])
            packet_length = int(row["PacketLength"])
            if (row.get("PreservationStatus") != "raw_complete"
                or packet_length != len(packet)
                or int.from_bytes(packet[2:4], "big") != 10
                or int.from_bytes(packet[6:8], "big") != packet_length
                or int.from_bytes(packet[16:20], "big") != 0x3B1D2268
                or len(packet) < 63):
                raise ValueError(
                    "captured raw weapon envelope is invalid capture={0} identity={1}".format(
                        capture, identity
                    )
                )
            owner_type = int.from_bytes(packet[33:37], "big")
            owner_instance = int.from_bytes(packet[37:41], "big")
            if owner_type != 50000 or owner_instance != identity_instance:
                continue
            count_offset = 55
            encoded_count = int.from_bytes(packet[count_offset : count_offset + 4], "big")
            if encoded_count % 0x03F1 != 0:
                raise ValueError(
                    "captured raw weapon stat count is invalid capture={0} identity={1}".format(
                        capture, identity
                    )
                )
            stat_count = (encoded_count // 0x03F1) - 1
            stats_offset = count_offset + 4
            stats_end = stats_offset + (stat_count * 8)
            if stat_count <= 0 or stats_end + 4 != len(packet):
                raise ValueError(
                    "captured raw weapon stat array is invalid capture={0} identity={1}".format(
                        capture, identity
                    )
                )
            stat_rows = [
                (
                    int.from_bytes(packet[offset : offset + 4], "big"),
                    int.from_bytes(packet[offset + 4 : offset + 8], "big"),
                )
                for offset in range(stats_offset, stats_end, 8)
            ]
            required_stats = (701, 702, 703)
            if any(sum(1 for stat, _ in stat_rows if stat == required) != 1
                   for required in required_stats):
                raise ValueError(
                    "captured raw weapon required stats are missing capture={0} identity={1}".format(
                        capture, identity
                    )
                )
            stats = dict(stat_rows)
            captured_weapons.add((stats[702], stats[703], stats[701]))
    if not captured_weapons:
        raise ValueError(
            "captured raw weapon owner was not found capture={0} identity={1} weaponRows={2} ownerHex={3}".format(
                capture,
                identity,
                raw_weapon_rows,
                ((50000).to_bytes(4, "big")
                 + identity_instance.to_bytes(4, "big")).hex().upper(),
            )
        )
    return captured_weapons


def reviewed_atomic_generation_variants(
    name: str,
    monster_data: int,
    evidence_by_source: dict[int, tuple[tuple[str, str], ...]],
    expected_stats: dict[int, tuple[int, int, int, int]],
    expected_count: int,
    unique_patrol_source: int | None = None,
) -> list[dict[str, object]]:
    scfu_by_evidence: dict[tuple[str, str], dict[str, str]] = {}
    for canonical_source, evidence_pairs in sorted(
        evidence_by_source.items()
    ):
        for capture, identity in evidence_pairs:
            scfu_rows = [
                row
                for row in first_rows_by_identity(load_raw_scfu_rows((capture,)))
                if row.get("Identity") == identity
            ]
            if len(scfu_rows) != 1:
                raise ValueError(
                    "{0} atomic variant SCFU drifted capture={1} identity={2} rows={3}".format(
                        name, capture, identity, len(scfu_rows)
                    )
                )
            row = scfu_rows[0]
            if row.get("Name") != name or int(row["MonsterData"]) != monster_data:
                raise ValueError(
                    "{0} atomic variant identity changed capture={1} identity={2}".format(
                        name, capture, identity
                    )
                )
            scfu_by_evidence[(capture, identity)] = row

    canonical_positions: dict[int, tuple[float, float, float]] = {}
    canonical_patrol_sources = set()
    for canonical_source, evidence_pairs in sorted(
        evidence_by_source.items()
    ):
        capture, identity = evidence_pairs[0]
        if identity != "(SimpleChar:{0:08X})".format(canonical_source):
            raise ValueError(
                "{0} canonical source row drifted source=0x{1:08X} identity={2}".format(
                    name, canonical_source, identity
                )
            )
        row = scfu_by_evidence[(capture, identity)]
        canonical_positions[canonical_source] = (
            float(row["PositionX"]),
            float(row["PositionY"]),
            float(row["PositionZ"]),
        )
        if row.get("Waypoints", ""):
            canonical_patrol_sources.add(canonical_source)

    if unique_patrol_source is not None and canonical_patrol_sources != {
        unique_patrol_source
    }:
        raise ValueError(
            "{0} unique patrol source drifted expected=0x{1:08X} actual={2}".format(
                name,
                unique_patrol_source,
                ["0x{0:08X}".format(value) for value in sorted(canonical_patrol_sources)],
            )
        )

    for canonical_source, evidence_pairs in sorted(
        evidence_by_source.items()
    ):
        for capture, identity in evidence_pairs[1:]:
            row = scfu_by_evidence[(capture, identity)]
            association_points = [
                (
                    float(row["PositionX"]),
                    float(row["PositionY"]),
                    float(row["PositionZ"]),
                )
            ]
            for waypoint in row.get("Waypoints", "").split("|"):
                parts = waypoint.split(":")
                if len(parts) == 3 and all(parts):
                    association_points.append(tuple(float(value) for value in parts))
            distances = {
                source: min(
                    math.sqrt(
                        sum(
                            (point[index] - source_position[index]) ** 2
                            for index in range(3)
                        )
                    )
                    for point in association_points
                )
                for source, source_position in canonical_positions.items()
            }
            ordered = sorted(distances.items(), key=lambda value: (value[1], value[0]))
            intended_distance = distances[canonical_source]
            unique_close_position = (
                intended_distance <= 1.5
                and ordered[0][0] == canonical_source
                and (
                    len(ordered) == 1
                    or ordered[1][1] > intended_distance + 0.05
                )
            )
            unique_patrol_shape = (
                unique_patrol_source == canonical_source
                and bool(row.get("Waypoints", ""))
                and canonical_patrol_sources == {canonical_source}
            )
            if not unique_close_position and not unique_patrol_shape:
                raise ValueError(
                    "{0} source-anchor association is not unique capture={1} identity={2} intended=0x{3:08X} distance={4:.6f} nearest={5}".format(
                        name,
                        capture,
                        identity,
                        canonical_source,
                        intended_distance,
                        ordered[:2],
                    )
                )

    records = []
    for canonical_source, evidence_pairs in sorted(
        evidence_by_source.items()
    ):
        by_signature: dict[tuple[int, ...], dict[str, object]] = {}
        for capture, identity in evidence_pairs:
            row = scfu_by_evidence[(capture, identity)]

            captured_weapons = captured_weapon_tuples(capture, identity)
            if len(captured_weapons) != 1:
                raise ValueError(
                    "{0} atomic variant weapon drifted capture={1} identity={2} tuples={3}".format(
                        name, capture, identity, sorted(captured_weapons)
                    )
                )
            low, high, quality = next(iter(captured_weapons))
            signature = (
                int(row["Level"]),
                int(row["Health"]),
                int(row["HealthDamage"]),
                int(row["MonsterScale"]),
                int(row["RunSpeedBase"]),
                low,
                high,
                quality,
            )
            existing = by_signature.get(signature)
            if existing is None:
                existing = {
                    "monsterData": monster_data,
                    "source": canonical_source,
                    "level": signature[0],
                    "health": signature[1],
                    "healthDamage": signature[2],
                    "monsterScale": signature[3],
                    "runSpeed": signature[4],
                    "low": signature[5],
                    "high": signature[6],
                    "quality": signature[7],
                    "evidence": [],
                }
                by_signature[signature] = existing
            existing["evidence"].append(capture + ":" + identity)

        records.extend(
            sorted(
                by_signature.values(),
                key=lambda value: (
                    int(value["level"]),
                    int(value["quality"]),
                    int(value["low"]),
                    int(value["high"]),
                ),
            )
        )

    if len(records) != expected_count:
        raise ValueError(
            "{0} atomic variant count drifted expected={1} actual={2}".format(
                name, expected_count, len(records)
            )
        )
    if {int(value["source"]) for value in records} != set(
        evidence_by_source
    ):
        raise ValueError(name + " atomic variant source coverage drifted")
    for record in records:
        actual_stats = (
            int(record["health"]),
            int(record["healthDamage"]),
            int(record["monsterScale"]),
            int(record["runSpeed"]),
        )
        if expected_stats.get(int(record["level"])) != actual_stats:
            raise ValueError(
                "{0} atomic variant stat progression drifted source=0x{1:08X} level={2}".format(
                    name, int(record["source"]), int(record["level"])
                )
            )
    return records


def incomplete_rebuild_generation_variants() -> list[dict[str, object]]:
    return reviewed_atomic_generation_variants(
        "Incomplete Rebuild",
        203728,
        INCOMPLETE_REBUILD_GENERATION_EVIDENCE,
        {
            17: (368, 0, 98, 59),
            18: (394, 0, 98, 62),
            19: (421, 0, 98, 66),
            20: (447, 0, 99, 69),
            21: (474, 0, 99, 73),
            22: (500, 0, 99, 76),
        },
        23,
    )


def redundant_scan_generation_variants() -> list[dict[str, object]]:
    return reviewed_atomic_generation_variants(
        "Redundant Scan",
        204178,
        REDUNDANT_SCAN_GENERATION_EVIDENCE,
        {
            19: (736, 0, 98, 66),
            20: (782, 0, 99, 69),
            21: (829, 0, 99, 73),
            22: (875, 0, 99, 76),
        },
        10,
        REDUNDANT_SCAN_PATROL_SOURCE,
    )


def fragmented_soul_generation_variants() -> list[dict[str, object]]:
    return reviewed_atomic_generation_variants(
        "Fragmented Soul",
        203729,
        FRAGMENTED_SOUL_GENERATION_EVIDENCE,
        {
            17: (368, 0, 98, 59),
            18: (394, 0, 98, 62),
            19: (421, 0, 98, 66),
            20: (447, 0, 99, 69),
            21: (474, 0, 99, 73),
        },
        19,
        FRAGMENTED_SOUL_PATROL_SOURCE,
    )


def visual_profile(row: dict[str, str]) -> tuple:
    return (
        row["AppearanceValue"],
        row["Side"],
        row["Fatness"],
        row["Breed"],
        row["Gender"],
        row["Race"],
        row["MonsterData"],
        row["NpcFamily"],
        row["NpcLosHeight"],
        row["CharacterFlags"],
        row["AccountFlags"],
        row["Expansions"],
        row["VisualFlags"],
        row["VisibleTitle"],
        row["HeadMesh"],
        row["Textures"],
        row["Meshes"],
        row["TextureOverrides"],
    )


def select_archetype_profiles(spawns: list[dict[str, str]]) -> dict[str, dict[str, str]]:
    profiles = {}
    for name in ARCHETYPES:
        candidates = [row for row in spawns if row["Name"] == name]
        if not candidates:
            raise ValueError("ordinary archetype has no captured profile: " + name)
        counts = Counter(visual_profile(row) for row in candidates)
        selected_profile = min(
            counts,
            key=lambda profile: (
                -counts[profile],
                min(
                    stable_row_key(row)
                    for row in candidates
                    if visual_profile(row) == profile
                ),
                tuple(str(value) for value in profile),
            ),
        )
        profiles[name] = min(
            (row for row in candidates if visual_profile(row) == selected_profile),
            key=stable_row_key,
        )
    return profiles


def combat_profiles() -> dict[str, dict[str, object]]:
    raw_attacks = []
    uncontrollable_anger_cadence_rows = []
    for capture_index, capture in enumerate(CAPTURES):
        name_by_identity = capture_identity_names(capture)
        for row_index, row in enumerate(
            read_csv(CAPTURE_ROOT / capture / "enemy-combat.csv")
        ):
            source_identity = row["SourceIdentity"]
            name = name_by_identity.get(source_identity, "")
            detail = row.get("Detail", "")
            if (
                capture == "20260709-222339"
                and name == "Uncontrollable Anger"
                and source_identity == "(SimpleChar:79545202)"
                and row["MessageType"] == "AttackInfo"
                and row.get("TargetIdentity") == "(SimpleChar:7954523C)"
            ):
                cadence_match = COMBAT_ATTACK_DETAIL.search(detail)
                cadence_amount = int(row.get("Amount") or 0)
                if cadence_match is None or cadence_amount <= 0:
                    raise ValueError(
                        "Uncontrollable Anger reviewed cadence row is malformed"
                    )
                uncontrollable_anger_cadence_rows.append(
                    {
                        "capturedUtc": row["CapturedUtc"],
                        "amount": cadence_amount,
                        "slot": int(cadence_match.group("slot")),
                        "unknown": int(cadence_match.group("unknown")),
                        "instance": int(cadence_match.group("instance")),
                    }
                )
            if (
                row["MessageType"] != "AttackInfo"
                or row.get("SourceRole") != "enemy"
                or row.get("TargetRole") != "local-player"
                or name not in ARCHETYPES
                or not capture_allows_archetype(capture, name)
            ):
                continue
            if not row["Amount"].isdigit() or int(row["Amount"]) <= 0:
                continue
            match = COMBAT_ATTACK_DETAIL.search(detail)
            if not match:
                continue
            hit_type = match.group("hit_type").lower()
            if hit_type not in {"normal", "critical"}:
                continue
            target_identity = row.get("TargetIdentity", "")
            if not target_identity:
                target_match = COMBAT_TARGET_DETAIL.search(detail)
                target_identity = target_match.group("target") if target_match else ""
            raw_attacks.append(
                {
                    "name": name,
                    "capture": capture,
                    "identity": source_identity,
                    "target": target_identity,
                    "targetRole": row.get("TargetRole", ""),
                    "messageType": row["MessageType"],
                    "capturedUtc": row["CapturedUtc"],
                    "time": parse_time(row["CapturedUtc"]),
                    "amount": int(row["Amount"]),
                    "slot": int(match.group("slot")),
                    "unknown": int(match.group("unknown")),
                    "instance": int(match.group("instance")),
                    "hitType": hit_type,
                    "order": (capture_index, row_index),
                    "provenanceCaptures": {capture},
                }
            )

    distinct_attacks, exclusions = deduplicate_overlapping_combat_events(raw_attacks)
    validate_combat_overlap_dedup(raw_attacks, distinct_attacks, exclusions)
    uncontrollable_anger_recharge = reviewed_uncontrollable_anger_recharge(
        uncontrollable_anger_cadence_rows
    )
    attacks = defaultdict(list)
    for row in distinct_attacks:
        attacks[row["name"]].append(row)

    result = {}
    for name in ARCHETYPES:
        rows = attacks[name]
        normal_rows = [row for row in rows if row["hitType"] == "normal"]
        intervals = combat_intervals(rows)
        attack_contexts = Counter(
            (row["slot"], row["unknown"], row["instance"]) for row in rows
        )
        if attack_contexts:
            slot, unknown, instance = min(
                attack_contexts,
                key=lambda context: (
                    -attack_contexts[context],
                    min(row["order"] for row in rows if (
                        row["slot"], row["unknown"], row["instance"]
                    ) == context),
                    context,
                ),
            )
        else:
            slot, unknown, instance = (None, None, None)
        recharge = intervals[(len(intervals) - 1) // 2] if intervals else None
        if name == "Uncontrollable Anger":
            recharge = uncontrollable_anger_recharge
        result[name] = {
            "observed": bool(normal_rows),
            "min": min((row["amount"] for row in normal_rows), default=None),
            "max": max((row["amount"] for row in normal_rows), default=None),
            "recharge": recharge,
            "slot": slot,
            "unknown": unknown,
            "instance": instance,
            "rows": len(normal_rows),
        }
    return result


def validate_workman_striker_empty_inventory_evidence() -> None:
    if (
        len(WORKMAN_STRIKER_EMPTY_INVENTORY_GENERATIONS)
        != WORKMAN_STRIKER_STRICT_EMPTY_CORPSES
    ):
        raise ValueError("Workman Striker explicit empty evidence count drifted")
    event_lines = Counter(
        (
            CAPTURE_ROOT
            / WORKMAN_STRIKER_DUPLICATE_LOOT_CAPTURE
            / "events.log"
        ).read_text(encoding="utf-8").splitlines()
    )
    canonical_generations = read_csv(
        CAPTURE_ROOT
        / WORKMAN_STRIKER_CANONICAL_LOOT_CAPTURE
        / "corpse-full-updates.csv"
    )
    for event_line, captured_utc, corpse_identity, dead_npc_identity in (
        WORKMAN_STRIKER_EMPTY_INVENTORY_GENERATIONS
    ):
        if event_lines[event_line] != 1:
            raise ValueError(
                "Workman Striker explicit empty InventoryUpdate evidence drifted: "
                + corpse_identity
            )
        matching_generations = [
            row
            for row in canonical_generations
            if row.get("CapturedUtc") == captured_utc
            and row.get("CorpseIdentity") == corpse_identity
            and row.get("DeadNpcIdentity") == dead_npc_identity
            and row.get("CorpseMonsterData") == "203854"
        ]
        if len(matching_generations) != 1:
            raise ValueError(
                "Workman Striker explicit empty corpse generation drifted: "
                + dead_npc_identity
            )


def parse_reviewed_raw_inventory_update(line: str) -> dict[str, object] | None:
    match = RAW_INVENTORY_UPDATE_LINE.fullmatch(line)
    if match is None:
        return None
    packet = bytes.fromhex(match.group("hex"))
    declared_length = int(match.group("length"))
    if (
        len(packet) != declared_length
        or len(packet) < 57
        or int.from_bytes(packet[6:8], "big") != declared_length
        or packet[16:20] != bytes.fromhex("4E536976")
    ):
        raise ValueError(
            "Reviewed raw InventoryUpdate framing drifted: #"
            + match.group("sequence")
        )
    inventory_type = int.from_bytes(packet[-16:-12], "big")
    if inventory_type != 0x0000C76A:
        return None
    if (
        packet[28:37] != bytes.fromhex("010000001500000002")
        or (len(packet) - 57) % 32 != 0
        or int.from_bytes(packet[-4:], "big") != 1
    ):
        raise ValueError(
            "Reviewed corpse InventoryUpdate structure drifted: #"
            + match.group("sequence")
        )
    item_count = (len(packet) - 57) // 32
    if int.from_bytes(packet[37:41], "big") != 1009 * (item_count + 1):
        raise ValueError(
            "Reviewed corpse InventoryUpdate array token drifted: #"
            + match.group("sequence")
        )
    items = []
    slots = []
    for index in range(item_count):
        offset = 41 + (index * 32)
        placement = int.from_bytes(packet[offset : offset + 4], "big")
        slots.append(placement)
        items.append(
            (
                int.from_bytes(packet[offset + 16 : offset + 20], "big"),
                int.from_bytes(packet[offset + 20 : offset + 24], "big"),
                int.from_bytes(packet[offset + 24 : offset + 28], "big"),
            )
        )
    return {
        "capturedUtc": match.group("captured_utc"),
        "time": parse_time(match.group("captured_utc")),
        "sequence": int(match.group("sequence")),
        "corpseIdentity": f"(Corpse:{int.from_bytes(packet[-12:-8], 'big'):08X})",
        "items": tuple(items),
        "slots": tuple(slots),
    }


def reviewed_legacy_strict_open_generations() -> dict[str, list[dict[str, object]]]:
    reviewed = {}
    for name, definition in REVIEWED_LEGACY_STRICT_LOOT_DEFINITIONS.items():
        observed = []
        monster_data = int(definition["monster_data"])
        for capture in definition["captures"]:
            capture_path = CAPTURE_ROOT / capture
            corpse_rows = []
            for row in read_csv(capture_path / "corpse-full-updates.csv"):
                captured_utc = row.get("CapturedUtc", "")
                corpse_identity = normalize_identity(row.get("CorpseIdentity", ""))
                dead_npc_identity = normalize_identity(row.get("DeadNpcIdentity", ""))
                if not captured_utc or not corpse_identity or not dead_npc_identity:
                    continue
                corpse_rows.append(
                    {
                        "capturedUtc": captured_utc,
                        "time": parse_time(captured_utc),
                        "corpseIdentity": corpse_identity,
                        "deadNpcIdentity": dead_npc_identity,
                        "name": row.get("CorpseName", "").removeprefix("Remains of "),
                        "monsterData": int(row.get("CorpseMonsterData", "0") or 0),
                    }
                )
            corpse_rows_by_identity = defaultdict(list)
            for row in corpse_rows:
                corpse_rows_by_identity[row["corpseIdentity"]].append(row)
            for rows in corpse_rows_by_identity.values():
                rows.sort(key=lambda value: value["time"])

            raw_updates = []
            packet_path = capture_path / "packets.hex.log"
            if not packet_path.exists():
                raise ValueError(
                    f"Reviewed legacy strict-open raw sink is missing: {capture}"
                )
            for line in packet_path.read_text(
                encoding="utf-8-sig", errors="strict"
            ).splitlines():
                update = parse_reviewed_raw_inventory_update(line)
                if update is not None:
                    raw_updates.append(update)
            raw_updates.sort(key=lambda value: (value["time"], value["sequence"]))

            first_update_by_generation = {}
            for update in raw_updates:
                candidates = [
                    row
                    for row in corpse_rows_by_identity.get(
                        update["corpseIdentity"], ()
                    )
                    if row["time"] <= update["time"]
                ]
                if not candidates:
                    continue
                generation = candidates[-1]
                generation_key = (
                    generation["capturedUtc"],
                    generation["corpseIdentity"],
                    generation["deadNpcIdentity"],
                )
                if generation_key in first_update_by_generation:
                    continue
                first_update_by_generation[generation_key] = update
                if generation["name"] != name:
                    continue
                if generation["monsterData"] != monster_data:
                    raise ValueError(
                        f"Reviewed legacy strict-open monster data drifted: {name} {generation_key}"
                    )
                observed.append(
                    {
                        "capture": capture,
                        "cfuCapturedUtc": generation["capturedUtc"],
                        "corpseIdentity": generation["corpseIdentity"],
                        "deadNpcIdentity": generation["deadNpcIdentity"],
                        "capturedUtc": update["capturedUtc"],
                        "sequence": update["sequence"],
                        "monsterData": monster_data,
                        "items": update["items"],
                        "slots": update["slots"],
                    }
                )

        actual_fingerprints = sorted(
            (
                row["capture"],
                row["cfuCapturedUtc"],
                row["corpseIdentity"],
                row["deadNpcIdentity"],
                row["capturedUtc"],
                row["sequence"],
                row["items"],
            )
            for row in observed
        )
        if "generations" in definition:
            generations_match = Counter(actual_fingerprints) == Counter(
                definition["generations"]
            )
        else:
            actual_digest = hashlib.sha256(
                json.dumps(
                    actual_fingerprints,
                    ensure_ascii=True,
                    separators=(",", ":"),
                ).encode("utf-8")
            ).hexdigest()
            generations_match = actual_digest == definition["generation_digest"]
        if not generations_match:
            raise ValueError(
                f"Reviewed legacy strict-open generation evidence drifted: {name}"
            )
        expected_capture_allocations = definition.get("capture_allocations")
        if (
            expected_capture_allocations is not None
            and Counter(row["capture"] for row in observed)
            != expected_capture_allocations
        ):
            raise ValueError(
                f"Reviewed legacy strict-open capture allocation drifted: {name}"
            )
        positive = sum(bool(row["items"]) for row in observed)
        empty = len(observed) - positive
        if (
            len(observed) != int(definition["opened"])
            or positive != int(definition["positive"])
            or empty != int(definition["empty"])
        ):
            raise ValueError(
                f"Reviewed legacy strict-open denominator drifted: {name}"
            )
        item_counts = Counter(
            item for row in observed for item in row["items"]
        )
        if item_counts != definition["item_counts"]:
            raise ValueError(
                f"Reviewed legacy strict-open item membership drifted: {name}"
            )
        reviewed[name] = sorted(
            observed,
            key=lambda value: (
                value["capturedUtc"],
                value["corpseIdentity"],
                value["sequence"],
            ),
        )
    return reviewed


def loot_profiles() -> dict[str, list[dict[str, int]]]:
    """Return only observations with a proven complete-inventory denominator.

    corpse-loot-observations.csv explicitly marks the initial snapshot and its
    death/corpse linkage.  Older inventory-updates.csv rows can still prove
    that an item was present, but cannot prove how many empty corpses were
    observed.  Those membership-only outcomes are kept separately by
    loot_outcome_profiles() and must never become runtime drop odds here.
    """
    mapped = defaultdict(list)
    opened_by_name = defaultdict(list)
    for capture in CAPTURES:
        strict_observations_path = CAPTURE_ROOT / capture / "corpse-loot-observations.csv"
        if not strict_observations_path.exists():
            continue
        for row in read_csv(strict_observations_path):
            name = row.get("EnemyName", "")
            if (
                row.get("InitialSnapshot", "").lower() != "true"
                or not row.get("CorrelationStatus", "").startswith("linked-")
                or name not in ARCHETYPES
                or name in REVIEWED_LEGACY_STRICT_LOOT_DEFINITIONS
                or not capture_allows_archetype(capture, name)
            ):
                continue
            items = []
            for item in row.get("Items", "").split(";"):
                if not item:
                    continue
                low, high, quality, count = (int(value) for value in item.split(":"))
                items.extend([(low, high, quality)] * count)
            opened_by_name[name].append(items)

    reviewed_legacy = reviewed_legacy_strict_open_generations()
    for name, generations in reviewed_legacy.items():
        if opened_by_name.get(name):
            raise ValueError(
                f"{name} gained direct strict snapshots; reconcile them with the reviewed legacy denominator"
            )
        opened_by_name[name] = [list(row["items"]) for row in generations]

    if opened_by_name.get("Workman Striker"):
        raise ValueError(
            "Workman Striker gained direct strict snapshots; reconcile them with the audited legacy denominator"
        )
    workman_outcomes = [
        row
        for row in loot_outcome_profiles().get("Workman Striker", [])
        if row["capture"] in WORKMAN_STRIKER_STRICT_LOOT_CAPTURES
    ]
    workman_by_corpse = defaultdict(list)
    for row in workman_outcomes:
        workman_by_corpse[
            (
                row["capture"],
                row["capturedUtc"],
                row["corpseIdentity"],
                row["deadNpcIdentity"],
            )
        ].append((row["low"], row["high"], row["quality"]))
    if len(workman_by_corpse) != WORKMAN_STRIKER_STRICT_POSITIVE_CORPSES:
        raise ValueError("Workman Striker positive complete-open count drifted")
    if Counter(key[0] for key in workman_by_corpse) != Counter(
        {"20260709-212336": 2, "20260709-220439": 6}
    ):
        raise ValueError("Workman Striker complete-open capture allocation drifted")
    workman_item_counts = Counter(
        item for items in workman_by_corpse.values() for item in items
    )
    if workman_item_counts != WORKMAN_STRIKER_STRICT_ITEM_COUNTS:
        raise ValueError("Workman Striker strict item membership drifted")
    if (
        WORKMAN_STRIKER_STRICT_POSITIVE_CORPSES
        + WORKMAN_STRIKER_STRICT_EMPTY_CORPSES
        != WORKMAN_STRIKER_STRICT_OPENED_CORPSES
    ):
        raise ValueError("Workman Striker strict denominator is internally inconsistent")
    validate_workman_striker_empty_inventory_evidence()
    opened_by_name["Workman Striker"] = list(workman_by_corpse.values()) + [
        [] for _ in range(WORKMAN_STRIKER_STRICT_EMPTY_CORPSES)
    ]

    for name, corpse_items in opened_by_name.items():
        opened_corpses = len(corpse_items)
        counts = Counter(item for items in corpse_items for item in items)
        for (low, high, quality), count in sorted(counts.items()):
            mapped[name].append(
                {
                    "low": low,
                    "high": high,
                    "quality": quality,
                    "count": count,
                    "corpses": opened_corpses,
                    "basis": min(10000, int(round(count * 10000.0 / opened_corpses))),
                }
            )
    return mapped


def strict_loot_profile_summaries(
    loot: dict[str, list[dict[str, int]]],
) -> list[dict[str, object]]:
    summaries = []
    for name, definition in REVIEWED_LEGACY_STRICT_LOOT_DEFINITIONS.items():
        opened = int(definition["opened"])
        entries = loot.get(name, [])
        if not entries or any(int(entry["corpses"]) != opened for entry in entries):
            raise ValueError(f"Reviewed strict-loot generated entries drifted: {name}")
        summaries.append(
            {
                "name": name,
                "monsterData": int(definition["monster_data"]),
                "opened": opened,
                "positive": int(definition["positive"]),
                "empty": int(definition["empty"]),
                "itemPoolComplete": False,
                "captures": tuple(definition["captures"]),
                "entries": entries,
            }
        )

    workman_entries = loot.get("Workman Striker", [])
    if not workman_entries or any(
        int(entry["corpses"]) != WORKMAN_STRIKER_STRICT_OPENED_CORPSES
        for entry in workman_entries
    ):
        raise ValueError("Workman Striker strict-loot generated entries drifted")
    summaries.append(
        {
            "name": "Workman Striker",
            "monsterData": 203854,
            "opened": WORKMAN_STRIKER_STRICT_OPENED_CORPSES,
            "positive": WORKMAN_STRIKER_STRICT_POSITIVE_CORPSES,
            "empty": WORKMAN_STRIKER_STRICT_EMPTY_CORPSES,
            "itemPoolComplete": False,
            "captures": tuple(sorted(WORKMAN_STRIKER_STRICT_LOOT_CAPTURES)),
            "entries": workman_entries,
        }
    )
    if len({int(summary["monsterData"]) for summary in summaries}) != len(summaries):
        raise ValueError("Reviewed strict-loot MonsterData keys are not unique")
    return sorted(summaries, key=lambda value: (int(value["monsterData"]), value["name"]))


def normalize_identity(value: str) -> str:
    match = re.fullmatch(
        r"\(?(?P<type>[A-Za-z]+):(?:0x)?(?P<instance>[0-9A-Fa-f]+)\)?",
        (value or "").strip(),
    )
    if not match:
        return ""
    return f"({match.group('type')}:{int(match.group('instance'), 16):08X})"


def corpse_row_name(row: dict[str, str]) -> str:
    name = row.get("DeadNpcName", "")
    if name:
        return name
    name = row.get("CorpseName", "")
    if name.startswith("Remains of "):
        return name[len("Remains of ") :]
    try:
        monster_data = int(row.get("CorpseMonsterData", ""))
    except ValueError:
        return ""
    return next(
        (
            candidate
            for candidate, expected_monster_data in EXPECTED_CORPSE_MONSTER_DATA.items()
            if expected_monster_data == monster_data
        ),
        "",
    )


def loot_outcome_fingerprint(value: dict[str, object]) -> tuple:
    return (
        value["corpseIdentity"],
        value["deadNpcIdentity"],
        value["monsterData"],
        value["slot"],
        value["low"],
        value["high"],
        value["quality"],
    )


def loot_outcome_profiles() -> dict[str, list[dict[str, object]]]:
    """Recover exact item-bearing snapshots without inventing probabilities.

    A corpse identity can be reused and an opened corpse can emit multiple
    inventory updates.  Each CFU is therefore linked to the most recent death
    of DeadNpcIdentity, bounded by the next CFU generation of that normalized
    corpse identity, and represented by only its earliest inventory timestamp
    group.  The result proves item membership for that observed outcome only.
    """
    mapped = defaultdict(list)
    for capture in CAPTURES:
        enemy_state_path = CAPTURE_ROOT / capture / "enemy-state.csv"
        corpse_updates_path = CAPTURE_ROOT / capture / "corpse-full-updates.csv"
        inventory_updates_path = CAPTURE_ROOT / capture / "inventory-updates.csv"
        if (
            not enemy_state_path.exists()
            or not corpse_updates_path.exists()
            or not inventory_updates_path.exists()
        ):
            continue
        deaths_by_identity = defaultdict(list)
        for row in read_csv(enemy_state_path):
            if str(row.get("eventType") or "").lower() != "death":
                continue
            identity = normalize_identity(row.get("entityId", ""))
            timestamp_text = row.get("timestamp", "")
            if not identity or not timestamp_text:
                continue
            deaths_by_identity[identity].append(parse_time(timestamp_text))
        for timestamps in deaths_by_identity.values():
            timestamps.sort()

        generations_by_death = {}
        for row in read_csv(corpse_updates_path):
            captured_utc = row.get("CapturedUtc", "")
            corpse_identity = normalize_identity(row.get("CorpseIdentity", ""))
            dead_npc_identity = normalize_identity(row.get("DeadNpcIdentity", ""))
            name = corpse_row_name(row)
            try:
                monster_data = int(row.get("CorpseMonsterData", ""))
            except ValueError:
                continue
            if (
                not captured_utc
                or not corpse_identity
                or not dead_npc_identity
                or name == "Killer"
                or name not in CORPSE_EVIDENCE_NAMES
                or (
                    name in ARCHETYPES
                    and not capture_allows_archetype(capture, name)
                )
            ):
                continue
            cfu_time = parse_time(captured_utc)
            matching_deaths = [
                death_time
                for death_time in deaths_by_identity.get(dead_npc_identity, ())
                if death_time <= cfu_time
            ]
            if not matching_deaths:
                continue
            death_time = matching_deaths[-1]
            generation_key = (dead_npc_identity, death_time)
            candidate = {
                "capture": capture,
                "cfuTime": cfu_time,
                "corpseIdentity": corpse_identity,
                "deadNpcIdentity": dead_npc_identity,
                "name": name,
                "monsterData": monster_data,
            }
            current = generations_by_death.get(generation_key)
            if current is None or cfu_time < current["cfuTime"]:
                generations_by_death[generation_key] = candidate

        generations = sorted(
            generations_by_death.values(),
            key=lambda value: (
                value["cfuTime"],
                value["corpseIdentity"],
                value["deadNpcIdentity"],
            ),
        )
        next_generation_time = {}
        by_corpse_identity = defaultdict(list)
        for generation in generations:
            by_corpse_identity[generation["corpseIdentity"]].append(generation)
        for same_identity_generations in by_corpse_identity.values():
            for index, generation in enumerate(same_identity_generations[:-1]):
                next_generation_time[id(generation)] = same_identity_generations[index + 1]["cfuTime"]

        inventory_by_identity = defaultdict(list)
        for row in read_csv(inventory_updates_path):
            inventory_identity = normalize_identity(row.get("InventoryIdentity", ""))
            captured_utc = row.get("CapturedUtc", "")
            if not inventory_identity.startswith("(Corpse:") or not captured_utc:
                continue
            inventory_by_identity[inventory_identity].append(
                (parse_time(captured_utc), row)
            )

        for generation in generations:
            end_time = next_generation_time.get(id(generation))
            candidates = [
                (timestamp, row)
                for timestamp, row in inventory_by_identity.get(
                    generation["corpseIdentity"], ()
                )
                if timestamp >= generation["cfuTime"]
                and (end_time is None or timestamp < end_time)
            ]
            if not candidates:
                continue
            first_inventory_time = min(timestamp for timestamp, _ in candidates)
            for timestamp, row in candidates:
                if timestamp != first_inventory_time:
                    continue
                try:
                    low = int(row.get("LowId", ""))
                    high = int(row.get("HighId", ""))
                    quality = int(row.get("Quality", ""))
                    sequence = int(row.get("Sequence", ""))
                    slot = int(row.get("Slot", ""))
                except ValueError:
                    continue
                mapped[generation["name"]].append(
                    {
                        "capture": capture,
                        "capturedUtc": timestamp.isoformat().replace("+00:00", "Z"),
                        "corpseIdentity": generation["corpseIdentity"],
                        "deadNpcIdentity": generation["deadNpcIdentity"],
                        "monsterData": generation["monsterData"],
                        "sequence": sequence,
                        "slot": slot,
                        "low": low,
                        "high": high,
                        "quality": quality,
                    }
                )

    workman_records = mapped.get("Workman Striker", [])
    canonical_fingerprints = {
        loot_outcome_fingerprint(record)
        for record in workman_records
        if record["capture"] == WORKMAN_STRIKER_CANONICAL_LOOT_CAPTURE
    }
    duplicate_records = [
        record
        for record in workman_records
        if record["capture"] == WORKMAN_STRIKER_DUPLICATE_LOOT_CAPTURE
        and loot_outcome_fingerprint(record) in canonical_fingerprints
    ]
    if len(duplicate_records) != WORKMAN_STRIKER_DUPLICATE_LOOT_ROWS:
        raise ValueError("Workman Striker overlapping legacy loot rows drifted")
    duplicate_record_ids = {id(record) for record in duplicate_records}
    mapped["Workman Striker"] = [
        record for record in workman_records if id(record) not in duplicate_record_ids
    ]

    for name, definition in REVIEWED_LEGACY_STRICT_LOOT_DEFINITIONS.items():
        overlap = definition["overlap"]
        if overlap is None:
            continue
        duplicate_capture, canonical_capture, expected_duplicate_rows = overlap
        records = mapped.get(name, [])
        canonical_fingerprints = {
            loot_outcome_fingerprint(record)
            for record in records
            if record["capture"] == canonical_capture
        }
        duplicate_records = [
            record
            for record in records
            if record["capture"] == duplicate_capture
            and loot_outcome_fingerprint(record) in canonical_fingerprints
        ]
        if len(duplicate_records) != expected_duplicate_rows:
            raise ValueError(
                f"{name} overlapping legacy loot rows drifted"
            )
        duplicate_ids = {id(record) for record in duplicate_records}
        mapped[name] = [
            record for record in records if id(record) not in duplicate_ids
        ]

    reviewed_legacy = reviewed_legacy_strict_open_generations()
    for name, generations in reviewed_legacy.items():
        records = mapped[name]
        semantic_keys = Counter(
            (
                record["capture"],
                record["corpseIdentity"],
                record["deadNpcIdentity"],
                record["monsterData"],
                record["slot"],
                record["low"],
                record["high"],
                record["quality"],
            )
            for record in records
        )
        for generation in generations:
            for slot, (low, high, quality) in zip(
                generation["slots"], generation["items"]
            ):
                semantic_key = (
                    generation["capture"],
                    generation["corpseIdentity"],
                    generation["deadNpcIdentity"],
                    generation["monsterData"],
                    slot,
                    low,
                    high,
                    quality,
                )
                if semantic_keys[semantic_key] > 1:
                    raise ValueError(
                        f"{name} legacy loot outcome is duplicated: {semantic_key}"
                    )
                if semantic_keys[semantic_key] == 1:
                    continue
                records.append(
                    {
                        "capture": generation["capture"],
                        "capturedUtc": generation["capturedUtc"],
                        "corpseIdentity": generation["corpseIdentity"],
                        "deadNpcIdentity": generation["deadNpcIdentity"],
                        "monsterData": generation["monsterData"],
                        "sequence": generation["sequence"],
                        "slot": slot,
                        "low": low,
                        "high": high,
                        "quality": quality,
                    }
                )
                semantic_keys[semantic_key] += 1

    for records in mapped.values():
        records.sort(
            key=lambda value: (
                value["capturedUtc"],
                value["corpseIdentity"],
                value["sequence"],
                value["slot"],
            )
        )
    return mapped


def corpse_profiles() -> dict[str, list[dict[str, object]]]:
    mapped = defaultdict(list)
    for capture, allowed_names in CAPTURE_CORPSE_EVIDENCE_FILTERS.items():
        allowed_dead_identities = CAPTURE_CORPSE_IDENTITY_FILTERS.get(capture)
        death_levels = {}
        for row in read_csv(CAPTURE_ROOT / capture / "enemy-state.csv"):
            if str(row.get("eventType") or "").lower() != "death":
                continue
            dead_identity = row.get("entityId", "")
            if dead_identity.startswith("SimpleChar:"):
                dead_identity = f"({dead_identity})"
            try:
                enemy_level = int(row.get("level", ""))
            except ValueError:
                continue
            if dead_identity and enemy_level > 0:
                death_levels[dead_identity] = enemy_level

        lifecycle_dead_identities = CAPTURE_LIFECYCLE_DEATH_LEVEL_FILTERS.get(capture)
        if lifecycle_dead_identities is not None:
            lifecycle_levels = {}
            lifecycle_deaths = set()
            for row in read_csv(CAPTURE_ROOT / capture / "npc-lifecycle.csv"):
                identity = row.get("PrimaryIdentity", "")
                if identity.startswith("SimpleChar:"):
                    identity = f"({identity})"
                if identity not in lifecycle_dead_identities:
                    continue
                phase = str(row.get("Phase") or "").lower()
                if phase == "character-seen":
                    match = re.search(r"\blevel=(\d+)\b", row.get("Detail", ""), re.IGNORECASE)
                    if match is None:
                        continue
                    level = int(match.group(1))
                    if level <= 0:
                        continue
                    previous = lifecycle_levels.get(identity)
                    if previous is not None and previous != level:
                        raise ValueError(
                            "lifecycle level evidence conflicted capture={0} identity={1} levels={2},{3}".format(
                                capture, identity, previous, level
                            )
                        )
                    lifecycle_levels[identity] = level
                elif phase == "death-action":
                    lifecycle_deaths.add(identity)

            for identity in lifecycle_dead_identities:
                if identity not in lifecycle_levels or identity not in lifecycle_deaths:
                    raise ValueError(
                        "lifecycle death-level evidence drifted capture={0} identity={1} level={2} death={3}".format(
                            capture,
                            identity,
                            lifecycle_levels.get(identity),
                            identity in lifecycle_deaths,
                        )
                    )
                existing_level = death_levels.get(identity)
                if existing_level is not None and existing_level != lifecycle_levels[identity]:
                    raise ValueError(
                        "enemy-state and lifecycle levels conflicted capture={0} identity={1} levels={2},{3}".format(
                            capture, identity, existing_level, lifecycle_levels[identity]
                        )
                    )
                death_levels[identity] = lifecycle_levels[identity]

        scfu_levels = SCFU_CORPSE_LEVEL_EVIDENCE.get(capture, {})
        if scfu_levels:
            scfu_rows = read_csv(CAPTURE_ROOT / capture / "scfu-appearance.csv")
            for identity, expected in scfu_levels.items():
                matching = [row for row in scfu_rows if row.get("Identity") == identity]
                expected_shape = (
                    expected["name"],
                    int(expected["monster_data"]),
                    int(expected["level"]),
                    int(expected["health"]),
                )
                observed_shapes = set()
                for row in matching:
                    if (
                        row.get("DecodeStatus") != "decoded_complete"
                        or row.get("DecodeFullyConsumed", "").lower() != "true"
                    ):
                        continue
                    try:
                        observed_shapes.add(
                            (
                                row.get("Name", ""),
                                int(row.get("MonsterData", "")),
                                int(row.get("Level", "")),
                                int(row.get("Health", "")),
                            )
                        )
                    except ValueError:
                        continue
                if observed_shapes != {expected_shape}:
                    raise ValueError(
                        "SCFU corpse level evidence drifted capture={0} identity={1} shapes={2}".format(
                            capture, identity, sorted(observed_shapes)
                        )
                    )
                existing_level = death_levels.get(identity)
                if existing_level is not None and existing_level != int(expected["level"]):
                    raise ValueError(
                        "SCFU corpse level conflicted capture={0} identity={1} levels={2},{3}".format(
                            capture, identity, existing_level, expected["level"]
                        )
                    )
                death_levels[identity] = int(expected["level"])

        cross_session_levels = CROSS_SESSION_CORPSE_LEVEL_EVIDENCE.get(capture, {})
        for identity, expected in cross_session_levels.items():
            dossier_path = (
                CAPTURE_ROOT / str(expected["dossier_capture"]) / "enemy-dossier.json"
            )
            with dossier_path.open("r", encoding="utf-8-sig") as stream:
                dossier = json.load(stream)
            matching = [
                row
                for row in dossier.get("enemies", [])
                if row.get("identity") == identity
            ]
            if len(matching) != 1:
                raise ValueError(
                    "cross-session corpse dossier identity drifted capture={0} identity={1} rows={2}".format(
                        expected["dossier_capture"], identity, len(matching)
                    )
                )
            row = matching[0]
            if (
                row.get("name") != expected["name"]
                or row.get("monsterData") != expected["monster_data"]
                or int(row.get("level", 0)) != int(expected["level"])
                or int(row.get("currentHealth", 0)) != int(expected["health"])
                or int(row.get("maxHealth", 0)) != int(expected["health"])
            ):
                raise ValueError(
                    "cross-session corpse dossier evidence drifted capture={0} identity={1}".format(
                        expected["dossier_capture"], identity
                    )
                )
            existing_level = death_levels.get(identity)
            if existing_level is not None and existing_level != int(expected["level"]):
                raise ValueError(
                    "cross-session corpse level conflicted capture={0} identity={1} levels={2},{3}".format(
                        capture, identity, existing_level, expected["level"]
                    )
                )
            death_levels[identity] = int(expected["level"])

        seen_dead_identities = set()
        selected_dead_identities = set()
        for row in read_csv(CAPTURE_ROOT / capture / "corpse-full-updates.csv"):
            dead_identity = row.get("DeadNpcIdentity", "")
            if dead_identity.startswith("SimpleChar:"):
                dead_identity = f"({dead_identity})"
            corpse_identity = row.get("CorpseIdentity", "")
            if corpse_identity.startswith("Corpse:"):
                corpse_identity = f"({corpse_identity})"
            if (
                not dead_identity
                or not corpse_identity
                or dead_identity not in death_levels
                or (
                    allowed_dead_identities is not None
                    and dead_identity not in allowed_dead_identities
                )
            ):
                continue
            # A corpse may emit another full update after credits are claimed.
            # Only the earliest generation row for the captured death can
            # describe its initial credit outcome.
            if dead_identity in seen_dead_identities:
                continue
            seen_dead_identities.add(dead_identity)
            try:
                monster_data = int(row.get("CorpseMonsterData", ""))
                cat_mesh = int(row.get("CorpseCatMesh", ""))
                credits = int(row.get("CorpseCredits", ""))
            except ValueError:
                continue
            corpse_name = row.get("CorpseName", "")
            if corpse_name.startswith("Remains of "):
                corpse_name = corpse_name[len("Remains of ") :]
            name = (
                row.get("DeadNpcName", "")
                or corpse_name
                or SUPPORTED_CORPSE_NAMES_BY_MONSTER_DATA.get(monster_data, "")
            )
            if name not in allowed_names or name not in CORPSE_EVIDENCE_NAMES:
                continue
            # A later full update for an already-opened corpse can carry zero
            # credits after the player has claimed them.  It proves the corpse
            # shape, but it is not the initial credit outcome and must not
            # create a zero-credit runtime rule.
            if monster_data <= 0 or cat_mesh <= 0 or credits <= 0:
                continue
            mapped[name].append(
                {
                    "capture": capture,
                    "capturedUtc": row.get("CapturedUtc", ""),
                    "corpseIdentity": corpse_identity,
                    "deadNpcIdentity": dead_identity,
                    "enemyLevel": death_levels[dead_identity],
                    "monsterData": monster_data,
                    "catMesh": cat_mesh,
                    "credits": credits,
                }
            )
            selected_dead_identities.add(dead_identity)

        if (
            allowed_dead_identities is not None
            and selected_dead_identities != allowed_dead_identities
        ):
            raise ValueError(
                "corpse identity allowlist drifted capture={0} missing={1} unexpected={2}".format(
                    capture,
                    ",".join(sorted(allowed_dead_identities - selected_dead_identities)),
                    ",".join(sorted(selected_dead_identities - allowed_dead_identities)),
                )
            )

    for records in mapped.values():
        records.sort(
            key=lambda value: (
                value["capturedUtc"],
                value["corpseIdentity"],
                value["deadNpcIdentity"],
            )
        )
    return mapped


def cs_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def cs_float(value: str | float) -> str:
    text = str(value)
    if "e" in text.lower():
        text = format(float(text), ".9f").rstrip("0").rstrip(".")
    if "." not in text:
        text += ".0"
    return text + "f"


def emit_array(items: list[str], indent: str) -> str:
    if not items:
        return "new " + ("CapturedSubwayTextureDefinition[0]" if "Texture" in indent else "object[0]")
    return "\n".join(items)


def compatibility_int(value: int | None) -> int:
    # The checked-in provider's legacy constructor has non-nullable value fields.
    # Keep unresolved evidence as None in the generator model and translate only
    # at this compatibility serialization boundary.
    return 0 if value is None else value


def compatibility_float(value: float | None) -> float:
    return 0.0 if value is None else value


def corpse_definition(item: dict[str, object]) -> str:
    return (
        "new CapturedSubwayCorpseEvidenceDefinition("
        f"{cs_string(str(item['capture']))}, "
        f"{cs_string(str(item['capturedUtc']))}, "
        f"{cs_string(str(item['corpseIdentity']))}, "
        f"{cs_string(str(item['deadNpcIdentity']))}, "
        f"{int(item['enemyLevel'])}, {int(item['monsterData'])}, "
        f"{int(item['catMesh'])}, {int(item['credits'])})"
    )


def loot_outcome_definition(item: dict[str, object]) -> str:
    return (
        "new CapturedSubwayLootOutcomeEvidenceDefinition("
        f"{cs_string(str(item['capture']))}, "
        f"{cs_string(str(item['capturedUtc']))}, "
        f"{cs_string(str(item['corpseIdentity']))}, "
        f"{cs_string(str(item['deadNpcIdentity']))}, "
        f"{int(item['monsterData'])}, "
        f"{int(item['sequence'])}, {int(item['slot'])}, "
        f"{int(item['low'])}, {int(item['high'])}, {int(item['quality'])})"
    )


def strict_loot_profile_definition(
    summary: dict[str, object],
    trailing_comma: bool,
) -> list[str]:
    entries = summary["entries"]
    captures = summary["captures"]
    lines = [
        "            new CapturedSubwayStrictLootProfileDefinition(",
        f"                {cs_string(str(summary['name']))},",
        f"                {int(summary['monsterData'])},",
        f"                {int(summary['opened'])},",
        f"                {int(summary['positive'])},",
        f"                {int(summary['empty'])},",
        f"                {str(bool(summary['itemPoolComplete'])).lower()},",
        "                new string[]",
        "                {",
    ]
    for index, capture in enumerate(captures):
        lines.append(
            "                    "
            + cs_string(str(capture))
            + ("," if index < len(captures) - 1 else "")
        )
    lines.extend(
        [
            "                },",
            "                new CapturedSubwayLootEvidenceDefinition[]",
            "                {",
        ]
    )
    for index, item in enumerate(entries):
        lines.append(
            "                    new CapturedSubwayLootEvidenceDefinition("
            f"{item['low']}, {item['high']}, {item['quality']}, "
            f"{item['count']}, {item['corpses']}, {item['basis']})"
            + ("," if index < len(entries) - 1 else "")
        )
    lines.extend(
        [
            "                })" + ("," if trailing_comma else ""),
        ]
    )
    return lines


def validate_content(
    spawns: list[dict[str, str]],
    profiles: dict[str, dict[str, str]],
    combat: dict[str, dict[str, object]],
    corpses: dict[str, list[dict[str, object]]],
) -> None:
    profile_keys = [key for key, _ in ARCHETYPES.values()]
    duplicate_profile_keys = sorted(
        key for key, count in Counter(profile_keys).items() if count > 1
    )
    if duplicate_profile_keys:
        raise ValueError(
            "duplicate ordinary profile keys: " + ", ".join(duplicate_profile_keys)
        )

    expected_names = set(ARCHETYPES)
    if set(profiles) != expected_names:
        missing = sorted(expected_names - set(profiles))
        unexpected = sorted(set(profiles) - expected_names)
        raise ValueError(
            "ordinary profile set mismatch missing="
            + ",".join(missing)
            + " unexpected="
            + ",".join(unexpected)
        )
    generated_profile_keys = {ARCHETYPES[name][0] for name in profiles}

    identities = [row["Identity"] for row in spawns]
    duplicate_identities = sorted(
        identity for identity, count in Counter(identities).items() if count > 1
    )
    if duplicate_identities:
        raise ValueError(
            "duplicate ordinary spawn identities: " + ", ".join(duplicate_identities)
        )

    selected_identities = set(identities)
    for row in spawns:
        disposition = classify_spawn_candidate(row)
        if disposition != CANDIDATE_ACCEPTED:
            raise ValueError(
                "rejected ordinary spawn reached output identity={0} name={1} disposition={2}".format(
                    row.get("Identity", ""), row.get("Name", ""), disposition
                )
            )
        profile_key = ARCHETYPES[row["Name"]][0]
        if row["Name"] not in profiles or profile_key not in generated_profile_keys:
            raise ValueError(
                "ordinary spawn has missing profile identity={0} profile={1}".format(
                    row["Identity"], profile_key
                )
            )

    for row in first_rows_by_identity(load_raw_scfu_rows(SPAWN_CAPTURES)):
        disposition = classify_spawn_candidate(row)
        if disposition != CANDIDATE_ACCEPTED and row.get("Identity") in selected_identities:
            raise ValueError(
                "excluded capture identity reached ordinary output identity={0} name={1} disposition={2}".format(
                    row.get("Identity", ""), row.get("Name", ""), disposition
                )
            )

    for name, evidence in combat.items():
        if evidence["observed"]:
            if evidence["min"] is None or evidence["max"] is None:
                raise ValueError("observed combat is missing damage evidence: " + name)
            if evidence["slot"] is None or evidence["unknown"] is None or evidence["instance"] is None:
                raise ValueError("observed combat is missing attack context: " + name)
        elif any(
            evidence[field] is not None
            for field in ("min", "max", "recharge", "slot", "unknown", "instance")
        ):
            raise ValueError("unobserved combat contains invented values: " + name)

    expected_level_credits = {
        "Architect Striker": Counter({(13, 79): 2, (14, 85): 1, (15, 92): 1}),
        "Bloodcreeper": Counter({(24, 150): 1}),
        "Deranged Shopper": Counter({(8, 47): 1, (9, 53): 1}),
        "Discarded Pet": Counter(
            {(5, 18): 1, (6, 21): 3, (7, 25): 8, (8, 28): 1, (9, 32): 4, (10, 35): 8}
        ),
        "Disobedient Bot": Counter(
            {(5, 6): 2, (6, 8): 2, (8, 10): 4, (9, 11): 3, (10, 12): 2}
        ),
        "Empty Shell": Counter({(19, 118): 1, (21, 131): 1}),
        "Filth Flea": Counter(
            {
                (4, 23): 9,
                (5, 29): 11,
                (6, 35): 4,
                (7, 41): 2,
                (8, 47): 1,
                (11, 66): 6,
                (12, 72): 2,
                (13, 79): 5,
                (15, 92): 1,
                (16, 98): 1,
                (19, 118): 2,
                (20, 124): 1,
                (21, 131): 2,
            }
        ),
        "Fragmented Soul": Counter(
            {(17, 105): 1, (18, 111): 2, (21, 131): 2}
        ),
        "Incomplete Rebuild": Counter(
            {(17, 105): 1, (18, 111): 1, (19, 118): 3, (21, 131): 2}
        ),
        "Infected Attendant": Counter(
            {(11, 14): 2, (12, 15): 2, (15, 19): 1, (23, 29): 1}
        ),
        "Infector": Counter(
            {
                (16, 98): 2,
                (17, 105): 2,
                (18, 111): 1,
                (19, 118): 3,
                (24, 150): 5,
                (25, 156): 2,
            }
        ),
        "Looter": Counter({(9, 53): 2, (10, 59): 9}),
        "Lost Thought": Counter(
            {(16, 20): 1, (18, 23): 1, (21, 26): 1, (22, 28): 1}
        ),
        "Melded Patterns": Counter(
            {(18, 111): 2, (20, 124): 1, (21, 131): 3, (24, 150): 1, (25, 156): 3}
        ),
        "Molested Molecules": Counter(
            {
                (19, 118): 1,
                (20, 124): 2,
                (21, 131): 1,
                (22, 137): 1,
                (23, 144): 1,
                (24, 150): 1,
                (25, 156): 1,
            }
        ),
        "Mugger": Counter({(5, 44): 6, (8, 71): 6, (9, 80): 6, (10, 88): 6}),
        "Neural Burnout": Counter(
            {
                (16, 98): 1,
                (17, 105): 1,
                (18, 111): 2,
                (23, 144): 1,
                (25, 156): 2,
            }
        ),
        "Premature Pattern": Counter(
            {(17, 105): 1, (18, 111): 1, (23, 144): 2}
        ),
        "Redundant Scan": Counter(
            {(19, 118): 1, (20, 124): 1, (21, 131): 1, (22, 137): 1}
        ),
        "Shadow": Counter(
            {
                (9, 53): 3,
                (10, 59): 5,
                (11, 66): 1,
                (13, 79): 1,
                (14, 85): 2,
                (15, 92): 2,
                (21, 131): 1,
                (22, 137): 2,
                (23, 144): 3,
            }
        ),
        "Slum Runner": Counter(
            {
                (11, 66): 1,
                (12, 72): 3,
                (15, 92): 1,
                (16, 98): 4,
                (17, 105): 3,
                (18, 111): 1,
                (20, 124): 1,
                (21, 131): 2,
                (22, 137): 2,
                (23, 144): 3,
            }
        ),
        "Stim Fiend": Counter(
            {(10, 59): 6, (11, 66): 2, (12, 72): 4, (13, 79): 2, (14, 85): 1}
        ),
        "Thief": Counter({(5, 29): 3}),
        "Uncontrollable Anger": Counter(
            {
                (11, 14): 1,
                (12, 15): 1,
                (13, 16): 2,
                (20, 25): 1,
                (21, 26): 1,
            }
        ),
        "Violent Vagabond": Counter({(6, 21): 9, (7, 25): 5, (10, 35): 3}),
        "Workman Striker": Counter(
            {(13, 79): 2, (14, 85): 7, (15, 92): 3, (16, 98): 4, (17, 105): 3, (25, 156): 1}
        ),
    }
    expected_cat_meshes = {
        "Architect Striker": 17870,
        "Bloodcreeper": 26978,
        "Deranged Shopper": 5927,
        "Discarded Pet": 15929,
        "Disobedient Bot": 15215,
        "Empty Shell": 5941,
        "Filth Flea": 15231,
        "Fragmented Soul": 5921,
        "Incomplete Rebuild": 5921,
        "Infected Attendant": 96024,
        "Infector": 31868,
        "Looter": 17870,
        "Lost Thought": 96179,
        "Melded Patterns": 23368,
        "Molested Molecules": 5921,
        "Mugger": 17534,
        "Neural Burnout": 5941,
        "Premature Pattern": 5941,
        "Redundant Scan": 23370,
        "Shadow": 30434,
        "Slum Runner": 31774,
        "Stim Fiend": 5907,
        "Thief": 5907,
        "Uncontrollable Anger": 96177,
        "Violent Vagabond": 17870,
        "Workman Striker": 17899,
    }
    if set(corpses) != set(expected_level_credits):
        raise ValueError("ordinary corpse evidence profile set drifted")
    for name, expected in expected_level_credits.items():
        records = corpses[name]
        actual = Counter((row["enemyLevel"], row["credits"]) for row in records)
        if actual != expected:
            raise ValueError(name + " level-credit corpse evidence drifted")
        if {row["monsterData"] for row in records} != {
            EXPECTED_CORPSE_MONSTER_DATA[name]
        }:
            raise ValueError(name + " corpse MonsterData attachment drifted")
        if {row["catMesh"] for row in records} != {expected_cat_meshes[name]}:
            raise ValueError(name + " corpse CATMesh drifted")
        if any(row["credits"] <= 0 for row in records):
            raise ValueError(name + " contains excluded zero-credit corpse evidence")
        if len(
            {
                (row["capture"], row["corpseIdentity"], row["capturedUtc"])
                for row in records
            }
        ) != len(records):
            raise ValueError(name + " corpse capture generations are not unique")
        if len({(row["capture"], row["deadNpcIdentity"]) for row in records}) != len(records):
            raise ValueError(name + " dead NPC capture identities are not unique")


def generate() -> str:
    spawns = select_spawns()
    source_weapons = source_weapon_evidence_profiles(spawns)
    generation_variants = (
        incomplete_rebuild_generation_variants()
        + redundant_scan_generation_variants()
        + fragmented_soul_generation_variants()
    )
    profiles = select_archetype_profiles(spawns)
    combat = combat_profiles()
    loot = loot_profiles()
    strict_loot = strict_loot_profile_summaries(loot)
    loot_outcomes = loot_outcome_profiles()
    corpses = corpse_profiles()
    validate_content(spawns, profiles, combat, corpses)
    evidence_captures = defaultdict(set)
    for capture in CAPTURES:
        for name in set(capture_identity_names(capture).values()):
            if name in ARCHETYPES and capture_allows_archetype(capture, name):
                evidence_captures[name].add(capture)
    for name, records in corpses.items():
        for record in records:
            evidence_captures[name].add(str(record["capture"]))
    for name, records in loot_outcomes.items():
        for record in records:
            evidence_captures[name].add(str(record["capture"]))
    for name, records in source_weapons.items():
        for record in records:
            evidence_captures[name].update(record["captures"])
    supported_corpses = sorted(
        (
            record
            for name in SUPPORTED_CORPSE_NAMES_BY_MONSTER_DATA.values()
            for record in corpses.get(name, [])
        ),
        key=lambda value: (
            value["capturedUtc"],
            value["corpseIdentity"],
            value["deadNpcIdentity"],
        ),
    )
    supported_loot_outcomes = sorted(
        (
            record
            for name in SUPPORTED_CORPSE_NAMES_BY_MONSTER_DATA.values()
            for record in loot_outcomes.get(name, [])
        ),
        key=lambda value: (
            value["capturedUtc"],
            value["corpseIdentity"],
            value["deadNpcIdentity"],
            value["sequence"],
            value["slot"],
        ),
    )
    strict_loot_definition_lines = []
    for index, summary in enumerate(strict_loot):
        strict_loot_definition_lines.extend(
            strict_loot_profile_definition(
                summary,
                index < len(strict_loot) - 1,
            )
        )
    supported_source_weapon_definition_lines = []
    supported_source_weapon_profiles = sorted(
        SUPPORTED_SOURCE_WEAPON_MONSTER_DATA.items(),
        key=lambda value: (value[1], value[0]),
    )
    for profile_index, (name, monster_data) in enumerate(
        supported_source_weapon_profiles
    ):
        supported_source_weapon_definition_lines.extend(
            [
                "            new CapturedSubwaySourceWeaponProfileDefinition(",
                f"                {cs_string(name)},",
                f"                {monster_data},",
                "                new CapturedSubwaySourceWeaponEvidenceDefinition[]",
                "                {",
            ]
        )
        records = source_weapons[name]
        for record_index, item in enumerate(records):
            suffix = "," if record_index < len(records) - 1 else ""
            supported_source_weapon_definition_lines.append(
                "                    new CapturedSubwaySourceWeaponEvidenceDefinition("
                f"0x{int(item['source']):08X}, {int(item['low'])}, {int(item['high'])}, "
                f"{int(item['quality'])}, {cs_string(','.join(item['captures']))}){suffix}"
            )
        profile_suffix = "," if profile_index < len(supported_source_weapon_profiles) - 1 else ""
        supported_source_weapon_definition_lines.extend(
            [
                "                })" + profile_suffix,
            ]
        )
    generation_variant_definition_lines = [
        "            new CapturedSubwayGenerationVariantDefinition("
        f"{int(item['monsterData'])}, 0x{int(item['source']):08X}, {int(item['level'])}, "
        f"{int(item['health'])}, {int(item['healthDamage'])}, "
        f"{int(item['monsterScale'])}, {int(item['runSpeed'])}, "
        f"{int(item['low'])}, {int(item['high'])}, {int(item['quality'])}, "
        f"{cs_string(';'.join(item['evidence']))})"
        for item in generation_variants
    ]
    lines = [
        "// <auto-generated>",
        "// Generated only from completed AOSharp Subway captures by generate_subway_ordinary_content.py.",
        "// Do not hand-edit captured values; regenerate from the checked capture evidence.",
        "// </auto-generated>",
        "namespace AORebirth.Core.Playfields",
        "{",
        "    using System;",
        "    using System.Collections.Generic;",
        "    using System.Linq;",
        "",
        "    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;",
        "",
        "    using ZoneEngine.Core;",
        "    using ZoneEngine.Core.Playfields;",
        "",
        "    internal sealed class CapturedSubwayOrdinaryContentProvider",
        "    {",
        "        public const int SubwayPlayfieldInstance = 127;",
        "",
        "        private static readonly CapturedSubwayCorpseEvidenceDefinition[] SupportedCorpseEvidence =",
        "        {",
        *[
            "            " + corpse_definition(item)
            + ("," if index < len(supported_corpses) - 1 else "")
            for index, item in enumerate(supported_corpses)
        ],
        "        };",
        "",
        "        private static readonly CapturedSubwayLootOutcomeEvidenceDefinition[] SupportedLootOutcomeEvidence =",
        "        {",
        *[
            "            " + loot_outcome_definition(item)
            + ("," if index < len(supported_loot_outcomes) - 1 else "")
            for index, item in enumerate(supported_loot_outcomes)
        ],
        "        };",
        "",
        "        private static readonly CapturedSubwaySourceWeaponProfileDefinition[] SupportedSourceWeaponProfiles =",
        "        {",
        *supported_source_weapon_definition_lines,
        "        };",
        "",
        "        private static readonly CapturedSubwayGenerationVariantDefinition[] GenerationVariants =",
        "        {",
        *[
            line + ("," if index < len(generation_variant_definition_lines) - 1 else "")
            for index, line in enumerate(generation_variant_definition_lines)
        ],
        "        };",
        "",
        "        private static readonly CapturedSubwayStrictLootProfileDefinition[] StrictLootProfiles =",
        "        {",
        *strict_loot_definition_lines,
        "        };",
        "",
        "        private static readonly CapturedSubwayOrdinaryArchetypeDefinition[] Archetypes =",
        "        {",
    ]

    for name, (key, family_key) in ARCHETYPES.items():
        row = profiles[name]
        combat_row = combat[name]
        texture_lines = [
            f"new CapturedSubwayTextureDefinition({place}, {texture_id}, {unknown})"
            for place, texture_id, unknown in parse_textures(row["Textures"])
        ]
        mesh_lines = [
            f"new CapturedSubwayMeshDefinition({position}, {mesh_id}u, {override}, {layer})"
            for position, mesh_id, override, layer in parse_meshes(row["Meshes"])
        ]
        loot_lines = [
            "new CapturedSubwayLootEvidenceDefinition("
            f"{item['low']}, {item['high']}, {item['quality']}, {item['count']}, {item['corpses']}, {item['basis']})"
            for item in loot.get(name, [])
        ]
        loot_outcome_lines = [
            loot_outcome_definition(item) for item in loot_outcomes.get(name, [])
        ]
        corpse_lines = [corpse_definition(item) for item in corpses.get(name, [])]
        source_weapon_lines = [
            "new CapturedSubwaySourceWeaponEvidenceDefinition("
            f"0x{int(item['source']):08X}, {int(item['low'])}, {int(item['high'])}, "
            f"{int(item['quality'])}, {cs_string(','.join(item['captures']))})"
            for item in source_weapons.get(name, [])
        ]
        lines.extend(
            [
                "            new CapturedSubwayOrdinaryArchetypeDefinition(",
                f"                {cs_string(key)},",
                f"                {cs_string(family_key)},",
                f"                {cs_string(name)},",
                f"                {int(row['MonsterData'])},",
                f"                {int(row['NpcFamily'] or 0)},",
                f"                {int(row['NpcLosHeight'] or 0)},",
                f"                {int(row['CharacterFlags'])},",
                f"                {int(row['AccountFlags'])},",
                f"                {int(row['Expansions'])},",
                f"                {int(row['VisualFlags'])},",
                f"                {int(row['VisibleTitle'])},",
                f"                {int(row['AppearanceValue'])}u,",
                f"                {int(row['HeadMesh'] or 0)},",
                "                new CapturedSubwayTextureDefinition[]",
                "                {",
            ]
        )
        for index, item in enumerate(texture_lines):
            lines.append("                    " + item + ("," if index < len(texture_lines) - 1 else ""))
        lines.extend(["                },", "                new CapturedSubwayMeshDefinition[]", "                {"])
        for index, item in enumerate(mesh_lines):
            lines.append("                    " + item + ("," if index < len(mesh_lines) - 1 else ""))
        lines.extend(
            [
                "                },",
                "                new CapturedSubwayCombatEvidenceDefinition(",
                f"                    {str(combat_row['observed']).lower()},",
                f"                    {compatibility_int(combat_row['min'])},",
                f"                    {compatibility_int(combat_row['max'])},",
                f"                    {compatibility_float(combat_row['recharge']):.6f},",
                f"                    {compatibility_int(combat_row['slot'])},",
                f"                    {compatibility_int(combat_row['unknown'])},",
                f"                    {compatibility_int(combat_row['instance'])},",
                f"                    {combat_row['rows']}),",
                "                new CapturedSubwayLootEvidenceDefinition[]",
                "                {",
            ]
        )
        for index, item in enumerate(loot_lines):
            lines.append("                    " + item + ("," if index < len(loot_lines) - 1 else ""))
        lines.extend(
            [
                "                },",
                "                new CapturedSubwayLootOutcomeEvidenceDefinition[]",
                "                {",
            ]
        )
        for index, item in enumerate(loot_outcome_lines):
            lines.append(
                "                    "
                + item
                + ("," if index < len(loot_outcome_lines) - 1 else "")
            )
        lines.extend(
            [
                "                },",
                "                new CapturedSubwayCorpseEvidenceDefinition[]",
                "                {",
            ]
        )
        for index, item in enumerate(corpse_lines):
            lines.append("                    " + item + ("," if index < len(corpse_lines) - 1 else ""))
        lines.extend(
            [
                "                },",
                "                new string[]",
                "                {",
                *[
                    "                    " + cs_string(capture)
                    + ("," if i < len(evidence_captures[name]) - 1 else "")
                    for i, capture in enumerate(sorted(evidence_captures[name]))
                ],
                "                }," if source_weapon_lines else "                }),",
            ]
        )
        if source_weapon_lines:
            lines.extend(
                [
                    "                new CapturedSubwaySourceWeaponEvidenceDefinition[]",
                    "                {",
                ]
            )
            for index, item in enumerate(source_weapon_lines):
                lines.append(
                    "                    "
                    + item
                    + ("," if index < len(source_weapon_lines) - 1 else "")
                )
            lines.append("                }),")

    lines.extend(["        };", "", "        private static readonly CapturedSubwayOrdinarySpawnDefinition[] Spawns =", "        {"])
    for row in spawns:
        key, _ = ARCHETYPES[row["Name"]]
        x, y, z = canonical_position(row)
        waypoints = parse_triplets(row.get("Waypoints", ""))
        lines.extend(
            [
                "            new CapturedSubwayOrdinarySpawnDefinition(",
                f"                0x{identity_hex(row['Identity'])},",
                f"                {cs_string(key)},",
                f"                {int(row['Level'])},",
                f"                {int(row['Health'])},",
                f"                {int(row['HealthDamage'])},",
                f"                {int(row['MonsterScale'])},",
                f"                {int(row['RunSpeed'])},",
                f"                {cs_float(x)}, {cs_float(y)}, {cs_float(z)},",
                f"                {cs_float(row['HeadingX'])}, {cs_float(row['HeadingY'])}, {cs_float(row['HeadingZ'])}, {cs_float(row['HeadingW'])},",
                f"                (SimpleCharFullUpdateFlags)0x{parse_flags(row['ScfuFlags']):08X},",
                f"                {int(row['ScfuFlags2'].replace('HasOwner', '4') or 0) if row['ScfuFlags2'].isdigit() else 0},",
                f"                {cs_string(row['ScfuUnknown1Hex'])},",
                f"                {int(row['ScfuUnknown2'])},",
                "                new CapturedSubwayWaypointDefinition[]",
                "                {",
            ]
        )
        for index, waypoint in enumerate(waypoints):
            suffix = "," if index < len(waypoints) - 1 else ""
            lines.append(
                "                    new CapturedSubwayWaypointDefinition("
                f"{cs_float(waypoint[0])}, {cs_float(waypoint[1])}, {cs_float(waypoint[2])}){suffix}"
            )
        lines.extend(
            [
                "                },",
                f"                {cs_string(row['Owner'])},",
                f"                {cs_string(row['EvidenceCapture'])},",
                f"                {cs_string(row['CapturedUtc'])}),",
            ]
        )

    lines.extend(
        [
            "        };",
            "",
            "        private static readonly Dictionary<string, CapturedSubwayOrdinaryArchetypeDefinition> ArchetypesByKey =",
            "            Archetypes.ToDictionary(value => value.Key, StringComparer.Ordinal);",
            "",
            "        public CapturedSubwayOrdinaryArchetypeDefinition[] GetArchetypes()",
            "        {",
            "            return Archetypes.ToArray();",
            "        }",
            "",
            "        public CapturedSubwayCorpseEvidenceDefinition[] GetCorpseEvidence(int monsterData)",
            "        {",
            "            return SupportedCorpseEvidence",
            "                .Concat(Archetypes.SelectMany(value => value.CorpseEvidence))",
            "                .Where(value => value.MonsterData == monsterData)",
            "                .ToArray();",
            "        }",
            "",
            "        public CapturedSubwayLootOutcomeEvidenceDefinition[] GetLootOutcomeEvidence(int monsterData)",
            "        {",
            "            return SupportedLootOutcomeEvidence",
            "                .Concat(Archetypes.SelectMany(value => value.LootOutcomeEvidence))",
            "                .Where(value => value.MonsterData == monsterData)",
            "                .ToArray();",
            "        }",
            "",
            "        public CapturedSubwaySourceWeaponEvidenceDefinition[] GetSourceWeaponEvidence(int monsterData)",
            "        {",
            "            CapturedSubwaySourceWeaponProfileDefinition supported = SupportedSourceWeaponProfiles",
            "                .SingleOrDefault(value => value.MonsterData == monsterData);",
            "            if (supported != null)",
            "            {",
            "                return supported.SourceWeaponEvidence.ToArray();",
            "            }",
            "",
            "            CapturedSubwayOrdinaryArchetypeDefinition archetype = Archetypes",
            "                .SingleOrDefault(value => value.MonsterData == monsterData);",
            "            return archetype == null",
            "                       ? new CapturedSubwaySourceWeaponEvidenceDefinition[0]",
            "                       : archetype.SourceWeaponEvidence.ToArray();",
            "        }",
            "",
            "        public CapturedSubwayGenerationVariantDefinition[] GetGenerationVariants(int monsterData, int sourceInstance)",
            "        {",
            "            return GenerationVariants",
            "                .Where(value => value.MonsterData == monsterData && value.SourceInstance == sourceInstance)",
            "                .ToArray();",
            "        }",
            "",
            "        public CapturedSubwayStrictLootProfileDefinition GetStrictLootProfile(int monsterData)",
            "        {",
            "            return StrictLootProfiles.SingleOrDefault(value => value.MonsterData == monsterData);",
            "        }",
            "",
            "        public CapturedSubwayOrdinarySpawnDefinition[] GetSpawns()",
            "        {",
            "            return Spawns",
            "                .Where(",
            "                    spawn => !string.Equals(spawn.EvidenceCapture, \"20260710-202132\", StringComparison.Ordinal)",
            "                             || SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(spawn.SourceInstance))",
            "                .ToArray();",
            "        }",
            "",
            "        internal CapturedSubwayOrdinarySpawnDefinition[] GetAllSpawns()",
            "        {",
            "            return Spawns.ToArray();",
            "        }",
            "",
            "        public bool TryGetArchetype(string key, out CapturedSubwayOrdinaryArchetypeDefinition archetype)",
            "        {",
            "            return ArchetypesByKey.TryGetValue(key, out archetype);",
            "        }",
            "",
            "        public CombatLootTableEntry[] BuildCapturedLootEntries()",
            "        {",
            "            var entries = new List<CombatLootTableEntry>();",
            "            foreach (CapturedSubwayStrictLootProfileDefinition strictLoot in StrictLootProfiles)",
            "            {",
            "                AddCapturedLootEntries(entries, strictLoot.Name, strictLoot.MonsterData, 0, strictLoot.Entries);",
            "            }",
            "",
            "            foreach (CapturedSubwayOrdinaryArchetypeDefinition archetype in Archetypes.Where(",
            "                value => StrictLootProfiles.All(strictLoot => strictLoot.MonsterData != value.MonsterData)))",
            "            {",
            "                AddCapturedLootEntries(entries, archetype.Name, archetype.MonsterData, archetype.NpcFamily, archetype.LootEvidence);",
            "            }",
            "",
            "            return entries.ToArray();",
            "        }",
            "",
            "        private static void AddCapturedLootEntries(List<CombatLootTableEntry> entries, string name, int monsterData, int npcFamily, CapturedSubwayLootEvidenceDefinition[] lootEvidence)",
            "        {",
            "            int slot = 0;",
            "            foreach (CapturedSubwayLootEvidenceDefinition loot in lootEvidence)",
            "            {",
            "                entries.Add(",
            "                    new CombatLootTableEntry",
            "                    {",
            "                        ExactName = name,",
            "                        MonsterData = monsterData,",
            "                        NpcFamily = npcFamily,",
            "                        Slot = slot++,",
            "                        DropChanceBasisPoints = loot.ObservedBasisPoints,",
            "                        ItemTemplates =",
            "                            new[]",
            "                            {",
            "                                new CombatLootItemTemplate",
            "                                {",
            "                                    LowId = loot.LowId,",
            "                                    HighId = loot.HighId,",
            "                                    MinQuality = loot.Quality,",
            "                                    MaxQuality = loot.Quality,",
            "                                    RangeCheck = 0,",
            "                                    DropGroupHash = \"captured-subway-ordinary\"",
            "                                }",
            "                            }",
            "                    });",
            "            }",
            "        }",
            "    }",
            "",
            "    internal sealed class CapturedSubwayOrdinaryArchetypeDefinition",
            "    {",
            "        public CapturedSubwayOrdinaryArchetypeDefinition(string key, string familyKey, string name, int monsterData, int npcFamily, int npcLosHeight, int characterFlags, int accountFlags, int expansions, int visualFlags, int visibleTitle, uint appearanceValue, int headMesh, CapturedSubwayTextureDefinition[] textures, CapturedSubwayMeshDefinition[] meshes, CapturedSubwayCombatEvidenceDefinition combat, CapturedSubwayLootEvidenceDefinition[] lootEvidence, CapturedSubwayLootOutcomeEvidenceDefinition[] lootOutcomeEvidence, CapturedSubwayCorpseEvidenceDefinition[] corpseEvidence, string[] evidenceCaptures, CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence = null)",
            "        {",
            "            this.Key = key; this.FamilyKey = familyKey; this.Name = name; this.MonsterData = monsterData; this.NpcFamily = npcFamily; this.NpcLosHeight = npcLosHeight; this.CharacterFlags = characterFlags; this.AccountFlags = accountFlags; this.Expansions = expansions; this.VisualFlags = visualFlags; this.VisibleTitle = visibleTitle; this.AppearanceValue = appearanceValue; this.HeadMesh = headMesh; this.Textures = textures ?? new CapturedSubwayTextureDefinition[0]; this.Meshes = meshes ?? new CapturedSubwayMeshDefinition[0]; this.Combat = combat; this.LootEvidence = lootEvidence ?? new CapturedSubwayLootEvidenceDefinition[0]; this.LootOutcomeEvidence = lootOutcomeEvidence ?? new CapturedSubwayLootOutcomeEvidenceDefinition[0]; this.CorpseEvidence = corpseEvidence ?? new CapturedSubwayCorpseEvidenceDefinition[0]; this.EvidenceCaptures = evidenceCaptures ?? new string[0]; this.SourceWeaponEvidence = sourceWeaponEvidence ?? new CapturedSubwaySourceWeaponEvidenceDefinition[0];",
            "        }",
            "        public string Key { get; private set; } public string FamilyKey { get; private set; } public string Name { get; private set; } public int MonsterData { get; private set; } public int NpcFamily { get; private set; } public int NpcLosHeight { get; private set; } public int CharacterFlags { get; private set; } public int AccountFlags { get; private set; } public int Expansions { get; private set; } public int VisualFlags { get; private set; } public int VisibleTitle { get; private set; } public uint AppearanceValue { get; private set; } public int HeadMesh { get; private set; } public CapturedSubwayTextureDefinition[] Textures { get; private set; } public CapturedSubwayMeshDefinition[] Meshes { get; private set; } public CapturedSubwayCombatEvidenceDefinition Combat { get; private set; } public CapturedSubwayLootEvidenceDefinition[] LootEvidence { get; private set; } public CapturedSubwayLootOutcomeEvidenceDefinition[] LootOutcomeEvidence { get; private set; } public CapturedSubwayCorpseEvidenceDefinition[] CorpseEvidence { get; private set; } public string[] EvidenceCaptures { get; private set; } public CapturedSubwaySourceWeaponEvidenceDefinition[] SourceWeaponEvidence { get; private set; }",
            "    }",
            "",
            "    internal sealed class CapturedSubwayOrdinarySpawnDefinition",
            "    {",
            "        public CapturedSubwayOrdinarySpawnDefinition(int sourceInstance, string archetypeKey, int level, int health, int healthDamage, int monsterScale, int runSpeed, float x, float y, float z, float headingX, float headingY, float headingZ, float headingW, SimpleCharFullUpdateFlags capturedFlags, int capturedFlags2, string unknown1Hex, int unknown2, CapturedSubwayWaypointDefinition[] waypoints, string sourceOwnerIdentity, string evidenceCapture, string evidenceTimestamp)",
            "        {",
            "            this.SourceInstance = sourceInstance; this.ArchetypeKey = archetypeKey; this.Level = level; this.Health = health; this.HealthDamage = healthDamage; this.MonsterScale = monsterScale; this.RunSpeed = runSpeed; this.X = x; this.Y = y; this.Z = z; this.HeadingX = headingX; this.HeadingY = headingY; this.HeadingZ = headingZ; this.HeadingW = headingW; this.CapturedFlags = capturedFlags; this.CapturedFlags2 = capturedFlags2; this.Unknown1 = HexToBytes(unknown1Hex); this.Unknown2 = unknown2; this.Waypoints = waypoints ?? new CapturedSubwayWaypointDefinition[0]; this.SourceOwnerIdentity = sourceOwnerIdentity; this.EvidenceCapture = evidenceCapture; this.EvidenceTimestamp = evidenceTimestamp;",
            "        }",
            "        public int SourceInstance { get; private set; } public string ArchetypeKey { get; private set; } public int Level { get; private set; } public int Health { get; private set; } public int HealthDamage { get; private set; } public int MonsterScale { get; private set; } public int RunSpeed { get; private set; } public float X { get; private set; } public float Y { get; private set; } public float Z { get; private set; } public float HeadingX { get; private set; } public float HeadingY { get; private set; } public float HeadingZ { get; private set; } public float HeadingW { get; private set; } public SimpleCharFullUpdateFlags CapturedFlags { get; private set; } public int CapturedFlags2 { get; private set; } public byte[] Unknown1 { get; private set; } public int Unknown2 { get; private set; } public CapturedSubwayWaypointDefinition[] Waypoints { get; private set; } public string SourceOwnerIdentity { get; private set; } public string EvidenceCapture { get; private set; } public string EvidenceTimestamp { get; private set; }",
            "        private static byte[] HexToBytes(string value) { if (string.IsNullOrEmpty(value)) return new byte[0]; var result = new byte[value.Length / 2]; for (int i = 0; i < result.Length; i++) result[i] = Convert.ToByte(value.Substring(i * 2, 2), 16); return result; }",
            "    }",
            "",
            "    internal sealed class CapturedSubwayTextureDefinition { public CapturedSubwayTextureDefinition(int place, int id, int unknown) { this.Place = place; this.Id = id; this.Unknown = unknown; } public int Place { get; private set; } public int Id { get; private set; } public int Unknown { get; private set; } }",
            "    internal sealed class CapturedSubwayMeshDefinition { public CapturedSubwayMeshDefinition(int position, uint id, int overrideTextureId, int layer) { this.Position = position; this.Id = id; this.OverrideTextureId = overrideTextureId; this.Layer = layer; } public int Position { get; private set; } public uint Id { get; private set; } public int OverrideTextureId { get; private set; } public int Layer { get; private set; } }",
            "    internal sealed class CapturedSubwayWaypointDefinition { public CapturedSubwayWaypointDefinition(float x, float y, float z) { this.X = x; this.Y = y; this.Z = z; } public float X { get; private set; } public float Y { get; private set; } public float Z { get; private set; } }",
            "    internal sealed class CapturedSubwaySourceWeaponProfileDefinition { public CapturedSubwaySourceWeaponProfileDefinition(string name, int monsterData, CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence) { this.Name = name; this.MonsterData = monsterData; this.SourceWeaponEvidence = sourceWeaponEvidence ?? new CapturedSubwaySourceWeaponEvidenceDefinition[0]; } public string Name { get; private set; } public int MonsterData { get; private set; } public CapturedSubwaySourceWeaponEvidenceDefinition[] SourceWeaponEvidence { get; private set; } }",
            "    internal sealed class CapturedSubwaySourceWeaponEvidenceDefinition { public CapturedSubwaySourceWeaponEvidenceDefinition(int sourceInstance, int lowId, int highId, int quality, string evidenceCaptures) { this.SourceInstance = sourceInstance; this.LowId = lowId; this.HighId = highId; this.Quality = quality; this.EvidenceCaptures = evidenceCaptures; } public int SourceInstance { get; private set; } public int LowId { get; private set; } public int HighId { get; private set; } public int Quality { get; private set; } public string EvidenceCaptures { get; private set; } }",
            "    internal sealed class CapturedSubwayGenerationVariantDefinition { public CapturedSubwayGenerationVariantDefinition(int monsterData, int sourceInstance, int level, int health, int healthDamage, int monsterScale, int runSpeed, int weaponLowId, int weaponHighId, int weaponQuality, string evidence) { this.MonsterData = monsterData; this.SourceInstance = sourceInstance; this.Level = level; this.Health = health; this.HealthDamage = healthDamage; this.MonsterScale = monsterScale; this.RunSpeed = runSpeed; this.WeaponLowId = weaponLowId; this.WeaponHighId = weaponHighId; this.WeaponQuality = weaponQuality; this.Evidence = evidence; } public int MonsterData { get; private set; } public int SourceInstance { get; private set; } public int Level { get; private set; } public int Health { get; private set; } public int HealthDamage { get; private set; } public int MonsterScale { get; private set; } public int RunSpeed { get; private set; } public int WeaponLowId { get; private set; } public int WeaponHighId { get; private set; } public int WeaponQuality { get; private set; } public string Evidence { get; private set; } }",
            "    internal sealed class CapturedSubwayCombatEvidenceDefinition { public CapturedSubwayCombatEvidenceDefinition(bool observed, int minDamage, int maxDamage, double rechargeSeconds, int weaponSlot, int attackInfoUnknown, int weaponInstance, int observedRows) { this.Observed = observed; this.MinDamage = minDamage; this.MaxDamage = maxDamage; this.RechargeSeconds = rechargeSeconds; this.WeaponSlot = weaponSlot; this.AttackInfoUnknown = attackInfoUnknown; this.WeaponInstance = weaponInstance; this.ObservedRows = observedRows; } public bool Observed { get; private set; } public int MinDamage { get; private set; } public int MaxDamage { get; private set; } public double RechargeSeconds { get; private set; } public int WeaponSlot { get; private set; } public int AttackInfoUnknown { get; private set; } public int WeaponInstance { get; private set; } public int ObservedRows { get; private set; } }",
            "    internal sealed class CapturedSubwayLootEvidenceDefinition { public CapturedSubwayLootEvidenceDefinition(int lowId, int highId, int quality, int observedCount, int observedCorpses, int observedBasisPoints) { this.LowId = lowId; this.HighId = highId; this.Quality = quality; this.ObservedCount = observedCount; this.ObservedCorpses = observedCorpses; this.ObservedBasisPoints = observedBasisPoints; } public int LowId { get; private set; } public int HighId { get; private set; } public int Quality { get; private set; } public int ObservedCount { get; private set; } public int ObservedCorpses { get; private set; } public int ObservedBasisPoints { get; private set; } }",
            "    internal sealed class CapturedSubwayStrictLootProfileDefinition { public CapturedSubwayStrictLootProfileDefinition(string name, int monsterData, int observedCompleteInventories, int observedPositiveInventories, int observedEmptyInventories, bool itemPoolComplete, string[] evidenceCaptures, CapturedSubwayLootEvidenceDefinition[] entries) { this.Name = name; this.MonsterData = monsterData; this.ObservedCompleteInventories = observedCompleteInventories; this.ObservedPositiveInventories = observedPositiveInventories; this.ObservedEmptyInventories = observedEmptyInventories; this.ItemPoolComplete = itemPoolComplete; this.EvidenceCaptures = evidenceCaptures ?? new string[0]; this.Entries = entries ?? new CapturedSubwayLootEvidenceDefinition[0]; } public string Name { get; private set; } public int MonsterData { get; private set; } public int ObservedCompleteInventories { get; private set; } public int ObservedPositiveInventories { get; private set; } public int ObservedEmptyInventories { get; private set; } public bool ItemPoolComplete { get; private set; } public string[] EvidenceCaptures { get; private set; } public CapturedSubwayLootEvidenceDefinition[] Entries { get; private set; } }",
            "    internal sealed class CapturedSubwayLootOutcomeEvidenceDefinition { public CapturedSubwayLootOutcomeEvidenceDefinition(string capture, string capturedUtc, string corpseIdentity, string deadNpcIdentity, int monsterData, int sequence, int slot, int lowId, int highId, int quality) { this.Capture = capture; this.CapturedUtc = capturedUtc; this.CorpseIdentity = corpseIdentity; this.DeadNpcIdentity = deadNpcIdentity; this.MonsterData = monsterData; this.Sequence = sequence; this.Slot = slot; this.LowId = lowId; this.HighId = highId; this.Quality = quality; } public string Capture { get; private set; } public string CapturedUtc { get; private set; } public string CorpseIdentity { get; private set; } public string DeadNpcIdentity { get; private set; } public int MonsterData { get; private set; } public int Sequence { get; private set; } public int Slot { get; private set; } public int LowId { get; private set; } public int HighId { get; private set; } public int Quality { get; private set; } }",
            "    internal sealed class CapturedSubwayCorpseEvidenceDefinition { public CapturedSubwayCorpseEvidenceDefinition(string capture, string capturedUtc, string corpseIdentity, string deadNpcIdentity, int enemyLevel, int monsterData, int catMesh, int credits) { this.Capture = capture; this.CapturedUtc = capturedUtc; this.CorpseIdentity = corpseIdentity; this.DeadNpcIdentity = deadNpcIdentity; this.EnemyLevel = enemyLevel; this.MonsterData = monsterData; this.CatMesh = catMesh; this.Credits = credits; } public string Capture { get; private set; } public string CapturedUtc { get; private set; } public string CorpseIdentity { get; private set; } public string DeadNpcIdentity { get; private set; } public int EnemyLevel { get; private set; } public int MonsterData { get; private set; } public int CatMesh { get; private set; } public int Credits { get; private set; } }",
            "}",
            "",
        ]
    )
    return "\n".join(lines)


def canonicalize_checked_content(value: bytes) -> str:
    text = value.decode("utf-8-sig").replace("\r\n", "\n").replace("\r", "\n")
    return text[:-1] if text.endswith("\n") else text


def check_output(generated: bytes) -> None:
    if not OUTPUT.exists():
        raise SystemExit("generated provider is missing: " + str(OUTPUT))
    checked_in = OUTPUT.read_bytes()
    checked_content = canonicalize_checked_content(checked_in)
    generated_content = canonicalize_checked_content(generated)
    if checked_content != generated_content:
        checked_lines = checked_content.splitlines()
        generated_lines = generated_content.splitlines()
        first_difference = next(
            (
                index
                for index in range(max(len(checked_lines), len(generated_lines)))
                if index >= len(checked_lines)
                or index >= len(generated_lines)
                or checked_lines[index] != generated_lines[index]
            ),
            0,
        )
        raise SystemExit(
            "generated provider is stale at line {0}; checked-in={1!r} generated={2!r}; run with --write: {3}".format(
                first_difference + 1,
                checked_lines[first_difference][:160]
                if first_difference < len(checked_lines)
                else "<missing>",
                generated_lines[first_difference][:160]
                if first_difference < len(generated_lines)
                else "<missing>",
                OUTPUT,
            )
        )


def write_output_atomically(generated: bytes) -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=OUTPUT.name + ".",
        suffix=".tmp",
        dir=str(OUTPUT.parent),
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(generated)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(str(temporary_path), str(OUTPUT))
    finally:
        if temporary_path.exists():
            temporary_path.unlink()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate capture-backed ordinary Subway content."
    )
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument(
        "--check",
        action="store_true",
        help="validate and content-compare the checked-in provider without writing",
    )
    mode.add_argument(
        "--write",
        action="store_true",
        help="validate and atomically replace the checked-in provider",
    )
    mode.add_argument(
        "--dry-run",
        action="store_true",
        help="validate normalized profile/spawn input and content equivalence without writing",
    )
    return parser.parse_args()


def main() -> None:
    arguments = parse_args()
    generated = generate().replace("\n", "\r\n").encode("utf-8")
    if arguments.write:
        write_output_atomically(generated)
        print(f"generated {OUTPUT}")
        return
    check_output(generated)
    print(f"PASS content-equivalent {OUTPUT}")


if __name__ == "__main__":
    main()
