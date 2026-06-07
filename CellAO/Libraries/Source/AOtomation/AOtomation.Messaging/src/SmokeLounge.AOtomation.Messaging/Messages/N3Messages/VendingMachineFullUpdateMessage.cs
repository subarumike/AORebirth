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

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    #endregion

    [AoContract((int)N3MessageType.VendingMachineFullUpdate)]
    public class VendingMachineFullUpdateMessage : N3Message
    {
        #region Constructors and Destructors

        public VendingMachineFullUpdateMessage()
        {
            this.N3MessageType = N3MessageType.VendingMachineFullUpdate;
        }

        #endregion

        #region AoMember Properties

        [AoMember(1)]
        public int TypeIdentifier { get; set; }

        // If NpcIdentity is Identity.None then dont serialize/deserialize Coordinates and Heading
        [AoMember(2)]
        public Identity NpcIdentity { get; set; }

        [AoMember(3)]
        public Vector3 Coordinates { get; set; }

        [AoMember(4)]
        public Quaternion Heading { get; set; }

        [AoMember(5)]
        public int PlayfieldId { get; set; }

        [AoMember(6)]
        public int Unknown4 { get; set; }

        [AoMember(7)]
        public int Unknown5 { get; set; }

        [AoMember(8)]
        public short Unknown6 { get; set; }

        [AoMember(9, SerializeSize = ArraySizeType.X3F1)]
        public GameTuple<CharacterStat, uint>[] Stats { get; set; }

        [AoMember(10, SerializeSize = ArraySizeType.Int32)]
        public string Unknown7 { get; set; }

        [AoMember(11)]
        public int Unknown8 { get; set; }

        [AoMember(12)]
        public int Unknown9 { get; set; }

        [AoMember(13, SerializeSize = ArraySizeType.X3F1)]
        public Identity[] Unknown10 { get; set; }

        [AoMember(14)]
        public int Unknown11 { get; set; }

        #endregion
    }
}