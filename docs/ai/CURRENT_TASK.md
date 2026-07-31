# Current Task

## Active

### Pet owner SystemChat (capture 20260731-005116) — ISCom link fixed; client lines NOT yet proven

**Do not claim fixed until Mike sees the 6 owner lines after restart-engines + full login, and ChatEngineLog shows `DistributeSystemChat ok … wire=0024…`.**

Clarification (Mike): these are **owner-only** type-36 SystemMessages (CharacterId-targeted). They may appear under Default Window tab labeled [Vicinity]; they are **not** playfield/vicinity broadcast.

#### Exact owner-only lines (`{Owner}'s pet, {PetName}: …`)

| Trigger | Line |
|---------|------|
| Spawn | `Hello master. I'm ready to obey your commands...` |
| Follow | `I will follow you wherever you go, master.` |
| Behind | `I will stay out of it until you need me again, master.` |
| Wait | `I will wait here.` |
| Guard | `I will protect you to the best of my ability.` |
| Attack | `Charge!` |

Attacked-by FormatFeedback (Zone N3, keep second `s` before `\x1e`) is separate and must not regress.

#### Prior failure (proven)

- Zone `PetSystemChat: ISCom disconnected` while ChatEngineLog frozen ~00:05 (no `ISCom ready`, no `DistributeSystemChat`).
- Earlier zombie: Zone `PetSystemChat sent` into half-open TCP after ChatEngine died.

#### ISCom architecture (this pass)

- ChatEngine **listens** ISCom on `CommPort` **6996** during `InitializeISCom` (before chat client port 7012).
- Zone **dials** `ChatIP:CommPort` from connector thread; keepalive ping every 5s.
- `start-engines.ps1`: ChatEngine first → wait 6996+7012 → Zone → require Established on 6996.

#### Code this pass

- `IsConnected` side-effect free (Poll detect only; no socket replace in getter — that raced ReceiveAsync).
- Fresh socket + TCP keepalive (5s/1s) on Connect; `ResetForReconnect` before redial.
- ChatEngine: silent Ping; `DistributeSystemChat` owner CharacterId only; `ISCom ready` log.
- `SystemChatMessage` in Communication.dll; DynamicMessage type resolve hardened.
- Attacked-by FormatFeedback still `…s{0}{1}s{2}{3}~` (second `s` before `\x1e`).

#### Log proof after rebuild + restart-engines (link only — not pet delivery)

- ChatEngineLog: `ISCom ready; SystemChatMessage type=… asm=AORebirth.Communication` @ 01:15:22
- ZoneEngineLog: `Trying to connect…` then `ISCom connected to ChatEngine` @ 01:15:31
- start-engines: `Zone-ChatEngine ISCom link established on port 6996`
- Engines stopped after verify (`stop-engines.cmd`). **No** `DistributeSystemChat ok` / client pet lines yet — needs Mike login + pet commands.

#### Mike retest

1. `cmd /d /c restart-engines.cmd` (expect `ISCom ready`, `ISCom connected`, Established 6996)
2. Full logout/login (chat client on ConnectedClients)
3. Spawn → Follow → Behind → Wait → Guard → Attack
4. Pass: owner sees 6 lines **and** ChatEngineLog `DistributeSystemChat ok … wire=0024…` **and** Zone `PetSystemChat sent`
5. Fail: Zone `ISCom disconnected` / ChatEngine miss / ok log but no orange → next capture
