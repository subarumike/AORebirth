namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using AODB;
    using AODB.Common.RDBObjects;
    using AORebirth.Core.GameData;

    /// <summary>
    /// Exports the client collision sphere of every door and mission entrance template (<see cref="DynelCollisionData"/>):
    /// the templates placed in any playfield's dynels plus the dungeon door styles. A template's mesh (stat 12) carries the
    /// torso sphere the client gives the dynel (n3VisualDynel_t::UpdateCollision); scale is the template's Scale stat.
    /// The player sphere is the solitus_male.cir body's (all breed bodies share it).
    /// </summary>
    internal sealed class DynelCollisionExporter
    {
        const int MeshStat = 12;
        const int ScaleStat = 360;
        const int DoorType = 0xC748;
        const int MissionEntranceType = 0xDAC6;
        const string PlayerBodyName = "solitus_male.cir";

        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        readonly RdbController controller;
        readonly string gameDataDirectory;

        internal DynelCollisionExporter(RdbController controller, string gameDataDirectory)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            if (string.IsNullOrWhiteSpace(gameDataDirectory))
                throw new ArgumentException("GameData directory is required.", nameof(gameDataDirectory));
            this.gameDataDirectory = gameDataDirectory;
        }

        internal ExportFileCounts Export(bool overwrite)
        {
            string path = Path.Combine(this.gameDataDirectory, GameDataPaths.DynelCollisionFileName);
            if (!overwrite && File.Exists(path))
                return new ExportFileCounts(0, 1);

            var data = new DynelCollisionData
            {
                SchemaVersion = DynelCollisionData.SupportedSchemaVersion,
                PlayerSphere = this.ReadPlayerSphere(),
                Templates = new Dictionary<string, CollisionSphereData>()
            };

            foreach (int templateId in this.CollectTemplates().OrderBy(id => id))
            {
                if (this.TryReadTemplateSphere(templateId, out CollisionSphereData sphere))
                    data.Templates[templateId.ToString(CultureInfo.InvariantCulture)] = sphere;
            }

            Directory.CreateDirectory(this.gameDataDirectory);
            File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));
            Console.WriteLine(
                "exported " + GameDataPaths.DynelCollisionFileName + " templates=" + data.Templates.Count
                + " player=" + (data.PlayerSphere != null ? "yes" : "no"));
            return new ExportFileCounts(1, 0);
        }

        HashSet<int> CollectTemplates()
        {
            var templates = new HashSet<int>();
            if (this.controller.RecordTypeToId.TryGetValue((int)ResourceTypeId.PlayfieldDynels, out var ids))
            {
                foreach (int id in ids.Keys)
                {
                    PlayfieldDynels dynels;
                    try
                    {
                        dynels = this.controller.Get<PlayfieldDynels>(ResourceTypeId.PlayfieldDynels, id);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (dynels?.Dynels == null)
                        continue;

                    foreach (PlayfieldDynel dynel in dynels.Dynels)
                    {
                        if (dynel.IdentityType is DoorType or MissionEntranceType && dynel.TemplateId > 0)
                            templates.Add(dynel.TemplateId);
                    }
                }
            }

            // Generated mission dungeons place these door templates (DungeonDoorStyles.json DoorTemplate/ExitTemplate).
            string styles = Path.Combine(this.gameDataDirectory, "DungeonDoorStyles.json");
            if (File.Exists(styles))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(styles));
                CollectDoorStyleTemplates(document.RootElement, templates);
            }

            return templates;
        }

        static void CollectDoorStyleTemplates(JsonElement element, HashSet<int> into)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.Name is "DoorTemplate" or "ExitTemplate"
                        && property.Value.ValueKind == JsonValueKind.Number
                        && property.Value.TryGetInt32(out int id) && id > 0)
                        into.Add(id);
                    else
                        CollectDoorStyleTemplates(property.Value, into);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                    CollectDoorStyleTemplates(item, into);
            }
        }

        bool TryReadTemplateSphere(int templateId, out CollisionSphereData sphere)
        {
            sphere = null;
            ItemObject item;
            try
            {
                item = this.controller.Get(1000020, templateId) as ItemObject;
            }
            catch (Exception)
            {
                return false;
            }

            if (item?.Stats == null)
                return false;

            int mesh = 0;
            int scale = 100;
            foreach (var stat in item.Stats)
            {
                if ((int)stat.Key == MeshStat)
                    mesh = (int)stat.Value;
                else if ((int)stat.Key == ScaleStat && stat.Value > 0)
                    scale = (int)stat.Value;
            }

            if (mesh <= 0)
                return false;

            if (!this.TryReadPackedTorso((int)ResourceTypeId.RdbMesh, mesh, MeshPackedTorsoOffset, out int packed))
                return false;

            DecodeTorsoSphere(packed, out float radius, out float centerY);
            if (radius <= 0f)
                return false;

            float factor = scale / 100f;
            sphere = new CollisionSphereData { Radius = radius * factor, CenterY = centerY * factor };
            return true;
        }

        /// <summary>
        /// A mesh's packed torso sphere: bytes (high to low) radius, z, y, x, each a signed byte v standing for
        /// 0.05 v + 0.000006 v^3 (matches AODB 1.0.12's decoded values exactly, e.g. 47 -> 2.972938, 64 -> 4.772864).
        /// </summary>
        static void DecodeTorsoSphere(int packed, out float radius, out float centerY)
        {
            radius = Unpack((sbyte)(packed >> 24));
            centerY = Unpack((sbyte)(packed >> 8));
        }

        static float Unpack(sbyte value) => (float)((0.05 * value) + (0.000006 * value * value * value));

        // The AODB package this tool builds against does not decode the torso sphere, so it is read from the raw record.
        // RDBMesh: header int at 16 counts named values that precede it; with none, the packed int sits at 20 (checked
        // against AODB 1.0.12 on 3000 meshes: every mesh with named values reports no sphere there). solitus_male.cir's
        // CatMesh record carries it at 268 (checked against AODB 1.0.12 for each breed body).
        const int MeshPackedTorsoOffset = 20;
        const int MeshNamedValueCountOffset = 16;
        const int CatMeshPackedTorsoOffset = 268;

        bool TryReadPackedTorso(int recordType, int recordId, int offset, out int packed)
        {
            packed = 0;
            byte[] raw;
            try
            {
                raw = this.controller.GetRaw(recordType, recordId);
            }
            catch (Exception)
            {
                return false;
            }

            if (raw == null || raw.Length < offset + 4)
                return false;
            if (recordType == (int)ResourceTypeId.RdbMesh && BitConverter.ToInt32(raw, MeshNamedValueCountOffset) != 0)
                return false;

            packed = BitConverter.ToInt32(raw, offset);
            return packed != 0;
        }

        CollisionSphereData ReadPlayerSphere()
        {
            if (!(this.controller.Get(1000010, 1) is InfoObject names))
                return null;

            foreach (var type in names.Types)
            {
                if (type.Key != ResourceTypeId.CatMesh)
                    continue;

                foreach (var entry in type.Value)
                {
                    if (!string.Equals(entry.Value, PlayerBodyName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!this.TryReadPackedTorso((int)ResourceTypeId.CatMesh, entry.Key, CatMeshPackedTorsoOffset, out int packed))
                        return null;

                    DecodeTorsoSphere(packed, out float radius, out float centerY);
                    return radius > 0f ? new CollisionSphereData { Radius = radius, CenterY = centerY } : null;
                }
            }

            return null;
        }
    }
}
