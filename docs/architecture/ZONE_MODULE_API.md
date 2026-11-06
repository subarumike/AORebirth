# Zone module API

ZoneEngine_New loads external modules: trusted .NET assemblies that run in the zone process. A module builds
against `AORebirth.Modules.Abstractions` (`AORebirth/Libraries/Source/AORebirth.Modules.Abstractions`). That
assembly is the whole contract. It holds primitives, strings and small records only, and no ZoneEngine type.

The current version is **1.8**.

## Loading

`Config/Modules.json` sits beside the GameData tree, or beside the process:

```json
{
  "Enabled": ["KnubotDevelopment"],
  "Required": [],
  "Directory": "Modules",
  "Capabilities": { "KnubotDevelopment": ["read-world", "refresh-npc", "send-messages"] }
}
```

- Each module lives in `Modules/<Id>/`, with a `module.json` file (`Id`, `Assembly`, `Version`). It loads into its
  own load context, and the Abstractions assembly is shared.
- Every public `IZoneModule` gets `Initialize(IZoneModuleContext)` at startup and `Shutdown()` at exit.
- There is no hot reload and no unloading.
- If a module fails to load, it is skipped and logged. If the module is also listed in `Required`, zone startup fails.

## Versioning

- The major version of the Abstractions `AssemblyVersion` is the contract. The engine refuses a module built
  against a different major version.
- Minor versions only add members: no member is renamed or removed. A 1.1 module therefore loads unchanged on a
  1.2 engine.
- A module built against 1.2 that uses 1.2 members needs a 1.2 engine.

| Version | Added |
|---|---|
| 1.0 | `Log`, `Events` (`NpcKilled`, `PlayerDied`), `GetConfig<T>`, `RegisterConnectionFilter` |
| 1.1 | `Knubot` (check, reload and override folder for Knubot scripts) |
| 1.2 | `World`, `Actions`, lifecycle events, capabilities, `RegisterGmCommand` |
| 1.3 | `GameData` (reloadable GameData and the override folder), `Actions.RespawnNpc`, the `gamedata-reload` and `respawn-npc` capabilities |
| 1.4 | `RegisterHandoffHook` (admit or refuse login handoffs to this zone) |
| 1.5 | `RegisterMessageObserver`, `World.Session`, `Actions.Operations` and `Actions.Invoke` (named operations), module.json `Operations`, the `observe-messages` and `read-sessions` capabilities |
| 1.6 | `Clock` and a timestamp on every event; action, cooldown, movement-correction, item-move, chat and combat events; `RegisterVisibilityObserver`; `World.Cooldowns` and `World.MessageCounts`; `MessageSubscription.AllTypes`; more on `NpcSpawned`, `NpcInfo` and `PlayerInfo`; the `observe-actions`, `observe-visibility`, `observe-chat` and `read-chat-text` capabilities |
| 1.7 | `PlayerInfo.TeamId`, `TeamLeader` and `OrgId`; the `server.shutdown` and `server.shutdown-abort` operations |
| 1.8 | Account sanctions: the `account.suspend` and `account.unban` operations, `account.ban` recorded as a sanction, `Actions.AccountSanctions`, the `read-sanctions` capability |

## Context members

