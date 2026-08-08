namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Nanos;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;

    using Utility;

    using ZoneEngine.Core.Packets;

    /// <summary>
    /// Vehicle / camo morph nanos (capture-backed SpellList + MonsterShape/CanFly).
    /// Capture 20260806-085523 (live):
    /// - 281569 hoverboard: CastNano → Finish/SetNanoDuration/Feedback; cancel Buff only
    /// - 288546 Phasefront Loungemaster - The Executive: SpellList apply + Buff+SpellList cancel
    /// - 270542 Phasefront Phantom - Camo: SpellList apply + Buff+SpellList cancel
    /// Capture 20260723-053632 Sparrow Flight 82835.
    /// </summary>
    public static class AdventurerMorphFlightRuntime
    {
        public const int SparrowFlightNanoId = 82835;

        /// <summary>Capture 20260806-085523 — first cast (hoverboard vehicle nano).</summary>
        public const int HoverboardNanoId = 281569;

        /// <summary>Used only when nanos.dat attribute 8 is 0 for a morph/flight nano.</summary>
        public const int FallbackNcuDurationCentiseconds = 1440000; // 4 hours

        /// <summary>Capture 20260806-085523 — Phasefront Loungemaster - The Executive.</summary>
        public const int PhasefrontExecutiveNanoId = 288546;

        /// <summary>Capture 20260806-081845 / 20260806-085523 — Phasefront Phantom - Camo.</summary>
        public const int PhasefrontPhantomCamoNanoId = 270542;

        private const int CapturedCharacterInstance = 0x00139459;

        private const int CapturedPhasefrontCharacterInstance = 0x762ABC21;

        private const int FightingRestrictFlag = 2;

        // Full capture wires (zone header + N3 SpellList body), identities patched at send.
        private static readonly byte[] SparrowApplyWire = Hex(
            "00E1000A000101A800000DB9001394594D4501140000C3500013945900000023790000CFB7000143930000000400000000000000010000000000000001000000090000009C000004B00000CFB7000143930000000400000000000000010000000000000001000000090000009A000000B60000CFB70001439300000004000000000000000100000000000000010000000900000099000000B60000CFB7000143930000000400000000000000010000000000000001000000090000009B000000B60000CF44000143930000000400000000000000010000000000000001000000090000769D000000000000CF4C0001439300000004000000000000000100000000000000010000000900000002000000000000CF92000143930000000400000003000002130000000000000000000000000000000000000070000000000000000000000004000000010000000000000001000000090000CF3B0001439300000004000000000000000100000000000000020000000900042B8C0000C350001394590000C3500013945900000E53706172726F7720466C69676874010000CF1B000143930000000000");

        private static readonly byte[] SparrowRemoveWire = Hex(
            "010C000A000101A800000DB9001394594D4501140000C3500013945900000023790000CFB7000143930000000400000000000000010000000000000001000000090000009C000004B00000CFB7000143930000000400000000000000010000000000000001000000090000009A000000B60000CFB70001439300000004000000000000000100000000000000010000000900000099000000B60000CFB7000143930000000400000000000000010000000000000001000000090000009B000000B60000CF44000143930000000400000000000000010000000000000001000000090000769D000000000000CF4C0001439300000004000000000000000100000000000000010000000900000002000000000000CF92000143930000000400000003000002130000000000000000000000000000000000000070000000000000000000000004000000010000000000000001000000090000CF3B0001439300000004000000000000000100000000000000020000000900042B8C0000C350001394590000C3500013945901000E53706172726F7720466C69676874010000CF1B000143930000000000");

        // Capture 20260806-085523 IN SpellList after FinishNanoCasting 270542.
        private static readonly byte[] PhasefrontCamoApplyWire = Hex(
            "00B5000A000101A300000DAF762ABC214D4501140000C350762ABC2100000013B50000CF35000420CE0000000400000000000000010000000000000001000000090000009C000000960000CF4C000420CE00000004000000000000000100000000000000010000000900000002000000000000CF44000420CE00000004000000070000000C0000447A000000000000000C00001727000000000000000000000000000000030000000C000044780000000000000000000000000000000300000167000420A20000000000000000000000000000000300000001000000000000000100000009000420A2000000000000CF44000420CE00000004000000070000000C00005B4A000000000000000C00001735000000000000000000000000000000030000000C00005B480000000000000000000000000000000300000167000420A20000000000000000000000000000000300000001000000000000000100000009000420A2000000000000C350762ABC210000C350762ABC21000019506861736566726F6E74205068616E746F6D202D2043616D6F010000CF1B000420CE0000000000");

        // Capture 20260806-081845 IN SpellList after RemoveFriendlyNano 270542 (remove flag 01).
        private static readonly byte[] PhasefrontCamoRemoveWire = Hex(
            "029A000A0001012700000DBC762ABC214D4501140000C350762ABC210000000FC40000CF35000420CE0000000400000000000000010000000000000001000000090000009C000000960000CF4C000420CE00000004000000000000000100000000000000010000000900000002000000000000CF44000420CE00000004000000070000000C0000447A000000000000000C00001727000000000000000000000000000000030000000C000044780000000000000000000000000000000300000167000420A20000000000000000000000000000000300000001000000000000000100000009000420A2000000000000C350762ABC210000C350762ABC21010019506861736566726F6E74205068616E746F6D202D2043616D6F010000CF1B000420CE0000000000");

        // Capture 20260806-085523 IN SpellList "Phasefront Loungemaster - The Executive".
        private static readonly byte[] PhasefrontExecutiveApplyWire = Hex(
            "009C000A000101B100000DAF762ABC214D4501140000C350762ABC2100000013B50000CF35000467220000000400000000000000010000000000000001000000090000009C000000960000CF4C0004672200000004000000000000000100000000000000010000000900000002000000000000CF440004672200000004000000070000000C0000447A000000000000000C00001727000000000000000000000000000000030000000C00004478000000000000000000000000000000030000016700046718000000000000000000000000000000030000000100000000000000010000000900046718000000000000CF440004672200000004000000070000000C00005B4A000000000000000C00001735000000000000000000000000000000030000000C00005B48000000000000000000000000000000030000016700046718000000000000000000000000000000030000000100000000000000010000000900046718000000000000C350762ABC210000C350762ABC21000027506861736566726F6E74204C6F756E67656D6173746572202D2054686520457865637574697665010000CF1B000467220000000000");

        // Capture 20260806-085523 IN SpellList remove for Executive (remove flag 01).
        private static readonly byte[] PhasefrontExecutiveRemoveWire = Hex(
            "00A5000A0001013500000DAF762ABC214D4501140000C350762ABC210000000FC40000CF35000467220000000400000000000000010000000000000001000000090000009C000000960000CF4C0004672200000004000000000000000100000000000000010000000900000002000000000000CF440004672200000004000000070000000C0000447A000000000000000C00001727000000000000000000000000000000030000000C00004478000000000000000000000000000000030000016700046718000000000000000000000000000000030000000100000000000000010000000900046718000000000000C350762ABC210000C350762ABC21010027506861736566726F6E74204C6F756E67656D6173746572202D2054686520457865637574697665010000CF1B000467220000000000");

        private static readonly byte[] CanFlyEffectBlock = Hex(
            "0000CF9200014393000000040000000300000213000000000000000000000000000000000000007000000000000000000000000400000001000000000000000100000009");

        private static readonly object Gate = new object();

        private static readonly Dictionary<int, MorphState> States = new Dictionary<int, MorphState>();

        public static bool IsVehicleMorphNano(int nanoId)
        {
            return nanoId == SparrowFlightNanoId
                   || nanoId == HoverboardNanoId
                   || nanoId == PhasefrontExecutiveNanoId
                   || nanoId == PhasefrontPhantomCamoNanoId;
        }

        public static bool IsMorphFlightNano(int nanoId)
        {
            if (IsVehicleMorphNano(nanoId))
            {
                return true;
            }

            NanoFormula nano;
            if (!NanoLoader.NanoList.TryGetValue(nanoId, out nano) || nano.Events == null)
            {
                return false;
            }

            foreach (var ev in nano.Events)
            {
                if (ev.EventType != EventType.OnUse || ev.Functions == null)
                {
                    continue;
                }

                foreach (var fn in ev.Functions)
                {
                    if (fn.FunctionType == (int)FunctionType.MonsterShape
                        || fn.FunctionType == (int)FunctionType.CanFly)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static void SyncExpansionPlayfield(ICharacter character, int playfieldId)
        {
            if (character == null)
            {
                return;
            }

            character.Stats[StatIds.expansionplayfield].Value = IsShadowlandsPlayfield(playfieldId) ? 1 : 0;
        }

        public static bool IsShadowlandsPlayfield(int playfieldId)
        {
            return playfieldId >= 4000 && playfieldId <= 4999;
        }

        public static void NoteScalingModify(Character character, int statId, int amount)
        {
            if (character == null || amount == 0)
            {
                return;
            }

            MorphState state = GetOrCreate(character.Identity.Instance);
            int prior;
            if (state.ScalingModifiers.TryGetValue(statId, out prior))
            {
                state.ScalingModifiers[statId] = prior + amount;
            }
            else
            {
                state.ScalingModifiers[statId] = amount;
            }
        }

        /// <summary>
        /// Reverse ScalingModify amounts tracked on MorphState. CalculateSkills ClearModifiers
        /// can already wipe Stat.Modifier while MorphState still holds the deltas — blind
        /// subtract then stuck characters at e.g. runspeed modifier -240.
        /// </summary>
        private static void ReverseTrackedScalingModifiers(Character ch, MorphState state)
        {
            if (ch == null || state == null || state.ScalingModifiers.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<int, int> pair in state.ScalingModifiers.ToList())
            {
                int tracked = pair.Value;
                if (tracked == 0)
                {
                    continue;
                }

                int current = ch.Stats[pair.Key].Modifier;
                int reverse;
                if (tracked > 0)
                {
                    reverse = Math.Min(tracked, Math.Max(0, current));
                }
                else
                {
                    reverse = Math.Max(tracked, Math.Min(0, current));
                }

                if (reverse != 0)
                {
                    ch.Stats[pair.Key].Modifier -= reverse;
                }
            }

            state.ScalingModifiers.Clear();
        }

        /// <summary>
        /// Clear orphaned negative runspeed left by double-reverse after ClearModifiers.
        /// </summary>
        public static void HealOrphanedRunspeedModifierOnLogin(ICharacter character)
        {
            Character ch = character as Character;
            if (ch == null)
            {
                return;
            }

            int runMod = ch.Stats[StatIds.runspeed].Modifier;
            if (runMod >= 0)
            {
                return;
            }

            if (HasActiveMorphNano(ch))
            {
                return;
            }

            MorphState state;
            lock (Gate)
            {
                States.TryGetValue(ch.Identity.Instance, out state);
            }

            if (state != null && state.ScalingModifiers.Count > 0)
            {
                ReverseTrackedScalingModifiers(ch, state);
            }

            // Still negative with no active morph ScalingModify nano — clamp orphan.
            if (ch.Stats[StatIds.runspeed].Modifier < 0)
            {
                ch.Stats[StatIds.runspeed].Modifier = 0;
                try
                {
                    ch.WriteStats();
                }
                catch
                {
                }
            }
        }

        public static void ApplyMonsterShape(Character character, int shapeId)
        {
            if (character == null || shapeId <= 0)
            {
                return;
            }

            MorphState state = GetOrCreate(character.Identity.Instance);
            if (!state.ShapeApplied)
            {
                state.PreviousMonsterData = character.Stats[StatIds.monsterdata].Value;
                state.PreviousCatMesh = character.Stats[StatIds.catmesh].Value;
                state.PreviousDisplayCatMesh = character.Stats[StatIds.displaycatmesh].Value;
                state.ShapeApplied = true;
            }

            character.Stats[StatIds.monsterdata].Value = shapeId;
            character.Stats[StatIds.catmesh].Value = shapeId;
            character.Stats[StatIds.displaycatmesh].Value = shapeId;
            character.ChangedAppearance = true;
            if (character.Playfield != null)
            {
                character.Playfield.AnnounceAppearanceUpdate(character);
            }

            ZoneClient client = character.Controller != null
                                    ? character.Controller.Client as ZoneClient
                                    : null;
            if (client != null)
            {
                SimpleCharFullUpdate.SendToPlayfield(client);
            }
        }

        public static void EnableFlight(Character character)
        {
            if (character == null)
            {
                return;
            }

            MorphState state = GetOrCreate(character.Identity.Instance);
            if (!state.FlightEnabled)
            {
                state.PreviousIsVehicle = character.Stats[StatIds.isvehicle].Value;
                state.FlightEnabled = true;
            }

            character.Stats[StatIds.isvehicle].Value = 1;
        }

        public static void RestrictActions(Character character, int flags)
        {
            if (character == null)
            {
                return;
            }

            GetOrCreate(character.Identity.Instance).ActionRestrictionFlags |= flags;
        }

        public static void MarkEquipmentVehicleMorph(ICharacter character)
        {
            Character ch = character as Character;
            if (ch == null)
            {
                return;
            }

            MorphState state = GetOrCreate(ch.Identity.Instance);
            state.FromEquipmentVehicle = true;
            if (!state.ScaleSaved)
            {
                state.PreviousMonsterScale = ch.Stats[StatIds.monsterscale].Value;
                state.ScaleSaved = true;
            }
        }

        public static bool HasEquipmentVehicleMorph(ICharacter character)
        {
            if (character == null)
            {
                return false;
            }

            lock (Gate)
            {
                MorphState state;
                return States.TryGetValue(character.Identity.Instance, out state)
                       && state != null
                       && state.FromEquipmentVehicle;
            }
        }

        /// <summary>
        /// Force-clear stuck hoverboard / vehicle / morph appearance that survives
        /// NCU cancel, unequip, and zone reboot (persisted monsterdata/isvehicle).
        /// Emits capture SpellList removes so client mesh clears (Buff alone is not enough).
        /// </summary>
        public static bool ForceClearStuckVehicleOrMorph(ICharacter character)
        {
            Character ch = character as Character;
            if (ch == null)
            {
                return false;
            }

            MorphState state;
            lock (Gate)
            {
                States.TryGetValue(ch.Identity.Instance, out state);
                States.Remove(ch.Identity.Instance);
            }

            // Prefer the active morph's SpellList remove; fall back to orphan clear (nanoId 0).
            int cancelNano = (state != null && state.ActiveNanoId != 0) ? state.ActiveNanoId : 0;
            SendCancelSpellListForNano(ch, cancelNano);

            bool changed = false;
            int missing = 1234567890;

            if (ch.Stats[StatIds.monsterdata].Value != 0)
            {
                int restore = (state != null && state.ShapeApplied)
                                  ? state.PreviousMonsterData
                                  : 0;
                ch.Stats[StatIds.monsterdata].Value = restore;
                changed = true;
            }

            if (ch.Stats[StatIds.catmesh].Value != 0 && ch.Stats[StatIds.catmesh].Value != missing)
            {
                int restore = (state != null && state.ShapeApplied)
                                  ? state.PreviousCatMesh
                                  : missing;
                ch.Stats[StatIds.catmesh].Value = restore;
                changed = true;
            }

            if (ch.Stats[StatIds.displaycatmesh].Value != 0
                && ch.Stats[StatIds.displaycatmesh].Value != missing)
            {
                int restore = (state != null && state.ShapeApplied)
                                  ? state.PreviousDisplayCatMesh
                                  : missing;
                ch.Stats[StatIds.displaycatmesh].Value = restore;
                changed = true;
            }

            if (ch.Stats[StatIds.isvehicle].Value != 0)
            {
                int restore = (state != null && state.FlightEnabled)
                                  ? state.PreviousIsVehicle
                                  : 0;
                ch.Stats[StatIds.isvehicle].Value = restore;
                changed = true;
            }

            if (state != null && state.ScaleSaved)
            {
                ch.Stats[StatIds.monsterscale].Value = state.PreviousMonsterScale;
                changed = true;
            }

            if (state != null && state.ScalingModifiers.Count > 0)
            {
                ReverseTrackedScalingModifiers(ch, state);
                changed = true;
            }

            if (!changed)
            {
                // SpellList remove still went out for client mesh clear.
                return true;
            }

            ch.ChangedAppearance = true;
            if (ch.Playfield != null)
            {
                ch.Playfield.AnnounceAppearanceUpdate(ch);
            }

            ZoneClient client = ch.Controller != null ? ch.Controller.Client as ZoneClient : null;
            if (client != null)
            {
                SimpleCharFullUpdate.SendToPlayfield(client);
            }

            try
            {
                ch.WriteStats();
            }
            catch
            {
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "[VehicleMorph] force-cleared character=" + ch.Identity.Instance);
            return true;
        }

        /// <summary>
        /// Capture 20260723-133842 unequip: reverse Hud1 vehicle OnWear morph/flight so
        /// ToWield MonsterData==0 can succeed on the next wear.
        /// </summary>
        public static void ClearEquipmentVehicleMorph(ICharacter character)
        {
            Character ch = character as Character;
            if (ch == null)
            {
                return;
            }

            MorphState state;
            lock (Gate)
            {
                if (!States.TryGetValue(ch.Identity.Instance, out state) || state == null
                    || !state.FromEquipmentVehicle)
                {
                    // Orphaned MonsterData after a prior vehicle wear without mark — clear
                    // only when no morph nano still owns the shape.
                    if (state == null || state.ActiveNanoId == 0)
                    {
                        ForceClearStuckVehicleOrMorph(ch);
                    }
                    else if (state != null)
                    {
                        state.FromEquipmentVehicle = false;
                    }

                    return;
                }

                if (state.ActiveNanoId != 0)
                {
                    // Nano morph owns the shape; only drop the equipment flag.
                    state.FromEquipmentVehicle = false;
                    return;
                }

                States.Remove(ch.Identity.Instance);
            }

            if (state.ShapeApplied)
            {
                ch.Stats[StatIds.monsterdata].Value = state.PreviousMonsterData;
                ch.Stats[StatIds.catmesh].Value = state.PreviousCatMesh;
                ch.Stats[StatIds.displaycatmesh].Value = state.PreviousDisplayCatMesh;
                ch.ChangedAppearance = true;
                if (ch.Playfield != null)
                {
                    ch.Playfield.AnnounceAppearanceUpdate(ch);
                }

                ZoneClient client = ch.Controller != null ? ch.Controller.Client as ZoneClient : null;
                if (client != null)
                {
                    SimpleCharFullUpdate.SendToPlayfield(client);
                }
            }

            if (state.FlightEnabled)
            {
                ch.Stats[StatIds.isvehicle].Value = state.PreviousIsVehicle;
            }

            if (state.ScaleSaved)
            {
                ch.Stats[StatIds.monsterscale].Value = state.PreviousMonsterScale;
            }

            state.ActionRestrictionFlags = 0;
            state.FromEquipmentVehicle = false;

            try
            {
                ch.WriteStats();
            }
            catch
            {
            }
        }

        public static bool IsFightingRestricted(ICharacter character)
        {
            if (character == null)
            {
                return false;
            }

            lock (Gate)
            {
                MorphState state;
                return States.TryGetValue(character.Identity.Instance, out state)
                       && state != null
                       && (state.ActionRestrictionFlags & FightingRestrictFlag) != 0;
            }
        }

        public static void OnMorphNanoApplied(ICharacter character, int nanoId)
        {
            Character ch = character as Character;
            if (ch == null || !IsMorphFlightNano(nanoId))
            {
                return;
            }

            GetOrCreate(ch.Identity.Instance).ActiveNanoId = nanoId;

            if (nanoId == SparrowFlightNanoId)
            {
                bool allowFly = ch.Stats[StatIds.expansionplayfield].Value == 0;
                SendSpellListWire(ch, SparrowApplyWire, CapturedCharacterInstance, allowFly);
            }
            else if (nanoId == PhasefrontPhantomCamoNanoId)
            {
                // Capture 20260806-085523: SpellList "Phasefront Phantom - Camo".
                SendSpellListWire(ch, PhasefrontCamoApplyWire, CapturedPhasefrontCharacterInstance, true);
            }
            else if (nanoId == PhasefrontExecutiveNanoId)
            {
                // Capture 20260806-085523: SpellList "Phasefront Loungemaster - The Executive".
                SendSpellListWire(ch, PhasefrontExecutiveApplyWire, CapturedPhasefrontCharacterInstance, true);
            }

            // Hoverboard 281569: live has no SpellList — OnUse MonsterShape/CanFly only.
        }

        /// <summary>
        /// Capture 20260806-085523: NCU cancel sends Buff (and SpellList for Phasefront).
        /// Reverse server morph for the cancelled nano only — do not ForceClear afterward
        /// (that used to blast unrelated Phasefront SpellList removes and stuck Adventurer
        /// metamorph exit / Sparrow demorph).
        /// </summary>
        public static void CancelVehicleMorphNano(ICharacter character, int nanoId)
        {
            OnMorphNanoRemoved(character, nanoId);
        }

        private static void SendCancelSpellListForNano(Character ch, int nanoId)
        {
            if (ch == null)
            {
                return;
            }

            if (nanoId == SparrowFlightNanoId)
            {
                SendSpellListWire(ch, SparrowRemoveWire, CapturedCharacterInstance, true);
            }
            else if (nanoId == PhasefrontPhantomCamoNanoId)
            {
                SendSpellListWire(ch, PhasefrontCamoRemoveWire, CapturedPhasefrontCharacterInstance, true);
            }
            else if (nanoId == PhasefrontExecutiveNanoId)
            {
                SendSpellListWire(ch, PhasefrontExecutiveRemoveWire, CapturedPhasefrontCharacterInstance, true);
            }
            else if (nanoId == HoverboardNanoId)
            {
                // Capture 20260806-085523: hoverboard cancel is Buff only — no SpellList.
            }
            else if (nanoId == 0)
            {
                // /dismount / orphan force-clear only: reverse every capture SpellList morph
                // that can stick without ActiveNanos. Never use this path on normal NCU cancel.
                SendSpellListWire(ch, SparrowRemoveWire, CapturedCharacterInstance, true);
                SendSpellListWire(ch, PhasefrontCamoRemoveWire, CapturedPhasefrontCharacterInstance, true);
                SendSpellListWire(ch, PhasefrontExecutiveRemoveWire, CapturedPhasefrontCharacterInstance, true);
            }
        }

        public static void OnMorphNanoRemoved(ICharacter character, int nanoId)
        {
            Character ch = character as Character;
            if (ch == null)
            {
                return;
            }

            MorphState state;
            lock (Gate)
            {
                if (!States.TryGetValue(ch.Identity.Instance, out state) || state == null)
                {
                    SendCancelSpellListForNano(ch, nanoId);
                    ClearAppearanceStats(ch);
                    return;
                }

                if (state.ActiveNanoId != 0 && state.ActiveNanoId != nanoId && !IsVehicleMorphNano(nanoId))
                {
                    return;
                }

                States.Remove(ch.Identity.Instance);
            }

            ReverseTrackedScalingModifiers(ch, state);

            if (state.ShapeApplied)
            {
                ch.Stats[StatIds.monsterdata].Value = state.PreviousMonsterData;
                ch.Stats[StatIds.catmesh].Value = state.PreviousCatMesh;
                ch.Stats[StatIds.displaycatmesh].Value = state.PreviousDisplayCatMesh;
            }
            else
            {
                ClearAppearanceStats(ch);
            }

            if (state.FlightEnabled || ch.Stats[StatIds.isvehicle].Value != 0)
            {
                ch.Stats[StatIds.isvehicle].Value = state.FlightEnabled ? state.PreviousIsVehicle : 0;
            }

            if (state.ScaleSaved)
            {
                ch.Stats[StatIds.monsterscale].Value = state.PreviousMonsterScale;
            }

            ch.ChangedAppearance = true;
            if (ch.Playfield != null)
            {
                ch.Playfield.AnnounceAppearanceUpdate(ch);
            }

            ZoneClient client = ch.Controller != null ? ch.Controller.Client as ZoneClient : null;
            if (client != null)
            {
                SimpleCharFullUpdate.SendToPlayfield(client);
            }

            SendCancelSpellListForNano(ch, nanoId != 0 ? nanoId : state.ActiveNanoId);

            try
            {
                ch.WriteStats();
            }
            catch
            {
            }
        }

        private static void ClearAppearanceStats(Character ch)
        {
            if (ch == null)
            {
                return;
            }

            int missing = 1234567890;
            ch.Stats[StatIds.monsterdata].Value = 0;
            ch.Stats[StatIds.isvehicle].Value = 0;
            if (ch.Stats[StatIds.catmesh].Value != 0 && ch.Stats[StatIds.catmesh].Value != missing)
            {
                ch.Stats[StatIds.catmesh].Value = missing;
            }

            if (ch.Stats[StatIds.displaycatmesh].Value != 0
                && ch.Stats[StatIds.displaycatmesh].Value != missing)
            {
                ch.Stats[StatIds.displaycatmesh].Value = missing;
            }

            // Push cleared appearance stats on the wire (SCFU alone left client morph stuck).
            try
            {
                ZoneEngine.Core.MessageHandlers.StatMessageHandler.Default.SendSingle(
                    ch,
                    (int)StatIds.monsterdata,
                    0);
                ZoneEngine.Core.MessageHandlers.StatMessageHandler.Default.SendSingle(
                    ch,
                    (int)StatIds.isvehicle,
                    0);
            }
            catch
            {
            }

            ch.ChangedAppearance = true;
            if (ch.Playfield != null)
            {
                ch.Playfield.AnnounceAppearanceUpdate(ch);
            }

            ZoneClient client = ch.Controller != null ? ch.Controller.Client as ZoneClient : null;
            if (client != null)
            {
                SimpleCharFullUpdate.SendToPlayfield(client);
            }

            try
            {
                ch.WriteStats();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Login heal: if MonsterData/IsVehicle are set but no HUD vehicle and no morph nano,
        /// clear the stuck hoverboard/yalm morph that survives zone/relog.
        /// Capture 20260806-085523 backup path — also skip when a morph buff is still
        /// persisted (zone stash/DB) because PrepareCharacterForLogin cleared memory NCU
        /// before restore runs.
        /// </summary>
        public static void HealOrphanedVehicleMorphOnLogin(ICharacter character)
        {
            Character ch = character as Character;
            if (ch == null)
            {
                return;
            }

            // Run even when not morphed — stuck -240 runspeed is independent of vehicle mesh.
            HealOrphanedRunspeedModifierOnLogin(ch);

            if (!LooksMorphed(ch))
            {
                return;
            }

            if (VehicleHudWearRuntime.HasEquippedVehicle(ch))
            {
                MarkEquipmentVehicleMorph(ch);
                return;
            }

            if (HasActiveMorphNano(ch))
            {
                return;
            }

            // Zone/login: ActiveNanos are cleared before restore — do not wipe a live morph.
            if (ActiveNanoRuntimeService.Default.HasZoneTransferStash(ch.Identity.Instance)
                || HasPersistedMorphFlightNano(ch.Identity.Instance))
            {
                return;
            }

            ForceClearStuckVehicleOrMorph(ch);
        }

        /// <summary>
        /// After ActiveNano restore: rebind MorphState / re-apply OnUse if HealOrphaned ran
        /// before restore, or MorphState was lost on zone.
        /// </summary>
        public static void EnsureMorphStateMatchesActiveNanos(ICharacter character)
        {
            Character ch = character as Character;
            if (ch == null || !HasActiveMorphNano(ch))
            {
                return;
            }

            foreach (KeyValuePair<int, AORebirth.Interfaces.IActiveNano> pair in ch.ActiveNanos)
            {
                if (pair.Value == null || !IsMorphFlightNano(pair.Value.ID))
                {
                    continue;
                }

                int nanoId = pair.Value.ID;
                GetOrCreate(ch.Identity.Instance).ActiveNanoId = nanoId;

                if (!LooksMorphed(ch))
                {
                    NanoFormula nano;
                    if (NanoLoader.NanoList.TryGetValue(nanoId, out nano) && nano != null)
                    {
                        NanoEventRuntimeService.Default.ExecuteOnUseEvents(ch, nano);
                    }
                }

                OnMorphNanoApplied(ch, nanoId);
            }
        }

        private static bool LooksMorphed(Character ch)
        {
            if (ch == null)
            {
                return false;
            }

            int missing = 1234567890;
            return ch.Stats[StatIds.monsterdata].Value != 0
                   || ch.Stats[StatIds.isvehicle].Value != 0
                   || (ch.Stats[StatIds.catmesh].Value != 0
                       && ch.Stats[StatIds.catmesh].Value != missing)
                   || (ch.Stats[StatIds.displaycatmesh].Value != 0
                       && ch.Stats[StatIds.displaycatmesh].Value != missing);
        }

        private static bool HasPersistedMorphFlightNano(int characterId)
        {
            try
            {
                var persisted = CharacterActiveNanosDao.Instance.ReadActiveNanos(characterId);
                if (persisted == null)
                {
                    return false;
                }

                foreach (var row in persisted)
                {
                    if (row != null && IsMorphFlightNano(row.NanoId))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool HasActiveMorphNano(Character character)
        {
            if (character == null || character.ActiveNanos == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, AORebirth.Interfaces.IActiveNano> pair in character.ActiveNanos)
            {
                if (pair.Value != null && IsMorphFlightNano(pair.Value.ID))
                {
                    return true;
                }
            }

            return false;
        }

        private static MorphState GetOrCreate(int characterInstance)
        {
            lock (Gate)
            {
                MorphState state;
                if (!States.TryGetValue(characterInstance, out state) || state == null)
                {
                    state = new MorphState();
                    States[characterInstance] = state;
                }

                return state;
            }
        }

        private static void SendSpellListWire(
            Character character,
            byte[] template,
            int capturedCharacterInstance,
            bool includeCanFly)
        {
            ZoneClient client = character != null && character.Controller != null
                                    ? character.Controller.Client as ZoneClient
                                    : null;
            if (client == null || template == null)
            {
                return;
            }

            try
            {
                byte[] packet = (byte[])template.Clone();
                if (!includeCanFly)
                {
                    packet = StripCanFlyEffect(packet);
                }

                ReplaceInstance(packet, capturedCharacterInstance, character.Identity.Instance);
                // Length field at bytes 6-7 (big-endian ushort total length).
                ushort totalLength = (ushort)packet.Length;
                packet[6] = (byte)(totalLength >> 8);
                packet[7] = (byte)totalLength;
                client.EnqueueOutboundCompressedBuffer(packet);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AdventurerMorphFlight SpellList failed: " + ex.Message);
            }
        }

        private static byte[] StripCanFlyEffect(byte[] packet)
        {
            string hex = BitConverter.ToString(packet).Replace("-", string.Empty);
            string canFly = BitConverter.ToString(CanFlyEffectBlock).Replace("-", string.Empty);
            int idx = hex.IndexOf(canFly, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return packet;
            }

            string stripped = hex.Remove(idx, canFly.Length);
            byte[] result = Hex(stripped);
            // N3 count dword sits at body offset: after zone hdr(16)+type(4)+identity(8)+unk(1)=29
            // effects 8→7: count=(7+1)*0x3F1=0x1F88
            int countOffset = 29;
            if (result.Length > countOffset + 3)
            {
                int count = (7 + 1) * 0x3F1;
                result[countOffset] = (byte)((count >> 24) & 0xFF);
                result[countOffset + 1] = (byte)((count >> 16) & 0xFF);
                result[countOffset + 2] = (byte)((count >> 8) & 0xFF);
                result[countOffset + 3] = (byte)(count & 0xFF);
            }

            return result;
        }

        private static void ReplaceInstance(byte[] packet, int from, int to)
        {
            byte b0 = (byte)(from >> 24);
            byte b1 = (byte)(from >> 16);
            byte b2 = (byte)(from >> 8);
            byte b3 = (byte)from;
            for (int i = 0; i + 4 <= packet.Length; i++)
            {
                if (packet[i] == b0 && packet[i + 1] == b1 && packet[i + 2] == b2 && packet[i + 3] == b3)
                {
                    packet[i] = (byte)(to >> 24);
                    packet[i + 1] = (byte)(to >> 16);
                    packet[i + 2] = (byte)(to >> 8);
                    packet[i + 3] = (byte)to;
                    i += 3;
                }
            }
        }

        private static byte[] Hex(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        private sealed class MorphState
        {
            public int ActiveNanoId;

            public bool ShapeApplied;

            public int PreviousMonsterData;

            public int PreviousCatMesh;

            public int PreviousDisplayCatMesh;

            public bool FlightEnabled;

            public int PreviousIsVehicle;

            public int ActionRestrictionFlags;

            public bool FromEquipmentVehicle;

            public bool ScaleSaved;

            public int PreviousMonsterScale;

            public readonly Dictionary<int, int> ScalingModifiers = new Dictionary<int, int>();
        }
    }
}
