# Bank Wear Mirror Capture Result

## Capture

- Folder: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260621-073837`
- Character: `Youwillmezz`
- Status: complete

## Captured Sequence

- `events.log:97-102`: client sent `ClientContainerAddItem` from `Inventory:0051` to `Bank:3CAC6F14`; server replied with `ContainerAddItem` into bank slot `0`.
- `events.log:142-220`: zoning initialized playfield `0320`, then emitted the player `SimpleCharFullUpdate`.
- `events.log:221-224`: server emitted `WeaponItemFullUpdate` for owner `SimpleChar:3CAC6F14`, item instances `WeaponInstance:17B305EE` and `WeaponInstance:17B3075D`, with wear-style slots `0x106` and `0x108`.
- `events.log:255-256`: server emitted `FullCharacter` after those weapon-definition packets.
- `packets.hex.log:149-150`: `FullCharacter` contained the same item instances `17B305EE` and `17B3075D` in the inventory slot payload.

## Conclusion

The bank deposit acknowledgement is not the point where the Wear tab is populated. The mirrored icons are produced during zone/login state serialization because bank page items are included in the outgoing character inventory payload and bank page weapon items are included in weapon-definition fanout.

## Validation

- `cmd /d /c git diff --check`: PASS
- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS
- `cmd /d /c restart-engines.cmd`: PASS
- Live smoke by Mike: PASS; deposited bank items no longer appear in the Wear Weapon tab after zone/relog, and bank withdraw still works.
