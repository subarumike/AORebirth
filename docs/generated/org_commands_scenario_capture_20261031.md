# /org command scenario capture (retail, client-side) - 2026-10-31 / 2026-11-01

Sources (read-only evidence), all under `D:\Funcom\AORebirthWebsite\ao\Captures\chat-org`:
`rules.txt`, `notes.txt`, `missing.ods` (sentences collected by hand), 10 scenario folders (`20261031-*`) and 3 phase-P8 folders (`20261101-142746` leader Nykorgtest, `-142747` non-leader Totoe5, `-144721` non-leader Totoe5 kick by name).
Captures are client-side only (`raw-packets.csv`, `events.log`, `system-messages.log`, `chat-dialogue.log`).
Complements `org_command_capture_20260623_084448.md` and `org_info_client_state_capture_20260623_042326.md`. Rules and AOR scope are in `org_rules_and_aor_scope_20261031.md`.

Evidence levels: **observed** = in packets; **declared** = rules.txt / missing.ods / tester memory; **inferred** = deduced, not proven.

## 1. Key finding: texts are client-side

- Server texts are catalog refs `~&!!!&t<5-char id>` with parameters separated by `s`, `~` or `!!!!`. The client holds the sentences.
- Only `Organization info updated.` is plain text.
- `Feedback` messages (CategoryId 501 / 508) carry numeric MessageIds only.
- Confirmation dialogs (leave, disband, presidency transfer, tax-increase leave, invitation) are shown by the client and are NOT in the packets. The server receives the already-confirmed request.
- Sentences therefore come from `missing.ods` / `rules.txt` and must live in data-driven GameData (ref -> sentence), not in C#.
- The AOR server may keep the retail refs, or send any text format the client accepts; the ref catalog in section 5 is the evidence for what retail sends.

## 2. OrgClient packet (client -> server)

Layout: `7F4B3108`, `u16 command`, target identity, `u32 Unknown1`, optional `u16`-prefixed string.
Command ids: Create 1, Ranks 2, Contract 3, Info 5, Disband 6, StartVote 7, VoteInfo 8, Vote 9, Promote 10, Demote 11, **0x0C kick on the selected target**, **0x0D kick by NAME**, Invite 14, Join 15, Leave 16, Tax 17, Bank 18, BankAdd 19, BankRemove 20, Debt 22, History 23, Objective 24, Description 25, Name 26, GoverningForm 27, StopVote 28.

- 0x0C (enum name "Unknown2") carries the target identity; 0x0D has target 0 and the name in args.
- Kick by name is case-sensitive on retail (`totoe5` -> not found, `Totoe5` OK). **AOR rule: upper-case the first letter of the name server-side** (character creation forbids a lowercase first letter).
- `Unknown1 = 0` on Disband, Join (target `0xDEAA:<org>`), Leave, Promote-confirm; `1` otherwise (inferred: "confirmed after a client dialog").
- The target identity is not reliable for text commands (it varies); server logic must use the sender and the arguments.
- Args formats seen: tax `1` / `5`, name `X`, vote `3`, startvote `"Q" <minutes> <n>:<text> ...`, governingform `faction`.

## 3. OrgServer packet (server -> client)

Layout: `64582A07`, player identity, `u16 subtype`, 8 zero bytes, `0xDEAA:<org>`, payload.

| Sub | Meaning |
|---|---|
| 0x0001 | Contract (out of scope) |
| 0x0002 | Info reply |
| 0x0005 | Invitation dialog; payload = org name + `u32 0` |
| 0x0008 | **"Leave the organization?" dialog after a tax INCREASE** (payload `00000001`). Shown only when the new tax is greater than the old one; otherwise the tax is applied silently |
| 0x0009 | Presidency transfer confirmation (target player identity) |

Dialog texts (declared, client-side): invitation "Do you want to join the organization "<Org>"?"; tax: "The organization tax has been changed to <n> credit(s). Do you wish to leave your organization? You will be unable to join a new one for 6 hours."

## 4. Responses by command (observed packets, sentences declared)

Org system messages arrive as chat-server `GroupMessage`, `ChannelId=1`, `SenderId=0`, channel name **"Org Msg"**. (Messages on other channels in the captures belong to unrelated players and are ignored.)

