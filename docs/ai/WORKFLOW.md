# Workflow

## First Checks

Run:

```powershell
git status --short --branch
```

Identify dirty files before editing. Do not revert user or previous-agent work unless Mike explicitly asks.

## Build And Engines

After code changes that affect server binaries:

1. Stop engines.
2. Build.
3. Start Chat, Login, and Zone.
4. Do not start WebEngine unless explicitly needed.

Stop engines:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\Mike\Documents\AORebirth\stop-engines.ps1'
```

Build:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' 'AORebirth\AORebirth.sln' /t:Build /p:Configuration=Debug /m
```

Start engines:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\Mike\Documents\AORebirth\start-engines.ps1'
```

## Database

- Use only `cellao_codex_clean`; this is the active legacy database name retained for local compatibility.
- Do not change schemas without explicit approval.
- Do not wipe or mass-edit data without explicit approval.
- Treat checked-in SQL and runtime DB changes as separate surfaces.

## Captures

- Use AOSharp capture tooling for live packet/data truth.
- Codex runs tools, builds, servers, and captures.
- Mike performs live client playtests.
- Do not ask Mike to run commands inside the game when Codex can run external tooling.

## Capture-Derived Content

These rules apply to NPC, mob, statel, static dynel, vendor, quest, item, and playfield reconstruction.

- Identity first. The captured AO identity is the primary key.
- Do not choose or replace an object based only on display name, item name, screenshot appearance, nearby objects, spatial proximity, visual similarity, assumed mesh, or guessed relationship.
- Search the complete relevant capture set for the exact identity before declaring evidence missing. Include `events.log`, `packets.hex.log`, `system-messages.log`, `npc-interactions.log`, `inventory-updates.csv`, `enemy-state.csv`, `enemy-state.json`, `vendor-full-updates.csv`, `shop-updates.csv`, and decoded full-update outputs.
- Separate interaction evidence from definition evidence. `GenericCmd Action=Use -> Terminal:56D9B4AF` proves only that the identity was used; template, mesh, name, position, rotation, stat blob, and event configuration require a full-update packet or another source tied to the same identity.
- Use the evidence hierarchy from `docs/project/KNOWN_DECISIONS.md`: exact identity-linked full-update, exact identity-linked stat/update, exact identity-linked interaction, decoded logs, extracted analysis, screenshots, then names/proximity/nearby objects.
- Do not copy template, mesh, stat blob, position, rotation, or events from a nearby identity unless the capture explicitly proves the relationship.
- Do not test alternate templates or mesh overrides because the current object looks wrong. Stop, search all captures for the exact identity, locate full-update/stat evidence, and rebuild from that evidence.
- Keep evidence extraction, data creation, visual smoke, use/interact routing, objective progression, mission completion, and rewards as separate tasks.
- Fail closed when exact identity evidence or required full-update fields are missing, conflicting, unknown, or only supported by name/appearance.

Before editing SQL or runtime data, state:

- exact captured identity;
- capture folders searched;
- full-update evidence found;
- fields that are confirmed;
- fields that remain unresolved;
- files and rows that will change.

Also provide this evidence table before any SQL or game-data edit:

| Field | Proposed value | Exact identity | Capture folder | Packet/log source | Confidence |
| --- | --- | --- | --- | --- | --- |

Local SQL/data patches must include exact rows affected, pre-apply verification query, apply command, post-apply verification query, rollback query, and confirmation that no unrelated rows changed.

## Evidence

Use this source order:

1. Official live capture.
2. Private-server capture as shape/reference evidence.
3. AO stripdown source/contracts.
4. Local code facts.

Do not patch packet-sensitive behavior from visual symptoms alone.