| Member | What it is |
|---|---|
| `ModuleId`, `ModuleDirectory`, `Log` | The module's identity, folder and prefixed logger. |
| `GetConfig<T>()` | Reads `Modules/<Id>/config.json`, or returns null when it is absent. |
| `RegisterConnectionFilter` | The byte filter for new zone connections. Only one module may register one. |
| `RegisterGmCommand` | Adds a `.name` GM command. Only during `Initialize` (see below). |
| `RegisterMessageObserver` | Watches N3 messages of the types it names (or all of them, 1.6), inbound, outbound or both. Only during `Initialize` (see below). |
| `RegisterVisibilityObserver` | Told when a client is told an entity exists and when it stops (1.6). Only during `Initialize` (see below). |
| `Clock` | The clock every timestamp is on (1.6, see below). |
| `Events` | Gameplay events (see below). |
| `Knubot` | `Check(id, json)`, `Reload()`, `OverrideDirectory` (the `AO_REBIRTH_KNUBOT_PATH` folder). |
| `World` | Read-only snapshots: `Players()`, `Player(id)`, `Playfields()`, `Npcs(pf)`, `Npc(pf, instance)`, `Stat(entity, statId)`. `Session(characterId)` (1.5) reads the character's connection. `Cooldowns(characterId)` and `MessageCounts(sessionId)` (1.6). |
| `Actions` | `RefreshNpc(pf, instance)`, `SendSystemMessage(characterId, text)`, `SendFeedback(characterId, feedbackId)`, `RespawnNpc(pf, instance)`. From 1.5 also `Operations` and `Invoke(name, target, args)` (see below). From 1.8 `AccountSanctions(account, limit)` (see Account sanctions). |
| `GameData` | `OverrideDirectory` (the `AO_REBIRTH_GAMEDATA_OVERRIDE_PATH` folder), `Reloadable` (the names), `Reload(name)` (see below). |

`World` and `Actions` cover **this zone instance only**. A player or NPC on another instance is not found, and
actions on it return false.

## Events

| Event | Raised when |
|---|---|
| `NpcKilled` | An NPC died, after XP and quest credit. |
| `PlayerDied` | A player died. |
| `PlayerEnteredPlayfield` / `PlayerLeftPlayfield` | A player entered or left a playfield on this instance: login or logout, a zone change, or a move to or from another instance. |
| `PlayerLoggedIn` / `PlayerLoggedOut` | A character came into play from a login, or left play by logging out or a link-dead timeout. Zone changes do not raise these. |
| `NpcSpawned` / `NpcDespawned` | An NPC entered or left a playfield. |
| `KnubotChatOpened` | A player opened a Knubot chat (character, NPC instance, script id). |
| `PlayerActionAttempted` / `PlayerActionResolved` | A player's request to use something was decided, and how it ended (1.6, `observe-actions`; see below). |
| `CooldownStarted` / `CooldownEnded` | A lock was set on a player, or ran out or was cleared (1.6, `observe-actions`; see below). |
| `PlayerMovementCorrected` | The engine refused or rewrote a client position and pulled the client back (1.6). |
| `ItemMoved` | An item changed place: loot, pickup, trade, shop, bank, reward, creation, destruction (1.6). |
| `PlayerSpoke` | A player spoke in chat the zone handles (1.6, `observe-chat`; the text needs `read-chat-text`). |
| `CombatResolved` | A hit or miss involving at least one player was resolved (1.6). |

From 1.6 every event carries `Timestamp`, when the engine raised it on `Clock`. An event a client message caused
also carries `RequestTimestamp`: that message's `MessageSample.Timestamp`, so an observer can match the two exactly,
or 0 when no message caused it. `NpcSpawned` and `NpcDespawned` also carry `Position`, `Level` and `SpawnerId` (the
spawn point, stable across respawns and restarts, or 0).

## Threading

- **Event handlers run synchronously on the playfield tick.**
  - Keep them short, and never block (no I/O, waits or locks).
  - Event objects are built only when someone subscribes.
  - A handler that throws is logged; after 10 failures in a row it is unsubscribed.
  - A handler slower than 2 ms is reported.
- **`World` is safe from any thread.**
  - It returns immutable copies that never refer to live entities.
  - Values come from a snapshot each playfield publishes at the end of its tick, so they may be up to one tick stale.
  - A playfield publishes only for 30 s after someone reads it, so the view costs nothing when unused.
  - From a module thread, the first read of a playfield, or of a stat newly watched on an entity, can wait up to
    500 ms for the next tick.
  - From an event handler it never waits: it reads the current tick directly, or uses the last snapshot of another
    playfield.
- **`Actions` never run on the caller's thread.**
  - Each action is queued onto the target's playfield tick, and the returned task completes after it ran there.
  - The task is false when the target is gone, and also false after 10 s if the playfield was retired first.
  - Awaiting an action inside an event handler is pointless, because the action runs on a later tick.
- Module code never runs under an engine lock, except for event handlers, which are already on the tick.

