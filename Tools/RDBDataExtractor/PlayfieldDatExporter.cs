namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.IO;
    using AODB;
    using AODB.Common.RDBObjects;
    using AORebirth.Core.GameData;

    internal sealed class PlayfieldDatExporter
    {
        private const int TilemapRecordType = 1000009;

        private readonly RdbController controller;
        private readonly string outputDirectory;

        internal PlayfieldDatExporter(RdbController controller, string outputDirectory)
        {
            if (controller == null)
                throw new ArgumentNullException("controller");

            this.controller = controller;
            this.outputDirectory = outputDirectory;
        }

        /// <summary>
        /// Writes Walls.dat, Dynels.dat, and/or Collision.dat from RDB raw bytes (the same
        /// payload AODB Deserialize consumes). Existing files are skipped unless
        /// <paramref name="overwrite"/> is true.
        /// </summary>
        internal ExportFileCounts Export(int playfieldId, bool overwrite)
        {
            string folder = Path.Combine(
                this.outputDirectory,
                playfieldId.ToString());
            string wallsPath = Path.Combine(folder, GameDataPaths.WallsFileName);
            string dynelsPath = Path.Combine(folder, GameDataPaths.DynelsFileName);
            string collisionPath = Path.Combine(folder, GameDataPaths.CollisionFileName);

            byte[] wallsPayload = TryGetRaw((int)ResourceTypeId.PlayfieldWall, playfieldId);
            byte[] dynelsPayload = TryGetRaw((int)ResourceTypeId.PlayfieldDynels, playfieldId);
            byte[] tilemapPayload = TryGetRaw(TilemapRecordType, playfieldId);
            byte[] surfacePayload = TryGetRaw((int)ResourceTypeId.SurfaceResource, playfieldId);
            byte[] collisionPayload = null;
            if ((tilemapPayload != null && tilemapPayload.Length > 0)
                || (surfacePayload != null && surfacePayload.Length > 0))
            {
                collisionPayload = PlayfieldCollisionDat.Build(tilemapPayload, surfacePayload);
            }

            int written = 0;
            int skipped = 0;

            if (wallsPayload != null)
            {
                if (TryWriteFile(wallsPath, wallsPayload, overwrite))
                    written++;
                else
                    skipped++;
            }

            if (dynelsPayload != null)
            {
                if (TryWriteFile(dynelsPath, dynelsPayload, overwrite))
                    written++;
                else
                    skipped++;
            }

            if (collisionPayload != null)
            {
                if (TryWriteFile(collisionPath, collisionPayload, overwrite))
                    written++;
                else
                    skipped++;
            }

            return new ExportFileCounts(written, skipped);
        }

        private byte[] TryGetRaw(int recordType, int recordId)
        {
            if (!HasRecord(recordType, recordId))
                return null;

            byte[] raw = this.controller.GetRaw(recordType, recordId);
            if (raw == null || raw.Length == 0)
                return null;

            return raw;
        }

        private bool HasRecord(int recordType, int recordId)
        {
            if (!this.controller.RecordTypeToId.ContainsKey(recordType))
                return false;

            return this.controller.RecordTypeToId[recordType].ContainsKey(recordId);
        }

        private static bool TryWriteFile(string path, byte[] payload, bool overwrite)
        {
            if (!overwrite && File.Exists(path))
                return false;

            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            File.WriteAllBytes(path, payload);
            return true;
        }
    }
}
