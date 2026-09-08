# ZoneEngine_New team reconciliation

Scope: child branch `codex/zoneengine-new-gameplay-reconciliation`, infrastructure
baseline `6dab287bd52590d7b9c0e7054bef95160a1594ba`. Windows source authority;
the implementation, shared source tables, and tests are platform-neutral.
No production, client, database, migration, or networking changes belong to this slice.

## Evidence inspected

- Compiled Legacy `AORebirth/Server/ZoneEngine/Core/Controllers/PlayerController.cs`,
  `TeamRuntime` at line 1186 onward. `PlayerController1.cs` is not the compiled authority.
- Legacy `CharacterActionMessageHandler`, `ChatCmdMessageHandler`,
  `TeamInviteMessageHandler`, `TeamMemberMessageHandler`, `TeamMemberInfoMessageHandler`,
  `RaidMessageHandler`, `FullCharacterMessageHandler`, `StatMessageHandler`,
  `LftInviteClientPresence`, `TeamXpShareWindow`, and `ChatCommandText`.
- Current AOtomation `CharacterActionType`, `CharacterActionMessage`, team message
  models, `ChatCmdMessage`, `N3Message`, and `N3RecoveredContractTests`.
- New `Player`, `Character`, `StatCollection`, `CharacterActionMessageHandler`,
  `TextMessageHandler`, `ZoneSession`, `IZoneSession`, `ZoneMessageCodec`,
  `Playfield`, `PlayfieldManager`, `PlayfieldInboundQueue`, and session ownership tests.
- Existing mission evidence explicitly says team identities are process-local;
  durable team-owned generated missions/rewards are not an accepted persistence contract.

Legacy sender comments identify the accepted packet reconstruction sources:
`20260727-065826`, `20260727-071217`, `20260728-234012`,
`20260729-173311/173411`, `20260815-194517`, `20260815-222131`, and
`20260902-065805/073932/080839`. This audit used those retained source contracts,
not a newly analyzed capture corpus. New runtime does not load capture files.

## Discrepancy and acceptance matrix

