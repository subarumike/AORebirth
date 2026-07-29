namespace ZoneEngine.Core.MessageHandlers;

public static class CityControllerInteractionRules
{
	public static CityControllerMenuMode ResolveMenuMode(int organizationId, int owningOrganizationId)
	{
		return (organizationId <= 0 || organizationId != owningOrganizationId) ? CityControllerMenuMode.NonOrgLimited : CityControllerMenuMode.OwnerMember;
	}
}
