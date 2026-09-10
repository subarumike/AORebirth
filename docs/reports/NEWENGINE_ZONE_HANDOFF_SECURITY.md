# LoginEngine to NewEngine admission security

## Result and scope

The direct unauthenticated character-admission flaw is repaired on
`codex/newengine-production-cutover-001`, starting at
`b87faf8b6de31d22f79d8f469990c27592bab6f9`. Final exact-source acceptance passes
at `75a78a88535bffc321fe82c5ab824852c4636b47`: 505 NewEngine tests, 1129
AOtomation tests, 12 mandatory gates, Windows acceptance, disposable database,
connected security/lifecycle and Linux publication. Binary hashes and proof
boundaries are recorded in `NEWENGINE_CUTOVER_VALIDATION_RECEIPT.md`.
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
| EventServerType | 0x12 | not returned in ZoneLogin |
| PlayerID | 0x16 | not returned in ZoneLogin |

ZoneInfo is System family 1/type `0x17`, 46 bytes including its header. ZoneLogin
is family 1/type `0x1b`, 32 bytes including its header, destination 2. The
serializer models the complete ZoneInfo body and both returned uint32 cookies;
exact fixtures verify both envelopes and round-trip bytes.

The completed official-retail capture is
`tools-temp/live-pcaps/retail-handshake/20260910-041844-14c0d788/network.pcapng`,
SHA256 `8F38383A7727DE8142B3182FD2717E84882B4D0FAE95934330B65CD93EB79171`.
Frame 788 is a type-0x17 ZoneInfo with a 26-byte body: selected character
`0x0D904118`, endpoint `37.18.193.20:7501`, EventServerType 1 and PlayerID
`0x59E3C875`. All four observed admissions begin with the 32-byte type-0x1b
ZoneLogin and reuse the same character/cookie tuple.

Three captured redirects change the endpoint to `37.18.193.56:7514`,
`37.18.193.20:7506` and `37.18.193.20:7509`. In each case the client closes the
old stream, opens the advertised stream and sends type-0x1b with the same
cookies; it does not return to LoginEngine. Recovered static client evidence at
commit `67aa6f36b030a99c73736acbc33bb3ec301bce10` independently establishes the
26-byte layout, the type-0x3c IP/port body, cookie reuse, and that unexpected
connection loss exits to the launcher instead of automatically resending.

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

Initial admission needs a fresh unconsumed ticket. After that ticket is consumed,
the admitted `ZoneSession` may authorize one redirect to the exact configured
destination endpoint immediately before it serializes and sends the playfield
transfer and type-0x3c redirect. The allowance is durable, expiring, endpoint
bound and consumed atomically. Wrong endpoints, unarmed reconnects, concurrent
reuse and replay reject. A successful destination admission may later arm the
next sequential redirect. A failed authorization prevents the transfer from
being sent.

Fresh successful authentication invalidates the account's older outstanding
initial tickets and prevents an older authenticated login socket from issuing new
ones. It does not invalidate the redirect authority of the currently admitted
session. Login TCP closure does not immediately revoke an initial ticket; its
short expiry and generation still apply. Existing login Online cleanup is
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
16 concurrent claims. Redirect tests cover no-arm rejection, target binding,
sequential rearm, expiry, concurrency and admitted-session generation behavior.
Wire tests verify the exact 32-byte ZoneLogin and 46-byte ZoneInfo envelopes.
The playfield-transfer fixture proves that the real send path arms authority and
that exactly one matching claim succeeds.

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

1. **Official client against AORebirth.** Run login, selection, initial world
   entry and at least two zone changes against an isolated deployment of the
   candidate. Capture the run passively and compare the emitted ZoneInfo,
   type-0x3c endpoint and each type-0x1b admission. This is the remaining
   end-to-end runtime gate; the official-server behavior itself is already known.
2. **PlayerID semantics.** The retail frame contains a nonzero value distinct
   from the selected character, but recovered client code only stores/exports it
   and no consumer establishes its meaning. AORebirth sends zero in the correct
   field. Do not substitute a character, account or session identifier without a
   direct consumer bridge. EventServerType is emitted as the captured value 1.
3. **Connection loss.** Static client evidence says unexpected loss returns to
   the launcher and does not automatically resend cookies. The server therefore
   accepts only an explicitly armed transfer. No generic cookie-based reconnect
   is required or permitted by this evidence.
4. **Deployment review.** Confirm both deployed services use the same protected
   authority directory and identity, then prepare exact-SHA cutover and database
   restore rollback. NewEngine writes still make executable-only Legacy rollback
   unsafe; a matching database restore remains required. After official-client
   staging passes, merge the Mike-owned cutover branch by exact SHA and deploy in
   a maintenance window. Remove Legacy only after the NewEngine release has
   completed burn-in and rollback has been rehearsed. DAO consolidation remains
   incomplete: the deterministic inventory has 99 persistence method rows and
   the architecture guard has seven reviewed Legacy baseline SQL exceptions.
