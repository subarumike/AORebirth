using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Serialization;
using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;
using SmokeLounge.AOtomation.Messaging.Serialization.Serializers;
using SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyCompany("SmokeLounge")]
[assembly: AssemblyCopyright("Copyright c SmokeLounge 2013")]
[assembly: AssemblyFileVersion("0.62.1.0")]
[assembly: AssemblyTitle("SmokeLounge.AOtomation.Messaging")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyProduct("SmokeLounge.AOtomation.Messaging")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("52f67401-e3ec-47d7-804e-ffd6c4dc77f2")]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: AssemblyVersion("0.62.1.0")]
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

		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Diagnostic Info:");
			stringBuilder.AppendLine("Length: " + Length.ToString("X8") + ", Offset: " + Offset.ToString("X8"));
			if (PropertyMetaData != null)
			{
				stringBuilder.AppendLine("Property: " + PropertyMetaData.Property.Name);
			}
			if (Value != null)
			{
				stringBuilder.AppendLine("Value: " + Value.ToString() + " of " + Value.GetType().Name);
			}
			return stringBuilder.ToString();
		}
	}
	public enum FlagsCriteria
	{
		HasAll,
		HasAny,
		EqualsToAny,
		HasNone,
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
	public class PacketInspector
	{
		private readonly TypeInfo typeInfo;

		public PacketInspector()
		{
			typeInfo = new TypeInfo(typeof(MessageBody));
		}

		public TypeInfo FindSubType(StreamReader reader)
		{
			TypeInfo typeInfo = this.typeInfo;
			while (typeInfo != null)
			{
				if (typeInfo.KnownType == null)
				{
					return typeInfo;
				}
				reader.Position = typeInfo.KnownType.Offset;
				int identifier;
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
			if (TryGetMethodInfo(lambdaExpression.Body, out var methodInfo))
			{
				return methodInfo;
			}
			throw new InvalidOperationException();
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
			if (type.IsValueType && !type.IsPrimitive)
			{
				return !type.IsEnum;
			}
			return false;
		}

		private static bool TryGetMethodInfo(Expression expression, out MethodInfo methodInfo)
		{
			methodInfo = null;
			if (expression is UnaryExpression unaryExpression)
			{
				return TryGetMethodInfo(unaryExpression.Operand, out methodInfo);
			}
			if (expression is ConstantExpression constantExpression)
			{
				methodInfo = constantExpression.Value as MethodInfo;
				return methodInfo != null;
			}
			if (expression is MethodCallExpression methodCallExpression)
			{
				if (methodCallExpression.Object != null && methodCallExpression.Object.NodeType == ExpressionType.Parameter)
				{
					methodInfo = methodCallExpression.Method;
					return true;
				}
				if (methodCallExpression.Object != null && TryGetMethodInfo(methodCallExpression.Object, out methodInfo))
				{
					return true;
				}
				foreach (Expression argument in methodCallExpression.Arguments)
				{
					if (TryGetMethodInfo(argument, out methodInfo))
					{
						return true;
					}
				}
			}
			return false;
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
				FlagsCriteria.HasNone => EvaluateHasNone(usesFlags), 
				FlagsCriteria.Default => true, 
				_ => throw new ArgumentOutOfRangeException(), 
			};
		}

		private bool EvaluateHasNone(AoUsesFlagsAttribute usesFlags)
		{
			int flagValue = GetFlagValue(usesFlags.Flag);
			return usesFlags.CriteriaValues.All((int v) => (v & flagValue) == 0);
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

		public void WriteByte(byte value)
		{
			writer.Write(value);
		}

		public void WriteSByte(sbyte value)
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

		public void WriteIdentity(Identity value)
		{
			writer.Write(IPAddress.HostToNetworkOrder((int)value.Type));
			writer.Write(IPAddress.HostToNetworkOrder(value.Instance));
		}
	}
	public class MessageSerializer
	{
		private readonly HeaderSerializer headerSerializer;

		private readonly PacketInspector packetInspector;

		private readonly SerializerResolver serializerResolver;

		public MessageSerializer()
		{
			packetInspector = new PacketInspector();
			serializerResolver = new SerializerResolverBuilder<MessageBody>().Build();
			headerSerializer = new HeaderSerializer();
		}

		public MessageSerializer(SerializerResolverBuilder serializerResolverBuilder)
		{
			packetInspector = new PacketInspector();
			serializerResolver = serializerResolverBuilder.Build();
			headerSerializer = new HeaderSerializer();
		}

		public Message Deserialize(Stream stream)
		{
			SerializationContext serializationContext;
			return Deserialize(stream, out serializationContext);
		}

		public Message Deserialize(Stream stream, out SerializationContext serializationContext)
		{
			serializationContext = null;
			StreamReader streamReader = new StreamReader(stream)
			{
				Position = 0L
			};
			TypeInfo typeInfo = packetInspector.FindSubType(streamReader);
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
				Body = (MessageBody)serializer.Deserialize(streamReader, serializationContext)
			};
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
				long position = streamWriter.Position;
				streamWriter.Position = 6L;
				streamWriter.WriteInt16((short)position);
			}
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

		public long Length => stream.Length;

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

		public byte ReadByte()
		{
			return reader.ReadByte();
		}

		public sbyte ReadSByte()
		{
			return reader.ReadSByte();
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

		public Identity ReadIdentity()
		{
			IdentityType type = (IdentityType)ReadInt32();
			int instance = ReadInt32();
			Identity result = default(Identity);
			result.Type = type;
			result.Instance = instance;
			return result;
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
			subTypes.Clear();
			IEnumerable<Type> enumerable = from t in type.Assembly.GetTypes()
				where t.BaseType == type
				select t;
			foreach (Type item in enumerable)
			{
				AoContractAttribute aoContractAttribute = item.GetCustomAttributes(typeof(AoContractAttribute), inherit: false).Cast<AoContractAttribute>().FirstOrDefault();
				if (aoContractAttribute != null)
				{
					TypeInfo value = new TypeInfo(item);
					subTypes.Add(aoContractAttribute.Identifier, value);
				}
			}
		}
	}
	public abstract class SerializerResolverBuilder
	{
		public abstract SerializerResolver Build();

		internal abstract ISerializer GetSerializer(Type type);
	}
	public class SerializerResolverBuilder<T> : SerializerResolverBuilder
	{
		private readonly Dictionary<Type, ISerializer> serializers;

		public SerializerResolverBuilder()
		{
			serializers = new Dictionary<Type, ISerializer>
			{
				{
					typeof(byte),
					new ByteSerializer()
				},
				{
					typeof(short),
					new Int16Serializer()
				},
				{
					typeof(int),
					new Int32Serializer()
				},
				{
					typeof(long),
					new Int64Serializer()
				},
				{
					typeof(IPAddress),
					new IPAddressSerializer()
				},
				{
					typeof(float),
					new SingleSerializer()
				},
				{
					typeof(string),
					new StringSerializer()
				},
				{
					typeof(ushort),
					new UInt16Serializer()
				},
				{
					typeof(uint),
					new UInt32Serializer()
				},
				{
					typeof(PlayfieldVendorInfo),
					new PlayfieldVendorInfoSerializer()
				},
				{
					typeof(SimpleCharFullUpdateMessage),
					new SimpleCharFullUpdateSerializer()
				},
				{
					typeof(FollowInfo),
					new FollowInfoSerializer()
				},
				{
					typeof(VendingMachineFullUpdateMessage),
					new VendingMachineFullUpdateMessageSerializer()
				},
				{
					typeof(GenericCmdMessage),
					new GenericCmdSerializer()
				},
				{
					typeof(AOTransportSignalMessage),
					new AOTransportSignalMessageSerializer()
				},
				{
					typeof(N3TeleportMessage),
					new N3TeleportMessageSerializer()
				},
				{
					typeof(PlayfieldAnarchyFMessage),
					new PlayfieldAnarchyFMessageSerializer()
				},
				{
					typeof(QuestFullUpdateMessage),
					new QuestFullUpdateMessageSerializer()
				},
				{
					typeof(ResurrectMessage),
					new ResurrectMessageSerializer()
				},
				{
					typeof(ToClientQuitMessage),
					new KeyOnlyN3MessageSerializer(typeof(ToClientQuitMessage))
				},
				{
					typeof(DropDynelMessage),
					new DropDynelMessageSerializer()
				},
				{
					typeof(RelocateDynelsMessage),
					new RelocateDynelsMessageSerializer()
				},
				{
					typeof(LocalityUpdateMessage),
					new LocalityUpdateMessageSerializer()
				},
				{
					typeof(ClientContainerAddItemMessage),
					new ClientContainerAddItemMessageSerializer()
				},
				{
					typeof(ClientGetItemMessage),
					new ClientGetItemMessageSerializer()
				},
				{
					typeof(StartLogoutMessage),
					new IdentityOnlyN3MessageSerializer(typeof(StartLogoutMessage))
				},
				{
					typeof(StopLogoutMessage),
					new IdentityOnlyN3MessageSerializer(typeof(StopLogoutMessage))
				}
			};
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
						serializers.Add(item, serializer);
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
				serializers.Add(type, arraySerializer);
				return arraySerializer;
			}
			value = CreateSerializer(type);
			if (value != null)
			{
				serializers.Add(type, value);
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
					Expression expression = Expression.Property(deserializedObject, propertyMeta.Property);
					MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<SerializationContext, Action<string, int>>>)((SerializationContext o) => o.SetFlagValue));
					yield return Expression.Call(serializationContextExpression, methodInfo, Expression.Constant(propertyMeta.FlagsAttribute.Flag, typeof(string)), Expression.Convert(expression, typeof(int)));
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
					Expression expression = Expression.Property(objectToSerialize, propertyMeta.Property);
					MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<SerializationContext, Action<string, int>>>)((SerializationContext o) => o.SetFlagValue));
					yield return Expression.Call(serializationContextExpression, methodInfo, Expression.Constant(propertyMeta.FlagsAttribute.Flag, typeof(string)), Expression.Convert(expression, typeof(int)));
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
			Array array = Array.CreateInstance(typeSerializer.Type, 0);
			if (propertyMetaData.Options.SerializeSize == ArraySizeType.NullTerminated)
			{
				List<object> list = new List<object>();
				int num = 0;
				do
				{
					num = streamReader.ReadInt32();
					if (num != 0)
					{
						streamReader.Position -= 4L;
						object item = typeSerializer.Deserialize(streamReader, serializationContext, propertyMetaData);
						list.Add(item);
					}
				}
				while (num != 0);
			}
			else
			{
				int num2;
				if (propertyMetaData.Options.SerializeSize != 0)
				{
					ArraySizeSerializer arraySizeSerializer = new ArraySizeSerializer(propertyMetaData.Options.SerializeSize);
					num2 = (int)arraySizeSerializer.Deserialize(streamReader, serializationContext, propertyMetaData);
				}
				else
				{
					num2 = propertyMetaData.Options.FixedSizeLength;
				}
				array = Array.CreateInstance(typeSerializer.Type, num2);
				for (int i = 0; i < num2; i++)
				{
					object value = typeSerializer.Deserialize(streamReader, serializationContext, propertyMetaData);
					array.SetValue(value, i);
				}
			}
			return array;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			List<Expression> list = new List<Expression>();
			ParameterExpression parameterExpression = Expression.Variable(typeof(int), "size");
			ParameterExpression parameterExpression2 = Expression.Parameter(this.type, "newArray");
			ParameterExpression parameterExpression3 = Expression.Variable(typeof(int), "i");
			ParameterExpression parameterExpression4 = Expression.Variable(typeSerializer.Type, "element");
			LabelTarget labelTarget = Expression.Label();
			if (propertyMetaData.Options.SerializeSize == ArraySizeType.NullTerminated)
			{
				Type type = typeof(List<>).MakeGenericType(typeSerializer.Type);
				ParameterExpression parameterExpression5 = Expression.Variable(type, "xt1");
				NewExpression right = Expression.New(type);
				list.Add(Expression.Assign(parameterExpression5, right));
				BinaryExpression binaryExpression = Expression.Assign(parameterExpression, Expression.Call(streamReaderExpression, ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<int>>>)((StreamReader o) => o.ReadInt32))));
				MemberExpression left = Expression.PropertyOrField(streamReaderExpression, "Position");
				ParameterExpression[] variables = new ParameterExpression[1] { parameterExpression4 };
				Expression[] obj = new Expression[3]
				{
					Expression.SubtractAssign(left, Expression.Constant(4L)),
					typeSerializer.DeserializerExpression(streamReaderExpression, serializationContextExpression, parameterExpression4, propertyMetaData),
					null
				};
				MethodInfo method = typeof(List<>).MakeGenericType(typeSerializer.Type).GetMethod("Add");
				Expression[] arguments = new ParameterExpression[1] { parameterExpression4 };
				obj[2] = Expression.Call(parameterExpression5, method, arguments);
				BlockExpression ifTrue = Expression.Block(variables, obj);
				BlockExpression body = Expression.Block(new ParameterExpression[1] { parameterExpression }, binaryExpression, Expression.IfThenElse(Expression.NotEqual(parameterExpression, Expression.Constant(0)), ifTrue, Expression.Break(labelTarget)));
				list.Add(Expression.Assign(parameterExpression, Expression.Constant(1)));
				list.Add(Expression.Loop(body, labelTarget));
				list.Add(Expression.Assign(parameterExpression2, Expression.Call(parameterExpression5, typeof(List<>).MakeGenericType(typeSerializer.Type).GetMethod("ToArray"))));
				BinaryExpression item = Expression.Assign(assignmentTargetExpression, Expression.Convert(parameterExpression2, this.type));
				list.Add(item);
				return Expression.Block(new ParameterExpression[4] { parameterExpression5, parameterExpression, parameterExpression2, parameterExpression3 }, list);
			}
			Expression item2 = ((propertyMetaData.Options.SerializeSize == ArraySizeType.NoSerialization) ? Expression.Assign(parameterExpression, Expression.Constant(propertyMetaData.Options.FixedSizeLength, typeof(int))) : new ArraySizeSerializer(propertyMetaData.Options.SerializeSize).DeserializerExpression(streamReaderExpression, serializationContextExpression, parameterExpression, propertyMetaData));
			list.Add(item2);
			NewArrayExpression right2 = Expression.NewArrayBounds(typeSerializer.Type, parameterExpression);
			list.Add(Expression.Assign(parameterExpression2, right2));
			list.Add(Expression.Assign(parameterExpression3, Expression.Constant(0)));
			BinaryExpression binaryExpression2 = Expression.Assign(Expression.ArrayAccess(parameterExpression2, parameterExpression3), parameterExpression4);
			ConditionalExpression body2 = Expression.IfThenElse(Expression.LessThan(parameterExpression3, parameterExpression), Expression.Block(new ParameterExpression[1] { parameterExpression4 }, typeSerializer.DeserializerExpression(streamReaderExpression, serializationContextExpression, parameterExpression4, propertyMetaData), binaryExpression2, Expression.Assign(parameterExpression3, Expression.Increment(parameterExpression3))), Expression.Break(labelTarget));
			LoopExpression item3 = Expression.Loop(body2, labelTarget);
			list.Add(item3);
			BinaryExpression item4 = Expression.Assign(assignmentTargetExpression, Expression.Convert(parameterExpression2, this.type));
			list.Add(item4);
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
			if (propertyMetaData.Options.SerializeSize != 0 && propertyMetaData.Options.SerializeSize != ArraySizeType.NullTerminated)
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
			if (propertyMetaData.Options.SerializeSize == ArraySizeType.NullTerminated)
			{
				MethodInfo methodInfo = null;
				methodInfo = ((propertyMetaData.Type == typeof(string) || propertyMetaData.Type == typeof(byte)) ? ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<byte>>>)((StreamWriter o) => o.WriteByte)) : ((!(propertyMetaData.Type == typeof(short))) ? ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<int>>>)((StreamWriter o) => o.WriteInt32)) : ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<short>>>)((StreamWriter o) => o.WriteInt16))));
				MethodInfo method = methodInfo;
				Expression[] arguments = new ConstantExpression[1] { Expression.Constant(0) };
				Expression item3 = Expression.Call(streamWriterExpression, method, arguments);
				list.Add(item3);
			}
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
			case ArraySizeType.NullTerminated:
				type = null;
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
			case ArraySizeType.NullTerminated:
				return null;
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
			if (arraySizeType != 0 && arraySizeType != ArraySizeType.NullTerminated)
			{
				int num = ((value is Array array) ? array.Length : ((string)value).Length);
				switch (arraySizeType)
				{
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
				case ArraySizeType.NoSerialization:
					break;
				}
			}
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			if (arraySizeType == ArraySizeType.NoSerialization || arraySizeType == ArraySizeType.NullTerminated)
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
	public class SByteSerializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public SByteSerializer()
		{
			type = typeof(sbyte);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return streamReader.ReadSByte();
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<sbyte>>>)((StreamReader o) => o.ReadSByte));
			MethodCallExpression methodCallExpression = Expression.Call(streamReaderExpression, methodInfo);
			if (assignmentTargetExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Assign(assignmentTargetExpression, methodCallExpression);
			}
			return Expression.Assign(assignmentTargetExpression, Expression.Convert(methodCallExpression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			streamWriter.WriteSByte((sbyte)value);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamWriter, Action<sbyte>>>)((StreamWriter o) => o.WriteSByte));
			if (valueExpression.Type.IsAssignableFrom(type))
			{
				return Expression.Call(streamWriterExpression, methodInfo, valueExpression);
			}
			return Expression.Call(streamWriterExpression, methodInfo, Expression.Convert(valueExpression, type));
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
				ArraySizeSerializer arraySizeSerializer = new ArraySizeSerializer(propertyMetaData.Options.SerializeSize);
				length = (int)arraySizeSerializer.Deserialize(streamReader, serializationContext, propertyMetaData);
			}
			return streamReader.ReadString(length);
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			List<Expression> list = new List<Expression>();
			ParameterExpression parameterExpression = Expression.Variable(typeof(int), "length");
			Expression item = ((propertyMetaData.Options.SerializeSize != 0) ? new ArraySizeSerializer(propertyMetaData.Options.SerializeSize).DeserializerExpression(streamReaderExpression, serializationContextExpression, parameterExpression, propertyMetaData) : Expression.Assign(parameterExpression, Expression.Constant(propertyMetaData.Options.FixedSizeLength, typeof(int))));
			list.Add(item);
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<StreamReader, Func<int, string>>>)((StreamReader o) => o.ReadString));
			MethodCallExpression methodCallExpression = Expression.Call(streamReaderExpression, methodInfo, parameterExpression);
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
			if (propertyMetaData.Options.SerializeSize != 0)
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
			object obj = null;
			long position = streamReader.Position;
			try
			{
				return DeserializerLambda(streamReader, serializationContext);
			}
			catch (Exception ex2)
			{
				Probe probe = serializationContext.BeginProbe();
				try
				{
					streamReader.Position = position;
					obj = DeserializerLambda(streamReader, serializationContext);
				}
				catch (Exception)
				{
				}
				serializationContext.EndProbe(probe);
				throw new Exception("TypeSerializer failed (" + Type.ToString() + ")." + Environment.NewLine + ex2.Message + string.Join(Environment.NewLine, probe.DiagnosticInfo) + Environment.NewLine, ex2);
			}
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
}
namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
	public class AOTransportSignalMessageSerializer : ISerializer
	{
		public Type Type => typeof(AOTransportSignalMessage);

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			AOTransportSignalMessage aOTransportSignalMessage = new AOTransportSignalMessage
			{
				N3MessageType = (N3MessageType)streamReader.ReadInt32(),
				Identity = streamReader.ReadIdentity(),
				Unknown = streamReader.ReadByte(),
				Signal = streamReader.ReadInt32()
			};
			int num = (int)(streamReader.Length - streamReader.Position);
			aOTransportSignalMessage.Payload = ((num > 0) ? streamReader.ReadBytes(num) : new byte[0]);
			return aOTransportSignalMessage;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<AOTransportSignalMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((AOTransportSignalMessageSerializer o) => o.Deserialize));
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
			AOTransportSignalMessage aOTransportSignalMessage = (AOTransportSignalMessage)value;
			byte[] buffer = aOTransportSignalMessage.Payload ?? new byte[0];
			streamWriter.WriteInt32((int)aOTransportSignalMessage.N3MessageType);
			streamWriter.WriteIdentity(aOTransportSignalMessage.Identity);
			streamWriter.WriteByte(aOTransportSignalMessage.Unknown);
			streamWriter.WriteInt32(aOTransportSignalMessage.Signal);
			streamWriter.WriteBytes(buffer);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<AOTransportSignalMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((AOTransportSignalMessageSerializer o) => o.Serialize));
			NewExpression instance = Expression.New(GetType());
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	public class FollowInfoSerializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public FollowInfoSerializer()
		{
			type = typeof(FollowInfo);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			switch (streamReader.ReadByte())
			{
			case 1:
			{
				FollowCoordinateInfo followCoordinateInfo = new FollowCoordinateInfo();
				followCoordinateInfo.FollowInfoType = 1;
				followCoordinateInfo.MoveMode = streamReader.ReadByte();
				followCoordinateInfo.CoordinateCount = streamReader.ReadByte();
				for (int i = 0; i < followCoordinateInfo.CoordinateCount; i++)
				{
					followCoordinateInfo.Coordinates.Add(ReadVector3(streamReader));
				}
				if (followCoordinateInfo.Coordinates.Count > 0)
				{
					followCoordinateInfo.CurrentCoordinates = followCoordinateInfo.Coordinates[0];
					followCoordinateInfo.EndCoordinates = followCoordinateInfo.Coordinates[followCoordinateInfo.Coordinates.Count - 1];
				}
				return followCoordinateInfo;
			}
			case 2:
			{
				long position = streamReader.Position;
				byte b = streamReader.ReadByte();
				if (b == 21 && streamReader.Length - streamReader.Position >= 37)
				{
					FollowStopInfo followStopInfo = new FollowStopInfo();
					followStopInfo.FollowInfoType = 2;
					followStopInfo.MoveType = b;
					followStopInfo.Unknown1 = streamReader.ReadInt32();
					followStopInfo.Unknown2 = streamReader.ReadInt32();
					followStopInfo.Unknown3 = streamReader.ReadInt32();
					followStopInfo.Coordinates = ReadVector3(streamReader);
					followStopInfo.Flag = streamReader.ReadByte();
					followStopInfo.ConfirmCoordinates = ReadVector3(streamReader);
					return followStopInfo;
				}
				if (b == 25 && streamReader.Length - streamReader.Position >= 25)
				{
					FollowPositionInfo followPositionInfo = new FollowPositionInfo();
					followPositionInfo.FollowInfoType = 2;
					followPositionInfo.MoveType = b;
					followPositionInfo.Unknown1 = streamReader.ReadInt32();
					followPositionInfo.Unknown2 = streamReader.ReadInt32();
					followPositionInfo.Unknown3 = streamReader.ReadInt32();
					followPositionInfo.Coordinates = ReadVector3(streamReader);
					followPositionInfo.Unknown4 = streamReader.ReadByte();
					return followPositionInfo;
				}
				streamReader.Position = position;
				FollowTargetInfo followTargetInfo = new FollowTargetInfo();
				followTargetInfo.FollowInfoType = 2;
				followTargetInfo.MoveType = streamReader.ReadByte();
				IdentityType identityType = (IdentityType)streamReader.ReadInt32();
				followTargetInfo.Target = new Identity
				{
					Type = identityType,
					Instance = streamReader.ReadInt32()
				};
				followTargetInfo.Dummy = streamReader.ReadByte();
				followTargetInfo.Dummy1 = streamReader.ReadInt32();
				followTargetInfo.X = streamReader.ReadSingle();
				followTargetInfo.Y = streamReader.ReadSingle();
				followTargetInfo.Z = streamReader.ReadSingle();
				return followTargetInfo;
			}
			default:
				streamReader.Position--;
				return null;
			}
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			if (value == null)
			{
				return;
			}
			if (value is FollowTargetInfo followTargetInfo)
			{
				streamWriter.WriteByte(followTargetInfo.FollowInfoType);
				streamWriter.WriteByte(followTargetInfo.MoveType);
				streamWriter.WriteInt32((int)followTargetInfo.Target.Type);
				streamWriter.WriteInt32(followTargetInfo.Target.Instance);
				streamWriter.WriteByte(followTargetInfo.Dummy);
				streamWriter.WriteInt32(followTargetInfo.Dummy1);
				streamWriter.WriteSingle(followTargetInfo.X);
				streamWriter.WriteSingle(followTargetInfo.Y);
				streamWriter.WriteSingle(followTargetInfo.Z);
			}
			if (value is FollowPositionInfo followPositionInfo)
			{
				streamWriter.WriteByte(followPositionInfo.FollowInfoType);
				streamWriter.WriteByte(followPositionInfo.MoveType);
				streamWriter.WriteInt32(followPositionInfo.Unknown1);
				streamWriter.WriteInt32(followPositionInfo.Unknown2);
				streamWriter.WriteInt32(followPositionInfo.Unknown3);
				WriteVector3(streamWriter, followPositionInfo.Coordinates);
				streamWriter.WriteByte(followPositionInfo.Unknown4);
			}
			if (value is FollowStopInfo { Coordinates: var coordinates, ConfirmCoordinates: var vector } followStopInfo)
			{
				if (vector == null)
				{
					vector = coordinates;
				}
				streamWriter.WriteByte(followStopInfo.FollowInfoType);
				streamWriter.WriteByte(followStopInfo.MoveType);
				streamWriter.WriteInt32(followStopInfo.Unknown1);
				streamWriter.WriteInt32(followStopInfo.Unknown2);
				streamWriter.WriteInt32(followStopInfo.Unknown3);
				WriteVector3(streamWriter, coordinates);
				streamWriter.WriteByte(followStopInfo.Flag);
				WriteVector3(streamWriter, vector);
			}
			if (!(value is FollowCoordinateInfo followCoordinateInfo))
			{
				return;
			}
			IList<Vector3> coordinates2 = GetCoordinates(followCoordinateInfo);
			streamWriter.WriteByte(followCoordinateInfo.FollowInfoType);
			streamWriter.WriteByte(followCoordinateInfo.MoveMode);
			streamWriter.WriteByte((byte)coordinates2.Count);
			foreach (Vector3 item in coordinates2)
			{
				WriteVector3(streamWriter, item);
			}
		}

		private IList<Vector3> GetCoordinates(FollowCoordinateInfo fcinfo)
		{
			if (fcinfo.Coordinates != null && fcinfo.Coordinates.Count > 0)
			{
				return fcinfo.Coordinates;
			}
			List<Vector3> list = new List<Vector3>();
			if (fcinfo.CurrentCoordinates != null)
			{
				list.Add(fcinfo.CurrentCoordinates);
			}
			if (fcinfo.EndCoordinates != null)
			{
				list.Add(fcinfo.EndCoordinates);
			}
			return list;
		}

		private Vector3 ReadVector3(StreamReader streamReader)
		{
			return new Vector3
			{
				X = streamReader.ReadSingle(),
				Y = streamReader.ReadSingle(),
				Z = streamReader.ReadSingle()
			};
		}

		private void WriteVector3(StreamWriter streamWriter, Vector3 value)
		{
			if (value == null)
			{
				value = new Vector3();
			}
			streamWriter.WriteSingle(value.X);
			streamWriter.WriteSingle(value.Y);
			streamWriter.WriteSingle(value.Z);
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<FollowInfoSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((FollowInfoSerializer o) => o.Deserialize));
			NewExpression instance = Expression.New(GetType());
			MethodCallExpression expression = Expression.Call(instance, methodInfo, new Expression[3]
			{
				streamReaderExpression,
				serializationContextExpression,
				Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
			});
			return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(expression, assignmentTargetExpression.Type));
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<FollowInfoSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((FollowInfoSerializer o) => o.Serialize));
			NewExpression instance = Expression.New(GetType());
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	public class GenericCmdSerializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public GenericCmdSerializer()
		{
			type = typeof(GenericCmdMessage);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			GenericCmdMessage genericCmdMessage = new GenericCmdMessage();
			genericCmdMessage.N3MessageType = (N3MessageType)streamReader.ReadInt32();
			genericCmdMessage.Identity = streamReader.ReadIdentity();
			genericCmdMessage.Unknown = streamReader.ReadByte();
			genericCmdMessage.Temp1 = streamReader.ReadInt32();
			genericCmdMessage.Count = streamReader.ReadInt32();
			genericCmdMessage.Action = (GenericCmdAction)streamReader.ReadInt32();
			genericCmdMessage.Temp4 = streamReader.ReadInt32();
			genericCmdMessage.User = streamReader.ReadIdentity();
			int num = 1;
			if (genericCmdMessage.Action == GenericCmdAction.UseItemOnItem)
			{
				num = 2;
			}
			genericCmdMessage.Target = new Identity[num];
			for (int i = 0; i < genericCmdMessage.Target.Length; i++)
			{
				genericCmdMessage.Target[i] = streamReader.ReadIdentity();
			}
			return genericCmdMessage;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<GenericCmdSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((GenericCmdSerializer o) => o.Deserialize));
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
			GenericCmdMessage genericCmdMessage = (GenericCmdMessage)value;
			streamWriter.WriteInt32((int)genericCmdMessage.N3MessageType);
			streamWriter.WriteIdentity(genericCmdMessage.Identity);
			streamWriter.WriteByte(genericCmdMessage.Unknown);
			streamWriter.WriteInt32(genericCmdMessage.Temp1);
			streamWriter.WriteInt32(genericCmdMessage.Count);
			streamWriter.WriteInt32((int)genericCmdMessage.Action);
			streamWriter.WriteInt32(genericCmdMessage.Temp4);
			streamWriter.WriteIdentity(genericCmdMessage.User);
			Identity[] target = genericCmdMessage.Target;
			foreach (Identity value2 in target)
			{
				streamWriter.WriteIdentity(value2);
			}
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<GenericCmdSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((GenericCmdSerializer o) => o.Serialize));
			NewExpression instance = Expression.New(GetType());
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	public class IdentityOnlyN3MessageSerializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public IdentityOnlyN3MessageSerializer(Type type)
		{
			this.type = type;
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			N3Message n3Message = (N3Message)Activator.CreateInstance(type);
			n3Message.N3MessageType = (N3MessageType)streamReader.ReadInt32();
			n3Message.Identity = streamReader.ReadIdentity();
			return n3Message;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<IdentityOnlyN3MessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((IdentityOnlyN3MessageSerializer o) => o.Deserialize));
			ConstructorInfo constructor = GetType().GetConstructor(new Type[1] { typeof(Type) });
			NewExpression instance = Expression.New(constructor, Expression.Constant(type, typeof(Type)));
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
			N3Message n3Message = (N3Message)value;
			streamWriter.WriteInt32((int)n3Message.N3MessageType);
			streamWriter.WriteIdentity(n3Message.Identity);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<IdentityOnlyN3MessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((IdentityOnlyN3MessageSerializer o) => o.Serialize));
			ConstructorInfo constructor = GetType().GetConstructor(new Type[1] { typeof(Type) });
			NewExpression instance = Expression.New(constructor, Expression.Constant(type, typeof(Type)));
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	public class N3TeleportMessageSerializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public N3TeleportMessageSerializer()
		{
			type = typeof(N3TeleportMessage);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			N3TeleportMessage n3TeleportMessage = new N3TeleportMessage();
			n3TeleportMessage.N3MessageType = (N3MessageType)streamReader.ReadInt32();
			n3TeleportMessage.Identity = streamReader.ReadIdentity();
			n3TeleportMessage.Unknown = streamReader.ReadByte();
			n3TeleportMessage.Destination = new Vector3
			{
				X = streamReader.ReadSingle(),
				Y = streamReader.ReadSingle(),
				Z = streamReader.ReadSingle()
			};
			n3TeleportMessage.Heading = new Quaternion
			{
				X = streamReader.ReadSingle(),
				Y = streamReader.ReadSingle(),
				Z = streamReader.ReadSingle(),
				W = streamReader.ReadSingle()
			};
			n3TeleportMessage.Unknown1 = streamReader.ReadByte();
			n3TeleportMessage.Playfield = streamReader.ReadIdentity();
			n3TeleportMessage.GameServerId = streamReader.ReadInt32();
			n3TeleportMessage.SgId = streamReader.ReadInt32();
			n3TeleportMessage.ChangePlayfield = streamReader.ReadIdentity();
			n3TeleportMessage.Unknown4 = streamReader.ReadInt32();
			n3TeleportMessage.Unknown5 = streamReader.ReadInt32();
			n3TeleportMessage.Playfield2 = streamReader.ReadIdentity();
			n3TeleportMessage.Unknown6 = streamReader.ReadInt32();
			n3TeleportMessage.Payload = ((n3TeleportMessage.Unknown6 > 0) ? streamReader.ReadBytes(n3TeleportMessage.Unknown6) : new byte[0]);
			return n3TeleportMessage;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<N3TeleportMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((N3TeleportMessageSerializer o) => o.Deserialize));
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
			N3TeleportMessage n3TeleportMessage = (N3TeleportMessage)value;
			byte[] array = n3TeleportMessage.Payload ?? new byte[0];
			streamWriter.WriteInt32((int)n3TeleportMessage.N3MessageType);
			streamWriter.WriteIdentity(n3TeleportMessage.Identity);
			streamWriter.WriteByte(n3TeleportMessage.Unknown);
			streamWriter.WriteSingle(n3TeleportMessage.Destination.X);
			streamWriter.WriteSingle(n3TeleportMessage.Destination.Y);
			streamWriter.WriteSingle(n3TeleportMessage.Destination.Z);
			streamWriter.WriteSingle(n3TeleportMessage.Heading.X);
			streamWriter.WriteSingle(n3TeleportMessage.Heading.Y);
			streamWriter.WriteSingle(n3TeleportMessage.Heading.Z);
			streamWriter.WriteSingle(n3TeleportMessage.Heading.W);
			streamWriter.WriteByte(n3TeleportMessage.Unknown1);
			streamWriter.WriteIdentity(n3TeleportMessage.Playfield);
			streamWriter.WriteInt32(n3TeleportMessage.GameServerId);
			streamWriter.WriteInt32(n3TeleportMessage.SgId);
			streamWriter.WriteIdentity(n3TeleportMessage.ChangePlayfield);
			streamWriter.WriteInt32(n3TeleportMessage.Unknown4);
			streamWriter.WriteInt32(n3TeleportMessage.Unknown5);
			streamWriter.WriteIdentity(n3TeleportMessage.Playfield2);
			streamWriter.WriteInt32(array.Length);
			streamWriter.WriteBytes(array);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<N3TeleportMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((N3TeleportMessageSerializer o) => o.Serialize));
			NewExpression instance = Expression.New(GetType());
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	public class PlayfieldAnarchyFMessageSerializer : ISerializer
	{
		public Type Type => typeof(PlayfieldAnarchyFMessage);

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			PlayfieldAnarchyFMessage playfieldAnarchyFMessage = new PlayfieldAnarchyFMessage
			{
				N3MessageType = (N3MessageType)streamReader.ReadInt32(),
				Identity = streamReader.ReadIdentity(),
				Unknown = streamReader.ReadByte(),
				Unknown1 = streamReader.ReadInt32(),
				CharacterCoordinates = new Vector3
				{
					X = streamReader.ReadSingle(),
					Y = streamReader.ReadSingle(),
					Z = streamReader.ReadSingle()
				},
				Unknown2 = streamReader.ReadByte(),
				PlayfieldId1 = streamReader.ReadIdentity(),
				Unknown3 = streamReader.ReadInt32(),
				Unknown4 = streamReader.ReadInt32(),
				PlayfieldId2 = streamReader.ReadIdentity()
			};
			int num = (int)(streamReader.Length - streamReader.Position);
			if (num <= 0)
			{
				return playfieldAnarchyFMessage;
			}
			if (LooksLikeGeneratorPayload(streamReader, num))
			{
				playfieldAnarchyFMessage.GeneratorPayload = streamReader.ReadBytes(num);
				return playfieldAnarchyFMessage;
			}
			playfieldAnarchyFMessage.Unknown5 = streamReader.ReadInt32();
			playfieldAnarchyFMessage.Unknown6 = streamReader.ReadInt32();
			playfieldAnarchyFMessage.PlayfieldVendorInfo = (PlayfieldVendorInfo)new PlayfieldVendorInfoSerializer().Deserialize(streamReader, serializationContext, propertyMetaData);
			playfieldAnarchyFMessage.PlayfieldX = streamReader.ReadInt32();
			playfieldAnarchyFMessage.PlayfieldZ = streamReader.ReadInt32();
			return playfieldAnarchyFMessage;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<PlayfieldAnarchyFMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((PlayfieldAnarchyFMessageSerializer o) => o.Deserialize));
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
			PlayfieldAnarchyFMessage playfieldAnarchyFMessage = (PlayfieldAnarchyFMessage)value;
			streamWriter.WriteInt32((int)playfieldAnarchyFMessage.N3MessageType);
			streamWriter.WriteIdentity(playfieldAnarchyFMessage.Identity);
			streamWriter.WriteByte(playfieldAnarchyFMessage.Unknown);
			streamWriter.WriteInt32(playfieldAnarchyFMessage.Unknown1);
			streamWriter.WriteSingle(playfieldAnarchyFMessage.CharacterCoordinates.X);
			streamWriter.WriteSingle(playfieldAnarchyFMessage.CharacterCoordinates.Y);
			streamWriter.WriteSingle(playfieldAnarchyFMessage.CharacterCoordinates.Z);
			streamWriter.WriteByte(playfieldAnarchyFMessage.Unknown2);
			streamWriter.WriteIdentity(playfieldAnarchyFMessage.PlayfieldId1);
			streamWriter.WriteInt32(playfieldAnarchyFMessage.Unknown3);
			streamWriter.WriteInt32(playfieldAnarchyFMessage.Unknown4);
			streamWriter.WriteIdentity(playfieldAnarchyFMessage.PlayfieldId2);
			if (playfieldAnarchyFMessage.GeneratorPayload != null)
			{
				streamWriter.WriteBytes(playfieldAnarchyFMessage.GeneratorPayload);
				return;
			}
			streamWriter.WriteInt32(playfieldAnarchyFMessage.Unknown5);
			streamWriter.WriteInt32(playfieldAnarchyFMessage.Unknown6);
			new PlayfieldVendorInfoSerializer().Serialize(streamWriter, serializationContext, playfieldAnarchyFMessage.PlayfieldVendorInfo, propertyMetaData);
			streamWriter.WriteInt32(playfieldAnarchyFMessage.PlayfieldX);
			streamWriter.WriteInt32(playfieldAnarchyFMessage.PlayfieldZ);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<PlayfieldAnarchyFMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((PlayfieldAnarchyFMessageSerializer o) => o.Serialize));
			NewExpression instance = Expression.New(GetType());
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}

		private static bool LooksLikeGeneratorPayload(StreamReader streamReader, int remaining)
		{
			if (remaining <= 16)
			{
				return false;
			}
			long position = streamReader.Position;
			int num = streamReader.ReadInt32();
			streamReader.Position = position;
			if (num != 51016 && num != 51005 && num != 51035)
			{
				return num == 51069;
			}
			return true;
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
	public class QuestFullUpdateMessageSerializer : ISerializer
	{
		public Type Type => typeof(QuestFullUpdateMessage);

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			QuestFullUpdateMessage questFullUpdateMessage = new QuestFullUpdateMessage
			{
				N3MessageType = (N3MessageType)streamReader.ReadInt32(),
				Identity = streamReader.ReadIdentity(),
				Unknown = streamReader.ReadByte()
			};
			int num = ReadX3F1Count(streamReader);
			questFullUpdateMessage.Quests = new Quest[num];
			for (int i = 0; i < num; i++)
			{
				questFullUpdateMessage.Quests[i] = ReadQuest(streamReader);
			}
			return questFullUpdateMessage;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<QuestFullUpdateMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((QuestFullUpdateMessageSerializer o) => o.Deserialize));
			MethodCallExpression expression = Expression.Call(Expression.New(GetType()), methodInfo, streamReaderExpression, serializationContextExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
			return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(expression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			QuestFullUpdateMessage questFullUpdateMessage = (QuestFullUpdateMessage)value;
			streamWriter.WriteInt32((int)questFullUpdateMessage.N3MessageType);
			streamWriter.WriteIdentity(questFullUpdateMessage.Identity);
			streamWriter.WriteByte(questFullUpdateMessage.Unknown);
			Quest[] array = questFullUpdateMessage.Quests ?? new Quest[0];
			WriteX3F1Count(streamWriter, array.Length);
			for (int i = 0; i < array.Length; i++)
			{
				WriteQuest(streamWriter, array[i]);
			}
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<QuestFullUpdateMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((QuestFullUpdateMessageSerializer o) => o.Serialize));
			return Expression.Call(Expression.New(GetType()), methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}

		private static Quest ReadQuest(StreamReader reader)
		{
			Quest quest = new Quest();
			quest.QuestId = reader.ReadIdentity();
			quest.Unknown1 = reader.ReadInt32();
			quest.Unknown2 = reader.ReadInt32();
			quest.Unknown3 = reader.ReadInt32();
			quest.Unknown4 = reader.ReadInt32();
			quest.ShortInfo = ReadNullTerminatedString(reader);
			quest.LongInfo = ReadLengthPrefixedString(reader);
			quest.UnknownId1 = reader.ReadIdentity();
			quest.Unknown5 = reader.ReadInt32();
			quest.Unknown6 = reader.ReadInt32();
			quest.Unknown7 = reader.ReadInt32();
			quest.Unknown8 = reader.ReadInt32();
			quest.Unknown9 = reader.ReadInt32();
			quest.Unknown10 = reader.ReadInt32();
			quest.MissionItemData = ReadX3F1Array(reader, ReadMissionItemReward);
			quest.Unknown11 = reader.ReadInt32();
			quest.Unknown12 = reader.ReadInt32();
			quest.Unknown13 = reader.ReadInt32();
			quest.UnknownHash1 = reader.ReadString(4);
			quest.Unknown14 = reader.ReadInt32();
			quest.Unknown15 = reader.ReadInt32();
			quest.Unknown16 = reader.ReadInt32();
			quest.Unknown17 = reader.ReadInt32();
			quest.Unknown18 = reader.ReadInt32();
			quest.UnknownId2 = reader.ReadIdentity();
			quest.MissionIconId = reader.ReadInt32();
			quest.Unknown20 = reader.ReadInt32();
			quest.Unknown21 = reader.ReadInt32();
			quest.QuestActions = ReadX3F1Array(reader, ReadQuestAction);
			quest.PlayerIds = ReadX3F1Array(reader, (StreamReader r) => r.ReadIdentity());
			quest.UnknownArray1 = ReadInt32Array(reader);
			quest.UnknownArray2 = ReadInt32Array(reader);
			quest.CharacterInfos = ReadInt32Array(reader, ReadCharacterInfo);
			quest.Unknown22 = reader.ReadInt32();
			quest.PlayerIds2 = ReadX3F1Array(reader, (StreamReader r) => r.ReadIdentity());
			quest.Unknown23 = reader.ReadInt32();
			quest.Unknown24 = reader.ReadInt32();
			quest.UnknownId3 = reader.ReadIdentity();
			quest.Unknown25 = reader.ReadInt32();
			quest.Unknown26 = reader.ReadInt32();
			quest.QuestIdentities = ReadInt32Array(reader, ReadQuestIdentity);
			quest.Unknown27 = reader.ReadInt32();
			quest.FactionInfos = ReadX3F1Array(reader, (StreamReader r) => r.ReadIdentity());
			quest.Unknown28 = reader.ReadByte();
			return quest;
		}

		private static void WriteQuest(StreamWriter writer, Quest quest)
		{
			if (quest == null)
			{
				throw new InvalidOperationException("QuestFullUpdate cannot serialize a null quest entry.");
			}
			writer.WriteIdentity(quest.QuestId);
			writer.WriteInt32(quest.Unknown1);
			writer.WriteInt32(quest.Unknown2);
			writer.WriteInt32(quest.Unknown3);
			writer.WriteInt32(quest.Unknown4);
			WriteNullTerminatedString(writer, quest.ShortInfo);
			WriteLengthPrefixedString(writer, quest.LongInfo);
			writer.WriteIdentity(quest.UnknownId1);
			writer.WriteInt32(quest.Unknown5);
			writer.WriteInt32(quest.Unknown6);
			writer.WriteInt32(quest.Unknown7);
			writer.WriteInt32(quest.Unknown8);
			writer.WriteInt32(quest.Unknown9);
			writer.WriteInt32(quest.Unknown10);
			WriteX3F1Array(writer, quest.MissionItemData, WriteMissionItemReward);
			writer.WriteInt32(quest.Unknown11);
			writer.WriteInt32(quest.Unknown12);
			writer.WriteInt32(quest.Unknown13);
			WriteFixedString(writer, quest.UnknownHash1, 4);
			writer.WriteInt32(quest.Unknown14);
			writer.WriteInt32(quest.Unknown15);
			writer.WriteInt32(quest.Unknown16);
			writer.WriteInt32(quest.Unknown17);
			writer.WriteInt32(quest.Unknown18);
			writer.WriteIdentity(quest.UnknownId2);
			writer.WriteInt32(quest.MissionIconId);
			writer.WriteInt32(quest.Unknown20);
			writer.WriteInt32(quest.Unknown21);
			WriteX3F1Array(writer, quest.QuestActions, WriteQuestAction);
			WriteX3F1Array(writer, quest.PlayerIds, delegate(StreamWriter w, Identity identity)
			{
				w.WriteIdentity(identity);
			});
			WriteInt32Array(writer, quest.UnknownArray1, delegate(StreamWriter w, int item)
			{
				w.WriteInt32(item);
			});
			WriteInt32Array(writer, quest.UnknownArray2, delegate(StreamWriter w, int item)
			{
				w.WriteInt32(item);
			});
			WriteInt32Array(writer, quest.CharacterInfos, WriteCharacterInfo);
			writer.WriteInt32(quest.Unknown22);
			WriteX3F1Array(writer, quest.PlayerIds2, delegate(StreamWriter w, Identity identity)
			{
				w.WriteIdentity(identity);
			});
			writer.WriteInt32(quest.Unknown23);
			writer.WriteInt32(quest.Unknown24);
			writer.WriteIdentity(quest.UnknownId3);
			writer.WriteInt32(quest.Unknown25);
			writer.WriteInt32(quest.Unknown26);
			WriteInt32Array(writer, quest.QuestIdentities, WriteQuestIdentity);
			writer.WriteInt32(quest.Unknown27);
			WriteX3F1Array(writer, quest.FactionInfos, delegate(StreamWriter w, Identity identity)
			{
				w.WriteIdentity(identity);
			});
			writer.WriteByte(quest.Unknown28);
		}

		private static MissionItemReward ReadMissionItemReward(StreamReader reader)
		{
			return new MissionItemReward
			{
				LowId = reader.ReadInt32(),
				HighId = reader.ReadInt32(),
				Ql = reader.ReadInt32(),
				Unknown = reader.ReadInt32()
			};
		}

		private static void WriteMissionItemReward(StreamWriter writer, MissionItemReward reward)
		{
			writer.WriteInt32(reward.LowId);
			writer.WriteInt32(reward.HighId);
			writer.WriteInt32(reward.Ql);
			writer.WriteInt32(reward.Unknown);
		}

		private static QuestActionInfo ReadQuestAction(StreamReader reader)
		{
			return new QuestActionInfo
			{
				Version = reader.ReadInt32(),
				Action = reader.ReadIdentity(),
				UnknownId1 = reader.ReadIdentity(),
				UnknownId2 = reader.ReadIdentity(),
				UnknownId3 = reader.ReadIdentity(),
				UnknownId4 = reader.ReadIdentity(),
				Unknown1 = reader.ReadSingle(),
				Unknown2 = reader.ReadSingle(),
				Unknown3 = reader.ReadSingle(),
				Unknown4 = reader.ReadSingle(),
				UnknownId5 = reader.ReadIdentity(),
				Unknown5 = reader.ReadSingle(),
				Unknown6 = reader.ReadSingle(),
				Unknown7 = reader.ReadSingle(),
				Unknown8 = reader.ReadSingle(),
				UnknownId6 = reader.ReadIdentity(),
				UnknownHash1 = reader.ReadString(4),
				Unknown9 = reader.ReadInt32(),
				UnknownId7 = reader.ReadIdentity(),
				PlayfieldId = reader.ReadIdentity(),
				Unknown10 = reader.ReadInt32(),
				Unknown11 = reader.ReadInt32(),
				Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())
			};
		}

		private static void WriteQuestAction(StreamWriter writer, QuestActionInfo action)
		{
			writer.WriteInt32(action.Version);
			writer.WriteIdentity(action.Action);
			writer.WriteIdentity(action.UnknownId1);
			writer.WriteIdentity(action.UnknownId2);
			writer.WriteIdentity(action.UnknownId3);
			writer.WriteIdentity(action.UnknownId4);
			writer.WriteSingle(action.Unknown1);
			writer.WriteSingle(action.Unknown2);
			writer.WriteSingle(action.Unknown3);
			writer.WriteSingle(action.Unknown4);
			writer.WriteIdentity(action.UnknownId5);
			writer.WriteSingle(action.Unknown5);
			writer.WriteSingle(action.Unknown6);
			writer.WriteSingle(action.Unknown7);
			writer.WriteSingle(action.Unknown8);
			writer.WriteIdentity(action.UnknownId6);
			WriteFixedString(writer, action.UnknownHash1, 4);
			writer.WriteInt32(action.Unknown9);
			writer.WriteIdentity(action.UnknownId7);
			writer.WriteIdentity(action.PlayfieldId);
			writer.WriteInt32(action.Unknown10);
			writer.WriteInt32(action.Unknown11);
			Vector3 vector = action.Position ?? new Vector3();
			writer.WriteSingle(vector.X);
			writer.WriteSingle(vector.Y);
			writer.WriteSingle(vector.Z);
		}

		private static CharacterInfo ReadCharacterInfo(StreamReader reader)
		{
			return new CharacterInfo
			{
				MissionIdentity = reader.ReadIdentity(),
				Name = ReadNullTerminatedString(reader)
			};
		}

		private static void WriteCharacterInfo(StreamWriter writer, CharacterInfo info)
		{
			writer.WriteIdentity(info.MissionIdentity);
			WriteNullTerminatedString(writer, info.Name);
		}

		private static QuestIdentity ReadQuestIdentity(StreamReader reader)
		{
			return new QuestIdentity
			{
				Unknown1 = reader.ReadIdentity(),
				Unknown2 = reader.ReadInt32()
			};
		}

		private static void WriteQuestIdentity(StreamWriter writer, QuestIdentity identity)
		{
			writer.WriteIdentity(identity.Unknown1);
			writer.WriteInt32(identity.Unknown2);
		}

		private static string ReadLengthPrefixedString(StreamReader reader)
		{
			return reader.ReadString(reader.ReadInt32());
		}

		private static void WriteLengthPrefixedString(StreamWriter writer, string value)
		{
			string text = value ?? string.Empty;
			writer.WriteInt32(Encoding.ASCII.GetByteCount(text) + 1);
			writer.WriteString(text);
			writer.WriteByte(0);
		}

		private static string ReadNullTerminatedString(StreamReader reader)
		{
			List<byte> list = new List<byte>();
			byte item;
			while ((item = reader.ReadByte()) != 0)
			{
				list.Add(item);
			}
			return Encoding.ASCII.GetString(list.ToArray());
		}

		private static void WriteNullTerminatedString(StreamWriter writer, string value)
		{
			writer.WriteString(value ?? string.Empty);
			writer.WriteByte(0);
		}

		private static void WriteFixedString(StreamWriter writer, string value, int length)
		{
			writer.WriteString(value ?? string.Empty, length);
		}

		private static int ReadX3F1Count(StreamReader reader)
		{
			return Math.Max(reader.ReadInt32() / 1009 - 1, 0);
		}

		private static void WriteX3F1Count(StreamWriter writer, int count)
		{
			writer.WriteInt32((count + 1) * 1009);
		}

		private static T[] ReadX3F1Array<T>(StreamReader reader, Func<StreamReader, T> readItem)
		{
			int num = ReadX3F1Count(reader);
			T[] array = new T[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = readItem(reader);
			}
			return array;
		}

		private static void WriteX3F1Array<T>(StreamWriter writer, T[] values, Action<StreamWriter, T> writeItem)
		{
			T[] array = values ?? new T[0];
			WriteX3F1Count(writer, array.Length);
			for (int i = 0; i < array.Length; i++)
			{
				writeItem(writer, array[i]);
			}
		}

		private static int[] ReadInt32Array(StreamReader reader)
		{
			return ReadInt32Array(reader, (StreamReader r) => r.ReadInt32());
		}

		private static T[] ReadInt32Array<T>(StreamReader reader, Func<StreamReader, T> readItem)
		{
			int num = reader.ReadInt32();
			T[] array = new T[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = readItem(reader);
			}
			return array;
		}

		private static void WriteInt32Array<T>(StreamWriter writer, T[] values, Action<StreamWriter, T> writeItem)
		{
			T[] array = values ?? new T[0];
			writer.WriteInt32(array.Length);
			for (int i = 0; i < array.Length; i++)
			{
				writeItem(writer, array[i]);
			}
		}
	}
	public class KeyOnlyN3MessageSerializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public KeyOnlyN3MessageSerializer(Type type)
		{
			this.type = type;
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			N3Message n3Message = (N3Message)Activator.CreateInstance(type);
			n3Message.N3MessageType = (N3MessageType)streamReader.ReadInt32();
			return n3Message;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<KeyOnlyN3MessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((KeyOnlyN3MessageSerializer o) => o.Deserialize));
			ConstructorInfo constructor = GetType().GetConstructor(new Type[1] { typeof(Type) });
			NewExpression instance = Expression.New(constructor, Expression.Constant(type, typeof(Type)));
			MethodCallExpression expression = Expression.Call(instance, methodInfo, streamReaderExpression, serializationContextExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
			return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(expression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			N3Message n3Message = (N3Message)value;
			streamWriter.WriteInt32((int)n3Message.N3MessageType);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<KeyOnlyN3MessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((KeyOnlyN3MessageSerializer o) => o.Serialize));
			ConstructorInfo constructor = GetType().GetConstructor(new Type[1] { typeof(Type) });
			NewExpression instance = Expression.New(constructor, Expression.Constant(type, typeof(Type)));
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	public class DropDynelMessageSerializer : ISerializer
	{
		public Type Type => typeof(DropDynelMessage);

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return new DropDynelMessage
			{
				N3MessageType = (N3MessageType)streamReader.ReadInt32(),
				Identity = streamReader.ReadIdentity(),
				Position = new Vector3
				{
					X = streamReader.ReadSingle(),
					Y = streamReader.ReadSingle(),
					Z = streamReader.ReadSingle()
				}
			};
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<DropDynelMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((DropDynelMessageSerializer o) => o.Deserialize));
			MethodCallExpression expression = Expression.Call(Expression.New(GetType()), methodInfo, streamReaderExpression, serializationContextExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
			return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(expression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			DropDynelMessage dropDynelMessage = (DropDynelMessage)value;
			streamWriter.WriteInt32((int)dropDynelMessage.N3MessageType);
			streamWriter.WriteIdentity(dropDynelMessage.Identity);
			streamWriter.WriteSingle(dropDynelMessage.Position.X);
			streamWriter.WriteSingle(dropDynelMessage.Position.Y);
			streamWriter.WriteSingle(dropDynelMessage.Position.Z);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<DropDynelMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((DropDynelMessageSerializer o) => o.Serialize));
			return Expression.Call(Expression.New(GetType()), methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	public class RelocateDynelsMessageSerializer : ISerializer
	{
		public Type Type => typeof(RelocateDynelsMessage);

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			RelocateDynelsMessage relocateDynelsMessage = new RelocateDynelsMessage();
			relocateDynelsMessage.N3MessageType = (N3MessageType)streamReader.ReadInt32();
			relocateDynelsMessage.Identity = streamReader.ReadIdentity();
			int num = streamReader.ReadInt32();
			int val = num / 1009 - 1;
			relocateDynelsMessage.RelocatedIdentities = new Identity[Math.Max(val, 0)];
			for (int i = 0; i < relocateDynelsMessage.RelocatedIdentities.Length; i++)
			{
				relocateDynelsMessage.RelocatedIdentities[i] = streamReader.ReadIdentity();
			}
			return relocateDynelsMessage;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<RelocateDynelsMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((RelocateDynelsMessageSerializer o) => o.Deserialize));
			MethodCallExpression expression = Expression.Call(Expression.New(GetType()), methodInfo, streamReaderExpression, serializationContextExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
			return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(expression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			RelocateDynelsMessage relocateDynelsMessage = (RelocateDynelsMessage)value;
			Identity[] array = relocateDynelsMessage.RelocatedIdentities ?? new Identity[0];
			streamWriter.WriteInt32((int)relocateDynelsMessage.N3MessageType);
			streamWriter.WriteIdentity(relocateDynelsMessage.Identity);
			streamWriter.WriteInt32((array.Length + 1) * 1009);
			for (int i = 0; i < array.Length; i++)
			{
				streamWriter.WriteIdentity(array[i]);
			}
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<RelocateDynelsMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((RelocateDynelsMessageSerializer o) => o.Serialize));
			return Expression.Call(Expression.New(GetType()), methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	public class LocalityUpdateMessageSerializer : ISerializer
	{
		public Type Type => typeof(LocalityUpdateMessage);

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return new LocalityUpdateMessage
			{
				N3MessageType = (N3MessageType)streamReader.ReadInt32(),
				Position = new Vector3
				{
					X = streamReader.ReadSingle(),
					Y = streamReader.ReadSingle(),
					Z = streamReader.ReadSingle()
				},
				LocalityFlag = streamReader.ReadByte()
			};
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<LocalityUpdateMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((LocalityUpdateMessageSerializer o) => o.Deserialize));
			MethodCallExpression expression = Expression.Call(Expression.New(GetType()), methodInfo, streamReaderExpression, serializationContextExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
			return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(expression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			LocalityUpdateMessage localityUpdateMessage = (LocalityUpdateMessage)value;
			streamWriter.WriteInt32((int)localityUpdateMessage.N3MessageType);
			streamWriter.WriteSingle(localityUpdateMessage.Position.X);
			streamWriter.WriteSingle(localityUpdateMessage.Position.Y);
			streamWriter.WriteSingle(localityUpdateMessage.Position.Z);
			streamWriter.WriteByte(localityUpdateMessage.LocalityFlag);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<LocalityUpdateMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((LocalityUpdateMessageSerializer o) => o.Serialize));
			return Expression.Call(Expression.New(GetType()), methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	public class ClientContainerAddItemMessageSerializer : ISerializer
	{
		public Type Type => typeof(ClientContainerAddItemMessage);

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			long position = streamReader.Position;
			int num = (int)(streamReader.Length - position);
			ClientContainerAddItemMessage result = new ClientContainerAddItemMessage
			{
				N3MessageType = (N3MessageType)streamReader.ReadInt32(),
				Identity = streamReader.ReadIdentity(),
				Unknown = streamReader.ReadByte(),
				Target = streamReader.ReadIdentity(),
				Source = streamReader.ReadIdentity()
			};
			streamReader.Position = position + num;
			return result;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<ClientContainerAddItemMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((ClientContainerAddItemMessageSerializer o) => o.Deserialize));
			MethodCallExpression expression = Expression.Call(Expression.New(GetType()), methodInfo, streamReaderExpression, serializationContextExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
			return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(expression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			ClientContainerAddItemMessage clientContainerAddItemMessage = (ClientContainerAddItemMessage)value;
			streamWriter.WriteInt32((int)clientContainerAddItemMessage.N3MessageType);
			streamWriter.WriteIdentity(clientContainerAddItemMessage.Identity);
			streamWriter.WriteByte(clientContainerAddItemMessage.Unknown);
			streamWriter.WriteIdentity(clientContainerAddItemMessage.Target);
			streamWriter.WriteIdentity(clientContainerAddItemMessage.Source);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<ClientContainerAddItemMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((ClientContainerAddItemMessageSerializer o) => o.Serialize));
			return Expression.Call(Expression.New(GetType()), methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	public class ClientGetItemMessageSerializer : ISerializer
	{
		public Type Type => typeof(ClientGetItemMessage);

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			return new ClientGetItemMessage
			{
				N3MessageType = (N3MessageType)streamReader.ReadInt32(),
				Identity1 = streamReader.ReadIdentity()
			};
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<ClientGetItemMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((ClientGetItemMessageSerializer o) => o.Deserialize));
			MethodCallExpression expression = Expression.Call(Expression.New(GetType()), methodInfo, streamReaderExpression, serializationContextExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
			return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(expression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			ClientGetItemMessage clientGetItemMessage = (ClientGetItemMessage)value;
			streamWriter.WriteInt32((int)clientGetItemMessage.N3MessageType);
			streamWriter.WriteIdentity(clientGetItemMessage.Identity1);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<ClientGetItemMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((ClientGetItemMessageSerializer o) => o.Serialize));
			return Expression.Call(Expression.New(GetType()), methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	public class ResurrectMessageSerializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public ResurrectMessageSerializer()
		{
			type = typeof(ResurrectMessage);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			ResurrectMessage resurrectMessage = new ResurrectMessage();
			resurrectMessage.N3MessageType = (N3MessageType)streamReader.ReadInt32();
			resurrectMessage.Unknown1 = streamReader.ReadInt32();
			resurrectMessage.Unknown2 = streamReader.ReadInt32();
			return resurrectMessage;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<ResurrectMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((ResurrectMessageSerializer o) => o.Deserialize));
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
			ResurrectMessage resurrectMessage = (ResurrectMessage)value;
			streamWriter.WriteInt32((int)resurrectMessage.N3MessageType);
			streamWriter.WriteInt32(resurrectMessage.Unknown1);
			streamWriter.WriteInt32(resurrectMessage.Unknown2);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<ResurrectMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((ResurrectMessageSerializer o) => o.Serialize));
			NewExpression instance = Expression.New(GetType());
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}
	}
	public class SimpleCharFullUpdateSerializer : ISerializer
	{
		private sealed class ScfuTail
		{
			public byte[] ExtendedTextureOverrideData { get; set; }

			public ActiveNano[] ActiveNanos { get; set; }

			public Vector3[] Waypoints { get; set; }

			public Texture[] Textures { get; set; }

			public Mesh[] Meshes { get; set; }

			public int Flags2 { get; set; }

			public byte Unknown2 { get; set; }

			public byte Unknown4 { get; set; }
		}

		private readonly Type type;

		public Type Type => type;

		public SimpleCharFullUpdateSerializer()
		{
			type = typeof(SimpleCharFullUpdateMessage);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			long position = streamReader.Position;
			byte[] rawBody = streamReader.ReadBytes((int)(streamReader.Length - position));
			streamReader.Position = position;
			SimpleCharFullUpdateMessage simpleCharFullUpdateMessage = new SimpleCharFullUpdateMessage
			{
				RawBody = rawBody,
				N3MessageType = (N3MessageType)streamReader.ReadInt32(),
				Identity = streamReader.ReadIdentity(),
				Unknown = streamReader.ReadByte(),
				Version = streamReader.ReadByte(),
				Flags = (SimpleCharFullUpdateFlags)streamReader.ReadInt32()
			};
			SimpleCharFullUpdateFlags flags = simpleCharFullUpdateMessage.Flags;
			if (flags.HasFlag(SimpleCharFullUpdateFlags.HasPlayfieldId))
			{
				simpleCharFullUpdateMessage.PlayfieldId = streamReader.ReadInt32();
			}
			if (flags.HasFlag(SimpleCharFullUpdateFlags.HasFightingTarget))
			{
				simpleCharFullUpdateMessage.FightingTarget = streamReader.ReadIdentity();
			}
			simpleCharFullUpdateMessage.Coordinates = ReadVector3(streamReader);
			if (flags.HasFlag(SimpleCharFullUpdateFlags.HasHeading))
			{
				simpleCharFullUpdateMessage.Heading = new Quaternion
				{
					X = streamReader.ReadSingle(),
					Y = streamReader.ReadSingle(),
					Z = streamReader.ReadSingle(),
					W = streamReader.ReadSingle()
				};
			}
			simpleCharFullUpdateMessage.Appearance = new Appearance
			{
				Value = streamReader.ReadUInt32()
			};
			simpleCharFullUpdateMessage.Name = streamReader.ReadString(streamReader.ReadByte());
			simpleCharFullUpdateMessage.CharacterFlags = (CharacterFlags)streamReader.ReadInt32();
			simpleCharFullUpdateMessage.AccountFlags = streamReader.ReadInt16();
			simpleCharFullUpdateMessage.Expansions = streamReader.ReadInt16();
			if (flags.HasFlag(SimpleCharFullUpdateFlags.IsNpc))
			{
				SimpleNpcInfo simpleNpcInfo = new SimpleNpcInfo
				{
					Family = (flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallNpcFamily) ? streamReader.ReadByte() : streamReader.ReadInt16()),
					LosHeight = (flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallNpcLosHeight) ? streamReader.ReadByte() : streamReader.ReadInt16()),
					UnknownData = (flags.HasFlag(SimpleCharFullUpdateFlags.UnknownDataFlag) ? streamReader.ReadByte() : streamReader.ReadInt16()),
					UnknownData2 = streamReader.ReadInt16()
				};
				if (simpleNpcInfo.UnknownData2 > 0)
				{
					simpleNpcInfo.UnknownData3 = streamReader.ReadByte();
				}
				simpleCharFullUpdateMessage.CharacterInfo = simpleNpcInfo;
			}
			else
			{
				SimplePcInfo simplePcInfo = new SimplePcInfo
				{
					CurrentNano = streamReader.ReadUInt32(),
					Team = streamReader.ReadInt32(),
					Swim = streamReader.ReadInt16(),
					StrengthBase = streamReader.ReadInt16(),
					AgilityBase = streamReader.ReadInt16(),
					StaminaBase = streamReader.ReadInt16(),
					IntelligenceBase = streamReader.ReadInt16(),
					SenseBase = streamReader.ReadInt16(),
					PsychicBase = streamReader.ReadInt16(),
					FirstName = string.Empty,
					LastName = string.Empty,
					OrgName = string.Empty
				};
				if (simpleCharFullUpdateMessage.CharacterFlags.HasFlag(CharacterFlags.HasVisibleName))
				{
					simplePcInfo.FirstName = streamReader.ReadString(streamReader.ReadInt16());
					simplePcInfo.LastName = streamReader.ReadString(streamReader.ReadInt16());
				}
				if (flags.HasFlag(SimpleCharFullUpdateFlags.HasOrgName))
				{
					simplePcInfo.OrgName = streamReader.ReadString(streamReader.ReadInt16());
				}
				simpleCharFullUpdateMessage.CharacterInfo = simplePcInfo;
			}
			simpleCharFullUpdateMessage.Level = (flags.HasFlag(SimpleCharFullUpdateFlags.HasExtendedLevel) ? streamReader.ReadInt16() : streamReader.ReadByte());
			simpleCharFullUpdateMessage.Health = (flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallHealth) ? streamReader.ReadInt16() : streamReader.ReadInt32());
			if (flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallHealthDamage))
			{
				simpleCharFullUpdateMessage.HealthDamage = streamReader.ReadByte();
			}
			else if (flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallHealth))
			{
				simpleCharFullUpdateMessage.HealthDamage = streamReader.ReadInt16();
			}
			else
			{
				simpleCharFullUpdateMessage.HealthDamage = streamReader.ReadInt32();
			}
			simpleCharFullUpdateMessage.MonsterData = streamReader.ReadUInt32();
			simpleCharFullUpdateMessage.MonsterScale = streamReader.ReadInt16();
			simpleCharFullUpdateMessage.VisualFlags = streamReader.ReadInt16();
			simpleCharFullUpdateMessage.VisibleTitle = streamReader.ReadByte();
			int num = streamReader.ReadInt32();
			if (num < 0 || num > streamReader.Length - streamReader.Position)
			{
				throw new InvalidDataException("Invalid SimpleCharFullUpdate Unknown1 length.");
			}
			simpleCharFullUpdateMessage.Unknown1 = streamReader.ReadBytes(num);
			if (flags.HasFlag(SimpleCharFullUpdateFlags.HasHeadMesh))
			{
				simpleCharFullUpdateMessage.HeadMesh = streamReader.ReadUInt32();
			}
			simpleCharFullUpdateMessage.RunSpeedBase = (flags.HasFlag(SimpleCharFullUpdateFlags.HasExtendedRunSpeed) ? streamReader.ReadInt16() : streamReader.ReadByte());
			if (flags.HasFlag(SimpleCharFullUpdateFlags.IsUnderAttack))
			{
				simpleCharFullUpdateMessage.FightingTarget = streamReader.ReadIdentity();
			}
			byte[] array = streamReader.ReadBytes((int)(streamReader.Length - streamReader.Position));
			if (TryDecodeTail(array, flags, simpleCharFullUpdateMessage.Identity, out var result))
			{
				simpleCharFullUpdateMessage.ExtendedTextureOverrideData = result.ExtendedTextureOverrideData;
				simpleCharFullUpdateMessage.ActiveNanos = result.ActiveNanos;
				simpleCharFullUpdateMessage.Waypoints = result.Waypoints;
				simpleCharFullUpdateMessage.Textures = result.Textures;
				simpleCharFullUpdateMessage.Meshes = result.Meshes;
				simpleCharFullUpdateMessage.Flags2 = result.Flags2;
				simpleCharFullUpdateMessage.Unknown2 = result.Unknown2;
				simpleCharFullUpdateMessage.Unknown4 = result.Unknown4;
				simpleCharFullUpdateMessage.TailFullyDecoded = true;
				simpleCharFullUpdateMessage.UndecodedTail = new byte[0];
			}
			else
			{
				simpleCharFullUpdateMessage.ExtendedTextureOverrideData = new byte[0];
				simpleCharFullUpdateMessage.ActiveNanos = new ActiveNano[0];
				simpleCharFullUpdateMessage.Waypoints = new Vector3[0];
				simpleCharFullUpdateMessage.Textures = new Texture[0];
				simpleCharFullUpdateMessage.Meshes = new Mesh[0];
				simpleCharFullUpdateMessage.TailFullyDecoded = false;
				simpleCharFullUpdateMessage.UndecodedTail = array;
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
			SimpleCharFullUpdateMessage simpleCharFullUpdateMessage = (SimpleCharFullUpdateMessage)value;
			streamWriter.WriteInt32((int)simpleCharFullUpdateMessage.N3MessageType);
			streamWriter.WriteInt32((int)simpleCharFullUpdateMessage.Identity.Type);
			streamWriter.WriteInt32(simpleCharFullUpdateMessage.Identity.Instance);
			streamWriter.WriteByte(simpleCharFullUpdateMessage.Unknown);
			streamWriter.WriteByte(simpleCharFullUpdateMessage.Version);
			streamWriter.WriteInt32((int)simpleCharFullUpdateMessage.Flags);
			SimpleCharFullUpdateFlags simpleCharFullUpdateFlags = SimpleCharFullUpdateFlags.None;
			if (simpleCharFullUpdateMessage.PlayfieldId.HasValue)
			{
				simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.HasPlayfieldId;
				streamWriter.WriteInt32(simpleCharFullUpdateMessage.PlayfieldId.Value);
			}
			streamWriter.WriteSingle(simpleCharFullUpdateMessage.Coordinates.X);
			streamWriter.WriteSingle(simpleCharFullUpdateMessage.Coordinates.Y);
			streamWriter.WriteSingle(simpleCharFullUpdateMessage.Coordinates.Z);
			if (simpleCharFullUpdateMessage.Heading != null)
			{
				simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.HasHeading;
				streamWriter.WriteSingle(simpleCharFullUpdateMessage.Heading.X);
				streamWriter.WriteSingle(simpleCharFullUpdateMessage.Heading.Y);
				streamWriter.WriteSingle(simpleCharFullUpdateMessage.Heading.Z);
				streamWriter.WriteSingle(simpleCharFullUpdateMessage.Heading.W);
			}
			streamWriter.WriteUInt32(simpleCharFullUpdateMessage.Appearance.Value);
			streamWriter.WriteByte((byte)(simpleCharFullUpdateMessage.Name.Length + 1));
			streamWriter.WriteString(simpleCharFullUpdateMessage.Name, simpleCharFullUpdateMessage.Name.Length + 1);
			streamWriter.WriteInt32((int)simpleCharFullUpdateMessage.CharacterFlags);
			streamWriter.WriteInt16(simpleCharFullUpdateMessage.AccountFlags);
			streamWriter.WriteInt16(simpleCharFullUpdateMessage.Expansions);
			if (simpleCharFullUpdateMessage.CharacterInfo is SimpleNpcInfo simpleNpcInfo)
			{
				simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.IsNpc;
				if (simpleNpcInfo.Family > 255)
				{
					streamWriter.WriteInt16(simpleNpcInfo.Family);
				}
				else
				{
					simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.HasSmallNpcFamily;
					streamWriter.WriteByte((byte)simpleNpcInfo.Family);
				}
				if (simpleNpcInfo.LosHeight > 255)
				{
					streamWriter.WriteInt16(simpleNpcInfo.LosHeight);
				}
				else
				{
					simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.HasSmallNpcLosHeight;
					streamWriter.WriteByte((byte)simpleNpcInfo.LosHeight);
				}
				simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.UnknownDataFlag;
				streamWriter.WriteByte((byte)simpleNpcInfo.UnknownData);
				streamWriter.WriteInt16(simpleNpcInfo.UnknownData2);
				if (simpleNpcInfo.UnknownData2 > 0)
				{
					streamWriter.WriteByte(simpleNpcInfo.UnknownData3);
				}
				simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.UnknownFlag;
				simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.UnknownFlag2;
			}
			if (simpleCharFullUpdateMessage.CharacterInfo is SimplePcInfo simplePcInfo)
			{
				streamWriter.WriteUInt32(simplePcInfo.CurrentNano);
				streamWriter.WriteInt32(simplePcInfo.Team);
				streamWriter.WriteInt16(simplePcInfo.Swim);
				streamWriter.WriteInt16(simplePcInfo.StrengthBase);
				streamWriter.WriteInt16(simplePcInfo.AgilityBase);
				streamWriter.WriteInt16(simplePcInfo.StaminaBase);
				streamWriter.WriteInt16(simplePcInfo.IntelligenceBase);
				streamWriter.WriteInt16(simplePcInfo.SenseBase);
				streamWriter.WriteInt16(simplePcInfo.PsychicBase);
				if (simpleCharFullUpdateMessage.CharacterFlags.HasFlag(CharacterFlags.HasVisibleName))
				{
					streamWriter.WriteInt16((short)simplePcInfo.FirstName.Length);
					streamWriter.WriteString(simplePcInfo.FirstName);
					streamWriter.WriteInt16((short)simplePcInfo.LastName.Length);
					streamWriter.WriteString(simplePcInfo.LastName);
				}
				if (!string.IsNullOrWhiteSpace(simplePcInfo.OrgName))
				{
					simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.HasOrgName;
					streamWriter.WriteInt16((short)simplePcInfo.OrgName.Length);
					streamWriter.WriteString(simplePcInfo.OrgName);
				}
			}
			if (simpleCharFullUpdateMessage.Level > 127)
			{
				simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.HasExtendedLevel;
				streamWriter.WriteInt16(simpleCharFullUpdateMessage.Level);
			}
			else
			{
				streamWriter.WriteByte((byte)simpleCharFullUpdateMessage.Level);
			}
			if (simpleCharFullUpdateMessage.Health <= 32767)
			{
				simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.HasSmallHealth;
				streamWriter.WriteInt16((short)simpleCharFullUpdateMessage.Health);
			}
			else
			{
				streamWriter.WriteInt32(simpleCharFullUpdateMessage.Health);
			}
			if (simpleCharFullUpdateMessage.HealthDamage <= 255)
			{
				simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.HasSmallHealthDamage;
				streamWriter.WriteByte((byte)simpleCharFullUpdateMessage.HealthDamage);
			}
			else if (simpleCharFullUpdateFlags.HasFlag(SimpleCharFullUpdateFlags.HasSmallHealth))
			{
				streamWriter.WriteInt16((short)simpleCharFullUpdateMessage.HealthDamage);
			}
			else
			{
				streamWriter.WriteInt32(simpleCharFullUpdateMessage.HealthDamage);
			}
			streamWriter.WriteUInt32(simpleCharFullUpdateMessage.MonsterData);
			streamWriter.WriteInt16(simpleCharFullUpdateMessage.MonsterScale);
			streamWriter.WriteInt16(simpleCharFullUpdateMessage.VisualFlags);
			streamWriter.WriteByte(simpleCharFullUpdateMessage.VisibleTitle);
			streamWriter.WriteInt32(simpleCharFullUpdateMessage.Unknown1.Length);
			streamWriter.WriteBytes(simpleCharFullUpdateMessage.Unknown1);
			if (simpleCharFullUpdateMessage.HeadMesh.HasValue)
			{
				simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.HasHeadMesh;
				streamWriter.WriteUInt32(simpleCharFullUpdateMessage.HeadMesh.Value);
			}
			if (simpleCharFullUpdateMessage.RunSpeedBase > 255)
			{
				simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.HasExtendedRunSpeed;
				streamWriter.WriteInt16(simpleCharFullUpdateMessage.RunSpeedBase);
			}
			else
			{
				streamWriter.WriteByte((byte)simpleCharFullUpdateMessage.RunSpeedBase);
			}
			if (simpleCharFullUpdateMessage.FightingTarget.HasValue)
			{
				simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.IsUnderAttack;
				Identity value2 = simpleCharFullUpdateMessage.FightingTarget.Value;
				streamWriter.WriteInt32((int)value2.Type);
				streamWriter.WriteInt32(value2.Instance);
			}
			if (simpleCharFullUpdateMessage.ExtendedTextureOverrideData != null && simpleCharFullUpdateMessage.ExtendedTextureOverrideData.Length != 0)
			{
				simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.HasExtendedTextures;
				streamWriter.WriteBytes(simpleCharFullUpdateMessage.ExtendedTextureOverrideData);
			}
			streamWriter.WriteInt32((simpleCharFullUpdateMessage.ActiveNanos.Length + 1) * 1009);
			ActiveNano[] activeNanos = simpleCharFullUpdateMessage.ActiveNanos;
			foreach (ActiveNano activeNano in activeNanos)
			{
				streamWriter.WriteInt32(activeNano.NanoId);
				streamWriter.WriteInt32(activeNano.NanoInstance);
				streamWriter.WriteInt32(activeNano.Time1);
				streamWriter.WriteInt32(activeNano.Time2);
			}
			if (simpleCharFullUpdateMessage.Waypoints != null && simpleCharFullUpdateMessage.Waypoints.Length != 0)
			{
				simpleCharFullUpdateFlags |= SimpleCharFullUpdateFlags.HasWaypoints;
				streamWriter.WriteInt32((int)simpleCharFullUpdateMessage.Identity.Type);
				streamWriter.WriteInt32(simpleCharFullUpdateMessage.Identity.Instance);
				streamWriter.WriteInt32(simpleCharFullUpdateMessage.Waypoints.Length);
				Vector3[] waypoints = simpleCharFullUpdateMessage.Waypoints;
				foreach (Vector3 vector in waypoints)
				{
					streamWriter.WriteSingle(vector.X);
					streamWriter.WriteSingle(vector.Y);
					streamWriter.WriteSingle(vector.Z);
				}
			}
			streamWriter.WriteInt32((simpleCharFullUpdateMessage.Textures.Length + 1) * 1009);
			Texture[] textures = simpleCharFullUpdateMessage.Textures;
			foreach (Texture texture in textures)
			{
				streamWriter.WriteInt32(texture.Place);
				streamWriter.WriteInt32(texture.Id);
				streamWriter.WriteInt32(texture.Unknown);
			}
			streamWriter.WriteInt32((simpleCharFullUpdateMessage.Meshes.Length + 1) * 1009);
			Mesh[] meshes = simpleCharFullUpdateMessage.Meshes;
			foreach (Mesh mesh in meshes)
			{
				streamWriter.WriteByte(mesh.Position);
				streamWriter.WriteUInt32(mesh.Id);
				streamWriter.WriteInt32(mesh.OverrideTextureId);
				streamWriter.WriteByte(mesh.Layer);
			}
			streamWriter.WriteInt32(simpleCharFullUpdateMessage.Flags2);
			streamWriter.WriteByte(simpleCharFullUpdateMessage.Unknown2);
			if (((uint)simpleCharFullUpdateMessage.Flags2 & 2u) != 0)
			{
				streamWriter.WriteByte(simpleCharFullUpdateMessage.Unknown4);
			}
			simpleCharFullUpdateFlags |= simpleCharFullUpdateMessage.AdditionalFlags;
			simpleCharFullUpdateFlags &= ~simpleCharFullUpdateMessage.SuppressedFlags;
			simpleCharFullUpdateFlags = ((simpleCharFullUpdateMessage.RunSpeedBase <= 255) ? (simpleCharFullUpdateFlags & ~SimpleCharFullUpdateFlags.HasExtendedRunSpeed) : (simpleCharFullUpdateFlags | SimpleCharFullUpdateFlags.HasExtendedRunSpeed));
			simpleCharFullUpdateFlags = ((!simpleCharFullUpdateMessage.FightingTarget.HasValue) ? (simpleCharFullUpdateFlags & ~SimpleCharFullUpdateFlags.IsUnderAttack) : (simpleCharFullUpdateFlags | SimpleCharFullUpdateFlags.IsUnderAttack));
			simpleCharFullUpdateFlags = ((simpleCharFullUpdateMessage.Waypoints == null || simpleCharFullUpdateMessage.Waypoints.Length == 0) ? (simpleCharFullUpdateFlags & ~SimpleCharFullUpdateFlags.HasWaypoints) : (simpleCharFullUpdateFlags | SimpleCharFullUpdateFlags.HasWaypoints));
			long position = streamWriter.Position;
			streamWriter.Position = 30L;
			streamWriter.WriteInt32((int)simpleCharFullUpdateFlags);
			streamWriter.Position = position;
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<SimpleCharFullUpdateSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((SimpleCharFullUpdateSerializer o) => o.Serialize));
			NewExpression instance = Expression.New(GetType());
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}

		private static Vector3 ReadVector3(StreamReader streamReader)
		{
			return new Vector3
			{
				X = streamReader.ReadSingle(),
				Y = streamReader.ReadSingle(),
				Z = streamReader.ReadSingle()
			};
		}

		private static bool TryDecodeTail(byte[] bytes, SimpleCharFullUpdateFlags flags, Identity identity, out ScfuTail result)
		{
			result = null;
			int num = (flags.HasFlag(SimpleCharFullUpdateFlags.HasExtendedTextures) ? 1 : 0);
			int num2 = (flags.HasFlag(SimpleCharFullUpdateFlags.HasExtendedTextures) ? Math.Max(1, bytes.Length - 17) : 0);
			for (int i = num; i <= num2; i++)
			{
				if (TryDecodeTailAt(bytes, i, flags, identity, out var result2))
				{
					result2.ExtendedTextureOverrideData = new byte[i];
					Buffer.BlockCopy(bytes, 0, result2.ExtendedTextureOverrideData, 0, i);
					result = result2;
					return true;
				}
			}
			return false;
		}

		private static bool TryDecodeTailAt(byte[] bytes, int offset, SimpleCharFullUpdateFlags flags, Identity identity, out ScfuTail result)
		{
			result = null;
			try
			{
				using MemoryStream stream = new MemoryStream(bytes, writable: false);
				using StreamReader streamReader = new StreamReader(stream);
				streamReader.Position = offset;
				if (!TryReadX3F1Count(streamReader, out var count))
				{
					return false;
				}
				List<ActiveNano> list = new List<ActiveNano>(count);
				for (int i = 0; i < count; i++)
				{
					list.Add(new ActiveNano
					{
						NanoId = streamReader.ReadInt32(),
						NanoInstance = streamReader.ReadInt32(),
						Time1 = streamReader.ReadInt32(),
						Time2 = streamReader.ReadInt32()
					});
				}
				List<Vector3> list2 = new List<Vector3>();
				if (flags.HasFlag(SimpleCharFullUpdateFlags.HasWaypoints))
				{
					Identity identity2 = streamReader.ReadIdentity();
					if (identity2.Type != identity.Type || identity2.Instance != identity.Instance)
					{
						return false;
					}
					int num = streamReader.ReadInt32();
					if (num < 0 || num > 4096)
					{
						return false;
					}
					for (int j = 0; j < num; j++)
					{
						list2.Add(ReadVector3(streamReader));
					}
				}
				if (!TryReadX3F1Count(streamReader, out count))
				{
					return false;
				}
				List<Texture> list3 = new List<Texture>(count);
				for (int k = 0; k < count; k++)
				{
					list3.Add(new Texture
					{
						Place = streamReader.ReadInt32(),
						Id = streamReader.ReadInt32(),
						Unknown = streamReader.ReadInt32()
					});
				}
				if (!TryReadX3F1Count(streamReader, out count))
				{
					return false;
				}
				List<Mesh> list4 = new List<Mesh>(count);
				for (int l = 0; l < count; l++)
				{
					list4.Add(new Mesh
					{
						Position = streamReader.ReadByte(),
						Id = streamReader.ReadUInt32(),
						OverrideTextureId = streamReader.ReadInt32(),
						Layer = streamReader.ReadByte()
					});
				}
				if (streamReader.Length - streamReader.Position < 5)
				{
					return false;
				}
				int num2 = streamReader.ReadInt32();
				int num3 = (((num2 & 2) == 0) ? 1 : 2);
				if (streamReader.Length - streamReader.Position != num3)
				{
					return false;
				}
				byte unknown = streamReader.ReadByte();
				byte unknown2 = (byte)((((uint)num2 & 2u) != 0) ? streamReader.ReadByte() : 0);
				result = new ScfuTail
				{
					ActiveNanos = list.ToArray(),
					Waypoints = list2.ToArray(),
					Textures = list3.ToArray(),
					Meshes = list4.ToArray(),
					Flags2 = num2,
					Unknown2 = unknown,
					Unknown4 = unknown2
				};
				return streamReader.Position == streamReader.Length;
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static bool TryReadX3F1Count(StreamReader reader, out int count)
		{
			count = 0;
			if (reader.Length - reader.Position < 4)
			{
				return false;
			}
			int num = reader.ReadInt32();
			if (num < 1009 || num % 1009 != 0)
			{
				return false;
			}
			count = num / 1009 - 1;
			if (count >= 0)
			{
				return count <= 4096;
			}
			return false;
		}
	}
	internal class VendingMachineFullUpdateMessageSerializer : ISerializer
	{
		private readonly Type type;

		public Type Type => type;

		public VendingMachineFullUpdateMessageSerializer()
		{
			type = typeof(VendingMachineFullUpdateMessage);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			VendingMachineFullUpdateMessage vendingMachineFullUpdateMessage = new VendingMachineFullUpdateMessage();
			vendingMachineFullUpdateMessage.N3MessageType = (N3MessageType)streamReader.ReadInt32();
			vendingMachineFullUpdateMessage.Identity = streamReader.ReadIdentity();
			vendingMachineFullUpdateMessage.Unknown = streamReader.ReadByte();
			vendingMachineFullUpdateMessage.TypeIdentifier = streamReader.ReadInt32();
			IdentityType identityType = (IdentityType)streamReader.ReadInt32();
			int instance = streamReader.ReadInt32();
			vendingMachineFullUpdateMessage.NpcIdentity = new Identity
			{
				Type = identityType,
				Instance = instance
			};
			if (vendingMachineFullUpdateMessage.NpcIdentity.Instance == 0)
			{
				vendingMachineFullUpdateMessage.Coordinates = new Vector3();
				vendingMachineFullUpdateMessage.Coordinates.X = streamReader.ReadSingle();
				vendingMachineFullUpdateMessage.Coordinates.Y = streamReader.ReadSingle();
				vendingMachineFullUpdateMessage.Coordinates.Z = streamReader.ReadSingle();
				vendingMachineFullUpdateMessage.Heading = new Quaternion();
				vendingMachineFullUpdateMessage.Heading.X = streamReader.ReadSingle();
				vendingMachineFullUpdateMessage.Heading.Y = streamReader.ReadSingle();
				vendingMachineFullUpdateMessage.Heading.Z = streamReader.ReadSingle();
				vendingMachineFullUpdateMessage.Heading.W = streamReader.ReadSingle();
			}
			vendingMachineFullUpdateMessage.PlayfieldId = streamReader.ReadInt32();
			vendingMachineFullUpdateMessage.Unknown4 = streamReader.ReadInt32();
			vendingMachineFullUpdateMessage.Unknown5 = streamReader.ReadInt32();
			vendingMachineFullUpdateMessage.Unknown6 = streamReader.ReadInt16();
			int num = streamReader.ReadInt32();
			num /= 1009;
			List<GameTuple<CharacterStat, uint>> list = new List<GameTuple<CharacterStat, uint>>();
			while (num > 1)
			{
				GameTuple<CharacterStat, uint> gameTuple = new GameTuple<CharacterStat, uint>();
				gameTuple.Value1 = (CharacterStat)streamReader.ReadInt32();
				gameTuple.Value2 = streamReader.ReadUInt32();
				list.Add(gameTuple);
				num--;
			}
			vendingMachineFullUpdateMessage.Stats = list.ToArray();
			vendingMachineFullUpdateMessage.Unknown7 = streamReader.ReadString(streamReader.ReadInt32()).Replace("\0", "");
			vendingMachineFullUpdateMessage.Unknown8 = streamReader.ReadInt32();
			if (vendingMachineFullUpdateMessage.Unknown8 == 2)
			{
				vendingMachineFullUpdateMessage.Unknown9 = streamReader.ReadInt32();
				num = streamReader.ReadInt32();
				num /= 1009;
				List<Identity> list2 = new List<Identity>();
				while (num > 1)
				{
					identityType = (IdentityType)streamReader.ReadInt32();
					instance = streamReader.ReadInt32();
					list2.Add(new Identity
					{
						Type = identityType,
						Instance = instance
					});
					num--;
				}
				vendingMachineFullUpdateMessage.Unknown10 = list2.ToArray();
			}
			vendingMachineFullUpdateMessage.Unknown11 = streamReader.ReadInt32();
			return vendingMachineFullUpdateMessage;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<VendingMachineFullUpdateMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((VendingMachineFullUpdateMessageSerializer o) => o.Deserialize));
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
			if (value == null)
			{
				return;
			}
			VendingMachineFullUpdateMessage vendingMachineFullUpdateMessage = (VendingMachineFullUpdateMessage)value;
			streamWriter.WriteInt32((int)vendingMachineFullUpdateMessage.N3MessageType);
			streamWriter.WriteIdentity(vendingMachineFullUpdateMessage.Identity);
			streamWriter.WriteByte(vendingMachineFullUpdateMessage.Unknown);
			streamWriter.WriteInt32(vendingMachineFullUpdateMessage.TypeIdentifier);
			streamWriter.WriteIdentity(vendingMachineFullUpdateMessage.NpcIdentity);
			if (vendingMachineFullUpdateMessage.NpcIdentity.Instance == 0)
			{
				streamWriter.WriteSingle(vendingMachineFullUpdateMessage.Coordinates.X);
				streamWriter.WriteSingle(vendingMachineFullUpdateMessage.Coordinates.Y);
				streamWriter.WriteSingle(vendingMachineFullUpdateMessage.Coordinates.Z);
				streamWriter.WriteSingle(vendingMachineFullUpdateMessage.Heading.X);
				streamWriter.WriteSingle(vendingMachineFullUpdateMessage.Heading.Y);
				streamWriter.WriteSingle(vendingMachineFullUpdateMessage.Heading.Z);
				streamWriter.WriteSingle(vendingMachineFullUpdateMessage.Heading.W);
			}
			streamWriter.WriteInt32(vendingMachineFullUpdateMessage.PlayfieldId);
			streamWriter.WriteInt32(vendingMachineFullUpdateMessage.Unknown4);
			streamWriter.WriteInt32(vendingMachineFullUpdateMessage.Unknown5);
			streamWriter.WriteInt16(vendingMachineFullUpdateMessage.Unknown6);
			if (vendingMachineFullUpdateMessage.Stats == null)
			{
				streamWriter.WriteInt32(1009);
			}
			else
			{
				int num = vendingMachineFullUpdateMessage.Stats.Length;
				num = (num + 1) * 1009;
				streamWriter.WriteInt32(num);
				GameTuple<CharacterStat, uint>[] stats = vendingMachineFullUpdateMessage.Stats;
				foreach (GameTuple<CharacterStat, uint> gameTuple in stats)
				{
					streamWriter.WriteInt32((int)gameTuple.Value1);
					streamWriter.WriteUInt32(gameTuple.Value2);
				}
			}
			if (vendingMachineFullUpdateMessage.Unknown7 == null)
			{
				streamWriter.WriteInt32(0);
			}
			else
			{
				streamWriter.WriteInt32(vendingMachineFullUpdateMessage.Unknown7.Length);
				streamWriter.WriteString(vendingMachineFullUpdateMessage.Unknown7);
			}
			streamWriter.WriteInt32(vendingMachineFullUpdateMessage.Unknown8);
			streamWriter.WriteInt32(vendingMachineFullUpdateMessage.Unknown9);
			streamWriter.WriteInt32((vendingMachineFullUpdateMessage.Unknown10.Length + 1) * 1009);
			Identity[] unknown = vendingMachineFullUpdateMessage.Unknown10;
			foreach (Identity value2 in unknown)
			{
				streamWriter.WriteIdentity(value2);
			}
			streamWriter.WriteInt32(vendingMachineFullUpdateMessage.Unknown11);
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<VendingMachineFullUpdateMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((VendingMachineFullUpdateMessageSerializer o) => o.Serialize));
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
	[AoContract(1363747104)]
	public class CharSecSpecAttackMessage : N3Message
	{
		[AoMember(1)]
		public Identity target { get; set; }

		[AoMember(2)]
		public int Unknown1 { get; set; }

		public CharSecSpecAttackMessage()
		{
			base.N3MessageType = N3MessageType.CharSecSpecAttack;
		}
	}
	public class Header
	{
		public ushort MessageId { get; set; }

		public PacketType PacketType { get; set; }

		public int Receiver { get; set; }

		public int Sender { get; set; }

		public short Size { get; set; }

		public short Unknown { get; set; }
	}
	[AoContract(32512)]
	public class InitiateCompressionMessage : MessageBody
	{
		public override PacketType PacketType => PacketType.InitiateCompressionMessage;
	}
	public class Message
	{
		public MessageBody Body { get; set; }

		public Header Header { get; set; }
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
		KnuBotNpcDescription = 658522,
		AddTemplate = 86912780,
		GridDestinationSelect = 104417101,
		WeatherControl = 207248749,
		PetToMaster = 221781762,
		FlushRdbCaches = 276329306,
		ShopSearchResult = 321942351,
		ShopSearchRequest = 341462886,
		AcceptBsInvite = 376062814,
		AddPet = 424562550,
		SetPos = 425609582,
		CityControllerWindowClose = 456941901,
		ReflectAttack = 473583479,
		SpecialAttackWeapon = 490475292,
		MentorInvite = 536950654,
		Action = 541676156,
		Script = 542066801,
		FormatFeedback = 543902579,
		KnuBotAnswer = 553854077,
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
		KnuBotCloseChatWindow = 654986338,
		SimpleCharFullUpdate = 656095851,
		StartLogout = 673521409,
		Attack = 675889264,
		TeamMemberInfo = 678969928,
		CreateQuest = 689911323,
		FullCharacter = 691028809,
		LaserTargetList = 691213647,
		TrapDisarmed = 707084127,
		Fov = 707345679,
		Stat = 724778350,
		QueueUpdate = 741279260,
		KnuBotRejectedItems = 757146631,
		PlayerShopFullUpdate = 772221560,
		OrgInfoPacket = 774523499,
		N3PlayfieldFullUpdate = 806753109,
		ResearchRequest = 823481165,
		AreaFormula = 824779579,
		InfromPlayer = 855716730,
		Mail = 859514983,
		ApplySpells = 875306269,
		Bank = 876357759,
		ShopInventory = 893341522,
		TemplateAction = 894457412,
		Trade = 908611438,
		ToClientQuit = 911278200,
		Despawn = 911278200,
		DoorFullUpdate = 911888497,
		CityAdvantages = 912151899,
		HealthDamage = 923805036,
		FightModeUpdate = 924648770,
		SetShopName = 926823699,
		Buff = 959724648,
		KnuBotTrade = 974859276,
		DropTemplate = 975454017,
		GridSelected = 976366154,
		SimpleItemFullUpdate = 990979439,
		KnuBotOpenChatWindow = 991112548,
		WeaponItemFullUpdate = 991765096,
		SocialActionCmd = 992544625,
		Raid = 993732728,
		ShadowLevel = 1008609283,
		Clone = 1009144185,
		ShopCommission = 1029391684,
		ServerPathPosDebugInfo = 1031040124,
		Skill = 1042306656,
		LeaveBattle = 1060772116,
		ShopInfo = 1079725863,
		AppearanceUpdate = 1096961805,
		N3Teleport = 1125743906,
		PerkUpdate = 1130328099,
		SendScore = 1145584442,
		Resurrect = 1147087371,
		UpdateClientVisual = 1158097453,
		HouseDemolishStart = 1160199946,
		PlaySound = 1163733304,
		AttackInfo = 1174417174,
		TeamMember = 1177627950,
		SpawnMech = 1179451402,
		QuestFullUpdate = 1180319841,
		ChestItemFullUpdate = 1180327283,
		NanoAttack = 1193746750,
		DropDynel = 1195914803,
		ContainerAddItem = 1196653092,
		Visibility = 1226974738,
		StopFight = 1245782078,
		BattleOver = 1258694937,
		InventoryUpdated = 1214149122,
		DoorStatusUpdate = 1283276859,
		LocalityUpdate = 1280508704,
		TeamInvite = 1294610747,
		ShopStatus = 1295200295,
		InfoPacket = 1295524910,
		SpellList = 1296367892,
		InventoryUpdate = 1314089334,
		CorpseFullUpdate = 1330073093,
		Feedback = 1347702041,
		CharSecSpecAttack = 1363747104,
		BankCorpse = 1377907744,
		GenericCmd = 1381132376,
		PathMoveCmd = 1382441770,
		ArriveAtBs = 1410218791,
		CharDCMove = 1410404643,
		ClientMoveItemToInventory = 1416181567,
		ClientContainerAddItem = 525164414,
		ClientGetItem = 924019819,
		PlayfieldAllTowers = 1428293414,
		KnuBotFinishTrade = 1432890148,
		KnuBotAnswerList = 1433423153,
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
		KnuBotAppendText = 1567642410,
		CharacterAction = 1581741936,
		HouseDisappeared = 1583046663,
		Impulse = 1598704748,
		PlayfieldAnarchyF = 1598757433,
		ChatText = 1598768170,
		GameTime = 1599226158,
		SetWantedDirection = 1612717326,
		AoTransportSignal = 1651777045,
		PetCommand = 1798517507,
		OrgServer = 1683499527,
		SetStat = 1851741806,
		SetName = 1934514811,
		StopMovingCmd = 1949180692,
		SpecialAttackInfo = 1968115989,
		GiveQuestToMember = 1998784807,
		KnuBotStartTrade = 2019835933,
		GfxTrigger = 2049057282,
		ShopItemPrice = 2113941807,
		NewLevel = 2134923798,
		OrgClient = 2135634184,
		VendingMachineFullUpdate = 2136230149
	}
	[AoContract(14)]
	public class OperatorMessage : MessageBody
	{
		public override PacketType PacketType => PacketType.OperatorMessage;
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
	[AoContract(1798517507)]
	public class PetCommandMessage : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		[AoMember(4, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] Identities { get; set; }

		[AoMember(5)]
		public int Unknown4 { get; set; }

		[AoMember(6, SerializeSize = ArraySizeType.Int32)]
		public string Name { get; set; }

		public PetCommandMessage()
		{
			base.N3MessageType = N3MessageType.PetCommand;
		}
	}
	[AoContract(11)]
	public class PingMessage : MessageBody
	{
		public override PacketType PacketType => PacketType.PingMessage;
	}
	[AoContract(1)]
	[AoKnownType(16, IdentifierType.Int32)]
	public abstract class SystemMessage : MessageBody
	{
		[AoMember(0)]
		public SystemMessageType SystemMessageType { get; set; }

		public override PacketType PacketType => PacketType.SystemMessage;
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
		public TextMessageRange Range { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		[AoMember(4)]
		public ChatMessage Message { get; set; }

		public override PacketType PacketType => PacketType.TextMessage;
	}
	public enum TextMessageRange
	{
		Whisper = 2,
		Say,
		Shout
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
	public enum LoginError
	{
		AlreadyLoggedIn = 20,
		InvalidUserNamePassword = 106,
		PlayerBannedOrNotPaid = 108
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
	[AoContract(27)]
	public class ZoneLoginMessage : SystemMessage
	{
		[AoMember(0)]
		public int CharacterId { get; set; }

		public ZoneLoginMessage()
		{
			base.SystemMessageType = SystemMessageType.ZoneLogin;
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
}
namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
	[AoContract(541676156)]
	public class ActionMessage : N3Message
	{
		[AoMember(0)]
		public int ActionCode { get; set; }

		[AoMember(1)]
		public int ActionIdentity { get; set; }

		[AoMember(2)]
		public Identity Target { get; set; }

		public ActionMessage()
		{
			base.N3MessageType = N3MessageType.Action;
		}
	}
	[AoContract(424562550)]
	public class AddPetMessage : N3Message
	{
		[AoMember(1)]
		public Identity PetIdentity { get; set; }

		public AddPetMessage()
		{
			base.N3MessageType = N3MessageType.AddPet;
		}
	}
	[AoContract(824779579)]
	public class AreaFormulaMessage : N3Message
	{
		[AoMember(1, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public Identity Unknown3 { get; set; }

		public AreaFormulaMessage()
		{
			base.N3MessageType = N3MessageType.AreaFormula;
		}
	}
	[AoContract(1174417174)]
	public class AttackInfoMessage : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		[AoMember(4)]
		public Identity Target { get; set; }

		[AoMember(5)]
		public int Unknown4 { get; set; }

		[AoMember(6)]
		public int Unknown5 { get; set; }

		[AoMember(7)]
		public int Unknown6 { get; set; }

		public AttackInfoMessage()
		{
			base.N3MessageType = N3MessageType.AttackInfo;
		}
	}
	[AoContract(675889264)]
	public class AttackMessage : N3Message
	{
		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public byte Action { get; set; }

		public AttackMessage()
		{
			base.N3MessageType = N3MessageType.Attack;
		}
	}
	[AoContract(959724648)]
	public class BuffMessage : N3Message
	{
		[AoMember(1)]
		public short Action { get; set; }

		[AoMember(2)]
		public Identity NanoProgram { get; set; }

		public BuffMessage()
		{
			base.N3MessageType = N3MessageType.Buff;
		}
	}
	[AoContract(1180327283)]
	public class ChestItemFullUpdateMessage : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public Identity Owner { get; set; }

		[AoMember(3)]
		public int PlayfieldId { get; set; }

		[AoMember(4)]
		public Identity StateMachine { get; set; }

		[AoMember(5)]
		public short Unknown5 { get; set; }

		[AoMember(6, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<CharacterStat, uint>[] Stats { get; set; }

		[AoMember(7)]
		public int Unknown6 { get; set; }

		[AoMember(8)]
		public int Unknown7 { get; set; }

		[AoMember(9)]
		public int Unknown8 { get; set; }

		[AoMember(10, SerializeSize = ArraySizeType.X3F1)]
		public int[] UnknownArray { get; set; }

		[AoMember(11)]
		public int Unknown9 { get; set; }

		public ChestItemFullUpdateMessage()
		{
			base.N3MessageType = N3MessageType.ChestItemFullUpdate;
		}
	}
	[AoContract(1330073093)]
	[Obsolete("Placeholder only. ZoneEngine sends corpse dynels with ZoneEngine.Core.Packets.CorpseFullUpdate.Build until a capture-backed serializer exists.", false)]
	public class CorpseFullUpdateMessage : N3Message
	{
		[AoMember(1)]
		public int MsgVersion { get; set; }

		public CorpseFullUpdateMessage()
		{
			base.N3MessageType = N3MessageType.CorpseFullUpdate;
		}
	}
	[AoContract(689911323)]
	public class CreateQuestMessage : N3Message
	{
		[AoMember(1)]
		public Identity QuestIdentity { get; set; }

		public CreateQuestMessage()
		{
			base.N3MessageType = N3MessageType.CreateQuest;
		}
	}
	[AoContract(911888497)]
	public class DoorFullUpdateMessage : N3Message
	{
		private int identityType;

		private int instance;

		public Identity Owner { get; set; }

		[AoMember(1)]
		public int MsgVersion { get; set; }

		[AoMember(2)]
		[AoFlags("flag")]
		public int Identitytype
		{
			get
			{
				return identityType;
			}
			set
			{
				identityType = value;
				Owner = new Identity
				{
					Type = (IdentityType)value,
					Instance = instance
				};
			}
		}

		[AoMember(3)]
		public int Instance
		{
			get
			{
				return instance;
			}
			set
			{
				instance = value;
				Owner = new Identity
				{
					Type = (IdentityType)identityType,
					Instance = value
				};
			}
		}

		[AoMember(4)]
		[AoUsesFlags("flag", typeof(Vector3), FlagsCriteria.HasNone, new int[] { int.MaxValue })]
		public Vector3 Coordinate { get; set; }

		[AoMember(5)]
		[AoUsesFlags("flag", typeof(Quaternion), FlagsCriteria.HasNone, new int[] { int.MaxValue })]
		public Quaternion Heading { get; set; }

		[AoMember(6)]
		public int Playfield { get; set; }

		[AoMember(7)]
		public Identity Unknown1 { get; set; }

		[AoMember(8)]
		public byte Unknown2 { get; set; }

		[AoMember(9)]
		public byte Unknown3 { get; set; }

		[AoMember(10, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<CharacterStat, uint>[] Stats { get; set; }

		[AoMember(11, SerializeSize = ArraySizeType.Int32)]
		public string Name { get; set; }

		[AoMember(12)]
		public int Unknown4 { get; set; }

		[AoMember(13)]
		public int Unknown5 { get; set; }

		[AoMember(14, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] Identities { get; set; }

		[AoMember(15)]
		public int Unknown6 { get; set; }

		[AoMember(16)]
		public int Unknown7 { get; set; }

		public DoorFullUpdateMessage()
		{
			base.N3MessageType = N3MessageType.DoorFullUpdate;
		}
	}
	[AoContract(1283276859)]
	public class DoorStatusUpdateMessage : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public byte Unknown2 { get; set; }

		[AoMember(3)]
		public byte Unknown3 { get; set; }

		[AoMember(4)]
		public int Unknown4 { get; set; }

		[AoMember(4)]
		public byte Unknown5 { get; set; }

		[AoMember(6, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] Unknown6 { get; set; }

		public DoorStatusUpdateMessage()
		{
			base.N3MessageType = N3MessageType.DoorStatusUpdate;
		}
	}
	[AoContract(924648770)]
	public class FightModeUpdateMessage : N3Message
	{
		[AoMember(1)]
		public Identity Unknown1 { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.X3F1)]
		public FightModeUpdateEntry[] Entries { get; set; }

		public FightModeUpdateMessage()
		{
			base.N3MessageType = N3MessageType.FightModeUpdate;
		}
	}
	[AoContract(1548372282)]
	public class FullAutoMessage : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		public FullAutoMessage()
		{
			base.N3MessageType = N3MessageType.FullAuto;
		}
	}
	[AoContract(923805036)]
	public class HealthDamageMessage : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		[AoMember(4)]
		public int Unknown4 { get; set; }

		[AoMember(5)]
		public Identity Target { get; set; }

		[AoMember(6)]
		public int Unknown5 { get; set; }

		public HealthDamageMessage()
		{
			base.N3MessageType = N3MessageType.HealthDamage;
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
		public Mesh[] Meshes { get; set; }

		[AoMember(2)]
		public short VisualFlags { get; set; }

		[AoMember(3)]
		public byte Unknown1 { get; set; }

		public AppearanceUpdateMessage()
		{
			base.N3MessageType = N3MessageType.AppearanceUpdate;
		}
	}
	[AoContract(876357759)]
	public class BankMessage : N3Message
	{
		[AoMember(0, SerializeSize = ArraySizeType.X3F1)]
		public BankSlot[] BankSlots { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public Identity Unknown2 { get; set; }

		public BankMessage()
		{
			base.N3MessageType = N3MessageType.Bank;
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
	[AoContract(525164414)]
	public class ClientContainerAddItemMessage : N3Message
	{
		public Identity Target { get; set; }

		public Identity Source { get; set; }

		public ClientContainerAddItemMessage()
		{
			base.N3MessageType = N3MessageType.ClientContainerAddItem;
		}
	}
	[AoContract(924019819)]
	public class ClientGetItemMessage : N3Message
	{
		public Identity Identity1 { get; set; }

		public ClientGetItemMessage()
		{
			base.N3MessageType = N3MessageType.ClientGetItem;
		}
	}
	public class DespawnMessage : N3Message
	{
		public DespawnMessage()
		{
			base.N3MessageType = N3MessageType.ToClientQuit;
		}
	}
	[AoContract(1195914803)]
	public class DropDynelMessage : N3Message
	{
		public Vector3 Position { get; set; }

		public DropDynelMessage()
		{
			base.N3MessageType = N3MessageType.DropDynel;
		}
	}
	[AoContract(1280508704)]
	public class LocalityUpdateMessage : N3Message
	{
		public Vector3 Position { get; set; }

		public byte LocalityFlag { get; set; }

		public LocalityUpdateMessage()
		{
			base.N3MessageType = N3MessageType.LocalityUpdate;
		}
	}
	[AoContract(642470219)]
	public class RelocateDynelsMessage : N3Message
	{
		public Identity[] RelocatedIdentities { get; set; }

		public RelocateDynelsMessage()
		{
			base.N3MessageType = N3MessageType.RelocateDynels;
			RelocatedIdentities = new Identity[0];
		}
	}
	[AoContract(911278200)]
	public class ToClientQuitMessage : N3Message
	{
		public ToClientQuitMessage()
		{
			base.N3MessageType = N3MessageType.ToClientQuit;
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
		public byte Unknown1 { get; set; }

		[AoMember(2)]
		public byte Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		public ChatTextMessage()
		{
			base.N3MessageType = N3MessageType.ChatText;
		}
	}
	[AoContract(1416181567)]
	public class ClientMoveItemToInventoryMessage : N3Message
	{
		[AoMember(0)]
		public Identity SourceContainer { get; set; }

		[AoMember(1)]
		public int TargetPlacement { get; set; }

		public ClientMoveItemToInventoryMessage()
		{
			base.N3MessageType = N3MessageType.ClientMoveItemToInventory;
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
	[AoContract(543902579)]
	public class FormatFeedbackMessage : N3Message
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public string FormattedMessage { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		public FormatFeedbackMessage()
		{
			base.N3MessageType = N3MessageType.FormatFeedback;
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
	[AoContract(1214149122)]
	public class InventoryUpdatedMessage : N3Message
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		public InventoryUpdatedMessage()
		{
			base.N3MessageType = N3MessageType.InventoryUpdated;
		}
	}
	[AoContract(1314089334)]
	public class InventoryUpdateMessage : N3Message
	{
		[AoMember(0)]
		public int NumberOfSlots { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.X3F1)]
		public InventoryEntry[] Entries { get; set; }

		[AoMember(3)]
		public Identity BagIdentity { get; set; }

		[AoMember(4)]
		public int SlotnumberInMainInventory { get; set; }

		[AoMember(5)]
		public int Unknown2 { get; set; }

		public InventoryUpdateMessage()
		{
			base.N3MessageType = N3MessageType.InventoryUpdate;
		}
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
			base.N3MessageType = N3MessageType.KnuBotAnswerList;
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
			base.N3MessageType = N3MessageType.KnuBotAppendText;
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

		public KnuBotFinishTradeMessage()
		{
			base.N3MessageType = N3MessageType.KnuBotFinishTrade;
		}
	}
	[AoContract(1410404643)]
	public class CharDCMoveMessage : N3Message
	{
		[AoMember(0)]
		public byte MoveType { get; set; }

		[AoMember(1)]
		public Quaternion Heading { get; set; }

		[AoMember(2)]
		public Vector3 Coordinates { get; set; }

		[AoMember(3)]
		public int Unknown1 { get; set; }

		[AoMember(4)]
		public float AuxA { get; set; }

		[AoMember(5)]
		public float AuxB { get; set; }

		public float Unknown2
		{
			get
			{
				return AuxA;
			}
			set
			{
				AuxA = value;
			}
		}

		public float Unknown3
		{
			get
			{
				return AuxB;
			}
			set
			{
				AuxB = value;
			}
		}

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
	[AoContract(2134923798)]
	public class NewLevelMessage : N3Message
	{
		[AoMember(0)]
		public int Level { get; set; }

		[AoMember(1)]
		public int Ip { get; set; }

		[AoMember(2)]
		public int Xp { get; set; }

		[AoMember(3)]
		public int LastSaveXp { get; set; }

		[AoMember(4)]
		public int NextLevelXp { get; set; }

		[AoMember(5)]
		public int Unknown1 { get; set; }

		[AoMember(6)]
		public int Unknown2 { get; set; }

		[AoMember(7)]
		public int LastXp { get; set; }

		public NewLevelMessage()
		{
			base.N3MessageType = N3MessageType.NewLevel;
		}
	}
	[AoContract(1196653092)]
	public class ContainerAddItemMessage : N3Message
	{
		[AoMember(0)]
		public Identity SourceContainer { get; set; }

		[AoMember(1)]
		public Identity Target { get; set; }

		[AoMember(2)]
		public int TargetPlacement { get; set; }

		public ContainerAddItemMessage()
		{
			base.N3MessageType = N3MessageType.ContainerAddItem;
		}
	}
	[AoContract(638531185)]
	public class FollowTargetMessage : N3Message
	{
		[AoMember(0)]
		public FollowInfo Info { get; set; }

		public FollowTargetMessage()
		{
			base.N3MessageType = N3MessageType.FollowTarget;
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

		[AoMember(2)]
		public GenericCmdAction Action { get; set; }

		[AoMember(3)]
		public int Temp4 { get; set; }

		[AoMember(4)]
		public Identity User { get; set; }

		[AoMember(5)]
		public Identity[] Target { get; set; }

		public GenericCmdMessage()
		{
			base.N3MessageType = N3MessageType.GenericCmd;
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
		public int Seconds { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		public KnuBotCloseChatWindowMessage()
		{
			base.N3MessageType = N3MessageType.KnuBotCloseChatWindow;
		}
	}
	[AoContract(658522)]
	public class KnuBotNpcDescriptionMessage : N3Message
	{
		[AoMember(1)]
		[AoFlags("flag")]
		public short Unknown1 { get; set; }

		[AoMember(2)]
		[AoUsesFlags("flag", typeof(Identity), FlagsCriteria.HasAll, new int[] { 2 })]
		public Identity? Unknown2 { get; set; }

		public KnuBotNpcDescriptionMessage()
		{
			base.N3MessageType = N3MessageType.KnuBotNpcDescription;
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
			base.N3MessageType = N3MessageType.KnuBotOpenChatWindow;
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
			base.N3MessageType = N3MessageType.KnuBotRejectedItems;
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
			base.N3MessageType = N3MessageType.KnuBotStartTrade;
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
			base.N3MessageType = N3MessageType.KnuBotTrade;
		}
	}
	[AoContract(575816799)]
	public class LookAtMessage : N3Message
	{
		[AoMember(0)]
		public Identity Target { get; set; }

		[AoMember(1)]
		public int ReturnInfo { get; set; }

		public LookAtMessage()
		{
			base.N3MessageType = N3MessageType.LookAt;
		}
	}
	[AoContract(1651777045)]
	public class AOTransportSignalMessage : N3Message
	{
		[AoMember(3)]
		public int Signal { get; set; }

		public byte[] Payload { get; set; }

		public AOTransportSignalMessage()
		{
			base.N3MessageType = N3MessageType.AoTransportSignal;
		}
	}
	[AoContract(456941901)]
	public class CityControllerWindowCloseMessage : N3Message
	{
		[AoMember(0)]
		public int WindowInstance { get; set; }

		public CityControllerWindowCloseMessage()
		{
			base.N3MessageType = N3MessageType.CityControllerWindowClose;
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
	public enum CharacterActionType
	{
		TeamRequest = 26,
		CastNano = 19,
		TeamRequestReply = 21,
		TeamKickMember = 22,
		LeaveTeam = 32,
		AcceptTeamRequest = 35,
		RemoveFriendlyNano = 65,
		UseItemOnItem = 81,
		StandUp = 87,
		Unknown3 = 97,
		SetNanoDuration = 98,
		ItemAnim = 99,
		Death = 99,
		InfoRequest = 105,
		FinishNanoCasting = 107,
		InterruptNanoCasting = 108,
		UseActionFinished = 110,
		DeleteItem = 112,
		Logout = 120,
		StopLogout = 122,
		Equip = 131,
		SpecialUnavailable = 132,
		Die = 152,
		StartedSneaking = 162,
		StartSneak = 163,
		SpecialAvailable = 164,
		DisableXP = 165,
		Search = 102,
		ChangeVisualFlag = 166,
		ChangeAnimationAndStance = 167,
		SpecialUsed = 170,
		DeathRespawn = 171,
		SitDown = 263,
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
		Split = 34
	}
	[AoContract(912151899)]
	public class CityAdvantagesMessage : N3Message
	{
		[AoMember(1, SerializeSize = ArraySizeType.Int32)]
		public CityAdvantage[] Advantages { get; set; }

		public CityAdvantagesMessage()
		{
			base.N3MessageType = N3MessageType.CityAdvantages;
			Advantages = new CityAdvantage[0];
		}
	}
	public class CityAdvantage
	{
		[AoMember(0)]
		public int LowId { get; set; }

		[AoMember(1)]
		public int HighId { get; set; }

		[AoMember(2)]
		public int QualityLevel { get; set; }

		[AoMember(3)]
		public int Unknown { get; set; }
	}
	[AoContract(691028809)]
	public class FullCharacterMessage : N3Message
	{
		[AoMember(0)]
		public int MsgVersion { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.X3F1)]
		public InventorySlot[] InventorySlots { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.X3F1)]
		public int[] UploadedNanoIds { get; set; }

		[AoMember(3, SerializeSize = ArraySizeType.X3F1)]
		public FullCharacterSub[] Unknown2 { get; set; }

		[AoMember(4)]
		public int Unknown3 { get; set; }

		[AoMember(6, SerializeSize = ArraySizeType.Int32)]
		public FullCharacterSub2[] Unknown4 { get; set; }

		[AoMember(7)]
		public int UnknownI2 { get; set; }

		[AoMember(8, SerializeSize = ArraySizeType.Int32)]
		public FullCharacterSub2[] Unknown5 { get; set; }

		[AoMember(9)]
		public int UnknownI3 { get; set; }

		[AoMember(10, SerializeSize = ArraySizeType.Int32)]
		public FullCharacterSub2[] Unknown6 { get; set; }

		[AoMember(11, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<int, uint>[] Stats1 { get; set; }

		[AoMember(12, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<int, uint>[] Stats2 { get; set; }

		[AoMember(13, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<byte, byte>[] Stats3 { get; set; }

		[AoMember(14, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<byte, short>[] Stats4 { get; set; }

		[AoMember(15)]
		public int Unknown9 { get; set; }

		[AoMember(16)]
		public int Unknown10 { get; set; }

		[AoMember(17, SerializeSize = ArraySizeType.X3F1)]
		public object[] Unknown11 { get; set; }

		[AoMember(18, SerializeSize = ArraySizeType.X3F1)]
		public object[] Unknown12 { get; set; }

		[AoMember(19, SerializeSize = ArraySizeType.X3F1)]
		public object[] Unknown13 { get; set; }

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
			base.N3MessageType = N3MessageType.KnuBotAnswer;
		}
	}
	[AoContract(1482113593)]
	public class MechInfoMessage : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.Int32)]
		public GameTuple<CharacterStat, uint>[] Stats { get; set; }

		[AoMember(3)]
		public Identity MechIdentity { get; set; }

		[AoMember(4, FixedSizeLength = 4)]
		public string Hash { get; set; }

		public MechInfoMessage()
		{
			base.N3MessageType = N3MessageType.MechInfo;
		}
	}
	[AoContract(1550142248)]
	public class MissedAttackInfoMessage : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public Identity Unknown3 { get; set; }

		[AoMember(4)]
		public Identity Unknown4 { get; set; }

		[AoMember(5)]
		public int Unknown5 { get; set; }

		public MissedAttackInfoMessage()
		{
			base.N3MessageType = N3MessageType.MissedAttackInfo;
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

		public byte[] Payload { get; set; }

		public N3TeleportMessage()
		{
			base.N3MessageType = N3MessageType.N3Teleport;
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
		CityAdvantages = 31
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
			25, 26, 27, 28, 31
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
		[AoMember(0, SerializeSize = ArraySizeType.Int16)]
		public string Name { get; set; }

		public OrgInfoPacketMessage()
		{
			base.N3MessageType = N3MessageType.OrgInfoPacket;
		}
	}
	[AoContract(1683499527)]
	[AoKnownType(29, IdentifierType.Byte)]
	public abstract class OrgServerMessage : N3Message
	{
		[AoMember(0)]
		public OrgServerMessageType OrgServerMessageType { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public Identity Organization { get; set; }

		[AoMember(4, SerializeSize = ArraySizeType.Int16)]
		public string OrganizationName { get; set; }

		protected OrgServerMessage()
		{
			base.N3MessageType = N3MessageType.OrgServer;
		}
	}
	public enum OrgServerMessageType : byte
	{
		OrgInfo = 2,
		OrgInvite = 5,
		OrgContract = 6
	}
	[AoContract(1130328099)]
	public class PerkUpdateMessage : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		public PerkUpdateMessage()
		{
			base.N3MessageType = N3MessageType.PerkUpdate;
		}
	}
	[AoContract(221781762)]
	public class PetToMasterMessage : N3Message
	{
		[AoMember(1)]
		public Identity PetIdentity { get; set; }

		[AoMember(2)]
		public int Unknown1 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		[AoMember(4)]
		public Identity Unknown4 { get; set; }

		public PetToMasterMessage()
		{
			base.N3MessageType = N3MessageType.PetToMaster;
		}
	}
	[AoContract(1495335206)]
	public class PlayfieldAllCitiesMessage : N3Message
	{
		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public byte[] Payload { get; set; }

		public PlayfieldAllCitiesMessage()
		{
			base.N3MessageType = N3MessageType.PlayfieldAllCities;
		}
	}
	[AoContract(1428293414)]
	public class PlayfieldAllTowersMessage : N3Message
	{
		[AoMember(1, SerializeSize = ArraySizeType.X3F1)]
		public TowerProxyBase[] Unknown1 { get; set; }

		public PlayfieldAllTowersMessage()
		{
			base.N3MessageType = N3MessageType.PlayfieldAllTowers;
		}
	}
	[AoContract(1598757433)]
	public class PlayfieldAnarchyFMessage : N3Message
	{
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
		public int Unknown4 { get; set; }

		[AoMember(6)]
		public Identity PlayfieldId2 { get; set; }

		[AoMember(7)]
		public int Unknown5 { get; set; }

		[AoMember(8)]
		public int Unknown6 { get; set; }

		[AoMember(9)]
		public PlayfieldVendorInfo PlayfieldVendorInfo { get; set; }

		[AoMember(10)]
		public int PlayfieldX { get; set; }

		[AoMember(11)]
		public int PlayfieldZ { get; set; }

		public byte[] GeneratorPayload { get; set; }

		public PlayfieldAnarchyFMessage()
		{
			base.N3MessageType = N3MessageType.PlayfieldAnarchyF;
			base.Unknown = 0;
			Unknown1 = 4;
			Unknown2 = 97;
		}
	}
	[AoContract(1163733304)]
	public class PlaySoundMessage : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.Int32)]
		public string Unknown2 { get; set; }

		[AoMember(3)]
		public Identity Unknown3 { get; set; }

		public PlaySoundMessage()
		{
			base.N3MessageType = N3MessageType.PlaySound;
		}
	}
	[AoContract(1547920905)]
	public class QuestAlternativeMessage : N3Message
	{
		[AoMember(0)]
		public byte VersionId { get; set; }

		[AoMember(1)]
		public byte LevelSlider { get; set; }

		[AoMember(2)]
		public byte GoodBadSlider { get; set; }

		[AoMember(3)]
		public byte OrderChaosSlider { get; set; }

		[AoMember(4)]
		public byte OpenHiddenSlider { get; set; }

		[AoMember(5)]
		public byte PhysicalMysticalSlider { get; set; }

		[AoMember(6)]
		public byte HeadOnStealthSlider { get; set; }

		[AoMember(7)]
		public byte MoneyExperienceSlider { get; set; }

		[AoMember(8)]
		public int Unknown4 { get; set; }

		[AoMember(9)]
		public byte Unknown5 { get; set; }

		[AoMember(10)]
		public Identity MissionTerminalIdentity { get; set; }

		[AoMember(11, SerializeSize = ArraySizeType.Byte)]
		public QuestInfo[] QuestInfos { get; set; }

		public QuestAlternativeMessage()
		{
			base.N3MessageType = N3MessageType.QuestAlternative;
		}
	}
	public enum QuestAction
	{
		Delete = 1
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
	[AoContract(1484007951)]
	public class RemovePetMessage : N3Message
	{
		[AoMember(1)]
		public Identity PetIdentity { get; set; }

		public RemovePetMessage()
		{
			base.N3MessageType = N3MessageType.RemovePet;
		}
	}
	[AoContract(823481165)]
	public class ResearchRequestMessage : N3Message
	{
		public ResearchRequestMessage()
		{
			base.N3MessageType = N3MessageType.ResearchRequest;
		}
	}
	[AoContract(624755264)]
	public class ResearchUpdateMessage : N3Message
	{
		[AoMember(1)]
		public byte Unknown1 { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.NullTerminated)]
		public ResearchUpdateEntry[] Entries { get; set; }

		public ResearchUpdateMessage()
		{
			base.N3MessageType = N3MessageType.ResearchUpdate;
		}
	}
	[AoContract(1147087371)]
	public class ResurrectMessage : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		public ResurrectMessage()
		{
			base.N3MessageType = N3MessageType.Resurrect;
		}
	}
	[AoContract(425609582)]
	public class SetPosMessage : N3Message
	{
		[AoMember(1)]
		public Vector3 Coordinates { get; set; }

		[AoMember(2)]
		public byte Unknown1 { get; set; }

		[AoMember(3)]
		public int Unknown2 { get; set; }

		[AoMember(4)]
		public byte Unknown3 { get; set; }

		public SetPosMessage()
		{
			base.N3MessageType = N3MessageType.SetPos;
		}
	}
	[AoContract(1612717326)]
	public class SetWantedDirectionMessage : N3Message
	{
		[AoMember(1)]
		public Vector3 DirectinVector { get; set; }

		public SetWantedDirectionMessage()
		{
			base.N3MessageType = N3MessageType.SetWantedDirection;
		}
	}
	[AoContract(1008609283)]
	public class ShadowLevelMessage : N3Message
	{
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

		[AoMember(6)]
		public int Unknown6 { get; set; }

		[AoMember(7)]
		public int Unknown7 { get; set; }

		[AoMember(8)]
		public int Unknown8 { get; set; }

		public ShadowLevelMessage()
		{
			base.N3MessageType = N3MessageType.ShadowLevel;
		}
	}
	[AoContract(622404726)]
	public class ShieldAttackMessage : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public Identity Target { get; set; }

		[AoMember(3)]
		public int Unknown2 { get; set; }

		public ShieldAttackMessage()
		{
			base.N3MessageType = N3MessageType.ShieldAttack;
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
		HasExtendedTextures = 0x10,
		HasFightingTarget = 0x20,
		UnknownFlag6 = 8,
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
		[AoMember(0)]
		public byte Version { get; set; }

		[AoMember(1)]
		public SimpleCharFullUpdateFlags Flags { get; set; }

		[AoMember(2)]
		public int? PlayfieldId { get; set; }

		[AoMember(3)]
		public Identity? FightingTarget { get; set; }

		[AoMember(4)]
		public Vector3 Coordinates { get; set; }

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
		public SimpleCharacterInfo CharacterInfo { get; set; }

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
		public byte[] Unknown1 { get; set; }

		[AoMember(20)]
		public uint? HeadMesh { get; set; }

		[AoMember(21)]
		public short RunSpeedBase { get; set; }

		public SimpleCharFullUpdateFlags AdditionalFlags { get; set; }

		public SimpleCharFullUpdateFlags SuppressedFlags { get; set; }

		public byte[] ExtendedTextureOverrideData { get; set; }

		public byte[] RawBody { get; set; }

		public bool TailFullyDecoded { get; set; }

		public byte[] UndecodedTail { get; set; }

		public Vector3[] Waypoints { get; set; }

		[AoMember(22, SerializeSize = ArraySizeType.X3F1)]
		public ActiveNano[] ActiveNanos { get; set; }

		[AoMember(23, SerializeSize = ArraySizeType.X3F1)]
		public Texture[] Textures { get; set; }

		[AoMember(24, SerializeSize = ArraySizeType.X3F1)]
		public Mesh[] Meshes { get; set; }

		[AoMember(25)]
		public int Flags2 { get; set; }

		[AoMember(26)]
		public byte Unknown2 { get; set; }

		public byte Unknown4 { get; set; }

		public SimpleCharFullUpdateMessage()
		{
			base.N3MessageType = N3MessageType.SimpleCharFullUpdate;
			base.Unknown = 0;
		}
	}
	[AoContract(990979439)]
	public class SimpleItemFullUpdateMessage : N3Message
	{
		private int identityType;

		private int instance;

		public Identity Owner { get; set; }

		[AoMember(1)]
		public int MsgVersion { get; set; }

		[AoMember(2)]
		[AoFlags("flag")]
		public int Identitytype
		{
			get
			{
				return identityType;
			}
			set
			{
				identityType = value;
				Owner = new Identity
				{
					Type = (IdentityType)value,
					Instance = instance
				};
			}
		}

		[AoMember(3)]
		public int Instance
		{
			get
			{
				return instance;
			}
			set
			{
				instance = value;
				Owner = new Identity
				{
					Type = (IdentityType)identityType,
					Instance = value
				};
			}
		}

		[AoMember(4)]
		[AoUsesFlags("flag", typeof(Vector3), FlagsCriteria.HasNone, new int[] { int.MaxValue })]
		public Vector3 Coordinate { get; set; }

		[AoMember(5)]
		[AoUsesFlags("flag", typeof(Quaternion), FlagsCriteria.HasNone, new int[] { int.MaxValue })]
		public Quaternion Heading { get; set; }

		[AoMember(6)]
		public int Playfield { get; set; }

		[AoMember(7)]
		public Identity Unknown1 { get; set; }

		[AoMember(8)]
		public byte Unknown2 { get; set; }

		[AoMember(9)]
		public byte Unknown3 { get; set; }

		[AoMember(10, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<CharacterStat, uint>[] Stats { get; set; }

		[AoMember(11, SerializeSize = ArraySizeType.Int32)]
		public string Name { get; set; }

		public SimpleItemFullUpdateMessage()
		{
			base.N3MessageType = N3MessageType.SimpleItemFullUpdate;
		}
	}
	[AoContract(1042306656)]
	public class SkillMessage : N3Message
	{
		[AoMember(0, SerializeSize = ArraySizeType.Int32)]
		public GameTuple<CharacterStat, uint>[] Skills { get; set; }

		public SkillMessage()
		{
			base.N3MessageType = N3MessageType.Skill;
		}
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
	[AoContract(1968115989)]
	public class SpecialAttackInfo : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		[AoMember(4)]
		public Identity Target { get; set; }

		[AoMember(5)]
		public int Unknown4 { get; set; }

		[AoMember(6)]
		public int Unknown5 { get; set; }

		public SpecialAttackInfo()
		{
			base.N3MessageType = N3MessageType.SpecialAttackInfo;
		}
	}
	[AoContract(490475292)]
	public class SpecialAttackWeaponMessage : N3Message
	{
		[AoMember(1, SerializeSize = ArraySizeType.X3F1)]
		public SpecialAttack[] Specials { get; set; }

		[AoMember(2)]
		public int Unknown1 { get; set; }

		[AoMember(3)]
		public int Unknown2 { get; set; }

		[AoMember(4)]
		public int Unknown3 { get; set; }

		[AoMember(5)]
		public int Unknown4 { get; set; }

		[AoMember(6)]
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
		[AoMember(0, SerializeSize = ArraySizeType.X3F1)]
		public NanoEffect[] NanoEffects { get; set; }

		[AoMember(1)]
		public Identity Character { get; set; }

		public SpellListMessage()
		{
			base.N3MessageType = N3MessageType.SpellList;
		}
	}
	[AoContract(724778350)]
	public class StatMessage : N3Message
	{
		[AoMember(0, SerializeSize = ArraySizeType.Int32)]
		public GameTuple<CharacterStat, uint>[] Stats { get; set; }

		public StatMessage()
		{
			base.N3MessageType = N3MessageType.Stat;
		}
	}
	[AoContract(673521409)]
	public class StartLogoutMessage : N3Message
	{
		public StartLogoutMessage()
		{
			base.N3MessageType = N3MessageType.StartLogout;
		}
	}
	[AoContract(1245782078)]
	public class StopFightMessage : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		public StopFightMessage()
		{
			base.N3MessageType = N3MessageType.StopFight;
		}
	}
	[AoContract(1446326328)]
	public class StopLogoutMessage : N3Message
	{
		public StopLogoutMessage()
		{
			base.N3MessageType = N3MessageType.StopLogout;
		}
	}
	[AoContract(1949180692)]
	public class StopMovingCmdMessage : N3Message
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		public StopMovingCmdMessage()
		{
			base.N3MessageType = N3MessageType.StopMovingCmd;
		}
	}
	[AoContract(678969928)]
	public class TeamMemberInfoMessage : N3Message
	{
		[AoMember(0)]
		public byte Unknown1 { get; set; }

		[AoMember(1)]
		public short Unknown2 { get; set; }

		[AoMember(2)]
		public Identity Character { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		[AoMember(4)]
		public int Unknown4 { get; set; }

		[AoMember(5)]
		public int Unknown5 { get; set; }

		[AoMember(6)]
		public int Unknown6 { get; set; }

		[AoMember(7)]
		public short Unknown7 { get; set; }

		public TeamMemberInfoMessage()
		{
			base.N3MessageType = N3MessageType.TeamMemberInfo;
		}
	}
	[AoContract(1177627950)]
	public class TeamMemberMessage : N3Message
	{
		[AoMember(0)]
		public byte Unknown1 { get; set; }

		[AoMember(1)]
		public short Unknown2 { get; set; }

		[AoMember(2)]
		public Identity Character { get; set; }

		[AoMember(3)]
		public Identity Team { get; set; }

		[AoMember(4)]
		public uint Unknown3 { get; set; }

		[AoMember(5)]
		public int Unknown4 { get; set; }

		[AoMember(6)]
		public short Unknown5 { get; set; }

		[AoMember(7, SerializeSize = ArraySizeType.Int32)]
		public string Name { get; set; }

		[AoMember(8)]
		public short Unknown6 { get; set; }

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
	public enum TradeAction : byte
	{
		Open = 0,
		None = 0,
		Accept = 1,
		End = 1,
		Decline = 2,
		Confirm = 3,
		Complete = 4,
		Unknown = 4,
		AddItem = 5,
		RemoveItem = 6,
		UpdateCredits = 7,
		Credits = 7,
		OtherPlayerAddItem = 8
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

		public Identity Target
		{
			get
			{
				Identity result = default(Identity);
				result.Type = (IdentityType)Param1;
				result.Instance = Param2;
				return result;
			}
			set
			{
				Param1 = (int)value.Type;
				Param2 = value.Instance;
			}
		}

		public Identity Container
		{
			get
			{
				Identity result = default(Identity);
				result.Type = (IdentityType)Param3;
				result.Instance = Param4;
				return result;
			}
			set
			{
				Param3 = (int)value.Type;
				Param4 = value.Instance;
			}
		}

		public TradeMessage()
		{
			base.N3MessageType = N3MessageType.Trade;
		}
	}
	[AoContract(2136230149)]
	public class VendingMachineFullUpdateMessage : N3Message
	{
		[AoMember(1)]
		public int TypeIdentifier { get; set; }

		[AoMember(2)]
		public Identity NpcIdentity { get; set; }

		[AoMember(3)]
		public Vector3 Coordinates { get; set; }

		[AoMember(4)]
		public Quaternion Heading { get; set; }

		[AoMember(5)]
		public int PlayfieldId { get; set; }

		[AoMember(6)]
		public int Unknown4 { get; set; }

		[AoMember(7)]
		public int Unknown5 { get; set; }

		[AoMember(8)]
		public short Unknown6 { get; set; }

		[AoMember(9, SerializeSize = ArraySizeType.X3F1)]
		public GameTuple<CharacterStat, uint>[] Stats { get; set; }

		[AoMember(10, SerializeSize = ArraySizeType.Int32)]
		public string Unknown7 { get; set; }

		[AoMember(11)]
		public int Unknown8 { get; set; }

		[AoMember(12)]
		public int Unknown9 { get; set; }

		[AoMember(13, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] Unknown10 { get; set; }

		[AoMember(14)]
		public int Unknown11 { get; set; }

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
		public GameTuple<CharacterStat, uint>[] Stats { get; set; }

		[AoMember(6)]
		public int Unknown3 { get; set; }

		public WeaponItemFullUpdateMessage()
		{
			base.N3MessageType = N3MessageType.WeaponItemFullUpdate;
		}
	}
	[AoContract(207248749)]
	public class WeatherControlMessage : N3Message
	{
		[AoMember(1)]
		public short FadeIn { get; set; }

		[AoMember(2)]
		public int Duration { get; set; }

		[AoMember(3)]
		public short FadeOut { get; set; }

		[AoMember(4)]
		public float Range { get; set; }

		[AoMember(5)]
		public byte WeatherType { get; set; }

		[AoMember(6)]
		public byte WeatherIntensity { get; set; }

		[AoMember(7)]
		public byte Wind { get; set; }

		[AoMember(8)]
		public byte Clouds { get; set; }

		[AoMember(9)]
		public byte Thunderstrikes { get; set; }

		[AoMember(10)]
		public byte Tremors { get; set; }

		[AoMember(11)]
		public byte TremorPercentage { get; set; }

		[AoMember(12)]
		public byte ThunderstrikePercentage { get; set; }

		[AoMember(13)]
		public byte CloudColorRed { get; set; }

		[AoMember(14)]
		public byte CloudColorGreen { get; set; }

		[AoMember(15)]
		public byte CloudColorBlue { get; set; }

		[AoMember(16)]
		public byte FogColorRed { get; set; }

		[AoMember(17)]
		public byte FogColorGreen { get; set; }

		[AoMember(18)]
		public byte FogColorBlue { get; set; }

		[AoMember(19)]
		public byte ZBufferVisibility { get; set; }

		[AoMember(20)]
		public Vector3 Position { get; set; }

		[AoMember(21)]
		public float UnknownSingle { get; set; }

		public WeatherControlMessage()
		{
			base.N3MessageType = N3MessageType.WeatherControl;
		}
	}
	[AoContract(1851741806)]
	public class SetStatMessage : N3Message
	{
		[AoMember(0)]
		public int Value { get; set; }

		[AoMember(1)]
		public CharacterStat Stat { get; set; }

		public SetStatMessage(CharacterStat stat, int value)
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
}
namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages.OrgServerMessages
{
	[AoContract(6)]
	public class OrgContractMessage : OrgServerMessage
	{
		[AoMember(1)]
		public short Quality { get; set; }

		[AoMember(2)]
		public int ItemLowId { get; set; }

		[AoMember(3)]
		public int ItemHighId { get; set; }

		[AoMember(4)]
		public byte Active { get; set; }

		public OrgContractMessage()
		{
			base.OrgServerMessageType = OrgServerMessageType.OrgContract;
		}
	}
	[AoContract(2)]
	public class OrgInfoMessage : OrgServerMessage
	{
		[AoMember(0, SerializeSize = ArraySizeType.Int16)]
		public string Description { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int16)]
		public string Objective { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.Int16)]
		public string History { get; set; }

		[AoMember(3, SerializeSize = ArraySizeType.Int16)]
		public string GoverningForm { get; set; }

		[AoMember(4, SerializeSize = ArraySizeType.Int16)]
		public string LeaderName { get; set; }

		[AoMember(5, SerializeSize = ArraySizeType.Int16)]
		public string Rank { get; set; }

		[AoMember(6, SerializeSize = ArraySizeType.X3F1)]
		public object[] Unknown3 { get; set; }

		public OrgInfoMessage()
		{
			base.OrgServerMessageType = OrgServerMessageType.OrgInfo;
		}
	}
	[AoContract(5)]
	public class OrgInviteMessage : OrgServerMessage
	{
		[AoMember(0)]
		public int Unknown3 { get; set; }

		public OrgInviteMessage()
		{
			base.OrgServerMessageType = OrgServerMessageType.OrgInvite;
		}
	}
}
namespace SmokeLounge.AOtomation.Messaging.GameData
{
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
	[Flags]
	public enum CharacterFlags
	{
		None = 0,
		HasVisibleName = 0x400000,
		HasBlueName = 0x800000
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
	public enum CharacterStatus
	{
		Active = 1
	}
	public class ChatMessage
	{
		[AoMember(0, SerializeSize = ArraySizeType.Int16)]
		public string Text { get; set; }

		[AoMember(1)]
		public ChatMessageType Type { get; set; }
	}
	public enum ChatMessageType : byte
	{
		Say,
		Whisper,
		Shout
	}
	public enum Fatness
	{
		Thin,
		Normal,
		Fat
	}
	public class FightModeUpdateEntry
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2, SerializeSize = ArraySizeType.Int16)]
		public string Name { get; set; }

		[AoMember(3)]
		public byte Unknown2 { get; set; }

		[AoMember(4)]
		public byte Unknown3 { get; set; }
	}
	public class FollowCoordinateInfo : FollowInfo
	{
		private byte followInfoType = 1;

		[AoMember(0)]
		public byte FollowInfoType
		{
			get
			{
				return followInfoType;
			}
			set
			{
				followInfoType = value;
			}
		}

		[AoMember(1)]
		public byte MoveMode { get; set; }

		[AoMember(2)]
		public byte CoordinateCount { get; set; }

		[AoMember(3)]
		public Vector3 CurrentCoordinates { get; set; }

		[AoMember(4)]
		public Vector3 EndCoordinates { get; set; }

		public List<Vector3> Coordinates { get; set; }

		public FollowCoordinateInfo()
		{
			Coordinates = new List<Vector3>();
		}
	}
	public class FollowPositionInfo : FollowInfo
	{
		private byte followInfoType = 2;

		public byte FollowInfoType
		{
			get
			{
				return followInfoType;
			}
			set
			{
				followInfoType = value;
			}
		}

		public byte MoveType { get; set; }

		public int Unknown1 { get; set; }

		public int Unknown2 { get; set; }

		public int Unknown3 { get; set; }

		public Vector3 Coordinates { get; set; }

		public byte Unknown4 { get; set; }

		public FollowPositionInfo()
		{
			MoveType = 25;
			Unknown2 = 64;
		}
	}
	public class FollowInfo
	{
	}
	public class FollowStopInfo : FollowInfo
	{
		private byte followInfoType = 2;

		public byte FollowInfoType
		{
			get
			{
				return followInfoType;
			}
			set
			{
				followInfoType = value;
			}
		}

		public byte MoveType { get; set; }

		public int Unknown1 { get; set; }

		public int Unknown2 { get; set; }

		public int Unknown3 { get; set; }

		public Vector3 Coordinates { get; set; }

		public byte Flag { get; set; }

		public Vector3 ConfirmCoordinates { get; set; }

		public FollowStopInfo()
		{
			MoveType = 21;
			Flag = 1;
		}
	}
	public class FollowTargetInfo : FollowInfo
	{
		private byte followInfoType = 2;

		[AoMember(0)]
		public byte FollowInfoType
		{
			get
			{
				return followInfoType;
			}
			set
			{
				followInfoType = value;
			}
		}

		[AoMember(1)]
		public byte MoveType { get; set; }

		[AoMember(2)]
		public Identity Target { get; set; }

		[AoMember(3)]
		public byte Dummy { get; set; }

		[AoMember(4)]
		public int Dummy1 { get; set; }

		[AoMember(5)]
		public float X { get; set; }

		[AoMember(6)]
		public float Y { get; set; }

		[AoMember(7)]
		public float Z { get; set; }

		public FollowTargetInfo()
		{
			MoveType = 0;
		}
	}
	public class FullCharacterSub
	{
		[AoMember(1)]
		public byte Unknown1 { get; set; }

		[AoMember(2)]
		public byte Unknown2 { get; set; }

		[AoMember(3)]
		public byte Unknown3 { get; set; }
	}
	public class FullCharacterSub2
	{
		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public Identity Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		[AoMember(4)]
		public int Unknown4 { get; set; }
	}
	public class GameTuple<T1, T2>
	{
		[AoMember(0)]
		public T1 Value1 { get; set; }

		[AoMember(1)]
		public T2 Value2 { get; set; }
	}
	public class CharacterInfo
	{
		[AoMember(0)]
		public Identity MissionIdentity { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.NullTerminated)]
		public string Name { get; set; }
	}
	public abstract class InfoPacket
	{
	}
	public class InventoryEntry
	{
		[AoMember(0)]
		public int Slotnumber { get; set; }

		[AoMember(1)]
		public short UnknownFlags { get; set; }

		[AoMember(2)]
		public short Unknown1 { get; set; }

		[AoMember(3)]
		public Identity Identity { get; set; }

		[AoMember(4)]
		public int LowId { get; set; }

		[AoMember(5)]
		public int HighId { get; set; }

		[AoMember(6)]
		public int Quality { get; set; }

		[AoMember(7)]
		public int Unknown2 { get; set; }
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
	public enum CharacterStat
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
		LevelNCUCost = 54,
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
		StrainOmniTokens = 75,
		EquipmentPage = 76,
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
		ExternalPlayfieldInstance = 192,
		ExternalDoorInstance = 193,
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
		WeaponRange = 380,
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
		IsFightingMe = 410,
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
	public enum Gender
	{
		None,
		Neutral,
		Male,
		Female
	}
	public struct Identity
	{
		public static readonly Identity None = new Identity
		{
			Type = IdentityType.None,
			Instance = 0
		};

		[AoMember(0)]
		public IdentityType Type { get; set; }

		[AoMember(1)]
		public int Instance { get; set; }

		public static bool operator ==(Identity identity1, Identity identity2)
		{
			return identity1.Equals(identity2);
		}

		public static bool operator !=(Identity identity1, Identity identity2)
		{
			return !identity1.Equals(identity2);
		}

		public override bool Equals(object obj)
		{
			if (obj is Identity && Type.Equals(((Identity)obj).Type))
			{
				return Instance.Equals(((Identity)obj).Instance);
			}
			return false;
		}

		public override int GetHashCode()
		{
			int num = 17;
			num = 23 * num + Type.GetHashCode();
			return 23 * num + Instance.GetHashCode();
		}

		public ulong Long()
		{
			ulong num = (ulong)Type;
			num <<= 32;
			return num | (uint)Instance;
		}

		public override string ToString()
		{
			return $"{Type}:{Instance}";
		}

		public string ToString(bool asHex)
		{
			if (asHex)
			{
				return string.Format("{0}:{1}", ((int)Type).ToString("X8"), Instance.ToString("X8"));
			}
			return $"{(int)Type}:{Instance}";
		}
	}
	public enum IdentityType
	{
		None = 0,
		WeaponPage = 101,
		ArmorPage = 102,
		ImplantPage = 103,
		Inventory = 104,
		Bank = 105,
		Backpack = 107,
		KnuBotTradeWindow = 108,
		OverflowWindow = 110,
		TradeWindow = 111,
		SocialPage = 115,
		ShopInventory = 1895,
		PlayerShopInventory = 1936,
		Playfield2 = 40016,
		CanbeAffected = 50000,
		CityController = 50200,
		Terminal = 51005,
		Door = 51016,
		Container = 51017,
		WeaponInstance = 51018,
		VendingMachine = 51035,
		TempBag = 51047,
		Corpse = 51050,
		MailTerminal = 51059,
		Playfield1 = 51100,
		Playfield = 51101,
		NanoProgram = 53019,
		GfxEffect = 53030,
		SpecialAction = 57008,
		MissionEntrance = 56006,
		MissionTerminal = 56481,
		TeamWindow = 57001,
		Organization = 57002,
		IncomingTradeWindow = 57005,
		Playfield3 = 100001
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
	public class ActiveNano
	{
		[AoMember(0)]
		public int NanoId { get; set; }

		[AoMember(1)]
		public int NanoInstance { get; set; }

		[AoMember(2)]
		public int Time1 { get; set; }

		[AoMember(3)]
		public int Time2 { get; set; }
	}
	public class MonsterInfoPacket : InfoPacket
	{
		[AoMember(0)]
		public byte Unknown1 { get; set; }

		[AoMember(1)]
		public byte Profession { get; set; }

		[AoMember(2)]
		public byte Level { get; set; }

		[AoMember(3)]
		public byte TitleLevel { get; set; }

		[AoMember(4)]
		public byte VisualProfession { get; set; }

		[AoMember(5)]
		public short Unknown2 { get; set; }

		[AoMember(6)]
		public int CurrentHealth { get; set; }

		[AoMember(7)]
		public int MaxHealth { get; set; }

		[AoMember(8)]
		public int Unknown3 { get; set; }

		[AoMember(9)]
		public int OrganizationId { get; set; }

		[AoMember(10)]
		public short Unknown4 { get; set; }

		[AoMember(11)]
		public short Unknown5 { get; set; }

		[AoMember(12)]
		public short Unknown6 { get; set; }

		[AoMember(13)]
		public short Unknown7 { get; set; }

		[AoMember(14)]
		public int Unknown8 { get; set; }

		[AoMember(15)]
		public int Unknown9 { get; set; }

		[AoMember(16)]
		public int Unknown10 { get; set; }
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
	public class MissionItemReward
	{
		[AoMember(0)]
		public int LowId { get; set; }

		[AoMember(1)]
		public int HighId { get; set; }

		[AoMember(2)]
		public int Ql { get; set; }

		[AoMember(3)]
		public int Unknown { get; set; }
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
	public enum Profession
	{
		None,
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
		Nanotechnician,
		Metaphysicist,
		Monster,
		Keeper,
		Shade
	}
	public class Quaternion
	{
		[AoMember(0)]
		public float X { get; set; }

		[AoMember(1)]
		public float Y { get; set; }

		[AoMember(2)]
		public float Z { get; set; }

		[AoMember(3)]
		public float W { get; set; }
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

		[AoMember(19)]
		public int Unknown14 { get; set; }

		[AoMember(20)]
		public int Unknown15 { get; set; }

		[AoMember(21)]
		public int Unknown16 { get; set; }

		[AoMember(22)]
		public int Unknown17 { get; set; }

		[AoMember(23)]
		public int Unknown18 { get; set; }

		[AoMember(24)]
		public Identity UnknownId2 { get; set; }

		[AoMember(25)]
		public int MissionIconId { get; set; }

		[AoMember(26)]
		public int Unknown20 { get; set; }

		[AoMember(27)]
		public int Unknown21 { get; set; }

		[AoMember(28, SerializeSize = ArraySizeType.X3F1)]
		public QuestActionInfo[] QuestActions { get; set; }

		[AoMember(29, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] PlayerIds { get; set; }

		[AoMember(30, SerializeSize = ArraySizeType.Int32)]
		public int[] UnknownArray1 { get; set; }

		[AoMember(31, SerializeSize = ArraySizeType.Int32)]
		public int[] UnknownArray2 { get; set; }

		[AoMember(32, SerializeSize = ArraySizeType.Int32)]
		public CharacterInfo[] CharacterInfos { get; set; }

		[AoMember(33)]
		public int Unknown22 { get; set; }

		[AoMember(34, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] PlayerIds2 { get; set; }

		[AoMember(35)]
		public int Unknown23 { get; set; }

		[AoMember(36)]
		public int Unknown24 { get; set; }

		[AoMember(37)]
		public Identity UnknownId3 { get; set; }

		[AoMember(38)]
		public int Unknown25 { get; set; }

		[AoMember(39)]
		public int Unknown26 { get; set; }

		[AoMember(40, SerializeSize = ArraySizeType.Int32)]
		public QuestIdentity[] QuestIdentities { get; set; }

		[AoMember(41)]
		public int Unknown27 { get; set; }

		[AoMember(42, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] FactionInfos { get; set; }

		[AoMember(43)]
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
	public class QuestActionList
	{
		[AoMember(0)]
		public int Version { get; set; }

		[AoMember(1)]
		public Identity Action { get; set; }

		[AoMember(2)]
		public Identity Unknown1 { get; set; }

		[AoMember(3)]
		public Identity Unknown2 { get; set; }

		[AoMember(4)]
		public Identity Unknown3 { get; set; }

		[AoMember(5)]
		public Identity Unknown4 { get; set; }

		[AoMember(6)]
		public float Unknown5 { get; set; }

		[AoMember(7)]
		public float Unknown6 { get; set; }

		[AoMember(8)]
		public float Unknown7 { get; set; }

		[AoMember(9)]
		public float Unknown8 { get; set; }

		[AoMember(10)]
		public Identity Unknown9 { get; set; }

		[AoMember(11)]
		public float Unknown10 { get; set; }

		[AoMember(12)]
		public float Unknown11 { get; set; }

		[AoMember(13)]
		public float Unknown12 { get; set; }

		[AoMember(14)]
		public float Unknown13 { get; set; }

		[AoMember(15)]
		public Identity Unknown14 { get; set; }

		[AoMember(16)]
		public int UnknownHash15 { get; set; }

		[AoMember(17)]
		public int Unknown16 { get; set; }

		[AoMember(18)]
		public Identity Unknown17 { get; set; }

		[AoMember(19)]
		public Identity Playfield { get; set; }

		[AoMember(20)]
		public int Unknown18 { get; set; }

		[AoMember(21)]
		public int Unknown19 { get; set; }

		[AoMember(22)]
		public float X { get; set; }

		[AoMember(23)]
		public float Y { get; set; }

		[AoMember(24)]
		public float Z { get; set; }
	}
	public class QuestCharInfo
	{
		[AoMember(0)]
		public Identity CharacteIdentity { get; set; }

		[AoMember(1, SerializeSize = ArraySizeType.Int32)]
		public string CharacterName { get; set; }
	}
	public class QuestFaction
	{
		[AoMember(0)]
		public int Unknown1 { get; set; }

		[AoMember(1)]
		public int Unknown2 { get; set; }
	}
	public class QuestIdentity
	{
		[AoMember(0)]
		public Identity Unknown1 { get; set; }

		[AoMember(1)]
		public int Unknown2 { get; set; }
	}
	public class QuestInfo
	{
		[AoMember(0)]
		public Identity QuestIdentity { get; set; }

		[AoMember(1)]
		public int Unknown1 { get; set; }

		[AoMember(2)]
		public int Unknown2 { get; set; }

		[AoMember(3)]
		public int Unknown3 { get; set; }

		[AoMember(4)]
		public int Unknown4 { get; set; }

		[AoMember(5, SerializeSize = ArraySizeType.NoSerialization, FixedSizeLength = 32)]
		public string ShortInfo { get; set; }

		[AoMember(6, SerializeSize = ArraySizeType.Int32)]
		public string Info { get; set; }

		[AoMember(7)]
		public Identity Unknown5 { get; set; }

		[AoMember(8)]
		public int RewardDescriptorVersion { get; set; }

		[AoMember(9)]
		public int CashReward { get; set; }

		[AoMember(10)]
		public int Unknown6 { get; set; }

		[AoMember(11)]
		public int ExperienceReward { get; set; }

		[AoMember(12, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] UnknownIdentities1 { get; set; }

		[AoMember(13, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] UnknownIdentities2 { get; set; }

		[AoMember(14, SerializeSize = ArraySizeType.X3F1)]
		public QuestItemShort[] ItemRewards { get; set; }

		[AoMember(15)]
		public int Unknown7 { get; set; }

		[AoMember(16)]
		public int Unknown8 { get; set; }

		[AoMember(17)]
		public int Unknown9 { get; set; }

		[AoMember(18)]
		public int UnknownHash { get; set; }

		[AoMember(19)]
		public int Quality { get; set; }

		[AoMember(20)]
		public int Unknown10 { get; set; }

		[AoMember(21)]
		public int Unknown11 { get; set; }

		[AoMember(22)]
		public int Unknown12 { get; set; }

		[AoMember(23)]
		public int Unknown13 { get; set; }

		[AoMember(24)]
		public Identity Unknown14 { get; set; }

		[AoMember(25)]
		public int MissionIconId { get; set; }

		[AoMember(26)]
		public int Unknown15 { get; set; }

		[AoMember(27)]
		public int Unknown16 { get; set; }

		[AoMember(28, SerializeSize = ArraySizeType.X3F1)]
		public QuestActionList[] QuestActions { get; set; }

		[AoMember(29, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] Unknown17 { get; set; }

		[AoMember(30, SerializeSize = ArraySizeType.Int32)]
		public int[] Unknown18 { get; set; }

		[AoMember(31, SerializeSize = ArraySizeType.Int32)]
		public int[] Unknown19 { get; set; }

		[AoMember(32, SerializeSize = ArraySizeType.Int32)]
		public QuestCharInfo[] CharInfos { get; set; }

		[AoMember(33)]
		public int Unknown20 { get; set; }

		[AoMember(34, SerializeSize = ArraySizeType.X3F1)]
		public Identity[] UnknownIdentities20 { get; set; }

		[AoMember(35)]
		public int Unknown21 { get; set; }

		[AoMember(36)]
		public int Unknown22 { get; set; }

		[AoMember(37)]
		public Identity Unknown23 { get; set; }

		[AoMember(38)]
		public int Unknown24 { get; set; }

		[AoMember(39)]
		public int Unknown25 { get; set; }

		[AoMember(40, SerializeSize = ArraySizeType.Int32)]
		public QuestIdentity[] QuestIdentities { get; set; }

		[AoMember(41)]
		public int Unknown26 { get; set; }

		[AoMember(42, SerializeSize = ArraySizeType.X3F1)]
		public QuestFaction[] FactionInfo { get; set; }

		[AoMember(43)]
		public byte Unknown27 { get; set; }
	}
	public class QuestItemShort
	{
		[AoMember(0)]
		public int LowId { get; set; }

		[AoMember(1)]
		public int HighId { get; set; }

		[AoMember(2)]
		public int Quality { get; set; }

		[AoMember(3)]
		public int Unknown1 { get; set; }
	}
	public class ResearchUpdateEntry
	{
		[AoMember(1)]
		public int ResearchId { get; set; }

		[AoMember(2)]
		public int Unknown1 { get; set; }

		[AoMember(3)]
		public int Unknown2 { get; set; }

		[AoMember(4)]
		public int Unknown3 { get; set; }
	}
	public enum Side
	{
		Neutral,
		Clan,
		Omni,
		Monster,
		Advisor,
		Guardian,
		Gm,
		Mixed
	}
	public abstract class SimpleCharacterInfo
	{
	}
	public class SimpleNpcInfo : SimpleCharacterInfo
	{
		[AoMember(0)]
		public short Family { get; set; }

		[AoMember(1)]
		public short LosHeight { get; set; }

		public short UnknownData { get; set; }

		public short UnknownData2 { get; set; }

		public byte UnknownData3 { get; set; }
	}
	public class SimplePcInfo : SimpleCharacterInfo
	{
		[AoMember(0)]
		public uint CurrentNano { get; set; }

		[AoMember(1)]
		public int Team { get; set; }

		[AoMember(2)]
		public short Swim { get; set; }

		[AoMember(3)]
		public short StrengthBase { get; set; }

		[AoMember(4)]
		public short AgilityBase { get; set; }

		[AoMember(5)]
		public short StaminaBase { get; set; }

		[AoMember(6)]
		public short IntelligenceBase { get; set; }

		[AoMember(7)]
		public short SenseBase { get; set; }

		[AoMember(8)]
		public short PsychicBase { get; set; }

		[AoMember(9, SerializeSize = ArraySizeType.Int16)]
		public string FirstName { get; set; }

		[AoMember(10, SerializeSize = ArraySizeType.Int16)]
		public string LastName { get; set; }

		[AoMember(11, SerializeSize = ArraySizeType.Int16)]
		public string OrgName { get; set; }
	}
	public class SpecialAttack
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
		public byte? Unknown13 { get; set; }

		[AoMember(22)]
		public int Unknown14 { get; set; }

		[AoMember(23)]
		public int Unknown15 { get; set; }

		[AoMember(24)]
		public int Unknown16 { get; set; }
	}
	public class TowerProxyBase
	{
		[AoMember(1)]
		public Identity TowerFieldIdentity { get; set; }

		[AoMember(2)]
		public Identity OwnerIdentity { get; set; }

		[AoMember(3)]
		public Vector3 Coordinates { get; set; }

		[AoMember(4)]
		public int Unknown1 { get; set; }

		[AoMember(5)]
		public int Unknown2 { get; set; }

		[AoMember(6)]
		public int Unknown3 { get; set; }

		[AoMember(7)]
		public float Unknown4 { get; set; }

		[AoMember(8)]
		public int Unknown5 { get; set; }
	}
	public class Vector3
	{
		[AoMember(0)]
		public float X { get; set; }

		[AoMember(1)]
		public float Y { get; set; }

		[AoMember(2)]
		public float Z { get; set; }

		public override string ToString()
		{
			return $"{X} {Y} {Z}";
		}

		public Vector3()
		{
		}

		public Vector3(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}
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
	public enum WeatherType : byte
	{
		Rain,
		Fog,
		SetTemperature,
		Quake,
		SandStorm,
		AshStorm,
		RedFalloutStorm,
		GreenFalloutStorm
	}
}
You are not using the latest version of the tool, please update.
Latest version is '10.1.1.8388' (yours is '8.2.0.7535-95108c96')
