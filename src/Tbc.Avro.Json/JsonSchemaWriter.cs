using Tbc.Avro.Abstract;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Tbc.Avro.Representation
{
    /// <summary>
    /// Writes an Avro schema to JSON.
    /// </summary>
    public interface IJsonSchemaWriter : ISchemaWriter
    {
        /// <summary>
        /// Writes a serialized Avro schema.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// An optional schema cache. The cache is populated as the schema is written and can be
        /// used to determine which named schemas have already been processed.
        /// </param>
        /// <returns>
        /// Returns a JSON-encoded schema.
        /// </returns>
        string Write(Schema schema, bool canonical = false, ConcurrentDictionary<string, NamedSchema>? names = null);

        /// <summary>
        /// Writes a serialized Avro schema.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The writer to use for JSON operations.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// An optional schema cache. The cache is populated as the schema is written and can be
        /// used to determine which named schemas have already been processed.
        /// </param>
        void Write(Schema schema, Utf8JsonWriter json, bool canonical = false, ConcurrentDictionary<string, NamedSchema>? names = null);
    }

    /// <summary>
    /// Represents the outcome of a JSON schema reader case.
    /// </summary>
    public interface IJsonSchemaWriteResult
    {
        /// <summary>
        /// Any exceptions related to the applicability of the case.
        /// </summary>
        ICollection<Exception> Exceptions { get; }
    }

    /// <summary>
    /// Writes specific Avro schemas to JSON. Used by <see cref="JsonSchemaWriter" /> to break apart
    /// write logic.
    /// </summary>
    public interface IJsonSchemaWriterCase
    {
        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names);
    }

    /// <summary>
    /// A customizable JSON schema writer backed by a list of cases.
    /// </summary>
    public class JsonSchemaWriter : IJsonSchemaWriter
    {
        /// <summary>
        /// A list of cases that the write methods will attempt to apply. If the first case does
        /// not match, the next case will be tested, and so on.
        /// </summary>
        public IEnumerable<IJsonSchemaWriterCase> Cases { get; }

        /// <summary>
        /// Creates a new JSON schema writer.
        /// </summary>
        public JsonSchemaWriter() : this(CreateCaseBuilders()) { }

        /// <summary>
        /// Creates a new JSON schema writer.
        /// </summary>
        /// <param name="caseBuilders">
        /// A list of case builders.
        /// </param>
        public JsonSchemaWriter(IEnumerable<Func<IJsonSchemaWriter, IJsonSchemaWriterCase>>? caseBuilders)
        {
            var cases = new List<IJsonSchemaWriterCase>();

            Cases = cases;

            // initialize cases last so that the schema writer is fully ready:
            foreach (var builder in caseBuilders ?? CreateCaseBuilders())
            {
                cases.Add(builder(this));
            }
        }

        /// <summary>
        /// Writes a serialized Avro schema.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// An optional schema cache. The cache is populated as the schema is written and can be
        /// used to determine which named schemas have already been processed.
        /// </param>
        /// <returns>
        /// Returns a JSON-encoded schema.
        /// </returns>
        /// <exception cref="InvalidSchemaException">
        /// Thrown when a schema constraint prevents a valid schema from being
        /// written.
        /// </exception>
        /// <exception cref="UnsupportedSchemaException">
        /// Thrown when no matching case is found for the schema.
        /// </exception>
        public virtual string Write(Schema schema, bool canonical = false, ConcurrentDictionary<string, NamedSchema>? names = null)
        {
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            {
                Write(schema, stream, canonical, names);

                stream.Seek(0, SeekOrigin.Begin);
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Writes a serialized Avro schema.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="stream">
        /// The stream to write the schema to. (The stream will not be disposed.)
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// An optional schema cache. The cache is populated as the schema is written and can be
        /// used to determine which named schemas have already been processed.
        /// </param>
        /// <returns>
        /// Returns a JSON-encoded schema.
        /// </returns>
        /// <exception cref="InvalidSchemaException">
        /// Thrown when a schema constraint prevents a valid schema from being
        /// written.
        /// </exception>
        /// <exception cref="UnsupportedSchemaException">
        /// Thrown when no matching case is found for the schema.
        /// </exception>
        public virtual void Write(Schema schema, Stream stream, bool canonical = false, ConcurrentDictionary<string, NamedSchema>? names = null)
        {
            using (var json = new Utf8JsonWriter(stream))
            {
                Write(schema, json, canonical, names);
            }
        }

        /// <summary>
        /// Writes a serialized Avro schema.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The writer to use for JSON operations.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// An optional schema cache. The cache is populated as the schema is written and can be
        /// used to determine which named schemas have already been processed.
        /// </param>
        /// <returns>
        /// Returns a JSON-encoded schema.
        /// </returns>
        /// <exception cref="InvalidSchemaException">
        /// Thrown when a schema constraint prevents a valid schema from being
        /// written.
        /// </exception>
        /// <exception cref="UnsupportedSchemaException">
        /// Thrown when no matching case is found for the schema.
        /// </exception>
        public virtual void Write(Schema schema, Utf8JsonWriter json, bool canonical = false, ConcurrentDictionary<string, NamedSchema>? names = null)
        {
            if (names == null)
            {
                names = new ConcurrentDictionary<string, NamedSchema>();
            }

            var exceptions = new List<Exception>();

            foreach (var @case in Cases)
            {
                var result = @case.Write(schema, json, canonical, names);

                if (result.Exceptions.Count > 0)
                {
                    exceptions.AddRange(result.Exceptions);
                }
                else
                {
                    return;
                }
            }

            throw new UnsupportedSchemaException(schema, $"No schema writer case matched {schema.GetType().Name}.", new AggregateException(exceptions));
        }

        /// <summary>
        /// Creates a default list of case builders.
        /// </summary>
        public static IEnumerable<Func<IJsonSchemaWriter, IJsonSchemaWriterCase>> CreateCaseBuilders()
        {
            return new Func<IJsonSchemaWriter, IJsonSchemaWriterCase>[]
            {
                // logical types:
                writer => new DateJsonSchemaWriterCase(),
                writer => new DecimalJsonSchemaWriterCase(),
                writer => new DurationJsonSchemaWriterCase(),
                writer => new MicrosecondTimeJsonSchemaWriterCase(),
                writer => new MicrosecondTimestampJsonSchemaWriterCase(),
                writer => new MillisecondTimeJsonSchemaWriterCase(),
                writer => new MillisecondTimestampJsonSchemaWriterCase(),
                writer => new UuidJsonSchemaWriterCase(),

                // collections:
                writer => new ArrayJsonSchemaWriterCase(writer),
                writer => new MapJsonSchemaWriterCase(writer),

                // unions:
                writer => new UnionJsonSchemaWriterCase(writer),

                // named:
                writer => new EnumJsonSchemaWriterCase(),
                writer => new FixedJsonSchemaWriterCase(),
                writer => new RecordJsonSchemaWriterCase(writer),

                // primitives:
                writer => new PrimitiveJsonSchemaWriterCase()
            };
        }
    }

    /// <summary>
    /// A base <see cref="IJsonSchemaWriteResult" /> implementation.
    /// </summary>
    public class JsonSchemaWriteResult : IJsonSchemaWriteResult
    {
        /// <summary>
        /// Any exceptions related to the applicability of the case.
        /// </summary>
        public ICollection<Exception> Exceptions { get; set; } = new List<Exception>();
    }

    /// <summary>
    /// A base <see cref="IJsonSchemaWriterCase" /> implementation.
    /// </summary>
    public abstract class JsonSchemaWriterCase : IJsonSchemaWriterCase
    {
        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public abstract IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names);
    }

    /// <summary>
    /// A JSON schema writer case that matches <see cref="ArraySchema" />.
    /// </summary>
    public class ArrayJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// A schema writer to use to write item schemas.
        /// </summary>
        public IJsonSchemaWriter Writer { get; }

        /// <summary>
        /// Creates a new array case.
        /// </summary>
        /// <param name="writer">
        /// A schema writer to use to write item schemas.
        /// </param>
        public ArrayJsonSchemaWriterCase(IJsonSchemaWriter writer)
        {
            Writer = writer ?? throw new ArgumentNullException(nameof(writer), "Schema writer cannot be null.");
        }

        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema is ArraySchema arraySchema)
            {
                json.WriteStartObject();
                json.WriteString(JsonAttributeToken.Type, JsonSchemaToken.Array);
                json.WritePropertyName(JsonAttributeToken.Items);
                Writer.Write(arraySchema.Item, json, canonical, names);
                json.WriteEndObject();
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The array case can only be applied to an array schema."));
            }

            return result;
        }
    }

    /// <summary>
    /// A JSON schema writer case that matches <see cref="DateLogicalType" />.
    /// </summary>
    public class DateJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema is IntSchema && schema.LogicalType is DateLogicalType)
            {
                if (canonical)
                {
                    json.WriteStringValue(JsonSchemaToken.Int);
                }
                else
                {
                    json.WriteStartObject();
                    json.WriteString(JsonAttributeToken.Type, JsonSchemaToken.Int);
                    json.WriteString(JsonAttributeToken.LogicalType, JsonSchemaToken.Date);
                    json.WriteEndObject();
                }
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The date case can only be applied to an int schema with a date logical type."));
            }

            return result;
        }
    }

    /// <summary>
    /// A JSON schema writer case that matches <see cref="DecimalLogicalType" />.
    /// </summary>
    public class DecimalJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema.LogicalType is DecimalLogicalType decimalLogicalType)
            {
                if (schema is FixedSchema fixedSchema)
                {
                    if (names.TryGetValue(fixedSchema.FullName, out var existing))
                    {
                        if (!schema.Equals(existing))
                        {
                            throw new InvalidSchemaException($"A conflicting schema with the name {fixedSchema.FullName} has already been written.");
                        }

                        json.WriteStringValue(fixedSchema.FullName);
                    }
                    else
                    {
                        if (!names.TryAdd(fixedSchema.FullName, fixedSchema))
                        {
                            throw new InvalidOperationException();
                        }

                        json.WriteStartObject();
                        json.WriteString(JsonAttributeToken.Name, fixedSchema.FullName);

                        if (!canonical)
                        {
                            if (fixedSchema.Aliases.Count > 0)
                            {
                                json.WritePropertyName(JsonAttributeToken.Aliases);
                                json.WriteStartArray();

                                foreach (var alias in fixedSchema.Aliases)
                                {
                                    json.WriteStringValue(alias);
                                }

                                json.WriteEndArray();
                            }
                        }

                        json.WriteString(JsonAttributeToken.Type, JsonSchemaToken.Fixed);

                        if (!canonical)
                        {
                            json.WriteString(JsonAttributeToken.LogicalType, JsonSchemaToken.Decimal);
                            json.WriteNumber(JsonAttributeToken.Precision, decimalLogicalType.Precision);
                            json.WriteNumber(JsonAttributeToken.Scale, decimalLogicalType.Scale);
                        }

                        json.WriteNumber(JsonAttributeToken.Size, fixedSchema.Size);
                        json.WriteEndObject();
                    }
                }
                else if (schema is BytesSchema)
                {
                    if (canonical)
                    {
                        json.WriteStringValue(JsonSchemaToken.Bytes);
                    }
                    else
                    {
                        json.WriteStartObject();
                        json.WriteString(JsonAttributeToken.Type, JsonSchemaToken.Bytes);
                        json.WriteString(JsonAttributeToken.LogicalType, JsonSchemaToken.Decimal);
                        json.WriteNumber(JsonAttributeToken.Precision, decimalLogicalType.Precision);
                        json.WriteNumber(JsonAttributeToken.Scale, decimalLogicalType.Scale);
                        json.WriteEndObject();
                    }
                }
                else
                {
                    throw new UnsupportedSchemaException(schema, "The decimal case can only be applied to bytes or fixed schemas with a decimal logical type.");
                }
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The decimal case can only be applied to bytes or fixed schemas with a decimal logical type."));
            }

            return result;
        }
    }

    /// <summary>
    /// A JSON schema writer case that matches <see cref="DurationLogicalType" />.
    /// </summary>
    public class DurationJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema is FixedSchema fixedSchema && schema.LogicalType is DurationLogicalType)
            {
                if (names.TryGetValue(fixedSchema.FullName, out var existing))
                {
                    if (!schema.Equals(existing))
                    {
                        throw new InvalidSchemaException($"A conflicting schema with the name {fixedSchema.FullName} has already been written.");
                    }

                    json.WriteStringValue(fixedSchema.FullName);
                }
                else
                {
                    if (!names.TryAdd(fixedSchema.FullName, fixedSchema))
                    {
                        throw new InvalidOperationException();
                    }

                    json.WriteStartObject();
                    json.WriteString(JsonAttributeToken.Name, fixedSchema.FullName);

                    if (!canonical)
                    {
                        if (fixedSchema.Aliases.Count > 0)
                        {
                            json.WritePropertyName(JsonAttributeToken.Aliases);
                            json.WriteStartArray();

                            foreach (var alias in fixedSchema.Aliases)
                            {
                                json.WriteStringValue(alias);
                            }

                            json.WriteEndArray();
                        }
                    }

                    json.WriteString(JsonAttributeToken.Type, JsonSchemaToken.Fixed);

                    if (!canonical)
                    {
                        json.WriteString(JsonAttributeToken.LogicalType, JsonSchemaToken.Duration);
                    }

                    json.WriteNumber(JsonAttributeToken.Size, fixedSchema.Size);
                    json.WriteEndObject();
                }
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The duration case can only be applied to a fixed schema with a duration logical type."));
            }

            return result;
        }
    }

    /// <summary>
    /// A JSON schema writer case that matches <see cref="EnumSchema" />.
    /// </summary>
    public class EnumJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema is EnumSchema enumSchema)
            {
                if (names.TryGetValue(enumSchema.FullName, out var existing))
                {
                    if (!schema.Equals(existing))
                    {
                        throw new InvalidSchemaException($"A conflicting schema with the name {enumSchema.FullName} has already been written.");
                    }

                    json.WriteStringValue(enumSchema.FullName);
                }
                else
                {
                    if (!names.TryAdd(enumSchema.FullName, enumSchema))
                    {
                        throw new InvalidOperationException();
                    }

                    json.WriteStartObject();
                    json.WriteString(JsonAttributeToken.Name, enumSchema.FullName);

                    if (!canonical)
                    {
                        if (enumSchema.Aliases.Count > 0)
                        {
                            json.WritePropertyName(JsonAttributeToken.Aliases);
                            json.WriteStartArray();

                            foreach (var alias in enumSchema.Aliases)
                            {
                                json.WriteStringValue(alias);
                            }

                            json.WriteEndArray();
                        }

                        if (!string.IsNullOrEmpty(enumSchema.Documentation))
                        {
                            json.WriteString(JsonAttributeToken.Doc, enumSchema.Documentation);
                        }
                    }

                    json.WriteString(JsonAttributeToken.Type, JsonSchemaToken.Enum);
                    json.WritePropertyName(JsonAttributeToken.Symbols);
                    json.WriteStartArray();

                    foreach (var symbol in enumSchema.Symbols)
                    {
                        json.WriteStringValue(symbol);
                    }

                    json.WriteEndArray();
                    json.WriteEndObject();
                }
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The enum case can only be applied to an enum schema."));
            }

            return result;
        }
    }

    /// <summary>
    /// A JSON schema writer case that matches <see cref="FixedSchema" />.
    /// </summary>
    public class FixedJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema is FixedSchema fixedSchema)
            {
                if (names.TryGetValue(fixedSchema.FullName, out var existing))
                {
                    if (!schema.Equals(existing))
                    {
                        throw new InvalidSchemaException($"A conflicting schema with the name {fixedSchema.FullName} has already been written.");
                    }

                    json.WriteStringValue(fixedSchema.FullName);
                }
                else
                {
                    if (!names.TryAdd(fixedSchema.FullName, fixedSchema))
                    {
                        throw new InvalidOperationException();
                    }

                    json.WriteStartObject();
                    json.WriteString(JsonAttributeToken.Name, fixedSchema.FullName);

                    if (!canonical)
                    {
                        if (fixedSchema.Aliases.Count > 0)
                        {
                            json.WritePropertyName(JsonAttributeToken.Aliases);
                            json.WriteStartArray();

                            foreach (var alias in fixedSchema.Aliases)
                            {
                                json.WriteStringValue(alias);
                            }

                            json.WriteEndArray();
                        }
                    }

                    json.WriteString(JsonAttributeToken.Type, JsonSchemaToken.Fixed);
                    json.WriteNumber(JsonAttributeToken.Size, fixedSchema.Size);
                    json.WriteEndObject();
                }
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The fixed case can only be applied to a fixed schema."));
            }

            return result;
        }
    }

    /// <summary>
    /// A JSON schema writer case that matches <see cref="MapSchema" />.
    /// </summary>
    public class MapJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// A schema writer to use to write value schemas.
        /// </summary>
        public IJsonSchemaWriter Writer { get; }

        /// <summary>
        /// Creates a new map case.
        /// </summary>
        /// <param name="writer">
        /// A schema writer to use to write value schemas.
        /// </param>
        public MapJsonSchemaWriterCase(IJsonSchemaWriter writer)
        {
            Writer = writer ?? throw new ArgumentNullException(nameof(writer), "Schema writer cannot be null.");
        }

        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema is MapSchema mapSchema)
            {
                json.WriteStartObject();
                json.WriteString(JsonAttributeToken.Type, JsonSchemaToken.Map);
                json.WritePropertyName(JsonAttributeToken.Values);
                Writer.Write(mapSchema.Value, json, canonical, names);
                json.WriteEndObject();
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The map case can only be applied to a map schema."));
            }

            return result;
        }
    }

    /// <summary>
    /// A JSON schema writer case that matches <see cref="MicrosecondTimeLogicalType" />.
    /// </summary>
    public class MicrosecondTimeJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema is LongSchema && schema.LogicalType is MicrosecondTimeLogicalType)
            {
                if (canonical)
                {
                    json.WriteStringValue(JsonSchemaToken.Long);
                }
                else
                {
                    json.WriteStartObject();
                    json.WriteString(JsonAttributeToken.Type, JsonSchemaToken.Long);
                    json.WriteString(JsonAttributeToken.LogicalType, JsonSchemaToken.TimeMicroseconds);
                    json.WriteEndObject();
                }
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The microsecond time case can only be applied to a long schema with a microsecond time logical type."));
            }

            return result;
        }
    }

    /// <summary>
    /// A JSON schema writer case that matches <see cref="MicrosecondTimestampLogicalType" />.
    /// </summary>
    public class MicrosecondTimestampJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema is LongSchema && schema.LogicalType is MicrosecondTimestampLogicalType)
            {
                if (canonical)
                {
                    json.WriteStringValue(JsonSchemaToken.Long);
                }
                else
                {
                    json.WriteStartObject();
                    json.WriteString(JsonAttributeToken.Type, JsonSchemaToken.Long);
                    json.WriteString(JsonAttributeToken.LogicalType, JsonSchemaToken.TimestampMicroseconds);
                    json.WriteEndObject();
                }
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The microsecond timestamp case can only be applied to a long schema with a microsecond timestamp logical type."));
            }

            return result;
        }
    }

    /// <summary>
    /// A JSON schema writer case that matches <see cref="MillisecondTimeLogicalType" />.
    /// </summary>
    public class MillisecondTimeJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema is IntSchema && schema.LogicalType is MillisecondTimeLogicalType)
            {
                if (canonical)
                {
                    json.WriteStringValue(JsonSchemaToken.Int);
                }
                else
                {
                    json.WriteStartObject();
                    json.WriteString(JsonAttributeToken.Type, JsonSchemaToken.Int);
                    json.WriteString(JsonAttributeToken.LogicalType, JsonSchemaToken.TimeMilliseconds);
                    json.WriteEndObject();
                }
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The millisecond time case can only be applied to an int schema with a millisecond time logical type."));
            }

            return result;
        }
    }

    /// <summary>
    /// A JSON schema writer case that matches <see cref="MillisecondTimestampLogicalType" />.
    /// </summary>
    public class MillisecondTimestampJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema is LongSchema && schema.LogicalType is MillisecondTimestampLogicalType)
            {
                if (canonical)
                {
                    json.WriteStringValue(JsonSchemaToken.Long);
                }
                else
                {
                    json.WriteStartObject();
                    json.WriteString(JsonAttributeToken.Type, JsonSchemaToken.Long);
                    json.WriteString(JsonAttributeToken.LogicalType, JsonSchemaToken.TimestampMilliseconds);
                    json.WriteEndObject();
                }
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The millisecond timestamp case can only be applied to a long schema with a millisecond timestamp logical type."));
            }

            return result;
        }
    }

    /// <summary>
    /// A JSON schema writer case that matches all <see cref="PrimitiveSchema" /> subclasses.
    /// </summary>
    public class PrimitiveJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema is PrimitiveSchema primitiveSchema)
            {
                json.WriteStringValue(GetSchemaToken(primitiveSchema));
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The primitive case can only be applied to a primitive schema."));
            }

            return result;
        }

        /// <summary>
        /// Matches a primitive schema to its type name.
        /// </summary>
        protected virtual string GetSchemaToken(PrimitiveSchema schema)
        {
            switch (schema)
            {
                case BooleanSchema _:
                    return JsonSchemaToken.Boolean;

                case BytesSchema _:
                    return JsonSchemaToken.Bytes;

                case DoubleSchema _:
                    return JsonSchemaToken.Double;

                case FloatSchema _:
                    return JsonSchemaToken.Float;

                case IntSchema _:
                    return JsonSchemaToken.Int;

                case LongSchema _:
                    return JsonSchemaToken.Long;

                case NullSchema _:
                    return JsonSchemaToken.Null;

                case StringSchema _:
                    return JsonSchemaToken.String;

                default:
                    throw new UnsupportedSchemaException(schema, $"Unknown primitive schema {schema.GetType().Name}.");
            }
        }
    }

    /// <summary>
    /// A JSON schema writer case that matches <see cref="RecordSchema" />.
    /// </summary>
    public class RecordJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// A schema writer to use to write field schemas.
        /// </summary>
        public IJsonSchemaWriter Writer { get; }

        /// <summary>
        /// Creates a new record case.
        /// </summary>
        /// <param name="writer">
        /// A schema writer to use to write field schemas.
        /// </param>
        public RecordJsonSchemaWriterCase(IJsonSchemaWriter writer)
        {
            Writer = writer ?? throw new ArgumentNullException(nameof(writer), "Schema writer cannot be null.");
        }

        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema is RecordSchema recordSchema)
            {
                if (names.TryGetValue(recordSchema.FullName, out var existing))
                {
                    if (!schema.Equals(existing))
                    {
                        throw new InvalidSchemaException($"A conflicting schema with the name {recordSchema.FullName} has already been written.");
                    }

                    json.WriteStringValue(recordSchema.FullName);
                }
                else
                {
                    if (!names.TryAdd(recordSchema.FullName, recordSchema))
                    {
                        throw new InvalidOperationException();
                    }

                    json.WriteStartObject();
                    json.WriteString(JsonAttributeToken.Name, recordSchema.FullName);

                    if (!canonical)
                    {
                        if (recordSchema.Aliases.Count > 0)
                        {
                            json.WritePropertyName(JsonAttributeToken.Aliases);
                            json.WriteStartArray();

                            foreach (var alias in recordSchema.Aliases)
                            {
                                json.WriteStringValue(alias);
                            }

                            json.WriteEndArray();
                        }

                        if (!string.IsNullOrEmpty(recordSchema.Documentation))
                        {
                            json.WriteString(JsonAttributeToken.Doc, recordSchema.Documentation);
                        }
                    }

                    json.WriteString(JsonAttributeToken.Type, JsonSchemaToken.Record);
                    json.WritePropertyName(JsonAttributeToken.Fields);
                    json.WriteStartArray();

                    foreach (var field in recordSchema.Fields)
                    {
                        json.WriteStartObject();
                        json.WriteString(JsonAttributeToken.Name, field.Name);

                        if (!canonical && !string.IsNullOrEmpty(field.Documentation))
                        {
                            json.WriteString(JsonAttributeToken.Doc, field.Documentation);
                        }

                        json.WritePropertyName(JsonAttributeToken.Type);
                        Writer.Write(field.Type, json, canonical, names);
                        json.WriteEndObject();
                    }

                    json.WriteEndArray();
                    json.WriteEndObject();
                }
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The record case can only be applied to a record schema."));
            }

            return result;
        }
    }

    /// <summary>
    /// A JSON schema writer case that matches <see cref="UnionSchema" />.
    /// </summary>
    public class UnionJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// A schema writer to use to write child schemas.
        /// </summary>
        public IJsonSchemaWriter Writer { get; }

        /// <summary>
        /// Creates a new union case.
        /// </summary>
        /// <param name="writer">
        /// A schema writer to use to write child schemas.
        /// </param>
        public UnionJsonSchemaWriterCase(IJsonSchemaWriter writer)
        {
            Writer = writer ?? throw new ArgumentNullException(nameof(writer), "Schema writer cannot be null.");
        }

        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema is UnionSchema unionSchema)
            {
                json.WriteStartArray();

                foreach (var child in unionSchema.Schemas)
                {
                    Writer.Write(child, json, canonical, names);
                }

                json.WriteEndArray();
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The union case can only be applied to a union schema."));
            }

            return result;
        }
    }

    /// <summary>
    /// A JSON schema writer case that matches <see cref="UuidLogicalType" />.
    /// </summary>
    public class UuidJsonSchemaWriterCase : JsonSchemaWriterCase
    {
        /// <summary>
        /// Writes a schema to JSON.
        /// </summary>
        /// <param name="schema">
        /// The schema to write.
        /// </param>
        /// <param name="json">
        /// The JSON writer to use for output.
        /// </param>
        /// <param name="canonical">
        /// Whether the schema should be written in Parsing Canonical Form (i.e., built without
        /// nonessential attributes).
        /// </param>
        /// <param name="names">
        /// A schema cache. The cache is populated as the schema is written and can be used to
        /// determine which named schemas have already been processed.
        /// </param>
        public override IJsonSchemaWriteResult Write(Schema schema, Utf8JsonWriter json, bool canonical, ConcurrentDictionary<string, NamedSchema> names)
        {
            var result = new JsonSchemaWriteResult();

            if (schema is StringSchema && schema.LogicalType is UuidLogicalType)
            {
                if (canonical)
                {
                    json.WriteStringValue(JsonSchemaToken.String);
                }
                else
                {
                    json.WriteStartObject();
                    json.WriteString(JsonAttributeToken.Type, JsonSchemaToken.String);
                    json.WriteString(JsonAttributeToken.LogicalType, JsonSchemaToken.Uuid);
                    json.WriteEndObject();
                }
            }
            else
            {
                result.Exceptions.Add(new UnsupportedSchemaException(schema, "The UUID case can only be applied to a string schema with a UUID logical type."));
            }

            return result;
        }
    }
}
