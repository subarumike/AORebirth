namespace UnifiedAccountFlowValidation
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using AORebirth.Core.Encryption;

    using MySqlConnector;

    internal static class Program
    {
        private const string ServiceUrl = "http://127.0.0.1:17931/";

        private const int SmtpPort = 17932;

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

        private static readonly List<string> SensitiveLogValues = new List<string>();

        private static int Main()
        {
            string connectionString = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("FAIL AO_REBIRTH_MYSQL_CONNECTION is missing.");
                return 1;
            }

            Process service = null;
            FakeSmtpServer smtp = null;
            try
            {
                ResetLocalDatabase(connectionString);
                smtp = new FakeSmtpServer(SmtpPort);
                smtp.Start();
                service = StartService();
                WaitForHealth();
                RunHttpFlow(connectionString, smtp);
                ValidateServiceLogs(service);
                CleanupFixtures(connectionString);
                Console.WriteLine("PASS UnifiedAccountFlowValidation " + passed + "/" + passed);
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

                if (smtp != null)
                {
                    smtp.Dispose();
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

        private static void RunHttpFlow(string connectionString, FakeSmtpServer smtp)
        {
            var anonymous = NewClient();
            HttpResult health = Get(anonymous, "health");
            Pass("health", health.StatusCode == 200 && health.Body.Contains("\"ready\""));

            HttpResult registerPage = Get(anonymous, "register");
            Pass("register-page", registerPage.StatusCode == 200 && registerPage.Body.Contains("Create AORebirth Account"));
            Pass("forgot-password-page", Get(anonymous, "forgot-password").StatusCode == 200);
            HttpResult invalidResetPage = Get(anonymous, "reset-password");
            Pass("reset-password-safe-invalid-page", invalidResetPage.StatusCode == 200 && invalidResetPage.Body.Contains("invalid or expired"));
            Pass("password-page-requires-session", Get(anonymous, "account/password").StatusCode == 302);

            HttpResult csrf = Get(anonymous, "api/csrf");
            string csrfToken = ExtractJsonValue(csrf.Body, "csrf");
            Pass("csrf", csrf.StatusCode == 200 && !string.IsNullOrEmpty(csrfToken));

            string password = "FlowPass!2026";
            SensitiveLogValues.Add(password);
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

            string verificationMessage = smtp.WaitForMessage("AORebirth account email verification", 5000);
            string verificationToken = ExtractMessageToken(verificationMessage, "#token=");
            SensitiveLogValues.Add(verificationToken);
            Pass("verification-email-captured", !string.IsNullOrEmpty(verificationToken));
            HttpResult verified = PostWithAccountSecret(
                anonymous,
                "api/email/verification/verify",
                Form("token", verificationToken),
                "local-account-secret");
            Pass("email-verified", verified.StatusCode == 200 && verified.Body.Contains("\"verified\":true"));

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

            HttpResult passwordPage = Get(anonymous, "account/password");
            Pass("password-page", passwordPage.StatusCode == 200 && passwordPage.Body.Contains("Change password"));
            Pass(
                "password-change-csrf-required",
                Post(
                    anonymous,
                    "account/password",
                    Form(
                        "currentPassword", password,
                        "newPassword", "FlowChanged!2026",
                        "confirmPassword", "FlowChanged!2026")).StatusCode == 403);
            Pass(
                "password-change-secret-required",
                Post(
                    anonymous,
                    "api/account/password/change",
                    Form(
                        "identityPublicId", identityPublicId,
                        "currentPassword", password,
                        "newPassword", "FlowChanged!2026",
                        "confirmPassword", "FlowChanged!2026")).StatusCode == 403);
            HttpResult wrongCurrent = PostWithAccountSecret(
                anonymous,
                "api/account/password/change",
                Form(
                    "identityPublicId", identityPublicId,
                    "currentPassword", "WrongCurrent!2026",
                    "newPassword", "FlowChanged!2026",
                    "confirmPassword", "FlowChanged!2026"),
                "local-account-secret");
            Pass("password-change-current-required", wrongCurrent.StatusCode == 400 && wrongCurrent.Body.Contains("INVALID_CURRENT_PASSWORD"));
            HttpResult confirmationMismatch = PostWithAccountSecret(
                anonymous,
                "api/account/password/change",
                Form(
                    "identityPublicId", identityPublicId,
                    "currentPassword", password,
                    "newPassword", "FlowChanged!2026",
                    "confirmPassword", "Different!2026"),
                "local-account-secret");
            Pass("password-change-confirmation-required", confirmationMismatch.StatusCode == 400 && confirmationMismatch.Body.Contains("PASSWORD_CONFIRMATION_MISMATCH"));

            string changeCsrf = ExtractJsonValue(Get(anonymous, "api/csrf").Body, "csrf");
            HttpResult passwordChange = Post(
                anonymous,
                "account/password",
                Form(
                    "csrf", changeCsrf,
                    "currentPassword", password,
                    "newPassword", "FlowChanged!2026",
                    "confirmPassword", "FlowChanged!2026"));
            Pass("password-change-route", passwordChange.StatusCode == 302);
            Pass("password-change-invalidates-session", Get(anonymous, "api/session").StatusCode == 401);
            string changedHash = ScalarString(connectionString, "SELECT Password FROM login WHERE Username='FlowA1'");
            Pass("old-password-fails-after-change", !PasswordHash.ValidatePassword(password, changedHash));
            Pass("new-password-passes-after-change", PasswordHash.ValidatePassword("FlowChanged!2026", changedHash));

            string changedLoginCsrf = ExtractJsonValue(Get(anonymous, "api/csrf").Body, "csrf");
            Pass(
                "old-website-login-fails-after-change",
                Post(anonymous, "api/login", Form("csrf", changedLoginCsrf, "username", "FlowA1", "password", password)).StatusCode == 401);
            HttpResult changedLogin = Post(
                anonymous,
                "api/login",
                Form("csrf", changedLoginCsrf, "username", "FlowA1", "password", "FlowChanged!2026"));
            Pass("new-website-login-passes-after-change", changedLogin.StatusCode == 200);

            Pass(
                "forgot-password-csrf-required",
                Post(anonymous, "forgot-password", Form("email", "flowa1@example.test")).StatusCode == 403);
            Pass(
                "csrf-failure-does-not-issue-reset",
                ScalarLong(connectionString, "SELECT COUNT(*) FROM account_password_reset_tokens") == 0);

            HttpResult resetRequest = PostWithAccountSecret(
                anonymous,
                "api/password/reset/request",
                Form("email", "flowa1@example.test"),
                "local-account-secret");
            HttpResult unknownResetRequest = PostWithAccountSecret(
                anonymous,
                "api/password/reset/request",
                Form("email", "unknown@example.test"),
                "local-account-secret");
            Pass(
                "reset-request-generic-response",
                resetRequest.StatusCode == 200
                    && unknownResetRequest.StatusCode == 200
                    && resetRequest.Body == unknownResetRequest.Body);
            string resetMessage = smtp.WaitForMessage("AORebirth password reset", 5000);
            string resetToken = ExtractMessageToken(resetMessage, "/reset-password#token=");
            string decodedResetMessage = DecodeMessageBody(resetMessage);
            SensitiveLogValues.Add(resetToken);
            SensitiveLogValues.Add("FlowChanged!2026");
            SensitiveLogValues.Add("FlowReset!2026");
            Pass("reset-email-captured", !string.IsNullOrEmpty(resetToken));
            Pass("reset-email-fragment-token", decodedResetMessage.Contains("/reset-password#token="));
            Pass("reset-email-no-query-token", !decodedResetMessage.Contains("/reset-password?token="));
            Pass("reset-token-not-stored-plaintext", ResetTokenStoredAsDigest(connectionString, resetToken));
            Pass(
                "reset-status-secret-required",
                Post(anonymous, "api/password/reset/status", Form("token", resetToken)).StatusCode == 403);
            HttpResult resetStatus = PostWithAccountSecret(
                anonymous,
                "api/password/reset/status",
                Form("token", resetToken),
                "local-account-secret");
            Pass("reset-token-status-valid", resetStatus.StatusCode == 200 && resetStatus.Body.Contains("\"valid\":true"));
            HttpResult resetPage = Get(anonymous, "reset-password?token=" + Uri.EscapeDataString(resetToken));
            Pass("reset-password-page", resetPage.StatusCode == 200 && resetPage.Body.Contains("Confirm new password"));
            Pass(
                "reset-confirmation-required",
                PostWithAccountSecret(
                    anonymous,
                    "api/password/reset/consume",
                    Form(
                        "token", resetToken,
                        "newPassword", "FlowReset!2026",
                        "confirmPassword", "Different!2026"),
                    "local-account-secret").StatusCode == 400);
            Pass(
                "reset-policy-required",
                PostWithAccountSecret(
                    anonymous,
                    "api/password/reset/consume",
                    Form("token", resetToken, "newPassword", "short", "confirmPassword", "short"),
                    "local-account-secret").StatusCode == 400);
            Pass(
                "reset-csrf-required",
                Post(
                    NewClient(),
                    "reset-password",
                    Form(
                        "token", resetToken,
                        "newPassword", "FlowReset!2026",
                        "confirmPassword", "FlowReset!2026")).StatusCode == 403);
            string resetCsrf = ExtractJsonValue(Get(anonymous, "api/csrf").Body, "csrf");
            HttpResult resetPassword = Post(
                anonymous,
                "reset-password",
                Form(
                    "csrf", resetCsrf,
                    "token", resetToken,
                    "newPassword", "FlowReset!2026",
                    "confirmPassword", "FlowReset!2026"));
            Pass("reset-password-route", resetPassword.StatusCode == 302);
            Pass("password-reset-invalidates-session", Get(anonymous, "api/session").StatusCode == 401);
            Pass(
                "reset-token-single-use",
                PostWithAccountSecret(
                    anonymous,
                    "api/password/reset/consume",
                    Form(
                        "token", resetToken,
                        "newPassword", "ReplayReset!2026",
                        "confirmPassword", "ReplayReset!2026"),
                    "local-account-secret").StatusCode == 400);
            string resetHash = ScalarString(connectionString, "SELECT Password FROM login WHERE Username='FlowA1'");
            Pass("pre-reset-password-fails", !PasswordHash.ValidatePassword("FlowChanged!2026", resetHash));
            Pass("final-reset-password-passes", PasswordHash.ValidatePassword("FlowReset!2026", resetHash));

            string finalLoginCsrf = ExtractJsonValue(Get(anonymous, "api/csrf").Body, "csrf");
            Pass(
                "pre-reset-website-login-fails",
                Post(anonymous, "api/login", Form("csrf", finalLoginCsrf, "username", "FlowA1", "password", "FlowChanged!2026")).StatusCode == 401);
            HttpResult finalLogin = Post(
                anonymous,
                "api/login",
                Form("csrf", finalLoginCsrf, "username", "FlowA1", "password", "FlowReset!2026"));
            Pass("final-reset-website-login-passes", finalLogin.StatusCode == 200);

            HttpResult secondEligibleReset = PostWithAccountSecret(
                anonymous,
                "api/password/reset/request",
                Form("email", "flowa1@example.test"),
                "local-account-secret");
            HttpResult rateLimitedReset = PostWithAccountSecret(
                anonymous,
                "api/password/reset/request",
                Form("email", "flowa1@example.test"),
                "local-account-secret");
            Pass(
                "reset-rate-limit-remains-generic",
                secondEligibleReset.StatusCode == 200
                    && rateLimitedReset.StatusCode == 200
                    && secondEligibleReset.Body == rateLimitedReset.Body);
            Pass(
                "reset-target-rate-limit-bounded",
                ScalarLong(connectionString, "SELECT COUNT(*) FROM account_password_reset_tokens t INNER JOIN account_identities i ON i.IdentityId=t.IdentityId WHERE i.NormalizedUsername='flowa1'") == 2);

            HttpResult unverifiedRegistration = Post(
                anonymous,
                "api/register",
                Form(
                    "csrf", finalLoginCsrf,
                    "username", "FlowB1",
                    "password", "FlowPass!2026",
                    "email", "flowb1@example.test",
                    "idempotencyKey", "flow-b-unverified"));
            Pass("unverified-fixture-created", unverifiedRegistration.StatusCode == 201);
            HttpResult unverifiedReset = PostWithAccountSecret(
                anonymous,
                "api/password/reset/request",
                Form("email", "flowb1@example.test"),
                "local-account-secret");
            Pass("unverified-reset-response-generic", unverifiedReset.StatusCode == 200 && unverifiedReset.Body == resetRequest.Body);
            Pass(
                "unverified-reset-token-not-issued",
                ScalarLong(connectionString, "SELECT COUNT(*) FROM account_password_reset_tokens t INNER JOIN account_identities i ON i.IdentityId=t.IdentityId WHERE i.NormalizedUsername='flowb1'") == 0);

            string logoutCsrf = ExtractJsonValue(Get(anonymous, "api/csrf").Body, "csrf");
            HttpResult logout = Post(anonymous, "api/logout", Form("csrf", logoutCsrf));
            Pass("logout", logout.StatusCode == 200);
            Pass("session-invalidated", Get(anonymous, "api/session").StatusCode == 401);

            var noCsrf = NewClient();
            Pass(
                "csrf-required",
                Post(noCsrf, "api/register", Form("username", "NoCsrf1", "password", password, "email", "nocsrf@example.test", "idempotencyKey", "no-csrf")).StatusCode == 403);

            string rateCsrf = ExtractJsonValue(Get(anonymous, "api/csrf").Body, "csrf");
            for (int index = 0; index < 10; index++)
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
            start.EnvironmentVariables["AOREBIRTH_ACCOUNT_BROKER_LOGIN_LIMIT"] = "10";
            start.EnvironmentVariables["AOREBIRTH_ACCOUNT_BROKER_FORUM_SSO_SECRET"] = "local-sso-secret";
            start.EnvironmentVariables["AOREBIRTH_ACCOUNT_BROKER_ACCOUNT_MAIL_SECRET"] = "local-account-secret";
            start.EnvironmentVariables["AOREBIRTH_ACCOUNT_BROKER_PASSWORD_RESET_IP_LIMIT"] = "20";
            start.EnvironmentVariables["AOREBIRTH_ACCOUNT_BROKER_PASSWORD_RESET_TARGET_LIMIT"] = "2";
            start.EnvironmentVariables["AOREBIRTH_PASSWORD_RESET_TOKEN_MINUTES"] = "30";
            start.EnvironmentVariables["AOREBIRTH_PUBLIC_BASE_URL"] = ServiceUrl.TrimEnd('/');
            start.EnvironmentVariables["AOREBIRTH_MAIL_SMTP_HOST"] = "127.0.0.1";
            start.EnvironmentVariables["AOREBIRTH_MAIL_SMTP_PORT"] = SmtpPort.ToString();
            start.EnvironmentVariables["AOREBIRTH_MAIL_SMTP_TLS"] = "None";
            start.EnvironmentVariables["AOREBIRTH_MAIL_SMTP_USERNAME"] = "local-user";
            start.EnvironmentVariables["AOREBIRTH_MAIL_SMTP_PASSWORD"] = "local-password";
            start.EnvironmentVariables["AOREBIRTH_MAIL_FROM_ADDRESS"] = "noreply@example.test";
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

        private static void ValidateServiceLogs(Process service)
        {
            if (service != null && !service.HasExited)
            {
                service.Kill();
                service.WaitForExit(5000);
            }

            string logs = service == null
                ? string.Empty
                : service.StandardOutput.ReadToEnd() + service.StandardError.ReadToEnd();
            bool safe = true;
            foreach (string secret in SensitiveLogValues)
            {
                if (!string.IsNullOrEmpty(secret)
                    && logs.IndexOf(secret, StringComparison.Ordinal) >= 0)
                {
                    safe = false;
                }
            }

            Pass("logs-do-not-contain-passwords-or-tokens", safe);
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
            return Request(cookies, "POST", path, body);
        }

        private static HttpResult PostWithSecret(CookieContainer cookies, string path, string body, string secret)
        {
            return Request(cookies, "POST", path, body, secret, null);
        }

        private static HttpResult PostWithAccountSecret(CookieContainer cookies, string path, string body, string secret)
        {
            return Request(cookies, "POST", path, body, null, secret);
        }

        private static HttpResult Request(CookieContainer cookies, string method, string path, string body)
        {
            return Request(cookies, method, path, body, null, null);
        }

        private static HttpResult Request(
            CookieContainer cookies,
            string method,
            string path,
            string body,
            string forumSsoSecret,
            string accountMailSecret)
        {
            var request = (HttpWebRequest)WebRequest.Create(ServiceUrl + path);
            request.Method = method;
            request.CookieContainer = cookies;
            request.AllowAutoRedirect = false;
            if (!string.IsNullOrEmpty(forumSsoSecret))
            {
                request.Headers["X-AORebirth-Forum-SSO-Secret"] = forumSsoSecret;
            }
            if (!string.IsNullOrEmpty(accountMailSecret))
            {
                request.Headers["X-AORebirth-Account-Mail-Secret"] = accountMailSecret;
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

        private static string ExtractMessageToken(string message, string marker)
        {
            string decoded = DecodeMessageBody(message);
            int start = decoded.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }

            start += marker.Length;
            int end = start;
            while (end < decoded.Length)
            {
                char value = decoded[end];
                if (!((value >= 'A' && value <= 'Z')
                    || (value >= 'a' && value <= 'z')
                    || (value >= '0' && value <= '9')
                    || value == '-'
                    || value == '_'))
                {
                    break;
                }

                end++;
            }

            return end == start ? null : decoded.Substring(start, end - start);
        }

        private static string DecodeMessageBody(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            int bodyStart = message.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (bodyStart < 0)
            {
                return message;
            }

            string headers = message.Substring(0, bodyStart);
            string body = message.Substring(bodyStart + 4);
            if (headers.IndexOf("Content-Transfer-Encoding: base64", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                try
                {
                    return Encoding.UTF8.GetString(
                        Convert.FromBase64String(body.Replace("\r", string.Empty).Replace("\n", string.Empty)));
                }
                catch (FormatException)
                {
                    return body;
                }
            }

            if (headers.IndexOf("Content-Transfer-Encoding: quoted-printable", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return body;
            }

            var decoded = new StringBuilder();
            for (int index = 0; index < body.Length; index++)
            {
                if (body[index] == '=' && index + 1 < body.Length)
                {
                    if (body[index + 1] == '\r' && index + 2 < body.Length && body[index + 2] == '\n')
                    {
                        index += 2;
                        continue;
                    }

                    if (index + 2 < body.Length)
                    {
                        int high = HexValue(body[index + 1]);
                        int low = HexValue(body[index + 2]);
                        if (high >= 0 && low >= 0)
                        {
                            decoded.Append((char)((high << 4) | low));
                            index += 2;
                            continue;
                        }
                    }
                }

                decoded.Append(body[index]);
            }

            return decoded.ToString();
        }

        private static int HexValue(char value)
        {
            if (value >= '0' && value <= '9')
            {
                return value - '0';
            }

            if (value >= 'a' && value <= 'f')
            {
                return value - 'a' + 10;
            }

            return value >= 'A' && value <= 'F' ? value - 'A' + 10 : -1;
        }

        private static bool ResetTokenStoredAsDigest(string connectionString, string token)
        {
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
            }

            using (var connection = new MySqlConnection(connectionString))
            using (var command = new MySqlCommand(
                "SELECT TokenHash FROM account_password_reset_tokens WHERE TokenHash=@tokenHash",
                connection))
            {
                command.Parameters.Add(new MySqlParameter("@tokenHash", digest));
                connection.Open();
                byte[] stored = command.ExecuteScalar() as byte[];
                if (stored == null || stored.Length != digest.Length || stored.Length == Encoding.UTF8.GetByteCount(token))
                {
                    return false;
                }

                int difference = 0;
                for (int index = 0; index < stored.Length; index++)
                {
                    difference |= stored[index] ^ digest[index];
                }

                return difference == 0;
            }
        }

        private static void ResetLocalDatabase(string connectionString)
        {
            CleanupFixtures(connectionString);
            Execute(connectionString, "DROP TABLE IF EXISTS account_provisioning_jobs");
            Execute(connectionString, "DROP TABLE IF EXISTS account_password_reset_tokens");
            Execute(connectionString, "DROP TABLE IF EXISTS account_email_verification_tokens");
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

        private sealed class FakeSmtpServer : IDisposable
        {
            private readonly TcpListener listener;

            private readonly List<string> messages = new List<string>();

            private readonly AutoResetEvent received = new AutoResetEvent(false);

            private Thread worker;

            public FakeSmtpServer(int port)
            {
                this.listener = new TcpListener(IPAddress.Loopback, port);
            }

            public void Start()
            {
                this.listener.Start();
                this.worker = new Thread(this.Run) { IsBackground = true };
                this.worker.Start();
            }

            public string WaitForMessage(string marker, int timeoutMilliseconds)
            {
                DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
                while (DateTime.UtcNow < deadline)
                {
                    lock (this.messages)
                    {
                        foreach (string message in this.messages)
                        {
                            if (DecodeMessageBody(message).IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return message;
                            }
                        }
                    }

                    int remaining = (int)Math.Max(1, (deadline - DateTime.UtcNow).TotalMilliseconds);
                    this.received.WaitOne(Math.Min(remaining, 250));
                }

                return null;
            }

            public void Dispose()
            {
                this.listener.Stop();
                if (this.worker != null)
                {
                    this.worker.Join(2000);
                }

                this.received.Dispose();
            }

            private void Run()
            {
                while (true)
                {
                    try
                    {
                        using (TcpClient client = this.listener.AcceptTcpClient())
                        {
                            this.HandleClient(client);
                        }
                    }
                    catch (SocketException)
                    {
                        return;
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                }
            }

            private void HandleClient(TcpClient client)
            {
                using (NetworkStream stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                using (var writer = new StreamWriter(stream, Encoding.ASCII, 1024, true) { AutoFlush = true, NewLine = "\r\n" })
                {
                    writer.WriteLine("220 localhost AORebirth test SMTP");
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
                        {
                            writer.WriteLine("250-localhost");
                            writer.WriteLine("250-AUTH LOGIN");
                            writer.WriteLine("250 OK");
                        }
                        else if (line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                        {
                            writer.WriteLine("250 localhost");
                        }
                        else if (line.StartsWith("AUTH LOGIN", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] parts = line.Split(' ');
                            if (parts.Length < 3)
                            {
                                writer.WriteLine("334 VXNlcm5hbWU6");
                                reader.ReadLine();
                            }

                            writer.WriteLine("334 UGFzc3dvcmQ6");
                            reader.ReadLine();
                            writer.WriteLine("235 Authentication successful");
                        }
                        else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase)
                            || line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
                        {
                            writer.WriteLine("250 OK");
                        }
                        else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                        {
                            writer.WriteLine("354 End data with <CR><LF>.<CR><LF>");
                            var message = new StringBuilder();
                            string dataLine;
                            while ((dataLine = reader.ReadLine()) != null && dataLine != ".")
                            {
                                if (dataLine.StartsWith("..", StringComparison.Ordinal))
                                {
                                    dataLine = dataLine.Substring(1);
                                }

                                message.Append(dataLine).Append("\r\n");
                            }

                            lock (this.messages)
                            {
                                this.messages.Add(message.ToString());
                            }

                            this.received.Set();
                            writer.WriteLine("250 Message accepted");
                        }
                        else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                        {
                            writer.WriteLine("221 Bye");
                            return;
                        }
                        else
                        {
                            writer.WriteLine("250 OK");
                        }
                    }
                }
            }
        }

        private sealed class HttpResult
        {
            public int StatusCode { get; set; }

            public string Body { get; set; }

            public string SetCookie { get; set; }
        }
    }
}
