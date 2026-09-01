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

namespace Utility.Config
{
    #region Usings ...

    using System;
    using System.IO;
    using System.Text;
    using System.Xml.Serialization;

    #endregion

    /// <summary>
    /// 
    /// </summary>
    public class ConfigReadWrite
    {
        #region Static Fields

        /// <summary>
        /// </summary>
        private static ConfigReadWrite _instance;

        #endregion

        #region Fields

        /// <summary>
        /// </summary>
        private Config _config;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// </summary>
        private ConfigReadWrite()
        {
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// 
        /// </summary>
        public static ConfigReadWrite Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ConfigReadWrite();
                }

                return _instance;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Config CurrentConfig
        {
            get
            {
#if AOREBIRTH_LINUX
                if (this._config == null)
                {
                    this._config = LoadConfig();
                }
#else
                try
                {
                    if (this._config == null)
                    {
                        this._config = LoadConfig();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error parsing configuration: {0}", ex.Message);
                    this._config = new Config();
                }
#endif

                return this._config;
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// Absolute or relative path to the configuration file that will be loaded.
        /// </summary>
        public static string ResolvedConfigPath
        {
            get
            {
                return GetConfigPath();
            }
        }

        /// <summary>
        /// Saves the current config back to the file
        /// </summary>
        /// <returns>true, if successful</returns>
        public bool SaveConfig()
        {
            if (this._config == null)
            {
                return false;
            }

            try
            {
                XmlSerializer ser = new XmlSerializer(typeof(Config));
#if AOREBIRTH_LINUX
                using (FileStream stream = File.Create(GetConfigPath()))
                {
                    ser.Serialize(stream, this._config);
                }
#else
                MemoryStream ms = new MemoryStream();
                ser.Serialize(ms, this._config);
                File.WriteAllText(GetConfigPath(), Encoding.UTF8.GetString(ms.GetBuffer()));
#endif
            }
            catch
            {
                return false;
            }

            return true;
        }

        private static string GetConfigPath()
        {
            string configuredPath = Environment.GetEnvironmentVariable("AO_REBIRTH_CONFIG_PATH");
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }

            string baseDirectoryConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.xml");
            if (File.Exists(baseDirectoryConfig))
            {
                return baseDirectoryConfig;
            }

            return "Config.xml";
        }

        private static Config LoadConfig()
        {
            Config config =
                (Config)
                    new XmlSerializer(typeof(Config)).Deserialize(
                        new MemoryStream(File.ReadAllBytes(GetConfigPath())));

            string mysqlConnection = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
#if AOREBIRTH_LINUX
            string requiredSqlType = Environment.GetEnvironmentVariable("AO_REBIRTH_REQUIRED_SQL_TYPE");
            if (string.Equals(requiredSqlType, "MySql", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(config.MysqlConnection)
                && config.MysqlConnection.IndexOf(
                    "REPLACE_WITH_",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidDataException(
                    "Config.xml must contain only a placeholder MySQL connection for the Linux deployment profile.");
            }
#endif
            if (!string.IsNullOrWhiteSpace(mysqlConnection))
            {
                config.MysqlConnection = mysqlConnection;
            }

            return config;
        }

        #endregion
    }
}
