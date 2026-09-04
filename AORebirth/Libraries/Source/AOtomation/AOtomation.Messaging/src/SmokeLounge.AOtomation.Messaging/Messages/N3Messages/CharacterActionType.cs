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

        /// <summary>
        /// Client→server: leave team (UI). Capture 20260727-065826 Action=0x18.
        /// </summary>
        LeaveTeam = 0x00000018,

        /// <summary>
        /// Client→server: invitee Accept/Decline. Capture 20260728-234012 Action=0x1C.
        /// Parameter2=1 accept (Target=inviter); decline uses 0 or 20.
        /// </summary>
        ClientTeamInviteReply = 0x0000001C,

        /// <summary>
        /// Server→client: member left the team. Capture 20260727-065826 Action=0x20.
        /// Parameter1 = team instance; Parameter2 = -1; Target = leaving character.
        /// </summary>
        TeamMemberLeft = 0x00000020,

        AcceptTeamRequest = 0x00000023,

        RemoveFriendlyNano = 0x00000041,

        UseItemOnItem = 0x00000051,

        /// <summary>
        /// Server→client: perk action queued (capture 20260715-194155 UsePerk reply).
        /// </summary>
        QueuePerk = 0x00000050,

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

        /// <summary>
        /// Server→client: NPC social/idle gesture. Target.Instance = animation id.
        /// Capture 20260719-Natalia-Akcoraanimation (Action=100 / 0x64).
        /// </summary>
        NpcSocialAnim = 0x00000064,

        Logout = 0x00000078,

        StopLogout = 0x0000007A,

        Equip = 0x00000083,

        SpecialUnavailable = 0x00000084,

        Die = 0x00000098,

        StartedSneaking = 0x000000A2,

        StartSneak = 0x000000A3,

        /// <summary>
        /// Client→server: leave sneak. Action=0xAD.
        /// </summary>
        StopSneaking = 0x000000AD,

        SpecialAvailable = 0x000000A4,

        DisableXP = 0x000000A5,

        Search = 0x00000066,

        ChangeVisualFlag = 0x000000A6,

        ChangeAnimationAndStance = 0x000000A7,

        SpecialUsed = 0x000000AA,

        DeathRespawn = 0x000000AB,

        /// <summary>
        /// Client→server: use a trained perk action (Perk Actions menu). Capture 20260715-194155.
        /// </summary>
        UsePerk = 0x000000B3,

        /// <summary>
        /// Server→client: grant a Perk Actions button for a trained perk. Capture 20260715-194155.
        /// Target.Instance = action template id; Parameter1 = 10000+PacketID; Parameter2 = 4-char action hash.
        /// </summary>
        AddPerkAction = 0x000000B4,

        /// <summary>
        /// Server→client: remove a Perk Actions button. Capture 20260716-Reset-perks (Action=182).
        /// Same Parameter layout as AddPerkAction.
        /// </summary>
        RemovePerkAction = 0x000000B6,

        /// <summary>
        /// Client↔server: train a perk by PacketID (Parameter2). Capture 20260715-194155.
        /// </summary>
        TrainPerk = 0x000000BB,

        /// <summary>
        /// Server→client: all trained perks cleared (full reset). Capture 20260716-Reset-perks (Action=201).
        /// </summary>
        ClearAllPerks = 0x000000C9,

        /// <summary>
        /// Client→server: Actions → Normal Actions → Reload (hotkey V). Capture 20260728-221109.
        /// </summary>
        Reload = 0x000000D2,

        /// <summary>
        /// Client→server: Character Info → Inspect Equipment.
        /// Server replies with InspectMessage. Capture 20260719-182611 (Action=0x105).
        /// </summary>
        Inspect = 0x00000105,

        SitDown = 0x00000107,

        UploadNano = 0x000000CC,

        /// <summary>
        /// Server→client: perk action off cooldown. Parameter2 = PacketID. Capture 20260715-194155.
        /// </summary>
        PerkAvailable = 0x000000CE,

        /// <summary>
        /// Server→client: perk action on cooldown. Parameter1 = PacketID. Capture 20260715-194155.
        /// </summary>
        PerkUnavailable = 0x000000CF,

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

        /// <summary>
        /// Server→inviter TooLow Yes/No warn. Capture 20260815-222131 (Bluehot 200 →
        /// Nediraj / Nicoldoc): after OUT 0x1A p2=0, Action=0xA8 Target=invitee.
        /// Yes is OUT 0x1A p2=1. Same layout as TooHigh 0xA9.
        /// </summary>
        TeamInviteTooLow = 0x000000A8,

        /// <summary>
        /// Server→inviter TooHigh Yes/No warn. Capture 20260815-194517 Action=0xA9.
        /// Same Target as the invite; parameters 0.
        /// </summary>
        TeamInviteAck = 0x000000A9,
 
		Split = 0x00000022,
    }
}
