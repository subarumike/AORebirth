using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Xml.Linq;

using AORebirth.Core.Exceptions;
using AORebirth.Database;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using AORebirth.Stats.SpecialStats;

using Dapper;

using SmokeLounge.AOtomation.Messaging.GameData;

using LinqBinary = System.Data.Linq.Binary;

internal static class Program
{
    private static int checks;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && string.Equals(args[0], "verify-artifacts", StringComparison.Ordinal))
            {
                if (args.Length != 6)
                {
                    throw new ArgumentException(
                        "verify-artifacts requires source SQL, build SQL, publish SQL, Database project, and content manifest paths.");
                }

                VerifySqlArtifacts(args[1], args[2], args[3], args[5]);
                VerifyPinnedDatabasePackages(args[4]);
                Console.WriteLine("Stage 3 artifact parity: PASS ({0} checks)", checks);
                return 0;
            }

            if (args.Length != 0)
            {
                throw new ArgumentException("Unknown Stage 3 offline smoke mode: " + args[0]);
            }

            Run("System.Data.Linq.Binary legacy contract", VerifyBinaryContract);
            Run("Dapper Binary parameter binding", VerifyDapperParameterBinding);
            Run("Dapper Binary reader materialization", VerifyDapperReaderMaterialization);
            Run("provider connector leaf construction", VerifyProviderConnectorConstruction);
            Run("SqlMapperUtil offline behavior", VerifySqlMapperUtil);
            Run("Stats offline tables and behavior", VerifyStatsOfflineBehavior);

            Console.WriteLine("Stage 3 offline smoke: PASS ({0} checks)", checks);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Stage 3 offline smoke: FAIL");
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static void Run(string name, Action test)
    {
        test();
        Console.WriteLine("PASS: " + name);
    }

    private static void VerifyBinaryContract()
    {
        byte[] source = { 0x00, 0x01, 0x7f, 0x80, 0xfe, 0xff };
        byte[] original = (byte[])source.Clone();
        LinqBinary value = new LinqBinary(source);

        Equal(original.Length, value.Length, "Binary Length");
        SequenceEqual(original, value.ToArray(), "Binary constructor bytes");

        source[0] = 0xff;
        SequenceEqual(original, value.ToArray(), "Binary constructor defensive copy");

        byte[] firstCopy = value.ToArray();
        firstCopy[1] = 0xff;
        SequenceEqual(original, value.ToArray(), "Binary ToArray defensive copy");

        LinqBinary equal = new LinqBinary(original);
        LinqBinary different = new LinqBinary(new byte[] { 0x00, 0x01, 0x7f });
        True(value.Equals(equal), "Binary typed equality");
        True(value.Equals((object)equal), "Binary object equality");
        False(value.Equals(different), "Binary unequal length/content");
        False(value.Equals((LinqBinary)null), "Binary typed null equality");
        True(value == equal, "Binary equality operator");
        False(value != equal, "Binary inequality operator equal values");
        True(value != different, "Binary inequality operator different values");

        LinqBinary nullLeft = null;
        LinqBinary nullRight = null;
        True(nullLeft == nullRight, "Binary null equality operator");
        False(value == nullLeft, "Binary value/null equality operator");

        LinqBinary implicitValue = original;
        SequenceEqual(original, implicitValue.ToArray(), "Binary implicit byte[] conversion");

        LinqBinary nullConstructor = new LinqBinary(null);
        LinqBinary nullImplicit = (byte[])null;
        Equal(0, nullConstructor.Length, "Binary null constructor becomes empty");
        Equal(0, nullImplicit.Length, "Binary null implicit input becomes empty");
        Equal("\"\"", nullConstructor.ToString(), "Binary empty quoted Base64");

        string expectedString = "\"" + Convert.ToBase64String(original) + "\"";
        Equal(expectedString, value.ToString(), "Binary quoted Base64 string");

        int expectedHash = ComputeLegacyBinaryHash(original);
        Equal(expectedHash, value.GetHashCode(), "Binary reference-source hash");
        Equal(expectedHash, value.GetHashCode(), "Binary cached hash stability");
        Equal(expectedHash, equal.GetHashCode(), "Binary equal hash");
        Equal(0, nullConstructor.GetHashCode(), "Binary empty hash");

        True(typeof(IEquatable<LinqBinary>).IsAssignableFrom(typeof(LinqBinary)), "Binary IEquatable contract");
        True(
            Attribute.IsDefined(typeof(LinqBinary), typeof(SerializableAttribute), false),
            "Binary Serializable contract");
        True(
            Attribute.IsDefined(typeof(LinqBinary), typeof(DataContractAttribute), false),
            "Binary DataContract contract");

        FieldInfo hashField = typeof(LinqBinary).GetField("hashCode", BindingFlags.Instance | BindingFlags.NonPublic);
        NotNull(hashField, "Binary cached hash field");
        False(
            Attribute.IsDefined(hashField, typeof(NonSerializedAttribute), false),
            "Binary hash field remains normally serializable");
        Equal(expectedHash, (int)hashField.GetValue(value), "Binary constructor computes cached hash immediately");

        DataContractSerializer serializer = new DataContractSerializer(typeof(LinqBinary));
        using (MemoryStream stream = new MemoryStream())
        {
            serializer.WriteObject(stream, value);
            stream.Position = 0;
            LinqBinary roundTrip = (LinqBinary)serializer.ReadObject(stream);
            True(value == roundTrip, "Binary DataContract round trip equality");
            Equal(expectedHash, roundTrip.GetHashCode(), "Binary DataContract recomputes hash");
        }
    }

    private static int ComputeLegacyBinaryHash(byte[] bytes)
    {
        unchecked
        {
            int s = 314;
            int t = 159;
            int hash = 0;
            for (int index = 0; index < bytes.Length; index++)
            {
                hash = (hash * s) + bytes[index];
                s *= t;
            }

            return hash;
        }
    }

    private static void VerifyDapperParameterBinding()
    {
        byte[] payload = { 0x10, 0x20, 0x30, 0x40 };
        LinqBinary binary = new LinqBinary(payload);
        FakeDbConnection connection = FakeDbConnection.ForNonQuery(1);

        int affected = connection.Execute(
            "UPDATE offline_only SET payload = @Payload",
            new { Payload = binary });

        Equal(1, affected, "Dapper fake Execute result");
        False(connection.OpenCalled, "Dapper did not call Open on the fake connection");
        NotNull(connection.LastCommand, "Dapper created a fake command");
        Equal(
            "UPDATE offline_only SET payload = @Payload",
            connection.LastCommand.CommandText,
            "Dapper command text");

        Equal(1, connection.LastCommand.ExecutedParameters.Count, "Dapper Binary parameter count");
        IDataParameter parameter = connection.LastCommand.ExecutedParameters[0];
        NotNull(parameter, "Dapper Binary parameter");
        Equal(
            "Payload",
            parameter.ParameterName.TrimStart('@', ':', '?'),
            "Dapper Binary parameter name");
        True(parameter.Value is byte[], "Dapper converts Binary parameter to byte[]");
        SequenceEqual(payload, (byte[])parameter.Value, "Dapper Binary parameter bytes");
        False(parameter.Value is LinqBinary, "Dapper does not send the compatibility object to the provider");
    }

    private static void VerifyDapperReaderMaterialization()
    {
        byte[] payload = { 0xaa, 0xbb, 0xcc, 0xdd };
        using (DataTable table = new DataTable("offline_binary_rows"))
        {
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("stats", typeof(byte[]));
            table.Rows.Add(7, payload);
            table.Rows.Add(8, DBNull.Value);

            FakeDbConnection connection = FakeDbConnection.ForReader(
                delegate { return table.CreateDataReader(); });
            List<DBStaticDynel> rows = connection.Query<DBStaticDynel>(
                "SELECT Id, stats FROM offline_binary_rows ORDER BY Id").ToList();

            Equal(2, rows.Count, "Dapper fake reader row count");
            Equal(7, rows[0].Id, "Dapper fake reader first identity");
            NotNull(rows[0].stats, "Dapper materializes byte[] into the real Binary entity property");
            SequenceEqual(payload, rows[0].stats.ToArray(), "Dapper materialized Binary bytes");
            Equal(8, rows[1].Id, "Dapper fake reader second identity");
            Null(rows[1].stats, "Dapper maps DBNull to null Binary");
            False(connection.OpenCalled, "Dapper reader did not call Open on the fake connection");
        }
    }

    private static void VerifyProviderConnectorConstruction()
    {
        VerifyConnector(
            "MySQL",
            "MySqlConnector.MySqlConnection",
            new MySQLConnector(),
            new MySQLConnector("Server=127.0.0.1;Port=3306;Database=aorebirth;User ID=offline;"));
        VerifyConnector(
            "PostgreSQL",
            "Npgsql.NpgsqlConnection",
            new NpgsqlConnector(),
            new NpgsqlConnector("Host=127.0.0.1;Port=5432;Database=aorebirth;Username=offline"));
        VerifyConnector(
            "MSSQL",
            "Microsoft.Data.SqlClient.SqlConnection",
            new MSSqlConnector(),
            new MSSqlConnector(
                "Server=127.0.0.1;Database=aorebirth;Integrated Security=True;TrustServerCertificate=True"));
    }

    private static void VerifyConnector(
        string label,
        string expectedConnectionType,
        IDatabaseConnector defaultConnector,
        IDatabaseConnector configuredConnector)
    {
        Equal(string.Empty, defaultConnector.ConnectionString, label + " default connection string");
        NullReferenceException emptyError = Throws<NullReferenceException>(
            delegate { defaultConnector.GetConnection(); },
            label + " empty connector rejection");
        Equal("Connection string can not be empty", emptyError.Message, label + " empty connector message");

        string configuredString = configuredConnector.ConnectionString;
        using (IDbConnection connection = configuredConnector.GetConnection())
        {
            Equal(expectedConnectionType, connection.GetType().FullName, label + " concrete connection type");
            Equal(ConnectionState.Closed, connection.State, label + " connection remains closed");
            Equal(configuredString, connection.ConnectionString, label + " connection string preservation");
        }
    }

    private static void VerifySqlMapperUtil()
    {
        SqlShape shape = new SqlShape { Id = 17, Name = "alpha", ParentId = 99 };

        DynamicParameters withoutForeignKeys = SqlMapperUtil.GetParametersFromObject(shape, null, true);
        SequenceEqual(
            new[] { "Id", "Name", "ReadOnly" },
            withoutForeignKeys.ParameterNames.ToArray(),
            "SqlMapperUtil removes ForeignKey properties");

        DynamicParameters withForeignKeys = SqlMapperUtil.GetParametersFromObject(shape, null, false);
        SequenceEqual(
            new[] { "Id", "Name", "ParentId", "ReadOnly" },
            withForeignKeys.ParameterNames.ToArray(),
            "SqlMapperUtil retains ForeignKey properties");

        DynamicParameters ignored = SqlMapperUtil.GetParametersFromObject(shape, new[] { "Name" }, false);
        SequenceEqual(
            new[] { "Id", "ParentId", "ReadOnly" },
            ignored.ParameterNames.ToArray(),
            "SqlMapperUtil explicit ignored properties");

        Equal("alpha", (string)SqlMapperUtil.GetPropertyValue(shape, "nAmE"), "SqlMapperUtil case-insensitive get");
        SqlMapperUtil.SetPropertyValue(shape, "Name", "beta");
        Equal("beta", shape.Name, "SqlMapperUtil writable property set");
        SqlMapperUtil.SetPropertyValue(shape, "name", "not-applied");
        Equal("beta", shape.Name, "SqlMapperUtil set preserves exact property-name behavior");
        SqlMapperUtil.SetPropertyValue(shape, "Missing", "not-applied");
        Equal("beta", shape.Name, "SqlMapperUtil missing property is a no-op");
        SqlMapperUtil.SetPropertyValue(shape, "ReadOnly", "not-applied");
        Equal("readonly", shape.ReadOnly, "SqlMapperUtil read-only property is a no-op");

        Equal(
            "UPDATE widget SET Name = @Name,ReadOnly = @ReadOnly WHERE id=@id ",
            SqlMapperUtil.CreateUpdateSQL("widget", shape),
            "SqlMapperUtil update SQL");
        Equal(
            "INSERT INTO widget ( Name,ParentId,ReadOnly ) VALUES ( @Name,@ParentId,@ReadOnly ) ",
            SqlMapperUtil.CreateInsertSQL("widget", shape),
            "SqlMapperUtil insert SQL without Id");
        Equal(
            "INSERT INTO widget ( Id,Name,ParentId,ReadOnly ) VALUES ( @Id,@Name,@ParentId,@ReadOnly ) ",
            SqlMapperUtil.CreateInsertSQL("widget", shape, false),
            "SqlMapperUtil insert SQL with Id");
        Equal(
            "DELETE FROM widget WHERE Id = @Id ",
            SqlMapperUtil.CreateDeleteSQL("widget"),
            "SqlMapperUtil default delete SQL");
        Equal(
            "DELETE FROM widget WHERE  ( Id = @Id ) AND ( Name = @Name ) ",
            SqlMapperUtil.CreateDeleteSQL("widget", new { Id = 17, Name = "beta" }),
            "SqlMapperUtil filtered delete SQL");
        Equal("SELECT * FROM widget", SqlMapperUtil.CreateGetSQL("widget"), "SqlMapperUtil unfiltered get SQL");
        Equal(
            "SELECT * FROM widget WHERE  ( Id = @Id ) AND ( Name = @Name ) ",
            SqlMapperUtil.CreateGetSQL("widget", new { Id = 17, Name = "beta" }),
            "SqlMapperUtil filtered get SQL");
        Equal(
            "SELECT COUNT(*) FROM widget",
            SqlMapperUtil.CreateCountSQL("widget"),
            "SqlMapperUtil unfiltered count SQL");
        Equal(
            "SELECT COUNT(*) FROM widget WHERE  ( Id = @Id ) AND ( Name = @Name ) ",
            SqlMapperUtil.CreateCountSQL("widget", new { Id = 17, Name = "beta" }),
            "SqlMapperUtil filtered count SQL");

        Throws<ArgumentNullException>(
            delegate { SqlMapperUtil.CreateUpdateSQL("widget", null); },
            "SqlMapperUtil update rejects null parameters");
        Throws<ArgumentNullException>(
            delegate { SqlMapperUtil.CreateInsertSQL("widget", null); },
            "SqlMapperUtil insert rejects null parameters");
    }

    private static void VerifyStatsOfflineBehavior()
    {
        Equal(72, SkillTrickleTable.table.GetLength(0), "SkillTrickleTable row count");
        Equal(7, SkillTrickleTable.table.GetLength(1), "SkillTrickleTable column count");
        Equal(100d, SkillTrickleTable.table[0, 0], "SkillTrickleTable first stat");
        Equal(0.2d, SkillTrickleTable.table[0, 1], "SkillTrickleTable first strength factor");
        Equal(1d, SkillTrickleTable.table[68, 0], "SkillTrickleTable low-id sentinel");
        Equal(364d, SkillTrickleTable.table[71, 0], "SkillTrickleTable final sentinel");

        VerifyTableShape(XPTable.TableAlienXP, 31, 3, "Alien XP");
        Equal(1d, XPTable.TableAlienXP[0, 0], "Alien XP first level");
        Equal(1500d, XPTable.TableAlienXP[0, 1], "Alien XP first total");
        Equal(31d, XPTable.TableAlienXP[30, 0], "Alien XP terminal level");
        Equal(0d, XPTable.TableAlienXP[30, 2], "Alien XP terminal delta");

        VerifyTableShape(XPTable.TableRKXP, 200, 3, "RK XP");
        Equal(1d, XPTable.TableRKXP[0, 0], "RK XP first level");
        Equal(1450d, XPTable.TableRKXP[0, 2], "RK XP first delta");
        Equal(200d, XPTable.TableRKXP[199, 0], "RK XP terminal level");
        Equal(2061453150d, XPTable.TableRKXP[199, 1], "RK XP terminal total");

        Equal(21, XPTable.TableShadowLandsSK.GetLength(0), "Shadowlands SK row count");
        Equal(3, XPTable.TableShadowLandsSK.GetLength(1), "Shadowlands SK column count");
        Equal(200, XPTable.TableShadowLandsSK[0, 0], "Shadowlands SK first level");
        Equal(80000, XPTable.TableShadowLandsSK[0, 2], "Shadowlands SK first delta");
        Equal(220, XPTable.TableShadowLandsSK[20, 0], "Shadowlands SK terminal level");
        Equal(0, XPTable.TableShadowLandsSK[20, 2], "Shadowlands SK terminal delta");

        Equal("level", StatNamesDefaults.GetStatName(54), "Stats id-to-name mapping");
        Equal(54, StatNamesDefaults.GetStatNumber("level"), "Stats lowercase name-to-id mapping");
        Equal(54, StatNamesDefaults.GetStatNumber("LEVEL"), "Stats case-insensitive name-to-id mapping");
        Equal(1, StatNamesDefaults.GetDefault(1), "Stats explicit default");
        Equal(1234567890, StatNamesDefaults.GetDefault(54), "Stats unspecified default sentinel");
        Throws<StatDoesNotExistException>(
            delegate { StatNamesDefaults.GetStatName(-1); },
            "Stats missing id mapping");
        Throws<StatDoesNotExistException>(
            delegate { StatNamesDefaults.GetStatNumber("not-a-real-stat"); },
            "Stats missing name mapping");

        SimpleStatList simpleList = new SimpleStatList();
        IStat numberStat = simpleList[54];
        Same(numberStat, simpleList[54], "SimpleStatList repeated numeric indexer");
        Same(numberStat, simpleList[(StatIds)54], "SimpleStatList enum indexer");
        Same(numberStat, simpleList["Level"], "SimpleStatList string indexer");
        Equal(1, simpleList.All.Count, "SimpleStatList lazy creation count");

        SimpleStat simple = new SimpleStat(9001);
        simple.Set(77);
        Equal(77, simple.Value, "SimpleStat direct set");
        Equal((uint)77, simple.GetMaxValue(77), "SimpleStat max passthrough");
        True(simple.NotDefault(), "SimpleStat legacy NotDefault behavior");

        // Construction and direct index lookup are offline-safe. Do not enumerate Values: Expansion,
        // GMLevel, and Life can reach LoginDataDao. Read/Write/GetStatValues are also forbidden here.
        AORebirth.Stats.Stats fullStats = new AORebirth.Stats.Stats(Identity.None);

        Stat formula = new Stat(fullStats, 9002, 100, true, false, false);
        Equal(100, formula.Value, "Stat initial formula");
        formula.Modifier = 20;
        formula.Trickle = 5;
        formula.PercentageModifier = 50;
        Equal(62, formula.Value, "Stat modifier/trickle/percentage formula boundary");

        formula.Changed = false;
        formula.Set(200);
        True(formula.Changed, "Stat changed flag after a different base value");
        Equal((uint)200, formula.BaseValue, "Stat changed base value");
        formula.Changed = false;
        formula.Set(200);
        False(formula.Changed, "Stat unchanged flag after the same base value");
        formula.Set(300, true);
        False(formula.Changed, "Stat starting value does not mark changed");
        Equal((uint)300, formula.BaseValue, "Stat starting base value");
        True(formula.NotDefault(), "Stat NotDefault boundary");

        FieldInfo allStatsField = typeof(AORebirth.Stats.Stats).GetField(
            "all",
            BindingFlags.Instance | BindingFlags.NonPublic);
        NotNull(allStatsField, "Stats internal list for isolated propagation test");
        List<IStat> allStats = (List<IStat>)allStatsField.GetValue(fullStats);
        TrackingStat affected = new TrackingStat(fullStats, 9004);
        allStats.Add(affected);
        int calculationsBefore = affected.CalculationCount;
        Stat affecting = new Stat(fullStats, 9003, 10, true, false, false);
        affecting.Affects.Add(9004);
        affecting.Set(11);
        Equal(
            calculationsBefore + 1,
            affected.CalculationCount,
            "Stat affected-stat recalculation propagation");
        False(affected.ReCalculate, "Stat affected-stat recalculation is consumed");

        int eventCount = 0;
        object eventSender = null;
        StatChangedEventArgs received = null;
        simpleList.AfterStatChangedEvent += delegate(object sender, StatChangedEventArgs eventArgs)
        {
            eventCount++;
            eventSender = sender;
            received = eventArgs;
        };
        StatChangedEventArgs expectedEvent = new StatChangedEventArgs(formula, 10, 20, true);
        simpleList.AfterStatChangedEventHandler(expectedEvent);
        Equal(1, eventCount, "Stats event dispatch count");
        Same(simpleList, eventSender, "Stats event sender");
        Same(expectedEvent, received, "Stats event args identity");
        Same(formula, received.Stat, "Stats event stat");
        Equal((uint)10, received.OldValue, "Stats event old value");
        Equal((uint)20, received.NewValue, "Stats event new value");
        True(received.AnnounceToPlayfield, "Stats event playfield flag");

        IStat fullLevelByNumber = fullStats[54];
        IStat fullLevelByName = fullStats["Level"];
        Same(fullLevelByNumber, fullLevelByName, "Full Stats number/name indexer parity");
        Equal(54, fullLevelByNumber.StatId, "Full Stats safe indexer stat id");
        Equal(Identity.None, fullStats.Owner, "Full Stats owner");
    }

    private static void VerifyTableShape(double[,] table, int rows, int columns, string name)
    {
        Equal(rows, table.GetLength(0), name + " row count");
        Equal(columns, table.GetLength(1), name + " column count");
    }

    private static void VerifySqlArtifacts(
        string sourceDirectory,
        string buildDirectory,
        string publishDirectory,
        string contentManifestPath)
    {
        Dictionary<string, FileFingerprint> source = FingerprintDeclaredSourceSqlDirectory(
            sourceDirectory,
            contentManifestPath);
        Dictionary<string, FileFingerprint> build = FingerprintSqlDirectory(buildDirectory, "build output");
        Dictionary<string, FileFingerprint> publish = FingerprintSqlDirectory(publishDirectory, "linux-x64 publish output");

        Equal(34, source.Count, "source SQL file count");
        VerifyFingerprintSet(source, build, "build output");
        VerifyFingerprintSet(source, publish, "linux-x64 publish output");
    }

    private static Dictionary<string, FileFingerprint> FingerprintDeclaredSourceSqlDirectory(
        string sourceDirectory,
        string contentManifestPath)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException("source SQL directory is missing: " + sourceDirectory);
        }

        XDocument manifest = XDocument.Parse(File.ReadAllText(contentManifestPath), LoadOptions.None);
        string[] declaredNames = manifest.Descendants("Content")
            .Select(x => (string)x.Attribute("Link"))
            .Select(Path.GetFileName)
            .ToArray();
        Equal(34, declaredNames.Length, "declared SQL content count");
        Equal(
            declaredNames.Length,
            declaredNames.Distinct(StringComparer.Ordinal).Count(),
            "declared case-sensitive SQL name uniqueness");

        Dictionary<string, string> physicalFiles = Directory.GetFiles(
                sourceDirectory,
                "*.sql",
                SearchOption.TopDirectoryOnly)
            .ToDictionary(Path.GetFileName, x => x, StringComparer.Ordinal);
        Dictionary<string, FileFingerprint> result = new Dictionary<string, FileFingerprint>(StringComparer.Ordinal);
        foreach (string name in declaredNames)
        {
            string path;
            if (!physicalFiles.TryGetValue(name, out path))
            {
                throw new FileNotFoundException(
                    "Declared source SQL file is missing or has different casing: " + name,
                    Path.Combine(sourceDirectory, name));
            }

            FileInfo info = new FileInfo(path);
            result.Add(name, new FileFingerprint(info.Length, ComputeSha256(path)));
        }

        return result;
    }

    private static Dictionary<string, FileFingerprint> FingerprintSqlDirectory(string path, string label)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(label + " SQL directory is missing: " + path);
        }

        string[] files = Directory.GetFiles(path, "*.sql", SearchOption.TopDirectoryOnly);
        Dictionary<string, FileFingerprint> result = new Dictionary<string, FileFingerprint>(StringComparer.Ordinal);
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            if (result.ContainsKey(name))
            {
                throw new InvalidOperationException(label + " contains a duplicate case-sensitive SQL name: " + name);
            }

            FileInfo info = new FileInfo(file);
            result.Add(name, new FileFingerprint(info.Length, ComputeSha256(file)));
        }

        return result;
    }

    private static string ComputeSha256(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }

    private static void VerifyFingerprintSet(
        Dictionary<string, FileFingerprint> expected,
        Dictionary<string, FileFingerprint> actual,
        string label)
    {
        Equal(expected.Count, actual.Count, label + " SQL file count");
        string[] expectedNames = expected.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        string[] actualNames = actual.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        SequenceEqual(expectedNames, actualNames, label + " exact SQL names and casing");

        foreach (string name in expectedNames)
        {
            Equal(expected[name].Length, actual[name].Length, label + " SQL length: " + name);
            Equal(expected[name].Sha256, actual[name].Sha256, label + " SQL SHA-256: " + name);
        }
    }

    private static void VerifyPinnedDatabasePackages(string projectPath)
    {
        Dictionary<string, string> expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Dapper", "2.1.79" },
            { "Microsoft.Data.SqlClient", "7.0.2" },
            { "MySqlConnector", "2.6.1" },
            { "Npgsql", "10.0.3" }
        };

        XDocument project = XDocument.Parse(File.ReadAllText(projectPath), LoadOptions.None);
        List<XElement> references = project.Descendants("PackageReference").ToList();
        Equal(expected.Count, references.Count, "Database direct PackageReference count");

        foreach (XElement reference in references)
        {
            XAttribute includeAttribute = reference.Attribute("Include");
            XAttribute versionAttribute = reference.Attribute("Version");
            NotNull(includeAttribute, "Database package Include attribute");
            NotNull(versionAttribute, "Database package pinned Version attribute");

            string expectedVersion;
            if (!expected.TryGetValue(includeAttribute.Value, out expectedVersion))
            {
                throw new InvalidOperationException("Unexpected direct Database package: " + includeAttribute.Value);
            }

            Equal(expectedVersion, versionAttribute.Value, "Database pinned package " + includeAttribute.Value);
        }
    }

    private static TException Throws<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            checks++;
            return ex;
        }

        throw new InvalidOperationException(message + ": expected " + typeof(TException).FullName);
    }

    private static void True(bool condition, string message)
    {
        checks++;
        if (!condition)
        {
            throw new InvalidOperationException(message + ": expected true");
        }
    }

    private static void False(bool condition, string message)
    {
        checks++;
        if (condition)
        {
            throw new InvalidOperationException(message + ": expected false");
        }
    }

    private static void Null(object value, string message)
    {
        checks++;
        if (value != null)
        {
            throw new InvalidOperationException(message + ": expected null");
        }
    }

    private static void NotNull(object value, string message)
    {
        checks++;
        if (value == null)
        {
            throw new InvalidOperationException(message + ": expected a value");
        }
    }

    private static void Same(object expected, object actual, string message)
    {
        checks++;
        if (!ReferenceEquals(expected, actual))
        {
            throw new InvalidOperationException(message + ": expected the same object reference");
        }
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        checks++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                string.Format("{0}: expected '{1}', actual '{2}'", message, expected, actual));
        }
    }

    private static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
    {
        checks++;
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                message + ": expected [" + string.Join(",", expected) + "], actual [" +
                string.Join(",", actual) + "]");
        }
    }

    private sealed class FileFingerprint
    {
        public FileFingerprint(long length, string sha256)
        {
            this.Length = length;
            this.Sha256 = sha256;
        }

        public long Length { get; private set; }

        public string Sha256 { get; private set; }
    }

    private sealed class SqlShape
    {
        public int Id { get; set; }

        public string Name { get; set; }

        [ForeignKey]
        public int ParentId { get; set; }

        public string ReadOnly
        {
            get { return "readonly"; }
        }
    }

    private sealed class TrackingStat : Stat
    {
        public TrackingStat(AORebirth.Stats.Stats stats, int statId)
            : base(stats, statId, 1, true, false, false)
        {
        }

        public int CalculationCount { get; private set; }

        public override int GetValue
        {
            get
            {
                this.CalculationCount++;
                return base.GetValue;
            }
        }
    }

    private sealed class FakeDbConnection : IDbConnection
    {
        private readonly Func<IDataReader> readerFactory;
        private readonly int nonQueryResult;
        private ConnectionState state = ConnectionState.Open;

        private FakeDbConnection(Func<IDataReader> readerFactory, int nonQueryResult)
        {
            this.readerFactory = readerFactory;
            this.nonQueryResult = nonQueryResult;
        }

        public string ConnectionString { get; set; }

        public int ConnectionTimeout { get { return 0; } }

        public string Database { get { return "offline"; } }

        public ConnectionState State { get { return this.state; } }

        public bool OpenCalled { get; private set; }

        public FakeDbCommand LastCommand { get; private set; }

        public static FakeDbConnection ForNonQuery(int result)
        {
            return new FakeDbConnection(null, result);
        }

        public static FakeDbConnection ForReader(Func<IDataReader> readerFactory)
        {
            return new FakeDbConnection(readerFactory, 0);
        }

        public IDbTransaction BeginTransaction()
        {
            throw new NotSupportedException("Offline fake transactions are not supported.");
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            throw new NotSupportedException("Offline fake transactions are not supported.");
        }

        public void ChangeDatabase(string databaseName)
        {
            throw new NotSupportedException("Offline fake database changes are not supported.");
        }

        public void Close()
        {
            this.state = ConnectionState.Closed;
        }

        public IDbCommand CreateCommand()
        {
            this.LastCommand = new FakeDbCommand(this, this.readerFactory, this.nonQueryResult);
            return this.LastCommand;
        }

        public void Open()
        {
            this.OpenCalled = true;
            throw new InvalidOperationException("Offline fake connections must never be opened.");
        }

        public void Dispose()
        {
            this.state = ConnectionState.Closed;
        }
    }

    private sealed class FakeDbCommand : IDbCommand
    {
        private readonly Func<IDataReader> readerFactory;
        private readonly int nonQueryResult;

        public FakeDbCommand(IDbConnection connection, Func<IDataReader> readerFactory, int nonQueryResult)
        {
            this.Connection = connection;
            this.readerFactory = readerFactory;
            this.nonQueryResult = nonQueryResult;
            this.Parameters = new FakeParameterCollection();
            this.CommandType = CommandType.Text;
        }

        public string CommandText { get; set; }

        public int CommandTimeout { get; set; }

        public CommandType CommandType { get; set; }

        public IDbConnection Connection { get; set; }

        public IDataParameterCollection Parameters { get; private set; }

        public List<IDataParameter> ExecutedParameters { get; private set; }

        public IDbTransaction Transaction { get; set; }

        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel()
        {
        }

        public IDbDataParameter CreateParameter()
        {
            return new FakeDbParameter();
        }

        public void Dispose()
        {
        }

        public int ExecuteNonQuery()
        {
            this.ExecutedParameters = new List<IDataParameter>();
            foreach (object item in this.Parameters)
            {
                this.ExecutedParameters.Add((IDataParameter)item);
            }

            return this.nonQueryResult;
        }

        public IDataReader ExecuteReader()
        {
            return this.ExecuteReader(CommandBehavior.Default);
        }

        public IDataReader ExecuteReader(CommandBehavior behavior)
        {
            if (this.readerFactory == null)
            {
                throw new NotSupportedException("No offline reader was configured.");
            }

            return this.readerFactory();
        }

        public object ExecuteScalar()
        {
            throw new NotSupportedException("Offline fake scalar execution is not supported.");
        }

        public void Prepare()
        {
        }
    }

    private sealed class FakeDbParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }

        public ParameterDirection Direction { get; set; }

        public bool IsNullable { get { return true; } }

        public string ParameterName { get; set; }

        public string SourceColumn { get; set; }

        public DataRowVersion SourceVersion { get; set; }

        public object Value { get; set; }

        public byte Precision { get; set; }

        public byte Scale { get; set; }

        public int Size { get; set; }
    }

    private sealed class FakeParameterCollection : ArrayList, IDataParameterCollection
    {
        public object this[string parameterName]
        {
            get
            {
                int index = this.IndexOf(parameterName);
                return index < 0 ? null : this[index];
            }

            set
            {
                int index = this.IndexOf(parameterName);
                if (index < 0)
                {
                    this.Add(value);
                }
                else
                {
                    this[index] = value;
                }
            }
        }

        public bool Contains(string parameterName)
        {
            return this.IndexOf(parameterName) >= 0;
        }

        public int IndexOf(string parameterName)
        {
            for (int index = 0; index < this.Count; index++)
            {
                IDataParameter parameter = this[index] as IDataParameter;
                if (parameter != null && ParameterNamesEqual(parameter.ParameterName, parameterName))
                {
                    return index;
                }
            }

            return -1;
        }

        public void RemoveAt(string parameterName)
        {
            int index = this.IndexOf(parameterName);
            if (index >= 0)
            {
                this.RemoveAt(index);
            }
        }

        private static bool ParameterNamesEqual(string left, string right)
        {
            return string.Equals(TrimParameterPrefix(left), TrimParameterPrefix(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string TrimParameterPrefix(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value[0] == '@' || value[0] == ':' || value[0] == '?' ? value.Substring(1) : value;
        }
    }
}
