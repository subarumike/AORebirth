import csv
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "player_combat_evidence.py"
SPEC = importlib.util.spec_from_file_location("player_combat_evidence", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


PROFILE_HEADER = (
    "CapturedUtc,Phase,Identity,Name,IsPlayer,IsInPlay,IsAlive,Level,XP,Breed,Profession,"
    "Gender,Sex,Race,Side,Faction,Health,MaxHealth,HealthPercent,ComputerLiteracy,"
    "MinDamage,MaxDamage,DefaultAttackType,DamageType1,DamageType2,AttackDelay,"
    "RechargeDelay,EquippedWeapons,Position,FightingTarget,StatsJson,Error\n"
)


def profile_row(phase, xp):
    return (
        f'"2026-08-21T06:39:14Z","{phase}","(SimpleChar:7429)","Enfonator",'
        f'"True","True","True","3","{xp}","4","9","","1","1","0","",'
        '"132","142","92.9","20","1234567890","1234567890","1234567890","0",'
        '"1234567890","1234567890","1234567890","1","(0,0,0)","","{}",""\n'
    )


def fight_line(sequence, detail):
    return f"2026-08-21T06:39:{16 + sequence // 10:02d}.0000000Z IN-N3 #{sequence} type={detail}\n"


class PlayerCombatEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.capture = Path(self.temp.name) / "ICC Shuttleport [PF 4582] - 20260821-013914"
        self.capture.mkdir()
        (self.capture / "player-profile.csv").write_text(
            PROFILE_HEADER + profile_row("capture-start", 7109) + profile_row("capture-end", 7109),
            encoding="utf-8",
        )

        amounts = [5, 7, 10, 6, 5, 9, 6, 6, 5, 5, 6, 8, 8, 7, 10, 7, 7, 8, 9, 7]
        lines = []
        for sequence, amount in enumerate(amounts, start=1):
            lines.append(
                fight_line(
                    sequence,
                    "AttackInfoMessage { Amount=%d AmmoCount=-1 WeaponSlot=0 Target=(SimpleChar:F55E4) "
                    "Unk1=0 HitType=Normal WeaponInstance=0 N3MessageType=AttackInfo Identity=(SimpleChar:7429) }"
                    % amount,
                )
            )
        lines.append(
            fight_line(
                21,
                "AttackInfoMessage { Amount=17 AmmoCount=-1 WeaponSlot=0 Target=(SimpleChar:F55E4) "
                "Unk1=4 HitType=Critical WeaponInstance=0 N3MessageType=AttackInfo Identity=(SimpleChar:7429) }",
            )
        )
        lines.append(
            fight_line(
                22,
                "SpecialAttackInfoMessage { EquipSlot=0 Amount=10 AmmoCount=-1 Target=(SimpleChar:F55E4) "
                "Stat=Brawl Unk1=0 N3MessageType=SpecialAttackInfo Identity=(SimpleChar:7429) }",
            )
        )
        (self.capture / "enemy-fight-events.log").write_text("".join(lines), encoding="utf-8")

    def tearDown(self):
        self.temp.cleanup()

    def test_reference_damage_and_governance(self):
        evidence = MODULE.build_player_combat_evidence(self.capture)
        damage = evidence["damage"]
        self.assertEqual(damage["damageEvents"], 22)
        self.assertEqual(damage["normalAttacks"], 21)
        self.assertEqual(damage["brawlAttacks"], 1)
        self.assertEqual(damage["observedNormalDamageMin"], 5)
        self.assertEqual(damage["observedNormalDamageMax"], 10)
        self.assertEqual(damage["observedCriticalDamage"], [17])
        self.assertEqual(damage["observedBrawlDamage"], [10])
        self.assertEqual(damage["totalObservedDamage"], 168)

        stats = evidence["statEvidence"]
        for field in ("MinDamage", "MaxDamage", "AttackDelay", "RechargeDelay"):
            self.assertEqual(stats[field]["value"], "UNPROVEN")
            self.assertEqual(stats[field]["source"], "sentinel/default")
        self.assertEqual(stats["EquippedWeapons"]["value"], "1")
        self.assertEqual(evidence["attackMode"], "UNRESOLVED")
        self.assertEqual(evidence["packetEvidence"]["WeaponInstance"]["values"], ["0", "UNPROVEN"])
        self.assertEqual(evidence["packetEvidence"]["AmmoCount"]["values"], [-1])
        self.assertEqual(stats["AttackRange"]["value"], "UNPROVEN")
        self.assertFalse(evidence["playerCombatComplete"])
        self.assertEqual(
            evidence["governedPromotion"]["status"],
            "BLOCKED_UNPROVEN_PLAYER_COMBAT_FIELDS",
        )

    def test_structured_player_combat_rows_are_preferred(self):
        with (self.capture / "player-combat.csv").open("w", newline="", encoding="utf-8") as handle:
            writer = csv.DictWriter(
                handle,
                fieldnames=(
                    "CapturedUtc,Direction,Sequence,MessageType,AttackerRole,AttackerIdentity,"
                    "TargetRole,TargetIdentity,AttackKind,Amount,HitType,Stat,WeaponSlot,"
                    "WeaponInstance,AmmoCount,EvidenceSource,Detail"
                ).split(","),
            )
            writer.writeheader()
            writer.writerow(
                {
                    "CapturedUtc": "2026-08-21T06:39:17Z",
                    "Direction": "IN-N3",
                    "Sequence": "1",
                    "MessageType": "AttackInfo",
                    "AttackerRole": "local-player",
                    "AttackerIdentity": "(SimpleChar:7429)",
                    "TargetRole": "enemy",
                    "TargetIdentity": "(SimpleChar:F55E4)",
                    "AttackKind": "Normal",
                    "Amount": "5",
                    "HitType": "Normal",
                    "Stat": "",
                    "WeaponSlot": "0",
                    "WeaponInstance": "0",
                    "AmmoCount": "-1",
                    "EvidenceSource": "direct-protocol-message",
                    "Detail": "",
                }
            )
        evidence = MODULE.build_player_combat_evidence(self.capture)
        self.assertEqual(evidence["inputSource"], "player-combat.csv")
        self.assertEqual(evidence["damage"]["damageEvents"], 1)

    def test_schema2_state_evidence_precedes_profile_sentinels(self):
        combat_fields = (
            "SchemaVersion,CapturedUtc,MonotonicTicks,MonotonicFrequency,Direction,Sequence,MessageType,EventPhase,"
            "AttackerRole,AttackerIdentity,TargetRole,TargetIdentity,AttackKind,Amount,HitType,DamageType,"
            "DamageTypeSource,Stat,WeaponSlot,WeaponInstance,AmmoCount,EquipmentSnapshotId,ActiveWeaponCorrelation,"
            "PlayerPositionX,PlayerPositionY,PlayerPositionZ,TargetPositionX,TargetPositionY,TargetPositionZ,"
            "EvidenceSource,Detail"
        ).split(",")
        with (self.capture / "player-combat.csv").open("w", newline="", encoding="utf-8") as handle:
            writer = csv.DictWriter(handle, fieldnames=combat_fields)
            writer.writeheader()
            writer.writerow(
                {
                    "SchemaVersion": "2",
                    "CapturedUtc": "2026-08-21T06:39:17Z",
                    "MonotonicTicks": "1000",
                    "MonotonicFrequency": "1000",
                    "Direction": "IN-N3",
                    "Sequence": "1",
                    "MessageType": "AttackInfo",
                    "EventPhase": "hit",
                    "AttackerRole": "local-player",
                    "AttackerIdentity": "(SimpleChar:7429)",
                    "TargetRole": "enemy",
                    "TargetIdentity": "(SimpleChar:F55E4)",
                    "AttackKind": "Normal",
                    "Amount": "7",
                    "HitType": "Normal",
                    "DamageType": "3",
                    "DamageTypeSource": "direct-protocol-message",
                    "WeaponSlot": "6",
                    "WeaponInstance": "0",
                    "AmmoCount": "-1",
                    "EquipmentSnapshotId": "PCS-000001",
                    "ActiveWeaponCorrelation": "source=LocalPlayer.Weapons;templateId=218406",
                    "PlayerPositionX": "1",
                    "PlayerPositionY": "2",
                    "PlayerPositionZ": "3",
                    "TargetPositionX": "2",
                    "TargetPositionY": "2",
                    "TargetPositionZ": "3",
                    "EvidenceSource": "direct-protocol-message",
                }
            )

        state_fields = (
            "SchemaVersion,SnapshotId,Phase,Trigger,Provenance,PlayerStatsJson,ActiveWeaponsJson,"
            "NaturalAttackMode,NaturalSpecialAttacks,MartialArts,UnarmedTemplateInstance,AttackRangeRuntime,"
            "AttackRangeSource"
        ).split(",")
        template_stats = {
            field: {"rawValue": value, "status": "observed", "source": "resolved-active-template"}
            for field, value in {
                "MinDamage": "6",
                "MaxDamage": "24",
                "CriticalBonus": "12",
                "AttackDelay": "150",
                "RechargeDelay": "350",
                "AttackRange": "2",
            }.items()
        }
        with (self.capture / "player-combat-state.csv").open("w", newline="", encoding="utf-8") as handle:
            writer = csv.DictWriter(handle, fieldnames=state_fields)
            writer.writeheader()
            writer.writerow(
                {
                    "SchemaVersion": "2",
                    "SnapshotId": "PCS-000001",
                    "Phase": "startup",
                    "Trigger": "capture-start",
                    "Provenance": "AOSharp runtime",
                    "PlayerStatsJson": json.dumps(
                        {
                            "MinDamage": {"rawValue": "1234567890", "status": "sentinel-or-default", "source": "runtime"},
                            "MaxDamage": {"rawValue": "1234567890", "status": "sentinel-or-default", "source": "runtime"},
                        }
                    ),
                    "ActiveWeaponsJson": json.dumps(
                        [
                            {
                                "slot": "Weap_RightHand",
                                "identity": "(WeaponInstance:ABC)",
                                "templateId": "218406",
                                "qualityLevel": "1",
                                "templateStats": template_stats,
                            }
                        ]
                    ),
                    "NaturalAttackMode": "armed-runtime-equipped-weapons",
                    "NaturalSpecialAttacks": "Brawl",
                    "MartialArts": "20",
                    "UnarmedTemplateInstance": "0",
                    "AttackRangeRuntime": "2",
                    "AttackRangeSource": "AOSharp.LocalPlayer.AttackRange",
                }
            )

        evidence = MODULE.build_player_combat_evidence(self.capture)
        self.assertEqual(evidence["statEvidence"]["MinDamage"]["value"], "6")
        self.assertEqual(evidence["statEvidence"]["MinDamage"]["source"], "resolved-active-template")
        self.assertEqual(evidence["statEvidence"]["AttackDelay"]["value"], "150")
        self.assertEqual(evidence["attackMode"], "WEAPON_ACTIVE")
        self.assertEqual(evidence["stateCorrelation"]["missingSnapshotIds"], [])
        self.assertEqual(evidence["packetEvidence"]["observedNormalAttackIntervalsMs"], [])


if __name__ == "__main__":
    unittest.main()
