namespace AORebirth.LinuxBuild.LoginEnginePublishDirectoryGuard
{
    using System;
    using System.IO;

    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine(
                    "Usage: LoginEnginePublishDirectoryGuard <repository-root> <linux-x64|linux-arm64> <framework-dependent|self-contained>");
                return 2;
            }

            if (args[1] != "linux-x64" && args[1] != "linux-arm64")
            {
                Console.Error.WriteLine("Unsupported runtime identifier.");
                return 2;
            }

            if (args[2] != "framework-dependent" && args[2] != "self-contained")
            {
                Console.Error.WriteLine("Unsupported package kind.");
                return 2;
            }

            string repositoryRoot = Path.GetFullPath(args[0]);
            if (!File.Exists(Path.Combine(repositoryRoot, "AGENTS.md"))
                || !File.Exists(Path.Combine(repositoryRoot, "AI_START_HERE.md"))
                || !File.Exists(Path.Combine(
                    repositoryRoot,
                    "LinuxBuild",
                    "Projects",
                    "LoginEngine.Linux.csproj"))
                || !File.Exists(Path.Combine(
                    repositoryRoot,
                    "LinuxBuild",
                    "source-inventory",
                    "inventory.json")))
            {
                Console.Error.WriteLine(
                    "Refusing to prepare a publish directory outside an AORebirth Linux workspace.");
                return 1;
            }

            string artifactsRoot = Path.GetFullPath(
                Path.Combine(repositoryRoot, "LinuxBuild", "artifacts", "loginengine"));
            string target = Path.GetFullPath(Path.Combine(artifactsRoot, args[1], args[2]));
            string requiredPrefix = artifactsRoot.TrimEnd(Path.DirectorySeparatorChar)
                                    + Path.DirectorySeparatorChar;

            if (!target.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(target, artifactsRoot, StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine(
                    "Refusing to prepare a publish directory outside LinuxBuild/artifacts/loginengine.");
                return 1;
            }

            if (ContainsReparsePoint(repositoryRoot, target))
            {
                Console.Error.WriteLine(
                    "Refusing to prepare a publish path containing a reparse point.");
                return 1;
            }

            if (Directory.Exists(target))
            {
                Directory.Delete(target, true);
            }

            Directory.CreateDirectory(target);
            Console.WriteLine("READY: " + target);
            return 0;
        }

        private static bool ContainsReparsePoint(string repositoryRoot, string target)
        {
            string current = Path.GetFullPath(repositoryRoot);
            if (ContainsReparsePointInExistingAncestry(current))
            {
                return true;
            }

            string relative = Path.GetRelativePath(current, target);
            foreach (string segment in relative.Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, segment);
                if (Directory.Exists(current)
                    && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsReparsePointInExistingAncestry(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(current)
                    && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }

                DirectoryInfo parent = Directory.GetParent(current);
                if (parent == null)
                {
                    break;
                }

                current = parent.FullName;
            }

            return false;
        }
    }
}
