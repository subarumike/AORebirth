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

namespace WebEngine
{
    #region Usings ...

    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Net;

    using _config = Utility.Config.ConfigReadWrite;

    #endregion

    public class Checks
    {
        public void CheckWebCore()
        {
            if (Directory.Exists(_config.Instance.CurrentConfig.WebHostRoot) == false)
            {
                var url = new WebClient();
                Console.WriteLine("Downloading WebCore...");
                url.DownloadFile(_config.Instance.CurrentConfig.WebCoreRepo, "WebCore.zip");
                Console.WriteLine("Download Complete.");
                Console.WriteLine();
                Console.WriteLine("Unzipping File...");
                this.Unzip2("WebCore.zip");
                string[] coreDirectories = Directory.GetDirectories(_config.Instance.CurrentConfig.WebHostRoot);

                foreach (string coreDirectory in coreDirectories)
                {
                    string[] files = Directory.GetFiles(coreDirectory);
                    // Copy the files and overwrite destination files if they already exist. 
                    foreach (string s in files)
                    {
                        // Use static Path methods to extract only the file name from the path.
                        string fileName = Path.GetFileName(s);
                        string destFile = Path.Combine(_config.Instance.CurrentConfig.WebHostRoot, fileName);
                        File.Move(s, destFile);
                    }

                    files = Directory.GetDirectories(coreDirectory);
                    // Copy the files and overwrite destination files if they already exist. 
                    foreach (string s in files)
                    {
                        // Use static Path methods to extract only the file name from the path.
                        string fileName = Path.GetFileName(s);
                        string destFile = Path.Combine(_config.Instance.CurrentConfig.WebHostRoot, fileName);
                        Directory.Move(s, destFile);
                    }
                    Directory.Delete(coreDirectory);
                }
            }
            else
            {
                Console.WriteLine("Webcore Exists.");
            }
        }

        private void Unzip2(string file)
        {
            ExtractArchive(file, _config.Instance.CurrentConfig.WebHostRoot);
            Console.WriteLine("Done.");
        }

        private static void ExtractArchive(string file, string destinationDirectory)
        {
            string destinationRoot = Path.GetFullPath(destinationDirectory);
            if (!destinationRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                destinationRoot += Path.DirectorySeparatorChar;
            }

            Directory.CreateDirectory(destinationRoot);

            using (ZipArchive archive = ZipFile.OpenRead(file))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
                    if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Archive entry escapes the configured extraction directory.");
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    string parentDirectory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(parentDirectory))
                    {
                        Directory.CreateDirectory(parentDirectory);
                    }

                    entry.ExtractToFile(destinationPath, true);
                }
            }
        }
    }
}
