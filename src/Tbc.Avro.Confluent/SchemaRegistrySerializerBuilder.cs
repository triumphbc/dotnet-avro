using Tbc.Avro.Representation;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tbc.Avro.Confluent
{
    /// <summary>
    /// Builds <see cref="T:ISerializer{T}" />s that are locked into specific schemas from a Schema
    /// Registry instance.
    /// </summary>
    public class SchemaRegistrySerializerBuilder : IDisposable
    {
        /// <summary>
        /// The client to use for Schema Registry operations.
        /// </summary>
        public ISchemaRegistryClient RegistryClient { get; }

        /// <summary>
        /// The schema builder used to create a schema for a C# type when registering automatically.
        /// </summary>
        public Abstract.ISchemaBuilder SchemaBuilder { get; }

        /// <summary>
        /// The JSON schema reader used to convert schemas received from the registry into abstract
        /// representations.
        /// </summary>
        public IJsonSchemaReader SchemaReader { get; }

        /// <summary>
        /// The JSON schema writer used to convert abstract schema representations when registering
        /// automatically.
        /// </summary>
        public IJsonSchemaWriter SchemaWriter { get; }

        /// <summary>
        /// The deserializer builder to use to build serialization functions for C# types.
        /// </summary>
        public Serialization.IBinarySerializerBuilder SerializerBuilder { get; }

        private readonly bool _disposeRegistryClient;

        /// <summary>
        /// Creates a serializer builder.
        /// </summary>
        /// <param name="registryConfiguration">
        /// Schema Registry configuration. Using the <see cref="SchemaRegistryConfig" /> class is
        /// highly recommended.
        /// </param>
        /// <param name="schemaBuilder">
        /// The schema builder to use to create a schema for a C# type when registering automatically.
        /// If none is provided, the default schema builder will be used.
        /// </param>
        /// <param name="schemaReader">
        /// The JSON schema reader to use to convert schemas received from the registry into abstract
        /// representations. If none is provided, the default schema reader will be used.
        /// </param>
        /// <param name="schemaWriter">
        /// The JSON schema writer to use to convert abstract schema representations when registering
        /// automatically. If none is provided, the default schema writer will be used.
        /// </param>
        /// <param name="serializerBuilder">
        /// The deserializer builder to use to build serialization functions for C# types. If none
        /// is provided, the default serializer builder will be used.
        /// </param>
        public SchemaRegistrySerializerBuilder(
            IEnumerable<KeyValuePair<string, string>> registryConfiguration,
            Abstract.ISchemaBuilder? schemaBuilder = null,
            IJsonSchemaReader? schemaReader = null,
            IJsonSchemaWriter? schemaWriter = null,
            Serialization.IBinarySerializerBuilder? serializerBuilder = null
        ) : this(
            new CachedSchemaRegistryClient(registryConfiguration),
            schemaBuilder,
            schemaReader,
            schemaWriter,
            serializerBuilder
        ) {
            _disposeRegistryClient = true;
        }

        /// <summary>
        /// Creates a serializer builder.
        /// </summary>
        /// <param name="registryClient">
        /// The client to use for Schema Registry operations. (The client will not be disposed.)
        /// </param>
        /// <param name="schemaBuilder">
        /// The schema builder to use to create a schema for a C# type when registering automatically.
        /// If none is provided, the default schema builder will be used.
        /// </param>
        /// <param name="schemaReader">
        /// The JSON schema reader to use to convert schemas received from the registry into abstract
        /// representations. If none is provided, the default schema reader will be used.
        /// </param>
        /// <param name="schemaWriter">
        /// The JSON schema writer to use to convert abstract schema representations when registering
        /// automatically. If none is provided, the default schema writer will be used.
        /// </param>
        /// <param name="serializerBuilder">
        /// The deserializer builder to use to build serialization functions for C# types. If none
        /// is provided, the default serializer builder will be used.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the registry client is null.
        /// </exception>
        public SchemaRegistrySerializerBuilder(
            ISchemaRegistryClient registryClient,
            Abstract.ISchemaBuilder? schemaBuilder = null,
            IJsonSchemaReader? schemaReader = null,
            IJsonSchemaWriter? schemaWriter = null,
            Serialization.IBinarySerializerBuilder? serializerBuilder = null
        ) {

            _disposeRegistryClient = false;

            RegistryClient = registryClient ?? throw new ArgumentNullException(nameof(registryClient));
            SchemaBuilder = schemaBuilder ?? new Abstract.SchemaBuilder();
            SchemaReader = schemaReader ?? new JsonSchemaReader();
            SchemaWriter = schemaWriter ?? new JsonSchemaWriter();
            SerializerBuilder = serializerBuilder ?? new Serialization.BinarySerializerBuilder();
        }

        /// <summary>
        /// Builds a serializer for a specific schema.
        /// </summary>
        /// <param name="id">
        /// The ID of the schema that should be used to serialize data.
        /// </param>
        /// <exception cref="AggregateException">
        /// Thrown when the type is incompatible with the retrieved schema.
        /// </exception>
        public async Task<ISerializer<T>> Build<T>(int id)
        {
            return Build<T>(id, await RegistryClient.GetSchemaAsync(id));
        }

        /// <summary>
        /// Builds a serializer for a specific schema.
        /// </summary>
        /// <param name="subject">
        /// The subject of the schema that should be used to serialize data. The latest version of
        /// the subject will be resolved.
        /// </param>
        /// <param name="registerAutomatically">
        /// Whether to automatically register a schema that matches <typeparamref name="T" /> if
        /// one does not already exist.
        /// </param>
        /// <exception cref="AggregateException">
        /// Thrown when the type is incompatible with the retrieved schema or a matching schema
        /// cannot be generated.
        /// </exception>
        public async Task<ISerializer<T>> Build<T>(string subject, bool registerAutomatically = false)
        {
            try
            {
                var schema = await RegistryClient.GetLatestSchemaAsync(subject);

                return Build<T>(schema.Id, schema.SchemaString);
            }
            catch (Exception e) when (registerAutomatically && (
                (e is SchemaRegistryException sre && sre.ErrorCode == 40401) ||
                (e is AggregateException a && a.InnerExceptions.All(i =>
                    i is UnsupportedSchemaException ||
                    i is UnsupportedTypeException
                ))
            ))
            {
                var schema = SchemaBuilder.BuildSchema<T>();
                var json = SchemaWriter.Write(schema);

                var id = await RegistryClient.RegisterSchemaAsync(subject, json);

                return Build<T>(id, json);
            }
        }

        /// <summary>
        /// Builds a serializer for a specific schema.
        /// </summary>
        /// <param name="subject">
        /// The subject of the schema that should be used to serialize data.
        /// </param>
        /// <param name="version">
        /// The version of the subject to be resolved.
        /// </param>
        /// <exception cref="AggregateException">
        /// Thrown when the type is incompatible with the retrieved schema.
        /// </exception>
        public virtual async Task<ISerializer<T>> Build<T>(string subject, int version)
        {
            var schema = await RegistryClient.GetSchemaAsync(subject, version);
            var id = await RegistryClient.GetSchemaIdAsync(subject, schema);

            return Build<T>(id, schema);
        }

        /// <summary>
        /// Disposes the builder, freeing up any resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the builder, freeing up any resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_disposeRegistryClient)
                {
                    RegistryClient.Dispose();
                }
            }
        }

        /// <summary>
        /// Builds a serializer for the Confluent wire format.
        /// </summary>
        /// <param name="id">
        /// A schema ID to include in each serialized payload.
        /// </param>
        /// <param name="schema">
        /// The schema to build the Avro serializer from.
        /// </param>
        protected virtual ISerializer<T> Build<T>(int id, string schema)
        {
            var bytes = BitConverter.GetBytes(id);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            var serialize = SerializerBuilder.BuildDelegate<T>(SchemaReader.Read(schema));

            return new DelegateSerializer<T>((data, stream) =>
            {
                stream.WriteByte(0x00);
                stream.Write(bytes, 0, bytes.Length);

                serialize(data, stream);
            });
        }
    }
}
