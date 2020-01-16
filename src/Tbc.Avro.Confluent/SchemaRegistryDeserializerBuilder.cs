using Tbc.Avro.Representation;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Tbc.Avro.Confluent
{
    /// <summary>
    /// Builds <see cref="T:IDeserializer{T}" />s that are locked into specific schemas from a
    /// Schema Registry instance.
    /// </summary>
    public class SchemaRegistryDeserializerBuilder : IDisposable
    {
        /// <summary>
        /// The deserializer builder used to generate deserialization functions for C# types.
        /// </summary>
        public Serialization.IBinaryDeserializerBuilder DeserializerBuilder { get; }

        /// <summary>
        /// The client used for Schema Registry operations.
        /// </summary>
        public ISchemaRegistryClient RegistryClient { get; }

        /// <summary>
        /// The JSON schema reader used to convert schemas received from the registry into abstract
        /// representations.
        /// </summary>
        public IJsonSchemaReader SchemaReader { get; }

        private readonly bool _disposeRegistryClient;

        /// <summary>
        /// Creates a deserializer builder.
        /// </summary>
        /// <param name="registryConfiguration">
        /// Schema Registry configuration. Using the <see cref="SchemaRegistryConfig" /> class is
        /// highly recommended.
        /// </param>
        /// <param name="deserializerBuilder">
        /// The deserializer builder to use to to generate deserialization functions for C# types.
        /// If none is provided, the default deserializer builder will be used.
        /// </param>
        /// <param name="schemaReader">
        /// The JSON schema reader to use to convert schemas received from the registry into abstract
        /// representations. If none is provided, the default schema reader will be used.
        /// </param>
        public SchemaRegistryDeserializerBuilder(
            IEnumerable<KeyValuePair<string, string>> registryConfiguration,
            Serialization.IBinaryDeserializerBuilder? deserializerBuilder = null,
            IJsonSchemaReader? schemaReader = null
        ) : this(
            new CachedSchemaRegistryClient(registryConfiguration),
            deserializerBuilder,
            schemaReader
        ) {
            _disposeRegistryClient = true;
        }

        /// <summary>
        /// Creates a deserializer builder.
        /// </summary>
        /// <param name="registryClient">
        /// The client to use for Schema Registry operations. (The client will not be disposed.)
        /// </param>
        /// <param name="deserializerBuilder">
        /// The deserializer builder to use to to generate deserialization functions for C# types.
        /// If none is provided, the default deserializer builder will be used.
        /// </param>
        /// <param name="schemaReader">
        /// The JSON schema reader to use to convert schemas received from the registry into abstract
        /// representations. If none is provided, the default schema reader will be used.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the registry client is null.
        /// </exception>
        public SchemaRegistryDeserializerBuilder(
            ISchemaRegistryClient registryClient,
            Serialization.IBinaryDeserializerBuilder? deserializerBuilder = null,
            IJsonSchemaReader? schemaReader = null
        ) {
            _disposeRegistryClient = false;

            DeserializerBuilder = deserializerBuilder ?? new Serialization.BinaryDeserializerBuilder();
            RegistryClient = registryClient ?? throw new ArgumentNullException(nameof(registryClient));
            SchemaReader = schemaReader ?? new JsonSchemaReader();
        }

        /// <summary>
        /// Builds a deserializer for a specific schema.
        /// </summary>
        /// <param name="id">
        /// The ID of the schema that should be used to deserialize data.
        /// </param>
        /// <exception cref="AggregateException">
        /// Thrown when the type is incompatible with the retrieved schema.
        /// </exception>
        public virtual async Task<IDeserializer<T>> Build<T>(int id)
        {
            return Build<T>(id, await RegistryClient.GetSchemaAsync(id));
        }

        /// <summary>
        /// Builds a deserializer for a specific schema.
        /// </summary>
        /// <param name="subject">
        /// The subject of the schema that should be used to deserialize data. The latest version
        /// of the subject will be resolved.
        /// </param>
        /// <exception cref="AggregateException">
        /// Thrown when the type is incompatible with the retrieved schema.
        /// </exception>
        public virtual async Task<IDeserializer<T>> Build<T>(string subject)
        {
            var schema = await RegistryClient.GetLatestSchemaAsync(subject);

            return Build<T>(schema.Id, schema.SchemaString);
        }

        /// <summary>
        /// Builds a deserializer for a specific schema.
        /// </summary>
        /// <param name="subject">
        /// The subject of the schema that should be used to deserialize data.
        /// </param>
        /// <param name="version">
        /// The version of the subject to be resolved.
        /// </param>
        /// <exception cref="AggregateException">
        /// Thrown when the type is incompatible with the retrieved schema.
        /// </exception>
        public virtual async Task<IDeserializer<T>> Build<T>(string subject, int version)
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
        /// Builds a deserializer for the Confluent wire format.
        /// </summary>
        /// <param name="id">
        /// A schema ID that all payloads must be serialized with. If a received schema ID does not
        /// match this ID, <see cref="InvalidDataException" /> will be thrown.
        /// </param>
        /// <param name="schema">
        /// The schema to build the Avro deserializer from.
        /// </param>
        protected virtual IDeserializer<T> Build<T>(int id, string schema)
        {
            var deserialize = DeserializerBuilder.BuildDelegate<T>(SchemaReader.Read(schema));

            return new DelegateDeserializer<T>(stream =>
            {
                var bytes = new byte[4];

                if (stream.ReadByte() != 0x00 || stream.Read(bytes, 0, bytes.Length) != bytes.Length)
                {
                    throw new InvalidDataException("Data does not conform to the Confluent wire format.");
                }

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                var received = BitConverter.ToInt32(bytes, 0);

                if (received != id)
                {
                    throw new InvalidDataException($"The received schema ({received}) does not match the specified schema ({id}).");
                }

                return deserialize(stream);
            });
        }
    }
}
