# Current Task

IMPLEMENTED AND TESTED (2026-11-04): repair shared ChatEngine TCP framing and incomplete sends
on `codex/chatengine-framing`, based on freshly fetched public master
`4457f015e60728fed8eeca304e47486770e66685`. Mike explicitly authorized focused
local regression fixtures. Preserve the separately committed initial bug repairs
on `codex/nov04-bug-repairs`, canonical GameData and unrelated work. No production
access, service changes, client control, deployment or master integration is
authorized. Live log access remains UNVERIFIED; the incident timestamp is UNKNOWN.
Source defects are proven independently of live attribution. Existing native
length fields define bounded frame assembly; do not invent flood or HTML policy.

Repair commit `50bf8097bd5970bc9aff85216bddc04569a6281c` passes all 40 focused
cases on a committed-source rerun (24 receive/codec, 16 send/ownership), with
zero failures or skips. ChatEngine and LoginEngine compile with zero errors.
Native framing, complete queued sends and pooled-buffer ownership are corrected;
no GameData or rate-policy change is included. Exact-input Windows startup and
Mike's acceptance, Linux packaging and deployment remain outside this run. See
[the repair report](../reports/CHATENGINE_STREAM_FRAMING_REPAIR.md).

## Retained prior task history

ACTIVE (2026-11-03): implement the static locked-door/switch repair on
`codex/crypt-door-triggers`, based on freshly fetched public master
`b091d32d9fdef66ca3e080674ee606643031bdf9`. Use the completed native trigger
research and finalized PF665 capture; distinguish native proof from editable
server compatibility policy. Mike authorized focused deterministic/multiplayer
tests, builds and deployment after validation. Exact-input Windows acceptance
is still required before Linux release work. Preserve user configuration and
other pre-existing dirty work. See
[the scoped repair report](../reports/STATIC_DOOR_TRIGGER_REPAIR.md).

Completed implementation (2026-11-03): integrate the captured Halloween Win and
Fail button responses after the preceding build. Reuse existing static placements,
template events, seasonal settings and nano execution. The generic buff HeadMesh repair
compiles; focused regression cases were authored without running a suite.
Private activation changes are restricted to the two reviewed existing Borealis
records and preserve all coordinates, raw metadata and other seasonal content.
Knockback and unobserved animation packet behavior remain unsupported. Public
source integration does not accept or deploy a new release tuple.

## Previous source integration

Completed source integration (2026-10-30): combine Mike's locally accepted Daily Login
Rewards feature at `c8fed6c026f3110043b8a0a18c19460485c62c9d` with freshly
fetched public master `132459c1d3f732a88abffca4750150446933f57b` on
`codex/daily-rewards-master-integration`, validate the combined source, and
integrate it into public master. All requested combat, world-object, developer
build and quest updates, including PR #46 and its follow-ups, are already
ancestors of that public tip. Preserve those changes and the Daily Rewards
account ownership, atomic grants, per-character progress and quiet callback
behavior. Update the Daily test adapters to the upstream boolean transfer API.
Private local configuration, character repairs, content and client artifacts
remain outside public commits. This source integration does not authorize
Linux work, publishing, deployment or release acceptance of the combined inputs.

Integration candidate `60b5ae335` includes both histories, with both transfer
test adapters aligned to the upstream interface. All four engines compiled
through the existing feature-validation helper into isolated output folders;
68 focused Daily Login checks passed. The running local engines retain the
previously tested build. The combined source still needs its normal content
reconciliation and Windows acceptance before any Linux release work.

## Accepted Daily Login feature

Active follow-up (2026-10-29): keep native Daily Login callbacks quiet when no
request is waiting, and avoid contradictory daily-limit chat after a successful
grant from the same queued batch. Preserve one claim per UTC day per character
and independent calendars for characters on the same account, on
`codex/repair-daily-login-rewards`, based on public master
`465e7ff882d71a2669dfbb56f08ec9bd3d950e1e`.

Retain authenticated account ownership, atomic inventory/receipt commits,
idempotent tokens and the verified immediate native Claim callback. Preserve
earned progress and all old account-era evidence; do not copy cumulative account
progress to sibling characters. Mike explicitly approved the new character/day
index migration; it was applied to the local database after a fresh backup.
Schema validation passed and all three existing claim rows retained identical
fingerprints. This versioned schema change is a material trigger for any
later authorized release. This local task does not authorize Linux work or deployment.

