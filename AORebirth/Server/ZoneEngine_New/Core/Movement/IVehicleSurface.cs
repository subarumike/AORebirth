namespace ZoneEngine_New.Core.Movement
{
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>Client <c>Surface_i</c> queries used by <c>Vehicle_t::EnsureSurfaceAlignment</c>.</summary>
    public interface IVehicleSurface
    {
        bool TryLinecast(Vector3 from, Vector3 to, out Vector3 hit, out Vector3 normal);

        /// <summary>True when a torso-height body at <paramref name="foot"/> is inside solid.</summary>
        bool OverlapsTorso(Vector3 foot);
    }
}
