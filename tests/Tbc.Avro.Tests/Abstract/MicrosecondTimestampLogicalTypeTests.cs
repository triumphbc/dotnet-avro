using Tbc.Avro.Abstract;
using Xunit;

namespace Tbc.Avro.Tests
{
    public class MicrosecondTimestampLogicalTypeTests
    {
        [Fact]
        public void IsLogicalType()
        {
            Assert.IsAssignableFrom<LogicalType>(new MicrosecondTimestampLogicalType());
        }

        [Fact]
        public void IsTimestampLogicalType()
        {
            Assert.IsAssignableFrom<TimestampLogicalType>(new MicrosecondTimestampLogicalType());
        }
    }
}
