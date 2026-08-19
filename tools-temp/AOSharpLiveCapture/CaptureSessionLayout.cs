using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace AOSharpLiveCapture
{
    internal static class CaptureSessionLayout
    {
        private const string CaptureRootConfigFileName = "capture-root.path";
        private const int MaximumAreaNameLength = 80;

        public static string CreateCaptureId(DateTime localTime)
        {
            return localTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        }

        public static string CreateSessionDirectory(
            string pluginDirectory,
            string areaName,
            int resourcePlayfieldId,
            string captureId,
            string qualifier)
        {
            if (string.IsNullOrWhiteSpace(pluginDirectory))
            {
                throw new ArgumentException("Plugin directory is required.", "pluginDirectory");
            }

            if (string.IsNullOrWhiteSpace(captureId))
            {
                throw new ArgumentException("Capture id is required.", "captureId");
            }

            string captureRoot = ResolveCaptureRoot(pluginDirectory);
            Directory.CreateDirectory(captureRoot);

            string normalizedAreaName = NormalizeAreaName(areaName);
            string playfieldLabel = resourcePlayfieldId > 0
                                        ? "PF " + resourcePlayfieldId.ToString(CultureInfo.InvariantCulture)
                                        : "PF unknown";
            string normalizedQualifier = NormalizeQualifier(qualifier);
            string stem = normalizedAreaName
                          + " ["
                          + playfieldLabel
                          + "]"
                          + normalizedQualifier
                          + " - "
                          + captureId;

            for (int suffix = 0; suffix < 1000; suffix++)
            {
                string name = suffix == 0
                                  ? stem
                                  : stem + " - " + suffix.ToString("000", CultureInfo.InvariantCulture);
                string candidate = Path.Combine(captureRoot, name);
                if (Directory.Exists(candidate))
                {
                    continue;
                }

                Directory.CreateDirectory(candidate);
                return candidate;
            }

            throw new IOException("Could not allocate a unique AOSharp capture directory.");
        }

        public static string NormalizeAreaName(string areaName)
        {
            string normalized = NormalizePathSegment(areaName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "Unknown Area";
            }

            if (normalized.Length > MaximumAreaNameLength)
            {
                normalized = normalized.Substring(0, MaximumAreaNameLength).TrimEnd(' ', '.');
            }

            return string.IsNullOrWhiteSpace(normalized) ? "Unknown Area" : normalized;
        }

        private static string ResolveCaptureRoot(string pluginDirectory)
        {
            string configPath = Path.Combine(pluginDirectory, CaptureRootConfigFileName);
            if (!File.Exists(configPath))
            {
                return Path.Combine(pluginDirectory, "captures");
            }

            string configuredRoot = File.ReadAllText(configPath, Encoding.UTF8).Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(configuredRoot) || !Path.IsPathRooted(configuredRoot))
            {
                throw new IOException("AOSharp capture root configuration must contain an absolute path: " + configPath);
            }

            return Path.GetFullPath(configuredRoot);
        }

        private static string NormalizeQualifier(string qualifier)
        {
            string normalized = NormalizePathSegment(qualifier);
            return string.IsNullOrWhiteSpace(normalized) ? string.Empty : " - " + normalized;
        }

        private static string NormalizePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            StringBuilder result = new StringBuilder(value.Length);
            bool previousWasSpace = false;
            foreach (char character in value.Trim())
            {
                bool replace = char.IsControl(character)
                               || Array.IndexOf(invalidCharacters, character) >= 0;
                char output = replace ? ' ' : character;
                if (char.IsWhiteSpace(output))
                {
                    if (!previousWasSpace)
                    {
                        result.Append(' ');
                    }

                    previousWasSpace = true;
                    continue;
                }

                result.Append(output);
                previousWasSpace = false;
            }

            return result.ToString().Trim(' ', '.');
        }
    }
}
