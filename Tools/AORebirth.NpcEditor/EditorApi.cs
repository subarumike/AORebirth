using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace AORebirth.NpcEditor;

internal sealed class EditorApi : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient client;

    public EditorApi(int port, string fingerprint)
    {
        if (port is < 1 or > 65535 || fingerprint.Length != 64 || !fingerprint.All(Uri.IsHexDigit))
            throw new ArgumentException("Enter a valid local port and the 64-digit ZoneEngine SHA-256 fingerprint.");

        byte[] pinnedHash = Convert.FromHexString(fingerprint);
        client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ServerCertificateCustomValidationCallback = (request, certificate, _, _) =>
                request.RequestUri is { Scheme: "https", Host: "127.0.0.1" } address
                && address.Port == port
                && certificate != null
                && CryptographicOperations.FixedTimeEquals(pinnedHash, SHA256.HashData(certificate.RawData))
        })
        {
            BaseAddress = new Uri($"https://127.0.0.1:{port}/npc-editor/"),
            Timeout = TimeSpan.FromSeconds(12),
            MaxResponseContentBufferSize = 12 * 1024 * 1024
        };
    }

    public async Task<EditorSession> LogInAsync(string username, string password)
    {
        var session = await PostAsync<EditorSession>("session", new { username, password });
        if (string.IsNullOrEmpty(session.Token))
            throw new InvalidDataException("ZoneEngine returned an empty session token.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        return session;
    }

    public async Task LogOutAsync()
    {
        using var response = await client.DeleteAsync("session");
        await EnsureSuccessAsync(response);
    }

    public Task<T> GetAsync<T>(string path) where T : class => SendAsync<T>(HttpMethod.Get, path, null);

    public Task<T> PostAsync<T>(string path, object body) where T : class => SendAsync<T>(HttpMethod.Post, path, body);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body) where T : class
    {
        using var request = new HttpRequestMessage(method, path);
        if (body != null)
            request.Content = JsonContent.Create(body, options: Json);
        using var response = await client.SendAsync(request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<T>(Json)
            ?? throw new InvalidDataException("ZoneEngine returned an empty response.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        string? error = null;
        try
        {
            error = (await response.Content.ReadFromJsonAsync<EditorError>(Json))?.Error;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // A failed or misconfigured endpoint may return a response without the editor error contract.
        }

        throw new EditorApiException(error ?? $"ZoneEngine returned HTTP {(int)response.StatusCode}.", response.StatusCode);
    }

    public void Dispose() => client.Dispose();

    private sealed record EditorError(string Error);
}

internal sealed class EditorApiException(string message, HttpStatusCode status) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}
