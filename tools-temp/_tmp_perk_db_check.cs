using System;
using System.Data;
using MySqlConnector;

class Program
{
    static int Main()
    {
        string cs = "Server=localhost;Database=cellao_codex_clean;Uid=root;Pwd=";
        try
        {
            using (var conn = new MySqlConnection(cs))
            {
                conn.Open();
                Console.WriteLine("CONNECTED");
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SHOW TABLES LIKE 'charactersperks'";
                    object t = cmd.ExecuteScalar();
                    Console.WriteLine("TABLE=" + (t == null ? "MISSING" : t.ToString()));
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM charactersperks";
                    try
                    {
                        Console.WriteLine("ROWCOUNT=" + cmd.ExecuteScalar());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("COUNT_FAIL=" + ex.Message);
                    }
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, CharacterId, PacketId FROM charactersperks ORDER BY Id DESC LIMIT 30";
                    try
                    {
                        using (var r = cmd.ExecuteReader())
                        {
                            int n = 0;
                            while (r.Read())
                            {
                                Console.WriteLine("ROW Id=" + r.GetInt32(0) + " Char=" + r.GetInt32(1) + " Packet=" + r.GetInt32(2));
                                n++;
                            }
                            if (n == 0) Console.WriteLine("NO_ROWS");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("SELECT_FAIL=" + ex.Message);
                    }
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, Name FROM characters WHERE Id=18 OR Name LIKE '%Nerko%' LIMIT 5";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            Console.WriteLine("CHAR Id=" + r.GetInt32(0) + " Name=" + r.GetString(1));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAIL=" + ex);
            return 1;
        }
        return 0;
    }
}
