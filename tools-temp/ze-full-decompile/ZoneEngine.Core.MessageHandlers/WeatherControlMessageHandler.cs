using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class WeatherControlMessageHandler : BaseMessageHandler<WeatherControlMessage, WeatherControlMessageHandler>
{
	public void Send(ICharacter character, WeatherEntry w, bool announceToPlayfield = false)
	{
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Expected I4, but got Unknown
		double totalMilliseconds = (DateTime.UtcNow - w.StartTime).TotalMilliseconds;
		double num = (float)w.FadeIn / 6f;
		short fadeIn = w.FadeIn;
		int num2 = w.Duration;
		if (totalMilliseconds > 0.0)
		{
			totalMilliseconds -= num;
			num = ((totalMilliseconds < 0.0) ? (0.0 - totalMilliseconds) : 0.0);
			totalMilliseconds = ((totalMilliseconds < 0.0) ? 0.0 : totalMilliseconds);
			totalMilliseconds -= (double)(num2 * 1000);
			num2 = ((!(totalMilliseconds < 0.0)) ? 1 : Convert.ToInt32((0.0 - totalMilliseconds) / 1000.0));
			fadeIn = Convert.ToInt16(num * 6.0);
		}
		Vector3 position = w.Position;
		Send(character, w.Playfield, position, fadeIn, num2, w.FadeOut, w.Range, (byte)(int)w.WeatherType, w.Intensity, w.Wind, w.Clouds, w.Thunderstrikes, w.Tremors, w.TremorPercentage, w.ThunderstrikePercentage, w.AmbientColor, w.FogColor, w.ZBufferVisibility, announceToPlayfield);
	}

	public void Send(ICharacter character, Identity playfield, Vector3 position, short fadeIn, int duration, short fadeOut, float range, byte weathertype, byte weatherIntensity, byte wind, byte clouds, byte thunderstrikes, byte tremors, byte tremorPercentage, byte thunderstrikePercentage, int ambientColor, int fogColor, byte zBufferVisibility, bool announceToPlayfield)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<WeatherControlMessage>)(object)this).Send(character, Filler1(playfield, position, fadeIn, duration, fadeOut, range, weathertype, weatherIntensity, wind, clouds, thunderstrikes, tremors, tremorPercentage, thunderstrikePercentage, ambientColor, fogColor, zBufferVisibility), announceToPlayfield);
	}

	private MessageDataFiller<WeatherControlMessage> Filler1(Identity playfield, Vector3 position, short fadeIn, int duration, short fadeOut, float range, byte weatherType, byte weatherIntensity, byte wind, byte clouds, byte thunderstrikes, byte tremors, byte tremorPercentage, byte thunderstrikePercentage, int ambientColor, int fogColor, byte zBufferVisibility)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		return delegate(WeatherControlMessage x)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Expected O, but got Unknown
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			x.Position = new Vector3();
			((N3Message)x).Unknown = 0;
			Identity identity = default(Identity);
			((Identity)(ref identity)).Type = (IdentityType)51100;
			((Identity)(ref identity)).Instance = ((Identity)(ref playfield)).Instance;
			((N3Message)x).Identity = identity;
			x.FadeIn = fadeIn;
			x.Duration = duration;
			x.FadeOut = fadeOut;
			x.Range = range;
			x.WeatherType = weatherType;
			x.WeatherIntensity = weatherIntensity;
			x.Wind = wind;
			x.Clouds = clouds;
			x.Thunderstrikes = thunderstrikes;
			x.Tremors = tremors;
			x.TremorPercentage = tremorPercentage;
			x.ThunderstrikePercentage = thunderstrikePercentage;
			x.CloudColorRed = (byte)((uint)ambientColor & 0xFFu);
			x.CloudColorGreen = (byte)((uint)(ambientColor >> 8) & 0xFFu);
			x.CloudColorBlue = (byte)(ambientColor >> 16);
			x.FogColorRed = (byte)((uint)fogColor & 0xFFu);
			x.FogColorGreen = (byte)((uint)(fogColor >> 8) & 0xFFu);
			x.FogColorBlue = (byte)(fogColor >> 16);
			x.ZBufferVisibility = zBufferVisibility;
			x.Position.X = position.X;
			x.Position.Y = position.Y;
			x.Position.Z = position.Z;
			x.UnknownSingle = 0f;
			LogUtil.Debug((DebugInfoDetail)1024, DebugStrings.DebugString<WeatherControlMessage>(x));
		};
	}
}
