// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TextureOverride.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the TextureOverride type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    /// <summary>
    /// One SimpleCharFullUpdate extended texture entry (flag HasExtendedTextures): puts a texture on a named material
    /// of the character's mesh. Wire: 32-byte NUL-padded ASCII material name, then texture id and two ints.
    /// </summary>
    public class TextureOverride
    {
        /// <summary>Width of the material name on the wire.</summary>
        public const int MaterialNameLength = 32;

        /// <summary>Bytes one entry takes on the wire.</summary>
        public const int WireSize = MaterialNameLength + 12;

        #region Public Properties

        /// <summary>Mesh material the texture goes on, e.g. "Material #9".</summary>
        public string Material { get; set; }

        public int Texture { get; set; }

        public int Unknown1 { get; set; }

        public int Unknown2 { get; set; }

        #endregion
    }
}
