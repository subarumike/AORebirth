namespace ZoneEngine.Core.Playfields;

internal sealed class Pf127GeometryDocumentDto
{
	public int? SchemaVersion { get; set; }

	public int? PlayfieldResource { get; set; }

	public string Source { get; set; }

	public string SourceSha256 { get; set; }

	public double? DamageLineOfSightProbeHeight { get; set; }

	public string DamageLineOfSightProbeHeightEvidence { get; set; }

	public Pf127GeometryTriangleDto[] Triangles { get; set; }
}
