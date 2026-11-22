using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace VectorRumble
{
    /// <summary>One sound effect: the clips it plays and how loud. Mirrors what the XACT project did for the same cue.</summary>
    sealed class SoundCue
    {
        /// <summary>
        /// The clips played together; each layer holds alternatives, and one of them is picked at random each time
        /// (laserBlaster has three, playerDeath has two layers that sound together).
        /// </summary>
        public readonly string[][] Layers;

        /// <summary>The most voices of this cue at once; the oldest is cut off to make room. 0 is no limit of its own.</summary>
        public readonly int MaxVoices;

        public SoundCue(int maxVoices, params string[][] layers)
        {
            MaxVoices = maxVoices;
            Layers = layers;
        }
    }

    /// <summary>
    /// Plays the game's sound effects from a place on the screen: an explosion on the left comes out of the left ear.
    /// The clips are the loose WAVs the XACT sound bank was built from (copied to Content/Audio/Waves), played as
    /// <see cref="SoundEffectInstance"/>s with a pan from the x position. Made so it can move into VectorEngine as it is:
    /// it knows nothing of the game's world, only a position, a width and the effects level.
    /// </summary>
    sealed class PositionalSounds : IDisposable
    {
        /// <summary>The furthest pan, left or right (very hard pans tire the ears in headphones).</summary>
        public const float MaxPan = 0.7f;

        /// <summary>The volume of every XACT cue of the project: -12 dB.</summary>
        public const float CueGain = 0.2512f;

        /// <summary>Voices playing at once, over every cue (a phone has a limit on loose sounds).</summary>
        public const int MaxVoices = 24;

        const string Folder = "Audio/Waves/";

        /// <summary>The cues played from a position. A name not in this table is not handled here (it stays in the sound bank).</summary>
        public static readonly Dictionary<string, SoundCue> Cues = new Dictionary<string, SoundCue>
        {
            ["laserBlaster"] = new SoundCue(0, new[] { "tx0_fire1", "tx0_fire2", "tx0_fire3" }),
            ["rocketFire"] = new SoundCue(0, new[] { "hax2_fire", "hax2_fire2" }),
            ["asteroidTouch"] = new SoundCue(3, new[] { "damage1" }),
            ["explosionMedium"] = new SoundCue(0, new[] { "explosion1" }),
            ["explosionLarge"] = new SoundCue(0, new[] { "explosion3" }),
            ["playerDeath"] = new SoundCue(0, new[] { "explosion2" }, new[] { "explosion3" }),
            ["playerSpawn"] = new SoundCue(0, new[] { "phase_expire_alt" }),
            ["powerUpSpawn"] = new SoundCue(0, new[] { "menu_back" }),
            ["hop"] = new SoundCue(0, new[] { "menu_scroll" }),
            ["powerUpTouch"] = new SoundCue(0, new[] { "weapon_pickup_alt_retro" }),
        };

        sealed class Voice
        {
            public SoundEffectInstance Instance;
            public string Cue;
        }

        readonly Dictionary<string, SoundEffect> clips = new Dictionary<string, SoundEffect>(StringComparer.Ordinal);
        readonly HashSet<string> missing = new HashSet<string>(StringComparer.Ordinal);
        readonly List<Voice> voices = new List<Voice>();
        readonly Random random = new Random();

        /// <summary>
        /// The pan for a sound at <paramref name="x"/> on a play area <paramref name="width"/> wide: -MaxPan at the left edge,
        /// 0 in the middle, MaxPan at the right edge, and no further for a sound off the screen.
        /// </summary>
        public static float PanFor(float x, float width)
        {
            if (width <= 0f)
            {
                return 0f;
            }

            return MathHelper.Clamp((x / width * 2f - 1f) * MaxPan, -MaxPan, MaxPan);
        }

        /// <summary>
        /// Plays a cue from a position and says whether it did. False means this class does not have the cue (or its clips
        /// cannot be loaded), so the caller can play it another way.
        /// </summary>
        /// <param name="cue">The cue's name.</param>
        /// <param name="x">Where it happens along the play area.</param>
        /// <param name="width">How wide the play area is.</param>
        /// <param name="level">The player's effects volume, 0 to 1.</param>
        /// <param name="directional">False plays it from the middle (the player turned panning off).</param>
        public bool Play(string cue, float x, float width, float level, bool directional)
        {
            if (!Cues.TryGetValue(cue, out SoundCue def))
            {
                return false;
            }

            var picked = new SoundEffect[def.Layers.Length];
            for (int i = 0; i < picked.Length; i++)
            {
                string[] alternatives = def.Layers[i];
                SoundEffect clip = Load(alternatives[alternatives.Length == 1 ? 0 : random.Next(alternatives.Length)]);
                if (clip == null)
                {
                    return false;
                }

                picked[i] = clip;
            }

            Prune();
            MakeRoom(cue, def.MaxVoices);

            float volume = MathHelper.Clamp(CueGain * level, 0f, 1f);
            float pan = directional ? PanFor(x, width) : 0f;
            foreach (SoundEffect clip in picked)
            {
                try
                {
                    SoundEffectInstance instance = clip.CreateInstance();
                    instance.Volume = volume;
                    instance.Pan = pan;
                    instance.Play();
                    voices.Add(new Voice { Instance = instance, Cue = cue });
                }
                catch (Exception ex)
                {
                    // no audio device, or too many sounds at once: this one is lost
                    System.Diagnostics.Debug.WriteLine($"Sound {cue}: {ex.Message}");
                    return true;
                }
            }

            return true;
        }

        // finished voices are given back
        void Prune()
        {
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                if (voices[i].Instance.State == SoundState.Stopped)
                {
                    voices[i].Instance.Dispose();
                    voices.RemoveAt(i);
                }
            }
        }

        // the oldest voice of the cue goes first when it is at its own limit, and the oldest of all when there are too many
        void MakeRoom(string cue, int cueLimit)
        {
            if (cueLimit > 0)
            {
                int count = 0;
                foreach (Voice v in voices)
                {
                    if (v.Cue == cue)
                    {
                        count++;
                    }
                }

                for (int i = 0; count >= cueLimit && i < voices.Count; )
                {
                    if (voices[i].Cue == cue)
                    {
                        Remove(i);
                        count--;
                    }
                    else
                    {
                        i++;
                    }
                }
            }

            while (voices.Count >= MaxVoices)
            {
                Remove(0);
            }
        }

        void Remove(int index)
        {
            voices[index].Instance.Stop();
            voices[index].Instance.Dispose();
            voices.RemoveAt(index);
        }

        SoundEffect Load(string name)
        {
            if (clips.TryGetValue(name, out SoundEffect clip))
            {
                return clip;
            }

            if (missing.Contains(name))
            {
                return null;
            }

            try
            {
                using (Stream stream = TitleContainer.OpenStream(Path.Combine("Content", Folder + name + ".wav")))
                {
                    WavData wav = ReadWav(stream);
                    clip = new SoundEffect(wav.Samples, wav.SampleRate, AudioChannels.Mono);
                }

                clips[name] = clip;
                return clip;
            }
            catch (Exception ex)
            {
                missing.Add(name);
                Console.Error.WriteLine($"Sound clip {name}: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            foreach (Voice v in voices)
            {
                v.Instance.Stop();
                v.Instance.Dispose();
            }

            voices.Clear();
            foreach (SoundEffect clip in clips.Values)
            {
                clip.Dispose();
            }

            clips.Clear();
        }

        /// <summary>A clip's samples as 16 bit mono.</summary>
        internal struct WavData
        {
            public byte[] Samples;
            public int SampleRate;
        }

        /// <summary>
        /// Reads a 16 bit PCM WAV and gives it as mono. A stereo clip is mixed down: a sound can only be placed left or right
        /// if it is one channel (the OpenAL sound of the desktop and Android builds ignores the pan of a stereo clip).
        /// </summary>
        internal static WavData ReadWav(Stream stream)
        {
            var reader = new BinaryReader(stream);
            if (new string(reader.ReadChars(4)) != "RIFF")
            {
                throw new InvalidDataException("not a RIFF file");
            }

            reader.ReadInt32();
            if (new string(reader.ReadChars(4)) != "WAVE")
            {
                throw new InvalidDataException("not a WAVE file");
            }

            int channels = 0, rate = 0, bits = 0;
            while (stream.Position + 8 <= stream.Length)
            {
                string id = new string(reader.ReadChars(4));
                int size = reader.ReadInt32();
                if (id == "fmt ")
                {
                    int format = reader.ReadInt16();
                    channels = reader.ReadInt16();
                    rate = reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt16();
                    bits = reader.ReadInt16();
                    if (format != 1 || bits != 16)
                    {
                        throw new InvalidDataException("only 16 bit PCM is read");
                    }

                    stream.Position += size - 16;
                }
                else if (id == "data")
                {
                    if (channels < 1)
                    {
                        throw new InvalidDataException("data before fmt");
                    }

                    byte[] data = reader.ReadBytes(size);
                    return new WavData { Samples = Downmix(data, channels), SampleRate = rate };
                }
                else
                {
                    stream.Position += size + (size & 1);
                }
            }

            throw new InvalidDataException("no data chunk");
        }

        /// <summary>Mixes interleaved 16 bit samples of <paramref name="channels"/> channels down to one.</summary>
        internal static byte[] Downmix(byte[] data, int channels)
        {
            if (channels == 1)
            {
                return data;
            }

            int frames = data.Length / (2 * channels);
            var mono = new byte[frames * 2];
            for (int f = 0; f < frames; f++)
            {
                int sum = 0;
                for (int c = 0; c < channels; c++)
                {
                    sum += BitConverter.ToInt16(data, (f * channels + c) * 2);
                }

                short mixed = (short)(sum / channels);
                mono[f * 2] = (byte)(mixed & 0xFF);
                mono[f * 2 + 1] = (byte)((mixed >> 8) & 0xFF);
            }

            return mono;
        }
    }
}
