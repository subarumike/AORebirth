namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.Linq;
using ZoneEngine.Core.Missions;

/// <summary>Evaluates editable travel priority bands against the existing geography and entrance catalog.</summary>
internal sealed class MissionRollLocations
{
    readonly MissionLocationPool.Spot[] _candidates;
    readonly List<MissionLocationPool.Spot> _selected = [];
    readonly bool _differentPlayfields;
    internal MissionRollLocations(int level, int terminal, float x, float z, MissionLocationSide side)
    {
        var policy = MissionRollPolicy.Current.Travel;
        _differentPlayfields = level > policy.DistinctPlayfieldsAboveLevel;
        var affiliation = MissionLocationPool.TryGetCityAffiliation(terminal, out var city) ? city : side;
        var all = MissionLocationPool.Spots;
        var allowed = all.Where(s => s.Playfield == terminal || MissionLocationPool.IsSpotAllowedForTerminal(s.Playfield, affiliation)).ToArray();
        double minimum = level <= policy.MinimumDistanceStartsAboveLevel ? 0 : Math.Max(policy.MinimumDistanceFloor, level * policy.MinimumDistancePerLevel + policy.MinimumDistanceOffset);
        double maximum = level * policy.MaximumDistancePerLevel + policy.MaximumDistanceOffset;
        bool DistanceMatches(MissionLocationPool.Spot spot)
        {
            double distance = terminal != 0 && terminal == spot.Playfield
                ? Math.Sqrt(Math.Pow(spot.X - x, 2) + Math.Pow(spot.Z - z, 2))
                : MissionLocationPool.ApproxTravelMeters(terminal, spot.Playfield);
            return distance >= minimum && distance <= maximum;
        }
        var groups = new Dictionary<string, MissionLocationPool.Spot[]>
        {
            ["same"] = allowed.Where(s => MissionLocationPool.NearClusterRank(terminal, s.Playfield) == 0).ToArray(),
            ["near"] = allowed.Where(s => MissionLocationPool.NearClusterRank(terminal, s.Playfield) == 1).ToArray(),
            ["ring"] = allowed.Where(s => MissionLocationPool.NearClusterRank(terminal, s.Playfield) == 2).ToArray(),
            ["distance"] = allowed.Where(DistanceMatches).ToArray(), ["side"] = allowed, ["all"] = all
        };
        groups["local"] = groups["same"].Concat(groups["near"]).Concat(groups["ring"]).ToArray();
        _candidates = policy.Bands.First(b => level <= b.MaximumLevel).Priority.Select(name => groups[name]).FirstOrDefault(g => g.Length != 0)
            ?? throw new InvalidOperationException("No configured entrance satisfies the mission travel policy.");
    }
    internal MissionLocationPool.Spot Next(Random random)
    {
        MissionLocationPool.Spot? result = null;
        for (int attempt = 0; attempt < MissionRollPolicy.Current.Travel.DistinctAttempts; attempt++)
        {
            var candidate = _candidates[random.Next(_candidates.Length)];
            if (!_selected.Contains(candidate) && (!_differentPlayfields || _selected.All(s => s.Playfield != candidate.Playfield)))
            { result = candidate; break; }
        }
        result ??= _candidates[random.Next(_candidates.Length)];
        _selected.Add(result);
        return result;
    }
}
