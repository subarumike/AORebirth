namespace AORebirth.Core.Playfields;

internal interface IPopulationStateStore
{
	void Save(PopulationRuntimeState state);

	PopulationRuntimeState Load(string spawnKey);
}
