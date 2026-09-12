namespace AORebirth.Database.Domain.Characters
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using AORebirth.Interfaces.Persistence.Characters;

    public sealed partial class MySqlCharacterPersistenceDao
    {
        public void SaveInventoryAndUploadedNanos(int characterId, IList<PersistedItemData> inserts,
            IList<ItemLocationData> locations, IList<int> uploadedNanoIds)
        {
            if (inserts == null || locations == null || uploadedNanoIds == null) throw new ArgumentNullException("batch");
            if (inserts.Count == 0 && locations.Count == 0 && uploadedNanoIds.Count == 0) return;
            Transaction((c, t) => { WriteItems(c, t, inserts, locations); WriteUploadedNanos(c, t, characterId, uploadedNanoIds); return 0; });
        }

        public void CommitInventoryMutation(CharacterInventoryMutationData mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            if (mutation.CharacterId <= 0) throw new ArgumentOutOfRangeException(nameof(mutation));
            Transaction((c, t) =>
            {
                WriteItems(c, t, mutation.Inserts, mutation.Locations);
                AssertContainersEmpty(c, t, mutation.EmptyContainersBeforeRetire);
                WriteStacks(c, t, mutation.Stacks);
                WriteUploadedNanos(c, t, mutation.CharacterId, mutation.UploadedNanoIds);
                WriteStats(c, t, mutation.CharacterId, mutation.FinalStats);
                return 0;
            }, IsolationLevel.RepeatableRead); // Preserve the child-range gap lock even on READ COMMITTED servers.
        }

        public void CommitItemCredits(ItemCreditMutationData mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            if (mutation.Characters.Count < 1 || mutation.Characters.Count > 2
                || mutation.Characters.Any(c => c.CharacterId <= 0 || c.Cash < 0)
                || mutation.Characters.Select(c => c.CharacterId).Distinct().Count() != mutation.Characters.Count)
                throw new ArgumentException("One or two distinct positive character ids and nonnegative cash are required.", nameof(mutation));
            Transaction((c, t) =>
            {
                foreach (var character in mutation.Characters.OrderBy(x => x.CharacterId)) LockCharacter(c, t, character.CharacterId);
                WriteItems(c, t, mutation.Inserts, mutation.Locations);
                foreach (var character in mutation.Characters.OrderBy(x => x.CharacterId))
                {
                    WriteUploadedNanos(c, t, character.CharacterId, character.UploadedNanoIds);
                    WriteStats(c, t, character.CharacterId, new[] { new CharacterStatData { StatId = (int)CharacterStat.Cash, StatValue = character.Cash } });
                }
                return 0;
            });
        }

        public IList<PersistedActiveNanoData> LoadActiveNanos(int characterId)
        {
            if (characterId <= 0) throw new ArgumentOutOfRangeException(nameof(characterId));
            return Query("SELECT NanoId,Strain,NanoInstance,DurationCentiseconds,ExpiresAtUtcTicks FROM charactersactivenanos "
                + "WHERE CharacterId=@Id ORDER BY Strain,NanoInstance", r => new PersistedActiveNanoData
                { NanoId = r.GetInt32(0), Strain = r.GetInt32(1), NanoInstance = r.GetInt32(2),
                    DurationCentiseconds = r.GetInt32(3), ExpiresAtUtcTicks = r.GetInt64(4) }, "@Id", characterId);
        }

        public void CommitActiveNanos(IList<CharacterActiveNanoData> characters)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            if (characters.Count == 0 || characters.Any(c => c.CharacterId <= 0)
                || characters.Select(c => c.CharacterId).Distinct().Count() != characters.Count)
                throw new ArgumentException("Distinct positive nano owners are required.", nameof(characters));
            foreach (var character in characters)
                if (character.ActiveNanos.Any(n => n.NanoId <= 0 || n.NanoInstance <= 0 || n.DurationCentiseconds < 0 || n.ExpiresAtUtcTicks < 0)
                    || character.ActiveNanos.Select(n => n.Strain).Distinct().Count() != character.ActiveNanos.Count
                    || character.ActiveNanos.Select(n => n.NanoInstance).Distinct().Count() != character.ActiveNanos.Count)
                    throw new ArgumentException("Invalid or duplicate active nano identity.", nameof(characters));
            Transaction((c, t) =>
            {
                foreach (var character in characters.OrderBy(x => x.CharacterId)) LockCharacter(c, t, character.CharacterId);
                foreach (var character in characters.OrderBy(x => x.CharacterId))
                {
                    Execute(c, t, "DELETE FROM charactersactivenanos WHERE CharacterId=@Id", "@Id", character.CharacterId);
                    foreach (var nano in character.ActiveNanos)
                        Execute(c, t, "INSERT INTO charactersactivenanos (CharacterId,NanoId,Strain,NanoInstance,DurationCentiseconds,ExpiresAtUtcTicks) "
                            + "VALUES (@Id,@Nano,@Strain,@Instance,@Duration,@Expiry)", "@Id", character.CharacterId,
                            "@Nano", nano.NanoId, "@Strain", nano.Strain, "@Instance", nano.NanoInstance,
                            "@Duration", nano.DurationCentiseconds, "@Expiry", nano.ExpiresAtUtcTicks);
                    WriteStats(c, t, character.CharacterId, character.BaseStats);
                }
                return 0;
            });
        }
    }
}
