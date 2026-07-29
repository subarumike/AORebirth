using System;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldCollisionGeometryLoadResult
{
	internal PlayfieldCollisionGeometry Geometry { get; private set; }

	internal string Error { get; private set; }

	internal bool IsLoaded => Geometry != null && string.IsNullOrEmpty(Error);

	private PlayfieldCollisionGeometryLoadResult(PlayfieldCollisionGeometry geometry, string error)
	{
		Geometry = geometry;
		Error = error ?? string.Empty;
	}

	internal static PlayfieldCollisionGeometryLoadResult Loaded(PlayfieldCollisionGeometry geometry)
	{
		if (geometry == null)
		{
			throw new ArgumentNullException("geometry");
		}
		return new PlayfieldCollisionGeometryLoadResult(geometry, string.Empty);
	}

	internal static PlayfieldCollisionGeometryLoadResult Failed(string error)
	{
		if (string.IsNullOrWhiteSpace(error))
		{
			throw new ArgumentException("A collision geometry load error is required.", "error");
		}
		return new PlayfieldCollisionGeometryLoadResult(null, error);
	}
}
