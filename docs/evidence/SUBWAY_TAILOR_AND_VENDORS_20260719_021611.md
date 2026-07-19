# Subway Tailor and vendor evidence - 20260719-021611

Source capture: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260719-021611`

This is an evidence-only record. It does not authorize replacing the current Subway
runtime stock snapshot. The exact captured 203-row ShopUpdate projection is checked in
as [data/subway-vendors-20260719-021611.csv](data/subway-vendors-20260719-021611.csv).

## Owner-to-terminal mapping

Each mapping is supported by an outbound GenericCmd Use targeting the SimpleChar,
an inbound acknowledgement, a ShopUpdate from the listed VendingMachine, and paired
Trade-open messages.

| Merchant | Capture owner identity | Capture terminal identity | Captured rows | Current canonical owner | Current canonical terminal | Current template |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Tailor | `0x79775804` | `0x12F6284F` | 22 | `0x79135F51` | `0x12ECC394` | 99637 |
| Basic Quality Weaponsdealer | `0x79775805` | `0x12F62850` | 31 | `0x79135F52` | `0x12ECC395` | 99572 |
| Basic Quality Armorer | `0x79775806` | `0x12F62851` | 29 | `0x79135F53` | `0x12ECC396` | 99570 |
| Basic Quality Pharmacist | `0x79775807` | `0x12F62852` | 40 | `0x79135F54` | `0x12ECC397` | 99574 |
| Basic Tools Merchant | `0x79775808` | `0x12F62853` | 19 | `0x79135F55` | `0x12ECC398` | 99601 |
| Container Supplier | `0x79775809` | `0x12F62854` | 62 | `0x79135F56` | `0x12ECC399` | 99634 |

The five non-Tailor vendors were opened directly with GenericCmd Use. Tailor's
captured dialogue ended with an answer list containing only `goodbye`; a distinct
GenericCmd Use subsequently opened the Tailor shop. The dialogue did not itself emit
the ShopUpdate.

## Tailor dialogue

The Tailor conversation starts with an outbound client `KnuBotOpenChatWindow`
targeting the Tailor owner identity. The server echoes the open and sends the
following captured dialogue tree. The first greeting was `Howdy.`; an immediate
close/reopen produced the alternate greeting `Yes?` with the same root choices.

| Node | Captured prompt | Captured choices |
| --- | --- | --- |
| Root | `Howdy.` / reopen `Yes?` | `How is life?`; `Um, I'd just like to look at your wares.`; `Goodbye` |
| Life | `Just ask. ` followed by an empty append | `Would you like to tell me a bit about yourself?`; Jobe Armor measurements; `Goodbye` |
| About | `Not much to tell really... ` followed by literal `\nLife has actually become more interesting recently.` | Jobe Armor measurements; wares; `Goodbye` |
| Measurements | `Certainly. Which armor piece do you need measurements for? ` | Pants; Sleeves; Boots; Gloves; Vest; Helmet; Support System; Shoulderpad; `Goodbye` |
| Measurement complete | `There you go.  Now, is there something else I can help you with? ` | More measurements; wares; `No, that was it I believe.  Thank you!`; `Goodbye` |
| Thanks | `You're welcome. ` | `Goodbye` |
| Wares | `Of course!` followed by `To do that you just left-clik the Shopping Basket icon at the bottom of this window.` | `Goodbye` |

The `left-clik` spelling is capture-exact. The second Wares append has `Unknown2=1`;
all other captured append segments have `Unknown2=0`. Choosing a measurement emitted
the following exact QL 1 item award before the common completion response:

| Choice index | Measurement item |
| ---: | ---: |
| 0 | `256415` - Pants |
| 1 | `256416` - Sleeves |
| 2 | `256417` - Boots |
| 3 | `256418` - Gloves |
| 4 | `256419` - Breastplate |
| 5 | `256420` - Helmet |
| 6 | `256421` - Life Support System |
| 7 | `256422` - Shoulder Pads |

Each award used `TemplateAction` values `Unknown1=1`, `Unknown2=87`, placement
`OverflowWindow:0`, followed by `ContainerAddItem` from `OverflowWindow:0` to the
player overflow window at placement `111`. The Wares choice only explains the basket;
the later basket `GenericCmd Use` remains the independent shop-open action.

## Stock comparison

The capture contains six ShopUpdate messages and 203 stock rows. When the capture
terminal identities are mapped to the current canonical terminal identities and the
existing test canonicalization is applied, this capture has fingerprint:

`b4004d6a7469c6d8c8f10677092bd44bef8d486beb5a1254335e0d61d29d5acf`

The current runtime snapshot fingerprint is:

`df02869ae481758d371dc23c9a4f5f11734d7aae97648f4b2e040de2daa21507`

| Merchant | Current rows | Captured rows | Same slot, item pair, and QL | Comparison |
| --- | ---: | ---: | ---: | --- |
| Tailor | 21 | 22 | 0 | All prior slots differ; only two item pairs overlap anywhere. Captured slot 21 is `41014/41014`, QL 1. |
| Basic Quality Weaponsdealer | 31 | 31 | 0 | Every slot differs; only one item pair overlaps anywhere. |
| Basic Quality Armorer | 29 | 29 | 0 | Every slot differs; eight item pairs overlap anywhere. |
| Basic Quality Pharmacist | 40 | 40 | 40 | Exact match. |
| Basic Tools Merchant | 19 | 19 | 8 | Eight more slots retain their item pair with a different QL; slots 0, 3, and 5 use different item pairs. |
| Container Supplier | 62 | 62 | 62 | Exact match. |

## Evidence decision

- Preserve the current runtime stock and fingerprint.
- Preserve this capture as another exact stock observation.
- Do not append Tailor slot 21 as guaranteed stock.
- Treat Tailor, Weaponsdealer, Armorer, and part of Tools as evidence of variable
  inventory selection.
- Pharmacist and Container are deterministic across the compared observations.
- Vendor-specific item pools, selection weights, refresh timing, and QL rolling rules
  remain unresolved. More observations may expand the evidence pool, but one snapshot
  cannot prove those algorithms.

## Evidence limitations

- This capture has no VendorFullUpdate for these merchants.
- Its four raw NPC SimpleCharFullUpdates belong to unrelated Vagabond and Mugger
  entities, not the six vendors. Existing appearance, template, and Tailor pet-flag
  values are therefore not changed by this evidence.
- The `0x79775804-0x79775809` and `0x12F6284F-0x12F62854` identities are
  capture-session identities and must not replace the canonical source identities.
