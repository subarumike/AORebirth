namespace ZoneEngine.Core.Missions
{
    using System;

    // Compatibility ownership only. ZoneEngine_New does not compile the file
    // allocator, state directory resolver or this partial into its runtime.
    internal static partial class MissionRollService
    {
        private static readonly object OfferIdentityLock = new object();
        private static MissionOfferIdentityStore offerIdentityStore;
        private static Func<int, bool> offerIdentityCollisionValidator;

        static MissionRollService()
        {
            defaultOfferIdentityAllocator = NextQuestInstance;
        }

        private static int NextQuestInstance()
        {
            lock (OfferIdentityLock)
            {
                if (offerIdentityStore == null)
                {
                    string missionStateDirectory = MissionStateDirectory.Resolve();
                    offerIdentityStore = new MissionOfferIdentityStore(missionStateDirectory);
                }

                MissionOfferIdentityAllocationResult result = offerIdentityStore.TryAllocate(IsOfferIdentityInUse);
                if (!result.Succeeded)
                    throw new InvalidOperationException("Mission offer identity allocation failed closed: " + result.Diagnostic);
                return result.OfferId;
            }
        }

        private static bool IsOfferIdentityInUse(int offerInstance)
        {
            Func<int, bool> validator = offerIdentityCollisionValidator;
            return validator != null && validator(offerInstance);
        }

        internal static void SetOfferIdentityCollisionValidator(Func<int, bool> validator)
        {
            offerIdentityCollisionValidator = validator;
        }
    }
}
