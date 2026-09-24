using Xunit;

// VehicleSim.DeltaTimeNow is static, faithfully mirroring stock's Vehicle_t::s_vDeltaTimeNow
// (Vehicle.dll 1001a148) which is a process-wide global the rest of the engine reads. xUnit
// parallelises across test classes by default, so two classes stepping vehicles at once would
// interleave writes to it and read each other's step size. Serialise the assembly rather than
// make the field non-static and diverge from stock.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
