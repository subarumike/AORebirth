# Connected NewEngine acceptance

## Scope

Mike-owned branch codex/newengine-production-cutover-001. Security closure starts
at b87faf8b6de31d22f79d8f469990c27592bab6f9; prior positive milestone started at
de764881cfae677bd0b2975499e8ad6cb5944c4a.
Worktree: C:\Users\Mike\Documents\AORebirth\tools-temp\cutover001.
Master remains 6e90dda030774726aa2060acb9edb756ea1f635c and the developer ref
53c858d9900266fb6740975dbb2b5011a5792e66. No master merge, developer edits,
production access, schema changes, Legacy deletion or DAO consolidation.

The validation receipt identifies the exact committed source, binary hashes,
fixture ID, UTC time and PIDs. Dirty-tree development runs are explicitly labelled
and are not exact-source acceptance.

Final positive, negative, concurrent and restart acceptance passes on clean
source `65f7e3c9e2d13b37a58f27bd1dfd72b1917ea80d`. The receipt records the
accepted binaries and all remaining original-retail evidence limits.

## Fixture and protocol

Tools/run_newengine_connected_acceptance.cmd takes explicit LoginEngine and
ZoneEngine_New binaries. DisposableSchemaDatabase owns fresh labelled Docker
MySQL/network, loopback endpoints and random credentials. Pinned image:
mysql@sha256:c592c15aaf4a1961e15d82eb31ea5987dda862d1c4b1e93424438c0e91dc1f8d.
Only that database is migrated with the existing explicit migration tool.

Before startup, setup creates account/character 9901, base stats, two items,
an uploaded active supported morph, the existing DeliverArmor quest and a
generated mission. Generated setup reuses the real deterministic generator,
captured ACG bundle, materializer, difficulty policy, reserved identities and DAO.
It adds one physical key. No invented runtime content or fabricated frozen wire.
Test-only internal visibility permits reuse of pure setup builders.

After startup, all mutations travel over TCP; database/DAO calls only assert
state. No repair writes occur between reconnects. The synthetic client uses
AOtomation and the existing DH/TEA login-key encoder, LoginEngine external
four-byte padding, ZoneSession aligned incoming frames and zlib outgoing stream.
It never starts or controls the AO client.

1. Reject an incorrect password through LoginEngine.
2. UserLogin -> ServerSalt -> UserCredentials -> CharacterList -> SelectCharacter
   -> ZoneInfo -> ZoneLogin -> FullCharacter -> CharInPlay -> both quest journals.
3. Assert character, exact inventory, credits, position, nano/morph and missions.
4. Send invalid inventory source 9999, then supported slot 64-to-66 move. Await its
   real acknowledgement and assert exact durable results.
5. Logout, discard clients, authenticate/select/connect again and compare.
6. Reject no/random/unknown/altered/expired/stale/cross-character/cross-account
   tickets and invalid header identities. Hash every table before/after each
   rejection. Race eight TCP clients with one real LoginEngine ticket: exactly
   one FullCharacter, seven disconnects. Verify the winner's exact world state;
   reject replay while in play and after logout without database changes.
7. Stop ZoneEngine cleanly, start the same binary with a different PID. Reject
   consumed and expired tickets again. Admit an unconsumed LoginEngine ticket
   issued before restart; then repeat fresh authentication/selection/entry and
   compare exact state for the main character.
8. Cancel morph over RemoveFriendlyNano, observe Buff removal and base
   MonsterData, logout and authenticate again to verify cancellation persisted.
9. Stop engines and remove the owned container/network. Admission failure
   produces nonzero overall exit even when the positive lifecycle passes.

## Expected state

| Field | Before move | After move/reconnect/restart |
| --- | --- | --- |
| Character/account | 9901 / cutoverconnected | unchanged |
| Credits/PF/XYZ/quaternion WXYZ | 1234 / 4582 / 100,0,100 / 1,0,0,0 | unchanged |
| Instance1 | 43384/43384,QL1,count3,owner9901,container104,slot64,ItemType0,Source1 | identical,slot66 |
| Instance2 | 42423/42423,QL4,count1,owner9901,container104,slot65,ItemType0,Source1 | identical |
| Key instance3 | 28577/28577,QL1,count1,owner9901,container104,slot67,ItemType0xC76D,Source0 | identical |
| Wire metadata | two Terminal identities, one key, Flags161,Unknown0 | identical |
| Active morph | nano270542,instance99,catalog strain/duration,exact expiry | same durable row/expiry,decreasing wire timer |
| Morph base | MonsterData0,CATMesh111,DisplayCATMesh222 | unchanged through overlay/cancellation |
| Authored quest | active DeliverArmor,step active,journal0x555BE9F6 | same state/journal |
| Generated quest | reserved quest/key/PF,real frozen offer,captured bundle | exact binding/objects,same journal/key |

Nano activation and mission state are administrative seeds. Inventory movement
and morph cancellation are connected mutations. No mission gameplay, credit
reward, equip legality or live-client visual claim is made.

## Repairs and security closure

The independent durable reload fixture reused character 9701 owned by
AuthoredMissionSmoke. It now owns 9801, removing a test-data collision.

Captured bundle hashes are uppercase; the DAO requires canonical lowercase.
NewEngine canonicalizes the acceptance-plan hash without changing its digest.
All captured bundle hashes have regression coverage, and the connected fixture
restores a real accepted bundle. No schema or transaction boundary changed.

The historical direct-zone admission failure is closed by ZoneHandoffStore and
ZoneAdmissionGate. LoginEngine issues both recovered cookies only after verified
credentials and owned character selection. NewEngine validates the 32-byte
envelope and consumes the account/character/login-generation-bound authorization
before lookup, hydration or reconnect ownership. See the dedicated security
report for protocol provenance, expiry, restart and deployment boundaries.

Extra fixture identities 9902 (same account), 9903 (another account), and 9904
(expired ticket) are created before either engine starts. Only the expired
negative ticket is administratively preseeded with an injected past clock; all
positive and concurrency tickets come through the real LoginEngine protocol.
The harness awaits normal LoginEngine disconnect cleanup before measuring each
negative zone attempt, avoiding a race with its legitimate Online flag cleanup.
No repair or authorization writes occur after engine startup outside real TCP
handlers. Original-retail end-to-end capture remains a separate evidence gap.

## Inspected files

LoginEngine UserLoginHandler/UserCredentialsHandler/SelectCharacterHandler,
CoreClient/Client, CheckLogin and encryption; AOtomation system/N3 contracts and
serializer; ZoneSession, ZoneLoginHandler, SpawnService, InventoryMoveService,
CharacterActionMessageHandler, NanoService, MorphNanoSpecialization/VisualPackets,
AuthoredQuestService/Journal, GeneratedMissionAcgService/RollProjection,
mission DAO/interfaces/schema, existing schema/inventory/nano/mission/durable
fixtures, governance, cutover reports and documented acceptance workflows.
The source inventory records reproducible broader inputs without claiming
semantic review of every linked gameplay method.
