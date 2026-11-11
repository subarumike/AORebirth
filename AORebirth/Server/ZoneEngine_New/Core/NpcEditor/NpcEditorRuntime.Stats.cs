namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Playfield;

internal sealed partial class NpcEditorRuntime
{
    public async Task<NpcEditorStatsTarget> CaptureStatsAsync(
        NpcEditorIdentity identity, CancellationToken cancellationToken, bool estimate = true,
        string? scope = null, string? familyHash = null)
    {
        scope ??= "templateBand";
        if (scope is not ("templateBand" or "family" or "playfield")
            || (scope != "family" && familyHash != null))
            throw new NpcEditorRuntimeException(400, "Select a supported stat scope and family.");

        NpcEditorStatsTarget target = await WithNpcAsync(identity,
            npc => CaptureScopedStatsTarget(npc, scope, familyHash), cancellationToken)
            .ConfigureAwait(false);
        if (estimate && target.UnavailableReason == null)
        {
            int? affected = scope == "templateBand"
                ? await EstimateTemplateBandAsync(target.TemplateHash, target.BandIndex, cancellationToken).ConfigureAwait(false)
                : await EstimateScopedStatsAsync(target, cancellationToken).ConfigureAwait(false);
            target = target with { EstimatedAffectedNpcs = affected };
        }
        return target;
    }

    static NpcEditorStatsTarget CaptureScopedStatsTarget(NpcCharacter npc, string scope, string? familyHash)
    {
        NpcEditorStatsTarget target = CaptureStatsTarget(npc) with { Scope = scope, FamilyHash = familyHash };
        if (target.UnavailableReason != null || scope == "templateBand")
            return target;
        if (scope == "playfield")
            return target.ResourcePlayfieldId is > 0 ? target
                : target with { UnavailableReason = "The selected NPC has no authoritative resource playfield." };

        if (familyHash is not { Length: > 0 and <= 64 } ||
            npc.Playfield?.GetService<IGameData>() is not GameDataStore data
            || data.EditorStatsFamily(familyHash) is not { } binding
            || !binding.Leaves.Contains(target.TemplateHash, StringComparer.Ordinal))
            return target with { UnavailableReason = "Choose an authored family containing the selected NPC template." };
        return target with { ScopeSignature = binding.Signature, FamilyLeaves = binding.Leaves };
    }

    async Task<int?> EstimateScopedStatsAsync(NpcEditorStatsTarget target, CancellationToken cancellationToken)
    {
        var family = target.FamilyLeaves == null ? null : new HashSet<string>(target.FamilyLeaves, StringComparer.Ordinal);
        int matches = 0;
        foreach (Playfield field in playfields.SnapshotPlayfields())
        {
            if (family == null && field.ResourceId != target.ResourcePlayfieldId)
                continue;
            try
            {
                matches += await OnTickAsync(field, () => field.GetRequiredService<DynelRegistry>().Dynels()
                    .OfType<NpcCharacter>().Count(npc => IsLive(field, npc) && npc.MobTemplate is { UnresolvedPlaceholder: false } template
                        && (family != null ? family.Contains(template.Hash)
                            : template.Hash == target.TemplateHash && template.AuthoredBandIndex == target.BandIndex)),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (NpcEditorRuntimeException error) when (error.StatusCode == 404)
            {
                // A playfield retired while its population was being counted.
            }
            catch (NpcEditorRuntimeException error) when (error.StatusCode == 503)
            {
                return null;
            }
        }
        return matches;
    }

    internal static NpcEditorStatsTarget CaptureStatsTarget(NpcCharacter npc)
    {
        if (npc.MobTemplate is not { UnresolvedPlaceholder: false, EditorLootLeaf: { } leaf } template
            || template.AuthoredBandIndex < 0 || template.AuthoredBandIndex >= leaf.Bands.Length
            || !npc.Attackable || npc.Pet != null || npc.Shop != null)
            return new(npc.MobTemplate?.Hash ?? string.Empty, -1, string.Empty,
                "Select an attackable, authored template NPC without a pet owner or shop.");
        return new(leaf.Hash, template.AuthoredBandIndex, leaf.EditorLootTemplateSignature, null,
            ResourcePlayfieldId: npc.Playfield?.ResourceId);
    }
}
