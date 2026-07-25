# Current Task

## Find Person polish (keep working pieces)

Mike status: aggro works (→2m), corpses/loot OK, mish window delete OK, map later.
Still: HP flicker, token at 100% not appearing, key until zone, wrong weapons on some mobs.

Fixes this pass:
- AggroRadius 2m
- healinterval/healdelta=0 + SuspendNpcRegen on spawn
- Token via TryGrantNamedItem finish wire (same path as reward item)
- Key: SendDeleteItem(slot) + CA 0x2F/Despawn
- Gun only when mesh Layer==2 (stop body-pose false guns)

Remote Subway combat restores (Premature Pattern / Filth Flea / Deathless / etc.) merged in; map deferred.
