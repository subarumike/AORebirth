# LoginEngine to NewEngine admission security

## Result and scope

The direct unauthenticated character-admission flaw is repaired on
`codex/newengine-production-cutover-001`, starting at
`b87faf8b6de31d22f79d8f469990c27592bab6f9`. Development connected acceptance
passes. Final exact-source results belong in `NEWENGINE_CUTOVER_VALIDATION_RECEIPT.md`.
Master, the other developer's branch and production are unchanged. Legacy remains
present; this task does not perform the full DAO conversion.

## Evidence and wire contract

Mike authorized read-only evidence inspection of
`C:\Users\Mike\Documents\AO stripdown\Docs\AO_RB_LOGIN_ZONE_HANDSHAKE_001.md`,
reported evidence commit `2ef0f9fc03c34025460d14d4c9787b42db222bda`, and its
referenced Interfaces.dll evidence. That workspace was not modified. The report
identifies retail `18.8.62_EP1`, Interfaces.dll SHA256
`3aa79a44e76c3413543404058c5d07323bd3b69f4c3493c7b136befa1a55b0a7`.
This hash is attributed to the recovered report, not a new binary hash audit.

`Client_t::ProcessMessage` RVA `0x2a9e`, branch VA `0x10002d11..0x10002db0`,
stores ZoneInfo cookies at globals `0x10032910` and `0x10032914`.
`Client_t::SendClientCookie` RVA `0x13b0` sends them unchanged. All values below
are big-endian. No extra client account field or guessed LoginKey is needed.

| Field | ZoneInfo semantic body offset | First ZoneLogin full-frame offset |
| --- | --- | --- |
| Selected character | 0x00 | 0x14; also header sender at 0x08 |
| Cookie 1 | 0x0a | 0x18 |
| Cookie 2 | 0x0e | 0x1c |

ZoneInfo is System family 1/type `0x17`; ZoneLogin is family 1/type `0x1b`,
32 bytes including its header, destination 2. The new serializer contract adds
both uint32 cookies; a synthetic exact 32-byte fixture verifies round-trip bytes.
It is labelled static-layout evidence, never a packet captured from retail.

The complete available recovery report explicitly records no complete retail
login-to-zone capture. Existing AOSharp captures begin after this boundary.
The clean-room client trace corroborates its own behavior only. Neither is
silently promoted to original-client runtime proof. No additional capture is
needed to invent or discover the returned-cookie mapping: that mapping is proven.

## Server authority and lifecycle

`ZoneHandoffStore` owns server authorization under
`AO_REBIRTH_SESSION_OWNERSHIP_DIR/zone-handoffs-v1`. Both engines must share this
directory as the same trusted service identity on the same host. Existing Linux
LoginEngine and NewEngine units already set the parent to
`/var/lib/ao-rebirth/session-ownership`. Windows defaults to the current service
user's LocalApplicationData/AORebirth/session-ownership; different identities
need explicit shared configuration and appropriate ACLs. Linux creates mode 0700
and rejects existing group/other-accessible handoff directories. The parent must
also be operator-owned and protected from replacement by untrusted users.

Only successful credential verification rotates an account's login generation.
Selection first checks authenticated account ownership, then issues a nonzero
CSPRNG 64-bit opaque value split across the two measured uint32 cookie slots.
Only its SHA256 digest is stored, with account, selected character, generation,
issuance, expiry and consumed state. There is one current record per character
and generation record per account, not an unbounded record for every attempt.
Cookie material and login credentials are omitted from network debug logs.

Default lifetime is 30 seconds. The existing handoff timeout setting accepts
5–120 seconds; invalid settings use 30. Claim rejects time before issuance and
time at/after expiry. Tests inject a clock; they do not sleep until expiration.

Claim verifies digest, character, lifetime, consumed status, current login
generation, and the current database account through existing read-only
`IMissionDao.ResolveCharacterAccountKey`. It serializes validation and consumption
under an in-process monitor plus a cross-process byte-range file lock, then
flushes and atomically replaces the consumed record before returning success.
No in-memory-only whitelist, Online flag, source IP, player lookup or login
history authorizes a new socket. The gate runs before FindPlayer, hydration,
mission restoration and reconnect ownership. Storage/lookup failure rejects.
An interrupted admission after consumption needs fresh authentication.

