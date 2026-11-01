# /org rules (retail, declared) and AOR scope - 2026-10-31 / 2026-11-01

Source: `rules.txt`, `notes.txt`, `missing.ods` in `D:\Funcom\AORebirthWebsite\ao\Captures\chat-org`, authored by Bitnykk from retail play.
Rules are **declared**, confirmed where indicated by captures. Packet evidence and the ref catalog are in `org_commands_scenario_capture_20261031.md`.

## 1. AOR scope

- No free/paid account distinction (AOR is open source): any character may lead. The "free player" sentence parts are dropped.
- `/org create` keeps a visible, commented GM-only lock until the feature is tested and patched; it is then removed manually.
- Out of scope: Notum Wars (contracts, controlled fields), Alien Invasion (cities, `/org city`) and Towers.
- Org system messages go to the "Org Msg" channel (existing org chat).
- Durations, rank tables and texts are data-driven (GameData JSON). Governing forms come from `GoverningForms.json` (default form = index 0, Feudalism; ranks listed from highest left to lowest right; Anarchism has a single rank).
- Any schema change for locks, votes or debt needs explicit approval first.
- Text commands must not depend on the target identity; the first letter of a name argument is upper-cased (kick by name).

## 2. Create

- Leader of a full team of 6, same side, nobody already in an org. Leader gets rank 0, the 5 others the lowest rank (Peasant = 3 in Feudalism).
- Name: first letter upper case, 4-40 characters, at least 3 alphabetical characters, not already used, no invalid characters.
- Refusals use the Feedback ids and sentences in the catalog.
- DB: row in `organizations` (Comission 1, other fields default), stat 5 = org id, stat 48 = rank for each member.
- Team membership is not org membership: former leavers/kicked characters (no org) can be in a team. `/org create` ignores locks.

## 3. Leader-only commands (rank 0)

- `/org name`, `description`, `objective`, `history` (255 chars for the last three), `governingform`, `tax <n>`, `bank remove`, `startvote`, `stopvote`, `disband`. A GM may also disband / start a vote.
- Governing form change resets everybody except the president to the lowest rank. Re-applying the SAME form is accepted and also resets the ranks (a way to rebuild the hierarchy).
- Tax: shows the value; non-leaders cannot change it. The "leave the org?" dialog (OrgServer 0x0008) is sent ONLY when the new tax is greater than the old one; a lower or equal tax is applied silently. Collected every 2 hours of played time from connected members; a member without enough credits drops to 0 and the shortfall becomes debt (`/org debt`); credits given with `bank add` reduce the debt.
- Bank: view refused to the lowest rank (except anarchism); `bank add` by anyone who owns the amount; org bank capped at 4,000,000,000 (excess is lost); `bank remove` limited by the org balance and by the toon limit of 1,000,000,000. Tested with 1 credit.
- Disband needs a client dialog; every member gets an 11 h lock.

## 4. Promote / demote / ranks

- Any member can promote/demote a selected member in sight of lower rank, up to one rank below the actor (the leader is the exception: promoting to the top transfers leadership after the 0x0009 dialog).
- Demote at the lowest rank answers the standard "filled up" message. There is no per-rank member limit; keep the retail message as is.
- `/org ranks` is available to every member.

## 5. Invite / join / leave / kick

- Invite: selected target in sight, same side, not in an org, not locked; the inviter must be at least 3 ranks above the lowest rank (rank 2 left of the lowest). Anarchism is exempt. The inviter sees nothing unless the target is another side or already in an org. Refusing the dialog produces nothing.
- Leave: client dialog, then 6 h lock; leaver sees "You've left the organization.", members see "<name> just left your organization."
- Kick: same right as invite AND a strictly higher rank than the kicked (anarchism exempt); applies immediately, no confirmation, no selection or line of sight needed (by name also works for offline characters). A non-eligible rank gets `$3*4s`; self-kick and non-member have their own messages. Kicked characters get a 6 h lock (anarchist leader 11 h).
- Locks are cumulative: each new lock is added to the remaining one (6 h x 2 minus elapsed online time). Time only elapses while online.

## 6. Votes

- Leader starts: `/org startvote "<text>" <minutes> 1:<a> 2:<b> ...`; 2 to 10 entries; 2 to 10080 minutes; 30 minutes minimum between votes.
- EVERY member, including the lowest rank, can `/org vote info` and `/org vote <id>`. One vote per member, cannot be changed; the id must exist.
- At the end all members receive the results message. The leader may `stopvote [reason]`.

## 7. Declared texts

All sentences are consolidated in the catalog of the scenario document (sections 5.1 and 5.2). Client-side dialogs (not in packets): disband "Are you sure you want to disband your organization ? Notice that the contents of the organization bank will be lost and any land control areas and cities controlled by the organization will be destroyed."; leave "Are you sure you want to leave the organization? You will be unable to join a new one for 6 hours"; invitation "Do you want to join the organization "<Org>"?".

## 8. Suggested implementation order (for the next step)

1. Data: ref/sentence catalog JSON, lock durations (base 6 h, disband/anarchist 11 h), vote limits, tax/bank caps.
2. Read-only commands: ranks, tax, bank, debt, vote info.
3. Create (GM-locked), invite/join, leave, kick, promote/demote.
4. Leader commands, bank, tax, then votes and disband.
5. Locks and debt persistence only after explicit schema approval.