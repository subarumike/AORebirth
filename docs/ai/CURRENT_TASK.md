# Current Task

## Current Focus

Mail Sent/Expires timestamps fixed from live capture `20260715-Recive-mail-datetime-stamp`.

## Done in this slice

- Capture truth: list/detail wire ints after Subject are **Sent unix** and **Expire unix** (not credits/COD). Money is `ExtendedField64`. `TimeField` = 0 on live Market mail.
- Credit-delivery expire = Sent + 2 days; player mail uses the same.
- Flags base `0x7C`, bit0 = read.

## Remaining

1. Restart engines; send **new** mail; confirm Sent ≈ now and Expires ≈ +2 days.
2. Subway when Mike returns that priority.

## Constraints

- Mail in-memory only.
- Commit Mail before pull; `git pull --no-rebase`.
