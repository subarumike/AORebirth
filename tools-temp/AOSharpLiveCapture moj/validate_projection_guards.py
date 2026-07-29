#!/usr/bin/env python3
"""Static regression guards for always-on structured capture projections."""

from pathlib import Path


SOURCE = Path(__file__).with_name("Main.cs")


def section(text: str, start: str, end: str) -> str:
    start_index = text.index(start)
    end_index = text.index(end, start_index)
    return text[start_index:end_index]


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> None:
    source = SOURCE.read_text(encoding="utf-8-sig")

    dispatch = section(
        source,
        '"enemy-fight-annotation"',
        '"shop-export"',
    )
    require(
        "this.ExportEnemyN3Evidence(direction, sequence, message)" in dispatch,
        "structured enemy evidence export is missing from its independent dispatch stage",
    )
    require(
        '"enemy-evidence-export"' in dispatch
        and '"enemy-state-track"' in dispatch,
        "focused annotation, structured export, and state tracking must use independent stages",
    )
    annotation_end = dispatch.index('"enemy-evidence-export"')
    require(
        "this.ExportEnemyN3Evidence(direction, sequence, message)"
        not in dispatch[:annotation_end],
        "focused annotation must never wrap or gate structured export",
    )

    projection = section(
        source,
        "private void ExportEnemyN3Evidence",
        "private void CacheEnemyFullUpdate",
    )
    require(
        "if (IsEnemyCombatEvidenceMessage(message))" in projection,
        "combat projection must classify by message type name, not concrete runtime casts",
    )

    classifier = section(
        source,
        "private static bool IsEnemyCombatEvidenceMessage",
        "private bool TryRegisterFocusedEnemyFromMessage",
    )
    required_types = (
        "Attack",
        "AttackInfo",
        "SpecialAttackWeapon",
        "CastNanoSpell",
        "CharacterAction",
        "HealthDamage",
        "StopFight",
    )
    for message_type in required_types:
        require(
            f'"{message_type}"' in classifier,
            f"combat classifier is missing {message_type}",
        )

    focus = section(
        source,
        "private bool ShouldCaptureEnemyFightEvidence",
        "private static bool IsEnemyCombatEvidenceMessage",
    )
    observed_index = focus.index("this.enemyFightCaptureStarted = true;")
    auto_gate_index = focus.index("if (!this.enemyFightAutoCaptureEnabled)")
    require(
        observed_index < auto_gate_index,
        "combat-observed accounting must occur before the human-log auto-mode gate",
    )

    inventory = section(
        source,
        '"inventory-export"',
        "private void RunN3CaptureStage",
    )
    require(
        'message.N3MessageType.ToString(),\n                        "InventoryUpdate"' in inventory,
        "inventory projection must not depend on a concrete runtime message cast",
    )

    print("PASS: AOSharpLiveCapture projection guards")


if __name__ == "__main__":
    main()
