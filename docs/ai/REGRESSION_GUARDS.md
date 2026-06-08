# Regression Guards

Do not regress these verified or repeatedly repaired behaviors:

- `FullCharacter` message version stays `26`.
- Login initializes live-style movement/social state.
- Fake server-side action bootstrap packets stay removed.
- Sit/stand remains working.
- Weapon and armor equip visuals remain working.
- Equipped items persist across relog.
- Normal auto-attacks use `AttackInfo` only unless captured evidence proves another packet path.
- Death/respawn must not return to the white-screen failure.
- Corpse use, item loot, credit loot, XP text, and corpse despawn must stay coherent.
- Player trade must not duplicate item visuals or lose credits.
- Vendor shop buy/sell/close must stay working.
- Shop/vendor database repairs must use captured terminal data when available.
- NPC chase movement must not be patched from intuition.

When one of these areas changes, record the evidence, validation, and remaining risk in the task or project docs.
