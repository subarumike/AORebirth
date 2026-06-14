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

    using System.Text;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Textures;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using Utility;

    #endregion

    /// <summary>
    /// Generic mesh function used by equipment events (including weapons in hands).
    /// </summary>
    internal class Function_mesh : FunctionPrototype
    {
        #region Constants

        /// <summary>
        /// </summary>
        private const FunctionType functionId = FunctionType.Mesh;

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

        #endregion

        #region Methods

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
        private bool FunctionExecute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            Character character = self as Character;
            if (character == null || arguments == null || arguments.Length == 0)
            {
                return false;
            }

            int placement = 0;
            int meshId;
            int overrideTexture;

            if (this.TryGetPlacement(arguments, out placement))
            {
                if (arguments.Length >= 3)
                {
                    // Expected form: overrideTexture, meshId, slot
                    overrideTexture = arguments[0].AsInt32();
                    meshId = arguments[1].AsInt32();
                }
                else
                {
                    // Expected form: meshId, slot
                    overrideTexture = 0;
                    meshId = arguments[0].AsInt32();
                }
            }
            else
            {
                // Fallback for mesh calls without slot argument
                overrideTexture = arguments[0].AsInt32();
                meshId = arguments.Length >= 2 ? arguments[1].AsInt32() : arguments[0].AsInt32();
            }

            int position = this.GetMeshPositionFromPlacement(placement);
            int layer = MeshLayers.GetLayer(placement);

            bool social = placement >= 49;
            if (social)
            {
                character.SocialMeshLayer.AddMesh(position, meshId, overrideTexture, layer);
            }
            else
            {
                character.MeshLayer.AddMesh(position, meshId, overrideTexture, layer);
                this.UpdateMeshStats(character, position, meshId);
            }

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "Function_mesh char={0} placement={1} position={2} layer={3} social={4} mesh={5} override={6} args={7}",
                    character.Identity,
                    placement,
                    position,
                    layer,
                    social ? 1 : 0,
                    meshId,
                    overrideTexture,
                    this.FormatArguments(arguments)));

            character.ChangedAppearance = true;
            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="placement">
        /// </param>
        /// <returns>
        /// </returns>
        private int GetMeshPositionFromPlacement(int placement)
        {
            switch (placement)
            {
                case 6: // Right hand
                case 56: // Social right hand
                    return 1;
                case 8: // Left hand
                case 58: // Social left hand
                    return 2;
                case 20: // Shoulder right
                case 52: // Social shoulder right
                    return 3;
                case 22: // Shoulder left
                case 54: // Social shoulder left
                    return 4;
                case 19: // Back
                case 51: // Social back
                    return 5;
                case 18: // Head
                case 50: // Social head
                    return 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="arguments">
        /// </param>
        /// <param name="placement">
        /// </param>
        /// <returns>
        /// </returns>
        private bool TryGetPlacement(MessagePackObject[] arguments, out int placement)
        {
            placement = 0;
            if (arguments == null || arguments.Length < 2)
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

            // Inventory/equipment slot numbers used by Item.PerformAction.
            if (candidate >= 1 && candidate <= 100)
            {
                placement = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="arguments">
        /// </param>
        /// <returns>
        /// </returns>
        private string FormatArguments(MessagePackObject[] arguments)
        {
            if (arguments == null)
            {
                return "<null>";
            }

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
                    if (arguments[i].IsTypeOf<int>() == true)
                    {
                        sb.Append(arguments[i].AsInt32());
                    }
                    else if (arguments[i].IsTypeOf<uint>() == true)
                    {
                        sb.Append(arguments[i].AsUInt32());
                    }
                    else if (arguments[i].IsTypeOf<string>() == true)
                    {
                        sb.Append('"').Append(arguments[i].AsString()).Append('"');
                    }
                    else
                    {
                        sb.Append(arguments[i].ToString());
                    }
                }
                catch
                {
                    sb.Append(arguments[i].ToString());
                }
            }

            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="position">
        /// </param>
        /// <param name="meshId">
        /// </param>
        private void UpdateMeshStats(Character character, int position, int meshId)
        {
            switch (position)
            {
                case 1:
                    character.Stats[StatIds.weaponmeshright].Value = meshId;
                    break;
                case 2:
                    character.Stats[StatIds.weaponmeshleft].Value = meshId;
                    break;
                case 3:
                    character.Stats[StatIds.shouldermeshright].Value = meshId;
                    break;
                case 4:
                    character.Stats[StatIds.shouldermeshleft].Value = meshId;
                    break;
                case 5:
                    character.Stats[StatIds.backmesh].Value = meshId;
                    break;
            }
        }

        #endregion
    }
}
