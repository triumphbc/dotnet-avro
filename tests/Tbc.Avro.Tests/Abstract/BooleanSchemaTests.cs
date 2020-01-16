using Tbc.Avro.Abstract;
using Xunit;

namespace Tbc.Avro.Tests
{
    public class BooleanSchemaTests
    {
        [Fact]
        public void IsPrimitiveSchema()
        {
            Assert.IsAssignableFrom<PrimitiveSchema>(new BooleanSchema());
        }
    }
}
