# /org rules (retail, declared) and AOR scope - 2026-10-31 / 2026-11-01

Source: `rules.txt`, `notes.txt`, `missing.ods` in `D:\Funcom\AORebirthWebsite\ao\Captures\chat-org`, authored by Bitnykk from retail play.
Rules are **declared**, confirmed where indicated by captures. Packet evidence and the ref catalog are in `org_commands_scenario_capture_20261031.md`.

## 1. AOR scope

- No free/paid account distinction (AOR is open source): any character may lead. The "free player" sentence parts are dropped.
- `/org create` keeps a visible, commented GM-only lock until the feature is tested and patched; it is then removed manually.
- Out of scope: Notum Wars (contracts, controlled fields), Alien Invasion (cities, `/org city`) and Towers.
- Org system messages go to the "Org Msg" channel (existing org chat).
- Durations, rank tables and texts are data-driven (GameData JSON). Governing forms come from `GoverningForms.json` (default form = index 0, Feudalism; ranks listed from highest left to lowest right; Anarchism has a single rank).
- Any schema change for locks, votes or debt needs explicit approval first. Debt persistence for [4B] uses the single approved table `org_member_tax` (replaces the earlier `org_debt` and `org_tax_tick` plan). Votes use the approved tables `org_vote`, `org_vote_choice` and `org_vote_member`, linked to `organizations.Id` (migration `20261112_org_vote.sql`).
- Text commands must not depend on the target identity; the first letter of a name argument is upper-cased (kick by name).

## 2. Create

- Leader of a full team of 6, same side, nobody already in an org. Leader gets rank 0, the 5 others the lowest rank (Peasant = 3 in Feudalism).
- Name: first letter upper case, 4-40 characters, at least 3 alphabetical characters, not already used, no invalid characters.
- Refusals use the Feedback ids and sentences in the catalog.
- DB: row in `organizations` (Comission 1, other fields default), stat 5 = org id, stat 48 = rank for each member.
- Team membership is not org membership: former leavers/kicked characters (no org) can be in a team. `/org create` ignores locks.

## 3. Leader-only commands (rank 0)

A GM may also disband the organization of a targeted leader (Org Msg 119220807, client notice 508/28601677 to online members, disband lockout not applied to the GM) / start a vote.
- Governing form change resets everybody except the president to the lowest rank. Re-applying the SAME form is accepted and also resets the ranks (a way to rebuild the hierarchy).
credits given with `bank add` reduce the debt first (step [4B]; AOR choice, not captured).
- Bank: view refused to the lowest rank (except anarchism, message 237299844; others get 78644776 with the Bank value). `bank add` by anyone who owns the amount, no success message; org bank capped at 3999999999 and the giver keeps the difference (decided, not captured). `bank remove` leader only (non-leader gets 222804051), limited by the org balance (210489620 with %i) and by the leader's 999999999 limit (5429092). Tested with 1 credit.
- Disband needs a client dialog; every member gets a 12 h lock (`disbandLockoutSeconds` 43200 in Gameplay.json).

## 4. Promote / demote / ranks

- Any member can promote/demote a selected member in sight of lower rank, up to one rank below the actor (the leader is the exception: promoting to the top transfers leadership after the 0x0009 dialog).
- Demote at the lowest rank answers the standard "filled up" message. There is no per-rank member limit; keep the retail message as is.
- `/org ranks` is available to every member.

## 5. Invite / join / leave / kick

- Invite: selected target in sight, same side, not in an org, not locked; the inviter must be at least 3 ranks above the lowest rank (rank 2 left of the lowest). Anarchism is exempt. The inviter sees nothing unless the target is another side or already in an org. Refusing the dialog produces nothing.
- Leave: client dialog, then 6 h lock; leaver sees "You've left the organization.", members see "<name> just left your organization."
Kicked characters get a 6 h lock (anarchist leader 12 h).
- Anarchism quorum: 2 distinct kickers remove a member, 3 remove the leader (the successor is drawn among the 3). Requests persist in `org_anar_pend_kick` under the `organizations` row lock; they are cleared when the kicker or target leaves, on disband, and on a governing form change.
- Each recorded (not yet applied) anarchist kick sends the kicker "%d more members need to kick player." (Text.json 233638763, category 508) with the remaining count (member: 1; leader: 2 then 1).
- Org Msg for quorum kicks: member `"<kicker1> and <kicker2> kicked <kicked> from your organization."` (Text.json 169766805 wording, plain text); leader `"<k1>, <k2> and <k3> kicked <leader> from the organization."` (Text.json 33408158, plain text); new leader `%UKqG` ("Leadership has been given to X.", ref reused from the promote capture, draw not captured). Client refs for the kick messages are not captured yet.
- Locks are cumulative: each new lock is added to the remaining one (6 h x 2 minus elapsed online time). Time only elapses while online.

