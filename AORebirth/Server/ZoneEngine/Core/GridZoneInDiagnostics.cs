#region License

// Copyright (c) 2005-2014, CellAO Team
//
//
// All rights reserved.
//

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Statels;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Playfields;

    using Quaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using Vector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    #endregion

    public static class GridZoneInDiagnostics
    {
        private const int GridPlayfieldId = 152;

        private const int SuspiciousValue = 0x12;

        private const int GridExitTerminalTemplateId = 95351;

        private static readonly TimeSpan PendingContextLifetime = TimeSpan.FromSeconds(45);

        private static readonly TimeSpan ActiveZoneInLifetime = TimeSpan.FromSeconds(15);

        private static readonly object SyncRoot = new object();

        private static readonly Dictionary<int, GridZoneInContext> PendingContexts =
            new Dictionary<int, GridZoneInContext>();

        private static readonly Dictionary<int, GridZoneInContext> ActiveContexts =
            new Dictionary<int, GridZoneInContext>();

        public static void RecordGridEntry(
            ICharacter character,
            StatelData sourceTerminal,
            StatelData destinationTerminal,
            Coordinate landing,
            string routeKind,
            string evidence,
            int rawDestinationInstance)
        {
            if (character == null || sourceTerminal == null || landing == null)
            {
                return;
            }

            var context = new GridZoneInContext
                          {
                              CharacterId = character.Identity.Instance,
                              SourcePlayfieldId = sourceTerminal.PlayfieldId,
                              SourceTerminalTypeId = (int)sourceTerminal.Identity.Type,
                              SourceTerminalInstance = sourceTerminal.Identity.Instance,
                              SourceTerminalTemplateId = sourceTerminal.TemplateId,
                              DestinationPlayfieldId = GridPlayfieldId,
                              DestinationStatelTypeId = destinationTerminal == null
                                                            ? 0
                                                            : (int)destinationTerminal.Identity.Type,
                              DestinationStatelInstance = destinationTerminal == null
                                                             ? 0
                                                             : destinationTerminal.Identity.Instance,
                              DestinationStatelTemplateId = destinationTerminal == null
                                                              ? 0
                                                              : destinationTerminal.TemplateId,
                              RawDestinationInstance = rawDestinationInstance,
                              LandingX = landing.x,
                              LandingY = landing.y,
                              LandingZ = landing.z,
                              RouteKind = routeKind ?? string.Empty,
                              Evidence = evidence ?? string.Empty,
                              PendingExpiresAt = DateTime.UtcNow + PendingContextLifetime
                          };

            lock (SyncRoot)
            {
                PendingContexts[context.CharacterId] = context;
            }

            LogGridRoute(context);
            LogGridExitComparison(context);
            WarnIfSuspiciousRouteValues(context);
        }

        public static void BeginGridZoneIn(ZoneClient client)
        {
            if (client == null || client.Controller == null || client.Controller.Character == null)
            {
                return;
            }

            ICharacter character = client.Controller.Character;
            if (character.Playfield == null || character.Playfield.Identity.Instance != GridPlayfieldId)
            {
                return;
            }

            GridZoneInContext context;
            lock (SyncRoot)
            {
                CleanupExpiredContexts();
                if (!PendingContexts.TryGetValue(character.Identity.Instance, out context))
                {
                    Coordinate current = character.Coordinates();
                    context = new GridZoneInContext
                              {
                                  CharacterId = character.Identity.Instance,
                                  DestinationPlayfieldId = GridPlayfieldId,
                                  LandingX = current.x,
                                  LandingY = current.y,
                                  LandingZ = current.z,
                                  RouteKind = "GridZoneInWithoutRecordedSource",
                                  Evidence = "No in-process grid terminal context was recorded before this zone login."
                              };
                }
                else
                {
                    PendingContexts.Remove(character.Identity.Instance);
                }

                context.ActiveExpiresAt = DateTime.UtcNow + ActiveZoneInLifetime;
                ActiveContexts[character.Identity.Instance] = context;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GRID_ZONE_IN_BEGIN char={0} playfield={1} sourceTerminal={2} sourceTemplate={3} destStatel={4} destTemplate={5} landing=({6:F3},{7:F3},{8:F3}) route={9} evidence={10}",
                    character.Identity.ToString(true),
                    GridPlayfieldId,
                    IdentityPart(context.SourceTerminalTypeId, context.SourceTerminalInstance),
                    context.SourceTerminalTemplateId,
                    IdentityPart(context.DestinationStatelTypeId, context.DestinationStatelInstance),
                    context.DestinationStatelTemplateId,
                    context.LandingX,
                    context.LandingY,
                    context.LandingZ,
                    context.RouteKind,
                    context.Evidence));
        }

        public static void LogOutboundMessage(ZoneClient client, MessageBody messageBody)
        {
            if (client == null || messageBody == null || client.Controller == null
                || client.Controller.Character == null)
            {
                return;
            }

            GridZoneInContext context;
            lock (SyncRoot)
            {
                CleanupExpiredContexts();
                if (!ActiveContexts.TryGetValue(client.Controller.Character.Identity.Instance, out context))
                {
                    return;
                }
            }

            N3Message n3Message = messageBody as N3Message;
            if (n3Message == null)
            {
                return;
            }

            var details = BuildObjectDetails(client, n3Message);
            if (!details.HasIdentity)
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GRID_ZONE_IN_OBJECT message={0} playfield={1} sourceTerminal={2} sourceTemplate={3} destStatel={4} destTemplate={5} object={6} objectTypeId={7} objectTypeName={8} coords={9} heading={10} modelResourceMesh={11}",
                    n3Message.GetType().Name,
                    details.PlayfieldId,
                    IdentityPart(context.SourceTerminalTypeId, context.SourceTerminalInstance),
                    context.SourceTerminalTemplateId,
                    IdentityPart(context.DestinationStatelTypeId, context.DestinationStatelInstance),
                    context.DestinationStatelTemplateId,
                    IdentityPart(details.ObjectTypeId, details.ObjectInstance),
                    details.ObjectTypeId,
                    details.ObjectTypeName,
                    details.Coordinates,
                    details.Heading,
                    details.ModelResourceMesh));

            WarnIfSuspiciousObjectValues(context, details, n3Message.GetType().Name);
            WarnIfVehicle(details, n3Message.GetType().Name);
        }

        private static ObjectDetails BuildObjectDetails(ZoneClient client, N3Message message)
        {
            var details = new ObjectDetails
                          {
                              ObjectTypeId = (int)message.Identity.Type,
                              ObjectInstance = message.Identity.Instance,
                              ObjectTypeName = ResolveIdentityTypeName((int)message.Identity.Type),
                              HasIdentity = message.Identity.Type != IdentityType.None || message.Identity.Instance != 0,
                              PlayfieldId = GridPlayfieldId,
                              Coordinates = "n/a",
                              Heading = "n/a",
                              ModelResourceMesh = "n/a"
                          };

            if (message is PlayfieldAnarchyFMessage)
            {
                var playfield = (PlayfieldAnarchyFMessage)message;
                details.PlayfieldId = playfield.PlayfieldId2.Instance;
                details.Coordinates = FormatVector(playfield.CharacterCoordinates);
                details.ModelResourceMesh = string.Format(
                    CultureInfo.InvariantCulture,
                    "playfieldX={0};playfieldZ={1}",
                    playfield.PlayfieldX,
                    playfield.PlayfieldZ);
                return details;
            }

            if (message is VendingMachineFullUpdateMessage)
            {
                var vending = (VendingMachineFullUpdateMessage)message;
                details.PlayfieldId = vending.PlayfieldId;
                details.Coordinates = FormatVector(vending.Coordinates);
                details.Heading = FormatQuaternion(vending.Heading);
                details.ModelResourceMesh = BuildStatMeshSummary(vending.Stats, vending.TypeIdentifier);
                return details;
            }

            if (message is SimpleCharFullUpdateMessage)
            {
                var scfu = (SimpleCharFullUpdateMessage)message;
                details.PlayfieldId = scfu.PlayfieldId.GetValueOrDefault(GridPlayfieldId);
                details.Coordinates = FormatVector(scfu.Coordinates);
                details.Heading = FormatQuaternion(scfu.Heading);
                details.ModelResourceMesh = BuildSimpleCharMeshSummary(scfu);
                return details;
            }

            if (message is WeaponItemFullUpdateMessage)
            {
                var weapon = (WeaponItemFullUpdateMessage)message;
                details.PlayfieldId = weapon.PlayfieldId;
                details.ModelResourceMesh = BuildStatMeshSummary(weapon.Stats, 0);
                return details;
            }

            if (message is FullCharacterMessage)
            {
                var full = (FullCharacterMessage)message;
                Coordinate coordinates = client.Controller.Character.Coordinates();
                details.PlayfieldId = client.Controller.Character.Playfield.Identity.Instance;
                details.Coordinates = FormatCoordinate(coordinates);
                details.Heading = FormatCoreQuaternion(client.Controller.Character.Heading);
                details.ModelResourceMesh = BuildFullCharacterMeshSummary(full);
                return details;
            }

            if (client.Controller.Character.Identity == message.Identity)
            {
                Coordinate coordinates = client.Controller.Character.Coordinates();
                details.PlayfieldId = client.Controller.Character.Playfield.Identity.Instance;
                details.Coordinates = FormatCoordinate(coordinates);
                details.Heading = FormatCoreQuaternion(client.Controller.Character.Heading);
            }

            return details;
        }

        private static string BuildSimpleCharMeshSummary(SimpleCharFullUpdateMessage message)
        {
            var parts = new List<string>
                        {
                            "monsterData=" + message.MonsterData.ToString(CultureInfo.InvariantCulture),
                            "monsterScale=" + message.MonsterScale.ToString(CultureInfo.InvariantCulture)
                        };

            if (message.HeadMesh.HasValue)
            {
                parts.Add("headMesh=" + message.HeadMesh.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (message.Meshes != null && message.Meshes.Length > 0)
            {
                parts.Add("meshes=" + string.Join(
                    "|",
                    message.Meshes.Select(
                        x => string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}:{1}:{2}:{3}",
                            x.Position,
                            x.Id,
                            x.OverrideTextureId,
                            x.Layer)).ToArray()));
            }

            return string.Join(";", parts.ToArray());
        }

        private static string BuildStatMeshSummary(GameTuple<CharacterStat, uint>[] stats, int typeIdentifier)
        {
            var parts = new List<string>();
            if (typeIdentifier != 0)
            {
                parts.Add("typeIdentifier=" + typeIdentifier.ToString(CultureInfo.InvariantCulture));
            }

            if (stats == null)
            {
                return parts.Count == 0 ? "n/a" : string.Join(";", parts.ToArray());
            }

            AddStatValue(parts, stats, (int)StatIds.mesh, "mesh");
            AddStatValue(parts, stats, (int)StatIds.headmesh, "headMesh");
            AddStatValue(parts, stats, (int)StatIds.monsterdata, "monsterData");
            AddStatValue(parts, stats, (int)StatIds.acgitemtemplateid, "templateId");

            return parts.Count == 0 ? "n/a" : string.Join(";", parts.ToArray());
        }

        private static string BuildFullCharacterMeshSummary(FullCharacterMessage message)
        {
            var parts = new List<string>();
            AddStatValue(parts, message.Stats1, (int)StatIds.mesh, "mesh");
            AddStatValue(parts, message.Stats1, (int)StatIds.headmesh, "headMesh");
            AddStatValue(parts, message.Stats1, (int)StatIds.monsterdata, "monsterData");
            AddStatValue(parts, message.Stats2, (int)StatIds.mesh, "mesh2");
            AddStatValue(parts, message.Stats2, (int)StatIds.headmesh, "headMesh2");
            AddStatValue(parts, message.Stats2, (int)StatIds.monsterdata, "monsterData2");
            return parts.Count == 0 ? "n/a" : string.Join(";", parts.ToArray());
        }

        private static void AddStatValue(
            ICollection<string> parts,
            GameTuple<CharacterStat, uint>[] stats,
            int statId,
            string label)
        {
            if (stats == null)
            {
                return;
            }

            foreach (GameTuple<CharacterStat, uint> stat in stats)
            {
                if ((int)stat.Value1 == statId)
                {
                    parts.Add(label + "=" + stat.Value2.ToString(CultureInfo.InvariantCulture));
                    return;
                }
            }
        }

        private static void AddStatValue(
            ICollection<string> parts,
            GameTuple<int, uint>[] stats,
            int statId,
            string label)
        {
            if (stats == null)
            {
                return;
            }

            foreach (GameTuple<int, uint> stat in stats)
            {
                if (stat.Value1 == statId)
                {
                    parts.Add(label + "=" + stat.Value2.ToString(CultureInfo.InvariantCulture));
                    return;
                }
            }
        }

        private static void WarnIfSuspiciousObjectValues(
            GridZoneInContext context,
            ObjectDetails details,
            string messageName)
        {
            WarnIfSuspiciousValue("message=" + messageName + " playfieldId", details.PlayfieldId, context);
            WarnIfSuspiciousValue("message=" + messageName + " objectInstance", details.ObjectInstance, context);
            WarnIfSuspiciousValue("message=" + messageName + " objectTypeId", details.ObjectTypeId, context);
        }

        private static void WarnIfSuspiciousRouteValues(GridZoneInContext context)
        {
            WarnIfSuspiciousValue("sourcePlayfieldId", context.SourcePlayfieldId, context);
            WarnIfSuspiciousValue("sourceTerminalTypeId", context.SourceTerminalTypeId, context);
            WarnIfSuspiciousValue("sourceTerminalInstance", context.SourceTerminalInstance, context);
            WarnIfSuspiciousValue("sourceTerminalTemplateId", context.SourceTerminalTemplateId, context);
            WarnIfSuspiciousValue("destinationPlayfieldId", context.DestinationPlayfieldId, context);
            WarnIfSuspiciousValue("destinationStatelTypeId", context.DestinationStatelTypeId, context);
            WarnIfSuspiciousValue("destinationStatelInstance", context.DestinationStatelInstance, context);
            WarnIfSuspiciousValue("destinationStatelTemplateId", context.DestinationStatelTemplateId, context);
            WarnIfSuspiciousValue("rawDestinationInstance", context.RawDestinationInstance, context);
        }

        private static void WarnIfSuspiciousValue(string field, int value, GridZoneInContext context)
        {
            if (value != SuspiciousValue)
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GRID_ZONE_IN_WARNING value_0x12 field={0} value={1} sourceTerminal={2} destStatel={3} note=0x12 maps to StatIds.Stamina when interpreted as a character stat id; it is not a known IdentityType in AORebirth.",
                    field,
                    value,
                    IdentityPart(context.SourceTerminalTypeId, context.SourceTerminalInstance),
                    IdentityPart(context.DestinationStatelTypeId, context.DestinationStatelInstance)));
        }

        private static void WarnIfVehicle(ObjectDetails details, string messageName)
        {
            if (details.ObjectTypeName.IndexOf("Vehicle", StringComparison.OrdinalIgnoreCase) < 0
                && messageName.IndexOf("Vehicle", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GRID_ZONE_IN_WARNING vehicle_route message={0} object={1} objectTypeId={2} objectTypeName={3}",
                    messageName,
                    IdentityPart(details.ObjectTypeId, details.ObjectInstance),
                    details.ObjectTypeId,
                    details.ObjectTypeName));
        }

        private static void LogGridRoute(GridZoneInContext context)
        {
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GRID_ROUTE sourcePf={0} sourceTerminal={1} sourceTemplate={2} rawDestTerminal={3} destPf={4} destStatel={5} destTemplate={6} landing=({7:F3},{8:F3},{9:F3}) route={10} evidence={11}",
                    context.SourcePlayfieldId,
                    IdentityPart(context.SourceTerminalTypeId, context.SourceTerminalInstance),
                    context.SourceTerminalTemplateId,
                    IdentityPart((int)IdentityType.Terminal, context.RawDestinationInstance),
                    context.DestinationPlayfieldId,
                    IdentityPart(context.DestinationStatelTypeId, context.DestinationStatelInstance),
                    context.DestinationStatelTemplateId,
                    context.LandingX,
                    context.LandingY,
                    context.LandingZ,
                    context.RouteKind,
                    context.Evidence));
        }

        private static void LogGridExitComparison(GridZoneInContext context)
        {
            PlayfieldData gridPlayfield;
            if (!PlayfieldLoader.PFData.TryGetValue(GridPlayfieldId, out gridPlayfield))
            {
                return;
            }

            StatelData expectedExit = FindGridExit(gridPlayfield, context.DestinationStatelInstance);
            StatelData rawExit = FindGridExit(gridPlayfield, context.RawDestinationInstance);

            if (expectedExit == null)
            {
                return;
            }

            string expectedNearby = FormatNearbyStatels(gridPlayfield, expectedExit);
            string rawNearby = rawExit == null ? "missing" : FormatNearbyStatels(gridPlayfield, rawExit);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GRID_EXIT_COMPARE sourceTerminal={0} rawExit={1} expectedExit={2} rawNearby=[{3}] expectedNearby=[{4}]",
                    IdentityPart(context.SourceTerminalTypeId, context.SourceTerminalInstance),
                    rawExit == null ? "missing" : rawExit.Identity.ToString(true),
                    expectedExit.Identity.ToString(true),
                    rawNearby,
                    expectedNearby));
        }

        private static StatelData FindGridExit(PlayfieldData gridPlayfield, int instance)
        {
            return gridPlayfield.Statels.FirstOrDefault(
                x => x.Identity.Instance == instance && x.TemplateId == GridExitTerminalTemplateId);
        }

        private static string FormatNearbyStatels(PlayfieldData playfield, StatelData center)
        {
            return string.Join(
                "|",
                playfield.Statels
                    .Where(x => Distance2D(x, center) <= 8.0f)
                    .OrderBy(x => Distance2D(x, center))
                    .ThenBy(x => x.Identity.Instance)
                    .Take(12)
                    .Select(
                        x => string.Format(
                            CultureInfo.InvariantCulture,
                            "{0};template={1};type={2};coords=({3:F1},{4:F1},{5:F1});distance={6:F2}",
                            x.Identity.ToString(true),
                            x.TemplateId,
                            ResolveIdentityTypeName((int)x.Identity.Type),
                            x.X,
                            x.Y,
                            x.Z,
                            Distance2D(x, center)))
                    .ToArray());
        }

        private static float Distance2D(StatelData left, StatelData right)
        {
            float dx = left.X - right.X;
            float dz = left.Z - right.Z;
            return (float)Math.Sqrt((dx * dx) + (dz * dz));
        }

        private static string ResolveIdentityTypeName(int typeId)
        {
            if (Enum.IsDefined(typeof(IdentityType), typeId))
            {
                return ((IdentityType)typeId).ToString();
            }

            return "UnknownIdentityType";
        }

        private static string IdentityPart(int typeId, int instance)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}",
                typeId.ToString("X8", CultureInfo.InvariantCulture),
                instance.ToString("X8", CultureInfo.InvariantCulture));
        }

        private static string FormatCoordinate(Coordinate coordinate)
        {
            if (coordinate == null)
            {
                return "n/a";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:F3},{1:F3},{2:F3})",
                coordinate.x,
                coordinate.y,
                coordinate.z);
        }

        private static string FormatVector(Vector3 vector)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:F3},{1:F3},{2:F3})",
                vector.X,
                vector.Y,
                vector.Z);
        }

        private static string FormatCoreQuaternion(AORebirth.Core.Vector.Quaternion heading)
        {
            if (heading == null)
            {
                return "n/a";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:F6},{1:F6},{2:F6},{3:F6})",
                heading.xf,
                heading.yf,
                heading.zf,
                heading.wf);
        }

        private static string FormatQuaternion(Quaternion heading)
        {
            if (heading == null)
            {
                return "n/a";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:F6},{1:F6},{2:F6},{3:F6})",
                heading.X,
                heading.Y,
                heading.Z,
                heading.W);
        }

        private static void CleanupExpiredContexts()
        {
            DateTime now = DateTime.UtcNow;
            foreach (int key in PendingContexts.Where(x => x.Value.PendingExpiresAt <= now).Select(x => x.Key).ToList())
            {
                PendingContexts.Remove(key);
            }

            foreach (int key in ActiveContexts.Where(x => x.Value.ActiveExpiresAt <= now).Select(x => x.Key).ToList())
            {
                ActiveContexts.Remove(key);
            }
        }

        private sealed class GridZoneInContext
        {
            public int CharacterId { get; set; }

            public int SourcePlayfieldId { get; set; }

            public int SourceTerminalTypeId { get; set; }

            public int SourceTerminalInstance { get; set; }

            public int SourceTerminalTemplateId { get; set; }

            public int DestinationPlayfieldId { get; set; }

            public int DestinationStatelTypeId { get; set; }

            public int DestinationStatelInstance { get; set; }

            public int DestinationStatelTemplateId { get; set; }

            public int RawDestinationInstance { get; set; }

            public float LandingX { get; set; }

            public float LandingY { get; set; }

            public float LandingZ { get; set; }

            public string RouteKind { get; set; }

            public string Evidence { get; set; }

            public DateTime PendingExpiresAt { get; set; }

            public DateTime ActiveExpiresAt { get; set; }
        }

        private sealed class ObjectDetails
        {
            public bool HasIdentity { get; set; }

            public int PlayfieldId { get; set; }

            public int ObjectTypeId { get; set; }

            public int ObjectInstance { get; set; }

            public string ObjectTypeName { get; set; }

            public string Coordinates { get; set; }

            public string Heading { get; set; }

            public string ModelResourceMesh { get; set; }
        }
    }
}
