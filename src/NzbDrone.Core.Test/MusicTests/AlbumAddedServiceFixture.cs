using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class AlbumAddedServiceFixture : CoreTest<AlbumAddedService>
    {
        [Test]
        public void should_apply_song_mode_monitoring_for_pending_albums()
        {
            var songModeAlbum = Builder<Album>.CreateNew()
                .With(a => a.Id = 10)
                .With(a => a.AddOptions = new AddAlbumOptions { MonitorTrackTitles = new List<string> { "Track One" } })
                .Build();

            var plainAlbum = Builder<Album>.CreateNew()
                .With(a => a.Id = 11)
                .With(a => a.AddOptions = new AddAlbumOptions())
                .Build();

            Mocker.GetMock<IAlbumService>()
                .Setup(s => s.GetAlbumsByArtist(5))
                .Returns(new List<Album> { songModeAlbum, plainAlbum });

            var matchedTrack = Builder<Track>.CreateNew().With(t => t.Id = 99).Build();

            Mocker.GetMock<ITrackService>()
                .Setup(s => s.SetMonitoredByTitle(10, It.Is<List<string>>(l => l.Single() == "Track One")))
                .Returns(new List<Track> { matchedTrack });

            Subject.SearchForRecentlyAdded(5);

            Mocker.GetMock<ITrackService>()
                .Verify(s => s.SetMonitoredByTitle(10, It.Is<List<string>>(l => l.Single() == "Track One")), Times.Once());

            Mocker.GetMock<ITrackService>()
                .Verify(s => s.SetMonitoredByTitle(11, It.IsAny<List<string>>()), Times.Never());
        }

        [Test]
        public void should_clear_pending_titles_after_applying()
        {
            var songModeAlbum = Builder<Album>.CreateNew()
                .With(a => a.Id = 10)
                .With(a => a.AddOptions = new AddAlbumOptions { MonitorTrackTitles = new List<string> { "Track One" } })
                .Build();

            Mocker.GetMock<IAlbumService>()
                .Setup(s => s.GetAlbumsByArtist(5))
                .Returns(new List<Album> { songModeAlbum });

            Mocker.GetMock<ITrackService>()
                .Setup(s => s.SetMonitoredByTitle(10, It.IsAny<List<string>>()))
                .Returns(new List<Track>());

            Subject.SearchForRecentlyAdded(5);

            songModeAlbum.AddOptions.MonitorTrackTitles.Should().BeEmpty();

            Mocker.GetMock<IAlbumService>()
                .Verify(s => s.SetAddOptions(It.Is<List<Album>>(l => l.Contains(songModeAlbum))), Times.Once());
        }

        [Test]
        public void should_do_nothing_when_no_albums_have_pending_titles()
        {
            var plainAlbum = Builder<Album>.CreateNew()
                .With(a => a.Id = 11)
                .With(a => a.AddOptions = new AddAlbumOptions())
                .Build();

            Mocker.GetMock<IAlbumService>()
                .Setup(s => s.GetAlbumsByArtist(5))
                .Returns(new List<Album> { plainAlbum });

            Subject.SearchForRecentlyAdded(5);

            Mocker.GetMock<ITrackService>()
                .Verify(s => s.SetMonitoredByTitle(It.IsAny<int>(), It.IsAny<List<string>>()), Times.Never());
        }
    }
}
