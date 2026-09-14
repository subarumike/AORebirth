# Official-client acceptance of 9817b708

**PENDING — the exact candidate is running on the local Linux test server; Mike's official-client run has not yet been observed.** This is not a failure report and does not inherit old client acceptance.

Source: `9817b708aef60027649b54cb06ea9ce2fd2c6a64`. Container: `aorebirth-linux-staging-9817b708`. Host: `DESKTOP-NFHOK0E`, Ubuntu 24.04.4 LTS, kernel 6.18.33.1-microsoft-standard-WSL2. Login/zone endpoints: 192.168.1.207:7500/7501; chat: 127.0.0.1:7012. Database: local aorebirth-chatengine-mysql-stage6, loopback 33067, database aorebirth_chatengine_stage6. No production host or database was contacted.

The old local staging applications were stopped; their existing data was backed up and preserved. Backup SHA256: `f7737fd6eda75c6374dc9caa6a4cda97ab21848f8cc3f8e35fe0841d978e836e`. Database container identity and original start time remain unchanged. LoginEngine, ZoneEngine_New and ChatEngine run the exact exported Linux apphosts/DLLs recorded in [the receipt](NEWENGINE_9817B708_RELEASE_RECEIPT.json). All startup/database checks pass, and all three headless services report ready.

Both LoginEngine and NewEngine use /var/lib/ao-rebirth/session-ownership, owner root:root, mode 0700. The existing local container's service user remains root. Production's reviewed systemd units use aorebirth:aorebirth; local supervised-process readiness is distinct from actual live systemd verification.

## Client and operator sequence

Official client: E:\Anarchy Online, version 18.8.62_EP1. Anarchy.exe and AnarchyOnline.exe plus the existing local login-key shim are hashed in the receipt. The existing DimensionServer.url resolves to the local launcher endpoint at 127.0.0.1:18080/dimensions_v2.txt, verified HTTP 200 and the correct test IP. The new desktop shortcut is **AORebirth 9817 Test - E Client**. The agent created the launcher but did not launch or control the AO client. Credentials remain in the private existing local credential file.

1. Mike enters the world with Staging50d (9950). If world entry fails, stop there and diagnose authentication/ZoneInfo/admission/SCFU/CharInPlay/ChatEngine evidence.
2. Once initial entry passes, record two real zone changes and inventory movement. The existing .giveitem command can create an actual catalog test item if needed; the connected fixture selected armor 21793, QL 200 using the runtime's wear-requirement check. Confirm suitability for this character before treating it as an equip fixture.
3. Record move/swap/equip/unequip/re-equip identities and state, then clean logout and fresh relogin.
4. After another clean logout, stop and restart the test service, prove a distinct process and readiness, and have Mike log in again. Compare all durable-state fingerprints, allowing only the explicit gameplay operations/time-dependent nano behavior established by evidence.

Local wire recording uses the existing bounded loopback capture method and accepted codec behavior. No new protocol meaning is assumed. The capture is supporting evidence until decoded and correlated with the server logs and Mike's result.

| Official-client gate | Result |
| --- | --- |
| RETAIL_AUTHENTICATION | NOT_RUN |
| RETAIL_CHARACTER_LIST | NOT_RUN |
| RETAIL_CHARACTER_SELECTION | NOT_RUN |
| RETAIL_ZONE_ADMISSION | NOT_RUN |
| RETAIL_CHAR_IN_PLAY | NOT_RUN |
| RETAIL_CHATENGINE_CONNECTION | NOT_RUN |
| RETAIL_WORLD_ENTRY | NOT_RUN |
| RETAIL_ZONE_CHANGE_1 | NOT_RUN |
| RETAIL_ZONE_CHANGE_2 | NOT_RUN |
| RETAIL_INVENTORY_MOVE | NOT_RUN |
| RETAIL_EQUIP | NOT_RUN |
| RETAIL_UNEQUIP | NOT_RUN |
| ITEM_INSTANCE_IDENTITY_PRESERVED | NOT_RUN |
| STATE_AFTER_ZONING | NOT_RUN |
| RETAIL_LOGOUT | NOT_RUN |
| SESSION_CLEANUP | NOT_RUN |
| RETAIL_RELOGIN | NOT_RUN |
| STATE_AFTER_RELOGIN | NOT_RUN |
| TEST_SERVER_RESTART | NOT_RUN |
| RETAIL_LOGIN_AFTER_RESTART | NOT_RUN |
| STATE_AFTER_RESTART | NOT_RUN |
| INVENTORY_AFTER_RESTART | NOT_RUN |
| EQUIPMENT_AFTER_RESTART | NOT_RUN |

Baseline snapshots contain complete deterministic per-table fingerprints for character/stats/inventory/credits/nanos/authored and generated missions, not just row counts. Equipment is represented in item_instances and preserved by instance and placement. Zero baseline nano/mission rows are explicitly recorded. The connected/disposable fixtures separately cover nonempty nano/mission state and actual inventory/equipment operations.

Evidence is under build-verify/release-9817b708 in this worktree: staging-processes.txt, staging-hashes.txt, staging-security-os.txt, staging-logs/, state-*.json, capture-session.json and official-client-9817b708.pcapng. Raw capture, credentials and SQL backup are not committed. All retail results remain NOT_RUN until fresh evidence is available.
