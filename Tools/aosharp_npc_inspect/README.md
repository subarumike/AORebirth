# NPC Inspect Probe 1.2.1 (Mike's AOSharp runtime)

Version 1.1.0 fixes outbound recognition at AOSharp's pre-framing hook. The
original version incorrectly required the outgoing transport length field to
already be populated. Version 1.2.0 adds bounded full game-packet diagnostics
and rejection-feedback logging. Version 1.2.1 ports the entry point to
`AOPluginEntry`, which supplies the installed runtime's `Init(string)` contract.
The startup banner now says `v1.2.1 Mike2022 ready`.
Replace only this plugin DLL with the client closed, then restart and load it.

Standalone .NET Framework 4.8, x86 AOSharp plugin. No ZamTools dependency,
binary patches, automatic targeting, or automatic retries. Loading it does
not send an Inspect request. Use only on a server where you may run this test.

## Run

1. Extract the package into its own writable folder and add
   `NpcInspectProbe.dll` through your existing AOSharp plugin loader.
   Do not replace AOSharp.Core.dll or AOSharp.Common.dll.
2. Select an ordinary NPC, then type `/npcinspect` in AO chat.
3. Wait 15 seconds. Chat reports native call, observed outbound request,
   matching reply (if any), and a final summary.
4. For a known-positive comparison, select another player and type
   `/npcinspect control` after the cooldown. Do not manually Inspect during
   either probe: the protocol has no probe-specific correlation token.
5. Send the generated `NpcInspectLogs/<session>/` folder back for analysis.

Other commands: `/npcinspect status`, `/npcinspect cancel`, `/npcinspect help`.
Cancel stops observation; it cannot recall an already-sent request. Zoning
aborts the probe. Only one probe runs at once, with at least 15 seconds
between attempts. Pets and self-targets are excluded. Selection must remain
unchanged until the queued call executes on the game update thread.

Logs are beside the plugin, not uploaded anywhere. Each session contains:

- `session.log`: target, actor, mode, UTC event times, callback counts, native
  invocation, Inspect/rejection observations, and completeness status.
- `NNN-packets.bin`: exact incoming/outgoing game-network callback bytes,
  including unknown message types, within the 15-second probe window.
- `NNN-packets.log`: readable packet index with UTC/elapsed observation time,
  direction, archive offset, size, family/key, sender/receiver and inherited
  identity. Malformed/unknown packets are retained, not silently filtered.
- `NNN-inspect-request.bin` / `NNN-inspect-reply.bin`: first matching pair.
- `NNN-feedback-NNN.bin`: first 64 feedback messages as convenient separate
  files. Their fields are logged even when their meaning is unknown.

Raw archives stop at 16 MiB or 20,000 packets per probe; truncation is explicit
in chat and logs, with omitted-callback counts. A callback over 1 MiB or null
also marks capture incomplete. No partial packet is saved. Files are flushed
every game-update second, on important replies, and on stop/unload/error.
An abrupt process crash can still lose the final buffer; a missing PROBE_END
means the session was not finalized. No capture occurs before the native call
or outside the window; zoning/cancel/unload stops it. No background collector,
separate chat-socket hook, socket capture, or packet injection is added.

The broader logs include unrelated nearby game activity during the test and
may contain player identities, item data, or text. Keep them private. Avoid
other actions/manual Inspect during testing. Late responses are not captured.

Restart the client when replacing/reloading the plugin: this AOSharp version
has no public command-unregister API. Unloading unsubscribes the network and
update hooks and makes the existing command inert.

## What the result establishes

- `NATIVE_CALL_RETURNED`: the exported routine returned; not proof of sending.
- `OUTBOUND_INSPECT_OBSERVED`: raw hook saw action 261 for the selected identity
  entering Connection.Send, before framing and the actual send. This does not
  prove socket delivery or server acceptance. Saved outgoing bytes are exactly
  the hook input and may have a zero transport length; do not treat them as a
  completed wire frame or replay them. The summary also records total outgoing
  callback and CharacterAction counts.
- `MATCHING_INSPECT_REPLY`: the raw hook saw InspectIIR with that target identity.
  The remaining equipment payload is preserved, **not decoded or assumed valid**.
- `NO_MATCHING_REPLY_OBSERVED`: no matching reply within 15 seconds. This is not
  proof of rejection, unsupported NPCs, or empty equipment. Use the player control
  to help distinguish an NPC-specific outcome from instrumentation problems.
- `FEEDBACK`: raw N3 Feedback (`0x50544D19`) fields: unknown1, category, message
  key, inherited identity, and trailing-byte count. No field is renamed as an
  error reason without evidence. Category 110/key `0x030C85E4` maps in the local
  text.mdb to "Your inspect request was rejected." This is a generic rejection,
  not proof of an NPC-type restriction or a privacy-setting restriction.
- `REJECTION_FEEDBACK_OBSERVED_IN_WINDOW`: that feedback was observed while the
  probe was active and no matching equipment reply was observed. Correlation
  is temporal only: the managed Feedback contract has no verified target field
  or probe token. Both reply and rejection counts are retained if both occur.

