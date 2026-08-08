from pathlib import Path

# --- PlayerController: restore backup SetNanoDuration with durationIdentity ---
p = Path(r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Controllers\PlayerController.cs")
t = p.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\r", "\n")

old = """                // Capture 20260723-053632 Sparrow Flight: SpellList after OnUse morph/flight.
                AdventurerMorphFlightRuntime.OnMorphNanoApplied(this.Character, nanoId);

                // Hoverboard/Phasefront: always register NCU. Attribute 8 can be 0 on some
                // vehicle nanos — without SetNanoDuration morph applies with no buff icon,
                // then zone/cancel desync (morph without NCU or NCU without morph).
                if (AdventurerMorphFlightRuntime.IsMorphFlightNano(nanoId))
                {
                    int morphDuration = duration > 0
                        ? duration
                        : AdventurerMorphFlightRuntime.FallbackNcuDurationCentiseconds;
                    if (!ActiveNanoRuntimeService.Default.ApplyActiveNano(
                            this.Character,
                            nanoId,
                            morphDuration,
                            target))
                    {
                        // NCU full / blocked — do not leave orphaned vehicle morph.
                        AdventurerMorphFlightRuntime.CancelVehicleMorphNano(this.Character, nanoId);
                        ChatTextMessageHandler.Default.Send(
                            this.Character,
                            "Not enough NCU to keep this nano running.");
                    }
                    else
                    {
                        CharacterActionMessageHandler.Default.NotifyActiveNanoDuration(
                            this.Character,
                            target,
                            nanoId,
                            morphDuration);
                        if (this.Character.Controller != null && this.Character.Controller.Client != null)
                        {
                            SimpleCharFullUpdate.SendToOne(
                                this.Character,
                                this.Character.Controller.Client);
                        }
                    }
                }
                else if (duration > 0 && !NanoEventRuntimeService.Default.HasOffensiveHitOnUse(nano))
                {
                    // Instant Hit drain nanos must not be treated as NCU buffs on the caster.
                    CharacterActionMessageHandler.Default.SetNanoDuration(
                        this.Character,
                        target,
                        nanoId,
                        duration);
                }
            }"""

new = """                // Capture 20260723-053632 Sparrow Flight: SpellList after OnUse morph/flight.
                AdventurerMorphFlightRuntime.OnMorphNanoApplied(this.Character, nanoId);

                // Instant Hit drain nanos must not be treated as NCU buffs on the caster.
                if (duration > 0 && !NanoEventRuntimeService.Default.HasOffensiveHitOnUse(nano))
                {
                    // Capture 20260806-085523: self-cast Target is often None — duration
                    // Identity must still be the caster SimpleChar or cancel cannot reverse morph.
                    Identity durationIdentity = (target.Type != IdentityType.None && target.Instance != 0)
                                                   ? target
                                                   : this.Character.Identity;
                    CharacterActionMessageHandler.Default.SetNanoDuration(
                        this.Character,
                        durationIdentity,
                        nanoId,
                        duration);
                }
                else if (duration <= 0
                         && AdventurerMorphFlightRuntime.IsMorphFlightNano(nanoId)
                         && !NanoEventRuntimeService.Default.HasOffensiveHitOnUse(nano))
                {
                    // Some vehicle nanos report attribute 8 as 0; still need an NCU entry.
                    Identity durationIdentity = (target.Type != IdentityType.None && target.Instance != 0)
                                                   ? target
                                                   : this.Character.Identity;
                    CharacterActionMessageHandler.Default.SetNanoDuration(
                        this.Character,
                        durationIdentity,
                        nanoId,
                        AdventurerMorphFlightRuntime.FallbackNcuDurationCentiseconds);
                }
            }"""

# FallbackNcuDurationCentiseconds won't exist after backup restore - add constant to backup file OR use literal
# Better: add Fallback constant to restored backup morph file after copy

assert t.count(old) == 1, "PlayerController block not found: " + str(t.count(old))
t = t.replace(old, new, 1)
p.write_bytes(t.replace("\n", "\r\n").encode("utf-8"))
print("PlayerController OK")

# --- Add Fallback constant to restored morph runtime ---
m = Path(r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\AdventurerMorphFlightRuntime.cs")
mt = m.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\r", "\n")
if "FallbackNcuDurationCentiseconds" not in mt:
    mt = mt.replace(
        "        public const int HoverboardNanoId = 281569;\n",
        "        public const int HoverboardNanoId = 281569;\n\n"
        "        /// <summary>Used only when nanos.dat attribute 8 is 0 for a morph/flight nano.</summary>\n"
        "        public const int FallbackNcuDurationCentiseconds = 1440000; // 4 hours\n",
        1,
    )
    m.write_bytes(mt.replace("\n", "\r\n").encode("utf-8"))
    print("Fallback const added")
else:
    print("Fallback already present")

# Fix Modifier casing if backup has lowercase
mt2 = m.read_text(encoding="utf-8")
if ".modifier" in mt2 and ".Modifier" not in mt2.replace(".modifier", ""):
    pass
# replace lowercase modifier property access
count = mt2.count(".modifier")
if count:
    # only property access on Stats
    mt2 = mt2.replace(".modifier", ".Modifier")
    m.write_bytes(mt2.replace("\r\n", "\n").replace("\n", "\r\n").encode("utf-8") if "\r\n" not in mt2[:100] else mt2.encode("utf-8"))
    # simpler:
    text = Path(m).read_text(encoding="utf-8").replace("\r\n", "\n")
    text = text.replace(".modifier", ".Modifier")
    Path(m).write_bytes(text.replace("\n", "\r\n").encode("utf-8"))
    print("Modifier casing fixed", count)

# --- Remove Reconcile from ActiveNanoRuntimeService ---
a = Path(r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\ActiveNanoRuntimeService.cs")
at = a.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\r", "\n")

at = at.replace(
"""            if (persistedNanos == null || persistedNanos.Count == 0)
            {
                // No buffs to restore — still clear orphaned hoverboard/Phasefront stats.
                AdventurerMorphFlightRuntime.ReconcileVehicleMorphAfterNanoRestore(character);
                return;
            }""",
"""            if (persistedNanos == null || persistedNanos.Count == 0)
            {
                return;
            }""",
1)

at = at.replace(
"""            this.SyncCurrentNcuStat(character);

            AdventurerMorphFlightRuntime.ReconcileVehicleMorphAfterNanoRestore(character);
        }

        public void CleanupOrphanSummonPetNanosAfterPetRestore(ICharacter character)""",
"""            this.SyncCurrentNcuStat(character);
        }

        public void CleanupOrphanSummonPetNanosAfterPetRestore(ICharacter character)""",
1)

old3 = """            if (!hasZoneTransferStash && !hasDbActiveNanos)
            {
                if (hasPendingPetRestore)
                {
                    PetRuntimeService.Default.ClearPendingRestoreForOwner(characterId);
                    LogUtil.Debug(
                        DebugInfoDetail.GameFunctions,
                        "Cleared stale pet pending restore on login char=" + characterId);
                }

                // Still clear orphaned morph stats when nothing is restored to NCU.
                ThreadPool.QueueUserWorkItem(
                    _ =>
                    {
                        Thread.Sleep(250);
                        ICharacter character = client.Controller != null ? client.Controller.Character : null;
                        if (character == null)
                        {
                            return;
                        }

                        AdventurerMorphFlightRuntime.ReconcileVehicleMorphAfterNanoRestore(character);
                    });
                return;
            }"""
new3 = """            if (!hasZoneTransferStash && !hasDbActiveNanos)
            {
                if (hasPendingPetRestore)
                {
                    PetRuntimeService.Default.ClearPendingRestoreForOwner(characterId);
                    LogUtil.Debug(
                        DebugInfoDetail.GameFunctions,
                        "Cleared stale pet pending restore on login char=" + characterId);
                }

                return;
            }"""
assert at.count(old3) == 1, at.count(old3)
at = at.replace(old3, new3, 1)
a.write_bytes(at.replace("\n", "\r\n").encode("utf-8"))
print("ActiveNano Reconcile removed")

# --- ClientConnected: HealOrphaned after FullCharacter ---
c = Path(r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs")
ct = c.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\r", "\n")
if "HealOrphanedVehicleMorphOnLogin" not in ct:
    needle = """                    CombatXpRuntimeService.LogXpWireSnapshot(
                        client.Controller.Character,
                        "ClientConnected",
                        "zone-login-after-fullchar");

                    // FullCharacter has no perk list yet — re-teach trained perks immediately
"""
    insert = """                    CombatXpRuntimeService.LogXpWireSnapshot(
                        client.Controller.Character,
                        "ClientConnected",
                        "zone-login-after-fullchar");

                    // Stuck hoverboard/yalm: MonsterData/IsVehicle can persist after NCU cancel
                    // or unequip if MorphState was lost on reboot.
                    AdventurerMorphFlightRuntime.HealOrphanedVehicleMorphOnLogin(
                        client.Controller.Character);

                    // FullCharacter has no perk list yet — re-teach trained perks immediately
"""
    assert ct.count(needle) == 1
    ct = ct.replace(needle, insert, 1)
    c.write_bytes(ct.replace("\n", "\r\n").encode("utf-8"))
    print("ClientConnected HealOrphaned OK")
else:
    print("ClientConnected already has HealOrphaned")
