using System;
using System.IO;

namespace AORebirth.NpcEditor.Official;

internal static class OfficialLocalPaths
{
    internal static string ResearchDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AORebirth", "NpcEditor");

    internal static string RequireLocalDirectory(string path)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new IOException("The research folder requires a fully qualified local path.");
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath)!;
        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal) || fullPath.StartsWith("//", StringComparison.Ordinal)
            || (OperatingSystem.IsWindows() && new DriveInfo(root).DriveType == DriveType.Network))
            throw new IOException("The research folder requires a local drive.");

        for (DirectoryInfo? directory = new(fullPath); directory != null; directory = directory.Parent)
            RejectLink(directory.FullName);
        return fullPath;
    }

    internal static void RejectLink(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Research files require paths without symbolic links or junctions.");
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
