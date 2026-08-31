namespace AORebirth.AccountBroker.Service
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Mail;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;

    using AORebirth.AccountBroker;

    using MySqlConnector;

    internal static class Program
    {
        private const string DefaultPrefix = "http://127.0.0.1:7510/";

        private static int Main(string[] args)
        {
            string prefix = GetArgumentValue(args, "/url") ?? GetEnvironment("AOREBIRTH_ACCOUNT_BROKER_URL", DefaultPrefix);
            string connectionString = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine("AO_REBIRTH_MYSQL_CONNECTION is required.");
                return 2;
            }

            bool allowPrivateBind = string.Equals(
                Environment.GetEnvironmentVariable("AOREBIRTH_ACCOUNT_BROKER_ALLOW_PRIVATE_BIND"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            if (!IsAllowedListenPrefix(prefix, allowPrivateBind))
            {
                Console.Error.WriteLine("Account Broker service requires a loopback URL unless explicit private-bind mode is enabled.");
                return 2;
            }

            var broker = new AccountBrokerService(() => new MySqlConnection(connectionString));
            var host = new AccountBrokerHttpHost(prefix, broker);
            Console.WriteLine("AORebirth Account Broker service listening on " + prefix);
            host.Run();
            return 0;
        }

        private static string GetEnvironment(string name, string defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private static string GetArgumentValue(string[] args, string argument)
        {
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], argument, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        private static bool IsAllowedListenPrefix(string prefix, bool allowPrivateBind)
        {
            Uri uri;
            if (!Uri.TryCreate(prefix, UriKind.Absolute, out uri))
            {
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            string host = uri.Host;
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            IPAddress address;
            if (!IPAddress.TryParse(host, out address))
            {
                return false;
            }

            if (IPAddress.IsLoopback(address))
            {
                return true;
            }

            return allowPrivateBind && IsPrivateIPv4(address);
        }

        private static bool IsPrivateIPv4(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();
            return bytes.Length == 4
                && (bytes[0] == 10
                    || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    || (bytes[0] == 192 && bytes[1] == 168));
        }
    }

    internal sealed class AccountBrokerHttpHost
    {
        private const int MaxRequestBytes = 8192;

        private const string SessionCookieName = "aor_session";

        private const string CsrfCookieName = "aor_csrf";

        private readonly AccountBrokerService broker;

        private readonly CsrfTokenStore csrfTokens = new CsrfTokenStore();

        private readonly HttpListener listener = new HttpListener();

        private readonly FixedWindowRateLimiter loginLimiter;

        private readonly FixedWindowRateLimiter registrationLimiter;

        private readonly FixedWindowRateLimiter emailVerificationLimiter;

        private readonly FixedWindowRateLimiter passwordResetIpLimiter;

        private readonly FixedWindowRateLimiter passwordResetTargetLimiter;

        private readonly AccountEmailSender emailSender;

        private readonly ForumSsoCodeStore forumSsoCodes;

        private readonly string forumSsoSecret;

        private readonly string accountMailSecret;

        private readonly string publicBaseUrl;

        private readonly int emailVerificationTtlMinutes;

        private readonly int passwordResetTtlMinutes;

        private readonly WebSessionStore sessions;

        public AccountBrokerHttpHost(string prefix, AccountBrokerService broker)
        {
            this.broker = broker;
            this.listener.Prefixes.Add(EnsureTrailingSlash(prefix));
            this.sessions = new WebSessionStore(GetIntEnvironment("AOREBIRTH_ACCOUNT_BROKER_SESSION_MINUTES", 480));
            this.forumSsoSecret = GetSecretEnvironment(
                "AOREBIRTH_ACCOUNT_BROKER_FORUM_SSO_SECRET",
                "AOREBIRTH_ACCOUNT_BROKER_FORUM_SSO_SECRET_FILE");
            this.accountMailSecret = GetSecretEnvironment(
                "AOREBIRTH_ACCOUNT_BROKER_ACCOUNT_MAIL_SECRET",
                "AOREBIRTH_ACCOUNT_BROKER_ACCOUNT_MAIL_SECRET_FILE");
            this.publicBaseUrl = GetEnvironment("AOREBIRTH_PUBLIC_BASE_URL", "https://ao-rebirth.com").TrimEnd('/');
            this.emailVerificationTtlMinutes = GetIntEnvironment("AOREBIRTH_EMAIL_VERIFICATION_TOKEN_MINUTES", 120);
            this.passwordResetTtlMinutes = GetIntEnvironment("AOREBIRTH_PASSWORD_RESET_TOKEN_MINUTES", 30);
            this.emailSender = AccountEmailSender.FromEnvironment();
            this.forumSsoCodes = new ForumSsoCodeStore(GetIntEnvironment("AOREBIRTH_ACCOUNT_BROKER_FORUM_SSO_SECONDS", 120));
            this.registrationLimiter = new FixedWindowRateLimiter(
                GetIntEnvironment("AOREBIRTH_ACCOUNT_BROKER_REGISTER_LIMIT", 5),
                TimeSpan.FromMinutes(10));
            this.loginLimiter = new FixedWindowRateLimiter(
                GetIntEnvironment("AOREBIRTH_ACCOUNT_BROKER_LOGIN_LIMIT", 5),
                TimeSpan.FromMinutes(5));
            this.emailVerificationLimiter = new FixedWindowRateLimiter(
                GetIntEnvironment("AOREBIRTH_ACCOUNT_BROKER_EMAIL_VERIFY_LIMIT", 3),
                TimeSpan.FromMinutes(15));
            TimeSpan passwordResetWindow = TimeSpan.FromMinutes(
                GetIntEnvironment("AOREBIRTH_ACCOUNT_BROKER_PASSWORD_RESET_WINDOW_MINUTES", 15));
            this.passwordResetIpLimiter = new FixedWindowRateLimiter(
                GetIntEnvironment("AOREBIRTH_ACCOUNT_BROKER_PASSWORD_RESET_IP_LIMIT", 10),
                passwordResetWindow);
            this.passwordResetTargetLimiter = new FixedWindowRateLimiter(
                GetIntEnvironment("AOREBIRTH_ACCOUNT_BROKER_PASSWORD_RESET_TARGET_LIMIT", 3),
                passwordResetWindow);
        }

        public void Run()
        {
            this.listener.Start();
            while (this.listener.IsListening)
            {
                HttpListenerContext context = this.listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => this.Handle(context));
            }
        }

        private void Handle(HttpListenerContext context)
        {
            try
            {
                this.Route(context);
            }
            catch (Exception)
            {
                WriteJson(context.Response, 500, "{\"ok\":false,\"error\":\"SERVER_ERROR\"}");
            }
        }

        private void Route(HttpListenerContext context)
        {
            string path = context.Request.Url.AbsolutePath.TrimEnd('/');
            if (path.Length == 0)
            {
                path = "/";
            }

            if (context.Request.HttpMethod == "GET" && path == "/health")
            {
                WriteJson(context.Response, 200, "{\"ok\":true,\"status\":\"ready\"}");
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/api/csrf")
            {
                string token = this.IssueCsrf(context);
                WriteJson(context.Response, 200, "{\"ok\":true,\"csrf\":\"" + Json(token) + "\"}");
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/register")
            {
                this.WriteRegisterPage(context, null);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/login")
            {
                this.WriteLoginPage(context, null);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/forgot-password")
            {
                this.WriteForgotPasswordPage(context, null, false);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/reset-password")
            {
                this.WriteResetPasswordPage(context, context.Request.QueryString["token"], null);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/change-password")
            {
                Redirect(context.Response, "/account/password");
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/account/password")
            {
                this.WritePasswordPage(context, null);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/member")
            {
                this.WriteMemberPage(context);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/api/session")
            {
                this.WriteSession(context);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/api/account/identity")
            {
                this.HandleAccountIdentity(context);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/api/account/characters")
            {
                this.HandleAccountCharacters(context);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/api/account/password/change")
            {
                this.HandleInternalPasswordChange(context);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/api/password/reset/request")
            {
                this.HandleInternalPasswordResetRequest(context);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/api/password/reset/status")
            {
                this.HandleInternalPasswordResetStatus(context);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/api/password/reset/consume")
            {
                this.HandleInternalPasswordResetConsume(context);
                return;
            }

            if (context.Request.HttpMethod == "POST" && (path == "/api/register" || path == "/register"))
            {
                this.HandleRegister(context, path == "/api/register");
                return;
            }

            if (context.Request.HttpMethod == "POST" && (path == "/api/login" || path == "/login"))
            {
                this.HandleLogin(context, path == "/api/login");
                return;
            }

            if (context.Request.HttpMethod == "POST" && (path == "/api/logout" || path == "/logout"))
            {
                this.HandleLogout(context, path == "/api/logout");
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/forgot-password")
            {
                this.HandlePasswordResetRequest(context);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/reset-password")
            {
                this.HandlePasswordReset(context);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/account/password")
            {
                this.HandlePasswordChange(context);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/api/email/verification/resend")
            {
                this.HandleEmailVerificationResend(context);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/api/email/verification/verify")
            {
                this.HandleEmailVerificationVerify(context);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/api/forum/sso/issue")
            {
                this.HandleForumSsoIssue(context);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/api/forum/sso/redeem")
            {
                this.HandleForumSsoRedeem(context);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/api/forum/mapping/confirm")
            {
                this.HandleForumMappingConfirm(context);
                return;
            }

            WriteJson(context.Response, 404, "{\"ok\":false,\"error\":\"NOT_FOUND\"}");
        }

        private void HandleRegister(HttpListenerContext context, bool api)
        {
            Dictionary<string, string> form = this.ReadForm(context);
            if (!this.ValidateCsrf(context, form))
            {
                this.WriteFailure(context, api, 403, "CSRF_INVALID", "The submitted form expired.");
                return;
            }

            string remote = GetRemoteAddress(context);
            if (!this.registrationLimiter.Allow("register:" + remote))
            {
                this.WriteFailure(context, api, 429, "RATE_LIMITED", "Too many registration attempts. Try again later.");
                return;
            }

            string username = GetForm(form, "username");
            string password = GetForm(form, "password");
            string email = GetForm(form, "email");
            string idempotencyKey = GetForm(form, "idempotencyKey");
            string validationError = ValidateRegistrationInput(username, password, email, idempotencyKey);
            if (validationError != null)
            {
                this.WriteFailure(context, api, 400, validationError, "Registration information is invalid.");
                return;
            }

            try
            {
                AccountProvisioningResult result = this.broker.CreateGameAccount(
                    new CreateAccountRequest
                    {
                        Username = username,
                        Password = password,
                        Email = email,
                        IdempotencyKey = idempotencyKey,
                        FirstName = string.Empty,
                        LastName = string.Empty
                    });
                EmailDeliveryResult emailResult = this.SendVerificationEmail(result.IdentityPublicId);
                if (api)
                {
                    WriteJson(
                        context.Response,
                        201,
                        "{\"ok\":true,\"username\":\"" + Json(result.CanonicalUsername)
                        + "\",\"identityStatus\":\"" + Json(result.ProvisioningState)
                        + "\",\"gameAccountLinked\":true"
                        + ",\"emailVerificationSent\":" + (emailResult.Sent ? "true" : "false")
                        + ",\"emailVerificationStatus\":\"" + Json(emailResult.Status) + "\"}");
                }
                else
                {
                    Redirect(context.Response, emailResult.Sent ? "/login?registered=1&verifyEmailSent=1" : "/login?registered=1");
                }
            }
            catch (AccountBrokerException exception)
            {
                int status = exception.Code == "USERNAME_EXISTS" || exception.Code == "EMAIL_EXISTS" ? 409 : 400;
                this.WriteFailure(context, api, status, exception.Code, "Registration could not be completed.");
            }
            catch (MySqlException exception)
            {
                if (exception.Number == 1062)
                {
                    this.WriteFailure(context, api, 409, "REGISTRATION_CONFLICT", "Registration could not be completed.");
                    return;
                }

                throw;
            }
        }

        private void HandleLogin(HttpListenerContext context, bool api)
        {
            Dictionary<string, string> form = this.ReadForm(context);
            if (!this.ValidateCsrf(context, form))
            {
                this.WriteFailure(context, api, 403, "CSRF_INVALID", "The submitted form expired.");
                return;
            }

            string username = GetForm(form, "username");
            string remote = GetRemoteAddress(context);
            if (!this.loginLimiter.Allow("login:" + remote + ":" + (username ?? string.Empty).ToLowerInvariant()))
            {
                this.WriteFailure(context, api, 429, "RATE_LIMITED", "Too many login attempts. Try again later.");
                return;
            }

            WebsiteAuthenticationResult result =
                this.broker.AuthenticateWebsiteIdentity(username, GetForm(form, "password"));
            if (!result.IsAuthenticated)
            {
                this.WriteFailure(context, api, 401, result.FailureCode ?? "INVALID_CREDENTIALS", "Login failed.");
                return;
            }

            WebSession session = this.sessions.Create(result.Identity);
            SetCookie(context.Response, SessionCookieName, session.Token, context.Request.IsSecureConnection, true);
            if (api)
            {
                WriteJson(context.Response, 200, "{\"ok\":true,\"username\":\"" + Json(result.Identity.CanonicalUsername) + "\"}");
            }
            else
            {
                Redirect(context.Response, "/member");
            }
        }

        private void HandleLogout(HttpListenerContext context, bool api)
        {
            Dictionary<string, string> form = this.ReadForm(context);
            if (!this.ValidateCsrf(context, form))
            {
                this.WriteFailure(context, api, 403, "CSRF_INVALID", "The submitted form expired.");
                return;
            }

            string token = GetCookie(context.Request, SessionCookieName);
            if (!string.IsNullOrEmpty(token))
            {
                this.sessions.Invalidate(token);
            }

            ExpireCookie(context.Response, SessionCookieName, context.Request.IsSecureConnection);
            if (api)
            {
                WriteJson(context.Response, 200, "{\"ok\":true}");
            }
            else
            {
                Redirect(context.Response, "/login?loggedOut=1");
            }
        }

        private void WriteSession(HttpListenerContext context)
        {
            WebSession session = this.GetCurrentSession(context);
            if (session == null)
            {
                WriteJson(context.Response, 401, "{\"ok\":false,\"error\":\"NOT_AUTHENTICATED\"}");
                return;
            }

            WriteJson(context.Response, 200, "{\"ok\":true,\"identity\":" + IdentityJson(session.Identity) + "}");
        }

        private void HandleAccountIdentity(HttpListenerContext context)
        {
            if (!this.ValidateAccountMailSecret(context))
            {
                WriteJson(context.Response, 403, "{\"ok\":false,\"error\":\"ACCOUNT_MAIL_FORBIDDEN\"}");
                return;
            }

            Dictionary<string, string> form = this.ReadForm(context);
            try
            {
                AccountIdentitySnapshot identity = this.broker.GetIdentityByPublicId(GetForm(form, "identityPublicId"));
                WriteJson(context.Response, 200, "{\"ok\":true,\"identity\":" + IdentityJson(identity) + "}");
            }
            catch (AccountBrokerException exception)
            {
                WriteJson(context.Response, 400, "{\"ok\":false,\"error\":\"" + Json(exception.Code) + "\"}");
            }
        }

        private void HandleAccountCharacters(HttpListenerContext context)
        {
            if (!this.ValidateAccountMailSecret(context))
            {
                WriteJson(context.Response, 403, "{\"ok\":false,\"error\":\"ACCOUNT_MAIL_FORBIDDEN\"}");
                return;
            }

            Dictionary<string, string> form = this.ReadForm(context);
            try
            {
                AccountCharacterSnapshot[] characters = this.broker.GetCharactersByIdentityPublicId(GetForm(form, "identityPublicId"));
                WriteJson(context.Response, 200, "{\"ok\":true,\"characters\":" + CharactersJson(characters) + "}");
            }
            catch (AccountBrokerException exception)
            {
                WriteJson(context.Response, 400, "{\"ok\":false,\"error\":\"" + Json(exception.Code) + "\"}");
            }
        }

        private void HandlePasswordChange(HttpListenerContext context)
        {
            WebSession session = this.GetCurrentSession(context);
            if (session == null)
            {
                Redirect(context.Response, "/login");
                return;
            }

            Dictionary<string, string> form = this.ReadForm(context);
            if (!this.ValidateCsrf(context, form))
            {
                this.WritePasswordPage(context, "The submitted form expired.", 403);
                return;
            }

            string newPassword = GetForm(form, "newPassword");
            if (newPassword != GetForm(form, "confirmPassword"))
            {
                this.WritePasswordPage(context, "The new passwords do not match.");
                return;
            }

            if (!PasswordPolicy.IsValid(newPassword))
            {
                this.WritePasswordPage(context, "The new password must be between 8 and 128 characters.");
                return;
            }

            PasswordChangeResult result = this.broker.ChangePassword(
                session.Identity.IdentityPublicId,
                GetForm(form, "currentPassword"),
                newPassword);
            if (!result.Changed)
            {
                this.WritePasswordPage(context, "The current password is incorrect.");
                return;
            }

            this.sessions.InvalidateIdentity(result.IdentityPublicId);
            ExpireCookie(context.Response, SessionCookieName, context.Request.IsSecureConnection);
            Redirect(context.Response, "/login?passwordChanged=1");
        }

        private void HandleInternalPasswordChange(HttpListenerContext context)
        {
            if (!this.ValidateAccountMailSecret(context))
            {
                WriteJson(context.Response, 403, "{\"ok\":false,\"error\":\"ACCOUNT_MAIL_FORBIDDEN\"}");
                return;
            }

            Dictionary<string, string> form = this.ReadForm(context);
            string newPassword = GetForm(form, "newPassword");
            if (newPassword != GetForm(form, "confirmPassword"))
            {
                WriteJson(context.Response, 400, "{\"ok\":false,\"error\":\"PASSWORD_CONFIRMATION_MISMATCH\"}");
                return;
            }

            if (!PasswordPolicy.IsValid(newPassword))
            {
                WriteJson(context.Response, 400, "{\"ok\":false,\"error\":\"INVALID_PASSWORD\"}");
                return;
            }

            try
            {
                PasswordChangeResult result = this.broker.ChangePassword(
                    GetForm(form, "identityPublicId"),
                    GetForm(form, "currentPassword"),
                    newPassword);
                if (!result.Changed)
                {
                    WriteJson(context.Response, 400, "{\"ok\":false,\"error\":\"INVALID_CURRENT_PASSWORD\"}");
                    return;
                }

                this.sessions.InvalidateIdentity(result.IdentityPublicId);
                WriteJson(context.Response, 200, "{\"ok\":true,\"passwordChanged\":true,\"invalidateSessions\":true}");
            }
            catch (AccountBrokerException)
            {
                WriteJson(context.Response, 400, "{\"ok\":false,\"error\":\"PASSWORD_CHANGE_FAILED\"}");
            }
        }

        private void HandlePasswordResetRequest(HttpListenerContext context)
        {
            Dictionary<string, string> form = this.ReadForm(context);
            if (!this.ValidateCsrf(context, form))
            {
                this.WriteForgotPasswordPage(context, "The submitted form expired.", false, 403);
                return;
            }

            this.QueuePasswordReset(context, GetForm(form, "email"));
            this.WriteForgotPasswordPage(context, null, true);
        }

        private void HandleInternalPasswordResetRequest(HttpListenerContext context)
        {
            if (!this.ValidateAccountMailSecret(context))
            {
                WriteJson(context.Response, 403, "{\"ok\":false,\"error\":\"ACCOUNT_MAIL_FORBIDDEN\"}");
                return;
            }

            Dictionary<string, string> form = this.ReadForm(context);
            this.QueuePasswordReset(context, GetForm(form, "email"));
            WriteJson(context.Response, 200, "{\"ok\":true,\"message\":\"If an eligible account exists for that email, a password reset message has been sent.\"}");
        }

        private void HandleInternalPasswordResetStatus(HttpListenerContext context)
        {
            if (!this.ValidateAccountMailSecret(context))
            {
                WriteJson(context.Response, 403, "{\"ok\":false,\"error\":\"ACCOUNT_MAIL_FORBIDDEN\"}");
                return;
            }

            PasswordResetTokenStatus status = this.broker.GetPasswordResetTokenStatus(
                GetForm(this.ReadForm(context), "token"));
            WriteJson(
                context.Response,
                200,
                "{\"ok\":true,\"valid\":" + (status.Valid ? "true" : "false") + "}");
        }

        private void HandlePasswordReset(HttpListenerContext context)
        {
            Dictionary<string, string> form = this.ReadForm(context);
            string token = GetForm(form, "token");
            if (!this.ValidateCsrf(context, form))
            {
                this.WriteResetPasswordPage(context, token, "The submitted form expired.", 403);
                return;
            }

            string newPassword = GetForm(form, "newPassword");
            if (newPassword != GetForm(form, "confirmPassword"))
            {
                this.WriteResetPasswordPage(context, token, "The new passwords do not match.");
                return;
            }

            if (!PasswordPolicy.IsValid(newPassword))
            {
                this.WriteResetPasswordPage(context, token, "The new password must be between 8 and 128 characters.");
                return;
            }

            PasswordResetResult result = this.broker.ResetPassword(token, newPassword);
            if (!result.Reset)
            {
                this.WriteResetPasswordPage(context, token, "This password reset link is invalid or expired.");
                return;
            }

            this.sessions.InvalidateIdentity(result.IdentityPublicId);
            Redirect(context.Response, "/login?passwordReset=1");
        }

        private void HandleInternalPasswordResetConsume(HttpListenerContext context)
        {
            if (!this.ValidateAccountMailSecret(context))
            {
                WriteJson(context.Response, 403, "{\"ok\":false,\"error\":\"ACCOUNT_MAIL_FORBIDDEN\"}");
                return;
            }

            Dictionary<string, string> form = this.ReadForm(context);
            string newPassword = GetForm(form, "newPassword");
            if (newPassword != GetForm(form, "confirmPassword"))
            {
                WriteJson(context.Response, 400, "{\"ok\":false,\"error\":\"PASSWORD_CONFIRMATION_MISMATCH\"}");
                return;
            }

            if (!PasswordPolicy.IsValid(newPassword))
            {
                WriteJson(context.Response, 400, "{\"ok\":false,\"error\":\"INVALID_PASSWORD\"}");
                return;
            }

            PasswordResetResult result = this.broker.ResetPassword(GetForm(form, "token"), newPassword);
            if (!result.Reset)
            {
                WriteJson(context.Response, 400, "{\"ok\":false,\"error\":\"PASSWORD_RESET_INVALID\"}");
                return;
            }

            this.sessions.InvalidateIdentity(result.IdentityPublicId);
            WriteJson(context.Response, 200, "{\"ok\":true,\"passwordReset\":true,\"invalidateSessions\":true}");
        }

        private void QueuePasswordReset(HttpListenerContext context, string email)
        {
            string targetKey = PasswordResetLimiterKey(email);
            bool sourceAllowed = this.passwordResetIpLimiter.Allow(
                "password-reset-source:" + GetRemoteAddress(context));
            bool targetAllowed = this.passwordResetTargetLimiter.Allow(
                "password-reset-target:" + targetKey);
            if (!sourceAllowed || !targetAllowed || !this.emailSender.IsConfigured)
            {
                return;
            }

            PasswordResetTokenResult reset = this.broker.CreatePasswordResetToken(
                email,
                this.passwordResetTtlMinutes);
            if (reset == null)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    try
                    {
                        this.emailSender.SendPasswordReset(reset, this.publicBaseUrl);
                    }
                    catch (Exception exception)
                    {
                        try
                        {
                            this.broker.CancelPasswordResetToken(reset.Token);
                        }
                        catch (Exception cancellationException)
                        {
                            Console.Error.WriteLine(
                                "AORebirth Account Broker password reset cancellation failed: "
                                + cancellationException.GetType().FullName);
                        }

                        Console.Error.WriteLine(
                            "AORebirth Account Broker password reset send failed: "
                            + exception.GetType().FullName);
                    }
                });
        }

        private void HandleEmailVerificationResend(HttpListenerContext context)
        {
            if (!this.ValidateAccountMailSecret(context))
            {
                WriteJson(context.Response, 403, "{\"ok\":false,\"error\":\"ACCOUNT_MAIL_FORBIDDEN\"}");
                return;
            }

            Dictionary<string, string> form = this.ReadForm(context);
            string identityPublicId = GetForm(form, "identityPublicId");
            string limiterKey = "email-verification:" + GetRemoteAddress(context) + ":" + (identityPublicId ?? string.Empty);
            if (!this.emailVerificationLimiter.Allow(limiterKey))
            {
                WriteJson(context.Response, 429, "{\"ok\":false,\"error\":\"RATE_LIMITED\"}");
                return;
            }

            EmailDeliveryResult result = this.SendVerificationEmail(identityPublicId);
            if (!result.Sent)
            {
                int status = result.Status == "MAIL_NOT_CONFIGURED" ? 503 : 400;
                WriteJson(context.Response, status, "{\"ok\":false,\"error\":\"" + Json(result.Status) + "\"}");
                return;
            }

            WriteJson(context.Response, 200, "{\"ok\":true,\"emailVerificationSent\":true}");
        }

        private void HandleEmailVerificationVerify(HttpListenerContext context)
        {
            if (!this.ValidateAccountMailSecret(context))
            {
                WriteJson(context.Response, 403, "{\"ok\":false,\"error\":\"ACCOUNT_MAIL_FORBIDDEN\"}");
                return;
            }

            Dictionary<string, string> form = this.ReadForm(context);
            EmailVerificationResult result = this.broker.VerifyEmailToken(GetForm(form, "token"));
            WriteJson(
                context.Response,
                result.Status == "Verified" || result.Status == "AlreadyVerified" ? 200 : 400,
                "{\"ok\":" + (result.Status == "Verified" || result.Status == "AlreadyVerified" ? "true" : "false")
                + ",\"verified\":" + (result.Verified ? "true" : "false")
                + ",\"status\":\"" + Json(result.Status)
                + "\",\"username\":\"" + Json(result.CanonicalUsername)
                + "\",\"email\":\"" + Json(result.CanonicalEmail) + "\"}");
        }

        private EmailDeliveryResult SendVerificationEmail(string identityPublicId)
        {
            if (!this.emailSender.IsConfigured)
            {
                return new EmailDeliveryResult { Sent = false, Status = "MAIL_NOT_CONFIGURED" };
            }

            EmailVerificationTokenResult token = null;
            try
            {
                token = this.broker.CreateEmailVerificationToken(identityPublicId, this.emailVerificationTtlMinutes);
                this.emailSender.SendVerification(token, this.publicBaseUrl);
                return new EmailDeliveryResult { Sent = true, Status = "SENT" };
            }
            catch (AccountBrokerException exception)
            {
                return new EmailDeliveryResult { Sent = false, Status = exception.Code };
            }
            catch (Exception exception)
            {
                if (token != null)
                {
                    this.broker.CancelEmailVerificationToken(token.Token);
                }

                Console.Error.WriteLine(
                    "AORebirth Account Broker email verification send failed: "
                    + exception.GetType().FullName
                    + ": "
                    + exception.Message);

                return new EmailDeliveryResult { Sent = false, Status = "MAIL_SEND_FAILED" };
            }
        }

        private void HandleForumSsoIssue(HttpListenerContext context)
        {
            if (!this.ValidateForumSsoSecret(context))
            {
                WriteJson(context.Response, 403, "{\"ok\":false,\"error\":\"SSO_FORBIDDEN\"}");
                return;
            }

            Dictionary<string, string> form = this.ReadForm(context);
            string identityPublicId = GetForm(form, "identityPublicId");
            string returnTo = GetForm(form, "returnTo");
            try
            {
                ForumSsoIdentity identity = this.broker.GetForumSsoIdentityByPublicId(identityPublicId);
                if (!string.Equals(identity.IdentityStatus, "Active", StringComparison.Ordinal))
                {
                    WriteJson(context.Response, 403, "{\"ok\":false,\"error\":\"IDENTITY_NOT_ACTIVE\"}");
                    return;
                }

                string code = this.forumSsoCodes.Issue(identity.IdentityPublicId, returnTo);
                WriteJson(
                    context.Response,
                    200,
                    "{\"ok\":true,\"code\":\"" + Json(code)
                    + "\",\"expiresInSeconds\":" + this.forumSsoCodes.TtlSeconds.ToString()
                    + "}");
            }
            catch (AccountBrokerException exception)
            {
                WriteJson(context.Response, 400, "{\"ok\":false,\"error\":\"" + Json(exception.Code) + "\"}");
            }
        }

        private void HandleForumSsoRedeem(HttpListenerContext context)
        {
            if (!this.ValidateForumSsoSecret(context))
            {
                WriteJson(context.Response, 403, "{\"ok\":false,\"error\":\"SSO_FORBIDDEN\"}");
                return;
            }

            Dictionary<string, string> form = this.ReadForm(context);
            ForumSsoCode code = this.forumSsoCodes.Consume(GetForm(form, "code"));
            if (code == null)
            {
                WriteJson(context.Response, 400, "{\"ok\":false,\"error\":\"SSO_CODE_INVALID\"}");
                return;
            }

            try
            {
                ForumSsoIdentity identity = this.broker.GetForumSsoIdentityByPublicId(code.IdentityPublicId);
                if (!string.Equals(identity.IdentityStatus, "Active", StringComparison.Ordinal))
                {
                    WriteJson(context.Response, 403, "{\"ok\":false,\"error\":\"IDENTITY_NOT_ACTIVE\"}");
                    return;
                }

                WriteJson(
                    context.Response,
                    200,
                    "{\"ok\":true,\"identity\":" + ForumSsoIdentityJson(identity)
                    + ",\"returnTo\":\"" + Json(code.ReturnTo) + "\"}");
            }
            catch (AccountBrokerException exception)
            {
                WriteJson(context.Response, 400, "{\"ok\":false,\"error\":\"" + Json(exception.Code) + "\"}");
            }
        }

        private void HandleForumMappingConfirm(HttpListenerContext context)
        {
            if (!this.ValidateForumSsoSecret(context))
            {
                WriteJson(context.Response, 403, "{\"ok\":false,\"error\":\"SSO_FORBIDDEN\"}");
                return;
            }

            Dictionary<string, string> form = this.ReadForm(context);
            try
            {
                ExternalMappingResult mapping = this.broker.ConfirmForumExternalMapping(
                    GetForm(form, "identityPublicId"),
                    GetForm(form, "mybbUid"));
                WriteJson(
                    context.Response,
                    200,
                    "{\"ok\":true,\"provider\":\"" + Json(mapping.Provider)
                    + "\",\"mybbUid\":\"" + Json(mapping.ExternalAccountId)
                    + "\",\"mappingState\":\"" + Json(mapping.MappingState) + "\"}");
            }
            catch (AccountBrokerException exception)
            {
                WriteJson(context.Response, 400, "{\"ok\":false,\"error\":\"" + Json(exception.Code) + "\"}");
            }
        }

        private void WriteRegisterPage(HttpListenerContext context, string error)
        {
            string csrf = this.IssueCsrf(context);
            string idempotencyKey = NewToken();
            string body =
                PageHeader("Register")
                + Alert(error)
                + "<h1>Create AORebirth Account</h1>"
                + "<form method=\"post\" action=\"/register\">"
                + Hidden("csrf", csrf)
                + Hidden("idempotencyKey", idempotencyKey)
                + Label("Username", "username", "text")
                + Label("Email", "email", "email")
                + Label("Password", "password", "password")
                + "<button type=\"submit\">Register</button>"
                + "</form><p><a href=\"/login\">Already have an account?</a></p>"
                + PageFooter();
            WriteHtml(context.Response, 200, body);
        }

        private void WriteLoginPage(HttpListenerContext context, string error)
        {
            string csrf = this.IssueCsrf(context);
            string body =
                PageHeader("Login")
                + Alert(error)
                + "<h1>AORebirth Login</h1>"
                + "<form method=\"post\" action=\"/login\">"
                + Hidden("csrf", csrf)
                + Label("Username", "username", "text")
                + Label("Password", "password", "password")
                + "<button type=\"submit\">Login</button>"
                + "</form><p><a href=\"/forgot-password\">Forgot password?</a></p>"
                + "<p><a href=\"/register\">Create an account</a></p>"
                + PageFooter();
            WriteHtml(context.Response, 200, body);
        }

        private void WriteForgotPasswordPage(
            HttpListenerContext context,
            string error,
            bool submitted,
            int statusCode = 200)
        {
            string csrf = this.IssueCsrf(context);
            string body =
                PageHeader("Forgot password")
                + Alert(error)
                + "<h1>Forgot password</h1>"
                + (submitted
                    ? "<p>If an eligible account exists for that email, a password reset message has been sent.</p>"
                    : "<form method=\"post\" action=\"/forgot-password\">"
                        + Hidden("csrf", csrf)
                        + Label("Email", "email", "email")
                        + "<button type=\"submit\">Send reset link</button></form>")
                + "<p><a href=\"/login\">Back to login</a></p>"
                + PageFooter();
            WriteHtml(context.Response, statusCode, body);
        }

        private void WriteResetPasswordPage(
            HttpListenerContext context,
            string token,
            string error,
            int statusCode = 200)
        {
            PasswordResetTokenStatus status = this.broker.GetPasswordResetTokenStatus(token);
            string body = PageHeader("Reset password") + Alert(error) + "<h1>Reset password</h1>";
            if (!status.Valid)
            {
                body += "<p>This password reset link is invalid or expired.</p>"
                    + "<p><a href=\"/forgot-password\">Request another reset link</a></p>";
            }
            else
            {
                string csrf = this.IssueCsrf(context);
                body += "<form method=\"post\" action=\"/reset-password\">"
                    + Hidden("csrf", csrf)
                    + Hidden("token", token)
                    + Label("New password", "newPassword", "password")
                    + Label("Confirm new password", "confirmPassword", "password")
                    + "<button type=\"submit\">Reset password</button></form>";
            }

            WriteHtml(context.Response, statusCode, body + PageFooter());
        }

        private void WritePasswordPage(HttpListenerContext context, string error, int statusCode = 200)
        {
            WebSession session = this.GetCurrentSession(context);
            if (session == null)
            {
                Redirect(context.Response, "/login");
                return;
            }

            string csrf = this.IssueCsrf(context);
            string body =
                PageHeader("Change password")
                + Alert(error)
                + "<h1>Change password</h1>"
                + "<form method=\"post\" action=\"/account/password\">"
                + Hidden("csrf", csrf)
                + Label("Current password", "currentPassword", "password")
                + Label("New password", "newPassword", "password")
                + Label("Confirm new password", "confirmPassword", "password")
                + "<button type=\"submit\">Change password</button></form>"
                + "<p><a href=\"/member\">Back to account</a></p>"
                + PageFooter();
            WriteHtml(context.Response, statusCode, body);
        }

        private void WriteMemberPage(HttpListenerContext context)
        {
            WebSession session = this.GetCurrentSession(context);
            if (session == null)
            {
                Redirect(context.Response, "/login");
                return;
            }

            string csrf = this.IssueCsrf(context);
            AccountIdentitySnapshot identity = session.Identity;
            string body =
                PageHeader("Member")
                + "<h1>Member Account</h1>"
                + "<dl>"
                + "<dt>Username</dt><dd>" + Html(identity.CanonicalUsername) + "</dd>"
                + "<dt>Email status</dt><dd>" + (identity.EmailVerified ? "Verified" : "Unverified") + "</dd>"
                + "<dt>Identity status</dt><dd>" + Html(identity.IdentityStatus) + "</dd>"
                + "<dt>Game account linkage</dt><dd>" + Html(identity.GameMappingState) + "</dd>"
                + "</dl>"
                + "<p><a href=\"/account/password\">Change password</a></p>"
                + "<form method=\"post\" action=\"/logout\">" + Hidden("csrf", csrf)
                + "<button type=\"submit\">Logout</button></form>"
                + PageFooter();
            WriteHtml(context.Response, 200, body);
        }

        private WebSession GetCurrentSession(HttpListenerContext context)
        {
            string token = GetCookie(context.Request, SessionCookieName);
            return string.IsNullOrEmpty(token) ? null : this.sessions.Get(token);
        }

        private string IssueCsrf(HttpListenerContext context)
        {
            string token = this.csrfTokens.Create();
            SetCookie(context.Response, CsrfCookieName, token, context.Request.IsSecureConnection, true);
            return token;
        }

        private bool ValidateCsrf(HttpListenerContext context, Dictionary<string, string> form)
        {
            string cookie = GetCookie(context.Request, CsrfCookieName);
            string submitted = GetForm(form, "csrf");
            return !string.IsNullOrEmpty(cookie)
                && string.Equals(cookie, submitted, StringComparison.Ordinal)
                && this.csrfTokens.Validate(submitted);
        }

        private Dictionary<string, string> ReadForm(HttpListenerContext context)
        {
            if (context.Request.ContentLength64 > MaxRequestBytes)
            {
                throw new InvalidOperationException("Request too large.");
            }

            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8))
            {
                string body = reader.ReadToEnd();
                var result = new Dictionary<string, string>(StringComparer.Ordinal);
                string[] pairs = body.Split('&');
                foreach (string pair in pairs)
                {
                    if (pair.Length == 0)
                    {
                        continue;
                    }

                    string[] parts = pair.Split(new[] { '=' }, 2);
                    string name = WebUtility.UrlDecode(parts[0] ?? string.Empty);
                    string value = parts.Length > 1 ? WebUtility.UrlDecode(parts[1]) : string.Empty;
                    if (!string.IsNullOrEmpty(name))
                    {
                        result[name] = value;
                    }
                }

                return result;
            }
        }

        private void WriteFailure(HttpListenerContext context, bool api, int status, string code, string message)
        {
            if (api)
            {
                WriteJson(context.Response, status, "{\"ok\":false,\"error\":\"" + Json(code) + "\"}");
            }
            else
            {
                if (context.Request.Url.AbsolutePath.IndexOf("register", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    this.WriteRegisterPage(context, message);
                }
                else
                {
                    this.WriteLoginPage(context, message);
                }
            }
        }

        private static string ValidateRegistrationInput(string username, string password, string email, string idempotencyKey)
        {
            try
            {
                UsernamePolicy.NormalizeForNewRegistration(username);
            }
            catch (AccountBrokerException)
            {
                return "INVALID_USERNAME";
            }

            if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Length > 128)
            {
                return "INVALID_PASSWORD";
            }

            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
            {
                return "INVALID_IDEMPOTENCY_KEY";
            }

            try
            {
                var address = new MailAddress(email ?? string.Empty);
                if (!string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
                {
                    return "INVALID_EMAIL";
                }
            }
            catch (FormatException)
            {
                return "INVALID_EMAIL";
            }

            return null;
        }

        private static string IdentityJson(AccountIdentitySnapshot identity)
        {
            return "{\"username\":\"" + Json(identity.CanonicalUsername)
                + "\",\"email\":\"" + Json(identity.CanonicalEmail)
                + "\",\"emailVerified\":" + (identity.EmailVerified ? "true" : "false")
                + ",\"identityStatus\":\"" + Json(identity.IdentityStatus)
                + "\",\"gameAccountLinked\":" + (identity.GameMappingState == "Linked" ? "true" : "false")
                + ",\"createdAt\":\"" + Json(identity.CreatedAt.ToUniversalTime().ToString("o"))
                + "\",\"identityPublicId\":\"" + Json(identity.IdentityPublicId) + "\"}";
        }

        private static string CharactersJson(AccountCharacterSnapshot[] characters)
        {
            if (characters == null || characters.Length == 0)
            {
                return "[]";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("[");
            for (int index = 0; index < characters.Length; index++)
            {
                AccountCharacterSnapshot character = characters[index];
                if (index > 0)
                {
                    builder.Append(",");
                }

                builder.Append("{\"name\":\"").Append(Json(character.Name))
                    .Append("\",\"firstName\":\"").Append(Json(character.FirstName))
                    .Append("\",\"lastName\":\"").Append(Json(character.LastName))
                    .Append("\",\"playfield\":").Append(character.Playfield.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"playfieldName\":\"").Append(Json(character.PlayfieldName))
                    .Append("\",\"online\":").Append(character.Online ? "true" : "false")
                    .Append(",\"x\":").Append(character.X.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"y\":").Append(character.Y.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"z\":").Append(character.Z.ToString(CultureInfo.InvariantCulture))
                    .Append("}");
            }

            builder.Append("]");
            return builder.ToString();
        }

        private static string ForumSsoIdentityJson(ForumSsoIdentity identity)
        {
            return "{\"identityPublicId\":\"" + Json(identity.IdentityPublicId)
                + "\",\"username\":\"" + Json(identity.CanonicalUsername)
                + "\",\"email\":\"" + Json(identity.CanonicalEmail)
                + "\",\"emailVerified\":" + (identity.EmailVerified ? "true" : "false")
                + ",\"identityStatus\":\"" + Json(identity.IdentityStatus)
                + "\",\"existingMybbUid\":\"" + Json(identity.ExistingMybbUid) + "\"}";
        }

        private bool ValidateForumSsoSecret(HttpListenerContext context)
        {
            if (string.IsNullOrEmpty(this.forumSsoSecret))
            {
                return false;
            }

            string provided = context.Request.Headers["X-AORebirth-Forum-SSO-Secret"];
            return FixedTimeEquals(this.forumSsoSecret, provided);
        }

        private bool ValidateAccountMailSecret(HttpListenerContext context)
        {
            if (string.IsNullOrEmpty(this.accountMailSecret))
            {
                return false;
            }

            string provided = context.Request.Headers["X-AORebirth-Account-Mail-Secret"];
            return FixedTimeEquals(this.accountMailSecret, provided);
        }

        private static bool FixedTimeEquals(string expected, string provided)
        {
            if (expected == null || provided == null)
            {
                return false;
            }

            byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
            byte[] providedBytes = Encoding.UTF8.GetBytes(provided);
            int diff = expectedBytes.Length ^ providedBytes.Length;
            int length = Math.Min(expectedBytes.Length, providedBytes.Length);
            for (int index = 0; index < length; index++)
            {
                diff |= expectedBytes[index] ^ providedBytes[index];
            }

            return diff == 0;
        }

        private static string GetForm(Dictionary<string, string> form, string name)
        {
            string value;
            return form.TryGetValue(name, out value) ? value : null;
        }

        private static string GetCookie(HttpListenerRequest request, string name)
        {
            Cookie cookie = request.Cookies[name];
            return cookie == null ? null : cookie.Value;
        }

        private static void SetCookie(HttpListenerResponse response, string name, string value, bool secure, bool httpOnly)
        {
            string cookie = name + "=" + value + "; Path=/; SameSite=Lax";
            if (httpOnly)
            {
                cookie += "; HttpOnly";
            }

            if (secure)
            {
                cookie += "; Secure";
            }

            response.Headers.Add("Set-Cookie", cookie);
        }

        private static void ExpireCookie(HttpListenerResponse response, string name, bool secure)
        {
            string cookie = name + "=; Path=/; Expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax; HttpOnly";
            if (secure)
            {
                cookie += "; Secure";
            }

            response.Headers.Add("Set-Cookie", cookie);
        }

        private static void Redirect(HttpListenerResponse response, string location)
        {
            response.StatusCode = 302;
            response.Headers["Location"] = location;
            response.Close();
        }

        private static void WriteJson(HttpListenerResponse response, int status, string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            response.StatusCode = status;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }

        private static void WriteHtml(HttpListenerResponse response, int status, string html)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(html);
            response.StatusCode = status;
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }

        private static string PageHeader(string title)
        {
            return "<!doctype html><html><head><meta charset=\"utf-8\"><title>AORebirth "
                + Html(title)
                + "</title><style>body{font-family:Arial,sans-serif;background:#111;color:#eee;max-width:720px;margin:40px auto}input,button{display:block;margin:8px 0;padding:8px}label{display:block;margin-top:12px}.error{color:#ff8080}a{color:#9cf}dt{font-weight:bold}</style></head><body>";
        }

        private static string PageFooter()
        {
            return "</body></html>";
        }

        private static string Alert(string error)
        {
            return string.IsNullOrEmpty(error) ? string.Empty : "<p class=\"error\">" + Html(error) + "</p>";
        }

        private static string Label(string label, string name, string type)
        {
            return "<label>" + Html(label) + "<input name=\"" + Html(name) + "\" type=\"" + Html(type) + "\"></label>";
        }

        private static string Hidden(string name, string value)
        {
            return "<input type=\"hidden\" name=\"" + Html(name) + "\" value=\"" + Html(value) + "\">";
        }

        private static string Html(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static string Json(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string GetRemoteAddress(HttpListenerContext context)
        {
            string proxyHeader = context.Request.Headers["X-Forwarded-For"];
            if (IsTrustedProxyRemote(context.Request.RemoteEndPoint == null ? null : context.Request.RemoteEndPoint.Address)
                && !string.IsNullOrWhiteSpace(proxyHeader))
            {
                string candidate = proxyHeader.Split(',')[0].Trim();
                IPAddress address;
                if (IPAddress.TryParse(candidate, out address))
                {
                    return address.ToString();
                }
            }

            return context.Request.RemoteEndPoint == null
                ? "unknown"
                : context.Request.RemoteEndPoint.Address.ToString();
        }

        private static string PasswordResetLimiterKey(string email)
        {
            string normalized = string.IsNullOrWhiteSpace(email)
                ? string.Empty
                : email.Trim().ToLowerInvariant();
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                var builder = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest)
                {
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static bool IsTrustedProxyRemote(IPAddress remoteAddress)
        {
            string configured = Environment.GetEnvironmentVariable("AOREBIRTH_ACCOUNT_BROKER_TRUSTED_PROXY_CIDRS");
            if (remoteAddress == null || string.IsNullOrWhiteSpace(configured))
            {
                return false;
            }

            string[] ranges = configured.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string range in ranges)
            {
                if (IsInCidr(remoteAddress, range.Trim()))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInCidr(IPAddress address, string cidr)
        {
            string[] parts = cidr.Split('/');
            if (parts.Length != 2)
            {
                return false;
            }

            IPAddress network;
            int prefixLength;
            if (!IPAddress.TryParse(parts[0], out network) || !int.TryParse(parts[1], out prefixLength))
            {
                return false;
            }

            byte[] addressBytes = address.GetAddressBytes();
            byte[] networkBytes = network.GetAddressBytes();
            if (addressBytes.Length != 4 || networkBytes.Length != 4 || prefixLength < 0 || prefixLength > 32)
            {
                return false;
            }

            uint addressValue = ToUInt32(addressBytes);
            uint networkValue = ToUInt32(networkBytes);
            uint mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
            return (addressValue & mask) == (networkValue & mask);
        }

        private static uint ToUInt32(byte[] bytes)
        {
            return ((uint)bytes[0] << 24)
                | ((uint)bytes[1] << 16)
                | ((uint)bytes[2] << 8)
                | bytes[3];
        }

        private static string EnsureTrailingSlash(string prefix)
        {
            return prefix.EndsWith("/", StringComparison.Ordinal) ? prefix : prefix + "/";
        }

        private static int GetIntEnvironment(string name, int defaultValue)
        {
            int value;
            return int.TryParse(Environment.GetEnvironmentVariable(name), out value) && value > 0 ? value : defaultValue;
        }

        private static string GetEnvironment(string name, string defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private static string GetSecretEnvironment(string directName, string fileName)
        {
            string filePath = Environment.GetEnvironmentVariable(fileName);
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                return File.ReadAllText(filePath).Trim();
            }

            return Environment.GetEnvironmentVariable(directName);
        }

        private static string NewToken()
        {
            byte[] bytes = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private sealed class EmailDeliveryResult
        {
            public bool Sent { get; set; }

            public string Status { get; set; }
        }

        private sealed class AccountEmailSender
        {
            private readonly string fromAddress;

            private readonly string fromName;

            private readonly string host;

            private readonly string password;

            private readonly int port;

            private readonly bool requireTls;

            private readonly string username;

            private AccountEmailSender(
                string host,
                int port,
                bool requireTls,
                string username,
                string password,
                string fromAddress,
                string fromName)
            {
                this.host = host;
                this.port = port;
                this.requireTls = requireTls;
                this.username = username;
                this.password = password;
                this.fromAddress = fromAddress;
                this.fromName = string.IsNullOrWhiteSpace(fromName) ? "AORebirth" : fromName;
            }

            public bool IsConfigured
            {
                get
                {
                    return !string.IsNullOrWhiteSpace(this.host)
                        && !string.IsNullOrWhiteSpace(this.username)
                        && !string.IsNullOrWhiteSpace(this.password)
                        && !string.IsNullOrWhiteSpace(this.fromAddress);
                }
            }

            public static AccountEmailSender FromEnvironment()
            {
                string tlsMode = GetEnvironment("AOREBIRTH_MAIL_SMTP_TLS", "StartTls");
                bool requireTls = !string.Equals(tlsMode, "None", StringComparison.OrdinalIgnoreCase);
                return new AccountEmailSender(
                    Environment.GetEnvironmentVariable("AOREBIRTH_MAIL_SMTP_HOST"),
                    GetIntEnvironment("AOREBIRTH_MAIL_SMTP_PORT", 587),
                    requireTls,
                    Environment.GetEnvironmentVariable("AOREBIRTH_MAIL_SMTP_USERNAME"),
                    GetSecretEnvironment("AOREBIRTH_MAIL_SMTP_PASSWORD", "AOREBIRTH_MAIL_SMTP_PASSWORD_FILE"),
                    Environment.GetEnvironmentVariable("AOREBIRTH_MAIL_FROM_ADDRESS"),
                    GetEnvironment("AOREBIRTH_MAIL_FROM_NAME", "AORebirth"));
            }

            public void SendVerification(EmailVerificationTokenResult verification, string publicBaseUrl)
            {
                if (!this.IsConfigured)
                {
                    throw new InvalidOperationException("Mail sender is not configured.");
                }

                string link = publicBaseUrl.TrimEnd('/') + "/verify-email.php#token=" + Uri.EscapeDataString(verification.Token);
                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(this.fromAddress, this.fromName, Encoding.UTF8);
                    message.To.Add(new MailAddress(verification.CanonicalEmail));
                    message.Subject = "Verify your AORebirth account email";
                    message.BodyEncoding = Encoding.UTF8;
                    message.SubjectEncoding = Encoding.UTF8;
                    message.Body =
                        "AORebirth account email verification" + Environment.NewLine
                        + Environment.NewLine
                        + "Account: " + verification.CanonicalUsername + Environment.NewLine
                        + Environment.NewLine
                        + "Open this link to verify your email address:" + Environment.NewLine
                        + link + Environment.NewLine
                        + Environment.NewLine
                        + "This verification link expires at "
                        + verification.ExpiresAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")
                        + " UTC." + Environment.NewLine
                        + Environment.NewLine
                        + "If you did not create this AORebirth account, ignore this message.";

                    using (var client = new SmtpClient(this.host, this.port))
                    {
                        client.DeliveryMethod = SmtpDeliveryMethod.Network;
                        client.EnableSsl = this.requireTls;
                        client.Credentials = new NetworkCredential(this.username, this.password);
                        client.Send(message);
                    }
                }
            }

            public void SendPasswordReset(PasswordResetTokenResult reset, string publicBaseUrl)
            {
                if (!this.IsConfigured)
                {
                    throw new InvalidOperationException("Mail sender is not configured.");
                }

                string link = publicBaseUrl.TrimEnd('/') + "/reset-password#token=" + Uri.EscapeDataString(reset.Token);
                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(this.fromAddress, this.fromName, Encoding.UTF8);
                    message.To.Add(new MailAddress(reset.CanonicalEmail));
                    message.Subject = "Reset your AORebirth account password";
                    message.BodyEncoding = Encoding.UTF8;
                    message.SubjectEncoding = Encoding.UTF8;
                    message.Body =
                        "AORebirth password reset" + Environment.NewLine
                        + Environment.NewLine
                        + "A password reset was requested for account: "
                        + reset.CanonicalUsername + Environment.NewLine
                        + Environment.NewLine
                        + "Open this link to choose a new password:" + Environment.NewLine
                        + link + Environment.NewLine
                        + Environment.NewLine
                        + "This password reset link expires at "
                        + reset.ExpiresAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")
                        + " UTC." + Environment.NewLine
                        + Environment.NewLine
                        + "If you did not request this password reset, ignore this message.";

                    using (var client = new SmtpClient(this.host, this.port))
                    {
                        client.DeliveryMethod = SmtpDeliveryMethod.Network;
                        client.EnableSsl = this.requireTls;
                        client.Credentials = new NetworkCredential(this.username, this.password);
                        client.Send(message);
                    }
                }
            }
        }

        private sealed class CsrfTokenStore
        {
            private readonly Dictionary<string, DateTime> tokens = new Dictionary<string, DateTime>(StringComparer.Ordinal);

            private readonly object sync = new object();

            public string Create()
            {
                string token = NewToken();
                lock (this.sync)
                {
                    this.tokens[token] = DateTime.UtcNow.AddMinutes(30);
                }

                return token;
            }

            public bool Validate(string token)
            {
                lock (this.sync)
                {
                    DateTime expires;
                    if (string.IsNullOrEmpty(token)
                        || !this.tokens.TryGetValue(token, out expires)
                        || expires < DateTime.UtcNow)
                    {
                        return false;
                    }

                    return true;
                }
            }
        }

        private sealed class FixedWindowRateLimiter
        {
            private readonly Dictionary<string, Counter> counters = new Dictionary<string, Counter>(StringComparer.Ordinal);

            private readonly int limit;

            private readonly object sync = new object();

            private readonly TimeSpan window;

            public FixedWindowRateLimiter(int limit, TimeSpan window)
            {
                this.limit = limit;
                this.window = window;
            }

            public bool Allow(string key)
            {
                lock (this.sync)
                {
                    DateTime now = DateTime.UtcNow;
                    Counter counter;
                    if (!this.counters.TryGetValue(key, out counter) || counter.WindowEnd <= now)
                    {
                        this.counters[key] = new Counter { Count = 1, WindowEnd = now.Add(this.window) };
                        return true;
                    }

                    if (counter.Count >= this.limit)
                    {
                        return false;
                    }

                    counter.Count++;
                    return true;
                }
            }

            private sealed class Counter
            {
                public int Count { get; set; }

                public DateTime WindowEnd { get; set; }
            }
        }

        private sealed class WebSessionStore
        {
            private readonly Dictionary<string, WebSession> sessions = new Dictionary<string, WebSession>(StringComparer.Ordinal);

            private readonly object sync = new object();

            private readonly TimeSpan ttl;

            public WebSessionStore(int ttlMinutes)
            {
                this.ttl = TimeSpan.FromMinutes(ttlMinutes);
            }

            public WebSession Create(AccountIdentitySnapshot identity)
            {
                var session = new WebSession
                {
                    Token = NewToken(),
                    Identity = identity,
                    ExpiresAt = DateTime.UtcNow.Add(this.ttl)
                };

                lock (this.sync)
                {
                    this.sessions[session.Token] = session;
                }

                return session;
            }

            public WebSession Get(string token)
            {
                lock (this.sync)
                {
                    WebSession session;
                    if (!this.sessions.TryGetValue(token, out session))
                    {
                        return null;
                    }

                    if (session.ExpiresAt < DateTime.UtcNow)
                    {
                        this.sessions.Remove(token);
                        return null;
                    }

                    return session;
                }
            }

            public void Invalidate(string token)
            {
                lock (this.sync)
                {
                    this.sessions.Remove(token);
                }
            }

            public void InvalidateIdentity(string identityPublicId)
            {
                if (string.IsNullOrEmpty(identityPublicId))
                {
                    return;
                }

                lock (this.sync)
                {
                    var expired = new List<string>();
                    foreach (KeyValuePair<string, WebSession> entry in this.sessions)
                    {
                        if (entry.Value.Identity != null
                            && string.Equals(
                                entry.Value.Identity.IdentityPublicId,
                                identityPublicId,
                                StringComparison.Ordinal))
                        {
                            expired.Add(entry.Key);
                        }
                    }

                    foreach (string token in expired)
                    {
                        this.sessions.Remove(token);
                    }
                }
            }
        }

        private sealed class WebSession
        {
            public string Token { get; set; }

            public AccountIdentitySnapshot Identity { get; set; }

            public DateTime ExpiresAt { get; set; }
        }

        private sealed class ForumSsoCodeStore
        {
            private readonly Dictionary<string, ForumSsoCode> codes = new Dictionary<string, ForumSsoCode>(StringComparer.Ordinal);

            private readonly object sync = new object();

            private readonly TimeSpan ttl;

            public ForumSsoCodeStore(int ttlSeconds)
            {
                this.TtlSeconds = ttlSeconds;
                this.ttl = TimeSpan.FromSeconds(ttlSeconds);
            }

            public int TtlSeconds { get; private set; }

            public string Issue(string identityPublicId, string returnTo)
            {
                string code = NewToken();
                lock (this.sync)
                {
                    this.codes[code] = new ForumSsoCode
                    {
                        Code = code,
                        IdentityPublicId = identityPublicId,
                        ReturnTo = returnTo ?? string.Empty,
                        ExpiresAt = DateTime.UtcNow.Add(this.ttl)
                    };
                }

                return code;
            }

            public ForumSsoCode Consume(string code)
            {
                lock (this.sync)
                {
                    ForumSsoCode stored;
                    if (string.IsNullOrEmpty(code) || !this.codes.TryGetValue(code, out stored))
                    {
                        return null;
                    }

                    this.codes.Remove(code);
                    return stored.ExpiresAt < DateTime.UtcNow ? null : stored;
                }
            }
        }

        private sealed class ForumSsoCode
        {
            public string Code { get; set; }

            public string IdentityPublicId { get; set; }

            public string ReturnTo { get; set; }

            public DateTime ExpiresAt { get; set; }
        }
    }
}
