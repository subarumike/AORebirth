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

namespace ChatEngine
{
    #region Usings ...

    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    using AORebirth.Communication.ISComV2Server;

    using ChatEngine.CoreServer;

    using locales;

    using NBug;
    using NBug.Properties;

    using NLog;

    using Utility;

    using ZoneEngine.Core.Playfields;

    using Config = Utility.Config.ConfigReadWrite;

    #endregion

    /// <summary>
    /// Program class for ChatEngine
    /// </summary>
    internal class Program
    {
        #region Static Fields

        /// <summary>
        /// </summary>
        public static ISComV2Server ISCom;

        /// <summary>
        /// </summary>
        private static ChatServer chatServer;

        /// <summary>
        /// </summary>
        private static ConsoleText ct = new ConsoleText();

        /// <summary>
        /// </summary>
        private static readonly ServerConsoleCommands consoleCommands = new ServerConsoleCommands();

        private const bool TcpEnable = true;

        private const bool UdpEnable = false;

        private static bool exited = false;

        private static StreamWriter headlessErrorWriter;

        private static StreamWriter headlessOutputWriter;

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool InitializeConsoleCommands()
        {
            consoleCommands.Engine = "Chat";
            consoleCommands.AddEntry("start", StartServer);
            consoleCommands.AddEntry("running", IsServerRunning);
            consoleCommands.AddEntry("stop", StopServer);
            consoleCommands.AddEntry("exit", ShutDownServer);
            consoleCommands.AddEntry("quit", ShutDownServer);
            consoleCommands.AddEntry("debug", SetDebug);
            return true;
        }

        private static void SetDebug(string[] obj)
        {
            {
                if (obj.Length == 1)
                {
                    LogUtil.Toggle("");
                }
                else
                {
                    for (int i = 1; i < obj.Length; i++)
                    {
                        LogUtil.Toggle(obj[i]);
                    }
                }
            }
        }

        private static void IsServerRunning(string[] obj)
        {
            Colouring.Push(ConsoleColor.White);
            if (chatServer.IsRunning)
            {
                Console.WriteLine(locales.ServerConsoleServerIsRunning);
            }
            else
            {
                Console.WriteLine(locales.ServerConsoleServerIsNotRunning);
            }

            Colouring.Pop();
        }

        private static void ShowCommandHelp()
        {
            Colouring.Push(ConsoleColor.White);
            Console.WriteLine(locales.ServerConsoleAvailableCommands);
            Console.WriteLine("---------------------------");
            Console.WriteLine(consoleCommands.HelpAll());
            Console.WriteLine("---------------------------");
            Console.WriteLine();
            Colouring.Pop();
        }

        /// <summary>
        /// </summary>
        /// <param name="args">
        /// </param>
        private static string GetArgumentValue(string[] args, string argument)
        {
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], argument, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        private static bool HasArgument(string[] args, string argument)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ConfigureHeadlessConsoleLogging(string[] args)
        {
            string stdoutLog = GetArgumentValue(args, "/stdout-log");
            if (!string.IsNullOrWhiteSpace(stdoutLog))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(stdoutLog));
                headlessOutputWriter = new StreamWriter(
                    new FileStream(stdoutLog, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
                headlessOutputWriter.AutoFlush = true;
                Console.SetOut(headlessOutputWriter);
            }

            string stderrLog = GetArgumentValue(args, "/stderr-log");
            if (!string.IsNullOrWhiteSpace(stderrLog))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(stderrLog));
                headlessErrorWriter = new StreamWriter(
                    new FileStream(stderrLog, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
                headlessErrorWriter.AutoFlush = true;
                Console.SetError(headlessErrorWriter);
            }
        }

        private static void FlushHeadlessConsoleLogging()
        {
            if (headlessOutputWriter != null)
            {
                headlessOutputWriter.Flush();
            }

            if (headlessErrorWriter != null)
            {
                headlessErrorWriter.Flush();
            }
        }

        private static void StartShutdownFileWatcher(string[] args)
        {
            string shutdownFile = GetArgumentValue(args, "/shutdown-file");
            if (string.IsNullOrWhiteSpace(shutdownFile))
            {
                return;
            }

            Thread shutdownThread = new Thread(
                () =>
                    {
                        while (!exited)
                        {
                            if (File.Exists(shutdownFile))
                            {
                                Console.WriteLine("Shutdown file requested.");
                                ShutDownServer(null);
                                FlushHeadlessConsoleLogging();
                                Environment.Exit(0);
                            }

                            Thread.Sleep(1000);
                        }
                    });

            shutdownThread.IsBackground = true;
            shutdownThread.Start();
        }

