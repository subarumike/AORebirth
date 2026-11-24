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
            IList<ItemLocationData> locations, IList<int> uploadedNanoIds,
            IList<PersistedActiveNanoData>? activeNanos = null,
            IList<PersistedSkillLockData>? skillLocks = null)
        {
            if (inserts == null || locations == null || uploadedNanoIds == null) throw new ArgumentNullException("batch");
            if (inserts.Count == 0 && locations.Count == 0 && uploadedNanoIds.Count == 0 && activeNanos == null && skillLocks == null) return;
            if (skillLocks != null
                && (skillLocks.Any(l => l.StatId < 0 || l.ExpiresAtUtcTicks <= 0)
                    || skillLocks.Select(l => l.StatId).Distinct().Count() != skillLocks.Count))
                throw new ArgumentException("Invalid or duplicate skill lock.", nameof(skillLocks));
            if (activeNanos != null)
                ValidateActiveNanos(activeNanos, nameof(activeNanos));
            Transaction((c, t) =>
            {
                WriteItems(c, t, inserts, locations);
                WriteUploadedNanos(c, t, characterId, uploadedNanoIds);
                if (activeNanos != null)
                {
                    Execute(c, t, "DELETE FROM charactersactivenanos WHERE CharacterId=@Id", "@Id", characterId);
                    foreach (var nano in activeNanos)
                        Execute(c, t, "INSERT INTO charactersactivenanos (CharacterId,NanoId,Strain,NanoInstance,DurationCentiseconds,ExpiresAtUtcTicks) "
                            + "VALUES (@Id,@Nano,@Strain,@Instance,@Duration,@Expiry)", "@Id", characterId,
                            "@Nano", nano.NanoId, "@Strain", nano.Strain, "@Instance", nano.NanoInstance,
                            "@Duration", nano.DurationCentiseconds, "@Expiry", nano.ExpiresAtUtcTicks);
                }
                if (skillLocks != null)
                {
                    Execute(c, t, "DELETE FROM characterskilllocks WHERE CharacterId=@Id", "@Id", characterId);
                    foreach (var skillLock in skillLocks)
                        Execute(c, t, "INSERT INTO characterskilllocks (CharacterId,StatId,ExpiresAtUtcTicks) VALUES (@Id,@Stat,@Expiry)",
                            "@Id", characterId, "@Stat", skillLock.StatId, "@Expiry", skillLock.ExpiresAtUtcTicks);
                }
                return 0;
            });
        }

        public IList<PersistedSkillLockData> LoadSkillLocks(int characterId)
        {
            if (characterId <= 0) throw new ArgumentOutOfRangeException(nameof(characterId));
            return Query("SELECT StatId,ExpiresAtUtcTicks FROM characterskilllocks WHERE CharacterId=@Id ORDER BY StatId",
                r => new PersistedSkillLockData { StatId = r.GetInt32(0), ExpiresAtUtcTicks = r.GetInt64(1) }, "@Id", characterId);
        }

        public void CommitInventoryMutation(CharacterInventoryMutationData mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            if (mutation.CharacterId <= 0) throw new ArgumentOutOfRangeException(nameof(mutation));
            Transaction((c, t) =>
            {
                AssertExpectedSources(c, t, mutation);
                WriteItems(c, t, mutation.Inserts, mutation.Locations);
                AssertContainersEmpty(c, t, mutation.EmptyContainersBeforeRetire);
                WriteStacks(c, t, mutation.Stacks);
                WriteQualities(c, t, mutation.Qualities);
                WriteUploadedNanos(c, t, mutation.CharacterId, mutation.UploadedNanoIds);
                WriteStats(c, t, mutation.CharacterId, mutation.FinalStats);
                return 0;
            }, IsolationLevel.RepeatableRead); // Preserve the child-range gap lock even on READ COMMITTED servers.
        }

        public void CommitItemCredits(ItemCreditMutationData mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            if (mutation.Characters.Count < 1 || mutation.Characters.Count > 2
                || mutation.Characters.Any(c => c.CharacterId <= 0 || c.Cash < 0
                    || c.Currencies.Any(s => s.StatId == (int)CharacterStat.Cash || s.StatValue < 0))
                || mutation.Characters.Select(c => c.CharacterId).Distinct().Count() != mutation.Characters.Count)
                throw new ArgumentException("One or two distinct positive character ids and nonnegative cash are required.", nameof(mutation));
            Transaction((c, t) =>
            {
                foreach (var character in mutation.Characters.OrderBy(x => x.CharacterId)) LockCharacter(c, t, character.CharacterId);
                WriteItems(c, t, mutation.Inserts, mutation.Locations);
                foreach (var character in mutation.Characters.OrderBy(x => x.CharacterId))
                {
                    WriteUploadedNanos(c, t, character.CharacterId, character.UploadedNanoIds);
                    WriteStats(c, t, character.CharacterId,
                        new[] { new CharacterStatData { StatId = (int)CharacterStat.Cash, StatValue = character.Cash } }
                            .Concat(character.Currencies).ToArray());
                }
                return 0;
            });
        }

        public IList<PersistedActiveNanoData> LoadActiveNanos(int characterId)
        {
            if (characterId <= 0) throw new ArgumentOutOfRangeException(nameof(characterId));
            return Query("SELECT NanoId,Strain,NanoInstance,DurationCentiseconds,ExpiresAtUtcTicks FROM charactersactivenanos "
                + "WHERE CharacterId=@Id AND NanoId<=@MaxRuntimeId AND Strain<=@MaxRuntimeId ORDER BY Strain,NanoInstance", r => new PersistedActiveNanoData
                { NanoId = r.GetInt32(0), Strain = r.GetInt32(1), NanoInstance = r.GetInt32(2),
                    DurationCentiseconds = r.GetInt32(3), ExpiresAtUtcTicks = r.GetInt64(4) }, "@Id", characterId, "@MaxRuntimeId", int.MaxValue);
        }

        public void CommitActiveNanos(IList<CharacterActiveNanoData> characters)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            if (characters.Count == 0 || characters.Any(c => c.CharacterId <= 0)
                || characters.Select(c => c.CharacterId).Distinct().Count() != characters.Count)
                throw new ArgumentException("Distinct positive nano owners are required.", nameof(characters));
            foreach (var character in characters)
                ValidateActiveNanos(character.ActiveNanos, nameof(characters));
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

        private static void ValidateActiveNanos(IList<PersistedActiveNanoData> nanos, string parameterName)
        {
            // The runtime decides stacking. Different active nanos may preserve the same original strain.
            if (nanos.Any(n => n == null || n.NanoId <= 0 || n.NanoInstance <= 0 || n.Strain < 0
                    || n.DurationCentiseconds < 0 || n.ExpiresAtUtcTicks < 0 || n.ExpiresAtUtcTicks > DateTime.MaxValue.Ticks)
                || nanos.Select(n => n.NanoId).Distinct().Count() != nanos.Count
                || nanos.Select(n => n.NanoInstance).Distinct().Count() != nanos.Count)
                throw new ArgumentException("Invalid or duplicate active nano identity.", parameterName);
        }
    }
}