The retail client handles its ordinary Inspect response; the plugin does not
force an Inspect window open or fabricate gear. NPC server support and whether
the client displays an NPC reply remain unverified until the live test.
Unknown rejection encodings remain in the raw archive for offline analysis;
absence of the recognized feedback key is not proof of absence of rejection.

## Raw archive format

Eight ASCII magic bytes `NPIPCAP2`, then repeated records: direction u8 (0 in,
1 out), UTC .NET ticks i64, elapsed milliseconds f64, payload length i32, exact
payload bytes. Archive numbers are little endian; AO payloads are unchanged.
The `.log` offset points to the beginning of each record. Timestamps are when
AOSharp drains its callbacks, not native send/receive times. AOSharp drains
incoming before outgoing queues, so their cross-direction order is not a
guarantee of wire ordering. Truncation/disconnection must not be treated as a
negative server result. Synthetic tests are not original-client evidence.

## Evidence and compatibility

Uses the named Gamecode.dll export
`?N3Msg_Inspect@n3EngineClientAnarchy_t@@QAEXABVIdentity_t@@@Z`, x86 thiscall,
with the existing `N3Engine_t.GetInstance()` and an 8-byte target identity.
No guessed request serializer is used.

Analyzed Gamecode.dll SHA256:
`654969A6B65946CB161F0E60AED8589260FC5ECA1795488F66BB56F8FFF73726`.
The plugin checks the loaded module's on-disk hash and refuses a mismatch.
Do not bypass that guard; analyze a different build before supporting it.
This is an ABI compatibility guard, not a check of in-memory integrity.

Local decompiler-derived evidence (not original source):

- Gamecode.dll+0x0001DDD4: Inspect constructs CharacterAction action `0x105`
  with the requested target, then sends through the existing client routine.
- Gamecode.dll+0x00072A31 / +0x000729BC: CharacterAction read/write layout;
  documented in `Anarchy Online/rebuild/docs/n3_character_action_iir.md`.
- Gamecode.dll+0x000750C4 / +0x00075125: Inspect read/write begins with target
  identity, followed by the shared inventory serializer. Activation is
  +0x0007514A. InspectIIR key is `0x5A585F65`.
- Installed AOSharp.Common HeaderSerializer, N3Message and StreamReader:
  16-byte transport header; big endian; N3 key at byte 16, inherited identity
  at 20, pass-on at 28, subclass payload at 29.
- Installed AOSharp.Core Network: raw PacketReceived/PacketSent hooks run
  during Network.Update. Raw input is queued before typed deserialization,
  allowing observation of reply types missing from the managed message table.
- Installed AOSharp.Bootstrap Main.Send_Hook calls PluginProxy.SentPacket before
  Connection_t.Send. Common.Connection_t.DSend marshals exactly the `len` bytes.
  MessageProtocol's serializer initializes offsets 4..7 to zero; Connection.dll
  raw Send at RVA 0x000016F3 fills count/size afterward. See
  `Anarchy Online/decompile_report/evidence/MessageProtocol.dll/message_layout.md`
  and `Anarchy Online/decompile_report/evidence/Connection.dll/framing.md`.
  Outgoing-only parsing therefore accepts the zero reserved header or an exact
  populated size. Incoming reply validation still requires an exact size.

The original source was copied from AO stripdown with Mike's explicit approval;
the original files were left untouched. All porting and builds happen in AORebirth.
The historical reverse-engineering references above describe the original
implementation; this port does not change packet parsing or the native hash guard.

Built against Mike's `D:\AOTools\ReadyToUse` AOSharp.Core/Common pair. The package
includes their build-time SHA256 hashes in `build-runtime.json`, not copies
of those assemblies. Build success is API compatibility evidence, not proof
of injection, native invocation, NPC server support, or equipment semantics.

## Rebuild and checks (no client execution)

From the repository root:

```powershell
powershell -NoProfile -File Tools/aosharp_npc_inspect/build.ps1 -AOSharpPath 'D:\AOTools\ReadyToUse'
powershell -NoProfile -File Tools/aosharp_npc_inspect/test_packet_view.ps1
powershell -NoProfile -File Tools/aosharp_npc_inspect/test_capture.ps1
& "$env:WINDIR/SysWOW64/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -File Tools/aosharp_npc_inspect/verify_runtime.ps1 -AOSharpPath 'D:\AOTools\ReadyToUse' -PluginPath '.local/npc-inspect-probe/package/NpcInspectProbe.dll'
```

Build output is private under `.local/npc-inspect-probe/`. Requires a .NET SDK
and .NET Framework 4.8 reference assemblies. The framing tests exercise only
the independent managed PacketView source through PowerShell; no AOSharp or
client code is run. Synthetic packets verify parser behavior, not server behavior.
The separate runtime check loads assembly types only; it does not instantiate
the plugin, call Run, or invoke native game code.
