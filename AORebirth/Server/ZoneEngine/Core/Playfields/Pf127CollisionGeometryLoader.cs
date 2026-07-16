namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Web.Script.Serialization;

    internal sealed class PlayfieldCollisionGeometryLoadResult
    {
        private PlayfieldCollisionGeometryLoadResult(
            PlayfieldCollisionGeometry geometry,
            string error)
        {
            this.Geometry = geometry;
            this.Error = error ?? string.Empty;
        }

        internal PlayfieldCollisionGeometry Geometry { get; private set; }

        internal string Error { get; private set; }

        internal bool IsLoaded
        {
            get
            {
                return this.Geometry != null && string.IsNullOrEmpty(this.Error);
            }
        }

        internal static PlayfieldCollisionGeometryLoadResult Loaded(
            PlayfieldCollisionGeometry geometry)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException("geometry");
            }

            return new PlayfieldCollisionGeometryLoadResult(geometry, string.Empty);
        }

        internal static PlayfieldCollisionGeometryLoadResult Failed(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                throw new ArgumentException("A collision geometry load error is required.", "error");
            }

            return new PlayfieldCollisionGeometryLoadResult(null, error);
        }
    }

    internal static class Pf127CollisionGeometryLoader
    {
        internal const int SubwayPlayfieldResource = 127;

        internal const string RelativePath = @"Content\Captured\Subway\pf127-geometry.json";

        private const int MaximumTriangleCount = 2000000;

        private static readonly Lazy<PlayfieldCollisionGeometryLoadResult> CurrentGeometry =
            new Lazy<PlayfieldCollisionGeometryLoadResult>(LoadDefaultPath, true);

        internal static string DefaultPath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativePath);
            }
        }

        internal static PlayfieldCollisionGeometryLoadResult Current
        {
            get
            {
                return CurrentGeometry.Value;
            }
        }

        internal static PlayfieldCollisionGeometryLoadResult LoadPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return PlayfieldCollisionGeometryLoadResult.Failed(
                    "PF127 collision geometry path is missing.");
            }

            if (!File.Exists(path))
            {
                return PlayfieldCollisionGeometryLoadResult.Failed(
                    "PF127 collision geometry file was not found: " + path);
            }

            try
            {
                return LoadJson(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                return PlayfieldCollisionGeometryLoadResult.Failed(
                    "PF127 collision geometry read failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message);
            }
        }

        internal static PlayfieldCollisionGeometryLoadResult LoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return PlayfieldCollisionGeometryLoadResult.Failed(
                    "PF127 collision geometry JSON is empty.");
            }

            Pf127GeometryDocumentDto document;
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                document = serializer.Deserialize<Pf127GeometryDocumentDto>(json);
            }
            catch (Exception exception)
            {
                return PlayfieldCollisionGeometryLoadResult.Failed(
                    "PF127 collision geometry JSON parse failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message);
            }

            if (document == null)
            {
                return PlayfieldCollisionGeometryLoadResult.Failed(
                    "PF127 collision geometry JSON did not contain a document.");
            }

            if (document.SchemaVersion != PlayfieldCollisionGeometry.SupportedSchemaVersion)
            {
                return PlayfieldCollisionGeometryLoadResult.Failed(
                    "PF127 collision geometry schemaVersion must be "
                    + PlayfieldCollisionGeometry.SupportedSchemaVersion
                    + ".");
            }

            if (document.PlayfieldResource != SubwayPlayfieldResource)
            {
                return PlayfieldCollisionGeometryLoadResult.Failed(
                    "PF127 collision geometry playfieldResource must be 127.");
            }

            if (!IsSha256(document.SourceSha256))
            {
                return PlayfieldCollisionGeometryLoadResult.Failed(
                    "PF127 collision geometry sourceSha256 must contain exactly 64 hexadecimal characters.");
            }

            if (!document.DamageLineOfSightProbeHeight.HasValue
                || double.IsNaN(document.DamageLineOfSightProbeHeight.Value)
                || double.IsInfinity(document.DamageLineOfSightProbeHeight.Value)
                || document.DamageLineOfSightProbeHeight.Value < 0.0
                || document.DamageLineOfSightProbeHeight.Value
                   > PlayfieldCollisionGeometry.MaximumDamageLineOfSightProbeHeight)
            {
                return PlayfieldCollisionGeometryLoadResult.Failed(
                    "PF127 collision geometry damageLineOfSightProbeHeight must be finite and between 0 and 10.");
            }

            if (string.IsNullOrWhiteSpace(document.DamageLineOfSightProbeHeightEvidence))
            {
                return PlayfieldCollisionGeometryLoadResult.Failed(
                    "PF127 collision geometry damageLineOfSightProbeHeightEvidence is required.");
            }

            if (document.Triangles == null || document.Triangles.Length == 0)
            {
                return PlayfieldCollisionGeometryLoadResult.Failed(
                    "PF127 collision geometry must contain triangles.");
            }

            if (document.Triangles.Length > MaximumTriangleCount)
            {
                return PlayfieldCollisionGeometryLoadResult.Failed(
                    "PF127 collision geometry exceeds the supported triangle limit.");
            }

            var triangles = new List<CollisionTriangle>(document.Triangles.Length);
            for (int index = 0; index < document.Triangles.Length; index++)
            {
                Pf127GeometryTriangleDto triangle = document.Triangles[index];
                string validationError;
                CollisionTriangle converted;
                if (!TryConvertTriangle(triangle, index, out converted, out validationError))
                {
                    return PlayfieldCollisionGeometryLoadResult.Failed(validationError);
                }

                triangles.Add(converted);
            }

            try
            {
                return PlayfieldCollisionGeometryLoadResult.Loaded(
                    new PlayfieldCollisionGeometry(
                        document.SchemaVersion.Value,
                        document.PlayfieldResource.Value,
                        document.Source,
                        document.SourceSha256,
                        document.DamageLineOfSightProbeHeight.Value,
                        document.DamageLineOfSightProbeHeightEvidence,
                        triangles));
            }
            catch (Exception exception)
            {
                return PlayfieldCollisionGeometryLoadResult.Failed(
                    "PF127 collision geometry validation failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message);
            }
        }

        private static PlayfieldCollisionGeometryLoadResult LoadDefaultPath()
        {
            return LoadPath(DefaultPath);
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool isHexadecimal = (character >= '0' && character <= '9')
                                     || (character >= 'a' && character <= 'f')
                                     || (character >= 'A' && character <= 'F');
                if (!isHexadecimal)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryConvertTriangle(
            Pf127GeometryTriangleDto triangle,
            int index,
            out CollisionTriangle converted,
            out string error)
        {
            converted = default(CollisionTriangle);
            if (triangle == null || !triangle.Id.HasValue)
            {
                error = "PF127 collision geometry triangle[" + index + "] is missing id.";
                return false;
            }

            CollisionPoint3 a;
            CollisionPoint3 b;
            CollisionPoint3 c;
            if (!TryConvertPoint(triangle.A, out a)
                || !TryConvertPoint(triangle.B, out b)
                || !TryConvertPoint(triangle.C, out c))
            {
                error = "PF127 collision geometry triangle[" + index + "] has missing or nonfinite coordinates.";
                return false;
            }

            try
            {
                converted = new CollisionTriangle(triangle.Id.Value, a, b, c);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "PF127 collision geometry triangle["
                        + index
                        + "] is invalid: "
                        + exception.Message;
                return false;
            }
        }

        private static bool TryConvertPoint(Pf127GeometryPointDto point, out CollisionPoint3 converted)
        {
            if (point == null
                || !point.X.HasValue
                || !point.Y.HasValue
                || !point.Z.HasValue)
            {
                converted = default(CollisionPoint3);
                return false;
            }

            converted = new CollisionPoint3(point.X.Value, point.Y.Value, point.Z.Value);
            return converted.IsFinite;
        }
    }

    internal sealed class Pf127GeometryDocumentDto
    {
        public int? SchemaVersion { get; set; }

        public int? PlayfieldResource { get; set; }

        public string Source { get; set; }

        public string SourceSha256 { get; set; }

        public double? DamageLineOfSightProbeHeight { get; set; }

        public string DamageLineOfSightProbeHeightEvidence { get; set; }

        public Pf127GeometryTriangleDto[] Triangles { get; set; }
    }

    internal sealed class Pf127GeometryTriangleDto
    {
        public int? Id { get; set; }

        public Pf127GeometryPointDto A { get; set; }

        public Pf127GeometryPointDto B { get; set; }

        public Pf127GeometryPointDto C { get; set; }
    }

    internal sealed class Pf127GeometryPointDto
    {
        public double? X { get; set; }

        public double? Y { get; set; }

        public double? Z { get; set; }
    }
}