| Command | Success reply | Org Msg to members | Stats |
|---|---|---|---|
| create | Feedback 501 / 182257540 "You created an organization and became the leader of it." | - | leader SocialStatus 3->15, `Clan=<org>`, `ClanLevel=0`; members `Clan=<org>` |
| info (member) | OrgServer 0x0002 + "Organization info updated." | - | - |
| ranks | `$c[u1s<Form>~<ranks>` | - | - |
| governingform (no args) | `#V)]p~<forms>` | - | - |
| governingform <form> (leader) | `$0:X;` | `#Y8r9s<leader>s<form>` | rank reset not sent as Stat |
| promote | `!ao(2s<target>s<rank>` to promoter, `#"JF6s<promoter>s<rank>` to target | - | target ClanLevel |
| demote | `"9;=Ts<target>s<rank>`, `$q@>ts<demoter>s<rank>` to target | - | target ClanLevel |
| invite (accepted) | joiner: OrgClient 0x0F then `$;EJ2` + OrgInfo | `$<Tm_s<inviter>s<joiner>` | joiner `Clan`, `ClanLevel=lowest` |
| invite (declined/ignored) | no packet | - | - |
| kick | kicked: `&!!!":$6F,Ns<kicker>` | `!]C$ks<kicker>s<kicked>` | `Clan=0` |
| leave | Feedback 508 / 45846567 | `!kjeAs<name>` | `Clan=0`, SocialStatus 1 |
| disband | `%.Neo` | `#`QX`s<leader>` | `Clan=0` online members |
| promote to president (dialog 0x0009) | - | `%UKqGs<newPresident>`; new president `&!!!":"lW;as<old>` | - |
| tax (no arg) | `#ge1Du!!!!<n>` | - | - |
| tax <n> (leader) | `$01,hu` + old + `i` + new (from X to Y); if increased: OrgServer 0x0008 | `"]9V7` + `s` + leader name + `i` + new + `u` + old (captured 0 -> 1: `"]9V7Nykorgtesti!!!!"u!!!!!`) | - |
| bank | `"L&(Yu!!!!<n>` | - | - |
| bank add <n> | none; credits move toon -> org | - | credits |
| bank remove <n> (leader) | none; credits move org -> toon | - | credits |
| debt | `$JG@@i` | - | - |
| startvote | `#?k4$` + `"7-$k` | `$qG9%s<text>u!!!!+s<entries>` | - |
| vote info | `"].9%s...`; `$F1!D` if none | - | - |
| vote <id> | `%).'*s1:<entry>` | - | - |
| stopvote | `#MjDG` | `$1c8ms<reason>` | - |
| vote end | - | `!;Ys!s<text>...` to all members | - |

Create refusals are Feedback 501 (see 5.2). Governing form: applying the CURRENT form again is accepted and resets all ranks except the president (declared by tester, no dedicated capture).

## 5. Ref -> sentence catalog

### 5.1 Refusals and errors (observed ref, sentence from missing.ods/rules.txt)

