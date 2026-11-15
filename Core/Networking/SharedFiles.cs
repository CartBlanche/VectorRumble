using System;
using System.Collections.Generic;

namespace VectorRumble.Networking
{
    public enum SharedFileKind : byte
    {
        Arena = 0,
        Ship = 1,
    }

    /// <summary>
    /// The community arenas and ships known to a network game: the ones this machine has in its folders (asked of
    /// <c>findLocal</c>) and the ones that came over the network, kept in memory for the session. A file is known by its id, the
    /// SHA-256 of its bytes, so every machine means the same file. Nothing is written to disk.
    /// </summary>
    public sealed class SharedFiles
    {
        readonly Dictionary<string, (SharedFileKind kind, byte[] bytes)> received = new Dictionary<string, (SharedFileKind, byte[])>();
        readonly Func<string, byte[]> findLocal;

        /// <param name="findLocal">The bytes of the community arena or ship with this id from this machine's own folders, or null</param>
        public SharedFiles(Func<string, byte[]> findLocal = null)
        {
            this.findLocal = findLocal ?? (_ => null);
        }

        /// <summary>The bytes of the file with this id, or null if this machine does not have it.</summary>
        public byte[] Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            if (received.TryGetValue(id, out var file))
                return file.bytes;
            return findLocal(id);
        }

        public bool Has(string id)
        {
            return Get(id) != null;
        }

        /// <summary>
        /// Keeps a file that came over the network, if it is what it says it is: within the size limit, its id the SHA-256 of
        /// its bytes, and the bytes a valid arena or ship of that kind. Returns false, with the reason, if not.
        /// </summary>
        public bool TryAdd(SharedFileKind kind, string id, byte[] bytes, out string reason)
        {
            reason = null;
            if (bytes == null || bytes.Length == 0 || bytes.Length > CommunityContent.MaxFileBytes)
            {
                reason = "The file is empty or too big.";
                return false;
            }

            if (CommunityContent.IdOf(bytes) != id)
            {
                reason = "The file does not match its id.";
                return false;
            }

            bool valid = kind == SharedFileKind.Arena
                ? CommunityContent.TryParseArena(bytes, out Arena _, out reason)
                : CommunityContent.TryParseShip(bytes, out Ship _, out reason);
            if (!valid)
                return false;

            received[id] = (kind, bytes);
            return true;
        }
    }
}
