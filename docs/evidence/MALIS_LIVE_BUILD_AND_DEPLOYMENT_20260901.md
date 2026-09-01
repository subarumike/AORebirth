# Malis Mission Roller 2.0 Live Build and Deployment Evidence

Date: 2026-09-01

## Scope and worktree safety

The primary worktree was preserved without reset, clean, stash, or edits. It
started on `codex/arpa3-mission-evidence` at
`1540ccac46130746bf455cecb0c5cd72ef6f68ba` with a clean status. This work was
performed in the isolated worktree
`C:\Users\Mike\Documents\AORebirth-malis-live-build` on
`codex/malis-live-build`, created from the requested modern-planner baseline
`aea19aba8d0069f4b6c34578247ec2ab53a6e584`.

## Audited source and architecture

The governed retained archive was used; no replacement source was downloaded:

- Malis commit: `3ac9943a4943b8cb80eda9e40359729e656686b0`
- Archive SHA-256:
  `c1dc1bf4c919193c0ea9b5ba3cc5419075becd5b94e1041391f0d9ebbae0074d`
- Project: `Malis Mission Roller 2.csproj`
- Architecture: AOSharp plugin, not a standalone executable
- Target framework/platform: .NET Framework 4.8, x86
- Output: `Malis Mission Roller 2.dll`

Malis initializes its main window as the plugin loads. `Start` toggles
automatic rolling, `Request` sends one request, and `Settings` controls the
difficulty/sliders, mission types, locations, roll list, and extras. `/mmr` is
a developer-only command and is not the normal UI entry point.

## Installed workstation runtime

- AO: `E:\Anarchy Online` (`18.8.50_EP1`)
- AOSharp runtime: `D:\AOTools\ReadyToUse`
- Plugin directory: `D:\AOTools\ReadyToUse\Plugins`
- AOSharp upstream source revision recorded by the installation:
  `d55eb12b5a763e5ed65851e69c50343de6c6d73c`
- AOSharp assembly identities: `AOSharp`, `AOSharp.Bootstrap`,
  `AOSharp.Common`, and `AOSharp.Core` are all assembly version `1.0.0.0`
- JSON dependency: `Newtonsoft.Json` assembly version `13.0.0.0`

The loader hashes each plugin DLL with uppercase MD5 for its `config.json`
entry, loads plugins into one application domain, resolves host assemblies from
the AOSharp directory, and resolves non-host dependencies from the plugin
directory. The package therefore deliberately excludes private copies of
AOSharp, Newtonsoft.Json, Serilog, and HtmlAgilityPack.

## Exact-runtime build

`Tools\build_malis_live_package.cmd` uses the repository-selected Python
runtime and workstation MSBuild 18.8.2, extracts the retained source into an
ignored temporary directory, and rebuilds Malis and MissionOfferHarvester
against `D:\AOTools\ReadyToUse`.

The unmodified audited Malis source compiled successfully. No AOSharp API,
namespace, mission, chat, UI, settings, IPC, terminal, inventory, identity,
Dynel, or player compatibility patch was needed. Two reviewed warnings remain:

- `CS0672`: retained obsolete `AOPluginEntry.Run(string)` compatibility seam
- `CS0649`: audited pre-existing unassigned `_missionLevel` field

Neither warning is an installed-runtime API mismatch. Malis request pacing,
mission QL tables, reward filters, sliders, acceptance behavior, the level-80
correction, and the above-200 QL200 behavior are unchanged.

Compiled Malis output:

`build-verify\MalisMissionLive\Malis Mission Roller 2\Malis Mission Roller 2.dll`

SHA-256:

`f4a358bf08d430104a70844bc84521dcc0dfc1c58c1a8aa63ad2ecc6c4b26736`

## Runtime resources and deterministic package

Deployment package: `build-verify\MalisMissionLive`

The tracked deterministic manifest is
`docs/generated/missions/malis-live/deployment-manifest.json`. It gives every
file's byte length, SHA-256, source provenance, package path, and target path.
It contains 8 JSON files, 2 WAV files, 40 UI textures, 11 UI views, and 3 UI
windows, plus the plugin DLL and its configuration. All JSON and XML parsed;
all PNG and WAV signatures validated; all hashes and the exact file set passed.

The package adds a safe `JSON\Settings.json` with Auto Accept, Auto Adjust QL,
Remove Roll, and all five mission-type matches disabled. The audited
`Default_Settings.json` remains byte-identical. This configuration prevents an
automatic match/accept during the first evidence check without changing Malis
code or mission selection.

## MissionOfferHarvester coexistence

Malis is the stimulus generator and MissionOfferHarvester remains the raw
evidence authority. The harvester received the minimum independent change
needed for that architecture: `/missionharvest observe <targetQL>` subscribes
to AOSharp's outbound message event, records Malis's exact terminal and slider
request, and correlates the returned cohort. Its original active-driver mode is
still available.

No chat command, settings filename, output directory, file lock, private
AOSharp assembly, or dependency identity conflict was found. Both plugin entry
types have public constructors, `Init(string)`, and `Teardown()`, and both
resolve the same installed `AOSharp.Core` identity. Initialization order is not
used for correlation because observation starts explicitly after both plugins
load.

## Live AOSharp installation

With both `Anarchy.exe` and `AOSharp.exe` confirmed absent, the validated
package was installed into the unambiguous loader paths:

- `D:\AOTools\ReadyToUse\Plugins\Malis Mission Roller 2`
- `D:\AOTools\ReadyToUse\Plugins\MissionOfferHarvester`

The initial installer run found no previous Malis or same-named
MissionOfferHarvester installation. The final verified install preserved
`config.json` and the preceding generated plugin directories at
`D:\AOTools\ReadyToUse\Backups\MalisLiveInstall-20260901T102804Z`, recorded
their pre-install SHA-256 values, used the loader's DLL-MD5 registration
convention, and enabled both plugins for the three existing profiles. Post-copy
DLL hashes match the package.

## Offline validation

- Exact audited archive commit and SHA: PASS
- Clean x86/.NET Framework 4.8 rebuild against installed runtime: PASS
- Static plugin entry metadata for both plugins: PASS
- Managed dependency closure: PASS
- Shared AOSharp assembly identity/coexistence: PASS
- Required JSON/UI/sound/config inventory: PASS
- JSON/XML/PNG/WAV validation: PASS
- Package hash and exact-file validation: PASS
- Deterministic package regeneration: PASS
- Recoverable targeted installation and post-copy hash verification: PASS
- AO client launch or live gameplay validation: NOT PERFORMED
- Malis unit tests: none present in the audited source

RUNTIME MISSION LOGIC CHANGED: NO
