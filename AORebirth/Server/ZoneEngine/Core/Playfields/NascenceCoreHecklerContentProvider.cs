namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    #endregion

    /// <summary>
    /// Capture-backed Heckler spawns for Nascence Core (PF 4312).
    /// Evidence: tools-temp/AOSharpLiveCapture/.../captures/20260716-071407
    /// </summary>
    internal static class NascenceCoreHecklerContentProvider
    {
        internal const int PlayfieldInstance = 4312;
        internal const int MonsterData = 214982;
        internal const int NpcFamily = 171;
        internal const int MonsterScale = 100;
        internal const int VisualFlags = 31;
        internal const double RespawnDelaySeconds = 600.0;
        internal const string CaptureId = "20260716-071407";
        internal const string TemplateHash = "BART";

        // Combat from fought Heckler of Earth 796C7244
        internal const int MinDamage = 106;
        internal const int MaxDamage = 320;
        internal const int CritDamage = 411;
        internal const double RechargeSeconds = 2.0;
        internal const int SpecialAttackWeaponUnknown = 480;
        internal const int PrimaryWeaponInstance = 1145132106; // DATJ

        private static readonly NascenceCoreHecklerSpawnDefinition[] Spawns =
            new NascenceCoreHecklerSpawnDefinition[]
            {
                new NascenceCoreHecklerSpawnDefinition(0x795FF1DE, "Heckler of Stones", 80, 1860.230710f, 28.014370f, 1739.636350f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x795FF1E8, "Heckler of Metals", 80, 1689.650270f, 28.500452f, 1785.053470f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x795FF1EB, "Heckler of Earth", 80, 1735.569000f, 28.400167f, 1785.246830f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x795FF1EC, "Heckler of Earth", 80, 1737.941770f, 29.018846f, 1767.942870f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x795FF1ED, "Heckler of Earth", 80, 1767.924000f, 29.237999f, 1769.720210f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x795FF1EE, "Heckler of Earth", 80, 1811.703370f, 28.546362f, 1756.460000f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x795FF1EF, "Heckler of Earth", 80, 1817.529660f, 28.210001f, 1726.774000f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x795FF1F6, "Heckler of Stones", 80, 1837.707000f, 28.210001f, 1718.971190f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x795FF1F7, "Heckler of Stones", 80, 1854.014400f, 28.210001f, 1700.105220f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x795FF1F8, "Heckler of Stones", 80, 1873.832760f, 28.087164f, 1713.660520f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x795FF1F9, "Heckler of Stones", 80, 1867.353640f, 28.352736f, 1674.888060f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x795FF1FF, "Heckler of Stones", 80, 1892.393800f, 27.870250f, 1647.228880f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x795FF201, "Heckler of Metals", 80, 1704.097780f, 28.336145f, 1765.592160f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C71BC, "Heckler of Earth", 80, 1525.801760f, 28.280605f, 1785.636000f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C71C0, "Heckler of Earth", 80, 1579.792720f, 29.258543f, 1792.423100f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C71CD, "Heckler of Metals", 80, 1630.561770f, 28.376963f, 1772.508540f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C71D2, "Heckler of Metals", 80, 1656.167600f, 28.245451f, 1790.837650f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C71D3, "Heckler of Metals", 80, 1672.095580f, 28.715395f, 1770.736000f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C7244, "Heckler of Earth", 80, 1727.780640f, 39.482180f, 1419.806760f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C7249, "Heckler of Earth", 80, 1747.175170f, 36.476486f, 1429.386720f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C724A, "Heckler of Earth", 80, 1747.323360f, 36.269524f, 1441.886000f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C724F, "Heckler of Earth", 80, 1762.310300f, 34.871925f, 1429.782230f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C7252, "Heckler of Earth", 80, 1766.768310f, 34.301533f, 1436.648190f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C725E, "Heckler of Earth", 80, 1773.088500f, 34.032715f, 1426.386110f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C7268, "Heckler of Earth", 80, 1778.393550f, 32.189480f, 1450.752440f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C726B, "Heckler of Earth", 80, 1785.762450f, 32.032110f, 1433.132000f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C7276, "Heckler of Earth", 80, 1800.257690f, 29.263454f, 1454.294000f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C727D, "Heckler of Earth", 80, 1817.378910f, 28.347641f, 1459.829830f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C7286, "Heckler of Earth", 80, 1800.027830f, 28.401964f, 1475.127930f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C728F, "Heckler of Earth", 80, 1839.766360f, 28.021572f, 1503.181880f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C7297, "Heckler of Earth", 80, 1848.904000f, 28.068722f, 1489.493000f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C72A2, "Heckler of Earth", 80, 1842.180180f, 28.289026f, 1527.448240f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C72A8, "Heckler of Earth", 80, 1860.352660f, 28.336730f, 1510.896610f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C72BF, "Heckler of Metals", 80, 1858.572750f, 28.600939f, 1538.487180f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C72CA, "Heckler of Metals", 80, 1869.915280f, 28.117410f, 1559.359250f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C72D2, "Heckler of Metals", 80, 1890.564580f, 28.181831f, 1563.345700f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C72D5, "Heckler of Metals", 80, 1874.818850f, 28.898687f, 1580.456180f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C72EB, "Heckler of Metals", 80, 1870.383420f, 28.542110f, 1600.365230f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C72F3, "Heckler of Metals", 80, 1888.735720f, 28.318750f, 1607.700440f, 5733, 285),
                new NascenceCoreHecklerSpawnDefinition(0x796C72FB, "Heckler of Stones", 80, 1871.625610f, 28.453390f, 1627.991000f, 5733, 285),
            };

        internal static NascenceCoreHecklerSpawnDefinition[] GetSpawns()
        {
            return (NascenceCoreHecklerSpawnDefinition[])Spawns.Clone();
        }
    }

    internal sealed class NascenceCoreHecklerSpawnDefinition
    {
        internal NascenceCoreHecklerSpawnDefinition(
            int sourceIdentity,
            string name,
            int level,
            float x,
            float y,
            float z,
            int health,
            int runSpeed)
        {
            this.SourceIdentity = sourceIdentity;
            this.Name = name;
            this.Level = level;
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.Health = health;
            this.RunSpeed = runSpeed;
        }

        internal int SourceIdentity { get; private set; }
        internal string Name { get; private set; }
        internal int Level { get; private set; }
        internal float X { get; private set; }
        internal float Y { get; private set; }
        internal float Z { get; private set; }
        internal int Health { get; private set; }
        internal int RunSpeed { get; private set; }
    }
}
