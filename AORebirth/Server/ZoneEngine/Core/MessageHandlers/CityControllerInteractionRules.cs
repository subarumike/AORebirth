namespace ZoneEngine.Core.MessageHandlers
{
    public enum CityControllerMenuMode
    {
        OwnerMember,
        NonOrgLimited
    }

    public static class CityControllerInteractionRules
    {
        public static CityControllerMenuMode ResolveMenuMode(int organizationId, int owningOrganizationId)
        {
            return organizationId > 0 && organizationId == owningOrganizationId
                       ? CityControllerMenuMode.OwnerMember
                       : CityControllerMenuMode.NonOrgLimited;
        }
    }
}
