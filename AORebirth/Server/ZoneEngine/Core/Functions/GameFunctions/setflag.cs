#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core.Functions.GameFunctions
{
    #region Usings ...

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.Stats;

    using MsgPack;

    #endregion

    /// <summary>
    /// FunctionType.SetFlag (53139) — set one bit in a character/item stat by bit index.
    /// nanos.dat stores args as [statId, bitIndex] (not a mask). Capture 20260830-110744
    /// Overview of Nascence and Jobe (223767) uses SetFlag mapareapart3/MapsC indices
    /// 0,1,2,3,4,5,6,15,16,17,18,19,27,28 → MapsC 403669119 for Ctrl+5 PF map.
    /// </summary>
    internal class Function_setflag : FunctionPrototype
    {
        private const FunctionType functionId = FunctionType.SetFlag;

        public override FunctionType FunctionId
        {
            get
            {
                return functionId;
            }
        }

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

        public bool FunctionExecute(
            INamedEntity Self,
            IEntity Caller,
            IInstancedEntity Target,
            MessagePackObject[] Arguments)
        {
            if (Arguments == null || Arguments.Length < 2)
            {
                return false;
            }

            IStats tempTarget = Target;
            if (tempTarget == null)
            {
                return false;
            }

            int bitIndex = Arguments[1].AsInt32();
            if (bitIndex < 0 || bitIndex > 31)
            {
                return false;
            }

            int statNumber = Arguments[0].AsInt32();
            // Use BaseValue — Value may still be LastCalculatedValue=-1 before first recalc.
            // Overview MapsC (585) SetFlag must run on cast so server matches client bit state;
            // SyncOverviewMapFlags remains the gate (0 vs 403669119) and Buff-clears when absent.
            uint current = tempTarget.Stats[statNumber].BaseValue;
            tempTarget.Stats[statNumber].Set(current | (1u << bitIndex));
            return true;
        }
    }
}
