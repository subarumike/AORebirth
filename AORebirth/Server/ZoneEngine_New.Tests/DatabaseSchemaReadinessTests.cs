namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AORebirth.Database.Migrations;
using AORebirth.Database.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.Data;

[TestClass]
public sealed class DatabaseSchemaReadinessTests
{
    [TestMethod]
    public void CurrentContractPassesWithoutChangingSnapshot()
    {
        var snapshot = Current();
        var before = snapshot.Columns.ToArray();
        Assert.IsTrue(DatabaseSchemaReadiness.Evaluate(snapshot).IsCurrent);
        Assert.IsTrue(DatabaseSchemaReadiness.Evaluate(snapshot).IsCurrent);
        CollectionAssert.AreEqual(before, snapshot.Columns.ToArray());
    }

    [TestMethod]
    public void MissingMigrationFailsClosed()
    {
        var result = DatabaseSchemaReadiness.Evaluate(Current() with { AppliedMigrations = SchemaContract.MigrationNames.Take(2).ToArray() });
        Assert.AreEqual(SchemaState.SCHEMA_MIGRATION_REQUIRED, result.State);
        Assert.IsFalse(result.IsCurrent);
        CollectionAssert.AreEqual(SchemaContract.MigrationNames.Skip(2).ToArray(), result.PendingMigrations.ToArray());
    }

    [TestMethod]
    public void MissingColumnFailsEvenWithCurrentLedger()
    {
        var snapshot = Current();
        Assert.AreEqual(SchemaState.SCHEMA_MIGRATION_REQUIRED, DatabaseSchemaReadiness.Evaluate(snapshot with { Columns = snapshot.Columns.Where(c => c.Column != "Source").ToArray() }).State);
    }

    [TestMethod]
    public void WrongSourceSignednessIsIncompatible()
    {
        var snapshot = Current();
        Assert.AreEqual(SchemaState.SCHEMA_INCOMPATIBLE, DatabaseSchemaReadiness.Evaluate(snapshot with { Columns = snapshot.Columns.Select(c => c.Column == "Source" ? c with { ColumnType = "tinyint" } : c).ToArray() }).State);
    }

    [TestMethod]
    public void WrongColumnTypeIsIncompatible()
    {
        var snapshot = Current();
        Assert.AreEqual(SchemaState.SCHEMA_INCOMPATIBLE, DatabaseSchemaReadiness.Evaluate(snapshot with { Columns = snapshot.Columns.Select(c => c.Column == "InstanceId" ? c with { DataType = "varchar" } : c).ToArray() }).State);
    }

    [TestMethod]
    public void UnknownFutureLedgerIsIncompatible()
    {
        Assert.AreEqual(SchemaState.SCHEMA_INCOMPATIBLE, DatabaseSchemaReadiness.Evaluate(Current() with { AppliedMigrations = SchemaContract.MigrationNames.Append("20990101_future.sql").ToArray() }).State);
    }

    [TestMethod]
    public void OutOfOrderLedgerIsIncompatible()
    {
        Assert.AreEqual(SchemaState.SCHEMA_INCOMPATIBLE, DatabaseSchemaReadiness.Evaluate(Current() with { AppliedMigrations = new[] { SchemaContract.MigrationNames[1] } }).State);
    }

    [TestMethod]
    public void MissingUniqueLocationKeyIsIncompatible()
    {
        var snapshot = Current();
        Assert.AreEqual(SchemaState.SCHEMA_INCOMPATIBLE, DatabaseSchemaReadiness.Evaluate(snapshot with { Indexes = snapshot.Indexes.Where(i => i.Columns != "ContainerType,ContainerInstance,ContainerPlacement").ToArray() }).State);
    }

    [TestMethod]
    public void NonTransactionalInventoryTableIsIncompatible()
    {
        var engines = new Dictionary<string, string>(Current().TableEngines) { ["item_instances"] = "MyISAM" };
        Assert.AreEqual(SchemaState.SCHEMA_INCOMPATIBLE, DatabaseSchemaReadiness.Evaluate(Current() with { TableEngines = engines }).State);
    }

