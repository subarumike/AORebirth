namespace ZoneEngine.Core.Arete
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Web.Script.Serialization;

    #endregion

    public static class AreteJsonContentFileLoader
    {
        public static AreteContentLoadResult<TPack> LoadDirectory<TPack>(
            string directoryPath,
            string contentType,
            Func<IEnumerable<TPack>, AreteValidationResult> validate)
        {
            var validation = new AreteValidationResult();
            string location = GetDirectoryLocation(contentType, directoryPath);

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                validation.AddError(location, "missing JSON content directory path");
                return new AreteContentLoadResult<TPack>(Enumerable.Empty<TPack>(), validation);
            }

            if (!Directory.Exists(directoryPath))
            {
                validation.AddError(location, "JSON content directory was not found");
                return new AreteContentLoadResult<TPack>(Enumerable.Empty<TPack>(), validation);
            }

            string[] filePaths = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (filePaths.Length == 0)
            {
                validation.AddError(location, "JSON content directory did not contain JSON content files");
                return new AreteContentLoadResult<TPack>(Enumerable.Empty<TPack>(), validation);
            }

            return LoadFiles(filePaths, contentType, validate);
        }

        public static AreteContentLoadResult<TPack> LoadFiles<TPack>(
            IEnumerable<string> filePaths,
            string contentType,
            Func<IEnumerable<TPack>, AreteValidationResult> validate)
        {
            var loadedPacks = new List<TPack>();
            var validation = new AreteValidationResult();
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            int fileIndex = 0;

            foreach (string filePath in filePaths ?? Enumerable.Empty<string>())
            {
                string location = GetLocation(contentType, filePath, fileIndex);
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    validation.AddError(location, "missing JSON content file path");
                    fileIndex++;
                    continue;
                }

                if (!File.Exists(filePath))
                {
                    validation.AddError(location, "JSON content file was not found");
                    fileIndex++;
                    continue;
                }

                try
                {
                    string json = File.ReadAllText(filePath);
                    TPack pack = serializer.Deserialize<TPack>(json);
                    if (pack == null)
                    {
                        validation.AddError(location, "JSON content file did not contain a content pack");
                    }
                    else
                    {
                        loadedPacks.Add(pack);
                    }
                }
                catch (Exception exception)
                {
                    validation.AddError(
                        location,
                        "failed to parse JSON content file: " + exception.GetType().Name + ": " + exception.Message);
                }

                fileIndex++;
            }

            if (validate != null)
            {
                validation.AddErrors(validate(loadedPacks));
            }

            return new AreteContentLoadResult<TPack>(loadedPacks, validation);
        }

        private static string GetDirectoryLocation(string contentType, string directoryPath)
        {
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                return directoryPath;
            }

            if (string.IsNullOrWhiteSpace(contentType))
            {
                contentType = "content";
            }

            return contentType + "Directory";
        }

        private static string GetLocation(string contentType, string filePath, int fileIndex)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                return filePath;
            }

            if (string.IsNullOrWhiteSpace(contentType))
            {
                contentType = "content";
            }

            return contentType + "File[" + fileIndex + "]";
        }
    }
}
