using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core.Navigation;

internal static class PlayfieldChaseNavigationProviderFactory
{
	internal static IPlayfieldChaseNavigationProvider Create(int playfieldResource)
	{
		IPlayfieldChaseNavigationProvider result;
		if (playfieldResource != 127)
		{
			IPlayfieldChaseNavigationProvider playfieldChaseNavigationProvider = new UnsupportedPlayfieldChaseNavigationProvider(playfieldResource);
			result = playfieldChaseNavigationProvider;
		}
		else
		{
			IPlayfieldChaseNavigationProvider playfieldChaseNavigationProvider = new Pf127ChaseNavigationProvider(playfieldResource, Pf127CollisionGeometryLoader.Current);
			result = playfieldChaseNavigationProvider;
		}
		return result;
	}
}
