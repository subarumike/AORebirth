# Daily Login Same-Session Inventory Synchronization Repair

Date: 2026-08-19

## Scope

This repair is limited to the server-to-client inventory notification emitted after a successful Daily Login reward grant. Reward selection, item creation, inventory persistence, claim-ledger persistence, VGTP routing, reconnect ownership, database schema, DNS, and global networking are unchanged.

## Reproduction evidence

- The Daily Login page loaded from the AORebirth endpoint and accepted the claim.
- The server selected Phasefront Banshee item `270996`, reported a successful `TryGrantQuestRewardItem`, and persisted the claim state.
- The active client did not show the granted item and subsequently displayed an empty inventory with impaired same-session actions.
- A cold login restored the complete inventory, displayed the Banshee, and restored sit and attack.
- Production was rolled back to accepted release `reconnect-fe6617b3`; the incident database snapshot remains at `/root/aorebirth-recovery/20260819-character39-dailylogin-270996`.

## Root cause

`TryGrantQuestRewardItem` adds the reward to `BaseInventory.StandardPage` and writes that authoritative aggregate. The Daily Login caller then emitted a `TemplateActionMessage` and `ContainerAddItemMessage` describing an `OverflowWindow` source and target before sending a full update for the standard inventory page.

Those packet identities contradicted the actual in-memory and persisted placement. The established corpse-transfer path targets the character identity and the actual destination slot; it does not claim an overflow target when the item was placed in normal inventory. The Daily Login mismatch is therefore the narrow post-claim session desynchronization boundary.

## Repair

- Remove the synthetic Daily Login overflow packet sequence.
- Retain the authoritative post-persistence `InventoryUpdateMessageHandler.Default.Send` for `BaseInventory.StandardPage`.
- Add a focused contract test requiring the persisted grant to precede the standard-page refresh and forbidding the removed overflow sender.
- Leave crash-reconnect ownership and hydration code unchanged.

## Acceptance state

| Gate | State |
| --- | --- |
| Focused Daily Login contract test | PASS (`DailyLoginClaimRefreshesAuthoritativeInventoryWithoutSyntheticOverflowMove`) |
| Login-key regression | PASS (client-patch offline wrapper self-test) |
| Crash reconnect/hydration regression | PASS (AOtomation contract and client-patch self-test) |
| RoomSpace/client-patch regression | PASS (proxy, forwarding, deployment-helper, and package self-tests) |
| Mandatory repository integration gate | PASS (`11/11`) |
| Linux exact-SHA deployment | NOT DEPLOYED |
| Same-session reward visible | NOT TESTED |
| Same-session full inventory visible | NOT TESTED |
| Same-session movement | NOT TESTED |
| Same-session sit | NOT TESTED |
| Same-session attack | NOT TESTED |
| Same-session continued gameplay | NOT TESTED |

Production remains on `reconnect-fe6617b3`. This document does not claim production acceptance.
