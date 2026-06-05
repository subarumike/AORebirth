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

namespace ZoneEngine.Core.Functions.GameFunctions
{
    #region Usings ...

    using System;

    using CellAO.Core.Entities;
    using CellAO.Enums;
    using CellAO.Interfaces;

    using MsgPack;

    using Utility;

    #endregion

    internal class lockskill : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.LockSkill;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            Character character = self as Character;
            int statId;
            int durationSeconds;

            if (character == null || !TryReadArguments(arguments, out statId, out durationSeconds))
            {
                return false;
            }

            character.LockSkill(statId, durationSeconds);

            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                string.Format(
                    "LockSkill char={0} stat={1} duration={2}",
                    character.Identity,
                    statId,
                    durationSeconds));

            return true;
        }

        public static bool TryReadArguments(MessagePackObject[] arguments, out int statId, out int durationSeconds)
        {
            statId = 0;
            durationSeconds = 0;

            if (arguments == null || arguments.Length < 2)
            {
                return false;
            }

            int first = arguments[0].AsInt32();
            int second = arguments[1].AsInt32();

            if (Enum.IsDefined(typeof(StatIds), first))
            {
                statId = first;
                durationSeconds = second;
                return durationSeconds > 0;
            }

            if (arguments.Length >= 3)
            {
                statId = second;
                durationSeconds = arguments[2].AsInt32();
                return durationSeconds > 0;
            }

            return false;
        }
    }
}
