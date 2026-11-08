namespace ZoneEngine_New.Tests;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using AORebirth.Interfaces.Persistence.Missions;
using AORebirth.World.Collision;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using ZoneEngine.Core.Missions;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

// Offline reconstruction only. An original generatedmissionbindings row identifies the captured layout;
// the matching generatedmissionobjects rows would still be required to restore an accepted world.
internal static class HistoricalMissionV1Replay
{
    internal sealed record Placement(int Type, int Instance, int SourcePlayfield, Vector3 Position, Quaternion Heading);

    internal sealed record CapturedDoor(Placement Placement, short Room, short LinkedRoom);

    internal sealed record Layout(AcgBuildingGeneratorData Generator, int PlayfieldX, int PlayfieldZ,
        Vector3 Entry, Placement Exit, Placement Objective, CapturedDoor[] Doors, DungeonWorldLayout Geometry,
        IReadOnlyList<DungeonDoorPlacement> StyleDoors, int LivePlayfield, int ExteriorPlayfield, Vector3 ExteriorPosition);

    internal static Layout Reconstruct(JsonElement catalog, GeneratedMissionBinding binding, string gameDataRoot)
    {
        JsonElement row = catalog.EnumerateArray().SingleOrDefault(candidate => candidate.GetProperty("LayoutId").GetString() == binding.BundleId);
        if (row.ValueKind == JsonValueKind.Undefined || !row.GetProperty("IsSelectable").GetBoolean()
            || !row.GetProperty("Completeness").GetProperty("IsSelectionComplete").GetBoolean())
            throw new InvalidDataException("The original accepted mission bundle is absent or incomplete.");

        var offer = binding.Offer;
        int sourcePlayfield = row.GetProperty("SourcePlayfield2").GetInt32();
        JsonElement compatibility = row.GetProperty("Compatibility");
        if (binding.OwnerId <= 0 || binding.QuestType != (int)IdentityType.Mission || binding.QuestInstance <= 0 || binding.KeyInstance <= 0
            || binding.LivePlayfield is < GeneratedMissionIdentitySpace.MinimumLivePlayfield2 or > GeneratedMissionIdentitySpace.MaximumLivePlayfield2
            || binding.LivePlayfield == sourcePlayfield
            || binding.AcceptedAtUtcTicks <= 0 || binding.ExpiresAtUtcTicks <= binding.AcceptedAtUtcTicks
            || offer == null || offer.OwnerId != binding.OwnerId || offer.OfferType != binding.OfferType
            || offer.OfferInstance != binding.OfferInstance || offer.DestinationPlayfield <= 0
            || !float.IsFinite(offer.DestinationX) || !float.IsFinite(offer.DestinationY) || !float.IsFinite(offer.DestinationZ)
            || !compatibility.GetProperty("MissionTypes").EnumerateArray().Any(type => type.GetInt32() == offer.MissionType)
            || offer.Quality < compatibility.GetProperty("MinimumMissionQuality").GetInt32()
            || offer.Quality > compatibility.GetProperty("MaximumMissionQuality").GetInt32())
            throw new InvalidDataException("The original accepted offer does not match the captured mission bundle.");

        byte[] payload = row.GetProperty("generatorPayload").EnumerateArray().Select(value => value.GetByte()).ToArray();
        string hash = Convert.ToHexString(SHA256.HashData(payload));
        if (payload.Length < 8 || !HashEquals(hash, binding.BundleSha256)
            || !HashEquals(hash, row.GetProperty("ExpectedGeneratorPayloadSha256").GetString())
            || !AcgBuildingGeneratorData.TryParse(payload[..^8], out AcgBuildingGeneratorData generator)
            || generator.Identity.Type != IdentityType.AcgBuildingGenerator || (int)generator.Identity.Type != binding.BuildingType
            || generator.Identity.Instance != binding.BuildingInstance
            || binding.BuildingType != row.GetProperty("BuildingIdentity").GetProperty("Type").GetInt32()
            || binding.BuildingInstance != row.GetProperty("BuildingIdentity").GetProperty("Instance").GetInt32())
            throw new InvalidDataException("The captured generator bytes do not match the original binding's hash and building identity.");

        // The old world separated the two signed PlayfieldAnarchyF world coordinates from its generator blob.
        int playfieldX = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(payload.Length - 8));
        int playfieldZ = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(payload.Length - 4));
        if (playfieldX != -1 || playfieldZ != -1)
            throw new InvalidDataException("The captured mission generator has no verified world-coordinate trailer.");

        var doors = row.GetProperty("Dynels").EnumerateArray()
            .Where(dynel => dynel.GetProperty("Category").GetInt32() == 1)
            .OrderBy(dynel => dynel.GetProperty("Slot").GetInt32())
            .Select(dynel => ReadDoor(dynel, sourcePlayfield)).ToArray();
        Placement exit = ReadDirectPlacement(row.GetProperty("Exit"), sourcePlayfield);
        JsonElement objective = row.GetProperty("ObjectiveSlots").EnumerateArray().Single();
        if (objective.GetProperty("TemplateId").GetInt32() != binding.ObjectiveTemplateId)
            throw new InvalidDataException("The original accepted objective template does not match the captured slot.");

        DungeonWorldLayout styleGeometry = DungeonCollisionBuilder.BuildLayout(gameDataRoot, generator);
        if (styleGeometry.Doors.Count != doors.Length)
            throw new InvalidDataException("The room resources do not reproduce the number of captured doors.");
        var originalDoors = new DungeonDoorPlacement[doors.Length];
        for (int i = 0; i < doors.Length; i++)
        {
            CapturedDoor captured = doors[i];
            DungeonDoorPlacement derived = styleGeometry.Doors[i];
            Vector3 exactPosition = captured.Placement.Position;
            var yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, derived.YawDegrees * MathF.PI / 180f);
            if (derived.Room != captured.Room || derived.LinkedRoom != captured.LinkedRoom
                || MathF.Abs(derived.Position.X - exactPosition.X) >= 0.01f || MathF.Abs(derived.Position.Z - exactPosition.Z) >= 0.01f
                || MathF.Abs(Quaternion.Dot(yaw, captured.Placement.Heading)) <= 0.9999f)
                throw new InvalidDataException("The room resources do not reproduce a captured door's links, horizontal position and heading.");

            // Captured doors carry their exact elevations. The style builder uses the owning room's origin Y,
            // which differs from the recorded height at one doorway between rooms with different origins.
            originalDoors[i] = new DungeonDoorPlacement(exactPosition, derived.YawDegrees, captured.Room, captured.LinkedRoom);
        }
        var originalGeometry = new DungeonWorldLayout(styleGeometry.Collision, styleGeometry.Rooms, originalDoors, styleGeometry.Placements);
        return new Layout(generator, playfieldX, playfieldZ, ReadVector(row.GetProperty("EntryPoint")), exit,
            ReadDirectPlacement(objective, sourcePlayfield), doors, originalGeometry, styleGeometry.Doors,
            binding.LivePlayfield, offer.DestinationPlayfield, new Vector3(offer.DestinationX, offer.DestinationY, offer.DestinationZ));
    }

    static CapturedDoor ReadDoor(JsonElement dynel, int sourcePlayfield)
    {
        JsonElement wire = dynel.GetProperty("Wire");
        byte[] bytes = VerifyPacket(wire, "PacketHex", "PacketSha256");
        // AcgLayoutExtractor.ParseDoorFullUpdate ends at Unknown6+Unknown7. The signed halves of
        // Unknown7 are the two generator room indices, with -1 as the exterior boundary.
        if (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)) != (int)N3MessageType.DoorFullUpdate)
            throw new InvalidDataException("A captured door record must contain DoorFullUpdate.");
        return new CapturedDoor(ReadPlacement(dynel, bytes, sourcePlayfield),
            BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(bytes.Length - 4)),
            BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(bytes.Length - 2)));
    }

    static Placement ReadDirectPlacement(JsonElement dynel, int sourcePlayfield)
        => ReadPlacement(dynel, VerifyPacket(dynel, "RawPacketHex", "RawPacketSha256"), sourcePlayfield);

    static byte[] VerifyPacket(JsonElement wire, string hexProperty, string hashProperty)
    {
        byte[] bytes = Convert.FromHexString(wire.GetProperty(hexProperty).GetString()!);
        if (bytes.Length < 73 || !HashEquals(Convert.ToHexString(SHA256.HashData(bytes)), wire.GetProperty(hashProperty).GetString()))
            throw new InvalidDataException("A captured placement packet fails its source hash or minimum size.");
        return bytes;
    }

    static Placement ReadPlacement(JsonElement dynel, byte[] packet, int sourcePlayfield)
    {
        // These are the offline capture extractor's original common FullUpdate and SimpleCharFullUpdate offsets.
        var message = (N3MessageType)BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(16));
        int positionOffset = message switch
        {
            N3MessageType.SimpleCharFullUpdate => 38,
            N3MessageType.DoorFullUpdate or N3MessageType.SimpleItemFullUpdate or N3MessageType.WeaponItemFullUpdate => 41,
            _ => throw new InvalidDataException("The captured placement packet type has no verified position offsets.")
        };
        int type = BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(20));
        int instance = BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(24));
        int playfield = BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(message == N3MessageType.SimpleCharFullUpdate ? 34 : 69));
        var position = new Vector3(ReadSingle(packet, positionOffset), ReadSingle(packet, positionOffset + 4), ReadSingle(packet, positionOffset + 8));
        var heading = new Quaternion(ReadSingle(packet, positionOffset + 12), ReadSingle(packet, positionOffset + 16),
            ReadSingle(packet, positionOffset + 20), ReadSingle(packet, positionOffset + 24));
        JsonElement identity = dynel.GetProperty("CapturedIdentity");
        JsonElement rotation = dynel.GetProperty("Heading");
        if (type != identity.GetProperty("Type").GetInt32() || instance != identity.GetProperty("Instance").GetInt32()
            || playfield != sourcePlayfield || dynel.GetProperty("CapturedPlayfield2").GetInt32() != sourcePlayfield
            || position != ReadVector(dynel.GetProperty("Position"))
            || heading != new Quaternion(rotation.GetProperty("X").GetSingle(), rotation.GetProperty("Y").GetSingle(),
                rotation.GetProperty("Z").GetSingle(), rotation.GetProperty("W").GetSingle()))
            throw new InvalidDataException("The captured placement packet conflicts with the original structured placement.");
        return new Placement(type, instance, sourcePlayfield, position, heading);
    }

    static Vector3 ReadVector(JsonElement vector)
        => new(vector.GetProperty("X").GetSingle(), vector.GetProperty("Y").GetSingle(), vector.GetProperty("Z").GetSingle());

    static float ReadSingle(byte[] bytes, int offset) => BinaryPrimitives.ReadSingleBigEndian(bytes.AsSpan(offset));

    static bool HashEquals(string actual, string? expected) => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
}
