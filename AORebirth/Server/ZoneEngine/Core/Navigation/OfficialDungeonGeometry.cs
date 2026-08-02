namespace ZoneEngine.Core.Navigation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;

    using System.Web.Script.Serialization;

    internal sealed class OfficialDungeonGeometryLoadResult
    {
        private OfficialDungeonGeometryLoadResult(
            OfficialDungeonGeometry geometry,
            string error)
        {
            this.Geometry = geometry;
            this.Error = error ?? string.Empty;
        }

        internal OfficialDungeonGeometry Geometry { get; private set; }

        internal string Error { get; private set; }

        internal bool IsLoaded
        {
            get { return this.Geometry != null && string.IsNullOrEmpty(this.Error); }
        }

        internal static OfficialDungeonGeometryLoadResult Loaded(
            OfficialDungeonGeometry geometry)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException("geometry");
            }

            return new OfficialDungeonGeometryLoadResult(geometry, string.Empty);
        }

        internal static OfficialDungeonGeometryLoadResult Failed(string error)
        {
            return new OfficialDungeonGeometryLoadResult(
                null,
                string.IsNullOrWhiteSpace(error)
                    ? "Official dungeon geometry is unavailable."
                    : error.Trim());
        }
    }

    internal static class Pf1931OfficialDungeonGeometryLoader
    {
        internal const int TemplePlayfieldResource = 1931;

        internal const int TempleTilemapResource = 1930;

        internal const string RelativePath =
            @"Content\Official\TempleOfThreeWinds\pf1931-dungeon-geometry.json";

        private static readonly Lazy<OfficialDungeonGeometryLoadResult> CurrentGeometry =
            new Lazy<OfficialDungeonGeometryLoadResult>(LoadDefaultPath, true);

        internal static string DefaultPath
        {
            get
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                DirectoryInfo cursor = new DirectoryInfo(Path.GetFullPath(baseDirectory));
                while (cursor != null)
                {
                    string runtimeCandidate = Path.Combine(cursor.FullName, RelativePath);
                    if (File.Exists(runtimeCandidate))
                    {
                        return runtimeCandidate;
                    }

                    string sourceCandidate = Path.Combine(
                        cursor.FullName,
                        @"AORebirth\Server\ZoneEngine",
                        RelativePath);
                    if (File.Exists(sourceCandidate))
                    {
                        return sourceCandidate;
                    }

                    cursor = cursor.Parent;
                }

                return Path.Combine(baseDirectory, RelativePath);
            }
        }

        internal static OfficialDungeonGeometryLoadResult Current
        {
            get { return CurrentGeometry.Value; }
        }

        internal static OfficialDungeonGeometryLoadResult LoadPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return OfficialDungeonGeometryLoadResult.Failed(
                    "Official dungeon geometry path is missing.");
            }

            if (!File.Exists(path))
            {
                return OfficialDungeonGeometryLoadResult.Failed(
                    "Official dungeon geometry file was not found: " + path);
            }

            try
            {
                return LoadJson(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                return OfficialDungeonGeometryLoadResult.Failed(
                    "Official dungeon geometry read failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message);
            }
        }

        internal static OfficialDungeonGeometryLoadResult LoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return OfficialDungeonGeometryLoadResult.Failed(
                    "Official dungeon geometry JSON is empty.");
            }

            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                OfficialDungeonGeometryDocumentDto document =
                    serializer.Deserialize<OfficialDungeonGeometryDocumentDto>(json);
                string validationError;
                OfficialDungeonGeometry geometry;
                if (!TryConvert(document, out geometry, out validationError))
                {
                    return OfficialDungeonGeometryLoadResult.Failed(validationError);
                }

                return OfficialDungeonGeometryLoadResult.Loaded(geometry);
            }
            catch (Exception exception)
            {
                return OfficialDungeonGeometryLoadResult.Failed(
                    "Official dungeon geometry validation failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message);
            }
        }

        private static OfficialDungeonGeometryLoadResult LoadDefaultPath()
        {
            return LoadPath(DefaultPath);
        }

        private static bool TryConvert(
            OfficialDungeonGeometryDocumentDto document,
            out OfficialDungeonGeometry geometry,
            out string error)
        {
            geometry = null;
            if (document == null)
            {
                error = "Official dungeon geometry document is missing.";
                return false;
            }

            if (document.SchemaVersion != OfficialDungeonGeometry.SupportedSchemaVersion
                || document.PlayfieldResource != TemplePlayfieldResource
                || document.TilemapResource != TempleTilemapResource)
            {
                error = "Official PF1931 dungeon geometry identity or schema is invalid.";
                return false;
            }

            if (document.Width != 200
                || document.Height != 200
                || !document.TileSize.HasValue
                || Math.Abs(document.TileSize.Value - 2.0) > 1.0e-6
                || !document.HeightScale.HasValue
                || Math.Abs(document.HeightScale.Value - 0.2) > 1.0e-6)
            {
                error = "Official PF1931 tilemap dimensions or scales are invalid.";
                return false;
            }

            if (!IsSha256(document.SourceSha256)
                || !IsSha256(document.PlayfieldRecordSha256)
                || !IsSha256(document.TilemapRecordSha256)
                || !IsSha256(document.CollisionPixelsSha256)
                || !IsSha256(document.HeightPixelsSha256))
            {
                error = "Official PF1931 source hashes are invalid.";
                return false;
            }

            byte[] collision;
            byte[] heights;
            try
            {
                collision = Convert.FromBase64String(document.CollisionDataBase64 ?? string.Empty);
                heights = Convert.FromBase64String(document.HeightDataBase64 ?? string.Empty);
            }
            catch (FormatException)
            {
                error = "Official PF1931 tilemap payload is not valid base64.";
                return false;
            }

            int expectedLength = document.Width.Value * document.Height.Value;
            if (collision.Length != expectedLength || heights.Length != expectedLength)
            {
                error = "Official PF1931 tilemap payload length is invalid.";
                return false;
            }

            if (!MatchesSha256(collision, document.CollisionPixelsSha256)
                || !MatchesSha256(heights, document.HeightPixelsSha256))
            {
                error = "Official PF1931 decoded tilemap hashes do not match the source manifest.";
                return false;
            }

            if (document.Rooms == null || document.Rooms.Length != 30)
            {
                error = "Official PF1931 room inventory must contain exactly 30 rooms.";
                return false;
            }

            var rooms = new List<OfficialDungeonRoom>(document.Rooms.Length);
            var roomIndices = new HashSet<int>();
            foreach (OfficialDungeonRoomDto room in document.Rooms)
            {
                OfficialDungeonRoom converted;
                if (!TryConvertRoom(
                        room,
                        document.Width.Value,
                        document.Height.Value,
                        out converted,
                        out error))
                {
                    return false;
                }

                if (!roomIndices.Add(converted.Index))
                {
                    error = "Official PF1931 room indices are duplicated.";
                    return false;
                }

                rooms.Add(converted);
            }

            if (roomIndices.Any(index => index < 0 || index >= rooms.Count)
                || rooms.Any(
                    room => room.Doors.Any(
                        door => door.RoomIndex < -1
                                || door.RoomIndex >= rooms.Count)))
            {
                error = "Official PF1931 room or door indices are outside the room inventory.";
                return false;
            }

            geometry = new OfficialDungeonGeometry(
                document.SchemaVersion.Value,
                document.PlayfieldResource.Value,
                document.TilemapResource.Value,
                document.Source,
                document.SourceSha256,
                document.Width.Value,
                document.Height.Value,
                document.TileSize.Value,
                document.HeightScale.Value,
                collision,
                heights,
                rooms);
            error = string.Empty;
            return true;
        }

        private static bool TryConvertRoom(
            OfficialDungeonRoomDto room,
            int width,
            int height,
            out OfficialDungeonRoom converted,
            out string error)
        {
            converted = null;
            if (room == null
                || !room.Index.HasValue
                || string.IsNullOrWhiteSpace(room.Name)
                || !room.RotationDegrees.HasValue
                || !room.Floor.HasValue
                || room.LocalRect == null
                || room.Center == null
                || !room.LocalRect.MinX.HasValue
                || !room.LocalRect.MinZ.HasValue
                || !room.LocalRect.MaxX.HasValue
                || !room.LocalRect.MaxZ.HasValue
                || !room.Center.X.HasValue
                || !room.Center.Y.HasValue
                || !room.Center.Z.HasValue)
            {
                error = "Official PF1931 room metadata is incomplete.";
                return false;
            }

            int rotation = room.RotationDegrees.Value;
            if ((rotation != 0 && rotation != 90 && rotation != 180 && rotation != 270)
                || room.LocalRect.MinX.Value < 1
                || room.LocalRect.MinZ.Value < 1
                || room.LocalRect.MaxX.Value <= room.LocalRect.MinX.Value
                || room.LocalRect.MaxZ.Value <= room.LocalRect.MinZ.Value
                || room.LocalRect.MaxX.Value >= width
                || room.LocalRect.MaxZ.Value >= height
                || !IsFinite(room.Center.X.Value)
                || !IsFinite(room.Center.Y.Value)
                || !IsFinite(room.Center.Z.Value))
            {
                error = "Official PF1931 room bounds, transform, or rotation are invalid.";
                return false;
            }

            var doors = new List<OfficialDungeonDoor>();
            foreach (OfficialDungeonDoorDto door in room.Doors ?? new OfficialDungeonDoorDto[0])
            {
                if (door == null || !door.RoomIndex.HasValue || !door.DoorIndex.HasValue)
                {
                    error = "Official PF1931 room door metadata is incomplete.";
                    return false;
                }

                doors.Add(new OfficialDungeonDoor(door.RoomIndex.Value, door.DoorIndex.Value));
            }

            converted = new OfficialDungeonRoom(
                room.Index.Value,
                room.Name.Trim(),
                rotation,
                room.Floor.Value,
                room.LocalRect.MinX.Value,
                room.LocalRect.MinZ.Value,
                room.LocalRect.MaxX.Value,
                room.LocalRect.MaxZ.Value,
                room.Center.X.Value,
                room.Center.Y.Value,
                room.Center.Z.Value,
                doors);
            error = string.Empty;
            return true;
        }

        private static bool MatchesSha256(byte[] value, string expected)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] actual = sha256.ComputeHash(value);
                return string.Equals(
                    BitConverter.ToString(actual).Replace("-", string.Empty),
                    expected,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
            {
                return false;
            }

            return value.All(
                character => (character >= '0' && character <= '9')
                             || (character >= 'a' && character <= 'f')
                             || (character >= 'A' && character <= 'F'));
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    internal sealed class OfficialDungeonGeometry
    {
        internal const int SupportedSchemaVersion = 1;

        internal const byte ImpassableCollisionValue = 0x80;

        private const double CoordinateTolerance = 1.0e-7;

        private readonly byte[] collision;

        private readonly byte[] heights;

        private readonly OfficialDungeonRoom[] rooms;

        internal OfficialDungeonGeometry(
            int schemaVersion,
            int playfieldResource,
            int tilemapResource,
            string source,
            string sourceSha256,
            int width,
            int height,
            double tileSize,
            double heightScale,
            byte[] collision,
            byte[] heights,
            IEnumerable<OfficialDungeonRoom> rooms)
        {
            this.SchemaVersion = schemaVersion;
            this.PlayfieldResource = playfieldResource;
            this.TilemapResource = tilemapResource;
            this.Source = source ?? string.Empty;
            this.SourceSha256 = sourceSha256 ?? string.Empty;
            this.Width = width;
            this.Height = height;
            this.TileSize = tileSize;
            this.HeightScale = heightScale;
            this.collision = (byte[])collision.Clone();
            this.heights = (byte[])heights.Clone();
            this.rooms = rooms.OrderBy(room => room.Index).ToArray();
            foreach (OfficialDungeonRoom room in this.rooms)
            {
                room.SetMinimumTileHeight(this.FindMinimumTileHeight(room));
            }
        }

        internal int SchemaVersion { get; private set; }

        internal int PlayfieldResource { get; private set; }

        internal int TilemapResource { get; private set; }

        internal string Source { get; private set; }

        internal string SourceSha256 { get; private set; }

        internal int Width { get; private set; }

        internal int Height { get; private set; }

        internal double TileSize { get; private set; }

        internal double HeightScale { get; private set; }

        internal int RoomCount
        {
            get { return this.rooms.Length; }
        }

        internal int DoorConnectionCount
        {
            get
            {
                return this.rooms.Sum(
                    room => room.Doors.Count(door => door.RoomIndex >= 0));
            }
        }

        internal int ExteriorDoorConnectionCount
        {
            get
            {
                return this.rooms.Sum(
                    room => room.Doors.Count(door => door.RoomIndex == -1));
            }
        }

        internal bool HasExteriorDoorConnection(string roomName, int doorIndex)
        {
            return this.rooms.Any(
                room => string.Equals(room.Name, roomName, StringComparison.Ordinal)
                        && room.Doors.Any(
                            door => door.RoomIndex == -1
                                    && door.DoorIndex == doorIndex));
        }

        internal IEnumerable<string> RoomNames
        {
            get { return this.rooms.Select(room => room.Name); }
        }

        internal bool TryProjectToSurface(
            ChaseNavigationPoint reference,
            double worldX,
            double worldZ,
            out ChaseNavigationPoint projected)
        {
            projected = default(ChaseNavigationPoint);
            if (!reference.IsFinite || !IsFinite(worldX) || !IsFinite(worldZ))
            {
                return false;
            }

            bool found = false;
            double nearestDifference = double.MaxValue;
            double nearestY = 0.0;
            int nearestRoom = int.MaxValue;
            foreach (OfficialDungeonRoom room in this.rooms)
            {
                double localX;
                double localZ;
                if (!room.TryWorldToLocal(
                        worldX,
                        worldZ,
                        this.TileSize,
                        out localX,
                        out localZ))
                {
                    continue;
                }

                int cellX = Math.Min(
                    room.TileWidth - 1,
                    Math.Max(0, (int)Math.Floor(localX / this.TileSize)));
                int cellZ = Math.Min(
                    room.TileHeight - 1,
                    Math.Max(0, (int)Math.Floor(localZ / this.TileSize)));
                int mapX = room.MinX + cellX;
                int mapZ = room.MinZ + cellZ;
                if (!this.IsWalkableMapCell(mapX, mapZ))
                {
                    continue;
                }

                double floorY = this.InterpolateFloorHeight(
                    room,
                    cellX,
                    cellZ,
                    localX,
                    localZ);
                double difference = Math.Abs(floorY - reference.Y);
                if (!found
                    || difference < nearestDifference
                    || (Math.Abs(difference - nearestDifference) <= CoordinateTolerance
                        && room.Index < nearestRoom))
                {
                    found = true;
                    nearestDifference = difference;
                    nearestY = floorY;
                    nearestRoom = room.Index;
                }
            }

            if (!found)
            {
                return false;
            }

            projected = new ChaseNavigationPoint(worldX, nearestY, worldZ);
            return true;
        }

        internal bool TryGetRoomAnchor(string roomName, out ChaseNavigationPoint anchor)
        {
            anchor = default(ChaseNavigationPoint);
            OfficialDungeonRoom room = this.rooms.FirstOrDefault(
                candidate => string.Equals(
                    candidate.Name,
                    roomName,
                    StringComparison.OrdinalIgnoreCase));
            if (room == null)
            {
                return false;
            }

            double bestDistance = double.MaxValue;
            bool found = false;
            for (int cellZ = 0; cellZ < room.TileHeight; cellZ++)
            {
                for (int cellX = 0; cellX < room.TileWidth; cellX++)
                {
                    if (!this.IsWalkableMapCell(room.MinX + cellX, room.MinZ + cellZ))
                    {
                        continue;
                    }

                    double localX = (cellX + 0.5) * this.TileSize;
                    double localZ = (cellZ + 0.5) * this.TileSize;
                    double centerX = room.TileWidth * this.TileSize * 0.5;
                    double centerZ = room.TileHeight * this.TileSize * 0.5;
                    double distance = ((localX - centerX) * (localX - centerX))
                                      + ((localZ - centerZ) * (localZ - centerZ));
                    if (distance >= bestDistance)
                    {
                        continue;
                    }

                    double worldX;
                    double worldZ;
                    room.LocalToWorld(
                        localX,
                        localZ,
                        this.TileSize,
                        out worldX,
                        out worldZ);
                    double floorY = this.InterpolateFloorHeight(
                        room,
                        cellX,
                        cellZ,
                        localX,
                        localZ);
                    anchor = new ChaseNavigationPoint(worldX, floorY, worldZ);
                    bestDistance = distance;
                    found = true;
                }
            }

            return found;
        }

        internal bool IsOfficialRoomGraphConnected()
        {
            if (this.rooms.Length == 0)
            {
                return false;
            }

            var visited = new HashSet<int>();
            var pending = new Queue<int>();
            pending.Enqueue(this.rooms[0].Index);
            visited.Add(this.rooms[0].Index);
            while (pending.Count > 0)
            {
                OfficialDungeonRoom room = this.rooms[pending.Dequeue()];
                foreach (OfficialDungeonDoor door in room.Doors)
                {
                    if (door.RoomIndex >= 0 && visited.Add(door.RoomIndex))
                    {
                        pending.Enqueue(door.RoomIndex);
                    }
                }
            }

            return visited.Count == this.rooms.Length;
        }

        internal bool IsWalkableMapCell(int mapX, int mapZ)
        {
            // RDBTilemap collision values 0x00..0x05 and 0x82..0x83 are
            // floor/material cells. The client uses 0x80 as the solid-cell
            // sentinel; retain that exact distinction instead of deriving
            // synthetic room collision from the room rectangles.
            return mapX >= 0
                   && mapX < this.Width
                   && mapZ >= 0
                   && mapZ < this.Height
                   && this.collision[(mapZ * this.Width) + mapX]
                   != ImpassableCollisionValue;
        }

        private int FindMinimumTileHeight(OfficialDungeonRoom room)
        {
            int minimum = byte.MaxValue;
            for (int mapZ = room.MinZ; mapZ < room.MaxZ; mapZ++)
            {
                for (int mapX = room.MinX; mapX < room.MaxX; mapX++)
                {
                    byte collisionValue = this.collision[(mapZ * this.Width) + mapX];
                    if ((collisionValue & 0x7F) == 0)
                    {
                        continue;
                    }

                    minimum = Math.Min(
                        minimum,
                        this.heights[(mapZ * this.Width) + mapX]);
                }
            }

            if (minimum == byte.MaxValue)
            {
                for (int mapZ = room.MinZ; mapZ < room.MaxZ; mapZ++)
                {
                    for (int mapX = room.MinX; mapX < room.MaxX; mapX++)
                    {
                        if (this.IsWalkableMapCell(mapX, mapZ))
                        {
                            minimum = Math.Min(
                                minimum,
                                this.heights[(mapZ * this.Width) + mapX]);
                        }
                    }
                }
            }

            return minimum == byte.MaxValue ? 0 : minimum;
        }

        private double InterpolateFloorHeight(
            OfficialDungeonRoom room,
            int cellX,
            int cellZ,
            double localX,
            double localZ)
        {
            int heightX = room.MinX - 1 + cellX;
            int heightZ = room.MinZ - 1 + cellZ;
            double fractionX = (localX / this.TileSize) - cellX;
            double fractionZ = (localZ / this.TileSize) - cellZ;
            double h00 = this.heights[(heightZ * this.Width) + heightX];
            double h10 = this.heights[(heightZ * this.Width) + heightX + 1];
            double h01 = this.heights[((heightZ + 1) * this.Width) + heightX];
            double h11 = this.heights[((heightZ + 1) * this.Width) + heightX + 1];
            double interpolated;
            if (fractionX + fractionZ <= 1.0)
            {
                interpolated = h00
                               + ((h10 - h00) * fractionX)
                               + ((h01 - h00) * fractionZ);
            }
            else
            {
                interpolated = h11
                               + ((h01 - h11) * (1.0 - fractionX))
                               + ((h10 - h11) * (1.0 - fractionZ));
            }

            return room.CenterY
                   - (room.MinimumTileHeight * this.HeightScale)
                   + (interpolated * this.HeightScale);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    internal sealed class OfficialDungeonRoom
    {
        private readonly OfficialDungeonDoor[] doors;

        internal OfficialDungeonRoom(
            int index,
            string name,
            int rotationDegrees,
            int floor,
            int minX,
            int minZ,
            int maxX,
            int maxZ,
            double centerX,
            double centerY,
            double centerZ,
            IEnumerable<OfficialDungeonDoor> doors)
        {
            this.Index = index;
            this.Name = name;
            this.RotationDegrees = rotationDegrees;
            this.Floor = floor;
            this.MinX = minX;
            this.MinZ = minZ;
            this.MaxX = maxX;
            this.MaxZ = maxZ;
            this.CenterX = centerX;
            this.CenterY = centerY;
            this.CenterZ = centerZ;
            this.doors = (doors ?? new OfficialDungeonDoor[0]).ToArray();
        }

        internal int Index { get; private set; }

        internal string Name { get; private set; }

        internal int RotationDegrees { get; private set; }

        internal int Floor { get; private set; }

        internal int MinX { get; private set; }

        internal int MinZ { get; private set; }

        internal int MaxX { get; private set; }

        internal int MaxZ { get; private set; }

        internal int TileWidth
        {
            get { return this.MaxX - this.MinX; }
        }

        internal int TileHeight
        {
            get { return this.MaxZ - this.MinZ; }
        }

        internal double CenterX { get; private set; }

        internal double CenterY { get; private set; }

        internal double CenterZ { get; private set; }

        internal int MinimumTileHeight { get; private set; }

        internal IEnumerable<OfficialDungeonDoor> Doors
        {
            get { return this.doors; }
        }

        internal void SetMinimumTileHeight(int value)
        {
            this.MinimumTileHeight = value;
        }

        internal bool TryWorldToLocal(
            double worldX,
            double worldZ,
            double tileSize,
            out double localX,
            out double localZ)
        {
            double deltaX = worldX - this.CenterX;
            double deltaZ = worldZ - this.CenterZ;
            double radians = this.RotationDegrees * Math.PI / 180.0;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);

            // The client rotates room terrain around +Y with this handedness.
            double unrotatedX = (deltaX * cosine) - (deltaZ * sine);
            double unrotatedZ = (deltaX * sine) + (deltaZ * cosine);
            localX = unrotatedX + (this.TileWidth * tileSize * 0.5);
            localZ = unrotatedZ + (this.TileHeight * tileSize * 0.5);
            return localX >= 0.0
                   && localZ >= 0.0
                   && localX < this.TileWidth * tileSize
                   && localZ < this.TileHeight * tileSize;
        }

        internal void LocalToWorld(
            double localX,
            double localZ,
            double tileSize,
            out double worldX,
            out double worldZ)
        {
            double unrotatedX = localX - (this.TileWidth * tileSize * 0.5);
            double unrotatedZ = localZ - (this.TileHeight * tileSize * 0.5);
            double radians = this.RotationDegrees * Math.PI / 180.0;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            worldX = this.CenterX
                     + (unrotatedX * cosine)
                     + (unrotatedZ * sine);
            worldZ = this.CenterZ
                     - (unrotatedX * sine)
                     + (unrotatedZ * cosine);
        }
    }

    internal sealed class OfficialDungeonDoor
    {
        internal OfficialDungeonDoor(int roomIndex, int doorIndex)
        {
            this.RoomIndex = roomIndex;
            this.DoorIndex = doorIndex;
        }

        internal int RoomIndex { get; private set; }

        internal int DoorIndex { get; private set; }
    }

    internal sealed class OfficialDungeonGeometryDocumentDto
    {
        public int? SchemaVersion { get; set; }

        public int? PlayfieldResource { get; set; }

        public int? TilemapResource { get; set; }

        public string Source { get; set; }

        public string SourceSha256 { get; set; }

        public string PlayfieldRecordSha256 { get; set; }

        public string TilemapRecordSha256 { get; set; }

        public string CollisionPixelsSha256 { get; set; }

        public string HeightPixelsSha256 { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        public double? TileSize { get; set; }

        public double? HeightScale { get; set; }

        public string CollisionDataBase64 { get; set; }

        public string HeightDataBase64 { get; set; }

        public OfficialDungeonRoomDto[] Rooms { get; set; }
    }

    internal sealed class OfficialDungeonRoomDto
    {
        public int? Index { get; set; }

        public string Name { get; set; }

        public int? RotationDegrees { get; set; }

        public int? Floor { get; set; }

        public OfficialDungeonRectDto LocalRect { get; set; }

        public OfficialDungeonPointDto Center { get; set; }

        public OfficialDungeonDoorDto[] Doors { get; set; }
    }

    internal sealed class OfficialDungeonRectDto
    {
        public int? MinX { get; set; }

        public int? MinZ { get; set; }

        public int? MaxX { get; set; }

        public int? MaxZ { get; set; }
    }

    internal sealed class OfficialDungeonPointDto
    {
        public double? X { get; set; }

        public double? Y { get; set; }

        public double? Z { get; set; }
    }

    internal sealed class OfficialDungeonDoorDto
    {
        public int? RoomIndex { get; set; }

        public int? DoorIndex { get; set; }
    }
}