## GM commands

During `Initialize`, a module calls `RegisterGmCommand(new ModuleGmCommand { Name, RequiredGmLevel, Usage, Summary, Execute })`.

- **How it runs:** the command runs exactly like a built-in one.
  - It can be typed in vicinity chat or relayed from the chat server.
  - It gets the same GM level check (the default `RequiredGmLevel` is 1; 0 lets every player run it).
  - `-h` / `--help` opens its help popup.
- **Names:** 1–32 letters, digits, `-` or `_`, without the dot, compared ignoring case.
- **Clashes:**
  - A name another module already registered fails the second module's load.
  - A name a built-in command uses is checked when the zone builds its GM command dispatcher, before it accepts
    connections. That module is then shut down and unloaded with all its commands. If it is `Required`, zone
    startup fails.
- **`Execute`:** receives a `GmCommandInvocation`, with `CharacterId`, `PlayfieldId`, `GmLevel`, `Args`, `Target`,
  `Reply`, `ReplyError` and `ReplyUsage`.
  - It runs on the issuer's playfield tick, so it must be fast and never block. Hand slow work to another thread
    and answer with `Actions.SendSystemMessage`.
  - The invocation is valid only while the handler runs.
  - If the handler throws, the issuer sees "Command failed: …" and the exception is logged.

## Capabilities

The `Capabilities` entry in Modules.json is optional.

- **A module with no entry** keeps 1.1 behaviour and may use everything.
- **A module with an entry** may use only what it lists. Any other member throws `UnauthorizedAccessException`, and
  the engine logs a warning the first time.
- **Unknown names** are ignored with a warning.

| Capability | Grants |
|---|---|
| `read-world` | every `World` member |
| `refresh-npc` | `Actions.RefreshNpc` |
| `send-messages` | `Actions.SendSystemMessage`, `Actions.SendFeedback` |
| `gamedata-reload` | `GameData.Reload` (reading `OverrideDirectory` and `Reloadable` needs nothing) |
| `respawn-npc` | `Actions.RespawnNpc` |
| `observe-messages` | `RegisterMessageObserver` (1.5) |
| `read-sessions` | `World.Session` (1.5), `World.MessageCounts` (1.6) |
| `observe-actions` | `PlayerActionAttempted`, `PlayerActionResolved`, `CooldownStarted`, `CooldownEnded`, `World.Cooldowns` (1.6) |
| `observe-visibility` | `RegisterVisibilityObserver` (1.6) |
| `observe-chat` | `PlayerSpoke` without the text (1.6) |
| `read-chat-text` | the text of `PlayerSpoke` (1.6) |
| `read-sanctions` | `Actions.AccountSanctions` (1.8) |
| an operation's name | `Actions.Invoke` of that operation (1.5), for example `player.teleport` |

The operations that replace older actions are also granted by the older name: `refresh-npc` grants `npc.refresh`,
`respawn-npc` grants `npc.respawn`, and `send-messages` grants `player.message` and `player.feedback`.

## Knubot reloads

The client learns whether an NPC talks through Knubot (`HasKnubotData`) only when the NPC enters its view.

After `Knubot.Reload()`, or any other catalog reload, every playfield does the following on its own tick:
- It compares each spawned NPC against the old and new scripts.
- When an NPC gained a script, everyone who sees it gets `HasKnubotData = 1`.
- When an NPC lost one, they get an explicit `0`.

Chats that are already open keep the script they started with. `Actions.RefreshNpc` sends the same refresh for one
NPC on demand.

## GameData reloads

Most GameData loads once at startup. A module can reload the catalogs below while the zone runs.

**The override folder.** The `AO_REBIRTH_GAMEDATA_OVERRIDE_PATH` environment variable names a writable folder laid
over GameData. A file at a relative path in it replaces the shipped file at the same relative path, for example
`<override>/MonsterCombatRules.json`. With the variable unset, or the file absent there, the shipped file is used.
For the NpcScripts folder this works per file: an override script replaces the shipped script with the same path, and
one only in the override folder is added. `AO_REBIRTH_KNUBOT_PATH` is separate and keeps its own merge rules.

