namespace ZoneEngine_New.Core.GameData;

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ZoneEngine_New.Core.Inventory;

/// <summary>A local authenticated operator surface for one catalog. Disabled unless explicitly configured.</summary>
internal sealed class ItemTemplatesReloadHost(WebApplication app) : IAsyncDisposable
{
    internal const string PortVariable = "AO_REBIRTH_ITEMTEMPLATES_RELOAD_PORT";
    internal const string TokenFileVariable = "AO_REBIRTH_ITEMTEMPLATES_RELOAD_TOKEN_FILE";
    const string Route = "/v1/item-templates";
    const string ValidationRoute = Route + "/validate";
    const string DigestHeader = "X-AORebirth-Content-Sha256";

    internal string Address => app.Urls.Single();

    internal static ItemTemplatesReloadHost? Start(IServiceProvider services)
    {
        string? port = Environment.GetEnvironmentVariable(PortVariable);
        string? tokenFile = Environment.GetEnvironmentVariable(TokenFileVariable);
        if (port is null && tokenFile is null)
            return null;

        if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            || number is <= 0 or > 65535 || string.IsNullOrWhiteSpace(tokenFile) || !Path.IsPathFullyQualified(tokenFile))
            throw new InvalidOperationException("ItemTemplates reload requires a loopback port and absolute private token file.");
        var tokenInfo = new FileInfo(tokenFile);
        if (!tokenInfo.Exists || tokenInfo.LinkTarget is not null || tokenInfo.Length is < 64 or > 66)
            throw new InvalidOperationException("ItemTemplates reload token must be a regular private token file.");
        if (OperatingSystem.IsLinux() && (File.GetUnixFileMode(tokenFile) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite
            | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("ItemTemplates reload token must be owner-only.");

        return StartAsync(services.GetRequiredService<GameDataReloadRegistry>(),
            services.GetRequiredService<IItemTemplateCatalog>(), number, File.ReadAllText(tokenFile).Trim()).GetAwaiter().GetResult();
    }

    internal static async Task<ItemTemplatesReloadHost> StartAsync(GameDataReloadRegistry registry, IItemTemplateCatalog items,
        int port, string token)
    {
        if (token.Length != 64 || !token.All(char.IsAsciiHexDigit))
            throw new InvalidOperationException("ItemTemplates reload token must be 64 hexadecimal characters.");
        byte[] expectedAuthorization = Encoding.ASCII.GetBytes("Bearer " + token);
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.Services.Configure<Microsoft.Extensions.Hosting.ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = ItemTemplatesReload.MaxCatalogBytes;
            options.Listen(IPAddress.Loopback, port);
        });
        WebApplication endpoint = builder.Build();
        var operations = new SemaphoreSlim(1);

        async Task Dispatch(HttpContext context)
        {
            string supplied = context.Request.Headers.Authorization.ToString();
            if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote)
                || context.Request.Headers.Authorization.Count != 1 || supplied.Length != expectedAuthorization.Length
                || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(supplied), expectedAuthorization))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await operations.WaitAsync(context.RequestAborted).ConfigureAwait(false);
            try
            {
                if (HttpMethods.IsGet(context.Request.Method))
                {
                    ItemTemplatesStatus status = registry.ItemTemplatesStatus();
                    context.Response.StatusCode = status.Sha256 is null ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        version = 1,
                        ok = status.Sha256 is not null,
                        sha256 = status.Sha256,
                        path = status.Path,
                        entries = status.Entries
                    }).ConfigureAwait(false);
                    return;
                }

                bool validate = context.Request.Path == ValidationRoute;
                int maximum = validate ? ItemTemplatesReload.MaxCatalogBytes : 128;
                if (context.Request.ContentLength is not long length || length is <= 0 || length > maximum)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                var body = new byte[(int)length];
                await context.Request.Body.ReadExactlyAsync(body, context.RequestAborted).ConfigureAwait(false);
                string? digest = validate && context.Request.Headers[DigestHeader].Count == 1
                    ? context.Request.Headers[DigestHeader].ToString()
                    : validate ? null : ReadDigest(body);
                if (digest is not { Length: 64 } || !digest.All(c => char.IsAsciiHexDigitLower(c)))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                var result = registry.VerifyItemTemplates(digest, validate ? body : null, items, out ItemTemplatesStatus current);
                context.Response.StatusCode = result.Ok ? StatusCodes.Status200OK : StatusCodes.Status422UnprocessableEntity;
                await context.Response.WriteAsJsonAsync(new
                {
                    version = 1,
                    ok = result.Ok,
                    sha256 = result.Ok && validate ? digest : current.Sha256,
                    entries = result.Entries,
                    problems = result.Problems.Take(3)
                }).ConfigureAwait(false);
            }
            finally
            {
                operations.Release();
            }
        }

        endpoint.MapMethods(Route, [HttpMethods.Get, HttpMethods.Post], (RequestDelegate)Dispatch);
        endpoint.MapPost(ValidationRoute, (RequestDelegate)Dispatch);
        try
        {
            await endpoint.StartAsync().ConfigureAwait(false);
            return new ItemTemplatesReloadHost(endpoint);
        }
        catch
        {
            await endpoint.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    static string? ReadDigest(byte[] body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;
            var properties = root.EnumerateObject();
            if (!properties.MoveNext() || !properties.Current.NameEquals("sha256") || properties.Current.Value.ValueKind != JsonValueKind.String)
                return null;
            string? digest = properties.Current.Value.GetString();
            return properties.MoveNext() ? null : digest;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await app.StopAsync().ConfigureAwait(false);
        await app.DisposeAsync().ConfigureAwait(false);
    }
}