Recover behavior from `feec603be8a2b8d788279c094fa53514417952d1` without
restoring the retired engine. Use current item/inventory/DAO owners, preserve
earned progress and pending requests, commit reward rows and the character
receipt atomically, and require committed confirmation before displaying Taken.
Mike explicitly approved the scoped development ledger migration and focused
success, concurrency, persistence, rollback, UTC progression and web tests.
Development validation passes 68 focused C# checks, 22 browser-state checks and
17 PHP HTTP checks. Disposable real-MySQL checks pass for migration reruns,
account ownership, concurrent sibling characters, UTC-character-day uniqueness, four partial-write
rollback points, lost commit acknowledgements and restart replay. Empty schema
rollback succeeds; populated rollback refuses and preserves the ledger.

The native claim trigger now uses the authenticated account and actual inventory
grant plan. Committed receipts and character progress share the inventory transaction;
file projections recover from interrupted publication. Account-era JSON and
receipts remain unchanged; only verified character-owned evidence can seed progress,
and pending requests are not deleted. The browser never trusts old optimistic
cache entries or marks a new claim Taken before committed confirmation.

Calendar IDs, pools and quantities are preserved. Public historical relation data
restores 172 explicit family records using the current item-family resolver;
the original profession-nano catalog is recovered unchanged. Days 19 and 21 still
reject without advancing. The 84 fixed blueprint definitions in day 13 and the
65 QL-1 prototypes in day 14 retain the original fixed-template fallback; their
declared level scaling cannot override their actual template quality bounds.

Local browser integration now uses the observed per-request
`X-Anarchy-CharacterID` header as a routing hint, with conflicting hints rejected
before writes. The Daily window does not expose the guessed claim JavaScript
methods: its stock close handler invokes the native RequestClaims/action 263 path.
The verified client adapter now registers `AORebirthDaily.Claim` through the
existing Awesomium callback API and invokes that same native function on the
owning GUI thread. The page queues first, invokes the registered callback, and
polls confirmation while remaining open. Storage stays character-scoped and
Taken still requires committed confirmation. Endpoint, module hashes, current
view URL, thread, callback ownership and arguments fail closed.
Local client routing preserves original HTTP request bytes; production routing
remains unchanged. The existing native package checks pass.

This is FEATURE_VALIDATION only, not Windows release acceptance. OBSERVED local
client verification (2026-10-29): Mike confirmed the reward arrived and Taken
appeared while the Daily window stayed open. PROVEN server-side evidence records
the registered native callback, action-263 request and matching committed receipt
in both configured claim roots. That check predates the per-character revision,
whose local four-engine build and approved character-scope migration passed.
Mike confirmed per-character claiming works locally. He then reported redundant
empty-queue and success-followed-by-daily-limit messages. Existing requests and
receipts prove an older queued intent granted before the fresh intent correctly
hit the same-character daily limit in one batch. The follow-up changes feedback
only: empty callbacks and successful receipt replays stay quiet; a fresh grant
suppresses only later, explicitly typed daily-limit feedback in the same batch.
Every token still receives its normal durable result, standalone refusals and
other errors remain visible, and no schema or inventory semantics changed.
The 68 focused checks and updated four-engine build passed; Mike then confirmed
all his character logins and Daily Rewards work locally. Relog and
uninterrupted client UTC rollover remain unverified. Anonymous or ambiguous legacy requests still fail closed.
All four rebuilt local engines remain running for Mike's testing. The approved local
migration and client patch installation occurred; no production data, deployment
or Linux work was involved, and the agent did not launch or control the AO client.

## Prior task context

Active task: final public master integration of the three completed retail chest
commits. Starting public master is `c679d683aeff50c3f3c2d737974fe1880feddb7a`;
the direct fast-forward candidate is `d76a2fdeecc78afd921fec23263c61ddb2b7eb36`.
Approved private content is fixed at `2b5431fd382bd5fa00a7b232efb85dc556ad7cc2`.
Work is isolated on `codex/mission-chest-retail-master-merge` to preserve unrelated
Windows checkout changes. Review the source/content pair, run fresh bounded
governed chest/objective checks, record the required merge report and publish
public master only after those checks succeed. Do not replace working runtime
implementations, regenerate catalogs, restart servers, touch player data or
perform Windows/Linux deployment. Fresh candidate validation is separate from
historical feature evidence and from future Windows release acceptance.

## Prior basket correction

Active correction: restore the verified captured Gift wrapped basket at its
exact supported QL to ordinary RK chest eligibility. Remain on
`codex/mission-chest-retail-integration` after `5c6b3103a804bd05c535fd70a0fcb9293acc82cf`.
Quest-use metadata is independent of acquisition eligibility. Remove only the
incorrect basket exclusion; retain every other approval, negative restriction,
loot mechanic and quest-use definition. Use exact capture-bound review evidence
for the item metadata exception. Validate through the existing bounded feature
workflow, update private review/manifests, commit and push public-safe changes.
No master merge, deployment, live data/server change or AO client operation.

