# Current Task

## Active Task

Fix AOSharp live capture wrapper reliability.

## Scope

- Work only in AORebirth.
- Modify only `tools-temp\start-aosharp-live-capture.cmd` and required task/result docs.
- Do not implement private-city gameplay.
- Do not modify CityController, OrgClient, CityAdvantages, grid, bank, surgery clinic, or other gameplay code.
- Do not perform repo cleanup or touch unrelated dirty GM/chat/grid-inspection files.

## Problem

`tools-temp\start-aosharp-live-capture.cmd` failed from Codex with:

`ERROR: Input redirection is not supported, exiting the process immediately.`

The wrapper used `start` to launch `AOSharpLiveInjector.exe`; under the Codex command host that path can inherit unsupported input redirection before the injector writes its startup log.

## Fix

- Replace the wrapper's direct `start` launch with a temporary Windows Script Host launcher.
- The launcher starts `AOSharpLiveInjector.exe` minimized and detached without inheriting Codex stdin.
- Treat live capture-folder output as success when the plugin is active, even if the injector log includes the known stdin warning.
- Require `packets.hex.log` and `events.log` to exist with content in the newest capture folder before using capture-folder output as the success signal.
- The approved external command remains unchanged:

```cmd
cmd /d /c tools-temp\start-aosharp-live-capture.cmd --title "<AO window title>"
```

or:

```cmd
cmd /d /c tools-temp\start-aosharp-live-capture.cmd --pid <ao-client-pid>
```

## Validation Plan

- `git diff --check`
- Run the fixed wrapper once against an already-running AO client.
- Confirm the wrapper reports success.
- Confirm a new capture folder path.
- Confirm live packet files are written.

## Validation Result

- Existing in-game capture folder confirmed: `tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260623-010420`.
- `20260623-010420` contains `capture_info.json`, `packets.hex.log` (`78470` bytes), and `events.log` (`123365` bytes).
- Fixed wrapper returned success against the running AO client using:

```cmd
cmd /d /c tools-temp\start-aosharp-live-capture.cmd --title "Anarchy Online"
```

- Latest wrapper-reported capture output: `tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260623-010708`.
- `20260623-010708` contains `packets.hex.log` (`9185` bytes) and `events.log` (`20539` bytes).
