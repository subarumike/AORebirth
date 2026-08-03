#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace WebEngine.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using WebEngine.ErrorHandlers;

    using _config = Utility.Config.ConfigReadWrite;

    internal sealed class PhpCgiExecutionPlan
    {
        public string Arguments { get; set; }

        public IDictionary<string, string> EnvironmentVariables { get; set; }

        public string ExecutablePath { get; set; }

        public string IniPath { get; set; }

        public string IniScanDirectory { get; set; }

        public string StateDirectory { get; set; }

        public byte[] StandardInput { get; set; }

        public string WorkingDirectory { get; set; }
    }

    internal sealed class PhpCgiResponse
    {
        public byte[] Body { get; set; }

        public string Headers { get; set; }
    }

    internal sealed class WebCoreDatabaseSettings
    {
        public string Database { get; set; }

        public string Host { get; set; }

        public string Password { get; set; }

        public string User { get; set; }
    }

    internal class PHPHandler
    {
        internal const int CgiTimeoutMilliseconds = 30000;

        internal const int MaximumCgiHeaderBytes = 64 * 1024;

        internal const int MaximumCgiOutputBytes = 8 * 1024 * 1024;

        private readonly byte[] responseBody;

        private readonly ResponseHeader responseHeaders;

        public PHPHandler(string fileName, Dictionary<string, string> envVariables)
        {
            string fullFilePath = Path.GetFullPath(fileName);
            if (!File.Exists(fullFilePath))
            {
                var error = new Error404();
                this.responseHeaders = error.getResponseHeader();
                this.responseBody = Encoding.UTF8.GetBytes(error.getResponseBody());
                return;
            }

            PhpRuntimeValidationResult runtime = Program.ValidatedPhpRuntime;
            if (runtime == null || !runtime.IsValid)
            {
                throw new InvalidOperationException(
                    "The immutable validated PHP runtime context is unavailable.");
            }

            PhpRuntimeValidator.ValidateMutableStateDirectories(runtime.StateDirectory);

            var requestVariables = new Dictionary<string, string>(envVariables, StringComparer.OrdinalIgnoreCase);
            AddDatabaseEnvironment(requestVariables, _config.Instance.CurrentConfig.MysqlConnection);
            PhpCgiExecutionPlan plan = BuildExecutionPlan(
                runtime.ExecutablePath,
                runtime.RuntimeDirectory,
                runtime.IniPath,
                runtime.StateDirectory,
                runtime.IniScanDirectory,
                fullFilePath,
                requestVariables);
            byte[] output = Execute(plan);
            PhpCgiResponse response = ParseCgiResponse(output);

            this.responseBody = response.Body;
            this.responseHeaders = new ResponseHeader(response.Headers, fullFilePath);
            this.responseHeaders.setContentLength(this.responseBody.Length);
        }

        internal static PhpCgiExecutionPlan BuildExecutionPlan(
            string executablePath,
            string runtimeDirectory,
            string iniPath,
            string stateDirectory,
            string iniScanDirectory,
            string scriptPath,
            IDictionary<string, string> suppliedVariables)
        {
            if (suppliedVariables == null)
            {
                throw new ArgumentNullException("suppliedVariables");
            }

            string canonicalRuntime = CanonicalDirectory(runtimeDirectory, "PHP runtime directory");
            string canonicalExecutable = CanonicalContainedFile(
                canonicalRuntime,
                executablePath,
                "PHP CGI executable");
            string canonicalIni = CanonicalContainedFile(
                canonicalRuntime,
                iniPath,
                "PHP configuration");
            string canonicalState = CanonicalDirectory(stateDirectory, "PHP state directory");
            if (!string.IsNullOrEmpty(iniScanDirectory))
            {
                throw new InvalidDataException("Supplemental PHP INI scanning must be disabled.");
            }

            string canonicalIniScan = string.Empty;
            string canonicalDocumentRoot = CanonicalDirectory(
                RequiredValue(suppliedVariables, "document_root"),
                "CGI document root");
            string canonicalScript = CanonicalContainedFile(
                canonicalDocumentRoot,
                scriptPath,
                "CGI script");
            string workingDirectory = Path.GetDirectoryName(canonicalScript);
            if (string.IsNullOrEmpty(workingDirectory))
            {
                throw new InvalidDataException("The CGI script directory is invalid.");
            }

            string requestMethod = RequiredValue(suppliedVariables, "request_method").ToUpperInvariant();
            if (requestMethod != "GET")
            {
                throw new InvalidDataException("Only host-normalized GET requests may execute PHP CGI.");
            }

            string post = OptionalValue(suppliedVariables, "post");
            if (post.Length != 0)
            {
                throw new InvalidDataException("The GET-only PHP boundary does not accept a request body.");
            }

            byte[] postBytes = new byte[0];

            string remoteAddress;
            string remotePort;
            SplitRemoteEndpoint(
                RequiredValue(suppliedVariables, "remote_addr"),
                out remoteAddress,
                out remotePort);

            string scriptName = RequiredValue(suppliedVariables, "script_name").Replace('\\', '/');
            if (!scriptName.StartsWith("/", StringComparison.Ordinal))
            {
                throw new InvalidDataException("CGI SCRIPT_NAME must be root-relative.");
            }

            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                              {
                                  { "AUTH_TYPE", string.Empty },
                                  { "AOREBIRTH_PHP_STATE_DIR", canonicalState },
                                  { "AOREBIRTH_WEBCORE_ROOT", canonicalDocumentRoot },
                                  { "CONTENT_LENGTH", postBytes.Length.ToString(CultureInfo.InvariantCulture) },
                                  {
                                      "CONTENT_TYPE",
                                      string.Empty
                                  },
                                  { "DOCUMENT_ROOT", canonicalDocumentRoot },
                                  { "DOCUMENT_URI", scriptName },
                                  { "GATEWAY_INTERFACE", "CGI/1.1" },
                                  { "HTTPS", "off" },
                                  { "HTTP_COOKIE", OptionalValue(suppliedVariables, "cookie") },
                                  {
                                      "HTTP_HOST",
                                      RequiredValue(suppliedVariables, "server_name") + ":"
                                      + RequiredValue(suppliedVariables, "server_port")
                                  },
                                  { "HTTP_REFERER", OptionalValue(suppliedVariables, "referer") },
                                  { "HTTP_USER_AGENT", OptionalValue(suppliedVariables, "user_agent") },
                                  { "PATH_INFO", string.Empty },
                                  { "PATH_TRANSLATED", canonicalScript },
                                  { "QUERY_STRING", OptionalValue(suppliedVariables, "query_string") },
                                  { "REDIRECT_STATUS", "200" },
                                  { "REMOTE_ADDR", remoteAddress },
                                  { "REMOTE_PORT", remotePort },
                                  { "REMOTE_USER", string.Empty },
                                  { "REQUEST_METHOD", requestMethod },
                                  { "REQUEST_SCHEME", "http" },
                                  { "REQUEST_URI", RequiredValue(suppliedVariables, "request_uri") },
                                  { "SCRIPT_FILENAME", canonicalScript },
                                  { "SCRIPT_NAME", scriptName },
                                  { "SERVER_NAME", RequiredValue(suppliedVariables, "server_name") },
                                  { "SERVER_PORT", RequiredValue(suppliedVariables, "server_port") },
                                  { "SERVER_PROTOCOL", RequiredValue(suppliedVariables, "server_protocol") },
                                  { "SERVER_SOFTWARE", "AORebirth-WebEngine" }
                              };

            AddOptionalDatabaseVariable(
                environment,
                suppliedVariables,
                "db_host",
                "AOREBIRTH_WEBCORE_DB_HOST");
            AddOptionalDatabaseVariable(
                environment,
                suppliedVariables,
                "db_name",
                "AOREBIRTH_WEBCORE_DB_NAME");
            AddOptionalDatabaseVariable(
                environment,
                suppliedVariables,
                "db_user",
                "AOREBIRTH_WEBCORE_DB_USER");
            AddOptionalDatabaseVariable(
                environment,
                suppliedVariables,
                "db_password",
                "AOREBIRTH_WEBCORE_DB_PASSWORD");

            foreach (KeyValuePair<string, string> variable in environment)
            {
                ValidateEnvironmentValue(variable.Key, variable.Value);
            }

            return new PhpCgiExecutionPlan
                   {
                       Arguments = "-c " + QuoteWindowsArgument(canonicalIni),
                       EnvironmentVariables = environment,
                       ExecutablePath = canonicalExecutable,
                       IniPath = canonicalIni,
                       IniScanDirectory = canonicalIniScan,
                       StateDirectory = canonicalState,
                       StandardInput = postBytes,
                       WorkingDirectory = workingDirectory
                   };
        }

        private static void AddDatabaseEnvironment(
            IDictionary<string, string> variables,
            string connectionString)
        {
            WebCoreDatabaseSettings settings = ParseDatabaseConnection(connectionString);
            variables["db_host"] = settings.Host;
            variables["db_name"] = settings.Database;
            variables["db_user"] = settings.User;
            variables["db_password"] = settings.Password;
        }

        internal static WebCoreDatabaseSettings ParseDatabaseConnection(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("The WebCore database connection is not configured.");
            }

            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            string host = ConnectionValue(builder, "Server", "Data Source", "Host");
            string database = ConnectionValue(builder, "Database", "Initial Catalog");
            string user = ConnectionValue(builder, "Uid", "User ID", "User");
            string password = ConnectionValue(builder, "Pwd", "Password");
            if (IsMissingOrPlaceholder(host)
                || IsMissingOrPlaceholder(database)
                || IsMissingOrPlaceholder(user)
                || IsMissingOrPlaceholder(password))
            {
                throw new InvalidOperationException(
                    "The WebCore database connection is incomplete or still contains a placeholder.");
            }

            return new WebCoreDatabaseSettings
                   {
                       Host = host,
                       Database = database,
                       User = user,
                       Password = password
                   };
        }

        private static bool IsMissingOrPlaceholder(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                   || value.IndexOf("REPLACE_WITH", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ConnectionValue(
            DbConnectionStringBuilder builder,
            params string[] keys)
        {
            foreach (string key in keys)
            {
                object value;
                if (builder.TryGetValue(key, out value) && value != null)
                {
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
                }
            }

            return string.Empty;
        }

        private static void AddOptionalDatabaseVariable(
            IDictionary<string, string> environment,
            IDictionary<string, string> suppliedVariables,
            string suppliedName,
            string environmentName)
        {
            string value;
            if (suppliedVariables.TryGetValue(suppliedName, out value))
            {
                environment[environmentName] = value;
            }
        }

        internal static ProcessStartInfo BuildProcessStartInfo(PhpCgiExecutionPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException("plan");
            }

            var startInfo = new ProcessStartInfo
                            {
                                Arguments = plan.Arguments,
                                CreateNoWindow = true,
                                FileName = plan.ExecutablePath,
                                RedirectStandardError = true,
                                RedirectStandardInput = true,
                                RedirectStandardOutput = true,
                                UseShellExecute = false,
                                WorkingDirectory = plan.WorkingDirectory
                            };

            string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            ValidateEnvironmentValue("SystemRoot", windowsDirectory);
            string temporaryDirectory = Path.Combine(plan.StateDirectory, "tmp");

            startInfo.EnvironmentVariables.Clear();
            startInfo.EnvironmentVariables["SystemRoot"] = windowsDirectory;
            startInfo.EnvironmentVariables["WINDIR"] = windowsDirectory;
            startInfo.EnvironmentVariables["TEMP"] = temporaryDirectory;
            startInfo.EnvironmentVariables["TMP"] = temporaryDirectory;
            startInfo.EnvironmentVariables["PHPRC"] = plan.IniPath;
            startInfo.EnvironmentVariables["PHP_INI_SCAN_DIR"] = plan.IniScanDirectory;
            startInfo.EnvironmentVariables["PHP_FCGI_CHILDREN"] = string.Empty;
            startInfo.EnvironmentVariables["PHP_FCGI_MAX_REQUESTS"] = string.Empty;

            foreach (KeyValuePair<string, string> variable in plan.EnvironmentVariables)
            {
                startInfo.EnvironmentVariables[variable.Key] = variable.Value;
            }

            return startInfo;
        }

        internal static PhpCgiResponse ParseCgiResponse(byte[] output)
        {
            if (output == null)
            {
                throw new ArgumentNullException("output");
            }

            if (output.Length > MaximumCgiOutputBytes)
            {
                throw new InvalidDataException("PHP CGI output exceeds the 8 MiB response limit.");
            }

            int delimiterLength;
            int headerLength = FindHeaderDelimiter(output, out delimiterLength);
            if (headerLength < 0)
            {
                throw new InvalidDataException("PHP CGI output does not contain a complete header block.");
            }

            if (headerLength == 0)
            {
                throw new InvalidDataException("PHP CGI output contains no response headers.");
            }

            if (headerLength > MaximumCgiHeaderBytes)
            {
                throw new InvalidDataException("PHP CGI headers exceed the 64 KiB header limit.");
            }

            for (int index = 0; index < headerLength; index++)
            {
                byte value = output[index];
                if (value != 9 && value != 10 && value != 13 && (value < 32 || value > 126))
                {
                    throw new InvalidDataException("PHP CGI headers contain a non-ASCII control byte.");
                }
            }

            string rawHeaders = Encoding.ASCII.GetString(output, 0, headerLength).Replace("\r\n", "\n");
            if (rawHeaders.IndexOf('\r') >= 0)
            {
                throw new InvalidDataException("PHP CGI headers contain malformed line endings.");
            }

            bool hasContentType = false;
            bool hasLocation = false;
            var normalizedHeaders = new StringBuilder(headerLength + 16);
            string[] lines = rawHeaders.Split(new[] { '\n' }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                if (line.Length == 0 || char.IsWhiteSpace(line[0]))
                {
                    throw new InvalidDataException("PHP CGI headers contain an empty or folded header line.");
                }

                int separator = line.IndexOf(':');
                if (separator <= 0)
                {
                    throw new InvalidDataException("PHP CGI output contains a malformed response header.");
                }

                string name = line.Substring(0, separator);
                for (int index = 0; index < name.Length; index++)
                {
                    if (!IsHeaderTokenCharacter(name[index]))
                    {
                        throw new InvalidDataException("PHP CGI output contains an invalid header name.");
                    }
                }

                string value = line.Substring(separator + 1).Trim(' ', '\t');
                if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Connection", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("PHP CGI output contains a server-owned response header.");
                }

                if (string.Equals(name, "Status", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateStatusHeader(value);
                    name = "Status";
                }
                else if (string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    hasContentType = true;
                }
                else if (string.Equals(name, "Location", StringComparison.OrdinalIgnoreCase))
                {
                    hasLocation = true;
                }

                normalizedHeaders.Append(name);
                normalizedHeaders.Append(": ");
                normalizedHeaders.Append(value);
                normalizedHeaders.Append("\r\n");
            }

            if (!hasContentType && !hasLocation)
            {
                throw new InvalidDataException(
                    "PHP CGI output must contain a Content-Type or Location response header.");
            }

            int bodyOffset = headerLength + delimiterLength;
            byte[] body = new byte[output.Length - bodyOffset];
            if (body.Length != 0)
            {
                Buffer.BlockCopy(output, bodyOffset, body, 0, body.Length);
            }

            return new PhpCgiResponse { Body = body, Headers = normalizedHeaders.ToString() };
        }

        public byte[] getResponseBody()
        {
            return this.responseBody;
        }

        public string getResponseHeaders()
        {
            return this.responseHeaders.getResponseHeaders();
        }

        private static string CanonicalContainedFile(string root, string filePath, string description)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new InvalidDataException(description + " is missing.");
            }

            string canonicalFile = Path.GetFullPath(filePath);
            string rootPrefix = AddDirectorySeparator(Path.GetFullPath(root));
            if (!canonicalFile.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(description + " is outside its approved root.");
            }

            return canonicalFile;
        }

        private static string CanonicalContainedDirectory(string root, string path, string description)
        {
            string canonicalDirectory = CanonicalDirectory(path, description);
            string rootPrefix = AddDirectorySeparator(Path.GetFullPath(root));
            if (!AddDirectorySeparator(canonicalDirectory).StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(description + " is outside its approved root.");
            }

            return canonicalDirectory;
        }

        private static string CanonicalDirectory(string path, string description)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidDataException(description + " is missing.");
            }

            string canonical = Path.GetFullPath(path);
            string root = Path.GetPathRoot(canonical);
            return string.Equals(canonical, root, StringComparison.OrdinalIgnoreCase)
                       ? canonical
                       : canonical.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string AddDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                       ? path
                       : path + Path.DirectorySeparatorChar;
        }

        private static byte[] Execute(PhpCgiExecutionPlan plan)
        {
            ProcessStartInfo startInfo = BuildProcessStartInfo(plan);
            using (var process = new Process { StartInfo = startInfo })
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("The validated PHP CGI process could not be started.");
                }

                var budget = new SharedOutputBudget(MaximumCgiOutputBytes);
                Task<byte[]> stdoutTask = Task.Factory.StartNew(
                    delegate { return ReadBounded(process.StandardOutput.BaseStream, budget); },
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
                Task<byte[]> stderrTask = Task.Factory.StartNew(
                    delegate { return ReadBounded(process.StandardError.BaseStream, budget); },
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
                Task inputTask = Task.Factory.StartNew(
                    delegate
                    {
                        try
                        {
                            process.StandardInput.BaseStream.Write(
                                plan.StandardInput,
                                0,
                                plan.StandardInput.Length);
                            process.StandardInput.BaseStream.Flush();
                        }
                        finally
                        {
                            process.StandardInput.Close();
                        }
                    },
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

                var stopwatch = Stopwatch.StartNew();
                while (!process.WaitForExit(50))
                {
                    ThrowIfFaulted(stdoutTask, stderrTask, inputTask, process);
                    if (stopwatch.ElapsedMilliseconds >= CgiTimeoutMilliseconds)
                    {
                        Terminate(process);
                        throw new TimeoutException("PHP CGI execution exceeded the 30 second limit.");
                    }
                }

                int remaining = CgiTimeoutMilliseconds - (int)stopwatch.ElapsedMilliseconds;
                if (remaining < 0
                    || !Task.WaitAll(new Task[] { stdoutTask, stderrTask, inputTask }, remaining))
                {
                    Terminate(process);
                    throw new TimeoutException("PHP CGI stream processing exceeded the 30 second limit.");
                }

                ThrowIfFaulted(stdoutTask, stderrTask, inputTask, process);
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "PHP CGI exited with status "
                        + process.ExitCode.ToString(CultureInfo.InvariantCulture)
                        + ".");
                }

                return stdoutTask.Result;
            }
        }

        private static int FindHeaderDelimiter(byte[] output, out int delimiterLength)
        {
            for (int index = 0; index < output.Length - 1; index++)
            {
                if (index <= output.Length - 4
                    && output[index] == 13
                    && output[index + 1] == 10
                    && output[index + 2] == 13
                    && output[index + 3] == 10)
                {
                    delimiterLength = 4;
                    return index;
                }

                if (output[index] == 10 && output[index + 1] == 10)
                {
                    delimiterLength = 2;
                    return index;
                }
            }

            delimiterLength = 0;
            return -1;
        }

        private static bool IsHeaderTokenCharacter(char value)
        {
            if ((value >= 'a' && value <= 'z')
                || (value >= 'A' && value <= 'Z')
                || (value >= '0' && value <= '9'))
            {
                return true;
            }

            return "!#$%&'*+-.^_`|~".IndexOf(value) >= 0;
        }

        private static string OptionalValue(
            IDictionary<string, string> variables,
            string key,
            string defaultValue = "")
        {
            string value;
            return variables.TryGetValue(key, out value) && value != null ? value : defaultValue;
        }

        private static string QuoteWindowsArgument(string value)
        {
            var result = new StringBuilder(value.Length + 2);
            result.Append('"');
            int backslashes = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (character == '"')
                {
                    result.Append('\\', backslashes * 2 + 1);
                    result.Append('"');
                    backslashes = 0;
                    continue;
                }

                result.Append('\\', backslashes);
                backslashes = 0;
                result.Append(character);
            }

            result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }

        private static byte[] ReadBounded(Stream stream, SharedOutputBudget budget)
        {
            var buffer = new byte[81920];
            using (var output = new MemoryStream())
            {
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    budget.Consume(read);
                    output.Write(buffer, 0, read);
                }

                return output.ToArray();
            }
        }

        private static string RequiredValue(IDictionary<string, string> variables, string key)
        {
            string value;
            if (!variables.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException("A required CGI variable is missing: " + key);
            }

            return value;
        }

        private static void SplitRemoteEndpoint(
            string endpoint,
            out string remoteAddress,
            out string remotePort)
        {
            IPAddress parsedAddress;
            if (IPAddress.TryParse(endpoint, out parsedAddress))
            {
                remoteAddress = parsedAddress.ToString();
                remotePort = "0";
                return;
            }

            if (endpoint.StartsWith("[", StringComparison.Ordinal))
            {
                int bracket = endpoint.IndexOf(']');
                int port;
                if (bracket > 1
                    && bracket + 2 < endpoint.Length
                    && endpoint[bracket + 1] == ':'
                    && IPAddress.TryParse(endpoint.Substring(1, bracket - 1), out parsedAddress)
                    && int.TryParse(
                        endpoint.Substring(bracket + 2),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out port)
                    && port >= 0
                    && port <= 65535)
                {
                    remoteAddress = parsedAddress.ToString();
                    remotePort = port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
            }
            else
            {
                int separator = endpoint.LastIndexOf(':');
                int port;
                if (separator > 0
                    && IPAddress.TryParse(endpoint.Substring(0, separator), out parsedAddress)
                    && int.TryParse(
                        endpoint.Substring(separator + 1),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out port)
                    && port >= 0
                    && port <= 65535)
                {
                    remoteAddress = parsedAddress.ToString();
                    remotePort = port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
            }

            throw new InvalidDataException("The CGI remote endpoint is invalid.");
        }

        private static void Terminate(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
            }
            catch
            {
                // The original bounded-execution failure remains authoritative.
            }
        }

        private static void ThrowIfFaulted(
            Task<byte[]> stdoutTask,
            Task<byte[]> stderrTask,
            Task inputTask,
            Process process)
        {
            Task faulted = stdoutTask.IsFaulted
                               ? (Task)stdoutTask
                               : stderrTask.IsFaulted
                                     ? stderrTask
                                     : inputTask.IsFaulted ? inputTask : null;
            if (faulted == null)
            {
                return;
            }

            Terminate(process);
            AggregateException aggregate = faulted.Exception;
            Exception failure = aggregate == null ? null : aggregate.Flatten().InnerExceptions[0];
            if (failure == null)
            {
                throw new InvalidOperationException("PHP CGI stream processing failed.");
            }

            throw failure;
        }

        private static void ValidateEnvironmentValue(string name, string value)
        {
            if (value == null
                || value.IndexOf('\0') >= 0
                || value.IndexOf('\r') >= 0
                || value.IndexOf('\n') >= 0)
            {
                throw new InvalidDataException("CGI environment variable is invalid: " + name);
            }
        }

        private static void ValidateStatusHeader(string value)
        {
            int status;
            if (value.Length < 3
                || !int.TryParse(
                    value.Substring(0, 3),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out status)
                || status < 100
                || status > 599
                || (value.Length > 3 && value[3] != ' '))
            {
                throw new InvalidDataException("PHP CGI output contains an invalid Status header.");
            }
        }

        private sealed class SharedOutputBudget
        {
            private readonly int limit;

            private int consumed;

            public SharedOutputBudget(int limit)
            {
                this.limit = limit;
            }

            public void Consume(int count)
            {
                int total = Interlocked.Add(ref this.consumed, count);
                if (total > this.limit)
                {
                    throw new InvalidDataException("PHP CGI output exceeds the 8 MiB response limit.");
                }
            }
        }
    }
}
