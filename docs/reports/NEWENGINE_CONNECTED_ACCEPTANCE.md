# Connected NewEngine acceptance

## Scope

Mike-owned branch codex/newengine-production-cutover-001, starting at
de764881cfae677bd0b2975499e8ad6cb5944c4a.
Worktree: C:\Users\Mike\Documents\AORebirth\tools-temp\cutover001.
Master remains 6e90dda030774726aa2060acb9edb756ea1f635c and the developer ref
53c858d9900266fb6740975dbb2b5011a5792e66. No master merge, developer edits,
production access, schema changes, Legacy deletion or DAO consolidation.

The validation receipt identifies the exact committed source, binary hashes,
fixture ID, UTC time and PIDs. Dirty-tree development runs are explicitly labelled
and are not exact-source acceptance.

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
6. Separately test direct zone admission. Its unexpected FullCharacter is FAIL,
   never positive authentication evidence.
7. Stop ZoneEngine cleanly, start the same binary with a different PID, repeat
   authentication/selection/entry and compare exact state.
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

## Repairs and concrete remaining blocker

The independent durable reload fixture reused character 9701 owned by
AuthoredMissionSmoke. It now owns 9801, removing a test-data collision.

Captured bundle hashes are uppercase; the DAO requires canonical lowercase.
NewEngine canonicalizes the acceptance-plan hash without changing its digest.
All captured bundle hashes have regression coverage, and the connected fixture
restores a real accepted bundle. No schema or transaction boundary changed.

ZoneLoginHandler.HandleAsyncCoreInner loads/reconnects by CharacterId without
authenticated handoff validation. SelectCharacterHandler validates the login
account but supplies no consumed proof. The mapped ZoneLoginMessage contains
only CharacterId. Direct zone entry after normal logout reproduces unauthorized
character access.

Safe closure needs a verifiable, expiring, single-use login-to-zone binding to
the authenticated account, selected character and session, grounded in the
established client wire contract. Verify actual handoff fields/transport first;
do not invent cookie fields, trust CharacterId/Online alone, or treat a prior
successful login as authorization of a different socket. Repeat missing,
forged, replayed and cross-character admission tests plus the positive lifecycle.
No unsafe handoff shim was introduced to make the gate green.

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
