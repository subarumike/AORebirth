namespace ZoneEngine_New.Core.DebugMcp
{
    using System;
    using System.Globalization;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using Utility.Config;

    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Missions;
    using ZoneEngine_New.Core.Playfield;

    public sealed class DebugMcpHost : IAsyncDisposable
    {
        readonly WebApplication _app;

        DebugMcpHost(WebApplication app)
        {
            _app = app;
        }

        public static DebugMcpHost? Start(IServiceProvider zoneServices, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            DebugMcpSettings? settings = ConfigReadWrite.Instance.CurrentConfig == null
                ? null
                : ConfigReadWrite.Instance.CurrentConfig.DebugMcp;
            DebugMcpHost? host = Start(zoneServices, logger, settings);
            if (host == null)
            {
                logger.Info("Debug MCP disabled.");
            }
            return host;
        }

        public static DebugMcpHost? Start(IServiceProvider zoneServices, IZoneLogger logger, DebugMcpSettings? settings)
        {
            DebugMcpEndpoint? endpoint = DebugMcpOptions.Resolve(settings);
            if (endpoint == null)
                return null;

            ArgumentNullException.ThrowIfNull(zoneServices);
            ArgumentNullException.ThrowIfNull(logger);

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = Array.Empty<string>(),
                ContentRootPath = AppContext.BaseDirectory,
                ApplicationName = "ZoneEngine_New"
            });
            builder.Logging.ClearProviders();
            builder.Services.Configure<Microsoft.Extensions.Hosting.ConsoleLifetimeOptions>(options =>
            {
                options.SuppressStatusMessages = true;
            });
            builder.WebHost.UseUrls(
                "http://"
                + (endpoint.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                    ? "[" + endpoint.ListenIP + "]"
                    : endpoint.ListenIP)
                + ":"
                + endpoint.Port.ToString(CultureInfo.InvariantCulture));
            builder.Services.AddSingleton(zoneServices.GetRequiredService<PlayfieldManager>());
            builder.Services.AddSingleton<IPlayfieldMetricsRegistry>(zoneServices.GetRequiredService<IPlayfieldMetricsRegistry>());
            builder.Services.AddSingleton(zoneServices.GetRequiredService<GeneratedMissionService>());
            builder.Services.AddSingleton(endpoint);
            builder.Services.AddMcpServer().WithHttpTransport().WithTools<ZoneDebugTools>();

            WebApplication app = builder.Build();
            app.MapMcp(DebugMcpOptions.Route);
            try
            {
                app.StartAsync().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                try
                {
                    app.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                catch (Exception)
                {
                }

                throw new StartupValidationException(
                    "Debug MCP failed to listen on "
                    + endpoint.ListenIP
                    + ":"
                    + endpoint.Port.ToString(CultureInfo.InvariantCulture)
                    + " ("
                    + exception.GetType().Name
                    + ").");
            }

            logger.Info("Debug MCP listening at " + endpoint.Url);
            return new DebugMcpHost(app);
        }

        public async System.Threading.Tasks.ValueTask DisposeAsync()
        {
            await _app.StopAsync().ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }
}
