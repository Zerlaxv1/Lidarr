using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class TrackRepositoryFixture : DbTest<TrackRepository, Track>
    {
        private AlbumRelease _monitoredRelease;
        private AlbumRelease _unmonitoredRelease;
        private ArtistMetadata _artistMeta;

        [SetUp]
        public void Setup()
        {
            var meta = Builder<ArtistMetadata>.CreateNew()
                .With(a => a.Id = 0)
                .Build();
            Db.Insert(meta);
            _artistMeta = meta;

            var artist = Builder<Artist>.CreateNew()
                .With(a => a.ArtistMetadataId = meta.Id)
                .With(a => a.Id = 0)
                .Build();
            Db.Insert(artist);

            var album = Builder<Album>.CreateNew()
                .With(a => a.Id = 0)
                .With(a => a.ArtistMetadataId = artist.ArtistMetadataId)
                .Build();
            Db.Insert(album);

            _monitoredRelease = Builder<AlbumRelease>.CreateNew()
                .With(a => a.Id = 0)
                .With(a => a.AlbumId = album.Id)
                .With(a => a.Monitored = true)
                .With(a => a.ForeignReleaseId = "monitored-release-1")
                .Build();
            Db.Insert(_monitoredRelease);

            _unmonitoredRelease = Builder<AlbumRelease>.CreateNew()
                .With(a => a.Id = 0)
                .With(a => a.AlbumId = album.Id)
                .With(a => a.Monitored = false)
                .With(a => a.ForeignReleaseId = "unmonitored-release-1")
                .Build();
            Db.Insert(_unmonitoredRelease);

            var tracks = Builder<Track>.CreateListOfSize(4)
                .All()
                .With(a => a.Id = 0)
                .With(a => a.ArtistMetadataId = meta.Id)
                .TheFirst(1)
                .With(a => a.AlbumReleaseId = _monitoredRelease.Id)
                .With(a => a.Title = "Muscle Museum")
                .TheNext(1)
                .With(a => a.AlbumReleaseId = _monitoredRelease.Id)
                .With(a => a.Title = "Museum Piece")
                .TheNext(1)
                .With(a => a.AlbumReleaseId = _monitoredRelease.Id)
                .With(a => a.Title = "Bliss")
                .TheNext(1)
                .With(a => a.AlbumReleaseId = _unmonitoredRelease.Id)
                .With(a => a.Title = "Muscle Museum")
                .Build();
            Db.InsertMany(tracks);
        }

        [Test]
        public void should_find_tracks_by_partial_title_on_monitored_release_only()
        {
            var results = Subject.SearchTracksByTitle("Museum", 10);

            results.Should().HaveCount(2);
            results.Should().OnlyContain(t => t.AlbumReleaseId == _monitoredRelease.Id);
        }

        [Test]
        public void should_populate_joined_artist_and_album()
        {
            var results = Subject.SearchTracksByTitle("Museum", 10);

            results.Should().OnlyContain(t => t.Artist.IsLoaded && t.Album != null);
            results.Should().OnlyContain(t => t.Artist.Value.ArtistMetadataId == _artistMeta.Id);
        }

        [Test]
        public void should_respect_limit()
        {
            var results = Subject.SearchTracksByTitle("Museum", 1);

            results.Should().HaveCount(1);
        }

        [Test]
        public void should_return_empty_when_no_match()
        {
            var results = Subject.SearchTracksByTitle("nonexistent title", 10);

            results.Should().BeEmpty();
        }
    }
}
