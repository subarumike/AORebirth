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
powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\Mike\Documents\Cellao-Clean\stop-engines.ps1'
```

Build:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' 'CellAO\CellAO.sln' /t:Build /p:Configuration=Debug /m
```

Start engines:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\Mike\Documents\Cellao-Clean\start-engines.ps1'
```

## Database

- Use only `cellao_codex_clean`.
- Do not change schemas without explicit approval.
- Do not wipe or mass-edit data without explicit approval.
- Treat checked-in SQL and runtime DB changes as separate surfaces.

## Captures

- Use AOSharp capture tooling for live packet/data truth.
- Codex runs tools, builds, servers, and captures.
- Mike performs live client playtests.
- Do not ask Mike to run commands inside the game when Codex can run external tooling.

## Evidence

Use this source order:

1. Official live capture.
2. Private-server capture as shape/reference evidence.
3. AO stripdown source/contracts.
4. Local code facts.

Do not patch packet-sensitive behavior from visual symptoms alone.
