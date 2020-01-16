using Tbc.Avro.Representation;
using Tbc.Avro.Serialization;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Tbc.Avro.Confluent
{
    /// <summary>
    /// An <see cref="IAsyncDeserializer{T}" /> that resolves Avro schemas on the fly. When
    /// deserializing messages, this deserializer will attempt to derive a schema ID from the first
    /// five bytes. (For more information, see the <a href="https://docs.confluent.io/current/schema-registry/docs/serializer-formatter.html#wire-format">Confluent wire format documentation</a>.)
    /// If a schema with that ID is not found in cache, it will attempt to pull down a matching
    /// schema from the Schema Registry.
    /// </summary>
    public class AsyncSchemaRegistryDeserializer<T> : IAsyncDeserializer<T>, IDisposable
    {
        /// <summary>
        /// The deserializer builder used to generate deserialization functions for C# types.
        /// </summary>
        public IBinaryDeserializerBuilder DeserializerBuilder { get; }

        /// <summary>
        /// The client used for Schema Registry operations.
        /// </summary>
        public ISchemaRegistryClient RegistryClient { get; }

        /// <summary>
        /// The JSON schema reader used to convert schemas received from the registry into abstract
        /// representations.
        /// </summary>
        public IJsonSchemaReader SchemaReader { get; }

        private readonly ConcurrentDictionary<int, Task<Func<Stream, T>>> _cache;

        private readonly bool _disposeRegistryClient;

        /// <summary>
        /// Creates a deserializer.
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
        /// <exception cref="ArgumentNullException">
        /// Thrown when the registry configuration is null.
        /// </exception>
        public AsyncSchemaRegistryDeserializer(
            IEnumerable<KeyValuePair<string, string>> registryConfiguration,
            IBinaryDeserializerBuilder? deserializerBuilder = null,
            IJsonSchemaReader? schemaReader = null
        ) {
            if (registryConfiguration == null)
            {
                throw new ArgumentNullException(nameof(registryConfiguration));
            }

            DeserializerBuilder = deserializerBuilder ?? new BinaryDeserializerBuilder();
            RegistryClient = new CachedSchemaRegistryClient(registryConfiguration);
            SchemaReader = schemaReader ?? new JsonSchemaReader();

            _cache = new ConcurrentDictionary<int, Task<Func<Stream, T>>>();
            _disposeRegistryClient = true;
        }

        /// <summary>
        /// Creates a deserializer.
        /// </summary>
        /// <param name="registryClient">
        /// The client to use for Schema Registry operations. (The client will not be disposed.)
        /// </param>
        /// <param name="deserializerBuilder">
        /// The deserializer builder used to generate deserialization functions for C# types. If
        /// none is provided, the default deserializer builder will be used.
        /// </param>
        /// <param name="schemaReader">
        /// The JSON schema reader used to convert schemas received from the registry into abstract
        /// representations. If none is provided, the default schema reader will be used.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the registry client is null.
        /// </exception>
        public AsyncSchemaRegistryDeserializer(
            ISchemaRegistryClient registryClient,
            IBinaryDeserializerBuilder? deserializerBuilder = null,
            IJsonSchemaReader? schemaReader = null
        ) {
            if (registryClient == null)
            {
                throw new ArgumentNullException(nameof(registryClient));
            }

            DeserializerBuilder = deserializerBuilder ?? new BinaryDeserializerBuilder();
            RegistryClient = registryClient;
            SchemaReader = schemaReader ?? new JsonSchemaReader();

            _cache = new ConcurrentDictionary<int, Task<Func<Stream, T>>>();
            _disposeRegistryClient = false;
        }

        /// <summary>
        /// Deserialize a message. (See <see cref="IAsyncDeserializer{T}.DeserializeAsync(ReadOnlyMemory{byte}, bool, SerializationContext)" />.)
        /// </summary>
        public virtual async Task<T> DeserializeAsync(ReadOnlyMemory<byte> data, bool isNull, SerializationContext context)
        {
            using (var stream = new MemoryStream(data.ToArray(), false))
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

                var @delegate = await (_cache.GetOrAdd(BitConverter.ToInt32(bytes, 0), async id =>
                {
                    var json = await RegistryClient.GetSchemaAsync(id).ConfigureAwait(false);
                    var schema = SchemaReader.Read(json);

                    return DeserializerBuilder.BuildDelegate<T>(schema);
                })).ConfigureAwait(false);

                return @delegate(stream);
            }
        }

        /// <summary>
        /// Disposes the deserializer, freeing up any resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the deserializer, freeing up any resources.
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
    }
}
