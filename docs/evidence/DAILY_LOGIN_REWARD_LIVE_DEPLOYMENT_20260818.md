# Daily Login fixed-reward live deployment

Date: 2026-08-18

## Scope

ZoneEngine-only deployment of the fixed Phasefront random reward grant path.
LoginEngine, ChatEngine, Daily Rewards web content, claim ordering, and reward
definitions were not changed by this deployment.

## Source and validation

- Windows `master` source SHA: `676c2d569685f5e768326832bfe8c0c940eb098b`
- Source commit: `676c2d56 Preserve fixed Phasefront daily rewards`
- Linux target: `linux-x64`, self-contained
- Guarded source inventory: PASS
- Stage 8 offline ZoneEngine smoke: PASS

## Production promotion

- Service: `ao-rebirth-zoneengine.service`
- Previous release: `/opt/ao-rebirth/zoneengine/releases/reconnect-fe6617b3`
- New immutable release: `/opt/ao-rebirth/zoneengine/releases/dailyreward-676c2d56`
- Active link: `/opt/ao-rebirth/zoneengine/current`
- Rollback target retained: `/opt/ao-rebirth/zoneengine/releases/reconnect-fe6617b3`

The guarded upgrade initially stopped before promotion because the previous
release was `root:root:755`. Its permissions were normalized to the checked-in
upgrade contract (`root:aorebirth`, directories/apphost `750`, files `640`),
then the guarded promotion completed successfully.

## Post-promotion status

- ZoneEngine: active/running, restart count `0`
- ZoneEngine listener: `0.0.0.0:7501`, owned by the new ZoneEngine process
- LoginEngine: active and not redeployed
- ChatEngine: active and not redeployed
- Daily Login account `subarumike`: days `1` through `27` taken, day `28`
  available, no last-claim or last-granted timestamp

## Remaining acceptance

Official-client acceptance is still required: claim day 28 once and confirm
that the granted fixed Phasefront item appears in character `39` inventory.
