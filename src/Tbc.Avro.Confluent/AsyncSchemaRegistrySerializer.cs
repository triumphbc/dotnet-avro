using Tbc.Avro.Representation;
using Tbc.Avro.Serialization;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Tbc.Avro.Confluent
{
    /// <summary>
    /// An <see cref="IAsyncSerializer{T}" /> that resolves Avro schemas on the fly. When serializing
    /// messages, this serializer will attempt to look up a subject that matches the topic name (if
    /// not already cached).
    /// </summary>
    /// <remarks>
    /// By default, when serializing keys for a topic with name "test_topic", this deserializer
    /// would query the Schema Registry for subject "test_topic-key". (This is a Confluent
    /// convention—values would be "test_topic-value".)
    /// </remarks>
    public class AsyncSchemaRegistrySerializer<T> : IAsyncSerializer<T>
    {
        /// <summary>
        /// Whether to automatically register schemas that match the type being serialized.
        /// </summary>
        public bool RegisterAutomatically { get; }

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
        public IBinarySerializerBuilder SerializerBuilder { get; }

        /// <summary>
        /// A function that determines the subject name given the topic name and a component type
        /// (key or value).
        /// </summary>
        public Func<SerializationContext, string> SubjectNameBuilder { get; }

        private readonly ConcurrentDictionary<string, Task<Func<T, byte[]>>> _cache;

        private readonly Func<string, string, Task<int>> _register;

        private readonly Func<string, Task<Schema>> _resolve;

        /// <summary>
        /// Creates a serializer.
        /// </summary>
        /// <param name="registryConfiguration">
        /// Schema Registry configuration. Using the <see cref="SchemaRegistryConfig" /> class is
        /// highly recommended.
        /// </param>
        /// <param name="registerAutomatically">
        /// Whether to automatically register schemas that match the type being serialized.
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
        /// <param name="subjectNameBuilder">
        /// A function that determines the subject name given the topic name and a component type
        /// (key or value). If none is provided, the default "{topic name}-{component}" naming
        /// convention will be used.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the registry configuration is null.
        /// </exception>
        public AsyncSchemaRegistrySerializer(
            IEnumerable<KeyValuePair<string, string>> registryConfiguration,
            bool registerAutomatically = false,
            Abstract.ISchemaBuilder? schemaBuilder = null,
            IJsonSchemaReader? schemaReader = null,
            IJsonSchemaWriter? schemaWriter = null,
            IBinarySerializerBuilder? serializerBuilder = null,
            Func<SerializationContext, string>? subjectNameBuilder = null
        ) {
            if (registryConfiguration == null)
            {
                throw new ArgumentNullException(nameof(registryConfiguration));
            }

            RegisterAutomatically = registerAutomatically;
            SchemaBuilder = schemaBuilder ?? new Abstract.SchemaBuilder();
            SchemaReader = schemaReader ?? new JsonSchemaReader();
            SchemaWriter = schemaWriter ?? new JsonSchemaWriter();
            SerializerBuilder = serializerBuilder ?? new BinarySerializerBuilder();
            SubjectNameBuilder = subjectNameBuilder ??
                (c => $"{c.Topic}-{(c.Component == MessageComponentType.Key ? "key" : "value")}");

            _cache = new ConcurrentDictionary<string, Task<Func<T, byte[]>>>();

            _register = async (subject, json) =>
            {
                using (var registry = new CachedSchemaRegistryClient(registryConfiguration))
                {
                    return await registry.RegisterSchemaAsync(subject, json).ConfigureAwait(false);
                }
            };

            _resolve = async subject =>
            {
                using (var registry = new CachedSchemaRegistryClient(registryConfiguration))
                {
                    return await registry.GetLatestSchemaAsync(subject).ConfigureAwait(false);
                }
            };
        }

        /// <summary>
        /// Creates a serializer.
        /// </summary>
        /// <param name="registryClient">
        /// The client to use for Schema Registry operations. (The client will not be disposed.)
        /// </param>
        /// <param name="registerAutomatically">
        /// Whether to automatically register schemas that match the type being serialized.
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
        /// <param name="subjectNameBuilder">
        /// A function that determines the subject name given the topic name and a component type
        /// (key or value). If none is provided, the default "{topic name}-{component}" naming
        /// convention will be used.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the registry client is null.
        /// </exception>
        public AsyncSchemaRegistrySerializer(
            ISchemaRegistryClient registryClient,
            bool registerAutomatically = false,
            Abstract.ISchemaBuilder? schemaBuilder = null,
            IJsonSchemaReader? schemaReader = null,
            IJsonSchemaWriter? schemaWriter = null,
            IBinarySerializerBuilder? serializerBuilder = null,
            Func<SerializationContext, string>? subjectNameBuilder = null
        ) {
            if (registryClient == null)
            {
                throw new ArgumentNullException(nameof(registryClient));
            }

            RegisterAutomatically = registerAutomatically;
            SchemaBuilder = schemaBuilder ?? new Abstract.SchemaBuilder();
            SchemaReader = schemaReader ?? new JsonSchemaReader();
            SchemaWriter = schemaWriter ?? new JsonSchemaWriter();
            SerializerBuilder = serializerBuilder ?? new BinarySerializerBuilder();
            SubjectNameBuilder = subjectNameBuilder ??
                (c => $"{c.Topic}-{(c.Component == MessageComponentType.Key ? "key" : "value")}");

            _cache = new ConcurrentDictionary<string, Task<Func<T, byte[]>>>();
            _register = (subject, json) => registryClient.RegisterSchemaAsync(subject, json);
            _resolve = subject => registryClient.GetLatestSchemaAsync(subject);
        }

        /// <summary>
        /// Serialize a message. (See <see cref="IAsyncSerializer{T}.SerializeAsync(T, SerializationContext)" />.)
        /// </summary>
        public virtual async Task<byte[]> SerializeAsync(T data, SerializationContext context)
        {
            var serialize = await (_cache.GetOrAdd(SubjectNameBuilder(context), async subject =>
            {
                int id;
                Action<T, Stream> @delegate;

                try
                {
                    var existing = await _resolve(subject).ConfigureAwait(false);
                    var schema = SchemaReader.Read(existing.SchemaString);

                    @delegate = SerializerBuilder.BuildDelegate<T>(schema);
                    id = existing.Id;
                }
                catch (Exception e) when (RegisterAutomatically && (
                    (e is SchemaRegistryException sre && sre.ErrorCode == 40401) ||
                    (e is UnsupportedSchemaException || e is UnsupportedTypeException)
                ))
                {
                    var schema = SchemaBuilder.BuildSchema<T>();
                    var json = SchemaWriter.Write(schema);

                    @delegate = SerializerBuilder.BuildDelegate<T>(schema);
                    id = await _register(subject, json).ConfigureAwait(false);
                }

                var bytes = BitConverter.GetBytes(id);

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                return value =>
                {
                    var stream = new MemoryStream();

                    using (stream)
                    {
                        stream.WriteByte(0x00);
                        stream.Write(bytes, 0, bytes.Length);

                        @delegate(value, stream);
                    }

                    return stream.ToArray();
                };
            })).ConfigureAwait(false);

            return serialize(data);
        }
    }
}
