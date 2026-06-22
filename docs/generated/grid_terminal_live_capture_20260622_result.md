# Grid Terminal Live Capture 2026-06-22

## Scope

Capture source terminal use and grid-side landing transforms for live `Enter The Grid` terminals used by `Youwillmezz`.

Capture folder:

- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260622-003221`

## Captured mappings

| Source PF | Source terminal | Source evidence | Grid landing evidence | Grid landing | Heading | Nearest grid exit |
| --- | --- | --- | --- | --- | --- | --- |
| `567` | `Terminal:C0010237` | `events.log:140-145` | `events.log:346-347` | `(177.5012, 3.7750, 179.1060)` | `(0, -0.01321465, 0, 0.9999127)` | `Terminal:C00D0098` |
| `705` | `Terminal:C00602C1` | `events.log:2420-2423` | `events.log:2624-2625` | `(174.8353, 37.3750, 240.2071)` | `(0, 1, 0, -4.371139E-08)` | `Terminal:C0010098` |
| `730` | `Terminal:C00002DA` | `events.log:3109-3112` | `events.log:3313-3314` | `(174.8353, 37.3750, 240.2071)` | `(0, 1, 0, -4.371139E-08)` | `Terminal:C0010098` |
| `695` | `Terminal:C00802B7` | `events.log:3806-3809` | `events.log:4010-4011` | `(188.4104, 37.37542, 234.9863)` | `(0, 0.9952047, 0, 0.09781352)` | `Terminal:C0030098` |
| `670` | `Terminal:C003029E` | `events.log:5421-5424` | `events.log:5639-5640` | `(203.2488, 37.38467, 222.7339)` | `(0, -0.9641039, 0, 0.2655251)` | `Terminal:C0070098` |
| `560` | `Terminal:C0060230` | `events.log:7519-7522` | `events.log:7733-7734` | `(183.9474, 44.0150, 150.8788)` | `(0, 0.7062106, 0, 0.7080019)` | `Terminal:C00E0098` |
| `700` | `Terminal:C00202BC` | `events.log:8380-8383` | `events.log:8594-8595` | `(224.5596, 44.0050, 231.8543)` | `(0, -0.7107669, 0, 0.7034276)` | `Terminal:C0090098` |
| `505` | `Terminal:C02301F9` | `events.log:9339-9342` | `events.log:9563-9564` | `(215.8618, 43.9950, 151.7285)` | `(0, 0.6904334, 0, 0.7233959)` | `Terminal:C0100098` |

Previously captured mapping retained:

| Source PF | Source terminal | Source evidence | Grid landing evidence | Grid landing | Heading | Nearest grid exit |
| --- | --- | --- | --- | --- | --- | --- |
| `655` | `Terminal:C002028F` | `20260621-091447/events.log:255-256,321-322` | `20260621-091447/events.log:645-646` | `(234.3062, 3.7750, 212.8138)` | `(0, 1, 0, -4.371139E-08)` | `Terminal:C04E0098` |

## Statel inspection

`playfields.dat` reports all inspected source terminals as `TemplateId 95350` with raw `TeleportProxy2` args `[51102,152,0,0]`, which computes destination instance `C0000098`. Live capture proves this raw proxy tuple is not enough to derive the real grid-side landing for every terminal.

Read-only `teleports` table inspection found no terminal-row override for `C0010237`, `C002028F`, `C00602C1`, `C00002DA`, `C00802B7`, or `C003029E`. The later captured `C0060230`, `C00202BC`, and `C02301F9` routes were identified from the same live capture and nearest PF `152` statel inspection.

## Implementation rule

Use captured route entries for the terminals above. For terminals without capture-backed landing evidence, keep the existing statel-based route path; do not assign them one of these captured landings unless later capture proves it.
