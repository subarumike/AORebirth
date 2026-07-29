using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Textures;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class AppearanceUpdateMessageHandler : BaseMessageHandler<AppearanceUpdateMessage, AppearanceUpdateMessageHandler>
{
	private static MessageDataFiller<AppearanceUpdateMessage> Filler(ICharacter iCharacter)
	{
		return delegate(AppearanceUpdateMessage message)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Expected O, but got Unknown
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_011d: Expected O, but got Unknown
			//IL_015c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0163: Expected O, but got Unknown
			//IL_0212: Unknown result type (might be due to invalid IL or missing references)
			//IL_0217: Unknown result type (might be due to invalid IL or missing references)
			//IL_0225: Unknown result type (might be due to invalid IL or missing references)
			//IL_0233: Unknown result type (might be due to invalid IL or missing references)
			//IL_0240: Expected O, but got Unknown
			Character val = (Character)iCharacter;
			((N3Message)message).Identity = ((PooledObject)val).Identity;
			((N3Message)message).Unknown = 0;
			int num = 0;
			List<AOTextures> list = new List<AOTextures>();
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			int value;
			bool flag;
			bool flag2;
			List<AOMeshs> meshs;
			lock (val)
			{
				value = ((Dynel)val).Stats[(StatIds)673].Value;
				flag = (value & 0x40) > 0;
				flag2 = (value & 0x20) > 0;
				num = ((Dynel)val).Stats[(StatIds)12].Value;
				Identity identity = ((IEntity)((Dynel)val).Playfield).Identity;
				int instance = ((Identity)(ref identity)).Instance;
				foreach (int key in val.SocialTab.Keys)
				{
					dictionary.Add(key, val.SocialTab[key]);
				}
				foreach (AOTextures texture in ((Dynel)val).Textures)
				{
					list.Add(new AOTextures(texture.place, texture.Texture));
				}
				meshs = MeshLayers.GetMeshs(val, flag2, flag);
			}
			List<Texture> list2 = new List<Texture>();
			AOTextures val2 = new AOTextures(0, 0);
			for (int i = 0; i < 5; i++)
			{
				val2.Texture = 0;
				val2.place = i;
				for (int j = 0; j < list.Count; j++)
				{
					if (list[j].place == i)
					{
						val2.Texture = list[j].Texture;
						break;
					}
				}
				if (flag2)
				{
					if (flag)
					{
						val2.Texture = dictionary[i];
					}
					else if (dictionary[i] != 0)
					{
						val2.Texture = dictionary[i];
					}
				}
				list2.Add(new Texture
				{
					Place = val2.place,
					Id = val2.Texture,
					Unknown = 0
				});
			}
			message.Textures = list2.ToArray();
			message.Meshes = ((IEnumerable<AOMeshs>)meshs).Select((Func<AOMeshs, Mesh>)((AOMeshs mesh) => new Mesh
			{
				Position = (byte)mesh.Position,
				Id = (uint)mesh.Mesh,
				OverrideTextureId = mesh.OverrideTexture,
				Layer = (byte)mesh.Layer
			})).ToArray();
			message.VisualFlags = (short)value;
			message.Unknown1 = 0;
		};
	}

	public void Send(ICharacter character)
	{
		((AbstractMessageHandler<AppearanceUpdateMessage>)(object)this).Send(character, Filler(character), true);
	}
}
