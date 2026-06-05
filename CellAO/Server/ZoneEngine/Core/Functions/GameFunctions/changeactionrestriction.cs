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

    using CellAO.Core.Entities;
    using CellAO.Enums;
    using CellAO.Interfaces;

    using MsgPack;

    using Utility;

    #endregion

    internal class changeactionrestriction : FunctionPrototype
    {
        private const int ImplantAccessFlag = 0x10;

        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.ChangeActionRestriction;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            Character character = self as Character;
            if (character == null || arguments == null || arguments.Length < 2)
            {
                return false;
            }

            int actionFlags = arguments[0].AsInt32();
            int durationSeconds = arguments[1].AsInt32();
            if ((actionFlags & ImplantAccessFlag) == 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.GameFunctions,
                    string.Format(
                        "ChangeActionRestriction ignored unsupported flags={0} duration={1}",
                        actionFlags,
                        durationSeconds));
                return true;
            }

            character.GrantImplantAccess(durationSeconds);
            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                string.Format(
                    "ChangeActionRestriction implant access char={0} duration={1}",
                    character.Identity,
                    durationSeconds));
            return true;
        }
    }
}
