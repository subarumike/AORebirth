#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace AORebirth.Communication.Messages
{
    /// <summary>
    /// Zone → ChatEngine: owner-only Your Pets announce (chat type 35).
    /// Live 20260731-085057: empty source + Text=
    ///   "{owner}'s pet, {pet}: {line}"
    /// Client shows brown ": {Text}" and gates on Public Groups → Your Pets.
    /// </summary>
    public class SystemChatMessage : MessageBase
    {
        public int CharacterId { get; set; }

        /// <summary>Fallback when CharacterId lookup misses on ChatEngine.</summary>
        public string CharacterName { get; set; }

        /// <summary>
        /// Unused on live wire (Unk1=0 / empty). Kept for join fallback if set.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Full live line, e.g. "Catcraty's pet, Bureaucrat Worker: Charge!".
        /// </summary>
        public string Text { get; set; }

        public int Unk1 { get; set; }

        public int Unk2 { get; set; }
    }
}
