namespace LostEden.Vehicles
{
    /// <summary>
    /// Stock <c>SteeringResult_e</c>. Recovered from the integrator's dispatch
    /// (<c>Vehicle.dll FUN_1000e3d3</c>) and the return paths of the steering behaviours; the enum
    /// itself carries no RTTI, so the names are ours and the values are stock.
    ///
    /// The integrator reads a steering result on three separate channels, each with its own virtual
    /// slot, and each channel only honours one value:
    /// <list type="bullet">
    ///   <item>longitudinal (vftable <c>+0x4c</c>) honours <see cref="Force"/>;</item>
    ///   <item>lateral (vftable <c>+0x50</c>) honours <see cref="Lateral"/>;</item>
    ///   <item>turn (vftable <c>+0x54</c>) honours <see cref="Turn"/>.</item>
    /// </list>
    /// Every channel treats <see cref="Halt"/> as "zero this channel", and the longitudinal one also
    /// calls <c>Vehicle_t::Halt</c>. Any other value leaves the channel's vector unused.
    /// </summary>
    public enum SteeringResult
    {
        /// <summary>
        /// No steering this step. Also the escape a behaviour returns when the computed force is
        /// non-finite in the large — <c>SteeringArrive</c> returns this above 1e7 (<c>1000ac6e</c>).
        /// </summary>
        None = 0,

        /// <summary>Stop: the channel's vector is zeroed. On the longitudinal channel, also halts.</summary>
        Halt = 1,

        /// <summary>The vector is a force in newtons; the integrator clamps and integrates it.</summary>
        Force = 2,

        /// <summary>The vector is a velocity applied straight to position, bypassing the force path.</summary>
        Lateral = 3,

        /// <summary>The vector is a rotation axis whose length is the angular rate in radians/second.</summary>
        Turn = 4,
    }
}