| Behavior | Baseline New vs Legacy | Baseline classification | Implemented reconciliation / retained boundary |
| --- | --- | --- | --- |
| Create/invite/accept/decline | No New team owner/actions | MISSING_IMPLEMENTATION | One `TeamService`; create only after a pending invitation is accepted |
| Reply parameters | Legacy accepts only `p2=1`; `0/20` decline; `17` outbound acknowledgment | MISSING_IMPLEMENTATION | Exact action branches; server leadership marker is not acceptance |
| Reply without invitation | Legacy falls back to requester identity | LEGACY_BUG_NOT_TO_PORT | Fail closed; exact pending actor/session/requester required |
| Concurrent invitations | Legacy stores one invitation per invitee | MISSING_IMPLEMENTATION | No inviter overwrite; independent invitees remain valid when first acceptance creates team |
| Already-member acceptance | Legacy can silently leave another team | LEGACY_BUG_NOT_TO_PORT | No silent reassignment; existing membership remains authoritative |
| Inviting member role | Legacy permits current members to invite | MISSING_IMPLEMENTATION | Preserve member invites; kick/leadership/raid remain leader-only |
| Replayed acceptance | Legacy can republish whole roster | LEGACY_BUG_NOT_TO_PORT | Consumed invitation cannot repeat join or roster publication |
| Invitation expiration | No supported invitation TTL; decline cooldown is 30 seconds | UNPROVEN_BEHAVIOR | No new expiry duration; retain 30-second decline cooldown and lifecycle cancellation |
| Leave/kick | Legacy removal packets, not full roster resend | MISSING_IMPLEMENTATION | Remove once; two-person team dissolves remaining solo member |
| Leadership | Legacy first member leads; oldest remaining member replaces leaving leader | MISSING_IMPLEMENTATION | Ordered member list retained; leader-only explicit transfer |
| Explicit transfer | Accepted target=new leader, social=15/13 | MISSING_IMPLEMENTATION | Exact leadership target; preserve transfer state through zoning refresh |
| Team identity | Legacy process-local TeamWindow ID | MISSING_IMPLEMENTATION | Same identity family/allocation range; no database/team persistence |
| Disconnect/reconnect | Legacy disconnect leaves; zone transfer explicitly exempt | MISSING_IMPLEMENTATION | Current transport removes membership; reconnect does not restore persisted stat IDs |
| Zoning | Same character ownership survives transfer | INTENTIONAL_NEWENGINE_DIFFERENCE | Retain same Player/team ID; old transport callback is a no-op |
| Duplicate actor/removal | Legacy lookups can recover unrelated stale objects | LEGACY_BUG_NOT_TO_PORT | Exact accepted Player reference fences actions, callbacks, snapshots and projections |
| Cross-playfield updates | No New team projection route; mutable Stats is owner-tick local | MISSING_IMPLEMENTATION | Recipient owner queue, immutable cached metadata, owner/session/projection-epoch fencing |
| Shutdown/stale members | Process-local cleanup required | MISSING_IMPLEMENTATION | Per-player detach resets transient stats before snapshot; shutdown clears team/invite/raid/chat state |
| Team roster/vitals | Legacy explicit typed senders | MISSING_IMPLEMENTATION | Current packet names/fields; self first, leadership then other members, channel last |
| Unknown vitals | Legacy can synthesize nano=469 | LEGACY_BUG_NOT_TO_PORT | Omit unresolved vital message; no invented health/nano values |
| Raid conversion | Current Legacy RaidCmd supports leader conversion | MISSING_IMPLEMENTATION | One raid flag on same team authority, one Raid message per member, existing chat bridge commands |
| `/team` and `/invite` | Legacy uses N3 ChatCmd, New handled only TextMessage dot commands | MISSING_IMPLEMENTATION | Dedicated typed ChatCmd handler and exact shared command normalization |
| FullCharacter conditional arrays | Both engines send empty team-related arrays | UNPROVEN_BEHAVIOR | Do not invent conditional payloads; roster refresh follows normal self-spawn |
| Cross-PF off-map SCFU name seeding | Legacy constructs an off-map visible-name entity | INTENTIONAL_NEWENGINE_DIFFERENCE | Do not create fake world entities; send current info/level and named TeamInvite plus CharacterAction popup; roster carries names |

The final presentation row is explicitly **not** a claim of live client rendering
verification. The accepted typed name/popup/roster payloads are tested; the legacy
off-map workaround is not reintroduced. No generalized LFT directory, arbitrary
raid reorganization, team persistence, or team mission reward distribution is
inferred by this slice.

## Ownership and integration hooks

`TeamService` is one application singleton, never one service per playfield.
Membership is keyed by character ID with exact accepted `Player` ownership.
`TeamSnapshot` is an immutable projection, not another mutable membership store.

- `AttachPlayer` runs on accepted spawn, before initial FullCharacter serialization;
  clears persisted transient team stats. Reattaching the same actor is idempotent.
- `RefreshPlayer` runs after initial/reconnect self-spawn reaches InPlay; zoning
  keeps the same team identity and sends only the arriving viewer's roster.
- `OnTransportDisconnected` checks the exact current Player/session before
  removing membership. It is safe from the transport callback: no direct Stats
  access; all projections use the recipient owner queue.
- `DetachPlayer` runs on owner tick before offline snapshot/despawn and resets
  transient team stats synchronously. Old-owner detach cannot remove new ownership.
- `Shutdown` runs after per-player disposal and clears remaining process state.
- `dispatchOnOwner` executes inline only on that player's owning tick, otherwise
  queues/reroutes to its current playfield. Queued work checks exact Player,
  exact session, and projection epoch. A newer inline leave cannot be undone by an
  older cross-playfield join still waiting in the queue.
