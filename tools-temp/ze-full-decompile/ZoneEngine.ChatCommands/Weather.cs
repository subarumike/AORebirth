using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.ChatCommands;

public class Weather : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		List<Type> list = new List<Type>();
		list.Add(typeof(short));
		list.Add(typeof(int));
		list.Add(typeof(short));
		list.Add(typeof(float));
		list.Add(typeof(byte));
		list.Add(typeof(byte));
		list.Add(typeof(byte));
		list.Add(typeof(byte));
		list.Add(typeof(byte));
		list.Add(typeof(byte));
		list.Add(typeof(byte));
		list.Add(typeof(byte));
		list.Add(typeof(string));
		list.Add(typeof(string));
		list.Add(typeof(byte));
		List<Type> typeList = list;
		return AOChatCommand.CheckArgumentHelper(typeList, args);
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Arguments: <fadeIn> <duration> <fadeOut> <Range> <Weathertype> <Intensity> <Wind> <Clouds> <thunderstrikes> <tremors> <tremorpercentage> <thunderstrikepercentage> <ambientColor> <fogColor> <zBufferMax>\r\nWeathertypes: 0 = Rain, 1 = Fog, 2 = Unknown, 3 = Quake, 4 = Sandstorm, 5 = AshStorm, 6 = RedFalloutStorm, 7 = GreenFalloutStorm\r\nType is 0-7, Range is Single, all other values are 0-100", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected O, but got Unknown
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected O, but got Unknown
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = new Vector3();
		val.X = ((IDynel)character).Coordinates().x;
		val.Y = ((IDynel)character).Coordinates().y;
		val.Z = ((IDynel)character).Coordinates().z;
		byte b = byte.Parse(args[1]);
		int ambientColor = Convert.ToInt32(args[13], 16);
		int fogColor = Convert.ToInt32(args[14], 16);
		WeatherEntry val2 = new WeatherEntry();
		val2.AmbientColor = ambientColor;
		val2.FogColor = fogColor;
		val2.Position = val;
		val2.FadeIn = short.Parse(args[1]);
		val2.Duration = int.Parse(args[2]);
		val2.FadeOut = short.Parse(args[3]);
		val2.Range = float.Parse(args[4]);
		val2.WeatherType = (WeatherType)byte.Parse(args[5]);
		val2.Intensity = byte.Parse(args[6]);
		val2.Wind = byte.Parse(args[7]);
		val2.Clouds = byte.Parse(args[8]);
		val2.Thunderstrikes = byte.Parse(args[9]);
		val2.Tremors = byte.Parse(args[10]);
		val2.ThunderstrikePercentage = byte.Parse(args[11]);
		val2.TremorPercentage = byte.Parse(args[12]);
		val2.ZBufferVisibility = byte.Parse(args[15]);
		val2.Playfield = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		WeatherSettings.Instance.Add(val2);
		BaseMessageHandler<WeatherControlMessage, WeatherControlMessageHandler>.Default.Send(character, val2);
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		return new List<string>(new string[1] { "weather" });
	}
}
