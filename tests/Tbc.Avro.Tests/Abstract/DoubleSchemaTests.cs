using Tbc.Avro.Abstract;
using Xunit;

namespace Tbc.Avro.Tests
{
    public class DoubleSchemaTests
    {
        [Fact]
        public void IsPrimitiveSchema()
        {
            Assert.IsAssignableFrom<PrimitiveSchema>(new DoubleSchema());
        }
    }
}
