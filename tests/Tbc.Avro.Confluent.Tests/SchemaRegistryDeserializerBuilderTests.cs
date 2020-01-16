using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Moq;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Tbc.Avro.Confluent.Tests
{
    public class SchemaRegistryDeserializerBuilderTests
    {
        protected const string TestSubject = "test_subject";

        protected const int TestSubjectLatestId = 12;

        protected const string TestSubjectLatestString = @"""string""";

        protected const int TestSubjectLatestVersion = 4;

        protected readonly Mock<ISchemaRegistryClient> RegistryMock;

        public SchemaRegistryDeserializerBuilderTests()
        {
            RegistryMock = new Mock<ISchemaRegistryClient>(MockBehavior.Strict);

            RegistryMock.Setup(r => r.GetLatestSchemaAsync(TestSubject))
                .ReturnsAsync(new Schema(
                    TestSubject,
                    TestSubjectLatestVersion,
                    TestSubjectLatestId,
                    TestSubjectLatestString
                ));

            RegistryMock.Setup(r => r.GetSchemaAsync(TestSubject, TestSubjectLatestVersion))
                .ReturnsAsync(TestSubjectLatestString);

            RegistryMock.Setup(r => r.GetSchemaAsync(TestSubjectLatestId))
                .ReturnsAsync(TestSubjectLatestString);

            RegistryMock.Setup(r => r.GetSchemaIdAsync(TestSubject, TestSubjectLatestString))
                .ReturnsAsync(TestSubjectLatestId);
        }

        [Fact]
        public async Task BuildsDeserializerWithSchemaId()
        {
            using (var builder = new SchemaRegistryDeserializerBuilder(RegistryMock.Object))
            {
                await builder.Build<string>(TestSubjectLatestId);

                RegistryMock.Verify(r => r.GetSchemaAsync(TestSubjectLatestId), Times.Once());
                RegistryMock.VerifyNoOtherCalls();
            }
        }

        [Fact]
        public async Task BuildsDeserializerWithSchemaSubject()
        {
            using (var builder = new SchemaRegistryDeserializerBuilder(RegistryMock.Object))
            {
                await builder.Build<string>(TestSubject);

                RegistryMock.Verify(r => r.GetLatestSchemaAsync(TestSubject), Times.Once());
                RegistryMock.VerifyNoOtherCalls();
            }
        }

        [Fact]
        public async Task BuildsDeserializerWithSchemaSubjectAndVersion()
        {
            using (var builder = new SchemaRegistryDeserializerBuilder(RegistryMock.Object))
            {
                await builder.Build<string>(TestSubject, TestSubjectLatestVersion);

                RegistryMock.Verify(r => r.GetSchemaAsync(TestSubject, TestSubjectLatestVersion), Times.Once());
                RegistryMock.Verify(r => r.GetSchemaIdAsync(TestSubject, TestSubjectLatestString), Times.Once());
                RegistryMock.VerifyNoOtherCalls();
            }
        }

        [Theory]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x0c, 0x00 }, "")]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x0c, 0x06, 0x73, 0x75, 0x70 }, "sup")]
        public async Task DeserializesConfluentWireFormat(byte[] encoding, string data)
        {
            var context = new SerializationContext(MessageComponentType.Value, "test_topic");

            using (var builder = new SchemaRegistryDeserializerBuilder(RegistryMock.Object))
            {
                var deserializer = await builder.Build<string>(TestSubjectLatestId);

                Assert.Equal(data, deserializer.Deserialize(encoding, false, context));
            }
        }

        [Theory]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x0b, 0x00 })]
        public async Task ThrowsOnSchemaIdMismatch(byte[] encoding)
        {
            var context = new SerializationContext(MessageComponentType.Value, "test_topic");

            using (var builder = new SchemaRegistryDeserializerBuilder(RegistryMock.Object))
            {
                var deserializer = await builder.Build<string>(TestSubjectLatestId);

                Assert.Throws<InvalidDataException>(
                    () => deserializer.Deserialize(encoding, false, context)
                );
            }
        }

        [Theory]
        [InlineData(new byte[] { 0x00, 0x00, 0x00 })]
        [InlineData(new byte[] { 0x01, 0x00, 0x00, 0x00, 0x0c, 0x00 })]
        public async Task ThrowsOnUnrecognizedWireFormat(byte[] encoding)
        {
            var context = new SerializationContext(MessageComponentType.Value, "test_topic");

            using (var builder = new SchemaRegistryDeserializerBuilder(RegistryMock.Object))
            {
                var deserializer = await builder.Build<string>(TestSubjectLatestId);

                Assert.Throws<InvalidDataException>(
                    () => deserializer.Deserialize(encoding, false, context)
                );
            }
        }
    }
}
