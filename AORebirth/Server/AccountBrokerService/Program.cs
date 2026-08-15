namespace AORebirth.AccountBroker.Service
{
    using System;
    using System.Collections.Generic;
    using System.Data;
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

        private readonly WebSessionStore sessions;

        public AccountBrokerHttpHost(string prefix, AccountBrokerService broker)
        {
            this.broker = broker;
            this.listener.Prefixes.Add(EnsureTrailingSlash(prefix));
            this.sessions = new WebSessionStore(GetIntEnvironment("AOREBIRTH_ACCOUNT_BROKER_SESSION_MINUTES", 480));
            this.registrationLimiter = new FixedWindowRateLimiter(
                GetIntEnvironment("AOREBIRTH_ACCOUNT_BROKER_REGISTER_LIMIT", 5),
                TimeSpan.FromMinutes(10));
            this.loginLimiter = new FixedWindowRateLimiter(
                GetIntEnvironment("AOREBIRTH_ACCOUNT_BROKER_LOGIN_LIMIT", 5),
                TimeSpan.FromMinutes(5));
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
                if (api)
                {
                    WriteJson(
                        context.Response,
                        201,
                        "{\"ok\":true,\"username\":\"" + Json(result.CanonicalUsername)
                        + "\",\"identityStatus\":\"" + Json(result.ProvisioningState)
                        + "\",\"gameAccountLinked\":true}");
                }
                else
                {
                    Redirect(context.Response, "/login?registered=1");
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
                + "</form><p><a href=\"/register\">Create an account</a></p>"
                + PageFooter();
            WriteHtml(context.Response, 200, body);
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

        private static string NewToken()
        {
            byte[] bytes = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
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
        }

        private sealed class WebSession
        {
            public string Token { get; set; }

            public AccountIdentitySnapshot Identity { get; set; }

            public DateTime ExpiresAt { get; set; }
        }
    }
}
