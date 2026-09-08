using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

internal static class Mike2022HealthExportTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static int Main()
    {
        try
        {
            AssertExport(12, 0, 12);
            AssertExport(190, 0, 190);
            AssertExport(12, 7, 5);
            AssertExport(12, 12, 0);
            AssertExport(12, 15, 0);
            Console.WriteLine("MIKE_2022_HEALTH_EXPORT=PASS (5 cases)");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.GetBaseException().Message);
            return 1;
        }
    }

    // Exercise the actual compiled CSV writer. Run/StartSession are never called;
    // no game process, injection, network callback or native stat query is used.
    private static void AssertExport(int healthPool, int healthDamage, int expectedCurrent)
    {
        Type recorderType = typeof(AOSharpLiveCapture.Mike2022.Main);
        object recorder = Activator.CreateInstance(recorderType);
        Assembly assembly = recorderType.Assembly;
        Type scfuType = assembly.GetType("AORebirth.CaptureProtocol.RawSimpleCharFullUpdate", true);
        object scfu = Activator.CreateInstance(scfuType, true);
        scfuType.GetProperty("Health", Members).SetValue(scfu, healthPool, null);
        scfuType.GetProperty("HealthDamage", Members).SetValue(scfu, healthDamage, null);
        PropertyInfo npcProperty = scfuType.GetProperty("Npc", Members);
        npcProperty.SetValue(scfu, Activator.CreateInstance(npcProperty.PropertyType, true), null);

        Type entityType = recorderType.GetNestedType("EntitySnapshot", BindingFlags.NonPublic);
        object entity = Activator.CreateInstance(entityType, true);
        entityType.GetField("Identity", Members).SetValue(entity, "SimpleChar:00000001");
        entityType.GetField("Name", Members).SetValue(entity, "Health export fixture");

        using (var stream = new MemoryStream())
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            recorderType.GetField("enemyStatSnapshotLog", Members).SetValue(recorder, writer);
            recorderType.GetMethod("WriteScfuStatSnapshot", Members).Invoke(
                recorder,
                new object[] { new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc), "IN", 1, scfu, entity });
            writer.Flush();
            string[] rows = Encoding.UTF8.GetString(stream.ToArray()).Split(
                new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (rows.Length != 14)
            {
                throw new InvalidOperationException("Expected all 14 spawn stat rows.");
            }

            bool sawMaximum = false;
            bool sawCurrent = false;
            foreach (string row in rows)
            {
                // Fixture strings deliberately contain no CSV commas or quotes.
                string[] cells = row.Split(',');
                if (cells.Length != 15)
                {
                    throw new InvalidOperationException("Unexpected spawn-stat CSV schema.");
                }

                int statId = int.Parse(cells[6].Trim('"'), CultureInfo.InvariantCulture);
                int value = int.Parse(cells[7].Trim('"'), CultureInfo.InvariantCulture);
                if (statId == 1)
                {
                    sawMaximum = true;
                    if (value != healthPool) throw new InvalidOperationException("Maximum life exported incorrectly.");
                }
                else if (statId == 27)
                {
                    sawCurrent = true;
                    if (value != expectedCurrent) throw new InvalidOperationException("Current health exported incorrectly.");
                }
            }

            if (!sawMaximum || !sawCurrent)
            {
                throw new InvalidOperationException("Health stat row missing.");
            }
        }
    }
}
