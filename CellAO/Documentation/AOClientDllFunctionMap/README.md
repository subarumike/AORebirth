# AO Client DLL Function Map

Generated from `C:\Funcom\Anarchy Online` on 2026-05-09.

This directory maps readable symbols from the AO client DLLs for future server work. It combines a fast PE export/string pass with a Ghidra headless analysis pass over the 31 AO-owned client DLLs.

## Files

- `ao_client_dll_summary.csv` - per-DLL counts from the fast export/string pass.
- `ao_client_dll_exports.csv` and `.jsonl` - 57,024 PE export rows, with MSVC undecorated signatures where available.
- `ao_client_dll_decorated_function_strings.csv` and `.jsonl` - 23,942 decorated function-like strings found by `strings`, with undecorated forms where possible.
- `ao_client_dll_symbol_hints.csv` and `.jsonl` - 1,701 AO-specific strings: N3Msg names, IIR classes, feedback ids, stat names, corpse/combat/loot terms, and animation filenames.
- `ghidra\ao_client_dll_ghidra_functions_all.csv` - 64,123 functions discovered by Ghidra.
- `ghidra\ao_client_dll_ghidra_functions_readable.csv` - 35,091 Ghidra functions with non-default/readable names.
- `ghidra\ao_client_dll_ghidra_summary.csv` - per-DLL Ghidra function counts.
- `ao_client_dll_combat_corpse_loot_readable_functions.csv` - 638 filtered readable Ghidra rows for combat, corpse, loot, N3Msg, full-update, feedback, and dynel-death terms.

## Generator Scripts

- `tools-temp\ao-dll-function-map\generate-ao-client-dll-map.ps1`
- `tools-temp\ao-dll-function-map\ExportAoFunctions.java`
- `tools-temp\ao-dll-function-map\run-ghidra-ao-client-map.ps1`

The Ghidra pass used:

`C:\Users\Mike\Downloads\ghidra-master\installed\ghidra_12.2_DEV_20260428_win_x86_64\ghidra_12.2_DEV\support\analyzeHeadless.bat`

## Key Combat/Corpse Rows

Important `Gamecode.dll` Ghidra rows:

| entry | name | signature |
| --- | --- | --- |
| `10026b19` | `N3Msg_CanAttack` | `bool __thiscall N3Msg_CanAttack(Identity_t * param_1)` |
| `10028433` | `N3Msg_ContainerAddItem` | `void __thiscall N3Msg_ContainerAddItem(Identity_t * param_1, Identity_t * param_2)` |
| `1002939b` | `N3Msg_DefaultActionOnDynel` | `void __thiscall N3Msg_DefaultActionOnDynel(Identity_t * param_1)` |
| `10028147` | `N3Msg_DefaultAttack` | `void __thiscall N3Msg_DefaultAttack(Identity_t * param_1, bool param_2)` |
| `10026ae0` | `N3Msg_GetAttackingID` | `bool __thiscall N3Msg_GetAttackingID(Identity_t * param_1)` |
| `100274b8` | `N3Msg_GetCorrectActionID` | `void __thiscall N3Msg_GetCorrectActionID(Identity_t * param_1)` |
| `10015bf3` | `N3Msg_GetCurrentFightMode` | `FightTypeAllowed_e __thiscall N3Msg_GetCurrentFightMode(Identity_t * param_1)` |
| `10026ac3` | `N3Msg_IsAttacking` | `bool __thiscall N3Msg_IsAttacking(void)` |
| `100273ed` | `N3Msg_RequestInfoPacket` | `void __thiscall N3Msg_RequestInfoPacket(Identity_t * param_1)` |
| `100184cf` | `N3Msg_SetLootAccess` | `void __thiscall N3Msg_SetLootAccess(Identity_t * param_1, Identity_t * param_2, Identity_t * param_3, bool param_4)` |
| `100280e0` | `N3Msg_StopAttack` | `void __thiscall N3Msg_StopAttack(void)` |
| `10028883` | `N3Msg_UseItem` | `void __thiscall N3Msg_UseItem(Identity_t * param_1, bool param_2)` |
| `10015c5a` | `ToClientDynelDead` | `void __thiscall ToClientDynelDead(void)` |

`N3.dll` also has `n3EngineClient_t::ToClientDynelDead` at `100075ba`.

## Search Examples

PowerShell:

```powershell
$map = 'C:\Users\Mike\Documents\Cellao-Clean\CellAO\Documentation\AOClientDllFunctionMap'
Import-Csv "$map\ghidra\ao_client_dll_ghidra_functions_readable.csv" |
  Where-Object { $_.Name -match 'Corpse|Loot|Attack|Fight|N3Msg' } |
  Select-Object Program,EntryPoint,Name,Namespace,Signature

Import-Csv "$map\ao_client_dll_exports.csv" |
  Where-Object { $_.Undecorated -match 'SetLootAccess|DefaultAttack|StopAttack' } |
  Select-Object Dll,ValueHex,Symbol,Undecorated
```

Notes:
- Prefer PE export rows and Ghidra readable rows for implementation work.
- String hints are useful for discovering terms, but many are class names, RTTI, feedback names, stat names, or animation filenames rather than callable functions.
- Third-party runtime DLLs are included in the fast export/string map for completeness, but the Ghidra pass only analyzed AO-owned DLLs.
