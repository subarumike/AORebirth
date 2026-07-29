using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Enums;
using AORebirth.Stats;
using Microsoft.CSharp;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.ChatCommands;
using ZoneEngine.Core.KnuBot;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Script;

public class ScriptCompiler : IDisposable
{
	public static ScriptCompiler Instance = new ScriptCompiler();

	private readonly Dictionary<string, Type> chatCommands = new Dictionary<string, Type>();

	private readonly CodeDomProvider compiler = new CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v4.0" } });

	private readonly List<Assembly> multipleDllList = new List<Assembly>();

	private bool disposed = false;

	private readonly CompilerParameters p = new CompilerParameters
	{
		GenerateInMemory = false,
		GenerateExecutable = false,
		IncludeDebugInformation = true,
		OutputAssembly = "Scripts.dll",
		TreatWarningsAsErrors = false,
		WarningLevel = 3,
		CompilerOptions = "/optimize"
	};

	private readonly Dictionary<string, Type> scriptList = new Dictionary<string, Type>();

	public List<string> ChatCommands => chatCommands.Keys.ToList();

	private string[] ScriptsList { get; set; }

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	public static string DllName(string scriptName)
	{
		scriptName = RemoveCharactersAfterChar(scriptName, '.');
		scriptName = RemoveCharactersBeforeChar(scriptName, '\\');
		scriptName = RemoveCharactersBeforeChar(scriptName, '/');
		return scriptName + ".dll";
	}

	public static void LogScriptAction(string owner, ConsoleColor ownerColor, string message, ConsoleColor messageColor)
	{
		Colouring.Push(ownerColor);
		Console.Write(owner + " ");
		Colouring.Pop();
		Colouring.Push(messageColor);
		Console.Write(message + "\n");
		Colouring.Pop();
	}

	public static string RemoveCharactersAfterChar(string hayStack, char needle)
	{
		string text = hayStack;
		int num = text.IndexOf(needle);
		if (num > 0)
		{
			text = text.Substring(0, num);
		}
		return text;
	}

	public static string RemoveCharactersBeforeChar(string hayStack, char needle)
	{
		int num = hayStack.IndexOf(needle);
		if (num < 0)
		{
			return hayStack;
		}
		return hayStack.Substring(num + 1);
	}

	public int AddScriptMembers()
	{
		scriptList.Clear();
		foreach (Assembly multipleDll in multipleDllList)
		{
			Type[] types = multipleDll.GetTypes();
			foreach (Type type in types)
			{
				Type[] interfaces = type.GetInterfaces();
				foreach (Type type2 in interfaces)
				{
					if (!(type2.FullName == typeof(IAOScript).FullName) || !(type.Name != "IAOScript"))
					{
						continue;
					}
					MemberInfo[] members = type.GetMembers();
					foreach (MemberInfo memberInfo in members)
					{
						if (!(memberInfo.Name == "GetType") && !(memberInfo.Name == ".ctor") && !(memberInfo.Name == "GetHashCode") && !(memberInfo.Name == "ToString") && !(memberInfo.Name == "Equals") && memberInfo.MemberType == MemberTypes.Method && !scriptList.ContainsKey(type.Namespace + "." + type.Name + ":" + memberInfo.Name))
						{
							scriptList.Add(type.Namespace + "." + type.Name + ":" + memberInfo.Name, type);
						}
					}
				}
			}
		}
		chatCommands.Clear();
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		int num = 0;
		foreach (Type item in from x in executingAssembly.GetTypes()
			where x.IsSubclassOf(typeof(AOChatCommand))
			select x)
		{
			num++;
			AOChatCommand aOChatCommand = (AOChatCommand)executingAssembly.CreateInstance(item.FullName);
			List<string> list = aOChatCommand.ListCommands();
			foreach (string item2 in list)
			{
				chatCommands.Add(item.FullName + ":" + item2, item);
			}
		}
		return num;
	}

