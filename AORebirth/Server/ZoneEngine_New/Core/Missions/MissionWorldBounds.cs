namespace ZoneEngine_New.Core.Missions;

using System;
using System.Linq;
using ZoneEngine.Core.Missions;
using Vector3 = AORebirth.Core.Vector.Vector3;

internal sealed class MissionWorldBounds
{
    readonly Vector3 minimum, maximum;
    internal double MaximumInternalDistance => Vector3.Abs(maximum - minimum);
    internal MissionWorldBounds(MissionAcgLayoutBundle layout)
    {
        var points = layout.Dynels.Select(value => value.Position)
            .Concat(layout.NpcSlots.Select(value => value.Position)).Concat(layout.ObjectiveSlots.Select(value => value.Position))
            .Append(layout.EntryPoint).Append(layout.Exit?.Position).ToArray();
        var settings = MissionGenerationSettings.Current;
        if (points.Any(point => point == null || !float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
            || points.Distinct().Count() < settings.MinimumGeometryPoints)
            throw new InvalidOperationException("The mission layout has insufficient finite geometry.");
        float tolerance = settings.PositionTolerance;
        minimum = new(points.Min(p => p.X) - tolerance, points.Min(p => p.Y) - tolerance, points.Min(p => p.Z) - tolerance);
        maximum = new(points.Max(p => p.X) + tolerance, points.Max(p => p.Y) + tolerance, points.Max(p => p.Z) + tolerance);
        if (!Finite(minimum) || !Finite(maximum)) throw new InvalidOperationException("Mission bounds are outside finite coordinates.");
    }
    internal bool Contains(Vector3 point) => Finite(point) && point.xf >= minimum.xf && point.xf <= maximum.xf
        && point.yf >= minimum.yf && point.yf <= maximum.yf && point.zf >= minimum.zf && point.zf <= maximum.zf;
    static bool Finite(Vector3 point) => float.IsFinite(point.xf) && float.IsFinite(point.yf) && float.IsFinite(point.zf);
}
