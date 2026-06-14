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

    using AORebirth.Core.Actions;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    public class FindWeaponVisual : AOChatCommand
    {
        public override bool CheckCommandArguments(string[] args)
        {
            if (args.Length == 1)
            {
                return true;
            }

            if (args.Length == 2)
            {
                int count;
                return int.TryParse(args[1], out count);
            }

            return false;
        }

        public override void CommandHelp(ICharacter character)
        {
            character.Playfield.Publish(
                ChatTextMessageHandler.Default.CreateIM(character, "Usage: /command findweaponvisual [count]"));
        }

        public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
        {
            int count = 12;
            if (args.Length > 1)
            {
                int.TryParse(args[1], out count);
            }

            if (count < 1)
            {
                count = 1;
            }

            if (count > 50)
            {
                count = 50;
            }

            List<WeaponVisualCandidate> candidates = ItemLoader.ItemList.Values
                .Where(x => x != null)
                .Select(
                    x =>
                        new WeaponVisualCandidate
                        {
                            Id = x.ID,
                            Quality = x.Quality,
                            MeshRight = NormalizeVisualValue(x.getItemAttribute((int)StatIds.weaponmeshright)),
                            MeshLeft = NormalizeVisualValue(x.getItemAttribute((int)StatIds.weaponmeshleft)),
                            HasToWield = x.Actions.Any(a => a.ActionType == ActionType.ToWield),
                            HasMeshFunction =
                                x.Events
                                    .Where(e => e.EventType == EventType.OnWear || e.EventType == EventType.OnWield)
                                    .SelectMany(e => e.Functions)
                                    .Any(
                                        f =>
                                            f.FunctionType == (int)FunctionType.Mesh
                                            || f.FunctionType == (int)FunctionType.BackMesh
                                            || f.FunctionType == (int)FunctionType.HeadMesh
                                            || f.FunctionType == (int)FunctionType.ChangeBodyMesh)
                        })
                .Where(x => x.MeshRight > 0 || x.MeshLeft > 0 || x.HasMeshFunction)
                .OrderByDescending(x => x.HasToWield)
                .ThenByDescending(x => x.MeshRight)
                .ThenBy(x => x.Id)
                .Take(count)
                .ToList();

            int totalMeshStatItems = ItemLoader.ItemList.Values.Count(
                x =>
                    x != null
                    && (NormalizeVisualValue(x.getItemAttribute((int)StatIds.weaponmeshright)) > 0
                        || NormalizeVisualValue(x.getItemAttribute((int)StatIds.weaponmeshleft)) > 0));
            int totalMeshFunctionItems = ItemLoader.ItemList.Values.Count(
                x =>
                    x != null
                    && x.Events
                        .Where(e => e.EventType == EventType.OnWear || e.EventType == EventType.OnWield)
                        .SelectMany(e => e.Functions)
                        .Any(
                            f =>
                                f.FunctionType == (int)FunctionType.Mesh
                                || f.FunctionType == (int)FunctionType.BackMesh
                                || f.FunctionType == (int)FunctionType.HeadMesh
                                || f.FunctionType == (int)FunctionType.ChangeBodyMesh));
            int totalToWieldItems = ItemLoader.ItemList.Values.Count(
                x => x != null && x.Actions.Any(a => a.ActionType == ActionType.ToWield));

            character.Playfield.Publish(
                ChatTextMessageHandler.Default.CreateIM(
                    character,
                    string.Format(
                        "Weapon visual candidates: showing {0}. withMeshStat={1}, withMeshFunction={2}, withToWield={3}. Use /command giveitem <id> <ql>",
                        candidates.Count,
                        totalMeshStatItems,
                        totalMeshFunctionItems,
                        totalToWieldItems)));

            foreach (WeaponVisualCandidate candidate in candidates)
            {
                character.Playfield.Publish(
                    ChatTextMessageHandler.Default.CreateIM(
                        character,
                        string.Format(
                            "id={0} ql={1} meshR={2} meshL={3} toWield={4} meshFunc={5} name={6}",
                            candidate.Id,
                            candidate.Quality,
                            candidate.MeshRight,
                            candidate.MeshLeft,
                            candidate.HasToWield ? 1 : 0,
                            candidate.HasMeshFunction ? 1 : 0,
                            GetItemName(candidate.Id))));
            }
        }

        public override int GMLevelNeeded()
        {
            return 1;
        }

        public override List<string> ListCommands()
        {
            return new List<string>() { "findweaponvisual" };
        }

        private static int NormalizeVisualValue(int value)
        {
            if (value <= 0 || value == 1234567890)
            {
                return 0;
            }

            return value;
        }

        private static string GetItemName(int itemId)
        {
            DBItemName itemName = ItemNamesDao.Instance.Get(itemId);
            if (itemName == null || string.IsNullOrEmpty(itemName.Name))
            {
                return "(no name)";
            }

            return itemName.Name;
        }

        private class WeaponVisualCandidate
        {
            public int Id { get; set; }

            public int Quality { get; set; }

            public int MeshRight { get; set; }

            public int MeshLeft { get; set; }

            public bool HasToWield { get; set; }

            public bool HasMeshFunction { get; set; }
        }
    }
}
