using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using VectorEngine.Networking;
using VectorRumble.Networking;
using Xunit;

namespace VectorRumble.Tests
{
    public class SharedFilesTests
    {
        static byte[] Bytes(string xml) => Encoding.UTF8.GetBytes(xml);

        static readonly byte[] ArenaFile = Bytes("<XnaContent><Asset Type=\"VectorRumble:Arena\"><Name>Maze</Name><Color>FF00FFFF</Color><Walls>0.1 0.1 0.9 0.1</Walls></Asset></XnaContent>");
        static readonly byte[] ShipFile = Bytes("<XnaContent><Asset Type=\"VectorRumble:Ship\"><Mass>20</Mass><Radius>15</Radius><Color>FF00FF00</Color><Shape><Points>0 -10 8 8 -8 8</Points></Shape><Name>Dart</Name></Asset></XnaContent>");

        static byte[] Write(INetworkPacket packet)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                packet.Serialize(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        static BinaryReader Reader(byte[] bytes) => new BinaryReader(new MemoryStream(bytes));

        [Fact]
        public void A_file_that_is_what_it_says_is_kept()
        {
            var files = new SharedFiles();
            string id = CommunityContent.IdOf(ArenaFile);

            Assert.False(files.Has(id));
            Assert.True(files.TryAdd(SharedFileKind.Arena, id, ArenaFile, out _));
            Assert.True(files.Has(id));
            Assert.Equal(ArenaFile, files.Get(id));
        }

        [Fact]
        public void Files_this_machine_has_in_its_folders_count_without_being_sent()
        {
            string id = CommunityContent.IdOf(ShipFile);
            var files = new SharedFiles(wanted => wanted == id ? ShipFile : null);

            Assert.True(files.Has(id));
            Assert.False(files.Has(new string('0', 64)));
        }

        [Fact]
        public void A_file_with_the_wrong_id_is_refused()
        {
            var files = new SharedFiles();

            Assert.False(files.TryAdd(SharedFileKind.Arena, new string('0', 64), ArenaFile, out string reason));
            Assert.Contains("id", reason);
            Assert.False(files.Has(new string('0', 64)));
        }

        [Fact]
        public void A_file_of_the_wrong_kind_is_refused()
        {
            var files = new SharedFiles();

            Assert.False(files.TryAdd(SharedFileKind.Ship, CommunityContent.IdOf(ArenaFile), ArenaFile, out _));
            Assert.False(files.TryAdd(SharedFileKind.Arena, CommunityContent.IdOf(ShipFile), ShipFile, out _));
        }

        [Fact]
        public void Garbage_empty_and_oversize_files_are_refused()
        {
            var files = new SharedFiles();
            byte[] junk = Bytes("not xml");
            byte[] huge = new byte[CommunityContent.MaxFileBytes + 1];

            Assert.False(files.TryAdd(SharedFileKind.Arena, CommunityContent.IdOf(junk), junk, out _));
            Assert.False(files.TryAdd(SharedFileKind.Arena, CommunityContent.IdOf(huge), huge, out _));
            Assert.False(files.TryAdd(SharedFileKind.Arena, new string('0', 64), new byte[0], out _));
            Assert.False(files.TryAdd(SharedFileKind.Arena, new string('0', 64), null, out _));
        }

        [Fact]
        public void File_data_round_trips_for_both_directions()
        {
            string id = CommunityContent.IdOf(ShipFile);
            var down = FileDataPacket.Deserialize(Reader(Write(new FileDataPacket { Kind = SharedFileKind.Ship, Id = id, Data = ShipFile })));
            Assert.Equal(PacketType.FileData, down.Packet);
            Assert.Equal(SharedFileKind.Ship, down.Kind);
            Assert.Equal(id, down.Id);
            Assert.Equal(ShipFile, down.Data);

            var up = new FileDataPacket { Packet = PacketType.FileUpload, Kind = SharedFileKind.Ship, Id = id, Data = ShipFile };
            Assert.Equal((byte)PacketType.FileUpload, up.Type);
            Assert.Equal((byte)PacketType.FileData, new FileDataPacket().Type);
            Assert.Equal(PacketType.FileUpload, FileDataPacket.Deserialize(Reader(Write(up)), PacketType.FileUpload).Packet);
        }

        [Fact]
        public void Damaged_file_packets_are_rejected_not_played()
        {
            string id = CommunityContent.IdOf(ShipFile);
            byte[] good = Write(new FileDataPacket { Kind = SharedFileKind.Ship, Id = id, Data = ShipFile });

            // cut short: the length says more than is there
            byte[] cut = new byte[good.Length - 5];
            System.Array.Copy(good, cut, cut.Length);
            Assert.ThrowsAny<System.Exception>(() => FileDataPacket.Deserialize(Reader(cut)));

            // an unknown kind
            byte[] kind = (byte[])good.Clone();
            kind[0] = 9;
            Assert.Throws<InvalidDataException>(() => FileDataPacket.Deserialize(Reader(kind)));

            // a length no file can have: nothing is allocated for it
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)SharedFileKind.Arena);
                writer.Write(id);
                writer.Write(int.MaxValue);
                writer.Flush();
                Assert.Throws<InvalidDataException>(() => FileDataPacket.Deserialize(Reader(stream.ToArray())));
            }

