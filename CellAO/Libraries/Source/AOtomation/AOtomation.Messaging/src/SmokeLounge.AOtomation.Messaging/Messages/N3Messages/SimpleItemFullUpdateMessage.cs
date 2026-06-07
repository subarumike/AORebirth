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

    [AoContract((int)N3MessageType.SimpleItemFullUpdate)]
    public class SimpleItemFullUpdateMessage : N3Message
    {

        public Identity Owner { get; set; }

        private int identityType;

        private int instance;

        public SimpleItemFullUpdateMessage()
        {
            this.N3MessageType = N3MessageType.SimpleItemFullUpdate;
        }

        [AoMember(1)]
        public int MsgVersion { get; set; }

        [AoMember(2)]
        [AoFlags("flag")]
        public int Identitytype
        {
            get
            {
                return this.identityType;
            }
            set
            {
                this.identityType = value;
                this.Owner = new Identity() { Type = (IdentityType)value, Instance = this.instance };
            }
        }

        [AoMember(3)]
        public int Instance
        {
            get
            {
                return this.instance;
            }
            set
            {
                this.instance = value;
                this.Owner = new Identity() { Type = (IdentityType)this.identityType, Instance = value };
            }
        }

        [AoMember(4)]
        [AoUsesFlags("flag", typeof(Vector3), FlagsCriteria.HasNone, new[] { int.MaxValue })]
        public Vector3 Coordinate { get; set; }

        [AoMember(5)]
        [AoUsesFlags("flag", typeof(Quaternion), FlagsCriteria.HasNone, new[] { int.MaxValue })]
        public Quaternion Heading { get; set; }

        // For items with owner (not dropped in world)
        [AoMember(6)]
        public int Playfield { get; set; }

        [AoMember(7)]
        public Identity Unknown1 { get; set; }

        [AoMember(8)]
        public byte Unknown2 { get; set; }

        [AoMember(9)]
        public byte Unknown3 { get; set; }

        // 3f1
        [AoMember(10, SerializeSize = ArraySizeType.X3F1)]
        public GameTuple<CharacterStat, uint>[] Stats { get; set; }

        [AoMember(11, SerializeSize = ArraySizeType.Int32)]
        public string Name { get; set; }

    }
}