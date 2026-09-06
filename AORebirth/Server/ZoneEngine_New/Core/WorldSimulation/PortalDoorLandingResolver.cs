namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    using AODB.Common.RDBObjects;

    using AORebirth.Enums;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Resolves portal landings from Dynels.dat teleport events + Doors.dat.
    /// </summary>
    public static class PortalDoorLandingResolver
    {
        static readonly HashSet<int> ZoningFunctionIds =
        [
            (int)FunctionType.Teleport,
            (int)FunctionType.TeleportProxy,
            (int)FunctionType.TeleportProxy2,
            (int)FunctionType.LineTeleport,
            (int)FunctionType.ExitProxyPlayfield
        ];

        public static bool TryResolve(
            PlayfieldDynel dynel,
            PlayfieldDoors? doors,
            PlayfieldDoors? destDoors,
            out int destPlayfieldId,
            out Vector3 landing)
        {
            destPlayfieldId = 0;
            landing = default;

            if (!TryReadTeleportTarget(dynel, out destPlayfieldId, out int doorInstance)
                || destPlayfieldId <= 0)
                return false;

            PlayfieldDoor? door = FindDoor(doors, doorInstance)
                ?? FindDoor(destDoors, doorInstance);
            if (door != null)
            {
                landing = new Vector3(door.X, door.Y, door.Z);
                if (door.PlayfieldId > 0)
                    destPlayfieldId = door.PlayfieldId;
                return true;
            }

            landing = new Vector3(dynel.Position.X, dynel.Position.Y, dynel.Position.Z);
            return true;
        }

        public static bool IsZoningCapable(PlayfieldDynel dynel)
        {
            if (dynel == null)
                return false;

            object? modifiers = dynel.GetType().GetProperty("Modifiers")?.GetValue(dynel);
            if (modifiers is not IEnumerable enumerable)
                return false;

            foreach (object? entry in enumerable)
            {
                if (entry == null)
                    continue;

                object? modEntry = entry is DictionaryEntry de
                    ? de.Value
                    : entry.GetType().GetProperty("Value")?.GetValue(entry) ?? entry;

                if (modEntry == null)
                    continue;

                object? fnMap = modEntry.GetType().GetProperty("Modifiers")?.GetValue(modEntry);
                if (fnMap is not IEnumerable fnEnumerable)
                    continue;

                foreach (object? fnEntry in fnEnumerable)
                {
                    object? key = fnEntry is DictionaryEntry d2
                        ? d2.Key
                        : fnEntry.GetType().GetProperty("Key")?.GetValue(fnEntry);
                    int id = ToInt(key);
                    if (ZoningFunctionIds.Contains(id))
                        return true;
                }
            }

            return false;
        }

        static bool TryReadTeleportTarget(PlayfieldDynel dynel, out int playfieldId, out int doorInstance)
        {
            playfieldId = 0;
            doorInstance = 0;
            object? modifiers = dynel.GetType().GetProperty("Modifiers")?.GetValue(dynel);
            if (modifiers is not IEnumerable enumerable)
                return false;

            foreach (object? entry in enumerable)
            {
                if (entry == null)
                    continue;

                object? modEntry = entry is DictionaryEntry de
                    ? de.Value
                    : entry.GetType().GetProperty("Value")?.GetValue(entry) ?? entry;
                object? fnMap = modEntry?.GetType().GetProperty("Modifiers")?.GetValue(modEntry);
                if (fnMap is not IEnumerable fnEnumerable)
                    continue;

                foreach (object? fnEntry in fnEnumerable)
                {
                    object? keyObj;
                    object? valueObj;
                    if (fnEntry is DictionaryEntry d2)
                    {
                        keyObj = d2.Key;
                        valueObj = d2.Value;
                    }
                    else
                    {
                        Type t = fnEntry!.GetType();
                        keyObj = t.GetProperty("Key")?.GetValue(fnEntry);
                        valueObj = t.GetProperty("Value")?.GetValue(fnEntry);
                    }

                    if (!ZoningFunctionIds.Contains(ToInt(keyObj)))
                        continue;

                    if (valueObj is not IEnumerable argSets)
                        continue;

                    foreach (object? argSet in argSets)
                    {
                        if (argSet is not IEnumerable args)
                            continue;

                        foreach (object? arg in args)
                        {
                            object? argKey;
                            object? argVal;
                            if (arg is DictionaryEntry ad)
                            {
                                argKey = ad.Key;
                                argVal = ad.Value;
                            }
                            else if (arg != null)
                            {
                                Type at = arg.GetType();
                                argKey = at.GetProperty("Key")?.GetValue(arg);
                                argVal = at.GetProperty("Value")?.GetValue(arg);
                            }
                            else
                                continue;

                            int val = ToInt(argVal);
                            // Common AO function arg slots: destination playfield / door instance appear as ints.
                            if (playfieldId <= 0 && val > 100 && val < 100000)
                                playfieldId = val;
                            else if (doorInstance == 0 && val != 0 && val != playfieldId)
                                doorInstance = val;
                        }

                        if (playfieldId > 0)
                            return true;
                    }
                }
            }

            return playfieldId > 0;
        }

        static PlayfieldDoor? FindDoor(PlayfieldDoors? doors, int instance)
        {
            if (doors?.Doors == null || instance == 0)
                return null;

            for (int i = 0; i < doors.Doors.Count; i++)
            {
                PlayfieldDoor d = doors.Doors[i];
                if (d.Id == instance || d.Index == instance || d.Index2 == instance)
                    return d;
            }

            return null;
        }

        static int ToInt(object? raw)
        {
            return raw switch
            {
                null => 0,
                int i => i,
                short s => s,
                long l => (int)l,
                byte b => b,
                Enum e => Convert.ToInt32(e),
                float f => (int)f,
                _ => int.TryParse(raw.ToString(), out int v) ? v : 0
            };
        }
    }
}