Completed: exact QL 1 approval restored; actual totals are 186/186 eligible
captured pairs, 25,118 definitions and 13,269 bands. Other approvals/exclusions
and quest-use data remain unchanged. Fourteen catalog tests, 99 focused tests,
50 MySQL checks and connected TCP validation completed (known shutdown baseline
retained). Full installed-pool selection and chest-command replay proved the
item selectable at QL 1. The pre-existing incomplete Smug/Eel exchange content is
documented separately, with no quest implementation added.

## Previous integration (basket exclusion superseded by this correction)

Active request (2026-10-28): integrate eligible recovered retail mission chest
items into the actual private development RK chest policy. Work is isolated on
`codex/mission-chest-retail-integration`, following recovery commit
`90b4de0ea5dfc490caf69008a42761b23cc4f4ff` and public master
`c679d683aeff50c3f3c2d737974fe1880feddb7a`.

- Reuse completed recovery outputs; do not repeat decoding or collect captures.
- Account for all 186 exact pairs and current supported QL spans. Retail chest
  contents are positive acquisition evidence; preserve expansion, seasonal,
  progression and special-purpose vetoes and retain unresolved eligibility.
- Use the existing approval generator and runtime policy loader. Preserve every
  existing valid approval and the intentional implant majority. Do not change
  rarity, mission QL windows, counts, placement, inventory or persistence.
- Install reviewed private policy bytes in development GameData. Keep catalogs,
  reports, credentials and capture records outside public Git.
- Run the requested bounded production-selector campaign and focused chest,
  inventory, disposable MySQL and connected TCP checks through feature validation.
  Verify the real ZoneEngine policy load and the existing chest-roll command.
- Commit and push public-safe tooling/test changes only. No master merge,
  deployment, live database/server changes or AO client control is authorized.

Offline workflow and output semantics:
`tools-temp/AOSharpMissionCaptureAnalyzer/CHEST_LOOT_RECOVERY.md`.

Completed development integration: 185 of 186 recovered pairs are eligible;
129 are new pair identities and eight receive supported QL extensions. One
quest component is excluded. The installed private policy has 25,117 approved
definitions and 13,268 bands, retaining every previous approval and all 23,052
implant definitions. Requested selector, focused, disposable MySQL and connected
TCP validation completed; TCP retains a proven pre-existing shutdown exception.
See `docs/reports/MISSION_CHEST_RETAIL_LOOT_INTEGRATION.md`. No release acceptance,
master merge or deployment is implied.

## Previous task context (not current release authorization)

Complete the existing ordinary RK mission chest restriction and its governed
Windows-first release. Starting freshly fetched public master:
`7213f2ab63aca7333b7d7be19a0d360c7af5c558`.

- Preserve the draft on `codex/mission-chest-rk-restriction`, initially based on
  `47dae275238c83972779d62d351c718554d7b7b3`; preserve newer master changes at integration.
- Retain the reviewed private 24,925-definition / 13,131-band policy and its
  independent seasonal/special vetoes. Do not rebalance implant eligibility,
  rarity, counts, quality limits or dedicated event loot.
- Use the supported validation-only feature mode for compilation and requested
  selector, persistence, inventory, MySQL and connected TCP checks. Feature
  observations never authorize production publishing or Windows acceptance.
- After successful feature validation, integrate and push public master, then
  build and validate the exact freshly synchronized Windows candidate with the
  verified private policies and complete canonical content manifest.
- Mike's explicit acceptance of the new exact Windows tuple is still required.
  Historical acceptance does not authorize changed source or data.
- Linux online build and normal production deployment are authorized only after
  that mandatory gate. Do not claim either before actual execution.
- Keep full execution evidence and release details private; maintain the required
  `docs/reports/MISSION_CHEST_RK_LIVE_DEPLOYMENT_FINAL.md` report without publishing
  protected catalogs, connections or private infrastructure.

Development observations: four engines compile; 10,000 production-selector
populations cover every mission QL with no unauthorized selections. Core results
are 293 passes and seven exact public-source baseline failures, not an all-green
suite. Disposable MySQL has 50 passing checks. The broad geometry run retains
unresolved long-run null references and a host abort; isolated controls do not
erase those outcomes. Unmodified public source separately reproduces the native
geometry crash and the exact Zone shutdown exception sequence. Connected TCP
results and any later Windows readiness remain measured private evidence.