**What a reload does.** `GameData.Reload(name)` reads the named catalog's files again on a worker thread, never on a
playfield tick, and swaps the result in whole. A lookup sees the old data or the new, never a mix.
- A file that fails to read or parse keeps the old data. The result has `Ok = false` and the error in `Problems`.
- An unknown name returns `Ok = false` with a problem; it does not throw.
- One reload runs at a time.

**What it reaches.** A reload changes what the next lookup sees. Anything already spawned keeps its state until it
respawns. `Actions.RespawnNpc(pf, instance)` removes an NPC as if it died (no corpse, loot or XP) and spawns its spawn
point again at once, skipping the respawn timer and chance, from the current catalogs. Every NPC of that spawn point
respawns together. It returns false, and leaves the NPC in place, when the NPC has no spawn point that respawns:
GM-spawned NPCs, pets, content placements, quest objectives and one-shot points. GMs have the same as `.npc respawn`
on the selected NPC.

| Name | Reads | Notes |
|---|---|---|
| `MonsterCombatRules` | `MonsterCombatRules.json` | Features, hates and pets. |
| `NpcTemplates` | `NpcTemplates.json` | |
| `NpcScripts` | `NpcScripts/` | A running script keeps its program until the NPC respawns. A file with a problem is left out and listed, as at startup. |
| `ItemTemplates` | `ItemTemplates.json` | Items already minted or spawned keep their template. Reloads `Knubot` too, because Knubot item conditions resolve hashes when the scripts load. |
| `Knubot` | `Knubot/` plus `AO_REBIRTH_KNUBOT_PATH` | Same as `Knubot.Reload()`. |

Everything else stays load-once, including WaterExits, PetTypes, items.dat, playfield data, navmeshes, dungeon
generation, Gameplay.json and Config.

**Instances.** Each zone instance holds its own catalogs. A module running on every instance reloads on every
instance; nothing coordinates reloads across instances.

## Message observers

`RegisterMessageObserver(observer, new MessageSubscription { MessageTypes = [...], Directions = ... })` watches N3
messages on this instance's connections (1.5). `MessageTypes` holds N3 message type ids; `AORebirth.Modules.Network.MessageTypes`
names the common ones. Observers only watch: every message is handled exactly as it would be without one.

- **Where they run.**
  - Inbound: on the connection's receive thread, in wire order, once the session is in play, before the message is
    queued for the playfield.
  - Outbound: on the connection's send thread, in wire order, just before the message is written.
  - Never on a playfield tick, never under an engine lock.
- **What an observer gets.** `OnMessage(in MessageSample sample)`, valid only during the call:
  - `SessionId` (the same id as `World.Session`), `CharacterId` (0 before one is bound), `Direction`, `MessageType`;
  - `Timestamp`: `Stopwatch.GetTimestamp()` when the bytes were read from the socket (inbound) or written (outbound);
  - `Body`: the plaintext message, header included (outbound: before compression);
  - `Fields`: the message's fields by name (below).
- **Cost.** A message type nobody subscribed to costs one dictionary lookup. An outbound message's fields are parsed
  back from its bytes only when an observer reads one.
- **Failures.** `OnMessage` must not block or allocate. One that throws is logged; after 10 failures in a row it is
  removed. One slower than 2 ms is reported, at most once a minute.
- **Logging.** Each subscription is logged at startup with its directions and types.
- **Every type (1.6).** `AllTypes = true` observes every message; `MessageTypes` is then ignored. It costs a call per
  message, so its subscription is logged as a warning. It is for tools that need everything (a packet logger,
  protocol research).
- **Counts (1.6).** `World.MessageCounts(sessionId)` (`read-sessions`) returns how many messages of each N3 type the
  connection received and sent since it opened. The engine counts every message anyway, so a module can fingerprint a
  client without observing every message. Live counters, never a snapshot; null when the connection is not on this
  instance.

### Fields

