using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.AlbumRepositoryTests
{
    [TestFixture]
    public class AlbumsWithoutFilesFixture : DbTest<AlbumService, Album>
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
                ReleaseDate = DateTime.UtcNow.AddDays(-1),
                AlbumReleases = new List<AlbumRelease> { _release },
            };

            _albumRepo.Insert(_album);
            _release.AlbumId = _album.Id;
            _releaseRepo.Insert(_release);
            _albumRepo.Update(_album);
        }

        private void GivenTrack(bool monitored)
        {
            _trackRepo.InsertMany(new List<Track>
            {
                new Track
                {
                    ForeignTrackId = "ft",
                    ForeignRecordingId = "fr1",
                    Title = "t",
                    TrackNumber = "1",
                    AlbumReleaseId = _release.Id,
                    ArtistMetadataId = _artist.ArtistMetadataId,
                    Monitored = monitored
                }
            });
        }

        private PagingSpec<Album> Paging()
        {
            return new PagingSpec<Album> { Page = 1, PageSize = 10, SortKey = "Id", SortDirection = SortDirection.Ascending };
        }

        [Test]
        public void should_return_album_with_a_missing_monitored_track()
        {
            GivenTrack(monitored: true);

            _albumRepo.AlbumsWithoutFiles(Paging()).Records.Should().Contain(a => a.Id == _album.Id);
        }

        [Test]
        public void should_ignore_album_when_missing_tracks_are_all_unmonitored()
        {
            GivenTrack(monitored: false);

            _albumRepo.AlbumsWithoutFiles(Paging()).Records.Should().NotContain(a => a.Id == _album.Id);
        }
    }
}
