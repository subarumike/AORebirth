namespace ZoneEngine.Core.Missions;

public interface IMissionRewardEffect
{
	MissionRewardEffectResult Apply(MissionRewardExecutionContext context);
}
