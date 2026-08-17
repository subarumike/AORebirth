# Linux login inventory and DailyLogin follow-up

Date: 2026-08-17

## Scope

This follow-up covers two narrow post-validation tasks:

- remove Linux exposure to Windows-only DailyLogin filesystem roots;
- audit the exact live inventory/equipment persistence mapping for character
  `Nanotechnica` after the earlier `ContainerInstance=39` query returned zero
  rows.

No production database rows were inserted, updated, or deleted for this audit.

## DailyLogin platform path result

`DailyLoginRewardRuntime` no longer treats the legacy XAMPP roots as an
unconditional path set. Claim and state file roots now resolve in this order:

- `AO_REBIRTH_DAILY_LOGIN_CLAIMS_ROOTS`;
- `AO_REBIRTH_ZONE_STATE_DIR/daily-login/claims`;
- legacy XAMPP claim roots only when the runtime platform is Windows.

Rewards JSON paths now resolve in this order:

- `AO_REBIRTH_DAILY_LOGIN_REWARDS_JSON`;
- `Content/Daily/rewards.json` under the application base directory;
- `Content/Daily/rewards.json` under the current directory;
- legacy XAMPP rewards JSON paths only when the runtime platform is Windows.

This keeps Windows compatibility while preventing Linux from attempting to
create paths such as `C:\xampp\htdocs\daily\data\claims` under a read-only
release directory.

## Inventory mapping audit

Live read-only SQL against `aorebirth_chatengine_stage6` showed:

```text
character 39 SubaruMike Nanotechnica Online=0 Playfield=6553
items_count_where_containerinstance_39 0
instanced_count_where_containerinstance_39 0
items_counts_where_containertype_39 ContainerType=39 ContainerInstance=104 Count=8
instanced_counts_where_containertype_39 ContainerType=39 ContainerInstance=104 Count=1
```

The earlier zero-count query was aimed at the wrong column. For player
inventory pages, the persisted owner is:

```text
ContainerType = character Id
ContainerInstance = inventory page identity
```

For `Nanotechnica`, character ID `39` owns the visible startup inventory through
`ContainerType=39`. The normal inventory page is `ContainerInstance=104`.

Rows observed under `ContainerType=39, ContainerInstance=104`:

```text
slot 64 Health and Nano Recharger x50
slot 65 Health and Nano Stim x25
slot 66 Blackmane's Belt Component Platform x1
slot 67 Razor's Polarized Specs x1
slot 68 Colonist Survival Pack x1
slot 69 Nano Crystal (Composite Utility Expertise) x1
slot 70 Nanotechnician: Startup Crystal - Ice Flechette x1
slot 71 Solar-Powered Pistol x1
slot 72 Worn Cyberdeck x1
```

## Persistence conclusion

The live database evidence reconciles with the runtime source model:

- `Character` creates `PlayerInventory`.
- `BaseInventoryPage` maps the owning character identity instance into the page
  identity type.
- page reads/writes use `(ContainerType, ContainerInstance)` as the persistence
  key.
- inventory writes fail closed unless aggregate and page hydration are trusted.

Therefore `UNTRUSTED_INVENTORY_CAN_OVERWRITE_DB=NO` remains the supported
conclusion for the normal character save path.

## Windows validation

```text
DailyLoginPathContractTests: PASS
Debug build wrapper: PASS
AOtomation messaging suite: PASS 1018/1018
```

## Linux candidate validation

```text
LinuxBuild\publish-zoneengine.cmd linux-x64 true: PASS
Stage 8 offline ZoneEngine smoke: PASS
```

## Linux production promotion

Validated source commit:

```text
360b3002 Fix DailyLogin Linux paths and document inventory mapping
```

Production release:

```text
/opt/ao-rebirth/zoneengine/releases/dailylogin-path-360b3002
/opt/ao-rebirth/zoneengine/current -> dailylogin-path-360b3002
```

Pre-deploy guard:

```text
online_count 0
previous current release login-hydration-b1c61405
```

Deployment note:

```text
Initial systemd start failed with status=203/EXEC because the uploaded
ZoneEngine apphost lacked executable mode after archive transfer.
No source or database change was required.
The deployed release file mode was corrected with chmod 750 ZoneEngine.
```

Post-deploy validation:

```text
ao-rebirth-zoneengine.service active
ZoneEngine --validate-startup PASS through systemd ExecStartPre
ZoneEngine --validate-database PASS through systemd ExecStartPre
ZoneEngine listener 0.0.0.0:7501 active
LoginEngine listener 0.0.0.0:7500 remained active
```

Immediate post-deploy journal scan did not show new DailyLogin read-only path
errors. A live client login remains the final exercise of the DailyLogin publish
path because DailyLogin board writes occur during character login.
