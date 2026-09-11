# Official EP1 retail gameplay acceptance

Tested source: `032ee4cd39433bbe124217a745474573e720e820`.
Local Linux staging: `aorebirth-linux-staging-032ee4cd`.
Client: `E:\Anarchy Online`, version `18.8.62_EP1`.

Mike confirmed: **"done everything seems to be working"**, in response to the requested login, two zone changes and logout/relogin test.

| Gate | Result | Evidence |
|---|---|---|
| Retail authentication and character selection | PASS | loginengine.log:11-12 and 20-21; selected character 9950 |
| Secure zone admission and world entry | PASS | zoneengine.log:5331-5339 and 5388-5403; user observed working world entry |
| First zone change | PASS | zoneengine.log:5351-5367; PF655 -> PF1136 |
| Second zone change | PASS | zoneengine.log:5368-5383; PF1136 -> PF655 |
| ChatEngine connection | PASS | chatengine.log:19 and 21; actual client connections from 172.19.0.1, distinct from internal ISCom |
| Logout/relogin and saved position | PASS | zoneengine.log:5384 saved (3197.0632,35.100002,892.28345); 5395 rehydrated (3197.06,35.1,892.283), 44 stats and unchanged one-item payload |
| Exact inbound CharInPlay packet | NOT CAPTURED | Capture ended after 600 seconds; gameplay started about 39 minutes after service startup. Zero packets were captured. Do not infer direct packet evidence from gameplay. |
| Official-client login/state after Linux service restart | NOT RUN | Automated distinct-process persistence acceptance remains PASS; no further staging restart was performed during this confirmation. |

The retail loading/hydration blocker is cleared for this tested character and EP1 client. FullCharacter and SCFU are operationally accepted for this bounded world-entry test. The explicit packet and post-restart gates remain open, so **PRODUCTION_CUTOVER_READY=NO**. Production, master, Legacy and both source branches remain unchanged.

Local evidence: `build-verify/hydration/staging-logs/{loginengine,zoneengine,chatengine}.log` and `retail-capture.log`; log hashes are recorded in the JSON receipt. No runtime code or database was changed for this receipt. Existing surface-geometry warnings remain outside the hydration repair; this report does not claim collision/content parity.
