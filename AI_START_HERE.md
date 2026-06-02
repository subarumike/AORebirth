# AI Start Here

This repository is the local CellAO server project for Mike's Anarchy Online server work.

Before modifying code, read these files in order:

1. `docs/PROJECT_OVERVIEW.md`
2. `docs/ARCHITECTURE.md`
3. `docs/CURRENT_TASK.md`
4. `docs/KNOWN_DECISIONS.md`
5. `docs/CODE_STANDARDS.md`
6. `docs/BUGS.md`
7. `docs/LESSONS_LEARNED.md`

Then review the existing technical documentation under `CellAO/Documentation`, especially:

- `CellAO/Documentation/ProjectWorkingReference.md`
- `CellAO/Documentation/LivePacketImplementationGaps.md`
- `CellAO/Documentation/CurrentClientStructureMismatchReport.md`
- `CellAO/Documentation/StripdownDirectRepairCandidates.md`
- `CellAO/Documentation/FullStripdownCellAODiffReport.md`
- `CellAO/Documentation/Index.md`

## Non-Negotiable Project Rules

- Use only the `cellao_codex_clean` MySQL database.
- Do not change SQL schemas, wipe data, or mass-edit data unless Mike explicitly approves the exact change.
- Do not run tests against Mike's live AO client unless he explicitly says to.
- Codex runs code, tools, builds, and servers. Mike performs live client playtests.
- Do not guess packet behavior. Use official live capture, private-server capture, AO stripdown source, or code evidence.
- Keep source authority labeled: official live, private-server, AO stripdown, or local observation.
- Do not regress the fixed sit/stand, equipment, death/respawn, corpse, loot, and combat packet behavior.
- If three attempts on one repair path fail, switch to the next evidence path. After three more failures, stop and re-evaluate from source truth.

## Current High-Risk Context

The current worktree may contain active gameplay changes. Check `git status --short --branch` before editing. Do not revert changes you did not make.

The most recent active problem area is corpse loot/inventory credit desync and trade/inventory packet correctness. NPC chase remains high risk and should not be patched from intuition.

## Build And Engine Commands

Stop engines:

```powershell
Get-Process ChatEngine,LoginEngine,ZoneEngine,WebEngine,MSBuild -ErrorAction SilentlyContinue | Stop-Process -Force
```

Build:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' 'CellAO\CellAO.sln' /t:Build /p:Configuration=Debug /m
```

Start engines:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\Mike\Documents\Cellao-Clean\start-engines.ps1'
```

## Evidence Roots

- Working repo: `C:\Users\Mike\Documents\Cellao-Clean`
- AO client install: `C:\Funcom\Anarchy Online`
- AO stripdown source: `C:\Users\Mike\Documents\AO stripdown\Anarchy Online`
- Research/tooling project: `C:\Users\Mike\Documents\New project`
- Private-server client path: `C:\Users\Mike\Desktop\win`
- Capture root: `tools-temp/live-pcaps`
- AOSharp capture tooling: `tools-temp/AOSharpLiveCapture`, `tools-temp/AOSharpLiveInjector`

