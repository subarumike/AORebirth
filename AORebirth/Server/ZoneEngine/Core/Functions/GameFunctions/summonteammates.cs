#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core.Functions.GameFunctions
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// FunctionType.SummonTeamMates (53155).
    /// Nano 154913 Team Beacon Warp — warp all teammates to caster from any Rubi-Ka PF.
    /// </summary>
    internal class summonteammates : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.SummonTeamMates;
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

            List<Identity> members;
            if (!TeamWarpRuntime.TryGetTeamRoster(caster, out members))
            {
                ChatTextMessageHandler.Default.Send(
                    caster,
                    "Team Beacon Warp: you are not on a team.");
                return false;
            }

            int warped = 0;
            int slot = 0;
            int missing = 0;
            for (int i = 0; i < members.Count; i++)
            {
                Identity memberId = members[i];
                if (memberId.Instance == 0 || memberId.Instance == caster.Identity.Instance)
                {
                    continue;
                }

                ICharacter member = TeamWarpRuntime.FindOnlineTeamMember(memberId);
                if (member == null)
                {
                    missing++;
                    continue;
                }

                if (TeamWarpRuntime.TryWarpTeamMemberToCaster(
                    caster,
                    member,
                    slot,
                    "SummonTeamMates evidence=20260808-warp-team"))
                {
                    warped++;
                }

                slot++;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "SummonTeamMates evidence=20260808-warp-team caster={0} warped={1} missing={2} roster={3}",
                    caster.Identity.ToString(true),
                    warped,
                    missing,
                    members.Count));

            if (warped == 0)
            {
                ChatTextMessageHandler.Default.Send(
                    caster,
                    missing > 0
                        ? "Team Beacon Warp: no online teammates to warp."
                        : "Team Beacon Warp: no teammates to warp.");
            }

            return warped > 0;
        }
    }
}
