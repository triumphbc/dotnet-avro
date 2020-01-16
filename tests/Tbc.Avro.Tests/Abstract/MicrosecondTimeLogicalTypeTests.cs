using Tbc.Avro.Abstract;
using Xunit;

namespace Tbc.Avro.Tests
{
    public class MicrosecondTimeLogicalTypeTests
    {
        [Fact]
        public void IsLogicalType()
        {
            Assert.IsAssignableFrom<LogicalType>(new MicrosecondTimeLogicalType());
        }

        [Fact]
        public void IsTimeLogicalType()
        {
            Assert.IsAssignableFrom<TimeLogicalType>(new MicrosecondTimeLogicalType());
        }
    }
}
