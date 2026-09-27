using System.Data;
using System.Reflection;
using System.Text.Json;

internal static class PersistenceAudit
{
    internal static void Run(string engine, string output)
    {
        var rows = new List<Dictionary<string,object>>();
        string status;
        try
        {
            string? value=Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION")
                ?? Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION",EnvironmentVariableTarget.User);
            if(string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("LOCAL_CONNECTION_NOT_CONFIGURED");
            var context=new DecoderContext(Path.GetFullPath(engine));
            var assembly=context.LoadFromAssemblyName(new AssemblyName("MySqlConnector"));
            var builder=Activator.CreateInstance(assembly.GetType("MySqlConnector.MySqlConnectionStringBuilder",true)!,[value])!;
            string server=(string)builder.GetType().GetProperty("Server")!.GetValue(builder)!;
            string database=(string)builder.GetType().GetProperty("Database")!.GetValue(builder)!;
            if(server is not ("localhost" or "127.0.0.1" or "::1") || database!="cellao_codex_clean")
                throw new InvalidOperationException("NOT_APPROVED_LOCAL_DATABASE");
            using var connection=(IDbConnection)Activator.CreateInstance(assembly.GetType("MySqlConnector.MySqlConnection",true)!,[value])!;
            connection.Open();
            using var command=connection.CreateCommand();
            command.CommandTimeout=10;
            command.CommandText="SELECT BundleId,BundleSha256,COUNT(*) AS BindingCount FROM generatedmissionbindings GROUP BY BundleId,BundleSha256 ORDER BY BundleId,BundleSha256";
            using var reader=command.ExecuteReader();
            while(reader.Read())
            {
                var row=new Dictionary<string,object>();
                for(int i=0;i<reader.FieldCount;i++) row.Add(reader.GetName(i),reader.GetValue(i));
                rows.Add(row);
            }
            status="READ_ONLY_LOCAL_QUERY_COMPLETED";
        }
        catch(Exception e)
        {
            while(e.InnerException!=null) e=e.InnerException;
            // No connection string, host, username or server diagnostic is emitted.
            status=e is InvalidOperationException && e.Message is "LOCAL_CONNECTION_NOT_CONFIGURED" or "NOT_APPROVED_LOCAL_DATABASE"
                ? e.Message : "READ_ONLY_QUERY_UNAVAILABLE:"+e.GetType().Name+":number="+e.GetType().GetProperty("Number")?.GetValue(e);
            if(e is FileNotFoundException missing) status += ":assembly="+missing.FileName;
        }
        using var stream=new FileStream(Path.GetFullPath(output),FileMode.CreateNew);
        JsonSerializer.Serialize(stream,new { status, schema_changed=false, rows_changed=0, bindings=rows },new JsonSerializerOptions { WriteIndented=true });
        Console.WriteLine(status);
    }
}
