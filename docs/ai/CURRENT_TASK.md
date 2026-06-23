# Current Task

## Active Task

Fix AOSharp live capture double injection.

## Scope

- Work only in AORebirth.
- Modify only `tools-temp\start-aosharp-live-capture.cmd` and required task/result docs.
- Do not implement private-city gameplay.
- Do not modify CityController, OrgClient, CityAdvantages, grid, bank, surgery clinic, or other gameplay code.
- Do not perform repo cleanup or touch unrelated dirty GM/chat/grid-inspection files.

## Problem

Commit `6a789e27` fixed false failure detection, but repeated wrapper runs can still inject while an AOSharpLiveCapture session is already active.

Client evidence showed one wrapper run producing duplicate in-game startup lines for the same capture folder. That points at duplicate load/attach/initialization, or repeated wrapper injection into an already-running capture session.

## Fix

- Keep the Windows Script Host launcher path from `6a789e27`.
- Add an active-capture preflight before launching the injector.
- If the newest capture folder is still marked `running` and already has non-empty `packets.hex.log` and `events.log`, report success and do not invoke `AOSharpLiveInjector.exe` again.
- Continue treating the known stdin warning as non-fatal when capture output proves the plugin is active.
- The approved external command remains unchanged:

```cmd
cmd /d /c tools-temp\start-aosharp-live-capture.cmd --title "<AO window title>"
```

or:

```cmd
cmd /d /c tools-temp\start-aosharp-live-capture.cmd --pid <ao-client-pid>
```

## Validation Plan

- Run the fixed wrapper once against an already-running AO client.
- Confirm exactly one capture folder outcome for the invocation: either one new folder after a fresh start, or the already-active folder with no reinjection.
- Confirm one in-game `AOSharpLiveCapture logging to ...` line for a fresh start, and no duplicate line when the wrapper detects an already-active capture.
- Confirm no lingering `AOSharpLiveInjector.exe` process/window.
- Confirm live packet files are written.
- `git diff --check`

## Validation Result

- First validation exposed the stale-success path: the injector logged a plugin load against PID `26428`, but the wrapper reported completed folder `20260623-010726`.
- Updated wrapper then returned before injector launch:

```cmd
cmd /d /c tools-temp\start-aosharp-live-capture.cmd --title "Anarchy Online"
```

- Wrapper result: `SUCCESS: AOSharp live capture already active.`
- Reported capture folder: `tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260623-011533`.
- `20260623-011533` contains `packets.hex.log` (`13631` bytes) and `events.log` (`26106` bytes).
- `AOSharpLiveInjector.exe` was not left running.
- `git diff --check` passed with existing CRLF warnings only.
- In-game duplicate-line confirmation is limited to wrapper behavior: the final validation returned before invoking the injector, so that run should not create a new startup line.