        private static void RunHeadless(string[] args)
        {
            Console.WriteLine("Starting ChatEngine in headless mode.");
            StartServer(null);

            string shutdownFile = GetArgumentValue(args, "/shutdown-file");
            while (!exited)
            {
                if (!string.IsNullOrWhiteSpace(shutdownFile) && File.Exists(shutdownFile))
                {
                    Console.WriteLine("Headless shutdown requested.");
                    ShutDownServer(null);
                    FlushHeadlessConsoleLogging();
                    Environment.Exit(0);
                }

                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="args">
        /// </param>
        private static void CommandLoop(string[] args)
        {
            bool processedargs = false;
            Console.WriteLine(locales.ZoneEngineConsoleCommands);

            while (!exited)
            {
                if (!processedargs)
                {
                    if (HasArgument(args, "/autostart"))
                    {
                        Console.WriteLine(locales.ServerConsoleAutostart);
                        StartServer(null);
                    }

                    processedargs = true;
                }

                Console.Write(Environment.NewLine + "{0} >>", locales.ServerConsoleCommand);
                string consoleCommand = Console.ReadLine();

                if (consoleCommand != null)
                {
                    if (!consoleCommands.Execute(consoleCommand))
                    {
                        ShowCommandHelp();
                    }
                }
            }
        }

        private static void ShutDownServer(string[] obj)
        {
            StopServer(null);
            exited = true;
        }

        private static void StopServer(string[] obj)
        {
            if (!chatServer.IsRunning)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ServerConsoleServerIsNotRunning);
                Colouring.Pop();
            }
            else
            {
                chatServer.Stop();
            }
        }

        private static void StartServer(string[] obj)
        {
            if (chatServer.IsRunning)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ServerConsoleServerIsRunning);
                Colouring.Pop();
            }

            chatServer.Start(TcpEnable, UdpEnable);
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool Initialize()
        {
            try
            {
                chatServer = new ChatServer();

                if (!InitializeLogAndBug())
                {
                    return false;
                }

                if (!InitializeTCP())
                {
                    return false;
                }

                if (!InitializeISCom())
                {
                    return false;
                }

                if (!InitializeConsoleCommands())
                {
                    return false;
                }

                PlayfieldLoader.CacheAllPlayfieldData();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                return false;
            }

            return true;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool InitializeISCom()
        {
            try
            {
                ISCom = new ISComV2Server();
                ISCom.DataReceived += chatServer.ISComDataReceived;
                if (Config.Instance.CurrentConfig.ListenIP == "0.0.0.0")
                {
                    ISCom.TcpEndPoint = new IPEndPoint(IPAddress.Any, Config.Instance.CurrentConfig.CommPort);
                }
                else
                {
                    ISCom.TcpEndPoint = new IPEndPoint(
                        IPAddress.Parse(Config.Instance.CurrentConfig.ListenIP),
                        Config.Instance.CurrentConfig.CommPort);
                }

                ISCom.Start(true, false);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool InitializeLogAndBug()
        {
            try
            {
                // Setup and enable NLog logging to file
                LogUtil.SetupConsoleLogging(LogLevel.Debug);
                LogUtil.SetupFileLogging("${basedir}/ChatEngineLog.txt", LogLevel.Trace);

                // NBug initialization
                SettingsOverride.LoadCustomSettings("NBug.ChatEngine.config");
                Settings.WriteLogToDisk = true;
                AppDomain.CurrentDomain.UnhandledException += Handler.UnhandledException;
                TaskScheduler.UnobservedTaskException += Handler.UnobservedTaskException;
            }
            catch (Exception e)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine("Error occured while initalizing NLog/NBug");
                Console.WriteLine(e.Message);
                Colouring.Pop();
                return false;
            }

            return true;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool InitializeTCP()
        {
            int Port = Convert.ToInt32(Config.Instance.CurrentConfig.ChatPort);
            try
            {
                if (Config.Instance.CurrentConfig.ListenIP == "0.0.0.0")
                {
                    chatServer.TcpEndPoint = new IPEndPoint(IPAddress.Any, Port);
                }
                else
                {
                    chatServer.TcpEndPoint = new IPEndPoint(
                        IPAddress.Parse(Config.Instance.CurrentConfig.ListenIP),
                        Port);
                }

                chatServer.MaximumPendingConnections = 100;
            }
            catch (Exception e)
            {
                Console.WriteLine(locales.ErrorIPAddressParseFailed);
                Console.Write(e.Message);
                Console.ReadKey();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Entry point
        /// </summary>
        /// <param name="args">
        /// Command line parameters
        /// </param>
        private static void Main(string[] args)
        {
            bool headless = HasArgument(args, "/headless");
            if (headless)
            {
                ConfigureHeadlessConsoleLogging(args);
            }

            ct = new ConsoleText();

            OnScreenBanner.PrintAORebirthBanner(ConsoleColor.Yellow);

            Console.WriteLine();

            Console.WriteLine(locales.ServerConsoleMainText, DateTime.Now.Year);

            if (!Initialize())
            {
                Console.WriteLine("Error occured while initilizing. Please check in log.");
                return;
            }

            if (headless)
            {
                RunHeadless(args);
                LogManager.Configuration = null;
                FlushHeadlessConsoleLogging();
                return;
            }

            StartShutdownFileWatcher(args);
            CommandLoop(args);
            LogManager.Configuration = null;
        }

        #endregion
    }
}
