namespace LostEden.Vehicles.Surfaces
{
    /// <summary>
    /// Stock <c>Surface_i</c> (<c>Vehicle.dll</c>, vftable <c>100121b0</c>) — the world a
    /// <c>Vehicle_t</c> collides against. See <c>Docs/Movement.md</c> §5.
    ///
    /// <para>
    /// Stock's interface has <b>nine</b> slots. Only the three below are reached from ground
    /// contact, and only the two implemented here are recovered; the rest are declared in the doc
    /// with their real signatures and deliberately left out rather than guessed:
    /// </para>
    /// <list type="table">
    ///   <item><term>slot 0</term><description>destructor</description></item>
    ///   <item><term>slot 1</term><description><c>CalculateClosestPoint</c> — used by the ground
    ///     clamp for the step-height reference and the liquid query. <b>Not yet recovered</b>
    ///     (<c>n3TilemapSurface_t 10018f0c</c> unread).</description></item>
    ///   <item><term>slot 2</term><description><c>CalculateNormal</c> — not reached from ground
    ///     contact.</description></item>
    ///   <item><term>slot 3</term><description>the 5-argument <c>GetLineIntersection</c> — not
    ///     reached from ground contact.</description></item>
    ///   <item><term>slot 4</term><description><see cref="GetLineIntersection"/> — the hot
    ///     path.</description></item>
    ///   <item><term>slots 5, 6</term><description><c>GetSphereIntersection</c> — not reached from
    ///     ground contact.</description></item>
    ///   <item><term>slot 7</term><description><c>IsInside</c> — not reached from ground
    ///     contact.</description></item>
    ///   <item><term>slot 8</term><description><see cref="VetoPosition"/>.</description></item>
    /// </list>
    ///
    /// <para>
    /// The <c>locality</c> argument is stock's <c>LocalitySource_t*</c>, which the tilemap path
    /// ignores; it is carried as <see cref="object"/> so the signature stays faithful without
    /// inventing a type hierarchy that is not ported yet.
    /// </para>
    /// </summary>
    public interface ISurface
    {
        /// <summary>
        /// Slot 4 — <c>bool GetLineIntersection(const Vector3&amp;, const Vector3&amp;, Vector3&amp;,
        /// Vector3&amp;, bool, LocalitySource_t*) const</c>.
        ///
        /// <para>
        /// On a miss stock writes <c>(-1, -1, -1)</c> into <paramref name="hit"/> rather than
        /// leaving it alone (<c>10018b72</c> line 162), so callers that read the out parameter after
        /// a false return see that sentinel.
        /// </para>
        /// </summary>
        bool GetLineIntersection(
            Vec3 start,
            Vec3 end,
            out Vec3 hit,
            out Vec3 normal,
            bool clipToBounds,
            object locality);

        /// <summary>
        /// Slot 8 — <c>bool VetoPosition(Vector3&amp;, LocalitySource_t*, const Vector3* const)
        /// const</c>. Returns true when the position is <b>rejected</b>; the ground clamp then backs
        /// the move off and tries again (<c>Docs/Movement.md</c> §6, step 1).
        /// </summary>
        bool VetoPosition(ref Vec3 position, object locality, Vec3 previous);

        /// <summary>
        /// Slot 1 — <c>void CalculateClosestPoint(const Vector3&amp;, Vector3&amp;, Vector3&amp;,
        /// LiquidMediumData_t*, LocalitySource_t*) const</c>. The ground clamp uses this for its
        /// step-height reference.
        ///
        /// <para>
        /// Stock's fourth argument is a <c>LiquidMediumData_t*</c> out-parameter carrying the water
        /// height, normal and medium type. Liquid is not ported (there is no water yet), so it is
        /// left off rather than stubbed with invented values — see <c>Docs/Movement.md</c> §6.3.
        /// </para>
        /// </summary>
        void CalculateClosestPoint(Vec3 point, out Vec3 closest, out Vec3 normal, object locality);
    }
}
