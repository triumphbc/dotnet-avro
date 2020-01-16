using Tbc.Avro.Abstract;
using Xunit;

namespace Tbc.Avro.Tests
{
    public class InvalidNameExceptionTests
    {
        [Fact]
        public void IncludesNameInMessage()
        {
            var name = "bad!";
            var exception = new InvalidNameException(name);

            Assert.StartsWith($"\"{name}\"", exception.Message);
            Assert.Equal(name, exception.Name);
        }
    }
}
