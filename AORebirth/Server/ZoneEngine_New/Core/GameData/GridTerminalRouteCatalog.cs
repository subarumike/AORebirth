namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    using Vector3 = AORebirth.Core.Vector.Vector3;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    /// <summary>
    /// Capture-backed Enter The Grid landings from <c>GameData/GridTerminalRoutes.json</c>.
    /// Raw TeleportProxy2 packs every enter to the same PF152 door index; live captures need
    /// per-terminal landings (see <c>GridTerminalRoutes.json</c>).
    /// When <see cref="CapturedGridEnterLanding.ExitTerminalInstance"/> is set, runtime resolves
    /// the pad from PF Dynels (correct floor Y) instead of the JSON xyz fallback.
    /// </summary>
    public sealed class GridTerminalRouteCatalog
    {
        public const string FileName = "GridTerminalRoutes.json";

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        readonly Dictionary<(int Playfield, int Instance), CapturedGridEnterLanding> _routes;

        GridTerminalRouteCatalog(Dictionary<(int, int), CapturedGridEnterLanding> routes)
        {
            _routes = routes;
        }

        public int Count => _routes.Count;

        public static GridTerminalRouteCatalog Empty { get; } =
            new(new Dictionary<(int, int), CapturedGridEnterLanding>());

        public static GridTerminalRouteCatalog Load(string gameDataRoot)
        {
            ArgumentException.ThrowIfNullOrEmpty(gameDataRoot);
            string path = Path.Combine(gameDataRoot, FileName);
            if (!File.Exists(path))
                return Empty;

            GridTerminalRoutesDocument? document =
                JsonSerializer.Deserialize<GridTerminalRoutesDocument>(File.ReadAllText(path), JsonOptions);
            if (document?.Routes == null || document.Routes.Count == 0)
                return Empty;

            var routes = new Dictionary<(int, int), CapturedGridEnterLanding>();
            for (int i = 0; i < document.Routes.Count; i++)
            {
                GridTerminalRouteRow row = document.Routes[i];
                if (row.SourcePlayfieldId <= 0
                    || row.DestinationPlayfieldId <= 0
                    || !TryParseInstance(row.SourceTerminalInstance, out int sourceInstance))
                    continue;

                TryParseInstance(row.DestinationExitTerminalInstance, out int exitInstance);

                routes[(row.SourcePlayfieldId, sourceInstance)] = new CapturedGridEnterLanding(
                    row.DestinationPlayfieldId,
                    exitInstance,
                    new Vector3(row.X, row.Y, row.Z),
                    new Quaternion(row.Qx, row.Qy, row.Qz, row.Qw));
            }

            return new GridTerminalRouteCatalog(routes);
        }

        public bool TryGet(
            int sourcePlayfieldId,
            int sourceTerminalInstance,
            out CapturedGridEnterLanding landing)
            => _routes.TryGetValue((sourcePlayfieldId, sourceTerminalInstance), out landing);

        static bool TryParseInstance(string? text, out int instance)
        {
            instance = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string hex = text.Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex[2..];

            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
                return false;

            instance = unchecked((int)value);
            return true;
        }

        sealed class GridTerminalRoutesDocument
        {
            [JsonPropertyName("routes")]
            public List<GridTerminalRouteRow>? Routes { get; set; }
        }

        sealed class GridTerminalRouteRow
        {
            public int SourcePlayfieldId { get; set; }

            public string? SourceTerminalInstance { get; set; }

            public int DestinationPlayfieldId { get; set; }

            public string? DestinationExitTerminalInstance { get; set; }

            public float X { get; set; }

            public float Y { get; set; }

            public float Z { get; set; }

            public float Qx { get; set; }

            public float Qy { get; set; }

            public float Qz { get; set; }

            public float Qw { get; set; } = 1f;
        }
    }

    public readonly struct CapturedGridEnterLanding
    {
        public CapturedGridEnterLanding(
            int playfieldId,
            int exitTerminalInstance,
            Vector3 positionFallback,
            Quaternion headingFallback)
        {
            PlayfieldId = playfieldId;
            ExitTerminalInstance = exitTerminalInstance;
            PositionFallback = positionFallback;
            HeadingFallback = headingFallback;
        }

        public int PlayfieldId { get; }

        /// <summary>PF152 exit pad instance used to resolve floor Y / clearance landing.</summary>
        public int ExitTerminalInstance { get; }

        public Vector3 PositionFallback { get; }

        public Quaternion HeadingFallback { get; }
    }
}
