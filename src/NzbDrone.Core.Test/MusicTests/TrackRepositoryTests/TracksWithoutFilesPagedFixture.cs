using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.TrackRepositoryTests
{
    [TestFixture]
    public class TracksWithoutFilesPagedFixture : DbTest<TrackService, Track>
    {
        private Artist _artist;
        private Album _album;
        private AlbumRelease _release;
        private AlbumRepository _albumRepo;
        private ReleaseRepository _releaseRepo;
        private TrackRepository _trackRepo;

        [SetUp]
        public void Setup()
        {
            _albumRepo = Mocker.Resolve<AlbumRepository>();
            _releaseRepo = Mocker.Resolve<ReleaseRepository>();
            _trackRepo = Mocker.Resolve<TrackRepository>();

            var metadata = Builder<ArtistMetadata>.CreateNew()
                .With(a => a.Id = 0)
                .With(a => a.Name = "artist")
                .BuildNew();

            _artist = Builder<Artist>.CreateNew()
                .With(a => a.Id = 0)
                .With(a => a.Metadata = metadata)
                .With(a => a.Monitored = true)
                .With(a => a.CleanName = "artist")
                .With(a => a.ForeignArtistId = "fa")
                .BuildNew();

            Mocker.Resolve<ArtistMetadataRepository>().Insert(metadata);
            _artist.ArtistMetadataId = metadata.Id;
            Mocker.Resolve<ArtistRepository>().Insert(_artist);

            _release = Builder<AlbumRelease>.CreateNew()
                .With(e => e.Id = 0)
                .With(e => e.ForeignReleaseId = "fr")
                .With(e => e.Monitored = true)
                .Build();

            _album = new Album
            {
                Title = "Alb",
                ForeignAlbumId = "1",
                CleanTitle = "alb",
                ArtistMetadataId = metadata.Id,
                AlbumType = "",
                Monitored = true,
                ReleaseDate = DateTime.UtcNow.AddDays(-1),
                AlbumReleases = new List<AlbumRelease> { _release },
            };

            _albumRepo.Insert(_album);
            _release.AlbumId = _album.Id;
            _releaseRepo.Insert(_release);
            _albumRepo.Update(_album);
        }

        private Track GivenTrack(string foreignId, bool monitored, int trackFileId = 0)
        {
            var track = new Track
            {
                ForeignTrackId = foreignId,
                ForeignRecordingId = "r" + foreignId,
                Title = "t" + foreignId,
                TrackNumber = "1",
                AlbumReleaseId = _release.Id,
                ArtistMetadataId = _artist.ArtistMetadataId,
                Monitored = monitored,
                TrackFileId = trackFileId
            };

            _trackRepo.InsertMany(new List<Track> { track });
            return track;
        }

        private PagingSpec<Track> Paging()
        {
            return new PagingSpec<Track> { Page = 1, PageSize = 10, SortKey = "albums.releaseDate", SortDirection = SortDirection.Descending };
        }

        [Test]
        public void should_return_monitored_fileless_track_with_album_and_artist_populated()
        {
            var wanted = GivenTrack("ft1", monitored: true);
            GivenTrack("ft2", monitored: false);

            var result = _trackRepo.TracksWithoutFilesPaged(Paging(), true);

            result.TotalRecords.Should().Be(1);
            result.Records.Should().ContainSingle(t => t.Id == wanted.Id);
            result.Records[0].Album.Should().NotBeNull();
            result.Records[0].Album.Id.Should().Be(_album.Id);
            result.Records[0].Artist.Value.Id.Should().Be(_artist.Id);
        }

        [Test]
        public void unmonitored_filter_should_return_the_unmonitored_track()
        {
            GivenTrack("ft1", monitored: true);
            var skipped = GivenTrack("ft2", monitored: false);

            var result = _trackRepo.TracksWithoutFilesPaged(Paging(), false);

            result.Records.Should().ContainSingle(t => t.Id == skipped.Id);
        }
    }
}
