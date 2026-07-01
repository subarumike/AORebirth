# GenericCmd Use Route Audit

Generated: 2026-06-30 20:01 local

Scope: no-behavior-change audit and guardrail for `GenericCmdAction.Use` routing in `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs`.

## Summary

`GenericCmdMessageHandler` currently treats `Use` as a shared dispatcher for quest targets, inventory, backpacks, private-city terminals, city controller UI, corpse routing, grid terminals, surgery clinics, pooled static dynels/vendors, and statel fallback.

The current branch order is behavior-significant. This report records that order and the ownership boundary each route should keep in future work.

## Current Route Order

| Order | Route name | Target match | Owning system | Capture evidence status | Packet or side effects | Risk | Suggested future owner | Must not be mixed with |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | RexB18DBoxProgress | `RexB18DBoxProgressTracker.TryObserveBoxUse(character, target)` returns true | Quest/Rex | Capture-backed preview flow references `20260614-194454/packets.hex.log` in quest tracker logs | Sends Rex quest preview/delete/handoff effects inside tracker, then `GenericCmd` ack | High: first route can consume a `Use` before any identity-specific route | `RexQuestUseHandler` | Corpse routing, inventory mutation, city/controller routes |
| 2 | InventoryItem | `target.Type == Inventory` | Inventory/item use | Generic server behavior | `client.Controller.UseItem(target)`, then `GenericCmd` ack | Medium: broad type match | `InventoryUseHandler` | Terminal/statel routes, corpse use |
| 3 | WearOrSocialBackpack | `target.Type == ArmorPage || target.Type == SocialPage` and `TryUseBackpackContainer` succeeds | Inventory/backpack | Generic server behavior | Opens backpack container if controller accepts target, then ack | Medium: broad page type match | `BackpackUseHandler` | Social/wear stat changes, terminal routes |
| 4 | BackpackContainer | `target.Type == Container` and `TryGetBackpackPage` succeeds | Inventory/backpack | Generic server behavior | Sends backpack close, marks backpack closed, then ack | Medium: broad container type match | `BackpackUseHandler` | Corpse containers, loot containers |
| 5 | PrivateCityGuestKeyGenerator | `Terminal:5751538B` or runtime `Terminal:574B84AB`, and current playfield is private-city candidate | Guest key/private city | `private_city_capture_20260623_012720`; runtime target added after private test evidence | Creates/persists City Access Card template `280642`, sends `SimpleItemFullUpdate`, sends `ContainerAddItem` to overflow slot `111`, then ack | High: hardcoded captured/runtime terminal identities plus inventory mutation | `PrivateCityGuestKeyUseHandler` | City Controller UI, city ownership, CityAdvantages, org commands |
| 6 | PrivateCityController | `CityController:009C182E`, runtime `CityController:009C6010`, or non-org `CityController:009CA011` | City Controller/private city | `private_city_owned_entry_capture_20260623_021643`; non-org/controller capture evidence logged by handler | Sends captured owner/member or non-org limited `AOTransportSignal` menu sequence, then ack | High: UI-crash-sensitive hardcoded identities and menu packet shape | `CityControllerUseHandler` | Guest key lifecycle, city purchase, ownership management, OrgClient flows |
| 7 | DirectCorpse | `target.Type == Corpse` | Corpse/loot | Generic corpse route; robot corpse behavior has capture-backed lifecycle elsewhere | Calls `Playfield.TryUseCorpse`; if used, delayed `GenericCmd` ack | High: corpse packet shape is client-crash-sensitive | `CorpseUseHandler` | Player death visuals, NPC death packet emission |
| 8 | DeadNpcCorpse | `target.Type == CanbeAffected` and `TryRouteDeadNpcCorpseUse` maps dead NPC to corpse identity | NPC corpse/loot | Cleaning robot corpse work uses this route boundary | Calls `Playfield.TryUseDeadNpcCorpse`; if routed, delayed `GenericCmd` ack for corpse identity | High: broad target type overlaps live NPC/player identities | `NpcCorpseUseHandler` | Live NPC combat, player target use, direct corpse use |
| 9 | CapturedGridTerminal | `Terminal` statel template `95350` with an exact captured grid route for current playfield and terminal | Grid | Captured/user-supplied grid route evidence stored per route; Borealis example `Terminal:C0040320` | Stops movement, clears external door/playfield stats, records diagnostics, teleports to PF152 near captured exit | High: hardcoded captured route table and teleport side effects | `GridTerminalUseHandler` | Surgery clinic, generic statel fallback, private-city terminals |
| 10 | GridEnterTerminal | `Terminal` statel template `95350` with `TeleportProxy2`, excluding captured route table matches | Grid | `playfields.dat` statel behavior, not an exact live packet capture route | Requirement check/feedback or teleport into PF152 using destination terminal lookup | Medium: broad template-based terminal route | `GridTerminalUseHandler` | Captured route overrides, surgery clinic |
| 11 | SurgeryClinic | `Terminal:C00204A2`, `Terminal:C00004A2`, or statel template `43553`/`295742` | Surgery clinic | Captures `20260620-213807` and `20260621-062224` referenced by handler | Deducts 300 credits, sends cash stat update, feedback, `CastNanoSpell`, nano duration, `SpecialUsed`, ack, delayed skill available | High: terminal route mutates credits/stats and sends timed packet sequence | `SurgeryClinicUseHandler` | Grid terminal handling, vendor/static dynel `OnTrade`, inventory use |
| 12 | PoolOnUseOrTrade | `Pool.Instance.Contains(target)` then matching pooled entity/event | Static dynel/vendor | Generic static dynel behavior | Runs `OnUse` event and ack, or `OnTrade` event, creates temp shopping bag, sends trade, then ack; OFAB vendor denial can intercept | High: broad fallback can consume many target types | `StaticDynelUseHandler` / `VendorUseHandler` | Captured identity-specific terminals, corpse routing |
| 13 | StatelFallback | Target not in pool after all earlier routes fail | Statel/doors/fallback | Generic statel behavior | In debug, sends diagnostic chat text; calls `client.Controller.UseStatel(target)` | High: final broad fallback for doors, statels, and missed captured routes | `StatelUseHandler` | Any route with proven captured identity or packet sequence |

## High-Risk Matching Boundaries

- Hardcoded identity routes: guest key terminals, City Controller instances, captured grid route table terminals, surgery clinic terminal identities.
- Broad type routes: `Inventory`, `ArmorPage`, `SocialPage`, `Container`, `Corpse`, and `CanbeAffected`.
- Broad fallback routes: pooled static dynel/vendor handling and statel fallback.
- Stateful context routes: private-city guest key requires private-city playfield context; grid routes require statel template and playfield route context; dead-NPC corpse routing requires playfield corpse state.

## Guardrail Added

`GenericCmdUseRouteClassifier` is a pure route-name classifier. It does not send packets, mutate inventory, call controllers, or change runtime routing. Tests assert the current route order and known target classifications for:

- runtime and captured Guest Key Generator terminals;
- runtime, captured, and non-org City Controller identities;
- direct corpse and dead-NPC corpse routing;
- captured Borealis grid terminal precedence over generic grid and surgery routes;
- surgery clinic terminal route;
- inventory, backpack, pooled static dynel, and statel fallback routes.

This guardrail documents and freezes route-selection intent before any future split into per-system handlers.
