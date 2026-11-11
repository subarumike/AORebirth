#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace LoginEngine.Packets
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text;

    using AORebirth.Database;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Interfaces.Persistence.CharacterCreation;
    using AORebirth.Interfaces.Persistence.Characters;
    using AORebirth.Interfaces.Persistence.Missions;

    using LoginEngine.CharacterCreation;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// </summary>
    public class CharacterName
    {
        #region Static Fields

        /// <summary>
        /// </summary>
        private static string mandatoryVowel = "aiueo"; /* 5 chars */

        /// <summary>
        /// </summary>
        private static string optionalOrdCon = "vybcfghjqktdnpmrlws"; /* 19 chars */

        /// <summary>
        /// </summary>
        private static string optionalOrdEnd = "nmrlstyzx"; /* 9 chars */

        private static IMissionDao missionDao;

        #endregion

        #region Public Properties

        /// <summary>
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// </summary>
        public int Breed { get; set; }

        /// <summary>
        /// </summary>
        public int Fatness { get; set; }

        /// <summary>
        /// </summary>
        public int Gender { get; set; }

        /// <summary>
        /// </summary>
        public int HeadMesh { get; set; }

        /// <summary>
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// </summary>
        public int MonsterScale { get; set; }

        /// <summary>
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// </summary>
        public int Profession { get; set; }

        #endregion

        #region Properties

        /// <summary>
        /// </summary>
        private int[] Abis { get; set; }

        #endregion

        #region Public Methods and Operators

        public static void InitializeMissionPersistence(IMissionDao dao)
        {
            if (dao == null)
            {
                throw new ArgumentNullException("dao");
            }

            missionDao = dao;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        public int CheckAgainstDatabase(bool startInSL = false)
        {
            /* name in use */
            if (CharacterDao.Instance.ExistsByName(this.Name))
            {
                return 0;
            }

            return this.CreateNewChar(startInSL);
        }

        /// <summary>
        /// </summary>
        /// <param name="charid">
        /// </param>
        public void DeleteChar(int charid)
        {
            this.TryDeleteChar(charid);
        }

        /// <summary>
        /// </summary>
        /// <param name="charid">
        /// </param>
        /// <returns>
        /// </returns>
        internal bool TryDeleteChar(int charid)
        {
            try
            {
                CharacterDao.Instance.Delete(charid);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(this.Name + e.Message);
                return false;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="accountName">
        /// </param>
        /// <param name="charid">
        /// </param>
        /// <returns>
        /// </returns>
        internal bool TryDeleteChar(string accountName, int charid)
        {
            try
            {
                return CharacterDao.Instance.DeleteForUser(accountName, charid);
            }
            catch (Exception e)
            {
                Console.WriteLine(this.Name + e.Message);
                return false;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="profession">
        /// </param>
        /// <returns>
        /// </returns>
        public string GetRandomName(Profession profession)
        {
            var random = new Random();
            byte randomNameLength = 0;
            var randomLength = (byte)random.Next(3, 8);
            var sb = new StringBuilder();
            while (randomNameLength <= randomLength)
            {
                if (random.Next(14) > 4)
                {
                    sb.Append(optionalOrdCon.Substring(random.Next(0, 18), 1));
                    randomNameLength++;
                }

                sb.Append(mandatoryVowel.Substring(random.Next(0, 4), 1));
                randomNameLength++;

                if (random.Next(14) <= 4)
                {
                    continue;
                }

                sb.Append(optionalOrdEnd.Substring(random.Next(0, 8), 1));
                randomNameLength++;
            }

            string name = sb.ToString();
            name = char.ToUpper(name[0]) + name.Substring(1);
            return name;
        }

        /// <summary>
        /// </summary>
        /// <param name="startInSL">
        /// </param>
        /// <param name="charid">
        /// </param>
        public void SendNameToStartPlayfield(bool startInSL, int charid)
        {
            NewCharacterStartLocation startLocation = NewCharacterStats.Current.StartLocation;

            DBCharacter character = CharacterDao.Instance.Get(charid);
            if (character != null)
            {
                CharacterDao.Instance.Save(
                    character // woo....
                    ,
                    new { Id = charid, Playfield = startLocation.Playfield, X = startLocation.X, Y = startLocation.Y, Z = startLocation.Z });

                CharacterDao.Instance.SetPlayfield(charid, (int)IdentityType.Playfield, startLocation.Playfield);

                if (!startInSL)
                {
                    if (missionDao == null)
                    {
                        throw new InvalidOperationException(
                            "New-character mission persistence has not been initialized.");
                    }

                    missionDao.MarkStartAreaSelectionPending(charid);
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        /// <returns>
        /// charID
        /// </returns>
        private int CreateNewChar(bool startInSL)
        {
            // Resolve all editable starter content and the account before opening the write transaction.
            NewCharacterStats starter = NewCharacterStats.Current;
            var startPackage = CharacterStartPackageApplier.RequirePackage(this.Profession);
            DBLoginData account = LoginDataDao.Instance.GetByUsername(this.AccountName);
            return this.CreateNewChar(startInSL, starter, account, startPackage, Connector.GetConnection);
        }

        internal int CreateNewChar(bool startInSL, NewCharacterStats starter, DBLoginData account,
            CharacterStartProfessionPackage startPackage, Func<IDbConnection> connectionFactory)
        {
            if (starter == null) throw new ArgumentNullException(nameof(starter));
            if (startPackage == null) throw new ArgumentNullException(nameof(startPackage));
            if (connectionFactory == null) throw new ArgumentNullException(nameof(connectionFactory));
            if (string.IsNullOrWhiteSpace(this.Name) || string.IsNullOrWhiteSpace(this.AccountName))
                throw new InvalidOperationException("Character creation requires a name and authenticated account.");
            if (account == null || account.Id <= 0
                || !string.Equals(account.Username, this.AccountName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The authenticated character-creation account is unavailable.");
            if (this.Profession <= 0 || !Enum.IsDefined(typeof(Profession), this.Profession)
                || startPackage.ProfessionId != this.Profession)
                throw new InvalidOperationException("The selected profession has no matching starting package.");
            if (this.Breed <= 0 || !Enum.IsDefined(typeof(Breed), this.Breed)
                || !Enum.IsDefined(typeof(Gender), this.Gender))
                throw new InvalidOperationException("The selected breed or gender is invalid.");

            NewCharacterStartLocation startLocation = starter.StartLocation;
            if (startLocation == null || startLocation.Playfield <= 0
                || float.IsNaN(startLocation.X) || float.IsInfinity(startLocation.X)
                || float.IsNaN(startLocation.Y) || float.IsInfinity(startLocation.Y)
                || float.IsNaN(startLocation.Z) || float.IsInfinity(startLocation.Z))
                throw new InvalidOperationException("Character creation requires a valid starting location.");

            DBCharacter newCharacter = new DBCharacter
                                       {
                                           FirstName = string.Empty,
                                           LastName = string.Empty,
                                           Name = this.Name,
                                           Username = this.AccountName,
                                       };

            int charID = 0;

            #region Statistics

            // Fixed starter stats and breed abilities come from GameData/NewCharacter.json.
            BreedAbilities breedAbilities = starter.AbilitiesFor(this.Breed);
            if (breedAbilities == null)
            {
                throw new InvalidOperationException(
                    "GameData NewCharacter.json has no starting abilities for breed " + this.Breed + ".");
            }

            this.Abis = breedAbilities.ToLegacyOrder();
            if (breedAbilities.Gender is int breedGender)
                this.Gender = breedGender;
            startPackage = startPackage.ResolveForGender(this.Gender);

            List<DBStats> stats = new List<DBStats>();
            foreach (StatValue starterStat in starter.Stats)
            {
                stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = starterStat.Stat, StatValue = starterStat.Value });
            }

            // Transmit GM level into stats table
            stats.Add(
                new DBStats
                {
                    Type = 50000,
                    Instance = charID,
                    StatId = 215,
                    StatValue = account.GM
                });

            // SEXXX
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 59, StatValue = this.Gender });

            // Headmesh
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 64, StatValue = this.HeadMesh });

            // MonsterScale
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 360, StatValue = this.MonsterScale });

            // Visual Sex (even better ^^)
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 369, StatValue = this.Gender });

            // Breed
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 4, StatValue = this.Breed });

            // Visual Breed
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 367, StatValue = this.Breed });

            // Profession / 60
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 60, StatValue = this.Profession });

            // VisualProfession / 368
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 368, StatValue = this.Profession });

            // Fatness / 47
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 47, StatValue = this.Fatness });

            // Strength / 16
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 16, StatValue = this.Abis[0] });

            // Psychic / 21
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 21, StatValue = this.Abis[1] });

            // Sense / 20
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 20, StatValue = this.Abis[2] });

            // Intelligence / 19
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 19, StatValue = this.Abis[3] });

            // Stamina / 18
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 18, StatValue = this.Abis[4] });

            // Agility / 17
            stats.Add(new DBStats { Type = 50000, Instance = charID, StatId = 17, StatValue = this.Abis[5] });

            stats.Add(
                new DBStats
                {
                    Type = 50000,
                    Instance = charID,
                    StatId = 389,
                    StatValue = account.Expansions
                });

            var initialStats = new Dictionary<int, int>();
            foreach (DBStats stat in stats)
            {
                if (stat.StatId < 0 || initialStats.ContainsKey(stat.StatId))
                    throw new InvalidOperationException("New-character starter stats contain an invalid or duplicate runtime stat: " + stat.StatId + ".");
                initialStats.Add(stat.StatId, stat.StatValue);
            }
            int value;
            if (!initialStats.TryGetValue((int)CharacterStat.Level, out value) || value <= 0
                || !initialStats.TryGetValue((int)CharacterStat.BodyDevelopment, out value) || value < 0
                || !initialStats.TryGetValue((int)CharacterStat.NanoPool, out value) || value < 0)
                throw new InvalidOperationException("New-character stats require a positive level and nonnegative BodyDevelopment and NanoPool.");
            #endregion

            IDbConnection connection = null;
            IDbTransaction transaction = null;
            Exception failure = null;
            bool commitAttempted = false;
            try
            {
                connection = connectionFactory();
                if (connection == null) throw new InvalidOperationException("Character creation connection is unavailable.");
                if (connection.State != ConnectionState.Open) connection.Open();
                transaction = connection.BeginTransaction();
                string stage = "character insertion";
                try
                {
                    CharacterDao.Instance.Add(newCharacter, connection, transaction);
                    charID = newCharacter.Id;
                    if (charID <= 0) throw new InvalidOperationException("Character insertion returned no identity.");
                    foreach (DBStats stat in stats) stat.Instance = charID;

                    stage = "initial stats";
                    StatDao.Instance.BulkReplace(stats, connection, transaction);
                    stage = "starting inventory and uploaded nanos";
                    CharacterStartPackageApplier.Apply(charID, startPackage, connection, transaction);
                    stage = "starting health and nano";
                    StarterVitalStats.Apply(charID, this.Breed, this.Profession, this.Abis, connection, transaction);
                    stage = "character creation time";
                    CharacterPlayedBornDao.RecordCreation(charID, newCharacter.Name ?? string.Empty,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds(), connection, transaction);
                    stage = "starting location";
                    CharacterDao.Instance.Save(newCharacter,
                        new { Id = charID, Playfield = startLocation.Playfield, X = startLocation.X, Y = startLocation.Y, Z = startLocation.Z },
                        connection, transaction);
                    CharacterDao.Instance.SetPlayfield(charID, (int)IdentityType.Playfield, startLocation.Playfield, connection, transaction);
                    if (!startInSL)
                    {
                        stage = "Rubi-Ka start-area selection";
                        if (!NewCharacterStartAreaSelectionDao.MarkPending(charID, connection, transaction))
                            throw new InvalidOperationException("The starting-area selection marker was not persisted.");
                    }

                    stage = "commit";
                    commitAttempted = true;
                    try { transaction.Commit(); }
                    catch (Exception exception) { throw new CharacterPersistenceCommitOutcomeUnknownException(exception); }
                    return charID;
                }
                catch (Exception exception)
                {
                    if (!commitAttempted)
                    {
                        try { transaction.Rollback(); }
                        catch (Exception rollbackFailure) { exception.Data["CharacterCreation.RollbackFailure"] = rollbackFailure; }
                    }
                    if (exception is CharacterPersistenceCommitOutcomeUnknownException) throw;
                    throw new InvalidOperationException("Character creation failed during " + stage + ".", exception);
                }
            }
            catch (Exception exception)
            {
                failure = exception;
                throw;
            }
            finally
            {
                bool hadFailure = failure != null;
                DisposeCreationResource(transaction, ref failure, commitAttempted);
                DisposeCreationResource(connection, ref failure, commitAttempted);
                if (!hadFailure && failure != null) throw failure;
            }
        }

        private static void DisposeCreationResource(IDisposable resource, ref Exception failure, bool commitAttempted)
        {
            if (resource == null) return;
            try { resource.Dispose(); }
            catch (Exception cleanupFailure)
            {
                if (failure == null)
                {
                    failure = commitAttempted
                        ? new CharacterPersistenceCommitOutcomeUnknownException(cleanupFailure)
                        : cleanupFailure;
                }
                else
                {
                    failure.Data["CharacterCreation.DisposeFailure." + resource.GetType().Name] = cleanupFailure;
                }
            }
        }

        #endregion
    }
}
