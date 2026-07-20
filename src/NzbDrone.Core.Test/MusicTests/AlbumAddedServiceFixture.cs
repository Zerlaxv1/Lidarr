using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

using It = Moq.It;

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

        [Test]
        public void should_search_matched_tracks_when_search_for_new_album_flag_set()
        {
            var songModeAlbum = Builder<Album>.CreateNew()
                .With(a => a.Id = 10)
                .With(a => a.AddOptions = new AddAlbumOptions { MonitorTrackTitles = new List<string> { "Track One" }, SearchTrackTitles = new List<string> { "Track One" }, SearchForNewAlbum = true })
                .Build();

            Mocker.GetMock<IAlbumService>()
                .Setup(s => s.GetAlbumsByArtist(5))
                .Returns(new List<Album> { songModeAlbum });

            var matchedTrack = Builder<Track>.CreateNew().With(t => t.Id = 99).With(t => t.Title = "Track One").Build();

            Mocker.GetMock<ITrackService>()
                .Setup(s => s.SetMonitoredByTitle(10, It.IsAny<List<string>>()))
                .Returns(new List<Track> { matchedTrack });

            Subject.SearchForRecentlyAdded(5);

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(s => s.Push(It.Is<TrackSearchCommand>(c => c.TrackIds.Count == 1 && c.TrackIds.Contains(99)), CommandPriority.Normal, CommandTrigger.Unspecified), Times.Once);
        }

        [Test]
        public void should_not_double_search_song_mode_album_via_whole_album_search()
        {
            var songModeAlbum = Builder<Album>.CreateNew()
                .With(a => a.Id = 10)
                .With(a => a.AddOptions = new AddAlbumOptions { MonitorTrackTitles = new List<string> { "Track One" }, SearchForNewAlbum = true })
                .Build();

            Mocker.GetMock<IAlbumService>()
                .Setup(s => s.GetAlbumsByArtist(5))
                .Returns(new List<Album> { songModeAlbum });

            Mocker.GetMock<ITrackService>()
                .Setup(s => s.SetMonitoredByTitle(10, It.IsAny<List<string>>()))
                .Returns(new List<Track> { Builder<Track>.CreateNew().With(t => t.Id = 99).Build() });

            Subject.SearchForRecentlyAdded(5);

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(s => s.Push(It.IsAny<AlbumSearchCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never);
        }

        [Test]
        public void should_not_search_when_monitor_track_titles_populated_but_search_track_titles_empty()
        {
            var songModeAlbum = Builder<Album>.CreateNew()
                .With(a => a.Id = 10)
                .With(a => a.AddOptions = new AddAlbumOptions { MonitorTrackTitles = new List<string> { "Track One" }, SearchTrackTitles = new List<string>() })
                .Build();

            Mocker.GetMock<IAlbumService>()
                .Setup(s => s.GetAlbumsByArtist(5))
                .Returns(new List<Album> { songModeAlbum });

            var matchedTrack = Builder<Track>.CreateNew().With(t => t.Id = 99).Build();

            Mocker.GetMock<ITrackService>()
                .Setup(s => s.SetMonitoredByTitle(10, It.IsAny<List<string>>()))
                .Returns(new List<Track> { matchedTrack });

            Subject.SearchForRecentlyAdded(5);

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(s => s.Push(It.IsAny<TrackSearchCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never);
        }
    }
}
