using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AORebirth.Core.Entities;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.ChatCommands;

public abstract class AOChatCommand
{
	public static bool CheckArgumentHelper(List<Type> typeList, string[] args)
	{
		if (args.Length - 1 != typeList.Count)
		{
			return false;
		}
		bool flag = true;
		for (int i = 0; i < typeList.Count; i++)
		{
			if (!(typeList.ElementAt(i).FullName == typeof(string).FullName))
			{
				if (typeList.ElementAt(i).FullName == typeof(int).FullName)
				{
					flag &= int.TryParse(args[i + 1], out var _);
				}
				else if (typeList.ElementAt(i).FullName == typeof(int).FullName)
				{
					flag &= int.TryParse(args[i + 1], out var _);
				}
				else if (typeList.ElementAt(i).FullName == typeof(bool).FullName)
				{
					flag &= bool.TryParse(args[i + 1], out var _);
				}
				else if (typeList.ElementAt(i).FullName == typeof(uint).FullName)
				{
					flag &= uint.TryParse(args[i + 1], out var _);
				}
				else if (typeList.ElementAt(i).FullName == typeof(float).FullName)
				{
					flag &= float.TryParse(args[i + 1], NumberStyles.Any, CultureInfo.InvariantCulture, out var _);
				}
			}
		}
		return flag;
	}

	public abstract bool CheckCommandArguments(string[] args);

	public abstract void CommandHelp(ICharacter character);

	public abstract void ExecuteCommand(ICharacter character, Identity target, string[] args);

	public abstract int GMLevelNeeded();

	public abstract List<string> ListCommands();
}