            // a bad id
            Assert.Throws<InvalidDataException>(() => FileDataPacket.Deserialize(Reader(Write(new FileDataPacket { Kind = SharedFileKind.Ship, Id = "../etc", Data = ShipFile }))));
        }

        [Fact]
        public void A_file_request_carries_an_id_and_nothing_else_is_accepted()
        {
            string id = new string('b', 64);
            Assert.Equal(id, FileRequestPacket.Deserialize(Reader(Write(new FileRequestPacket { Id = id }))).Id);
            Assert.Equal((byte)PacketType.FileRequest, new FileRequestPacket().Type);
            Assert.Throws<InvalidDataException>(() => FileRequestPacket.Deserialize(Reader(Write(new FileRequestPacket { Id = "x" }))));
            Assert.Throws<InvalidDataException>(() => FileRequestPacket.Deserialize(Reader(Write(new FileRequestPacket { Id = "" }))));
        }

        [Fact]
        public void The_lobby_carries_each_players_ship_id_and_whether_they_have_the_files()
        {
            string id = CommunityContent.IdOf(ShipFile);
            var players = new List<LobbyPlayerInfo>
            {
                new LobbyPlayerInfo { Id = "a", Name = "Ann", Ship = 2, Color = Color.Red, Ready = true },
                new LobbyPlayerInfo { Id = "b", Name = "Bob", ShipId = id, Color = Color.Blue, Ready = false, HasFiles = false },
            };

            var sync = LobbySyncPacket.Deserialize(Reader(Write(new LobbySyncPacket { Rules = new MatchRules(), Players = players })));

            Assert.Equal(string.Empty, sync.Players[0].ShipId);
            Assert.True(sync.Players[0].HasFiles);
            Assert.Equal(id, sync.Players[1].ShipId);
            Assert.False(sync.Players[1].HasFiles);

            var update = LobbyUpdatePacket.Deserialize(Reader(Write(new LobbyUpdatePacket { ShipId = id, Ready = true, HasFiles = false })));
            Assert.Equal(id, update.ShipId);
            Assert.False(update.HasFiles);
        }

        [Fact]
        public void A_lobby_packet_with_a_bad_ship_id_is_dropped()
        {
            Assert.Throws<InvalidDataException>(() => LobbyUpdatePacket.Deserialize(Reader(Write(new LobbyUpdatePacket { ShipId = "nope" }))));
            Assert.Throws<InvalidDataException>(() => LobbySyncPacket.Deserialize(Reader(Write(new LobbySyncPacket
            {
                Rules = new MatchRules(),
                Players = new List<LobbyPlayerInfo> { new LobbyPlayerInfo { Id = "a", Name = "A", ShipId = "nope" } },
            }))));
        }
    }
}
