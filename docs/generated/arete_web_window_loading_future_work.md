# Arete Web Window Loading Future Work

Generated: 2026-06-18

## Purpose

Document a future investigation item only. No debugging, code changes, SQL changes, configuration changes, network capture, or behavior changes were performed for this note.

During Arete/Rex live smoke testing, two AO client web windows opened but did not load usable content.

## Observed Symptoms

### Item Store

- Window title: `Item Store`
- URL shown by client: `vgtp://uwg.store.icc-rk/index.app`
- Visible result: service-unavailable text page.
- Exact visible text:
  - `We're sorry, the shop is currently temporarily unavailable. Please wait a moment and try your request again. If this condition persists, please contact Customer Service.`
- Screenshot reference:
  - `C:/Users/Mike/AppData/Local/Temp/codex-clipboard-2705e5a8-e4d8-487e-aa4a-0ae104a578f4.png`

### Daily Login Rewards

- Window title: `Daily Login Rewards`
- URL shown by client: `vgtp://uwg.daily.icc-rk/index.app`
- Visible result: blank white page inside the client web window.
- Screenshot reference:
  - `C:/Users/Mike/AppData/Local/Temp/codex-clipboard-deb2471e-53f4-494f-b614-0d087afb61da.png`

## Current Scope Decision

This is deferred future work. Do not interrupt the current Rex/B18D Cargo Box and quest-chain smoke work to investigate these client web windows.

Do not assume the cause yet. The screenshots prove only that the AO client opened `vgtp://` web windows and failed to load useful content for the store and daily-login apps.

## Future Investigation Questions

- Which client action, packet, or local UI path opens each `vgtp://` window?
- Does AO Rebirth currently send or omit any packet that affects these windows?
- Are `uwg.store.icc-rk` and `uwg.daily.icc-rk` expected to be served by AO Rebirth `WebEngine`, an external service, a client-local resolver, or a live Funcom service?
- Does the current local environment need DNS, hosts-file, proxy, or WebEngine routing for `*.icc-rk` virtual hosts?
- Is the `Item Store` service-unavailable page coming from the client, local AO Rebirth web handling, or an external/unreachable service?
- Is the `Daily Login Rewards` blank page a failed fetch, missing app bundle, script failure, blocked host, or missing server response?
- Are these features required for local AO Rebirth gameplay, or should they be disabled/stubbed safely for now?

## Future Evidence To Gather

- Live-server capture of opening the Item Store and Daily Login Rewards windows, if possible.
- AO Rebirth capture/logs while opening the same windows.
- Search repo for `vgtp`, `uwg.store`, `uwg.daily`, `icc-rk`, item-store, daily-login, and browser/window open packet handling.
- Inspect WebEngine responsibilities and whether it serves client UI apps or only account/admin pages.
- Inspect client configuration or host routing used by the installed AO client for `vgtp://` URLs.
- Compare any packets around client web-window open behavior between live and AO Rebirth.

## Guardrails For Future Work

- Do not guess packet behavior.
- Do not change DNS, hosts-file, proxy, WebEngine, or client configuration without documenting why.
- Do not add store purchases, claims, rewards, paid services, or daily reward logic without separate explicit approval.
- Keep any future implementation gated or isolated until the routing and service expectations are proven.
- Do not let this work block current Rex quest-chain smoke unless one of these windows becomes a direct quest-flow dependency.

## Suggested Future Deliverable

When this is actively investigated, write a separate result document such as:

`docs/generated/client_vgtp_web_window_investigation_result.md`

That future result should include files inspected, packets/logs inspected, source of each response, routing conclusion, and any recommended stub or implementation path.
