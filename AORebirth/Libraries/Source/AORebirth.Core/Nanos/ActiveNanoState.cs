#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace AORebirth.Core.Nanos
{
    using System;

    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    public sealed class ActiveNanoState : IActiveNano
    {
        public int ID { get; set; }

        public int Instance { get; set; }

        public int Nanotype { get; set; }

        public int TickCounter { get; set; }

        public int TickInterval { get; set; }

        public int Value3 { get; set; }

        public int NcuCost { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public bool PlayfieldBound { get; set; }

        public Identity DurationPacketIdentity { get; set; }

        public int DurationParameter1 { get; set; }
    }
}
