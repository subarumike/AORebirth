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

    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    #endregion

    [AoContract((int)N3MessageType.WeatherControl)]
    public class WeatherControlMessage : N3Message
    {
        public WeatherControlMessage()
        {
            this.N3MessageType = N3MessageType.WeatherControl;
        }

        [AoMember(1)]
        public short FadeIn { get; set; }

        [AoMember(2)]
        public int Duration { get; set; }

        [AoMember(3)]
        public short FadeOut { get; set; }

        [AoMember(4)]
        public Single Range { get; set; }

        [AoMember(5)]
        public byte WeatherType { get; set; }

        [AoMember(6)]
        public byte WeatherIntensity { get; set; }

        [AoMember(7)]
        public byte Wind { get; set; }

        [AoMember(8)]
        public byte Clouds { get; set; }

        [AoMember(9)]
        public byte Thunderstrikes { get; set; }

        [AoMember(10)]
        public byte Tremors { get; set; }

        [AoMember(11)]
        public byte TremorPercentage { get; set; }

        [AoMember(12)]
        public byte ThunderstrikePercentage { get; set; }

        [AoMember(13)]
        public byte CloudColorRed { get; set; }

        [AoMember(14)]
        public byte CloudColorGreen { get; set; }

        [AoMember(15)]
        public byte CloudColorBlue { get; set; }

        [AoMember(16)]
        public byte FogColorRed { get; set; }

        [AoMember(17)]
        public byte FogColorGreen { get; set; }

        [AoMember(18)]
        public byte FogColorBlue { get; set; }

        [AoMember(19)]
        public byte ZBufferVisibility { get; set; }

        [AoMember(20)]
        public Vector3 Position { get; set; }
        [AoMember(21)]
        public Single UnknownSingle { get; set; }
    }
}