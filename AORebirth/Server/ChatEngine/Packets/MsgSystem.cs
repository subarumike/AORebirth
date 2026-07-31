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

namespace ChatEngine.Packets
{
    /// <summary>
    /// Owner-only brown pet announce — AOSharp NpcMessage / ChatMessageType.NpcMessage = 35.
    /// Not Vicinity (34). Not wiki SimpleSystemMessage (36).
    /// </summary>
    public static class MsgSystem
    {
        #region Public Methods and Operators

        /// <summary>
        /// Capture 20260731-054922 / 20260731-pet-chat:
        /// AOSharp NpcMessage [AoContract(35)]: short Unk1, string Text, short Unk2.
        /// Live values Unk1=0 Unk2=1. Owner chat client only.
        /// </summary>
        public static byte[] Create(string message)
        {
            return Create(message, 0, 1);
        }

        /// <summary>
        /// Wire: type 35 | payloadLen | i16be Unk1 | AO string | i16be Unk2.
        /// </summary>
        public static byte[] Create(string message, int unk1, int unk2)
        {
            PacketWriter writer = new PacketWriter((ushort)MessageType.AnonymousMessage);
            writer.WriteUInt16(unchecked((ushort)(short)unk1));
            writer.WriteString(message ?? string.Empty);
            writer.WriteUInt16(unchecked((ushort)(short)unk2));
            return writer.Finish();
        }

        #endregion
    }
}
