#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core.Functions.GameFunctions
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// FunctionType.SummonPlayer (53154).
    /// Nano 154914 Beacon Warp — warp selected team member to caster from any Rubi-Ka PF.
    /// </summary>
    internal class summonplayer : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.SummonPlayer;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            ICharacter caster = self as ICharacter;
            if (caster == null || caster.Playfield == null)
            {
                return false;
            }

            if (TeamWarpRuntime.TryRefuseShadowlands(caster))
            {
                return false;
            }

            Identity selected = caster.SelectedTarget;
            if (selected.Instance == 0 || selected.Instance == caster.Identity.Instance)
            {
                ChatTextMessageHandler.Default.Send(
                    caster,
                    "Beacon Warp: select a team member first.");
                return false;
            }

            List<Identity> members;
            if (!TeamWarpRuntime.TryGetTeamRoster(caster, out members))
            {
                ChatTextMessageHandler.Default.Send(
                    caster,
                    "Beacon Warp: you are not on a team.");
                return false;
            }

            bool onTeam = false;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Instance == selected.Instance)
                {
                    onTeam = true;
                    break;
                }
            }

            if (!onTeam)
            {
                ChatTextMessageHandler.Default.Send(
                    caster,
                    "Beacon Warp: target is not on your team.");
                return false;
            }

            ICharacter member = TeamWarpRuntime.FindOnlineTeamMember(selected);
            if (member == null)
            {
                ChatTextMessageHandler.Default.Send(
                    caster,
                    "Beacon Warp: team member is not online.");
                return false;
            }

            bool warped = TeamWarpRuntime.TryWarpTeamMemberToCaster(
                caster,
                member,
                0,
                "SummonPlayer evidence=20260808-Warp-single");
            if (!warped)
            {
                ChatTextMessageHandler.Default.Send(
                    caster,
                    "Beacon Warp: could not warp that team member here.");
            }

            return warped;
        }
    }
}
