namespace WebEngine
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    internal enum WebRequestFileKind
    {
        None,
        Static,
        Php
    }

    internal sealed class WebRequestPathResult
    {
        private WebRequestPathResult(bool isAllowed, string relativePath, string fullPath, WebRequestFileKind kind)
        {
            this.IsAllowed = isAllowed;
            this.RelativePath = relativePath;
            this.FullPath = fullPath;
            this.Kind = kind;
        }

        public string FullPath { get; private set; }

        public bool IsAllowed { get; private set; }

        public WebRequestFileKind Kind { get; private set; }

        public string RelativePath { get; private set; }

        public static WebRequestPathResult Allowed(string relativePath, string fullPath, WebRequestFileKind kind)
        {
            return new WebRequestPathResult(true, relativePath, fullPath, kind);
        }

        public static WebRequestPathResult Rejected()
        {
            return new WebRequestPathResult(false, null, null, WebRequestFileKind.None);
        }
    }

    internal static class WebRequestPathPolicy
    {
        private static readonly ISet<string> AllowedPhpRoutes = new HashSet<string>(
            new[] { "about.php", "index.php", "notfound.php", "support.php" },
            StringComparer.OrdinalIgnoreCase);

        private static readonly ISet<string> AllowedStaticExtensions = new HashSet<string>(
            new[] { ".css", ".gif", ".ico", ".jpeg", ".jpg", ".js", ".png" },
            StringComparer.OrdinalIgnoreCase);

        public static WebRequestPathResult Resolve(string root, string rawRelativePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(rawRelativePath))
                {
                    return WebRequestPathResult.Rejected();
                }

                if (rawRelativePath.IndexOf('\\') >= 0
                    || rawRelativePath.IndexOf('\0') >= 0
                    || rawRelativePath.StartsWith("/", StringComparison.Ordinal))
                {
                    return WebRequestPathResult.Rejected();
                }

                string decoded = Uri.UnescapeDataString(rawRelativePath);
                if (decoded.IndexOf('%') >= 0
                    || !decoded.IsNormalized(NormalizationForm.FormC)
                    || Path.IsPathRooted(decoded))
                {
                    return WebRequestPathResult.Rejected();
                }

                string[] segments = decoded.Split('/');
                foreach (string segment in segments)
                {
                    if (string.IsNullOrEmpty(segment)
                        || segment == "."
                        || segment == ".."
                        || segment.IndexOf(':') >= 0
                        || segment.EndsWith(" ", StringComparison.Ordinal)
                        || segment.EndsWith(".", StringComparison.Ordinal))
                    {
                        return WebRequestPathResult.Rejected();
                    }

                    foreach (char character in segment)
                    {
                        if (char.IsControl(character)
                            || Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0)
                        {
                            return WebRequestPathResult.Rejected();
                        }
                    }
                }

                string relativePath = string.Join("/", segments);
                if (relativePath.StartsWith("admin/", StringComparison.OrdinalIgnoreCase)
                    || relativePath.StartsWith("includes/", StringComparison.OrdinalIgnoreCase))
                {
                    return WebRequestPathResult.Rejected();
                }

                string extension = Path.GetExtension(relativePath);
                WebRequestFileKind kind;
                if (string.Equals(extension, ".php", StringComparison.OrdinalIgnoreCase))
                {
                    if (!AllowedPhpRoutes.Contains(relativePath))
                    {
                        return WebRequestPathResult.Rejected();
                    }

                    kind = WebRequestFileKind.Php;
                }
                else
                {
                    if (!AllowedStaticExtensions.Contains(extension))
                    {
                        return WebRequestPathResult.Rejected();
                    }

                    kind = WebRequestFileKind.Static;
                }

                string canonicalRoot = Path.GetFullPath(root)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string rootPrefix = canonicalRoot + Path.DirectorySeparatorChar;
                string fullPath = Path.GetFullPath(
                    Path.Combine(canonicalRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return WebRequestPathResult.Rejected();
                }

                return WebRequestPathResult.Allowed(relativePath, fullPath, kind);
            }
            catch (Exception exception)
            {
                if (exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is PathTooLongException
                    || exception is UriFormatException)
                {
                    return WebRequestPathResult.Rejected();
                }

                throw;
            }
        }
    }
}