| Ref | Trigger | Sentence |
|---|---|---|
| `#*:Xm` | any /org command without an org | You're not in an organization. |
| `!Y3)a` | `/org info` on yourself without an org | You are not a member of an organization. |
| `#0%m>` | `/org info` on a selected target without an org | Target is not a member of an organization. |
| `$m0qh` | promote on a non-member/invalid target | You must target a player of your own organization to promote her/him. |
| `#M?Vc` | demote on a non-member | You must target a player of your own organization to demote her/him. |
| `%:/@"~~#_`93s<rank>` | promote above allowed level | You can only promote someone to one rank-level below your own level (the exception is the leader, who - when a new leader is appointed through promotion - will lose the leadership). (free-player sentence ignored in AOR) |
| `$jh(cs<name>` | demote when no lower rank exists | You cannot demote <name> to that rank because it is filled up. (standard retail message meaning "no lower rank"; there is NO per-rank member limit; keep the message) |
| `$I:N3` | invite by a rank too low | Your rank is not high enough for you to be able to invite new members. You must be at least 3 ranks above the lowest rank. |
| `%`cZp` | invite other side | The target is not from your side. All players must have the same side in your organization. |
| `"Y^0os<name>` | invite already in an org | <name> is already member of another organization. Players must leave their current organizations before joining another. |
| `$3*4s` | kick refused for rank: selected target (0x0C) AND by name (0x0D, captured 2026-11-01 14:47) | You have to be the leader, or someone 3 organizational ranks above the player to kick him... |
| `"#Irns<name>` | kick by name, name unknown/online character not found (also wrong case) | There is currently no active character called '<name>'. |
| `$Q-c>` | kick yourself (selected or by name) | You can't kick yourself. Use '/org leave' instead. |
| `%a;V\s<name>` | kick of a non-member (selected or by name) | <name> is not a member. |
| `!Y9V.` | governingform by a non-leader | Only the leader can change the governing form of the organization. |
| `#L.-2` / `$cZ=l` / `"!Qr$` | description / objective / history by a non-leader | Only the leader is allowed to change the description / objective / history. |
| `#L8'X` | name by a non-leader | Only the leader is allowed to change the name. |
| `$[a;"` | tax by a non-leader | Only leaders can change the organization tax. |
| `"3ue2` | disband by a non-leader | Only the leader or a GM can disband your organization. |
| `%r0M2` | startvote by a non-leader | Only the leader or a GM can initiate voting for your organization. |
| `";`_V` | startvote with a duration out of range (1 min tested) | The legal vote time is between 2 and 10080 minutes. ( 10080 minutes is 1 week ). |
| `"l9r3` | startvote with 0, 1 or 11 entries | Voting should have a minimum of 2 and a maximum of 10 choices. |
| `#0k,>u!!!!?...` | startvote within 30 min of the previous vote | There is a minumum period of 30 minutes between voting. Voting may be started in N minutes from now. |
| `"mLTEs` | vote on an entry that does not exist | The number [] exceeds the maximum number of options on vote. Please do a '/org vote info' for details. |
| `%rgLG` | vote twice, or try to change a vote | You have already cast your vote. You cannot vote more than once on this matter. |
| `%7dp,` | bank remove by a non-leader | Only leaders can take credits out of the organization. |
| `%#`LIu` | bank remove above the balance (parameter = balance) | You cannot withdraw more credits from the organizational bank account than is currently in it. The account holds N credits right now. |
| `"]LeU` | bank add above own credits (to confirm in a capture) | You cannot add more credits than you own. (Text.json 89364632) |
| - | bank add invalid, zero or negative amount (declared, no capture) | The specified amount of credits is not allowed. (Text.json 5429092) |
| - | bank add when the Bank is already at 3999999999 (AOR only, declared) | The specified amount of credits is not allowed. (Text.json 5429092) |
| - | bank remove by the leader when the toon would exceed 999999999 (declared, no capture; the 'reach your limit' sentence is not in Text.json) | The specified amount of credits is not allowed. (Text.json 5429092) |
| - | kick by name refused for a non-leader, "offline" case | Only the leader can kick this character. (declared in missing.ods; NO ref captured; the captured by-name rank refusal uses `$3*4s`) |
| client-side | invited character under lock | Unable to perform action, able in HH:MM:SS (evaluated by the client, see section 7) |

Create refusals (Feedback CategoryId 501, MessageId -> sentence):
- 89300600 (empty name) / 24190273 (`1234`): The organization name must contain at least 3 alphabetical characters. / invalid length (see rules.txt; the exact id-to-sentence pairing for these two must be taken from missing.ods).
- 167950692 (name taken): The name is already used by another organization.
- 13951557 (`artes`): The name cannot start with a lowercase letter.
- 13952082 (`#lol`): The organization name contained an invalid character. The character might not be allowed at all, or not in combination with the surrounding characters.
- 174731972 (partial team, mixed sides, non-leader, member already in an org): You can only create an organization of a full 6 member team. All members must be of the same side. No members can be part of any other organization. (free-player sentence ignored in AOR)

### 5.2 Info sentences (declared)

- `$c[u1s<Form>~<ranks>`: "Governing form: Feudalism / Ranks: Lord, Knight, Vassal, Peasant".
- `$01,hu` + old + `i` + new: "Organizational tax changed from X to Y credits." (leader only)
- `"]9V7` + leader name + `i` + new + `u` + old: "Your leader, <name>, just changed the organizational tax. The new tax is Y credits (the old value was X)." (Org Msg, all members)
- `#ge1Du!!!!<n>`: "The current level of organizational tax is N."
- `$JG@@i`: "Your debt to the organization is N credits."
- `"L&(Yu!!!!<n>`: "Organization bank account: N credits."
- `!ao(2s` / `#"JF6s`: "You promoted X to the rank of R." / "You were just promoted by P to the rank of R."
- `"9;=Ts` / `$q@>ts`: "You demoted X to the rank of R." / "You were just demoted by D to the rank of R."
- `$0:X;`: "Governing form changed for the organization."; `#Y8r9s...`: "<leader> changed governing form to '<form>'."
- `!kjeAs`: "<name> just left your organization."; `!]C$ks`: "<kicked> was kicked by <kicker>."; `&!!!":$6F,Ns`: "<kicker> kicked you from your organization."; `$;EJ2`: "You've joined the organization."; `$<Tm_s`: "<inviter> invited <joiner> to your organization."
- `%UKqGs`: "Leadership has been given to <name>."; `&!!!":"lW;as`: "Leadership was given to you by <old>."
- `%.Neo`: "Organization disbanded."; `#`QX`s`: "Your leader, <name> has disbanded your organization."
- `#?k4$~"7-$k`: "Organization members have been informed. Voting can begin in a few seconds. Use '/org vote info' to show the voting status."
- `$qG9%s...`: Org Msg "Voting notice for "<text>"... Duration, Candidates, Voting can begin in a few seconds. Use '/org vote <voteentry id>' to cast your vote."
- `"].9%s...`: "Voting information for ... Candidates ... Status: N minutes remaining. You have not voted yet / your choice."
- `%).'*s...`: "Your vote has been cast. You voted for 1:Simpson".
- `!;Ys!s...`: "Results for ... Status: N member voted. First place ( N vote ): ..."
- `$F1!D`: "No voting in progress."; `#MjDG`: "Voting stopped."; `$1c8ms<reason>`: "Organization leader has stopped the voting with message : "<reason>""
- `%).'*s` and the other vote texts take the entries as `n:text` separated by spaces.
- Fragments `#g:]I`, `"fdKu`, `"9U8%`, `"k2UU`, `$J*Dq` and the demote summary `$KHl9~~...~~#-CF8` were seen but are not mapped to a sentence with certainty (probably parts of the vote info / ranks summaries). They are not needed for the first implementation.

## 6. Stat observations

- Create: see section 4. Promote/demote: only `ClanLevel` changes.
- Governing form change: the rank reset is NOT sent as a Stat (visible at the next update).
- Join: `Clan=<org>`, `ClanLevel=lowest`. Kick: `Clan=0`, ClanLevel not reset. Leave: `Clan=0`, SocialStatus 1 + Feedback 508.
- Disband: `Clan=0` to online members; leader SocialStatus back to 3.
- Rank chain, Department (from lowest): Unit Member 6, Unit Leader 5, Unit Commander 4, Squad Commander 3, General 2, 1, 0.

## 7. Invitation and locks

- Accepting an invitation: joiner sends OrgClient 0x0F (target `0xDEAA:<org>`, Unknown1=0); about 0.12 s later the server answers `$;EJ2` + OrgInfo + Stats Clan/ClanLevel (observed for Nykorgtest2, Testou, Totoe5).
- Refusing or ignoring: no outgoing packet.
- Invited character under lock (Hazorgtest): the server still sends OrgServer 0x0005 (identical to a normal invitation). The CLIENT then shows "Unable to perform action, able in HH:MM:SS" to the invited; the inviter sees nothing (declared).
anarchist leader kicked 12 h, disbanded org 12 h for everyone (`disbandLockoutSeconds` 43200).
- **OPEN:** the packet/stat that tells the client the remaining lock time was not identified (the leave reply carries only Feedback 508, empty OrgInfo and Stats `Clan=0`, SocialStatus 1). The AOR implementation must either find it (compare Hazorgtest's login stats) or reproduce the retail behavior with a dedicated capture.

## 8. Amounts

The bank cap 3999999999 is supported by captured views (3999999999 at 11:24 and 11:26, 3000000001 at 11:26:38 on Old Athen). The toon limit 999999999 and the overflow rule (giver keeps the difference) are declared in rules.txt, not captured.

## 9. Still open

- Lock-time transport (section 7).
- Refs for "Only the leader can kick this character." and "You cannot receive that many credits...", "You cannot add more credits than you own." (sentences known).
- Mapping of the fragments listed at the end of 5.2 and the id-to-sentence pairing of the first two create refusals.
- Semantics of `Unknown1` (inferred only).
