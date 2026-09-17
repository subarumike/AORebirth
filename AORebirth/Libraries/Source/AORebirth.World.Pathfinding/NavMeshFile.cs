namespace AORebirth.World.Pathfinding
{
    using System;
    using System.IO;

    using DotRecast.Core;
    using DotRecast.Detour;
    using DotRecast.Detour.Io;

    public static class NavMeshFile
    {
        public const int Magic = 0x4D4E4F41; // 'AONM'

        public static void Write(string path, NavMeshBakeResult result)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentNullException.ThrowIfNull(result);

            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new BinaryWriter(stream);
            WriteHeader(writer, result);
            new DtMeshSetWriter().Write(writer, result.Mesh, RcByteOrder.LITTLE_ENDIAN, cCompatibility: false);
        }

        public static DtNavMesh Read(string path, out int playfieldId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream);
            playfieldId = ReadHeader(reader);
            return new DtMeshSetReader().Read(reader, maxVertPerPoly: 6);
        }

        static void WriteHeader(BinaryWriter writer, NavMeshBakeResult result)
        {
            NavMeshBuildSettings settings = result.Settings;
            writer.Write(Magic);
            writer.Write(NavMeshBuildSettings.CurrentVersion);
            writer.Write(result.PlayfieldId);
            writer.Write(settings.CellSize);
            writer.Write(settings.CellHeight);
            writer.Write(settings.AgentRadius);
            writer.Write(settings.AgentHeight);
            writer.Write(settings.AgentMaxClimb);
        }

        static int ReadHeader(BinaryReader reader)
        {
            int magic = reader.ReadInt32();
            if (magic != Magic)
                throw new InvalidDataException("Navmesh.dat magic is not AONM.");

            int version = reader.ReadInt32();
            if (version != NavMeshBuildSettings.CurrentVersion)
                throw new InvalidDataException("Unsupported Navmesh.dat version " + version + ".");

            int playfieldId = reader.ReadInt32();
            reader.ReadSingle();
            reader.ReadSingle();
            reader.ReadSingle();
            reader.ReadSingle();
            reader.ReadSingle();
            return playfieldId;
        }
    }
}
