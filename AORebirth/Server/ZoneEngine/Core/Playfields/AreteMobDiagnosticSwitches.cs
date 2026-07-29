namespace ZoneEngine.Core.Playfields
{
    /// <summary>
    /// Bisect kill-switches for Arete mob/patrol work. Only TODAY's additions stay OFF
    /// (AlienAreaMobs + LeonoraMarty). Older Rex/Alex/oasis/robot packs stay ON.
    /// Uses static readonly (not const) so gated call sites stay reachable to the compiler.
    /// </summary>
    internal static class AreteMobDiagnosticSwitches
    {
        // --- older packs (ON) ---
        internal static readonly bool MalfunctioningCleaningRobots = true; // Rex-area burn/explode robots
        internal static readonly bool JunkyardCleaningRobots = true;
        internal static readonly bool AlexAreaMobs = true;
        internal static readonly bool LoreleiOasisMobs = true; // on + Arete population tick throttle
        internal static readonly bool MarcusPadAmbientCombat = true;
        internal static readonly bool FinishCaptureMobs = true;
        internal static readonly bool SurveillanceDroids = true;
        internal static readonly bool IccPeacekeeperPatrol = true;
        internal static readonly bool RoboticGuardDog = true;

        // --- Arete wildlife / patrol packs ---
        internal static readonly bool AlienAreaMobs = true; // Minibull/Saltworm/Spider/east rats
        internal static readonly bool LeonoraMarty = true; // on; south floor-stuck waypoints removed
        internal static readonly bool KarliCappelleri = true; // PF 8009 Crashed Alien Ship; capture 20260727-055715
        internal static readonly bool SandstormMarauders = true; // Remi Gallois Hellfyre quest; capture 20260727-204902

        // --- Elysium ---
        internal static readonly bool ElysiumEastMobs = true; // PF 4543/4540 captures 182451+190145+193914
    }
}