	public string ScriptExists(string scriptname)
	{
		string result = "";
		foreach (string key in scriptList.Keys)
		{
			if (key.Substring(key.IndexOf(":", StringComparison.Ordinal) + 1).ToLower() == scriptname.ToLower())
			{
				result = key.Substring(key.IndexOf(":", StringComparison.Ordinal) + 1);
				break;
			}
		}
		return result;
	}

	public string ClassExists(string scriptname)
	{
		string result = "";
		foreach (Assembly multipleDll in multipleDllList)
		{
			Type type = multipleDll.GetTypes().FirstOrDefault((Type x) => x.BaseType == typeof(BaseKnuBot) && x.Name.ToLower() == scriptname.ToLower());
			if (type != null)
			{
				result = type.Name;
				break;
			}
		}
		return result;
	}

	public void CallChatCommand(string commandName, IZoneClient client, Identity target, string[] commandArguments)
	{
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		if (commandName.ToUpperInvariant() != "LISTCOMMANDS")
		{
			foreach (KeyValuePair<string, Type> chatCommand in chatCommands)
			{
				if (chatCommand.Key.Substring(chatCommand.Key.IndexOf(":", StringComparison.Ordinal) + 1).ToUpperInvariant() == commandName.ToUpperInvariant())
				{
					AOChatCommand aOChatCommand = (AOChatCommand)executingAssembly.CreateInstance(chatCommand.Key.Substring(0, chatCommand.Key.IndexOf(":", StringComparison.Ordinal)));
					if (aOChatCommand != null)
					{
						if (((IStats)client.Controller.Character).Stats[(StatIds)215].Value < aOChatCommand.GMLevelNeeded() && aOChatCommand.GMLevelNeeded() > 0)
						{
							((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "You are not authorized to use this command!. This incident will be recorded.", 0, 0));
							break;
						}
						if (commandArguments.Length == 2 && commandArguments[1].ToUpperInvariant() == "HELP")
						{
							aOChatCommand.CommandHelp(client.Controller.Character);
							break;
						}
						if (aOChatCommand.CheckCommandArguments(commandArguments))
						{
							aOChatCommand.ExecuteCommand(client.Controller.Character, target, commandArguments);
						}
						else
						{
							aOChatCommand.CommandHelp(client.Controller.Character);
						}
					}
				}
			}
			return;
		}
		((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "Available Commands:", 0, 0));
		string[] array = chatCommands.Keys.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = array[i].Substring(array[i].IndexOf(":", StringComparison.Ordinal) + 1) + ":" + array[i].Substring(0, array[i].IndexOf(":", StringComparison.Ordinal));
		}
		Array.Sort(array);
		string[] array2 = array;
		foreach (string text in array2)
		{
			string typeName = text.Substring(text.IndexOf(":", StringComparison.Ordinal) + 1);
			AOChatCommand aOChatCommand2 = (AOChatCommand)executingAssembly.CreateInstance(typeName);
			if (aOChatCommand2 != null && ((IStats)client.Controller.Character).Stats[(StatIds)215].Value >= aOChatCommand2.GMLevelNeeded())
			{
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, text.Substring(0, text.IndexOf(":", StringComparison.Ordinal)), 0, 0));
			}
		}
	}

	public void CallMethod(string functionName, ICharacter character)
	{
		foreach (Assembly multipleDll in multipleDllList)
		{
			foreach (KeyValuePair<string, Type> script in scriptList)
			{
				if (script.Key.Substring(script.Key.IndexOf(":", StringComparison.Ordinal)) == ":" + functionName)
				{
					IAOScript iAOScript = (IAOScript)multipleDll.CreateInstance(script.Key.Substring(0, script.Key.IndexOf(":", StringComparison.Ordinal)));
					if (iAOScript != null)
					{
						script.Value.InvokeMember(functionName, BindingFlags.InvokeMethod, null, iAOScript, new object[1] { character }, CultureInfo.InvariantCulture);
					}
				}
			}
		}
	}

	public BaseKnuBot CreateKnuBot(string knuBotName, Identity mobId)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		foreach (Assembly multipleDll in multipleDllList)
		{
			Type type = multipleDll.GetTypes().FirstOrDefault((Type x) => x.BaseType == typeof(BaseKnuBot) && x.Name == knuBotName);
			if (type != null)
			{
				return (BaseKnuBot)Activator.CreateInstance(type, mobId);
			}
		}
		return null;
	}

	public bool Compile(bool multipleFiles)
	{
		if (!LoadFiles())
		{
			return false;
		}
		p.ReferencedAssemblies.Clear();
		multipleDllList.Clear();
		scriptList.Clear();
		chatCommands.Clear();
		foreach (Assembly item in from x in AppDomain.CurrentDomain.GetAssemblies()
			where !x.IsDynamic
			select x)
		{
			string location = item.Location;
			if (!string.IsNullOrEmpty(location) && !IsGeneratedScriptAssembly(location))
			{
				p.ReferencedAssemblies.Add(location);
			}
		}
		if (multipleFiles)
		{
			string path = Path.Combine("tmp", "run-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture));
			LogScriptAction("ScriptCompiler:", ConsoleColor.Yellow, "multiple scripts configuration active.", ConsoleColor.Magenta);
			string[] scriptsList = ScriptsList;
			foreach (string text in scriptsList)
			{
				string text2 = Path.Combine(path, DllName(text));
				p.OutputAssembly = string.Format(CultureInfo.CurrentCulture, text2);
				FileInfo fileInfo = new FileInfo(text2);
				if (fileInfo.Directory != null)
				{
					fileInfo.Directory.Create();
				}
				CompilerResults results = compiler.CompileAssemblyFromFile(p, text);
				if (ErrorReporting(results).Length != 0)
				{
					LogScriptAction("Error:", ConsoleColor.Yellow, ErrorReporting(results), ConsoleColor.Red);
					return false;
				}
				LogScriptAction("Script " + text, ConsoleColor.Green, "Compiled to: " + p.OutputAssembly, ConsoleColor.Green);
				multipleDllList.Add(Assembly.LoadFile(fileInfo.FullName));
			}
			foreach (Assembly multipleDll in multipleDllList)
			{
				RunScript(multipleDll);
			}
		}
		else
		{
			CompilerResults results2 = compiler.CompileAssemblyFromFile(p, ScriptsList);
			if (ErrorReporting(results2).Length != 0)
			{
				LogScriptAction("Error:", ConsoleColor.Yellow, ErrorReporting(results2), ConsoleColor.Red);
				return false;
			}
			try
			{
				FileInfo fileInfo2 = new FileInfo("Scripts.dll");
				Assembly assembly = Assembly.LoadFile(fileInfo2.FullName);
				multipleDllList.Add(assembly);
				RunScript(assembly);
			}
			catch (FileLoadException ex)
			{
				LogScriptAction("ERROR", ConsoleColor.Red, "File loading not successful:\r\n" + ex, ConsoleColor.Red);
				return false;
			}
			catch (FileNotFoundException ex2)
			{
				LogScriptAction("ERROR", ConsoleColor.Red, "Script not found:\r\n" + ex2, ConsoleColor.Red);
				return false;
			}
			catch (BadImageFormatException ex3)
			{
				LogScriptAction("ERROR", ConsoleColor.Red, "Bad image format:\r\n" + ex3, ConsoleColor.Red);
				return false;
			}
			AddScriptMembers();
		}
		return true;
	}

	private bool TryResolve(CompilerError e, string scriptFile)
	{
		bool result = false;
		string text = GetLineOfFile(scriptFile, e.Line).Replace("using", "").Replace(";", "").Trim();
		bool flag = true;
		while (flag)
		{
			if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, text + ".dll")))
			{
				p.ReferencedAssemblies.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, text + ".dll"));
				result = true;
				break;
			}
			flag = text.IndexOf(".") > -1;
			if (text.IndexOf(".") > -1)
			{
				text = text.Substring(0, text.LastIndexOf("."));
			}
		}
		return result;
	}

	private string GetLineOfFile(string scriptFile, int p)
	{
		string text = "";
		using (TextReader textReader = new StreamReader(scriptFile))
		{
			while (p > 0)
			{
				text = textReader.ReadLine();
				p--;
				if (p == 0 && string.IsNullOrWhiteSpace(text))
				{
					text = textReader.ReadLine();
				}
			}
		}
		return text;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing && !disposed)
		{
			compiler.Dispose();
		}
		disposed = true;
	}

	private static string ErrorReporting(CompilerResults results)
	{
		StringBuilder stringBuilder = new StringBuilder();
		if (results.Errors.HasErrors)
		{
			int count = results.Errors.Count;
			for (int i = 0; i < count; i++)
			{
				stringBuilder.Append(results.Errors[i].FileName);
				stringBuilder.AppendLine(" In Line: " + results.Errors[i].Line + " Error: " + results.Errors[i].ErrorNumber + " " + results.Errors[i].ErrorText);
			}
		}
		return stringBuilder.ToString();
	}

	private static void RunScript(Assembly script)
	{
		Type[] exportedTypes = script.GetExportedTypes();
		foreach (Type type in exportedTypes)
		{
			Type[] interfaces = type.GetInterfaces();
			foreach (Type type2 in interfaces)
			{
				if (!(type2.FullName == typeof(IAOScript).FullName))
				{
					continue;
				}
				ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
				if (constructor != null && constructor.IsPublic)
				{
					if (constructor.Invoke(null) is IAOScript iAOScript)
					{
						LogScriptAction("Script", ConsoleColor.Green, iAOScript.GetType().Name + " Loaded.", ConsoleColor.Green);
						iAOScript.Main(null);
					}
					else
					{
						LogScriptAction("Error!", ConsoleColor.Red, "Script not loaded.", ConsoleColor.Red);
					}
				}
				else
				{
					LogScriptAction("Error!", ConsoleColor.Red, "No valid constructor found.", ConsoleColor.Red);
				}
			}
		}
	}

	private static bool IsGeneratedScriptAssembly(string assemblyLocation)
	{
		string fullPath = Path.GetFullPath(assemblyLocation);
		string fileName = Path.GetFileName(fullPath);
		string value = Path.GetFullPath("tmp") + Path.DirectorySeparatorChar;
		return string.Equals(fileName, "Scripts.dll", StringComparison.OrdinalIgnoreCase) || fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase);
	}

	private void CleanTemporaryScriptAssemblies()
	{
		string fullPath = Path.GetFullPath("tmp");
		if (!Directory.Exists(fullPath))
		{
			return;
		}
		try
		{
			Directory.Delete(fullPath, recursive: true);
		}
		catch (IOException)
		{
			LogScriptAction("ScriptCompiler:", ConsoleColor.Yellow, "Could not clean tmp script assemblies; another ZoneEngine process may still be running.", ConsoleColor.Red);
		}
		catch (UnauthorizedAccessException)
		{
			LogScriptAction("ScriptCompiler:", ConsoleColor.Yellow, "Could not clean tmp script assemblies due to file permissions.", ConsoleColor.Red);
		}
	}

	private bool LoadFiles()
	{
		try
		{
			ScriptsList = Directory.GetFiles("Scripts", "*.cs", SearchOption.AllDirectories);
		}
		catch (DirectoryNotFoundException)
		{
			LogScriptAction("Error", ConsoleColor.Red, "Scripts directory does not exist!", ConsoleColor.Red);
			return false;
		}
		catch (PathTooLongException)
		{
			LogScriptAction("Error", ConsoleColor.Red, "Path name is too long", ConsoleColor.Red);
			return false;
		}
		catch (ArgumentException)
		{
			LogScriptAction("Error", ConsoleColor.Red, "Path is zero length or has invalid chars", ConsoleColor.Red);
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			LogScriptAction("Error", ConsoleColor.Red, "You don't have permission to access this directory", ConsoleColor.Red);
			return false;
		}
		catch (IOException)
		{
			LogScriptAction("Error", ConsoleColor.Red, "I/O Error occured. (Path is filename or network error)", ConsoleColor.Red);
			return false;
		}
		if (ScriptsList.Length == 0)
		{
			LogScriptAction("Error:", ConsoleColor.Red, "Scripts directory contains no scripts!", ConsoleColor.Yellow);
			return false;
		}
		return true;
	}
}
