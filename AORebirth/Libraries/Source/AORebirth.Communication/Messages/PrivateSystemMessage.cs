#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace AORebirth.Communication.Messages
{
    /// <summary>
    /// Zone → ChatEngine: PrivateMsg for Daily Login claim feedback.
    /// Capture 20260806-063619: Sender=0 Text="1 rewards claimed." Unk1=3 Unk2=0.
    /// Empty claim: Text="You currently have no pending reward items."
    /// </summary>
    public class PrivateSystemMessage : MessageBase
    {
        public int CharacterId { get; set; }

        public string CharacterName { get; set; }

        public string Text { get; set; }

        public int Unk1 { get; set; }

        public int Unk2 { get; set; }
    }
}
