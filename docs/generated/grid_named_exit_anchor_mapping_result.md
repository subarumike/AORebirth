# Grid Named Exit Anchor Mapping

## Scope

Connect named grid-side exit landing spots to the matching outside `Enter The Grid` terminals.

The user supplied these PF `152` interior landing spots on 2026-06-22. They are used as the authoritative grid-side anchors for the named city/hub entrances below.

## Mapping

| Named grid exit | Outside PF | Outside terminal | Grid-side landing | Heading source | Nearest PF 152 exit statel |
| --- | --- | --- | --- | --- | --- |
| Tir | `640` | `Terminal:C0030280` | `(156.0, 3.8, 185.1)` | nearest exit statel | `Terminal:C00B0098` |
| Omni Trade | `710` | `Terminal:C00502C6` | `(165.2, 3.8, 235.0)` | nearest exit statel | `Terminal:C0000098` |
| Newland | `567` | `Terminal:C0010237` | `(177.7, 3.8, 181.7)` | nearest exit statel | `Terminal:C00D0098` |
| Old Athen | `540` | `Terminal:C00A021C` | `(210.2, 3.8, 172.8)` | nearest exit statel | `Terminal:C04A0098` |
| Unicorn Defence Hub | `6007` | `Terminal:C0001777` | `(209.4, 3.8, 210.5)` | nearest exit statel | `Terminal:C0580098` |

## Playfield inspection

Packed `playfields.dat` confirms these outside entrance terminals:

| PF | Name | `TemplateId 95350` terminal |
| --- | --- | --- |
| `540` | Old Athen | `Terminal:C00A021C` |
| `567` | Newland | `Terminal:C0010237` |
| `6007` | BS Signup (dng) / Unicorn Defence Hub route | `Terminal:C0001777` |
| `640` | Tir | `Terminal:C0030280` |
| `710` | Omni-1 Trade | `Terminal:C00502C6` |

`Terminal:C0040320` in PF `800` and `Terminal:C002022C` in PF `556` remain separate routes; these named anchor notes do not prove their destinations.
