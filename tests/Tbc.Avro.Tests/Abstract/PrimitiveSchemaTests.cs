using Tbc.Avro.Abstract;
using Xunit;

namespace Tbc.Avro.Tests
{
    public class PrimitiveSchemaTests
    {
        [Fact]
        public void IsSchema()
        {
            Assert.IsAssignableFrom<Schema>(new ConcretePrimitiveSchema());
        }

        private class ConcretePrimitiveSchema : PrimitiveSchema { }
    }
}
