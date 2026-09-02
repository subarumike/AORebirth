namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.IO;

    internal static class RepositoryRootResolver
    {
        internal static string Resolve()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo cursor = new DirectoryInfo(Path.GetFullPath(baseDirectory));
            while (cursor != null)
            {
                if (Directory.Exists(Path.Combine(cursor.FullName, "AORebirth"))
                    && Directory.Exists(Path.Combine(cursor.FullName, "Tools")))
                {
                    return cursor.FullName;
                }

                cursor = cursor.Parent;
            }

            return Directory.GetCurrentDirectory();
        }
    }
}
