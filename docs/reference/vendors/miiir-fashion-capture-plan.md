# Miiir Fashion Capture Plan

Status: unresolved evidence-acquisition target.

Current verified uncovered total: `570` statel vendors.

The Miiir fashion terminal family has no safe vendor mapping yet. Existing related fashion evidence is limited to `ICCFash` / `Fash`, which is not safe to reuse for Miiir terminals because it is a broader ICC Fashion Supplies family and does not prove Miiir-specific terminal behavior.

Do not create `vendors`, `vendortemplate`, or `shopinventorytemplates` rows from this plan alone. A live-client capture is required before any mapping can be considered.

## Target Templates

| Template ID | Terminal family |
| ---: | --- |
| 99543 | Miiir Armwear |
| 99544 | Miiir Footwear |
| 99545 | Miiir Legwear |
| 99546 | Miiir Handwear |
| 99547 | Miiir Swimwear |
| 99548 | Miiir Chestwear |
| 99550 | Miiir Backwear |
| 99554 | Miiir Headwear |

## Capture Checklist

1. Start the vendor/shop packet logger before entering the shop.
2. Enter one target playfield fresh.
3. Open each listed terminal exactly once.
4. For every terminal, record:
   - playfield ID/name
   - statel ID
   - statel instance
   - template ID
   - expected terminal family/name
   - position
   - observed terminal window title/name
   - shop hash or packet/shop identifier if exposed
   - full item list
   - item low/high IDs if decoded
   - item names
   - item QLs
   - item prices
   - whether the terminal opens empty, errors, or returns a populated shop
5. Save raw capture plus decoded/exported rows.
6. Repeat in the next playfield only after the previous capture is closed and clearly labeled.

## Per-Playfield Terminal Matrix

### 1136 Mir shop clan

| Statel ID | Instance | Template | Expected terminal | Position | Capture required |
| ---: | --- | ---: | --- | --- | --- |
| 74448896 | 0xC0000470 | 99554 | Miiir Headwear | 159,5.01,198.999 | shop id/hash, full inventory, names, QLs, prices |
| 74448897 | 0xC0010470 | 99544 | Miiir Footwear | 143.001,5.011,198.999 | shop id/hash, full inventory, names, QLs, prices |
| 74448898 | 0xC0020470 | 99546 | Miiir Handwear | 143,5.01,195.358 | shop id/hash, full inventory, names, QLs, prices |
| 74448899 | 0xC0030470 | 99545 | Miiir Legwear | 143,5.01,203.283 | shop id/hash, full inventory, names, QLs, prices |
| 74448900 | 0xC0040470 | 99547 | Miiir Swimwear | 159,5.01,196.01 | shop id/hash, full inventory, names, QLs, prices |
| 74448901 | 0xC0050470 | 99548 | Miiir Chestwear | 143,5.01,197.283 | shop id/hash, full inventory, names, QLs, prices |
| 74448902 | 0xC0060470 | 99543 | Miiir Armwear | 143,5.01,201.283 | shop id/hash, full inventory, names, QLs, prices |
| 74448903 | 0xC0070470 | 99550 | Miiir Backwear | 159,5.01,201.955 | shop id/hash, full inventory, names, QLs, prices |

### 1137 mir shop omni

| Statel ID | Instance | Template | Expected terminal | Position | Capture required |
| ---: | --- | ---: | --- | --- | --- |
| 74514432 | 0xC0000471 | 99554 | Miiir Headwear | 159,5,192.88 | shop id/hash, full inventory, names, QLs, prices |
| 74514433 | 0xC0010471 | 99544 | Miiir Footwear | 143.001,5.001,192.88 | shop id/hash, full inventory, names, QLs, prices |
| 74514434 | 0xC0020471 | 99546 | Miiir Handwear | 143,5,189.24 | shop id/hash, full inventory, names, QLs, prices |
| 74514435 | 0xC0030471 | 99545 | Miiir Legwear | 143,5,197.165 | shop id/hash, full inventory, names, QLs, prices |
| 74514436 | 0xC0040471 | 99547 | Miiir Swimwear | 159,5,189.892 | shop id/hash, full inventory, names, QLs, prices |
| 74514437 | 0xC0050471 | 99548 | Miiir Chestwear | 143,5,191.165 | shop id/hash, full inventory, names, QLs, prices |
| 74514438 | 0xC0060471 | 99543 | Miiir Armwear | 143,5,195.165 | shop id/hash, full inventory, names, QLs, prices |
| 74514439 | 0xC0070471 | 99550 | Miiir Backwear | 159,5,195.837 | shop id/hash, full inventory, names, QLs, prices |

### 2096 4holes Fashion

| Statel ID | Instance | Template | Expected terminal | Position | Capture required |
| ---: | --- | ---: | --- | --- | --- |
| 137363456 | 0xC0000830 | 99554 | Miiir Headwear | 164.6,5,237.394 | shop id/hash, full inventory, names, QLs, prices |
| 137363458 | 0xC0020830 | 99547 | Miiir Swimwear | 164.6,5,235.4 | shop id/hash, full inventory, names, QLs, prices |
| 137363460 | 0xC0040830 | 99545 | Miiir Legwear | 164.6,5,233.4 | shop id/hash, full inventory, names, QLs, prices |
| 137363461 | 0xC0050830 | 99548 | Miiir Chestwear | 164.6,5,232.4 | shop id/hash, full inventory, names, QLs, prices |

## Minimum Safe-Mapping Evidence Threshold

A future mapping is only safe if each mapped terminal has direct live evidence tying its statel/template to a shop response.

Minimum acceptable evidence per template:

- terminal opened successfully from the listed statel ID/template ID
- captured packet/shop identifier or hash for that terminal
- full decoded inventory from that exact terminal
- item IDs, item names, QLs, and prices captured
- no mismatch between terminal title/name and expected Miiir category
- repeat evidence from `1136` and `1137` agrees, or any side-specific difference is explicitly captured
- `2096` partial set is not assumed to share stock unless captured data proves it

Do not use `ICCFash` / `Fash` as a substitute unless live capture proves the Miiir terminal itself returns that shop identity.

## Recommended Capture Output

Store raw and decoded capture under:

`tools-temp/vendor-captures/miiir-fashion/YYYYMMDD-HHMM/`

Recommended files:

- `raw/` for packet/log output
- `terminal-captures.csv`
- `inventory-items.csv`
- `capture-summary.json`

Suggested `terminal-captures.csv` columns:

`capture_id, playfield, playfield_name, statel_id, statel_instance, template_id, expected_name, observed_name, coords, shop_identifier, opened, item_count, notes`

Suggested `inventory-items.csv` columns:

`capture_id, statel_id, template_id, shop_identifier, slot, low_id, high_id, item_name, ql, price, count, notes`

## Risks And Ambiguity

- Miiir may be obsolete or deprecated; terminals may open empty on live.
- `1136` and `1137` may be old sided shop layouts with different stock than current neutral/ICC fashion shops.
- `2096` is mixed: four Miiir terminals remain uncovered next to already mapped non-Miiir terminals, so do not infer one family from nearby terminals.
- QL and price may rotate or vary; capture item IDs and observed QLs/prices, and note whether the terminal appears static or randomized.
- Terminal display names may differ from `itemnames.sql`; capture the visible title exactly.
