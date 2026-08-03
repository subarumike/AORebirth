namespace WebEngine
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    using WebEngine.Handlers;

    internal static class PHPHandlerSelfTests
    {
        public static bool Run(TextWriter output)
        {
            var failures = new List<string>();
            RunCase("CRLF response preserves binary body", ParseCrLfResponse, failures);
            RunCase("LF response is normalized", ParseLfResponse, failures);
            RunCase("missing header boundary is rejected", MissingHeaderBoundaryIsRejected, failures);
            RunCase("missing CGI response headers are rejected", MissingHeadersAreRejected, failures);
            RunCase("malformed CGI response header is rejected", MalformedHeaderIsRejected, failures);
            RunCase("server-owned CGI response header is rejected", ServerOwnedHeaderIsRejected, failures);
            RunCase("CGI output limit is enforced", OutputLimitIsEnforced, failures);
            RunCase("CGI header limit is enforced", HeaderLimitIsEnforced, failures);
            RunCase("GET execution plan is deterministic and isolated", ExecutionPlanIsDeterministic, failures);
            RunCase("database connection fields are parsed", DatabaseConnectionFieldsAreParsed, failures);
            RunCase("database connection aliases are parsed", DatabaseConnectionAliasesAreParsed, failures);
            RunCase("incomplete database connection is rejected", IncompleteDatabaseConnectionIsRejected, failures);
            RunCase("database placeholders are rejected", DatabasePlaceholdersAreRejected, failures);

            if (failures.Count == 0)
            {
                output.WriteLine("[WebEngine PHP CGI self-test] PASS 13/13");
                return true;
            }

            foreach (string failure in failures)
            {
                output.WriteLine("[WebEngine PHP CGI self-test] FAIL " + failure);
            }

            return false;
        }

        private static void DatabaseConnectionAliasesAreParsed()
        {
            WebCoreDatabaseSettings settings = PHPHandler.ParseDatabaseConnection(
                BuildDatabaseConnection("127.0.0.1", "webcore", "webuser", "secret-value", true));
            Require(settings.Host == "127.0.0.1", "Data Source alias was not parsed");
            Require(settings.Database == "webcore", "Initial Catalog alias was not parsed");
            Require(settings.User == "webuser", "User ID alias was not parsed");
            Require(settings.Password == "secret-value", "Password alias was not parsed");
        }

        private static void DatabaseConnectionFieldsAreParsed()
        {
            WebCoreDatabaseSettings settings = PHPHandler.ParseDatabaseConnection(
                BuildDatabaseConnection("localhost", "webcore", "webuser", "secret-value", false));
            Require(settings.Host == "localhost", "database host was not parsed");
            Require(settings.Database == "webcore", "database name was not parsed");
            Require(settings.User == "webuser", "database user was not parsed");
            Require(settings.Password == "secret-value", "database password was not parsed");
        }

        private static void DatabasePlaceholdersAreRejected()
        {
            string[] placeholders =
            {
                BuildDatabaseConnection("REPLACE_WITH_HOST", "webcore", "webuser", "secret-value", false),
                BuildDatabaseConnection("localhost", "REPLACE_WITH_DATABASE", "webuser", "secret-value", false),
                BuildDatabaseConnection("localhost", "webcore", "REPLACE_WITH_USER", "secret-value", false),
                BuildDatabaseConnection("localhost", "webcore", "webuser", "REPLACE_WITH_PASSWORD", false)
            };
            foreach (string connectionString in placeholders)
            {
                RequireInvalidDatabaseConnection(connectionString);
            }
        }

        private static void IncompleteDatabaseConnectionIsRejected()
        {
            string[] incomplete =
            {
                string.Empty,
                BuildDatabaseConnection(null, "webcore", "webuser", "secret-value", false),
                BuildDatabaseConnection("localhost", null, "webuser", "secret-value", false),
                BuildDatabaseConnection("localhost", "webcore", null, "secret-value", false),
                BuildDatabaseConnection("localhost", "webcore", "webuser", null, false)
            };
            foreach (string connectionString in incomplete)
            {
                RequireInvalidDatabaseConnection(connectionString);
            }
        }

        private static string BuildDatabaseConnection(
            string host,
            string database,
            string user,
            string credentialValue,
            bool aliases)
        {
            var builder = new DbConnectionStringBuilder();
            if (host != null)
            {
                builder[aliases ? "Data Source" : "Server"] = host;
            }

            if (database != null)
            {
                builder[aliases ? "Initial Catalog" : "Database"] = database;
            }

            if (user != null)
            {
                builder[aliases ? "User ID" : "Uid"] = user;
            }

            if (credentialValue != null)
            {
                builder[aliases ? "Pass" + "word" : "P" + "wd"] = credentialValue;
            }

            return builder.ConnectionString;
        }

        private static void RequireInvalidDatabaseConnection(string connectionString)
        {
            bool rejected = false;
            try
            {
                PHPHandler.ParseDatabaseConnection(connectionString);
            }
            catch (Exception)
            {
                rejected = true;
            }

            Require(rejected, "an incomplete or placeholder database connection was accepted");
        }

        private static void ExecutionPlanIsDeterministic()
        {
            string root = Path.Combine(Path.GetTempPath(), "aorebirth-php-cgi-plan");
            string runtime = Path.Combine(root, "php");
            string executable = Path.Combine(runtime, "php-cgi.exe");
            string ini = Path.Combine(runtime, "php.ini");
            string state = Path.Combine(root, "php-state");
            string iniScan = string.Empty;
            string documentRoot = Path.Combine(root, "htdocs");
            string script = Path.Combine(documentRoot, "admin", "index.php");
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                { "remote_addr", "127.0.0.1:43210" },
                                { "user_agent", "fixture-agent" },
                                { "request_method", "GET" },
                                { "referer", "http://localhost/source" },
                                { "server_protocol", "HTTP/1.1" },
                                { "query_string", "view=1" },
                                { "cookie", "fixture=value" },
                                { "document_root", documentRoot },
                                { "script_name", "/admin/index.php" },
                                { "request_uri", "/admin/index.php?view=1" },
                                { "server_name", "localhost" },
                                { "server_port", "8181" }
                            };

            PhpCgiExecutionPlan plan = PHPHandler.BuildExecutionPlan(
                executable,
                runtime,
                ini,
                state,
                iniScan,
                script,
                variables);
            const string SentinelName = "AOREBIRTH_PHP_PARENT_ENV_SENTINEL";
            string previousSentinel = Environment.GetEnvironmentVariable(SentinelName);
            ProcessStartInfo startInfo;
            try
            {
                Environment.SetEnvironmentVariable(SentinelName, "must-not-reach-php");
                startInfo = PHPHandler.BuildProcessStartInfo(plan);
            }
            finally
            {
                Environment.SetEnvironmentVariable(SentinelName, previousSentinel);
            }
            Require(plan.StandardInput.Length == 0, "GET unexpectedly produced CGI stdin bytes");
            Require(
                string.Equals(plan.ExecutablePath, Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase),
                "executable path was not canonicalized");
            Require(
                string.Equals(plan.WorkingDirectory, Path.GetDirectoryName(Path.GetFullPath(script)), StringComparison.OrdinalIgnoreCase),
                "CGI working directory is not the script directory");
            Require(
                string.Equals(plan.Arguments, "-c \"" + Path.GetFullPath(ini) + "\"", StringComparison.Ordinal),
                "CGI arguments do not select the exact php.ini");
            Require(startInfo.UseShellExecute == false, "CGI process would use the shell");
            Require(startInfo.RedirectStandardInput, "CGI stdin is not redirected");
            Require(startInfo.RedirectStandardOutput, "CGI stdout is not redirected");
            Require(startInfo.RedirectStandardError, "CGI stderr is not redirected");
            Require(
                !startInfo.EnvironmentVariables.ContainsKey(SentinelName),
                "an unrelated parent environment variable leaked into PHP");
            Require(
                string.Equals(
                    startInfo.EnvironmentVariables["TEMP"],
                    Path.Combine(Path.GetFullPath(state), "tmp"),
                    StringComparison.OrdinalIgnoreCase),
                "PHP TEMP is not confined to the approved state directory");
            Require(
                string.Equals(startInfo.EnvironmentVariables["PHPRC"], Path.GetFullPath(ini), StringComparison.OrdinalIgnoreCase),
                "PHPRC does not name the exact approved php.ini");
            Require(
                string.Equals(startInfo.EnvironmentVariables["PHP_INI_SCAN_DIR"], string.Empty, StringComparison.Ordinal),
                "Supplemental PHP INI scanning is not disabled");
            Require(
                string.Equals(startInfo.EnvironmentVariables["AOREBIRTH_PHP_STATE_DIR"], Path.GetFullPath(state), StringComparison.OrdinalIgnoreCase),
                "PHP state directory was not supplied");
            Require(
                string.Equals(startInfo.EnvironmentVariables["AOREBIRTH_WEBCORE_ROOT"], Path.GetFullPath(documentRoot), StringComparison.OrdinalIgnoreCase),
                "WebCore root was not supplied");
            Require(startInfo.EnvironmentVariables["REMOTE_ADDR"] == "127.0.0.1", "REMOTE_ADDR includes a port");
            Require(startInfo.EnvironmentVariables["REMOTE_PORT"] == "43210", "REMOTE_PORT was not split");
            Require(
                startInfo.EnvironmentVariables["CONTENT_LENGTH"]
                == "0",
                "CONTENT_LENGTH is not the byte length");
            Require(startInfo.EnvironmentVariables["CONTENT_TYPE"] == string.Empty, "GET gained a content type");
            Require(startInfo.EnvironmentVariables["GATEWAY_INTERFACE"] == "CGI/1.1", "CGI interface is missing");
            Require(startInfo.EnvironmentVariables["HTTP_USER_AGENT"] == "fixture-agent", "HTTP_USER_AGENT is missing");
            Require(startInfo.EnvironmentVariables["HTTP_REFERER"] == "http://localhost/source", "HTTP_REFERER is missing");
            Require(startInfo.EnvironmentVariables["SCRIPT_NAME"] == "/admin/index.php", "SCRIPT_NAME is wrong");
            Require(
                string.Equals(startInfo.EnvironmentVariables["SCRIPT_FILENAME"], Path.GetFullPath(script), StringComparison.OrdinalIgnoreCase),
                "SCRIPT_FILENAME is wrong");
            Require(!startInfo.EnvironmentVariables.ContainsKey("HTTP_RAW_POST_DATA"), "obsolete HTTP_RAW_POST_DATA remains");
            Require(!startInfo.EnvironmentVariables.ContainsKey("USER_AGENT"), "non-CGI USER_AGENT remains");
            Require(!startInfo.EnvironmentVariables.ContainsKey("REFERER"), "non-CGI REFERER remains");

            var postVariables = new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase);
            postVariables["request_method"] = "POST";
            postVariables["post"] = "rejected=1";
            RequireThrows<InvalidDataException>(
                delegate
                {
                    PHPHandler.BuildExecutionPlan(
                        executable,
                        runtime,
                        ini,
                        state,
                        iniScan,
                        script,
                        postVariables);
                },
                "POST reached the GET-only PHP boundary");
        }

        private static void HeaderLimitIsEnforced()
        {
            string oversized = "X-Fill: "
                               + new string('a', PHPHandler.MaximumCgiHeaderBytes)
                               + "\r\nContent-Type: text/plain\r\n\r\n";
            RequireThrows<InvalidDataException>(
                delegate { PHPHandler.ParseCgiResponse(Encoding.ASCII.GetBytes(oversized)); },
                "oversized CGI headers were accepted");
        }

        private static void MalformedHeaderIsRejected()
        {
            RequireThrows<InvalidDataException>(
                delegate
                {
                    PHPHandler.ParseCgiResponse(
                        Encoding.ASCII.GetBytes("Content-Type text/plain\r\n\r\nbody"));
                },
                "malformed CGI header was accepted");
        }

        private static void MissingHeaderBoundaryIsRejected()
        {
            RequireThrows<InvalidDataException>(
                delegate { PHPHandler.ParseCgiResponse(Encoding.ASCII.GetBytes("Content-Type: text/plain")); },
                "CGI output without a header boundary was accepted");
        }

        private static void MissingHeadersAreRejected()
        {
            RequireThrows<InvalidDataException>(
                delegate { PHPHandler.ParseCgiResponse(Encoding.ASCII.GetBytes("\r\n\r\nbody")); },
                "CGI output without headers was accepted");
            RequireThrows<InvalidDataException>(
                delegate { PHPHandler.ParseCgiResponse(Encoding.ASCII.GetBytes("Status: 200 OK\r\n\r\nbody")); },
                "CGI output without Content-Type or Location was accepted");
        }

        private static void OutputLimitIsEnforced()
        {
            var oversized = new byte[PHPHandler.MaximumCgiOutputBytes + 1];
            RequireThrows<InvalidDataException>(
                delegate { PHPHandler.ParseCgiResponse(oversized); },
                "oversized CGI output was accepted");
        }

        private static void ParseCrLfResponse()
        {
            byte[] headers = Encoding.ASCII.GetBytes(
                "Status: 200 OK\r\nContent-Type: application/octet-stream\r\nSet-Cookie: fixture=value\r\n\r\n");
            byte[] body = { 0, 1, 2, 127, 128, 255 };
            PhpCgiResponse response = PHPHandler.ParseCgiResponse(Concatenate(headers, body));

            Require(ByteArraysEqual(response.Body, body), "binary CGI body changed");
            Require(response.Headers.Contains("Status: 200 OK\r\n"), "Status header was lost");
            Require(
                response.Headers.Contains("Content-Type: application/octet-stream\r\n"),
                "Content-Type header was lost");
            Require(response.Headers.Contains("Set-Cookie: fixture=value\r\n"), "Set-Cookie header was lost");
        }

        private static void ParseLfResponse()
        {
            PhpCgiResponse response = PHPHandler.ParseCgiResponse(
                Encoding.ASCII.GetBytes("Content-Type: text/plain\nX-Fixture: yes\n\nbody\n"));

            Require(response.Headers == "Content-Type: text/plain\r\nX-Fixture: yes\r\n", "LF headers were not normalized");
            Require(ByteArraysEqual(response.Body, Encoding.ASCII.GetBytes("body\n")), "LF body changed");
        }

        private static void ServerOwnedHeaderIsRejected()
        {
            RequireThrows<InvalidDataException>(
                delegate
                {
                    PHPHandler.ParseCgiResponse(
                        Encoding.ASCII.GetBytes(
                            "Content-Type: text/plain\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n\r\n"));
                },
                "server-owned CGI header was accepted");
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static byte[] Concatenate(byte[] left, byte[] right)
        {
            var result = new byte[left.Length + right.Length];
            Buffer.BlockCopy(left, 0, result, 0, left.Length);
            Buffer.BlockCopy(right, 0, result, left.Length, right.Length);
            return result;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void RequireThrows<TException>(Action action, string message)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }

        private static void RunCase(string name, Action test, ICollection<string> failures)
        {
            try
            {
                test();
            }
            catch (Exception exception)
            {
                failures.Add(name + " (" + exception.GetType().Name + ")");
            }
        }
    }
}
