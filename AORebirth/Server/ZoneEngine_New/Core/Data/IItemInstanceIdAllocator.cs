namespace ZoneEngine_New.Core.Data
{
    /// <summary>
    /// Mints unique <c>InstanceId</c> values without writing <c>item_instances</c> per allocate.
    /// Backed by leased blocks from <c>item_instance_id_sequence</c>.
    /// </summary>
    public interface IItemInstanceIdAllocator
    {
        /// <summary>Returns the next unique positive InstanceId from the current lease.</summary>
        int Allocate();
    }
}