    [TestMethod]
    public void MissingOrStaleSequenceIsIncompatible()
    {
        Assert.AreEqual(SchemaState.SCHEMA_INCOMPATIBLE, DatabaseSchemaReadiness.Evaluate(Current() with { NextInstanceId = null }).State);
        Assert.AreEqual(SchemaState.SCHEMA_INCOMPATIBLE, DatabaseSchemaReadiness.Evaluate(Current() with { NextInstanceId = 10, MaximumInstanceId = 10 }).State);
    }

    [TestMethod]
    public void InvalidConfigurationNeverEchoesSecrets()
    {
        var result = DatabaseSchemaReadiness.Check("password=secret-for-test;bad-setting=yes");
        Assert.AreEqual(SchemaState.SCHEMA_INCOMPATIBLE, result.State);
        Assert.IsFalse(result.Message.Contains("secret-for-test", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MutationRequiresExactSeparateOperatorCommandBeforeConnection()
    {
        foreach (var args in new[] { Array.Empty<string>(), new[] { "migrate" }, new[] { "start" }, new[] { "migrate", "--expected-database", "fixture" }, new[] { "status", "--migrate" } })
        {
            int result = MigrationCommand.Run(args, () => throw new AssertFailedException("An invalid command reached the connection provider."), TextWriter.Null);
            Assert.AreEqual(64, result);
        }
        Assert.IsTrue(MigrationCommand.TryParse(new[] { "migrate", "--expected-database", "fixture", "--acknowledge-backup", "--acknowledge-engines-stopped" }, out _, out string? name));
        Assert.AreEqual("fixture", name);
    }

    [TestMethod]
    public void ServerAssemblyCannotReferenceMigrationWriter()
    {
        Assert.IsFalse(typeof(MySqlInventoryRepository).Assembly.GetReferencedAssemblies().Any(a => a.Name == typeof(MigrationCommand).Assembly.GetName().Name));
        Assert.IsFalse(typeof(DatabaseSchemaReadiness).Assembly.GetReferencedAssemblies().Any(a => a.Name == typeof(MigrationCommand).Assembly.GetName().Name));
        Assert.IsFalse(typeof(MySqlInventoryRepository).Assembly.GetTypes().Any(t => t.Name is "DatabaseMigrationRunner" or "SqlTablesBootstrap"));
    }

    [TestMethod]
    public void MigrationCatalogIsOrderedEmbeddedAndLimitedToGovernedDelmusAssets()
    {
        CollectionAssert.AreEqual(SchemaContract.MigrationNames.Order(StringComparer.Ordinal).ToArray(), SchemaContract.MigrationNames.ToArray());
        foreach (string name in SchemaContract.MigrationNames)
        {
            string script = MigrationCommand.ReadScript(name);
            Assert.IsFalse(script.Contains('\r'));
            Assert.IsFalse(script.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(script.Contains("DELETE FROM", StringComparison.OrdinalIgnoreCase));
        }
        Assert.Throws<Exception>(() => MigrationCommand.ReadScript("arbitrary.sql"));
    }

    private static SchemaSnapshot Current()
    {
        var columns = SchemaContract.Columns.Select(c => new SchemaColumn(c.Table, c.Column, c.DataType, c.DataType + (c.Unsigned == true ? " unsigned" : string.Empty))).ToArray();
        var indexes = SchemaContract.UniqueKeys.Select((key, index) => new SchemaIndex(key.Table, "governed_" + index, true, key.Columns)).ToArray();
        var engines = SchemaContract.Columns.Select(c => c.Table).Distinct().ToDictionary(name => name, _ => "InnoDB", StringComparer.OrdinalIgnoreCase);
        return new(columns, indexes, SchemaContract.MigrationNames.ToArray(), 11, 10, engines);
    }
}
