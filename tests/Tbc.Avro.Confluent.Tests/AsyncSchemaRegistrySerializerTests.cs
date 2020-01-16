using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Moq;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

using ISchemaRegistryClient = Confluent.SchemaRegistry.ISchemaRegistryClient;

namespace Tbc.Avro.Confluent.Tests
{
    public class AsyncSchemaRegistrySerializerTests
    {
        protected readonly Mock<ISchemaRegistryClient> RegistryClientMock;

        public AsyncSchemaRegistrySerializerTests()
        {
            RegistryClientMock = new Mock<ISchemaRegistryClient>();
        }

        [Fact]
        public async Task CachesGeneratedSerializers()
        {
            var serializer = new AsyncSchemaRegistrySerializer<object>(
                RegistryClientMock.Object
            );

            var metadata = new MessageMetadata();
            var context = new SerializationContext(MessageComponentType.Value, "test_topic");
            var subject = $"{context.Topic}-value";

            RegistryClientMock
                .Setup(c => c.GetLatestSchemaAsync(subject))
                .ReturnsAsync(new Schema(subject, 1, 12, "\"null\""));

            await Task.WhenAll(Enumerable.Range(0, 5).Select(i =>
                serializer.SerializeAsync(null, context)
            ));

            RegistryClientMock
                .Verify(c => c.GetLatestSchemaAsync(subject), Times.Once());
        }

        [Fact]
        public async Task ProvidesDefaultSerializationComponents()
        {
            var serializer = new AsyncSchemaRegistrySerializer<int>(
                RegistryClientMock.Object
            );

            var data = 4;
            var encoding = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x04, 0x08 };
            var metadata = new MessageMetadata();
            var context = new SerializationContext(MessageComponentType.Key, "test_topic");
            var subject = $"{context.Topic}-key";

            RegistryClientMock
                .Setup(c => c.GetLatestSchemaAsync(subject))
                .ReturnsAsync(new Schema(subject, 1, 4, "\"int\""));

            Assert.Equal(encoding,
                await serializer.SerializeAsync(data, context)
            );
        }

        [Fact]
        public async Task RegistersWhenSchemaIncompatible()
        {
            var serializer = new AsyncSchemaRegistrySerializer<int>(
                RegistryClientMock.Object,
                registerAutomatically: true
            );

            var data = 6;
            var encoding = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x0a, 0x0c };
            var metadata = new MessageMetadata();
            var context = new SerializationContext(MessageComponentType.Value, "test_topic");
            var subject = $"{context.Topic}-value";

            RegistryClientMock
                .Setup(c => c.GetLatestSchemaAsync(subject))
                .ReturnsAsync(new Schema(subject, 1, 9, "\"string\""));

            RegistryClientMock
                .Setup(c => c.RegisterSchemaAsync(subject, It.IsAny<string>()))
                .ReturnsAsync(10);

            Assert.Equal(encoding,
                await serializer.SerializeAsync(data, context)
            );
        }

        [Fact]
        public async Task RegistersWhenSubjectMissing()
        {
            var serializer = new AsyncSchemaRegistrySerializer<int>(
                RegistryClientMock.Object,
                registerAutomatically: true
            );

            var data = 6;
            var encoding = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x08, 0x0c };
            var metadata = new MessageMetadata();
            var context = new SerializationContext(MessageComponentType.Value, "test_topic");
            var subject = $"{context.Topic}-value";

            RegistryClientMock
                .Setup(c => c.GetLatestSchemaAsync(subject))
                .ThrowsAsync(new SchemaRegistryException("Subject not found", HttpStatusCode.NotFound, 40401));

            RegistryClientMock
                .Setup(c => c.RegisterSchemaAsync(subject, It.IsAny<string>()))
                .ReturnsAsync(8);

            Assert.Equal(encoding,
                await serializer.SerializeAsync(data, context)
            );
        }

        [Fact]
        public async Task UsesSubjectNameBuilder()
        {
            var version = GetType().Assembly.GetName().Version;

            var serializer = new AsyncSchemaRegistrySerializer<int>(
                RegistryClientMock.Object,
                subjectNameBuilder: c => $"{c.Topic}-{version}-{c.Component.ToString().ToLowerInvariant()}"
            );

            var data = 2;
            var encoding = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x08, 0x04 };
            var metadata = new MessageMetadata();
            var context = new SerializationContext(MessageComponentType.Key, "test_topic");
            var subject = $"{context.Topic}-{version}-key";

            RegistryClientMock
                .Setup(c => c.GetLatestSchemaAsync(subject))
                .ReturnsAsync(new Schema(subject, 2, 8, "\"int\""));

            Assert.Equal(encoding,
                await serializer.SerializeAsync(data, context)
            );
        }
    }
}
