using System.IO;

namespace VectorEngine.Networking
{
    /// <summary>
    /// The packet ids the engine keeps for itself. A game's own packets use 70 and up (the Net library keeps the
    /// ids below 64 for its own messages).
    /// </summary>
    public static class EnginePacketIds
    {
        public const byte Ping = 64;
        public const byte Pong = 65;

        /// <summary>The first id a game may use.</summary>
        public const byte FirstGameId = 70;
    }

    /// <summary>
    /// Connectivity test: sent to a peer, who answers with a <see cref="PongPacket"/> carrying the same token
    /// </summary>
    public class PingPacket : INetworkPacket
    {
        public byte Type { get { return EnginePacketIds.Ping; } }
        public uint Token { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Token);
        }

        public static PingPacket Deserialize(BinaryReader reader)
        {
            return new PingPacket { Token = reader.ReadUInt32() };
        }
    }

    public class PongPacket : INetworkPacket
    {
        public byte Type { get { return EnginePacketIds.Pong; } }
        public uint Token { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Token);
        }

        public static PongPacket Deserialize(BinaryReader reader)
        {
            return new PongPacket { Token = reader.ReadUInt32() };
        }
    }
}
