# NewEngine operational acceptance

The authenticated positive lifecycle is proven on disposable loopback MySQL.
Production cutover remains **NO**: a direct zone connection received the character
without LoginEngine authentication. This is an observed character-access integrity
failure, not missing gameplay coverage.

| Gate | Result | Evidence |
| --- | --- | --- |
| Authentication, list, selection, world entry | PASS | Real challenge/credentials, ZoneInfo, FullCharacter and both quest journals |
| Character save, exact inventory, credits, position | PASS | Connected move 64 to 66, real acknowledgement, SQL and subsequent wire assertions |
| Authenticated reconnect | PASS | Fresh clients repeat credentials and selection; no repair writes |
| Clean restart and exact state | PASS | Exit zero, distinct ZoneEngine PID, identical binary, fresh authenticated entry |
| Active nano and morph restoration | PASS | Exact durable identity/strain/duration/expiry, decreasing wire timer, captured morph payload |
| Morph cancellation and baseline restoration | PASS | RemoveFriendlyNano, Buff removal, MonsterData=0, original meshes; another authenticated reconnect |
| Authored and generated mission reload | PASS | Pre-start seeds, both real journals, exact generated binding/object snapshot and physical key |
| Generic unsupported item effects | PASS | Five retained Set/Hit/SetFlag/ClearFlag/UploadNano regression tests |
| Wrong password and invalid inventory source | PASS | Connected rejection; valid move acknowledgement establishes ordering; exact state remains |
| Transactions, schema refusal, durable repository reload | PASS | Separate full disposable suite, injected failures and process cycles |
| Unauthenticated zone admission | **FAIL** | Direct ZoneLogin receives FullCharacter without any LoginEngine connection |
| Executable-only Legacy rollback after NewEngine writes | **UNSAFE** | Executed stale Legacy item-table assertions |

Inventory move and morph cancellation are CONNECTED_MUTATION_PROVEN.
Nano activation and authored/generated mission state are SEEDED_STATE_RELOAD;
connected casting, mission rolling/acceptance/completion/rewards are not claimed.
Credits remain exactly 1234; this proves preservation, not a credit-changing action.
No equipment action is claimed. Separate repository transactions are still
REPOSITORY_ONLY_PROOF even when they execute real MySQL.

The generic SCFU tail and fixed-effect SpellList reader cannot fully describe
the captured variable-criterion morph payload. The fixture compares the actual
received payload with the existing capture-backed MorphVisualPackets adapter,
checks SetNanoDuration and the durable nano record, and observes baseline
restoration on cancellation. It does not invent a decoder or claim live-client
visual acceptance.

See [connected report](NEWENGINE_CONNECTED_ACCEPTANCE.md) and
[exact-source receipt](NEWENGINE_CUTOVER_VALIDATION_RECEIPT.md).
The historical Docker failure at d1c6d01 is superseded by actual disposable runs.
No global Docker configuration or production state was changed.

NEWENGINE_OPERATIONAL_CUTOVER_READY=NO
CHARACTER_AND_INVENTORY_INTEGRITY_PROVEN=YES (tested authenticated lifecycle)
AUTHENTICATED_CONNECTED_ACCEPTANCE_PROVEN=YES (positive sequence; admission isolation FAIL)
