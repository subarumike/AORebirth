namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Globalization;

    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// Mints unique InstanceIds from leased DB blocks. Allocate is in-memory until the local block is empty.
    /// </summary>
    public sealed class ItemInstanceIdAllocator : IItemInstanceIdAllocator
    {
        public const int LeaseBlockSize = 10000;

        private readonly IInventoryRepository _inventory;
        private readonly IZoneLogger _logger;
        private readonly object _gate = new();
        private int _next;
        private int _end;

        public ItemInstanceIdAllocator(IInventoryRepository inventory, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(inventory);
            ArgumentNullException.ThrowIfNull(logger);
            _inventory = inventory;
            _logger = logger;
            LeaseBlock();
        }

        public int Allocate()
        {
            lock (_gate)
            {
                if (_next >= _end)
                    LeaseBlock();

                int id = _next++;
                if (id <= 0)
                    throw new InvalidOperationException("InstanceId allocator overflowed.");
                return id;
            }
        }

        void LeaseBlock()
        {
            int start = _inventory.LeaseInstanceIdBlock(LeaseBlockSize);
            int end = checked(start + LeaseBlockSize);
            _next = start;
            _end = end;

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ItemInstanceIdAllocator leased [{0}, {1})",
                    start,
                    end));
        }
    }
}
