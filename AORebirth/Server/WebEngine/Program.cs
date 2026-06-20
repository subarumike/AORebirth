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

#region Usings ...

using Config = Utility.Config.ConfigReadWrite;

#endregion

namespace WebEngine
{
    #region Usings ...

    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using locales;

    using NBug;
    using NBug.Properties;

    using NLog;

    using Utility;

    #endregion

    /// <summary>
    /// </summary>
    internal class Program
    {
        #region Static Fields

        /// <summary>
        /// </summary>
        public static bool DebugNetwork;

        /// <summary>
        /// </summary>
        private static readonly ServerConsoleCommands consoleCommands = new ServerConsoleCommands();

        /// <summary>
        /// </summary>
        private static bool exited = false;

        private static StreamWriter headlessErrorWriter;

        private static StreamWriter headlessOutputWriter;

        /// <summary>
        /// </summary>
        private static HttpServer myServer;

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        public static void StartTheServer()
        {
            myServer.StartServer();
            Console.WriteLine(locales.ServerConsoleServerIsRunning);
        }

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool CheckDatabase()
        {
            bool result = true;
            try
            {
                // LoginDataDao.GetAll();
                // TODO: Add code to load WebCore DB
            }
            catch (Exception)
            {
                result = false;
            }

            return result;
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
            Console.WriteLine("Starting WebEngine in headless mode.");
            StartTheServer();

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
                        StartTheServer();
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

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool Initialize()
        {
            Console.WriteLine();
            Colouring.Push(ConsoleColor.Green);

            if (!InitializeLogAndBug())
            {
                Colouring.Push(ConsoleColor.Red);

                Console.WriteLine(locales.ErrorInitializingNLogNBug);
                Colouring.Pop();
                Colouring.Pop();
                return false;
            }

            if (!InitializeServerInstance())
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine("Error initializing WebServer.");

                Console.WriteLine(locales.ErrorInitializingEngine);
                Colouring.Pop();
                Colouring.Pop();
                return false;
            }

            if (!CheckDatabase())
            {
                Colouring.Push(ConsoleColor.Red);

                Console.WriteLine(locales.ErrorInitializingDatabase);
                Colouring.Pop();
                Colouring.Pop();
                return false;
            }

            if (!InitializeConsoleCommands())
            {
                Colouring.Pop();
                return false;
            }

            Colouring.Pop();
            return true;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private static bool InitializeConsoleCommands()
        {
            consoleCommands.Engine = "Web";

            consoleCommands.AddEntry("start", StartServer);
            consoleCommands.AddEntry("running", IsServerRunning);
            consoleCommands.AddEntry("stop", StopServer);
            consoleCommands.AddEntry("exit", ShutDownServer);
            consoleCommands.AddEntry("quit", ShutDownServer);
            consoleCommands.AddEntry("debugnetwork", SetDebugNetwork);

            consoleCommands.AddEntry("checkphp", CheckPphp);
            consoleCommands.AddEntry("checkWebCore", CheckWebCore);
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
                LogUtil.SetupFileLogging("${basedir}/WebEngineLog.txt", LogLevel.Trace);

                // NBug initialization
                SettingsOverride.LoadCustomSettings("NBug.WebEngine.config");
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
        private static bool InitializeServerInstance()
        {
            try
            {
                myServer = default(HttpServer);
                myServer = new HttpServer();
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="obj">
        /// </param>
        private static void IsServerRunning(string[] obj)
        {
            // TODO: Write Code to check if Server is Running

            if (myServer.isRunning)
            {
                Console.WriteLine(locales.ServerConsoleServerIsRunning);
            }
            else
            {
                Console.WriteLine(locales.ServerConsoleServerIsNotRunning);
            }

            // Colouring.Pop();
        }

        /// <summary>
        /// </summary>
        /// <param name="args">
        /// </param>
        private static void Main(string[] args)
        {
            bool headless = HasArgument(args, "/headless");
            if (headless)
            {
                ConfigureHeadlessConsoleLogging(args);
            }

            OnScreenBanner.PrintAORebirthBanner(ConsoleColor.Red);

            Console.WriteLine();

            Console.WriteLine(locales.ServerConsoleMainText, DateTime.Now.Year);

            if (!Initialize())
            {
                Console.WriteLine(locales.ErrorInitializingEngine);
                Console.WriteLine("Press enter to exit");
                Console.ReadLine();
            }
            else
            {
                if (headless)
                {
                    RunHeadless(args);
                    LogManager.Configuration = null;
                    FlushHeadlessConsoleLogging();
                    return;
                }

                StartShutdownFileWatcher(args);
#if DEBUG

                // Commented out to check if start stop works
                // StartTheServer();
#endif
                CommandLoop(args);
            }

            // NLog<->Mono lockup fix
            LogManager.Configuration = null;
        }

        /// <summary>
        /// </summary>
        /// <param name="obj">
        /// </param>
        private static void SetDebugNetwork(string[] obj)
        {
            DebugNetwork = !DebugNetwork;
            Colouring.Push(ConsoleColor.Green);
            if (DebugNetwork)
            {
                Console.WriteLine("Debugging of network traffic enabled");
            }
            else
            {
                Console.WriteLine("Debugging of network traffic disabled");
            }

            Colouring.Pop();
        }

        /// <summary>
        /// </summary>
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
        /// <param name="obj">
        /// </param>
        private static void ShutDownServer(string[] obj)
        {
            if (myServer.isRunning)
            {
                myServer.StopServer();
            }

            Console.WriteLine(locales.ServerConsoleServerIsNotRunning);
            exited = true;
        }

        /// <summary>
        /// </summary>
        /// <param name="parts">
        /// </param>
        private static void StartServer(string[] parts)
        {
            if (myServer.isRunning)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine("WebServer is running already.");

                Console.WriteLine(locales.ServerConsoleServerIsRunning);
                Colouring.Pop();
            }
            else
            {
                StartTheServer();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        private static void CheckPphp(string[] obj)
        {
            var _checks = new Checks();
            _checks.CheckPhp();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        private static void CheckWebCore(string[] obj)
        {
            var _checks = new Checks();
            _checks.CheckWebCore();
        }

        /// <summary>
        /// </summary>
        /// <param name="obj">
        /// </param>
        private static void StopServer(string[] obj)
        {
            if (!myServer.isRunning)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine(locales.ServerConsoleServerIsNotRunning);
                Colouring.Pop();
            }
            else
            {
                myServer.StopServer();
            }
        }

        #endregion
    }
}
