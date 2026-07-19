using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class TrackServiceFixture : CoreTest<TrackService>
    {
        [Test]
        public void should_relink_trackfiles_by_recording_id_when_switching_release()
        {
            var oldRelease = new AlbumRelease { Id = 1 };
            var newRelease = new AlbumRelease { Id = 2 };

            var oldTracks = new List<Track>
            {
                new Track { Id = 10, AlbumReleaseId = 1, ForeignRecordingId = "rec-1", Title = "Track One", TrackFileId = 100, Monitored = true },
                new Track { Id = 11, AlbumReleaseId = 1, ForeignRecordingId = "rec-2", Title = "Track Two", TrackFileId = 101, Monitored = false }
            };

            var newTracks = new List<Track>
            {
                new Track { Id = 20, AlbumReleaseId = 2, ForeignRecordingId = "rec-1", Title = "Track One", TrackFileId = 0, Monitored = true },
                new Track { Id = 21, AlbumReleaseId = 2, ForeignRecordingId = "rec-2", Title = "Track Two", TrackFileId = 0, Monitored = true }
            };

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByRelease(1))
                .Returns(oldTracks);

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByRelease(2))
                .Returns(newTracks);

            var result = Subject.RelinkTrackFilesToRelease(newRelease, new List<AlbumRelease> { oldRelease });

            result.Should().HaveCount(2);
            result.Single(t => t.Id == 20).TrackFileId.Should().Be(100);
            result.Single(t => t.Id == 20).Monitored.Should().BeTrue();
            result.Single(t => t.Id == 21).TrackFileId.Should().Be(101);
            result.Single(t => t.Id == 21).Monitored.Should().BeFalse();

            Mocker.GetMock<ITrackRepository>()
                .Verify(s => s.UpdateMany(It.Is<List<Track>>(l => l.Count == 2)), Times.Once());
        }

        [Test]
        public void should_fall_back_to_title_match_when_recording_id_does_not_match()
        {
            var oldRelease = new AlbumRelease { Id = 1 };
            var newRelease = new AlbumRelease { Id = 2 };

            var oldTracks = new List<Track>
            {
                new Track { Id = 10, AlbumReleaseId = 1, ForeignRecordingId = "old-rec-1", Title = "Muscle Museum", TrackFileId = 100, Monitored = true }
            };

            var newTracks = new List<Track>
            {
                new Track { Id = 20, AlbumReleaseId = 2, ForeignRecordingId = "new-rec-1", Title = "Muscle Museum", TrackFileId = 0, Monitored = true }
            };

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByRelease(1))
                .Returns(oldTracks);

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByRelease(2))
                .Returns(newTracks);

            var result = Subject.RelinkTrackFilesToRelease(newRelease, new List<AlbumRelease> { oldRelease });

            result.Should().ContainSingle(t => t.Id == 20 && t.TrackFileId == 100);
        }

        [Test]
        public void should_not_modify_old_release_tracks()
        {
            var oldRelease = new AlbumRelease { Id = 1 };
            var newRelease = new AlbumRelease { Id = 2 };

            var oldTracks = new List<Track>
            {
                new Track { Id = 10, AlbumReleaseId = 1, ForeignRecordingId = "rec-1", Title = "Track One", TrackFileId = 100, Monitored = true }
            };

            var newTracks = new List<Track>
            {
                new Track { Id = 20, AlbumReleaseId = 2, ForeignRecordingId = "rec-1", Title = "Track One", TrackFileId = 0, Monitored = true }
            };

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByRelease(1))
                .Returns(oldTracks);

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByRelease(2))
                .Returns(newTracks);

            Subject.RelinkTrackFilesToRelease(newRelease, new List<AlbumRelease> { oldRelease });

            oldTracks.Single().TrackFileId.Should().Be(100);
        }

        [Test]
        public void should_not_match_tracks_that_have_no_file_and_are_unmonitored()
        {
            var oldRelease = new AlbumRelease { Id = 1 };
            var newRelease = new AlbumRelease { Id = 2 };

            var oldTracks = new List<Track>
            {
                new Track { Id = 10, AlbumReleaseId = 1, ForeignRecordingId = "rec-1", Title = "Track One", TrackFileId = 0, Monitored = false }
            };

            var newTracks = new List<Track>
            {
                new Track { Id = 20, AlbumReleaseId = 2, ForeignRecordingId = "rec-1", Title = "Track One", TrackFileId = 0, Monitored = true }
            };

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByRelease(1))
                .Returns(oldTracks);

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByRelease(2))
                .Returns(newTracks);

            var result = Subject.RelinkTrackFilesToRelease(newRelease, new List<AlbumRelease> { oldRelease });

            result.Should().BeEmpty();
        }
    }
}
