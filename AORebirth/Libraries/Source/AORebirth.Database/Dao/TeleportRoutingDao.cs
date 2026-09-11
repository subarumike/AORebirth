namespace AORebirth.Database.Dao
{
    using System;
    using System.Collections.Generic;
    using System.Data;

    public sealed class TeleportRoute
    {
        public int Playfield { get; set; }
        public int StatelType { get; set; }
        public uint StatelInstance { get; set; }
        public int DestinationPlayfield { get; set; }
        public int DestinationType { get; set; }
        public uint DestinationInstance { get; set; }
    }

    /// <summary>Read-only routing snapshot using the caller's configured connection.</summary>
    public static class TeleportRoutingDao
    {
        public static IReadOnlyList<TeleportRoute> ReadSnapshot(IDbConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT playfield, statelType, statelInstance, destinationPlayfield, destinationType, destinationInstance FROM teleports ORDER BY Id";
                using (var reader = command.ExecuteReader())
                {
                    var routes = new List<TeleportRoute>();
                    while (reader.Read())
                        routes.Add(new TeleportRoute
                        {
                            Playfield = Convert.ToInt32(reader.GetValue(0)),
                            StatelType = Convert.ToInt32(reader.GetValue(1)),
                            StatelInstance = Convert.ToUInt32(reader.GetValue(2)),
                            DestinationPlayfield = Convert.ToInt32(reader.GetValue(3)),
                            DestinationType = Convert.ToInt32(reader.GetValue(4)),
                            DestinationInstance = Convert.ToUInt32(reader.GetValue(5))
                        });
                    return routes;
                }
            }
        }
    }
}
