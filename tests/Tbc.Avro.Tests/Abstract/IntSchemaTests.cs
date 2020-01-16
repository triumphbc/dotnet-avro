using Tbc.Avro.Abstract;
using Xunit;

namespace Tbc.Avro.Tests
{
    public class IntSchemaTests
    {
        [Fact]
        public void IsPrimitiveSchema()
        {
            Assert.IsAssignableFrom<PrimitiveSchema>(new IntSchema());
        }
    }
}
