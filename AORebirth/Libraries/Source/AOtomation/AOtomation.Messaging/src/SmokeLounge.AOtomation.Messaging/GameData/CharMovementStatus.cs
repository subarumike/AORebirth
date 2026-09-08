// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CharMovementStatus.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the CharMovementStatus type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Explicit, Size = 0x1a)]
    public struct CharMovementStatus
    {
        public const int Size = 0x19;

        public const int NpcWireSize = 0x1c;

        public const int PlayerWireSize = 0x2a;

        /// <summary>0x80 on player Unknown1, 0 on NPC.</summary>
        [FieldOffset(0x00)] public byte Header;

        [FieldOffset(0x0c)] public byte ModeId;

        /// <summary>1=stopped, 2=moving</summary>
        [FieldOffset(0x0d)] public byte FwdState;

        /// <summary>0=none, 1=forward, 2=reverse</summary>
        [FieldOffset(0x0e)] public byte FwdDir;

        /// <summary>1=none, 2=strafing</summary>
        [FieldOffset(0x0f)] public byte StrafeState;

        /// <summary>0, 3=left, 4=right</summary>
        [FieldOffset(0x10)] public byte StrafeDir;

        [FieldOffset(0x11)] public byte ElevateState;

        /// <summary>0, 5=up</summary>
        [FieldOffset(0x12)] public byte ElevateDir;

        /// <summary>1=none, 4=turning</summary>
        [FieldOffset(0x13)] public byte TurnState;

        /// <summary>0, 3=left, 4=right</summary>
        [FieldOffset(0x14)] public byte TurnDir;

        /// <summary>1=none, 3=jumping</summary>
        [FieldOffset(0x15)] public byte JumpState;

        /// <summary>2=walk, 3=run</summary>
        [FieldOffset(0x19)] public byte LastSpeedMode;

        public static CharMovementStatus FromBytes(byte[] data)
        {
            if (data == null || data.Length < Size)
                throw new ArgumentException($"Expected at least {Size} bytes.", nameof(data));

            var result = new CharMovementStatus
            {
                Header = data[0x00],
                ModeId = data[0x0c],
                FwdState = data[0x0d],
                FwdDir = data[0x0e],
                StrafeState = data[0x0f],
                StrafeDir = data[0x10],
                ElevateState = data[0x11],
                ElevateDir = data[0x12],
                TurnState = data[0x13],
                TurnDir = data[0x14],
                JumpState = data[0x15],
            };

            if (data.Length >= 0x1a)
                result.LastSpeedMode = data[0x19];

            return result;
        }

        public byte[] ToBytes()
        {
            var data = new byte[Header == 0x80 ? PlayerWireSize : NpcWireSize];
            data[0x00] = Header;
            data[0x0c] = ModeId;
            data[0x0d] = FwdState;
            data[0x0e] = FwdDir;
            data[0x0f] = StrafeState;
            data[0x10] = StrafeDir;
            data[0x11] = ElevateState;
            data[0x12] = ElevateDir;
            data[0x13] = TurnState;
            data[0x14] = TurnDir;
            data[0x15] = JumpState;
            data[0x19] = LastSpeedMode;
            return data;
        }
    }
}
