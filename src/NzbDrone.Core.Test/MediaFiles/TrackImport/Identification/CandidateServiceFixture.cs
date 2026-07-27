using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.TrackImport;
using NzbDrone.Core.MediaFiles.TrackImport.Identification;
using NzbDrone.Core.Music;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.TrackImport.Identification
{
    [TestFixture]
    public class GetCandidatesFixture : CoreTest<CandidateService>
    {
        private ArtistMetadata _artist;

        [SetUp]
        public void Setup()
        {
            _artist = Builder<ArtistMetadata>
                .CreateNew()
                .With(x => x.Name = "artist")
                .Build();
        }

        private List<Track> GivenTracks(int count)
        {
             return Builder<Track>
                .CreateListOfSize(count)
                .All()
                .With(x => x.ArtistMetadata = _artist)
                .Build()
                .ToList();
        }

        private ParsedTrackInfo GivenParsedTrackInfo(Track track, AlbumRelease release)
        {
            return Builder<ParsedTrackInfo>
                .CreateNew()
                .With(x => x.Title = track.Title)
                .With(x => x.AlbumTitle = release.Title)
                .With(x => x.Disambiguation = release.Disambiguation)
                .With(x => x.ReleaseMBId = release.ForeignReleaseId)
                .With(x => x.ArtistTitle = track.ArtistMetadata.Value.Name)
                .With(x => x.TrackNumbers = new[] { track.AbsoluteTrackNumber })
                .With(x => x.RecordingMBId = track.ForeignRecordingId)
                .With(x => x.Country = IsoCountries.Find("US"))
                .With(x => x.Label = release.Label.First())
                .With(x => x.Year = (uint)release.Album.Value.ReleaseDate.Value.Year)
                .Build();
        }

        private List<LocalTrack> GivenLocalTracks(List<Track> tracks, AlbumRelease release)
        {
            var output = Builder<LocalTrack>
                .CreateListOfSize(tracks.Count)
                .Build()
                .ToList();

            for (var i = 0; i < tracks.Count; i++)
            {
                output[i].FileTrackInfo = GivenParsedTrackInfo(tracks[i], release);
            }

            return output;
        }

        private AlbumRelease GivenAlbumRelease(string title, List<Track> tracks)
        {
            var album = Builder<Album>
                .CreateNew()
                .With(x => x.Title = title)
                .With(x => x.ArtistMetadata = _artist)
                .Build();

            var media = Builder<Medium>
                .CreateListOfSize(1)
                .Build()
                .ToList();

            return Builder<AlbumRelease>
                .CreateNew()
                .With(x => x.Tracks = tracks)
                .With(x => x.Title = title)
                .With(x => x.Album = album)
                .With(x => x.Media = media)
                .With(x => x.Country = new List<string>())
                .With(x => x.Label = new List<string> { "label" })
                .With(x => x.ForeignReleaseId = null)
                .Build();
        }

        private LocalAlbumRelease GivenLocalAlbumRelease()
        {
            var tracks = GivenTracks(3);
            var release = GivenAlbumRelease("album", tracks);
            var localTracks = GivenLocalTracks(tracks, release);

            return new LocalAlbumRelease(localTracks);
        }

        [Test]
        public void get_candidates_by_fingerprint_should_not_fail_if_fingerprint_lookup_returned_null()
        {
            Mocker.GetMock<IFingerprintingService>()
                .Setup(x => x.Lookup(It.IsAny<List<LocalTrack>>(), It.IsAny<double>()))
                .Callback((List<LocalTrack> x, double thres) =>
                    {
                        foreach (var track in x)
                        {
                            track.AcoustIdResults = null;
                        }
                    });

            Mocker.GetMock<IReleaseService>()
                .Setup(x => x.GetReleasesByRecordingIds(It.IsAny<List<string>>()))
                .Returns(new List<AlbumRelease>());

            var local = GivenLocalAlbumRelease();

            Subject.GetDbCandidatesFromFingerprint(local, null, false).Should().BeEquivalentTo(new List<CandidateAlbumRelease>());
        }

        [Test]
        public void get_candidates_should_only_return_specified_release_if_set()
        {
            var tracks = GivenTracks(3);
            var release = GivenAlbumRelease("album", tracks);
            var localTracks = GivenLocalTracks(tracks, release);
            var localAlbumRelease = new LocalAlbumRelease(localTracks);
            var idOverrides = new IdentificationOverrides
            {
                AlbumRelease = release
            };

            Subject.GetDbCandidatesFromTags(localAlbumRelease, idOverrides, false).Should().BeEquivalentTo(
                new List<CandidateAlbumRelease> { new CandidateAlbumRelease(release) });
        }

        [Test]
        public void get_candidates_should_use_consensus_release_id()
        {
            var tracks = GivenTracks(3);
            var release = GivenAlbumRelease("album", tracks);
            release.ForeignReleaseId = "xxx";
            var localTracks = GivenLocalTracks(tracks, release);
            var localAlbumRelease = new LocalAlbumRelease(localTracks);

            Mocker.GetMock<IReleaseService>()
                  .Setup(x => x.GetReleaseByForeignReleaseId("xxx", true))
                  .Returns(release);

            Subject.GetDbCandidatesFromTags(localAlbumRelease, null, false).Should().BeEquivalentTo(
                new List<CandidateAlbumRelease> { new CandidateAlbumRelease(release) });
        }

        private LocalAlbumRelease GivenUnmatchableArtist(string artistTag, AlbumRelease release)
        {
            var tracks = GivenTracks(3);
            var localTracks = GivenLocalTracks(tracks, release);
            localTracks.ForEach(x => x.FileTrackInfo.ArtistTitle = artistTag);

            Mocker.GetMock<IArtistService>()
                  .Setup(x => x.GetCandidates(It.IsAny<string>()))
                  .Returns(new List<Artist>());

            Mocker.GetMock<IReleaseService>()
                  .Setup(x => x.GetReleasesByAlbum(release.Album.Value.Id))
                  .Returns(new List<AlbumRelease> { release });

            return new LocalAlbumRelease(localTracks);
        }

        [TestCase("Various Artists")]
        [TestCase("Varios Artistas")]
        [TestCase("Kenshi Yonezu")]
        public void get_candidates_should_fall_back_to_album_title_when_artist_matches_nothing(string artistTag)
        {
            var release = GivenAlbumRelease("album", GivenTracks(3));
            var localAlbumRelease = GivenUnmatchableArtist(artistTag, release);

            Mocker.GetMock<IAlbumService>()
                  .Setup(x => x.GetCandidatesByTitle("album"))
                  .Returns(new List<Album> { release.Album.Value });

            Subject.GetDbCandidatesFromTags(localAlbumRelease, null, false).Should().BeEquivalentTo(
                new List<CandidateAlbumRelease> { new CandidateAlbumRelease(release) });
        }

        [Test]
        public void get_candidates_should_not_fall_back_to_album_title_when_the_artist_matches()
        {
            var release = GivenAlbumRelease("album", GivenTracks(3));
            var album = release.Album.Value;
            var localTracks = GivenLocalTracks(GivenTracks(3), release);
            var localAlbumRelease = new LocalAlbumRelease(localTracks);

            var artist = Builder<Artist>.CreateNew().With(x => x.ArtistMetadataId = 1).Build();

            Mocker.GetMock<IArtistService>()
                  .Setup(x => x.GetCandidates(It.IsAny<string>()))
                  .Returns(new List<Artist> { artist });

            Mocker.GetMock<IAlbumService>()
                  .Setup(x => x.GetCandidates(artist.ArtistMetadataId, "album"))
                  .Returns(new List<Album> { album });

            Mocker.GetMock<IReleaseService>()
                  .Setup(x => x.GetReleasesByAlbum(album.Id))
                  .Returns(new List<AlbumRelease> { release });

            Subject.GetDbCandidatesFromTags(localAlbumRelease, null, false).Should().BeEquivalentTo(
                new List<CandidateAlbumRelease> { new CandidateAlbumRelease(release) });

            Mocker.GetMock<IAlbumService>()
                  .Verify(x => x.GetCandidatesByTitle(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void get_candidates_should_not_look_up_a_blank_album_title()
        {
            var release = GivenAlbumRelease("album", GivenTracks(3));
            var localAlbumRelease = GivenUnmatchableArtist("Varios Artistas", release);
            localAlbumRelease.LocalTracks.ForEach(x => x.FileTrackInfo.AlbumTitle = null);

            Subject.GetDbCandidatesFromTags(localAlbumRelease, null, false).Should().BeEmpty();

            Mocker.GetMock<IAlbumService>()
                  .Verify(x => x.GetCandidatesByTitle(It.IsAny<string>()), Times.Never());
        }
    }
}
