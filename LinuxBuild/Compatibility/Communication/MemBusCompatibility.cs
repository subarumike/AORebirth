namespace MemBus
{
    public interface IBus
    {
    }

    public static class BusSetup
    {
        public static BusConfiguration StartWith<TConfiguration>()
        {
            return new BusConfiguration();
        }
    }

    public sealed class BusConfiguration
    {
        internal BusConfiguration()
        {
        }

        public IBus Construct()
        {
            return new InertBus();
        }
    }

    internal sealed class InertBus : IBus
    {
    }
}

namespace MemBus.Configurators
{
    public sealed class AsyncConfiguration
    {
    }
}