`Fields.TryGetInt / TryGetFloat / TryGetIdentity / TryGetString(name, out value)` read a property path of the engine's
parsed message: `MoveType`, `Coordinates.X`, `Target[0].Instance`. An enum reads as its integer, an identity as an
`EntityRef`. A name the message lacks, an index past the end, or a value of another kind gives false; a module treats
that as "not available". A path through a base type is read on the runtime type (`FollowTarget`'s `Info`).

These names are guaranteed (ZoneEngine_New.Tests `ModuleMessageApiTests` locks them):

| Message | Names |
|---|---|
| `CharDCMove` | `Identity`, `MoveType`, `Coordinates.X`, `Coordinates.Y`, `Coordinates.Z`, `Heading.X`, `Heading.Y`, `Heading.Z`, `Heading.W`, `AuxA`, `AuxB`, `Unknown1` |
| `Attack` | `Identity`, `Target`, `Action` |
| `StopFight` | `Identity`, `Unknown1` |
| `CastNanoSpell` | `Identity`, `NanoId`, `Target`, `Caster` |
| `CharacterAction` | `Identity`, `Action`, `Target`, `Parameter1`, `Parameter2` |
| `GenericCmd` | `Identity`, `Action`, `Count`, `User`, `Target[0]` |
| `LookAt` | `Identity`, `Target` |
| `FollowTarget` | `Identity`, `Info.MoveType`, `Info.Target` (only a follow of a target), `Info.Flag` (only a follow stop) |
| `CharSecSpecAttack` | `Identity`, `Target`, `Stat` (1.6) |
| `GenericCmd` (1.6) | `Temp1` (the accept or deny reply) |
| `Feedback` | `Identity`, `MessageId` (1.6) |
| `ChatText` | `Identity`, `Text` (1.6) |

Other names usually work too, but may change with the engine's message types.

## Sessions

`World.Session(characterId)` (1.5, `read-sessions`) returns the character's connection on this instance, or null when it
is not in play here or is link-dead: `SessionId`, `CharacterId`, `AccountName`, `RemoteAddress` and `ConnectedAtUtc`.
A zone change opens a new connection, so `SessionId` and `ConnectedAtUtc` change with it. It reads the live session,
never a snapshot, so it never waits. It needs its own capability because it carries the account and IP address.

## Operations

`Actions.Invoke(name, target, args)` (1.5) runs a named operation the engine registers; `Actions.Operations` lists them,
with their target kind, parameters, the capability they need and whether this module is granted it. New operations are
engine additions and don't change the module API.

- **Where they run.** Player and NPC operations on the target's playfield tick, account operations on a worker thread.
  The task completes after the operation ran.
- **Results.** `Invoke` never throws for an unknown name, a wrong target or bad arguments; the result's `Code` says why:
  `unknown_operation`, `bad_target`, `bad_args`, `not_here` (not on this instance or not in play) or `failed`. It throws
  `UnauthorizedAccessException` when the module is not granted the operation.
- **Arguments.** `OperationArgs.Of(("x", 1.0f), ...)` holds int, float, bool and string values. A float parameter also
  takes an int.
- **Declaring them.** module.json may list `"Operations": ["player.kick", ...]`. A listed operation this engine does not
  offer, or that Modules.json does not grant, fails that module's load (the zone's, when it is `Required`).
- **Logging.** Every invoke is logged with the module, operation, target, arguments and result.

