namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Characters;
    using ZoneEngine_New.Core.Logging;
    using static SharedCharacterPersistence;
    /// <summary>Runtime mapping/configuration only. All persistence statements and transactions live in the shared DAO.</summary>
    internal static class SharedCharacterPersistence
    {
        internal static ICharacterPersistenceDao Create()
        {
            string connectionString = MySqlConnectionSettings.GetRequiredConnectionString();
            return new AORebirth.Database.Domain.Characters.MySqlCharacterPersistenceDao(() => new MySqlConnector.MySqlConnection(connectionString));
        }
        internal static T Run<T>(Func<T> operation, IZoneLogger? logger = null)
        {
            try { return operation(); }
            catch (CharacterPersistenceCommitOutcomeUnknownException exception)
            {
                logger?.Error(exception, "Shared character persistence commit outcome unknown");
                throw new DatabaseCommitOutcomeUnknownException(exception);
            }
            catch (Exception exception) { logger?.Error(exception, "Shared character persistence failed"); throw; }
        }
        internal static void Run(Action operation, IZoneLogger? logger = null)
            => Run(() => { operation(); return 0; }, logger);
        internal static CharacterStateData Map(CharacterRecord v) => new CharacterStateData { Id = v.Id, Name = v.Name, FirstName = v.FirstName, LastName = v.LastName, Playfield = v.Playfield, X = v.X, Y = v.Y, Z = v.Z, HeadingW = v.HeadingW, HeadingX = v.HeadingX, HeadingY = v.HeadingY, HeadingZ = v.HeadingZ };
        internal static CharacterRecord Map(CharacterStateData v) => new CharacterRecord { Id = v.Id, Name = v.Name, FirstName = v.FirstName, LastName = v.LastName, Playfield = v.Playfield, X = v.X, Y = v.Y, Z = v.Z, HeadingW = v.HeadingW, HeadingX = v.HeadingX, HeadingY = v.HeadingY, HeadingZ = v.HeadingZ };
        internal static CharacterStatData Map(StatRecord v) => new CharacterStatData { StatId = v.StatId, StatValue = v.StatValue };
        internal static StatRecord Map(CharacterStatData v) => new StatRecord { StatId = v.StatId, StatValue = v.StatValue };
        internal static PersistedItemData Map(ItemInstanceRecord v) => new PersistedItemData { InstanceId = v.InstanceId, ContainerType = v.ContainerType, ContainerInstance = v.ContainerInstance, ContainerPlacement = v.ContainerPlacement, ItemType = v.ItemType, LowId = v.LowId, HighId = v.HighId, Quality = v.Quality, StackCount = v.StackCount, Source = (int)v.Source };
        internal static ItemInstanceRecord Map(PersistedItemData v) => new ItemInstanceRecord { InstanceId = v.InstanceId, ContainerType = v.ContainerType, ContainerInstance = v.ContainerInstance, ContainerPlacement = v.ContainerPlacement, ItemType = v.ItemType, LowId = v.LowId, HighId = v.HighId, Quality = v.Quality, StackCount = v.StackCount, Source = (AORebirth.Enums.ItemSource)v.Source };

        internal static ItemLocationData Map(ItemLocationUpdate v) => new() { InstanceId = v.InstanceId, ContainerType = v.ContainerType, ContainerInstance = v.ContainerInstance, ContainerPlacement = v.ContainerPlacement };
    }
}
