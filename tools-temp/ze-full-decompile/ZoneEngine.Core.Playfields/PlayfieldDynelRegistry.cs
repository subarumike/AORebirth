using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Statels;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Controllers;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldDynelRegistry
{
	private readonly object sync = new object();

	private readonly Identity playfieldIdentity;

	private readonly Dictionary<ulong, IEntity> entities = new Dictionary<ulong, IEntity>();

	private readonly Dictionary<ulong, IInstancedEntity> instancedEntities = new Dictionary<ulong, IInstancedEntity>();

	private readonly Dictionary<ulong, IDynel> dynels = new Dictionary<ulong, IDynel>();

	private readonly Dictionary<ulong, ICharacter> characters = new Dictionary<ulong, ICharacter>();

	private readonly Dictionary<ulong, ICharacter> players = new Dictionary<ulong, ICharacter>();

	private readonly Dictionary<ulong, ICharacter> npcs = new Dictionary<ulong, ICharacter>();

	private readonly Dictionary<ulong, Vendor> vendors = new Dictionary<ulong, Vendor>();

	private readonly Dictionary<ulong, StaticDynel> staticDynels = new Dictionary<ulong, StaticDynel>();

	private readonly Dictionary<ulong, StatelData> statels = new Dictionary<ulong, StatelData>();

	private readonly Dictionary<ulong, StatelData> terminals = new Dictionary<ulong, StatelData>();

	private readonly Dictionary<ulong, StatelData> doors = new Dictionary<ulong, StatelData>();

	private readonly Dictionary<ulong, Identity> corpseIdentities = new Dictionary<ulong, Identity>();

	internal PlayfieldDynelRegistry(Identity playfieldIdentity)
	{
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		this.playfieldIdentity = playfieldIdentity;
	}

	internal void RefreshFromPool()
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		lock (sync)
		{
			ClearPooledViews();
			foreach (IEntity item in Pool.Instance.GetAll<IEntity>(playfieldIdentity))
			{
				RegisterUnlocked(item);
			}
		}
	}

	internal void Register(IEntity entity)
	{
		lock (sync)
		{
			RegisterUnlocked(entity);
		}
	}

	internal void Unregister(Identity identity)
	{
		ulong key = ((Identity)(ref identity)).Long();
		lock (sync)
		{
			entities.Remove(key);
			instancedEntities.Remove(key);
			dynels.Remove(key);
			characters.Remove(key);
			players.Remove(key);
			npcs.Remove(key);
			vendors.Remove(key);
			staticDynels.Remove(key);
			corpseIdentities.Remove(key);
		}
	}

	internal void RegisterStatels(IEnumerable<StatelData> playfieldStatels)
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Invalid comparison between Unknown and I4
		lock (sync)
		{
			statels.Clear();
			terminals.Clear();
			doors.Clear();
			if (playfieldStatels == null)
			{
				return;
			}
			foreach (StatelData playfieldStatel in playfieldStatels)
			{
				if (playfieldStatel != null)
				{
					Identity identity = playfieldStatel.Identity;
					ulong key = ((Identity)(ref identity)).Long();
					statels[key] = playfieldStatel;
					identity = playfieldStatel.Identity;
					if (IsTerminal(((Identity)(ref identity)).Type))
					{
						terminals[key] = playfieldStatel;
					}
					identity = playfieldStatel.Identity;
					if ((int)((Identity)(ref identity)).Type == 51016)
					{
						doors[key] = playfieldStatel;
					}
				}
			}
		}
	}

	internal void RegisterCorpse(Identity corpseIdentity)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		lock (sync)
		{
			corpseIdentities[((Identity)(ref corpseIdentity)).Long()] = corpseIdentity;
		}
	}

	internal IInstancedEntity FindByIdentity(Identity identity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (TryGetRegistered<IInstancedEntity>(identity, out IInstancedEntity entity))
		{
			return entity;
		}
		entity = Pool.Instance.GetObject<IInstancedEntity>(identity);
		if (entity != null)
		{
			Register((IEntity)(object)entity);
		}
		return entity;
	}

	internal T FindByIdentity<T>(Identity identity) where T : class, IEntity
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (TryGetRegistered<T>(identity, out var entity))
		{
			return entity;
		}
		entity = Pool.Instance.GetObject<T>(identity);
		if (entity != null)
		{
			Register((IEntity)(object)entity);
		}
		return entity;
	}

	internal ReadOnlyCollection<IDynel> FindDynelsInRange(IDynel dynel, float range)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Invalid comparison between Unknown and I4
		RefreshFromPool();
		List<IDynel> list = new List<IDynel>();
		if (dynel == null)
		{
			return list.AsReadOnly();
		}
		Coordinate val = dynel.Coordinates();
		foreach (IDynel item in DynelsSnapshot())
		{
			if (item != dynel)
			{
				Identity identity = ((IEntity)item).Identity;
				if ((int)((Identity)(ref identity)).Type == 50000 && item.Coordinates().Distance2D(val) <= (double)range)
				{
					list.Add(item);
				}
			}
		}
		return list.AsReadOnly();
	}

	internal ReadOnlyCollection<ICharacter> FindCharactersInRange(IDynel dynel, float range)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Invalid comparison between Unknown and I4
		RefreshFromPool();
		List<ICharacter> list = new List<ICharacter>();
		if (dynel == null)
		{
			return list.AsReadOnly();
		}
		Coordinate val = dynel.Coordinates();
		foreach (ICharacter item in Characters())
		{
			if (item != dynel)
			{
				Identity identity = ((IEntity)item).Identity;
				if ((int)((Identity)(ref identity)).Type == 50000 && ((IDynel)item).Coordinates().Distance2D(val) <= (double)range)
				{
					list.Add(item);
				}
			}
		}
		return list.AsReadOnly();
	}

	internal ReadOnlyCollection<ICharacter> Characters()
	{
		RefreshFromPool();
		lock (sync)
		{
			return characters.Values.ToList().AsReadOnly();
		}
	}

	internal ReadOnlyCollection<Character> CharacterEntities()
	{
		RefreshFromPool();
		lock (sync)
		{
			return characters.Values.OfType<Character>().ToList().AsReadOnly();
		}
	}

	internal ReadOnlyCollection<ICharacter> Players()
	{
		RefreshFromPool();
		lock (sync)
		{
			return players.Values.ToList().AsReadOnly();
		}
	}

	internal ReadOnlyCollection<ICharacter> Npcs()
	{
		RefreshFromPool();
		lock (sync)
		{
			return npcs.Values.ToList().AsReadOnly();
		}
	}

	internal ReadOnlyCollection<Vendor> Vendors()
	{
		RefreshFromPool();
		lock (sync)
		{
			return vendors.Values.ToList().AsReadOnly();
		}
	}

	internal ReadOnlyCollection<StaticDynel> StaticDynels()
	{
		RefreshFromPool();
		lock (sync)
		{
			return staticDynels.Values.ToList().AsReadOnly();
		}
	}

	internal ReadOnlyCollection<StatelData> Statels()
	{
		lock (sync)
		{
			return statels.Values.ToList().AsReadOnly();
		}
	}

	internal ReadOnlyCollection<StatelData> Terminals()
	{
		lock (sync)
		{
			return terminals.Values.ToList().AsReadOnly();
		}
	}

	internal ReadOnlyCollection<StatelData> Doors()
	{
		lock (sync)
		{
			return doors.Values.ToList().AsReadOnly();
		}
	}

	internal ReadOnlyCollection<Identity> CorpseIdentities()
	{
		lock (sync)
		{
			return corpseIdentities.Values.ToList().AsReadOnly();
		}
	}

	private void RegisterUnlocked(IEntity entity)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		if (entity == null || entity.Parent != playfieldIdentity)
		{
			return;
		}
		Identity identity = entity.Identity;
		ulong key = ((Identity)(ref identity)).Long();
		entities[key] = entity;
		IInstancedEntity val = (IInstancedEntity)(object)((entity is IInstancedEntity) ? entity : null);
		if (val != null)
		{
			instancedEntities[key] = val;
		}
		IDynel val2 = (IDynel)(object)((entity is IDynel) ? entity : null);
		if (val2 != null)
		{
			dynels[key] = val2;
		}
		ICharacter val3 = (ICharacter)(object)((entity is ICharacter) ? entity : null);
		if (val3 != null)
		{
			characters[key] = val3;
			if (((IDynel)val3).Controller is NPCController)
			{
				npcs[key] = val3;
			}
			else
			{
				players[key] = val3;
			}
		}
		Vendor val4 = (Vendor)(object)((entity is Vendor) ? entity : null);
		if (val4 != null)
		{
			vendors[key] = val4;
		}
		StaticDynel val5 = (StaticDynel)(object)((entity is StaticDynel) ? entity : null);
		if (val5 != null)
		{
			staticDynels[key] = val5;
		}
	}

	private bool TryGetRegistered<T>(Identity identity, out T entity) where T : class, IEntity
	{
		lock (sync)
		{
			if (entities.TryGetValue(((Identity)(ref identity)).Long(), out var value))
			{
				entity = value as T;
				return entity != null;
			}
		}
		entity = null;
		return false;
	}

	private IEnumerable<IDynel> DynelsSnapshot()
	{
		lock (sync)
		{
			return dynels.Values.ToList();
		}
	}

	private void ClearPooledViews()
	{
		entities.Clear();
		instancedEntities.Clear();
		dynels.Clear();
		characters.Clear();
		players.Clear();
		npcs.Clear();
		vendors.Clear();
		staticDynels.Clear();
	}

	private static bool IsTerminal(IdentityType identityType)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Invalid comparison between Unknown and I4
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Invalid comparison between Unknown and I4
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Invalid comparison between Unknown and I4
		return (int)identityType == 51005 || (int)identityType == 51059 || (int)identityType == 56481 || (int)identityType == 51035;
	}
}
