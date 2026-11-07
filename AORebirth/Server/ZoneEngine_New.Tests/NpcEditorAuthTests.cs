namespace ZoneEngine_New.Tests
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    using AORebirth.Core.Encryption;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.NpcEditor;

    using static ZoneEngine_New.Core.Accounts.MySqlAccountSanctionRepository;

    [TestClass]
    public sealed class NpcEditorAuthTests
    {
        const string Password = "test-password";

        [TestMethod]
        public void ValidPasswordAloneDoesNotGrantGmPermissions()
        {
            var account = new EditorCredentials(7, "admin", PasswordHash.CreateHash(Password), 0);
            var auth = new NpcEditorSessions(_ => account, TimeProvider.System, 1, 100, true, true);

            Assert.IsNull(auth.Login(new("admin", Password)));
            account = account with { GmLevel = 1 };
            Assert.IsNull(auth.Login(new("other", Password)));
            Assert.IsNull(auth.Login(new("admin", "incorrect")));
            var session = auth.Login(new("ADMIN", Password))!;
            Assert.IsNotNull(session);
            Assert.IsFalse(session.CanWrite);
            Assert.IsFalse(session.CanSave);
            Assert.IsFalse(auth.CanMutate(session.Token, auth.Find(session.Token)!, save: false));
        }

        [TestMethod]
        public void ReadsAvoidAccountLookupsAndWritesRejectRevokedAuthority()
        {
            EditorCredentials? account = new(7, "admin", PasswordHash.CreateHash(Password), 100);
            int lookups = 0;
            var auth = new NpcEditorSessions(_ => { lookups++; return account; }, TimeProvider.System, 1, 100, true, false);
            var reply = auth.Login(new("admin", Password))!;
            Assert.IsTrue(reply.CanWrite);
            Assert.IsFalse(reply.CanSave);
            var session = auth.Find(reply.Token)!;
            Assert.AreEqual(1, lookups);
            Assert.IsFalse(auth.CanMutate(reply.Token, session, save: true));
            Assert.IsTrue(auth.CanMutate(reply.Token, session, save: false));
            Assert.AreEqual(2, lookups);

            account = null;
            Assert.IsFalse(auth.CanMutate(reply.Token, session, save: false));
            Assert.IsNull(auth.Find(reply.Token));
        }

        [TestMethod]
        public void ExpiryAndNewLoginInvalidateOldBearerTokens()
        {
            var time = new TestTime();
            var account = new EditorCredentials(7, "admin", PasswordHash.CreateHash(Password), 100);
            var auth = new NpcEditorSessions(_ => account, time, 1, 100, true, true);
            string first = auth.Login(new("admin", Password))!.Token;
            var second = auth.Login(new("admin", Password))!;
            Assert.IsNull(auth.Find(first));
            Assert.IsNotNull(auth.Find(second.Token));
            time.Now = second.ExpiresAt;
            Assert.IsNull(auth.Find(second.Token));
        }

        [TestMethod]
        public async Task LocalTlsListenerRejectsUntrustedOriginAndUnauthenticatedRequests()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var account = new EditorCredentials(7, "admin", PasswordHash.CreateHash(Password), 100);
            var auth = new NpcEditorSessions(_ => account, TimeProvider.System, 1, 100, false, false);
            using var cert = NpcEditorHost.CreateCertificate();
            await using var app = NpcEditorHost.Build(0, cert, auth, new QuietLogger());
            app.MapGet("/npc-editor/check", () => Results.Ok());
            // The authorization gates must reject these mutations before using either runtime service.
            NpcEditorHost.MapEditorRoutes(app, null!, null!);
            string statPath = $"/npc-editor/npcs/1/{(int)IdentityType.CanbeAffected}/100/{Guid.NewGuid():D}/stats";
            var statEdit = new { statId = (int)CharacterStat.Health, expectedBase = 100, value = 75 };
            await app.StartAsync(timeout.Token);

            byte[] fingerprint = cert.GetCertHash(HashAlgorithmName.SHA256);
            using var client = new HttpClient(new HttpClientHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                ServerCertificateCustomValidationCallback = (_, actual, _, _) => actual != null
                    && CryptographicOperations.FixedTimeEquals(actual.GetCertHash(HashAlgorithmName.SHA256), fingerprint)
            }) { BaseAddress = new Uri(app.Urls.Single()) };
            Assert.AreEqual("127.0.0.1", client.BaseAddress.Host);
            using HttpResponseMessage missing = await client.GetAsync("/npc-editor/check", timeout.Token);
            Assert.AreEqual(HttpStatusCode.Unauthorized, missing.StatusCode);
            using var unauthenticatedEdit = await client.PostAsJsonAsync(statPath, statEdit, timeout.Token);
            Assert.AreEqual(HttpStatusCode.Unauthorized, unauthenticatedEdit.StatusCode);
            using (var browser = new HttpRequestMessage(HttpMethod.Post, "/npc-editor/session"))
            {
                browser.Headers.Add("Origin", "https://example.invalid");
                browser.Content = JsonContent.Create(new NpcEditorLogin("admin", Password));
                using var denied = await client.SendAsync(browser, timeout.Token);
                Assert.AreEqual(HttpStatusCode.Forbidden, denied.StatusCode);
            }
            using var login = await client.PostAsJsonAsync("/npc-editor/session", new NpcEditorLogin("admin", Password), timeout.Token);
            login.EnsureSuccessStatusCode();
            var session = (await login.Content.ReadFromJsonAsync<NpcEditorSessionReply>(cancellationToken: timeout.Token))!;
            Assert.IsFalse(session.CanWrite);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
            using var accepted = await client.GetAsync("/npc-editor/check", timeout.Token);
            Assert.AreEqual(HttpStatusCode.OK, accepted.StatusCode);
            using var readOnlyEdit = await client.PostAsJsonAsync(statPath, statEdit, timeout.Token);
            Assert.AreEqual(HttpStatusCode.Forbidden, readOnlyEdit.StatusCode);
            using var logout = await client.DeleteAsync("/npc-editor/session", timeout.Token);
            Assert.AreEqual(HttpStatusCode.NoContent, logout.StatusCode);
            using var revoked = await client.GetAsync("/npc-editor/check", timeout.Token);
            Assert.AreEqual(HttpStatusCode.Unauthorized, revoked.StatusCode);
            await app.StopAsync(timeout.Token);
        }

        sealed class TestTime : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;
            public override DateTimeOffset GetUtcNow() => Now;
        }

        sealed class QuietLogger : IZoneLogger
        {
            public void Debug(string message) { }
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
            public void Error(Exception exception, string message) { }
            public IZoneLogger CreateForPlayfield(int playfieldId) => this;
        }
    }
}
