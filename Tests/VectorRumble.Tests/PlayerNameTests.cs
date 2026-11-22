using VectorRumble.Networking;
using Xunit;

namespace VectorRumble.Tests
{
    public class PlayerNameTests
    {
        [Fact]
        public void The_network_suffix_is_hidden_from_the_name_shown()
        {
            Assert.Equal("John", LanLobby.DisplayName("John_1a2b3c4d"));
            Assert.Equal("John", LanLobby.DisplayName("John"));
            Assert.Equal("Player", LanLobby.DisplayName(null));
            Assert.Equal("Player", LanLobby.DisplayName(""));
        }
    }
}
