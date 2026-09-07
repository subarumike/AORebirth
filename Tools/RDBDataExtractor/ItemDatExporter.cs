namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using AODB;
    using AODB.Common.Enums;
    using AODB.Common.RDBObjects;

    using AORebirth.Core.GameData;

    using ZoneEngine_New.Core.Inventory.Dat;

    /// <summary>
    /// Exports AODB <see cref="ItemObject"/> and <see cref="NanoObject"/> records
    /// into one GameData/items.dat, including DynelType.
    /// </summary>
    internal sealed class ItemDatExporter
    {
        internal const int NanoRecordType = 1040005;

        private const int QualityStat = 54;
        private const int FlagsStat = 0;
        private const int MultipleCountStat = 412;

        private readonly RdbController controller;
        private readonly string gameDataDirectory;

        internal ItemDatExporter(RdbController controller, string gameDataDirectory)
        {
            if (controller == null)
                throw new ArgumentNullException("controller");
            if (string.IsNullOrWhiteSpace(gameDataDirectory))
                throw new ArgumentException("GameData directory is required.", "gameDataDirectory");

            this.controller = controller;
            this.gameDataDirectory = gameDataDirectory;
        }

        internal bool HasItemRecordType()
        {
            return this.controller.RecordTypeToId.ContainsKey((int)ResourceTypeId.RDBItem);
        }

        internal bool HasNanoRecordType()
        {
            return this.controller.RecordTypeToId.ContainsKey(NanoRecordType);
        }

        /// <summary>
        /// Writes items.dat under the GameData root. Existing files are skipped
        /// unless <paramref name="overwrite"/> is true.
        /// </summary>
        internal ExportFileCounts Export(bool overwrite)
        {
            string path = Path.Combine(
                this.gameDataDirectory,
                GameDataPaths.ItemsFileName);

            if (!overwrite && File.Exists(path))
                return new ExportFileCounts(0, 1);

            if (!this.HasItemRecordType())
            {
                throw new InvalidOperationException(
                    "RDB item record type "
                    + (int)ResourceTypeId.RDBItem
                    + " was not found.");
            }

            if (!this.HasNanoRecordType())
            {
                throw new InvalidOperationException(
                    "RDB nano record type " + NanoRecordType + " was not found.");
            }

            List<DatItemTemplate> templates = this.BuildTemplates();
            ItemsDatWriter.Write(path, templates);

            Console.WriteLine(
                "exported "
                + GameDataPaths.ItemsFileName
                + " templates="
                + templates.Count);
            return new ExportFileCounts(1, 0);
        }

        private List<DatItemTemplate> BuildTemplates()
        {
            var templates = new List<DatItemTemplate>();

            foreach (int itemId in this.controller.RecordTypeToId[(int)ResourceTypeId.RDBItem].Keys.OrderBy(id => id))
            {
                ItemObject item = this.controller.Get<ItemObject>(ResourceTypeId.RDBItem, itemId);
                if (item == null)
                    continue;

                templates.Add(Map(itemId, item.DynelType, item.Stats));
            }

            foreach (int nanoId in this.controller.RecordTypeToId[NanoRecordType].Keys.OrderBy(id => id))
            {
                NanoObject nano = this.controller.Get<NanoObject>(
                    (ResourceTypeId)NanoRecordType,
                    nanoId);
                if (nano == null)
                    continue;

                templates.Add(Map(nanoId, nano.DynelType, nano.Stats));
            }

            templates.Sort((left, right) => left.ID.CompareTo(right.ID));
            return templates;
        }

        internal static DatItemTemplate Map(int id, int dynelType, Dictionary<StatId, uint> stats)
        {
            var template = new DatItemTemplate
            {
                ID = id,
                DynelType = dynelType,
                Quality = 1,
            };

            if (stats == null)
                return template;

            foreach (KeyValuePair<StatId, uint> pair in stats)
            {
                int key = (int)pair.Key;
                int value = unchecked((int)pair.Value);
                template.Stats[key] = value;
                if (key == QualityStat && value > 0)
                    template.Quality = value;
                else if (key == FlagsStat)
                    template.Flags = value;
                else if (key == MultipleCountStat)
                    template.MultipleCount = value;
            }

            return template;
        }
    }
}
