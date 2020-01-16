using Tbc.Avro.Abstract;
using Xunit;

namespace Tbc.Avro.Tests
{
    public class BytesSchemaTests
    {
        [Fact]
        public void IsPrimitiveSchema()
        {
            Assert.IsAssignableFrom<PrimitiveSchema>(new BytesSchema());
        }
    }
}