- Level/profession/vital metadata is captured on accepted attach/refresh and local
  stat-change callbacks. Team operations never enumerate another actor's mutable
  stat dictionary. Observers are removed on detach/replacement/shutdown.
- Team XP bounds and command normalization link the existing platform-neutral
  source files. New does not reference the Legacy ZoneEngine assembly or process.

The owner-queue seam is a required gameplay addition: existing reconciliation
protected actor/session ownership, but did not make cross-playfield mutation of
`StatCollection` safe. It does not change database ownership or transport semantics.

## Packet contracts

All packets use current AOtomation types, not renamed historical aliases.

| Packet | Required fields/order |
| --- | --- |
| TeamRequestInvite action | Identity=invitee, Target=inviter, p1=0, p2=0 |
| TooHigh / TooLow | Actions `0xA9` / `0xA8`, Identity=inviter, Target=invitee; confirmation p2=1 delivers invite |
| TeamInvite | Identity=invitee, Unknown=1, full inviter identity, Int16-length inviter name |
| TeamRequestReply acknowledgment | Target=None, p1=0, p2=17; never accepted inbound as a join |
| Decline | Identity=inviter, Target=decliner, p2=20 |
| TeamMember | Identity=viewer, full member identity, TeamWindow identity, Unknown4=-1, actual level/profession/name |
| TeamMemberInfo | Identity=viewer, Member=other member, paired known max health/nano as current Legacy sends |
| AcceptTeamRequest | Full target leader identity, p1=TeamWindow type, p2=team ID |
| TeamMemberLeft | Identity=viewer, Target=leaving member, p1=team ID, p2=-1; no roster rebroadcast |
| Raid | Identity=each member, Unknown=0, Unknown1=0 |

An exact fixture caught and fixed an implementation error during this slice:
`N3Message` defaults `Unknown` to 1, but Legacy team action/roster/info/raid senders
explicitly use 0. Runtime was repaired; fixture expectations were not changed.
TeamInvite remains 1; Stat/ChatText retain their existing default of 1.

The explicit join test preserves Legacy ordering: social/team-side, leader ack,
self member, leader marker, other member/vitals, team channel. Leave tests require
removal-only propagation. Header sender/receiver and byte-for-byte codec roundtrip
are covered separately. New body fixtures are labeled contract-derived test actor
fixtures, not fabricated live captures.

## Validation

`Tools\run_zoneengine_new_tests.cmd`: **PASS**, 307 total tests at the latest combined
checkpoint, including 27 team tests; zero failures/skips. Runtime startup preflight PASS.
Log: ignored `.codex-inventory-tests.log`. Counts are a concurrent
branch snapshot, not a fixed acceptance threshold. Full Windows/Linux exact-SHA
acceptance remains the parent task's final gate.

Team coverage: create/invite/accept/decline, reply flags, stale/replayed requests,
identity spoofing, concurrent invitees, existing member invites, conflicting teams,
leave/kick/leader/disband, reconnect, same-actor zoning, stale session/actor callback,
deferred projection ordering, level confirmation, raid/chat bridge, shutdown,
unknown/updated vitals, ChatCmd normalization, codec/header roundtrip, and exact
body field widths/endianness. No existing tests were changed to bless a regression.

Files added by this slice:

- `AORebirth/Server/ZoneEngine_New/Core/Teams/TeamService.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Teams/TeamChatMessageHandler.cs`
- `AORebirth/Server/ZoneEngine_New.Tests/TeamServiceTests.cs`
- This report.

Root-owned integration edits register/wire these contracts in existing Program,
playfield/session/spawn/queue/action/raid paths and project source links.

`RUNTIME_DEPENDS_ON_HISTORICAL_CAPTURES=NO`

`TEAM_PERSISTENCE_INVENTED=NO`

`PRODUCTION_OR_LIVE_CLIENT_CHANGED=NO`
