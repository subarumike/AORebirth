namespace ZoneEngine_New.Core.NpcEditor
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.RateLimiting;
    using System.Threading.Tasks;

    using AORebirth.Core.GameData;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.RateLimiting;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    using MySqlConnector;

    using ZoneEngine_New.Core.Accounts;
    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Configuration;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;

    internal sealed class NpcEditorHost(WebApplication app, X509Certificate2 certificate) : IAsyncDisposable
    {
        const string Route = "/npc-editor";
        const string PortVariable = "AO_REBIRTH_NPC_EDITOR_PORT";
        const string WriteVariable = "AO_REBIRTH_NPC_EDITOR_ALLOW_WRITES";
        const string LoginRatePolicy = "npc-editor-login";
        static readonly object SessionKey = new();

        internal static NpcEditorHost? Start(IServiceProvider services, IZoneLogger logger)
        {
            string? configuredPort = Environment.GetEnvironmentVariable(PortVariable);
            if (configuredPort == null)
                return null;
            if (!int.TryParse(configuredPort, NumberStyles.None, CultureInfo.InvariantCulture, out int port)
                || port is < 1 or > 65535)
                throw new StartupValidationException(PortVariable + " must be between 1 and 65535.");

            var permissions = services.GetRequiredService<GmPermissions>();
            int readLevel = Math.Max(1, permissions.CommandLevel("npc", 1));
            int writeLevel = Math.Max(readLevel, Math.Max(permissions.CommandLevel("set", 1),
                permissions.ActionLevel(GmPermissions.SetOtherCharacterAction, SetCommand.OtherCharacterGmLevel)));
            bool allowWrites = Environment.GetEnvironmentVariable(WriteVariable) == "1";
            var gameData = (GameDataStore)services.GetRequiredService<IGameData>();
            var loot = new NpcEditorLoot(gameData, services.GetRequiredService<IItemTemplateCatalog>());
            var sessions = new NpcEditorSessions(MySqlAccountSanctionRepository.LoadEditorCredentials,
                TimeProvider.System, readLevel, writeLevel, allowWrites,
                !string.IsNullOrWhiteSpace(GameDataPaths.ResolveOverrideRoot()));
            var runtime = new NpcEditorRuntime(services.GetRequiredService<PlayfieldManager>());

            X509Certificate2 certificate = CreateCertificate();
            WebApplication? app = null;
            try
            {
                app = Build(port, certificate, sessions, logger);
                MapEditorRoutes(app, runtime, loot);
                app.StartAsync().GetAwaiter().GetResult();
                string fingerprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
                logger.Info($"NPC Editor listening on https://127.0.0.1:{port} TLS SHA256={fingerprint}.");
                return new NpcEditorHost(app, certificate);
            }
            catch (Exception exception)
            {
                try { app?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
                finally { certificate.Dispose(); }
                throw new StartupValidationException($"NPC Editor failed to start on port {port} ({exception.GetType().Name}).");
            }
        }

        internal static X509Certificate2 CreateCertificate()
        {
            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=AORebirth NPC Editor", rsa, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build());
            DateTimeOffset now = DateTimeOffset.UtcNow;
            using X509Certificate2 generated = request.CreateSelfSigned(now.AddMinutes(-5), now.AddDays(30));
            // Windows Schannel needs a key it can open outside the generating process.
            byte[] pfx = generated.Export(X509ContentType.Pfx);
            try
            {
                return X509CertificateLoader.LoadPkcs12(pfx, null);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(pfx);
            }
        }

        internal static WebApplication Build(int port, X509Certificate2 certificate, NpcEditorSessions sessions,
            IZoneLogger logger)
        {
            // This listener's address must never be augmented by inherited ASP.NET hosting settings.
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
            {
                Args = Array.Empty<string>(),
                ContentRootPath = AppContext.BaseDirectory,
                EnvironmentName = "Production"
            });
            builder.Services.AddLogging();
            builder.Services.AddRouting();
            builder.WebHost.UseKestrelCore();
            builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
            builder.Services.Configure<Microsoft.Extensions.Hosting.ConsoleLifetimeOptions>(options =>
                options.SuppressStatusMessages = true);
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.AddServerHeader = false;
                options.Limits.MaxRequestBodySize = 16 * 1024;
                options.Limits.MaxRequestLineSize = 2048;
                options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
                options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
                options.Listen(IPAddress.Loopback, port, listener => listener.UseHttps(certificate));
            });
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetConcurrencyLimiter("npc-editor", _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = 16,
                        QueueLimit = 0
                    }));
                options.AddFixedWindowLimiter(LoginRatePolicy, limit =>
                {
                    limit.Window = TimeSpan.FromMinutes(1);
                    limit.PermitLimit = 8;
                    limit.QueueLimit = 0;
                });
            });

            WebApplication app = builder.Build();
            app.UseRateLimiter();
            app.Use(async (context, next) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                if (!context.Request.IsHttps || !Equals(context.Connection.RemoteIpAddress, IPAddress.Loopback)
                    || context.Request.Host.Host != "127.0.0.1"
                    || context.Request.Host.Port != context.Connection.LocalPort
                    || context.Request.Headers.ContainsKey("Origin"))
                {
                    await Error(403, "Only a local desktop editor connection is allowed.").ExecuteAsync(context);
                    return;
                }

                if (!(HttpMethods.IsPost(context.Request.Method) && context.Request.Path == Route + "/session"))
                {
                    string authorization = context.Request.Headers.Authorization.ToString();
                    string token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? authorization[7..] : string.Empty;
                    if (sessions.Find(token) is not NpcEditorSessions.Session session)
                    {
                        await Error(401, "Sign in with a GM account.").ExecuteAsync(context);
                        return;
                    }
                    context.Items[SessionKey] = new AuthenticatedRequest(token, session, sessions);
                }

                try
                {
                    await next(context);
                }
                catch (NpcEditorRuntimeException ex)
                {
                    await Error(ex.StatusCode, ex.Message).ExecuteAsync(context);
                }
                catch (NpcEditorLootConflictException ex)
                {
                    await Error(409, ex.Message).ExecuteAsync(context);
                }
                catch (BadHttpRequestException ex)
                {
                    await Error(ex.StatusCode, "Invalid editor request.").ExecuteAsync(context);
                }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                {
                }
                catch (TimeoutException)
                {
                    await Error(503, "The requested playfield did not respond in time.").ExecuteAsync(context);
                }
                catch (ArgumentException)
                {
                    await Error(400, "An editor request value is out of range.").ExecuteAsync(context);
                }
                catch (MySqlException ex)
                {
                    logger.Warn($"NPC Editor account lookup failed ({ex.GetType().Name}).");
                    await Error(503, "Account verification is unavailable.").ExecuteAsync(context);
                }
                catch (Exception ex) when (!context.Response.HasStarted)
                {
                    logger.Warn($"NPC Editor request failed ({ex.GetType().Name}).");
                    await Error(500, "NPC Editor request failed. See the ZoneEngine log.").ExecuteAsync(context);
                }
            });

            app.MapPost(Route + "/session", (NpcEditorLogin request) => sessions.Login(request) is { } reply
                ? Results.Ok(reply)
                : Error(401, "Credentials are invalid or the account does not have NPC editor access."))
                .RequireRateLimiting(LoginRatePolicy);
            app.MapDelete(Route + "/session", (HttpContext context) =>
            {
                AuthenticatedRequest auth = Auth(context);
                auth.Sessions.Revoke(auth.Token);
                return Results.NoContent();
            });
            return app;
        }

        internal static void MapEditorRoutes(WebApplication app, NpcEditorRuntime runtime, NpcEditorLoot loot)
        {
            var api = app.MapGroup(Route);
            api.MapGet("/npcs", async (string? q, HttpContext ctx) =>
                Results.Ok(await runtime.BrowseAsync(q, ctx.RequestAborted)));
            api.MapGet("/target", async (int playfieldId, int identityType, int instanceId, HttpContext ctx) =>
                Results.Ok(await runtime.ResolveAsync(playfieldId, identityType, instanceId, ctx.RequestAborted)));
            api.MapGet("/items", (string? q) => Results.Ok(runtime.SearchItems(q ?? string.Empty)));
            api.MapGet("/loot-items", (string? q) => Results.Ok(loot.SearchItemFamilies(q ?? string.Empty)));

            var npc = api.MapGroup("/npcs/{playfieldId:int}/{identityType:int}/{instanceId:int}/{generation:guid}");
            npc.MapGet("", async (int playfieldId, int identityType, int instanceId, Guid generation, HttpContext ctx) =>
                Results.Ok(await runtime.InspectAsync(new(playfieldId, identityType, instanceId, generation), ctx.RequestAborted)));
            npc.MapGet("/loot", async (int playfieldId, int identityType, int instanceId, Guid generation, HttpContext ctx) =>
                Results.Ok(await runtime.WithNpcAsync(new(playfieldId, identityType, instanceId, generation),
                    loot.Describe, ctx.RequestAborted)));
            npc.MapPost("/stats", async (int playfieldId, int identityType, int instanceId, Guid generation,
                StatEdit edit, HttpContext ctx) =>
            {
                if (!CanMutate(ctx))
                    return WriteDenied();
                if (edit.StatId is not int stat || edit.ExpectedBase is not int expected || edit.Value is not int value)
                    return Error(400, "Supply statId, expectedBase and value.");
                return Results.Ok(await runtime.ApplyStatAsync(new(playfieldId, identityType, instanceId, generation),
                    stat, expected, value, ctx.RequestAborted));
            });
            npc.MapPost("/equipment", async (int playfieldId, int identityType, int instanceId, Guid generation,
                NpcEditorEquipmentEdit edit, HttpContext ctx) =>
            {
                if (!CanMutate(ctx))
                    return WriteDenied();
                return Results.Ok(await runtime.ApplyEquipmentAsync(new(playfieldId, identityType, instanceId, generation),
                    edit, ctx.RequestAborted));
            });
            npc.MapPost("/revert", async (int playfieldId, int identityType, int instanceId, Guid generation,
                LiveRevert edit, HttpContext ctx) =>
            {
                if (!CanMutate(ctx))
                    return WriteDenied();
                if (edit.ChangeId == Guid.Empty)
                    return Error(400, "Supply a changeId.");
                return Results.Ok(await runtime.RevertAsync(new(playfieldId, identityType, instanceId, generation),
                    edit.ChangeId, ctx.RequestAborted));
            });
            npc.MapPost("/loot", (int playfieldId, int identityType, int instanceId, Guid generation,
                NpcEditorLootSave edit, HttpContext ctx) => SaveLoot(ctx, runtime, loot,
                    new(playfieldId, identityType, instanceId, generation), (hash, actor) => loot.Save(hash, edit, actor)));
            npc.MapPost("/loot/revert", (int playfieldId, int identityType, int instanceId, Guid generation,
                NpcEditorLootRevert edit, HttpContext ctx) => SaveLoot(ctx, runtime, loot,
                    new(playfieldId, identityType, instanceId, generation), (hash, actor) => loot.Revert(hash, edit, actor)));
        }

        static async Task<IResult> SaveLoot(HttpContext context, NpcEditorRuntime runtime, NpcEditorLoot loot,
            NpcEditorIdentity identity, Func<NpcEditorLootTarget, string, NpcEditorLootState> save)
        {
            if (!CanMutate(context, save: true))
                return WriteDenied();
            try
            {
                NpcEditorLootTarget target = await runtime.WithNpcAsync(identity, loot.CaptureSaveTarget, context.RequestAborted);
                context.RequestAborted.ThrowIfCancellationRequested();
                return Results.Ok(save(target, Auth(context).Session.Username));
            }
            catch (ArgumentException ex)
            {
                return Error(400, ex.Message);
            }
            catch (InvalidOperationException ex) when (ex.GetType() == typeof(InvalidOperationException))
            {
                return Error(409, ex.Message);
            }
            catch (IOException)
            {
                return Error(503, "Could not save the NPC loot overlay. Check the configured overlay folder.");
            }
        }

        static AuthenticatedRequest Auth(HttpContext context) => (AuthenticatedRequest)context.Items[SessionKey]!;
        static bool CanMutate(HttpContext context, bool save = false)
        {
            AuthenticatedRequest auth = Auth(context);
            return auth.Sessions.CanMutate(auth.Token, auth.Session, save);
        }
        static IResult WriteDenied() => Error(403, "This session or ZoneEngine configuration does not allow this edit.");
        static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);

        sealed record AuthenticatedRequest(string Token, NpcEditorSessions.Session Session, NpcEditorSessions Sessions);
        sealed record StatEdit(int? StatId, int? ExpectedBase, int? Value);
        sealed record LiveRevert(Guid ChangeId);

        public async ValueTask DisposeAsync()
        {
            try
            {
                using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                try { await app.StopAsync(timeout.Token); }
                finally { await app.DisposeAsync(); }
            }
            finally
            {
                certificate.Dispose();
            }
        }
    }
}
