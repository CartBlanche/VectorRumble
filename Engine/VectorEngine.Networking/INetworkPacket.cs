using System.IO;

namespace VectorEngine.Networking
{
    /// <summary>
    /// A gameplay packet. The type byte is written by <see cref="NetworkManager"/>
    /// before <see cref="Serialize"/>; each packet class also has a static
    /// Deserialize(BinaryReader) that reads what Serialize wrote.
    /// </summary>
    public interface INetworkPacket
    {
        /// <summary>The packet's id, the byte after the protocol version. Ids 64-69 are the engine's (see <see cref="EnginePacketIds"/>); a game uses 70 and up.</summary>
        byte Type { get; }
        void Serialize(BinaryWriter writer);
    }
}
