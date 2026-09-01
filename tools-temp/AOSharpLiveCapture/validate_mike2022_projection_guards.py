from pathlib import Path


SOURCE = Path(__file__).with_name("Mike2022Main.cs")


def require(source, value, description):
    if value not in source:
        raise AssertionError("missing {0}: {1}".format(description, value))


def require_count(source, value, minimum, description):
    count = source.count(value)
    if count < minimum:
        raise AssertionError(
            "missing {0}: expected at least {1}, found {2}".format(
                description, minimum, count
            )
        )


def main():
    source = SOURCE.read_text(encoding="utf-8-sig")

    requirements = {
        "exact AOSharp inventory alias": "using AOInventory = AOSharp.Core.Inventory.Inventory;",
        "GenericCmd opcode": "private const int GenericCmdMessage = 0x52526858;",
        "Use action": "private const int GenericCmdUseAction = 3;",
        "DeleteItem action": "private const int DeleteItemAction = 112;",
        "inventory snapshot file": '"inventory-snapshots.csv"',
        "item-use observation file": '"item-use-observations.csv"',
        "inventory CSV template IDs": "ItemUniqueIdentity,LowId,HighId,QualityLevel,Name,Charges",
        "item-use CSV template IDs": "ItemUniqueIdentity,LowId,HighId,QualityLevel,Name,Charges,ItemSnapshotUtc",
        "outbound item-use parser": "private void CaptureGenericCmdItemUse(",
        "GenericCmd action offset": "ReadInt32BigEndian(packet, 37) != GenericCmdUseAction",
        "GenericCmd target offset": "TryReadIdentity(packet, 53, out targetType, out targetInstance)",
        "DeleteItem target offset": "TryReadIdentity(packet, 37, out targetType, out targetInstance)",
        "main inventory resolution": "AOInventory.Find(slot, out item)",
        "open backpack resolution": "backpack.Find(slot, out item)",
        "complete slot cache key": "this.latestItemsBySlot[snapshot.SlotIdentity] = snapshot;",
        "exact pending-use key": "this.pendingItemUses[targetSlotIdentity] = relatedUse;",
        "server acknowledgment phase": 'outbound ? "USE_REQUEST" : "USE_ACK"',
        "DeleteItem phase": '"DELETE_ITEM"',
        "unresolved item-use validation": "unresolved item-use slot identities=",
        "item-use write error counter": "itemUseObservationErrors",
        "item-use write error validation": "item-use observation write errors=",
        "inventory validation coverage": "playerInventorySnapshot",
        "item-use validation coverage": "itemUseIdentity",
        "visibility sampled heading columns": "EntityHeadingX,EntityHeadingY,EntityHeadingZ,EntityHeadingW,EntityForwardX,EntityForwardZ",
        "aggro pre-heading columns": "PreAggroSampleUtc,PreAggroSourceX,PreAggroSourceY,PreAggroSourceZ",
        "aggro pre-heading relative angle": "PreAggroRelativeAngleDegrees",
        "aggro event-heading relative angle": "EventRelativeAngleDegrees",
        "pre-attack source snapshot": "PreAttackSourceState = preAttackSourceState",
        "pre-attack target snapshot": "PreAttackTargetState = preAttackTargetState",
        "horizontal forward derivation": "private static bool TryGetHorizontalForward(",
        "relative approach angle derivation": "private static double? RelativeHorizontalAngleDegrees(",
        "pre-aggro heading validation": "unprovoked aggro observed without pre-aggro heading correlation",
        "pre-aggro heading validation coverage": "preAggroHeading",
        "container-open subscription": "AOInventory.ContainerOpened += this.OnContainerOpened;",
        "container-open unsubscription": "AOInventory.ContainerOpened -= this.OnContainerOpened;",
        "container-open game-thread queue": "this.inventorySnapshotRequested = true;",
        "inventory mutation cache invalidation": "this.latestItemsBySlot.Clear();",
        "queued inventory snapshot": 'this.WriteInventorySnapshot("deferred-inventory-refresh");',
        "capture-start inventory snapshot": 'this.WriteInventorySnapshot("capture-start");',
        "capture-end inventory snapshot": 'this.WriteInventorySnapshot("capture-end");',
    }
    for description, value in requirements.items():
        require(source, value, description)

    require_count(
        source,
        "this.inventorySnapshotLog = CloseWriter(this.inventorySnapshotLog);",
        2,
        "inventory writer startup-failure and finalization closes",
    )
    require_count(
        source,
        "this.itemUseObservationLog = CloseWriter(this.itemUseObservationLog);",
        2,
        "item-use writer startup-failure and finalization closes",
    )
    require(source, "TryFlush(this.inventorySnapshotLog);", "inventory writer flush")
    require(source, "TryFlush(this.itemUseObservationLog);", "item-use writer flush")

    raw_guard_start = source.index("private bool RawRecaptureRequired()")
    raw_guard_end = source.index("private void WriteEvent(", raw_guard_start)
    raw_guard = source[raw_guard_start:raw_guard_end]
    if "itemUse" in raw_guard or "inventorySnapshot" in raw_guard:
        raise AssertionError("projection gaps must not change raw recapture policy")

    print("MIKE_2022_PROJECTION_GUARDS=PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
