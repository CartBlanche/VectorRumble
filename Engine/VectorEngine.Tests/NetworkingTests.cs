using System.IO;
using VectorEngine.Networking;
using Xunit;

namespace VectorEngine.Tests
{
    public class NetworkingTests
    {
        enum GameIds : byte { Hello = 70, Move = 80 }

        static byte[] Payload(byte version, INetworkPacket packet)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(version);
            writer.Write(packet.Type);
            packet.Serialize(writer);
            writer.Flush();
            return stream.ToArray();
        }

        [Fact]
        public void The_engine_keeps_ids_64_and_65_and_a_game_starts_at_70()
        {
            Assert.Equal(64, new PingPacket().Type);
            Assert.Equal(65, new PongPacket().Type);
            Assert.Equal(70, EnginePacketIds.FirstGameId);
        }

        [Fact]
        public void A_packet_of_the_right_version_reaches_its_handler()
        {
            var dispatcher = new PacketDispatcher { ProtocolVersion = 3 };
            uint token = 0;
            dispatcher.Register(EnginePacketIds.Ping, PacketAudience.Any, (s, r) => token = PingPacket.Deserialize(r).Token);

            dispatcher.Dispatch(null, Payload(3, new PingPacket { Token = 9 }), false);

            Assert.Equal(9u, token);
            Assert.Equal(0, dispatcher.VersionMismatchCount);
        }

        [Fact]
        public void A_packet_of_another_version_is_dropped_and_reported()
        {
            var dispatcher = new PacketDispatcher { ProtocolVersion = 3 };
            int handled = 0;
            byte seen = 0;
            dispatcher.Register(EnginePacketIds.Ping, PacketAudience.Any, (s, r) => handled++);
            dispatcher.ProtocolMismatch += v => seen = v;

            dispatcher.Dispatch(null, Payload(2, new PingPacket()), false);

            Assert.Equal(0, handled);
            Assert.Equal(1, dispatcher.VersionMismatchCount);
            Assert.Equal(2, seen);
            Assert.Equal(0, dispatcher.UnknownCount);
        }

        [Fact]
        public void A_game_registers_its_handlers_with_an_enum()
        {
            var dispatcher = new PacketDispatcher();
            int handled = 0;
            dispatcher.Register(GameIds.Move, PacketAudience.Any, (s, r) => handled++);

            dispatcher.Dispatch(null, new byte[] { 1, (byte)GameIds.Move }, false);
            dispatcher.Unregister(GameIds.Move);
            dispatcher.Dispatch(null, new byte[] { 1, (byte)GameIds.Move }, false);

            Assert.Equal(1, handled);
            Assert.Equal(1, dispatcher.UnknownCount);
        }
    }
}
