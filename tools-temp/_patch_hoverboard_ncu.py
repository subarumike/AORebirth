from pathlib import Path

# --- ActiveNanoRuntimeService ---
p = Path(r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\ActiveNanoRuntimeService.cs")
t = p.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\r", "\n")

old1 = """            if (persistedNanos == null || persistedNanos.Count == 0)
            {
                return;
            }"""
new1 = """            if (persistedNanos == null || persistedNanos.Count == 0)
            {
                // No buffs to restore — still clear orphaned hoverboard/Phasefront stats.
                AdventurerMorphFlightRuntime.ReconcileVehicleMorphAfterNanoRestore(character);
                return;
            }"""
assert t.count(old1) == 1, t.count(old1)
t = t.replace(old1, new1, 1)

old2 = """            this.SyncCurrentNcuStat(character);
        }

        public void CleanupOrphanSummonPetNanosAfterPetRestore(ICharacter character)"""
new2 = """            this.SyncCurrentNcuStat(character);

            AdventurerMorphFlightRuntime.ReconcileVehicleMorphAfterNanoRestore(character);
        }

        public void CleanupOrphanSummonPetNanosAfterPetRestore(ICharacter character)"""
assert t.count(old2) == 1, t.count(old2)
t = t.replace(old2, new2, 1)

old3 = """            if (!hasZoneTransferStash && !hasDbActiveNanos)
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
new3 = """            if (!hasZoneTransferStash && !hasDbActiveNanos)
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
assert t.count(old3) == 1, t.count(old3)
t = t.replace(old3, new3, 1)

p.write_bytes(t.replace("\n", "\r\n").encode("utf-8"))
print("ActiveNano OK")

# --- PlayerController cast path ---
p2 = Path(
    r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Controllers\PlayerController.cs"
)
t2 = p2.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\r", "\n")
old4 = """                // Capture 20260723-053632 Sparrow Flight: SpellList after OnUse morph/flight.
                AdventurerMorphFlightRuntime.OnMorphNanoApplied(this.Character, nanoId);

                // Instant Hit drain nanos must not be treated as NCU buffs on the caster.
                if (duration > 0 && !NanoEventRuntimeService.Default.HasOffensiveHitOnUse(nano))
                {
                    CharacterActionMessageHandler.Default.SetNanoDuration(
                        this.Character,
                        target,
                        nanoId,
                        duration);
                }
            }"""
new4 = """                // Capture 20260723-053632 Sparrow Flight: SpellList after OnUse morph/flight.
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
assert t2.count(old4) == 1, t2.count(old4)
t2 = t2.replace(old4, new4, 1)
p2.write_bytes(t2.replace("\n", "\r\n").encode("utf-8"))
print("PlayerController OK")
