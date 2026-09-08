namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
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
        /// Writes Walls.dat, Dynels.dat, Doors.dat, Destinations.dat, and/or Collision.dat from RDB raw bytes
        /// (the same payload AODB Deserialize consumes). Existing files are skipped unless
        /// <paramref name="overwrite"/> is true.
        /// </summary>
        internal ExportFileCounts Export(int playfieldId, bool overwrite)
        {
            string folder = Path.Combine(
                this.outputDirectory,
                playfieldId.ToString());
            string wallsPath = Path.Combine(folder, GameDataPaths.WallsFileName);
            string dynelsPath = Path.Combine(folder, GameDataPaths.DynelsFileName);
            string doorsPath = Path.Combine(folder, GameDataPaths.DoorsFileName);
            string destinationsPath = Path.Combine(folder, GameDataPaths.DestinationsFileName);
            string collisionPath = Path.Combine(folder, GameDataPaths.CollisionFileName);
            string surfacesPath = Path.Combine(folder, GameDataPaths.SurfacesFileName);

            byte[] wallsPayload = TryGetRaw((int)ResourceTypeId.PlayfieldWall, playfieldId);
            byte[] dynelsPayload = TryGetRaw((int)ResourceTypeId.PlayfieldDynels, playfieldId);
            byte[] doorsPayload = TryGetRaw((int)ResourceTypeId.PlayfieldDoor, playfieldId);
            byte[] destinationsPayload = TryGetRaw(PlayfieldDestinationResourceTypeId, playfieldId);
            byte[] tilemapPayload = TryGetRaw(TilemapRecordType, playfieldId);
            byte[] surfacePayload = TryGetRaw((int)ResourceTypeId.SurfaceResource, playfieldId);
            if (tilemapPayload != null)
                tilemapPayload = StripRdbRecordHeader(tilemapPayload);
            if (surfacePayload != null)
                surfacePayload = StripRdbRecordHeader(surfacePayload);
            byte[] collisionPayload = null;
            if ((tilemapPayload != null && tilemapPayload.Length > 0)
                || (surfacePayload != null && surfacePayload.Length > 0))
            {
                collisionPayload = PlayfieldCollisionDat.Build(tilemapPayload, surfacePayload);
            }

            List<PlayfieldSurfaceEntry> cellSurfaces = CollectCellSurfaces(playfieldId);
            byte[] surfacesPayload = cellSurfaces.Count > 0
                ? PlayfieldSurfacesDat.Build(cellSurfaces)
                : null;

            int written = 0;
            int skipped = 0;

            if (wallsPayload != null)
            {
                if (TryWriteFile(wallsPath, StripRdbRecordHeader(wallsPayload), overwrite))
                    written++;
                else
                    skipped++;
            }

            if (dynelsPayload != null)
            {
                if (TryWriteFile(dynelsPath, StripRdbRecordHeader(dynelsPayload), overwrite))
                    written++;
                else
                    skipped++;
            }

            if (doorsPayload != null)
            {
                if (TryWriteFile(doorsPath, StripRdbRecordHeader(doorsPayload), overwrite))
                    written++;
                else
                    skipped++;
            }

            if (destinationsPayload != null)
            {
                if (TryWriteFile(destinationsPath, StripRdbRecordHeader(destinationsPayload), overwrite))
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

            if (surfacesPayload != null)
            {
                if (TryWriteFile(surfacesPath, surfacesPayload, overwrite))
                    written++;
                else
                    skipped++;
            }

            return new ExportFileCounts(written, skipped);
        }

        /// <summary>
        /// Outdoor playfields store their static geometry as one SurfaceResource per locality cell,
        /// keyed <c>(playfieldId &lt;&lt; 16) | cellId</c>, so the record whose id is the bare
        /// playfield id usually does not exist. Enumerate the RDB index for the whole id range
        /// instead of guessing which cells are populated.
        /// </summary>
        private List<PlayfieldSurfaceEntry> CollectCellSurfaces(int playfieldId)
        {
            List<PlayfieldSurfaceEntry> entries = new List<PlayfieldSurfaceEntry>();
            int surfaceType = (int)ResourceTypeId.SurfaceResource;
            if (!this.controller.RecordTypeToId.ContainsKey(surfaceType))
                return entries;

            List<int> recordIds = new List<int>();
            foreach (int recordId in this.controller.RecordTypeToId[surfaceType].Keys)
            {
                if ((recordId >> 16) == playfieldId && (recordId & 0xFFFF) != 0)
                    recordIds.Add(recordId);
            }

            recordIds.Sort();
            for (int i = 0; i < recordIds.Count; i++)
            {
                byte[] raw = this.controller.GetRaw(surfaceType, recordIds[i]);
                if (raw == null || raw.Length == 0)
                    continue;

                entries.Add(new PlayfieldSurfaceEntry(
                    recordIds[i] & 0xFFFF,
                    StripRdbRecordHeader(raw)));
            }

            return entries;
        }

        private const int PlayfieldDestinationResourceTypeId = 1000001;

        /// <summary>
        /// AODB GetRaw includes type+id+version (12 bytes) before the body that
        /// <c>RDBObject.Deserialize</c> expects.
        /// </summary>
        private static byte[] StripRdbRecordHeader(byte[] payload)
        {
            if (payload == null || payload.Length < 12)
                return payload;

            uint typeId = unchecked((uint)BitConverter.ToInt32(payload, 0));
            bool looksLikeRdbHeader =
                typeId >= 0x000F4200 && typeId <= 0x000F42FF
                || typeId >= 0x000F6900 && typeId <= 0x000F69FF
                || typeId == 0x000FDE97;
            if (!looksLikeRdbHeader)
                return payload;

            byte[] body = new byte[payload.Length - 12];
            Buffer.BlockCopy(payload, 12, body, 0, body.Length);
            return body;
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
