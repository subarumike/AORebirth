namespace ZoneEngine_New.Tests
{
    using System;
    using System.Reflection;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.World;

    [TestClass]
    [DoNotParallelize]
    public sealed class WorldLinkShutdownTests
    {
        private static readonly FieldInfo CurrentInstance = typeof(ZoneInstanceOptions)
            .GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)!;
        private object? previousInstance;

        [TestInitialize]
        public void ConfigureInstance()
        {
            previousInstance = CurrentInstance.GetValue(null);
            ZoneInstanceOptions.Configure(["--instance", "worldlink-shutdown-test", "--zone-port", "7501"]);
        }

        [TestCleanup]
        public void RestoreInstance() => CurrentInstance.SetValue(null, previousInstance);

        [TestMethod]
        public void ShutdownCanFlushAnEmptyReleaseQueue()
        {
            using var link = new WorldLink(new NLogZoneLogger());

            link.FlushReleases(TimeSpan.Zero);
        }

        [TestMethod]
        public void ExplicitAndContainerDisposalCanBothRun()
        {
            using ServiceProvider services = new ServiceCollection()
                .AddSingleton<IZoneLogger>(new NLogZoneLogger())
                .AddSingleton<WorldLink>()
                .BuildServiceProvider();

            services.GetRequiredService<WorldLink>().Dispose();
        }
    }
}
