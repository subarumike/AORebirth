#region License



// Copyright (c) 2005-2014, CellAO Team

//

// All rights reserved.



#endregion



namespace AORebirth.Communication.Messages

{

    /// <summary>

    /// Zone → ChatEngine: owner-only brown pet announce.

    /// Capture 20260731-054922: AOSharp NpcMessage PacketType=NpcMessage (=35)

    /// Unk1=0 Text="Owner's pet, Name: …" Unk2=1.

    /// Delivered only to the owner's chat client. Never Vicinity (34).

    /// </summary>

    public class SystemChatMessage : MessageBase

    {

        public int CharacterId { get; set; }



        /// <summary>Fallback when CharacterId lookup misses on ChatEngine.</summary>

        public string CharacterName { get; set; }



        public string Text { get; set; }



        /// <summary>AOSharp NpcMessage.Unk1 — capture always 0.</summary>

        public int Unk1 { get; set; }



        /// <summary>AOSharp NpcMessage.Unk2 — capture always 1.</summary>

        public int Unk2 { get; set; }

    }

}
