# Official-client acceptance of 9817b708

**PASS.** Mike completed the supplied client checklist and confirmed the final post-restart result: "looks good to me". Source `9817b708aef60027649b54cb06ea9ce2fd2c6a64` remained running throughout the test; no binary rebuild occurred.

## Observed client and environment

The actual client was **C:\Funcom\Anarchy Online, 18.8.62_EP2**, identified from the running executable and LoginEngine handshake. Its launcher, game executable and existing test shim hashes are recorded in [the receipt](NEWENGINE_9817B708_RELEASE_RECEIPT.json). The prepared E: EP1 shortcut was not used; EP1 is not claimed as this run's acceptance.

Test container: aorebirth-linux-staging-9817b708 on DESKTOP-NFHOK0E, Ubuntu 24.04.4 LTS, kernel 6.18.33.1-microsoft-standard-WSL2. Login/zone: 192.168.1.207:7500/7501; chat: 127.0.0.1:7012. Local database aorebirth_chatengine_stage6 remains in the same loopback-only Docker database container. Both engines share /var/lib/ao-rebirth/session-ownership, root:root, mode 0700. Actual systemd deployment uses aorebirth:aorebirth and is separately covered by the release fixtures.

## Client and wire result

| Gate | Result / evidence |
| --- | --- |
| Authentication, character list and selection | PASS; fresh EP2 authentication and selection of character 9950 |
| ZoneInfo / secure admission | PASS; LoginEngine hands off and NewEngine admits the same character |
| SCFU / FullCharacter / CharInPlay | PASS; eight complete zone streams, including seven pre-restart and one post-restart client CharInPlay for 50000:9950 |
| ChatEngine and world entry | PASS; matching connections and Mike's gameplay confirmation |
| Two zone changes | PASS; PF655 -> PF1186 -> PF655, repeated after equipping |
| Inventory movement / equip / unequip / re-equip | PASS; five captured requests and five server acknowledgements move the shirt between inventory and chest; GUI carried-slot rearrangement is operator-accepted |
| Item identity preservation | PASS; instance 4 remains QL1 template 27383; all four inventory identities preserved |
| Logout / fresh relogin | PASS; fresh hydration shows four items and the shirt in chest slot 21 |
| Clean test-server restart | PASS; host NewEngine PID 98925 -> 33215, intervening process exit proven |
| Post-restart official login / state | PASS; fresh login and final clean logout preserve character, inventory/equipment, credits, nanos and missions |
| Session cleanup | PASS; online state returns to zero; completed old sessions close |

The originally suggested QL200 sleeves were unsuitable for this character. They were retained as inventory instances 2 and 3; the successful wear fixture is the QL1 Omni-Med Suit Shirt (27383), instance 4. The original crystal remains instance 1, stack 3. No item was removed to make the test pass. The user's requested inventory/equipment checklist is accepted; packet evidence specifically proves inventory slot 67 -> chest 21 -> automatic inventory return, repeated twice, ending equipped. No separate server-side carried-slot swap is inferred from those requests.

## Exact durable-state result

The stopped snapshot, restarted-before-login snapshot and all 18 table fingerprints match exactly across the service restart. After gameplay/relogin, the character row and item rows still match exactly. The sole stat delta is Health (27), 500000039 -> 500000045, matching the two +3 regeneration updates in the server log. Credits and every other stat are unchanged; this is explained runtime regeneration, not a persistence tolerance. Nano and mission tables were empty and remained empty; nonempty coverage comes from the already-passing disposable fixtures.

Final location: PF655, X=3208.820068359375, Y=35.10000228881836, Z=938.1519775390625. Online=0. Shirt: item instance 4, ArmorPage (102), character 9950, slot 21, template 27383/27383, QL1, stack 1.

## Evidence and limits

[Sanitized wire and state evidence](NEWENGINE_9817B708_RETAIL_EVIDENCE.json) retains capture hashes, typed packet identities, item movement fields, restart process identities and state comparisons. TCP streams are reconstructed separately; the decoder follows ZoneSession.cs:548's 4-byte client framing alignment and the established server zlib negotiation, with zero unparsed zone tails. N3 message identifiers and layout come from AOtomation contracts. No cookies, credentials or raw authentication payloads are committed.

Raw evidence remains under build-verify/release-9817b708: the two closed captures, logs-before-retail-restart/, staging-logs/, state-*.json and restart-receipt.json. The prior connected height issue was not reopened or relaxed. Missing vending-machine definitions and incomplete playfield collision/content warnings remain existing gameplay limits outside this release's operational acceptance.

Official acceptance now permits local master reconciliation and exact-result automation. Production deployment and direct master push remain outside this task's authorization.
