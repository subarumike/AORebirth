namespace AORebirth.Core.GameData
{
    using System;
    using System.IO;

    /// <summary>
    /// Length-prefixed Collision.dat layout written by RDBDataExtractor:
    /// u32 tilemapLength, tilemap bytes, u32 surfaceLength, surface bytes.
    /// </summary>
    public static class PlayfieldCollisionDat
    {
        public static byte[] Build(byte[] tilemapPayload, byte[] surfacePayload)
        {
            byte[] tilemap = tilemapPayload ?? Array.Empty<byte>();
            byte[] surface = surfacePayload ?? Array.Empty<byte>();
            using (MemoryStream stream = new MemoryStream(8 + tilemap.Length + surface.Length))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(tilemap.Length);
                writer.Write(tilemap);
                writer.Write(surface.Length);
                writer.Write(surface);
                return stream.ToArray();
            }
        }

        public static void Parse(
            byte[] collisionDat,
            out byte[] tilemapPayload,
            out byte[] surfacePayload)
        {
            if (collisionDat == null)
            {
                throw new ArgumentNullException("collisionDat");
            }

            using (MemoryStream stream = new MemoryStream(collisionDat, writable: false))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int tilemapLength = reader.ReadInt32();
                if (tilemapLength < 0 || tilemapLength > collisionDat.Length - 8)
                {
                    throw new InvalidDataException(
                        "Collision.dat tilemap length was invalid.");
                }

                tilemapPayload = reader.ReadBytes(tilemapLength);
                if (tilemapPayload.Length != tilemapLength)
                {
                    throw new InvalidDataException(
                        "Collision.dat ended before tilemap payload.");
                }

                int surfaceLength = reader.ReadInt32();
                if (surfaceLength < 0
                    || stream.Position + surfaceLength > collisionDat.Length)
                {
                    throw new InvalidDataException(
                        "Collision.dat surface length was invalid.");
                }

                surfacePayload = reader.ReadBytes(surfaceLength);
                if (surfacePayload.Length != surfaceLength)
                {
                    throw new InvalidDataException(
                        "Collision.dat ended before surface payload.");
                }
            }
        }
    }
}
