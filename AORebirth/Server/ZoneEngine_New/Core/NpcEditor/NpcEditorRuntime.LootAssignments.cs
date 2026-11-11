namespace ZoneEngine_New.Core.NpcEditor;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Playfield;

internal sealed partial class NpcEditorRuntime
{
    internal async Task<int?> EstimateLootAssignmentAsync(NamedLootAssignment candidate, NamedLootAssignment? previous,
        CancellationToken cancellationToken)
    {
        int matches = 0;
        foreach (Playfield field in playfields.SnapshotPlayfields())
        {
            if (candidate.PlayfieldId is int resource && resource != field.ResourceId
                && (previous == null || previous.PlayfieldId is int oldResource && oldResource != field.ResourceId))
                continue;
            try
            {
                matches += await OnTickAsync(field, () => field.GetRequiredService<DynelRegistry>().Dynels()
                    .OfType<NpcCharacter>().Count(npc =>
                    {
                        if (!IsLive(field, npc))
                            return false;
                        NamedLootContext context = NpcEditorNamedLoot.CaptureContext(npc);
                        return NamedLootRegistry.Applies(candidate, context)
                            || previous != null && NamedLootRegistry.Applies(previous, context);
                    }), cancellationToken).ConfigureAwait(false);
            }
            catch (NpcEditorRuntimeException error) when (error.StatusCode == 404)
            {
                // A playfield retired after the manager snapshot.
            }
            catch (NpcEditorRuntimeException error) when (error.StatusCode == 503)
            {
                return null;
            }
        }
        return matches;
    }
}
