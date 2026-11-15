using System;
using System.IO;

namespace VectorRumble
{
    /// <summary>
    /// The rules of one match. Every player in a match plays by the same rules, so these
    /// are chosen on the game setup screen (by the host in a network game) and never
    /// depend on one machine's own settings, which live in <see cref="WorldRules"/>.
    /// </summary>
    public class MatchRules
    {
        public const int MinScoreLimit = 5;
        public const int MaxScoreLimit = 25;

        /// <summary>The rules for the next match; the game setup screen edits them.</summary>
        public static MatchRules Current { get; set; } = new MatchRules();

        public AsteroidDensity AsteroidDensity = AsteroidDensity.Low;
        public bool ControllersCanShootInAllDirections;

        /// <summary>An arena's name, or <see cref="Strings.Arena_Random"/> to have one picked for the match.</summary>
        public string Arena = Strings.Arena_Random;

        /// <summary>
        /// For an arena that did not come with the game: the id of its file (see <see cref="CommunityContent.IdOf"/>), so every machine
        /// plays the same file even if two arenas share a name. Empty for the arenas that come with the game and for "Random".
        /// </summary>
        public string ArenaId = string.Empty;

        public int ScoreLimit = 10;

        public const int MaxAIPilots = 3;

        /// <summary>
        /// How many computer pilots fly against the player in a game on this machine alone, 0 to <see cref="MaxAIPilots"/>.
        /// Not sent on the network: a network match has only people in it.
        /// </summary>
        public int AIPilots = 1;

        public bool IsRandomArena => Arena == Strings.Arena_Random;

        /// <summary>A file id is empty or 64 lower-case hex digits (a SHA-256).</summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return true;
            if (id.Length != 64)
                return false;
            foreach (char c in id)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    return false;
            return true;
        }

        public MatchRules Clone() => (MatchRules)MemberwiseClone();

        /// <summary>
        /// Writes the rules for the network. "Random" is written as an empty name, because the
        /// word is translated and so differs between machines.
        /// </summary>
        public void Write(BinaryWriter writer)
        {
            writer.Write(IsRandomArena ? string.Empty : Arena);
            writer.Write(IsRandomArena ? string.Empty : ArenaId ?? string.Empty);
            writer.Write((byte)AsteroidDensity);
            writer.Write((byte)ScoreLimit);
            writer.Write(ControllersCanShootInAllDirections);
        }

        /// <summary>
        /// Reads rules written by <see cref="Write"/>. Throws <see cref="InvalidDataException"/> for values
        /// no game screen can produce, so a damaged packet is dropped rather than played.
        /// </summary>
        public static MatchRules Read(BinaryReader reader)
        {
            string arena = reader.ReadString();
            string arenaId = reader.ReadString();
            byte density = reader.ReadByte();
            byte scoreLimit = reader.ReadByte();
            bool shootInAllDirections = reader.ReadBoolean();

            if (!Enum.IsDefined(typeof(AsteroidDensity), (int)density))
                throw new InvalidDataException("Unknown asteroid density " + density);
            if (!IsValidId(arenaId))
                throw new InvalidDataException("Bad arena id");
            if (scoreLimit < MinScoreLimit || scoreLimit > MaxScoreLimit)
                throw new InvalidDataException("Score limit " + scoreLimit + " out of range");

            return new MatchRules
            {
                Arena = arena.Length == 0 ? Strings.Arena_Random : arena,
                ArenaId = arena.Length == 0 ? string.Empty : arenaId,
                AsteroidDensity = (AsteroidDensity)density,
                ScoreLimit = scoreLimit,
                AIPilots = 0,
                ControllersCanShootInAllDirections = shootInAllDirections,
            };
        }
    }
}
