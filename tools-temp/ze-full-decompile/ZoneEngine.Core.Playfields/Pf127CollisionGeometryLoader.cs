using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace ZoneEngine.Core.Playfields;

internal static class Pf127CollisionGeometryLoader
{
	internal const int SubwayPlayfieldResource = 127;

	internal const string RelativePath = "Content\\Captured\\Subway\\pf127-geometry.json";

	private const int MaximumTriangleCount = 2000000;

	private static readonly Lazy<PlayfieldCollisionGeometryLoadResult> CurrentGeometry = new Lazy<PlayfieldCollisionGeometryLoadResult>(LoadDefaultPath, isThreadSafe: true);

	internal static string DefaultPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content\\Captured\\Subway\\pf127-geometry.json");

	internal static PlayfieldCollisionGeometryLoadResult Current => CurrentGeometry.Value;

	internal static PlayfieldCollisionGeometryLoadResult LoadPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return PlayfieldCollisionGeometryLoadResult.Failed("PF127 collision geometry path is missing.");
		}
		if (!File.Exists(path))
		{
			return PlayfieldCollisionGeometryLoadResult.Failed("PF127 collision geometry file was not found: " + path);
		}
		try
		{
			return LoadJson(File.ReadAllText(path));
		}
		catch (Exception ex)
		{
			return PlayfieldCollisionGeometryLoadResult.Failed("PF127 collision geometry read failed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	internal static PlayfieldCollisionGeometryLoadResult LoadJson(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return PlayfieldCollisionGeometryLoadResult.Failed("PF127 collision geometry JSON is empty.");
		}
		Pf127GeometryDocumentDto pf127GeometryDocumentDto;
		try
		{
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer
			{
				MaxJsonLength = int.MaxValue
			};
			pf127GeometryDocumentDto = javaScriptSerializer.Deserialize<Pf127GeometryDocumentDto>(json);
		}
		catch (Exception ex)
		{
			return PlayfieldCollisionGeometryLoadResult.Failed("PF127 collision geometry JSON parse failed: " + ex.GetType().Name + ": " + ex.Message);
		}
		if (pf127GeometryDocumentDto == null)
		{
			return PlayfieldCollisionGeometryLoadResult.Failed("PF127 collision geometry JSON did not contain a document.");
		}
		if (pf127GeometryDocumentDto.SchemaVersion != 1)
		{
			return PlayfieldCollisionGeometryLoadResult.Failed("PF127 collision geometry schemaVersion must be " + 1 + ".");
		}
		if (pf127GeometryDocumentDto.PlayfieldResource != 127)
		{
			return PlayfieldCollisionGeometryLoadResult.Failed("PF127 collision geometry playfieldResource must be 127.");
		}
		if (!IsSha256(pf127GeometryDocumentDto.SourceSha256))
		{
			return PlayfieldCollisionGeometryLoadResult.Failed("PF127 collision geometry sourceSha256 must contain exactly 64 hexadecimal characters.");
		}
		if (!pf127GeometryDocumentDto.DamageLineOfSightProbeHeight.HasValue || double.IsNaN(pf127GeometryDocumentDto.DamageLineOfSightProbeHeight.Value) || double.IsInfinity(pf127GeometryDocumentDto.DamageLineOfSightProbeHeight.Value) || pf127GeometryDocumentDto.DamageLineOfSightProbeHeight.Value < 0.0 || pf127GeometryDocumentDto.DamageLineOfSightProbeHeight.Value > 10.0)
		{
			return PlayfieldCollisionGeometryLoadResult.Failed("PF127 collision geometry damageLineOfSightProbeHeight must be finite and between 0 and 10.");
		}
		if (string.IsNullOrWhiteSpace(pf127GeometryDocumentDto.DamageLineOfSightProbeHeightEvidence))
		{
			return PlayfieldCollisionGeometryLoadResult.Failed("PF127 collision geometry damageLineOfSightProbeHeightEvidence is required.");
		}
		if (pf127GeometryDocumentDto.Triangles == null || pf127GeometryDocumentDto.Triangles.Length == 0)
		{
			return PlayfieldCollisionGeometryLoadResult.Failed("PF127 collision geometry must contain triangles.");
		}
		if (pf127GeometryDocumentDto.Triangles.Length > 2000000)
		{
			return PlayfieldCollisionGeometryLoadResult.Failed("PF127 collision geometry exceeds the supported triangle limit.");
		}
		List<CollisionTriangle> list = new List<CollisionTriangle>(pf127GeometryDocumentDto.Triangles.Length);
		for (int i = 0; i < pf127GeometryDocumentDto.Triangles.Length; i++)
		{
			Pf127GeometryTriangleDto triangle = pf127GeometryDocumentDto.Triangles[i];
			if (!TryConvertTriangle(triangle, i, out var converted, out var error))
			{
				return PlayfieldCollisionGeometryLoadResult.Failed(error);
			}
			list.Add(converted);
		}
		try
		{
			return PlayfieldCollisionGeometryLoadResult.Loaded(new PlayfieldCollisionGeometry(pf127GeometryDocumentDto.SchemaVersion.Value, pf127GeometryDocumentDto.PlayfieldResource.Value, pf127GeometryDocumentDto.Source, pf127GeometryDocumentDto.SourceSha256, pf127GeometryDocumentDto.DamageLineOfSightProbeHeight.Value, pf127GeometryDocumentDto.DamageLineOfSightProbeHeightEvidence, list));
		}
		catch (Exception ex2)
		{
			return PlayfieldCollisionGeometryLoadResult.Failed("PF127 collision geometry validation failed: " + ex2.GetType().Name + ": " + ex2.Message);
		}
	}

	private static PlayfieldCollisionGeometryLoadResult LoadDefaultPath()
	{
		return LoadPath(DefaultPath);
	}

	private static bool IsSha256(string value)
	{
		if (value == null || value.Length != 64)
		{
			return false;
		}
		foreach (char c in value)
		{
			if ((c < '0' || c > '9') && (c < 'a' || c > 'f') && (c < 'A' || c > 'F'))
			{
				return false;
			}
		}
		return true;
	}

	private static bool TryConvertTriangle(Pf127GeometryTriangleDto triangle, int index, out CollisionTriangle converted, out string error)
	{
		converted = default(CollisionTriangle);
		if (triangle == null || !triangle.Id.HasValue)
		{
			error = "PF127 collision geometry triangle[" + index + "] is missing id.";
			return false;
		}
		if (!TryConvertPoint(triangle.A, out var converted2) || !TryConvertPoint(triangle.B, out var converted3) || !TryConvertPoint(triangle.C, out var converted4))
		{
			error = "PF127 collision geometry triangle[" + index + "] has missing or nonfinite coordinates.";
			return false;
		}
		try
		{
			converted = new CollisionTriangle(triangle.Id.Value, converted2, converted3, converted4);
			error = string.Empty;
			return true;
		}
		catch (Exception ex)
		{
			error = "PF127 collision geometry triangle[" + index + "] is invalid: " + ex.Message;
			return false;
		}
	}

	private static bool TryConvertPoint(Pf127GeometryPointDto point, out CollisionPoint3 converted)
	{
		if (point == null || !point.X.HasValue || !point.Y.HasValue || !point.Z.HasValue)
		{
			converted = default(CollisionPoint3);
			return false;
		}
		converted = new CollisionPoint3(point.X.Value, point.Y.Value, point.Z.Value);
		return converted.IsFinite;
	}
}
