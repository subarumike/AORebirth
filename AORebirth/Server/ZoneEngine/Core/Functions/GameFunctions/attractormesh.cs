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

    #endregion

    /// <summary>
    /// FunctionType.AttractorMesh (53055).
    /// Capture 20260806-132103 (Penumbra White Quabbit social head):
    /// AppearanceUpdate adds mesh 301724 at Position=0 Override=0 Layer=0 stacked with the real head.
    /// Hop animation is client-side on that mesh; no separate CastNano is required for the visual.
    /// </summary>
    internal class attractormesh : FunctionPrototype
    {
        #region Constants

        private const FunctionType functionId = FunctionType.AttractorMesh;

        /// <summary>
        /// Capture-backed attractor layer (stacks beside head layer 4).
        /// </summary>
        private const int AttractorLayer = 0;

        #endregion

        #region Public Properties

        public override FunctionType FunctionId
        {
            get
            {
                return functionId;
            }
        }

        #endregion

        #region Public Methods and Operators

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            if (target == null)
            {
                return this.FunctionExecute(self, arguments);
            }

            lock (target)
            {
                return this.FunctionExecute(self, arguments);
            }
        }

        #endregion

        #region Methods

        private bool FunctionExecute(INamedEntity self, MessagePackObject[] arguments)
        {
            Character character = self as Character;
            if (character == null || arguments == null || arguments.Length == 0)
            {
                return false;
            }

            int placement;
            int meshId;
            int overrideTexture;
            if (this.TryGetPlacement(arguments, out placement))
            {
                if (arguments.Length >= 3)
                {
                    overrideTexture = arguments[0].AsInt32();
                    meshId = arguments[1].AsInt32();
                }
                else
                {
                    overrideTexture = 0;
                    meshId = arguments[0].AsInt32();
                }
            }
            else if (arguments.Length >= 2)
            {
                placement = 18;
                overrideTexture = arguments[0].AsInt32();
                meshId = arguments[1].AsInt32();
            }
            else
            {
                placement = 18;
                overrideTexture = 0;
                meshId = arguments[0].AsInt32();
            }

            int position = this.GetMeshPositionFromPlacement(placement);
            bool social = placement >= 49;

            if (overrideTexture != 0)
            {
                character.Stats[StatIds.overridetextureattractor].Value = overrideTexture;
            }

            if (social)
            {
                character.SocialMeshLayer.AddMesh(position, meshId, overrideTexture, AttractorLayer);
            }
            else
            {
                character.MeshLayer.AddMesh(position, meshId, overrideTexture, AttractorLayer);
            }

            character.ChangedAppearance = true;
            return true;
        }

        private int GetMeshPositionFromPlacement(int placement)
        {
            switch (placement)
            {
                case 6:
                case 56:
                    return 1;
                case 8:
                case 58:
                    return 2;
                case 20:
                case 52:
                    return 3;
                case 22:
                case 54:
                    return 4;
                case 19:
                case 51:
                    return 5;
                case 18:
                case 50:
                default:
                    return 0;
            }
        }

        private bool TryGetPlacement(MessagePackObject[] arguments, out int placement)
        {
            placement = 0;
            if (arguments.Length < 2)
            {
                return false;
            }

            int candidate;
            try
            {
                candidate = arguments[arguments.Length - 1].AsInt32();
            }
            catch
            {
                return false;
            }

            if (candidate >= 1 && candidate <= 100)
            {
                placement = candidate;
                return true;
            }

            return false;
        }

        #endregion
    }
}
