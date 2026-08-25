namespace ZoneEngine.Core.Playfields.Hydration
{
    using System;

    using AORebirth.Core.Playfields;

    internal static class LegacyPlayfieldSourcePrecedence
    {
        internal static readonly string[] OrderedSources =
        {
            "playfields.dat base metadata and statels",
            "Playfields.xml playfield catalog metadata",
            "database mob spawns with suppression policy",
            "registered captured and hardcoded content modules",
            "database and RDB-backed vendors",
            "database static dynels",
            "runtime dynel registry refresh"
        };
    }

    internal sealed class LegacyPlayfieldRuntimeMaterializer : IPlayfieldRuntimeMaterializer
    {
        private readonly Func<int, IPlayfield> legacyFactory;

        internal LegacyPlayfieldRuntimeMaterializer(Func<int, IPlayfield> legacyFactory)
        {
            this.legacyFactory = legacyFactory ?? throw new ArgumentNullException("legacyFactory");
        }

        public IPlayfield Materialize(PlayfieldRuntimeMaterializationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            return this.legacyFactory(request.PlayfieldInstance);
        }
    }

    internal sealed class PlayfieldInstantiationCoordinator
    {
        private readonly PlayfieldHydrationMode mode;

        private readonly IPlayfieldRuntimeMaterializer legacyMaterializer;

        internal PlayfieldInstantiationCoordinator(
            PlayfieldHydrationMode mode,
            IPlayfieldRuntimeMaterializer legacyMaterializer)
        {
            this.mode = mode;
            this.legacyMaterializer = legacyMaterializer ?? throw new ArgumentNullException("legacyMaterializer");
        }

        internal PlayfieldHydrationMode Mode
        {
            get { return this.mode; }
        }

        internal IPlayfield Materialize(int playfieldInstance)
        {
            if (this.mode != PlayfieldHydrationMode.Legacy)
            {
                throw new NotSupportedException(
                    "Only legacy playfield materialization is enabled during Stage 1.");
            }

            return this.legacyMaterializer.Materialize(
                new PlayfieldRuntimeMaterializationRequest(playfieldInstance));
        }
    }
}
