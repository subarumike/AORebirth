namespace AOSharpCaptureAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;

    internal sealed class Pf127CaptureSnapshotResult
    {
        internal string OutputDirectory { get; set; }

        internal string ManifestPath { get; set; }
    }

    internal static class Pf127CaptureSnapshot
    {
        private static readonly string[] RequiredFiles =
        {
            "pf127-geometry.json",
            "pf127-line-of-sight.csv",
            "pf127-door-state.csv"
        };

        internal static Pf127CaptureSnapshotResult Create(string sourceDirectory, string outputDirectory)
        {
            return Create(
                sourceDirectory,
                outputDirectory,
                TimeSpan.FromSeconds(20),
                TimeSpan.FromMilliseconds(250));
        }

        internal static int RunSelfTest()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "aorebirth-pf127-snapshot-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source");
            string output = Path.Combine(root, "snapshot");
            try
            {
                Directory.CreateDirectory(source);
                File.WriteAllText(
                    Path.Combine(source, RequiredFiles[0]),
                    "{}\n",
                    new UTF8Encoding(false));
                File.WriteAllText(
                    Path.Combine(source, RequiredFiles[1]),
                    "A,B\n1,2\n",
                    new UTF8Encoding(false));
                File.WriteAllText(
                    Path.Combine(source, RequiredFiles[2]),
                    "C,D\n3,4\n",
                    new UTF8Encoding(false));

                Pf127CaptureSnapshotResult result = Create(
                    source,
                    output,
                    TimeSpan.FromSeconds(3),
                    TimeSpan.FromMilliseconds(25));
                if (!Directory.Exists(result.OutputDirectory)
                    || !File.Exists(result.ManifestPath))
                {
                    throw new InvalidOperationException("Snapshot output or manifest is missing.");
                }

                foreach (string fileName in RequiredFiles)
                {
                    byte[] sourceBytes = File.ReadAllBytes(Path.Combine(source, fileName));
                    byte[] outputBytes = File.ReadAllBytes(Path.Combine(output, fileName));
                    if (!BytesEqual(sourceBytes, outputBytes))
                    {
                        throw new InvalidOperationException(
                            "Snapshot self-test changed " + fileName + ".");
                    }
                }

                bool rejectedExisting = false;
                try
                {
                    Create(
                        source,
                        output,
                        TimeSpan.FromMilliseconds(100),
                        TimeSpan.FromMilliseconds(10));
                }
                catch (IOException)
                {
                    rejectedExisting = true;
                }

                if (!rejectedExisting)
                {
                    throw new InvalidOperationException(
                        "Snapshot self-test did not reject an existing output directory.");
                }

                Console.WriteLine("PF127 capture snapshot self-test PASS");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    "PF127 capture snapshot self-test FAIL: " + exception.Message);
                return 1;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, true);
                    }
                }
                catch
                {
                    // Temporary self-test cleanup must not hide the test result.
                }
            }
        }

        private static Pf127CaptureSnapshotResult Create(
            string sourceDirectory,
            string outputDirectory,
            TimeSpan timeout,
            TimeSpan retryInterval)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory))
            {
                throw new ArgumentException("Source capture directory is required.", "sourceDirectory");
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Snapshot output directory is required.", "outputDirectory");
            }

            string sourceFullPath = Path.GetFullPath(sourceDirectory);
            string outputFullPath = Path.GetFullPath(outputDirectory);
            if (!Directory.Exists(sourceFullPath))
            {
                throw new DirectoryNotFoundException(
                    "PF127 capture directory does not exist: " + sourceFullPath);
            }

            if (Directory.Exists(outputFullPath) || File.Exists(outputFullPath))
            {
                throw new IOException(
                    "PF127 snapshot output already exists and will not be overwritten: "
                    + outputFullPath);
            }

            string parent = Path.GetDirectoryName(outputFullPath);
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidOperationException("Snapshot output has no parent directory.");
            }

            Directory.CreateDirectory(parent);
            string staging = outputFullPath + ".pending-" + Guid.NewGuid().ToString("N");
            DateTime deadlineUtc = DateTime.UtcNow + timeout;
            Dictionary<string, byte[]> previous = null;
            Dictionary<string, byte[]> stable = null;
            while (DateTime.UtcNow <= deadlineUtc)
            {
                Dictionary<string, byte[]> current = ReadRequiredFiles(sourceFullPath);
                ValidateRequiredFiles(current);
                if (previous != null && FileSetsEqual(previous, current))
                {
                    stable = current;
                    break;
                }

                previous = current;
                Thread.Sleep(retryInterval);
            }

            if (stable == null)
            {
                throw new IOException(
                    "PF127 capture files did not produce two byte-identical, structurally complete reads before the snapshot timeout.");
            }

            try
            {
                Directory.CreateDirectory(staging);
                var manifestRows = new List<string>();
                foreach (string fileName in RequiredFiles)
                {
                    byte[] bytes = stable[fileName];
                    string destination = Path.Combine(staging, fileName);
                    WriteDurable(destination, bytes);
                    byte[] written = File.ReadAllBytes(destination);
                    if (!BytesEqual(bytes, written))
                    {
                        throw new IOException(
                            "PF127 snapshot verification changed " + fileName + ".");
                    }

                    manifestRows.Add(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "    {0}\"file\": \"{1}\", \"length\": {2}, \"sha256\": \"{3}\"{4}",
                            "{ ",
                            EscapeJson(fileName),
                            bytes.LongLength,
                            ComputeSha256(bytes),
                            " }") );
                }

                string manifest = BuildManifest(sourceFullPath, manifestRows);
                string manifestPath = Path.Combine(staging, "pf127-snapshot-manifest.json");
                WriteDurable(manifestPath, new UTF8Encoding(false).GetBytes(manifest));
                Directory.Move(staging, outputFullPath);
                return new Pf127CaptureSnapshotResult
                {
                    OutputDirectory = outputFullPath,
                    ManifestPath = Path.Combine(outputFullPath, "pf127-snapshot-manifest.json")
                };
            }
            catch
            {
                try
                {
                    if (Directory.Exists(staging))
                    {
                        Directory.Delete(staging, true);
                    }
                }
                catch
                {
                    // Preserve the original snapshot failure.
                }

                throw;
            }
        }

        private static Dictionary<string, byte[]> ReadRequiredFiles(string sourceDirectory)
        {
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string fileName in RequiredFiles)
            {
                string path = Path.Combine(sourceDirectory, fileName);
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(
                        "Required PF127 promotion input is missing: " + path,
                        path);
                }

                files.Add(fileName, ReadSharedBytes(path));
            }

            return files;
        }

        private static byte[] ReadSharedBytes(string path)
        {
            using (var input = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                if (input.Length > int.MaxValue)
                {
                    throw new IOException("PF127 snapshot input is too large: " + path);
                }

                var bytes = new byte[(int)input.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = input.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0)
                    {
                        throw new EndOfStreamException(
                            "PF127 snapshot input changed while reading: " + path);
                    }

                    offset += read;
                }

                if (input.ReadByte() != -1)
                {
                    throw new IOException(
                        "PF127 snapshot input grew while reading: " + path);
                }

                return bytes;
            }
        }

        private static void ValidateRequiredFiles(Dictionary<string, byte[]> files)
        {
            byte[] geometry = files[RequiredFiles[0]];
            string geometryText = DecodeUtf8(geometry, RequiredFiles[0]);
            if (!EndsWithNewLine(geometry)
                || !geometryText.TrimStart().StartsWith("{", StringComparison.Ordinal)
                || !geometryText.TrimEnd().EndsWith("}", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "pf127-geometry.json is not a complete newline-terminated JSON object.");
            }

            ValidateCsv(files[RequiredFiles[1]], RequiredFiles[1]);
            ValidateCsv(files[RequiredFiles[2]], RequiredFiles[2]);
        }

        private static void ValidateCsv(byte[] bytes, string fileName)
        {
            if (!EndsWithNewLine(bytes))
            {
                throw new InvalidDataException(fileName + " does not end with a complete row.");
            }

            string text = DecodeUtf8(bytes, fileName).Replace("\r\n", "\n");
            string[] lines = text.Split('\n');
            if (lines.Length < 2 || string.IsNullOrEmpty(lines[0]))
            {
                throw new InvalidDataException(fileName + " has no CSV header.");
            }

            int columns = ParseCsvLine(lines[0], fileName, 1).Count;
            for (int index = 1; index < lines.Length - 1; index++)
            {
                if (string.IsNullOrEmpty(lines[index]))
                {
                    throw new InvalidDataException(
                        fileName + " contains an empty physical row at line " + (index + 1) + ".");
                }

                int rowColumns = ParseCsvLine(lines[index], fileName, index + 1).Count;
                if (rowColumns != columns)
                {
                    throw new InvalidDataException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} line {1} has {2} columns; expected {3}.",
                            fileName,
                            index + 1,
                            rowColumns,
                            columns));
                }
            }
        }

        private static List<string> ParseCsvLine(string line, string fileName, int lineNumber)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < line.Length; index++)
            {
                char character = line[index];
                if (quoted)
                {
                    if (character == '"')
                    {
                        if (index + 1 < line.Length && line[index + 1] == '"')
                        {
                            current.Append('"');
                            index++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        current.Append(character);
                    }
                }
                else if (character == ',')
                {
                    fields.Add(current.ToString());
                    current.Length = 0;
                }
                else if (character == '"')
                {
                    if (current.Length != 0)
                    {
                        throw new InvalidDataException(
                            fileName + " line " + lineNumber + " has an invalid quote.");
                    }

                    quoted = true;
                }
                else
                {
                    current.Append(character);
                }
            }

            if (quoted)
            {
                throw new InvalidDataException(
                    fileName + " line " + lineNumber + " has an unterminated quote.");
            }

            fields.Add(current.ToString());
            return fields;
        }

        private static string DecodeUtf8(byte[] bytes, string fileName)
        {
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException(fileName + " is not valid UTF-8.", exception);
            }
        }

        private static bool EndsWithNewLine(byte[] bytes)
        {
            return bytes.Length > 0 && bytes[bytes.Length - 1] == (byte)'\n';
        }

        private static bool FileSetsEqual(
            Dictionary<string, byte[]> left,
            Dictionary<string, byte[]> right)
        {
            foreach (string fileName in RequiredFiles)
            {
                if (!BytesEqual(left[fileName], right[fileName]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

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

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                {
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static void WriteDurable(string path, byte[] bytes)
        {
            using (var output = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                65536,
                FileOptions.WriteThrough))
            {
                output.Write(bytes, 0, bytes.Length);
                output.Flush(true);
            }
        }

        private static string BuildManifest(string sourceDirectory, List<string> rows)
        {
            var json = new StringBuilder();
            json.Append("{\n");
            json.Append("  \"schemaVersion\": 1,\n");
            json.Append("  \"createdUtc\": \"");
            json.Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            json.Append("\",\n");
            json.Append("  \"sourceDirectory\": \"");
            json.Append(EscapeJson(sourceDirectory));
            json.Append("\",\n");
            json.Append("  \"files\": [\n");
            for (int index = 0; index < rows.Count; index++)
            {
                json.Append(rows[index]);
                json.Append(index + 1 == rows.Count ? "\n" : ",\n");
            }

            json.Append("  ]\n");
            json.Append("}\n");
            return json.ToString();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
