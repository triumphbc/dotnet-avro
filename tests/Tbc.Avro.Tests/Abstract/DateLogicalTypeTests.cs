using Tbc.Avro.Abstract;
using Xunit;

namespace Tbc.Avro.Tests
{
    public class DateLogicalTypeTests
    {
        [Fact]
        public void IsLogicalType()
        {
            Assert.IsAssignableFrom<LogicalType>(new DateLogicalType());
        }
    }
}
