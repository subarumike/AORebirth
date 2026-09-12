namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using AODB.Common.Enums;
    using AODB.Common.RDBObjects;
    using AORebirth.Database.Dao;
    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>The server's DAO routing overrides, applied before portal and reverse-exit baking.</summary>
    public sealed class TeleportDestinationCatalog
    {
        readonly Dictionary<(int Playfield, int Type, uint Instance), (int Playfield, int Type, uint Instance)> _routes = new();
        readonly Action<string>? _warn;

        public TeleportDestinationCatalog(IEnumerable<TeleportRoute> rows, Action<string>? warn = null)
        {
            ArgumentNullException.ThrowIfNull(rows);
            _warn = warn;
            foreach (TeleportRoute row in rows)
            {
                var key = (row.Playfield, row.StatelType, row.StatelInstance);
                var target = (row.DestinationPlayfield, row.DestinationType, row.DestinationInstance);
                if (_routes.TryGetValue(key, out var existing) && existing != target)
                    throw new InvalidDataException($"Conflicting teleport routes for {key}.");
                _routes[key] = target;
            }
        }

        public int Count => _routes.Count;

        public void Apply(int sourcePlayfieldId, PlayfieldDynels? dynels)
        {
            if (dynels?.Dynels == null) return;
            foreach (PlayfieldDynel dynel in dynels.Dynels)
            {
                if (!_routes.TryGetValue((sourcePlayfieldId, dynel.IdentityType, unchecked((uint)dynel.IdentityInstance)), out var target)
                    || dynel.Modifiers == null) continue;
                foreach (Modifier modifier in dynel.Modifiers.Values)
                {
                    if (modifier?.Modifiers == null) continue;
                    foreach (FunctionType function in new[] { FunctionType.TeleportProxy, FunctionType.TeleportProxy2 })
                    {
                        if (!modifier.Modifiers.TryGetValue(function, out var sets)) continue;
                        foreach (var arguments in sets)
                        {
                            if (!arguments.TryGetValue(FunctionOperator.Arg1, out object? raw)
                                || raw is not IList values || values.Count < 3
                                || Convert.ToInt32(values[0]) != (int)IdentityType.PlayfieldDoor) continue;
                            if (target.Type != (int)IdentityType.Door || target.Playfield <= 0 || target.Playfield > 0xFFFF
                                || (target.Instance & 0xFF000000u) != 0xC0000000u
                                || (target.Instance & 0xFFFFu) != (uint)target.Playfield)
                            {
                                // Zero destinations disable a route. Other unsupported targets must
                                // also fail locally, without reverting to the wrong raw RDB door.
                                values[1] = 0;
                                values[2] = 0;
                                if (target.Playfield != 0 || target.Type != 0 || target.Instance != 0)
                                    _warn?.Invoke($"Teleport DAO route disabled: unsupported target sourcePf={sourcePlayfieldId} door={dynel.IdentityInstance:X8} destinationPf={target.Playfield} destinationType={target.Type} destination={target.Instance:X8}");
                                continue;
                            }
                            // Preserve the original event, proxy kind, clearance and return behavior.
                            values[1] = target.Playfield;
                            values[2] = (int)((target.Instance >> 16) & 0xFFu);
                        }
                    }
                }
            }
        }
    }
}
