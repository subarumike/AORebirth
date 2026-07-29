using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AORebirth.Communication.ISComV2Client;
using AORebirth.Communication.Messages;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Core.Nanos;
using AORebirth.Database;
using AORebirth.Interfaces;
using Cell.Core;
using NBug;
using NBug.Properties;
using NLog;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using Utility.Config;
using ZoneEngine.Core;
using ZoneEngine.Core.Arete;
using ZoneEngine.Core.Functions;
using ZoneEngine.Core.Missions;
using ZoneEngine.Core.Playfields;
using ZoneEngine.Script;
using locales;

namespace ZoneEngine;

internal class Program
{
	public static ISComV2Client ISComClient;

	public static ZoneServer zoneServer;

	private static readonly ServerConsoleCommands consoleCommands = new ServerConsoleCommands();

	private static bool exited = false;

	private static StreamWriter headlessErrorWriter;

	private static StreamWriter headlessOutputWriter;

	private static void CheckDatabase(string[] parts)
	{
		Misc.CheckDatabase();
	}

	private static bool CheckZoneServerCreation()
	{
		try
		{
			zoneServer = new ZoneServer();
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex);
			return false;
		}
		return true;
	}

	private static string GetArgumentValue(string[] args, string argument)
	{
		for (int i = 0; i < args.Length - 1; i++)
		{
			if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
			{
				return args[i + 1];
			}
		}
		return null;
	}

	private static bool HasArgument(string[] args, string argument)
	{
		foreach (string a in args)
		{
			if (string.Equals(a, argument, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private static void ConfigureHeadlessConsoleLogging(string[] args)
	{
		string argumentValue = GetArgumentValue(args, "/stdout-log");
		if (!string.IsNullOrWhiteSpace(argumentValue))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(argumentValue));
			headlessOutputWriter = new StreamWriter(new FileStream(argumentValue, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
			headlessOutputWriter.AutoFlush = true;
			Console.SetOut(headlessOutputWriter);
		}
		string argumentValue2 = GetArgumentValue(args, "/stderr-log");
		if (!string.IsNullOrWhiteSpace(argumentValue2))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(argumentValue2));
			headlessErrorWriter = new StreamWriter(new FileStream(argumentValue2, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
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
		Thread thread = new Thread((ThreadStart)delegate
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
		thread.IsBackground = true;
		thread.Start();
	}

	private static void RunHeadless(string[] args)
	{
		Console.WriteLine("Starting ZoneEngine in headless mode.");
		StartTheServer();
		string argumentValue = GetArgumentValue(args, "/shutdown-file");
		while (!exited)
		{
			if (!string.IsNullOrWhiteSpace(argumentValue) && File.Exists(argumentValue))
			{
				Console.WriteLine("Headless shutdown requested.");
				ShutDownServer(null);
				FlushHeadlessConsoleLogging();
				Environment.Exit(0);
			}
			Thread.Sleep(1000);
		}
	}

	private static void CommandLoop(string[] args)
	{
		bool flag = false;
		Console.WriteLine(locales.ZoneEngineConsoleCommands);
		while (!exited)
		{
			if (!flag)
			{
				if (HasArgument(args, "/autostart"))
				{
					Console.WriteLine(locales.ServerConsoleAutostart);
					StartTheServer();
				}
				flag = true;
			}
			string text = Console.ReadLine();
			if (text != null)
			{
				if (!consoleCommands.Execute(text))
				{
					ShowCommandHelp();
				}
			}
			else
			{
				Thread.Sleep(1000);
			}
		}
	}

	private static void ConsoleCancelKeyPress(object sender, ConsoleCancelEventArgs e)
	{
		if (zoneServer != null)
		{
			exited = true;
			ISComClient.ShutDown();
			zoneServer.DisconnectAllClients();
			LogUtil.Debug((DebugInfoDetail)128, "Shutting down ZoneEngine hard");
		}
	}

	private static void ISComClientOnReceiveData(object sender, DynamicMessage messageobject)
	{
		zoneServer.ProcessISComMessage(messageobject);
	}

	private static bool ISComInitialization()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		IPAddress iPAddress;
		int commPort;
		try
		{
			ISComClient = new ISComV2Client();
			string chatIP = ConfigReadWrite.Instance.CurrentConfig.ChatIP;
			iPAddress = IPAddress.Parse(chatIP);
			commPort = ConfigReadWrite.Instance.CurrentConfig.CommPort;
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex);
			return false;
		}
		try
		{
			ISComClient.OnReceiveData += new OnReceiveDataHandler(ISComClientOnReceiveData);
			ISComClient.Connect(iPAddress, commPort);
		}
		catch (Exception ex2)
		{
			LogUtil.ErrorException(ex2);
			return true;
		}
		return true;
	}

	private static bool Initialize()
	{
		Console.WriteLine();
		Colouring.Push(ConsoleColor.Green);
		if (!InitializeGameFunctions())
		{
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine(locales.ErrorInitializingGamefunctions);
			Colouring.Pop();
			Colouring.Pop();
			return false;
		}
		if (!InitializeLogAndBug())
		{
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine(locales.ErrorInitializingNLogNBug);
			Colouring.Pop();
			Colouring.Pop();
			return false;
		}
		if (!CheckZoneServerCreation())
		{
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine(locales.ErrorCreatingZoneServerInstance);
			Colouring.Pop();
			Colouring.Pop();
			return false;
		}
		if (!ISComInitialization())
		{
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine(locales.ErrorInitializingISCom);
			Colouring.Pop();
			Colouring.Pop();
			return false;
		}
		if (!InizializeTCPIP())
		{
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine(locales.ErrorTCPIPSetup);
			Colouring.Pop();
			Colouring.Pop();
			return false;
		}
		if (!Misc.CheckDatabase())
		{
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine(locales.ErrorInitializingDatabase);
			Colouring.Pop();
			Colouring.Pop();
			return false;
		}
		try
		{
			AreteFrameworkRegistries registries = AreteFrameworkBootstrap.InitializeCheckedInContent();
			MissionRuntime.Initialize(registries);
		}
		catch (Exception ex)
		{
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine("Persistent mission initialization failed: " + ex.Message);
			Colouring.Pop();
			Colouring.Pop();
			return false;
		}
		Misc.LogOffAll();
		Colouring.Push(ConsoleColor.Green);
		if (!LoadItemsAndNanos())
		{
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine(locales.ErrorLoadingItemsNanos);
			Colouring.Pop();
			Colouring.Pop();
			return false;
		}
		Colouring.Pop();
		Colouring.Push(ConsoleColor.Green);
		if (!LoadTradeSkills())
		{
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine("No locale yet: Error reading trade skills");
			Colouring.Pop();
			Colouring.Pop();
			return false;
		}
		Colouring.Pop();
		if (!InitializeConsoleCommands())
		{
			return false;
		}
		Colouring.Pop();
		return true;
	}

	private static bool InitializeConsoleCommands()
	{
		consoleCommands.Engine = "Zone";
		consoleCommands.AddEntry("start", (Action<string[]>)StartServer);
		consoleCommands.AddEntry("startm", (Action<string[]>)StartServerMultipleScriptDlls);
		consoleCommands.AddEntry("running", (Action<string[]>)IsServerRunning);
		consoleCommands.AddEntry("ping", (Action<string[]>)PingChatServer);
		consoleCommands.AddEntry("stop", (Action<string[]>)StopServer);
		consoleCommands.AddEntry("exit", (Action<string[]>)ShutDownServer);
		consoleCommands.AddEntry("quit", (Action<string[]>)ShutDownServer);
		consoleCommands.AddEntry("check", (Action<string[]>)CheckDatabase);
		consoleCommands.AddEntry("updatedb", (Action<string[]>)CheckDatabase);
		consoleCommands.AddEntry("online", (Action<string[]>)ShowOnlineCharacters);
		consoleCommands.AddEntry("ls", (Action<string[]>)ListAvailableScripts);
		consoleCommands.AddEntry("debug", (Action<string[]>)SetDebug);
		return true;
	}

	private static void SetDebug(string[] obj)
	{
		if (obj.Length == 1)
		{
			LogUtil.Toggle("");
			return;
		}
		for (int i = 1; i < obj.Length; i++)
		{
			LogUtil.Toggle(obj[i]);
		}
	}

	private static bool InitializeGameFunctions()
	{
		try
		{
			Colouring.Push(ConsoleColor.Green);
			Console.WriteLine("{0} Game functions loaded", FunctionCollection.Instance.NumberofRegisteredFunctions());
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex);
			Colouring.Pop();
			return false;
		}
		Colouring.Pop();
		return true;
	}

	private static bool InitializeLogAndBug()
	{
		try
		{
			LogUtil.SetupConsoleLogging(LogLevel.Debug);
			LogUtil.SetupFileLogging("${basedir}/ZoneEngineLog.txt", LogLevel.Trace);
			SettingsOverride.LoadCustomSettings("NBug.ZoneEngine.config");
			Settings.WriteLogToDisk = true;
			AppDomain.CurrentDomain.UnhandledException += Handler.UnhandledException;
			TaskScheduler.UnobservedTaskException += Handler.UnobservedTaskException;
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex);
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine(locales.ErrorInitializingNLogNBug);
			Console.WriteLine(ex.Message);
			Colouring.Pop();
			return false;
		}
		return true;
	}

	private static bool InizializeTCPIP()
	{
		int port = Convert.ToInt32(ConfigReadWrite.Instance.CurrentConfig.ZonePort);
		try
		{
			if (ConfigReadWrite.Instance.CurrentConfig.ListenIP == "0.0.0.0")
			{
				((ServerBase)zoneServer).TcpEndPoint = new IPEndPoint(IPAddress.Any, port);
			}
			else
			{
				((ServerBase)zoneServer).TcpEndPoint = new IPEndPoint(IPAddress.Parse(ConfigReadWrite.Instance.CurrentConfig.ListenIP), port);
			}
			((ServerBase)zoneServer).MaximumPendingConnections = 100;
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex);
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine(locales.ErrorIPAddressParseFailed);
			Console.Write(ex.Message);
			Colouring.Pop();
			Console.ReadKey();
			return false;
		}
		return true;
	}

	private static void IsServerRunning(string[] parts)
	{
		Colouring.Push(ConsoleColor.White);
		if (((ServerBase)zoneServer).IsRunning)
		{
			Console.WriteLine(locales.ServerConsoleServerIsRunning);
		}
		else
		{
			Console.WriteLine(locales.ServerConsoleServerIsNotRunning);
		}
		Colouring.Pop();
	}

	private static void ListAvailableScripts(string[] parts)
	{
		Colouring.Push(ConsoleColor.White);
		Console.WriteLine(locales.ServerConsoleAvailableScripts + ":");
		string[] files = Directory.GetFiles("Scripts" + Path.DirectorySeparatorChar, "*.cs", SearchOption.AllDirectories);
		if (files.Length == 0)
		{
			Console.WriteLine(locales.ServerConsoleNoScriptsFound);
			return;
		}
		Colouring.Push(ConsoleColor.Green);
		string[] array = files;
		foreach (string value in array)
		{
			Console.WriteLine(value);
		}
		Colouring.Pop();
	}

	private static bool LoadItemsAndNanos()
	{
		Colouring.Push(ConsoleColor.Green);
		try
		{
			Console.WriteLine(locales.ItemLoaderLoadedItems, ItemLoader.CacheAllItems());
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex);
			Colouring.Pop();
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine(locales.ErrorReadingItemsFile);
			Console.WriteLine(ex.Message);
			Colouring.Pop();
			return false;
		}
		Colouring.Pop();
		Colouring.Push(ConsoleColor.Green);
		try
		{
			Console.WriteLine(locales.NanoLoaderLoadedNanos, NanoLoader.CacheAllNanos());
			Console.WriteLine();
		}
		catch (Exception ex2)
		{
			LogUtil.ErrorException(ex2);
			Colouring.Pop();
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine(locales.ErrorReadingNanosFile);
			Console.WriteLine(ex2.Message);
			Colouring.Pop();
			return false;
		}
		Colouring.Pop();
		Colouring.Push(ConsoleColor.Green);
		try
		{
			Console.WriteLine("Loaded {0} Playfields", PlayfieldLoader.CacheAllPlayfieldData());
			Console.WriteLine();
		}
		catch (Exception ex3)
		{
			LogUtil.ErrorException(ex3);
			Colouring.Pop();
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine("Error reading statels.dat");
			Console.WriteLine(ex3.Message);
			Colouring.Pop();
			return false;
		}
		Colouring.Pop();
		return true;
	}

	private static bool LoadTradeSkills()
	{
		try
		{
			int count = TradeSkill.Instance.ItemNames.Count;
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex);
			return false;
		}
		return true;
	}

	private static void Main(string[] args)
	{
		bool flag = HasArgument(args, "/headless");
		if (flag)
		{
			ConfigureHeadlessConsoleLogging(args);
		}
		Console.CancelKeyPress += ConsoleCancelKeyPress;
		OnScreenBanner.PrintAORebirthBanner(ConsoleColor.Green);
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
			if (flag)
			{
				RunHeadless(args);
				LogManager.Configuration = null;
				FlushHeadlessConsoleLogging();
				return;
			}
			StartShutdownFileWatcher(args);
			StartTheServer();
			CommandLoop(args);
		}
		LogManager.Configuration = null;
	}

	private static void PingChatServer(string[] parts)
	{
		Console.WriteLine("Ping is disabled till we can do it");
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

	private static void ShowOnlineCharacters(string[] parts)
	{
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		if (!((ServerBase)zoneServer).IsRunning)
		{
			return;
		}
		Colouring.Push(ConsoleColor.White);
		lock (zoneServer.Clients)
		{
			foreach (ZoneClient client in zoneServer.Clients)
			{
				string name = ((INamedEntity)client.Controller.Character).Name;
				Identity identity = ((IEntity)((IInstancedEntity)client.Controller.Character).Playfield).Identity;
				Console.WriteLine("Character " + name + " online in PF " + ((Identity)(ref identity)).Instance);
			}
		}
		Colouring.Pop();
	}

	private static void ShutDownServer(string[] parts)
	{
		if (((ServerBase)zoneServer).IsRunning)
		{
			((ServerBase)zoneServer).Stop();
		}
		ISComClient.ShutDown();
		exited = true;
	}

	private static void StartServer(string[] parts)
	{
		if (((ServerBase)zoneServer).IsRunning)
		{
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine(locales.ServerConsoleServerIsRunning);
			Colouring.Pop();
		}
		else
		{
			StartTheServer();
		}
	}

	private static void StartServerMultipleScriptDlls(string[] parts)
	{
		if (((ServerBase)zoneServer).IsRunning)
		{
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine(locales.ServerConsoleServerIsRunning);
			Colouring.Pop();
		}
		else
		{
			StartTheServer();
		}
	}

	private static void StartTheServer()
	{
		ScriptCompiler.Instance.Compile(multipleFiles: true);
		Console.WriteLine(ScriptCompiler.Instance.AddScriptMembers() + " chat commands loaded");
		((ServerBase)zoneServer).Start(true, false);
	}

	private static void StopServer(string[] parts)
	{
		if (!((ServerBase)zoneServer).IsRunning)
		{
			Colouring.Push(ConsoleColor.Red);
			Console.WriteLine(locales.ServerConsoleServerIsNotRunning);
			Colouring.Pop();
		}
		else
		{
			((ServerBase)zoneServer).Stop();
		}
	}
}
