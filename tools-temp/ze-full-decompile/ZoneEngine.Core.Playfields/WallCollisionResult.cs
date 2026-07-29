using System;
using AORebirth.Core.Playfields;

namespace ZoneEngine.Core.Playfields;

public class WallCollisionResult
{
	public float Factor = 0f;

	public PlayfieldWall FirstWall = new PlayfieldWall();

	public PlayfieldWall SecondWall = new PlayfieldWall();

	public override string ToString()
	{
		return "First: " + Environment.NewLine + ((object)FirstWall).ToString() + Environment.NewLine + "Second: " + Environment.NewLine + ((object)SecondWall).ToString();
	}

	internal int GetDestinationIndex()
	{
		return (FirstWall.DestinationIndex << 16) | (ushort)FirstWall.DestinationPlayfield;
	}
}
