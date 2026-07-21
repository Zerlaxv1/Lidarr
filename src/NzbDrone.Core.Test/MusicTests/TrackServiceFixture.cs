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
        public void should_relink_duplicate_titles_to_distinct_files()
        {
            var oldRelease = new AlbumRelease { Id = 1 };
            var newRelease = new AlbumRelease { Id = 2 };

            var oldTracks = new List<Track>
            {
                new Track { Id = 10, AlbumReleaseId = 1, ForeignRecordingId = "old-rec-1", Title = "Interlude", TrackFileId = 11, Monitored = true },
                new Track { Id = 11, AlbumReleaseId = 1, ForeignRecordingId = "old-rec-2", Title = "Interlude", TrackFileId = 22, Monitored = true }
            };

            var newTracks = new List<Track>
            {
                new Track { Id = 20, AlbumReleaseId = 2, ForeignRecordingId = "new-rec-1", Title = "Interlude", TrackFileId = 0, Monitored = true },
                new Track { Id = 21, AlbumReleaseId = 2, ForeignRecordingId = "new-rec-2", Title = "Interlude", TrackFileId = 0, Monitored = true }
            };

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByRelease(1))
                .Returns(oldTracks);

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByRelease(2))
                .Returns(newTracks);

            var result = Subject.RelinkTrackFilesToRelease(newRelease, new List<AlbumRelease> { oldRelease });

            result.Should().HaveCount(2);
            result.Single(t => t.Id == 20).TrackFileId.Should().Be(11);
            result.Single(t => t.Id == 21).TrackFileId.Should().Be(22);
            result.Select(t => t.TrackFileId).Should().OnlyHaveUniqueItems();
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

            // The unmonitored, fileless old track is not a valid match source, so no file is
            // relinked - but the new track's monitored flag is still mirrored: with no
            // monitored counterpart, it becomes unmonitored (and is reported as changed).
            result.Should().ContainSingle(t => t.Id == 20);
            newTracks.Single().TrackFileId.Should().Be(0);
            newTracks.Single().Monitored.Should().BeFalse();
        }

        [Test]
        public void should_monitor_only_matching_titles_and_unmonitor_the_rest()
        {
            var tracks = new List<Track>
            {
                new Track { Id = 1, Title = "Bliss", Monitored = false },
                new Track { Id = 2, Title = "Muscle Museum", Monitored = true },
                new Track { Id = 3, Title = "Uno", Monitored = true }
            };

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByAlbum(5))
                .Returns(tracks);

            var result = Subject.SetMonitoredByTitle(5, new List<string> { "Bliss" });

            result.Should().ContainSingle(t => t.Id == 1);
            tracks.Single(t => t.Id == 1).Monitored.Should().BeTrue();
            tracks.Single(t => t.Id == 2).Monitored.Should().BeFalse();
            tracks.Single(t => t.Id == 3).Monitored.Should().BeFalse();

            Mocker.GetMock<ITrackRepository>()
                .Verify(s => s.UpdateMany(tracks), Times.Once());
        }

        [Test]
        public void should_match_titles_case_and_punctuation_insensitively()
        {
            var tracks = new List<Track>
            {
                new Track { Id = 1, Title = "B.Y.O.B.", Monitored = false }
            };

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByAlbum(5))
                .Returns(tracks);

            var result = Subject.SetMonitoredByTitle(5, new List<string> { "byob" });

            result.Should().ContainSingle(t => t.Id == 1);
        }

        [Test]
        public void should_return_empty_when_no_title_matches()
        {
            var tracks = new List<Track>
            {
                new Track { Id = 1, Title = "Bliss", Monitored = true }
            };

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByAlbum(5))
                .Returns(tracks);

            var result = Subject.SetMonitoredByTitle(5, new List<string> { "Nonexistent" });

            result.Should().BeEmpty();
            tracks.Single().Monitored.Should().BeFalse();
        }

        [Test]
        public void should_unmonitor_new_release_tracks_with_no_monitored_counterpart()
        {
            var oldRelease = new AlbumRelease { Id = 1 };
            var newRelease = new AlbumRelease { Id = 2 };

            // Old monitored release: only "Bohemian Rhapsody" monitored, no files.
            var oldTracks = new List<Track>
            {
                new Track { Id = 10, AlbumReleaseId = 1, ForeignRecordingId = "rec-bohemian", Title = "Bohemian Rhapsody", TrackFileId = 0, Monitored = true },
                new Track { Id = 11, AlbumReleaseId = 1, ForeignRecordingId = "rec-death", Title = "Death on Two Legs", TrackFileId = 0, Monitored = false }
            };

            // New release tracks all default-monitored (as PrepareNewChild leaves them).
            var newTracks = new List<Track>
            {
                new Track { Id = 20, AlbumReleaseId = 2, ForeignRecordingId = "rec-bohemian", Title = "Bohemian Rhapsody", TrackFileId = 0, Monitored = true },
                new Track { Id = 21, AlbumReleaseId = 2, ForeignRecordingId = "rec-death", Title = "Death on Two Legs", TrackFileId = 0, Monitored = true },
                new Track { Id = 22, AlbumReleaseId = 2, ForeignRecordingId = "rec-bff", Title = "You're My Best Friend", TrackFileId = 0, Monitored = true }
            };

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByRelease(1))
                .Returns(oldTracks);

            Mocker.GetMock<ITrackRepository>()
                .Setup(s => s.GetTracksByRelease(2))
                .Returns(newTracks);

            Subject.RelinkTrackFilesToRelease(newRelease, new List<AlbumRelease> { oldRelease });

            // "Bohemian Rhapsody" stays monitored (had a monitored counterpart);
            // every other new-release track becomes unmonitored (no counterpart) -
            // "Death on Two Legs" is filtered out of oldTracks by the .Where(HasFile || Monitored)
            // guard since it is unmonitored with no file, so new track 21 has no counterpart.
            newTracks.Single(t => t.Id == 20).Monitored.Should().BeTrue();
            newTracks.Single(t => t.Id == 21).Monitored.Should().BeFalse();
            newTracks.Single(t => t.Id == 22).Monitored.Should().BeFalse();
        }
    }
}
