using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.AlbumRepositoryTests
{
    [TestFixture]
    public class AlbumServiceFixture : CoreTest<AlbumService>
    {
        private List<Album> _albums;

        [SetUp]
        public void Setup()
        {
            _albums = new List<Album>();
            _albums.Add(new Album
            {
                Title = "ANThology",
                CleanTitle = "anthology",
            });

            _albums.Add(new Album
            {
                Title = "+",
                CleanTitle = "",
            });

            Mocker.GetMock<IAlbumRepository>()
                .Setup(s => s.GetAlbumsByArtistMetadataId(It.IsAny<int>()))
                .Returns(_albums);
        }

        private void GivenAlbumWithTracks(int albumId, params bool[] monitoredFlags)
        {
            var tracks = new List<Track>();

            for (var i = 0; i < monitoredFlags.Length; i++)
            {
                tracks.Add(new Track { Id = i + 1, Monitored = monitoredFlags[i] });
            }

            Mocker.GetMock<IAlbumRepository>()
                .Setup(s => s.Get(albumId))
                .Returns(new Album { Id = albumId });

            Mocker.GetMock<ITrackService>()
                .Setup(s => s.GetTracksByAlbum(albumId))
                .Returns(tracks);
        }

        [Test]
        public void should_not_cascade_monitored_over_an_individual_track_selection()
        {
            GivenAlbumWithTracks(7, false, true, false);

            Subject.SetAlbumMonitored(7, true);

            Mocker.GetMock<ITrackService>()
                .Verify(s => s.SetMonitored(It.IsAny<IEnumerable<int>>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_cascade_monitored_when_no_track_is_selected()
        {
            GivenAlbumWithTracks(7, false, false);

            Subject.SetAlbumMonitored(7, true);

            Mocker.GetMock<ITrackService>()
                .Verify(s => s.SetMonitored(It.IsAny<IEnumerable<int>>(), true), Times.Once());
        }

        [Test]
        public void should_always_cascade_unmonitored()
        {
            GivenAlbumWithTracks(7, true, false);

            Subject.SetAlbumMonitored(7, false);

            Mocker.GetMock<ITrackService>()
                .Verify(s => s.SetMonitored(It.IsAny<IEnumerable<int>>(), false), Times.Once());
        }

        private void GivenSimilarAlbum()
        {
            _albums.Add(new Album
            {
                Title = "ANThology2",
                CleanTitle = "anthology2",
            });
        }

        [TestCase("ANTholog", "ANThology")]
        [TestCase("antholoyg", "ANThology")]
        [TestCase("ANThology CD", "ANThology")]
        [TestCase("ANThology CD xxxx (Remastered) - [Oh please why do they do this?]", "ANThology")]
        [TestCase("+ (Plus) - I feel the need for redundant information in the title field", "+")]
        public void should_find_album_in_db_by_inexact_title(string title, string expected)
        {
            var album = Subject.FindByTitleInexact(0, title);

            album.Should().NotBeNull();
            album.Title.Should().Be(expected);
        }

        [TestCase("ANTholog")]
        [TestCase("antholoyg")]
        [TestCase("ANThology CD")]
        [TestCase("÷")]
        [TestCase("÷ (Divide)")]
        public void should_not_find_album_in_db_by_inexact_title_when_two_similar_matches(string title)
        {
            GivenSimilarAlbum();
            var album = Subject.FindByTitleInexact(0, title);

            album.Should().BeNull();
        }
    }
}