| Operation | Target | Arguments | What it does |
|---|---|---|---|
| `npc.refresh` | NPC | | As `RefreshNpc`. |
| `npc.respawn` | NPC | | As `RespawnNpc`. |
| `player.message` | character | `text` | As `SendSystemMessage`. |
| `player.feedback` | character | `id` | As `SendFeedback`. |
| `player.teleport` | character | `playfield`, `x`, `y`, `z` | Within the character's playfield, a soft teleport as `.tp` does. To another playfield, the normal playfield transfer (to another instance too), with no exit-proxy return. An unknown playfield is `bad_args`. |
| `player.kick` | character | `reason` (optional) | Logs the character out as a client logout does, with the normal logout save. |
| `account.ban` | account | `reason` (optional) | Bans the account permanently. From 1.8 this is a sanction row (see Account sanctions) instead of `login.Flags`, and the account's characters in play are removed. |
| `account.suspend` | account | `seconds` (60 to ten years), `reason` (optional) | Suspends the account for that long; it ends by itself. The account's characters in play are removed (1.8). |
| `account.unban` | account | `reason` (optional) | Lifts every active suspension and ban, and a pre-1.8 `login.Flags` ban. Fails when there is none (1.8). |
| `server.shutdown` | none | `seconds` (1 to 86400), `message` (optional), `reason` (optional) | Shuts this zone instance down the normal way (players saved) after a countdown, as `.shutdown` does: every online player is told at the start and at the usual checkpoints (1 hour ... 1 second), with `message` after each line. Scheduling again replaces the countdown (1.7). |
| `server.shutdown-abort` | none | `reason` (optional) | Cancels a scheduled shutdown and tells online players; fails when none is scheduled (1.7). |

## Account sanctions

Suspensions and bans live in `account_sanctions` (1.8). A row blocks the account while it is not lifted and has not ended; a ban has no end.

- **Where they apply:** LoginEngine and the account broker refuse a sanctioned account, and the zone refuses its handoff ticket.
- **Characters in play:** issuing one removes the account's characters on this instance at once (they are told why). Every instance also checks its players once a minute, so a sanction issued on another instance, or written to the database directly, removes them within a minute.
- **Issuer:** a row records who issued it: `gm:Name` for a GM command, `module:Id` for a module (set by the engine, not by the module).
- **GM command:** `.account info <target>`, `.account suspend <target> <duration> [reason]` and `.account history <target>` (level 100), `.account ban <target> [reason]` and `.account unban <target> [reason]` (level 1000). A target is an account or character name, or `account:NAME` / `char:NAME`. Durations combine s, m, h, d and w, such as `1d12h`.
- **`Actions.AccountSanctions(account, limit)`:** the account's rows, newest first, as `AccountSanctionInfo` (times UTC, `EndsAtUtc` null for a ban, `Active`). Needs `read-sanctions`.

## Clock and timestamps

`Clock` (1.6) is `Stopwatch.GetTimestamp()`, the clock `MessageSample.Timestamp` and every event's `Timestamp` use.
`Now` and `Frequency` give the current tick and ticks per second, so an interval is a subtraction divided by
`Frequency`. `ToUtc` and `FromUtc` convert through one offset taken when the zone started: they agree with each other
but drift from wall time by the system clock's adjustments, so use them for intervals, not calendar dates.

## Actions and cooldowns

`PlayerActionAttempted` and `PlayerActionResolved` (1.6, `observe-actions`) report every request a player makes to use
something: an item (`ItemUse`, `ItemUseOn`), a nano (`NanoCast`), a special attack, a perk, or the start of a fight
(`FightStart`). They are raised where the engine decides, so they report what the engine did, not what the client asked.

- **Attempted** is raised once per request: `Started` (runs now), `Queued` (behind the use queue or a cast) or
  `Refused`, with an `ActionRefusal` (`Cooldown`, `Busy`, `QueueFull`, `Requirements`, `Resources`, `Target`, `Range`,
  `LineOfSight`, `State`, `Other`). A `Cooldown` refusal names the lock (`Cooldown`) and how long it still had (`ReadyIn`).
  A request the engine drops without deciding (a malformed message) raises nothing.
- **Resolved** is raised once for every `Started` or `Queued` request, when it ends: `Executed`, `Interrupted`,
  `Missed`, `Resisted`, `Failed` (refused when it came to run) or `Cancelled` (replaced, or the player left first).
- `AbilityId` is the item's template id, the nano id, the special's stat id or the perk id; `Item` carries the item.
  `Detail` is the engine's own reason for logs, not a stable contract.
- Auto-attack swings are the server's own timer, not requests; they appear in `CombatResolved`.

`CooldownStarted` and `CooldownEnded` (1.6, `observe-actions`) report locks on players: `SkillLock` (by skill stat),
`NanoRecharge` (caster-wide), `NanoLine` (by strain) and `PerkLock` (by perk).

