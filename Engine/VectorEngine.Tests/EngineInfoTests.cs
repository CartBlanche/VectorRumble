using Xunit;

namespace VectorEngine.Tests;

public class EngineInfoTests
{
    [Fact]
    public void Engine_is_named() => Assert.Equal("VectorEngine", EngineInfo.Name);
}
