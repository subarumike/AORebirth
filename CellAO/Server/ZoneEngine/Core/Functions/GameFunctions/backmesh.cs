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
    using System.Text;

    using CellAO.Core.Entities;
    using CellAO.Core.Textures;
    using CellAO.Enums;
    using CellAO.Interfaces;

    using MsgPack;

    using Utility;

    #endregion

    /// <summary>
    /// </summary>
    internal class backmesh : FunctionPrototype
    {
        #region Constants

        /// <summary>
        /// </summary>
        private const FunctionType functionId = FunctionType.BackMesh;

        #endregion

        #region Public Properties

        /// <summary>
        /// </summary>
        public override FunctionType FunctionId
        {
            get
            {
                return functionId;
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
            lock (target)
            {
                return this.FunctionExecute(self, caller, target, arguments);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="Self">
        /// </param>
        /// <param name="Caller">
        /// </param>
        /// <param name="Target">
        /// </param>
        /// <param name="Arguments">
        /// </param>
        /// <returns>
        /// </returns>
        public bool FunctionExecute(
            INamedEntity Self,
            IEntity Caller,
            IInstancedEntity Target,
            MessagePackObject[] Arguments)
        {
            Character character = Self as Character;
            if (character == null || Arguments == null || Arguments.Length == 0)
            {
                return false;
            }

            int placement;
            int meshId;
            int overrideTexture;
            if (this.TryGetPlacement(Arguments, out placement))
            {
                if (Arguments.Length >= 3)
                {
                    // Expected form after inventory appends the equipment slot:
                    // overrideTexture, meshId, slot
                    overrideTexture = Arguments[0].AsInt32();
                    meshId = Arguments[1].AsInt32();
                }
                else
                {
                    // Expected form after inventory appends the equipment slot:
                    // meshId, slot
                    overrideTexture = 0;
                    meshId = Arguments[0].AsInt32();
                }
            }
            else if (Arguments.Length >= 2)
            {
                // Legacy form without an appended slot: overrideTexture, meshId
                placement = 19;
                overrideTexture = Arguments[0].AsInt32();
                meshId = Arguments[1].AsInt32();
            }
            else
            {
                // Legacy form without an appended slot: meshId
                placement = 19;
                overrideTexture = 0;
                meshId = Arguments[0].AsInt32();
            }

            bool social = placement == 51;
            int layer = MeshLayers.GetLayer(placement);
            if (social)
            {
                character.SocialMeshLayer.AddMesh(5, meshId, overrideTexture, layer);
            }
            else
            {
                character.Stats[StatIds.backmesh].Value = meshId;
                character.MeshLayer.AddMesh(5, meshId, overrideTexture, layer);
            }

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "Function_backmesh char={0} placement={1} position=5 layer={2} social={3} mesh={4} override={5} args={6}",
                    character.Identity,
                    placement,
                    layer,
                    social ? 1 : 0,
                    meshId,
                    overrideTexture,
                    this.FormatArguments(Arguments)));

            character.ChangedAppearance = true;
            return true;
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

        private string FormatArguments(MessagePackObject[] arguments)
        {
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                try
                {
                    sb.Append(arguments[i].AsInt32());
                }
                catch
                {
                    sb.Append(arguments[i].ToString());
                }
            }

            sb.Append("]");
            return sb.ToString();
        }

        #endregion
    }
}
