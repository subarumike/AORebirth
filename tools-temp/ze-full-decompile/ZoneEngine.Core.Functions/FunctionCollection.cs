using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;
using Utility;

namespace ZoneEngine.Core.Functions;

public class FunctionCollection
{
	public static readonly FunctionCollection Instance;

	private readonly Dictionary<int, FunctionPrototype> functions = new Dictionary<int, FunctionPrototype>();

	private Assembly assembly;

	static FunctionCollection()
	{
		Instance = new FunctionCollection();
		Instance.ReadFunctions();
	}

	public bool CallFunction(int functionNumber, INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		FunctionPrototype functionByNumber = GetFunctionByNumber(functionNumber);
		if (functionByNumber != null)
		{
			LogUtil.Debug((DebugInfoDetail)256, "Called " + functionByNumber.GetType().Name + ": ");
			LogUtil.Debug((DebugInfoDetail)256, FunctionArgumentList.List(arguments));
			return functionByNumber.Execute(self, caller, target, arguments);
		}
		string[] obj = new string[5] { "Function ", null, null, null, null };
		FunctionType val = (FunctionType)functionNumber;
		obj[1] = ((object)(FunctionType)(ref val)).ToString();
		obj[2] = "(";
		obj[3] = functionNumber.ToString();
		obj[4] = ") not found!";
		LogUtil.Debug((DebugInfoDetail)256, string.Concat(obj));
		LogUtil.Debug((DebugInfoDetail)256, FunctionArgumentList.List(arguments));
		return false;
	}

	public FunctionPrototype GetFunctionByNumber(int functionnumber)
	{
		if (functions.Keys.Contains(functionnumber))
		{
			return functions[functionnumber];
		}
		return null;
	}

	public int NumberofRegisteredFunctions()
	{
		return functions.Keys.Count;
	}

	public bool ReadFunctions()
	{
		try
		{
			assembly = Assembly.GetExecutingAssembly();
			foreach (Type item in from x in assembly.GetTypes()
				where x.IsClass && x.BaseType == typeof(FunctionPrototype)
				select x)
			{
				FunctionPrototype functionPrototype = (FunctionPrototype)assembly.CreateInstance(item.FullName);
				if (functionPrototype == null)
				{
					throw new NullReferenceException("Could not create function " + item.FullName);
				}
				functions.Add(functionPrototype.ReturnNumber(), functionPrototype);
			}
		}
		catch (MissingMethodException)
		{
			return false;
		}
		catch (FileNotFoundException)
		{
			return false;
		}
		catch (FileLoadException)
		{
			return false;
		}
		return true;
	}
}
