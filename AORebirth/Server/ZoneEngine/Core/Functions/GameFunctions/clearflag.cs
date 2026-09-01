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
    /// FunctionType.ClearFlag (53140) — clear one bit from a character/item stat by bit index.
    /// Paired with SetFlag (args [statId, bitIndex]). Used when Overview map nanos leave NCU
    /// so PF map / red dots turn off again.
    /// </summary>
    internal class Function_clearflag : FunctionPrototype
    {
        private const FunctionType functionId = FunctionType.ClearFlag;

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
            uint current = tempTarget.Stats[statNumber].BaseValue;
            tempTarget.Stats[statNumber].Set(current & ~(1u << bitIndex));
            return true;
        }
    }
}
