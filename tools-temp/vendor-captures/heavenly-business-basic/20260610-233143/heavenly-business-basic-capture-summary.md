# Heavenly Business Basic Evidence Summary

Evidence source: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\vendor-captures\broad-shop-capture\20260610-233143

Normalized output folder: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\vendor-captures\heavenly-business-basic\20260610-233143

## Scope

Focused on the 10 newly captured unresolved Heavenly Business - Basic terminals only.

## Template Evidence

| Template | Name | Captured VendorId | Rows | Unique Pairs | QL Range | Resolved Rows | Exact Unmapped Statels |
| --- | --- | --- | ---: | ---: | --- | ---: | ---: |
| 155604 | Basic Devices | 299565056 | 27 | 13 | 1-45 | 27 | 2 |
| 155284 | General Tools and Bases - Basic | 299565057 | 12 | 10 | 1-49 | 12 | 5 |
| 155290 | Tools for Electrical and Mechanical Engineering - Basic | 299565058 | 5 | 4 | 10-49 | 5 | 5 |
| 155287 | Pharmacy and Chemistry Tools and Bases - Basic | 299565059 | 20 | 13 | 7-49 | 20 | 5 |
| 155508 | General Recipes - Basic | 299565060 | 17 | 17 | 1-100 | 17 | 5 |
| 155499 | General Components - Basic | 299565061 | 4 | 2 | 1-46 | 4 | 5 |
| 155493 | Mechanical and Electrical Engineering Components - Basic | 299565062 | 17 | 7 | 3-49 | 17 | 5 |
| 155314 | Pharmacy and Chemistry Components - Basic | 299565063 | 37 | 13 | 3-50 | 37 | 5 |
| 155496 | Armour and Clothing Components - Basic | 299565064 | 4 | 2 | 9-37 | 4 | 5 |
| 155311 | Nano Crystal Components - Basic | 299565065 | 58 | 25 | 2-50 | 58 | 5 |

## Inventory Overlap

- 155493 Mechanical and Electrical Engineering Components - Basic <-> 155496 Armour and Clothing Components - Basic: 1 shared low/high pairs
- 155314 Pharmacy and Chemistry Components - Basic <-> 155496 Armour and Clothing Components - Basic: 1 shared low/high pairs
- 155284 General Tools and Bases - Basic <-> 155499 General Components - Basic: 1 shared low/high pairs
- 155287 Pharmacy and Chemistry Tools and Bases - Basic <-> 155314 Pharmacy and Chemistry Components - Basic: 1 shared low/high pairs

## Exact-Template Coverage Potential

Total exact-template unmapped statels for these 10 templates: 47

- 155284 General Tools and Bases - Basic: 5 exact unmapped statels across Parnassos; ord_smarket_clan_basic; ord_smarket_omni_basic; Heavenly Business - Basic
- 155287 Pharmacy and Chemistry Tools and Bases - Basic: 5 exact unmapped statels across Parnassos; ord_smarket_clan_basic; ord_smarket_omni_basic; Heavenly Business - Basic
- 155290 Tools for Electrical and Mechanical Engineering - Basic: 5 exact unmapped statels across Parnassos; ord_smarket_clan_basic; ord_smarket_omni_basic; Heavenly Business - Basic
- 155311 Nano Crystal Components - Basic: 5 exact unmapped statels across Parnassos; ord_smarket_clan_basic; ord_smarket_omni_basic; Heavenly Business - Basic
- 155314 Pharmacy and Chemistry Components - Basic: 5 exact unmapped statels across Parnassos; ord_smarket_clan_basic; ord_smarket_omni_basic; Heavenly Business - Basic
- 155493 Mechanical and Electrical Engineering Components - Basic: 5 exact unmapped statels across Parnassos; ord_smarket_clan_basic; ord_smarket_omni_basic; Heavenly Business - Basic
- 155496 Armour and Clothing Components - Basic: 5 exact unmapped statels across Parnassos; ord_smarket_clan_basic; ord_smarket_omni_basic; Heavenly Business - Basic
- 155499 General Components - Basic: 5 exact unmapped statels across Parnassos; ord_smarket_clan_basic; ord_smarket_omni_basic; Heavenly Business - Basic
- 155508 General Recipes - Basic: 5 exact unmapped statels across Parnassos; ord_smarket_clan_basic; ord_smarket_omni_basic; Heavenly Business - Basic
- 155604 Basic Devices: 2 exact unmapped statels across Parnassos; Heavenly Business - Basic

## Vendor-Family Recommendation

- Keep all 10 templates as separate vendor families. The terminal names describe separate tradeskill categories, and the captured inventories show category-specific stock even where a few construction-kit/base pairs overlap.
- Do not collapse the tools, recipes, components, armor/clothing, and nano-crystal component terminals into one shared inventory.
- The evidence is sufficient for SQL patch planning for 4571 Heavenly Business - Basic and for later exact-template mapping audits across the matching uncovered statels.

## Generated Files

- heavenly-business-basic-template-evidence.csv
- heavenly-business-basic-inventory-items.csv
- heavenly-business-basic-overlap-summary.csv
- heavenly-business-basic-exact-template-coverage.csv
- heavenly-business-basic-capture-summary.md
