using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework.Net;

namespace VectorEngine.Networking
{
    /// <summary>
    /// Who a packet type may come from. Replaces the per-handler host/client checks
    /// scattered through the handlers.
    /// </summary>
    public enum PacketAudience
    {
        Any,            // accept from anybody
        FromHost,       // we are a client and the host sent it
        FromClient,     // we are the host and a client sent it
    }

    /// <summary>
    /// Routes received packets to the handler registered for their type, dropping
    /// packets from the wrong side and logging (not swallowing) malformed ones.
    /// </summary>
    public sealed class PacketDispatcher
    {
        struct Entry
        {
            public PacketAudience Audience;
            public Action<NetworkGamer, BinaryReader> Handler;
        }

        readonly Dictionary<byte, Entry> handlers = new Dictionary<byte, Entry>();

        /// <summary>
        /// The protocol version the other machines must send (the first byte of every payload). A payload with another
        /// version is dropped and counted, so builds with different packet formats never misread each other.
        /// </summary>
        public byte ProtocolVersion { get; set; } = 1;

        /// <summary>Raised (with the version received) when a payload of another protocol version arrives.</summary>
        public event Action<byte> ProtocolMismatch;

        public int VersionMismatchCount { get; private set; }

        public int ErrorCount { get; private set; }
        public int RejectedCount { get; private set; }
        public int UnknownCount { get; private set; }

        public void Register(byte type, PacketAudience audience,
            Action<NetworkGamer, BinaryReader> handler)
        {
            handlers[type] = new Entry { Audience = audience, Handler = handler };
        }

        /// <summary>Registers a handler for a game's packet id enum (an enum whose values fit in a byte).</summary>
        public void Register<TId>(TId type, PacketAudience audience,
            Action<NetworkGamer, BinaryReader> handler) where TId : struct, Enum
        {
            Register(Convert.ToByte(type), audience, handler);
        }

        public void Unregister<TId>(TId type) where TId : struct, Enum
        {
            Unregister(Convert.ToByte(type));
        }

        public void Unregister(byte type)
        {
            handlers.Remove(type);
        }

        public void Clear()
        {
            handlers.Clear();
        }

        /// <summary>
        /// Dispatch one payload (type byte followed by the packet body)
        /// </summary>
        internal void Dispatch(NetworkGamer sender, byte[] payload, bool localIsHost)
        {
            // payload: [protocol version][packet id][body]
            if (payload.Length < 2)
                return;

            if (payload[0] != ProtocolVersion)
            {
                VersionMismatchCount++;
                Debug.WriteLine("[NET] Dropped a packet of protocol version " + payload[0] + " (this build speaks " + ProtocolVersion + ")");
                ProtocolMismatch?.Invoke(payload[0]);
                return;
            }

            byte type = payload[1];

            Entry entry;
            if (!handlers.TryGetValue(type, out entry))
            {
                UnknownCount++;
                Debug.WriteLine("[NET] No handler for packet type " + type);
                return;
            }

            bool accepted =
                entry.Audience == PacketAudience.Any ||
                (entry.Audience == PacketAudience.FromHost && !localIsHost && sender != null && sender.IsHost) ||
                (entry.Audience == PacketAudience.FromClient && localIsHost);

            if (!accepted)
            {
                RejectedCount++;
                Debug.WriteLine("[NET] Rejected " + type + " from " + (sender == null ? "?" : sender.Gamertag));
                return;
            }

            try
            {
                using (MemoryStream stream = new MemoryStream(payload, 2, payload.Length - 2, false))
                using (BinaryReader reader = new BinaryReader(stream))
                    entry.Handler(sender, reader);
            }
            catch (Exception ex)
            {
                // a bad packet must not take the game down, but it must not vanish either
                ErrorCount++;
                Debug.WriteLine("[NET] Error handling " + type + " from " +
                    (sender == null ? "?" : sender.Gamertag) + ": " +
                    ex.GetType().Name + " " + ex.Message);
            }
        }
    }
}
