using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using AORebirth.Communication.Messages;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using Cell.Core;
using MemBus;
using MemBus.Configurators;
using MemBus.Setup;
using MemBus.Support;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using ZoneEngine.ChatCommands;
using ZoneEngine.Core.Playfields;
using ZoneEngine.Script;

namespace ZoneEngine.Core;

public class ZoneServer : ServerBase
{
	public HashSet<ZoneClient> Clients = new HashSet<ZoneClient>();

	public int Id;

	private readonly List<IPlayfield> playfields = new List<IPlayfield>();

	private readonly DisposeContainer memBusDisposeContainer = new DisposeContainer(Array.Empty<object>());

	private readonly IBus zoneBus;

	private readonly MessageSerializer messageSerializer = new MessageSerializer();

	private readonly List<Type> subscribedMessageHandlers = new List<Type>();

	private readonly Dictionary<IPAddress, DateTime> connectDelayList = new Dictionary<IPAddress, DateTime>();

	public ZoneServer()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Expected O, but got Unknown
		Id = 854;
		((ServerBase)this).ClientDisconnected += new ClientDisconnectedHandler(ZoneServerClientDisconnected);
		zoneBus = BusSetup.StartWith<AsyncConfiguration>(Array.Empty<ISetup<IConfigurableBus>>()).Construct();
		subscribedMessageHandlers.Clear();
		IEnumerable<Type> enumerable = from x in Assembly.GetExecutingAssembly().GetTypes()
			where x.GetCustomAttributes(typeof(MessageHandlerAttribute), inherit: false).Any((object y) => (int)((MessageHandlerAttribute)y).Direction != 2)
			select x;
		MethodInfo method = typeof(ZoneServer).GetMethod("SubscribeMessage", BindingFlags.Instance | BindingFlags.NonPublic);
		foreach (Type item in enumerable)
		{
			Type[] genericArguments = item.BaseType.GetGenericArguments();
			MethodInfo methodInfo = method.MakeGenericMethod(genericArguments[1], genericArguments[0]);
			methodInfo.Invoke(this, null);
		}
		CheckSubscribedMessageHandlers();
	}

	private void SubscribeMessage<T, TU>() where T : AbstractMessageHandler<TU> where TU : MessageBody, new()
	{
		T val = (T)typeof(T).GetProperty("Default", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy).GetValue(null, null);
		memBusDisposeContainer.Add(((ISubscriber)zoneBus).Subscribe<MessageWrapper<TU>>((Action<MessageWrapper<TU>>)((AbstractMessageHandler<TU>)val).Receive));
		subscribedMessageHandlers.Add(typeof(TU));
	}

	private void CheckSubscribedMessageHandlers()
	{
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Expected O, but got Unknown
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Invalid comparison between Unknown and I4
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Invalid comparison between Unknown and I4
		bool flag = false;
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		foreach (Type item2 in from x in executingAssembly.GetTypes()
			where x.IsClass && x.GetCustomAttributes(typeof(MessageHandlerAttribute), inherit: true).Any()
			select x)
		{
			if (!(item2.BaseType != null))
			{
				continue;
			}
			Type item = item2.BaseType.GetGenericArguments()[0];
			MessageHandlerAttribute val = (MessageHandlerAttribute)item2.GetCustomAttributes(typeof(MessageHandlerAttribute), inherit: true).FirstOrDefault();
			if ((int)val.Direction == 0)
			{
				Console.WriteLine("Warning: '" + item2.Name + "' has no Direction defined (MessageHandlerAttribute missing in declaration?)");
			}
			else if ((int)val.Direction != 2 && !subscribedMessageHandlers.Contains(item))
			{
				if (!flag)
				{
					Console.WriteLine("Warning! Following Messagehandlers have not been subscribed!");
					flag = true;
				}
				Console.WriteLine("Missing: " + item2.Name);
			}
		}
	}

	public void DisconnectAllClients()
	{
		foreach (Playfield playfield in playfields)
		{
			playfield.DisconnectAllClients();
		}
	}

	public Dictionary<Identity, string> ListAvailablePlayfields(bool global = true)
	{
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<Identity, string> dictionary = new Dictionary<Identity, string>();
		Dictionary<int, string> dictionary2 = ZoneEngine.Core.Playfields.Playfields.PlayfieldNames();
		Identity key;
		if (!global)
		{
			foreach (Playfield playfield in playfields)
			{
				Identity identity = ((PooledObject)playfield).Identity;
				key = ((PooledObject)playfield).Identity;
				dictionary.Add(identity, dictionary2[((Identity)(ref key)).Instance]);
			}
		}
		else
		{
			foreach (KeyValuePair<int, string> item in dictionary2)
			{
				key = default(Identity);
				((Identity)(ref key)).Type = (IdentityType)51101;
				((Identity)(ref key)).Instance = item.Key;
				dictionary.Add(key, item.Value);
			}
		}
		return dictionary;
	}

	public IPlayfield PlayfieldById(Identity id)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		lock (playfields)
		{
			foreach (IPlayfield playfield in playfields)
			{
				if (((IEntity)playfield).Identity == id)
				{
					return playfield;
				}
			}
			return CreatePlayfieldUnlocked(id);
		}
	}

	internal void ProcessISComMessage(DynamicMessage messageobject)
	{
		MessageBase dataObject = messageobject.DataObject;
		ChatCommand val = (ChatCommand)(object)((dataObject is ChatCommand) ? dataObject : null);
		if (val != null)
		{
			HandleChatCommand(val);
		}
		MessageBase dataObject2 = messageobject.DataObject;
		RequestPlayfieldList val2 = (RequestPlayfieldList)(object)((dataObject2 is RequestPlayfieldList) ? dataObject2 : null);
		if (val2 != null)
		{
			HandleRequestPlayfieldList(val2);
		}
	}

	private void HandleRequestPlayfieldList(RequestPlayfieldList requestPlayfieldList)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		requestPlayfieldList.ZoneEngineAddress = ((ServerBase)this).TcpEndPoint.Address.ToString();
		lock (playfields)
		{
			requestPlayfieldList.PlayfieldIds.Clear();
			foreach (Playfield playfield in playfields)
			{
				requestPlayfieldList.PlayfieldIds.Add(((PooledObject)playfield).Identity);
			}
		}
		Program.ISComClient.Send((MessageBase)(object)requestPlayfieldList);
	}

	protected override IClient CreateClient(IPAddress address)
	{
		bool flag = false;
		if (address != null)
		{
			lock (connectDelayList)
			{
				flag = connectDelayList.Any((KeyValuePair<IPAddress, DateTime> x) => x.Key.Equals(address) && x.Value < DateTime.UtcNow);
			}
		}
		if (flag)
		{
			Thread.Sleep(1000);
		}
		return (IClient)(object)new ZoneClient(this, (IMessageSerializer)(object)messageSerializer, zoneBus);
	}

	protected IPlayfield CreatePlayfield(Identity playfieldIdentity)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		lock (playfields)
		{
			foreach (IPlayfield playfield in playfields)
			{
				if (((IEntity)playfield).Identity == playfieldIdentity)
				{
					return playfield;
				}
			}
			return CreatePlayfieldUnlocked(playfieldIdentity);
		}
	}

	private IPlayfield CreatePlayfieldUnlocked(Identity playfieldIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		Playfield playfield = new Playfield(this, playfieldIdentity);
		playfields.Add((IPlayfield)(object)playfield);
		return (IPlayfield)(object)playfield;
	}

	protected override void OnReceiveUDP(int num_bytes, byte[] buf, IPEndPoint ip)
	{
		throw new NotImplementedException();
	}

	protected override void OnSendTo(IPEndPoint clientIP, int num_bytes)
	{
		throw new NotImplementedException();
	}

	private void HandleChatCommand(ChatCommand chatCommand)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		Pool instance = Pool.Instance;
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = chatCommand.CharacterId;
		ICharacter @object = (ICharacter)(object)instance.GetObject<Character>(val);
		if (@object == null)
		{
			return;
		}
		string text = ChatCommandText.Normalize(chatCommand.ChatCommandString);
		if (!string.IsNullOrWhiteSpace(text))
		{
			string[] array = text.Trim().Split(' ');
			string text2 = array[0].ToLower();
			if (text2 == "sit" || text2 == "stand")
			{
				new Posture().ExecuteCommand(@object, ((ITargetingEntity)@object).SelectedTarget, array);
			}
			else
			{
				ScriptCompiler.Instance.CallChatCommand(text2, ((IDynel)@object).Controller.Client, ((ITargetingEntity)@object).SelectedTarget, array);
			}
		}
	}

	private void ZoneServerClientDisconnected(IClient client, bool forced)
	{
		ZoneClient zoneClient = (ZoneClient)(object)client;
		IPAddress address = ((ClientBase)zoneClient).ClientAddress;
		if (address != null)
		{
			lock (connectDelayList)
			{
				if (connectDelayList.Any((KeyValuePair<IPAddress, DateTime> x) => x.Key.Equals(address)))
				{
					KeyValuePair<IPAddress, DateTime> keyValuePair = connectDelayList.First((KeyValuePair<IPAddress, DateTime> x) => x.Key.Equals(address));
					connectDelayList[keyValuePair.Key] = DateTime.UtcNow + TimeSpan.FromSeconds(2.0);
				}
				else
				{
					connectDelayList.Add(address, DateTime.UtcNow + TimeSpan.FromSeconds(2.0));
				}
			}
		}
		if (zoneClient != null)
		{
			((ClientBase)zoneClient).Dispose();
		}
	}

	public override void Stop()
	{
		lock (playfields)
		{
			foreach (Playfield playfield in playfields)
			{
				((PooledObject)playfield).Dispose();
			}
		}
		((ServerBase)this).Stop();
	}
}
