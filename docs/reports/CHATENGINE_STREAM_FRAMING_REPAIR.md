# ChatEngine framed stream repair

Date: 2026-11-04. Branch: `codex/chatengine-framing`.
Starting public source: `4457f015e60728fed8eeca304e47486770e66685`.
Repair commit: `50bf8097bd5970bc9aff85216bddc04569a6281c`.

## Defect and supported correction

PROVEN source defect: ChatClient treated each TCP receive chunk as a complete
native chat frame, despite the existing type/length header. A fragmented string
could be decoded and forwarded as a shorter tell; coalesced trailing frames
were discarded. Unknown continuation bytes could remain at the front of the
receive buffer and block later traffic. These mechanisms support the reported
long-HTML tell symptoms, but the actual live incident remains UNVERIFIED.

The repair uses the existing protocol and transport mechanisms:

- ChatClient collects the four-byte header and exactly its declared body,
  dispatches complete frames in order, and retains partial frames across reads.
  Allocation is bounded by the existing unsigned 16-bit body field. The shared
  8192-byte receive chunk size is unchanged.
- Completed frames have independent owned arrays, including replies sent
  asynchronously. Dispatch stops when a handler disposes its client.
- PacketReader rejects missing declared bytes. PacketWriter rejects string/body
  length overflow before it can emit a wrapped header. Existing empty-string
  encoding and invalid-UTF8 replacement behavior are preserved.
- The existing Cell.Core send path serializes pending writes and accounts
  for actual completion bytes, retaining unsent tails and failing closed on
  socket errors or zero-byte progress. Its existing pooled socket arguments and
  receive-buffer reference counts provide operation ownership through disposal.

No HTML interpretation, bot command implementation, arbitrary message ceiling,
flood policy or throttle is introduced. `!modules` remains opaque tell text.
Raw array Send retains its borrowed-buffer contract; SendCopy provides a
snapshot, and pooled segment sends snapshot only their validated range.

## Validation evidence

Mike explicitly authorized focused local tests. They use actual ChatClient,
Parser, Tell and packet writers with synthetic authenticated-state fields;
there are no database or World-service calls. Send fixtures use controlled
socket completions and ephemeral loopback sockets, without engine or AO-client
startup. Test reflection is confined to fixtures.

- Initial receive/codec baseline: **21 total, 7 passed, 14 failed**, with no
  fixture compilation failures.
- After receive correction, expanded cohort: **23 receive/codec cases passed**.
  The send/lease cohort initially recorded **15 total, 2 passed, 13 failed**.
  One new range fixture incorrectly expected raw Send to snapshot caller-mutated
  bytes. It was corrected to preserve the existing borrowed-range contract;
  the other twelve failures reproduced send/lease defects.
- Initial combined green: **38 passed, 0 failed, 0 skipped**.
- Final expanded validation and committed-source rerun: **40 passed, 0 failed,
  0 skipped**, comprising 24 receive/codec and16 send/ownership cases.
- ChatEngine compiled through the focused test project. LoginEngine's explicit
  compile also succeeded: **0 errors** (existing warnings retained).
- The impacted product dependency closure is ChatEngine and LoginEngine plus
  their shared Cell.Core dependencies. No full-world or unrelated engine build
  was required. Three existing runtime files changed; no new runtime class file,
  package dependency, source inventory or build wrapper was introduced.

Coverage includes every split point of a 7 KB HTML frame, byte-at-a-time headers
and UTF-8, coalesced frames and tails, receive segment offsets/boundaries,
unsigned length boundaries, zero-body LFT behavior, independent ping buffers,
handler disposal, incomplete sends, ordering, counters, failures and pooled
buffer lifetime. Additional bounded cases cover another producer enqueueing
during a completion-driven partial write, and actual BeginReceive/Chat parser
cleanup when a handler synchronously disposes its client. Every test is
deterministic or uses bounded loopback I/O.
No broad gameplay/world suite is part of this task.

The existing test project has an explicit ChatNetworkFocusedTests cohort,
referencing the actual ChatEngine project graph. Its two new test files are
excluded from other cohorts/default compilation to preserve the existing Zone
reference graph. Both files are mandatory in the focused cohort.

Executed command forms from the owning checkout:

```cmd
cmd /d /c tools\windows_release_contract.cmd run-feature-validation -- "C:\Program Files\dotnet\dotnet.exe" test AORebirth\Server\ZoneEngine_New.Tests\ZoneEngine_New.Tests.csproj -p:ChatNetworkFocusedTests=true
cmd /d /c tools\windows_release_contract.cmd run-feature-validation -- "C:\Program Files\dotnet\dotnet.exe" build WindowsBuildNet10\Projects\LoginEngine.WinNet10.csproj -m:1 -nr:false
```

Each focused run and compile had a declared ten-minute limit; fixture I/O has
shorter individual bounds. Generated TRX/feature receipts, exact commands and
measured source/input/output identities remain in ignored local evidence.

All builds and tests use the existing development-only feature-validation
operation. Receipts cannot authorize publishing, release startup, Linux work
or deployment; they remain `DEPLOYABLE=NO` and `WINDOWS_USER_ACCEPTED=NO`.

## Production and release boundary

`LIVE_CHAT_LOG_ACCESS=UNVERIFIED`

`MODULES_FAILURE_TIMESTAMP=UNKNOWN`

`PRODUCTION_CHANGES=NONE`

The supplied report names Helpbot and Bitnykk, but no approved standalone live
access route, reliable incident window or actual payload was available. No
production access was attempted. The older Windows log directory was not
treated as Linux production evidence. Current deployed binary/source identity
and original-client behavior remain UNVERIFIED.

The earlier private pipeline source comparison found the relevant transport
files identical to public source. Its build records do not prove which binaries
are currently installed. This repair originates in public product source;
there is no Linux-only gameplay or protocol change.

No GameData, database schema/rows, service settings or website reports change.
The completed initial website bug repairs remain on their separate branch.
After review and normal public-source integration, an exact-input Windows
build/startup and Mike's acceptance must precede online Linux packaging.
Production identity and the normal release provenance must be verified before
any separately authorized deployment.

The server frame bound follows its existing unsigned length field; this is not
a claim that every original client accepts payloads up to that bound. A partial
frame may retain up to 65,539 bytes per connection. No new queue-pressure or
flood policy was invented. Actual Helpbot payloads, its connection route and
original-client/live behavior still require the missing evidence or manual
verification after the normal acceptance gates.
