namespace WebEngine
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal static class WebRequestPathPolicySelfTests
    {
        public static bool Run(TextWriter output)
        {
            var cases = new Dictionary<string, bool>
            {
                { "public-index", Resolve("index.php", WebRequestFileKind.Php) },
                { "public-static", Resolve("images/icon.png", WebRequestFileKind.Static) },
                { "parent-traversal", Reject("../Config.xml") },
                { "encoded-traversal", Reject("%2e%2e/Config.xml") },
                { "double-encoded-traversal", Reject("%252e%252e/Config.xml") },
                { "mixed-slash", Reject("images\\..\\Config.xml") },
                { "absolute-path", Reject("/index.php") },
                { "drive-path", Reject("C:/index.php") },
                { "admin-route", Reject("admin/index.php") },
                { "internal-include", Reject("includes/config.php") },
                { "login-route", Reject("process-login.php") },
                { "registration-route", Reject("register.php") },
                { "sql-file", Reject("includes/login.sql") },
                { "executable", Reject("tool.exe") },
                { "unknown-extension", Reject("README.md") }
            };

            foreach (KeyValuePair<string, bool> testCase in cases)
            {
                if (!testCase.Value)
                {
                    output.WriteLine("[Web request path policy] FAIL case=" + testCase.Key);
                    return false;
                }
            }

            output.WriteLine("[Web request path policy] PASS 15/15");
            return true;
        }

        private static bool Reject(string path)
        {
            return !WebRequestPathPolicy.Resolve(Path.GetTempPath(), path).IsAllowed;
        }

        private static bool Resolve(string path, WebRequestFileKind expectedKind)
        {
            WebRequestPathResult result = WebRequestPathPolicy.Resolve(Path.GetTempPath(), path);
            return result.IsAllowed && result.Kind == expectedKind;
        }
    }
}
