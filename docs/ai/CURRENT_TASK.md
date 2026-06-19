# Current Task

Generated: 2026-06-19

## Current Objective

Default-enable remaining Rex local/dev quest gates.

## Current Scope

Apply the already-fixed content-driven dialogue gate semantics to the remaining Rex quest gates:

- Missing or empty environment variable defaults enabled.
- Explicit falsey values disable: `0`, `false`, `no`, `off`.
- Explicit truthy values enable: `1`, `true`, `yes`, `on`.
- Other non-empty values remain disabled.

Target gates:

- `AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW`
- `AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS`
- `AO_REBIRTH_ENABLE_ARETE_REX_B18D_PREVIEW`
- `AO_REBIRTH_ENABLE_ARETE_REX_B18E_COMPLETION`

## Current Status

- Active investigation found the Rex content-driven dialogue gate already default-enabled in `ContentDrivenNpcDialogueRouter`.
- The four remaining Rex quest gates still used private truthy-only checks that default disabled when unset.
- Marcus has no separate per-mission environment gate; it reuses the content-driven dialogue routing gate.
- Do not change quest logic, packet serialization, rewards, database schema, or SQL/data.

## Result Document

Write `docs/generated/rex_default_enabled_gate_fix_result.md`.

## Next Step

Implement shared gate helper, replace inconsistent checks, build, dry-run, run `git diff --check`, restart ZoneEngine with Rex gate environment variables unset, and confirm logs no longer block B18C preview because of disabled gates.
