# Capture-backed combat governance, 2026-08-19

This note separates the accepted legacy generated combat cohort from future
capture-backed promotion gates. It does not promote ICC Shuttleport / PF 4582
combat and it does not change the strict historical raw validator.

## Evidence states

- `LEGACY_ACCEPTED_RAW_UNAVAILABLE`: the checked-in generated combat artifact is
  accepted only as an immutable baseline because some historical raw capture
  roots required to reproduce it are unavailable.
- `RAW_REVALIDATABLE`: selected capture raw files are present and can be scoped
  without depending on missing unrelated historical roots.
- `NEW_RAW_VERIFIED`: an explicit new capture root has all validator-grade raw
  files and has no sentinel combat fields in the scoped audit.
- `BLOCKED_INSUFFICIENT_EVIDENCE`: the selected capture is missing required raw
  files or contains sentinel combat fields such as `1234567890`.

## Immutable legacy baseline

The accepted generated cohort identity is
`1d2ed701e0221f4099ec847554103f7ede065f2682a11cd60a7ca441ced2405f`.

| Role | SHA-256 | Bytes |
| --- | --- | ---: |
| `inventory` | `0a65399104a87f5b40fec86e2ab0ce0152225bc10c4dbad6e560de931c0cf404` | 124999078 |
| `catalog` | `405ec3eb7b13b094032b284f8f6d660382b982652517d161780ff3d5c9ed2921` | 1026255 |
| `fixtures` | `26b3d5f69c8e976e78ada3b6562467aa093c9b01a51e144cc3beeb0493214793` | 2112018 |
| `activeCoverage` | `56bf3c7b58dab3cea3c6d00242164ac15a3a610acff6ca5734f0ed55240a5fd0` | 11270299 |
| `formulaDataset` | `55b91bc84a958a3b2e131fee6754393730a361510367efa62fc54ea3a6dee6ea` | 9000177 |
| `attackRangeAudit` | `a6460852f25011d417e286118f27d987ed7ebfb0106cb7c8efd926b8ed18a66d` | 3801456 |
| `secondaryEvidenceAudit` | `046d44051c7cd1ef2d122b0c5eb9f21b7679c438775f810f2f3006ca45690e1c` | 4081609 |

Baseline counts:

- Historical capture roots required by the accepted cohort: 65.
- Historical capture roots currently raw-revalidatable in this checkout: 3.
- Historical capture roots currently raw-unavailable in this checkout: 62.
- Runtime-ready profiles: 96.
- Runtime-ready generated semantic definitions: 101.
- Runtime-ready accepted variant rows: 114.
- Accepted rows fully raw-revalidatable now: 21.
- Accepted rows protected only by immutable baseline now: 93.

Run:

```cmd
cmd /d /c Tools\generate_capture_backed_npc_combat_inventory.cmd --validate-legacy-baseline
```

This validates the artifact hashes, artifact byte counts, generation identity,
summary counts, and current raw availability counts. It intentionally does not
pretend that missing historical raw can be reproduced.

## Strict historical validator

Run:

```cmd
cmd /d /c Tools\generate_capture_backed_npc_combat_inventory.cmd --validate-current
```

This remains the full historical revalidation gate. It must fail while any of
the required 65 historical raw capture roots are unavailable.

## Scoped new-capture gate

Run:

```cmd
cmd /d /c Tools\generate_capture_backed_npc_combat_inventory.cmd --audit-scoped-raw-captures --capture-root "<capture-folder>"
cmd /d /c Tools\generate_capture_backed_npc_combat_inventory.cmd --audit-scoped-raw-captures --capture-root "<capture-folder>" --require-promotable-captures
```

The scoped gate consumes only explicit `--capture-root` paths, requires
`capture_info.json`, `packets.hex.log`, `raw-packets.csv`, and
`scfu-appearance.csv`, produces a deterministic audit hash, rejects sentinel
combat values, and never mutates the legacy generated cohort.

Use the audit command for dry-run reports. Use `--require-promotable-captures`
for promotion gating; it returns a nonzero result if any selected capture is
`BLOCKED_INSUFFICIENT_EVIDENCE`.

For ICC Shuttleport / PF 4582, the current capture remains dry-run only. The
visual spawn evidence can be used separately, but combat promotion is blocked
while enemy dossier fields such as `minDamage`, `maxDamage`,
`defaultAttackType`, `attackDelay`, `rechargeDelay`, or `catMesh` are sentinel
values.
