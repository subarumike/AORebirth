# Code Standards

Generated: 2026-06-02

## Naming Conventions

- Preserve existing CellAO naming where possible.
- Use clear packet field names, but document when historical names are misleading.
- Do not rename broad public APIs during focused gameplay repairs unless necessary.
- When packet fields are unknown, use stable names such as `Unknown1` only with comments or docs explaining source evidence.

## Folder Structure Rules

- Server runtime code belongs under the matching engine directory.
- Zone gameplay handlers belong under `CellAO/Server/ZoneEngine/Core/MessageHandlers`.
- Custom packet builders belong under `CellAO/Server/ZoneEngine/Core/Packets`.
- Core entity/inventory/item logic belongs under `CellAO/Libraries/Source/CellAO.Core`.
- Packet models belong under AOtomation unless a custom ZoneEngine packet builder is intentionally used.
- Temporary capture/test tooling belongs under `tools-temp`.
- AI handoff documentation belongs under root `docs`.
- Existing generated or technical docs remain under `CellAO/Documentation`.

## Class Design Rules

- Keep changes scoped to the system being repaired.
- Prefer existing CellAO patterns over new abstractions.
- Add abstractions only when they reduce real complexity or protect a verified contract.
- Avoid adding broad gameplay heuristics in packet-sensitive paths.
- For high-risk systems, separate evidence/model/test work from runtime behavior changes.

## File Naming Rules

- Use existing C# naming style.
- Keep new markdown files uppercase when part of the root AI docs system.
- Use descriptive test/tool names that identify the target system.

## Comment Standards

- Add comments only where they preserve evidence or prevent future misuse.
- Packet comments should state the source: official live, private-server, AO stripdown, AOSharp, or local playtest.
- Do not add generic comments that restate obvious code.

## Formatting Standards

- Preserve the repo's existing formatting style.
- Do not churn unrelated formatting in active source files.
- Prefer ASCII in new files unless a file already uses non-ASCII or the content requires it.

## Dependency Rules

- Do not add third-party packages for small repairs without a clear need.
- Prefer existing libraries and tools.
- Treat external references such as AOSharp, AODB, and AO stripdown as evidence unless explicitly integrated.
- Do not copy large external code blocks blindly.

## Error Handling Rules

- Prefer explicit failure messages for smoke/test tooling.
- Runtime packet paths should fail safely and log targeted diagnostics when practical.
- Do not hide a bad packet value by clamping unless the source is proven and documented.

## Performance Expectations

- Avoid high-frequency logging in movement/combat ticks unless it is temporary and targeted.
- Do not spam movement updates without capture-backed need.
- Be mindful that Mike may run heavy compile jobs in parallel.

## Security And Data Safety

- Use only `cellao_codex_clean`.
- Do not expose or duplicate local passwords in new docs.
- Do not wipe or mass-edit database rows without explicit approval.
- Do not run client-side live tests unless Mike explicitly asks.

## Git And Worktree Rules

- Check `git status --short --branch` before editing.
- Do not revert user or previous-agent changes unless Mike explicitly asks.
- Keep commits split by system.
- Do not mix unverified NPC movement with stable equipment/trade/corpse fixes.

