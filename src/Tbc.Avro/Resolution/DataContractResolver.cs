using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Tbc.Avro.Resolution
{
    /// <summary>
    /// A type resolver that extends <see cref="ReflectionResolver" /> with support for
    /// <see cref="System.Runtime.Serialization" /> attributes.
    /// </summary>
    public class DataContractResolver : TypeResolver
    {
        /// <summary>
        /// Creates a new data contract resolver.
        /// </summary>
        /// <param name="memberVisibility">
        /// The binding flags that will be used to select fields and properties. If none are provided,
        /// public instance members will be selected by default.
        /// </param>
        /// <param name="resolveReferenceTypesAsNullable">
        /// Whether to resolve reference types as nullable.
        /// </param>
        /// <param name="resolveUnderlyingEnumTypes">
        /// Whether to resolve enum types as their underlying integral types.
        /// </param>
        public DataContractResolver(
            BindingFlags memberVisibility = BindingFlags.Public | BindingFlags.Instance,
            bool resolveReferenceTypesAsNullable = false,
            bool resolveUnderlyingEnumTypes = false
        ) : base(CreateDataContractCaseBuilders(memberVisibility, resolveUnderlyingEnumTypes), resolveReferenceTypesAsNullable) { }

        /// <summary>
        /// Creates a default list of case builders.
        /// </summary>
        /// <param name="memberVisibility">
        /// The binding flags that will be used to select fields and properties.
        /// </param>
        /// <param name="resolveUnderlyingEnumTypes">
        /// Whether to resolve enum types as their underlying integral types.
        /// </param>
        public static IEnumerable<Func<ITypeResolver, ITypeResolverCase>> CreateDataContractCaseBuilders(BindingFlags memberVisibility, bool resolveUnderlyingEnumTypes)
        {
            return new Func<ITypeResolver, ITypeResolverCase>[]
            {
                // nullables:
                resolver => new NullableResolverCase(resolver),

                // primitives:
                resolver => new BooleanResolverCase(),
                resolver => new ByteResolverCase(),
                resolver => new ByteArrayResolverCase(),
                resolver => new DecimalResolverCase(),
                resolver => new DoubleResolverCase(),
                resolver => new SingleResolverCase(),
                resolver => new Int16ResolverCase(),
                resolver => new Int32ResolverCase(),
                resolver => new Int64ResolverCase(),
                resolver => new SByteResolverCase(),
                resolver => new StringResolverCase(),
                resolver => new UInt16ResolverCase(),
                resolver => new UInt32ResolverCase(),
                resolver => new UInt64ResolverCase(),

                // enums:
                resolver => resolveUnderlyingEnumTypes
                    ? new EnumUnderlyingTypeResolverCase(resolver)
                    : new DataContractEnumResolverCase() as ITypeResolverCase,

                // dictionaries:
                resolver => new DictionaryResolverCase(),

                // enumerables:
                resolver => new EnumerableResolverCase(memberVisibility),

                // built-ins:
                resolver => new DateTimeResolverCase(),
                resolver => new GuidResolverCase(),
                resolver => new TimeSpanResolverCase(),
                resolver => new UriResolverCase(),

                // classes and structs:
                resolver => new DataContractObjectResolverCase(memberVisibility)
            };
        }
    }

    /// <summary>
    /// An <see cref="ITypeResolverCase" /> that uses <see cref="System.Runtime.Serialization" />
    /// attributes to obtain additional type information.
    /// </summary>
    public abstract class DataContractResolverCase : ReflectionResolverCase
    {
        /// <summary>
        /// Creates a name resolution for a <see cref="DataMemberAttribute" />-annotated type.
        /// </summary>
        protected virtual IdentifierResolution CreateNameResolution(MemberInfo member, DataMemberAttribute? attribute = null)
        {
            return string.IsNullOrEmpty(attribute?.Name)
                ? new IdentifierResolution(member.Name)
                : new IdentifierResolution(attribute!.Name, true);
        }

        /// <summary>
        /// Creates a name resolution for a <see cref="EnumMemberAttribute" />-annotated type.
        /// </summary>
        protected virtual IdentifierResolution CreateNameResolution(MemberInfo member, EnumMemberAttribute? attribute = null)
        {
            return string.IsNullOrEmpty(attribute?.Value)
                ? new IdentifierResolution(member.Name)
                : new IdentifierResolution(attribute!.Value, true);
        }

        /// <summary>
        /// Creates a name resolution for a <see cref="DataContractAttribute" />-annotated type.
        /// </summary>
        protected virtual IdentifierResolution CreateNameResolution(Type type, DataContractAttribute? attribute = null)
        {
            return string.IsNullOrEmpty(attribute?.Name)
                ? new IdentifierResolution(type.Name)
                : new IdentifierResolution(attribute!.Name, true);
        }

        /// <summary>
        /// Creates a namespace resolution for a <see cref="DataContractAttribute" />-annotated
        /// type.
        /// </summary>
        protected virtual IdentifierResolution? CreateNamespaceResolution(Type type, DataContractAttribute? attribute = null)
        {
            return string.IsNullOrEmpty(attribute?.Namespace)
                ? string.IsNullOrEmpty(type.Namespace)
                    ? null
                    : new IdentifierResolution(type.Namespace)
                : new IdentifierResolution(attribute!.Namespace, true);
        }
    }

    /// <summary>
    /// A type resolver case that matches <see cref="Enum" /> types, taking
    /// <see cref="System.Runtime.Serialization" /> attributes into account.
    /// </summary>
    public class DataContractEnumResolverCase : DataContractResolverCase
    {
        /// <summary>
        /// Resolves enum type information.
        /// </summary>
        /// <param name="type">
        /// The type to resolve.
        /// </param>
        /// <returns>
        /// A successful <see cref="EnumResolution" /> result if <paramref name="type" /> is an
        /// <see cref="Enum" /> type; an unsuccessful <see cref="UnsupportedTypeException" />
        /// result otherwise.
        /// </returns>
        public override ITypeResolutionResult ResolveType(Type type)
        {
            var result = new TypeResolutionResult();

            if (type.IsEnum)
            {
                var contract = GetAttribute<DataContractAttribute>(type);

                var name = CreateNameResolution(type, contract);
                var @namespace = CreateNamespaceResolution(type, contract);

                var isFlagEnum = GetAttribute<FlagsAttribute>(type) != null;

                var symbols = (contract == null
                    ? type.GetFields(BindingFlags.Public | BindingFlags.Static)
                        .Select(f => (
                            MemberInfo: f as MemberInfo,
                            Attribute: GetAttribute<NonSerializedAttribute>(f)
                        ))
                        .Where(f => f.Attribute == null)
                        .Select(f => (
                            f.MemberInfo,
                            Name: new IdentifierResolution(f.MemberInfo.Name),
                            Value: Enum.Parse(type, f.MemberInfo.Name)
                        ))
                    : type.GetFields(BindingFlags.Public | BindingFlags.Static)
                        .Select(f => (
                            MemberInfo: f as MemberInfo,
                            Attribute: GetAttribute<EnumMemberAttribute>(f)
                        ))
                        .Where(f => f.Attribute != null)
                        .Select(f => (
                            f.MemberInfo,
                            Name: CreateNameResolution(f.MemberInfo, f.Attribute),
                            Value: Enum.Parse(type, f.MemberInfo.Name)
                        )))
                    .OrderBy(f => f.Value)
                    .ThenBy(f => f.Name.Value)
                    .Select(f => new SymbolResolution(f.MemberInfo, f.Name, f.Value))
                    .ToList();

                result.TypeResolution = new EnumResolution(type, type.GetEnumUnderlyingType(), name, @namespace, isFlagEnum, symbols);
            }
            else
            {
                result.Exceptions.Add(new UnsupportedTypeException(type));
            }

            return result;
        }
    }

    /// <summary>
    /// A general type resolver case that inspects fields and properties, taking
    /// <see cref="System.Runtime.Serialization" /> attributes into account.
    /// </summary>
    public class DataContractObjectResolverCase : DataContractResolverCase
    {
        /// <summary>
        /// The binding flags that will be used to select fields and properties.
        /// </summary>
        public BindingFlags MemberVisibility { get; }

        /// <summary>
        /// Creates a new object resolver case.
        /// </summary>
        /// <param name="memberVisibility">
        /// The binding flags that will be used to select fields and properties.
        /// </param>
        public DataContractObjectResolverCase(BindingFlags memberVisibility)
        {
            MemberVisibility = memberVisibility;
        }

        /// <summary>
        /// Resolves class, interface, or struct type information.
        /// </summary>
        /// <param name="type">
        /// The type to resolve.
        /// </param>
        /// <returns>
        /// An unsuccessful <see cref="UnsupportedTypeException" /> result if <paramref name="type" />
        /// is an array or a primitive type; a successful <see cref="RecordResolution" /> result
        /// otherwise.
        /// </returns>
        public override ITypeResolutionResult ResolveType(Type type)
        {
            var result = new TypeResolutionResult();

            if (!type.IsArray && !type.IsPrimitive)
            {
                var contract = GetAttribute<DataContractAttribute>(type);

                var name = CreateNameResolution(type, contract);
                var @namespace = CreateNamespaceResolution(type, contract);

                var fields = (contract == null
                    ? GetMembers(type, MemberVisibility)
                        .Select(m => (
                            m.MemberInfo,
                            m.Type,
                            Attribute: GetAttribute<NonSerializedAttribute>(m.MemberInfo)
                        ))
                        .Where(m => m.Attribute == null)
                        .Select(m => (
                            m.MemberInfo,
                            m.Type,
                            Name: new IdentifierResolution(m.MemberInfo.Name),
                            Order: 0
                        ))
                    : GetMembers(type, MemberVisibility)
                        .Select(m => (
                            m.MemberInfo,
                            m.Type,
                            Attribute: GetAttribute<DataMemberAttribute>(m.MemberInfo)
                        ))
                        .Where(m => m.Attribute != null)
                        .Select(m => (
                            m.MemberInfo,
                            m.Type,
                            Name: CreateNameResolution(m.MemberInfo, m.Attribute),
                            m.Attribute.Order
                        )))
                    .OrderBy(m => m.Order)
                    .ThenBy(m => m.Name.Value)
                    .Select(m => new FieldResolution(m.MemberInfo, m.Type, m.Name))
                    .ToList();

                var constructors = GetConstructors(type, MemberVisibility)
                    .Select(c => new ConstructorResolution(
                        c.ConstructorInfo,
                        c.Parameters.Select(p => new ParameterResolution(p, p.ParameterType, new IdentifierResolution(p.Name))).ToList()
                    )).ToList();

                result.TypeResolution = new RecordResolution(type, name, @namespace, fields, constructors);
            }
            else
            {
                result.Exceptions.Add(new UnsupportedTypeException(type));
            }

            return result;
        }
    }
}
