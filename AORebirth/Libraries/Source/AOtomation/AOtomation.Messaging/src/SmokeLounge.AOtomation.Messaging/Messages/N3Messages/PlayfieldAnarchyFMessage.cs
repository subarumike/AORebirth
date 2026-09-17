// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PlayfieldAnarchyFMessage.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the PlayfieldAnarchyFMessage type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    /// <summary>
    /// Client type <c>PlayfieldAnarchyFIIR_t</c> (extends <c>n3PlayfieldFullUpdateIIR_t</c>).
    /// Wire layout from N3 <c>ReadSubClass</c>/<c>WriteSubClass</c> plus Gamecode override:
    /// version, spawn XYZ, PlayfieldProxy ('a' + two identities + two ints),
    /// optional generator DbObject (or Identity 0:0), then PF world X/Z.
    /// </summary>
    [AoContract((int)N3MessageType.PlayfieldAnarchyF)]
    public class PlayfieldAnarchyFMessage : N3Message
    {
        #region Constructors and Destructors

        public PlayfieldAnarchyFMessage()
        {
            this.N3MessageType = N3MessageType.PlayfieldAnarchyF;
            this.Unknown = 0x00;
            this.Version = 0x00000004;
            this.PlayfieldProxyVersion = (byte)'a';
        }

        #endregion

        #region AoMember Properties

        /// <summary>n3PlayfieldFullUpdateIIR version; live uses 4 (generator slot present).</summary>
        [AoMember(0)]
        public int Version { get; set; }

        /// <summary>Character spawn coordinates (Vector3_t at IIR +0x1C).</summary>
        [AoMember(1)]
        public Vector3 CharacterCoordinates { get; set; }

        /// <summary>PlayfieldProxy stream version; client requires <c>'a'</c> (0x61).</summary>
        [AoMember(2)]
        public byte PlayfieldProxyVersion { get; set; }

        /// <summary>PlayfieldProxy identity (Gamecode PlayfieldId1 / proxy id).</summary>
        [AoMember(3)]
        public Identity PlayfieldId1 { get; set; }

        /// <summary>PlayfieldProxy int after id1 (org-building / unused on static PFs).</summary>
        [AoMember(4)]
        public int Unknown3 { get; set; }

        /// <summary>PlayfieldProxy int after Unknown3.</summary>
        [AoMember(5)]
        public int Unknown4 { get; set; }

        /// <summary>PlayfieldProxy second identity (usually Playfield2 matching N3 identity).</summary>
        [AoMember(6)]
        public Identity PlayfieldId2 { get; set; }

        /// <summary>
        /// Optional generator DbObject blob including its leading Identity.
        /// Null means write Identity 0:0 (no generator). Must not include trailing PF world X/Z.
        /// When this is an ACG building/entrance v3 blob, see <see cref="AcgBuildingGenerator"/>.
        /// </summary>
        public byte[] GeneratorPayload { get; set; }

        /// <summary>
        /// Parsed <c>ACGBuildingGeneratorData_t</c> when <see cref="GeneratorPayload"/> is a v3 ACG blob.
        /// </summary>
        public AcgBuildingGeneratorData AcgBuildingGenerator { get; set; }

        /// <summary>PFWorldXPos (PlayfieldAnarchyFIIR +0x44). Generated PFs often use -1.</summary>
        [AoMember(7)]
        public int PlayfieldX { get; set; }

        /// <summary>PFWorldZPos (PlayfieldAnarchyFIIR +0x48). Generated PFs often use -1.</summary>
        [AoMember(8)]
        public int PlayfieldZ { get; set; }

        #endregion
    }
}
