# Web identity as a zone module: plan

Status: proposal. The module API is Lost Eden's contract (`ZONE_MODULE_API.md`); this plan needs Mike's and
TylerDelmus's agreement before any of it lands, together with `DAILY_LOGIN_MODULE_PLAN.md`, which wants the next
module API version too ("1.11" below is a placeholder). Nothing here changes what a game window does until its page
starts asking.

## Why

The game's web windows, as GUI.dll opens them, send who the player is in their headers: Daily Rewards
(`http://dailyrewards.anarchy-online.com/`), the Item Store (`http://aoshop.funcom.com/shop/`), the GMI
(`http://aomarket.funcom.com/market/`), Customer Service (`http://aopetition.funcom.com`, the `/petition` window),
and a bug report window a client plugin can open the same way for `/bug`. A page that shows or changes a player's
own things needs to know that the request really comes from that player, and today nothing can tell:

- the headers are claims: anyone can send them to the web server without a game client;
- the only secret in them, the cookie, is known only to the zone the character plays on, and no page can ask it;
- so every page either trusts a header (and anyone can read or act as anyone) or needs a game action to confirm it
  (Daily Rewards' action 263), which most windows don't have: `/bug` and `/petition` stay in the client, and a page
  listing a player's earlier reports, with staff replies, is a read with no action at all.

As a module, checking a window's request is one small piece every page can use, granted and switched off like any
other.

## What the game windows have today

| Part | Where | What |
| --- | --- | --- |
| Headers | GUI.dll, for the hosts it trusts (`http://aoshop.funcom.com/*` and the others) | `X-Platform`, `X-Anarchy-Cookie`, `X-Anarchy-PlayerID`, `X-Anarchy-CharacterID`, `X-Anarchy-DimensionType`, `X-Anarchy-ServerID`, `X-Anarchy-Appearance` with each request, page loads and the page's own XHRs alike (captures of the Item Store window, 2026-10-25 and 2026-11-14). |
| Where they go | the URL's host and port | Plain HTTP to the window's host on port 80, resolved by name. A client patch that points those names at a server's web host changes only the name's address, never the port: the requests reach the web server, not the zone engine. |
| What the cookie is | GUI.dll imports `Client_t::s_nCookie1` / `s_nCookie2` and formats them `"%u %u"` | The client's own login cookie pair, the one it presents to the zone at handoff: the same secret the zone holds. |
| `X-Anarchy-ServerID` | the client | The playfield the character is in: 4582 (ICC Shuttleport) from a character standing there on an AORebirth server, 2026-11-14 (retail sent 3505 and 3516 for one account on one dimension). A playfield, not a zone address: which instance runs it is World's to know, and it changes as the player zones. |
| Query values | the shop page's script | Funcom's shop adds `subscription_id`, `character_id`, `dimension` and a 40-hex `session_id` to its XHRs (`/items/?...`). None of them is in GUI.dll: they come from Funcom's page and its own web session, so they prove nothing the headers don't. |
| The cookie | `ZoneLoginHandler`, `ZoneSession.BindZoneHandoff` | The pair the client entered the zone with, kept on its session (`_handoffCookie1/2`); a zone transfer presents the same pair. |
| The session | module API `World.Session(characterId)` (1.5, `read-sessions`) | `AccountName`, `RemoteAddress`, `ConnectedAtUtc` of the live connection on this instance; null when not in play here or link-dead. |
| Asking a zone | `ServiceInstance.RequestAsync` (WorldEngine), `WorldLink` (zone) | World sends a typed `ServiceRequest` down an instance's Registry stream and waits for its `ServiceReply` (`ReleaseCharacter`, `LocateCharacter`, `AdmitClient`; the last is how the handoff hook, 1.4, is reached). |
| Finding the zone | `Directory.Locate`, the leases; `Directory.Route` | World knows which instance owns a character, and routes fire-and-forget `InstanceCommand`s there (`WorldCommands.Register`). |
| Who may call World | `TokenInterceptor`; `RequireLoginServer` (4950e37b) | Every call carries the world token; the `Session` service also requires a login-only token, so a zone or chat engine can't mint zone tickets. |
| Daily Rewards | `tools-temp/icc-rk-local-web/daily/claim.php`, `CharacterActionMessageHandler` | The page treats the headers as routing hints only and writes a pending claim request; the client's CharacterAction 263 on the zone session owns the grant (`DAILY_LOGIN_MODULE_PLAN.md`). |

The property that matters most: the cookie is a secret only the client and the zone it plays on hold. A check that
asks that zone, for that character, from the address the zone sees the client at, in the playfield the character
stands in, can't be passed with copied headers from elsewhere, stops passing the moment the character logs out, and
stops passing for headers taken before the character's last zone change.

## What the module API lacks

1. **Being asked from outside the zones.** A module can answer GM commands, handoffs and game messages, but nothing
   outside the zone engines can put a question to a module and get its answer.
2. **Checking a cookie.** `World.Session` gives the account and address, but not the cookie, and shouldn't: a module
   needs a yes or no, not the secret.
3. **A web caller World can tell apart.** Anything holding the world token can call every World service; a web
   server needs one call, and nothing else.

## Module API 1.11 additions

### A. World commands

A module answers named requests that reach it through World, from the instance that owns the character.

- **World:** `Directory.Ask(AskRequest) returns (AskReply)`. World finds the instance that owns `character_id` (as
  `Route` does) and sends it a new `ServiceRequest` body, `ModuleCommand { name, character_id, arguments
  (map<string, string>) }`, with `RequestAsync`, waiting up to 5 s for `ModuleCommandReply { handled, ok, code,
  values (map<string, string>) }`. Its own codes: `not_in_play` (no owner), `timeout`, `no_handler`.
- **Zone:** `WorldLink` answers `ModuleCommand` by name from a registry the module host fills, on a worker thread (a
  handler that needs the tick hops onto it, as `Actions` does).
- **Module API:** `RegisterWorldCommand(string name, IWorldCommandHandler handler)` (capability
  `answer-world-commands`, only during `Initialize`). A name another module registered fails startup. The handler
  gets `WorldCommandRequest { Name, CharacterId, Arguments }` and returns `Task<WorldCommandReply>`.
- Names are namespaced (`web.verify`) and World passes them through without knowing them: a new question is module
  work, not World work.

### B. Session verification

`World.VerifySession(characterId, cookie1, cookie2)` (capability `verify-sessions`) returns `SessionVerdict { Ok,
Reason, AccountName, CharacterId, RemoteAddress, PlayfieldId, PreviousPlayfieldId, PlayfieldSinceUtc }`.

- `Ok` only while the character is in play on this instance, not link-dead, and the pair matches the session's, in
  constant time.
- The cookie never leaves the zone: the verdict carries none.
- `PlayfieldId` is the session's playfield now (live, not the tick snapshot `PlayerInfo` has); `PreviousPlayfieldId`
  and `PlayfieldSinceUtc` say where it was before its last zone change and when that was.
- Reasons: `not_in_play`, `link_dead`, `mismatch`.

### C. The web caller

- A web token of its own (`AORebirth.World.WebToken`, header `x-aorebirth-web-token`) that `Directory.Ask` requires,
  checked as `RequireLoginServer` checks the login token.
- The world token alone is not enough, so a zone or chat engine can't ask; and the web server gets only this token,
  not the world token's other powers.

### D. Smaller pieces

- All of it is additions: a 1.10 module loads unchanged (`ZONE_MODULE_API.md`, Versioning).
- Arguments and values are string maps, so World doesn't change for each new command; typed messages per command
  would be stricter.
- `Ask` goes to the owning instance only; nothing here needs a broadcast ask.

## The WebIdentity module

A new project, `AORebirth/Modules/WebIdentity`, built against the Abstractions only.

- `Initialize` registers `web.verify`: arguments `cookie1`, `cookie2`, `address` (the address the web server saw the
  request from) and `playfield` (the request's `X-Anarchy-ServerID`).
- It calls `VerifySession` and answers `ok` with `account` and `character` only when the verdict is `Ok`, `address`
  equals the session's `RemoteAddress`, and `playfield` is the session's playfield; otherwise the reason
  (`address_mismatch`, `playfield_mismatch`).
- **A zone change in flight:** a page loaded just before the character zoned sends the old playfield on its next
  XHR, so `playfield` may also be `PreviousPlayfieldId` for 10 seconds after `PlayfieldSinceUtc`.
- **Config:** none needed; `GetConfig` may later turn the address check off for a server whose players reach the web
  server and the zone from different addresses, or change the 10 seconds.
- **Capabilities:** `answer-world-commands`, `verify-sessions`.

### The web server

- A client for `Directory.Ask`, generated from `world.proto` (not copied), with World's address and the web token.
- For each request from a game window: `Ask("web.verify", characterId, {cookie1, cookie2, address, playfield})`,
  the playfield being the request's `X-Anarchy-ServerID`. On `ok` the page knows the account and character;
  otherwise the reason. When World can't be reached, nothing personal is shown or written.
- The address is the request's own when nothing stands in front of the web server; behind a proxy, the proxy's
  `X-Forwarded-For`, only when the proxy is trusted.
- A verdict is cached for that character, cookie pair and playfield for 60 seconds, so a page and its form post don't
  ask twice; a zone change asks again (another playfield).
- A page never sends an account: the account is the one `web.verify` answers with.

### Daily Rewards: verified first, then its own flow

The Daily Rewards window sends the same headers as every other window, so it is checked the same way, and then
`DAILY_LOGIN_MODULE_PLAN.md`'s flow runs:

1. **Every request from the window is verified first.** The page asks `web.verify` before it reads or writes
   anything. A request that isn't `ok` gets nothing: no board, no receipt, no pending request written.
2. **Then the existing flow runs unchanged.** A verified request may write the pending claim request, as now, for
   the account and character `web.verify` named (not the ones the page or its query sent). The client's action 263
   on the zone session still owns the grant: the zone reads the pending request for that session's account and
   character, grants under the persistence gate, and writes the result.
3. **Reads of the board and receipts** answer only for the verified character.

A claim then needs both: a verified web request (the player's own cookie, from their own address, in the playfield
they stand in) and the game's own action on the same session; neither alone grants anything. What
`DAILY_LOGIN_MODULE_PLAN.md` adds to the module API (module storage, `player.grant-items` with the
claim in the same transaction as the items, message handlers for action 263) is unchanged by this.

Its request and result files need the web page and the zone on one disk, which a server running them in separate
containers or machines doesn't have. To weigh: the pending request and the result could go through `Directory.Ask`
to the module instead, without changing who owns the grant (still action 263).

## Moving existing data

None: nothing is stored. A server that runs no modules yet ("no Modules.json; no modules loaded") gets a
`Modules.json` granting WebIdentity its two capabilities and the module's folder; World and the web server get the
web token, made once like the world token. Worth setting at the same time: the login-only token (4950e37b), which
World warns about when it is missing.

## Order of work

1. **API 1.11, engine side:** `Directory.Ask` and the `ModuleCommand` body (A), `RegisterWorldCommand`, the web
   token (C). Tests for routing to the owner, `not_in_play`, `no_handler`, the timeout, and a call with the world
   token alone refused.
2. **`World.VerifySession` (B) and the WebIdentity module.** Tests for `ok`, `mismatch`, `link_dead`, after logout,
   `address_mismatch`, `playfield_mismatch`, and the previous playfield accepted for 10 seconds after a zone change
   and not after; the verdict never carries a cookie.
3. **The web server asks.** A game window's page knows the account and character; copied headers from another
   machine are refused, and so are headers from before a zone change once the 10 seconds have passed. In the game:
   open a window in a building, an instanced dungeon and after a zone change, and check `X-Anarchy-ServerID` follows
   the playfield each time.
4. **The pages:** Daily Rewards verified first (above), the bug report and Customer Service windows with the
   player's earlier reports, later the GMI.

## Risks

- **The address check.** A player whose game and web window reach the server from different addresses (an IPv4 and
  an IPv6 path, a proxy in between) is refused. That is the check doing its job against copied headers, but it needs
  watching in the logs; the module config is the way out.
- **The playfield check.** If `X-Anarchy-ServerID` turns out not to follow every zone change (an instanced dungeon, a
  door proxy shown as its interior), a player there is refused; the logs show it as `playfield_mismatch` with both
  values, and the check can then compare what the client really sends.
- **Plain HTTP.** The windows use plain HTTP, so the cookie can be read on the player's network; the address check
  and the live session bound what a reader can do with it. HTTPS for the windows (a client plugin) removes it.
- **The cache.** A verdict cached for 60 seconds outlives a logout by up to that long; pages that change something
  ask again instead of using the cache.
- **World commands are a new power.** Something holding the web token can put any registered name to a module; each
  handler checks its own arguments, and only names a module registers exist.
- **Load.** Every page view asks World and a zone; the cache and the 5 s timeout bound it, and a slow zone answers
  `timeout`, never a guess.
