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

namespace ZoneEngine.ChatCommands
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using CellAO.Core.Entities;
    using CellAO.Core.NPCHandler;
    using CellAO.Core.Playfields;
    using CellAO.Core.Vector;
    using CellAO.Database.Dao;
    using CellAO.Enums;
    using CellAO.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;

    using Utility;

    using Vector3 = CellAO.Core.Vector.Vector3;

    #endregion

    /// <summary>
    /// </summary>
    public class ChatCommandSpawn : AOChatCommand
    {
        private static readonly float[,] ZonePopulationOffsets =
        {
            { 4.0f, 5.0f },
            { -4.0f, 6.5f },
            { 7.0f, 0.0f },
            { -7.0f, 0.0f },
            { 4.0f, -5.0f },
            { -4.0f, -6.5f },
            { 0.0f, 9.0f },
            { 9.0f, 5.0f }
        };

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="args">
        /// </param>
        /// <returns>
        /// </returns>
        public override bool CheckCommandArguments(string[] args)
        {
            CombatTestMobArchetype.Entry combatTestMob;
            if (TryResolveCombatTestMob(args, out combatTestMob))
            {
                return true;
            }

            if ((args.Length == 2) && (string.Compare(args[1], "testmobs", true) == 0))
            {
                return true;
            }

            if ((args.Length == 2)
                && ((string.Compare(args[1], "hints", true) == 0)
                    || (string.Compare(args[1], "zone", true) == 0)
                    || (string.Compare(args[1], "status", true) == 0)
                    || (string.Compare(args[1], "clear", true) == 0)))
            {
                return true;
            }

            if (args[0] == "spawnrandom")
            {
                return true;
            }
            List<Type> check = new List<Type>();
            check.Add(typeof(string));
            check.Add(typeof(uint));
            bool check1 = CheckArgumentHelper(check, args);
            if (check1)
            {
                return true;
            }

            if (args.Length == 2)
            {
                return true;
            }

            if (args.Length > 1)
            {
                if (args[1].ToLower() != "list")
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public override void CommandHelp(ICharacter character)
        {
            character.Playfield.Publish(ChatTextMessageHandler.Default.CreateIM(character,
                @"Usage: /command Spawn hash level
For a list of available templates: /command spawn list [filter1,filter2...]
Spawn the current combat test mob: /command spawnleet
Spawn combat test mob aliases: /command spawn testmobs
List supported population mobs for this playfield: /command spawn hints
Spawn supported DB population mobs for this playfield: /command spawn zone
Show live spawned mobs for this playfield: /command spawn status
Clear live spawned mobs/corpses for this playfield: /command spawn clear
Filter will be applied to mob name"));
        }

        public void SpawnRandomMob(ICharacter character)
        {
            Coordinate coord = character.Coordinates();

            DBMobTemplate[] templates = MobTemplateDao.Instance.GetAll().ToArray();
            Random rnd = new Random(Environment.TickCount);

            int mobNumber = rnd.Next(templates.Length);

            DBMobTemplate template = templates[mobNumber];
            NPCController npcController = new NPCController();
            Character mobCharacter = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                template.Hash,
                character.Playfield.Identity,
                character.Coordinates(),
                character.RawHeading,
                npcController);
            mobCharacter.Playfield = character.Playfield;
            SimpleCharFullUpdateMessage mess = SimpleCharFullUpdate.ConstructMessage(mobCharacter);
            character.Playfield.Announce(mess);
            AppearanceUpdateMessageHandler.Default.Send(mobCharacter);

            Vector3 v = new Vector3(coord.x, coord.y, coord.z + 5);
            mobCharacter.AddWaypoint(v, false);
            v.x += 10 - rnd.Next(20);
            v.z -= 10 - rnd.Next(20);

            mobCharacter.AddWaypoint(v, false);
            v.x += 10 - rnd.Next(20);
            v.z -= 10 - rnd.Next(20);
            mobCharacter.AddWaypoint(v, false);
            v.x += 10 - rnd.Next(20);
            v.z -= 10 - rnd.Next(20);
            mobCharacter.AddWaypoint(v, false);
            mobCharacter.Stats[StatIds.health].Value = 10000;
            mobCharacter.DoNotDoTimers = false;
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="target">
        /// </param>
        /// <param name="args">
        /// </param>
        public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
        {
            CombatTestMobArchetype.Entry combatTestMob;
            if (TryResolveCombatTestMob(args, out combatTestMob))
            {
                this.SpawnCombatTestMob(character, combatTestMob);
                return;
            }

            if (string.Compare(args[0], "spawnrandom", true) == 0)
            {
                this.SpawnRandomMob(character);
                return;
            }

            if (string.Compare(args[0], "spawncount", true) == 0)
            {
                this.SpawnCount(character);
                return;
            }

            if ((args.Length == 2) && (string.Compare(args[1], "hints", true) == 0))
            {
                this.ListClientHintedMobs(character);
                return;
            }

            if ((args.Length == 2) && (string.Compare(args[1], "status", true) == 0))
            {
                this.ShowCombatTestMobStatus(character);
                return;
            }

            if ((args.Length == 2) && (string.Compare(args[1], "clear", true) == 0))
            {
                this.ClearCombatTestMobs(character);
                return;
            }

            if ((args.Length == 2) && (string.Compare(args[1], "zone", true) == 0))
            {
                this.SpawnClientHintedMobs(character);
                return;
            }

            if ((args.Length == 2) && (string.Compare(args[1], "testmobs", true) == 0))
            {
                this.ListCombatTestMobs(character);
                return;
            }

            if ((args.Length > 1) && (string.Compare(args[1], "list", true) == 0))
            {
                // list templates
                IEnumerable<DBMobTemplate> mobTemplates =
                    MobTemplateDao.Instance.GetMobTemplatesByName((args.Length > 2) ? args[2] : "%", false);

                StringBuilder text = new StringBuilder("List of mobtemplates (Hash, Name): ");
                foreach (DBMobTemplate mt in mobTemplates)
                {
                    text.AppendLine(string.Format("{0},'{1}'", mt.Hash, mt.Name));
                }

                character.Playfield.Publish(ChatTextMessageHandler.Default.CreateIM(character, text.ToString()));
            }
            else
            {
                // try spawning mob
                Character mobCharacter = null;
                string templateHash = args.Length > 1 ? args[1] : string.Empty;
                if (args.Length == 3)
                {
                    // DBMobTemplate mt = MobTemplateDao.GetMobTemplateByHash(args[1])
                    // character.Playfield.Despawn
                    //NonPlayerCharacterHandler.SpawnMonster(client, args[1], uint.Parse(args[2]));
                    NPCController npcController = new NPCController();
                    mobCharacter = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                        args[1],
                        character.Playfield.Identity,
                        character.Coordinates(),
                        character.RawHeading,
                        npcController,
                        int.Parse(args[2]));
                }
                if (args.Length == 2)
                {
                    NPCController npcController = new NPCController();
                    mobCharacter = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                        templateHash,
                        character.Playfield.Identity,
                        character.Coordinates(),
                        character.RawHeading,
                        npcController);
                }
                if (mobCharacter != null)
                {
                    mobCharacter.Playfield = character.Playfield;
                    mobCharacter.Stats[StatIds.health].Value = mobCharacter.Stats[StatIds.life].Value;
                    mobCharacter.Stats[StatIds.health].BaseValue = (uint)mobCharacter.Stats[StatIds.life].Value;
                    mobCharacter.DoNotDoTimers = false;

                    SimpleCharFullUpdateMessage mess = SimpleCharFullUpdate.ConstructMessage(mobCharacter);
                    character.Playfield.Announce(mess);
                    character.Playfield.Announce(new CharInPlayMessage { Identity = mobCharacter.Identity, Unknown = 0x00 });
                    AppearanceUpdateMessageHandler.Default.Send(mobCharacter);

                    character.Playfield.Publish(
                        ChatTextMessageHandler.Default.CreateIM(
                            character,
                            string.Format(
                                "Spawned {0} {1}.",
                                mobCharacter.Name,
                                mobCharacter.Identity.ToString(true))));

                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        string.Format(
                            "DB mob spawned template={0} name={1} identity={2} pf={3} hp={4}/{5} monsterData={6} catMesh={7}",
                            templateHash,
                            mobCharacter.Name,
                            mobCharacter.Identity.ToString(true),
                            character.Playfield.Identity.ToString(true),
                            mobCharacter.Stats[StatIds.health].Value,
                            mobCharacter.Stats[StatIds.life].Value,
                            mobCharacter.Stats[StatIds.monsterdata].Value,
                            mobCharacter.Stats[StatIds.catmesh].Value));
                }
                else if (args.Length == 2 || args.Length == 3)
                {
                    character.Playfield.Publish(
                        ChatTextMessageHandler.Default.CreateIM(
                            character,
                            string.Format("No mob template found for hash '{0}'.", templateHash)));

                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        string.Format("DB mob spawn failed: no template for hash {0}.", templateHash));
                }
            }

            // this.CommandHelp(client);

            //var check = new List<Type> { typeof(float), typeof(float), typeof(int) };

            //var coord = new Coordinate();
            //int pf = character.Playfield.Identity.Instance;
            //if (CheckArgumentHelper(check, args))
            //{
            //    coord = new Coordinate(
            //        float.Parse(args[1], NumberStyles.Any, CultureInfo.InvariantCulture),
            //        character.Coordinates.y,
            //        float.Parse(args[2], NumberStyles.Any, CultureInfo.InvariantCulture));
            //    pf = int.Parse(args[3]);
            //}

            //check.Clear();
            //check.Add(typeof(float));
            //check.Add(typeof(float));
            //check.Add(typeof(string));
            //check.Add(typeof(float));
            //check.Add(typeof(int));

            //if (CheckArgumentHelper(check, args))
            //{
            //    coord = new Coordinate(
            //        float.Parse(args[1], NumberStyles.Any, CultureInfo.InvariantCulture),
            //        float.Parse(args[4], NumberStyles.Any, CultureInfo.InvariantCulture),
            //        float.Parse(args[2], NumberStyles.Any, CultureInfo.InvariantCulture));
            //    pf = int.Parse(args[5]);
            //}

            //if (!Playfields.ValidPlayfield(pf))
            //{
            //    character.Playfield.Publish(
            //        new IMSendAOtomationMessageBodyToClient()
            //        {
            //            Body =
            //                new FeedbackMessage()
            //                {
            //                    CategoryId = 110,
            //                    MessageId = 188845972
            //                },
            //            client = character.Client
            //        });
            //    return;
            //}

            //character.Playfield.Teleport(
            //    (Character)character,
            //    coord,
            //    character.Heading,
            //    new Identity() { Type = IdentityType.Playfield, Instance = pf });
        }

        private void SpawnCount(ICharacter character)
        {
            character.Playfield.Publish(ChatTextMessageHandler.Default.CreateIM(character, "Spawncount on this PF: "+Pool.Instance.GetAll<ICharacter>(character.Playfield.Identity).Count(x => x.Controller is NPCController)));
        }

        private static List<ICharacter> GetLiveCombatTestMobs(ICharacter character)
        {
            return Pool.Instance.GetAll<ICharacter>(character.Playfield.Identity, (int)IdentityType.CanbeAffected)
                .Where(CombatTestMobArchetype.IsCombatTestMob)
                .ToList();
        }

        private static bool TryResolveCombatTestMob(string[] args, out CombatTestMobArchetype.Entry combatTestMob)
        {
            combatTestMob = null;

            if (string.Compare(args[0], "spawnleet", true) == 0)
            {
                combatTestMob = CombatTestMobArchetype.Default;
                return true;
            }

            if (args.Length == 2)
            {
                return CombatTestMobArchetype.TryGetByAlias(args[1], out combatTestMob);
            }

            return false;
        }

        private void ListCombatTestMobs(ICharacter character)
        {
            var text = new StringBuilder("Combat test mobs:");
            foreach (CombatTestMobArchetype.Entry entry in CombatTestMobArchetype.All)
            {
                text.AppendLine(
                    string.Format(
                        "{0}: /command spawn {1} ({2}, template {3})",
                        entry.DisplayName,
                        entry.Key,
                        string.Join(", ", entry.Aliases),
                        entry.TemplateHash));
            }

            character.Playfield.Publish(ChatTextMessageHandler.Default.CreateIM(character, text.ToString()));
        }

        private void ShowCombatTestMobStatus(ICharacter character)
        {
            List<ICharacter> liveTestMobs = GetLiveCombatTestMobs(character);
            List<CombatTestMobArchetype.Entry> hintedEntries =
                CombatTestMobArchetype.ForPlayfield(character.Playfield.Identity.Instance).ToList();

            var text = new StringBuilder();
            text.AppendLine(
                string.Format(
                    "Combat test mob status for playfield {0}:",
                    character.Playfield.Identity.Instance));
            text.AppendLine(string.Format("Live test mobs: {0}", liveTestMobs.Count));
            foreach (ICharacter mob in liveTestMobs)
            {
                text.AppendLine(string.Format("- {0} {1}", mob.Name, mob.Identity.ToString(true)));
            }

            text.AppendLine(
                string.Format(
                    "Supported population mobs: {0}",
                    hintedEntries.Count == 0
                        ? "none"
                        : string.Join(", ", hintedEntries.Select(x => x.RuntimeName))));

            character.Playfield.Publish(ChatTextMessageHandler.Default.CreateIM(character, text.ToString()));
        }

        private void ClearCombatTestMobs(ICharacter character)
        {
            List<ICharacter> liveTestMobs = GetLiveCombatTestMobs(character);
            Playfield playfield = character.Playfield as Playfield;

            foreach (ICharacter mob in liveTestMobs)
            {
                if (playfield != null)
                {
                    playfield.DespawnNpcImmediately(mob);
                }
                else
                {
                    character.Playfield.Despawn(mob.Identity);
                    Pool.Instance.RemoveObject((Character)mob);
                }
            }

            int corpseCount = 0;
            if (playfield != null)
            {
                corpseCount = playfield.DespawnDebugCorpses(
                    (name, deadNpc) => CombatTestMobArchetype.IsCombatTestCorpseName(name));
            }

            character.Playfield.Publish(
                ChatTextMessageHandler.Default.CreateIM(
                    character,
                    string.Format(
                        "Cleared {0} live combat test mobs and {1} combat test corpses from playfield {2}.",
                        liveTestMobs.Count,
                        corpseCount,
                        character.Playfield.Identity.Instance)));
        }

        private void ListClientHintedMobs(ICharacter character)
        {
            List<CombatTestMobArchetype.Entry> entries =
                CombatTestMobArchetype.ForPlayfield(character.Playfield.Identity.Instance).ToList();

            if (entries.Count == 0)
            {
                character.Playfield.Publish(
                    ChatTextMessageHandler.Default.CreateIM(
                        character,
                        string.Format(
                            "No supported combat test mobs are mapped from client hints for playfield {0}.",
                            character.Playfield.Identity.Instance)));
                return;
            }

            character.Playfield.Publish(
                ChatTextMessageHandler.Default.CreateIM(
                    character,
                    string.Format(
                        "Supported population mobs for playfield {0}: {1}.",
                        character.Playfield.Identity.Instance,
                        string.Join(", ", entries.Select(x => x.RuntimeName + " [" + x.TemplateHash + "]")))));
        }

        private void SpawnClientHintedMobs(ICharacter character)
        {
            List<CombatTestMobArchetype.Entry> entries =
                CombatTestMobArchetype.ForPlayfield(character.Playfield.Identity.Instance).ToList();

            if (entries.Count == 0)
            {
                character.Playfield.Publish(
                    ChatTextMessageHandler.Default.CreateIM(
                        character,
                        string.Format(
                            "No supported population mobs are mapped for playfield {0}.",
                            character.Playfield.Identity.Instance)));
                return;
            }

            var spawnedNames = new List<string>();
            for (int index = 0; index < entries.Count; index++)
            {
                CombatTestMobArchetype.Entry entry = entries[index];
                int offsetIndex = index % ZonePopulationOffsets.GetLength(0);
                Character mobCharacter = this.SpawnPopulationMob(
                    character,
                    entry,
                    ZonePopulationOffsets[offsetIndex, 0],
                    ZonePopulationOffsets[offsetIndex, 1]);
                if (mobCharacter != null)
                {
                    spawnedNames.Add(mobCharacter.Name + " " + mobCharacter.Identity.ToString(true));
                }
            }

            character.Playfield.Publish(
                ChatTextMessageHandler.Default.CreateIM(
                    character,
                    string.Format(
                        "Spawned {0} DB population mobs for playfield {1}: {2}.",
                        spawnedNames.Count,
                        character.Playfield.Identity.Instance,
                        string.Join(", ", spawnedNames))));
        }

        private Character SpawnPopulationMob(
            ICharacter character,
            CombatTestMobArchetype.Entry entry,
            float xOffset,
            float zOffset)
        {
            Coordinate spawnCoordinate = new Coordinate(character.Coordinates());
            spawnCoordinate.x += xOffset;
            spawnCoordinate.z += zOffset;

            var npcController = new NPCController();
            Character mobCharacter = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                entry.TemplateHash,
                character.Playfield.Identity,
                spawnCoordinate,
                character.RawHeading,
                npcController,
                entry.Level);

            if (mobCharacter == null)
            {
                character.Playfield.Publish(
                    ChatTextMessageHandler.Default.CreateIM(
                        character,
                        string.Format(
                            "Population mob spawn failed for {0} [{1}].",
                            entry.RuntimeName,
                            entry.TemplateHash)));
                return null;
            }

            mobCharacter.Playfield = character.Playfield;
            CombatTestMobArchetype.Prepare(mobCharacter, entry);
            mobCharacter.DoNotDoTimers = false;

            character.Playfield.Announce(SimpleCharFullUpdate.ConstructMessage(mobCharacter));
            character.Playfield.Announce(new CharInPlayMessage { Identity = mobCharacter.Identity, Unknown = 0x00 });
            AppearanceUpdateMessageHandler.Default.Send(mobCharacter);

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "DB population mob spawned template={0} name={1} identity={2} pf={3} pos={4:0.00},{5:0.00},{6:0.00} hp={7}/{8} monsterData={9} catMesh={10}",
                    entry.TemplateHash,
                    mobCharacter.Name,
                    mobCharacter.Identity.ToString(true),
                    character.Playfield.Identity.ToString(true),
                    mobCharacter.RawCoordinates.X,
                    mobCharacter.RawCoordinates.Y,
                    mobCharacter.RawCoordinates.Z,
                    mobCharacter.Stats[StatIds.health].Value,
                    mobCharacter.Stats[StatIds.life].Value,
                    mobCharacter.Stats[StatIds.monsterdata].Value,
                    mobCharacter.Stats[StatIds.catmesh].Value));

            return mobCharacter;
        }

        private void SpawnCombatTestMob(ICharacter character, CombatTestMobArchetype.Entry entry)
        {
            this.SpawnCombatTestMob(character, entry, 5.0f);
        }

        private Character SpawnCombatTestMob(ICharacter character, CombatTestMobArchetype.Entry entry, float zOffset)
        {
            Character mobCharacter = CombatTestMobArchetype.SpawnNear(character, entry, zOffset);

            if (mobCharacter == null)
            {
                character.Playfield.Publish(
                    ChatTextMessageHandler.Default.CreateIM(
                        character,
                        string.Format("Combat test mob spawn failed for {0}.", entry.DisplayName)));
                return null;
            }

            character.Playfield.Announce(SimpleCharFullUpdate.ConstructMessage(mobCharacter));
            character.Playfield.Announce(new CharInPlayMessage { Identity = mobCharacter.Identity, Unknown = 0x00 });
            AppearanceUpdateMessageHandler.Default.Send(mobCharacter);

            character.Playfield.Publish(
                ChatTextMessageHandler.Default.CreateIM(
                    character,
                    string.Format(
                        "Spawned {0} {1}.",
                        entry.DisplayName,
                        mobCharacter.Identity.ToString(true))));

            return mobCharacter;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        public override int GMLevelNeeded()
        {
            return 1;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        public override List<string> ListCommands()
        {
            return new List<string> { "spawn", "spawnleet", "spawnrandom", "spawncount" };
        }

        #endregion
    }
}
