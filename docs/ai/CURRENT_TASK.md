# Current Task

## Current Focus

Mail subsystem (`Core/Mail`): Return to Sender (action 7) + Sent/Expires day encoding.

## Done in this slice

- Implemented `MailAction.ReturnToSender` (7): remaining attachments/credits return to original sender as new mail (`Returned: …`), arrival time = now.
- Sent uses local arrival day as wire `TimeField` (days since 1970-01-01). Client Expires = Sent + 2 days. Server purge matches that expiry day.
- Snapshot `SentDayNumber` at enqueue so list/detail stay stable.

## Remaining

1. Restart engines and live-validate: new mail shows today’s Sent and Expires = +2 days; Return to Sender delivers letter back.
2. Live GUI shows Sent/Expires as `YYYY-Mon-DD 00:00` (day resolution — live client does not show wall-clock time on those columns).
3. Subway when Mike returns that priority.

## Constraints

- Mail still in-memory only.
- Commit Mail before pull; `git pull --no-rebase`.
