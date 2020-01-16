using Tbc.Avro.Abstract;
using Xunit;

namespace Tbc.Avro.Tests
{
    public class DurationLogicalTypeTests
    {
        [Fact]
        public void IsLogicalType()
        {
            Assert.IsAssignableFrom<LogicalType>(new DurationLogicalType());
        }
    }
}