- **Started** fires when a lock is set, with the server's `Duration` after modifiers, the `Deadline` on `Clock`, the
  action that caused it, and `ClientDuration`: what the client was told, or null when it is told nothing. The client is
  told the unmodified length of a skill lock, so the two can differ.
- **Ended** fires on the tick that clears it, `Expired` or `Cleared` early (a reset, a GM, death rules). For a lock with
  an "available" message (SpecialAvailable, PerkAvailable) it fires on the tick that sends it, with `ClientNotified`.
- Locks restored at login or a zone change raise no `Started`; `World.Cooldowns(characterId)` lists every running
  lock, those included. It follows the snapshot rules of `Stat`.

## Visibility

`RegisterVisibilityObserver(observer, new VisibilitySubscription { EntityTypes = [...] })` (1.6, `observe-visibility`)
is told when the server tells a client that an entity exists, and when it stops: the moment a player could first have
seen something, which is not the entity's spawn. An empty `EntityTypes` means NPCs only.

- **Where it runs.** On the playfield tick, where the engine adds an entity to or removes it from a player's
  interest set, as the spawn or despawn message is queued to that client.
- **What it gets.** `OnVisibility(in VisibilitySample sample)`: `CharacterId` (whose client), `PlayfieldId`, `Entity`,
  `Visible`, `Reason` (`Spawned`, `EnteredRange`, `PlayerArrived`, `Despawned`, `LeftRange`, `PlayerLeft`) and
  `Timestamp`.
- **Cost.** It is the highest-volume hook, so it is an observer with a struct sample rather than an event. With no
  observer it costs one field read. `OnVisibility` must not block or allocate; failures and slow calls are handled as
  for message observers.

## Item moves

`ItemMoved` (1.6) reports every item changing place for a player, so loot, pickups, trades, shops, the bank and
rewards look the same: `Item`, `Count`, `From` and `To` (an `ItemPlace`: the kind, and its owner or container such as
the corpse, the shop NPC or the other trader). `From None` means created (a reward, a GM spawn) and `To Destroyed`
deleted or consumed. Rearranging slots within one place of one character is not raised.

## Chat

`PlayerSpoke` (1.6, `observe-chat`) reports chat the zone handles: the channel (`Vicinity`, `Say`, `Shout`, `Whisper`,
`Emote`, `Team`, `Other`) and the text's `Length`. `Text` is filled only for a module that also has `read-chat-text`,
because chat is personal. Tells and channels go through the chat server and are not seen here.

## Combat

`CombatResolved` (1.6) reports every resolved hit or miss with at least one player as attacker or target: `Attacker`,
`Target`, `Source` (`AutoAttack`, `Special`, `Nano`, `Item`, `Perk`, `Pet`, `Environment`), `SourceId`, `Result`
(`Hit`, `Critical`, `Miss`, `Evaded`, `Resisted`, `Blocked`), `Amount` (after absorbs) and `DamageType`. Fights
between NPCs are left out to keep the volume down.

## World additions (1.6)

- `PlayerInfo` adds `InCombat`, `IsSitting` and `IsMoving` (from the motor).
- `NpcInfo` adds `SpawnedAt` (on `Clock`) and `Hostile` (attacks players on sight).
- `MessageTypes` adds `CharSecSpecAttack`, `Feedback`, `ChatText`, `SpecialAttackInfo`, `MissedAttackInfo` and
  `TemplateAction`.

## World additions (1.7)

- `PlayerInfo` adds `TeamId` (0 when not in a team), `TeamLeader` and `OrgId` (0 when not in an organization).
  Two players share a `TeamId` exactly when they are in the same team; it is stable while that team exists, and a
  team re-formed later gets a new one. Same snapshot rules as the other fields (`read-world`, up to one tick stale).
  There is no team-change event: compare `TeamId` between snapshots.
- `server.shutdown` and `server.shutdown-abort` drive the engine's own timed shutdown (the `.shutdown` GM command), so a
  module's countdown and a GM's are the same one: either can replace or cancel the other. Each needs its own name as a
  capability. They shut down this zone instance only.
