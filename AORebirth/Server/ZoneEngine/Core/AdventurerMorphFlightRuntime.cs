namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Nanos;
    using AORebirth.Enums;

    using Utility;

    using ZoneEngine.Core.Packets;

    /// <summary>
    /// Adventurer Rubi-Ka flight morphs (Sparrow Flight nano 82835).
    /// Capture 20260723-053632: CastNano 82835 → SpellList "Sparrow Flight" with
    /// ScalingModify runspeed/evades, MonsterShape 30365, RestrictAction 2, CanFly
    /// (only when expansionplayfield==0). Shadowlands: morph + runspeed, no flight.
    /// </summary>
    public static class AdventurerMorphFlightRuntime
    {
        public const int SparrowFlightNanoId = 82835;

        private const int CapturedCharacterInstance = 0x00139459;

        private const int FightingRestrictFlag = 2;

        // Full capture wires (zone header + N3 SpellList body), identities patched at send.
        private static readonly byte[] SparrowApplyWire = Hex(
            "00E1000A000101A800000DB9001394594D4501140000C3500013945900000023790000CFB7000143930000000400000000000000010000000000000001000000090000009C000004B00000CFB7000143930000000400000000000000010000000000000001000000090000009A000000B60000CFB70001439300000004000000000000000100000000000000010000000900000099000000B60000CFB7000143930000000400000000000000010000000000000001000000090000009B000000B60000CF44000143930000000400000000000000010000000000000001000000090000769D000000000000CF4C0001439300000004000000000000000100000000000000010000000900000002000000000000CF92000143930000000400000003000002130000000000000000000000000000000000000070000000000000000000000004000000010000000000000001000000090000CF3B0001439300000004000000000000000100000000000000020000000900042B8C0000C350001394590000C3500013945900000E53706172726F7720466C69676874010000CF1B000143930000000000");

        private static readonly byte[] SparrowRemoveWire = Hex(
            "010C000A000101A800000DB9001394594D4501140000C3500013945900000023790000CFB7000143930000000400000000000000010000000000000001000000090000009C000004B00000CFB7000143930000000400000000000000010000000000000001000000090000009A000000B60000CFB70001439300000004000000000000000100000000000000010000000900000099000000B60000CFB7000143930000000400000000000000010000000000000001000000090000009B000000B60000CF44000143930000000400000000000000010000000000000001000000090000769D000000000000CF4C0001439300000004000000000000000100000000000000010000000900000002000000000000CF92000143930000000400000003000002130000000000000000000000000000000000000070000000000000000000000004000000010000000000000001000000090000CF3B0001439300000004000000000000000100000000000000020000000900042B8C0000C350001394590000C3500013945901000E53706172726F7720466C69676874010000CF1B000143930000000000");

        private static readonly byte[] CanFlyEffectBlock = Hex(
            "0000CF9200014393000000040000000300000213000000000000000000000000000000000000007000000000000000000000000400000001000000000000000100000009");

        private static readonly object Gate = new object();

        private static readonly Dictionary<int, MorphState> States = new Dictionary<int, MorphState>();

        public static bool IsMorphFlightNano(int nanoId)
        {
            if (nanoId == SparrowFlightNanoId)
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
                    if (fn.FunctionType == (int)FunctionType.MonsterShape)
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
                    // Orphaned MonsterData after a prior vehicle wear without mark — still clear
                    // vehicle flight bit when no morph nano is active.
                    if (ch.Stats[StatIds.isvehicle].Value != 0
                        && (state == null || state.ActiveNanoId == 0))
                    {
                        ch.Stats[StatIds.isvehicle].Value = 0;
                    }

                    if (ch.Stats[StatIds.monsterdata].Value != 0
                        && (state == null || state.ActiveNanoId == 0))
                    {
                        ch.Stats[StatIds.monsterdata].Value = 0;
                        ch.Stats[StatIds.catmesh].Value = 1234567890;
                        ch.Stats[StatIds.displaycatmesh].Value = 1234567890;
                        ch.ChangedAppearance = true;
                        if (ch.Playfield != null)
                        {
                            ch.Playfield.AnnounceAppearanceUpdate(ch);
                        }
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
            // Capture-backed SpellList wire is Sparrow Flight only (82835).
            if (ch == null || nanoId != SparrowFlightNanoId)
            {
                return;
            }

            bool allowFly = ch.Stats[StatIds.expansionplayfield].Value == 0;
            SendSpellListWire(ch, SparrowApplyWire, allowFly);
            GetOrCreate(ch.Identity.Instance).ActiveNanoId = nanoId;
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
                    return;
                }

                if (state.ActiveNanoId != 0 && state.ActiveNanoId != nanoId)
                {
                    return;
                }

                States.Remove(ch.Identity.Instance);
            }

            foreach (KeyValuePair<int, int> pair in state.ScalingModifiers)
            {
                ch.Stats[pair.Key].Modifier -= pair.Value;
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

            if (nanoId == SparrowFlightNanoId || state.ActiveNanoId == SparrowFlightNanoId)
            {
                SendSpellListWire(ch, SparrowRemoveWire, true);
            }
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

        private static void SendSpellListWire(Character character, byte[] template, bool includeCanFly)
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

                ReplaceInstance(packet, CapturedCharacterInstance, character.Identity.Instance);
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
