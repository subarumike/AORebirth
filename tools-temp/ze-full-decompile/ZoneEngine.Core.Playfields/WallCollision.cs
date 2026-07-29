using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

public static class WallCollision
{
	private static float WallCollisionThreshold = 2f;

	public static WallCollisionResult CheckCollision(ICharacter character)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		Coordinate c = ((IDynel)character).Coordinates();
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		return CheckCollision(c, ((Identity)(ref identity)).Instance);
	}

	public static WallCollisionResult CheckCollision(Coordinate c, int playfieldId)
	{
		float x = c.x;
		float z = c.z;
		List<PlayfieldWalls> walls = PlayfieldLoader.PFData[playfieldId].Walls;
		foreach (PlayfieldWalls item in walls)
		{
			int count = item.Walls.Count;
			for (int i = 0; i < count; i++)
			{
				if (MinimalDistance(item.Walls[i], item.Walls[(i + 1) % count], x, z) < WallCollisionThreshold)
				{
					WallCollisionResult wallCollisionResult = new WallCollisionResult();
					wallCollisionResult.FirstWall = item.Walls[i];
					wallCollisionResult.SecondWall = item.Walls[(i + 1) % count];
					wallCollisionResult.Factor = Distance(wallCollisionResult.FirstWall, x, z) / Distance(wallCollisionResult.FirstWall, wallCollisionResult.SecondWall.X, wallCollisionResult.SecondWall.Z);
					return wallCollisionResult;
				}
			}
		}
		return null;
	}

	public static float Distance(float x1, float z1, float x2, float z2)
	{
		return (float)Math.Sqrt((x1 - x2) * (x1 - x2) + (z1 - z2) * (z1 - z2));
	}

	private static float CrossProduct(PlayfieldWall w1, PlayfieldWall w2, float x, float z)
	{
		float[] array = new float[2];
		float[] array2 = new float[2];
		array[0] = w2.X - w1.X;
		array[1] = w2.Z - w1.Z;
		array2[0] = x - w1.X;
		array2[1] = z - w1.Z;
		return array[0] * array2[1] - array[1] * array2[0];
	}

	private static float Distance(PlayfieldWall w1, float x, float z)
	{
		float num = w1.X - x;
		float num2 = w1.Z - z;
		return (float)Math.Sqrt(num * num + num2 * num2);
	}

	private static float DotProduct(PlayfieldWall w1, PlayfieldWall w2, float x, float z)
	{
		float[] array = new float[2];
		float[] array2 = new float[2];
		array[0] = w2.X - w1.X;
		array[1] = w2.Z - w1.Z;
		array2[0] = x - w2.X;
		array2[1] = z - w2.Z;
		return array[0] * array2[0] + array[1] * array2[1];
	}

	private static float MinimalDistance(PlayfieldWall w1, PlayfieldWall w2, float x, float z)
	{
		if (DotProduct(w1, w2, x, z) > 0f)
		{
			return 15f;
		}
		if (DotProduct(w2, w1, x, z) > 0f)
		{
			return 15f;
		}
		return Math.Abs(CrossProduct(w1, w2, x, z) / Distance(w1, w2.X, w2.Z));
	}
}
