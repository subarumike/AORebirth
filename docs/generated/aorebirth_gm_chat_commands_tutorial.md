# AORebirth GM Chat Commands Tutorial

## Summary

AORebirth private-server GM commands use the server chat-command router, not the AO client's native raw slash-command path.

Use a dot prefix in vicinity chat for interactive testing:

```text
.tp 234.3062 212.8138 y 3.775 152
```

Use `/command` inside client macros:

```text
/command tp 234.3062 212.8138 y 3.775 152
```

Do not use raw `/tp` for AORebirth GM teleport testing. The AO client/native command layer can intercept raw slash commands before AORebirth receives them and return messages such as:

```text
You are not authorized to perform that command. (Your request has been logged)
```

That text is not emitted by AORebirth's GM command gate.

## Command Routing

AORebirth dot-command flow:

1. Player sends a vicinity chat message starting with `.`.
2. `VicinityChatMessageHandler` converts the text into a `ChatCmdMessage`.
3. `ChatCmdMessageHandler` normalizes the command name and arguments.
4. `ScriptCompiler.CallChatCommand` finds the matching `AOChatCommand`.
5. `ScriptCompiler.CallChatCommand` checks `StatIds.gmlevel` against `GMLevelNeeded()`.
6. If allowed, the command executes.

AO macro flow:

1. Player macro sends `/command <name> <args>`.
2. The client sends a `ChatCmdMessage` to ZoneEngine.
3. `ChatCommandText.Normalize` strips the wrapper word `command`.
4. The remaining command uses the same `ScriptCompiler.CallChatCommand` path as dot commands.

Key files:

- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/VicinityChatMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/ChatCmdMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/ChatCommandText.cs`
- `AORebirth/Server/ZoneEngine/Script/ScriptCompiler.cs`
- `AORebirth/Server/ZoneEngine/ChatCommands/Teleport.cs`
- `AORebirth/Server/ZoneEngine/ChatCommands/InstaGrid.cs`

## Useful Commands

Teleport using current height:

```text
.tp X Z PLAYFIELD
```

Teleport with explicit height:

```text
.tp X Z y Y PLAYFIELD
```

Captured grid landing example:

```text
.tp 234.3062 212.8138 y 3.775 152
```

Macro form:

```text
/macro Grid /command tp 234.3062 212.8138 y 3.775 152
```

Teleport directly to grid command:

```text
.grid
```

Macro form:

```text
/macro Grid /command grid
```

List available AORebirth chat commands:

```text
.listcommands
```

Macro-compatible item command example:

```text
/command giveitem 43552 1
```

## GM Privileges

GM command authorization uses account-level `login.GM`, exposed through character stat `gmlevel` / stat id `215`.

Relevant code:

- `ScriptCompiler.CallChatCommand` checks `client.Controller.Character.Stats[StatIds.gmlevel].Value`.
- `Teleport.GMLevelNeeded()` returns `1`.
- `InstaGrid.GMLevelNeeded()` returns `1`.
- `StatGmLevel.GetValue` reads `LoginDataDao.Instance.GetByCharacterId(characterId).GM`.
- `CharInPlayMessageHandler` refreshes `gmlevel` when the character enters play.

Targeted GM check:

```sql
SELECT c.Id, c.Name, c.Username, l.GM
FROM characters c
JOIN login l ON l.Username = c.Username
WHERE c.Name = 'Mikedoc';
```

Targeted GM elevation:

```sql
UPDATE login l
JOIN characters c ON c.Username = l.Username
SET l.GM = 511
WHERE c.Name = 'Mikedoc';
```

If `login.GM` is changed while the character is already online, relog the character before retesting. The login/CharInPlay flow refreshes the DB-backed `gmlevel` stat.

## Important Warning

Do not use the LoginEngine console `setgm` command until `LoginDataDao.SetGM` is fixed. The current DAO implementation executes:

```sql
UPDATE login SET GM=@gm
```

without a `WHERE` clause, so it can update every account row.

## Troubleshooting

If `.tp` fails with AORebirth's authorization text:

```text
You are not authorized to use this command!. This incident will be recorded.
```

then the command reached AORebirth and the character's `gmlevel` was below the command's `GMLevelNeeded()`.

If raw `/tp` fails with client/native authorization text:

```text
You are not authorized to perform that command. (Your request has been logged)
```

then the command likely did not reach AORebirth. Use `.tp` interactively or `/command tp` in a macro instead.

To check whether AORebirth saw a command, inspect `logs/engines/ZoneEngine.out.log` for `ChatCmdMessage` or the AORebirth authorization text. If the log only shows `GenericCmd`, terminal, or vendor entries, the GM chat command did not reach the server command router.
