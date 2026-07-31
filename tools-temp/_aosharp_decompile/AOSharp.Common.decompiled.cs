using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
using AOSharp.Common.GameData;
using AOSharp.Common.GameData.UI;
using AOSharp.Common.Helpers;
using AOSharp.Common.Unmanaged.DataTypes;
using AOSharp.Common.Unmanaged.DbObjects;
using AOSharp.Common.Unmanaged.Imports;
using AOSharp.Common.Unmanaged.Imports.DatabaseController;
using AOSharp.Common.Unmanaged.Imports.GameData;
using AOSharp.Common.Unmanaged.Interfaces;
using Microsoft.CodeAnalysis;
using SmokeLounge.AOtomation.Messaging.Exceptions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.ChatMessages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Serialization;
using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;
using SmokeLounge.AOtomation.Messaging.Serialization.Serializers;
using SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: AssemblyTitle("AOSharp.Common")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("AOSharp.Common")]
[assembly: AssemblyCopyright("Copyright ©  2018")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("2f48116b-5d7e-449c-a05a-7d82ea7169a3")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("1.0.0.0")]
[module: UnverifiableCode]
namespace Microsoft.CodeAnalysis
{
	[CompilerGenerated]
	[Embedded]
	internal sealed class EmbeddedAttribute : Attribute
	{
	}
}
namespace System.Runtime.CompilerServices
{
	[CompilerGenerated]
	[Embedded]
	internal sealed class IsUnmanagedAttribute : Attribute
	{
	}
}
public enum CloakStatus
{
	Unknown = 0,
	Disabled = -1,
	Enabled = 1
}
namespace SmokeLounge.AOtomation.Messaging.Serialization
{
	public enum ArraySizeType
	{
		NoSerialization,
		Byte,
		Int16,
		Int32,
		X3F1,
		NullTerminated
	}
	public class DebuggingSerializerResolverBuilder<T> : SerializerResolverBuilder<T>
	{
		internal override ISerializer GetSerializer(Type type)
		{
			ISerializer serializer = base.GetSerializer(type);
			return new DiagnosticSerializer(serializer);
		}
	}
	public class DiagnosticInfo
	{
		private readonly List<DiagnosticInfo> diagnosticInfos;

		public IEnumerable<DiagnosticInfo> DiagnosticInfos => diagnosticInfos;

		public long Length { get; set; }

		public long Offset { get; set; }

		public PropertyMetaData PropertyMetaData { get; set; }

		public object Value { get; set; }

		public DiagnosticInfo()
		{
			diagnosticInfos = new List<DiagnosticInfo>();
		}

		public void Add(DiagnosticInfo diagnosticInfo)
		{
			diagnosticInfos.Add(diagnosticInfo);
		}
	}
	public enum FlagsCriteria
	{
		HasAll,
		HasAny,
		EqualsToAny,
		Default
	}
	public enum IdentifierType
	{
		Byte,
		Int16,
		Int32
	}
	public interface ISerializer
	{
		Type Type { get; }

		object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null);

		Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData);

		void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null);

		Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData);
	}
	public class KnownType
	{
		private readonly IdentifierType identifierType;

		private readonly int offset;

		public IdentifierType IdentifierType => identifierType;

		public int Offset => offset;

		public KnownType(int offset, IdentifierType identifierType)
		{
			this.offset = offset;
			this.identifierType = identifierType;
		}
	}
	public class MemberOptions
	{
		private readonly int fixedSizeLength;

		private readonly bool isFixedSize;

		private readonly int padAfter;

		private readonly int padBefore;

		private readonly ArraySizeType serializeSize;

		private readonly Type type;

		private readonly AoUsesFlagsAttribute[] usesFlagsAttributes;

		public int FixedSizeLength => fixedSizeLength;

		public bool IsFixedSize => isFixedSize;

		public int PadAfter => padAfter;

		public int PadBefore => padBefore;

		public ArraySizeType SerializeSize => serializeSize;

		public Type Type => type;

		public AoUsesFlagsAttribute[] UsesFlagsAttributes => usesFlagsAttributes;

		public MemberOptions(Type type, bool isFixedSize, int fixedSizeLength, ArraySizeType serializeSize, int padAfter, int padBefore, AoUsesFlagsAttribute[] usesFlagsAttributes)
		{
			this.type = type;
			this.isFixedSize = isFixedSize;
			this.fixedSizeLength = fixedSizeLength;
			this.serializeSize = serializeSize;
			this.padAfter = padAfter;
			this.padBefore = padBefore;
			this.usesFlagsAttributes = usesFlagsAttributes;
		}
	}
	public class ChatMessageSerializer
	{
		private readonly ChatHeaderSerializer headerSerializer;

		private readonly PacketInspector packetInspector;

		private readonly SerializerResolver serializerResolver;

		public ChatMessageSerializer()
		{
			packetInspector = new PacketInspector(new TypeInfo(typeof(ChatMessageBody)));
			serializerResolver = new SerializerResolverBuilder<ChatMessageBody>().Build();
			headerSerializer = new ChatHeaderSerializer();
		}

		public ChatMessageSerializer(SerializerResolverBuilder serializerResolverBuilder)
		{
			packetInspector = new PacketInspector(new TypeInfo(typeof(ChatMessageBody)));
			serializerResolver = serializerResolverBuilder.Build();
			headerSerializer = new ChatHeaderSerializer();
		}

		public ChatMessage Deserialize(Stream stream)
		{
			SerializationContext serializationContext;
			return Deserialize(stream, out serializationContext);
		}

		public ChatMessage Deserialize(byte[] message)
		{
			using MemoryStream stream = new MemoryStream(message);
			return Deserialize(stream);
		}

		public ChatMessage Deserialize(Stream stream, out SerializationContext serializationContext)
		{
			serializationContext = null;
			StreamReader streamReader = new StreamReader(stream)
			{
				Position = 0L
			};
			int identifier;
			TypeInfo typeInfo = packetInspector.FindSubType(streamReader, out identifier);
			if (typeInfo == null)
			{
				return null;
			}
			ISerializer serializer = serializerResolver.GetSerializer(typeInfo.Type);
			if (serializer == null)
			{
				return null;
			}
			streamReader.Position = 0L;
			serializationContext = new SerializationContext(serializerResolver);
			return new ChatMessage
			{
				Header = (ChatHeader)headerSerializer.Deserialize(streamReader, serializationContext),
				Body = (ChatMessageBody)serializer.Deserialize(streamReader, serializationContext)
			};
		}

		public void Serialize(Stream stream, ChatMessage message)
		{
			Serialize(stream, message, out var _);
		}

		public void Serialize(Stream stream, ChatMessage message, out SerializationContext serializationContext)
		{
			serializationContext = null;
			ISerializer serializer = serializerResolver.GetSerializer(message.Body.GetType());
			if (serializer != null)
			{
				serializationContext = new SerializationContext(serializerResolver);
				StreamWriter streamWriter = new StreamWriter(stream)
				{
					Position = 0L
				};
				headerSerializer.Serialize(streamWriter, serializationContext, message.Header);
				serializer.Serialize(streamWriter, serializationContext, message.Body);
				long position = streamWriter.Position;
				streamWriter.Position = 2L;
				streamWriter.WriteInt16((short)(position - 4));
			}
		}
	}
	public class MessageSerializer
	{
		private readonly HeaderSerializer headerSerializer;

		private readonly PacketInspector packetInspector;

		private readonly SerializerResolver serializerResolver;

		public MessageSerializer()
		{
			packetInspector = new PacketInspector(new TypeInfo(typeof(MessageBody)));
			serializerResolver = new SerializerResolverBuilder<MessageBody>().Build();
			headerSerializer = new HeaderSerializer();
		}

		public MessageSerializer(SerializerResolverBuilder serializerResolverBuilder)
		{
			packetInspector = new PacketInspector(new TypeInfo(typeof(MessageBody)));
			serializerResolver = serializerResolverBuilder.Build();
			headerSerializer = new HeaderSerializer();
		}

		public Message Deserialize(Stream stream)
		{
			SerializationContext serializationContext;
			return Deserialize(stream, out serializationContext);
		}

		public Message Deserialize(byte[] datablock)
		{
			using MemoryStream stream = new MemoryStream(datablock);
			return Deserialize(stream);
		}

		public Message Deserialize(Stream stream, out SerializationContext serializationContext)
		{
			serializationContext = null;
			StreamReader streamReader = new StreamReader(stream)
			{
				Position = 0L
			};
			int identifier;
			TypeInfo typeInfo = packetInspector.FindSubType(streamReader, out identifier);
			if (typeInfo == null)
			{
				return null;
			}
			ISerializer serializer = serializerResolver.GetSerializer(typeInfo.Type);
			if (serializer == null)
			{
				return null;
			}
			streamReader.Position = 0L;
			serializationContext = new SerializationContext(serializerResolver);
			return new Message
			{
				Header = (Header)headerSerializer.Deserialize(streamReader, serializationContext),
				Body = (MessageBody)serializer.Deserialize(streamReader, serializationContext),
				RawPacket = streamReader.ReadAll()
			};
		}

		public MessageBody DeserializeDatablock(Stream stream)
		{
			SerializationContext serializationContext = null;
			using StreamReader streamReader = new StreamReader(stream)
			{
				Position = 0L
			};
			int identifier;
			TypeInfo typeInfo = packetInspector.FindSubType(streamReader, out identifier);
			if (typeInfo == null)
			{
				return null;
			}
			ISerializer serializer = serializerResolver.GetSerializer(typeInfo.Type);
			if (serializer == null)
			{
				return null;
			}
			streamReader.Position = 16L;
			serializationContext = new SerializationContext(serializerResolver);
			return (MessageBody)serializer.Deserialize(streamReader, serializationContext);
		}

		public void Serialize(Stream stream, Message message)
		{
			Serialize(stream, message, out var _);
		}

		public void Serialize(Stream stream, Message message, out SerializationContext serializationContext)
		{
			serializationContext = null;
			ISerializer serializer = serializerResolver.GetSerializer(message.Body.GetType());
			if (serializer != null)
			{
				serializationContext = new SerializationContext(serializerResolver);
				StreamWriter streamWriter = new StreamWriter(stream)
				{
					Position = 0L
				};
				headerSerializer.Serialize(streamWriter, serializationContext, message.Header);
				serializer.Serialize(streamWriter, serializationContext, message.Body);
				int num = (int)streamWriter.Position;
				int num2 = ((num % 4 != 0) ? (4 - num % 4) : 0);
				for (int i = 0; i < num2; i++)
				{
					streamWriter.WriteByte(0);
				}
				streamWriter.Position = 6L;
				streamWriter.WriteInt16((short)num);
			}
		}
	}
	public class PacketInspector
	{
		private readonly TypeInfo typeInfo;

		public PacketInspector(TypeInfo typeInfo)
		{
			this.typeInfo = typeInfo;
		}

		public TypeInfo FindSubType(StreamReader reader, out int identifier)
		{
			identifier = 0;
			TypeInfo typeInfo = this.typeInfo;
			while (typeInfo != null)
			{
				if (typeInfo.KnownType == null)
				{
					return typeInfo;
				}
				reader.Position = typeInfo.KnownType.Offset;
				switch (typeInfo.KnownType.IdentifierType)
				{
				case IdentifierType.Byte:
					identifier = reader.ReadByte();
					break;
				case IdentifierType.Int16:
					identifier = reader.ReadInt16();
					break;
				case IdentifierType.Int32:
					identifier = reader.ReadInt32();
					break;
				default:
					return null;
				}
				TypeInfo subType = typeInfo.GetSubType(identifier);
				if (subType == null)
				{
					return null;
				}
				typeInfo = subType;
			}
			return null;
		}
	}
	public class Probe
	{
		private readonly DiagnosticInfo diagnosticInfo;

		private readonly Probe parent;

		public DiagnosticInfo DiagnosticInfo => diagnosticInfo;

		public Probe Parent => parent;

		public Probe(Probe parent = null)
		{
			this.parent = parent;
			diagnosticInfo = new DiagnosticInfo();
		}
	}
	public class PropertyMetaData
	{
		private readonly AoFlagsAttribute flagsAttribute;

		private readonly MemberOptions options;

		private readonly PropertyInfo propertyInfo;

		private readonly AoUsesFlagsAttribute[] usesFlagsAttributes;

		public AoFlagsAttribute FlagsAttribute => flagsAttribute;

		public MemberOptions Options => options;

		public PropertyInfo Property => propertyInfo;

		public Type Type => propertyInfo.PropertyType;

		public AoUsesFlagsAttribute[] UsesFlagsAttributes => usesFlagsAttributes;

		public PropertyMetaData(PropertyInfo propertyInfo, AoMemberAttribute memberAttribute, AoFlagsAttribute flagsAttribute, AoUsesFlagsAttribute[] usesFlagsAttributes)
		{
			this.propertyInfo = propertyInfo;
			this.flagsAttribute = flagsAttribute;
			this.usesFlagsAttributes = usesFlagsAttributes;
			options = new MemberOptions(this.propertyInfo.PropertyType, memberAttribute.IsFixedSize, memberAttribute.FixedSizeLength, memberAttribute.SerializeSize, memberAttribute.PadAfter, memberAttribute.PadBefore, usesFlagsAttributes);
		}
	}
	public static class ReflectionHelper
	{
		public static MethodInfo GetMethodInfo<TSource, TSignature>(Expression<Func<TSource, TSignature>> lambdaExpression)
		{
			if (!(lambdaExpression.Body is UnaryExpression unaryExpression))
			{
				throw new InvalidOperationException();
			}
			if (!(unaryExpression.Operand is MethodCallExpression methodCallExpression))
			{
				throw new InvalidOperationException();
			}
			if (methodCallExpression.Arguments.Count < 2)
			{
				throw new InvalidOperationException();
			}
			if (!(methodCallExpression.Object is ConstantExpression constantExpression))
			{
				throw new InvalidOperationException();
			}
			MethodInfo methodInfo = constantExpression.Value as MethodInfo;
			if (methodInfo == null)
			{
				throw new InvalidOperationException();
			}
			return methodInfo;
		}

		public static PropertyInfo GetPropertyInfo<TSource>(Expression<Func<TSource, object>> propertyExpression)
		{
			if (propertyExpression == null)
			{
				throw new InvalidOperationException();
			}
			MemberExpression memberExpression = ((!(propertyExpression.Body is UnaryExpression unaryExpression)) ? (propertyExpression.Body as MemberExpression) : (unaryExpression.Operand as MemberExpression));
			if (memberExpression == null)
			{
				throw new InvalidOperationException();
			}
			PropertyInfo propertyInfo = memberExpression.Member as PropertyInfo;
			if (propertyInfo == null)
			{
				throw new InvalidOperationException();
			}
			return propertyInfo;
		}

		public static bool IsStruct(Type type)
		{
			return type.IsValueType && !type.IsPrimitive && !type.IsEnum;
		}
	}
	public class SerializationContext
	{
		private readonly List<DiagnosticInfo> diagnosticInfos;

		private readonly IDictionary<string, int> flags;

		private readonly SerializerResolver serializerResolver;

		private Probe probe;

		public IEnumerable<DiagnosticInfo> DiagnosticInfos => diagnosticInfos;

		public SerializationContext(SerializerResolver serializerResolver)
		{
			this.serializerResolver = serializerResolver;
			diagnosticInfos = new List<DiagnosticInfo>();
			flags = new Dictionary<string, int>();
		}

		public Probe BeginProbe()
		{
			probe = new Probe(probe);
			return probe;
		}

		public void EndProbe(Probe probe)
		{
			this.probe = probe.Parent;
			if (this.probe != null)
			{
				this.probe.DiagnosticInfo.Add(probe.DiagnosticInfo);
			}
			else
			{
				diagnosticInfos.Add(probe.DiagnosticInfo);
			}
		}

		public AoUsesFlagsAttribute Evaluate(IEnumerable<AoUsesFlagsAttribute> usesFlags)
		{
			return usesFlags.FirstOrDefault(Evaluate);
		}

		public int GetFlagValue(string flag)
		{
			flags.TryGetValue(flag, out var value);
			return value;
		}

		public void SetFlagValue(string flag, int value)
		{
			flags[flag] = value;
		}

		internal object Deserialize(StreamReader streamReader, PropertyMetaData propertyMetaData)
		{
			if (!propertyMetaData.UsesFlagsAttributes.Any())
			{
				return null;
			}
			AoUsesFlagsAttribute aoUsesFlagsAttribute = Evaluate(propertyMetaData.UsesFlagsAttributes);
			if (aoUsesFlagsAttribute == null)
			{
				return null;
			}
			ISerializer serializer = serializerResolver.GetSerializer(aoUsesFlagsAttribute.Type);
			object obj = serializer.Deserialize(streamReader, this, propertyMetaData);
			if (propertyMetaData.Type.IsValueType)
			{
				if (propertyMetaData.Type.IsPrimitive)
				{
					return Convert.ChangeType(obj, propertyMetaData.Type);
				}
				if (propertyMetaData.Type.IsEnum)
				{
					return Convert.ChangeType(obj, Enum.GetUnderlyingType(propertyMetaData.Type));
				}
			}
			return obj;
		}

		internal void Serialize(StreamWriter streamWriter, object obj, PropertyMetaData propertyMetaData)
		{
			ISerializer serializer;
			if (!propertyMetaData.UsesFlagsAttributes.Any())
			{
				serializer = serializerResolver.GetSerializer(obj.GetType());
			}
			else
			{
				AoUsesFlagsAttribute aoUsesFlagsAttribute = Evaluate(propertyMetaData.UsesFlagsAttributes);
				if (aoUsesFlagsAttribute == null)
				{
					return;
				}
				serializer = serializerResolver.GetSerializer(aoUsesFlagsAttribute.Type);
			}
			if (propertyMetaData.Type.IsValueType && (propertyMetaData.Type.IsPrimitive || propertyMetaData.Type.IsEnum))
			{
				obj = Convert.ChangeType(obj, serializer.Type);
			}
			serializer.Serialize(streamWriter, this, obj, propertyMetaData);
		}

		private bool Evaluate(AoUsesFlagsAttribute usesFlags)
		{
			return usesFlags.Criteria switch
			{
				FlagsCriteria.HasAll => EvaluateHasAll(usesFlags), 
				FlagsCriteria.HasAny => EvaluateHasAny(usesFlags), 
				FlagsCriteria.EqualsToAny => EvaluateEqualsToAny(usesFlags), 
				FlagsCriteria.Default => true, 
				_ => throw new ArgumentOutOfRangeException(), 
			};
		}

		private bool EvaluateEqualsToAny(AoUsesFlagsAttribute usesFlags)
		{
			int flagValue = GetFlagValue(usesFlags.Flag);
			return usesFlags.CriteriaValues.Any((int v) => v == flagValue);
		}

		private bool EvaluateHasAll(AoUsesFlagsAttribute usesFlags)
		{
			int flagValue = GetFlagValue(usesFlags.Flag);
			return (flagValue & usesFlags.CriteriaValue) == usesFlags.CriteriaValue;
		}

		private bool EvaluateHasAny(AoUsesFlagsAttribute usesFlags)
		{
			int flagValue = GetFlagValue(usesFlags.Flag);
			return (flagValue & usesFlags.CriteriaValue) > 0;
		}
	}
	public class SerializerResolver
	{
		private readonly SerializerResolverBuilder serializerResolverBuilder;

		public SerializerResolver(SerializerResolverBuilder serializerResolverBuilder)
		{
			this.serializerResolverBuilder = serializerResolverBuilder;
		}

		public void Add(Type type, ISerializer serializer)
		{
			throw new NotImplementedException();
		}

		public ISerializer GetSerializer(Type type)
		{
			return serializerResolverBuilder.GetSerializer(type);
		}
	}
	public abstract class SerializerResolverBuilder
	{
		public abstract SerializerResolver Build();

		internal abstract ISerializer GetSerializer(Type type);
	}
	public class SerializerResolverBuilder<T> : SerializerResolverBuilder
	{
		private readonly ConcurrentDictionary<Type, ISerializer> serializers;

		public SerializerResolverBuilder()
		{
			serializers = new ConcurrentDictionary<Type, ISerializer>();
			serializers.TryAdd(typeof(bool), new BoolSerializer());
			serializers.TryAdd(typeof(byte), new ByteSerializer());
			serializers.TryAdd(typeof(short), new Int16Serializer());
			serializers.TryAdd(typeof(int), new Int32Serializer());
			serializers.TryAdd(typeof(long), new Int64Serializer());
			serializers.TryAdd(typeof(IPAddress), new IPAddressSerializer());
			serializers.TryAdd(typeof(float), new SingleSerializer());
			serializers.TryAdd(typeof(string), new StringSerializer());
			serializers.TryAdd(typeof(ushort), new UInt16Serializer());
			serializers.TryAdd(typeof(uint), new UInt32Serializer());
			serializers.TryAdd(typeof(PlayfieldVendorInfo), new PlayfieldVendorInfoSerializer());
			serializers.TryAdd(typeof(SimpleCharFullUpdateMessage), new SimpleCharFullUpdateSerializer());
			serializers.TryAdd(typeof(GroupMsgMessage), new GroupMessageSerializer());
			serializers.TryAdd(typeof(PlayfieldTowerUpdateClientMessage), new PlayfieldTowerUpdateClientSerializer());
			serializers.TryAdd(typeof(InspectMessage), new InspectSerializer());
		}

		public override SerializerResolver Build()
		{
			Type typeFromHandle = typeof(T);
			IEnumerable<Type> enumerable = typeFromHandle.Assembly.GetTypes().Where(typeFromHandle.IsAssignableFrom);
			foreach (Type item in enumerable)
			{
				if (!serializers.ContainsKey(item))
				{
					ISerializer serializer = CreateSerializer(item);
					if (serializer != null)
					{
						serializers.TryAdd(item, serializer);
					}
				}
			}
			return new SerializerResolver(this);
		}

		internal override ISerializer GetSerializer(Type type)
		{
			if (serializers.TryGetValue(type, out var value))
			{
				return value;
			}
			if (type.IsEnum)
			{
				Type enumUnderlyingType = type.GetEnumUnderlyingType();
				if (serializers.TryGetValue(enumUnderlyingType, out value))
				{
					return value;
				}
			}
			if (type.IsArray)
			{
				Type elementType = type.GetElementType();
				value = GetSerializer(elementType);
				if (value == null)
				{
					return null;
				}
				ArraySerializer arraySerializer = new ArraySerializer(type, value);
				serializers.TryAdd(type, arraySerializer);
				return arraySerializer;
			}
			value = CreateSerializer(type);
			if (value != null)
			{
				serializers.TryAdd(type, value);
			}
			return value;
		}

		private ISerializer CreateSerializer(Type type)
		{
			if (type.IsAbstract)
			{
				return null;
			}
			TypeSerializerBuilder typeSerializerBuilder = new TypeSerializerBuilder(type, GetSerializer);
			return new TypeSerializer(type, typeSerializerBuilder);
		}
	}
	public sealed class StreamReader : IDisposable
	{
		private readonly BinaryReader reader;

		private readonly Stream stream;

		public long Position
		{
			get
			{
				return stream.Position;
			}
			set
			{
				stream.Position = value;
			}
		}

		public StreamReader(Stream stream)
		{
			this.stream = stream;
			reader = new BinaryReader(stream);
		}

		public void Dispose()
		{
			reader.Dispose();
			stream.Dispose();
		}

		public bool ReadBool()
		{
			return reader.ReadBoolean();
		}

		public byte ReadByte()
		{
			return reader.ReadByte();
		}

		public byte[] ReadBytes(int count)
		{
			return reader.ReadBytes(count);
		}

		public short ReadInt16()
		{
			return IPAddress.NetworkToHostOrder(reader.ReadInt16());
		}

		public int ReadInt32()
		{
			return IPAddress.NetworkToHostOrder(reader.ReadInt32());
		}

		public long ReadInt64()
		{
			return IPAddress.NetworkToHostOrder(reader.ReadInt64());
		}

		public float ReadSingle()
		{
			byte[] array = reader.ReadBytes(4);
			Array.Reverse(array);
			return BitConverter.ToSingle(array, 0);
		}

		public string ReadString(int length)
		{
			byte[] bytes = reader.ReadBytes(length);
			return Encoding.ASCII.GetString(bytes).TrimEnd(default(char));
		}

		public ushort ReadUInt16()
		{
			int network = reader.ReadUInt16() << 16;
			return (ushort)IPAddress.NetworkToHostOrder(network);
		}

		public uint ReadUInt32()
		{
			uint num = reader.ReadUInt32();
			return (uint)(IPAddress.NetworkToHostOrder(num) >> 32);
		}

		public string ReadNullTerminatedString()
		{
			string text = "";
			byte value;
			while ((value = reader.ReadByte()) != 0)
			{
				text += Convert.ToChar(value);
			}
			return text;
		}

		public int PeekNullTermStringLength()
		{
			long position = reader.BaseStream.Position;
			while (reader.ReadByte() != 0)
			{
			}
			long num = reader.BaseStream.Position - position;
			reader.BaseStream.Position = position;
			return (int)num;
		}

		public int PeekUntilEnd()
		{
			long position = reader.BaseStream.Position;
			int num = 0;
			while (reader.BaseStream.Position != reader.BaseStream.Length)
			{
				reader.ReadByte();
				num++;
			}
			reader.BaseStream.Position = position;
			return num;
		}

		public byte[] ReadAll()
		{
			reader.BaseStream.Position = 0L;
			return reader.ReadBytes((int)reader.BaseStream.Length);
		}
	}
	public sealed class StreamWriter : IDisposable
	{
		private readonly Stream stream;

		private readonly BinaryWriter writer;

		public long Position
		{
			get
			{
				return stream.Position;
			}
			set
			{
				stream.Position = value;
			}
		}

		public StreamWriter(Stream stream)
		{
			this.stream = stream;
			writer = new BinaryWriter(this.stream);
		}

		public void Dispose()
		{
			writer.Dispose();
			stream.Dispose();
		}

		public void WriteBool(bool value)
		{
			writer.Write(value);
		}

		public void WriteByte(byte value)
		{
			writer.Write(value);
		}

		public void WriteBytes(byte[] buffer)
		{
			writer.Write(buffer);
		}

		public void WriteInt16(short value)
		{
			writer.Write(IPAddress.HostToNetworkOrder(value));
		}

		public void WriteInt32(int value)
		{
			writer.Write(IPAddress.HostToNetworkOrder(value));
		}

		public void WriteInt64(long value)
		{
			writer.Write(IPAddress.HostToNetworkOrder(value));
		}

		public void WriteSingle(float value)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			Array.Reverse(bytes);
			writer.Write(bytes);
		}

		public void WriteString(string str, int? padToLength = null)
		{
			byte[] array = new byte[padToLength ?? str.Length];
			int charCount = ((str.Length > array.Length) ? array.Length : str.Length);
			Encoding.ASCII.GetBytes(str, 0, charCount, array, 0);
			writer.Write(array);
		}

		public void WriteUInt16(ushort value)
		{
			int num = IPAddress.HostToNetworkOrder(value) >> 16;
			writer.Write((ushort)num);
		}

		public void WriteUInt32(uint value)
		{
			long num = IPAddress.HostToNetworkOrder(value) >> 32;
			writer.Write((uint)num);
		}
	}
	public class TypeInfo
	{
		private readonly Dictionary<int, TypeInfo> subTypes;

		private readonly Type type;

		public KnownType KnownType { get; set; }

		public Type Type => type;

		public TypeInfo(Type type)
		{
			this.type = type;
			subTypes = new Dictionary<int, TypeInfo>();
			AoKnownTypeAttribute aoKnownTypeAttribute = this.type.GetCustomAttributes(typeof(AoKnownTypeAttribute), inherit: false).Cast<AoKnownTypeAttribute>().FirstOrDefault();
			if (aoKnownTypeAttribute != null)
			{
				KnownType = new KnownType(aoKnownTypeAttribute.Offset, aoKnownTypeAttribute.IdentifierType);
			}
			InitializeSubTypes();
		}

		public TypeInfo GetSubType(int identifier)
		{
			subTypes.TryGetValue(identifier, out var value);
			return value;
		}

		private void InitializeSubTypes()
		{
			InitializeSubTypesForAssembly(type.Assembly);
		}

		public void InitializeSubTypesForAssembly(Assembly assembly)
		{
			IEnumerable<Type> enumerable = from t in assembly.GetTypes()
				where t.BaseType == type
				select t;
			foreach (Type item in enumerable)
			{
				AoContractAttribute aoContractAttribute = item.GetCustomAttributes(typeof(AoContractAttribute), inherit: false).Cast<AoContractAttribute>().FirstOrDefault();
				if (aoContractAttribute != null)
				{
					TypeInfo typeInfo = new TypeInfo(item);
					if (subTypes.ContainsKey(aoContractAttribute.Identifier))
					{
						throw new ContractIdCollisionException($"Contracts must have unique identifiers. {typeInfo.Type.Name}({aoContractAttribute.Identifier}) shares the same identifier as {subTypes[aoContractAttribute.Identifier].Type.Name}({aoContractAttribute.Identifier})");
					}
					subTypes.Add(aoContractAttribute.Identifier, typeInfo);
				}
			}
		}
	}
	public class TypeSerializerBuilder
	{
		private readonly Lazy<PropertyMetaData[]> propertyMetas;

		private readonly Func<Type, ISerializer> serializerResolver;

		private readonly Type type;

		public TypeSerializerBuilder(Type type, Func<Type, ISerializer> serializerResolver)
		{
			this.type = type;
			this.serializerResolver = serializerResolver;
			propertyMetas = new Lazy<PropertyMetaData[]>(InitializePropertyMetas);
		}

		public Expression BuildDeserializer(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression)
		{
			ParameterExpression parameterExpression = Expression.Variable(type, "serializerVar");
			BinaryExpression item = Expression.Assign(parameterExpression, Expression.New(type));
			List<Expression> list = new List<Expression> { item };
			IEnumerable<Expression> collection = CreatePropertyDeserializers(streamReaderExpression, serializationContextExpression, parameterExpression);
			list.AddRange(collection);
			Expression expression = parameterExpression;
			if (ReflectionHelper.IsStruct(type))
			{
				expression = Expression.Convert(expression, typeof(object));
			}
			LabelTarget target = Expression.Label(typeof(object));
			GotoExpression item2 = Expression.Return(target, expression, typeof(object));
			LabelExpression item3 = Expression.Label(target, expression);
			list.Add(item2);
			list.Add(item3);
			BlockExpression body = Expression.Block(new ParameterExpression[1] { parameterExpression }, list);
			return Expression.Lambda<Func<StreamReader, SerializationContext, object>>(body, new ParameterExpression[2] { streamReaderExpression, serializationContextExpression });
		}

		public Expression BuildSerializer(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression)
		{
			ParameterExpression parameterExpression = Expression.Parameter(typeof(object), "object");
			ParameterExpression parameterExpression2 = Expression.Variable(type, "serializerVar");
			BinaryExpression item = Expression.Assign(parameterExpression2, Expression.Convert(parameterExpression, type));
			List<Expression> list = new List<Expression> { item };
			IEnumerable<Expression> collection = CreatePropertySerializers(streamWriterExpression, serializationContextExpression, parameterExpression2);
			list.AddRange(collection);
			BlockExpression body = Expression.Block(new ParameterExpression[1] { parameterExpression2 }, list);
			return Expression.Lambda<Action<StreamWriter, SerializationContext, object>>(body, new ParameterExpression[3] { streamWriterExpression, serializationContextExpression, parameterExpression });
		}

		private Expression CreatePropertyDeserializer(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, PropertyMetaData propertyMetaData, ParameterExpression deserializedObject)
		{
			Expression expression = Expression.Property(deserializedObject, propertyMetaData.Property);
			if (propertyMetaData.UsesFlagsAttributes.Any())
			{
				MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<SerializationContext, Func<StreamReader, PropertyMetaData, object>>>)((SerializationContext o) => o.Deserialize));
				Expression[] arguments = new Expression[2]
				{
					streamReaderExpression,
					Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
				};
				MethodCallExpression expression2 = Expression.Call(serializationContextExpression, methodInfo, arguments);
				return Expression.Assign(expression, Expression.Convert(expression2, propertyMetaData.Type));
			}
			ISerializer serializer = serializerResolver(propertyMetaData.Type);
			return serializer.DeserializerExpression(streamReaderExpression, serializationContextExpression, expression, propertyMetaData);
		}

		private IEnumerable<Expression> CreatePropertyDeserializers(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, ParameterExpression deserializedObject)
		{
			PropertyMetaData[] value = propertyMetas.Value;
			foreach (PropertyMetaData propertyMeta in value)
			{
				yield return CreatePropertyDeserializer(streamReaderExpression, serializationContextExpression, propertyMeta, deserializedObject);
				if (propertyMeta.FlagsAttribute != null)
				{
					Expression propertyExpression = Expression.Property(deserializedObject, propertyMeta.Property);
					MethodInfo setFlagValueMethodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<SerializationContext, Action<string, int>>>)((SerializationContext o) => o.SetFlagValue));
					yield return Expression.Call(serializationContextExpression, setFlagValueMethodInfo, Expression.Constant(propertyMeta.FlagsAttribute.Flag, typeof(string)), Expression.Convert(propertyExpression, typeof(int)));
				}
			}
		}

		private Expression CreatePropertySerializer(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, PropertyMetaData propertyMetaData, ParameterExpression objectToSerialize)
		{
			Expression expression = Expression.Property(objectToSerialize, propertyMetaData.Property);
			if (ReflectionHelper.IsStruct(propertyMetaData.Type))
			{
				expression = Expression.Convert(expression, typeof(object));
			}
			if (propertyMetaData.UsesFlagsAttributes.Any())
			{
				MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<SerializationContext, Action<StreamWriter, object, PropertyMetaData>>>)((SerializationContext o) => o.Serialize));
				Expression[] arguments = new Expression[3]
				{
					streamWriterExpression,
					Expression.Convert(expression, typeof(object)),
					Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
				};
				return Expression.Call(serializationContextExpression, methodInfo, arguments);
			}
			ISerializer serializer = serializerResolver(propertyMetaData.Type);
			return serializer.SerializerExpression(streamWriterExpression, serializationContextExpression, expression, propertyMetaData);
		}

		private IEnumerable<Expression> CreatePropertySerializers(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, ParameterExpression objectToSerialize)
		{
			PropertyMetaData[] value = propertyMetas.Value;
			foreach (PropertyMetaData propertyMeta in value)
			{
				if (propertyMeta.FlagsAttribute != null)
				{
					Expression propertyExpression = Expression.Property(objectToSerialize, propertyMeta.Property);
					MethodInfo setFlagValueMethodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<SerializationContext, Action<string, int>>>)((SerializationContext o) => o.SetFlagValue));
					yield return Expression.Call(serializationContextExpression, setFlagValueMethodInfo, Expression.Constant(propertyMeta.FlagsAttribute.Flag, typeof(string)), Expression.Convert(propertyExpression, typeof(int)));
				}
				yield return CreatePropertySerializer(streamWriterExpression, serializationContextExpression, propertyMeta, objectToSerialize);
			}
		}

		private PropertyMetaData[] InitializePropertyMetas()
		{
			Type baseType = type;
			Stack<Type> stack = new Stack<Type>();
			do
			{
				stack.Push(baseType);
				baseType = baseType.BaseType;
			}
			while (baseType != null);
			List<PropertyMetaData> list = new List<PropertyMetaData>();
			while (stack.Count > 0)
			{
				Type t = stack.Pop();
				IEnumerable<PropertyMetaData> collection = from property in t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
					let memberAttribute = property.GetCustomAttributes(typeof(AoMemberAttribute), inherit: false).Cast<AoMemberAttribute>().FirstOrDefault()
					where property.CanWrite && memberAttribute != null && property.DeclaringType == t
					orderby memberAttribute.Order
					let flagsAttribute = property.GetCustomAttributes(typeof(AoFlagsAttribute), inherit: false).Cast<AoFlagsAttribute>().FirstOrDefault()
					let usesFlagsAttribute = property.GetCustomAttributes(typeof(AoUsesFlagsAttribute), inherit: false).Cast<AoUsesFlagsAttribute>().ToArray()
					select new PropertyMetaData(property, memberAttribute, flagsAttribute, usesFlagsAttribute);
				list.AddRange(collection);
			}
			return list.ToArray();
		}
	}
}
namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers
{
	public class ArraySerializer : ISerializer
	{
		private readonly Type type;

		private readonly ISerializer typeSerializer;

		public Type Type => type;

		public ArraySerializer(Type type, ISerializer typeSerializer)
		{
			this.type = type;
			this.typeSerializer = typeSerializer;
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			int num;
			if (propertyMetaData.Options.SerializeSize != 0)
			{
				ArraySizeSerializer arraySizeSerializer = new ArraySizeSerializer(propertyMetaData.Options.SerializeSize);
				num = (int)arraySizeSerializer.Deserialize(streamReader, serializationContext, propertyMetaData);
			}
			else
			{
				num = propertyMetaData.Options.FixedSizeLength;
			}
			Array array = Array.CreateInstance(typeSerializer.Type, num);
			for (int i = 0; i < num; i++)
			{
				object value = typeSerializer.Deserialize(streamReader, serializationContext, propertyMetaData);
				array.SetValue(value, i);
			}
			return array;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			List<Expression> list = new List<Expression>();
			ParameterExpression parameterExpression = Expression.Variable(typeof(int), "size");
			Expression item = ((propertyMetaData.Options.SerializeSize == ArraySizeType.NoSerialization) ? Expression.Assign(parameterExpression, Expression.Constant(propertyMetaData.Options.FixedSizeLength, typeof(int))) : new ArraySizeSerializer(propertyMetaData.Options.SerializeSize).DeserializerExpression(streamReaderExpression, serializationContextExpression, parameterExpression, propertyMetaData));
			list.Add(item);
			NewArrayExpression right = Expression.NewArrayBounds(typeSerializer.Type, parameterExpression);
			ParameterExpression parameterExpression2 = Expression.Parameter(type, "newArray");
			list.Add(Expression.Assign(parameterExpression2, right));
			LabelTarget labelTarget = Expression.Label();
			ParameterExpression parameterExpression3 = Expression.Variable(typeof(int), "i");
			list.Add(Expression.Assign(parameterExpression3, Expression.Constant(0)));
			ParameterExpression parameterExpression4 = Expression.Variable(typeSerializer.Type, "element");
			BinaryExpression binaryExpression = Expression.Assign(Expression.ArrayAccess(parameterExpression2, parameterExpression3), parameterExpression4);
			ConditionalExpression body = Expression.IfThenElse(Expression.LessThan(parameterExpression3, parameterExpression), Expression.Block(new ParameterExpression[1] { parameterExpression4 }, typeSerializer.DeserializerExpression(streamReaderExpression, serializationContextExpression, parameterExpression4, propertyMetaData), binaryExpression, Expression.Assign(parameterExpression3, Expression.Increment(parameterExpression3))), Expression.Break(labelTarget));
			LoopExpression item2 = Expression.Loop(body, labelTarget);
			list.Add(item2);
			BinaryExpression item3 = Expression.Assign(assignmentTargetExpression, Expression.Convert(parameterExpression2, type));
			list.Add(item3);
			return Expression.Block(new ParameterExpression[3] { parameterExpression, parameterExpression2, parameterExpression3 }, list);
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			if (propertyMetaData.Options.SerializeSize != 0)
			{
				ArraySizeSerializer arraySizeSerializer = new ArraySizeSerializer(propertyMetaData.Options.SerializeSize);
				arraySizeSerializer.Serialize(streamWriter, serializationContext, value, propertyMetaData);
			}
			Array array = (Array)value;
			for (int i = 0; i < array.Length; i++)
			{
				typeSerializer.Serialize(streamWriter, serializationContext, array.GetValue(i), propertyMetaData);
			}
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			if (!valueExpression.Type.IsAssignableFrom(type))
			{
				valueExpression = Expression.Convert(valueExpression, type);
			}
			List<Expression> list = new List<Expression>();
			if (propertyMetaData.Options.SerializeSize != 0)
			{
				Expression item = new ArraySizeSerializer(propertyMetaData.Options.SerializeSize).SerializerExpression(streamWriterExpression, serializationContextExpression, valueExpression, propertyMetaData);
				list.Add(item);
			}
			LabelTarget labelTarget = Expression.Label();
			UnaryExpression right = Expression.ArrayLength(valueExpression);
			ParameterExpression parameterExpression = Expression.Variable(typeof(int), "i");
			list.Add(Expression.Assign(parameterExpression, Expression.Constant(0)));
			ParameterExpression parameterExpression2 = Expression.Variable(typeSerializer.Type, "element");
			BinaryExpression binaryExpression = Expression.Assign(parameterExpression2, Expression.ArrayIndex(valueExpression, parameterExpression));
			ConditionalExpression body = Expression.IfThenElse(Expression.LessThan(parameterExpression, right), Expression.Block(new ParameterExpression[1] { parameterExpression2 }, binaryExpression, typeSerializer.SerializerExpression(streamWriterExpression, serializationContextExpression, parameterExpression2, propertyMetaData), Expression.Assign(parameterExpression, Expression.Increment(parameterExpression))), Expression.Break(labelTarget));
			LoopExpression item2 = Expression.Loop(body, labelTarget);
			list.Add(item2);
			return Expression.Block(new ParameterExpression[1] { parameterExpression }, list);
		}
	}
	public class ArraySizeSerializer : ISerializer
	{
		private readonly ArraySizeType arraySizeType;

		private readonly Type type;

		public Type Type => type;

		public ArraySizeSerializer(ArraySizeType arraySizeType)
		{
			this.arraySizeType = arraySizeType;
			switch (this.arraySizeType)
			{
			case ArraySizeType.Byte:
				type = typeof(byte);
				break;
			case ArraySizeType.Int16:
				type = typeof(short);
				break;
			case ArraySizeType.Int32:
				type = typeof(int);
				break;
			case ArraySizeType.X3F1:
				type = typeof(int);
				break;
			}
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			switch (arraySizeType)
			{
			case ArraySizeType.NoSerialization:
				return null;
			case ArraySizeType.Byte:
				return (int)streamReader.ReadByte();
			case ArraySizeType.Int16:
				return (int)streamReader.ReadInt16();
			case ArraySizeType.Int32:
				return streamReader.ReadInt32();
			case ArraySizeType.X3F1:
			{
				int num = streamReader.ReadInt32();
				return num / 1009 - 1;
			}
			default:
				throw new ArgumentOutOfRangeException();
			}
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			if (arraySizeType == ArraySizeType.NoSerialization)
			{
				return null;
			}
			MethodInfo methodInfo = null;
			if (type == typeof(byte))
			{
				methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<byte>>>)((StreamReader o) => o.ReadByte));
			}
			if (type == typeof(short))
			{
				methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<short>>>)((StreamReader o) => o.ReadInt16));
			}
			if (type == typeof(int))
			{
				methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<int>>>)((StreamReader o) => o.ReadInt32));
			}
			if (methodInfo == null)
			{
				return null;
			}
			Expression expression = ((!propertyMetaData.Options.IsFixedSize) ? ((Expression)Expression.Call(streamReaderExpression, methodInfo)) : ((Expression)Expression.Constant(propertyMetaData.Options.FixedSizeLength, type)));
			if (arraySizeType == ArraySizeType.X3F1)
			{
				Expression left = expression;
				expression = Expression.Subtract(Expression.Divide(left, Expression.Constant(1009)), Expression.Constant(1));
			}
			return Expression.Assign(assignmentTargetExpression, (type == assignmentTargetExpression.Type) ? expression : Expression.Convert(expression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			if (arraySizeType != 0)
			{
				int num = ((value is Array array) ? array.Length : ((string)value).Length);
				switch (arraySizeType)
				{
				case ArraySizeType.NoSerialization:
					break;
				case ArraySizeType.Byte:
					streamWriter.WriteByte((byte)num);
					break;
				case ArraySizeType.Int16:
					streamWriter.WriteInt16((short)num);
					break;
				case ArraySizeType.Int32:
					streamWriter.WriteInt32(num);
					break;
				case ArraySizeType.X3F1:
					streamWriter.WriteInt32((num + 1) * 1009);
					break;
				default:
					throw new ArgumentOutOfRangeException();
				}
			}
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			if (arraySizeType == ArraySizeType.NoSerialization)
			{
				return null;
			}
			MethodInfo methodInfo = null;
			if (type == typeof(byte))
			{
				methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<byte>>>)((StreamWriter o) => o.WriteByte));
			}
			if (type == typeof(short))
			{
				methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<short>>>)((StreamWriter o) => o.WriteInt16));
			}
			if (type == typeof(int))
			{
				methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<int>>>)((StreamWriter o) => o.WriteInt32));
			}
			if (methodInfo == null)
			{
				return null;
			}
			Expression expression = ((!propertyMetaData.Options.IsFixedSize) ? ((Expression)Expression.Convert(Expression.Property(valueExpression, "Length"), type)) : ((Expression)Expression.Constant(propertyMetaData.Options.FixedSizeLength, type)));
			if (arraySizeType == ArraySizeType.X3F1)
			{
				Expression left = expression;
				expression = Expression.Multiply(Expression.Add(left, Expression.Constant(1)), Expression.Constant(1009));
			}
			return Expression.Call(streamWriterExpression, methodInfo, expression);
		}
	}
	public class ByteSerializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public ByteSerializer()
		{
			type = typeof(byte);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return streamReader.ReadByte();
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<byte>>>)((StreamReader o) => o.ReadByte));
			MethodCallExpression methodCallExpression = Expression.Call(streamReaderExpression, methodInfo);
			if (assignmentTargetExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Assign(assignmentTargetExpression, methodCallExpression);
			}
			return Expression.Assign(assignmentTargetExpression, Expression.Convert(methodCallExpression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			streamWriter.WriteByte((byte)value);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<byte>>>)((StreamWriter o) => o.WriteByte));
			if (valueExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Call(streamWriterExpression, methodInfo, valueExpression);
			}
			return Expression.Call(streamWriterExpression, methodInfo, Expression.Convert(valueExpression, type));
		}
	}
	public class BoolSerializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public BoolSerializer()
		{
			type = typeof(bool);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return streamReader.ReadBool();
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<bool>>>)((StreamReader o) => o.ReadBool));
			MethodCallExpression methodCallExpression = Expression.Call(streamReaderExpression, methodInfo);
			if (assignmentTargetExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Assign(assignmentTargetExpression, methodCallExpression);
			}
			return Expression.Assign(assignmentTargetExpression, Expression.Convert(methodCallExpression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			streamWriter.WriteBool((bool)value);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<bool>>>)((StreamWriter o) => o.WriteBool));
			if (valueExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Call(streamWriterExpression, methodInfo, valueExpression);
			}
			return Expression.Call(streamWriterExpression, methodInfo, Expression.Convert(valueExpression, type));
		}
	}
	public class DiagnosticSerializer : ISerializer
	{
		private readonly ISerializer serializer;

		public Type Type => serializer.Type;

		public DiagnosticSerializer(ISerializer serializer)
		{
			this.serializer = serializer;
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return serializer.Deserialize(streamReader, serializationContext, propertyMetaData);
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<DiagnosticSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, Probe>>>)((DiagnosticSerializer o) => o.BeginProbeDeserialize));
			MethodCallExpression right = Expression.Call(Expression.Constant(this), methodInfo, new Expression[3]
			{
				streamReaderExpression,
				serializationContextExpression,
				Expression.Constant(propertyMetaData)
			});
			ParameterExpression parameterExpression = Expression.Variable(typeof(Probe));
			BinaryExpression arg = Expression.Assign(parameterExpression, right);
			Expression arg2 = serializer.DeserializerExpression(streamReaderExpression, serializationContextExpression, assignmentTargetExpression, propertyMetaData);
			MethodInfo methodInfo2 = ReflectionHelper.GetMethodInfo((Expression<Func<DiagnosticSerializer, Action<StreamReader, SerializationContext, PropertyMetaData, object, Probe>>>)((DiagnosticSerializer o) => o.EndProbeDeserialize));
			MethodCallExpression @finally = Expression.Call(Expression.Constant(this), methodInfo2, streamReaderExpression, serializationContextExpression, Expression.Constant(propertyMetaData), Expression.Convert(assignmentTargetExpression, typeof(object)), parameterExpression);
			TryExpression tryExpression = Expression.TryFinally(Expression.Block(arg, arg2), @finally);
			ParameterExpression[] variables = new ParameterExpression[1] { parameterExpression };
			Expression[] expressions = new TryExpression[1] { tryExpression };
			return Expression.Block(variables, expressions);
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			serializer.Serialize(streamWriter, serializationContext, value, propertyMetaData);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<DiagnosticSerializer, Func<StreamWriter, SerializationContext, object, PropertyMetaData, Probe>>>)((DiagnosticSerializer o) => o.BeginProbeSerialize));
			MethodCallExpression right = Expression.Call(Expression.Constant(this), methodInfo, streamWriterExpression, serializationContextExpression, Expression.Convert(valueExpression, typeof(object)), Expression.Constant(propertyMetaData));
			ParameterExpression parameterExpression = Expression.Variable(typeof(Probe));
			BinaryExpression arg = Expression.Assign(parameterExpression, right);
			Expression arg2 = serializer.SerializerExpression(streamWriterExpression, serializationContextExpression, valueExpression, propertyMetaData);
			MethodInfo methodInfo2 = ReflectionHelper.GetMethodInfo((Expression<Func<DiagnosticSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData, Probe>>>)((DiagnosticSerializer o) => o.EndProbeSerialize));
			MethodCallExpression @finally = Expression.Call(Expression.Constant(this), methodInfo2, streamWriterExpression, serializationContextExpression, Expression.Convert(valueExpression, typeof(object)), Expression.Constant(propertyMetaData), parameterExpression);
			TryExpression tryExpression = Expression.TryFinally(Expression.Block(arg, arg2), @finally);
			ParameterExpression[] variables = new ParameterExpression[1] { parameterExpression };
			Expression[] expressions = new TryExpression[1] { tryExpression };
			return Expression.Block(variables, expressions);
		}

		private Probe BeginProbeDeserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData)
		{
			Probe probe = serializationContext.BeginProbe();
			probe.DiagnosticInfo.Offset = streamReader.Position;
			probe.DiagnosticInfo.PropertyMetaData = propertyMetaData;
			return probe;
		}

		private Probe BeginProbeSerialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData)
		{
			Probe probe = serializationContext.BeginProbe();
			probe.DiagnosticInfo.Offset = streamWriter.Position;
			probe.DiagnosticInfo.PropertyMetaData = propertyMetaData;
			probe.DiagnosticInfo.Value = value;
			return probe;
		}

		private void EndProbeDeserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData, object deserializedObject, Probe probe)
		{
			probe.DiagnosticInfo.Length = streamReader.Position - probe.DiagnosticInfo.Offset;
			probe.DiagnosticInfo.Value = deserializedObject;
			serializationContext.EndProbe(probe);
		}

		private void EndProbeSerialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData, Probe probe)
		{
			probe.DiagnosticInfo.Length = streamWriter.Position - probe.DiagnosticInfo.Offset;
			serializationContext.EndProbe(probe);
		}
	}
	public class ChatHeaderSerializer : ISerializer
	{
		private readonly Type type;

		public Func<StreamReader, SerializationContext, object> DeserializerLambda { get; private set; }

		public Action<StreamWriter, SerializationContext, object> SerializerLambda { get; private set; }

		public Type Type => type;

		public ChatHeaderSerializer()
		{
			type = typeof(Header);
			SerializerLambda = delegate(StreamWriter streamWriter, SerializationContext serializationContext, object value)
			{
				Serialize(streamWriter, serializationContext, value);
			};
			DeserializerLambda = (StreamReader streamReader, SerializationContext serializationContext) => Deserialize(streamReader, serializationContext);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			ChatHeader chatHeader = new ChatHeader();
			chatHeader.PacketType = (ChatMessageType)streamReader.ReadInt16();
			chatHeader.Size = streamReader.ReadInt16();
			return chatHeader;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			throw new NotImplementedException();
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			ChatHeader chatHeader = (ChatHeader)value;
			streamWriter.WriteInt16((short)chatHeader.PacketType);
			streamWriter.WriteInt16(chatHeader.Size);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			throw new NotImplementedException();
		}
	}
	public class HeaderSerializer : ISerializer
	{
		private readonly Type type;

		public Func<StreamReader, SerializationContext, object> DeserializerLambda { get; private set; }

		public Action<StreamWriter, SerializationContext, object> SerializerLambda { get; private set; }

		public Type Type => type;

		public HeaderSerializer()
		{
			type = typeof(Header);
			SerializerLambda = delegate(StreamWriter streamWriter, SerializationContext serializationContext, object value)
			{
				Serialize(streamWriter, serializationContext, value);
			};
			DeserializerLambda = (StreamReader streamReader, SerializationContext serializationContext) => Deserialize(streamReader, serializationContext);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			Header header = new Header();
			header.MessageId = streamReader.ReadUInt16();
			header.PacketType = (PacketType)streamReader.ReadInt16();
			header.Unknown = streamReader.ReadInt16();
			header.Size = streamReader.ReadInt16();
			header.Sender = streamReader.ReadInt32();
			header.Receiver = streamReader.ReadInt32();
			return header;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			throw new NotImplementedException();
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			Header header = (Header)value;
			streamWriter.WriteUInt16(header.MessageId);
			streamWriter.WriteInt16((short)header.PacketType);
			streamWriter.WriteInt16(header.Unknown);
			streamWriter.WriteInt16(header.Size);
			streamWriter.WriteInt32(header.Sender);
			streamWriter.WriteInt32(header.Receiver);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			throw new NotImplementedException();
		}
	}
	public class Int16Serializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public Int16Serializer()
		{
			type = typeof(short);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return streamReader.ReadInt16();
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<short>>>)((StreamReader o) => o.ReadInt16));
			MethodCallExpression methodCallExpression = Expression.Call(streamReaderExpression, methodInfo);
			if (assignmentTargetExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Assign(assignmentTargetExpression, methodCallExpression);
			}
			return Expression.Assign(assignmentTargetExpression, Expression.Convert(methodCallExpression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			streamWriter.WriteInt16((short)value);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<short>>>)((StreamWriter o) => o.WriteInt16));
			if (valueExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Call(streamWriterExpression, methodInfo, valueExpression);
			}
			return Expression.Call(streamWriterExpression, methodInfo, Expression.Convert(valueExpression, type));
		}
	}
	public class Int32Serializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public Int32Serializer()
		{
			type = typeof(int);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return streamReader.ReadInt32();
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<int>>>)((StreamReader o) => o.ReadInt32));
			MethodCallExpression methodCallExpression = Expression.Call(streamReaderExpression, methodInfo);
			if (assignmentTargetExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Assign(assignmentTargetExpression, methodCallExpression);
			}
			return Expression.Assign(assignmentTargetExpression, Expression.Convert(methodCallExpression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			streamWriter.WriteInt32((int)value);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<int>>>)((StreamWriter o) => o.WriteInt32));
			if (valueExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Call(streamWriterExpression, methodInfo, valueExpression);
			}
			return Expression.Call(streamWriterExpression, methodInfo, Expression.Convert(valueExpression, type));
		}
	}
	public class Int64Serializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public Int64Serializer()
		{
			type = typeof(long);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return streamReader.ReadInt64();
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<long>>>)((StreamReader o) => o.ReadInt64));
			MethodCallExpression methodCallExpression = Expression.Call(streamReaderExpression, methodInfo);
			if (assignmentTargetExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Assign(assignmentTargetExpression, methodCallExpression);
			}
			return Expression.Assign(assignmentTargetExpression, Expression.Convert(methodCallExpression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			streamWriter.WriteInt64((long)value);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<long>>>)((StreamWriter o) => o.WriteInt64));
			if (valueExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Call(streamWriterExpression, methodInfo, valueExpression);
			}
			return Expression.Call(streamWriterExpression, methodInfo, Expression.Convert(valueExpression, type));
		}
	}
	public class IPAddressSerializer : ISerializer
	{
		private readonly ConstructorInfo constructor;

		private readonly Type type;

		public Type Type => type;

		public IPAddressSerializer()
		{
			type = typeof(IPAddress);
			constructor = type.GetConstructor(new Type[1] { typeof(byte[]) });
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return new IPAddress(streamReader.ReadBytes(4));
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<int, byte[]>>>)((StreamReader o) => o.ReadBytes));
			MethodCallExpression methodCallExpression = Expression.Call(streamReaderExpression, methodInfo, Expression.Constant(4));
			NewExpression newExpression = Expression.New(constructor, methodCallExpression);
			if (assignmentTargetExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Assign(assignmentTargetExpression, newExpression);
			}
			return Expression.Assign(assignmentTargetExpression, Expression.Convert(newExpression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			IPAddress iPAddress = (IPAddress)value;
			streamWriter.WriteBytes(iPAddress.GetAddressBytes());
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<byte[]>>>)((StreamWriter o) => o.WriteBytes));
			MethodInfo methodInfo2 = ReflectionHelper.GetMethodInfo((Expression<Func<IPAddress, Func<byte[]>>>)((IPAddress o) => o.GetAddressBytes));
			MethodCallExpression methodCallExpression = Expression.Call(valueExpression, methodInfo2);
			return Expression.Call(streamWriterExpression, methodInfo, methodCallExpression);
		}
	}
	public class SingleSerializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public SingleSerializer()
		{
			type = typeof(float);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return streamReader.ReadSingle();
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<float>>>)((StreamReader o) => o.ReadSingle));
			MethodCallExpression methodCallExpression = Expression.Call(streamReaderExpression, methodInfo);
			if (assignmentTargetExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Assign(assignmentTargetExpression, methodCallExpression);
			}
			return Expression.Assign(assignmentTargetExpression, Expression.Convert(methodCallExpression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			streamWriter.WriteSingle((short)value);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<float>>>)((StreamWriter o) => o.WriteSingle));
			if (valueExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Call(streamWriterExpression, methodInfo, valueExpression);
			}
			return Expression.Call(streamWriterExpression, methodInfo, Expression.Convert(valueExpression, type));
		}
	}
	public class StringSerializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public StringSerializer()
		{
			type = typeof(string);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			int length;
			if (propertyMetaData.Options.SerializeSize == ArraySizeType.NoSerialization)
			{
				length = propertyMetaData.Options.FixedSizeLength;
			}
			else
			{
				if (propertyMetaData.Options.SerializeSize == ArraySizeType.NullTerminated)
				{
					return streamReader.ReadNullTerminatedString();
				}
				ArraySizeSerializer arraySizeSerializer = new ArraySizeSerializer(propertyMetaData.Options.SerializeSize);
				length = (int)arraySizeSerializer.Deserialize(streamReader, serializationContext, propertyMetaData);
			}
			return streamReader.ReadString(length);
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			List<Expression> list = new List<Expression>();
			ParameterExpression parameterExpression = Expression.Variable(typeof(int), "length");
			Expression item;
			if (propertyMetaData.Options.SerializeSize == ArraySizeType.NoSerialization)
			{
				item = Expression.Assign(parameterExpression, Expression.Constant(propertyMetaData.Options.FixedSizeLength, typeof(int)));
			}
			else if (propertyMetaData.Options.SerializeSize == ArraySizeType.NullTerminated)
			{
				MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<int>>>)((StreamReader o) => o.PeekNullTermStringLength));
				item = Expression.Assign(parameterExpression, Expression.Call(streamReaderExpression, methodInfo));
			}
			else
			{
				item = new ArraySizeSerializer(propertyMetaData.Options.SerializeSize).DeserializerExpression(streamReaderExpression, serializationContextExpression, parameterExpression, propertyMetaData);
			}
			list.Add(item);
			MethodInfo methodInfo2 = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<int, string>>>)((StreamReader o) => o.ReadString));
			MethodCallExpression methodCallExpression = Expression.Call(streamReaderExpression, methodInfo2, parameterExpression);
			Expression item2 = (assignmentTargetExpression.Type.IsAssignableFrom(type) ? Expression.Assign(assignmentTargetExpression, methodCallExpression) : Expression.Assign(assignmentTargetExpression, Expression.Convert(methodCallExpression, assignmentTargetExpression.Type)));
			list.Add(item2);
			return Expression.Block(new ParameterExpression[1] { parameterExpression }, list);
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			if (propertyMetaData.Options.SerializeSize != 0)
			{
				ArraySizeSerializer arraySizeSerializer = new ArraySizeSerializer(propertyMetaData.Options.SerializeSize);
				arraySizeSerializer.Serialize(streamWriter, serializationContext, value, propertyMetaData);
			}
			int? padToLength = (propertyMetaData.Options.IsFixedSize ? new int?(propertyMetaData.Options.FixedSizeLength) : null);
			streamWriter.WriteString((string)value, padToLength);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			if (!valueExpression.Type.IsAssignableFrom(type))
			{
				valueExpression = Expression.Convert(valueExpression, type);
			}
			List<Expression> list = new List<Expression>();
			if (propertyMetaData.Options.SerializeSize != 0 && propertyMetaData.Options.SerializeSize != ArraySizeType.NullTerminated)
			{
				Expression item = new ArraySizeSerializer(propertyMetaData.Options.SerializeSize).SerializerExpression(streamWriterExpression, serializationContextExpression, valueExpression, propertyMetaData);
				list.Add(item);
			}
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<string, int?>>>)((StreamWriter o) => o.WriteString));
			Expression expression = (propertyMetaData.Options.IsFixedSize ? Expression.Constant(propertyMetaData.Options.FixedSizeLength, typeof(int?)) : Expression.Constant(null, typeof(int?)));
			MethodCallExpression item2 = Expression.Call(streamWriterExpression, methodInfo, new Expression[2]
			{
				Expression.Convert(valueExpression, type),
				expression
			});
			list.Add(item2);
			return Expression.Block(list);
		}
	}
	public class TypeSerializer : ISerializer
	{
		private readonly Lazy<Expression> deserializerExpression;

		private readonly Lazy<Func<StreamReader, SerializationContext, object>> lazyDeserializerLambda;

		private readonly Lazy<Action<StreamWriter, SerializationContext, object>> lazySerializerLambda;

		private readonly Lazy<Expression> serializerExpression;

		private readonly Type type;

		private readonly TypeSerializerBuilder typeSerializerBuilder;

		public Type Type => type;

		private Func<StreamReader, SerializationContext, object> DeserializerLambda => lazyDeserializerLambda.Value;

		private Action<StreamWriter, SerializationContext, object> SerializerLambda => lazySerializerLambda.Value;

		public TypeSerializer(Type type, TypeSerializerBuilder typeSerializerBuilder)
		{
			this.type = type;
			this.typeSerializerBuilder = typeSerializerBuilder;
			serializerExpression = new Lazy<Expression>(BuildSerializerExpression);
			lazySerializerLambda = new Lazy<Action<StreamWriter, SerializationContext, object>>(CompileSerializer);
			deserializerExpression = new Lazy<Expression>(BuildDeserializerExpression);
			lazyDeserializerLambda = new Lazy<Func<StreamReader, SerializationContext, object>>(CompileDeserializer);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return DeserializerLambda(streamReader, serializationContext);
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			InvocationExpression expression = Expression.Invoke(deserializerExpression.Value, streamReaderExpression, serializationContextExpression);
			return Expression.Assign(assignmentTargetExpression, Expression.Convert(expression, type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			SerializerLambda(streamWriter, serializationContext, value);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			return Expression.Invoke(serializerExpression.Value, streamWriterExpression, serializationContextExpression, valueExpression);
		}

		private Expression BuildDeserializerExpression()
		{
			ParameterExpression streamReaderExpression = Expression.Parameter(typeof(StreamReader), "streamReader");
			ParameterExpression serializationContextExpression = Expression.Parameter(typeof(SerializationContext), "serializationContext");
			return typeSerializerBuilder.BuildDeserializer(streamReaderExpression, serializationContextExpression);
		}

		private Expression BuildSerializerExpression()
		{
			ParameterExpression streamWriterExpression = Expression.Parameter(typeof(StreamWriter), "streamWriter");
			ParameterExpression serializationContextExpression = Expression.Parameter(typeof(SerializationContext), "serializationContext");
			return typeSerializerBuilder.BuildSerializer(streamWriterExpression, serializationContextExpression);
		}

		private Func<StreamReader, SerializationContext, object> CompileDeserializer()
		{
			Expression<Func<StreamReader, SerializationContext, object>> expression = (Expression<Func<StreamReader, SerializationContext, object>>)deserializerExpression.Value;
			return expression.Compile();
		}

		private Action<StreamWriter, SerializationContext, object> CompileSerializer()
		{
			Expression<Action<StreamWriter, SerializationContext, object>> expression = (Expression<Action<StreamWriter, SerializationContext, object>>)serializerExpression.Value;
			return expression.Compile();
		}
	}
	public class UInt16Serializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public UInt16Serializer()
		{
			type = typeof(ushort);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return streamReader.ReadUInt16();
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<ushort>>>)((StreamReader o) => o.ReadUInt16));
			MethodCallExpression methodCallExpression = Expression.Call(streamReaderExpression, methodInfo);
			if (assignmentTargetExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Assign(assignmentTargetExpression, methodCallExpression);
			}
			return Expression.Assign(assignmentTargetExpression, Expression.Convert(methodCallExpression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			streamWriter.WriteUInt16((ushort)value);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<ushort>>>)((StreamWriter o) => o.WriteUInt16));
			if (valueExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Call(streamWriterExpression, methodInfo, valueExpression);
			}
			return Expression.Call(streamWriterExpression, methodInfo, Expression.Convert(valueExpression, type));
		}
	}
	public class UInt32Serializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public UInt32Serializer()
		{
			type = typeof(uint);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return streamReader.ReadUInt32();
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<uint>>>)((StreamReader o) => o.ReadUInt32));
			MethodCallExpression methodCallExpression = Expression.Call(streamReaderExpression, methodInfo);
			if (assignmentTargetExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Assign(assignmentTargetExpression, methodCallExpression);
			}
			return Expression.Assign(assignmentTargetExpression, Expression.Convert(methodCallExpression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			streamWriter.WriteUInt32((uint)value);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<uint>>>)((StreamWriter o) => o.WriteUInt32));
			if (valueExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Call(streamWriterExpression, methodInfo, valueExpression);
			}
			return Expression.Call(streamWriterExpression, methodInfo, Expression.Convert(valueExpression, type));
		}
	}
}
namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
	internal class InspectSerializer : ISerializer
	{
		public Type Type { get; }

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			InspectMessage inspectMessage = new InspectMessage();
			inspectMessage.N3MessageType = (N3MessageType)streamReader.ReadInt32();
			inspectMessage.Identity = new Identity((IdentityType)streamReader.ReadInt32(), streamReader.ReadInt32());
			inspectMessage.Unknown = streamReader.ReadByte();
			inspectMessage.Target = new Identity((IdentityType)streamReader.ReadInt32(), streamReader.ReadInt32());
			long num = (streamReader.PeekUntilEnd() - 4) / 32;
			inspectMessage.Slot = new InspectSlotInfo[num];
			for (int i = 0; i < num; i++)
			{
				inspectMessage.Slot[i] = new InspectSlotInfo
				{
					Unk = streamReader.ReadInt32(),
					EquipSlot = (EquipSlot)streamReader.ReadInt32(),
					Unk2 = streamReader.ReadInt32(),
					UniqueIdentity = new Identity((IdentityType)streamReader.ReadInt32(), streamReader.ReadInt32()),
					HighId = streamReader.ReadInt32(),
					LowId = streamReader.ReadInt32(),
					Ql = streamReader.ReadInt32()
				};
			}
			return inspectMessage;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<InspectSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((InspectSerializer o) => o.Deserialize));
			NewExpression instance = Expression.New(GetType());
			MethodCallExpression expression = Expression.Call(instance, methodInfo, new Expression[3]
			{
				streamReaderExpression,
				serializationContextExpression,
				Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
			});
			return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(expression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			throw new NotImplementedException();
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<InspectSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((InspectSerializer o) => o.Serialize));
			NewExpression instance = Expression.New(GetType());
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	internal class PlayfieldTowerUpdateClientSerializer : ISerializer
	{
		public Type Type { get; }

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			PlayfieldTowerUpdateClientMessage playfieldTowerUpdateClientMessage = new PlayfieldTowerUpdateClientMessage();
			playfieldTowerUpdateClientMessage.N3MessageType = (N3MessageType)streamReader.ReadInt32();
			playfieldTowerUpdateClientMessage.Identity = new Identity((IdentityType)streamReader.ReadInt32(), streamReader.ReadInt32());
			playfieldTowerUpdateClientMessage.Unknown = streamReader.ReadByte();
			playfieldTowerUpdateClientMessage.TowerId = new Identity((IdentityType)streamReader.ReadInt32(), streamReader.ReadInt32());
			playfieldTowerUpdateClientMessage.UpdateType = (PlayfieldUpdateClientType)streamReader.ReadInt32();
			if (playfieldTowerUpdateClientMessage.UpdateType == PlayfieldUpdateClientType.Planted)
			{
				playfieldTowerUpdateClientMessage.Tower = new TowerInfo
				{
					PlaceholderId = new Identity((IdentityType)streamReader.ReadInt32(), streamReader.ReadInt32()),
					TowerCharId = new Identity((IdentityType)streamReader.ReadInt32(), streamReader.ReadInt32()),
					Position = new Vector3(streamReader.ReadSingle(), streamReader.ReadSingle(), streamReader.ReadSingle()),
					MeshId = streamReader.ReadInt32(),
					Side = (Side)streamReader.ReadInt32(),
					DestroyedMeshId = streamReader.ReadInt32(),
					Scale = streamReader.ReadSingle(),
					Class = (TowerClass)streamReader.ReadInt32()
				};
			}
			return playfieldTowerUpdateClientMessage;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<PlayfieldTowerUpdateClientSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((PlayfieldTowerUpdateClientSerializer o) => o.Deserialize));
			NewExpression instance = Expression.New(GetType());
			MethodCallExpression expression = Expression.Call(instance, methodInfo, new Expression[3]
			{
				streamReaderExpression,
				serializationContextExpression,
				Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
			});
			return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(expression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			throw new NotImplementedException();
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<PlayfieldTowerUpdateClientSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((PlayfieldTowerUpdateClientSerializer o) => o.Serialize));
			NewExpression instance = Expression.New(GetType());
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	internal class GroupMessageSerializer : ISerializer
	{
		public Type Type { get; }

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			GroupMsgMessage groupMsgMessage = new GroupMsgMessage();
			groupMsgMessage.MessageType = (GroupMessageType)streamReader.ReadByte();
			groupMsgMessage.ChannelId = streamReader.ReadInt32();
			groupMsgMessage.SenderId = streamReader.ReadUInt32();
			groupMsgMessage.Text = streamReader.ReadString(streamReader.ReadUInt16());
			return groupMsgMessage;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<GroupMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((GroupMessageSerializer o) => o.Deserialize));
			NewExpression instance = Expression.New(GetType());
			MethodCallExpression expression = Expression.Call(instance, methodInfo, new Expression[3]
			{
				streamReaderExpression,
				serializationContextExpression,
				Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
			});
			return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(expression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			GroupMsgMessage groupMsgMessage = (GroupMsgMessage)value;
			streamWriter.WriteByte((byte)groupMsgMessage.MessageType);
			streamWriter.WriteInt32(groupMsgMessage.ChannelId);
			streamWriter.WriteUInt16((ushort)groupMsgMessage.Text.Length);
			streamWriter.WriteString(groupMsgMessage.Text);
			streamWriter.WriteInt16(0);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<GroupMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((GroupMessageSerializer o) => o.Serialize));
			NewExpression instance = Expression.New(GetType());
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	public class PlayfieldVendorInfoSerializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public PlayfieldVendorInfoSerializer()
		{
			type = typeof(PlayfieldVendorInfo);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			IdentityType identityType = (IdentityType)streamReader.ReadInt32();
			if (identityType != IdentityType.VendingMachine)
			{
				streamReader.Position -= 4L;
				return null;
			}
			return new PlayfieldVendorInfo
			{
				Unknown1 = new Identity
				{
					Type = identityType,
					Instance = streamReader.ReadInt32()
				},
				Unknown2 = streamReader.ReadInt32(),
				VendorCount = streamReader.ReadInt32(),
				FirstVendorId = streamReader.ReadInt32()
			};
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<PlayfieldVendorInfoSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((PlayfieldVendorInfoSerializer o) => o.Deserialize));
			NewExpression instance = Expression.New(GetType());
			MethodCallExpression expression = Expression.Call(instance, methodInfo, new Expression[3]
			{
				streamReaderExpression,
				serializationContextExpression,
				Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
			});
			return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(expression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			if (value != null)
			{
				PlayfieldVendorInfo playfieldVendorInfo = (PlayfieldVendorInfo)value;
				streamWriter.WriteInt32((int)playfieldVendorInfo.Unknown1.Type);
				streamWriter.WriteInt32(playfieldVendorInfo.Unknown1.Instance);
				streamWriter.WriteInt32(playfieldVendorInfo.Unknown2);
				streamWriter.WriteInt32(playfieldVendorInfo.VendorCount);
				streamWriter.WriteInt32(playfieldVendorInfo.FirstVendorId);
			}
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<PlayfieldVendorInfoSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((PlayfieldVendorInfoSerializer o) => o.Serialize));
			NewExpression instance = Expression.New(GetType());
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	public class SimpleCharFullUpdateSerializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public SimpleCharFullUpdateSerializer()
		{
			type = typeof(SimpleCharFullUpdateMessage);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			SimpleCharFullUpdateMessage simpleCharFullUpdateMessage = new SimpleCharFullUpdateMessage();
			simpleCharFullUpdateMessage.N3MessageType = (N3MessageType)streamReader.ReadInt32();
			simpleCharFullUpdateMessage.Identity = new Identity
			{
				Type = (IdentityType)streamReader.ReadInt32(),
				Instance = streamReader.ReadInt32()
			};
			simpleCharFullUpdateMessage.Unknown = streamReader.ReadByte();
			simpleCharFullUpdateMessage.Version = streamReader.ReadByte();
			simpleCharFullUpdateMessage.Flags = (SimpleCharFullUpdateFlags)streamReader.ReadInt32();
			if (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.HasPlayfieldId))
			{
				simpleCharFullUpdateMessage.PlayfieldId = streamReader.ReadInt32();
			}
			simpleCharFullUpdateMessage.Position = new Vector3(streamReader.ReadSingle(), streamReader.ReadSingle(), streamReader.ReadSingle());
			simpleCharFullUpdateMessage.Heading = (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.HasHeading) ? new Quaternion(streamReader.ReadSingle(), streamReader.ReadSingle(), streamReader.ReadSingle(), streamReader.ReadSingle()) : Quaternion.Identity);
			simpleCharFullUpdateMessage.Appearance = new Appearance
			{
				Value = streamReader.ReadUInt32()
			};
			simpleCharFullUpdateMessage.Name = streamReader.ReadString(streamReader.ReadByte());
			simpleCharFullUpdateMessage.CharacterFlags = (CharacterFlags)streamReader.ReadInt32();
			simpleCharFullUpdateMessage.AccountFlags = streamReader.ReadInt16();
			simpleCharFullUpdateMessage.Expansions = streamReader.ReadInt16();
			if (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.IsNpc))
			{
				simpleCharFullUpdateMessage.CharacterInfo = new SimpleCharInfo.NPCInfo();
				SimpleCharInfo.NPCInfo nPCInfo = simpleCharFullUpdateMessage.CharacterInfo as SimpleCharInfo.NPCInfo;
				nPCInfo.Family = (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallNpcFamily) ? streamReader.ReadByte() : streamReader.ReadInt16());
				nPCInfo.LosHeight = (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallNpcLosHeight) ? streamReader.ReadByte() : streamReader.ReadInt16());
				if (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.UnknownDataFlag))
				{
					streamReader.ReadByte();
					streamReader.ReadInt16();
				}
			}
			else
			{
				simpleCharFullUpdateMessage.CharacterInfo = new SimpleCharInfo.PlayerInfo();
				SimpleCharInfo.PlayerInfo playerInfo = simpleCharFullUpdateMessage.CharacterInfo as SimpleCharInfo.PlayerInfo;
				playerInfo.CurrentNano = streamReader.ReadUInt32();
				playerInfo.Team = streamReader.ReadInt32();
				playerInfo.Swim = streamReader.ReadInt16();
				playerInfo.StrengthBase = streamReader.ReadInt16();
				playerInfo.AgilityBase = streamReader.ReadInt16();
				playerInfo.StaminaBase = streamReader.ReadInt16();
				playerInfo.IntelligenceBase = streamReader.ReadInt16();
				playerInfo.SenseBase = streamReader.ReadInt16();
				playerInfo.PsychicBase = streamReader.ReadInt16();
				if (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.HasOrgName))
				{
					playerInfo.OrgId = streamReader.ReadInt32();
					streamReader.ReadByte();
				}
				if (simpleCharFullUpdateMessage.CharacterFlags.HasFlag(CharacterFlags.HasVisibleName))
				{
					playerInfo.FirstName = streamReader.ReadString(streamReader.ReadByte());
					playerInfo.LastName = streamReader.ReadString(streamReader.ReadByte());
				}
				if (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.HasOrgName))
				{
					playerInfo.OrgName = streamReader.ReadString(streamReader.ReadByte());
				}
			}
			if (simpleCharFullUpdateMessage.CharacterFlags.HasFlag(CharacterFlags.Tower))
			{
				simpleCharFullUpdateMessage.ScfuTowerUnk = streamReader.ReadByte();
			}
			simpleCharFullUpdateMessage.Level = (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.HasExtendedLevel) ? streamReader.ReadInt16() : streamReader.ReadByte());
			simpleCharFullUpdateMessage.Health = (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallHealth) ? streamReader.ReadUInt16() : streamReader.ReadInt32());
			simpleCharFullUpdateMessage.HealthDamage = (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallHealthDamage) ? streamReader.ReadByte() : (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallHealth) ? streamReader.ReadUInt16() : streamReader.ReadInt32()));
			simpleCharFullUpdateMessage.MonsterData = streamReader.ReadUInt32();
			simpleCharFullUpdateMessage.MonsterScale = streamReader.ReadInt16();
			simpleCharFullUpdateMessage.VisualFlags = streamReader.ReadInt16();
			simpleCharFullUpdateMessage.VisibleTitle = streamReader.ReadByte();
			simpleCharFullUpdateMessage.ScfuUnk1 = streamReader.ReadBytes(streamReader.ReadInt32());
			if (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.HasHeadMesh))
			{
				simpleCharFullUpdateMessage.HeadMesh = streamReader.ReadInt32();
			}
			simpleCharFullUpdateMessage.RunSpeedBase = (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.HasExtendedRunSpeed) ? streamReader.ReadInt16() : streamReader.ReadByte());
			if (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.IsUnderAttack))
			{
				simpleCharFullUpdateMessage.FightingTarget = new Identity((IdentityType)streamReader.ReadInt32(), streamReader.ReadInt32());
			}
			if (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.HasExtendedTextures))
			{
				int num = streamReader.ReadInt32() / 1009 - 1;
				simpleCharFullUpdateMessage.TextureOverrides = new SimpleCharInfo.TextureOverride[num];
				for (int i = 0; i < num; i++)
				{
					SimpleCharInfo.TextureOverride textureOverride = new SimpleCharInfo.TextureOverride();
					textureOverride.Name = Encoding.ASCII.GetString(streamReader.ReadBytes(32));
					textureOverride.TextureId = streamReader.ReadInt32();
					textureOverride.Unknown1 = streamReader.ReadInt32();
					textureOverride.Unknown2 = streamReader.ReadInt32();
					simpleCharFullUpdateMessage.TextureOverrides[i] = textureOverride;
				}
			}
			if (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.IsImmune))
			{
				byte b = streamReader.ReadByte();
			}
			if (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.UnknownFlag3))
			{
				byte b2 = streamReader.ReadByte();
			}
			int num2 = streamReader.ReadInt32() / 1009 - 1;
			simpleCharFullUpdateMessage.ActiveNanos = new SimpleCharInfo.ActiveNano[num2];
			for (int j = 0; j < num2; j++)
			{
				simpleCharFullUpdateMessage.ActiveNanos[j] = new SimpleCharInfo.ActiveNano
				{
					Identity = new Identity((IdentityType)streamReader.ReadInt32(), streamReader.ReadInt32()),
					NanoInstance = streamReader.ReadInt32(),
					Time1 = streamReader.ReadInt32(),
					Time2 = streamReader.ReadInt32()
				};
			}
			if (simpleCharFullUpdateMessage.Flags.HasFlag(SimpleCharFullUpdateFlags.HasWaypoints))
			{
				streamReader.ReadUInt32();
				streamReader.ReadUInt32();
				simpleCharFullUpdateMessage.Waypoints = new List<Vector3>();
				int num3 = streamReader.ReadInt32();
				for (int k = 0; k < num3; k++)
				{
					simpleCharFullUpdateMessage.Waypoints.Add(new Vector3(streamReader.ReadSingle(), streamReader.ReadSingle(), streamReader.ReadSingle()));
				}
			}
			int num4 = streamReader.ReadInt32() / 1009 - 1;
			List<Texture> list = new List<Texture>();
			for (int l = 0; l < num4; l++)
			{
				list.Add(new Texture
				{
					Place = streamReader.ReadInt32(),
					Id = streamReader.ReadInt32(),
					Unknown = streamReader.ReadInt32()
				});
			}
			simpleCharFullUpdateMessage.Textures = list.ToArray();
			int num5 = streamReader.ReadInt32() / 1009 - 1;
			List<SmokeLounge.AOtomation.Messaging.GameData.Mesh> list2 = new List<SmokeLounge.AOtomation.Messaging.GameData.Mesh>();
			for (int m = 0; m < num5; m++)
			{
				list2.Add(new SmokeLounge.AOtomation.Messaging.GameData.Mesh
				{
					Position = streamReader.ReadByte(),
					Id = streamReader.ReadUInt32(),
					OverrideTextureId = streamReader.ReadInt32(),
					Layer = streamReader.ReadByte()
				});
			}
			simpleCharFullUpdateMessage.Meshes = list2.ToArray();
			simpleCharFullUpdateMessage.Flags2 = (ScfuFlags2)streamReader.ReadInt32();
			if (simpleCharFullUpdateMessage.Flags2.HasFlag(ScfuFlags2.HasOwner))
			{
				simpleCharFullUpdateMessage.Owner = new Identity(IdentityType.SimpleChar, streamReader.ReadInt32());
			}
			simpleCharFullUpdateMessage.ScfuUnk2 = streamReader.ReadByte();
			if (simpleCharFullUpdateMessage.Flags2.HasFlag(ScfuFlags2.Unknown3))
			{
				byte b3 = streamReader.ReadByte();
				simpleCharFullUpdateMessage.SpecialAttacks = new SimpleCharInfo.SpecialAttackData[b3];
				for (int n = 0; n < b3; n++)
				{
					short num6 = streamReader.ReadInt16();
					if (num6 != 0)
					{
						simpleCharFullUpdateMessage.SpecialAttacks[n] = new SimpleCharInfo.SpecialAttackData
						{
							Unknown1 = num6,
							Unknown2 = streamReader.ReadInt16(),
							Unknown3 = streamReader.ReadInt16(),
							Unknown4 = streamReader.ReadInt16(),
							Unknown5 = streamReader.ReadInt16(),
							Name = streamReader.ReadString(4),
							Unknown6 = streamReader.ReadInt16()
						};
					}
				}
			}
			return simpleCharFullUpdateMessage;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<SimpleCharFullUpdateSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((SimpleCharFullUpdateSerializer o) => o.Deserialize));
			NewExpression instance = Expression.New(GetType());
			MethodCallExpression expression = Expression.Call(instance, methodInfo, new Expression[3]
			{
				streamReaderExpression,
				serializationContextExpression,
				Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
			});
			return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(expression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<SimpleCharFullUpdateSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((SimpleCharFullUpdateSerializer o) => o.Serialize));
			NewExpression instance = Expression.New(GetType());
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
}
namespace SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class AoContractAttribute : Attribute
	{
		private readonly int identifier;

		public int Identifier => identifier;

		public AoContractAttribute(int identifier)
		{
			this.identifier = identifier;
		}
	}
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
	public class AoFlagsAttribute : Attribute
	{
		private readonly string flag;

		public string Flag => flag;

		public AoFlagsAttribute(string flag)
		{
			this.flag = flag;
		}
	}
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class AoKnownTypeAttribute : Attribute
	{
		private readonly IdentifierType identifierType;

		private readonly int offset;

		public IdentifierType IdentifierType => identifierType;

		public int Offset => offset;

		public AoKnownTypeAttribute(int offset, IdentifierType identifierType)
		{
			this.offset = offset;
			this.identifierType = identifierType;
		}
	}
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
	public class AoMemberAttribute : Attribute
	{
		private readonly int order;

		public int FixedSizeLength { get; set; }

		public bool IsFixedSize { get; set; }

		public int Order => order;

		public int PadAfter { get; set; }

		public int PadBefore { get; set; }

		public ArraySizeType SerializeSize { get; set; }

		public AoMemberAttribute(int order)
		{
			this.order = order;
		}
	}
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
	public class AoUsesFlagsAttribute : Attribute
	{
		private readonly FlagsCriteria criteria;

		private readonly int criteriaValue;

		private readonly int[] criteriaValues;

		private readonly string flag;

		private readonly Type type;

		public FlagsCriteria Criteria => criteria;

		public int CriteriaValue => criteriaValue;

		public int[] CriteriaValues => criteriaValues;

		public string Flag => flag;

		public Type Type => type;

		public AoUsesFlagsAttribute(string flag, Type type, FlagsCriteria criteria, params int[] criteriaValues)
		{
			this.flag = flag;
			this.type = type;
			this.criteria = criteria;
			this.criteriaValues = criteriaValues;
			foreach (int num in criteriaValues)
			{
				criteriaValue |= num;
			}
		}
	}
}
namespace SmokeLounge.AOtomation.Messaging.Messages
{
	public class ChatHeader
	{
		public ChatMessageType PacketType { get; set; }

		public short Size { get; set; }
	}
	public class Header
	{
		public ushort MessageId { get; set; }

		public PacketType PacketType { get; set; }

		public int Receiver { get; set; }

		public int Sender { get; set; }

		public short Size { get; set; }

		public short Unknown { get; set; }

		public Header()
		{
			Unknown = 1;
		}
	}
	[AoContract(32512)]
	public class InitiateCompressionMessage : MessageBody
	{
		public override PacketType PacketType => PacketType.InitiateCompressionMessage;
	}
	public class ChatMessage
	{
		public ChatMessageBody Body { get; set; }

		public ChatHeader Header { get; set; }
	}
	public class Message
	{
		public MessageBody Body { get; set; }

		public Header Header { get; set; }

		public byte[] RawPacket { get; set; }
	}
	[AoKnownType(0, IdentifierType.Int16)]
	public abstract class ChatMessageBody
	{
		public abstract ChatMessageType PacketType { get; }
	}
	[AoKnownType(2, IdentifierType.Int16)]
	public abstract class MessageBody
	{
		public abstract PacketType PacketType { get; }
	}
	[AoContract(10)]
	[AoKnownType(16, IdentifierType.Int32)]
	public abstract class N3Message : MessageBody
	{
		[AoMember(0)]
		public N3MessageType N3MessageType { get; set; }

		[AoMember(1)]
		public Identity Identity { get; set; }

		[AoMember(2)]
		public byte Unknown { get; set; }

		public override PacketType PacketType => PacketType.N3Message;

		protected N3Message()
		{
			Unknown = 1;
		}
	}
	public enum N3MessageType
	{
		KnubotNPCDescription = 658522,
		AddTemplate = 86912780,
		GridDestinationSelect = 104417101,
		CentralControllerState = 139685733,
		WeatherControl = 207248749,
		PetToMaster = 221781762,
		FlushRDBCaches = 276329306,
		CentralControllerFullUpdate = 354759431,
		AcceptBSInvite = 376062814,
		AddPet = 424562550,
		SetPos = 425609582,
		ReflectAttack = 473583479,
		SpecialAttackWeapon = 490475292,
		ClientContainerAddItem = 525164414,
		MentorInvite = 536950654,
		Action = 541676156,
		Script = 542066801,
		FormatFeedback = 543902579,
		KnubotAnswer = 553854077,
		Quest = 556550266,
		MineFullUpdate = 559634040,
		LookAt = 575816799,
		ShieldAttack = 622404726,
		CastNanoSpell = 623988077,
		ResearchUpdate = 624755264,
		FollowTarget = 638531185,
		RelocateDynels = 642470219,
		Absorb = 642670433,
		Reload = 642866785,
		KnubotCloseChatWindow = 654986338,
		SimpleCharFullUpdate = 656095851,
		StartLogout = 673521409,
		Attack = 675889264,
		TeamMemberInfo = 678969928,
		CreateQuest = 689911323,
		FullCharacter = 691028809,
		LaserTagList = 691213647,
		TrapDisarmed = 707084127,
		Fov = 707345679,
		Stat = 724778350,
		QueueUpdate = 741279260,
		KnubotRejectedItems = 757146631,
		OrgInfoPacket = 774523499,
		n3PlayfieldFullUpdate = 806753109,
		AreaFormula = 824779579,
		InfromPlayer = 855716730,
		WaypointPath = 858857538,
		Mail = 859514983,
		ApplySpells = 875306269,
		Bank = 876357759,
		TemplateAction = 894457412,
		Trade = 908611438,
		Despawn = 911278200,
		DoorFullUpdate = 911888497,
		CityAdvantages = 912151899,
		HealthDamage = 923805036,
		PickUp = 924019819,
		FightModeUpdate = 924648770,
		Buff = 959724648,
		KnubotTrade = 974859276,
		ItemReplaced = 975321936,
		DropTemplate = 975454017,
		GridSelected = 976366154,
		SimpleItemFullUpdate = 990979439,
		KnubotOpenChatWindow = 991112548,
		WeaponItemFullUpdate = 991765096,
		SocialActionCmd = 992544625,
		Raid = 993732728,
		ShadowLevel = 1008609283,
		Clone = 1009144185,
		ServerPathPosDebugInfo = 1031040112,
		Skill = 1042306656,
		LeaveBattle = 1060772116,
		ToggleCloak = 1063144262,
		AppearanceUpdate = 1096961805,
		N3Teleport = 1125743906,
		PerkUpdate = 1130328099,
		SendScore = 1145584442,
		Resurrect = 1147087371,
		UpdateClientVisual = 1158097453,
		PlaySound = 1163733304,
		AttackInfo = 1174417174,
		TeamMember = 1177627950,
		SpawnMech = 1179451402,
		QuestFullUpdate = 1180319841,
		ChestFullUpdate = 1180327283,
		MarketSend = 1191915028,
		DropDynel = 1195914803,
		ContainerAddItem = 1196653092,
		InventoryUpdated = 1214149122,
		Visibility = 1226974738,
		StopFight = 1245782078,
		BattleOver = 1258694937,
		DoorStatusUpdate = 1283276859,
		TeamInvite = 1294610747,
		InfoPacket = 1295524910,
		SpellList = 1296367892,
		RaidCmd = 1314020952,
		InventoryUpdate = 1314089334,
		CorpseFullUpdate = 1330073093,
		Feedback = 1347702041,
		CharSecSpecAttack = 1363747104,
		BankCorpse = 1377907744,
		GenericCmd = 1381132376,
		ArriveAtBs = 1410218791,
		CharDCMove = 1410404643,
		ClientMoveItemToInventory = 1416181567,
		PlayfieldAllTowers = 1428293414,
		KnubotFinishTrade = 1432890148,
		KnubotAnswerList = 1433423153,
		StopLogout = 1446326328,
		CharInPlay = 1460412473,
		ShopUpdate = 1479942688,
		MechInfo = 1482113593,
		RemovePet = 1484007951,
		PlayfieldAllCities = 1495335206,
		TrapItemFullUpdate = 1496398120,
		Inspect = 1515741029,
		PlayfieldTowerUpdateClient = 1528694060,
		ServerPosDebugInfo = 1545864196,
		QuestAlternative = 1547920905,
		FullAuto = 1548372282,
		ChatCmd = 1548900987,
		MissedAttackInfo = 1550142248,
		KnubotAppendText = 1567642410,
		CharacterAction = 1581741936,
		Impulse = 1598704748,
		PlayfieldAnarchyF = 1598757433,
		ChatText = 1598768170,
		GameTime = 1599226158,
		SetWantedDirection = 1612717326,
		AOTransportSignal = 1651777045,
		OrgServer = 1683499527,
		PetCommand = 1798517507,
		SetStat = 1851741806,
		SetName = 1934514811,
		StopMovingCmd = 1949180692,
		SpecialAttackInfo = 1968115989,
		GiveQuestToMembers = 1998784807,
		KnubotStartTrade = 2019835933,
		GfxTrigger = 2049057282,
		NewLevel = 2134923798,
		OrgClient = 2135634184,
		VendingMachineFullUpdate = 2136230149
	}
	[AoContract(14)]
	public class OperatorMessage : MessageBody
	{
		public override PacketType PacketType => PacketType.OperatorMessage;
	}
	public enum ChatMessageType : short
	{
		ServerSalt = 0,
		LoginRequest = 2,
		SelectCharacter = 3,
		LoginOK = 5,
		LoginError = 6,
		CharacterList = 7,
		CharacterName = 20,
		LookupMessage = 21,
		PrivateMessage = 30,
		VicinityMessage = 34,
		NpcMessage = 35,
		PrivateGroupInvite = 50,
		PrivateGroupInviteAccept = 52,
		PrivateGroupInviteDecline = 53,
		PrivateGroupMessage = 57,
		ChannelList = 60,
		GroupMessage = 65,
		Ping = 100
	}
	public enum PacketType : short
	{
		SystemMessage = 1,
		TextMessage = 5,
		N3Message = 10,
		PingMessage = 11,
		OperatorMessage = 14,
		InitiateCompressionMessage = 32512
	}
	[AoContract(11)]
	public class PingMessage : MessageBody
	{
		public override PacketType PacketType => PacketType.PingMessage;

		[AoMember(0)]
		public PingMessageType PingMessageType { get; set; }

		[AoMember(1)]
		public int Unk1 { get; set; }

		[AoMember(2)]
		public uint ServerTime { get; set; }

		[AoMember(3)]
		public uint UpTime1 { get; set; }

		[AoMember(4)]
		public uint UpTime2 { get; set; }

		[AoMember(5)]
		public uint Unk2 { get; set; }
	}
	[AoContract(1)]
	[AoKnownType(16, IdentifierType.Int32)]
	public abstract class SystemMessage : MessageBody
	{
		[AoMember(0)]
		public SystemMessageType SystemMessageType { get; set; }

		public override PacketType PacketType => PacketType.SystemMessage;
	}
	public enum PingMessageType
	{
		Ping = 1,
		Pong
	}
	public enum SystemMessageType
	{
		LoginError = 13,
		CharacterList = 14,
		CreateCharacter = 15,
		NameInUse = 16,
		CharacterCreated = 17,
		DeleteCharacter = 20,
		CharacterDeleted = 21,
		SelectCharacter = 22,
		ZoneInfo = 23,
		ZoneLogin = 27,
		UserLogin = 34,
		ServerSalt = 36,
		UserCredentials = 37,
		ZoneRedirection = 60,
		ChatServerInfo = 67,
		RandomNameRequest = 85,
		SuggestName = 86
	}
	[AoContract(5)]
	public class TextMessage : MessageBody
	{
		[AoMember(0)]
		public TextMessageType TextMessageType { get; set; }

		[AoMember(1)]
		public Identity Unk { get; set; }

		[AoMember(2)]
		public int PayloadSize { get; set; }

		[AoMember(3, SerializeSize = ArraySizeType.Int16)]
		public string Text { get; set; }

		[AoMember(4)]
		public TextMessageRange Range { get; set; }

		public override PacketType PacketType => PacketType.TextMessage;
	}
	public enum TextMessageType
	{
		Whisper = 2,
		Say,
		Shout
	}
	public enum TextMessageRange : byte
	{
		Vicinity,
		Whisper,
		Shout,
		RP
	}
}
namespace SmokeLounge.AOtomation.Messaging.Messages.SystemMessages
{
	[AoContract(17)]
	public class CharacterCreatedMessage : SystemMessage
	{
		[AoMember(0)]
		public int CharacterId { get; set; }

		[AoMember(1)]
		public uint Unknown { get; set; }

		public CharacterCreatedMessage()
		{
			base.SystemMessageType = SystemMessageType.CharacterCreated;
			Unknown = 2966618111u;
		}
	}
	[AoContract(21)]
	public class CharacterDeletedMessage : SystemMessage
	{
		[AoMember(0)]
		public int CharacterId { get; set; }

		public CharacterDeletedMessage()
		{
			base.SystemMessageType = SystemMessageType.CharacterDeleted;
		}
	}
	[AoContract(14)]
	public class CharacterListMessage : SystemMessage
	{
		[AoMember(0, SerializeSize = ArraySizeType.Int32)]
		public LoginCharacterInfo[] Characters { get; set; }

		[AoMember(1)]
		public int AllowedCharacters { get; set; }

		[AoMember(2)]
		public int Expansions { get; set; }

		public CharacterListMessage()
		{
			base.SystemMessageType = SystemMessageType.CharacterList;
		}
	}
	[AoContract(67)]
	public class ChatServerInfoMessage : SystemMessage
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int32)]
		public string HostName { get; set; }

		[AoMember(2)]
		public int Port { get; set; }

		[AoMember(3)]
		public int Unknown2 { get; set; }

		public ChatServerInfoMessage()
		{
			base.SystemMessageType = SystemMessageType.ChatServerInfo;
			Unknown1 = 1;
		}
	}
	[AoContract(15)]
	public class CreateCharacterMessage : SystemMessage
	{
		[AoMember(0, IsFixedSize = true, FixedSizeLength = 49)]
		public byte[] Unknown1 { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int32)]
		public string Name { get; set; }

		[AoMember(2)]
		public Breed Breed { get; set; }

		[AoMember(3)]
		public Gender Gender { get; set; }

		[AoMember(4)]
		public Profession Profession { get; set; }

		[AoMember(5)]
		public int Level { get; set; }

		[AoMember(6, SerializeSize = ArraySizeType.Int32)]
		public string AreaName { get; set; }

		[AoMember(7)]
		public int Unknown2 { get; set; }

		[AoMember(8)]
		public int Unknown3 { get; set; }

		[AoMember(9)]
		public int HeadMesh { get; set; }

		[AoMember(10)]
		public int MonsterScale { get; set; }

		[AoMember(11)]
		public Fatness Fatness { get; set; }

		[AoMember(12)]
		public StarterArea StarterArea { get; set; }

		public CreateCharacterMessage()
		{
			base.SystemMessageType = SystemMessageType.CreateCharacter;
		}
	}
	[AoContract(20)]
	public class DeleteCharacterMessage : SystemMessage
	{
		[AoMember(0)]
		public int CharacterId { get; set; }

		public DeleteCharacterMessage()
		{
			base.SystemMessageType = SystemMessageType.DeleteCharacter;
		}
	}
	public enum LoginError
	{
		AlreadyLoggedIn = 20,
		InvalidUserNamePassword = 106,
		PlayerBannedOrNotPaid = 108
	}
	[AoContract(13)]
	public class LoginErrorMessage : SystemMessage
	{
		[AoMember(0)]
		public LoginError Error { get; set; }

		public LoginErrorMessage()
		{
			base.SystemMessageType = SystemMessageType.LoginError;
		}
	}
	[AoContract(16)]
	public class NameInUseMessage : SystemMessage
	{
		[AoMember(0)]
		public int Unknown { get; set; }

		public NameInUseMessage()
		{
			base.SystemMessageType = SystemMessageType.NameInUse;
			Unknown = 30;
		}
	}
	[AoContract(85)]
	public class RandomNameRequestMessage : SystemMessage
	{
		[AoMember(0)]
		public Profession Profession { get; set; }

		public RandomNameRequestMessage()
		{
			base.SystemMessageType = SystemMessageType.RandomNameRequest;
		}
	}
	[AoContract(22)]
	public class SelectCharacterMessage : SystemMessage
	{
		[AoMember(0)]
		public int CharacterId { get; set; }

		public SelectCharacterMessage()
		{
			base.SystemMessageType = SystemMessageType.SelectCharacter;
		}
	}
	[AoContract(36)]
	public class ServerSaltMessage : SystemMessage
	{
		[AoMember(0, IsFixedSize = true, FixedSizeLength = 32)]
		public byte[] ServerSalt { get; set; }

		public ServerSaltMessage()
		{
			base.SystemMessageType = SystemMessageType.ServerSalt;
		}
	}
	public enum StarterArea
	{
		RubiKa,
		Shadowlands
	}
	[AoContract(86)]
	public class SuggestNameMessage : SystemMessage
	{
		[AoMember(0, SerializeSize = ArraySizeType.Int16)]
		public string Name { get; set; }

		public SuggestNameMessage()
		{
			base.SystemMessageType = SystemMessageType.SuggestName;
		}
	}
	[AoContract(37)]
	public class UserCredentialsMessage : SystemMessage
	{
		[AoMember(0, IsFixedSize = true, FixedSizeLength = 40)]
		public string UserName { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int32)]
		public string Credentials { get; set; }

		public UserCredentialsMessage()
		{
			base.SystemMessageType = SystemMessageType.UserCredentials;
		}
	}
	[AoContract(34)]
	public class UserLoginMessage : SystemMessage
	{
		[AoMember(0)]
		public int Unknown { get; set; }

		[AoMember(1, IsFixedSize = true, FixedSizeLength = 40)]
		public string UserName { get; set; }

		[AoMember(2, IsFixedSize = true, FixedSizeLength = 20)]
		public string ClientVersion { get; set; }

		public UserLoginMessage()
		{
			base.SystemMessageType = SystemMessageType.UserLogin;
			Unknown = 2;
		}
	}
	[AoContract(23)]
	public class ZoneInfoMessage : SystemMessage
	{
		[AoMember(0)]
		public int CharacterId { get; set; }

		[AoMember(1)]
		public IPAddress ServerIpAddress { get; set; }

		[AoMember(2)]
		public ushort ServerPort { get; set; }

		[AoMember(3)]
		public uint Cookie1 { get; set; }

		[AoMember(4)]
		public uint Cookie2 { get; set; }

		public ZoneInfoMessage()
		{
			base.SystemMessageType = SystemMessageType.ZoneInfo;
		}
	}
	[AoContract(27)]
	public class ZoneLoginMessage : SystemMessage
	{
		[AoMember(0)]
		public int CharacterId { get; set; }

		[AoMember(1)]
		public uint Cookie1 { get; set; }

		[AoMember(2)]
		public uint Cookie2 { get; set; }

		public ZoneLoginMessage()
		{
			base.SystemMessageType = SystemMessageType.ZoneLogin;
		}
	}
	[AoContract(60)]
	public class ZoneRedirectionMessage : SystemMessage
	{
		[AoMember(0)]
		public IPAddress ServerIpAddress { get; set; }

		[AoMember(1)]
		public ushort ServerPort { get; set; }

		public ZoneRedirectionMessage()
		{
			base.SystemMessageType = SystemMessageType.ZoneRedirection;
		}
	}
}
namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
	[AoContract(912151899)]
	public class CityAdvantagesMessage : N3Message
	{
		[AoMember(0, SerializeSize = ArraySizeType.Int32)]
		public AdvantagesItem[] Advantages { get; set; }

		public CityAdvantagesMessage()
		{
			base.N3MessageType = N3MessageType.CityAdvantages;
		}
	}
	[AoContract(642670433)]
	public class AbsorbMessage : N3Message
	{
		[AoMember(0)]
		public int Amount { get; set; }

		[AoMember(1)]
		public Stat DmgType { get; set; }

		public AbsorbMessage()
		{
			base.N3MessageType = N3MessageType.Absorb;
		}
	}
	[AoContract(1174417174)]
	public class AttackInfoMessage : N3Message
	{
		[AoMember(0)]
		public int Amount { get; set; }

		[AoMember(1)]
		public int AmmoCount { get; set; }

		[AoMember(2)]
		public int WeaponSlot { get; set; }

		[AoMember(3)]
		public Identity Target { get; set; }

		[AoMember(4)]
		public int Unk1 { get; set; }

		[AoMember(5)]
		public HitType HitType { get; set; }

		[AoMember(6)]
		public int WeaponInstance { get; set; }

		public AttackInfoMessage()
		{
			base.N3MessageType = N3MessageType.AttackInfo;
		}
	}
	[AoContract(1330073093)]
	public class CorpseFullUpdateMessage : N3Message
	{
		private DateTime receivedAt;

		public DateTime DecayTime
		{
			get
			{
				int num = 18000;
				int num2 = 60;
				GameTuple<Stat, int>[] stats = Stats;
				foreach (GameTuple<Stat, int> gameTuple in stats)
				{
					if (gameTuple.Value1 == Stat.TimeExist)
					{
						num = gameTuple.Value2;
					}
					if (gameTuple.Value1 == Stat.DeadTimer)
					{
						num2 = gameTuple.Value2;
					}
				}
				return receivedAt.AddMinutes((double)num / (double)num2 / 100.0);
			}
		}

		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public int Unknown2 { get; set; }

		[AoMember(2)]
		public Identity Owner { get; set; }

		[AoMember(3)]
		public Vector3 Position { get; set; }

		[AoMember(4)]
		public Quaternion Heading { get; set; }

		[AoMember(5)]
		public int PlayfieldId { get; set; }

		[AoMember(6)]
		public Identity StateMachine { get; set; }

		[AoMember(7)]
		public short Unknown3 { get; set; }

		[AoMember(8, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<Stat, int>[] Stats { get; set; }

		[AoMember(10, SerializeSize = ArraySizeType.Int32)]
		public string Name { get; set; }

		[AoMember(11)]
		public int Unknown4 { get; set; }

		[AoMember(12)]
		public int Unknown5 { get; set; }

		[AoMember(12, SerializeSize = ArraySizeType.X3F1)]
		public int[] UnknownArray { get; set; }

		[AoMember(13)]
		public int Unknown6 { get; set; }

		[AoMember(14, SerializeSize = ArraySizeType.X3F1)]
		public AnimationEffect[] AnimationEffects { get; set; }

		[AoMember(15)]
		public Identity UnknownIdentity { get; set; }

		[AoMember(16, SerializeSize = ArraySizeType.X3F1)]
		public Texture[] Textures { get; set; }

		public CorpseFullUpdateMessage()
		{
			base.N3MessageType = N3MessageType.CorpseFullUpdate;
			receivedAt = DateTime.Now;
		}
	}
	[AoContract(1283276859)]
	public class DoorStatusUpdateMessage : N3Message
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public int Unknown2 { get; set; }

		[AoMember(2)]
		public byte Unknown3 { get; set; }

		[AoMember(3)]
		public short Unknown4 { get; set; }

		[AoMember(4, SerializeSize = ArraySizeType.X3F1)]
		public int UnknownArray { get; set; }

		public DoorStatusUpdateMessage()
		{
			base.N3MessageType = N3MessageType.DoorStatusUpdate;
		}
	}
	[AoContract(924648770)]
	public class FightModeUpdate : N3Message
	{
		[AoMember(0)]
		public Identity ResourceIdentity { get; set; }

		[AoMember(0, SerializeSize = ArraySizeType.X3F1)]
		public FightModeName[] Name { get; set; }

		protected FightModeUpdate()
		{
			base.N3MessageType = N3MessageType.FightModeUpdate;
		}
	}
	[AoContract(1651777045)]
	public class AOTransportSignalMessage : N3Message
	{
		[AoFlags("action")]
		[AoMember(0)]
		public AOSignalAction Action { get; set; }

		[AoUsesFlags("action", typeof(CityInfo), FlagsCriteria.EqualsToAny, new int[] { 5 })]
		[AoUsesFlags("action", typeof(CityCreditsUpkeep), FlagsCriteria.EqualsToAny, new int[] { 10 })]
		[AoUsesFlags("action", typeof(CloakInfo), FlagsCriteria.EqualsToAny, new int[] { 14 })]
		[AoUsesFlags("action", typeof(CityNextUpkeep), FlagsCriteria.EqualsToAny, new int[] { 13 })]
		[AoUsesFlags("action", typeof(CityCharge), FlagsCriteria.EqualsToAny, new int[] { 15 })]
		[AoMember(1)]
		public IAOTransportSignalMessage TransportSignalMessage { get; set; }

		public AOTransportSignalMessage()
		{
			base.N3MessageType = N3MessageType.AOTransportSignal;
		}
	}
	public interface IAOTransportSignalMessage
	{
	}
	public class CityInfo : IAOTransportSignalMessage
	{
		[AoMember(0)]
		public Identity UnknownIdentity1 { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public Identity UnknownIdentity2 { get; set; }

		[AoMember(3)]
		public Identity User { get; set; }

		[AoMember(4)]
		public int Unknown2 { get; set; }

		[AoMember(5)]
		public int Unknown3 { get; set; }

		[AoMember(6, SerializeSize = ArraySizeType.Int32)]
		public string OrgName { get; set; }
	}
	public class CityCreditsUpkeep : IAOTransportSignalMessage
	{
		[AoMember(0)]
		public int CreditsUpkeep { get; set; }
	}
	public class CloakInfo : IAOTransportSignalMessage
	{
		[AoMember(0)]
		public CloakStatus CloakState { get; set; }

		[AoMember(1)]
		public int ShieldTimerInSeconds { get; set; }
	}
	public class CityNextUpkeep : IAOTransportSignalMessage
	{
		[AoMember(0)]
		public int NextUpkeepPaymentInSeconds { get; set; }
	}
	public class CityCharge : IAOTransportSignalMessage
	{
		[AoMember(0)]
		public float CityControllerCharge { get; set; }
	}
	[AoContract(855716730)]
	public class InfromPlayerMessage : N3Message
	{
		[AoMember(0)]
		public Identity UnkIdentity { get; set; }

		public InfromPlayerMessage()
		{
			base.N3MessageType = N3MessageType.InfromPlayer;
		}
	}
	[AoContract(1515741029)]
	public class InspectMessage : N3Message
	{
		[AoMember(0)]
		public Identity Target { get; set; }

		[AoMember(1)]
		public InspectSlotInfo[] Slot { get; set; }

		public InspectMessage()
		{
			base.N3MessageType = N3MessageType.Inspect;
		}
	}
	[AoContract(642866785)]
	public class ReloadMessage : N3Message
	{
		[AoMember(0)]
		public Identity AmmoSlot { get; set; }

		[AoMember(1)]
		public Identity WeaponIdentity { get; set; }

		[AoMember(2)]
		public int AmmoCount { get; set; }

		[AoMember(3)]
		public int WeaponAmmoCount { get; set; }

		public ReloadMessage()
		{
			base.N3MessageType = N3MessageType.Reload;
		}
	}
	[AoContract(624755264)]
	public class ResearchUpdateMessage : N3Message
	{
		[AoMember(0)]
		public byte Unknown1 { get; set; }

		[AoMember(1, FixedSizeLength = 54, IsFixedSize = true, SerializeSize = ArraySizeType.NoSerialization)]
		public ResearchLine[] ResearchLine { get; set; }

		[AoMember(1)]
		public int Unknown2 { get; set; }

		public ResearchUpdateMessage()
		{
			base.N3MessageType = N3MessageType.ResearchUpdate;
		}
	}
	[AoContract(1180319841)]
	public class QuestFullUpdateMessage : N3Message
	{
		[AoMember(0, SerializeSize = ArraySizeType.X3F1)]
		public Quest[] Quests { get; set; }

		public QuestFullUpdateMessage()
		{
			base.N3MessageType = N3MessageType.QuestFullUpdate;
		}
	}
	[AoContract(1495335206)]
	public class PlayfieldAllCitiesMessage : N3Message
	{
		[AoMember(0)]
		public short Unknown1 { get; set; }

		public PlayfieldAllCitiesMessage()
		{
			base.N3MessageType = N3MessageType.PlayfieldAllCities;
		}
	}
	[AoContract(675889264)]
	public class AttackMessage : N3Message
	{
		[AoMember(0)]
		public Identity Target { get; set; }

		[AoMember(1)]
		public byte Unknown1 { get; set; }

		public AttackMessage()
		{
			base.N3MessageType = N3MessageType.Attack;
		}
	}
	[AoContract(1145584442)]
	public class SendScoreMessage : N3Message
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public BattlestationSide A { get; set; }

		[AoMember(2)]
		public BattlestationSide B { get; set; }

		[AoMember(3)]
		public BattlestationSide C { get; set; }

		[AoMember(4)]
		public BattlestationSide Core { get; set; }

		[AoMember(5)]
		public int Unknown2 { get; set; }

		[AoMember(6)]
		public int RedScore { get; set; }

		[AoMember(7)]
		public int BlueScore { get; set; }

		public SendScoreMessage()
		{
			base.N3MessageType = N3MessageType.SendScore;
		}
	}
	[AoContract(1294610747)]
	public class TeamInviteMessage : N3Message
	{
		[AoMember(0)]
		public Identity Requestor { get; set; }

		[AoMember(1)]
		public byte Unknown1 { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.Int16)]
		public string Name { get; set; }

		public TeamInviteMessage()
		{
			base.N3MessageType = N3MessageType.TeamInvite;
		}
	}
	[AoContract(1180327283)]
	public class ChestFullUpdateMessage : N3Message
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public Identity Owner { get; set; }

		[AoMember(2)]
		public int PlayfieldId { get; set; }

		[AoMember(3)]
		public Identity StateMachine { get; set; }

		[AoMember(4)]
		public short Unknown5 { get; set; }

		[AoMember(5, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<Stat, int>[] Stats { get; set; }

		[AoMember(6)]
		public int Unknown6 { get; set; }

		[AoMember(7)]
		public int Unknown7 { get; set; }

		[AoMember(8)]
		public int Unknown8 { get; set; }

		[AoMember(9, SerializeSize = ArraySizeType.X3F1)]
		public int[] UnknownArray { get; set; }

		public ChestFullUpdateMessage()
		{
			base.N3MessageType = N3MessageType.ChestFullUpdate;
		}
	}
	[AoContract(1196653092)]
	public class ContainerAddItem : N3Message
	{
		[AoMember(0)]
		public Identity Source { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public int Slot { get; set; }

		public ContainerAddItem()
		{
			base.N3MessageType = N3MessageType.ContainerAddItem;
		}
	}
	[AoContract(911888497)]
	public class DoorFullUpdateMessage : N3Message
	{
		[AoMember(0)]
		public Identity Door { get; set; }

		[AoMember(1)]
		public int Unk1 { get; set; }

		[AoMember(2)]
		public Vector3 Position { get; set; }

		[AoMember(3)]
		public Quaternion Heading { get; set; }

		[AoMember(4)]
		public int Playfield { get; set; }

		public DoorFullUpdateMessage()
		{
			base.N3MessageType = N3MessageType.DoorFullUpdate;
		}
	}
	[AoContract(689911323)]
	public class CreateQuestMessage : N3Message
	{
		[AoMember(0)]
		public Identity MissionId { get; set; }

		public CreateQuestMessage()
		{
			base.N3MessageType = N3MessageType.CreateQuest;
		}
	}
	[AoContract(86912780)]
	public class AddTemplateMessage : N3Message
	{
		[AoMember(0)]
		public int HighId { get; set; }

		[AoMember(1)]
		public int LowId { get; set; }

		[AoMember(2)]
		public int Quality { get; set; }

		[AoMember(3)]
		public int Count { get; set; }

		public AddTemplateMessage()
		{
			base.N3MessageType = N3MessageType.AddTemplate;
		}
	}
	[AoContract(1096961805)]
	public class AppearanceUpdateMessage : N3Message
	{
		[AoMember(0, SerializeSize = ArraySizeType.X3F1)]
		public Texture[] Textures { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.X3F1)]
		public SmokeLounge.AOtomation.Messaging.GameData.Mesh[] Meshes { get; set; }

		[AoMember(2)]
		public short VisualFlags { get; set; }

		[AoMember(3)]
		public byte Unknown1 { get; set; }

		public AppearanceUpdateMessage()
		{
			base.N3MessageType = N3MessageType.AppearanceUpdate;
		}
	}
	[AoContract(923805036)]
	public class HealthDamageMessage : N3Message
	{
		[AoMember(0)]
		public int TargetHp { get; set; }

		[AoMember(1)]
		public int Amount { get; set; }

		[AoMember(2)]
		public Stat Stat { get; set; }

		[AoMember(3)]
		public int Unk1 { get; set; }

		[AoMember(4)]
		public Identity Target { get; set; }

		[AoMember(5)]
		public int Unk2 { get; set; }

		public HealthDamageMessage()
		{
			base.N3MessageType = N3MessageType.HealthDamage;
		}
	}
	[AoContract(990979439)]
	public class SimpleItemFullUpdateMessage : N3Message
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoFlags("OwnerType")]
		[AoMember(1)]
		public int OwnerType { get; set; }

		[AoMember(2)]
		public int OwnerInstance { get; set; }

		[AoUsesFlags("OwnerType", typeof(Vector3), FlagsCriteria.EqualsToAny, new int[] { 0 })]
		[AoMember(3)]
		public Vector3? Position { get; set; }

		[AoUsesFlags("OwnerType", typeof(Quaternion), FlagsCriteria.EqualsToAny, new int[] { 0 })]
		[AoMember(4)]
		public Quaternion? Rotation { get; set; }

		[AoMember(5)]
		public int PlayfieldId { get; set; }

		[AoMember(6)]
		public Identity StateMachine { get; set; }

		[AoMember(7)]
		public short Unknown2 { get; set; }

		[AoMember(8, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<Stat, int>[] Stats { get; set; }

		public SimpleItemFullUpdateMessage()
		{
			base.N3MessageType = N3MessageType.SimpleItemFullUpdate;
		}
	}
	[AoContract(1528694060)]
	public class PlayfieldTowerUpdateClientMessage : N3Message
	{
		[AoMember(0)]
		public Identity TowerId { get; set; }

		[AoMember(1)]
		public PlayfieldUpdateClientType UpdateType { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.X3F1)]
		public TowerInfo Tower { get; set; }

		public PlayfieldTowerUpdateClientMessage()
		{
			base.N3MessageType = N3MessageType.PlayfieldTowerUpdateClient;
		}
	}
	public enum PlayfieldUpdateClientType
	{
		Destroyed = 1,
		Planted
	}
	[AoContract(1428293414)]
	public class PlayfieldAllTowersMessage : N3Message
	{
		[AoMember(0, SerializeSize = ArraySizeType.X3F1)]
		public TowerInfo[] TowerInfo { get; set; }

		public PlayfieldAllTowersMessage()
		{
			base.N3MessageType = N3MessageType.PlayfieldAllTowers;
		}
	}
	[AoContract(975454017)]
	public class DropTemplateMessage : N3Message
	{
		[AoMember(0)]
		public Identity Item { get; set; }

		[AoMember(1)]
		public Vector3 Position { get; set; }

		public DropTemplateMessage()
		{
			base.N3MessageType = N3MessageType.DropTemplate;
		}
	}
	[AoContract(1550142248)]
	public class MissedAttackInfoMessage : N3Message
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public int Unknown2 { get; set; }

		[AoMember(2)]
		public Identity Attacker { get; set; }

		[AoMember(3)]
		public Identity Defender { get; set; }

		[AoMember(4)]
		public int Unknown3 { get; set; }

		public MissedAttackInfoMessage()
		{
			base.N3MessageType = N3MessageType.MissedAttackInfo;
		}
	}
	[AoContract(924019819)]
	public class PickUpMessage : N3Message
	{
		[AoMember(0)]
		public Identity Target { get; set; }

		public PickUpMessage()
		{
			base.N3MessageType = N3MessageType.PickUp;
		}
	}
	[AoContract(1363747104)]
	public class CharSecSpecAttackMessage : N3Message
	{
		[AoMember(0)]
		public Identity Target { get; set; }

		[AoMember(1)]
		public Stat Stat { get; set; }

		public CharSecSpecAttackMessage()
		{
			base.N3MessageType = N3MessageType.CharSecSpecAttack;
		}
	}
	[AoContract(1191915028)]
	public class MarketSendMessage : N3Message
	{
		[AoMember(0)]
		public Identity Sender { get; set; }

		[AoMember(1)]
		public int Credits { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.X3F1)]
		public MarketSendSlot[] Items { get; set; }

		public MarketSendMessage()
		{
			base.N3MessageType = N3MessageType.MarketSend;
		}
	}
	public enum PetCommand
	{
		Follow = 1,
		Behind = 2,
		Wait = 4,
		Guard = 6,
		Attack = 7,
		Social = 9,
		Terminate = 10,
		Release = 11,
		Heal = 12,
		Report = 14,
		Chat = 16
	}
	[AoContract(1798517507)]
	public class PetCommandMessage : N3Message
	{
		[AoMember(0)]
		public int Unk1 { get; set; }

		[AoMember(1)]
		public PetCommand Command { get; set; }

		[AoMember(2)]
		public int Unk2 { get; set; }

		[AoMember(3, SerializeSize = ArraySizeType.X3F1)]
		public PetBase[] Pets { get; set; }

		[AoMember(4)]
		public int Unk3 { get; set; }

		[AoMember(5)]
		public int Unk4 { get; set; }

		public PetCommandMessage()
		{
			base.N3MessageType = N3MessageType.PetCommand;
			Pets = new PetBase[0];
		}
	}
	public class PetBase
	{
		[AoMember(0)]
		public Identity Identity { get; set; }

		public PetBase()
		{
		}

		public PetBase(Identity identity)
		{
			Identity = identity;
		}
	}
	[AoContract(1547920905)]
	public class QuestAlternativeMessage : N3Message
	{
		[AoMember(0)]
		public byte Unknown1 { get; set; }

		[AoMember(1)]
		public MissionSliders MissionSliders { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public MissionScope Scope { get; set; }

		[AoMember(4)]
		public Identity Terminal { get; set; }

		[AoMember(5, SerializeSize = ArraySizeType.Byte)]
		public MissionInfo[] MissionDetails { get; set; }

		public QuestAlternativeMessage()
		{
			base.N3MessageType = N3MessageType.QuestAlternative;
		}
	}
	[AoContract(2134923798)]
	public class NewLevelMessage : N3Message
	{
		public NewLevelMessage()
		{
			base.N3MessageType = N3MessageType.NewLevel;
		}
	}
	[AoContract(473583479)]
	public class ReflectAttackMessage : N3Message
	{
		[AoMember(0)]
		public int Amount { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public Stat Stat { get; set; }

		public ReflectAttackMessage()
		{
			base.N3MessageType = N3MessageType.ReflectAttack;
		}
	}
	[AoContract(622404726)]
	public class ShieldAttackMessage : N3Message
	{
		[AoMember(0)]
		public int Amount { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public Stat Stat { get; set; }

		public ShieldAttackMessage()
		{
			base.N3MessageType = N3MessageType.ShieldAttack;
		}
	}
	[AoContract(1042306656)]
	public class SkillMessage : N3Message
	{
		[AoMember(0, SerializeSize = ArraySizeType.Int32)]
		public GameTuple<Stat, uint>[] Skills { get; set; }

		public SkillMessage()
		{
			base.N3MessageType = N3MessageType.Skill;
		}
	}
	[AoContract(1968115989)]
	public class SpecialAttackInfoMessage : N3Message
	{
		[AoMember(0)]
		public EquipSlot EquipSlot { get; set; }

		[AoMember(1)]
		public int Amount { get; set; }

		[AoMember(2)]
		public int AmmoCount { get; set; }

		[AoMember(3)]
		public Identity Target { get; set; }

		[AoMember(4)]
		public Stat Stat { get; set; }

		[AoMember(5)]
		public int Unk1 { get; set; }

		public SpecialAttackInfoMessage()
		{
			base.N3MessageType = N3MessageType.SpecialAttackInfo;
		}
	}
	[AoContract(425609582)]
	public class SetPosMessage : N3Message
	{
		[AoMember(0)]
		public Vector3 Position { get; set; }

		public SetPosMessage()
		{
			base.N3MessageType = N3MessageType.SetPos;
		}
	}
	[AoContract(1245782078)]
	public class StopFightMessage : N3Message
	{
		[AoMember(0)]
		public int Unk { get; set; }

		public StopFightMessage()
		{
			base.N3MessageType = N3MessageType.StopFight;
		}
	}
	[AoContract(876357759)]
	public class BankMessage : N3Message
	{
		[AoMember(0, SerializeSize = ArraySizeType.X3F1)]
		public InventorySlot[] BankSlots { get; set; }

		public BankMessage()
		{
			base.N3MessageType = N3MessageType.Bank;
		}
	}
	[AoContract(376062814)]
	public class AcceptBSInviteMessage : N3Message
	{
		[AoMember(0)]
		public Identity UnkIdentity { get; set; }

		[AoMember(1)]
		public byte UnkByte { get; set; }

		public AcceptBSInviteMessage()
		{
			base.N3MessageType = N3MessageType.AcceptBSInvite;
		}
	}
	[AoContract(623988077)]
	public class CastNanoSpellMessage : N3Message
	{
		[AoMember(0)]
		public int NanoId { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public int Unknown1 { get; set; }

		[AoMember(3)]
		public Identity Caster { get; set; }

		public CastNanoSpellMessage()
		{
			base.N3MessageType = N3MessageType.CastNanoSpell;
		}
	}
	public enum RaidCmdType
	{
		CreateRaid = 1,
		ShowMemberList = 2,
		MoveMember = 4,
		RequestLocks = 6
	}
	[AoContract(1314020952)]
	public class RaidCmdMessage : N3Message
	{
		[AoMember(0)]
		public RaidCmdType CommandType { get; set; }

		[AoMember(1)]
		public int Unknown2 { get; set; }

		[AoMember(2)]
		public int Unknown3 { get; set; }

		public RaidCmdMessage()
		{
			base.N3MessageType = N3MessageType.RaidCmd;
		}
	}
	[AoContract(1581741936)]
	public class CharacterActionMessage : N3Message
	{
		[AoMember(0)]
		public CharacterActionType Action { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public Identity Target { get; set; }

		[AoMember(3)]
		public int Parameter1 { get; set; }

		[AoMember(4)]
		public int Parameter2 { get; set; }

		[AoMember(5)]
		public short Unknown2 { get; set; }

		public CharacterActionMessage()
		{
			base.N3MessageType = N3MessageType.CharacterAction;
		}
	}
	[AoContract(1410404643)]
	public class CharDCMoveMessage : N3Message
	{
		[AoMember(0)]
		public MovementAction MoveType { get; set; }

		[AoMember(1)]
		public Quaternion Heading { get; set; }

		[AoMember(2)]
		public Vector3 Position { get; set; }

		[AoMember(3)]
		public int DeltaTime { get; set; }

		[AoMember(4)]
		public int Unknown2 { get; set; }

		[AoMember(5)]
		public int Unknown3 { get; set; }

		public CharDCMoveMessage()
		{
			base.N3MessageType = N3MessageType.CharDCMove;
		}
	}
	[AoContract(1460412473)]
	public class CharInPlayMessage : N3Message
	{
		public CharInPlayMessage()
		{
			base.N3MessageType = N3MessageType.CharInPlay;
		}
	}
	[AoContract(1548900987)]
	public class ChatCmdMessage : N3Message
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.Int32)]
		public string Command { get; set; }

		public ChatCmdMessage()
		{
			base.N3MessageType = N3MessageType.ChatCmd;
		}
	}
	[AoContract(1598768170)]
	public class ChatTextMessage : N3Message
	{
		[AoMember(0, SerializeSize = ArraySizeType.Int16)]
		public string Text { get; set; }

		[AoMember(1)]
		public short Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		public ChatTextMessage()
		{
			base.N3MessageType = N3MessageType.ChatText;
		}
	}
	[AoContract(1416181567)]
	public class ClientMoveItemToInventory : N3Message
	{
		[AoMember(0)]
		public Identity SourceContainer { get; set; }

		[AoMember(1)]
		public int Slot { get; set; }

		public ClientMoveItemToInventory()
		{
			base.N3MessageType = N3MessageType.ClientMoveItemToInventory;
		}
	}
	[AoContract(525164414)]
	public class ClientContainerAddItem : N3Message
	{
		[AoMember(0)]
		public Identity Target { get; set; }

		[AoMember(1)]
		public Identity Source { get; set; }

		public ClientContainerAddItem()
		{
			base.N3MessageType = N3MessageType.ClientContainerAddItem;
		}
	}
	[AoContract(911278200)]
	public class DespawnMessage : N3Message
	{
		public DespawnMessage()
		{
			base.N3MessageType = N3MessageType.Despawn;
		}
	}
	[AoContract(1347702041)]
	public class FeedbackMessage : N3Message
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public int CategoryId { get; set; }

		[AoMember(2)]
		public int MessageId { get; set; }

		public FeedbackMessage()
		{
			base.N3MessageType = N3MessageType.Feedback;
		}
	}
	[AoContract(638531185)]
	public class FollowTargetMessage : N3Message
	{
		public interface IInfo
		{
		}

		public class TargetInfo : IInfo
		{
			[AoMember(0)]
			public Identity Target { get; set; }

			[AoMember(1)]
			public int Unknown1 { get; set; }

			[AoMember(2)]
			public int Unknown2 { get; set; }

			[AoMember(3)]
			public int Unknown3 { get; set; }

			[AoMember(4)]
			public int Unknown4 { get; set; }
		}

		public class PathInfo : IInfo
		{
			[AoMember(0, SerializeSize = ArraySizeType.Byte)]
			public Vector3[] Waypoints { get; set; }
		}

		[AoMember(0)]
		[AoFlags("Type")]
		public FollowTargetType Type { get; set; }

		[AoMember(1)]
		public byte Unknown1 { get; set; }

		[AoMember(2)]
		[AoUsesFlags("Type", typeof(PathInfo), FlagsCriteria.EqualsToAny, new int[] { 1 })]
		[AoUsesFlags("Type", typeof(TargetInfo), FlagsCriteria.EqualsToAny, new int[] { 2 })]
		public IInfo Info { get; set; }

		public FollowTargetMessage()
		{
			base.N3MessageType = N3MessageType.FollowTarget;
		}
	}
	public enum FollowTargetType : byte
	{
		NpcPath = 1,
		Target
	}
	[AoContract(543902579)]
	public class FormatFeedbackMessage : N3Message
	{
		private string _formattedMessage = null;

		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public string Message { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		public string FormattedMessage
		{
			get
			{
				if (_formattedMessage == null)
				{
					_formattedMessage = FormatMessage();
				}
				return _formattedMessage;
			}
		}

		public FormatFeedbackMessage()
		{
			base.N3MessageType = N3MessageType.FormatFeedback;
		}

		private string FormatMessage()
		{
			StdString stdString = StdString.Create();
			RemoteFormat.ParseString(stdString.Pointer, Message);
			string result = stdString.ToString();
			stdString.Dispose();
			return result;
		}
	}
	[AoContract(691028809)]
	public class FullCharacterMessage : N3Message
	{
		public class TeamMember
		{
			[AoMember(0)]
			public Identity Identity { get; set; }

			[AoMember(1, SerializeSize = ArraySizeType.Int16)]
			public string Name { get; set; }

			[AoMember(2)]
			public int Unknown1 { get; set; }

			[AoMember(3)]
			public byte Unknown2 { get; set; }

			[AoMember(4)]
			public short Level { get; set; }

			[AoMember(5)]
			public short Profession { get; set; }
		}

		public class UnknownDataType1
		{
			[AoMember(0)]
			public byte Unknown1 { get; set; }

			[AoMember(1)]
			public byte Unknown2 { get; set; }

			[AoMember(2)]
			public byte Unknown3 { get; set; }
		}

		public class UnknownDataType2
		{
			[AoMember(0)]
			public int Unknown1 { get; set; }

			[AoMember(1)]
			public Identity Unknown2 { get; set; }

			[AoMember(2)]
			public int Unknown3 { get; set; }

			[AoMember(3)]
			public int Unknown4 { get; set; }
		}

		public class UnknownDataType4
		{
			[AoMember(0)]
			public int Unknown1 { get; set; }

			[AoMember(1)]
			public int Unknown2 { get; set; }

			[AoMember(2)]
			public int Unknown3 { get; set; }

			[AoMember(3)]
			public int Unknown4 { get; set; }

			[AoMember(4)]
			public int Unknown5 { get; set; }

			[AoMember(5)]
			public int Unknown6 { get; set; }

			[AoMember(6)]
			public int Unknown7 { get; set; }

			[AoMember(7)]
			public int Unknown8 { get; set; }

			[AoMember(8)]
			public int Unknown9 { get; set; }

			[AoMember(9)]
			public int Unknown10 { get; set; }
		}

		public class Perk
		{
			[AoMember(0)]
			public int SkillId { get; set; }

			[AoMember(1)]
			public int Unknown1 { get; set; }

			[AoMember(2)]
			public int Unknown2 { get; set; }

			[AoMember(3)]
			public int Unknown3 { get; set; }
		}

		[AoMember(0)]
		public int Version { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.X3F1)]
		public InventorySlot[] InventorySlots { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.X3F1)]
		public int[] UploadedNanoIds { get; set; }

		[AoMember(3, SerializeSize = ArraySizeType.X3F1)]
		public UnknownDataType1[] Unknown2 { get; set; }

		[AoMember(4)]
		public int Unknown3 { get; set; }

		[AoMember(5, SerializeSize = ArraySizeType.Int32)]
		public UnknownDataType2[] Unknown4 { get; set; }

		[AoMember(6)]
		public int Unknown5 { get; set; }

		[AoMember(7, SerializeSize = ArraySizeType.Int32)]
		public UnknownDataType2[] Unknown6 { get; set; }

		[AoMember(8)]
		public int Unknown7 { get; set; }

		[AoMember(9, SerializeSize = ArraySizeType.Int32)]
		public UnknownDataType2[] Unknown8 { get; set; }

		[AoMember(10, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<int, int>[] Stats1 { get; set; }

		[AoMember(11, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<int, int>[] Stats2 { get; set; }

		[AoMember(12, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<byte, byte>[] Stats3 { get; set; }

		[AoMember(13, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<byte, short>[] Stats4 { get; set; }

		[AoMember(14, SerializeSize = ArraySizeType.Int32)]
		public GameTuple<int, int>[] AbsorbStats { get; set; }

		[AoMember(15, SerializeSize = ArraySizeType.Int32)]
		public Identity[] UnknownIdentities { get; set; }

		[AoMember(16, SerializeSize = ArraySizeType.X3F1)]
		public TeamMember[] TeamMembers { get; set; }

		[AoMember(17, SerializeSize = ArraySizeType.X3F1)]
		public UnknownDataType4[] Unknown12 { get; set; }

		[AoMember(18, SerializeSize = ArraySizeType.X3F1)]
		public Perk[] Perks { get; set; }

		public FullCharacterMessage()
		{
			base.N3MessageType = N3MessageType.FullCharacter;
			base.Unknown = 0;
		}
	}
	[AoContract(1599226158)]
	public class GameTimeMessage : N3Message
	{
		[AoMember(0)]
		public float Unknown1 { get; set; }

		[AoMember(1)]
		public int Unknown2 { get; set; }

		[AoMember(2)]
		public int Unknown3 { get; set; }

		[AoMember(3)]
		public float Unknown4 { get; set; }

		public GameTimeMessage()
		{
			base.N3MessageType = N3MessageType.GameTime;
		}
	}
	public enum GenericCmdAction
	{
		None,
		Get,
		Drop,
		Use,
		Repair,
		UseItemOnItem
	}
	[AoContract(1381132376)]
	public class GenericCmdMessage : N3Message
	{
		[AoMember(0)]
		public int Temp1 { get; set; }

		[AoMember(1)]
		public int Count { get; set; }

		[AoFlags("action")]
		[AoMember(2)]
		public GenericCmdAction Action { get; set; }

		[AoMember(3)]
		public int Temp4 { get; set; }

		[AoMember(4)]
		public Identity User { get; set; }

		[AoUsesFlags("action", typeof(Identity), FlagsCriteria.EqualsToAny, new int[] { 4, 5 })]
		[AoMember(5)]
		public Identity? Source { get; set; }

		[AoMember(6)]
		public Identity Target { get; set; }

		public GenericCmdMessage()
		{
			base.N3MessageType = N3MessageType.GenericCmd;
		}
	}
	[AoContract(1295524910)]
	public class InfoPacketMessage : N3Message
	{
		[AoMember(0)]
		[AoFlags("flags")]
		public InfoPacketType Type { get; set; }

		[AoMember(1)]
		[AoUsesFlags("flags", typeof(CharacterInfoPacket), FlagsCriteria.EqualsToAny, new int[] { 64, 65, 67, 71 })]
		[AoUsesFlags("flags", typeof(MonsterInfoPacket), FlagsCriteria.EqualsToAny, new int[] { 80 })]
		[AoUsesFlags("flags", typeof(TowerInfoPacket), FlagsCriteria.EqualsToAny, new int[] { 84, 92 })]
		public InfoPacket Info { get; set; }

		public InfoPacketMessage()
		{
			base.N3MessageType = N3MessageType.InfoPacket;
		}
	}
	public enum InfoPacketType : byte
	{
		Character = 64,
		CharacterOrg = 65,
		CharacterOrgSite = 67,
		CharacterOrgSiteTower = 71,
		Monster = 80,
		Tower = 84,
		ControlTower = 92
	}
	[AoContract(1433423153)]
	public class KnuBotAnswerListMessage : N3Message
	{
		[AoMember(0)]
		public short Unknown1 { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.Int32)]
		public KnuBotDialogOption[] DialogOptions { get; set; }

		public KnuBotAnswerListMessage()
		{
			base.N3MessageType = N3MessageType.KnubotAnswerList;
		}
	}
	[AoContract(553854077)]
	public class KnuBotAnswerMessage : N3Message
	{
		[AoMember(0)]
		public short Unknown1 { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public int Answer { get; set; }

		public KnuBotAnswerMessage()
		{
			base.N3MessageType = N3MessageType.KnubotAnswer;
		}
	}
	[AoContract(1567642410)]
	public class KnuBotAppendTextMessage : N3Message
	{
		[AoMember(0)]
		public short Unknown1 { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3, SerializeSize = ArraySizeType.Int32)]
		public string Text { get; set; }

		[AoMember(4)]
		public int Unknown3 { get; set; }

		public KnuBotAppendTextMessage()
		{
			base.N3MessageType = N3MessageType.KnubotAppendText;
		}
	}
	[AoContract(654986338)]
	public class KnuBotCloseChatWindowMessage : N3Message
	{
		[AoMember(0)]
		public short Unknown1 { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		public KnuBotCloseChatWindowMessage()
		{
			base.N3MessageType = N3MessageType.KnubotCloseChatWindow;
		}
	}
	[AoContract(1432890148)]
	public class KnuBotFinishTradeMessage : N3Message
	{
		[AoMember(0)]
		public short Unknown1 { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public int Decline { get; set; }

		[AoMember(3)]
		public int Amount { get; set; }

		public KnuBotFinishTradeMessage()
		{
			base.N3MessageType = N3MessageType.KnubotFinishTrade;
		}
	}
	[AoContract(991112548)]
	public class KnuBotOpenChatWindowMessage : N3Message
	{
		[AoMember(0)]
		public short Unknown1 { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		public KnuBotOpenChatWindowMessage()
		{
			base.N3MessageType = N3MessageType.KnubotOpenChatWindow;
		}
	}
	[AoContract(757146631)]
	public class KnuBotRejectedItemsMessage : N3Message
	{
		[AoMember(0)]
		public short Unknown1 { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public KnuBotRejectedItem[] Items { get; set; }

		[AoMember(3)]
		public int Unknown2 { get; set; }

		public KnuBotRejectedItemsMessage()
		{
			base.N3MessageType = N3MessageType.KnubotRejectedItems;
		}
	}
	[AoContract(2019835933)]
	public class KnuBotStartTradeMessage : N3Message
	{
		[AoMember(0)]
		public short Unknown1 { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public int NumberOfItemSlotsInTradeWindow { get; set; }

		[AoMember(3, SerializeSize = ArraySizeType.Int32)]
		public string Message { get; set; }

		public KnuBotStartTradeMessage()
		{
			base.N3MessageType = N3MessageType.KnubotStartTrade;
			base.Identity = default(Identity);
			Target = default(Identity);
		}
	}
	[AoContract(974859276)]
	public class KnuBotTradeMessage : N3Message
	{
		[AoMember(0)]
		public short Unknown1 { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		[AoMember(4)]
		public int Unknown4 { get; set; }

		[AoMember(5)]
		public Identity Container { get; set; }

		public KnuBotTradeMessage()
		{
			base.N3MessageType = N3MessageType.KnubotTrade;
		}
	}
	[AoContract(575816799)]
	public class LookAtMessage : N3Message
	{
		[AoMember(0)]
		public Identity Target { get; set; }

		[AoMember(1)]
		public int Unk { get; set; }

		public LookAtMessage()
		{
			base.N3MessageType = N3MessageType.LookAt;
		}
	}
	[AoContract(1125743906)]
	public class N3TeleportMessage : N3Message
	{
		[AoMember(0)]
		public Vector3 Destination { get; set; }

		[AoMember(1)]
		public Quaternion Heading { get; set; }

		[AoMember(2)]
		public byte Unknown1 { get; set; }

		[AoMember(3)]
		public Identity Playfield { get; set; }

		[AoMember(4)]
		public int GameServerId { get; set; }

		[AoMember(5)]
		public int SgId { get; set; }

		[AoMember(6)]
		public Identity ChangePlayfield { get; set; }

		[AoMember(7)]
		public int Unknown4 { get; set; }

		[AoMember(8)]
		public int Unknown5 { get; set; }

		[AoMember(9)]
		public Identity Playfield2 { get; set; }

		[AoMember(10)]
		public int Unknown6 { get; set; }

		public N3TeleportMessage()
		{
			base.N3MessageType = N3MessageType.N3Teleport;
		}
	}
	[AoContract(2135634184)]
	public class OrgClientMessage : N3Message
	{
		[AoMember(0)]
		[AoFlags("flags")]
		public OrgClientCommand Command { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public int Unknown1 { get; set; }

		[AoMember(3, SerializeSize = ArraySizeType.Int16)]
		[AoUsesFlags("flags", typeof(string), FlagsCriteria.EqualsToAny, new int[]
		{
			1, 7, 9, 13, 17, 19, 20, 21, 23, 24,
			25, 26, 27, 28
		})]
		public string CommandArgs { get; set; }

		public OrgClientMessage()
		{
			base.N3MessageType = N3MessageType.OrgClient;
		}
	}
	[AoContract(774523499)]
	public class OrgInfoPacketMessage : N3Message
	{
		[AoMember(0)]
		public int OrgId { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public string Name { get; set; }

		public OrgInfoPacketMessage()
		{
			base.N3MessageType = N3MessageType.OrgInfoPacket;
		}
	}
	[AoContract(1598757433)]
	public class PlayfieldAnarchyFMessage : N3Message
	{
		public class UnknownStruct1
		{
			[AoMember(0)]
			public int Unknown1 { get; set; }

			[AoMember(1)]
			public Identity Unknown2 { get; set; }

			[AoMember(2)]
			public int Unknown3 { get; set; }

			[AoMember(3)]
			public Vector3 Unknown4 { get; set; }

			[AoMember(4)]
			public int Unknown5 { get; set; }

			[AoMember(5)]
			public int Unknown6 { get; set; }

			[AoMember(6)]
			public float Unknown7 { get; set; }

			[AoMember(7)]
			public int Unknown8 { get; set; }

			[AoMember(8)]
			public int Unknown9 { get; set; }
		}

		public class PlayfieldDynelInfo
		{
			[AoMember(0)]
			public IdentityType IdentityType { get; set; }

			[AoMember(1)]
			public int Unknown1 { get; set; }

			[AoMember(2)]
			public int Unknown2 { get; set; }

			[AoMember(3)]
			public int Unknown3 { get; set; }

			[AoMember(4)]
			public int Instance { get; set; }
		}

		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public Vector3 CharacterCoordinates { get; set; }

		[AoMember(2)]
		public byte Unknown2 { get; set; }

		[AoMember(3)]
		public Identity PlayfieldId1 { get; set; }

		[AoMember(4)]
		public int Unknown3 { get; set; }

		[AoMember(5)]
		public int SG { get; set; }

		[AoMember(6)]
		public Identity ProxyId { get; set; }

		[AoFlags("flags")]
		[AoMember(7)]
		public int UnknownIdType { get; set; }

		[AoMember(8)]
		public int UnknownIdInstance { get; set; }

		[AoMember(9)]
		public int Unknown5 { get; set; }

		[AoMember(10)]
		public int Unknown6 { get; set; }

		[AoUsesFlags("flags", typeof(UnknownStruct1), FlagsCriteria.EqualsToAny, new int[] { 51067 })]
		[AoMember(11)]
		public UnknownStruct1 Unknown7 { get; set; }

		[AoUsesFlags("flags", typeof(PlayfieldDynelInfo[]), FlagsCriteria.EqualsToAny, new int[] { 51067, 51069 })]
		[AoMember(12, SerializeSize = ArraySizeType.Int32)]
		public PlayfieldDynelInfo[] Dynels { get; set; }

		public PlayfieldAnarchyFMessage()
		{
			base.N3MessageType = N3MessageType.PlayfieldAnarchyF;
			base.Unknown = 0;
			Unknown1 = 4;
			Unknown2 = 97;
		}
	}
	[AoContract(1851741806)]
	public class SetStatMessage : N3Message
	{
		[AoMember(0)]
		public int Value { get; set; }

		[AoMember(1)]
		public Stat Stat { get; set; }

		public SetStatMessage(Stat stat, int value)
			: this()
		{
			Stat = stat;
			Value = value;
		}

		public SetStatMessage()
		{
			base.N3MessageType = N3MessageType.SetStat;
		}
	}
	[AoContract(1314089334)]
	public class InventoryUpdateMessage : N3Message
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public int Unknown2 { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.X3F1)]
		public InventorySlot[] Items { get; set; }

		[AoMember(3)]
		public Identity InventoryIdentity { get; set; }

		[AoMember(4)]
		public int Handle { get; set; }

		[AoMember(5)]
		public int Unknown3 { get; set; }

		public InventoryUpdateMessage()
		{
			base.N3MessageType = N3MessageType.InventoryUpdate;
		}
	}
	[AoContract(1479942688)]
	public class ShopUpdateMessage : N3Message
	{
		[AoMember(0, SerializeSize = ArraySizeType.X3F1)]
		public VendingMachineSlot[] VendingMachineSlots { get; set; }

		public ShopUpdateMessage()
		{
			base.N3MessageType = N3MessageType.ShopUpdate;
		}
	}
	[Flags]
	public enum SimpleCharFullUpdateFlags
	{
		None = 0,
		IsNpc = 1,
		UnknownFlag = 2,
		UnknownFlag6 = 8,
		UnknownFlag7 = 0x200000,
		HasExtendedTextures = 0x10,
		HasFightingTarget = 0x20,
		HasPlayfieldId = 0x40,
		HasHeadMesh = 0x80,
		HasNoWeaponPairs = 0x100,
		HasHeading = 0x200,
		IsUnderAttack = 0x400,
		HasSmallHealth = 0x800,
		HasExtendedLevel = 0x1000,
		HasExtendedRunSpeed = 0x2000,
		HasSmallHealthDamage = 0x4000,
		HasWaypoints = 0x10000,
		HasSmallNpcFamily = 0x20000,
		HasSmallNpcLosHeight = 0x80000,
		UnknownFlag2 = 0x200000,
		IsImmune = 0x800000,
		UnknownFlag3 = 0x1000000,
		UnknownDataFlag = 0x2000000,
		HasOrgName = 0x4000000,
		IsPet = 0x8000000,
		UnknownFlag5 = 0x10000000,
		UnknownFlag4 = 0x20000000
	}
	[AoContract(656095851)]
	public class SimpleCharFullUpdateMessage : N3Message
	{
		public class MovementInfo
		{
			[AoMember(0)]
			public float Unk1 { get; set; }

			[AoMember(1)]
			public float Unk2 { get; set; }

			[AoMember(2)]
			public float Unk3 { get; set; }

			[AoMember(3)]
			public MovementState State { get; set; }
		}

		[AoMember(0)]
		public byte Version { get; set; }

		[AoMember(1)]
		public SimpleCharFullUpdateFlags Flags { get; set; }

		[AoMember(2)]
		public int? PlayfieldId { get; set; }

		[AoMember(3)]
		public Identity? FightingTarget { get; set; }

		[AoMember(4)]
		public Vector3 Position { get; set; }

		[AoMember(5)]
		public Quaternion Heading { get; set; }

		[AoMember(6)]
		public Appearance Appearance { get; set; }

		[AoMember(7)]
		public string Name { get; set; }

		[AoMember(8)]
		public CharacterFlags CharacterFlags { get; set; }

		[AoMember(9)]
		public short AccountFlags { get; set; }

		[AoMember(10)]
		public short Expansions { get; set; }

		[AoMember(11)]
		public SimpleCharInfo CharacterInfo { get; set; }

		[AoMember(12)]
		public short Level { get; set; }

		[AoMember(13)]
		public int Health { get; set; }

		[AoMember(14)]
		public int HealthDamage { get; set; }

		[AoMember(15)]
		public uint MonsterData { get; set; }

		[AoMember(16)]
		public short MonsterScale { get; set; }

		[AoMember(17)]
		public short VisualFlags { get; set; }

		[AoMember(18)]
		public byte VisibleTitle { get; set; }

		[AoMember(19, SerializeSize = ArraySizeType.Int32)]
		public byte[] ScfuUnk1 { get; set; }

		[AoMember(20)]
		public int? HeadMesh { get; set; }

		[AoMember(21)]
		public short RunSpeedBase { get; set; }

		[AoMember(22, SerializeSize = ArraySizeType.X3F1)]
		public SimpleCharInfo.ActiveNano[] ActiveNanos { get; set; }

		[AoMember(23, SerializeSize = ArraySizeType.X3F1)]
		public Texture[] Textures { get; set; }

		[AoMember(24, SerializeSize = ArraySizeType.X3F1)]
		public SmokeLounge.AOtomation.Messaging.GameData.Mesh[] Meshes { get; set; }

		[AoMember(25)]
		public ScfuFlags2 Flags2 { get; set; }

		[AoMember(26)]
		public SimpleCharInfo.SpecialAttackData[] SpecialAttacks { get; set; }

		[AoMember(27)]
		public byte ScfuUnk2 { get; set; }

		[AoMember(28)]
		public float ScfuUnk3 { get; set; }

		[AoMember(29)]
		public byte ScfuUnk4 { get; set; }

		[AoMember(30)]
		public SimpleCharInfo.TextureOverride[] TextureOverrides { get; set; }

		[AoMember(31)]
		public List<Vector3> Waypoints { get; set; }

		[AoMember(32)]
		public Identity? Owner { get; set; }

		[AoMember(33)]
		public byte ScfuTowerUnk { get; set; }

		public SimpleCharFullUpdateMessage()
		{
			base.N3MessageType = N3MessageType.SimpleCharFullUpdate;
			base.Unknown = 0;
		}
	}
	[Flags]
	public enum ScfuFlags2
	{
		Unknown1 = 2,
		HasOwner = 4,
		Unknown3 = 0x40,
		Unknown4 = 0x80,
		Unknown5 = 0x100,
		Unknown6 = 0x200,
		Unknown7 = 0x400,
		Unknown8 = 0x800
	}
	public enum SocialAction
	{
		GreetingBow = 9,
		GreetingCurt = 15,
		GreetingGreet = 25,
		GreetingKneel = 27,
		GreetingSalute = 45,
		GreetingScared = 46,
		GreetingSurprised = 57,
		GreetingSurrender = 58,
		GreetingWave = 62,
		GesturesCross = 12,
		GesturesFishsize = 20,
		GesturesGloat = 24,
		GesturesItalian = 26,
		GesturesNod = 33,
		GesturesNono = 34,
		GesturesPray = 40,
		GesturesShrug = 49,
		GesturesSpeech = 51,
		GesturesThinker = 60,
		ApprovalApplause = 4,
		ApprovalBlowkiss = 8,
		ApprovalGiggle = 23,
		ApprovalKisshigh = 66,
		ApprovalKisslow = 65,
		ApprovalLaughB = 28,
		ApprovalLaughS = 29,
		ApprovalProstrate = 1,
		ApprovalSwroyal = 59,
		ApprovalThumbs = 61,
		DislikeAngry = 2,
		DislikeBulge = 10,
		DislikeFblock = 19,
		DislikeFlip = 22,
		DislikeMoon = 32,
		DislikePuke = 41,
		DislikeShake = 48,
		DislikeSlap = 50,
		DislikeSpit = 52,
		DanceBallet = 7,
		DanceChicken = 11,
		DanceDisco = 16,
		DanceFlamenco = 21,
		DancePulp = 42,
		DanceRocky = 44,
		DanceYmca = 63,
		AthleteBackflip = 6,
		AthleteStrong1 = 53,
		AthleteStrong2 = 54,
		AthleteStrong3 = 55,
		AthleteStrong4 = 56,
		RelaxingAdjust = 14,
		RelaxingDrink = 17,
		RelaxingEat = 18,
		RelaxingItch = 5,
		RelaxingLegshake = 30,
		RelaxingLounge = 69,
		RelaxingRead = 43,
		RelaxingScratch = 47,
		DirectionsApachi = 3,
		DirectionsLookout = 31,
		DirectionsPointba = 35,
		DirectionsPointfor = 36,
		DirectionsPointlef = 37,
		DirectionsPointrig = 38,
		DirectionsPointup = 39
	}
	[AoContract(992544625)]
	public class SocialActionCmdMessage : N3Message
	{
		[AoMember(0)]
		public byte Unknown1 { get; set; }

		[AoMember(1)]
		public byte Unknown2 { get; set; }

		[AoMember(2)]
		public byte Unknown3 { get; set; }

		[AoMember(3)]
		public byte Unknown4 { get; set; }

		[AoMember(4)]
		public int Unknown5 { get; set; }

		[AoMember(5)]
		public SocialAction Action { get; set; }

		public SocialActionCmdMessage()
		{
			base.N3MessageType = N3MessageType.SocialActionCmd;
		}
	}
	[AoContract(490475292)]
	public class SpecialAttackWeaponMessage : N3Message
	{
		[AoMember(0, SerializeSize = ArraySizeType.X3F1)]
		public SpecialAttackInfo[] Specials { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		[AoMember(4)]
		public int Unknown4 { get; set; }

		[AoMember(5)]
		public int Unknown5 { get; set; }

		public SpecialAttackWeaponMessage()
		{
			base.N3MessageType = N3MessageType.SpecialAttackWeapon;
			Unknown1 = 7;
			Unknown2 = 7;
			Unknown3 = 7;
			Unknown4 = 14;
			Unknown5 = 100;
		}
	}
	[AoContract(1296367892)]
	public class SpellListMessage : N3Message
	{
		public SpellListMessage()
		{
			base.N3MessageType = N3MessageType.SpellList;
		}
	}
	[AoContract(724778350)]
	public class StatMessage : N3Message
	{
		[AoMember(0, SerializeSize = ArraySizeType.Int32)]
		public GameTuple<Stat, uint>[] Stats { get; set; }

		public StatMessage()
		{
			base.N3MessageType = N3MessageType.Stat;
		}
	}
	[AoContract(678969928)]
	public class TeamMemberInfoMessage : N3Message
	{
		[AoMember(0)]
		public Identity Character { get; set; }

		[AoMember(1)]
		public int Unknown2 { get; set; }

		[AoMember(2)]
		public int Unknown4 { get; set; }

		[AoMember(3)]
		public int Unknown6 { get; set; }

		[AoMember(4)]
		public int Unknown8 { get; set; }

		public TeamMemberInfoMessage()
		{
			base.N3MessageType = N3MessageType.TeamMemberInfo;
		}
	}
	[AoContract(1063144262)]
	public class ToggleCloakMessage : N3Message
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		public ToggleCloakMessage()
		{
			base.N3MessageType = N3MessageType.ToggleCloak;
		}
	}
	[AoContract(1177627950)]
	public class TeamMemberMessage : N3Message
	{
		[AoMember(0)]
		public Identity Character { get; set; }

		[AoMember(1)]
		public Identity Team { get; set; }

		[AoMember(2)]
		public uint Unknown1 { get; set; }

		[AoMember(3)]
		public int Unknown2 { get; set; }

		[AoMember(4)]
		public int Unknown3 { get; set; }

		[AoMember(5, SerializeSize = ArraySizeType.Int16)]
		public string Name { get; set; }

		public TeamMemberMessage()
		{
			base.N3MessageType = N3MessageType.TeamMember;
		}
	}
	[AoContract(894457412)]
	public class TemplateActionMessage : N3Message
	{
		[AoMember(0)]
		public int ItemLowId { get; set; }

		[AoMember(1)]
		public int ItemHighId { get; set; }

		[AoMember(2)]
		public int Quality { get; set; }

		[AoMember(3)]
		public int Unknown1 { get; set; }

		[AoMember(4)]
		public int Unknown2 { get; set; }

		[AoMember(5)]
		public Identity Placement { get; set; }

		[AoMember(6)]
		public int Unknown3 { get; set; }

		[AoMember(7)]
		public int Unknown4 { get; set; }

		public TemplateActionMessage()
		{
			base.N3MessageType = N3MessageType.TemplateAction;
		}
	}
	public enum QuestAction
	{
		Delete = 1
	}
	[AoContract(556550266)]
	public class QuestMessage : N3Message
	{
		[AoMember(0)]
		public QuestAction Action { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public Identity Mission { get; set; }

		[AoMember(3)]
		public int Unknown2 { get; set; }

		[AoMember(4)]
		public int Unknown3 { get; set; }

		public QuestMessage()
		{
			base.N3MessageType = N3MessageType.Quest;
		}
	}
	[AoContract(908611438)]
	public class TradeMessage : N3Message
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public TradeAction Action { get; set; }

		[AoMember(2)]
		public int Param1 { get; set; }

		[AoMember(3)]
		public int Param2 { get; set; }

		[AoMember(4)]
		public int Param3 { get; set; }

		[AoMember(5)]
		public int Param4 { get; set; }

		public TradeMessage()
		{
			base.N3MessageType = N3MessageType.Trade;
		}
	}
	[AoContract(2136230149)]
	public class VendingMachineFullUpdateMessage : N3Message
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoFlags("OwnerType")]
		[AoMember(1)]
		public int OwnerType { get; set; }

		[AoMember(2)]
		public int OwnerInstance { get; set; }

		[AoUsesFlags("OwnerType", typeof(Vector3), FlagsCriteria.EqualsToAny, new int[] { 0 })]
		[AoMember(3)]
		public Vector3? Position { get; set; }

		[AoUsesFlags("OwnerType", typeof(Quaternion), FlagsCriteria.EqualsToAny, new int[] { 0 })]
		[AoMember(4)]
		public Quaternion? Rotation { get; set; }

		[AoMember(5)]
		public int PlayfieldId { get; set; }

		[AoMember(6)]
		public Identity StateMachine { get; set; }

		[AoMember(7)]
		public short Unknown4 { get; set; }

		[AoMember(8, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<Stat, int>[] Stats { get; set; }

		[AoMember(9)]
		public int Unknown6 { get; set; }

		[AoMember(10)]
		public int Unknown7 { get; set; }

		[AoMember(11)]
		public int Unknown8 { get; set; }

		[AoMember(12, SerializeSize = ArraySizeType.X3F1)]
		public int[] UnknownArray { get; set; }

		[AoMember(13)]
		public int Unknown9 { get; set; }

		public VendingMachineFullUpdateMessage()
		{
			base.N3MessageType = N3MessageType.VendingMachineFullUpdate;
		}
	}
	[AoContract(991765096)]
	public class WeaponItemFullUpdateMessage : N3Message
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public Identity Owner { get; set; }

		[AoMember(2)]
		public int PlayfieldId { get; set; }

		[AoMember(3)]
		public Identity StateMachine { get; set; }

		[AoMember(4)]
		public short Unknown2 { get; set; }

		[AoMember(5, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<Stat, int>[] Stats { get; set; }

		[AoMember(6)]
		public int Unknown3 { get; set; }

		public WeaponItemFullUpdateMessage()
		{
			base.N3MessageType = N3MessageType.WeaponItemFullUpdate;
		}
	}
	public enum OrgClientCommand : byte
	{
		None = 0,
		Create = 1,
		Ranks = 2,
		Contract = 3,
		Unknown1 = 4,
		Info = 5,
		Disband = 6,
		StartVote = 7,
		VoteInfo = 8,
		Vote = 9,
		Promote = 10,
		Demote = 11,
		Unknown2 = 12,
		Kick = 13,
		Invite = 14,
		Join = 15,
		Leave = 16,
		Tax = 17,
		Bank = 18,
		BankAdd = 19,
		BankRemove = 20,
		BankPaymembers = 21,
		Debt = 22,
		History = 23,
		Objective = 24,
		Description = 25,
		Name = 26,
		GoverningForm = 27,
		StopVote = 28,
		Benefits = 31
	}
	[AoContract(1683499527)]
	public class OrgServerMessage : N3Message
	{
		[AoFlags("orgmessagetype")]
		[AoMember(0)]
		public OrgServerMessageType OrgServerMessageType { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public Identity Organization { get; set; }

		[AoUsesFlags("orgmessagetype", typeof(OrgInvite), FlagsCriteria.EqualsToAny, new int[] { 5 })]
		[AoUsesFlags("orgmessagetype", typeof(OrganizationInfo), FlagsCriteria.EqualsToAny, new int[] { 2 })]
		[AoUsesFlags("orgmessagetype", typeof(ContractsInfo), FlagsCriteria.EqualsToAny, new int[] { 1 })]
		[AoMember(4)]
		public IOrgServerMessage IOrgServerMessage { get; set; }

		protected OrgServerMessage()
		{
			base.N3MessageType = N3MessageType.OrgServer;
		}
	}
	public class OrgInvite : IOrgServerMessage
	{
		[AoMember(0)]
		public int Unknown3 { get; set; }
	}
	public class ContractsInfo : IOrgServerMessage
	{
		[AoMember(0, SerializeSize = ArraySizeType.X3F1)]
		public OrgContractSlot[] Contracts { get; set; }
	}
	public class OrganizationInfo : IOrgServerMessage
	{
		[AoMember(0, SerializeSize = ArraySizeType.Int16)]
		public string OrganizationName { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public string Description { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.Int16)]
		public string Objective { get; set; }

		[AoMember(3, SerializeSize = ArraySizeType.Int16)]
		public string History { get; set; }

		[AoMember(4, SerializeSize = ArraySizeType.Int16)]
		public string GoverningForm { get; set; }

		[AoMember(5, SerializeSize = ArraySizeType.Int16)]
		public string LeaderName { get; set; }

		[AoMember(6, SerializeSize = ArraySizeType.Int16)]
		public string Rank { get; set; }

		[AoMember(7, SerializeSize = ArraySizeType.X3F1)]
		public object[] Unknown3 { get; set; }
	}
	public interface IOrgServerMessage
	{
	}
	public enum OrgServerMessageType : byte
	{
		OrgContract = 1,
		OrgInfo = 2,
		OrgInvite = 5
	}
	public enum TeamRequestResponseAction
	{
		Decline,
		Accept
	}
}
namespace SmokeLounge.AOtomation.Messaging.Messages.ChatMessages
{
	[AoContract(5)]
	public class ChatLoginOKMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.LoginOK;
	}
	[AoContract(6)]
	public class ChatLoginErrorMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.LoginError;
	}
	[AoContract(7)]
	public class ChatCharacterListMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.CharacterList;

		[AoMember(0, SerializeSize = ArraySizeType.Int16)]
		public uint[] Ids { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public string[] Names { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.Int16)]
		public int[] Levels { get; set; }

		[AoMember(3, SerializeSize = ArraySizeType.Int16)]
		public bool[] Online { get; set; }

		public ChatCharacter[] Characters => ToCharacters();

		private ChatCharacter[] ToCharacters()
		{
			ChatCharacter[] array = new ChatCharacter[Names.Length];
			for (int i = 0; i < Names.Length; i++)
			{
				array[i] = new ChatCharacter
				{
					Name = Names[i],
					Id = Ids[i],
					Level = Levels[i],
					Online = Online[i]
				};
			}
			return array;
		}
	}
	public class ChatCharacter
	{
		public string Name;

		public uint Id;

		public int Level;

		public bool Online;
	}
	[AoContract(20)]
	public class CharacterNameMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.CharacterName;

		[AoMember(0)]
		public uint Id { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public string Name { get; set; }
	}
	[AoContract(100)]
	public class ChatPingMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.Ping;

		[AoMember(0)]
		public byte[] Data { get; set; }

		public ChatPingMessage()
		{
			Data = new byte[3] { 0, 1, 2 };
		}
	}
	[AoContract(3)]
	public class ChatSelectCharacterMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.SelectCharacter;

		[AoMember(0)]
		public uint CharacterId { get; set; }
	}
	[AoContract(2)]
	public class ChatLoginRequestMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.LoginRequest;

		[AoMember(0)]
		public int Unk { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public string Username { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.Int16)]
		public string Credentials { get; set; }
	}
	[AoContract(0)]
	public class ChatServerSaltMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.ServerSalt;

		[AoMember(0, SerializeSize = ArraySizeType.Int16)]
		public byte[] ServerSalt { get; set; }
	}
	[AoContract(35)]
	public class NpcMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.NpcMessage;

		[AoMember(0)]
		public short Unk1 { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public string Text { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.Int16)]
		public short Unk2 { get; set; }
	}
	[AoContract(65)]
	public class GroupMsgMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.GroupMessage;

		[AoMember(0)]
		public GroupMessageType MessageType { get; set; }

		[AoMember(1)]
		public int ChannelId { get; set; }

		[AoMember(3)]
		public uint SenderId { get; set; }

		[AoMember(4, SerializeSize = ArraySizeType.Int16)]
		public string Text { get; set; }
	}
	[AoContract(60)]
	public class ChannelListMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.ChannelList;

		[AoMember(0)]
		public byte Unk1 { get; set; }

		[AoMember(1)]
		public int ChannelId { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.Int16)]
		public string ChannelName { get; set; }

		[AoMember(3)]
		public short Unk2 { get; set; }

		[AoMember(4)]
		public short Unk3 { get; set; }

		[AoMember(5)]
		public short Unk4 { get; set; }
	}
	[AoContract(52)]
	public class PrivateGroupInviteAcceptMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.PrivateGroupInviteAccept;

		[AoMember(0)]
		public uint Sender { get; set; }
	}
	[AoContract(50)]
	public class PrivateGroupInviteMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.PrivateGroupInvite;

		[AoMember(0)]
		public uint Sender { get; set; }
	}
	[AoContract(57)]
	public class PrivateGroupMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.PrivateGroupMessage;

		[AoMember(0)]
		public uint ChannelId { get; set; }

		[AoMember(0)]
		public uint Sender { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public string Text { get; set; }

		[AoMember(2)]
		public byte Unk1 { get; set; }

		[AoMember(3)]
		public byte Unk2 { get; set; }
	}
	[AoContract(34)]
	public class VicinityMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.VicinityMessage;

		[AoMember(0)]
		public uint Sender { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public string Text { get; set; }

		[AoMember(2)]
		public short Unk1 { get; set; }

		[AoMember(3)]
		public byte Unk2 { get; set; }
	}
	[AoContract(30)]
	public class PrivateMsgMessage : ChatMessageBody
	{
		public override ChatMessageType PacketType => ChatMessageType.PrivateMessage;

		[AoMember(0)]
		public uint Sender { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public string Text { get; set; }

		[AoMember(2)]
		public short Unk1 { get; set; }

		[AoMember(3)]
		public byte Unk2 { get; set; }
	}
}
namespace SmokeLounge.AOtomation.Messaging.Exceptions
{
	public class ContractIdCollisionException : Exception
	{
		public ContractIdCollisionException(string message)
			: base(message)
		{
		}
	}
}
namespace SmokeLounge.AOtomation.Messaging.GameData
{
	public class AdvantagesItem
	{
		[AoMember(0)]
		public int LowId { get; set; }

		[AoMember(1)]
		public int HighId { get; set; }

		[AoMember(2)]
		public int Ql { get; set; }

		[AoMember(3)]
		public int Status { get; set; }
	}
	public class AnimationEffect
	{
		[AoMember(0)]
		public int IdentityType { get; set; }

		[AoMember(1)]
		public int NanoId { get; set; }

		[AoMember(2)]
		public int NanoInstance { get; set; }

		[AoMember(3)]
		public int Time1 { get; set; }

		[AoMember(4)]
		public int Time2 { get; set; }

		[AoMember(5)]
		public int Unknown1 { get; set; }

		[AoMember(6)]
		public int Unknown2 { get; set; }

		[AoMember(7)]
		public int Unknown3 { get; set; }

		[AoMember(8)]
		public int Unknown4 { get; set; }

		[AoMember(9)]
		public int Unknown5 { get; set; }

		[AoMember(10)]
		public int Unknown6 { get; set; }

		[AoMember(11)]
		public int Unknown7 { get; set; }

		[AoMember(12)]
		public int Unknown8 { get; set; }

		[AoMember(13)]
		public int VisualDataId { get; set; }

		[AoMember(14)]
		public int Unknown9 { get; set; }
	}
	public enum AOSignalAction
	{
		CityInfo = 5,
		Close = 7,
		CreditsUpkeepInfo = 10,
		UpkeepInfo = 13,
		CloakInfo = 14,
		ChargeInfo = 15
	}
	public class FightModeName
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public string Text { get; set; }

		[AoMember(2)]
		public byte Unknown2 { get; set; }

		[AoMember(3)]
		public byte Unknown3 { get; set; }
	}
	public class InspectSlotInfo
	{
		public int Unk { get; set; }

		public EquipSlot EquipSlot { get; set; }

		public int Unk2 { get; set; }

		public Identity UniqueIdentity { get; set; }

		public int LowId { get; set; }

		public int HighId { get; set; }

		public int Ql { get; set; }
	}
	public class OrgContractSlot
	{
		[AoMember(0)]
		public int Slot { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		[AoMember(4)]
		public int LowId { get; set; }

		[AoMember(5)]
		public int HighId { get; set; }

		[AoMember(6)]
		public int Ql { get; set; }

		[AoMember(7)]
		public int Unknown4 { get; set; }
	}
	public class ResearchLine
	{
		[AoMember(0)]
		public int ResearchId { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }
	}
	public class CharacterInfo
	{
		[AoMember(0)]
		public Identity MissionIdentity { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.NullTerminated)]
		public string Name { get; set; }
	}
	public class QuestIdentity
	{
		[AoMember(0)]
		public Identity Identity { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }
	}
	public class Quest
	{
		[AoMember(0)]
		public Identity QuestId { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		[AoMember(4)]
		public int Unknown4 { get; set; }

		[AoMember(5, SerializeSize = ArraySizeType.NullTerminated)]
		public string ShortInfo { get; set; }

		[AoMember(6, SerializeSize = ArraySizeType.Int32)]
		public string LongInfo { get; set; }

		[AoMember(7)]
		public Identity UnknownId1 { get; set; }

		[AoMember(8)]
		public int Unknown5 { get; set; }

		[AoMember(9)]
		public int Unknown6 { get; set; }

		[AoMember(10)]
		public int Unknown7 { get; set; }

		[AoMember(11)]
		public int Unknown8 { get; set; }

		[AoMember(12)]
		public int Unknown9 { get; set; }

		[AoMember(13)]
		public int Unknown10 { get; set; }

		[AoMember(14, SerializeSize = ArraySizeType.X3F1)]
		public MissionItemReward[] MissionItemData { get; set; }

		[AoMember(15)]
		public int Unknown11 { get; set; }

		[AoMember(16)]
		public int Unknown12 { get; set; }

		[AoMember(17)]
		public int Unknown13 { get; set; }

		[AoMember(18, SerializeSize = ArraySizeType.NoSerialization, FixedSizeLength = 4, IsFixedSize = true)]
		public string UnknownHash1 { get; set; }

		[AoMember(15)]
		public int Unknown14 { get; set; }

		[AoMember(16)]
		public int Unknown15 { get; set; }

		[AoMember(17)]
		public int Unknown16 { get; set; }

		[AoMember(18)]
		public int Unknown17 { get; set; }

		[AoMember(19)]
		public int Unknown18 { get; set; }

		[AoMember(20)]
		public Identity UnknownId2 { get; set; }

		[AoMember(21)]
		public int MissionIconId { get; set; }

		[AoMember(22)]
		public int Unknown20 { get; set; }

		[AoMember(23)]
		public int Unknown21 { get; set; }

		[AoMember(24, SerializeSize = ArraySizeType.X3F1)]
		public QuestActionInfo[] QuestActions { get; set; }

		[AoMember(25, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] PlayerIds { get; set; }

		[AoMember(26, SerializeSize = ArraySizeType.Int32)]
		public int[] UnknownArray1 { get; set; }

		[AoMember(27, SerializeSize = ArraySizeType.Int32)]
		public int[] UnknownArray2 { get; set; }

		[AoMember(28, SerializeSize = ArraySizeType.Int32)]
		public CharacterInfo[] CharacterInfos { get; set; }

		[AoMember(29)]
		public int Unknown22 { get; set; }

		[AoMember(30, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] PlayerIds2 { get; set; }

		[AoMember(31)]
		public int Unknown23 { get; set; }

		[AoMember(32)]
		public int Unknown24 { get; set; }

		[AoMember(33)]
		public Identity UnknownId3 { get; set; }

		[AoMember(34)]
		public int Unknown25 { get; set; }

		[AoMember(35)]
		public int Unknown26 { get; set; }

		[AoMember(36, SerializeSize = ArraySizeType.Int32)]
		public QuestIdentity[] QuestIdentities { get; set; }

		[AoMember(37)]
		public int Unknown27 { get; set; }

		[AoMember(38, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] FactionInfos { get; set; }

		[AoMember(39)]
		public byte Unknown28 { get; set; }
	}
	public class QuestActionInfo
	{
		[AoMember(0)]
		public int Version { get; set; }

		[AoMember(1)]
		public Identity Action { get; set; }

		[AoMember(2)]
		public Identity UnknownId1 { get; set; }

		[AoMember(3)]
		public Identity UnknownId2 { get; set; }

		[AoMember(4)]
		public Identity UnknownId3 { get; set; }

		[AoMember(5)]
		public Identity UnknownId4 { get; set; }

		[AoMember(6)]
		public float Unknown1 { get; set; }

		[AoMember(7)]
		public float Unknown2 { get; set; }

		[AoMember(8)]
		public float Unknown3 { get; set; }

		[AoMember(9)]
		public float Unknown4 { get; set; }

		[AoMember(10)]
		public Identity UnknownId5 { get; set; }

		[AoMember(11)]
		public float Unknown5 { get; set; }

		[AoMember(12)]
		public float Unknown6 { get; set; }

		[AoMember(13)]
		public float Unknown7 { get; set; }

		[AoMember(14)]
		public float Unknown8 { get; set; }

		[AoMember(15)]
		public Identity UnknownId6 { get; set; }

		[AoMember(16, SerializeSize = ArraySizeType.NoSerialization, FixedSizeLength = 4, IsFixedSize = true)]
		public string UnknownHash1 { get; set; }

		[AoMember(17)]
		public int Unknown9 { get; set; }

		[AoMember(18)]
		public Identity UnknownId7 { get; set; }

		[AoMember(19)]
		public Identity PlayfieldId { get; set; }

		[AoMember(20)]
		public int Unknown10 { get; set; }

		[AoMember(21)]
		public int Unknown11 { get; set; }

		[AoMember(22)]
		public Vector3 Position { get; set; }
	}
	public class MissionInfo
	{
		[AoMember(0)]
		public Identity MissionIdentity { get; set; }

		[AoMember(1, FixedSizeLength = 16, IsFixedSize = true)]
		public byte[] UnkChunk1 { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.NullTerminated)]
		public string Title { get; set; }

		[AoMember(3, SerializeSize = ArraySizeType.Int32)]
		public string Description { get; set; }

		[AoMember(4)]
		public Identity TerminalIdentity { get; set; }

		[AoMember(5)]
		public int RewardDescriptorVersion { get; set; }

		[AoMember(6)]
		public int Credits { get; set; }

		[AoMember(7)]
		public int Unk1 { get; set; }

		[AoMember(8)]
		public int XpReward { get; set; }

		[AoMember(9, FixedSizeLength = 8, IsFixedSize = true)]
		public byte[] UnkChunk2 { get; set; }

		[AoMember(10, SerializeSize = ArraySizeType.X3F1)]
		public MissionItemReward[] MissionItemData { get; set; }

		[AoMember(11, FixedSizeLength = 44, IsFixedSize = true)]
		public byte[] UnkChunk3 { get; set; }

		[AoMember(12)]
		public int MissionIcon { get; set; }

		[AoMember(13, FixedSizeLength = 120, IsFixedSize = true)]
		public byte[] UnkChunk4 { get; set; }

		[AoMember(14)]
		public Identity Playfield { get; set; }

		[AoMember(15, FixedSizeLength = 8, IsFixedSize = true)]
		public byte[] UnkChunk5 { get; set; }

		[AoMember(16)]
		public Vector3 Location { get; set; }

		[AoMember(17, FixedSizeLength = 61, IsFixedSize = true)]
		public byte[] UnkChunk6 { get; set; }
	}
	public class MissionItemReward
	{
		[AoMember(0)]
		public int LowId { get; set; }

		[AoMember(1)]
		public int HighId { get; set; }

		[AoMember(2)]
		public int Ql { get; set; }

		[AoMember(3)]
		public int Unk { get; set; }
	}
	public class MissionSliders
	{
		[AoMember(0)]
		public byte Difficulty { get; set; }

		[AoMember(1)]
		public byte GoodBad { get; set; }

		[AoMember(2)]
		public byte OrderChaos { get; set; }

		[AoMember(3)]
		public byte OpenHidden { get; set; }

		[AoMember(4)]
		public byte PhysicalMystical { get; set; }

		[AoMember(5)]
		public byte HeadonStealth { get; set; }

		[AoMember(6)]
		public byte CreditsXp { get; set; }
	}
	public enum TradeAction : byte
	{
		Open,
		Accept,
		Decline,
		Confirm,
		Complete,
		AddItem,
		RemoveItem,
		UpdateCredits,
		OtherPlayerAddItem
	}
	public class Appearance
	{
		private Breed breed;

		private Fatness fatness;

		private Gender gender;

		private uint race;

		private Side side;

		private uint value;

		[AoMember(0)]
		public uint Value
		{
			get
			{
				return value;
			}
			set
			{
				this.value = value;
				UpdateStats();
			}
		}

		public Breed Breed
		{
			get
			{
				return breed;
			}
			set
			{
				breed = value;
				UpdateValue();
			}
		}

		public Fatness Fatness
		{
			get
			{
				return fatness;
			}
			set
			{
				fatness = value;
				UpdateValue();
			}
		}

		public Gender Gender
		{
			get
			{
				return gender;
			}
			set
			{
				gender = value;
				UpdateValue();
			}
		}

		public uint Race
		{
			get
			{
				return race;
			}
			set
			{
				race = value;
				UpdateValue();
			}
		}

		public Side Side
		{
			get
			{
				return side;
			}
			set
			{
				side = value;
				UpdateValue();
			}
		}

		private void UpdateStats()
		{
			uint num = value & 7u;
			side = (Side)num;
			uint num2 = (value & 0x1F) >> 3;
			fatness = (Fatness)num2;
			uint num3 = (value & 0xFF) >> 5;
			breed = (Breed)num3;
			uint num4 = (value & 0x3FF) >> 8;
			gender = (Gender)num4;
			uint num5 = value >> 10;
			race = num5;
		}

		private void UpdateValue()
		{
			uint num = (uint)side;
			uint num2 = (uint)((int)fatness << 3);
			uint num3 = (uint)((int)breed << 5);
			uint num4 = (uint)((int)gender << 8);
			uint num5 = race << 10;
			value = num + num2 + num3 + num4 + num5;
		}
	}
	public class BankSlot
	{
		[AoMember(0)]
		public int Placement { get; set; }

		[AoMember(1)]
		public short Flags { get; set; }

		[AoMember(2)]
		public short Count { get; set; }

		[AoMember(3)]
		public Identity Identity { get; set; }

		[AoMember(4)]
		public int ItemLowId { get; set; }

		[AoMember(5)]
		public int ItemHighId { get; set; }

		[AoMember(6)]
		public int Quality { get; set; }

		[AoMember(7)]
		public int Unknown { get; set; }
	}
	public class TowerInfo
	{
		[AoMember(0)]
		public Identity PlaceholderId { get; set; }

		[AoMember(1)]
		public Identity TowerCharId { get; set; }

		[AoMember(2)]
		public Vector3 Position { get; set; }

		[AoMember(3)]
		public int MeshId { get; set; }

		[AoMember(4)]
		public Side Side { get; set; }

		[AoMember(5)]
		public int DestroyedMeshId { get; set; }

		[AoMember(6)]
		public float Scale { get; set; }

		[AoMember(7)]
		public TowerClass Class { get; set; }
	}
	public enum CharacterActionType
	{
		TeamRequest = 26,
		CastNano = 19,
		TeamRequestReply = 28,
		TeamKickMember = 22,
		LeaveTeam = 24,
		TeamMemberLeft = 32,
		AcceptTeamRequest = 35,
		RemoveFriendlyNano = 65,
		UseItemOnItem = 81,
		StandUp = 87,
		Unknown3 = 97,
		SetNanoDuration = 98,
		Death = 99,
		InfoRequest = 105,
		FinishNanoCasting = 107,
		InterruptNanoCasting = 108,
		DeleteItem = 112,
		Logout = 120,
		StopLogout = 122,
		Equip = 131,
		SpecialUnavailable = 132,
		Die = 152,
		StartedSneaking = 162,
		StartSneak = 163,
		SpecialAvailable = 164,
		SpecialUsed = 170,
		Search = 102,
		DisableXP = 165,
		ChangeVisualFlag = 166,
		ChangeAnimationAndStance = 167,
		ShipInvite = 186,
		TrainPerk = 187,
		UploadNano = 204,
		TradeskillSourceChanged = 220,
		TradeskillTargetChanged = 221,
		TradeskillBuildPressed = 222,
		TradeskillSource = 223,
		TradeskillTarget = 224,
		TradeskillNotValid = 225,
		TradeskillOutOfRange = 226,
		TradeskillRequirement = 227,
		TradeskillResult = 228,
		TransferLeader = 25,
		TeamRequestInvite = 26,
		Split = 34,
		DuelUpdate = 262,
		TeamRequestResponse = 35,
		SplitItem = 52,
		QueuePerk = 80,
		UsePerk = 179,
		PerkAvailable = 206,
		PerkUnavailable = 207,
		JoinBattlestationQueue = 253,
		LeaveBattlestationQueue = 255,
		Inspect = 261
	}
	public class CharacterInfoPacket : InfoPacket
	{
		[AoMember(0)]
		public byte Unknown1 { get; set; }

		[AoMember(1)]
		[AoUsesFlags("flags", typeof(byte), FlagsCriteria.Default, new int[] { })]
		public Profession Profession { get; set; }

		[AoMember(2)]
		public byte Level { get; set; }

		[AoMember(3)]
		public byte TitleLevel { get; set; }

		[AoMember(4)]
		[AoUsesFlags("flags", typeof(byte), FlagsCriteria.Default, new int[] { })]
		public Profession VisualProfession { get; set; }

		[AoMember(5)]
		public short SideXp { get; set; }

		[AoMember(6)]
		public int Health { get; set; }

		[AoMember(7)]
		public int MaxHealth { get; set; }

		[AoMember(8)]
		public int BreedHostility { get; set; }

		[AoMember(9)]
		[AoUsesFlags("flags", typeof(int), FlagsCriteria.EqualsToAny, new int[] { 65, 67, 71 })]
		public int? OrganizationId { get; set; }

		[AoMember(10, SerializeSize = ArraySizeType.Int16)]
		public string FirstName { get; set; }

		[AoMember(11, SerializeSize = ArraySizeType.Int16)]
		public string LastName { get; set; }

		[AoMember(12, SerializeSize = ArraySizeType.Int16)]
		public string LegacyTitle { get; set; }

		[AoMember(13)]
		public short Unknown2 { get; set; }

		[AoMember(14, SerializeSize = ArraySizeType.Int16)]
		[AoUsesFlags("flags", typeof(string), FlagsCriteria.EqualsToAny, new int[] { 65, 67, 71 })]
		public string OrganizationRank { get; set; }

		[AoMember(15, SerializeSize = ArraySizeType.X3F1)]
		[AoUsesFlags("flags", typeof(TowerField[]), FlagsCriteria.EqualsToAny, new int[] { 67, 71 })]
		public TowerField[] TowerFields { get; set; }

		[AoMember(16)]
		public int CityPlayfieldId { get; set; }

		[AoMember(17, SerializeSize = ArraySizeType.X3F1)]
		[AoUsesFlags("flags", typeof(Tower[]), FlagsCriteria.EqualsToAny, new int[] { 71 })]
		public Tower[] Towers { get; set; }

		[AoMember(18)]
		public int InvadersKilled { get; set; }

		[AoMember(19)]
		public int KilledByInvaders { get; set; }

		[AoMember(20)]
		public int AiLevel { get; set; }

		[AoMember(21)]
		public int PvpDuelWins { get; set; }

		[AoMember(22)]
		public int PvpDuelLoses { get; set; }

		[AoMember(23)]
		public int PvpProfessionDuelLoses { get; set; }

		[AoMember(24)]
		public int PvpSoloKills { get; set; }

		[AoMember(25)]
		public int PvpTeamKills { get; set; }

		[AoMember(26)]
		public int PvpSoloScore { get; set; }

		[AoMember(27)]
		public int PvpTeamScore { get; set; }

		[AoMember(28)]
		public int PvpDuelScore { get; set; }
	}
	public class GameTuple<T1, T2>
	{
		[AoMember(0)]
		public T1 Value1 { get; set; }

		[AoMember(1)]
		public T2 Value2 { get; set; }
	}
	public abstract class InfoPacket
	{
	}
	public class InventorySlot
	{
		[AoMember(0)]
		public int Placement { get; set; }

		[AoMember(1)]
		public short Flags { get; set; }

		[AoMember(2)]
		public short Count { get; set; }

		[AoMember(3)]
		public Identity Identity { get; set; }

		[AoMember(4)]
		public int ItemLowId { get; set; }

		[AoMember(5)]
		public int ItemHighId { get; set; }

		[AoMember(6)]
		public int Quality { get; set; }

		[AoMember(7)]
		public int Unknown { get; set; }
	}
	public class KnuBotDialogOption
	{
		[AoMember(0, SerializeSize = ArraySizeType.Int32)]
		public string Text { get; set; }
	}
	public class KnuBotRejectedItem
	{
		[AoMember(0)]
		public int LowId { get; set; }

		[AoMember(1)]
		public int HighId { get; set; }

		[AoMember(2)]
		public int Quality { get; set; }

		[AoMember(3)]
		public int Unknown { get; set; }
	}
	public class LoginCharacterInfo
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public int Id { get; set; }

		[AoMember(2)]
		public byte PlayfieldProxyVersion { get; set; }

		[AoMember(3)]
		public Identity PlayfieldId { get; set; }

		[AoMember(4)]
		public int PlayfieldAttribute { get; set; }

		[AoMember(5)]
		public int ExitDoor { get; set; }

		[AoMember(6)]
		public Identity ExitDoorId { get; set; }

		[AoMember(7)]
		public int Unknown2 { get; set; }

		[AoMember(8)]
		public int CharacterInfoVersion { get; set; }

		[AoMember(9)]
		public int CharacterId { get; set; }

		[AoMember(10)]
		public int Unknown3 { get; set; }

		[AoMember(11, SerializeSize = ArraySizeType.Int32)]
		public string Name { get; set; }

		[AoMember(12)]
		public Breed Breed { get; set; }

		[AoMember(13)]
		public Gender Gender { get; set; }

		[AoMember(14)]
		public Profession Profession { get; set; }

		[AoMember(15)]
		public int Level { get; set; }

		[AoMember(16, SerializeSize = ArraySizeType.Int32)]
		public string AreaName { get; set; }

		[AoMember(17)]
		public int Unknown4 { get; set; }

		[AoMember(18)]
		public int Unknown5 { get; set; }

		[AoMember(19)]
		public int Unknown6 { get; set; }

		[AoMember(20)]
		public int Unknown7 { get; set; }

		[AoMember(21)]
		public int Unknown8 { get; set; }

		[AoMember(22)]
		public CharacterStatus Status { get; set; }

		public LoginCharacterInfo()
		{
			Unknown1 = 4;
			Unknown2 = 1;
		}
	}
	public class Mesh
	{
		[AoMember(0)]
		public byte Position { get; set; }

		[AoMember(2)]
		public int OverrideTextureId { get; set; }

		[AoMember(1)]
		public uint Id { get; set; }

		[AoMember(3)]
		public byte Layer { get; set; }
	}
	public class MonsterInfoPacket : InfoPacket
	{
	}
	public class NanoEffect
	{
		[AoMember(0)]
		public Identity Effect { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int CriterionCount { get; set; }

		[AoMember(3)]
		public int Hits { get; set; }

		[AoMember(4)]
		public int Delay { get; set; }

		[AoMember(5)]
		public int Unknown2 { get; set; }

		[AoMember(6)]
		public int Unknown3 { get; set; }

		[AoMember(7)]
		public int GfxValue { get; set; }

		[AoMember(8)]
		public int GfxLife { get; set; }

		[AoMember(9)]
		public int GfxSize { get; set; }

		[AoMember(10)]
		public int GfxRed { get; set; }

		[AoMember(11)]
		public int GfxGreen { get; set; }

		[AoMember(12)]
		public int GfxBlue { get; set; }

		[AoMember(13)]
		public int GfxFade { get; set; }
	}
	public class PlayfieldVendorInfo
	{
		[AoMember(0)]
		public Identity Unknown1 { get; set; }

		[AoMember(1)]
		public int Unknown2 { get; set; }

		[AoMember(2)]
		public int FirstVendorId { get; set; }

		[AoMember(3)]
		public int VendorCount { get; set; }

		public PlayfieldVendorInfo()
		{
			Unknown1 = new Identity
			{
				Type = IdentityType.VendingMachine,
				Instance = 1
			};
			Unknown2 = 1;
		}
	}
	public class SimpleCharInfo
	{
		public class NPCInfo : SimpleCharInfo
		{
			public short Family { get; set; }

			public short LosHeight { get; set; }
		}

		public class PlayerInfo : SimpleCharInfo
		{
			public uint CurrentNano { get; set; }

			public int Team { get; set; }

			public short Swim { get; set; }

			public short StrengthBase { get; set; }

			public short AgilityBase { get; set; }

			public short StaminaBase { get; set; }

			public short IntelligenceBase { get; set; }

			public short SenseBase { get; set; }

			public short PsychicBase { get; set; }

			public string FirstName { get; set; }

			public string LastName { get; set; }

			public string OrgName { get; set; }

			public int OrgId { get; set; }
		}

		public class SpecialAttackData
		{
			public short Unknown1 { get; set; }

			public short Unknown2 { get; set; }

			public short Unknown3 { get; set; }

			public short Unknown4 { get; set; }

			public short Unknown5 { get; set; }

			public string Name { get; set; }

			public short Unknown6 { get; set; }
		}

		public class TextureOverride
		{
			public int TextureId;

			public int Unknown1;

			public int Unknown2;

			public string Name { get; set; }
		}

		public class ActiveNano
		{
			public Identity Identity { get; set; }

			public int NanoInstance { get; set; }

			public int Time1 { get; set; }

			public int Time2 { get; set; }
		}
	}
	public class SpecialAttackInfo
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public int Unknown2 { get; set; }

		[AoMember(2)]
		public int Unknown3 { get; set; }

		[AoMember(3, IsFixedSize = true, FixedSizeLength = 4)]
		public string Unknown4 { get; set; }
	}
	public class Texture
	{
		[AoMember(0)]
		public int Place { get; set; }

		[AoMember(1)]
		public int Id { get; set; }

		[AoMember(2)]
		public int Unknown { get; set; }
	}
	public class Tower
	{
		[AoMember(0)]
		public int LowId { get; set; }

		[AoMember(1)]
		public int HighId { get; set; }

		[AoMember(2)]
		public int Quality { get; set; }

		[AoMember(3)]
		public int Unknown { get; set; }
	}
	public class TowerField
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public Identity Identity { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.Int16)]
		public string Name { get; set; }

		[AoMember(3)]
		public int Unknown2 { get; set; }

		[AoMember(4)]
		public int Unknown3 { get; set; }
	}
	public class TowerInfoPacket : InfoPacket
	{
		[AoMember(0)]
		public byte Unknown1 { get; set; }

		[AoMember(1)]
		public byte Unknown2 { get; set; }

		[AoMember(2)]
		public byte Unknown3 { get; set; }

		[AoMember(3)]
		public byte Unknown4 { get; set; }

		[AoMember(4)]
		public byte Unknown5 { get; set; }

		[AoMember(5)]
		public byte Unknown6 { get; set; }

		[AoMember(6)]
		public byte Unknown7 { get; set; }

		[AoMember(7)]
		public int Health { get; set; }

		[AoMember(8)]
		public int MaxHealth { get; set; }

		[AoMember(9)]
		public int Unknown8 { get; set; }

		[AoMember(10)]
		public int OrganizationId { get; set; }

		[AoMember(11)]
		public short Unknown9 { get; set; }

		[AoMember(12)]
		public short Unknown10 { get; set; }

		[AoMember(13)]
		public short Unknown11 { get; set; }

		[AoMember(14, SerializeSize = ArraySizeType.Int16)]
		public byte[] FormattedText { get; set; }

		[AoMember(15)]
		public int TowerCount3F1 { get; set; }

		[AoMember(16)]
		public int TowerLowId { get; set; }

		[AoMember(17)]
		public int TowerHighId { get; set; }

		[AoMember(18)]
		public int TowerQuality { get; set; }

		[AoMember(19)]
		public int Unknown12 { get; set; }

		[AoMember(20)]
		[AoUsesFlags("flags", typeof(int), FlagsCriteria.EqualsToAny, new int[] { 92 })]
		public int? Timer { get; set; }

		[AoMember(21)]
		[AoUsesFlags("flags", typeof(byte), FlagsCriteria.EqualsToAny, new int[] { 92 })]
		public byte? NextSuppressionGas { get; set; }

		[AoMember(22)]
		public int Unknown14 { get; set; }

		[AoMember(23)]
		public int Unknown15 { get; set; }

		[AoMember(24)]
		public int Unknown16 { get; set; }
	}
	public class MarketSendSlot
	{
		[AoMember(0)]
		public Identity Slot { get; set; }
	}
	public class VendingMachineSlot
	{
		[AoMember(0)]
		public int ItemLowId { get; set; }

		[AoMember(1)]
		public int ItemHighId { get; set; }

		[AoMember(2)]
		public int Quality { get; set; }
	}
}
namespace AOSharp.Core.IPC
{
	public abstract class IPCChannelBase
	{
		private static IPAddress MulticastIP = IPAddress.Parse("224.0.0.111");

		private IPEndPoint _localEndPoint = new IPEndPoint(IPAddress.Any, 1911);

		private IPEndPoint _remoteEndPoint = new IPEndPoint(MulticastIP, 1911);

		private const int Port = 1911;

		private const ushort PacketPrefix = ushort.MaxValue;

		private byte _channelId;

		private UdpClient _udpClient;

		private static SerializerResolver _serializerResolver = new SerializerResolverBuilder<IPCMessage>().Build();

		private static SmokeLounge.AOtomation.Messaging.Serialization.TypeInfo _typeInfo = new SmokeLounge.AOtomation.Messaging.Serialization.TypeInfo(typeof(IPCMessage));

		private static PacketInspector _packetInspector;

		private ConcurrentQueue<byte[]> _packetQueue = new ConcurrentQueue<byte[]>();

		private Dictionary<int, Action<int, IPCMessage>> _callbacks = new Dictionary<int, Action<int, IPCMessage>>();

		private static List<IPCChannelBase> _ipcChannels = new List<IPCChannelBase>();

		protected abstract int _localDynelId { get; }

		protected IPCChannelBase(byte channelId)
		{
			_channelId = channelId;
			_udpClient = new UdpClient();
			_udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
			_udpClient.Client.Bind(_localEndPoint);
			_udpClient.JoinMulticastGroup(MulticastIP);
			_udpClient.BeginReceive(ReceiveCallback, null);
			_packetInspector = new PacketInspector(_typeInfo);
			_ipcChannels.Add(this);
		}

		~IPCChannelBase()
		{
			_ipcChannels.Remove(this);
		}

		protected static void Update()
		{
			try
			{
				foreach (IPCChannelBase ipcChannel in _ipcChannels)
				{
					ipcChannel.ProcessQueue();
				}
			}
			catch (Exception)
			{
			}
		}

		private void ProcessQueue()
		{
			byte[] result;
			while (_packetQueue.TryDequeue(out result))
			{
				ProcessIPCMessage(result);
			}
		}

		private void ReceiveCallback(IAsyncResult ar)
		{
			byte[] array = _udpClient.EndReceive(ar, ref _localEndPoint);
			_udpClient.BeginReceive(ReceiveCallback, null);
			if (array.Length >= 11)
			{
				_packetQueue.Enqueue(array);
			}
		}

		private void ProcessIPCMessage(byte[] msgBytes)
		{
			try
			{
				using MemoryStream stream = new MemoryStream(msgBytes);
				SmokeLounge.AOtomation.Messaging.Serialization.StreamReader streamReader = new SmokeLounge.AOtomation.Messaging.Serialization.StreamReader(stream)
				{
					Position = 0L
				};
				if (streamReader.ReadUInt16() != ushort.MaxValue)
				{
					return;
				}
				ushort num = streamReader.ReadUInt16();
				if (num != msgBytes.Length)
				{
					return;
				}
				byte b = streamReader.ReadByte();
				if (b != _channelId)
				{
					return;
				}
				int num2 = streamReader.ReadInt32();
				if (num2 == _localDynelId)
				{
					return;
				}
				streamReader.Position = 2L;
				int identifier;
				SmokeLounge.AOtomation.Messaging.Serialization.TypeInfo typeInfo = _packetInspector.FindSubType(streamReader, out identifier);
				if (typeInfo == null)
				{
					return;
				}
				ISerializer serializer = _serializerResolver.GetSerializer(typeInfo.Type);
				if (serializer != null)
				{
					streamReader.Position = 11L;
					SerializationContext serializationContext = new SerializationContext(_serializerResolver);
					IPCMessage arg = (IPCMessage)serializer.Deserialize(streamReader, serializationContext);
					if (_callbacks.ContainsKey(identifier))
					{
						_callbacks[identifier]?.Invoke(num2, arg);
					}
				}
			}
			catch (Exception)
			{
			}
		}

		public void Broadcast(IPCMessage msg)
		{
			using MemoryStream memoryStream = new MemoryStream();
			ISerializer serializer = _serializerResolver.GetSerializer(msg.GetType());
			if (serializer != null)
			{
				int identifier = ((AoContractAttribute)msg.GetType().GetCustomAttributes(typeof(AoContractAttribute)).FirstOrDefault()).Identifier;
				SerializationContext serializationContext = new SerializationContext(_serializerResolver);
				SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter streamWriter = new SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter(memoryStream)
				{
					Position = 0L
				};
				streamWriter.WriteUInt16(ushort.MaxValue);
				streamWriter.WriteInt16(0);
				streamWriter.WriteByte(_channelId);
				streamWriter.WriteInt32(_localDynelId);
				streamWriter.WriteInt16((short)identifier);
				serializer.Serialize(streamWriter, serializationContext, msg);
				long position = streamWriter.Position;
				streamWriter.Position = 2L;
				streamWriter.WriteInt16((short)position);
				streamWriter.Dispose();
				byte[] array = memoryStream.ToArray();
				_udpClient.Send(array, array.Length, _remoteEndPoint);
			}
		}

		public void RegisterCallback(int opCode, Action<int, IPCMessage> callback)
		{
			if (!_callbacks.ContainsKey(opCode))
			{
				_callbacks.Add(opCode, callback);
			}
		}

		public static void LoadMessages(Assembly assembly)
		{
			_typeInfo.InitializeSubTypesForAssembly(assembly);
		}

		public bool SetChannelId(byte channelId)
		{
			if (_ipcChannels.Any((IPCChannelBase x) => x._channelId == channelId))
			{
				return false;
			}
			_channelId = channelId;
			return true;
		}
	}
	[AoKnownType(9, IdentifierType.Int16)]
	public class IPCMessage
	{
		public virtual short Opcode { get; }
	}
}
namespace AOSharp.Common
{
	public static class Extensions
	{
		public static string ToHexString(this byte[] data)
		{
			return BitConverter.ToString(data).Replace("-", "");
		}

		public static string ToString(this PerkHash perkHash)
		{
			return Encoding.ASCII.GetString(BitConverter.GetBytes((uint)perkHash).Reverse().ToArray());
		}
	}
}
namespace AOSharp.Common.Unmanaged.Interfaces
{
	public class GuiResourceManager
	{
		public static IntPtr GetGUITexture(int gfxId, string path, int format = 2, int unk1 = 0, int unk2 = 0)
		{
			return GuiResourceManager_t.GetGuiTexture(GuiResourceManager_t.GetInstance(), gfxId, path, format, unk1, unk2);
		}

		public static void ReleaseTexture(IntPtr pSprite)
		{
			GuiResourceManager_t.ReleaseTexture(GuiResourceManager_t.GetInstance(), pSprite);
		}

		public static IntPtr CreateGUITexture(string name, int id, string path)
		{
			if (!File.Exists(path))
			{
				return IntPtr.Zero;
			}
			DynamicID.Add(name, id);
			return GetGUITexture(id, path);
		}
	}
	public class InventoryGUIModule
	{
		public static string GetBackpackName(Identity identity)
		{
			IntPtr instance = InventoryGUIModule_c.GetInstance();
			if (instance == IntPtr.Zero)
			{
				return string.Empty;
			}
			StdString stdString = StdString.Create();
			InventoryGUIModule_c.GetBackpackName(instance, stdString.Pointer, ref identity, unk: true);
			return stdString.ToString();
		}

		public static int SetBackpackName(Identity identity, string name)
		{
			IntPtr instance = InventoryGUIModule_c.GetInstance();
			if (instance == IntPtr.Zero)
			{
				return -1;
			}
			return InventoryGUIModule_c.SetBackpackName(instance, ref identity, StdString.Create(name).Pointer, unk: true);
		}
	}
	public class PlayfieldAnarchy
	{
		private IntPtr _pointer;

		private PlayfieldAnarchy(IntPtr pointer)
		{
			_pointer = pointer;
		}

		public bool IsShadowlandPF()
		{
			return PlayfieldAnarchy_t.IsShadowlandPF(_pointer);
		}

		public bool AreVehiclesAllowed()
		{
			return PlayfieldAnarchy_t.AreVehiclesAllowed(_pointer);
		}

		public PlayfieldDistrictInfo GetDistrictInfo()
		{
			return new PlayfieldDistrictInfo(PlayfieldAnarchy_t.GetDistrictInfo(_pointer));
		}

		public LandControlMap GetLandControlMap()
		{
			return new LandControlMap(PlayfieldAnarchy_t.GetLandControlMap(_pointer));
		}

		public int GetPFWorldXPos()
		{
			return PlayfieldAnarchy_t.GetPFWorldXPos(_pointer);
		}

		public int GetPFWorldZPos()
		{
			return PlayfieldAnarchy_t.GetPFWorldZPos(_pointer);
		}

		public Vector3 GetSafePos()
		{
			return PlayfieldAnarchy_t.GetSafePos(_pointer);
		}

		public bool IsGrid()
		{
			return PlayfieldAnarchy_t.IsGrid(_pointer);
		}
	}
	public class DynamicID
	{
		public static Dictionary<string, int> DynamicIDOverrides = new Dictionary<string, int>();

		public static int GetID(string name, bool unk)
		{
			if (DynamicIDOverrides.TryGetValue(name, out var value))
			{
				return value;
			}
			IntPtr instance = DynamicID_t.GetInstance();
			if (instance == IntPtr.Zero)
			{
				return 0;
			}
			return DynamicID_t.GetID(instance, name, unk);
		}

		public static void Add(string name, int id)
		{
			DynamicIDOverrides.Add(name, id);
		}
	}
	public class Preferences
	{
		public static string GetCharacterPath()
		{
			IntPtr instanceIfAny = Preferences_t.GetInstanceIfAny();
			if (instanceIfAny == IntPtr.Zero)
			{
				return null;
			}
			return Utils.UnsafePointerToString(Preferences_t.GetCharacterPath(instanceIfAny));
		}
	}
	public class N3EngineClientAnarchy
	{
		public static string GetDesc(Identity identity)
		{
			IntPtr instance = N3InterfaceModule_t.GetInstance();
			if (instance == IntPtr.Zero)
			{
				return string.Empty;
			}
			return Marshal.PtrToStringAnsi(N3InterfaceModule_t.GetDesc(instance, ref identity, 0));
		}

		public static string GetPFName(int id)
		{
			return Marshal.PtrToStringAnsi(N3EngineClientAnarchy_t.GetPFName(id));
		}

		public static string GetPerkName(int perkId, bool unk = false)
		{
			StdString stdString = StdString.Create();
			IntPtr perkName = N3EngineClientAnarchy_t.GetPerkName(stdString.Pointer, perkId, unk);
			if (perkName == IntPtr.Zero)
			{
				return string.Empty;
			}
			return stdString.ToString();
		}

		public static float GetPerkProgress(uint perkId)
		{
			return N3InterfaceModule_t.GetPerkProgress(N3InterfaceModule_t.GetInstance(), perkId);
		}

		public static List<uint> GetCompletedPersonalResearchGoals()
		{
			StdStructVector vector = default(StdStructVector);
			N3InterfaceModule_t.GetCompletedPersonalResearchGoals(N3InterfaceModule_t.GetInstance(), ref vector);
			return vector.ToList<uint>();
		}

		public static List<ResearchGoal> GetPersonalResearchGoals()
		{
			StdStructVector vector = default(StdStructVector);
			N3InterfaceModule_t.GetPersonalResearchGoals(N3InterfaceModule_t.GetInstance(), ref vector);
			return vector.ToList<ResearchGoal>();
		}

		public static void DebugSpellListToChat(Identity identity, int unk, int spellList)
		{
			IntPtr instance = N3Engine_t.GetInstance();
			if (!(instance == IntPtr.Zero))
			{
				N3EngineClientAnarchy_t.DebugSpellListToChat(instance, unk, ref identity, spellList);
			}
		}

		public static Identity TemplateIDToDynelID(Identity templateId)
		{
			N3EngineClientAnarchy_t.TemplateIDToDynelID(N3Engine_t.GetInstance(), out var dynelId, ref templateId);
			return dynelId;
		}

		public static string GetName(Identity identity)
		{
			IntPtr instance = N3Engine_t.GetInstance();
			if (instance == IntPtr.Zero)
			{
				return null;
			}
			Identity identityUnk = default(Identity);
			return Utils.UnsafePointerToString(N3EngineClientAnarchy_t.GetName(instance, ref identity, ref identityUnk));
		}

		public static bool HasPerk(int perkId)
		{
			IntPtr instance = N3Engine_t.GetInstance();
			if (instance == IntPtr.Zero)
			{
				throw new NullReferenceException("Could not get N3Engine instance");
			}
			return N3EngineClientAnarchy_t.HasPerk(instance, perkId);
		}

		public static void UseItem(Identity identity, bool unknown = false)
		{
			IntPtr instance = N3Engine_t.GetInstance();
			if (instance != IntPtr.Zero)
			{
				N3EngineClientAnarchy_t.UseItem(instance, ref identity, unknown);
			}
		}

		public static void UseItemOnItem(Identity source, Identity target)
		{
			IntPtr instance = N3Engine_t.GetInstance();
			if (instance != IntPtr.Zero)
			{
				N3EngineClientAnarchy_t.UseItemOnItem(instance, ref source, ref target);
			}
		}

		public static void UseItemOnCharacter(Identity source, Identity target)
		{
			IntPtr instance = N3Engine_t.GetInstance();
			if (instance != IntPtr.Zero)
			{
				N3EngineClientAnarchy_t.UseItemOnCharacter(instance, ref source, ref target);
			}
		}

		public static bool GetQuestWorldPos(Identity mission, out Identity playfield, out Vector3 universePos, out Vector3 zonePos)
		{
			playfield = Identity.None;
			universePos = Vector3.Zero;
			zonePos = Vector3.Zero;
			IntPtr instance = N3Engine_t.GetInstance();
			if (instance == IntPtr.Zero)
			{
				return false;
			}
			return N3EngineClientAnarchy_t.GetQuestWorldPos(instance, ref mission, ref playfield, ref universePos, ref zonePos);
		}

		public static int GetNumberOfFreeInventorySlots()
		{
			IntPtr instance = N3Engine_t.GetInstance();
			if (instance == IntPtr.Zero)
			{
				return 0;
			}
			return N3EngineClientAnarchy_t.GetNumberOfFreeInventorySlots(instance);
		}

		public static void SetStat(Stat stat, int value)
		{
			IntPtr instance = N3Engine_t.GetInstance();
			if (instance != IntPtr.Zero)
			{
				N3EngineClientAnarchy_t.SetStat(instance, value, stat);
			}
		}
	}
	public class ResourceDatabase
	{
		public static IntPtr GetDbObject(DBIdentity identity)
		{
			IntPtr pThis = N3DatabaseHandler_t.Get();
			IntPtr resourceDatabase = N3DatabaseHandler_t.GetResourceDatabase(pThis);
			return ResourceDatabase_t.GetDbObject(resourceDatabase, ref identity);
		}

		public static T GetDbObject<T>(DBIdentity identity) where T : DbObject
		{
			IntPtr dbObject = GetDbObject(identity);
			if (dbObject == IntPtr.Zero)
			{
				return null;
			}
			return (T)Activator.CreateInstance(typeof(T), dbObject);
		}

		public static void PutDbBlob(DBIdentity identity, byte[] blobData)
		{
			IntPtr pThis = N3DatabaseHandler_t.Get();
			IntPtr resourceDatabase = N3DatabaseHandler_t.GetResourceDatabase(pThis);
			ResourceDatabase_t.PutDbBlob(resourceDatabase, ref identity, blobData, blobData.Length);
		}

		public static void PutDbObject(IntPtr pDbObject)
		{
			IntPtr pThis = N3DatabaseHandler_t.Get();
			IntPtr resourceDatabase = N3DatabaseHandler_t.GetResourceDatabase(pThis);
			ResourceDatabase_t.PutDbObject(resourceDatabase, pDbObject);
		}
	}
}
namespace AOSharp.Common.Unmanaged.Imports
{
	public class DynamicID_t
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, SetLastError = true)]
		public delegate int DGetID(IntPtr pThis, [MarshalAs(UnmanagedType.LPStr)] string name, bool unk);

		[DllImport("AFCM.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstance@DynamicID_t@@SAPAV1@XZ")]
		public static extern IntPtr GetInstance();

		[DllImport("AFCM.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetID@DynamicID_t@@QAEHPBD_N@Z")]
		public static extern int GetID(IntPtr pThis, [MarshalAs(UnmanagedType.LPStr)] string name, bool unk);
	}
	public class Connection_t
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate int DSend(IntPtr pConnection, uint unk, int len, [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buf);

		[DllImport("Connection.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?Send@Connection_t@@QAEHIIPBX@Z")]
		public static extern int Send(IntPtr pConnection, uint unk, int len, [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buf);
	}
	public class VisualEnvFX_t
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate int DFrameProcess(IntPtr pThis, float unk1, float unk2, int unk3, float unk4, int unk5, int unk6);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?FrameProcess@VisualEnvFX_t@@QAEXMMIMAAVVector3_t@@AAVQuaternion_t@@@Z")]
		public static extern int FrameProcess(IntPtr pThis, float unk1, float unk2, int unk3, float unk4, int unk5, int unk6);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstance@VisualEnvFX_t@@SAPAV1@XZ")]
		public static extern IntPtr GetInstance();

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleOcclusionCulling@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleOcclusionCulling(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerDepthDisplay@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerDepthDisplay(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerDoNotRender@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerDoNotRender(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerKDTreeDisplay@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerKDTreeDisplay(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerOcclusionBodies@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerOcclusionBodies(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerOcclusionScan@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerOcclusionScan(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerOcclusionTest@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerOcclusionTest(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerOffscreenDisplay@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerOffscreenDisplay(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerRefractionDisplay@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerRefractionDisplay(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerShowCATWireframe@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerShowCATWireframe(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerShowGroundWireframe@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerShowGroundWireframe(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerShowLiquidWireframe@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerShowLiquidWireframe(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerShowMouseFix@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerShowMouseFix(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerShowStatelWireframe@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerShowStatelWireframe(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerShowSurfaceSliding@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerShowSurfaceSliding(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerSphereTest@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerSphereTest(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerSyncDisplay@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerSyncDisplay(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerToggleCapOffscreen@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerToggleCapOffscreen(IntPtr pVisualEnvFX);

		[DllImport("DisplaySystem.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleRandyDebuggerToggleCapRefraction@VisualEnvFX_t@@QAE_NXZ")]
		public static extern bool ToggleRandyDebuggerToggleCapRefraction(IntPtr pVisualEnvFX);
	}
	public class DummyItem_t
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate int GetStatDelegate(IntPtr pThis, Stat stat, int detail);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate IntPtr GetSpellListDelegate(IntPtr pThis, SpellListType spellList);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate IntPtr GetSpellDataUnkDelegate(IntPtr pThis, ref GetSpellDataUnkStruct pUnkStruct);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate IntPtr GetSpellDataDelegate(ref GetSpellDataUnkStruct pThis);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate IntPtr GetOpNameDelegate(int op);

		public struct GetSpellDataUnkStruct
		{
			public IntPtr SpellListPointer;

			public int Unk;

			public int Idx;
		}

		public static GetStatDelegate GetStat;

		public static GetSpellListDelegate GetSpellList;

		public static GetSpellDataUnkDelegate GetSpellDataUnk;

		public static GetSpellDataDelegate GetSpellData;

		public static GetOpNameDelegate GetOpName;
	}
	public class GamecodeUnk
	{
		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public delegate int AppendSystemTextDelegate(int unk, [MarshalAs(UnmanagedType.LPStr)] string message, ChatColor color);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		[return: MarshalAs(UnmanagedType.U1)]
		public delegate bool IsInLineOfSightDelegate(IntPtr pThis, IntPtr pTarget);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate void FollowTargetDelegate(IntPtr pVehicle_t, IntPtr pDynel, float distance, IntPtr waypoints);

		public static AppendSystemTextDelegate AppendSystemText;

		public static IsInLineOfSightDelegate IsInLineOfSight;

		public static FollowTargetDelegate FollowTarget;
	}
	public class GameTime_t
	{
		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstance@GameTime_t@@SAPAV1@XZ")]
		public static extern IntPtr GetInstance();

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetNormalTime@GameTime_t@@QBENXZ")]
		public static extern double GetNormalTime(IntPtr pThis);
	}
	public class N3EngineClientAnarchy_t
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, SetLastError = true)]
		public delegate bool DCastNanoSpell(IntPtr pThis, ref Identity nanoIdentity, ref Identity targetIdentity);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate bool DPerformSpecialAction(IntPtr pThis, ref Identity identity);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate IntPtr DTextCommand(IntPtr pThis, IntPtr unk, IntPtr text, IntPtr identity);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public unsafe delegate StdObjList* GetMissionListDelegate(IntPtr pThis, IntPtr unk);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate IntPtr GetItemActionInfoDelegate(IntPtr pThis, ItemActionInfo action);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate void ToClientN3MessageDelegate(IntPtr pThis, ref Identity identity, IntPtr pDataBlock);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate void DRunEngine(IntPtr pThis, float unk);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate bool DSendInPlayMessage(IntPtr pThis);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate void DPlayfieldInit(IntPtr pThis, uint id);

		public static GetMissionListDelegate GetMissionList;

		public static GetItemActionInfoDelegate GetItemActionInfo;

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0n3EngineClientAnarchy_t@@QAE@XZ")]
		public static extern IntPtr Constructor(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetPlayfieldFactory@n3EngineClientAnarchy_t@@UAEPAVn3PlayfieldFactory_i@@ABVPlayfieldProxy_t@@@Z")]
		public static extern IntPtr GetPlayfieldFactory(IntPtr pThis, ref PlayfieldProxy playfieldProxy);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?OpenClient@n3EngineClientAnarchy_t@@QAEXPAVResourceDatabase_t@@I@Z")]
		public static extern void OpenClient(IntPtr pThis, IntPtr pResourceDatabase, int clientInst);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetQuestWorldPos@n3EngineClientAnarchy_t@@QBE_NABVIdentity_t@@AAV2@AAVVector3_t@@2@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool GetQuestWorldPos(IntPtr pThis, ref Identity mission, ref Identity playfield, ref Vector3 universePos, ref Vector3 ZonePos);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_DebugSpellListToChat@n3EngineClientAnarchy_t@@QBEXHABVIdentity_t@@W4SpellList_e@GameData@@@Z")]
		public static extern void DebugSpellListToChat(IntPtr pThis, int unk, ref Identity identity, int spellList);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_SecondarySpecialAttack@n3EngineClientAnarchy_t@@QBE_NABVIdentity_t@@W4Stat_e@GameData@@@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool SecondarySpecialAttack(IntPtr pThis, ref Identity target, Stat stat);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_DefaultAttack@n3EngineClientAnarchy_t@@QBEXABVIdentity_t@@_N@Z")]
		public static extern void DefaultAttack(IntPtr pThis, ref Identity target, bool unk);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_TeamJoinRequest@n3EngineClientAnarchy_t@@QBE_NABVIdentity_t@@_N@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool TeamJoinRequest(IntPtr pThis, ref Identity identity, bool force);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_StopAttack@n3EngineClientAnarchy_t@@QBEXXZ")]
		public static extern void StopAttack(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetSkill@n3EngineClientAnarchy_t@@QBEHABVIdentity_t@@W4Stat_e@GameData@@H0@Z")]
		public static extern int GetSkill(IntPtr pThis, ref Identity dynel, Stat stat, int detail, ref Identity unk);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetSkillMax@n3EngineClientAnarchy_t@@QAEHW4Stat_e@GameData@@@Z")]
		public static extern int GetSkillMax(IntPtr pThis, Stat stat);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_PersonalResearchGoals@n3EngineClientAnarchy_t@@QAEXAAV?$vector@U?$pair@I_N@std@@V?$allocator@U?$pair@I_N@std@@@2@@std@@@Z")]
		public static extern IntPtr PersonalResearchGoals(IntPtr pThis, IntPtr pVector);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_IsSecondarySpecialAttackAvailable@n3EngineClientAnarchy_t@@QBE_NW4Stat_e@GameData@@@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsSecondarySpecialAttackAvailable(IntPtr pThis, Stat stat);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_IsResearch@n3EngineClientAnarchy_t@@QBE_NI@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsResearch(IntPtr pThis, int id);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetAttackRange@n3EngineClientAnarchy_t@@QBEMXZ")]
		public static extern float GetAttackRange(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_CastNanoSpell@n3EngineClientAnarchy_t@@QAEXABVIdentity_t@@0@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool CastNanoSpell(IntPtr pThis, ref Identity nano, ref Identity target);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetCorrectActionID@n3EngineClientAnarchy_t@@QBEXAAVIdentity_t@@@Z")]
		public static extern void GetCorrectActionId(IntPtr pThis, ref Identity identity);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_PerformSpecialAction@n3EngineClientAnarchy_t@@QAE_NABVIdentity_t@@@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool PerformSpecialAction(IntPtr pThis, ref Identity action);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "?N3Msg_GetPFName@n3EngineClientAnarchy_t@@QBEPBDI@Z")]
		public static extern IntPtr GetPFName(int id);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetName@n3EngineClientAnarchy_t@@QBEPBDABVIdentity_t@@0@Z")]
		public static extern IntPtr GetName(IntPtr pThis, ref Identity identity, ref Identity identityUnk);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "?N3Msg_GetPerkName@n3EngineClientAnarchy_t@@QBE?AV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@I_N@Z")]
		public static extern IntPtr GetPerkName(IntPtr retStr, int perkId, bool unk);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_IsFormulaReady@n3EngineClientAnarchy_t@@QBE_NABVIdentity_t@@@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsFormulaReady(IntPtr pThis, ref Identity identity);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_HasPerk@n3EngineClientAnarchy_t@@QAE_NI@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool HasPerk(IntPtr pThis, int perkId);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_IsAttacking@n3EngineClientAnarchy_t@@QBE_NXZ")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsAttacking(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetSpecialActionList@n3EngineClientAnarchy_t@@QAEPAV?$list@VSpecialAction_t@@V?$allocator@VSpecialAction_t@@@std@@@std@@XZ")]
		public unsafe static extern StdObjList* GetSpecialActionList(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetNanoSpellList@n3EngineClientAnarchy_t@@QAEPBV?$list@HV?$allocator@H@std@@@std@@XZ")]
		public unsafe static extern StdObjList* GetNanoSpellList(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetNanoTemplateInfoList@n3EngineClientAnarchy_t@@QBEPAV?$list@VNanoTemplateInfo_c@@V?$allocator@VNanoTemplateInfo_c@@@std@@@std@@ABVIdentity_t@@@Z")]
		public unsafe static extern StdObjList* GetNanoTemplateInfoList(IntPtr pThis, Identity* identity);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_IsMoving@n3EngineClientAnarchy_t@@QBE_NXZ")]
		public static extern void IsMoving(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_MovementChanged@n3EngineClientAnarchy_t@@QAEXW4MovementAction_e@Movement_n@@MM_N@Z")]
		public static extern void MovementChanged(IntPtr pThis, MovementAction action, float unk1, float unk2, bool unk3);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetNumberOfFreeInventorySlots@n3EngineClientAnarchy_t@@QAEHXZ")]
		public static extern int GetNumberOfFreeInventorySlots(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetContainerInventoryList@n3EngineClientAnarchy_t@@QBEPBV?$list@VInventoryEntry_t@@V?$allocator@VInventoryEntry_t@@@std@@@std@@ABVIdentity_t@@@Z")]
		public static extern IntPtr GetContainerInventoryList(IntPtr pThis, ref Identity identity);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetInventoryVec@n3EngineClientAnarchy_t@@QAEPBV?$vector@PAVNewInventoryEntry_t@@V?$allocator@PAVNewInventoryEntry_t@@@std@@@std@@ABVIdentity_t@@@Z")]
		public static extern IntPtr GetInventoryVec(IntPtr pThis, ref Identity identity);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_IsInTeam@n3EngineClientAnarchy_t@@QBE_NABVIdentity_t@@@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public unsafe static extern bool IsInTeam(IntPtr pThis, Identity* identity);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_TradeskillCombine@n3EngineClientAnarchy_t@@QBEXABVIdentity_t@@0@Z")]
		public static extern IntPtr TradeskillCombine(IntPtr pThis, IntPtr source, IntPtr destination);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetClientDynelId@n3EngineClientAnarchy_t@@UBE?AVIdentity_t@@XZ")]
		public unsafe static extern Identity* GetClientDynelId(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_SelectedTarget@n3EngineClientAnarchy_t@@QAEXABVIdentity_t@@@Z")]
		public static extern IntPtr SelectedTarget(IntPtr pThis, ref Identity target);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_IsInRaidTeam@n3EngineClientAnarchy_t@@QAE_NXZ")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsInRaidTeam(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetTeamMemberList@n3EngineClientAnarchy_t@@QAEPAV?$vector@PAVTeamEntry_t@@V?$allocator@PAVTeamEntry_t@@@std@@@std@@H@Z")]
		public unsafe static extern StdObjVector* GetTeamMemberList(IntPtr pThis, int teamIndex);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetFullPerkMap@n3EngineClientAnarchy_t@@QBEABV?$vector@VPerk_t@@V?$allocator@VPerk_t@@@std@@@std@@XZ")]
		public unsafe static extern StdStructVector* GetFullPerkMap(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_IsTeamLeader@n3EngineClientAnarchy_t@@QBE_NABVIdentity_t@@@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public unsafe static extern bool IsTeamLeader(IntPtr pThis, Identity* target);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetItemByTemplate@n3EngineClientAnarchy_t@@ABEPAVDummyItemBase_t@@VIdentity_t@@ABV3@@Z")]
		public static extern IntPtr GetItemByTemplate(IntPtr pThis, Identity template, ref Identity unk);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetBuffCurrentTime@n3EngineClientAnarchy_t@@QAEHABVIdentity_t@@0@Z")]
		public static extern int GetBuffCurrentTime(IntPtr pThis, ref Identity identity, ref Identity unk);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetBuffTotalTime@n3EngineClientAnarchy_t@@QAEHABVIdentity_t@@0@Z")]
		public static extern int GetBuffTotalTime(IntPtr pThis, ref Identity identity, ref Identity unk);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_RemoveBuff@n3EngineClientAnarchy_t@@QAE_NABVIdentity_t@@@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool RemoveBuff(IntPtr pThis, ref Identity identity);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_CreateDummyItemID@n3EngineClientAnarchy_t@@QBE_NAAVIdentity_t@@ABVACGItem_t@GameData@@@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool CreateDummyItemID(IntPtr pThis, ref Identity template, ref ACGItemQueryData acgItem);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_TextCommand@n3EngineClientAnarchy_t@@QAE_NHPBDABVIdentity_t@@@Z")]
		public static extern IntPtr TextCommand(IntPtr pThis, IntPtr unk, IntPtr text, IntPtr identity);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToClientN3Message@n3EngineClientAnarchy_t@@UBEXABVIdentity_t@@PAVACE_Data_Block@@@Z")]
		public static extern void ToClientN3Message(IntPtr pThis, ref Identity identity, IntPtr pDataBlock);

		[DllImport("N3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetPlayfield@n3EngineClient_t@@SAPAVn3Playfield_t@@XZ")]
		public static extern IntPtr GetPlayfield();

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?RunEngine@n3EngineClientAnarchy_t@@UAEXM@Z")]
		public static extern void RunEngine(IntPtr pThis, float deltaTime);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_SendInPlayMessage@n3EngineClientAnarchy_t@@QBE_NXZ")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool SendInPlayMessage(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?PlayfieldInit@n3EngineClientAnarchy_t@@UAEXI@Z")]
		public static extern void PlayfieldInit(IntPtr pThis, uint id);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_IsPerk@n3EngineClientAnarchy_t@@QBE_NI@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsPerk(IntPtr pThis, int id);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetSpecialActionState@n3EngineClientAnarchy_t@@QAEHABVIdentity_t@@@Z")]
		public static extern SpecialActionState GetSpecialActionState(IntPtr pThis, ref Identity action);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_UseItem@n3EngineClientAnarchy_t@@QAEXABVIdentity_t@@_N@Z")]
		public static extern void UseItem(IntPtr pThis, ref Identity identity, bool unknown);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_UseItemOnItem@n3EngineClientAnarchy_t@@QAEXABVIdentity_t@@0@Z")]
		public static extern void UseItemOnItem(IntPtr pThis, ref Identity source, ref Identity target);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_UseItemOnCharacter@n3EngineClientAnarchy_t@@QAEXABVIdentity_t@@0@Z")]
		public static extern void UseItemOnCharacter(IntPtr pThis, ref Identity source, ref Identity target);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_SetStat@n3EngineClientAnarchy_t@@QAEXHW4Stat_e@GameData@@@Z")]
		public static extern void SetStat(IntPtr pThis, int value, Stat stat);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_TemplateIDToDynelID@n3EngineClientAnarchy_t@@QBE?AVIdentity_t@@ABV2@@Z")]
		public static extern IntPtr TemplateIDToDynelID(IntPtr pThis, out Identity dynelId, ref Identity templateId);
	}
	public class PlayfieldAnarchy_t
	{
		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetNumberOfWaters@PlayfieldAnarchy_t@@QAEHXZ")]
		public static extern int GetNumberOfWaters(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetWaters@PlayfieldAnarchy_t@@QAEPAUn3WaterData_t@@XZ")]
		public static extern IntPtr GetWaters(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AreVehiclesAllowed@PlayfieldAnarchy_t@@QBE_NXZ")]
		public static extern bool AreVehiclesAllowed(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?IsShadowlandPF@PlayfieldAnarchy_t@@QBE_NXZ")]
		public static extern bool IsShadowlandPF(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "GetDistrictInfo@PlayfieldAnarchy_t@@QAEPAVPlayfieldDistrictInfo_t@GameData@@XZ")]
		public static extern IntPtr GetDistrictInfo(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetLandControlMap@PlayfieldAnarchy_t@@QBEPBVLandControlMap_t@GameData@@XZ")]
		public static extern IntPtr GetLandControlMap(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetPFWorldXPos@PlayfieldAnarchy_t@@QBEHXZ")]
		public static extern int GetPFWorldXPos(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetPFWorldZPos@PlayfieldAnarchy_t@@QBEHXZ")]
		public static extern int GetPFWorldZPos(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetSafePos@PlayfieldAnarchy_t@@UBE?AVVector3_t@@XZ")]
		public static extern Vector3 GetSafePos(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?IsGrid@PlayfieldAnarchy_t@@QBE_NXZ")]
		public static extern bool IsGrid(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AddChildDynel@n3Playfield_t@@QAEXPAVn3Dynel_t@@ABVVector3_t@@ABVQuaternion_t@@@Z")]
		public static extern void AddChildDynel(IntPtr pThis, IntPtr pDynel, ref Vector3 pos, ref Quaternion rot);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AddCellMonitor@PlayfieldAnarchy_t@@QAEXABVVector3_t@@@Z")]
		public static extern void AddCellMonitor(IntPtr pThis, ref Vector3 pos);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?CalculateWaterHeightMax@n3Playfield_t@@QAEXXZ")]
		public static extern void CalculateWaterHeightMax(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?Run@PlayfieldAnarchy_t@@UAE_NXZ")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool Run(IntPtr pThis);
	}
	public class TeleportTrier_t
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate void DTeleportFailed(IntPtr pThis);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?TeleportFailed@TeleportTrier_t@@QAEXXZ")]
		public static extern void TeleportFailed(IntPtr pThis);
	}
	public class WeaponHolder_t
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate IntPtr GetWeaponDelegate(IntPtr pThis, EquipSlot slot, int unk);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		[return: MarshalAs(UnmanagedType.U1)]
		public delegate bool IsDynelInWeaponRangeDelegate(IntPtr pThis, IntPtr pWeapon, IntPtr pDynel);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate byte IsInRangeDelegate(IntPtr pThis);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate IntPtr GetDummyWeaponDelegate(IntPtr pThis, Stat stat);

		public static GetWeaponDelegate GetWeapon;

		public static IsDynelInWeaponRangeDelegate IsDynelInWeaponRange;

		public static IsInRangeDelegate IsInRange;

		public static GetDummyWeaponDelegate GetDummyWeapon;
	}
	public class _EffectHandler_t
	{
		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstance@_EffectHandler_t@@SAPAV1@XZ")]
		public static extern IntPtr GetInstance();

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?CreateEffect2@_EffectHandler_t@@QAEIH@Z")]
		public static extern uint CreateEffect2(IntPtr pThis, int effect);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?CreateEffect2@_EffectHandler_t@@QAEIHABVVector3_t@@@Z")]
		public static extern uint CreateEffect2(IntPtr pThis, int effect, Vector3 pos);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?CreateEffect2@_EffectHandler_t@@QAEIHABVn3Dynel_t@@H@Z")]
		public static extern uint CreateEffect2(IntPtr pThis, int effect, IntPtr pDynel, int unk);

		[DllImport("Gamecode.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetEffectByID@_EffectHandler_t@@QAEPAV_GfxControl_t@@I@Z")]
		public static extern IntPtr GetEffectByID(IntPtr pThis, uint effectId);

		[DllImport("Gamecode.dll", EntryPoint = "?SetDuration@_EffectHandler_t@@QAEXIM@Z")]
		public static extern void SetDuration(IntPtr pThis, uint hEffect, float duration);
	}
	public class DistrictData_t
	{
		[DllImport("GameData.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetFightMode@DistrictData_t@GameData@@QBE?AW4FightTypeAllowed_e@@XZ")]
		public static extern int GetFightMode(IntPtr pDistrictData);

		[DllImport("GameData.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?IsLandControlled@DistrictData_t@GameData@@QBE_NXZ")]
		public static extern bool IsLandControlled(IntPtr pDistrictData);
	}
	public class DropdownMenu_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AppendItem@DropdownMenu_c@@QAEIABVVariant@@ABVString@@@Z")]
		public static extern int AppendItem(IntPtr pThis, IntPtr pVar, IntPtr pLabelStr);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetSelection@DropdownMenu_c@@QBEHXZ")]
		public static extern uint GetSelection(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetItemLabel@DropdownMenu_c@@QBEABVString@@I@Z")]
		public static extern IntPtr GetItemLabel(IntPtr pThis, uint num);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SelectByIndex@DropdownMenu_c@@QAEXH_N@Z")]
		public static extern IntPtr SelectByIndex(IntPtr pThis, uint num, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?DeleteItem@DropdownMenu_c@@QAEXI@Z")]
		public static extern IntPtr DeleteItem(IntPtr pThis, uint num);
	}
	public class BitmapView_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AddBitmap@BitmapView_c@@QAEXH@Z")]
		public static extern void SetBitmap(IntPtr pThis, int id);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?Clear@BitmapView_c@@QAEX_N@Z")]
		public static extern void Clear(IntPtr pThis, bool unk);
	}
	public class Button_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetLabel@Button_c@@QAEABVString@@XZ")]
		public static extern IntPtr GetLabel(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetLabel@Button_c@@QAEXABVString@@@Z")]
		public static extern void SetLabel(IntPtr pThis, IntPtr pStr);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetGfx@Button_c@@QAEXW4StateID_e@1@H@Z")]
		public static extern void SetGfx(IntPtr pThis, ButtonState buttonState, int gfxId);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetBackgroundIcon@Button_c@@QAEXH_N@Z")]
		public static extern void SetBackgroundIcon(IntPtr pThis, int id, bool state);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetColorOverride@Button_c@@QAEXI@Z")]
		public static extern void SetColorOverride(IntPtr pThis, uint unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetLabelColor@Button_c@@QAEXI@Z")]
		public static extern void SetLabelColor(IntPtr pThis, uint unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetBorderView@Button_c@@QAEPAVBorderView_c@@W4StateID_e@1@@Z")]
		public static extern IntPtr GetBorderView(IntPtr pThis, ButtonState buttonState);
	}
	public class ButtonBase_c
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate void DSetValue(IntPtr pThis, IntPtr pVariant, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetValue@ButtonBase_c@@UAEXABVVariant@@_N@Z")]
		public static extern void SetValue(IntPtr pThis, IntPtr pVariant, bool unk);
	}
	public class ComboBox_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AppendItem@ComboBox_c@@QAEIABVVariant@@ABVString@@@Z")]
		public static extern int AppendItem(IntPtr pThis, IntPtr pVar, IntPtr pLabelStr);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?Clear@ComboBox_c@@QAEXXZ")]
		public static extern int Clear(IntPtr pThis);
	}
	public class ItemListViewBase_c
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate IntPtr CreateItemListViewDelegate(IntPtr pThis, ref Rect rect, int flags, int unk3, int unk4, ref Identity unk5);

		public static CreateItemListViewDelegate CreateItemListView;

		public static IntPtr Create(Rect rect, int flags, int unk3, int unk4, Identity unk5)
		{
			return CreateItemListView(MSVCR100.New(880), ref rect, flags, unk3, unk4, ref unk5);
		}
	}
	public class InventoryListViewItem_c
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate IntPtr CreateInventoryListViewItemDelegate(IntPtr pThis, int unk1, ref Identity dummyItem, bool unk2);

		public static CreateInventoryListViewItemDelegate CreateInventoryListViewItem;

		public static IntPtr Create(int unk1, Identity dummyItem, bool unk2)
		{
			return CreateInventoryListViewItem(MSVCR100.New(288), unk1, ref dummyItem, unk2);
		}
	}
	public class MultiListViewItem_c
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, SetLastError = true)]
		public delegate void DSelect(IntPtr pThis, bool selected, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?IsSelected@MultiListViewItem_c@@QBE_NXZ")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsSelected(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetID@MultiListViewItem_c@@QBEABVVariant@@XZ")]
		public static extern IntPtr GetID(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetListView@MultiListViewItem_c@@QBEPAVMultiListView_c@@XZ")]
		public static extern IntPtr GetListView(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?Invalidate@MultiListViewItem_c@@QAEXXZ")]
		public static extern void Invalidate(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?Select@MultiListViewItem_c@@QAEX_N0@Z")]
		public static extern void Select(IntPtr pThis, bool selected, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0MultiListViewItem_c@@QAE@ABVVariant@@@Z")]
		internal static extern IntPtr Constructor(IntPtr pThis, IntPtr pVariant);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1MultiListViewItem_c@@MAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);

		public static IntPtr Create(Variant variant)
		{
			return Constructor(MSVCR100.New(288), variant.Pointer);
		}
	}
	public class LFTListViewItem_c
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate IntPtr CreateLFTListViewItemDelegate(IntPtr pThis, int id, StdStringStruct str1, StdStringStruct str2, StdStringStruct str3, StdStringStruct str4, StdStringStruct str5);

		public static CreateLFTListViewItemDelegate CreateLFTListViewItem;
	}
	public class MultiListView_c
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, SetLastError = true)]
		public delegate void DItemSelectionStateChanged(IntPtr pThis, IntPtr pItem, byte selected);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0MultiListView_c@@QAE@ABVRect@@III@Z")]
		internal static extern IntPtr Constructor(IntPtr pThis, ref Rect rect, int flags, int unk1, int unk2);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1MultiListView_c@@UAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AddColumn@MultiListView_c@@QAEXHABVString@@MI@Z")]
		public static extern void AddColumn(IntPtr pThis, int idx, IntPtr pStr, float width, int unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetScrolledView@MultiListView_c@@QAEPAVView@@XZ")]
		public static extern IntPtr GetScrolledView(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AddItem@MultiListView_c@@QAE_NABVIPoint@@PAVMultiListViewItem_c@@_N@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool AddItem(IntPtr pThis, ref IPoint slot, IntPtr listViewItem, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetGridIconSize@MultiListView_c@@QAEXW4IconSize_e@1@@Z")]
		public static extern void SetGridIconSize(IntPtr pThis, int num);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetLayoutMode@MultiListView_c@@QAEXW4LayoutMode_e@1@@Z")]
		public static extern void SetLayoutMode(IntPtr pThis, int mode);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetBackgroundBitmap@MultiListView_c@@QAEXH@Z")]
		public static extern void SetBackgroundBitmap(IntPtr pThis, int gfxId);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetGridIconSpacing@MultiListView_c@@QAEXABVPoint@@@Z")]
		public static extern void SetGridIconSpacing(IntPtr pThis, ref Vector2 spacing);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetGridLabelsOnTop@MultiListView_c@@QAEX_N@Z")]
		public static extern void SetGridLabelsOnTop(IntPtr pThis, bool spacing);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetViewCellCounts@MultiListView_c@@QAEXABVIPoint@@0@Z")]
		public static extern void SetViewCellCounts(IntPtr pThis, ref IPoint unk1, ref IPoint unk2);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetFirstFreePos@MultiListView_c@@QBE?AVIPoint@@XZ")]
		public static extern IntPtr GetFirstFreePos(IntPtr pThis, ref IPoint pos);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetSelectedItem@MultiListView_c@@QBEPAVMultiListViewItem_c@@XZ")]
		public static extern IntPtr GetSelectedItem(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?InvalidateItem@MultiListView_c@@QAEXPAVMultiListViewItem_c@@@Z")]
		public static extern void InvalidateItem(IntPtr pThis, IntPtr pListViewItem);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?RemoveItem@MultiListView_c@@QAEXPAVMultiListViewItem_c@@@Z")]
		public static extern void RemoveItem(IntPtr pThis, IntPtr pListViewItem);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ItemSelectionStateChanged@MultiListView_c@@AAEXPAVMultiListViewItem_c@@_N@Z")]
		public static extern void ItemSelectionStateChanged(IntPtr pThis, IntPtr pItem, byte selected);

		public static IntPtr Create(Rect rect, int flags, int unk1, int unk2)
		{
			return Constructor(MSVCR100.New(728), ref rect, flags, unk1, unk2);
		}
	}
	public class Preferences_t
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstanceIfAny@Preferences_t@@SAPAV1@XZ")]
		internal static extern IntPtr GetInstanceIfAny();

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetCharacterPath@Preferences_t@@QAEPBDXZ")]
		public static extern IntPtr GetCharacterPath(IntPtr pThis);
	}
	public class PlayfieldDistrictInfo_t
	{
		[DllImport("GameData.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetDistrictData@PlayfieldDistrictInfo_t@GameData@@QBEPBVDistrictData_t@2@I@Z")]
		public static extern IntPtr GetDistrictData(IntPtr pPlayfieldDistrictInfo, uint unk1);
	}
	public class PetWindowModule_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstance@PetWindowModule_c@@SAPAV1@XZ")]
		public static extern IntPtr GetInstance();

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetPetListMap@PetWindowModule_c@@AAEPAV?$map@HVIdentity_t@@U?$less@H@std@@V?$allocator@U?$pair@$$CBHVIdentity_t@@@std@@@3@@std@@XZ")]
		public static extern IntPtr GetPetListMap(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetPetID@PetWindowModule_c@@QAE?AVIdentity_t@@H@Z")]
		public static extern IntPtr GetPetID(IntPtr pThis, ref Identity identity, byte idx);
	}
	public class InputConfig_t
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstance@InputConfig_t@@SAPAV1@XZ")]
		public static extern IntPtr GetInstance();

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetCurrentTarget@InputConfig_t@@QBE?AVIdentity_t@@XZ")]
		public static extern IntPtr GetCurrentTarget(IntPtr pThis, ref Identity identity);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetMouseWorldPosition@InputConfig_t@@QBE?AVVector3_t@@XZ")]
		public static extern IntPtr GetMouseWorldPosition(IntPtr pThis, ref Vector3 pos);
	}
	public class GUIUnk
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.U1)]
		public delegate bool LoadViewFromXmlDelegate(out IntPtr pView, IntPtr pPathStr, IntPtr pUnkStr);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate IntPtr UploadMissionToMapDelegate(ref Identity identity);

		public static LoadViewFromXmlDelegate LoadViewFromXml;

		public static UploadMissionToMapDelegate UploadMissionToMap;
	}
	public class RadioButtonGroup_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0RadioButtonGroup_c@@QAE@ABVRect@@ABVString@@HII@Z")]
		internal static extern IntPtr Constructor(IntPtr pThis, IntPtr pName, int unk1, uint unk2, uint unk3);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetValue@RadioButtonGroup_c@@UBE?AVVariant@@XZ")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern IntPtr GetState(IntPtr pThis, IntPtr pVariant);

		public static IntPtr Create(string name, int unk1, uint unk2, uint unk3)
		{
			IntPtr intPtr = MSVCR100.New(592);
			StdString stdString = StdString.Create(name);
			return Constructor(intPtr, stdString.Pointer, unk1, (uint)(int)intPtr, (uint)(int)intPtr);
		}
	}
	public class RadioButton_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0RadioButton_c@@QAE@ABVRect@@ABVString@@1HII@Z")]
		internal static extern IntPtr Constructor(IntPtr pThis, IntPtr pName, IntPtr pLabel, int unk1, uint unk2, uint unk3);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1RadioButton_c@@UAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetState@RadioButton_c@@QBE_NXZ")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool GetState(IntPtr pThis);

		public static IntPtr Create(string name, string labelText, int unk1, uint unk2, uint unk3)
		{
			StdString stdString = StdString.Create(name);
			StdString stdString2 = StdString.Create(labelText);
			return Constructor(MSVCR100.New(360), stdString.Pointer, stdString2.Pointer, unk1, unk2, unk3);
		}
	}
	public class CommandInterpreter_c
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate IntPtr DGetCommand(IntPtr pThis, IntPtr pCmdText, bool unk);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate byte DProcessChatInput(IntPtr pThis, IntPtr pWindow, IntPtr pCmdText);

		public static DGetCommand GetCommand;

		public static DProcessChatInput ProcessChatInput;
	}
	public class BorderView_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0BorderView_c@@QAE@ABVRect@@ABV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@II@Z")]
		internal unsafe static extern IntPtr Constructor(IntPtr pThis, Rect* rect, IntPtr pName, int unk1, int unk2);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1BorderView_c@@UAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetClient@BorderView_c@@QAEXPAVView@@MMMM@Z")]
		public static extern void SetClient(IntPtr pThis, IntPtr pView, float x1, float y1, float x2, float y2);

		public unsafe static IntPtr Create(Rect rect, string name, int unk1, int unk2)
		{
			StdString stdString = StdString.Create(name);
			return Constructor(MSVCR100.New(424), &rect, stdString.Pointer, unk1, unk2);
		}
	}
	public class ChatGUIModule_t
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate void DHandleGroupAction(IntPtr pThis, IntPtr pGroupMessage);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstanceIfAny@ChatGUIModule_c@@SAPAV1@XZ")]
		public static extern IntPtr GetInstance();

		[DllImport("GUI.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?ExpandChatTextArgs@ChatGUIModule_c@@SA?AV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@ABV23@@Z")]
		public static extern IntPtr ExpandChatTextArgs(IntPtr pOut, IntPtr pMsg);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?HandleGroupMessage@ChatGUIModule_c@@AAEXPBUGroupMessage_t@Client_c@ppj@@@Z")]
		public static extern void HandleGroupMessage(IntPtr pThis, IntPtr pGroupMessage);
	}
	public class ChatWindowNode_t
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate int AppendTextDelegate(IntPtr pThis, IntPtr pMsg, ChatColor color);

		public static IntPtr ChatWindowController = Kernel32.GetProcAddress(Kernel32.GetModuleHandle("GUI.dll"), "?s_pcInstance@ChatGUIModule_c@@0PAV1@A") + 28;

		public static AppendTextDelegate AppendText;
	}
	public class CheckBox_c
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate void DSlotButtonToggled(IntPtr pThis, bool enabled);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0CheckBox_c@@QAE@ABVString@@0_N1@Z")]
		internal static extern IntPtr Constructor(IntPtr pThis, IntPtr string1, IntPtr string2, bool defaultValue, bool horizontalSpacer);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1CheckBox_c@@UAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetValue@CheckBox_c@@UBE?AVVariant@@XZ")]
		public static extern IntPtr GetValue(IntPtr pThis, IntPtr unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetValue@CheckBox_c@@UAEXABVVariant@@_N@Z")]
		public static extern void SetValue(IntPtr pThis, IntPtr pVariant, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SlotButtonToggled@CheckBox_c@@AAEX_N@Z")]
		public static extern void SlotButtonToggled(IntPtr pThis, bool enabled);

		public static IntPtr Create(string name, string text, bool defaultValue, bool horizontalSpacer)
		{
			IntPtr pThis = MSVCR100.New(344);
			StdString stdString = StdString.Create(name);
			StdString stdString2 = StdString.Create(text);
			return Constructor(pThis, stdString.Pointer, stdString2.Pointer, defaultValue, horizontalSpacer);
		}
	}
	public class FlowControlModule_t
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate void DTeleportStartedMessage();

		public unsafe static bool* pIsTeleporting = (bool*)(void*)Kernel32.GetProcAddress(Kernel32.GetModuleHandle("GUI.dll"), "?m_isTeleporting@FlowControlModule_t@@2_NA");

		[DllImport("GUI.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?TeleportStartedMessage@FlowControlModule_t@@CAXXZ")]
		public static extern void TeleportStartedMessage();
	}
	public class HLayoutNode_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0HLayoutNode@@QAE@XZ")]
		internal static extern IntPtr Constructor(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1HLayoutNode@@UAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);

		public static IntPtr Create()
		{
			return Constructor(MSVCR100.New(44));
		}
	}
	public class HLayoutSpacer
	{
	}
	public class ListViewBaseItem_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AppendChild@ListViewBaseItem_c@@QAEXPAV1@@Z")]
		public static extern void AppendChild(IntPtr pThis, IntPtr pItem);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?MakeSelectable@ListViewBaseItem_c@@QAEX_N@Z")]
		public static extern void MakeSelectable(IntPtr pThis, bool selectable);
	}
	public class ListViewBase_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0ListViewBase_c@@QAE@ABVRect@@ABVString@@HII@Z")]
		internal unsafe static extern IntPtr Constructor(IntPtr pThis, Rect* rect, IntPtr pName, int unk1, int unk2, int unk3);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1ListViewBase_c@@UAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AppendItem@ListViewBase_c@@QAEXPAVListViewBaseItem_c@@@Z")]
		public static extern void AppendItem(IntPtr pThis, IntPtr pItem);

		public unsafe static IntPtr Create(Rect rect, string name, int unk1, int unk2, int unk3)
		{
			StdString stdString = StdString.Create(name);
			return Constructor(MSVCR100.New(400), &rect, stdString.Pointer, unk1, unk2, unk3);
		}
	}
	public class OptionPanelModule_c
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate IntPtr GetOptionWindowDelegate(IntPtr pThis);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate void DModuleActivated(IntPtr pThis, bool unk);

		public static GetOptionWindowDelegate GetOptionWindow;

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ModuleActivated@OptionPanelModule_c@@UAEX_N@Z")]
		public static extern void ModuleActivated(IntPtr pThis, bool unk);
	}
	public class ScrollView_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0ScrollView_c@@QAE@ABVRect@@ABVString@@W4ScrollBarMode_e@0@2HII@Z")]
		internal unsafe static extern IntPtr Constructor(IntPtr pThis, Rect* rect, IntPtr pName, int scrollBarModeH, int scrollBarModeV, int unk1, int unk2, int unk3);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1ScrollView_c@@UAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetClient@ScrollView_c@@QAEXPAVView@@0@Z")]
		public static extern void SetClient(IntPtr pThis, IntPtr pView1, IntPtr pView2);

		public unsafe static IntPtr Create(Rect rect, string name, int scrollBarModeH, int scrollBarModeV, int unk1, int unk2, int unk3)
		{
			StdString stdString = StdString.Create(name);
			return Constructor(MSVCR100.New(432), &rect, stdString.Pointer, scrollBarModeH, scrollBarModeV, unk1, unk2, unk3);
		}
	}
	public class SliderView_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetValue@Slider_c@@UBE?AVVariant@@XZ")]
		public static extern IntPtr GetValue(IntPtr pThis, IntPtr pVar);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetValue@Slider_c@@UAEXABVVariant@@_N@Z")]
		public static extern void SetValue(IntPtr pThis, IntPtr pVar, bool unk);
	}
	public class TextInputView_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetText@TextInputView_c@@QAEXABV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@@Z")]
		public static extern void SetText(IntPtr pThis, IntPtr pStr);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetText@TextInputView_c@@QBEABV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@XZ")]
		public static extern IntPtr GetText(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetTextView@TextInputView_c@@QAEPAVTextView_c@@XZ")]
		public static extern IntPtr GetTextView(IntPtr pTextInputView);
	}
	public class StringListViewItem_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0StringListViewItem_c@@QAE@ABVVariant@@ABVString@@HH@Z")]
		internal static extern IntPtr Constructor(IntPtr pThis, IntPtr pVariant, IntPtr pName, int unk1, int unk2);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1StringListViewItem_c@@UAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);

		public static IntPtr Create(Variant variant, string name, int unk1, int unk2)
		{
			StdString stdString = StdString.Create(name);
			return Constructor(MSVCR100.New(152), variant.Pointer, stdString.Pointer, unk1, unk2);
		}
	}
	public class PowerBarView_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetValue@PowerbarView_c@@UAEXABVVariant@@_N@Z")]
		public static extern void SetValue(IntPtr pThis, IntPtr pVariant, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetValue@PowerbarView_c@@UBE?AVVariant@@XZ")]
		public static extern IntPtr GetValue(IntPtr pThis, IntPtr pVariant);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetLabel@PowerbarView_c@@QAEXABVString@@@Z")]
		public static extern void SetLabel(IntPtr pThis, IntPtr text);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetLabels@PowerbarView_c@@QAEXABVString@@0@Z")]
		public static extern void SetLabels(IntPtr pThis, IntPtr left, IntPtr right);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetBarColor@PowerbarView_c@@QAEXI@Z")]
		public static extern void SetBarColor(IntPtr pThis, uint color);
	}
	public class TextView_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetValue@TextView_c@@UAEXABVVariant@@_N@Z")]
		public static extern void SetValue(IntPtr pThis, IntPtr pVariant, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetText@TextView_c@@QAEXABV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@@Z")]
		public static extern void SetText(IntPtr pThis, IntPtr pStr);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetValue@TextView_c@@UBE?AVVariant@@XZ")]
		public static extern IntPtr GetValue(IntPtr pThis, IntPtr pVariant);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetDefaultColor@TextView_c@@QAEXI@Z")]
		public static extern void SetDefaultColor(IntPtr pThis, uint unk);
	}
	public class TabView_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetTabCount@TabView@@QBEHXZ")]
		public static extern int GetTabCount(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AppendTab@TabView@@QAEHABVString@@PAVView@@@Z")]
		public static extern IntPtr AppendTab(IntPtr pThis, IntPtr pName, IntPtr pView);
	}
	public class TargetingModule_t
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstanceIfAny@TargetingModule_t@@SAPAV1@XZ")]
		public static extern IntPtr GetInstanceIfAny();

		[DllImport("GUI.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?SetTarget@TargetingModule_t@@CAXABVIdentity_t@@_N@Z")]
		public static extern void SetTarget(ref Identity target, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SelectSelf@TargetingModule_t@@QAEXXZ")]
		public static extern void SelectSelf(IntPtr pThis);
	}
	public class TeamViewModule_c
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate void DSlotJoinTeamRequest(IntPtr pThis, ref Identity identity, IntPtr pName);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate void DSlotJoinTeamRequestFailed(IntPtr pThis, ref Identity identity);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstanceIfAny@TeamViewModule_c@@SAPAV1@XZ")]
		public static extern IntPtr GetInstanceIfAny();

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?IsTeamLeader@TeamViewModule_c@@QBE_NXZ")]
		public static extern byte IsTeamLeader(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?IsInTeam@TeamViewModule_c@@QBE_NXZ")]
		public static extern byte IsInTeam(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SlotJoinTeamRequest@TeamViewModule_c@@AAEXABVIdentity_t@@ABV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@@Z")]
		public static extern void SlotJoinTeamRequest(IntPtr pThis, ref Identity identity, IntPtr pName);
	}
	public class ToolTip_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0ToolTip_c@@QAE@ABVString@@0@Z")]
		internal static extern IntPtr Constructor(IntPtr pThis, IntPtr string1, IntPtr string2);

		public static IntPtr Create(string string1, string string2)
		{
			IntPtr pThis = MSVCR100.New(116);
			StdString stdString = StdString.Create(string1);
			StdString stdString2 = StdString.Create(string2);
			return Constructor(pThis, stdString.Pointer, stdString2.Pointer);
		}
	}
	public class ViewSelector_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0ViewSelector_c@@QAE@ABVRect@@VString@@HII@Z")]
		internal unsafe static extern IntPtr Constructor(IntPtr pThis, Rect* rect, IntPtr pName, int garbage1, int garbage2, int garbage3, int garbage4, int garbage5, int garbage6, int unk1, int unk2, int unk3);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1ViewSelector_c@@UAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetListView@ViewSelector_c@@QAEXPAVListViewBase_c@@@Z")]
		public static extern void SetListView(IntPtr pThis, IntPtr pListViewBase);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetListView@ViewSelector_c@@QBEPAVListViewBase_c@@XZ")]
		public static extern IntPtr GetListView(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AppendView@ViewSelector_c@@QAEXPAVView@@@Z")]
		public static extern void AppendView(IntPtr pThis, IntPtr pView);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetValue@ViewSelector_c@@UAEXABVVariant@@_N@Z")]
		public static extern void SetValue(IntPtr pThis, IntPtr pVar, bool unk);

		public unsafe static IntPtr Create(Rect rect, string name, int unk1, int unk2, int unk3)
		{
			StdString stdString = StdString.Create(name);
			return Constructor(MSVCR100.New(376), &rect, stdString.Pointer, 0, 0, 0, 0, 0, 0, unk1, unk2, unk3);
		}
	}
	public class View_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0View@@QAE@ABVRect@@ABVString@@II@Z")]
		internal unsafe static extern IntPtr Constructor(IntPtr pThis, Rect* rect, IntPtr pName, int unk1, int unk2);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1View@@UAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AddChild@View@@UAEXPAV1@_N@Z")]
		public static extern void AddChild(IntPtr pThis, IntPtr pView, bool assignTabOrder);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?RemoveChild@View@@QAEXPAV1@@Z")]
		public static extern void RemoveChild(IntPtr pThis, IntPtr pView);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?DeleteAllChildren@View@@QAEXXZ")]
		public static extern void DeleteAllChildren(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?FindChild@View@@QAEPAV1@PBD_N@Z")]
		public static extern IntPtr FindChild(IntPtr pThis, [MarshalAs(UnmanagedType.LPStr)] string name, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetBorders@View@@QAEXMMMM@Z")]
		public static extern void SetBorders(IntPtr pThis, float minX, float minY, float maxX, float maxY);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetPreferredSize@View@@UBE?AVPoint@@_N@Z")]
		public static extern IntPtr GetPreferredSize(IntPtr pThis, ref Vector2 preferredSize, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?CalculatePreferredSize@View@@UBE?AVPoint@@_N@Z")]
		public static extern IntPtr CalculatePreferredSize(IntPtr pThis, ref Vector2 preferredSize, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ResizeTo@View@@UAEXABVPoint@@@Z")]
		public static extern void ResizeTo(IntPtr pThis, ref Vector2 size);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ScaleTo@View@@QAEXABVPoint@@@Z")]
		public static extern void ScaleTo(IntPtr pThis, ref Vector2 scale);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?LimitMaxSize@View@@QAEXABVPoint@@@Z")]
		public static extern void LimitMaxSize(IntPtr pThis, ref Vector2 size);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetFrame@View@@UAEXABVRect@@_N@Z")]
		public unsafe static extern void SetFrame(IntPtr pThis, Rect* rect, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?Show@View@@QAEX_N0@Z")]
		public static extern void Show(IntPtr pThis, bool visible, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetLayoutNode@View@@QAEXPAVLayoutNode@@@Z")]
		public static extern void SetLayoutNode(IntPtr pThis, IntPtr pLayoutNode);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetLocalColor@View@@QAEXI@Z")]
		public static extern void SetLocalColor(IntPtr pThis, uint value);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetColor@View@@QAEXI@Z")]
		public static extern void SetColor(IntPtr pThis, uint value);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetLocalAlpha@View@@QAEXM@Z")]
		public static extern void SetLocalAlpha(IntPtr pThis, float value);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetAlpha@View@@QAEXM@Z")]
		public static extern void SetAlpha(IntPtr pThis, float value);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?Enable@View@@QAEX_N@Z")]
		public static extern void Enable(IntPtr pThis, bool enabled);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?BeginDrag@View@@QAEXPAVDragObject_c@@ABVPoint@@PAV1@@Z")]
		public static extern void BeginDrag(IntPtr pThis, IntPtr pDragObject, ref Vector2 point, IntPtr pView);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?IsEnabled@View@@QBE_NXZ")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsEnabled(IntPtr pThis);

		public unsafe static IntPtr Create(Rect rect, string name, int unk1, int unk2)
		{
			StdString stdString = StdString.Create(name);
			return Constructor(MSVCR100.New(296), &rect, stdString.Pointer, unk1, unk2);
		}
	}
	public class VLayoutNode_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0VLayoutNode@@QAE@XZ")]
		internal static extern IntPtr Constructor(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1VLayoutNode@@UAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);

		public static IntPtr Create()
		{
			return Constructor(MSVCR100.New(44));
		}
	}
	public class InventoryGUIModule_c
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate void DContainerOpened(IntPtr pThis, ref Identity identity, bool unk, bool unk2);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstanceIfAny@InventoryGUIModule_c@@SAPAV1@XZ")]
		public static extern IntPtr GetInstance();

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetBackpackName@InventoryGUIModule_c@@QAE?AV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@ABVIdentity_t@@_N@Z")]
		public static extern IntPtr GetBackpackName(IntPtr pThis, IntPtr pStr, ref Identity identity, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetBackpackName@InventoryGUIModule_c@@QAEXABVIdentity_t@@ABV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@_N@Z")]
		public static extern int SetBackpackName(IntPtr pThis, ref Identity identity, IntPtr pStr, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SlotContainerOpened@InventoryGUIModule_c@@AAEXABVIdentity_t@@_N1@Z")]
		public static extern void ContainerOpened(IntPtr pThis, ref Identity identity, bool unk, bool unk2);
	}
	public class WindowController_c
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate void DViewDeleted(IntPtr pThis, IntPtr pView);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate void DRemoveWindow(IntPtr pThis, IntPtr pWindow);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstanceIfAny@WindowController_c@@SAPAV1@XZ")]
		public static extern IntPtr GetInstance();

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ViewDeleted@WindowController_c@@QAEXPAVView@@@Z")]
		public static extern void ViewDeleted(IntPtr pThis, IntPtr pView);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?RemoveWindow@WindowController_c@@QAEXPAVWindow@@@Z")]
		public static extern void RemoveWindow(IntPtr pThis, IntPtr pWindow);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetActiveWindow@WindowController_c@@QAEPAVWindow@@XZ")]
		public static extern IntPtr GetActiveWindow(IntPtr pThis);
	}
	public class Window_c
	{
		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0Window@@QAE@ABVRect@@ABVString@@1W4WindowStyle_e@@I@Z")]
		internal static extern IntPtr Constructor(IntPtr pThis, ref Rect rect, IntPtr pNameStr, IntPtr pTitleStr, WindowStyle windowStyle, WindowFlags flags);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1Window@@UAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?Show@Window@@QAEX_N@Z")]
		public static extern void Show(IntPtr pThis, bool visible);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?Close@Window@@QAEXXZ")]
		public static extern void Close(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?MoveToCenter@Window@@QAEXXZ")]
		public static extern void MoveToCenter(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?MoveTo@Window@@QAEXMM@Z")]
		public static extern void MoveTo(IntPtr pThis, float x, float y);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetMousePosition@Window@@QBE?AVPoint@@XZ")]
		public static extern IntPtr GetMousePos(IntPtr pThis, ref Vector2 refPos);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetFrame@Window@@QBE?AVRect@@_N@Z")]
		public static extern IntPtr GetFrame(IntPtr pThis, IntPtr pRect);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetScreenSize@Window@@SA?AVPoint@@XZ")]
		public static extern IntPtr GetScreenSize(IntPtr pThis, ref Vector2 refPos);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetTabView@Window@@QBEPAVTabView@@XZ")]
		public static extern IntPtr GetTabView(IntPtr pThis);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetBounds@Window@@QBE?AVRect@@XZ")]
		public static extern IntPtr GetBounds(IntPtr pThis, IntPtr pRect);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AppendTab@Window@@QAEHABVString@@PAVView@@@Z")]
		public static extern IntPtr AppendTab(IntPtr pThis, IntPtr pName, IntPtr pView);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AddChild@Window@@QAEXPAVView@@_N@Z")]
		public static extern IntPtr AppendChild(IntPtr pThis, IntPtr pView, bool unk);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetTitle@Window@@QAEXABVString@@@Z")]
		public static extern void SetTitle(IntPtr pThis, IntPtr pTitle);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ResizeTo@Window@@QAEXABVPoint@@@Z")]
		public static extern void ResizeTo(IntPtr pThis, ref Vector2 size);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetSizeLimits@Window@@QAEXABVPoint@@0@Z")]
		public static extern void SetSizeLimits(IntPtr pThis, ref Vector2 minSize, ref Vector2 maxSize);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?FindView@Window@@QBEPAVView@@ABVString@@@Z")]
		public static extern IntPtr FindView(IntPtr pThis, IntPtr viewName);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?FindWindowName@Window@@SAPAV1@PBD@Z")]
		public static extern IntPtr FindWindowName([MarshalAs(UnmanagedType.LPStr)] string windowName);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetAlpha@Window@@QAEXM@Z")]
		public static extern void SetAlpha(IntPtr pThis, float value);

		[DllImport("GUI.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?IsVisible@Window@@QBE_NXZ")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsVisible(IntPtr pThis);

		public static IntPtr Create(Rect rect, string string1, string string2, WindowStyle style, WindowFlags flags)
		{
			StdString stdString = StdString.Create(string1);
			StdString stdString2 = StdString.Create(string2);
			return Constructor(MSVCR100.New(172), ref rect, stdString.Pointer, stdString2.Pointer, style, flags);
		}
	}
	public class GuiResourceManager_t
	{
		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstance@GuiResourceManager_t@@SAPAV1@XZ")]
		internal static extern IntPtr GetInstance();

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetGuiTexture@GuiResourceManager_t@@QAEPAVSpriteInfo_t@@HPBDW4Format_e@@II@Z")]
		internal static extern IntPtr GetGuiTexture(IntPtr pThis, int gfxId, [MarshalAs(UnmanagedType.LPStr)] string file, int format, int unk1, int unk2);

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ReleaseTexture@GuiResourceManager_t@@QAEXPAVSpriteInfo_t@@@Z")]
		internal static extern void ReleaseTexture(IntPtr pThis, IntPtr pSprite);
	}
	public class Client_t
	{
		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstanceIfAny@Client_t@@SAPAV1@XZ")]
		public static extern IntPtr GetInstanceIfAny();

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SendVicinityMessage@Client_t@@QAEXPADIABVIdentity_t@@@Z")]
		public static extern void SendVicinityMessage(IntPtr pThis, [MarshalAs(UnmanagedType.LPStr)] string message, int length, ref Identity unk);

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetCookies@Client_t@@SAXAAI0@Z")]
		public static extern void GetCookies(ref uint cookie1, ref uint cookie2);

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetServerID@Client_t@@QBEIXZ")]
		public static extern int GetServerID(IntPtr pThis);
	}
	public class N3InterfaceModule_t
	{
		public unsafe delegate void DCastNanoSpell(IntPtr pThis, Identity* nanoIdentity, Identity targetIdentity);

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstance@N3InterfaceModule_t@@SAPAV1@XZ")]
		public static extern IntPtr GetInstance();

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetClientInst@N3InterfaceModule_t@@QBEIXZ")]
		public static extern int GetClientInst();

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?ShutdownMessage@N3InterfaceModule_t@@CAXXZ")]
		public static extern void ShutdownMessage();

		public static IntPtr GetPFName(int pfId)
		{
			return GetPFName(GetInstance(), pfId);
		}

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetPFName@N3InterfaceModule_t@@QBEPBDI@Z")]
		public static extern IntPtr GetPFName(IntPtr pThis, int pfId);

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_CastNanoSpell@N3InterfaceModule_t@@QBEXABVIdentity_t@@0@Z")]
		public unsafe static extern void CastNanoSpell(IntPtr pThis, Identity* nano, Identity target);

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetPerkProgress@N3InterfaceModule_t@@QBEMI@Z")]
		public static extern float GetPerkProgress(IntPtr pThis, uint perkId);

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetDesc@N3InterfaceModule_t@@QBEPBDABVIdentity_t@@0@Z")]
		public static extern IntPtr GetDesc(IntPtr pThis, ref Identity target, int unk);

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_GetCompletedPersonalResearchGoals@N3InterfaceModule_t@@QAEXAAV?$vector@IV?$allocator@I@std@@@std@@@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool GetCompletedPersonalResearchGoals(IntPtr pThis, ref StdStructVector vector);

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?N3Msg_PersonalResearchGoals@N3InterfaceModule_t@@QAEXAAV?$vector@U?$pair@I_N@std@@V?$allocator@U?$pair@I_N@std@@@2@@std@@@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool GetPersonalResearchGoals(IntPtr pThis, ref StdStructVector vector);

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?SetCharID@Client_t@@SAXI@Z")]
		public static extern void SetCharID(int charId);

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetCharID@Client_t@@SAIXZ")]
		public static extern int GetCharID();

		[DllImport("Interfaces.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ProcessMessage@Client_t@@AAEHPAVMessage_t@@@Z")]
		public static extern IntPtr ProcessMessage(IntPtr pThis, IntPtr pMsg);
	}
	public class RemoteFormat
	{
		[DllImport("ldb.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?ParseString@RemoteFormat@@SA?AV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@PBD@Z")]
		public static extern IntPtr ParseString(IntPtr pString, [MarshalAs(UnmanagedType.LPStr)] string remoteFormat);
	}
	public class LDBFace
	{
		[DllImport("ldb.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetText@LDBface@@SA?AV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@II@Z")]
		public static extern IntPtr GetText(IntPtr pString, int type, int instance);
	}
	public class MessageProtocol
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate IntPtr DDataBlockToMessage(uint size, [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] byte[] dataBlock);

		[DllImport("MessageProtocol.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?DataBlockToMessage@@YAPAVMessage_t@@IPAX@Z")]
		public static extern IntPtr DataBlockToMessage(uint size, [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] byte[] dataBlock);
	}
	public class N3DatabaseHandler_t
	{
		[DllImport("N3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?Initialize@n3DatabaseHandler_t@@SAXXZ")]
		public static extern void Initialize();

		[DllImport("N3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?Get@n3DatabaseHandler_t@@SAAAV1@XZ")]
		public static extern IntPtr Get();

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetResourceDatabase@n3DatabaseHandler_t@@QBEAAVResourceDatabase_t@@XZ")]
		public static extern IntPtr GetResourceDatabase(IntPtr pThis);
	}
	public class N3Dynel_t
	{
		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0n3Dynel_t@@IAE@ABVIdentity_t@@@Z")]
		public static extern IntPtr Constructor(IntPtr pThis, ref Identity identity);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetRelRot@n3Dynel_t@@QAEXABVQuaternion_t@@@Z")]
		public static extern void SetRelRot(IntPtr pThis, ref Quaternion rot);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetZone@n3Dynel_t@@QBEPAVn3Zone_t@@XZ")]
		public static extern IntPtr GetZone(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetGlobalPos@n3Dynel_t@@QBEABVVector3_t@@XZ")]
		public unsafe static extern Vector3* GetGlobalPos(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetCollPrimScale@n3Dynel_t@@IAEXM@Z")]
		public static extern void SetCollPrimScale(IntPtr pThis, float radius);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetVehicle@n3Dynel_t@@QAEXPAVVehicle_t@@@Z")]
		public static extern void SetVehicle(IntPtr pThis, IntPtr pVehicle);
	}
	public class N3EngineClient_t
	{
		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?OpenClient@n3EngineClient_t@@QAEXPAVResourceDatabase_t@@I@Z")]
		public static extern void OpenClient(IntPtr pThis, IntPtr pResourceDatabase, int clientInst);

		[DllImport("N3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetPlayfield@n3EngineClient_t@@SAPAVn3Playfield_t@@XZ")]
		public static extern IntPtr GetPlayfield();

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetActiveCamera@n3EngineClient_t@@QBEPAVn3Camera_t@@XZ")]
		public static extern IntPtr GetActiveCamera(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetClientControlDynel@n3EngineClient_t@@QBEPAVn3VisualDynel_t@@XZ")]
		public static extern IntPtr GetClientControlDynel(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetClientInst@n3EngineClient_t@@QBEIXZ")]
		public static extern int GetClientInst(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SendIIRToServer@n3EngineClient_t@@QBEXABVn3InfoItemRemote_t@@@Z")]
		public static extern void SendIIRToServer(IntPtr pThis, IntPtr pIIR);
	}
	public class N3Camera_t
	{
		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?IsFirstPerson@n3Camera_t@@QBE_NXZ")]
		public static extern bool IsFirstPerson(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?StartZoomOut@n3Camera_t@@QAEXXZ")]
		public static extern void StartZoomOut(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?StopZoomOut@n3Camera_t@@QAEXXZ")]
		public static extern void StopZoomOut(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?StartZoomIn@n3Camera_t@@QAEXXZ")]
		public static extern void StartZoomIn(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?StopZoomIn@n3Camera_t@@QAEXXZ")]
		public static extern void StopZoomIn(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ToggleCameraView@n3Camera_t@@QAEXXZ")]
		public static extern void ToggleCameraView(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetTargetGlobalPos@CameraVehicle_t@@QBEABVVector3_t@@XZ")]
		public static extern Vector3 GetTargetGlobalPos();
	}
	public class N3Root_t
	{
		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AddPlayfieldRoot@n3Root_t@@QAEXPAVn3Playfield_t@@@Z")]
		public static extern void AddPlayfieldRoot(IntPtr pThis, IntPtr pPlayfield);
	}
	public class N3Engine_t
	{
		[DllImport("N3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetInstance@n3Engine_t@@SAPAV1@XZ")]
		public static extern IntPtr GetInstance();

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetRoot@n3Engine_t@@QAEAAVn3Root_t@@XZ")]
		public static extern IntPtr GetRoot(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?Close@n3Engine_t@@QAEXXZ")]
		public static extern void Close(IntPtr pThis);
	}
	public class N3InfoItemRemote_t
	{
		[DllImport("N3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?KeyToString@n3InfoItemRemote_t@@SAABV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@J@Z")]
		public static extern IntPtr KeyToString(int key);
	}
	public class N3Playfield_t
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate void DAddChildDynel(IntPtr pThis, IntPtr pDynel, IntPtr pos, IntPtr rot);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?LineOfSight@n3Playfield_t@@QBE_NABVVector3_t@@0H_N@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public unsafe static extern bool LineOfSight(IntPtr pThis, Vector3* pos1, Vector3* pos2, int zoneCell, bool unknown);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AddChildDynel@n3Playfield_t@@QAEXPAVn3Dynel_t@@ABVVector3_t@@ABVQuaternion_t@@@Z")]
		public static extern void AddChildDynel(IntPtr pThis, IntPtr pDynel, IntPtr pos, IntPtr rot);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?IsDungeon@n3Playfield_t@@QBE_NXZ")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsDungeon(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?IsBattleStation@n3Playfield_t@@QBE_NXZ")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsBattleStation(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, CharSet = CharSet.Ansi, EntryPoint = "?GetName@n3Playfield_t@@UBEPBDXZ")]
		public static extern IntPtr GetName(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetIdentity@n3Playfield_t@@QBEABVIdentity_t@@XZ")]
		public unsafe static extern Identity* GetIdentity(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetModelID@n3Playfield_t@@QBEABVIdentity_t@@XZ")]
		public unsafe static extern Identity* GetModelID(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, CharSet = CharSet.Ansi, EntryPoint = "?GetTilemap@n3Playfield_t@@QBEPBVn3Tilemap_t@@XZ")]
		public static extern IntPtr GetTilemap(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, CharSet = CharSet.Ansi, EntryPoint = "?GetSurface@n3Playfield_t@@QBEPBVSurface_i@@XZ")]
		public static extern IntPtr GetSurface(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, CharSet = CharSet.Ansi, EntryPoint = "?GetZone@n3Playfield_t@@QAEPAVn3Zone_t@@H@Z")]
		public static extern IntPtr GetZone(IntPtr pThis, int id);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, CharSet = CharSet.Ansi, EntryPoint = "?GetZones@n3Playfield_t@@QBEABV?$vector@PAVn3Zone_t@@V?$allocator@PAVn3Zone_t@@@std@@@std@@XZ")]
		public unsafe static extern StdObjVector* GetZones(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?IsDoorOpenBetweenRooms@n3Playfield_t@@QBE_NFF@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsDoorOpenBetweenRooms(IntPtr pThis, short roomId1, short roomId2);
	}
	public class N3Room_t
	{
		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetRoomRect@n3Room_t@@QBEXAAM000@Z")]
		public static extern void GetRoomRect(IntPtr pThis, out float x, out float x2, out float y, out float y2);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetCenter@n3Room_t@@QBEABVVector3_t@@XZ")]
		public unsafe static extern Vector3* GetCenter(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetPos@n3Room_t@@QBEABVVector3_t@@XZ")]
		public unsafe static extern Vector3* GetPos(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetRot@n3Room_t@@QBEHXZ")]
		public static extern int GetRot(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetName@n3Room_t@@QBEPBDXZ")]
		public static extern IntPtr GetName(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetFloor@n3Room_t@@QBEHXZ")]
		public static extern int GetFloor(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetNumDoors@n3Room_t@@QBEHXZ")]
		public static extern int GetNumDoors(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetDoorPosRot@n3Room_t@@QAEXHABVn3Tilemap_t@@AAVVector3_t@@AAVQuaternion_t@@@Z")]
		public static extern void GetDoorPosRot(IntPtr pThis, int doorIdx, IntPtr pTilemap, out Vector3 pos, out Quaternion rot);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetDoorConnectZone@n3Room_t@@QBEHH@Z")]
		public static extern int GetDoorConnectZone(IntPtr pThis, int doorIdx);
	}
	public class N3Zone_t
	{
		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, CharSet = CharSet.Ansi, EntryPoint = "?LoadSurface@n3Zone_t@@QAEXPAVCellSurface_t@@@Z")]
		public static extern void LoadSurface(IntPtr pThis, IntPtr pSurface);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetSurface@n3Zone_t@@QBEPBVSurface_i@@XZ")]
		public static extern IntPtr GetSurface(IntPtr pThis);

		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetInstance@n3Zone_t@@QBEIXZ")]
		public static extern int GetInstance(IntPtr pThis);
	}
	public class RoomSurface_t
	{
		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetLineIntersection@n3RoomSurface_t@@UBE_NABVVector3_t@@0AAV2@1_NPAVLocalitySource_t@@@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool GetLineIntersection(IntPtr pThis, ref Vector3 pos1, ref Vector3 pos2, ref Vector3 hitPos, ref Vector3 hitNormal, byte unk, IntPtr plocalitySource);
	}
	public class TilemapSurface_t
	{
		[DllImport("N3.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetLineIntersection@n3TilemapSurface_t@@UBE_NABVVector3_t@@0AAV2@1_NPAVLocalitySource_t@@@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool GetLineIntersection(IntPtr pThis, ref Vector3 pos1, ref Vector3 pos2, ref Vector3 hitPos, ref Vector3 hitNormal, byte unk, IntPtr plocalitySource);
	}
	public class Ws2_32
	{
		[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
		public delegate int RecvDelegate(int socket, IntPtr buffer, int len, int flags);

		[UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
		public delegate int SendDelegate(int socket, IntPtr buffer, int len, int flags);

		[DllImport("ws2_32.dll")]
		public static extern int recv(int socket, IntPtr buffer, int len, int flags);

		[DllImport("ws2_32.dll", CallingConvention = CallingConvention.StdCall)]
		public static extern int send(int socket, IntPtr buffer, int len, int flags);
	}
	public class Kernel32
	{
		public enum Protection
		{
			PAGE_NOACCESS = 1,
			PAGE_READONLY = 2,
			PAGE_READWRITE = 4,
			PAGE_WRITECOPY = 8,
			PAGE_EXECUTE = 0x10,
			PAGE_EXECUTE_READ = 0x20,
			PAGE_EXECUTE_READWRITE = 0x40,
			PAGE_EXECUTE_WRITECOPY = 0x80,
			PAGE_GUARD = 0x100,
			PAGE_NOCACHE = 0x200,
			PAGE_WRITECOMBINE = 0x400
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		public static extern IntPtr GetModuleHandle(string lpModuleName);

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr GetCurrentProcess();

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, Protection flNewProtect, out Protection lpflOldProtect);

		[DllImport("kernel32.dll")]
		public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

		[DllImport("kernel32.dll")]
		public static extern void CopyMemory(IntPtr destination, IntPtr source, uint length);
	}
	public class MSVCR100
	{
		[DllImport("MSVCR100.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Auto, EntryPoint = "??2@YAPAXI@Z")]
		public static extern IntPtr New(int size);

		[DllImport("MSVCR100.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Auto, EntryPoint = "??3@YAXPAX@Z")]
		public static extern void Delete(IntPtr pointer);
	}
	internal class Psapi
	{
		internal struct MODULEINFO
		{
			public IntPtr lpBaseOfDll;

			public uint SizeOfImage;

			public IntPtr EntryPoint;
		}

		[DllImport("psapi.dll", SetLastError = true)]
		internal static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, int cb);
	}
	public class RCATMesh_t
	{
		[DllImport("Randy31.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetBoneMatrix@RCATMesh_t@@QAE_NPBDAAVTMatrix4_t@@@Z")]
		public static extern IntPtr GetBoneMatrix(IntPtr pThis, [MarshalAs(UnmanagedType.LPStr)] string name, ref Matrix4x4 matrix);
	}
	public class Debugger_t
	{
		[DllImport("Randy31.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?Get@Debugger_t@@SAPAV1@XZ")]
		public static extern IntPtr GetInstance();

		[DllImport("Randy31.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AddLine@Debugger_t@@QAEXVVector3_t@@0MMM@Z")]
		public static extern int DrawLine(IntPtr pThis, float pos1X, float pos1Y, float pos1Z, float pos2X, float pos2Y, float pos2Z, float unk1, float unk2, float unk3);

		[DllImport("Randy31.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AddSphere@Debugger_t@@QAEXVVector3_t@@MMMM@Z")]
		public static extern int DrawSphere(IntPtr pThis, float posX, float posY, float posZ, float radius, float unk1, float unk2, float unk3);
	}
	public class Render_t
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate IntPtr CreateRenderDelegate();

		public static CreateRenderDelegate CreateRender;
	}
	public class Looper
	{
		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetName@Looper@@QBE?AV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@XZ")]
		public static extern IntPtr GetName(IntPtr pLooper, IntPtr pName);
	}
	public class Rect_c
	{
		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0Rect@@QAE@XZ")]
		internal static extern IntPtr Constructor(IntPtr pThis);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1Rect@@QAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);

		public static IntPtr Create()
		{
			return Constructor(MSVCR100.New(16));
		}
	}
	public class String_c
	{
		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0String@@QAE@PBDH@Z")]
		public static extern IntPtr Constructor(IntPtr pThis, byte[] str, int len);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1String@@QAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);
	}
	public class DistributedValue_c
	{
		[DllImport("Utils.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?AddVariable@DistributedValue_c@@SAXABVString@@ABVVariant@@_N2@Z")]
		internal static extern void AddVariable(IntPtr pName, IntPtr pVariant, bool unk1, bool unk2);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetDValue@DistributedValue_c@@SA?AVVariant@@ABVString@@_N@Z")]
		public static extern IntPtr GetDValue(IntPtr pVariant, IntPtr pName, bool unk);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?SetDValue@DistributedValue_c@@SAXABVString@@ABVVariant@@@Z")]
		public static extern void SetDValue(IntPtr pName, IntPtr pValue);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?SaveConfig@DistributedValue_c@@SA_NABV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@W4DValueCategory_e@@@Z")]
		public static extern int SaveConfig(IntPtr path, int category);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?LoadConfig@DistributedValue_c@@SA_NABV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@W4DValueCategory_e@@_N@Z")]
		public static extern int LoadConfig(IntPtr path, int category, bool unk);
	}
	public class Variant_c
	{
		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AsInt32@Variant@@QBEJXZ")]
		public static extern int AsInt32(IntPtr pThis);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AsFloat@Variant@@QBEMXZ")]
		public static extern float AsFloat(IntPtr pThis);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AsDouble@Variant@@QBENXZ")]
		public static extern double AsDouble(IntPtr pThis);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AsBool@Variant@@QBE_NXZ")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool AsBool(IntPtr pThis);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?AsString@Variant@@QBE?AV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@XZ")]
		public static extern IntPtr AsString(IntPtr pThis, IntPtr pStr);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetBool@Variant@@QAEX_N@Z")]
		public static extern void SetBool(IntPtr pThis, bool value);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SaveToString@Variant@@QAE_NAAV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool SaveToString(IntPtr pThis, IntPtr pStr);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?LoadFromString@Variant@@QAE_NPBD@Z")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool LoadFromString(IntPtr pThis, [MarshalAs(UnmanagedType.LPStr)] string value);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0Variant@@QAE@XZ")]
		public static extern IntPtr Constructor(IntPtr pThis);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0Variant@@QAE@H@Z")]
		public static extern IntPtr Constructor(IntPtr pThis, int value);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0Variant@@QAE@M@Z")]
		public static extern IntPtr Constructor(IntPtr pThis, float value);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0Variant@@QAE@N@Z")]
		public static extern IntPtr Constructor(IntPtr pThis, double value);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0Variant@@QAE@_N@Z")]
		public static extern IntPtr Constructor(IntPtr pThis, bool value);

		[DllImport("Utils.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??1Variant@@QAE@XZ")]
		public static extern int Deconstructor(IntPtr pThis);
	}
	public static class XMLObject_c
	{
		[DllImport("Utils.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?LoadXMLObject@XMLObject_c@@SAPAV1@ABVString@@0@Z")]
		public static extern IntPtr LoadXMLObject(IntPtr pPathStr, IntPtr pUnkStr);
	}
	public static class Vehicle_t
	{
		[DllImport("Vehicle.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?EnableFalling@Vehicle_t@@QAEXXZ")]
		public static extern void EnableFalling(IntPtr pThis);

		[DllImport("Vehicle.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetMaxVel@Vehicle_t@@QAEXM@Z")]
		public static extern void SetMaxVel(IntPtr pThis, float maxVel);

		[DllImport("Vehicle.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?SetBody@Vehicle_t@@QAEXPAVVehicleBody_i@@@Z")]
		public static extern void SetBody(IntPtr pThis, IntPtr pBody);
	}
}
namespace AOSharp.Common.Unmanaged.Imports.GameData
{
	public class LandControlMap_t
	{
		[DllImport("GameData.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetBitmapData@LandControlMap_t@GameData@@QBEPBXXZ")]
		public static extern IntPtr GetBitmapData(IntPtr pLandControlMap);

		[DllImport("GameData.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?IsTilePlaceAble@LandControlMap_t@GameData@@IBE_NHH@Z")]
		public static extern bool IsTilePlaceAble(IntPtr pLandControlMap, int x, int z);

		[DllImport("GameData.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?CanPlaceInTileAt@LandControlMap_t@GameData@@QBE_NABVVector3_t@@@Z")]
		public static extern bool CanPlaceInTileAt(IntPtr pLandControlMap, ref Vector3 pos);

		[DllImport("GameData.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?IsTileAtAllowedBorder@LandControlMap_t@GameData@@QBE_NABVVector3_t@@@Z")]
		public static extern bool IsTileAtAllowedBorder(IntPtr pLandControlMap, ref Vector3 pos);
	}
}
namespace AOSharp.Common.Unmanaged.Imports.Gamecode
{
	public class GamecodeVtblDelegates
	{
		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate int GetStatDelegate(IntPtr pThis, Stat stat, int detail);

		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		[return: MarshalAs(UnmanagedType.U1)]
		public delegate bool IsLockedDelegate(IntPtr pThis);
	}
}
namespace AOSharp.Common.Unmanaged.Imports.DatabaseController
{
	public class DatabaseController_t
	{
		[DllImport("DatabaseController.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ErrNo@DatabaseController_t@@UAEHXZ")]
		public static extern int ErrorNo(IntPtr pThis);

		[DllImport("DatabaseController.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?ErrorStr@DatabaseController_t@@UAEPBDXZ")]
		public static extern IntPtr ErrorStr(IntPtr pThis);
	}
	public class ResourceDatabase_t
	{
		[DllImport("DatabaseController.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "??0ResourceDatabase_t@@QAE@XZ")]
		public static extern IntPtr Constructor(IntPtr pThis);

		[DllImport("DatabaseController.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?Open@ResourceDatabase_t@@QAEHABV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@_N@Z")]
		public static extern int Open(IntPtr pThis, IntPtr pPath, bool readOnly);

		[DllImport("DatabaseController.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?GetDbObject@ResourceDatabase_t@@UAEPAVDbObject_t@@ABVIdentity_t@@@Z")]
		public static extern IntPtr GetDbObject(IntPtr pThis, ref DBIdentity identity);

		[DllImport("DatabaseController.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?PutDbBlob@ResourceDatabase_t@@QAEXABVIdentity_t@@PBXI@Z")]
		public static extern void PutDbBlob(IntPtr pThis, ref DBIdentity identity, [MarshalAs(UnmanagedType.LPArray)] byte[] data, int size);

		[DllImport("DatabaseController.dll", CallingConvention = CallingConvention.ThisCall, EntryPoint = "?PutDbObject@ResourceDatabase_t@@UAEXPAVDbObject_t@@@Z")]
		public static extern void PutDbObject(IntPtr pThis, IntPtr pDBObject);
	}
}
namespace AOSharp.Common.Unmanaged.DbObjects
{
	public abstract class DbObject
	{
		public IntPtr Pointer { get; }

		protected DbObject(IntPtr pointer)
		{
			Pointer = pointer;
		}
	}
	public class DistrictData : DbObject
	{
		public DistrictData(IntPtr pointer)
			: base(pointer)
		{
		}

		public FightMode GetFightMode()
		{
			return (FightMode)DistrictData_t.GetFightMode(base.Pointer);
		}

		public bool IsLandControlled()
		{
			return DistrictData_t.IsLandControlled(base.Pointer);
		}
	}
	public class LandControlMap : DbObject
	{
		public LandControlMapMemStruct MemStruct;

		public unsafe LandControlMap(IntPtr pointer)
			: base(pointer)
		{
			MemStruct = *(LandControlMapMemStruct*)(void*)pointer;
		}

		public static LandControlMap Get(int playfieldId)
		{
			DBIdentity identity = new DBIdentity(DBIdentityType.LandControlMap, playfieldId);
			return ResourceDatabase.GetDbObject<LandControlMap>(identity);
		}

		public byte[] GetBitmapData()
		{
			IntPtr bitmapData = LandControlMap_t.GetBitmapData(base.Pointer);
			if (bitmapData == IntPtr.Zero)
			{
				return Array.Empty<byte>();
			}
			int num = MemStruct.NumTilesX * MemStruct.NumTilesZ / 8;
			byte[] array = new byte[num];
			Marshal.Copy(bitmapData, array, 0, num);
			return array;
		}

		public bool IsTilePlaceAble(int x, int z)
		{
			return LandControlMap_t.IsTilePlaceAble(base.Pointer, x, z);
		}

		public bool CanPlaceInTileAt(Vector3 pos)
		{
			return LandControlMap_t.CanPlaceInTileAt(base.Pointer, ref pos);
		}

		public bool IsTileAtAllowedBorder(Vector3 pos)
		{
			return LandControlMap_t.IsTileAtAllowedBorder(base.Pointer, ref pos);
		}
	}
	public struct LandControlMapMemStruct
	{
		public int Offset0 { get; set; }

		public int Offset4 { get; set; }

		public Identity Identity { get; set; }

		public int Offset10 { get; set; }

		public int Offset14 { get; set; }

		public int Version { get; set; }

		public int NumTilesX { get; set; }

		public int NumTilesZ { get; set; }

		public int Offset24 { get; set; }
	}
	public class PlayfieldDistrictInfo : DbObject
	{
		public PlayfieldDistrictInfoMemStruct MemStruct;

		public int ZoneCount;

		public static PlayfieldDistrictInfo Get(int instance)
		{
			DBIdentity identity = new DBIdentity(DBIdentityType.PlayfieldDistrictInfo, instance);
			return ResourceDatabase.GetDbObject<PlayfieldDistrictInfo>(identity);
		}

		internal unsafe PlayfieldDistrictInfo(IntPtr pointer)
			: base(pointer)
		{
			MemStruct = *(PlayfieldDistrictInfoMemStruct*)(void*)pointer;
			ZoneCount = (MemStruct.ZoneToDistrictMapLast - MemStruct.ZoneToDistrictMapFirst) / 2;
		}

		public DistrictData GetDistrictData(uint zone)
		{
			return new DistrictData(PlayfieldDistrictInfo_t.GetDistrictData(base.Pointer, zone));
		}
	}
	public struct PlayfieldDistrictInfoMemStruct
	{
		public int Offset0 { get; set; }

		public int Offset4 { get; set; }

		public int Offset8 { get; set; }

		public int OffsetC { get; set; }

		public int Offset10 { get; set; }

		public int Offset14 { get; set; }

		public int Offset18 { get; set; }

		public int Offset1C { get; set; }

		public int Offset20 { get; set; }

		public int Offset24 { get; set; }

		public int ZoneToDistrictMapFirst { get; set; }

		public int ZoneToDistrictMapLast { get; set; }

		public int Offset30 { get; set; }

		public int Offset34 { get; set; }
	}
	public class DungeonRDBTilemap : RDBTilemap
	{
		public byte[,] Heightmap;

		public byte[,] CollisionData;

		public unsafe IntPtr DungeonHeightmapPtr => ((RDBTilemapMemStruct*)(void*)base.Pointer)->DungeonHeightmapPtr;

		public unsafe IntPtr CollisionDataPtr => ((RDBTilemapMemStruct*)(void*)base.Pointer)->CollisionDataPtr;

		internal DungeonRDBTilemap(IntPtr pointer)
			: base(pointer)
		{
			Parse();
		}

		public static DungeonRDBTilemap Get(int id)
		{
			DBIdentity identity = new DBIdentity(DBIdentityType.RDBTilemap, id);
			return ResourceDatabase.GetDbObject<DungeonRDBTilemap>(identity);
		}

		public static DungeonRDBTilemap FromPointer(IntPtr pointer)
		{
			return new DungeonRDBTilemap(pointer);
		}

		protected unsafe override void Parse()
		{
			using (UnmanagedMemoryStream input = new UnmanagedMemoryStream((byte*)DungeonHeightmapPtr.ToPointer(), base.Width * base.Height))
			{
				using BinaryReader binaryReader = new BinaryReader(input);
				Heightmap = new byte[base.Width, base.Height];
				for (int i = 0; i < base.Height; i++)
				{
					for (int j = 0; j < base.Width; j++)
					{
						Heightmap[j, i] = binaryReader.ReadByte();
					}
				}
			}
			using UnmanagedMemoryStream input2 = new UnmanagedMemoryStream((byte*)CollisionDataPtr.ToPointer(), base.Width * base.Height);
			using BinaryReader binaryReader2 = new BinaryReader(input2);
			CollisionData = new byte[base.Width, base.Height];
			for (int k = 0; k < base.Height; k++)
			{
				for (int l = 0; l < base.Width; l++)
				{
					CollisionData[l, k] = binaryReader2.ReadByte();
				}
			}
		}
	}
	public class OutdoorRDBTilemap : RDBTilemap
	{
		public class Chunk
		{
			public int X;

			public int Y;

			public int Size;

			public ushort[,] Heightmap;
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct AnarchyGroundDataMemStruct
		{
			[FieldOffset(52)]
			public int Width;

			[FieldOffset(56)]
			public int Height;

			[FieldOffset(60)]
			public int Modulo;

			[FieldOffset(80)]
			public int NumChunksX;

			[FieldOffset(84)]
			public int NumChunksZ;

			[FieldOffset(88)]
			public IntPtr ChunkDataPtr;
		}

		public List<Chunk> Chunks;

		public int NumChunksX => GroundData.NumChunksX;

		public int NumChunksZ => GroundData.NumChunksZ;

		public int Modulo => GroundData.Modulo;

		private unsafe IntPtr GroundDataPtr => ((RDBTilemapMemStruct*)(void*)base.Pointer)->GroundDataPtr;

		private unsafe AnarchyGroundDataMemStruct GroundData => *(AnarchyGroundDataMemStruct*)(void*)GroundDataPtr;

		internal OutdoorRDBTilemap(IntPtr pointer)
			: base(pointer)
		{
			Parse();
		}

		public static OutdoorRDBTilemap Get(int id)
		{
			DBIdentity identity = new DBIdentity(DBIdentityType.RDBTilemap, id);
			return ResourceDatabase.GetDbObject<OutdoorRDBTilemap>(identity);
		}

		public static OutdoorRDBTilemap FromPointer(IntPtr pointer)
		{
			return new OutdoorRDBTilemap(pointer);
		}

		protected unsafe override void Parse()
		{
			Chunks = new List<Chunk>();
			int num = NumChunksX * NumChunksZ;
			using UnmanagedMemoryStream input = new UnmanagedMemoryStream((byte*)GroundData.ChunkDataPtr.ToPointer(), 84 * num);
			using BinaryReader binaryReader = new BinaryReader(input);
			for (int i = 0; i < num; i++)
			{
				int x = binaryReader.ReadInt32();
				int y = binaryReader.ReadInt32();
				binaryReader.ReadInt32();
				int num2 = binaryReader.ReadInt32() + 1;
				byte[] array = DecompressArray(binaryReader.ReadInt32(), (IntPtr)binaryReader.ReadInt32());
				ushort[,] heightmap = ((Math.Sqrt(array.Length) != Math.Truncate(Math.Sqrt(array.Length))) ? UnfilterShortHeightmap(array, num2) : UnfilterHeightmap(array, num2));
				binaryReader.ReadBytes(60);
				Chunks.Add(new Chunk
				{
					X = x,
					Y = y,
					Size = num2,
					Heightmap = heightmap
				});
			}
		}

		private unsafe byte[] DecompressArray(int size, IntPtr pArray)
		{
			using UnmanagedMemoryStream stream = new UnmanagedMemoryStream((byte*)pArray.ToPointer(), size);
			using DeflateStream deflateStream = new DeflateStream(stream, CompressionMode.Decompress);
			using MemoryStream memoryStream = new MemoryStream();
			deflateStream.BaseStream.ReadByte();
			deflateStream.BaseStream.ReadByte();
			deflateStream.CopyTo(memoryStream);
			return memoryStream.ToArray();
		}
	}
	public class RDBTilemap : DbObject
	{
		[StructLayout(LayoutKind.Explicit)]
		protected struct RDBTilemapMemStruct
		{
			[FieldOffset(24)]
			public bool IsDungeon;

			[FieldOffset(28)]
			public float HeightmapScale;

			[FieldOffset(32)]
			public IntPtr CollisionDataPtr;

			[FieldOffset(552)]
			public IntPtr DungeonHeightmapPtr;

			[FieldOffset(560)]
			public short NumMainTiles;

			[FieldOffset(564)]
			public IntPtr MainTileIds;

			[FieldOffset(33368)]
			public IntPtr GroundDataPtr;

			[FieldOffset(33372)]
			public int Width;

			[FieldOffset(33376)]
			public int Height;

			[FieldOffset(33380)]
			public float TileSize;
		}

		public unsafe bool IsDungeon => ((RDBTilemapMemStruct*)(void*)base.Pointer)->IsDungeon;

		public unsafe float HeightmapScale => ((RDBTilemapMemStruct*)(void*)base.Pointer)->HeightmapScale;

		public unsafe short NumMainTiles => ((RDBTilemapMemStruct*)(void*)base.Pointer)->NumMainTiles;

		public unsafe IntPtr MainTileIds => ((RDBTilemapMemStruct*)(void*)base.Pointer)->MainTileIds;

		public unsafe int Width => ((RDBTilemapMemStruct*)(void*)base.Pointer)->Width;

		public unsafe int Height => ((RDBTilemapMemStruct*)(void*)base.Pointer)->Height;

		public unsafe float TileSize => ((RDBTilemapMemStruct*)(void*)base.Pointer)->TileSize;

		protected RDBTilemap(IntPtr pointer)
			: base(pointer)
		{
			Parse();
		}

		protected virtual void Parse()
		{
		}

		private ushort[] GetShortHeights(BinaryReader reader, int numHeights)
		{
			ushort[] array = new ushort[numHeights];
			for (int i = 0; i < numHeights; i++)
			{
				array[i] = reader.ReadUInt16();
			}
			return array;
		}

		protected ushort[,] UnfilterShortHeightmap(byte[] heightMap, int chunkSize)
		{
			ushort[] array;
			using (BinaryReader binaryReader = new BinaryReader(new MemoryStream(heightMap)))
			{
				array = new ushort[heightMap.Length / 2];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = binaryReader.ReadUInt16();
				}
			}
			ushort num;
			for (int j = 0; j < chunkSize; j++)
			{
				num = 0;
				for (int k = 0; k < chunkSize; k++)
				{
					int num2 = k + j * chunkSize;
					array[num2] += num;
					num = array[num2];
				}
			}
			num = 0;
			for (int l = 0; l < chunkSize; l++)
			{
				num = 0;
				for (int m = 0; m < chunkSize; m++)
				{
					int num3 = l + m * chunkSize;
					array[num3] += num;
					num = array[num3];
				}
			}
			ushort[,] array2 = new ushort[chunkSize, chunkSize];
			for (int n = 0; n < chunkSize; n++)
			{
				for (int num4 = 0; num4 < chunkSize; num4++)
				{
					array2[n, num4] = array[chunkSize * num4 + n];
				}
			}
			return array2;
		}

		protected ushort[,] UnfilterHeightmap(byte[] heightMap, int chunkSize)
		{
			for (int i = 0; i < chunkSize; i++)
			{
				int num = 0;
				for (int j = 0; j < chunkSize; j++)
				{
					byte b = heightMap[chunkSize * i + j];
					heightMap[chunkSize * i + j] = (byte)(num + b);
					num += b;
				}
			}
			for (int k = 0; k < chunkSize; k++)
			{
				int num = 0;
				for (int l = 0; l < chunkSize; l++)
				{
					byte b2 = heightMap[chunkSize * l + k];
					heightMap[chunkSize * l + k] = (byte)(num + b2);
					num += b2;
				}
			}
			ushort[,] array = new ushort[chunkSize, chunkSize];
			for (int m = 0; m < chunkSize; m++)
			{
				for (int n = 0; n < chunkSize; n++)
				{
					array[m, n] = heightMap[chunkSize * n + m];
				}
			}
			return array;
		}
	}
	public class SurfaceResource : DbObject
	{
		[StructLayout(LayoutKind.Explicit)]
		private struct Surface
		{
			[FieldOffset(12)]
			public StdStructList Meshes;
		}

		[StructLayout(LayoutKind.Explicit, Size = 56)]
		private struct SurfaceMeshData
		{
			[FieldOffset(0)]
			public int NumTriangles;

			[FieldOffset(4)]
			public int NumVertices;

			[FieldOffset(8)]
			public IntPtr TriangleArray;
		}

		public readonly List<AOSharp.Common.GameData.Mesh> Meshes;

		private SurfaceResource(IntPtr pointer)
			: base(pointer)
		{
			Meshes = GetMeshes();
		}

		public static SurfaceResource FromPointer(IntPtr pointer)
		{
			return new SurfaceResource(pointer);
		}

		public static SurfaceResource Get(int id)
		{
			DBIdentity identity = new DBIdentity(DBIdentityType.SurfaceResource, id);
			IntPtr dbObject = ResourceDatabase.GetDbObject(identity);
			if (dbObject == IntPtr.Zero)
			{
				return null;
			}
			return new SurfaceResource(dbObject + 24);
		}

		private unsafe List<AOSharp.Common.GameData.Mesh> GetMeshes()
		{
			List<AOSharp.Common.GameData.Mesh> list = new List<AOSharp.Common.GameData.Mesh>();
			Surface surface = *(Surface*)(void*)base.Pointer;
			foreach (SurfaceMeshData item in surface.Meshes.ToList<SurfaceMeshData>())
			{
				List<Vector3> list2 = new List<Vector3>();
				List<int> list3 = new List<int>();
				IntPtr intPtr = item.TriangleArray + ((item.NumTriangles * ((item.NumVertices > 255) ? 6 : 3) + 3) & -4);
				for (int i = 0; i < item.NumVertices; i++)
				{
					list2.Add(new Vector3(*(float*)(void*)(intPtr + i * sizeof(Vector3)), *(float*)(void*)(intPtr + i * sizeof(Vector3) + 4), *(float*)(void*)(intPtr + i * sizeof(Vector3) + 8)));
				}
				for (int j = 0; j < item.NumTriangles; j++)
				{
					if (item.NumVertices <= 255)
					{
						list3.Add(*(byte*)(void*)(item.TriangleArray + j * 3));
						list3.Add(*(byte*)(void*)(item.TriangleArray + j * 3 + 1));
						list3.Add(*(byte*)(void*)(item.TriangleArray + j * 3 + 2));
					}
					else
					{
						list3.Add(*(short*)(void*)(item.TriangleArray + j * 6));
						list3.Add(*(short*)(void*)(item.TriangleArray + j * 6 + 2));
						list3.Add(*(short*)(void*)(item.TriangleArray + j * 6 + 4));
					}
				}
				list.Add(new AOSharp.Common.GameData.Mesh
				{
					Vertices = list2,
					Triangles = list3
				});
			}
			return list;
		}
	}
	internal class RDBPlayfield : DbObject
	{
		public RDBPlayfield(IntPtr pointer)
			: base(pointer)
		{
		}

		public static RDBPlayfield Get(int playfieldId)
		{
			DBIdentity identity = new DBIdentity(DBIdentityType.RDBPlayfield, playfieldId);
			return ResourceDatabase.GetDbObject<RDBPlayfield>(identity);
		}
	}
}
namespace AOSharp.Common.Unmanaged.DataTypes
{
	[StructLayout(LayoutKind.Explicit)]
	public struct ACEDataBlock
	{
		[FieldOffset(8)]
		public int BlockSize;

		[FieldOffset(20)]
		public IntPtr Data;
	}
	[StructLayout(LayoutKind.Sequential, Size = 24)]
	public struct PlayfieldProxy
	{
		public Identity ProxyId;

		public Identity Unknown;

		public Identity PlayfieldId;
	}
	public struct StdStructList
	{
		private IntPtr _pArray;

		public int Count;

		public unsafe IEnumerable<T> ToList<T>() where T : unmanaged
		{
			List<T> list = new List<T>();
			for (int i = 0; i < Count; i++)
			{
				list.Add(*(T*)(void*)(_pArray + i * sizeof(T)));
			}
			return list;
		}
	}
	public struct StdObjList
	{
		private IntPtr pFirst;

		public int count;

		public unsafe List<IntPtr> ToList()
		{
			List<IntPtr> list = new List<IntPtr>();
			IntPtr intPtr = pFirst;
			for (int i = 0; i < count; i++)
			{
				intPtr = *(IntPtr*)(void*)intPtr;
				list.Add(intPtr);
			}
			return list;
		}
	}
	public struct StdObjVector
	{
		private IntPtr pFirst;

		private IntPtr pLast;

		public unsafe List<IntPtr> ToList()
		{
			List<IntPtr> list = new List<IntPtr>();
			IntPtr intPtr = pFirst;
			while (intPtr.ToInt32() < pLast.ToInt32())
			{
				list.Add(*(IntPtr*)(void*)intPtr);
				intPtr += 4;
			}
			return list;
		}
	}
	public class StdString : IDisposable
	{
		private const int NativeObjectSize = 24;

		public readonly IntPtr Pointer;

		private bool _disposedValue;

		private bool _shouldDispose;

		public unsafe int Length => ((StdStringStruct*)(void*)Pointer)->Length;

		internal StdString(IntPtr pointer, bool shouldDispose = true)
		{
			Pointer = pointer;
			_shouldDispose = shouldDispose;
		}

		public static StdString FromPointer(IntPtr pointer, bool shouldDispose = true)
		{
			return new StdString(pointer, shouldDispose);
		}

		public static StdString Create()
		{
			return Create(string.Empty);
		}

		public static StdString Create(string str)
		{
			byte[] bytes = Encoding.ASCII.GetBytes(str);
			return new StdString(String_c.Constructor(MSVCR100.New(24), bytes, bytes.Length));
		}

		public unsafe override string ToString()
		{
			return ((StdStringStruct*)(void*)Pointer)->ToString();
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
			{
				return false;
			}
			if (this == obj)
			{
				return true;
			}
			return ToString() == obj.ToString();
		}

		public static bool operator ==(StdString str1, StdString str2)
		{
			if ((object)str1 == null)
			{
				if ((object)str2 == null)
				{
					return true;
				}
				return false;
			}
			return str1.Equals(str2);
		}

		public static bool operator !=(StdString str1, StdString str2)
		{
			return !(str1 == str2);
		}

		public static bool operator ==(StdString str1, string str2)
		{
			if ((object)str1 == null)
			{
				if (str2 == null)
				{
					return true;
				}
				return false;
			}
			return str1.Equals(str2);
		}

		public static bool operator !=(StdString str1, string str2)
		{
			return !(str1 == str2);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				String_c.Deconstructor(Pointer);
				MSVCR100.Delete(Pointer);
				_disposedValue = true;
			}
		}

		~StdString()
		{
			if (_shouldDispose)
			{
				Dispose(disposing: false);
			}
		}

		public void Dispose()
		{
			if (_shouldDispose)
			{
				Dispose(disposing: true);
				GC.SuppressFinalize(this);
			}
		}
	}
	[StructLayout(LayoutKind.Explicit)]
	public struct StdStringStruct
	{
		[FieldOffset(0)]
		private unsafe fixed byte _shortBuffer[16];

		[FieldOffset(0)]
		private unsafe byte* _pLongBuffer;

		[FieldOffset(16)]
		public int Length;

		[FieldOffset(20)]
		public int Capacity;

		public unsafe override string ToString()
		{
			if (Length < 16)
			{
				fixed (byte* bytes = _shortBuffer)
				{
					return Encoding.ASCII.GetString(bytes, Length);
				}
			}
			return Encoding.ASCII.GetString(_pLongBuffer, Length);
		}
	}
	public struct StdStructVector
	{
		private IntPtr pFirst;

		private IntPtr pLast;

		private IntPtr Unk;

		public List<IntPtr> ToList(int size)
		{
			List<IntPtr> list = new List<IntPtr>();
			IntPtr item = pFirst;
			while (item.ToInt32() < pLast.ToInt32())
			{
				list.Add(item);
				item += size;
			}
			return list;
		}

		public unsafe List<T> ToList<T>() where T : unmanaged
		{
			List<T> list = new List<T>();
			IntPtr intPtr = pFirst;
			while (intPtr.ToInt32() < pLast.ToInt32())
			{
				list.Add(*(T*)(void*)intPtr);
				intPtr += sizeof(T);
			}
			return list;
		}
	}
	public class DistributedValue
	{
		public static void Create(string name, int value)
		{
			Create(name, Variant.Create(value));
		}

		public static void Create(string name, float value)
		{
			Create(name, Variant.Create(value));
		}

		public static void Create(string name, bool value)
		{
			Create(name, Variant.Create(value));
		}

		public static void Create(string name, Variant value)
		{
			StdString stdString = StdString.Create(name);
			DistributedValue_c.AddVariable(stdString.Pointer, value.Pointer, unk1: false, unk2: false);
		}

		public static Variant GetDValue(string name, bool unk)
		{
			StdString stdString = StdString.Create(name);
			IntPtr dValue = DistributedValue_c.GetDValue(MSVCR100.New(16), stdString.Pointer, unk);
			return (dValue == IntPtr.Zero) ? null : Variant.FromPointer(dValue);
		}

		public static void SetDValue(string name, Variant value)
		{
			StdString stdString = StdString.Create(name);
			DistributedValue_c.SetDValue(stdString.Pointer, value.Pointer);
		}

		public static void LoadConfig(string path, int category, bool addVariables)
		{
			StdString stdString = StdString.Create(path);
			DistributedValue_c.LoadConfig(stdString.Pointer, category, addVariables);
		}

		public static void SaveConfig(string path, int category)
		{
			StdString stdString = StdString.Create(path);
			DistributedValue_c.SaveConfig(stdString.Pointer, category);
		}
	}
	public class Variant : IDisposable
	{
		public const int SizeOf = 16;

		public readonly IntPtr Pointer;

		private bool _disposedValue;

		private bool _shouldDispose;

		private Variant(IntPtr pointer, bool shouldDispose = true)
		{
			Pointer = pointer;
			_shouldDispose = shouldDispose;
		}

		public static Variant Create()
		{
			return new Variant(Variant_c.Constructor(MSVCR100.New(16)));
		}

		public static Variant Create(int value)
		{
			return new Variant(Variant_c.Constructor(MSVCR100.New(16), value));
		}

		public static Variant Create(float value)
		{
			return new Variant(Variant_c.Constructor(MSVCR100.New(16), value));
		}

		public static Variant Create(double value)
		{
			return new Variant(Variant_c.Constructor(MSVCR100.New(16), value));
		}

		public static Variant Create(bool value)
		{
			return new Variant(Variant_c.Constructor(MSVCR100.New(16), value));
		}

		public static Variant FromPointer(IntPtr pointer, bool shouldDispose = true)
		{
			return new Variant(pointer, shouldDispose);
		}

		public override string ToString()
		{
			StdString stdString = StdString.Create();
			Variant_c.SaveToString(Pointer, stdString.Pointer);
			return stdString.ToString();
		}

		public static Variant LoadFromString(string value)
		{
			Variant variant = Create(0);
			Variant_c.LoadFromString(variant.Pointer, value);
			return variant;
		}

		public int AsInt32()
		{
			return Variant_c.AsInt32(Pointer);
		}

		public float AsFloat()
		{
			return Variant_c.AsFloat(Pointer);
		}

		public double AsDouble()
		{
			return Variant_c.AsDouble(Pointer);
		}

		public bool AsBool()
		{
			return Variant_c.AsBool(Pointer);
		}

		public string AsString()
		{
			StdString stdString = StdString.Create();
			Variant_c.AsString(Pointer, stdString.Pointer);
			return stdString.ToString();
		}

		public void SetBool(bool value)
		{
			Variant_c.SetBool(Pointer, value);
		}

		public static implicit operator Variant(int v)
		{
			return Create(v);
		}

		public static implicit operator Variant(float v)
		{
			return Create(v);
		}

		public static implicit operator Variant(bool v)
		{
			return Create(v);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				Variant_c.Deconstructor(Pointer);
				MSVCR100.Delete(Pointer);
				_disposedValue = true;
			}
		}

		~Variant()
		{
			if (_shouldDispose)
			{
				Dispose(disposing: false);
			}
		}

		public void Dispose()
		{
			if (_shouldDispose)
			{
				Dispose(disposing: true);
				GC.SuppressFinalize(this);
			}
		}
	}
}
namespace AOSharp.Common.SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
	[AoContract(959724648)]
	public class BuffMessage : N3Message
	{
		[AoMember(0)]
		public short Unknown1 { get; set; }

		[AoMember(1)]
		public Identity Buff { get; set; }

		public BuffMessage()
		{
			base.N3MessageType = N3MessageType.Buff;
		}
	}
	[AoContract(859514983)]
	public class MailMessage : N3Message
	{
		[AoMember(0)]
		public short Unknown1 { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public string Recipient { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.Int16)]
		public string Subject { get; set; }

		[AoMember(3, SerializeSize = ArraySizeType.Int16)]
		public string Body { get; set; }

		[AoMember(4)]
		public Identity Item { get; set; }

		[AoMember(5)]
		public int Credits { get; set; }

		[AoMember(6)]
		public bool Express { get; set; }

		public MailMessage()
		{
			base.N3MessageType = N3MessageType.Mail;
		}
	}
}
namespace AOSharp.Common.Helpers
{
	public enum BitFlag : uint
	{
		None = 0u,
		Bit0 = 1u,
		Bit1 = 2u,
		Bit2 = 4u,
		Bit3 = 8u,
		Bit4 = 0x10u,
		Bit5 = 0x20u,
		Bit6 = 0x40u,
		Bit7 = 0x80u,
		Bit8 = 0x100u,
		Bit9 = 0x200u,
		Bit10 = 0x400u,
		Bit11 = 0x800u,
		Bit12 = 0x1000u,
		Bit13 = 0x2000u,
		Bit14 = 0x4000u,
		Bit15 = 0x8000u,
		Bit16 = 0x10000u,
		Bit17 = 0x20000u,
		Bit18 = 0x40000u,
		Bit19 = 0x80000u,
		Bit20 = 0x100000u,
		Bit21 = 0x200000u,
		Bit22 = 0x400000u,
		Bit23 = 0x800000u,
		Bit24 = 0x1000000u,
		Bit25 = 0x2000000u,
		Bit26 = 0x4000000u,
		Bit27 = 0x8000000u,
		Bit28 = 0x10000000u,
		Bit29 = 0x20000000u,
		Bit30 = 0x40000000u,
		Bit31 = 0x80000000u
	}
	public static class Utils
	{
		public unsafe static string UnsafePointerToString(IntPtr pointer)
		{
			if (pointer == IntPtr.Zero)
			{
				return string.Empty;
			}
			byte* ptr = (byte*)pointer.ToPointer();
			int i;
			for (i = 0; ptr[i] != 0; i++)
			{
			}
			char[] array = new char[i];
			fixed (char* chars = array)
			{
				Encoding.ASCII.GetChars(ptr, i, chars, i);
			}
			return new string(array);
		}

		public unsafe static bool Compare(byte* pData, byte[] pattern, bool[] mask)
		{
			for (int i = 0; i < pattern.Length; i++)
			{
				if (mask[i] && *pData != pattern[i])
				{
					return false;
				}
				pData++;
			}
			return true;
		}

		public unsafe static IntPtr FindPattern(string module, string pattern)
		{
			IntPtr moduleHandle = Kernel32.GetModuleHandle(module);
			if (moduleHandle == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}
			if (!Psapi.GetModuleInformation(Kernel32.GetCurrentProcess(), moduleHandle, out var lpmodinfo, sizeof(Psapi.MODULEINFO)))
			{
				return IntPtr.Zero;
			}
			uint num = (uint)(int)lpmodinfo.lpBaseOfDll;
			uint sizeOfImage = lpmodinfo.SizeOfImage;
			string[] source = pattern.Split(' ');
			bool[] mask = source.Select((string x) => x != "?").ToArray();
			byte[] array = source.Select((string x) => (byte)((x != "?") ? ((byte)Convert.ToInt32(x, 16)) : 0)).ToArray();
			for (uint num2 = 0u; num2 < sizeOfImage - array.Length; num2++)
			{
				if (Compare((byte*)(num + num2), array, mask))
				{
					return new IntPtr(num + num2);
				}
			}
			return IntPtr.Zero;
		}

		public static byte[] StringToByteArray(string hex)
		{
			return (from x in Enumerable.Range(0, hex.Length)
				where x % 2 == 0
				select Convert.ToByte(hex.Substring(x, 2), 16)).ToArray();
		}
	}
}
namespace AOSharp.Common.SharedEventArgs
{
	public class AttemptingSpellCastEventArgs : EventArgs
	{
		public readonly Identity Nano;

		public readonly Identity Target;

		public bool Blocked { get; private set; }

		public AttemptingSpellCastEventArgs(Identity nano, Identity target)
		{
			Nano = nano;
			Target = target;
			Blocked = false;
		}

		public void Block()
		{
			Blocked = true;
		}
	}
	public class GroupMessageEventArgs : EventArgs
	{
		public readonly GroupMessage Message;

		public bool Cancel { get; set; } = false;


		public GroupMessageEventArgs(GroupMessage message)
		{
			Message = message;
		}
	}
}
namespace AOSharp.Common.GameData
{
	public enum BattlestationSide
	{
		Red,
		Blue,
		None
	}
	public enum HitType
	{
		Glancing = 2,
		Normal,
		Critical
	}
	public struct IPoint
	{
		public int X;

		public int Y;

		public static readonly IPoint Zero = new IPoint(0, 0);

		public IPoint(int x, int y)
		{
			X = x;
			Y = y;
		}

		public override string ToString()
		{
			return $"({X}, {Y})";
		}

		public Vector2 ToVector2()
		{
			return new Vector2(X, Y);
		}

		public Vector3 ToVector3()
		{
			return new Vector3(X, Y, 0f);
		}
	}
	[Flags]
	public enum SimpleItemFlags
	{
		Locked = 0x40,
		Open = 0x80
	}
	public enum ItemClass
	{
		None,
		Weapon,
		Armor,
		Implant
	}
	public enum SpellListType
	{
		Use = 0,
		Hit = 5,
		Wear = 14,
		Failure = 27
	}
	public enum SpellModifierTarget
	{
		Self = 1,
		User,
		Target
	}
	public enum SpellFunction
	{
		Hit = 53002,
		AnimEffect = 53003,
		ModifyNanoStat = 53012,
		ModifyTemp = 53014,
		TeleportPerk = 53016,
		Upload = 53019,
		Set = 53026,
		HeadMesh = 53035,
		AddSkill = 53028,
		GfxEffect = 53030,
		LockSkill = 53033,
		BackMesh = 53037,
		ShoulderMesh = 53038,
		ApplyTexture = 53039,
		SystemText = 53044,
		ModifyStat = 53045,
		CastNano = 53051,
		BodyMesh = 53054,
		AttractorMesh = 53055,
		FloatText = 53057,
		ChangeMesh = 53060,
		SpawnMonster = 53063,
		SpawnItem = 53064,
		CastTeam = 53066,
		ImplantAccess = 53067,
		Disallow = 53068,
		AoeDmg = 53073,
		ScreenEffect = 53079,
		Teleport = 53083,
		RefreshModel = 53086,
		CastPerk = 53087,
		CastChance = 53089,
		OpenBank = 53092,
		NpcSay = 53104,
		Remove = 53105,
		TempChange = 53110,
		Taunt = 53117,
		ClearHateList = 53126,
		DestroyItem = 53130,
		SetGovernType = 53133,
		Text = 53134,
		ClearFlag = 53140,
		LockPerk = 53187,
		EnableFlight = 53138,
		SetFlag = 53139,
		TeleportLastSave = 53144,
		ResistNano = 53162,
		GenerateName = 53166,
		SummonPet = 53167,
		Deploy = 53173,
		ModifyLvlScaling = 53175,
		Reduce = 53177,
		DisableDefShield = 53178,
		AddAction = 53182,
		DrainDmg = 53185,
		Update = 53189,
		Polymorph = 53193,
		HitPerk = 53196,
		AttractorGfx = 53204,
		RunScript = 53221,
		AddDefProc = 53224,
		CreateCityGuestKey = 53235,
		SpawnQuest = 53226,
		AddOffProc = 53227,
		CastOnPf = 53228,
		SolveQuest = 53229,
		Knockback = 53230,
		EnableRaidLockOnPf = 53231,
		ResetAllPerks = 53234,
		RemoveStrain = 53236,
		ChangeBreed = 53238,
		ChangeGender = 53239,
		CastOnPets = 53240,
		CastBuff = 53242,
		Charge = 53243,
		Transfer = 53249,
		DeleteQuest = 53250,
		FailQuest = 53251,
		SendMail = 53252,
		EndFight = 53253
	}
	public enum SpellPropertyOperator
	{
		Stat = 0,
		Type = 1,
		Min = 2,
		Duration = 3,
		Interval = 4,
		TargetType = 5,
		TargetInstance = 6,
		AnimEffect = 7,
		MeshEffect = 8,
		ItemType = 9,
		ItemInstance = 10,
		Radius = 11,
		RemoveType = 12,
		TextID = 13,
		VisualEffectMesh = 14,
		VisualEffectAnim = 15,
		VisualRadius = 16,
		AudioEffectID = 17,
		AudioEffectDuration = 18,
		AudioEffectRepeat = 19,
		AudioEffectVolume = 20,
		PoisonType = 21,
		PoisonDifficulty = 22,
		SkillType = 24,
		TimedLength = 25,
		Criteria = 26,
		Operator = 27,
		RelXPos = 28,
		RelYPos = 29,
		RelZPos = 30,
		TeleportDest = 31,
		ApplyOn = 32,
		PoisonSpellType = 33,
		PoisonSpellInstance = 34,
		TargetList = 35,
		Music = 36,
		Max = 37,
		AnimReverse = 38,
		Value = 39,
		TargetExpression = 40,
		Pos = 41,
		Play = 42,
		Speed = 43,
		CatMeshEffect = 44,
		AnimFlag = 45,
		Icon = 46,
		Sex = 47,
		Breed = 48,
		GfxLife = 49,
		GfxSize = 50,
		GfxRed = 51,
		GfxGreen = 52,
		GfxBlue = 53,
		GfxFade = 54,
		Action = 55,
		BodyPart = 56,
		Texture = 57,
		SeeSkin = 58,
		WeaponEffect = 59,
		BaseAmount = 60,
		RegenAmount = 61,
		RegenInterval = 62,
		RarityValue = 63,
		Cost = 64,
		Text = 65,
		Arg1 = 66,
		Arg2 = 67,
		Arg3 = 68,
		Arg4 = 69,
		DamageType = 71,
		TimeExist = 72,
		Flags = 73,
		StrMesh = 74,
		StrTexture = 75,
		BodyCatMesh = 76,
		Hash = 77,
		ToClient = 78,
		Unk1 = 81,
		HighItemIdMaxQl = 84,
		HighItemIdMinQl = 85,
		ScreenVfx = 87,
		ItemId = 88,
		PfModelIdentityType = 94,
		PfModelIdentityInstance = 95,
		Unk4 = 96,
		Unk5 = 97,
		Unk6 = 98,
		Unk7 = 99,
		Unk8 = 100,
		Unk2 = 102,
		Unk3 = 103,
		NanoSchool = 116,
		Unk10 = 120,
		Unk11 = 121,
		Unk21 = 124,
		PetReq1 = 128,
		PetReq2 = 129,
		PetReq3 = 130,
		PetReqVal1 = 131,
		PetReqVal2 = 132,
		PetReqVal3 = 133,
		NanoProperty = 152,
		Unk12 = 153,
		Unk13 = 154,
		Unk14 = 155,
		Unk15 = 156,
		Unk16 = 157,
		Unk17 = 158,
		Unk = 159,
		Unk19 = 160,
		Unk20 = 161,
		Unk9 = 169
	}
	public enum GroupMessageType : byte
	{
		Tower = 10,
		Org = 3,
		Team = 130,
		Shopping = 134,
		OOC = 135
	}
	public enum PlayfieldId
	{
		Grid = 152,
		Avalon = 505,
		BrokenCrest = 510,
		OldAthen = 540,
		AthenWest = 545,
		AthenShire = 550,
		WailingWastes = 551,
		CoastofPeace = 556,
		Mort = 560,
		NewlandDesert = 565,
		NewlandCity = 566,
		Newland = 567,
		PerpetualWastelands = 570,
		PlainsofSalt = 575,
		ThreeCraters = 580,
		Aegean = 585,
		WartornValley = 586,
		CentralArteryValley = 590,
		DeepArteryValley = 595,
		VarmintWoods = 600,
		BelialForest = 605,
		SouthernArteryValley = 610,
		SouthernFoulsHills = 615,
		EasternFoulsPlains = 620,
		MilkyWay = 625,
		PleasantMeadows = 630,
		StretEastBank = 635,
		Tir = 640,
		TirCounty = 646,
		GreaterTirCounty = 647,
		UpperStretEastBank = 650,
		Andromeda = 655,
		CoastofTranquility = 656,
		BayofRome = 660,
		BrokenShores = 665,
		Clondyke = 670,
		GalwayCounty = 685,
		GalwayShire = 687,
		LushFields = 695,
		MutantDomain = 696,
		OmniHQ = 700,
		OmniEntertainment = 705,
		OmniTrade = 710,
		OmniForest = 716,
		GreaterOmniForest = 717,
		RomeReddistrict = 730,
		RomeBluedistrict = 735,
		RomeGreendistrict = 740,
		TheReck = 750,
		TheSpur = 755,
		_4Holes = 760,
		GanglyMountains = 765,
		StretWestBank = 790,
		HolesIntheWall = 791,
		TheLongestRoad = 795,
		BorealisCity = 800,
		JobeResearch = 4001,
		BurningMarshes = 4005,
		PenumbraForest = 4006,
		FixerGrid = 4107,
		NascenseFrontier = 4310,
		NascenseWilds = 4311,
		NascenseSwamp = 4312,
		theresearchandtrainingbiosphere = 4313,
		PenumbraHollows = 4320,
		PenumbraValley = 4321,
		Caina = 4328,
		Antenora = 4329,
		Ptolemea = 4330,
		Judecca = 4331,
		Sector13 = 4365,
		Sector28 = 4366,
		Sector35 = 4367,
		APFHub = 4368,
		Pandemonium = 4389,
		BeastLair = 4391,
		JobePlatform = 4530,
		theHarbor = 4531,
		theMarket = 4532,
		thePlaza = 4533,
		aHangingGardencondo = 4534,
		SouthElysium = 4540,
		WestElysium = 4541,
		Elysium = 4542,
		EasternElysium = 4543,
		NorthernElysium = 4544,
		ElysiumTrial = 4545,
		theShadowlandsportal = 4560,
		ashopinJobe = 4561,
		Inferno = 4605,
		TheGardenofAban = 4676,
		TheGardenofThrak = 4677,
		TheGardenofEnel = 4678,
		EnelsSanctuary = 4679,
		TheGardenofShere = 4680,
		SheresSanctuary = 4681,
		TheGardenofOcra = 4682,
		TheGardenofRoch = 4683,
		TheGardenofGilthar = 4684,
		GiltharsSanctuary = 4685,
		TheGardenofDalja = 4686,
		DaljasSanctuary = 4687,
		TheGardenofCama = 4688,
		CamasSanctuary = 4689,
		TheGardenofVanya = 4690,
		VanyasSanctuary = 4691,
		TheGardenofLordGalahad = 4692,
		LordGalahadsSanctuary = 4693,
		TheGardenofLordMordeth = 4694,
		LordMordethsSanctuary = 4695,
		DantesSanctuary = 4696,
		MacciavellisSanctuary = 4697,
		OcrasSanctuary = 4698,
		RochsSanctuary = 4699,
		AdonisCity = 4872,
		AdonisAbyss = 4873,
		UpperScheol = 4880,
		LowerScheol = 4881,
		PlayadelDesierto = 5001,
		Montroyal = 5002,
		UnicornDefenceHub = 6007,
		SerenityIslands = 6010,
		ElderHall = 6015,
		ThreeCratersWest = 6101,
		ThreeCratersEast = 6102,
		Arete = 6553,
		ArbitersHall = 7001,
		ThePyramidofHome = 8020,
		TheGate = 8030,
		FoundryofNightmares = 8050,
		AbandonedResearchFacility = 8060,
		XanReliquary = 9042,
		TempleofThreeWinds = 9061,
		CondemnedSubway = 9070,
		TheBullsArena = 9080
	}
	[StructLayout(LayoutKind.Sequential, Size = 16)]
	public struct Vector4
	{
		public const float ZeroTolerance = 1E-06f;

		[AoMember(0)]
		public float X { get; set; }

		[AoMember(1)]
		public float Y { get; set; }

		[AoMember(2)]
		public float Z { get; set; }

		[AoMember(3)]
		public float W { get; set; }

		public Vector4(float x, float y, float z, float w)
		{
			X = x;
			Y = y;
			Z = z;
			W = w;
		}

		public override string ToString()
		{
			return $"({X}, {Y}, {Z}, {W})";
		}
	}
	public struct ResearchGoal
	{
		public int ResearchId;

		[MarshalAs(UnmanagedType.I1)]
		public bool Available;
	}
	public enum DuelUpdate
	{
		Challenge,
		Accept,
		Decline,
		Stop
	}
	public struct Matrix4x4
	{
		public float m00;

		public float m10;

		public float m20;

		public float m30;

		public float m01;

		public float m11;

		public float m21;

		public float m31;

		public float m02;

		public float m12;

		public float m22;

		public float m32;

		public float m03;

		public float m13;

		public float m23;

		public float m33;

		public float this[int row, int column]
		{
			get
			{
				return this[row + column * 4];
			}
			set
			{
				this[row + column * 4] = value;
			}
		}

		public float this[int index]
		{
			get
			{
				return index switch
				{
					0 => m00, 
					1 => m10, 
					2 => m20, 
					3 => m30, 
					4 => m01, 
					5 => m11, 
					6 => m21, 
					7 => m31, 
					8 => m02, 
					9 => m12, 
					10 => m22, 
					11 => m32, 
					12 => m03, 
					13 => m13, 
					14 => m23, 
					15 => m33, 
					_ => throw new IndexOutOfRangeException("Invalid matrix index!"), 
				};
			}
			set
			{
				switch (index)
				{
				case 0:
					m00 = value;
					break;
				case 1:
					m10 = value;
					break;
				case 2:
					m20 = value;
					break;
				case 3:
					m30 = value;
					break;
				case 4:
					m01 = value;
					break;
				case 5:
					m11 = value;
					break;
				case 6:
					m21 = value;
					break;
				case 7:
					m31 = value;
					break;
				case 8:
					m02 = value;
					break;
				case 9:
					m12 = value;
					break;
				case 10:
					m22 = value;
					break;
				case 11:
					m32 = value;
					break;
				case 12:
					m03 = value;
					break;
				case 13:
					m13 = value;
					break;
				case 14:
					m23 = value;
					break;
				case 15:
					m33 = value;
					break;
				default:
					throw new IndexOutOfRangeException("Invalid matrix index!");
				}
			}
		}

		public Matrix4x4(Vector4 column0, Vector4 column1, Vector4 column2, Vector4 column3)
		{
			m00 = column0.X;
			m01 = column1.X;
			m02 = column2.X;
			m03 = column3.X;
			m10 = column0.Y;
			m11 = column1.Y;
			m12 = column2.Y;
			m13 = column3.Y;
			m20 = column0.Z;
			m21 = column1.Z;
			m22 = column2.Z;
			m23 = column3.Z;
			m30 = column0.W;
			m31 = column1.W;
			m32 = column2.W;
			m33 = column3.W;
		}

		public Vector4 GetColumn(int index)
		{
			return index switch
			{
				0 => new Vector4(m00, m10, m20, m30), 
				1 => new Vector4(m01, m11, m21, m31), 
				2 => new Vector4(m02, m12, m22, m32), 
				3 => new Vector4(m03, m13, m23, m33), 
				_ => throw new IndexOutOfRangeException("Invalid column index!"), 
			};
		}

		public Vector3 MultiplyPoint3x4(Vector3 point)
		{
			Vector3 result = default(Vector3);
			result.X = m00 * point.X + m01 * point.Y + m02 * point.Z + m03;
			result.Y = m10 * point.X + m11 * point.Y + m12 * point.Z + m13;
			result.Z = m20 * point.X + m21 * point.Y + m22 * point.Z + m23;
			return result;
		}

		public Vector3 MultiplyPoint(Vector3 point)
		{
			Vector3 zero = Vector3.Zero;
			zero.X = m00 * point.X + m01 * point.Y + m02 * point.Z + m03;
			zero.Y = m10 * point.X + m11 * point.Y + m12 * point.Z + m13;
			zero.Z = m20 * point.X + m21 * point.Y + m22 * point.Z + m23;
			float num = m30 * point.X + m31 * point.Y + m32 * point.Z + m33;
			num = 1f / num;
			zero.X *= num;
			zero.Y *= num;
			zero.Z *= num;
			return zero;
		}

		public static Matrix4x4 Scale(Vector3 vector)
		{
			Matrix4x4 result = default(Matrix4x4);
			result.m00 = vector.X;
			result.m01 = 0f;
			result.m02 = 0f;
			result.m03 = 0f;
			result.m10 = 0f;
			result.m11 = vector.Y;
			result.m12 = 0f;
			result.m13 = 0f;
			result.m20 = 0f;
			result.m21 = 0f;
			result.m22 = vector.Z;
			result.m23 = 0f;
			result.m30 = 0f;
			result.m31 = 0f;
			result.m32 = 0f;
			result.m33 = 1f;
			return result;
		}

		public static Matrix4x4 Translate(Vector3 vector)
		{
			Matrix4x4 result = default(Matrix4x4);
			result.m00 = 1f;
			result.m01 = 0f;
			result.m02 = 0f;
			result.m03 = vector.X;
			result.m10 = 0f;
			result.m11 = 1f;
			result.m12 = 0f;
			result.m13 = vector.Y;
			result.m20 = 0f;
			result.m21 = 0f;
			result.m22 = 1f;
			result.m23 = vector.Z;
			result.m30 = 0f;
			result.m31 = 0f;
			result.m32 = 0f;
			result.m33 = 1f;
			return result;
		}

		public static Matrix4x4 Rotate(Quaternion q)
		{
			float num = q.X * 2f;
			float num2 = q.Y * 2f;
			float num3 = q.Z * 2f;
			float num4 = q.X * num;
			float num5 = q.Y * num2;
			float num6 = q.Z * num3;
			float num7 = q.X * num2;
			float num8 = q.X * num3;
			float num9 = q.Y * num3;
			float num10 = q.W * num;
			float num11 = q.W * num2;
			float num12 = q.W * num3;
			Matrix4x4 result = default(Matrix4x4);
			result.m00 = 1f - (num5 + num6);
			result.m10 = num7 + num12;
			result.m20 = num8 - num11;
			result.m30 = 0f;
			result.m01 = num7 - num12;
			result.m11 = 1f - (num4 + num6);
			result.m21 = num9 + num10;
			result.m31 = 0f;
			result.m02 = num8 + num11;
			result.m12 = num9 - num10;
			result.m22 = 1f - (num4 + num5);
			result.m32 = 0f;
			result.m03 = 0f;
			result.m13 = 0f;
			result.m23 = 0f;
			result.m33 = 1f;
			return result;
		}

		public static Matrix4x4 operator *(Matrix4x4 lhs, Matrix4x4 rhs)
		{
			Matrix4x4 result = default(Matrix4x4);
			result.m00 = lhs.m00 * rhs.m00 + lhs.m01 * rhs.m10 + lhs.m02 * rhs.m20 + lhs.m03 * rhs.m30;
			result.m01 = lhs.m00 * rhs.m01 + lhs.m01 * rhs.m11 + lhs.m02 * rhs.m21 + lhs.m03 * rhs.m31;
			result.m02 = lhs.m00 * rhs.m02 + lhs.m01 * rhs.m12 + lhs.m02 * rhs.m22 + lhs.m03 * rhs.m32;
			result.m03 = lhs.m00 * rhs.m03 + lhs.m01 * rhs.m13 + lhs.m02 * rhs.m23 + lhs.m03 * rhs.m33;
			result.m10 = lhs.m10 * rhs.m00 + lhs.m11 * rhs.m10 + lhs.m12 * rhs.m20 + lhs.m13 * rhs.m30;
			result.m11 = lhs.m10 * rhs.m01 + lhs.m11 * rhs.m11 + lhs.m12 * rhs.m21 + lhs.m13 * rhs.m31;
			result.m12 = lhs.m10 * rhs.m02 + lhs.m11 * rhs.m12 + lhs.m12 * rhs.m22 + lhs.m13 * rhs.m32;
			result.m13 = lhs.m10 * rhs.m03 + lhs.m11 * rhs.m13 + lhs.m12 * rhs.m23 + lhs.m13 * rhs.m33;
			result.m20 = lhs.m20 * rhs.m00 + lhs.m21 * rhs.m10 + lhs.m22 * rhs.m20 + lhs.m23 * rhs.m30;
			result.m21 = lhs.m20 * rhs.m01 + lhs.m21 * rhs.m11 + lhs.m22 * rhs.m21 + lhs.m23 * rhs.m31;
			result.m22 = lhs.m20 * rhs.m02 + lhs.m21 * rhs.m12 + lhs.m22 * rhs.m22 + lhs.m23 * rhs.m32;
			result.m23 = lhs.m20 * rhs.m03 + lhs.m21 * rhs.m13 + lhs.m22 * rhs.m23 + lhs.m23 * rhs.m33;
			result.m30 = lhs.m30 * rhs.m00 + lhs.m31 * rhs.m10 + lhs.m32 * rhs.m20 + lhs.m33 * rhs.m30;
			result.m31 = lhs.m30 * rhs.m01 + lhs.m31 * rhs.m11 + lhs.m32 * rhs.m21 + lhs.m33 * rhs.m31;
			result.m32 = lhs.m30 * rhs.m02 + lhs.m31 * rhs.m12 + lhs.m32 * rhs.m22 + lhs.m33 * rhs.m32;
			result.m33 = lhs.m30 * rhs.m03 + lhs.m31 * rhs.m13 + lhs.m32 * rhs.m23 + lhs.m33 * rhs.m33;
			return result;
		}

		public override string ToString()
		{
			return ToString(null, null);
		}

		public string ToString(string format)
		{
			return ToString(format, null);
		}

		public string ToString(string format, IFormatProvider formatProvider)
		{
			if (string.IsNullOrEmpty(format))
			{
				format = "F5";
			}
			if (formatProvider == null)
			{
				formatProvider = CultureInfo.InvariantCulture.NumberFormat;
			}
			return $"{m00.ToString(format, formatProvider)}\t{m01.ToString(format, formatProvider)}\t{m02.ToString(format, formatProvider)}\t{m03.ToString(format, formatProvider)}\n{m10.ToString(format, formatProvider)}\t{m11.ToString(format, formatProvider)}\t{m12.ToString(format, formatProvider)}\t{m13.ToString(format, formatProvider)}\n{m20.ToString(format, formatProvider)}\t{m21.ToString(format, formatProvider)}\t{m22.ToString(format, formatProvider)}\t{m23.ToString(format, formatProvider)}\n{m30.ToString(format, formatProvider)}\t{m31.ToString(format, formatProvider)}\t{m32.ToString(format, formatProvider)}\t{m33.ToString(format, formatProvider)}\n";
		}
	}
	public enum TowerClass
	{
		ControlTower = 1,
		Turret = 2,
		Conductor = 4
	}
	public enum ChatColor
	{
		White = 0,
		LightBlue = 4,
		Yellow = 5,
		Green = 8,
		DarkPink = 9,
		Black = 11,
		Red = 12,
		DarkBlue = 14,
		Gold = 17,
		Orange = 27
	}
	public enum Breed
	{
		None,
		Solitus,
		Opifex,
		Nanomage,
		Atrox,
		Special,
		Monster,
		HumanMonster
	}
	public enum CharacterStatus
	{
		Active = 1
	}
	public enum ChatRange : byte
	{
		Say,
		Whisper,
		Shout
	}
	[Flags]
	public enum CharacterFlags
	{
		None = 0,
		Unknown = 1,
		Unknown1 = 8,
		Unknown2 = 0x40,
		PetTower = 0x200,
		Unknown4 = 0x800,
		Unknown5 = 0x1000,
		Tower = 0x20000,
		CollideWithStatels = 0x80000,
		Unknown7 = 0x100000,
		HasItemsForSale = 0x200000,
		HasVisibleName = 0x400000,
		HasBlueName = 0x800000,
		Pet = 0x8000000,
		Unknown8 = 0x20000000
	}
	public enum NpcClan
	{
		EngineerAttackPet = 95,
		MPHealPets = 96,
		MPAttackPets = 97,
		MPMezzPets = 98,
		ShadowMutants = 150
	}
	public enum NpcFamily
	{
		AttackPet = 100001,
		HealPet = 110001,
		SupportPet = 120001,
		Vendor = 11001,
		GuardsA = 70001,
		GuardsB = 70002
	}
	public enum AppearanceFlags
	{
		None = 0,
		HelmetVisible = 4,
		RightPadVisible = 1,
		LeftPadVisible = 2,
		AllowDoubleLeftPads = 8,
		AllowDoubleRightPads = 16,
		SocialTabEnabled = 32,
		SocialTabOnly = 64
	}
	[Flags]
	public enum ExpansionFlags
	{
		NotumWars = 1,
		ShadowLands = 2,
		ShadowLandsPreOrder = 4,
		AlienInvasion = 8,
		AlienInvasionPreOrder = 0x10,
		LostEden = 0x20,
		LostEdenPreOrder = 0x40,
		LegacyOfTheXan = 0x80,
		LegacyOfTheXanPreOrder = 0x100,
		Mail = 0x200,
		PMVObsidianEdition = 0x400
	}
	public enum EquipSlot
	{
		Weap_Hud1 = 1,
		Weap_Hud2 = 15,
		Weap_Hud3 = 2,
		Weap_Utils1 = 3,
		Weap_Utils2 = 4,
		Weap_Utils3 = 5,
		Weap_RightHand = 6,
		Weap_Belt = 7,
		Weap_LeftHand = 8,
		Weap_Deck1 = 9,
		Weap_Deck2 = 10,
		Weap_Deck3 = 11,
		Weap_Deck4 = 12,
		Weap_Deck5 = 13,
		Weap_Deck6 = 14,
		Cloth_Neck = 17,
		Cloth_Head = 18,
		Cloth_Back = 19,
		Cloth_RightShoulder = 20,
		Cloth_Body = 21,
		Cloth_LeftShoulder = 22,
		Cloth_RightArm = 23,
		Cloth_Hands = 24,
		Cloth_LeftArm = 25,
		Cloth_RightWrist = 26,
		Cloth_Legs = 27,
		Cloth_LeftWrist = 28,
		Cloth_RightFinger = 29,
		Cloth_Feet = 30,
		Cloth_LeftFinger = 31,
		Imp_Eye = 33,
		Imp_Head = 34,
		Imp_Ear = 35,
		Imp_RightArm = 36,
		Imp_Body = 37,
		Imp_LeftArm = 38,
		Imp_RightWrist = 39,
		Imp_Waist = 40,
		Imp_LeftWrist = 41,
		Imp_RightHand = 42,
		Imp_Legs = 43,
		Imp_LeftHand = 44,
		Imp_Feet = 45,
		Social_Neck = 49,
		Social_Head = 50,
		Social_Back = 51,
		Social_RightShoulder = 52,
		Social_Chest = 53,
		Social_LeftShoulder = 54,
		Social_RightArm = 55,
		Social_Hands = 56,
		Social_LeftArm = 57,
		Social_RightWrist = 58,
		Social_Legs = 59,
		Social_LeftWrist = 60,
		Social_RightWeap = 61,
		Social_Feet = 62,
		Social_LeftWeap = 63
	}
	public enum FightMode
	{
		OneHundredPercent,
		SeventyFivePercent,
		TwentyFivePercent,
		FivePercent,
		ZeroPercent
	}
	public class GroupMessage
	{
		[StructLayout(LayoutKind.Explicit)]
		private struct MemStruct
		{
			[FieldOffset(0)]
			public int ChannelIdMaybe;

			[FieldOffset(4)]
			public int ChannelType;

			[FieldOffset(8)]
			public uint SenderId;

			[FieldOffset(12)]
			public IntPtr SenderName;
		}

		public readonly IntPtr Pointer;

		public unsafe string SenderName => ((MemStruct*)(void*)Pointer)->SenderName.ToString();

		public unsafe uint SenderId => ((MemStruct*)(void*)Pointer)->SenderId;

		public unsafe int ChannelIdMaybe => ((MemStruct*)(void*)Pointer)->ChannelIdMaybe;

		public unsafe int ChannelType => ((MemStruct*)(void*)Pointer)->ChannelType;

		public GroupMessage(IntPtr pointer)
		{
			Pointer = pointer;
		}
	}
	public enum PerkLine
	{
		Accumulator,
		Acrobat,
		Alchemist,
		Ambidextrous,
		Assassin,
		AssaultForceMedic,
		AuraofRevival,
		BioShielding,
		BlackOps,
		Blessing,
		BluntMastery,
		Brawler,
		BureaucraticShuffle,
		CarefulinBattle,
		Cartographer,
		ChannelRage,
		ChannelingofNotum,
		ColossalHealth,
		CommandingPresence,
		Directorship,
		Disharmony,
		DistillLife,
		EdgedMastery,
		EnhanceDNA,
		EnhanceHealth,
		EnhanceNano,
		EnhancedNanoDamage,
		EssenceofNotum,
		Explorer,
		FerocityofNature,
		FirstAid,
		FormofTroll,
		FreakStrength,
		SpatialDisplacement,
		Gadgeteer,
		Genius,
		GridNCUExtension,
		HeavyRanged,
		HolyMark,
		RifleMastery,
		KungFuMaster,
		MannersofMongo,
		Mechanic,
		Mountaineer,
		NanoDoctorate,
		NanoSurgeon,
		NCUExtensions,
		NotumRepulsor,
		NotumSiphon,
		NotumSource,
		Outdoorsman,
		InsuranceAgent,
		SpiritPhylactery,
		PistolMastery,
		PowerUp,
		Shadowsneak,
		Shadowstalker,
		Sharpshooter,
		SublimeRapport,
		ShotgunMastery,
		SMGMastery,
		SoothingSpirits,
		SpecialForces,
		SpecialistHealer,
		SpiritualMaster,
		Starfall,
		CyberneticSamurai,
		TheoreticalResearch,
		Thief,
		Tinkerer,
		TotemicRites,
		TrainingSeminar,
		Unstunnable,
		WormICE,
		Motorist,
		Demolitions,
		PiercingMastery,
		ShadeTouch,
		Reaver,
		BoneCrusher,
		Lightstalker,
		HitandRun,
		Shadow,
		Embrace,
		PowerInNumbers,
		Volunteer,
		Xuyun,
		DarkKin,
		AncientMatrix,
		Mutate,
		TheCall,
		Made,
		Loophole,
		CreativeMind,
		AncientKnowledge,
		Crusade,
		ChampionofHeavyInfantry,
		ChampionofLightInfantry,
		ChampionofLightArtillery,
		ChampionofHeavyArtillery,
		ChampionofNanoCombat,
		Ranger,
		Nanomorph,
		Counterweight,
		IllogicalPatterns,
		TheUnknownFactor,
		Opportunist,
		AlienTechnologyExpertise,
		CombatKnowledge,
		AtroxPrimaryGenome,
		AtroxSecondaryGenome,
		OpifexPrimaryGenome,
		OpifexSecondaryGenome,
		NanobreedPrimaryGenome,
		NanobreedSecondaryGenome,
		SolitusPrimaryGenome,
		SolitusSecondayGenome,
		Exploration,
		KeenEyes,
		WildernessLore,
		Gunslinger,
		WildernessSurvival,
		GameWarden,
		SafariGuide,
		EndCertification,
		ThreatAssessment,
		DirectAction,
		Fitness,
		Intuition,
		Stealth,
		Marksmanship,
		HumanResources,
		TeamBuilding,
		ProcessTheory,
		ExecutiveDecisions,
		ProfessionalDevelopment,
		MarketAwareness,
		HostileNegotiations,
		BedsideManner,
		Rehabilitation,
		Diagnosis,
		Internship,
		UndergroundDoctor,
		Toxicology,
		AggressiveSurgery,
		Endurance,
		Flexibility,
		BrawlersSense,
		Kneecapping,
		AngerManagement,
		HardLabor,
		Brutality,
		PracticalApplication,
		Serendipity,
		MechanicalAssistance,
		ProcessRefinement,
		Ergonomics,
		MilitaryHardware,
		CombatApplications,
		Acquisition,
		Subtlety,
		Cunning,
		SmugglersSense,
		RespectableBusinessman,
		FallbackPlan,
		Insurance,
		Champion,
		Exemplar,
		Wisdom,
		Loyalty,
		Judgement,
		Virtue,
		Paragon,
		Meditation,
		Empathy,
		Nimble,
		Alacrity,
		Reflex,
		Cognizance,
		Foresight,
		Sympathy,
		SpatialAwareness,
		Trauma,
		Perseverences,
		Jealousy,
		Angst,
		IntellectualRefinement,
		NanoTheory,
		ParticlePhysics,
		CombatExecution,
		PracticalUse,
		Discipline,
		KineticMastery,
		SweepandClear,
		StrategicPlanning,
		CombatSense,
		ForwardObserver,
		ClassifiedOps,
		ForceRecon,
		AssassinsAwareness,
		HonedSenses,
		KillingBlows,
		StilettoMastery,
		MaliciousForethought,
		Lithe,
		Ambushing,
		EyeforaDeal,
		FastTalk,
		SensibleInvestment,
		AggressivePricing,
		DoorToDoorSalesman,
		SensitiveNegotiations,
		HostileTakeover,
		PersonalMechanizedVehicle,
		PersonalMechanizedVehicleArmorUpgrade,
		PersonalMechanizedVehicleWeaponUpgrade,
		AntiPersonnelTurret,
		AntiPersonnelTurretArmorUpgrade,
		AntiPersonnelTurretWeaponUpgrade,
		AntiVehicularBattery,
		AntiVehicularBatteryArmorUpgrade,
		AntiVehicularBatteryWeaponUpgrade,
		MechanizedScoutVehicle,
		MechanizedScoutVehicleArmorUpgrade,
		MechanizedScoutVehicleWeaponUpgrade,
		MechanizedAssaultVehicle,
		MechanizedAssaultVehicleArmorUpgrade,
		MechanizedAssaultVehicleWeaponUpgrade,
		GlobalAdvantageHealth,
		GlobalAdvantageOffensivePower,
		GlobalAdvantageDefensivePower,
		GlobalAdvantageMedicalKnowledge,
		GlobalAdvantageCombatKnowledge,
		GlobalAdvantageNanoTechnologyKnowledge,
		PMVObsidianEdition,
		Shady,
		Adumbrated,
		Darkened,
		Benighted,
		Noctivagant,
		Noctivagous,
		Murky,
		Dark,
		Black,
		Dusky,
		Gloomy,
		Nocturnal,
		Cloudless,
		Loustrous,
		Shiny,
		Radient,
		Splendid,
		Blazing,
		Vivid,
		Bright,
		Scintillant,
		Luminous,
		Splendent,
		Metoric,
		Doubtful,
		Dubious,
		Undefinable,
		Balanced,
		Casual,
		Uncertain,
		Vague,
		Indeterminated,
		Ambigous,
		Unsettled,
		Undetermined,
		Undecided,
		NoName,
		Apotheosis
	}
	public enum PetType
	{
		Unknown = 0,
		Attack = 10,
		Heal = 11,
		Support = 12,
		Social = 14
	}
	public enum SpecialActionState
	{
		Ready,
		NotReady
	}
	public enum Side
	{
		Neutral,
		Clan,
		OmniTek,
		Monster,
		Advisor,
		Guardian,
		Gm,
		Mixed
	}
	public struct ACGItemQueryData
	{
		public int LowId;

		public int HighId;

		public int QL;
	}
	public enum PerkHash
	{
		Limber = 1279872322,
		DanceOfFools = 1145130822,
		TaintWounds = 1413568335,
		ChemicalBlindness = 1128809033,
		PoisonSprinkle = 1347638089,
		SealWounds = 1397512021,
		Tranquilizer = 1380011601,
		ToxicShock = 1414091587,
		ConcussiveShot = 1128485704,
		Assassinate = 1397967182,
		BattlegroupHeal1 = 1112819777,
		BattlegroupHeal2 = 1111574604,
		ViralCombination = 1447641935,
		BattlegroupHeal3 = 1112819781,
		BattlegroupHeal4 = 1112819764,
		BioShield = 1112101705,
		BioCocoon = 1112097603,
		BioRejuvenation = 1112101450,
		BioRegrowth = 1112101447,
		ChaoticModulation = 1128811844,
		SoftenUp = 1397708112,
		PinpointStrike = 1347310409,
		DeathStrike = 1146377033,
		LayOnHands = 1279348558,
		DevotionalArmor = 1145389389,
		CuringTouch = 1129665615,
		QuickBash = 1364542035,
		CrushBone = 1129661007,
		BringThePain = 1112101968,
		DevastatingBlow = 1146503756,
		BigSmash = 1112101709,
		FollowupSmash = 1179407187,
		BlindsideBlow = 1112097367,
		DodgeTheBlame = 1146049605,
		Succumb = 1430471509,
		ConfoundWithRules = 1129207625,
		EvasiveStance = 1161909076,
		ElementaryTeleportation = 1162630213,
		ChannelRage = 1129206341,
		BlessingOfLife = 1112428361,
		Lifeblood = 1279870533,
		DrawBlood = 1146241615,
		Leadership = 1161905221,
		Governance = 1196381765,
		TheDirector = 1414022217,
		BalanceOfYinAndYang = 1111576409,
		ReapLife = 1380011081,
		Bloodletting = 1280266052,
		VitalShock = 1447121739,
		QuickCut = 1364542292,
		Flay = 1464947798,
		FlurryOfCuts = 1180258115,
		RibbonFlesh = 1380533829,
		ReconstructDNA = 1380271182,
		ViralWipe = 1447647056,
		BreachDefenses = 1112687685,
		NanoHeal = 1413564492,
		ExplorationTeleportation = 1163416645,
		Devour = 1347374423,
		BleedingWounds = 1112299343,
		GuttingBlow = 1196769868,
		Heal = 1111772738,
		TrollForm = 1414678095,
		DisableNaturalHealing = 1145654868,
		StoneFist = 1397638729,
		Avalanche = 1096171852,
		Grasp = 1111901513,
		Bearhug = 1161908808,
		GripOfColossus = 1195986755,
		Removal1 = 1196183876,
		Removal2 = 1296320836,
		Purge1 = 1414808905,
		Purge2 = 1280725830,
		GreatPurge = 1196576853,
		Reconstruction = 1162039118,
		TauntBox = 1413562968,
		SiphonBox = 1397768792,
		ChaoticEnergy = 1129596242,
		RegainNano = 1380404814,
		NCUBooster = 1314210383,
		LaserPaintTarget = 1279348809,
		WeaponBash = 1464156755,
		TriangulateTarget = 1414681665,
		NapalmSpray = 1313887058,
		MarkOfVengeance = 1296781142,
		MarkOfSufferance = 1296125779,
		MarkOfTheUnclean = 1296781128,
		MarkOfTheUnhallowed = 1296387925,
		ArmorPiercingShot = 1095585865,
		FindTheFlaw = 1178883142,
		CalledShot = 1480869706,
		TremorHand = 1414678606,
		HarmonizeBodyAndMind = 1481132629,
		Taunt = 1195661896,
		Charge = 1113085272,
		Headbutt = 1415075908,
		Hatred = 1313691476,
		GroinKick = 1196378955,
		RepairPet = 1380274245,
		Deconstruction = 1414681923,
		EncaseInStone = 1162758483,
		DetonateStoneworks = 1146377038,
		SkillDrainRemoval = 1397248589,
		ShutdownRemoval = 1398100549,
		EnhancedHeal = 1162364993,
		TeamHeal = 1413564481,
		MaliciousProhibition = 1296126031,
		TreatmentTransfer = 1414681682,
		ZapNano = 1514229313,
		NanoShakes = 1313755976,
		StripNano = 1398033985,
		AnnihilateNotumMolecules = 1095323215,
		FadeAnger = 1178681671,
		TapNotumSource = 1413566036,
		AccessNotumSource = 1094929999,
		BlastNano = 1112297038,
		StopNotumFlow = 1398034004,
		NotumOverflow = 1313820502,
		Stoneworks = 1330529623,
		InsuranceClaim = 1230193484,
		CaptureVigor = 1129338441,
		UnsealedBlight = 1430340172,
		CaptureEssence = 1129334099,
		UnsealedPestilence = 1431195731,
		CaptureSpirit = 1129337673,
		UnsealedContagion = 1431192391,
		CaptureVitality = 1129600585,
		QuickShot = 1363759944,
		DoubleShot = 1145197384,
		Deadeye = 1313821517,
		Energize = 1163020105,
		PowerVolley = 1347376719,
		PowerShock = 1347375944,
		PowerBlast = 1347371596,
		PowerCombo = 1347896143,
		FadeArmor = 1178681677,
		ShadowBullet = 1396982357,
		NightKiller = 1313426249,
		ShadowStab = 1397248834,
		BladeOfNight = 1111969614,
		ShadowKiller = 1397246796,
		SnipeShot1 = 1397642056,
		SnipeShot2 = 1397773106,
		Exultation = 1163416908,
		EtherealTouch = 1163154511,
		DimensionalFist = 1145652809,
		Disorientate = 1230196562,
		ConvulsiveTremor = 1129206861,
		Symbiosis = 1498235465,
		LegShot = 1279742792,
		EasyShot = 1161909064,
		PointBlank = 1245861698,
		ReinforceSlugs = 1380275020,
		JarringBurst = 1245790805,
		SolidSlug = 1397510997,
		NeutroniumSlug = 1313166156,
		SpiritOfBlessing = 1398034242,
		SpiritOfPurity = 1397772117,
		FieldBandage = 1178944078,
		Tracer = 1414676803,
		ContainedBurst = 1129202261,
		Violence = 1162167620,
		Guardian = 1095910473,
		Cure1 = 1229540439,
		Vaccinate1 = 1297369165,
		Cure2 = 1296845127,
		Vaccinate2 = 1398032726,
		HaleAndHearty = 1212236100,
		TeamHaleAndHearty = 1413826636,
		Dragonfire = 1380009807,
		ChiConductor = 1128874820,
		Incapacitate = 1229865793,
		FleshQuiver = 1179406665,
		Obliterate = 1329744969,
		DazzleWithLights = 1146771273,
		Combust = 1465013323,
		ThermalDetonation = 1413825620,
		Supernova = 1346720334,
		DeepCuts = 1145389908,
		BladeWhirlwind = 1112299346,
		HonoringTheAncients = 1213158472,
		SeppukuSlash = 1363824464,
		QuarkContainmentField = 1364542286,
		AccelerateDecayingQuarks = 1095058501,
		KnowledgeEnhancer = 1263420750,
		Escape = 1162500172,
		SabotageQuarkField = 1396789569,
		IgnitionFlare = 1229407820,
		RitualOfDevotion = 1380732740,
		DevourVigor = 1145394761,
		RitualOfZeal = 1381257050,
		DevourEssence = 1145390419,
		RitualOfSpirit = 1381322579,
		DevourVitality = 1146050121,
		RitualOfBlood = 1381257026,
		ECM1 = 1330529872,
		ECM2 = 1431717713,
		InstallExplosiveDevice = 1230194008,
		InstallNotumDepletionDevice = 1229868623,
		SuppressivePrimer = 1397903433,
		ThermalPrimer = 1414680658,
		Stab = 1230525772,
		DoubleStab = 1146049345,
		Perforate = 1346720326,
		Lacerate = 1279345477,
		Impale = 1229803585,
		Gore = 1262573392,
		Hecatomb = 1162035540,
		Atrophy = 1414680400,
		ConsumeTheSoul = 1129272403,
		DoomTouch = 1146049615,
		SpiritDissolution = 1397769292,
		Cleave = 1129071937,
		Transfix = 1380011603,
		PainLance = 1346456641,
		SliceAndDice = 1396785481,
		Pulverize = 1347767382,
		HammerAndAnvil = 1213022532,
		OverwhelmingMight = 1329941831,
		SeismicSmash = 1397576525,
		LightBullet = 1279869525,
		PowerOfLight = 1346850644,
		LightKiller = 1279871820,
		TriggerHappy = 1413957697,
		EatBullets = 1163149909,
		Blur = 1246384717,
		Diffuse = 1145652806,
		ChaosRitual = 1296781136,
		Mistreatment = 1380270420,
		CloseCall = 1363427654,
		NanoTransmission = 1314149459,
		SupressiveHorde = 1397770319,
		Clipfever = 1129072197,
		MuzzleOverload = 1297764182,
		TapVitae = 1413568073,
		Sacrifice = 1330204482,
		PurpleHeart = 1347766341,
		RedDawn = 1380271191,
		Moonmist = 1330597453,
		RedDusk = 1380271189,
		PowerBolt = 1347895884,
		Numb = 1497715013,
		Cripple = 1230000208,
		FlimFocus = 1179403843,
		Utilize = 1431587148,
		ProgramOverload = 1347571542,
		ArouseAnger = 1096106318,
		CauseOfAnger = 1129533249,
		Highway = 1229408343,
		Beckoning = 1347704906,
		NocturnalStrike = 1112101707,
		Awakening = 1096237387,
		Recalibrate = 1162035532,
		SilentPlague = 1397313612,
		TheShot = 1414812488,
		Puppeteer = 1347768400,
		Antitrust = 1314146644,
		Overrule = 1447383634,
		FreakShield = 1163022933,
		Medallion = 1162101068,
		OptimizeBotProtocol = 1330659919,
		KenFi = 1262831177,
		KenSi = 1262834505,
		KaMon = 1262570830,
		ForceOpponent = 1179602768,
		Insight = 1246188615,
		Purify = 1430996806,
		Bluntness = 1431524936,
		Break = 1196775753,
		Crave = 1095717199,
		Bore = 1263294031,
		Collapser = 1129270348,
		Implode = 1297108047,
		Fuzz = 1129863002,
		FireFrenzy = 1179797074,
		NanoFeast = 1313752641,
		BotConfinement = 1497847892,
		Clearshot = 1161908819,
		Popshot = 1347375187,
		Clearsight = 1381189959,
		Tick = 1296846676,
		AssumeTarget = 1095980114,
		PeelLayers = 1346718785,
		FullFrontal = 1179993682,
		Confinement = 1313229134,
		Guesstimate = 1196770643,
		MemoryScrabble = 1296388931,
		HostileTakeover = 1213158465,
		ChaoticAssumption = 1129595221,
		OpportunityKnocks = 1330662222,
		ControlledChance = 1129268035,
		InitialStrike = 1229542226,
		RedeemLastWish = 1380273217,
		BodyTackle = 1330664264,
		MongoRage = 1296519749,
		MyOwnFortress = 1464815430,
		WitOfTheAtrox = 1464225620,
		Opening = 1398233417,
		Derivate = 1163020630,
		BlindedByDelights = 1380537921,
		DizzyingHeights = 1145653321,
		Reject = 1179539526,
		Sword = 1314017621,
		Pen = 1346719314,
		NotumShield = 1313821508,
		TackyHack = 1413564483,
		Sphere = 1397770309,
		Feel = 1297041474,
		Survival = 1398100566,
		LEProcAdventurerCharringBlow = 1296581199,
		LEProcAdventurerAesirAbsorption = 1397705028,
		LEProcAdventurerMacheteFlurry = 1296254540,
		LEProcAdventurerHealingHerbs = 1212237890,
		LEProcAdventurerBasicDressing = 1347635282,
		LEProcAdventurerSoothingHerbs = 1398032450,
		LEProcAdventurerSkinProtection = 1397049667,
		LEProcAdventurerMacheteSlice = 1296257868,
		LEProcAdventurerRestoreVigor = 1279608914,
		LEProcAdventurerCombustion = 1112822866,
		LEProcAdventurerFerociousHits = 1464618305,
		LEProcAdventurerSelfPreservation = 1145197381,
		LEProcAgentLaserAim = 1111577165,
		LEProcAgentDisableCuffs = 1347310415,
		LEProcAgentGrimReaper = 1464554561,
		LEProcAgentMinorNanobotEnhance = 1464160321,
		LEProcAgentImprovedFocus = 1413562956,
		LEProcAgentCellKiller = 1329941332,
		LEProcAgentIntenseMetabolism = 1229538903,
		LEProcAgentPlasteelPiercingRounds = 1280136015,
		LEProcAgentBrokenAnkle = 1481851730,
		LEProcAgentNoEscape = 1162433352,
		LEProcAgentNotumChargedRounds = 1196774223,
		LEProcAgentNanoEnhancedTargeting = 1095520333,
		LEProcBureaucratFormsInTriplicate = 1179601236,
		LEProcBureaucratWrongWindow = 1465014094,
		LEProcBureaucratTaxAudit = 1415070025,
		LEProcBureaucratPleaseHold = 1280593985,
		LEProcBureaucratInflationAdjustment = 1229340996,
		LEProcBureaucratWaitInThatQueue = 1346720323,
		LEProcBureaucratDeflation = 1398166355,
		LEProcBureaucratNextWindowOver = 1314412356,
		LEProcBureaucratLostPaperwork = 1346717775,
		LEProcBureaucratMobilityEmbargo = 1314214988,
		LEProcBureaucratPapercut = 1346459477,
		LEProcBureaucratSocialServices = 1514685775,
		LEProcDoctorHealingCare = 1498502234,
		LEProcDoctorAntiseptic = 1096177490,
		LEProcDoctorInflammation = 1381188174,
		LEProcDoctorAstringent = 1296908628,
		LEProcDoctorAnesthetic = 1263289936,
		LEProcDoctorBloodTransfusion = 1145979733,
		LEProcDoctorPathogen = 1128874835,
		LEProcDoctorMassiveVitaePlan = 1229931077,
		LEProcDoctorRestrictiveBandaging = 1414547023,
		LEProcDoctorAnatomicBlight = 1414025031,
		LEProcDoctorDangerousCulture = 1111774273,
		LEProcDoctorMuscleMemory = 1313229889,
		LEProcEnforcerInspireIre = 1112754266,
		LEProcEnforcerShieldOfTheOgre = 1229867077,
		LEProcEnforcerViolationBuffer = 1112886085,
		LEProcEnforcerRagingBlow = 1380532823,
		LEProcEnforcerBustKneecaps = 1480807238,
		LEProcEnforcerShrugOffHits = 1447973709,
		LEProcEnforcerAirOfHatred = 1413827655,
		LEProcEnforcerVileRage = 1230195026,
		LEProcEnforcerTearLigaments = 1111577427,
		LEProcEnforcerVortexOfHate = 1112689735,
		LEProcEnforcerIgnorePain = 1229410377,
		LEProcEnforcerInspireRage = 1230197319,
		LEProcEngineerEnergyTransfer = 1145654611,
		LEProcEngineerReactiveArmor = 1146377031,
		LEProcEngineerDestructiveTheorem = 1380274768,
		LEProcEngineerDroneMissiles = 1145394248,
		LEProcEngineerCushionBlows = 1146242392,
		LEProcEngineerCongenialEncasement = 1381254213,
		LEProcEngineerSplinterPreservation = 1162171474,
		LEProcEngineerDestructiveSignal = 1095717441,
		LEProcEngineerEndureBarrage = 1146245699,
		LEProcEngineerAssaultForceRelief = 1380995154,
		LEProcEngineerPersonalProtection = 1145395030,
		LEProcEngineerDroneExplosives = 1112425541,
		LEProcFixerDirtyTricks = 1162630233,
		LEProcFixerBendingTheRules = 1146246226,
		LEProcFixerSlipThemAMickey = 1179075400,
		LEProcFixerContaminatedBullets = 1145394241,
		LEProcFixerFishInABarrel = 1230195529,
		LEProcFixerIntenseMetabolism = 1179538255,
		LEProcFixerEscapeTheSystem = 1179208014,
		LEProcFixerBootlegRemedies = 1195725889,
		LEProcFixerUndergroundSutures = 1095259201,
		LEProcFixerFightingChance = 1112429640,
		LEProcFixerBackyardBandages = 1397314637,
		LEProcFixerLucksCalamity = 1196774482,
		LEProcKeeperVirtuousReaper = 1179144773,
		LEProcKeeperSymbioticBypass = 1380537165,
		LEProcKeeperAmbientPurification = 1178945877,
		LEProcKeeperRighteousSmite = 1179013701,
		LEProcKeeperRighteousStrike = 1179931461,
		LEProcKeeperEschewTheFaithless = 1146770517,
		LEProcKeeperPureStrike = 1163088981,
		LEProcKeeperSubjugation = 1380467279,
		LEProcKeeperIgnoreTheUnrepentant = 1163154517,
		LEProcKeeperHonorRestored = 1129202757,
		LEProcKeeperFaithfulReconstruction = 1398232143,
		LEProcKeeperBenevolentBarrier = 1380537172,
		LEProcMartialArtistStrengthenKi = 1398033225,
		LEProcMartialArtistSmashingFist = 1397245523,
		LEProcMartialArtistSelfReconstruction = 1397510739,
		LEProcMartialArtistMedicinalRemedy = 1296388685,
		LEProcMartialArtistAttackLigaments = 1096043593,
		LEProcMartialArtistStrengthenSpirit = 1096042573,
		LEProcMartialArtistAbsoluteFist = 1094862409,
		LEProcMartialArtistStingingFist = 1398031955,
		LEProcMartialArtistDisruptKi = 1146243913,
		LEProcMartialArtistHealingMeditation = 1212960068,
		LEProcMartialArtistDebilitatingStrike = 1145197396,
		LEProcMetaPhysicistEgoStrike = 1196837713,
		LEProcMetaPhysicistAnticipatedEvasion = 1398228037,
		LEProcMetaPhysicistSuppressFury = 1397703763,
		LEProcMetaPhysicistDiffuseRage = 1296385093,
		LEProcMetaPhysicistEconomicNanobotUse = 1162302292,
		LEProcMetaPhysicistSowDoubt = 1398228047,
		LEProcMetaPhysicistRegainFocus = 1229673298,
		LEProcMetaPhysicistMindWail = 1212240981,
		LEProcMetaPhysicistNanobotContingentArrest = 1178949448,
		LEProcMetaPhysicistSowDespair = 1347310663,
		LEProcMetaPhysicistThoughtfulMeans = 1163284553,
		LEProcMetaPhysicistSuperEgoStrike = 1380271683,
		LEProcNanoTechnicianLoopingService = 1431522377,
		LEProcNanoTechnicianThermalReprieve = 1347900245,
		LEProcNanoTechnicianHarvestEnergy = 1128877135,
		LEProcNanoTechnicianOptimizedLibrary = 1330465858,
		LEProcNanoTechnicianCircularLogic = 1346851668,
		LEProcNanoTechnicianIncreaseMomentum = 1229147471,
		LEProcNanoTechnicianSourceTap = 1364218711,
		LEProcNanoTechnicianLayeredAmnesty = 1397248588,
		LEProcNanoTechnicianAcceleratedReality = 1163086928,
		LEProcNanoTechnicianUnstableLibrary = 1146311237,
		LEProcNanoTechnicianPoweredNanoFortress = 1414677832,
		LEProcSoldierTargetAcquired = 1196573778,
		LEProcSoldierOnTheDouble = 1179992397,
		LEProcSoldierGrazeJugularVein = 1381256527,
		LEProcSoldierFuriousAmmunition = 1229865293,
		LEProcSoldierShootArtery = 1397899609,
		LEProcSoldierDeepSixInitiative = 1162691137,
		LEProcSoldierEmergencyBandages = 1163018573,
		LEProcSoldierConcussiveShot = 1095188813,
		LEProcSoldierGearAssaultAbsorption = 1128617037,
		LEProcSoldierFuseBodyArmor = 1381190981,
		LEProcSoldierSuccessfulTargeting = 1129730888,
		LEProcSoldierReconditioned = 1398096452,
		LEProcShadeShadowedGift = 1396787014,
		LEProcShadeSiphonBeing = 1397768775,
		LEProcShadeTwistedCaress = 1347179342,
		LEProcShadeBlackheart = 1129007173,
		LEProcShadeDeviousSpirit = 1146508112,
		LEProcShadeMisdirection = 1396984146,
		LEProcShadeConcealedSurprise = 1213354838,
		LEProcShadeToxicConfusion = 1330529877,
		LEProcShadeElusiveSpirit = 1163219794,
		LEProcShadeBlackenedLegacy = 1111706695,
		LEProcShadeSapLife = 1397771337,
		LEProcShadeDrainEssence = 1146242387,
		LEProcTraderExchangeProduct = 1096108366,
		LEProcTraderUnopenedLetter = 1195724622,
		LEProcTraderRigidLiquidation = 1145458777,
		LEProcTraderAccumulatedInterest = 1163084626,
		LEProcTraderRebate = 1313882454,
		LEProcTraderRefinanceLoans = 1145456965,
		LEProcTraderEscrow = 1179599938,
		LEProcTraderUnexpectedBonus = 1430602318,
		LEProcTraderUnforgivenDebts = 1380338753,
		LEProcTraderDebtCollection = 1431327317,
		LEProcTraderPaymentPlan = 1348030540,
		LEProcTraderDepleteAssets = 1162039378,
		RighteousWrath = 1380538194,
		UnhallowedWrath = 1431197522,
		SpectatorWrath = 1396922194,
		RighteousFury = 1380533845,
		UnhallowedFury = 1431193173,
		GravityShift = 1162757970
	}
	public enum UseCriteriaOperator
	{
		EqualTo = 0,
		LessThan = 1,
		GreaterThan = 2,
		Or = 3,
		And = 4,
		TimeLess = 5,
		TimeLarger = 6,
		ItemHas = 7,
		ItemHasnot = 8,
		Id = 9,
		TargetId = 10,
		TargetSignal = 11,
		TargetStat = 12,
		PrimaryItem = 13,
		SecondaryItem = 14,
		AreaZMinMax = 15,
		User = 16,
		ItemAnim = 17,
		OnTarget = 18,
		OnSelf = 19,
		Signal = 20,
		OnSecondaryItem = 21,
		BitAnd = 22,
		BitOr = 23,
		Unequal = 24,
		Illegal = 25,
		OnUser = 26,
		OnValidTarget = 27,
		OnInvalidTarget = 28,
		OnValidUser = 29,
		OnInvalidUser = 30,
		HasWornItem = 31,
		HasNotWornItem = 32,
		HasWieldedItem = 33,
		HasNotWieldedItem = 34,
		HasFormula = 35,
		HasNotFormula = 36,
		OnGeneralBeholder = 37,
		IsValid = 38,
		IsInvalid = 39,
		IsAlive = 40,
		IsWithinVicinity = 41,
		Not = 42,
		IsWithinWeaponrange = 43,
		IsNpc = 44,
		IsFighting = 45,
		IsAttacked = 46,
		IsAnyoneLooking = 47,
		IsFoe = 48,
		IsInDungeon = 49,
		IsSameAs = 50,
		DistanceTo = 51,
		IsInNoFightingArea = 52,
		TemplateCompare = 53,
		MinMaxLevelCompare = 54,
		MonsterTemplate = 57,
		HasMaster = 58,
		CanExecuteFormulaIOnTarget = 59,
		AreaTargetInVicinity = 60,
		IsUnderHeavyAttack = 61,
		IsLocationOk = 62,
		IsNotTooHighLevel = 63,
		HasChangedRoomWhileFighting = 64,
		KullNumberOf = 65,
		TestNumPets = 66,
		NumberOfItems = 67,
		PrimaryTemplate = 68,
		IsTeleporting = 69,
		IsFlying = 70,
		ScanForStat = 71,
		HasMeOnPetList = 72,
		TrickleDownLarger = 73,
		TrickleDownLess = 74,
		IsPetOverEquipped = 75,
		HasPetPendingNanoFormula = 76,
		IsPet = 77,
		CanAttackChar = 79,
		IsTowerCreateAllowed = 80,
		InventorySlotIsFull = 81,
		InventorySlotIsEmpty = 82,
		CanDisableDefenseShield = 83,
		IsNpcOrNpcControlledPet = 84,
		SameAsSelectedTarget = 85,
		IsPlayerOrPlayerControlledPet = 86,
		HasEnteredNonPvpZone = 87,
		UseLocation = 88,
		IsFalling = 89,
		IsOnDifferentPlayfield = 90,
		HasRunningNano = 91,
		HasRunningNanoLine = 92,
		HasPerk = 93,
		IsPerkLocked = 94,
		IsFactionReactionSet = 95,
		HasMoveToTarget = 96,
		IsPerkUnlocked = 97,
		True = 98,
		False = 99,
		OnCaster = 100,
		HasNotRunningNano = 101,
		HasNotRunningNanoLine = 102,
		HasNotPerk = 103,
		HasFreeSlots = 106,
		NotBitAnd = 107,
		ObtainedItem = 108,
		OnFightingTarget = 110,
		IsOwnPet = 118,
		HasNcuFor = 127,
		AlliesNotInCombat = 136,
		Blank = 255
	}
	public enum ItemActionInfo
	{
		UseCriteria = 3,
		Activate = 10
	}
	public enum Fatness
	{
		Thin,
		Normal,
		Fat
	}
	public enum Gender
	{
		None,
		Uni,
		Male,
		Female
	}
	public enum IdentityType
	{
		None = 0,
		WeaponPage = 101,
		ArmorPage = 102,
		ImplantPage = 103,
		Inventory = 104,
		BankByRef = 105,
		Reclaim = 106,
		Backpack = 107,
		KnuBotTradeWindow = 108,
		OverflowWindow = 110,
		TradeWindow = 111,
		SocialPage = 115,
		ShopInventory = 1895,
		PlayerShopInventory = 1936,
		PlayfieldUnk = 40003,
		Playfield2 = 40016,
		SimpleChar = 50000,
		CityController = 50200,
		Terminal = 51005,
		Door = 51016,
		Container = 51017,
		WeaponInstance = 51018,
		VendingMachine = 51035,
		TempBag = 51047,
		Corpse = 51050,
		MissionKey = 51053,
		MissionKeyDuplicator = 51054,
		MailTerminal = 51059,
		ProxyInstance = 51069,
		DummyItem = 51080,
		PerkHash = 51086,
		Battlestation = 51092,
		PlayfieldProxy = 51100,
		Playfield = 51101,
		ACGBuildingGeneratorData = 51103,
		NanoProgram = 53019,
		GfxEffect = 53030,
		MissionTerminal = 56001,
		Mission = 56003,
		ACGEntrance = 56006,
		TeamWindow = 57001,
		Organization = 57002,
		Bank = 57005,
		SpecialAction = 57008,
		MobHash = 70099,
		Playfield3 = 100001
	}
	public enum DBIdentityType
	{
		RDBPlayfield = 1000001,
		Texture = 1010004,
		LandControlMap = 1000008,
		RDBTilemap = 1000009,
		InfoObject = 1000010,
		SurfaceResource = 1000013,
		PlayfieldDistrictInfo = 1000014,
		Mesh = 1010001
	}
	public struct Identity
	{
		[AoMember(0)]
		public IdentityType Type { get; set; }

		[AoMember(1)]
		public int Instance { get; set; }

		public static Identity None => new Identity(IdentityType.None, 0);

		public Identity(IdentityType type, int instance)
		{
			Type = type;
			Instance = instance;
		}

		public Identity(int instance)
		{
			Type = IdentityType.SimpleChar;
			Instance = instance;
		}

		public override string ToString()
		{
			if (Type == IdentityType.MobHash)
			{
				return $"({Type}:{Encoding.ASCII.GetString(BitConverter.GetBytes(Instance))})";
			}
			return string.Format("({0}:{1})", Type, Instance.ToString("X4"));
		}

		public static bool operator ==(Identity identity1, IdentityType Type)
		{
			return identity1.TypeEquals(Type);
		}

		public static bool operator !=(Identity identity1, IdentityType Type)
		{
			return !identity1.TypeEquals(Type);
		}

		public static bool operator ==(Identity identity1, Identity identity2)
		{
			return identity1.Equals(identity2);
		}

		public static bool operator !=(Identity identity1, Identity identity2)
		{
			return !identity1.Equals(identity2);
		}

		public bool TypeEquals(object obj)
		{
			return obj is IdentityType && Type.Equals((IdentityType)obj);
		}

		public override bool Equals(object obj)
		{
			return obj is Identity && Type.Equals(((Identity)obj).Type) && Instance.Equals(((Identity)obj).Instance);
		}

		public override int GetHashCode()
		{
			int num = 17;
			num = 23 * num + Type.GetHashCode();
			return 23 * num + Instance.GetHashCode();
		}
	}
	public struct DBIdentity
	{
		public DBIdentityType Type;

		public int Instance;

		public DBIdentity(DBIdentityType type, int instance)
		{
			Type = type;
			Instance = instance;
		}
	}
	public class Mesh
	{
		public List<Vector3> Vertices;

		public List<int> Triangles;

		public Quaternion Rotation;

		public Vector3 Position;

		public Vector3 Scale;

		public Matrix4x4 LocalToWorldMatrix
		{
			get
			{
				Matrix4x4 matrix4x = Matrix4x4.Translate(Position);
				Matrix4x4 matrix4x2 = Matrix4x4.Rotate(Rotation);
				Matrix4x4 matrix4x3 = Matrix4x4.Scale(Scale);
				return matrix4x * matrix4x2 * matrix4x3;
			}
		}

		public Mesh()
		{
			Scale = new Vector3(1f, 1f, 1f);
		}
	}
	public enum MissionScope : byte
	{
		Solo = 1,
		Team
	}
	public enum MissionActionType
	{
		KillPerson = 1,
		UseItemOnItem = 8,
		FindItem = 15,
		FindPerson = 16,
		KillMultiPerson = 20
	}
	public enum MissionDirection
	{
		Ascending,
		Descending,
		Boss
	}
	public static class MissionDirectionMethods
	{
		public static MissionDirection Invert(this MissionDirection direction)
		{
			return (direction != MissionDirection.Descending) ? MissionDirection.Descending : MissionDirection.Ascending;
		}
	}
	public enum MovementAction : byte
	{
		ForwardStart = 1,
		ForwardStop,
		BackwardStart,
		BackwardStop,
		StrafeRightStart,
		StrafeRightStop,
		StrafeLeftStart,
		StrafeLeftStop,
		TurnRightStart,
		TurnRightMouse,
		TurnRightStop,
		TurnLeftStart,
		TurnLeftMouse,
		TurnLeftStop,
		JumpStart,
		JumpStop,
		ElevateUpStart,
		ElevateUpStop,
		ElevateDownStart,
		ElevateDownStop,
		FullStop,
		Update,
		SwitchToFrozen,
		SwitchToWalk,
		SwitchToRun,
		SwitchToSwim,
		SwitchToCrawl,
		SwitchToSneak,
		SwitchToFly,
		SwitchToSit,
		Unknown0x1f,
		Unknown0x20,
		SwitchToSleep,
		SwitchToLounge,
		LeaveSwim,
		LeaveSneak,
		LeaveSit,
		LeaveFrozen,
		LeaveFly,
		LeaveCrawl,
		LeaveSleep,
		LeaveLounge
	}
	public enum MovementState
	{
		Rooted = 1,
		Walk,
		Run,
		Swim,
		Crawl,
		Sneak,
		Fly,
		Sit
	}
	public enum NanoSchool
	{
		None,
		Combat,
		Medical,
		Protection,
		Psi,
		Space
	}
	public enum NanoLine
	{
		NOSTACKING = 0,
		DamageShields = 1,
		ReflectShield = 2,
		ArmorBuff = 3,
		DamageBuffs_LineA = 4,
		Challenger = 5,
		DOT_LineA = 6,
		DOT_LineB = 7,
		DOTNanotechnicianStrainA = 8,
		DOTAgentStrainA = 9,
		DOTNanotechnicianStrainB = 10,
		HaloNanoDebuff = 11,
		HealOverTime = 12,
		AAODebuffs = 13,
		NanoOverTime_LineA = 14,
		XPBonus = 15,
		General1HandBluntBuff = 16,
		General1HandBluntDebuff = 17,
		GeneralAimedShotBuff = 18,
		GeneralAimedShotDebuff = 19,
		GeneralAirTransportBuff = 20,
		General1HEdgedBuff = 21,
		General1HEdgedDebuff = 22,
		General2HBluntBuff = 23,
		General2HBluntDebuff = 24,
		General2HEdgedBuff = 25,
		General2HEdgedDebuff = 26,
		GeneralAssaultRifleBuff = 27,
		GeneralAssaultRifleDebuff = 28,
		GeneralAgilityBuff = 29,
		GeneralIntelligenceBuff = 30,
		GeneralPsychicBuff = 31,
		GeneralSenseBuff = 32,
		GeneralStaminaBuff = 33,
		GeneralStrengthBuff = 34,
		GeneralBioMetBuff = 35,
		GeneralBioMetDebuff = 36,
		GeneralBowBuff = 37,
		GeneralBowDebuff = 38,
		GeneralBowSpecialBuff = 39,
		GeneralBowSpecialDebuff = 40,
		GeneralBrawlBuff = 41,
		GeneralBrawlDebuff = 42,
		GeneralBreakEntryBuff = 43,
		GeneralBurstBuff = 44,
		GeneralBurstDebuff = 45,
		GeneralChemicalACBuff = 46,
		GeneralChemistryBuff = 47,
		GeneralClimbBuff = 48,
		GeneralColdACBuff = 49,
		GeneralComputerLiteracyBuff = 50,
		GeneralConcealmentBuff = 51,
		GeneralDimachDebuff = 52,
		GeneralAgilityDebuff = 53,
		GeneralIntelligenceDebuff = 54,
		GeneralPsychicDebuff = 55,
		GeneralSenseDebuff = 56,
		GeneralStaminaDebuff = 57,
		GeneralStrengthDebuff = 58,
		GeneralDisarmTrapsBuff = 59,
		GeneralElectricalEngineeringBuff = 60,
		GeneralEnergyMeleeBuff = 61,
		GeneralEnergyMeleeDebuff = 62,
		GeneralEnergyACBuff = 63,
		GeneralLREnergyWeaponBuff = 64,
		GeneralLREnergyWeaponDebuff = 65,
		GeneralFastAttackBuff = 66,
		GeneralFastAttackDebuff = 67,
		GeneralFieldQuantumPhysicsBuff = 68,
		GeneralFireACBuff = 69,
		GeneralFirstAidBuff = 70,
		GeneralFlingShotBuff = 71,
		GeneralFlingShotDebuff = 72,
		GeneralFullAutoBuff = 73,
		GeneralFullAutoDebuff = 74,
		GeneralThrownGrapplingBuff = 75,
		GeneralThrownGrapplingDebuff = 76,
		GeneralGrenadeBuff = 77,
		GeneralGrenadeDebuff = 78,
		GeneralGroundTransportBuff = 79,
		GeneralMaxHealthBuff = 80,
		GeneralKnifeBuff = 81,
		GeneralKnifeDebuff = 82,
		GeneralSMGBuff = 83,
		GeneralSMGDebuff = 84,
		GeneralMartialArtsBuff = 85,
		GeneralMartialArtsDebuff = 86,
		GeneralMatCreaBuff = 87,
		GeneralMatCreaDebuff = 88,
		GeneralMatLocBuff = 89,
		GeneralMatLocDebuff = 90,
		GeneralMatMetBuff = 91,
		GeneralMatMetDebuff = 92,
		GeneralMechanicalEngineeringBuff = 93,
		GeneralMeleeACBuff = 94,
		GeneralNanoProgrammingBuff = 95,
		GeneralNanoACBuff = 96,
		GeneralNPRegeneration = 97,
		GeneralDeflectBuff = 98,
		GeneralDeflectDebuff = 99,
		GeneralPharmaceuticalBuff = 100,
		GeneralPiercingBuff = 101,
		GeneralPiercingDebuff = 102,
		GeneralPistolBuff = 103,
		GeneralPistoDebuff = 104,
		GeneralPoisonACBuff = 105,
		GeneralProjectileACBuff = 106,
		GeneralPsychologyBuff = 107,
		GeneralPsyModBuff = 108,
		GeneralPsyModDebuff = 109,
		GeneralRadiationACBuff = 110,
		GeneralHPRegeneration = 111,
		GeneralRifleBuff = 112,
		GeneralRifleDebuff = 113,
		GeneralRiposteBuff = 114,
		GeneralRiposteDebuff = 115,
		GeneralSenseImpBuff = 116,
		GeneralSenseImpDebuff = 117,
		GeneralShotgunBuff = 118,
		GeneralShotgunDebuff = 119,
		GeneralSneakAttackBuff = 120,
		GeneralSneakAttackDebuff = 121,
		GeneralNanoACDebuff = 122,
		GeneralPoisonACDebuff = 123,
		GeneralSwimBuff = 124,
		GeneralTreatmentBuff = 125,
		GeneralTutoringBuff = 126,
		GeneralChemicalACDebuff = 127,
		GeneralColdACDebuff = 128,
		GeneralEnergyACDebuff = 129,
		GeneralFireACDebuff = 130,
		GeneralMeleeACDebuff = 131,
		GeneralProjectileACDebuff = 132,
		GeneralRadiationACDebuff = 133,
		GeneralWeaponSmithingBuff = 134,
		TraderSkillTransferTargetDebuff_Deprive = 135,
		TraderSkillTransferTargetDebuff_Ransack = 136,
		TraderSkillTransferCasterBuff_Deprive = 137,
		TraderSkillTransferCasterBuff_Ransack = 138,
		TraderACTransferTargetDebuff_Siphon = 139,
		TraderACTransferTargetDebuff_Draw = 140,
		TraderACTransferCasterBuff_Siphon = 141,
		TraderACTransferCasterBuff_Draw = 142,
		TraderACTransferTargetBuff_Redeem = 143,
		MajorEvasionBuffs = 144,
		Snare = 145,
		Root = 146,
		Mezz = 147,
		NPCostBuff = 148,
		GeneralRunspeedBuffs = 149,
		RunspeedBuffs = 150,
		HPBuff = 151,
		InitiativeBuffs = 152,
		_2HEdgedBuff = 153,
		BrawlBuff = 154,
		RiposteBuff = 155,
		StrengthBuff = 156,
		MatMetBuff = 157,
		MatMetDebuff = 158,
		MatCreaBuff = 159,
		MatCreaDebuff = 160,
		MatLocBuff = 161,
		MatLocDebuff = 162,
		BioMetBuff = 163,
		BioMetDebuff = 164,
		SenseImpBuff = 165,
		SenseImpDebuff = 166,
		PsyModBuff = 167,
		PsyModDebuff = 168,
		PsychicDebuff = 169,
		IntelligenceDebuff = 170,
		Break_EntryBuffs = 171,
		ElectricalEngineeringBuff = 172,
		FieldQuantumPhysicsBuff = 173,
		MechanicalEngineeringBuff = 174,
		PharmaceuticalsBuff = 175,
		WeaponSmithingBuff = 176,
		ComputerLiteracyBuff = 177,
		NPBuff = 178,
		_1HBluntBuff = 179,
		MeleeWeaponBuffLine = 180,
		NFRangeBuff = 181,
		CriticalIncreaseBuff = 182,
		InterruptModifier = 183,
		DoctorHPBuffs = 184,
		DoctorShortHPBuffs = 185,
		InitiativeDebuffs = 186,
		MetaPhysicistDamageDebuff = 187,
		MongoBuff = 188,
		Rage = 189,
		FirstAidAndTreatmentBuff = 190,
		PerceptionBuffs = 191,
		SenseBuff = 192,
		ConcealmentBuff = 193,
		RifleBuffs = 194,
		AgilityBuff = 195,
		Chemistry_PharmBuff = 196,
		EvasionDebuffs = 197,
		AimedShotBuffs = 198,
		PistolBuff = 199,
		PsychologyBuff = 200,
		NanoDeltaBuffs = 201,
		CharmOther = 202,
		HealDeltaBuff = 203,
		NanoResistanceBuffs = 204,
		Breaking_Entry_DisarmTrapsBuff = 206,
		GrenadeBuffs = 207,
		SneakAttackBuffs = 208,
		MartialArtsBuff = 209,
		NanoProgrammingBuff = 210,
		NPCostDebuff = 211,
		AssaultRifleBuffs = 212,
		RangedEnergyWeaponBuffs = 213,
		BurstBuff = 214,
		NanoDrain_LineA = 215,
		MPPetDamageBuffs = 216,
		MPPetInitiativeBuffs = 217,
		FalseProfession = 218,
		AbsorbACBuff = 219,
		TraderTeamSkillWranglerBuff = 220,
		MetaphysicistMindDamageNanoDebuffs = 221,
		ControlledDestructionBuff = 222,
		Polymorph = 223,
		Fortify = 224,
		PetShortTermDamageBuffs = 225,
		ElianSoul = 226,
		EngineerAuras = 227,
		EngineerAura_Armour = 228,
		EngineerAura_DamageBuff = 229,
		EngineerAura_DamageShieldBuff = 230,
		EngineerAura_ReflectionDamageBuff = 231,
		PetTauntBuff = 232,
		SpeechLine = 233,
		MotivationalSpeechEffect = 234,
		DisarmTrapBuff = 235,
		EngineerDebuffAuras = 236,
		MotivationalSpeechNanoResistBuff = 237,
		DemotivationalSpeeches = 238,
		NanoShutdownDebuff = 239,
		ConcentrationCriticalLine = 240,
		SureshotCriticalLine = 241,
		ExecutionerBuff = 242,
		DamageShieldUpgrades = 243,
		_1HEdgedBuff = 244,
		MultiwieldBuff = 245,
		ControlledRageBuff = 246,
		KinofTarasque = 247,
		MorphHeal = 248,
		PackHunterBase = 249,
		PackHunterBuff = 250,
		AdventurerMorphBuff = 251,
		DamageBuff_LineC = 252,
		FixerSuppressorBuff = 253,
		ChestBuffLine = 254,
		FixerLongHoT = 255,
		Fear = 256,
		FixerNCUBuff = 257,
		TraderTeamHeals1 = 258,
		TraderTeamHeals2 = 259,
		TraderTeamHeals3 = 260,
		TraderTeamHeals4 = 261,
		TraderTeamHeals5 = 262,
		TraderTeamHeals6 = 263,
		TraderTeamHeals7 = 264,
		TraderTeamHeals8 = 265,
		TraderTeamHeals9 = 266,
		TraderTeamHeals10 = 267,
		TraderTeamHeals11 = 268,
		TraderTeamHeals12 = 269,
		TraderTeamHeals13 = 270,
		TraderTeamHeals14 = 271,
		TraderTeamHeals15 = 272,
		TraderTeamHeals16 = 273,
		TraderTeamHeals17 = 274,
		UNUSED1 = 275,
		TowerSmokeBuffEffects = 276,
		DroneTowerBuff = 277,
		EnforcerPiercingBuff = 278,
		EnforcerMeleeEnergyBuff = 279,
		SoldierShotgunBuff = 280,
		SoldierFullAutoBuff = 281,
		CompleteHealingLine = 282,
		SelfRoot_SnareResistBuff = 283,
		OtherRoot_SnareResistBuff = 284,
		PetSnare_RootResistanceBuff = 285,
		EngineerSpecialAttackAbsorber = 286,
		Ransack_DepriveResistBuff = 287,
		EngineerPetAOESnareBuff = 288,
		TemporalChaliceVisualEffectBuff = 289,
		TeporaryRoot_SnareResistanceBuff = 290,
		MongoHoTComponent = 291,
		UnhallowedForceLine = 292,
		BeaconWarp = 293,
		BurntOutArmorProc = 294,
		HellGunDispelProc = 295,
		PerkLimber = 296,
		PerkDanceOfFools = 297,
		PerkChemicalBlindness = 298,
		PerkPoisonSprinkle = 299,
		PerkSealWounds = 300,
		PerkTranquilizer = 301,
		PerkToxicShock = 302,
		PerkConcussiveShot = 303,
		PerkAssasinate = 304,
		PerkBattlegroupHeal1 = 305,
		PerkBattlegroupHeal2 = 306,
		PerkViralCombination = 307,
		PerkBattlegroupHeal3 = 308,
		PerkBattlegroupHeal4 = 309,
		PerkBioShield = 310,
		PerkBioCocoon = 311,
		PerkBioRejuvenation = 312,
		PerkBioRegrowth = 313,
		PerkChaoticModulation = 314,
		PerkSoftenUp = 315,
		PerkPinpointStrike = 316,
		PerkDeathStrike = 317,
		PerkLayOnHands = 318,
		PerkDevotionalArmor = 319,
		PerkCuringTouch = 320,
		PerkQuickBash = 321,
		PerkCrushBone = 322,
		PerkBringThePain = 323,
		PerkDevastatingBlow = 324,
		PerkBigSmash = 325,
		PerkFollowupSmash = 326,
		PerkBlindsideBlow = 327,
		PerkBureaucraticShuffle = 328,
		PerkSuccumb = 329,
		PerkConfoundWithRules = 330,
		PerkEvasiveStance = 331,
		PerkElementaryTeleportation1 = 332,
		PerkElementaryTeleportation2 = 333,
		PerkElementaryTeleportation3 = 334,
		PerkElementaryTeleportation4 = 335,
		PerkICCNodeTeleportation = 336,
		PerkChannelRage = 337,
		PerkBlessingOfLife = 338,
		PerkLifeblood = 339,
		PerkDrawBlood = 340,
		PerkInstallExplosiveDevices = 341,
		PerkInstallNotumDepletionDevice = 342,
		PerkSuppressivePrimer = 343,
		PerkThermalPrimer = 344,
		PerkLeadership = 345,
		PerkGovernance = 346,
		PerkTheDirector = 347,
		PerkBalanceOfYinandYang = 348,
		PerkReapLife = 349,
		PerkBloodletting = 350,
		PerkVitalShock = 351,
		PerkQuickCut = 352,
		PerkFlay = 353,
		PerkFlurryofCuts = 354,
		PerkRibbonFlesh = 355,
		PerkReconstructDNA = 356,
		PerkViralWipe = 357,
		PerkBreachDefenses = 358,
		PerkNanoHeal = 359,
		PerkExplorationTeleportation1 = 360,
		PerkExplorationTeleportation2 = 361,
		PerkDevour = 362,
		PerkBleedingWounds = 363,
		PerkGuttingBlow = 364,
		PerkHeal = 365,
		PerkInvocation = 366,
		PerkTrollForm = 367,
		PerkDisableNaturalHealing = 368,
		PerkStonefist = 369,
		PerkAvalanche = 370,
		PerkGrasp = 371,
		PerkBearhug = 372,
		PerkGripofColossus = 373,
		PerkRemoval1 = 374,
		PerkRemoval2 = 375,
		PerkPurge1 = 376,
		PerkPurge2 = 377,
		PerkGreatPurge = 378,
		PerkReconstruction = 379,
		PerkTauntBox = 380,
		PerkSiphonLife = 381,
		PerkChaoticEnergy = 382,
		PerkRegainNano = 383,
		PerkNCUBooster = 384,
		PerkLaserPaintTarget = 385,
		PerkWeaponBash = 386,
		PerkTriangulateTarget = 387,
		PerkNapalmSpray = 388,
		PerkMarkofVengeance = 389,
		PerkMarkofSufferance = 390,
		PerkMarkoftheUnclean = 391,
		PerkMarkoftheUnhallowed = 392,
		PerkArmorPiercingShot = 393,
		PerkFindtheFlaw = 394,
		PerkCalledShot = 395,
		PerkTremorHand = 396,
		PerkHarmonizeBodyandMind = 397,
		PerkTaunt = 398,
		PerkCharge = 399,
		PerkHeadbutt = 400,
		PerkHatred = 401,
		PerkGroinKick = 402,
		PerkDeconstruction = 403,
		PerkEncaseinStone = 404,
		PerkDetonateStoneWorks = 405,
		PerkShutdownRemoval1 = 406,
		PerkShutdownRemoval2 = 407,
		PerkEnhancedHeal = 408,
		PerkMaliciousProhibition = 409,
		PerkTeamHeal = 410,
		PerkTreatmentTransfer = 411,
		PerkZapNano = 412,
		PerkNanoShakes = 413,
		PerkStripNano = 414,
		PerkAnnihilateNotumMolecules = 415,
		PerkFadeAnger = 416,
		PerkTapNotumSource = 417,
		PerkAccessNotumSource = 418,
		PerkBlastNano = 419,
		PerkStopNotumFlow = 420,
		PerkNotumOverflow = 421,
		PerkStoneworks = 422,
		PerkCripplePsyche = 423,
		PerkShatterPsyche = 424,
		PerkDominator = 425,
		PerkStab = 426,
		PerkDoubleStab = 427,
		PerkPerforate = 428,
		PerkLacerate = 429,
		PerkImpale = 430,
		PerkGore = 431,
		PerkHecatomb = 432,
		PerkQuickShot = 433,
		PerkDoubleShot = 434,
		PerkDeadeye = 435,
		PerkEnergize = 436,
		PerkPowerVolley = 437,
		PerkPowerShock = 438,
		PerkPowerBlast = 439,
		PerkPowerCombo = 440,
		PerkAtrophy = 441,
		PerkDoomTouch = 442,
		PerkSpiritDissolution = 443,
		PerkFadeArmor = 444,
		PerkShadowBullet = 445,
		PerkNightKiller = 446,
		PerkShadowStab = 447,
		PerkBladeofNight = 448,
		PerkShadowKiller = 449,
		PerkSnipeShot1 = 450,
		PerkSnipeShot2 = 451,
		PerkLegShot = 452,
		PerkEasyShot = 453,
		PerkReinforceSlugs = 454,
		PerkJarringBurst = 455,
		PerkSolidSlug = 456,
		PerkNeutroniumSlug = 457,
		PerkFieldBandage = 458,
		PerkTracer = 459,
		PerkContainedBurst = 460,
		PerkViolence = 461,
		PerkGuardian = 462,
		PerkCure = 463,
		PerkVaccinate = 464,
		PerkCure2 = 465,
		PerkVaccinate2 = 466,
		PerkHaleandHearty = 467,
		PerkTeamHaleandHearty = 468,
		PerkCaptureVigor = 469,
		PerkUnhealedBlight = 470,
		PerkCaptureEssence = 471,
		PerkUnsealedPestilence = 472,
		PerkCaptureSpirit = 473,
		PerkUnsealedContagation = 474,
		PerkCaptureVitality = 475,
		PerkBane = 476,
		PerkDragonfire = 477,
		PerkChiConductor = 478,
		PerkIncapacitate = 479,
		PerkFleshQuiver = 480,
		PerkOboliterate = 481,
		PerkDazzlewithLights = 482,
		PerkCombust = 483,
		PerkThermalDetonation = 484,
		PerkSupernova = 485,
		PerkDeepCuts = 486,
		PerkBladeWhirlwind = 487,
		PerkHonoringTheAncients = 488,
		PerkSeppukuSlash = 489,
		PerkExultation = 490,
		PerkEtheralTouch = 491,
		PerkDimensionalFist = 492,
		PerkDisorient = 493,
		PerkConvulsiveTremor = 494,
		PerkSymbiosis = 495,
		PerkMaliciousSymbiosis = 496,
		PerkMalevolentSymbiosis = 497,
		PerkChtonianSymbiosis = 498,
		PerkQuarkContainmentField = 499,
		PerkAccelerateDecayingQuarks = 500,
		PerkKnowledgeEnhancer = 501,
		PerkEscape = 502,
		PerkSabotageQuarkField = 503,
		PerkIgnitionFlare = 504,
		PerkRitualofDevotion = 505,
		PerkDevourVigor = 506,
		PerkRitualofZeal = 507,
		PerkDevourEssence = 508,
		PerkRitualofSpirit = 509,
		PerkDevourVitality = 510,
		PerkRitualofBlood = 511,
		PerkECM1 = 512,
		PerkECM2 = 513,
		PerkSPECIALAcrobat = 514,
		PerkSPECIALbureaucraticshuffle = 515,
		PerkSPECIALpersuader = 516,
		PerkSPECIALalchemist = 517,
		KeeperDeflect_RiposteBuff = 518,
		FastAttackBuffs = 519,
		ShadeDamageProc_DamageInflictSegment = 520,
		ShadeProcBuff = 521,
		ShadeHP_NPDoTProc_DamageInflictSegment = 522,
		ShadeInitDebuffProc = 523,
		KeeperSanctifierProc_DamageInflictSegment = 524,
		KeeperReaperProc_DamageInflictSegment = 525,
		KeeperProcBuff = 526,
		KeeperAura_HPandNPHeal = 527,
		KeeperAura_Absorb_Reflect_AMSBuff = 528,
		KeeperAura_Damage_SnareReductionBuff = 529,
		KeeperHealAura_Team = 530,
		KeeperNPHealAura_Team = 531,
		KeeperAbsorbAura_Team = 532,
		KeeperAMS_DMSAura_Team = 533,
		KeeperReflectAura_Team = 534,
		KeeperDamageAura_Team = 535,
		KeeperSnareReductionAura_Team = 536,
		PerkSPECIALAssasin = 537,
		AddAllDef_PerkBuff = 538,
		KeeperStr_Stam_AgiBuff = 539,
		PerkSPECIALTinkerer = 540,
		PerkSpecialThief = 541,
		PerkSPECIALStarfall = 542,
		PerkSpecialShadowsneak = 543,
		PerkSpecialKungfuMaster = 544,
		KeeperEvade_Dodge_DuckBuff = 545,
		ShadePiercingBuff = 546,
		DimachBuff = 547,
		PerkAuraOfRevival_HealStopper = 548,
		PerkCommandingPresence = 549,
		PerkDirectorshipBuff = 550,
		PerkChannelingOfNotum_HealStopper = 551,
		PerkTheoreticalResearch = 552,
		PerkStreetSamurai = 553,
		PerkSpecialForces = 554,
		PerkSMGMastery = 555,
		PerkNanoSurgeon556 = 556,
		PerkHeavyRanged = 557,
		PerkGridNCU = 558,
		PerkEnhancedNanoDamage = 559,
		GMNanobuff = 560,
		PerkNanoSurgeon561 = 561,
		UNUSED2 = 562,
		GeneralDimachBuff = 563,
		GeneralMeleeMultipleBuff = 564,
		MonsterWaveSpawn1 = 565,
		MonsterWaveSpawn2 = 566,
		MonsterWaveSpawn3 = 567,
		MonsterWaveSpawn4 = 568,
		MonsterWaveSpawn5 = 569,
		MonsterWaveSpawn6 = 570,
		MonsterWaveSpawn7 = 571,
		MonsterWaveSpawn8 = 572,
		MonsterWaveSpawn9 = 573,
		MonsterWaveSpawn10 = 574,
		BattlegroupHeal = 575,
		Psy_IntBuff = 576,
		BioShielding = 577,
		BioCocoon = 578,
		BioRejuvenation = 579,
		BioRegrowth = 580,
		GeneralRangedMultipleBuff = 581,
		DOTStrainC = 582,
		DevotionalArmor = 583,
		ScaleRepair = 584,
		SlobberWounds = 585,
		LickWoundsNA = 586,
		SLNanopointDrain = 587,
		NanoPointHeals = 588,
		BlessingofLife = 589,
		Lifeblood = 590,
		DrawBlood = 591,
		HeavyWeaponsBuffs = 592,
		EtherealTouch = 593,
		ConvulsiveTremor = 594,
		NanoRecharge = 595,
		HealthRecharge = 596,
		DamageChangeBuffs = 597,
		BonfireRecharger = 598,
		RitualofDevotion = 599,
		RitualofZeal = 600,
		RitualofSpirit = 601,
		RitualofBlood = 602,
		MonsterEffect1 = 603,
		MonsterEffect2 = 604,
		MonsterEffect3 = 605,
		MonsterEffect4 = 606,
		MonsterEffect5 = 607,
		MonsterEffect6 = 608,
		MonsterEffect7 = 609,
		MonsterEffect8 = 610,
		ShortTermXPGain = 611,
		DoubleStabBleedingWounds = 612,
		LacerateBleedingWounds = 613,
		GoreBleedingWounds = 614,
		HecatombBleedingWounds = 615,
		MonsterEffect_Breakable = 616,
		MonsterEffect_DuringFight = 617,
		PerkCleave = 618,
		PerkTransfix = 619,
		PerkPainLance = 620,
		PerkSliceAndDice = 621,
		PerkPulverize = 622,
		PerkHammerAndAnvil = 623,
		PerkOverwhelmingMight = 624,
		PerkSeismicSmash = 625,
		PainLanceDoT = 626,
		EnforcerTauntProcs = 627,
		EnforcerTauntProcsFearbringer = 628,
		EnforcerTauntProcsIrebringer = 629,
		EnforcerTauntProcsWrathbringer = 630,
		EnforcerTauntProcsHatebringer = 631,
		EnforcerTauntProcsRagebringer = 632,
		EnforcerTauntProcsDreadbringer = 633,
		AccelerateDecayingQuarksDebuff = 634,
		AgentDamageProc_DamageInflictSegment = 635,
		AgentProcBuff = 636,
		MonsterEffect_MainLoop = 637,
		Atrophy = 638,
		DeepCuts = 639,
		TraderDebuffACNanos = 640,
		LegShot = 641,
		CrushBone = 642,
		NanoResistanceDebuff_LineA = 643,
		DebuffNanoACHeavy = 644,
		CalledShotBleedingWounds = 645,
		Energize = 646,
		MarkofVengeance = 647,
		MarkofSufferance = 648,
		MarkoftheUnclean = 649,
		MarkoftheUnhallowed = 650,
		ToxicShock = 651,
		ToxicShockProcEffect = 652,
		DodgetheBlame = 653,
		ConfoundwithRules = 654,
		Succumb = 655,
		TrollForm = 656,
		DisableNaturalHealing = 657,
		MPDamageDebuffLineA = 658,
		MPDamageDebuffLineB = 659,
		NanoShakes = 660,
		TapNotumSource = 661,
		BlastNano = 662,
		StopNotumFlow = 663,
		NotumOverflow = 664,
		BladeofNight = 665,
		Violence = 666,
		ViolenceController = 667,
		Guardian = 668,
		TotalMirrorShield = 669,
		DazzlewithLights = 670,
		KnowledgeEnhancer = 671,
		BleedingWounds = 672,
		FixerDodgeBuffLine = 673,
		HammerandAnvil = 674,
		ZapNano = 675,
		ChannelRage = 676,
		ChaoticModulation = 677,
		FreakStrengthStun = 678,
		FreakStrengthSelfStun = 679,
		AgentEscapeNanos = 680,
		Reconstruction681 = 681,
		TauntBox682 = 682,
		SiphonBox683 = 683,
		GadgeteerPetProcs = 684,
		GroinKick = 685,
		Reconstruction686 = 686,
		TauntBox687 = 687,
		SiphonBox688 = 688,
		Deconstruction = 689,
		InstallExplosiveDeviceDoT = 690,
		InstallNotumDepletionDeviceDoT = 691,
		InstallExplosiveDeviceCountdown = 692,
		InstallNotumDepletionDeviceCountdown = 693,
		ShadowlandReflectBase = 694,
		Blackstep = 695,
		ObscureVision = 696,
		GatherDarkness = 697,
		Silence = 698,
		SilenceDebuff = 699,
		Misery = 700,
		Death = 701,
		PathofDarkness = 702,
		PathofDarknessDebuff = 703,
		RoadToDarkness = 704,
		RoadToDarknessDebuff = 705,
		TheChoice_Omni = 706,
		TheChoiceDebuff_Omni = 707,
		Blackfist = 708,
		SlamofDarkness = 709,
		SlamofDarknessDebuff = 710,
		ScreamofDeath = 711,
		ScreamofDeathDebuff = 712,
		Lightstep = 713,
		GatherLight = 714,
		RainofLight = 715,
		RainofLightBuff = 716,
		Morning = 717,
		MorningDebuff = 718,
		Hope = 719,
		HopeBuff = 720,
		HopeDebuff = 721,
		Life = 722,
		PathofLight = 723,
		TunnelofLight = 724,
		TunnelofLightBuff = 725,
		TheChoice_Clan = 726,
		ScreenofLight = 727,
		ShieldofLight = 728,
		ShieldofLightBuff = 729,
		FortressofLight = 730,
		FortressofLightBuff = 731,
		MiseryBuff = 732,
		MiseryDebuff = 733,
		QuarkContainmentField = 734,
		Fury = 735,
		ReinforcedSlugs = 736,
		AffectedbyNanoHeal = 737,
		ShadowlandBindandRecall = 738,
		PerformedRitualofDevotion = 739,
		PerformedRitualofZeal = 740,
		PerformedRitualofSpirit = 741,
		PerformedRitualofBlood = 742,
		PerformedDevourVigor = 743,
		PerformedDevourEssence = 744,
		PerformedDevourVitality = 745,
		PerformedStab = 746,
		PerformedPerforate = 747,
		PerformedImpale = 748,
		PerformedDoubleStab = 749,
		PerformedLacerate = 750,
		PerformedGore = 751,
		PerformedHecatomb = 752,
		PerformedCaptureVigor = 753,
		PerformedCaptureEssence = 754,
		PerformedCaptureSpirit = 755,
		PerformedCaptureVitality = 756,
		AffectedbyTaintWounds = 757,
		PerformedUnsealedBlight = 758,
		PerformedUnsealedPestilence = 759,
		PerformedUnsealedContagion = 760,
		TransitionOfErgo = 761,
		InsuranceAgent = 762,
		InsuranceClaim = 763,
		AffectedbyInsuranceClaim = 764,
		RegainNano = 765,
		GroveHealingMultiplier = 766,
		InstinctiveControl = 767,
		SpecialAttackAbsorberBase = 768,
		TotalFocus = 769,
		SoldierDamageBase = 770,
		AffectedByDefensiveStance = 771,
		DefensiveStance = 772,
		AgentDetauntProc_DetauntSegment = 773,
		AffectedbyDeceptiveStance = 774,
		DeceptiveStance = 775,
		AffectedbyConsumetheSoul = 776,
		ShortTermHPBuff = 777,
		AffectedbySpiritofBlessing = 778,
		AffectedbySpiritofPurity = 779,
		SpiritofBlessing = 780,
		SpiritofPurity = 781,
		WaitForAttackEffectNano2 = 782,
		DuringFightNanoEffect2 = 783,
		DanceofFools = 784,
		EnvironmentalDamage = 785,
		FixerRunspeedBase = 786,
		AIPERKBlur = 787,
		AIPERKSacrifice = 788,
		MINIDoT = 789,
		ZixLine = 790,
		AIAMSmodifierproc = 791,
		AIPERKSilentPlague = 792,
		AIPERKInsight = 793,
		AIPERKAssumeTarget = 794,
		Daring = 795,
		LeetEmpower = 796,
		Link = 797,
		NoTerraform = 798,
		BossRoot = 799,
		Cocoon = 800,
		NTAreaNukes = 801,
		AELevelSpawn = 802,
		Scones = 803,
		PrivacyShield = 804,
		BatterUp = 805,
		ArmorDamage = 806,
		HealingConstructEmpowerment = 807,
		PH = 808,
		DamagetoNano = 809,
		MesmerizationConstructEmpowerment = 810,
		EngineerMiniaturization = 811,
		ResearchAbility1 = 812,
		ResearchAbility2 = 813,
		TraderAAODrain = 814,
		MartialArtistBowBuffs = 815,
		PetDefensiveNanos = 816,
		PetDamageOverTimeResistNanos = 817,
		ColdBlooded = 818,
		SingedFists = 819,
		AMS = 820,
		ShovelBuffs = 821,
		AncientBlessings = 822,
		AugmentedMirrorShieldNano = 823,
		NullitySphereNano = 824,
		DOTRemoval = 825,
		TraderNanoTheft1 = 826,
		TraderNanoTheft2 = 827,
		HealthandNanoOverTimeDrain = 828,
		HealthandNanoOverTimeTransfer = 829,
		TrueProfession = 830,
		ShieldoftheObedientServant = 831,
		NTAreaNukes2 = 832,
		BureaucratResearchStun1 = 833,
		BureaucratResearchStun2 = 834,
		NanoResistBuff = 835,
		AAOBuffs = 836,
		AffectedbyOFABDebuff = 837,
		DustBrigadeTurretsI = 838,
		DustBrigadeTurretsII = 839,
		DustBrigadeTurretsIII = 840,
		AdventurerDamageModifier = 841,
		DeTaunt = 842,
		PetHealDelta843 = 843,
		HealthDrain = 844,
		DamageDrain = 845,
		SkillLockModifierDebuff847 = 847,
		Incapacitate = 849,
		PetHealDelta850 = 850,
		ReanimatedCloakBuffs = 851,
		ReanimatedCloakBlocker = 852,
		ReanimatedCloakDebuffs = 853,
		AggressiveConstructEmpowerment = 854,
		MaxNanoBuffs = 855,
		NanoDrain_LineB = 856,
		NotumShield = 857,
		NanoBurst_CyberdeckSpecial = 858,
		MartialArtistHOTLineA = 859,
		Malpractice = 860,
		WeaponEffectAdd_On2 = 861,
		NanoResistDebuffProc = 862,
		MPAttackPetDamageType = 863,
		MagnifyingGlassBuffs = 864,
		BreathingLine1 = 865,
		BreathingLine2 = 866,
		BreathingLine3 = 867,
		EvasionDebuffs_Agent = 868,
		DBPFTeleportA = 869,
		DBPFTeleportB = 870,
		DBPFTeleportC = 871,
		DBPFTeleportD = 872,
		DBPFTeleportE = 873,
		DBPFTeleportF = 874,
		DBPFTeleportX = 875,
		MagnifyingGlassAttunementBX11 = 876,
		MagnifyingGlassAttunementWQEL = 877,
		MagnifyingGlassAttunementMVCN = 878,
		MagnifyingGlassAttunementZLQ6 = 879,
		AlienDropshipShield1insidewest = 880,
		AlienDropshipShield2insideeast = 881,
		AlienDropshipShield3insidenorth = 882,
		Fear_PVP = 883,
		Knockback = 884,
		Fear_Cooldown = 885,
		ReverseKnockback = 886,
		UnremovableSnare = 887,
		TraderShutdownSkillDebuff = 889,
		TraderShutdownSkillBuff = 890,
		KeeperFearImmunity = 900,
		FixerFearImmunity = 901,
		EnduranceSkin = 909,
		PvPEnabled = 910,
		DarkRuinsRootandSnare = 911,
		Vehicles = 914,
		PrototypeNanoformula = 916,
		BorrowReflect = 922,
		TotalControl = 924,
		TrollFormRunDebuff = 925,
		SocialPets = 926,
		MarkofthePious = 927,
		Focus = 928,
		Loophole = 929,
		OptimizeBotProtocol = 930,
		FreakShield = 931,
		FlimFocus = 932,
		BringThePain = 933,
		ChemicalBlindness = 934,
		PoisonSprinkle = 935,
		MongoFury = 936,
		WitoftheAtrox = 937,
		WayofTheAtrox = 938,
		NotumDomination = 939,
		NotumSpring = 940,
		BlindedbyDelights = 941,
		Derivate = 942,
		DizzyingHeights = 943,
		SprainedAnkle = 944,
		Feel = 945,
		Propaganda = 946,
		TreatmentTransfer = 947,
		GeneralPerceptionBuff = 949,
		SingleTargetHealing = 951,
		TeamHealing = 952,
		KyrOzchGenePool = 956,
		AlienParasite = 957,
		MindControl = 958,
		ExperienceConstructs_XPBonus = 959,
		NemesisNanoPrograms = 1000,
		AADBuffs = 1002,
		Stun = 1003,
		AOEMezz = 1004,
		AOESnare = 1005,
		AOERoot = 1006,
		SnareRemovalSelf = 1007,
		SnareRemovalOther = 1008,
		SnareRemovalTeam = 1009,
		RootRemovalSelf = 1010,
		RootRemovalOther = 1011,
		RootRemovalTeam = 1012,
		PetRoot = 1013,
		SnareandMezzRemoval = 1013,
		PetHealing = 1014,
		AttackPets = 1015,
		HealPets = 1016,
		SupportPets = 1017,
		PetSacrifice = 1018,
		PetWarp = 1019,
		PetProc_LineB = 1020,
		PetAOESnare = 1021,
		Charm_Short = 1022,
		PetProc_LineA = 1023,
		DamageToPet = 1024,
		Nukes = 1025,
		AlphaNukes = 1026,
		FinishingNukes = 1027,
		SpecialEffectNukes = 1028,
		BossBuffs = 1029,
		SelfGrid = 1030,
		TeamGrid = 1031,
		EmergencyGrid = 1032,
		ShadowlandsMaps = 1033,
		TeamRunSpeedBuffs = 1034,
		SpiritDrain = 1035,
		SummonItem = 1036,
		Taunt = 1037,
		AOETauntDOT = 1038,
		FixerGrid = 1039,
		NanoDeltaDebuff = 1040,
		Nuke = 1041,
		AlphaNuke = 1042,
		OmegaNuke = 1043,
		AOENuke = 1044,
		ResurrectionSicknessRemoval = 1045,
		FoodandDrinkBuffs = 1046,
		PetDebuffCleanse = 1047,
		ProximityRangeDebuff = 1048,
		EmergencySneak = 1049,
		HealDeltaDebuff = 1050,
		DrainHeal = 1051,
		CriticalDecreaseBuff = 1052,
		SkillLockModifierDebuff1053 = 1053,
		ICCSurveillanceSoftware = 1054,
		HealReactivityMultiplierBuff = 1055,
		HealReactivityMultiplierDebuff = 1056,
		Charge = 1057,
		MartialArtistZazenStance = 1058,
		MartialArtistHOT_LineB = 1059,
		TraderAADDrain = 1060,
		NanoOverTime_LineB = 1061,
		NanoDamageMultiplierBuffs = 1062
	}
	public enum Profession : uint
	{
		Unknown,
		Soldier,
		MartialArtist,
		Engineer,
		Fixer,
		Agent,
		Adventurer,
		Trader,
		Bureaucrat,
		Enforcer,
		Doctor,
		NanoTechnician,
		Metaphysicist,
		Monster,
		Keeper,
		Shade
	}
	[Flags]
	public enum ProfessionFlag : uint
	{
		None = 0u,
		Soldier = 2u,
		MartialArtist = 4u,
		Engineer = 8u,
		Fixer = 0x10u,
		Agent = 0x20u,
		Adventurer = 0x40u,
		Trader = 0x80u,
		Bureaucrat = 0x100u,
		Enforcer = 0x200u,
		Doctor = 0x400u,
		NanoTechnician = 0x800u,
		MetaPhysicist = 0x1000u,
		Keeper = 0x4000u,
		Shade = 0x8000u
	}
	public struct Quaternion
	{
		[AoMember(0)]
		public float X { get; set; }

		[AoMember(1)]
		public float Y { get; set; }

		[AoMember(2)]
		public float Z { get; set; }

		[AoMember(3)]
		public float W { get; set; }

		public Vector3 Forward => this * Vector3.Forward;

		public static Quaternion Identity => new Quaternion(0f, 0f, 0f, 1f);

		public double Yaw
		{
			get
			{
				double num = Math.Atan2(2f * Y * W - 2f * X * Z, 1f - 2f * Y * Y - 2f * Z * Z);
				if (num < 0.0)
				{
					num += Math.PI * 2.0;
				}
				return num;
			}
		}

		public double Pitch => -2.0 * Math.Atan2(2f * X * W - 2f * Y * Z, 1f - 2f * X * Y - 2f * Z * Z);

		public double Roll => Math.Asin(2f * X * Y + 2f * Z * W);

		public double Magnitude => Math.Sqrt(X * X + Y * Y + Z * Z + W * W);

		public Quaternion(double x, double y, double z, double w)
		{
			X = (float)x;
			Y = (float)y;
			Z = (float)z;
			W = (float)w;
		}

		public Quaternion(float x, float y, float z, float w)
		{
			X = x;
			Y = y;
			Z = z;
			W = w;
		}

		public Quaternion(Vector3 v, float angle)
		{
			Vector3 vector = v.Normalize();
			double num = Math.Sin(angle / 2f);
			X = (float)((double)vector.X * num);
			Y = (float)((double)vector.Y * num);
			Z = (float)((double)vector.Z * num);
			W = (float)Math.Cos(angle / 2f);
		}

		public Quaternion(Vector3 v)
		{
			X = v.X;
			Y = v.Y;
			Z = v.Z;
			W = 0f;
		}

		public void Update(float x, float y, float z, float w)
		{
			X = x;
			Y = y;
			Z = z;
			W = w;
		}

		public static Quaternion Conjugate(Quaternion q1)
		{
			return new Quaternion(0f - q1.X, 0f - q1.Y, 0f - q1.Z, q1.W);
		}

		public Quaternion Conjugate()
		{
			return Conjugate(this);
		}

		public static Quaternion LookRotation(Vector3 forward, Vector3 up)
		{
			forward = Vector3.Normalize(forward);
			Vector3 vRight = Vector3.Normalize(Vector3.Cross(up, forward));
			up = Vector3.Cross(forward, vRight);
			float x = vRight.X;
			float y = vRight.Y;
			float z = vRight.Z;
			float x2 = up.X;
			float y2 = up.Y;
			float z2 = up.Z;
			float x3 = forward.X;
			float y3 = forward.Y;
			float z3 = forward.Z;
			float num = x + y2 + z3;
			Quaternion result = default(Quaternion);
			if (num > 0f)
			{
				float num2 = (float)Math.Sqrt(num + 1f);
				result.W = num2 * 0.5f;
				num2 = 0.5f / num2;
				result.X = (z2 - y3) * num2;
				result.Y = (x3 - z) * num2;
				result.Z = (y - x2) * num2;
				return result;
			}
			if (x >= y2 && x >= z3)
			{
				float num3 = (float)Math.Sqrt(1f + x - y2 - z3);
				float num4 = 0.5f / num3;
				result.X = 0.5f * num3;
				result.Y = (y + x2) * num4;
				result.Z = (z + x3) * num4;
				result.W = (z2 - y3) * num4;
				return result;
			}
			if (y2 > z3)
			{
				float num5 = (float)Math.Sqrt(1f + y2 - x - z3);
				float num6 = 0.5f / num5;
				result.X = (x2 + y) * num6;
				result.Y = 0.5f * num5;
				result.Z = (y3 + z2) * num6;
				result.W = (x3 - z) * num6;
				return result;
			}
			float num7 = (float)Math.Sqrt(1f + z3 - x - y2);
			float num8 = 0.5f / num7;
			result.X = (x3 + z) * num8;
			result.Y = (y3 + z2) * num8;
			result.Z = 0.5f * num7;
			result.W = (y - x2) * num8;
			return result;
		}

		public static Quaternion FromTo(Vector3 u, Vector3 v)
		{
			return LookRotation(v - u, Vector3.Up);
		}

		public static Quaternion Hamilton(Quaternion vLeft, Quaternion vRight)
		{
			double w = vLeft.W * vRight.W - vLeft.X * vRight.X - vLeft.Y * vRight.Y - vLeft.Z * vRight.Z;
			double x = vLeft.W * vRight.X + vLeft.X * vRight.W + vLeft.Y * vRight.Z - vLeft.Z * vRight.Y;
			double y = vLeft.W * vRight.Y - vLeft.X * vRight.Z + vLeft.Y * vRight.W + vLeft.Z * vRight.X;
			double z = vLeft.W * vRight.Z + vLeft.X * vRight.Y - vLeft.Y * vRight.X + vLeft.Z * vRight.W;
			return new Quaternion(x, y, z, w);
		}

		public void Rotate(float heading, float attitude, float bank)
		{
			double num = Math.Cos(heading / 2f);
			double num2 = Math.Sin(heading / 2f);
			double num3 = Math.Cos(attitude / 2f);
			double num4 = Math.Sin(attitude / 2f);
			double num5 = Math.Cos(bank / 2f);
			double num6 = Math.Sin(bank / 2f);
			double num7 = num * num3;
			double num8 = num2 * num4;
			W = (float)(num7 * num5 - num8 * num6);
			X = (float)(num7 * num6 + num8 * num5);
			Y = (float)(num2 * num3 * num5 + num * num4 * num6);
			Z = (float)(num * num4 * num5 - num2 * num3 * num6);
		}

		public static Quaternion CreateFromAxisAngle(Vector3 axis, double a)
		{
			return CreateFromAxisAngle(axis.X, axis.Y, axis.Z, a);
		}

		public static Quaternion CreateFromAxisAngle(double xx, double yy, double zz, double a)
		{
			double num = Math.Sin(a / 2.0);
			double x = xx * num;
			double y = yy * num;
			double z = zz * num;
			double w = Math.Cos(a / 2.0);
			return new Quaternion(x, y, z, w).Normalize();
		}

		public static Quaternion AngleAxis(float degress, Vector3 axis)
		{
			if (axis.Magnitude == 0.0)
			{
				return Identity;
			}
			Quaternion identity = Identity;
			float num = degress * ((float)Math.PI / 180f);
			num *= 0.5f;
			axis.Normalize();
			axis *= (float)Math.Sin(num);
			identity.X = axis.X;
			identity.Y = axis.Y;
			identity.Z = axis.Z;
			identity.W = (float)Math.Cos(num);
			return Normalize(identity);
		}

		public Quaternion Hamilton(Quaternion vRight)
		{
			return Hamilton(this, vRight);
		}

		public static Quaternion Normalize(Quaternion q1)
		{
			double magnitude = q1.Magnitude;
			return new Quaternion((double)q1.X / magnitude, (double)q1.Y / magnitude, (double)q1.Z / magnitude, (double)q1.W / magnitude);
		}

		public Quaternion Normalize()
		{
			return Normalize(this);
		}

		public static Vector3 RotateVector3(Quaternion q1, Vector3 v2)
		{
			Quaternion vRight = new Quaternion(v2.X, v2.Y, v2.Z, 0f);
			Quaternion vLeft = q1.Normalize();
			Quaternion quaternion = Hamilton(Hamilton(vLeft, vRight), vLeft.Conjugate());
			return new Vector3(quaternion.X, quaternion.Y, quaternion.Z);
		}

		public Vector3 RotateVector3(Vector3 v1)
		{
			return RotateVector3(this, v1);
		}

		public static Vector3 VectorRepresentation(Quaternion q1)
		{
			return new Vector3(q1.X, q1.Y, q1.Z);
		}

		public Vector3 VectorRepresentation()
		{
			return VectorRepresentation(this);
		}

		public override string ToString()
		{
			return $"X: {X} | Y: {Y} | Z: {Z} | W: {W}";
		}

		public static Vector3 operator *(Quaternion rotation, Vector3 point)
		{
			float num = rotation.X * 2f;
			float num2 = rotation.Y * 2f;
			float num3 = rotation.Z * 2f;
			float num4 = rotation.X * num;
			float num5 = rotation.Y * num2;
			float num6 = rotation.Z * num3;
			float num7 = rotation.X * num2;
			float num8 = rotation.X * num3;
			float num9 = rotation.Y * num3;
			float num10 = rotation.W * num;
			float num11 = rotation.W * num2;
			float num12 = rotation.W * num3;
			Vector3 result = default(Vector3);
			result.X = (1f - (num5 + num6)) * point.X + (num7 - num12) * point.Y + (num8 + num11) * point.Z;
			result.Y = (num7 + num12) * point.X + (1f - (num4 + num6)) * point.Y + (num9 - num10) * point.Z;
			result.Z = (num8 - num11) * point.X + (num9 + num10) * point.Y + (1f - (num4 + num5)) * point.Z;
			return result;
		}
	}
	public struct Rect
	{
		public float MinX;

		public float MinY;

		public float MaxX;

		public float MaxY;

		public static Rect Default
		{
			get
			{
				Rect result = default(Rect);
				result.MinX = 0f;
				result.MinY = 0f;
				result.MaxX = 99999f;
				result.MaxY = 99999f;
				return result;
			}
		}

		public Rect(float minX, float minY, float maxX, float maxY)
		{
			MinX = minX;
			MinY = minY;
			MaxX = maxX;
			MaxY = maxY;
		}

		public bool Contains(Vector3 Pos)
		{
			return Pos.X > MinX && Pos.X < MaxX && Pos.Z > MinY && Pos.Z < MaxY;
		}

		public override string ToString()
		{
			return $"({MinX}, {MinY}, {MaxX}, {MaxY})";
		}
	}
	[Flags]
	public enum CanFlags : uint
	{
		None = 0u,
		Carry = 1u,
		Sit = 2u,
		Wear = 4u,
		Use = 8u,
		ConfirmUse = 0x10u,
		Consume = 0x20u,
		TutorChip = 0x40u,
		TutorDevice = 0x80u,
		BreakingAndEntering = 0x100u,
		Stackable = 0x200u,
		NoAmmo = 0x400u,
		Burst = 0x800u,
		FlingShot = 0x1000u,
		FullAuto = 0x2000u,
		AimedShot = 0x4000u,
		Bow = 0x8000u,
		ThrowAttack = 0x10000u,
		SneakAttack = 0x20000u,
		FastAttack = 0x40000u,
		DisarmTraps = 0x80000u,
		AutoSelect = 0x100000u,
		ApplyOnFriendly = 0x200000u,
		ApplyOnHostile = 0x400000u,
		ApplyOnSelf = 0x800000u,
		CantSplit = 0x1000000u,
		Brawl = 0x2000000u,
		Dimach = 0x4000000u,
		EnableHandAttractors = 0x8000000u,
		CanBeWornWithSocialArmor = 0x10000000u,
		CanParryRiposite = 0x20000000u,
		CanBeParriedRiposited = 0x40000000u,
		ApplyOnFightingTarget = 0x80000000u
	}
	public enum Stat
	{
		Flags = 0,
		MaxHealth = 1,
		Mass = 2,
		AttackSpeed = 3,
		Breed = 4,
		Clan = 5,
		Team = 6,
		State = 7,
		TimeExist = 8,
		MapFlags = 9,
		ProfessionLevel = 10,
		PreviousHealth = 11,
		Mesh = 12,
		Anim = 13,
		Name = 14,
		Info = 15,
		Strength = 16,
		Agility = 17,
		Stamina = 18,
		Intelligence = 19,
		Sense = 20,
		Psychic = 21,
		AMS = 22,
		StaticInstance = 23,
		MaxMass = 24,
		StaticType = 25,
		Energy = 26,
		Health = 27,
		Height = 28,
		DMS = 29,
		Can = 30,
		Face = 31,
		HairMesh = 32,
		Side = 33,
		DeadTimer = 34,
		AccessCount = 35,
		AttackCount = 36,
		TitleLevel = 37,
		BackMesh = 38,
		ShoulderMesh = 39,
		AlienXP = 40,
		FabricType = 41,
		CATMesh = 42,
		ParentType = 43,
		ParentInstance = 44,
		BeltSlots = 45,
		BandolierSlots = 46,
		Fatness = 47,
		ClanLevel = 48,
		InsuranceTime = 49,
		AggDef = 51,
		XP = 52,
		IP = 53,
		Level = 54,
		InventoryId = 55,
		TimeSinceCreation = 56,
		LastXP = 57,
		Age = 58,
		Sex = 59,
		Profession = 60,
		Cash = 61,
		AlignmentClanTokens = 62,
		Attitude = 63,
		HeadMesh = 64,
		HairTexture = 65,
		HairColourRGB = 67,
		NumConstructedQuest = 68,
		MaxConstructedQuest = 69,
		SpeedPenalty = 70,
		ItemType = 72,
		RepairDifficulty = 73,
		Value = 74,
		NanoStrain = 75,
		ItemClass = 76,
		RepairSkill = 77,
		CurrentMass = 78,
		Icon = 79,
		PrimaryItemType = 80,
		PrimaryItemInstance = 81,
		SecondaryItemType = 82,
		SecondaryItemInstance = 83,
		UserType = 84,
		UserInstance = 85,
		AreaType = 86,
		AreaInstance = 87,
		DefaultPos = 88,
		Race = 89,
		ProjectileAC = 90,
		MeleeAC = 91,
		EnergyAC = 92,
		ChemicalAC = 93,
		RadiationAC = 94,
		ColdAC = 95,
		PoisonAC = 96,
		FireAC = 97,
		StateAction = 98,
		ItemAnim = 99,
		MartialArts = 100,
		MultiMelee = 101,
		_1hBlunt = 102,
		_1hEdged = 103,
		MeleeEnergy = 104,
		Skill2hEdged = 105,
		Piercing = 106,
		_2hBlunt = 107,
		SharpObject = 108,
		Grenade = 109,
		HeavyWeapons = 110,
		Bow = 111,
		Pistol = 112,
		Rifle = 113,
		MGSMG = 114,
		Shotgun = 115,
		AssaultRifle = 116,
		VehicleWater = 117,
		MeleeInit = 118,
		RangedInit = 119,
		PhysicalInit = 120,
		BowSpecialAttack = 121,
		SensoryImprovement = 122,
		FirstAid = 123,
		Treatment = 124,
		MechanicalEngineering = 125,
		ElectricalEngineering = 126,
		MaterialMetamorphosis = 127,
		BiologicalMetamorphosis = 128,
		PsychologicalModification = 129,
		MaterialCreation = 130,
		SpaceTime = 131,
		NanoPool = 132,
		RangedEnergy = 133,
		MultiRanged = 134,
		TrapDisarm = 135,
		Perception = 136,
		Adventuring = 137,
		Swimming = 138,
		VehicleAir = 139,
		MapNavigation = 140,
		Tutoring = 141,
		Brawl = 142,
		Riposte = 143,
		Dimach = 144,
		Parry = 145,
		SneakAttack = 146,
		FastAttack = 147,
		Burst = 148,
		NanoCInit = 149,
		FlingShot = 150,
		AimedShot = 151,
		BodyDevelopment = 152,
		DuckExp = 153,
		DodgeRanged = 154,
		EvadeClsC = 155,
		RunSpeed = 156,
		QuantumFT = 157,
		WeaponSmithing = 158,
		Pharmaceuticals = 159,
		NanoProgramming = 160,
		ComputerLiteracy = 161,
		Psychology = 162,
		Chemistry = 163,
		Concealment = 164,
		BreakingEntry = 165,
		VehicleGround = 166,
		FullAuto = 167,
		NanoResist = 168,
		AlienLevel = 169,
		HealthChangeBest = 170,
		HealthChangeWorst = 171,
		HealthChange = 172,
		MoreFlags = 177,
		AlienNextXP = 178,
		NPCFlags = 179,
		CurrentNCU = 180,
		MaxNCU = 181,
		Specialization = 182,
		EffectIcon = 183,
		BuildingType = 184,
		BuildingInstance = 185,
		CardOwnerType = 186,
		CardOwnerInstance = 187,
		BuildingComplexInst = 188,
		ExitInstance = 189,
		NextDoorInBuilding = 190,
		LastConcretePlayfieldInstance = 191,
		ExtenalPlayfieldInstance = 192,
		ExtenalDoorInstance = 193,
		InPlay = 194,
		AccessKey = 195,
		ConflictReputation = 196,
		OrientationMode = 197,
		SessionTime = 198,
		RP = 199,
		Conformity = 200,
		Aggressiveness = 201,
		Stability = 202,
		Extroverty = 203,
		Taunt = 204,
		ReflectProjectileAC = 205,
		ReflectMeleeAC = 206,
		ReflectEnergyAC = 207,
		ReflectChemicalAC = 208,
		WeaponMesh = 209,
		RechargeDelay = 210,
		EquipDelay = 211,
		MaxEnergy = 212,
		TeamSide = 213,
		CurrentNano = 214,
		GmLevel = 215,
		ReflectRadiationAC = 216,
		ReflectColdAC = 217,
		ReflectNanoAC = 218,
		ReflectFireAC = 219,
		CurrBodyLocation = 220,
		MaxNanoEnergy = 221,
		AccumulatedDamage = 222,
		CanChangeClothes = 223,
		Features = 224,
		ReflectPoisonAC = 225,
		ShieldProjectileAC = 226,
		ShieldMeleeAC = 227,
		ShieldEnergyAC = 228,
		ShieldChemicalAC = 229,
		ShieldRadiationAC = 230,
		ShieldColdAC = 231,
		ShieldNanoAC = 232,
		ShieldFireAC = 233,
		ShieldPoisonAC = 234,
		BerserkMode = 235,
		InsurancePercentage = 236,
		ChangeSideCount = 237,
		AbsorbProjectileAC = 238,
		AbsorbMeleeAC = 239,
		AbsorbEnergyAC = 240,
		AbsorbChemicalAC = 241,
		AbsorbRadiationAC = 242,
		AbsorbColdAC = 243,
		AbsorbFireAC = 244,
		AbsorbPoisonAC = 245,
		AbsorbNanoAC = 246,
		TemporarySkillReduction = 247,
		BirthDate = 248,
		LastSaved = 249,
		SoundVolume = 250,
		Pets = 251,
		MetersWalked = 252,
		QuestLevelsSolved = 253,
		MonsterLevelsKilled = 254,
		PvPLevelsKilled = 255,
		MissionBits1 = 256,
		MissionBits2 = 257,
		DoorFlags = 259,
		ClanHierarchy = 260,
		QuestStat = 261,
		ClientActivated = 262,
		PersonalResearchLevel = 263,
		GlobalResearchLevel = 264,
		PersonalResearchGoal = 265,
		GlobalResearchGoal = 266,
		TurnSpeed = 267,
		LiquidType = 268,
		GatherSound = 269,
		CastSound = 270,
		TravelSound = 271,
		HitSound = 272,
		SecondaryItemTemplate = 273,
		EquippedWeapons = 274,
		XPKillRange = 275,
		AddAllOff = 276,
		AddAllDef = 277,
		ProjectileDamageModifier = 278,
		MeleeDamageModifier = 279,
		EnergyDamageModifier = 280,
		ChemicalDamageModifier = 281,
		RadiationDamageModifier = 282,
		ItemHateValue = 283,
		CriticalBonus = 284,
		MaxDamage = 285,
		MinDamage = 286,
		AttackRange = 287,
		HateValueModifier = 288,
		TrapDifficulty = 289,
		StatOne = 290,
		NumAttackEffects = 291,
		DefaultAttackType = 292,
		ItemSkill = 293,
		AttackDelay = 294,
		ItemOpposedSkill = 295,
		ItemSIS = 296,
		InteractionRadius = 297,
		Slot = 298,
		LockDifficulty = 299,
		Members = 300,
		MinMembers = 301,
		ClanPrice = 302,
		ClanUpkeep = 303,
		ClanType = 304,
		ClanInstance = 305,
		VoteCount = 306,
		MemberType = 307,
		MemberInstance = 308,
		GlobalClanType = 309,
		GlobalClanInstance = 310,
		ColdDamageModifier = 311,
		ClanUpkeepInterval = 312,
		TimeSinceUpkeep = 313,
		ClanFinalized = 314,
		NanoDamageModifier = 315,
		FireDamageModifier = 316,
		PoisonDamageModifier = 317,
		NPCostModifier = 318,
		XPModifier = 319,
		BreedLimit = 320,
		GenderLimit = 321,
		LevelLimit = 322,
		PlayerKilling = 323,
		TeamAllowed = 324,
		WeaponDisallowedType = 325,
		WeaponDisallowedInstance = 326,
		Taboo = 327,
		Compulsion = 328,
		SkillDisabled = 329,
		ClanItemType = 330,
		ClanItemInstance = 331,
		DebuffFormula = 332,
		PvP_Rating = 333,
		SavedXP = 334,
		DamageType1 = 339,
		BrainType = 340,
		XPBonus = 341,
		HealInterval = 342,
		HealDelta = 343,
		MonsterTexture = 344,
		HasAlwaysLootable = 345,
		NextXP = 350,
		SISCap = 352,
		AnimSet = 353,
		AttackType = 354,
		NanoFocusLevel = 355,
		MonsterData = 359,
		Scale = 360,
		HitEffectType = 361,
		ResurrectDest = 362,
		NanoInterval = 363,
		NanoDelta = 364,
		ReclaimItem = 365,
		GatherEffectType = 366,
		VisualBreed = 367,
		VisualProfession = 368,
		VisualSex = 369,
		RitualTargetInst = 370,
		SkillTimeOnSelectedTarget = 371,
		LastSaveXP = 372,
		ExtendedTime = 373,
		BurstRecharge = 374,
		FullAutoRecharge = 375,
		GatherAbstractAnim = 376,
		CastTargetAbstractAnim = 377,
		CastSelfAbstractAnim = 378,
		CriticalIncrease = 379,
		RangeIncreaserWeapon = 380,
		NanoRange = 381,
		SkillLockModifier = 382,
		InterruptModifier = 383,
		ACGEntranceStyles = 384,
		ChanceOfBreakOnSpellAttack = 385,
		ChanceOfBreakOnDebuff = 386,
		DieAnim = 387,
		TowerType = 388,
		Expansion = 389,
		LowresMesh = 390,
		CritialResistance = 391,
		SelectedTargetType = 397,
		Corpse_Hash = 398,
		AmmoName = 399,
		Rotation = 400,
		CATAnim = 401,
		CATAnimFlags = 402,
		DisplayCATAnim = 403,
		DisplayCATMesh = 404,
		School = 405,
		NanoPoints = 407,
		TrainSkill = 408,
		TrainSkillCost = 409,
		NumFightingOpponents = 410,
		MultipleCount = 412,
		EffectType = 413,
		ImpactEffectType = 414,
		CorpseType = 415,
		CorpseInstance = 416,
		CorpseAnimKey = 417,
		UnarmedTemplateInstance = 418,
		TracerEffectType = 419,
		AmmoType = 420,
		CharRadius = 421,
		ChanceOfUse = 422,
		CurrentState = 423,
		ArmourType = 424,
		RestModifier = 425,
		BuyModifier = 426,
		SellModifier = 427,
		CastEffectType = 428,
		NPCBrainState = 429,
		WaitState = 430,
		SelectedTarget = 431,
		ErrorCode = 432,
		OwnerInstance = 433,
		CharState = 434,
		ReadOnly = 435,
		DamageType2 = 436,
		CollideCheckInterval = 437,
		PlayfieldType = 438,
		NPCCommand = 439,
		InitiativeType = 440,
		CharTmp1 = 441,
		CharTmp2 = 442,
		CharTmp3 = 443,
		CharTmp4 = 444,
		NPCCommandArg = 445,
		NameTemplate = 446,
		DesiredTargetDistance = 447,
		VicinityRange = 448,
		NPCIsSurrendering = 449,
		StateMachine = 450,
		NPCSurrenderInstance = 451,
		NPCHasPatrolList = 452,
		NPCVicinityChars = 453,
		ProximityRangeOutdoors = 454,
		NPCFamily = 455,
		CommandRange = 456,
		NPCHatelistSize = 457,
		NPCNumPets = 458,
		EffectRed = 460,
		EffectGreen = 461,
		EffectBlue = 462,
		DurationModifier = 464,
		NPCCryForHelpRange = 465,
		PetReq1 = 467,
		PetReq2 = 468,
		PetReq3 = 469,
		MapOptions = 470,
		MapsA = 471,
		MapsB = 472,
		FixtureFlags = 473,
		FallDamage = 474,
		MaxReflectedProjectileDmg = 475,
		MaxReflectedMeleeDmg = 476,
		MaxReflectedEnergyDmg = 477,
		MaxReflectedChemicalDmg = 478,
		MaxReflectedRadiationDmg = 479,
		MaxReflectedColdDmg = 480,
		MaxReflectedNanoDmg = 481,
		MaxReflectedFireDmg = 482,
		MaxReflectedPoisonDmg = 483,
		ProximityRangeIndoors = 484,
		PetReqVal1 = 485,
		PetReqVal2 = 486,
		PetReqVal3 = 487,
		TargetFacing = 488,
		Backstab = 489,
		OriginatorType = 490,
		QuestInstance = 491,
		AnimPos = 500,
		AnimPlay = 501,
		AnimSpeed = 502,
		Tower_NPCHash = 511,
		PetType = 512,
		OnTowerCreation = 513,
		OwnedTowers = 514,
		TowerInstance = 515,
		AttackShield = 516,
		SpecialAttackShield = 517,
		NPCVicinityPlayers = 518,
		Rnd = 520,
		SocialStatus = 521,
		LastRnd = 522,
		AttackDelayCap = 523,
		RechargeDelayCap = 524,
		PercentRemainingHealth = 525,
		PercentRemainingNano = 526,
		TargetDistance = 527,
		TeamCloseness = 528,
		ExpansionPlayfield = 531,
		ShadowBreed = 532,
		DudChance = 534,
		HealMultiplier = 535,
		NanoDamageMultiplier = 536,
		NanoVulnerability = 537,
		AMSCap = 538,
		ProcInitiative1 = 539,
		ProcInitiative2 = 540,
		ProcInitiative3 = 541,
		ProcInitiative4 = 542,
		FactionModifier = 543,
		StackingLine2 = 546,
		StackingLine3 = 547,
		StackingLine4 = 548,
		StackingLine5 = 549,
		StackingLine6 = 550,
		StackingOrder = 551,
		ProcNano1 = 552,
		ProcNano2 = 553,
		ProcNano3 = 554,
		ProcNano4 = 555,
		ProcChance1 = 556,
		ProcChance2 = 557,
		ProcChance3 = 558,
		ProcChance4 = 559,
		OTArmedForces = 560,
		ClanSentinels = 561,
		OTMed = 562,
		ClanGaia = 563,
		OTTrans = 564,
		ClanVanguards = 565,
		GOS = 566,
		OTFollowers = 567,
		OTOperator = 568,
		OTUnredeemed = 569,
		ClanDevoted = 570,
		ClanConserver = 571,
		ClanRedeemed = 572,
		SK = 573,
		LastSK = 574,
		NextSK = 575,
		PlayerOptions = 576,
		LastPerkResetTime = 577,
		CurrentTime = 578,
		ShadowBreedTemplate = 579,
		NPCVicinityFamily = 580,
		NPCScriptAMSScale = 581,
		ApartmentsAllowed = 582,
		ApartmentsOwned = 583,
		ApartmentAccessCard = 584,
		MapsC = 585,
		MapsD = 586,
		NumberOfTeamMembers = 587,
		ActionCategory = 588,
		PlayfieldProxy = 589,
		UnsavedXP = 592,
		RegainXPPercentage = 593,
		ExtendedFlags = 598,
		NewbieHP = 600,
		HPLevelUp = 601,
		HPPerSkill = 602,
		NewbieNP = 603,
		NPLevelUp = 604,
		NPPerSkill = 605,
		MaxShopItems = 606,
		PlayerID = 607,
		ShopRent = 608,
		ShopFlags = 610,
		ShopLastUsed = 611,
		ShopType = 612,
		InvadersKilled = 615,
		KilledByInvaders = 616,
		HouseTemplate = 620,
		PercentFireDamage = 621,
		PercentColdDamage = 622,
		PercentMeleeDamage = 623,
		PercentProjectileDamage = 624,
		PercentPoisonDamage = 625,
		PercentRadiationDamage = 626,
		PercentEnergyDamage = 627,
		PercentChemicalDamage = 628,
		TotalDamage = 629,
		TrackProjectileDamage = 630,
		TrackMeleeDamage = 631,
		TrackEnergyDamage = 632,
		TrackChemicalDamage = 633,
		TrackRadiationDamage = 634,
		TrackColdDamage = 635,
		TrackPoisonDamage = 636,
		TrackFireDamage = 637,
		NPCSpellArg = 638,
		NPCSpellRet = 639,
		CityInstance = 640,
		DistanceToSpawnpoint = 641,
		HasUnreadMail = 649,
		AdvantageHash1 = 651,
		AdvantageHash2 = 652,
		AdvantageHash3 = 653,
		AdvantageHash4 = 654,
		AdvantageHash5 = 655,
		ShopIndex = 656,
		ShopID = 657,
		IsVehicle = 658,
		DamageToNano = 659,
		AccountFlags = 660,
		DamageToNanoMultiplier = 661,
		MechData = 662,
		PointValue = 663,
		VehicleAC = 664,
		VehicleDamage = 665,
		VehicleHealth = 666,
		VehicleSpeed = 667,
		BattlestationSide = 668,
		VP = 669,
		BattlestationRep = 670,
		PetState = 671,
		PaidPoints = 672,
		VisualFlags = 673,
		PVPDuelKills = 674,
		PVPDuelDeaths = 675,
		PVPProfessionDuelKills = 676,
		PVPProfessionDuelDeaths = 677,
		PVPRankedSoloKills = 678,
		PVPRankedSoloDeaths = 679,
		PVPRankedTeamKills = 680,
		PVPRankedTeamDeaths = 681,
		PVPSoloScore = 682,
		PVPTeamScore = 683,
		PVPDuelScore = 684,
		ACGItemSeed = 700,
		ACGItemLevel = 701,
		ACGItemTemplateID = 702,
		ACGItemTemplateID2 = 703,
		ACGItemCategoryID = 704,
		HasKnubotData = 768,
		QuestBoothDifficulty = 800,
		QuestASMinimumRange = 801,
		QuestASMaximumRange = 802,
		VisualLODLevel = 888,
		TargetDistanceChange = 889,
		TideRequiredDynelID = 900,
		Type = 1001,
		Instance = 1002
	}
	public struct Vector2
	{
		public float X;

		public float Y;

		public static readonly Vector2 Zero = new Vector2(0f, 0f);

		public Vector2(double x, double y)
		{
			X = (float)x;
			Y = (float)y;
		}

		public Vector2(float x, float y)
		{
			X = x;
			Y = y;
		}

		public static Vector2 AngleToVector(float angle, float mag = 1f)
		{
			float num = (float)(Math.PI * (double)angle / 180.0);
			return new Vector2((float)((double)mag * Math.Sin(num)), (float)((double)mag * Math.Sin(num)));
		}

		public float DistanceFrom(Vector2 v)
		{
			return (float)Math.Sqrt(Math.Pow(Math.Abs(X - v.X), 2.0) + Math.Pow(Math.Abs(Y - v.Y), 2.0));
		}

		public override string ToString()
		{
			return $"({X}, {Y})";
		}

		public Vector3 ToVector3()
		{
			return new Vector3(X, Y, 0f);
		}
	}
	[StructLayout(LayoutKind.Sequential, Size = 12)]
	public struct Vector3
	{
		public const float ZeroTolerance = 1E-06f;

		public static readonly Vector3 Zero = new Vector3(0f, 0f, 0f);

		public static readonly Vector3 Forward = new Vector3(0f, 0f, 1f);

		public static readonly Vector3 Right = new Vector3(1f, 0f, 0f);

		public static readonly Vector3 Up = new Vector3(0f, 1f, 0f);

		[AoMember(0)]
		public float X { get; set; }

		[AoMember(1)]
		public float Y { get; set; }

		[AoMember(2)]
		public float Z { get; set; }

		public double Magnitude
		{
			get
			{
				return Math.Sqrt(Math.Pow(X, 2.0) + Math.Pow(Y, 2.0) + Math.Pow(Z, 2.0));
			}
			set
			{
				if (value < 0.0)
				{
					throw new ArgumentOutOfRangeException("value", value, "The magnitude of a Vector must be positive or 0.");
				}
				if (Magnitude == 0.0)
				{
					throw new DivideByZeroException("Can not set the magnitude of a Vector with no direction");
				}
				double num = value / Magnitude;
				X = (float)((double)X * num);
				Y = (float)((double)Y * num);
				Z = (float)((double)Z * num);
			}
		}

		public Vector3(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public Vector3(double x, double y, double z)
		{
			X = (float)x;
			Y = (float)y;
			Z = (float)z;
		}

		public Vector3(double x, double y)
		{
			X = (float)x;
			Y = (float)y;
			Z = 0f;
		}

		public static float Angle(Vector3 from, Vector3 to)
		{
			return (float)Math.Acos(Dot(from.Normalize(), to.Normalize())) * 57.29578f;
		}

		public static float Distance(Vector3 from, Vector3 to)
		{
			return (float)Math.Sqrt(Math.Pow(Math.Abs(from.X - to.X), 2.0) + Math.Pow(Math.Abs(from.Y - to.Y), 2.0) + Math.Pow(Math.Abs(from.Z - to.Z), 2.0));
		}

		public float DistanceFrom(Vector3 pos)
		{
			return (float)Math.Sqrt(Math.Pow(Math.Abs(X - pos.X), 2.0) + Math.Pow(Math.Abs(Y - pos.Y), 2.0) + Math.Pow(Math.Abs(Z - pos.Z), 2.0));
		}

		public float Distance2DFrom(Vector3 pos)
		{
			return (float)Math.Sqrt(Math.Pow(Math.Abs(X - pos.X), 2.0) + Math.Pow(Math.Abs(Z - pos.Z), 2.0));
		}

		public Vector3 Translate(Vector2 vec)
		{
			return new Vector3(X + vec.X, Y, Z + vec.Y);
		}

		public static Vector3 Rotate(Vector3 pivot, Vector3 localPos, float angle)
		{
			localPos.X -= pivot.X;
			localPos.Z -= pivot.Z;
			float num = (float)Math.Sqrt(localPos.X * localPos.X + localPos.Z * localPos.Z);
			double num2 = Math.Atan2(localPos.Z, localPos.X) * 180.0 / Math.PI;
			double num3 = (num2 + (double)(360f - angle)) % 360.0 * Math.PI / 180.0;
			Vector3 result = new Vector3(0f, localPos.Y, 0f);
			result.X = pivot.X + num * (float)Math.Cos(num3);
			result.Z = pivot.Z + num * (float)Math.Sin(num3);
			return result;
		}

		public Vector3 PointOnLine(Vector3 start, Vector3 end)
		{
			Vector3 vector = end - start;
			Vector3 v = this - start;
			float num = vector.LengthSquared();
			float num2 = (float)vector.Dot(v);
			float num3 = num2 / num;
			if (num3 < 0f || num3 > 1f)
			{
				return Zero;
			}
			return start + vector * num3;
		}

		public Vector3 Randomize(float magnitude)
		{
			Random random = new Random();
			return this + new Vector3(random.Next((int)(0f - magnitude), (int)magnitude), 0f, random.Next((int)(0f - magnitude), (int)magnitude));
		}

		public float Length()
		{
			return (float)Math.Sqrt(X * X + Y * Y + Z * Z);
		}

		public float LengthSquared()
		{
			return X * X + Y * Y + Z * Z;
		}

		public static double Abs(Vector3 v1)
		{
			return v1.Magnitude;
		}

		public double Abs()
		{
			return Magnitude;
		}

		public static bool IsUnitVector(Vector3 v1)
		{
			return Math.Abs(v1.Magnitude - 1.0) <= double.Epsilon;
		}

		public bool IsUnitVector()
		{
			return IsUnitVector(this);
		}

		public static Vector3 Normalize(Vector3 v1)
		{
			if (v1.Magnitude == 0.0)
			{
				throw new DivideByZeroException("Can not normalize a Vector with no direction");
			}
			Vector3 result = v1;
			result.Magnitude = 1.0;
			return result;
		}

		public Vector3 Normalize()
		{
			return Normalize(this);
		}

		public static Vector3 Cross(Vector3 vLeft, Vector3 vRight)
		{
			return new Vector3(vLeft.Y * vRight.Z - vLeft.Z * vRight.Y, vLeft.Z * vRight.X - vLeft.X * vRight.Z, vLeft.X * vRight.Y - vLeft.Y * vRight.X);
		}

		public Vector3 Cross(Vector3 vRight)
		{
			return Cross(this, vRight);
		}

		public static double Dot(Vector3 v1, Vector3 v2)
		{
			return v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z;
		}

		public double Dot(Vector3 v1)
		{
			return Dot(this, v1);
		}

		public override string ToString()
		{
			return $"({X}, {Y}, {Z})";
		}

		public Vector2 ToVector2()
		{
			return new Vector2(X, Y);
		}

		public static Vector3 operator *(Vector3 v, float mag)
		{
			return new Vector3(v.X * mag, v.Y * mag, v.Z * mag);
		}

		public static Vector3 operator *(Vector3 v1, Vector3 v2)
		{
			return new Vector3(v1.X * v2.X, v1.Y * v2.Y, v1.Z * v2.Z);
		}

		public static Vector3 operator +(Vector3 v1, Vector3 v2)
		{
			return new Vector3(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
		}

		public static Vector3 operator -(Vector3 v1, Vector3 v2)
		{
			return new Vector3(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
		}

		public static Vector3 operator /(Vector3 v, float mag)
		{
			return new Vector3(v.X / mag, v.Y / mag, v.Z / mag);
		}

		public static Vector3 operator /(Vector3 v1, Vector3 v2)
		{
			return new Vector3(v1.X / v2.X, v1.Y / v2.Y, v1.Z / v2.Z);
		}

		public static bool operator ==(Vector3 v1, Vector3 v2)
		{
			if (v1.X == v2.X && v1.Y == v2.Y && v1.Z == v2.Z)
			{
				return true;
			}
			return false;
		}

		public static bool operator !=(Vector3 v1, Vector3 v2)
		{
			if (v1.X != v2.X || v1.Y != v2.Y || v1.Z != v2.Z)
			{
				return true;
			}
			return false;
		}

		public bool Equals(Vector3 pos)
		{
			return pos == this;
		}
	}
}
namespace AOSharp.Common.GameData.UI
{
	public enum ButtonState
	{
		Raised,
		Pressed,
		Hover
	}
	public enum WindowFlags
	{
		None = 0,
		IgnoreRaycast = 1,
		NoExit = 4,
		NoFade = 2048,
		ManualScale = 2304,
		AutoScale = 4096
	}
	public enum WindowStyle
	{
		Default = 0,
		Popup = 2
	}
}
