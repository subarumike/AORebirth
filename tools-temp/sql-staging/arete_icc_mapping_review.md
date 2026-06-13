# Arete ICC Staged Mapping Review

- Source capture: `20260613-172753`
- Scope: five core Arete ICC vendors only. Incidental nearby vendors are excluded.
- Expected coverage after import validation: `147 -> 142`.
- New shop inventory groups: 5. Existing exact inventory reuse: none.

| VendorId | Template | Terminal | VT Hash | Shop Hash | Inventory Rows | QL Range | Capture Identity |
| ---: | ---: | --- | --- | --- | ---: | --- | --- |
| `429457412` | `297320` | ICC Basic Implants | `AIYZS6H` | `B5K4` | 39 | 1-10 | `(VendingMachine:12D1BF1D)` |
| `429457411` | `297321` | ICC Faded Clusters | `AIBFIDW` | `QDLJ` | 166 | 5-50 | `(VendingMachine:12D1BF1C)` |
| `429457410` | `297322` | ICC Bright Clusters | `AIGFHES` | `RLKT` | 166 | 5-50 | `(VendingMachine:12D1BF1B)` |
| `429457409` | `297323` | ICC Shiny Clusters | `AIZDXJ7` | `EFER` | 166 | 5-50 | `(VendingMachine:12D1BF1A)` |
| `429457408` | `297325` | ICC Pharmacy | `AIYTWIL` | `KPMO` | 36 | 5-30 | `(VendingMachine:12D1BF19)` |

## Validation Notes

- All five core targets have VendorFullUpdate and ShopUpdate evidence.
- No `155225` VendorFullUpdate rows were present in the capture.
- Faded, Bright, and Shiny cluster inventories are distinct by exact `(LowId, HighId, QL)` signature.
- No exact inventory signature matched existing shop inventory SQL/staging surfaces.
