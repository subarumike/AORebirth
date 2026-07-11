# Cursor pet-work export (full day snapshot)

Hand-off folder so you and a friend can share pet/summon/heal/combat work **without git merge hell**.

## What is inside

**~45 files** from today's work — not only the last heal tweak:

- Pet summon (Belamorte + Demon, dual slot, SpellList, SCFU, AddPet/RemovePet)
- Shell items (Engineer/Bureaucrat), `summonpet` / `summonpets` nanos
- Pet commands (follow, attack, heal, wait, terminate)
- Attack pet combat packets
- Heal pet (Belamorte's Blessing), focus target, follow heal target
- Zone login / pool duplicate-key fix, nano restore, inventory shell use
- Subway spawn stat guards (same session crashes)
- DB: `mobtemplate.sql` + DAO helpers for BSLX / PT56

Full list: **`MANIFEST.txt`**

## Friend: install snapshot

1. Back up files you changed locally.
2. Put this `Cursor` folder inside your `AORebirth\` project (same level as `Server\`, `Libraries\`).
3. Double-click **`apply_copy.cmd`** (or copy paths from `MANIFEST.txt` by hand).
4. Rebuild in Visual Studio:
   - **ZoneEngine**
   - **AORebirth.Database**
   - **AORebirth.Stats**
   - **AORebirth.ObjectManager** (if Pool.cs changed)
5. Run updated `mobtemplate.sql` inserts on MySQL if you don't have BSLX/PT56 yet.
6. Restart engines, logout/login.

## You: refresh after more work

Double-click **`export_copy.cmd`** — re-copies everything listed in `MANIFEST.txt` from live project into `Cursor\`.

## Share

Zip the whole **`AORebirth\Cursor`** folder and send (Discord, USB, etc.). Paths inside zip stay `Server\...` and `Libraries\...`.

## Git tip

- Do **not** both push the same pet files while the other pulls.
- Use this folder OR separate branches (`pet-work` vs `subway-work`).
- Optional: add `Cursor/` to `.gitignore` so snapshots never hit the remote.
