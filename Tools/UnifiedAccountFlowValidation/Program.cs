namespace UnifiedAccountFlowValidation
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using AORebirth.Core.Encryption;

    using MySqlConnector;

    internal static class Program
    {
        private const string ServiceUrl = "http://127.0.0.1:17931/";

        private static readonly string[] FixtureUsers =
        {
            "FlowA1",
            "FlowB1",
            "FlowC1",
            "FlowD1",
            "RateA1",
            "LegacyA"
        };

        private static int passed;

        private static int Main()
        {
            string connectionString = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("FAIL AO_REBIRTH_MYSQL_CONNECTION is missing.");
                return 1;
            }

            Process service = null;
            try
            {
                ResetLocalDatabase(connectionString);
                service = StartService();
                WaitForHealth();
                RunHttpFlow(connectionString);
                CleanupFixtures(connectionString);
                Console.WriteLine("PASS UnifiedAccountFlowValidation " + passed + "/41");
                return 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine("FAIL " + exception.GetType().Name + ": " + exception.Message);
                return 1;
            }
            finally
            {
                if (service != null && !service.HasExited)
                {
                    service.Kill();
                    service.WaitForExit(5000);
                }

                try
                {
                    CleanupFixtures(connectionString);
                }
                catch
                {
                }
            }
        }

        private static void RunHttpFlow(string connectionString)
        {
            var anonymous = NewClient();
            HttpResult health = Get(anonymous, "health");
            Pass("health", health.StatusCode == 200 && health.Body.Contains("\"ready\""));

            HttpResult registerPage = Get(anonymous, "register");
            Pass("register-page", registerPage.StatusCode == 200 && registerPage.Body.Contains("Create AORebirth Account"));

            HttpResult csrf = Get(anonymous, "api/csrf");
            string csrfToken = ExtractJsonValue(csrf.Body, "csrf");
            Pass("csrf", csrf.StatusCode == 200 && !string.IsNullOrEmpty(csrfToken));

            string password = "FlowPass!2026";
            HttpResult registration = Post(
                anonymous,
                "api/register",
                Form(
                    "csrf", csrfToken,
                    "username", "FlowA1",
                    "password", password,
                    "email", "flowa1@example.test",
                    "idempotencyKey", "flow-a-key"));
            Pass("register-success", registration.StatusCode == 201 && registration.Body.Contains("\"gameAccountLinked\":true"));
            Pass("one-identity", ScalarLong(connectionString, "SELECT COUNT(*) FROM account_identities WHERE NormalizedUsername='flowa1'") == 1);
            Pass("one-login", ScalarLong(connectionString, "SELECT COUNT(*) FROM login WHERE Username='FlowA1'") == 1);
            Pass("one-mapping", ScalarLong(connectionString, "SELECT COUNT(*) FROM account_game_mappings m INNER JOIN login l ON l.Id=m.GameAccountId WHERE l.Username='FlowA1'") == 1);
            Pass("flags", ScalarLong(connectionString, "SELECT Flags FROM login WHERE Username='FlowA1'") == 0);
            string hash = ScalarString(connectionString, "SELECT Password FROM login WHERE Username='FlowA1'");
            Pass("hash-format", hash.Split(':').Length == 3);
            Pass("password-correct", PasswordHash.ValidatePassword(password, hash));
            Pass("password-wrong", !PasswordHash.ValidatePassword("WrongPass!2026", hash));

            HttpResult retry = Post(
                anonymous,
                "api/register",
                Form("csrf", csrfToken, "username", "FlowA1", "password", password, "email", "flowa1@example.test", "idempotencyKey", "flow-a-key"));
            Pass("idempotent-retry", retry.StatusCode == 201);
            Pass("retry-no-duplicate", ScalarLong(connectionString, "SELECT COUNT(*) FROM login WHERE Username='FlowA1'") == 1);

            HttpResult duplicate = Post(
                anonymous,
                "api/register",
                Form("csrf", csrfToken, "username", "FlowA1", "password", password, "email", "flowa2@example.test", "idempotencyKey", "flow-a-duplicate"));
            Pass("duplicate-username", duplicate.StatusCode == 409);

            HttpResult caseDuplicate = Post(
                anonymous,
                "api/register",
                Form("csrf", csrfToken, "username", "flowa1", "password", password, "email", "flowa3@example.test", "idempotencyKey", "flow-a-case"));
            Pass("case-duplicate", caseDuplicate.StatusCode == 409);

            HttpResult duplicateEmail = Post(
                anonymous,
                "api/register",
                Form("csrf", csrfToken, "username", "FlowB1", "password", password, "email", "flowa1@example.test", "idempotencyKey", "flow-b-key"));
            Pass("duplicate-email", duplicateEmail.StatusCode == 409);

            Pass(
                "invalid-username",
                Post(anonymous, "api/register", Form("csrf", csrfToken, "username", "Bad Name", "password", password, "email", "badname@example.test", "idempotencyKey", "bad-name")).StatusCode == 400);
            Pass(
                "invalid-email",
                Post(anonymous, "api/register", Form("csrf", csrfToken, "username", "FlowD1", "password", password, "email", "not-email", "idempotencyKey", "bad-email")).StatusCode == 400);
            Pass(
                "weak-password",
                Post(anonymous, "api/register", Form("csrf", csrfToken, "username", "FlowD1", "password", "short", "email", "flowd1@example.test", "idempotencyKey", "bad-pass")).StatusCode == 400);

            Pass(
                "wrong-login",
                Post(anonymous, "api/login", Form("csrf", csrfToken, "username", "FlowA1", "password", "WrongPass!2026")).StatusCode == 401);

            HttpResult login = Post(anonymous, "api/login", Form("csrf", csrfToken, "username", "FlowA1", "password", password));
            Pass("correct-login", login.StatusCode == 200 && login.SetCookie.Contains("aor_session"));
            Pass("session-cookie-flags", login.SetCookie.Contains("HttpOnly") && login.SetCookie.Contains("SameSite=Lax"));

            HttpResult session = Get(anonymous, "api/session");
            Pass("session-created", session.StatusCode == 200 && session.Body.Contains("\"username\":\"FlowA1\""));
            string identityPublicId = ExtractJsonValue(session.Body, "identityPublicId");
            Pass("session-public-id", !string.IsNullOrEmpty(identityPublicId));

            Pass(
                "forum-sso-secret-required",
                Post(anonymous, "api/forum/sso/issue", Form("identityPublicId", identityPublicId)).StatusCode == 403);
            HttpResult ssoIssue = PostWithSecret(
                anonymous,
                "api/forum/sso/issue",
                Form("identityPublicId", identityPublicId, "returnTo", "https://forum.ao-rebirth.com/"),
                "local-sso-secret");
            string ssoCode = ExtractJsonValue(ssoIssue.Body, "code");
            Pass("forum-sso-issued", ssoIssue.StatusCode == 200 && !string.IsNullOrEmpty(ssoCode) && ssoIssue.Body.Contains("\"expiresInSeconds\":120"));
            HttpResult ssoRedeem = PostWithSecret(
                anonymous,
                "api/forum/sso/redeem",
                Form("code", ssoCode),
                "local-sso-secret");
            Pass("forum-sso-redeemed-once", ssoRedeem.StatusCode == 200 && ssoRedeem.Body.Contains("\"username\":\"FlowA1\"") && ssoRedeem.Body.Contains("\"existingMybbUid\":\"\""));
            Pass(
                "forum-sso-replay-rejected",
                PostWithSecret(anonymous, "api/forum/sso/redeem", Form("code", ssoCode), "local-sso-secret").StatusCode == 400);
            HttpResult mapping = PostWithSecret(
                anonymous,
                "api/forum/mapping/confirm",
                Form("identityPublicId", identityPublicId, "mybbUid", "77"),
                "local-sso-secret");
            Pass("forum-mapping-confirmed", mapping.StatusCode == 200 && mapping.Body.Contains("\"mybbUid\":\"77\""));
            HttpResult secondIssue = PostWithSecret(
                anonymous,
                "api/forum/sso/issue",
                Form("identityPublicId", identityPublicId),
                "local-sso-secret");
            string secondCode = ExtractJsonValue(secondIssue.Body, "code");
            HttpResult secondRedeem = PostWithSecret(
                anonymous,
                "api/forum/sso/redeem",
                Form("code", secondCode),
                "local-sso-secret");
            Pass("forum-sso-existing-mapping", secondRedeem.StatusCode == 200 && secondRedeem.Body.Contains("\"existingMybbUid\":\"77\""));

            HttpResult member = Get(anonymous, "member");
            Pass("member-page", member.StatusCode == 200 && member.Body.Contains("Member Account") && member.Body.Contains("FlowA1"));

            string logoutCsrf = ExtractJsonValue(Get(anonymous, "api/csrf").Body, "csrf");
            HttpResult logout = Post(anonymous, "api/logout", Form("csrf", logoutCsrf));
            Pass("logout", logout.StatusCode == 200);
            Pass("session-invalidated", Get(anonymous, "api/session").StatusCode == 401);

            var noCsrf = NewClient();
            Pass(
                "csrf-required",
                Post(noCsrf, "api/register", Form("username", "NoCsrf1", "password", password, "email", "nocsrf@example.test", "idempotencyKey", "no-csrf")).StatusCode == 403);

            string rateCsrf = ExtractJsonValue(Get(anonymous, "api/csrf").Body, "csrf");
            for (int index = 0; index < 3; index++)
            {
                Post(anonymous, "api/login", Form("csrf", rateCsrf, "username", "RateA1", "password", "bad"));
            }

            Pass(
                "login-rate-limit",
                Post(anonymous, "api/login", Form("csrf", rateCsrf, "username", "RateA1", "password", "bad")).StatusCode == 429);

            RunConcurrentDuplicateTest(connectionString);
            RunLegacyMappingRequiredTest(connectionString, anonymous);

            Pass("no-hash-in-register-response", !registration.Body.Contains(hash));
            Pass("health-no-secret", !health.Body.Contains("Password") && !health.Body.Contains("Pwd"));
            Pass("unauthenticated-member-redirect", Get(NewClient(), "member").StatusCode == 302);
        }

        private static void RunConcurrentDuplicateTest(string connectionString)
        {
            var first = NewClient();
            var second = NewClient();
            string firstCsrf = ExtractJsonValue(Get(first, "api/csrf").Body, "csrf");
            string secondCsrf = ExtractJsonValue(Get(second, "api/csrf").Body, "csrf");
            Task<HttpResult> a = Task.Run(
                () => Post(first, "api/register", Form("csrf", firstCsrf, "username", "FlowC1", "password", "FlowPass!2026", "email", "flowc1@example.test", "idempotencyKey", "flow-c-1")));
            Task<HttpResult> b = Task.Run(
                () => Post(second, "api/register", Form("csrf", secondCsrf, "username", "FlowC1", "password", "FlowPass!2026", "email", "flowc2@example.test", "idempotencyKey", "flow-c-2")));
            Task.WaitAll(a, b);
            bool oneSucceeded = (a.Result.StatusCode == 201 && b.Result.StatusCode == 409)
                || (a.Result.StatusCode == 409 && b.Result.StatusCode == 201);
            Pass("concurrent-one-success " + a.Result.StatusCode + "/" + b.Result.StatusCode, oneSucceeded);
            Pass("concurrent-one-row", ScalarLong(connectionString, "SELECT COUNT(*) FROM login WHERE Username='FlowC1'") == 1);
        }

        private static void RunLegacyMappingRequiredTest(string connectionString, CookieContainer cookies)
        {
            Execute(
                connectionString,
                "INSERT INTO login (CreationDate, Email, FirstName, LastName, Username, Password, AllowedCharacters, Flags, AccountFlags, Expansions, GM) VALUES (CURRENT_TIMESTAMP(), 'legacy@example.test', '', '', 'LegacyA', @password, 6, 0, 0, 127, 0)",
                new MySqlParameter("@password", PasswordHash.CreateHash("LegacyPass!2026")));
            string csrfToken = ExtractJsonValue(Get(cookies, "api/csrf").Body, "csrf");
            HttpResult result = Post(cookies, "api/login", Form("csrf", csrfToken, "username", "LegacyA", "password", "LegacyPass!2026"));
            Pass("legacy-unmapped-safe-fail", result.StatusCode == 401 && result.Body.Contains("IDENTITY_MAPPING_REQUIRED"));
        }

        private static Process StartService()
        {
#if DEBUG
            string configuration = "Debug";
#else
            string configuration = "Release";
#endif
            string executable = Path.Combine(Environment.CurrentDirectory, "AORebirth", "Built", configuration, "AORebirth.AccountBroker.Service.exe");
            var start = new ProcessStartInfo(executable, "/url " + ServiceUrl)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.EnvironmentVariables["AOREBIRTH_ACCOUNT_BROKER_REGISTER_LIMIT"] = "50";
            start.EnvironmentVariables["AOREBIRTH_ACCOUNT_BROKER_LOGIN_LIMIT"] = "3";
            start.EnvironmentVariables["AOREBIRTH_ACCOUNT_BROKER_FORUM_SSO_SECRET"] = "local-sso-secret";
            return Process.Start(start);
        }

        private static void WaitForHealth()
        {
            var cookies = NewClient();
            Exception last = null;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                try
                {
                    if (Get(cookies, "health").StatusCode == 200)
                    {
                        return;
                    }
                }
                catch (Exception exception)
                {
                    last = exception;
                }

                Thread.Sleep(250);
            }

            throw new InvalidOperationException("Broker service did not become healthy: " + (last == null ? "no response" : last.Message));
        }

        private static CookieContainer NewClient()
        {
            return new CookieContainer();
        }

        private static HttpResult Get(CookieContainer cookies, string path)
        {
            return Request(cookies, "GET", path, null);
        }

        private static HttpResult Post(CookieContainer cookies, string path, string body)
        {
            return Request(cookies, "POST", path, body, null);
        }

        private static HttpResult PostWithSecret(CookieContainer cookies, string path, string body, string secret)
        {
            return Request(cookies, "POST", path, body, secret);
        }

        private static HttpResult Request(CookieContainer cookies, string method, string path, string body)
        {
            return Request(cookies, method, path, body, null);
        }

        private static HttpResult Request(CookieContainer cookies, string method, string path, string body, string forumSsoSecret)
        {
            var request = (HttpWebRequest)WebRequest.Create(ServiceUrl + path);
            request.Method = method;
            request.CookieContainer = cookies;
            request.AllowAutoRedirect = false;
            if (!string.IsNullOrEmpty(forumSsoSecret))
            {
                request.Headers["X-AORebirth-Forum-SSO-Secret"] = forumSsoSecret;
            }

            if (body != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = bytes.Length;
                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    return ReadResponse(response);
                }
            }
            catch (WebException exception)
            {
                using (var response = (HttpWebResponse)exception.Response)
                {
                    return ReadResponse(response);
                }
            }
        }

        private static HttpResult ReadResponse(HttpWebResponse response)
        {
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                return new HttpResult
                {
                    StatusCode = (int)response.StatusCode,
                    Body = reader.ReadToEnd(),
                    SetCookie = response.Headers["Set-Cookie"] ?? string.Empty
                };
            }
        }

        private static string Form(params string[] values)
        {
            var builder = new StringBuilder();
            for (int index = 0; index < values.Length; index += 2)
            {
                if (builder.Length > 0)
                {
                    builder.Append('&');
                }

                builder.Append(Uri.EscapeDataString(values[index]));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(values[index + 1] ?? string.Empty));
            }

            return builder.ToString();
        }

        private static string ExtractJsonValue(string json, string name)
        {
            string marker = "\"" + name + "\":\"";
            int start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }

            start += marker.Length;
            int end = json.IndexOf('"', start);
            return end < 0 ? null : json.Substring(start, end - start);
        }

        private static void ResetLocalDatabase(string connectionString)
        {
            CleanupFixtures(connectionString);
            Execute(connectionString, "DROP TABLE IF EXISTS account_provisioning_jobs");
            Execute(connectionString, "DROP TABLE IF EXISTS account_external_mappings");
            Execute(connectionString, "DROP TABLE IF EXISTS account_game_mappings");
            Execute(connectionString, "DROP TABLE IF EXISTS account_identities");
            string schema = File.ReadAllText(
                Path.Combine(Environment.CurrentDirectory, "AORebirth", "Libraries", "Source", "AORebirth.Database", "SqlTables", "aorebirth_identity.sql"));
            foreach (string statement in SplitSql(schema))
            {
                Execute(connectionString, statement);
            }
        }

        private static IEnumerable<string> SplitSql(string sql)
        {
            var builder = new StringBuilder();
            using (var reader = new StringReader(sql))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.TrimStart().StartsWith("--", StringComparison.Ordinal))
                    {
                        builder.AppendLine(line);
                    }
                }
            }

            foreach (string statement in builder.ToString().Split(';'))
            {
                if (!string.IsNullOrWhiteSpace(statement))
                {
                    yield return statement;
                }
            }
        }

        private static void CleanupFixtures(string connectionString)
        {
            string names = "'" + string.Join("','", FixtureUsers) + "'";
            Execute(connectionString, "DELETE FROM characters WHERE Username IN (" + names + ")");
            Execute(connectionString, "DELETE FROM login WHERE Username IN (" + names + ")");
        }

        private static long ScalarLong(string connectionString, string sql)
        {
            object value = Scalar(connectionString, sql);
            return Convert.ToInt64(value);
        }

        private static string ScalarString(string connectionString, string sql)
        {
            object value = Scalar(connectionString, sql);
            return Convert.ToString(value);
        }

        private static object Scalar(string connectionString, string sql)
        {
            using (var connection = new MySqlConnection(connectionString))
            using (var command = new MySqlCommand(sql, connection))
            {
                connection.Open();
                return command.ExecuteScalar();
            }
        }

        private static void Execute(string connectionString, string sql, params MySqlParameter[] parameters)
        {
            using (var connection = new MySqlConnection(connectionString))
            using (var command = new MySqlCommand(sql, connection))
            {
                foreach (MySqlParameter parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }

                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private static void Pass(string name, bool condition)
        {
            if (!condition)
            {
                throw new InvalidOperationException("validation failed: " + name);
            }

            passed++;
        }

        private sealed class HttpResult
        {
            public int StatusCode { get; set; }

            public string Body { get; set; }

            public string SetCookie { get; set; }
        }
    }
}
