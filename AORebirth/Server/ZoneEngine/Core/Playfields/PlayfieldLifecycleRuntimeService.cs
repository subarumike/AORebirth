namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Threading;

    using AORebirth.Core.Entities;

    #endregion

    internal sealed class PlayfieldLifecycleRuntimeService
    {
        internal void PreparePlayfieldTransfer(
            Dynel dynel,
            Action<int> clearTransferContactState,
            Action<Dynel> disableTimers)
        {
            Require(clearTransferContactState, "clearTransferContactState");
            Require(disableTimers, "disableTimers");

            Thread.Sleep(200);
            clearTransferContactState(dynel.Identity.Instance);
            disableTimers(dynel);
            Thread.Sleep(1000);
        }

        private static void Require(Delegate callback, string name)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
