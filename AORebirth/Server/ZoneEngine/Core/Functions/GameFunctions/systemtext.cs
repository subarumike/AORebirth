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

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    /// <summary>
    /// </summary>
    internal class systemtext : FunctionPrototype
    {
        #region Public Properties

        /// <summary>
        /// </summary>
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.SystemText;
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="self">
        /// </param>
        /// <param name="caller">
        /// </param>
        /// <param name="target">
        /// </param>
        /// <param name="arguments">
        /// </param>
        /// <returns>
        /// </returns>
        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            ICharacter character = self as ICharacter;
            if (character == null || arguments == null || arguments.Length < 1)
            {
                return false;
            }

            string text = arguments[0].AsString();

            // Token-board OnUse: "Side tokens collected: %d." / variants (capture 20260723-123341).
            if (text != null && text.IndexOf("%d", System.StringComparison.Ordinal) >= 0)
            {
                text = text.Replace("%d", GetSideTokenCount(character).ToString());
            }

            // Capture 20260723-123341: yellow system chat needs AO format prefix or client
            // shows an empty yellow line.
            text = TokenBoardRuntime.ToYellowSystemFeedback(text);

            var message = new FormatFeedbackMessage()
                          {
                              Identity = character.Identity,
                              Unknown = 1,
                              FormattedMessage = text,
                              Unknown1 = 0,
                              Unknown2 = 0,
                          };
            character.Send(message);
            return true;
        }

        /// <summary>
        /// Clan = alignment (62), Omni = metatype (75). Neutral/other → 0.
        /// </summary>
        private static int GetSideTokenCount(ICharacter character)
        {
            int side = character.Stats[StatIds.side].Value;
            if (side == (int)Side.Clan)
            {
                return character.Stats[StatIds.alignment].Value;
            }

            if (side == (int)Side.Omni)
            {
                return character.Stats[StatIds.metatype].Value;
            }

            return 0;
        }

        #endregion
    }
}