## 6. Votes

- Leader starts: `/org startvote "<text>" <minutes> 1:<a> 2:<b> ...`; 2 to 10 entries; 2 to 10080 minutes.
- Cooldown between votes: 30 minutes by default, configurable as `org.voteCooldownSeconds` in `AORebirth\Config\Gameplay.json` (AOR testing option; retail value 1800 is the default). Other vote constants stay retail hardcoded.
- EVERY member, including the lowest rank, can `/org vote info` and `/org vote <id>`. One vote per member, cannot be changed; the id must exist. Members who have not voted may vote even if offline at start; leavers and kicked members keep their vote.
- Invalid entry (non-numeric or out of range): AOR uses Text.json 179467989 (usage) with the typed number, instead of the retail "exceeds the maximum" text (AOR choice, captured text differs).
- Results: choices sorted by vote count descending, then choice number ascending. Places use the captured ordinals (Text.json category 508, index 0 = First place to 9 = Tenth place). Choices with the same count share a place; a tie line (54457652) is shown when more than one choice shares a place. Zero votes: 88959635. Zero-vote choices are ranked too.
- At the end all members receive the results message. The leader may `stopvote [reason]`. Stopped votes are hidden from `/org vote info`.

## 7. Declared texts

All sentences are consolidated in the catalog of the scenario document (sections 5.1 and 5.2). Client-side dialogs (not in packets): disband "Are you sure you want to disband your organization ? Notice that the contents of the organization bank will be lost and any land control areas and cities controlled by the organization will be destroyed."; leave "Are you sure you want to leave the organization? You will be unable to join a new one for 6 hours"; invitation "Do you want to join the organization "<Org>"?".

## 8. Suggested implementation order (for the next step)

1. Data: ref/sentence catalog JSON, lock durations (base 6 h, disband 12 h, anarchist leader kick 12 h), vote limits, tax/bank caps.
2. Read-only commands: ranks, tax, bank, debt, vote info.
3. Create (GM-locked), invite/join, leave, kick, promote/demote.
4. Leader commands, bank, then tax [4A] (per-member tick, no debt write).
5. Debt and upkeep state [4B] with the approved table `org_member_tax`; then votes [5] and disband.

## 9. Organization tax (step [4A])

- `/org tax` (any member) shows the current tax (147882695, %i). A non-leader trying to change it gets 192685736; the leader gets 198384040 for a negative or non-integer value.
- A valid change (int32, >= 0) sets `Organizations.Tax` and sends an Org Msg to all members, then the feedback 165930356 to the leader.
- Org Msg 89226052 is ref `"]9V7` + text arg (leader name, `s` + length + 1 + name) + `i` + new tax + `u` + old tax (base-85 ints). Captured 0 -> 1: `"]9V7Nykorgtest i!!!!" u!!!!!` (new first, then old).
- Feedback 165930356 is ref `$01,h` + `u` (old) + `i` (new). Captured 0 -> 1: `$01,hu!!!!!i!!!!"` (old first, then new, matching the text "changed from %u to %u").
- If the new tax is strictly higher than the old one, online members get the client dialog 220373365 (OrgServer 0x0008); Yes runs the existing `/org leave` path without a second dialog, No does nothing.
- Tax tick: each member is charged individually every `taxTick` (Gameplay.json > org > taxTick, default 7200) of his personal /played time, only while online. Only applied when the tax is > 0. The leader is not charged in the retail capture.
- Retail upkeep sequence (captured, debt\...-195354): unpaid tick gives warning 84220094 (30 min real time); still unpaid gives debt 125% of tax and 30 min suspension (168308756); suspended members are refused with 153045876 except debt, info and leave; paying at period end moves the debt to the org bank (236096388). Full details in rules.txt [4B].
- Bank cap 3 999 999 999 remains an AOR choice. The earlier 108633128 deduction ref and 78644776 bank-full alert at tick are no longer part of the retail behaviour.
