// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CharacterActionType.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the CharacterActionType type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    public enum CharacterActionType
    {
        TeamRequest = 0x0000001A,

        CastNano = 0x00000013,

        TeamRequestReply = 0x00000015,

        TeamKickMember = 0x00000016,

		LeaveTeam = 0x00000020,

        AcceptTeamRequest = 0x00000023,

        RemoveFriendlyNano = 0x00000041,

        UseItemOnItem = 0x00000051,

        StandUp = 0x00000057,

        Unknown3 = 0x00000061,

        SetNanoDuration = 0x00000062,

        ItemAnim = 0x00000063,

        Death = 0x00000063,

        InfoRequest = 0x00000069,

        FinishNanoCasting = 0x0000006B,

        InterruptNanoCasting = 0x0000006C,

        UseActionFinished = 0x0000006E,

        DeleteItem = 0x00000070,

        Logout = 0x00000078,

        StopLogout = 0x0000007A,

        Equip = 0x00000083,

        SpecialUnavailable = 0x00000084,

        Die = 0x00000098,

        StartedSneaking = 0x000000A2,

        StartSneak = 0x000000A3,

        SpecialAvailable = 0x000000A4,

        DisableXP = 0x000000A5,

        Search = 0x00000066,

        ChangeVisualFlag = 0x000000A6,

        ChangeAnimationAndStance = 0x000000A7,

        SpecialUsed = 0x000000AA,

        DeathRespawn = 0x000000AB,

        SitDown = 0x00000107,

        UploadNano = 0x000000CC,

        TradeskillSourceChanged = 0x000000DC,

        TradeskillTargetChanged = 0x000000DD,

        TradeskillBuildPressed = 0x000000DE,

        TradeskillSource = 0x000000DF,

        TradeskillTarget = 0x000000E0,

        TradeskillNotValid = 0x000000E1,

        TradeskillOutOfRange = 0x000000E2,

        TradeskillRequirement = 0x000000E3,

        TradeskillResult = 0x000000E4,
        
		TransferLeader = 0x00000019,
        
		TeamRequestInvite = 0x0000001A,
 
		Split = 0x00000022,
    }
}