Each new zone TCP admission needs a fresh unconsumed ticket. Fresh successful
authentication invalidates the account's older outstanding tickets and prevents
an older authenticated login socket from issuing new ones. Already admitted
sessions retain their existing ownership lifecycle. Login TCP closure does not
immediately revoke a ticket, avoiding an invented disconnect-order requirement;
its short expiry and generation still apply. Existing login Online cleanup is
retained, with the unsafe unconditional SetOffline before issuance removed.

Clean ZoneEngine restart preserves consumed/expired rejection and permits a
still-valid unconsumed ticket. File state is not a distributed authority for
independent hosts, and restoring old authority files from backups is unsupported.
This proof concerns process restart, not storage rollback or power-loss durability.
If the shared directory is unavailable, admission fails closed.

## Validation

Unit tests cover missing/unknown, selected character/account, wrong current
database account, exact expiry, future issuance, consumed persistence, outstanding
ticket reload, newer login generation, stale issuer, lookup/storage failure and
16 concurrent claims. The wire test verifies the recovered 32-byte envelope.

The disposable connected fixture runs actual LoginEngine and NewEngine processes
against an owned MySQL database. It rejects missing/random/unknown/altered,
expired, wrong-character, wrong-account, stale and malformed-header handoffs.
Every rejected attempt compares hashes of every table before and after, without
printing credentials or database values. Eight simultaneous sockets reuse one
real LoginEngine ticket: one admits, seven reject. The winner completes world
entry with exact inventory, credits, nanos, morph and both mission journals.
Replay is rejected while that owner is in play, after logout, and after restart.
An outstanding ticket issued before restart also admits and logs out normally.

The original connected inventory mutation, save, fresh-login reconnect, clean
process restart, durable reload, morph cancellation and subsequent reconnect all
remain required. Expired negative setup uses an injected past clock before any
engine starts; positive tickets always come through real credential verification.
No fixture administration changes running engine state. Exact build, test,
transaction and schema results are recorded separately in the validation receipt.

## What is still needed, and how to get it

1. **Retail initial-entry runtime evidence.** One completed passive PCAP/PCAPNG
   beginning before character selection and ending after world entry, with the
   exact client build/hashes, endpoints and both stream IDs. Compare ZoneInfo
   body +0x0a/+0x0e to first ZoneLogin frame +0x18/+0x1c. Mike controls the client;
   Codex can analyze the completed trace. No client patch or authentication
   bypass is required. If transport hides bytes, the recovery report names
   ProcessMessage and Send_i observation points for an authorized debugger trace.
2. **Remaining ZoneInfo fields.** Retail reads a 26-byte body, including
   EventServerType at +0x12 and PlayerID at +0x16. The existing AORebirth emitter
   models only the first 18 bytes. This task does not invent values for the
   trailing fields. A full type-0x17 frame plus downstream source/decompiler
   consumers must establish the values and semantics before claiming complete
   18.8.62 wire compatibility. The synthetic connected client cannot prove this.
3. **Redirect and connection-loss reconnect.** The recovered type-0x3c path
   reuses cookies; generic TCP-loss retry behavior remains unresolved. The new
   boundary intentionally rejects reuse on a fresh socket. Observe those
   specific transitions and define a separately authorized ownership-transfer
   protocol if required. Tested reconnect here is fresh LoginEngine authentication;
   in-process NewEngine playfield transfer is not a fresh zone admission.
4. **Deployment review.** Confirm both deployed services use the same protected
   authority directory and identity, then prepare exact-SHA cutover and database
   restore rollback. This task performs no live deployment. NewEngine writes
   still make executable-only Legacy rollback unsafe; a matching database restore
   remains required. Gameplay gaps, Legacy extraction and DAO consolidation are
   separately scoped follow-ups, not hidden prerequisites for these security tests.
